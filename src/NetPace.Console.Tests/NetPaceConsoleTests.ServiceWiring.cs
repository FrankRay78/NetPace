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
}
