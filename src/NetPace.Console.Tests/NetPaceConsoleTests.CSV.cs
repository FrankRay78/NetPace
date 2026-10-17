using System.Text.Json;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    public sealed class CSV
    {
        [Fact]
        public async Task Should_Perform_Speed_Test_With_CSV()
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--csv"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

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

        [InlineData(',')]
        [InlineData(';')]
        [InlineData('\t')]
        [Theory]
        public async Task Should_Perform_Speed_Test_With_CSV_Delimiter(char delimiter)
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--csv", "--csv-delimiter", delimiter.ToString()]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(delimiter);
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

        [Theory]
        [InlineData("+", "plus")]
        [InlineData("-", "hyphen")]
        [InlineData(":", "colon")]
        [InlineData("T", "tee")]
        [InlineData("0", "zero")]
        public async Task Should_Reject_A_CSV_Delimiter_That_Occurs_In_The_Timestamp(string delimiter, string delimiterName)
        {
            // Given
            var host = GetCommandLineTestHost(StubServices());

            // When a delimiter is chosen that the fixed timestamp itself contains.
            var result = await host.RunAsync(["--csv", "--csv-delimiter", delimiter]);

            // Then the run is refused up front, naming the option, rather than writing a row whose
            // timestamp is split across two fields.
            Assert.NotEqual(0, result.ExitCode);

            // The snapshot is named for the delimiter in words: ":" is not a filename character on
            // every platform, and sanitising it collides with the "-" case.
            await Verify(result.Output).UseTextForParameters(delimiterName);
        }

        [Fact]
        public async Task Should_Report_Speed_And_Latency_As_Csv_Numbers()
        {
            // SCENARIO: CSV reports speed and latency as numbers in fixed units

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
            var lines = CsvLines(csv.Output);
            using var document = JsonDocument.Parse(json.Output);
            string[] numericColumns = ["LatencyMilliseconds", "DownloadSpeedBitsPerSecond", "UploadSpeedBitsPerSecond"];
            foreach (string column in numericColumns)
            {
                Assert.Equal(document.RootElement.GetProperty(column).GetInt64().ToString(), Cell(lines, column));
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
                ["--csv", "--unit", "BytesPerSecond", "--unit-scale", "Mega", "--unit-system", "IEC"]);

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
            var fullLines = CsvLines(full.Output);
            var skippedLines = CsvLines(skipped.Output);
            Assert.Equal(fullLines[0], skippedLines[0]);

            // And the row has a cell for every column, with the skipped test's cells left empty.
            Assert.Equal(skippedLines[0].Split(',').Length, skippedLines[1].Split(',').Length);
            Assert.Equal(string.Empty, Cell(skippedLines, "UploadSpeedBitsPerSecond"));
            Assert.Equal(string.Empty, Cell(skippedLines, "UploadSucceeded"));
            Assert.Equal(string.Empty, Cell(skippedLines, "UploadFailed"));

            // And the tests that did run still report their measurements.
            Assert.Equal("100", Cell(skippedLines, "LatencyMilliseconds"));
            Assert.Equal("8000", Cell(skippedLines, "DownloadSpeedBitsPerSecond"));
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
        /// Splits CSV output into its records.
        /// </summary>
        private static string[] CsvLines(string output) =>
            output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r')).ToArray();

        /// <summary>
        /// Reads the named column out of the single data row, so a test names the column it means
        /// rather than counting commas.
        /// </summary>
        private static string Cell(string[] lines, string column)
        {
            var index = Array.IndexOf(lines[0].Split(','), column);
            Assert.True(index >= 0, $"The CSV header does not carry a '{column}' column: {lines[0]}");
            return lines[1].Split(',')[index];
        }
    }
}
