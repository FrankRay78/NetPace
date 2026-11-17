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
        private static CommandLineTestHost HostWith(IServiceCollection? extra = null)
        {
            var services = extra ?? new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(new DiagnosticSpeedTester());
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            return new CommandLineTestHost(services);
        }

        [Fact]
        public async Task DiagnosticsFile_HoldsWhatTheErrorStreamWouldHaveCarried()
        {
            // SCENARIO: Diagnostics written to a named file

            // Given the same run asked for twice, once with the records on the error stream and
            // once with them in a file.
            using var file = new TempDiagnosticsFile();

            // When both run.
            var toStream = await HostWith().RunAsync(["--diagnostics"]);
            var toFile = await HostWith().RunAsync(["--diagnostics-file", file.FilePath]);

            // Then the file holds the records the error stream would have carried, and the error
            // stream carried none of them. Asserted non-empty first, because two empty record sets
            // would compare equal and this is the only test that this file holds the records.
            var records = WithoutInvocation(await System.IO.File.ReadAllTextAsync(file.FilePath));
            Assert.NotEmpty(records);
            Assert.Equal(WithoutInvocation(toStream.DiagnosticOutput), records);
            Assert.Equal(string.Empty, toFile.DiagnosticOutput);
        }

        [Theory]
        [MemberData(nameof(EveryOutputModeOfASpeedTest), MemberType = typeof(NetPaceConsoleTests))]
        public async Task DiagnosticsFile_LeavesTheResultUntouchedAndTheErrorStreamClear(string outputFormat)
        {
            // Given the same invocation twice, once with diagnostics going to a file and once
            // without diagnostics at all.
            using var file = new TempDiagnosticsFile();

            // When both run.
            var without = await HostWith().RunAsync(Invocation(outputFormat));
            var withFile = await HostWith().RunAsync(Invocation(outputFormat, "--diagnostics-file", file.FilePath));

            // Then the result is untouched in this format, and no record reached the error
            // stream - a script reading either stream cannot tell the difference.
            Assert.Equal(without.Output, withFile.Output);
            Assert.Equal(without.ExitCode, withFile.ExitCode);
            Assert.Equal(string.Empty, withFile.DiagnosticOutput);
            Assert.DoesNotContain("event=", withFile.ErrorOutput, StringComparison.Ordinal);
            Assert.Contains("event=run.start", await System.IO.File.ReadAllTextAsync(file.FilePath), StringComparison.Ordinal);
        }

        [Fact]
        public async Task DiagnosticsFile_SwitchesDiagnosticsOn_AndPassingDiagnosticsTooChangesNothing()
        {
            // Given the file named on its own, and named alongside --diagnostics.
            using var alone = new TempDiagnosticsFile();
            using var withSwitch = new TempDiagnosticsFile();

            // When both run.
            var fileOnly = await HostWith().RunAsync(["--diagnostics-file", alone.FilePath]);
            var bothSwitches = await HostWith().RunAsync(["--diagnostics", "--diagnostics-file", withSwitch.FilePath]);

            // Then the file was written either way, and the extra switch added nothing to it
            // and sent nothing to the error stream. Asserted non-empty first, because two empty
            // record sets would compare equal and leave the switch's having done nothing unproven.
            var records = WithoutInvocation(await System.IO.File.ReadAllTextAsync(alone.FilePath));
            Assert.NotEmpty(records);
            Assert.Equal(records, WithoutInvocation(await System.IO.File.ReadAllTextAsync(withSwitch.FilePath)));
            Assert.Equal(string.Empty, fileOnly.DiagnosticOutput);
            Assert.Equal(string.Empty, bothSwitches.DiagnosticOutput);
        }

        [Theory]
        [InlineData]
        [InlineData("--file-mode", "Append")]
        public async Task DiagnosticsFile_ReplacesWhateverTheFileHeld(params string[] fileMode)
        {
            // Given a file that already holds something, and a run that may ask for append mode -
            // which governs the --file target only and is stated not to reach the diagnostics file.
            using var file = new TempDiagnosticsFile();
            await System.IO.File.WriteAllTextAsync(file.FilePath, "records from an earlier run" + Environment.NewLine);

            // When a run directs its diagnostics at it.
            await HostWith().RunAsync(["--diagnostics-file", file.FilePath, .. fileMode]);

            // Then only this run's records are in it.
            var written = await System.IO.File.ReadAllTextAsync(file.FilePath);
            Assert.DoesNotContain("earlier run", written, StringComparison.Ordinal);
            Assert.Contains("event=run.start", written, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Errors_StayOnTheErrorStream_WhenDiagnosticsGoToAFile()
        {
            // SCENARIO: Errors stay visible when diagnostics go to a file

            // Given a server that never answers, so the run reports a failure.
            using var file = new TempDiagnosticsFile();
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(new SpeedTestMock
            {
                GetServerLatencyByServerUrlAsyncFunc = (_, _, _) => throw new HttpRequestException("No such host is known."),
            });
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();

            // When it runs with its diagnostics going to a file.
            var result = await new CommandLineTestHost(services).RunAsync(
                ["--server-url", "http://unreachable.example/upload.php", "--diagnostics-file", file.FilePath]);

            // Then the error is still on the error stream, where the user sees it, and the records
            // are in the file - the tail as well as the head, because the file is the one
            // destination whose last records reach disk only when the writer is closed.
            Assert.Contains("No such host is known.", result.ErrorOutput, StringComparison.Ordinal);

            var written = await System.IO.File.ReadAllTextAsync(file.FilePath);
            Assert.Contains("event=run.start", written, StringComparison.Ordinal);
            Assert.Contains($"event=run.end exit={result.ExitCode}", written, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Unwritable_DiagnosticsFile_FailsTheRunNamingThePath()
        {
            // SCENARIO: Diagnostics file cannot be written

            // Given a diagnostics path under a directory that does not exist.
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "diagnostics.log");

            // When the run is asked for.
            var result = await HostWith().RunAsync(["--diagnostics-file", path]);

            // Then it fails before measuring anything, saying that the diagnostics file is what it
            // could not write and which path that was - a run may name two files, so the message
            // has to say which of them stopped it.
            Assert.Equal(1, result.ExitCode);
            Assert.Empty(result.Output);
            Assert.Contains($"The diagnostics file {path} could not be opened", result.ErrorOutput, StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_DiagnosticsFile_NamingTheResultFile_IsRefused()
        {
            // Given --file and --diagnostics-file naming one path, which both open truncating.
            using var file = new TempDiagnosticsFile();

            // When the run is asked for.
            var result = await HostWith().RunAsync(["--simple", "--file", file.FilePath, "--diagnostics-file", file.FilePath]);

            // Then it is refused naming both options, rather than the records silently replacing
            // the result the user asked --file for.
            Assert.Equal(1, result.ExitCode);
            Assert.Contains("--file", result.ErrorOutput, StringComparison.Ordinal);
            Assert.Contains("--diagnostics-file", result.ErrorOutput, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task A_BlankDiagnosticsFile_IsRefused(string blank)
        {
            // Given --diagnostics-file given a value that names no path.
            // When the run is asked for.
            var result = await HostWith().RunAsync(["--simple", "--diagnostics-file", blank]);

            // Then it is refused naming the option, rather than exiting zero having written no
            // records and said nothing - the user asked for a file and would find none.
            Assert.Equal(1, result.ExitCode);
            Assert.Contains("--diagnostics-file", result.ErrorOutput, StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_WriteFailurePartWayThrough_IsReportedOnceAndLeavesTheRunAlone()
        {
            // Given a diagnostics destination that takes the open and then refuses every record,
            // and three iterations, so the run flushes to it more than once. The stub never touches
            // the path, which is only what the run has to name and what the report has to quote.
            using var file = new TempDiagnosticsFile();
            var services = new ServiceCollection();
            services.AddSingleton<IDiagnosticFileOpener>(new UnwritableFileOpener());

            // When the run goes ahead.
            var result = await HostWith(services).RunAsync(["--simple", "--count", "3", "--diagnostics-file", file.FilePath]);

            // Then the user is told once which path failed, and the result and the exit code are
            // what a run with no diagnostics at all would have produced.
            var without = await HostWith().RunAsync(["--simple", "--count", "3"]);
            Assert.Equal(1, Occurrences(result.ErrorOutput, file.FilePath));
            Assert.Equal(without.Output, result.Output);
            Assert.Equal(without.ExitCode, result.ExitCode);
        }

        /// <summary>
        /// A diagnostics file path unique to one test, deleted when that test finishes. Nothing is
        /// created up front, so a test chooses for itself whether the path already holds content.
        /// </summary>
        private sealed class TempDiagnosticsFile : IDisposable
        {
            public string FilePath { get; } = Path.Combine(Path.GetTempPath(), $"netpace-diagnostics-{Guid.NewGuid():N}.log");

            public void Dispose()
            {
                if (System.IO.File.Exists(FilePath))
                {
                    System.IO.File.Delete(FilePath);
                }
            }
        }

        /// <summary>
        /// An opener whose file accepts the open and then refuses every record, standing in for a
        /// destination that fails once the run is under way: a full disk, a share that went away.
        /// </summary>
        /// <remarks>
        /// Refuses the close as well as the writes, because a real <see cref="StreamWriter"/>
        /// flushes what it still holds when it is disposed and raises there too. A stub that
        /// accepted the close would fail only where the code already handles it, and report green
        /// over a run whose last act throws.
        /// </remarks>
        private sealed class UnwritableFileOpener : IDiagnosticFileOpener
        {
            public TextWriter Open(string path) => new RefusingWriter();

            private sealed class RefusingWriter : TextWriter
            {
                private const string DiskFull = "There is not enough space on the disk.";

                public override Encoding Encoding => Encoding.UTF8;

                public override void Write(char value) => throw new IOException(DiskFull);

                public override void WriteLine(string? value) => throw new IOException(DiskFull);

                public override void Flush() => throw new IOException(DiskFull);

                protected override void Dispose(bool disposing) => throw new IOException(DiskFull);
            }
        }
    }
}
