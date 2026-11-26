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
    /// <summary>
    /// The application description
    /// </summary>
    internal const string Description = "Network speed tester including server discovery, latency measurement, download and upload speed testing.";

    /// <summary>
    /// The fewest connections <c>--connections</c> accepts: a single transfer at a time.
    /// </summary>
    internal const int MinimumConnections = 1;

    /// <summary>
    /// The most connections <c>--connections</c> accepts: never more than the heaviest profile
    /// already asks for, which today is Mega's 32.
    /// </summary>
    internal const int MaximumConnections = 32;

    /// <summary>
    /// How long one speed test run may take when <c>--time-limit</c> is not given. On by default,
    /// so a scheduled run is bounded without the user having to configure anything.
    /// </summary>
    internal static readonly TimeSpan DefaultTimeLimit = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The one form <c>--time-limit</c> accepts, exactly hours, minutes and seconds. Shared by the
    /// parser and <see cref="DefaultTimeLimitText"/>, so <c>--help</c> and the parser cannot come
    /// to disagree about what the default is.
    /// </summary>
    private const string TimeLimitFormat = @"hh\:mm\:ss";

    /// <summary>
    /// <see cref="DefaultTimeLimit"/> written the way the option accepts it.
    /// </summary>
    internal static string DefaultTimeLimitText => DefaultTimeLimit.ToString(TimeLimitFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// Create the RootCommand with System.CommandLine.
    /// </summary>
    /// <remarks>
    /// Extracted here so the testing project can reuse the production configuration.
    /// </remarks>
    internal static RootCommand CreateRootCommand(IServiceProvider serviceProvider, string[] args, Theme theme)
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

        // Taken as a string rather than an Option<TimeSpan> so that every rejected value lands on
        // one message naming the form that works. An Option<TimeSpan> reads a bare 600 as six
        // hundred days and binds it without complaint, which is the one wrong answer a scheduled
        // run has no way of noticing.
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
            Description = "Do not perform latency test.\nWhen used without --server-url, the first server listed is selected.",
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
            Description = $"The kind of server to test against. {OptionValues.Providers.HelpList}\n'librespeed' needs --server-url or --server-file naming where LibreSpeed is installed.",
            DefaultValueFactory = _ => Provider.Ookla,
            CustomParser = result => OptionValues.Providers.Parse(result, "--provider")
        };

        var serverUrlOption = new Option<string>("--server-url")
        {
            Description = "The URL of a specific speed test server.\n'netpace servers -l' will return your nearest servers.\nWith --provider librespeed, the address LibreSpeed is installed at.",
            DefaultValueFactory = _ => string.Empty
        };

        var serverFileOption = new Option<string>("--server-file")
        {
            Description = "A JSON file listing the servers to choose from, in place of Ookla's nearby list.\nEach entry gives a 'url' and an optional 'name', shown as the server's sponsor.\nNot with --server-url, which names one server outright.",
            DefaultValueFactory = _ => string.Empty
        };
        serverFileOption.Validators.Add(result =>
        {
            // Only the implicit default stands for "no file named". A blank value the user did pass
            // reads as that downstream, so the run would answer about Ookla's nearby list instead.
            if (!result.Implicit && string.IsNullOrWhiteSpace(result.GetValueOrDefault<string>()))
            {
                result.AddError("--server-file needs a path: the value given names no file.");
            }
        });

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

        // Taken as a string rather than an Option<int> so that every rejected value - zero, above
        // the ceiling, or not a whole number at all - lands on one message naming the valid range.
        // An Option<int> would answer "2.5" with a built-in parse failure that never mentions it.
        var connectionsOption = new Option<string>("--connections")
        {
            Description = $"Maximum transfers the download and upload tests each run at once ({MinimumConnections}-{MaximumConnections}).\nOverrides the --profile connection count; the profile's data caps still apply.\nA maximum, so a profile with fewer transfers to run than this will not reach it.",
            DefaultValueFactory = _ => string.Empty
        };
        connectionsOption.Validators.Add(result =>
        {
            // The validator also sees the implicit result the default value produces, which stands
            // for "the profile decides" and is not a value the user asked to be checked.
            if (result.Implicit)
            {
                return;
            }

            var value = result.GetValueOrDefault<string>();
            if (!TryParseConnections(value, out var connections) || connections < MinimumConnections || connections > MaximumConnections)
            {
                result.AddError(ConnectionsError(value));
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

        // Pinned rather than left to the default so that each occurrence takes exactly one
        // key=value argument: a stray second word is then an unrecognised argument the user is told
        // about, not a silently swallowed pair.
        var customFieldOption = new Option<string[]>("--custom-field")
        {
            Description = $"Record a {CustomField.ExpectedForm} pair in CSV and JSON output.\nMay be given more than once, and is recorded in the order given.",
            AllowMultipleArgumentsPerToken = false
        };
        // A validator, not a check at binding time, so a malformed pair fails the parse: the run
        // stops before any test is performed in every output mode, including the ones that ignore
        // custom fields altogether. A missing argument is already the parser's own error.
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

        // --server has been renamed --server-url, but the old name stays registered so that a
        // script still passing it is told what to use rather than getting a generic
        // unrecognised-option error. It takes an optional string so that a bare --server lands on
        // the same message instead of a built-in missing-argument failure. Hidden, and
        // CustomHelpProvider strips hidden options, so --help advertises only the new name.
        var serverOption = new Option<string>("--server")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Hidden = true
        };
        serverOption.Validators.Add(result => result.AddError("--server has been renamed --server-url, see --help."));

        // --verbosity is retired, but stays registered so that a script still passing it gets a
        // message naming the removal rather than a generic unrecognised-option error. It takes an
        // optional string so that every former value - and no value at all - lands on the same
        // error instead of a built-in parse failure. Hidden, and CustomHelpProvider strips hidden
        // options, so --help does not advertise it.
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
            Description = $"Exit {ExitCode.MissedTarget} when the download speed is below this, such as {ThresholdOption.SpeedExample}.\nTakes any unit NetPace writes a speed in, whatever --unit, --unit-system and --unit-scale say.\nChecks one run, so not with --loop, --count above 1, or --no-download.",
            DefaultValueFactory = _ => null,
            CustomParser = result => ThresholdOption.ParseSpeed(result, "--min-download")
        };

        var minUploadOption = new Option<MeasurementThreshold?>("--min-upload")
        {
            Description = $"Exit {ExitCode.MissedTarget} when the upload speed is below this, such as {ThresholdOption.SpeedExample}.\nRead the same way as --min-download; not with --loop, --count above 1, or --no-upload.",
            DefaultValueFactory = _ => null,
            CustomParser = result => ThresholdOption.ParseSpeed(result, "--min-upload")
        };

        var maxLatencyOption = new Option<MeasurementThreshold?>("--max-latency")
        {
            Description = $"Exit {ExitCode.MissedTarget} when the idle latency is above this, such as {ThresholdOption.LatencyExample}.\nMilliseconds only; not with --loop, --count above 1, or --no-latency.",
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
            // The implicit result the default value produces stands for "no file named" and is not
            // a value the user asked to be checked. A value the user did pass has to name a path:
            // every consumer downstream reads a blank one as "no file named", so letting it
            // through would exit zero having written no records and said nothing, and the user
            // would find out only on going to look for the file. --diagnostics-file "$LOG" with
            // LOG unset is the ordinary way to arrive here.
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
            Description = $"Exit {ExitCode.FailOnTriggered} on a failed measurement. {OptionValues.FailOnModes.HelpList}\nWith 'none' the exit code is never affected; 'total' triggers when a test is all-failed;\n'partial' triggers on any failed request. Fail-fast across --count and --loop.",
            DefaultValueFactory = _ => FailOn.None,
            CustomParser = result => OptionValues.FailOnModes.Parse(result, "--fail-on")
        };

        // Add options
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
        command.Options.Add(serverFileOption);
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

        // Set command action
        command.SetAction((Func<ParseResult, CancellationToken, Task<int>>)(async (parseResult, cancellationToken) =>
        {
            // Declared out here so the diagnostics file is closed however the run ends, including
            // the throws the catch below turns into exit code 1.
            TextWriter? diagnosticsFile = null;

            try
            {
                // Get option values and populate settings
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
                    ServerFile = parseResult.GetValue(serverFileOption) ?? string.Empty,
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

                // Validate settings
                settings.Validate();

                // Read only now the command line has passed: a server file is part of the
                // environment the run met, so a failure to read it is an operational fault, and a
                // run that is both unusable and names an unreadable file must still report the
                // usage error that any machine would have given it.
                settings = settings with { ListedServers = ServerFileReader.Read(settings.ServerFile) };

                // Build the provider's settings from --provider, --profile and the per-cap
                // overrides. The settings accessor is the test seam: production resolves the
                // provider from it; tests inspect Settings post-run to verify CLI → settings
                // binding.
                var accessor = serviceProvider.GetRequiredService<SpeedTestSettingsAccessor>();
                accessor.Settings = BuildSpeedTestSettings(settings);

                // Get services from DI
                var ansiConsole = serviceProvider.GetRequiredService<IAnsiConsole>();
                var errorOutput = serviceProvider.GetRequiredService<ErrorOutput>();
                var speedTestService = serviceProvider.GetRequiredService<ISpeedTestService>();
                var clock = serviceProvider.GetRequiredService<IClock>();
                var clientInfoProvider = serviceProvider.GetRequiredService<IClientInfoProvider>();
                var waiter = serviceProvider.GetRequiredService<IWaiter>();
                var runTimeLimit = serviceProvider.GetRequiredService<IRunTimeLimit>();
                var liveDisplayRefresh = serviceProvider.GetRequiredService<ILiveDisplayRefresh>();

                // Switched off, nothing is recorded and nothing is written - so no call site
                // downstream has to ask whether diagnostics are on. A named file switches them on
                // by itself, so --diagnostics alongside it asks for nothing more; the file wins
                // either way, and the error stream carries no record at all.
                //
                // The file is opened here, before the first test and inside this action's own error
                // handling, so a path that cannot be written ends the run with the path named
                // rather than surfacing part way through a measurement.
                IDiagnosticRecorder recorder = NullDiagnosticRecorder.Instance;

                if (!string.IsNullOrWhiteSpace(settings.DiagnosticsFile))
                {
                    // The reason is wrapped rather than reported raw: the framework's own message
                    // says what went wrong but not which file it was about, and a run may name two.
                    // Wrapping also makes the path named whatever the failure was, which the
                    // message for a path too long to open does not do on its own.
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

                // Recorded once per process, before the first test: what NetPace is, what it is
                // running on, and how it was asked to run.
                var environmentInfoProvider = serviceProvider.GetRequiredService<IEnvironmentInfoProvider>();
                recorder.Record(
                    "run.start",
                    ("version", environmentInfoProvider.GetVersion()),
                    ("runtime", environmentInfoProvider.GetRuntime()),
                    ("os", environmentInfoProvider.GetOperatingSystem()),
                    ("arch", environmentInfoProvider.GetArchitecture()));
                recorder.Record("run.invocation", ("args", string.Join(' ', args)));

                // Create and execute command
                var command = new SpeedTestCommand(ansiConsole, errorOutput, serviceProvider.GetRequiredService<NoticeOutput>(), speedTestService, clock, clientInfoProvider, recorder, waiter, runTimeLimit, liveDisplayRefresh, theme);
                return await command.ExecuteAsync(settings, cancellationToken);
            }
            catch (Exception ex)
            {
                // Usage errors and operational failures go to the error stream, so a redirect of
                // the result never swallows them and no reader of the result ever meets a line
                // that is not part of its format. Resolved again here rather than reused from
                // above, because the throw may have come before that line ran.
                serviceProvider.GetRequiredService<ErrorOutput>().WriteLine(theme, ex.Message);
                return ExitCodeFor(ex);
            }
            finally
            {
                // Closed after the command has returned, so the last flush it makes is on disk.
                //
                // Guarded because closing a StreamWriter flushes what it still holds, and this
                // finally sits outside the catch above: a raise here escapes the action altogether,
                // which turned a completed measurement into a stack trace and a non-zero exit. A
                // diagnostics write must not decide the run's fate, which is the same rule
                // SpeedTestCommand.FlushDiagnostics keeps. Nothing is written about it because the
                // records are flushed explicitly after every batch, so a close that fails follows a
                // flush that failed, and the user has already been told which path and why.
                try
                {
                    diagnosticsFile?.Dispose();
                }
                catch (IOException)
                {
                }
            }
        }));

        // Add list servers command
        var listServersCommand = CreateListServersCommand(serviceProvider, theme);
        command.Subcommands.Add(listServersCommand);

        return command;
    }

    /// <summary>
    /// Build the provider's <see cref="SpeedTestSettings"/> from the parsed CLI settings.
    /// <c>--provider</c> picks which record is built; everything after that is provider-neutral and
    /// is applied once, to the shared half of it. Profile sets per-request shape and cap defaults;
    /// explicit --downloadsize / --uploadsize override only the cap via with-expressions, and
    /// --connections overrides only the number of transfers each direction runs at once.
    /// <c>--simple</c> and <c>--minimal</c> additionally switch the under-load latency probes off,
    /// because neither format shows an under-load figure, and <c>--no-latency</c> does because it
    /// asks for no latency at all.
    /// </summary>
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
            // --simple and --minimal report no under-load figure, so the probes that would measure
            // one are traffic nobody reads. Switching them off keeps --simple's traffic as it was
            // before the probes existed; the idle latency test still runs, being where the
            // latency figure comes from. --no-latency switches them off for the opposite reason:
            // asking for no latency asks for none anywhere, so no probe goes out under a transfer
            // either, in whichever format the run reports.
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

    /// <summary>
    /// The provider to measure with, chosen from the kind of settings record it was given.
    /// </summary>
    /// <remarks>
    /// Reading the provider off the record leaves nothing to disagree with it: a run cannot end up
    /// with LibreSpeed settings in an Ookla provider.
    /// </remarks>
    internal static ISpeedTestService CreateSpeedTestService(SpeedTestSettings settings) => settings switch
    {
        OoklaSpeedtestSettings ookla => new OoklaSpeedtest(ookla),
        LibreSpeedtestSettings libreSpeed => new LibreSpeedtest(libreSpeed),
        _ => throw new ArgumentOutOfRangeException(nameof(settings), settings.GetType().Name, "No speed test provider is registered for these settings."),
    };

    /// <summary>
    /// Parse a <c>--time-limit</c> value the way its validator does: hours, minutes and seconds,
    /// culture independent, and a duration of more than nothing.
    /// </summary>
    /// <remarks>
    /// Exactly <c>hh:mm:ss</c>, because <see cref="TimeSpan"/>'s own parsing reads shorter forms
    /// as something else: <c>600</c> as six hundred days and <c>10:00</c> as ten hours, so a user
    /// asking for ten minutes would get no limit worth the name. <c>--delay</c> accepts any
    /// <see cref="TimeSpan"/> form; only this option is strict.
    /// </remarks>
    private static bool TryParseTimeLimit(string? value, out TimeSpan limit) =>
        TimeSpan.TryParseExact(value, TimeLimitFormat, CultureInfo.InvariantCulture, out limit) && limit > TimeSpan.Zero;

    /// <summary>
    /// The requested run time limit. A value that is present but will not parse means the
    /// validator never ran, and throwing beats bounding a run by a duration nobody asked for.
    /// </summary>
    private static TimeSpan ParseTimeLimit(string? value) =>
        TryParseTimeLimit(value, out var limit)
            ? limit
            : throw new UsageException(TimeLimitError(value));

    /// <summary>
    /// What a <c>--time-limit</c> value that will not parse is reported as, by the validator and by
    /// <see cref="ParseTimeLimit"/> alike.
    /// </summary>
    private static string TimeLimitError(string? value) =>
        $"--time-limit must be written as HH:MM:SS, from 00:00:01 to 23:59:59, such as {DefaultTimeLimitText}, but was '{value}'.";

    /// <summary>
    /// Parse a <c>--connections</c> value the way its validator does: digits only, culture
    /// independent, so the accepted set is exactly the whole number the help text promises.
    /// </summary>
    private static bool TryParseConnections(string? value, out int connections) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out connections);

    /// <summary>
    /// The requested connection count, or <c>null</c> when <c>--connections</c> was not given so
    /// the profile's own counts stand. Absent is the only silent case: a value that is present but
    /// will not parse means the validator never ran, and throwing beats reporting a speed for a
    /// load nobody asked for.
    /// </summary>
    private static int? ParseConnections(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;

        return TryParseConnections(value, out var connections)
            ? connections
            : throw new UsageException(ConnectionsError(value));
    }

    /// <summary>
    /// What a <c>--connections</c> value the run cannot use is reported as, by the validator and by
    /// <see cref="ParseConnections"/> alike.
    /// </summary>
    private static string ConnectionsError(string? value) =>
        $"--connections must be a whole number from {MinimumConnections} to {MaximumConnections}, but was '{value}'.";

    /// <summary>
    /// Create the list servers command.
    /// </summary>
    private static Command CreateListServersCommand(IServiceProvider serviceProvider, Theme theme)
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
                var errorOutput = serviceProvider.GetRequiredService<ErrorOutput>();
                var speedTestService = serviceProvider.GetRequiredService<ISpeedTestService>();

                // Create and execute command
                var command = new ListServersCommand(ansiConsole, errorOutput, speedTestService, theme);
                return await command.ExecuteAsync(settings, cancellationToken);
            }
            catch (Exception ex)
            {
                // Usage errors and operational failures go to the error stream. Network and
                // discovery outcomes never reach here: ListServersCommand reports those itself and
                // leaves the exit code at 0, as a speed test does.
                serviceProvider.GetRequiredService<ErrorOutput>().WriteLine(theme, ex.Message);
                return ExitCodeFor(ex);
            }
        });

        return command;
    }

    /// <summary>
    /// Which code a command exits with for a failure that reached its top-level handler: the
    /// command line's own fault, or NetPace's.
    /// </summary>
    /// <remarks>
    /// Both commands' handlers decide it here, so the two cannot come to disagree about what a code
    /// means. Anything that is not a <see cref="UsageException"/> is an operational fault, which is
    /// the safer reading of an exception nobody anticipated.
    /// </remarks>
    private static int ExitCodeFor(Exception exception) =>
        exception is UsageException ? ExitCode.UsageError : ExitCode.OperationalFault;

    /// <summary>
    /// Registers the three outputs that go to the process error stream rather than to the result:
    /// a notice about the way the run was asked for, an error, and the diagnostic records.
    /// </summary>
    /// <remarks>
    /// Its own method so a test can resolve these three and assert which stream each carries;
    /// built inline in <see cref="Main"/>, the one line that decides it was reachable by nothing.
    /// </remarks>
    /// <param name="services">The collection to register the outputs in.</param>
    internal static void AddStreamOutputs(IServiceCollection services)
    {
        // A word to the user that is neither the result nor an error goes to the error stream, so
        // it never lands in the middle of a result another program is reading.
        services.AddSingleton(new NoticeOutput(System.Console.Error));

        // Every error goes to the error stream, for the same reason and one more: a run whose
        // result is redirected still shows the user what went wrong. Its own console rather than
        // the result's, so --quiet and --file never reach it.
        services.AddSingleton(CreateErrorOutput(
            System.Console.Error,
            isRedirected: System.Console.IsErrorRedirected,
            isWindows: OperatingSystem.IsWindows()));

        // Diagnostics go to the error stream, never through the Spectre console: one record per
        // line, whatever the terminal width, and whatever --quiet or --file were set to.
        services.AddSingleton(new DiagnosticOutput(System.Console.Error));
    }

    /// <summary>
    /// Builds the console every error is written to, over the given stream.
    /// </summary>
    /// <remarks>
    /// How ANSI support is settled is <see cref="ErrorAnsiSupport"/>'s decision, and the platform
    /// is a parameter rather than read here so that a test fixes it instead of inheriting the
    /// machine it runs on. The <c>styled</c> argument below is what keeps a captured error log to
    /// the words: where the stream is redirected, errors are not styled at all.
    /// <para>
    /// Detection only ever reaches the error handle because <paramref name="writer"/> is
    /// <see cref="System.Console.Error"/> itself. Spectre.Console picks the handle to probe by
    /// reference-comparing the writer against the standard streams, so handing this method any
    /// other writer with <paramref name="isWindows"/> set would send detection to standard
    /// output - the one handle the error stream exists to leave alone.
    /// </para>
    /// </remarks>
    /// <param name="writer">The stream errors are written to.</param>
    /// <param name="isRedirected">Whether that stream goes somewhere other than a terminal.</param>
    /// <param name="isWindows">Whether the run is on Windows.</param>
    internal static ErrorOutput CreateErrorOutput(TextWriter writer, bool isRedirected, bool isWindows) =>
        new(
            AnsiConsole.Create(new AnsiConsoleSettings
            {
                Out = new AnsiConsoleOutput(writer),
                Ansi = ErrorAnsiSupport(isWindows, isRedirected),
                ColorSystem = ColorSystemSupport.Detect
            }),
            styled: !isRedirected);

    /// <summary>
    /// Whether Spectre.Console detects the error stream's ANSI support or is told it outright.
    /// </summary>
    /// <remarks>
    /// Platform-specific: a Windows terminal detects, everything else is told.
    /// <para>
    /// Told outright, Spectre.Console writes colour as escape sequences to the writer it was
    /// given, and to no other stream. That is what the error stream needs almost everywhere. But
    /// being told also means Spectre.Console never asks to switch the mode on. It may still read
    /// the handle, and only detection passes the flag that upgrades it, which on Windows is what
    /// switches virtual terminal processing on. A classic console window renders escape sequences
    /// only once something has switched that mode on. Normally the console that writes the result
    /// does it, and the error stream benefits because both share the window; when the result is
    /// redirected or piped, nothing does, and the error line shows literal escape characters. So
    /// on Windows, where the error stream is a terminal, support is detected: Spectre.Console
    /// reads the error handle and switches the mode on. Where that call fails, it falls back to
    /// setting colour through System.Console's own colour properties, which on Windows write no
    /// bytes to any stream; they set the standard OUTPUT handle's colour attributes, so where
    /// that handle is a redirected file the call fails and the error simply prints unstyled.
    /// </para>
    /// <para>
    /// Detecting is unsafe anywhere else. On Unix that same fallback is reached whenever
    /// <c>TERM</c> is not one Spectre.Console recognises, and .NET then writes those colour
    /// properties to standard OUTPUT wherever that is a terminal, stamping the error console's
    /// colours onto the live result. The trigger is not an incapable terminal: a terminal that
    /// cannot take escape sequences writes nothing at all, while a capable one Spectre.Console's
    /// list has not caught up with is exactly the case that spills. A redirected standard stream
    /// is read as unable to take escape sequences before <c>TERM</c> or the console mode is
    /// consulted at all, reaching the fallback on every platform. Both keep the forced answer.
    /// </para>
    /// </remarks>
    /// <param name="isWindows">Whether the run is on Windows.</param>
    /// <param name="isRedirected">Whether the error stream goes somewhere other than a terminal.</param>
    /// <returns>What to set <see cref="AnsiConsoleSettings.Ansi"/> to.</returns>
    internal static AnsiSupport ErrorAnsiSupport(bool isWindows, bool isRedirected) =>
        isWindows && !isRedirected ? AnsiSupport.Detect : AnsiSupport.Yes;

    /// <summary>
    /// Run the command line application with the given arguments.
    /// </summary>
    public async static Task<int> Main(string[] args)
    {
        // Setup DI
        var services = new ServiceCollection();

        // Register AnsiConsole (standard output).
        services.AddSingleton(AnsiConsole.Console);

        // The theme comes from the environment in every run, --test included: a theme is the one
        // thing about a run a stub would make harder to look at rather than easier.
        services.AddSingleton<IThemeProvider, EnvironmentThemeProvider>();

        AddStreamOutputs(services);

        // Needed by both stacks: --diagnostics-file writes a real file whether the measurement
        // behind it was stubbed or not.
        services.AddSingleton<IDiagnosticFileOpener, DiagnosticFileOpener>();

        // Needed by both stacks: the root command populates the accessor after option binding,
        // before it knows which ISpeedTestService is in play.
        services.AddSingleton<SpeedTestSettingsAccessor>();

        // Needed by both stacks: a person running either one wants the live displays to redraw as
        // the measurement goes. Only the test host turns that off, so a capture holds one frame.
        services.AddSingleton<ILiveDisplayRefresh, LiveDisplayRefresh>();

        // Needed by both stacks: a run is bounded by a real clock whichever speed tester is behind
        // it, --test included. Only the test host replaces it, so a suite does not wait the limit
        // out to watch it fire.
        services.AddSingleton<IRunTimeLimit, RunTimeLimit>();

        if (args != null && args.Contains("--test"))
        {
            // Executes NetPace against stub service implementations.
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
            // Try to cancel gracefully the first time, then abort the process the second time Ctrl+C is pressed
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
        // ever being parsed, and a theme that arrived later would not reach it. The error stream is
        // themed here too, so an error is drawn the way the rest of the run is.
        var selection = serviceProvider.GetRequiredService<IThemeProvider>().Resolve();
        selection.Theme.ApplyTo(ansiConsole);

        var errorOutput = serviceProvider.GetRequiredService<ErrorOutput>();
        errorOutput.ApplyTheme(selection.Theme);

        var rootCommand = CreateRootCommand(serviceProvider, args, selection.Theme);
        var parseResult = rootCommand.Parse(args);
        ReportUnrecognisedTheme(serviceProvider, parseResult, selection);

        // Check for version request before parsing
        if (args.Length > 0 && (args[0] is "-v" or "--version"))
        {
            ansiConsole.WriteLine(AppVersion.Current);
            return 0;
        }

        // Check for help request before parsing
        if (args.Length > 0 && (args[0] is "-h" or "--help" or "-?"))
        {
            CustomHelpProvider.RenderHelp(ansiConsole, rootCommand, selection.Theme);
            return 0;
        }

        // Check for subcommand help
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

        // Handle parse errors - these should be shown even in quiet mode
        if (parseResult.Errors.Count > 0)
        {
            foreach (var error in parseResult.Errors)
            {
                errorOutput.WriteLine(selection.Theme, error.Message);
            }
            return ExitCode.UsageError;
        }

        return await parseResult.InvokeAsync(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Tells the user, once per run, that the theme they asked for does not exist and which ones
    /// do. Nothing is written when the value named a theme, or named nothing.
    /// </summary>
    /// <remarks>
    /// Written before <c>--help</c> and <c>--version</c> return, so a user who typo'd the variable
    /// hears about it whichever command they ran. Whether the run is quiet is read from the parse
    /// rather than the raw arguments, so every spelling the parser accepts — a separate
    /// <c>false</c>, a bundled <c>-tq</c>, a response file — means here what it means to the run.
    /// </remarks>
    private static void ReportUnrecognisedTheme(IServiceProvider serviceProvider, ParseResult parseResult, ThemeSelection selection)
    {
        if (selection.UnrecognisedValue is not { } requested) return;
        if (AsksForQuiet(parseResult)) return;

        // The theme is named from what the run settled on rather than assumed to be the default:
        // NO_COLOR outranks the variable, so an unreadable name can still end in the plain theme.
        serviceProvider.GetRequiredService<NoticeOutput>().Writer.WriteLine(
            $"{EnvironmentThemeProvider.ThemeVariable} is set to '{requested}', which is not a theme. Using the {selection.Theme.Name} theme. Valid themes: {Theme.NameList}.");
    }

    /// <summary>
    /// Whether the parsed run asked for quiet. Only the root command has <c>--quiet</c>, so a
    /// subcommand run is never quiet.
    /// </summary>
    private static bool AsksForQuiet(ParseResult parseResult)
    {
        var quiet = parseResult.RootCommandResult.Command.Options.OfType<Option<bool>>().Single(option => option.Name == "--quiet");
        return parseResult.GetResult(quiet) is { Errors: var errors } && !errors.Any() && parseResult.GetValue(quiet);
    }
}
