using NetPace.Console.Diagnostics;
using NetPace.Console.Theming;
using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

/// <summary>
/// The screen a person watches while a speed test runs, drawn in <paramref name="theme"/>: the
/// server as soon as it is known, then a live table with a row per measurement, each figure
/// arriving beside the test it belongs to. The table is the result, so no result line follows it.
/// </summary>
/// <remarks>
/// <para>
/// This output is for a person at a modern terminal, and wherever it cannot redraw — a pipe, a
/// redirect, a <c>--file</c> target, a terminal Spectre.Console reports as not interactive — it
/// writes the <c>--simple</c> lines instead, so a log never holds control codes and a script never
/// parses a half-drawn table. A run with <c>--file</c> at a terminal therefore has both at once:
/// the table on screen and the simple lines in the file. Each destination is judged on its own.
/// </para>
/// </remarks>
/// <param name="liveDisplayRefresh">
/// Whether the live displays redraw as they go. Switching redrawing off, as the console test host
/// does, leaves a slow run with the same screen as an instant one.
/// </param>
/// <param name="theme">The colours and emphasis to draw in.</param>
public sealed class DefaultConsoleWriter(ILiveDisplayRefresh liveDisplayRefresh, Theme theme) : IConsoleWriter
{
    /// <inheritdoc/>
    public bool MachineReadable => false;

    public async Task<SpeedTestOutcome> PerformSpeedTestAsync(IAnsiConsole console, IClock clock, IClientInfoProvider clientInfoProvider, IDiagnosticRecorder recorder, ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, CancellationToken cancellationToken, CancellationToken timeLimitToken = default)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(settings);

        (IAnsiConsole? animating, IAnsiConsole? plain) = Split(console);

        if (animating is null)
        {
            // Nothing this run writes to can redraw, so default output is the simple lines - which
            // is exactly what that writer already produces, rather than a second rendering of it.
            return await new SimpleConsoleWriter().PerformSpeedTestAsync(plain ?? console, clock, clientInfoProvider, recorder, speedTestClient, settings, cancellationToken, timeLimitToken);
        }

        // Chosen first, so the screen can name the server before the first probe answers. The
        // spinner is skipped where the user named the server and there was nothing to choose.
        var chosen = string.IsNullOrEmpty(settings.ServerUrl)
            ? await ChooseWithSpinnerAsync(animating, speedTestClient, settings, recorder, cancellationToken, timeLimitToken)
            : await ServerSelector.ChooseServerAsync(speedTestClient, settings, recorder, cancellationToken, timeLimitToken);

        WriteHeader(animating, chosen);

        var table = new SpeedTestTable(theme, settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale, showLatency: !settings.NoLatency, animated: liveDisplayRefresh.AutoRefresh);

        var pinging = settings.NoLatency ? null : table.AddRow("Pinging");
        var downloading = settings.NoDownload ? null : table.AddRow("Downloading");
        var uploading = settings.NoUpload ? null : table.AddRow("Uploading");

        LatencyTestResult? latency = null;
        SpeedTestResult? downloadResult = null;
        SpeedTestResult? uploadResult = null;

        await animating.Live(table)
            .AutoClear(false)
            .StartAsync(async context =>
            {
                void Redraw()
                {
                    if (liveDisplayRefresh.AutoRefresh)
                    {
                        context.Refresh();
                    }
                }

                // Draw the rows before anything is measured: the display shows nothing until it is
                // refreshed, and the first latency report arrives only once a probe has answered,
                // so on a slow line the screen would otherwise name the server and then sit blank.
                Redraw();

                try
                {
                    latency = await ServerSelector.MeasureLatencyAsync(speedTestClient, chosen, settings, recorder, LatencyReports(pinging, Redraw), cancellationToken, timeLimitToken);

                    if (pinging is not null)
                    {
                        Settle(table, pinging, latency);
                        Redraw();
                    }

                    // Each test asks whether the run still has time, because the test before it
                    // may have used the last of it. A test that never ran loses its row rather
                    // than keeping a spinner or gaining a cross: it did not fail.
                    if (downloading is not null)
                    {
                        if (timeLimitToken.IsCancellationRequested)
                        {
                            table.Remove(downloading);
                        }
                        else
                        {
                            using var download = DiagnosticTestScope.For(recorder, DiagnosticTests.Download);
                            downloadResult = Measurements.Measured(await speedTestClient.GetDownloadSpeedAsync(chosen.Server, TransferReports(downloading, Redraw), download, cancellationToken, timeLimitToken), timeLimitToken);

                            if (downloadResult is null)
                            {
                                table.Remove(downloading);
                            }
                            else
                            {
                                downloading.Settle(downloadResult);
                            }
                        }

                        Redraw();
                    }

                    if (uploading is not null)
                    {
                        if (timeLimitToken.IsCancellationRequested)
                        {
                            table.Remove(uploading);
                        }
                        else
                        {
                            using var upload = DiagnosticTestScope.For(recorder, DiagnosticTests.Upload);
                            uploadResult = Measurements.Measured(await speedTestClient.GetUploadSpeedAsync(chosen.Server, TransferReports(uploading, Redraw), upload, cancellationToken, timeLimitToken), timeLimitToken);

                            if (uploadResult is null)
                            {
                                table.Remove(uploading);
                            }
                            else
                            {
                                uploading.Settle(uploadResult);
                            }
                        }

                        Redraw();
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // No row is marked failed: cancellation stopped the measurements rather than
                    // failing them. Filtered on the run's own token, because a probe that timed out
                    // throws a cancellation exception too, and that is a failure.
                    context.Refresh();
                    throw;
                }
                catch (Exception)
                {
                    // A measurement that ends by throwing never settles its row, so without this
                    // the row keeps its spinner and the last frame on screen says it is still
                    // measuring after the run has ended.
                    foreach (SpeedTestTableRow? row in new[] { pinging, downloading, uploading })
                    {
                        if (row is { IsRunning: true })
                        {
                            row.Fail();
                        }
                    }

                    // The display draws its last frame when the block finishes, but not when it
                    // ends by throwing, so a failed run draws that frame itself.
                    context.Refresh();
                    throw;
                }
            });

        var outcome = SpeedTestOutcome.Create(latency, downloadResult, uploadResult);

        if (plain is not null)
        {
            SimpleLines.Write(plain, latency, outcome, settings);
        }

        return outcome;
    }

    private async Task<ChosenServer> ChooseWithSpinnerAsync(IAnsiConsole console, ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, IDiagnosticRecorder recorder, CancellationToken cancellationToken, CancellationToken timeLimitToken) =>
        await console.Progress()
            .AutoClear(true)
            .AutoRefresh(liveDisplayRefresh.AutoRefresh)
            .Columns(
            [
                new SpinnerColumn { Style = theme.Spinner },
                new TaskDescriptionColumn(),
            ])
            .StartAsync(async progress =>
            {
                var choosing = progress.AddTask("Choosing server", autoStart: true, maxValue: 100);

                try
                {
                    return await ServerSelector.ChooseServerAsync(speedTestClient, settings, recorder, cancellationToken, timeLimitToken);
                }
                finally
                {
                    choosing.StopTask();
                }
            });

    private void WriteHeader(IAnsiConsole console, ChosenServer chosen)
    {
        console.WriteLine("");

        console.WriteLine(chosen.Server.Sponsor, theme.ServerName);
        console.WriteLine(chosen.Server.Url, theme.ServerUrl);
        console.WriteLine("");
    }

    // Drops the row where nothing was measured, so it never reports on nothing.
    private static void Settle(SpeedTestTable table, SpeedTestTableRow row, LatencyTestResult? latency)
    {
        if (latency is LatencyTestResult measured)
        {
            row.Settle(measured);
            return;
        }

        table.Remove(row);
    }

    // Applied inline: Progress<T> posts reports through the synchronization context or the thread
    // pool, which races the renderer and drops updates.
    private static IProgress<LatencyTestProgress>? LatencyReports(SpeedTestTableRow? row, Action redraw) =>
        row is null ? null : new InlineProgress<LatencyTestProgress>(report => { row.Report(report); redraw(); });

    // Applied inline for the same reason.
    private static IProgress<SpeedTestProgress> TransferReports(SpeedTestTableRow row, Action redraw) =>
        new InlineProgress<SpeedTestProgress>(report => { row.Report(report); redraw(); });

    // A composite console is unpacked so each destination is judged on what it is: a terminal
    // redraws, a --file target never does. Both are null under --quiet with no file, where the run
    // writes nowhere at all.
    private static (IAnsiConsole? Animating, IAnsiConsole? Plain) Split(IAnsiConsole console)
    {
        if (console is not CompositeAnsiConsole composite)
        {
            return CanAnimate(console) ? (console, null) : (null, console);
        }

        var animating = composite.Destinations.Where(CanAnimate).ToArray();
        var plain = composite.Destinations.Where(destination => !CanAnimate(destination)).ToArray();

        return (Wrap(animating), Wrap(plain));
    }

    // Interactivity alone decides it: "a terminal that cannot redraw" means one Spectre.Console
    // reports as not interactive.
    internal static bool CanAnimate(IAnsiConsole console) => console.Profile.Capabilities.Interactive;

    private static IAnsiConsole? Wrap(IAnsiConsole[] destinations) => destinations.Length switch
    {
        0 => null,
        1 => destinations[0],

        // Two of the same kind: --file away from a terminal, under cron or through a pipe, where
        // neither can redraw and both get the simple lines.
        _ => new CompositeAnsiConsole(destinations[0], destinations),
    };

    private sealed class InlineProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
