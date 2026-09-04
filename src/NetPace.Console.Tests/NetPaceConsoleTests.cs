namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    /// <summary>
    /// Create the CommandLineTestHost with System.CommandLine.
    /// </summary>
    private static CommandLineTestHost GetCommandLineTestHost(IServiceCollection? serviceCollection)
    {
        return new CommandLineTestHost(serviceCollection);
    }

    [Fact]
    public async Task Should_Perform_Speed_Test()
    {
        // Given
        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService, SpeedTestStub>();
        services.AddSingleton<IClock, ClockStub>();
        services.AddSingleton<IWaiter, NoDelayStub>();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync([]);

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output);
    }

    [Fact]
    public async Task Should_Perform_Speed_Test_With_Fixed_Unit_Scale()
    {
        // Given
        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService, SpeedTestStub>();
        services.AddSingleton<IClock, ClockStub>();
        services.AddSingleton<IWaiter, NoDelayStub>();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(["--unit-scale", "Mega"]);

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

        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService, SpeedTestStub>();
        services.AddSingleton<IClock, IncrementingClockStub>();
        services.AddSingleton<IWaiter>(waiter);
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(["-t", "--loop", "--verbosity", "Minimal"], cancellationTokenSource.Token);

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output);
    }

    [InlineData(5)]
    [Theory]
    public async Task Should_Perform_Speed_Test_Multiple_Times(int count)
    {
        // Given
        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService, SpeedTestStub>();
        services.AddSingleton<IClock, IncrementingClockStub>();
        services.AddSingleton<IWaiter, NoDelayStub>();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(["-t", "--count", $"{count}", "--verbosity", "Minimal"]);

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

        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService, SpeedTestStub>();
        services.AddSingleton<IClock, IncrementingClockStub>();
        services.AddSingleton<IWaiter>(waiter);
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(["-t", "--count", $"{count}", "--delay", $"{delay}", "--verbosity", "Minimal"]);

        // Then
        Assert.Equal(count - 1, waiter.CallCount);
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output).UseParameters(count, delay);
    }

    [Fact]
    public async Task Should_Perform_Speed_Test_Multiple_Times_With_Fixed_Scale()
    {
        // Given
        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService, VariableSpeedTester>();
        services.AddSingleton<IClock, IncrementingClockStub>();
        services.AddSingleton<IWaiter, NoDelayStub>();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(["--count", "3", "--unit-scale", "Mega", "--verbosity", "Minimal"]);

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
        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService, SpeedTestStub>();
        services.AddSingleton<IClock, ClockStub>();
        services.AddSingleton<IWaiter, NoDelayStub>();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(["--verbosity", verbosity]);

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
        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService, SpeedTestStub>();
        services.AddSingleton<IClock, ClockStub>();
        services.AddSingleton<IWaiter, NoDelayStub>();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(["--server", url]);

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
        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService, SpeedTestStub>();
        services.AddSingleton<IClock, ClockStub>();
        services.AddSingleton<IWaiter, NoDelayStub>();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(["--csv", "--count", "3", "--unit-scale", "Mega", "--server", url]);

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
        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService, SpeedTestStub>();
        services.AddSingleton<IClock, ClockStub>();
        services.AddSingleton<IWaiter, NoDelayStub>();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync([timestamp]);

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
        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService, SpeedTestStub>();
        services.AddSingleton<IClock, ClockStub>();
        services.AddSingleton<IWaiter, NoDelayStub>();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(["--unit", unit.ToString(), "--unit-system", unitSystem.ToString()]);

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
        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService, SpeedTestStub>();
        services.AddSingleton<IClock, ClockStub>();
        services.AddSingleton<IWaiter, NoDelayStub>();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(["--no-download", "--verbosity", verbosity]);

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
        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService, SpeedTestStub>();
        services.AddSingleton<IClock, ClockStub>();
        services.AddSingleton<IWaiter, NoDelayStub>();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(["--no-upload", "--verbosity", verbosity]);

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
        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService, SpeedTestStub>();
        services.AddSingleton<IClock, ClockStub>();
        services.AddSingleton<IWaiter, NoDelayStub>();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(["--no-download", "--no-upload", "--verbosity", verbosity]);

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output).UseParameters(verbosity);
    }

    [InlineData("Minimal")]
    [InlineData("Normal")]
    [InlineData("Debug")]
    [Theory]
    public async Task Should_Not_Perform_Latency_Test(string verbosity)
    {
        // Given
        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService, SpeedTestStub>();
        services.AddSingleton<IClock, ClockStub>();
        services.AddSingleton<IWaiter, NoDelayStub>();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(["--no-latency", "--verbosity", verbosity]);

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output).UseParameters(verbosity);
    }

    [Fact]
    public async Task Should_Return_Validation_Error_When_No_Tests_Selected()
    {
        // Given
        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService, SpeedTestStub>();
        services.AddSingleton<IClock, ClockStub>();
        services.AddSingleton<IWaiter, NoDelayStub>();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(["--no-latency", "--no-download", "--no-upload"]);

        // Then the validation error is reported on the console.
        Assert.Equal(1, result.ExitCode);
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

        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService>(mock);
        services.AddSingleton<IClock, ClockStub>();
        services.AddSingleton<IWaiter, NoDelayStub>();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(Array.Empty<string>(), cancellationTokenSource.Token);

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

        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService, SpeedTestStub>();
        services.AddSingleton<IClock, IncrementingClockStub>();
        services.AddSingleton<IWaiter>(waiter);
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(["-t", "--loop", "--verbosity", "Minimal"], cancellationTokenSource.Token);

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

        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService, SpeedTestStub>();
        services.AddSingleton<IClock, IncrementingClockStub>();
        services.AddSingleton<IWaiter>(waiter);
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(["-t", "--count", "100", "--verbosity", "Minimal"], cancellationTokenSource.Token);

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output);
    }

    [Fact]
    public async Task Should_Handle_Configuration_Exceptions()
    {
        // Given
        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService, SpeedTestStub>();
        services.AddSingleton<IClock, ClockStub>();
        services.AddSingleton<IWaiter, NoDelayStub>();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(["--count", "ABC"]);

        // Then
        Assert.Equal(1, result.ExitCode);
        await Verify(result.Output);
    }

    [Fact]
    public async Task Should_Handle_Network_Exceptions()
    {
        // Given
        var mock = new SpeedTestMock
        {
            GetServersAsyncFunc = (cancellationToken) => throw new HttpRequestException("Could not open socket")
        };

        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService>(mock);
        services.AddSingleton<IClock, ClockStub>();
        services.AddSingleton<IWaiter, NoDelayStub>();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(Array.Empty<string>());

        // Then an unreachable discovery endpoint is a reported data outcome (exit 0), surfaced on the console.
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

        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService>(faultyTester);
        services.AddSingleton<IClock, IncrementingClockStub>();
        services.AddSingleton<IWaiter>(waiter);
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(["-t", "--count", "100", "--verbosity", "Minimal"], cancellationTokenSource.Token);

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output);
    }

    [Fact]
    public async Task Should_Continue_Multiple_Speed_Tests_When_A_Measurement_All_Fails()
    {
        // A measurement that all-fails is data, not an error: the loop keeps running and the exit
        // code stays 0 (network conditions never set it by default).

        // Given the second iteration's download all-fails, the rest measure cleanly.
        var cancellationTokenSource = new CancellationTokenSource();
        var waiter = new SelfCancellingWaiter(10, cancellationTokenSource);

        var service = new ScriptedSpeedTester
        {
            DownloadFactory = i => i == 1 ? ScriptedSpeedTester.AllFailed(150) : ScriptedSpeedTester.Clean(150)
        };

        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService>(service);
        services.AddSingleton<IClock, IncrementingClockStub>();
        services.AddSingleton<IWaiter>(waiter);
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(["-t", "--count", "100", "--verbosity", "Minimal"], cancellationTokenSource.Token);

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output);
    }

    [Fact]
    public async Task Should_Handle_No_Servers_Available()
    {
        // Given
        var mock = new SpeedTestMock
        {
            GetServersAsyncFunc = (cancellationToken) => Task.FromResult(Array.Empty<IServer>()),
        };

        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService>(mock);
        services.AddSingleton<IClock, ClockStub>();
        services.AddSingleton<IWaiter, NoDelayStub>();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(Array.Empty<string>());

        // Then the no-servers condition is reported on the console and exits 0.
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output);
    }

    [Fact]
    public async Task Should_Handle_No_Servers_Available_With_NoLatency()
    {
        // Given
        var mock = new SpeedTestMock
        {
            GetServersAsyncFunc = (cancellationToken) => Task.FromResult(Array.Empty<IServer>()),
        };

        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService>(mock);
        services.AddSingleton<IClock, ClockStub>();
        services.AddSingleton<IWaiter, NoDelayStub>();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(["--no-latency"]);

        // Then the no-servers condition is reported on the console and exits 0.
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output);
    }

    [InlineData("-h")]
    [InlineData("--help")]
    [InlineData("-?")]
    [Theory]
    public async Task Should_Display_Help(string help)
    {
        // Given
        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService, SpeedTestStub>();
        services.AddSingleton<IClock, ClockStub>();
        services.AddSingleton<IWaiter, NoDelayStub>();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync([help]);

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
        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService, SpeedTestStub>();
        services.AddSingleton<IClock, ClockStub>();
        services.AddSingleton<IWaiter, NoDelayStub>();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync([version]);

        // Then
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output).DisableRequireUniquePrefix();
    }

}
