namespace NetPace.Core.Clients;

/// <summary>
/// Configuration unique to Ookla Speedtest.
/// </summary>
/// <remarks>See https://www.speedtest.net/</remarks>
public sealed class OoklaSpeedtestSettings
{
    public string ServersUrl = "http://www.speedtest.net/speedtest-servers.php";

    // The default timeout for HttpClient is 100 seconds.
    // ref: https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient.timeout?view=net-9.0
    public int DefaultHttpTimeoutMilliseconds = 100000;

    // These are used to generate the url for downloading test files.
    // eg: random1500x1500.jpg
    public int[] DownloadSizes = { 1500, 2000, 3000, 3500, 4000 };
    public int DownloadSizeIterations = 4;

    public int LatencyTestIterations = 4;
    public int DownloadParallelTasks = 8;

    public int UploadIncrements = 6;
    public int UploadParallelTasks = 8;
}