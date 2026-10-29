using System.CommandLine;

namespace NetPace.Console.Tests;

public sealed class OptionValueSetTests
{
    [Fact]
    public void Parse_ValueOmittedOnAnOptionalValueOption_ListsTheValidValues()
    {
        // Given — an option whose value may be omitted. That arity is the one that hands a custom
        // parser no token at all, and --verbosity already uses it.
        var option = new Option<SpeedUnit>("--unit")
        {
            Arity = ArgumentArity.ZeroOrOne,
            CustomParser = result => OptionValues.Units.Parse(result, "--unit")
        };
        var command = new RootCommand();
        command.Options.Add(option);

        // When
        var result = command.Parse("--unit");

        // Then
        Assert.Contains("<bits, bytes>", Assert.Single(result.Errors).Message);
    }
}
