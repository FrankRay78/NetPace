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
    /// How requests are counted is deliberately not what these tests are about. Only the bytes
    /// change.
    /// </remarks>
    public sealed class DeliveredBytes
    {
        private const string ServerUrl = "http://example.com/";

        private const int BudgetBytes = 1024 * 1024;
        private const int BytesDeliveredInFlight = 8192;

        /// <summary>
        /// How long a test waits for another request to reach the point it is ordered behind, so a
        /// regression that never gets there fails the test rather than hanging the suite.
        /// </summary>
        private static readonly TimeSpan GateLimit = TimeSpan.FromSeconds(5);

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
                await inFlightDelivered.Task.WaitAsync(GateLimit);
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

            // A real handler, not a mocked response: the body must actually be serialised for there
            // to be anything to cut off, and the cut-off must surface as a transport failure.
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
        public async Task GetUploadSpeedAsync_WhenTheServerAcceptsPartOfTheBodyAndAnswersSuccess_CountsOnlyWhatWasSent()
        {
            // Given a server that reads 32 KiB of a 100 KiB upload, stops reading, and answers
            // success anyway.
            const int bytesBeforeCutOff = 32768;

            using var handler = new CutOffUploadHandler(bytesBeforeCutOff, answerSuccessAfterCutOff: true);

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

            // Then the figure is built from what was sent, not from the length the upload asked
            // for: crediting the unsent rest would be the over-count the measurement exists to
            // remove. The request itself succeeded.
            result.BytesProcessed.ShouldBe(bytesBeforeCutOff);
            result.RequestsSucceeded.ShouldBe(1);
            result.RequestsFailed.ShouldBe(0);
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
        public async Task GetDownloadSpeedAsync_WhileOneRequestIsStillArriving_ReportsTheFigureAsItClimbs()
        {
            // Given one download delivered in four chunks, with a long pause on the clock before
            // each of the middle two - far longer than any reasonable refresh of a live figure.
            const int chunkSize = 1024;
            var longPause = TimeSpan.FromSeconds(10);

            var clock = new ControlledTimeProvider(ClockStart);
            var body = new ClockAdvancingResponseStream(chunkSize, clock, TimeSpan.Zero, longPause, longPause, TimeSpan.Zero);

            using var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("*").Respond(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) });

            var speedtest = new OoklaSpeedtest(DownloadOf([100]), mockHttp.ToHttpClient(), timeProviderOverride: clock);
            var progressReports = new List<SpeedTestProgress>();

            // When
            var result = await speedtest.GetDownloadSpeedAsync(TestServer, new SynchronousProgress<SpeedTestProgress>(progressReports.Add));

            // Then the figure was reported while the one request was still arriving, rather than
            // standing still until it finished - which on a slow link is the whole transfer.
            var figures = progressReports.Select(report => report.BytesProcessed).ToList();
            figures.ShouldContain(bytes => bytes > 0 && bytes < body.TotalBytes);

            // And it only ever climbed, ending on everything the request delivered.
            figures.ShouldBe(figures.Order().ToList());
            result.BytesProcessed.ShouldBe(body.TotalBytes);
            figures[^1].ShouldBe(body.TotalBytes);
        }

        [Fact]
        public async Task GetDownloadSpeedAsync_WhenManyChunksArriveAtOnce_ReportsFarFewerFiguresThanChunks()
        {
            // Given one download delivered in many chunks while the clock stands still - a fast link,
            // where a report per chunk would redraw a consumer thousands of times a second.
            const int chunkSize = 1024;
            const int chunks = 200;

            var clock = new ControlledTimeProvider(ClockStart);
            var body = new ClockAdvancingResponseStream(chunkSize, clock, Enumerable.Repeat(TimeSpan.Zero, chunks).ToArray());

            using var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("*").Respond(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) });

            var speedtest = new OoklaSpeedtest(DownloadOf([100]), mockHttp.ToHttpClient(), timeProviderOverride: clock);
            var progressReports = new List<SpeedTestProgress>();

            // When
            var result = await speedtest.GetDownloadSpeedAsync(TestServer, new SynchronousProgress<SpeedTestProgress>(progressReports.Add));

            // Then the consumer was not reported to once per chunk.
            progressReports.Count.ShouldBeLessThan(chunks / 10);
            progressReports[^1].BytesProcessed.ShouldBe(result.BytesProcessed);
        }

        [Fact]
        public async Task GetDownloadSpeedAsync_WhenARequestDroppedAtTheBudgetDeliversAfterTheLastReport_ReportsItsBytesToo()
        {
            // Given two parallel downloads against a 1 MiB budget: one filling it at once, and one
            // whose 8 KiB arrive after the transfer reported itself complete. The budget fills only
            // once the other is reading, so that one is always in flight when the budget ends it.
            var completeReported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var inFlightReading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            using var mockHttp = new MockHttpMessageHandler();
            mockHttp.When($"{ServerUrl}random200x200.jpg").Respond(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new CutOffResponseStream(BytesDeliveredInFlight, waitForCancellationInsteadOfFaulting: true, handOverAfter: completeReported.Task, readStarted: inFlightReading))
            });
            mockHttp.When($"{ServerUrl}random100x100.jpg").Respond(async _ =>
            {
                await inFlightReading.Task.WaitAsync(GateLimit);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[BudgetBytes]) };
            });

            var clock = new ControlledTimeProvider(ClockStart);
            var speedtest = new OoklaSpeedtest(
                DownloadOf([100, 200], parallelTasks: 2, downloadSizeMb: 1),
                mockHttp.ToHttpClient(),
                timeProviderOverride: clock);

            var progressReports = new List<SpeedTestProgress>();
            var progress = new SynchronousProgress<SpeedTestProgress>(report =>
            {
                progressReports.Add(report);

                if (report.PercentageComplete == 100)
                {
                    completeReported.TrySetResult();
                }
            });

            // When
            var result = await speedtest.GetDownloadSpeedAsync(TestServer, progress);

            // Then the last figure a consumer was shown counts the late bytes too, and is the one
            // the result reports: a figure that stopped short of them would be contradicted the
            // moment the test ended.
            result.BytesProcessed.ShouldBe(BudgetBytes + BytesDeliveredInFlight);
            progressReports[^1].BytesProcessed.ShouldBe(result.BytesProcessed);
        }

        [Fact]
        public async Task GetUploadSpeedAsync_WhenTheByteBudgetEndsARequestStillInFlight_CountsWhatThatRequestSent()
        {
            // Given two parallel uploads against a 1 MiB budget: a 2 MiB one whose server accepts
            // 8 KiB and then stops reading, and a 1 MiB one that fills the budget once it has.
            using var handler = new StalledUploadHandler(BudgetBytes, BytesDeliveredInFlight);

            var settings = new OoklaSpeedtestSettings
            {
                UploadTest = new()
                {
                    UploadSizeIncrementKb = 1024,
                    UploadIncrements = 2,
                    UploadSizeIterations = 1,
                    UploadParallelTasks = 2,
                    UploadSizeMb = 1
                },
                LatencyTest = new() { LoadedLatencyEnabled = false }
            };

            var speedtest = new OoklaSpeedtest(settings, new HttpClient(handler, disposeHandler: false));
            var diagnostics = new List<RequestDiagnostic>();

            // When
            var result = await speedtest.GetUploadSpeedAsync(TestServer, null, new SynchronousProgress<RequestDiagnostic>(diagnostics.Add));

            // Then what the in-flight upload had sent is in the figure and on its record, and no
            // more. The counts are untouched: a request dropped behind the budget is neither.
            var sentBeforeStall = handler.BytesAcceptedBeforeStall;
            sentBeforeStall.ShouldBeGreaterThan(0);

            result.BytesProcessed.ShouldBe(BudgetBytes + sentBeforeStall);
            result.RequestsSucceeded.ShouldBe(1);
            result.RequestsFailed.ShouldBe(0);

            var dropped = diagnostics.Single(d => d.Kind == RequestKind.Transfer && d.Outcome == RequestOutcome.Cancelled);
            dropped.BytesProcessed.ShouldBe(sentBeforeStall);
        }
    }
}
