# Contract: Release Archives

**Feature**: 001-linux-aot-release
**Audience**: end users, install scripts, package maintainers (Homebrew/AUR/etc.), automation that watches GitHub Releases.
**Stability**: pre-existing 12 archives — stable. Two new AOT archives — stable from first release that includes them.

---

## Filename schema

```
netpace-{version}-{runtime_id}-{variant}.{archive_format}
```

| Token | Allowed values |
|-------|----------------|
| `version` | semver tag, no `v` prefix (e.g. `0.6.0`). |
| `runtime_id` | `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`. |
| `variant` | `standalone`, `net8`, `aot`. |
| `archive_format` | `zip` for `win-*`, `tar.gz` for everything else. |

`variant=aot` is permitted **only** with `runtime_id ∈ { linux-x64, linux-arm64 }` in the current scope.

---

## Per-release archive set (post-feature)

For tag `{V}`, the following 14 archives MUST be attached to the GitHub Release:

| # | Filename |
|---|----------|
| 1 | `netpace-{V}-win-x64-standalone.zip` |
| 2 | `netpace-{V}-win-x64-net8.zip` |
| 3 | `netpace-{V}-win-arm64-standalone.zip` |
| 4 | `netpace-{V}-win-arm64-net8.zip` |
| 5 | `netpace-{V}-linux-x64-standalone.tar.gz` |
| 6 | `netpace-{V}-linux-x64-net8.tar.gz` |
| 7 | `netpace-{V}-linux-arm64-standalone.tar.gz` |
| 8 | `netpace-{V}-linux-arm64-net8.tar.gz` |
| 9 | `netpace-{V}-osx-x64-standalone.tar.gz` |
| 10 | `netpace-{V}-osx-x64-net8.tar.gz` |
| 11 | `netpace-{V}-osx-arm64-standalone.tar.gz` |
| 12 | `netpace-{V}-osx-arm64-net8.tar.gz` |
| **13** | **`netpace-{V}-linux-x64-aot.tar.gz`** *(NEW)* |
| **14** | **`netpace-{V}-linux-arm64-aot.tar.gz`** *(NEW)* |

Names #1–#12 are unchanged from prior releases.

---

## Archive contents

### `-standalone` (existing)
- Single-file `.NET 8` self-contained executable + supporting files (unchanged).

### `-net8` (existing)
- Framework-dependent build; requires .NET 8 runtime on host (unchanged).

### `-aot` (new)
- **Exactly one** native ELF executable (`netpace`).
- **No** `.dll` files.
- **No** embedded .NET runtime.
- **No** `.deps.json` file.
- **No** ICU data companions (`InvariantGlobalization=true`).
- File mode: executable (`+x`) on the file inside the tar.

---

## Size invariant (per release)

For each `runtime_id ∈ { linux-x64, linux-arm64 }`:

```
size(netpace-{V}-{runtime_id}-aot.tar.gz)
  < size(netpace-{V}-{runtime_id}-standalone.tar.gz)
```

Existing invariant retained:
```
size(netpace-{V}-{runtime_id}-net8.{ext})
  < size(netpace-{V}-{runtime_id}-standalone.{ext})
```

Both invariants are enforced by the release pipeline; violation fails the release.

---

## Smoke-test gate (AOT only)

Each AOT archive must, on its native runner, satisfy:

| Command | Expected exit code |
|---------|--------------------|
| `./netpace --version` | `0` |
| `./netpace --help` | `0` |
| `./netpace servers` | `0` |

`netpace servers` exercises HTTPS + XML parsing end-to-end. Failure fails the release.

---

## Backwards compatibility

- No existing filename is renamed.
- No existing archive is removed.
- No existing archive contents change (size may drift slightly with toolchain updates, as today).
- Consumers pinning to existing names continue to work without modification.

Future deprecation of `-standalone` or `-net8`, or rename of suffixes, is **out of scope** for this feature.
