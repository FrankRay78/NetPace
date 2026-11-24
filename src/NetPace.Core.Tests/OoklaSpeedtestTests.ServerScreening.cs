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
    /// Choosing a server and measuring a server are two different jobs. Screening answers "is this
    /// server reachable, and roughly how fast?" for every discovered candidate, concurrently and under
    /// one overall ceiling, so a reachable-but-slow server is ranked rather than rejected and the
    /// comparison covers more than whichever server answered first.
    /// </summary>
    public sealed class ServerScreening
    {
        /// <summary>
        /// Answers the screening request for <paramref name="latencyUrl"/> only once
        /// <paramref name="delayMilliseconds"/> has passed, so a test can place a candidate either
        /// side of the screening ceiling.
        /// </summary>
        private static void RespondAfterDelay(MockHttpMessageHandler mockHttp, string latencyUrl, int delayMilliseconds)
        {
            mockHttp.When(latencyUrl)
                    .Respond(async _ =>
                    {
                        await Task.Delay(delayMilliseconds);
                        return new HttpResponseMessage { Content = new StringContent(ScreeningResponseBody) };
                    });
        }

        [Fact]
        public async Task GetFastestServerByLatencyAsync_ReachableButSlowServer_IsSelectedRatherThanRejected()
        {
            // SCENARIO: A run auto-selects a server

            // Given a single server that is plainly reachable, but slow enough that a full latency
            // measurement of it cannot fit inside the selection ceiling.
            using var mockHttp = new MockHttpMessageHandler();
            RespondAfterDelay(mockHttp, "http://slow.com/latency.txt", 150);

            var httpClient = mockHttp.ToHttpClient();
            var speedtest = new OoklaSpeedtest(new OoklaSpeedtestSettings(), httpClient, new DelayProviderStub());
            var slowServer = new Server { Url = "http://slow.com/", Sponsor = "SlowSponsor", Location = "SlowLocation" };

            // When
            var result = await speedtest.GetFastestServerByLatencyAsync([slowServer]);

            // Then
            result.Server.ShouldBe(slowServer);

            // And the extremes are stated as not recorded rather than as zero: screening keeps no
            // run of probe times, so it has no measured spread to report.
            result.MinimumLatencyMilliseconds.ShouldBeNull();
            result.MaximumLatencyMilliseconds.ShouldBeNull();
        }

        [Fact]
        public async Task GetFastestServerByLatencyAsync_CandidateAnswersThenFails_IsRankedOnWhatItDidAnswer()
        {
            // Given a fast candidate that answers once and then fails, against a slower one that
            // answers every request: the partial answer is kept and ranked, where the all-or-nothing
            // probe this replaced would have dropped it. See RespondAfterAdvancing for the order.
            const int flakyMilliseconds = 10;
            var clock = new ControlledTimeProvider(ClockStart);
            var flakyScreened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            using var mockHttp = new MockHttpMessageHandler(BackendDefinitionBehavior.Always);

            mockHttp.Expect("http://flaky.com/latency.txt")
                    .Respond(_ =>
                    {
                        clock.Advance(TimeSpan.FromMilliseconds(flakyMilliseconds));
                        return new HttpResponseMessage { Content = new StringContent(ScreeningResponseBody) };
                    });
            mockHttp.When("http://flaky.com/latency.txt")
                    .Throw(new HttpRequestException("Connection reset."));

            RespondAfterAdvancing(mockHttp, "http://steady.com/latency.txt", clock, TimeSpan.FromMilliseconds(60), flakyScreened.Task);

            var httpClient = mockHttp.ToHttpClient();
            var speedtest = new OoklaSpeedtest(new OoklaSpeedtestSettings(), httpClient, new DelayProviderStub(), clock);
            var flakyServer = new Server { Url = "http://flaky.com/", Sponsor = "FlakySponsor", Location = "FlakyLocation" };
            var steadyServer = new Server { Url = "http://steady.com/", Sponsor = "SteadySponsor", Location = "SteadyLocation" };

            var diagnostics = new List<RequestDiagnostic>();

            // When
            var result = await speedtest.GetFastestServerByLatencyAsync(
                [flakyServer, steadyServer],
                new SynchronousProgress<SpeedTestProgress>(_ => flakyScreened.TrySetResult()),
                Collect(diagnostics));

            // Then the steady candidate was actually screened - asserted, because a gate that never
            // opened would leave it abandoned at the ceiling and the ranking below unmade.
            diagnostics.ShouldContain(d => d.Url == "http://steady.com/latency.txt" && d.Outcome == RequestOutcome.Succeeded);

            result.Server.ShouldBe(flakyServer);
            result.LatencyMilliseconds.ShouldBe(flakyMilliseconds);
        }

        [Theory]
        [InlineData("ftp://files.example.com/speedtest/upload.php")]
        [InlineData("file:///etc/hosts")]
        [InlineData("mailto:someone@example.com")]
        [InlineData("good.com/speedtest/upload.php")]
        [InlineData("/speedtest/upload.php")]
        [InlineData("   ")]
        [InlineData("")]
        [InlineData(null)]
        public async Task GetFastestServerByLatencyAsync_CandidateUrlIsNotAWebAddress_IsRankedOutAndAGoodServerIsStillChosen(string? malformedUrl)
        {
            // Given a remote feed carrying one entry that is not a web address, ahead of a server
            // that answers. The handler refuses it as the shipped transport would, not as a mock.
            using var goodServerHandler = new MockHttpMessageHandler();
            goodServerHandler.When("http://good.com/latency.txt")
                             .Respond("text/plain", ScreeningResponseBody);

            using var httpClient = new HttpClient(new WebAddressesOnlyHandler(goodServerHandler));
            var speedtest = new OoklaSpeedtest(new OoklaSpeedtestSettings(), httpClient, new DelayProviderStub());
            var malformedServer = new Server { Url = malformedUrl!, Sponsor = "MalformedSponsor", Location = "MalformedLocation" };
            var goodServer = new Server { Url = "http://good.com/", Sponsor = "GoodSponsor", Location = "GoodLocation" };

            // When
            var result = await speedtest.GetFastestServerByLatencyAsync([malformedServer, goodServer]);

            // Then one bad entry does not cost the run its server.
            result.Server.ShouldBe(goodServer);
        }

        [Theory]
        [InlineData("https://secure.com/")]
        [InlineData("HTTPS://SECURE.COM/")]
        [InlineData("HTTP://PLAIN.COM/")]
        public async Task GetFastestServerByLatencyAsync_CandidateUrlIsAWebAddressInAnyCasing_IsChosen(string serverUrl)
        {
            // Given a server whose address is a web address, secure or not, however it is cased.
            using var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("*/latency.txt")
                    .Respond("text/plain", ScreeningResponseBody);

            var httpClient = mockHttp.ToHttpClient();
            var speedtest = new OoklaSpeedtest(new OoklaSpeedtestSettings(), httpClient, new DelayProviderStub());
            var server = new Server { Url = serverUrl, Sponsor = "WebSponsor", Location = "WebLocation" };

            // When
            var result = await speedtest.GetFastestServerByLatencyAsync([server]);

            // Then ranking out what is not a web address has not cost a server that is one.
            result.Server.ShouldBe(server);
        }

        [Fact]
        public async Task GetFastestServerByLatencyAsync_FaultThatIsNotAboutTheServer_SurfacesRatherThanReportingNoServers()
        {
            // Given a client that cannot make any request at all, so the failure says nothing about
            // whether the candidate is reachable.
            using var mockHttp = new MockHttpMessageHandler();
            var httpClient = mockHttp.ToHttpClient();
            httpClient.Dispose();

            var speedtest = new OoklaSpeedtest(new OoklaSpeedtestSettings(), httpClient, new DelayProviderStub());
            var server = new Server { Url = "http://anywhere.com/", Sponsor = "AnySponsor", Location = "AnyLocation" };

            // When
            var exception = await Record.ExceptionAsync(() => speedtest.GetFastestServerByLatencyAsync([server]));

            // Then the caller is told what actually went wrong, instead of being told the server list
            // was unreachable.
            exception.ShouldBeOfType<ObjectDisposedException>();
        }

        [Fact]
        public async Task GetFastestServerByLatencyAsync_ManyUnresponsiveCandidates_ChoosesWithinTheCeiling()
        {
            // Regression guard for the ceiling itself (refs #239): slow candidates stalled runs for
            // minutes, which is why this asserts on wall-clock time where other tests do not.

            // Given six candidates that never answer, and one that answers at once.
            const int unresponsiveCandidateCount = 6;
            const int ceilingMilliseconds = 500;

            using var mockHttp = new MockHttpMessageHandler();
            var candidates = new List<IServer>();

            for (var candidate = 0; candidate < unresponsiveCandidateCount; candidate++)
            {
                var url = $"http://unresponsive{candidate}.com/";
                NeverRespond(mockHttp, url + "latency.txt");
                candidates.Add(new Server { Url = url, Sponsor = $"Unresponsive{candidate}", Location = "Nowhere" });
            }

            mockHttp.When("http://prompt.com/latency.txt")
                    .Respond("text/plain", ScreeningResponseBody);

            var promptServer = new Server { Url = "http://prompt.com/", Sponsor = "PromptSponsor", Location = "PromptLocation" };
            candidates.Add(promptServer);

            var httpClient = mockHttp.ToHttpClient();
            var settings = new OoklaSpeedtestSettings
            {
                ServerScreening = new() { ServerTimeoutMilliseconds = ceilingMilliseconds }
            };

            var speedtest = new OoklaSpeedtest(settings, httpClient, new DelayProviderStub());

            // When
            var stopwatch = Stopwatch.StartNew();
            var result = await speedtest.GetFastestServerByLatencyAsync(candidates.ToArray()).WaitAsync(HangGuard);
            stopwatch.Stop();

            // Then the responsive server is chosen, and the choice did not cost one timeout per
            // candidate: screened one after another, the six silent candidates alone would take 3s.
            result.Server.ShouldBe(promptServer);
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(2000);
        }

        [Fact]
        public async Task GetFastestServerByLatencyAsync_NoCandidateRespondsWithinTheCeiling_ReportsNoServersAvailable()
        {
            // SCENARIO: No server can be reached

            // Given no candidate answers before the ceiling.
            const int ceilingMilliseconds = 300;

            using var mockHttp = new MockHttpMessageHandler();
            NeverRespond(mockHttp, "*/latency.txt");

            var httpClient = mockHttp.ToHttpClient();
            var settings = new OoklaSpeedtestSettings
            {
                ServerScreening = new() { ServerTimeoutMilliseconds = ceilingMilliseconds }
            };

            var speedtest = new OoklaSpeedtest(settings, httpClient, new DelayProviderStub());
            IServer[] servers =
            [
                new Server { Url = "http://silent1.com/", Sponsor = "Silent1", Location = "Location1" },
                new Server { Url = "http://silent2.com/", Sponsor = "Silent2", Location = "Location2" }
            ];

            // When
            var exception = await Record.ExceptionAsync(() => speedtest.GetFastestServerByLatencyAsync(servers).WaitAsync(HangGuard));

            // Then the condition is raised as its own type, and the message the console surfaces as
            // "Error: No servers available" is unchanged.
            exception.ShouldNotBeNull();
            exception.ShouldBeOfType<NoServersAvailableException>();
            exception.Message.ShouldBe("No servers available");
        }

        /// <summary>
        /// Refuses any request that is not for a web address, exactly as the transport NetPace ships
        /// with does, and hands everything else to <paramref name="innerHandler"/>.
        /// </summary>
        private sealed class WebAddressesOnlyHandler(HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var scheme = request.RequestUri!.Scheme;

                if (scheme != Uri.UriSchemeHttp && scheme != Uri.UriSchemeHttps)
                {
                    throw new NotSupportedException($"The '{scheme}' scheme is not supported.");
                }

                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}
