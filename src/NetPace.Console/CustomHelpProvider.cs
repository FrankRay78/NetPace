using System.CommandLine;
using NetPace.Console.Theming;

namespace NetPace.Console;

internal static class CustomHelpProvider
{
    // Split at the column where Net ends and Pace begins, so each half can be drawn in its own
    // colour. ASCII alone: the quadrant-block lettering this replaced came apart wherever the font
    // in use lacked those characters and a fallback of different metrics supplied them.
    private static readonly (string Net, string Pace)[] WordmarkRows =
    [
        (@" _  _       _   ", @" ___"),
        (@"| \| | ___ | |_ ", @"| _ \ __ _  __  ___"),
        (@"| .` |/ -_)|  _|", @"|  _// _` |/ _|/ -_)"),
        (@"|_|\_|\___| \__|", @"|_|  \__,_|\__|\___|")
    ];

    // A Text rather than "" because a grid row takes renderables or strings, never a mix.
    private static Text Blank => new(string.Empty);

    public static void RenderHelp(IAnsiConsole console, RootCommand command, Theme theme)
    {
        RenderHeader(console, theme);
        RenderDescription(console, command, theme);
        RenderUsage(console, command, theme);
        RenderOptions(console, command, theme);
        RenderSubcommands(console, command, theme);
        RenderEnvironment(console, theme);
        RenderFooter(console, theme);
    }

    public static void RenderHelp(IAnsiConsole console, Command command, Theme theme)
    {
        RenderHeader(console, theme);
        RenderDescription(console, command, theme);
        RenderUsage(console, command, theme);
        RenderOptions(console, command, theme);
        RenderFooter(console, theme);
    }

    private static void RenderHeader(IAnsiConsole console, Theme theme)
    {
        console.WriteLine();

        var netStyle = new Style(theme.BannerNet);
        var paceStyle = new Style(theme.BannerPace);

        foreach (var (net, pace) in WordmarkRows)
        {
            console.WriteStyledLine((net, netStyle), (pace, paceStyle));
        }

        console.WriteLine();
    }

    private static void RenderDescription(IAnsiConsole console, Command command, Theme theme)
    {
        if (!string.IsNullOrWhiteSpace(command.Description))
        {
            console.WriteLine("DESCRIPTION:", theme.Heading);
            console.WriteLine(command.Description);
            console.WriteLine();
        }
    }

    private static void RenderUsage(IAnsiConsole console, Command command, Theme theme)
    {
        console.WriteLine("USAGE:", theme.Heading);
        if (command is RootCommand)
        {
            console.WriteStyledLine(
                ("    netpace ", Style.Plain),
                ("[OPTIONS]", theme.Placeholder),
                (" ", Style.Plain),
                ("[COMMAND]", theme.CommandName));
        }
        else
        {
            console.WriteStyledLine(
                ($"    netpace {command.Name} ", Style.Plain),
                ("[OPTIONS]", theme.Placeholder));
        }
        console.WriteLine();
    }

    private static void RenderOptions(IAnsiConsole console, Command command, Theme theme)
    {
        if (command.Options.Count == 0)
        {
            return;
        }

        console.WriteLine("OPTIONS:", theme.Heading);

        var sortedOptions = command.Options.ToList();

        // Drop System.CommandLine's auto-generated --version, matched on its description, and keep
        // NetPace's own.
        sortedOptions.RemoveAll(o => o.Name == "--version" && (o.Description?.Contains("Show version information") ?? false));

        // Hidden options are registered only so that passing them produces a useful error.
        sortedOptions.RemoveAll(o => o.Hidden);

        var helpOption = sortedOptions.FirstOrDefault(o => o.Name == "--help");
        if (helpOption != null)
        {
            sortedOptions.Remove(helpOption);
        }

        // Added for display only: help is intercepted before System.CommandLine registers its own.
        var helpOptionForDisplay = new Option<bool>("--help")
        {
            Description = "Prints help information."
        };
        helpOptionForDisplay.Aliases.Add("-h");
        sortedOptions.Insert(0, helpOptionForDisplay);

        var hasDefaults = sortedOptions.Any(o => !string.IsNullOrEmpty(GetDefaultValue(o)));

        // Paddings copied from Spectre.Console.Cli, so this help lines up with the help NetPace
        // rendered before it moved off that library.
        var grid = new Grid();
        grid.AddColumn(new GridColumn { Padding = new Padding(4, 4), NoWrap = true });

        if (hasDefaults)
        {
            grid.AddColumn(new GridColumn { Padding = new Padding(0, 0, 4, 0) });
        }

        grid.AddColumn(new GridColumn { Padding = new Padding(0, 0) });

        if (hasDefaults)
        {
            grid.AddRow(Blank, new Text("DEFAULT", theme.DefaultColumnHeading), Blank);
        }

        foreach (var option in sortedOptions)
        {
            var description = option.Name == "--help" ? "Prints help information." : (option.Description ?? "");

            var shortAliases = option.Aliases
                .Where(a => a.StartsWith("-") && !a.StartsWith("--") && a != option.Name && !a.StartsWith("/"))
                .ToList();

            var longName = option.Name;

            string displayName;
            if (shortAliases.Count > 0)
            {
                // -h in preference to -?.
                string primaryAlias;
                if (longName == "--help" && shortAliases.Contains("-h"))
                {
                    primaryAlias = "-h";
                }
                else
                {
                    primaryAlias = shortAliases.OrderBy(a => a).First();
                }

                displayName = primaryAlias + ", " + longName;
            }
            else
            {
                // Indented, so the long name lines up under the aliased ones.
                displayName = "    " + longName;
            }

            var defaultValue = GetDefaultValue(option);
            var descriptionLines = description.Split('\n');

            if (hasDefaults)
            {
                grid.AddRow(new Text(displayName, theme.OptionName), new Text(defaultValue, theme.DefaultValue), new Text(descriptionLines[0]));

                for (int i = 1; i < descriptionLines.Length; i++)
                {
                    grid.AddRow(Blank, Blank, new Text(descriptionLines[i]));
                }
            }
            else
            {
                grid.AddRow(new Text(displayName, theme.OptionName), new Text(descriptionLines[0]));

                for (int i = 1; i < descriptionLines.Length; i++)
                {
                    grid.AddRow(Blank, new Text(descriptionLines[i]));
                }
            }
        }

        console.Write(grid);
        console.WriteLine();
    }

    private static string GetDefaultValue(Option option)
    {
        try
        {
            var defaultValue = option.GetDefaultValue();

            return defaultValue switch
            {
                null => "",
                "" => "",
                bool boolValue => boolValue ? "True" : "",
                int intValue when intValue == int.MaxValue => "",
                int intValue when intValue == 0 => "",
                int intValue when intValue == 1 => "",
                TimeSpan ts when ts == TimeSpan.Zero => "",
                _ => OptionValues.Spell(defaultValue)
            };
        }
        catch (Exception)
        {
            return "";
        }
    }

    private static void RenderSubcommands(IAnsiConsole console, RootCommand command, Theme theme)
    {
        if (command.Subcommands.Count == 0)
        {
            return;
        }

        console.WriteLine("COMMANDS:", theme.Heading);

        foreach (var subcommand in command.Subcommands.OrderBy(c => c.Name))
        {
            var description = subcommand.Description ?? "";
            console.WriteStyledLine(
                ("    ", Style.Plain),
                ($"{subcommand.Name,-11}", theme.CommandName),
                (description, Style.Plain));
        }

        console.WriteLine();
    }

    // Root help only: a variable is not a property of one command.
    private static void RenderEnvironment(IAnsiConsole console, Theme theme)
    {
        console.WriteLine("ENVIRONMENT:", theme.Heading);
        console.WriteStyledLine(
            ("    ", Style.Plain),
            (EnvironmentThemeProvider.ThemeVariable, theme.OptionName),
            ($"  Colour theme to draw in. {Theme.NameList}", Style.Plain));
        console.WriteLine();
    }

    private static void RenderFooter(IAnsiConsole console, Theme theme)
    {
        const string userGuideUrl = "https://github.com/FrankRay78/NetPace/blob/main/USER_GUIDE.md";

        console.WriteLine("SEE ALSO:", theme.Heading);
        console.MarkupLine($"    [link={userGuideUrl}]{userGuideUrl}[/]");
    }
}
