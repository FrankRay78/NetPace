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
/// Built afresh on every draw, Spectre.Console refusing a column added once a table holds rows.
/// Column widths are therefore held here, growing but never narrowing. See CIR
/// <c>docs/change-intent-records/2026-11-15-three-significant-figures-and-held-column-widths.md</c>.
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
/// Whether the spinner turns as the measurement reports. Off leaves it on its first frame, so a
/// captured screen does not depend on how much a run happened to report.
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
    /// column beside it. Three rather than two: at two, <c>100%</c> and <c>8.00 Mbps</c> stood
    /// close enough to read as one figure, as did <c>4 ms</c> and <c>1 of 150</c>, and the third
    /// blank separates them while still leaving an auto-scaled table inside 80 characters.
    /// </summary>
    private const int ColumnGap = 3;

    /// <summary>The headings of the columns whose width is held between draws.</summary>
    private const string DoneHeading = "Done";
    private const string SpeedHeading = "Speed";
    private const string LatencyHeading = "Latency";
    private const string JitterHeading = "Jitter";
    private const string FailedHeading = "Failed";

    /// <summary>
    /// The widest figure an auto-scaled speed is ever written as. Three significant figures gives
    /// <c>9.99</c>, <c>99.9</c> or <c>999</c> under SI, and IEC adds <c>1023</c>, which is the
    /// widest of them: four characters at most, whichever band and unit system a reading lands in.
    /// A pinned <see cref="SpeedScale"/> has no such bound, and widens the column instead.
    /// </summary>
    private const int WidestSpeedFigure = 4;

    /// <summary>
    /// What the latency and jitter columns reserve: three digits and the unit beside them, which
    /// covers every reading on a link anyone would run a speed test over. The <c>Latency</c> and
    /// <c>Jitter</c> headings are already this wide or wider, so today this floor is what the
    /// headings would give anyway; it states the intent and outlives a shorter heading. A slower
    /// reading widens the column when it arrives and keeps it there.
    /// </summary>
    private const int ReservedMillisecondsWidth = 6;

    private readonly List<SpeedTestTableRow> rows = [];

    /// <summary>
    /// The width each held column has been drawn at so far, by heading. Read and written only
    /// through <see cref="AtColumnWidth"/>, which is what keeps it a running maximum.
    /// </summary>
    private readonly Dictionary<string, int> columnWidths = [];

    private int reportsTaken;

    /// <summary>Adds a row for the measurement <paramref name="name"/> names, and returns it.</summary>
    public SpeedTestTableRow AddRow(string name)
    {
        var row = new SpeedTestTableRow(this, name);
        rows.Add(row);
        return row;
    }

    /// <summary>
    /// Drops <paramref name="row"/> from the table, for a measurement that turned out not to run
    /// at all.
    /// </summary>
    /// <remarks>
    /// Every test the run was asked for gets a row before any starts, so the screen shows what is
    /// coming. A test never reached did not fail, so a spinner or a cross left behind would lie.
    /// </remarks>
    public void Remove(SpeedTestTableRow row)
    {
        rows.Remove(row);
        Changed();
    }

    /// <summary>Called by a row when something it shows has changed.</summary>
    /// <remarks>
    /// Also what turns the spinner, so the screen moves whenever the measurement has something new
    /// to say. A measurement reports per probe and per request, often enough to read as turning.
    /// </remarks>
    internal void Changed() => reportsTaken++;

    /// <summary>A speed as the table shows it, in the unit options the run was given.</summary>
    internal string SpeedOf(SpeedTestResult result) => result.GetSpeedString(unit, unitSystem, scale);

    /// <summary>A speed as the table shows it while the transfer is still running.</summary>
    internal string SpeedOf(SpeedTestProgress progress) => progress.GetSpeedString(unit, unitSystem, scale);

    /// <inheritdoc/>
    /// <remarks>
    /// A table with no rows left takes no room, for the reason <see cref="Render"/> draws nothing.
    /// </remarks>
    protected override Measurement Measure(RenderOptions options, int maxWidth)
    {
        ArgumentNullException.ThrowIfNull(options);

        return rows.Count == 0 ? new Measurement(0, 0) : ((IRenderable)Build(options.Unicode)).Measure(options, maxWidth);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// With every row removed, nothing is drawn: headings over no figures would read as a result
    /// with its figures missing, where <c>--simple</c> writes nothing for the same run.
    /// </remarks>
    protected override IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        ArgumentNullException.ThrowIfNull(options);

        return rows.Count == 0 ? [] : ((IRenderable)Build(options.Unicode)).Render(options, maxWidth);
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

        // The gap belongs between columns, so the last carries none and a line ends at its last
        // column. An empty right-aligned cell is still padded to the column width.
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

    /// <summary>
    /// The blank a column leaves after itself. Trailing rather than leading, so a right-aligned
    /// column's own widest figure is what sets where its values sit, and the gap is what separates
    /// them from the next column.
    /// </summary>
    private static Padding Gap(int width) => new(0, 0, width, 0);

    /// <summary>
    /// <paramref name="text"/> padded out to the width the column headed <paramref name="heading"/>
    /// holds, widening that column where the text is wider than anything it has held before.
    /// </summary>
    /// <remarks>
    /// Padding the value rather than fixing the width only raises the floor Spectre.Console sizes
    /// against, so a value too wide for it is still drawn whole.
    /// <para>
    /// Blank cells are padded too, so a column is at its reserved width from the first draw.
    /// </para>
    /// </remarks>
    private string AtColumnWidth(string heading, string text)
    {
        var width = Math.Max(
            columnWidths.TryGetValue(heading, out var held) ? held : ReservedWidth(heading),
            text.Length);

        columnWidths[heading] = width;

        return text.PadLeft(width);
    }

    /// <summary>
    /// The width a column opens at, before any value has been drawn in it.
    /// </summary>
    /// <remarks>
    /// Only a column whose figures change width mid-run reserves anything. All are still held, so
    /// the no-narrowing guarantee covers every column rather than the ones it was needed for.
    /// </remarks>
    private int ReservedWidth(string heading) => heading switch
    {
        SpeedHeading => WidestSpeedFigure + 1 + WidestUnitName(),
        LatencyHeading or JitterHeading => ReservedMillisecondsWidth,
        _ => 0,
    };

    /// <summary>
    /// The longest unit name a speed in this run can be written in: every scale auto-scaling can
    /// reach, or the one the run pinned.
    /// </summary>
    /// <remarks>
    /// Read back out of the formatter rather than from a list kept here, so a new or renamed unit
    /// widens the reserve by itself.
    /// </remarks>
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

    /// <summary>
    /// The width of the widest mark or spinner frame the console can be shown, which every mark is
    /// padded to. Without Unicode the spinner's frames are one character and <c>OK</c> is two, so
    /// a column sized by its contents would widen as the first row settled and move every column
    /// to its right while the user was reading them.
    /// </summary>
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
    /// deliberately the exception: a transfer whose every request failed shows what those requests
    /// moved, <c>0 bps</c> where nothing arrived, rather than a blank, because the mark and the Failed count beside it already say what happened,
    /// and a blank speed on a finished row would read as a figure still to come.
    /// </summary>
    private static string Milliseconds(long? value) =>
        value is long measured ? $"{measured.ToString(CultureInfo.InvariantCulture)} ms" : string.Empty;
}
