# C# Style Guide

> This document elaborates Section V of the constitution and must remain consistent with it.

**Scope**: Detailed C# coding conventions for NetPace
**Extends**: `CLAUDE.md` (read that first for essential patterns)
**Audience**: Reference for detailed style questions during code writing/review

---

## Naming

- **PascalCase**: classes, methods, public properties, events, enums, non-private static and readonly fields, **constants** (not ALL_CAPS)
- **camelCase**: local variables, method parameters
- **_camelCase**: private instance fields
- **IPrefix**: interfaces (e.g. ISpeedTestService, IWorkerQueue)
- **No Hungarian notation**, no abbreviations

The field rules are the ones `.editorconfig` states, so the IDE flags a field that breaks them.

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
- **One class per file**; filename matches class name. One exception, a test seam: an interface, its production implementation and its stubs may share the interface's file (e.g. `IClock.cs` holds `IClock`, `Clock`, `ClockStub` and `IncrementingClockStub`), because the interface exists only so a stub can stand in for the real class, and together they read as one unit.

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

- Brace placement (Allman), 4-space indentation and file-scoped namespaces are enforced by `.editorconfig` + `dotnet format --verify-no-changes` in CI — don't hand-police them.
- **Always use braces** for if/else — never omit even for single-line bodies
- **One statement per line**, one declaration per line
- **No column alignment.** Separate tokens with a single space. Don't pad consecutive `if`/`return`/`=>`/`=` lines to line up columns: renaming the longest identifier then reformats every row, and the alignment breaks outside the editor that made it. The only exception is end-of-line comments on tabular test data such as `[InlineData]` rows.

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

## Testing

See `docs/conventions/testing.md`; test naming and structure are set by the constitution's Testing Standards.

---

**Last Updated**: April 2026
**Target Framework**: .NET 10.0 (C# 14)
