# Linux Native AOT Release Artifacts

**Intent:** Ship two new release archives — `netpace-{ver}-linux-x64-aot.tar.gz` and `netpace-{ver}-linux-arm64-aot.tar.gz` — containing single native ELF binaries that run on Linux IoT/embedded hosts without a .NET runtime; advertise AOT compatibility on the `NetPace.Core` NuGet package; preserve the existing 12 archive variants byte-identically.

**Behaviour:**
- Given: a semver tag is pushed
- When: `release-binaries.yml` runs to completion
- Then: the GitHub Release contains exactly 14 assets — the 12 pre-existing variants (unchanged filenames + contents) plus `netpace-{tag}-linux-x64-aot.tar.gz` and `netpace-{tag}-linux-arm64-aot.tar.gz`. Each AOT archive contains a single ELF executable, no `.dll`, no `.deps.json`, no `runtimes/` subdirectory. Smoke tests (`./netpace --version | --help | servers`) on each AOT archive's native runner all exit 0; AOT archive size is strictly less than the matching `-standalone` archive size per RID.
- Given: an AOT consumer references the published `NetPace.Core` package and builds with `dotnet publish -p:PublishAot=true -warnaserror:IL2026,IL2090,IL3050,IL3056`
- When: the consumer's build runs
- Then: the build completes with exit 0 and zero AOT/trim warnings reference any `NetPace.Core.*` symbol.

**Constraints:**
- `IsAotCompatible=true` must be a per-project property in `NetPace.Core.csproj` and `NetPace.Console.csproj`. No `Directory.Build.props`. No static `<PublishAot>` in either csproj — AOT is opt-in via the workflow's `-p:PublishAot=true` flag so dev-machine builds remain non-AOT.
- The 12 pre-existing matrix combinations must execute identical `dotnet publish` flags before and after this change. Both matrix axes (`runtime` and `deployment`) must remain byte-identical; AOT entries are added only via `matrix.include:`.
- AOT cross-compilation across operating systems is not supported — Linux AOT must be built on a Linux runner natively. ARM64 must run on `ubuntu-24.04-arm` (smoke test must run natively, not under QEMU).
- `OoklaServer` and `OoklaServerList` keep all public members; only the `[XmlAttribute]`/`[XmlRoot]`/`[XmlArray]`/`[XmlArrayItem]` decorations are removed (observable to a hypothetical consumer who reflects over the types or runs their own `XmlSerializer` against them — documented break, no rename or signature change).
- `publish-nuget.yml` must be byte-identical before and after the change.
- `dotnet build src/NetPace.sln` must emit zero warnings for `IL2026`/`IL2090`/`IL3050`/`IL3056` codes.

**Decisions:**

1. **AOT-safe XML parser**: rewrite `XmlExtensions.DeserializeFromXml<OoklaServerList>` using `XDocument` + `XmlReader` + explicit `CultureInfo.InvariantCulture` numeric parsing.
   - Rejected: `System.Text.Json` source generator — Ookla wire format is XML, not JSON.
   - Rejected: `IXmlSerializable` / `DataContractSerializer` — both still drag reflection codepaths under AOT.
   - Rejected: Roslyn source generator — overkill for one type; hand-rolled parser fits in <80 LOC.
   - Chose: BCL `XDocument`/`XmlReader` — zero new dependencies, fully AOT-safe, readable.

2. **Humanizer replacement**: introduce internal `TimeSpanFormatter.Humanize(this TimeSpan)` in `NetPace.Console.ConsoleWriters` and remove the `Humanizer` package.
   - Rejected: keep Humanizer with AOT-suppression annotations — leaves IL2026 warnings in place; suppressions accumulate over time.
   - Rejected: `TimeSpan.ToString("g")` — produces unfriendly `"0:00:01.2345678"` output.
   - Chose: hand-rolled formatter mirroring `Humanizer.TimeSpan.Humanize(precision: 1)` for 0–600 seconds, the only range NetPace produces.

3. **Source-generated JSON for `JsonConsoleWriter`** (not in the original task list — discovered during the FR-008 zero-warning gate): switch from reflection-based `JsonSerializer.Serialize<T>(value, options)` to two source-generated `JsonSerializerContext` partials (`JsonResultCompactContext`, `JsonResultIndentedContext`).
   - Rejected: a single context with runtime-built options — would require building a `JsonTypeInfo` from a context's options at runtime, defeating AOT. Two contexts pay a tiny code-size cost for full AOT compliance.
   - Chose: two source-gen contexts, runtime branch on `settings.JsonPretty`.

4. **Two `matrix.include:` entries** rather than a third matrix dimension.
   - Rejected: `variant: [self-contained, framework-dependent, aot]` cross-product with `runtime` — produces nonsensical combinations (e.g. `osx-arm64 × aot`) requiring `exclude:` entries; obscures intent.
   - Rejected: separate parallel job — duplicates checkout/setup-dotnet/extract-version steps.
   - Chose: matrix include + `runs-on: ${{ matrix.runs_on || 'ubuntu-latest' }}` so existing entries inherit the default and AOT entries can override.

5. **Per-RID relative size assertion (`aot < standalone`)** rather than absolute thresholds — mirrors the existing `framework-dependent < self-contained` check; no maintenance burden as .NET evolves.

6. **No support for cross-OS native compilation** — local AOT validation on a Windows dev box is acknowledged as out of scope; CI's native runners are the validation gate (smoke tests + size assertion). _(Note: this CIR is scoped to Linux AOT only. Windows AOT — `win-x64-aot` and `win-arm64-aot` — is added later under [`specs/002-win-aot-release/`](../../specs/002-win-aot-release/); the cross-OS-cannot-cross-compile rationale carried over unchanged.)_

**Date:** 2026-05-02
