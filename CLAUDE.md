# NetPace Development Guide

## Quick Reference

**For Core Principles & Governance**: See `.specify/memory/constitution.md`

This guide provides implementation-specific details for developing NetPace. The constitution defines **WHAT** and **WHY** (principles, governance), while this guide covers **HOW** (C# specifics, code patterns, NetPace domain knowledge).

## Project Overview

NetPace is a cross-platform network speed testing CLI application built with .NET 8.0, utilizing Ookla's Speedtest servers.

**Key Components:**
- `NetPace.Console` - Command-line application using Spectre.Console
- `NetPace.Core` - Reusable library with `ISpeedTestService` interface (published to NuGet)

**Technology Stack:**
- Framework: .NET 8.0
- Language: C# 12
- CLI Library: Spectre.Console
- Testing: xUnit
- Nullable Reference Types: Enabled

## C# Implementation Details

### Naming Conventions

- **PascalCase**: Classes, methods, properties, namespaces, public fields
- **camelCase**: Private fields, local variables, parameters
- **Interfaces** start with `I`: `ISpeedTestService`
- **Async methods** end with `Async`: `GetServersAsync()`

### Code Organization

- **One class per file** (exceptions for small, tightly related types)
- **File names match type names**: `OoklaSpeedtest.cs` contains `OoklaSpeedtest` class
- **Namespace matches folder structure**: `NetPace.Core.Clients.Ookla`

### C# Best Practices

- **Use `var`** when type is obvious: `var result = GetResult();`
- **Explicit types** when clarity helps: `ISpeedTestService speedTester = ...`
- **Favor immutability**: Use `readonly` fields, consider `record` types for DTOs
- **Avoid magic strings/numbers**: Use constants or enums
- **Guard clauses**: Validate inputs early at method start

## Testing Implementation

### Test Organization

- **Test project naming**: `NetPace.Core.Tests`, `NetPace.Console.Tests`
- **Framework**: xUnit
- **Test file mirrors source**: `OoklaSpeedtest.cs` → `OoklaSpeedtestTests.cs`
- **Test naming**: `MethodName_Scenario_ExpectedResult`
- **Pattern**: Given-When-Then

### What to Test

- **NetPace.Core**: Unit tests for all public APIs
- **Business logic**: Speed calculations, unit conversions, server selection
- **Happy paths**: Normal successful scenarios
- **Alternative scenarios**: Different configurations, edge cases
- **Error scenarios**: Invalid input, network failures, timeouts
- **Integration tests**: Real network calls (consider separate test category)

### What NOT to Test

- **Spectre.Console output** - trust the library works
- **Simple property getters/setters** with no logic
- **Third-party libraries** - assume they work

### Test Quality Expectations

- **Readable**: Another developer should understand what's being tested
- **Independent**: Can run in any order
- **Fast**: Entire test suite runs in seconds
- **Deterministic**: Same input = same result, every time
- **Mock externals**: Mock network, filesystem, time for unit tests

## NetPace-Specific Patterns

### Speed Test Provider Pattern

All speed test implementations must implement `ISpeedTestService`:

```csharp
public interface ISpeedTestService
{
    Task<IEnumerable<Server>> GetServersAsync(...);
    Task<LatencyResult> GetLatencyAsync(Server server, ...);
    Task<DownloadResult> GetDownloadSpeedAsync(Server server, ...);
    Task<UploadResult> GetUploadSpeedAsync(Server server, ...);
}
```

- Currently using Ookla, but architecture allows for alternatives
- Keep provider-specific code isolated in `Clients/{ProviderName}/`

### Units and Formatting

NetPace supports flexible unit configurations:

- **Unit systems**: SI (1000-based: KB, MB, GB) and IEC (1024-based: KiB, MiB, GiB)
- **Speed units**: BitsPerSecond and BytesPerSecond
- **Scaling**: Auto-scale by default (Mbps, Gbps) but allow user override via `--unit-scale`
- **Consistency**: Same formatting across all output modes (normal, CSV, JSON)

### Common Code Patterns

#### Result Objects

Return rich result objects with full test information:

```csharp
public class DownloadResult
{
    public double SpeedBitsPerSecond { get; init; }
    public TimeSpan Duration { get; init; }
    public long BytesTransferred { get; init; }

    public string GetSpeedString(SpeedUnit unit, SpeedUnitSystem system) { ... }
}
```

#### Extension Methods

Use extension methods for formatting and conversion logic that doesn't belong in core types:

```csharp
public static class SpeedResultExtensions
{
    public static string GetSpeedString(this DownloadResult result, ...) { ... }
}
```

#### Options Pattern

For complex configuration, use options objects instead of many parameters:

```csharp
public async Task<DownloadResult> GetDownloadSpeedAsync(
    Server server,
    DownloadTestSettings? settings = null,
    CancellationToken cancellationToken = default)
{
    settings ??= DownloadTestSettings.Default;
    // ...
}
```

## Documentation Maintenance

When making changes, update relevant documentation:

- **README.md** - Contains static `--help` output (update if CLI options change)
- **USER_GUIDE.md** - Check if sections reference changed options or features
- **XML documentation** - All public APIs must have XML docs

## Working with Claude Code

### Claude Must Always

- **Follow TDD strictly** - Write failing test first (RED-GREEN-REFACTOR cycle from constitution)
- **Add XML documentation** to all public APIs (methods, properties, classes)
- **Consider cross-platform compatibility** (Windows, Linux, macOS)
- **Write testable code** (interfaces, dependency injection)
- **Ask for clarification** if requirements are ambiguous
- **Use built-in planning tools** for non-trivial changes before writing code

### Tell Claude About

- **Which component** you're working on (Core vs Console)
- **Public API changes** - affects NuGet consumers, requires discussion
- **Platform-specific considerations** - if code behavior varies by OS
- **Performance requirements** - if optimization is needed

### Never Let Claude

- Write production code without a failing test first
- Skip the RED step (must see test fail)
- Change public APIs without discussion and approval
- Add dependencies to NetPace.Core without justification
- Commit code with failing tests or build warnings

## Quick Command Reference

### Build and Test

```bash
# Build solution
dotnet build

# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Git Workflow

```bash
# Start new work
git checkout main
git pull origin main
git checkout -b feature/your-feature-name

# Before committing: verify build and tests pass
dotnet build
dotnet test
```

## Detailed References

For deeper guidance on specific topics, see:

**C# Style Details** (`docs/conventions/csharp-style.md`)
- Underscore field naming conventions (`_camelCase`, `s_camelCase`, `t_camelCase`)
- File-scoped namespaces
- ConfigureAwait patterns for library code
- Collection expressions (C# 12)
- Allman braces and member ordering
- Primary constructor parameters

**Change Intent Records** (`docs/conventions/change-intent-records.md`)
- When to create CIRs
- CIR template and examples
- Documenting architectural decisions

**AI Agents**: Read these files when working on C# code or making architectural decisions.

## Resources

- [.NET API Documentation](https://learn.microsoft.com/en-us/dotnet/api/)
- [C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [CLI Guidelines](https://clig.dev/)
- [Spectre.Console Documentation](https://spectreconsole.net/)
- [xUnit Documentation](https://xunit.net/)
- [Test-Driven Development by Example (Kent Beck)](https://www.amazon.com/Test-Driven-Development-Kent-Beck/dp/0321146530)

---

**Last Updated**: April 2026 (Streamlined - core principles moved to constitution)
**Maintained by**: Frank Ray
**Project**: https://github.com/FrankRay78/NetPace
**Constitution**: `.specify/memory/constitution.md`
