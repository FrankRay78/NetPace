using System.Text.Json;
using NetPace.Console;
using NetPace.Core.Clients.LibreSpeed;
using NetPace.Core.Clients.Ookla;
using NetPace.Core.Settings;

namespace NetPace.Console.Tests;

/// <summary>
/// <c>--provider</c>: which kind of server a run tests against, what it needs alongside it, and
/// what it leaves unchanged.
/// </summary>
public sealed partial class NetPaceConsoleTests
{
    public sealed class ProviderOption
    {
        private const string LibreSpeedUrl = "http://pi.local:8080/";

        [Fact]
        public async Task Provider_LibreSpeed_WithAServerUrl_TestsAgainstALibreSpeedServer()
        {
            // SCENARIO: LibreSpeed run reports every measurement

            // Given
            var (services, accessor) = BuildServicesWithSettingsAccessor();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--provider", "librespeed", "--server-url", LibreSpeedUrl]);

            // Then the run is configured for a LibreSpeed server, so the measurement goes there.
            Assert.Equal(0, result.ExitCode);
            Assert.IsType<LibreSpeedtestSettings>(accessor.Settings);
        }

        [Fact]
        public async Task Provider_LibreSpeed_WithoutAServerUrl_IsRejectedBeforeAnyTestRuns()
        {
            // SCENARIO: LibreSpeed without a server URL is rejected

            // Given
            var (services, _) = BuildServicesWithSettingsAccessor();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--provider", "librespeed"]);

            // Then nothing is measured, the user is told a server URL is needed, and the exit code
            // is non-zero: a missing option is a usage error, not a network outcome.
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("--server-url", result.Output);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Provider_UnknownValue_IsRejectedNamingTheAcceptedValues()
        {
            // Given
            var (services, _) = BuildServicesWithSettingsAccessor();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--provider", "fibrebox"]);

            // Then
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("fibrebox", result.Output);
            Assert.Contains("ookla", result.Output);
            Assert.Contains("librespeed", result.Output);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("ookla")]
        public async Task Provider_OmittedOrOokla_RunsAgainstAnOoklaServer(string? provider)
        {
            // SCENARIO: Default provider is unchanged

            // Given
            var (services, accessor) = BuildServicesWithSettingsAccessor();
            var host = GetCommandLineTestHost(services);
            string[] args = provider is null ? [] : ["--provider", provider];

            // When
            var result = await host.RunAsync(args);

            // Then the run is the Ookla run it always was.
            Assert.Equal(0, result.ExitCode);
            var ookla = Assert.IsType<OoklaSpeedtestSettings>(accessor.Settings);
            Assert.Equal(new OoklaSpeedtestSettings(Core.Profile.Medium).DownloadSizes, ookla.DownloadSizes);
        }

        [Fact]
        public async Task Provider_LibreSpeed_IsShapedByTheSameOptionsAsAnOoklaRun()
        {
            // Given
            var (services, accessor) = BuildServicesWithSettingsAccessor();
            var host = GetCommandLineTestHost(services);

            // When the options that shape an Ookla run are given to a LibreSpeed one.
            var result = await host.RunAsync([
                "--provider", "librespeed",
                "--server-url", LibreSpeedUrl,
                "--profile", "large",
                "--connections", "2",
                "--downloadsize", "7",
                "--uploadsize", "3",
            ]);

            // Then each one lands on the LibreSpeed settings exactly as it does on Ookla's.
            Assert.Equal(0, result.ExitCode);
            var settings = Assert.IsType<LibreSpeedtestSettings>(accessor.Settings);
            Assert.Equal(7, settings.DownloadTest.DownloadSizeMb);
            Assert.Equal(3, settings.UploadTest.UploadSizeMb);
            Assert.Equal(2, settings.DownloadTest.DownloadParallelTasks);
            Assert.Equal(2, settings.UploadTest.UploadParallelTasks);

            // And the profile still decides the request shape it did not override.
            var large = new LibreSpeedtestSettings(Core.Profile.Large);
            Assert.Equal(large.DownloadRequestSizeMib, settings.DownloadRequestSizeMib);
            Assert.Equal(large.DownloadRequestCount, settings.DownloadRequestCount);
        }

        [Theory]
        [InlineData("--no-latency")]
        [InlineData("--simple")]
        [InlineData("--minimal")]
        public async Task Provider_LibreSpeed_SwitchesLoadedLatencyOffWhereAnOoklaRunDoes(string option)
        {
            // Given
            var (services, accessor) = BuildServicesWithSettingsAccessor();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--provider", "librespeed", "--server-url", LibreSpeedUrl, option]);

            // Then
            Assert.Equal(0, result.ExitCode);
            Assert.False(accessor.Settings.LatencyTest.LoadedLatencyEnabled);
        }

        [Fact]
        public async Task Provider_LibreSpeed_CsvCarriesTheSameFieldsWithTheServerUrl()
        {
            // SCENARIO: LibreSpeed result in CSV and JSON

            // Given / When a LibreSpeed run and an Ookla run both report as CSV.
            var libreSpeed = await GetCommandLineTestHost(StubServices())
                .RunAsync(["--provider", "librespeed", "--server-url", LibreSpeedUrl, "--csv"]);
            var ookla = await GetCommandLineTestHost(StubServices()).RunAsync(["--csv"]);

            // Then the field set is the same, and the server fields show the URL tested.
            Assert.Equal(0, libreSpeed.ExitCode);
            Assert.Equal(HeaderOf(ookla.Output), HeaderOf(libreSpeed.Output));
            Assert.Contains(LibreSpeedUrl, libreSpeed.Output);
            await Verify(libreSpeed.Output);

            static string HeaderOf(string csv) => csv.ReplaceLineEndings("\n").TrimStart('﻿').Split('\n')[0];
        }

        [Fact]
        public async Task Provider_LibreSpeed_JsonCarriesTheSameFieldsWithTheServerUrl()
        {
            // SCENARIO: LibreSpeed result in CSV and JSON

            // Given / When a LibreSpeed run and an Ookla run both report as JSON.
            var libreSpeed = await GetCommandLineTestHost(StubServices())
                .RunAsync(["--provider", "librespeed", "--server-url", LibreSpeedUrl, "--json"]);
            var ookla = await GetCommandLineTestHost(StubServices()).RunAsync(["--json"]);

            // Then the field names are the same, and the server fields show the URL tested.
            Assert.Equal(0, libreSpeed.ExitCode);
            Assert.Equal(FieldsOf(ookla.Output), FieldsOf(libreSpeed.Output));
            await Verify(libreSpeed.Output);

            static string[] FieldsOf(string json) =>
                JsonDocument.Parse(json).RootElement.EnumerateObject().Select(p => p.Name).ToArray();
        }

        [Fact]
        public async Task Provider_LibreSpeed_ServerThatDoesNotAnswerAsLibreSpeed_IsReportedWithNoMeasurement()
        {
            // SCENARIO: Unreachable LibreSpeed server is reported

            // Given a server that answers, but not as LibreSpeed: the provider names the URL.
            const string rejected = "The server at http://pi.local:8080/backend/empty.php answered, but not as a LibreSpeed server.";
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(new SpeedTestMock
            {
                GetServerLatencyByServerUrlAsyncFunc = (_, _, _) => throw new InvalidOperationException(rejected),
            });
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();

            // When
            var result = await new CommandLineTestHost(services)
                .RunAsync(["--provider", "librespeed", "--server-url", LibreSpeedUrl]);

            // Then the failure is reported naming the URL, with no measurement, and the exit code
            // stays 0: a server that will not answer is a network outcome.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains(rejected, result.Output);
        }

        [Theory]
        [InlineData("total")]
        [InlineData("partial")]
        public async Task Provider_LibreSpeed_ServerThatDoesNotAnswerAsLibreSpeed_ExitsNonZeroUnderFailOn(string failOn)
        {
            // SCENARIO: Unreachable LibreSpeed server is reported

            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(new SpeedTestMock
            {
                GetServerLatencyByServerUrlAsyncFunc = (_, _, _) => throw new InvalidOperationException("not a LibreSpeed server"),
            });
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();

            // When
            var result = await new CommandLineTestHost(services)
                .RunAsync(["--provider", "librespeed", "--server-url", LibreSpeedUrl, "--fail-on", failOn]);

            // Then
            Assert.Equal(1, result.ExitCode);
        }
    }
}
