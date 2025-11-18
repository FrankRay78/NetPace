using NetPace.Console.DependencyInjection;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    public sealed class File
    {
        [InlineData("-f")]
        [InlineData("--file")]
        [Theory]
        public async Task Should_Write_Output_To_File(string file)
        {
            // Given
            var testFile = Path.Join(Path.GetTempPath(), $"netpace-test-{Guid.NewGuid()}.txt");

            try
            {
                var registrar = new TypeRegistrar();
                registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
                registrar.Register(typeof(IClock), typeof(ClockStub));
                registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
                var app = GetCommandAppTester(registrar);

                // When
                var result = await app.RunAsync(file, testFile);

                // Then
                Assert.Equal(0, result.ExitCode);
                await Verify(result.Output).DisableRequireUniquePrefix(); ;

                var fileContent = await System.IO.File.ReadAllTextAsync(testFile);
                await Verify(fileContent).DisableRequireUniquePrefix();
            }
            finally
            {
                if (System.IO.File.Exists(testFile))
                {
                    System.IO.File.Delete(testFile);
                }
            }
        }

        [InlineData("-f")]
        [InlineData("--file")]
        [Theory]
        public async Task Should_Write_CSV_Output_To_File(string file)
        {
            // Given
            var testFile = Path.Join(Path.GetTempPath(), $"netpace-test-{Guid.NewGuid()}.txt");

            try
            {
                var registrar = new TypeRegistrar();
                registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
                registrar.Register(typeof(IClock), typeof(ClockStub));
                registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
                var app = GetCommandAppTester(registrar);

                // When
                var result = await app.RunAsync("--csv", file, testFile);

                // Then
                Assert.Equal(0, result.ExitCode);
                await Verify(result.Output).DisableRequireUniquePrefix(); ;

                var fileContent = await System.IO.File.ReadAllTextAsync(testFile);
                await Verify(fileContent).DisableRequireUniquePrefix(); ;
            }
            finally
            {
                if (System.IO.File.Exists(testFile))
                {
                    System.IO.File.Delete(testFile);
                }
            }
        }

        [InlineData("-f")]
        [InlineData("--file")]
        [Theory]
        public async Task Should_Write_Json_Output_To_File(string file)
        {
            // Given
            var testFile = Path.Join(Path.GetTempPath(), $"netpace-test-{Guid.NewGuid()}.txt");

            try
            {
                var registrar = new TypeRegistrar();
                registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
                registrar.Register(typeof(IClock), typeof(ClockStub));
                registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
                var app = GetCommandAppTester(registrar);

                // When
                var result = await app.RunAsync("--json", file, testFile);

                // Then
                Assert.Equal(0, result.ExitCode);
                await Verify(result.Output).DisableRequireUniquePrefix(); ;

                var fileContent = await System.IO.File.ReadAllTextAsync(testFile);
                await Verify(fileContent).DisableRequireUniquePrefix(); ;
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
        public async Task Should_Create_New_File_When_FileMode_Is_Append_And_File_Does_Not_Exist()
        {
            // Given
            var testFile = Path.Join(Path.GetTempPath(), $"netpace-test-{Guid.NewGuid()}.txt");

            try
            {
                var registrar = new TypeRegistrar();
                registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
                registrar.Register(typeof(IClock), typeof(ClockStub));
                registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
                var app = GetCommandAppTester(registrar);

                // When
                var result = await app.RunAsync("--file", testFile, "--file-mode", "Append");

                // Then
                Assert.Equal(0, result.ExitCode);
                Assert.True(System.IO.File.Exists(testFile));

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
        public async Task Should_Create_New_File_When_FileMode_Is_Overwrite_And_File_Does_Not_Exist()
        {
            // Given
            var testFile = Path.Join(Path.GetTempPath(), $"netpace-test-{Guid.NewGuid()}.txt");

            try
            {
                var registrar = new TypeRegistrar();
                registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
                registrar.Register(typeof(IClock), typeof(ClockStub));
                registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
                var app = GetCommandAppTester(registrar);

                // When
                var result = await app.RunAsync("--file", testFile, "--file-mode", "Overwrite");

                // Then
                Assert.Equal(0, result.ExitCode);
                Assert.True(System.IO.File.Exists(testFile));

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
        public async Task Should_Overwrite_Existing_File_When_FileMode_Is_Overwrite()
        {
            // Given
            var testFile = Path.Join(Path.GetTempPath(), $"netpace-test-{Guid.NewGuid()}.txt");

            try
            {
                // Create a file with existing content
                await System.IO.File.WriteAllTextAsync(testFile, "OLD CONTENT THAT SHOULD BE REPLACED");

                var registrar = new TypeRegistrar();
                registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
                registrar.Register(typeof(IClock), typeof(ClockStub));
                registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
                var app = GetCommandAppTester(registrar);

                // When
                var result = await app.RunAsync("--file", testFile, "--file-mode", "Overwrite");

                // Then
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
        public async Task Should_Append_To_Existing_File_When_FileMode_Not_Specified()
        {
            // Given
            var testFile = Path.Join(Path.GetTempPath(), $"netpace-test-{Guid.NewGuid()}.txt");

            try
            {
                // Create a file with existing content
                await System.IO.File.WriteAllTextAsync(testFile, "FIRST RUN\n");

                var registrar = new TypeRegistrar();
                registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
                registrar.Register(typeof(IClock), typeof(ClockStub));
                registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
                var app = GetCommandAppTester(registrar);

                // When
                var result = await app.RunAsync("--file", testFile);

                // Then
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
        public async Task Should_Append_To_Existing_File_When_FileMode_Is_Append()
        {
            // Given
            var testFile = Path.Join(Path.GetTempPath(), $"netpace-test-{Guid.NewGuid()}.txt");

            try
            {
                // Create a file with existing content
                await System.IO.File.WriteAllTextAsync(testFile, "EXISTING CONTENT\n");

                var registrar = new TypeRegistrar();
                registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
                registrar.Register(typeof(IClock), typeof(ClockStub));
                registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
                var app = GetCommandAppTester(registrar);

                // When
                var result = await app.RunAsync("--file", testFile, "--file-mode", "Append");

                // Then
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
        public async Task Should_Handle_File_Creation_Exception()
        {
            // Given
            var invalidPath = Path.Join(Path.GetTempPath(), "nonexistent-directory", "output.txt");

            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync("--file", invalidPath, "--verbosity", "Minimal");

            // Then
            Assert.NotEqual(0, result.ExitCode);

            // Normalize directory separators to Windows-style backslashes so the Windows snapshots match across platforms.
            var normalizedOutput = (result.Output ?? string.Empty).Replace('/', '\\');
            await Verify(normalizedOutput);
        }
    }
}