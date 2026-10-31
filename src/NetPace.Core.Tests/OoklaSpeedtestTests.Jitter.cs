using System;
using System.Net.Http;
using NetPace.Core.Clients.Ookla;
using RichardSzalay.MockHttp;
using Shouldly;

namespace NetPace.Core.Tests;

/// <summary>
/// A latency measurement reports how uneven the round trips were as well as how long they took on
/// average. These tests script each probe's duration on a clock the test moves itself, so every
/// jitter figure here is exactly what the probe times imply and nothing waits on the wall clock.
/// </summary>
public sealed partial class OoklaSpeedtestTests
{
    private const string JitterProbeUrl = "http://testserver.com/latency.txt";
    private const string JitterServerUrl = "http://testserver.com/";

    /// <summary>
    /// The server these tests measure, whose latency endpoint is <see cref="JitterProbeUrl"/>. Its
    /// sponsor and location play no part in a jitter figure.
    /// </summary>
    private static Server JitterTestServer() =>
        new() { Url = JitterServerUrl, Sponsor = "Sponsor", Location = "Location" };

    /// <summary>
    /// Answers each successive request for <paramref name="url"/> after moving
    /// <paramref name="clock"/> on by the next duration in <paramref name="probeMilliseconds"/>,
    /// cycling once the list runs out. Latency probes are issued one at a time, so the order the
    /// durations are handed out in is the order the probes take them.
    /// </summary>
    private static void RespondAfterAdvancingInTurn(MockHttpMessageHandler mockHttp, string url, ControlledTimeProvider clock, params int[] probeMilliseconds)
    {
        var probe = 0;

        mockHttp.When(url)
                .Respond(_ =>
                {
                    var duration = probeMilliseconds[probe % probeMilliseconds.Length];
                    probe++;
                    clock.Advance(TimeSpan.FromMilliseconds(duration));

                    return new HttpResponseMessage { Content = new StringContent(ScreeningResponseBody) };
                });
    }

    /// <summary>
    /// A provider that probes <paramref name="iterations"/> times with no wait between probes, so a
    /// test controls how many round trips a measurement is derived from and nothing pauses.
    /// </summary>
    private static OoklaSpeedtest LatencyProbingSpeedtest(MockHttpMessageHandler mockHttp, ControlledTimeProvider clock, int iterations)
    {
        var settings = new OoklaSpeedtestSettings
        {
            LatencyTest = new()
            {
                LatencyTestIterations = iterations,
                LatencyTestIntervalMilliseconds = 0
            }
        };

        return new OoklaSpeedtest(settings, mockHttp.ToHttpClient(), timeProviderOverride: clock);
    }

    [Fact]
    public async Task GetServerLatencyAsync_ProbesVaryingInTime_ReportsJitterBesideTheLatency()
    {
        // SCENARIO: Jitter is reported with latency

        // Given four probes taking 20, 50, 30 and 40 ms of a clock the test moves itself, so the
        // changes between consecutive probes are 30, 20 and 10 ms.
        var clock = new ControlledTimeProvider(ClockStart);

        using var mockHttp = new MockHttpMessageHandler();
        RespondAfterAdvancingInTurn(mockHttp, JitterProbeUrl, clock, 20, 50, 30, 40);

        var speedtest = LatencyProbingSpeedtest(mockHttp, clock, iterations: 4);
        var server = JitterTestServer();

        // When
        var result = await speedtest.GetServerLatencyAsync(server);

        // Then the result carries both figures: the average round trip, and how uneven the round
        // trips were.
        result.LatencyMilliseconds.ShouldBe(35);
        result.JitterMilliseconds.ShouldBe(20);
    }

    [Fact]
    public async Task GetServerLatencyAsync_ProbesAllTakingTheSameTime_ReportsNoJitter()
    {
        // SCENARIO: Jitter is reported with latency

        // Given five probes that each take exactly 40 ms of a clock the test moves itself, so no
        // probe differs from the one before it.
        const int probeMilliseconds = 40;
        var clock = new ControlledTimeProvider(ClockStart);

        using var mockHttp = new MockHttpMessageHandler();
        RespondAfterAdvancingInTurn(mockHttp, JitterProbeUrl, clock, probeMilliseconds);

        var speedtest = LatencyProbingSpeedtest(mockHttp, clock, iterations: 5);
        var server = JitterTestServer();

        // When
        var result = await speedtest.GetServerLatencyAsync(server);

        // Then a perfectly steady line reports no unevenness at all.
        result.LatencyMilliseconds.ShouldBe(probeMilliseconds);
        result.JitterMilliseconds.ShouldBe(0);
    }

    [Fact]
    public async Task GetServerLatencyAsync_ProbesOfTenAndTwentyMilliseconds_ReportsTenMillisecondsOfJitter()
    {
        // SCENARIO: Jitter is the mean change between consecutive probes

        // Given probe times of 10, 20, 10 and 20 ms of a clock the test moves itself.
        var clock = new ControlledTimeProvider(ClockStart);

        using var mockHttp = new MockHttpMessageHandler();
        RespondAfterAdvancingInTurn(mockHttp, JitterProbeUrl, clock, 10, 20, 10, 20);

        var speedtest = LatencyProbingSpeedtest(mockHttp, clock, iterations: 4);
        var server = JitterTestServer();

        // When
        var result = await speedtest.GetServerLatencyAsync(server);

        // Then the jitter is the mean of the changes from one probe to the next — 10, 10 and 10 —
        // rather than the spread of the probes around their average.
        result.JitterMilliseconds.ShouldBe(10);
    }

    [Fact]
    public async Task GetServerLatencyAsync_AMeasurementOfOneProbe_ReportsNoJitter()
    {
        // SCENARIO: A single probe reports no unevenness

        // Given a measurement configured for a single probe, so there is no consecutive pair of
        // round trips to compare.
        var clock = new ControlledTimeProvider(ClockStart);

        using var mockHttp = new MockHttpMessageHandler();
        RespondAfterAdvancingInTurn(mockHttp, JitterProbeUrl, clock, 70);

        var speedtest = LatencyProbingSpeedtest(mockHttp, clock, iterations: 1);
        var server = JitterTestServer();

        // When
        var result = await speedtest.GetServerLatencyAsync(server);

        // Then the latency is still reported, and the unevenness is reported as none.
        result.LatencyMilliseconds.ShouldBe(70);
        result.JitterMilliseconds.ShouldBe(0);
    }

    [Fact]
    public async Task GetServerLatencyAsync_JitterOfHalfAMillisecond_ReportsOneMillisecond()
    {
        // Given three probes taking 10, 11 and 11 ms of a clock the test moves itself, so the
        // changes between consecutive probes are 1 and 0 ms and the mean change is half a
        // millisecond. A whole-millisecond figure that truncated would report a line with real
        // unevenness as perfectly steady.
        var clock = new ControlledTimeProvider(ClockStart);

        using var mockHttp = new MockHttpMessageHandler();
        RespondAfterAdvancingInTurn(mockHttp, JitterProbeUrl, clock, 10, 11, 11);

        var speedtest = LatencyProbingSpeedtest(mockHttp, clock, iterations: 3);
        var server = JitterTestServer();

        // When
        var result = await speedtest.GetServerLatencyAsync(server);

        // Then the figure rounds up to the nearest whole millisecond rather than down to zero.
        result.JitterMilliseconds.ShouldBe(1);
    }

    [Fact]
    public async Task GetFastestServerByLatencyAsync_AScreenedCandidate_ReportsNoJitter()
    {
        // Given the only candidate, screened at the default request count, with its requests
        // taking 90, 70 and 80 ms of a clock the test moves itself. Screening times all three but
        // keeps only the fastest, so no run of probe times survives for it to derive jitter from.
        var clock = new ControlledTimeProvider(ClockStart);

        using var mockHttp = new MockHttpMessageHandler();
        RespondAfterAdvancingInTurn(mockHttp, JitterProbeUrl, clock, 90, 70, 80);

        var speedtest = new OoklaSpeedtest(httpClientOverride: mockHttp.ToHttpClient(), timeProviderOverride: clock);
        var server = JitterTestServer();

        // When
        var result = await speedtest.GetFastestServerByLatencyAsync([server]);

        // Then the winner is reported at the fastest figure screening saw, and reports no jitter
        // even though it did make consecutive round trips.
        result.LatencyMilliseconds.ShouldBe(70);
        result.JitterMilliseconds.ShouldBe(0);
    }

    [Fact]
    public async Task GetServerLatencyAsync_AMeasurementOfTwoProbes_ReportsTheChangeBetweenThem()
    {
        // SCENARIO: Jitter is the mean change between consecutive probes

        // Given the fewest probes that make a consecutive pair at all, taking 10 and 30 ms of a
        // clock the test moves itself.
        var clock = new ControlledTimeProvider(ClockStart);

        using var mockHttp = new MockHttpMessageHandler();
        RespondAfterAdvancingInTurn(mockHttp, JitterProbeUrl, clock, 10, 30);

        var speedtest = LatencyProbingSpeedtest(mockHttp, clock, iterations: 2);
        var server = JitterTestServer();

        // When
        var result = await speedtest.GetServerLatencyAsync(server);

        // Then the one change between them is the whole of the jitter, rather than the pair being
        // treated as too few to compare.
        result.LatencyMilliseconds.ShouldBe(20);
        result.JitterMilliseconds.ShouldBe(20);
    }

    [Fact]
    public async Task GetServerLatencyAsync_JitterOfAQuarterOfAMillisecond_ReportsNoJitter()
    {
        // Given five probes taking 10, 11, 11, 11 and 11 ms of a clock the test moves itself, so
        // the changes between consecutive probes are 1, 0, 0 and 0 ms and the mean change is a
        // quarter of a millisecond. Rounding to nearest is not rounding up: a single blip across a
        // long run is not unevenness worth a whole millisecond.
        var clock = new ControlledTimeProvider(ClockStart);

        using var mockHttp = new MockHttpMessageHandler();
        RespondAfterAdvancingInTurn(mockHttp, JitterProbeUrl, clock, 10, 11, 11, 11, 11);

        var speedtest = LatencyProbingSpeedtest(mockHttp, clock, iterations: 5);
        var server = JitterTestServer();

        // When
        var result = await speedtest.GetServerLatencyAsync(server);

        // Then the figure rounds down to none.
        result.JitterMilliseconds.ShouldBe(0);
    }
}
