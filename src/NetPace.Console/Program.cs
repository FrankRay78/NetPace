using System.CommandLine;

using Microsoft.Extensions.DependencyInjection;
using NetPace.Console.Commands;
using NetPace.Core;
using Spectre.Console;

namespace NetPace.Console;

public static class Program
{
    /// <summary>
    /// The application description
    /// </summary>
    internal const string Description = "Network speed tester including server discovery, latency measurement, download and upload speed testing.";

    /// <summary>
    /// Create the RootCommand with System.CommandLine.
    /// </summary>
    /// <remarks>
    /// Extracted here so the testing project can reuse the production configuration.
    /// </remarks>
    internal static RootCommand CreateRootCommand(IServiceProvider serviceProvider)
    {
        var command = new RootCommand(Description);

        // Define options
        var versionOption = new Option<bool>("--version")
        {
            Description = "Prints version information."
        };
        versionOption.Aliases.Add("-v");

        var loopOption = new Option<bool>("--loop")
        {
            Description = "Performs the speed test on continuous loop.",
            DefaultValueFactory = _ => false
        };

        var countOption = new Option<int>("--count")
        {
            Description = "Stop speed testing after this many times.",
            DefaultValueFactory = _ => 1
        };

        var delayOption = new Option<TimeSpan>("--delay")
        {
            Description = "Time between multiple speed tests (HH:MM:SS).",
            DefaultValueFactory = _ => TimeSpan.Zero
        };

        var csvOption = new Option<bool>("--csv")
        {
            Description = "Display minimal output in CSV format (always includes timestamp).",
            DefaultValueFactory = _ => false
        };

        var csvDelimiterOption = new Option<string>("--csv-delimiter")
        {
            Description = "Single character delimiter to use in CSV output.",
            DefaultValueFactory = _ => ","
        };
        csvDelimiterOption.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<string>();
            if (value != null && value.Length != 1)
            {
                result.AddError("--csv-delimiter must be a single character.");
            }
        });

        var csvHeaderUnitsOption = new Option<bool>("--csv-header-units")
        {
            Description = "Display speed test units (eg. Mbps) in the CSV header row, not the data rows.\n--unit-scale must not be <Auto> for multiple speed tests (eg. --loop or --count).",
            DefaultValueFactory = _ => false
        };

        var jsonOption = new Option<bool>("--json")
        {
            Description = "Display output in Json format.",
            DefaultValueFactory = _ => false
        };

        var jsonPrettyOption = new Option<bool>("--json-pretty")
        {
            Description = "Display output in Json format (pretty print).",
            DefaultValueFactory = _ => false
        };

        var noLatencyOption = new Option<bool>("--no-latency")
        {
            Description = "Do not perform latency test.\nWhen used without --server, the first available server is selected.",
            DefaultValueFactory = _ => false
        };

        var noDownloadOption = new Option<bool>("--no-download")
        {
            Description = "Do not perform download test.",
            DefaultValueFactory = _ => false
        };

        var noUploadOption = new Option<bool>("--no-upload")
        {
            Description = "Do not perform upload test.",
            DefaultValueFactory = _ => false
        };

        var serverOption = new Option<string>("--server")
        {
            Description = "The url of a specific speed test sever. \n'NetPace servers -l' will return your nearest servers.",
            DefaultValueFactory = _ => string.Empty
        };

        var timestampOption = new Option<bool>("--timestamp")
        {
            Description = "Include a timestamp in the output.",
            DefaultValueFactory = _ => false
        };
        timestampOption.Aliases.Add("-t");

        var datetimeFormatOption = new Option<string>("--datetimeformat")
        {
            Description = "The datetime format string, as defined by Microsoft.Net.",
            DefaultValueFactory = _ => "yyyy-MM-dd HH:mm:ss"
        };

        var downloadSizeOption = new Option<int>("--downloadsize")
        {
            Description = "Stop the download test after this many megabytes (IEC MiB).",
            DefaultValueFactory = _ => int.MaxValue
        };

        var uploadSizeOption = new Option<int>("--uploadsize")
        {
            Description = "Stop the upload test after this many megabytes (IEC MiB).",
            DefaultValueFactory = _ => int.MaxValue
        };

        var unitOption = new Option<SpeedUnit>("--unit")
        {
            Description = "The speed unit. <BitsPerSecond, BytesPerSecond>",
            DefaultValueFactory = _ => SpeedUnit.BitsPerSecond
        };
        unitOption.Aliases.Add("-u");

        var unitScaleOption = new Option<SpeedScale>("--unit-scale")
        {
            Description = "The speed unit scale. <Auto, Base, Kilo, Mega, Giga, Tera, Peta>",
            DefaultValueFactory = _ => SpeedScale.Auto
        };

        var unitSystemOption = new Option<SpeedUnitSystem>("--unit-system")
        {
            Description = "The speed unit system. <SI, IEC>\nSI steps up in powers of 1000 (KB, MB, GB), common in networking, while IEC uses powers of 1024 (KiB, MiB, GiB), standard in computing and storage.",
            DefaultValueFactory = _ => SpeedUnitSystem.SI
        };

        var verbosityOption = new Option<Verbosity>("--verbosity")
        {
            Description = "The verbosity level. <Minimal, Normal, Debug>\nMinimal is ideal for batch scripts and redirected output.",
            DefaultValueFactory = _ => Verbosity.Normal
        };

        var fileOption = new Option<string>("--file")
        {
            Description = "Write output to file.",
            DefaultValueFactory = _ => string.Empty
        };
        fileOption.Aliases.Add("-f");

        var fileModeOption = new Option<FileMode>("--file-mode")
        {
            Description = "Determines file output behavior. <Append, Overwrite>",
            DefaultValueFactory = _ => FileMode.Append
        };

        var quietOption = new Option<bool>("--quiet")
        {
            Description = "Suppress all normal console output (file output still works).",
            DefaultValueFactory = _ => false
        };
        quietOption.Aliases.Add("-q");

        // Add options
        command.Options.Add(versionOption);
        command.Options.Add(loopOption);
        command.Options.Add(countOption);
        command.Options.Add(delayOption);
        command.Options.Add(csvOption);
        command.Options.Add(csvDelimiterOption);
        command.Options.Add(csvHeaderUnitsOption);
        command.Options.Add(jsonOption);
        command.Options.Add(jsonPrettyOption);
        command.Options.Add(noLatencyOption);
        command.Options.Add(noDownloadOption);
        command.Options.Add(noUploadOption);
        command.Options.Add(serverOption);
        command.Options.Add(timestampOption);
        command.Options.Add(datetimeFormatOption);
        command.Options.Add(downloadSizeOption);
        command.Options.Add(uploadSizeOption);
        command.Options.Add(unitOption);
        command.Options.Add(unitScaleOption);
        command.Options.Add(unitSystemOption);
        command.Options.Add(verbosityOption);
        command.Options.Add(fileOption);
        command.Options.Add(fileModeOption);
        command.Options.Add(quietOption);

        // Set command action
        command.SetAction((Func<ParseResult, CancellationToken, Task<int>>)(async (parseResult, cancellationToken) =>
        {
            try
            {
                // Get option values and populate settings
                var settings = new SpeedTestCommandSettings
                {
                    Loop = parseResult.GetValue(loopOption),
                    Count = parseResult.GetValue(countOption),
                    Delay = parseResult.GetValue(delayOption),
                    CSV = parseResult.GetValue(csvOption),
                    CSVDelimiter = (parseResult.GetValue(csvDelimiterOption) ?? ",")[0],
                    CSVHeaderUnits = parseResult.GetValue(csvHeaderUnitsOption),
                    Json = parseResult.GetValue(jsonOption),
                    JsonPretty = parseResult.GetValue(jsonPrettyOption),
                    NoLatency = parseResult.GetValue(noLatencyOption),
                    NoDownload = parseResult.GetValue(noDownloadOption),
                    NoUpload = parseResult.GetValue(noUploadOption),
                    ServerUrl = parseResult.GetValue(serverOption) ?? string.Empty,
                    IncludeTimestamp = parseResult.GetValue(timestampOption),
                    DateTimeFormat = parseResult.GetValue(datetimeFormatOption)!,
                    DownloadSizeMb = parseResult.GetValue(downloadSizeOption),
                    UploadSizeMb = parseResult.GetValue(uploadSizeOption),
                    SpeedUnit = parseResult.GetValue(unitOption),
                    SpeedScale = parseResult.GetValue(unitScaleOption),
                    SpeedUnitSystem = parseResult.GetValue(unitSystemOption),
                    Verbosity = parseResult.GetValue(verbosityOption),
                    OutputFile = parseResult.GetValue(fileOption) ?? string.Empty,
                    FileModeValue = parseResult.GetValue(fileModeOption),
                    Quiet = parseResult.GetValue(quietOption)
                };

                // Validate settings
                settings.Validate();

                // Get services from DI
                var ansiConsole = serviceProvider.GetRequiredService<IAnsiConsole>();
                var speedTestService = serviceProvider.GetRequiredService<ISpeedTestService>();
                var clock = serviceProvider.GetRequiredService<IClock>();
                var clientInfoProvider = serviceProvider.GetRequiredService<IClientInfoProvider>();
                var waiter = serviceProvider.GetRequiredService<IWaiter>();

                // Create and execute command
                var command = new SpeedTestCommand(ansiConsole, speedTestService, clock, clientInfoProvider, waiter);
                return await command.ExecuteAsync(settings, cancellationToken);
            }
            catch (Exception ex)
            {
                var ansiConsole = serviceProvider.GetRequiredService<IAnsiConsole>();
                ansiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
                return 1;
            }
        }));

        // Add list servers command
        var listServersCommand = CreateListServersCommand(serviceProvider);
        command.Subcommands.Add(listServersCommand);

        return command;
    }

    /// <summary>
    /// Create the list servers command.
    /// </summary>
    private static Command CreateListServersCommand(IServiceProvider serviceProvider)
    {
        var command = new Command("servers", "Show the nearest speed test servers.");

        // Define options
        var latencyOption = new Option<bool>("--latency")
        {
            Description = "Include server latency in the results."
        };
        latencyOption.Aliases.Add("-l");

        var fastestOption = new Option<bool>("--fastest")
        {
            Description = "Show the fastest server details, selected by lowest latency."
        };
        fastestOption.Aliases.Add("-f");

        // Add options
        command.Options.Add(latencyOption);
        command.Options.Add(fastestOption);

        // Set command action
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            try
            {
                // Get option values and populate settings
                var settings = new ListServersCommandSettings
                {
                    ShowLatency = parseResult.GetValue(latencyOption),
                    Fastest = parseResult.GetValue(fastestOption)
                };

                // Get services from DI
                var ansiConsole = serviceProvider.GetRequiredService<IAnsiConsole>();
                var speedTestService = serviceProvider.GetRequiredService<ISpeedTestService>();

                // Create and execute command
                var command = new ListServersCommand(ansiConsole, speedTestService);
                return await command.ExecuteAsync(settings, cancellationToken);
            }
            catch (Exception ex)
            {
                var ansiConsole = serviceProvider.GetRequiredService<IAnsiConsole>();
                ansiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
                return 1;
            }
        });

        return command;
    }

    /// <summary>
    /// Run the command line application with the given arguments.
    /// </summary>
    public async static Task<int> Main(string[] args)
    {
        // Setup DI
        var services = new ServiceCollection();

        // Register AnsiConsole
        services.AddSingleton(AnsiConsole.Console);

        if (args != null && args.Contains("--test"))
        {
            // Executes NetPace against stub service implementations.
            services.AddSingleton<ISpeedTestService>(new SpeedTestStub(250));
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IClientInfoProvider, ClientInfoProviderStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
        }
        else
        {
            services.AddSingleton<ISpeedTestService, OoklaSpeedtest>();
            services.AddSingleton<IClock, Clock>();
            services.AddSingleton<IClientInfoProvider, ClientInfoProvider>();
            services.AddSingleton<IWaiter, Waiter>();
        }

        await using var serviceProvider = services.BuildServiceProvider();

        using var cancellationTokenSource = new CancellationTokenSource();

        System.Console.CancelKeyPress += (_, eventArgs) =>
        {
            // Try to cancel gracefully the first time, then abort the process the second time Ctrl+C is pressed
            eventArgs.Cancel = !cancellationTokenSource.IsCancellationRequested;
            cancellationTokenSource.Cancel();
        };

        return await RunAsync(
            serviceProvider,
            args!.Where(s => !s.Equals("--test")).ToArray(),
            cancellationTokenSource.Token);
    }

    internal static async Task<int> RunAsync(IServiceProvider serviceProvider, string[] args, CancellationToken cancellationToken = default)
    {
        var ansiConsole = serviceProvider.GetRequiredService<IAnsiConsole>();
        var rootCommand = CreateRootCommand(serviceProvider);

        // Check for version request before parsing
        if (args.Length > 0 && (args[0] is "-v" or "--version"))
        {
            var assemblyVersion = typeof(Program).Assembly.GetName().Version;
            var version = assemblyVersion != null
                ? $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}"
                : "Unknown";
            ansiConsole.WriteLine(version);
            return 0;
        }

        // Check for help request before parsing
        if (args.Length > 0 && (args[0] is "-h" or "--help" or "-?"))
        {
            CustomHelpProvider.RenderHelp(ansiConsole, rootCommand);
            return 0;
        }

        // Check for subcommand help
        if (args.Length > 1 && (args[1] is "-h" or "--help" or "-?"))
        {
            var subcommandName = args[0];
            var subcommand = rootCommand.Subcommands.FirstOrDefault(c => c.Name == subcommandName);
            if (subcommand != null)
            {
                CustomHelpProvider.RenderHelp(ansiConsole, subcommand);
                return 0;
            }
        }

        var parseResult = rootCommand.Parse(args);

        // Handle parse errors - these should be shown even in quiet mode
        if (parseResult.Errors.Count > 0)
        {
            foreach (var error in parseResult.Errors)
            {
                ansiConsole.MarkupLine($"[red]Error:[/] {error.Message}");
            }
            return 1;
        }

        return await parseResult.InvokeAsync(cancellationToken: cancellationToken);
    }
}
