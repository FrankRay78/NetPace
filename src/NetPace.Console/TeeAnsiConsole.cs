using System.Text;
using Spectre.Console.Rendering;

namespace NetPace.Console;

/// <summary>
/// An IAnsiConsole implementation that writes output to both the wrapped console and a file.
/// </summary>
public sealed class TeeAnsiConsole : IAnsiConsole
{
    private readonly IAnsiConsole _inner;
    private readonly StreamWriter _fileWriter;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeeAnsiConsole"/> class.
    /// </summary>
    /// <param name="inner">The console to wrap.</param>
    /// <param name="filePath">The path to the output file.</param>
    /// <param name="fileMode">Determines whether to append to or overwrite the file.</param>
    public TeeAnsiConsole(IAnsiConsole inner, string filePath, FileMode fileMode)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        bool append = fileMode == FileMode.Append;
        _fileWriter = new StreamWriter(filePath, append: append, Encoding.UTF8) { AutoFlush = true };
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
    /// Writes text to both console and file.
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

        // Also write to console
        _inner.Write(renderable);
    }

    /// <summary>
    /// Clears the console (console only, not written to file).
    /// </summary>
    public void Clear(bool home)
    {
        _inner.Clear(home);
    }
}
