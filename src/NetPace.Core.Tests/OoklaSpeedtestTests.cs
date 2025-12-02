using System;
using System.Net;
using System.Text.RegularExpressions;
using NetPace.Core.Clients.Ookla;
using NetPace.Core.Clients.Testing;
using RichardSzalay.MockHttp;
using Shouldly;

namespace NetPace.Core.Tests;

public sealed partial class OoklaSpeedtestTests
{
    #region --- GetServersAsync ---

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

        using var mockHttp = new MockHttpMessageHandler();
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

        using var mockHttp = new MockHttpMessageHandler();
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

        using var mockHttp = new MockHttpMessageHandler();
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

        using var mockHttp = new MockHttpMessageHandler();
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
        using var mockHttp = new MockHttpMessageHandler();
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

    #endregion

    #region --- GetServerLatencyAsync ---

    [Fact]
    public async Task GetServerLatencyAsync_ShouldReturnLatency_WhenResponseIsValid()
    {
        // Given
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("http://testserver.com/latency.txt")
                .Respond("text/plain", "test=test");

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            LatencyTest = new()
            {
                LatencyTestIterations = 1,
                LatencyTestIntervalMilliseconds = 0
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Server { Url = "http://testserver.com/", Sponsor = "Sponsor", Location = "Location" };

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
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("http://failserver.com/latency.txt")
                .Throw(new HttpRequestException("Server unreachable"));

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(httpClientOverride: httpClient);
        var server = new Server { Url = "http://failserver.com/", Sponsor = "FailSponsor", Location = "FailLocation" };

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
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("http://badserver.com/latency.txt")
                .Respond("text/plain", "garbage");

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(httpClientOverride: httpClient);
        var server = new Server { Url = "http://badserver.com/", Sponsor = "BadSponsor", Location = "BadLocation" };

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
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(async _ =>
        {
            // Simulate slow response.
            await Task.Delay(1000);

            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(httpClientOverride: httpClient);
        var server = new Server { Url = "http://testserver.com/", Sponsor = "Sponsor", Location = "Location" };

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(200);

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetServerLatencyAsync(server, cts.Token));

        // Then
        exception.ShouldNotBeNull();
        exception.ShouldBeAssignableTo<OperationCanceledException>();
        cts.IsCancellationRequested.ShouldBeTrue();
    }

    #endregion

    #region --- GetServerLatencyAsync with Progress ---

    [Fact]
    public async Task GetServerLatencyAsync_WithProgress_ShouldReportPingTimesAndPercentage_ForThreePings()
    {
        // Given
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When("http://testserver.com/latency.txt")
            .Respond(async _ =>
            {
                await Task.Delay(60);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("test=test")
                };
            });

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            LatencyTest = new()
            {
                LatencyTestIterations = 3,
                LatencyTestIntervalMilliseconds = 0,
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Server { Url = "http://testserver.com/", Sponsor = "Sponsor", Location = "Location" };
        var progressReports = new List<LatencyTestProgress>();

        // When
        var result = await speedtest.GetServerLatencyAsync(server, progress => progressReports.Add(progress));

        // Then
        result.ShouldNotBeNull();
        result.Server.ShouldBe(server);
        result.Latency.ShouldBe((int)progressReports[2].Pings.Average());

        // Should receive 3 progress reports
        progressReports.Count.ShouldBe(3);

        // First progress report: 1 ping, 33% complete
        progressReports[0].Pings.Count.ShouldBe(1);
        progressReports[0].Pings.ShouldAllBe(p => p >= 50);
        progressReports[0].PercentageComplete.ShouldBe(33);

        // Second progress report: 2 pings, 66% complete
        progressReports[1].Pings.Count.ShouldBe(2);
        progressReports[1].Pings.ShouldAllBe(p => p >= 50);
        progressReports[1].PercentageComplete.ShouldBe(66);

        // Third progress report: 3 pings, 100% complete
        progressReports[2].Pings.Count.ShouldBe(3);
        progressReports[2].Pings.ShouldAllBe(p => p >= 50);
        progressReports[2].PercentageComplete.ShouldBe(100);
    }

    [Theory]
    [InlineData(1, new[] { 100 })]
    [InlineData(2, new[] { 50, 100 })]
    [InlineData(4, new[] { 25, 50, 75, 100 })]
    [InlineData(5, new[] { 20, 40, 60, 80, 100 })]
    [InlineData(10, new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 })]
    public async Task GetServerLatencyAsync_WithProgress_ShouldReportCorrectPercentages(int iterations, int[] expectedPercentage)
    {
        // Given
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("http://testserver.com/latency.txt")
                .Respond("text/plain", "test=test");

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            LatencyTest = new()
            {
                LatencyTestIterations = iterations,
                LatencyTestIntervalMilliseconds = 0,
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Server { Url = "http://testserver.com/", Sponsor = "Sponsor", Location = "Location" };
        var percentageComplete = new List<int>();

        // When
        var result = await speedtest.GetServerLatencyAsync(server, progress => percentageComplete.Add(progress.PercentageComplete));

        // Then
        result.ShouldNotBeNull();
        percentageComplete.ShouldBe(expectedPercentage);
    }

    [Fact]
    public async Task GetServerLatencyAsync_WithProgress_ShouldCancel_WhenTokenIsCancelled()
    {
        // Given
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(async _ =>
        {
            // Simulate slow response.
            await Task.Delay(1000);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(httpClientOverride: httpClient);
        var server = new Server { Url = "http://testserver.com/", Sponsor = "Sponsor", Location = "Location" };
        var progressReports = new List<int>();

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(200);

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetServerLatencyAsync(server, progress => progressReports.Add(progress.PercentageComplete), cts.Token));

        // Then
        exception.ShouldNotBeNull();
        exception.ShouldBeAssignableTo<OperationCanceledException>();
        cts.IsCancellationRequested.ShouldBeTrue();
        progressReports.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetServerLatencyAsync_WithProgress_ShouldPropagateException_WhenProgressCallbackThrows()
    {
        // Given
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("http://testserver.com/latency.txt")
                .Respond("text/plain", "test=test");

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(httpClientOverride: httpClient);
        var server = new Server { Url = "http://testserver.com/", Sponsor = "Sponsor", Location = "Location" };

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetServerLatencyAsync(server, progress =>
        {
            throw new InvalidOperationException("Progress callback failed");
        }));

        // Then
        exception.ShouldNotBeNull();
        exception.ShouldBeOfType<InvalidOperationException>();
        exception.Message.ShouldBe("Progress callback failed");
    }

    [Fact]
    public async Task GetServerLatencyAsync_WithInterval_ShouldRequestDelayBetweenIterations()
    {
        // Given
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("http://testserver.com/latency.txt")
                .Respond("text/plain", "test=test");

        var httpClient = mockHttp.ToHttpClient();
        var delayStub = new DelayProviderStub();
        var settings = new OoklaSpeedtestSettings
        {
            LatencyTest = new()
            {
                LatencyTestIterations = 3,
                LatencyTestIntervalMilliseconds = 50 // 50ms between iterations
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient, delayStub);
        var server = new Server { Url = "http://testserver.com/", Sponsor = "Sponsor", Location = "Location" };

        // When
        await speedtest.GetServerLatencyAsync(server);

        // Then
        // With 3 iterations, we expect 2 delays (between iterations, not before first)
        delayStub.DelayCallCount.ShouldBe(2);
        delayStub.RequestedDelays.ShouldAllBe(d => d == 50);
    }

    [Fact]
    public async Task GetServerLatencyAsync_WithZeroInterval_ShouldNotRequestDelay()
    {
        // Given
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("http://testserver.com/latency.txt")
                .Respond("text/plain", "test=test");

        var httpClient = mockHttp.ToHttpClient();
        var delayStub = new DelayProviderStub();
        var settings = new OoklaSpeedtestSettings
        {
            LatencyTest = new()
            {
                LatencyTestIterations = 3,
                LatencyTestIntervalMilliseconds = 0 // No serverDelay
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient, delayStub);
        var server = new Server { Url = "http://testserver.com/", Sponsor = "Sponsor", Location = "Location" };

        // When
        await speedtest.GetServerLatencyAsync(server);

        // Then
        // With 0ms interval, no delays should be requested
        delayStub.DelayCallCount.ShouldBe(0);
    }

    #endregion

    #region --- GetFastestServerByLatencyAsync ---

    [Fact]
    public async Task GetFastestServerByLatencyAsync_ShouldReturnServerWithLowestLatency()
    {
        // Given
        using var mockHttp = new MockHttpMessageHandler();

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
                LatencyTestIterations = 1,
                LatencyTestIntervalMilliseconds = 0
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var fastServer = new Server { Url = "http://fastserver.com/", Sponsor = "FastSponsor", Location = "FastLocation" };
        var slowServer = new Server { Url = "http://slowserver.com/", Sponsor = "SlowSponsor", Location = "SlowLocation" };
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
        using var mockHttp = new MockHttpMessageHandler();

        mockHttp.When("http://fail1.com/latency.txt")
                .Throw(new HttpRequestException("Unreachable 1"));

        mockHttp.When("http://fail2.com/latency.txt")
                .Throw(new HttpRequestException("Unreachable 2"));

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            LatencyTest = new()
            {
                LatencyTestIterations = 1,
                LatencyTestIntervalMilliseconds = 0
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var servers = new[]
        {
            new Server { Url = "http://fail1.com/", Sponsor = "DeadSponsor1", Location = "DeadLocation1" },
            new Server { Url = "http://fail2.com/", Sponsor = "DeadSponsor2", Location = "DeadLocation2" }
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
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(async _ =>
        {
            // Simulate slow response.
            await Task.Delay(1000);

            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(httpClientOverride: httpClient);
        var fastServer = new Server { Url = "http://fastserver.com/", Sponsor = "FastSponsor", Location = "FastLocation" };
        var slowServer = new Server { Url = "http://slowserver.com/", Sponsor = "SlowSponsor", Location = "SlowLocation" };
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

    #endregion

    #region --- GetFastestServerByLatencyAsync with Progress ---

    [Fact]
    public async Task GetFastestServerByLatencyAsync_WithProgress_ShouldReportProgress_ForThreeServers()
    {
        // Given
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*/latency.txt")
                .Respond("text/plain", "test=test");

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            LatencyTest = new()
            {
                LatencyTestIterations = 1,
                LatencyTestIntervalMilliseconds = 0
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var servers = new[] {
            new Server { Url = "http://server1.com/", Sponsor = "Sponsor1", Location = "Location1" },
            new Server { Url = "http://server2.com/", Sponsor = "Sponsor2", Location = "Location2" },
            new Server { Url = "http://server3.com/", Sponsor = "Sponsor3", Location = "Location3" }
        };
        var progressReports = new List<int>();

        // When
        var result = await speedtest.GetFastestServerByLatencyAsync(servers, progress => progressReports.Add(progress.PercentageComplete));

        // Then
        result.ShouldNotBeNull();
        result.Server.ShouldBeOneOf(servers);
        progressReports.ShouldBe(new[] { 33, 66, 100 });
    }

    [Fact]
    public async Task GetFastestServerByLatencyAsync_WithProgress_ShouldReport100Percent_WithSingleServer()
    {
        // Given
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*/latency.txt")
                .Respond("text/plain", "test=test");

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            LatencyTest = new()
            {
                LatencyTestIterations = 1,
                LatencyTestIntervalMilliseconds = 0
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Server { Url = "http://server.com/", Sponsor = "Sponsor", Location = "Location" };
        var servers = new[] { server };
        var progressReports = new List<int>();

        // When
        var result = await speedtest.GetFastestServerByLatencyAsync(servers, progress => progressReports.Add(progress.PercentageComplete));

        // Then
        result.ShouldNotBeNull();
        result.Server.ShouldBe(server);
        progressReports.ShouldBe(new[] { 100 });
    }

    [Fact]
    public async Task GetFastestServerByLatencyAsync_WithProgress_ShouldReportProgress_EvenWhenServersFail()
    {
        // Given
        using var mockHttp = new MockHttpMessageHandler();

        // First two servers fail, third succeeds
        mockHttp.When("http://fail1.com/latency.txt")
                .Throw(new HttpRequestException("Server unreachable"));
        mockHttp.When("http://fail2.com/latency.txt")
                .Throw(new HttpRequestException("Server unreachable"));
        mockHttp.When("http://success.com/latency.txt")
                .Respond("text/plain", "test=test");

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            LatencyTest = new()
            {
                LatencyTestIterations = 1,
                LatencyTestIntervalMilliseconds = 0
            }
        }; ;

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var failServer1 = new Server { Url = "http://fail1.com/", Sponsor = "Fail1", Location = "Location1" };
        var failServer2 = new Server { Url = "http://fail2.com/", Sponsor = "Fail2", Location = "Location2" };
        var successServer = new Server { Url = "http://success.com/", Sponsor = "Success", Location = "Location3" };
        var servers = new[] { failServer1, failServer2, successServer };
        var progressReports = new List<int>();

        // When
        var result = await speedtest.GetFastestServerByLatencyAsync(servers, progress => progressReports.Add(progress.PercentageComplete));

        // Then
        result.ShouldNotBeNull();
        result.Server.ShouldBe(successServer);
        progressReports.ShouldBe(new[] { 33, 66, 100 });
    }

    [Fact]
    public async Task GetFastestServerByLatencyAsync_WithProgress_ShouldPropagateException_WhenProgressCallbackThrows()
    {
        // Given
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*/latency.txt")
                .Respond("text/plain", "test=test");

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            LatencyTest = new()
            {
                LatencyTestIterations = 1,
                LatencyTestIntervalMilliseconds = 0
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Server { Url = "http://server.com/", Sponsor = "Sponsor", Location = "Location" };
        var servers = new[] { server };

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetFastestServerByLatencyAsync(servers, progress =>
        {
            throw new InvalidOperationException("Progress callback failed");
        }));

        // Then
        exception.ShouldNotBeNull();
        exception.ShouldBeOfType<InvalidOperationException>();
        exception.Message.ShouldBe("Progress callback failed");
    }

    [Fact]
    public async Task GetFastestServerByLatencyAsync_WithProgress_ShouldCancel_WhenTokenIsCancelled()
    {
        // Given
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(async _ =>
        {
            // Simulate slow response.
            await Task.Delay(1000);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(httpClientOverride: httpClient);
        var servers = new[] {
            new Server { Url = "http://server1.com/", Sponsor = "Sponsor1", Location = "Location1" },
            new Server { Url = "http://server2.com/", Sponsor = "Sponsor2", Location = "Location2" },
            new Server { Url = "http://server3.com/", Sponsor = "Sponsor3", Location = "Location3" }
        };
        var progressReports = new List<int>();

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(200);

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetFastestServerByLatencyAsync(servers, progress => progressReports.Add(progress.PercentageComplete), cts.Token));

        // Then
        exception.ShouldNotBeNull();
        exception.ShouldBeAssignableTo<OperationCanceledException>();
        cts.IsCancellationRequested.ShouldBeTrue();
        progressReports.ShouldBeEmpty();
    }

    #endregion

    #region --- GetDownloadSpeedAsync ---

    [Fact]
    public async Task GetDownloadSpeedAsync_ShouldReturnSpeedTestResult_WhenSuccessful()
    {
        // Given
        long actualBytes = 0;

        using var mockHttp = new MockHttpMessageHandler();
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
        var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

        // When
        var result = await speedtest.GetDownloadSpeedAsync(server);

        // Then
        result.ShouldNotBeNull();
        result.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(0);
        result.BytesProcessed.ShouldBe(actualBytes);
    }

    [Theory]
    [InlineData(1)]   // 1MB
    [InlineData(5)]   // 5MB
    [InlineData(10)]  // 10MB
    [InlineData(20)]  // 20MB
    [InlineData(40)]  // 40MB
    [InlineData(100)] // 100MB
    public async Task GetDownloadSpeedAsync_ShouldRespectDownloadSize(int downloadSizeMb)
    {
        // Given
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(request =>
        {
            // Extract dimensions from URL like: /random1500x1500.jpg
            var match = Regex.Match(request?.RequestUri?.AbsolutePath ?? "", @"random(\d+)x(\d+)\.jpg");

            if (!match.Success)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            int width = int.Parse(match.Groups[1].Value);
            int height = int.Parse(match.Groups[2].Value);

            // Simulate byte size: assume 3 bytes per pixel (RGB)
            int byteCount = width * height * 3;
            var content = new ByteArrayContent(new byte[byteCount]);

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            };

            return response;
        });

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            DownloadTest = new()
            {
                DownloadSizes = new[] { 100, 200, 500, 1000, 1500, 2000, 3000, 3500, 4000 },
                DownloadParallelTasks = 1
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

        // When
        var result = await speedtest.GetDownloadSpeedAsync(server, downloadSizeMb);

        // Then
        result.ShouldNotBeNull();
        result.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(0);

        // HACK: Actual bytes should be very close to the intended download size.
        // Incomplete tasks makes this difficult to test so use the following workaround:
        result.BytesProcessed.ShouldBeLessThanOrEqualTo((long)(2 * downloadSizeMb * 1024 * 1024));
    }

    [Fact]
    public async Task GetDownloadSpeedAsync_ShouldReportProgress_RespectDownloadSize()
    {
        // Given
        const int downloadSizeMb = 2;

        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(request =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[512 * 1024])
            };

            return response;
        });

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            DownloadTest = new()
            {
                DownloadSizes = new[] { 100 },
                DownloadSizeIterations = 10,
                DownloadParallelTasks = 1
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };
        var progressReports = new List<int>();

        // When
        var result = await speedtest.GetDownloadSpeedAsync(server, downloadSizeMb, progress => progressReports.Add(progress.PercentageComplete));

        // Then
        result.ShouldNotBeNull();
        result.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(0);
        result.BytesProcessed.ShouldBe(4 * 512 * 1024);
        progressReports.ShouldNotBeNull();
        progressReports.ShouldBe(new[] { 25, 50, 75, 100 });
    }

    [Fact]
    public async Task GetDownloadSpeedAsync_ShouldCancel_WhenTokenIsCancelled()
    {
        // Given
        using var mockHttp = new MockHttpMessageHandler();
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
        var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

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
        using var mockHttp = new MockHttpMessageHandler();
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
        var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };
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
        using var mockHttp = new MockHttpMessageHandler();

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
        var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };
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

    [Fact]
    public async Task GetDownloadSpeedAsync_ShouldCompleteWithZeroBytes_WhenAllDownloadsFail()
    {
        // Given
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Throw(new HttpRequestException("Network failure"));

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            DownloadTest = new()
            {
                DownloadSizes = new[] { 100 },
                DownloadSizeIterations = 5,
                DownloadParallelTasks = 1
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

        // When
        var result = await speedtest.GetDownloadSpeedAsync(server);

        // Then
        result.ShouldNotBeNull();
        result.BytesProcessed.ShouldBe(0); // All downloads failed
        result.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task GetDownloadSpeedAsync_ShouldTreatErrorResponsesAsFailures(HttpStatusCode errorCode)
    {
        // Given
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(errorCode);

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            DownloadTest = new()
            {
                DownloadSizes = new[] { 100 },
                DownloadSizeIterations = 3,
                DownloadParallelTasks = 1
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

        // When
        var result = await speedtest.GetDownloadSpeedAsync(server);

        // Then
        result.ShouldNotBeNull();
        result.BytesProcessed.ShouldBe(0); // All downloads returned error status
        result.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetDownloadSpeedAsync_ShouldCompleteSuccessfully_WhenServerReturnsZeroBytes()
    {
        // Given
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(request =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Array.Empty<byte>())
            };
            return response;
        });

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            DownloadTest = new()
            {
                DownloadSizes = new[] { 100 },
                DownloadSizeIterations = 3,
                DownloadParallelTasks = 1
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

        // When
        var result = await speedtest.GetDownloadSpeedAsync(server);

        // Then
        result.ShouldNotBeNull();
        result.BytesProcessed.ShouldBe(0); // Server returned empty responses
        result.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetDownloadSpeedAsync_ShouldPropagateException_WhenProgressCallbackThrows()
    {

        // Given
        using var mockHttp = new MockHttpMessageHandler();
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
        var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetDownloadSpeedAsync(server, progress =>
        {
            throw new InvalidOperationException("Progress callback failed");
        }));

        // Then
        exception.ShouldNotBeNull();
        exception.ShouldBeOfType<InvalidOperationException>();
        exception.Message.ShouldBe("Progress callback failed");
    }

    #endregion

    #region --- GetUploadSpeedAsync ---

    [Fact]
    public async Task GetUploadSpeedAsync_ShouldReturnSpeedTestResult_WhenSuccessful()
    {
        // Given
        long actualBytes = 0;

        using var mockHttp = new MockHttpMessageHandler();
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
        var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

        // When
        var result = await speedtest.GetUploadSpeedAsync(server);

        // Then
        result.ShouldNotBeNull();
        result.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(0);
        result.BytesProcessed.ShouldBe(actualBytes);
    }

    [Theory]
    [InlineData(1)]   // 1MB
    [InlineData(5)]   // 5MB
    [InlineData(10)]  // 10MB
    [InlineData(20)]  // 20MB
    [InlineData(40)]  // 40MB
    [InlineData(100)] // 100MB
    public async Task GetUploadSpeedAsync_ShouldRespectUploadSize(int uploadSizeMb)
    {
        // Given
        long actualBytes = 0;

        using var mockHttp = new MockHttpMessageHandler();
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
        var settings = new OoklaSpeedtestSettings
        {
            UploadTest = new()
            {
                UploadParallelTasks = 1
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

        // When
        var result = await speedtest.GetUploadSpeedAsync(server, uploadSizeMb);

        // Then
        result.ShouldNotBeNull();
        result.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(0);

        // HACK: Actual bytes should be very close to the intended upload size.
        // Incomplete tasks makes this difficult to test so use the following workaround:
        actualBytes.ShouldBeLessThanOrEqualTo((long)(2 * uploadSizeMb * 1024 * 1024));
    }

    [Fact]
    public async Task GetUploadSpeedAsync_ShouldReportProgress_RespectUploadSize()
    {
        // Given
        const int uploadSizeMb = 2;

        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            UploadTest = new()
            {
                UploadIncrements = 1,
                UploadSizeIncrementKb = 512,
                UploadSizeIterations = 10,
                UploadParallelTasks = 1
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };
        var progressReports = new List<int>();

        // When
        var result = await speedtest.GetUploadSpeedAsync(server, uploadSizeMb, progress => progressReports.Add(progress.PercentageComplete));

        // Then
        result.ShouldNotBeNull();
        result.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(0);
        result.BytesProcessed.ShouldBe(4 * 512 * 1024);
        progressReports.ShouldNotBeNull();
        progressReports.ShouldBe(new[] { 25, 50, 75, 100 });
    }

    [Fact]
    public async Task GetUploadSpeedAsync_ShouldCancel_WhenTokenIsCancelled()
    {
        // Given
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(async _ =>
        {
            // Simulate slow response.
            await Task.Delay(1000);

            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var httpClient = mockHttp.ToHttpClient();
        var speedtest = new OoklaSpeedtest(httpClientOverride: httpClient);
        var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

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
        using var mockHttp = new MockHttpMessageHandler();
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
        var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };
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
        using var mockHttp = new MockHttpMessageHandler();

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
        var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };
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

    [Fact]
    public async Task GetUploadSpeedAsync_ShouldCompleteWithZeroBytes_WhenAllUploadsFail()
    {
        // Given
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Throw(new HttpRequestException("Network failure"));

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            UploadTest = new()
            {
                UploadIncrements = 1,
                UploadSizeIterations = 5,
                UploadParallelTasks = 1
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

        // When
        var result = await speedtest.GetUploadSpeedAsync(server);

        // Then
        result.ShouldNotBeNull();
        result.BytesProcessed.ShouldBe(0); // All uploads failed
        result.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task GetUploadSpeedAsync_ShouldCompleteSuccessfully_EvenWithErrorResponses(HttpStatusCode errorCode)
    {
        // Given
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(errorCode);

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

        // Then
        result.ShouldNotBeNull();
        // Unlike downloads, uploads don't check response status codes
        // Bytes are counted as uploaded even if server returns error status
        result.BytesProcessed.ShouldBeGreaterThan(0);
        result.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetUploadSpeedAsync_ShouldPropagateException_WhenProgressCallbackThrows()
    {

        // Given
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(HttpStatusCode.OK);

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings
        {
            UploadTest = new()
            {
                UploadIncrements = 1,
                UploadSizeIterations = 4,
                UploadParallelTasks = 1
            }
        };

        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetUploadSpeedAsync(server, progress =>
        {
            throw new InvalidOperationException("Progress callback failed");
        }));

        // Then
        exception.ShouldNotBeNull();
        exception.ShouldBeOfType<InvalidOperationException>();
        exception.Message.ShouldBe("Progress callback failed");
    }

    #endregion
}