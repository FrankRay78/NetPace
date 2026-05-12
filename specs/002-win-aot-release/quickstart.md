# Quickstart: Verifying Feature 002 (Windows AOT Release)

**Feature**: 002-win-aot-release
**Date**: 2026-05-10

This document is the maintainer's runbook for verifying the feature end-to-end. It assumes implementation is complete on `002-win-aot-release` and the branch is being readied for a release-rehearsal tag.

## Prereqs

- Local clone of `FrankRay78/NetPace` on branch `002-win-aot-release` (or a PR branch built on top of it).
- Local .NET 8 SDK installed.
- Permission to push tags to the repo (release-rehearsal tag).

## Step 1: Local build sanity check (no Windows-specific path needed yet)

The clean-trim warning policy applies on every `dotnet build`, so any new IL2026 / IL2090 / IL3050 / IL3056 issue introduced anywhere in the codebase fails locally before you ever touch the workflow.

```powershell
dotnet build
dotnet test
```

Both must exit `0` with zero warnings. (Project memory: "Don't commit with failing tests or build warnings.")

## Step 2: Local AOT publish smoke (x64 only — ARM64 requires hardware)

This validates the AOT publish flow that the workflow will run on `windows-latest`. Run from the repo root in PowerShell on a Windows x64 machine:

```powershell
dotnet publish src/NetPace.Console/NetPace.Console.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output .\publish\win-x64-aot `
  -p:PublishAot=true `
  -p:InvariantGlobalization=true
```

Expected:
- Exit code `0`.
- `.\publish\win-x64-aot\NetPace.exe` exists.
- Running `.\publish\win-x64-aot\NetPace.exe --version` and `.\publish\win-x64-aot\NetPace.exe --help` both exit `0`.
- `.\publish\win-x64-aot\NetPace.pdb` also exists in the publish output — this is expected; the **archive** step is responsible for excluding it. Inspecting the staged archive after the workflow runs will confirm the `.pdb` is absent.

If you have a Windows-on-ARM device, repeat with `--runtime win-arm64`. Otherwise rely on the workflow to do this on `windows-11-arm`.

## Step 3: Push a release-rehearsal tag

Use a pre-release semver to avoid polluting the public release stream:

```powershell
rtk git tag 0.6.0-rc.0
rtk git push origin 0.6.0-rc.0
```

Watch the `release-binaries.yml` workflow run. Expected outcome:
- 16 matrix jobs run; all pass.
- The `attach-to-release` job runs; size-assertion step passes for all four AOT entries (Linux x64/arm64, Windows x64/arm64).
- 16 archives attach to the GitHub Release for tag `0.6.0-rc.0`.

## Step 4: Asset-level inspection

Download the two new archives and inspect:

```powershell
Invoke-WebRequest -Uri "https://github.com/FrankRay78/NetPace/releases/download/0.6.0-rc.0/netpace-0.6.0-rc.0-win-x64-aot.zip" -OutFile .\rehearsal-x64.zip
Expand-Archive .\rehearsal-x64.zip -DestinationPath .\rehearsal-x64
Get-ChildItem .\rehearsal-x64
```

Expected: a single `NetPace.exe`. No `.pdb`, `.dll`, `.deps.json`, or `.runtimeconfig.json`. Run `.\rehearsal-x64\NetPace.exe --version` to confirm end-to-end.

Repeat for `netpace-0.6.0-rc.0-win-arm64-aot.zip` if you have an ARM64 Windows device handy; otherwise rely on the in-job smoke gate.

## Step 5: No-regression check on existing 14 archives

For each of the 14 pre-existing archives, verify content equivalence to a comparable post-#176 release. The simplest hand-check:

```powershell
# Example for one archive
$old = "https://github.com/FrankRay78/NetPace/releases/download/{post-176-tag}/netpace-{post-176-tag}-linux-x64-aot.tar.gz"
$new = "https://github.com/FrankRay78/NetPace/releases/download/0.6.0-rc.0/netpace-0.6.0-rc.0-linux-x64-aot.tar.gz"
# Extract both, diff the file lists and `dotnet --info` outputs of the binary.
```

What is allowed to differ: the embedded version string in the binary (assembly metadata reflects the new tag). What must NOT differ: the file list, the binary entry-point, code paths, dependency versions.

## Step 6: Documentation parity check

After the workflow lands and before opening the PR for merge:

- `README.md` install table includes rows for `win-x64-aot.zip` and `win-arm64-aot.zip`.
- `docs/RELEASING.md` matrix table shows AOT cells filled for `win-x64` and `win-arm64`.
- `docs/RELEASING.md` runner-per-RID table includes rows for the two Windows AOT entries with their runners (`windows-latest`, `windows-11-arm`) and the rationale (native AOT cannot cross-compile across OS).
- `USER_GUIDE.md` AOT availability note mentions Windows alongside Linux.
- `CHANGELOG.md` is **not** edited (project doesn't have one — release notes are GitHub-auto-generated).

## Rollback

If the rehearsal tag fails:
- Inspect the failing matrix job.
- Common causes: `windows-11-arm` runner unavailable (re-queue or wait), trim warning regression introduced upstream of this branch (fix in code, retag), `.pdb` slipped into archive (fix archive step, retag).
- The rehearsal tag can be safely deleted from GitHub if you want to retry with the same number; production tags should never be rewound.

## Done criteria

This feature is verified when:
1. A release-rehearsal tag (or the next real semver tag) produces 16 archives.
2. Both new Windows AOT archives pass content + smoke + size invariants.
3. The 14 pre-existing archives remain identical in shape.
4. All four documentation files reflect the new artefacts.
