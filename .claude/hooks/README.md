# Claude Code hooks

Repo-committed hooks so they travel to every checkout. Registered in [`.claude/settings.json`](../settings.json) once a human has reviewed them (harness-safety: a hook lands in `settings.json` only after review, because a bad hook can lock out the tools that would fix it).

**The failure rule, and its one exception.** By default a hook **fails open**: any missing tool, unparseable input, or internal error is a no-op, never a false block, because a false block inside an interactive turn can lock out the tools that would fix it. The exception is a hook gating an action where silence would read as a pass — there the hook **fails closed for that action only**, because a gate that cannot run must not be mistaken for a gate that ran and found nothing. `no-skipped-tests.sh` is the case in this directory: it waves a call that cannot be a commit through fail-open, and fails closed once the raw payload (any field, description and cwd included) contains both `git` and `commit`. `scripts/traceability-check.sh` is the same rule applied to a merge gate rather than a turn. The only actions any hook takes are the narrow, high-confidence cases, each with an announced environment-variable override. Every gate is provable without a running app by a committed `*.tests.sh` matrix that drives it on synthetic hook JSON — against a throwaway `CLAUDE_PROJECT_DIR` for the gates that read the filesystem.

**Every `*.tests.sh` in the repo runs in CI.** [`shell-tests.yml`](../../.github/workflows/shell-tests.yml) finds them with `git ls-files '*.tests.sh'` instead of listing them, one matrix entry per script, so a new matrix is gated the moment it is committed and each failure names its own script. Still run the matrix yourself after any edit to a gate — CI is the backstop, not the first you hear of a break.

NetPace keeps both production and `*.Tests` projects under `src/`, so the filesystem-reading gates scan `src/` (there is no top-level `tests/`).

## `no-skipped-tests.sh` — skip-family ban (`CONVENTIONS.md` → *Testing*)

PreToolUse(Bash) gate that blocks a `git commit` while any banned skipped-test construct exists under `src/`: `Skip.If/IfNot/Always/Unless`, `Assert.Skip`, `[Fact/Theory(Skip=…)]`, `[SkippableFact/Theory]`, `SkipException`, and xUnit v3's `SkipUnless=`/`SkipWhen=`. Also exposes `--check` for CI/manual scans (the audit ignores the override, so a leaked env var can never silence it). Fails **closed** once the raw payload (any field, description and cwd included) contains both `git` and `commit` — a gate that can't scan must not read as clean — and waves every other Bash call straight through. Override: `NETPACE_ALLOW_SKIPS=1` (announced on stderr). Promotes the no-skipped-tests rule (`CONVENTIONS.md` → *Testing*) into a gate rather than a rule the agent must remember.

**What the registration reaches.** `.claude/settings.json` registers it with no `if:` filter, as it does `green-gate.sh`, so the script is handed every Bash call and its own classifier — `git` followed anywhere by `commit` — decides what to gate. That is what makes the form of the command irrelevant: `cd somewhere && git commit -m x`, `git -C <path> commit`, `git -c k=v commit`, `env X=1 git commit`, `bash -c 'git commit …'` are all scanned alike. It was previously registered with `if: Bash(git commit:*)`, which matches on the command's leading words, so any git global option or wrapper before the subcommand slipped past it entirely and such a commit was never scanned. The price of removing the filter is that a read-only command merely mentioning the words — `grep -r "git commit" docs/` — is gated too; that over-block is accepted. On the scan path it fires only when `src/` already holds a banned construct, which includes read-only review and audit sessions (e.g. `git rev-parse <ref>^{commit}`) that meet a dirty `src/` while doing their job and cannot fix it; on the fail-closed paths (no jq, no grep) it fires on any Bash call whose raw payload contains both words, clean tree or not, loudly, rather than only a commit; the no-`src/` guard needs the command itself to match. Every other Bash call exits at the pre-filter, whose own test is a shell builtin.

**What no hook can reach.** An *indirect* commit is invisible to a gate that reads the command text: a git alias (`git ci`), or a script that commits internally. The backstop for those, and for a commit made outside a Claude session at all — by hand, or in the GitHub UI — is CI: the `build` job in [`dotnet.yml`](../../.github/workflows/dotnet.yml) runs `--check` as its first step after checkout, and `build` is a required check, so a pull request whose tree holds a skip cannot merge however the commit was made.

```bash
.claude/hooks/no-skipped-tests.sh --check          # scan src/, exit 1 on any banned construct
.claude/hooks/no-skipped-tests.tests.sh            # synthetic-sandbox matrix — non-zero on failure
```

## The `no-chmod` hook is gaffer's

The executable-bit `chmod` refusal — the gate that turns `Bash(chmod:*)` on `permissions.ask` from a stall into a redirection to `bash script.sh` — ships with the [gaffer](https://github.com/FrankRay78/gaffer-claude) plugin, registered by its `hooks/hooks.json` on every Bash call, and runs here because `.claude/settings.json` enables the plugin. Its scope, known limits, fail-open rule and `GAFFER_ALLOW_CHMOD=1` override are documented there, and its matrix runs in gaffer's CI. What this repository keeps is the backstop: `Bash(chmod:*)` stays on `permissions.ask` for the forms the hook declines to decide, because a plugin cannot set permission rules.

## `green-gate.sh` — `dotnet test --no-build` staleness guard

PreToolUse(Bash) gate that denies `dotnet test --no-build` when it would report misleading results: no test assembly has been built yet, or a `*.cs` under `src/` is newer than the newest built `*.Tests.dll`. Either way `--no-build` would run a stale or absent assembly. Promotes [`feedback_trusting_a_test_run`](../memory/feedback_trusting_a_test_run.md).

**Command detection:** the `dotnet test` matcher fires only when it is the actual command — after stripping benign `cd …&&` / `export …&&` / env-assignment / `rtk` prefixes — not when the string merely appears inside a commit message, an `echo`, or quoted data. `--no-build` must be a flag of the `dotnet test` invocation itself, not a substring in a chained command. The staleness scan ignores generated `obj/`/`bin/` `.cs` so an unrelated restore or build can't falsely fire.

**Fail open.** Any missing tool, unparseable input, or non-`dotnet test` command is a no-op. It is wired without an `if`, so the script's own prefix-stripping does the filtering and a prefix-wrapped `cd repo && dotnet test --no-build` is still caught. Override: `NETPACE_SKIP_GREEN_GATE=1` (announced on stderr).

```bash
.claude/hooks/green-gate.tests.sh                  # synthetic-JSON matrix — non-zero on failure
```
