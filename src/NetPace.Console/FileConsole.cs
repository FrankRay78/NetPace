using System.Text;
using Spectre.Console.Rendering;

namespace NetPace.Console;

/// <summary>
/// An IAnsiConsole implementation that writes output only to a file.
/// This is a single-purpose console for file output only.
/// </summary>
public sealed class FileConsole : IAnsiConsole, IDisposable
{
    private readonly StreamWriter _fileWriter;
    private readonly IAnsiConsole _templateConsole;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileConsole"/> class.
    /// </summary>
    /// <param name="filePath">The path to the output file.</param>
    /// <param name="fileMode">Determines whether to append to or overwrite the file.</param>
    public FileConsole(string filePath, FileMode fileMode)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        _fileWriter = new StreamWriter(filePath, append: (fileMode == FileMode.Append), Encoding.UTF8) { AutoFlush = true };

        // Create console settings for plain text output
        var settings = new AnsiConsoleSettings
        {
            Out = new FileConsoleOutput(_fileWriter),
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No
        };

        _templateConsole = AnsiConsole.Create(settings);
    }

    /// <summary>
    /// Flushes and closes the file writer.
    /// </summary>
    public void Dispose()
    {
        _fileWriter?.Flush();
        _fileWriter?.Dispose();
    }

    // IAnsiConsole implementation - delegate to template console for properties
    public Profile Profile => _templateConsole.Profile;
    public IAnsiConsoleCursor Cursor => _templateConsole.Cursor;
    public IAnsiConsoleInput Input => _templateConsole.Input;
    public IExclusivityMode ExclusivityMode => _templateConsole.ExclusivityMode;
    public RenderPipeline Pipeline => _templateConsole.Pipeline;

    /// <summary>
    /// Writes text to the file.
    /// </summary>
    public void Write(IRenderable renderable)
    {
        // Capture the output as plain text using template console for rendering
        var builder = new StringBuilder();
        var segments = renderable.GetSegments(_templateConsole);
        foreach (var segment in segments)
        {
            builder.Append(segment.Text);
        }

        var text = builder.ToString();
        _fileWriter.Write(text);
    }

    /// <summary>
    /// Clear operations are ignored for file output.
    /// </summary>
    public void Clear(bool home)
    {
    }

    /// <summary>
    /// Custom IAnsiConsoleOutput implementation that writes to a file.
    /// Configures Spectre.Console to treat the output as a non-terminal with fixed dimensions.
    /// </summary>
    private sealed class FileConsoleOutput : IAnsiConsoleOutput
    {
        private readonly TextWriter _writer;

        public FileConsoleOutput(TextWriter writer)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        public TextWriter Writer => _writer;
        public bool IsTerminal => false;
        public int Width => int.MaxValue;
        public int Height => int.MaxValue;

        public void SetEncoding(Encoding encoding)
        {
            // Encoding is set on StreamWriter creation
        }
    }
}
