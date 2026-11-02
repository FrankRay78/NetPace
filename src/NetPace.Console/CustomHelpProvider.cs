using System.CommandLine;
using System.Reflection;

using NetPace.Console.Theming;
using Spectre.Console.Rendering;

namespace NetPace.Console;

/// <summary>
/// Custom help provider for System.CommandLine that uses Spectre.Console for rendering.
/// </summary>
internal static class CustomHelpProvider
{
    /// <summary>
    /// Render help for the root command.
    /// </summary>
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

    /// <summary>
    /// Render help for a subcommand.
    /// </summary>
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
        var font = LoadEmbeddedFont("slant.flf");
        var figlet = new FigletText(font, "NetPace")
            .LeftJustified()
            .Color(theme.Banner);

        console.WriteLine();
        console.Write(figlet);
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

        // Add options in a specific order: --help first, then the rest in the order they were added
        var sortedOptions = command.Options.ToList();

        // Remove System.CommandLine's auto-generated --version option (it has description "Show version information")
        // We want to keep only our custom one with "Prints version information."
        sortedOptions.RemoveAll(o => o.Name == "--version" && (o.Description?.Contains("Show version information") ?? false));

        // Hidden options are registered only so that passing them produces a useful error; help
        // must not advertise them.
        sortedOptions.RemoveAll(o => o.Hidden);

        // Ensure --help is included and put it first
        var helpOption = sortedOptions.FirstOrDefault(o => o.Name == "--help");
        if (helpOption != null)
        {
            sortedOptions.Remove(helpOption);
        }

        // Always insert --help at the beginning for display purposes
        // (we intercept help before System.CommandLine adds it automatically)
        var helpOptionForDisplay = new Option<bool>("--help")
        {
            Description = "Prints help information."
        };
        helpOptionForDisplay.Aliases.Add("-h");
        sortedOptions.Insert(0, helpOptionForDisplay);

        // Check if any option has a default value (to determine if we need the default column)
        var hasDefaults = sortedOptions.Any(o => !string.IsNullOrEmpty(GetDefaultValue(o)));

        // Create grid matching Spectre.Console.Cli's exact configuration
        var grid = new Grid();
        grid.AddColumn(new GridColumn { Padding = new Padding(4, 4), NoWrap = true });

        if (hasDefaults)
        {
            grid.AddColumn(new GridColumn { Padding = new Padding(0, 0, 4, 0) });
        }

        grid.AddColumn(new GridColumn { Padding = new Padding(0, 0) });

        // Add header row if we have defaults
        if (hasDefaults)
        {
            grid.AddRow(Blank, new Text("DEFAULT", theme.DefaultColumnHeading), Blank);
        }

        foreach (var option in sortedOptions)
        {
            // Override System.CommandLine's default --help description to match Spectre.Console
            var description = option.Name == "--help" ? "Prints help information." : (option.Description ?? "");

            // Build the display name: combine short alias with the long option name
            // Example: -v, --version
            var shortAliases = option.Aliases
                .Where(a => a.StartsWith("-") && !a.StartsWith("--") && a != option.Name && !a.StartsWith("/"))
                .ToList();

            var longName = option.Name;

            string displayName;
            if (shortAliases.Count > 0)
            {
                // For --help, prefer -h over -?
                string primaryAlias;
                if (longName == "--help" && shortAliases.Contains("-h"))
                {
                    primaryAlias = "-h";
                }
                else
                {
                    primaryAlias = shortAliases.OrderBy(a => a).First();
                }

                // Use only the primary short alias: "-v, --version"
                displayName = primaryAlias + ", " + longName;
            }
            else
            {
                // Just the long name: "    --version" (with indent)
                displayName = "    " + longName;
            }

            // Get default value if available
            var defaultValue = GetDefaultValue(option);

            // Handle multi-line descriptions by splitting and properly indenting
            var descriptionLines = description.Split('\n');

            // Add rows based on whether we have a default column
            if (hasDefaults)
            {
                // First line
                grid.AddRow(new Text(displayName, theme.OptionName), new Text(defaultValue, theme.DefaultValue), new Text(descriptionLines[0]));

                // Subsequent lines (empty option and default columns, just description)
                for (int i = 1; i < descriptionLines.Length; i++)
                {
                    grid.AddRow(Blank, Blank, new Text(descriptionLines[i]));
                }
            }
            else
            {
                // First line (no default column)
                grid.AddRow(new Text(displayName, theme.OptionName), new Text(descriptionLines[0]));

                // Subsequent lines
                for (int i = 1; i < descriptionLines.Length; i++)
                {
                    grid.AddRow(Blank, new Text(descriptionLines[i]));
                }
            }
        }

        console.Write(grid);
        console.WriteLine();
    }

    /// <summary>
    /// An empty grid cell. A <see cref="Text"/> rather than <c>""</c> because the rest of a row is
    /// built from renderables, and a grid row takes one kind or the other, never a mix.
    /// </summary>
    private static Text Blank => new(string.Empty);

    private static string GetDefaultValue(Option option)
    {
        try
        {
            // Get the default value from the option
            var defaultValue = option.GetDefaultValue();

            // Handle different default value types
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

    /// <summary>
    /// The variables a user can set to change how NetPace behaves across every run.
    /// </summary>
    /// <remarks>
    /// Root help only. A variable is not a property of one command, and repeating the section under
    /// every subcommand would say the same thing in more places.
    /// </remarks>
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

    private static FigletFont LoadEmbeddedFont(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"NetPace.Console.{fileName}";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new FileNotFoundException($"Embedded resource '{resourceName}' not found.");
        }

        return FigletFont.Load(stream);
    }
}
