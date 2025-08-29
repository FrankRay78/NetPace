using System;
using ByteSizeLib;
using Humanizer;
using NetPace.Core;
using Spectre.Console.Extensions;

namespace NetPace.Console.Commands;

public sealed class SpeedTestCommand(IAnsiConsole console, ISpeedTestService speedTestClient, IClock clock, CancellationToken cancellationToken) : CancelableCommand<SpeedTestCommandSettings>(cancellationToken)
{
    protected override async Task<int> ExecuteAsync(CommandContext context, SpeedTestCommandSettings settings, CancellationToken cancellationToken)
    {
        ServerLatencyResult fastest;

        if (string.IsNullOrEmpty(settings.ServerUrl))
        {
            // Get the fastest speed test server
            var servers = await speedTestClient.GetServersAsync(cancellationToken);
            fastest = await speedTestClient.GetFastestServerByLatencyAsync(servers, cancellationToken);
        }
        else
        {
            var server = new NetPace.Core.Clients.Ookla.Server() { Sponsor = "(Unknown)", Url = settings.ServerUrl };
            fastest = await speedTestClient.GetServerLatencyAsync(server, cancellationToken);
        }


        if (!settings.CSV && ((settings.Verbosity & (Verbosity.Normal | Verbosity.Debug)) != 0))
        {
            console.WriteLine("");
            console.WriteLine($"{fastest.Server.Sponsor}", new Style(foreground: Color.Yellow, decoration: Decoration.Bold));
            console.WriteLine($"{fastest.Server.Url}");

            if (!console.Profile.Capabilities.Interactive)
            {
                // Add an extra line given the live widget will not appear.
                console.WriteLine("");
            }
        }


        // Perform speed test
        var (downloadResult, uploadResult) = await PerformSpeedTestAsync(fastest.Server, settings, cancellationToken);


        // CSV output overrides the display options below
        if (settings.CSV)
        {
            // Always including the timestamp in the CSV output seems reasonable
            settings.IncludeTimestamp = true;

            console.WriteLine(string.Join(settings.CSVDelimiter, new[]
            {
                settings.IncludeTimestamp ? "Timestamp" : null,
                "Latency",
                !settings.NoDownload ? "Download" : null,
                !settings.NoUpload ? "Upload" : null
            }.Where(s => !string.IsNullOrEmpty(s))));

            console.WriteLine(string.Join(settings.CSVDelimiter, new[]
            {
                settings.IncludeTimestamp ? clock.Now.ToString(settings.DateTimeFormat) : null,
                $"{fastest.Latency} ms",
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

            if (!(settings.NoDownload && settings.NoUpload))
            {
                console.WriteLine("");
            }
        }

        if ((settings.NoDownload && settings.NoUpload) && ((settings.Verbosity & (Verbosity.Normal | Verbosity.Debug)) != 0) &&
            console.Profile.Capabilities.Interactive)
        {
            // Latency only test: Add an extra blank line for formatting.
            console.WriteLine("");
        }


        // Display speed test result
        console.WriteLine(string.Join(", ", new[]
        {
            settings.IncludeTimestamp ? clock.Now.ToString(settings.DateTimeFormat) : null,
            $"Latency: {fastest.Latency} ms",
            !settings.NoDownload ? $"Download: {downloadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem)}" : null,
            !settings.NoUpload ? $"Upload: {uploadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem)}" : null
        }.Where(s => !string.IsNullOrEmpty(s))));


        if ((settings.Verbosity & (Verbosity.Normal | Verbosity.Debug)) != 0)
        {
            console.WriteLine("\nTry 'NetPace --help' for more information.");
        }


        return 0;
    }

    private async Task<(SpeedTestResult downloadResult, SpeedTestResult uploadResult)> PerformSpeedTestAsync(IServer server, SpeedTestCommandSettings settings, CancellationToken cancellationToken)
    {
        var downloadResult = new SpeedTestResult();
        var uploadResult = new SpeedTestResult();


        if (settings.NoDownload && settings.NoUpload)
        {
            // Latency only test - so just return
            return (downloadResult, uploadResult);
        }


        if (settings.CSV || ((settings.Verbosity & Verbosity.Minimal) != 0))
        {
            if (!settings.NoDownload) downloadResult = await speedTestClient.GetDownloadSpeedAsync(server, settings.DownloadSizeMb, cancellationToken);
            if (!settings.NoUpload) uploadResult = await speedTestClient.GetUploadSpeedAsync(server, settings.UploadSizeMb, cancellationToken);
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
                        }, cancellationToken);
                    }
                    if (!settings.NoUpload)
                    {
                        uploadResult = await speedTestClient.GetUploadSpeedAsync(server, settings.UploadSizeMb, (SpeedTestProgress progress) =>
                        {
                            uploadProgress!.Value = progress.PercentageComplete;
                        }, cancellationToken);
                    }
                });
        }

        return (downloadResult, uploadResult);
    }
}