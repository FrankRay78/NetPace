<!--
Sync Impact Report:
Version: 1.4.0 → 1.5.0
Bump rationale: MINOR — updates the mandated target framework from .NET 8.0 to
.NET 10.0 (the clean LTS→LTS hop; .NET 8 reaches end of support ~November 2026).
This updates a technology constraint rather than removing or inverting a principle,
so it is materially updated guidance (MINOR), not a governance redefinition (MAJOR).
The mandated language baseline is bumped in lockstep to C# 14 (unlocked by .NET 10).

Modified Principles: IV — Cross-Platform Compatibility (target .NET 10.0)
Modified Sections: Technology Constraints → Required Technologies (Framework .NET 10.0, Language C# 14)
Added Sections: N/A
Removed Sections: N/A
Downstream documents reviewed (per Amendment Process clause 4):
  ✅ CLAUDE.md — Project Overview / Stack line updated to .NET 10.0 · C# 14.
  ✅ README.md — build-with line, release-variant name/link, developed-with line updated.
  ✅ docs/conventions/csharp-style.md — Target Framework line updated to .NET 10.0 (C# 14).
  ✅ docs/RELEASING.md — framework-dependent artifact name (-net8 → -net10), .NET marker wording, global.json note.
  ✅ 6 project files + 4 CI workflows — TargetFramework net10.0, SDK pins 10.0.x.
  ✅ global.json — new SDK pin (10.0.x, rollForward latestFeature).
Follow-up TODOs: None
-->

# NetPace Constitution

## Core Principles

### I. Test-Driven Development (NON-NEGOTIABLE)

Every line of production code MUST be written in response to a failing test following the RED-GREEN-REFACTOR cycle:

1. **RED** - Write failing test describing desired behavior, run and watch it FAIL
2. **GREEN** - Write minimum code needed to pass, run and watch it PASS
3. **REFACTOR** - Commit before refactoring, improve design, run tests - still PASS

**Critical Rules:**
- MUST NEVER write production code without a failing test first
- MUST NEVER skip the RED step (must see test fail)
- MUST NEVER refactor on red (always get to green first)
- MUST NEVER add features not covered by tests
- MUST NEVER proceed if tests are failing

**Rationale**: TDD ensures every feature is testable, reduces bugs, improves design, and provides living documentation through tests. This is foundational to NetPace quality standards.

### II. Library-First Architecture

Every feature MUST start as a standalone library (`NetPace.Core`) before CLI implementation:

- Libraries MUST be self-contained, independently testable, and documented
- Core library MUST have no dependencies on Console application
- Core library MUST be usable in any context (console, web API, GUI, tests)
- Clear purpose required - no organizational-only libraries
- Interfaces over concrete implementations for abstraction and testability

**Rationale**: Library-first design ensures code reusability, testability, and enables NuGet package distribution. Consumers can use NetPace.Core without any CLI dependencies.

### III. CLI Excellence

The command-line interface MUST follow industry best practices:

- Follow [CLI Guidelines (clig.dev)](https://clig.dev/) strictly
- Use Spectre.Console for all console output and interaction
- Support `--help` and `--version` flags
- Provide clear error messages with actionable guidance
- Support multiple output formats (normal, CSV, JSON) for scripting
- Default behavior should work for most users without flags
- Verbosity levels: Minimal (scripts), Normal (users), Debug (troubleshooting)

**Rationale**: CLI applications are tools for users. Following established guidelines ensures NetPace is intuitive, scriptable, and professional.

### IV. Cross-Platform Compatibility

All code MUST run on Windows, Linux, and macOS without platform-specific workarounds:

- Target .NET 10.0 for cross-platform support
- Consider file paths, line endings, console encoding
- Test on multiple platforms before release
- Avoid platform-specific APIs unless absolutely necessary
- Document any platform-specific behavior clearly

**Rationale**: NetPace serves a diverse user base across operating systems. Cross-platform support maximizes accessibility and adoption.

### V. Code Quality Standards

All production code MUST meet these quality standards:

- **Naming**: PascalCase for classes/methods/properties, camelCase for private fields/variables
- **Documentation**: XML documentation on all public APIs
- **Async/Await**: Network operations MUST be async with CancellationToken support
- **Nullable Reference Types**: Enabled to prevent null reference exceptions
- **Error Handling**: Validate inputs early, don't swallow exceptions, use specific exception types
- **No Warnings**: Build MUST succeed with zero warnings

**Rationale**: Consistent quality standards ensure maintainability, reduce bugs, and provide a professional developer experience for NuGet package consumers.

### VI. Minimal Dependencies

NetPace.Core MUST keep dependencies minimal:

- Every dependency MUST be justified (fewer version conflicts for consumers)
- Prefer .NET BCL over third-party libraries when possible
- Document all dependencies and their purpose
- Review dependency security regularly

**Rationale**: As a NuGet package, NetPace.Core's dependencies become consumers' dependencies. Minimal dependencies reduce version conflicts and security surface area.

### VII. Semantic Versioning

All releases MUST follow semantic versioning (MAJOR.MINOR.PATCH):

- **MAJOR**: Breaking changes to public API
- **MINOR**: New features, backward compatible
- **PATCH**: Bug fixes, backward compatible
- Document breaking changes in release notes
- Discuss public API changes before implementation

**Rationale**: NuGet consumers depend on predictable versioning to avoid breaking changes. Semantic versioning is industry standard for package distribution.

### VIII. AC-to-Test Traceability

All acceptance scenarios in `spec.md` MUST carry a `**Scenario:**` label:

```
**Scenario: [Descriptive name]**
Given [state], When [action], Then [outcome]
```

Label names MUST match the `#### Scenario:` headers in `test-plan.md` exactly —
they are the traceability key linking acceptance criteria → test scenarios → test code.

**Rationale**: Consistent labels enable `/speckit.testchecklist` to verify end-to-end coverage automatically. Violations are flagged CRITICAL by `/speckit.analyze`.

### IX. Behavioural Specification (NON-NEGOTIABLE)

Acceptance criteria and tests MUST describe outcomes an outside observer can verify, not the mechanism that delivers them. Multiple reasonable implementations of the same feature MUST satisfy the same ACs and pass the same tests.

**The independence test**: would this AC (or test) still hold under a different reasonable implementation of the same feature? If no, it is describing the mechanism, not the outcome.

**Critical Rules:**

- ACs MUST be phrased as user-observable outcomes. Mechanism details MUST NOT appear in ACs, including: CSS classes, DOM IDs or element types, animation specifics, font names/weights/colours, and pixel measurements; HTTP methods, endpoint paths, and status codes; response/payload schemas (JSON keys, field names); database tables, collections, columns, or indexes; algorithm or protocol choices (hash functions, signature schemes, encryption modes); framework or library picks; storage technology; specific URLs or ports; timing values (Ns / Nms) and polling cadences; exact error message strings; and log line formats or log levels.
- Tests MUST verify the AC as written, not the chosen implementation. A test that would fail under a different reasonable implementation of the same AC is testing mechanism, not outcome.
- Project housekeeping (project exists, sln updated, scaffolding created) belongs in `tasks.md`, not in ACs.
- **Regression exception**: an AC or test that pins a specific mechanism is permitted only when it exists to prevent a named, previously-fixed bug. Reference the bug in the AC text, scenario name, or a one-line comment in the test so future readers understand why the coupling exists.

**Rationale**: Mechanism-coupled ACs invite brittle, implementation-mirroring tests that lock the codebase to its current shape and make refactors expensive. Outcome-level ACs preserve the implementer's freedom to choose the simplest mechanism, keep the test suite meaningful through refactors, and give `/speckit.analyze` an enforceable rule rather than style guidance.

**Downstream references**: detailed avoid/prefer guidance lives in `.claude/commands/speckit.draftissue.md` (AC drafting) and `.claude/commands/speckit.testplan.md` (test scenario authoring). Update those in lockstep with any change to this principle.

### X. No Skipped Tests (NON-NEGOTIABLE)

No test in the suite may be skipped. A skipped test reports green while verifying nothing — silent non-coverage that hides regressions behind a passing run. The entire skip family is prohibited: `[Fact(Skip=…)]` / `[Theory(Skip=…)]`, `Assert.Skip`, `Skip.If` / `Skip.IfNot` / `Skip.Always` / `Skip.Unless`, and `[SkippableFact]` / `[SkippableTheory]`.

**Critical Rules:**

- A missing runtime dependency or unavailable external resource MUST fail loudly, not skip.
- A destructive or environment-specific opt-in suite MUST be gated by `[Trait("Category", …)]` and excluded by default in the test runner, then included on demand — never conditioned on a runtime skip.
- A genuinely untestable branch MUST be documented with a comment at the site explaining why (referencing this principle), not silently skipped.
- Enforcement is a gate, not advisory guidance: `.claude/hooks/no-skipped-tests.sh` blocks any commit introducing a skip-family construct under `src/`, with a `--check` mode for CI/manual scans.

**Rationale**: A skipped test is worse than a missing one — it occupies a coverage slot and shows green, so the gap it leaves is invisible in every report. Making the ban constitutional and gate-enforced keeps the signal honest without relying on anyone remembering not to reach for `Skip`.

## Development Workflow

### Git Workflow

- Work on feature branches (`feature/your-feature-name`)
- Commit frequently, especially before refactoring
- Use clear, concise commit messages in imperative mood
- Reference issues when applicable: "Fix #123: Handle null server response"

### Code Review Standards

Before committing, verify:
- Build succeeds with no warnings
- All tests pass (RED-GREEN-REFACTOR cycle followed)
- Code follows naming conventions
- Public APIs have XML documentation
- No commented-out code (delete it, git remembers)
- Documentation updated (README.md, USER_GUIDE.md)

### Testing Standards

- Test project naming: `NetPace.Core.Tests`, `NetPace.Console.Tests`
- Use xUnit testing framework
- Test naming: `MethodName_Scenario_ExpectedResult`
- Given-When-Then pattern for test structure
- Tests MUST be readable, independent, fast, and deterministic
- Mock external dependencies (network, filesystem, time) for unit tests

**Do NOT test**: Spectre.Console output (trust the library), simple property getters/setters with no logic, third-party libraries

## Technology Constraints

### Required Technologies

- **Framework**: .NET 10.0 (cross-platform)
- **Language**: C# 14
- **CLI Library**: Spectre.Console
- **Testing**: xUnit
- **Package Distribution**: NuGet (NetPace.Core)

### Architecture Patterns

- **Separation of Concerns**: NetPace.Core (business logic), NetPace.Console (UI/CLI)
- **Dependency Injection Ready**: Depend on interfaces, constructor injection
- **Result Objects**: Return rich result objects with speed, duration, bytes transferred
- **Extension Methods**: For formatting/conversion logic that doesn't belong in core types
- **Options Pattern**: Complex configuration via options objects instead of many parameters

## Performance & Scale

### Performance Requirements

- Async operations for all network calls
- HttpClient best practices (singleton, pooling)
- CancellationToken support for long operations
- Measure and optimize hot paths (speed test loops)

### Units and Formatting

- Support SI (1000-based) and IEC (1024-based) unit systems
- Support BitsPerSecond and BytesPerSecond
- Auto-scale by default (Mbps, Gbps) with user override
- Consistent formatting across all output modes (normal, CSV, JSON)

## Governance

### Constitutional Authority

This constitution supersedes all other development practices and guides. All development work MUST verify compliance with these principles before proceeding.

### Amendment Process

1. Amendments require clear documentation of rationale
2. Breaking changes to principles require project maintainer approval
3. Version bump per semantic versioning rules:
   - **MAJOR**: Backward incompatible governance/principle removals or redefinitions
   - **MINOR**: New principle/section added or materially expanded guidance
   - **PATCH**: Clarifications, wording, typo fixes, non-semantic refinements
4. Any amendment to a principle that has a corresponding detailed reference in `CLAUDE.md` or `docs/conventions/` MUST note which downstream documents were reviewed.

### Compliance Review

- All pull requests MUST verify constitutional compliance
- Complexity MUST be justified against simplicity principles
- For runtime development guidance, refer to `CLAUDE.md`

**Version**: 1.5.0 | **Ratified**: 2026-04-10 | **Last Amended**: 2026-07-11
