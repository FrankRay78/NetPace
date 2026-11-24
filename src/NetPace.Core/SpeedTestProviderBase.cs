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
/// Everything a measurement needs is here: the latency probe loop and its jitter, server
/// screening, the parallel transfer with its total-byte budget, latency under load, progress and
/// per-request diagnostic reporting, and the time and delay provider seams. A derived provider
/// supplies only what differs between kinds of server: the latency probe address and what counts
/// as a valid answer there, the download requests, the address uploads go to, and server discovery.
/// <para>
/// Derived providers live in <c>NetPace.Core</c>: the only constructor is <c>internal</c> and
/// every override point is <c>private protected</c>, so a consumer can accept and use this type
/// but not extend it. See CIR
/// <c>docs/change-intent-records/2026-11-10-one-measurement-two-providers.md</c>.
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

    /// <summary>
    /// Paces reports sent from inside a running request. Not a setting: it is a refresh rate, and
    /// bounds no measurement.
    /// </summary>
    private const int LiveFigureIntervalMilliseconds = 250;

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
    /// The address the transfer is actually using, not the server's listed one, which an upload's
    /// redirect may differ from: probing the listed one would time an idle line.
    /// </remarks>
    /// <param name="server">The server being measured.</param>
    /// <param name="transferUrl">The address the transfer's requests are going to.</param>
    private protected virtual string LoadedLatencyProbeUrl(IServer server, string transferUrl) => LatencyProbeUrl(transferUrl);

    /// <summary>
    /// Whether <paramref name="response"/> is the answer a speed test server of this kind gives to
    /// a latency probe. A non-success status never reaches here; it has already failed the request,
    /// as <see cref="LatencyStatusFailureMessage"/> describes.
    /// </summary>
    private protected abstract bool IsLatencyResponseValid(string response);

    /// <summary>
    /// What to tell the caller when the latency probe at <paramref name="probeUrl"/> is answered
    /// with the non-success <paramref name="status"/>, or <see langword="null"/> to report the
    /// HTTP failure as it came.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> by default. A provider whose server the user names gives a message
    /// here, so a wrong address is reported with what to check.
    /// </remarks>
    private protected virtual string? LatencyStatusFailureMessage(string probeUrl, HttpStatusCode status) => null;

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

        // Zero would report a configuration error as a server that never answered, and -1
        // (Timeout.Infinite) would let one silent server hang the measurement.
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.LatencyTest.HttpTimeoutMilliseconds, nameof(settings.LatencyTest.HttpTimeoutMilliseconds));

        var latencyUrl = LatencyProbeUrl(server.Url);
        var pings = new List<long>();

        var maxIterations = settings.LatencyTest.LatencyTestIterations;
        var intervalMilliseconds = settings.LatencyTest.LatencyTestIntervalMilliseconds;
        var httpTimeoutMilliseconds = settings.LatencyTest.HttpTimeoutMilliseconds;

        // Linked so an outstanding probe is abandoned promptly rather than waited out at its own
        // timeout. Which token fired is read from the tokens below, not from the exception.
        using var probingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, stopAndReportToken);
        var probingToken = probingCts.Token;

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Asked to stop between probes: what has answered so far is the measurement, so the
            // remaining probes are simply not sent.
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
                    // Asked to stop during the pause: the same stop as the one between probes above.
                    break;
                }
            }

            // Stamped before the request leaves, so a record says when it started rather than
            // when the consumer received it.
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
                // Frozen before the recording below, so the figure excludes the triage work.
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

            // Running figures are derived the same way the result's are, so the last report lands
            // on the figures the result states.
            var percentageComplete = (iteration + 1) * 100 / maxIterations;
            ReportProgress(progress, new LatencyTestProgress
            {
                PercentageComplete = percentageComplete,
                LatencyMilliseconds = (long)pings.Average(),
                JitterMilliseconds = RunningJitterMilliseconds(pings),
            });
        }

        // Nothing answered, so the stop surfaces as the cancellation it is: a figure from no probes
        // would be about nothing, and zero would read as a perfect line.
        if (pings.Count == 0)
        {
            stopAndReportToken.ThrowIfCancellationRequested();
        }

        return MeasuredLatency(server, pings);
    }

    /// <summary>
    /// The latency figures of one run of probe times. Derived in one place, so an idle measurement
    /// and one taken under load state the same figures of the same samples.
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
    /// The mean absolute change between consecutive probes, in whole milliseconds; zero below two
    /// probes. NetPace's one definition of jitter — see CIR
    /// <c>docs/change-intent-records/2026-11-01-jitter-is-the-mean-change-between-consecutive-probes.md</c>.
    /// </summary>
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
    /// <see cref="CalculateJitterMilliseconds"/> for a measurement still running, or
    /// <see langword="null"/> below two probes — a zero there would read as perfectly even round
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
        // consumer callback that blocks would otherwise hold up the stage decision.
        var ranked = 0;

        // One counter for the whole pass: the consumer brackets the pass in a single diagnostic
        // scope, so numbering each candidate from one would reissue the same sequence.
        var requestsIssued = 0;

        // One ceiling for the whole pass, not a budget per candidate - see ServerTimeoutMilliseconds.
        var ceilingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        async Task ScreenAndRecordAsync(int index)
        {
            var result = await ScreenServerAsync(servers[index], diagnostics, () => Interlocked.Increment(ref requestsIssued), callerToken: cancellationToken, ceilingToken: ceilingCts.Token).ConfigureAwait(false);

            // One lock, so a consumer callback is never entered concurrently. Nothing is reported
            // once the pass is over: a candidate abandoned at the ceiling can still finish.
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

        // Only a pass that has ranked nothing waits out the second stage — see CIR
        // docs/change-intent-records/2026-11-09-screening-ceiling-is-spent-in-stages.md.
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

        // Outstanding candidates are abandoned, not awaited: awaiting one would make the
        // wall-clock ceiling advisory rather than hard.
        ceilingCts.Cancel();

        // Observe whatever an abandoned candidate throws, so a fault cannot vanish as an
        // unobserved task exception, and dispose only once nothing can still read the token.
        _ = allScreenings.ContinueWith(
            completed =>
            {
                _ = completed.Exception;
                ceilingCts.Dispose();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        // A fault is a defect in NetPace rather than a verdict on a server, so it surfaces rather
        // than reading as one more unreachable candidate.
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

    /// <summary>
    /// Screens one server, returning <see langword="null"/> when the candidate cannot be ranked.
    /// Deliberately far cheaper than the measurement taken of the winner afterwards, and reports
    /// the fastest request rather than the average because screening has no warm-up.
    /// </summary>
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
                // Only the tokens separate a candidate abandoned at the ceiling from an unreachable
                // one; HttpClient.Timeout and the linked ceiling raise the same type. Frozen first.
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

                // Ranked out rather than raised, because finding that out is what screening is for.
                // Any other exception type is left to propagate by the filter above.
                break;
            }
        }

        // Only the fastest request survives, so there is no run of probe times to derive jitter or
        // the extremes from: zero and null respectively, not measurements.
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
    /// the downloads that completed successfully cross
    /// <see cref="DownloadTestSettings.DownloadSizeMb"/>. In-flight downloads are then cancelled
    /// rather than awaited, and what they had already received still counts towards the reported
    /// figure, so <see cref="SpeedTestResult.BytesProcessed"/> may exceed the cap.
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
            // Streamed rather than read as a string: a download is tens of megabytes.
            using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            var buffer = ArrayPool<byte>.Shared.Rent(81920);
            try
            {
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    // Reported per chunk, not totalled: a cut-off or budget-ended request leaves by
                    // the exception path, taking any running total with it.
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
    /// uploads that completed successfully cross <see cref="UploadTestSettings.UploadSizeMb"/>.
    /// In-flight uploads are then cancelled rather than awaited, and what they had already sent
    /// still counts towards the reported figure, so
    /// <see cref="SpeedTestResult.BytesProcessed"/> may exceed the cap.
    /// </remarks>
    public async Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default, CancellationToken stopAndReportToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentException.ThrowIfNullOrWhiteSpace(server.Url);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.UploadTest.UploadSizeMb, nameof(settings.UploadTest.UploadSizeMb));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.LatencyTest.LoadedLatencyIntervalMilliseconds, nameof(settings.LatencyTest.LoadedLatencyIntervalMilliseconds));

        // Lengths, not buffers: the bodies are generated as they are streamed.
        var testDataLengths = GenerateUploadDataLengths(settings.UploadTest.UploadIncrements, settings.UploadTest.UploadSizeIncrementKb, settings.UploadTest.UploadSizeIterations);

        // Any redirect in front of the upload address is resolved first, because a streaming body
        // cannot survive one; the requests that takes are numbered before the measured uploads.
        string uploadUrl;
        int probeRequests;
        try
        {
            (uploadUrl, probeRequests) = await ResolveUploadUrlAsync(UploadUrl(server.Url), diagnostics, cancellationToken, stopAndReportToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stopAndReportToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // A stop before any upload was sent reports what a test stopped before it began reports.
            return new SpeedTestResult { BytesProcessed = 0, ElapsedMilliseconds = 0, RequestsSucceeded = 0, RequestsFailed = 0, Latency = null };
        }

        Func<HttpClient, int, Action<int>, CancellationToken, Task> UploadAndMeasureAsync = async (client, length, recordBytesDelivered, cancellationToken) =>
        {
            // Bytes written to the transport are a ceiling, not a measurement: CIR
            // docs/change-intent-records/2026-11-15-a-figure-counts-what-the-link-delivered.md.
            using var content = new RandomStreamContent(length, recordBytesDelivered);
            using var response = await client.PostAsync(uploadUrl, content, cancellationToken).ConfigureAwait(false);

            // A rejected upload is a failed request, not throughput.
            response.EnsureSuccessStatusCode();

            // A handler that never serialised the body reported no chunks, so an accepted upload is
            // counted at its asked-for length. A partial body keeps only what it wrote.
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

    /// <summary>
    /// Resolves the address the server actually wants uploads sent to, following any redirects
    /// with an empty probe POST before the measured uploads begin.
    /// </summary>
    /// <remarks>
    /// A streaming body cannot survive a redirect, so the endpoint is resolved once, with a probe
    /// that carries no body and so stays out of the measurement.
    /// <para>
    /// Best-effort: a transport failure falls back to the original address. Either cancellation
    /// propagates, as does any other exception type.
    /// </para>
    /// </remarks>
    /// <returns>
    /// The address to upload to, and how many requests the probe issued getting there; the
    /// measured uploads are numbered after those. See <see cref="RequestDiagnostic.Sequence"/>.
    /// </returns>
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

                // Nothing else fails this probe, so without recording the status the one request
                // that can misdirect every upload after it would always read ok.
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
                // it ends the hops rather than being recorded as a failed request.
                if (redirected && response.Headers.Location is { } location)
                {
                    if (Uri.TryCreate(new Uri(currentUrl), location, out var redirectTarget))
                    {
                        currentUrl = redirectTarget.ToString();
                        continue;
                    }

                    return (currentUrl, requestsIssued);
                }

                // Where the handler followed redirects itself, the answering endpoint is the final
                // hop rather than where the probe started.
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

    /// <summary>
    /// Determines whether a status code asks the caller to repeat the request elsewhere.
    /// </summary>
    /// <remarks>
    /// Permanent and temporary redirects are alike here, this resolving an address rather than
    /// replaying a transfer: the probe is discarded once its address is known.
    /// </remarks>
    private static bool IsRedirect(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.MovedPermanently or
        HttpStatusCode.Found or
        HttpStatusCode.SeeOther or
        HttpStatusCode.TemporaryRedirect or
        HttpStatusCode.PermanentRedirect;

    /// <summary>
    /// The transfer both download and upload run on: test data processed in parallel, measuring
    /// bytes, elapsed time and, where it is on, the latency under that load.
    /// </summary>
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

        // Delivered bytes are the measurement, returned bytes are what the budget is spent against:
        // CIR docs/change-intent-records/2026-11-15-a-figure-counts-what-the-link-delivered.md.
        long totalBytesDelivered = 0;
        long budgetBytesReturned = 0;

        var completedCount = 0;
        var succeededCount = 0;
        var failedCount = 0;
        var totalCount = testData.Count();

        var throttler = new SemaphoreSlim(parallelTasks);

        // One source ends the transfer however it was ended: by the caller, by a stop-and-report,
        // or by the byte budget. What separates the three is read from the tokens, not the exception.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, stopAndReportToken);

        var testStartTimestamp = timeProvider.GetTimestamp();

        // Started where the transfer starts, not where the operation does: an upload resolves its
        // endpoint first, and a probe sent before any data moved would time an idle line.
        using var probing = LoadedLatencyProbing.Start(this, server, LoadedLatencyProbeUrl(server, transferUrl), diagnostics, cancellationToken);

        // Lets a report from inside a request repeat the last percentage rather than invent one.
        // Read and written only under lockObject, so a repeat cannot carry a replaced value.
        var lastPercentageComplete = 0;

        // The clock reading of the last report sent from inside a request, which paces them.
        var lastLiveReportTimestamp = testStartTimestamp;

        // One place, so the bytes, the clock and the latency figures are always read together and
        // the result cannot contradict what the consumer was last shown. Null repeats the last.
        void ReportTransferProgress(int? percentageComplete = null)
        {
            if (progress is null)
            {
                return;
            }

            // Held so a consumer's IProgress is called one report at a time: two at once would let
            // the figure go backwards. Re-entrant, so callers already holding it are unaffected.
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

        // Paced rather than per chunk, on the measurement's own clock: CIR
        // docs/change-intent-records/2026-11-15-a-figure-counts-what-the-link-delivered.md.
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

            // Whichever request wins the exchange sends the report; the ones that lose it have
            // nothing to add, because the figure is the shared running total rather than theirs.
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

            // Called per chunk rather than returning a total: a request that ends badly never
            // returns, and everything it carried would be thrown away with it.
            void RecordBytesDelivered(int count)
            {
                bytesDelivered += count;
                Interlocked.Add(ref totalBytesDelivered, count);
                ReportLiveFigure();
            }

            // A request still waiting its turn when the test ends was never sent, so it has nothing
            // to report; everything below is only meaningful once it has started.
            var requestStarted = false;
            var sequence = 0;
            var startedAt = default(DateTime);
            var startTimestamp = 0L;
            Exception? failure = null;

            // Captured where the request ends, not in the counting block below: that block waits on
            // a shared lock, so timing it there would bill a request for the ones queued ahead.
            var elapsedMilliseconds = 0L;

            try
            {
                await throttler.WaitAsync(cts.Token).ConfigureAwait(false);

                // SemaphoreSlim.WaitAsync can return a permit after its token was cancelled, so a
                // request the test ended while it queued would otherwise be sent and recorded.
                cts.Token.ThrowIfCancellationRequested();

                sequence = Interlocked.Increment(ref sequenceIssued);
                startedAt = timeProvider.GetLocalNow().DateTime;
                startTimestamp = timeProvider.GetTimestamp();

                // Set last: guarding on it while startTimestamp was still zero would time the
                // request from the clock's own epoch.
                requestStarted = true;

                await doWork(httpClient, data, RecordBytesDelivered, cts.Token).ConfigureAwait(false);
                elapsedMilliseconds = ElapsedMillisecondsSince(startTimestamp);
            }
            catch (Exception e)
            {
                // Captured before the clock is read: ElapsedMillisecondsSince calls a
                // caller-supplied provider, so a clock fault would otherwise read as a success.
                failure = e;

                // wasCancelledLocally is read unlocked, a benign race argued in CIR
                // docs/change-intent-records/2026-11-24-a-transfers-counts-are-gated-not-flagged.md.
                if (!(e is OperationCanceledException && (wasCancelledLocally || cancellationToken.IsCancellationRequested || stopAndReportToken.IsCancellationRequested)))
                {
                    requestFailed = true;
                }

                // Read after the classification, for the clock-fault reason above, and before the
                // rethrow, so a cancelled request's record carries the duration it reached.
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
                        // Read before the counting block that may itself cancel, so the request
                        // tripping the byte cap is counted and only the ones behind it are excluded.
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

                                if (maxBytes != long.MaxValue)
                                {
                                    // Under a byte cap, the request count understates progress once
                                    // the cap will end the transfer early, so take the greater.
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
                            // Excluded from the counts but not the figure: without this the result
                            // could report more than the last figure the user saw.
                            ReportTransferProgress();
                        }

                        if (requestStarted && diagnostics is not null)
                        {
                            RequestOutcome outcome;
                            string? failureReason = null;
                            string? failureType = null;

                            if (requestExcluded)
                            {
                                // Cancelled, to agree with the counts, but a request that failed
                                // before the cap tripped keeps its own cause as the reason.
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
                                    // Read after the byte budget, so where both happen at once a
                                    // request keeps the reason that actually excluded it.
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

        // Read where the transfer ends, not inside the result below, so neither anything added
        // between the two nor the probes' own winding up is billed to it.
        var testElapsedMilliseconds = ElapsedMillisecondsSince(testStartTimestamp);

        var loadedLatency = probing?.Stop();

        return new SpeedTestResult
        {
            BytesProcessed = Interlocked.Read(ref totalBytesDelivered),
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
    /// Reports progress, isolating the measurement from a throwing consumer callback: progress is
    /// best-effort telemetry, so a callback that throws must not fault a running speed test.
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
            // The one legitimate catch-all here: it isolates caller-supplied callback code, and
            // swallows no internal error of NetPace's own.
        }
    }

    /// <summary>
    /// Reports a latency probe that answered correctly, whether it was taking the measurement or
    /// screening a candidate.
    /// </summary>
    /// <remarks>
    /// The response is passed rather than its byte count, so nothing is computed for a caller that
    /// asked for no diagnostics; that is why the guard is in here and not at the call sites.
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
    /// Reports a request that was in flight when the caller asked the measurement to stop and
    /// report what it had: cancelled rather than failed, and excluded from the counts.
    /// </summary>
    /// <remarks>
    /// No exception type is recorded: the cancellation came from the caller's token, so naming
    /// <c>TaskCanceledException</c> would dress a caller's decision up as a fault.
    /// </remarks>
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
    /// The outermost message alone names no cause ("The SSL connection could not be established,
    /// see inner exception."), and the innermost alone loses the context that makes it readable.
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
    /// Every elapsed time is read through here, so a supplied time provider is the clock every
    /// measurement is taken against. Waiting stays on the system clock: CIR
    /// <c>docs/change-intent-records/2026-10-31-elapsed-time-reads-the-supplied-clock.md</c>.
    /// </remarks>
    /// <param name="startTimestamp">A timestamp previously read from this provider's clock.</param>
    /// <exception cref="InvalidOperationException">
    /// The supplied time provider reported a negative duration.
    /// </exception>
    private protected long ElapsedMillisecondsSince(long startTimestamp)
    {
        var elapsed = timeProvider.GetElapsedTime(startTimestamp);

        // Reported rather than clamped: a negative duration divides into a negative speed that
        // reads as a real measurement, and a clamp to zero reads as a sub-millisecond one.
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

    /// <summary>The upload payload lengths, in bytes, that one upload test sends.</summary>
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
    /// Probes never overlap and have no time limit of their own: a slow probe under load is the
    /// measurement, not a fault.
    /// <para>
    /// They share the transfer's <see cref="HttpClient"/>, which does not queue them behind its
    /// data, and one still out when the transfer ends is discarded rather than waited for. See CIR
    /// <c>docs/change-intent-records/2026-11-09-under-load-latency-rides-on-the-transfer-result.md</c>.
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

        // Read by a transfer without taking the gate, where a consumer's handler runs. The count
        // carries presence: a nullable long cannot be read atomically.
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
        /// Starts probing, or returns <see langword="null"/> where the measurement is switched off,
        /// so no caller has to re-test the setting.
        /// </summary>
        public static LoadedLatencyProbing? Start(SpeedTestProviderBase owner, IServer server, string probeUrl, IProgress<RequestDiagnostic>? diagnostics, CancellationToken callerToken)
        {
            if (!owner.settings.LatencyTest.LoadedLatencyEnabled)
            {
                return null;
            }

            var probing = new LoadedLatencyProbing(owner, server, probeUrl, diagnostics, callerToken);

            // Run off the caller's thread: a delay provider and handler that both complete
            // synchronously never yield, so Start would not return and no transfer would begin.
            probing.loop = Task.Run(probing.RunAsync);
            return probing;
        }

        /// <summary>
        /// The figures of the probes that have answered so far, for a transfer's own progress
        /// reports. Both absent until the first probe answers, the jitter until the second.
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
            // Figures settled before anything is cancelled, so a second call returns without
            // touching a token source the loop's completion below may already have disposed.
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

            // After the figures above, so the result does not depend on how quickly this unwinds.
            stop.Cancel();

            // Faults are observed and the source disposed only once no probe can read the token.
            // Attached after the cancellation, so a finished loop cannot dispose it first.
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
                    // Waited before the first probe too: a transfer has built no queue at the
                    // instant it starts, so a probe sent then would time the idle line.
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
                    // Any answer counts: the endpoint already answered correctly for the idle
                    // measurement, so rejecting a body here would discard a real round trip.
                    answer = await owner.httpClient.GetStringAsync(probeUrl, stop.Token).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    failure = e;
                }

                // A caller-supplied clock that faults here ends probing for the rest of the
                // transfer; TimeProvider.System never does.
                if (!Complete(probe, owner.ElapsedMillisecondsSince(probe.StartTimestamp), answer, failure))
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Takes one probe's outcome. <see langword="false"/> means the probe had already been
        /// abandoned, so the loop is over.
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

                    // Published before the report below, so a transfer's next progress report
                    // carries this sample. The count is written last: it is what makes them read.
                    Volatile.Write(ref runningLatencyMilliseconds, (long)samples.Average());
                    Volatile.Write(ref runningJitterMilliseconds, CalculateJitterMilliseconds(samples));
                    Volatile.Write(ref runningSampleCount, samples.Count);

                    Report(probe, RequestOutcome.Succeeded, answer, elapsedMilliseconds, failureReason: null, failureType: null);
                }
                else
                {
                    // Recorded and skipped: not a sample, and not a verdict on the transfer either.
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
