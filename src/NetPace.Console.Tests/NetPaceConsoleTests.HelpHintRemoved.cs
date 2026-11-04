namespace NetPace.Console.Tests;

/// <summary>
/// The default output ends on the result of a completed run. The trailing
/// <c>Try 'netpace --help' for more information.</c> hint is gone: a run that asked for a speed test
/// and got one does not need telling where help lives, and the hint landed under every result on
/// screen and in every default-output <c>--file</c> log.
/// </summary>
public sealed partial class NetPaceConsoleTests
{
    public sealed class HelpHintRemoved
    {
        [Fact]
        public async Task CompletedRun_InDefaultOutput_EndsOnItsResultLine()
        {
            // SCENARIO: A completed run ends on its result

            // Given
            var host = GetCommandLineTestHost(StubServices());

            // When
            var result = await host.RunAsync([]);

            // Then no hint followed the result; the committed snapshots pin the output's shape.
            Assert.Equal(0, result.ExitCode);
            Assert.DoesNotContain("Try 'netpace --help'", result.Output);
        }

        [Fact]
        public async Task EveryRunUnderCount_EndsOnItsResultLine()
        {
            // Given
            var host = GetCommandLineTestHost(StubServices());

            // When
            var result = await host.RunAsync(["--count", "2"]);

            // Then both runs printed a result, and neither was followed by a hint. No snapshot
            // covers non-interactive multi-run default output, so the run count is asserted here.
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(2, ResultLineCount(result.Output));
            Assert.DoesNotContain("Try 'netpace --help'", result.Output);
        }

        [Fact]
        public async Task EveryRunUnderLoop_EndsOnItsResultLine()
        {
            // Given a loop that cancels itself after two runs.
            using var cancellationTokenSource = new CancellationTokenSource();

            var services = StubServices();
            services.AddSingleton<IWaiter>(new SelfCancellingWaiter(2, cancellationTokenSource));
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--loop"], cancellationTokenSource.Token);

            // Then every run printed a result, and none was followed by a hint.
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(2, ResultLineCount(result.Output));
            Assert.DoesNotContain("Try 'netpace --help'", result.Output);
        }

        [Fact]
        public async Task DefaultOutputWrittenToFile_CarriesNoHint()
        {
            // SCENARIO: Default output written to a file carries no hint

            // Given
            var testFile = Path.Join(Path.GetTempPath(), $"netpace-test-{Guid.NewGuid()}.txt");

            try
            {
                var host = GetCommandLineTestHost(StubServices());

                // When
                var result = await host.RunAsync(["--file", testFile]);

                // Then the log holds no hint.
                Assert.Equal(0, result.ExitCode);

                var fileContent = await System.IO.File.ReadAllTextAsync(testFile);
                Assert.DoesNotContain("Try 'netpace --help'", fileContent);
            }
            finally
            {
                if (System.IO.File.Exists(testFile))
                {
                    System.IO.File.Delete(testFile);
                }
            }
        }

        [Fact]
        public async Task RunWhoseRequestsAllFailed_EndsOnItsAnnotatedResultLine()
        {
            // SCENARIO: A run with failed requests ends on its annotated result

            // Given a server is found, latency measures, the download succeeds, and every upload
            // request fails.
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(new ScriptedSpeedTester { UploadFactory = _ => ScriptedSpeedTester.AllFailed(32) });
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([]);

            // Then no hint followed the annotated result; the Failures snapshot pins the
            // annotation itself.
            Assert.Equal(0, result.ExitCode);
            Assert.DoesNotContain("Try 'netpace --help'", result.Output);
        }

        /// <summary>
        /// The registrations a default-output test needs: a stub speed tester, a fixed clock, and a
        /// waiter that returns immediately. A test that needs more adds it to the returned
        /// collection.
        /// </summary>
        private static ServiceCollection StubServices()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            return services;
        }

        /// <summary>
        /// How many runs printed a result, counted by the latency-led result line of default
        /// output. Assumes latency on and no timestamp, which is what these tests run.
        /// </summary>
        private static int ResultLineCount(string output) =>
            output.Split('\n').Count(line => line.Trim().StartsWith("Latency:", StringComparison.Ordinal));
    }
}
