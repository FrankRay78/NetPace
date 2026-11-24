using System.Text;
using Spectre.Console.Rendering;

namespace NetPace.Console;

public sealed class FileConsole : IAnsiConsole, IPersistentConsole, IDisposable
{
    private static readonly UTF8Encoding Utf8NoByteOrderMark = new(encoderShouldEmitUTF8Identifier: false);

    private readonly StreamWriter _fileWriter;
    private readonly IAnsiConsole _templateConsole;
    private readonly object _writeLock = new();
    private bool _disposed;

    // startedEmpty must be read before the file is opened, because opening it for append is itself
    // about to add content. A file that started empty is replaced rather than appended to, so a
    // stray line ending, or a byte-order mark the file already carries, is not left above the first
    // line written.
    public FileConsole(string filePath, FileMode fileMode, bool startedEmpty)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        StartedEmpty = startedEmpty;

        // UTF-8 with no byte-order mark: a strict JSON parser rejects a document behind one, and
        // Unix text tools read one as part of the first field. Encoding.UTF8 emits one, so it is
        // not what this writer is given
        // (docs/change-intent-records/2026-11-02-file-output-carries-no-byte-order-mark.md).
        _fileWriter = new StreamWriter(filePath, append: (fileMode == FileMode.Append && !startedEmpty), Utf8NoByteOrderMark) { AutoFlush = true };

        var settings = new AnsiConsoleSettings
        {
            Out = new FileConsoleOutput(_fileWriter),
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No
        };

        _templateConsole = AnsiConsole.Create(settings);
    }

    /// <inheritdoc/>
    public bool StartedEmpty { get; }

    public void Dispose()
    {
        lock (_writeLock)
        {
            if (_disposed) return;
            _disposed = true;
            _fileWriter?.Flush();
            _fileWriter?.Dispose();
        }
    }

    public Spectre.Console.Profile Profile => _templateConsole.Profile;
    public IAnsiConsoleCursor Cursor => _templateConsole.Cursor;
    public IAnsiConsoleInput Input => _templateConsole.Input;
    public IExclusivityMode ExclusivityMode => _templateConsole.ExclusivityMode;
    public RenderPipeline Pipeline => _templateConsole.Pipeline;

    public void Write(IRenderable renderable)
    {
        // Rendered outside the lock: GetSegments has no side-effects on the writer.
        var builder = new StringBuilder();
        var segments = renderable.GetSegments(_templateConsole);
        foreach (var segment in segments)
        {
            builder.Append(segment.Text);
        }

        var text = builder.ToString();

        // Guard against Spectre.Console's background ProgressRefreshThread firing after disposal.
        lock (_writeLock)
        {
            if (_disposed) return;
            _fileWriter.Write(text);
        }
    }

    // Nothing to clear in a file.
    public void Clear(bool home)
    {
    }

    // File output is plain text, so a raw escape sequence is dropped rather than written.
    public void WriteAnsi(Action<AnsiWriter> action)
    {
    }

    // Reports itself to Spectre.Console as a non-terminal of fixed dimensions.
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
            // Encoding is set on StreamWriter creation, and the request is ignored deliberately:
            // honouring one for Encoding.UTF8 would put a byte-order mark back in front of the
            // content.
        }
    }
}
