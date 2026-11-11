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
/// but not extend it. Opening it later, by widening those members, is a non-breaking change and
/// waits for a third provider to prove the shape.
/// </para>
/// </remarks>
public abstract class SpeedTestProviderBase : ISpeedTestService
{
    // Reasons a request was abandoned rather than failing on its own merits.
    private const string ByteBudgetReached = "byte budget reached";
    private const string ScreeningCeilingReached = "screening ceiling reached";
    private const string TransferEnded = "transfer ended";

    /// <summary>The failure reason recorded for a request the caller asked to stop.</summary>
    private protected const string CancelledByCaller = "cancelled by caller";

    // Reasons a request failed on its own merits with no exception behind it.
    private const string NotASpeedTestServer = "not a speed test server";

    /// <summary>
    /// The failure reason recorded for an endpoint probe whose status says the endpoint is not
    /// there; the status code is appended to it.
    /// </summary>
    private protected const string EndpointProbeAnswered = "endpoint probe answered";

    private readonly HttpClient httpClient;
    private readonly SpeedTestSettings settings;
    private readonly IDelayProvider delayProvider;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Constructs the measurement core with the settings and seams a derived provider was given.
    /// </summary>
    /// <param name="speedtestSettings">The provider's settings; the shared half of them is read here.</param>
    /// <param name="httpClientOverride">An HTTP client to use in place of the one this class would construct.</param>
    /// <param name="delayProviderOverride">A delay provider to use in place of real waiting.</param>
    /// <param name="timeProviderOverride">The clock every elapsed time is measured on; the system clock when omitted.</param>
    internal SpeedTestProviderBase(SpeedTestSettings speedtestSettings, HttpClient? httpClientOverride, IDelayProvider? delayProviderOverride, TimeProvider? timeProviderOverride)
    {
        ArgumentNullException.ThrowIfNull(speedtestSettings);

        settings = speedtestSettings;
        httpClient = httpClientOverride ?? CreateHttpClient(settings.UseProxy, settings.ProxyAddress, settings.ProxyCredential);
        delayProvider = delayProviderOverride ?? new DelayProvider();
        timeProvider = timeProviderOverride ?? TimeProvider.System;
    }

    /// <summary>The HTTP client every request of this provider travels on.</summary>
    private protected HttpClient Client => httpClient;

    /// <summary>The shared half of the provider's settings.</summary>
    private protected SpeedTestSettings SharedSettings => settings;

    #region --- What a derived provider supplies ---

    /// <summary>
    /// The address the latency probe is sent to for a server at <paramref name="serverUrl"/>.
    /// </summary>
    private protected abstract string LatencyProbeUrl(string serverUrl);

    /// <summary>
    /// The address the under-load latency probes of one transfer are sent to.
    /// </summary>
    /// <remarks>
    /// Probed against the address the transfer is actually using by default, not the server's
    /// listed one: an upload resolves a redirect first, and those can differ in scheme, host and
    /// port, so probing the listed address would time a line that is carrying nothing. A provider
    /// whose transfer address cannot have the probe path joined onto it overrides this.
    /// </remarks>
    /// <param name="server">The server being measured.</param>
    /// <param name="transferUrl">The address the transfer's requests are going to.</param>
    private protected virtual string LoadedLatencyProbeUrl(IServer server, string transferUrl) => LatencyProbeUrl(transferUrl);

    /// <summary>
    /// Whether <paramref name="response"/> is the answer a speed test server of this kind gives to
    /// a latency probe. A non-success status never reaches here; it has already failed the request,
    /// as <see cref="IsNonSuccessLatencyStatusTheWrongServer"/> describes.
    /// </summary>
    private protected abstract bool IsLatencyResponseValid(string response);

    /// <summary>
    /// Whether a non-success status from the latency probe means the address is not a speed test
    /// server of this kind, and is reported with <see cref="LatencyResponseRejectedMessage"/>
    /// rather than as the bare HTTP failure.
    /// </summary>
    /// <remarks>
    /// <see langword="false"/> by default: the status is reported as it came. A provider whose
    /// server is named by the user, where a wrong address answers with a status such as 404, says
    /// so here, so the user is told which address answered and what to check.
    /// </remarks>
    private protected virtual bool IsNonSuccessLatencyStatusTheWrongServer => false;

    /// <summary>
    /// What to tell the caller when something answered <paramref name="probeUrl"/> but not as a
    /// speed test server of this kind.
    /// </summary>
    private protected abstract string LatencyResponseRejectedMessage(string probeUrl);

    /// <summary>
    /// The download requests to make against a server at <paramref name="serverUrl"/>, in the
    /// order they are scheduled. The total-byte budget decides how many of them actually run.
    /// </summary>
    /// <remarks>
    /// Enumerated more than once, so the sequence must be re-enumerable and free of side
    /// effects: the count is read before the transfer starts, and the addresses again as it runs.
    /// </remarks>
    private protected abstract IEnumerable<string> DownloadRequestUrls(string serverUrl);

    /// <summary>
    /// The address uploads are sent to for a server at <paramref name="serverUrl"/>, before any
    /// redirect is resolved; see <see cref="ResolveUploadUrlAsync"/>.
    /// </summary>
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
    public async Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, IProgress<LatencyTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverUrl);

        var server = new Server { Sponsor = Server.UnknownSponsor, Url = serverUrl };
        return await GetServerLatencyAsync(server, progress, diagnostics, cancellationToken);
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
    public async Task<LatencyTestResult> GetServerLatencyAsync(IServer server, IProgress<LatencyTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentException.ThrowIfNullOrWhiteSpace(server.Url);

        // Guarded here because zero cancels every probe before it leaves, which surfaces as a
        // timeout and would report a configuration error as a server that never answered; and -1
        // (Timeout.Infinite) would remove the bound on a probe altogether, letting one server that
        // never answers hang the measurement.
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.LatencyTest.HttpTimeoutMilliseconds, nameof(settings.LatencyTest.HttpTimeoutMilliseconds));

        var latencyUrl = LatencyProbeUrl(server.Url);
        var pings = new List<long>();

        var maxIterations = settings.LatencyTest.LatencyTestIterations;
        var intervalMilliseconds = settings.LatencyTest.LatencyTestIntervalMilliseconds;
        var httpTimeoutMilliseconds = settings.LatencyTest.HttpTimeoutMilliseconds;

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Add delay between iterations (not before first iteration)
            if (iteration > 0 && intervalMilliseconds > 0)
            {
                await delayProvider.DelayAsync(intervalMilliseconds, cancellationToken).ConfigureAwait(false);
            }

            // Stamped before the request leaves, so a record describes when the request started
            // rather than when the consumer happened to receive it.
            var startedAt = timeProvider.GetLocalNow().DateTime;
            var startTimestamp = timeProvider.GetTimestamp();
            var sequence = iteration + 1;

            string testString;
            long elapsedMilliseconds;
            try
            {
                testString = await httpClient.GetStringWithTimeoutAsync(latencyUrl, TimeSpan.FromMilliseconds(httpTimeoutMilliseconds), cancellationToken).ConfigureAwait(false);
                elapsedMilliseconds = ElapsedMillisecondsSince(startTimestamp);
            }
            catch (Exception e)
            {
                // The failure still surfaces to the caller - a latency measurement that cannot be
                // taken is not a measurement. It is recorded first, so a diagnostic log names the
                // cause of a run that ended here, and the duration is frozen before that recording.
                // The record keeps the HTTP failure itself, status and all, for whoever triages it.
                var failedElapsedMilliseconds = ElapsedMillisecondsSince(startTimestamp);
                ReportRequestFailure(diagnostics, RequestKind.LatencyProbe, startedAt, sequence, latencyUrl, failedElapsedMilliseconds, e, cancellationToken);

                if (e is HttpRequestException { StatusCode: not null } && IsNonSuccessLatencyStatusTheWrongServer)
                {
                    throw new InvalidOperationException(LatencyResponseRejectedMessage(latencyUrl), e);
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

            // Record this ping time
            pings.Add(elapsedMilliseconds);

            // Report progress after each iteration, with the figures of the probes that have
            // answered so far, so a consumer can show a latency that climbs rather than one that
            // appears only at the end. The running figures are derived the same way the result's
            // are, so the last report lands on the figures the result states.
            var percentageComplete = (iteration + 1) * 100 / maxIterations;
            ReportProgress(progress, new LatencyTestProgress
            {
                PercentageComplete = percentageComplete,
                LatencyMilliseconds = (long)pings.Average(),
                JitterMilliseconds = RunningJitterMilliseconds(pings),
            });
        }

        // The average server latency, the extremes behind it, and how uneven the probes behind it
        // were.
        return MeasuredLatency(server, pings);
    }

    /// <summary>
    /// The latency figures of one run of probe times: their average, the extremes behind it, and
    /// how uneven they were. Derived in one place, so an idle measurement and a measurement taken
    /// under load state the same figures of the same samples.
    /// </summary>
    private static LatencyTestResult MeasuredLatency(IServer server, IReadOnlyList<long> probeMilliseconds) => new()
    {
        Server = server,
        LatencyMilliseconds = (long)probeMilliseconds.Average(),
        MinimumLatencyMilliseconds = probeMilliseconds.Min(),
        MaximumLatencyMilliseconds = probeMilliseconds.Max(),
        JitterMilliseconds = CalculateJitterMilliseconds(probeMilliseconds)
    };

    /// <summary>
    /// How uneven a run of probe times was: the mean of the absolute differences between
    /// consecutive probes, in whole milliseconds, rounded to nearest so that half a millisecond or
    /// more does not read as none. Fewer than two probes leaves no consecutive pair to compare and
    /// reports zero.
    /// </summary>
    /// <remarks>
    /// This is the interarrival-jitter family of RFC 3550, chosen over the standard deviation
    /// because it measures the change from one reply to the next — what a caller on the line
    /// hears — rather than the spread of the probes around their average. It is the one definition
    /// of jitter in NetPace, so a figure measured under load stays comparable with an idle one.
    /// </remarks>
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

    /// <summary>
    /// <see cref="CalculateJitterMilliseconds"/> as a measurement still running reports it, or
    /// <see langword="null"/> where there is not yet a consecutive pair to compare. A running
    /// figure says nothing rather than zero, because a zero would read as perfectly even round
    /// trips.
    /// </summary>
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

        // Counted as results are recorded rather than scanned for at the stage boundary below: a
        // consumer's progress callback is invoked under screeningLock, so a handler of theirs that
        // blocks would otherwise hold up the decision of whether to wait out the rest of the
        // ceiling.
        var ranked = 0;

        // One counter for the whole pass, not one per candidate: the consumer brackets the entire
        // pass in a single diagnostic scope, so numbering each candidate from one would issue the
        // same sequence to a request against every server.
        var requestsIssued = 0;

        // One ceiling for the whole pass, not a budget per candidate - see ServerTimeoutMilliseconds.
        var ceilingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        async Task ScreenAndRecordAsync(int index)
        {
            var result = await ScreenServerAsync(servers[index], diagnostics, () => Interlocked.Increment(ref requestsIssued), callerToken: cancellationToken, ceilingToken: ceilingCts.Token).ConfigureAwait(false);

            // Recorded and reported under one lock, so a consumer callback is never entered
            // concurrently and the percentage it is handed matches the number screened at that
            // moment. Nothing is reported once the pass is over, because a candidate abandoned at
            // the ceiling can still finish after the caller has moved on.
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

        // The ceiling is reached in stages rather than waited out. A pass that has ranked a
        // candidate by the end of the first stage takes it and moves on, so a link whose candidates
        // finish all their screening requests inside that stage chooses a server as fast as it did
        // when the first stage was the whole ceiling, however long the ceiling now is. A slower one
        // does not: a candidate counts as ranked only once its whole request loop has returned.
        // Only a pass that has ranked nothing - the run that would
        // otherwise end in "no servers available" - waits for the rest, which is what gives a link
        // with a round trip of a second or two the time to answer at all. The second stage then
        // waits for every candidate rather than the first one ranked, so the ranking still compares
        // every server that answers inside the ceiling.
        var allScreenings = Task.WhenAll(screenings);
        var ceilingMilliseconds = settings.ServerScreening.ServerTimeoutMilliseconds;
        var firstStageMilliseconds = Math.Min(settings.ServerScreening.FirstScreeningStageMilliseconds, ceilingMilliseconds);
        var remainingMilliseconds = ceilingMilliseconds - firstStageMilliseconds;

        await Task.WhenAny(allScreenings, Task.Delay(firstStageMilliseconds, ceilingCts.Token)).ConfigureAwait(false);

        // A ceiling at or below the first stage leaves nothing remaining, and is spent in one stage.
        if (remainingMilliseconds > 0 && !allScreenings.IsCompleted && Volatile.Read(ref ranked) == 0)
        {
            await Task.WhenAny(allScreenings, Task.Delay(remainingMilliseconds, ceilingCts.Token)).ConfigureAwait(false);
        }

        // Stop the ceiling timer, and abandon any candidate still outstanding rather than waiting
        // on it: the ceiling is a wall-clock bound on choosing a server, and awaiting an
        // outstanding request would make it advisory rather than hard.
        ceilingCts.Cancel();

        // An abandoned candidate is still tidied up after. Observe whatever it throws, so a fault
        // cannot vanish as an unobserved task exception, and dispose the ceiling only once no
        // screening can still be reading its token.
        _ = allScreenings.ContinueWith(
            completed =>
            {
                _ = completed.Exception;
                ceilingCts.Dispose();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        // A screening that faulted is a defect in NetPace rather than a verdict on a server, so it
        // surfaces instead of being recorded as one more unreachable candidate. Only a fault from
        // the completed pass can be surfaced this way; one from a candidate abandoned at the
        // ceiling is observed above, because nothing is waiting on it by then.
        if (allScreenings.IsFaulted)
        {
            throw allScreenings.Exception!.GetBaseException();
        }

        // Honour any user cancellation during the screening pass.
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

    /// <summary>
    /// Screens one server - is it reachable, and roughly how fast? - returning <c>null</c> when the
    /// candidate cannot be ranked: its URL is missing or not a web address, no request completed
    /// before the pass ended, or what answered was not a speed test server. A server that answered
    /// correctly at least once returns a figure and simply ranks lower when it is slow.
    /// </summary>
    /// <remarks>
    /// Deliberately far cheaper than <see cref="GetServerLatencyAsync(IServer, CancellationToken)"/>,
    /// which is the measurement taken of the winner afterwards: a few requests with no deliberate
    /// waiting between them, and a partial answer is kept rather than discarded. The figure is the
    /// fastest request that completed, because screening has no warm-up and the first request
    /// carries connection setup the link itself is not responsible for.
    /// </remarks>
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
                    // Something answered, but it is not a speed test server.
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
                // A candidate abandoned at the ceiling is distinguished from one that could not be
                // reached: both rank out, but only one of them is a verdict on the server. The
                // exception type alone cannot tell them apart, because the ceiling token is linked
                // to the caller's and HttpClient.Timeout raises the same type again - so a Ctrl-C
                // and a server that never answered would both read as "ceiling reached", each a
                // statement about the wrong thing. Only the tokens say which actually happened.
                //
                // Frozen first, as the success path does: read inside the record below instead, the
                // figure would carry the triage and failure-description work done in between.
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

                // Unreachable, or abandoned at the ceiling: ranked out rather than raised, because
                // finding that out is what screening is for. Whatever earlier requests achieved
                // still counts, and the surrounding pass reports when no candidate answered at all.
                // Anything else - a disposed client, a bad proxy, an exhausted socket pool - is a
                // fault in NetPace rather than a verdict on this server, so it is left to propagate
                // instead of being disguised as one unreachable candidate.
                break;
            }
        }

        // Screening times each of its requests but keeps only the fastest, so no run of probe
        // times survives to derive a jitter figure from. Reported as zero rather than measured
        // from requests issued back to back for ranking, and the extremes as not recorded.
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
    /// Downloads are processed in parallel batches, as many at once as
    /// <see cref="DownloadTestSettings.DownloadParallelTasks"/> allows. The total-byte budget cap is
    /// read from <see cref="DownloadTestSettings.DownloadSizeMb"/>; once the running total crosses
    /// that threshold the internal <see cref="CancellationTokenSource"/> is cancelled, so in-flight
    /// parallel downloads are cancelled rather than awaited and their bytes excluded. The actual
    /// bytes processed may still exceed the cap depending on parallelism and per-request size.
    /// </remarks>
    public async Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentException.ThrowIfNullOrWhiteSpace(server.Url);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.DownloadTest.DownloadSizeMb, nameof(settings.DownloadTest.DownloadSizeMb));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.LatencyTest.LoadedLatencyIntervalMilliseconds, nameof(settings.LatencyTest.LoadedLatencyIntervalMilliseconds));

        var downloadUrls = DownloadRequestUrls(server.Url);

        // Download content from a specified URL and return the size of the data in bytes.
        Func<HttpClient, string, CancellationToken, Task<int>> DownloadAndMeasureAsync = async (client, downloadUrl, cancellationToken) =>
        {
            // Stream the response to avoid allocating large strings for each download.
            using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            var buffer = ArrayPool<byte>.Shared.Rent(81920); // 80KB buffer
            try
            {
                long total = 0;
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    total += bytesRead;
                }

                return (int)total;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        };

        var maxBytes = settings.DownloadTest.DownloadSizeMb == int.MaxValue
            ? long.MaxValue
            : (long)settings.DownloadTest.DownloadSizeMb * 1024L * 1024L;
        var downloadResult = await GenericTestSpeedAsync(server, server.Url, downloadUrls, DownloadAndMeasureAsync, url => url, progress, diagnostics, settings.DownloadTest.DownloadParallelTasks, maxBytes, sequenceOffset: 0, cancellationToken);

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
    /// Uploads are processed in parallel batches, as many at once as
    /// <see cref="UploadTestSettings.UploadParallelTasks"/> allows. The total-byte budget cap is
    /// read from <see cref="UploadTestSettings.UploadSizeMb"/>; once the running total crosses that
    /// threshold the internal <see cref="CancellationTokenSource"/> is cancelled, so in-flight
    /// parallel uploads are cancelled rather than awaited and their bytes excluded. The actual
    /// bytes processed may still exceed the cap depending on parallelism and per-request size.
    /// </remarks>
    public async Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentException.ThrowIfNullOrWhiteSpace(server.Url);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.UploadTest.UploadSizeMb, nameof(settings.UploadTest.UploadSizeMb));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.LatencyTest.LoadedLatencyIntervalMilliseconds, nameof(settings.LatencyTest.LoadedLatencyIntervalMilliseconds));

        // Generate upload sizes (in bytes) rather than allocating large buffers up-front.
        var testDataLengths = GenerateUploadDataLengths(settings.UploadTest.UploadIncrements, settings.UploadTest.UploadSizeIncrementKb, settings.UploadTest.UploadSizeIterations);

        // Where uploads go is the provider's business, but any redirect in front of it is resolved
        // first, because a streaming body cannot survive one. Whatever requests that takes are
        // numbered before the measured uploads.
        var (uploadUrl, probeRequests) = await ResolveUploadUrlAsync(UploadUrl(server.Url), diagnostics, cancellationToken).ConfigureAwait(false);

        // Upload content to a specified URL and return the size of the data in bytes.
        Func<HttpClient, int, CancellationToken, Task<int>> UploadAndMeasureAsync = async (client, length, cancellationToken) =>
        {
            // Use RandomStreamContent to stream generated random bytes in small chunks to avoid LOH allocations.
            using var content = new RandomStreamContent(length);
            using var response = await client.PostAsync(uploadUrl, content, cancellationToken).ConfigureAwait(false);

            // A rejected upload (non-success status) is a failed request, not throughput -
            // mirror the download path so error statuses are aggregated into the failure count.
            response.EnsureSuccessStatusCode();
            return length;
        };

        var maxBytes = settings.UploadTest.UploadSizeMb == int.MaxValue
            ? long.MaxValue
            : (long)settings.UploadTest.UploadSizeMb * 1024L * 1024L;
        var uploadResult = await GenericTestSpeedAsync(server, uploadUrl, testDataLengths, UploadAndMeasureAsync, _ => uploadUrl, progress, diagnostics, settings.UploadTest.UploadParallelTasks, maxBytes, sequenceOffset: probeRequests, cancellationToken);

        return uploadResult;
    }

    /// <summary>
    /// Resolves the address the server actually wants uploads sent to, following any redirects
    /// with an empty probe POST before the measured uploads begin.
    /// </summary>
    /// <remarks>
    /// Ookla is migrating its fleet to HTTPS, and a migrated server answers a plain-HTTP upload
    /// POST with a redirect; so does a self-hosted server behind a reverse proxy that sends plain
    /// HTTP to HTTPS. The streaming body cannot survive that redirect - the server responds and
    /// closes the request stream while the body is still being written, or the body is sent twice
    /// and counted once, or dropped altogether when the POST is replayed as a GET - so the real
    /// endpoint is resolved once, cheaply, and the uploads go straight to it.
    /// <para>
    /// The probe carries no body: a redirect is decided by scheme and host rather than payload, so
    /// an empty POST draws the same answer with nothing to write and nothing to tear down mid-write.
    /// Sending no bytes also keeps the probe out of the measurement entirely. Whether the redirect is
    /// followed by the underlying handler or reported back to us, the endpoint that answered the probe
    /// is the one returned - whether or not it answered with success. A rejection is recorded so a
    /// triager can see it, not acted on. The probe never fails the test, other than on caller
    /// cancellation, which propagates: if it cannot complete at all, the original URL is used and
    /// any real fault surfaces through the uploads themselves.
    /// </para>
    /// </remarks>
    /// <returns>
    /// The address to upload to, and how many requests the probe issued getting there - the
    /// measured uploads are numbered after those. The under-load latency probes are numbered
    /// separately, so a sequence number is distinct only within one kind; see
    /// <see cref="RequestDiagnostic.Sequence"/>.
    /// </returns>
    private async Task<(string Url, int RequestsIssued)> ResolveUploadUrlAsync(string serverUrl, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken)
    {
        const int maximumHops = 5;

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
                using var response = await Client.PostAsync(currentUrl, probeContent, cancellationToken).ConfigureAwait(false);

                // PostAsync does not throw on a non-success status and the probe never calls
                // EnsureSuccessStatusCode, so without this the one request that can quietly
                // misdirect every upload that follows is the one record that always reads ok.
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

                // The handler did not follow the redirect for us - follow it explicitly. A
                // Location that will not compose into an address is not a transport failure, so it
                // ends the hops here rather than being recorded as a failed request.
                if (redirected && response.Headers.Location is { } location)
                {
                    if (Uri.TryCreate(new Uri(currentUrl), location, out var redirectTarget))
                    {
                        currentUrl = redirectTarget.ToString();
                        continue;
                    }

                    return (currentUrl, requestsIssued);
                }

                // Otherwise the endpoint that answered is the one to upload to. When the handler
                // followed redirects itself, that is the final hop rather than where we started.
                return (response.RequestMessage?.RequestUri?.ToString() ?? currentUrl, requestsIssued);
            }
            catch (Exception e) when (e is HttpRequestException or IOException or OperationCanceledException)
            {
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

    /// <summary>
    /// Determines whether a status code asks the caller to repeat the request elsewhere.
    /// </summary>
    /// <remarks>
    /// The permanent and temporary redirects are treated alike because this resolves an address
    /// rather than replaying a transfer: an HTTPS migration is as likely to be announced with a
    /// permanent 301 as with the 307 that prompted this, and either way the answer we want is
    /// simply "go here instead". The method rewriting that a handler applies to 301/302/303 when
    /// replaying a request does not apply - the probe is discarded once its address is known.
    /// </remarks>
    private static bool IsRedirect(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.MovedPermanently or
        HttpStatusCode.Found or
        HttpStatusCode.SeeOther or
        HttpStatusCode.TemporaryRedirect or
        HttpStatusCode.PermanentRedirect;

    /// <summary>
    /// Executes a generic speed test by processing a collection of test data in parallel,
    /// measuring total bytes processed and elapsed time - and, where that measurement is on, the
    /// latency to the server under that load.
    /// </summary>
    private async Task<SpeedTestResult> GenericTestSpeedAsync<T>(
        IServer server,
        string transferUrl,
        IEnumerable<T> testData,
        Func<HttpClient, T, CancellationToken, Task<int>> doWork,
        Func<T, string> urlOf,
        IProgress<SpeedTestProgress>? progress,
        IProgress<RequestDiagnostic>? diagnostics,
        int parallelTasks,
        long maxBytes,
        int sequenceOffset,
        CancellationToken cancellationToken)
    {
        object lockObject = new();
        bool wasCancelledLocally = false;
        long totalBytesReturned = 0;

        var completedCount = 0;
        var succeededCount = 0;
        var failedCount = 0;
        var totalCount = testData.Count();

        var throttler = new SemaphoreSlim(parallelTasks);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var testStartTimestamp = timeProvider.GetTimestamp();

        // Started where the transfer starts, so its first probe is due one interval into the
        // transfer rather than one interval into the whole operation: an upload resolves its
        // endpoint first, and a probe sent before any data moved would time an idle line.
        using var probing = LoadedLatencyProbing.Start(this, server, LoadedLatencyProbeUrl(server, transferUrl), diagnostics, cancellationToken);

        // One place that composes a transfer's progress report, so the bytes, the clock and the
        // running latency figures are always read together. Nothing is computed where no consumer
        // asked for progress.
        void ReportTransferProgress(int percentageComplete, long bytesProcessed)
        {
            if (progress is null)
            {
                return;
            }

            (long? latencyMilliseconds, long? jitterMilliseconds) = probing?.Running ?? (null, null);

            ReportProgress(progress, new SpeedTestProgress
            {
                PercentageComplete = percentageComplete,
                BytesProcessed = bytesProcessed,
                ElapsedMilliseconds = ElapsedMillisecondsSince(testStartTimestamp),
                LatencyMilliseconds = latencyMilliseconds,
                JitterMilliseconds = jitterMilliseconds
            });
        }

        // Create and execute tasks to process the test data in parallel.
        var sequenceIssued = sequenceOffset;

        var tasks = testData.Select(async data =>
        {
            var bytesReturned = 0;
            var requestFailed = false;

            // A request still waiting its turn when the test ends was never sent, so it has nothing
            // to report; everything below is only meaningful once it has started.
            var requestStarted = false;
            var sequence = 0;
            var startedAt = default(DateTime);
            var startTimestamp = 0L;
            Exception? failure = null;

            // Captured where the request actually ends rather than read in the counting block
            // below: that block waits on a lock shared with every other finishing request, and
            // runs this request's own progress callback on the way through. Timing it there would
            // bill a request for the ones queued ahead of it, so bytes/duration per record would not
            // reconcile with the measured throughput.
            var elapsedMilliseconds = 0L;

            try
            {
                // Limit concurrent executions by waiting for a permit from the semaphore.
                await throttler.WaitAsync(cts.Token).ConfigureAwait(false);

                sequence = Interlocked.Increment(ref sequenceIssued);
                startedAt = timeProvider.GetLocalNow().DateTime;
                startTimestamp = timeProvider.GetTimestamp();

                // Set last, so the flag never claims a start that the clock read above did not
                // reach. Guarding on it while startTimestamp was still zero would time a request
                // from the clock's own epoch and report its whole elapsed reading as this
                // request's duration.
                requestStarted = true;

                // Perform the work and retrieve the processed byte count.
                bytesReturned = await doWork(httpClient, data, cts.Token).ConfigureAwait(false);
                elapsedMilliseconds = ElapsedMillisecondsSince(startTimestamp);
            }
            catch (Exception e)
            {
                // Captured before the clock is read. ElapsedMillisecondsSince calls into a
                // caller-supplied provider, so it can throw; leaving failure unset until after it
                // would let a clock fault classify a failed request as a success in the finally
                // block below, and substitute itself for the transport error that actually happened.
                failure = e;

                // A cancellation raised locally because the byte budget was reached is not a
                // failure - the request is simply excluded. Any other exception (transport error,
                // TLS, timeout, or a non-success HTTP status surfaced by EnsureSuccessStatusCode)
                // is a per-request failure, aggregated into the counts rather than swallowed. Its
                // bytes remain zero.
                //
                // wasCancelledLocally is read here without holding lockObject, deliberately. This is
                // a benign race, not a correctness bug: bool loads are atomic in .NET, so the read
                // cannot tear and staleness is the only failure mode to account for. It is also not
                // what classifies the request - the authoritative exclusion is the
                // `if (!cts.IsCancellationRequested)` gate in the finally block below, which guards
                // completedCount, failedCount, succeededCount and totalBytesReturned alike.
                //
                // The flag is set and cts.Cancel() called on the next line inside the same lock the
                // gate acquires, so no thread can pass the gate between those two statements. Any
                // request whose counts are applied therefore ran its gate before cancellation, which
                // means its read of wasCancelledLocally was not stale; and cancellation is monotonic,
                // so there is no path back to false. A stale `false` read can only set requestFailed
                // on a request whose counts are discarded anyway.
                //
                // This argument breaks if cts.Cancel() moves out of that lock, or if any counter
                // update moves outside the gate.
                //
                // Genuine user cancellation (the caller's token) is not a per-request failure
                // either; it is named here rather than left to the rethrow below, so that the
                // classification is complete before the clock is read.
                if (!(e is OperationCanceledException && (wasCancelledLocally || cancellationToken.IsCancellationRequested)))
                {
                    requestFailed = true;
                }

                // Read after the classification above, which must not depend on a caller-supplied
                // clock: ElapsedMillisecondsSince calls into one, so it can throw, and a request
                // still unclassified at that point would be counted as a success by the finally
                // block below. Read before the rethrow, so a cancelled request's record carries the
                // duration it did reach.
                elapsedMilliseconds = requestStarted ? ElapsedMillisecondsSince(startTimestamp) : 0L;

                // Genuine user cancellation must propagate.
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
                        // Read under the same lock as the gate, and before the counting block that
                        // may itself cancel: the request that trips the byte cap is counted, and
                        // only the ones behind it are excluded. A record and the counts must agree.
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
                                totalBytesReturned += bytesReturned;
                            }

                            if (totalBytesReturned >= maxBytes)
                            {
                                // Configured byte cap is hit.
                                wasCancelledLocally = true;
                                cts.Cancel();
                                ReportTransferProgress(100, totalBytesReturned);
                            }
                            else
                            {
                                // Update the completion percentage.
                                var percentageComplete = (int)((double)completedCount / totalCount * 100);

                                if (maxBytes != long.MaxValue)
                                {
                                    // When a configured byte cap is in effect,
                                    // we should defer to the greater % complete value.

                                    var percentageCompleteMaxBytes = (int)((double)totalBytesReturned / maxBytes * 100);

                                    if (percentageCompleteMaxBytes > percentageComplete)
                                    {
                                        percentageComplete = percentageCompleteMaxBytes;
                                    }
                                }

                                ReportTransferProgress(percentageComplete, totalBytesReturned);
                            }
                        }

                        if (requestStarted && diagnostics is not null)
                        {
                            RequestOutcome outcome;
                            string? failureReason = null;
                            string? failureType = null;

                            if (requestExcluded)
                            {
                                // Excluded from the counts, so the record reads Cancelled to agree
                                // with them. But a request that genuinely failed before the cap
                                // tripped still carries its cause: overwriting it with "byte budget
                                // reached" would throw away the only evidence of a transport fault,
                                // because of a race the request lost only after it had already
                                // failed on its own merits.
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
                                BytesProcessed = bytesReturned,
                                ElapsedMilliseconds = elapsedMilliseconds,
                                FailureReason = failureReason,
                                FailureType = failureType
                            });
                        }
                    }
                }
                finally
                {
                    // Release the semaphore to allow another task to proceed.
                    // This must always execute, even if UpdateProgress throws.
                    throttler.Release();
                }
            }

            return bytesReturned;
        }).ToArray();

        // Wait for all tasks to complete.
        await Task.WhenAll(tasks);

        // Read where the speed test actually ends, rather than inside the result below, so that
        // anything later added between the two is not billed to the transfer. Probing is stopped
        // after it, so the probes' own winding up is not billed either.
        var testElapsedMilliseconds = ElapsedMillisecondsSince(testStartTimestamp);

        var loadedLatency = probing?.Stop();

        return new SpeedTestResult
        {
            BytesProcessed = totalBytesReturned,
            ElapsedMilliseconds = testElapsedMilliseconds,
            RequestsSucceeded = succeededCount,
            RequestsFailed = failedCount,

            // No request moved any data, so whatever the probes saw was not a loaded line and
            // reporting it as latency under load would be a figure about nothing.
            Latency = succeededCount == 0 ? null : loadedLatency
        };
    }

    #endregion

    #region --- Shared helpers ---

    /// <summary>
    /// Reports progress to the consumer, isolating the operation from a throwing callback.
    /// Progress is best-effort telemetry for the caller's UI; a callback that throws is the
    /// consumer's bug and MUST NOT fault the running speed test, so its exception is swallowed.
    /// </summary>
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
            // Intentionally swallowed: a misbehaving consumer progress callback must never
            // break the measurement. This is the one legitimate catch-all — isolating
            // caller-supplied callback code — not a swallowed internal error.
        }
    }

    /// <summary>
    /// Reports a latency probe that answered correctly, whether it was taking the measurement or
    /// screening a candidate.
    /// </summary>
    /// <remarks>
    /// The response is passed rather than its byte count so that nothing at all is computed for a
    /// caller that asked for no diagnostics, which is why the guard lives in here rather than at
    /// the call sites.
    /// </remarks>
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

    /// <summary>
    /// Reports a request that did not succeed, distinguishing one the caller cancelled from one
    /// that genuinely failed.
    /// </summary>
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

    /// <summary>
    /// The whole exception chain's messages, outermost first, joined with <c>" &lt;- "</c>.
    /// </summary>
    /// <remarks>
    /// The outermost message alone is routinely useless - "The SSL connection could not be
    /// established, see inner exception." names no cause at all - and the innermost alone loses the
    /// context that makes it readable.
    /// </remarks>
    private static string DescribeFailure(Exception failure)
    {
        var messages = new List<string>();

        for (Exception? current = failure; current is not null; current = current.InnerException)
        {
            messages.Add(current.Message);
        }

        return string.Join(" <- ", messages);
    }

    /// <summary>
    /// Milliseconds elapsed on this instance's time provider since a
    /// <see cref="TimeProvider.GetTimestamp"/> reading from it.
    /// </summary>
    /// <remarks>
    /// Every elapsed time the provider measures is read through here, so a caller that supplied a
    /// time provider is supplying the clock each measurement is taken against. Waiting is a
    /// separate concern and stays on the system clock: the screening ceiling, the per-request HTTP
    /// timeouts and the pause between latency probes are bounds on how long to wait, not
    /// measurements of how long something took.
    /// </remarks>
    /// <param name="startTimestamp">A timestamp previously read from this provider's clock.</param>
    /// <exception cref="InvalidOperationException">
    /// The supplied time provider reported a negative duration.
    /// </exception>
    private protected long ElapsedMillisecondsSince(long startTimestamp)
    {
        var elapsed = timeProvider.GetElapsedTime(startTimestamp);

        // Stopwatch was monotonic by construction; a caller-supplied clock is monotonic only by
        // contract. Reported rather than clamped: a negative duration divides into a negative speed
        // that reads as a real measurement, and a clamp to zero reads as a real sub-millisecond one.
        if (elapsed < TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                $"The supplied {nameof(TimeProvider)} reported a negative elapsed time ({elapsed}). " +
                $"{nameof(TimeProvider.GetTimestamp)} must be monotonic for a measurement to be taken against it.");
        }

        return (long)elapsed.TotalMilliseconds;
    }

    /// <summary>
    /// The moment a request is about to leave, as a diagnostic record stamps it, paired with a
    /// timestamp to measure its duration against.
    /// </summary>
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

    /// <summary>
    /// Generate upload payload lengths (in bytes) for the upload test.
    /// </summary>
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

    /// <summary>
    /// The latency probes of one transfer: small requests to the server's latency endpoint, sent
    /// while a download or an upload runs, whose round trips are what latency under load means.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Probes never overlap — the next is due an interval after the last one finished, so a slow
    /// probe delays its successor rather than stacking up behind it — and none has a time limit of
    /// its own beyond the HTTP client's default, because a probe that takes a long time under load
    /// is the measurement rather than a fault.
    /// </para>
    /// <para>
    /// They travel on the same <see cref="HttpClient"/> as the transfer, which does not put them
    /// behind the transfer's data: the handler leaves <c>MaxConnectionsPerServer</c> unbounded, and
    /// HTTP/1.1 does not multiplex, so a probe issued while a transfer holds a connection opens one
    /// of its own instead of queueing. A second client would add nothing but a second set of proxy
    /// settings to keep in step.
    /// </para>
    /// <para>
    /// A probe still waiting when the transfer ends is discarded rather than waited for, so every
    /// figure comes from a probe that answered: the cost is that a line too congested for any probe
    /// to answer within the transfer reports nothing measured, which a diagnostic log shows as the
    /// abandoned probe. Discarding and answering are decided under one lock, so exactly one report
    /// is made for each probe.
    /// </para>
    /// </remarks>
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

        // The running figures, published as each sample is taken so that a transfer reporting its
        // own progress can read them without taking the gate. Taking it would make a consumer's
        // diagnostics handler - which runs under the gate - able to delay the transfer's progress
        // reports. The sample count carries presence, because a nullable long cannot be read
        // atomically: zero means nothing to report, one means a latency but no unevenness yet.
        private long runningLatencyMilliseconds;
        private long runningJitterMilliseconds;
        private int runningSampleCount;

        /// <summary>One probe in flight: what it will be reported as, and when it left.</summary>
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

        /// <summary>
        /// Starts probing, or returns <see langword="null"/> where the measurement is switched off —
        /// which is what makes "no probing object" mean "nothing is being probed", so no caller has
        /// to re-test the setting.
        /// </summary>
        public static LoadedLatencyProbing? Start(SpeedTestProviderBase owner, IServer server, string probeUrl, IProgress<RequestDiagnostic>? diagnostics, CancellationToken callerToken)
        {
            if (!owner.settings.LatencyTest.LoadedLatencyEnabled)
            {
                return null;
            }

            var probing = new LoadedLatencyProbing(owner, server, probeUrl, diagnostics, callerToken);

            // Run off the caller's thread: a delay provider and an HTTP handler that both complete
            // at once (DelayProviderStub with an in-memory handler) would otherwise never yield, so
            // Start would never return and the transfer would never begin.
            probing.loop = Task.Run(probing.RunAsync);
            return probing;
        }

        /// <summary>
        /// The figures of the probes that have answered so far, for a transfer to carry on its own
        /// progress reports. Both absent until the first probe answers, and the unevenness until
        /// the second.
        /// </summary>
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

        /// <summary>
        /// Stops probing and returns what the probes that answered measured, or
        /// <see langword="null"/> where none did. Idempotent, so a transfer that ended by throwing
        /// still abandons its probes through <see cref="Dispose"/>.
        /// </summary>
        public LatencyTestResult? Stop()
        {
            // The figures are settled before anything is cancelled, so a second call returns under
            // the lock without touching the token source - which by then may already have been
            // disposed by the loop's own completion below.
            lock (gate)
            {
                if (stopped)
                {
                    return measured;
                }

                stopped = true;

                if (outstanding is { } abandoned)
                {
                    // Reported before the transfer returns, under the lock its own answer would
                    // take, so the probe is accounted for exactly once.
                    var reason = callerToken.IsCancellationRequested ? CancelledByCaller : TransferEnded;
                    Report(abandoned, RequestOutcome.Cancelled, response: null, owner.ElapsedMillisecondsSince(abandoned.StartTimestamp), reason, failureType: null);
                    outstanding = null;
                }

                measured = samples.Count == 0 ? null : MeasuredLatency(server, samples);
            }

            // Ends the wait before the next probe, and abandons any request already out. After the
            // figures above, so the result does not depend on how quickly either of those unwinds.
            stop.Cancel();

            // Nothing waits on the loop - the result is not held up by a probe - but its faults are
            // observed, so one cannot vanish as an unobserved task exception, and the token source
            // is disposed only once no probe can still be reading its token. Attached after the
            // cancellation above, so a loop that has already finished cannot dispose the source
            // before it is cancelled.
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
                    // no queue at the instant it starts, so a probe sent then would report the idle
                    // line and flatter the figure.
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
                    // Whatever the server returns is treated as an answer: by the time a transfer
                    // is running, the endpoint has already answered correctly for the latency
                    // measurement, and rejecting a sample on its body would discard a round trip
                    // over something the probe is not measuring.
                    answer = await owner.httpClient.GetStringAsync(probeUrl, stop.Token).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    failure = e;
                }

                // Read once, after the outcome is captured. A caller-supplied clock that faults
                // here (one stepping backwards) ends probing for the rest of the transfer;
                // TimeProvider.System never does.
                if (!Complete(probe, owner.ElapsedMillisecondsSince(probe.StartTimestamp), answer, failure))
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Takes one probe's outcome, or reports that the probe had already been abandoned - in
        /// which case the loop is over and the caller should stop.
        /// </summary>
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
                    // report carries this sample however long a consumer's handler takes. The
                    // count is written last, because it is what makes the two figures readable.
                    Volatile.Write(ref runningLatencyMilliseconds, (long)samples.Average());
                    Volatile.Write(ref runningJitterMilliseconds, CalculateJitterMilliseconds(samples));
                    Volatile.Write(ref runningSampleCount, samples.Count);

                    Report(probe, RequestOutcome.Succeeded, answer, elapsedMilliseconds, failureReason: null, failureType: null);
                }
                else
                {
                    // Recorded and skipped: a probe that could not be taken is not a sample, and it
                    // is not a verdict on the transfer it was measuring either, which carries on.
                    Report(probe, RequestOutcome.Failed, response: null, elapsedMilliseconds, DescribeFailure(failure), failure.GetType().Name);
                }

                return true;
            }
        }

        /// <summary>
        /// Reports one probe. The response is passed rather than its byte count so that nothing is
        /// computed for a caller that asked for no diagnostics.
        /// </summary>
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
