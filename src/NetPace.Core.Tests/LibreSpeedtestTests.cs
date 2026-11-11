using System.Net;
using NetPace.Core.Clients.LibreSpeed;
using RichardSzalay.MockHttp;
using Shouldly;

namespace NetPace.Core.Tests;

/// <summary>
/// The LibreSpeed provider: which endpoints it speaks to inside a LibreSpeed install, what it
/// accepts as an answer from them, and what it reports.
/// </summary>
public sealed class LibreSpeedtestTests
{
    private const string InstallUrl = "http://pi.local:8080/";
    private const string PathInstallUrl = "http://nas.local/speedtest/";

    /// <summary>
    /// Settings that keep a test's traffic to a handful of requests and never wait between them.
    /// </summary>
    private static LibreSpeedtestSettings Quick() =>
        new LibreSpeedtestSettings(Profile.Tiny) with
        {
            LatencyTest = new()
            {
                LatencyTestIterations = 3,
                LatencyTestIntervalMilliseconds = 0,
                LoadedLatencyEnabled = false,
            },
        };

    /// <summary>
    /// What a LibreSpeed install answers its ping and upload endpoint with: a success status and no
    /// body at all, which is what the provider reads to tell a LibreSpeed server from a web server.
    /// A fresh response per request, because each is read and disposed by the one that asked.
    /// </summary>
    private static HttpResponseMessage EmptyAnswer() =>
        new(HttpStatusCode.OK) { Content = new StringContent(string.Empty) };

    /// <summary>
    /// Answers every LibreSpeed endpoint as the official image does: an empty body for the ping
    /// and upload endpoint, and a body for garbage. The size of that body is the measurement's
    /// business, not the endpoint's, so it stays small here.
    /// </summary>
    private static MockHttpMessageHandler LibreSpeedServer(string install)
    {
        var mockHttp = new MockHttpMessageHandler();

        mockHttp.When($"{install}backend/empty.php")
                .Respond(_ => EmptyAnswer());

        mockHttp.When($"{install}backend/garbage.php")
                .Respond(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[1024]) });

        return mockHttp;
    }

    private static Server ServerAt(string url) => new() { Url = url, Sponsor = Server.UnknownSponsor };

    [Fact]
    public async Task GetServerLatencyAsync_AgainstALibreSpeedServer_ReportsLatencyAndJitter()
    {
        // SCENARIO: LibreSpeed run reports every measurement

        // Given a reachable LibreSpeed server.
        using var mockHttp = LibreSpeedServer(InstallUrl);
        var speedtest = new LibreSpeedtest(Quick(), mockHttp.ToHttpClient(), new DelayProviderStub());

        // When its latency is measured.
        var result = await speedtest.GetServerLatencyAsync(ServerAt(InstallUrl));

        // Then a figure and its unevenness are reported for that server.
        result.Server.Url.ShouldBe(InstallUrl);
        result.LatencyMilliseconds.ShouldBeGreaterThanOrEqualTo(0);
        result.JitterMilliseconds.ShouldBeGreaterThanOrEqualTo(0);
        result.MinimumLatencyMilliseconds.ShouldNotBeNull();
        result.MaximumLatencyMilliseconds.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetServerLatencyAsync_ProbesThePingEndpointInsideTheInstall()
    {
        // Given a LibreSpeed server, with only its ping endpoint answering.
        using var mockHttp = new MockHttpMessageHandler();
        var probe = mockHttp.Expect($"{InstallUrl}backend/empty.php").Respond(_ => EmptyAnswer());
        var speedtest = new LibreSpeedtest(Quick() with { LatencyTest = new() { LatencyTestIterations = 1, LatencyTestIntervalMilliseconds = 0, LoadedLatencyEnabled = false } }, mockHttp.ToHttpClient(), new DelayProviderStub());

        // When
        await speedtest.GetServerLatencyAsync(ServerAt(InstallUrl));

        // Then that is the endpoint that was asked.
        mockHttp.GetMatchCount(probe).ShouldBe(1);
    }

    /// <summary>
    /// The ways a user names one LibreSpeed install under a path: with and without its trailing
    /// slash, with a query or fragment copied from the browser, and as the page the browser shows.
    /// </summary>
    public static TheoryData<string> FormsOfThePathInstall() => new(
        "http://nas.local/speedtest/",
        "http://nas.local/speedtest",
        "http://nas.local/speedtest/?lang=en#start",
        "http://nas.local/speedtest/index.html");

    [Theory]
    [MemberData(nameof(FormsOfThePathInstall))]
    public async Task GetServerLatencyAsync_ForAnInstallUnderAPath_ProbesInsideThatPath(string given)
    {
        // Given a LibreSpeed install under a path, named in each form a user might give it.
        using var mockHttp = LibreSpeedServer(PathInstallUrl);
        var speedtest = new LibreSpeedtest(Quick(), mockHttp.ToHttpClient(), new DelayProviderStub());

        // When
        var result = await speedtest.GetServerLatencyAsync(ServerAt(given));

        // Then every form tests the same server: the probe lands inside the install path, so the
        // measurement completes rather than failing against the web root.
        result.LatencyMilliseconds.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetServerLatencyAsync_WhenThePingEndpointIsNotThere_FailsNamingTheUrl()
    {
        // SCENARIO: Unreachable LibreSpeed server is reported
        //
        // Naming the web root when the install sits under a path is the likeliest way to get this
        // wrong, and the ping endpoint is then not found. That is as much a server that is not
        // LibreSpeed as one answering with a page, so it is reported the same way.

        // Given an address where the ping endpoint is not served.
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond(HttpStatusCode.NotFound);
        var speedtest = new LibreSpeedtest(Quick(), mockHttp.ToHttpClient(), new DelayProviderStub());

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetServerLatencyAsync(ServerAt(InstallUrl)));

        // Then the run fails, naming the server that answered wrongly, and reports no measurement.
        exception.ShouldBeOfType<InvalidOperationException>();
        exception.Message.ShouldContain(InstallUrl);
    }

    [Fact]
    public async Task GetServerLatencyAsync_WhenTheServerAnswersWithAPage_FailsNamingTheUrl()
    {
        // SCENARIO: Unreachable LibreSpeed server is reported
        //
        // The ping endpoint sends no body, so a body is the evidence that whatever answered is not
        // a LibreSpeed server. A plain web server is the case that matters: it answers 200 with a
        // page, which no status check would catch.

        // Given something at the address answers, but with a page.
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When("*").Respond("text/html", "<html>It works!</html>");
        var speedtest = new LibreSpeedtest(Quick(), mockHttp.ToHttpClient(), new DelayProviderStub());

        // When
        var exception = await Record.ExceptionAsync(() => speedtest.GetServerLatencyAsync(ServerAt(InstallUrl)));

        // Then the run fails, naming the URL that answered wrongly, and reports no measurement.
        exception.ShouldBeOfType<InvalidOperationException>();
        exception.Message.ShouldContain(InstallUrl);
    }

    [Fact]
    public async Task GetDownloadSpeedAsync_AgainstALibreSpeedServer_ReportsBytesAndLatencyUnderLoad()
    {
        // SCENARIO: LibreSpeed run reports every measurement

        // Given a reachable LibreSpeed server, and a download held open until the first under-load
        // probe has been taken, so the figure is reported rather than raced for.
        using var probing = new HeldTransfer();
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When($"{InstallUrl}backend/empty.php")
                .Respond(_ => EmptyAnswer());
        mockHttp.When($"{InstallUrl}backend/garbage.php")
                .Respond(async _ =>
                {
                    await probing.UntilFirstProbeTaken();
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[1024]) };
                });
        var settings = new LibreSpeedtestSettings(Profile.Tiny) with
        {
            LatencyTest = new() { LoadedLatencyIntervalMilliseconds = 1 },
            DownloadRequestCount = 1,
        };
        var speedtest = new LibreSpeedtest(settings, mockHttp.ToHttpClient(), new DelayProviderStub());

        // When
        var result = await speedtest.GetDownloadSpeedAsync(ServerAt(InstallUrl), null, probing.Reporter);

        // Then bytes and a duration are reported, with the latency measured while they moved, from
        // probes sent to the install's own ping endpoint.
        result.BytesProcessed.ShouldBe(1024);
        result.RequestsSucceeded.ShouldBe(1);
        result.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(0);
        result.Latency.ShouldNotBeNull();
        probing.ProbedUrls.ShouldNotBeEmpty();
        probing.ProbedUrls.ShouldAllBe(url => url == $"{InstallUrl}backend/empty.php");
    }

    [Fact]
    public async Task GetDownloadSpeedAsync_AsksGarbageForTheConfiguredMibPerRequest()
    {
        // Given a LibreSpeed server and a two-request download of 3 MiB each.
        using var mockHttp = new MockHttpMessageHandler();
        var garbage = mockHttp.When($"{PathInstallUrl}backend/garbage.php")
                              .WithQueryString("ckSize", "3")
                              .Respond(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[1024]) });
        var settings = new LibreSpeedtestSettings(Profile.Tiny) with
        {
            LatencyTest = new() { LoadedLatencyEnabled = false },
            DownloadRequestSizeMib = 3,
            DownloadRequestCount = 2,
        };
        var speedtest = new LibreSpeedtest(settings, mockHttp.ToHttpClient(), new DelayProviderStub());

        // When
        var result = await speedtest.GetDownloadSpeedAsync(ServerAt(PathInstallUrl));

        // Then every request asked garbage.php inside the install for that many MiB.
        mockHttp.GetMatchCount(garbage).ShouldBe(2);
        result.RequestsSucceeded.ShouldBe(2);
    }

    [Fact]
    public async Task GetUploadSpeedAsync_AgainstALibreSpeedServer_PostsToThePingEndpointAndReportsBytes()
    {
        // SCENARIO: LibreSpeed run reports every measurement

        // Given a reachable LibreSpeed server, and the upload held open until the first under-load
        // probe has been taken, so the figure is reported rather than raced for.
        using var probing = new HeldTransfer();
        using var mockHttp = new MockHttpMessageHandler();
        var uploads = mockHttp.When(HttpMethod.Post, $"{InstallUrl}backend/empty.php")
                              .Respond(async request =>
                              {
                                  // The empty POST that resolves the upload address is answered
                                  // at once; only a measured upload, which carries a body, waits.
                                  if (request.Content?.Headers.ContentLength > 0)
                                  {
                                      await probing.UntilFirstProbeTaken();
                                  }

                                  return EmptyAnswer();
                              });
        mockHttp.When(HttpMethod.Get, $"{InstallUrl}backend/empty.php")
                .Respond(_ => EmptyAnswer());
        var settings = new LibreSpeedtestSettings(Profile.Tiny) with
        {
            LatencyTest = new() { LoadedLatencyIntervalMilliseconds = 1 },
        };
        var speedtest = new LibreSpeedtest(settings, mockHttp.ToHttpClient(), new DelayProviderStub());

        // When
        var result = await speedtest.GetUploadSpeedAsync(ServerAt(InstallUrl), null, probing.Reporter);

        // Then the bytes went to the install's upload endpoint, with the under-load figure beside
        // them.
        mockHttp.GetMatchCount(uploads).ShouldBeGreaterThan(0);
        result.BytesProcessed.ShouldBe(50 * 1024);
        result.RequestsSucceeded.ShouldBe(1);
        result.Latency.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetUploadSpeedAsync_WhenTheInstallRedirectsToHttps_MeasuresTheUploadsWhereTheyLand()
    {
        // SCENARIO: LibreSpeed run reports every measurement
        //
        // A self-hosted install behind a reverse proxy that redirects plain HTTP to HTTPS: the
        // ping and the downloads follow the redirect unnoticed, but a streamed upload body cannot
        // survive it, so the uploads must go straight to where the install really is.

        const string redirectedInstall = "https://pi.local/";

        // Given an install whose plain-HTTP address redirects every request to HTTPS, and an
        // HTTPS address that accepts the uploads.
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Post, $"{InstallUrl}backend/empty.php")
                .Respond(_ => new HttpResponseMessage(HttpStatusCode.PermanentRedirect)
                {
                    Headers = { Location = new Uri($"{redirectedInstall}backend/empty.php") }
                });
        mockHttp.When(HttpMethod.Post, $"{redirectedInstall}backend/empty.php")
                .Respond(_ => EmptyAnswer());
        var settings = new LibreSpeedtestSettings(Profile.Tiny) with
        {
            LatencyTest = new() { LoadedLatencyEnabled = false },
        };
        var speedtest = new LibreSpeedtest(settings, mockHttp.ToHttpClient(), new DelayProviderStub());

        // When the upload test runs against the address the user named.
        var result = await speedtest.GetUploadSpeedAsync(ServerAt(InstallUrl));

        // Then the uploads are measured rather than failed.
        result.RequestsFailed.ShouldBe(0);
        result.RequestsSucceeded.ShouldBe(1);
        result.BytesProcessed.ShouldBe(50 * 1024);
    }

    [Theory]
    [MemberData(nameof(FormsOfThePathInstall))]
    public async Task GetDownloadSpeedAsync_ForAnInstallUnderAPath_ProbesLatencyInsideThatPath(string given)
    {
        // Given a LibreSpeed install under a path, named in each form a user might give it, and a
        // download held open until the first under-load probe has been taken.
        using var probing = new HeldTransfer();
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When($"{PathInstallUrl}backend/empty.php")
                .Respond(_ => EmptyAnswer());
        mockHttp.When($"{PathInstallUrl}backend/garbage.php")
                .Respond(async _ =>
                {
                    await probing.UntilFirstProbeTaken();
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[1024]) };
                });
        var settings = new LibreSpeedtestSettings(Profile.Tiny) with
        {
            LatencyTest = new() { LoadedLatencyIntervalMilliseconds = 1 },
            DownloadRequestCount = 1,
        };
        var speedtest = new LibreSpeedtest(settings, mockHttp.ToHttpClient(), new DelayProviderStub());

        // When
        var result = await speedtest.GetDownloadSpeedAsync(ServerAt(given), null, probing.Reporter);

        // Then the download and its under-load probes all land inside the install.
        result.RequestsSucceeded.ShouldBe(1);
        probing.ProbedUrls.ShouldNotBeEmpty();
        probing.ProbedUrls.ShouldAllBe(url => url == $"{PathInstallUrl}backend/empty.php");
    }

    [Theory]
    [MemberData(nameof(FormsOfThePathInstall))]
    public async Task GetUploadSpeedAsync_ForAnInstallUnderAPath_UploadsAndProbesInsideThatPath(string given)
    {
        // Given a LibreSpeed install under a path, named in each form a user might give it, and an
        // upload held open until the first under-load probe has been taken.
        using var probing = new HeldTransfer();
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Post, $"{PathInstallUrl}backend/empty.php")
                .Respond(async request =>
                {
                    if (request.Content?.Headers.ContentLength > 0)
                    {
                        await probing.UntilFirstProbeTaken();
                    }

                    return EmptyAnswer();
                });
        mockHttp.When(HttpMethod.Get, $"{PathInstallUrl}backend/empty.php")
                .Respond(_ => EmptyAnswer());
        var settings = new LibreSpeedtestSettings(Profile.Tiny) with
        {
            LatencyTest = new() { LoadedLatencyIntervalMilliseconds = 1 },
        };
        var speedtest = new LibreSpeedtest(settings, mockHttp.ToHttpClient(), new DelayProviderStub());

        // When
        var result = await speedtest.GetUploadSpeedAsync(ServerAt(given), null, probing.Reporter);

        // Then the upload and its under-load probes all land inside the install.
        result.RequestsFailed.ShouldBe(0);
        result.RequestsSucceeded.ShouldBe(1);
        probing.ProbedUrls.ShouldNotBeEmpty();
        probing.ProbedUrls.ShouldAllBe(url => url == $"{PathInstallUrl}backend/empty.php");
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1025, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public async Task GetDownloadSpeedAsync_WithARequestShapeGarbageWouldNotHonour_IsRejected(int requestSizeMib, int requestCount)
    {
        // garbage.php answers a ckSize of zero or less with its own default and caps one above
        // 1024, so either would be measured as a size nobody asked for; and no requests at all
        // would report a download that never ran.

        // Given
        using var mockHttp = LibreSpeedServer(InstallUrl);
        var settings = Quick() with { DownloadRequestSizeMib = requestSizeMib, DownloadRequestCount = requestCount };
        var speedtest = new LibreSpeedtest(settings, mockHttp.ToHttpClient(), new DelayProviderStub());

        // When / Then
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => speedtest.GetDownloadSpeedAsync(ServerAt(InstallUrl)));
    }

    [Fact]
    public async Task GetServersAsync_IsNotSupported()
    {
        // Given a LibreSpeed provider; LibreSpeed publishes no server feed NetPace reads.
        using var mockHttp = LibreSpeedServer(InstallUrl);
        var speedtest = new LibreSpeedtest(Quick(), mockHttp.ToHttpClient(), new DelayProviderStub());

        // When / Then discovery says so rather than returning an empty list.
        await Should.ThrowAsync<NotSupportedException>(() => speedtest.GetServersAsync());
    }

    [Fact]
    public async Task GetFastestServerByLatencyAsync_ScreensACallerSuppliedList()
    {
        // Given two LibreSpeed installs, only one of which answers as LibreSpeed.
        using var mockHttp = new MockHttpMessageHandler();
        mockHttp.When($"{InstallUrl}backend/empty.php")
                .Respond(_ => EmptyAnswer());
        mockHttp.When("*").Respond("text/html", "<html>not librespeed</html>");
        var speedtest = new LibreSpeedtest(Quick(), mockHttp.ToHttpClient(), new DelayProviderStub());

        // When
        var fastest = await speedtest.GetFastestServerByLatencyAsync(
            [ServerAt("http://elsewhere.local/"), ServerAt(InstallUrl)]);

        // Then the one that answered as LibreSpeed is chosen, as screening a list does for Ookla.
        fastest.Server.Url.ShouldBe(InstallUrl);
    }

    /// <summary>
    /// Holds a transfer's response until the first under-load probe has been taken, so a test can
    /// assert the figure those probes produce without racing a transfer that would otherwise finish
    /// first and report nothing measured.
    /// </summary>
    /// <remarks>
    /// The probe is observed through the diagnostics channel rather than from the responder that
    /// answered it: the responder returns before the probe's sample has been recorded, so releasing
    /// the transfer there would still race. Every wait carries a hang guard, so a regression that
    /// stops probing fails the test rather than hanging the run.
    /// </remarks>
    private sealed class HeldTransfer : IDisposable
    {
        private static readonly TimeSpan HangGuard = TimeSpan.FromSeconds(10);

        private readonly TaskCompletionSource firstProbeTaken = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<RequestDiagnostic> records = [];

        public IProgress<RequestDiagnostic> Reporter { get; }

        public HeldTransfer()
        {
            Reporter = new SynchronousProgress<RequestDiagnostic>(record =>
            {
                lock (records)
                {
                    records.Add(record);
                }

                if (record is { Kind: RequestKind.LatencyProbe, Outcome: RequestOutcome.Succeeded })
                {
                    firstProbeTaken.TrySetResult();
                }
            });
        }

        /// <summary>The addresses the under-load probes were sent to.</summary>
        public IReadOnlyList<string> ProbedUrls => Snapshot(RequestKind.LatencyProbe).Select(record => record.Url).ToArray();

        public Task UntilFirstProbeTaken() => firstProbeTaken.Task.WaitAsync(HangGuard);

        public void Dispose() => firstProbeTaken.TrySetCanceled();

        private RequestDiagnostic[] Snapshot(RequestKind kind)
        {
            lock (records)
            {
                return records.Where(record => record.Kind == kind).ToArray();
            }
        }
    }
}
