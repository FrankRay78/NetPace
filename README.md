# NetPace

[![NuGet](https://img.shields.io/nuget/v/NetPace.Core.svg)](https://www.nuget.org/packages/NetPace.Core/)
[![Build & Tests](https://github.com/FrankRay78/NetPace/actions/workflows/dotnet.yml/badge.svg)](https://github.com/FrankRay78/NetPace/actions/workflows/dotnet.yml)

Network speed tester including server discovery, latency measurement, download and upload speed testing.

Built with .NET 8.0 — runs on Windows, Linux, and macOS.

<p align="left">
    <a href="https://github.com/FrankRay78/NetPace/issues/new?labels=needs%20triage,bug&template=bug-report---.md">Report Bug</a>
    -
    <a href="https://github.com/FrankRay78/NetPace/issues/new?labels=needs%20triage,enhancement&template=feature-request---.md">Request Feature</a>
</p>

<br />


## Features

* Server discovery, latency measurement, download and upload speed testing.
* Command-line application and C# Microsoft .Net [NuGet](https://www.nuget.org/packages/NetPace.Core/) library for developers.
* User configurable output (eg. SI or IEC units, BitsPerSecond or BytesPerSecond, CSV and Json formats).
* Highly configurable traffic profiles (see [LatencyTestSettings](https://github.com/FrankRay78/NetPace/blob/main/src/NetPace.Core/Clients/Ookla/Settings/LatencyTestSettings.cs), [DownloadTestSettings](https://github.com/FrankRay78/NetPace/blob/main/src/NetPace.Core/Clients/Ookla/Settings/DownloadTestSettings.cs) and [UploadTestSettings](https://github.com/FrankRay78/NetPace/blob/main/src/NetPace.Core/Clients/Ookla/Settings/UploadTestSettings.cs)).
* Highly reliable, utilises [Ookla's](https://www.speedtest.net/) Speedtest servers.

<br />


## About The Project
A cross-platform command-line application for performing network speed tests, including server discovery, latency measurement, download and upload speed testing. 
The core speed test library, `NetPace.Core`, has been designed for developer use and can be installed via [NuGet](https://www.nuget.org/packages/NetPace.Core/).

NetPace is not affiliated with or endorsed by Ookla or [Speedtest by Ookla](https://www.speedtest.net/) in any way, although their servers are used by the default speed test provider.

The obligatory screenshot (as of 29 August 2025):

<img width="1115" height="628" alt="NetPace_Screenshot_2025-08-29" src="https://github.com/user-attachments/assets/e4bced3d-04a9-47a9-8ba5-fd6ef9390833" />

<br />


## Background
This project came out of my time as the [Spectre.Console](https://github.com/spectreconsole/spectre.console) CLI sub-system maintainer, having never actually used the library for myself. I wanted to gain practical experience by developing a command-line application, following best practices such as the [Command Line Interface Guidelines](https://clig.dev/), and then applying that experience in my maintainer role. This is also known as 'dogfooding' in the tech industry ie. using your own product before expecting others to do the same.

I am no longer the `Spectre.Console` CLI sub-system maintainer, but this project continues to be well supported.

<br />


## Getting Started
Each release contains precompiled binaries you can simply [download](https://github.com/FrankRay78/NetPace/releases), unzip and run. 

Choose from Windows, Linux and macOS; standalone (large file but includes all dependencies), net8 (small file but requires [Microsoft .NET 8.0 runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)) to already be installed, or AOT compiled for bare metal execution (Linux only). 

Alternatively, clone this repository locally and build.

Developed with Microsoft .NET 8.0 on Windows 10 using Visual Studio 2022 Community. Other modern environments should work fine.

<br />


## Usage

For most users, running a simple speed test is as easy as:

```bash
NetPace
```

For common scenarios and advanced usage (such as scripting, restricting payload size, or customising output), see the [User Guide](https://github.com/FrankRay78/NetPace/blob/main/USER_GUIDE.md).

`NetPace --help` will display detailed usage instructions.

```txt
C:\>NetPace.exe --help

    _   __         __     ____
   / | / /  ___   / /_   / __ \  ____ _  _____  ___
  /  |/ /  / _ \ / __/  / /_/ / / __ `/ / ___/ / _ \
 / /|  /  /  __// /_   / ____/ / /_/ / / /__  /  __/
/_/ |_/   \___/ \__/  /_/      \__,_/  \___/  \___/


DESCRIPTION:
Network speed tester including server discovery, latency measurement, download and upload speed testing.

USAGE:
    NetPace [OPTIONS] [COMMAND]

OPTIONS:
                              DEFAULT
    -h, --help                                       Prints help information.
    -v, --version                                    Prints version information.
        --loop                                       Performs the speed test on continuous loop.
        --count                                      Stop speed testing after this many times.
        --delay                                      Time between multiple speed tests (HH:MM:SS).
        --csv                                        Display minimal output in CSV format (always includes timestamp).
        --csv-delimiter       ,                      Single character delimiter to use in CSV output.
        --csv-header-units                           Display speed test units (eg. Mbps) in the CSV header row, not the data rows.
                                                     --unit-scale must not be <Auto> for multiple speed tests (eg. --loop or --count).
        --json                                       Display output in Json format.
        --json-pretty                                Display output in Json format (pretty print).
        --no-latency                                 Do not perform latency test.                                                                                                                       
                                                     When used without --server, the first available server is selected.                                                                                
        --no-download                                Do not perform download test.
        --no-upload                                  Do not perform upload test.
        --server                                     The url of a specific speed test sever.
                                                     'NetPace servers -l' will return your nearest servers.
    -t, --timestamp                                  Include a timestamp in the output.
        --datetimeformat      yyyy-MM-dd HH:mm:ss    The datetime format string, as defined by Microsoft.Net.
        --downloadsize                               Stop the download test after this many megabytes (IEC MiB).
        --uploadsize                                 Stop the upload test after this many megabytes (IEC MiB).
    -u, --unit                BitsPerSecond          The speed unit. <BitsPerSecond, BytesPerSecond>
        --unit-scale          Auto                   The speed unit scale. <Auto, Base, Kilo, Mega, Giga, Tera, Peta>
        --unit-system         SI                     The speed unit system. <SI, IEC>
                                                     SI steps up in powers of 1000 (KB, MB, GB), common in networking, 
                                                     while IEC uses powers of 1024 (KiB, MiB, GiB), standard in computing and storage.
        --verbosity           Normal                 The verbosity level. <Minimal, Normal, Debug>
                                                     Minimal is ideal for batch scripts and redirected output.
    -f, --file                                       Write output to file.
        --file-mode           Append                 Determines file output behavior. <Append, Overwrite>
    -q, --quiet                                      Suppress all normal console output (file output still works).

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

NetPace.Core includes test implementations of `ISpeedTestService` so you can test your code without making real network calls:

- **`SpeedTestStub`** - Simple stub returning fixed values with configurable delays
- **`SpeedTestMock`** - Fully configurable mock with injectable delegate functions
- **`VariableSpeedTester`** - Returns different speeds on each call (simulates variable network conditions)
- **`FaultySpeedTester`** - Simulates network failures and timeouts (for testing error handling)

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

<br />


## Contact
Frank Ray - [LinkedIn](https://www.linkedin.com/in/frankray/) - [Better Software UK](https://bettersoftware.uk)

GitHub: [https://github.com/FrankRay78/NetPace](https://github.com/FrankRay78/NetPace)
