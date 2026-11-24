using System.Globalization;
using System.Text.Json;
using NetPace.Core;

namespace NetPace.Console.Tests;

/// <summary>
/// The bytes each direction moved, in the two machine-readable formats. Both are whole numbers of
/// bytes that no unit option reaches, both formats carry them under the same names, and a direction
/// that did not run has no figure at all — an empty CSV cell and an absent JSON field.
/// </summary>
public sealed partial class NetPaceConsoleTests
{
    public sealed class BytesProcessed
    {
        /// <summary>
        /// What the stubbed download and upload move, so a test naming a figure names the one it
        /// expects rather than whatever came back.
        /// </summary>
        private const long StubDownloadBytes = 1000;
        private const long StubUploadBytes = 7000;

        private static readonly string[] DownloadField = ["DownloadBytesProcessed"];
        private static readonly string[] UploadField = ["UploadBytesProcessed"];

        [Fact]
        public async Task Should_Report_The_Bytes_Each_Direction_Moved_In_Json()
        {
            // SCENARIO: A completed run reports the bytes it moved in JSON

            // Given a run whose download received a known number of bytes and whose upload sent a
            // known number.
            var result = await GetCommandLineTestHost(StubServices()).RunAsync(["--json"]);

            // Then the result carries both numbers exactly.
            Assert.Equal(0, result.ExitCode);
            using var document = JsonDocument.Parse(result.Output);
            Assert.Equal(StubDownloadBytes, document.RootElement.GetProperty("DownloadBytesProcessed").GetInt64());
            Assert.Equal(StubUploadBytes, document.RootElement.GetProperty("UploadBytesProcessed").GetInt64());
        }

        [Fact]
        public async Task Should_Report_The_Bytes_Each_Direction_Moved_In_Csv()
        {
            // SCENARIO: A completed run reports the bytes it moved in CSV

            // Given the same run written in both machine-readable formats.
            var csv = await GetCommandLineTestHost(StubServices()).RunAsync(["--csv"]);
            var json = await GetCommandLineTestHost(StubServices()).RunAsync(["--json"]);

            // Then the header names a column for each figure, the row carries the known numbers,
            // and they are the numbers JSON reports for the same run.
            Assert.Equal(0, csv.ExitCode);
            Assert.Equal(0, json.ExitCode);
            string[][] records = CsvOutput.Records(csv.Output);
            Assert.Equal(StubDownloadBytes.ToString(CultureInfo.InvariantCulture), CsvOutput.Field(records, "DownloadBytesProcessed"));
            Assert.Equal(StubUploadBytes.ToString(CultureInfo.InvariantCulture), CsvOutput.Field(records, "UploadBytesProcessed"));
            AssertBothCarry(csv.Output, json.Output, [.. DownloadField, .. UploadField]);
        }

        [Fact]
        public async Task Should_Leave_Out_The_Byte_Figure_Of_A_Direction_That_Did_Not_Run()
        {
            // SCENARIO: A skipped direction has no byte figure

            // Given a run with no upload test.
            var csv = await GetCommandLineTestHost(StubServices()).RunAsync(["--csv", "--no-upload"]);
            var json = await GetCommandLineTestHost(StubServices()).RunAsync(["--json", "--no-upload"]);

            // Then the upload figure is an empty cell and an absent field, while the download
            // figure is still reported.
            Assert.Equal(0, csv.ExitCode);
            Assert.Equal(0, json.ExitCode);
            AssertBothLeaveOut(csv.Output, json.Output, UploadField);
            AssertBothCarry(csv.Output, json.Output, DownloadField);
        }

        [Fact]
        public async Task Should_Report_The_Same_Byte_Figures_Whatever_The_Unit_Options_Say()
        {
            // SCENARIO: The byte figures ignore the unit options

            // Given the same run with and without every unit option set.
            string[] unitOptions = ["--unit", "bytes", "--unit-system", "IEC", "--unit-scale", "Kilo"];
            var plainCsv = await GetCommandLineTestHost(StubServices()).RunAsync(["--csv"]);
            var unitCsv = await GetCommandLineTestHost(StubServices()).RunAsync(["--csv", .. unitOptions]);
            var plainJson = await GetCommandLineTestHost(StubServices()).RunAsync(["--json"]);
            var unitJson = await GetCommandLineTestHost(StubServices()).RunAsync(["--json", .. unitOptions]);

            // Then the figures are the same whole numbers in both formats, untouched by the options
            // that shape output written for a person.
            Assert.Equal(0, unitCsv.ExitCode);
            Assert.Equal(0, unitJson.ExitCode);
            foreach (string field in new[] { "DownloadBytesProcessed", "UploadBytesProcessed" })
            {
                Assert.Equal(
                    CsvOutput.Field(CsvOutput.Records(plainCsv.Output), field),
                    CsvOutput.Field(CsvOutput.Records(unitCsv.Output), field));
                using var plainDocument = JsonDocument.Parse(plainJson.Output);
                using var unitDocument = JsonDocument.Parse(unitJson.Output);
                Assert.Equal(
                    plainDocument.RootElement.GetProperty(field).GetRawText(),
                    unitDocument.RootElement.GetProperty(field).GetRawText());
            }
        }

        [Fact]
        public async Task Should_Report_What_A_Direction_Moved_When_Every_Request_Failed()
        {
            // Given a run whose every upload request failed after moving something, and whose every
            // download request failed having moved nothing.
            var csv = await GetCommandLineTestHost(AllFailedServices()).RunAsync(["--csv"]);
            var json = await GetCommandLineTestHost(AllFailedServices()).RunAsync(["--json"]);

            // Then each direction still reports what it moved, which is 0 only where nothing
            // arrived — not an absent figure, which would say the test did not run.
            string[][] records = CsvOutput.Records(csv.Output);
            using var document = JsonDocument.Parse(json.Output);
            Assert.Equal("0", CsvOutput.Field(records, "DownloadBytesProcessed"));
            Assert.Equal(0, document.RootElement.GetProperty("DownloadBytesProcessed").GetInt64());
            Assert.Equal("1000", CsvOutput.Field(records, "UploadBytesProcessed"));
            Assert.Equal(1000, document.RootElement.GetProperty("UploadBytesProcessed").GetInt64());
        }

        [Fact]
        public async Task Should_Report_The_Byte_Total_Its_Diagnostic_Records_Add_Up_To()
        {
            // Given a run whose diagnostic records account for every request it made, reported in
            // both machine-readable formats.
            var csv = await DiagnosticHost().RunAsync(["--csv", "--diagnostics"]);
            var json = await DiagnosticHost().RunAsync(["--json", "--diagnostics"]);

            // Then each direction's figure is the bytes total that direction's test.end record
            // carries, so the figure a log reads is the one the reported speed was built from.
            Assert.Equal(0, csv.ExitCode);
            Assert.Equal(0, json.ExitCode);
            string[][] records = CsvOutput.Records(csv.Output);
            using var document = JsonDocument.Parse(json.Output);
            foreach ((string test, string field) in new[] { ("download", "DownloadBytesProcessed"), ("upload", "UploadBytesProcessed") })
            {
                string total = DiagnosticByteTotal(csv.DiagnosticOutput, test);
                Assert.Equal(total, CsvOutput.Field(records, field));
                Assert.Equal(total, document.RootElement.GetProperty(field).GetRawText());
            }
        }

        /// <summary>
        /// The <c>bytes</c> total the named test's <c>test.end</c> record carries, read out of the
        /// diagnostic stream the way a person reading it would.
        /// </summary>
        private static string DiagnosticByteTotal(string diagnosticOutput, string test)
        {
            string[] line = [.. diagnosticOutput
                .Split('\n')
                .Where(candidate => candidate.Contains("event=test.end", StringComparison.Ordinal)
                    && candidate.Contains($"test={test} ", StringComparison.Ordinal))];

            string record = Assert.Single(line);
            return record.Split("bytes=", StringSplitOptions.None)[1].Trim();
        }

        private static CommandLineTestHost DiagnosticHost()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(new DiagnosticSpeedTester());
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            return GetCommandLineTestHost(services);
        }

        /// <summary>
        /// A run where every request of both directions failed: the download moved nothing before
        /// it did, the upload moved something — the two shapes a failed transfer can leave behind.
        /// </summary>
        private static ServiceCollection AllFailedServices()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(new ScriptedSpeedTester
            {
                DownloadFactory = _ => ScriptedSpeedTester.AllFailed(150),
                UploadFactory = _ => ScriptedSpeedTester.AllFailedWithBytes(32)
            });
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            return services;
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
        /// column and leaves the cell empty, JSON leaves the field out. Never a zero, which is what
        /// a direction that ran and received nothing reports.
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
    }
}
