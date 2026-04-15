using NetPace.Console.DependencyInjection;
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
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ quiet ]);

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
                var registrar = new TypeRegistrar();
                registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
                registrar.Register(typeof(IClock), typeof(ClockStub));
                registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
                registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
                var app = GetCommandAppTester(registrar);

                // When
                var result = await app.RunAsync([ quiet, "--file", testFile ]);

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
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ quiet, "--csv" ]);

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
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ quiet, "--json" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            Assert.Empty(result.Output);
        }

        [InlineData("-q", "Minimal")]
        [InlineData("-q", "Normal")]
        [InlineData("-q", "Debug")]
        [InlineData("--quiet", "Minimal")]
        [InlineData("--quiet", "Normal")]
        [InlineData("--quiet", "Debug")]
        [Theory]
        public async Task Should_Suppress_Console_With_Verbosity_In_Quiet_Mode(string quiet, string verbosity)
        {
            // Given
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ quiet, "--verbosity", verbosity ]);

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
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ quiet, "--count", "ABC" ]);

            // Then
            Assert.NotEqual(0, result.ExitCode);
            await Verify(result.Output).DisableRequireUniquePrefix();

            // Configuration errors that prevent the programme from commencing
            // are shown on the console, despite the `-q|--quiet` switch.
            // This is inline with at least grep:
            // ```bash
            // C:\Users\frank>grep -q -f NONEXISTANT
            // grep: NONEXISTANT: No such file or directory
            // ```
            // This behaviour can remain under review.
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

            var registrar = new TypeRegistrar();
            registrar.RegisterInstance(typeof(ISpeedTestService), mock);
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ quiet ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            Assert.Empty(result.Output);
        }
    }
}
