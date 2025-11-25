using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using NetPace.Core.Clients.Ookla.Extensions;

namespace NetPace.Core.Clients.Ookla;

/// <summary>
/// An Ookla Speedtest implementation of the <see cref="ISpeedTestService"/> interface.
/// </summary>
public sealed class OoklaSpeedtest : ISpeedTestService
{
    private readonly HttpClient httpClient;
    private readonly OoklaSpeedtestSettings settings;
    private readonly IDelayProvider delayProvider;

    /// <summary>
    /// Constructs a new instance of the <see cref="OoklaSpeedtest"/> class.
    /// </summary>
    public OoklaSpeedtest(OoklaSpeedtestSettings? speedtestSettings = null, HttpClient? httpClientOverride = null, IDelayProvider? delayProviderOverride = null)
    {
        // Use default settings when none provided
        settings = speedtestSettings ?? new OoklaSpeedtestSettings();

        httpClient = httpClientOverride ?? CreateHttpClient(settings.UseProxy, settings.ProxyAddress, settings.ProxyCredential);
        delayProvider = delayProviderOverride ?? new DelayProvider();
    }

    /// <inheritdoc/>
    public async Task<IServer[]> GetServersAsync(CancellationToken cancellationToken = default)
    {
        var serversXml = await httpClient.GetStringAsync(settings.ServerDiscovery.ServersUrl, cancellationToken).ConfigureAwait(false);
        var servers = serversXml.DeserializeFromXml<OoklaServerList>()?.Servers ?? Array.Empty<OoklaServer>();
        return servers.Where(s =>
                !string.IsNullOrWhiteSpace(s.Location) &&
                !string.IsNullOrWhiteSpace(s.Sponsor) &&
                !string.IsNullOrWhiteSpace(s.Url)).ToArray();
    }

    /// <inheritdoc/>
    public async Task<ServerLatencyResult> GetServerLatencyAsync(IServer server, CancellationToken cancellationToken = default)
    {
        return await GetServerLatencyAsync(server, (_) => { }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ServerLatencyResult> GetServerLatencyAsync(IServer server, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentException.ThrowIfNullOrWhiteSpace(server.Url);

        return await GetServerLatencyAsync(server, httpClient, delayProvider, settings.LatencyTest.DefaultHttpTimeoutMilliseconds, settings.LatencyTest.LatencyTestIterations, settings.LatencyTest.LatencyTestIntervalMilliseconds, UpdateProgress, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ServerLatencyResult> GetServerLatencyAsync(string serverUrl, CancellationToken cancellationToken = default)
    {
        return await GetServerLatencyAsync(serverUrl, (_) => { }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ServerLatencyResult> GetServerLatencyAsync(string serverUrl, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverUrl);

        var server = new Server() { Sponsor = "(Unknown)", Url = serverUrl };
        return await GetServerLatencyAsync(server, httpClient, delayProvider, settings.LatencyTest.DefaultHttpTimeoutMilliseconds, settings.LatencyTest.LatencyTestIterations, settings.LatencyTest.LatencyTestIntervalMilliseconds, UpdateProgress, cancellationToken);
    }

    private static async Task<ServerLatencyResult> GetServerLatencyAsync(IServer server, HttpClient httpClient, IDelayProvider delayProvider, int httpTimeoutMilliseconds, int maxIterations, int intervalMilliseconds, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken)
    {
        var latencyUrl = GetBaseUrl(server.Url) + "latency.txt";
        var stopwatch = new Stopwatch();


        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Add delay between iterations (not before first iteration)
            if (iteration > 0 && intervalMilliseconds > 0)
            {
                await delayProvider.DelayAsync(intervalMilliseconds, cancellationToken).ConfigureAwait(false);
            }

            stopwatch.Start();
            var testString = await httpClient.GetStringWithTimeoutAsync(latencyUrl, TimeSpan.FromMilliseconds(httpTimeoutMilliseconds), cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            if (!testString.StartsWith("test=test"))
            {
                throw new InvalidOperationException("Server returned incorrect test string for latency.txt");
            }

            // Report progress after each iteration
            var percentageComplete = (iteration + 1) * 100 / maxIterations;
            UpdateProgress(new SpeedTestProgress { PercentageComplete = percentageComplete });
        }

        // Calculate the average server latency.
        var latencyResult = new ServerLatencyResult
        {
            Server = server,
            Latency = (int)stopwatch.ElapsedMilliseconds / maxIterations
        };

        return latencyResult;
    }

    /// <inheritdoc/>
    public async Task<ServerLatencyResult> GetFastestServerByLatencyAsync(IServer[] servers, CancellationToken cancellationToken = default)
    {
        return await GetFastestServerByLatencyAsync(servers, _ => { }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<ServerLatencyResult> GetFastestServerByLatencyAsync(IServer[] servers, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(servers);
        if (servers.Length == 0)
        {
            throw new ArgumentException("At least one server must be provided.", nameof(servers));
        }

        var fastestLatency = settings.LatencyTest.DefaultHttpTimeoutMilliseconds;
        ServerLatencyResult? fastestServer = null;
        var serversProcessed = 0;

        foreach (var server in servers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // nb. Bump up the fastest latency/timeout by a slight margin
            // Apply a minimum threshold to prevent timeouts from becoming too aggressive
            const int MinimumTimeoutMilliseconds = 100;
            var adaptiveTimeout = (int)(fastestLatency * 1.5);
            var httpTimeoutMilliseconds = fastestLatency == settings.LatencyTest.DefaultHttpTimeoutMilliseconds
                ? fastestLatency
                : Math.Max(adaptiveTimeout, MinimumTimeoutMilliseconds);

            try
            {
                var latencyResult = await GetServerLatencyAsync(server, httpClient, delayProvider, httpTimeoutMilliseconds, settings.LatencyTest.LatencyTestIterations, settings.LatencyTest.LatencyTestIntervalMilliseconds, _ => { }, cancellationToken);

                if (latencyResult.Latency < fastestLatency)
                {
                    // Reduce the http timeout to the new fastest latency
                    // (ie. do not wait for servers that are slower)
                    fastestLatency = latencyResult.Latency;
                    fastestServer = latencyResult;
                }
            }
            catch (Exception e)
            {
                if (e is OperationCanceledException)
                {
                    // Propagate user cancelled exceptions
                    throw;
                }

                // A exception was thrown when pinging the server
                // Ignore and continue with the next server
            }

            // Report progress after each server is tested
            serversProcessed++;
            var percentageComplete = serversProcessed * 100 / servers.Length;
            UpdateProgress(new SpeedTestProgress { PercentageComplete = percentageComplete });
        }

        if (fastestServer == null)
        {
            throw new Exception("No servers available");
        }

        return fastestServer;
    }

    /// <inheritdoc/>
    public async Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, CancellationToken cancellationToken = default)
    {
        return await GetDownloadSpeedAsync(server, (_) => { }, cancellationToken);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// In the Ookla implementation, downloads are processed in parallel batches 
    /// (configured via <see cref="OoklaSpeedtestSettings.DownloadTest"/>). The <paramref name="downloadSizeMb"/> 
    /// parameter triggers cancellation of the internal <see cref="CancellationTokenSource"/> once the threshold 
    /// is reached, but all currently executing parallel download tasks will complete before termination.
    /// The actual bytes processed may significantly exceed the specified limit depending on the number of 
    /// concurrent downloads and their individual sizes.
    /// </remarks>
    public async Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, int downloadSizeMb, CancellationToken cancellationToken = default)
    {
        return await GetDownloadSpeedAsync(server, downloadSizeMb, (_) => { }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        return await GetDownloadSpeedAsync(server, int.MaxValue, UpdateProgress, cancellationToken);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// In the Ookla implementation, downloads are processed in parallel batches
    /// (configured via <see cref="OoklaSpeedtestSettings.DownloadTest"/>). The <paramref name="downloadSizeMb"/>
    /// parameter triggers cancellation of the internal <see cref="CancellationTokenSource"/> once the threshold
    /// is reached, but all currently executing parallel download tasks will complete before termination.
    /// The actual bytes processed may significantly exceed the specified limit depending on the number of
    /// concurrent downloads and their individual sizes.
    /// </remarks>
    public async Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, int downloadSizeMb, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentException.ThrowIfNullOrWhiteSpace(server.Url);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(downloadSizeMb);

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

        var downloadResult = await GenericTestSpeedAsync(downloadUrls, DownloadAndMeasureAsync, UpdateProgress, settings.DownloadTest.DownloadParallelTasks, downloadSizeMb * 1024L * 1024L, cancellationToken);

        return downloadResult;
    }

    /// <inheritdoc/>
    public async Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, CancellationToken cancellationToken = default)
    {
        return await GetUploadSpeedAsync(server, int.MaxValue, (_) => { }, cancellationToken);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// In the default Ookla implementation, uploads are processed in parallel batches
    /// (configured via <see cref="OoklaSpeedtestSettings.UploadTest"/>). The <paramref name="uploadSizeMb"/> 
    /// parameter triggers cancellation of the internal <see cref="CancellationTokenSource"/> once the threshold 
    /// is reached, but all currently executing parallel upload tasks will complete before termination.
    /// The actual bytes processed may significantly exceed the specified limit depending on the number of 
    /// concurrent uploads and their individual sizes.
    /// </remarks>
    public async Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, int uploadSizeMb, CancellationToken cancellationToken = default)
    {
        return await GetUploadSpeedAsync(server, uploadSizeMb, (_) => { }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        return await GetUploadSpeedAsync(server, int.MaxValue, UpdateProgress, cancellationToken);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// In the default Ookla implementation, uploads are processed in parallel batches
    /// (configured via <see cref="OoklaSpeedtestSettings.UploadTest"/>). The <paramref name="uploadSizeMb"/>
    /// parameter triggers cancellation of the internal <see cref="CancellationTokenSource"/> once the threshold
    /// is reached, but all currently executing parallel upload tasks will complete before termination.
    /// The actual bytes processed may significantly exceed the specified limit depending on the number of
    /// concurrent uploads and their individual sizes.
    /// </remarks>
    public async Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, int uploadSizeMb, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentException.ThrowIfNullOrWhiteSpace(server.Url);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(uploadSizeMb);

        // Generate upload sizes (in bytes) rather than allocating large buffers up-front.
        var testDataLengths = GenerateUploadDataLengths(settings.UploadTest.UploadIncrements, settings.UploadTest.UploadSizeIncrementKb, settings.UploadTest.UploadSizeIterations);

        // Upload content to a specified URL and return the size of the data in bytes.
        Func<HttpClient, int, CancellationToken, Task<int>> UploadAndMeasureAsync = async (client, length, cancellationToken) =>
        {
            // Use RandomStreamContent to stream generated random bytes in small chunks to avoid LOH allocations.
            using var content = new RandomStreamContent(length);
            await client.PostAsync(server.Url, content, cancellationToken).ConfigureAwait(false);
            return length;
        };

        var uploadResult = await GenericTestSpeedAsync(testDataLengths, UploadAndMeasureAsync, UpdateProgress, settings.UploadTest.UploadParallelTasks, uploadSizeMb * 1024L * 1024L, cancellationToken);

        return uploadResult;
    }

    /// <summary>
    /// Executes a generic speed test by processing a collection of test data in parallel, 
    /// measuring total bytes processed and elapsed time.
    /// </summary>
    private async Task<SpeedTestResult> GenericTestSpeedAsync<T>(
        IEnumerable<T> testData,
        Func<HttpClient, T, CancellationToken, Task<int>> doWork,
        Action<SpeedTestProgress> UpdateProgress,
        int parallelTasks,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        object lockObject = new();
        bool wasCancelledLocally = false;
        long totalBytesReturned = 0;

        var completedCount = 0;
        var totalCount = testData.Count();

        var timer = new Stopwatch();
        var throttler = new SemaphoreSlim(parallelTasks);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timer.Start();

        // Create and execute tasks to process the test data in parallel.
        var tasks = testData.Select(async data =>
        {
            var bytesReturned = 0;

            try
            {
                // Limit concurrent executions by waiting for a permit from the semaphore.
                await throttler.WaitAsync(cts.Token).ConfigureAwait(false);

                // Perform the work and retrieve the processed byte count.
                bytesReturned = await doWork(httpClient, data, cts.Token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                // An exception was thrown when performing the work
                // - Progress will be reported as if no failure
                // - Bytes returned will be treated as zero

                if (e is OperationCanceledException && !wasCancelledLocally)
                {
                    // Propagate user cancelled exceptions
                    throw;
                }    
            }
            finally
            {
                try
                {
                    lock (lockObject)
                    {
                        if (!cts.IsCancellationRequested)
                        {
                            completedCount++;
                            totalBytesReturned += bytesReturned;

                            if (totalBytesReturned >= maxBytes)
                            {
                                // User specified byte limit is hit.
                                wasCancelledLocally = true;
                                cts.Cancel();
                                UpdateProgress(new SpeedTestProgress { PercentageComplete = 100 });
                            }
                            else
                            {
                                // Update the completion percentage.
                                var percentageComplete = (int)((double)completedCount / totalCount * 100);

                                if (maxBytes != long.MaxValue)
                                {
                                    // When a user specified limit has been imposed on the test,
                                    // we should defer to the greater % complete value.

                                    var percentageCompleteMaxBytes = (int)((double)totalBytesReturned / maxBytes * 100);

                                    if (percentageCompleteMaxBytes > percentageComplete)
                                    {
                                        percentageComplete = percentageCompleteMaxBytes;
                                    }
                                }

                                UpdateProgress(new SpeedTestProgress { PercentageComplete = percentageComplete });
                            }
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
            ElapsedMilliseconds = timer.ElapsedMilliseconds
        };
    }

    #region Static Functions

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