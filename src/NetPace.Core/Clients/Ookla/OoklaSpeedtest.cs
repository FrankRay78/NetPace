using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
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

        // Calculate the average server latency
        var latency = (int)stopwatch.ElapsedMilliseconds / maxIterations;


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
    public async Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        var downloadUrls = GenerateDownloadUrls(server.Url, settings.DownloadTest.DownloadSizes, settings.DownloadTest.DownloadSizeIterations);

        // Download content from a specified URL and return the size of the data in bytes.
        Func<HttpClient, string, CancellationToken, Task<int>> DownloadAndMeasureAsync = async (client, downloadUrl, cancellationToken) =>
        {
            var data = await client.GetStringAsync(downloadUrl, cancellationToken).ConfigureAwait(false);
            return data.Length;
        };

        var downloadResult = await GenericTestSpeedAsync(downloadUrls, DownloadAndMeasureAsync, UpdateProgress, settings.DownloadTest.DownloadParallelTasks, cancellationToken);

        return downloadResult;
    }

    /// <inheritdoc/>
    public async Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, CancellationToken cancellationToken = default)
    {
        return await GetUploadSpeedAsync(server, (_) => { }, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        var testData = GenerateUploadData(settings.UploadTest.UploadIncrements, settings.UploadTest.UploadSizeIncrementKb, settings.UploadTest.UploadSizeIterations);

        // Upload content to a specified URL and return the size of the data in bytes.
        Func<HttpClient, byte[], CancellationToken, Task<int>> UploadAndMeasureAsync = async (client, uploadData, cancellationToken) =>
        {
            using var content = new ByteArrayContent(uploadData);
            await client.PostAsync(server.Url, content, cancellationToken).ConfigureAwait(false);
            return uploadData.Length;
        };

        var uploadResult = await GenericTestSpeedAsync(testData, UploadAndMeasureAsync, UpdateProgress, settings.UploadTest.UploadParallelTasks, cancellationToken);

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
        CancellationToken cancellationToken)
    {
        object lockObject = new();
        var completedCount = 0;
        var totalCount = testData.Count();

        var timer = new Stopwatch();
        var throttler = new SemaphoreSlim(parallelTasks);

        timer.Start();

        // Create and execute tasks to process the test data in parallel.
        var tasks = testData.Select(async data =>
        {
            // Limit concurrent executions by waiting for a permit from the semaphore.
            await throttler.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var bytesReturned = 0;

                try
                {
                    // Perform the work and retrieve the processed byte count.
                    bytesReturned = await doWork(httpClient, data, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // An exception was thrown when performing the work
                    // - Progress will be reported as if no failure
                    // - Bytes returned will be treated as zero
                }
                finally
                {
                    lock (lockObject)
                    {
                        // Safely update the progress count and report completion percentage.
                        completedCount++;
                        var percentageComplete = (int)((double)completedCount / totalCount * 100);
                        UpdateProgress(new SpeedTestProgress { PercentageComplete = percentageComplete });
                    }
                }

                return bytesReturned;
            }
            finally
            {
                // Release the semaphore to allow another task to proceed.
                throttler.Release();
            }
        }).ToArray();

        // Wait for all tasks to complete.
        await Task.WhenAll(tasks);
        timer.Stop();

        // Compute the total bytes returned.
        long totalBytesReturned = tasks.Sum(task => task.Result);

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
    /// Generates a collection of byte arrays representing simulated upload data.
    /// </summary>
    /// <remarks>
    /// - The method creates a series of byte arrays of increasing size, up to a defined maximum.
    /// - Each byte array is filled with random uppercase A–Z characters.
    /// - Repeated arrays are independent copies to simulate distinct upload chunks.
    /// - The purpose of this method is to simulate varying upload payloads for testing performance.
    /// </remarks>
    private static IEnumerable<byte[]> GenerateUploadData(int uploadIncrements, int baseSizeKb, int repeatsPerSize)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var random = new Random(0); // Fixed seed
        var result = new List<byte[]>();

        for (var increment = 1; increment <= uploadIncrements; increment++)
        {
            var size = increment * baseSizeKb * 1024; // Increasing size in baseSizeKb increments
            var builder = new StringBuilder(size);

            // Fill the StringBuilder with random characters
            for (var i = 0; i < size; ++i)
            {
                builder.Append(chars[random.Next(chars.Length)]);
            }

            var bytes = Encoding.UTF8.GetBytes(builder.ToString());

            // Add repeatsPerSize copies of the generated byte array to the result list
            for (var i = 0; i < repeatsPerSize; i++)
            {
                result.Add((byte[])bytes.Clone()); // Ensure each entry is a distinct instance
            }
        }

        return result;
    }

    #endregion
}