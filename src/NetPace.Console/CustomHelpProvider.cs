using System.CommandLine;
using System.Reflection;

namespace NetPace.Console;

/// <summary>
/// Custom help provider for System.CommandLine that uses Spectre.Console for rendering.
/// </summary>
internal static class CustomHelpProvider
{
    /// <summary>
    /// Render help for the root command.
    /// </summary>
    public static void RenderHelp(IAnsiConsole console, RootCommand command)
    {
        RenderHeader(console);
        RenderDescription(console, command);
        RenderUsage(console, command);
        RenderOptions(console, command);
        RenderSubcommands(console, command);
        RenderFooter(console);
    }

    /// <summary>
    /// Render help for a subcommand.
    /// </summary>
    public static void RenderHelp(IAnsiConsole console, Command command)
    {
        RenderHeader(console);
        RenderDescription(console, command);
        RenderUsage(console, command);
        RenderOptions(console, command);
        RenderFooter(console);
    }

    private static void RenderHeader(IAnsiConsole console)
    {
        var font = LoadEmbeddedFont("slant.flf");
        var figlet = new FigletText(font, "NetPace")
            .LeftJustified()
            .Color(Color.Gold1);

        console.WriteLine();
        console.Write(figlet);
        console.WriteLine();
    }

    private static void RenderDescription(IAnsiConsole console, Command command)
    {
        if (!string.IsNullOrWhiteSpace(command.Description))
        {
            console.MarkupLine("[bold yellow]DESCRIPTION:[/]");
            console.WriteLine(command.Description);
            console.WriteLine();
        }
    }

    private static void RenderUsage(IAnsiConsole console, Command command)
    {
        console.MarkupLine("[bold yellow]USAGE:[/]");
        if (command is RootCommand)
        {
            console.MarkupLine("    NetPace [grey][[OPTIONS]][/] [cyan][[COMMAND]][/]");
        }
        else
        {
            console.MarkupLine($"    NetPace {command.Name} [grey][[OPTIONS]][/]");
        }
        console.WriteLine();
    }

    private static void RenderOptions(IAnsiConsole console, Command command)
    {
        if (command.Options.Count == 0)
        {
            return;
        }

        console.MarkupLine("[bold yellow]OPTIONS:[/]");

        // Add options in a specific order: --help first, then the rest in the order they were added
        var sortedOptions = command.Options.ToList();

        // Remove System.CommandLine's auto-generated --version option (it has description "Show version information")
        // We want to keep only our custom one with "Prints version information."
        sortedOptions.RemoveAll(o => o.Name == "--version" && (o.Description?.Contains("Show version information") ?? false));

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
            grid.AddRow("", "[lime]DEFAULT[/]", "");
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
                grid.AddRow($"[silver]{displayName}[/]", $"[bold]{defaultValue}[/]", descriptionLines[0]);

                // Subsequent lines (empty option and default columns, just description)
                for (int i = 1; i < descriptionLines.Length; i++)
                {
                    grid.AddRow("", "", descriptionLines[i]);
                }
            }
            else
            {
                // First line (no default column)
                grid.AddRow($"[silver]{displayName}[/]", descriptionLines[0]);

                // Subsequent lines
                for (int i = 1; i < descriptionLines.Length; i++)
                {
                    grid.AddRow("", descriptionLines[i]);
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
                _ => defaultValue.ToString() ?? ""
            };
        }
        catch (Exception)
        {
            return "";
        }
    }

    private static void RenderSubcommands(IAnsiConsole console, RootCommand command)
    {
        if (command.Subcommands.Count == 0)
        {
            return;
        }

        console.MarkupLine("[bold yellow]COMMANDS:[/]");

        foreach (var subcommand in command.Subcommands.OrderBy(c => c.Name))
        {
            var description = subcommand.Description ?? "";
            console.Write("    ");
            console.MarkupLine($"[cyan]{subcommand.Name,-11}[/]{description}");
        }

        console.WriteLine();
    }

    private static void RenderFooter(IAnsiConsole console)
    {
        const string userGuideUrl = "https://github.com/FrankRay78/NetPace/blob/main/USER_GUIDE.md";

        console.MarkupLine($"[bold yellow]SEE ALSO:[/]");
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
