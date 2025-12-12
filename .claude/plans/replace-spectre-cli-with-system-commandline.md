# Replace Spectre.Console.Cli with System.CommandLine for AOT Compatibility

**Date**: 2025-12-12
**Status**: Ready to Implement
**Branch**: `replace-spectre-console-cli-with-aot-trimmable-alternative`
**Estimated Effort**: 2-3 weeks

---

## Executive Summary

**Finding**: Spectre.Console.Cli is **NOT AOT-compatible** (explicitly marked with `RequiresDynamicCodeAttribute`). Your AOT investigation confirmed that compilation fails completely with IL3050 warnings.

**Recommendation**: Replace with **System.CommandLine** - Microsoft's official, AOT-compatible CLI framework (GA since .NET 9).

**User Decisions**:
- ✅ Replace in existing NetPace.Console (not separate NetPace.IoT project)
- ✅ Use System.CommandLine framework
- ✅ Critical requirement: Preserve CommandAppTester testing pattern

**Extent of Rework**:
- **Production**: 8 files (~500-800 LOC changes)
- **Tests**: 9 files, 83+ tests (~1,000-1,500 LOC changes)
- **Key Challenge**: Build CommandLineTestHost wrapper to replace CommandAppTester
- **Estimated Effort**: 14-21 days (2-3 weeks)

---

## Investigation Results

### Current Spectre.Console.Cli Usage

#### Production Code (8 files)

**Commands & Settings** (4 files):
- `Commands/SpeedTestCommand.cs` - Inherits `AsyncCommand<SpeedTestCommandSettings>`
- `Commands/ListServersCommand.cs` - Inherits `AsyncCommand<ListServersCommandSettings>`
- `Commands/SpeedTestCommandSettings.cs` - 27 command options with `[CommandOption]` attributes
- `Commands/ListServersCommandSettings.cs` - 2 command options

**DI Integration** (2 files):
- `DependencyInjection/TypeRegistrar.cs` - Implements `ITypeRegistrar` (bridges to MS.Extensions.DI)
- `DependencyInjection/TypeResolver.cs` - Implements `ITypeResolver`

**Help Customization** (1 file):
- `CustomHelpProvider.cs` - Extends `HelpProvider` with ASCII art header and GitHub footer

**Bootstrap** (1 file):
- `Program.cs` - Creates `CommandApp<SpeedTestCommand>`, configures via `IConfigurator`

**Global Usings** (1 file):
- `Properties/Usings.cs` - `global using Spectre.Console.Cli;`

#### Test Code (9 files, 83+ tests)

**Test Infrastructure**:
- Uses `CommandAppTester` from `Spectre.Console.Cli.Testing`
- 74 instances across 8 test files
- Pattern: `await app.RunAsync(args) → result.ExitCode + result.Output`
- Verify snapshot testing for output validation

**Test Files**:
- `NetPaceConsoleTests.cs` (27 tests)
- `NetPaceConsoleTests.CSV.cs` (21 tests)
- `NetPaceConsoleTests.Json.cs` (10 tests)
- `NetPaceConsoleTests.Servers.cs` (5 tests)
- `NetPaceConsoleTests.Quiet.cs` (9 tests)
- `NetPaceConsoleTests.File.cs` (11 tests)
- `FileConsoleTests.cs`
- `Properties/Usings.cs`

#### Command Options Inventory (29 total)

**SpeedTestCommand** (27 options):
- `--loop`, `--count`, `--delay`
- `--csv`, `--csv-delimiter`, `--csv-header-units`
- `--json`, `--json-pretty`
- `--no-latency`, `--no-download`, `--no-upload`
- `--server`
- `-t|--timestamp`, `--datetimeformat`
- `--downloadsize`, `--uploadsize`
- `-u|--unit`, `--unit-scale`, `--unit-system`
- `--verbosity`
- `-f|--file`, `--file-mode`
- `-q|--quiet`

**ListServersCommand** (2 options):
- `-l|--latency`
- `-f|--fastest`

**Custom Validation**:
- Cross-option validation in `SpeedTestCommandSettings.Validate()`
- Example: CSV header units incompatible with auto-scale + looping

---

## CLI Framework Alternatives Evaluated

### System.CommandLine (Microsoft) ✅ SELECTED

**AOT Support**: ✅ Full (GA since .NET 9, AOT from beta 4)
**API Style**: Fluent builder pattern
**Testing**: `TestConsole` class (requires wrapper)
**DI Support**: ✅ Via Microsoft.Extensions.Hosting
**Help Generation**: ✅ Built-in, customizable
**Validation**: ✅ Built-in
**Maintenance**: Microsoft-maintained, active development

**Pros**:
- ✅ Official Microsoft library
- ✅ Similar API to Spectre.Console.Cli
- ✅ Full AOT and trimming support
- ✅ Good documentation
- ✅ Zero breaking changes to NetPace.Core

**Cons**:
- ⚠️ Currently beta (2.0.0-beta5), but GA with .NET 9
- ⚠️ TestConsole is lower-level than CommandAppTester (needs wrapper)

### ConsoleAppFramework (Cysharp)

**AOT Support**: ✅ Full (Zero reflection, source-generated)
**API Style**: Lambda-based
**Testing**: Benchmark-focused
**DI Support**: ✅ Basic type-based registration
**Help Generation**: ✅ From XML docs

**Pros**:
- ✅ Explicitly AOT-safe
- ✅ Smallest binaries (zero dependencies)
- ✅ High performance

**Cons**:
- ⚠️ Lambda-based API (different paradigm from current)
- ⚠️ Less suitable for complex CLI apps with many options
- ⚠️ Smaller community

### Ookii.CommandLine

**AOT Support**: ✅ With source generation
**API Style**: Attribute-based
**Testing**: Standard dotnet test
**DI Support**: ❌ None
**Help Generation**: ✅ Highly customizable

**Pros**:
- ✅ Attribute-based (similar to current)
- ✅ AOT-compatible with source generation
- ✅ Stable, mature library

**Cons**:
- ❌ No dependency injection support (critical for testability)
- ⚠️ More work to adapt existing tests

### Cocona

**AOT Support**: ⚠️ Unknown (no documentation found)
**Status**: Not recommended due to lack of AOT verification

### CliFx

**AOT Support**: ❌ No evidence of support
**Status**: Not recommended

---

## System.CommandLine Migration Details

### API Conversion Patterns

#### Current: Spectre.Console.Cli
```csharp
public class SpeedTestCommand : AsyncCommand<SpeedTestCommandSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        SpeedTestCommandSettings settings,
        CancellationToken ct)
    {
        // Implementation
    }
}

public class SpeedTestCommandSettings : CommandSettings
{
    [CommandOption("--loop")]
    [Description("Run continuous speed tests")]
    public bool Loop { get; init; }

    [CommandOption("--count")]
    [DefaultValue(1)]
    public int Count { get; init; }
}
```

#### New: System.CommandLine
```csharp
// In Program.cs
var loopOption = new Option<bool>(
    aliases: new[] { "--loop" },
    description: "Run continuous speed tests");

var countOption = new Option<int>(
    aliases: new[] { "--count" },
    description: "Number of tests to run",
    getDefaultValue: () => 1);

var rootCommand = new RootCommand("NetPace - Network speed tester");
rootCommand.AddOption(loopOption);
rootCommand.AddOption(countOption);

rootCommand.SetHandler(async (loop, count, services, ct) =>
{
    var speedTestService = services.GetRequiredService<ISpeedTestService>();
    // Implementation
}, loopOption, countOption, servicesBinder, cancellationTokenBinder);
```

### Testing Pattern Conversion

#### Current: CommandAppTester
```csharp
[Fact]
public async Task Version_Flag_Shows_Version()
{
    var app = GetCommandAppTester();
    var result = await app.RunAsync(new[] { "--version" });

    Assert.Equal(0, result.ExitCode);
    await Verify(result.Output);
}

private static CommandAppTester GetCommandAppTester()
{
    var app = new CommandAppTester(
        new CommandAppTesterSettings { TrimConsoleOutput = false });
    app.SetDefaultCommand<SpeedTestCommand>(Program.Description);
    app.Configure(Program.ConfigureAction);
    return app;
}
```

#### New: CommandLineTestHost (To Build)
```csharp
[Fact]
public async Task Version_Flag_Shows_Version()
{
    var host = GetCommandLineTestHost();
    var result = await host.RunAsync(new[] { "--version" });

    Assert.Equal(0, result.ExitCode);
    await Verify(result.Output);
}

private static CommandLineTestHost GetCommandLineTestHost()
{
    // Build wrapper around System.CommandLine's TestConsole
    return new CommandLineTestHost(
        rootCommand: CreateRootCommand(),
        serviceProvider: GetServiceProvider());
}

// CommandLineTestHost wrapper class to build
public class CommandLineTestHost
{
    private readonly RootCommand _rootCommand;
    private readonly IServiceProvider _serviceProvider;

    public async Task<TestResult> RunAsync(
        string[] args,
        CancellationToken ct = default)
    {
        var console = new TestConsole();
        var exitCode = await _rootCommand.InvokeAsync(args, console);
        return new TestResult
        {
            ExitCode = exitCode,
            Output = console.Out.ToString()
        };
    }
}

public record TestResult
{
    public int ExitCode { get; init; }
    public string Output { get; init; } = string.Empty;
}
```

---

## Implementation Plan

### Phase 1: Research & Prototyping (3-5 days)

**Goal**: De-risk the migration by validating approach early

#### Task 1.1: Study System.CommandLine (1 day)
- Review official Microsoft documentation
- Study example projects using System.CommandLine
- Understand DI integration patterns
- Research help customization options

**Deliverable**: Understanding of API patterns, confidence in approach

#### Task 1.2: Build CommandLineTestHost Wrapper (1 day)
- Create `CommandLineTestHost` class wrapping `TestConsole`
- Match `CommandAppTester` interface (`RunAsync()` → `TestResult`)
- Support DI injection (IServiceProvider)
- Test with simple "hello world" command

**Deliverable**: `CommandLineTestHost.cs` that preserves test patterns

#### Task 1.3: Create Prototype (1-2 days)
- Install System.CommandLine NuGet package
- Create prototype with SpeedTestCommand
- Implement 2-3 simple options (e.g., `--version`, `--count`, `--loop`)
- Test DI injection works (ISpeedTestService)

**Deliverable**: Working prototype proving concept is viable

#### Task 1.4: Validate Testing Approach (1 day)
- Write 2-3 tests using CommandLineTestHost
- Verify Verify snapshots work
- Ensure exit codes captured correctly
- Test cancellation token handling

**Deliverable**: Confidence that testing approach will work for all 83+ tests

**Phase 1 Success Criteria**:
- ✅ CommandLineTestHost wrapper works
- ✅ Prototype command executes successfully
- ✅ DI injection works
- ✅ Tests run and capture output
- ✅ Verify snapshots generate correctly

---

### Phase 2: Core Migration (5-7 days)

**Goal**: Replace all production code with System.CommandLine

#### Task 2.1: Migrate All 29 Options (2-3 days)

**TDD Approach** (per option):
1. **RED**: Write test for option → fails (option doesn't exist)
2. **GREEN**: Add option to command → test passes
3. **REFACTOR**: Clean up if needed → commit
4. **Repeat** for each option

**Options to Migrate**:
- SpeedTestCommand: 27 options
  - Boolean flags: `--loop`, `--no-latency`, `--no-download`, `--no-upload`, `--csv`, `--json`, `--json-pretty`, `--csv-header-units`, `--timestamp`, `--quiet`
  - Integers: `--count`, `--downloadsize`, `--uploadsize`
  - TimeSpan: `--delay`
  - Strings: `--server`, `--datetimeformat`, `--file`, `--csv-delimiter`
  - Enums: `--unit`, `--unit-scale`, `--unit-system`, `--verbosity`, `--file-mode`
  - Char: `--csv-delimiter`

- ListServersCommand: 2 options
  - Boolean flags: `--latency`, `--fastest`

**Considerations**:
- Handle short aliases (e.g., `-t|--timestamp`, `-u|--unit`)
- Set default values
- Add descriptions for help text

**Deliverable**: All 29 options working with tests

#### Task 2.2: Port Validation Logic (1-2 days)

**Current Validation** (in `SpeedTestCommandSettings.Validate()`):
```csharp
public override ValidationResult Validate()
{
    if (CsvHeaderUnits && UnitScale == SpeedScale.Auto && (Loop || Count > 1))
        return ValidationResult.Error("CSV header units incompatible with auto-scale...");

    if (NoLatency && NoDownload && NoUpload)
        return ValidationResult.Error("At least one test type must be enabled");

    return ValidationResult.Success();
}
```

**System.CommandLine Approach**:
- Add custom validators using `AddValidator()` on options
- OR implement validation in command handler before execution
- Return appropriate exit codes on validation failure

**TDD Approach**:
1. **RED**: Port validation test → fails
2. **GREEN**: Implement validation → test passes
3. **REFACTOR**: Extract to validation methods

**Deliverable**: All validation logic ported and tested

#### Task 2.3: Migrate ListServersCommand (1 day)

- Create `serversCommand` as subcommand or separate command
- Add `--latency` and `--fastest` options
- Wire up handler with DI injection
- Test command execution

**Deliverable**: ListServersCommand fully functional

#### Task 2.4: Implement Custom Help (1-2 days)

**Current**: CustomHelpProvider with ASCII art header and GitHub footer

**System.CommandLine Approach**:
- Use `HelpBuilder` for customization
- Override `GetLayout()` to add custom sections
- Add ASCII art (FigletText) to header
- Add "SEE ALSO" section with GitHub link

**Resources**:
- Spectre.Console (base library) is AOT-compatible - can still use FigletText
- Only Spectre.Console.Cli is not AOT-compatible

**Example Pattern**:
```csharp
var helpBuilder = new HelpBuilder(LocalizationResources.Instance);
helpBuilder.CustomizeLayout(context =>
{
    return HelpBuilder.Default.GetLayout()
        .Prepend(_ =>
        {
            // Add ASCII art header
            AnsiConsole.Write(new FigletText("NetPace").Centered());
        })
        .Append(_ =>
        {
            // Add footer
            AnsiConsole.MarkupLine("[grey]SEE ALSO:[/]");
            AnsiConsole.MarkupLine("  https://github.com/...");
        });
});
```

**Deliverable**: Custom help with matching UX

**Phase 2 Success Criteria**:
- ✅ All 29 options implemented and tested
- ✅ All validation logic ported
- ✅ Both commands (SpeedTest, ListServers) functional
- ✅ Custom help matches current UX
- ✅ All new code has passing tests (TDD)

---

### Phase 3: Testing Migration (4-6 days)

**Goal**: Update all 83+ tests to use new testing infrastructure

#### Task 3.1: Update Test Infrastructure (1 day)

- Create shared `CommandLineTestHost` setup in test base/helpers
- Update `GetCommandAppTester()` → `GetCommandLineTestHost()`
- Ensure DI mocking works (ISpeedTestService, IClock, etc.)
- Handle cancellation token testing

**Deliverable**: Test helper infrastructure ready

#### Task 3.2: Migrate Test Files (2-3 days)

**Per Test File**:
1. Update setup method to use `GetCommandLineTestHost()`
2. Verify tests still pass with same assertions
3. Fix any breaking changes
4. Commit

**Test Files** (8 files, 83+ tests):
- `NetPaceConsoleTests.cs` (27 tests) - Core functionality
- `NetPaceConsoleTests.CSV.cs` (21 tests) - CSV output
- `NetPaceConsoleTests.Json.cs` (10 tests) - JSON output
- `NetPaceConsoleTests.Servers.cs` (5 tests) - Server listing
- `NetPaceConsoleTests.Quiet.cs` (9 tests) - Quiet mode
- `NetPaceConsoleTests.File.cs` (11 tests) - File output
- `FileConsoleTests.cs` - File console tests
- Update `Properties/Usings.cs`

**Deliverable**: All tests updated and passing

#### Task 3.3: Regenerate Verify Snapshots (1-2 days)

**Why**: Help text format will differ between frameworks

**Process**:
1. Delete all `.verified.txt` files
2. Run all tests → generates new snapshots
3. **Manually review EVERY snapshot** for correctness
4. Verify help text quality (ASCII art, options, descriptions)
5. Accept snapshots

**Critical**: Don't blindly accept - validate output quality

**Deliverable**: All Verify snapshots regenerated and validated

**Phase 3 Success Criteria**:
- ✅ All 83+ tests passing
- ✅ 100% test coverage maintained
- ✅ All Verify snapshots regenerated and reviewed
- ✅ No functionality regressions

---

### Phase 4: Validation & Cleanup (2-3 days)

**Goal**: Ensure AOT works and everything is production-ready

#### Task 4.1: AOT Compilation Testing (1 day)

**Add to NetPace.Console.csproj**:
```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <InvariantGlobalization>false</InvariantGlobalization>
</PropertyGroup>
```

**Test Compilation**:
```bash
dotnet publish src/NetPace.Console -c Release -r win-x64 -o publish/aot-win-x64
dotnet publish src/NetPace.Console -c Release -r linux-x64 -o publish/aot-linux-x64
dotnet publish src/NetPace.Console -c Release -r linux-arm -o publish/aot-linux-arm
dotnet publish src/NetPace.Console -c Release -r linux-arm64 -o publish/aot-linux-arm64
```

**Verify**:
- ✅ No IL2026/IL3050 warnings (AOT blockers)
- ✅ Binaries created successfully
- ✅ Binary sizes: 5-10 MB target
- ✅ Functional testing on each platform

**Deliverable**: AOT compilation working on all platforms

#### Task 4.2: Functional Testing (1 day)

**Test Suite**:
```bash
# Version
./NetPace --version

# Help
./NetPace --help
./NetPace servers --help

# List servers
./NetPace servers
./NetPace servers --latency
./NetPace servers --fastest

# Speed test
./NetPace --no-upload --downloadsize 1
./NetPace --csv
./NetPace --json

# Edge cases
./NetPace --loop --count 2  # Should error (validation)
./NetPace --no-latency --no-download --no-upload  # Should error
```

**Deliverable**: All functionality verified working

#### Task 4.3: Documentation Updates (1 day)

**Files to Update**:
- `README.md` - Update help output examples (if embedded)
- `USER_GUIDE.md` - Verify all CLI examples still accurate
- `.claude/CLAUDE.md` - Update if needed (CLI framework changed)
- Update NuGet package references in docs

**Deliverable**: Documentation accurate and up-to-date

**Phase 4 Success Criteria**:
- ✅ AOT compilation succeeds (no warnings)
- ✅ Binaries tested on all platforms
- ✅ All functionality validated
- ✅ Documentation updated

---

## Key Risks & Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| **TestConsole != CommandAppTester** | High | Build wrapper early in Phase 1, validate before bulk migration |
| **Help text differs significantly** | Medium | Manual review of all Verify snapshots, validate UX |
| **Custom validation complex to port** | Medium | Port incrementally with TDD, test each rule |
| **DI integration works differently** | High | Study System.CommandLine DI docs, leverage Microsoft.Extensions.Hosting |
| **Learning curve slows progress** | Medium | Prototype first (Phase 1), validate approach before committing |
| **Verify snapshots need regeneration** | Low | Expected, allow time for manual review |
| **AOT still has issues** | High | Test early in Phase 4, iterate on fixes |

---

## TDD Approach (CLAUDE.md Compliance)

**Strict RED-GREEN-REFACTOR per option**:

1. **RED**:
   - Write test for option
   - Run test → verify it fails (option doesn't exist)
   - Commit failing test

2. **GREEN**:
   - Add option to System.CommandLine command
   - Implement minimum code to pass
   - Run test → verify it passes
   - Commit passing test

3. **REFACTOR** (optional):
   - Improve code structure
   - Extract helper methods
   - Run test → verify still passes
   - Commit refactoring

**For Validation**:
1. **RED**: Port validation test → fails
2. **GREEN**: Implement validation logic → passes
3. **REFACTOR**: Extract to validation methods

**No Exceptions**: Every line of production code written in response to failing test.

---

## Success Criteria

### Technical Metrics
✅ All 83+ tests pass with identical behavior
✅ 100% test coverage maintained
✅ All Verify snapshots regenerated and manually reviewed
✅ AOT compilation succeeds (no IL2026/IL3050 warnings)
✅ Binary sizes: 3-10 MB (AOT-compiled, trimmed)
✅ No functionality regressions

### UX Metrics
✅ Help output matches current quality (ASCII art, GitHub link)
✅ All 29 options work identically
✅ Error messages clear and actionable
✅ Validation logic preserved

### Code Quality
✅ Zero breaking changes to NetPace.Core
✅ All code follows CLAUDE.md standards
✅ TDD strictly followed (RED-GREEN-REFACTOR)
✅ XML documentation on all public APIs

### Documentation
✅ README.md updated
✅ USER_GUIDE.md updated
✅ CLAUDE.md updated if needed

---

## Rollback Plan

If migration fails or is blocked:

1. **Git reset**: All work on feature branch, easy to discard
2. **Revert commits**: Granular commits allow selective rollback
3. **Keep Spectre.Console.Cli**: For desktop users (no AOT)
4. **Alternative**: Create separate NetPace.IoT project (original plan)

---

## Next Steps (Implementation)

### Immediate Actions:
1. ✅ Create feature branch: `replace-spectre-console-cli-with-aot-trimmable-alternative`
2. ✅ Update todo list with Phase 1 tasks
3. Start Phase 1: Research & Prototyping

### Before Each Phase:
- Use **planner agent** for detailed sub-task planning
- Review CLAUDE.md for standards compliance
- Stop and ask for a review of all changes and approval before proceeding to next phase

### During Implementation:
- Use **tdd-workflow agent** for strict TDD enforcement
- Use **test-quality-reviewer agent** for test code review
- Run full test suite regularly
- Build AOT binaries periodically to catch issues early
- Stop after each task for review and approval

---

## Reference Links

### System.CommandLine Resources
- [Official Documentation](https://learn.microsoft.com/en-us/dotnet/standard/commandline/)
- [GitHub Repository](https://github.com/dotnet/command-line-api)
- [Get Started Tutorial](https://learn.microsoft.com/en-us/dotnet/standard/commandline/get-started-tutorial)
- [TestConsole API](https://learn.microsoft.com/en-us/dotnet/api/system.commandline.io.testconsole)

### AOT Resources
- [.NET 8 Native AOT Deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
- [Preparing Libraries for Trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming)
- [Fixing AOT Warnings](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/fixing-warnings)

### NetPace Specific
- [AOT Investigation Results](.claude/plans/aot-investigation.md)
- [CLAUDE.md Development Standards](.claude/CLAUDE.md)

---

## Questions for Later Consideration

1. **Should we target System.CommandLine 2.0 (beta) or wait for GA?**
   - Recommendation: Use 2.0 beta - GA coming with .NET 9, actively maintained

2. **Keep both Spectre.Console.Cli and System.CommandLine temporarily?**
   - Recommendation: No, clean migration avoids confusion

3. **Publish separate NuGet packages for desktop vs IoT?**
   - Recommendation: Single package, AOT works for both

4. **Update IoT MVP plan timeline?**
   - Recommendation: Yes, add 2-3 weeks upfront for CLI migration

---

## Appendix: Current vs. New Architecture

### Current Architecture
```
NetPace.Console (Spectre.Console.Cli - NOT AOT)
├── Commands/
│   ├── SpeedTestCommand : AsyncCommand<SpeedTestCommandSettings>
│   └── ListServersCommand : AsyncCommand<ListServersCommandSettings>
├── DependencyInjection/
│   ├── TypeRegistrar : ITypeRegistrar
│   └── TypeResolver : ITypeResolver
├── CustomHelpProvider : HelpProvider
└── Program.cs (CommandApp<SpeedTestCommand>)

NetPace.Core (Unchanged)
├── ISpeedTestService
├── OoklaSpeedtest
└── ... (all business logic)

Tests (CommandAppTester)
├── GetCommandAppTester() helper
├── result = await app.RunAsync(args)
└── Assert.Equal(0, result.ExitCode)
```

### New Architecture
```
NetPace.Console (System.CommandLine - AOT COMPATIBLE)
├── Program.cs
│   ├── RootCommand (default: speed test)
│   ├── ServersCommand (subcommand)
│   ├── 29 Option<T> definitions
│   └── SetHandler() with DI injection
├── (No separate Command classes)
├── (No Settings classes)
├── (No TypeRegistrar/TypeResolver)
├── CustomHelpBuilder
│   └── Extends HelpBuilder for custom layout
└── (Optional) Validation/
    └── ValidationHelpers.cs

NetPace.Core (Unchanged)
├── ISpeedTestService
├── OoklaSpeedtest
└── ... (all business logic)

Tests (CommandLineTestHost wrapper)
├── CommandLineTestHost.cs (wrapper around TestConsole)
├── GetCommandLineTestHost() helper
├── result = await host.RunAsync(args)
└── Assert.Equal(0, result.ExitCode)
```

---

**Status**: Ready to implement
**Next**: Begin Phase 1 - Research & Prototyping
**Estimated Completion**: 2-3 weeks from start
