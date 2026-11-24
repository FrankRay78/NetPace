using System.Globalization;
using System.Text.Json;
using NetPace.Core;

namespace NetPace.Console.Tests;

/// <summary>
/// The bytes each direction moved, in the two machine-readable formats: whole numbers no unit
/// option reaches, under the same names, and no figure at all for a direction that did not run.
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

            // Given the same stubbed run written in both machine-readable formats.
            var csv = await GetCommandLineTestHost(StubServices()).RunAsync(["--csv"]);
            var json = await GetCommandLineTestHost(StubServices()).RunAsync(["--json"]);

            // Then the header names a column for each figure, the row carries the known numbers,
            // and they are the numbers JSON reports for the same stubbed run.
            Assert.Equal(0, csv.ExitCode);
            Assert.Equal(0, json.ExitCode);
            string[][] records = CsvOutput.Records(csv.Output);
            Assert.Equal(StubDownloadBytes.ToString(CultureInfo.InvariantCulture), CsvOutput.Field(records, "DownloadBytesProcessed"));
            Assert.Equal(StubUploadBytes.ToString(CultureInfo.InvariantCulture), CsvOutput.Field(records, "UploadBytesProcessed"));
            AssertBothCarry(csv.Output, json.Output, ["DownloadBytesProcessed", "UploadBytesProcessed"]);
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
            AssertBothLeaveOut(csv.Output, json.Output, ["UploadBytesProcessed"]);
            AssertBothCarry(csv.Output, json.Output, ["DownloadBytesProcessed"]);
        }

        [Fact]
        public async Task Should_Report_The_Same_Byte_Figures_Whatever_The_Unit_Options_Say()
        {
            // SCENARIO: The byte figures ignore the unit options

            // Given the same stubbed run with and without every unit option set.
            string[] unitOptions = ["--unit", "bytes", "--unit-system", "IEC", "--unit-scale", "Kilo"];
            var plainCsv = await GetCommandLineTestHost(StubServices()).RunAsync(["--csv"]);
            var unitCsv = await GetCommandLineTestHost(StubServices()).RunAsync(["--csv", .. unitOptions]);
            var plainJson = await GetCommandLineTestHost(StubServices()).RunAsync(["--json"]);
            var unitJson = await GetCommandLineTestHost(StubServices()).RunAsync(["--json", .. unitOptions]);

            // Then the figures are the same whole numbers in both formats, untouched by the options
            // that shape output written for a person.
            Assert.Equal(0, unitCsv.ExitCode);
            Assert.Equal(0, unitJson.ExitCode);
            string[][] plainRecords = CsvOutput.Records(plainCsv.Output);
            string[][] unitRecords = CsvOutput.Records(unitCsv.Output);
            using var plainDocument = JsonDocument.Parse(plainJson.Output);
            using var unitDocument = JsonDocument.Parse(unitJson.Output);
            string[] byteFields = ["DownloadBytesProcessed", "UploadBytesProcessed"];
            foreach (string field in byteFields)
            {
                Assert.Equal(CsvOutput.Field(plainRecords, field), CsvOutput.Field(unitRecords, field));
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Should_Report_The_Byte_Total_Its_Diagnostic_Records_Add_Up_To(bool failEveryUpload)
        {
            // Given a run whose diagnostic records account for every request it made, reported in
            // both machine-readable formats.
            var csv = await DiagnosticHost(failEveryUpload).RunAsync(["--csv", "--diagnostics"]);
            var json = await DiagnosticHost(failEveryUpload).RunAsync(["--json", "--diagnostics"]);

            // Then each direction's figure is the bytes total that direction's test.end record
            // carries, so the figure a log reads is the one the reported speed was built from.
            Assert.Equal(0, csv.ExitCode);
            Assert.Equal(0, json.ExitCode);
            string[][] records = CsvOutput.Records(csv.Output);
            using var document = JsonDocument.Parse(json.Output);
            (string test, string field)[] directions =
                [("download", "DownloadBytesProcessed"), ("upload", "UploadBytesProcessed")];
            foreach ((string test, string field) in directions)
            {
                string total = DiagnosticByteTotal(csv.DiagnosticOutput, test);
                Assert.Equal(total, CsvOutput.Field(records, field));
                Assert.Equal(total, document.RootElement.GetProperty(field).GetRawText());
            }
        }

        /// <summary>
        /// The <c>bytes</c> total the named test's <c>test.end</c> record carries, read by name so
        /// the field's place on the line does not matter.
        /// </summary>
        private static string DiagnosticByteTotal(string diagnosticOutput, string test)
        {
            string record = Assert.Single(
                diagnosticOutput.Split('\n'),
                candidate => candidate.Contains($"event=test.end test={test} ", StringComparison.Ordinal));

            return record
                .Split(' ')
                .Single(field => field.StartsWith("bytes=", StringComparison.Ordinal))
                .Split('=')[1]
                .Trim();
        }

        private static CommandLineTestHost DiagnosticHost(bool failEveryUpload = false)
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(new DiagnosticSpeedTester { FailEveryUpload = failEveryUpload });
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
        /// Asserts that each named field is missing in the shape its format uses: an empty CSV cell,
        /// an absent JSON field. Never a zero, which a direction that ran and received nothing has.
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
