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

        /// <summary>
        /// The screening latency a candidate answers with, or <c>null</c> when it cannot be
        /// screened at all - which is how a candidate drops out of selection.
        /// </summary>
        private static int? ScreeningLatencyFor(IServer server) => server.Sponsor switch
        {
            "Test Sponsor 1" => 10,
            "Test Sponsor 3" => 30,
            _ => null,
        };

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
        public async Task Should_Report_The_Failure_When_The_Chosen_Server_Cannot_Be_Measured()
        {
            // Given screening chooses a server successfully, and measuring that server then fails.
            // Splitting one call into two created this window, and the run is meant to fail in it
            // rather than quietly fall back to the next-ranked candidate.
            var mock = new SpeedTestMock
            {
                GetServersAsyncFunc = _ => Task.FromResult(discoveredServers),
                GetFastestServerByLatencyAsyncFunc = (servers, _, _) =>
                    Task.FromResult(new LatencyTestResult { Server = servers[0], LatencyMilliseconds = 5 }),
                GetServerLatencyAsyncFunc = (_, _, _) =>
                    throw new Exception("Server returned incorrect test string for latency.txt"),
            };

            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(mock);
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([]);

            // Then the measurement failure is what the user is told about, and no download or
            // upload figure is reported for a server whose latency could not be measured.
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
                    // Honour the whole array rather than assuming one candidate per call: rank the
                    // candidates that can be screened and report "no servers" only when none can.
                    // That is the documented contract, so this stands in for screening however the
                    // command chooses to batch its candidates.
                    var ranked = servers
                        .Select(candidate => new { candidate, latency = ScreeningLatencyFor(candidate) })
                        .Where(entry => entry.latency is not null)
                        .OrderBy(entry => entry.latency)
                        .ToArray();

                    if (ranked.Length == 0)
                    {
                        throw new Exception("No servers available");
                    }

                    return Task.FromResult(new LatencyTestResult { Server = ranked[0].candidate, LatencyMilliseconds = ranked[0].latency!.Value });
                },
                GetServerLatencyAsyncFunc = (server, _, _) =>
                    Task.FromResult(new LatencyTestResult { Server = server, LatencyMilliseconds = 999 }),
            };

            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(mock);
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["servers", "-l"]);

            // Then every server a run could use carries its screening latency, and the one that
            // could not be reached carries none.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Report_A_Misconfigured_Provider_Rather_Than_Listing_Every_Server_As_Unreachable()
        {
            // Given a provider whose settings make screening impossible for any server.
            var mock = new SpeedTestMock
            {
                GetServersAsyncFunc = _ => Task.FromResult(discoveredServers),
                GetFastestServerByLatencyAsyncFunc = (_, _, _) =>
                    throw new ArgumentOutOfRangeException("ScreeningRequestCount"),
            };

            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(mock);
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["servers", "-l"]);

            // Then the user is told about the misconfiguration, instead of reading a table that
            // says no server could be reached.
            Assert.NotEqual(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_List_Latencies_Without_Waiting_On_One_Server_Before_Screening_The_Next()
        {
            // Given every server answers its screening only once all of them have been asked, so
            // a listing that waits on one server before asking the next leaves every server but
            // the last unanswered, and those rows give up instead.
            var screeningsAsked = 0;
            var allServersAsked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var mock = new SpeedTestMock
            {
                GetServersAsyncFunc = _ => Task.FromResult(discoveredServers),
                GetFastestServerByLatencyAsyncFunc = async (servers, _, cancellationToken) =>
                {
                    if (Interlocked.Increment(ref screeningsAsked) == discoveredServers.Length)
                    {
                        allServersAsked.SetResult();
                    }

                    await allServersAsked.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

                    return new LatencyTestResult { Server = servers[0], LatencyMilliseconds = Array.IndexOf(discoveredServers, servers[0]) + 71 };
                },
            };

            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(mock);
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["servers", "-l"]);

            // Then every server is listed with its latency. These are targeted assertions rather
            // than a snapshot because the rows fill in as the servers answer, and that order is
            // not part of what the listing promises.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("71ms", result.Output);
            Assert.Contains("72ms", result.Output);
            Assert.Contains("73ms", result.Output);
        }
    }
}
