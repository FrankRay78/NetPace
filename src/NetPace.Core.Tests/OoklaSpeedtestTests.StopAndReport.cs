using System;
using System.Collections.Generic;
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

            var speedtest = new OoklaSpeedtest(DownloadSettings(), mockHttp.ToHttpClient());
            var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

            // When
            var result = await speedtest.GetDownloadSpeedAsync(server, StopAfterFirstReport(stop), null, CancellationToken.None, stop.Token);

            // Then the call returns rather than throwing, and reports what the answered request
            // delivered. The request still in flight is neither a success nor a failure, so a
            // caller watching for failed requests sees none.
            result.BytesProcessed.ShouldBe(4096);
            result.RequestsSucceeded.ShouldBe(1);
            result.RequestsFailed.ShouldBe(0);
            result.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(0);
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
        private static IProgress<SpeedTestProgress> StopAfterFirstReport(CancellationTokenSource stop)
        {
            var stopped = false;

            return new SynchronousProgress<SpeedTestProgress>(_ =>
            {
                if (stopped)
                {
                    return;
                }

                stopped = true;
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

        /// <remarks>
        /// The last answered request is held back until the request after it has reached the
        /// server. Without that, a fast enough first answer can complete, report progress and so
        /// trigger the stop before the second request has even started, leaving nothing in flight.
        /// </remarks>
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

        #endregion
    }
}
