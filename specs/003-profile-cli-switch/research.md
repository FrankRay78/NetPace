# Phase 0 — Research: `--profile` CLI switch

**Feature**: 003-profile-cli-switch
**Date**: 2026-05-15
**Status**: Complete — all `NEEDS CLARIFICATION` resolved before plan was filled

## Scope of research

The source GitHub issue (#174) is unusually detailed and already contains a `Confirmed decisions` block resolving every option that would normally surface as `NEEDS CLARIFICATION` in a fresh spec. Phase 0 therefore consolidates those decisions, validates them against the constitution and existing codebase, and resolves the remaining genuinely-open mechanical questions before Phase 1 design.

No external web research was required; all inputs are repo-internal (issue body, `docs/architecture/download-upload-size-controls.md`, existing enum/CLI patterns in `src/`).

---

## Decisions

### D1 — `Profile` enum location and shape

- **Decision**: Public enum `Profile` at `src/NetPace.Core/Profile.cs` with five members in ascending-size order: `Tiny`, `Small`, `Medium`, `Large`, `Mega`. Sibling of `SpeedUnit.cs`, `SpeedScale.cs`, `SpeedUnitSystem.cs`. No `[Flags]`, no underlying `byte`/`short` cast — default `int` backing.
- **Rationale**: Mirrors three existing precedents in `NetPace.Core`. Top-level placement structurally enforces "provider-agnostic" (no `Clients/Ookla/` namespace).
- **Alternatives rejected**:
  - `Clients/Ookla/Profile.cs` — would couple the label to one provider; rejected by FR-001/FR-002 and the existing "dependency-direction" memory rule.
  - `enum Profile : byte` — micro-optimisation; no measurable benefit; breaks symmetry with `SpeedUnit` etc.
  - Six profiles (adding `XL`, `Custom`, etc.) — explicitly out-of-scope per issue.

### D2 — Provider mapping lives in a constructor's inline switch, not a helper class

- **Decision**: `OoklaSpeedtestSettings` gains two public constructors:
  - `public OoklaSpeedtestSettings() : this(Profile.Medium) { }`
  - `public OoklaSpeedtestSettings(Profile profile) { (DownloadTest, UploadTest) = profile switch { … }; }`
  Entire profile → settings mapping is one switch expression in one file. No `OoklaSpeedtestSettingsExtensions`, no `OoklaProfileBuilder`, no factory method.
- **Rationale**: Confirmed in issue body. Maximum locality; reviewer sees all five profiles side-by-side; `with`-expression composes cleanly with the synthesised record copy-ctor.
- **Alternatives rejected**:
  - Static factory `OoklaSpeedtestSettings.ForProfile(Profile p)` — added indirection with no benefit.
  - Extension method `profile.ToOoklaSettings()` — would violate FR-002 (`Profile` would carry provider knowledge through its extension surface).
  - Per-profile factory methods (`ForTiny()`, `ForSmall()`, …) — five times the API surface for no gain.

### D3 — Move `DownloadSizeMb` / `UploadSizeMb` off method signatures, onto settings records

- **Decision**: Add `int DownloadSizeMb { get; init; } = int.MaxValue;` to `DownloadTestSettings`; add `int UploadSizeMb { get; init; } = int.MaxValue;` to `UploadTestSettings`. Delete four `int sizeMb` overloads on `ISpeedTestService` and the matching `OoklaSpeedtest` methods. `OoklaSpeedtest.GetDownloadSpeedAsync(server, ct)` and `GetDownloadSpeedAsync(server, IProgress, ct)` survive (same shape for upload).
- **Rationale**: Confirmed in issue body. Profile must coherently bundle per-request shape **and** total-byte cap — splitting them across record state and method args defeats the bundle. `int.MaxValue` default preserves "no cap" semantics for raw-record consumers who don't go through `OoklaSpeedtestSettings(Profile)`.
- **Alternatives rejected**:
  - Keep `int sizeMb` overloads in addition to record-state — two ways to set the same value; ambiguous precedence; rejected.
  - Default to `0` instead of `int.MaxValue` — would require sentinel-value branch in cap-check loop; uglier than truncating-at-`int.MaxValue` natural behaviour.

### D4 — Profile values for Ookla

- **Decision**: Use exactly the table in spec FR-018..FR-020 (issue body's table). Tiny/Small/Medium/Large draw payloads only from `{350, 500, 750, 1000, 1500, 2000, 2500, 3000, 3500, 4000}`. Mega adds `5000, 6000, 7000`.
- **Rationale**: Per-request byte sizes are validated cross-server in `docs/architecture/download-upload-size-controls.md`. Quarantining the bonus-payload risk to one profile keeps four out of five profiles maximally resilient to upstream changes.
- **Alternatives rejected**:
  - All profiles use bonus payloads — fragility leaks to ordinary users; rejected.
  - Mega uses only historic-10 with higher iterations — cannot reach steady-state on 10 Gbps; documented as future fallback only if upstream removes bonus payloads.

### D5 — Default profile and override interaction

- **Decision**: `Medium` is the implicit default (`netpace` with no flags equals `netpace --profile medium`). Explicit `--downloadsize` / `--uploadsize` override only `DownloadTest.DownloadSizeMb` / `UploadTest.UploadSizeMb` via `with`-expression; all other per-request shape fields are profile-derived and not CLI-overridable in this feature. `--no-download` / `--no-upload` short-circuit regardless of profile.
- **Rationale**: Confirmed in issue body. Single user-visible decision; explicit overrides act as a backstop, not a directive.
- **Alternatives rejected**:
  - `Tiny` default — too conservative for the typical home-broadband user.
  - Auto-detect default ("pick a profile based on observed link speed") — explicit user choice only per "Out of scope".

### D6 — CLI flag binding shape

- **Decision**: `var profileOption = new Option<Profile>("--profile") { Description = "Profile bundle of payload settings (Tiny | Small | Medium | Large | Mega).", DefaultValueFactory = _ => Profile.Medium };`. No short alias, no custom error message — rely on `System.CommandLine`'s default unknown-value error.
- **Rationale**: Confirmed in issue body. Matches existing `--unit-system` precedent.
- **Alternatives rejected**:
  - Short alias `-p` — none of the existing enum flags has a short alias; consistency wins.
  - Custom error message ("did you mean Tiny?") — defer; `System.CommandLine`'s default is good enough.

### D7 — CIR storage path

- **Decision**: `docs/change-intent-records/CIR-NNN-profile-cli-switch.md` (using next sequential CIR number).
- **Rationale**: Confirmed in issue body. The `docs/cir/` reference in the original issue text is a typo; the actual repo directory is `docs/change-intent-records/`.
- **Alternatives rejected**: `docs/cir/` — does not exist in the repo.

### D8 — Testing strategy

- **Decision**:
  - Profile → settings mapping covered by **unit tests only** in `NetPace.Core.Tests` (no Docker-backed integration test).
  - CLI binding and `--profile` × `--downloadsize` override interaction covered in `NetPace.Console.Tests`, using the existing `CommandLineTestHost` pattern.
  - `--help` snapshot refreshed via VerifyXunit pattern under `Expectations/`.
  - End-to-end byte-budget verification (SC-001/SC-002/SC-003) is treated as a manual/operational check (run against a known server, observe transferred bytes) — not gated by automation, because spec confirmed decisions explicitly ruled out Docker integration tests.
- **Rationale**: Per spec confirmed decisions: "Docker integration tests considered an anti-pattern" for profile→settings wiring. The mapping is pure data; testing it via integration would just re-verify what unit tests already prove field-by-field.
- **Alternatives rejected**:
  - Add Docker integration test against `docker/ooklaserver/` — rejected by confirmed decision.
  - Skip the regression-guard test for Mega's bonus payloads — rejected by FR-019 / spec scenario "Mega regression guard".

### D9 — Documentation scope

- **Decision**: Update README (--help snapshot, options table, one example), USER_GUIDE (new "Choosing a profile" section + Mega warning callout), `docs/architecture/download-upload-size-controls.md` (cross-ref section), XML docs on every new public member, new CIR. No CHANGELOG.md (does not exist in repo); release notes auto-generated from PR title/body.
- **Rationale**: Per CLAUDE.md and the project memory rule `feedback_cli_feature_doc_scope` — every NetPace CLI feature must scope user-facing docs from the start; release-pipeline / docs/RELEASING.md not in scope here.
- **Alternatives rejected**:
  - Add CHANGELOG.md to track this feature — explicitly counter to the repo convention.

### D10 — Test naming for partial-class style

- **Decision**: `NetPace.Core.Tests/OoklaSpeedtestSettingsTests.cs` (top-level entry point + shared helpers) + `OoklaSpeedtestSettingsTests.Profiles.cs` (per-profile exact-equality assertions). Mirrors the existing `OoklaSpeedtestTests.Guards.cs`, `.Memory.cs`, `.ServerListParsing.cs` partial-class split.
- **Rationale**: Established repo convention.

---

## Resolved unknowns

| Original area | Resolution |
|---|---|
| Where does `Profile` live? | `src/NetPace.Core/Profile.cs` (D1). |
| How is the profile → settings mapping expressed? | Inline switch in `OoklaSpeedtestSettings(Profile)` ctor (D2). |
| Do per-phase caps move? | Yes — `DownloadSizeMb` / `UploadSizeMb` move onto `DownloadTestSettings` / `UploadTestSettings`; method overloads deleted (D3). |
| Concrete profile field values? | Per-table in spec FR-018..FR-020 (D4). |
| Default profile? | `Medium`; both via parameterless ctor chaining and CLI `DefaultValueFactory` (D5). |
| CLI flag binding? | `Option<Profile>` with default-value factory; no short alias (D6). |
| CIR path? | `docs/change-intent-records/` (D7). |
| Docker integration tests? | No (D8). |
| Docs scope? | README + USER_GUIDE + architecture doc + XML + CIR; no CHANGELOG (D9). |
| Test file naming? | Partial-class split per existing convention (D10). |

**No `NEEDS CLARIFICATION` markers remain.** Phase 1 may proceed.

---

## Constitution re-check after research

All eight principles still PASS — research surfaced no contradictions. TDD obligations are concrete (each FR has a paired test in spec FR-005..FR-007 / SC-005). Library-First is reinforced (CLI is a thin consumer). Minimal-Dependencies is unchanged (zero new packages).
