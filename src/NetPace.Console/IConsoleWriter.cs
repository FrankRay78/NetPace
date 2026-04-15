using NetPace.Core;

namespace NetPace.Console;

/// <summary>
/// Interface for writing speed test results to the console.
/// </summary>
public interface IConsoleWriter
{
    Task PerformSpeedTestAsync(bool initialSpeedTest, IAnsiConsole console, IClock clock, IClientInfoProvider clientInfoProvider, ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, CancellationToken cancellationToken);
}
