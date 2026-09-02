---
name: Never pipe a command whose pass/fail you are about to trust
description: A pipeline's exit code belongs to the last command, so `dotnet test … | tail` reports green over a failed suite; and a backgrounded job piped through `tail` leaves an empty log while it runs.
type: feedback
---

A shell pipeline exits with the status of its **last** command. So this:

```bash
dotnet build ./src && dotnet test ./src 2>&1 | tail -35
```

exits `0` whenever `tail` succeeds — **including when the suite failed**. This has happened in practice on this harness: a run reported exit code 0 over a real failure (1 failed / 517 passed), caught only by reading the output rather than trusting the status. A test gate whose exit code is `tail`'s is not a gate.

The same construct has a second failure mode. `tail` buffers its entire input until upstream closes, so a backgrounded long-running script piped through it leaves a **0-byte output file for its whole run**. A live PID with an empty log reads as "stuck" when it is merely buffering.

**Why:** `/ship` step 1 gates on the suite run's **exit code**, not on any stored marker — that instruction is correct and it *is* the gate. A single `| tail` in how the suite gets invoked silently voids it, and nothing reports that it has. NetPace is not exposed here today: all three hooks (`green-gate.sh`, `no-skipped-tests.sh`, `traceability-gate.sh`) set `-uo pipefail`, and `/ship` as written does not pipe. This entry is **prophylactic** — the exposure is an agent hand-running a piped suite command inside a session, which is exactly how it went wrong elsewhere, and is a place no hook is watching.

**How to apply:**
- Never pipe a command whose pass/fail you are about to trust — a test run, a build, a gate script. Run it raw and read the output.
- If a pipe is genuinely unavoidable, `set -o pipefail` first, or the exit code belongs to the filter rather than the suite.
- Judge a suite by its `Passed!` / `Failed!` lines, not by exit status alone. Pairs with [[feedback_rerun_tests_before_done]] and [[feedback_dotnet_test_no_build]]: an exit code is the weakest of the three signals, and the easiest to fake by accident.
- Background invocations emit raw stdout; apply filtering at the reader (read the output file), never at the source.
