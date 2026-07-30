# Claude Code hooks

Repo-committed hooks so they travel to every checkout. Registered in [`.claude/settings.json`](../settings.json) once a human has reviewed them (harness-safety: a hook lands in `settings.json` only after review, because a bad hook can lock out the tools that would fix it).

They **fail open**: any missing tool, unparseable input, or internal error is a no-op, never a false block. The only actions any hook takes are the narrow, high-confidence cases, each with an announced environment-variable override. Every gate is provable without a running app by a committed `*.tests.sh` matrix that drives it against a throwaway `CLAUDE_PROJECT_DIR`; run the matrix after any edit to the gate.

NetPace keeps both production and `*.Tests` projects under `src/`, so the filesystem-reading gates scan `src/` (there is no top-level `tests/`).

## `no-skipped-tests.sh` — skip-family ban (Constitution §X)

PreToolUse(Bash) gate that blocks a `git commit` while any banned skipped-test construct exists under `src/`: `Skip.If/IfNot/Always/Unless`, `Assert.Skip`, `[Fact/Theory(Skip=…)]`, `[SkippableFact/Theory]`, `SkipException`, and xUnit v3's `SkipUnless=`/`SkipWhen=`. Also exposes `--check` for CI/manual scans (the audit ignores the override, so a leaked env var can never silence it). Fails **closed** once a command is classified as a commit — a gate that can't scan must not read as clean — and waves every non-commit Bash call straight through. Override: `NETPACE_ALLOW_SKIPS=1` (announced on stderr). Promotes Constitution §X into a gate rather than a rule the agent must remember.

```bash
.claude/hooks/no-skipped-tests.sh --check          # scan src/, exit 1 on any banned construct
.claude/hooks/no-skipped-tests.tests.sh            # synthetic-sandbox matrix — non-zero on failure
```

## `green-gate.sh` — `dotnet test --no-build` staleness guard

PreToolUse(Bash) gate that denies `dotnet test --no-build` when it would report misleading results: no test assembly has been built yet, or a `*.cs` under `src/` is newer than the newest built `*.Tests.dll`. Either way `--no-build` would run a stale or absent assembly. Promotes [`feedback_dotnet_test_no_build`](../memory/feedback_dotnet_test_no_build.md).

**Command detection:** the `dotnet test` matcher fires only when it is the actual command — after stripping benign `cd …&&` / `export …&&` / env-assignment / `rtk` prefixes — not when the string merely appears inside a commit message, an `echo`, or quoted data. `--no-build` must be a flag of the `dotnet test` invocation itself, not a substring in a chained command. The staleness scan ignores generated `obj/`/`bin/` `.cs` so an unrelated restore or build can't falsely fire.

**Fail open.** Any missing tool, unparseable input, or non-`dotnet test` command is a no-op. It is wired without an `if`, so the script's own prefix-stripping does the filtering and a prefix-wrapped `cd repo && dotnet test --no-build` is still caught. Override: `NETPACE_SKIP_GREEN_GATE=1` (announced on stderr).

```bash
.claude/hooks/green-gate.tests.sh                  # synthetic-JSON matrix — non-zero on failure
```

## `traceability-gate.sh` — AC↔marker traceability gate (Constitution §VIII)

Stop hook enforcing the two exact-match edges of the §VIII traceability chain — spec.md `**Scenario: X**` label → test-plan.md `#### Scenario: X` header → test `// SCENARIO: X` marker under `src/`. It checks the edges a machine can decide; the judgment checks (fuzzy match, mock self-satisfaction, trivially-passing bodies, undocumented-test detection) stay in `/speckit.testchecklist`.

| Edge | Rule | Direction |
|------|------|-----------|
| **spec ⟷ test-plan** | every `**Scenario: X**` label has exactly one matching `#### Scenario: X` header, and vice versa — a repeated name on either side is flagged (the label is a unique §VIII key) | bijection |
| **test-plan → code** | every `#### Scenario: X` header has ≥1 matching `// SCENARIO: X` marker under `src/` (generated `obj/`/`bin/` copies excluded) | coverage only |

**Scope — active specs only.** The gate reads `specs/*/spec.md`. Merged features have their specs deleted (leaving only drifted markers behind), so a repo with no in-flight feature — the steady state — is a clean no-op. The test-plan→code edge is deliberately **directional**: a marker with no plan scenario is not flagged, because `src/` accumulates markers from already-merged features whose specs are gone. "Undocumented test" is a judgment left to `/speckit.testchecklist`.

**Staged fail-open:** a spec still being authored never blocks. No `test-plan.md`, or a plan with no scenarios yet → no-op. Plan scenarios present but zero have a marker → pre-implementation, so the coverage edge is skipped (only the spec⟷plan edge runs). Once any scenario has a marker, all must. It is loop-guarded (`stop_hook_active`), so it nudges at most once per turn and can never hard-lock. Override: `NETPACE_SKIP_TRACEABILITY_GATE=1` (announced on stderr).

```bash
.claude/hooks/traceability-gate.sh --check [specdir]   # report + exit 1 on any mismatch (CI/manual)
.claude/hooks/traceability-gate.tests.sh               # synthetic-fixture matrix — non-zero on failure
```
