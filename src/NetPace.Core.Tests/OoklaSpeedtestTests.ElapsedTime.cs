using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
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
    /// two without anything waiting on the wall clock.
    /// <para>
    /// A caller opens the gate from a progress report, which is the only seam that fires after a
    /// candidate's elapsed figure has been recorded - signalling from the earlier candidate's own
    /// responder races, because the responder returns before that figure is read. The order
    /// therefore rests on screening reporting progress once per completed candidate and not at
    /// candidate start; an implementation that also reported 0% up front would open the gate early
    /// and put the figures back in doubt. A test using this helper must assert that the gated
    /// candidate was screened at all: a gate that never opens leaves it abandoned at the screening
    /// ceiling, which is shorter than <see cref="HangGuard"/> and would otherwise pass quietly with
    /// only one candidate compared.
    /// </para>
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
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(new byte[payloadBytes])
                    };
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
        var reports = new List<SpeedTestProgress>();

        // When
        var result = await speedtest.GetDownloadSpeedAsync(server, new SynchronousProgress<SpeedTestProgress>(reports.Add));

        // Then the reported speed is those bytes over that second, exactly - and so is the live
        // figure a progress display computes its speed from.
        result.BytesProcessed.ShouldBe(payloadBytes);
        result.ElapsedMilliseconds.ShouldBe(1000);
        result.GetBitsPerSecond().ShouldBe(payloadBytes * 8L);
        reports.ShouldNotBeEmpty();
        reports[^1].ElapsedMilliseconds.ShouldBe(1000);
    }

    [Fact]
    public async Task GetDownloadSpeedAsync_ACappedTransferTakingOneSecondOfTheSuppliedClock_ReportsThatSecondAsItReachesTheCap()
    {
        // Given a download capped at one megabyte, whose single request fills the cap in exactly one
        // second of a clock the test moves itself. Every built-in profile caps a transfer, so a
        // default run sends its final progress report as the cap is reached - a different report
        // from the one an uncapped transfer ends on, and the one a live display shows last.
        const int payloadBytes = 1024 * 1024;
        var clock = new ControlledTimeProvider(ClockStart);

        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*")
                .Respond(_ =>
                {
                    clock.Advance(TimeSpan.FromSeconds(1));
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(new byte[payloadBytes])
                    };
                });

        var settings = new OoklaSpeedtestSettings
        {
            DownloadTest = new()
            {
                DownloadSizes = [100],
                DownloadSizeIterations = 1,
                DownloadParallelTasks = 1,
                DownloadSizeMb = 1
            }
        };

        var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient(), timeProviderOverride: clock);
        var server = new Server { Url = "http://testserver.com/", Sponsor = "Sponsor", Location = "Location" };
        var reports = new List<SpeedTestProgress>();

        // When
        var result = await speedtest.GetDownloadSpeedAsync(server, new SynchronousProgress<SpeedTestProgress>(reports.Add));

        // Then the report that announces the cap carries that second, exactly.
        result.ElapsedMilliseconds.ShouldBe(1000);
        reports.ShouldNotBeEmpty();
        reports[^1].PercentageComplete.ShouldBe(100);
        reports[^1].ElapsedMilliseconds.ShouldBe(1000);
    }

    [Fact]
    public async Task GetUploadSpeedAsync_ATransferTakingOneSecondOfTheSuppliedClock_ReportsThoseBytesPerSecond()
    {
        // Given a single upload of a known size, which takes exactly one second of a clock the test
        // moves itself. The endpoint probe that precedes the measured uploads is resolved before the
        // measurement window opens, so the second it also costs the clock is outside the figure.
        const int payloadKb = 200;
        const int payloadBytes = payloadKb * 1024;
        var clock = new ControlledTimeProvider(ClockStart);

        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*")
                .Respond(_ =>
                {
                    clock.Advance(TimeSpan.FromSeconds(1));
                    return new HttpResponseMessage(HttpStatusCode.OK);
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

    [Fact]
    public async Task GetDownloadSpeedAsync_EachRequestTakingAKnownTimeOfTheSuppliedClock_RecordsThatAgainstTheRequest()
    {
        // Given two sequential downloads, each taking half a second of a clock the test moves
        // itself. A transfer reports a figure per request as well as one for the whole test, and
        // both come off the supplied clock.
        var clock = new ControlledTimeProvider(ClockStart);

        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*")
                .Respond(_ =>
                {
                    clock.Advance(TimeSpan.FromMilliseconds(500));
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(new byte[16])
                    };
                });

        var settings = new OoklaSpeedtestSettings
        {
            DownloadTest = new()
            {
                DownloadSizes = [100],
                DownloadSizeIterations = 2,
                DownloadParallelTasks = 1
            }
        };

        var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient(), timeProviderOverride: clock);
        var server = new Server { Url = "http://testserver.com/", Sponsor = "Sponsor", Location = "Location" };
        var diagnostics = new List<RequestDiagnostic>();

        // When
        var result = await speedtest.GetDownloadSpeedAsync(server, null, Collect(diagnostics));

        // Then each request carries the half-second it took, and the test carries both.
        diagnostics.Select(d => d.ElapsedMilliseconds).ShouldBe([500, 500]);
        result.ElapsedMilliseconds.ShouldBe(1000);
    }

    [Fact]
    public async Task GetFastestServerByLatencyAsync_ASuppliedClockThatNeverMoves_StillGivesUpAtTheCeiling()
    {
        // Given a candidate that never answers, screened against a clock that the test never moves.
        // Waiting is not measurement: the screening ceiling is a bound on how long to wait and stays
        // on the system clock, so a frozen clock must not suspend it. Were the ceiling ever moved
        // onto the supplied clock, this call would never return and HangGuard would fail the test.
        var clock = new ControlledTimeProvider(ClockStart);

        using var mockHttp = new MockHttpMessageHandler();
        NeverRespond(mockHttp, "http://silent.com/latency.txt");

        var speedtest = new OoklaSpeedtest(new OoklaSpeedtestSettings(), mockHttp.ToHttpClient(), new DelayProviderStub(), clock);
        var silentServer = new Server { Url = "http://silent.com/", Sponsor = "SilentSponsor", Location = "SilentLocation" };

        // When
        var exception = await Record.ExceptionAsync(
            () => speedtest.GetFastestServerByLatencyAsync([silentServer]).WaitAsync(HangGuard));

        // Then the pass reached its ceiling and reported no server, rather than hanging.
        exception.ShouldNotBeNull();
        exception.Message.ShouldBe("No servers available");
    }

    [Fact]
    public async Task GetDownloadSpeedAsync_ASuppliedClockThatStepsBackwards_FailsRatherThanReportingANegativeSpeed()
    {
        // Given a clock that steps backwards mid-transfer, which TimeProvider.GetTimestamp forbids.
        // A negative duration divides into a negative speed that reads as a real measurement, so it
        // is rejected at the point of reading rather than reported or quietly clamped to zero.
        var clock = new ControlledTimeProvider(ClockStart);

        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*")
                .Respond(_ =>
                {
                    clock.Advance(TimeSpan.FromSeconds(-1));
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(new byte[1000])
                    };
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
        var exception = await Record.ExceptionAsync(() => speedtest.GetDownloadSpeedAsync(server));

        // Then the clock is named as the cause, rather than a negative speed reaching the caller.
        exception.ShouldBeOfType<InvalidOperationException>();
        exception.Message.ShouldContain("negative elapsed time");
    }

    [Fact]
    public async Task GetDownloadSpeedAsync_ARequestThatFailsAfterAKnownTimeOfTheSuppliedClock_RecordsThatTimeAgainstTheFailure()
    {
        // Given a download that fails after a quarter of a second of a clock the test moves itself.
        var clock = new ControlledTimeProvider(ClockStart);

        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*")
                .Respond(_ =>
                {
                    clock.Advance(TimeSpan.FromMilliseconds(250));
                    throw new HttpRequestException("connection reset");
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
        var diagnostics = new List<RequestDiagnostic>();

        // When
        await speedtest.GetDownloadSpeedAsync(server, null, Collect(diagnostics));

        // Then the failed request's record carries the time it took before failing.
        var record = diagnostics.ShouldHaveSingleItem();
        record.Outcome.ShouldBe(RequestOutcome.Failed);
        record.ElapsedMilliseconds.ShouldBe(250);
    }

    [Fact]
    public async Task GetServerLatencyAsync_AProbeThatFailsAfterAKnownTimeOfTheSuppliedClock_RecordsThatTimeAgainstTheFailure()
    {
        // Given a latency probe that fails after 400 ms of a clock the test moves itself.
        var clock = new ControlledTimeProvider(ClockStart);

        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*")
                .Respond(_ =>
                {
                    clock.Advance(TimeSpan.FromMilliseconds(400));
                    throw new HttpRequestException("connection reset");
                });

        var settings = new OoklaSpeedtestSettings
        {
            LatencyTest = new()
            {
                LatencyTestIterations = 1,
                LatencyTestIntervalMilliseconds = 0
            }
        };

        var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient(), timeProviderOverride: clock);
        var server = new Server { Url = "http://testserver.com/", Sponsor = "Sponsor", Location = "Location" };
        var diagnostics = new List<RequestDiagnostic>();

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetServerLatencyAsync(server, null, Collect(diagnostics)));

        // Then the measurement still fails, and the failed probe's record carries the time it took.
        exception.ShouldNotBeNull();
        var record = diagnostics.ShouldHaveSingleItem();
        record.Outcome.ShouldBe(RequestOutcome.Failed);
        record.ElapsedMilliseconds.ShouldBe(400);
    }

    [Fact]
    public async Task GetFastestServerByLatencyAsync_ACandidateThatFailsAfterAKnownTimeOfTheSuppliedClock_RecordsThatTimeAgainstTheFailure()
    {
        // Given the only candidate, whose screening request fails after 300 ms of a clock the test
        // moves itself.
        var clock = new ControlledTimeProvider(ClockStart);

        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*")
                .Respond(_ =>
                {
                    clock.Advance(TimeSpan.FromMilliseconds(300));
                    throw new HttpRequestException("connection reset");
                });

        var settings = new OoklaSpeedtestSettings
        {
            ServerDiscovery = new()
            {
                ScreeningRequestCount = 1
            }
        };

        var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient(), new DelayProviderStub(), clock);
        var server = new Server { Url = "http://testserver.com/", Sponsor = "Sponsor", Location = "Location" };
        var diagnostics = new List<RequestDiagnostic>();

        // When
        var exception = await Record.ExceptionAsync(
            () => speedtest.GetFastestServerByLatencyAsync([server], null, Collect(diagnostics)).WaitAsync(HangGuard));

        // Then the candidate is ranked out, and its record carries the time the request took.
        exception.ShouldNotBeNull();
        exception.Message.ShouldBe("No servers available");
        var record = diagnostics.ShouldHaveSingleItem();
        record.Outcome.ShouldBe(RequestOutcome.Failed);
        record.ElapsedMilliseconds.ShouldBe(300);
    }

    [Fact]
    public async Task GetUploadSpeedAsync_AnEndpointProbeAnsweringAfterAKnownTimeOfTheSuppliedClock_RecordsThatTimeAgainstTheProbe()
    {
        // Given an upload whose endpoint probe - the empty request that finds where uploads should
        // go, made before the measured uploads - answers after 200 ms of a clock the test moves
        // itself, and whose one measured upload takes a second.
        var clock = new ControlledTimeProvider(ClockStart);
        var requests = 0;

        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*")
                .Respond(_ =>
                {
                    clock.Advance(++requests == 1 ? TimeSpan.FromMilliseconds(200) : TimeSpan.FromSeconds(1));
                    return new HttpResponseMessage(HttpStatusCode.OK);
                });

        var speedtest = new OoklaSpeedtest(SingleUploadSettings(), mockHttp.ToHttpClient(), timeProviderOverride: clock);
        var server = new Server { Url = "http://testserver.com/", Sponsor = "Sponsor", Location = "Location" };
        var diagnostics = new List<RequestDiagnostic>();

        // When
        await speedtest.GetUploadSpeedAsync(server, null, Collect(diagnostics));

        // Then the probe's record carries its 200 ms, and the measured upload's its second.
        diagnostics.Select(d => (d.Sequence, d.Outcome, d.ElapsedMilliseconds))
                   .ShouldBe([(1, RequestOutcome.Succeeded, 200L), (2, RequestOutcome.Succeeded, 1000L)]);
    }

    [Fact]
    public async Task GetUploadSpeedAsync_AnEndpointProbeFailingAfterAKnownTimeOfTheSuppliedClock_RecordsThatTimeAgainstTheProbe()
    {
        // Given an upload whose endpoint probe fails after 300 ms of a clock the test moves itself.
        // Probing is best-effort, so the measured upload still goes ahead, to the configured address.
        var clock = new ControlledTimeProvider(ClockStart);
        var requests = 0;

        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*")
                .Respond(_ =>
                {
                    if (++requests == 1)
                    {
                        clock.Advance(TimeSpan.FromMilliseconds(300));
                        throw new HttpRequestException("connection reset");
                    }

                    clock.Advance(TimeSpan.FromSeconds(1));
                    return new HttpResponseMessage(HttpStatusCode.OK);
                });

        var speedtest = new OoklaSpeedtest(SingleUploadSettings(), mockHttp.ToHttpClient(), timeProviderOverride: clock);
        var server = new Server { Url = "http://testserver.com/", Sponsor = "Sponsor", Location = "Location" };
        var diagnostics = new List<RequestDiagnostic>();

        // When
        await speedtest.GetUploadSpeedAsync(server, null, Collect(diagnostics));

        // Then the failed probe's record carries the time it took before failing.
        diagnostics.Select(d => (d.Sequence, d.Outcome, d.ElapsedMilliseconds))
                   .ShouldBe([(1, RequestOutcome.Failed, 300L), (2, RequestOutcome.Succeeded, 1000L)]);
    }

    /// <summary>
    /// One measured upload, one request at a time, so each request's share of a controlled clock
    /// is known.
    /// </summary>
    private static OoklaSpeedtestSettings SingleUploadSettings() => new()
    {
        UploadTest = new()
        {
            UploadSizeIncrementKb = 200,
            UploadIncrements = 1,
            UploadSizeIterations = 1,
            UploadParallelTasks = 1
        }
    };

    [Fact]
    public async Task GetDownloadSpeedAsync_ASuppliedClockThatStepsBackwardsAsARequestFails_StillRecordsTheRequestsOwnFailure()
    {
        // Given a second request that fails in transport while the clock steps back behind its
        // start, so reading that request's duration also fails. The first request moves the clock
        // on far enough that the step stays ahead of the transfer's own start: only the request's
        // reading is negative, so its record is still written. The clock's fault must not
        // overwrite what the request did: the record still reads as a failure, with the transport
        // fault as its cause.
        var clock = new ControlledTimeProvider(ClockStart);
        var requests = 0;

        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*")
                .Respond(_ =>
                {
                    if (++requests == 1)
                    {
                        clock.Advance(TimeSpan.FromSeconds(5));
                        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[16]) };
                    }

                    clock.Advance(TimeSpan.FromSeconds(-1));
                    throw new HttpRequestException("connection reset");
                });

        var settings = new OoklaSpeedtestSettings
        {
            DownloadTest = new()
            {
                DownloadSizes = [100],
                DownloadSizeIterations = 2,
                DownloadParallelTasks = 1
            }
        };

        var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient(), timeProviderOverride: clock);
        var server = new Server { Url = "http://testserver.com/", Sponsor = "Sponsor", Location = "Location" };
        var diagnostics = new List<RequestDiagnostic>();

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetDownloadSpeedAsync(server, null, Collect(diagnostics)));

        // Then the clock is still named as the run's fault, and the request's record names its own.
        exception.ShouldBeOfType<InvalidOperationException>();
        diagnostics.Count.ShouldBe(2);
        diagnostics[1].Outcome.ShouldBe(RequestOutcome.Failed);
        diagnostics[1].FailureType.ShouldBe(nameof(HttpRequestException));
    }
}
