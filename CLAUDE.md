# NetPace Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-04-14

## Active Technologies

- C# 12 / .NET 8.0 + Spectre.Console (CLI), xUnit + Verify (testing) — no new dependencies (main)

## Project Structure

```text
src/NetPace.Console/
src/NetPace.Core/
tests/NetPace.Console.Tests/
tests/NetPace.Core.Tests/
specs/
```

## Commands

```bash
dotnet build
dotnet test
```

## Code Style

C# 12 / .NET 8.0: Follow conventions in .claude/CLAUDE.md and docs/conventions/csharp-style.md

## Recent Changes

- main: Added IClientInfoProvider abstraction; IPAddress + Hostname fields to JSON/CSV output (specs/001-hostname-ip-output)

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
