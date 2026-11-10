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
/// <para>
/// The only writer that takes a theme: the machine-readable writers produce records another
/// program parses, and <c>--simple</c> is a plain line per figure that has never been coloured, so
/// none of them has an appearance to choose.
/// </para>
/// </remarks>
/// <param name="liveDisplayRefresh">
/// Whether the live displays redraw as they go. Switching redrawing off — as the console test host
/// does — leaves a slow run with the same screen as an instant one.
/// </param>
/// <param name="theme">The colours and emphasis to draw in.</param>
public sealed class DefaultConsoleWriter(ILiveDisplayRefresh liveDisplayRefresh, Theme theme) : IConsoleWriter
{
    /// <inheritdoc/>
    public bool MachineReadable => false;

    public async Task<SpeedTestOutcome> PerformSpeedTestAsync(bool initialSpeedTest, IAnsiConsole console, IClock clock, IClientInfoProvider clientInfoProvider, IDiagnosticRecorder recorder, ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(settings);

        (IAnsiConsole? animating, IAnsiConsole? plain) = Split(console);

        if (animating is null)
        {
            // Nothing this run writes to can redraw, so default output is the simple lines - which
            // is exactly what that writer already produces, rather than a second rendering of it.
            return await new SimpleConsoleWriter().PerformSpeedTestAsync(initialSpeedTest, plain ?? console, clock, clientInfoProvider, recorder, speedTestClient, settings, cancellationToken);
        }

        // Choose the server first, so the screen can name it before the first probe answers. The
        // spinner covers the wait, and is skipped where the user named the server and there was
        // nothing to choose.
        var chosen = string.IsNullOrEmpty(settings.ServerUrl)
            ? await ChooseWithSpinnerAsync(animating, speedTestClient, settings, recorder, cancellationToken)
            : await ServerSelector.ChooseServerAsync(speedTestClient, settings, recorder, cancellationToken);

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
                    latency = await ServerSelector.MeasureLatencyAsync(speedTestClient, chosen, settings, recorder, LatencyReports(pinging, Redraw), cancellationToken);
                    pinging?.Settle(latency);
                    Redraw();

                    if (downloading is not null)
                    {
                        using var download = DiagnosticTestScope.For(recorder, DiagnosticTests.Download);
                        downloadResult = await speedTestClient.GetDownloadSpeedAsync(chosen.Server, TransferReports(downloading, Redraw), download, cancellationToken);
                        downloading.Settle(downloadResult);
                        Redraw();
                    }

                    if (uploading is not null)
                    {
                        using var upload = DiagnosticTestScope.For(recorder, DiagnosticTests.Upload);
                        uploadResult = await speedTestClient.GetUploadSpeedAsync(chosen.Server, TransferReports(uploading, Redraw), upload, cancellationToken);
                        uploading.Settle(uploadResult);
                        Redraw();
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Cancellation stopped the measurements rather than failing them, so no row is
                    // marked failed: the table keeps whatever it had measured. The final frame is
                    // drawn below for the reason the next handler gives. Filtered on the run's own
                    // token, because a probe that timed out also throws a cancellation exception,
                    // and that is a failure the next handler must mark.
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

                    // The display draws its last frame when the block finishes, and not when the
                    // block ends by throwing, so a run whose measurement failed draws that frame
                    // itself: whatever was measured before it ended stays on screen, beside the
                    // row that says what happened.
                    context.Refresh();
                    throw;
                }
            });

        var outcome = SpeedTestOutcome.Create(downloadResult, uploadResult);

        if (plain is not null)
        {
            // The block above either set this or threw, in which case the run never reaches here.
            // Guarding on it instead would read as a condition that can fail, and would silently
            // leave a --file target empty if it ever did.
            SimpleLines.Write(plain, latency!, outcome, settings);
        }

        return outcome;
    }

    /// <summary>
    /// Chooses the server behind a spinner, so a wait of seconds on a slow link shows that
    /// something is happening.
    /// </summary>
    private async Task<ChosenServer> ChooseWithSpinnerAsync(IAnsiConsole console, ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, IDiagnosticRecorder recorder, CancellationToken cancellationToken) =>
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
                    return await ServerSelector.ChooseServerAsync(speedTestClient, settings, recorder, cancellationToken);
                }
                finally
                {
                    choosing.StopTask();
                }
            });

    /// <summary>
    /// What stands above the table: the chosen server's sponsor and address.
    /// </summary>
    private void WriteHeader(IAnsiConsole console, ChosenServer chosen)
    {
        console.WriteLine("");

        console.WriteLine(chosen.Server.Sponsor, theme.ServerName);
        console.WriteLine(chosen.Server.Url, theme.ServerUrl);
        console.WriteLine("");
    }

    /// <summary>
    /// A receiver for a latency measurement's reports that moves <paramref name="row"/> and
    /// redraws, or <c>null</c> where there is no row to move.
    /// </summary>
    /// <remarks>
    /// Reports are applied inline. <see cref="Progress{T}"/> posts them through the
    /// synchronization context or the thread pool, which races the renderer and drops updates.
    /// </remarks>
    private static IProgress<LatencyTestProgress>? LatencyReports(SpeedTestTableRow? row, Action redraw) =>
        row is null ? null : new InlineProgress<LatencyTestProgress>(report => { row.Report(report); redraw(); });

    /// <summary>A receiver for a transfer's reports, applied inline for the same reason.</summary>
    private static IProgress<SpeedTestProgress> TransferReports(SpeedTestTableRow row, Action redraw) =>
        new InlineProgress<SpeedTestProgress>(report => { row.Report(report); redraw(); });

    /// <summary>
    /// The destinations that can redraw and those that cannot, each wrapped back up as one console,
    /// or <c>null</c> where this run has none of that kind.
    /// </summary>
    /// <remarks>
    /// A composite console is unpacked so each destination is judged on what it is: a terminal
    /// redraws, a <c>--file</c> target never does. Both can be <c>null</c> under <c>--quiet</c>
    /// with no file, where the run writes nowhere at all.
    /// </remarks>
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

    /// <summary>
    /// Whether a destination can redraw. Interactivity alone decides it: "a terminal that cannot
    /// redraw" means one Spectre.Console reports as not interactive.
    /// </summary>
    internal static bool CanAnimate(IAnsiConsole console) => console.Profile.Capabilities.Interactive;

    private static IAnsiConsole? Wrap(IAnsiConsole[] destinations) => destinations.Length switch
    {
        0 => null,
        1 => destinations[0],

        // Two destinations of the same kind: the path --file takes away from a terminal (under
        // cron, through a pipe, or with a stream redirected), where the terminal and the file
        // both cannot redraw and both get the simple lines. The File tests run this way. The
        // wrapper is never disposed here: the composite the command built owns these
        // destinations and closes them itself.
        _ => new CompositeAnsiConsole(destinations[0], destinations),
    };

    private sealed class InlineProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
