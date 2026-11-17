using System.Text;
using Microsoft.Extensions.DependencyInjection;
using NetPace.Console.Diagnostics;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    /// <summary>
    /// <c>--diagnostics-file</c>: the same records <c>--diagnostics</c> writes, sent to a file the
    /// user names instead of to the error stream, so one file can be attached to a bug report and
    /// the error stream still shows what went wrong.
    /// </summary>
    public sealed class DiagnosticsFile
    {
        /// <summary>
        /// The output formats this suite runs the diagnostics file against, by the switch that
        /// selects each. The default format is selected by no switch at all.
        /// </summary>
        public static TheoryData<string> OutputFormats() => new("", "--minimal", "--simple", "--csv", "--json");

        private static CommandLineTestHost HostWith(IServiceCollection? extra = null)
        {
            var services = extra ?? new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(new DiagnosticSpeedTester());
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            return new CommandLineTestHost(services);
        }

        private static string[] Invocation(string outputFormat, params string[] rest) =>
            [.. (string.IsNullOrEmpty(outputFormat) ? Array.Empty<string>() : new[] { outputFormat }), .. rest];

        private static string TempPath() =>
            Path.Combine(Path.GetTempPath(), $"netpace-diagnostics-{Guid.NewGuid():N}.log");

        /// <summary>
        /// The records with the one that reports the invocation dropped, so two runs asked for in
        /// different ways can be compared on everything else they recorded.
        /// </summary>
        private static string[] WithoutInvocation(string records) =>
            [.. records.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !line.Contains(" event=run.invocation ", StringComparison.Ordinal))];

        [Fact]
        public async Task DiagnosticsFile_HoldsWhatTheErrorStreamWouldHaveCarried()
        {
            // SCENARIO: Diagnostics written to a named file

            // Given the same run asked for twice, once with the records on the error stream and
            // once with them in a file.
            var path = TempPath();

            try
            {
                // When both run.
                var toStream = await HostWith().RunAsync(["--diagnostics"]);
                var toFile = await HostWith().RunAsync(["--diagnostics-file", path]);

                // Then the file holds the records the error stream would have carried, and the
                // error stream carried none of them.
                Assert.Equal(
                    WithoutInvocation(toStream.DiagnosticOutput),
                    WithoutInvocation(await System.IO.File.ReadAllTextAsync(path)));
                Assert.Equal(string.Empty, toFile.DiagnosticOutput);
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        [Theory]
        [MemberData(nameof(OutputFormats))]
        public async Task DiagnosticsFile_LeavesTheResultUntouchedAndTheErrorStreamClear(string outputFormat)
        {
            // Given the same invocation twice, once with diagnostics going to a file and once
            // without diagnostics at all.
            var path = TempPath();

            try
            {
                // When both run.
                var without = await HostWith().RunAsync(Invocation(outputFormat));
                var withFile = await HostWith().RunAsync(Invocation(outputFormat, "--diagnostics-file", path));

                // Then the result is untouched in this format, and no record reached the error
                // stream - a script reading either stream cannot tell the difference.
                Assert.Equal(without.Output, withFile.Output);
                Assert.Equal(without.ExitCode, withFile.ExitCode);
                Assert.Equal(string.Empty, withFile.DiagnosticOutput);
                Assert.DoesNotContain("event=", withFile.ErrorOutput, StringComparison.Ordinal);
                Assert.Contains("event=run.start", await System.IO.File.ReadAllTextAsync(path), StringComparison.Ordinal);
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        [Fact]
        public async Task DiagnosticsFile_SwitchesDiagnosticsOn_AndPassingDiagnosticsTooChangesNothing()
        {
            // Given the file named on its own, and named alongside --diagnostics.
            var alone = TempPath();
            var withSwitch = TempPath();

            try
            {
                // When both run.
                await HostWith().RunAsync(["--diagnostics-file", alone]);
                await HostWith().RunAsync(["--diagnostics", "--diagnostics-file", withSwitch]);

                // Then the file was written either way, and the extra switch added nothing to it
                // and sent nothing to the error stream.
                Assert.Equal(
                    WithoutInvocation(await System.IO.File.ReadAllTextAsync(alone)),
                    WithoutInvocation(await System.IO.File.ReadAllTextAsync(withSwitch)));
            }
            finally
            {
                System.IO.File.Delete(alone);
                System.IO.File.Delete(withSwitch);
            }
        }

        [Fact]
        public async Task DiagnosticsFile_ReplacesWhateverTheFileHeld()
        {
            // Given a file that already holds something.
            var path = TempPath();
            await System.IO.File.WriteAllTextAsync(path, "records from an earlier run" + Environment.NewLine);

            try
            {
                // When a run directs its diagnostics at it.
                await HostWith().RunAsync(["--diagnostics-file", path]);

                // Then only this run's records are in it.
                var written = await System.IO.File.ReadAllTextAsync(path);
                Assert.DoesNotContain("earlier run", written, StringComparison.Ordinal);
                Assert.Contains("event=run.start", written, StringComparison.Ordinal);
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        [Fact]
        public async Task Errors_StayOnTheErrorStream_WhenDiagnosticsGoToAFile()
        {
            // SCENARIO: Errors stay visible when diagnostics go to a file

            // Given a server that never answers, so the run reports a failure.
            var path = TempPath();
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(new SpeedTestMock
            {
                GetServerLatencyByServerUrlAsyncFunc = (_, _, _) => throw new HttpRequestException("No such host is known."),
            });
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();

            try
            {
                // When it runs with its diagnostics going to a file.
                var result = await new CommandLineTestHost(services).RunAsync(
                    ["--server-url", "http://unreachable.example/upload.php", "--diagnostics-file", path]);

                // Then the error is still on the error stream, where the user sees it, and the
                // records are in the file.
                Assert.Contains("No such host is known.", result.ErrorOutput, StringComparison.Ordinal);
                Assert.Contains("event=run.start", await System.IO.File.ReadAllTextAsync(path), StringComparison.Ordinal);
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        [Fact]
        public async Task Unwritable_DiagnosticsFile_FailsTheRunNamingThePath()
        {
            // SCENARIO: Diagnostics file cannot be written

            // Given a diagnostics path under a directory that does not exist.
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "diagnostics.log");

            // When the run is asked for.
            var result = await HostWith().RunAsync(["--diagnostics-file", path]);

            // Then it fails before measuring anything, saying which path it could not write.
            Assert.Equal(1, result.ExitCode);
            Assert.Empty(result.Output);
            Assert.Contains(path, result.ErrorOutput, StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_WriteFailurePartWayThrough_IsReportedOnceAndLeavesTheRunAlone()
        {
            // Given a diagnostics file that takes the open and then refuses every write, and three
            // iterations, so the run flushes to it more than once.
            var path = Path.Combine(Path.GetTempPath(), "netpace-diagnostics-unwritable.log");
            var services = new ServiceCollection();
            services.AddSingleton<IDiagnosticFileOpener>(new UnwritableFileOpener());

            // When the run goes ahead.
            var result = await HostWith(services).RunAsync(["--simple", "--count", "3", "--diagnostics-file", path]);

            // Then the user is told once which path failed, and the result and the exit code are
            // what a run with no diagnostics at all would have produced.
            var without = await HostWith().RunAsync(["--simple", "--count", "3"]);
            Assert.Equal(1, Occurrences(result.ErrorOutput, path));
            Assert.Equal(without.Output, result.Output);
            Assert.Equal(without.ExitCode, result.ExitCode);
        }

        /// <summary>
        /// An opener whose file accepts the open and then refuses every write, standing in for a
        /// destination that fails once the run is under way: a full disk, a share that went away.
        /// </summary>
        private sealed class UnwritableFileOpener : IDiagnosticFileOpener
        {
            public TextWriter Open(string path) => new RefusingWriter();

            private sealed class RefusingWriter : TextWriter
            {
                public override Encoding Encoding => Encoding.UTF8;

                public override void Write(char value) => throw new IOException("There is not enough space on the disk.");

                public override void WriteLine(string? value) => throw new IOException("There is not enough space on the disk.");
            }
        }
    }
}
