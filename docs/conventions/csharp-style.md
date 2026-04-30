# C# Style Guide

**Scope**: Detailed C# coding conventions for NetPace
**Extends**: `CLAUDE.md` (read that first for essential patterns)
**Audience**: Reference for detailed style questions during code writing/review

---

## Naming

- **PascalCase**: classes, methods, public properties, events, enums, **constants** (not ALL_CAPS)
- **camelCase**: local variables, method parameters, private/internal fields
- **_camelCase**: private instance fields (underscore prefix)
- **s_camelCase**: private/internal static fields
- **t_camelCase**: thread-static fields
- **IPrefix**: interfaces (e.g. ISpeedTestService, IWorkerQueue)
- **No Hungarian notation**, no abbreviations

## Primary Constructor Parameters

- **class/struct types**: camelCase (consistent with method parameters)
- **record types**: PascalCase (they become public properties)

## var / Implicit Typing

- **Use var** when type is obvious from the right-hand side: new, explicit cast, or literal
- **Do NOT use var** when type comes from a method name alone (e.g. `var x = GetValue()`)
- **Do NOT use var** in foreach loops — state the element type explicitly
- **DO use var** in LINQ queries (anonymous/nested generic types)

## Namespaces & Files

- **File-scoped namespaces**: `namespace NetPace.Core.Clients.Ookla;`
- **Place using directives OUTSIDE** the namespace declaration
- **One class per file**; filename matches class name

## Strings

- Use **string interpolation** for short strings: `$"{first}, {last}"`
- Use **StringBuilder** for string concatenation in loops
- Prefer **raw string literals** over escape sequences (C# 11+)

## Collections

- Use **collection expressions** (C# 12): `string[] x = ["a", "b"];`
- Prefer `IEnumerable<T>`, `IReadOnlyList<T>` for method parameters
- Use concrete types (`List<T>`, `Dictionary<T>`) for internal implementation

## Immutability & Constants

- **Favor immutability**: prefer `readonly` fields, `init`-only properties, and `record` types for DTOs
- **No magic strings/numbers**: extract to a `const`, `static readonly`, or `enum` — even one-off values gain meaning from a name

## Async

- **Async methods must have the Async suffix** (e.g. `GetServersAsync`)
- **Never use .Result or .Wait()** — always await
- **Include CancellationToken** on all public async methods
- **Return Task, not void** (except event handlers)
- **Use ConfigureAwait(false)** in library code (NetPace.Core)
  - Not needed in application code (NetPace.Console)

## Error Handling

- **Only catch exceptions you can meaningfully handle**
- **Catch specific exception types**, not System.Exception
- **Use `using` declarations** (not blocks): `using var conn = ...;`
- **Throw ArgumentNullException.ThrowIfNull()** for null argument guards (C# 11+)
- **Validate inputs early** (guard clauses at method start)

## Code Structure

- **Allman braces**: opening brace on its own line
  ```csharp
  if (condition)
  {
      // code
  }
  ```
- **Always use braces** for if/else — never omit even for single-line bodies
- **One statement per line**, one declaration per line
- **4-space indentation** (no tabs)
- **using directives outside** namespace declarations

## Member Ordering

Within a class, order members as follows:

1. Static fields
2. Instance fields
3. Constructors
4. Properties
5. Methods
6. Nested types

## Dependency Injection

- **Inject via constructor**, never new() internally
- **Depend on interfaces**, not concrete types (e.g. `ISpeedTestService`, not `OoklaSpeedtest`)
- Store injected dependencies in private readonly fields

## LINQ

- Use **meaningful query variable names**
- Use **where clauses early** to filter before other operations
- Prefer **method syntax** for simple chains
- Prefer **query syntax** for complex multi-clause queries

## Testing (xUnit)

- **Name tests**: `MethodName_Scenario_ExpectedResult`
- **Follow**: Arrange / Act / Assert (Given / When / Then)
- **Cover**: Happy paths, edge cases, error scenarios
- **Mock**: External dependencies (network, filesystem, time)

## Sources

- [.NET C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [C# Identifier Names](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names)
- [.NET Runtime Coding Style](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md)

---

**Last Updated**: April 2026
**Target Framework**: .NET 8.0 (C# 12)
