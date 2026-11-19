using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using NetPace.Core.Clients.Ookla;
using RichardSzalay.MockHttp;
using Shouldly;

namespace NetPace.Core.Tests;

public sealed partial class OoklaSpeedtestTests
{
    /// <summary>
    /// The stop-and-report token: a caller that asks a measurement to stop and hand back what it
    /// has, rather than to abandon it. A request still in flight counts as neither succeeded nor
    /// failed, and is recorded as cancelled with a reason of its own, so the caller can name
    /// whatever its own stop was for.
    /// </summary>
    public sealed class StopAndReport
    {
        private const string LatencyResponseBody = "test=test";

        [Fact]
        public async Task GetDownloadSpeedAsync_StoppedPartWayThrough_ReportsTheBytesAlreadyDelivered()
        {
            // Given four download requests, two at a time, where the first to be answered carries
            // data and anything after it never answers; the stop is asked for the moment that first
            // request has been counted.
            using var stop = new CancellationTokenSource();
            using var mockHttp = new MockHttpMessageHandler();
            AnswerOnceThenHang(mockHttp, payloadBytes: 4096);

            // The clock moves on 1.5 seconds before the stop, so the figure's duration is known.
            var clock = new ManualTimeProvider();
            var speedtest = new OoklaSpeedtest(DownloadSettings(), mockHttp.ToHttpClient(), timeProviderOverride: clock);
            var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

            // When
            var result = await speedtest.GetDownloadSpeedAsync(server, StopAfterFirstReport(stop, () => clock.Advance(1500)), null, CancellationToken.None, stop.Token);

            // Then the call returns rather than throwing, and reports what the answered request
            // delivered over the time the test ran. The request still in flight is neither a
            // success nor a failure, so a caller watching for failed requests sees none.
            result.BytesProcessed.ShouldBe(4096);
            result.ElapsedMilliseconds.ShouldBe(1500);
            result.RequestsSucceeded.ShouldBe(1);
            result.RequestsFailed.ShouldBe(0);
        }

        [Fact]
        public async Task GetDownloadSpeedAsync_StoppedPartWayThrough_RecordsTheStoppedRequestAsCancelled()
        {
            // Given the same run, with per-request detail collected.
            var diagnostics = new List<RequestDiagnostic>();
            using var stop = new CancellationTokenSource();
            using var mockHttp = new MockHttpMessageHandler();
            AnswerOnceThenHang(mockHttp, payloadBytes: 4096);

            var speedtest = new OoklaSpeedtest(DownloadSettings(), mockHttp.ToHttpClient());
            var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

            // When
            await speedtest.GetDownloadSpeedAsync(server, StopAfterFirstReport(stop), Collect(diagnostics), CancellationToken.None, stop.Token);

            // Then the request that was in flight reads as cancelled, carrying a reason that says
            // the caller stopped it rather than one that reads as a transport fault - and the
            // reason is the library's own generic wording, which a caller replaces with whatever
            // its own stop was for.
            RequestDiagnostic stopped = diagnostics.Single(diagnostic => diagnostic.Outcome == RequestOutcome.Cancelled);
            stopped.FailureReason.ShouldBe(RequestDiagnostic.StoppedByCaller);
            stopped.FailureType.ShouldBeNull();

            diagnostics.Count(diagnostic => diagnostic.Outcome == RequestOutcome.Succeeded).ShouldBe(1);
            diagnostics.ShouldNotContain(diagnostic => diagnostic.Outcome == RequestOutcome.Failed);
        }

        [Fact]
        public async Task GetDownloadSpeedAsync_StoppedPartWayThroughARequest_ReportsWhatThatRequestHadDelivered()
        {
            // Given one request at a time, where the server sends part of the body and then goes
            // quiet: a slow link, where one request outlasts the stop. The stop is asked for the
            // moment the transfer is left waiting on more of that body.
            using var stop = new CancellationTokenSource();
            using var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("*").Respond(_ => new HttpResponseMessage { Content = new StreamContent(new TrickleThenHangStream(2048, stop.Cancel)) });

            var settings = DownloadSettings() with { DownloadTest = new() { DownloadParallelTasks = 1 } };
            var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient());
            var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

            // When
            var result = await speedtest.GetDownloadSpeedAsync(server, null, null, CancellationToken.None, stop.Token);

            // Then the bytes the unfinished request carried are in the figure, and the request
            // itself counts as neither a success nor a failure.
            result.BytesProcessed.ShouldBe(2048);
            result.RequestsSucceeded.ShouldBe(0);
            result.RequestsFailed.ShouldBe(0);
        }

        [Fact]
        public async Task GetUploadSpeedAsync_StoppedPartWayThrough_ReportsTheBytesAlreadyDelivered()
        {
            // Given four uploads, two at a time. The endpoint probe that resolves where uploads go
            // is answered first, then one upload, and anything after it never answers.
            using var stop = new CancellationTokenSource();
            using var mockHttp = new MockHttpMessageHandler();
            AnswerTwiceThenHang(mockHttp);

            var settings = new OoklaSpeedtestSettings
            {
                LatencyTest = new() { LoadedLatencyEnabled = false },
                UploadTest = new() { UploadIncrements = 1, UploadSizeIncrementKb = 4, UploadSizeIterations = 4, UploadParallelTasks = 2 }
            };
            var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient());
            var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

            // When
            var result = await speedtest.GetUploadSpeedAsync(server, StopAfterFirstReport(stop), null, CancellationToken.None, stop.Token);

            // Then
            result.BytesProcessed.ShouldBeGreaterThan(0);
            result.RequestsSucceeded.ShouldBe(1);
            result.RequestsFailed.ShouldBe(0);
        }

        [Fact]
        public async Task GetDownloadSpeedAsync_StoppedBeforeItBegins_ReportsNothingMeasuredAndNoFailure()
        {
            // Given a stop already asked for when the measurement is called.
            using var stop = new CancellationTokenSource();
            await stop.CancelAsync();

            using var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("*").Respond(_ => new HttpResponseMessage { Content = new ByteArrayContent(new byte[4096]) });

            var speedtest = new OoklaSpeedtest(DownloadSettings(), mockHttp.ToHttpClient());
            var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

            // When
            var result = await speedtest.GetDownloadSpeedAsync(server, null, null, CancellationToken.None, stop.Token);

            // Then nothing was measured, and nothing failed: a caller checking for failed requests
            // must not read a stop as one.
            result.BytesProcessed.ShouldBe(0);
            result.RequestsSucceeded.ShouldBe(0);
            result.RequestsFailed.ShouldBe(0);
        }

        [Fact]
        public async Task GetDownloadSpeedAsync_CancelledByTheCaller_StillThrows()
        {
            // Given the caller's own token cancelled rather than the stop-and-report one.
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();

            using var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("*").Respond(_ => new HttpResponseMessage { Content = new ByteArrayContent(new byte[4096]) });

            var speedtest = new OoklaSpeedtest(DownloadSettings(), mockHttp.ToHttpClient());
            var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

            // When / Then the two tokens stay distinct: asking to stop and report must not have
            // turned a genuine cancellation into a result.
            await Should.ThrowAsync<OperationCanceledException>(async () =>
                await speedtest.GetDownloadSpeedAsync(server, null, null, cancellation.Token, CancellationToken.None));
        }

        [Fact]
        public async Task GetServerLatencyAsync_StoppedPartWayThrough_ReportsTheProbesAlreadyAnswered()
        {
            // Given a latency measurement of four probes, stopped once two have answered.
            using var stop = new CancellationTokenSource();
            using var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("*").Respond("text/plain", LatencyResponseBody);

            var speedtest = new OoklaSpeedtest(LatencySettings(), mockHttp.ToHttpClient());
            var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

            var reports = 0;
            var progress = new SynchronousProgress<LatencyTestProgress>(_ =>
            {
                if (++reports == 2)
                {
                    stop.Cancel();
                }
            });

            // When
            var result = await speedtest.GetServerLatencyAsync(server, progress, null, CancellationToken.None, stop.Token);

            // Then the figures are those of the probes that answered, not of the four that were
            // planned: the measurement reports what it took rather than failing.
            reports.ShouldBe(2);
            result.Server.Url.ShouldBe(server.Url);
            result.MinimumLatencyMilliseconds.ShouldNotBeNull();
            result.MaximumLatencyMilliseconds.ShouldNotBeNull();
        }

        [Fact]
        public async Task GetServerLatencyAsync_StoppedBeforeAnyProbeAnswered_ReportsNothingToReport()
        {
            // Given a stop already asked for when the measurement is called.
            using var stop = new CancellationTokenSource();
            await stop.CancelAsync();

            using var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("*").Respond("text/plain", LatencyResponseBody);

            var speedtest = new OoklaSpeedtest(LatencySettings(), mockHttp.ToHttpClient());
            var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

            // When / Then no probe answered, so there is no figure to hand back and the stop reads
            // as a cancellation. A latency of zero would be a measurement claiming a perfect line.
            await Should.ThrowAsync<OperationCanceledException>(async () =>
                await speedtest.GetServerLatencyAsync(server, null, null, CancellationToken.None, stop.Token));
        }

        [Fact]
        public async Task GetServerLatencyAsync_StoppedInThePauseBetweenProbes_ReportsTheProbesAlreadyAnswered()
        {
            // Given probes paced apart, with the stop landing in the first pause, after one probe
            // has answered.
            using var stop = new CancellationTokenSource();
            using var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("*").Respond("text/plain", LatencyResponseBody);

            var settings = new OoklaSpeedtestSettings
            {
                LatencyTest = new() { LatencyTestIterations = 4, LatencyTestIntervalMilliseconds = 50 }
            };
            var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient(), new StopWhenPaused(stop));
            var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

            // When
            var result = await speedtest.GetServerLatencyAsync(server, null, null, CancellationToken.None, stop.Token);

            // Then the answered probe is the measurement, rather than the pause turning the stop
            // into a cancellation that throws it away.
            result.Server.Url.ShouldBe(server.Url);
            result.MinimumLatencyMilliseconds.ShouldNotBeNull();
        }

        [Fact]
        public async Task GetServerLatencyAsync_StoppedWhileAProbeIsOut_RecordsItAsStoppedAndReportsTheOthers()
        {
            // Given a first probe that answers and a second that is still out when the stop lands.
            var diagnostics = new List<RequestDiagnostic>();
            using var stop = new CancellationTokenSource();
            using var mockHttp = new MockHttpMessageHandler();
            var arrived = 0;
            mockHttp.When("*").Respond(async _ =>
            {
                if (Interlocked.Increment(ref arrived) == 1)
                {
                    return new HttpResponseMessage { Content = new StringContent(LatencyResponseBody) };
                }

                await stop.CancelAsync();
                return await new TaskCompletionSource<HttpResponseMessage>().Task;
            });

            var speedtest = new OoklaSpeedtest(LatencySettings(), mockHttp.ToHttpClient());
            var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

            // When
            var result = await speedtest.GetServerLatencyAsync(server, null, Collect(diagnostics), CancellationToken.None, stop.Token);

            // Then the answered probe is the figure, and the one that was out reads as stopped
            // rather than as a failed probe.
            result.MinimumLatencyMilliseconds.ShouldNotBeNull();

            RequestDiagnostic stopped = diagnostics.Single(diagnostic => diagnostic.Outcome == RequestOutcome.Cancelled);
            stopped.FailureReason.ShouldBe(RequestDiagnostic.StoppedByCaller);
            diagnostics.ShouldNotContain(diagnostic => diagnostic.Outcome == RequestOutcome.Failed);
        }

        [Fact]
        public async Task GetUploadSpeedAsync_StoppedWhileFindingWhereUploadsGo_ReportsNothingMeasuredAndSendsNoUpload()
        {
            // Given a server that never answers the request finding where uploads go, with the stop
            // landing while it is out.
            var diagnostics = new List<RequestDiagnostic>();
            using var stop = new CancellationTokenSource();
            using var mockHttp = new MockHttpMessageHandler();
            var arrived = 0;
            mockHttp.When("*").Respond(async _ =>
            {
                Interlocked.Increment(ref arrived);
                await stop.CancelAsync();
                return await new TaskCompletionSource<HttpResponseMessage>().Task;
            });

            var settings = new OoklaSpeedtestSettings
            {
                LatencyTest = new() { LoadedLatencyEnabled = false },
                UploadTest = new() { UploadIncrements = 1, UploadSizeIncrementKb = 4, UploadSizeIterations = 4, UploadParallelTasks = 2 }
            };
            var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient());
            var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

            // When
            var result = await speedtest.GetUploadSpeedAsync(server, null, Collect(diagnostics), CancellationToken.None, stop.Token);

            // Then the stop ends the test where it was, rather than being waited out, and no
            // upload is sent after it.
            arrived.ShouldBe(1);
            result.BytesProcessed.ShouldBe(0);
            result.RequestsSucceeded.ShouldBe(0);
            result.RequestsFailed.ShouldBe(0);
            diagnostics.Single().FailureReason.ShouldBe(RequestDiagnostic.StoppedByCaller);
        }

        #region --- Helpers ---

        /// <summary>Four download requests, two at a time, with no under-load probing beside them.</summary>
        private static OoklaSpeedtestSettings DownloadSettings() => new()
        {
            DownloadSizes = [100],
            DownloadSizeIterations = 4,
            LatencyTest = new() { LoadedLatencyEnabled = false },
            DownloadTest = new() { DownloadParallelTasks = 2 }
        };

        /// <summary>Four latency probes, back to back, so nothing waits on the wall clock.</summary>
        private static OoklaSpeedtestSettings LatencySettings() => new()
        {
            LatencyTest = new() { LatencyTestIterations = 4, LatencyTestIntervalMilliseconds = 0 }
        };

        /// <summary>
        /// Asks for the stop as soon as the transfer reports its first progress. That report is
        /// raised once the answered request has been counted, so the stop lands with exactly one
        /// request counted and one still in flight.
        /// </summary>
        private static IProgress<SpeedTestProgress> StopAfterFirstReport(CancellationTokenSource stop, Action? beforeStopping = null)
        {
            var stopped = false;

            return new SynchronousProgress<SpeedTestProgress>(_ =>
            {
                if (stopped)
                {
                    return;
                }

                stopped = true;
                beforeStopping?.Invoke();
                stop.Cancel();
            });
        }

        /// <summary>
        /// Answers the first request with <paramref name="payloadBytes"/> of data and never answers
        /// another, so one request is counted while the next is still in flight when the stop
        /// arrives.
        /// </summary>
        private static void AnswerOnceThenHang(MockHttpMessageHandler mockHttp, int payloadBytes) =>
            AnswerThenHang(mockHttp, answers: 1, payloadBytes);

        /// <summary>
        /// As <see cref="AnswerOnceThenHang"/>, but two requests are answered: the upload path
        /// resolves where uploads go with a probe POST before the measured uploads begin.
        /// </summary>
        private static void AnswerTwiceThenHang(MockHttpMessageHandler mockHttp) =>
            AnswerThenHang(mockHttp, answers: 2, payloadBytes: 0);

        /// <summary>
        /// Answers the first <paramref name="answers"/> requests and never answers another. The last
        /// answered request is held back until the request after it has reached the server.
        /// Without that, a fast enough answer can complete, report progress and so trigger the
        /// stop before the next request has even started, leaving nothing in flight.
        /// </summary>
        private static void AnswerThenHang(MockHttpMessageHandler mockHttp, int answers, int payloadBytes)
        {
            var arrived = 0;
            var nextRequestInFlight = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            mockHttp.When("*").Respond(async _ =>
            {
                var order = Interlocked.Increment(ref arrived);

                if (order < answers)
                {
                    return Answer(payloadBytes);
                }

                if (order == answers)
                {
                    await nextRequestInFlight.Task;
                    return Answer(payloadBytes);
                }

                nextRequestInFlight.TrySetResult();
                return await new TaskCompletionSource<HttpResponseMessage>().Task;
            });

            static HttpResponseMessage Answer(int payloadBytes) => new() { Content = new ByteArrayContent(new byte[payloadBytes]) };
        }

        /// <summary>
        /// Collects reported diagnostics inline, under a lock: a parallel transfer reports from
        /// several requests at once, so an unsynchronised list would tear.
        /// </summary>
        private static IProgress<RequestDiagnostic> Collect(List<RequestDiagnostic> sink) =>
            new SynchronousProgress<RequestDiagnostic>(diagnostic =>
            {
                lock (sink)
                {
                    sink.Add(diagnostic);
                }
            });

        /// <summary>
        /// A clock that moves only when a test moves it, so a measured duration is known exactly.
        /// </summary>
        private sealed class ManualTimeProvider : TimeProvider
        {
            private long now;

            public override long TimestampFrequency => 1000;

            public override long GetTimestamp() => Interlocked.Read(ref now);

            public void Advance(long milliseconds) => Interlocked.Add(ref now, milliseconds);
        }

        /// <summary>
        /// A response body that yields <paramref name="bytes"/> on its first read and then never
        /// yields again, calling <paramref name="onHang"/> once it is waiting, so a test can stop
        /// the transfer while a request is part way through its body.
        /// </summary>
        private sealed class TrickleThenHangStream(int bytes, Action onHang) : Stream
        {
            private bool delivered;

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (!delivered)
                {
                    delivered = true;
                    var count = Math.Min(bytes, buffer.Length);
                    buffer.Span[..count].Clear();
                    return count;
                }

                onHang();
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return 0;
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
                ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        /// <summary>
        /// A pause between latency probes that asks for the stop and then ends as a pause on a
        /// cancelled token does.
        /// </summary>
        private sealed class StopWhenPaused(CancellationTokenSource stop) : IDelayProvider
        {
            public Task DelayAsync(int milliseconds, CancellationToken cancellationToken = default)
            {
                stop.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        }

        #endregion
    }
}
