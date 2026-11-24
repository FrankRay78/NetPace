using static NetPace.Console.Tests.NetPaceConsoleTests;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    public sealed class Quiet
    {
        [InlineData("-q")]
        [InlineData("--quiet")]
        [Theory]
        public async Task Should_Suppress_Console_In_Quiet_Mode(string quiet)
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([quiet]);

            // Then
            Assert.Equal(0, result.ExitCode);
            Assert.Empty(result.Output);
        }

        [InlineData("-q")]
        [InlineData("--quiet")]
        [Theory]
        public async Task Should_Suppress_Console_But_Write_To_File_In_Quiet_Mode(string quiet)
        {
            // Given
            var testFile = Path.Join(Path.GetTempPath(), $"netpace-test-quiet-{Guid.NewGuid()}.txt");

            try
            {
                var services = new ServiceCollection();
                services.AddSingleton<ISpeedTestService, SpeedTestStub>();
                services.AddSingleton<IClock, ClockStub>();
                services.AddSingleton<IWaiter, NoDelayStub>();
                var host = GetCommandLineTestHost(services);

                // When
                var result = await host.RunAsync([quiet, "--file", testFile]);

                // Then
                Assert.Equal(0, result.ExitCode);

                // Console should be empty
                Assert.Empty(result.Output);

                // File should contain output
                Assert.True(System.IO.File.Exists(testFile));
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

        [InlineData("-q")]
        [InlineData("--quiet")]
        [Theory]
        public async Task Should_Suppress_Console_With_CSV_Format_In_Quiet_Mode(string quiet)
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([quiet, "--csv"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            Assert.Empty(result.Output);
        }

        [InlineData("-q")]
        [InlineData("--quiet")]
        [Theory]
        public async Task Should_Suppress_Console_With_Json_Format_In_Quiet_Mode(string quiet)
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([quiet, "--json"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            Assert.Empty(result.Output);
        }

        [InlineData("-q")]
        [InlineData("--quiet")]
        [Theory]
        public async Task Should_Suppress_Console_With_Simple_In_Quiet_Mode(string quiet)
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([quiet, "--simple"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            Assert.Empty(result.Output);
        }

        [InlineData("-q")]
        [InlineData("--quiet")]
        [Theory]
        public async Task Should_Handle_Configuration_Exceptions_In_Quiet_Mode(string quiet)
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([quiet, "--count", "ABC"]);

            // Then
            Assert.Equal(1, result.ExitCode);
            Assert.Empty(result.Output);
            await Verify(result.ErrorOutput).DisableRequireUniquePrefix();

            // A configuration error that stops the run starting still reaches the error stream
            // under --quiet, as grep -q reports a missing pattern file.
        }

        [InlineData("-q")]
        [InlineData("--quiet")]
        [Theory]
        public async Task Should_Handle_Network_Exceptions_In_Quiet_Mode(string quiet)
        {
            // Given
            var mock = new SpeedTestMock
            {
                GetServersAsyncFunc = (cancellationToken) => throw new HttpRequestException("Could not connect to server")
            };

            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(mock);
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([quiet]);

            // Then
            Assert.Equal(0, result.ExitCode);
            Assert.Empty(result.Output);
        }
    }
}
