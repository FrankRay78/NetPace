# Installing NetPace

Every route below installs the same `netpace` command, which takes the same options and produces the same output whichever way you got it. `netpace --version` reports the release it came from.

## Windows: winget

```powershell
winget install netpace
```

- The package identifier is `FrankRay78.NetPace` (moniker `netpace`). Scripts should name the identifier exactly rather than rely on the moniker.
- You get the Native AOT build for your machine's architecture, x64 or ARM64. `netpace` is on your path from the next terminal you open.
- winget carries stable releases only, from the package's first acceptance onward, so not every version on the releases page is available to pin.
- NetPace's Windows binaries are not code-signed, so Windows SmartScreen may warn the first time it runs.

Upgrading, uninstalling and unattended installs work as for any winget package — see the [winget documentation](https://learn.microsoft.com/windows/package-manager/winget/).

## macOS and Linux: Homebrew

```bash
brew install FrankRay78/tap/netpace
```

- NetPace is served from its own tap, `FrankRay78/tap`, rather than homebrew-core; the command above adds the tap and installs the `netpace` formula in one step.
- You get the Native AOT build for your platform and architecture (macOS or Linux, Intel/x64 or ARM64), which needs no .NET runtime. `netpace` is on your path straight away.
- On macOS it runs without a Gatekeeper warning, even though NetPace's macOS binaries are not signed or notarised: Homebrew formula downloads do not carry the quarantine flag that a browser download does.
- The tap carries only the latest stable release, so no older version can be pinned through Homebrew. To pin one, use the [.NET tool](#any-platform-with-the-net-sdk-net-tool) or a [release binary](#any-platform-release-binaries).

Upgrading and uninstalling work as for any formula — see the [Homebrew documentation](https://docs.brew.sh/).

## Any platform with the .NET SDK: .NET tool

With the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) installed:

```bash
dotnet tool install -g netpace
```

- `netpace` is on your path provided the .NET global tools directory (`~/.dotnet/tools`, or `%USERPROFILE%\.dotnet\tools` on Windows) is; `dotnet tool install` tells you if it is not.
- `dotnet tool update -g netpace` moves to the latest release, and `dotnet tool install -g netpace --version <release>` pins any version from the [releases page](https://github.com/FrankRay78/NetPace/releases).
- `dnx netpace` runs NetPace once without installing it. The first run asks you to confirm the download, so pass `--yes` where nothing can answer, such as a CI job: `dnx --yes netpace -- --json`.
- **Platforms covered:** Windows, Linux and macOS, each on x64 and ARM64. All six get the Native AOT build, which needs no .NET runtime. Anything else — Alpine-style Linux among them — is not covered; use a standalone [release binary](#any-platform-release-binaries) instead.

## Any platform: release binaries

Every [release](https://github.com/FrankRay78/NetPace/releases) carries archives for Windows, Linux and macOS, on x64 and ARM64: download, unzip and run. Each comes in three variants:

| Variant | What it is |
|---|---|
| `aot` | Native AOT build; needs no .NET runtime and starts fastest |
| `standalone` | Bundles the .NET runtime; a large download that needs nothing installed |
| `net10` | A small download that needs the [.NET 10 runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) already installed |

On macOS, a browser download carries the quarantine flag, so the `aot` and `standalone` binaries raise a Gatekeeper warning the first time they run; NetPace's macOS binaries are not signed or notarised. Homebrew and the .NET tool avoid the warning.

## Building from source

Clone the repository and build it with the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0):

```bash
git clone https://github.com/FrankRay78/NetPace.git
cd NetPace
dotnet build src
```
