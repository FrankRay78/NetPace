using System.Net;
using System.Text.RegularExpressions;
using NetPace.Core.Clients.Ookla;
using RichardSzalay.MockHttp;
using Shouldly;

namespace NetPace.Core.Tests;

/// <summary>
/// End-to-end guards that the default run (no <c>--profile</c>, i.e. the parameterless
/// <see cref="OoklaSpeedtestSettings"/> constructor resolving to Medium) keeps realized traffic
/// within the reduced Medium budget. Complements the settings-level cap assertions in
/// <c>OoklaSpeedtestSettingsTests.Profiles.cs</c> by proving the caps actually bound wire traffic.
/// </summary>
public sealed partial class OoklaSpeedtestTests
{
    // The realized total can overshoot the cap by up to one in-flight parallel batch before the
    // cap trips, so bound at 2x the cap — the same allowance used by the explicit-cap tests in
    // OoklaSpeedtestTests.cs. Even this loose bound stays well under the pre-profile ~370 MiB run.
    private const long MediumDownloadCapBytes = 100L * 1024 * 1024;
    private const long MediumUploadCapBytes = 25L * 1024 * 1024;

    [Fact]
    public async Task GetDownloadSpeedAsync_DefaultSettings_CapsDownloadTrafficAtMediumBudget()
    {
        // Given — default settings (parameterless ctor => Medium), realistic per-dimension payloads.
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(request =>
        {
            var match = Regex.Match(request?.RequestUri?.AbsolutePath ?? "", @"random(\d+)x(\d+)\.jpg");

            if (!match.Success)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            int width = int.Parse(match.Groups[1].Value);
            int height = int.Parse(match.Groups[2].Value);

            // Simulate byte size: assume 3 bytes per pixel (RGB).
            var content = new ByteArrayContent(new byte[width * height * 3]);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings();
        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

        // When
        var result = await speedtest.GetDownloadSpeedAsync(server);

        // Then
        result.ShouldNotBeNull();
        result.BytesProcessed.ShouldBeLessThanOrEqualTo(2 * MediumDownloadCapBytes);
    }

    [Fact]
    public async Task GetUploadSpeedAsync_DefaultSettings_CapsUploadTrafficAtMediumBudget()
    {
        // Given — default settings (parameterless ctor => Medium); count uploaded body bytes.
        long actualBytes = 0;

        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(async request =>
        {
            if (request?.Content != null)
            {
                var body = await request.Content.ReadAsByteArrayAsync();
                Interlocked.Add(ref actualBytes, body.LongLength);
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var httpClient = mockHttp.ToHttpClient();
        var settings = new OoklaSpeedtestSettings();
        var speedtest = new OoklaSpeedtest(settings, httpClient);
        var server = new Server { Url = "http://example.com/", Sponsor = "Test", Location = "Test" };

        // When
        var result = await speedtest.GetUploadSpeedAsync(server);

        // Then
        result.ShouldNotBeNull();
        actualBytes.ShouldBeLessThanOrEqualTo(2 * MediumUploadCapBytes);
    }
}
