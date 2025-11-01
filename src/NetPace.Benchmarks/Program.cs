using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
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

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 0, iterationCount: 1)]
public class OoklaMemoryBench
{
    private ISpeedTestService speedtest = null!;
    private IServer server = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Create an Ookla network speed test client.
        speedtest = new OoklaSpeedtest();

        // Get the fastest server from those available.
        var servers = speedtest.GetServersAsync().GetAwaiter().GetResult();
        server = speedtest.GetFastestServerByLatencyAsync(servers).GetAwaiter().GetResult().Server;
    }

    [Benchmark]
    public Task Download() => speedtest.GetDownloadSpeedAsync(server);

    [Benchmark]
    public Task Upload() => speedtest.GetUploadSpeedAsync(server);
}