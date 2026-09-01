using NetPace.Console;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    /// <summary>
    /// Regression: `netpace --test` reported "No service for type
    /// 'NetPace.Console.OoklaSpeedtestSettingsAccessor' has been registered". Driven through
    /// <see cref="Program.Main"/> rather than <see cref="CommandLineTestHost"/> on purpose — the
    /// test host supplies its own default registrations, which mask gaps in the production container.
    /// </summary>
    [Fact]
    public async Task Main_TestSwitch_RunsSpeedTestSuccessfully()
    {
        // Given/When — latency only, so the stub's per-step delay keeps the test quick.
        var exitCode = await Program.Main(["--test", "--no-download", "--no-upload"]);

        // Then
        Assert.Equal(0, exitCode);
    }

    /// <summary>
    /// Regression: <see cref="Program.Main"/> subscribed a Ctrl+C handler to the process-wide
    /// <c>Console.CancelKeyPress</c> event and never removed it, leaving a handler reachable over a
    /// disposed <see cref="CancellationTokenSource"/> for the rest of the process's life.
    /// </summary>
    [Fact]
    public async Task Main_CanBeCalledTwiceInTheSameProcess()
    {
        // Given/When Main runs twice, as it now does in this test host.
        var first = await Program.Main(["--test", "--no-download", "--no-upload"]);
        var second = await Program.Main(["--test", "--no-download", "--no-upload"]);

        // Then neither run leaves the other's cancellation plumbing behind.
        Assert.Equal(0, first);
        Assert.Equal(0, second);
    }
}
