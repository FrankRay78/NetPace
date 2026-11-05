using NetPace.Console.Theming;

namespace NetPace.Console.Tests.Theming;

/// <summary>
/// A theme provider that always hands back the theme it was given.
/// </summary>
/// <remarks>
/// The suite's default, so no snapshot depends on what the person running the tests happens to have
/// set <c>NETPACE_THEME</c> to. A test about how the variable is read wants
/// <see cref="EnvironmentThemeProvider"/> with a reader of its own instead.
/// </remarks>
internal sealed class FixedThemeProvider(Theme theme) : IThemeProvider
{
    /// <inheritdoc />
    public ThemeSelection Resolve() => new(theme, UnrecognisedValue: null);
}
