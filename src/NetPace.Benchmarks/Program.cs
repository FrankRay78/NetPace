using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using NetPace.Core.Clients.Ookla;

namespace NetPace.Core.Benchmarks;

class Program
{
    static void Main(string[] args)
    {
        var config = DefaultConfig.Instance
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }
}

/// <summary>
/// Represents a memory benchmarking class for measuring network speed using Ookla's speed test service.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 0, iterationCount: 1)]
public class OoklaMemoryBench
{
    private ISpeedTestService speedtest = null!;
    private IServer server = null!;

    /// <summary>
    /// Global setup for the benchmark: creates the speed test client and selects the fastest server.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        // Create an Ookla network speed test client.
        speedtest = new OoklaSpeedtest();

        // Get the fastest server from those available.
        var servers = speedtest.GetServersAsync().GetAwaiter().GetResult();
        server = speedtest.GetFastestServerByLatencyAsync(servers).GetAwaiter().GetResult().Server;
    }

    /// <summary>
    /// Measures download speed against the selected server.
    /// </summary>
    [Benchmark]
    public Task Download() => speedtest.GetDownloadSpeedAsync(server);

    /// <summary>
    /// Measures upload speed against the selected server.
    /// </summary>
    [Benchmark]
    public Task Upload() => speedtest.GetUploadSpeedAsync(server);
}
