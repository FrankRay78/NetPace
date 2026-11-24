using NetPace.Console.Theming;

namespace NetPace.Console.Tests.Theming;

/// <summary>
/// The themes a theory runs over, read from <see cref="Theme.All"/> rather than listed by hand.
/// </summary>
/// <remarks>
/// A theme added to <see cref="Theme.All"/> joins every theory reading these rows, so its
/// snapshots fail as missing rather than leaving it silently untested.
/// <para>
/// The names a user types are the exception, being an interface contract, so the tests that pin
/// them list them explicitly.
/// </para>
/// </remarks>
internal static class ThemeRows
{
    /// <summary>Every theme that draws in colour: all of them but the one that suppresses it.</summary>
    public static IEnumerable<Theme> InColour => Theme.All.Where(theme => !theme.SuppressesColour);

    /// <summary>
    /// The themes a user chooses for their colours: every theme in colour but today's look, which
    /// is what a run draws in when nothing is chosen.
    /// </summary>
    public static IEnumerable<Theme> Coloured => InColour.Where(theme => theme != Theme.Default);

    /// <summary>Every theme NetPace carries, by name.</summary>
    public static TheoryData<string> EveryName => Names(Theme.All);

    /// <summary>Every theme that draws in colour, by name.</summary>
    public static TheoryData<string> InColourNames => Names(InColour);

    /// <summary>Every theme a user chooses for its colours, by name.</summary>
    public static TheoryData<string> ColouredNames => Names(Coloured);

    /// <summary>The theme a theory row names.</summary>
    public static Theme Named(string name) => Theme.All.Single(theme => theme.Name == name);

    private static TheoryData<string> Names(IEnumerable<Theme> themes)
    {
        var rows = new TheoryData<string>();
        foreach (var theme in themes)
        {
            rows.Add(theme.Name);
        }
        return rows;
    }
}
