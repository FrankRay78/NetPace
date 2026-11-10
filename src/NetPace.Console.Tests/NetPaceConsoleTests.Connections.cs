using NetPace.Core;
using NetPace.Core.Clients.Ookla;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    public sealed class Connections
    {
        [Fact]
        public async Task Connections_OverridesProfileCount_LeavingProfileCapsIntact()
        {
            // SCENARIO: Connection count overrides the profile

            // Given
            var (services, accessor) = BuildServicesWithSettingsAccessor();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--profile", "large", "--connections", "2"]);

            // Then neither direction may run more than two transfers at once, while Large's
            // total-byte caps and per-request shape are untouched.
            Assert.Equal(0, result.ExitCode);
            var ookla = Ookla(accessor);
            Assert.Equal(2, ookla.DownloadTest.DownloadParallelTasks);
            Assert.Equal(2, ookla.UploadTest.UploadParallelTasks);

            var large = new OoklaSpeedtestSettings(Profile.Large);
            Assert.Equal(large.DownloadTest.DownloadSizeMb, ookla.DownloadTest.DownloadSizeMb);
            Assert.Equal(large.UploadTest.UploadSizeMb, ookla.UploadTest.UploadSizeMb);
            Assert.Equal(large.DownloadSizes, ookla.DownloadSizes);
            Assert.Equal(large.DownloadSizeIterations, ookla.DownloadSizeIterations);
            Assert.Equal(large.UploadTest.UploadSizeIncrementKb, ookla.UploadTest.UploadSizeIncrementKb);
            Assert.Equal(large.UploadTest.UploadIncrements, ookla.UploadTest.UploadIncrements);
            Assert.Equal(large.UploadTest.UploadSizeIterations, ookla.UploadTest.UploadSizeIterations);
        }

        [Fact]
        public async Task Connections_One_RunsOneTransferAtATimeInEachDirection()
        {
            // SCENARIO: A single connection

            // Given
            var (services, accessor) = BuildServicesWithSettingsAccessor();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--connections", "1"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            var ookla = Ookla(accessor);
            Assert.Equal(1, ookla.DownloadTest.DownloadParallelTasks);
            Assert.Equal(1, ookla.UploadTest.UploadParallelTasks);
        }

        [Theory]
        [InlineData("1")]
        [InlineData("32")]
        public async Task Connections_AtEitherEndOfTheAcceptedRange_IsAccepted(string value)
        {
            // Given
            var (services, accessor) = BuildServicesWithSettingsAccessor();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--connections", value]);

            // Then both ends bind, so a range check written one off - rejecting its own boundary -
            // fails here rather than only when a user tries the end of the documented range.
            Assert.Equal(0, result.ExitCode);
            var ookla = Ookla(accessor);
            Assert.Equal(int.Parse(value), ookla.DownloadTest.DownloadParallelTasks);
            Assert.Equal(int.Parse(value), ookla.UploadTest.UploadParallelTasks);
        }

        [Fact]
        public async Task Connections_WithExplicitSizeOverrides_LeavesBothCapsIntact()
        {
            // Given
            var (services, accessor) = BuildServicesWithSettingsAccessor();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--profile", "large", "--connections", "2", "--downloadsize", "200", "--uploadsize", "60"]);

            // Then the connection count and the user's own caps all survive together, so an override
            // that rebuilt the test settings instead of amending them would be caught.
            Assert.Equal(0, result.ExitCode);
            var ookla = Ookla(accessor);
            Assert.Equal(2, ookla.DownloadTest.DownloadParallelTasks);
            Assert.Equal(2, ookla.UploadTest.UploadParallelTasks);
            Assert.Equal(200, ookla.DownloadTest.DownloadSizeMb);
            Assert.Equal(60, ookla.UploadTest.UploadSizeMb);
        }

        [Fact]
        public async Task NoConnectionsFlag_ProfileConnectionCountApplies()
        {
            // SCENARIO: Without the option the profile decides

            // Given
            var (services, accessor) = BuildServicesWithSettingsAccessor();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--profile", "small"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            var small = new OoklaSpeedtestSettings(Profile.Small);
            var ookla = Ookla(accessor);
            Assert.Equal(small.DownloadTest.DownloadParallelTasks, ookla.DownloadTest.DownloadParallelTasks);
            Assert.Equal(small.UploadTest.UploadParallelTasks, ookla.UploadTest.UploadParallelTasks);
            // Explicit per-spec assertions: Small runs two transfers at once in each direction.
            Assert.Equal(2, ookla.DownloadTest.DownloadParallelTasks);
            Assert.Equal(2, ookla.UploadTest.UploadParallelTasks);
        }

        [Theory]
        [InlineData("0", "zero")]
        [InlineData("33", "above-range")]
        [InlineData("2.5", "fraction")]
        [InlineData("lots", "words")]
        [InlineData("-1", "negative")]
        public async Task Connections_OutOfRangeOrNotAWholeNumber_IsRejectedBeforeAnyTestRuns(string value, string valueName)
        {
            // SCENARIO: An out-of-range connection count is rejected

            // Given
            var (services, _) = BuildServicesWithSettingsAccessor();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--connections", value]);

            // Then the run is refused up front and nothing is measured — the snapshot is the whole
            // output, so a result line appearing would fail it.
            Assert.NotEqual(0, result.ExitCode);

            // The snapshot is named for the rejected value in words, so the filename reads as the case
            // it covers rather than as the raw value.
            await Verify(result.Output).UseTextForParameters(valueName);
        }

        [Theory]
        [InlineData("--minimal")]
        [InlineData("--simple")]
        [InlineData("--csv")]
        [InlineData("--json")]
        [InlineData("--json-pretty")]
        public async Task Connections_AppliesUnderEveryOutputFormat(string formatSwitch)
        {
            // Given
            var (services, accessor) = BuildServicesWithSettingsAccessor();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--connections", "3", formatSwitch]);

            // Then
            Assert.Equal(0, result.ExitCode);
            var ookla = Ookla(accessor);
            Assert.Equal(3, ookla.DownloadTest.DownloadParallelTasks);
            Assert.Equal(3, ookla.UploadTest.UploadParallelTasks);
        }

        [Fact]
        public async Task Connections_BindsAlongsideCount()
        {
            // Given
            var (services, accessor) = BuildServicesWithSettingsAccessor();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--connections", "3", "--count", "3", "--simple"]);

            // Then the two options parse together and the count binds. The accessor holds the settings
            // built once for the whole invocation, so this does not observe the individual runs.
            Assert.Equal(0, result.ExitCode);
            var ookla = Ookla(accessor);
            Assert.Equal(3, ookla.DownloadTest.DownloadParallelTasks);
            Assert.Equal(3, ookla.UploadTest.UploadParallelTasks);
        }

        [Fact]
        public async Task Connections_BindsAlongsideLoop()
        {
            // Given a loop that cancels itself after two runs.
            var cancellationTokenSource = new CancellationTokenSource();
            var waiter = new SelfCancellingWaiter(2, cancellationTokenSource);

            var (services, accessor) = BuildServicesWithSettingsAccessor();
            services.AddSingleton<IWaiter>(waiter);
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--connections", "3", "--loop", "--simple"], cancellationTokenSource.Token);

            // Then — --loop takes its own branch through the command, so the count has to be shown to
            // survive it as well as --count.
            Assert.Equal(0, result.ExitCode);
            var ookla = Ookla(accessor);
            Assert.Equal(3, ookla.DownloadTest.DownloadParallelTasks);
            Assert.Equal(3, ookla.UploadTest.UploadParallelTasks);
        }
    }
}
