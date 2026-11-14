using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using NetPace.Core.Clients.Ookla;
using RichardSzalay.MockHttp;
using Shouldly;

namespace NetPace.Core.Tests;

public sealed partial class OoklaSpeedtestTests
{
    /// <summary>
    /// Per-request failure aggregation for download and upload tests: failed requests are
    /// counted, not swallowed; the call does not throw for network outcomes; user cancellation still
    /// propagates.
    /// </summary>
    public sealed class Failures
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
            result.RequestsSucceeded.ShouldBe(0);
            result.RequestsFailed.ShouldBeGreaterThan(0);
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
                DownloadSizes = new[] { 100 },
                DownloadSizeIterations = 4,
                DownloadTest = new()
                {
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
            result.RequestsSucceeded.ShouldBe(0);
            result.RequestsFailed.ShouldBeGreaterThan(0);
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

        [Fact]
        public async Task GetServerLatencyAsync_ShouldRaiseACancellationCarryingATimeout_WhenAProbeTimesOut()
        {
            // A caller cannot act on a timeout it cannot recognise, and a bare cancellation reads
            // as "the caller asked to stop". The probe timeout is therefore raised in the shape
            // .NET's own HTTP client uses for its Timeout: a cancellation with a TimeoutException
            // inside.

            // Given a server that never answers, and a probe timeout short enough to fire first.
            using var handler = new NeverAnsweringHandler();
            using var httpClient = new HttpClient(handler, disposeHandler: false);
            var settings = new OoklaSpeedtestSettings
            {
                LatencyTest = new() { HttpTimeoutMilliseconds = 20, LatencyTestIterations = 1, LatencyTestIntervalMilliseconds = 0 }
            };
            var speedtest = new OoklaSpeedtest(settings, httpClient);
            var server = new Server { Url = "http://slow.example/", Sponsor = "Test", Location = "Test" };

            // When. Recorded rather than caught through Should.ThrowAsync: a probe timeout
            // completes its task as cancelled, and Should.ThrowAsync replaces such a task's
            // exception with a bare one of its own, hiding the very thing this test reads. The
            // 20 ms probe timeout is the guard against a hang.
            var failure = await Record.ExceptionAsync(() => speedtest.GetServerLatencyAsync(server));

            // Then the timeout is recognisable, and is not reported as the caller's cancellation.
            failure.ShouldBeAssignableTo<OperationCanceledException>();
            failure!.InnerException.ShouldBeOfType<TimeoutException>();
        }

        [Fact]
        public async Task GetServerLatencyAsync_ShouldRaiseAPlainCancellation_WhenTheCallerCancelsAProbe()
        {
            // The caller's own cancellation must stay distinguishable from a timeout, or a run the
            // user stopped would be reported as a failure.

            // Given a server that never answers, and a caller that cancels while a probe is in
            // flight.
            using var cancellation = new CancellationTokenSource();
            using var handler = new NeverAnsweringHandler(_ => cancellation.Cancel());
            using var httpClient = new HttpClient(handler, disposeHandler: false);
            var settings = new OoklaSpeedtestSettings
            {
                LatencyTest = new() { LatencyTestIterations = 1, LatencyTestIntervalMilliseconds = 0 }
            };
            var speedtest = new OoklaSpeedtest(settings, httpClient);
            var server = new Server { Url = "http://slow.example/", Sponsor = "Test", Location = "Test" };

            // When (recorded rather than caught, for the reason the test above gives).
            var failure = await Record.ExceptionAsync(() => speedtest.GetServerLatencyAsync(server, cancellation.Token));

            // Then
            failure.ShouldBeAssignableTo<OperationCanceledException>();
            failure!.InnerException.ShouldNotBeOfType<TimeoutException>();
        }

        [Fact]
        public async Task GetServerLatencyAsync_ShouldRaiseAPlainCancellation_WhenTheCallerCancelsAsTheProbeTimesOut()
        {
            // Both deadlines have fired by the time the probe timeout is caught, so this is the
            // only case in which the second half of the filter decides the outcome. Without it a
            // user who pressed Ctrl-C would be told the server timed out.

            // Given a caller whose token is cancelled by the probe timeout itself, so both are
            // cancelled when the catch filter runs.
            using var cancellation = new CancellationTokenSource();
            using var handler = new NeverAnsweringHandler(token => token.Register(cancellation.Cancel));
            using var httpClient = new HttpClient(handler, disposeHandler: false);
            var settings = new OoklaSpeedtestSettings
            {
                LatencyTest = new() { HttpTimeoutMilliseconds = 20, LatencyTestIterations = 1, LatencyTestIntervalMilliseconds = 0 }
            };
            var speedtest = new OoklaSpeedtest(settings, httpClient);
            var server = new Server { Url = "http://slow.example/", Sponsor = "Test", Location = "Test" };

            // When (recorded rather than caught, for the reason the first timeout test gives).
            var failure = await Record.ExceptionAsync(() => speedtest.GetServerLatencyAsync(server, cancellation.Token));

            // Then the caller's cancellation wins the tie and no timeout is claimed.
            failure.ShouldBeAssignableTo<OperationCanceledException>();
            failure!.InnerException.ShouldNotBeOfType<TimeoutException>();
        }

        [Fact]
        public async Task GetServersAsync_ShouldRaiseACancellationCarryingATimeout_WhenTheFetchTimesOut()
        {
            // The server-list fetch applies no timeout of its own - it relies on HttpClient.Timeout
            // - which is why it needed no change. That makes .NET's own shape load-bearing here, so
            // it is pinned rather than assumed: if it ever stopped holding, a timed-out fetch would
            // quietly stop being recognisable and no other test would notice.

            // Given a server that never answers, and a client whose own timeout ends the fetch.
            using var handler = new NeverAnsweringHandler();
            using var httpClient = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromMilliseconds(20) };
            var speedtest = new OoklaSpeedtest(new OoklaSpeedtestSettings(), httpClient);

            // When (recorded rather than caught, for the reason the first timeout test gives).
            var failure = await Record.ExceptionAsync(() => speedtest.GetServersAsync());

            // Then the fetch reports its timeout in the same shape the probe does.
            failure.ShouldBeAssignableTo<OperationCanceledException>();
            failure!.InnerException.ShouldBeOfType<TimeoutException>();
        }

        [Fact]
        public async Task GetUploadSpeedAsync_ShouldCountNeitherSucceededNorFailed_WhenTheByteBudgetSkipsRequests()
        {
            // SCENARIO: Requests skipped by the byte budget are not counted as failures

            // Given a server that accepts every request, and a run whose total-byte cap is reached
            // long before the scheduled requests are exhausted.
            using var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("*").Respond(HttpStatusCode.OK);

            var httpClient = mockHttp.ToHttpClient();
            var settings = new OoklaSpeedtestSettings
            {
                UploadTest = new()
                {
                    UploadIncrements = 1,
                    UploadSizeIncrementKb = 1024,
                    UploadSizeIterations = 20,
                    UploadParallelTasks = 1,
                    UploadSizeMb = 2
                }
            };
            var speedtest = new OoklaSpeedtest(settings, httpClient);
            var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

            // When
            var result = await speedtest.GetUploadSpeedAsync(server);

            // Then the requests the cap cut short are excluded from both counts rather than being
            // reported as failures.
            result.ShouldNotBeNull();
            result.RequestsFailed.ShouldBe(0);
            result.RequestsSucceeded.ShouldBeGreaterThan(0);
            (result.RequestsSucceeded + result.RequestsFailed).ShouldBeLessThan(20);
        }

        /// <summary>
        /// Never answers a request: it waits for the request's own cancellation, so whichever
        /// deadline fires first - the probe timeout or the caller's token - is the one the probe
        /// reports.
        /// </summary>
        /// <param name="onRequest">
        /// Run as each request arrives, before the wait begins, and handed the request's own token
        /// so a test can act on whichever deadline cancels it.
        /// </param>
        private sealed class NeverAnsweringHandler(Action<CancellationToken>? onRequest = null) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                onRequest?.Invoke(cancellationToken);

                await Task.Delay(Timeout.Infinite, cancellationToken);

                throw new InvalidOperationException("An infinite delay completed.");
            }
        }
    }
}
