using System.CommandLine;

namespace NetPace.Console.Tests;

public sealed class DurationOptionTests
{
    [Fact]
    public void Parse_ValueOmittedOnAnOptionalValueOption_ReportsTheOptionNeedsAValue()
    {
        // Given an option whose value may be omitted. That arity is the one that hands a custom
        // parser no token at all.
        var option = new Option<TimeSpan>("--time-limit")
        {
            Arity = ArgumentArity.ZeroOrOne,
            CustomParser = result => DurationOption.Parse(result, "--time-limit", allowZero: false)
        };
        var command = new RootCommand();
        command.Options.Add(option);

        // When
        var result = command.Parse("--time-limit");

        // Then
        string message = Assert.Single(result.Errors).Message;
        Assert.Contains("--time-limit", message, StringComparison.Ordinal);
        Assert.Contains("10m", message, StringComparison.Ordinal);
    }
}
