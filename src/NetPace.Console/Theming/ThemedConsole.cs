using Spectre.Console.Rendering;

namespace NetPace.Console.Theming;

/// <summary>
/// Writing one line that is drawn in more than one style.
/// </summary>
internal static class ThemedConsole
{
    /// <summary>
    /// Writes a line made of differently styled runs of text, followed by a line ending.
    /// </summary>
    /// <remarks>
    /// The runs are handed to the console as a single renderable, not written one after another,
    /// because the console wraps each renderable it is given on its own: a line written in pieces
    /// overruns a narrow terminal that a line written whole would have folded. An empty run is left
    /// out, so a figure that was not measured leaves no stray space behind.
    /// </remarks>
    /// <param name="console">The console to write to.</param>
    /// <param name="runs">The text to write, each run with the style to draw it in.</param>
    public static void WriteStyledLine(this IAnsiConsole console, params (string Text, Style Style)[] runs)
    {
        console.Write(Compose(runs));
        console.WriteLine();
    }

    /// <summary>
    /// The runs as one renderable, so whatever wraps it sees the whole line.
    /// </summary>
    public static IRenderable Compose(params (string Text, Style Style)[] runs)
    {
        var paragraph = new Paragraph();

        foreach (var (text, style) in runs)
        {
            if (text.Length > 0)
            {
                paragraph.Append(text, style);
            }
        }

        return paragraph;
    }
}
