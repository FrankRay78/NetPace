 Code Review Results

  The branch migrates the CLI framework from Spectre.Console.Cli to System.CommandLine. Build is clean, all 530 tests pass. The overall approach is sound.

  Critical Issues

  1. ServiceProvider never disposed (Program.cs:343) — services.BuildServiceProvider() returns an IDisposable that's never cleaned up, leaking any disposable
  singletons. Fix: await using var serviceProvider = services.BuildServiceProvider();
  2. Bare catch swallowing all exceptions (CustomHelpProvider.cs:215) — Catches everything including OutOfMemoryException. Should catch Exception at minimum, or
   specific expected exception types.
  3. Leftover Spectre.Console.Cli NuGet refs in test project (NetPace.Console.Tests.csproj:11-12) — The alpha packages Spectre.Console.Cli and
  Spectre.Console.Cli.Testing are still referenced but no longer used after the migration.
  4. CSVDelimiter changed from char to string with no length validation (SpeedTestCommandSettings.cs:33) — Allows multi-character delimiters despite the
  description saying "single character". Needs a Validate() check.

  Important Issues

  5. Fragile positional help/version detection (Program.cs:366-393) — Manually checks args[0]/args[1] for -h/--help/--version rather than scanning all args.
  netpace --csv --help won't show help.
  6. bool? for non-nullable settings (ListServersCommandSettings.cs) — ShowLatency and Fastest are bool? but always set from a non-nullable Option<bool>,
  leading to verbose triple-null-checks in ListServersCommand.cs:19.
  7. CommandLineTestHost.RunAsync mutates shared IServiceCollection (CommandLineTestHost.cs:33) — Calling RunAsync twice on the same instance would register
  IAnsiConsole twice.
  8. Tests disabled via #if FALSE (FileConsoleTests.cs:1) — FileConsoleTests are silently compiled out. Should use [Fact(Skip = "...")] so they appear as
  skipped in test results.
  9. Dead null-coalescing after .ToArray() (Program.cs:356) — .ToArray() never returns null, making ?? Array.Empty<string>() dead code; also the args!
  null-forgiving operator doesn't protect against a runtime null.