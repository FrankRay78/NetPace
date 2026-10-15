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

            using var parsed = JsonDocument.Parse(lines[0]);
            Assert.Equal("Test Sponsor 1", parsed.RootElement.GetProperty("ServerSponsor").GetString());
            Assert.Equal("8 Kbps", parsed.RootElement.GetProperty("DownloadSpeed").GetString());

            Assert.Equal(await RunAtWidthAsync(CommandLineTestHost.UnlimitedWidth, ["--json"]), output);
            await Verify(output);
        }

        [Fact]
        public async Task Should_Write_Json_Pretty_Without_Splitting_Long_Values_When_Stdout_Is_Redirected()
        {
            // Given stdout is redirected, and a server whose name and URL each pass 80 columns
            var service = new ScriptedSpeedTester
            {
                Server = new Server { Location = LongLocation, Sponsor = LongSponsor, Url = LongUrl }
            };

            // When
            var output = await RunAtWidthAsync(CommandLineTestHost.RedirectedWidth, ["--json-pretty"], service);

            // Then the indentation --json-pretty asks for is the only thing breaking lines, and every
            // value survives whole.
            using var parsed = JsonDocument.Parse(output);
            Assert.Equal(LongSponsor, parsed.RootElement.GetProperty("ServerSponsor").GetString());
            Assert.Equal(LongLocation, parsed.RootElement.GetProperty("ServerLocation").GetString());
            Assert.Equal(LongUrl, parsed.RootElement.GetProperty("ServerUrl").GetString());

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
        public async Task Should_Write_Minimal_On_One_Line_When_Stdout_Is_Redirected()
        {
            // Given stdout is redirected, and a result long enough to pass 80 columns
            var service = new ScriptedSpeedTester { DownloadFactory = _ => ScriptedSpeedTester.Partial(150, 5) };

            // When
            var output = await RunAtWidthAsync(CommandLineTestHost.RedirectedWidth, ["--minimal", "--timestamp"], service);

            // Then
            Assert.Single(Lines(output));
            await Verify(output);
        }

        // SCENARIO: Machine-readable output is unbroken in a narrow terminal
        [InlineData("--json")]
        [InlineData("--json-pretty")]
        [InlineData("--csv")]
        [InlineData("--minimal")]
        [Theory]
        public async Task Should_Write_Machine_Readable_Output_Unbroken_In_A_Narrow_Terminal(string formatSwitch)
        {
            // Given an interactive terminal narrower than the output line
            var server = new Server { Location = LongLocation, Sponsor = LongSponsor, Url = LongUrl };

            // When
            var narrow = await RunAtWidthAsync(CommandLineTestHost.NarrowTerminalWidth, [formatSwitch], new ScriptedSpeedTester { Server = server });

            // Then nothing the window's width did shows in the output: it is what the same run writes
            // with no width limit at all, break for break.
            var unlimited = await RunAtWidthAsync(CommandLineTestHost.UnlimitedWidth, [formatSwitch], new ScriptedSpeedTester { Server = server });
            Assert.Equal(unlimited, narrow);

            await Verify(narrow).UseParameters(formatSwitch);
        }

        [Fact]
        public async Task Should_Leave_File_Output_Unchanged_When_Stdout_Is_Redirected()
        {
            // Given stdout is redirected, and a --file target
            var testFile = Path.Join(Path.GetTempPath(), $"netpace-test-{Guid.NewGuid()}.txt");

            try
            {
                // When
                await RunAtWidthAsync(CommandLineTestHost.RedirectedWidth, ["--csv", "--file", testFile]);

                // Then the file holds what it has always held - it never went through the console the
                // terminal's width applies to.
                var fileContent = await System.IO.File.ReadAllTextAsync(testFile);
                await Verify(fileContent);
            }
            finally
            {
                if (System.IO.File.Exists(testFile))
                {
                    System.IO.File.Delete(testFile);
                }
            }
        }

        /// <summary>
        /// Runs NetPace at the given console width and returns what it wrote to stdout. A fresh host
        /// per run, because a host registers its console once.
        /// </summary>
        private static async Task<string> RunAtWidthAsync(int width, string[] args, ISpeedTestService? speedTestService = null, IWaiter? waiter = null, CancellationToken cancellationToken = default)
        {
            var services = new ServiceCollection();
            services.AddSingleton(speedTestService ?? new SpeedTestStub());
            services.AddSingleton<IClock, IncrementingClockStub>();
            services.AddSingleton(waiter ?? new NoDelayStub());

            var result = await new CommandLineTestHost(services, width).RunAsync(args, cancellationToken);

            Assert.Equal(0, result.ExitCode);
            return result.Output;
        }

        /// <summary>
        /// The lines of output, which for a machine-readable format means its records: none of the four
        /// writes a blank line, so a break that produced one came from the console.
        /// </summary>
        private static string[] Lines(string output) =>
            output.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);

        private static void AssertEveryLineMatchesTheHeaderFieldCount(string[] lines)
        {
            var headerFieldCount = lines[0].Split(',').Length;

            Assert.All(lines, line => Assert.Equal(headerFieldCount, line.Split(',').Length));
        }
    }
}
