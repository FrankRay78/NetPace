using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using NetPace.Core.Clients.Ookla;
using RichardSzalay.MockHttp;
using Shouldly;

namespace NetPace.Core.Tests;

/// <summary>
/// Per-request failure aggregation for download and upload tests (issue #206): failed requests are
/// counted, not swallowed; the call does not throw for network outcomes; user cancellation still
/// propagates.
/// </summary>
public sealed partial class OoklaSpeedtestTests
{
    [Fact]
    public async Task GetUploadSpeedAsync_ShouldReportAllRequestsFailed_WhenEveryRequestThrows()
    {
        // SCENARIO: All requests fail, no exception

        // Given every upload POST fails at the transport level.
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Throw(new HttpRequestException("Connection reset by peer"));

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            UploadTest = new()
            {
                UploadIncrements = 1,
                UploadSizeIterations = 4,
                UploadParallelTasks = 2
            }
        };
        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

        // When
        var result = await speedtest.GetUploadSpeedAsync(server);

        // Then the call returns (does not throw), zero succeeded, all attempts failed, zero bytes.
        result.ShouldNotBeNull();
        result.BytesProcessed.ShouldBe(0);
        result.RequestsAttempted.ShouldBeGreaterThan(0);
        result.RequestsSucceeded.ShouldBe(0);
        result.RequestsFailed.ShouldBe(result.RequestsAttempted);
    }

    [Fact]
    public async Task GetDownloadSpeedAsync_ShouldReportAllRequestsFailed_WhenEveryRequestThrows()
    {
        // SCENARIO: All requests fail, no exception (download facet)

        // Given every download GET fails at the transport level.
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Throw(new HttpRequestException("Name or service not known"));

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            DownloadTest = new()
            {
                DownloadSizes = new[] { 100 },
                DownloadSizeIterations = 4,
                DownloadParallelTasks = 2
            }
        };
        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

        // When
        var result = await speedtest.GetDownloadSpeedAsync(server);

        // Then
        result.ShouldNotBeNull();
        result.BytesProcessed.ShouldBe(0);
        result.RequestsAttempted.ShouldBeGreaterThan(0);
        result.RequestsSucceeded.ShouldBe(0);
        result.RequestsFailed.ShouldBe(result.RequestsAttempted);
    }

    [Fact]
    public async Task GetUploadSpeedAsync_ShouldCountOnlySuccessfulBytes_WhenSomeRequestsFail()
    {
        // SCENARIO: Partial failure is measured, not hidden

        // Given a server that rejects the middle request but accepts the others (serialised so the
        // alternation is deterministic).
        var requestNumber = 0;
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(_ =>
        {
            var n = Interlocked.Increment(ref requestNumber);
            return new HttpResponseMessage(n == 2 ? HttpStatusCode.InternalServerError : HttpStatusCode.OK);
        });

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            UploadTest = new()
            {
                UploadIncrements = 1,
                UploadSizeIterations = 3,
                UploadParallelTasks = 1
            }
        };
        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

        // When
        var result = await speedtest.GetUploadSpeedAsync(server);

        // Then only the successful requests contribute bytes; the failure is counted.
        result.ShouldNotBeNull();
        result.RequestsFailed.ShouldBe(1);
        result.RequestsSucceeded.ShouldBe(result.RequestsAttempted - 1);
        result.RequestsSucceeded.ShouldBeGreaterThan(0);
        result.BytesProcessed.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GetUploadSpeedAsync_ShouldPropagateCancellation_WhenCallerCancels()
    {
        // SCENARIO: Cancellation still propagates

        // Given a caller token that is already cancelled.
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(HttpStatusCode.OK);

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            UploadTest = new()
            {
                UploadIncrements = 1,
                UploadSizeIterations = 3,
                UploadParallelTasks = 1
            }
        };
        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // When / Then cancellation surfaces to the caller (it is not swallowed as a failed request).
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await speedtest.GetUploadSpeedAsync(server, cts.Token));
    }
}
