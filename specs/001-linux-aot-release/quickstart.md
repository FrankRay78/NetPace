# Quickstart: Building and Validating Linux AOT Locally

**Feature**: 001-linux-aot-release
**Audience**: contributors implementing or reviewing this feature.

This walks through producing a Linux AOT binary on a developer machine and validating the same gates the CI pipeline will enforce. It is the local equivalent of one matrix entry in `release-binaries.yml`.

---

## Prerequisites

- .NET 8 SDK (`dotnet --version` reports `8.0.x`).
- A Linux x64 host (or WSL2 on Windows). For ARM64 validation, an actual ARM64 Linux host is needed — cross-compilation works but the smoke test should run natively.
- Network access (for `netpace servers`).

---

## 1. Verify zero AOT analyzer warnings on a normal build

After implementing the csproj changes (`IsAotCompatible=true` on both projects):

```bash
dotnet build src/NetPace.sln -warnaserror:IL2026,IL2090,IL3050,IL3056
```

Expected: build succeeds, zero output for the listed codes.

If any of those codes fire, the offending source is not yet AOT-clean — fix before continuing.

---

## 2. Run the AOT publish for `linux-x64`

```bash
dotnet publish src/NetPace.Console/NetPace.Console.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained true \
  -p:PublishAot=true \
  -p:InvariantGlobalization=true \
  -p:WarningsAsErrors=IL2026,IL2090,IL3050,IL3056 \
  --output ./publish/linux-x64-aot
```

Expected: completes with exit code `0`.

Note: do **not** pass `-p:PublishSingleFile=true`. AOT already produces a single native ELF binary; the flag is redundant and can conflict.

---

## 3. Inspect the output

```bash
ls -la ./publish/linux-x64-aot/
file ./publish/linux-x64-aot/netpace
```

Expected:

- Exactly one executable file: `netpace`.
- `file` reports `ELF 64-bit LSB pie executable, x86-64, ...`.
- **No** `.dll` files in the output directory.
- **No** `.deps.json` file.

---

## 4. Run the smoke test

```bash
cd ./publish/linux-x64-aot
./netpace --version          # expect exit 0, prints version
./netpace --help             # expect exit 0, prints help
./netpace servers            # expect exit 0, prints server list
```

All three must exit `0`. If `servers` fails with an XML-parsing exception under AOT, `XmlExtensions` still has a reflection codepath — return to step 1.

---

## 5. Compare archive sizes

```bash
# After building self-contained variant the same way (without -p:PublishAot):
dotnet publish src/NetPace.Console/NetPace.Console.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  --output ./publish/linux-x64-standalone

# Compare:
du -sh ./publish/linux-x64-aot/
du -sh ./publish/linux-x64-standalone/
```

Expected: AOT publish output is materially smaller than the self-contained publish output.

---

## 6. Run unit tests

```bash
dotnet test src/NetPace.sln
```

Expected: all tests pass, including the new `XmlExtensionsTests` (Ookla XML parser) and the new `TimeSpanFormatterTests` (Humanizer replacement) added under this feature.

---

## 7. (Optional) Run the workflow locally with `act`

```bash
act push -W .github/workflows/release-binaries.yml --matrix runtime:linux-x64 --matrix deployment:aot
```

Caveat: `act` may not have an ARM64 image available; use it only for the x64 path locally.

---

## Troubleshooting

| Symptom | Likely cause |
|---------|--------------|
| `IL2026` on `XmlSerializer.Deserialize` | The `XmlExtensions` rewrite hasn't been applied yet. |
| `IL2026` on `Humanizer.*` | The `Humanizer` package and call sites haven't been removed yet. |
| `netpace servers` succeeds in normal build but throws under AOT | Hidden reflection path in the new XML parser — check for unintentional `XmlSerializer`/`DataContractSerializer` use, or LINQ providers that require dynamic code. |
| AOT archive larger than self-contained | Trimming likely failed. Inspect publish logs for `IL2104` (assembly is not trimmable) and add `<TrimmableAssembly>` entries or fix the offending dependency. |
| `dotnet publish` reports unsupported runtime for AOT | Cross-compilation of AOT requires extra toolchain. Build on a native host of the target architecture. |
