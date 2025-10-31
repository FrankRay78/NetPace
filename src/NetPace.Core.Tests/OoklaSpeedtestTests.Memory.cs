using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using NetPace.Core.Clients.Ookla;
using RichardSzalay.MockHttp;
using Shouldly;

namespace NetPace.Core.Tests;

public sealed partial class OoklaSpeedtestTests
{
    public sealed class Memory
    {
        [Trait("Category", "Memory")]
        [Fact]
        public void OoklaSpeedtest_Should_Remain_In_Reasonable_Memory_Limits()
        {
            var job = Job.Dry
                .WithLaunchCount(0)
                .WithWarmupCount(1)
                .WithIterationCount(1000)
                .WithInvocationCount(1)
                .WithUnrollFactor(1);

            var config = DefaultConfig.Instance
                .WithOptions(ConfigOptions.DisableOptimizationsValidator) // Allow Debug if needed
                .AddJob(job)
                .AddDiagnoser(MemoryDiagnoser.Default)
                .AddLogger(ConsoleLogger.Default)
                .AddExporter(JsonExporter.Full, MarkdownExporter.GitHub);

            var summary = BenchmarkRunner.Run<OoklaMemoryBench>(config);
        }
    }
}
