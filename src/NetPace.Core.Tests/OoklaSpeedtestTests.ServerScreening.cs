using System;
using System.Diagnostics;
using System.Net.Http;
using NetPace.Core.Clients.Ookla;
using RichardSzalay.MockHttp;
using Shouldly;

namespace NetPace.Core.Tests;

/// <summary>
/// Choosing a server and measuring a server are two different jobs. Screening answers "is this
/// server reachable, and roughly how fast?" for every discovered candidate, concurrently and under
/// one overall ceiling, so a reachable-but-slow server is ranked rather than rejected and the
/// comparison covers more than whichever server answered first.
/// </summary>
public sealed partial class OoklaSpeedtestTests
{
    private const string ScreeningResponseBody = "test=test";

    /// <summary>
    /// Bounds a call that a broken ceiling would leave waiting forever, so that regression fails
    /// the test instead of hanging the run. It is not a timing assertion.
    /// </summary>
    private static readonly TimeSpan HangGuard = TimeSpan.FromSeconds(10);

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

    /// <summary>
    /// Never answers the screening request for <paramref name="latencyUrl"/>, so the request stays
    /// pending until screening gives up on it at the ceiling. That leaves the ceiling as the only
    /// timer in the test, rather than a second one racing it.
    /// </summary>
    private static void NeverRespond(MockHttpMessageHandler mockHttp, string latencyUrl)
    {
        mockHttp.When(latencyUrl)
                .Respond(_ => new TaskCompletionSource<HttpResponseMessage>().Task);
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
    }

    [Fact]
    public async Task GetFastestServerByLatencyAsync_CandidateAnswersThenFails_IsRankedOnWhatItDidAnswer()
    {
        // Given a fast candidate that answers its first screening request and then fails, against a
        // slower candidate that answers every request. Screening keeps what the first candidate did
        // achieve, so it is ranked on that figure rather than discarded for the later failure - the
        // all-or-nothing probe this replaced would have dropped it and chosen the slower server.
        const int steadyDelayMilliseconds = 60;
        using var mockHttp = new MockHttpMessageHandler();

        var flakyRequests = 0;
        mockHttp.When("http://flaky.com/latency.txt")
                .Respond(_ =>
                {
                    if (Interlocked.Increment(ref flakyRequests) > 1)
                    {
                        throw new HttpRequestException("Connection reset.");
                    }

                    return Task.FromResult(new HttpResponseMessage { Content = new StringContent(ScreeningResponseBody) });
                });

        RespondAfterDelay(mockHttp, "http://steady.com/latency.txt", steadyDelayMilliseconds);

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(new OoklaSpeedtestSettings(), httpClient, new DelayProviderStub());
        var flakyServer = new Server { Url = "http://flaky.com/", Sponsor = "FlakySponsor", Location = "FlakyLocation" };
        var steadyServer = new Server { Url = "http://steady.com/", Sponsor = "SteadySponsor", Location = "SteadyLocation" };

        // When
        var result = await speedtest.GetFastestServerByLatencyAsync([flakyServer, steadyServer]);

        // Then
        result.Server.ShouldBe(flakyServer);
        result.LatencyMilliseconds.ShouldBeLessThan(steadyDelayMilliseconds);
    }

    [Fact]
    public async Task GetFastestServerByLatencyAsync_SlowerServerListedFirst_StillChoosesTheFastestCandidate()
    {
        // SCENARIO: A run auto-selects a server

        // Given a slower server ahead of a faster one in the candidate list, both reachable. The
        // gap between them is wide enough that scheduling noise cannot reorder the two.
        using var mockHttp = new MockHttpMessageHandler();
        RespondAfterDelay(mockHttp, "http://slower.com/latency.txt", 150);
        RespondAfterDelay(mockHttp, "http://faster.com/latency.txt", 10);

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(new OoklaSpeedtestSettings(), httpClient, new DelayProviderStub());
        var slowerServer = new Server { Url = "http://slower.com/", Sponsor = "SlowerSponsor", Location = "SlowerLocation" };
        var fasterServer = new Server { Url = "http://faster.com/", Sponsor = "FasterSponsor", Location = "FasterLocation" };

        // When
        var result = await speedtest.GetFastestServerByLatencyAsync([slowerServer, fasterServer]);

        // Then the slower server is ranked below the faster one rather than being the only
        // candidate compared.
        result.Server.ShouldBe(fasterServer);
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
        // Given a server list - which comes from a remote feed NetPace does not control - carrying
        // one entry that is not a web address, ahead of a server that answers normally. The
        // handler refuses such an address with the exception the shipped transport raises, so
        // the test fails the way a real run would rather than on a mock's own unmatched request.
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
        // Regression guard for the ceiling itself (refs #239): servers answering in 20s-60s per
        // request could stall a run for minutes, so the time taken to choose a server must stay
        // bounded and must not grow with the number of slow candidates. That is why this test
        // asserts on wall-clock time, which tests here otherwise avoid.

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
            ServerDiscovery = new() { ServerTimeoutMilliseconds = ceilingMilliseconds }
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
            ServerDiscovery = new() { ServerTimeoutMilliseconds = ceilingMilliseconds }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient, new DelayProviderStub());
        IServer[] servers =
        [
            new Server { Url = "http://silent1.com/", Sponsor = "Silent1", Location = "Location1" },
            new Server { Url = "http://silent2.com/", Sponsor = "Silent2", Location = "Location2" }
        ];

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetFastestServerByLatencyAsync(servers).WaitAsync(HangGuard));

        // Then the message the console surfaces as "Error: No servers available" is unchanged.
        exception.ShouldNotBeNull();
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
