using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using NetPace.Core.Clients.Ookla.Extensions;

namespace NetPace.Core.Clients.Ookla;

/// <summary>
/// An Ookla Speedtest implementation of the <see cref="ISpeedTestService"/> interface.
/// </summary>
public sealed class OoklaSpeedtest : ISpeedTestService
{
    private const string LatencyFileName = "latency.txt";
    private const string LatencyResponsePrefix = "test=test";

    // Reasons a request was abandoned rather than failing on its own merits.
    private const string ByteBudgetReached = "byte budget reached";
    private const string ScreeningCeilingReached = "screening ceiling reached";
    private const string CancelledByCaller = "cancelled by caller";

    private readonly HttpClient httpClient;
    private readonly OoklaSpeedtestSettings settings;
    private readonly IDelayProvider delayProvider;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Constructs a new instance of the <see cref="OoklaSpeedtest"/> class.
    /// </summary>
    /// <param name="speedtestSettings">Per-request sizing, iteration and parallelism settings; the defaults are used when omitted.</param>
    /// <param name="httpClientOverride">An HTTP client to use in place of the one this class would construct.</param>
    /// <param name="delayProviderOverride">A delay provider to use in place of real waiting.</param>
    /// <param name="timeProviderOverride">
    /// The clock that stamps <see cref="RequestDiagnostic.StartedAt"/> on each reported request;
    /// the system clock is used when omitted.
    /// </param>
    public OoklaSpeedtest(OoklaSpeedtestSettings? speedtestSettings = null, HttpClient? httpClientOverride = null, IDelayProvider? delayProviderOverride = null, TimeProvider? timeProviderOverride = null)
    {
        // Use default settings when none provided
        settings = speedtestSettings ?? new OoklaSpeedtestSettings();

        httpClient = httpClientOverride ?? CreateHttpClient(settings.UseProxy, settings.ProxyAddress, settings.ProxyCredential);
        delayProvider = delayProviderOverride ?? new DelayProvider();
        timeProvider = timeProviderOverride ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async Task<IServer[]> GetServersAsync(CancellationToken cancellationToken = default)
    {
        var serversXml = await httpClient.GetStringAsync(settings.ServerDiscovery.ServersUrl, cancellationToken).ConfigureAwait(false);
        var servers = serversXml.DeserializeFromXml()?.Servers ?? Array.Empty<OoklaServer>();
        return servers.Where(s =>
                !string.IsNullOrWhiteSpace(s.Location) &&
                !string.IsNullOrWhiteSpace(s.Sponsor) &&
                !string.IsNullOrWhiteSpace(s.Url)).ToArray();
    }

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

        var server = new Server() { Sponsor = "(Unknown)", Url = serverUrl };
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

        var latencyUrl = GetBaseUrl(server.Url) + LatencyFileName;
        var pings = new List<long>();
        var stopwatch = new Stopwatch();

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
            var sequence = iteration + 1;

            string testString;
            try
            {
                stopwatch.Restart();
                testString = await httpClient.GetStringWithTimeoutAsync(latencyUrl, TimeSpan.FromMilliseconds(httpTimeoutMilliseconds), cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();
            }
            catch (Exception e)
            {
                // The failure still surfaces to the caller - a latency measurement that cannot be
                // taken is not a measurement. It is recorded first, so a diagnostic log names the
                // cause of a run that ended here.
                stopwatch.Stop();
                ReportRequestFailure(diagnostics, startedAt, sequence, latencyUrl, stopwatch.ElapsedMilliseconds, e, cancellationToken);
                throw;
            }

            if (!testString.StartsWith(LatencyResponsePrefix))
            {
                var wrongServer = new InvalidOperationException("Server returned incorrect test string for latency.txt");
                ReportRequestFailure(diagnostics, startedAt, sequence, latencyUrl, stopwatch.ElapsedMilliseconds, wrongServer, cancellationToken);
                throw wrongServer;
            }

            if (diagnostics is not null)
            {
                ReportProgress(diagnostics, new RequestDiagnostic
                {
                    StartedAt = startedAt,
                    Sequence = sequence,
                    Url = latencyUrl,
                    Outcome = RequestOutcome.Succeeded,
                    BytesProcessed = Encoding.UTF8.GetByteCount(testString),
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
                });
            }

            // Record this ping time
            pings.Add(stopwatch.ElapsedMilliseconds);

            // Report progress after each iteration
            var percentageComplete = (iteration + 1) * 100 / maxIterations;
            ReportProgress(progress, new LatencyTestProgress
            {
                PercentageComplete = percentageComplete,
            });
        }

        // Calculate the average server latency.
        var latencyResult = new LatencyTestResult
        {
            Server = server,
            LatencyMilliseconds = (long)pings.Average()
        };

        return latencyResult;
    }

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
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.ServerDiscovery.ScreeningRequestCount, nameof(settings.ServerDiscovery.ScreeningRequestCount));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.ServerDiscovery.ServerTimeoutMilliseconds, nameof(settings.ServerDiscovery.ServerTimeoutMilliseconds));

        var screened = new LatencyTestResult?[servers.Length];
        var screeningLock = new object();
        var serversScreened = 0;

        // One ceiling for the whole pass, not a budget per candidate - see ServerTimeoutMilliseconds.
        var ceilingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        async Task ScreenAndRecordAsync(int index)
        {
            var result = await ScreenServerAsync(servers[index], diagnostics, callerToken: cancellationToken, ceilingToken: ceilingCts.Token).ConfigureAwait(false);

            // Recorded and reported under one lock, so a consumer callback is never entered
            // concurrently and the percentage it is handed matches the number screened at that
            // moment. Nothing is reported once the pass is over, because a candidate abandoned at
            // the ceiling can still finish after the caller has moved on.
            lock (screeningLock)
            {
                screened[index] = result;
                serversScreened++;

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

        var allScreenings = Task.WhenAll(screenings);
        var ceiling = Task.Delay(settings.ServerDiscovery.ServerTimeoutMilliseconds, ceilingCts.Token);
        await Task.WhenAny(allScreenings, ceiling).ConfigureAwait(false);

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
            throw new Exception("No servers available");
        }

        return reachable.OrderBy(result => result.LatencyMilliseconds).First();
    }

    /// <summary>
    /// Screens one server - is it reachable, and roughly how fast? - returning <c>null</c> when the
    /// candidate cannot be ranked: its URL is missing or not a web address, no request completed
    /// inside the ceiling, or what answered was not a speed test server. A server that answered
    /// correctly at least once returns a figure and simply ranks lower when it is slow.
    /// </summary>
    /// <remarks>
    /// Deliberately far cheaper than <see cref="GetServerLatencyAsync(IServer, CancellationToken)"/>,
    /// which is the measurement taken of the winner afterwards: a few requests with no deliberate
    /// waiting between them, and a partial answer is kept rather than discarded. The figure is the
    /// fastest request that completed, because screening has no warm-up and the first request
    /// carries connection setup the link itself is not responsible for.
    /// </remarks>
    private async Task<LatencyTestResult?> ScreenServerAsync(IServer server, IProgress<RequestDiagnostic>? diagnostics, CancellationToken callerToken, CancellationToken ceilingToken)
    {
        // The server list comes from a remote feed, so an entry NetPace cannot request is ranked
        // out here rather than left to throw from the transport and fail the whole selection.
        if (!Uri.TryCreate(server.Url, UriKind.Absolute, out var serverUri) ||
            (serverUri.Scheme != Uri.UriSchemeHttp && serverUri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        var latencyUrl = GetBaseUrl(server.Url) + LatencyFileName;
        var stopwatch = new Stopwatch();
        long? fastestMilliseconds = null;

        for (var request = 0; request < settings.ServerDiscovery.ScreeningRequestCount; request++)
        {
            if (ceilingToken.IsCancellationRequested)
            {
                break;
            }

            var startedAt = timeProvider.GetLocalNow().DateTime;
            var sequence = request + 1;

            try
            {
                stopwatch.Restart();
                var testString = await httpClient.GetStringAsync(latencyUrl, ceilingToken).ConfigureAwait(false);
                stopwatch.Stop();

                if (!testString.StartsWith(LatencyResponsePrefix))
                {
                    // Something answered, but it is not a speed test server.
                    if (diagnostics is not null)
                    {
                        ReportProgress(diagnostics, new RequestDiagnostic
                        {
                            StartedAt = startedAt,
                            Sequence = sequence,
                            Url = latencyUrl,
                            Outcome = RequestOutcome.Failed,
                            BytesProcessed = Encoding.UTF8.GetByteCount(testString),
                            ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                            FailureReason = "not a speed test server"
                        });
                    }

                    return null;
                }

                if (diagnostics is not null)
                {
                    ReportProgress(diagnostics, new RequestDiagnostic
                    {
                        StartedAt = startedAt,
                        Sequence = sequence,
                        Url = latencyUrl,
                        Outcome = RequestOutcome.Succeeded,
                        BytesProcessed = Encoding.UTF8.GetByteCount(testString),
                        ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
                    });
                }

                if (fastestMilliseconds is null || stopwatch.ElapsedMilliseconds < fastestMilliseconds)
                {
                    fastestMilliseconds = stopwatch.ElapsedMilliseconds;
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
                stopwatch.Stop();
                var cancelledByCaller = e is OperationCanceledException && callerToken.IsCancellationRequested;
                var abandonedAtCeiling = e is OperationCanceledException && !cancelledByCaller && ceilingToken.IsCancellationRequested;
                var rankedOut = !cancelledByCaller && !abandonedAtCeiling;

                if (diagnostics is not null)
                {
                    ReportProgress(diagnostics, new RequestDiagnostic
                    {
                        StartedAt = startedAt,
                        Sequence = sequence,
                        Url = latencyUrl,
                        Outcome = rankedOut ? RequestOutcome.Failed : RequestOutcome.Cancelled,
                        BytesProcessed = 0,
                        ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                        FailureReason = rankedOut ? DescribeFailure(e)
                            : cancelledByCaller ? CancelledByCaller
                            : ScreeningCeilingReached,
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

        return fastestMilliseconds is null
            ? null
            : new LatencyTestResult { Server = server, LatencyMilliseconds = fastestMilliseconds.Value };
    }

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
    /// In the Ookla implementation, downloads are processed in parallel batches
    /// (configured via <see cref="OoklaSpeedtestSettings.DownloadTest"/>). The total-byte
    /// budget cap is read from <see cref="Settings.DownloadTestSettings.DownloadSizeMb"/>;
    /// once the running total crosses that threshold the internal
    /// <see cref="CancellationTokenSource"/> is cancelled, so in-flight parallel downloads are
    /// cancelled rather than awaited and their bytes excluded. The actual bytes processed may
    /// still exceed the cap depending on parallelism and per-request size.
    /// </remarks>
    public async Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentException.ThrowIfNullOrWhiteSpace(server.Url);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.DownloadTest.DownloadSizeMb, nameof(settings.DownloadTest.DownloadSizeMb));

        var downloadUrls = GenerateDownloadUrls(server.Url, settings.DownloadTest.DownloadSizes, settings.DownloadTest.DownloadSizeIterations);

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
        var downloadResult = await GenericTestSpeedAsync(downloadUrls, DownloadAndMeasureAsync, url => url, progress, diagnostics, settings.DownloadTest.DownloadParallelTasks, maxBytes, sequenceOffset: 0, cancellationToken);

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
    /// In the default Ookla implementation, uploads are processed in parallel batches
    /// (configured via <see cref="OoklaSpeedtestSettings.UploadTest"/>). The total-byte
    /// budget cap is read from <see cref="Settings.UploadTestSettings.UploadSizeMb"/>;
    /// once the running total crosses that threshold the internal
    /// <see cref="CancellationTokenSource"/> is cancelled, so in-flight parallel uploads are
    /// cancelled rather than awaited and their bytes excluded. The actual bytes processed may
    /// still exceed the cap depending on parallelism and per-request size.
    /// </remarks>
    public async Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentException.ThrowIfNullOrWhiteSpace(server.Url);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.UploadTest.UploadSizeMb, nameof(settings.UploadTest.UploadSizeMb));

        // Generate upload sizes (in bytes) rather than allocating large buffers up-front.
        var testDataLengths = GenerateUploadDataLengths(settings.UploadTest.UploadIncrements, settings.UploadTest.UploadSizeIncrementKb, settings.UploadTest.UploadSizeIterations);

        // Ookla is migrating its fleet to HTTPS, and a migrated server answers a plain-HTTP upload
        // POST with a redirect. The streaming body cannot survive that redirect - the server responds
        // and closes the request stream while the body is still being written - so resolve the real
        // endpoint once, cheaply, and upload straight to it.
        // The probe is a request like any other, and takes the first sequence numbers of the upload
        // test; the measured uploads are numbered after it.
        var (uploadUrl, probeRequests) = await ResolveUploadUrlAsync(server.Url, diagnostics, cancellationToken).ConfigureAwait(false);

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
        var uploadResult = await GenericTestSpeedAsync(testDataLengths, UploadAndMeasureAsync, _ => uploadUrl, progress, diagnostics, settings.UploadTest.UploadParallelTasks, maxBytes, sequenceOffset: probeRequests, cancellationToken);

        return uploadResult;
    }

    /// <summary>
    /// Executes a generic speed test by processing a collection of test data in parallel,
    /// measuring total bytes processed and elapsed time.
    /// </summary>
    private async Task<SpeedTestResult> GenericTestSpeedAsync<T>(
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

        var timer = new Stopwatch();
        var throttler = new SemaphoreSlim(parallelTasks);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timer.Start();

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
                startTimestamp = Stopwatch.GetTimestamp();

                // Set last, so the flag never claims a start that the clock read above did not
                // reach. Guarding on it while startTimestamp was still zero would time a request
                // from the Stopwatch epoch and report the machine's uptime as its duration.
                requestStarted = true;

                // Perform the work and retrieve the processed byte count.
                bytesReturned = await doWork(httpClient, data, cts.Token).ConfigureAwait(false);
                elapsedMilliseconds = ElapsedMillisecondsSince(startTimestamp);
            }
            catch (Exception e)
            {
                elapsedMilliseconds = requestStarted ? ElapsedMillisecondsSince(startTimestamp) : 0L;
                failure = e;

                // Genuine user cancellation (the caller's token) must propagate; it is not a
                // per-request failure.
                if (e is OperationCanceledException && cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

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
                if (!(e is OperationCanceledException && wasCancelledLocally))
                {
                    requestFailed = true;
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
                                ReportProgress(progress, new SpeedTestProgress
                                {
                                    PercentageComplete = 100,
                                    BytesProcessed = totalBytesReturned,
                                    ElapsedMilliseconds = timer.ElapsedMilliseconds
                                });
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

                                ReportProgress(progress, new SpeedTestProgress
                                {
                                    PercentageComplete = percentageComplete,
                                    BytesProcessed = totalBytesReturned,
                                    ElapsedMilliseconds = timer.ElapsedMilliseconds
                                });
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
                                failureReason = requestFailed
                                    ? DescribeFailure(failure!)
                                    : cancellationToken.IsCancellationRequested ? CancelledByCaller : ByteBudgetReached;
                                failureType = requestFailed ? failure!.GetType().Name : null;
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
        timer.Stop();

        return new SpeedTestResult
        {
            BytesProcessed = totalBytesReturned,
            ElapsedMilliseconds = timer.ElapsedMilliseconds,
            RequestsSucceeded = succeededCount,
            RequestsFailed = failedCount
        };
    }

    /// <summary>
    /// Resolves the address the server actually wants uploads sent to, following any redirects
    /// with a small probe body before the measured uploads begin.
    /// </summary>
    /// <remarks>
    /// The probe carries no body: a redirect is decided by scheme and host rather than payload, so
    /// an empty POST draws the same answer with nothing to write and nothing to tear down mid-write.
    /// Sending no bytes also keeps the probe out of the measurement entirely. Whether the redirect is
    /// followed by the underlying handler or reported back to us, the endpoint that answered the probe
    /// is the one returned - whether or not it answered with success. A rejection is recorded so a
    /// triager can see it, not acted on. The probe never fails the test: if it cannot complete at
    /// all, the original URL is used and any real fault surfaces through the uploads themselves.
    /// </remarks>
    /// <returns>
    /// The address to upload to, and how many requests the probe issued getting there - the
    /// measured uploads are numbered after those, so every request of an upload test has a distinct
    /// sequence number.
    /// </returns>
    private async Task<(string Url, int RequestsIssued)> ResolveUploadUrlAsync(string url, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken)
    {
        const int maximumHops = 5;

        var currentUrl = url;
        var requestsIssued = 0;

        for (var hop = 0; hop < maximumHops; hop++)
        {
            var probeUrl = currentUrl;
            var startedAt = timeProvider.GetLocalNow().DateTime;
            var startTimestamp = Stopwatch.GetTimestamp();
            requestsIssued++;

            try
            {
                using var probeContent = new RandomStreamContent(0);
                using var response = await httpClient.PostAsync(currentUrl, probeContent, cancellationToken).ConfigureAwait(false);

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
                        Sequence = requestsIssued,
                        Url = probeUrl,
                        Outcome = probeAnswered ? RequestOutcome.Succeeded : RequestOutcome.Failed,
                        BytesProcessed = 0,
                        ElapsedMilliseconds = ElapsedMillisecondsSince(startTimestamp),
                        FailureReason = probeAnswered ? null : $"endpoint probe answered {(int)response.StatusCode}"
                    });
                }

                // The handler did not follow the redirect for us - follow it explicitly.
                if (redirected && response.Headers.Location is { } location)
                {
                    currentUrl = new Uri(new Uri(currentUrl), location).ToString();
                    continue;
                }

                // Otherwise the endpoint that answered is the one to upload to. When the handler
                // followed redirects itself, that is the final hop rather than where we started.
                return (response.RequestMessage?.RequestUri?.ToString() ?? currentUrl, requestsIssued);
            }
            catch (Exception e)
            {
                ReportRequestFailure(diagnostics, startedAt, requestsIssued, probeUrl, ElapsedMillisecondsSince(startTimestamp), e, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                // Probing is best-effort; fall back to the configured URL.
                return (url, requestsIssued);
            }
        }

        return (currentUrl, requestsIssued);
    }

    #region Static Functions

    /// <summary>
    /// Reports progress to the consumer, isolating the operation from a throwing callback.
    /// Progress is best-effort telemetry for the caller's UI; a callback that throws is the
    /// consumer's bug and MUST NOT fault the running speed test, so its exception is swallowed.
    /// </summary>
    private static void ReportProgress<T>(IProgress<T>? progress, T value)
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
    /// Reports a request that did not succeed, distinguishing one the caller cancelled from one
    /// that genuinely failed.
    /// </summary>
    private static void ReportRequestFailure(IProgress<RequestDiagnostic>? diagnostics, DateTime startedAt, int sequence, string url, long elapsedMilliseconds, Exception failure, CancellationToken cancellationToken)
    {
        if (diagnostics is null)
        {
            return;
        }

        var cancelled = failure is OperationCanceledException && cancellationToken.IsCancellationRequested;

        ReportProgress(diagnostics, new RequestDiagnostic
        {
            StartedAt = startedAt,
            Sequence = sequence,
            Url = url,
            Outcome = cancelled ? RequestOutcome.Cancelled : RequestOutcome.Failed,
            BytesProcessed = 0,
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
    /// context that makes it readable. #245 was diagnosed from exactly this chain.
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
    /// Milliseconds elapsed since a <see cref="Stopwatch.GetTimestamp"/> reading.
    /// </summary>
    private static long ElapsedMillisecondsSince(long startTimestamp) =>
        (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

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
    /// Returns the base URL (ending with a trailing slash) by removing
    /// the file name and query parameters from a full URL string.
    /// </summary>
    /// <example>
    /// Input:  "http://example.com/path/speedtest/file.jpg?x=1"
    /// Output: "http://example.com/path/speedtest/"
    /// </example>
    private static string GetBaseUrl(string url)
    {
        var uri = new Uri(url);
        var baseUri = new Uri(uri, ".");
        return baseUri.ToString();
    }

    /// <summary>
    /// Generates numerous download URLs for the speed test.
    /// </summary>
    /// <example>
    /// http://manchester.speedtest.boundlessnetworks.uk:8080/speedtest/random1500x1500.jpg?r=0
    /// http://manchester.speedtest.boundlessnetworks.uk:8080/speedtest/random1500x1500.jpg?r=1
    /// ...
    /// </example>
    private static IEnumerable<string> GenerateDownloadUrls(string serverUrl, int[] downloadSizes, int downloadSizeIterations)
    {
        var downloadUrl = GetBaseUrl(serverUrl) + "random{0}x{0}.jpg?r={1}";

        foreach (var downloadSize in downloadSizes)
        {
            for (var i = 0; i < downloadSizeIterations; i++)
            {
                yield return string.Format(downloadUrl, downloadSize, i);
            }
        }
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

    /// <summary>
    /// HttpContent that streams cryptographically-random bytes on demand in small chunks.
    /// Avoids allocating a single large byte[] and prevents LOH allocations.
    /// </summary>
    private sealed class RandomStreamContent : HttpContent
    {
        private readonly long totalSize;
        private readonly int chunkSize;

        public RandomStreamContent(long totalSize, int chunkSize = 8192)
        {
            this.totalSize = totalSize;
            this.chunkSize = chunkSize > 0 ? chunkSize : 8192;
            Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        }

        protected override bool TryComputeLength(out long length)
        {
            length = totalSize;
            return true;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(chunkSize);
            try
            {
                long remaining = totalSize;
                while (remaining > 0)
                {
                    var toWrite = (int)Math.Min(buffer.Length, remaining);
                    RandomNumberGenerator.Fill(buffer.AsSpan(0, toWrite));
                    await stream.WriteAsync(buffer.AsMemory(0, toWrite)).ConfigureAwait(false);
                    remaining -= toWrite;
                }

                await stream.FlushAsync().ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    #endregion
}
