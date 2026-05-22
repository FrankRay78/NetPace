using NetPace.Core.Clients.Ookla;

namespace NetPace.Core.Tests;

/// <summary>
/// Guard clause tests for <see cref="OoklaSpeedtest"/>.
/// </summary>
public sealed partial class OoklaSpeedtestTests
{
    public sealed class Guards
    {
        [Fact]
        public async Task GetServerLatencyAsync_Server_Null_ThrowsArgumentNullException()
        {
            // Given
            var speedtest = new OoklaSpeedtest();
            IServer? server = null;

            // When
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => speedtest.GetServerLatencyAsync(server!));

            // Then
            Assert.Equal("server", exception.ParamName);
        }

        [Fact]
        public async Task GetServerLatencyAsync_IServer_UrlNull_ThrowsArgumentNullException()
        {
            // Given
            var speedtest = new OoklaSpeedtest();
            var server = new Server { Url = null!, Sponsor = "Test", Location = "Test" };

            // When
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => speedtest.GetServerLatencyAsync(server));

            // Then
            Assert.Equal("server.Url", exception.ParamName);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        public async Task GetServerLatencyAsync_IServer_UrlEmptyOrWhitespace_ThrowsArgumentException(string url)
        {
            // Given
            var speedtest = new OoklaSpeedtest();
            var server = new Server { Url = url, Sponsor = "Test", Location = "Test" };

            // When
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => speedtest.GetServerLatencyAsync(server));

            // Then
            Assert.Contains("server.Url", exception.Message);
        }

        [Fact]
        public async Task GetServerLatencyAsync_ServerUrl_Null_ThrowsArgumentNullException()
        {
            // Given
            var speedtest = new OoklaSpeedtest();
            string? serverUrl = null;

            // When
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => speedtest.GetServerLatencyAsync(serverUrl!));

            // Then
            Assert.Equal("serverUrl", exception.ParamName);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        public async Task GetServerLatencyAsync_ServerUrl_EmptyOrWhitespace_ThrowsArgumentException(string serverUrl)
        {
            // Given
            var speedtest = new OoklaSpeedtest();

            // When
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => speedtest.GetServerLatencyAsync(serverUrl));

            // Then
            Assert.Equal("serverUrl", exception.ParamName);
        }

        [Fact]
        public async Task GetServerLatencyAsync_WithProgress_Server_Null_ThrowsArgumentNullException()
        {
            // Given
            var speedtest = new OoklaSpeedtest();
            IServer? server = null;

            // When
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => speedtest.GetServerLatencyAsync(server!, new NullProgress<LatencyTestProgress>()));

            // Then
            Assert.Equal("server", exception.ParamName);
        }

        [Fact]
        public async Task GetServerLatencyAsync_WithProgress_IServer_UrlNull_ThrowsArgumentNullException()
        {
            // Given
            var speedtest = new OoklaSpeedtest();
            var server = new Server { Url = null!, Sponsor = "Test", Location = "Test" };

            // When
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => speedtest.GetServerLatencyAsync(server, new NullProgress<LatencyTestProgress>()));

            // Then
            Assert.Equal("server.Url", exception.ParamName);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        public async Task GetServerLatencyAsync_WithProgress_IServer_UrlEmptyOrWhitespace_ThrowsArgumentException(string url)
        {
            // Given
            var speedtest = new OoklaSpeedtest();
            var server = new Server { Url = url, Sponsor = "Test", Location = "Test" };

            // When
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => speedtest.GetServerLatencyAsync(server, new NullProgress<LatencyTestProgress>()));

            // Then
            Assert.Contains("server.Url", exception.Message);
        }

        [Fact]
        public async Task GetServerLatencyAsync_ByUrl_WithProgress_ServerUrl_Null_ThrowsArgumentNullException()
        {
            // Given
            var speedtest = new OoklaSpeedtest();
            string? serverUrl = null;

            // When
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => speedtest.GetServerLatencyAsync(serverUrl!, new NullProgress<LatencyTestProgress>()));

            // Then
            Assert.Equal("serverUrl", exception.ParamName);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        public async Task GetServerLatencyAsync_ByUrl_WithProgress_ServerUrl_EmptyOrWhitespace_ThrowsArgumentException(string serverUrl)
        {
            // Given
            var speedtest = new OoklaSpeedtest();

            // When
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => speedtest.GetServerLatencyAsync(serverUrl, new NullProgress<LatencyTestProgress>()));

            // Then
            Assert.Equal("serverUrl", exception.ParamName);
        }

        [Fact]
        public async Task GetFastestServerByLatencyAsync_Servers_Null_ThrowsArgumentNullException()
        {
            // Given
            var speedtest = new OoklaSpeedtest();
            IServer[]? servers = null;

            // When
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => speedtest.GetFastestServerByLatencyAsync(servers!));

            // Then
            Assert.Equal("servers", exception.ParamName);
        }

        [Fact]
        public async Task GetFastestServerByLatencyAsync_Servers_Empty_ThrowsArgumentException()
        {
            // Given
            var speedtest = new OoklaSpeedtest();
            IServer[] servers = Array.Empty<IServer>();

            // When
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => speedtest.GetFastestServerByLatencyAsync(servers));

            // Then
            Assert.Equal("servers", exception.ParamName);
        }

        [Fact]
        public async Task GetDownloadSpeedAsync_Server_Null_ThrowsArgumentNullException()
        {
            // Given
            var speedtest = new OoklaSpeedtest();
            IServer? server = null;

            // When
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => speedtest.GetDownloadSpeedAsync(server!));

            // Then
            Assert.Equal("server", exception.ParamName);
        }

        [Fact]
        public async Task GetDownloadSpeedAsync_WithProgress_Server_Null_ThrowsArgumentNullException()
        {
            // Given
            var speedtest = new OoklaSpeedtest();
            IServer? server = null;

            // When
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => speedtest.GetDownloadSpeedAsync(server!, new NullProgress<SpeedTestProgress>()));

            // Then
            Assert.Equal("server", exception.ParamName);
        }

        [Fact]
        public async Task GetDownloadSpeedAsync_ServerUrl_Null_ThrowsArgumentNullException()
        {
            // Given
            var speedtest = new OoklaSpeedtest();
            var server = new Server { Url = null!, Sponsor = "Test", Location = "Test" };

            // When
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => speedtest.GetDownloadSpeedAsync(server, new NullProgress<SpeedTestProgress>()));

            // Then
            Assert.Equal("server.Url", exception.ParamName);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        public async Task GetDownloadSpeedAsync_ServerUrl_EmptyOrWhitespace_ThrowsArgumentException(string url)
        {
            // Given
            var speedtest = new OoklaSpeedtest();
            var server = new Server { Url = url, Sponsor = "Test", Location = "Test" };

            // When
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => speedtest.GetDownloadSpeedAsync(server, new NullProgress<SpeedTestProgress>()));

            // Then
            Assert.Contains("server.Url", exception.Message);
        }

        [Fact]
        public async Task GetUploadSpeedAsync_Server_Null_ThrowsArgumentNullException()
        {
            // Given
            var speedtest = new OoklaSpeedtest();
            IServer? server = null;

            // When
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => speedtest.GetUploadSpeedAsync(server!));

            // Then
            Assert.Equal("server", exception.ParamName);
        }

        [Fact]
        public async Task GetUploadSpeedAsync_WithProgress_Server_Null_ThrowsArgumentNullException()
        {
            // Given
            var speedtest = new OoklaSpeedtest();
            IServer? server = null;

            // When
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => speedtest.GetUploadSpeedAsync(server!, new NullProgress<SpeedTestProgress>()));

            // Then
            Assert.Equal("server", exception.ParamName);
        }

        [Fact]
        public async Task GetUploadSpeedAsync_ServerUrl_Null_ThrowsArgumentNullException()
        {
            // Given
            var speedtest = new OoklaSpeedtest();
            var server = new Server { Url = null!, Sponsor = "Test", Location = "Test" };

            // When
            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                () => speedtest.GetUploadSpeedAsync(server, new NullProgress<SpeedTestProgress>()));

            // Then
            Assert.Equal("server.Url", exception.ParamName);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        public async Task GetUploadSpeedAsync_ServerUrl_EmptyOrWhitespace_ThrowsArgumentException(string url)
        {
            // Given
            var speedtest = new OoklaSpeedtest();
            var server = new Server { Url = url, Sponsor = "Test", Location = "Test" };

            // When
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => speedtest.GetUploadSpeedAsync(server, new NullProgress<SpeedTestProgress>()));

            // Then
            Assert.Contains("server.Url", exception.Message);
        }
    }
}
