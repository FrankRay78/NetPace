# Contract: Release Matrix After Feature 002

**Feature**: 002-win-aot-release
**Type**: Pipeline-output contract (the public-facing assets a tagged release MUST produce)
**Date**: 2026-05-10

NetPace exposes no traditional API at the boundary touched by this feature. The "interface" being changed is the **set of release assets attached to each GitHub Release** — i.e. what end users see and download. This file is the binding contract on that surface.

## Inputs

- A semver tag pushed to `main` (e.g. `0.6.0`), no `v` prefix.
- Source state at that tag, which must include feature 001 (Linux AOT, post-#176) plus this feature (#177).

## Outputs (mandatory, ordered alphabetically)

The GitHub Release MUST have exactly the following 16 assets attached, where `{ver}` is the tag:

```
netpace-{ver}-linux-arm64-aot.tar.gz
netpace-{ver}-linux-arm64-net8.tar.gz
netpace-{ver}-linux-arm64-standalone.tar.gz
netpace-{ver}-linux-x64-aot.tar.gz
netpace-{ver}-linux-x64-net8.tar.gz
netpace-{ver}-linux-x64-standalone.tar.gz
netpace-{ver}-osx-arm64-net8.tar.gz
netpace-{ver}-osx-arm64-standalone.tar.gz
netpace-{ver}-osx-x64-net8.tar.gz
netpace-{ver}-osx-x64-standalone.tar.gz
netpace-{ver}-win-arm64-aot.zip            ← new
netpace-{ver}-win-arm64-net8.zip
netpace-{ver}-win-arm64-standalone.zip
netpace-{ver}-win-x64-aot.zip              ← new
netpace-{ver}-win-x64-net8.zip
netpace-{ver}-win-x64-standalone.zip
```

## Per-asset contracts

### `netpace-{ver}-win-x64-aot.zip` (new)

- **Built on**: `windows-latest`.
- **Archive format**: ZIP.
- **Contents**: exactly one entry — `NetPace.exe`. No `.dll`, no `.deps.json`, no `.runtimeconfig.json`, no `.pdb`, no other files.
- **Smoke gate (must pass on the build runner before upload)**: `NetPace.exe --version` exits `0`; `NetPace.exe --help` exits `0`.
- **Size invariant**: `sizeof(this) < sizeof(netpace-{ver}-win-x64-standalone.zip)`.

### `netpace-{ver}-win-arm64-aot.zip` (new)

- **Built on**: `windows-11-arm`.
- **Archive format**: ZIP.
- **Contents**: exactly one entry — `NetPace.exe`. No `.dll`, no `.deps.json`, no `.runtimeconfig.json`, no `.pdb`, no other files.
- **Smoke gate (must pass on the build runner before upload)**: `NetPace.exe --version` exits `0`; `NetPace.exe --help` exits `0`. Native execution — no x64 emulation.
- **Size invariant**: `sizeof(this) < sizeof(netpace-{ver}-win-arm64-standalone.zip)`.

### Existing 14 assets (regression contract)

For the same source state, each of the 14 pre-existing assets MUST be byte-identical to the comparable post-#176 release, modulo the tag-version substitution that the publish step performs (`-p:Version=…` etc.) which is unchanged behaviour. No matrix-entry edit, no archive-step edit, and no size-assertion edit may regress any of them.

## Failure modes

If any of the following occur, the `attach-to-release` job MUST NOT publish any asset:

1. Either Windows AOT smoke gate exits non-zero.
2. The `dotnet publish` for `win-x64-aot` or `win-arm64-aot` exits non-zero (e.g. trim/AOT warning escalated to error).
3. The `windows-11-arm` runner is unallocatable for the matrix job.
4. Either Windows AOT archive's size is `≥` its `-standalone` counterpart's size.
5. The Windows AOT archive contains anything other than the single `NetPace.exe` (verifiable post-implementation by an explicit content check or by hand-inspection on the first post-feature tag).

## Out of contract

- Code signing / Authenticode signature on `NetPace.exe`.
- `.pdb` distribution.
- macOS AOT assets (separate future feature).
- Renaming or removing any existing asset.
- Behaviour of `NetPace.exe` itself beyond what `--version` / `--help` exercise (covered by the regular xUnit test suite, not this release contract).
