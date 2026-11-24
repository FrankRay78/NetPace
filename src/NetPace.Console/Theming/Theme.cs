namespace NetPace.Console.Theming;

/// <summary>
/// The colours and emphasis NetPace draws its human-readable screens in.
/// </summary>
/// <remarks>
/// A standing preference rather than a per-run choice, so it is chosen through the environment and
/// never through an option. Only the screens a person reads take their appearance from here.
/// <para>
/// Every element NetPace colours has a slot, named for what is being drawn and never for the
/// colour it ends up, because the colour is what a theme changes.
/// </para>
/// </remarks>
public sealed record Theme
{
    /// <summary>
    /// The name a user types into <c>NETPACE_THEME</c> to choose this theme, in lower case.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Whether this theme draws nothing in colour at all, so <see cref="ApplyTo"/> takes colour
    /// away from the console rather than relying on every slot being left unset.
    /// </summary>
    /// <remarks>
    /// Read off the slots rather than stated alongside them, so it cannot disagree with them: a
    /// theme wrongly claiming colour would leave the console colour-capable.
    /// </remarks>
    public bool SuppressesColour =>
        BannerNet == Color.Default && BannerPace == Color.Default
        && Styles.All(style => style.Foreground == Color.Default && style.Background == Color.Default);

    /// <summary>
    /// The Net half of the lettering at the top of the help screen.
    /// </summary>
    /// <remarks>
    /// Two slots so a theme may draw the halves in a pair of colours, purely as decoration: the
    /// lettering spells the product name whether or not they are told apart.
    /// </remarks>
    public required Color BannerNet { get; init; }

    /// <summary>
    /// The Pace half of the lettering at the top of the help screen.
    /// </summary>
    public required Color BannerPace { get; init; }

    /// <summary>The help screen's section headings — DESCRIPTION, USAGE, OPTIONS and the rest.</summary>
    public required Style Heading { get; init; }

    /// <summary>The stand-ins in the usage line for what the user would type, such as [OPTIONS].</summary>
    public required Style Placeholder { get; init; }

    /// <summary>A command's name, in the usage line and in the list of commands.</summary>
    public required Style CommandName { get; init; }

    /// <summary>The DEFAULT heading above the column of option defaults.</summary>
    public required Style DefaultColumnHeading { get; init; }

    /// <summary>An option's name and short alias in the options table.</summary>
    public required Style OptionName { get; init; }

    /// <summary>An option's default value in the options table.</summary>
    public required Style DefaultValue { get; init; }

    /// <summary>A suggestion to the user in passing, such as which key exits a live listing.</summary>
    public required Style Hint { get; init; }

    /// <summary>
    /// The word that marks a message as a failure, and in the live table the mark against a
    /// measurement that produced nothing and the count of its failed requests. Each theme may
    /// colour this differently, and <see cref="Plain"/> not at all, because the word or mark itself
    /// is what makes a failure recognisable, never the colour alone.
    /// </summary>
    public required Style Error { get; init; }

    /// <summary>The sponsor of the server a speed test chose.</summary>
    public required Style ServerName { get; init; }

    /// <summary>The address of the server a speed test chose.</summary>
    public required Style ServerUrl { get; init; }

    /// <summary>A column heading of the live table — Test, Done, Speed, Latency, Jitter, Failed.</summary>
    public required Style ColumnHeading { get; init; }

    /// <summary>
    /// A measured figure in the live table, or the name of the test on its row (Pinging,
    /// Downloading, Uploading).
    /// </summary>
    public required Style Figure { get; init; }

    /// <summary>The lines a table is drawn with.</summary>
    public required Style TableBorder { get; init; }

    /// <summary>The mark against a measurement whose every request succeeded.</summary>
    public required Style Success { get; init; }

    /// <summary>
    /// The mark against a measurement some of whose requests failed, and the count that says how
    /// many. Distinct from <see cref="Error"/>, because a partial failure still measured something.
    /// </summary>
    public required Style PartialFailure { get; init; }

    /// <summary>How far through a measurement still running is.</summary>
    public required Style Percentage { get; init; }

    /// <summary>How far through a measurement that has finished got.</summary>
    public required Style PercentageComplete { get; init; }

    /// <summary>The spinner shown while a server is being chosen, and beside a running measurement.</summary>
    public required Style Spinner { get; init; }

    /// <summary>
    /// Every style slot on this theme, in a fixed order, so a theme can be checked as a whole
    /// rather than one named property at a time.
    /// </summary>
    /// <remarks>
    /// Every <see cref="Style"/> slot belongs here, the two <see cref="Color"/> banner slots being
    /// asserted on separately.
    /// <para>
    /// A test pins this list against the <see cref="Style"/> properties the type declares, so a
    /// slot left out of it fails rather than quietly going uncovered.
    /// </para>
    /// </remarks>
    public IReadOnlyList<Style> Styles =>
    [
        Heading,
        Placeholder,
        CommandName,
        DefaultColumnHeading,
        OptionName,
        DefaultValue,
        Hint,
        Error,
        ServerName,
        ServerUrl,
        ColumnHeading,
        Figure,
        TableBorder,
        Success,
        PartialFailure,
        Percentage,
        PercentageComplete,
        Spinner
    ];

    /// <summary>
    /// Today's look, and what a run with no theme chosen draws in.
    /// </summary>
    /// <remarks>
    /// Every slot carries a value rather than being left unset, so every theme reaches the
    /// renderer the same way and there is one place to read what the unthemed screen looks like.
    /// </remarks>
    public static Theme Default { get; } = new()
    {
        Name = "default",
        BannerNet = Color.Yellow,
        BannerPace = Color.Yellow,
        Heading = new Style(foreground: Color.Yellow, decoration: Decoration.Bold),
        Placeholder = new Style(foreground: Color.Grey),
        CommandName = new Style(foreground: Color.Aqua),
        DefaultColumnHeading = new Style(foreground: Color.Lime),
        OptionName = new Style(foreground: Color.Silver),
        DefaultValue = new Style(decoration: Decoration.Bold),
        Hint = new Style(foreground: Color.Yellow),
        Error = new Style(foreground: Color.Red),
        ServerName = new Style(foreground: Color.Yellow, decoration: Decoration.Bold),
        ServerUrl = Style.Plain,
        ColumnHeading = Style.Plain,
        Figure = Style.Plain,
        TableBorder = new Style(foreground: Color.Red),
        Success = new Style(foreground: Color.Green),
        PartialFailure = new Style(foreground: Color.Yellow),
        Percentage = Style.Plain,
        PercentageComplete = new Style(foreground: Color.Green),
        Spinner = new Style(foreground: Color.Yellow)
    };

    /// <summary>
    /// No colour and no emphasis anywhere — what NetPace looks like when the user has asked for no
    /// colour at all. Nothing in the live table is marked by colour alone: a measurement's outcome
    /// is readable from its symbol and its failed count, so an uncoloured screen loses nothing but
    /// the colour.
    /// </summary>
    public static Theme Plain { get; } = new()
    {
        Name = "plain",
        BannerNet = Color.Default,
        BannerPace = Color.Default,
        Heading = Style.Plain,
        Placeholder = Style.Plain,
        CommandName = Style.Plain,
        DefaultColumnHeading = Style.Plain,
        OptionName = Style.Plain,
        DefaultValue = Style.Plain,
        Hint = Style.Plain,
        Error = Style.Plain,
        ServerName = Style.Plain,
        ServerUrl = Style.Plain,
        ColumnHeading = Style.Plain,
        Figure = Style.Plain,
        TableBorder = Style.Plain,
        Success = Style.Plain,
        PartialFailure = Style.Plain,
        Percentage = Style.Plain,
        PercentageComplete = Style.Plain,
        Spinner = Style.Plain
    };

    /// <summary>
    /// A green-led look.
    /// </summary>
    /// <remarks>
    /// Every colour but one is a mid-tone green, which reads against a black terminal and a white
    /// one alike where a bright or a deep green manages only one.
    /// <para>
    /// <see cref="BannerPace"/> is the exception: the help lettering needs a pair of greens far
    /// enough apart to read as two words, and it washes out on a white terminal.
    /// </para>
    /// </remarks>
    public static Theme Green { get; } = new()
    {
        Name = "green",
        BannerNet = Color.Green4,
        BannerPace = Color.Green1,
        Heading = new Style(foreground: Color.Green4, decoration: Decoration.Bold),
        Placeholder = new Style(foreground: Color.DarkSeaGreen4),
        CommandName = new Style(foreground: Color.SpringGreen4),
        DefaultColumnHeading = new Style(foreground: Color.Chartreuse4),
        OptionName = new Style(foreground: Color.DarkSeaGreen4),
        DefaultValue = new Style(foreground: Color.SpringGreen4, decoration: Decoration.Bold),
        Hint = new Style(foreground: Color.SpringGreen4),
        Error = new Style(foreground: Color.Chartreuse4, decoration: Decoration.Bold),
        ServerName = new Style(foreground: Color.Green4, decoration: Decoration.Bold),
        ServerUrl = new Style(foreground: Color.DarkSeaGreen4),
        ColumnHeading = new Style(foreground: Color.SpringGreen4),
        Figure = new Style(foreground: Color.Green4, decoration: Decoration.Bold),
        TableBorder = new Style(foreground: Color.DarkSeaGreen4),
        Success = new Style(foreground: Color.Green4),
        PartialFailure = new Style(foreground: Color.SpringGreen4, decoration: Decoration.Bold),
        Percentage = new Style(foreground: Color.DarkSeaGreen4),
        PercentageComplete = new Style(foreground: Color.SpringGreen4),
        Spinner = new Style(foreground: Color.SpringGreen4)
    };

    /// <summary>
    /// An orange-led look for Halloween, chosen by name and never by the date.
    /// </summary>
    /// <remarks>
    /// Neither failure tone is load-bearing: the mark and the failed count say how a measurement
    /// went, as <see cref="Error"/> describes.
    /// <para>
    /// Every colour clears a 3:1 contrast ratio against pure black and pure white alike, so the
    /// palette reads on a dark terminal and a light one.
    /// </para>
    /// </remarks>
    public static Theme Halloween { get; } = new()
    {
        Name = "halloween",
        BannerNet = Color.DarkOrange3_1,
        BannerPace = Color.Purple_2,
        Heading = new Style(foreground: Color.DarkOrange3_1, decoration: Decoration.Bold),
        Placeholder = new Style(foreground: Color.Grey53),
        CommandName = new Style(foreground: Color.DarkOrange3_1),
        DefaultColumnHeading = new Style(foreground: Color.MediumPurple),
        OptionName = new Style(foreground: Color.MediumPurple),
        DefaultValue = new Style(foreground: Color.DarkOrange3_1, decoration: Decoration.Bold),
        Hint = new Style(foreground: Color.Grey53),
        Error = new Style(foreground: Color.Red3_1, decoration: Decoration.Bold),
        ServerName = new Style(foreground: Color.DarkOrange3_1, decoration: Decoration.Bold),
        ServerUrl = new Style(foreground: Color.Grey53),
        ColumnHeading = new Style(foreground: Color.MediumPurple),
        Figure = new Style(foreground: Color.DarkOrange3_1, decoration: Decoration.Bold),
        TableBorder = new Style(foreground: Color.MediumPurple),
        Success = new Style(foreground: Color.Green4),
        PartialFailure = new Style(foreground: Color.DarkOrange3, decoration: Decoration.Bold),
        Percentage = new Style(foreground: Color.MediumPurple),
        PercentageComplete = new Style(foreground: Color.Green4),
        Spinner = new Style(foreground: Color.DarkOrange3_1)
    };

    /// <summary>
    /// Every theme a user can choose, in the order the help screen lists them.
    /// </summary>
    public static IReadOnlyList<Theme> All { get; } = [Default, Plain, Green, Halloween];

    /// <summary>
    /// The theme names as the help screen spells them, in the notation the options table uses for a
    /// fixed set of choices.
    /// </summary>
    public static string NameList { get; } = $"<{string.Join(", ", All.Select(theme => theme.Name))}>";

    /// <summary>
    /// The theme <paramref name="requested"/> names, matched without regard to case.
    /// </summary>
    /// <param name="requested">
    /// What the user asked for. <c>null</c> and empty both mean nothing was asked for, which is
    /// <see cref="Default"/> and not a mistake.
    /// </param>
    /// <param name="theme">The theme to draw in, which is <see cref="Default"/> whenever the name was not understood.</param>
    /// <returns>
    /// <c>false</c> only when <paramref name="requested"/> named something, and that something is
    /// not a theme — so a caller can tell the user without having to decide what to draw in.
    /// </returns>
    public static bool TryParse(string? requested, out Theme theme)
    {
        theme = Default;

        if (string.IsNullOrEmpty(requested)) return true;

        Theme? named = All.FirstOrDefault(candidate => string.Equals(candidate.Name, requested, StringComparison.OrdinalIgnoreCase));

        if (named is null) return false;

        theme = named;
        return true;
    }

    /// <summary>
    /// Narrows <paramref name="console"/> to what this theme is willing to draw.
    /// </summary>
    /// <remarks>
    /// A colourless theme takes colour off the console rather than leaving every slot unset:
    /// anything Spectre.Console draws of its own accord has no slot here to unset.
    /// </remarks>
    /// <param name="console">The console the run draws on.</param>
    /// <exception cref="ArgumentNullException"><paramref name="console"/> is <c>null</c>.</exception>
    public void ApplyTo(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);

        if (SuppressesColour)
        {
            console.Profile.Capabilities.ColorSystem = ColorSystem.NoColors;
        }
    }
}
