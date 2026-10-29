namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    public sealed class CSVHeader
    {
        private static string NewTempFilePath() =>
            Path.Join(Path.GetTempPath(), $"netpace-test-{Guid.NewGuid()}.csv");

        private static CommandLineTestHost NewHost(bool incrementingClock = false)
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            if (incrementingClock)
            {
                services.AddSingleton<IClock, IncrementingClockStub>();
            }
            else
            {
                services.AddSingleton<IClock, ClockStub>();
            }
            services.AddSingleton<IWaiter, NoDelayStub>();
            return GetCommandLineTestHost(services);
        }

        // SCENARIO: Repeated appends to one file keep a single header
        [Fact]
        public async Task Should_Keep_One_Header_When_Csv_Is_Appended_To_The_Same_File_Repeatedly()
        {
            // Given no log file exists
            var testFile = NewTempFilePath();

            try
            {
                // When three runs each append a CSV result to it
                for (var run = 0; run < 3; run++)
                {
                    var result = await NewHost().RunAsync(["--csv", "--file", testFile]);
                    Assert.Equal(0, result.ExitCode);
                }

                // Then the file holds one header line followed by three data rows
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

        // SCENARIO: Appending to an empty file writes the header
        [Fact]
        public async Task Should_Write_The_Header_When_Csv_Is_Appended_To_An_Empty_File()
        {
            // Given an empty log file exists
            var testFile = NewTempFilePath();

            try
            {
                await System.IO.File.WriteAllTextAsync(testFile, string.Empty);

                // When a CSV result is appended to it
                var result = await NewHost().RunAsync(["--csv", "--file", testFile]);

                // Then the file holds the header line followed by one data row
                Assert.Equal(0, result.ExitCode);

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

        // SCENARIO: Overwrite mode always starts with the header
        [Fact]
        public async Task Should_Start_With_The_Header_When_Csv_Overwrites_A_File_That_Has_Content()
        {
            // Given a log file already holding a header and rows
            var testFile = NewTempFilePath();

            try
            {
                await System.IO.File.WriteAllTextAsync(
                    testFile,
                    "Timestamp,LatencyMilliseconds,DownloadSpeedBitsPerSecond,DownloadSucceeded,DownloadFailed,UploadSpeedBitsPerSecond,UploadSucceeded,UploadFailed,IPAddress,Hostname\n1970-01-01T00:00:00+00:00,1,1,1,0,1,1,0,10.0.0.1,old-host\n");

                // When a CSV result overwrites it
                var result = await NewHost().RunAsync(["--csv", "--file", testFile, "--file-mode", "Overwrite"]);

                // Then the file holds only the header line and the new data row
                Assert.Equal(0, result.ExitCode);

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

        [Fact]
        public async Task Should_Keep_The_Header_On_The_Terminal_While_An_Appended_File_Does_Not_Repeat_It()
        {
            // Given a log file that already holds a header and a row from an earlier run
            var testFile = NewTempFilePath();

            try
            {
                var first = await NewHost().RunAsync(["--csv", "--file", testFile]);
                Assert.Equal(0, first.ExitCode);

                // When a second run appends to it
                var second = await NewHost().RunAsync(["--csv", "--file", testFile]);

                // Then the terminal still labels its columns, while the file gains a data row only
                Assert.Equal(0, second.ExitCode);
                await Verify(second.Output).UseTextForParameters("stdout");

                var fileContent = await System.IO.File.ReadAllTextAsync(testFile);
                await Verify(fileContent).UseTextForParameters("file");
            }
            finally
            {
                if (System.IO.File.Exists(testFile))
                {
                    System.IO.File.Delete(testFile);
                }
            }
        }

        // SCENARIO: No header on standard output
        [Fact]
        public async Task Should_Write_No_Header_To_Standard_Output_When_The_Header_Is_Turned_Off()
        {
            // Given --csv --no-csv-header
            var host = NewHost();

            // When NetPace runs
            var result = await host.RunAsync(["--csv", "--no-csv-header"]);

            // Then standard output holds the data row and no header line
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        // SCENARIO: No header across a multi-test run
        [Fact]
        public async Task Should_Write_No_Header_Across_A_Multi_Test_Run_When_The_Header_Is_Turned_Off()
        {
            // Given --csv --no-csv-header --count 3
            var host = NewHost(incrementingClock: true);

            // When NetPace runs
            var result = await host.RunAsync(["--csv", "--no-csv-header", "--count", "3"]);

            // Then the output holds three data rows and no header line
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Write_No_Header_Across_A_Looping_Run_When_The_Header_Is_Turned_Off()
        {
            // Given a looping run that cancels itself after a few iterations
            var cancellationTokenSource = new CancellationTokenSource();
            var waiter = new SelfCancellingWaiter(3, cancellationTokenSource);

            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, IncrementingClockStub>();
            services.AddSingleton<IWaiter>(waiter);
            var host = GetCommandLineTestHost(services);

            // When NetPace runs
            var result = await host.RunAsync(["--csv", "--no-csv-header", "--loop"], cancellationTokenSource.Token);

            // Then no iteration writes a header line
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        // SCENARIO: No header written to a new file
        [Fact]
        public async Task Should_Write_No_Header_To_A_New_File_When_The_Header_Is_Turned_Off()
        {
            // Given no log file exists
            var testFile = NewTempFilePath();

            try
            {
                // When NetPace runs with --csv --no-csv-header --file
                var result = await NewHost().RunAsync(["--csv", "--no-csv-header", "--file", testFile]);

                // Then the file holds the data row and no header line
                Assert.Equal(0, result.ExitCode);

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

        // SCENARIO: Standard output keeps its header by default
        [Fact]
        public async Task Should_Keep_The_Header_On_Standard_Output_By_Default()
        {
            // Given --csv --count 2 with no other header option
            var host = NewHost(incrementingClock: true);

            // When NetPace runs
            var result = await host.RunAsync(["--csv", "--count", "2"]);

            // Then standard output holds one header line followed by two data rows
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Ignore_The_Header_Switch_Outside_Csv_Output()
        {
            // Given the header switch passed to a format that has no header row
            var host = NewHost();

            // When NetPace runs
            var result = await host.RunAsync(["--minimal", "--no-csv-header"]);

            // Then the run is accepted and the output is unchanged
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }
    }
}
