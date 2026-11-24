using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;

using Microsoft.Extensions.DependencyInjection;
using NetPace.Console.Commands;
using NetPace.Console.ConsoleWriters;
using NetPace.Console.Diagnostics;
using NetPace.Console.Theming;
using NetPace.Core;
using NetPace.Core.Clients.LibreSpeed;
using NetPace.Core.Clients.Ookla;
using NetPace.Core.Settings;
using Spectre.Console;

namespace NetPace.Console;

public static class Program
{
    internal const string Description = "Network speed tester including server discovery, latency measurement, download and upload speed testing.";

    internal const int MinimumConnections = 1;

    // Never more than the heaviest profile already asks for, which today is Mega's 32.
    internal const int MaximumConnections = 32;

    // On by default, so a scheduled run is bounded without the user configuring anything.
    internal static readonly TimeSpan DefaultTimeLimit = TimeSpan.FromMinutes(10);

    // Shared by the parser and DefaultTimeLimitText, so --help and the parser cannot come to
    // disagree about what the default is.
    private const string TimeLimitFormat = @"hh\:mm\:ss";

    internal static string DefaultTimeLimitText => DefaultTimeLimit.ToString(TimeLimitFormat, CultureInfo.InvariantCulture);

    // Its own method so the test project reuses the production configuration rather than a copy.
    internal static RootCommand CreateRootCommand(IServiceProvider serviceProvider, string[] args, Theme theme)
    {
        var command = new RootCommand(Description);

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

        // A string rather than an Option<TimeSpan>: that reads a bare 600 as six hundred days and
        // binds it without complaint, which a scheduled run has no way of noticing.
        var timeLimitOption = new Option<string>("--time-limit")
        {
            Description = "Stop a speed test run after this long (HH:MM:SS, up to 23:59:59).\nA run that reaches the limit reports what it measured up to then.\nEach run of --count or --loop gets the limit afresh, and --delay is not counted.",
            DefaultValueFactory = _ => DefaultTimeLimitText
        };
        timeLimitOption.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<string>();
            if (!TryParseTimeLimit(value, out _))
            {
                result.AddError(TimeLimitError(value));
            }
        });

        var csvOption = new Option<bool>("--csv")
        {
            Description = "Display output as a single CSV row (always includes timestamp).",
            DefaultValueFactory = _ => false
        };

        var noCSVHeaderOption = new Option<bool>("--no-csv-header")
        {
            Description = "Do not write the header row (CSV output only).\nAn appended --file target that already holds rows is never sent the header.",
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

        var minimalOption = new Option<bool>("--minimal")
        {
            Description = "Display the result as a single timestamped line per run.\nIdeal for watching repeated runs with --loop or --count.",
            DefaultValueFactory = _ => false
        };

        var simpleOption = new Option<bool>("--simple")
        {
            Description = "Display the result as one named line per figure: Ping, Download, Upload.\nIdeal for batch scripts and line-based parsing.",
            DefaultValueFactory = _ => false
        };

        var noLatencyOption = new Option<bool>("--no-latency")
        {
            Description = "Do not perform latency test.\nWhen used without --server-url, the first available server is selected.",
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

        var providerOption = new Option<Provider>("--provider")
        {
            Description = $"The kind of server to test against. {OptionValues.Providers.HelpList}\n'librespeed' needs --server-url naming where LibreSpeed is installed.",
            DefaultValueFactory = _ => Provider.Ookla,
            CustomParser = result => OptionValues.Providers.Parse(result, "--provider")
        };

        var serverUrlOption = new Option<string>("--server-url")
        {
            Description = "The URL of a specific speed test server.\n'netpace servers -l' will return your nearest servers.\nWith --provider librespeed, the address LibreSpeed is installed at.",
            DefaultValueFactory = _ => string.Empty
        };

        var profileOption = new Option<Profile>("--profile")
        {
            Description = $"Profile bundle of payload settings. {OptionValues.Profiles.HelpList}",
            DefaultValueFactory = _ => Profile.Medium,
            CustomParser = result => OptionValues.Profiles.Parse(result, "--profile")
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

        // A string rather than an Option<int>, which would answer "2.5" with a built-in parse
        // failure that never mentions the valid range.
        var connectionsOption = new Option<string>("--connections")
        {
            Description = $"Maximum transfers the download and upload tests each run at once ({MinimumConnections}-{MaximumConnections}).\nOverrides the --profile connection count; the profile's data caps still apply.\nA maximum, so a profile with fewer transfers to run than this will not reach it.",
            DefaultValueFactory = _ => string.Empty
        };
        connectionsOption.Validators.Add(result =>
        {
            // The implicit result the default produces stands for "the profile decides", not a
            // value the user asked to be checked.
            if (result.Implicit)
            {
                return;
            }

            var value = result.GetValueOrDefault<string>();
            if (!TryParseConnections(value, out var connections) || connections < MinimumConnections || connections > MaximumConnections)
            {
                result.AddError($"--connections must be a whole number from {MinimumConnections} to {MaximumConnections}, but was '{value}'.");
            }
        });

        var unitOption = new Option<SpeedUnit>("--unit")
        {
            Description = $"The speed unit. {OptionValues.Units.HelpList}\nApplies to the default, minimal and simple output.",
            DefaultValueFactory = _ => SpeedUnit.BitsPerSecond,
            CustomParser = result => OptionValues.Units.Parse(result, "--unit")
        };
        unitOption.Aliases.Add("-u");

        var unitScaleOption = new Option<SpeedScale>("--unit-scale")
        {
            Description = $"The speed unit scale. {OptionValues.UnitScales.HelpList}\nApplies to the default, minimal and simple output.",
            DefaultValueFactory = _ => SpeedScale.Auto,
            CustomParser = result => OptionValues.UnitScales.Parse(result, "--unit-scale")
        };

        var unitSystemOption = new Option<SpeedUnitSystem>("--unit-system")
        {
            Description = $"The speed unit system. {OptionValues.UnitSystems.HelpList}\nApplies to the default, minimal and simple output.\nSI steps up in powers of 1000 (KB, MB, GB), common in networking,\nwhile IEC uses powers of 1024 (KiB, MiB, GiB), standard in computing and storage.",
            DefaultValueFactory = _ => SpeedUnitSystem.SI,
            CustomParser = result => OptionValues.UnitSystems.Parse(result, "--unit-system")
        };

        var deviceIdOption = new Option<string>("--device-id")
        {
            Description = "Record a label for this machine in CSV and JSON output.\nThe CSV column is always present; the JSON field appears only when a label is given."
        };

        // Pinned so each occurrence takes exactly one key=value argument: a stray second word is
        // then an unrecognised argument rather than a silently swallowed pair.
        var customFieldOption = new Option<string[]>("--custom-field")
        {
            Description = $"Record a {CustomField.ExpectedForm} pair in CSV and JSON output.\nMay be given more than once, and is recorded in the order given.",
            AllowMultipleArgumentsPerToken = false
        };
        // A validator rather than a check at binding time, so a malformed pair fails the parse and
        // stops the run even in the output modes that ignore custom fields altogether.
        customFieldOption.Validators.Add(result =>
        {
            foreach (Token token in result.Tokens)
            {
                if (!CustomField.TryParse(token.Value, out _))
                {
                    result.AddError($"--custom-field '{token.Value}' is not in the form {CustomField.ExpectedForm}.");
                }
            }
        });

        // Renamed --server-url, but still registered so a script passing the old name is told what
        // to use. Optional string, so a bare --server lands on the same message rather than a
        // built-in missing-argument failure; hidden, so --help advertises only the new name.
        var serverOption = new Option<string>("--server")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Hidden = true
        };
        serverOption.Validators.Add(result => result.AddError("--server has been renamed --server-url, see --help."));

        // Retired, but still registered so a script passing it is told so. Optional string, so
        // every former value and no value at all land on the same error; hidden, so --help does not
        // advertise it.
        var verbosityOption = new Option<string>("--verbosity")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Hidden = true
        };
        // No replacement is named: --minimal stands in only for the former Minimal value, so
        // pointing at it would mislead anyone who was passing Normal or Debug.
        verbosityOption.Validators.Add(result => result.AddError("--verbosity has been removed, see --help."));

        var fileOption = new Option<string>("--file")
        {
            Description = "Write output to file.",
            DefaultValueFactory = _ => string.Empty
        };
        fileOption.Aliases.Add("-f");

        var fileModeOption = new Option<FileMode>("--file-mode")
        {
            Description = $"Determines file output behavior. {OptionValues.FileModes.HelpList}",
            DefaultValueFactory = _ => FileMode.Append,
            CustomParser = result => OptionValues.FileModes.Parse(result, "--file-mode")
        };

        var quietOption = new Option<bool>("--quiet")
        {
            Description = "Suppress all normal console output (file output still works).",
            DefaultValueFactory = _ => false
        };
        quietOption.Aliases.Add("-q");

        var minDownloadOption = new Option<MeasurementThreshold?>("--min-download")
        {
            Description = $"Exit {ThresholdCheck.MissedExitCode} when the download speed is below this, such as {ThresholdOption.SpeedExample}.\nTakes any unit NetPace writes a speed in, whatever --unit, --unit-system and --unit-scale say.\nChecks one run, so not with --loop, --count above 1, or --no-download.",
            DefaultValueFactory = _ => null,
            CustomParser = result => ThresholdOption.ParseSpeed(result, "--min-download")
        };

        var minUploadOption = new Option<MeasurementThreshold?>("--min-upload")
        {
            Description = $"Exit {ThresholdCheck.MissedExitCode} when the upload speed is below this, such as {ThresholdOption.SpeedExample}.\nRead the same way as --min-download; not with --loop, --count above 1, or --no-upload.",
            DefaultValueFactory = _ => null,
            CustomParser = result => ThresholdOption.ParseSpeed(result, "--min-upload")
        };

        var maxLatencyOption = new Option<MeasurementThreshold?>("--max-latency")
        {
            Description = $"Exit {ThresholdCheck.MissedExitCode} when the idle latency is above this, such as {ThresholdOption.LatencyExample}.\nMilliseconds only; not with --loop, --count above 1, or --no-latency.",
            DefaultValueFactory = _ => null,
            CustomParser = result => ThresholdOption.ParseLatency(result, "--max-latency")
        };

        var diagnosticsOption = new Option<bool>("--diagnostics")
        {
            Description = "Write per-request diagnostic detail about the run to the error stream.\nThe result is unchanged, so 'netpace --json --diagnostics > result.json 2> diagnostics.log' keeps the two apart.",
            DefaultValueFactory = _ => false
        };

        var diagnosticsFileOption = new Option<string>("--diagnostics-file")
        {
            Description = "Write the diagnostic detail to this file instead of the error stream.\nTurns diagnostics on by itself, and replaces the file on every run.",
            DefaultValueFactory = _ => string.Empty
        };
        diagnosticsFileOption.Validators.Add(result =>
        {
            // The implicit result the default produces stands for "no file named". A blank value
            // the user did pass reads the same way downstream, so letting it through would exit
            // zero having written nothing - which `--diagnostics-file "$LOG"` with LOG unset,
            // the ordinary way to arrive here, would never reveal.
            if (result.Implicit)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(result.GetValueOrDefault<string>()))
            {
                result.AddError("--diagnostics-file needs a path: the value given names no file.");
            }
        });

        var failOnOption = new Option<FailOn>("--fail-on")
        {
            Description = $"Exit with a non-zero code on a failed measurement. {OptionValues.FailOnModes.HelpList}\nWith 'none' the exit code is never affected; 'total' triggers when a test is all-failed;\n'partial' triggers on any failed request. Fail-fast across --count and --loop.",
            DefaultValueFactory = _ => FailOn.None,
            CustomParser = result => OptionValues.FailOnModes.Parse(result, "--fail-on")
        };

        command.Options.Add(versionOption);
        command.Options.Add(loopOption);
        command.Options.Add(countOption);
        command.Options.Add(delayOption);
        command.Options.Add(timeLimitOption);
        command.Options.Add(csvOption);
        command.Options.Add(noCSVHeaderOption);
        command.Options.Add(jsonOption);
        command.Options.Add(jsonPrettyOption);
        command.Options.Add(minimalOption);
        command.Options.Add(simpleOption);
        command.Options.Add(noLatencyOption);
        command.Options.Add(noDownloadOption);
        command.Options.Add(noUploadOption);
        command.Options.Add(providerOption);
        command.Options.Add(serverUrlOption);
        command.Options.Add(profileOption);
        command.Options.Add(downloadSizeOption);
        command.Options.Add(uploadSizeOption);
        command.Options.Add(connectionsOption);
        command.Options.Add(unitOption);
        command.Options.Add(unitScaleOption);
        command.Options.Add(unitSystemOption);
        command.Options.Add(deviceIdOption);
        command.Options.Add(customFieldOption);
        command.Options.Add(serverOption);
        command.Options.Add(verbosityOption);
        command.Options.Add(fileOption);
        command.Options.Add(fileModeOption);
        command.Options.Add(quietOption);
        command.Options.Add(failOnOption);
        command.Options.Add(minDownloadOption);
        command.Options.Add(minUploadOption);
        command.Options.Add(maxLatencyOption);
        command.Options.Add(diagnosticsOption);
        command.Options.Add(diagnosticsFileOption);

        command.SetAction((Func<ParseResult, CancellationToken, Task<int>>)(async (parseResult, cancellationToken) =>
        {
            // Declared out here so the diagnostics file is closed however the run ends, including
            // the throws the catch below turns into exit code 1.
            TextWriter? diagnosticsFile = null;

            try
            {
                var settings = new SpeedTestCommandSettings
                {
                    Loop = parseResult.GetValue(loopOption),
                    Count = parseResult.GetValue(countOption),
                    Delay = parseResult.GetValue(delayOption),
                    TimeLimit = ParseTimeLimit(parseResult.GetValue(timeLimitOption)),
                    CSV = parseResult.GetValue(csvOption),
                    NoCSVHeader = parseResult.GetValue(noCSVHeaderOption),
                    Json = parseResult.GetValue(jsonOption),
                    JsonPretty = parseResult.GetValue(jsonPrettyOption),
                    Simple = parseResult.GetValue(simpleOption),
                    Minimal = parseResult.GetValue(minimalOption),
                    NoLatency = parseResult.GetValue(noLatencyOption),
                    NoDownload = parseResult.GetValue(noDownloadOption),
                    NoUpload = parseResult.GetValue(noUploadOption),
                    Provider = parseResult.GetValue(providerOption),
                    ServerUrl = parseResult.GetValue(serverUrlOption) ?? string.Empty,
                    Profile = parseResult.GetValue(profileOption),
                    DownloadSizeMb = parseResult.GetValue(downloadSizeOption),
                    UploadSizeMb = parseResult.GetValue(uploadSizeOption),
                    Connections = ParseConnections(parseResult.GetValue(connectionsOption)),
                    SpeedUnit = parseResult.GetValue(unitOption),
                    SpeedScale = parseResult.GetValue(unitScaleOption),
                    SpeedUnitSystem = parseResult.GetValue(unitSystemOption),
                    OutputFile = parseResult.GetValue(fileOption) ?? string.Empty,
                    FileModeValue = parseResult.GetValue(fileModeOption),
                    Quiet = parseResult.GetValue(quietOption),
                    FailOn = parseResult.GetValue(failOnOption),
                    MinimumDownload = parseResult.GetValue(minDownloadOption),
                    MinimumUpload = parseResult.GetValue(minUploadOption),
                    MaximumLatency = parseResult.GetValue(maxLatencyOption),
                    Diagnostics = parseResult.GetValue(diagnosticsOption),
                    DiagnosticsFile = parseResult.GetValue(diagnosticsFileOption) ?? string.Empty,
                    DeviceId = parseResult.GetValue(deviceIdOption),
                    CustomFields = CustomField.ParseAll(parseResult.GetValue(customFieldOption))
                };

                settings.Validate();

                // The accessor is the test seam: production resolves the provider from it, and a
                // test reads Settings after the run to check the CLI bound as it should.
                var accessor = serviceProvider.GetRequiredService<SpeedTestSettingsAccessor>();
                accessor.Settings = BuildSpeedTestSettings(settings);

                var ansiConsole = serviceProvider.GetRequiredService<IAnsiConsole>();
                var errorOutput = serviceProvider.GetRequiredService<ErrorOutput>();
                var speedTestService = serviceProvider.GetRequiredService<ISpeedTestService>();
                var clock = serviceProvider.GetRequiredService<IClock>();
                var clientInfoProvider = serviceProvider.GetRequiredService<IClientInfoProvider>();
                var waiter = serviceProvider.GetRequiredService<IWaiter>();
                var runTimeLimit = serviceProvider.GetRequiredService<IRunTimeLimit>();
                var liveDisplayRefresh = serviceProvider.GetRequiredService<ILiveDisplayRefresh>();

                // A null recorder when diagnostics are off, so no call site downstream asks whether
                // they are on. Opened here, before the first test and inside this action's own
                // error handling, so an unwritable path ends the run with the path named rather
                // than surfacing part way through a measurement.
                IDiagnosticRecorder recorder = NullDiagnosticRecorder.Instance;

                if (!string.IsNullOrWhiteSpace(settings.DiagnosticsFile))
                {
                    // Wrapped rather than reported raw: the framework's own message says what went
                    // wrong but never which file it was about, and a run may name two.
                    try
                    {
                        diagnosticsFile = serviceProvider.GetRequiredService<IDiagnosticFileOpener>().Open(settings.DiagnosticsFile);
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                    {
                        throw new InvalidOperationException(
                            $"The diagnostics file {settings.DiagnosticsFile} could not be opened: {e.Message} Name a path in an existing directory you can write to.", e);
                    }

                    recorder = new BufferingDiagnosticRecorder(serviceProvider.GetRequiredService<IDiagnosticClock>(), diagnosticsFile);
                }
                else if (settings.Diagnostics)
                {
                    recorder = new BufferingDiagnosticRecorder(
                        serviceProvider.GetRequiredService<IDiagnosticClock>(),
                        serviceProvider.GetRequiredService<DiagnosticOutput>().Writer);
                }

                var environmentInfoProvider = serviceProvider.GetRequiredService<IEnvironmentInfoProvider>();
                recorder.Record(
                    "run.start",
                    ("version", environmentInfoProvider.GetVersion()),
                    ("runtime", environmentInfoProvider.GetRuntime()),
                    ("os", environmentInfoProvider.GetOperatingSystem()),
                    ("arch", environmentInfoProvider.GetArchitecture()));
                recorder.Record("run.invocation", ("args", string.Join(' ', args)));

                var command = new SpeedTestCommand(ansiConsole, errorOutput, serviceProvider.GetRequiredService<NoticeOutput>(), speedTestService, clock, clientInfoProvider, recorder, waiter, runTimeLimit, liveDisplayRefresh, theme);
                return await command.ExecuteAsync(settings, cancellationToken);
            }
            catch (Exception ex)
            {
                // The error stream, so a redirect of the result never swallows the message and no
                // reader of the result meets a line that is not part of its format. Resolved again
                // rather than reused from above, because the throw may have come first.
                serviceProvider.GetRequiredService<ErrorOutput>().WriteLine(theme, ex.Message);
                return 1;
            }
            finally
            {
                // Guarded because this finally sits outside the catch above, and closing a
                // StreamWriter flushes what it still holds: a raise here would turn a completed
                // measurement into a stack trace and a non-zero exit. Nothing is written about it -
                // records are flushed after every batch, so the user has already been told.
                try
                {
                    diagnosticsFile?.Dispose();
                }
                catch (IOException)
                {
                }
            }
        }));

        var listServersCommand = CreateListServersCommand(serviceProvider, theme);
        command.Subcommands.Add(listServersCommand);

        return command;
    }

    // --provider picks which record is built; everything after that is provider-neutral and is
    // applied once, to the shared half of it.
    internal static SpeedTestSettings BuildSpeedTestSettings(SpeedTestCommandSettings settings)
    {
        SpeedTestSettings speedTestSettings = settings.Provider switch
        {
            Provider.LibreSpeed => new LibreSpeedtestSettings(settings.Profile),
            _ => new OoklaSpeedtestSettings(settings.Profile),
        };

        if (settings.DownloadSizeMb != int.MaxValue)
        {
            speedTestSettings = speedTestSettings with
            {
                DownloadTest = speedTestSettings.DownloadTest with { DownloadSizeMb = settings.DownloadSizeMb }
            };
        }

        if (settings.UploadSizeMb != int.MaxValue)
        {
            speedTestSettings = speedTestSettings with
            {
                UploadTest = speedTestSettings.UploadTest with { UploadSizeMb = settings.UploadSizeMb }
            };
        }

        if (settings.Simple || settings.Minimal || settings.NoLatency)
        {
            // --simple and --minimal report no under-load figure, so these probes would be traffic
            // nobody reads; --no-latency asks for no latency anywhere. The idle latency test still
            // runs under the first two, being where their latency figure comes from.
            speedTestSettings = speedTestSettings with
            {
                LatencyTest = speedTestSettings.LatencyTest with { LoadedLatencyEnabled = false }
            };
        }

        if (settings.Connections is int connections)
        {
            speedTestSettings = speedTestSettings with
            {
                DownloadTest = speedTestSettings.DownloadTest with { DownloadParallelTasks = connections },
                UploadTest = speedTestSettings.UploadTest with { UploadParallelTasks = connections }
            };
        }

        return speedTestSettings;
    }

    // Read off the settings record rather than from --provider again, so a run cannot end up with
    // LibreSpeed settings in an Ookla provider.
    internal static ISpeedTestService CreateSpeedTestService(SpeedTestSettings settings) => settings switch
    {
        OoklaSpeedtestSettings ookla => new OoklaSpeedtest(ookla),
        LibreSpeedtestSettings libreSpeed => new LibreSpeedtest(libreSpeed),
        _ => throw new ArgumentOutOfRangeException(nameof(settings), settings.GetType().Name, "No speed test provider is registered for these settings."),
    };

    // Exactly hh:mm:ss, because TimeSpan's own parsing reads shorter forms as something else: 600
    // as six hundred days, 10:00 as ten hours. --delay accepts any TimeSpan form; this is the one
    // strict option.
    private static bool TryParseTimeLimit(string? value, out TimeSpan limit) =>
        TimeSpan.TryParseExact(value, TimeLimitFormat, CultureInfo.InvariantCulture, out limit) && limit > TimeSpan.Zero;

    // A value present but unparseable means the validator never ran, and throwing beats bounding a
    // run by a duration nobody asked for.
    private static TimeSpan ParseTimeLimit(string? value) =>
        TryParseTimeLimit(value, out var limit)
            ? limit
            : throw new ArgumentException(TimeLimitError(value));

    private static string TimeLimitError(string? value) =>
        $"--time-limit must be written as HH:MM:SS, from 00:00:01 to 23:59:59, such as {DefaultTimeLimitText}, but was '{value}'.";

    // Digits only and culture independent, so the accepted set is exactly the whole number the
    // help text promises.
    private static bool TryParseConnections(string? value, out int connections) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out connections);

    // Null where --connections was not given, so the profile's own counts stand. Absent is the only
    // silent case: a value present but unparseable means the validator never ran, and throwing
    // beats reporting a speed for a load nobody asked for.
    private static int? ParseConnections(string? value) =>
        string.IsNullOrEmpty(value) ? null : int.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);

    private static Command CreateListServersCommand(IServiceProvider serviceProvider, Theme theme)
    {
        var command = new Command("servers", "Show the nearest speed test servers.");

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

        command.Options.Add(latencyOption);
        command.Options.Add(fastestOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            try
            {
                var settings = new ListServersCommandSettings
                {
                    ShowLatency = parseResult.GetValue(latencyOption),
                    Fastest = parseResult.GetValue(fastestOption)
                };

                var ansiConsole = serviceProvider.GetRequiredService<IAnsiConsole>();
                var errorOutput = serviceProvider.GetRequiredService<ErrorOutput>();
                var speedTestService = serviceProvider.GetRequiredService<ISpeedTestService>();

                var command = new ListServersCommand(ansiConsole, errorOutput, speedTestService, theme);
                return await command.ExecuteAsync(settings, cancellationToken);
            }
            catch (Exception ex)
            {
                // Network and discovery outcomes never reach here: ListServersCommand reports
                // those itself and leaves the exit code at 0, as a speed test does.
                serviceProvider.GetRequiredService<ErrorOutput>().WriteLine(theme, ex.Message);
                return 1;
            }
        });

        return command;
    }

    // The three outputs that go to the process error stream rather than to the result, so none of
    // them lands in the middle of a result another program is reading. Its own method so a test can
    // resolve all three and assert which stream each carries; inline in Main, the one line that
    // decides it would be reachable by nothing
    // (docs/change-intent-records/2026-11-14-errors-go-to-a-themed-console-on-standard-error.md).
    internal static void AddStreamOutputs(IServiceCollection services)
    {
        services.AddSingleton(new NoticeOutput(System.Console.Error));

        // Its own console rather than the result's, so --quiet and --file never reach it.
        services.AddSingleton(CreateErrorOutput(
            System.Console.Error,
            isRedirected: System.Console.IsErrorRedirected,
            isWindows: OperatingSystem.IsWindows()));

        // Never through the Spectre console: one record per line, whatever the terminal width, and
        // whatever --quiet or --file were set to.
        services.AddSingleton(new DiagnosticOutput(System.Console.Error));
    }

    // The platform is a parameter rather than read here, so a test fixes it instead of inheriting
    // the machine it runs on. Pass System.Console.Error and nothing else with isWindows set:
    // Spectre.Console picks the handle to probe by reference-comparing the writer against the
    // standard streams, so any other writer would send detection to standard output - the one
    // handle the error stream exists to leave alone.
    internal static ErrorOutput CreateErrorOutput(TextWriter writer, bool isRedirected, bool isWindows) =>
        new(
            AnsiConsole.Create(new AnsiConsoleSettings
            {
                Out = new AnsiConsoleOutput(writer),
                Ansi = ErrorAnsiSupport(isWindows, isRedirected),
                ColorSystem = ColorSystemSupport.Detect
            }),
            styled: !isRedirected);

    // Detect only on a Windows terminal: only detection passes the flag that switches virtual
    // terminal processing on, without which a classic console window prints the escape sequences
    // literally once the result is redirected. Detecting anywhere else risks Spectre.Console's
    // fallback writing System.Console's colour properties to standard OUTPUT, stamping the error
    // console's colours onto the live result
    // (docs/change-intent-records/2026-11-17-error-colour-on-a-windows-console.md).
    internal static AnsiSupport ErrorAnsiSupport(bool isWindows, bool isRedirected) =>
        isWindows && !isRedirected ? AnsiSupport.Detect : AnsiSupport.Yes;

    public async static Task<int> Main(string[] args)
    {
        var services = new ServiceCollection();

        services.AddSingleton(AnsiConsole.Console);
        services.AddSingleton<IThemeProvider, EnvironmentThemeProvider>();

        AddStreamOutputs(services);

        // Registered outside the --test branch below, because each is wanted whether the
        // measurement behind it was stubbed or not: a named diagnostics file writes a real file,
        // the accessor is populated after option binding, a person watching wants the live
        // displays to redraw, and a run is bounded by a real clock. Only the test host replaces
        // them, so a suite holds one frame and does not wait a time limit out.
        services.AddSingleton<IDiagnosticFileOpener, DiagnosticFileOpener>();
        services.AddSingleton<SpeedTestSettingsAccessor>();
        services.AddSingleton<ILiveDisplayRefresh, LiveDisplayRefresh>();
        services.AddSingleton<IRunTimeLimit, RunTimeLimit>();

        if (args != null && args.Contains("--test"))
        {
            services.AddSingleton<ISpeedTestService>(new SpeedTestStub(250));
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IClientInfoProvider, ClientInfoProviderStub>();
            services.AddSingleton<IEnvironmentInfoProvider, EnvironmentInfoProviderStub>();
            services.AddSingleton<IDiagnosticClock, IncrementingDiagnosticClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
        }
        else
        {
            services.AddSingleton<ISpeedTestService>(sp => CreateSpeedTestService(sp.GetRequiredService<SpeedTestSettingsAccessor>().Settings));
            services.AddSingleton<IClock, Clock>();
            services.AddSingleton<IClientInfoProvider, ClientInfoProvider>();
            services.AddSingleton<IEnvironmentInfoProvider, EnvironmentInfoProvider>();
            services.AddSingleton<IDiagnosticClock, DiagnosticClock>();
            services.AddSingleton<IWaiter, Waiter>();
        }

        await using var serviceProvider = services.BuildServiceProvider();

        using var cancellationTokenSource = new CancellationTokenSource();

        void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs)
        {
            // Cancel gracefully the first time, abort the process on the second Ctrl+C.
            eventArgs.Cancel = !cancellationTokenSource.IsCancellationRequested;
            cancellationTokenSource.Cancel();
        }

        // CancelKeyPress is process-wide, so the handler must come off before the token source it
        // closes over is disposed.
        System.Console.CancelKeyPress += OnCancelKeyPress;

        try
        {
            return await RunAsync(
                serviceProvider,
                args!.Where(s => !s.Equals("--test")).ToArray(),
                cancellationTokenSource.Token);
        }
        finally
        {
            System.Console.CancelKeyPress -= OnCancelKeyPress;
        }
    }

    internal static async Task<int> RunAsync(IServiceProvider serviceProvider, string[] args, CancellationToken cancellationToken = default)
    {
        var ansiConsole = serviceProvider.GetRequiredService<IAnsiConsole>();

        // Settled before anything is drawn, because --help is rendered below without the options
        // ever being parsed, so a theme arriving later would not reach it.
        var selection = serviceProvider.GetRequiredService<IThemeProvider>().Resolve();
        selection.Theme.ApplyTo(ansiConsole);

        var errorOutput = serviceProvider.GetRequiredService<ErrorOutput>();
        errorOutput.ApplyTheme(selection.Theme);

        var rootCommand = CreateRootCommand(serviceProvider, args, selection.Theme);
        var parseResult = rootCommand.Parse(args);
        ReportUnrecognisedTheme(serviceProvider, parseResult, selection);

        if (args.Length > 0 && (args[0] is "-v" or "--version"))
        {
            ansiConsole.WriteLine(AppVersion.Current);
            return 0;
        }

        // Help is recognised at position 0 or straight after a subcommand name, and nowhere else,
        // so this interception stays simple (CLAUDE.md, CLI Help Behaviour).
        if (args.Length > 0 && (args[0] is "-h" or "--help" or "-?"))
        {
            CustomHelpProvider.RenderHelp(ansiConsole, rootCommand, selection.Theme);
            return 0;
        }

        if (args.Length > 1 && (args[1] is "-h" or "--help" or "-?"))
        {
            var subcommandName = args[0];
            var subcommand = rootCommand.Subcommands.FirstOrDefault(c => c.Name == subcommandName);
            if (subcommand != null)
            {
                CustomHelpProvider.RenderHelp(ansiConsole, subcommand, selection.Theme);
                return 0;
            }
        }

        // Shown even in quiet mode.
        if (parseResult.Errors.Count > 0)
        {
            foreach (var error in parseResult.Errors)
            {
                errorOutput.WriteLine(selection.Theme, error.Message);
            }
            return 1;
        }

        return await parseResult.InvokeAsync(cancellationToken: cancellationToken);
    }

    // Called before --help and --version return, so a user who typo'd the variable hears about it
    // whichever command they ran. Quiet is read from the parse rather than the raw arguments, so
    // every spelling the parser accepts - a separate false, a bundled -tq, a response file - means
    // here what it means to the run.
    private static void ReportUnrecognisedTheme(IServiceProvider serviceProvider, ParseResult parseResult, ThemeSelection selection)
    {
        if (selection.UnrecognisedValue is not { } requested) return;
        if (AsksForQuiet(parseResult)) return;

        // Named from what the run settled on rather than assumed to be the default: NO_COLOR
        // outranks the variable, so an unreadable name can still end in the plain theme.
        serviceProvider.GetRequiredService<NoticeOutput>().Writer.WriteLine(
            $"{EnvironmentThemeProvider.ThemeVariable} is set to '{requested}', which is not a theme. Using the {selection.Theme.Name} theme. Valid themes: {Theme.NameList}.");
    }

    // Only the root command has --quiet, so a subcommand run is never quiet.
    private static bool AsksForQuiet(ParseResult parseResult)
    {
        var quiet = parseResult.RootCommandResult.Command.Options.OfType<Option<bool>>().Single(option => option.Name == "--quiet");
        return parseResult.GetResult(quiet) is { Errors: var errors } && !errors.Any() && parseResult.GetValue(quiet);
    }
}
