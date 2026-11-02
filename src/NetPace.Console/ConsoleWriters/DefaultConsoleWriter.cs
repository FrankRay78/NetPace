using System.Globalization;
using NetPace.Console.ConsoleWriters;
using NetPace.Console.Diagnostics;
using NetPace.Console.Theming;
using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

/// <summary>
/// The screen a person watches while a speed test runs, drawn in <paramref name="theme"/>.
/// </summary>
/// <remarks>
/// The only writer that takes a theme: the machine-readable writers produce records another program
/// parses, and <c>--minimal</c> is a single compact line that has never been coloured, so none of
/// them has an appearance to choose.
/// </remarks>
/// <param name="theme">The colours and emphasis to draw in.</param>
public sealed class DefaultConsoleWriter(Theme theme) : IConsoleWriter
{
    /// <inheritdoc/>
    public bool MachineReadable => false;

    public async Task<SpeedTestOutcome> PerformSpeedTestAsync(bool initialSpeedTest, IAnsiConsole console, IClock clock, IClientInfoProvider clientInfoProvider, IDiagnosticRecorder recorder, ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, CancellationToken cancellationToken)
    {

        // Get the server to use for speed testing.
        var fastest = await console.Progress()
            .AutoClear(true)
            .Columns(
            [
                new SpinnerColumn { Style = theme.Spinner },
                new TaskDescriptionColumn(),
            ])
            .StartAsync(async progress =>
            {
                var fastestServerProgress = progress.AddTask("Choosing server", autoStart: true, maxValue: 100);

                try
                {
                    return await ServerSelector.GetServerAsync(speedTestClient, settings, recorder, cancellationToken);
                }
                finally
                {
                    fastestServerProgress.StopTask();
                }
            });


        // Display server latency.
        console.WriteLine("");
        console.WriteLine($"{fastest.Server.Sponsor}", theme.ServerName);
        console.WriteLine($"{fastest.Server.Url}", theme.ServerUrl);

        if (!console.Profile.Capabilities.Interactive)
        {
            // Interactive console: Add an extra line given the live widget will not appear.
            console.WriteLine("");
        }


        // A test that did not run is absent, never a zeroed result.
        SpeedTestResult? downloadResult = null;
        SpeedTestResult? uploadResult = null;

        // Perform speed test
        if (!(settings.NoDownload && settings.NoUpload))
        {
            // The live figure beside each bar follows the same unit options as the result line, so
            // the speed the user watches is the speed they are given.
            var liveSpeed = new LiveSpeedColumn(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale);

            await console.Progress()
                .AutoClear(false)
                .Columns(
                [
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn
                    {
                        CompletedStyle = theme.ProgressComplete,
                        FinishedStyle = theme.ProgressFinished,
                        RemainingStyle = theme.ProgressRemaining
                    },
                    new PercentageColumn { Style = theme.Percentage, CompletedStyle = theme.PercentageComplete },
                    liveSpeed,
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
                        var downloadProgressReporter = new SyncProgress<SpeedTestProgress>(p =>
                        {
                            downloadProgress!.Value = p.PercentageComplete;
                            liveSpeed.Report(downloadProgress, p);
                        });
                        using var download = DiagnosticTestScope.For(recorder, DiagnosticTests.Download);
                        downloadResult = await speedTestClient.GetDownloadSpeedAsync(fastest.Server, downloadProgressReporter, download, cancellationToken);
                        liveSpeed.Settle(downloadProgress!, downloadResult);
                    }
                    if (!settings.NoUpload)
                    {
                        var uploadProgressReporter = new SyncProgress<SpeedTestProgress>(p =>
                        {
                            uploadProgress!.Value = p.PercentageComplete;
                            liveSpeed.Report(uploadProgress, p);
                        });
                        using var upload = DiagnosticTestScope.For(recorder, DiagnosticTests.Upload);
                        uploadResult = await speedTestClient.GetUploadSpeedAsync(fastest.Server, uploadProgressReporter, upload, cancellationToken);
                        liveSpeed.Settle(uploadProgress!, uploadResult);
                    }
                });
        }


        if ((settings.NoDownload && settings.NoUpload) && console.Profile.Capabilities.Interactive)
        {
            // Latency only test: Add an extra blank line for formatting.
            console.WriteLine("");
        }


        // Display speed test result. What each figure measures and the figure itself are drawn
        // separately, so a theme can tell the two apart.
        var figures = new List<(string? Label, string Value)>();

        if (settings.IncludeTimestamp)
        {
            figures.Add((null, clock.Now.ToHumanReadable()));
        }

        if (!settings.NoLatency)
        {
            figures.Add(("Latency:", $"{fastest.LatencyMilliseconds.ToString(CultureInfo.InvariantCulture)} ms"));
        }

        if (downloadResult is not null)
        {
            figures.Add(("Download:", $"{downloadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale)}{downloadResult.GetFailureAnnotation()}"));
        }

        if (uploadResult is not null)
        {
            figures.Add(("Upload:", $"{uploadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale)}{uploadResult.GetFailureAnnotation()}"));
        }

        WriteResultLine(console, figures);


        console.WriteLine("\nTry 'netpace --help' for more information.");

        return SpeedTestOutcome.Create(downloadResult, uploadResult);
    }

    /// <summary>
    /// Writes the result line: the figures that were measured, comma separated, each one preceded
    /// by what it measures where it has a name. A test that did not run contributes no figure, so
    /// the line never reports a zero for a measurement nobody asked for.
    /// </summary>
    private void WriteResultLine(IAnsiConsole console, IEnumerable<(string? Label, string Value)> figures)
    {
        var runs = new List<(string, Style)>();

        foreach ((string? label, string value) in figures)
        {
            if (string.IsNullOrEmpty(value)) continue;

            if (runs.Count > 0) runs.Add((", ", Style.Plain));

            if (label is not null)
            {
                runs.Add((label, theme.ResultLabel));
                runs.Add((" ", Style.Plain));
            }

            runs.Add((value, theme.ResultValue));
        }

        console.WriteStyledLine([.. runs]);
    }

    private sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
