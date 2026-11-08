using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using NetPace.Core.Clients.Ookla;
using NetPace.Core.Clients.Ookla.Settings;
using RichardSzalay.MockHttp;
using Shouldly;

namespace NetPace.Core.Tests;

public sealed partial class OoklaSpeedtestTests
{
    /// <summary>
    /// Latency under load: the small probes a download or an upload sends to the server it is
    /// using, and the figures the transfer's result then reports for them.
    /// </summary>
    /// <remarks>
    /// Every test here paces the probes itself through <see cref="GatedDelayProvider"/> and times
    /// them on a <see cref="ControlledTimeProvider"/>, so a figure asserted below is exactly what
    /// the test said it was rather than a wall-clock reading. The transfer is held open while the
    /// probes it is being measured under go out, which is also what makes the probe count the
    /// test's to decide: a transfer that ended first would simply report nothing measured.
    /// <para>
    /// A probe's answer is observed through the diagnostics channel rather than from the responder
    /// that answered it. The responder returns before the probe's elapsed figure has been read, so
    /// a test that released the next probe on the responder would let that probe advance the clock
    /// inside the previous probe's measurement. The diagnostic report is the seam that fires after
    /// the sample has been taken.
    /// </para>
    /// </remarks>
    public sealed class LoadedLatency
    {
        private const string ServerUrl = "http://example.com/";
        private const string LatencyUrl = "http://example.com/latency.txt";
        private const string DownloadUrlPattern = "http://example.com/random*";
        private const string ProbeResponseBody = "test=test";

        /// <summary>The bytes a probe's answer carries, which must stay out of a transfer's total.</summary>
        private const int ProbeResponseBytes = 9;

        /// <summary>The bytes one download request answers with.</summary>
        private const int DownloadResponseBytes = 2048;

        /// <summary>How long the held download takes once the test releases it, on the test's clock.</summary>
        private static readonly TimeSpan TransferDuration = TimeSpan.FromMilliseconds(500);

        private static readonly TimeSpan HangGuard = TimeSpan.FromSeconds(10);

        private static readonly DateTimeOffset ClockStart = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// One instance, not a fresh one per read: <see cref="Server"/> compares by reference, so a
        /// test asserting that a result names the server it measured needs the same object back.
        /// </summary>
        private static readonly IServer TestServer = new Server { Url = ServerUrl, Sponsor = "Sponsor", Location = "Location" };

        private static OoklaSpeedtestSettings SingleDownloadSettings(LatencyTestSettings? latencyTest = null) =>
            new()
            {
                DownloadTest = new()
                {
                    DownloadSizes = [100],
                    DownloadSizeIterations = 1,
                    DownloadParallelTasks = 1
                },
                LatencyTest = latencyTest ?? new LatencyTestSettings()
            };

        private static OoklaSpeedtestSettings SingleUploadSettings() =>
            new()
            {
                UploadTest = new()
                {
                    UploadIncrements = 1,
                    UploadSizeIterations = 1,
                    UploadParallelTasks = 1
                }
            };

        /// <summary>
        /// Collects every record reported, and signals each time one of them is a latency probe, so
        /// a test can step the probes one at a time.
        /// </summary>
        private sealed class ProbeWatcher
        {
            private readonly List<RequestDiagnostic> records = [];
            private readonly SemaphoreSlim reported = new(0);

            public IProgress<RequestDiagnostic> Reporter { get; }

            public ProbeWatcher()
            {
                Reporter = new SynchronousProgress<RequestDiagnostic>(record =>
                {
                    lock (records)
                    {
                        records.Add(record);
                    }

                    if (record.Kind == RequestKind.LatencyProbe)
                    {
                        reported.Release();
                    }
                });
            }

            public IReadOnlyList<RequestDiagnostic> All
            {
                get
                {
                    lock (records)
                    {
                        return records.ToArray();
                    }
                }
            }

            public IReadOnlyList<RequestDiagnostic> Probes => All.Where(record => record.Kind == RequestKind.LatencyProbe).ToArray();

            /// <summary>
            /// Waits until one more probe has been reported, which is the point at which its sample
            /// has been taken.
            /// </summary>
            public Task<bool> NextProbeReportedAsync() => reported.WaitAsync(HangGuard);
        }

        /// <summary>
        /// Answers a probe after moving <paramref name="clock"/> on by the next of
        /// <paramref name="roundTripMilliseconds"/>, so each probe takes exactly the time named.
        /// Probes never overlap, so the times are handed out in order with nothing to synchronise.
        /// </summary>
        private static void RespondToProbes(MockHttpMessageHandler mockHttp, ControlledTimeProvider clock, params int[] roundTripMilliseconds)
        {
            var times = new Queue<int>(roundTripMilliseconds);

            mockHttp.When(HttpMethod.Get, LatencyUrl)
                    .Respond(_ =>
                    {
                        clock.Advance(TimeSpan.FromMilliseconds(times.Dequeue()));
                        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ProbeResponseBody) };
                    });
        }

        [Fact]
        public async Task GetDownloadSpeedAsync_WithProbesThatAnsweredKnownTimes_ReportsTheirAverageLowestHighestAndJitter()
        {
            // SCENARIO: A transfer reports the latency measured during it

            // Given a download held open while three probes answer in 40, 60 and 20 ms of a clock
            // the test moves itself.
            var clock = new ControlledTimeProvider(ClockStart);
            var delays = new GatedDelayProvider();
            var watcher = new ProbeWatcher();
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            using var mockHttp = new MockHttpMessageHandler();
            RespondToProbes(mockHttp, clock, 40, 60, 20);
            mockHttp.When(DownloadUrlPattern)
                    .Respond(async _ =>
                    {
                        await release.Task.WaitAsync(HangGuard);
                        clock.Advance(TransferDuration);
                        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[DownloadResponseBytes]) };
                    });

            var speedtest = new OoklaSpeedtest(SingleDownloadSettings(), mockHttp.ToHttpClient(), delays, clock);

            // When the three probes have each gone out and answered, and the download then finishes.
            var download = speedtest.GetDownloadSpeedAsync(TestServer, null, watcher.Reporter);

            for (var probe = 0; probe < 3; probe++)
            {
                (await delays.ReleaseNextAsync(HangGuard)).ShouldBeTrue();
                (await watcher.NextProbeReportedAsync()).ShouldBeTrue();
            }

            release.SetResult();
            var result = await download.WaitAsync(HangGuard);

            // Then the result carries the figures of those three round trips.
            result.Latency.ShouldNotBeNull();
            result.Latency!.Server.ShouldBe(TestServer);
            result.Latency.LatencyMilliseconds.ShouldBe(40);
            result.Latency.MinimumLatencyMilliseconds.ShouldBe(20);
            result.Latency.MaximumLatencyMilliseconds.ShouldBe(60);
            result.Latency.JitterMilliseconds.ShouldBe(30);

            // And its speed comes from the transfer's own bytes and elapsed time alone: the probes'
            // bytes are not in the total, the probes are not in the counts, and the elapsed figure
            // is the transfer's own window rather than the probe durations added to it.
            result.BytesProcessed.ShouldBe(DownloadResponseBytes);
            result.RequestsSucceeded.ShouldBe(1);
            result.RequestsFailed.ShouldBe(0);
            result.ElapsedMilliseconds.ShouldBe(620);
        }

        [Fact]
        public async Task GetUploadSpeedAsync_WithProbesThatAnsweredKnownTimes_ReportsTheirFiguresToo()
        {
            // Given an upload whose endpoint probe answers at once and whose one measured upload is
            // held open while two latency probes answer in 30 and 50 ms.
            var clock = new ControlledTimeProvider(ClockStart);
            var delays = new GatedDelayProvider();
            var watcher = new ProbeWatcher();
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var posts = 0;

            using var mockHttp = new MockHttpMessageHandler();
            RespondToProbes(mockHttp, clock, 30, 50);
            mockHttp.When(HttpMethod.Post, ServerUrl)
                    .Respond(async _ =>
                    {
                        // The first POST is the endpoint probe, made before the measured uploads.
                        if (Interlocked.Increment(ref posts) == 1)
                        {
                            return new HttpResponseMessage(HttpStatusCode.OK);
                        }

                        await release.Task.WaitAsync(HangGuard);
                        clock.Advance(TransferDuration);
                        return new HttpResponseMessage(HttpStatusCode.OK);
                    });

            var speedtest = new OoklaSpeedtest(SingleUploadSettings(), mockHttp.ToHttpClient(), delays, clock);

            // When both probes have answered and the upload then finishes.
            var upload = speedtest.GetUploadSpeedAsync(TestServer, null, watcher.Reporter);

            for (var probe = 0; probe < 2; probe++)
            {
                (await delays.ReleaseNextAsync(HangGuard)).ShouldBeTrue();
                (await watcher.NextProbeReportedAsync()).ShouldBeTrue();
            }

            release.SetResult();
            var result = await upload.WaitAsync(HangGuard);

            // Then the upload's result carries the figures of those two round trips.
            result.Latency.ShouldNotBeNull();
            result.Latency!.LatencyMilliseconds.ShouldBe(40);
            result.Latency.MinimumLatencyMilliseconds.ShouldBe(30);
            result.Latency.MaximumLatencyMilliseconds.ShouldBe(50);
            result.Latency.JitterMilliseconds.ShouldBe(20);

            // And the endpoint probe and the latency probes are both outside the measured counts.
            result.RequestsSucceeded.ShouldBe(1);
            result.RequestsFailed.ShouldBe(0);
        }

        [Fact]
        public async Task GetDownloadSpeedAsync_BeforeItsFirstProbe_WaitsOneConfiguredInterval()
        {
            // Given a download held open, and the default probe interval.
            var clock = new ControlledTimeProvider(ClockStart);
            var delays = new GatedDelayProvider();
            var watcher = new ProbeWatcher();
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            using var mockHttp = new MockHttpMessageHandler();
            RespondToProbes(mockHttp, clock, 40);
            mockHttp.When(DownloadUrlPattern)
                    .Respond(async _ =>
                    {
                        await release.Task.WaitAsync(HangGuard);
                        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[DownloadResponseBytes]) };
                    });

            var speedtest = new OoklaSpeedtest(SingleDownloadSettings(), mockHttp.ToHttpClient(), delays, clock);

            // When the download starts and the first wait is asked for.
            var download = speedtest.GetDownloadSpeedAsync(TestServer, null, watcher.Reporter);
            (await delays.ReleaseNextAsync(HangGuard)).ShouldBeTrue();
            (await watcher.NextProbeReportedAsync()).ShouldBeTrue();

            release.SetResult();
            await download.WaitAsync(HangGuard);

            // Then the wait before that probe was the configured 250 ms, and nothing was probed
            // before it - a probe at the transfer's start would see a line with no queue on it yet.
            delays.RequestedIntervals.First().ShouldBe(250);
            watcher.Probes.Count.ShouldBe(1);
        }

        [Fact]
        public async Task GetDownloadSpeedAsync_WithOneProbeThatFailed_TakesItsFiguresFromTheOthers()
        {
            // SCENARIO: A failed probe is skipped

            // Given a download held open while five probes go out, the third of which errors.
            var clock = new ControlledTimeProvider(ClockStart);
            var delays = new GatedDelayProvider();
            var watcher = new ProbeWatcher();
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var times = new Queue<int>([10, 20, 30, 40]);
            var probesIssued = 0;

            using var mockHttp = new MockHttpMessageHandler();
            mockHttp.When(HttpMethod.Get, LatencyUrl)
                    .Respond(_ =>
                    {
                        if (++probesIssued == 3)
                        {
                            throw new HttpRequestException("connection reset");
                        }

                        clock.Advance(TimeSpan.FromMilliseconds(times.Dequeue()));
                        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ProbeResponseBody) };
                    });
            mockHttp.When(DownloadUrlPattern)
                    .Respond(async _ =>
                    {
                        await release.Task.WaitAsync(HangGuard);
                        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[DownloadResponseBytes]) };
                    });

            var speedtest = new OoklaSpeedtest(SingleDownloadSettings(), mockHttp.ToHttpClient(), delays, clock);

            // When all five have been and gone, and the download then finishes.
            var download = speedtest.GetDownloadSpeedAsync(TestServer, null, watcher.Reporter);

            for (var probe = 0; probe < 5; probe++)
            {
                (await delays.ReleaseNextAsync(HangGuard)).ShouldBeTrue();
                (await watcher.NextProbeReportedAsync()).ShouldBeTrue();
            }

            release.SetResult();
            var result = await download.WaitAsync(HangGuard);

            // Then the figures come from the four that answered - 10, 20, 30 and 40 ms.
            result.Latency.ShouldNotBeNull();
            result.Latency!.LatencyMilliseconds.ShouldBe(25);
            result.Latency.MinimumLatencyMilliseconds.ShouldBe(10);
            result.Latency.MaximumLatencyMilliseconds.ShouldBe(40);
            result.Latency.JitterMilliseconds.ShouldBe(10);

            // And the probe that errored is recorded as a failure rather than dropped silently.
            watcher.Probes.Count(record => record.Outcome == RequestOutcome.Succeeded).ShouldBe(4);
            watcher.Probes.Count(record => record.Outcome == RequestOutcome.Failed).ShouldBe(1);
        }

        [Fact]
        public async Task GetDownloadSpeedAsync_WithEveryProbeFailing_StillSucceedsAndReportsNoLatency()
        {
            // SCENARIO: Failed probes do not fail a transfer

            // Given a download held open while three probes go out, every one of which errors.
            var clock = new ControlledTimeProvider(ClockStart);
            var delays = new GatedDelayProvider();
            var watcher = new ProbeWatcher();
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            using var mockHttp = new MockHttpMessageHandler();
            mockHttp.When(HttpMethod.Get, LatencyUrl).Throw(new HttpRequestException("connection reset"));
            mockHttp.When(DownloadUrlPattern)
                    .Respond(async _ =>
                    {
                        await release.Task.WaitAsync(HangGuard);
                        clock.Advance(TransferDuration);
                        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[DownloadResponseBytes]) };
                    });

            var speedtest = new OoklaSpeedtest(SingleDownloadSettings(), mockHttp.ToHttpClient(), delays, clock);

            // When all three have failed and the download then finishes.
            var download = speedtest.GetDownloadSpeedAsync(TestServer, null, watcher.Reporter);

            for (var probe = 0; probe < 3; probe++)
            {
                (await delays.ReleaseNextAsync(HangGuard)).ShouldBeTrue();
                (await watcher.NextProbeReportedAsync()).ShouldBeTrue();
            }

            release.SetResult();
            var result = await download.WaitAsync(HangGuard);

            // Then the download succeeded with its speed, and says under-load latency was not measured.
            result.BytesProcessed.ShouldBe(DownloadResponseBytes);
            result.RequestsSucceeded.ShouldBe(1);
            result.RequestsFailed.ShouldBe(0);
            result.Latency.ShouldBeNull();
        }

        [Fact]
        public async Task GetDownloadSpeedAsync_WithAProbeStillWaitingAtTheEnd_DiscardsItAndReportsNoLatency()
        {
            // SCENARIO: A probe still waiting at the end is discarded

            // Given a download whose only probe never answers.
            var clock = new ControlledTimeProvider(ClockStart);
            var delays = new GatedDelayProvider();
            var watcher = new ProbeWatcher();
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var probeIssued = new SemaphoreSlim(0);

            using var mockHttp = new MockHttpMessageHandler();
            mockHttp.When(HttpMethod.Get, LatencyUrl)
                    .Respond(_ =>
                    {
                        probeIssued.Release();
                        return new TaskCompletionSource<HttpResponseMessage>().Task;
                    });
            mockHttp.When(DownloadUrlPattern)
                    .Respond(async _ =>
                    {
                        await release.Task.WaitAsync(HangGuard);
                        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[DownloadResponseBytes]) };
                    });

            var speedtest = new OoklaSpeedtest(SingleDownloadSettings(), mockHttp.ToHttpClient(), delays, clock);

            // When the probe is outstanding and the download finishes under it.
            var download = speedtest.GetDownloadSpeedAsync(TestServer, null, watcher.Reporter);
            (await delays.ReleaseNextAsync(HangGuard)).ShouldBeTrue();
            (await probeIssued.WaitAsync(HangGuard)).ShouldBeTrue();

            release.SetResult();

            // Then the result is produced without waiting on the probe, and says not measured.
            var result = await download.WaitAsync(HangGuard);
            result.Latency.ShouldBeNull();

            // And that one probe was recorded as abandoned rather than left unaccounted for. Only
            // one went out: a second would have meant the loop probing while one was outstanding.
            var probe = watcher.Probes.ShouldHaveSingleItem();
            probe.Outcome.ShouldBe(RequestOutcome.Cancelled);
        }

        [Fact]
        public async Task GetDownloadSpeedAsync_EndingBeforeTheFirstProbeIsDue_ReportsNoLatency()
        {
            // SCENARIO: A transfer shorter than the interval is not measured

            // Given a download that answers at once, and a probe interval that never elapses.
            var clock = new ControlledTimeProvider(ClockStart);
            var delays = new GatedDelayProvider();
            var watcher = new ProbeWatcher();

            using var mockHttp = new MockHttpMessageHandler();
            RespondToProbes(mockHttp, clock, 40);
            mockHttp.When(DownloadUrlPattern)
                    .Respond(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[DownloadResponseBytes]) });

            var speedtest = new OoklaSpeedtest(SingleDownloadSettings(), mockHttp.ToHttpClient(), delays, clock);

            // When the download runs to completion.
            var result = await speedtest.GetDownloadSpeedAsync(TestServer, null, watcher.Reporter).WaitAsync(HangGuard);

            // Then no probe was sent, and the result says under-load latency was not measured.
            watcher.Probes.ShouldBeEmpty();
            result.Latency.ShouldBeNull();
            result.BytesProcessed.ShouldBe(DownloadResponseBytes);
        }

        [Fact]
        public async Task GetDownloadSpeedAsync_WhoseRequestsAllFailed_ReportsNoLatencyEvenThoughProbesAnswered()
        {
            // SCENARIO: A transfer whose requests all failed is not measured

            // Given a download whose two requests are both rejected, held until a probe has answered.
            var clock = new ControlledTimeProvider(ClockStart);
            var delays = new GatedDelayProvider();
            var watcher = new ProbeWatcher();
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var settings = new OoklaSpeedtestSettings
            {
                DownloadTest = new()
                {
                    DownloadSizes = [100],
                    DownloadSizeIterations = 2,
                    DownloadParallelTasks = 2
                }
            };

            using var mockHttp = new MockHttpMessageHandler();
            RespondToProbes(mockHttp, clock, 40);
            mockHttp.When(DownloadUrlPattern)
                    .Respond(async _ =>
                    {
                        await release.Task.WaitAsync(HangGuard);
                        return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                    });

            var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient(), delays, clock);

            // When the probe has answered and the download's requests then all fail.
            var download = speedtest.GetDownloadSpeedAsync(TestServer, null, watcher.Reporter);
            (await delays.ReleaseNextAsync(HangGuard)).ShouldBeTrue();
            (await watcher.NextProbeReportedAsync()).ShouldBeTrue();

            release.SetResult();
            var result = await download.WaitAsync(HangGuard);

            // Then nothing moved, so the probes did not see a loaded line and nothing is reported
            // for them - even though one of them answered.
            result.RequestsSucceeded.ShouldBe(0);
            result.RequestsFailed.ShouldBe(2);
            watcher.Probes.Count(record => record.Outcome == RequestOutcome.Succeeded).ShouldBe(1);
            result.Latency.ShouldBeNull();
        }

        [Fact]
        public async Task GetDownloadSpeedAsync_WithTheMeasurementSwitchedOff_SendsNoProbeAndReportsNoLatency()
        {
            // SCENARIO: Under-load measurement can be switched off

            // Given settings with the under-load measurement switched off.
            var clock = new ControlledTimeProvider(ClockStart);
            var delays = new GatedDelayProvider();
            var watcher = new ProbeWatcher();

            using var mockHttp = new MockHttpMessageHandler();
            RespondToProbes(mockHttp, clock, 40);
            mockHttp.When(DownloadUrlPattern)
                    .Respond(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[DownloadResponseBytes]) });

            var settings = SingleDownloadSettings(new LatencyTestSettings { LoadedLatencyEnabled = false });
            var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient(), delays, clock);

            // When a download runs.
            var result = await speedtest.GetDownloadSpeedAsync(TestServer, null, watcher.Reporter).WaitAsync(HangGuard);

            // Then nothing waited for a probe interval, no probe was sent, and the result says
            // under-load latency was not measured.
            delays.RequestedIntervals.ShouldBeEmpty();
            watcher.Probes.ShouldBeEmpty();
            result.Latency.ShouldBeNull();
        }

        [Fact]
        public async Task GetDownloadSpeedAsync_WithProbesThatAnsweredFailedAndWereAbandoned_ReportsAllThreeAsProbes()
        {
            // SCENARIO: Probes are reported to diagnostics

            // Given a download held open while one probe answers, one errors, and a third is still
            // waiting when the transfer ends.
            var clock = new ControlledTimeProvider(ClockStart);
            var delays = new GatedDelayProvider();
            var watcher = new ProbeWatcher();
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var probesIssued = 0;
            var thirdProbeIssued = new SemaphoreSlim(0);

            using var mockHttp = new MockHttpMessageHandler();
            mockHttp.When(HttpMethod.Get, LatencyUrl)
                    .Respond(_ =>
                    {
                        switch (++probesIssued)
                        {
                            case 1:
                                clock.Advance(TimeSpan.FromMilliseconds(40));
                                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ProbeResponseBody) });
                            case 2:
                                throw new HttpRequestException("connection reset");
                            default:
                                thirdProbeIssued.Release();
                                return new TaskCompletionSource<HttpResponseMessage>().Task;
                        }
                    });
            mockHttp.When(DownloadUrlPattern)
                    .Respond(async _ =>
                    {
                        await release.Task.WaitAsync(HangGuard);
                        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[DownloadResponseBytes]) };
                    });

            var speedtest = new OoklaSpeedtest(SingleDownloadSettings(), mockHttp.ToHttpClient(), delays, clock);

            // When all three have gone out and the download then finishes under the third.
            var download = speedtest.GetDownloadSpeedAsync(TestServer, null, watcher.Reporter);

            for (var probe = 0; probe < 2; probe++)
            {
                (await delays.ReleaseNextAsync(HangGuard)).ShouldBeTrue();
                (await watcher.NextProbeReportedAsync()).ShouldBeTrue();
            }

            (await delays.ReleaseNextAsync(HangGuard)).ShouldBeTrue();
            (await thirdProbeIssued.WaitAsync(HangGuard)).ShouldBeTrue();

            release.SetResult();
            await download.WaitAsync(HangGuard);

            // Then the reporter received all three, each identifiable as a probe rather than as one
            // of the transfer's own requests, as succeeded, failed and cancelled.
            watcher.Probes.Count.ShouldBe(3);
            watcher.Probes.ShouldAllBe(record => record.Url == LatencyUrl);
            watcher.Probes.Select(record => record.Outcome)
                   .ShouldBe([RequestOutcome.Succeeded, RequestOutcome.Failed, RequestOutcome.Cancelled]);
            watcher.Probes.Single(record => record.Outcome == RequestOutcome.Succeeded).BytesProcessed.ShouldBe(ProbeResponseBytes);

            // And the transfer's own request is reported under its own kind, so a caller can tell
            // the two apart.
            watcher.All.Count(record => record.Kind == RequestKind.Transfer).ShouldBe(1);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task GetDownloadSpeedAsync_WithANonPositiveProbeInterval_IsRejected(int intervalMilliseconds)
        {
            // Given a probe interval of zero or less, which would probe continuously rather than
            // mean "do not probe" - the off switch is the only way to turn probing off.
            using var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("*").Respond(_ => new HttpResponseMessage(HttpStatusCode.OK));

            var settings = SingleDownloadSettings(new LatencyTestSettings { LoadedLatencyIntervalMilliseconds = intervalMilliseconds });
            var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient());

            // When the download starts.
            var exception = await Record.ExceptionAsync(() => speedtest.GetDownloadSpeedAsync(TestServer));

            // Then it is rejected rather than run.
            exception.ShouldBeOfType<ArgumentOutOfRangeException>();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task GetUploadSpeedAsync_WithANonPositiveProbeInterval_IsRejected(int intervalMilliseconds)
        {
            // Given a probe interval of zero or less.
            using var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("*").Respond(_ => new HttpResponseMessage(HttpStatusCode.OK));

            var settings = new OoklaSpeedtestSettings
            {
                UploadTest = new() { UploadIncrements = 1, UploadSizeIterations = 1, UploadParallelTasks = 1 },
                LatencyTest = new() { LoadedLatencyIntervalMilliseconds = intervalMilliseconds }
            };
            var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient());

            // When the upload starts.
            var exception = await Record.ExceptionAsync(() => speedtest.GetUploadSpeedAsync(TestServer));

            // Then it is rejected rather than run.
            exception.ShouldBeOfType<ArgumentOutOfRangeException>();
        }
    }
}
