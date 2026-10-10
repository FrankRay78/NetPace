using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using NetPace.Core.Clients.Ookla;
using RichardSzalay.MockHttp;
using Shouldly;

namespace NetPace.Core.Tests;

/// <summary>
/// Per-request diagnostics: the detail a triager needs about how a run went, reported on its own
/// channel so a consumer that renders no progress display still receives it.
/// </summary>
/// <remarks>
/// Every request gets a record, including the latency probes made while choosing a server and the
/// probe that resolves where uploads go. A request that failed carries the cause attributable to
/// that request - the information the provider previously reduced to a failure count and threw away.
/// </remarks>
public sealed partial class OoklaSpeedtestTests
{
    private const string TestServerUrl = "http://example.com/";

    [Fact]
    public async Task GetDownloadSpeedAsync_WhenEveryRequestSucceeds_ReportsOneDiagnosticPerRequest()
    {
        // Given a server that answers every download with a fixed payload.
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[2048])
        });

        var settings = new OoklaSpeedtestSettings
        {
            DownloadTest = new()
            {
                DownloadSizes = [100],
                DownloadSizeIterations = 4,
                DownloadParallelTasks = 2
            }
        };
        var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient());
        var server = new Server { Url = TestServerUrl, Sponsor = "Test", Location = "Test" };

        var diagnostics = new List<RequestDiagnostic>();

        // When the download test runs with a diagnostic reporter.
        var result = await speedtest.GetDownloadSpeedAsync(server, null, Collect(diagnostics));

        // Then every request is accounted for individually, and the records agree with the counts.
        diagnostics.Count.ShouldBe(4);
        diagnostics.Count(d => d.Outcome == RequestOutcome.Succeeded).ShouldBe(result.RequestsSucceeded);
        diagnostics.Select(d => d.Sequence).OrderBy(sequence => sequence).ShouldBe([1, 2, 3, 4]);
        diagnostics.ShouldAllBe(d => d.Url.StartsWith(TestServerUrl));
        diagnostics.ShouldAllBe(d => d.BytesProcessed == 2048);
        diagnostics.ShouldAllBe(d => d.FailureReason == null);
    }

    [Fact]
    public async Task GetDownloadSpeedAsync_WhenEveryRequestFails_ReportsTheCauseAgainstEachRequest()
    {
        // Given a server whose every download fails with a nested transport fault.
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Throw(new HttpRequestException(
            "The SSL connection could not be established.",
            new InvalidOperationException("Authentication failed because the remote party sent a TLS alert.")));

        var settings = new OoklaSpeedtestSettings
        {
            DownloadTest = new()
            {
                DownloadSizes = [100],
                DownloadSizeIterations = 2,
                DownloadParallelTasks = 1
            }
        };
        var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient());
        var server = new Server { Url = TestServerUrl, Sponsor = "Test", Location = "Test" };

        var diagnostics = new List<RequestDiagnostic>();

        // When the download test runs.
        var result = await speedtest.GetDownloadSpeedAsync(server, null, Collect(diagnostics));

        // Then each failed request names its own cause, down the whole exception chain.
        diagnostics.Count.ShouldBe(2);
        diagnostics.Count(d => d.Outcome == RequestOutcome.Failed).ShouldBe(result.RequestsFailed);
        diagnostics.ShouldAllBe(d => d.Outcome == RequestOutcome.Failed);
        diagnostics.ShouldAllBe(d => d.BytesProcessed == 0);
        diagnostics.ShouldAllBe(d => d.FailureType == "HttpRequestException");
        diagnostics.ShouldAllBe(d => d.FailureReason!.Contains("The SSL connection could not be established."));
        diagnostics.ShouldAllBe(d => d.FailureReason!.Contains("Authentication failed because the remote party sent a TLS alert."));
        diagnostics.ShouldAllBe(d => d.FailureReason!.Contains(" <- "));
    }

    [Fact]
    public async Task GetUploadSpeedAsync_WithDiagnostics_RecordsTheEndpointProbeAndEveryUpload()
    {
        // Given a server that accepts the probe and every upload.
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var settings = new OoklaSpeedtestSettings
        {
            UploadTest = new()
            {
                UploadIncrements = 1,
                UploadSizeIterations = 2,
                UploadParallelTasks = 1
            }
        };
        var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient());
        var server = new Server { Url = TestServerUrl, Sponsor = "Test", Location = "Test" };

        var diagnostics = new List<RequestDiagnostic>();

        // When the upload test runs.
        await speedtest.GetUploadSpeedAsync(server, null, Collect(diagnostics));

        // Then the request that resolved where uploads go is recorded alongside the uploads, and is
        // numbered first because it ran first.
        diagnostics.Count.ShouldBe(3);
        diagnostics.Select(d => d.Sequence).OrderBy(sequence => sequence).ShouldBe([1, 2, 3]);
        diagnostics.ShouldAllBe(d => d.Outcome == RequestOutcome.Succeeded);
        diagnostics.Single(d => d.Sequence == 1).BytesProcessed.ShouldBe(0);
    }

    [Fact]
    public async Task GetServerLatencyAsync_WithDiagnostics_ReportsOneDiagnosticPerProbe()
    {
        // Given a server answering the latency probe correctly.
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond("text/plain", "test=test");

        var settings = new OoklaSpeedtestSettings
        {
            LatencyTest = new()
            {
                LatencyTestIterations = 3,
                LatencyTestIntervalMilliseconds = 0
            }
        };
        var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient());
        var server = new Server { Url = TestServerUrl, Sponsor = "Test", Location = "Test" };

        var diagnostics = new List<RequestDiagnostic>();

        // When latency is measured.
        await speedtest.GetServerLatencyAsync(server, null, Collect(diagnostics));

        // Then each probe is recorded individually, against the latency endpoint.
        diagnostics.Count.ShouldBe(3);
        diagnostics.Select(d => d.Sequence).ShouldBe([1, 2, 3]);
        diagnostics.ShouldAllBe(d => d.Outcome == RequestOutcome.Succeeded);
        diagnostics.ShouldAllBe(d => d.Url == TestServerUrl + "latency.txt");
    }

    [Fact]
    public async Task GetServerLatencyAsync_WhenTheProbeFails_ReportsTheCauseBeforeSurfacingIt()
    {
        // Given a server that cannot be reached.
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Throw(new HttpRequestException("Name or service not known"));

        var speedtest = new OoklaSpeedtest(new OoklaSpeedtestSettings(), mockHttp.ToHttpClient());
        var server = new Server { Url = TestServerUrl, Sponsor = "Test", Location = "Test" };

        var diagnostics = new List<RequestDiagnostic>();

        // When latency is measured, the failure still surfaces to the caller.
        await Should.ThrowAsync<HttpRequestException>(
            () => speedtest.GetServerLatencyAsync(server, null, Collect(diagnostics)));

        // Then the request that caused it was recorded first, so a diagnostic log names the cause.
        diagnostics.ShouldNotBeEmpty();
        diagnostics[0].Outcome.ShouldBe(RequestOutcome.Failed);
        diagnostics[0].FailureReason.ShouldBe("Name or service not known");
        diagnostics[0].FailureType.ShouldBe("HttpRequestException");
    }

    [Fact]
    public async Task GetFastestServerByLatencyAsync_WithDiagnostics_ReportsEveryScreeningRequest()
    {
        // Given two candidates, both answering the screening probe.
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond("text/plain", "test=test");

        var settings = new OoklaSpeedtestSettings
        {
            ServerDiscovery = new()
            {
                ScreeningRequestCount = 2,
                ServerTimeoutMilliseconds = 30000
            }
        };
        var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient());
        IServer[] servers =
        [
            new Server { Url = "http://one.example.com/", Sponsor = "One", Location = "One" },
            new Server { Url = "http://two.example.com/", Sponsor = "Two", Location = "Two" }
        ];

        var diagnostics = new List<RequestDiagnostic>();

        // When the candidates are screened.
        await speedtest.GetFastestServerByLatencyAsync(servers, null, Collect(diagnostics));

        // Then every screening request is recorded, for every candidate - a failed selection would
        // otherwise carry no cause at all.
        diagnostics.Count.ShouldBe(4);
        diagnostics.Count(d => d.Url.StartsWith("http://one.example.com/")).ShouldBe(2);
        diagnostics.Count(d => d.Url.StartsWith("http://two.example.com/")).ShouldBe(2);
        diagnostics.ShouldAllBe(d => d.Outcome == RequestOutcome.Succeeded);
    }

    [Fact]
    public async Task GetDownloadSpeedAsync_WithAnInjectedClock_StampsWhenEachRequestStarted()
    {
        // Given a clock that starts at a known instant and advances a second per read.
        var clock = new DeterministicTimeProvider(new DateTimeOffset(1980, 1, 1, 10, 5, 0, TimeSpan.Zero), TimeSpan.FromSeconds(1));

        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[16])
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
        var server = new Server { Url = TestServerUrl, Sponsor = "Test", Location = "Test" };

        var diagnostics = new List<RequestDiagnostic>();

        // When the download test runs.
        await speedtest.GetDownloadSpeedAsync(server, null, Collect(diagnostics));

        // Then the stamps come from the injected clock, not from the wall clock.
        diagnostics.Select(d => d.StartedAt).ShouldBe(
        [
            new DateTime(1980, 1, 1, 10, 5, 0),
            new DateTime(1980, 1, 1, 10, 5, 1)
        ]);
    }

    [Fact]
    public async Task GetDownloadSpeedAsync_WhenTheByteBudgetCutsRequestsOff_RecordsThemAsCancelled()
    {
        // Given a byte budget one request's payload is enough to exhaust, and more requests queued
        // behind it than the budget allows. Only the first request answers immediately; the rest
        // are held long enough to still be in flight when it trips the cap, and every request gets
        // its own permit so none is queued. Both matter: with all eight answering at once they can
        // clear the counting block before the cap fires and nothing is cancelled, and with a queue
        // a freed permit lets a waiter start before the cancellation reaches it, so the cancelled
        // count moves. The test lost both races intermittently under full-suite load.
        var served = 0;
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(async () =>
        {
            // Yields before anything else, so no request can complete during the enumeration that
            // starts them. If the first one did, the rest would throw from the semaphore with the
            // token already cancelled, never start, and report nothing - and the total below would
            // see 1 record instead of 8.
            await Task.Yield();

            if (Interlocked.Increment(ref served) > 1)
            {
                await Task.Delay(250, CancellationToken.None);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[1024 * 1024])
            };
        });

        var settings = new OoklaSpeedtestSettings
        {
            DownloadTest = new()
            {
                DownloadSizes = [100],
                DownloadSizeIterations = 8,
                DownloadParallelTasks = 8,
                DownloadSizeMb = 1
            }
        };
        var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient());
        var server = new Server { Url = TestServerUrl, Sponsor = "Test", Location = "Test" };

        var diagnostics = new List<RequestDiagnostic>();

        // When the download test runs under the budget.
        var result = await speedtest.GetDownloadSpeedAsync(server, null, Collect(diagnostics));

        // Then the records still agree with the counts the result reports, and a request excluded by
        // the budget is accounted for as cancelled rather than silently dropped or counted a failure.
        diagnostics.Count(d => d.Outcome == RequestOutcome.Succeeded).ShouldBe(result.RequestsSucceeded);
        diagnostics.Count(d => d.Outcome == RequestOutcome.Failed).ShouldBe(result.RequestsFailed);
        diagnostics.Count.ShouldBe(8);
        diagnostics.Count(d => d.Outcome == RequestOutcome.Succeeded).ShouldBe(1);
        diagnostics.Count(d => d.Outcome == RequestOutcome.Cancelled).ShouldBe(7);
        diagnostics.Where(d => d.Outcome == RequestOutcome.Cancelled)
            .ShouldAllBe(d => d.FailureReason == "byte budget reached");
    }

    [Fact]
    public async Task GetDownloadSpeedAsync_WithoutADiagnosticReporter_StillMeasures()
    {
        // Given a working server and no diagnostic reporter.
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[512])
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
        var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient());
        var server = new Server { Url = TestServerUrl, Sponsor = "Test", Location = "Test" };

        // When the download test runs with both reporters absent.
        var result = await speedtest.GetDownloadSpeedAsync(server, null, null);

        // Then the measurement is unaffected.
        result.RequestsSucceeded.ShouldBe(2);
        result.BytesProcessed.ShouldBe(1024);
    }

    /// <summary>
    /// Collects reported diagnostics inline, on the reporting thread, under a lock: the download and
    /// upload paths report from parallel requests, so an unsynchronised list would tear.
    /// </summary>
    [Fact]
    public async Task GetFastestServerByLatencyAsync_WhenACandidateIsUnreachable_RecordsItAsFailedWithTheCause()
    {
        // Given a candidate whose transport refuses the connection.
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Throw(new HttpRequestException("Connection refused"));

        var settings = new OoklaSpeedtestSettings
        {
            ServerDiscovery = new()
            {
                ScreeningRequestCount = 1,
                ServerTimeoutMilliseconds = 30000
            }
        };
        var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient());
        IServer[] servers = [new Server { Url = "http://one.example.com/", Sponsor = "One", Location = "One" }];

        var diagnostics = new List<RequestDiagnostic>();

        // When screening runs and finds nothing reachable.
        var noServers = await Should.ThrowAsync<Exception>(() => speedtest.GetFastestServerByLatencyAsync(servers, null, Collect(diagnostics)));
        noServers.Message.ShouldBe("No servers available");

        // Then the candidate is recorded as failed, carrying the cause and the exception type: a
        // verdict on the server, not an abandonment. (How that renders on the wire is the console's
        // business and is pinned by its snapshot, not here.)
        var record = diagnostics.ShouldHaveSingleItem();
        record.Outcome.ShouldBe(RequestOutcome.Failed);
        record.FailureReason.ShouldNotBeNull().ShouldContain("Connection refused");
        record.FailureType.ShouldBe(nameof(HttpRequestException));
    }

    [Fact]
    public async Task GetFastestServerByLatencyAsync_WhenTheCallerCancels_DoesNotBlameTheScreeningCeiling()
    {
        // Given a caller who cancels, against a ceiling nowhere near expiring.
        using var cancellation = new CancellationTokenSource();

        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(async () =>
        {
            cancellation.Cancel();
            await Task.Delay(50, CancellationToken.None);
            cancellation.Token.ThrowIfCancellationRequested();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var settings = new OoklaSpeedtestSettings
        {
            ServerDiscovery = new()
            {
                ScreeningRequestCount = 1,
                ServerTimeoutMilliseconds = 30000
            }
        };
        var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient());
        IServer[] servers = [new Server { Url = "http://one.example.com/", Sponsor = "One", Location = "One" }];

        var diagnostics = new List<RequestDiagnostic>();

        // When the pass is cancelled mid-screening.
        await Should.ThrowAsync<OperationCanceledException>(
            () => speedtest.GetFastestServerByLatencyAsync(servers, null, Collect(diagnostics), cancellation.Token));

        // Then the record blames the caller by name. Asserting only that it does NOT say "ceiling"
        // would pass on an empty list, and would still pass if the caller branch were reverted -
        // the ceiling token is linked to the caller's, so it reads as cancelled either way.
        var record = diagnostics.ShouldHaveSingleItem();
        record.Outcome.ShouldBe(RequestOutcome.Cancelled);
        record.FailureReason.ShouldBe("cancelled by caller");
    }

    [Fact]
    public async Task GetUploadSpeedAsync_WhenTheEndpointProbeIsRejected_RecordsItAsFailed()
    {
        // Given a server that rejects the zero-byte endpoint probe but accepts uploads.
        using var mockHttp = new MockHttpMessageHandler();
        var probeRejected = false;

        mockHttp.When("*").Respond(_ =>
        {
            if (!probeRejected)
            {
                probeRejected = true;
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var settings = new OoklaSpeedtestSettings
        {
            UploadTest = new()
            {
                UploadIncrements = 1,
                UploadSizeIterations = 1,
                UploadParallelTasks = 1
            }
        };
        var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient());
        var server = new Server { Url = TestServerUrl, Sponsor = "Test", Location = "Test" };

        var diagnostics = new List<RequestDiagnostic>();

        // When the upload test runs.
        await speedtest.GetUploadSpeedAsync(server, null, Collect(diagnostics));

        // Then the probe is not recorded as a success. PostAsync does not throw on a non-success
        // status, so without this the one request that can silently misdirect every upload after it
        // is the one record that always reads ok.
        var probe = diagnostics.OrderBy(d => d.Sequence).First();
        probe.Outcome.ShouldBe(RequestOutcome.Failed);
        probe.FailureReason.ShouldNotBeNull().ShouldContain("503");
    }

    [Fact]
    public async Task GetFastestServerByLatencyAsync_WhenTheCeilingExpires_BlamesTheCeiling()
    {
        // Given a candidate slower than the pass is willing to wait for.
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(async () =>
        {
            await Task.Delay(5000, CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var settings = new OoklaSpeedtestSettings
        {
            ServerDiscovery = new()
            {
                ScreeningRequestCount = 1,
                ServerTimeoutMilliseconds = 100
            }
        };
        var speedtest = new OoklaSpeedtest(settings, mockHttp.ToHttpClient());
        IServer[] servers = [new Server { Url = "http://slow.example.com/", Sponsor = "Slow", Location = "Slow" }];

        var diagnostics = new List<RequestDiagnostic>();

        // When the ceiling expires before the candidate answers.
        await Should.ThrowAsync<Exception>(() => speedtest.GetFastestServerByLatencyAsync(servers, null, Collect(diagnostics)));

        // Then a record attributing it to the ceiling is what the other half of the classification
        // produces. Nothing else asserts this branch, so without it a regression here ships green
        // while the test for the caller branch stays happy.
        diagnostics.ShouldAllBe(d => d.Outcome == RequestOutcome.Cancelled);
        diagnostics.ShouldAllBe(d => d.FailureReason == "screening ceiling reached");
    }

    private static IProgress<RequestDiagnostic> Collect(List<RequestDiagnostic> sink)
    {
        return new SynchronousProgress<RequestDiagnostic>(diagnostic =>
        {
            lock (sink)
            {
                sink.Add(diagnostic);
            }
        });
    }
}
