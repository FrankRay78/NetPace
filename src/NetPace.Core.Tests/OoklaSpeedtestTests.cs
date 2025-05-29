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

    [Fact]
    public async Task GetServerLatencyAsync_ShouldThrow_WhenCancelledByUser()
    {
        // Given
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("http://slowserver.com/latency.txt")
                .Respond(async _ =>
                {
                    await Task.Delay(500);
                    return new HttpResponseMessage { Content = new StringContent("test=test") };
                });

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(httpClientOverride: httpClient);

        var server = new Clients.Testing.Server
        {
            Url = "http://slowserver.com/",
            Sponsor = "SlowSponsor",
            Location = "SlowLocation"
        };

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50);

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetServerLatencyAsync(server, cts.Token));

        // Then
        Assert.NotNull(exception);
        Assert.IsType<TaskCanceledException>(exception);
    }

    [Fact]
    public async Task GetServerLatencyAsync_ShouldThrow_WhenRequestTimesOut()
    {
        // Given
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("http://timeoutserver.com/latency.txt")
                .Respond(async _ =>
                {
                    await Task.Delay(1000);
                    return new HttpResponseMessage { Content = new StringContent("test=test") };
                });

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            LatencyTest = new()
            {
                DefaultHttpTimeoutMilliseconds = 10,
                LatencyTestIterations = 1
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);

        var server = new Clients.Testing.Server
        {
            Url = "http://timeoutserver.com/",
            Sponsor = "TimeoutSponsor",
            Location = "TimeoutLocation"
        };

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetServerLatencyAsync(server));

        // Then
        Assert.NotNull(exception);
        Assert.IsType<TaskCanceledException>(exception);
    }

    // --- GetFastestServerByLatencyAsync ---

    [Fact]
    public async Task GetFastestServerByLatencyAsync_ShouldReturnServerWithLowestLatency()
    {
        await Task.CompletedTask;
        throw new NotImplementedException();
    }

    [Fact]
    public async Task GetFastestServerByLatencyAsync_ShouldThrow_WhenAllServersFail()
    {
        await Task.CompletedTask;
        throw new NotImplementedException();
    }

    // --- GetDownloadSpeedAsync ---

    [Fact]
    public async Task GetDownloadSpeedAsync_ShouldReturnSpeedTestResult_WhenSuccessful()
    {
        await Task.CompletedTask;
        throw new NotImplementedException();
    }

    [Fact]
    public async Task GetDownloadSpeedAsync_ShouldReportProgress_WhileDownloading()
    {
        await Task.CompletedTask;
        throw new NotImplementedException();
    }

    [Fact]
    public async Task GetDownloadSpeedAsync_ShouldHandlePartialFailures_AndContinue()
    {
        await Task.CompletedTask;
        throw new NotImplementedException();
    }

    // --- GetUploadSpeedAsync ---

    [Fact]
    public async Task GetUploadSpeedAsync_ShouldReturnSpeedTestResult_WhenSuccessful()
    {
        await Task.CompletedTask;
        throw new NotImplementedException();
    }

    [Fact]
    public async Task GetUploadSpeedAsync_ShouldReportProgress_WhileUploading()
    {
        await Task.CompletedTask;
        throw new NotImplementedException();
    }

    [Fact]
    public async Task GetUploadSpeedAsync_ShouldHandlePartialFailures_AndContinue()
    {
        await Task.CompletedTask;
        throw new NotImplementedException();
    }
}