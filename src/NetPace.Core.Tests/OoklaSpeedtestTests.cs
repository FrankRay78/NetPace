using System.Net;
using NetPace.Core.Clients.Ookla;
using RichardSzalay.MockHttp;
using Shouldly;

namespace NetPace.Core.Tests;

public class OoklaSpeedtestTests
{
    // --- GetServersAsync ---

    [Fact]
    public async Task GetServersAsync_ShouldReturnSingleServer_WhenResponseHasOneServer()
    {
        // Given
        var fakeXml = """
        <settings>
            <servers>
                <server id="1" url="http://testurl.com" lat="0" lon="0" name="TestLocation" country="TestCountry" sponsor="TestSponsor"/>
            </servers>
        </settings>
        """;

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*")
                .Respond("application/xml", fakeXml);

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(httpClientOverride: httpClient);

        // When
        var servers = await speedtest.GetServersAsync();

        // Then
        servers.ShouldNotBeNull();
        servers.ShouldHaveSingleItem();
        servers[0].Location.ShouldBe("TestLocation");
        servers[0].Sponsor.ShouldBe("TestSponsor");
        servers[0].Url.ShouldBe("http://testurl.com");
    }

    [Fact]
    public async Task GetServersAsync_ShouldReturnMultipleServers_WhenResponseHasMultipleServers()
    {
        // Given
        var fakeXml = """
        <settings>
            <servers>
                <server id="1" url="http://testurl1.com" lat="0" lon="0" name="Location1" country="Country1" sponsor="Sponsor1"/>
                <server id="2" url="http://testurl2.com" lat="1" lon="1" name="Location2" country="Country2" sponsor="Sponsor2"/>
            </servers>
        </settings>
        """;

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*")
                .Respond("application/xml", fakeXml);

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(httpClientOverride: httpClient);

        // When
        var servers = await speedtest.GetServersAsync();

        // Then
        servers.ShouldNotBeNull();
        servers.Length.ShouldBe(2);
        servers[0].Location.ShouldBe("Location1");
        servers[0].Sponsor.ShouldBe("Sponsor1");
        servers[0].Url.ShouldBe("http://testurl1.com");
        servers[1].Location.ShouldBe("Location2");
        servers[1].Sponsor.ShouldBe("Sponsor2");
        servers[1].Url.ShouldBe("http://testurl2.com");
    }

    [Fact]
    public async Task GetServersAsync_ShouldReturnEmptyArray_WhenNoServersFound()
    {
        // Given
        var fakeXml = """
        <settings>
            <servers>
            </servers>
        </settings>
        """;

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*")
                .Respond("application/xml", fakeXml);

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(httpClientOverride: httpClient);

        // When
        var servers = await speedtest.GetServersAsync();

        // Then
        servers.ShouldNotBeNull();
        servers.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetServersAsync_ShouldThrow_WhenResponseIsInvalid()
    {
        // Given
        var invalidXml = "Not XML at all <><>??";

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*")
                .Respond("application/xml", invalidXml);

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(httpClientOverride: httpClient);

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetServersAsync());

        // Then
        exception.ShouldNotBeNull();
        exception.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task GetServersAsync_ShouldCancel_WhenTokenIsCancelled()
    {
        // Given
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(async _ =>
        {
            // Simulate slow response.
            await Task.Delay(1000);

            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(httpClientOverride: httpClient);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(200);

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetServersAsync(cts.Token));

        // Then
        exception.ShouldNotBeNull();
        exception.ShouldBeAssignableTo<OperationCanceledException>();
        cts.IsCancellationRequested.ShouldBeTrue();
    }

    // --- GetServerLatencyAsync ---

    [Fact]
    public async Task GetServerLatencyAsync_ShouldReturnLatency_WhenResponseIsValid()
    {
        // Given
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("http://testserver.com/latency.txt")
                .Respond("text/plain", "test=test");

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(httpClientOverride: httpClient);
        var server = new Clients.Testing.Server { Url = "http://testserver.com/", Sponsor = "Sponsor", Location = "Location" };

        // When
        var result = await speedtest.GetServerLatencyAsync(server);

        // Then
        result.ShouldNotBeNull();
        result.Server.ShouldBe(server);
        result.Latency.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetServerLatencyAsync_ShouldThrow_WhenLatencyTestFails()
    {
        // Given
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("http://failserver.com/latency.txt")
                .Throw(new HttpRequestException("Server unreachable"));

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(httpClientOverride: httpClient);
        var server = new Clients.Testing.Server { Url = "http://failserver.com/", Sponsor = "FailSponsor", Location = "FailLocation" };

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetServerLatencyAsync(server));

        // Then
        exception.ShouldNotBeNull();
        exception.ShouldBeOfType<HttpRequestException>();
        exception.Message.ShouldBe("Server unreachable");
    }

    [Fact]
    public async Task GetServerLatencyAsync_ShouldThrow_WhenResponseIsInvalid()
    {
        // Given
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("http://badserver.com/latency.txt")
                .Respond("text/plain", "garbage");

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(httpClientOverride: httpClient);
        var server = new Clients.Testing.Server { Url = "http://badserver.com/", Sponsor = "BadSponsor", Location = "BadLocation" };

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetServerLatencyAsync(server));

        // Then
        exception.ShouldNotBeNull();
        exception.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task GetServerLatencyAsync_ShouldCancel_WhenTokenIsCancelled()
    {
        // Given
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(async _ =>
        {
            // Simulate slow response.
            await Task.Delay(1000);

            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(httpClientOverride: httpClient);
        var server = new Clients.Testing.Server { Url = "http://testserver.com/", Sponsor = "Sponsor", Location = "Location" };

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(200);

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetServerLatencyAsync(server, cts.Token));

        // Then
        exception.ShouldNotBeNull();
        exception.ShouldBeAssignableTo<OperationCanceledException>();
        cts.IsCancellationRequested.ShouldBeTrue();
    }

    // --- GetFastestServerByLatencyAsync ---

    [Fact]
    public async Task GetFastestServerByLatencyAsync_ShouldReturnServerWithLowestLatency()
    {
        // Given
        var mockHttp = new MockHttpMessageHandler();

        // Fast server responds quickly
        mockHttp.When("http://fastserver.com/latency.txt")
                .Respond(async _ =>
                {
                    await Task.Delay(10);
                    return new HttpResponseMessage { Content = new StringContent("test=test") };
                });

        // Slow server responds more slowly
        mockHttp.When("http://slowserver.com/latency.txt")
                .Respond(async _ =>
                {
                    await Task.Delay(50);
                    return new HttpResponseMessage { Content = new StringContent("test=test") };
                });

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            LatencyTest = new()
            {
                DefaultHttpTimeoutMilliseconds = 500,
                LatencyTestIterations = 1
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var fastServer = new Clients.Testing.Server { Url = "http://fastserver.com/", Sponsor = "FastSponsor", Location = "FastLocation" };
        var slowServer = new Clients.Testing.Server { Url = "http://slowserver.com/", Sponsor = "SlowSponsor", Location = "SlowLocation" };
        var servers = new[] { slowServer, fastServer };

        // When
        var result = await speedtest.GetFastestServerByLatencyAsync(servers);

        // Then
        result.ShouldNotBeNull();
        result.Server.ShouldBe(fastServer);
    }

    [Fact]
    public async Task GetFastestServerByLatencyAsync_ShouldThrow_WhenAllServersFail()
    {
        // Given
        var mockHttp = new MockHttpMessageHandler();

        mockHttp.When("http://fail1.com/latency.txt")
                .Throw(new HttpRequestException("Unreachable 1"));

        mockHttp.When("http://fail2.com/latency.txt")
                .Throw(new HttpRequestException("Unreachable 2"));

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            LatencyTest = new()
            {
                DefaultHttpTimeoutMilliseconds = 100,
                LatencyTestIterations = 1
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var servers = new[]
        {
            new Clients.Testing.Server { Url = "http://fail1.com/", Sponsor = "DeadSponsor1", Location = "DeadLocation1" },
            new Clients.Testing.Server { Url = "http://fail2.com/", Sponsor = "DeadSponsor2", Location = "DeadLocation2" }
        };

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetFastestServerByLatencyAsync(servers));

        // Then
        exception.ShouldNotBeNull();
        exception.ShouldBeOfType<Exception>();
        exception.Message.ShouldBe("No servers available");
    }

    [Fact]
    public async Task GetFastestServerByLatencyAsync_ShouldCancel_WhenTokenIsCancelled()
    {
        // Given
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(async _ =>
        {
            // Simulate slow response.
            await Task.Delay(1000);

            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(httpClientOverride: httpClient);
        var fastServer = new Clients.Testing.Server { Url = "http://fastserver.com/", Sponsor = "FastSponsor", Location = "FastLocation" };
        var slowServer = new Clients.Testing.Server { Url = "http://slowserver.com/", Sponsor = "SlowSponsor", Location = "SlowLocation" };
        var servers = new[] { slowServer, fastServer };

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(200);

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetFastestServerByLatencyAsync(servers, cts.Token));

        // Then
        exception.ShouldNotBeNull();
        exception.ShouldBeAssignableTo<OperationCanceledException>();
        cts.IsCancellationRequested.ShouldBeTrue();
    }

    // --- GetDownloadSpeedAsync ---

    [Fact]
    public async Task GetDownloadSpeedAsync_ShouldReturnSpeedTestResult_WhenSuccessful()
    {
        // Given
        long actualBytes = 0;

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(request =>
        {
            // Respond with fixed 1KB payload for simplicity.
            var body = new string('X', 1024);

            Interlocked.Add(ref actualBytes, body.Length);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            });
        });

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            DownloadTest = new()
            {
                DownloadParallelTasks = 1
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Clients.Testing.Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

        // When
        var result = await speedtest.GetDownloadSpeedAsync(server);

        // Then
        result.ShouldNotBeNull();
        result.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(0);
        result.BytesProcessed.ShouldBe(actualBytes);
    }

    [Fact]
    public async Task GetDownloadSpeedAsync_ShouldCancel_WhenTokenIsCancelled()
    {
        // Given
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(async _ =>
        {
            // Simulate slow response.
            await Task.Delay(1000);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(new string('X', 1024))
            };
        });

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(httpClientOverride: httpClient);
        var server = new Clients.Testing.Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(200);

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetDownloadSpeedAsync(server, cts.Token));

        // Then
        exception.ShouldNotBeNull();
        exception.ShouldBeAssignableTo<OperationCanceledException>();
        cts.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task GetDownloadSpeedAsync_ShouldReportProgress_WhileDownloading()
    {
        // Given
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond("text/plain", new string('X', 1024));

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            DownloadTest = new()
            {
                DownloadSizes = new[] { 100 },
                DownloadSizeIterations = 4,
                DownloadParallelTasks = 1
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Clients.Testing.Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };
        var progressReports = new List<int>();

        // When
        await speedtest.GetDownloadSpeedAsync(server, progress =>
        {
            progressReports.Add(progress.PercentageComplete);
        });

        // Then
        progressReports.ShouldNotBeNull();
        progressReports.ShouldBe(new[] { 25, 50, 75, 100 });
    }

    [Fact]
    public async Task GetDownloadSpeedAsync_ShouldHandlePartialFailures_AndContinue()
    {
        // Given
        var mockHttp = new MockHttpMessageHandler();

        // Fail the first download attempt
        mockHttp.When("http://example.com/random100x100.jpg?r=0").Throw(new HttpRequestException("Simulated failure"));
        mockHttp.When("http://example.com/random100x100.jpg?r=1").Respond("text/plain", new string('X', 1024));

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            DownloadTest = new()
            {
                DownloadSizes = new[] { 100 },
                DownloadSizeIterations = 2,
                DownloadParallelTasks = 1
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Clients.Testing.Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };
        var progressReports = new List<int>();

        // When
        var result = await speedtest.GetDownloadSpeedAsync(server, progress => progressReports.Add(progress.PercentageComplete));

        // Then
        result.ShouldNotBeNull();
        result.BytesProcessed.ShouldBe(1024); // One download was successful
        result.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(0);
        progressReports.ShouldNotBeNull();
        progressReports.ShouldBe(new[] { 50, 100 });
    }

    // --- GetUploadSpeedAsync ---

    [Fact]
    public async Task GetUploadSpeedAsync_ShouldReturnSpeedTestResult_WhenSuccessful()
    {
        // Given
        long actualBytes = 0;

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(async request =>
        {
            if (request != null && request.Content != null)
            {
                var body = await request.Content.ReadAsByteArrayAsync();

                Interlocked.Add(ref actualBytes, body.LongLength);
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            UploadTest = new()
            {
                UploadParallelTasks = 1
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Clients.Testing.Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

        // When
        var result = await speedtest.GetUploadSpeedAsync(server);

        // Then
        result.ShouldNotBeNull();
        result.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(0);
        result.BytesProcessed.ShouldBe(actualBytes);
    }

    [Theory]
    [InlineData(1)]    // 1MB
    [InlineData(5)]    // 5MB
    [InlineData(10)]   // 10MB
    [InlineData(20)]   // 20MB
    [InlineData(40)]   // 20MB
    [InlineData(100)]  // 100MB
    public async Task GetUploadSpeedAsync_ShouldRespectUploadSize(int uploadSizeMb)
    {
        // Given
        long actualBytes = 0;

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(async request =>
        {
            if (request?.Content != null)
            {
                var body = await request.Content.ReadAsByteArrayAsync();
                Interlocked.Add(ref actualBytes, body.LongLength);
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(httpClientOverride: httpClient);
        var server = new Clients.Testing.Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

        // When
        var result = await speedtest.GetUploadSpeedAsync(server, uploadSizeMb);

        // Then
        result.ShouldNotBeNull();
        result.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(0);

        // HACK: Actual bytes should be less than double the intended upload size
        actualBytes.ShouldBeLessThanOrEqualTo((long)(2 * uploadSizeMb * 1024 * 1024));
    }

    [Fact]
    public async Task GetUploadSpeedAsync_ShouldCancel_WhenTokenIsCancelled()
    {
        // Given
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(async _ =>
        {
            // Simulate slow response.
            await Task.Delay(1000);

            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(httpClientOverride: httpClient);
        var server = new Clients.Testing.Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(200);

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetUploadSpeedAsync(server, cts.Token));

        // Then
        exception.ShouldNotBeNull();
        exception.ShouldBeAssignableTo<OperationCanceledException>();
        cts.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task GetUploadSpeedAsync_ShouldReportProgress_WhileUploading()
    {
        // Given
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(HttpStatusCode.OK);

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            UploadTest = new()
            {
                UploadIncrements = 1,
                UploadSizeIterations = 10,
                UploadParallelTasks = 1
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Clients.Testing.Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };
        var progressReports = new List<int>();

        // When
        await speedtest.GetUploadSpeedAsync(server, progress =>
        {
            progressReports.Add(progress.PercentageComplete);
        });

        // Then
        progressReports.ShouldNotBeNull();
        progressReports.ShouldBe(new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 });
    }

    [Fact]
    public async Task GetUploadSpeedAsync_ShouldHandlePartialFailures_AndContinue()
    {
        // Given
        var mockHttp = new MockHttpMessageHandler();

        // Fail the first upload attempt
        var failureTriggered = false;
        mockHttp.When("*").Respond(request =>
        {
            if (!failureTriggered)
            {
                failureTriggered = true;
                throw new HttpRequestException("Simulated failure");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            UploadTest = new()
            {
                UploadIncrements = 1,
                UploadSizeIterations = 10,
                UploadParallelTasks = 1
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Clients.Testing.Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };
        var progressReports = new List<int>();

        // When
        var result = await speedtest.GetUploadSpeedAsync(server, progress => progressReports.Add(progress.PercentageComplete));

        // Then
        result.ShouldNotBeNull();
        result.BytesProcessed.ShouldBeGreaterThan(0); // 9 of 10 uploads succeeded
        result.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(0);
        progressReports.ShouldNotBeNull();
        progressReports.ShouldBe(new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 });
    }
}