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

    [Fact]
    public async Task GetFastestServerByLatencyAsync_ManyUnresponsiveCandidates_ChoosesWithinTheCeiling()
    {
        // Regression guard for the ceiling itself (refs #239): servers answering in 20s-60s per
        // request could stall a run for minutes, so the time taken to choose a server must stay
        // bounded and must not grow with the number of slow candidates. That is why this test
        // asserts on wall-clock time, which tests here otherwise avoid.

        // Given six candidates far slower than the ceiling, and one that answers at once.
        const int unresponsiveCandidateCount = 6;
        const int unresponsiveDelayMilliseconds = 900;
        const int ceilingMilliseconds = 500;

        using var mockHttp = new MockHttpMessageHandler();
        var candidates = new List<IServer>();

        for (var candidate = 0; candidate < unresponsiveCandidateCount; candidate++)
        {
            var url = $"http://unresponsive{candidate}.com/";
            RespondAfterDelay(mockHttp, url + "latency.txt", unresponsiveDelayMilliseconds);
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
        var result = await speedtest.GetFastestServerByLatencyAsync(candidates.ToArray());
        stopwatch.Stop();

        // Then the responsive server is chosen, and the choice did not cost one timeout per
        // candidate: screened one after another, the six slow candidates alone would take 5.4s.
        result.Server.ShouldBe(promptServer);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(2000);
    }

    [Fact]
    public async Task GetFastestServerByLatencyAsync_NoCandidateRespondsWithinTheCeiling_ReportsNoServersAvailable()
    {
        // SCENARIO: No server can be reached

        // Given every candidate is slower than the ceiling allows.
        const int unresponsiveDelayMilliseconds = 900;
        const int ceilingMilliseconds = 300;

        using var mockHttp = new MockHttpMessageHandler();
        RespondAfterDelay(mockHttp, "*/latency.txt", unresponsiveDelayMilliseconds);

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
        var exception = await Record.ExceptionAsync(() => speedtest.GetFastestServerByLatencyAsync(servers));

        // Then the message the console surfaces as "Error: No servers available" is unchanged.
        exception.ShouldNotBeNull();
        exception.Message.ShouldBe("No servers available");
    }
}
