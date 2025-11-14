using NetPace.Console.Commands;
using NetPace.Console.DependencyInjection;
using Spectre.Console.Cli;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    /// <summary>
    /// Create the CommandAppTester and configure.
    /// </summary>
    private static CommandAppTester GetCommandAppTester(ITypeRegistrar? registrar = null, CancellationToken cancellationToken = default)
    {
        var app = registrar == null ? 
            new CommandAppTester(new CommandAppTesterSettings { TrimConsoleOutput = false }) :
            new CommandAppTester(registrar, new CommandAppTesterSettings { TrimConsoleOutput = false });

        app.SetDefaultCommand<SpeedTestCommand>(Program.Description);
        app.Configure(Program.ConfigureAction);

        app.Registrar?.RegisterInstance(typeof(CancellationToken), cancellationToken);

        return app;
    }

    #region Speed Test

    [Fact]
    public async Task Should_Perform_Speed_Test()
    {
        // Given
        var registrar = new TypeRegistrar();
        registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
        registrar.Register(typeof(IClock), typeof(ClockStub));
        registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
        var app = GetCommandAppTester(registrar);

        // When
        var result = await app.RunAsync();

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output);
    }

    [Fact]
    public async Task Should_Perform_Speed_Test_With_Fixed_Unit_Scale()
    {
        // Given
        var registrar = new TypeRegistrar();
        registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
        registrar.Register(typeof(IClock), typeof(ClockStub));
        registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
        var app = GetCommandAppTester(registrar);

        // When
        var result = await app.RunAsync("--unit-scale", "Mega");

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output);
    }

    [Fact]
    public async Task Should_Perform_Speed_Test_Continuously()
    {
        // Given
        var cancellationTokenSource = new CancellationTokenSource();
        var waiter = new SelfCancellingWaiter(10, cancellationTokenSource);

        var registrar = new TypeRegistrar();
        registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
        registrar.Register(typeof(IClock), typeof(IncrementingClockStub));
        registrar.RegisterInstance(typeof(IWaiter), waiter);
        var app = GetCommandAppTester(registrar, cancellationTokenSource.Token);

        // When
        var result = await app.RunAsync("-t", "--loop", "--verbosity", "Minimal");

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output);
    }

    [InlineData(5)]
    [Theory]
    public async Task Should_Perform_Speed_Test_Multiple_Times(int count)
    {
        // Given
        var registrar = new TypeRegistrar();
        registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
        registrar.Register(typeof(IClock), typeof(IncrementingClockStub));
        registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
        var app = GetCommandAppTester(registrar);

        // When
        var result = await app.RunAsync("-t", "--count", $"{count}", "--verbosity", "Minimal");

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output).UseParameters(count);
    }

    [InlineData(10, "00:10:00")]
    [Theory]
    public async Task Should_Perform_Speed_Test_Multiple_Times_With_Delay(int count, string delay)
    {
        // Given
        var waiter = new NoDelayStub();

        var registrar = new TypeRegistrar();
        registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
        registrar.Register(typeof(IClock), typeof(IncrementingClockStub));
        registrar.RegisterInstance(typeof(IWaiter), waiter);
        var app = GetCommandAppTester(registrar);

        // When
        var result = await app.RunAsync("-t", "--count", $"{count}", "--delay", $"{delay}", "--verbosity", "Minimal");

        // Then
        Assert.Equal(count - 1, waiter.CallCount);
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output).UseParameters(count, delay);
    }

    [Fact]
    public async Task Should_Perform_Speed_Test_Multiple_Times_With_Fixed_Scale()
    {
        // Given
        var registrar = new TypeRegistrar();
        registrar.Register(typeof(ISpeedTestService), typeof(VariableSpeedTester));
        registrar.Register(typeof(IClock), typeof(IncrementingClockStub));
        registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
        var app = GetCommandAppTester(registrar);

        // When
        var result = await app.RunAsync("--count", "3", "--unit-scale", "Mega", "--verbosity", "Minimal");

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output);
    }

    [InlineData("Minimal")]
    [InlineData("Normal")]
    [InlineData("Debug")]
    [Theory]
    public async Task Should_Perform_Speed_Test_With_Verbosity(string verbosity)
    {
        // Given
        var registrar = new TypeRegistrar();
        registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
        registrar.Register(typeof(IClock), typeof(ClockStub));
        registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
        var app = GetCommandAppTester(registrar);

        // When
        var result = await app.RunAsync("--verbosity", verbosity);

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output).UseParameters(verbosity);
    }

    [InlineData("http://test1.com")]
    [InlineData("http://test2.com")]
    [InlineData("http://test3.com")]
    [InlineData("http://random-speedtest-server.com")]
    [Theory]
    public async Task Should_Perform_Speed_Test_With_Server(string url)
    {
        // Given
        var registrar = new TypeRegistrar();
        registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
        registrar.Register(typeof(IClock), typeof(ClockStub));
        registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
        var app = GetCommandAppTester(registrar);

        // When
        var result = await app.RunAsync("--server", url);

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output).UseParameters(url);
    }

    [InlineData("http://test1.com")]
    [InlineData("http://test2.com")]
    [InlineData("http://test3.com")]
    [InlineData("http://random-speedtest-server.com")]
    [Theory]
    public async Task Should_Perform_Speed_Test_Multiple_Times_With_Server(string url)
    {
        // Given
        var registrar = new TypeRegistrar();
        registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
        registrar.Register(typeof(IClock), typeof(ClockStub));
        registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
        var app = GetCommandAppTester(registrar);

        // When
        var result = await app.RunAsync("--csv", "--count", "3", "--unit-scale", "Mega", "--server", url);

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output).UseParameters(url);
    }

    [InlineData("-t")]
    [InlineData("--timestamp")]
    [Theory]
    public async Task Should_Perform_Speed_Test_With_Timestamp(string timestamp)
    {
        // Given
        var registrar = new TypeRegistrar();
        registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
        registrar.Register(typeof(IClock), typeof(ClockStub));
        registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
        var app = GetCommandAppTester(registrar);

        // When
        var result = await app.RunAsync(timestamp);

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output).DisableRequireUniquePrefix();
    }

    [InlineData(SpeedUnit.BytesPerSecond, SpeedUnitSystem.SI)]
    [InlineData(SpeedUnit.BytesPerSecond, SpeedUnitSystem.IEC)]
    [InlineData(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI)]
    [InlineData(SpeedUnit.BitsPerSecond, SpeedUnitSystem.IEC)]
    [Theory]
    public async Task Should_Perform_Speed_Test_With_Units(SpeedUnit unit, SpeedUnitSystem unitSystem)
    {
        // Given
        var registrar = new TypeRegistrar();
        registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
        registrar.Register(typeof(IClock), typeof(ClockStub));
        registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
        var app = GetCommandAppTester(registrar);

        // When
        var result = await app.RunAsync("--unit", unit.ToString(), "--unit-system", unitSystem.ToString());

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output).UseParameters(unit, unitSystem);
    }

    [InlineData("Minimal")]
    [InlineData("Normal")]
    [InlineData("Debug")]
    [Theory]
    public async Task Should_Not_Perform_Download_Speed_Test(string verbosity)
    {
        // Given
        var registrar = new TypeRegistrar();
        registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
        registrar.Register(typeof(IClock), typeof(ClockStub));
        registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
        var app = GetCommandAppTester(registrar);

        // When
        var result = await app.RunAsync("--no-download", "--verbosity", verbosity);

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output).UseParameters(verbosity);
    }

    [InlineData("Minimal")]
    [InlineData("Normal")]
    [InlineData("Debug")]
    [Theory]
    public async Task Should_Not_Perform_Upload_Speed_Test(string verbosity)
    {
        // Given
        var registrar = new TypeRegistrar();
        registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
        registrar.Register(typeof(IClock), typeof(ClockStub));
        registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
        var app = GetCommandAppTester(registrar);

        // When
        var result = await app.RunAsync("--no-upload", "--verbosity", verbosity);

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output).UseParameters(verbosity);
    }

    [InlineData("Minimal")]
    [InlineData("Normal")]
    [InlineData("Debug")]
    [Theory]
    public async Task Should_Not_Perform_Download_Upload_Speed_Test(string verbosity)
    {
        // Given
        var registrar = new TypeRegistrar();
        registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
        registrar.Register(typeof(IClock), typeof(ClockStub));
        registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
        var app = GetCommandAppTester(registrar);

        // When
        var result = await app.RunAsync("--no-download", "--no-upload", "--verbosity", verbosity);

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output).UseParameters(verbosity);
    }

    [Fact]
    public async Task Should_Handle_Unknown_Exceptions()
    {
        // Given
        var mock = new SpeedTestMock
        {
            GetServersAsyncFunc = (cancellationToken) => throw new HttpRequestException("Could not open socket")
        };

        var registrar = new TypeRegistrar();
        registrar.RegisterInstance(typeof(ISpeedTestService), mock);
        registrar.Register(typeof(IClock), typeof(ClockStub));
        registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
        var app = GetCommandAppTester(registrar);

        // When
        var result = await app.RunAsync();

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output);
    }

    [Fact]
    public async Task Should_Cancel_When_User_Requests()
    {
        // Given
        var mock = new SpeedTestMock
        {
            GetServersAsyncFunc = async (cancellationToken) =>
            {
                await Task.Delay(1000, cancellationToken);
                return Array.Empty<IServer>();
            }
        };

        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(200);

        var registrar = new TypeRegistrar();
        registrar.RegisterInstance(typeof(ISpeedTestService), mock);
        registrar.Register(typeof(IClock), typeof(ClockStub));
        registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
        var app = GetCommandAppTester(registrar, cancellationTokenSource.Token);

        // When
        var result = await app.RunAsync();

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output);
    }

    [Fact]
    public async Task Should_Cancel_Continuous_Speed_Tests_When_User_Requests()
    {
        // Given
        var cancellationTokenSource = new CancellationTokenSource();
        var waiter = new SelfCancellingWaiter(10, cancellationTokenSource);

        var registrar = new TypeRegistrar();
        registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
        registrar.Register(typeof(IClock), typeof(IncrementingClockStub));
        registrar.RegisterInstance(typeof(IWaiter), waiter);
        var app = GetCommandAppTester(registrar, cancellationTokenSource.Token);

        // When
        var result = await app.RunAsync("-t", "--loop",  "--verbosity", "Minimal");

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output);
    }

    [Fact]
    public async Task Should_Cancel_Multiple_Speed_Tests_When_User_Requests()
    {
        // Given
        var cancellationTokenSource = new CancellationTokenSource();
        var waiter = new SelfCancellingWaiter(10, cancellationTokenSource);

        var registrar = new TypeRegistrar();
        registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
        registrar.Register(typeof(IClock), typeof(IncrementingClockStub));
        registrar.RegisterInstance(typeof(IWaiter), waiter);
        var app = GetCommandAppTester(registrar, cancellationTokenSource.Token);

        // When
        var result = await app.RunAsync("-t", "--count", "100", "--verbosity", "Minimal");

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output);
    }

    [Fact]
    public async Task Should_Continue_Multiple_Speed_Tests_On_Exception()
    {
        // Given
        var cancellationTokenSource = new CancellationTokenSource();
        var waiter = new SelfCancellingWaiter(10, cancellationTokenSource);

        // Create a stateful fault function that tracks calls
        var downloadCallCount = 0;
        var faultyTester = new FaultySpeedTester(
            inner: new SpeedTestStub(),
            isFaulted: (sponsor, methodName) =>
            {
                if (methodName == nameof(ISpeedTestService.GetDownloadSpeedAsync))
                {
                    downloadCallCount++;
                    return downloadCallCount == 2; // Fail only on the second call
                }
                return false; // Don't fail other methods
            }
        );

        var registrar = new TypeRegistrar();
        registrar.RegisterInstance(typeof(ISpeedTestService), faultyTester);
        registrar.Register(typeof(IClock), typeof(IncrementingClockStub));
        registrar.RegisterInstance(typeof(IWaiter), waiter);
        var app = GetCommandAppTester(registrar, cancellationTokenSource.Token);

        // When
        var result = await app.RunAsync("-t", "--count", "100", "--verbosity", "Minimal");

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output);
    }

    #endregion

    #region CommandApp

    [InlineData("-h")]
    [InlineData("--help")]
    [InlineData("-?")]
    [Theory]
    public async Task Should_Display_Help(string help)
    {
        // Given
        var registrar = new TypeRegistrar();
        registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
        registrar.Register(typeof(IClock), typeof(ClockStub));
        registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
        var app = GetCommandAppTester(registrar);

        // When
        var result = await app.RunAsync(help);

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output).DisableRequireUniquePrefix();
    }

    [InlineData("-v")]
    [InlineData("--version")]
    [Theory]
    public async Task Should_Display_Version(string version)
    {
        // Given
        var registrar = new TypeRegistrar();
        registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
        registrar.Register(typeof(IClock), typeof(ClockStub));
        registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
        var app = GetCommandAppTester(registrar);

        // When
        var result = await app.RunAsync(version);

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output).DisableRequireUniquePrefix();
    }

    #endregion

}
