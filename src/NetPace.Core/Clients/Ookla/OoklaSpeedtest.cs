using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using NetPace.Core.Clients.Ookla.Extensions;

namespace NetPace.Core.Clients.Ookla;

/// <summary>
/// An Ookla Speedtest implementation of the <see cref="ISpeedTestService"/> interface.
/// </summary>
public sealed class OoklaSpeedtest : ISpeedTestService
{
    private readonly HttpClient httpClient;
    private readonly OoklaSpeedtestSettings settings;

    public OoklaSpeedtest(OoklaSpeedtestSettings? speedtestSettings = null, HttpClient? httpClientOverride = null)
    {
        // Use default settings when none provided
        settings = speedtestSettings ?? new OoklaSpeedtestSettings();

        httpClient = httpClientOverride ?? CreateHttpClient(settings.UseProxy, settings.ProxyAddress, settings.ProxyCredential);
    }

    /// <inheritdoc/>
    public async Task<IServer[]> GetServersAsync(CancellationToken cancellationToken = default)
    {
        var serversXml = await httpClient.GetStringAsync(settings.ServerDiscovery.ServersUrl, cancellationToken).ConfigureAwait(false);
        var servers = serversXml.DeserializeFromXml<ServerList>()?.Servers ?? Array.Empty<Server>();
        return servers.Where(s =>
                !string.IsNullOrWhiteSpace(s.Location) &&
                !string.IsNullOrWhiteSpace(s.Sponsor) &&
                !string.IsNullOrWhiteSpace(s.Url)).ToArray();
    }

    /// <inheritdoc/>
    public async Task<ServerLatencyResult> GetServerLatencyAsync(IServer server, CancellationToken cancellationToken = default)
    {
        return await GetServerLatencyAsync(server, httpClient, settings.LatencyTest.DefaultHttpTimeoutMilliseconds, settings.LatencyTest.LatencyTestIterations, cancellationToken);
    }

    private static async Task<ServerLatencyResult> GetServerLatencyAsync(IServer server, HttpClient httpClient, int httpTimeoutMilliseconds, int maxIterations, CancellationToken cancellationToken)
    {
        var latencyUrl = GetBaseUrl(server.Url) + "latency.txt";
        var stopwatch = new Stopwatch();


        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            stopwatch.Start();
            var testString = await httpClient.GetStringWithTimeoutAsync(latencyUrl, TimeSpan.FromMilliseconds(httpTimeoutMilliseconds), cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            if (!testString.StartsWith("test=test"))
            {
                throw new InvalidOperationException("Server returned incorrect test string for latency.txt");
            }
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
        var fastestLatency = settings.LatencyTest.DefaultHttpTimeoutMilliseconds;
        ServerLatencyResult? fastestServer = null;

        foreach (var server in servers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // nb. Bump up the fastest latency/timeout by a slight margin
            var httpTimeoutMilliseconds = fastestLatency == settings.LatencyTest.DefaultHttpTimeoutMilliseconds ? fastestLatency : (int)(fastestLatency * 1.5);

            try
            {
                var latencyResult = await GetServerLatencyAsync(server, httpClient, httpTimeoutMilliseconds, settings.LatencyTest.LatencyTestIterations, cancellationToken);

                if (latencyResult.Latency < fastestLatency)
                {
                    // Reduce the http timeout to the new fastest latency
                    // (ie. do not wait for servers that are slower)
                    fastestLatency = latencyResult.Latency;
                    fastestServer = latencyResult;
                }
            }
            catch
            {
                // A exception was thrown when pinging the server
                // Ignore and continue with the next server
            }
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
        var testData = GenerateUploadData(settings.UploadTest.UploadIncrements, settings.UploadTest.UploadSizeIncrementKb, settings.UploadTest.UploadSizeIterations);

        // Upload content to a specified URL and return the size of the data in bytes.
        // The test data factory returns pooled buffers and the upload handler returns them to the pool after sending.
        Func<HttpClient, Func<(byte[] Buffer, int Length)>, CancellationToken, Task<int>> UploadAndMeasureAsync = async (client, getUploadData, cancellationToken) =>
        {
            var (buffer, length) = getUploadData();

            try
            {
                using var content = new ByteArrayContent(buffer, 0, length);
                await client.PostAsync(server.Url, content, cancellationToken).ConfigureAwait(false);
                return length;
            }
            finally
            {
                // Return the rented buffer to the pool.
                ArrayPool<byte>.Shared.Return(buffer);
            }
        };

        var uploadResult = await GenericTestSpeedAsync(testData, UploadAndMeasureAsync, UpdateProgress, settings.UploadTest.UploadParallelTasks, uploadSizeMb * 1024L * 1024L, cancellationToken);

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

                // Release the semaphore to allow another task to proceed.
                throttler.Release();
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
    /// Lazily generates a sequence of functions that produce random byte arrays simulating upload data.
    /// </summary>
    /// <remarks>
    /// - Each function, when invoked, returns a rented buffer plus the effective length to use.
    /// - Arrays increase in size in increments of <paramref name="baseSizeKb"/>, up to the number of <paramref name="uploadIncrements"/>.
    /// - For each size, <paramref name="repeatsPerSize"/> independent byte arrays are generated.
    /// - Data generation uses cryptographically secure randomness for realistic performance testing.
    /// - The method yields data lazily via <see cref="Func{(byte[],int)}"/>, avoiding unnecessary memory allocation until use.
    /// </remarks>
    private static IEnumerable<Func<(byte[] Buffer, int Length)>> GenerateUploadData(int uploadIncrements, int baseSizeKb, int repeatsPerSize)
    {
        for (var increment = 1; increment <= uploadIncrements; increment++)
        {
            int incrementSize = increment * baseSizeKb * 1024;

            for (var repeat = 0; repeat < repeatsPerSize; repeat++)
            {
                yield return () =>
                {
                    // Rent a buffer at least as large as requested and fill only the requested length.
                    var buffer = ArrayPool<byte>.Shared.Rent(incrementSize);
                    RandomNumberGenerator.Fill(buffer.AsSpan(0, incrementSize));
                    return (buffer, incrementSize);
                };
            }
        }
    }

    #endregion
}