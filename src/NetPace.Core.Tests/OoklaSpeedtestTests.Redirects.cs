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
    /// Upload behaviour when a speed test server redirects the upload endpoint.
    /// <para>
    /// Regression cover, PR #69: a streaming body replaced a buffered one, and every upload against
    /// a server redirecting plain HTTP to HTTPS then failed at 0 bps. Throughput is what is pinned.
    /// </para>
    /// </summary>
    public sealed class Redirects
    {
        [Fact]
        public async Task GetUploadSpeedAsync_ShouldMeasureThroughput_WhenServerRedirectsUploadEndpoint()
        {
            // SCENARIO: Upload endpoint answers with a redirect

            const string uploadUrl = "http://example.com/speedtest/upload.php";
            const string redirectedUrl = "https://migrated.example.com/speedtest/upload.php";

            // Given a server that redirects every upload POST to its migrated endpoint,
            // and a migrated endpoint that accepts the upload.
            using var mockHttp = new MockHttpMessageHandler();

            mockHttp.When(HttpMethod.Post, uploadUrl)
                .Respond(_ => new HttpResponseMessage(HttpStatusCode.TemporaryRedirect)
                {
                    Headers = { Location = new Uri(redirectedUrl) }
                });

            mockHttp.When(HttpMethod.Post, redirectedUrl)
                .Respond(_ => new HttpResponseMessage(HttpStatusCode.OK));

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
            var server = new Server { Url = uploadUrl, Sponsor = "Test", Location = "Test" };

            // When the upload test runs against the redirecting server.
            var result = await speedtest.GetUploadSpeedAsync(server);

            // Then the redirect is honoured and real throughput is reported - not 0 bps.
            result.ShouldNotBeNull();
            result.RequestsFailed.ShouldBe(0);
            result.RequestsSucceeded.ShouldBeGreaterThan(0);
            result.BytesProcessed.ShouldBeGreaterThan(0);
        }

        [Fact]
        public async Task GetUploadSpeedAsync_ShouldMeasureThroughputAtTheConfiguredUrl_WhenResolvingTheEndpointFails()
        {
            // SCENARIO: Endpoint resolution fails before the measured uploads

            const string uploadUrl = "http://example.com/speedtest/upload.php";

            // Given the first request - the one that resolves the endpoint - fails outright,
            // while the configured URL itself accepts uploads normally.
            var requestCount = 0;

            using var mockHttp = new MockHttpMessageHandler();
            mockHttp.When(HttpMethod.Post, uploadUrl).Respond(_ =>
            {
                if (Interlocked.Increment(ref requestCount) == 1)
                {
                    throw new HttpRequestException("Endpoint resolution failed");
                }

                return new HttpResponseMessage(HttpStatusCode.OK);
            });

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
            var server = new Server { Url = uploadUrl, Sponsor = "Test", Location = "Test" };

            // When the upload test runs.
            var result = await speedtest.GetUploadSpeedAsync(server);

            // Then the failure to resolve does not fail the run: uploads proceed against the
            // configured URL and report throughput. Only that URL is mocked, so had resolution
            // yielded anything else the requests would have gone unanswered and failed.
            result.ShouldNotBeNull();
            result.RequestsFailed.ShouldBe(0);
            result.RequestsSucceeded.ShouldBeGreaterThan(0);
            result.BytesProcessed.ShouldBeGreaterThan(0);
        }
    }
}
