using System.Text;
using Spectre.Console.Rendering;

namespace NetPace.Console;

/// <summary>
/// An IAnsiConsole implementation that writes output only to a file, suppressing console output.
/// Used for quiet mode with file output.
/// </summary>
public sealed class FileOnlyConsole : IAnsiConsole
{
    private readonly IAnsiConsole _inner;
    private readonly StreamWriter _fileWriter;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileOnlyConsole"/> class.
    /// </summary>
    /// <param name="inner">The console to wrap (used for property delegation only).</param>
    /// <param name="fileWriter">The stream writer for file output.</param>
    public FileOnlyConsole(IAnsiConsole inner, StreamWriter fileWriter)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _fileWriter = fileWriter ?? throw new ArgumentNullException(nameof(fileWriter));
    }

    /// <summary>
    /// Flushes and closes the file writer.
    /// </summary>
    public void Dispose()
    {
        _fileWriter?.Flush();
        _fileWriter?.Dispose();
    }

    // Forward all IAnsiConsole properties to inner console
    public Profile Profile => _inner.Profile;
    public IAnsiConsoleCursor Cursor => _inner.Cursor;
    public IAnsiConsoleInput Input => _inner.Input;
    public IExclusivityMode ExclusivityMode => _inner.ExclusivityMode;
    public RenderPipeline Pipeline => _inner.Pipeline;

    /// <summary>
    /// Writes text to file only, suppressing console output.
    /// </summary>
    public void Write(IRenderable renderable)
    {
        // Capture the output
        var builder = new StringBuilder();
        var segments = renderable.GetSegments(_inner);
        foreach (var segment in segments)
        {
            builder.Append(segment.Text);
        }

        var text = builder.ToString();
        _fileWriter.Write(text);

        // Do NOT write to console - quiet mode
    }

    /// <summary>
    /// Suppresses clear operations (does nothing).
    /// </summary>
    public void Clear(bool home)
    {
        // Do not clear console in quiet mode
    }
}
