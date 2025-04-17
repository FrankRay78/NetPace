# NetPace.Core

Network speed test library including server discovery, latency measurement, download and upload speed testing.

<br/>

## Quick Start

```csharp
using NetPace.Core;
using NetPace.Core.Clients;
using NetPace.Core.DataTypes;
using NetPace.Core.Extensions;

var speedTester = new OoklaSpeedtest() as ISpeedTestService;

var servers = await speedTester.GetServersAsync();
var fastestServer = await speedTester.GetFastestServerByLatencyAsync(servers) ?? default;

Console.WriteLine($"{fastestServer.server.Sponsor} ({fastestServer.latency} ms)");

var downloadResult = await speedTester.GetDownloadSpeedAsync(fastestServer.server);
Console.WriteLine($"Download: {downloadResult.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI)}");

var uploadResult = await speedTester.GetUploadSpeedAsync(fastestServer.server);
Console.WriteLine($"Upload: {uploadResult.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI)}");
```

See [full example](https://github.com/FrankRay78/NetPace/tree/main/examples/ConsoleApp/Program.cs) on GitHub.

<br/>


## API Overview

`ISpeedTestService` is the main interface you'll interact with.

```csharp
public interface ISpeedTestService
{
    public Task<IServer[]> GetServersAsync();
    public Task<int?> GetServerLatencyAsync(IServer server);
    public Task<(IServer server, int latency)?> GetFastestServerByLatencyAsync(IServer[] servers);

    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server);
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, Action<int> UpdateProgress);

    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server);
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, Action<int> UpdateProgress);
}
```

See [ISpeedTestService](https://github.com/FrankRay78/NetPace/tree/main/src/NetPace.Core/ISpeedTestService.cs) on GitHub.
