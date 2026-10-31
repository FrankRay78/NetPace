# Conventions

NetPace's additions to the SDLC commands under [`.claude/commands/`](.claude/commands/). The commands hold the method — their steps, the verdict protocol, the chain scripts that read it and the sibling command names — and read this file for what is particular to this repository: its build, test and format commands, test conventions, product and dependency rules, documentation obligations, issue sources and the hooks and plugins a port needs. Each section sits under a heading the commands document in their *Repository conventions* table, is read together with the command's own generic rule for that concern, and wins where the two conflict. A command that finds no section for a concern runs on its generic rule alone. The detailed guides under [`docs/conventions/`](docs/conventions/) are linked, not restated. `CLAUDE.md` imports this file, so every session in this repository sees it without a second copy being kept there. The review criteria `/review-pr` and `/review-followup` apply live in [`REVIEW.md`](REVIEW.md), not here.

## Build, test and format

- **Build-and-test command** — `dotnet build ./src && dotnet test ./src`, run from the repository root. Both halves, in that order. Run both clean before committing: no failing test, no build warning.
- **Never `--no-build`** — the `green-gate.sh` hook denies it when stale, and rightly.
- **Formatter** — `dotnet format ./src/NetPace.sln`. `/build` never runs it; it belongs to `/verify`'s formatting pass.

The formatter is the bare command, deliberately — the same one CI runs as `dotnet format ./src/NetPace.sln --verify-no-changes` ([dotnet.yml](.github/workflows/dotnet.yml)). It was previously the narrower `dotnet format style … && dotnet format whitespace …`, which can leave a tree that CI's check then rejects: a branch verified locally would fail the format check on the pull request. Use CI's command, so what passes here is what passes there, whatever the gap between them turns out to be. The bare command also runs analyzer fixes, so read what it changed rather than assuming it only moved whitespace.

The explicit `./src/NetPace.sln` argument is **required, not decorative**: `dotnet format` looks for a project or solution in the *current directory only*, and NetPace's solution is at `src/`, not the repository root. Omitting it fails with `Could not find a MSBuild project file or solution file`.

## Test-driven development

Constitution §I (NON-NEGOTIABLE) states the rule, and it applies as written. Its *Configuration, tooling and CI changes* carve-out is the one `/build` applies when the change is not production code: the RED step is the real tool or gate failing, never a hand-rolled test standing in for a tool that already performs the check.

## Testing

- **Conventions** — xUnit, Given-When-Then, `MethodName_Scenario_ExpectedResult`, and a test file mirroring the source file's name (`OoklaSpeedtest.cs` → `OoklaSpeedtestTests.cs`). [`docs/conventions/testing.md`](docs/conventions/testing.md) is the guide to read before writing tests, and [`docs/conventions/csharp-style.md`](docs/conventions/csharp-style.md) holds the style.
- **Test in `NetPace.Core`** — all public APIs; speed calculations, unit conversions, server selection; happy paths, alternative configurations, and error scenarios (invalid input, network failures, timeouts).
- **Console output** — `await Verify(result.Output)`; snapshots live in `NetPace.Console.Tests/Expectations/*.verified.txt`. A test with no snapshot fails first — review the generated `.received.txt`, then rename it to `.verified.txt` to accept. Constitution → *Testing Standards* for when a targeted assert is right instead.
- **No skipped tests.** Do not use `Skip`, `[Fact(Skip=…)]`, `Assert.Skip` or any of the family; the `no-skipped-tests.sh` commit hook blocks them, and constitution §X bans them.

## Acceptance criteria

- **Outcomes, not mechanism** — constitution §IX (NON-NEGOTIABLE): a criterion or test describes what an outside observer can verify, and an interface contract a user, script or consumer touches directly counts as an outcome. `/draftissue` works the rule for drafting and [`docs/conventions/testing.md`](docs/conventions/testing.md) for test authoring.
- **Scenario labels** — give each scenario a test will verify a `**Scenario: <name>**` label on its own line, with the Given / When / Then beneath it. `/build` writes a test carrying a matching `SCENARIO: <name>` marker, and `scripts/traceability-check.sh` fails the pull request if one is missing (constitution §VIII). Leave a scenario **unlabelled** when only an external tool or gate can check it — "the formatter passes", "the build is warning-free" — since a label would demand a test that should not exist (§I's configuration/tooling carve-out). The label must be a bare line of the body: one mid-sentence, in backticks or inside a code fence is not read as a label. The fence below is for presentation only:

  ```
  **Scenario: Server list is screened before measuring**
  Given a server list containing an unreachable host, when a test runs, then that host is not measured.
  ```

- **Traceability** — constitution §VIII, the issue-label → test-marker chain. Check it with `bash scripts/traceability-check.sh` before raising a pull request. It reads the issue number off the branch and names any `**Scenario: X**` label with no `SCENARIO: X` marker in a **committed** test file — an uncommitted marker does not count. The `traceability` CI job runs the same script and is a required check in the `Main CI/CD` ruleset, so running it locally only moves the failure to before the push.

## Code quality

- Constitution §V states the standards — naming, XML documentation on public APIs, async with cancellation for network operations, nullable reference types, early validation, zero build warnings — and [`docs/conventions/csharp-style.md`](docs/conventions/csharp-style.md) elaborates them.
- **Trim and AOT safety** — keep production code trim/AOT-safe: no reflection-heavy or non-trim-safe code. NetPace targets AOT-trimmable builds (Spectre.Console.Cli was replaced for this reason); avoid runtime reflection, keep types annotation-clean.

## Dependencies

`NetPace.Core` is the dependency-controlled library (constitution §VI). It is published to NuGet, so its dependencies become its consumers' dependencies: every one is justified, and a runtime dependency's licence is permissive. `/build` adds none unattended.

## Public API and breaking changes

- **Protected public API** — the public surface of `NetPace.Core`, which is published to NuGet, so consumers depend on it (constitution §II, §VII).
- **Stance on breaking changes** — while every released version is below 1.0.0, a breaking or public-API change to the CLI or to `NetPace.Core` needs no prior discussion, approval, deprecation period or MAJOR/MINOR/PATCH classification (constitution §VII, *Pre-1.0 stance*). State in the issue or pull request what breaks and for whom, and carry on. This is the stance that lifts `/build`'s discussion duty on the protected public API.

## Commit messages

The constitution's *Git Workflow*: imperative mood, `Refs #<N>`, never a GitHub closing keyword (`Fix`/`Fixes`/`Close`/`Closes`/`Resolve`/`Resolves` `#<N>`), which closes the issue the moment the commit reaches `main`, before review. The pull request body's `Closes #<N>` is the only place that should close an issue.

## Documentation

- Any new or changed **public API in `NetPace.Core`** needs `///` XML docs — they ship to NuGet consumers.
- Any changed **CLI option** needs the `README.md` `--help` snapshot and `USER_GUIDE.md` updated, and a design-doc cross-reference where applicable. Per-release "what changed" notes are GitHub-auto-generated from merged pull requests — there is no `CHANGELOG.md` to maintain.
- Any change to **`release-binaries.yml`, or other release-pipeline scope**, needs [`docs/RELEASING.md`](docs/RELEASING.md) updated: the release matrix, runner-per-RID rationale, naming convention, smoke-test contract and size-assertion contract live there, and out-of-sync release docs make adding a new RID or variant cost extra. Action-version bumps such as `actions/checkout@v4→v5` are exempt.
- **Write markdown one line per paragraph**, bullet and table row — no hard column wrapping — and let the viewer soft-wrap it; a fixed-column hard wrap reflows the whole block on a one-word edit and buries the real change in a noisy diff.

## Change-intent records

This project keeps them. The convention is [`docs/conventions/change-intent-records.md`](docs/conventions/change-intent-records.md), whose decision table `/build` runs, and records live in [`docs/change-intent-records/`](docs/change-intent-records/).

- **The `→ No CIR` rows that most often apply.** Reasoning already recorded in the issue's confirmed decisions is linked, not restated — unless the decision constrains future work (a public API, an interface contract, dependency direction), which keeps a short record linking the issue for its reasoning. A change only to documentation written for the project's readers needs none at all; command prompts, conventions, the constitution and workflows are not covered by that row, because they direct future work.
- **Length and shape.** About **150 words**: the intent in a sentence, each rejected alternative with its reason, and any constraint the code will not make obvious.

## Scratch files

`.claude/scratch/` is git-ignored. `/raise-pr` writes the pull request body to `.claude/scratch/raise-pr-body.md`; the `Write` tool creates the directory if it is missing. `/draftissue` writes the issue body to `.claude/scratch/gh-issue-body-<slug>.md`, where the slug is a short kebab-case form of the title, and `/reviewissue` writes the review comment to `.claude/scratch/reviewissue-body.md`; run `mkdir -p .claude/scratch` first if unsure it exists.

## Pull requests

The `issue-link` check (`.github/workflows/pr-issue-link.yml`) fails any pull request that GitHub says closes no open issue in this repository. It runs on every pull request and re-runs on `edited`, so fixing the body needs no new push, and it does not block the merge.

## Issue sources

- **Backlog** — `backlog <number>` and `item <number>` mean a row of the [NetPace Consolidated Backlog](https://github.com/FrankRay78/NetPace/wiki/NetPace-Consolidated-Backlog). Read it from the wiki repository: `git clone https://github.com/FrankRay78/NetPace.wiki.git` into the scratch directory, then `NetPace-Consolidated-Backlog.md`. A row gives the number, the feature, why it matters, its origin and its size; a size such as "S, after 17" names the item it depends on. Also read the prose under the row's table that names the item, the *Open questions* that name it, and the *Not doing* and *Already shipped* tables, which bound its scope.
- **Research** — a row's Origin column names a section of [Background research](https://github.com/FrankRay78/NetPace/wiki/Background-research) (`Background-research.md` in the same repository). Read that section for the evidence behind the row.
- **Linking** — Related links the backlog page and names the item number; the page has no per-row anchors.

## Issue concerns

What `/draftissue` considers when it looks for the requirements decisions a source leaves open:

- **Library consumers** — `NetPace.Core` is published to NuGet. Where the feature has a library side, its consumers are users too: say whether the capability is offered to them, not only to the CLI.
- **Output modes** — a result shown to the user must appear consistently in the default and minimal output, and respect `--unit-scale`. CSV and JSON are not formatted output: both carry plain numbers in fixed units that the unit options do not touch, and the numbers they report must agree with each other. Say which modes are in scope.
- **CLI docs** — if the feature adds or changes a CLI option, updating the README `--help` snapshot and `USER_GUIDE.md` is part of the work; make it an acceptance criterion.
- **Breaking changes** — while NetPace is below 1.0.0, a breaking change needs no approval, but the issue must state plainly what breaks and for whom (constitution §VII).

## Issue review checks

What `/reviewissue` considers alongside its generic categories, under the same test: raise a gap only where the issue leaves the point open.

- **Library first** — does the feature start in `NetPace.Core`, and does it change `ISpeedTestService`?
- **Output modes** — does it look the same across default and minimal output, and does it respect `--unit-scale`? CSV and JSON carry plain numbers in fixed units, so the unit options do not reach either; check instead that their numbers agree with each other.
- **CLI docs** — if it touches a CLI option, is updating the README `--help` snapshot and USER_GUIDE.md in scope?
- **Acceptance criteria** — do they describe outcomes rather than mechanism (constitution §IX), and are the `**Scenario:**` labels right (§VIII)?
- **AOT trimming** — is it safe for trimmed builds, with no runtime reflection?
- **Server selection and measurement** — if it touches either, does `docs/architecture/server-screening-and-measurement.md` apply?

## Audit

What `/audit-codebase` reads about this repository.

- **Scope** — everything tracked under `src/`, `examples/`, `scripts/`, `docs/`, `resources/`, `docker/`, `.github/`, `.claude/`, and the root files (`CLAUDE.md`, `CONVENTIONS.md`, `REVIEW.md`, `README.md`, `INSTALLATION.md`, `USER_GUIDE.md`, `.editorconfig`, `global.json`). Security is out of scope: CI already fails on a dependency with a known advisory (`.github/workflows/dotnet.yml`) and runs CodeQL.
- **Dependencies are in scope** — `NetPace.Core` is published to NuGet, so its dependencies become its consumers' dependencies (constitution §VI). A deprecated or outdated package, an unjustified `NetPace.Core` dependency, or a non-permissive licence is a finding. Distinguish runtime dependencies from test-only and benchmark-only ones; the licence rule binds the first.
- **Evidence** — the tools that already decide a question, run from the repository root:

  | Evidence | Command | Feeds |
  |---|---|---|
  | Build and test result, coverage | `dotnet build src` then `dotnet tool exec -y dotnet-coverage@18.0.6 -- collect "dotnet test src --no-build" -f cobertura -o .claude/scratch/audit/coverage.cobertura.xml` — the `--no-build` is allowed here because the build it follows is the one it reuses | lenses 1, 2 |
  | Deprecated packages | `dotnet list src package --deprecated` | lens 1 |
  | Outdated packages | `dotnet list src package --outdated` | lens 1 |
  | Package licences | `dotnet tool exec -y nuget-license@4.0.18 -- -i src/NetPace.sln -t` | lens 1 |
  | Unused internal and private symbols | `dotnet tool exec -y roslynator.dotnet.cli@1.0.1 -- find-symbol src/NetPace.sln --unused --visibility internal private` | lens 5 |
  | Copy-paste clones | `npx --yes jscpd@5.4.1 --format csharp --min-lines 8 --ignore "**/bin/**,**/obj/**" --reporters json --output .claude/scratch/audit/jscpd --silent src` | lens 5 |

  The test projects reference no coverage collector, so `dotnet test --collect:"XPlat Code Coverage"` runs the suite and collects nothing; the wrapper above is what produces the report. It covers every loaded assembly, third-party ones included, so lens 2 reads only the `NetPace.Core` and `netpace` packages in it. `npx` is not on the project allow-list, so a headless run will be refused it.

- **Rules to audit (lens 1)** — the principles of `docs/constitution.md`, concentrating on any amended inside the window:
  - **II (library-first)** — does `NetPace.Core` reference anything in `NetPace.Console`, or assume a console host (writing to the console, reading environment a library should be handed)? Does any provider-agnostic type (`Profile`, `SpeedUnit`, the result records) reach forward into a concrete provider under `Clients/`? Is any behaviour that belongs in the library implemented only in the console project?
  - **III (CLI)** — is each output format selected by its own switch, with exactly one selectable? Do error messages say what to do next? Does every command and option that exists appear in `--help`?
  - **Units and formatting** — is a given measurement formatted identically across default and minimal output, including under `--unit-scale`? Trace one value through both and report any path that formats it separately. CSV and JSON are deliberately not among them: both carry plain numbers in fixed units that the unit options do not touch, so check only that their numbers agree with each other.
  - **IV (cross-platform)** — hard-coded path separators, line endings, encodings, or platform APIs without a documented reason.
  - **V (quality)** — a public `NetPace.Core` member without XML documentation; a network operation that is not async or cannot be cancelled; an exception caught and discarded; a `#pragma warning disable` or `[SuppressMessage]` with no stated reason.
  - **VI (dependencies)** — from the deprecated, outdated and licence evidence: a `NetPace.Core` runtime dependency with no stated justification, any runtime dependency whose licence is not MIT, Apache 2.0 or BSD, and any deprecated package.
  - **Trim and AOT safety** — runtime reflection, `dynamic`, or serialisation that is not source-generated, anywhere on a path the published binary reaches.
  - **X (no skipped tests)** — the commit hook covers new writes; confirm with `bash .claude/hooks/no-skipped-tests.sh --check` that nothing already committed slips past it.
- **Test suite (lens 2)** — a test that wants to measure how long something took has a seam for it: `OoklaSpeedtest` takes every elapsed-time measurement from the time provider a caller supplies. Coverage is read for public `NetPace.Core` members and whole output paths with no covering test. The markers in `scripts/traceability-check.sh`, `scripts/traceability-check.tests.sh` and `docs/` are fixtures and examples of the convention, not traceability keys.
- **Documents against code (lens 3)** — `README.md`, `INSTALLATION.md` and `USER_GUIDE.md` against the real CLI: build it and compare the `--help` output with the documented snapshot, and check every documented option, default and example still exists and behaves as described. `resources/nuget/README.md` and `examples/ConsoleApp` against the public `NetPace.Core` API: does each sample still compile against the current types? `docs/architecture/*.md` against the code they describe — settings names, defaults, and the stated reasons. `docs/RELEASING.md` against `.github/workflows/release-binaries.yml` and `publish-nuget.yml`: the release matrix, naming convention, smoke-test and size-assertion contracts.
- **Harness (lens 4)** — the two workflow documents `docs/agentic-workflow.md` and `docs/agentic-workflow-NetPace.md` are part of the set the harness must agree with, and `.claude/hooks/README.md` is one of the documents that describe what a hook does.
- **Dead code (lens 5)** — the Roslynator list reports members that a source generator consumes as unused: the `JsonSerializerContext` subclasses in `NetPace.Console` are the known case.
- **Issue label** — `housekeeping`.

## Hooks, scripts and plugins

What a port of the commands must bring, beyond the commands themselves.

- **Hooks**, wired in `.claude/settings.json` — `.claude/hooks/green-gate.sh`, which denies a stale `--no-build`; `.claude/hooks/no-skipped-tests.sh`, which runs on every `git commit` and so can block any commit a command makes; and the `Bash(gh pr create:*)` `PreToolUse` hook that re-runs the build-and-test command when a pull request is created — which `/verify` weighs and does not rely on, since `/raise-pr` may not run for a long while, or at all.
- **Scripts** — `scripts/traceability-check.sh`. The chain scripts are part of the harness and named in the steps.
- **Plugins** — `pr-review-toolkit`, which supplies every wave-1 reporter and the wave-2 `code-simplifier` in `/verify`. `/build` spawns no reviewer subagents.
