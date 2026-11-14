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
        /// Comfortably past the two seconds that used to end a probe and a screening pass, so a
        /// server answering only after this stands in for one reached across a slow link.
        /// </summary>
        /// <remarks>
        /// Bounded above by the screening ceiling divided by <c>ScreeningRequestCount</c>: a
        /// candidate is screened with that many requests, issued one after another, and is ranked
        /// only once the last of them returns. Raise this past a third of the ceiling and a
        /// screening pass can no longer finish inside it, leaving the candidates abandoned and the
        /// test resting on whether an abandoned candidate's figure happens to be recorded before
        /// the pass reads its results.
        /// </remarks>
        private const int SlowLinkRoundTripMilliseconds = 2500;

        /// <summary>
        /// The per-probe limit this replaced, and the whole screening ceiling before it became only
        /// the first screening stage. A figure beating it shows that neither still ends the work: a
        /// probe is no longer abandoned at two seconds, and a pass that has ranked nothing by then
        /// no longer gives up.
        /// </summary>
        private const int FormerLimitMilliseconds = 2000;

        /// <summary>
        /// A ceiling longer than the first stage, so that a pass which ranks nothing spends both
        /// stages rather than collapsing to one. Four seconds: far enough above the first stage that
        /// spending the ceiling once is plainly distinguishable from spending it twice, and short
        /// enough to leave the suite's wall clock alone.
        /// </summary>
        private const int StagedCeilingMilliseconds = 4000;

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
        /// <see cref="OoklaSpeedtestTests.HangGuard"/>, which is exactly the ten seconds the
        /// defaults under test here are allowed to take and would fail them for taking it.
        /// </summary>
        private static readonly TimeSpan SlowLinkHangGuard = TimeSpan.FromSeconds(30);

        /// <summary>
        /// What "as fast as it ever was" has to mean for a pass that ranked a candidate inside the
        /// first stage: that stage - the two seconds that were the whole ceiling - and a second more
        /// for a loaded machine's timers. Kept close to the stage deliberately, so that a first
        /// stage which grew would fail this rather than hide under the slack; a regression that
        /// waited the whole slow-link allowance out takes ten seconds and misses it by far.
        /// </summary>
        private static readonly TimeSpan PromptChoiceBound = TimeSpan.FromMilliseconds(FormerLimitMilliseconds + 1000);

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
        /// to show the per-probe limit is no longer what ends the measurement, and the default
        /// twenty, each waiting out <see cref="SlowLinkRoundTripMilliseconds"/>, would put nearly a
        /// minute into the suite.
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

            // The ceiling and the per-probe limit are left at the defaults, since they are what has
            // to carry a slow link; only the number of screening requests is lowered. One request is
            // enough to leave the pass unranked when the first stage is up, which is the path under
            // test, and the default three would spend three round trips per candidate to reach it.
            var settings = new OoklaSpeedtestSettings
            {
                ServerScreening = new() { ScreeningRequestCount = 1 }
            };

            var httpClient = mockHttp.ToHttpClient();
            var speedtest = new OoklaSpeedtest(settings, httpClient, new DelayProviderStub());
            IServer[] servers =
            [
                new Server { Url = "http://satellite1.com/", Sponsor = "Satellite1", Location = "Orbit" },
                new Server { Url = "http://satellite2.com/", Sponsor = "Satellite2", Location = "Orbit" }
            ];

            // When a server is chosen under the shipped ceiling.
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
            // slow link is that it must still be a limit. The failure is the probe's own timeout,
            // in the shape NetPace raises one: a bare "some exception was thrown" would be
            // satisfied by the argument guard that fires instantly on a non-positive limit, and
            // would report a bound this test never observed.
            exception.ShouldBeAssignableTo<OperationCanceledException>();
            exception!.InnerException.ShouldBeOfType<TimeoutException>();
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
            stopwatch.Elapsed.ShouldBeLessThan(PromptChoiceBound);
        }

        [Fact]
        public async Task GetFastestServerByLatencyAsync_NothingRanked_SpendsTheCeilingOnceAcrossBothStages()
        {
            // The stages divide the ceiling, they do not add to it: a pass that has ranked nothing
            // waits the first stage and then only what is left of the ceiling. A second stage that
            // waited the whole ceiling again would report the same failure after half again as
            // long, so only a bound on both sides of the figure notices it.

            // Given candidates that never answer, and a ceiling longer than the first stage so that
            // both stages are spent.
            using var mockHttp = new MockHttpMessageHandler();
            NeverRespond(mockHttp, "*/latency.txt");

            var settings = new OoklaSpeedtestSettings
            {
                ServerScreening = new() { ServerTimeoutMilliseconds = StagedCeilingMilliseconds }
            };

            var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient(), new DelayProviderStub());
            IServer[] servers =
            [
                new Server { Url = "http://silent1.com/", Sponsor = "Silent1", Location = "Nowhere" },
                new Server { Url = "http://silent2.com/", Sponsor = "Silent2", Location = "Nowhere" }
            ];

            // When a server is chosen.
            var stopwatch = Stopwatch.StartNew();
            var exception = await Record.ExceptionAsync(() => speedtest.GetFastestServerByLatencyAsync(servers).WaitAsync(SlowLinkHangGuard));
            stopwatch.Stop();

            // Then the pass gave up only once the ceiling was spent, and did not spend it twice.
            exception.ShouldBeOfType<NoServersAvailableException>();
            stopwatch.Elapsed.ShouldBeGreaterThan(TimeSpan.FromMilliseconds(StagedCeilingMilliseconds - 200));
            stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromMilliseconds(StagedCeilingMilliseconds + FormerLimitMilliseconds - 500));
        }

        [Fact]
        public async Task GetFastestServerByLatencyAsync_EveryCandidateFailsOnItsOwnMerits_GivesUpBeforeTheFirstStageIsOut()
        {
            // A longer ceiling costs a run only while something is still outstanding. A pass whose
            // candidates have all answered - with something no speed test server would send - has
            // nothing left to wait for, so it reports the failure at once instead of holding the
            // user for a ceiling that cannot change the answer. Without this the raised ceiling
            // would turn an instant error on a link where nothing is listening into a ten-second
            // one.

            // Given candidates that answer promptly, with a body no speed test server would send.
            const string wrongBody = "<html>hello</html>";
            using var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("*/latency.txt").Respond("text/plain", wrongBody);

            var httpClient = mockHttp.ToHttpClient();
            var speedtest = new OoklaSpeedtest(new OoklaSpeedtestSettings(), httpClient, new DelayProviderStub());
            IServer[] servers =
            [
                new Server { Url = "http://wrong1.com/", Sponsor = "Wrong1", Location = "Nowhere" },
                new Server { Url = "http://wrong2.com/", Sponsor = "Wrong2", Location = "Nowhere" }
            ];

            // When a server is chosen, with the shipped ceiling in force.
            var stopwatch = Stopwatch.StartNew();
            var exception = await Record.ExceptionAsync(() => speedtest.GetFastestServerByLatencyAsync(servers).WaitAsync(SlowLinkHangGuard));
            stopwatch.Stop();

            // Then the pass says no server could be reached, without spending even the first stage.
            exception.ShouldBeOfType<NoServersAvailableException>();
            stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromMilliseconds(FormerLimitMilliseconds));
        }

        /// <summary>
        /// The round trip of the steady candidate in <see cref="ChooseBetweenASteadyAndAWarmingCandidateAsync"/>,
        /// every request alike, so it is ranked after <see cref="StagedScreeningRequestCount"/> of
        /// them - well before <see cref="FirstStageAfterTheSteadyCandidateMilliseconds"/>, and well
        /// after <see cref="FirstStageBeforeAnyCandidateMilliseconds"/>.
        /// </summary>
        private const int SteadyRoundTripMilliseconds = 300;

        /// <summary>
        /// The first request to the warming candidate, which pays for connection setup the link is not
        /// responsible for. Long enough that the candidate is ranked only after the steady one and
        /// well after the default first stage, so a first stage that ignored its setting would stop
        /// waiting before this candidate could be ranked; short enough to leave it ranked well inside
        /// <see cref="StagedCeilingMilliseconds"/>.
        /// </summary>
        private const int WarmingFirstRequestMilliseconds = 2500;

        /// <summary>
        /// The warming candidate's round trip once its connection is set up: far below
        /// <see cref="SteadyRoundTripMilliseconds"/>, so the warming candidate wins on its fastest
        /// request whenever the pass waits for it to be ranked, and loses only by being abandoned.
        /// </summary>
        private const int WarmingRoundTripMilliseconds = 50;

        /// <summary>
        /// Enough screening requests for the warming candidate to show its real round trip after the
        /// first has paid for connection setup.
        /// </summary>
        private const int StagedScreeningRequestCount = 2;

        /// <summary>
        /// A first stage that ends after the steady candidate is ranked and long before the warming one
        /// is.
        /// </summary>
        private const int FirstStageAfterTheSteadyCandidateMilliseconds = 1000;

        /// <summary>
        /// A first stage that ends before either candidate is ranked, so the pass goes on into the
        /// second stage.
        /// </summary>
        private const int FirstStageBeforeAnyCandidateMilliseconds = 200;

        /// <summary>
        /// Answers each request for <paramref name="latencyUrl"/> after the next round trip in
        /// <paramref name="roundTripsMilliseconds"/>, repeating the last once they run out, on the
        /// wall clock the stages are timed on.
        /// </summary>
        private static void RespondInTurn(MockHttpMessageHandler mockHttp, string latencyUrl, params int[] roundTripsMilliseconds)
        {
            var requestsReceived = 0;
            mockHttp.When(latencyUrl)
                    .Respond(async _ =>
                    {
                        var request = Interlocked.Increment(ref requestsReceived) - 1;
                        await Task.Delay(roundTripsMilliseconds[Math.Min(request, roundTripsMilliseconds.Length - 1)]);
                        return new HttpResponseMessage { Content = new StringContent(ScreeningResponseBody) };
                    });
        }

        /// <summary>
        /// Chooses between a steady candidate, ranked quickly at its middling round trip, and a
        /// warming candidate, ranked only much later but with the lower round trip, under
        /// <see cref="StagedCeilingMilliseconds"/> and the first stage given. Which of the two is
        /// chosen says whether the pass waited for the warming candidate to be ranked.
        /// </summary>
        private static async Task<(LatencyTestResult Result, IServer Steady, IServer Warming)> ChooseBetweenASteadyAndAWarmingCandidateAsync(int firstStageMilliseconds)
        {
            using var mockHttp = new MockHttpMessageHandler();
            RespondInTurn(mockHttp, "http://steady.com/latency.txt", SteadyRoundTripMilliseconds);
            RespondInTurn(mockHttp, "http://warming.com/latency.txt", WarmingFirstRequestMilliseconds, WarmingRoundTripMilliseconds);

            var settings = new OoklaSpeedtestSettings
            {
                ServerScreening = new()
                {
                    ServerTimeoutMilliseconds = StagedCeilingMilliseconds,
                    FirstScreeningStageMilliseconds = firstStageMilliseconds,
                    ScreeningRequestCount = StagedScreeningRequestCount
                }
            };

            var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient(), new DelayProviderStub());
            var steady = new Server { Url = "http://steady.com/", Sponsor = "Steady", Location = "Nearby" };
            var warming = new Server { Url = "http://warming.com/", Sponsor = "Warming", Location = "Nearby" };

            var result = await speedtest.GetFastestServerByLatencyAsync([steady, warming]).WaitAsync(SlowLinkHangGuard);
            return (result, steady, warming);
        }

        [Fact]
        public async Task GetFastestServerByLatencyAsync_ACandidateRankedByTheConfiguredFirstStage_IsChosenWithoutWaitingForASlowerOne()
        {
            // Given a steady candidate ranked inside a first stage shorter than the default, and a
            // warming candidate with the lower round trip that is ranked only after that stage.
            // When a server is chosen.
            var (result, steady, _) = await ChooseBetweenASteadyAndAWarmingCandidateAsync(FirstStageAfterTheSteadyCandidateMilliseconds);

            // Then the pass settled for the candidate ranked by the end of the configured first stage,
            // rather than waiting for the one that would have beaten it.
            result.Server.ShouldBe(steady);
        }

        [Fact]
        public async Task GetFastestServerByLatencyAsync_FirstStageAsLongAsTheCeiling_GivesASlowerCandidateTheWholeCeiling()
        {
            // Given a first stage as long as the ceiling, a steady candidate ranked quickly, and a
            // warming candidate with the lower round trip that is ranked only after the default first
            // stage is out.
            // When a server is chosen.
            var (result, _, warming) = await ChooseBetweenASteadyAndAWarmingCandidateAsync(StagedCeilingMilliseconds);

            // Then the ceiling was spent as one stage, as it was before it was spent in stages: the
            // warming candidate was given the time to be ranked beside the quicker one, and won.
            result.Server.ShouldBe(warming);
        }

        [Fact]
        public async Task GetFastestServerByLatencyAsync_NothingRankedByTheFirstStage_SecondStageWaitsForEveryCandidate()
        {
            // Given a first stage that ends before either candidate is ranked, a steady candidate
            // ranked soon after it, and a warming candidate with the lower round trip ranked later
            // still, both inside the ceiling.
            // When a server is chosen.
            var (result, _, warming) = await ChooseBetweenASteadyAndAWarmingCandidateAsync(FirstStageBeforeAnyCandidateMilliseconds);

            // Then the second stage waited for every candidate rather than ending on the first one
            // ranked, which would compare one server and choose the steady one: the warming candidate
            // was ranked too, and won.
            result.Server.ShouldBe(warming);
        }
    }
}
