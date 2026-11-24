using System.Collections.Generic;
using System.Linq;
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
    /// <remarks>
    /// Each test reads the cap off a quantity the cap actually governs. The upload test weighs the
    /// bodies the mock received, which is what crossed the wire. The download test cannot do the
    /// same, because its mock builds each response up front and so records bytes the client never
    /// read; it weighs the bytes of the requests the budget counted instead, and bounds the run as
    /// a whole by the profile's own request list. The reported figure is deliberately not the
    /// subject of either: it now also counts what requests still in flight had received when the
    /// budget tripped, which runs past the cap by design.
    /// </remarks>
    public sealed class DefaultTraffic
    {
        // Bounded at twice the cap, the allowance the explicit-cap tests use: a transfer can
        // overshoot by one in-flight parallel batch before the cap trips.
        private const long MediumDownloadCapBytes = 100L * 1024 * 1024;
        private const long MediumUploadCapBytes = 25L * 1024 * 1024;

        // Every byte the Medium download pass can ask for: five sizes, twice each, at the three
        // bytes per pixel the mock serves.
        private const long MediumDownloadPassBytes = 2L * 3 * (1500L * 1500 + 2000L * 2000 + 3000L * 3000 + 3500L * 3500 + 4000L * 4000);

        // How many requests the Medium download pass issues, so a run that the budget did not end
        // is told apart from one it did.
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
            var diagnostics = new List<RequestDiagnostic>();

            // When
            var result = await speedtest.GetDownloadSpeedAsync(server, null, new SynchronousProgress<RequestDiagnostic>(diagnostics.Add));

            // Then the budget ended the phase short of its request list, and the bytes it spent on
            // the requests it counted stayed inside the cap and its one-batch allowance.
            //
            // Read off the counted requests: the reported figure legitimately runs past the cap.
            result.ShouldNotBeNull();
            result.RequestsSucceeded.ShouldBeLessThan(MediumDownloadRequests);

            var bytesSpentOnCountedRequests = diagnostics
                .Where(record => record.Kind == RequestKind.Transfer && record.Outcome == RequestOutcome.Succeeded)
                .Sum(record => record.BytesProcessed);

            bytesSpentOnCountedRequests.ShouldBeLessThanOrEqualTo(2 * MediumDownloadCapBytes);
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
