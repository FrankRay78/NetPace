---
name: dotnet test --no-build runs the stale test DLL
description: After editing test-project sources, `dotnet test --no-build` runs the stale DLL — always rebuild first. green-gate.sh now enforces this.
type: feedback
---

`dotnet test --no-build` skips compilation across the **entire solution**, including test projects. After editing any `.cs` under `src/` (production or a `*.Tests` project), run `dotnet build` first (or drop the `--no-build` flag) before trusting the run output — otherwise you are asserting against the DLL compiled *before* your edit.

**Why:** `--no-build` is a genuine footgun. Edit a source, run `dotnet test --no-build`, and you get the OLD compiled behaviour — a failure (or a pass) that no longer matches the code on disk — followed by a confused investigation whose real fix is just `dotnet build` and re-run. Compile-current-on-disk is not implied by "I just edited the source".

**How to apply:**
- After any source edit, **always** rebuild before re-running tests.
- Only use `--no-build` to re-run an already-current build — i.e. no source has changed since the last build.
- This is now gate-enforced: [`green-gate.sh`](../hooks/green-gate.sh) denies a `dotnet test --no-build` when no test assembly is built yet, or when a `*.cs` under `src/` is newer than the newest `*.Tests.dll`. Emergency override: `NETPACE_SKIP_GREEN_GATE=1` (announced on stderr).
- Pairs with [[feedback_rerun_tests_before_done]]: compile-clean is not test-clean, and DLL-current-on-disk is not implied by the most recent build either.
