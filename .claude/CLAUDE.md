# NetPace Development Guide

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
- **Given-When-Then** pattern in tests
- Test file mirrors source: `OoklaSpeedtest.cs` → `OoklaSpeedtestTests.cs`

### What to Test
- **NetPace.Core**: Unit tests for all public APIs
- **NetPace.Console**: Speed calculations, unit conversions
- Happy paths, alternative scenarios, edge cases, error scenarios
- Application performance

### What NOT to Test
- **Spectre.Console output** - trust the library works
- **Simple property getters/setters**

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

### Performance
- **Async operations** for all network calls
- Consider **HttpClient best practices** (singleton, pooling)
- **CancellationToken** support for long operations
- Measure and optimize **hot paths** (speed test loops)

## Development Workflow

### Before Starting Work
1. Pull latest from `main` branch
2. Create a feature branch: `feature/your-feature-name`
3. Review this CLAUDE.md for project standards

### During Development
1. Write code following the standards above
2. Add XML documentation to public APIs
3. Write/update tests as you go

### Before Committing
1. Build succeeds with no warnings
2. All tests pass
3. Code follows naming conventions
4. Public APIs have XML documentation

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

### Tell Claude About
- Which component you're working on (Core vs Console)
- Whether changes affect the public API (NuGet consumers)
- Platform-specific considerations
- Performance requirements

### Ask Claude To
- Follow these coding standards
- Add XML documentation to public members
- Suggest appropriate test cases
- Consider cross-platform compatibility

### Don't Let Claude
- Change public APIs without discussion
- Add dependencies to Core without good reason
- Skip error handling
- Create code without considering testability

## Resources

- [.NET API Documentation](https://learn.microsoft.com/en-us/dotnet/api/)
- [C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [CLI Guidelines](https://clig.dev/)
- [Spectre.Console Documentation](https://spectreconsole.net/)

---

**Last Updated**: 2025 by Claude Code  
**Maintained by**: Frank Ray  
**Project**: https://github.com/FrankRay78/NetPace