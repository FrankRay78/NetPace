namespace Spectre.Console.Extensions;

public static class RedirectionFriendlyConsole
{
    public static IAnsiConsole Out { get; } = CreateRedirectionFriendlyConsole(System.Console.Out);
    public static IAnsiConsole Error { get; } = CreateRedirectionFriendlyConsole(System.Console.Error);

    private static IAnsiConsole CreateRedirectionFriendlyConsole(TextWriter textWriter)
    {
        var output = new RedirectionFriendlyAnsiConsoleOutput(new AnsiConsoleOutput(textWriter));
        var settings = new AnsiConsoleSettings
        {
            Out = output,
            Ansi = output.IsTerminal ? AnsiSupport.Detect : AnsiSupport.No,
        };
        return AnsiConsole.Create(settings);
    }

    private sealed class RedirectionFriendlyAnsiConsoleOutput(IAnsiConsoleOutput ansiConsoleOutput) : IAnsiConsoleOutput
    {
        public TextWriter Writer => ansiConsoleOutput.Writer;
        public bool IsTerminal => ansiConsoleOutput.IsTerminal;
        public int Width => IsTerminal ? ansiConsoleOutput.Width : 320;
        public int Height => IsTerminal ? ansiConsoleOutput.Height : 240;
        public void SetEncoding(Encoding encoding) => ansiConsoleOutput.SetEncoding(encoding);
    }
}