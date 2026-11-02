using NetPace.Console.Theming;

namespace NetPace.Console.Tests.Theming;

public sealed class EnvironmentThemeProviderTests
{
    /// <summary>
    /// A provider reading the variables given here and nothing else, so a test says what the
    /// environment holds without changing the environment the rest of the suite runs in.
    /// </summary>
    private static EnvironmentThemeProvider Reading(params (string Name, string Value)[] variables) =>
        new(name => variables.Where(v => v.Name == name).Select(v => v.Value).FirstOrDefault());

    [Fact]
    public void Resolve_NoThemeVariable_SelectsTheDefaultThemeAndSaysNothing()
    {
        // Given, When
        var selection = Reading().Resolve();

        // Then
        Assert.Same(Theme.Default, selection.Theme);
        Assert.Null(selection.UnrecognisedValue);
    }

    [InlineData("green", "green")]
    [InlineData("GREEN", "green")]
    [InlineData("plain", "plain")]
    [InlineData("", "default")]
    [Theory]
    public void Resolve_ThemeVariableNamesATheme_SelectsIt(string requested, string expected)
    {
        // Given, When
        var selection = Reading(("NETPACE_THEME", requested)).Resolve();

        // Then
        Assert.Equal(expected, selection.Theme.Name);
        Assert.Null(selection.UnrecognisedValue);
    }

    [Fact]
    public void Resolve_ThemeVariableNamesNoTheme_SelectsTheDefaultThemeAndReportsTheValue()
    {
        // Given, When
        var selection = Reading(("NETPACE_THEME", "halloween")).Resolve();

        // Then the value is handed back so the run can tell the user, rather than silently ignored.
        Assert.Same(Theme.Default, selection.Theme);
        Assert.Equal("halloween", selection.UnrecognisedValue);
    }

    [InlineData("green")]
    [InlineData("default")]
    [InlineData("")]
    [Theory]
    public void Resolve_NoColourAsked_SelectsThePlainThemeWhateverTheThemeVariableSays(string requested)
    {
        // SCENARIO: NO_COLOR outranks the theme

        // Given, When
        var selection = Reading(("NETPACE_THEME", requested), ("NO_COLOR", "1")).Resolve();

        // Then
        Assert.Same(Theme.Plain, selection.Theme);
    }

    [Fact]
    public void Resolve_NoColourVariableEmpty_LeavesTheChosenThemeAlone()
    {
        // Given an empty NO_COLOR is how a user switches the request off again.

        // When
        var selection = Reading(("NETPACE_THEME", "green"), ("NO_COLOR", "")).Resolve();

        // Then
        Assert.Same(Theme.Green, selection.Theme);
    }

    [Fact]
    public void Resolve_NoColourAskedAndTheThemeNameIsWrong_StillReportsTheValue()
    {
        // Given, When
        var selection = Reading(("NETPACE_THEME", "halloween"), ("NO_COLOR", "1")).Resolve();

        // Then the user hears about the typo even though the theme they named would not have shown.
        Assert.Same(Theme.Plain, selection.Theme);
        Assert.Equal("halloween", selection.UnrecognisedValue);
    }
}
