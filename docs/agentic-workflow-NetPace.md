# Agentic Workflow — the NetPace delta

Companion to [agentic-workflow.md](agentic-workflow.md), the generic, stack-portable guide. This file records **only where NetPace differs** from it, or makes a generic instruction concrete. If a practice is not listed here, NetPace follows the generic guide as written.

## Why a generic guide plus this delta

[agentic-workflow.md](agentic-workflow.md) is kept deliberately **stack-neutral** — the portable principles, with no NetPace specifics — so it stays reusable and easy to keep current. This file is where NetPace's concrete implementation, and any deviations from the guide, live.

The rule that keeps the split honest: **NetPace-specific behaviour never edits the generic guide** — it belongs here. When the guide's principles themselves change, that diff belongs in `agentic-workflow.md`; only NetPace's concrete reading of them belongs in this delta.

## Platform

- **Cross-platform, developed on Windows + WSL.** NetPace targets `win`/`linux`/`osx` (`x64`/`arm64`) and is developed on Windows with a WSL sandbox for the agent (see [wsl-claude-sandbox.md](wsl-claude-sandbox.md)).
- **Both `sh` and PowerShell script variants are kept.** NetPace retains the spec-kit `.ps1` variants alongside the `.sh` ones. Spec-kit is initialised `--script sh`; the PowerShell copies stay for Windows-native use.
- **AOT-trimmable.** Production code must stay trim/AOT-safe (reflection-heavy APIs like `Spectre.Console.Cli` were deliberately replaced). A code constraint rather than a workflow one, but it shapes what "implement" may reach for.

## CI

The generic guide's "CI on PR" step **applies fully**; NetPace realises the whole `.github/workflows/` set the guide's Appendix describes:

| Workflow | Trigger | Role |
|---|---|---|
| `dotnet.yml` — Build and Test | pull_request → main | the generic **CI-on-PR** gate: build + test every PR |
| `codeql.yml` — CodeQL | push/PR/weekly | security analysis (the supply-chain-hardening line — see the CIR) |
| `claude.yml` — Claude Code | `@claude` in an issue/PR comment (author-gated) | the generic **Agent review action** — this is **Review B** |
| `publish-nuget.yml` | tag push | publish `NetPace.Core` to NuGet |
| `release-binaries.yml` | tag push | cross-platform binary release matrix |

**Review B is live:** the `@claude` action posts on the raised PR. `/ship` requests it via `/raise-pr` and never waits on it; a human reads it at merge, and `capture-learnings` can fold it in later.

## Release pipeline

NetPace ships: `NetPace.Core` as a **NuGet package**, and cross-platform **binaries** (6 RIDs × self-contained/framework-dependent) on tag push. The generic guide has no release step; for NetPace it is first-class.

The contract — release matrix, runner-per-RID rationale, naming convention, smoke-test and size-assertion contracts — lives in [RELEASING.md](RELEASING.md); touching `release-binaries.yml` (or any release-pipeline scope) without updating it is a documented no-no (memory: `feedback_release_pipeline_doc`). Per-release "what changed" notes are GitHub-auto-generated from merged PRs — there is no `CHANGELOG.md` to maintain.

## Decision ledger: Change-Intent-Records **and** memory

Where the generic guide offers "Change-Intent-Records (or an equivalent decision ledger)", NetPace uses **both, for different jobs**:

- **Change-Intent-Records** — [`docs/change-intent-records/`](change-intent-records/), dated `YYYY-MM-DD-slug.md` files, are the human-facing record of *why* a non-obvious change was made (the AOT release shape, the profile CLI switch, the speckit-file guard, supply-chain hardening). When to write one is governed by [`docs/conventions/change-intent-records.md`](conventions/change-intent-records.md).
- **Memory** — [`.claude/memory/`](../.claude/memory/), indexed by `MEMORY.md` and loaded via `CLAUDE.md`, holds the agent-facing facts and corrections (one fact per file). The generic guide's "prefer a gate to a memory entry" rule is live: several memories exist only as the *rationale* for a gate that now enforces them (`feedback_dotnet_test_no_build` → `green-gate.sh`; the skip ban → `no-skipped-tests.sh`).

## The gates, concretely

The generic enforcement layer, as NetPace wires it. Hooks live in [`.claude/hooks/`](../.claude/hooks/), wired in `.claude/settings.json`, each with a `.tests.sh` case matrix beside it. Documented per-hook in [`.claude/hooks/README.md`](../.claude/hooks/README.md).

| Generic gate | NetPace implementation | Event |
|---|---|---|
| Stale-build guard | `green-gate.sh` — denies `dotnet test --no-build` when a `*.cs` under `src/` is newer than the built assembly | PreToolUse(Bash) |
| No skipped tests | `no-skipped-tests.sh` — blocks commits reintroducing the skip family (incl. xUnit-v3 `SkipUnless=`/`SkipWhen=`); `--check` mode for CI | PreToolUse(Bash) |
| Traceability gate | `traceability-gate.sh` — spec label ↔ test-plan scenario ↔ `// SCENARIO:` marker under `src/`, exact match; loop-guarded nudge, never a lock-out | Stop |
| Upstream-file guard | `permissions.deny` — one `Edit(path)` rule each on `.claude/skills/speckit-*/SKILL.md`, `.specify/templates/*.md`, `.specify/scripts/bash/*.sh` (an `Edit` rule covers every file-editing tool, Write included) | settings |
| PR pre-flight | `dotnet build ./src && dotnet test ./src` before `gh pr create` | PreToolUse(Bash), `if gh pr create` |
| **Formatting** | **`/ship` step 1a — `dotnet format style/whitespace ./src/NetPace.sln`, once per PR. Not a hook** (see below) | — |
| **Test-green gate** | **`/ship` step 1b — a real `dotnet build ./src && dotnet test ./src`. Not a hook.** | — |

Every hook is **fail-open with an announced override** (`NETPACE_SKIP_GREEN_GATE=1`, `NETPACE_ALLOW_SKIPS=1`, `NETPACE_SKIP_TRACEABILITY_GATE=1`). For a harness edited with itself, a false block can lock out the tools that would fix it — so uncertain paths allow, and the override announces itself on stderr.

Every hook is also a **script in `.claude/hooks/` with a `.tests.sh` case matrix beside it**, never an inline one-liner in `settings.json`. An untested gate can no-op silently — a bad path or a missing argument makes it exit without doing its job — and from the outside that is indistinguishable from a gate that passed. Note too that only **exit 2** blocks a `PreToolUse` hook; any other non-zero exit is reported and ignored, so a gate that means to block must say so explicitly.

**Two generic gates do not apply here.** NetPace has **no stack-guard** — there is no external service stack to orchestrate — and **no UI-automation denylist**: it is a console CLI, not a browser UI, so a denylist has nothing to guard. NetPace's console output *is* verified — see below — just not by a browser-automation framework.

### Formatting

Formatting runs **once per PR**, as `/ship` step 1a — never on commit. This is the generic guide's *Formatting is not verification — do it at ship cadence* section, made concrete:

```bash
dotnet format style ./src/NetPace.sln && dotnet format whitespace ./src/NetPace.sln
```

The explicit solution argument is **required, not decorative**: `dotnet format` looks for a project or solution in the *current directory only*, and NetPace's lives under `src/`, not the repo root. Omitting it fails outright.

**Why not per-commit.** Measured on this solution (84 `.cs` files, ~9,900 LOC): **21.4s** for a single staged file, **29.5s** for a seven-file set. The cost is MSBuild **workspace load**, not file count — so staging fewer files makes it no cheaper, and every commit would pay the full ~20–30s. Against that, a release cycle's worth of drift is a handful of import reorderings and a couple of hundred trailing spaces on blank lines: nothing a reviewer would catch. The guide's "tens of seconds" figure holds even at this size, which is why the "our solution is small enough to absorb it" argument does not survive.

**Line endings.** `.gitattributes` pins `*.cs text eol=lf`, agreeing with `.editorconfig`'s `end_of_line = lf` and the LF the index already stores. Without it, a Windows checkout with `core.autocrlf=true` gets a CRLF working tree, and `dotnet format whitespace` then rewrites every file it touches — no committed diff, since the rewrite normalises back on commit, but thousands of phantom findings drowning the real ones. A Windows working tree created *before* that attribute needs a one-time refresh to pick it up (re-clone, or `git rm --cached -r . && git reset --hard` on a clean tree); fresh clones and Linux checkouts are unaffected.

## `/ship`

NetPace's `/ship` follows the generic *ship gate* section as written:

- **Formats first.** Step 1a runs `dotnet format style/whitespace ./src/NetPace.sln` and commits any result on its own, before the suite — so formatting is verified by the gate rather than landing after it, and step 3's clean-tree invariant survives. The explicit solution argument is load-bearing (see above).
- **Always runs the suite.** Step 1b is `dotnet build ./src && dotnet test ./src` — no docs-only skip. The suite is fast (no external stack), and the `gh pr create` pre-flight hook would re-run it anyway, so a skip would save nothing.
- **Review B posts.** Because `claude.yml` is wired, the async `@claude` review the generic flow describes actually appears on the PR. `/ship` still never waits on it.

## Permissions and unattended runs

`ask` rules sit **above** every permission mode: `bypassPermissions` does not clear them, and nothing does except removing the rule. Subagents inherit the parent's mode — "if the parent uses `bypassPermissions` or `acceptEdits`, this takes precedence and can't be overridden" — so a reviewer subagent that stops on a permission has hit an `ask` rule, never the subagent boundary.

An `ask`-matched call **prompts** in an interactive session but is **silently denied** in a headless `claude -p` one, which cannot draw a prompt: the worker loses that capability and carries on degraded. A green unattended run is therefore not evidence that its `ask` rules were harmless.

That asymmetry makes headless a permission oracle — run a workflow under `claude -p --dangerously-skip-permissions` and anything that would have prompted interactively comes back denied instead, with no human in the loop to mask it. Not CI-gateable: it needs the `claude` binary and an authenticated session.

`Bash(rm:*)` and `Bash(rmdir:*)` came off the list for this reason ([CIR](change-intent-records/2026-09-04-rm-off-the-ask-list.md)).

## Test-green gate & categories

- The completion gate is the real suite run inside `/ship` (above), backed belt-and-braces by the `gh pr create` pre-flight hook — both are `dotnet build ./src && dotnet test ./src`. There is no ledger/Stop-hook proxy (the shape the generic guide's *Where the completion gate belongs* section warns against).
- **Fast/slow split.** Real-network integration tests live in a **separate test category**, excluded from the default run, so the inner loop stays seconds-fast; the whole (default) suite is the completion gate.
- **Console output is verified by snapshot.** `NetPace.Console.Tests` uses `Spectre.Console.Testing` with `Expectations/*.verified.txt` snapshots — that is how a CLI covers the generic guide's *verify* duty for rendered output. Check the `*.verified.txt` before reporting an output mode as untested (memory: `feedback_console_output_snapshot_coverage`).

## Spec-kit

Pinned at **0.12.10**, initialised `--script sh`. A `--force` re-init resets every stock skill's `disable-model-invocation` flag to `false` — the flip must be re-applied after any upgrade, and the upstream-file guard above exists to stop that regression recurring (CIR: `2026-07-10-guard-speckit-files`; memory: `speckit_upgrade_procedure`). NetPace's custom `speckit.*` commands (`draftissue`, `reviewissue`, `confirmissue`, `testplan`, `testchecklist`) are authored here, not stock — an upgrade does not touch them; the guarded files are the hyphenated `speckit-*` skills.

## Token / context tooling

The generic guide's [*Token / context management plugins*](agentic-workflow.md#token--context-management-plugins) section names `read-once`, `context-mode` and `rtk` in three bullets; [wsl-claude-sandbox.md](wsl-claude-sandbox.md) step 7 has the detail — what each does, the install commands, and the marketplace-over-SSH gotcha. Both stay deliberately portable: neither says whether any of them is actually present on the box in front of you.

Two of the three are wired into NetPace's config: `rtk` has `Bash(rtk …)` allow-entries in `.claude/settings.json` and a prefix-strip in `green-gate.sh`'s `strip_cmd_prefixes()` — a gate written on the assumption that rtk may be in play — while `context-mode` has a block of `mcp__plugin_context-mode_context-mode__*` allow-entries and an `enabledPlugins` entry. `read-once` is described in the guides but referenced by no config at all. **Nothing verifies that any of it is installed**, and a tool that silently isn't there costs exactly what one that is there costs; you just stop getting the benefit.

[`scripts/plugin-report.sh`](../scripts/plugin-report.sh) is that missing check — a manually-run, read-only report (installs nothing, edits nothing, starts nothing) covering four sections: `TOOLING` (expected tool → declared / installed / enabled / reachable, `pr-review-toolkit` included), `CONFIG` (unresolvable hook and statusLine paths, plus dangling or duplicated allow-entries), `HOOKS` (what is registered and what each costs per invocation), and `PERFORMANCE` (live savings counters, where a tool exposes them).

```bash
bash scripts/plugin-report.sh
```

It is not a gate: no `--check` mode, no exit-code contract, and it is wired into no hook, no CI job and no `/ship` step. The intended use is running it on two boxes and diffing — which is why `TOOLING`, `CONFIG` and `HOOKS` carry no timestamps, no absolute paths and no raw millisecond figures. `PERFORMANCE` is explicitly exempt (live counters move every session), so cross-box diffs use the other three sections. A probe it cannot reach a verdict on reports `unknown` rather than `no` — a report whose product is a truthful yes/no must not launder a failed lookup into an answer.

[`/install-harness-tooling`](../.claude/commands/install-harness-tooling.md) is the other half of the pair: it installs what the report says is missing. It reads each upstream `install.sh` before recommending it and **prints** the command for a human to run rather than executing it, so the `Bash(curl:*)` / `Bash(wget:*)` denies stay intact; and because two of the installers write a `PreToolUse` hook into settings themselves, it stops at each of those points and shows the diff — the generic guide's rule 4 (*a human reviews each hook before it lands*) applied to installers that would otherwise wire hooks in silently.

Install status is deliberately **not** recorded in this or any other doc: it is a per-box, manual job, so a written answer goes stale on the next clone. Run the report — that is the live answer.

## Related

- [../.specify/memory/constitution.md](../.specify/memory/constitution.md) — governance; supersedes this file and the generic guide alike.
- [RELEASING.md](RELEASING.md) — the release matrix and its contracts.
- [conventions/change-intent-records.md](conventions/change-intent-records.md) — when a change warrants a CIR; [conventions/csharp-style.md](conventions/csharp-style.md) — C# style.
- [../.claude/hooks/README.md](../.claude/hooks/README.md) — per-hook documentation.
