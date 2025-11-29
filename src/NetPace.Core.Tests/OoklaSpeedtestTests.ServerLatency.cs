using NetPace.Core.Clients.Ookla;
using RichardSzalay.MockHttp;
using Shouldly;

namespace NetPace.Core.Tests;

public sealed partial class OoklaSpeedtestTests
{
    public sealed class ServerLatency
    {
        [Fact]
        public async Task GetServerLatencyAsync_WithProgress_ShouldReportPingTimesAndPercentage_ForThreePings()
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
                    LatencyTestIterations = 3
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
            result.Latency.ShouldBeGreaterThanOrEqualTo(0);

            // Should receive 3 progress reports (one per ping)
            progressReports.Count.ShouldBe(3);

            // First progress report: 1 ping, 33% complete
            progressReports[0].Pings.Count.ShouldBe(1);
            progressReports[0].PercentageComplete.ShouldBe(33);

            // Second progress report: 2 pings, 66% complete
            progressReports[1].Pings.Count.ShouldBe(2);
            progressReports[1].PercentageComplete.ShouldBe(66);

            // Third progress report: 3 pings, 100% complete
            progressReports[2].Pings.Count.ShouldBe(3);
            progressReports[2].PercentageComplete.ShouldBe(100);
        }
    }
}
