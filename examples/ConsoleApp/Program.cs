using NetPace.Core;
using NetPace.Core.Clients;


// Instantiate a speed tester
var speedTester = new OoklaSpeedtest() as ISpeedTestService;

// Get a list of available speed test servers
var servers = await speedTester.GetServersAsync();

// Get the fastest speed test server
var fastestServer = await speedTester.GetFastestServerByLatencyAsync(servers) ?? default;

// Display the sever name and latency
Console.WriteLine($"{fastestServer.server.Sponsor} ({fastestServer.latency} ms)");