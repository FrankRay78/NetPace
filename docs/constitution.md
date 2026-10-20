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

**Configuration, tooling and CI changes:**

A change to configuration, tooling, or CI is not production code, and its RED step is the real tool or gate failing. Run that tool before the change and capture the failure, make the change, run it again — then record both outcomes in the commit message or PR body. That is the RED-GREEN evidence, and it is complete.

- MUST NOT write a bespoke test to stand in for a tool that already performs the check. A hand-rolled reimplementation of a formatter, linter, or config parser covers strictly less than the tool it imitates, carries its own bugs, and reports green when its own matching logic silently fails — the exact opposite of what the RED step exists to prove.
- MUST still make the check repeatable. Where the tool can run in CI, add it there, so the invariant is gated rather than verified once.

**Rationale**: TDD ensures every feature is testable, reduces bugs, improves design, and provides living documentation through tests. This is foundational to NetPace quality standards. The carve-out exists because the rule was previously read as "a `dotnet test` case must exist for every change", which on a config-only branch produced a bespoke `.editorconfig` reader written purely to give the RED step something to execute — larger than the fix, buggier than the tool it imitated, and deleted before merge (#249). Where a real tool already decides the question, its exit code is the better test.

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

- Follow [CLI Guidelines (clig.dev)](https://clig.dev/) strictly, with one exception while NetPace is pre-1.0 (Principle VII): clig.dev's advice to warn users before changing or removing an option does NOT apply, because there is no deprecation period. A removed or renamed option SHOULD fail with a message saying it was removed, where that is simple to do — a preference, never a requirement, and a review MUST NOT raise its absence as a gap. The exception lapses when 1.0.0 is tagged.
- Use Spectre.Console for all console output and interaction
- Support `--help` and `--version` flags
- Provide clear error messages with actionable guidance
- Support multiple output formats (default, minimal, CSV, JSON) for scripting
- Default behavior should work for most users without flags
- Output format is selected by one switch per format, and exactly one may be selected: a format selector is not a level on a scale, and the two MUST NOT be conflated in a single option

**Rationale**: CLI applications are tools for users. Following established guidelines ensures NetPace is intuitive, scriptable, and professional. The pre-1.0 exception exists because "strictly" was being read as importing clig.dev's deprecation advice wholesale: the review of #267 raised a warning period for `--verbosity` that the maintainer did not want, and the exception settles that question once rather than per option.

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
- Runtime dependencies MUST use a permissive licence (MIT, Apache 2.0, or BSD). Copyleft licences (GPL, LGPL, AGPL) are prohibited without documented justification and maintainer sign-off.

**Rationale**: As a NuGet package, NetPace.Core's dependencies become consumers' dependencies — a copyleft runtime dependency propagates its licence obligations to every consumer, so a permissive-only runtime baseline is a compatibility guarantee, not just hygiene. Minimal dependencies reduce version conflicts and security surface area; the permissive-licence constraint keeps NetPace freely embeddable in closed and commercial software.

### VII. Semantic Versioning

All releases MUST follow semantic versioning (MAJOR.MINOR.PATCH):

- **MAJOR**: Breaking changes to public API
- **MINOR**: New features, backward compatible
- **PATCH**: Bug fixes, backward compatible
- Document breaking changes in release notes
- Discuss public API changes before implementation

**Pre-1.0 stance**: while every released version of NetPace is below 1.0.0, the bullets above do not govern breaking changes or public API changes. The following governs instead, and overrides any rule elsewhere in this constitution, in `CLAUDE.md`, or in an agent prompt that says otherwise:

- A breaking or public API change to the CLI or to `NetPace.Core` that improves the app needs NO prior discussion and NO approval, and needs no justification against the bullets above.
- One obligation remains: the issue or pull request making the change MUST state plainly what breaks and for whom. The maintainer sees every break; they are not asked to approve it first.
- There is NO deprecation period. An option, output format or public library member may be removed or renamed outright in any release.
- Nobody classifies a change as MAJOR, MINOR or PATCH. The maintainer chooses the version number when tagging a release.
- A breaking change need NOT be called out in release notes. Users are warned once, by the standing pre-1.0 notice in `README.md` and `resources/nuget/README.md`.

The stance ends when a 1.0.0 release is tagged. From that point the bullets above apply in full, with no further amendment to this principle needed. No date or milestone for 1.0.0 is implied.

**Rationale**: NuGet consumers depend on predictable versioning to avoid breaking changes. Semantic versioning is industry standard for package distribution. The pre-1.0 stance is stated here because it was previously written down only in dated change-intent records that no rule reads, so every breaking change was re-argued from first principles and reached the same conclusion each time — #267 over a `--verbosity` deprecation period, #268 over whether adding members to `ISpeedTestService` was MAJOR or MINOR. Semver itself reserves `0.y.z` for exactly this: a public API that may change at any time. Stating it once removes a recurring negotiation without weakening anything that will apply from 1.0.0.

### VIII. AC-to-Test Traceability

Where a GitHub issue's acceptance scenario carries a `**Scenario:**` label, at least one test MUST carry a `// SCENARIO:` marker naming it exactly:

```
issue:  **Scenario: Server list is screened before measuring**
test:   // SCENARIO: Server list is screened before measuring
```

The marker is written in the test file's own comment syntax, so a shell test matrix carries `# SCENARIO:` where a C# test carries `// SCENARIO:`. What must match exactly is the name after `SCENARIO:`, not the comment characters before it.

The label itself must be a bare line of the issue body, outside any code fence: one written mid-sentence, in backticks, or inside a fence describes the convention rather than declaring a scenario, and is deliberately not read as a label. The example above is fenced for presentation only.

The label is optional — an issue may carry none, and that is not a defect. Where one is present the matching marker is mandatory: the label-to-marker pair is the traceability key linking an acceptance criterion to the test that verifies it. Names MUST match exactly; a marker naming no label, or a label with no marker, traces nothing.

A scenario that only an external tool or gate can verify — §I's configuration/tooling carve-out — MUST be left unlabelled. Labelling one demands a test that should not exist, and the only way to satisfy the demand is the hand-rolled stand-in §I bans.

Markers MUST NOT be invented. A `SCENARIO:` marker with no corresponding issue label looks like a traceability key and is worse than no marker at all.

**Enforcement**: `scripts/traceability-check.sh` checks the pairing for the one issue the current branch implements, and the `traceability` CI job runs it on every pull request to `main`, so a label whose marker was never written or was lost to a later edit is reported on the pull request. The fixed-name `traceability` context is a required check in the `Main CI/CD` ruleset, so a failure blocks the merge. The direction is one way — label → marker — because a scenario may legitimately be covered by a test that already existed. The reverse direction (the invented-marker rule above) stays a judgement call for review. Agents run the same script locally before raising a PR (`CLAUDE.md`).

**Rationale**: The issue is the specification, so traceability runs directly from it to the test — one hop, verifiable by searching the issue body against the test files. Making the label conditional keeps lightweight issues cheap to write while preserving an exact, checkable link wherever an author chose to draw one. The chain needs a gate because it spans artefacts no single stage owns: the issue is written at draft time, the marker at build time, and anything between then and merge can break the pair without either end looking wrong on its own.

### IX. Behavioural Specification (NON-NEGOTIABLE)

Acceptance criteria and tests MUST describe outcomes an outside observer can verify, not the mechanism that delivers them. Multiple reasonable implementations of the same feature MUST satisfy the same ACs and pass the same tests.

**The independence test**: would this AC (or test) still hold under a different reasonable implementation of the same feature? If no, it is describing the mechanism, not the outcome.

**Critical Rules:**

- ACs MUST be phrased as user-observable outcomes. Mechanism details MUST NOT appear in ACs. Unless it is an interface contract (next rule), mechanism includes: CSS classes, DOM IDs or element types, animation specifics, font names/weights/colours, and pixel measurements; HTTP methods, endpoint paths, and status codes; response/payload schemas (JSON keys, field names); database tables, collections, columns, or indexes; algorithm or protocol choices (hash functions, signature schemes, encryption modes); framework or library picks; storage technology; specific URLs or ports; timing values (Ns / Nms) and polling cadences; exact error message strings; and log line formats or log levels.
- **Interface contracts are outcomes**: a detail that users, their scripts or other programs work with directly — a command, option or flag name; a config key; a field in machine-readable output; an exit code; a file format users write; a public library member; a route that clients call — is part of the outcome, and an AC MAY name it exactly. The test is who touches it: if changing it would break a user, a script or a consumer, it is a contract. The exact wording of a message meant for a person to read stays mechanism.
- Tests MUST verify the AC as written, not the chosen implementation. A test that would fail under a different reasonable implementation of the same AC is testing mechanism, not outcome.
- Project housekeeping (project exists, sln updated, scaffolding created) is not an acceptance criterion — it is a step on the way to one, and is left out of the criteria.
- **Regression exception**: an AC or test that pins a specific mechanism is permitted only when it exists to prevent a named, previously-fixed bug. Reference the bug in the AC text, scenario name, or a one-line comment in the test so future readers understand why the coupling exists.

**Rationale**: Mechanism-coupled ACs invite brittle, implementation-mirroring tests that lock the codebase to its current shape and make refactors expensive. Outcome-level ACs preserve the implementer's freedom to choose the simplest mechanism, keep the test suite meaningful through refactors, and give a reviewer an enforceable rule rather than style guidance. The interface-contract rule exists because the mechanism list was written for a web application and read literally on a CLI and library: it banned naming the JSON field, option or exit code that scripts depend on, which is the very outcome such an AC must promise — a different implementation that renamed it would break its users, so the independence test already treats it as outcome.

**Downstream references**: detailed avoid/prefer guidance lives in `.claude/commands/draftissue.md` (AC drafting) and `docs/conventions/testing.md` (test authoring). Update those in lockstep with any change to this principle.

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
- Reference issues when applicable: "Refs #123: Handle null server response" — never a GitHub closing keyword (`Fix`/`Fixes`/`Close`/`Closes`/`Resolve`/`Resolves` `#123`), which closes the issue the moment the commit reaches `main`, before review. The PR body is the only place that should close an issue.

### Testing Standards

- Test project naming: `NetPace.Core.Tests`, `NetPace.Console.Tests`
- Use xUnit testing framework
- Test naming: `MethodName_Scenario_ExpectedResult`
- Given-When-Then pattern for test structure
- Tests MUST be readable, independent, fast, and deterministic
- Mock external dependencies (network, filesystem, time) for unit tests
- Console output MUST be verified against a committed snapshot, never hand-rolled string matching

**On snapshots**: a changed snapshot is a changed user-visible contract — read it in the diff, never blind-accept it. This does not contradict "do not test Spectre.Console": the library's own rendering is its business; the text NetPace composes and hands to it is ours. A targeted assertion stays correct for a single observable fact that is not about output shape — an exit code, or one specific line present or absent; whenever the assertion is really "the output looks like this", it belongs in a snapshot.

**Do NOT test**: Spectre.Console's own rendering (trust the library — snapshot NetPace's composed output instead), simple property getters/setters with no logic, third-party libraries

## Authority

This constitution supersedes `CLAUDE.md` and every other guide where they conflict.
