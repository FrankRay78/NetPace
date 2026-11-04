namespace NetPace.Console.Tests;

/// <summary>
/// The default output ends on the result of a completed run. The trailing
/// <c>Try 'netpace --help' for more information.</c> hint is gone: a run that asked for a speed test
/// and got one does not need telling where help lives, and the hint landed under every result on
/// screen and in every <c>--file</c> log.
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
            var host = GetCommandLineTestHost(DefaultServices());

            // When
            var result = await host.RunAsync([]);

            // Then the result is the last thing printed, with no hint after it.
            Assert.Equal(0, result.ExitCode);
            Assert.DoesNotContain("--help", result.Output);
            Assert.StartsWith("Latency:", LastLine(result.Output));
        }

        [Fact]
        public async Task EveryRunUnderCount_EndsOnItsResultLine()
        {
            // Given
            var host = GetCommandLineTestHost(DefaultServices());

            // When
            var result = await host.RunAsync(["--count", "2"]);

            // Then both runs printed a result, and neither was followed by a hint.
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(2, ResultLineCount(result.Output));
            Assert.DoesNotContain("--help", result.Output);
            Assert.StartsWith("Latency:", LastLine(result.Output));
        }

        [Fact]
        public async Task EveryRunUnderLoop_EndsOnItsResultLine()
        {
            // Given a loop that cancels itself after two runs.
            var cancellationTokenSource = new CancellationTokenSource();

            var services = DefaultServices();
            services.AddSingleton<IWaiter>(new SelfCancellingWaiter(2, cancellationTokenSource));
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--loop"], cancellationTokenSource.Token);

            // Then every run printed a result, and none was followed by a hint.
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(2, ResultLineCount(result.Output));
            Assert.DoesNotContain("--help", result.Output);
            Assert.StartsWith("Latency:", LastLine(result.Output));
        }

        [Fact]
        public async Task DefaultOutputWrittenToFile_CarriesNoHint()
        {
            // SCENARIO: Default output written to a file carries no hint

            // Given
            var testFile = Path.Join(Path.GetTempPath(), $"netpace-test-{Guid.NewGuid()}.txt");

            try
            {
                var host = GetCommandLineTestHost(DefaultServices());

                // When
                var result = await host.RunAsync(["--file", testFile]);

                // Then the file ends on the result line and holds no hint.
                Assert.Equal(0, result.ExitCode);

                var fileContent = await System.IO.File.ReadAllTextAsync(testFile);
                Assert.DoesNotContain("--help", fileContent);
                Assert.StartsWith("Latency:", LastLine(fileContent));
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

            // Given a server is found and latency measures, but every upload request fails.
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(new ScriptedSpeedTester { UploadFactory = _ => ScriptedSpeedTester.AllFailed(32) });
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([]);

            // Then the annotated result is still the last thing printed.
            Assert.Equal(0, result.ExitCode);
            Assert.DoesNotContain("--help", result.Output);

            var lastLine = LastLine(result.Output);
            Assert.StartsWith("Latency:", lastLine);
            Assert.Contains("32 of 32 requests failed", lastLine);
        }

        private static ServiceCollection DefaultServices()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            return services;
        }

        /// <summary>
        /// The last line carrying anything, so a trailing newline does not hide what the output
        /// actually ends on.
        /// </summary>
        private static string LastLine(string output) =>
            output.Split('\n').Select(line => line.Trim()).Last(line => line.Length > 0);

        private static int ResultLineCount(string output) =>
            output.Split('\n').Count(line => line.Trim().StartsWith("Latency:", StringComparison.Ordinal));
    }
}
