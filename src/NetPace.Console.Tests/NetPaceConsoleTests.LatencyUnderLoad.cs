using System.Text.Json;
using NetPace.Core;
using NetPace.Core.Clients.Ookla;

namespace NetPace.Console.Tests;

/// <summary>
/// Latency under load, and idle jitter, in the two machine-readable formats. Both are plain numbers
/// of milliseconds that no unit option reaches, both formats carry them under the same names, and a
/// figure that was not measured is an empty CSV cell and an absent JSON field — never a zero a
/// chart would read as a perfect line.
/// </summary>
public sealed partial class NetPaceConsoleTests
{
    public sealed class LatencyUnderLoad
    {
        /// <summary>
        /// Idle jitter, which belongs beside the idle latency already reported.
        /// </summary>
        private static readonly string[] IdleFields = ["JitterMilliseconds"];

        /// <summary>
        /// The four figures the download reports about the line while it was loading it.
        /// </summary>
        private static readonly string[] DownloadFields =
        [
            "DownloadLatencyMilliseconds",
            "DownloadLowestLatencyMilliseconds",
            "DownloadHighestLatencyMilliseconds",
            "DownloadJitterMilliseconds"
        ];

        /// <summary>
        /// The upload's four, mirroring the download's word for word.
        /// </summary>
        private static readonly string[] UploadFields =
        [
            "UploadLatencyMilliseconds",
            "UploadLowestLatencyMilliseconds",
            "UploadHighestLatencyMilliseconds",
            "UploadJitterMilliseconds"
        ];

        /// <summary>
        /// All nine, for a test that does not care which measurement a figure came from.
        /// </summary>
        private static readonly string[] AllFields = [.. IdleFields, .. DownloadFields, .. UploadFields];

        [Fact]
        public async Task Should_Report_Latency_Under_Load_In_Csv_And_In_Json()
        {
            // SCENARIO: CSV and JSON report latency under load

            // Given a run whose download and upload both report under-load figures, written once in
            // each machine-readable format.
            var csv = await GetCommandLineTestHost(StubServices()).RunAsync(["--csv"]);
            var json = await GetCommandLineTestHost(StubServices()).RunAsync(["--json"]);

            // Then both carry all nine figures, under the same names and as the same numbers.
            Assert.Equal(0, csv.ExitCode);
            Assert.Equal(0, json.ExitCode);
            AssertBothCarry(csv.Output, json.Output, AllFields);
        }

        [Fact]
        public async Task Should_Leave_Out_The_Figures_Of_A_Direction_That_Measured_None()
        {
            // SCENARIO: A direction not measured is left out

            // Given a run whose download reports under-load latency and whose upload reports none.
            var csv = await GetCommandLineTestHost(PartlyLoadedServices()).RunAsync(["--csv"]);
            var json = await GetCommandLineTestHost(PartlyLoadedServices()).RunAsync(["--json"]);

            // Then the upload's four are an empty cell and an absent field, and the download's four
            // are still reported.
            Assert.Equal(0, csv.ExitCode);
            Assert.Equal(0, json.ExitCode);
            AssertBothLeaveOut(csv.Output, json.Output, UploadFields);
            AssertBothCarry(csv.Output, json.Output, DownloadFields);
        }

        [Fact]
        public async Task Should_Leave_Out_The_Figures_Of_A_Test_That_Did_Not_Run()
        {
            // SCENARIO: A skipped test leaves its fields out

            // Given a run with no download test.
            var csv = await GetCommandLineTestHost(StubServices()).RunAsync(["--csv", "--no-download"]);
            var json = await GetCommandLineTestHost(StubServices()).RunAsync(["--json", "--no-download"]);

            // Then the download's four are an empty cell and an absent field, and the upload's four
            // are still reported.
            Assert.Equal(0, csv.ExitCode);
            Assert.Equal(0, json.ExitCode);
            AssertBothLeaveOut(csv.Output, json.Output, DownloadFields);
            AssertBothCarry(csv.Output, json.Output, UploadFields);
        }

        [Fact]
        public async Task Should_Name_The_New_Columns_In_The_Csv_Header_With_A_Cell_In_Every_Row()
        {
            // SCENARIO: The CSV header lists the new columns

            // Given a log of three runs in CSV.
            var result = await GetCommandLineTestHost(StubServices()).RunAsync(["--csv", "--count", "3"]);

            // Then the header names all nine columns, and every row lines up with it, so a reader
            // finds a cell for each of them in every row.
            Assert.Equal(0, result.ExitCode);
            string[][] records = CsvOutput.Records(result.Output);
            Assert.Equal(4, records.Length);
            foreach (string field in AllFields)
            {
                Assert.Contains(field, records[0]);
            }
            foreach (string[] row in records.Skip(1))
            {
                Assert.Equal(records[0].Length, row.Length);
            }
        }

        [Fact]
        public async Task Should_Leave_Out_Idle_Jitter_When_Latency_Was_Not_Measured()
        {
            // SCENARIO: Idle jitter is left out when latency is not measured

            // Given a run with no latency measurement.
            var csv = await GetCommandLineTestHost(StubServices()).RunAsync(["--csv", "--no-latency"]);
            var json = await GetCommandLineTestHost(StubServices()).RunAsync(["--json", "--no-latency"]);

            // Then idle jitter is an empty cell and an absent field, exactly as the idle latency
            // beside it already is.
            Assert.Equal(0, csv.ExitCode);
            Assert.Equal(0, json.ExitCode);
            AssertBothLeaveOut(csv.Output, json.Output, [.. IdleFields, "LatencyMilliseconds"]);
        }

        /// <summary>
        /// Asserts that each named field is a number in JSON and the same number in the CSV cell of
        /// that name, so the two formats cannot drift apart on a figure.
        /// </summary>
        private static void AssertBothCarry(string csvOutput, string jsonOutput, string[] fields)
        {
            string[][] records = CsvOutput.Records(csvOutput);
            using var document = JsonDocument.Parse(jsonOutput);

            foreach (string field in fields)
            {
                Assert.True(
                    document.RootElement.TryGetProperty(field, out JsonElement value),
                    $"JSON does not carry a '{field}' field: {jsonOutput}");
                Assert.Equal(JsonValueKind.Number, value.ValueKind);
                Assert.Equal(value.GetRawText(), CsvOutput.Field(records, field));
            }
        }

        /// <summary>
        /// Asserts that each named field is missing in the shape its format uses: CSV keeps the
        /// column and leaves the cell empty, JSON leaves the field out. Never a zero.
        /// </summary>
        private static void AssertBothLeaveOut(string csvOutput, string jsonOutput, string[] fields)
        {
            string[][] records = CsvOutput.Records(csvOutput);
            using var document = JsonDocument.Parse(jsonOutput);

            foreach (string field in fields)
            {
                Assert.Equal(string.Empty, CsvOutput.Field(records, field));
                Assert.False(
                    document.RootElement.TryGetProperty(field, out _),
                    $"JSON carries a '{field}' field it should have left out: {jsonOutput}");
            }
        }

        /// <summary>
        /// A run whose download measured the line under its own load and whose upload did not, so
        /// one direction has figures to report and the other has none.
        /// </summary>
        private static ServiceCollection PartlyLoadedServices()
        {
            var loadedDownload = ScriptedSpeedTester.Clean(150) with
            {
                Latency = new LatencyTestResult
                {
                    Server = new Server { Location = "Frankfurt", Sponsor = "Deutsche Telekom", Url = ScriptedSpeedTester.Url },
                    LatencyMilliseconds = 90,
                    MinimumLatencyMilliseconds = 70,
                    MaximumLatencyMilliseconds = 140,
                    JitterMilliseconds = 12
                }
            };

            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(new ScriptedSpeedTester
            {
                DownloadFactory = _ => loadedDownload,
                UploadFactory = _ => ScriptedSpeedTester.Clean(32)
            });
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            return services;
        }
    }
}
