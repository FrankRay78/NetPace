// Exception handling is omitted for brevity. NetPace.Core propagates every exception to the
// caller, so a real consumer handles them.

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
