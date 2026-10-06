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

    [Fact]
    public async Task GetFastestServerByLatencyAsync_ReachableButSlowServer_IsSelectedRatherThanRejected()
    {
        // SCENARIO: A run auto-selects a server

        // Given a single server that is plainly reachable, but slow enough that a full latency
        // measurement of it cannot fit inside the selection ceiling.
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("http://slow.com/latency.txt")
                .Respond(async _ =>
                {
                    await Task.Delay(150);
                    return new HttpResponseMessage { Content = new StringContent(ScreeningResponseBody) };
                });

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(new OoklaSpeedtestSettings(), httpClient, new DelayProviderStub());
        var slowServer = new Server { Url = "http://slow.com/", Sponsor = "SlowSponsor", Location = "SlowLocation" };

        // When
        var result = await speedtest.GetFastestServerByLatencyAsync([slowServer]);

        // Then
        result.Server.ShouldBe(slowServer);
    }

    [Fact]
    public async Task GetFastestServerByLatencyAsync_SlowerServerListedFirst_StillChoosesTheFastestCandidate()
    {
        // SCENARIO: A run auto-selects a server

        // Given a slower server ahead of a faster one in the candidate list, both reachable.
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("http://slower.com/latency.txt")
                .Respond(async _ =>
                {
                    await Task.Delay(40);
                    return new HttpResponseMessage { Content = new StringContent(ScreeningResponseBody) };
                });
        mockHttp.When("http://faster.com/latency.txt")
                .Respond(async _ =>
                {
                    await Task.Delay(10);
                    return new HttpResponseMessage { Content = new StringContent(ScreeningResponseBody) };
                });

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            LatencyTest = new()
            {
                LatencyTestIterations = 20,
                LatencyTestIntervalMilliseconds = 0
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient, new DelayProviderStub());
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
        // Regression guard for the ceiling itself: before any budget existed, servers answering in
        // 20s-60s per request could stall a run for minutes, so the time taken to choose a server
        // must stay bounded and must not grow with the number of slow candidates.

        // Given six candidates far slower than the ceiling, and one that answers at once.
        const int unresponsiveCandidateCount = 6;
        const int unresponsiveDelayMilliseconds = 900;
        const int ceilingMilliseconds = 500;

        using var mockHttp = new MockHttpMessageHandler();
        var candidates = new List<IServer>();

        for (var candidate = 0; candidate < unresponsiveCandidateCount; candidate++)
        {
            var url = $"http://unresponsive{candidate}.com/";
            mockHttp.When(url + "latency.txt")
                    .Respond(async _ =>
                    {
                        await Task.Delay(unresponsiveDelayMilliseconds);
                        return new HttpResponseMessage { Content = new StringContent(ScreeningResponseBody) };
                    });
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
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*/latency.txt")
                .Respond(async _ =>
                {
                    await Task.Delay(900);
                    return new HttpResponseMessage { Content = new StringContent(ScreeningResponseBody) };
                });

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            ServerDiscovery = new() { ServerTimeoutMilliseconds = 300 }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient, new DelayProviderStub());
        IServer[] servers =
        [
            new Server { Url = "http://silent1.com/", Sponsor = "Silent1", Location = "Location1" },
            new Server { Url = "http://silent2.com/", Sponsor = "Silent2", Location = "Location2" }
        ];

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetFastestServerByLatencyAsync(servers));

        // Then the message the console turns into "No speed test servers were found" is unchanged.
        exception.ShouldNotBeNull();
        exception.Message.ShouldBe("No servers available");
    }
}
