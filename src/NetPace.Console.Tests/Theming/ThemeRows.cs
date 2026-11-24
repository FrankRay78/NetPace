using NetPace.Console.Theming;

namespace NetPace.Console.Tests.Theming;

/// <summary>
/// The themes a theory runs over, read from <see cref="Theme.All"/> rather than listed by hand.
/// </summary>
/// <remarks>
/// A theme added to <see cref="Theme.All"/> joins every theory that reads these rows, so its
/// snapshots fail as missing until someone reads and accepts them. Rows listed by hand would leave
/// it silently untested instead. The names a user types are the exception: they are an interface
/// contract, so the tests that pin them list them explicitly.
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

    public static TheoryData<string> EveryName => Names(Theme.All);

    public static TheoryData<string> InColourNames => Names(InColour);

    public static TheoryData<string> ColouredNames => Names(Coloured);

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
