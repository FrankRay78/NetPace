# Conventions

NetPace's additions to the SDLC commands under [`.claude/commands/`](.claude/commands/). The commands hold the method — their steps, the verdict protocol, the chain scripts that read it and the sibling command names — and read this file for what is particular to this repository: its build, test and format commands, the rules of its codebase (test-driven development, testing, acceptance criteria, code quality, dependencies, versioning, commits), documentation obligations, issue sources and the hooks and plugins a port needs. Each rule is stated here once and nowhere else; `CLAUDE.md` holds the product principles and `REVIEW.md` what a reviewer checks, and none of the three overrides another. Each section sits under a heading the commands document in their *Repository conventions* table, is read together with the command's own generic rule for that concern, and wins where the two conflict. A command that finds no section for a concern runs on its generic rule alone. The detailed guides under [`docs/conventions/`](docs/conventions/) are linked, not restated. `CLAUDE.md` imports this file, so every session in this repository sees it without a second copy being kept there. The review criteria `/review-pr` and `/review-followup` apply live in [`REVIEW.md`](REVIEW.md), not here.

## Build, test and format

- **Build-and-test command** — `dotnet build ./src && dotnet test ./src`, run from the repository root. Both halves, in that order. Run both clean before committing: no failing test, no build warning.
- **Never `--no-build`** — the `green-gate.sh` hook denies it when stale, and rightly.
- **Formatter** — `dotnet format ./src/NetPace.sln`. `/build` never runs it; it belongs to `/verify`'s formatting pass.

The formatter is the bare command, deliberately — the same one CI runs as `dotnet format ./src/NetPace.sln --verify-no-changes` ([dotnet.yml](.github/workflows/dotnet.yml)). It was previously the narrower `dotnet format style … && dotnet format whitespace …`, which can leave a tree that CI's check then rejects: a branch verified locally would fail the format check on the pull request. Use CI's command, so what passes here is what passes there, whatever the gap between them turns out to be. The bare command also runs analyzer fixes, so read what it changed rather than assuming it only moved whitespace.

The explicit `./src/NetPace.sln` argument is **required, not decorative**: `dotnet format` looks for a project or solution in the *current directory only*, and NetPace's solution is at `src/`, not the repository root. Omitting it fails with `Could not find a MSBuild project file or solution file`.

## Test-driven development

Every line of production code is written in response to a failing test, in the RED-GREEN-REFACTOR cycle: write the failing test that describes the behaviour and watch it fail; write the minimum code that passes and watch it pass; commit, then improve the design with the tests still passing. Never write production code without a failing test first, never skip the RED step, never refactor on red, never add a feature no test covers, and never proceed while tests are failing.

**Configuration, tooling and CI changes.** A change to configuration, tooling or CI is not production code, and its RED step is the real tool or gate failing. Run the tool before the change and capture the failure, make the change, run it again, and record both outcomes in the commit message or pull request body — that is the RED-GREEN evidence, and it is complete. Do not write a bespoke test to stand in for a tool that already performs the check: a hand-rolled reimplementation of a formatter, linter or config parser covers strictly less than the tool it imitates, carries its own bugs, and reports green when its own matching logic fails. Do make the check repeatable: where the tool can run in CI, add it there, so the invariant is gated rather than verified once. `/build` applies this carve-out when the change is not production code.

## Testing

- **Conventions** — test projects `NetPace.Core.Tests` and `NetPace.Console.Tests`; xUnit; `MethodName_Scenario_ExpectedResult` naming; Given-When-Then structure; a test file mirroring the source file's name (`OoklaSpeedtest.cs` → `OoklaSpeedtestTests.cs`). Tests are readable, independent, fast and deterministic, and unit tests mock external dependencies (network, filesystem, time). [`docs/conventions/testing.md`](docs/conventions/testing.md) is the guide to read before writing tests, and [`docs/conventions/csharp-style.md`](docs/conventions/csharp-style.md) holds the style.
- **Test in `NetPace.Core`** — all public APIs; speed calculations, unit conversions, server selection; happy paths, alternative configurations, and error scenarios (invalid input, network failures, timeouts).
- **Console output** — verified against a committed snapshot, never hand-rolled string matching: `await Verify(result.Output)`, with snapshots in `NetPace.Console.Tests/Expectations/*.verified.txt`. A test with no snapshot fails first — review the generated `.received.txt`, then rename it to `.verified.txt` to accept. A changed snapshot is a changed user-visible contract: read it in the diff, never blind-accept it. This does not contradict the rule below not to test Spectre.Console: the library's own rendering is its business; the text NetPace composes and hands to it is ours. A targeted assertion stays correct for a single observable fact that is not about output shape — an exit code, or one specific line present or absent; whenever the assertion is really "the output looks like this", it belongs in a snapshot.
- **Do not test** Spectre.Console's own rendering (trust the library — snapshot NetPace's composed output instead), simple property getters and setters with no logic, or third-party libraries.
- **No skipped tests.** No test in the suite may be skipped: a skipped test reports green while verifying nothing, and the gap it leaves is invisible in every report. The whole skip family is prohibited — `[Fact(Skip=…)]` / `[Theory(Skip=…)]`, `Assert.Skip`, `Skip.If` / `Skip.IfNot` / `Skip.Always` / `Skip.Unless`, and `[SkippableFact]` / `[SkippableTheory]`. A missing runtime dependency or unavailable external resource fails loudly, not skips. A destructive or environment-specific opt-in suite is gated by `[Trait("Category", …)]`, excluded by default in the test runner and included on demand — never conditioned on a runtime skip. A genuinely untestable branch is documented with a comment at the site explaining why, referencing this rule, not silently skipped. The `.claude/hooks/no-skipped-tests.sh` hook blocks any commit introducing a skip-family construct under `src/`, with a `--check` mode for CI and manual scans.

## Acceptance criteria

- **Outcomes, not mechanism** — acceptance criteria and tests describe outcomes an outside observer can verify, not the mechanism that delivers them, so that multiple reasonable implementations of the same feature satisfy the same criteria and pass the same tests. The independence test: would this criterion or test still hold under a different reasonable implementation of the same feature? If not, it describes the mechanism. Mechanism includes, unless it is an interface contract: CSS classes, DOM IDs or element types, animation specifics, font names, weights and colours, pixel measurements; HTTP methods, endpoint paths and status codes; response or payload schemas (JSON keys, field names); database tables, collections, columns or indexes; algorithm or protocol choices (hash functions, signature schemes, encryption modes); framework or library picks; storage technology; specific URLs or ports; timing values and polling cadences; exact error message strings; and log line formats or levels.
- **Interface contracts are outcomes** — a detail that users, their scripts or other programs work with directly is part of the outcome, and a criterion may name it exactly: a command, option or flag name; a config key; a field in machine-readable output; an exit code; a file format users write; a public library member; a route that clients call. The test is who touches it: if changing it would break a user, a script or a consumer, it is a contract. The exact wording of a message meant for a person to read stays mechanism.
- **Tests verify the criterion as written**, not the chosen implementation: a test that would fail under a different reasonable implementation of the same criterion is testing mechanism. Project housekeeping — a project exists, the solution is updated, scaffolding is created — is a step on the way to a criterion, not one itself, and is left out. **Regression exception:** a criterion or test may pin a specific mechanism only when it exists to prevent a named, previously fixed bug; reference the bug in the criterion text, the scenario name or a one-line comment in the test so future readers understand why the coupling exists. `/draftissue` works this rule for drafting and [`docs/conventions/testing.md`](docs/conventions/testing.md) for test authoring.
- **Scenario labels** — give each scenario a test will verify a `**Scenario: <name>**` label on its own line, with the Given / When / Then beneath it. At least one test then carries a `SCENARIO: <name>` marker naming it exactly, written in the test file's own comment syntax — `// SCENARIO: <name>` in C#, `# SCENARIO: <name>` in a shell test matrix — so what must match character for character is the name after `SCENARIO:`, not the comment characters before it. `/build` writes that test, and the *Traceability* check below enforces the pairing. Labels are optional: an issue may carry none, and that is not a defect; where one is present the matching marker is mandatory, because the pair is the key linking an acceptance criterion to the test that verifies it, and a marker naming no label, or a label with no marker, traces nothing. Leave a scenario **unlabelled** when only an external tool or gate can check it — "the formatter passes", "the build is warning-free" — since a label would demand a test that should not exist (the configuration/tooling carve-out in *Test-driven development*). Never invent a marker: a `SCENARIO:` marker with no corresponding label looks like a traceability key and is worse than none. The label must be a bare line of the body, outside any code fence: one mid-sentence, in backticks or inside a fence describes the convention rather than declaring a scenario, and is deliberately not read as a label. The fence below is for presentation only:

  ```
  **Scenario: Server list is screened before measuring**
  Given a server list containing an unreachable host, when a test runs, then that host is not measured.
  ```

- **Traceability** — the issue-label → test-marker chain is checked one way, label to marker, because a scenario may legitimately be covered by a test that already existed; the invented-marker rule above stays a judgement call for review. Run `bash scripts/traceability-check.sh` before raising a pull request. It reads the issue number off the branch and names any `**Scenario: X**` label with no `SCENARIO: X` marker in a **committed** test file — an uncommitted marker does not count. The `traceability` CI job runs the same script on every pull request to `main` and is a required check in the `Main CI/CD` ruleset, so a label whose marker was never written or was lost to a later edit blocks the merge, and running the script locally only moves the failure to before the push.

## Code quality

- **Standards** — PascalCase for classes, methods and properties, camelCase for private fields and variables; XML documentation on all public APIs; network operations async with `CancellationToken` support; nullable reference types enabled; inputs validated early, exceptions never swallowed, specific exception types used; the build succeeds with zero warnings. [`docs/conventions/csharp-style.md`](docs/conventions/csharp-style.md) elaborates them.
- **Cross-platform** — all code runs on Windows, Linux and macOS without platform-specific workarounds: target .NET 10.0; consider file paths, line endings and console encoding; avoid platform-specific APIs unless absolutely necessary; document any platform-specific behaviour clearly; test on multiple platforms before release.
- **Trim and AOT safety** — keep production code trim/AOT-safe: no reflection-heavy or non-trim-safe code. NetPace targets AOT-trimmable builds (Spectre.Console.Cli was replaced for this reason); avoid runtime reflection, keep types annotation-clean.

## Dependencies

`NetPace.Core` keeps its dependencies minimal. It is published to NuGet, so its dependencies become its consumers' dependencies: every dependency is justified (fewer version conflicts for consumers); the .NET base class library is preferred over a third-party package where possible; every dependency and its purpose is documented; dependency security is reviewed regularly. Runtime dependencies use a permissive licence (MIT, Apache 2.0 or BSD); a copyleft licence (GPL, LGPL, AGPL) is prohibited without documented justification and maintainer sign-off, because a copyleft runtime dependency propagates its obligations to every consumer. `/build` adds no dependency to `NetPace.Core` unattended.

## Public API and breaking changes

- **Protected public API** — the public surface of `NetPace.Core`, which is published to NuGet, so consumers depend on it.
- **Versioning** — releases follow semantic versioning (MAJOR.MINOR.PATCH): MAJOR for breaking changes to the public API, MINOR for backward-compatible features, PATCH for backward-compatible fixes; breaking changes are documented in release notes, and public API changes are discussed before implementation. The pre-1.0 stance below suspends those obligations for now.
- **Pre-1.0 stance** — while every released version of NetPace is below 1.0.0, a breaking or public-API change to the CLI or to `NetPace.Core` that improves the app needs no prior discussion, no approval and no justification against the versioning bullets. One obligation remains: the issue or pull request making the change states plainly what breaks and for whom — the maintainer sees every break, and is not asked to approve it first. There is no deprecation period: an option, output format or public library member may be removed or renamed outright in any release. Nobody classifies a change as MAJOR, MINOR or PATCH; the maintainer chooses the version number when tagging a release. A breaking change need not be called out in release notes; users are warned once, by the standing pre-1.0 notice in `README.md` and `resources/nuget/README.md`. The stance ends when a 1.0.0 release is tagged, from which point the versioning bullets apply in full; no date or milestone for 1.0.0 is implied. This is the stance that lifts `/build`'s discussion duty on the protected public API.

## Commit messages

Work on feature branches (`feature/<N>-<short-slug>`); commit frequently, especially before refactoring. Messages are clear and concise, in the imperative mood, and reference the issue as `Refs #<N>: …` — never a GitHub closing keyword (`Fix`/`Fixes`/`Close`/`Closes`/`Resolve`/`Resolves` `#<N>`), which closes the issue the moment the commit reaches `main`, before review. The pull request body's `Closes #<N>` is the only place that should close an issue.

## Documentation

- Any new or changed **public API in `NetPace.Core`** needs `///` XML docs — they ship to NuGet consumers.
- Any changed **CLI option** needs the `README.md` `--help` snapshot and `USER_GUIDE.md` updated, and a design-doc cross-reference where applicable. Per-release "what changed" notes are GitHub-auto-generated from merged pull requests — there is no `CHANGELOG.md` to maintain.
- Any change to **`release-binaries.yml`, or other release-pipeline scope**, needs [`docs/RELEASING.md`](docs/RELEASING.md) updated: the release matrix, runner-per-RID rationale, naming convention, smoke-test contract and size-assertion contract live there, and out-of-sync release docs make adding a new RID or variant cost extra. Action-version bumps such as `actions/checkout@v4→v5` are exempt.
- **Write markdown one line per paragraph**, bullet and table row — no hard column wrapping — and let the viewer soft-wrap it; a fixed-column hard wrap reflows the whole block on a one-word edit and buries the real change in a noisy diff.

## Change-intent records

This project keeps them. The convention is [`docs/conventions/change-intent-records.md`](docs/conventions/change-intent-records.md), whose decision table `/build` runs, and records live in [`docs/change-intent-records/`](docs/change-intent-records/).

- **The `→ No CIR` rows that most often apply.** Reasoning already recorded in the issue's confirmed decisions is linked, not restated — unless the decision constrains future work (a public API, an interface contract, dependency direction), which keeps a short record linking the issue for its reasoning. A change only to documentation written for the project's readers needs none at all; command prompts, conventions and workflows are not covered by that row, because they direct future work.
- **Length and shape.** About **150 words**: the intent in a sentence, each rejected alternative with its reason, and any constraint the code will not make obvious.

## Scratch files

`.claude/scratch/` is git-ignored. `/raise-pr` writes the pull request body to `.claude/scratch/raise-pr-body.md`; the `Write` tool creates the directory if it is missing. `/draftissue` writes the issue body to `.claude/scratch/gh-issue-body-<slug>.md`, where the slug is a short kebab-case form of the title, and `/reviewissue` writes the review comment to `.claude/scratch/reviewissue-body.md`.

## Pull requests

The `issue-link` check (`.github/workflows/pr-issue-link.yml`) fails any pull request that GitHub says closes no open issue in this repository. It runs on every pull request and re-runs on `edited`, so fixing the body needs no new push, and it does not block the merge.

## Issue sources

- **Backlog** — `backlog <number>` and `item <number>` mean a row of the [NetPace Consolidated Backlog](https://github.com/FrankRay78/NetPace/wiki/NetPace-Consolidated-Backlog). Read it from the wiki repository: `git clone https://github.com/FrankRay78/NetPace.wiki.git` into the scratch directory, then `NetPace-Consolidated-Backlog.md`. A row gives the number, the feature, why it matters, its origin and its size; a size such as "S, after 17" names the item it depends on. Also read the prose under the row's table that names the item, the *Open questions* that name it, and the *Not doing* and *Already shipped* tables, which bound its scope.
- **Research** — a row's Origin column names a section of [Background research](https://github.com/FrankRay78/NetPace/wiki/Background-research) (`Background-research.md` in the same repository). Read that section for the evidence behind the row.
- **Linking** — Related links the backlog page and names the item number; the page has no per-row anchors.

## Issue concerns

What `/draftissue` considers when it looks for the requirements decisions a source leaves open:

- **Library consumers** — `NetPace.Core` is published to NuGet. Where the feature has a library side, its consumers are users too: say whether the capability is offered to them, not only to the CLI.
- **Output modes** — a result shown to the user must follow the rule `CLAUDE.md` → *Units and Formatting* states for default, minimal, CSV and JSON output. Say which modes are in scope.
- **CLI docs** — if the feature adds or changes a CLI option, the documentation *Documentation* names for one is part of the work; make it an acceptance criterion.
- **Breaking changes** — under the stance *Public API and breaking changes* states, a breaking change needs no approval, but the issue must state plainly what breaks and for whom.

## Issue review checks

What `/reviewissue` considers alongside its generic categories, under the same test: raise a gap only where the issue leaves the point open.

- **Library first** — does the feature start in `NetPace.Core`, and does it change `ISpeedTestService`?
- **Output modes** — does it hold to the rule `CLAUDE.md` → *Units and Formatting* states for default, minimal, CSV and JSON output?
- **CLI docs** — if it touches a CLI option, is the documentation *Documentation* names for one in scope?
- **Acceptance criteria** — do they describe outcomes rather than mechanism, and are the `**Scenario:**` labels right (*Acceptance criteria*)?
- **AOT trimming** — is it safe for trimmed builds (*Code quality*)?
- **Server selection and measurement** — if it touches either, does `docs/architecture/server-screening-and-measurement.md` apply?

## Audit

What `/audit-codebase` reads about this repository.

- **Scope** — everything tracked under `src/`, `examples/`, `scripts/`, `docs/`, `resources/`, `docker/`, `.github/`, `.claude/`, and the root files (`CLAUDE.md`, `CONVENTIONS.md`, `REVIEW.md`, `README.md`, `INSTALLATION.md`, `USER_GUIDE.md`, `.editorconfig`, `global.json`). Security is out of scope: CI already fails on a dependency with a known advisory (`.github/workflows/dotnet.yml`) and runs CodeQL.
- **Dependencies are in scope** — `NetPace.Core` is published to NuGet, so its dependencies become its consumers' dependencies (*Dependencies*). A deprecated or outdated package, an unjustified `NetPace.Core` dependency, or a non-permissive licence is a finding. Distinguish runtime dependencies from test-only and benchmark-only ones; the licence rule binds the first.
- **Evidence** — the tools that already decide a question, run from the repository root:

  | Evidence | Command | Feeds |
  |---|---|---|
  | Build and test result, coverage | `dotnet build ./src` then `dotnet tool exec -y dotnet-coverage@18.0.6 -- collect "dotnet test ./src --no-build" -f cobertura -o .claude/scratch/audit/coverage.cobertura.xml` — the `--no-build` is safe here because it runs straight after the build it reuses; the `green-gate.sh` hook does not inspect a `dotnet test` nested inside `dotnet tool exec` | lenses 1, 2 |
  | Deprecated packages | `dotnet list src package --deprecated` | lens 1 |
  | Outdated packages | `dotnet list src package --outdated` | lens 1 |
  | Package licences | `dotnet tool exec -y nuget-license@4.0.18 -- -i src/NetPace.sln -t` | lens 1 |
  | Unused internal and private symbols | `dotnet tool exec -y roslynator.dotnet.cli@1.0.1 -- find-symbol src/NetPace.sln --unused --visibility internal private` | lens 5 |
  | Copy-paste clones | `npx --yes jscpd@5.4.1 --format csharp --min-lines 8 --ignore "**/bin/**,**/obj/**" --reporters json --output .claude/scratch/audit/jscpd --silent src` | lens 5 |

  The test projects reference no coverage collector, so `dotnet test --collect:"XPlat Code Coverage"` runs the suite and collects nothing; the wrapper above is what produces the report. It covers every loaded assembly, third-party ones included, so lens 2 reads only the `NetPace.Core` and `netpace` packages in it. `npx` is not on the project allow-list, so a headless run will be refused it.

- **Rules to audit (lens 1)** — the rules this file states and the product principles `CLAUDE.md` → *Product Principles* states, concentrating on any amended inside the window:
  - **Library-first** — does `NetPace.Core` reference anything in `NetPace.Console`, or assume a console host (writing to the console, reading environment a library should be handed)? Does any provider-agnostic type (`Profile`, `SpeedUnit`, the result records) reach forward into a concrete provider under `Clients/`? Is any behaviour that belongs in the library implemented only in the console project?
  - **CLI excellence** — is each output format selected by its own switch, with exactly one selectable? Do error messages say what to do next? Does every command and option that exists appear in `--help`?
  - **Units and formatting** — is a given measurement formatted identically across default and minimal output, including under `--unit-scale`? Trace one value through both and report any path that formats it separately. CSV and JSON are deliberately not among them (`CLAUDE.md` → *Units and Formatting*): check only that their numbers agree with each other.
  - **Cross-platform** (*Code quality*) — hard-coded path separators, line endings, encodings, or platform APIs without a documented reason.
  - **Code quality** — a public `NetPace.Core` member without XML documentation; a network operation that is not async or cannot be cancelled; an exception caught and discarded; a `#pragma warning disable` or `[SuppressMessage]` with no stated reason.
  - **Dependencies** — from the deprecated, outdated and licence evidence: a `NetPace.Core` runtime dependency with no stated justification, any runtime dependency whose licence is not MIT, Apache 2.0 or BSD, and any deprecated package.
  - **Trim and AOT safety** — runtime reflection, `dynamic`, or serialisation that is not source-generated, anywhere on a path the published binary reaches.
  - **No skipped tests** (*Testing*) — the commit hook covers new writes; confirm with `bash .claude/hooks/no-skipped-tests.sh --check` that nothing already committed slips past it.
- **Test suite (lens 2)** — a known non-deterministic test exists in the `OoklaSpeedtest` latency-margin test; confirm whether it is still the only one. Coverage is read for public `NetPace.Core` members and whole output paths with no covering test. The markers in `scripts/traceability-check.sh`, `scripts/traceability-check.tests.sh` and `docs/` are fixtures and examples of the convention, not traceability keys.
- **Documents against code (lens 3)** — `README.md`, `INSTALLATION.md` and `USER_GUIDE.md` against the real CLI: build it and compare the `--help` output with the documented snapshot, and check every documented option, default and example still exists and behaves as described. `resources/nuget/README.md` and `examples/ConsoleApp` against the public `NetPace.Core` API: does each sample still compile against the current types? `docs/architecture/*.md` against the code they describe — settings names, defaults, and the stated reasons. `docs/RELEASING.md` against `.github/workflows/release-binaries.yml` and `publish-nuget.yml`: the release matrix, naming convention, smoke-test and size-assertion contracts.
- **Harness (lens 4)** — the two workflow documents `docs/agentic-workflow.md` and `docs/agentic-workflow-NetPace.md` are part of the set the harness must agree with, and `.claude/hooks/README.md` is one of the documents that describe what a hook does.
- **Dead code (lens 5)** — the Roslynator list reports members that a source generator consumes as unused: the `JsonSerializerContext` subclasses in `NetPace.Console` are the known case.
- **Issue label** — `housekeeping`.

## Hooks, scripts and plugins

What a port of the commands must bring, beyond the commands themselves.

- **Hooks**, wired in `.claude/settings.json` — `.claude/hooks/green-gate.sh`, which denies a stale `--no-build`; `.claude/hooks/no-skipped-tests.sh`, which runs on every `git commit` and so can block any commit a command makes; `.claude/hooks/no-chmod.sh`, which refuses the executable-bit forms of `chmod` that stall an unattended run on a permission prompt; and the `Bash(gh pr create:*)` `PreToolUse` hook that re-runs the build-and-test command when a pull request is created — which `/verify` weighs and does not rely on, since `/raise-pr` may not run for a long while, or at all.
- **Scripts** — `scripts/traceability-check.sh`. The chain scripts are part of the harness and named in the steps.
- **Plugins** — `pr-review-toolkit`, which supplies every wave-1 reporter and the wave-2 `code-simplifier` in `/verify`. `/build` spawns no reviewer subagents.
