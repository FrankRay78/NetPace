# NetPace Development Guide

## Summary

**TDD (Test-Driven Development) is non-negotiable.** Every line of production code must be written in response to a failing test. No exceptions.

This document guides Claude Code in maintaining NetPace, a cross-platform .NET 8.0 CLI application for network speed testing. Follow these standards strictly:

- **RED-GREEN-REFACTOR**: Write failing test → Make it pass → Improve code
- **C# best practices**: XML docs, async/await, nullable reference types
- **CLI excellence**: Follow clig.dev guidelines, Spectre.Console for UI
- **Clean architecture**: Core library separate from Console application
- **Cross-platform**: Windows, Linux, macOS support

## Core Philosophy

### Test-Driven Development is Mandatory

**TDD is not optional.** Every single change to production code must follow the RED-GREEN-REFACTOR cycle:

1. **RED**: Write a failing test first
   - No production code without a failing test
   - Test describes the behavior you want
   - Run test and watch it fail (confirms test is valid)

2. **GREEN**: Write minimum code to pass
   - Write only enough code to make the test pass
   - Don't add features "while you're there"
   - Get to green as quickly as possible

3. **REFACTOR**: Improve the code
   - Only after test passes
   - Improve design, remove duplication, enhance readability
   - Tests must still pass after refactoring

**Critical Rules:**
- **Never write production code without a failing test first**
- **Never skip the RED step** - you must see the test fail
- **Never refactor on red** - always get to green first
- **Commit before refactoring** - so you can safely rollback if needed
- **Run all tests frequently** - catch regressions immediately

### Why This Matters

TDD provides:
- **Design feedback** - Hard to test = bad design
- **Regression protection** - Changes don't break existing behavior
- **Living documentation** - Tests show how code should be used
- **Confidence** - Refactor safely knowing tests will catch issues

## Project Overview

NetPace is a cross-platform network speed testing CLI application built with .NET 8.0, utilizing Ookla's Speedtest servers. It includes both a command-line application and a reusable Core library published to NuGet.

**Key Components:**
- `NetPace.Console` - Command-line application using Spectre.Console
- `NetPace.Core` - Reusable library with `ISpeedTestService` interface
- `NetPace.Core` published as NuGet package

## C# and .NET Standards

### Language and Framework
- Target Framework: **.NET 8.0**
- Language Version: **C# 12** (latest for .NET 8)
- Nullable Reference Types: **Enabled** (helps prevent null reference exceptions)

### Naming Conventions
- **PascalCase** for: Classes, methods, properties, namespaces, public fields
- **camelCase** for: Private fields, local variables, parameters
- **Interfaces** start with `I`: `ISpeedTestService`
- **Async methods** end with `Async`: `GetServersAsync()`

### Code Organization
- **One class per file** (with exceptions for small, tightly related types)
- **File names match type names**: `OoklaSpeedtest.cs` contains `OoklaSpeedtest` class
- **Namespace matches folder structure**: `NetPace.Core.Clients.Ookla`

### Best Practices
- **Use interfaces** for abstraction and testability (like `ISpeedTestService`)
- **Favor immutability**: Use `readonly` fields, consider `record` types for DTOs
- **Avoid magic strings/numbers**: Use constants or enums
- **Use `var`** when type is obvious: `var result = GetResult();`
- **Explicit types** when clarity helps: `ISpeedTestService speedTester = ...`
- **XML documentation** on all public APIs (methods, properties, classes)
- **Async all the way**: Network operations should be async with proper cancellation token support

### Error Handling
- **Don't swallow exceptions** - let them bubble unless you can meaningfully handle them
- **Use specific exception types** when creating custom exceptions
- **Validate inputs** early (guard clauses at method start)

## CLI Application Specific Guidelines

### Command-Line Interface
- Follow **[CLI Guidelines](https://clig.dev/)** (as per project philosophy)
- Use **Spectre.Console** for all console output and interaction
- Support **`--help`** and **`--version`** flags
- Provide **clear error messages** with actionable guidance
- Support **multiple output formats** (normal, CSV, JSON) for scripting

### User Experience
- **Default behavior should work for most users**: `NetPace` runs a simple test
- **Progress indication** for long-running operations
- **Verbosity levels**: Minimal (scripts), Normal (users), Debug (troubleshooting)
- **Cross-platform** considerations: file paths, line endings, console encoding

### Configuration
- Use **command-line options** over config files (CLI app principle)
- **Sensible defaults** - users shouldn't need to specify everything
- **Validate user input** and provide helpful error messages

## Testing

### Test Organization
- Test project naming: `NetPace.Core.Tests`, `NetPace.Console.Tests`
- Use **xUnit** for testing framework
- **Given-When-Then** or **Arrange-Act-Assert** pattern in tests
- Test file mirrors source: `OoklaSpeedtest.cs` → `OoklaSpeedtestTests.cs`

### TDD Workflow in Practice

For every feature or bug fix:
```csharp
// 1. RED - Write the failing test first
[Fact]
public async Task GetDownloadSpeed_WhenServerResponds_ReturnsValidSpeed()
{
    // Given: A speed test service with a valid server
    var service = new OoklaSpeedtest();
    var server = new Server { Url = "http://test.example.com" };
    
    // When: We get the download speed
    var result = await service.GetDownloadSpeedAsync(server);
    
    // Then: Result should be valid
    Assert.NotNull(result);
    Assert.True(result.SpeedBitsPerSecond > 0);
}

// Run test - it MUST fail (because GetDownloadSpeedAsync doesn't exist yet)

// 2. GREEN - Write minimum implementation
public async Task<DownloadResult> GetDownloadSpeedAsync(Server server)
{
    // Simplest thing that makes test pass
    return new DownloadResult { SpeedBitsPerSecond = 1000000 };
}

// Run test - it should pass now

// 3. REFACTOR - Now improve the implementation
public async Task<DownloadResult> GetDownloadSpeedAsync(Server server)
{
    // Now add proper implementation
    var client = new HttpClient();
    var stopwatch = Stopwatch.StartNew();
    var bytes = await client.GetByteArrayAsync(server.Url);
    stopwatch.Stop();
    
    var bitsPerSecond = (bytes.Length * 8) / stopwatch.Elapsed.TotalSeconds;
    return new DownloadResult { SpeedBitsPerSecond = bitsPerSecond };
}

// Run test - still passes after refactoring
```

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

### Test Quality Standards
- Tests should be **readable** - another developer should understand what's being tested
- Tests should be **independent** - can run in any order
- Tests should be **fast** - entire test suite runs in seconds
- Tests should be **deterministic** - same input = same result, every time
- Mock external dependencies (network, filesystem, time) for unit tests

## Architecture Principles

### Separation of Concerns
- **NetPace.Core**: Business logic, no UI, no console output
- **NetPace.Console**: User interaction, parsing args, formatting output
- Core library should be **usable in any context** (console, web API, GUI)

### Dependency Injection Ready
- Design with DI in mind even if not using a container
- Depend on **interfaces, not concrete implementations**
- Constructor injection for dependencies

### NuGet Package Considerations
- **Keep Core library dependencies minimal** (fewer version conflicts for consumers)
- **Document breaking changes** in release notes
- **Semantic versioning**: MAJOR.MINOR.PATCH

## Project-Specific Guidelines

### Speed Test Provider Pattern
- All speed test implementations should implement `ISpeedTestService`
- Currently using Ookla, but architecture allows for alternatives
- Keep provider-specific code isolated in `Clients/{ProviderName}/`

### Units and Formatting
- Support both **SI (1000-based)** and **IEC (1024-based)** unit systems
- Support both **BitsPerSecond** and **BytesPerSecond**
- Auto-scale by default (Mbps, Gbps) but allow user override
- Consistent formatting across output modes (normal, CSV, JSON)

### Performance
- **Async operations** for all network calls
- Consider **HttpClient best practices** (singleton, pooling)
- **CancellationToken** support for long operations
- Measure and optimize **hot paths** (speed test loops)

## Development Workflow

### Starting New Work

1. **Pull latest from main**
```bash
   git checkout main
   git pull origin main
```

2. **Create feature branch**
```bash
   git checkout -b feature/your-feature-name
```

3. **Review CLAUDE.md** for project standards

### The TDD Cycle (For Every Change)
```
┌─────────────────────────────────────────────┐
│  1. RED - Write failing test                │
│     - Describes desired behavior            │
│     - Run and watch it FAIL                 │
└──────────────┬──────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────┐
│  2. GREEN - Make test pass                  │
│     - Write minimum code needed             │
│     - Run and watch it PASS                 │
└──────────────┬──────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────┐
│  3. REFACTOR - Improve code (optional)      │
│     - Commit before refactoring             │
│     - Improve design/remove duplication     │
│     - Run tests - still PASS                │
└──────────────┬──────────────────────────────┘
               │
               ▼
         Back to RED for next behavior
```

### During Development
1. **Follow TDD cycle** for every behavior change
2. Add **XML documentation** to public APIs as you go
3. **Commit frequently** - especially before refactoring
4. Run **full test suite** regularly

### Before Committing
- Build succeeds with **no warnings**
- **All tests pass**
- Code follows **naming conventions**
- Public APIs have **XML documentation**
- No **commented-out code** (delete it, git remembers)

### Commit Messages
- **Clear and concise**: "Add support for custom server URLs"
- **Imperative mood**: "Add feature" not "Added feature"
- **Reference issues** if applicable: "Fix #123: Handle null server response"

## Common Patterns in This Project

### Result Objects
Prefer returning result objects with rich information:
```csharp
public class DownloadResult
{
    public double SpeedBitsPerSecond { get; init; }
    public TimeSpan Duration { get; init; }
    public long BytesTransferred { get; init; }
    
    public string GetSpeedString(SpeedUnit unit, SpeedUnitSystem system) { ... }
}
```

### Extension Methods
Use extension methods for formatting and conversion logic that doesn't belong in core types.

### Options Pattern
For complex configuration, use options objects instead of many parameters:
```csharp
public async Task<DownloadResult> GetDownloadSpeedAsync(
    Server server, 
    DownloadTestSettings? settings = null)
```

## When Working with Claude Code

### Claude Must Always
- **Follow TDD** - write failing test before any production code
- Follow the **RED-GREEN-REFACTOR** cycle
- Add **XML documentation** to public members
- Consider **cross-platform compatibility**
- Write **testable code** (interfaces, dependency injection)
- Ask for clarification if requirements are ambiguous

### Tell Claude About
- Which component you're working on (Core vs Console)
- Whether changes affect the public API (NuGet consumers)
- Platform-specific considerations
- Performance requirements

### Never Let Claude
- Write production code without a failing test first
- Skip the RED step (must see test fail)
- Change public APIs without discussion
- Add dependencies to Core without good reason
- Skip error handling
- Commit code with failing tests

### Example Interaction

**Good:**
```
You: Add validation to ensure server URL is not null
Claude: I'll follow TDD. First, I'll write a test that expects an 
        ArgumentNullException when server URL is null...
        [writes failing test]
        [makes it pass]
        [suggests refactoring if appropriate]
```

**Bad:**
```
You: Add validation to ensure server URL is not null
Claude: Here's the updated code with null checking...
        [provides implementation without test]
STOP - This violates TDD!
```

## Resources

- [.NET API Documentation](https://learn.microsoft.com/en-us/dotnet/api/)
- [C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [CLI Guidelines](https://clig.dev/)
- [Spectre.Console Documentation](https://spectreconsole.net/)
- [xUnit Documentation](https://xunit.net/)
- [Test-Driven Development by Example (Kent Beck)](https://www.amazon.com/Test-Driven-Development-Kent-Beck/dp/0321146530)

---

**Last Updated**: November 2025  
**Maintained by**: Frank Ray  
**Project**: https://github.com/FrankRay78/NetPace  
**Philosophy**: Test-Driven Development is non-negotiable