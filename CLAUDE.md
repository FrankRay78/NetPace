@docs/constitution.md
@CONVENTIONS.md

# NetPace Development Guide

## Quick Reference

This guide covers **HOW** (NetPace patterns, conventions, gotchas). The constitution covers **WHAT** and **WHY** (principles). `CONVENTIONS.md`, imported above, holds what the harness commands apply that is particular to NetPace, so every session loads it with this file. A `Read` of this file does not follow the import; open `CONVENTIONS.md` itself when a command names a section of it.

## Project Overview

NetPace is a cross-platform network speed testing CLI built with .NET 10.0, using Ookla's Speedtest servers.

**Key Components:**
- `NetPace.Console` — CLI app using Spectre.Console
- `NetPace.Core` — Reusable library with `ISpeedTestService` interface (published to NuGet)

**Stack:** .NET 10.0 · C# 14 · Spectre.Console · System.CommandLine · xUnit · Nullable reference types enabled

## NetPace-Specific Patterns

### Speed Test Provider Pattern

Providers implement `ISpeedTestService` (`src/NetPace.Core/ISpeedTestService.cs`): server discovery, latency, download and upload. Each returns a result record (`LatencyTestResult`, `SpeedTestResult`) and has `CancellationToken` and `IProgress<T>` overloads. Provider code stays in `src/NetPace.Core/Clients/{Provider}/` (today only `Clients/Ookla/`).

### CLI Help Behaviour

`--help` (and `-h`, `-?`) is intercepted manually in `Program.RunAsync` before `System.CommandLine` parses arguments. This keeps help rendering under full control of `CustomHelpProvider`.

**Intentional constraint**: help is only recognised at position 0 (root help) or as the second token after a subcommand name (e.g. `netpace servers --help`). Flags placed before `--help` (e.g. `netpace --csv --help`) are silently ignored. Deliberate trade-off to keep the help-interception logic simple.

Do not add tests for the `--flag --help` pattern — it is not expected to work.

### Units and Formatting

Speeds support SI (1000-based) and IEC (1024-based) unit systems, in bits or bytes per second, auto-scaled by default (Mbps, Gbps). `--unit-scale` overrides auto-scaling; formatting must match across default and minimal output. CSV and JSON are not formatted output — both carry plain numbers in fixed units (`LatencyMilliseconds`, `…SpeedBitsPerSecond`) that the unit options do not touch, and the numbers they report must agree with each other. The two formats also carry the same fields under the same names, so a new field goes into both or neither; only the shape for something missing differs, CSV always carrying every built-in column and leaving the cell empty where JSON leaves the field out. User-defined `--custom-field` pairs are the one thing outside that rule in shape but not in name: trailing columns in CSV, one grouped `CustomFields` object in JSON, so a custom key can never displace a built-in field. Rationale: CIR [`2026-10-30-csv-and-json-carry-the-same-fields`](docs/change-intent-records/2026-10-30-csv-and-json-carry-the-same-fields.md).

### Result, Extension and Settings Patterns

- Results are immutable records of raw measurements (`SpeedTestResult { BytesProcessed, ElapsedMilliseconds, RequestsSucceeded, RequestsFailed }`). Formatting lives in extensions, not on the record:
  ```csharp
  // src/NetPace.Core/SpeedTestExtensions.cs
  public static string GetSpeedString(this SpeedTestResult result, SpeedUnit unit, SpeedUnitSystem unitSystem, SpeedScale scale = SpeedScale.Auto)
  ```
- Provider configuration is a settings record passed to the constructor, not per-call parameters: `new OoklaSpeedtest(new OoklaSpeedtestSettings(Profile.Small))`. Override single fields with `with`.

## Working with Claude Code

Paired rules — `Don't` X → `Do` Y instead:

- **Constitution rules apply as written**, and `CONVENTIONS.md` says where each one bears on the work: TDD and its config/tooling carve-out (*Test-driven development*), the skipped-test ban (*Testing*), the traceability check (*Acceptance criteria*), justified Core dependencies (*Dependencies*), the pre-1.0 stance on breaking changes (*Public API and breaking changes*), and the build, test, code-quality and documentation obligations a change owes (*Build, test and format*, *Code quality*, *Documentation*).
- **Don't leave commented-out code** → delete it; git remembers.
- **Don't delete or rename a linked-to file or heading without re-running the link check** → run `bash scripts/link-check.sh`. It names every markdown link in a tracked `*.md`, and every bare `docs/change-intent-records/<name>.md` citation in any tracked text file, whose target no longer exists — the inbound pointers a deletion leaves in files nobody opens at the time. A link's `#heading` is matched against the target file's headings by GitHub's own slug, so retitling a heading fails the links into it, and a full `https://github.com/FrankRay78/NetPace/{blob,tree,raw}/main/<path>` link is checked against `<path>` in this branch, with nothing fetched. It reads the working tree, so it works midway through a deletion. Links inside fences or backticks are not checked; bare record citations are, wherever they appear. The `link-check` CI job runs it on every PR but is not a required check, so a failure does not block the merge.
- **Don't make a throwaway script executable** → run it as `bash script.sh`, which needs no permission and does the same job. `Bash(chmod:*)` is on `permissions.ask`, so setting the executable bit stalls an interactive run until a human answers and is silently denied in a headless one — in the #265 A/B runs two reviewers each blocked well over an hour on exactly that call. `.claude/hooks/no-chmod.sh` refuses the executable-bit forms and names the alternative; to record the bit on a file you are committing, use `git add --chmod=+x <path>`.
- **Don't write an issue number outside a change-intent record** → in the codebase an issue number belongs only in `docs/change-intent-records/`, except in a regression test or acceptance criterion naming the bug it guards against (Constitution §IX's *Regression exception*, worked in `docs/conventions/testing.md`). The rule is about place, not meaning, so nobody has to weigh what a number was for. Why a rule exists stays as prose beside it, linking a record where one holds the decision. Pending work is described as the current state and tracked in an open issue, because a to-do in the codebase points at a thread that closes unnoticed. Outside the codebase, and unaffected: commit messages, branch names, PR and issue bodies (`Refs #<N>`, `Closes #<N>`), `.claude/memory/` entries (the number lets the incident be checked), and illustrative numbers in examples. Rationale: CIR [`2026-10-10-issue-numbers-live-in-records`](docs/change-intent-records/2026-10-10-issue-numbers-live-in-records.md).
- **Don't frame a decision in implementation jargon** → when putting a choice to Frank (an `AskUserQuestion`, a spec tradeoff), lead with plain-language consequences — what it costs, what it unlocks — before the mechanism.
- **Don't fold a second, unrelated mission into an in-flight branch** → ship the original branch with a documented known-issue and open a separate branch/issue for the new mission instead.
- **Don't thread project specifics through a harness command's steps** → keep a `.claude/commands/` prompt's steps repository-agnostic and put every NetPace binding (build/test/format commands, paths, test conventions, product and dependency rules, doc obligations, where the constitution states its rules, issue sources, hooks and plugins a port needs) in `CONVENTIONS.md` at the repository root, under a heading the command documents in its *Repository conventions* table, so the command reads each section as an addition to its own generic rule and a port writes that file and touches no step. The harness itself stays in the steps — the verdict protocol, `scripts/chain.sh`/`scripts/chain-next.sh` that read it, sibling command names, and failure reasons that name no project tool (`formatting failed`, not `dotnet format failed`) — because whoever edits the protocol must see what consumes it. References to the constitution, hooks and scripts left inside steps are the standing exception until the constitution is retired. `/review-pr`'s and `/review-followup`'s bindings are review criteria rather than tools and paths, so they live in `REVIEW.md` the same way (CIR [`2026-10-31-review-criteria-live-in-review-md`](docs/change-intent-records/2026-10-31-review-criteria-live-in-review-md.md)). Rationale for `CONVENTIONS.md`: CIR [`2026-10-31-commands-read-their-conventions-from-conventions-md`](docs/change-intent-records/2026-10-31-commands-read-their-conventions-from-conventions-md.md).

## Detailed References

Load these on demand for the matching topic:

- **Conventions** — `CONVENTIONS.md` — already loaded by the import above: build, test and format commands, test conventions, documentation obligations, issue sources and concerns, the hooks and plugins a port needs. Edit it, not a `.claude/commands/` file, to change how NetPace builds, tests, documents or raises work.
- **Review criteria** — `REVIEW.md` — what the PR review applies that is particular to NetPace: severities, high-risk areas, always-a-person paths, contracts. Edit it, not `.claude/commands/review-pr.md`, to change what the review holds NetPace to.
- **C# Style** — `docs/conventions/csharp-style.md` — read before writing or reviewing any .cs file.
- **Testing** — `docs/conventions/testing.md` — read before writing tests: outcome-not-mechanism assertions, what makes a good scenario, the integrity failure modes that pass while verifying nothing.
- **Change Intent Records** — `docs/conventions/change-intent-records.md` — read when deciding whether a change warrants documenting intent.
- **Release Pipeline** — `docs/RELEASING.md` — release matrix (RIDs × variants), naming convention, runner-per-RID rationale, smoke-test contract, size-assertion contract.
- **Server Screening and Measurement** — `docs/architecture/server-screening-and-measurement.md` — read before changing server selection, the latency probe, or the settings that bound either; records why choosing and measuring are separate jobs and why the ceiling on choosing must stay.
- **Ookla Download/Upload Sizing** — `docs/architecture/download-upload-size-controls.md` — how `OoklaSpeedtestSettings` shapes per-request sizing, iterations, and parallelism; what `--downloadsize`/`--uploadsize` actually cap (total-byte budget); Docker OoklaServer endpoints for local verification.

---

**Last Updated**: September 2026 · **Maintained by**: Frank Ray · **Constitution**: `docs/constitution.md`

---

## Project Memory

Project memory lives in [.claude/memory/](.claude/memory/) — git-tracked, written here (not user-level) so it travels with the codebase.

@.claude/memory/MEMORY.md
