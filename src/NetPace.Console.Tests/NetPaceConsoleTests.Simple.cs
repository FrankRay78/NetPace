using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;

namespace NetPace.Console.Tests;

/// <summary>
/// The <c>--simple</c> format: one line per figure, named and unstyled, for someone moving a
/// line-based script over from <c>speedtest-cli</c>.
/// </summary>
public sealed partial class NetPaceConsoleTests
{
    public sealed class Simple
    {
        private static ServiceCollection StubServices()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            return services;
        }

        private static CommandLineTestHost HostWith(ISpeedTestService service)
        {
            var services = new ServiceCollection();
            services.AddSingleton(service);
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            return new CommandLineTestHost(services);
        }

        /// <summary>
        /// The output's lines, with the byte-order mark and any blank line dropped, as a
        /// line-based reader sees them.
        /// </summary>
        private static string[] Lines(string output) =>
            output.TrimStart('﻿').Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        /// <summary>
        /// The one line holding <paramref name="label"/>, as a <c>grep</c> in a pipeline picks it
        /// out — named rather than taken by position, so a test says which figure it is reading.
        /// </summary>
        private static string LineHolding(string output, string label) =>
            Lines(output).Single(line => line.Contains(label, StringComparison.Ordinal));

        /// <summary>
        /// What a <c>grep '^Download:' | awk '{print $2}'</c> pipeline reads out of the output: the
        /// second space-separated word of the line holding <paramref name="label"/>.
        /// </summary>
        private static string SecondWordOfLineHolding(string output, string label) =>
            LineHolding(output, label).Split(' ', StringSplitOptions.RemoveEmptyEntries)[1];

        [Fact]
        public async Task Simple_Reports_Ping_Download_And_Upload_Each_On_Its_Own_Line()
        {
            // SCENARIO: Simple output reports three lines

            // Given a run of all three tests
            var host = GetCommandLineTestHost(StubServices());

            // When
            var result = await host.RunAsync(["--simple"]);

            // Then the whole output is a Ping line, a Download line and an Upload line, in that
            // order, and nothing else - which the snapshot carries.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Simple_Gives_The_Download_Speed_As_The_Second_Word_Of_Its_Line()
        {
            // SCENARIO: A line-based parser reads the download speed

            // Given a --simple result
            var host = GetCommandLineTestHost(StubServices());

            // When a script takes the second word of the line holding Download
            var result = await host.RunAsync(["--simple"]);
            var word = SecondWordOfLineHolding(result.Output, "Download");

            // Then it is the download speed's number, and nothing else on that line is.
            Assert.Equal(0, result.ExitCode);
            Assert.True(double.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out var speed));
            Assert.True(speed > 0);
        }

        [Fact]
        public async Task Simple_Figures_Are_The_Ones_Default_Output_Reports()
        {
            // Given the same run written in each human-readable format
            var simple = await GetCommandLineTestHost(StubServices()).RunAsync(["--simple"]);
            var byDefault = await GetCommandLineTestHost(StubServices()).RunAsync([]);

            // Then each figure reads the same in both, so neither format is formatting it alone.
            // Each is checked non-empty first: Figure returns "" for a label it cannot find, so a
            // label renamed on both sides would otherwise compare "" with "" and pass vacuously.
            Assert.Equal(0, simple.ExitCode);
            Assert.Equal(0, byDefault.ExitCode);
            Assert.NotEmpty(Figure(byDefault.Output, "Download"));
            Assert.NotEmpty(Figure(byDefault.Output, "Upload"));
            Assert.NotEmpty(Figure(byDefault.Output, "Latency"));
            Assert.Equal(Figure(byDefault.Output, "Download"), Figure(simple.Output, "Download"));
            Assert.Equal(Figure(byDefault.Output, "Upload"), Figure(simple.Output, "Upload"));
            Assert.Equal(Figure(byDefault.Output, "Latency"), Figure(simple.Output, "Ping"));

            static string Figure(string output, string label) =>
                Regex.Match(output, $@"{label}: ([^,\r\n]+)").Groups[1].Value;
        }

        [Theory]
        [InlineData("--no-latency")]
        [InlineData("--no-download")]
        [InlineData("--no-upload")]
        public async Task Simple_Omits_The_Line_Of_A_Test_That_Did_Not_Run(string skipSwitch)
        {
            // SCENARIO: A skipped test has no line

            // Given a run with one test skipped
            var host = GetCommandLineTestHost(StubServices());

            // When
            var result = await host.RunAsync(["--simple", skipSwitch]);

            // Then that test contributes no line - never a zero that reads as a measurement - and
            // the other two are untouched, which the snapshot carries.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(skipSwitch);
        }

        [Theory]
        [InlineData(32, 32)]
        [InlineData(32, 5)]
        public async Task Simple_Notes_Failed_Requests_On_That_Tests_Line(int attempts, int failed)
        {
            // SCENARIO: Failed requests are noted on their line

            // Given a run in which some or all upload requests fail
            var service = new ScriptedSpeedTester
            {
                UploadFactory = _ => failed == attempts
                    ? ScriptedSpeedTester.AllFailed(attempts)
                    : ScriptedSpeedTester.Partial(attempts, failed)
            };

            // When
            var result = await HostWith(service).RunAsync(["--simple"]);

            // Then the Upload line carries the speed and how many requests failed, and no other
            // line does, which the snapshot carries.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(attempts, failed);
        }

        [Fact]
        public async Task Simple_With_Timestamp_Writes_It_On_Its_Own_First_Line()
        {
            // SCENARIO: A timestamp has its own first line

            // Given a --simple run asked for a timestamp
            var host = GetCommandLineTestHost(StubServices());

            // When
            var result = await host.RunAsync(["--simple", "--timestamp"]);

            // Then the timestamp is alone above the figures, labelled like every other line so one
            // parser reads them all, which the snapshot carries.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Show_Simple_Speeds_In_The_Unit_The_Switches_Ask_For()
        {
            // SCENARIO: Unit switches still shape human-readable output
            // SCENARIO: Units follow the unit options

            // Given the same speed test run twice, once with the default units and once with every
            // unit switch set to something else.
            var defaultUnits = await GetCommandLineTestHost(StubServices()).RunAsync(["--simple"]);
            var switchedUnits = await GetCommandLineTestHost(StubServices()).RunAsync(
                ["--simple", "--unit", "bytes", "--unit-scale", "Mega", "--unit-system", "IEC"]);

            // Then neither run reports an error.
            Assert.Equal(0, defaultUnits.ExitCode);
            Assert.Equal(0, switchedUnits.ExitCode);

            // And each run shows its speeds in the unit its command line asked for, which the
            // snapshot carries. The switched run shows 0 MiBps because the stub measures about
            // 1 kB/s down and 2.3 kB/s up, which a Mega scale over IEC bytes rounds to zero.
            await Verify(defaultUnits.Output + switchedUnits.Output);
        }

        [Fact]
        public async Task Should_Not_Include_IPAddress_Or_Hostname_In_Simple_Output()
        {
            // SCENARIO: Minimal output does not include IPAddress or Hostname

            // Given
            var services = StubServices();
            services.AddSingleton<IClientInfoProvider>(new ClientInfoProviderStub());
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--simple"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Not_Include_IPAddress_Or_Hostname_In_Simple_Output_With_Stub_Providing_Error_Values()
        {
            // SCENARIO: Minimal output remains clean even when IClientInfoProvider reports ERROR values

            // Given
            var services = StubServices();
            services.AddSingleton<IClientInfoProvider>(new ClientInfoProviderErrorStub());
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--simple"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Simple_Sends_No_Under_Load_Probes()
        {
            // SCENARIO: Simple output sends no probes

            // Given a --simple run
            var (services, accessor) = BuildServicesWithSettingsAccessor();
            var host = GetCommandLineTestHost(services);

            // When its transfers run
            var result = await host.RunAsync(["--simple"]);

            // Then no under-load probe goes out: this format shows no under-load figure, so its
            // traffic is as it was before the probes existed.
            Assert.Equal(0, result.ExitCode);
            Assert.False(accessor.Settings.LatencyTest.LoadedLatencyEnabled);
        }

        [Fact]
        public async Task Default_Output_Still_Sends_Under_Load_Probes()
        {
            // Given a default-format run
            var (services, accessor) = BuildServicesWithSettingsAccessor();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([]);

            // Then opting out is --simple's alone, and nothing else lost its probes with it.
            Assert.Equal(0, result.ExitCode);
            Assert.True(accessor.Settings.LatencyTest.LoadedLatencyEnabled);
        }

        [Theory]
        [InlineData("--minimal")]
        [InlineData("--minimal", "true")]
        public async Task Minimal_Is_Refused_With_A_Message_Naming_Simple(params string[] arguments)
        {
            // SCENARIO: The old switch says what replaced it

            // Given a script still passing the old switch
            var host = GetCommandLineTestHost(StubServices());

            // When NetPace starts
            var result = await host.RunAsync(arguments);

            // Then it fails and names the switch to use instead.
            Assert.Equal(1, result.ExitCode);
            Assert.Contains("--minimal", result.Output, StringComparison.Ordinal);
            Assert.Contains("--simple", result.Output, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("--csv")]
        [InlineData("--json")]
        [InlineData("--json-pretty")]
        public async Task Simple_With_Another_Format_Is_An_Error_Naming_The_Conflict(string otherFormat)
        {
            // Given
            var host = GetCommandLineTestHost(StubServices());

            // When
            var result = await host.RunAsync(["--simple", otherFormat]);

            // Then
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("Only one output format may be specified", result.Output, StringComparison.Ordinal);
            Assert.Contains("--simple", result.Output, StringComparison.Ordinal);
            Assert.Contains(otherFormat, result.Output, StringComparison.Ordinal);
        }
    }
}
