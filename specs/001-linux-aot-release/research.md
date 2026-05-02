# Phase 0 Research: Linux Native AOT Release Artifacts

**Feature**: 001-linux-aot-release
**Date**: 2026-05-01

The issue body (#176) and spec resolved most decisions up front. This document records the residual research needed to design Phase 1 artifacts and to bound risk during implementation.

---

## R-1: AOT-safe replacement for `XmlSerializer.Deserialize<OoklaServerList>`

**Decision**: Hand-roll an `XDocument`/`XmlReader`-based parser that reads attributes directly off the `<server>` elements under `<settings><servers>`. Drop `[XmlRoot]`/`[XmlArray]`/`[XmlArrayItem]`/`[XmlAttribute]` decoration on `OoklaServer` and `OoklaServerList`; the types stay public (consumer-facing data) but no longer carry serializer attributes.

**Rationale**:
- `XmlSerializer` triggers `IL2026` / `IL3050` (requires unreferenced/dynamic code) under AOT and is the single biggest source of trim risk in `NetPace.Core` today.
- `XDocument` and `XmlReader` are fully BCL, AOT-safe, and have no reflection requirements when used with explicit attribute reads.
- The Ookla server-list XML is small (~500 entries × 8 attributes); LINQ-to-XML over `XDocument.Parse(...)` with `.Element("settings").Element("servers").Elements("server").Select(...)` is sufficient and remains readable.
- No third-party library or source generator needed. Zero new dependencies.

**Alternatives considered**:
- **`System.Text.Json` source generator**: rejected — Ookla's wire format is XML, not JSON. Out of scope.
- **`System.Xml.Serialization.IXmlSerializable`**: rejected — still drags `XmlSerializer` and reflection codepaths during AOT.
- **`System.Runtime.Serialization.DataContractSerializer`**: rejected — same trim hazards as `XmlSerializer`.
- **Roslyn source generator for XML**: rejected — overkill for one type. Hand-rolled parser fits in <60 LOC and is explicit.

**Implementation notes**:
- Tests use real Ookla XML fixtures in `NetPace.Core.Tests` (a sample response captured from `/speedtest-config.php` once, checked into the test project as a string resource).
- Parser MUST tolerate missing optional attributes (`country`, `host`) — already optional today.
- Parser MUST throw a clear exception on malformed XML (current behaviour: `InvalidOperationException` from `XmlSerializer`); choose `XmlException` or a wrapping `InvalidOperationException` for parity.

---

## R-2: Replacing `Humanizer.TimeSpan.Humanize()` with a hand-rolled formatter

**Decision**: Add a small private extension method `static string Humanize(this TimeSpan ts)` (or an inline helper) inside the `NetPace.Console` project that reproduces the user-visible output of `Humanizer.TimeSpan.Humanize(precision: 1)` for the durations currently produced by speed tests (typically 1–60 seconds).

**Rationale**:
- Humanizer is the only reflection-heavy package referenced by `NetPace.Console` and is a known AOT hazard (resource-loading, `ILocaliser`).
- Only **two** call sites use it today: `DefaultConsoleWriter.cs:100` and `:106` (`elapsed.Humanize()`). The `MinimalConsoleWriter.cs` `using Humanizer;` is dead and can be removed.
- Output for sub-minute, second-resolution `TimeSpan` is trivial: "X seconds" / "1 second" / "X.Y seconds" depending on precision.
- Removing the package shrinks both `-net8` and `-aot` archives slightly and removes a transitive dep.

**Alternatives considered**:
- **Keep Humanizer and annotate AOT exemptions**: rejected — leaves IL2026 warnings in place and grows over time as more code touches the package.
- **Spectre.Console formatting helpers**: Spectre formats markup, not durations.
- **`TimeSpan.ToString("g")`**: rejected — produces "0:00:01.2345678", not user-friendly.

**Acceptance**:
- Output for the same `TimeSpan` value matches Humanizer's `Humanize(precision: 1)` for 1–600 seconds (covers >99% of realistic test durations).
- Unit tests in `NetPace.Console.Tests` cover singular/plural, fractional seconds (rounded), and zero/negative defensive cases.

---

## R-3: Choice of GitHub-hosted ARM64 runner

**Decision**: Use `ubuntu-24.04-arm` for the `linux-arm64-aot` matrix entry.

**Rationale**:
- GitHub-hosted ARM64 runners became free for public repos in January 2025.
- Native runner avoids QEMU/cross-compilation complexity entirely — `dotnet publish -r linux-arm64 -p:PublishAot=true` runs natively, smoke test runs natively.
- Cross-compilation of AOT (`linux-x64` host → `linux-arm64` target) requires extra toolchain (`gcc-aarch64-linux-gnu`, etc.) and emulated smoke test (`qemu-user-static`), both of which add fragility.

**Alternatives considered**:
- **`ubuntu-latest` + cross-compile + QEMU smoke test**: rejected — fragile, slower, more moving parts, and free ARM64 runners exist.
- **Self-hosted ARM runners**: rejected — unnecessary complexity for a public repo.

**Risk**: GitHub may revoke or rate-limit the free ARM64 runner tier. Documented as an Assumption in `spec.md`. If revoked, fallback would be cross-compile + QEMU.

---

## R-4: Where to set AOT properties — `csproj` vs MSBuild flags

**Decision**:
- `IsAotCompatible=true` set as a static property in **both** `NetPace.Core.csproj` and `NetPace.Console.csproj`.
- `PublishAot=true` set **only** as a CLI flag (`-p:PublishAot=true`) in the workflow's AOT matrix entries — never as a static `csproj` property.
- `InvariantGlobalization=true` set **only** as a CLI flag in the workflow's AOT matrix entries.
- No `Directory.Build.props`; per-project properties are sufficient given there are only two projects in scope.

**Rationale**:
- `IsAotCompatible=true` activates the analyzer continuously (during normal `dotnet build`), surfacing problems early — issue requirement.
- Statically setting `PublishAot=true` would force every dev-machine `dotnet publish` to attempt AOT, breaking the existing self-contained and framework-dependent variants.
- `InvariantGlobalization` is undesirable for non-AOT variants (they have ICU available) — restrict to AOT publish only.
- Avoiding `Directory.Build.props` keeps the change footprint minimal and easy to revert.

**Alternatives considered**:
- **`Directory.Build.props` with conditional properties**: rejected — over-abstraction for two csproj files.
- **Separate AOT-only csproj (`NetPace.Console.Aot.csproj`)**: rejected — duplicates the project file and introduces drift risk.

---

## R-5: Workflow matrix shape — third dimension vs `matrix.include`

**Decision**: Two explicit `matrix.include:` entries appended to the existing single matrix block. Each `include` entry carries:

- `runtime: linux-x64` / `linux-arm64`
- `deployment: aot`
- `runs-on: ubuntu-latest` / `ubuntu-24.04-arm`
- `publish_single_file: false` (default for non-AOT entries: `true`)
- `publish_aot: true` (default for non-AOT entries: `false`)
- `invariant_globalization: true` (default: `false`)

**Rationale**:
- Adding a third matrix dimension (`variant: [self-contained, framework-dependent, aot]`) would multiply with the runtime axis and produce nonsensical combinations (e.g. `osx-arm64 × aot`) that need explicit `exclude:` entries — more verbose, error-prone.
- The 12 pre-existing combinations stay byte-identical: existing matrix block keeps its current `runtime × deployment` grid, and the AOT entries are pure additions via `include`.
- `runs-on` becomes a per-entry value (existing entries inherit `ubuntu-latest` from the job's top-level `runs-on`; AOT entries override).

**Alternatives considered**:
- **Separate parallel job (`build-linux-aot`)**: rejected — duplicates checkout/setup-dotnet/extract-version steps. Less maintainable than a unified matrix.
- **Third matrix dimension with exclusions**: rejected — verbose and obscures intent.

**Implementation note**: Set the job-level `runs-on:` to a matrix expression (`runs-on: ${{ matrix.runs_on || 'ubuntu-latest' }}`) so existing entries continue to use `ubuntu-latest` while AOT entries can specify `ubuntu-24.04-arm`.

---

## R-6: Smoke test scope and failure mode

**Decision**: Three commands per AOT archive on its native runner: `./netpace --version`, `./netpace --help`, `./netpace servers`. All three MUST exit `0`. `netpace servers` exercises HTTPS + XML parsing end-to-end.

**Rationale**:
- `--version` validates the binary launches at all (most common AOT failure mode).
- `--help` validates `System.CommandLine` argument parsing under AOT.
- `servers` validates the `XmlExtensions` rewrite under AOT against the real Ookla wire format. If the rewrite has a bug only AOT-exposes, this catches it before release.

**Failure mode**: any non-zero exit fails the matrix job, which fails the release. No release archive is attached on smoke-test failure.

**Alternatives considered**:
- **Smoke test only `--version`**: rejected — wouldn't catch the `XmlExtensions` AOT regression that this feature is partly about.
- **Run a download/upload speed test**: rejected — non-deterministic, slow, network-dependent on Ookla server health.

---

## R-7: Size assertion — absolute threshold vs relative comparison

**Decision**: Per-RID relative assertion: `aot < standalone` for both `linux-x64` and `linux-arm64`. Mirror the existing `framework-dependent < self-contained` check pattern in `release-binaries.yml`.

**Rationale**:
- Mirrors existing convention (no new validation pattern to learn).
- Absolute thresholds (e.g. "AOT must be < 30 MB") would drift over time as .NET evolves and would need maintenance.
- Relative assertion is a meaningful gate: if the AOT archive is bigger than self-contained, something has gone wrong (trimming failed, or AOT compilation produced unexpectedly large output).

**Alternatives considered**:
- **Absolute size threshold**: rejected — drift risk, maintenance burden.
- **No size assertion**: rejected — loses a cheap, valuable signal that AOT is actually trimming.

---

## R-8: Documentation surface — what goes where

**Decision**:

| File | Section | Content |
|------|---------|---------|
| `README.md` | Install table | Add two rows: `linux-x64-aot.tar.gz`, `linux-arm64-aot.tar.gz`. Footnote: "recommended for IoT/embedded Linux deployments". |
| `USER_GUIDE.md` | "Choosing a download" (new short section) | Three-bullet comparison: AOT (smallest, fastest start, no runtime), self-contained (any RID, larger, runtime included), framework-dependent (smallest archive but requires .NET 8 runtime). |
| `CHANGELOG.md` | Next release entry | "Added: Linux Native AOT release artifacts (`linux-x64-aot`, `linux-arm64-aot`). Removed: `Humanizer` dependency from `NetPace.Console`. Internal: `OoklaServerList` XML parsing rewritten to be AOT-safe (no public-API change)." |
| `docs/RELEASING.md` (new) | Full doc | Matrix table, runner-per-RID, naming convention, rationale per variant, smoke-test contract, size-assertion contract, future Windows/macOS AOT placeholders. |
| CIR (filename TBD by `docs/conventions/change-intent-records.md`) | Single doc, alongside PR | Public-API metadata change (`IsAotCompatible=true` on `NetPace.Core`), XML parser rewrite (internal but external wire format), pipeline extension. Seeds the eventual Windows/macOS follow-up. |

**Rationale**: matches the issue's documentation requirements and the existing `CLAUDE.md` rule that CLI option/release-pipeline changes must update README, USER_GUIDE, and CHANGELOG together. CIR boundary is set per `change-intent-records.md` (public-API + cross-cutting infra change qualifies).

---

## Open risks (not blocking; tracked for follow-up)

- **`SuppressTrimAnalysisWarnings`** — must NOT be set anywhere in the codebase. Verify during PR review.
- **`Spectre.Console` AOT readiness** — Spectre.Console 0.54.0 advertises AOT compatibility; if a hidden hazard surfaces under `IsAotCompatible=true` analyzer, file a sub-issue and pin or refactor as needed.
- **`System.CommandLine` 2.0.1** — already AOT-tested by Microsoft; warnings here would be unexpected but not impossible. Same mitigation.
- **`ByteSize` and `Microsoft.Extensions.DependencyInjection`** — both BCL-style; expected clean under AOT analyzer.
