# Feature Specification: Add `--profile` CLI switch (Tiny/Small/Medium/Large/Mega)

**Feature Branch**: `003-profile-cli-switch`
**Created**: 2026-05-15
**Status**: Draft
**Input**: GitHub issue #174 — "Add --profile CLI switch (Tiny/Small/Medium/Large/Mega)"

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Run NetPace on a constrained data plan without busting the cap (Priority: P1)

A user on a metered or IoT data plan (e.g. 10 MB/month) needs to run NetPace to check link health without blowing through their monthly allowance. Today the default run transfers ≈ 370 MiB and there is no single-knob way to ask for a lightweight run. The user picks `--profile tiny` (or `--profile small`) and NetPace runs end-to-end within a strict byte budget appropriate to their plan.

**Why this priority**: Without this, an entire class of users (IoT, cellular, metered) cannot use NetPace at all. It is the largest reachability gap the feature closes and the most concrete user pain-point cited in the motivation.

**Independent Test**: Run `netpace --profile tiny` against any reachable Ookla server (or the bundled local Docker OoklaServer) and confirm the total transferred bytes are within ±10 % of the published Tiny budget (~245 KB down + ~50 KB up). Delivers value on its own — even with no other profile, Tiny alone makes NetPace usable on a 10 MB/month plan.

**Acceptance Scenarios**:

1. **Scenario: Tiny profile stays within IoT budget**
   Given a user runs NetPace with `--profile tiny`, When the run completes successfully against a reachable Ookla server, Then total transferred bytes are ≤ 1 MiB (well under the ~370 MiB default), and per-run download bytes fall within ±10 % of ~245 KB and upload bytes within ±10 % of ~50 KB.

2. **Scenario: Small profile suits cellular**
   Given a user runs NetPace with `--profile small`, When the run completes, Then total transferred bytes are ≤ ~12 MiB, suitable for a typical mobile data plan.

3. **Scenario: Profile is authoritative for per-request shape**
   Given a user runs `netpace --profile tiny`, When per-request HTTP traffic is observed, Then no individual download request fetches a payload larger than the Tiny profile's largest declared payload size (i.e. no full 4000-pixel JPEG is requested), and concurrent parallel requests do not exceed the profile's parallel-task count.

---

### User Story 2 — Get a sensible default without thinking about it (Priority: P1)

A user runs `netpace` with no flags. They expect a reasonable, well-tested test — not a power-user-grade saturation run. After this change, the implicit default is `--profile medium`, which transfers ≈ 121 MiB (≈ 100 MiB down + ≈ 21 MiB up) instead of the current ≈ 370 MiB. The user gets a faster, lighter, still-representative result with no flag change required.

**Why this priority**: The default is the most-trodden path. Shifting it to `Medium` reduces per-run traffic for every user who never reads the docs. P1 because it is a behaviour change visible to 100 % of users.

**Independent Test**: Run `netpace` with no arguments and confirm the settings record actually constructed inside `Program.RunAsync` is equal — field for field — to `new OoklaSpeedtestSettings(Profile.Medium)`. Total per-run transfer falls within the Medium budget.

**Acceptance Scenarios**:

1. **Scenario: Omitted --profile defaults to Medium**
   Given a user invokes `netpace` with no `--profile` flag, When the CLI binds options and constructs `OoklaSpeedtestSettings`, Then the resulting record is field-for-field identical to `new OoklaSpeedtestSettings(Profile.Medium)`.

2. **Scenario: Parameterless ctor chains to Medium**
   Given library code calls `new OoklaSpeedtestSettings()` with no argument, When the record is constructed, Then it is field-for-field identical to `new OoklaSpeedtestSettings(Profile.Medium)` (single source of truth via `: this(Profile.Medium)`).

3. **Scenario: Default-run traffic drops vs pre-change baseline**
   Given a user runs `netpace` with no flags, When the run completes, Then total per-run transferred bytes are within ±10 % of ~121 MiB (down from the prior ~370 MiB default), and the user sees no functional regression in reported download/upload speeds.

---

### User Story 3 — Saturate a 10 Gbps inter-DC link (Priority: P2)

A power user running NetPace on a fibre or inter-data-centre link cannot currently push enough traffic to reach steady-state — the hardcoded payloads top out at the 4000-pixel image and parallelism is capped low. With `--profile mega`, the user opts into a deliberately heavyweight profile that uses the larger 5000/6000/7000-pixel payloads (undocumented but observed on current OoklaServer) and far higher parallelism, enabling ~10 GiB total transfer per run.

**Why this priority**: Power-user need. Smaller addressable population than P1, but the only path that closes the upper-end reachability gap. P2 because the feature is still useful even if Mega is the last profile delivered.

**Independent Test**: Run `netpace --profile mega` against a fibre-class endpoint (or the Docker OoklaServer with `OoklaServer.MaxFileBlock` set high enough) and observe that requests for `5000`, `6000`, and `7000` payloads are issued, parallel-task count reaches the profile's declared maximum, and total transfer reaches ~10 GiB ±10 %.

**Acceptance Scenarios**:

1. **Scenario: Mega uses bonus payloads**
   Given a user runs `netpace --profile mega`, When download requests are issued, Then the requested `DownloadSizes` set includes `5000`, `6000`, and `7000` (the bonus payloads).

2. **Scenario: Mega's bonus-payload dependency is documented**
   Given a developer reads the XML doc on `Profile.Mega`, When they review the doc text, Then it explicitly states that Mega depends on undocumented OoklaServer payloads (5000/6000/7000) and may break on future OoklaServer releases, and it cross-references `docs/architecture/download-upload-size-controls.md`.

3. **Scenario: Mega regression guard**
   Given a future refactor changes `OoklaSpeedtestSettings(Profile)`'s switch expression, When the test suite runs, Then a dedicated regression test asserts that `new OoklaSpeedtestSettings(Profile.Mega).DownloadTest.DownloadSizes` still includes `5000`, `6000`, and `7000` — preventing silent demotion of Mega.

---

### User Story 4 — Choose a profile, then override one cap (Priority: P2)

A user wants the request shape of a known profile (e.g. parallelism, iterations, payload mix from `Large`) but wants to cap the total transfer lower than the profile's default — for example, `--profile large --downloadsize 100` to use Large's per-request shape but stop after 100 MiB downloaded.

The profile is authoritative for per-request shape; user-supplied `--downloadsize` / `--uploadsize` override the per-phase byte-budget caps via `with`-expression on top of the profile-built settings.

**Why this priority**: Power-user composability. Useful but not a reachability gap; profiles alone (P1) deliver MVP. P2.

**Independent Test**: Bind `--profile large --downloadsize 100`, inspect the constructed settings record, and confirm: download per-request shape (`DownloadSizes`, iterations, parallel tasks) equals the Large profile's; `DownloadTest.DownloadSizeMb` equals 100 (not Large's natural ~1024).

**Acceptance Scenarios**:

1. **Scenario: --downloadsize overrides only the cap, profile shape is preserved**
   Given a user runs `netpace --profile tiny --downloadsize 5`, When the settings record is constructed, Then `DownloadTest.DownloadSizes`, `DownloadTest.DownloadSizeIterations`, and `DownloadTest.DownloadParallelTasks` match Tiny's profile values, and `DownloadTest.DownloadSizeMb` equals 5.

2. **Scenario: --uploadsize overrides only the upload cap**
   Given a user runs `netpace --profile small --uploadsize 1`, When the settings record is constructed, Then upload per-request shape matches Small's profile and `UploadTest.UploadSizeMb` equals 1.

3. **Scenario: Override cap larger than natural transfer is a no-op backstop**
   Given a user runs `netpace --profile tiny --downloadsize 5000`, When the run completes, Then the run completes naturally (Tiny transfers well under 5000 MiB so the cap is never hit), and the cap is mechanically present on the settings record but the cap-hit check never triggers.

4. **Scenario: --no-download short-circuits regardless of profile**
   Given a user runs `netpace --no-download --profile large`, When the test runs, Then the download phase is skipped, the upload phase still uses Large's profile values, and Large's download shape has no observable effect.

---

### User Story 5 — Library consumer uses Profile from `NetPace.Core` (Priority: P2)

A library consumer (e.g. a unit-test author or a NuGet downstream) wants to drive `OoklaSpeedtest` directly without going through the CLI. They construct a settings record from a profile, optionally `with`-customise non-payload fields (e.g. proxy), and pass it in.

**Why this priority**: The library-first principle means NuGet consumers must benefit alongside the CLI. Important but reachable via library use only — P2.

**Independent Test**: In `NetPace.Core.Tests`, write a test that constructs `new OoklaSpeedtestSettings(Profile.Tiny)`, then `with { UseProxy = true, ProxyAddress = … }`, and asserts both the profile-derived fields and the proxy fields are present on the resulting record. No CLI involvement.

**Acceptance Scenarios**:

1. **Scenario: Profile enum is provider-agnostic and at the root of NetPace.Core**
   Given a developer browses `NetPace.Core` source, When they inspect `Profile`, Then `Profile` lives at `src/NetPace.Core/Profile.cs` (top-level, sibling of `SpeedUnit`, `SpeedScale`, `SpeedUnitSystem`, **not** under `Clients/Ookla/`), and `Profile` has no extension methods that reference any provider type.

2. **Scenario: `with` expression composes cleanly on profile-built record**
   Given a developer writes `var s = new OoklaSpeedtestSettings(Profile.Mega) with { UseProxy = true };`, When the expression is compiled and evaluated, Then `s` has Mega's `DownloadTest`/`UploadTest` values and `UseProxy == true`.

3. **Scenario: Construct invalid profile throws**
   Given a developer calls `new OoklaSpeedtestSettings((Profile)999)`, When the constructor's switch evaluates, Then `ArgumentOutOfRangeException` is thrown with parameter name `profile`.

---

### Edge Cases

- **Invalid `--profile` value** — e.g. `--profile huge`. `System.CommandLine` enum binding rejects with its standard error message; no custom alias or short flag is offered (per confirmed decision in the issue body).
- **Case sensitivity** — `--profile TINY`, `--profile tiny`, `--profile Tiny` must all parse to `Profile.Tiny` (matches existing `--unit-system` etc.).
- **No-cap raw-record consumers** — A library consumer who constructs `new DownloadTestSettings { … }` directly (without going through `OoklaSpeedtestSettings(Profile)`) still gets `int.MaxValue` as the default for `DownloadSizeMb` / `UploadSizeMb`, preserving "no cap unless explicitly set" semantics for raw-record use.
- **Override cap larger than profile's natural transfer** — `--profile tiny --downloadsize 5000`: the cap is present on the record but the cap-hit check (`totalBytesReturned >= maxBytes`) never triggers because Tiny completes well under 5000 MiB on its own. Documented as a backstop, not a directive.
- **Mega payloads disappear upstream** — If `5000`/`6000`/`7000` stop being served by future OoklaServer releases, Mega returns errors. The fallback strategy (revert to historic-10 only, raise iterations proportionally) is out of scope for this feature but called out in `Open questions`. The XML doc on `Profile.Mega` warns of this risk.
- **`--no-download` / `--no-upload` with any profile** — Phase short-circuit always wins; the profile's values for the skipped phase have no observable effect.
- **Existing `int downloadSizeMb` / `int uploadSizeMb` method overloads on `ISpeedTestService`** — These are deleted (breaking change to the public NuGet contract; CHANGELOG breaking-change entry required). Per-call variation now uses `settings with { DownloadSizeMb = N }`.

## Requirements *(mandatory)*

### Functional Requirements

#### Public API surface

- **FR-001**: `NetPace.Core` MUST expose a public enum `Profile` with members `Tiny`, `Small`, `Medium`, `Large`, `Mega`, located at `src/NetPace.Core/Profile.cs` (top-level, not under `Clients/Ookla/`).
- **FR-002**: `Profile` MUST have no extension methods that reference any provider-specific type — verified by structural test / grep.
- **FR-003**: `OoklaSpeedtestSettings` MUST expose two public constructors: a parameterless `OoklaSpeedtestSettings()` and `OoklaSpeedtestSettings(Profile profile)`.
- **FR-004**: The parameterless constructor MUST chain to the profile-taking constructor with `Profile.Medium` (`: this(Profile.Medium)`), so `Profile.Medium` is the single source of truth for the default.
- **FR-005**: The `Profile`-taking constructor MUST contain the entire profile → download/upload settings mapping inline as a single switch expression. No separate helper class, factory method, or extension method may hold any of the per-profile values.
- **FR-006**: No `OoklaSpeedtestSettingsExtensions`, `OoklaProfileExtensions`, or any similarly-named profile-related helper class may exist in the codebase.
- **FR-007**: The constructor's switch expression MUST throw `ArgumentOutOfRangeException` (with parameter name `profile`) for unknown `Profile` enum values.
- **FR-008**: `OoklaSpeedtestSettings` instance state MUST NOT include a `Profile` property — settings record state stays pure data with no profile field.

#### Per-phase settings move

- **FR-009**: `DownloadSizeMb` MUST move from a method parameter on `GetDownloadSpeedAsync` into `DownloadTestSettings`; `UploadSizeMb` MUST move into `UploadTestSettings`. `OoklaSpeedtest` reads them from the settings record.
- **FR-010**: The existing `int sizeMb` / `int downloadSizeMb` / `int uploadSizeMb` parameter overloads on `OoklaSpeedtest` AND on `ISpeedTestService` MUST be deleted (not just declined to be added) — `GetDownloadSpeedAsync(server, ct)` and `GetDownloadSpeedAsync(server, IProgress<…>, ct)` are the only surviving signatures per direction. This is an accepted breaking change to the public NuGet contract.
- **FR-011**: The default value for `DownloadTestSettings.DownloadSizeMb` and `UploadTestSettings.UploadSizeMb` when constructed directly (not via `OoklaSpeedtestSettings(Profile)`) MUST be `int.MaxValue`, preserving "no cap unless explicitly set" semantics for raw-record consumers.

#### CLI surface

- **FR-012**: The CLI MUST expose a `--profile` option of type `Profile`, with case-insensitive parsing (matching existing enum-flag behaviour for `--unit-system` etc.).
- **FR-013**: The CLI MUST reject unknown `--profile` values with the default `System.CommandLine` error message; no custom alias or short flag is required.
- **FR-014**: When `--profile` is omitted, the resulting settings record MUST be field-for-field identical to `new OoklaSpeedtestSettings(Profile.Medium)`.
- **FR-015**: Explicit `--downloadsize` / `--uploadsize` flags MUST override the profile-derived `DownloadTest.DownloadSizeMb` / `UploadTest.UploadSizeMb` via `with`-expression applied after the profile-taking constructor. All other profile-derived per-request shape values (`DownloadSizes`, iterations, parallel tasks, upload increments) MUST be preserved.
- **FR-016**: `--no-download` / `--no-upload` MUST continue to short-circuit their respective phases regardless of profile.
- **FR-017**: When the override cap exceeds the profile's natural transfer total, the override MUST be mechanically present on the settings record but the cap-hit check MUST NOT artificially extend the test — the test completes naturally when the profile's iterations conclude.

#### Profile values (Ookla mapping)

- **FR-018**: For Ookla, `Tiny`, `Small`, `Medium`, and `Large` profiles MUST use only `DownloadSizes` values drawn from the historic Speedtest.net Flash-client array `{350, 500, 750, 1000, 1500, 2000, 2500, 3000, 3500, 4000}`.
- **FR-019**: `Profile.Mega` MUST include `5000`, `6000`, and `7000` in its `DownloadSizes` (verified by regression-guard test) to enable saturation of 10 Gbps inter-DC links.
- **FR-020**: Profile-derived per-run byte totals MUST fall within ±10 % of the published targets (Tiny ~245 KB / ~50 KB; Small ~10 MiB / ~2 MiB; Medium ~100 MiB / ~21 MiB; Large ~1 GiB / ~211 MiB; Mega ~10 GiB / ~2 GiB) when measured against a known-good test server.

#### Documentation

- **FR-021**: All new public types and members in `NetPace.Core` (`Profile` enum and each member; both `OoklaSpeedtestSettings` constructors; the relocated `DownloadSizeMb`/`UploadSizeMb` properties) MUST carry XML documentation comments. The XML doc on `Profile.Mega` MUST explicitly state that it depends on undocumented OoklaServer payloads (5000/6000/7000), warn that it may break on future OoklaServer releases, and cross-reference `docs/architecture/download-upload-size-controls.md`.
- **FR-022**: `README.md` MUST be updated: the `--help` snapshot refreshed and `--profile` documented in the options reference, with a one-line usage example.
- **FR-023**: `USER_GUIDE.md` MUST gain a "Choosing a profile" section with the budget table and decision guidance, including a dedicated warning callout for `Mega` explaining the undocumented-payload dependency.
- **FR-024**: `docs/architecture/download-upload-size-controls.md` MUST gain a section cross-referencing profiles to the per-request size tables and explicitly noting that `Mega` is the only profile relying on the bonus payloads.
- **FR-025**: A new Change Intent Record (CIR) MUST be filed under `docs/change-intent-records/` (not `docs/cir/`) documenting: (a) the public API addition (`Profile` enum and two new ctors); (b) the rationale for placing `Profile` in `NetPace.Core` rather than the Console layer; (c) the dependency direction (provider knows `Profile`; `Profile` knows no provider; entire mapping inline in the provider's settings ctor); (d) the move of `DownloadSizeMb`/`UploadSizeMb` into per-phase settings records and the deletion of the corresponding method overloads on `ISpeedTestService` and `OoklaSpeedtest`.
- **FR-026**: Release notes (auto-generated from PR titles per the repo's release process) MUST flag the new default profile and the per-run traffic reduction. Since there is no checked-in CHANGELOG.md, the breaking-change note belongs in the PR title/body so the GitHub-generated release notes pick it up.

### Key Entities

- **Profile** — Provider-agnostic vocabulary describing the *intent* of a test run. Lives in `NetPace.Core`. Five labels: `Tiny`, `Small`, `Medium`, `Large`, `Mega`. Carries no payload semantics on its own; each provider translates the label into its own settings record.
- **OoklaSpeedtestSettings** — Existing provider-specific root settings record in `NetPace.Core.Clients.Ookla`. Gains two new public constructors (parameterless → Medium; `Profile`-taking with inline switch). Instance state stays pure data — no `Profile` property.
- **DownloadTestSettings** / **UploadTestSettings** — Existing per-phase settings records. Each gains a `DownloadSizeMb` / `UploadSizeMb` property (default `int.MaxValue`) so profile values and `--downloadsize` / `--uploadsize` overrides flow through the settings record instead of method parameters.
- **ISpeedTestService** — Public interface in `NetPace.Core`. The `int sizeMb` overloads on `GetDownloadSpeedAsync` and `GetUploadSpeedAsync` are deleted; only the `(server, ct)` and `(server, IProgress, ct)` signatures survive per direction.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user on a 10 MB/month data plan can run NetPace via `--profile tiny` and complete at least 30 runs/month within their cap (each run consumes ≤ 1 MiB end-to-end, ±10 %).
- **SC-002**: A user running NetPace with no flags transfers ≈ 121 MiB per run (±10 %) — a reduction of ≥ 65 % versus the previous default of ≈ 370 MiB — with no functional regression in reported download/upload speeds.
- **SC-003**: A power user running `--profile mega` against a fibre/inter-DC endpoint sustains transfer totalling ≈ 10 GiB per run (±10 %) and reaches steady-state throughput on a 10 Gbps link.
- **SC-004**: For every profile, the user can predict per-run total traffic from a single published table (USER_GUIDE.md "Choosing a profile") without reading source code. Verified by a documentation review: the table appears in `USER_GUIDE.md` and the per-profile rows match the test-asserted values.
- **SC-005**: For every profile, a unit test asserts the exact `DownloadTest` and `UploadTest` field values produced by `new OoklaSpeedtestSettings(profile)` — 5 profiles × per-field exact-equality. (Pass criterion: each profile's test is green; no field is left unasserted.)
- **SC-006**: Adding a second speed-test provider in future requires zero changes to `Profile` itself — the new provider's settings record adds its own `Profile`-taking constructor with its own inline switch. Verified structurally: the existing `Profile.cs` file has no provider import.
- **SC-007**: Help discoverability — `netpace --help` output displays `--profile` with the description "Profile bundle of payload settings (Tiny | Small | Medium | Large | Mega)" and lists Medium as the default. Verified by a CLI snapshot test under `NetPace.Console.Tests` Expectations.

## Assumptions

- The local Docker OoklaServer (`docker/ooklaserver/`) reproduces the production server's behaviour faithfully enough for byte-budget verification in development, but no Docker-backed integration test is added — per confirmed decision, profile→settings mapping is covered by unit tests only.
- The historic Flash-client payload array `{350, 500, 750, 1000, 1500, 2000, 2500, 3000, 3500, 4000}` remains supported by the current Ookla server fleet for the lifetime of NetPace.
- The bonus payloads `5000`, `6000`, `7000` continue to be served by the OoklaServer endpoints used by `Mega`. Fallback strategy if they disappear is out of scope here (tracked in Open questions on the issue).
- `System.CommandLine` enum binding is already case-insensitive in the existing NetPace setup; no extra binder wiring is needed for `--profile`.
- The repo's release-notes generation (GitHub-auto from PR titles/labels) is sufficient to surface the breaking-change ISpeedTestService overload deletion and the default-traffic reduction to consumers — there is no checked-in CHANGELOG.md to maintain.
- NetPace is pre-1.0, so the deletion of `int sizeMb` overloads on `ISpeedTestService` and the change of the default profile (`Medium` instead of the prior hardcoded behaviour) are treated as routine breaking changes — flagged in the PR title and CIR, but not requiring a major-version step beyond normal semver progression.
- "Per-run transferred byte totals fall within ±10 % of the target" is verifiable in isolation by inspecting the settings record's `DownloadSizes`/iterations/parallel/cap fields; full end-to-end byte counting is captured by SC-001 through SC-003 but does not require a Docker integration test.
- The CIR storage path is `docs/change-intent-records/` (the existing authoritative directory) — the issue body's `docs/cir/` reference is treated as a typo and corrected.
