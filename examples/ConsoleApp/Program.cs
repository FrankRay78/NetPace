using NetPace.Core;
using NetPace.Core.Clients;


// The following example code demonstrates how to use NetPace.Core library.
// Error handling, null and empty array checks have been omitted for brevity.

// Instantiate a speed tester
//var speedTester = new OoklaSpeedtest() as ISpeedTestService;
var speedTester = new SpeedTestStub() as ISpeedTestService;

// Get a list of available speed test servers
var servers = await speedTester.GetServersAsync();

// Get the fastest speed test server
var fastestServer = await speedTester.GetFastestServerByLatencyAsync(servers) ?? default;

// Display the sever name and latency
Console.WriteLine($"{fastestServer.server.Sponsor} ({fastestServer.latency} ms)");