# Contract: `NetPace.Core` NuGet Package Metadata

**Feature**: 001-linux-aot-release
**Audience**: NuGet consumers of `NetPace.Core`, especially those publishing AOT-enabled .NET applications.
**Stability**: SemVer MINOR — additive metadata signal, no source-breaking change to public types.

---

## Metadata change

| Property | Before | After |
|----------|--------|-------|
| `IsAotCompatible` | unset | `true` |

When set, the property:

- is reflected in the produced `.nupkg` (NuGet client tooling reads it to decide whether to surface AOT-compatibility warnings to consumers);
- activates the AOT/trim warning analyzers (`IL2026`, `IL2090`, `IL3050`, `IL3056`) during `dotnet build`, on the library project itself.

---

## Public type surface

### Unchanged
- All public types, members, and signatures in `NetPace.Core` are preserved.
- `OoklaServer` and `OoklaServerList` remain `public sealed class` with their existing properties.
- `ISpeedTestService` and result-object types unchanged.

### Behavioural — XML attribute decorations removed
The following attributes are removed from `OoklaServer` and `OoklaServerList`:

- `[XmlRoot("settings")]` (on `OoklaServerList`)
- `[XmlArray("servers")]`, `[XmlArrayItem("server")]` (on `OoklaServerList.Servers`)
- `[XmlAttribute("...")]` (on each property of `OoklaServer`)

**Impact on consumers**:

- Consumers who simply use `OoklaServer` / `OoklaServerList` as data carriers — **unaffected**.
- Consumers who reflect over the attribute set via `typeof(OoklaServer).GetCustomAttributes(...)` — **affected**: attributes will no longer be present.
- Consumers running their own `XmlSerializer.Deserialize<OoklaServerList>(...)` against an Ookla XML response — **affected**: deserialization will silently produce empty/default values because the XML mapping metadata is gone.

This is a deliberate trade-off: NetPace's own consumer surface for these types is data-only; they were never documented for external `XmlSerializer` use. The CIR captures this decision.

---

## Compatibility matrix

| Consumer scenario | Before this feature | After |
|-------------------|---------------------|-------|
| Reference `NetPace.Core` from a non-AOT app | works | works (unchanged) |
| Reference `NetPace.Core` from an AOT-published app | warnings: `IL2026`/`IL3050` from `XmlSerializer` path | clean — zero AOT-related warnings originating from `NetPace.Core` |
| Use `OoklaServer` / `OoklaServerList` as data POCOs | works | works (unchanged) |
| Reflect over `[XmlAttribute]` decoration | sees attributes | does not see attributes |
| Run own `XmlSerializer<OoklaServerList>` against Ookla XML | works (matched element/attribute names) | does not work (no decorations) |

Last row: undocumented usage; no known consumer relies on it. Captured in CIR for transparency.

---

## NuGet workflow

`publish-nuget.yml` is **unchanged**. Setting `IsAotCompatible=true` in `NetPace.Core.csproj` is sufficient — the existing `dotnet pack` invocation propagates the property into the `.nupkg` automatically.

---

## Versioning

- The metadata addition is **MINOR** (new capability advertised).
- The XML-attribute removal is **technically a behavioural change**; SemVer treatment depends on how NetPace.Core's public API contract treats undocumented serializer compatibility. Recommended treatment: MINOR, documented in CHANGELOG and CIR. If a consumer is found to depend on it, escalate to MAJOR.
