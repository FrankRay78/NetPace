using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using NetPace.Core.Clients.Ookla;
using RichardSzalay.MockHttp;
using Shouldly;

namespace NetPace.Core.Tests;

public sealed partial class OoklaSpeedtestTests
{
    /// <summary>
    /// What a transfer counts: every byte a request delivered, however the request ended. A server
    /// that cuts a connection off partway, and a request still in flight when the byte budget is
    /// reached, both moved data across the link, and the reported figure is about the link.
    /// </summary>
    /// <remarks>
    /// How requests are counted is deliberately not what these tests are about: a request that did
    /// not finish still counts as failed, and one dropped at the byte budget still counts as
    /// neither. Only the bytes change.
    /// </remarks>
    public sealed class DeliveredBytes
    {
        private const string ServerUrl = "http://example.com/";

        private const int BudgetBytes = 1024 * 1024;
        private const int BytesDeliveredInFlight = 8192;

        private static readonly IServer TestServer = new Server { Url = ServerUrl, Sponsor = "Test", Location = "Test" };

        /// <summary>
        /// Settings for a download of exactly the requests named, run one at a time, with the
        /// under-load probes off so that nothing but the transfer itself moves any bytes.
        /// </summary>
        private static OoklaSpeedtestSettings DownloadOf(int[] sizes, int iterations = 1, int parallelTasks = 1, int downloadSizeMb = int.MaxValue) =>
            new()
            {
                DownloadSizes = sizes,
                DownloadSizeIterations = iterations,
                DownloadTest = new()
                {
                    DownloadParallelTasks = parallelTasks,
                    DownloadSizeMb = downloadSizeMb
                },
                LatencyTest = new() { LoadedLatencyEnabled = false }
            };

        /// <summary>
        /// Two parallel downloads against a <see cref="BudgetBytes"/> budget: one that delivers
        /// <see cref="BytesDeliveredInFlight"/> bytes and is then still in flight, and one that
        /// delivers the whole budget once it has.
        /// </summary>
        private static OoklaSpeedtest TwoDownloadsAgainstTheBudget(MockHttpMessageHandler mockHttp)
        {
            var inFlightDelivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            mockHttp.When($"{ServerUrl}random200x200.jpg").Respond(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new CutOffResponseStream(BytesDeliveredInFlight, inFlightDelivered, waitForCancellationInsteadOfFaulting: true))
            });

            mockHttp.When($"{ServerUrl}random100x100.jpg").Respond(async _ =>
            {
                await inFlightDelivered.Task;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[BudgetBytes]) };
            });

            return new OoklaSpeedtest(
                DownloadOf([100, 200], parallelTasks: 2, downloadSizeMb: 1),
                mockHttp.ToHttpClient());
        }

        [Fact]
        public async Task GetDownloadSpeedAsync_WhenTheServerClosesTheResponsePartway_CountsWhatArrived()
        {
            // SCENARIO: A cut-off request's delivered bytes count towards the figure

            // Given a server that hands over 4 KiB of a download and then closes the connection.
            const int bytesBeforeCutOff = 4096;

            using var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("*").Respond(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new CutOffResponseStream(bytesBeforeCutOff))
            });

            var speedtest = new OoklaSpeedtest(DownloadOf([100]), mockHttp.ToHttpClient());

            // When
            var result = await speedtest.GetDownloadSpeedAsync(TestServer);

            // Then the bytes that arrived before the close are what the figure is built from, and
            // the request that delivered them still counts as failed.
            result.BytesProcessed.ShouldBe(bytesBeforeCutOff);
            result.RequestsSucceeded.ShouldBe(0);
            result.RequestsFailed.ShouldBe(1);
        }

        [Fact]
        public async Task GetDownloadSpeedAsync_WhenTheServerClosesTheResponsePartway_RecordsWhatArrivedOnTheDiagnostic()
        {
            // Given a server that hands over 4 KiB of a download and then closes the connection.
            const int bytesBeforeCutOff = 4096;

            using var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("*").Respond(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new CutOffResponseStream(bytesBeforeCutOff))
            });

            var speedtest = new OoklaSpeedtest(DownloadOf([100]), mockHttp.ToHttpClient());
            var diagnostics = new List<RequestDiagnostic>();

            // When
            await speedtest.GetDownloadSpeedAsync(TestServer, null, new SynchronousProgress<RequestDiagnostic>(diagnostics.Add));

            // Then the request's own record says what it delivered, not nothing.
            var record = diagnostics.Single(d => d.Kind == RequestKind.Transfer);
            record.Outcome.ShouldBe(RequestOutcome.Failed);
            record.BytesProcessed.ShouldBe(bytesBeforeCutOff);
        }

        [Fact]
        public async Task GetUploadSpeedAsync_WhenTheServerClosesTheRequestPartway_CountsWhatWasSent()
        {
            // Given a server that accepts 32 KiB of a 100 KiB upload and then closes the connection.
            const int bytesBeforeCutOff = 32768;

            // A handler of its own rather than a mocked response: the body has to be really
            // serialised for there to be anything to cut off partway, and the cut-off has to
            // surface as the transport failure a closed connection is.
            using var handler = new CutOffUploadHandler(bytesBeforeCutOff);

            var settings = new OoklaSpeedtestSettings
            {
                UploadTest = new()
                {
                    UploadSizeIncrementKb = 100,
                    UploadIncrements = 1,
                    UploadSizeIterations = 1,
                    UploadParallelTasks = 1
                },
                LatencyTest = new() { LoadedLatencyEnabled = false }
            };

            var speedtest = new OoklaSpeedtest(settings, new HttpClient(handler, disposeHandler: false));

            // When
            var result = await speedtest.GetUploadSpeedAsync(TestServer);

            // Then what was sent before the close is what the figure is built from, and the request
            // that sent it still counts as failed.
            result.BytesProcessed.ShouldBe(bytesBeforeCutOff);
            result.RequestsSucceeded.ShouldBe(0);
            result.RequestsFailed.ShouldBe(1);
        }

        [Fact]
        public async Task GetDownloadSpeedAsync_WhenTheByteBudgetEndsARequestStillInFlight_CountsWhatThatRequestDelivered()
        {
            // SCENARIO: A request stopped at the byte budget counts what it delivered

            // Given two parallel downloads against a 1 MiB budget: one that delivers 8 KiB and is
            // then still in flight, and one that delivers the whole budget once it has.
            using var mockHttp = new MockHttpMessageHandler();
            var speedtest = TwoDownloadsAgainstTheBudget(mockHttp);

            // When
            var result = await speedtest.GetDownloadSpeedAsync(TestServer);

            // Then the in-flight request's bytes are in the figure, while the counts are untouched:
            // the request that filled the budget succeeded, and the one dropped behind it is
            // neither a success nor a failure.
            result.BytesProcessed.ShouldBe(BudgetBytes + BytesDeliveredInFlight);
            result.RequestsSucceeded.ShouldBe(1);
            result.RequestsFailed.ShouldBe(0);
        }

        [Fact]
        public async Task GetDownloadSpeedAsync_WhenTheByteBudgetEndsARequestStillInFlight_RecordsWhatItDeliveredOnTheDiagnostic()
        {
            // Given the same two parallel downloads against a 1 MiB budget.
            using var mockHttp = new MockHttpMessageHandler();
            var speedtest = TwoDownloadsAgainstTheBudget(mockHttp);

            var diagnostics = new List<RequestDiagnostic>();

            // When
            await speedtest.GetDownloadSpeedAsync(TestServer, null, new SynchronousProgress<RequestDiagnostic>(diagnostics.Add));

            // Then the dropped request's own record says what it delivered. This is what lets a
            // diagnostics log reconcile: the summary adds up every record's bytes whatever its
            // outcome, so a record reporting nothing here would disagree with the reported speed.
            var dropped = diagnostics.Single(d => d.Kind == RequestKind.Transfer && d.Outcome == RequestOutcome.Cancelled);
            dropped.BytesProcessed.ShouldBe(BytesDeliveredInFlight);
        }

        [Fact]
        public async Task GetDownloadSpeedAsync_WhileOneRequestIsStillArriving_ReportsThePacedFigureAsItClimbs()
        {
            // Given one download delivered in four chunks, with the clock moved past the pacing
            // interval before the first and third of them and left standing before the second and
            // fourth. Two chunks therefore fall inside one interval, and two open a new one.
            const int chunkSize = 1024;
            var pacingInterval = TimeSpan.FromMilliseconds(300);

            var clock = new ControlledTimeProvider(ClockStart);
            var body = new ClockAdvancingResponseStream(chunkSize, clock, pacingInterval, TimeSpan.Zero, pacingInterval, TimeSpan.Zero);

            using var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("*").Respond(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) });

            var speedtest = new OoklaSpeedtest(DownloadOf([100]), mockHttp.ToHttpClient(), timeProviderOverride: clock);
            var progressReports = new List<SpeedTestProgress>();

            // When
            var result = await speedtest.GetDownloadSpeedAsync(TestServer, new SynchronousProgress<SpeedTestProgress>(progressReports.Add));

            // Then the figure was reported while the one request was still arriving, rather than
            // standing still until it finished - which on a slow link is the whole transfer. The
            // two chunks that shared an interval were reported once between them, not twice.
            var whileArriving = progressReports.Where(report => report.BytesProcessed < body.TotalBytes).ToList();
            whileArriving.Select(report => report.BytesProcessed).ShouldBe([chunkSize, 3 * chunkSize]);

            // And the figure the transfer ends on is still everything the request delivered.
            result.BytesProcessed.ShouldBe(body.TotalBytes);
            progressReports[^1].BytesProcessed.ShouldBe(body.TotalBytes);
        }

        [Fact]
        public async Task GetDownloadSpeedAsync_WhenTheByteBudgetEndsARequestStillInFlight_ReportsNoLiveFigureBelowTheFinalOne()
        {
            // Given the same two parallel downloads against a 1 MiB budget.
            using var mockHttp = new MockHttpMessageHandler();
            var speedtest = TwoDownloadsAgainstTheBudget(mockHttp);

            var progressReports = new List<SpeedTestProgress>();

            // When
            var result = await speedtest.GetDownloadSpeedAsync(TestServer, new SynchronousProgress<SpeedTestProgress>(progressReports.Add));

            // Then the last figure a consumer was shown already counted the in-flight request's
            // bytes, and is the one the result reports: a live figure that stopped short of it
            // would be contradicted the moment the test ended.
            progressReports.ShouldNotBeEmpty();
            progressReports[^1].BytesProcessed.ShouldBe(BudgetBytes + BytesDeliveredInFlight);
            progressReports[^1].BytesProcessed.ShouldBe(result.BytesProcessed);
        }
    }
}
