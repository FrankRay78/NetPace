using System.Text.RegularExpressions;

namespace NetPace.Console.Tests;

/// <summary>
/// Turns captured terminal output into something a snapshot can hold and a reviewer can read, and
/// answers whether anything in it was styled.
/// </summary>
/// <remarks>
/// A colour-recording run captures real escape sequences, and an escape character in a committed
/// snapshot is invisible in a diff — a colour change would land as an unreadable blob of control
/// bytes. Spelling the escape as <c>\e</c> keeps the sequence legible, so a reviewer can see which
/// colour an element moved to.
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
    /// Reads the escape sequences the terminal is sent rather than the styles the code asked for,
    /// so it answers for whatever reaches the screen. Only the sequence that sets graphics —
    /// colour, bold, dim, italic, underline — counts: moving the cursor and clearing a line are how
    /// a live display redraws itself and say nothing about appearance, and a hyperlink is an
    /// address rather than a colour.
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
