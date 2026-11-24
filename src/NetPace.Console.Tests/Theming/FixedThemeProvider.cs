using NetPace.Console.Theming;

namespace NetPace.Console.Tests.Theming;

/// <summary>
/// A theme provider that always hands back the theme it was given.
/// </summary>
/// <remarks>
/// The suite's default, so no snapshot depends on what <c>NETPACE_THEME</c> happens to be set to.
/// A test about how the variable is read wants <see cref="EnvironmentThemeProvider"/> instead.
/// </remarks>
internal sealed class FixedThemeProvider(Theme theme) : IThemeProvider
{
    /// <inheritdoc />
    public ThemeSelection Resolve() => new(theme, UnrecognisedValue: null);
}
