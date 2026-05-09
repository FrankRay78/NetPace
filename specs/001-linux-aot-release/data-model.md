# Phase 1 Data Model: Linux Native AOT Release Artifacts

**Feature**: 001-linux-aot-release
**Date**: 2026-05-01

This feature is largely build/release infrastructure with one internal-data-shape change (XML parsing). Entities below are the *external/contract-bearing* objects that the change must preserve or extend.

---

## E-1: Release Archive

A downloadable artefact attached to a GitHub Release.

| Field | Type | Description |
|-------|------|-------------|
| `version` | string (semver) | Tag name without `v` prefix (existing convention). |
| `runtime_id` | enum: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64` | .NET runtime identifier. |
| `variant` | enum: `standalone` (self-contained), `net8` (framework-dependent), **`aot`** (NEW, Linux-only) | Build variant suffix. |
| `archive_format` | enum: `.zip` (Windows), `.tar.gz` (Linux/macOS) | Existing convention preserved. |
| `filename` | derived: `netpace-{version}-{runtime_id}-{variant}.{archive_format}` | Example: `netpace-0.6.0-linux-arm64-aot.tar.gz`. |
| `contents` | enum: `dotnet-bundle` (existing variants), **`single-elf`** (AOT) | AOT archives contain exactly one native ELF executable; no `.dll`, no embedded runtime, no `.deps.json`. |

**Validation rules (enforced in workflow)**:

- For `variant == aot`: `runtime_id ∈ { linux-x64, linux-arm64 }` only (Windows/macOS AOT out of scope).
- For `variant == aot`: archive size MUST be strictly less than the matching `(runtime_id, variant=standalone)` archive size.
- For all variants on Linux: `archive_format == .tar.gz`.
- 14 archives MUST exist per release (12 pre-existing + 2 new).

**State transitions**: archives are immutable once attached to a release. No state machine.

---

## E-2: Release Matrix Entry

One configuration row in `release-binaries.yml` describing a single (RID × variant × runner) build.

| Field | Type | Default (existing entries) | New AOT entries |
|-------|------|----------------------------|-----------------|
| `runtime` | string (RID) | `win-x64` … `osx-arm64` | `linux-x64`, `linux-arm64` |
| `deployment` | string | `self-contained` or `framework-dependent` | `aot` |
| `runs_on` | string | `ubuntu-latest` (job-level inherit) | `ubuntu-latest` (x64), `ubuntu-24.04-arm` (arm64) |
| `publish_aot` | bool | `false` (omitted in csproj/CLI) | `true` (`-p:PublishAot=true`) |
| `publish_single_file` | bool | `true` | `false` (AOT already produces single binary) |
| `invariant_globalization` | bool | `false` | `true` (`-p:InvariantGlobalization=true`) |
| `self_contained_flag` | bool | derived from `deployment` | `true` |
| `archive_suffix` | string | `-standalone` or `-net8` | `-aot` |

**Existing 12 entries stay byte-identical.** The two new entries are added via `matrix.include:` to avoid mutating the existing grid.

---

## E-3: NuGet Package Metadata (`NetPace.Core`)

| Field | Before | After |
|-------|--------|-------|
| `IsAotCompatible` | unset | **`true`** |
| `PackageId` | `NetPace.Core` | unchanged |
| `Description`, `Authors`, etc. | unchanged | unchanged |
| Public type surface | (current) | unchanged — `OoklaServer` and `OoklaServerList` keep all members; `[XmlAttribute]`/`[XmlRoot]`/`[XmlArray]`/`[XmlArrayItem]` decorations removed. |

**Note**: removing the XML attribute decorations is observable to a NuGet consumer who reflects over the types or runs their own `XmlSerializer` against them. This is a behavioural break for any such hypothetical consumer. Documented in CIR. The decorations had no role beyond NetPace's own internal deserializer call.

---

## E-4: Ookla XML Wire Format (consumed; not produced)

The XML response from the Ookla server-discovery endpoint. Not owned by NetPace; documented for parser-rewrite reference.

```xml
<?xml version="1.0" encoding="UTF-8"?>
<settings>
  <servers>
    <server id="1234" name="London" country="United Kingdom"
            sponsor="ISP Name" host="speedtest.example.com:8080"
            url="http://speedtest.example.com/speedtest/upload.php"
            lat="51.5074" lon="-0.1278" />
    <!-- repeated; ~500 entries typical -->
  </servers>
</settings>
```

| Element / Attribute | Maps to | Required? |
|---------------------|---------|-----------|
| `<settings>` | (root) | Required |
| `<settings><servers>` | `OoklaServerList.Servers` (collection) | Required (may be empty) |
| `<server id>` | `OoklaServer.Id` (int) | Required |
| `<server name>` | `OoklaServer.Location` (string) | Required |
| `<server country>` | `OoklaServer.Country` (string?) | Optional |
| `<server sponsor>` | `OoklaServer.Sponsor` (string) | Required |
| `<server host>` | `OoklaServer.Host` (string?) | Optional |
| `<server url>` | `OoklaServer.Url` (string) | Required |
| `<server lat>` | `OoklaServer.Latitude` (double, invariant-culture) | Required |
| `<server lon>` | `OoklaServer.Longitude` (double, invariant-culture) | Required |

**Parser invariants**:
- Numeric attributes (`id`, `lat`, `lon`) parsed with `CultureInfo.InvariantCulture`.
- Missing optional attributes → `null`. Missing required attributes → throw.
- Element ordering inside `<server>` is irrelevant (attributes-only).

---

## E-5: Smoke Test Outcome

| Field | Type | Constraint |
|-------|------|------------|
| `archive_path` | string | Path to extracted AOT binary. |
| `command` | enum: `--version`, `--help`, `servers` | Three commands run sequentially. |
| `exit_code` | int | MUST be `0` for the release job to succeed. |

**Failure mode**: any non-zero exit fails the matrix job → fails attach-to-release → no archive is attached.
