# Claude Code hooks

Repo-committed hooks so they travel to every checkout. Registered in [`.claude/settings.json`](../settings.json) once a human has reviewed them (harness-safety: a hook lands in `settings.json` only after review, because a bad hook can lock out the tools that would fix it).

They **fail open**: any missing tool, unparseable input, or internal error is a no-op, never a false block. The only actions any hook takes are the narrow, high-confidence cases, each with an announced environment-variable override. Every gate is provable without a running app by a committed `*.tests.sh` matrix that drives it on synthetic hook JSON — against a throwaway `CLAUDE_PROJECT_DIR` for the gates that read the filesystem.

**Every `*.tests.sh` in the repo runs in CI.** [`shell-tests.yml`](../../.github/workflows/shell-tests.yml) finds them with `git ls-files '*.tests.sh'` instead of listing them, one matrix entry per script, so a new matrix is gated the moment it is committed and each failure names its own script. Still run the matrix yourself after any edit to a gate — CI is the backstop, not the first you hear of a break.

NetPace keeps both production and `*.Tests` projects under `src/`, so the filesystem-reading gates scan `src/` (there is no top-level `tests/`).

## `no-skipped-tests.sh` — skip-family ban (Constitution §X)

PreToolUse(Bash) gate that blocks a `git commit` while any banned skipped-test construct exists under `src/`: `Skip.If/IfNot/Always/Unless`, `Assert.Skip`, `[Fact/Theory(Skip=…)]`, `[SkippableFact/Theory]`, `SkipException`, and xUnit v3's `SkipUnless=`/`SkipWhen=`. Also exposes `--check` for CI/manual scans (the audit ignores the override, so a leaked env var can never silence it). Fails **closed** once a command is classified as a commit — a gate that can't scan must not read as clean — and waves every non-commit Bash call straight through. Override: `NETPACE_ALLOW_SKIPS=1` (announced on stderr). Promotes Constitution §X into a gate rather than a rule the agent must remember.

```bash
.claude/hooks/no-skipped-tests.sh --check          # scan src/, exit 1 on any banned construct
.claude/hooks/no-skipped-tests.tests.sh            # synthetic-sandbox matrix — non-zero on failure
```

## `no-chmod.sh` — executable-bit `chmod` refusal (issue #293)

PreToolUse(Bash) gate that refuses a `chmod` which would set the executable bit, with a message naming `bash script.sh` as the alternative. The gated call is how an agent tries to run a throwaway script to verify its own work, and `Bash(chmod:*)` on `permissions.ask` makes it stall an interactive run and vanish silently in a headless one — in the #265 A/B comparisons two reviewers each blocked well over an hour on it. A one-line instruction to use `bash script.sh` removed the problem entirely in the follow-up run; this gate makes that instruction a gate rather than a rule the agent must remember.

**Scope — executable-bit forms only.** `+x`, `u+x`, `u=rx`, `+rwx`, `755`, `4755`, a bare `7`. Forms that cannot set the bit are waved through and stay governed by the `ask` rule: `644`, `1644`, `-x`, `a-x`, `u+w`, `a=`, a `g+u` copy form, `--reference=`. `git add --chmod=+x <path>` is not a `chmod` call and is never refused — it is the way to record the bit on a committed file.

**`Bash(chmod:*)` stays on `permissions.ask`.** The hook is the actionable layer, not a replacement: the `ask` rule is the fail-open backstop for the cases this gate declines to decide (an undecidable mode, a `chmod` reached through another command's arguments) and for any run where the hook does not fire at all. Keeping both is what stops a gate miss being a free pass. It also over-asks in one visible way — the harness's own permission matcher reads a `chmod` line inside a heredoc body or a multi-line commit message as an invocation, so writing about this rule can still prompt; put the message in a file and use `git commit -F <file>`. (The hook itself does *not* over-ask on those: a heredoc body and a quoted multi-line string are both excluded from its scan.)

**Command detection:** `chmod` must be the head of a command *segment* — separators are `&&`, `||`, `;`, `|`, `&` and a newline — after stripping env-assignments, wrappers that run their argument unchanged (`sudo`, `command`, `env`, `exec`, `time`, `nohup`, `rtk`, a leading `\`, an absolute `/usr/bin/chmod` path) and the keywords that head a compound body (`then`, `else`, `do`, `{`, `(`, `!`), so `for f in *.sh; do chmod +x "$f"; done` is caught. A whole-command substring match would fire on any mention; segment matching catches the chained forms that actually occur and leaves an `echo`, a commit message or a grep pattern alone. Splitting is quote- and heredoc-aware in one linear pass: a separator inside a quoted span — **including a newline**, so a multi-line commit message about this rule is safe — is not a boundary, and a heredoc body is dropped outright because it is data rather than commands. Getting either wrong is not theoretical: the gate refused its own first live call on a `&&` inside a quoted JSON payload, and a quoted multi-line message refused itself until the newline was handled too.

**Known limits, declined rather than overlooked:** a `chmod` reached through another command's arguments is not seen — `find … -exec chmod +x {} \;`, `… | xargs chmod +x`, `bash -c "chmod +x f"` — as is one hidden by an unbalanced quote or a command substitution. All fall through to the `ask` rule, and each is pinned as an explicit allow-case in the matrix so the gap is recorded rather than rediscovered.

**Fail open**, like `green-gate.sh`: a missing tool, unparseable input, or an undecidable mode is a no-op, with the `ask` rule behind it — and a missing tool says so on stderr, so an unenforced gate is never silently in effect. The single exception is a decision already made and then unwritable, which falls back to the blocking exit code rather than dropping a refusal it had already decided on. Override: `NETPACE_ALLOW_CHMOD=1` (announced on stderr; `--check` ignores it). Registered **without** an `if` clause — the segment scan does all the filtering and the clause is not relied upon — and invoked via `bash` so it needs no working-tree executable bit. Both halves of the wiring (no `if` clause; `Bash(chmod:*)` still on `ask`) are asserted by the matrix, because editing either away silently un-gates the rule.

```bash
bash .claude/hooks/no-chmod.sh --check 'chmod +x t.sh'   # exit 1 + the refusal message; 0 if allowed
bash .claude/hooks/no-chmod.tests.sh                     # synthetic-JSON matrix — non-zero on failure
```

## `green-gate.sh` — `dotnet test --no-build` staleness guard

PreToolUse(Bash) gate that denies `dotnet test --no-build` when it would report misleading results: no test assembly has been built yet, or a `*.cs` under `src/` is newer than the newest built `*.Tests.dll`. Either way `--no-build` would run a stale or absent assembly. Promotes [`feedback_trusting_a_test_run`](../memory/feedback_trusting_a_test_run.md).

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
