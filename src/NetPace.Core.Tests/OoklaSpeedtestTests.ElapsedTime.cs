using System;
using System.Net;
using System.Net.Http;
using NetPace.Core;
using NetPace.Core.Clients.Ookla;
using RichardSzalay.MockHttp;
using Shouldly;

namespace NetPace.Core.Tests;

/// <summary>
/// A caller that supplies a time provider is supplying the clock every elapsed-time measurement is
/// taken against, not just the one that stamps a diagnostic record. These tests hold that: each
/// figure the provider reports follows a clock the test moves itself, so nothing here waits on the
/// wall clock and nothing here needs a tolerance.
/// </summary>
public sealed partial class OoklaSpeedtestTests
{
    /// <summary>
    /// A fixed instant for a controlled clock to start from. Any instant would do - it is named
    /// only so a test reads as a measurement rather than an arbitrary number.
    /// </summary>
    private static readonly DateTimeOffset ClockStart = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Answers the request for <paramref name="url"/> after moving <paramref name="clock"/> on by
    /// <paramref name="duration"/>, so the request takes exactly that much of the supplied clock's
    /// time and none of the wall clock's.
    /// </summary>
    private static void RespondAfterAdvancing(MockHttpMessageHandler mockHttp, string url, ControlledTimeProvider clock, TimeSpan duration) =>
        RespondAfterAdvancing(mockHttp, url, clock, duration, Task.CompletedTask);

    /// <summary>
    /// As <see cref="RespondAfterAdvancing(MockHttpMessageHandler, string, ControlledTimeProvider, TimeSpan)"/>,
    /// but the request is not answered until <paramref name="gate"/> completes.
    /// </summary>
    /// <remarks>
    /// Screening runs every candidate concurrently against one clock, so without an order between
    /// them one candidate's advance can land inside another candidate's measurement and neither
    /// figure is predictable. Gating the later candidate on the earlier one's completion orders the
    /// two without anything waiting on the wall clock. The gate is bounded by
    /// <see cref="HangGuard"/> so a regression that never opens it fails the test instead of
    /// hanging the run.
    /// </remarks>
    private static void RespondAfterAdvancing(MockHttpMessageHandler mockHttp, string url, ControlledTimeProvider clock, TimeSpan duration, Task gate)
    {
        mockHttp.When(url)
                .Respond(async _ =>
                {
                    await gate.WaitAsync(HangGuard);
                    clock.Advance(duration);
                    return new HttpResponseMessage { Content = new StringContent(ScreeningResponseBody) };
                });
    }

    [Fact]
    public async Task GetDownloadSpeedAsync_ATransferTakingOneSecondOfTheSuppliedClock_ReportsThoseBytesPerSecond()
    {
        // SCENARIO: Speed follows the supplied time provider

        // Given a single download of a known size, which takes exactly one second of a clock the
        // test moves itself.
        const int payloadBytes = 1_000_000;
        var clock = new ControlledTimeProvider(ClockStart);

        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*")
                .Respond(_ =>
                {
                    clock.Advance(TimeSpan.FromSeconds(1));
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(new byte[payloadBytes])
                    });
                });

        var settings = new OoklaSpeedtestSettings
        {
            DownloadTest = new()
            {
                DownloadSizes = [100],
                DownloadSizeIterations = 1,
                DownloadParallelTasks = 1
            }
        };

        var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient(), timeProviderOverride: clock);
        var server = new Server { Url = "http://testserver.com/", Sponsor = "Sponsor", Location = "Location" };

        // When
        var result = await speedtest.GetDownloadSpeedAsync(server);

        // Then the reported speed is those bytes over that second, exactly.
        result.BytesProcessed.ShouldBe(payloadBytes);
        result.ElapsedMilliseconds.ShouldBe(1000);
        result.GetBitsPerSecond().ShouldBe(payloadBytes * 8L);
    }

    [Fact]
    public async Task GetUploadSpeedAsync_ATransferTakingOneSecondOfTheSuppliedClock_ReportsThoseBytesPerSecond()
    {
        // Given a single upload of a known size, which takes exactly one second of a clock the test
        // moves itself. The endpoint probe that precedes the measured uploads carries no bytes and
        // is answered without the clock moving, so it is outside the measurement.
        const int payloadKb = 200;
        var payloadBytes = payloadKb * 1024;
        var clock = new ControlledTimeProvider(ClockStart);
        var probeAnswered = false;

        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*")
                .Respond(_ =>
                {
                    if (probeAnswered)
                    {
                        clock.Advance(TimeSpan.FromSeconds(1));
                    }

                    probeAnswered = true;
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                });

        var settings = new OoklaSpeedtestSettings
        {
            UploadTest = new()
            {
                UploadSizeIncrementKb = payloadKb,
                UploadIncrements = 1,
                UploadSizeIterations = 1,
                UploadParallelTasks = 1
            }
        };

        var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient(), timeProviderOverride: clock);
        var server = new Server { Url = "http://testserver.com/", Sponsor = "Sponsor", Location = "Location" };

        // When
        var result = await speedtest.GetUploadSpeedAsync(server);

        // Then
        result.BytesProcessed.ShouldBe(payloadBytes);
        result.ElapsedMilliseconds.ShouldBe(1000);
        result.GetBitsPerSecond().ShouldBe(payloadBytes * 8L);
    }
}
