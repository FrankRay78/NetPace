using Microsoft.Extensions.DependencyInjection;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    /// <summary>
    /// Screening chooses the server; a full measurement reports its latency. These cover what the
    /// user sees of that split: the figure a run reports, and the figure <c>servers -l</c> lists.
    /// </summary>
    public sealed class ServerSelection
    {
        private static readonly IServer[] discoveredServers =
        [
            new Server { Location = "Location 1", Sponsor = "Test Sponsor 1", Url = "http://test1.com" },
            new Server { Location = "Location 2", Sponsor = "Test Sponsor 2", Url = "http://test2.com" },
            new Server { Location = "Location 3", Sponsor = "Test Sponsor 3", Url = "http://test3.com" },
        ];

        [Fact]
        public async Task Should_Report_The_Measured_Latency_Of_The_Chosen_Server()
        {
            // SCENARIO: A run auto-selects a server

            // Given screening ranks the candidates roughly and the full measurement of the winner
            // reports a different figure, so the two are distinguishable in the output.
            var mock = new SpeedTestMock
            {
                GetServersAsyncFunc = _ => Task.FromResult<IServer[]>([discoveredServers[0]]),
                GetFastestServerByLatencyAsyncFunc = (servers, _, _) =>
                    Task.FromResult(new LatencyTestResult { Server = servers[0], LatencyMilliseconds = 5 }),
                GetServerLatencyAsyncFunc = (server, _, _) =>
                    Task.FromResult(new LatencyTestResult { Server = server, LatencyMilliseconds = 42 }),
                GetDownloadSpeedAsyncFunc = (_, _, _) =>
                    Task.FromResult(new SpeedTestResult { BytesProcessed = 1000, ElapsedMilliseconds = 1000, RequestsSucceeded = 150, RequestsFailed = 0 }),
                GetUploadSpeedAsyncFunc = (_, _, _) =>
                    Task.FromResult(new SpeedTestResult { BytesProcessed = 7000, ElapsedMilliseconds = 3000, RequestsSucceeded = 32, RequestsFailed = 0 }),
            };

            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(mock);
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--csv"]);

            // Then the measured figure is the one reported, so the latency NetPace prints stays as
            // accurate as it was before screening existed.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_List_The_Screening_Latency_And_Omit_It_For_A_Server_Selection_Could_Not_Reach()
        {
            // SCENARIO: A user lists available servers

            // Given the second server cannot be screened, while a full measurement of any server
            // would succeed — so only the screening path can produce the listed figures.
            var mock = new SpeedTestMock
            {
                GetServersAsyncFunc = _ => Task.FromResult(discoveredServers),
                GetFastestServerByLatencyAsyncFunc = (servers, _, _) =>
                {
                    var candidate = servers[0];
                    if (candidate.Sponsor == "Test Sponsor 2")
                    {
                        throw new Exception("No servers available");
                    }

                    var latency = candidate.Sponsor == "Test Sponsor 1" ? 10 : 30;
                    return Task.FromResult(new LatencyTestResult { Server = candidate, LatencyMilliseconds = latency });
                },
                GetServerLatencyAsyncFunc = (server, _, _) =>
                    Task.FromResult(new LatencyTestResult { Server = server, LatencyMilliseconds = 999 }),
            };

            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(mock);
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["servers", "-l"]);

            // Then every server a run could use carries its screening latency, and the one that
            // could not be reached carries none.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }
    }
}
