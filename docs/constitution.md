<!--
Sync Impact Report:
Version: 2.1.0 → 2.1.1
Bump rationale: PATCH — Principle III's verbosity-levels bullet described the `--verbosity` surface that issue #267 retires for a `--minimal` format peer of `--csv` and `--json`. The replacement states the one-switch-per-format rule. Maintainer confirmed PATCH in #267. For review: the new bullet carries a normative MUST NOT the old one did not, which could read as MINOR.
Modified Principles: III — CLI Excellence
Modified Sections: Performance & Scale → Units and Formatting (output-modes list gains minimal)
Downstream reviewed (Amendment Process clause 4): CLAUDE.md (Units and Formatting), README.md, USER_GUIDE.md updated; docs/conventions/testing.md, .claude/commands/{draftissue,reviewissue,confirmissue}.md and the profile-CLI-switch CIR (a dated record) unchanged.

--- superseded, retained for history ---
Version: 2.0.0 → 2.1.0
Bump rationale: MINOR — Principle VIII (AC-to-Test Traceability) gains materially expanded
guidance: the marker is stated to be written in the test file's own comment syntax (shell test
matrices use `#`, which the previous `// SCENARIO:` wording described only for C#), and the
principle now names its enforcement. The chain itself is unchanged and the label stays
conditional, so this is expanded guidance, not a redefinition.
Enforcement is restored: `scripts/traceability-check.sh` checks the label-to-marker pairing for
the one issue the branch implements, and the `traceability` CI job runs it on every pull request
to `main`. The 2.0.0 report's "nothing mechanical now checks the label-to-marker pairing" no
longer holds and is corrected below.

Modified Principles: VIII — AC-to-Test Traceability (comment syntax generalised, enforcement named,
  and a new MUST added: a scenario only an external tool or gate can verify MUST be left
  unlabelled, which is §I's configuration/tooling carve-out seen from the traceability side)
Modified Sections: N/A
Added Sections: VIII — **Enforcement** paragraph (names the script and the CI job, and records
  that the job is not yet a required check)
Removed Sections: N/A
Maintainer approval for the MINOR amendment: issue #319.
Downstream documents reviewed (per Amendment Process clause 4):
  ✅ CLAUDE.md — gains a paired rule to run the check before raising a PR.
  ✅ .claude/commands/draftissue.md — now instructs the `**Scenario: X**` labels by default on
     test-verified scenarios, and none on tool-verified ones (§I's carve-out). Nothing wrote them
     before, which is what made the check worth having.
  ✅ .claude/commands/reviewissue.md — reviewed, unchanged: it probes for missing edge scenarios,
     never for a missing label, so an unlabelled issue raises no gap. §VIII stays conditional.
  ✅ .claude/commands/build.md — updated. It already wrote a marker per label, but still pinned
     `// SCENARIO:` as the only form, which is the exact pinning this amendment generalises; it
     now states the test file's own comment syntax, and that a marker counts only once committed.
     The first pass recorded it here as "reviewed, unchanged" before reading it — the same slip
     docs/study/319.md records for testing.md, found twice in one amendment.
  ✅ docs/agentic-workflow.md, docs/agentic-workflow-NetPace.md — the enforcement layer now
     describes the pre-merge check rather than saying the rule needs no gate of its own.
  ✅ docs/conventions/testing.md — its *Scenario traceability* section no longer pins `// SCENARIO:`
     as the only marker form, and now points at the check. The check deliberately excludes docs, so
     this file cannot be what satisfies a label it describes.

--- superseded, retained for history ---
Version: 1.8.1 → 2.0.0
Bump rationale: MAJOR — Principle VIII (AC-to-Test Traceability) is redefined. Its
traceability chain previously ran through a separate specification and test-plan document,
a route produced only by the retired multi-stage planning pipeline. The chain is redefined
to run from a `**Scenario:**` label in the GitHub issue straight to a matching `// SCENARIO:`
marker in the test, and the label becomes conditional rather than mandatory.
Redefining a principle is MAJOR under the Amendment Process, not a clarification.
Enforcement weakens with it: the `Stop` traceability gate hook is removed, so nothing mechanical
now checks the label-to-marker pairing — `/build` writing the markers is the only enforcement.
(Superseded at 2.1.0: the pairing is checked pre-merge again. See the report above.)

Modified Principles: VIII — AC-to-Test Traceability (chain redefined, label made conditional);
  IX — Behavioural Specification (housekeeping bullet and downstream references repointed)
Modified Sections: N/A
Added Sections: N/A
Removed Sections: N/A
Relocated: this file now lives at `docs/constitution.md`, alongside the rest of the project docs.
Maintainer approval for the MAJOR amendment: issue #316.
Downstream documents reviewed (per Amendment Process clause 4):
  ✅ CLAUDE.md — `@` import repointed to the new path; *Detailed References* gains
     `docs/conventions/testing.md`, the new home for the test-authoring guidance §IX cites.
  ✅ docs/conventions/testing.md — created by the same change; carries the outcome-not-mechanism
     avoid/prefer guidance for tests that §IX's downstream-references note now points at.
  ✅ .claude/commands/draftissue.md — retains the AC-drafting avoid/prefer guidance §IX cites;
     renamed by the same change, so the citation is repathed.
  ✅ .claude/commands/build.md — writes the `// SCENARIO:` markers §VIII now describes; its
     own rule already matches the redefined chain and is repointed at this principle.
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

- Follow [CLI Guidelines (clig.dev)](https://clig.dev/) strictly
- Use Spectre.Console for all console output and interaction
- Support `--help` and `--version` flags
- Provide clear error messages with actionable guidance
- Support multiple output formats (default, minimal, CSV, JSON) for scripting
- Default behavior should work for most users without flags
- Output format is selected by one switch per format, and exactly one may be selected: a format selector is not a level on a scale, and the two MUST NOT be conflated in a single option

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
- Runtime dependencies MUST use a permissive licence (MIT, Apache 2.0, or BSD). Copyleft licences (GPL, LGPL, AGPL) are prohibited without documented justification and maintainer sign-off.

**Rationale**: As a NuGet package, NetPace.Core's dependencies become consumers' dependencies — a copyleft runtime dependency propagates its licence obligations to every consumer, so a permissive-only runtime baseline is a compatibility guarantee, not just hygiene. Minimal dependencies reduce version conflicts and security surface area; the permissive-licence constraint keeps NetPace freely embeddable in closed and commercial software.

### VII. Semantic Versioning

All releases MUST follow semantic versioning (MAJOR.MINOR.PATCH):

- **MAJOR**: Breaking changes to public API
- **MINOR**: New features, backward compatible
- **PATCH**: Bug fixes, backward compatible
- Document breaking changes in release notes
- Discuss public API changes before implementation

**Rationale**: NuGet consumers depend on predictable versioning to avoid breaking changes. Semantic versioning is industry standard for package distribution.

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

**Enforcement**: `scripts/traceability-check.sh` checks the pairing for the one issue the current branch implements, and the `traceability` CI job runs it on every pull request to `main`, so a label whose marker was never written or was lost to a later edit is reported on the pull request. That job is **not yet registered in the `Main CI/CD` ruleset**, so today it reports without blocking; registering the fixed-name `traceability` context is the step that makes it binding. The direction is one way — label → marker — because a scenario may legitimately be covered by a test that already existed. The reverse direction (the invented-marker rule above) stays a judgement call for review. Agents run the same script locally before raising a PR (`CLAUDE.md`).

**Rationale**: The issue is the specification, so traceability runs directly from it to the test — one hop, verifiable by searching the issue body against the test files. Making the label conditional keeps lightweight issues cheap to write while preserving an exact, checkable link wherever an author chose to draw one. The chain needs a gate because it spans artefacts no single stage owns: the issue is written at draft time, the marker at build time, and anything between then and merge can break the pair without either end looking wrong on its own.

### IX. Behavioural Specification (NON-NEGOTIABLE)

Acceptance criteria and tests MUST describe outcomes an outside observer can verify, not the mechanism that delivers them. Multiple reasonable implementations of the same feature MUST satisfy the same ACs and pass the same tests.

**The independence test**: would this AC (or test) still hold under a different reasonable implementation of the same feature? If no, it is describing the mechanism, not the outcome.

**Critical Rules:**

- ACs MUST be phrased as user-observable outcomes. Mechanism details MUST NOT appear in ACs, including: CSS classes, DOM IDs or element types, animation specifics, font names/weights/colours, and pixel measurements; HTTP methods, endpoint paths, and status codes; response/payload schemas (JSON keys, field names); database tables, collections, columns, or indexes; algorithm or protocol choices (hash functions, signature schemes, encryption modes); framework or library picks; storage technology; specific URLs or ports; timing values (Ns / Nms) and polling cadences; exact error message strings; and log line formats or log levels.
- Tests MUST verify the AC as written, not the chosen implementation. A test that would fail under a different reasonable implementation of the same AC is testing mechanism, not outcome.
- Project housekeeping (project exists, sln updated, scaffolding created) is not an acceptance criterion — it is a step on the way to one, and belongs in the issue's technical notes.
- **Regression exception**: an AC or test that pins a specific mechanism is permitted only when it exists to prevent a named, previously-fixed bug. Reference the bug in the AC text, scenario name, or a one-line comment in the test so future readers understand why the coupling exists.

**Rationale**: Mechanism-coupled ACs invite brittle, implementation-mirroring tests that lock the codebase to its current shape and make refactors expensive. Outcome-level ACs preserve the implementer's freedom to choose the simplest mechanism, keep the test suite meaningful through refactors, and give a reviewer an enforceable rule rather than style guidance.

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
- Console output MUST be verified against a committed snapshot, never hand-rolled string matching

**On snapshots**: a changed snapshot is a changed user-visible contract — read it in the diff, never blind-accept it. This does not contradict "do not test Spectre.Console": the library's own rendering is its business; the text NetPace composes and hands to it is ours. A targeted assertion stays correct for a single observable fact that is not about output shape — an exit code, or one specific line present or absent; whenever the assertion is really "the output looks like this", it belongs in a snapshot.

**Do NOT test**: Spectre.Console's own rendering (trust the library — snapshot NetPace's composed output instead), simple property getters/setters with no logic, third-party libraries

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
- Consistent formatting across all output modes (default, minimal, CSV, JSON)

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

**Version**: 2.1.1 | **Ratified**: 2026-04-10 | **Last Amended**: 2026-10-07
