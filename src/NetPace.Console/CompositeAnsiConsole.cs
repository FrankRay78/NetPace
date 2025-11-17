using Spectre.Console.Rendering;

namespace NetPace.Console;

/// <summary>
/// An IAnsiConsole implementation that delegates to multiple inner consoles.
/// Useful for writing output to multiple destinations (e.g., terminal + file).
/// </summary>
public sealed class CompositeAnsiConsole : IAnsiConsole, IDisposable
{
    private readonly IReadOnlyList<IAnsiConsole> _consoles;
    private readonly IAnsiConsole _primary;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeAnsiConsole"/> class.
    /// </summary>
    /// <param name="consoles">The list of consoles to delegate to. Empty list means no output (quiet mode).</param>
    public CompositeAnsiConsole(params IAnsiConsole[] consoles)
    {
        _consoles = consoles ?? Array.Empty<IAnsiConsole>();

        // Use first console as primary, or create a minimal console for quiet mode
        _primary = _consoles.Count > 0 ? _consoles[0] : AnsiConsole.Create(new AnsiConsoleSettings());
    }

    /// <summary>
    /// Disposes all inner consoles that implement IDisposable.
    /// </summary>
    public void Dispose()
    {
        foreach (var console in _consoles)
        {
            if (console is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    // Delegate properties to primary console
    public Profile Profile => _primary.Profile;
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
}
