# NetPace.Core

Network speed test library including server discovery, latency measurement, download and upload speed testing.

## Quick Start

```csharp
using NetPace.Core;
using NetPace.Core.Clients.Ookla;

var speedTester = new OoklaSpeedtest() as ISpeedTestService;

var servers = await speedTester.GetServersAsync();
var fastest = await speedTester.GetFastestServerByLatencyAsync(servers);

var downloadResult = await speedTester.GetDownloadSpeedAsync(fastest.Server);
var uploadResult = await speedTester.GetUploadSpeedAsync(fastest.Server);

Console.WriteLine($"{fastest.Server.Sponsor} ({fastest.Latency} ms)");
Console.WriteLine($"Download: {downloadResult.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI)}");
Console.WriteLine($"Upload: {uploadResult.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI)}");
```

The example above uses the `OoklaSpeedtest` implementation which uses Ookla Speedtest servers under the hood. Ookla and Speedtest are trademarks of Ookla, LLC; this project is not affiliated with or endorsed by Ookla.

See the [full example](https://github.com/FrankRay78/NetPace/tree/main/examples/ConsoleApp/Program.cs) on GitHub.

## API Overview

`ISpeedTestService` is the primary interface you will interact with.

```csharp
public interface ISpeedTestService
{
    public Task<IServer[]> GetServersAsync(CancellationToken cancellationToken = default);

    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, CancellationToken cancellationToken = default);
    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, IProgress<LatencyTestProgress> progress, CancellationToken cancellationToken = default);
    public Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, CancellationToken cancellationToken = default);
    public Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, IProgress<LatencyTestProgress> progress, CancellationToken cancellationToken = default);

    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, CancellationToken cancellationToken = default);
    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default);

    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, CancellationToken cancellationToken = default);
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, int downloadSizeMb, CancellationToken cancellationToken = default);
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default);
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, int downloadSizeMb, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default);

    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, CancellationToken cancellationToken = default);
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, int uploadSizeMb, CancellationToken cancellationToken = default);
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default);
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, int uploadSizeMb, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default);
}
```

See [ISpeedTestService](https://github.com/FrankRay78/NetPace/tree/main/src/NetPace.Core/ISpeedTestService.cs) on GitHub.

### Testing Your Code

NetPace.Core includes test implementations of `ISpeedTestService` so you can test your code without making real network calls:

- **`SpeedTestStub`** - Simple stub returning fixed values with configurable delays
- **`SpeedTestMock`** - Fully configurable mock with injectable delegate functions
- **`VariableSpeedTester`** - Returns different speeds on each call (simulates variable network conditions)
- **`FaultySpeedTester`** - Simulates network failures and timeouts (for testing error handling)

See the implementations in [NetPace.Core.Clients.Testing](https://github.com/FrankRay78/NetPace/tree/main/src/NetPace.Core/Clients/Testing) and their usage throughout the solution.