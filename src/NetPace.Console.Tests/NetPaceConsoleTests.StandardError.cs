using NetPace.Console.Diagnostics;
using NetPace.Console.Tests.Theming;
using NetPace.Console.Theming;
using Spectre.Console;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    /// <summary>
    /// Which stream carries an error. Standard output and a <c>--file</c> target carry results
    /// only, so a script reading a format never meets a line that is not part of it.
    /// </summary>
    public sealed class StandardError
    {
        /// <summary>
        /// The message a failing stub reports, distinctive enough that finding it in a stream says
        /// which stream carried the error rather than which words it used.
        /// </summary>
        private const string NetworkFailure = "Could not connect to server";

        /// <summary>
        /// A host whose every test fails the way an unreachable network does: a condition NetPace
        /// reports and exits 0 on, so the run reaches the end and each format writes whatever it
        /// writes for a test that produced nothing.
        /// </summary>
        private static CommandLineTestHost FailingHost(int consoleWidth = CommandLineTestHost.UnlimitedWidth, bool recordColour = false, IServiceCollection? services = null)
        {
            services ??= new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(new SpeedTestMock
            {
                GetServersAsyncFunc = _ => throw new HttpRequestException(NetworkFailure)
            });
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            return new CommandLineTestHost(services, consoleWidth, recordColour: recordColour);
        }

        [Theory]
        [InlineData("--csv")]
        [InlineData("--json")]
        [InlineData("--minimal")]
        [InlineData("--simple")]
        public async Task A_Failed_Tests_Error_Goes_To_Standard_Error_In_Every_Machine_Format(string format)
        {
            // SCENARIO: A failed test's error goes to standard error

            // Given a run of two tests, both of which fail.
            var host = FailingHost();

            // When
            var result = await host.RunAsync([format, "--count", "2"]);

            // Then every error is on standard error, and standard output carries none of them.
            Assert.Equal(2, result.ErrorOutput.Split(NetworkFailure).Length - 1);
            Assert.DoesNotContain(NetworkFailure, result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("Error:", result.Output, StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_Failed_Tests_Error_Goes_To_Standard_Error_In_Default_Output()
        {
            // SCENARIO: A failed test's error goes to standard error

            // Given a looping run whose tests fail, in the format a person reads.
            var host = FailingHost();

            // When
            var result = await host.RunAsync(["--count", "2"]);

            // Then
            Assert.Equal(2, result.ErrorOutput.Split(NetworkFailure).Length - 1);
            Assert.DoesNotContain(NetworkFailure, result.Output, StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_File_Log_Never_Receives_An_Error()
        {
            // SCENARIO: A file log never receives an error

            // Given a --file target and a run whose test fails.
            var testFile = Path.Join(Path.GetTempPath(), $"netpace-test-{Guid.NewGuid()}.txt");
            var host = FailingHost();

            try
            {
                // When
                var result = await host.RunAsync(["--simple", "--file", testFile]);

                // Then the log holds results only, and the error reached standard error.
                Assert.Contains(NetworkFailure, result.ErrorOutput, StringComparison.Ordinal);

                var fileContent = await System.IO.File.ReadAllTextAsync(testFile);
                Assert.DoesNotContain(NetworkFailure, fileContent, StringComparison.Ordinal);
                Assert.DoesNotContain("Error:", fileContent, StringComparison.Ordinal);
            }
            finally
            {
                if (System.IO.File.Exists(testFile)) System.IO.File.Delete(testFile);
            }
        }

        [Fact]
        public async Task A_Failed_First_Run_Leaves_A_New_Csv_Log_Ready_For_Its_Header()
        {
            // SCENARIO: A failed first run leaves a new CSV log ready for its header

            // Given a new, empty --file target and a CSV run whose only test fails.
            var testFile = Path.Join(Path.GetTempPath(), $"netpace-test-{Guid.NewGuid()}.csv");

            try
            {
                var failed = await FailingHost().RunAsync(["--csv", "--file", testFile]);
                Assert.Equal(0, failed.ExitCode);

                // When a later CSV run into the same file succeeds.
                var services = new ServiceCollection();
                services.AddSingleton<ISpeedTestService, SpeedTestStub>();
                services.AddSingleton<IClock, ClockStub>();
                services.AddSingleton<IWaiter, NoDelayStub>();
                var succeeded = await new CommandLineTestHost(services).RunAsync(["--csv", "--file", testFile]);
                Assert.Equal(0, succeeded.ExitCode);

                // Then the file's first line is the header and its second is that run's data row.
                var lines = await System.IO.File.ReadAllLinesAsync(testFile);
                Assert.Equal(2, lines.Length);
                Assert.StartsWith("Timestamp,LatencyMilliseconds,", lines[0], StringComparison.Ordinal);
                Assert.DoesNotContain("Timestamp", lines[1], StringComparison.Ordinal);
            }
            finally
            {
                if (System.IO.File.Exists(testFile)) System.IO.File.Delete(testFile);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Errors_Are_Reported_Under_Quiet(bool withFile)
        {
            // SCENARIO: Errors are reported under quiet

            // Given --quiet, with and without a --file target, and a run whose test fails.
            var testFile = Path.Join(Path.GetTempPath(), $"netpace-test-{Guid.NewGuid()}.txt");
            var host = FailingHost();
            string[] args = withFile ? ["--quiet", "--file", testFile] : ["--quiet"];

            try
            {
                // When
                var result = await host.RunAsync(args);

                // Then the error is still reported, on standard error and nowhere else.
                Assert.Contains(NetworkFailure, result.ErrorOutput, StringComparison.Ordinal);
                Assert.Empty(result.Output);

                if (withFile)
                {
                    var fileContent = await System.IO.File.ReadAllTextAsync(testFile);
                    Assert.DoesNotContain(NetworkFailure, fileContent, StringComparison.Ordinal);
                }
            }
            finally
            {
                if (System.IO.File.Exists(testFile)) System.IO.File.Delete(testFile);
            }
        }

        [Fact]
        public async Task A_Rejected_Option_Is_Reported_On_Standard_Error()
        {
            // SCENARIO: A rejected option is reported on standard error

            // Given an invalid option value.
            var host = FailingHost();

            // When
            var result = await host.RunAsync(["--profile", "huge"]);

            // Then
            Assert.Equal(1, result.ExitCode);
            Assert.Contains("huge", result.ErrorOutput, StringComparison.Ordinal);
            Assert.Empty(result.Output);
        }

        [Fact]
        public async Task A_Validation_Error_Is_Reported_On_Standard_Error()
        {
            // Given a combination of options the settings reject after parsing.
            var host = FailingHost();

            // When
            var result = await host.RunAsync(["--no-latency", "--no-download", "--no-upload"]);

            // Then
            Assert.Equal(1, result.ExitCode);
            Assert.NotEmpty(result.ErrorOutput);
            Assert.Empty(result.Output);
        }

        [Fact]
        public async Task An_Operational_Fault_Is_Reported_On_Standard_Error()
        {
            // Given a --file target that cannot be opened for writing.
            var unwritable = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "out.csv");
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = new CommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--file", unwritable]);

            // Then
            Assert.Equal(1, result.ExitCode);
            Assert.Contains("out.csv", result.ErrorOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("out.csv", result.Output, StringComparison.Ordinal);
        }

        [Fact]
        public async Task An_Error_From_The_Servers_Command_Is_Reported_On_Standard_Error()
        {
            // Given server discovery that fails.
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(new SpeedTestMock
            {
                GetServersAsyncFunc = _ => throw new HttpRequestException(NetworkFailure)
            });
            var host = new CommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["servers"]);

            // Then
            Assert.Contains(NetworkFailure, result.ErrorOutput, StringComparison.Ordinal);
            Assert.DoesNotContain(NetworkFailure, result.Output, StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_Failed_Tests_Error_Follows_That_Runs_Diagnostic_Records()
        {
            // Given both streams written to one place, as they are in a terminal, and a run whose
            // test fails under --diagnostics.
            using var stream = new StringWriter();
            var services = new ServiceCollection();
            services.AddSingleton(new DiagnosticOutput(stream));
            services.AddSingleton(new ErrorOutput(AnsiConsole.Create(new AnsiConsoleSettings
            {
                Out = new AnsiConsoleOutput(stream),
                Ansi = AnsiSupport.No,
                ColorSystem = ColorSystemSupport.NoColors,
                Interactive = InteractionSupport.No
            })));

            // A download that throws, so the run reaches the records a test contributes before it
            // fails, rather than failing at discovery with only the run-level records written.
            services.AddSingleton<ISpeedTestService>(new ScriptedSpeedTester
            {
                DownloadFactory = _ => throw new HttpRequestException(NetworkFailure)
            });
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();

            // When
            var result = await new CommandLineTestHost(services).RunAsync(["--simple", "--diagnostics"]);

            // Then the records that explain the failure come first and the error last, so a reader
            // of the stream meets what was attempted before what went wrong.
            Assert.Equal(0, result.ExitCode);
            var written = stream.ToString();
            var error = written.IndexOf("Error:", StringComparison.Ordinal);
            var testStarted = written.IndexOf("event=test.start", StringComparison.Ordinal);
            Assert.InRange(testStarted, 0, int.MaxValue);
            Assert.InRange(error, testStarted + 1, int.MaxValue);
        }

        [Fact]
        public async Task An_Error_Is_One_Unbroken_Line_However_Narrow_The_Terminal()
        {
            // Given a terminal narrower than the message, and a run whose test fails with one long
            // enough to wrap.
            var sentence = string.Join(' ', Enumerable.Repeat("unreachable", 12));
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(new SpeedTestMock
            {
                GetServersAsyncFunc = _ => throw new HttpRequestException(sentence)
            });
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = new CommandLineTestHost(services, CommandLineTestHost.NarrowTerminalWidth);

            // When
            var result = await host.RunAsync([]);

            // Then the whole message sits on one line of standard error.
            var lines = result.ErrorOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Single(lines);
            Assert.Contains(sentence, lines[0], StringComparison.Ordinal);
        }

        [Fact]
        public async Task An_Error_Is_Drawn_In_Colour_On_A_Colour_Capable_Terminal()
        {
            // Given a colour-capable terminal and the default theme.
            var host = FailingHost(CommandLineTestHost.RedirectedWidth, recordColour: true);

            // When
            var result = await host.RunAsync([]);

            // Then the error stream carries the colour a person would see, so the assertions below
            // about losing it are about the theme rather than about a colourless console.
            Assert.True(AnsiSnapshot.CarriesStyling(result.ErrorOutput));
        }

        [Fact]
        public async Task An_Error_Carries_No_Colour_Under_The_Plain_Theme()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IThemeProvider>(new FixedThemeProvider(Theme.Plain));
            var host = FailingHost(CommandLineTestHost.RedirectedWidth, recordColour: true, services: services);

            // When
            var result = await host.RunAsync([]);

            // Then
            Assert.Contains(NetworkFailure, AnsiSnapshot.WithoutStyling(result.ErrorOutput), StringComparison.Ordinal);
            Assert.False(AnsiSnapshot.CarriesStyling(result.ErrorOutput));
        }

        [Fact]
        public async Task An_Error_Carries_No_Colour_Under_No_Color()
        {
            // Given NO_COLOR set in the environment the run reads.
            var services = new ServiceCollection();
            services.AddSingleton<IThemeProvider>(new EnvironmentThemeProvider(
                name => name == EnvironmentThemeProvider.NoColourVariable ? "1" : null));
            var host = FailingHost(CommandLineTestHost.RedirectedWidth, recordColour: true, services: services);

            // When
            var result = await host.RunAsync([]);

            // Then
            Assert.Contains(NetworkFailure, AnsiSnapshot.WithoutStyling(result.ErrorOutput), StringComparison.Ordinal);
            Assert.False(AnsiSnapshot.CarriesStyling(result.ErrorOutput));
        }
    }
}
