namespace NetPace.Console.Tests;

/// <summary>
/// The code NetPace exits with, one per kind of outcome: an operational fault, a usage error, a
/// missed target and a <c>--fail-on</c> trigger each have their own, and a run to which more than
/// one applies reports the most serious.
/// </summary>
/// <remarks>
/// Every code here is an interface contract a monitoring check reads, so each is named as a literal
/// rather than read back off the code under test.
/// </remarks>
public sealed partial class NetPaceConsoleTests
{
    public sealed class ExitCodes
    {
        private static CommandLineTestHost HostWith(ISpeedTestService service)
        {
            var services = new ServiceCollection();
            services.AddSingleton(service);
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            return GetCommandLineTestHost(services);
        }

        /// <summary>
        /// A path in a directory that does not exist, so nothing can be read from it or written to
        /// it. Fresh on every call.
        /// </summary>
        private static string UnusableFilePath() =>
            Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "netpace-exit-codes");

        [Theory]
        [InlineData("--csv", "--json")]
        [InlineData("--no-latency", "--no-download", "--no-upload")]
        [InlineData("--profile", "huge")]
        [InlineData("--count", "ABC")]
        [InlineData("--min-download", "Mbps")]
        [InlineData("--not-an-option")]
        public async Task An_Unusable_Command_Line_Exits_Two(params string[] args)
        {
            // SCENARIO: An unusable command line exits two

            // Given a command line NetPace refuses before running anything.
            var host = StubHost();

            // When
            var result = await host.RunAsync(args);

            // Then the refusal is a usage error, and it reaches the error stream.
            Assert.Equal(2, result.ExitCode);
            Assert.NotEmpty(result.ErrorOutput);
        }

        [Fact]
        public async Task An_Unusable_Command_Line_To_The_Servers_Command_Exits_Two()
        {
            // Given the servers command asked for an option it does not have.
            var host = StubHost();

            // When
            var result = await host.RunAsync(["servers", "--not-an-option"]);

            // Then
            Assert.Equal(2, result.ExitCode);
            Assert.NotEmpty(result.ErrorOutput);
        }

        [Fact]
        public async Task A_FailOn_Trigger_Exits_Four()
        {
            // SCENARIO: A fail-on trigger exits four

            // Given a requested test in which no request succeeded.
            var service = new ScriptedSpeedTester { UploadFactory = _ => ScriptedSpeedTester.AllFailed(32) };
            var host = HostWith(service);

            // When
            var result = await host.RunAsync(["--fail-on", "total"]);

            // Then
            Assert.Equal(4, result.ExitCode);
        }

        [Theory]
        [InlineData("--count", "5")]
        [InlineData("--loop")]
        public async Task A_FailOn_Trigger_Exits_Four_In_A_Repeating_Run(params string[] repeat)
        {
            // Given a repeating run whose first test already all-fails.
            var service = new ScriptedSpeedTester { UploadFactory = _ => ScriptedSpeedTester.AllFailed(32) };
            var host = HostWith(service);

            // A trigger that stopped working would leave --loop running forever, so the run is
            // given up on after a while instead: the test then fails rather than hangs.
            using var giveUp = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            // When
            var result = await host.RunAsync([.. repeat, "--fail-on", "total", "--simple"], giveUp.Token);

            // Then the code is the trigger's own.
            Assert.Equal(4, result.ExitCode);
        }

        [Theory]
        [InlineData("total")]
        [InlineData("partial")]
        public async Task A_Measurement_That_Never_Ran_Exits_Four(string failOn)
        {
            // Given discovery fails, so nothing is measured at all.
            var mock = new SpeedTestMock
            {
                GetServersAsyncFunc = _ => throw new HttpRequestException("No such host is known"),
            };
            var host = HostWith(mock);

            // When
            var result = await host.RunAsync(["--fail-on", failOn]);

            // Then a run that produced nothing trips either threshold.
            Assert.Equal(4, result.ExitCode);
        }

        [Fact]
        public async Task A_FailOn_Trigger_Outranks_A_Missed_Target()
        {
            // SCENARIO: A fail-on trigger outranks a missed target

            // Given a download test whose every request failed, with both --fail-on and a target
            // the zero figure misses.
            var service = new ScriptedSpeedTester { DownloadFactory = _ => ScriptedSpeedTester.AllFailed(150) };
            var host = HostWith(service);

            // When
            var result = await host.RunAsync(["--simple", "--fail-on", "total", "--min-download", "50Mbps"]);

            // Then the trigger decides the code, not the missed target.
            Assert.Equal(4, result.ExitCode);
        }

        [Fact]
        public async Task An_Operational_Fault_Exits_One()
        {
            // SCENARIO: An operational fault still exits one

            // Given a valid command line whose --file output cannot be written.
            var host = StubHost();

            // When
            var result = await host.RunAsync(["--file", UnusableFilePath()]);

            // Then
            Assert.Equal(1, result.ExitCode);
            Assert.NotEmpty(result.ErrorOutput);
        }

        [Fact]
        public async Task An_Operational_Fault_Outranks_A_FailOn_Trigger_And_A_Missed_Target()
        {
            // Given a test whose download already trips --fail-on and misses its target, and whose
            // upload then meets an operational fault, so all three outcomes arise in one test.
            const string diskFull = "There is not enough space on the disk.";
            var service = new ScriptedSpeedTester
            {
                DownloadFactory = _ => ScriptedSpeedTester.AllFailed(150),
                UploadFactory = _ => throw new IOException(diskFull),
            };
            var host = HostWith(service);

            // When
            var result = await host.RunAsync(["--simple", "--fail-on", "total", "--min-download", "50Mbps"]);

            // Then the fault decides the code, and it is the fault that was reported.
            Assert.Equal(1, result.ExitCode);
            Assert.Contains(diskFull, result.ErrorOutput);
        }

        [Fact]
        public async Task An_Unusable_Server_File_Exits_One()
        {
            // The file is part of the environment the run met rather than of the command line, so
            // it is an operational fault however it fails.
            var host = StubHost();

            // When
            var result = await host.RunAsync(["--server-file", UnusableFilePath()]);

            // Then
            Assert.Equal(1, result.ExitCode);
            Assert.NotEmpty(result.ErrorOutput);
        }

        [Fact]
        public async Task An_Unusable_Command_Line_Outranks_An_Unusable_Server_File()
        {
            // Given a command line that is refused on its own terms, naming a server file that
            // cannot be read either.
            var host = StubHost();

            // When
            var result = await host.RunAsync(["--csv", "--json", "--server-file", UnusableFilePath()]);

            // Then the command line is checked first, so the usage error is what the run reports.
            Assert.Equal(2, result.ExitCode);
        }

        [Fact]
        public async Task A_Missed_Target_Alone_Exits_Three()
        {
            // Given a target far above what the run measures, with no fault and no --fail-on.
            var host = StubHost();

            // When
            var result = await host.RunAsync(["--simple", "--min-download", "50Mbps"]);

            // Then
            Assert.Equal(3, result.ExitCode);
        }

        [Fact]
        public async Task A_Run_That_Met_Every_Condition_Exits_Zero()
        {
            // Given a run with no opt-in asked for.
            var host = StubHost();

            // When
            var result = await host.RunAsync(["--simple"]);

            // Then
            Assert.Equal(0, result.ExitCode);
        }
    }
}
