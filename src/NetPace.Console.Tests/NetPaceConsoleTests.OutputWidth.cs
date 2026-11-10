using System.Text.Json;
using NetPace.Core.Clients.Ookla;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    /// <summary>
    /// Machine-readable output is a record another program reads, so it leaves NetPace with no line
    /// breaks beyond the ones its own format defines - however narrow its destination is. The rest of
    /// the suite runs at an unlimited console width, which is exactly the condition under which the
    /// breaks never appear, so these tests choose the width themselves.
    /// </summary>
    public sealed class OutputWidth
    {
        // A sponsor, location and URL that each run past 80 columns on their own, so no line break
        // could fit one of them whole. Real servers carry names and URLs this long.
        private const string LongSponsor = "Deutsche Telekom Technik Gesellschaft mit beschrankter Haftung, Frankfurt am Main";
        private const string LongLocation = "Frankfurt am Main, Hessen, Bundesrepublik Deutschland, Western Europe, EMEA Region";
        private const string LongUrl = "http://ffm.wsqm.telekom-dienste.de:8080/speedtest/upload-and-download-endpoint.php";

        // A failure message that runs past 80 columns on its own, as a real host-resolution or
        // socket message does once the host name is in it.
        private const string LongErrorMessage = "No such host is known (ffm.wsqm.telekom-dienste.de:8080). The server could not be resolved.";

        // SCENARIO: Piped JSON output parses as JSON
        [Fact]
        public async Task Should_Write_Json_As_One_Parseable_Line_When_Stdout_Is_Redirected()
        {
            // Given stdout is redirected to another program

            // When
            var output = await RunAtWidthAsync(CommandLineTestHost.RedirectedWidth, ["--json"]);

            // Then the result is a single line a standard JSON parser accepts, carrying the values
            // NetPace composed rather than values a line break landed inside.
            var lines = Lines(output);
            Assert.Single(lines);
            AssertParsesAsJson(lines[0]);

            Assert.Equal(await RunAtWidthAsync(CommandLineTestHost.UnlimitedWidth, ["--json"]), output);
            await Verify(output);
        }

        [Fact]
        public async Task Should_Write_Json_Pretty_Without_Splitting_Long_Values_When_Stdout_Is_Redirected()
        {
            // Given stdout is redirected, and a server whose name and URL each pass 80 columns

            // When
            var output = await RunAtWidthAsync(CommandLineTestHost.RedirectedWidth, ["--json-pretty"], LongNamedServerTester());

            // Then the indentation --json-pretty asks for is the only thing breaking lines, and every
            // value survives whole - the snapshot shows each long value unbroken.
            AssertParsesAsJson(output);

            await Verify(output);
        }

        // SCENARIO: Piped CSV output keeps one record per line
        [Fact]
        public async Task Should_Write_Csv_One_Record_Per_Line_When_Stdout_Is_Redirected()
        {
            // Given stdout is redirected to another program

            // When
            var output = await RunAtWidthAsync(CommandLineTestHost.RedirectedWidth, ["--csv"]);

            // Then a CSV reader sees a one-line header and one line per result, every line carrying
            // the same number of fields.
            var lines = Lines(output);
            Assert.Equal(2, lines.Length);
            AssertEveryLineMatchesTheHeaderFieldCount(lines);

            Assert.Equal(await RunAtWidthAsync(CommandLineTestHost.UnlimitedWidth, ["--csv"]), output);
            await Verify(output);
        }

        [Fact]
        public async Task Should_Write_Csv_One_Record_Per_Line_For_Every_Result_Under_Count()
        {
            // Given stdout is redirected, and a run asked for three results

            // When
            var output = await RunAtWidthAsync(CommandLineTestHost.RedirectedWidth, ["--csv", "--count", "3"]);

            // Then the header still stands alone, and each of the three results is its own line
            var lines = Lines(output);
            Assert.Equal(4, lines.Length);
            AssertEveryLineMatchesTheHeaderFieldCount(lines);

            await Verify(output);
        }

        [Fact]
        public async Task Should_Write_Csv_One_Record_Per_Line_For_Every_Result_Under_Loop()
        {
            // Given stdout is redirected, and a loop cancelled after three results
            using var cancellationTokenSource = new CancellationTokenSource();
            var waiter = new SelfCancellingWaiter(3, cancellationTokenSource);

            // When
            var output = await RunAtWidthAsync(CommandLineTestHost.RedirectedWidth, ["--csv", "--loop"], waiter: waiter, cancellationToken: cancellationTokenSource.Token);

            // Then
            var lines = Lines(output);
            Assert.Equal(4, lines.Length);
            AssertEveryLineMatchesTheHeaderFieldCount(lines);

            await Verify(output);
        }

        [Fact]
        public async Task Should_Write_Simple_One_Figure_Per_Line_When_Stdout_Is_Redirected()
        {
            // Given stdout is redirected, and a download annotated with failed requests
            var service = new ScriptedSpeedTester { DownloadFactory = _ => ScriptedSpeedTester.Partial(150, 5) };

            // When
            var output = await RunAtWidthAsync(CommandLineTestHost.RedirectedWidth, ["--simple"], service);

            // Then the only line breaks are the ones the format defines - one per figure - so the
            // annotated figure arrives whole on its own line, which the snapshot carries. A line
            // wider than the window is the narrow-terminal test's job.
            await Verify(output);
        }

        [Fact]
        public async Task Should_Write_An_Error_Raised_During_The_Run_On_One_Line()
        {
            // Given stdout is redirected, and discovery fails with a message longer than that width.
            // A network condition is data rather than a fault, so the run reports it and still exits 0.
            var service = new SpeedTestMock
            {
                GetServersAsyncFunc = _ => throw new HttpRequestException(LongErrorMessage),
            };

            // When
            var output = await RunAtWidthAsync(CommandLineTestHost.RedirectedWidth, ["--csv"], service);

            // Then the error shares the records' stream and is unbroken on it, so a reader taking the
            // output a line at a time still sees the whole message.
            Assert.Contains($"Error: {LongErrorMessage}", Lines(output));
        }

        // SCENARIO: Machine-readable output is unbroken in a narrow terminal
        [InlineData("--json")]
        [InlineData("--json-pretty")]
        [InlineData("--csv")]
        [InlineData("--simple")]
        [InlineData("--minimal")]
        [Theory]
        public async Task Should_Write_Machine_Readable_Output_Unbroken_In_A_Narrow_Terminal(string formatSwitch)
        {
            // Given a terminal window narrower than the output line, reporting a server whose name
            // and URL each pass 80 columns, and a download annotated with failed requests - so even
            // --simple, which prints no server, has a line past the window's 40 columns
            static ScriptedSpeedTester Tester()
            {
                var tester = LongNamedServerTester();
                tester.DownloadFactory = _ => ScriptedSpeedTester.Partial(150, 5);
                return tester;
            }

            // When
            var narrow = await RunAtWidthAsync(CommandLineTestHost.NarrowTerminalWidth, [formatSwitch], Tester());

            // Then nothing the window's width did shows in the output: it is what the same run writes
            // with no width limit at all, break for break.
            var unlimited = await RunAtWidthAsync(CommandLineTestHost.UnlimitedWidth, [formatSwitch], Tester());
            Assert.Equal(unlimited, narrow);

            await Verify(narrow).UseParameters(formatSwitch);
        }

        [Fact]
        public async Task Should_Leave_Default_Output_Unbroken_In_A_Narrow_Terminal()
        {
            // Given a destination that cannot redraw, narrower than the lines default output writes
            // there. Those are the --simple lines, so they carry the same contract and must survive
            // a narrow window whole, just as the machine-readable formats above do.

            // When
            var narrow = await RunAtWidthAsync(CommandLineTestHost.NarrowTerminalWidth, []);

            // Then nothing the window's width did shows in the output.
            var unlimited = await RunAtWidthAsync(CommandLineTestHost.UnlimitedWidth, []);
            Assert.Equal(unlimited, narrow);

            await Verify(narrow);
        }

        [Fact]
        public async Task Should_Leave_File_Output_Unwrapped_In_A_Narrow_Terminal()
        {
            // Given a --file target and a terminal narrow enough to wrap what reaches the screen.
            // The screen must be a terminal for there to be anything the width applies to at all:
            // default output draws its table only where something can redraw, and a table is the
            // one thing NetPace writes that a narrow window folds.
            var narrowFile = Path.Join(Path.GetTempPath(), $"netpace-test-{Guid.NewGuid()}.txt");
            var unlimitedFile = Path.Join(Path.GetTempPath(), $"netpace-test-{Guid.NewGuid()}.txt");

            try
            {
                // When the same run is made at a narrow terminal and at a wide one
                var narrowOutput = await RunAtWidthAsync(CommandLineTestHost.NarrowTerminalWidth, ["--file", narrowFile], interactive: true);
                var wideOutput = await RunAtWidthAsync(CommandLineTestHost.InteractiveTerminalWidth, ["--file", unlimitedFile], interactive: true);

                // Then the table on screen wrapped, which is what leaves the file leg with something
                // to prove, and the file came out byte-identical either way: it never went through
                // the console the terminal's width applies to.
                Assert.NotEqual(wideOutput, narrowOutput);

                var narrowContent = await System.IO.File.ReadAllTextAsync(narrowFile);
                Assert.Equal(await System.IO.File.ReadAllTextAsync(unlimitedFile), narrowContent);

                await Verify(narrowContent);
            }
            finally
            {
                foreach (var file in new[] { narrowFile, unlimitedFile })
                {
                    if (System.IO.File.Exists(file))
                    {
                        System.IO.File.Delete(file);
                    }
                }
            }
        }

        /// <summary>
        /// A tester reporting the long-named server above. Fresh per run, like the host.
        /// </summary>
        private static ScriptedSpeedTester LongNamedServerTester() =>
            new() { Server = new Server { Location = LongLocation, Sponsor = LongSponsor, Url = LongUrl } };

        /// <summary>
        /// Runs NetPace at the given console width and returns what it wrote to stdout. A fresh host
        /// per run, because a host registers its console once. The destination is one that cannot
        /// redraw unless <paramref name="interactive"/> says otherwise.
        /// </summary>
        private static async Task<string> RunAtWidthAsync(int width, string[] args, ISpeedTestService? speedTestService = null, IWaiter? waiter = null, bool interactive = false, CancellationToken cancellationToken = default)
        {
            var services = new ServiceCollection();
            services.AddSingleton(speedTestService ?? new SpeedTestStub());
            services.AddSingleton<IClock, IncrementingClockStub>();
            services.AddSingleton(waiter ?? new NoDelayStub());

            var result = await new CommandLineTestHost(services, width, interactive).RunAsync(args, cancellationToken);

            Assert.Equal(0, result.ExitCode);
            return result.Output;
        }

        /// <summary>
        /// The lines of output, which for a machine-readable format means its records. Empty entries are
        /// dropped so that the trailing newline every writer emits is not counted as a record; none of
        /// the three machine-readable formats writes a blank line of its own, and the snapshots are what
        /// catch a stray one.
        /// </summary>
        private static string[] Lines(string output) =>
            output.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);

        /// <summary>
        /// Asserts the text is JSON a standard parser accepts. A line break landed inside a record
        /// leaves text no parser accepts, so the parse is the whole assertion and the document is
        /// discarded unread - what the output says is the snapshot's job.
        /// </summary>
        private static void AssertParsesAsJson(string json) =>
            JsonDocument.Parse(json).Dispose();

        private static void AssertEveryLineMatchesTheHeaderFieldCount(string[] lines)
        {
            var headerFieldCount = lines[0].Split(',').Length;

            Assert.All(lines, line => Assert.Equal(headerFieldCount, line.Split(',').Length));
        }
    }
}
