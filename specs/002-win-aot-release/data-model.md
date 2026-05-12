# Phase 1 Data Model: Windows Native AOT Release Artifacts

**Feature**: 002-win-aot-release
**Date**: 2026-05-10

This feature ships no source-code changes and therefore no C# domain entities. The "data" of the feature is the **release-pipeline configuration shape** — the rows of the build matrix and the resulting attached release assets. Modelling that shape explicitly catches drift between spec, plan, and tasks.

## Entity 1: Matrix entry

A single row of `jobs.build-cross-platform.strategy.matrix` (either from the base cross-product or from `matrix.include:`).

| Field | Type | Notes |
|-------|------|-------|
| `runtime` | enum: `win-x64` \| `win-arm64` \| `linux-x64` \| `linux-arm64` \| `osx-x64` \| `osx-arm64` | .NET Runtime Identifier. |
| `deployment` | enum: `self-contained` \| `framework-dependent` \| `aot` | Variant suffix selector. |
| `runs_on` | string (GitHub runner image) | Defaults to `ubuntu-latest` via the job-level expression `${{ matrix.runs_on || 'ubuntu-latest' }}`. AOT entries override per R1/R2. |
| `publish_aot` | bool | Set on AOT entries only; flips the publish step's branch. |
| `publish_single_file` | bool | `false` for AOT (native AOT is already a single executable); `true` for self-contained / framework-dependent on Linux/macOS, ignored on Windows non-AOT (Windows non-AOT inherits the same `true` value for `PublishSingleFile`). |
| `invariant_globalization` | bool | `true` for AOT entries. |

### State transitions

A matrix entry has no state — it's static configuration. The pipeline derives a job from each entry.

### New entries for this feature

| `runtime` | `deployment` | `runs_on` | `publish_aot` | `publish_single_file` | `invariant_globalization` |
|-----------|--------------|-----------|---------------|----------------------|---------------------------|
| `win-x64` | `aot` | `windows-latest` | `true` | `false` | `true` |
| `win-arm64` | `aot` | `windows-11-arm` | `true` | `false` | `true` |

Shape is intentionally identical to the existing Linux AOT entries; only `runtime` and `runs_on` differ.

## Entity 2: Release archive

A single archive file attached to the GitHub Release after the `attach-to-release` job runs.

| Field | Type | Notes |
|-------|------|-------|
| `version` | string (semver, no `v` prefix) | Extracted from the pushed tag via `${GITHUB_REF#refs/tags/}`. |
| `runtime` | string | Mirrors the matrix entry's `runtime`. |
| `variant` | enum: `standalone` \| `net8` \| `aot` | Maps from `deployment`: `self-contained → standalone`, `framework-dependent → net8`, `aot → aot`. |
| `archive_format` | enum: `.zip` \| `.tar.gz` | `.zip` for `runtime` starting `win-`; `.tar.gz` otherwise. |
| `filename` | string | Computed: `netpace-{version}-{runtime}-{variant}.{archive_format}`. |
| `contents` | set of file paths inside the archive | Variant-specific (see invariants). |

### Naming invariants

- Filename pattern is fixed by `docs/RELEASING.md` §Naming convention.
- For `variant = aot`, archive contains the single binary `NetPace` (Linux/macOS) or `NetPace.exe` (Windows). No `.dll`, `.deps.json`, `.runtimeconfig.json`, or `.pdb`.
- For `variant = standalone`, archive contains the framework-bundled publish output.
- For `variant = net8`, archive contains the framework-dependent publish output (small; requires .NET 8 runtime on host).

### Size invariant (relative)

For each `runtime` for which both variants exist:

```
sizeof(net8 archive) < sizeof(standalone archive)
sizeof(aot archive)  < sizeof(standalone archive)   -- when aot variant exists
```

Enforced by the `attach-to-release` job's "Verify framework-dependent and AOT binaries are smaller than standalone" step. See R9 — this feature widens the AOT branch of that check from "Linux only" to "Linux + Windows".

### After this feature: 16 archives total

| RID | `standalone` | `net8` | `aot` |
|-----|--------------|--------|-------|
| `win-x64` | ✓ | ✓ | **✓ (new)** |
| `win-arm64` | ✓ | ✓ | **✓ (new)** |
| `linux-x64` | ✓ | ✓ | ✓ |
| `linux-arm64` | ✓ | ✓ | ✓ |
| `osx-x64` | ✓ | ✓ | — |
| `osx-arm64` | ✓ | ✓ | — |

= 16 archives. Two added; existing 14 unchanged.

## Entity 3: Smoke gate

Per-job step that executes against the freshly-built archive on the matrix entry's runner.

| Field | Type | Notes |
|-------|------|-------|
| `applies_when` | predicate | `matrix.deployment == 'aot'`. |
| `commands` | ordered list of two | `./NetPace --version`, `./NetPace --help` (Linux/macOS); `./NetPace.exe --version`, `./NetPace.exe --help` (Windows, via Git Bash `shell: bash`). |
| `success_predicate` | bool | All commands exit `0`. |
| `failure_effect` | enum | Fails the matrix job; the dependent `attach-to-release` job does not run; no archives attached. |

### Invariant

Every AOT archive produced by the pipeline is gated by exactly one smoke step on its native runner. Cross-architecture smoke (e.g. running win-arm64 binary on x64 via emulation) is not allowed — explicitly rejected in R2.

## Relationships

```
Tag pushed
  ──► Workflow run
        ├─► For each Matrix entry:
        │     ├─► Publish step ──produces──► binary
        │     ├─► Archive step ─wraps────► Release archive
        │     └─► Smoke step (if aot) ─gates─► Release archive
        └─► attach-to-release job
              ├─► Size-assertion step ─validates─► all Release archives
              └─► Attach step ─publishes─► all Release archives to GitHub Release
```

This feature adds two new matrix-entry → release-archive paths and widens the size-assertion validation domain to include them.
