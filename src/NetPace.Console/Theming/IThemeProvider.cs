namespace NetPace.Console.Theming;

/// <summary>
/// Where the theme for a run comes from.
/// </summary>
/// <remarks>
/// An interface rather than a call to <see cref="Environment.GetEnvironmentVariable(string)"/> at
/// the point of use, so a test says what the user set without changing the environment every other
/// test in the run shares.
/// </remarks>
public interface IThemeProvider
{
    /// <summary>
    /// The theme this run draws in, and what the user asked for if it was not a theme.
    /// </summary>
    ThemeSelection Resolve();
}

/// <summary>
/// Reads the theme from the environment.
/// </summary>
/// <remarks>
/// <c>NETPACE_THEME</c> is set once, in a shell profile, and reaches every command — including
/// <c>--help</c>, which NetPace renders before it parses any option and which an option therefore
/// could not reach.
/// <para>
/// <c>NO_COLOR</c> outranks it. A user who has asked every program on the machine for no colour has
/// already answered the question a theme asks, so any non-empty value selects
/// <see cref="Theme.Plain"/> whatever <c>NETPACE_THEME</c> says — the convention published at
/// no-color.org, which Spectre.Console does not implement itself.
/// </para>
/// </remarks>
public sealed class EnvironmentThemeProvider : IThemeProvider
{
    /// <summary>The variable a user sets to choose a theme.</summary>
    public const string ThemeVariable = "NETPACE_THEME";

    /// <summary>The variable a user sets to ask every program for no colour.</summary>
    public const string NoColourVariable = "NO_COLOR";

    private readonly Func<string, string?> _readVariable;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvironmentThemeProvider"/> class, reading the
    /// process environment.
    /// </summary>
    public EnvironmentThemeProvider() : this(Environment.GetEnvironmentVariable)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvironmentThemeProvider"/> class reading from
    /// <paramref name="readVariable"/> instead of the process environment.
    /// </summary>
    /// <param name="readVariable">Answers with the value of the named variable, or <c>null</c> when it is not set.</param>
    internal EnvironmentThemeProvider(Func<string, string?> readVariable)
    {
        _readVariable = readVariable;
    }

    /// <inheritdoc />
    public ThemeSelection Resolve()
    {
        var requested = _readVariable(ThemeVariable);
        var recognised = Theme.TryParse(requested, out var theme);

        if (!string.IsNullOrEmpty(_readVariable(NoColourVariable)))
        {
            theme = Theme.Plain;
        }

        // A name that means nothing is still worth saying, even where no colour would have shown
        // either way: the user has a typo in their shell profile and nothing else will mention it.
        return new ThemeSelection(theme, recognised ? null : requested);
    }
}
