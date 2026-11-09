using System;
using System.Diagnostics;
using System.Net.Http;
using NetPace.Core.Clients.Ookla;
using RichardSzalay.MockHttp;
using Shouldly;

namespace NetPace.Core.Tests;

public sealed partial class OoklaSpeedtestTests
{
    /// <summary>
    /// A geostationary satellite link, a congested mobile cell or a 2G data link has a round trip of
    /// one to two seconds or more. Those are the links a speed test matters most on, so the library
    /// defaults have to get through the latency stage on them - both when the user names the server
    /// and when NetPace chooses one - while still giving up on a server that never answers.
    /// </summary>
    public sealed class HighLatencyLinks
    {
        /// <summary>
        /// Longer than the round trip of the slowest link NetPace undertakes to measure, so a server
        /// answering after this is a stand-in for one reached across such a link.
        /// </summary>
        private const int SlowLinkRoundTripMilliseconds = 4000;

        /// <summary>
        /// The per-probe limit and the screening ceiling this replaced, kept here as the figure a
        /// measurement has to beat to show that neither of them is in force any more.
        /// </summary>
        private const int FormerLimitMilliseconds = 2000;

        /// <summary>
        /// What "bounded" has to mean for a server that never answers: the ten seconds the defaults
        /// allow, with headroom for a loaded machine's timers, and far short of the hundred seconds
        /// the HTTP client's own timeout would otherwise impose. It is the bound under test, not a
        /// hang guard.
        /// </summary>
        private static readonly TimeSpan BoundedWait = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Bounds a wait a regression would otherwise leave outstanding far longer than the defaults
        /// allow, so that regression fails the test instead of stalling the suite behind the HTTP
        /// client's own hundred-second timeout. Longer than the shared
        /// <see cref="OoklaSpeedtestTests.HangGuard"/>, which is shorter than the ten seconds the
        /// defaults under test here are allowed to take and would fail them for taking it.
        /// </summary>
        private static readonly TimeSpan SlowLinkHangGuard = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Answers every latency request only once the round trip of a slow link has passed, on the
        /// wall clock: the limits under test are waits rather than measurements, so they are the one
        /// thing a supplied clock deliberately does not govern.
        /// </summary>
        private static void RespondAfterASlowRoundTrip(MockHttpMessageHandler mockHttp, string latencyUrl)
        {
            mockHttp.When(latencyUrl)
                    .Respond(async _ =>
                    {
                        await Task.Delay(SlowLinkRoundTripMilliseconds);
                        return new HttpResponseMessage { Content = new StringContent(ScreeningResponseBody) };
                    });
        }

        /// <summary>
        /// The library defaults, with only the number of latency probes lowered: one probe is enough
        /// to show the per-probe limit is no longer what ends the measurement, and twenty of them
        /// across a slow link would put the better part of a minute into the suite.
        /// </summary>
        private static OoklaSpeedtestSettings DefaultsWithOneLatencyProbe() => new()
        {
            LatencyTest = new() { LatencyTestIterations = 1 }
        };

        [Fact]
        public async Task GetServerLatencyAsync_ServerAcrossASlowLink_ReportsAMeasuredLatency()
        {
            // SCENARIO: High-latency named server is measured

            // Given a named server whose round trip is longer than the per-probe limit NetPace used
            // to allow.
            using var mockHttp = new MockHttpMessageHandler();
            RespondAfterASlowRoundTrip(mockHttp, "http://satellite.com/latency.txt");

            var httpClient = mockHttp.ToHttpClient();
            var speedtest = new OoklaSpeedtest(DefaultsWithOneLatencyProbe(), httpClient, new DelayProviderStub());
            var server = new Server { Url = "http://satellite.com/", Sponsor = "SatelliteSponsor", Location = "Orbit" };

            // When the latency is measured.
            var result = await speedtest.GetServerLatencyAsync(server).WaitAsync(SlowLinkHangGuard);

            // Then the run has a latency for that server rather than a probe abandoned in flight,
            // and the figure is the round trip the link really has.
            result.Server.ShouldBe(server);
            result.LatencyMilliseconds.ShouldBeGreaterThan(FormerLimitMilliseconds);
        }

        [Fact]
        public async Task GetFastestServerByLatencyAsync_EveryCandidateAcrossASlowLink_ChoosesOne()
        {
            // SCENARIO: High-latency server can be chosen automatically

            // Given a user whose round trip to every listed server is longer than the screening
            // ceiling NetPace used to allow, so that today's pass ranks nothing.
            using var mockHttp = new MockHttpMessageHandler();
            RespondAfterASlowRoundTrip(mockHttp, "*/latency.txt");

            var httpClient = mockHttp.ToHttpClient();
            var speedtest = new OoklaSpeedtest(new OoklaSpeedtestSettings(), httpClient, new DelayProviderStub());
            IServer[] servers =
            [
                new Server { Url = "http://satellite1.com/", Sponsor = "Satellite1", Location = "Orbit" },
                new Server { Url = "http://satellite2.com/", Sponsor = "Satellite2", Location = "Orbit" }
            ];

            // When a server is chosen with nothing overridden.
            var stopwatch = Stopwatch.StartNew();
            var result = await speedtest.GetFastestServerByLatencyAsync(servers).WaitAsync(SlowLinkHangGuard);
            stopwatch.Stop();

            // Then one of the slow candidates is chosen and carries its screening figure, instead of
            // the run reporting that no server was available.
            servers.ShouldContain(result.Server);
            result.LatencyMilliseconds.ShouldBeGreaterThan(FormerLimitMilliseconds);

            // And choosing one still took no longer than the bound the defaults allow.
            stopwatch.Elapsed.ShouldBeLessThan(BoundedWait);
        }

        [Fact]
        public async Task GetServerLatencyAsync_ServerNeverAnswers_FailsWithinTheBoundedWait()
        {
            // SCENARIO: Unresponsive server is still bounded

            // Given a named server that never answers at all.
            using var mockHttp = new MockHttpMessageHandler();
            NeverRespond(mockHttp, "http://silent.com/latency.txt");

            var httpClient = mockHttp.ToHttpClient();
            var speedtest = new OoklaSpeedtest(DefaultsWithOneLatencyProbe(), httpClient, new DelayProviderStub());
            var server = new Server { Url = "http://silent.com/", Sponsor = "SilentSponsor", Location = "Nowhere" };

            // When the latency is measured.
            var stopwatch = Stopwatch.StartNew();
            var exception = await Record.ExceptionAsync(() => speedtest.GetServerLatencyAsync(server).WaitAsync(SlowLinkHangGuard));
            stopwatch.Stop();

            // Then the measurement ends in a failure within the bound rather than waiting on a
            // server that is never going to reply - the price of a probe limit long enough for a
            // slow link is that it must still be a limit.
            exception.ShouldNotBeNull();
            stopwatch.Elapsed.ShouldBeLessThan(BoundedWait);
        }

        [Fact]
        public async Task GetFastestServerByLatencyAsync_NoCandidateEverAnswers_ReportsNoServersAvailableWithinTheBoundedWait()
        {
            // SCENARIO: Unresponsive server is still bounded

            // Given candidates that never answer at all.
            using var mockHttp = new MockHttpMessageHandler();
            NeverRespond(mockHttp, "*/latency.txt");

            var httpClient = mockHttp.ToHttpClient();
            var speedtest = new OoklaSpeedtest(new OoklaSpeedtestSettings(), httpClient, new DelayProviderStub());
            IServer[] servers =
            [
                new Server { Url = "http://silent1.com/", Sponsor = "Silent1", Location = "Nowhere" },
                new Server { Url = "http://silent2.com/", Sponsor = "Silent2", Location = "Nowhere" }
            ];

            // When a server is chosen.
            var stopwatch = Stopwatch.StartNew();
            var exception = await Record.ExceptionAsync(() => speedtest.GetFastestServerByLatencyAsync(servers).WaitAsync(SlowLinkHangGuard));
            stopwatch.Stop();

            // Then the pass gives up within the bound and says no server could be reached.
            exception.ShouldBeOfType<NoServersAvailableException>();
            stopwatch.Elapsed.ShouldBeLessThan(BoundedWait);
        }

        [Fact]
        public async Task GetFastestServerByLatencyAsync_OneCandidateAnswersPromptly_DoesNotWaitOutTheSlowLinkAllowance()
        {
            // A run on an unshaped link must choose a server as quickly as it does today. The
            // allowance a slow link needs is therefore reached in stages: a pass that has ranked
            // something takes it and moves on, and only a pass that has ranked nothing - the run
            // that would otherwise end in "no servers available" - waits for the rest of it.

            // Given a candidate that answers at once beside one that never answers.
            using var mockHttp = new MockHttpMessageHandler();
            NeverRespond(mockHttp, "http://silent.com/latency.txt");
            mockHttp.When("http://prompt.com/latency.txt")
                    .Respond("text/plain", ScreeningResponseBody);

            var httpClient = mockHttp.ToHttpClient();
            var speedtest = new OoklaSpeedtest(new OoklaSpeedtestSettings(), httpClient, new DelayProviderStub());
            var promptServer = new Server { Url = "http://prompt.com/", Sponsor = "PromptSponsor", Location = "PromptLocation" };
            IServer[] servers =
            [
                new Server { Url = "http://silent.com/", Sponsor = "SilentSponsor", Location = "Nowhere" },
                promptServer
            ];

            // When a server is chosen.
            var stopwatch = Stopwatch.StartNew();
            var result = await speedtest.GetFastestServerByLatencyAsync(servers).WaitAsync(SlowLinkHangGuard);
            stopwatch.Stop();

            // Then the candidate that answered is chosen, and the silent one did not cost the run
            // the whole allowance a slow link is given.
            result.Server.ShouldBe(promptServer);
            stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromMilliseconds(2 * FormerLimitMilliseconds));
        }
    }
}
