using Spectre.Console;
using Spectre.Console.Rendering;

namespace NetPace.Console.Tests;

/// <summary>
/// Unit tests for <see cref="CompositeAnsiConsole"/> verifying it fans operations
/// out to every inner console.
/// </summary>
public sealed class CompositeAnsiConsoleTests
{
    [Fact]
    public void WriteAnsi_ForwardsActionToEveryInnerConsole()
    {
        // Given
        var primary = new SpyConsole();
        var secondary = new SpyConsole();
        var composite = new CompositeAnsiConsole(primary, primary, secondary);

        // When
        composite.WriteAnsi(_ => { });

        // Then
        Assert.Equal(1, primary.WriteAnsiCallCount);
        Assert.Equal(1, secondary.WriteAnsiCallCount);
    }

    [Fact]
    public void Write_ForwardsRenderableToEveryInnerConsole()
    {
        // Given
        var primary = new SpyConsole();
        var secondary = new SpyConsole();
        var composite = new CompositeAnsiConsole(primary, primary, secondary);

        // When
        composite.Write(new Markup("hello"));

        // Then
        Assert.Equal(1, primary.WriteCallCount);
        Assert.Equal(1, secondary.WriteCallCount);
    }

    private sealed class SpyConsole : IAnsiConsole
    {
        public int WriteCallCount { get; private set; }
        public int WriteAnsiCallCount { get; private set; }

        public Profile Profile => AnsiConsole.Console.Profile;
        public IAnsiConsoleCursor Cursor => AnsiConsole.Console.Cursor;
        public IAnsiConsoleInput Input => AnsiConsole.Console.Input;
        public IExclusivityMode ExclusivityMode => AnsiConsole.Console.ExclusivityMode;
        public RenderPipeline Pipeline => AnsiConsole.Console.Pipeline;

        public void Clear(bool home) { }
        public void Write(IRenderable renderable) => WriteCallCount++;
        public void WriteAnsi(Action<AnsiWriter> action) => WriteAnsiCallCount++;
    }
}
