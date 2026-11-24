using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using NetPace.Core.Settings;

namespace NetPace.Core;

/// <summary>
/// The measurement half of a speed test provider: the whole of <see cref="ISpeedTestService"/>
/// implemented once, over HTTP, against whatever endpoints a derived provider names.
/// </summary>
/// <remarks>
/// Everything a measurement needs is here - the idle latency probe loop and its jitter, server
/// screening, the parallel transfer with its total-byte budget, latency under load, the progress
/// and per-request diagnostic reporting, and the time and delay provider seams. A derived provider
/// supplies only what differs between kinds of server: the latency probe address and what counts
/// as a valid answer there, the download requests, the address uploads go to, and server
/// discovery.
/// <para>
/// Derived providers live in <c>NetPace.Core</c>: the only constructor is <c>internal</c> and
/// every override point is <c>private protected</c>, so a consumer can accept and use this type
/// but not extend it.
/// </para>
/// </remarks>
public abstract class SpeedTestProviderBase : ISpeedTestService
{
    private const string ByteBudgetReached = "byte budget reached";
    private const string ScreeningCeilingReached = "screening ceiling reached";
    private const string TransferEnded = "transfer ended";
    private protected const string CancelledByCaller = "cancelled by caller";
    private const string NotASpeedTestServer = "not a speed test server";

    // The status code is appended to this one.
    private protected const string EndpointProbeAnswered = "endpoint probe answered";

    // Paces the reports sent from inside a running request only. A request that finishes and is
    // counted reports at once, whatever this says.
    private const int LiveFigureIntervalMilliseconds = 250;

    private readonly HttpClient httpClient;
    private readonly SpeedTestSettings settings;
    private readonly IDelayProvider delayProvider;
    private readonly TimeProvider timeProvider;

    internal SpeedTestProviderBase(SpeedTestSettings speedtestSettings, HttpClient? httpClientOverride, IDelayProvider? delayProviderOverride, TimeProvider? timeProviderOverride)
    {
        ArgumentNullException.ThrowIfNull(speedtestSettings);

        settings = speedtestSettings;
        httpClient = httpClientOverride ?? CreateHttpClient(settings.UseProxy, settings.ProxyAddress, settings.ProxyCredential);
        delayProvider = delayProviderOverride ?? new DelayProvider();
        timeProvider = timeProviderOverride ?? TimeProvider.System;
    }

    private protected HttpClient Client => httpClient;

    private protected SpeedTestSettings SharedSettings => settings;

    #region --- What a derived provider supplies ---

    private protected abstract string LatencyProbeUrl(string serverUrl);

    // Probed against the address the transfer is actually using, not the server's listed one: an
    // upload resolves a redirect first, and the two can differ in scheme, host and port, so the
    // listed address would time a line that is carrying nothing.
    private protected virtual string LoadedLatencyProbeUrl(IServer server, string transferUrl) => LatencyProbeUrl(transferUrl);

    // A non-success status never reaches here; it has already failed the request.
    private protected abstract bool IsLatencyResponseValid(string response);

    // Null reports the HTTP failure as it came. A provider whose server the user named gives a
    // message instead, so the user is told which address answered.
    private protected virtual string? LatencyStatusFailureMessage(string probeUrl, HttpStatusCode status) => null;

    private protected abstract string LatencyResponseRejectedMessage(string probeUrl);

    // Enumerated more than once - the count before the transfer starts, the addresses again as it
    // runs - so the sequence must be re-enumerable and free of side effects. The total-byte budget
    // decides how many of these requests actually run.
    private protected abstract IEnumerable<string> DownloadRequestUrls(string serverUrl);

    // Before any redirect is resolved; see ResolveUploadUrlAsync.
    private protected abstract string UploadUrl(string serverUrl);

    /// <inheritdoc/>
    public abstract Task<IServer[]> GetServersAsync(CancellationToken cancellationToken = default);

    #endregion

    #region --- Latency ---

    /// <inheritdoc/>
    public async Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, CancellationToken cancellationToken = default)
    {
        return await GetServerLatencyAsync(serverUrl, null, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, IProgress<LatencyTestProgress> progress, CancellationToken cancellationToken = default)
    {
        return await GetServerLatencyAsync(serverUrl, progress, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, IProgress<LatencyTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default, CancellationToken stopAndReportToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverUrl);

        var server = new Server { Sponsor = Server.UnknownSponsor, Url = serverUrl };
        return await GetServerLatencyAsync(server, progress, diagnostics, cancellationToken, stopAndReportToken);
    }

    /// <inheritdoc/>
    public async Task<LatencyTestResult> GetServerLatencyAsync(IServer server, CancellationToken cancellationToken = default)
    {
        return await GetServerLatencyAsync(server, null, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<LatencyTestResult> GetServerLatencyAsync(IServer server, IProgress<LatencyTestProgress> progress, CancellationToken cancellationToken = default)
    {
        return await GetServerLatencyAsync(server, progress, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<LatencyTestResult> GetServerLatencyAsync(IServer server, IProgress<LatencyTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default, CancellationToken stopAndReportToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentException.ThrowIfNullOrWhiteSpace(server.Url);

        // Zero cancels every probe before it leaves, reporting a configuration error as a server
        // that never answered; -1 (Timeout.Infinite) lets one silent server hang the measurement.
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.LatencyTest.HttpTimeoutMilliseconds, nameof(settings.LatencyTest.HttpTimeoutMilliseconds));

        var latencyUrl = LatencyProbeUrl(server.Url);
        var pings = new List<long>();

        var maxIterations = settings.LatencyTest.LatencyTestIterations;
        var intervalMilliseconds = settings.LatencyTest.LatencyTestIntervalMilliseconds;
        var httpTimeoutMilliseconds = settings.LatencyTest.HttpTimeoutMilliseconds;

        // Which of the two tokens fired is read from the tokens themselves below, never from the
        // exception, which is the same type either way.
        using var probingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, stopAndReportToken);
        var probingToken = probingCts.Token;

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (stopAndReportToken.IsCancellationRequested)
            {
                break;
            }

            if (iteration > 0 && intervalMilliseconds > 0)
            {
                try
                {
                    await delayProvider.DelayAsync(intervalMilliseconds, probingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stopAndReportToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }

            // Stamped before the request leaves, so a record says when the request started rather
            // than when the consumer received it.
            var startedAt = timeProvider.GetLocalNow().DateTime;
            var startTimestamp = timeProvider.GetTimestamp();
            var sequence = iteration + 1;

            string testString;
            long elapsedMilliseconds;
            try
            {
                testString = await httpClient.GetStringWithTimeoutAsync(latencyUrl, TimeSpan.FromMilliseconds(httpTimeoutMilliseconds), probingToken).ConfigureAwait(false);
                elapsedMilliseconds = ElapsedMillisecondsSince(startTimestamp);
            }
            catch (Exception e)
            {
                // Frozen before anything is recorded, so the duration is the request's rather than
                // the recording work's.
                var failedElapsedMilliseconds = ElapsedMillisecondsSince(startTimestamp);

                // Checked before the caller's own token, which the next branch claims, so a
                // cancellation during a stop is still the stop.
                if (e is OperationCanceledException && stopAndReportToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    ReportStoppedRequest(diagnostics, RequestKind.LatencyProbe, startedAt, sequence, latencyUrl, failedElapsedMilliseconds);
                    break;
                }

                ReportRequestFailure(diagnostics, RequestKind.LatencyProbe, startedAt, sequence, latencyUrl, failedElapsedMilliseconds, e, cancellationToken);

                if (e is HttpRequestException { StatusCode: { } status } && LatencyStatusFailureMessage(latencyUrl, status) is { } message)
                {
                    throw new InvalidOperationException(message, e);
                }

                throw;
            }

            if (!IsLatencyResponseValid(testString))
            {
                var wrongServer = new InvalidOperationException(LatencyResponseRejectedMessage(latencyUrl));
                ReportRequestFailure(diagnostics, RequestKind.LatencyProbe, startedAt, sequence, latencyUrl, elapsedMilliseconds, wrongServer, cancellationToken, Encoding.UTF8.GetByteCount(testString));
                throw wrongServer;
            }

            ReportLatencyRequestSuccess(diagnostics, startedAt, sequence, latencyUrl, testString, elapsedMilliseconds);

            pings.Add(elapsedMilliseconds);

            // Derived the same way the result's figures are, so the last report lands on exactly
            // what the result states.
            var percentageComplete = (iteration + 1) * 100 / maxIterations;
            ReportProgress(progress, new LatencyTestProgress
            {
                PercentageComplete = percentageComplete,
                LatencyMilliseconds = (long)pings.Average(),
                JitterMilliseconds = RunningJitterMilliseconds(pings),
            });
        }

        // Nothing answered, so there is no measurement: the stop surfaces as the cancellation it
        // is rather than as a figure derived from no probes.
        if (pings.Count == 0)
        {
            stopAndReportToken.ThrowIfCancellationRequested();
        }

        return MeasuredLatency(server, pings);
    }

    // One place, so an idle measurement and one taken under load state the same figures of the
    // same samples.
    private static LatencyTestResult MeasuredLatency(IServer server, IReadOnlyList<long> probeMilliseconds) => new()
    {
        Server = server,
        LatencyMilliseconds = (long)probeMilliseconds.Average(),
        MinimumLatencyMilliseconds = probeMilliseconds.Min(),
        MaximumLatencyMilliseconds = probeMilliseconds.Max(),
        JitterMilliseconds = CalculateJitterMilliseconds(probeMilliseconds)
    };

    // The mean absolute change between consecutive probes - the interarrival-jitter family of RFC
    // 3550, not the standard deviation (docs/change-intent-records/2026-11-01-jitter-is-the-mean-change-between-consecutive-probes.md).
    // The one definition of jitter in NetPace, so an under-load figure stays comparable with an
    // idle one.
    private static long CalculateJitterMilliseconds(IReadOnlyList<long> probeMilliseconds)
    {
        if (probeMilliseconds.Count < 2)
        {
            return 0;
        }

        long totalChange = 0;
        for (var probe = 1; probe < probeMilliseconds.Count; probe++)
        {
            totalChange += Math.Abs(probeMilliseconds[probe] - probeMilliseconds[probe - 1]);
        }

        var meanChange = (double)totalChange / (probeMilliseconds.Count - 1);
        return (long)Math.Round(meanChange, MidpointRounding.AwayFromZero);
    }

    // Nothing rather than zero until there is a consecutive pair: a zero would read as perfectly
    // even round trips.
    private static long? RunningJitterMilliseconds(IReadOnlyList<long> probeMilliseconds) =>
        probeMilliseconds.Count < 2 ? null : CalculateJitterMilliseconds(probeMilliseconds);

    #endregion

    #region --- Server screening ---

    /// <inheritdoc/>
    public async Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, CancellationToken cancellationToken = default)
    {
        return await GetFastestServerByLatencyAsync(servers, null, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default)
    {
        return await GetFastestServerByLatencyAsync(servers, progress, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(servers);
        if (servers.Length == 0)
        {
            throw new ArgumentException("At least one server must be provided.", nameof(servers));
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.ServerScreening.ScreeningRequestCount, nameof(settings.ServerScreening.ScreeningRequestCount));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.ServerScreening.ServerTimeoutMilliseconds, nameof(settings.ServerScreening.ServerTimeoutMilliseconds));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.ServerScreening.FirstScreeningStageMilliseconds, nameof(settings.ServerScreening.FirstScreeningStageMilliseconds));

        var screened = new LatencyTestResult?[servers.Length];
        var screeningLock = new object();
        var serversScreened = 0;

        // Counted as results are recorded rather than scanned for at the stage boundary below: the
        // scan would run under screeningLock, where a consumer's blocking progress handler could
        // hold up the decision of whether to wait out the rest of the ceiling.
        var ranked = 0;

        // One counter for the whole pass, not one per candidate: the consumer brackets the pass in
        // a single diagnostic scope, so numbering from one per candidate would issue the same
        // sequence to a request against every server.
        var requestsIssued = 0;

        // One ceiling for the whole pass, not a budget per candidate - see ServerTimeoutMilliseconds.
        var ceilingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        async Task ScreenAndRecordAsync(int index)
        {
            var result = await ScreenServerAsync(servers[index], diagnostics, () => Interlocked.Increment(ref requestsIssued), callerToken: cancellationToken, ceilingToken: ceilingCts.Token).ConfigureAwait(false);

            // One lock, so a consumer callback is never entered concurrently and the percentage it
            // is handed matches the number screened at that moment. Nothing is reported once the
            // pass is over: a candidate abandoned at the ceiling can still finish after it.
            lock (screeningLock)
            {
                screened[index] = result;
                serversScreened++;

                if (result is not null)
                {
                    ranked++;
                }

                if (!ceilingCts.IsCancellationRequested)
                {
                    ReportProgress(progress, new SpeedTestProgress
                    {
                        PercentageComplete = serversScreened * 100 / servers.Length
                    });
                }
            }
        }

        var screenings = new Task[servers.Length];
        for (var index = 0; index < servers.Length; index++)
        {
            screenings[index] = ScreenAndRecordAsync(index);
        }

        // Spent in stages, not waited out: only a pass that has ranked nothing by the end of the
        // first stage waits for the rest
        // (docs/change-intent-records/2026-11-09-screening-ceiling-is-spent-in-stages.md). A
        // candidate counts as ranked only once its whole request loop has returned.
        var allScreenings = Task.WhenAll(screenings);
        var ceilingMilliseconds = settings.ServerScreening.ServerTimeoutMilliseconds;
        var firstStageMilliseconds = Math.Min(settings.ServerScreening.FirstScreeningStageMilliseconds, ceilingMilliseconds);
        var remainingMilliseconds = ceilingMilliseconds - firstStageMilliseconds;

        await Task.WhenAny(allScreenings, Task.Delay(firstStageMilliseconds, ceilingCts.Token)).ConfigureAwait(false);

        if (remainingMilliseconds > 0 && !allScreenings.IsCompleted && Volatile.Read(ref ranked) == 0)
        {
            await Task.WhenAny(allScreenings, Task.Delay(remainingMilliseconds, ceilingCts.Token)).ConfigureAwait(false);
        }

        // Any candidate still outstanding is abandoned rather than awaited: awaiting one would make
        // the ceiling advisory rather than a hard wall-clock bound.
        ceilingCts.Cancel();

        // Observe whatever an abandoned candidate throws, so a fault cannot vanish as an unobserved
        // task exception, and dispose only once no screening can still be reading the token.
        _ = allScreenings.ContinueWith(
            completed =>
            {
                _ = completed.Exception;
                ceilingCts.Dispose();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        // A fault here is a defect in NetPace rather than a verdict on a server, so it surfaces
        // instead of being recorded as one more unreachable candidate. Only the completed pass can
        // be read this way; a candidate abandoned at the ceiling is observed above instead.
        if (allScreenings.IsFaulted)
        {
            throw allScreenings.Exception!.GetBaseException();
        }

        cancellationToken.ThrowIfCancellationRequested();

        LatencyTestResult[] reachable;
        lock (screeningLock)
        {
            reachable = screened.OfType<LatencyTestResult>().ToArray();
        }

        if (reachable.Length == 0)
        {
            throw new NoServersAvailableException();
        }

        return reachable.OrderBy(result => result.LatencyMilliseconds).First();
    }

    // Null where the candidate cannot be ranked at all. Far cheaper than the measurement taken of
    // the winner afterwards (docs/architecture/server-screening-and-measurement.md), and the figure
    // is the fastest request that completed, because screening has no warm-up and the first request
    // carries connection setup the link is not responsible for.
    private async Task<LatencyTestResult?> ScreenServerAsync(IServer server, IProgress<RequestDiagnostic>? diagnostics, Func<int> nextSequence, CancellationToken callerToken, CancellationToken ceilingToken)
    {
        // The server list comes from a remote feed, so an entry NetPace cannot request is ranked
        // out here rather than left to throw from the transport and fail the whole selection.
        if (!Uri.TryCreate(server.Url, UriKind.Absolute, out var serverUri) ||
            (serverUri.Scheme != Uri.UriSchemeHttp && serverUri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        var latencyUrl = LatencyProbeUrl(server.Url);
        long? fastestMilliseconds = null;

        for (var request = 0; request < settings.ServerScreening.ScreeningRequestCount; request++)
        {
            if (ceilingToken.IsCancellationRequested)
            {
                break;
            }

            var startedAt = timeProvider.GetLocalNow().DateTime;
            var startTimestamp = timeProvider.GetTimestamp();
            var sequence = nextSequence();

            try
            {
                var testString = await httpClient.GetStringAsync(latencyUrl, ceilingToken).ConfigureAwait(false);
                var elapsedMilliseconds = ElapsedMillisecondsSince(startTimestamp);

                if (!IsLatencyResponseValid(testString))
                {
                    if (diagnostics is not null)
                    {
                        ReportProgress(diagnostics, new RequestDiagnostic
                        {
                            StartedAt = startedAt,
                            Kind = RequestKind.LatencyProbe,
                            Sequence = sequence,
                            Url = latencyUrl,
                            Outcome = RequestOutcome.Failed,
                            BytesProcessed = Encoding.UTF8.GetByteCount(testString),
                            ElapsedMilliseconds = elapsedMilliseconds,
                            FailureReason = NotASpeedTestServer
                        });
                    }

                    return null;
                }

                ReportLatencyRequestSuccess(diagnostics, startedAt, sequence, latencyUrl, testString, elapsedMilliseconds);

                if (fastestMilliseconds is null || elapsedMilliseconds < fastestMilliseconds)
                {
                    fastestMilliseconds = elapsedMilliseconds;
                }
            }
            catch (Exception e) when (e is HttpRequestException or IOException or OperationCanceledException)
            {
                // Only the tokens say which of the three happened. The exception type cannot: the
                // ceiling token is linked to the caller's, and HttpClient.Timeout raises the same
                // type again, so a Ctrl-C and a server that never answered would both read as
                // "ceiling reached". Frozen first, before the triage work below.
                var failedElapsedMilliseconds = ElapsedMillisecondsSince(startTimestamp);
                var cancelledByCaller = e is OperationCanceledException && callerToken.IsCancellationRequested;
                var abandonedAtCeiling = e is OperationCanceledException && !cancelledByCaller && ceilingToken.IsCancellationRequested;
                var rankedOut = !cancelledByCaller && !abandonedAtCeiling;

                if (diagnostics is not null)
                {
                    string failureReason;

                    if (rankedOut)
                    {
                        failureReason = DescribeFailure(e);
                    }
                    else if (cancelledByCaller)
                    {
                        failureReason = CancelledByCaller;
                    }
                    else
                    {
                        failureReason = ScreeningCeilingReached;
                    }

                    ReportProgress(diagnostics, new RequestDiagnostic
                    {
                        StartedAt = startedAt,
                        Kind = RequestKind.LatencyProbe,
                        Sequence = sequence,
                        Url = latencyUrl,
                        Outcome = rankedOut ? RequestOutcome.Failed : RequestOutcome.Cancelled,
                        BytesProcessed = 0,
                        ElapsedMilliseconds = failedElapsedMilliseconds,
                        FailureReason = failureReason,
                        FailureType = rankedOut ? e.GetType().Name : null
                    });
                }

                // Anything not caught above - a disposed client, a bad proxy, an exhausted socket
                // pool - is a fault in NetPace and propagates rather than being disguised as one
                // unreachable candidate.
                break;
            }
        }

        // Only the fastest request survives, so there is no run of probe times to derive jitter or
        // the extremes from: zero and not-recorded, rather than figures about one sample.
        return fastestMilliseconds is null
            ? null
            : new LatencyTestResult
            {
                Server = server,
                LatencyMilliseconds = fastestMilliseconds.Value,
                MinimumLatencyMilliseconds = null,
                MaximumLatencyMilliseconds = null,
                JitterMilliseconds = 0
            };
    }

    #endregion

    #region --- Download and upload ---

    /// <inheritdoc/>
    public async Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, CancellationToken cancellationToken = default)
    {
        return await GetDownloadSpeedAsync(server, null, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default)
    {
        return await GetDownloadSpeedAsync(server, progress, null, cancellationToken);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Downloads run in parallel batches, as many at once as
    /// <see cref="DownloadTestSettings.DownloadParallelTasks"/> allows, until the bytes returned by
    /// the downloads that completed successfully reach
    /// <see cref="DownloadTestSettings.DownloadSizeMb"/>. Downloads still in flight at that point
    /// are abandoned, and what they had already received counts towards the reported figure, so the
    /// bytes processed may exceed the cap.
    /// </remarks>
    public async Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default, CancellationToken stopAndReportToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentException.ThrowIfNullOrWhiteSpace(server.Url);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.DownloadTest.DownloadSizeMb, nameof(settings.DownloadTest.DownloadSizeMb));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.LatencyTest.LoadedLatencyIntervalMilliseconds, nameof(settings.LatencyTest.LoadedLatencyIntervalMilliseconds));

        var downloadUrls = DownloadRequestUrls(server.Url);

        Func<HttpClient, string, Action<int>, CancellationToken, Task> DownloadAndMeasureAsync = async (client, downloadUrl, recordBytesDelivered, cancellationToken) =>
        {
            using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            var buffer = ArrayPool<byte>.Shared.Rent(81920);
            try
            {
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    recordBytesDelivered(bytesRead);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        };

        var maxBytes = settings.DownloadTest.DownloadSizeMb == int.MaxValue
            ? long.MaxValue
            : (long)settings.DownloadTest.DownloadSizeMb * 1024L * 1024L;
        var downloadResult = await GenericTestSpeedAsync(server, server.Url, downloadUrls, DownloadAndMeasureAsync, url => url, progress, diagnostics, settings.DownloadTest.DownloadParallelTasks, maxBytes, sequenceOffset: 0, cancellationToken, stopAndReportToken);

        return downloadResult;
    }

    /// <inheritdoc/>
    public async Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, CancellationToken cancellationToken = default)
    {
        return await GetUploadSpeedAsync(server, null, null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default)
    {
        return await GetUploadSpeedAsync(server, progress, null, cancellationToken);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Uploads run in parallel batches, as many at once as
    /// <see cref="UploadTestSettings.UploadParallelTasks"/> allows, until the bytes sent by the
    /// uploads that completed successfully reach <see cref="UploadTestSettings.UploadSizeMb"/>.
    /// Uploads still in flight at that point are abandoned, and what they had already sent counts
    /// towards the reported figure, so the bytes processed may exceed the cap.
    /// </remarks>
    public async Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default, CancellationToken stopAndReportToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentException.ThrowIfNullOrWhiteSpace(server.Url);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.UploadTest.UploadSizeMb, nameof(settings.UploadTest.UploadSizeMb));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.LatencyTest.LoadedLatencyIntervalMilliseconds, nameof(settings.LatencyTest.LoadedLatencyIntervalMilliseconds));

        var testDataLengths = GenerateUploadDataLengths(settings.UploadTest.UploadIncrements, settings.UploadTest.UploadSizeIncrementKb, settings.UploadTest.UploadSizeIterations);

        // Any redirect in front of the upload endpoint is resolved first, because a streaming body
        // cannot survive one. A stop that lands while it is still being resolved ends the test
        // there, before any upload is sent.
        string uploadUrl;
        int probeRequests;
        try
        {
            (uploadUrl, probeRequests) = await ResolveUploadUrlAsync(UploadUrl(server.Url), diagnostics, cancellationToken, stopAndReportToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stopAndReportToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return new SpeedTestResult { BytesProcessed = 0, ElapsedMilliseconds = 0, RequestsSucceeded = 0, RequestsFailed = 0, Latency = null };
        }

        Func<HttpClient, int, Action<int>, CancellationToken, Task> UploadAndMeasureAsync = async (client, length, recordBytesDelivered, cancellationToken) =>
        {
            // Bytes written is a ceiling on what was sent, not a measurement of it: a write returns
            // once the transport has taken the data, so whatever the send buffer still held when
            // the connection closed is counted as sent. On a link slow enough for the buffer to
            // hold a whole request - the narrow profiles under docker/lab are - a cut-off upload
            // can be credited with most of its body.
            using var content = new RandomStreamContent(length, recordBytesDelivered);
            using var response = await client.PostAsync(uploadUrl, content, cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            // A handler that never serialised the body reported no chunks, so an upload the server
            // accepted is counted at the length asked for. A body partway through when it stopped
            // keeps what it wrote: topping that up would credit bytes the transport never took.
            if (content.BytesWritten == 0 && length > 0)
            {
                recordBytesDelivered(length);
            }
        };

        var maxBytes = settings.UploadTest.UploadSizeMb == int.MaxValue
            ? long.MaxValue
            : (long)settings.UploadTest.UploadSizeMb * 1024L * 1024L;
        var uploadResult = await GenericTestSpeedAsync(server, uploadUrl, testDataLengths, UploadAndMeasureAsync, _ => uploadUrl, progress, diagnostics, settings.UploadTest.UploadParallelTasks, maxBytes, sequenceOffset: probeRequests, cancellationToken, stopAndReportToken);

        return uploadResult;
    }

    // A plain-HTTP upload to a server migrated to HTTPS, or behind a reverse proxy, is answered
    // with a redirect that a streaming body cannot survive: the server closes the request stream
    // mid-write, or the body is sent twice and counted once, or dropped when the POST is replayed
    // as a GET. So the endpoint is resolved first, with a body-less POST - a redirect is decided by
    // scheme and host rather than payload, and sending no bytes keeps the probe out of the
    // measurement. A probe that is rejected or fails is recorded, not acted on. Returns how many
    // requests it issued, because the measured uploads are numbered after them.
    private async Task<(string Url, int RequestsIssued)> ResolveUploadUrlAsync(string serverUrl, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken, CancellationToken stopAndReportToken)
    {
        const int maximumHops = 5;

        using var probingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, stopAndReportToken);

        var currentUrl = serverUrl;
        var requestsIssued = 0;

        for (var hop = 0; hop < maximumHops; hop++)
        {
            var probeUrl = currentUrl;
            var (startedAt, startTimestamp) = StartRequest();
            requestsIssued++;

            try
            {
                using var probeContent = new RandomStreamContent(0);
                using var response = await Client.PostAsync(currentUrl, probeContent, probingCts.Token).ConfigureAwait(false);

                // PostAsync does not throw on a non-success status and the probe never calls
                // EnsureSuccessStatusCode, so without this the one request that can quietly
                // misdirect every upload would be the one record that always reads ok.
                var redirected = IsRedirect(response.StatusCode);
                var probeAnswered = response.IsSuccessStatusCode || redirected;

                if (diagnostics is not null)
                {
                    ReportProgress(diagnostics, new RequestDiagnostic
                    {
                        StartedAt = startedAt,
                        Kind = RequestKind.EndpointProbe,
                        Sequence = requestsIssued,
                        Url = probeUrl,
                        Outcome = probeAnswered ? RequestOutcome.Succeeded : RequestOutcome.Failed,
                        BytesProcessed = 0,
                        ElapsedMilliseconds = ElapsedMillisecondsSince(startTimestamp),
                        FailureReason = probeAnswered ? null : $"{EndpointProbeAnswered} {(int)response.StatusCode}"
                    });
                }

                // A Location that will not compose into an address is not a transport failure, so
                // it ends the hops here rather than being recorded as a failed request.
                if (redirected && response.Headers.Location is { } location)
                {
                    if (Uri.TryCreate(new Uri(currentUrl), location, out var redirectTarget))
                    {
                        currentUrl = redirectTarget.ToString();
                        continue;
                    }

                    return (currentUrl, requestsIssued);
                }

                // Where the handler followed the redirects itself, RequestUri is the final hop
                // rather than where this started.
                return (response.RequestMessage?.RequestUri?.ToString() ?? currentUrl, requestsIssued);
            }
            catch (Exception e) when (e is HttpRequestException or IOException or OperationCanceledException)
            {
                if (e is OperationCanceledException && stopAndReportToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    ReportStoppedRequest(diagnostics, RequestKind.EndpointProbe, startedAt, requestsIssued, probeUrl, ElapsedMillisecondsSince(startTimestamp));
                    throw;
                }

                ReportRequestFailure(diagnostics, RequestKind.EndpointProbe, startedAt, requestsIssued, probeUrl, ElapsedMillisecondsSince(startTimestamp), e, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                // Probing is best-effort; fall back to the configured URL.
                return (serverUrl, requestsIssued);
            }
        }

        return (currentUrl, requestsIssued);
    }

    // Permanent and temporary alike: this resolves an address rather than replaying a transfer, so
    // the method rewriting a handler applies to 301/302/303 never comes into it.
    private static bool IsRedirect(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.MovedPermanently or
        HttpStatusCode.Found or
        HttpStatusCode.SeeOther or
        HttpStatusCode.TemporaryRedirect or
        HttpStatusCode.PermanentRedirect;

    private async Task<SpeedTestResult> GenericTestSpeedAsync<T>(
        IServer server,
        string transferUrl,
        IEnumerable<T> testData,
        Func<HttpClient, T, Action<int>, CancellationToken, Task> doWork,
        Func<T, string> urlOf,
        IProgress<SpeedTestProgress>? progress,
        IProgress<RequestDiagnostic>? diagnostics,
        int parallelTasks,
        long maxBytes,
        int sequenceOffset,
        CancellationToken cancellationToken,
        CancellationToken stopAndReportToken)
    {
        object lockObject = new();
        bool wasCancelledLocally = false;

        // Two totals, answering two different questions. totalBytesDelivered is the measurement:
        // every byte that crossed the link, whatever became of the request carrying it
        // (docs/change-intent-records/2026-11-15-a-figure-counts-what-the-link-delivered.md).
        // budgetBytesReturned is what the budget is spent against, and counts only the requests
        // that completed successfully and were counted - spending the budget on delivered bytes
        // would trip it sooner and change the succeeded and failed counts.
        long totalBytesDelivered = 0;
        long budgetBytesReturned = 0;

        var completedCount = 0;
        var succeededCount = 0;
        var failedCount = 0;
        var totalCount = testData.Count();

        var throttler = new SemaphoreSlim(parallelTasks);

        // One source for all three ways a transfer ends: the caller, the caller asking for what was
        // measured, or the byte budget below. Which of them it was is read from the tokens where a
        // request is classified, not from the exception, which is the same type either way.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, stopAndReportToken);

        var testStartTimestamp = timeProvider.GetTimestamp();

        // Started where the transfer starts, not where the operation did: an upload resolves its
        // endpoint first, and a probe sent before any data moved would time an idle line.
        using var probing = LoadedLatencyProbing.Start(this, server, LoadedLatencyProbeUrl(server, transferUrl), diagnostics, cancellationToken);

        // Repeated by a report sent from inside a request, which knows what has arrived but not how
        // far through the request list the transfer is. Read and written only under lockObject, so
        // a repeat cannot carry a value a later report has replaced.
        var lastPercentageComplete = 0;

        var lastLiveReportTimestamp = testStartTimestamp;

        // One place, so the bytes, the clock and the running latency figures are always read
        // together, and the figure a consumer is shown cannot be contradicted by the result. A
        // null percentage repeats the last one reported.
        void ReportTransferProgress(int? percentageComplete = null)
        {
            if (progress is null)
            {
                return;
            }

            // Held so a consumer's IProgress is called one report at a time: two at once would let
            // the figure go backwards and hand a live display two frames to draw together. The lock
            // is re-entrant, so the finally block below, which already holds it, is unaffected -
            // and a slow consumer callback holds up whichever request is reporting.
            lock (lockObject)
            {
                lastPercentageComplete = percentageComplete ?? lastPercentageComplete;

                (long? latencyMilliseconds, long? jitterMilliseconds) = probing?.Running ?? (null, null);

                ReportProgress(progress, new SpeedTestProgress
                {
                    PercentageComplete = lastPercentageComplete,
                    BytesProcessed = Interlocked.Read(ref totalBytesDelivered),
                    ElapsedMilliseconds = ElapsedMillisecondsSince(testStartTimestamp),
                    LatencyMilliseconds = latencyMilliseconds,
                    JitterMilliseconds = jitterMilliseconds
                });
            }
        }

        // Paced rather than sent per chunk, because a consumer redraws on every report and a fast
        // link delivers thousands of chunks a second. Paced on the measurement's own clock, so a
        // test holding that clock still sees exactly the reports the finishing requests send.
        void ReportLiveFigure()
        {
            if (progress is null)
            {
                return;
            }

            var last = Interlocked.Read(ref lastLiveReportTimestamp);

            if (ElapsedMillisecondsSince(last) < LiveFigureIntervalMilliseconds)
            {
                return;
            }

            // The losers of the exchange have nothing to add: the figure is the shared running
            // total rather than theirs.
            if (Interlocked.CompareExchange(ref lastLiveReportTimestamp, timeProvider.GetTimestamp(), last) != last)
            {
                return;
            }

            ReportTransferProgress();
        }

        var sequenceIssued = sequenceOffset;

        var tasks = testData.Select(async data =>
        {
            var bytesDelivered = 0L;
            var requestFailed = false;

            // Called as each chunk crosses the link rather than totalled and returned at the end:
            // a request that ends badly never returns, and would take its total with it.
            void RecordBytesDelivered(int count)
            {
                bytesDelivered += count;
                Interlocked.Add(ref totalBytesDelivered, count);
                ReportLiveFigure();
            }

            // A request still waiting its turn when the test ends was never sent, so nothing below
            // means anything until this is set.
            var requestStarted = false;
            var sequence = 0;
            var startedAt = default(DateTime);
            var startTimestamp = 0L;
            Exception? failure = null;

            // Captured where the request ends, not in the counting block below: that block waits on
            // a lock shared with every finishing request, so timing it there would bill a request
            // for the ones queued ahead of it.
            var elapsedMilliseconds = 0L;

            try
            {
                await throttler.WaitAsync(cts.Token).ConfigureAwait(false);

                // SemaphoreSlim.WaitAsync can return a permit after its token was cancelled, when a
                // Release lands before the cancelled waiter is dequeued. Checked again so a request
                // the test ended while it waited its turn is never sent and leaves no record.
                cts.Token.ThrowIfCancellationRequested();

                sequence = Interlocked.Increment(ref sequenceIssued);
                startedAt = timeProvider.GetLocalNow().DateTime;
                startTimestamp = timeProvider.GetTimestamp();

                // Set last, so the flag never claims a start the clock read above did not reach:
                // guarding on it while startTimestamp was still zero would report the clock's whole
                // elapsed reading as this request's duration.
                requestStarted = true;

                await doWork(httpClient, data, RecordBytesDelivered, cts.Token).ConfigureAwait(false);
                elapsedMilliseconds = ElapsedMillisecondsSince(startTimestamp);
            }
            catch (Exception e)
            {
                // Captured before the clock is read, and the classification below completed before
                // it too: ElapsedMillisecondsSince calls into a caller-supplied provider, so it can
                // throw, and a request still unclassified then would be counted as a success by the
                // finally block below.
                failure = e;

                // Only a cancellation escapes the failure count, whether the byte budget, the
                // caller or a stop raised it. Whatever the request delivered stays in the
                // measurement either way.
                //
                // wasCancelledLocally is read without holding lockObject, which stays safe only
                // while cts.Cancel() sits inside the lock the finally block's gate takes and every
                // counter update sits inside that gate
                // (docs/change-intent-records/2026-11-24-the-byte-cap-race-is-benign-by-construction.md).
                if (!(e is OperationCanceledException && (wasCancelledLocally || cancellationToken.IsCancellationRequested || stopAndReportToken.IsCancellationRequested)))
                {
                    requestFailed = true;
                }

                // Before the rethrow, so a cancelled request's record carries the duration it did
                // reach.
                elapsedMilliseconds = requestStarted ? ElapsedMillisecondsSince(startTimestamp) : 0L;

                if (e is OperationCanceledException && cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
            }
            finally
            {
                try
                {
                    lock (lockObject)
                    {
                        // Read before the counting block, which may itself cancel: the request that
                        // trips the byte cap is counted, and only the ones behind it are excluded.
                        var requestExcluded = cts.IsCancellationRequested;

                        if (!requestExcluded)
                        {
                            completedCount++;

                            if (requestFailed)
                            {
                                failedCount++;
                            }
                            else
                            {
                                succeededCount++;
                                budgetBytesReturned += bytesDelivered;
                            }

                            if (budgetBytesReturned >= maxBytes)
                            {
                                wasCancelledLocally = true;
                                cts.Cancel();
                                ReportTransferProgress(100);
                            }
                            else
                            {
                                var percentageComplete = (int)((double)completedCount / totalCount * 100);

                                // Under a byte cap a run usually ends well before its last request,
                                // so the greater of the two readings is the honest one.
                                if (maxBytes != long.MaxValue)
                                {
                                    var percentageCompleteMaxBytes = (int)((double)budgetBytesReturned / maxBytes * 100);

                                    if (percentageCompleteMaxBytes > percentageComplete)
                                    {
                                        percentageComplete = percentageCompleteMaxBytes;
                                    }
                                }

                                ReportTransferProgress(percentageComplete);
                            }
                        }
                        else if (bytesDelivered > 0)
                        {
                            // Excluded from the counts but not from the figure, so the result cannot
                            // report more than the last figure the consumer was shown.
                            ReportTransferProgress();
                        }

                        if (requestStarted && diagnostics is not null)
                        {
                            RequestOutcome outcome;
                            string? failureReason = null;
                            string? failureType = null;

                            if (requestExcluded)
                            {
                                // Cancelled, to agree with the counts - but a request that failed
                                // before the cap tripped keeps its own cause below, rather than
                                // losing the only evidence of a transport fault to a race it lost
                                // after it had already failed.
                                outcome = RequestOutcome.Cancelled;

                                if (requestFailed)
                                {
                                    failureReason = DescribeFailure(failure!);
                                    failureType = failure!.GetType().Name;
                                }
                                else if (cancellationToken.IsCancellationRequested)
                                {
                                    failureReason = CancelledByCaller;
                                }
                                else if (!wasCancelledLocally && stopAndReportToken.IsCancellationRequested)
                                {
                                    // Read after the byte budget, so a request the budget excluded
                                    // keeps that reason when both happen at once.
                                    failureReason = RequestDiagnostic.StoppedByCaller;
                                }
                                else
                                {
                                    failureReason = ByteBudgetReached;
                                }
                            }
                            else if (requestFailed)
                            {
                                outcome = RequestOutcome.Failed;
                                failureReason = DescribeFailure(failure!);
                                failureType = failure!.GetType().Name;
                            }
                            else
                            {
                                outcome = RequestOutcome.Succeeded;
                            }

                            ReportProgress(diagnostics, new RequestDiagnostic
                            {
                                StartedAt = startedAt,
                                Kind = RequestKind.Transfer,
                                Sequence = sequence,
                                Url = urlOf(data),
                                Outcome = outcome,
                                BytesProcessed = bytesDelivered,
                                ElapsedMilliseconds = elapsedMilliseconds,
                                FailureReason = failureReason,
                                FailureType = failureType
                            });
                        }
                    }
                }
                finally
                {
                    throttler.Release();
                }
            }
        }).ToArray();

        await Task.WhenAll(tasks);

        // Read where the transfer ends rather than inside the result below, so neither anything
        // added between the two nor the probes' winding up is billed to it.
        var testElapsedMilliseconds = ElapsedMillisecondsSince(testStartTimestamp);

        var loadedLatency = probing?.Stop();

        return new SpeedTestResult
        {
            BytesProcessed = Interlocked.Read(ref totalBytesDelivered),
            ElapsedMilliseconds = testElapsedMilliseconds,
            RequestsSucceeded = succeededCount,
            RequestsFailed = failedCount,

            // No request moved any data, so whatever the probes saw was not a loaded line.
            Latency = succeededCount == 0 ? null : loadedLatency
        };
    }

    #endregion

    #region --- Shared helpers ---

    // A consumer callback that throws is the consumer's bug and must not fault the measurement.
    private protected static void ReportProgress<T>(IProgress<T>? progress, T value)
    {
        if (progress is null)
        {
            return;
        }

        try
        {
            progress.Report(value);
        }
        catch
        {
            // Swallowed deliberately: isolating caller-supplied callback code, not hiding an
            // internal error.
        }
    }

    // Takes the response rather than its byte count, so nothing at all is computed for a caller
    // that asked for no diagnostics - which is why the guard is in here, not at the call sites.
    private static void ReportLatencyRequestSuccess(IProgress<RequestDiagnostic>? diagnostics, DateTime startedAt, int sequence, string url, string response, long elapsedMilliseconds)
    {
        if (diagnostics is null)
        {
            return;
        }

        ReportProgress(diagnostics, new RequestDiagnostic
        {
            StartedAt = startedAt,
            Kind = RequestKind.LatencyProbe,
            Sequence = sequence,
            Url = url,
            Outcome = RequestOutcome.Succeeded,
            BytesProcessed = Encoding.UTF8.GetByteCount(response),
            ElapsedMilliseconds = elapsedMilliseconds
        });
    }

    // No exception type is recorded: the cancellation came from the caller's token, so naming
    // TaskCanceledException would dress a caller's decision up as a fault.
    private static void ReportStoppedRequest(IProgress<RequestDiagnostic>? diagnostics, RequestKind kind, DateTime startedAt, int sequence, string url, long elapsedMilliseconds)
    {
        if (diagnostics is null)
        {
            return;
        }

        ReportProgress(diagnostics, new RequestDiagnostic
        {
            StartedAt = startedAt,
            Kind = kind,
            Sequence = sequence,
            Url = url,
            Outcome = RequestOutcome.Cancelled,
            BytesProcessed = 0,
            ElapsedMilliseconds = elapsedMilliseconds,
            FailureReason = RequestDiagnostic.StoppedByCaller
        });
    }

    private protected static void ReportRequestFailure(IProgress<RequestDiagnostic>? diagnostics, RequestKind kind, DateTime startedAt, int sequence, string url, long elapsedMilliseconds, Exception failure, CancellationToken cancellationToken, long bytesProcessed = 0)
    {
        if (diagnostics is null)
        {
            return;
        }

        var cancelled = failure is OperationCanceledException && cancellationToken.IsCancellationRequested;

        ReportProgress(diagnostics, new RequestDiagnostic
        {
            StartedAt = startedAt,
            Kind = kind,
            Sequence = sequence,
            Url = url,
            Outcome = cancelled ? RequestOutcome.Cancelled : RequestOutcome.Failed,
            BytesProcessed = bytesProcessed,
            ElapsedMilliseconds = elapsedMilliseconds,
            FailureReason = cancelled ? CancelledByCaller : DescribeFailure(failure),
            FailureType = cancelled ? null : failure.GetType().Name
        });
    }

    // The whole chain, because the outermost message alone routinely names no cause at all - "The
    // SSL connection could not be established, see inner exception." - and the innermost alone
    // loses the context that makes it readable.
    private static string DescribeFailure(Exception failure)
    {
        var messages = new List<string>();

        for (Exception? current = failure; current is not null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }

        return string.Join(" <- ", messages);
    }

    // Every elapsed time a measurement states is read through here, so a caller that supplied a
    // time provider supplied the clock those measurements are taken against. Waiting stays on the
    // system clock: the screening ceiling, the HTTP timeouts and the pause between latency probes
    // bound how long to wait rather than measuring how long something took
    // (docs/change-intent-records/2026-10-31-elapsed-time-reads-the-supplied-clock.md).
    private protected long ElapsedMillisecondsSince(long startTimestamp)
    {
        var elapsed = timeProvider.GetElapsedTime(startTimestamp);

        // A caller-supplied clock is monotonic only by contract. Reported rather than clamped: a
        // negative duration divides into a negative speed, and a clamp to zero into a
        // sub-millisecond one, each reading as a real measurement.
        if (elapsed < TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                $"The supplied {nameof(TimeProvider)} reported a negative elapsed time ({elapsed}). " +
                $"{nameof(TimeProvider.GetTimestamp)} must be monotonic for a measurement to be taken against it.");
        }

        return (long)elapsed.TotalMilliseconds;
    }

    private protected (DateTime StartedAt, long StartTimestamp) StartRequest() =>
        (timeProvider.GetLocalNow().DateTime, timeProvider.GetTimestamp());

    private static HttpClient CreateHttpClient(bool useProxy, Uri? proxyAddress, NetworkCredential? proxyCredential)
    {
        var handler = new HttpClientHandler();

        if (useProxy && proxyAddress != null)
        {
            handler.Proxy = new WebProxy
            {
                Address = proxyAddress,
                Credentials = proxyCredential
            };
            handler.UseProxy = true;
        }
        else
        {
            handler.UseProxy = false;
        }

        var httpClient = new HttpClient(handler);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/112.0.0.0 Safari/537.36");
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html, application/xhtml+xml, */*");
        httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
        return httpClient;
    }

    private static IEnumerable<int> GenerateUploadDataLengths(int uploadIncrements, int baseSizeKb, int repeatsPerSize)
    {
        for (var increment = 1; increment <= uploadIncrements; increment++)
        {
            int incrementSize = increment * baseSizeKb * 1024;

            for (var repeat = 0; repeat < repeatsPerSize; repeat++)
            {
                yield return incrementSize;
            }
        }
    }

    #endregion

    // The latency probes sent while a transfer runs, whose round trips are what latency under load
    // means. Probes never overlap, and none has a time limit of its own beyond the HTTP client's
    // default, because a probe that takes a long time under load is the measurement rather than a
    // fault. They share the transfer's HttpClient without queueing behind its data: the handler
    // leaves MaxConnectionsPerServer unbounded and HTTP/1.1 does not multiplex, so a probe opens a
    // connection of its own. A probe still out when the transfer ends is discarded rather than
    // waited for, so a line too congested for any probe to answer reports nothing measured.
    private sealed class LoadedLatencyProbing : IDisposable
    {
        private readonly SpeedTestProviderBase owner;
        private readonly IServer server;
        private readonly string probeUrl;
        private readonly IProgress<RequestDiagnostic>? diagnostics;
        private readonly int intervalMilliseconds;
        private readonly CancellationToken callerToken;

        private readonly object gate = new();
        private readonly List<long> samples = [];
        private readonly CancellationTokenSource stop = new();

        private Task loop = Task.CompletedTask;
        private Probe? outstanding;
        private bool stopped;
        private int probesIssued;
        private LatencyTestResult? measured;

        // Read without the gate by a transfer reporting its own progress: taking it would let a
        // consumer's diagnostics handler, which runs under the gate, delay those reports. The
        // sample count carries presence, because a nullable long cannot be read atomically.
        private long runningLatencyMilliseconds;
        private long runningJitterMilliseconds;
        private int runningSampleCount;

        private readonly record struct Probe(int Sequence, DateTime StartedAt, long StartTimestamp);

        private LoadedLatencyProbing(SpeedTestProviderBase owner, IServer server, string probeUrl, IProgress<RequestDiagnostic>? diagnostics, CancellationToken callerToken)
        {
            this.callerToken = callerToken;
            this.owner = owner;
            this.server = server;
            this.probeUrl = probeUrl;
            this.diagnostics = diagnostics;
            this.intervalMilliseconds = owner.settings.LatencyTest.LoadedLatencyIntervalMilliseconds;
        }

        // Null where the measurement is switched off, so no caller has to re-test the setting.
        public static LoadedLatencyProbing? Start(SpeedTestProviderBase owner, IServer server, string probeUrl, IProgress<RequestDiagnostic>? diagnostics, CancellationToken callerToken)
        {
            if (!owner.settings.LatencyTest.LoadedLatencyEnabled)
            {
                return null;
            }

            var probing = new LoadedLatencyProbing(owner, server, probeUrl, diagnostics, callerToken);

            // Run off the caller's thread: a delay provider and an HTTP handler that both complete
            // synchronously, as the test doubles do, would never yield, so Start would never return
            // and the transfer would never begin.
            probing.loop = Task.Run(probing.RunAsync);
            return probing;
        }

        // Both absent until the first probe answers, and the unevenness until the second.
        public (long? LatencyMilliseconds, long? JitterMilliseconds) Running
        {
            get
            {
                var samplesTaken = Volatile.Read(ref runningSampleCount);

                if (samplesTaken == 0)
                {
                    return (null, null);
                }

                return (
                    Volatile.Read(ref runningLatencyMilliseconds),
                    samplesTaken < 2 ? null : Volatile.Read(ref runningJitterMilliseconds));
            }
        }

        // Idempotent, so a transfer that ended by throwing still abandons its probes through
        // Dispose.
        public LatencyTestResult? Stop()
        {
            // The figures are settled before anything is cancelled, so a second call returns under
            // the lock without touching a token source the loop's completion may already have
            // disposed below.
            lock (gate)
            {
                if (stopped)
                {
                    return measured;
                }

                stopped = true;

                if (outstanding is { } abandoned)
                {
                    // Reported under the lock the probe's own answer would take, so it is accounted
                    // for exactly once.
                    var reason = callerToken.IsCancellationRequested ? CancelledByCaller : TransferEnded;
                    Report(abandoned, RequestOutcome.Cancelled, response: null, owner.ElapsedMillisecondsSince(abandoned.StartTimestamp), reason, failureType: null);
                    outstanding = null;
                }

                measured = samples.Count == 0 ? null : MeasuredLatency(server, samples);
            }

            // After the figures above, so the result does not depend on how quickly the wait or the
            // outstanding request unwinds.
            stop.Cancel();

            // Nothing waits on the loop, but its faults are observed so one cannot vanish as an
            // unobserved task exception, and the token source is disposed only once no probe can
            // still be reading its token. Attached after the cancellation above, so a loop that has
            // already finished cannot dispose the source before it is cancelled.
            _ = loop.ContinueWith(
                completed =>
                {
                    _ = completed.Exception;
                    stop.Dispose();
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            return measured;
        }

        /// <inheritdoc/>
        public void Dispose() => Stop();

        private async Task RunAsync()
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    // Waited before the first probe as well as between them: a transfer has built
                    // no queue at the instant it starts, so a probe sent then would time the idle
                    // line.
                    await owner.delayProvider.DelayAsync(intervalMilliseconds, stop.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                Probe probe;

                lock (gate)
                {
                    if (stopped)
                    {
                        return;
                    }

                    probe = new Probe(probesIssued + 1, owner.timeProvider.GetLocalNow().DateTime, owner.timeProvider.GetTimestamp());
                    probesIssued = probe.Sequence;
                    outstanding = probe;
                }

                string? answer = null;
                Exception? failure = null;

                try
                {
                    // Whatever comes back counts as an answer: the endpoint already answered
                    // correctly for the idle measurement, so rejecting a sample on its body would
                    // discard a round trip over something the probe is not measuring.
                    answer = await owner.httpClient.GetStringAsync(probeUrl, stop.Token).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    failure = e;
                }

                // A caller-supplied clock that faults here, one stepping backwards, ends probing
                // for the rest of the transfer; TimeProvider.System never does.
                if (!Complete(probe, owner.ElapsedMillisecondsSince(probe.StartTimestamp), answer, failure))
                {
                    return;
                }
            }
        }

        // False where the probe had already been abandoned, in which case the loop is over.
        private bool Complete(Probe probe, long elapsedMilliseconds, string? answer, Exception? failure)
        {
            lock (gate)
            {
                // The transfer ended while this probe was still out, so it has already been
                // reported as abandoned and whatever it eventually said is discarded.
                if (stopped || outstanding?.Sequence != probe.Sequence)
                {
                    return false;
                }

                outstanding = null;

                if (failure is null)
                {
                    samples.Add(elapsedMilliseconds);

                    // Published before the diagnostics report below, so a transfer's next progress
                    // report carries this sample however long a consumer's handler takes. The count
                    // is written last, because it is what makes the two figures readable.
                    Volatile.Write(ref runningLatencyMilliseconds, (long)samples.Average());
                    Volatile.Write(ref runningJitterMilliseconds, CalculateJitterMilliseconds(samples));
                    Volatile.Write(ref runningSampleCount, samples.Count);

                    Report(probe, RequestOutcome.Succeeded, answer, elapsedMilliseconds, failureReason: null, failureType: null);
                }
                else
                {
                    // A probe that could not be taken is not a sample, and not a verdict on the
                    // transfer it was measuring, which carries on.
                    Report(probe, RequestOutcome.Failed, response: null, elapsedMilliseconds, DescribeFailure(failure), failure.GetType().Name);
                }

                return true;
            }
        }

        // Takes the response rather than its byte count, so nothing is computed for a caller that
        // asked for no diagnostics.
        private void Report(Probe probe, RequestOutcome outcome, string? response, long elapsedMilliseconds, string? failureReason, string? failureType)
        {
            if (diagnostics is null)
            {
                return;
            }

            ReportProgress(diagnostics, new RequestDiagnostic
            {
                StartedAt = probe.StartedAt,
                Kind = RequestKind.LatencyProbe,
                Sequence = probe.Sequence,
                Url = probeUrl,
                Outcome = outcome,
                BytesProcessed = response is null ? 0 : Encoding.UTF8.GetByteCount(response),
                ElapsedMilliseconds = elapsedMilliseconds,
                FailureReason = failureReason,
                FailureType = failureType
            });
        }
    }
}
