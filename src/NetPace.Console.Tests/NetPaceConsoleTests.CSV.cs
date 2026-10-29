using System.Text.Json;
using Microsoft.VisualBasic.FileIO;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    public sealed class CSV
    {
        [Fact]
        public async Task Should_Perform_Speed_Test_With_CSV_Continuously()
        {
            // Given
            var cancellationTokenSource = new CancellationTokenSource();
            var waiter = new SelfCancellingWaiter(10, cancellationTokenSource);

            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, IncrementingClockStub>();
            services.AddSingleton<IWaiter>(waiter);
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--csv", "--loop"], cancellationTokenSource.Token);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [InlineData(5)]
        [Theory]
        public async Task Should_Perform_Speed_Test_With_CSV_Multiple_Times(int count)
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, IncrementingClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--csv", "--count", $"{count}"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(count);
        }

        [InlineData(10, "00:10:00")]
        [Theory]
        public async Task Should_Perform_Speed_Test_With_CSV_Multiple_Times_With_Delay(int count, string delay)
        {
            // Given
            var waiter = new NoDelayStub();

            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, IncrementingClockStub>();
            services.AddSingleton<IWaiter>(waiter);
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--csv", "--count", $"{count}", "--delay", $"{delay}"]);

            // Then
            Assert.Equal(count - 1, waiter.CallCount);
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(count, delay);
        }

        [InlineData("Auto")]
        [InlineData("Base")]
        [InlineData("Mega")]
        [Theory]
        public async Task Should_Perform_Speed_Test_With_CSV_Multiple_Times_Under_Every_Unit_Scale(string scale)
        {
            // Given a tester whose speed changes between runs, so an automatic scale would have
            // reported different units from one row to the next.
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, VariableSpeedTester>();
            services.AddSingleton<IClock, IncrementingClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--csv", "--count", "3", "--unit-scale", scale]);

            // Then the run is accepted and every row carries bare numbers in the same fixed units.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(scale);
        }

        [Fact]
        public async Task Should_Perform_Speed_Test_With_CSV_No_Download()
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--csv", "--no-download"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Perform_Speed_Test_With_CSV_No_Upload()
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--csv", "--no-upload"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Perform_Speed_Test_With_CSV_No_Latency()
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--csv", "--no-latency"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Include_Empty_IPAddress_And_Hostname_In_CSV_When_Device_Identity_Unavailable()
        {
            // SCENARIO: CSV IPAddress and Hostname columns contain empty values when device identity is unavailable

            // Given
            var services = StubServices();
            services.AddSingleton<IClientInfoProvider>(new ClientInfoProviderStub { IPAddress = "", Hostname = "" });
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--csv"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Include_Error_IPAddress_In_CSV_When_IP_Retrieval_Fails()
        {
            // SCENARIO: CSV IPAddress column contains ERROR when IP address retrieval raises an exception

            // Given
            var services = StubServices();
            services.AddSingleton<IClientInfoProvider>(new ClientInfoProviderStub { IPAddress = "ERROR", Hostname = "router-a" });
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--csv"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Include_Error_Hostname_In_CSV_When_Hostname_Retrieval_Fails()
        {
            // SCENARIO: CSV Hostname column contains ERROR when hostname retrieval raises an exception
            // SCENARIO: CSV speed test completes and writes output when hostname retrieval raises an exception

            // Given
            var services = StubServices();
            services.AddSingleton<IClientInfoProvider>(new ClientInfoProviderStub { IPAddress = "192.168.1.1", Hostname = "ERROR" });
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--csv"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Include_Error_IPAddress_And_Hostname_In_CSV_When_Both_Retrievals_Fail()
        {
            // SCENARIO: CSV speed test completes and writes output when both device identity lookups raise exceptions

            // Given
            var services = StubServices();
            services.AddSingleton<IClientInfoProvider>(new ClientInfoProviderErrorStub());
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--csv"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Escape_A_Csv_Hostname_Containing_A_Comma()
        {
            // SCENARIO: CSV hostname containing a comma is escaped

            // Given a machine whose hostname carries a comma, which would otherwise add a field to
            // the row.
            const string hostname = "desk,top";
            var services = StubServices();
            services.AddSingleton<IClientInfoProvider>(new ClientInfoProviderStub { Hostname = hostname });
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--csv"]);

            // Then a standard CSV reader sees one field per column and reads the hostname back whole.
            Assert.Equal(0, result.ExitCode);
            string[][] records = CsvRecords(result.Output);
            Assert.Equal(records[0].Length, records[1].Length);
            Assert.Equal(hostname, Field(records, "Hostname"));
        }

        [Fact]
        public async Task Should_Escape_A_Csv_Hostname_Containing_A_Double_Quote()
        {
            // SCENARIO: CSV hostname containing a double quote is escaped

            // Given a machine whose hostname opens with a double quote, the character CSV quoting is
            // itself written with, so an unescaped value would be read as a quoted field and lose it.
            const string hostname = "\"desktop\"";
            var services = StubServices();
            services.AddSingleton<IClientInfoProvider>(new ClientInfoProviderStub { Hostname = hostname });
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--csv"]);

            // Then a standard CSV reader sees one field per column and reads the hostname back whole.
            Assert.Equal(0, result.ExitCode);
            string[][] records = CsvRecords(result.Output);
            Assert.Equal(records[0].Length, records[1].Length);
            Assert.Equal(hostname, Field(records, "Hostname"));
        }

        [Fact]
        public async Task Should_Escape_Every_Free_Text_Csv_Field_Carrying_A_Csv_Character()
        {
            // Given both free-text fields carrying a comma and a double quote at once.
            const string ipAddress = "192.168.1.1,\"spare\"";
            const string hostname = "desk,\"top\"";
            var services = StubServices();
            services.AddSingleton<IClientInfoProvider>(new ClientInfoProviderStub { IPAddress = ipAddress, Hostname = hostname });
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--csv"]);

            // Then both read back whole, and the row still lines up with its header.
            Assert.Equal(0, result.ExitCode);
            string[][] records = CsvRecords(result.Output);
            Assert.Equal(records[0].Length, records[1].Length);
            Assert.Equal(ipAddress, Field(records, "IPAddress"));
            Assert.Equal(hostname, Field(records, "Hostname"));
        }

        [Theory]
        [InlineData("desk\r\ntop", "desk top")]
        [InlineData("desk\ntop", "desk top")]
        [InlineData("desk\rtop", "desk top")]
        [InlineData("desktop\n", "desktop ")]
        public async Task Should_Write_A_Line_Break_In_A_Csv_Value_As_A_Space(string hostname, string expected)
        {
            // Given a machine whose hostname carries a line break, which would otherwise break the
            // record across two lines.
            var services = StubServices();
            services.AddSingleton<IClientInfoProvider>(new ClientInfoProviderStub { Hostname = hostname });
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--csv"]);

            // Then the whole result is still one header and one data row, and the line break reads
            // back as a space.
            Assert.Equal(0, result.ExitCode);
            string[][] records = CsvRecords(result.Output);
            Assert.Equal(2, records.Length);
            Assert.Equal(expected, Field(records, "Hostname"));
        }

        [Fact]
        public async Task Should_Report_Speed_And_Latency_As_Csv_Numbers()
        {
            // SCENARIO: CSV reports speed and latency as numbers in fixed units
            // SCENARIO: CSV output without special characters is unchanged
            //
            // The second scenario is a preservation criterion: this snapshot is the comma-separated
            // header and row NetPace already wrote for ordinary values, so escaping must leave it
            // byte for byte alone.

            // Given a stub that measures 100 ms of latency, 1000 bytes downloaded in 1000 ms
            // (8,000 bits per second) and 7000 bytes uploaded in 3000 ms (18,666.67, rounded).
            var host = GetCommandLineTestHost(StubServices());

            // When
            var result = await host.RunAsync(["--csv"]);

            // Then the columns are named for their fixed units and the cells hold bare numbers, as
            // the whole row shows.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Report_The_Same_Csv_Numbers_As_Json_For_The_Same_Run()
        {
            // Given the same speed test reported in both machine-readable formats.
            var csv = await GetCommandLineTestHost(StubServices()).RunAsync(["--csv"]);
            var json = await GetCommandLineTestHost(StubServices()).RunAsync(["--json"]);

            // Then every numeric measurement agrees between the two.
            Assert.Equal(0, csv.ExitCode);
            Assert.Equal(0, json.ExitCode);
            string[][] records = CsvRecords(csv.Output);
            using var document = JsonDocument.Parse(json.Output);
            string[] numericColumns = ["LatencyMilliseconds", "DownloadSpeedBitsPerSecond", "UploadSpeedBitsPerSecond"];
            foreach (string column in numericColumns)
            {
                Assert.Equal(document.RootElement.GetProperty(column).GetInt64().ToString(), Field(records, column));
            }
        }

        [Fact]
        public async Task Should_Report_The_Same_Csv_Whatever_The_Unit_Switches_Say()
        {
            // SCENARIO: Unit switches do not change CSV output

            // Given the same speed test run twice, once with the default units and once with every
            // unit switch set to something else.
            var defaultUnits = await GetCommandLineTestHost(StubServices()).RunAsync(["--csv"]);
            var switchedUnits = await GetCommandLineTestHost(StubServices()).RunAsync(
                ["--csv", "--unit", "bytes", "--unit-scale", "Mega", "--unit-system", "IEC"]);

            // Then the two outputs are the same.
            Assert.Equal(0, defaultUnits.ExitCode);
            Assert.Equal(0, switchedUnits.ExitCode);
            Assert.Equal(defaultUnits.Output, switchedUnits.Output);
        }

        [Fact]
        public async Task Should_Keep_Csv_Columns_Fixed_When_A_Test_Is_Skipped()
        {
            // SCENARIO: CSV columns stay fixed when a test is skipped

            // Given a full run and a run that skips the upload test.
            var full = await GetCommandLineTestHost(StubServices()).RunAsync(["--csv"]);
            var skipped = await GetCommandLineTestHost(StubServices()).RunAsync(["--csv", "--no-upload"]);

            // Then both headers list the same columns in the same order.
            Assert.Equal(0, full.ExitCode);
            Assert.Equal(0, skipped.ExitCode);
            string[][] fullRecords = CsvRecords(full.Output);
            string[][] skippedRecords = CsvRecords(skipped.Output);
            Assert.Equal(fullRecords[0], skippedRecords[0]);

            // And the row has a cell for every column, with the skipped test's cells left empty.
            Assert.Equal(skippedRecords[0].Length, skippedRecords[1].Length);
            Assert.Equal(string.Empty, Field(skippedRecords, "UploadSpeedBitsPerSecond"));
            Assert.Equal(string.Empty, Field(skippedRecords, "UploadSucceeded"));
            Assert.Equal(string.Empty, Field(skippedRecords, "UploadFailed"));

            // And the tests that did run still report their measurements.
            Assert.Equal("100", Field(skippedRecords, "LatencyMilliseconds"));
            Assert.Equal("8000", Field(skippedRecords, "DownloadSpeedBitsPerSecond"));
        }

        /// <summary>
        /// The registrations a CSV test needs: a stub speed tester, a fixed clock, and a waiter that
        /// returns immediately. A test that needs more adds it to the returned collection.
        /// </summary>
        private static ServiceCollection StubServices()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            return services;
        }

        /// <summary>
        /// Reads CSV output with <c>TextFieldParser</c>, a quoted-field CSV reader from the shared
        /// framework, so a test asserts what a standard CSV consumer recovers rather than the text
        /// NetPace happened to write. Whitespace is kept: it trims inside quoted fields by default,
        /// which would discard the space this writer substitutes for a line break.
        /// </summary>
        private static string[][] CsvRecords(string output)
        {
            using var reader = new StringReader(output);
            using var parser = new TextFieldParser(reader)
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = false
            };
            parser.SetDelimiters(",");

            var records = new List<string[]>();
            while (!parser.EndOfData)
            {
                records.Add(parser.ReadFields()!);
            }

            return [.. records];
        }

        /// <summary>
        /// Reads the named column out of the single data row of parsed records, so a test names the
        /// column it means rather than counting fields.
        /// </summary>
        private static string Field(string[][] records, string column)
        {
            int index = Array.IndexOf(records[0], column);
            Assert.True(index >= 0, $"The CSV header does not carry a '{column}' column: {string.Join(',', records[0])}");
            return records[1][index];
        }
    }
}
