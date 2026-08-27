---
name: Re-run tests before declaring done
description: After any post-implementation edit, re-run `dotnet test ./src` before reporting work as complete — don't extrapolate from an earlier green run.
type: feedback
---

After any post-implementation edit (review fixes, refactors, late-stage tweaks), re-run `dotnet test ./src` before reporting the work as complete. Don't extrapolate from an earlier green run — compile-clean is not test-clean, and the earlier count was taken against earlier code.

**Where the hard guarantee lives:** the "suite is green before a PR" check is a **real whole-suite run inside `/ship`** ([.claude/commands/ship.md](../commands/ship.md)), which gates the review/PR on `dotnet build ./src && dotnet test ./src`. During implementation, keeping the suite green is a **soft** standard — run it at your own discretion; the hard gate is `/ship`.

**Why:** Relaying an earlier "N/N passed" after subsequent edits is how a regression reaches review unseen. The fix is cheap — one more run — and NetPace's suite is fast and needs no external stack (unlike a services-backed project, there is nothing to "bring up" first, so "deferred — needs a running stack" is never the right answer here).

**How to apply:** When ending a session, capping a feature, or reporting after fixes, run `dotnet test ./src` one more time and state the actual fresh count, not the most recent earlier count.
- Pairs with [[feedback_dotnet_test_no_build]]: when you re-run, rebuild first — a `--no-build` re-run can report the stale DLL and undo the point of re-running.
