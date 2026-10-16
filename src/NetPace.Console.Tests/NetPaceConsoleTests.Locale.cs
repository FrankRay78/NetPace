using System.Globalization;

namespace NetPace.Console.Tests;

/// <summary>
/// Every value NetPace writes reads the same way on every machine, whatever the regional settings
/// it runs under: speeds use <c>.</c> as the decimal separator, and the timestamp has one fixed
/// shape per kind of output — <c>yyyy-MM-dd HH:mm:ss</c> for a person, ISO 8601 with the UTC offset
/// for a program.
/// </summary>
public sealed partial class NetPaceConsoleTests
{
    public sealed class Locale
    {
        /// <summary>
        /// The fixed instant <see cref="ClockStub"/> reports, as the human-readable output renders
        /// it.
        /// </summary>
        private const string HumanReadableTimestamp = "1980-01-01 10:05:00";

        /// <summary>
        /// The same instant as the machine-readable output renders it: ISO 8601 local time to the
        /// second, carrying the stub clock's UTC offset. That offset is deliberately non-zero, so a
        /// machine-readable timestamp that dropped it could not pass by accident.
        /// </summary>
        private const string MachineReadableTimestamp = "1980-01-01T10:05:00+01:00";

        /// <summary>
        /// A clock whose instant carries a fraction of a second, so a writer that failed to drop it
        /// renders a timestamp this suite can see. Same instant as <see cref="ClockStub"/>
        /// otherwise.
        /// </summary>
        private sealed class FractionalSecondClockStub : IClock
        {
            public DateTimeOffset Now => new DateTimeOffset(1980, 1, 1, 10, 5, 0, TimeSpan.FromHours(1)).AddTicks(1234567);
        }

        /// <summary>
        /// A clock sitting on UTC, where the offset is zero. The two machine-readable writers reach
        /// the offset by different routes — the CSV format string against System.Text.Json — so a
        /// zero offset is the case where they could most easily disagree.
        /// </summary>
        private sealed class UtcClockStub : IClock
        {
            public DateTimeOffset Now => new DateTimeOffset(1980, 1, 1, 10, 5, 0, TimeSpan.Zero);
        }

        /// <summary>
        /// Hosts the CLI against the stub speed tester, the given clock (the fixed-instant
        /// <see cref="ClockStub"/> by default), and no inter-test delay. A fresh host per run, so
        /// two runs cannot share state.
        /// </summary>
        private static CommandLineTestHost Host(IClock? clock = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(new SpeedTestStub());
            services.AddSingleton<IClock>(clock ?? new ClockStub());
            services.AddSingleton<IWaiter, NoDelayStub>();
            return new CommandLineTestHost(services);
        }

        private static async Task<string> RunUnderCultureAsync(string culture, string[] args, IClock? clock = null)
        {
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo(culture);
                var result = await Host(clock).RunAsync(args);

                // Reported with the output, so a run that failed for a culture-sensitive reason
                // names the reason rather than just its exit code.
                Assert.True(result.ExitCode == 0, $"Exit code {result.ExitCode}. Output: {result.Output}");
                return result.Output;
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        /// <summary>
        /// The timestamp field of a machine-readable row, however that output wraps it.
        /// </summary>
        private static string TimestampIn(string output)
        {
            var match = System.Text.RegularExpressions.Regex.Match(output, @"\d{4}-\d{2}-\d{2}T[\d:.]+(?:[+-]\d{2}:\d{2}|Z)");
            Assert.True(match.Success, $"No machine-readable timestamp found in: {output}");
            return match.Value;
        }

        private static string[] ArgsFrom(string commandLine)
        {
            return commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Every output mode, each asked for a timestamp where that is optional. The stub's upload
        /// speed is fractional, so each of these carries a decimal separator a comma-decimal
        /// locale would otherwise change.
        /// </summary>
        public static TheoryData<string> EveryOutputMode() =>
        [
            "-t",
            "--minimal -t",
            "--csv",
            "--json",
            "--json-pretty"
        ];

        [Theory]
        [MemberData(nameof(EveryOutputMode))]
        public async Task SpeedTest_UnderCommaDecimalLocale_WritesTheSameOutputAsEnglish(string commandLine)
        {
            // SCENARIO: Speed output is the same under a comma-decimal locale

            // Given a run whose result has a fractional speed.
            var args = ArgsFrom(commandLine);

            // When the same run is made under a locale that uses "," as its decimal separator.
            var german = await RunUnderCultureAsync("de-DE", args);
            var english = await RunUnderCultureAsync("en-GB", args);

            // Then the two runs wrote the same text.
            Assert.Equal(english, german);
            Assert.DoesNotContain("18,67", german);
            Assert.Contains("18.67", german);
        }

        [Fact]
        public async Task SpeedTest_CsvUnderCommaDecimalLocale_WritesAsManyFieldsAsItsHeader()
        {
            // Given a comma-delimited CSV run under a locale whose decimal separator is also ",".
            var output = await RunUnderCultureAsync("de-DE", ["--csv"]);

            // When the header and the data row are split on the delimiter.
            var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            var headerFields = lines[0].Split(',');
            var rowFields = lines[1].Split(',');

            // Then a CSV reader sees one field per column, rather than a speed split across two.
            Assert.Equal(headerFields.Length, rowFields.Length);
        }

        [Theory]
        [MemberData(nameof(EveryOutputMode))]
        public async Task SpeedTest_UnderNonGregorianLocale_WritesTheGregorianYear(string commandLine)
        {
            // SCENARIO: Timestamp is the same under a non-Gregorian locale

            // Given a run under a locale whose default calendar is the Buddhist calendar, in which
            // the stub clock's year 1980 would otherwise render as 2523.
            var args = ArgsFrom(commandLine);

            // When
            var thai = await RunUnderCultureAsync("th-TH", args);
            var english = await RunUnderCultureAsync("en-GB", args);

            // Then the timestamp shows the Gregorian year, and the whole output matches English.
            Assert.Contains("1980-01-01", thai);
            Assert.DoesNotContain("2523", thai);
            Assert.Equal(english, thai);

            // And a locale whose own time separator is "." still writes the time with ":".
            var finnish = await RunUnderCultureAsync("fi-FI", args);
            Assert.Contains("10:05:00", finnish);
            Assert.Equal(english, finnish);
        }

        [Theory]
        [InlineData("--csv")]
        [InlineData("--json")]
        [InlineData("--json-pretty")]
        public async Task SpeedTest_MachineReadableOutput_WritesTheTimestampWithItsUtcOffset(string formatSwitch)
        {
            // SCENARIO: Machine-readable timestamp carries its UTC offset

            // Given a clock fixed at a local time whose zone is an hour ahead of UTC.

            // When the result is written for a program to read.
            var output = await RunUnderCultureAsync("en-GB", [formatSwitch]);

            // Then the timestamp is that local time in ISO 8601 form, followed by the offset.
            Assert.Contains(MachineReadableTimestamp, output);
        }

        [Theory]
        [InlineData("-t")]
        [InlineData("--minimal -t")]
        public async Task SpeedTest_HumanReadableOutput_WritesTheTimestampInItsFamiliarFormat(string commandLine)
        {
            // SCENARIO: Human-readable timestamp keeps its familiar format

            // Given a run that asks for a timestamp in output meant for a person to read.

            // When
            var output = await RunUnderCultureAsync("en-GB", ArgsFrom(commandLine));

            // Then the timestamp reads as local time with no offset, as it always has.
            Assert.Contains(HumanReadableTimestamp, output);
            Assert.DoesNotContain(MachineReadableTimestamp, output);
        }

        [Theory]
        [InlineData("--csv")]
        [InlineData("--json")]
        [InlineData("--json-pretty")]
        public async Task SpeedTest_ClockReportsAFractionOfASecond_WritesTheTimestampToTheWholeSecond(string formatSwitch)
        {
            // Given a clock whose instant carries a fraction of a second.

            // When the result is written for a program to read.
            var output = await RunUnderCultureAsync("en-GB", [formatSwitch], new FractionalSecondClockStub());

            // Then the fraction is dropped, so CSV and JSON name the same instant whichever route
            // they took to render it.
            Assert.Equal(MachineReadableTimestamp, TimestampIn(output));
        }

        [Theory]
        [InlineData("--csv")]
        [InlineData("--json")]
        [InlineData("--json-pretty")]
        public async Task SpeedTest_ClockSitsOnUtc_WritesAZeroOffsetRatherThanOmittingIt(string formatSwitch)
        {
            // Given a clock on UTC, where the offset is zero.

            // When the result is written for a program to read.
            var output = await RunUnderCultureAsync("en-GB", [formatSwitch], new UtcClockStub());

            // Then the offset is still written out in full, rather than left off or abbreviated.
            Assert.Equal("1980-01-01T10:05:00+00:00", TimestampIn(output));
        }
    }
}
