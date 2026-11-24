using System.Reflection;
using NetPace.Console.Theming;
using Spectre.Console;
using Spectre.Console.Testing;

namespace NetPace.Console.Tests.Theming;

public sealed class ThemeTests
{
    /// <summary>
    /// The contrast ratio a themed colour must clear against both terminal backgrounds: the WCAG
    /// floor for large text and user-interface parts.
    /// </summary>
    private const double ContrastFloor = 3.0;

    [InlineData("default")]
    [InlineData("DEFAULT")]
    [InlineData("Default")]
    [InlineData("plain")]
    [InlineData("PLAIN")]
    [InlineData("green")]
    [InlineData("GrEeN")]
    [InlineData("halloween")]
    [InlineData("HALLOWEEN")]
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
    [InlineData("grene")]
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
        Assert.Equal(["default", "plain", "green", "halloween"], names);
        foreach (string name in names)
        {
            Assert.True(Theme.TryParse(name, out _));
            Assert.Equal(name.ToLowerInvariant(), name);
        }
        Assert.Equal(names.Length, names.Distinct().Count());
        Assert.Equal("<default, plain, green, halloween>", Theme.NameList);
    }

    [Fact]
    public void Styles_ListsEveryStyleSlotTheThemeDeclares()
    {
        // Given the style slots the type declares, counted without reading the hand-written list
        int declared = typeof(Theme)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Count(property => property.PropertyType == typeof(Style));

        // Then the list the whole-theme sweeps read covers all of them. Without this, a slot
        // added to the type and forgotten here would drop out of every sweep and none would fail.
        Assert.Equal(declared, Theme.Default.Styles.Count);
    }

    [Fact]
    public void PlainTheme_AsksForNoColourAndNoEmphasis()
    {
        // Given, When
        var theme = Theme.Plain;

        // Then
        Assert.Equal("plain", theme.Name);
        Assert.Equal(Color.Default, theme.BannerNet);
        Assert.Equal(Color.Default, theme.BannerPace);
        foreach (var style in theme.Styles)
        {
            Assert.Equal(Color.Default, style.Foreground);
            Assert.Equal(Color.Default, style.Background);
            Assert.Equal(Decoration.None, style.Decoration);
        }
    }

    [MemberData(nameof(ThemeRows.ColouredNames), MemberType = typeof(ThemeRows))]
    [Theory]
    public void AColouredTheme_DrawsEveryElementDifferentlyFromTheDefaultTheme(string name)
    {
        // Given
        var theme = ThemeRows.Named(name);

        // Then no element is left at the default theme's colour, so every line is drawn in the
        // chosen theme rather than only the elements that happened to be easy to change.
        Assert.NotEqual(Theme.Default.BannerNet, theme.BannerNet);
        Assert.NotEqual(Theme.Default.BannerPace, theme.BannerPace);
        for (var slot = 0; slot < theme.Styles.Count; slot++)
        {
            Assert.NotEqual(Theme.Default.Styles[slot], theme.Styles[slot]);
        }
    }

    [MemberData(nameof(ThemeRows.ColouredNames), MemberType = typeof(ThemeRows))]
    [Theory]
    public void AColouredTheme_DrawsTheTwoHalvesOfTheHelpLetteringInDifferentColours(string name)
    {
        // Given
        var theme = ThemeRows.Named(name);

        // Then both halves carry a colour of their own and the two differ, which is the whole of
        // what makes the lettering read as two words rather than one.
        Assert.NotEqual(Color.Default, theme.BannerNet);
        Assert.NotEqual(Color.Default, theme.BannerPace);
        Assert.NotEqual(theme.BannerNet, theme.BannerPace);
    }

    /// <summary>
    /// The colours a theme is allowed to draw that read on a black terminal and wash out on a
    /// white one, with what each is and why it is let off.
    /// </summary>
    /// <remarks>
    /// The green theme's lettering needs a pair of greens far enough apart to read as two words,
    /// which two mid-tones are not. Documented under "Choosing a colour theme" in USER_GUIDE.md.
    /// </remarks>
    public static TheoryData<string, string> LightBackgroundExemptions() => new()
    {
        { "green", nameof(Theme.BannerPace) }
    };

    /// <summary>
    /// The colour a theme carries in the slot named here, read by name so an exemption row can name
    /// a slot and both tests below read the same colour from it.
    /// </summary>
    private static Color ColourOf(Theme theme, string slot) =>
        (Color)typeof(Theme).GetProperty(slot)!.GetValue(theme)!;

    [MemberData(nameof(ThemeRows.ColouredNames), MemberType = typeof(ThemeRows))]
    [Theory]
    public void AColouredTheme_ReadsOnABlackTerminalAndAWhiteOneAlike(string name)
    {
        // Given every colour the theme draws in, the help lettering among them
        var theme = ThemeRows.Named(name);
        var colours = theme.Styles
            .SelectMany(style => new[] { style.Foreground, style.Background })
            .Append(theme.BannerNet)
            .Append(theme.BannerPace)
            .Where(colour => colour != Color.Default)
            .Distinct()
            .ToArray();

        // and the colours this theme is let off the white-background half of the test, read from
        // the slots they are named against so a slot moved to another colour carries its exemption
        var exempt = LightBackgroundExemptions()
            .Where(row => (string)row[0] == name)
            .Select(row => ColourOf(theme, (string)row[1]))
            .ToArray();

        // Then each clears the contrast floor against pure black, and each but the exempted ones
        // clears it against pure white, so the palette reads on either terminal background rather
        // than only the one it was picked on.
        Assert.NotEmpty(colours);
        foreach (var colour in colours)
        {
            double againstBlack = ContrastRatio(colour, Color.Black);
            Assert.True(againstBlack >= ContrastFloor, $"#{colour.ToHex()} against black is {againstBlack:F2}:1");
        }

        foreach (var colour in colours.Except(exempt))
        {
            double againstWhite = ContrastRatio(colour, Color.White);
            Assert.True(againstWhite >= ContrastFloor, $"#{colour.ToHex()} against white is {againstWhite:F2}:1");
        }
    }

    [MemberData(nameof(LightBackgroundExemptions))]
    [Theory]
    public void AnExemptedColour_IsOneThatWouldOtherwiseFailTheWhiteBackgroundTest(string name, string slot)
    {
        // Given a colour let off reading on a white terminal
        var colour = ColourOf(ThemeRows.Named(name), slot);

        // Then the exemption is one the palette still needs, so a stale one fails here rather than
        // quietly outliving the colour it was granted for.
        Assert.True(ContrastRatio(colour, Color.White) < ContrastFloor, $"#{colour.ToHex()} no longer needs its exemption");
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

    [MemberData(nameof(ThemeRows.InColourNames), MemberType = typeof(ThemeRows))]
    [Theory]
    public void ApplyTo_AColouredTheme_LeavesTheConsoleAsItFoundIt(string name)
    {
        // Given
        using var console = new TestConsole().Colors(ColorSystem.TrueColor);
        var theme = ThemeRows.Named(name);

        // When
        theme.ApplyTo(console);

        // Then
        Assert.Equal(ColorSystem.TrueColor, console.Profile.Capabilities.ColorSystem);
    }

    /// <summary>
    /// How far apart two colours are to read, by the WCAG contrast-ratio formula: 1:1 for two
    /// identical colours and 21:1 for black against white.
    /// </summary>
    private static double ContrastRatio(Color one, Color other)
    {
        double first = RelativeLuminance(one);
        double second = RelativeLuminance(other);

        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }

    /// <summary>The WCAG relative luminance of <paramref name="colour"/>, from 0 for black to 1 for white.</summary>
    private static double RelativeLuminance(Color colour) =>
        (0.2126 * Channel(colour.R)) + (0.7152 * Channel(colour.G)) + (0.0722 * Channel(colour.B));

    /// <summary>One colour channel, gamma-corrected as the WCAG formula asks.</summary>
    private static double Channel(byte value)
    {
        double channel = value / 255.0;

        return channel <= 0.03928 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
    }
}
