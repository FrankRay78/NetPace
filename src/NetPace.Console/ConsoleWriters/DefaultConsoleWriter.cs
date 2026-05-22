using ByteSizeLib;
using NetPace.Console.ConsoleWriters;
using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

public sealed class DefaultConsoleWriter : IConsoleWriter
{
    public async Task PerformSpeedTestAsync(bool initialSpeedTest, IAnsiConsole console, IClock clock, IClientInfoProvider clientInfoProvider, ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, CancellationToken cancellationToken)
    {
        // Get the server to use for speed testing.
        var fastest = await console.Progress()
            .AutoClear(true)
            .Columns(
            [
                new SpinnerColumn(),
                new TaskDescriptionColumn(),
            ])
            .StartAsync(async progress =>
            {
                var fastestServerProgress = progress.AddTask("Choosing server", autoStart: true, maxValue: 100);

                try
                { 
                    return await ServerSelector.GetServerAsync(speedTestClient, settings, cancellationToken);
                }
                finally
                { 
                    fastestServerProgress.StopTask();
                }
            });


        // Display server latency.
        console.WriteLine("");
        console.WriteLine($"{fastest.Server.Sponsor}", new Style(foreground: Color.Yellow, decoration: Decoration.Bold));
        console.WriteLine($"{fastest.Server.Url}");

        if (!console.Profile.Capabilities.Interactive)
        {
            // Interactive console: Add an extra line given the live widget will not appear.
            console.WriteLine("");
        }


        var downloadResult = new SpeedTestResult();
        var uploadResult = new SpeedTestResult();

        // Perform speed test
        if (!(settings.NoDownload && settings.NoUpload))
        {
            await console.Progress()
                .AutoClear(false)
                .Columns(
                [
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                ])
                .StartAsync(async progress =>
                {
                    ProgressTask? downloadProgress = null; ProgressTask? uploadProgress = null;

                    // Create the graphical progress bars
                    if (!settings.NoDownload)
                    {
                        downloadProgress = progress.AddTask("Downloading", autoStart: true, maxValue: 100);
                    }
                    if (!settings.NoUpload)
                    {
                        uploadProgress = progress.AddTask("Uploading", autoStart: true, maxValue: 100);
                    }

                    // Perform the speed tests and show progress.
                    // Note: SyncProgress applies reports inline; Progress<T> posts them via the
                    // SyncContext/thread pool, which races Spectre's renderer and drops updates.
                    if (!settings.NoDownload)
                    {
                        var downloadProgressReporter = new SyncProgress<SpeedTestProgress>(p => downloadProgress!.Value = p.PercentageComplete);
                        downloadResult = await speedTestClient.GetDownloadSpeedAsync(fastest.Server, downloadProgressReporter, cancellationToken);
                    }
                    if (!settings.NoUpload)
                    {
                        var uploadProgressReporter = new SyncProgress<SpeedTestProgress>(p => uploadProgress!.Value = p.PercentageComplete);
                        uploadResult = await speedTestClient.GetUploadSpeedAsync(fastest.Server, uploadProgressReporter, cancellationToken);
                    }
                });
        }


        if ((settings.Verbosity & Verbosity.Debug) != 0)
        {
            // Display detailed diagnostics
            ByteSize size; TimeSpan elapsed;

            if (!settings.NoDownload)
            {
                size = ByteSize.FromBytes(downloadResult.BytesProcessed);
                elapsed = TimeSpan.FromMilliseconds(downloadResult.ElapsedMilliseconds);
                console.WriteLine($"{size} downloaded in {elapsed.Humanize()}");
            }
            if (!settings.NoUpload)
            {
                size = ByteSize.FromBytes(uploadResult.BytesProcessed);
                elapsed = TimeSpan.FromMilliseconds(uploadResult.ElapsedMilliseconds);
                console.WriteLine($"{size} uploaded in {elapsed.Humanize()}");
            }

            if (!(settings.NoDownload && settings.NoUpload))
            {
                console.WriteLine("");
            }
        }


        if ((settings.NoDownload && settings.NoUpload) && console.Profile.Capabilities.Interactive)
        {
            // Latency only test: Add an extra blank line for formatting.
            console.WriteLine("");
        }


        // Display speed test result.
        console.WriteLine(string.Join(", ", new[]
        {
            settings.IncludeTimestamp ? clock.Now.ToString(settings.DateTimeFormat) : null,
            !settings.NoLatency ? $"Latency: {fastest.LatencyMilliseconds} ms" : null,
            !settings.NoDownload ? $"Download: {downloadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale)}" : null,
            !settings.NoUpload ? $"Upload: {uploadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale)}" : null
        }.Where(s => !string.IsNullOrEmpty(s))));


        console.WriteLine("\nTry 'NetPace --help' for more information.");
    }

    private sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
