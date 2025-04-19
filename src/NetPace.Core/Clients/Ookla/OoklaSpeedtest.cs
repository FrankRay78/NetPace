using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;

namespace NetPace.Core.Clients.Ookla;

/// <summary>
/// An Ookla Speedtest implementation of the <see cref="ISpeedTestService"/> interface.
/// </summary>
public sealed class OoklaSpeedtest : ISpeedTestService
{
    private OoklaSpeedtestSettings settings { get; set; }

    public OoklaSpeedtest()
    {
        // Use default settings when none provided
        settings = new OoklaSpeedtestSettings();
    }

    public OoklaSpeedtest(OoklaSpeedtestSettings settings)
    {
        this.settings = settings;
    }

    /// <inheritdoc/>
    public async Task<IServer[]> GetServersAsync()
    {
        using var httpClient = GetHttpClient();
        var serversXml = await httpClient.GetStringAsync(settings.ServersUrl);
        var servers = serversXml.DeserializeFromXml<ServerList>()?.Servers ?? Array.Empty<Server>();
        return servers.Where(s =>
                !string.IsNullOrWhiteSpace(s.Location) &&
                !string.IsNullOrWhiteSpace(s.Sponsor) &&
                !string.IsNullOrWhiteSpace(s.Url)).ToArray();
    }

    /// <inheritdoc/>
    public async Task<int?> GetServerLatencyAsync(IServer server)
    {
        return await GetServerLatencyAsync(server, settings.DefaultHttpTimeoutMilliseconds, settings.LatencyTestIterations);
    }

    private async Task<int?> GetServerLatencyAsync(IServer server, int httpTimeoutMilliseconds, int testIterations)
    {
        int? latency = null;

        try
        {
            if (string.IsNullOrWhiteSpace(server.Url))
            {
                throw new NullReferenceException("Server url was null");
            }

            var latencyUrl = GetBaseUrl(server.Url).Append("latency.txt");
            var stopwatch = new Stopwatch();

            using var httpClient = GetHttpClient();
            httpClient.Timeout = TimeSpan.FromMilliseconds(httpTimeoutMilliseconds);


            var iteration = 1;
            do
            {
                stopwatch.Start();
                var testString = await httpClient.GetStringAsync(latencyUrl);
                stopwatch.Stop();

                if (!testString.StartsWith("test=test"))
                {
                    throw new InvalidOperationException("Server returned incorrect test string for latency.txt");
                }

                iteration++;
            }
            while (iteration < testIterations);

            // Calculate the average server latency
            latency = (int)stopwatch.ElapsedMilliseconds / testIterations;
        }
        catch
        {
            // Ignore this server
        }

        return latency;
    }

    /// <inheritdoc/>
    public async Task<(IServer server, int latency)?> GetFastestServerByLatencyAsync(IServer[] servers)
    {
        var fastestLatency = settings.DefaultHttpTimeoutMilliseconds;
        IServer? fastestServer = null;

        foreach (var server in servers)
        {
            // nb. Bump up the fastest latency/timeout by a slight margin
            var httpTimeoutMilliseconds = fastestLatency == settings.DefaultHttpTimeoutMilliseconds ? fastestLatency : (int)(fastestLatency * 1.5);

            var latency = await GetServerLatencyAsync(server, httpTimeoutMilliseconds, settings.LatencyTestIterations);

            if (latency != null && latency < fastestLatency)
            {
                // Reduce the http timeout to the new fastest latency
                // (ie. do not wait for servers that are slower)
                fastestLatency = latency.Value;
                fastestServer = server;
            }
        }

        return fastestServer == null ? null : (fastestServer, fastestLatency);
    }

    /// <inheritdoc/>
    public async Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server)
    {
        return await GetDownloadSpeedAsync(server, (_) => { });
    }

    /// <inheritdoc/>
    public async Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, Action<int> UpdateProgress)
    {
        if (string.IsNullOrWhiteSpace(server.Url))
        {
            throw new NullReferenceException("Server url was null");
        }

        var downloadUrls = GenerateDownloadUrls(server.Url, settings.DownloadSizes, settings.DownloadSizeIterations);

        // Download content from a specified URL and return the size of the data in bytes.
        Func<HttpClient, string, Task<int>> DownloadAndMeasureAsync = async (client, url) =>
        {
            var data = await client.GetStringAsync(url).ConfigureAwait(false);
            return data.Length;
        };

        var downloadResult = await GenericTestSpeedAsync(downloadUrls, DownloadAndMeasureAsync, UpdateProgress, settings.DownloadParallelTasks);

        return downloadResult;
    }

    /// <inheritdoc/>
    public async Task<SpeedTestResult> GetUploadSpeedAsync(IServer server)
    {
        return await GetUploadSpeedAsync(server, (_) => { });
    }

    /// <inheritdoc/>
    public async Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, Action<int> UpdateProgress)
    {
        if (string.IsNullOrWhiteSpace(server.Url))
        {
            throw new NullReferenceException("Server url was null");
        }

        var testData = GenerateUploadData(settings.UploadIncrements);

        // Upload content to a specified URL and return the size of the data in bytes.
        Func<HttpClient, byte[], Task<int>> UploadAndMeasureAsync = async (client, uploadData) =>
        {
            using var content = new ByteArrayContent(uploadData);
            await client.PostAsync(server.Url, content).ConfigureAwait(false);
            return uploadData.Length;
        };

        var uploadResult = await GenericTestSpeedAsync(testData, UploadAndMeasureAsync, UpdateProgress, settings.UploadParallelTasks);

        return uploadResult;
    }

    /// <summary>
    /// Executes a generic speed test by processing a collection of test data in parallel, 
    /// measuring total bytes processed and elapsed time.
    /// </summary>
    private async Task<SpeedTestResult> GenericTestSpeedAsync<T>(
        IEnumerable<T> testData,
        Func<HttpClient, T, Task<int>> doWork,
        Action<int> UpdateProgress,
        int parallelTasks)
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
            await throttler.WaitAsync().ConfigureAwait(false);

            using var httpClient = GetHttpClient();
            try
            {
                // Perform the work and retrieve the processed byte count.
                var size = await doWork(httpClient, data).ConfigureAwait(false);

                // Safely update the progress count and report completion percentage.
                lock (lockObject)
                {
                    completedCount++;
                    var percentageComplete = (int)((double)completedCount / totalCount * 100);
                    UpdateProgress(percentageComplete);
                }

                return size;
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

        // Compute the total bytes processed.
        long totalBytesProcessed = tasks.Sum(task => task.Result);

        return new SpeedTestResult
        {
            BytesProcessed = totalBytesProcessed,
            ElapsedMilliseconds = timer.ElapsedMilliseconds
        };
    }

    #region Static Helper Functions

    /// <summary>
    /// Generates a collection of byte arrays representing simulated upload data.
    /// </summary>
    /// <remarks>
    /// - The method creates a series of byte arrays of increasing size, up to a defined maximum.
    /// - Each byte array is filled with randomly selected characters from a predefined set.
    /// - For each size increment, ten copies of the generated byte array are added to the collection.
    /// - The purpose of this method is to simulate varying upload payloads for testing performance.
    /// </remarks>
    private static IEnumerable<byte[]> GenerateUploadData(int uploadIncrements)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        var random = new Random();
        var result = new List<byte[]>();

        for (var increment = 1; increment < uploadIncrements + 1; increment++)
        {
            var size = increment * 200 * 1024; // Increasing size in 200KB increments
            var builder = new StringBuilder(size);

            // Fill the StringBuilder with random characters
            for (var i = 0; i < size; ++i)
            {
                builder.Append(chars[random.Next(chars.Length)]);
            }

            var bytes = Encoding.UTF8.GetBytes(builder.ToString());

            // Add ten copies of the generated byte array to the result list
            for (var i = 0; i < 10; i++)
            {
                result.Add(bytes);
            }
        }

        return result;
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
        var downloadUrl = GetBaseUrl(serverUrl).Append("random{0}x{0}.jpg?r={1}");

        foreach (var downloadSize in downloadSizes)
        {
            for (var i = 0; i < downloadSizeIterations; i++)
            {
                yield return string.Format(downloadUrl, downloadSize, i);
            }
        }
    }

    private static string GetBaseUrl(string url)
    {
        return new Uri(new Uri(url), ".").OriginalString;
    }

    private static HttpClient GetHttpClient()
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/112.0.0.0 Safari/537.36");
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html, application/xhtml+xml, */*");
        httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
        return httpClient;
    }

    #endregion
}