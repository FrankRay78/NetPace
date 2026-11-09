using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using NetPace.Core.Clients.Ookla;
using RichardSzalay.MockHttp;
using Shouldly;

namespace NetPace.Core.Tests;

public sealed partial class OoklaSpeedtestTests
{
    /// <summary>
    /// The running latency and jitter a measurement reports while it is still in flight, so a
    /// consumer can show a figure that climbs rather than one that appears only at the end.
    /// </summary>
    /// <remarks>
    /// Every probe here is timed on a clock the test moves itself and paced through
    /// <see cref="GatedDelayProvider"/>, so a reported figure is exactly what the test's probe
    /// times imply rather than a wall-clock reading.
    /// </remarks>
    public sealed class LiveFigures
    {
        private const string ServerUrl = "http://example.com/";
        private const string LatencyUrl = "http://example.com/latency.txt";
        private const string DownloadUrlPattern = "http://example.com/random*";

        private static readonly TimeSpan HangGuard = TimeSpan.FromSeconds(10);

        private static readonly IServer TestServer = new Server { Url = ServerUrl, Sponsor = "Sponsor", Location = "Location" };

        /// <summary>
        /// A provider whose idle latency measurement probes <paramref name="iterations"/> times with
        /// no wait between probes.
        /// </summary>
        private static OoklaSpeedtest LatencyProbingSpeedtest(MockHttpMessageHandler mockHttp, ControlledTimeProvider clock, int iterations) =>
            new(
                new OoklaSpeedtestSettings
                {
                    LatencyTest = new()
                    {
                        LatencyTestIterations = iterations,
                        LatencyTestIntervalMilliseconds = 0
                    }
                },
                mockHttp.ToHttpClient(),
                timeProviderOverride: clock);

        /// <summary>
        /// A provider whose download is one request, so the transfer reports progress exactly once
        /// and a test decides what the probes had answered by then.
        /// </summary>
        private static OoklaSpeedtest SingleRequestDownloadSpeedtest(MockHttpMessageHandler mockHttp, GatedDelayProvider delays, ControlledTimeProvider clock) =>
            new(
                new OoklaSpeedtestSettings
                {
                    DownloadTest = new()
                    {
                        DownloadSizes = [100],
                        DownloadSizeIterations = 1,
                        DownloadParallelTasks = 1
                    }
                },
                mockHttp.ToHttpClient(),
                delays,
                clock);

        /// <summary>
        /// Answers each successive probe after moving <paramref name="clock"/> on by the next of
        /// <paramref name="roundTripMilliseconds"/>. Probes never overlap, so the times are handed
        /// out in the order the probes take them.
        /// </summary>
        private static void RespondToProbes(MockHttpMessageHandler mockHttp, ControlledTimeProvider clock, params int[] roundTripMilliseconds)
        {
            var times = new Queue<int>(roundTripMilliseconds);

            mockHttp.When(HttpMethod.Get, LatencyUrl)
                    .Respond(_ =>
                    {
                        clock.Advance(TimeSpan.FromMilliseconds(times.Dequeue()));
                        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ScreeningResponseBody) };
                    });
        }

        [Fact]
        public async Task GetServerLatencyAsync_AfterEachProbe_ReportsTheFiguresOfTheProbesSoFar()
        {
            // SCENARIO: Library progress carries running figures

            // Given three probes taking 20, 50 and 30 ms of a clock the test moves itself.
            var clock = new ControlledTimeProvider(ClockStart);
            var reports = new List<LatencyTestProgress>();

            using var mockHttp = new MockHttpMessageHandler();
            RespondToProbes(mockHttp, clock, 20, 50, 30);

            var speedtest = LatencyProbingSpeedtest(mockHttp, clock, iterations: 3);

            // When the measurement runs, collecting every progress report it raises.
            await speedtest.GetServerLatencyAsync(TestServer, new SynchronousProgress<LatencyTestProgress>(reports.Add));

            // Then the first report carries the one round trip and no jitter, because unevenness
            // needs two probes to compare.
            reports.Count.ShouldBe(3);
            reports[0].LatencyMilliseconds.ShouldBe(20);
            reports[0].JitterMilliseconds.ShouldBeNull();

            // And each later report carries the average and the unevenness of the probes answered
            // by then.
            reports[1].LatencyMilliseconds.ShouldBe(35);
            reports[1].JitterMilliseconds.ShouldBe(30);
            reports[2].LatencyMilliseconds.ShouldBe(33);
            reports[2].JitterMilliseconds.ShouldBe(25);
        }

        [Fact]
        public async Task GetServerLatencyAsync_WhereTheLastReportLandsOnTheResult_ReportsTheSameFigures()
        {
            // Given two probes taking 40 and 60 ms.
            var clock = new ControlledTimeProvider(ClockStart);
            var reports = new List<LatencyTestProgress>();

            using var mockHttp = new MockHttpMessageHandler();
            RespondToProbes(mockHttp, clock, 40, 60);

            var speedtest = LatencyProbingSpeedtest(mockHttp, clock, iterations: 2);

            // When
            var result = await speedtest.GetServerLatencyAsync(TestServer, new SynchronousProgress<LatencyTestProgress>(reports.Add));

            // Then the figure the consumer last watched is the figure it is given.
            reports[^1].LatencyMilliseconds.ShouldBe(result.LatencyMilliseconds);
            reports[^1].JitterMilliseconds.ShouldBe(result.JitterMilliseconds);
        }

        [Fact]
        public async Task GetDownloadSpeedAsync_OnceProbesHaveAnswered_ReportsTheirFiguresWithTheProgress()
        {
            // SCENARIO: Figures are live while a test runs

            // Given a download held open while two probes answer in 40 and 60 ms.
            var clock = new ControlledTimeProvider(ClockStart);
            var delays = new GatedDelayProvider();
            var reports = new List<SpeedTestProgress>();
            var probesAnswered = 0;
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            using var mockHttp = new MockHttpMessageHandler();
            var times = new Queue<int>([40, 60]);
            mockHttp.When(HttpMethod.Get, LatencyUrl)
                    .Respond(_ =>
                    {
                        clock.Advance(TimeSpan.FromMilliseconds(times.Dequeue()));
                        Interlocked.Increment(ref probesAnswered);
                        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ScreeningResponseBody) };
                    });
            mockHttp.When(DownloadUrlPattern)
                    .Respond(async _ =>
                    {
                        await release.Task.WaitAsync(HangGuard);
                        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[2048]) };
                    });

            var speedtest = SingleRequestDownloadSpeedtest(mockHttp, delays, clock);

            // When both probes have answered and the download then finishes.
            var download = speedtest.GetDownloadSpeedAsync(TestServer, new SynchronousProgress<SpeedTestProgress>(reports.Add));

            for (var probe = 0; probe < 2; probe++)
            {
                (await delays.ReleaseNextAsync(HangGuard)).ShouldBeTrue();
                await WaitForAsync(() => Volatile.Read(ref probesAnswered) > probe);
            }

            release.SetResult();
            var result = await download.WaitAsync(HangGuard);

            // Then the transfer's own progress report carries the figures of the round trips taken
            // under it, beside the bytes and the elapsed time it already carried.
            reports.ShouldNotBeEmpty();
            reports[^1].LatencyMilliseconds.ShouldBe(50);
            reports[^1].JitterMilliseconds.ShouldBe(20);

            // And they are the figures the result goes on to report.
            result.Latency.ShouldNotBeNull();
            reports[^1].LatencyMilliseconds.ShouldBe(result.Latency!.LatencyMilliseconds);
            reports[^1].JitterMilliseconds.ShouldBe(result.Latency.JitterMilliseconds);
        }

        [Fact]
        public async Task GetDownloadSpeedAsync_BeforeAnyProbeAnswered_ReportsNeitherFigure()
        {
            // Given a download that finishes before its first probe is even due.
            var clock = new ControlledTimeProvider(ClockStart);
            var delays = new GatedDelayProvider();
            var reports = new List<SpeedTestProgress>();

            using var mockHttp = new MockHttpMessageHandler();
            RespondToProbes(mockHttp, clock, 40);
            mockHttp.When(DownloadUrlPattern)
                    .Respond(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[2048]) });

            var speedtest = SingleRequestDownloadSpeedtest(mockHttp, delays, clock);

            // When
            var result = await speedtest.GetDownloadSpeedAsync(TestServer, new SynchronousProgress<SpeedTestProgress>(reports.Add)).WaitAsync(HangGuard);

            // Then its progress says nothing about latency rather than reporting a zero, which is
            // the same thing its result says.
            reports.ShouldNotBeEmpty();
            reports[^1].LatencyMilliseconds.ShouldBeNull();
            reports[^1].JitterMilliseconds.ShouldBeNull();
            result.Latency.ShouldBeNull();
        }

        [Fact]
        public async Task GetFastestServerByLatencyAsync_WhileScreening_ReportsNeitherFigure()
        {
            // Given three candidates to screen, each answering at once.
            var clock = new ControlledTimeProvider(ClockStart);
            var reports = new List<SpeedTestProgress>();

            using var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("*latency.txt").Respond("text/plain", ScreeningResponseBody);

            var speedtest = new OoklaSpeedtest(new OoklaSpeedtestSettings(), mockHttp.ToHttpClient(), timeProviderOverride: clock);

            IServer[] candidates =
            [
                new Server { Url = "http://one.example.com/", Sponsor = "One", Location = "Here" },
                new Server { Url = "http://two.example.com/", Sponsor = "Two", Location = "There" },
            ];

            // When
            await speedtest.GetFastestServerByLatencyAsync(candidates, new SynchronousProgress<SpeedTestProgress>(reports.Add));

            // Then no report claims a latency or a jitter: the two figures describe the one server
            // under test, and screening is measuring several.
            reports.ShouldNotBeEmpty();
            reports.ShouldAllBe(report => report.LatencyMilliseconds == null);
            reports.ShouldAllBe(report => report.JitterMilliseconds == null);
        }

        /// <summary>
        /// Waits for <paramref name="condition"/> to hold, so a test can step past something that
        /// happens off its own thread without waiting on the wall clock for a fixed time.
        /// </summary>
        private static async Task WaitForAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow + HangGuard;

            while (!condition())
            {
                DateTime.UtcNow.ShouldBeLessThan(deadline);
                await Task.Delay(1);
            }
        }
    }
}
