using System.Reflection;
using NetPace.Console.Theming;
using Spectre.Console;
using Spectre.Console.Testing;

namespace NetPace.Console.Tests.Theming;

public sealed class ThemeTests
{
    [InlineData("default")]
    [InlineData("DEFAULT")]
    [InlineData("Default")]
    [InlineData("plain")]
    [InlineData("PLAIN")]
    [InlineData("green")]
    [InlineData("GrEeN")]
    [Theory]
    public void TryParse_ANameInAnyCase_SelectsThatTheme(string requested)
    {
        // Given, When
        var recognised = Theme.TryParse(requested, out var theme);

        // Then
        Assert.True(recognised);
        Assert.Equal(requested.ToLowerInvariant(), theme.Name);
    }

    [InlineData(null)]
    [InlineData("")]
    [Theory]
    public void TryParse_NothingAsked_SelectsTheDefaultTheme(string? requested)
    {
        // Given, When
        var recognised = Theme.TryParse(requested, out var theme);

        // Then
        Assert.True(recognised);
        Assert.Same(Theme.Default, theme);
    }

    [InlineData("purple")]
    [InlineData("halloween")]
    [InlineData(" green ")]
    [Theory]
    public void TryParse_ANameNoThemeCarries_FallsBackToTheDefaultTheme(string requested)
    {
        // Given, When
        var recognised = Theme.TryParse(requested, out var theme);

        // Then the caller can tell the value was not understood, and still has a theme to draw in.
        Assert.False(recognised);
        Assert.Same(Theme.Default, theme);
    }

    [Fact]
    public void NameList_NamesEveryThemeThatCanBeChosen()
    {
        // Given, When
        var names = Theme.All.Select(theme => theme.Name).ToArray();

        // Then every name offered to the user is one TryParse accepts, and none is missing.
        Assert.Equal(["default", "plain", "green"], names);
        foreach (string name in names)
        {
            Assert.True(Theme.TryParse(name, out _));
            Assert.Equal(name.ToLowerInvariant(), name);
        }
        Assert.Equal(names.Length, names.Distinct().Count());
        Assert.Equal("<default, plain, green>", Theme.NameList);
    }

    [Fact]
    public void Styles_ListsEveryStyleSlotTheThemeDeclares()
    {
        // Given the style slots the type declares, counted without reading the hand-written list
        int declared = typeof(Theme)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Count(property => property.PropertyType == typeof(Style));

        // Then the list the two whole-theme sweeps read covers all of them. Without this, a slot
        // added to the type and forgotten here would drop out of both sweeps and neither would fail.
        Assert.Equal(declared, Theme.Default.Styles.Count);
    }

    [Fact]
    public void PlainTheme_AsksForNoColourAndNoEmphasis()
    {
        // Given, When
        var theme = Theme.Plain;

        // Then
        Assert.Equal("plain", theme.Name);
        Assert.Equal(Color.Default, theme.Banner);
        foreach (var style in theme.Styles)
        {
            Assert.Equal(Color.Default, style.Foreground);
            Assert.Equal(Color.Default, style.Background);
            Assert.Equal(Decoration.None, style.Decoration);
        }
    }

    [Fact]
    public void GreenTheme_DrawsEveryElementDifferentlyFromTheDefaultTheme()
    {
        // Given
        var green = Theme.Green;
        var @default = Theme.Default;

        // Then no element is left at the default theme's colour, so the difference shows on every
        // line rather than only on the elements that happened to be easy to change.
        Assert.NotEqual(@default.Banner, green.Banner);
        for (var slot = 0; slot < green.Styles.Count; slot++)
        {
            Assert.NotEqual(@default.Styles[slot], green.Styles[slot]);
        }
    }

    [Fact]
    public void ApplyTo_ThePlainTheme_LeavesTheConsoleUnableToDrawColour()
    {
        // Given
        using var console = new TestConsole().Colors(ColorSystem.TrueColor);

        // When
        Theme.Plain.ApplyTo(console);

        // Then
        Assert.Equal(ColorSystem.NoColors, console.Profile.Capabilities.ColorSystem);
    }

    [InlineData("default")]
    [InlineData("green")]
    [Theory]
    public void ApplyTo_AColouredTheme_LeavesTheConsoleAsItFoundIt(string name)
    {
        // Given
        using var console = new TestConsole().Colors(ColorSystem.TrueColor);
        Theme.TryParse(name, out var theme);

        // When
        theme.ApplyTo(console);

        // Then
        Assert.Equal(ColorSystem.TrueColor, console.Profile.Capabilities.ColorSystem);
    }
}
