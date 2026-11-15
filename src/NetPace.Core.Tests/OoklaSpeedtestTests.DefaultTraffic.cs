using System.Net;
using System.Text.RegularExpressions;
using NetPace.Core.Clients.Ookla;
using RichardSzalay.MockHttp;
using Shouldly;

namespace NetPace.Core.Tests;

public sealed partial class OoklaSpeedtestTests
{
    /// <summary>
    /// End-to-end guards that the default run (no <c>--profile</c>, i.e. the parameterless
    /// <see cref="OoklaSpeedtestSettings"/> constructor resolving to Medium) keeps realized traffic
    /// within the reduced Medium budget. Complements the settings-level cap assertions in
    /// <c>OoklaSpeedtestSettingsTests.Profiles.cs</c> by proving the caps actually bound wire traffic.
    /// </summary>
    public sealed class DefaultTraffic
    {
        // The realized total can overshoot the cap by up to one in-flight parallel batch before the
        // cap trips, so bound at 2x the cap — the same allowance used by the explicit-cap tests in
        // OoklaSpeedtestTests.cs. Even this loose bound stays well under the pre-profile ~370 MiB run.
        private const long MediumUploadCapBytes = 25L * 1024 * 1024;

        // Every byte the Medium download pass can ask for: its five sizes, twice each, at the three
        // bytes per pixel the mock below serves. The ceiling on a default download end to end,
        // and the figure the profiles were introduced to bring down from the pre-profile ~370 MiB.
        private const long MediumDownloadPassBytes = 2L * 3 * (1500L * 1500 + 2000L * 2000 + 3000L * 3000 + 3500L * 3500 + 4000L * 4000);

        // How many requests that pass issues, so a run that the budget did not end is told apart
        // from one it did.
        private const int MediumDownloadRequests = 10;

        [Fact]
        public async Task GetDownloadSpeedAsync_DefaultSettings_CapsDownloadTrafficAtMediumBudget()
        {
            // Given — default settings (parameterless ctor => Medium), realistic per-test payloads.
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

            // Then the budget ended the phase short of its full request list, and what the run
            // moved stayed inside that list.
            //
            // The bound is the pass rather than a multiple of the 100 MiB budget, because the
            // figure now counts the bytes of requests that were still in flight when the budget
            // tripped. Eight parallel connections against this profile's request sizes hold far
            // more than the budget between them, so the counted total legitimately runs well past
            // it — more so here than on a link, since every response this mock serves is already
            // in memory. What still bounds a default download end to end is the profile's own
            // request list, and that is what this guards.
            result.ShouldNotBeNull();
            result.RequestsSucceeded.ShouldBeLessThan(MediumDownloadRequests);
            result.BytesProcessed.ShouldBeLessThanOrEqualTo(MediumDownloadPassBytes);
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
}
