using System.Text.Json;

namespace NetPace.Console.Tests;

/// <summary>
/// CSV and JSON carry the same fields under the same names: the server each result was measured
/// against, the machine that produced it, and whatever else the user asked to record. Each format
/// keeps its own shape for what is missing — CSV always has every built-in column and leaves a cell
/// empty, JSON leaves a built-in field out.
/// </summary>
public sealed partial class NetPaceConsoleTests
{
    public sealed class ResultFields
    {
        /// <summary>
        /// The ten columns CSV carried before the server and device fields were added, in the order
        /// it carried them. A script reading by name or by position must be unaffected, so these
        /// stay at the front of the header.
        /// </summary>
        private static readonly string[] OriginalCsvColumns =
        [
            "Timestamp",
            "LatencyMilliseconds",
            "DownloadSpeedBitsPerSecond",
            "DownloadSucceeded",
            "DownloadFailed",
            "UploadSpeedBitsPerSecond",
            "UploadSucceeded",
            "UploadFailed",
            "IPAddress",
            "Hostname"
        ];

        [Fact]
        public async Task Should_Report_The_Same_Server_In_Csv_As_In_Json_For_The_Same_Run()
        {
            // SCENARIO: CSV reports the server each result used

            // Given the same speed test reported in both machine-readable formats.
            var csv = await GetCommandLineTestHost(StubServices()).RunAsync(["--csv"]);
            var json = await GetCommandLineTestHost(StubServices()).RunAsync(["--json"]);

            // Then the CSV row names the server under the same column names JSON uses, carrying the
            // same values.
            Assert.Equal(0, csv.ExitCode);
            Assert.Equal(0, json.ExitCode);
            string[][] records = CsvOutput.Records(csv.Output);
            using var document = JsonDocument.Parse(json.Output);
            string[] serverFields = ["ServerLocation", "ServerSponsor", "ServerUrl"];
            foreach (string field in serverFields)
            {
                Assert.Equal(document.RootElement.GetProperty(field).GetString(), CsvOutput.Field(records, field));
            }
        }

        [Fact]
        public async Task Should_Keep_The_Original_Csv_Columns_At_The_Front_Of_The_Header()
        {
            // Given a run in CSV.
            var result = await GetCommandLineTestHost(StubServices()).RunAsync(["--csv"]);

            // Then the ten original columns still open the header, in their original order, and the
            // new built-in columns follow Hostname.
            Assert.Equal(0, result.ExitCode);
            string[][] records = CsvOutput.Records(result.Output);
            Assert.Equal(OriginalCsvColumns, records[0].Take(OriginalCsvColumns.Length).ToArray());
            Assert.Equal<string[]>(
                ["ServerLocation", "ServerSponsor", "ServerUrl", "DeviceId"],
                [.. records[0].Skip(OriginalCsvColumns.Length)]);
        }

        [Fact]
        public async Task Should_Report_The_Device_Label_In_Csv_And_In_Json()
        {
            // SCENARIO: Device label appears in CSV and JSON

            // Given a label for this machine.
            const string label = "kitchen-pi";

            // When one run writes CSV and another writes JSON.
            var csv = await GetCommandLineTestHost(StubServices()).RunAsync(["--csv", "--device-id", label]);
            var json = await GetCommandLineTestHost(StubServices()).RunAsync(["--json", "--device-id", label]);

            // Then both carry the label as given.
            Assert.Equal(0, csv.ExitCode);
            Assert.Equal(0, json.ExitCode);
            Assert.Equal(label, CsvOutput.Field(CsvOutput.Records(csv.Output), "DeviceId"));
            using var document = JsonDocument.Parse(json.Output);
            Assert.Equal(label, document.RootElement.GetProperty("DeviceId").GetString());
        }

        [Fact]
        public async Task Should_Keep_The_Csv_Device_Column_When_No_Label_Is_Given()
        {
            // SCENARIO: CSV keeps the device column when no label is given

            // Given a CSV run with no device label.
            var result = await GetCommandLineTestHost(StubServices()).RunAsync(["--csv"]);

            // Then the column is still there and its cell is empty, so a script reading by position
            // is not moved along by a label nobody gave.
            Assert.Equal(0, result.ExitCode);
            string[][] records = CsvOutput.Records(result.Output);
            Assert.Equal(records[0].Length, records[1].Length);
            Assert.Equal(string.Empty, CsvOutput.Field(records, "DeviceId"));
        }

        [Fact]
        public async Task Should_Omit_The_Json_Device_Field_When_No_Label_Is_Given()
        {
            // Given a JSON run with no device label.
            var result = await GetCommandLineTestHost(StubServices()).RunAsync(["--json"]);

            // Then the field is absent rather than empty, the shape JSON uses for what does not
            // apply.
            Assert.Equal(0, result.ExitCode);
            using var document = JsonDocument.Parse(result.Output);
            Assert.False(document.RootElement.TryGetProperty("DeviceId", out _));
        }

        [Fact]
        public async Task Should_Record_Custom_Fields_As_Trailing_Csv_Columns_In_Command_Line_Order()
        {
            // SCENARIO: Custom fields are recorded in CSV in command-line order

            // Given two custom fields.
            var result = await GetCommandLineTestHost(StubServices()).RunAsync(
                ["--csv", "--custom-field", "site=home", "--custom-field", "plan=500M"]);

            // Then the header ends with the built-in columns followed by the two keys in the order
            // they were given, and the row ends with their values.
            Assert.Equal(0, result.ExitCode);
            string[][] records = CsvOutput.Records(result.Output);
            Assert.Equal<string[]>(["site", "plan"], [.. records[0].TakeLast(2)]);
            Assert.Equal<string[]>(["home", "500M"], [.. records[1].TakeLast(2)]);

            // And the built-in columns before them are untouched.
            var builtIn = await GetCommandLineTestHost(StubServices()).RunAsync(["--csv"]);
            string[][] builtInRecords = CsvOutput.Records(builtIn.Output);
            Assert.Equal(builtInRecords[0], records[0].Take(builtInRecords[0].Length));
            Assert.Equal(builtInRecords[1], records[1].Take(builtInRecords[1].Length));
        }

        [Fact]
        public async Task Should_Group_Custom_Fields_In_One_Json_Object()
        {
            // SCENARIO: Custom fields are grouped in JSON

            // Given two custom fields.
            var result = await GetCommandLineTestHost(StubServices()).RunAsync(
                ["--json", "--custom-field", "site=home", "--custom-field", "plan=500M"]);

            // Then they are grouped in one object, holding both pairs as given.
            Assert.Equal(0, result.ExitCode);
            using var document = JsonDocument.Parse(result.Output);
            var customFields = document.RootElement.GetProperty("CustomFields");
            Assert.Equal("home", customFields.GetProperty("site").GetString());
            Assert.Equal("500M", customFields.GetProperty("plan").GetString());

            // And every built-in field reads exactly as it does without them.
            var builtIn = await GetCommandLineTestHost(StubServices()).RunAsync(["--json"]);
            using var builtInDocument = JsonDocument.Parse(builtIn.Output);
            foreach (var property in builtInDocument.RootElement.EnumerateObject())
            {
                Assert.Equal(property.Value.GetRawText(), document.RootElement.GetProperty(property.Name).GetRawText());
            }
        }

        [Fact]
        public async Task Should_Omit_The_Json_Custom_Fields_Object_When_None_Are_Given()
        {
            // Given a JSON run with no custom fields.
            var result = await GetCommandLineTestHost(StubServices()).RunAsync(["--json"]);

            // Then the object is left out entirely rather than written empty.
            Assert.Equal(0, result.ExitCode);
            using var document = JsonDocument.Parse(result.Output);
            Assert.False(document.RootElement.TryGetProperty("CustomFields", out _));
        }

        [Fact]
        public async Task Should_Not_Let_A_Custom_Key_Replace_A_Built_In_Json_Field()
        {
            // SCENARIO: A custom key named like a built-in field does not replace it in JSON

            // Given a custom key named after a built-in field.
            var services = StubServices();
            services.AddSingleton<IClientInfoProvider>(new ClientInfoProviderStub { Hostname = "test-host" });
            var result = await GetCommandLineTestHost(services).RunAsync(
                ["--json", "--custom-field", "Hostname=label"]);

            // Then the built-in field still holds the machine's hostname, and the custom pair sits
            // in its own object beside it.
            Assert.Equal(0, result.ExitCode);
            using var document = JsonDocument.Parse(result.Output);
            Assert.Equal("test-host", document.RootElement.GetProperty("Hostname").GetString());
            Assert.Equal("label", document.RootElement.GetProperty("CustomFields").GetProperty("Hostname").GetString());
        }

        [Fact]
        public async Task Should_Write_A_Second_Csv_Column_For_A_Custom_Key_Named_Like_A_Built_In()
        {
            // Given a custom key named after a built-in column. NetPace does not police the keys, so
            // CSV writes it as given — a second column of that name.
            var services = StubServices();
            services.AddSingleton<IClientInfoProvider>(new ClientInfoProviderStub { Hostname = "test-host" });
            var result = await GetCommandLineTestHost(services).RunAsync(
                ["--csv", "--custom-field", "Hostname=label"]);

            // Then the built-in column still holds the machine's hostname and the custom column
            // follows it.
            Assert.Equal(0, result.ExitCode);
            string[][] records = CsvOutput.Records(result.Output);
            Assert.Equal(2, records[0].Count(column => column == "Hostname"));
            Assert.Equal("test-host", CsvOutput.Field(records, "Hostname"));
            Assert.Equal("Hostname", records[0][^1]);
            Assert.Equal("label", records[1][^1]);
        }

        [Theory]
        [InlineData("site")]
        [InlineData("=home")]
        [InlineData("")]
        public async Task Should_Reject_A_Custom_Field_That_Is_Not_A_Key_Value_Pair_Without_Measuring(string argument)
        {
            // SCENARIO: A custom field without a key and value is rejected

            // Given a speed tester that records the one call a run makes before measuring.
            var (host, serviceCalls) = HostRecordingMeasurements();

            // When a custom field arrives with no key and value to read.
            var result = await host.RunAsync(["--csv", "--custom-field", argument]);

            // Then no test ran, the exit code reports the usage error, and the message names the
            // form expected so the user is not left with a bare failure.
            Assert.Empty(serviceCalls);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("key=value", result.Output);
        }

        [Theory]
        [InlineData("--csv")]
        [InlineData("--json")]
        [InlineData("--minimal")]
        [InlineData("--timestamp")]
        public async Task Should_Reject_A_Malformed_Custom_Field_Whichever_Output_Mode_Is_Chosen(string modeSwitch)
        {
            // Given a run in each output mode. --timestamp stands for the default output, which
            // ignores custom fields altogether.
            var (host, serviceCalls) = HostRecordingMeasurements();

            // When a malformed custom field is passed.
            var result = await host.RunAsync([modeSwitch, "--custom-field", "site"]);

            // Then it is refused before anything is measured, whichever format was asked for: a
            // mistyped field in a script fails loudly rather than being silently dropped.
            Assert.Empty(serviceCalls);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("key=value", result.Output);
        }

        [Theory]
        [InlineData("expr=a=b", "expr", "a=b")]
        [InlineData("note=", "note", "")]
        [InlineData("plan=500M/mo, unlimited", "plan", "500M/mo, unlimited")]
        public async Task Should_Take_A_Custom_Field_Value_As_Everything_After_The_First_Equals(string argument, string key, string value)
        {
            // Given a custom field whose value itself contains an equals sign, is empty, or carries a
            // comma.
            var result = await GetCommandLineTestHost(StubServices()).RunAsync(
                ["--json", "--custom-field", argument]);

            // Then the key is what precedes the first equals and the value is everything after it.
            Assert.Equal(0, result.ExitCode);
            using var document = JsonDocument.Parse(result.Output);
            Assert.Equal(value, document.RootElement.GetProperty("CustomFields").GetProperty(key).GetString());
        }

        [Fact]
        public async Task Should_Let_The_Last_Value_Win_For_A_Repeated_Custom_Key_In_Json()
        {
            // Given the same key given twice. A JSON object cannot hold it twice, so the last value
            // given wins.
            var result = await GetCommandLineTestHost(StubServices()).RunAsync(
                ["--json", "--custom-field", "site=home", "--custom-field", "site=office"]);

            // Then one pair is written, carrying the last value.
            Assert.Equal(0, result.ExitCode);
            using var document = JsonDocument.Parse(result.Output);
            var customFields = document.RootElement.GetProperty("CustomFields");
            Assert.Single(customFields.EnumerateObject());
            Assert.Equal("office", customFields.GetProperty("site").GetString());
        }

        [Fact]
        public async Task Should_Write_Every_Repeated_Custom_Key_As_Its_Own_Csv_Column()
        {
            // Given the same key given twice. A CSV header can hold it twice, so both columns are
            // written as given.
            var result = await GetCommandLineTestHost(StubServices()).RunAsync(
                ["--csv", "--custom-field", "site=home", "--custom-field", "site=office"]);

            // Then both columns appear, each with the value given beside it.
            Assert.Equal(0, result.ExitCode);
            string[][] records = CsvOutput.Records(result.Output);
            Assert.Equal<string[]>(["site", "site"], [.. records[0].TakeLast(2)]);
            Assert.Equal<string[]>(["home", "office"], [.. records[1].TakeLast(2)]);
        }

        [Theory]
        [InlineData("--timestamp")]
        [InlineData("--minimal")]
        public async Task Should_Leave_The_Default_And_Minimal_Output_Untouched(string modeSwitch)
        {
            // Given the same speed test run twice, once plainly and once with a device label and a
            // custom field. Both describe the run for a log, not the measurement, so neither output
            // written for a person to read shows them. --timestamp stands for the default output.
            var plain = await GetCommandLineTestHost(StubServices()).RunAsync([modeSwitch]);
            var labelled = await GetCommandLineTestHost(StubServices()).RunAsync(
                [modeSwitch, "--device-id", "kitchen-pi", "--custom-field", "site=home"]);

            // Then both runs are accepted and the two outputs are the same.
            Assert.Equal(0, plain.ExitCode);
            Assert.Equal(0, labelled.ExitCode);
            Assert.Equal(plain.Output, labelled.Output);
        }

        /// <summary>
        /// The registrations a result-fields test needs: a stub speed tester, a fixed clock, and a
        /// waiter that returns immediately. A test that needs more adds it to the returned
        /// collection.
        /// </summary>
        private static ServiceCollection StubServices()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            return services;
        }
    }
}
