using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;

namespace NetPace.Console.Tests;

/// <summary>
/// The <c>--minimal</c> format: one timestamped line per run, for someone leaving NetPace looping
/// in a terminal and scanning the lines that scroll past for a slow period.
/// </summary>
public sealed partial class NetPaceConsoleTests
{
    public sealed class Minimal
    {
        private static CommandLineTestHost HostWith(ISpeedTestService service)
        {
            var services = new ServiceCollection();
            services.AddSingleton(service);
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            return new CommandLineTestHost(services);
        }

        /// <summary>
        /// The stub registrations with a clock that moves between runs, so a repeated run shows a
        /// different timestamp on each of its lines.
        /// </summary>
        private static ServiceCollection StubServicesOnAMovingClock()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, IncrementingClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            return services;
        }

        [Fact]
        public async Task Minimal_Writes_One_Line_Opening_With_The_Timestamp()
        {
            // SCENARIO: Minimal output is one timestamped line

            // Given a run of all three tests
            var host = GetCommandLineTestHost(StubServices());

            // When
            var result = await host.RunAsync(["--minimal"]);

            // Then the whole output is one line, it opens with the local timestamp, and the latency,
            // download and upload figures follow it, which the snapshot carries.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Minimal_Under_Count_Writes_One_Timestamped_Line_Per_Run()
        {
            // SCENARIO: Repeated minimal runs write one line each

            // Given --minimal asked for two runs, on a clock that moves between them
            var host = GetCommandLineTestHost(StubServicesOnAMovingClock());

            // When both runs complete
            var result = await host.RunAsync(["--minimal", "--count", "2"]);

            // Then the output is two lines, each opening with its own timestamp, which the snapshot
            // carries.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Minimal_Under_File_Writes_One_Line_Per_Run()
        {
            // Given a --file target and two runs asked for
            var testFile = Path.Join(Path.GetTempPath(), $"netpace-test-{Guid.NewGuid()}.txt");

            try
            {
                var host = GetCommandLineTestHost(StubServicesOnAMovingClock());

                // When
                var result = await host.RunAsync(["--minimal", "--count", "2", "--file", testFile]);

                // Then the file holds one line per run, which the snapshot carries.
                Assert.Equal(0, result.ExitCode);
                var fileContent = await System.IO.File.ReadAllTextAsync(testFile);
                await Verify(fileContent);
            }
            finally
            {
                if (System.IO.File.Exists(testFile))
                {
                    System.IO.File.Delete(testFile);
                }
            }
        }

        [Theory]
        [InlineData("--no-latency")]
        [InlineData("--no-download")]
        [InlineData("--no-upload")]
        public async Task Minimal_Omits_The_Field_Of_A_Test_That_Did_Not_Run(string skipSwitch)
        {
            // SCENARIO: A skipped test has no field in minimal output

            // Given a run with one test skipped
            var host = GetCommandLineTestHost(StubServices());

            // When
            var result = await host.RunAsync(["--minimal", skipSwitch]);

            // Then the line still opens with the timestamp, that test contributes no field - never a
            // zero that reads as a measurement - and the others are untouched, which the snapshot
            // carries.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(skipSwitch);
        }

        [Theory]
        [InlineData(32, 32)]
        [InlineData(32, 5)]
        public async Task Minimal_Notes_Failed_Requests_On_That_Tests_Figure(int attempts, int failed)
        {
            // Given a run in which some or all upload requests fail
            var service = new ScriptedSpeedTester
            {
                UploadFactory = _ => failed == attempts
                    ? ScriptedSpeedTester.AllFailed(attempts)
                    : ScriptedSpeedTester.Partial(attempts, failed)
            };

            // When
            var result = await HostWith(service).RunAsync(["--minimal"]);

            // Then the Upload figure carries the speed and how many requests failed, and no other
            // figure does, which the snapshot carries.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(attempts, failed);
        }

        [Fact]
        public async Task Minimal_Writes_A_Failed_Runs_Error_Line_Without_A_Timestamp()
        {
            // Given a run whose server discovery fails
            var service = new SpeedTestMock
            {
                GetServersAsyncFunc = _ => throw new HttpRequestException("Could not open socket")
            };

            // When
            var result = await HostWith(service).RunAsync(["--minimal"]);

            // Then the line is the bare Error: message, with no timestamp in front of it: this
            // format is a quick shorthand, not a detailed log.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Theory]
        [InlineData("")]
        [InlineData("--unit bytes --unit-scale Kilo --unit-system IEC")]
        public async Task Minimal_Figures_Are_The_Ones_Simple_Output_Reports(string unitSwitches)
        {
            // Given the same run written in each one-line and line-per-figure format, under the same
            // unit switches - Kilo rather than Mega, so the stub's figures do not round to zero.
            string[] units = unitSwitches.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var minimal = await GetCommandLineTestHost(StubServices()).RunAsync(["--minimal", .. units]);
            var simple = await GetCommandLineTestHost(StubServices()).RunAsync(["--simple", .. units]);

            // Then every figure --simple reports reads the same inside the one --minimal line, so
            // neither format is formatting it alone. Each is checked non-empty first, so a label
            // renamed on both sides cannot compare "" with "" and pass vacuously.
            Assert.Equal(0, minimal.ExitCode);
            Assert.Equal(0, simple.ExitCode);

            foreach ((string simpleLabel, string minimalLabel) in new[] { ("Ping", "Latency"), ("Download", "Download"), ("Upload", "Upload") })
            {
                var figure = Figure(simple.Output, simpleLabel);

                Assert.NotEmpty(figure);
                Assert.Contains($"{minimalLabel}: {figure}", minimal.Output, StringComparison.Ordinal);
            }

            static string Figure(string output, string label) =>
                Regex.Match(output, $@"^{label}: (.+)$", RegexOptions.Multiline).Groups[1].Value.Trim();
        }

        [Fact]
        public async Task Minimal_Sends_No_Under_Load_Probes()
        {
            // SCENARIO: Minimal output sends no probes

            // Given a --minimal run
            var (services, accessor) = BuildServicesWithSettingsAccessor();
            var host = GetCommandLineTestHost(services);

            // When its transfers run
            var result = await host.RunAsync(["--minimal"]);

            // Then no under-load probe goes out: this format shows no under-load figure, so its
            // traffic is as it was before the probes existed.
            Assert.Equal(0, result.ExitCode);
            Assert.False(accessor.Settings.LatencyTest.LoadedLatencyEnabled);
        }

        [Theory]
        [InlineData("--simple")]
        [InlineData("--csv")]
        [InlineData("--json")]
        [InlineData("--json-pretty")]
        public async Task Minimal_With_Another_Format_Is_An_Error_Naming_The_Conflict(string otherFormat)
        {
            // SCENARIO: Minimal conflicts with another format

            // Given
            var host = GetCommandLineTestHost(StubServices());

            // When NetPace starts
            var result = await host.RunAsync(["--minimal", otherFormat]);

            // Then
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("Only one output format may be specified", result.Output, StringComparison.Ordinal);
            Assert.Contains("--minimal", result.Output, StringComparison.Ordinal);
            Assert.Contains(otherFormat, result.Output, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Minimal_Composes_With_The_Result_Shaping_Options()
        {
            // Given
            var host = GetCommandLineTestHost(StubServices());

            // When the one-line format is combined with unit and scale selection
            var result = await host.RunAsync(["--minimal", "--unit", "bytes", "--unit-scale", "Kilo", "--unit-system", "IEC"]);

            // Then the one line still opens with the timestamp and carries the chosen units, which
            // the snapshot carries.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Minimal_Does_Not_Include_IPAddress_Or_Hostname()
        {
            // Given a client-info provider with something to leak
            var services = StubServices();
            services.AddSingleton<IClientInfoProvider>(new ClientInfoProviderStub());
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--minimal"]);

            // Then the line carries the figures alone, which the snapshot carries.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }
    }
}
