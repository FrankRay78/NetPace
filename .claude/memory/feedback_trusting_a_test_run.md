---
name: Trust only a fresh, rebuilt, unpiped test run
description: Before reporting green, rebuild, re-run `dotnet test ./src` after the last edit, run it unpiped, and read the Passed!/Failed! lines.
type: feedback
---

Report a test result only from a run that (1) comes after the last edit, (2) was built from current source, and (3) wasn't piped through a filter.

**Why:** An earlier "N/N passed" doesn't cover later edits — that is how a regression reaches review unseen. `dotnet test --no-build` skips compilation across the whole solution and runs the DLL built *before* your edit. A pipeline exits with its last command's status, so `dotnet test … | tail` reported exit 0 over a real failure (1 failed / 517 passed) in a sibling repo using this harness; `tail` also buffers, so a backgrounded run piped through it leaves an empty log that looks stuck. NetPace's suite is fast and needs no running stack, so "deferred" is never the right answer.

**How to apply:**
- After any post-implementation edit, run `dotnet build ./src && dotnet test ./src` again and report the fresh count. The hard gate is `/verify` step 1b; during implementation, green is a soft standard.
- Use `--no-build` only when no source changed since the last build. [`green-gate.sh`](../hooks/green-gate.sh) denies a stale or absent-assembly `--no-build` (override `NETPACE_SKIP_GREEN_GATE=1`).
- Never pipe a command whose pass/fail you're about to trust. If a pipe is unavoidable, `set -o pipefail` first. Filter when reading the output file, not at the source.
- Judge the run by its `Passed!`/`Failed!` lines, not by exit code alone.
