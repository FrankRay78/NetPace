using System.Text.Json;
using NetPace.Console;
using NetPace.Core.Clients.LibreSpeed;
using NetPace.Core.Clients.Ookla;

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
        public async Task Provider_LibreSpeed_WithAServerUrl_ConfiguresALibreSpeedRun()
        {
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
            await Verify(result.Output);
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
            Assert.Contains(LibreSpeedUrl, libreSpeed.Output);

            static string[] FieldsOf(string json) =>
                JsonDocument.Parse(json).RootElement.EnumerateObject().Select(p => p.Name).ToArray();
        }

        [Fact]
        public async Task Provider_LibreSpeed_CsvAndJsonNumbersAgree()
        {
            // SCENARIO: LibreSpeed result in CSV and JSON

            // Given / When one LibreSpeed run reports as CSV and another as JSON.
            var csv = await GetCommandLineTestHost(StubServices())
                .RunAsync(["--provider", "librespeed", "--server-url", LibreSpeedUrl, "--csv"]);
            var json = await GetCommandLineTestHost(StubServices())
                .RunAsync(["--provider", "librespeed", "--server-url", LibreSpeedUrl, "--json"]);

            // Then every figure one carries reads the same in the other. Both formats hold plain
            // numbers in fixed units, so they are compared value for value under their shared names.
            var lines = csv.Output.ReplaceLineEndings("\n").TrimStart('﻿').Split('\n');
            var cells = lines[0].Split(',').Zip(lines[1].Split(','))
                .ToDictionary(pair => pair.First, pair => pair.Second);
            var fields = JsonDocument.Parse(json.Output).RootElement;

            foreach (var field in fields.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Number))
            {
                Assert.Equal(field.Value.GetRawText(), cells[field.Name]);
            }
        }

        [Theory]
        [InlineData("--csv")]
        [InlineData("--json")]
        [InlineData("--minimal")]
        [InlineData("--simple")]
        [InlineData(null)]
        public async Task Provider_LibreSpeed_ReportsInEveryOutputFormat(string? format)
        {
            // SCENARIO: LibreSpeed result in CSV and JSON

            // Given a LibreSpeed run in each format NetPace reports in. The test host cannot
            // redraw, so the default format is captured as the simple output it falls back to
            // there, not as the live table.
            string[] args = format is null
                ? ["--provider", "librespeed", "--server-url", LibreSpeedUrl]
                : ["--provider", "librespeed", "--server-url", LibreSpeedUrl, format];

            // When
            var result = await GetCommandLineTestHost(StubServices()).RunAsync(args);

            // Then the result is reported. Only CSV and JSON carry server fields, so only their
            // snapshots name the URL; --simple and --minimal report figures alone, as they do for an
            // Ookla run.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(format ?? "default");
        }

        [Theory]
        [InlineData("--no-download")]
        [InlineData("--no-upload")]
        public async Task Provider_LibreSpeed_SkipsTheTestTheUserTurnedOff(string option)
        {
            // Given / When a LibreSpeed run with one transfer turned off.
            var result = await GetCommandLineTestHost(StubServices())
                .RunAsync(["--provider", "librespeed", "--server-url", LibreSpeedUrl, option, "--csv"]);

            // Then the run completes and the skipped test's cells are empty, as for an Ookla run.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(option);
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

        [Fact]
        public void CreateSpeedTestService_BuildsTheProviderThatSpeaksTheSettingsRecordsProtocol()
        {
            // Given the settings records a run can be configured with.
            // When each is resolved to the service that will do the measuring.
            // Then the provider matches the record, so a LibreSpeed run speaks to LibreSpeed: this
            // is the only step that turns --provider into a provider, and a run that took the wrong
            // one would ask a LibreSpeed server for latency.txt.
            Assert.IsType<OoklaSpeedtest>(Program.CreateSpeedTestService(new OoklaSpeedtestSettings()));
            Assert.IsType<LibreSpeedtest>(Program.CreateSpeedTestService(new LibreSpeedtestSettings()));
        }

        [Theory]
        [InlineData("ookla", typeof(OoklaSpeedtest))]
        [InlineData("librespeed", typeof(LibreSpeedtest))]
        public async Task Provider_EachAcceptedValue_RunsOnTheMatchingProvider(string provider, Type expected)
        {
            // Given a run naming each accepted --provider value.
            var (services, accessor) = BuildServicesWithSettingsAccessor();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--provider", provider, "--server-url", LibreSpeedUrl]);

            // Then the settings the option built resolve to that provider, so the option reaches
            // the measurement rather than stopping at the settings record.
            Assert.Equal(0, result.ExitCode);
            Assert.IsType(expected, Program.CreateSpeedTestService(accessor.Settings));
        }
    }
}
