using System.Globalization;
using NetPace.Console.Theming;
using NetPace.Core;
using Spectre.Console.Rendering;

namespace NetPace.Console.ConsoleWriters;

/// <summary>
/// The live table a speed test draws at a terminal: one row per measurement, each figure beside
/// the test it belongs to, so idle latency and the latency measured under each transfer read
/// straight down a column.
/// </summary>
/// <remarks>
/// <para>
/// Built afresh on every draw rather than updated in place. Spectre.Console's table refuses a
/// column added once it holds rows, and the <c>Failed</c> column appears only when a transfer
/// turns out to have failed, so a table kept between draws would have to be rebuilt at exactly
/// that moment anyway. Rebuilding always is one path instead of two, and it is what lets every
/// column size itself to the widest value it has been given: no width is reserved in advance, and
/// the table redraws to fit when a larger figure arrives.
/// </para>
/// <para>
/// Every column is drawn at every width. A window too narrow for the table wraps it rather than
/// dropping a figure the user asked to see, which is the one thing a narrow window must not do:
/// a missing column reads as a measurement that was never taken.
/// </para>
/// </remarks>
/// <param name="theme">The colours and emphasis to draw in.</param>
/// <param name="unit">The unit a speed is shown in.</param>
/// <param name="unitSystem">Whether a scale steps by 1000 or by 1024.</param>
/// <param name="scale">The scale a speed is shown at.</param>
/// <param name="showLatency">
/// Whether the latency and jitter columns are drawn at all. They are not under
/// <c>--no-latency</c>, where nothing would ever fill them.
/// </param>
/// <param name="animated">
/// Whether the spinner beside a running measurement turns as the measurement reports. Off leaves
/// it on its first frame, so a captured screen does not depend on how much a run happened to
/// report - the same reason <see cref="ILiveDisplayRefresh"/> exists, and driven by the same switch.
/// </param>
internal sealed class SpeedTestTable(
    Theme theme,
    SpeedUnit unit,
    SpeedUnitSystem unitSystem,
    SpeedScale scale,
    bool showLatency,
    bool animated) : Renderable
{
    /// <summary>The mark against a measurement whose every request succeeded.</summary>
    private const string SucceededMark = "✓";

    /// <summary>The mark against a measurement some of whose requests failed.</summary>
    private const string PartiallyFailedMark = "!";

    /// <summary>The mark against a measurement that produced nothing.</summary>
    private const string FailedMark = "✗";

    /// <summary>What those first and last marks become where the console cannot draw them.</summary>
    private const string SucceededMarkAscii = "OK";
    private const string FailedMarkAscii = "X";

    /// <summary>
    /// The blank between a row's mark and the name of what it measures, wide enough that the two
    /// read as separate things.
    /// </summary>
    private const int MarkGap = 3;

    /// <summary>
    /// The blank between one column and the next, so a column's figures do not run into the
    /// column beside it.
    /// </summary>
    private const int ColumnGap = 2;

    private readonly List<SpeedTestTableRow> rows = [];

    private int reportsTaken;

    /// <summary>Adds a row for the measurement <paramref name="name"/> names, and returns it.</summary>
    public SpeedTestTableRow AddRow(string name)
    {
        var row = new SpeedTestTableRow(this, name);
        rows.Add(row);
        return row;
    }

    /// <summary>Called by a row when something it shows has changed.</summary>
    /// <remarks>
    /// Also what turns the spinner, so the screen moves whenever the measurement has something new
    /// to say and stands still when it has not. A measurement reports as it goes - per probe, per
    /// request - so this is often enough for the spinner to read as turning.
    /// </remarks>
    internal void Changed() => reportsTaken++;

    /// <summary>A speed as the table shows it, in the unit options the run was given.</summary>
    internal string SpeedOf(SpeedTestResult result) => result.GetSpeedString(unit, unitSystem, scale);

    /// <summary>A speed as the table shows it while the transfer is still running.</summary>
    internal string SpeedOf(SpeedTestProgress progress) => progress.GetSpeedString(unit, unitSystem, scale);

    /// <inheritdoc/>
    protected override Measurement Measure(RenderOptions options, int maxWidth)
    {
        ArgumentNullException.ThrowIfNull(options);

        return ((IRenderable)Build(options.Unicode)).Measure(options, maxWidth);
    }

    /// <inheritdoc/>
    protected override IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        ArgumentNullException.ThrowIfNull(options);

        return ((IRenderable)Build(options.Unicode)).Render(options, maxWidth);
    }

    /// <summary>
    /// The table as it stands, for a console that can draw the marks and spinner frames where
    /// <paramref name="unicode"/> is set and plain stand-ins where it is not.
    /// </summary>
    private Table Build(bool unicode)
    {
        // Borderless, so the table reads as a block of figures rather than as a grid. The mark
        // column carries no heading: what it means is read from the mark, not from a label.
        var table = new Table()
            .Border(TableBorder.None)
            .ShowHeaders();

        // Read off the rows rather than asked for up front, so the column appears the moment a
        // transfer turns out to have failed and never before.
        var showFailed = rows.Any(row => row.FailedCount is not null);

        List<TableColumn> columns =
        [
            new TableColumn(string.Empty) { Alignment = Justify.Left, Padding = Gap(MarkGap) },
            Heading("Test", Justify.Left),
            Heading("Done", Justify.Right),
            Heading("Speed", Justify.Right),
        ];

        if (showLatency)
        {
            columns.Add(Heading("Latency", Justify.Right));
            columns.Add(Heading("Jitter", Justify.Right));
        }

        if (showFailed)
        {
            columns.Add(Heading("Failed", Justify.Right));
        }

        // The gap belongs between columns, so the last one carries none: a line ends at its last
        // column rather than a column's worth of blank past it. An empty right-aligned cell is
        // still padded out to the column width, so a row whose last cell is blank does end in
        // blanks.
        columns[^1].Padding = Gap(0);

        foreach (TableColumn column in columns)
        {
            table.AddColumn(column);
        }

        foreach (SpeedTestTableRow row in rows)
        {
            var outcome = OutcomeStyle(row);

            var cells = new List<IRenderable>
            {
                Cell(Mark(row, unicode), outcome),
                Cell(row.Name, theme.Figure),
                Cell(row.PercentageText, row.IsRunning ? theme.Percentage : theme.PercentageComplete),
                Cell(row.Speed, theme.Figure),
            };

            if (showLatency)
            {
                cells.Add(Cell(Milliseconds(row.LatencyMilliseconds), theme.Figure));
                cells.Add(Cell(Milliseconds(row.JitterMilliseconds), theme.Figure));
            }

            if (showFailed)
            {
                cells.Add(Cell(row.FailedText, outcome));
            }

            table.AddRow(cells);
        }

        return table;
    }

    private TableColumn Heading(string text, Justify alignment) =>
        new(new Markup(Markup.Escape(text), theme.ColumnHeading))
        {
            Alignment = alignment,
            Padding = Gap(ColumnGap),
        };

    /// <summary>
    /// The blank a column leaves after itself. Trailing rather than leading, so a right-aligned
    /// column's own widest figure is what sets where its values sit, and the gap is what separates
    /// them from the next column.
    /// </summary>
    private static Padding Gap(int width) => new(0, 0, width, 0);

    /// <summary>
    /// One cell, drawn literally: a sponsor or a unit holding a square bracket is text, not markup.
    /// </summary>
    private static IRenderable Cell(string text, Style style) => new Markup(Markup.Escape(text), style);

    /// <summary>
    /// What stands against a row: the spinner while it runs, and afterwards a mark saying whether
    /// every request succeeded, some failed, or all did. A mark rather than a colour alone, so the
    /// outcome reads under the plain theme and with no colour at all.
    /// </summary>
    private string Mark(SpeedTestTableRow row, bool unicode) => row.State switch
    {
        SpeedTestTableRowState.Running => SpinnerFrame(unicode),
        SpeedTestTableRowState.Succeeded => unicode ? SucceededMark : SucceededMarkAscii,
        SpeedTestTableRowState.PartiallyFailed => PartiallyFailedMark,
        _ => unicode ? FailedMark : FailedMarkAscii,
    };

    private Style OutcomeStyle(SpeedTestTableRow row) => row.State switch
    {
        SpeedTestTableRowState.Running => theme.Spinner,
        SpeedTestTableRowState.Succeeded => theme.Success,
        SpeedTestTableRowState.PartiallyFailed => theme.PartialFailure,
        _ => theme.Error,
    };

    /// <summary>
    /// The spinner's current frame, which stands still unless the caller asked for animation:
    /// how many frames a run would otherwise have turned through depends on how much it reported.
    /// Spectre.Console's own frames, so this spinner matches the one shown while the server is
    /// being chosen, and its plain stand-ins where the console cannot draw them.
    /// </summary>
    private string SpinnerFrame(bool unicode)
    {
        var frames = unicode ? Spinner.Known.Dots.Frames : Spinner.Known.Ascii.Frames;

        return frames[animated ? reportsTaken % frames.Count : 0];
    }

    /// <summary>
    /// A figure in milliseconds as a cell carries it, or nothing at all where it was not measured.
    /// A blank cell says not measured; a zero would say measured as none. The Speed column is
    /// deliberately the exception: a transfer whose every request failed shows <c>0 bps</c> rather
    /// than a blank, because the mark and the Failed count beside it already say what happened,
    /// and a blank speed on a finished row would read as a figure still to come.
    /// </summary>
    private static string Milliseconds(long? value) =>
        value is long measured ? $"{measured.ToString(CultureInfo.InvariantCulture)} ms" : string.Empty;
}
