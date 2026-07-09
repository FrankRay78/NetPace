# NetPace sidecar for `/diagnose`

This sidecar lists NetPace-specific feedback-loop builders, conventions, and gotchas referenced by [SKILL.md](SKILL.md). Read it before Phase 1.

**Repo shape:** the solution and all projects live under `src/` (`src/NetPace.sln`), not the repo root. There is no `.sln` in CWD, so every `dotnet` command must name a path — `dotnet test src`, `dotnet build src`, `dotnet test src/NetPace.Core.Tests`. A bare `dotnet test` from the repo root finds nothing.

---

## Phase 1 — Feedback-loop builders available in NetPace

Concrete substitutes for the generic builders in SKILL.md Phase 1, roughly fastest-first. Prefer the hermetic in-process loops (1–4) — they are deterministic and need no network or Docker. Reach for the live Docker server (5–6) only when a stubbed loop genuinely cannot reproduce the symptom.

1. **Single test by filter** — sharpest, fastest signal. xUnit filter, no documented alias but the standard forms work:
   ```bash
   dotnet test src --filter "FullyQualifiedName~OoklaSpeedtestTests.GetServersAsync_ShouldReturnSingleServer_WhenResponseHasOneServer"
   dotnet test src --filter "FullyQualifiedName~OoklaSpeedtestTests"          # whole class
   dotnet test src --filter "DisplayName~CSV"                                  # substring
   ```
   Never add `--no-build` / `--no-restore` after editing a test or source file — you will run a stale DLL and chase a ghost. CI uses `--no-build` only because it builds in a prior step.

2. **Whole suite, clean** — the one-shot "is it all still green": `dotnet build src && dotnet test src`. Core.Tests names tests `MethodName_Scenario_ExpectedResult`; Console.Tests uses `Should_...`.

3. **Verify snapshot loop (output-format bugs)** — the correct loop for any normal/CSV/JSON/file/quiet/minimal/servers formatting bug. Tests in `src/NetPace.Console.Tests/` drive the real CLI in-process through `CommandLineTestHost.RunAsync([...])` (which injects a `TestConsole().Width(int.MaxValue)` and calls `Program.RunAsync`) with DI stubs (`ISpeedTestService`→`SpeedTestStub`, `IClock`→`ClockStub`/`IncrementingClockStub`, `IWaiter`→`NoDelayStub`) — so it is fully deterministic. On mismatch, Verify writes a `*.received.txt` next to the `*.verified.txt` and fails; snapshots live in `src/NetPace.Console.Tests/Expectations/*.verified.txt` (~120 files, `[ModuleInitializer]` in `VerifyConfiguration.cs` points there). Facet partial classes: `NetPaceConsoleTests.CSV.cs`, `.Json.cs`, `.File.cs`, `.Servers.cs`, etc. **Review the `.received.txt` diff before accepting** — an accepted snapshot is a coverage assertion (see [feedback_console_output_snapshot_coverage.md](../../memory/feedback_console_output_snapshot_coverage.md)); never blind-overwrite `.verified.txt`.

4. **Replay a captured payload through MockHttp (Core bugs)** — for speed-calculation, unit-conversion, server-list-parsing, or sizing bugs. `NetPace.Core.Tests` fakes HTTP with `RichardSzalay.MockHttp` (`MockHttpMessageHandler`) and replays real JPEG fixtures from `src/NetPace.Core.Tests/Payloads/random{N}x{N}.jpg` (N ∈ {1500, 2000, 3000, 3500, 4000}, e.g. `random1500x1500.jpg`) — a hermetic stand-in for the Docker server. Turn a captured server response (server-list JSON, a payload size) into a MockHttp fixture and assert on the parsed/computed result.

5. **Run the real CLI against the local Docker OoklaServer** — for end-to-end or transport-shaped bugs the stubs can't reach. A self-contained real OoklaServer daemon lives in `docker/ooklaserver/`:
   ```bash
   ./docker/ooklaserver/start.sh    # build, start, poll until ready (~30s), prints the NetPace command
   dotnet run --project src/NetPace.Console -- --server http://localhost:18080/speedtest/upload.php --csv
   ./docker/ooklaserver/stop.sh     # tear down (docker compose down)
   ```
   The Console `AssemblyName` is `NetPace`. Output modes usable as a diff signal: `--csv`, `--csv-delimiter`, `--csv-header-units`, `--json`, `--json-pretty`.

6. **Direct curl probes against the Docker server** — isolate client-side vs server-side fault:
   ```bash
   curl -sS -o /dev/null -w '%{size_download} bytes in %{time_total}s\n' http://localhost:18080/speedtest/random4000x4000.jpg
   head -c 1048576 /dev/urandom | curl -sS -o /dev/null -w '%{http_code}\n' --data-binary @- http://localhost:18080/speedtest/upload.php
   curl -fsS http://localhost:18080/speedtest/latency.txt   # health probe; body begins "test=test"
   ```

7. **Differential loop against a known-good commit** — `git worktree add ../NetPace-baseline <known-good-sha>`, run the same test (or the same `dotnet run … --json`) against both worktrees, diff. The hermetic stubs make the two runs directly comparable.

### Docker OoklaServer endpoint catalogue (for curl / `--server` targeting)

Host port **18080** → container 8080 (TCP + UDP), chosen to dodge the common 8080 conflict.

| Endpoint | Purpose |
|---|---|
| `http://localhost:18080/speedtest/random{N}x{N}.jpg` | Download payload |
| `http://localhost:18080/speedtest/upload.php` | Upload sink |
| `http://localhost:18080/speedtest/latency.txt` | Health/latency probe; body begins `test=test` |

OoklaServer only serves `random{N}x{N}.jpg` for a **fixed set of N** — the in-repo fixtures confirm `1500,2000,3000,3500,4000`; the daemon also serves the wider standard set (`350,500,750,1000,2500`, and `5000,6000,7000`). Any N outside its set returns 404 (the server does not generate on demand) — if unsure, prefer a size the repo already uses. The image needs an **AES-NI** CPU. A stray local Kestrel/ASP.NET app on 18080 yields "Server returned incorrect test string for latency.txt" — detect with `ss -ltnp | grep 18080`.

### Gotchas that will waste a reproduction attempt

- **Flags before `--help`** (e.g. `netpace --csv --help`) are silently ignored **by design** — not a bug, do not add tests for it (help is only recognised at position 0 or as the second token after a subcommand). See CLAUDE.md "CLI Help Behaviour".
- **`--downloadsize`/`--uploadsize` are total-byte budget caps** (default effectively off), NOT per-request size controls. Per-request sizing is hard-wired and only reachable through the `NetPace.Core` library API — see [download-upload-size-controls.md](../../../docs/architecture/download-upload-size-controls.md).
- **Core.Tests runs serially** — `[assembly: CollectionBehavior(DisableTestParallelization = true)]` (the memory-limit test needs deterministic allocation). Don't read the lack of parallelism as a hang.
- **There is no real-network test category.** CLAUDE.md mentions "a separate test category" but no `[Trait]`/`Collection`/`Skip` implements it today — every existing test is hermetic. A live-network loop must use the Docker server (builder 5), not a test filter.

---

## Phase 3 — Locate the bug's project, then read the source

### Name the project before ranking hypotheses

NetPace splits cleanly, and the split drives where the loop and the fix belong:
- **`NetPace.Core`** — speed calculation, unit conversion (SI/IEC, bits/bytes), server selection, sizing, all provider code under `Clients/Ookla/`. Reproduce with builder 4 (MockHttp).
- **`NetPace.Console`** — CLI parsing, `--help`, output formatting (normal/CSV/JSON), Spectre rendering. Reproduce with builder 3 (Verify snapshot).

A symptom seen in CSV output whose root cause is a wrong speed value belongs in Core, not Console — don't build the loop at the wrong layer.

### Read the source that implements a hypothesis before betting on it

Test names and class names lie; source doesn't. Read the implementing lines before assigning a confidence weight. Read [docs/conventions/csharp-style.md](../../../docs/conventions/csharp-style.md) before touching any `.cs`.

### Watch the dependency direction

Provider-agnostic types in `NetPace.Core` (`SpeedUnit`, `SpeedScale`, `SpeedUnitSystem`) must never depend on concrete provider types — a "fix" that adds e.g. `speedUnit.ToOoklaSpeedtestSettings()` points the arrow the wrong way. See [project_dependency_direction.md](../../memory/project_dependency_direction.md) (its `Profile`/`ToOoklaSpeedtestSettings` example is a hypothetical rejected design, not a type that exists today).

---

## Phase 5 — Regression test before the fix (NON-NEGOTIABLE)

Constitution Principle I makes TDD non-negotiable: write the failing regression test, watch it RED, apply the fix, watch it GREEN, then refactor. The **correct seam** (SKILL.md Phase 5) maps to the project:

- **Core bug** → a test in `src/NetPace.Core.Tests/` using MockHttp against a fixture that reproduces the real call pattern. Mirror the source file (`OoklaSpeedtest.cs` → `OoklaSpeedtestTests.cs`, split into partial facet files for large classes).
- **Console output bug** → a Verify test in `src/NetPace.Console.Tests/` via `CommandLineTestHost` + DI stubs. The new `.verified.txt` is the regression lock; review its `.received.txt` before accepting.

If the only reachable seam is at the wrong layer (a Console snapshot for a Core calculation bug), that shallow seat gives false confidence — note it as the finding per SKILL.md Phase 5.

Before the fix is "done":
- `dotnet build src` succeeds with **zero warnings** (constitution V); no `[Fact(Skip)]`, no `NotImplementedException` stubs.
- If the fix touches a **public `NetPace.Core` API**, it ships to NuGet consumers — it needs `///` XML docs and prior approval (CLAUDE.md); it is not done until both are handled.
- Keep the change **AOT-trim-safe** — no runtime reflection (Spectre.Console.Cli was removed for exactly this).

---

## Phase 6 — Cleanup

- **Grep `[DEBUG-...]` across the whole repo** before declaring done — stale tags rot in files outside your diff. After any simplification, also grep the repo for the removed concept (comments and docs drift silently); see [feedback_grep_after_simplifying.md](../../memory/feedback_grep_after_simplifying.md).
- **Tear down the Docker server** (`./docker/ooklaserver/stop.sh`) and delete any stray `*.received.txt` or throwaway fixtures.
- **Scratch/instrumentation files go in `.claude/scratch/`** (gitignored) — never `/tmp` or the repo root. See [feedback_scratch_file_location.md](../../memory/feedback_scratch_file_location.md).
- **State the winning hypothesis in the commit / PR message** so the next debugger inherits it. If the post-mortem's "what would have prevented this?" answer is architectural (no good seam, tangled layers, wrong dependency direction), record it as the finding and hand off — after the fix is in, not before.
