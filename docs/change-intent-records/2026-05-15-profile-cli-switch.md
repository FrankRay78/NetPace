# `--profile` CLI Switch and `Profile` Enum (Public API)

**Intent:** Introduce a public, provider-agnostic `Profile` enum (`Tiny`, `Small`, `Medium`, `Large`, `Mega`) on `NetPace.Core`, surfaced as a single `--profile` CLI switch and as two new public constructors on `OoklaSpeedtestSettings`. The profile bundles per-request shape (`DownloadSizes`, iterations, parallel tasks) and a total-byte cap into one knob — so `--profile tiny` keeps a constrained-plan user within their data budget without further tuning. Shift the default-run traffic from ~370 MiB to ~125 MiB (Medium = the new default), a ≥ 65 % reduction. Accept a pre-1.0 breaking change to `ISpeedTestService`: delete the four `int sizeMb` per-call overloads; the cap now lives on `DownloadTestSettings.DownloadSizeMb` / `UploadTestSettings.UploadSizeMb`.

**Behaviour:**
- Given: a user runs `netpace --profile tiny`
- When: option binding completes
- Then: the constructed `OoklaSpeedtestSettings.DownloadTest.DownloadSizes` is exactly `[350]`, `DownloadSizeIterations == 1`, `DownloadParallelTasks == 1`, `DownloadSizeMb == 1`; the upload counterparts are `UploadSizeIncrementKb == 50`, `UploadIncrements == 1`, `UploadSizeIterations == 1`, `UploadParallelTasks == 1`, `UploadSizeMb == 1` (≤ 1 MiB total per run).
- Given: a user runs `netpace` with no flags
- When: option binding completes
- Then: the constructed `OoklaSpeedtestSettings` is field-for-field equal to `new OoklaSpeedtestSettings(Profile.Medium)` (the parameterless constructor chains to `Profile.Medium` as the single source of truth); the Medium cap is 100 MiB down + 25 MiB up, ≥ 65 % below the prior ~370 MiB baseline.
- Given: a user runs `netpace --profile tiny --downloadsize 5`
- When: option binding completes
- Then: Tiny's per-request shape is preserved (`DownloadSizes == [350]`, iterations and parallel tasks unchanged) and only `DownloadSizeMb` is overridden to `5` via a `with`-expression. `--no-download` / `--no-upload` continue to short-circuit phases independently of profile.
- Given: a NuGet consumer constructs `new OoklaSpeedtestSettings((Profile)999)`
- When: the constructor body runs
- Then: an `ArgumentOutOfRangeException` is thrown with `ParamName == "profile"`.
- Given: a NuGet consumer reflects on `typeof(NetPace.Core.Profile)`
- When: the inspection runs
- Then: `Namespace == "NetPace.Core"` (top-level, not under `Clients/*`); the `NetPace.Core` assembly contains no static method that takes `Profile` as its first parameter and returns a type under `NetPace.Core.Clients.*`; no `OoklaSpeedtestSettingsExtensions` or `OoklaProfileExtensions` type exists; the source file lives at `src/NetPace.Core/Profile.cs`.
- Given: the published `NetPace.Core.xml` documentation
- When: it is parsed for the `F:NetPace.Core.Profile.Mega` member
- Then: the summary contains `undocumented` (case-insensitive), `5000`, `6000`, `7000`, and a reference to `download-upload-size-controls` (the architecture doc), so consumers see the bonus-payload caveat in IntelliSense.

**Constraints:**
- `Profile` lives at the root of `NetPace.Core` (`src/NetPace.Core/Profile.cs`), not under `Clients/Ookla/`. It carries no extension methods or helper types that reach into provider-specific namespaces. The dependency direction is one-way: `OoklaSpeedtestSettings(Profile)` knows `Profile`; `Profile` knows no provider.
- The entire profile → Ookla mapping is one inline `switch` expression in the new `OoklaSpeedtestSettings(Profile profile)` constructor. No `OoklaSpeedtestSettingsExtensions`, no `OoklaProfileExtensions`, no per-profile factory methods — one source of truth.
- The four `int sizeMb` overloads on `ISpeedTestService` (and the matching ones on `OoklaSpeedtest`) are **deleted**, not deprecated. NetPace is pre-1.0; this is an accepted breaking change to the public NuGet contract and is flagged in the PR title so auto-generated release notes pick it up.
- `DownloadSizeMb` and `UploadSizeMb` move from method parameters into `DownloadTestSettings` / `UploadTestSettings` (with sentinel default `int.MaxValue` for the bare-record case). `OoklaSpeedtest` reads the cap from `settings.DownloadTest.DownloadSizeMb` / `settings.UploadTest.UploadSizeMb` at run-time.
- `dotnet build src/NetPace.sln` must succeed with zero warnings; all tests must remain green. AOT-trim safety is preserved — the constructor switch is pure value-dispatch, no reflection.
- Every public addition carries XML documentation. `Profile.Mega`'s doc explicitly contains the word "undocumented", names the bonus payloads `5000`/`6000`/`7000`, and cross-references `docs/architecture/download-upload-size-controls.md` (enforced by `ProfileXmlDocTests.Profile_Mega_XmlDoc_DocumentsBonusPayloadDependency`).

**Decisions:**

1. **`Profile` at top level of `NetPace.Core`, not under `Clients/Ookla/`.**
   - Rejected: place under `Clients/Ookla/Profile.cs` — couples a provider-agnostic vocabulary to one provider; blocks any second provider from translating the same labels into its own settings record.
   - Rejected: separate `NetPace.Core.Profiles` namespace — over-namespaces for one type and breaks the "sibling-of-`SpeedUnit`" pattern already established for top-level public enums.
   - Chose: top-level. File location is itself a grep-able invariant (verified by a reflection + file-existence test).

2. **Two new constructors on `OoklaSpeedtestSettings` (parameterless + `Profile`-taking) — not an extension method and not a factory.**
   - Rejected: `OoklaSpeedtestSettings.For(Profile)` static factory — introduces a second equally-valid construction path and risks drift if one is updated and the other isn't.
   - Rejected: `Profile.ToOoklaSettings()` extension method — couples `Profile` to provider types (violates the placement invariant).
   - Rejected: parameterless ctor with separate `Profile` property + per-property defaults — splits the mapping across N field initializers and a profile field; users could then construct an inconsistent record (e.g. `Profile = Mega` with Tiny field values).
   - Chose: two ctors, parameterless chains to `Profile.Medium`. One inline switch expression is the single source of truth. `Profile` is consumed by the ctor and not stored on the record (reflection-verified by `OoklaSpeedtestSettings_HasNoProfileProperty`).

3. **Delete the four `int sizeMb` overloads on `ISpeedTestService` rather than keeping them as deprecated wrappers.**
   - Rejected: keep + `[Obsolete]` — adds two construction paths for the same intent and grows the public surface; pre-1.0 status makes the break acceptable.
   - Rejected: keep as overloads that build a transient `OoklaSpeedtestSettings with { … }` per call — works mechanically but encourages per-call settings mutation against a long-lived service instance, which is exactly what this change is moving away from.
   - Chose: delete. Cap variation is now "configure once, build a new `OoklaSpeedtest` instance", matching the existing pattern for proxy/latency/server-discovery settings.

4. **Caps live on the per-phase records (`DownloadTestSettings.DownloadSizeMb`, `UploadTestSettings.UploadSizeMb`), not on `OoklaSpeedtestSettings` directly.**
   - Rejected: top-level `DownloadSizeMb`/`UploadSizeMb` on `OoklaSpeedtestSettings` — splits the "everything about a download phase" surface across two record types; `with`-expression composition becomes awkward (`settings with { DownloadSizeMb = N }` is fine but doesn't co-locate with `DownloadSizes`).
   - Chose: per-phase. `settings.DownloadTest with { DownloadSizeMb = N }` cleanly groups all download knobs together; `--downloadsize` overrides one field via `with` while leaving the profile-supplied `DownloadSizes` / iterations / parallel intact.

5. **`Mega` ships against the bonus payloads (`5000`/`6000`/`7000`) without a runtime fallback.**
   - Rejected: detect-404-and-fall-back-to-historic-10 at runtime — adds a probing round-trip + state machine to every Mega run; the bonus payloads are universal across the nine UK OoklaServer operators we probed (see `docs/architecture/download-upload-size-controls.md` Cross-server validation).
   - Rejected: probe-on-first-use and cache — pushes runtime complexity into the per-server discovery flow.
   - Chose: ship as-is, document the dependency explicitly on `Profile.Mega`'s XML doc, recommend users hit `--profile large` if Mega 404s. A future fallback (re-tune Mega to the historic-10 array with higher iterations) is tracked but out of scope for this change.

6. **CLI option uses `System.CommandLine`'s built-in case-insensitive enum binding; no custom parser; no alias.**
   - Rejected: add `-p` short alias — single-letter aliases are precious; reserve for top-tier options. The full `--profile` name is short enough.
   - Rejected: custom enum-parser with friendlier error text — `System.CommandLine`'s built-in unknown-value message already names the option and lists valid values.
   - Chose: built-in binding, default `Medium`, no alias. Matches the pattern already used by `--unit-system`, `--unit-scale`, `--verbosity`.

7. **Test seam: an `OoklaSpeedtestSettingsAccessor` singleton holds the built settings, written by the action before `ISpeedTestService` is resolved.**
   - Rejected: pass settings via `SpeedTestCommandSettings.OoklaSettings` and have the writers read it — couples the writers to Ookla-specific settings; they'd be unable to work against a different provider.
   - Rejected: change `OoklaSpeedtest` registration to a `Func<OoklaSpeedtestSettings, ISpeedTestService>` factory invoked from the action — production code paths complicate when the action also has to decide test-vs-production wiring.
   - Chose: a small `OoklaSpeedtestSettingsAccessor` (mutable holder) registered as singleton; production resolves `OoklaSpeedtest` via factory reading `accessor.Settings`; tests register an accessor instance and inspect `accessor.Settings` after `RunAsync`.

**Date:** 2026-05-15
