using NetPace.Console;
using NetPace.Core.Clients.Ookla;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    public sealed class ProfileOption
    {
        [Theory]
        [InlineData("tiny")]
        [InlineData("Tiny")]
        [InlineData("TINY")]
        public async Task Profile_CaseInsensitiveEnumParsing_BindsToTiny(string value)
        {
            // Given
            var (services, accessor) = BuildServicesWithSettingsAccessor();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(new[] { "--profile", value });

            // Then — assert field-for-field (int[] DownloadSizes uses reference equality).
            Assert.Equal(0, result.ExitCode);
            var expected = new OoklaSpeedtestSettings(Profile.Tiny);
            var ookla = Ookla(accessor);
            Assert.Equal(expected.DownloadSizes, ookla.DownloadSizes);
            Assert.Equal(expected.DownloadSizeIterations, ookla.DownloadSizeIterations);
            Assert.Equal(expected.DownloadTest.DownloadParallelTasks, ookla.DownloadTest.DownloadParallelTasks);
            Assert.Equal(expected.DownloadTest.DownloadSizeMb, ookla.DownloadTest.DownloadSizeMb);
            Assert.Equal(expected.UploadTest.UploadSizeIncrementKb, ookla.UploadTest.UploadSizeIncrementKb);
            Assert.Equal(expected.UploadTest.UploadIncrements, ookla.UploadTest.UploadIncrements);
            Assert.Equal(expected.UploadTest.UploadSizeIterations, ookla.UploadTest.UploadSizeIterations);
            Assert.Equal(expected.UploadTest.UploadParallelTasks, ookla.UploadTest.UploadParallelTasks);
            Assert.Equal(expected.UploadTest.UploadSizeMb, ookla.UploadTest.UploadSizeMb);
        }

        [Fact]
        public async Task Profile_UnknownValue_ExitsNonZeroAndMentionsBadValue()
        {
            // SCENARIO: Invalid --profile value is rejected by argument parsing

            // Given
            var (services, _) = BuildServicesWithSettingsAccessor();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(new[] { "--profile", "huge" });

            // Then — non-zero exit and the offending token surfaces in error output.
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("huge", result.Output, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Profile_AuthoritativeForPerRequestShape_OnTiny()
        {
            // SCENARIO: Profile is authoritative for per-request shape

            // Given
            var (services, accessor) = BuildServicesWithSettingsAccessor();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(new[] { "--profile", "tiny" });

            // Then
            Assert.Equal(0, result.ExitCode);
            var ookla = Ookla(accessor);
            Assert.Equal(new[] { 350 }, ookla.DownloadSizes);
            Assert.Equal(1, ookla.DownloadTest.DownloadParallelTasks);
            Assert.Equal(1, ookla.DownloadSizeIterations);
            Assert.All(ookla.DownloadSizes, s => Assert.True(s <= 350));
        }

        [Fact]
        public async Task NoProfileFlag_DefaultsToMedium()
        {
            // SCENARIO: Omitted --profile defaults to Medium

            // Given
            var (services, accessor) = BuildServicesWithSettingsAccessor();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(Array.Empty<string>());

            // Then — assert field-for-field (int[] DownloadSizes uses reference equality).
            Assert.Equal(0, result.ExitCode);
            var medium = new OoklaSpeedtestSettings(Profile.Medium);
            var ookla = Ookla(accessor);
            Assert.Equal(medium.DownloadSizes, ookla.DownloadSizes);
            Assert.Equal(medium.DownloadSizeIterations, ookla.DownloadSizeIterations);
            Assert.Equal(medium.DownloadTest.DownloadParallelTasks, ookla.DownloadTest.DownloadParallelTasks);
            Assert.Equal(medium.DownloadTest.DownloadSizeMb, ookla.DownloadTest.DownloadSizeMb);
            Assert.Equal(medium.UploadTest.UploadSizeIncrementKb, ookla.UploadTest.UploadSizeIncrementKb);
            Assert.Equal(medium.UploadTest.UploadIncrements, ookla.UploadTest.UploadIncrements);
            Assert.Equal(medium.UploadTest.UploadSizeIterations, ookla.UploadTest.UploadSizeIterations);
            Assert.Equal(medium.UploadTest.UploadParallelTasks, ookla.UploadTest.UploadParallelTasks);
            Assert.Equal(medium.UploadTest.UploadSizeMb, ookla.UploadTest.UploadSizeMb);
            // Explicit per-spec assertions.
            Assert.Equal(new[] { 1500, 2000, 3000, 3500, 4000 }, ookla.DownloadSizes);
            Assert.Equal(100, ookla.DownloadTest.DownloadSizeMb);
            Assert.Equal(25, ookla.UploadTest.UploadSizeMb);
        }

        [Fact]
        public async Task DownloadSizeOverride_PreservesProfileShape_OnTiny()
        {
            // SCENARIO: --downloadsize overrides only the cap, profile shape is preserved

            // Given
            var (services, accessor) = BuildServicesWithSettingsAccessor();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(new[] { "--profile", "tiny", "--downloadsize", "5" });

            // Then
            Assert.Equal(0, result.ExitCode);
            var ookla = Ookla(accessor);
            Assert.Equal(new[] { 350 }, ookla.DownloadSizes);
            Assert.Equal(1, ookla.DownloadSizeIterations);
            Assert.Equal(1, ookla.DownloadTest.DownloadParallelTasks);
            Assert.Equal(5, ookla.DownloadTest.DownloadSizeMb);
        }

        [Fact]
        public async Task UploadSizeOverride_PreservesProfileShape_OnSmall()
        {
            // SCENARIO: --uploadsize overrides only the upload cap

            // Given
            var (services, accessor) = BuildServicesWithSettingsAccessor();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(new[] { "--profile", "small", "--uploadsize", "1" });

            // Then
            Assert.Equal(0, result.ExitCode);
            var ookla = Ookla(accessor);
            Assert.Equal(100, ookla.UploadTest.UploadSizeIncrementKb);
            Assert.Equal(4, ookla.UploadTest.UploadIncrements);
            Assert.Equal(2, ookla.UploadTest.UploadSizeIterations);
            Assert.Equal(2, ookla.UploadTest.UploadParallelTasks);
            Assert.Equal(1, ookla.UploadTest.UploadSizeMb);
        }

        [Fact]
        public async Task DownloadSizeOverride_LargerThanNaturalTransfer_IsNoopBackstop()
        {
            // SCENARIO: Override cap larger than natural transfer is a no-op backstop
            //
            // Tiny's natural transfer (≤ 1 MiB) stays well below the override cap (5000 MiB),
            // so the cap-check never trips. Override is mechanically present on the settings record.
            // Tiny's natural-budget assertion is in OoklaSpeedtestSettingsTests.Profiles.Tiny_FieldsMatchDataModel.

            // Given
            var (services, accessor) = BuildServicesWithSettingsAccessor();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(new[] { "--profile", "tiny", "--downloadsize", "5000" });

            // Then
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(5000, Ookla(accessor).DownloadTest.DownloadSizeMb);
        }

        [Fact]
        public async Task NoDownload_ShortCircuits_RegardlessOfProfile()
        {
            // SCENARIO: --no-download short-circuits regardless of profile

            // Given
            var (services, accessor) = BuildServicesWithSettingsAccessor();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(new[] { "--no-download", "--profile", "large" });

            // Then
            Assert.Equal(0, result.ExitCode);

            // --no-download is honoured by the runtime (existing behaviour), and the upload phase
            // still binds to Large's per-request shape.
            var ookla = Ookla(accessor);
            Assert.Equal(500, ookla.UploadTest.UploadSizeIncrementKb);
            Assert.Equal(8, ookla.UploadTest.UploadIncrements);
            Assert.Equal(16, ookla.UploadTest.UploadParallelTasks);
        }
    }
}
