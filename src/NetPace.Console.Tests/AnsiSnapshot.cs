using System.Text.RegularExpressions;

namespace NetPace.Console.Tests;

/// <summary>
/// Turns captured terminal output into something a snapshot can hold and a reviewer can read, and
/// answers whether anything in it was styled.
/// </summary>
/// <remarks>
/// An escape character in a committed snapshot is invisible in a diff, so a colour change would
/// land as a blob of control bytes. Spelling it <c>\e</c> keeps the sequence legible.
/// </remarks>
internal static partial class AnsiSnapshot
{
    /// <summary>
    /// The escape character spelled so it survives a text snapshot: <c>\e</c>, the shell's own name
    /// for it. The session id a terminal hyperlink carries is replaced with a fixed stand-in,
    /// because it differs between renders and would make the snapshot unrepeatable.
    /// </summary>
    public static string Readable(string output) =>
        HyperlinkId().Replace(output.Replace("\u001b", @"\e"), "id=0;");

    /// <summary>
    /// Whether anything in the captured output was drawn in a colour or with emphasis.
    /// </summary>
    /// <remarks>
    /// Reads the escape sequences sent rather than the styles asked for, so it answers for
    /// whatever reaches the screen. Only the graphics sequence counts; a redraw is not appearance.
    /// </remarks>
    public static bool CarriesStyling(string output) => GraphicsSequence().IsMatch(output);

    /// <summary>
    /// The captured output with every colour and emphasis sequence removed, leaving what the screen
    /// says rather than how it looks. Lets two themes be compared for wording.
    /// </summary>
    public static string WithoutStyling(string output) => GraphicsSequence().Replace(output, string.Empty);

    [GeneratedRegex(@"id=\d+;")]
    private static partial Regex HyperlinkId();

    [GeneratedRegex(@"(?:\u001b|\\e)\[[0-9;]*m")]
    private static partial Regex GraphicsSequence();
}
