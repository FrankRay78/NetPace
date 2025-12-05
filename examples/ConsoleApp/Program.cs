// This example omits exception handling for brevity. However, network operations
// are inherently unreliable and you should assume that every method call may throw.
// NetPace.Core propagates all exceptions to the caller; handle them appropriately.

using NetPace.Core;
using NetPace.Core.Clients.Ookla;

// Create an Ookla network speed test client.
var speedTester = new OoklaSpeedtest() as ISpeedTestService;

// Get the fastest server from those available.
var servers = await speedTester.GetServersAsync();
var fastest = await speedTester.GetFastestServerByLatencyAsync(servers);

// Perform download and upload speed tests.
var downloadResult = await speedTester.GetDownloadSpeedAsync(fastest.Server);
var uploadResult = await speedTester.GetUploadSpeedAsync(fastest.Server);

// Display the server, latency, download and upload speeds.
Console.WriteLine($"{fastest.Server.Sponsor} ({fastest.LatencyMilliseconds} ms)");
Console.WriteLine($"Download: {downloadResult.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI)}");
Console.WriteLine($"Upload: {uploadResult.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI)}");