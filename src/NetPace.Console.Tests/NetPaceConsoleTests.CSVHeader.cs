using System.Runtime.Versioning;
using System.Text;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    public sealed class CSVHeader
    {
        private static CommandLineTestHost NewHost(bool incrementingClock = false) =>
            NewHost(incrementingClock ? new IncrementingClockStub() : new ClockStub());

        private static CommandLineTestHost NewHost(IClock clock)
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton(clock);
            services.AddSingleton<IWaiter, NoDelayStub>();
            return GetCommandLineTestHost(services);
        }

        // SCENARIO: Repeated appends to one file keep a single header
        [Fact]
        public async Task Should_Keep_One_Header_When_Csv_Is_Appended_To_The_Same_File_Repeatedly()
        {
            // Given no log file exists
            using var log = new TempLogFile();

            // When three runs each append a CSV result to it. One clock is shared across the runs,
            // so every row carries a different timestamp and the snapshot cannot be satisfied by
            // one run's row written three times.
            var clock = new IncrementingClockStub();

            for (var run = 0; run < 3; run++)
            {
                var result = await NewHost(clock).RunAsync(["--csv", "--file", log.FilePath]);
                Assert.Equal(0, result.ExitCode);
            }

            // Then the file holds one header line followed by three data rows
            var fileContent = await System.IO.File.ReadAllTextAsync(log.FilePath);
            await Verify(fileContent);
        }

        // SCENARIO: Appending to an empty file writes the header
        [Fact]
        public async Task Should_Write_The_Header_When_Csv_Is_Appended_To_An_Empty_File()
        {
            // Given an empty log file exists
            using var log = new TempLogFile();
            await System.IO.File.WriteAllTextAsync(log.FilePath, string.Empty);

            // When a CSV result is appended to it
            var result = await NewHost().RunAsync(["--csv", "--file", log.FilePath]);

            // Then the file holds the header line followed by one data row
            Assert.Equal(0, result.ExitCode);

            var fileContent = await System.IO.File.ReadAllTextAsync(log.FilePath);
            await Verify(fileContent);
        }

        // SCENARIO: Overwrite mode always starts with the header
        [Fact]
        public async Task Should_Start_With_The_Header_When_Csv_Overwrites_A_File_That_Has_Content()
        {
            // Given a log file already holding content. Nothing reads what that content is, only
            // whether there is any, so it does not need to be a well-formed log.
            using var log = new TempLogFile();
            await System.IO.File.WriteAllTextAsync(log.FilePath, "rows from an earlier run\n");

            // When a CSV result overwrites it
            var result = await NewHost().RunAsync(["--csv", "--file", log.FilePath, "--file-mode", "Overwrite"]);

            // Then the file holds only the header line and the new data row
            Assert.Equal(0, result.ExitCode);

            var fileContent = await System.IO.File.ReadAllTextAsync(log.FilePath);
            await Verify(fileContent);
        }

        [Fact]
        public async Task Should_Keep_The_Header_On_The_Terminal_While_An_Appended_File_Does_Not_Repeat_It()
        {
            // Given a log file that already holds a header and a row from an earlier run
            using var log = new TempLogFile();

            var first = await NewHost().RunAsync(["--csv", "--file", log.FilePath]);
            Assert.Equal(0, first.ExitCode);

            // When a second run appends to it
            var second = await NewHost().RunAsync(["--csv", "--file", log.FilePath]);

            // Then the terminal still labels its columns, while the file gains a data row only
            Assert.Equal(0, second.ExitCode);
            await Verify(second.Output).UseTextForParameters("stdout");

            var fileContent = await System.IO.File.ReadAllTextAsync(log.FilePath);
            await Verify(fileContent).UseTextForParameters("file");
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
            using var log = new TempLogFile();

            // When NetPace runs with --csv --no-csv-header --file
            var result = await NewHost().RunAsync(["--csv", "--no-csv-header", "--file", log.FilePath]);

            // Then the file holds the data row and no header line
            Assert.Equal(0, result.ExitCode);

            var fileContent = await System.IO.File.ReadAllTextAsync(log.FilePath);
            await Verify(fileContent);
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

        [Fact]
        public async Task Should_Write_The_Header_To_A_New_File_When_The_Terminal_Is_Quiet()
        {
            // Given no log file exists, and a run that prints nothing to the terminal
            using var log = new TempLogFile();

            // When a CSV result is written to the file
            var result = await NewHost().RunAsync(["--quiet", "--csv", "--file", log.FilePath]);

            // Then the file holds the header line followed by one data row
            Assert.Equal(0, result.ExitCode);

            var fileContent = await System.IO.File.ReadAllTextAsync(log.FilePath);
            await Verify(fileContent);
        }

        [Fact]
        public async Task Should_Not_Repeat_The_Header_In_A_Quiet_Appended_File()
        {
            // Given a log file that already holds a header and a row from an earlier quiet run
            using var log = new TempLogFile();

            var first = await NewHost().RunAsync(["--quiet", "--csv", "--file", log.FilePath]);
            Assert.Equal(0, first.ExitCode);

            // When a second quiet run appends to it
            var second = await NewHost().RunAsync(["--quiet", "--csv", "--file", log.FilePath]);

            // Then the file gains a data row only
            Assert.Equal(0, second.ExitCode);

            var fileContent = await System.IO.File.ReadAllTextAsync(log.FilePath);
            await Verify(fileContent);
        }

        [Fact]
        public async Task Should_Write_One_Header_When_A_Multi_Test_Run_Appends_To_A_File()
        {
            // Given no log file exists
            using var log = new TempLogFile();

            // When one run appends three results to it
            var result = await NewHost(incrementingClock: true)
                .RunAsync(["--csv", "--file", log.FilePath, "--count", "3"]);

            // Then the file holds one header line followed by three data rows
            Assert.Equal(0, result.ExitCode);

            var fileContent = await System.IO.File.ReadAllTextAsync(log.FilePath);
            await Verify(fileContent);
        }

        [Fact]
        public async Task Should_Write_The_Header_When_The_Log_Holds_Nothing_But_A_Byte_Order_Mark()
        {
            // Given a log file that every reader shows as empty, which is what a run that opened
            // the file and then ended before writing a row leaves behind
            using var log = new TempLogFile();
            await System.IO.File.WriteAllBytesAsync(log.FilePath, Encoding.UTF8.GetPreamble());

            // When a CSV result is appended to it
            var result = await NewHost().RunAsync(["--csv", "--file", log.FilePath]);

            // Then the log gains the header, rather than collecting rows under none for good
            Assert.Equal(0, result.ExitCode);

            var fileContent = await System.IO.File.ReadAllTextAsync(log.FilePath);
            await Verify(fileContent);
        }

        [Fact]
        public async Task Should_Write_The_Header_On_The_First_Line_When_The_Log_Holds_Only_A_Line_Ending()
        {
            // Given a log file holding nothing but the line ending `echo > log.csv` leaves behind
            using var log = new TempLogFile();
            await System.IO.File.WriteAllTextAsync(log.FilePath, "\n");

            // When a CSV result is appended to it
            var result = await NewHost().RunAsync(["--csv", "--file", log.FilePath]);

            // Then the header is the log's first line, not one below a blank line
            Assert.Equal(0, result.ExitCode);

            var fileContent = await System.IO.File.ReadAllTextAsync(log.FilePath);
            await Verify(fileContent);
        }

        [Fact]
        public async Task Should_Write_The_Header_On_The_First_Line_When_The_Log_Holds_A_Byte_Order_Mark_And_A_Windows_Line_Ending()
        {
            // Given a log file holding a byte-order mark and a Windows line ending, the longest file
            // that still holds no row
            using var log = new TempLogFile();
            await System.IO.File.WriteAllBytesAsync(log.FilePath, [.. Encoding.UTF8.GetPreamble(), (byte)'\r', (byte)'\n']);

            // When a CSV result is appended to it
            var result = await NewHost().RunAsync(["--csv", "--file", log.FilePath]);

            // Then the header is the log's first line
            Assert.Equal(0, result.ExitCode);

            var fileContent = await System.IO.File.ReadAllTextAsync(log.FilePath);
            await Verify(fileContent);
        }

        [Fact]
        public async Task Should_Write_The_Header_On_The_First_Line_When_The_Log_Holds_Only_A_Windows_Line_Ending()
        {
            // Given a log file holding nothing but the line ending `echo.>log.csv` leaves behind
            using var log = new TempLogFile();
            await System.IO.File.WriteAllTextAsync(log.FilePath, "\r\n");

            // When a CSV result is appended to it
            var result = await NewHost().RunAsync(["--csv", "--file", log.FilePath]);

            // Then the header is the log's first line
            Assert.Equal(0, result.ExitCode);

            var fileContent = await System.IO.File.ReadAllTextAsync(log.FilePath);
            await Verify(fileContent);
        }

        [Fact]
        public async Task Should_Keep_A_Log_Of_Blank_Lines_Longer_Than_A_Byte_Order_Mark_And_A_Line_Ending()
        {
            // Given a log file of blank lines, one byte longer than the longest file that holds no row
            using var log = new TempLogFile();
            await System.IO.File.WriteAllTextAsync(log.FilePath, "\r\n\r\n\r\n");

            // When a CSV result is appended to it
            var result = await NewHost().RunAsync(["--csv", "--file", log.FilePath]);

            // Then those lines are kept and the row is added beneath them, with no header
            Assert.Equal(0, result.ExitCode);

            var fileContent = await System.IO.File.ReadAllTextAsync(log.FilePath);
            await Verify(fileContent);
        }

        [Fact]
        [UnsupportedOSPlatform("windows")]
        public async Task Should_Write_To_An_Empty_Log_That_Can_Be_Written_But_Not_Read()
        {
            // Given an empty log that NetPace may write to but not read, as one pre-created for a
            // service account can be. File modes are a Unix permission model, so this fails loudly
            // rather than skipping on Windows (Constitution §X).
            using var log = new TempLogFile();
            await System.IO.File.WriteAllTextAsync(log.FilePath, string.Empty);
            System.IO.File.SetUnixFileMode(log.FilePath, UnixFileMode.UserWrite);

            // When a CSV result is appended to it
            var result = await NewHost().RunAsync(["--csv", "--file", log.FilePath]);

            // Then the run succeeds and the log holds the header line followed by one data row
            Assert.Equal(0, result.ExitCode);

            System.IO.File.SetUnixFileMode(log.FilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            var fileContent = await System.IO.File.ReadAllTextAsync(log.FilePath);
            await Verify(fileContent);
        }

        [Fact]
        public async Task Should_Keep_A_Short_Log_With_Content_When_Csv_Is_Appended()
        {
            // Given a log file whose content is shorter than a byte-order mark and a line ending
            using var log = new TempLogFile();
            await System.IO.File.WriteAllTextAsync(log.FilePath, "x\n");

            // When a CSV result is appended to it
            var result = await NewHost().RunAsync(["--csv", "--file", log.FilePath]);

            // Then that content is kept and the row is added beneath it, with no header
            Assert.Equal(0, result.ExitCode);

            var fileContent = await System.IO.File.ReadAllTextAsync(log.FilePath);
            await Verify(fileContent);
        }

        [Fact]
        public async Task Should_Not_Repeat_The_Header_When_Appending_Through_A_Link_To_A_Log_With_Rows()
        {
            // Given a link to a log that already holds a header and a row
            using var target = new TempLogFile();
            using var link = new TempLogFile();

            var first = await NewHost().RunAsync(["--csv", "--file", target.FilePath]);
            Assert.Equal(0, first.ExitCode);
            System.IO.File.CreateSymbolicLink(link.FilePath, target.FilePath);

            // When a CSV result is appended through the link
            var second = await NewHost().RunAsync(["--csv", "--file", link.FilePath]);

            // Then the log gains a data row only
            Assert.Equal(0, second.ExitCode);

            var fileContent = await System.IO.File.ReadAllTextAsync(target.FilePath);
            await Verify(fileContent);
        }

        [Fact]
        public async Task Should_Write_The_Header_When_Appending_Through_A_Link_To_An_Empty_Log()
        {
            // Given a link to an empty log
            using var target = new TempLogFile();
            using var link = new TempLogFile();
            await System.IO.File.WriteAllTextAsync(target.FilePath, string.Empty);
            System.IO.File.CreateSymbolicLink(link.FilePath, target.FilePath);

            // When a CSV result is appended through the link
            var result = await NewHost().RunAsync(["--csv", "--file", link.FilePath]);

            // Then the log holds the header line followed by one data row
            Assert.Equal(0, result.ExitCode);

            var fileContent = await System.IO.File.ReadAllTextAsync(target.FilePath);
            await Verify(fileContent);
        }

        [Fact]
        public async Task Should_Write_The_Header_When_Appending_Through_A_Link_Whose_Log_Is_Gone()
        {
            // Given a link whose log has been rotated away
            using var target = new TempLogFile();
            using var link = new TempLogFile();
            System.IO.File.CreateSymbolicLink(link.FilePath, target.FilePath);

            // When a CSV result is appended through the link
            var result = await NewHost().RunAsync(["--csv", "--file", link.FilePath]);

            // Then a fresh log starts with the header line followed by one data row
            Assert.Equal(0, result.ExitCode);

            var fileContent = await System.IO.File.ReadAllTextAsync(target.FilePath);
            await Verify(fileContent);
        }

        /// <summary>
        /// A log file path unique to one test, deleted when that test finishes. Nothing is created
        /// up front, so a test chooses for itself whether the path already holds content.
        /// </summary>
        private sealed class TempLogFile : IDisposable
        {
            public string FilePath { get; } = Path.Join(Path.GetTempPath(), $"netpace-test-{Guid.NewGuid()}.csv");

            public void Dispose()
            {
                if (System.IO.File.Exists(FilePath))
                {
                    System.IO.File.Delete(FilePath);
                }
            }
        }
    }
}
