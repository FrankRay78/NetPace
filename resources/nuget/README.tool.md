# NetPace

Cross-platform network speed testing CLI. Measures latency, download and upload speed against Ookla Speedtest servers.

> **NetPace is pre-1.0.** Any release may change or remove command-line options or output formats, without a deprecation period. Backward compatibility will be maintained from 1.0.0 onwards.

Fan of NetPace? Consider giving the [repo](https://github.com/FrankRay78/NetPace) a star ⭐ - it helps others find it too.

## Install

```bash
dotnet tool install -g NetPace
```

Then run a speed test:

```bash
netpace
```

Update to the latest release, or run NetPace once without installing it:

```bash
dotnet tool update -g NetPace
dnx NetPace
```

On Windows and Linux (x64 and ARM64) the installed command is a Native AOT binary, so it starts without a .NET runtime. On macOS it runs on the .NET runtime the SDK already provides. Platforms outside those six — Alpine-style Linux among them — are not covered by this package; the [releases page](https://github.com/FrankRay78/NetPace/releases) carries standalone downloads instead.

## Usage

```bash
netpace --profile tiny        # ≤ 1 MiB total, for metered or IoT connections
netpace --minimal             # the result as a single compact line
netpace --json                # a JSON object, for scripting
netpace servers               # list the nearest speed test servers
netpace --help                # detailed usage instructions
```

See the [User Guide](https://github.com/FrankRay78/NetPace/blob/main/USER_GUIDE.md) for scripting, output formats, profiles and exit codes.

Ookla and Speedtest are trademarks of Ookla, LLC; this project is not affiliated with or endorsed by Ookla.

## Using NetPace from your own code

The same engine is published as a library: [NetPace.Core](https://www.nuget.org/packages/NetPace.Core).
