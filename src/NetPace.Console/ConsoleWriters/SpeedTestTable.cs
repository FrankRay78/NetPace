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
/// Built afresh on every draw rather than updated in place: Spectre.Console's table refuses a
/// column added once it holds rows, and the <c>Failed</c> column appears only when a transfer
/// turns out to have failed.
/// </para>
/// <para>
/// A column's width is therefore the one thing that cannot be read off a single draw, and is held
/// here instead: a held column opens at the width reserved for it, grows where a wider value
/// arrives, and never narrows again for the rest of the run
/// (docs/change-intent-records/2026-11-15-three-significant-figures-and-held-column-widths.md).
/// The widths belong to this instance, so a width grown under <c>--count</c> or <c>--loop</c> does
/// not carry into the next run.
/// </para>
/// <para>
/// Down to about twenty-five characters every column is drawn, and a narrower window wraps the
/// table rather than dropping a figure, because a missing column reads as a measurement that was
/// never taken. Narrower than that, the console library reduces the table itself.
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
    private const string SucceededMark = "✓";
    private const string PartiallyFailedMark = "!";
    private const string FailedMark = "✗";

    // What the first and last marks become where the console cannot draw them.
    private const string SucceededMarkAscii = "OK";
    private const string FailedMarkAscii = "X";

    private const int MarkGap = 3;

    // Three rather than two: at two, 100% and 8.00 Mbps read as one figure, as did 4 ms and
    // 1 of 150. Three still leaves an auto-scaled table inside 80 characters.
    private const int ColumnGap = 3;

    // The headings of the columns whose width is held between draws.
    private const string DoneHeading = "Done";
    private const string SpeedHeading = "Speed";
    private const string LatencyHeading = "Latency";
    private const string JitterHeading = "Jitter";
    private const string FailedHeading = "Failed";

    // Three significant figures gives 9.99, 99.9 or 999 under SI, and IEC adds 1023 - four
    // characters at most, auto-scaled. A pinned SpeedScale has no such bound and widens the column.
    private const int WidestSpeedFigure = 4;

    // Three digits and the unit beside them, which covers every reading on a link anyone would run
    // a speed test over. A slower reading widens the column when it arrives and keeps it there.
    private const int ReservedMillisecondsWidth = 6;

    private readonly List<SpeedTestTableRow> rows = [];

    // Read and written only through AtColumnWidth, which is what keeps it a running maximum.
    private readonly Dictionary<string, int> columnWidths = [];

    private int reportsTaken;

    public SpeedTestTableRow AddRow(string name)
    {
        var row = new SpeedTestTableRow(this, name);
        rows.Add(row);
        return row;
    }

    // A row is added for every test the run was asked for before any has started, so the screen
    // shows what is coming. A test the run then never reached is not a measurement that failed, and
    // leaving its row behind with a spinner or a cross would say it was.
    public void Remove(SpeedTestTableRow row)
    {
        rows.Remove(row);
        Changed();
    }

    // Also what turns the spinner, so the screen moves whenever the measurement has something new
    // to say and stands still when it has not.
    internal void Changed() => reportsTaken++;

    internal string SpeedOf(SpeedTestResult result) => result.GetSpeedString(unit, unitSystem, scale);

    internal string SpeedOf(SpeedTestProgress progress) => progress.GetSpeedString(unit, unitSystem, scale);

    /// <inheritdoc/>
    /// <remarks>A table with no rows left takes no room, as <see cref="Render"/> draws nothing.</remarks>
    protected override Measurement Measure(RenderOptions options, int maxWidth)
    {
        ArgumentNullException.ThrowIfNull(options);

        return rows.Count == 0 ? new Measurement(0, 0) : ((IRenderable)Build(options.Unicode)).Measure(options, maxWidth);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Nothing is drawn once every row has been removed: headings over no figures would read as a
    /// result with its figures missing, where <c>--simple</c> writes nothing for the same run.
    /// </remarks>
    protected override IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        ArgumentNullException.ThrowIfNull(options);

        return rows.Count == 0 ? [] : ((IRenderable)Build(options.Unicode)).Render(options, maxWidth);
    }

    private Table Build(bool unicode)
    {
        // Borderless, so the table reads as a block of figures rather than as a grid. The mark
        // column carries no heading: what it means is read from the mark.
        var table = new Table()
            .Border(TableBorder.None)
            .ShowHeaders();

        // Read off the rows rather than asked for up front, so the column appears the moment a
        // transfer turns out to have failed, and never before.
        var showFailed = rows.Any(row => row.FailedCount is not null);

        List<TableColumn> columns =
        [
            new TableColumn(string.Empty) { Alignment = Justify.Left, Padding = Gap(MarkGap) },
            Heading("Test", Justify.Left),
            Heading(DoneHeading, Justify.Right),
            Heading(SpeedHeading, Justify.Right),
        ];

        if (showLatency)
        {
            columns.Add(Heading(LatencyHeading, Justify.Right));
            columns.Add(Heading(JitterHeading, Justify.Right));
        }

        if (showFailed)
        {
            columns.Add(Heading(FailedHeading, Justify.Right));
        }

        // The last column carries no gap, so a line ends at its last column rather than a column's
        // worth of blank past it. An empty right-aligned cell is still padded to the column width,
        // so a row whose last cell is blank does end in blanks.
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
                Cell(Mark(row, unicode).PadRight(WidestMark(unicode)), outcome),
                Cell(row.Name, theme.Figure),
                Cell(AtColumnWidth(DoneHeading, row.PercentageText), row.IsRunning ? theme.Percentage : theme.PercentageComplete),
                Cell(AtColumnWidth(SpeedHeading, row.Speed), theme.Figure),
            };

            if (showLatency)
            {
                cells.Add(Cell(AtColumnWidth(LatencyHeading, Milliseconds(row.LatencyMilliseconds)), theme.Figure));
                cells.Add(Cell(AtColumnWidth(JitterHeading, Milliseconds(row.JitterMilliseconds)), theme.Figure));
            }

            if (showFailed)
            {
                cells.Add(Cell(AtColumnWidth(FailedHeading, row.FailedText), outcome));
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

    // Trailing rather than leading, so a right-aligned column's own widest figure sets where its
    // values sit.
    private static Padding Gap(int width) => new(0, 0, width, 0);

    // Pads the value rather than fixing the column's width, which leaves Spectre.Console sizing the
    // column to the widest thing in it and only raises the floor it sizes against, so a value too
    // wide for the floor is still drawn whole. Blank cells are padded too, so a column is at its
    // reserved width from the first draw rather than from the first figure.
    private string AtColumnWidth(string heading, string text)
    {
        var width = Math.Max(
            columnWidths.TryGetValue(heading, out var held) ? held : ReservedWidth(heading),
            text.Length);

        columnWidths[heading] = width;

        return text.PadLeft(width);
    }

    // Only the columns whose figures change width mid-run reserve anything: a percentage never
    // outgrows its own heading, and a failure count is written once. Done and Failed are still
    // held, so the no-narrowing guarantee covers every column.
    private int ReservedWidth(string heading) => heading switch
    {
        SpeedHeading => WidestSpeedFigure + 1 + WidestUnitName(),
        LatencyHeading or JitterHeading => ReservedMillisecondsWidth,
        _ => 0,
    };

    // Read back out of the formatter rather than from a list of unit names kept here, so a new unit
    // system or a renamed unit widens the reserve by itself.
    private int WidestUnitName()
    {
        IEnumerable<SpeedScale> reachable = scale == SpeedScale.Auto
            ? Enum.GetValues<SpeedScale>().Where(candidate => candidate != SpeedScale.Auto)
            : [scale];

        // Any non-zero measurement will do: a unit name does not depend on the figure beside it.
        var oneByteInOneSecond = new SpeedTestProgress { BytesProcessed = 1, ElapsedMilliseconds = 1000 };

        return reachable.Max(candidate =>
            oneByteInOneSecond.GetSpeedStringParts(unit, unitSystem, candidate).unit.Length);
    }

    // Drawn literally: a sponsor or a unit holding a square bracket is text, not markup.
    private static IRenderable Cell(string text, Style style) => new Markup(Markup.Escape(text), style);

    // A mark rather than a colour alone, so the outcome reads under the plain theme and with no
    // colour at all.
    private string Mark(SpeedTestTableRow row, bool unicode) => row.State switch
    {
        SpeedTestTableRowState.Running => SpinnerFrame(unicode),
        SpeedTestTableRowState.Succeeded => unicode ? SucceededMark : SucceededMarkAscii,
        SpeedTestTableRowState.PartiallyFailed => PartiallyFailedMark,
        _ => unicode ? FailedMark : FailedMarkAscii,
    };

    // Without Unicode the spinner's frames are one character and OK is two, so a column sized by
    // its contents would widen as the first row settled and shift every column to its right.
    private static int WidestMark(bool unicode) =>
        (unicode
            ? Spinner.Known.Dots.Frames.Append(SucceededMark).Append(FailedMark)
            : Spinner.Known.Ascii.Frames.Append(SucceededMarkAscii).Append(FailedMarkAscii))
        .Append(PartiallyFailedMark)
        .Max(mark => mark.Length);

    private Style OutcomeStyle(SpeedTestTableRow row) => row.State switch
    {
        SpeedTestTableRowState.Running => theme.Spinner,
        SpeedTestTableRowState.Succeeded => theme.Success,
        SpeedTestTableRowState.PartiallyFailed => theme.PartialFailure,
        _ => theme.Error,
    };

    // Stands still unless the caller asked for animation, because how many frames a run would turn
    // through depends on how much it reported. Spectre.Console's own frames, so this spinner
    // matches the one shown while the server is being chosen.
    private string SpinnerFrame(bool unicode)
    {
        var frames = unicode ? Spinner.Known.Dots.Frames : Spinner.Known.Ascii.Frames;

        return frames[animated ? reportsTaken % frames.Count : 0];
    }

    // A blank cell says not measured; a zero would say measured as none. The Speed column is the
    // exception: a transfer whose every request failed shows what they moved, 0 bps where nothing
    // arrived, because a blank speed on a finished row would read as a figure still to come.
    private static string Milliseconds(long? value) =>
        value is long measured ? $"{measured.ToString(CultureInfo.InvariantCulture)} ms" : string.Empty;
}
