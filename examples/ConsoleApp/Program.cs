using NetPace.Core;
using NetPace.Core.Clients.Ookla;


// The following example code demonstrates how to use NetPace.Core library.
// Error handling, null and empty array checks have been omitted for brevity.


// Instantiate a speed tester
var speedTester = new OoklaSpeedtest() as ISpeedTestService;

// Get a list of available speed test servers
var servers = await speedTester.GetServersAsync();

// Get the fastest speed test server
var fastestServer = await speedTester.GetFastestServerByLatencyAsync(servers);

// Display the fastest server and latency
Console.WriteLine($"{fastestServer.Server.Sponsor} ({fastestServer.Latency} ms)");

// Perform download speed test
var downloadResult = await speedTester.GetDownloadSpeedAsync(fastestServer.Server);

// Display the download speed
Console.WriteLine($"Download: {downloadResult.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI)}");

// Perform upload speed test
var uploadResult = await speedTester.GetUploadSpeedAsync(fastestServer.Server);

// Display the upload speed
Console.WriteLine($"Upload: {uploadResult.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI)}");