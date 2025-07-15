using ByteSizeLib;
using Humanizer;
using NetPace.Core;

namespace NetPace.Console.Commands;

public sealed class SpeedTestCommand : AsyncCommand<SpeedTestCommandSettings>
{
    private IAnsiConsole console;
    private ISpeedTestService speedTestClient;
    private IClock clock;

    public SpeedTestCommand(IAnsiConsole console, ISpeedTestService speedTestClient, IClock clock)
    {
        this.console = console;
        this.speedTestClient = speedTestClient;
        this.clock = clock;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, SpeedTestCommandSettings settings)
    {
        // Get the speed test server
        var servers = await speedTestClient.GetServersAsync();
        var fastest = await speedTestClient.GetFastestServerByLatencyAsync(servers);

        if (!settings.CSV && ((settings.Verbosity & (Verbosity.Normal | Verbosity.Debug)) != 0))
        {
            console.WriteLine($"{fastest.Server.Sponsor} ({fastest.Latency} ms)");
        }

        if (settings.NoDownload && settings.NoUpload)
        {
            return 0;
        }


        // Perform speed test
        var (downloadResult, uploadResult) = await PerformSpeedTestAsync(fastest.Server, settings);


        // CSV output overrides the display options below
        if (settings.CSV)
        {
            // Always including the timestamp in the CSV output seems reasonable
            settings.IncludeTimestamp = true;

            console.WriteLine(string.Join(settings.CSVDelimiter, new[]
            {
                settings.IncludeTimestamp ? "Timestamp" : null,
                !settings.NoDownload ? "Download" : null,
                !settings.NoUpload ? "Upload" : null
            }.Where(s => !string.IsNullOrEmpty(s))));

            console.WriteLine(string.Join(settings.CSVDelimiter, new[]
            {
                settings.IncludeTimestamp ? clock.Now.ToString(settings.DateTimeFormat) : null,
                !settings.NoDownload ? downloadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem) : null,
                !settings.NoUpload ? uploadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem) : null
            }.Where(s => !string.IsNullOrEmpty(s))));

            return 0;
        }


        if ((settings.Verbosity & Verbosity.Debug) != 0)
        {
            // Display detailed diagnostics
            ByteSize size; TimeSpan elapsed;

            if (!settings.NoDownload)
            {
                size = ByteSize.FromBytes(downloadResult.BytesProcessed);
                elapsed = TimeSpan.FromMilliseconds(downloadResult.ElapsedMilliseconds);
                console.WriteLine($"{size.ToString()} downloaded in {elapsed.Humanize()}");
            }
            if (!settings.NoUpload)
            {
                size = ByteSize.FromBytes(uploadResult.BytesProcessed);
                elapsed = TimeSpan.FromMilliseconds(uploadResult.ElapsedMilliseconds);
                console.WriteLine($"{size.ToString()} uploaded in {elapsed.Humanize()}");
            }
        }


        // Display speed test result
        console.WriteLine(string.Join(" ", new[]
        {
            settings.IncludeTimestamp ? clock.Now.ToString(settings.DateTimeFormat) : null,
            !settings.NoDownload ? $"Download: {downloadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem)}" : null,
            !settings.NoUpload ? $"Upload: {uploadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem)}" : null
        }.Where(s => !string.IsNullOrEmpty(s))));


        if ((settings.Verbosity & (Verbosity.Normal | Verbosity.Debug)) != 0)
        {
            console.WriteLine("\nTry 'NetPace --help' for more information.");
        }


        return 0;
    }

    private async Task<(SpeedTestResult downloadResult, SpeedTestResult uploadResult)> PerformSpeedTestAsync(IServer server, SpeedTestCommandSettings settings)
    {
        var downloadResult = new SpeedTestResult();
        var uploadResult = new SpeedTestResult();

        if (settings.CSV || ((settings.Verbosity & Verbosity.Minimal) != 0))
        {
            if (!settings.NoDownload) downloadResult = await speedTestClient.GetDownloadSpeedAsync(server, settings.DownloadSizeMb);
            if (!settings.NoUpload) uploadResult = await speedTestClient.GetUploadSpeedAsync(server, settings.UploadSizeMb);
        }
        else
        {
            // Graphical progress bar
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

                    // Create the progress bars
                    if (!settings.NoDownload)
                    {
                        downloadProgress = progress.AddTask("Downloading", autoStart: true, maxValue: 100);
                    }
                    if (!settings.NoUpload)
                    {
                        uploadProgress = progress.AddTask("Uploading", autoStart: true, maxValue: 100);
                    }

                    // Perform the speed tests and show progress
                    if (!settings.NoDownload)
                    {
                        downloadResult = await speedTestClient.GetDownloadSpeedAsync(server, settings.DownloadSizeMb, (SpeedTestProgress progress) =>
                        {
                            downloadProgress!.Value = progress.PercentageComplete;
                        });
                    }
                    if (!settings.NoUpload)
                    {
                        uploadResult = await speedTestClient.GetUploadSpeedAsync(server, settings.UploadSizeMb, (SpeedTestProgress progress) =>
                        {
                            uploadProgress!.Value = progress.PercentageComplete;
                        });
                    }
                });
        }

        return (downloadResult, uploadResult);
    }
}