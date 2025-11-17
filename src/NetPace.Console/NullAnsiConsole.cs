using Spectre.Console.Rendering;

namespace NetPace.Console;

/// <summary>
/// An IAnsiConsole implementation that suppresses all console output.
/// Used for quiet mode where output should be suppressed.
/// </summary>
public sealed class NullAnsiConsole : IAnsiConsole
{
    private readonly IAnsiConsole _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="NullAnsiConsole"/> class.
    /// </summary>
    /// <param name="inner">The console to wrap (used for property delegation only).</param>
    public NullAnsiConsole(IAnsiConsole inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    // Forward properties to inner console (needed for API compatibility)
    public Profile Profile => _inner.Profile;
    public IAnsiConsoleCursor Cursor => _inner.Cursor;
    public IAnsiConsoleInput Input => _inner.Input;
    public IExclusivityMode ExclusivityMode => _inner.ExclusivityMode;
    public RenderPipeline Pipeline => _inner.Pipeline;

    /// <summary>
    /// Suppresses all write operations (does nothing).
    /// </summary>
    public void Write(IRenderable renderable)
    {
        // Suppress output - do nothing
    }

    /// <summary>
    /// Suppresses clear operations (does nothing).
    /// </summary>
    public void Clear(bool home)
    {
        // Suppress clear - do nothing
    }
}
