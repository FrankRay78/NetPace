using System.Net;
using NetPace.Core.Clients.Ookla;
using RichardSzalay.MockHttp;
using static System.Net.WebRequestMethods;

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
        mockHttp.When("http://*")
                .Respond("application/xml", fakeXml);

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(httpClientOverride: httpClient);

        // When
        var servers = await speedtest.GetServersAsync();

        // Then
        Assert.Single(servers);
        Assert.Equal("TestLocation", servers[0].Location);
        Assert.Equal("TestSponsor", servers[0].Sponsor);
        Assert.Equal("http://testurl.com", servers[0].Url);
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
        mockHttp.When("http://*")
                .Respond("application/xml", fakeXml);

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(httpClientOverride: httpClient);

        // When
        var servers = await speedtest.GetServersAsync();

        // Then
        Assert.Equal(2, servers.Length);
        Assert.Collection(servers,
            s =>
            {
                Assert.Equal("Location1", s.Location);
                Assert.Equal("Sponsor1", s.Sponsor);
                Assert.Equal("http://testurl1.com", s.Url);
            },
            s =>
            {
                Assert.Equal("Location2", s.Location);
                Assert.Equal("Sponsor2", s.Sponsor);
                Assert.Equal("http://testurl2.com", s.Url);
            });
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
        mockHttp.When("http://*")
                .Respond("application/xml", fakeXml);

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(httpClientOverride: httpClient);

        // When
        var servers = await speedtest.GetServersAsync();

        // Then
        Assert.Empty(servers);
    }

    [Fact]
    public async Task GetServersAsync_ShouldThrow_WhenResponseIsInvalid()
    {
        // Given
        var invalidXml = "Not XML at all <><>??";

        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("http://*")
                .Respond("application/xml", invalidXml);

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(httpClientOverride: httpClient);

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetServersAsync());

        // Then
        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
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

        var server = new Clients.Testing.Server
        {
            Url = "http://testserver.com/",
            Sponsor = "Sponsor",
            Location = "Location"
        };

        // When
        var result = await speedtest.GetServerLatencyAsync(server);

        // Then
        Assert.NotNull(result);
        Assert.Equal(server, result.Server);
        Assert.True(result.Latency >= 0);
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

        var server = new Clients.Testing.Server
        {
            Url = "http://failserver.com/",
            Sponsor = "FailSponsor",
            Location = "FailLocation"
        };

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetServerLatencyAsync(server));

        // Then
        Assert.NotNull(exception);
        Assert.IsType<HttpRequestException>(exception);
        Assert.Equal("Server unreachable", exception.Message);
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

        var server = new Clients.Testing.Server
        {
            Url = "http://badserver.com/",
            Sponsor = "BadSponsor",
            Location = "BadLocation"
        };

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetServerLatencyAsync(server));

        // Then
        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
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

        var fastServer = new Clients.Testing.Server
        {
            Url = "http://fastserver.com/",
            Sponsor = "FastSponsor",
            Location = "FastLocation"
        };

        var slowServer = new Clients.Testing.Server
        {
            Url = "http://slowserver.com/",
            Sponsor = "SlowSponsor",
            Location = "SlowLocation"
        };

        var servers = new[] { slowServer, fastServer };

        // When
        var result = await speedtest.GetFastestServerByLatencyAsync(servers);

        // Then
        Assert.NotNull(result);
        Assert.Equal(fastServer, result.Server);
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
            new Clients.Testing.Server
            {
                Url = "http://fail1.com/",
                Sponsor = "DeadSponsor1",
                Location = "DeadLocation1"
            },
            new Clients.Testing.Server
            {
                Url = "http://fail2.com/",
                Sponsor = "DeadSponsor2",
                Location = "DeadLocation2"
            }
        };

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetFastestServerByLatencyAsync(servers));

        // Then
        Assert.NotNull(exception);
        Assert.IsType<Exception>(exception);
        Assert.Equal("No servers available", exception.Message);
    }


    // --- GetDownloadSpeedAsync ---

    [Fact]
    public async Task GetDownloadSpeedAsync_ShouldReturnSpeedTestResult_WhenSuccessful()
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
                DownloadSizeIterations = 1,
                DownloadParallelTasks = 1
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Clients.Testing.Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

        // When
        var result = await speedtest.GetDownloadSpeedAsync(server);

        // Then
        Assert.NotNull(result);
        Assert.True(result.BytesProcessed == 1024);
        Assert.True(result.ElapsedMilliseconds >= 0);
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
        Assert.NotEmpty(progressReports);
        Assert.Equal(new[] { 25, 50, 75, 100 }, progressReports);
    }

    [Fact]
    public async Task GetDownloadSpeedAsync_ShouldHandlePartialFailures_AndContinue()
    {
        // Given
        var mockHttp = new MockHttpMessageHandler();
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
        Assert.NotNull(result);
        Assert.True(result.BytesProcessed == 1024); // Only one successful response
        Assert.True(result.ElapsedMilliseconds >= 0);
        Assert.NotEmpty(progressReports);
        Assert.Equal(new[] { 50, 100 }, progressReports);
    }

    // --- GetUploadSpeedAsync ---

    [Fact]
    public async Task GetUploadSpeedAsync_ShouldReturnSpeedTestResult_WhenSuccessful()
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
                UploadParallelTasks = 1
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Clients.Testing.Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

        // When
        var result = await speedtest.GetUploadSpeedAsync(server);

        // Then
        Assert.NotNull(result);
        Assert.True(result.BytesProcessed > 0);
        Assert.True(result.ElapsedMilliseconds >= 0);
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
                UploadIncrements = 1, // 1 increment → 10 uploads
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
        Assert.NotEmpty(progressReports);
        Assert.Equal(new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 }, progressReports);
    }

    [Fact]
    public async Task GetUploadSpeedAsync_ShouldHandlePartialFailures_AndContinue()
    {
        // Given
        var mockHttp = new MockHttpMessageHandler();

        // Fail exactly the first upload attempt by matching URL or header
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
                UploadIncrements = 1, // 1 increment → 10 uploads
                UploadParallelTasks = 1
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Clients.Testing.Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };
        var progressReports = new List<int>();

        // When
        var result = await speedtest.GetUploadSpeedAsync(server, progress => progressReports.Add(progress.PercentageComplete));

        // Then
        Assert.NotNull(result);
        Assert.True(result.BytesProcessed > 0); // 9 of 10 uploads succeeded
        Assert.True(result.ElapsedMilliseconds >= 0);
        Assert.NotEmpty(progressReports);
        Assert.Equal(new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 }, progressReports);
    }
}