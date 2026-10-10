using Microsoft.Extensions.DependencyInjection;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    /// <summary>
    /// <c>--diagnostics</c>: a second stream carrying how the run went, in every output format,
    /// leaving the result on the first stream exactly as it was.
    /// </summary>
    public sealed class Diagnostics
    {
        /// <summary>
        /// The four output formats, by the switch that selects each. The default format is selected
        /// by no switch at all.
        /// </summary>
        public static TheoryData<string> OutputFormats() => new("", "--minimal", "--csv", "--json");

        private static CommandLineTestHost HostWith(bool failEveryUpload = false)
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(new DiagnosticSpeedTester { FailEveryUpload = failEveryUpload });
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            return new CommandLineTestHost(services);
        }

        private static string[] Invocation(string outputFormat, params string[] rest) =>
            [.. (string.IsNullOrEmpty(outputFormat) ? Array.Empty<string>() : new[] { outputFormat }), .. rest];

        [Theory]
        [MemberData(nameof(OutputFormats))]
        public async Task Diagnostics_LeaveTheResultStreamByteIdentical(string outputFormat)
        {
            // Given the same invocation twice, once with --diagnostics and once without.
            // When both run.
            var without = await HostWith().RunAsync(Invocation(outputFormat));
            var with = await HostWith().RunAsync(Invocation(outputFormat, "--diagnostics"));

            // Then the result is untouched - a script piping the result cannot tell the difference.
            Assert.Equal(without.Output, with.Output);
            Assert.Equal(without.ExitCode, with.ExitCode);
        }

        [Theory]
        [MemberData(nameof(OutputFormats))]
        public async Task Without_Diagnostics_NothingIsWrittenToTheDiagnosticStream(string outputFormat)
        {
            // Given an invocation that does not ask for diagnostics.
            // When it runs.
            var result = await HostWith().RunAsync(Invocation(outputFormat));

            // Then the diagnostic stream carries nothing at all.
            Assert.Equal(string.Empty, result.DiagnosticOutput);
        }

        [Theory]
        [MemberData(nameof(OutputFormats))]
        public async Task Each_Stream_CarriesOnlyItsOwnContent(string outputFormat)
        {
            // Given a run with diagnostics in some output format.
            // When it runs.
            var result = await HostWith().RunAsync(Invocation(outputFormat, "--diagnostics"));

            // Then the two streams can be redirected apart: no diagnostic record reaches the result,
            // and no result line reaches the diagnostics.
            Assert.DoesNotContain("event=", result.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("Download:", result.DiagnosticOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("DownloadSucceeded", result.DiagnosticOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("\"DownloadSpeed\"", result.DiagnosticOutput, StringComparison.Ordinal);
        }

        [Theory]
        [MemberData(nameof(OutputFormats))]
        public async Task Every_OutputFormat_ProducesTheSameDiagnostics(string outputFormat)
        {
            // Given the compact, CSV and JSON formats pass no progress reporter at all, diagnostics
            // must not ride on one.
            // When each runs with --diagnostics.
            var result = await HostWith().RunAsync(Invocation(outputFormat, "--diagnostics"));

            // Then every format produces the whole record set.
            Assert.Contains("event=run.start", result.DiagnosticOutput, StringComparison.Ordinal);
            Assert.Contains("event=run.invocation", result.DiagnosticOutput, StringComparison.Ordinal);
            Assert.Contains("event=server.selected", result.DiagnosticOutput, StringComparison.Ordinal);
            Assert.Contains("event=test.start test=download", result.DiagnosticOutput, StringComparison.Ordinal);
            Assert.Contains("event=test.start test=upload", result.DiagnosticOutput, StringComparison.Ordinal);
            Assert.Contains("event=request test=download", result.DiagnosticOutput, StringComparison.Ordinal);
            Assert.Contains("event=run.end", result.DiagnosticOutput, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Diagnostics_IdentifyTheRunAndTheMachineItRanOn()
        {
            // Given a run invoked with several switches.
            // When it runs with diagnostics.
            var result = await HostWith().RunAsync(["--json", "--no-upload", "--diagnostics"]);

            // Then the record set says when, as what, on what, and how it was asked to run.
            Assert.Contains("ts=1980-01-01T10:05:00.000 event=run.start version=0.0.0 runtime=\".NET 10.0.0\" os=\"Test OS 1.0\" arch=x64", result.DiagnosticOutput, StringComparison.Ordinal);
            Assert.Contains("event=run.invocation args=\"--json --no-upload --diagnostics\"", result.DiagnosticOutput, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Diagnostics_RecordAnAutoSelectedServerAndTheScreeningFigureThatChoseIt()
        {
            // Given no --server, so screening chooses.
            // When the run goes ahead with diagnostics.
            var result = await HostWith().RunAsync(["--minimal", "--diagnostics"]);

            // Then the route is named, and the figure shown is the screening figure the choice was
            // based on - not the measured figure, which appears in the latency test's own records.
            Assert.Contains("event=server.selected sponsor=\"Deutsche Telekom\" location=Frankfurt url=" + DiagnosticSpeedTester.Url + " selection=auto-latency latency_ms=31", result.DiagnosticOutput, StringComparison.Ordinal);
            Assert.Contains("event=test.start test=screening", result.DiagnosticOutput, StringComparison.Ordinal);
            Assert.Contains("event=test.start test=latency", result.DiagnosticOutput, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Diagnostics_RecordAUserSpecifiedServer()
        {
            // Given the user named the server.
            // When the run goes ahead with diagnostics.
            var result = await HostWith().RunAsync(["--minimal", "--server", DiagnosticSpeedTester.Url, "--diagnostics"]);

            // Then the route says so, and no screening figure is claimed - nothing was screened.
            Assert.Contains("selection=specified", result.DiagnosticOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("latency_ms=", result.DiagnosticOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("test=screening", result.DiagnosticOutput, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Diagnostics_RecordAFirstInListServerAndOmitTheLatencyFigure()
        {
            // Given --no-latency without --server, so the first server offered is used.
            // When the run goes ahead with diagnostics.
            var result = await HostWith().RunAsync(["--minimal", "--no-latency", "--diagnostics"]);

            // Then the route says so, and no latency figure is reported at all.
            Assert.Contains("selection=first-in-list", result.DiagnosticOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("latency_ms=", result.DiagnosticOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("test=latency", result.DiagnosticOutput, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Diagnostics_SayWhichTestsRan()
        {
            // Given a run with the download test switched off.
            // When it runs with diagnostics.
            var result = await HostWith().RunAsync(["--minimal", "--no-download", "--diagnostics"]);

            // Then the tests that ran are named, and the one that did not is absent.
            Assert.Contains("event=test.start test=latency", result.DiagnosticOutput, StringComparison.Ordinal);
            Assert.Contains("event=test.start test=upload", result.DiagnosticOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("test=download", result.DiagnosticOutput, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Diagnostics_RecordEveryRequestIndividually()
        {
            // Given a run whose every test issues more than one request.
            // When it runs with diagnostics.
            var result = await HostWith().RunAsync(["--minimal", "--diagnostics"]);

            // Then each request has its own record, and the test's summary agrees with them.
            await Verify(result.DiagnosticOutput);
        }

        [Fact]
        public async Task An_AllFailedUploadTest_NamesTheCauseWithoutReRunningTheTest()
        {
            // Regression bar for #245: a server that rejects every upload reported 0 bps and
            // nothing else. The diagnostics must name the rejection cause from that one run.

            // Given a server that rejects every upload.
            // When the run goes ahead with diagnostics.
            var result = await HostWith(failEveryUpload: true).RunAsync(["--minimal", "--diagnostics"]);

            // Then the cause is attributable to the requests that failed, and it is on the record -
            // no second run, no hand-patched build.
            Assert.Contains("event=test.end test=upload requests=2 succeeded=0 failed=2", result.DiagnosticOutput, StringComparison.Ordinal);
            Assert.Contains("status=failed", result.DiagnosticOutput, StringComparison.Ordinal);
            Assert.Contains("exception=HttpRequestException", result.DiagnosticOutput, StringComparison.Ordinal);
            Assert.Contains("sent a TLS alert", result.DiagnosticOutput, StringComparison.Ordinal);
            Assert.Equal(0, result.ExitCode);
        }

        [Fact]
        public async Task Diagnostics_AreWrittenUnderQuietAndAreKeptOutOfTheFileTarget()
        {
            // Given --quiet with a file target, which together suppress the console entirely.
            var outputFile = Path.Combine(Path.GetTempPath(), $"netpace-diagnostics-{Guid.NewGuid():N}.txt");

            try
            {
                // When the run goes ahead with diagnostics.
                var result = await HostWith().RunAsync(["--minimal", "--quiet", "--file", outputFile, "--diagnostics"]);

                // Then diagnostics still reach the diagnostic stream, and never the file target.
                Assert.Contains("event=run.start", result.DiagnosticOutput, StringComparison.Ordinal);
                Assert.DoesNotContain("event=", await System.IO.File.ReadAllTextAsync(outputFile), StringComparison.Ordinal);
            }
            finally
            {
                System.IO.File.Delete(outputFile);
            }
        }

        [Fact]
        public async Task Under_Count_TheRunLevelRecordsAppearOnceAndTheTestsPerIteration()
        {
            // Given three iterations of the same test.
            // When they run with diagnostics.
            var result = await HostWith().RunAsync(["--minimal", "--count", "3", "--diagnostics"]);

            // Then the run is described once, and each iteration contributes its own test records.
            Assert.Equal(1, CountLines(result.DiagnosticOutput, "event=run.start"));
            Assert.Equal(1, CountLines(result.DiagnosticOutput, "event=run.invocation"));
            Assert.Equal(1, CountLines(result.DiagnosticOutput, "event=run.end"));
            Assert.Equal(3, CountLines(result.DiagnosticOutput, "event=test.start test=download"));
            Assert.Equal(3, CountLines(result.DiagnosticOutput, "event=server.selected"));
        }

        [Fact]
        public async Task Diagnostics_RecordTheExitCodeOfAFailedRun()
        {
            // Given a run told to exit non-zero on a totally failed measurement.
            // When every upload fails.
            var result = await HostWith(failEveryUpload: true).RunAsync(["--minimal", "--fail-on", "Total", "--diagnostics"]);

            // Then the diagnostics record the code the process actually exited with.
            Assert.Equal(1, result.ExitCode);
            Assert.Contains("event=run.end exit=1", result.DiagnosticOutput, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Every_DiagnosticRecordIsOneLine()
        {
            // Given a run whose failure reasons are long and would wrap at any terminal width.
            // When it runs with diagnostics.
            var result = await HostWith(failEveryUpload: true).RunAsync(["--minimal", "--diagnostics"]);

            // Then every line is a complete record, so grepping a subset loses nothing.
            var lines = result.DiagnosticOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.NotEmpty(lines);
            Assert.All(lines, line => Assert.StartsWith("ts=", line, StringComparison.Ordinal));
            Assert.All(lines, line => Assert.Contains(" event=", line, StringComparison.Ordinal));
        }

        private static int CountLines(string text, string value) =>
            text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .Count(line => line.Contains(value, StringComparison.Ordinal));
    }
}
