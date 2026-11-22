using Spectre.Console.Rendering;

namespace NetPace.Console;

/// <summary>
/// An IAnsiConsole implementation that delegates to multiple inner consoles.
/// Useful for writing output to multiple destinations (e.g., terminal + file).
/// </summary>
/// <remarks>
/// A composite borrows its destinations and owns none of them, so it is not disposable: whoever
/// opened a destination closes it. A composite that closed its destinations would close the
/// terminal console it was given as well as the file, and that console is one the caller was
/// handed and goes on using.
/// </remarks>
public sealed class CompositeAnsiConsole : IAnsiConsole
{
    private readonly IReadOnlyList<IAnsiConsole> _consoles;
    private readonly IAnsiConsole _primary;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeAnsiConsole"/> class.
    /// </summary>
    /// <param name="consoles">The list of consoles to delegate to. Empty list means no output (quiet mode).</param>
    public CompositeAnsiConsole(IAnsiConsole primary, params IAnsiConsole[] consoles)
    {
        _consoles = consoles ?? Array.Empty<IAnsiConsole>();
        _primary = primary;
    }

    /// <summary>
    /// The destinations this console writes to, in the order they were given. Exposed so a format
    /// that sends one line to some destinations and withholds it from others — the CSV header line,
    /// which a log already holding rows does not need — can address each on its own. Which
    /// destinations are present depends on the run: <c>--quiet</c> drops the terminal, so judge a
    /// destination by what it is and never by its position here.
    /// </summary>
    internal IReadOnlyList<IAnsiConsole> Destinations => _consoles;

    // Delegate properties to primary console
    public Spectre.Console.Profile Profile => _primary.Profile;
    public IAnsiConsoleCursor Cursor => _primary.Cursor;
    public IAnsiConsoleInput Input => _primary.Input;
    public IExclusivityMode ExclusivityMode => _primary.ExclusivityMode;
    public RenderPipeline Pipeline => _primary.Pipeline;

    /// <summary>
    /// Writes to all inner consoles.
    /// </summary>
    public void Write(IRenderable renderable)
    {
        foreach (var console in _consoles)
        {
            console.Write(renderable);
        }
    }

    /// <summary>
    /// Clears all inner consoles.
    /// </summary>
    public void Clear(bool home)
    {
        foreach (var console in _consoles)
        {
            console.Clear(home);
        }
    }

    /// <summary>
    /// Writes raw ANSI to all inner consoles.
    /// </summary>
    public void WriteAnsi(Action<AnsiWriter> action)
    {
        foreach (var console in _consoles)
        {
            console.WriteAnsi(action);
        }
    }
}
