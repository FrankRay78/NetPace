using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using ByteSizeLib;
using Humanizer;
using NetPace.Core;
using Spectre.Console.Extensions;

namespace NetPace.Console.Commands;

public sealed class SpeedTestCommand(IAnsiConsole console, ISpeedTestService speedTestClient, IClock clock, IWaiter waiter, CancellationToken cancellationToken) : CancelableCommand<SpeedTestCommandSettings>(cancellationToken)
{
    protected override async Task<int> ExecuteAsync(CommandContext context, SpeedTestCommandSettings settings, CancellationToken cancellationToken)
    {
        if (settings.Loop)
        {
            // Run continuously.
            var firstLoop = true;
            do
            {
                try
                {
                    // Run the speed test.
                    await PerformSpeedTestAsync(includeCSVHeader: firstLoop, settings, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // User requested cancellation.
                    return 0;
                }
                catch (Exception e)
                {
                    console.Markup($"[red]Error:[/] {e.Message.EscapeMarkup()}\n");
                }
                finally
                {
                    firstLoop = false;
                }

                try
                {
                    // Pause before the next speed test.
                    await waiter.Delay(settings.Delay, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // User requested cancellation.
                    return 0;
                }
            }
            while (true);
        }
        else if (settings.Count > 1)
        {
            // Run multiple times.
            for (int i = 0; i < settings.Count; i++)
            {
                try
                {
                    // Run the speed test.
                    await PerformSpeedTestAsync(includeCSVHeader: (i == 0), settings, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // User requested cancellation.
                    return 0;
                }
                catch (Exception e)
                {
                    console.Markup($"[red]Error:[/] {e.Message.EscapeMarkup()}\n");
                }

                if ((i + 1) < settings.Count)
                {
                    try
                    {
                        // Pause before the next speed test.
                        await waiter.Delay(settings.Delay, cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        // User requested cancellation.
                        return 0;
                    }
                }
            }
        }
        else
        {
            // Run once.
            try
            {
                // Run the speed test.
                await PerformSpeedTestAsync(includeCSVHeader: true, settings, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                // User requested cancellation.
                return 0;
            }
            catch (Exception e)
            {
                console.Markup($"[red]Error:[/] {e.Message.EscapeMarkup()}\n");
            }
        }

        return 0;
    }

    private async Task PerformSpeedTestAsync(bool includeCSVHeader, SpeedTestCommandSettings settings, CancellationToken cancellationToken)
    {
        ServerLatencyResult fastest;

        if (string.IsNullOrEmpty(settings.ServerUrl))
        {
            // Get the fastest speed test server.
            var servers = await speedTestClient.GetServersAsync(cancellationToken);
            fastest = await speedTestClient.GetFastestServerByLatencyAsync(servers, cancellationToken);
        }
        else
        {
            // User specified speed test server.
            var server = new Core.Clients.Ookla.Server() { Sponsor = "(Unknown)", Url = settings.ServerUrl };
            fastest = await speedTestClient.GetServerLatencyAsync(server, cancellationToken);
        }


        if (!settings.CSV && !settings.Json && !settings.JsonPretty && ((settings.Verbosity & (Verbosity.Normal | Verbosity.Debug)) != 0))
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



        var downloadResult = new SpeedTestResult();
        var uploadResult = new SpeedTestResult();

        // Perform speed test
        if (settings.CSV || settings.Json || settings.JsonPretty || ((settings.Verbosity & Verbosity.Minimal) != 0))
        {
            // No progress is reported
            if (!settings.NoDownload) downloadResult = await speedTestClient.GetDownloadSpeedAsync(fastest.Server, settings.DownloadSizeMb, cancellationToken);
            if (!settings.NoUpload) uploadResult = await speedTestClient.GetUploadSpeedAsync(fastest.Server, settings.UploadSizeMb, cancellationToken);
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
                        downloadResult = await speedTestClient.GetDownloadSpeedAsync(fastest.Server, settings.DownloadSizeMb, (SpeedTestProgress progress) =>
                        {
                            downloadProgress!.Value = progress.PercentageComplete;
                        }, cancellationToken);
                    }
                    if (!settings.NoUpload)
                    {
                        uploadResult = await speedTestClient.GetUploadSpeedAsync(fastest.Server, settings.UploadSizeMb, (SpeedTestProgress progress) =>
                        {
                            uploadProgress!.Value = progress.PercentageComplete;
                        }, cancellationToken);
                    }
                });
        }



        // CSV output overrides the display options below
        if (settings.CSV)
        {
            // Always including the timestamp in the CSV output seems reasonable
            settings.IncludeTimestamp = true;

            if (settings.CSVHeaderUnits)
            {
                var downloadFormattedParts = downloadResult.GetSpeedStringParts(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale);
                var uploadFormattedParts = uploadResult.GetSpeedStringParts(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale);

                // Header row.
                if (includeCSVHeader)
                {
                    console.WriteLine(string.Join(settings.CSVDelimiter, new[]
                    {
                        settings.IncludeTimestamp ? "Timestamp" : null,
                        "Latency (ms)",
                        !settings.NoDownload ? $"Download ({downloadFormattedParts.unit})" : null,
                        !settings.NoUpload ? $"Upload ({uploadFormattedParts.unit})" : null
                    }.Where(s => !string.IsNullOrEmpty(s))));
                }

                // Data row.
                console.WriteLine(string.Join(settings.CSVDelimiter, new[]
                {
                    settings.IncludeTimestamp ? clock.Now.ToString(settings.DateTimeFormat) : null,
                    $"{fastest.Latency}",
                    !settings.NoDownload ? downloadFormattedParts.speed : null,
                    !settings.NoUpload ? uploadFormattedParts.speed : null
                }.Where(s => !string.IsNullOrEmpty(s))));
            }
            else
            {
                // Header row.
                if (includeCSVHeader)
                {
                    console.WriteLine(string.Join(settings.CSVDelimiter, new[]
                    {
                        settings.IncludeTimestamp ? "Timestamp" : null,
                        "Latency",
                        !settings.NoDownload ? "Download" : null,
                        !settings.NoUpload ? "Upload" : null
                    }.Where(s => !string.IsNullOrEmpty(s))));
                }

                // Data row.
                console.WriteLine(string.Join(settings.CSVDelimiter, new[]
                {
                    settings.IncludeTimestamp ? clock.Now.ToString(settings.DateTimeFormat) : null,
                    $"{fastest.Latency} ms",
                    !settings.NoDownload ? downloadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale) : null,
                    !settings.NoUpload ? uploadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale) : null
                }.Where(s => !string.IsNullOrEmpty(s))));
            }
        }
        // Json output overrides the display options below
        else if (settings.Json || settings.JsonPretty)
        {
            var downloadFormatted = !settings.NoDownload ? downloadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale) : null;
            var uploadFormatted = !settings.NoUpload ? uploadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale) : null;

            var jsonResult = new JsonResult
            {
                ServerLocation = fastest.Server.Location,
                ServerSponsor = fastest.Server.Sponsor,
                ServerUrl = fastest.Server.Url,
                Timestamp = clock.Now.ToString(settings.DateTimeFormat),
                Latency = $"{fastest.Latency} ms",
                DownloadSpeed = downloadFormatted!,
                UploadSpeed = uploadFormatted!
            };

            var options = new JsonSerializerOptions { WriteIndented = settings.JsonPretty, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
            string jsonString = JsonSerializer.Serialize(jsonResult, options);

            console.WriteLine(jsonString);
        }
        else
        {
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
                !settings.NoDownload ? $"Download: {downloadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale)}" : null,
                !settings.NoUpload ? $"Upload: {uploadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale)}" : null
            }.Where(s => !string.IsNullOrEmpty(s))));


            if ((settings.Verbosity & (Verbosity.Normal | Verbosity.Debug)) != 0)
            {
                console.WriteLine("\nTry 'NetPace --help' for more information.");
            }
        }
    }
}