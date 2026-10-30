# NetPace

[![NuGet](https://img.shields.io/nuget/v/NetPace.Core.svg)](https://www.nuget.org/packages/NetPace.Core/)
[![Build & Tests](https://github.com/FrankRay78/NetPace/actions/workflows/dotnet.yml/badge.svg)](https://github.com/FrankRay78/NetPace/actions/workflows/dotnet.yml)

Network speed tester including server discovery, latency measurement, download and upload speed testing.

Built with .NET 10.0 — runs on Windows, Linux, and macOS.

> [!IMPORTANT]\
> **NetPace is pre-1.0.** Any release may change or remove command-line options, output formats or `NetPace.Core` library members, without a deprecation period. Backward compatibility will be maintained from 1.0.0 onwards.

<p align="left">
    <a href="https://github.com/FrankRay78/NetPace/issues/new?labels=needs%20triage,bug&template=bug-report---.md">Report Bug</a>
    -
    <a href="https://github.com/FrankRay78/NetPace/issues/new?labels=needs%20triage,enhancement&template=feature-request---.md">Request Feature</a>
</p>

Fan of NetPace? Consider giving the [repo](https://github.com/FrankRay78/NetPace) a star ⭐ - it helps others find it too.

<br />


## Features

* Server discovery, latency measurement, download and upload speed testing.
* Command-line application and C# Microsoft .Net [NuGet](https://www.nuget.org/packages/NetPace.Core/) library for developers.
* User configurable output (eg. SI or IEC units, bits or bytes, CSV and Json formats).
* Highly configurable traffic profiles (see [LatencyTestSettings](https://github.com/FrankRay78/NetPace/blob/main/src/NetPace.Core/Clients/Ookla/Settings/LatencyTestSettings.cs), [DownloadTestSettings](https://github.com/FrankRay78/NetPace/blob/main/src/NetPace.Core/Clients/Ookla/Settings/DownloadTestSettings.cs) and [UploadTestSettings](https://github.com/FrankRay78/NetPace/blob/main/src/NetPace.Core/Clients/Ookla/Settings/UploadTestSettings.cs)).
* Highly reliable, utilises [Ookla's](https://www.speedtest.net/) Speedtest servers.

<br />


## About The Project
A cross-platform command-line application for performing network speed tests, including server discovery, latency measurement, download and upload speed testing. The core speed test library, `NetPace.Core`, has been designed for developer use and can be installed via [NuGet](https://www.nuget.org/packages/NetPace.Core/).

NetPace is not affiliated with or endorsed by Ookla or [Speedtest by Ookla](https://www.speedtest.net/) in any way, although their servers are used by the default speed test provider.

The obligatory screenshot (as of 29 August 2025):

<img width="1115" height="628" alt="NetPace_Screenshot_2025-08-29" src="https://github.com/user-attachments/assets/e4bced3d-04a9-47a9-8ba5-fd6ef9390833" />

<br />


## Background
This project came out of my time as the [Spectre.Console](https://github.com/spectreconsole/spectre.console) CLI sub-system maintainer, having never actually used the library for myself. I wanted to gain practical experience by developing a command-line application, following best practices such as the [Command Line Interface Guidelines](https://clig.dev/), and then applying that experience in my maintainer role. This is also known as 'dogfooding' in the tech industry ie. using your own product before expecting others to do the same.

I am no longer the `Spectre.Console` CLI sub-system maintainer, but this project continues to be well supported.

<br />


## Installation

* Windows: `winget install netpace`
* macOS and Linux: `brew install FrankRay78/tap/netpace`
* Any platform with the .NET 10 SDK: `dotnet tool install -g netpace`
* Or download a binary from the [releases page](https://github.com/FrankRay78/NetPace/releases)

See [INSTALLATION.md](INSTALLATION.md) for platform notes and which build each route gives you.

<br />


## Usage

For most users, running a simple speed test is as easy as:

```bash
netpace
```

On a metered or IoT connection? Use the `tiny` profile (≤ 1 MiB total per run):

```bash
netpace --profile tiny
```

For common scenarios and advanced usage (such as scripting, restricting payload size, choosing a traffic profile, or customising output), see the [User Guide](https://github.com/FrankRay78/NetPace/blob/main/USER_GUIDE.md).

`netpace --help` will display detailed usage instructions.

```txt
C:\>netpace.exe --help

    _   __         __     ____
   / | / /  ___   / /_   / __ \  ____ _  _____  ___
  /  |/ /  / _ \ / __/  / /_/ / / __ `/ / ___/ / _ \
 / /|  /  /  __// /_   / ____/ / /_/ / / /__  /  __/
/_/ |_/   \___/ \__/  /_/      \__,_/  \___/  \___/


DESCRIPTION:
Network speed tester including server discovery, latency measurement, download and upload speed testing.

USAGE:
    netpace [OPTIONS] [COMMAND]

OPTIONS:
                           DEFAULT
    -h, --help                        Prints help information.
    -v, --version                     Prints version information.
        --loop                        Performs the speed test on continuous loop.
        --count                       Stop speed testing after this many times.
        --delay                       Time between multiple speed tests (HH:MM:SS).
        --csv                         Display output as a single CSV row (always includes timestamp).
        --no-csv-header               Do not write the header row (CSV output only).
                                      An appended --file target that already holds rows is never sent the header.
        --json                        Display output in Json format.
        --json-pretty                 Display output in Json format (pretty print).
        --minimal                     Display the result as a single compact line.
                                      Ideal for batch scripts and redirected output.
        --no-latency                  Do not perform latency test.
                                      When used without --server, the first available server is selected.
        --no-download                 Do not perform download test.
        --no-upload                   Do not perform upload test.
        --server                      The URL of a specific speed test server.
                                      'netpace servers -l' will return your nearest servers.
    -t, --timestamp                   Include a timestamp in the output.
        --profile          medium     Profile bundle of payload settings. <tiny, small, medium, large, mega>
        --downloadsize                Stop the download test after this many megabytes (IEC MiB).
        --uploadsize                  Stop the upload test after this many megabytes (IEC MiB).
        --connections                 Maximum transfers the download and upload tests each run at once (1-32).
                                      Overrides the --profile connection count; the profile's data caps still apply.
                                      A maximum, so a profile with fewer transfers to run than this will not reach it.
    -u, --unit             bits       The speed unit. <bits, bytes>
                                      Applies to the default and minimal output.
        --unit-scale       auto       The speed unit scale. <auto, base, kilo, mega, giga, tera, peta>
                                      Applies to the default and minimal output.
        --unit-system      SI         The speed unit system. <SI, IEC>
                                      Applies to the default and minimal output.
                                      SI steps up in powers of 1000 (KB, MB, GB), common in networking, 
                                      while IEC uses powers of 1024 (KiB, MiB, GiB), standard in computing and storage.
        --device-id                   Record a label for this machine in CSV and JSON output.
                                      The CSV column is always present; the JSON field appears only when a label is given.
        --custom-field                Record a key=value pair in CSV and JSON output.
                                      May be given more than once, and is recorded in the order given.
    -f, --file                        Write output to file.
        --file-mode        append     Determines file output behavior. <append, overwrite>
    -q, --quiet                       Suppress all normal console output (file output still works).
        --fail-on          none       Exit with a non-zero code on a failed measurement. <none, total, partial>
                                      With 'none' the exit code is never affected; 'total' triggers when a test is all-failed;
                                      'partial' triggers on any failed request. Fail-fast across --count and --loop.
        --diagnostics                 Write per-request diagnostic detail about the run to the error stream.
                                      The result is unchanged, so 'netpace --json --diagnostics > result.json 2> diagnostics.log' keeps the two apart.

COMMANDS:
    servers    Show the nearest speed test servers.

SEE ALSO:
    https://github.com/FrankRay78/NetPace/blob/main/USER_GUIDE.md
```

<br />


## Developer Use

Want to integrate network speed testing into your own app?

Install the core library via NuGet:

```bash
dotnet add package NetPace.Core
```

Then use the `ISpeedTestService` interface:

```csharp
using NetPace.Core;
using NetPace.Core.Clients.Ookla;

var speedTester = new OoklaSpeedtest() as ISpeedTestService;

var servers = await speedTester.GetServersAsync();
var fastest = await speedTester.GetFastestServerByLatencyAsync(servers);

var downloadResult = await speedTester.GetDownloadSpeedAsync(fastest.Server);
var uploadResult = await speedTester.GetUploadSpeedAsync(fastest.Server);

Console.WriteLine($"{fastest.Server.Sponsor} ({fastest.LatencyMilliseconds} ms)");
Console.WriteLine($"Download: {downloadResult.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI)}");
Console.WriteLine($"Upload: {uploadResult.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI)}");
```

The example above uses the `OoklaSpeedtest` implementation which uses Ookla Speedtest servers under the hood. Ookla and Speedtest are trademarks of Ookla, LLC; this project is not affiliated with or endorsed by Ookla.

See the [ISpeedTestService](https://github.com/FrankRay78/NetPace/blob/main/src/NetPace.Core/ISpeedTestService.cs) interface for full method details and overloads.

[Example Console App](https://github.com/FrankRay78/NetPace/tree/main/examples/ConsoleApp/Program.cs) is a minimal usage example, and [NetPace command-line application](https://github.com/FrankRay78/NetPace/tree/main/src/NetPace.Console) is an extensive, production-quality usage example.

<br />


### Testing Your Code

NetPace.Core includes test implementations of `ISpeedTestService` so you can test your code without making real network calls. They are a supported part of the library: each double's documented behaviour is covered by its own tests, so code you build on one keeps working from release to release.

- **`SpeedTestStub`** - Simple stub returning fixed values with configurable delays
- **`SpeedTestMock`** - Fully configurable mock with injectable delegate functions
- **`VariableSpeedTester`** - Returns different speeds on each call (simulates variable network conditions)
- **`FaultySpeedTester`** - Simulates network failures and timeouts (for testing error handling)
- **`DelayProviderStub`** - Stands in for `IDelayProvider`, recording the pauses your code asked for instead of waiting them out

See the implementations in [NetPace.Core.Clients.Testing](https://github.com/FrankRay78/NetPace/tree/main/src/NetPace.Core/Clients/Testing) and their usage throughout the solution.

<br />


## Contributing
> [!IMPORTANT]\
> I'm not currently accepting pull requests for this project. 

You can contribute by [opening a new issue](https://github.com/FrankRay78/NetPace/issues/new/choose) or commenting on existing ones, and you are most welcome to fork the repository for your own purposes. 

But please **don't be offended** if I close or delete issues as I see fit.

<br />


## License
Distributed under the MIT license. See `LICENSE` for more information.
