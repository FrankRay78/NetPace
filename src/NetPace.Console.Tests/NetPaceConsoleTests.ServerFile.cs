namespace NetPace.Console.Tests;

/// <summary>
/// <c>--server-file</c> names a file listing the servers a run chooses from, in place of Ookla's
/// nearby list: the choice, the name an entry carries, and the failures that stop a run early.
/// </summary>
public sealed partial class NetPaceConsoleTests
{
    public sealed class ServerFile
    {
        /// <summary>
        /// A temporary path nothing has been written to, fresh on every call.
        /// </summary>
        private static string UnusedTempPath() =>
            Path.Combine(Path.GetTempPath(), $"netpace-servers-{Guid.NewGuid():N}.json");

        /// <summary>
        /// Screening that picks the lowest latency <paramref name="latencyFor"/> gives a candidate.
        /// A candidate it gives <c>null</c> for cannot be reached, so it drops out of selection.
        /// </summary>
        private static Func<IServer[], IProgress<SpeedTestProgress>?, CancellationToken, Task<LatencyTestResult>> ScreeningThatRanks(Func<string, int?> latencyFor) =>
            (servers, _, _) =>
            {
                var fastest = servers
                    .Select(candidate => (Server: candidate, Latency: latencyFor(candidate.Url)))
                    .Where(entry => entry.Latency is not null)
                    .MinBy(entry => entry.Latency);

                if (fastest.Server is null)
                {
                    throw new NoServersAvailableException();
                }

                return Task.FromResult(new LatencyTestResult
                {
                    Server = fastest.Server,
                    LatencyMilliseconds = fastest.Latency!.Value,
                    MinimumLatencyMilliseconds = null,
                    MaximumLatencyMilliseconds = null,
                    JitterMilliseconds = 0
                });
            };

        /// <summary>
        /// A speed tester that measures whatever it is handed, recording every candidate list
        /// screening was given and every time discovery was asked. Screening ranks by latency.
        /// </summary>
        private static (SpeedTestMock mock, List<IServer[]> screened, List<string> discoveries) TesterRanking(Func<string, int?> latencyFor)
        {
            var screened = new List<IServer[]>();
            var discoveries = new List<string>();
            var ranks = ScreeningThatRanks(latencyFor);

            var mock = new SpeedTestMock
            {
                GetServersAsyncFunc = _ =>
                {
                    discoveries.Add(nameof(ISpeedTestService.GetServersAsync));
                    return Task.FromResult(discoveredServers);
                },
                GetFastestServerByLatencyAsyncFunc = (servers, progress, cancellationToken) =>
                {
                    lock (screened)
                    {
                        screened.Add(servers);
                    }

                    return ranks(servers, progress, cancellationToken);
                },
                GetServerLatencyAsyncFunc = (server, _, _) =>
                    Task.FromResult(new LatencyTestResult { Server = server, LatencyMilliseconds = 42, MinimumLatencyMilliseconds = null, MaximumLatencyMilliseconds = null, JitterMilliseconds = 4 }),
                GetServerLatencyByServerUrlAsyncFunc = (url, _, _) =>
                    Task.FromResult(new LatencyTestResult { Server = new Server { Sponsor = Server.UnknownSponsor, Url = url }, LatencyMilliseconds = 42, MinimumLatencyMilliseconds = null, MaximumLatencyMilliseconds = null, JitterMilliseconds = 4 }),
                GetDownloadSpeedAsyncFunc = (_, _, _) => Task.FromResult(ScriptedSpeedTester.Clean(150)),
                GetUploadSpeedAsyncFunc = (_, _, _) => Task.FromResult(ScriptedSpeedTester.Clean(32)),
            };

            return (mock, screened, discoveries);
        }

        private static CommandLineTestHost HostOf(ISpeedTestService speedTester)
        {
            var services = new ServiceCollection();
            services.AddSingleton(speedTester);
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            return GetCommandLineTestHost(services);
        }

        /// <summary>
        /// A host whose speed tester records every call a run could make before it has a result, so
        /// a test can assert the run never got that far.
        /// </summary>
        private static (CommandLineTestHost host, List<string> serviceCalls) HostRecordingEverything()
        {
            var serviceCalls = new List<string>();

            var speedTester = new SpeedTestMock
            {
                GetServersAsyncFunc = _ =>
                {
                    serviceCalls.Add(nameof(ISpeedTestService.GetServersAsync));
                    return Task.FromResult(discoveredServers);
                },
                GetFastestServerByLatencyAsyncFunc = (_, _, _) =>
                {
                    serviceCalls.Add(nameof(ISpeedTestService.GetFastestServerByLatencyAsync));
                    throw new NotImplementedException(nameof(ISpeedTestService.GetFastestServerByLatencyAsync));
                },
                GetServerLatencyAsyncFunc = (_, _, _) =>
                {
                    serviceCalls.Add(nameof(ISpeedTestService.GetServerLatencyAsync));
                    throw new NotImplementedException(nameof(ISpeedTestService.GetServerLatencyAsync));
                },
                GetServerLatencyByServerUrlAsyncFunc = (_, _, _) =>
                {
                    serviceCalls.Add(nameof(ISpeedTestService.GetServerLatencyAsync));
                    throw new NotImplementedException(nameof(ISpeedTestService.GetServerLatencyAsync));
                },
            };

            return (HostOf(speedTester), serviceCalls);
        }

        [Fact]
        public async Task A_ServerFile_IsChosenFromInsteadOfAskingDiscovery()
        {
            // SCENARIO: A fixed list is used instead of discovery

            // Given a file naming three reachable servers, none of them in the list discovery
            // would have offered.
            using var file = new TempServerFile(
                """
                [
                  { "url": "http://listed1.example/" },
                  { "url": "http://listed2.example/" },
                  { "url": "http://listed3.example/" }
                ]
                """);

            var (mock, screened, discoveries) = TesterRanking(url => url.Contains("listed2", StringComparison.Ordinal) ? 10 : 90);

            // When
            var result = await HostOf(mock).RunAsync(["--csv", "--server-file", file.Path]);

            // Then the three listed servers are the candidates, discovery was never asked, and the
            // result is one of them.
            Assert.Equal(0, result.ExitCode);
            Assert.Empty(discoveries);
            Assert.Equal(
                ["http://listed1.example/", "http://listed2.example/", "http://listed3.example/"],
                Assert.Single(screened).Select(server => server.Url).Order());
            Assert.Contains("http://listed2.example/", result.Output, StringComparison.Ordinal);
        }

        [Fact]
        public async Task TheFastestReachableEntry_IsTheOneTested()
        {
            // SCENARIO: The fastest entry in the file is chosen

            // Given a file naming a slow and a fast reachable server.
            using var file = new TempServerFile(
                """
                [{ "url": "http://slow.example/" }, { "url": "http://fast.example/" }]
                """);

            var (mock, _, _) = TesterRanking(url => url.Contains("fast", StringComparison.Ordinal) ? 9 : 120);

            // When
            var result = await HostOf(mock).RunAsync(["--csv", "--server-file", file.Path]);

            // Then the fast one is what was tested.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("http://fast.example/", result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("http://slow.example/", result.Output, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("--csv")]
        [InlineData("--json")]
        public async Task AnEntrysName_IsShownWhereverTheSponsorIs(string outputFormat)
        {
            // SCENARIO: An entry's name is shown as its sponsor

            // Given a file whose only entry is a self-hosted LibreSpeed server the user has named.
            using var file = new TempServerFile(
                """
                [{ "url": "http://pi.local:8080/", "name": "Home Pi" }]
                """);

            var (mock, _, _) = TesterRanking(_ => 11);

            // When the run tests it as a LibreSpeed server.
            var result = await HostOf(mock).RunAsync(Invocation(outputFormat, "--provider", "librespeed", "--server-file", file.Path));

            // Then the name the user chose is what the output calls the server, in place of the
            // placeholder a server named by URL alone carries.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Home Pi", result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain(Server.UnknownSponsor, result.Output, StringComparison.Ordinal);
        }

        [Fact]
        public async Task AnEntrysName_IsShownOnTheScreenAPersonWatches()
        {
            // Given the same named entry, and a run at a terminal, which is the only destination
            // default output names the server on at all.
            using var file = new TempServerFile(
                """
                [{ "url": "http://pi.local:8080/", "name": "Home Pi" }]
                """);

            var (mock, _, _) = TesterRanking(_ => 11);

            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(mock);
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();

            // When
            var result = await GetInteractiveCommandLineTestHost(services).RunAsync(["--server-file", file.Path]);

            // Then
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Home Pi", result.Output, StringComparison.Ordinal);
        }

        [Fact]
        public async Task AnEntryWithNoName_ShowsTheSamePlaceholderAsAServerNamedByUrl()
        {
            // Given one file entry carrying no name, and the same server named with --server-url.
            using var file = new TempServerFile(
                """
                [{ "url": "http://pi.local:8080/" }]
                """);

            var (listed, _, _) = TesterRanking(_ => 11);
            var (named, _, _) = TesterRanking(_ => 11);

            // When each run is asked for.
            var fromFile = await HostOf(listed).RunAsync(["--csv", "--server-file", file.Path]);
            var fromUrl = await HostOf(named).RunAsync(["--csv", "--server-url", "http://pi.local:8080/"]);

            // Then the unnamed entry reads as the named server does: whatever the placeholder is,
            // the two agree on it.
            Assert.Equal(0, fromFile.ExitCode);
            Assert.Equal(fromUrl.Output, fromFile.Output);
        }

        [Fact]
        public async Task AnEntryWithNoUrl_FailsTheRunBeforeAnythingIsContacted()
        {
            // SCENARIO: An invalid entry fails the run before testing

            // Given a file whose second of three entries has no url field.
            using var file = new TempServerFile(
                """
                [
                  { "url": "http://listed1.example/" },
                  { "name": "Nameless" },
                  { "url": "http://listed3.example/" }
                ]
                """);

            var (host, serviceCalls) = HostRecordingEverything();

            // When
            var result = await host.RunAsync(["--csv", "--server-file", file.Path]);

            // Then nothing was contacted, the run exits non-zero, and the message names the file
            // and points at the entry that is wrong.
            Assert.Empty(serviceCalls);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Empty(result.Output);
            Assert.Contains(file.Path, result.ErrorOutput, StringComparison.Ordinal);
            Assert.Contains("entry 2", result.ErrorOutput, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AnEntryWithAnUnexpectedField_FailsTheRunNamingThatField()
        {
            // Given a file whose entry carries a field that is neither url nor name.
            using var file = new TempServerFile(
                """
                [{ "url": "http://listed1.example/", "sponsor": "Someone" }]
                """);

            var (host, serviceCalls) = HostRecordingEverything();

            // When
            var result = await host.RunAsync(["--csv", "--server-file", file.Path]);

            // Then the run fails before contacting anything, and the field the user wrote is what
            // the message names, so they can find it in their own file.
            Assert.Empty(serviceCalls);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(file.Path, result.ErrorOutput, StringComparison.Ordinal);
            Assert.Contains("sponsor", result.ErrorOutput, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("", "is empty")]
        [InlineData("   \n", "is empty")]
        [InlineData("[]", "lists no servers")]
        [InlineData("{ \"url\": \"http://listed1.example/\" }", "must hold a JSON array")]
        [InlineData("[{ \"url\": \"http://listed1.example/\" },", "is not valid JSON")]
        [InlineData("not json at all", "is not valid JSON")]
        [InlineData("[\"http://listed1.example/\"]", "is not a server")]
        [InlineData("[{ \"url\": 8080 }]", "something other than text")]
        [InlineData("[{ \"url\": \"http://a.example/\", \"url\": \"http://b.example/\" }]", "more than once")]
        [InlineData("[{ \"url\": \"http://a.example/\", \"name\": 5 }]", "something other than text")]
        [InlineData("[{ \"url\": \"http://a.example/\", \"name\": null }]", "something other than text")]
        [InlineData("[{ \"url\": \"http://a.example/\", \"name\": \"A\", \"name\": \"B\" }]", "more than once")]
        public async Task AMalformedServerFile_FailsTheRunBeforeAnythingIsContacted(string json, string cause)
        {
            // Given a file that is not a list of server entries: empty, not JSON, not a list, or a
            // list of something other than an entry.
            using var file = new TempServerFile(json);

            var (host, serviceCalls) = HostRecordingEverything();

            // When
            var result = await host.RunAsync(["--csv", "--server-file", file.Path]);

            // Then nothing was contacted, the run exits non-zero, and the message names the file so
            // the user knows which one to go and look at.
            Assert.Empty(serviceCalls);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Empty(result.Output);
            Assert.Contains(file.Path, result.ErrorOutput, StringComparison.Ordinal);
            Assert.Contains(cause, result.ErrorOutput, StringComparison.Ordinal);
        }

        [Fact]
        public async Task AnUnreadableServerFile_FailsTheRunBeforeAnythingIsContacted()
        {
            // Given a path naming a directory, which is readable but is not a file to read from.
            var path = UnusedTempPath();
            System.IO.Directory.CreateDirectory(path);

            var (host, serviceCalls) = HostRecordingEverything();

            try
            {
                // When
                var result = await host.RunAsync(["--csv", "--server-file", path]);

                // Then
                Assert.Empty(serviceCalls);
                Assert.NotEqual(0, result.ExitCode);
                Assert.Empty(result.Output);
                Assert.Contains(path, result.ErrorOutput, StringComparison.Ordinal);
                Assert.Contains("could not be read", result.ErrorOutput, StringComparison.Ordinal);
            }
            finally
            {
                System.IO.Directory.Delete(path);
            }
        }

        [Fact]
        public async Task AnEntryWhoseValuesAreBlank_ReachesSelectionRatherThanBeingRefused()
        {
            // Given a file whose first entry gives a blank url and name, and whose second names a
            // reachable server.
            using var file = new TempServerFile(
                """
                [{ "url": "", "name": "" }, { "url": "http://reachable.example/" }]
                """);

            var (mock, screened, _) = TesterRanking(url => url.Length == 0 ? null : 12);

            // When
            var result = await HostOf(mock).RunAsync(["--csv", "--server-file", file.Path]);

            // Then the blank entry was carried to screening, where it lost as an unreachable server
            // does, and the run measured the entry that answered.
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(
                ["", "http://reachable.example/"],
                Assert.Single(screened).Select(server => server.Url).Order());
            Assert.Contains("http://reachable.example/", result.Output, StringComparison.Ordinal);
        }

        [Fact]
        public async Task AMissingServerFile_FailsTheRunBeforeAnythingIsContacted()
        {
            // Given a path no file was written to.
            var path = UnusedTempPath();
            var (host, serviceCalls) = HostRecordingEverything();

            // When
            var result = await host.RunAsync(["--csv", "--server-file", path]);

            // Then
            Assert.Empty(serviceCalls);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Empty(result.Output);
            Assert.Contains(path, result.ErrorOutput, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ABlankServerFile_IsRefused(string blank)
        {
            // Given --server-file given a value that names no path.
            var (host, serviceCalls) = HostRecordingEverything();

            // When
            var result = await host.RunAsync(["--csv", "--server-file", blank]);

            // Then it is refused naming the option, rather than quietly falling back to discovery:
            // the user asked for a file and would get an answer about another set of servers.
            Assert.Empty(serviceCalls);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("--server-file", result.ErrorOutput, StringComparison.Ordinal);
        }

        [Fact]
        public async Task AnUnreachableEntry_LosesToAReachableOne()
        {
            // SCENARIO: Unreachable entries lose, as today

            // Given a file whose first entry cannot be reached and whose second can.
            using var file = new TempServerFile(
                """
                [{ "url": "http://unreachable.example/" }, { "url": "http://reachable.example/" }]
                """);

            var (mock, _, _) = TesterRanking(url => url.Contains("reachable.example", StringComparison.Ordinal) && !url.Contains("unreachable", StringComparison.Ordinal) ? 30 : null);

            // When
            var result = await HostOf(mock).RunAsync(["--csv", "--server-file", file.Path]);

            // Then the reachable entry is tested and the run succeeds.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("http://reachable.example/", result.Output, StringComparison.Ordinal);
        }

        [Fact]
        public async Task NoReachableEntry_FailsAsNoDiscoveredServerDoes()
        {
            // Given a file none of whose entries can be reached, and a discovery run in which
            // none of the discovered servers can be reached either.
            using var file = new TempServerFile(
                """
                [{ "url": "http://unreachable1.example/" }, { "url": "http://unreachable2.example/" }]
                """);

            var (listed, _, _) = TesterRanking(_ => null);
            var (discovered, _, _) = TesterRanking(_ => null);

            // When each run is asked for.
            var fromFile = await HostOf(listed).RunAsync(["--csv", "--server-file", file.Path]);
            var fromDiscovery = await HostOf(discovered).RunAsync(["--csv"]);

            // Then the user is told the same thing either way: a curated list that nothing answers
            // on is the same dead end as a discovered one.
            Assert.Equal(fromDiscovery.ExitCode, fromFile.ExitCode);
            Assert.Equal(fromDiscovery.ErrorOutput, fromFile.ErrorOutput);
        }

        [Fact]
        public async Task EveryIterationOfARepeatedRun_ChoosesFromTheSameEntries()
        {
            // Given a file naming two servers, and three tests asked for.
            using var file = new TempServerFile(
                """
                [{ "url": "http://listed1.example/" }, { "url": "http://listed2.example/" }]
                """);

            var (mock, screened, discoveries) = TesterRanking(url => url.Contains("listed2", StringComparison.Ordinal) ? 10 : 90);

            // When
            var result = await HostOf(mock).RunAsync(["--csv", "--count", "3", "--server-file", file.Path]);

            // Then every test chose from the file's own entries, and none of them asked discovery.
            Assert.Equal(0, result.ExitCode);
            Assert.Empty(discoveries);
            Assert.Equal(3, screened.Count);
            Assert.All(screened, candidates => Assert.Equal(
                ["http://listed1.example/", "http://listed2.example/"],
                candidates.Select(server => server.Url).Order()));
        }

        [Fact]
        public async Task WithLatencySkipped_TheFirstEntryListedIsUsed()
        {
            // Given a file whose first entry is not the fastest, and a run with no latency test to
            // rank the entries by.
            using var file = new TempServerFile(
                """
                [{ "url": "http://listed1.example/" }, { "url": "http://listed2.example/" }]
                """);

            var (mock, screened, discoveries) = TesterRanking(url => url.Contains("listed2", StringComparison.Ordinal) ? 10 : 90);

            // When
            var result = await HostOf(mock).RunAsync(["--csv", "--no-latency", "--server-file", file.Path]);

            // Then the first server listed is the one tested, nothing was screened to get there,
            // and discovery was not asked.
            Assert.Equal(0, result.ExitCode);
            Assert.Empty(discoveries);
            Assert.Empty(screened);
            Assert.Contains("http://listed1.example/", result.Output, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("http://named.example/")]
        [InlineData("   ")]
        public async Task AServerFileAndAServerUrlTogether_AreRefused(string serverUrl)
        {
            // Given a valid file and a server named outright, two answers to the same question,
            // even where the one named is blank and so names no server the run could test.
            using var file = new TempServerFile(
                """
                [{ "url": "http://listed1.example/" }]
                """);

            var (host, serviceCalls) = HostRecordingEverything();

            // When
            var result = await host.RunAsync(["--csv", "--server-file", file.Path, "--server-url", serverUrl]);

            // Then the run is refused before contacting anything, naming both options so the user
            // knows which pair is in conflict.
            Assert.Empty(serviceCalls);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("--server-file", result.ErrorOutput, StringComparison.Ordinal);
            Assert.Contains("--server-url", result.ErrorOutput, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ALibreSpeedRun_IsSatisfiedByAServerFileAlone()
        {
            // Given a LibreSpeed run whose servers come from a file rather than from --server-url,
            // which is the only way it could name one before.
            using var file = new TempServerFile(
                """
                [{ "url": "http://pi.local:8080/", "name": "Home Pi" }]
                """);

            var (mock, _, _) = TesterRanking(_ => 11);

            // When
            var result = await HostOf(mock).RunAsync(["--csv", "--provider", "librespeed", "--server-file", file.Path]);

            // Then the run goes ahead rather than being refused for having no server named.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("http://pi.local:8080/", result.Output, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Help_AdvertisesTheServerFileOption()
        {
            // Given
            var (host, _) = HostRecordingEverything();

            // When
            var result = await host.RunAsync(["--help"]);

            // Then the option a user has to discover from --help is in it.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("--server-file", result.Output, StringComparison.Ordinal);
        }

        /// <summary>
        /// A server file written as given rather than composed, so a test can hand the reader
        /// whatever it wants to, and deleted when the test finishes.
        /// </summary>
        private sealed class TempServerFile : IDisposable
        {
            public TempServerFile(string json)
            {
                Path = UnusedTempPath();
                System.IO.File.WriteAllText(Path, json);
            }

            public string Path { get; }

            public void Dispose()
            {
                if (System.IO.File.Exists(Path))
                {
                    System.IO.File.Delete(Path);
                }
            }
        }
    }
}
