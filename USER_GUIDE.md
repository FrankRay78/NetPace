# NetPace User Guide

Run a simple speed test using the nearest available server:  
```bash
netpace
```

Write test results to a file, while also displaying them in the console:
```bash
netpace --file results.txt
```

Produce CSV output, with every speed and latency a plain number, suitable for parsing:
```bash
netpace --csv
```

Report the result as a single compact line:
```bash
netpace --minimal
```

Run download test only:  
```bash
netpace --no-upload
```

Run upload test only:  
```bash
netpace --no-download
```

Run speed tests continuously, with a 15 minute delay between each:
```bash
netpace --loop --delay 00:15:00
```

Run 3 tests in a row, with 30 seconds delay between each:  
```bash
netpace --count 3 --delay 00:00:30
```

List the nearest speed test servers:  
```bash
netpace servers
```

Use a specific server URL for testing:  
```bash
netpace --server https://speedtest.example.com/
```

Show speeds in bytes/sec, using IEC units (KiB, MiB, GiB):  
```bash
netpace --unit BytesPerSecond --unit-system IEC
```

Add a timestamp to the result:  
```bash
netpace --timestamp
```

The timestamp has one fixed format per output mode, with no option to change it — see [Timestamps and regional settings](#timestamps-and-regional-settings).

Limit download to 50 MiB and upload to 20 MiB (for low bandwidth connections):  
```bash
netpace --downloadsize 50 --uploadsize 20
```

---

## Installing NetPace

With the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) installed, NetPace installs as a .NET tool in one line, and the `netpace` command is on your path afterwards — provided the .NET global tools directory (`~/.dotnet/tools`, or `%USERPROFILE%\.dotnet\tools` on Windows) is on `PATH`. `dotnet tool install` tells you if it is not:

```bash
dotnet tool install -g netpace
```

Move to the latest release at any time:

```bash
dotnet tool update -g netpace
```

Or run NetPace once without installing it — `dnx` fetches the package, runs it, and leaves nothing installed. The first run asks you to confirm the download, so pass `--yes` where there is no terminal to answer, such as a CI job:

```bash
dnx netpace
dnx --yes netpace -- --json
```

Pin a known version in a build or CI job with `dotnet tool install -g netpace --version <release>`, naming any version from the [releases page](https://github.com/FrankRay78/NetPace/releases).

The installed command behaves exactly like the release binary of the same version, and `netpace --version` reports the release it was published with.

**Which platforms the tool package covers.** Windows and Linux, on x64 and ARM64, get the Native AOT binary — built the same way as the `-aot` release download, so it starts without a .NET runtime. macOS, on Intel and Apple silicon, gets a build that runs on the .NET runtime the SDK already provides, because NetPace has no macOS Native AOT build yet. Anything outside those six — Alpine-style Linux among them — is not covered by the tool package; [download](https://github.com/FrankRay78/NetPace/releases) a standalone binary instead.

Without the .NET SDK, the [releases page](https://github.com/FrankRay78/NetPace/releases) carries binaries for every supported platform: unzip and run.

---

## Choosing an output format

With no format switch, NetPace renders the rich terminal output: a live progress display while each test runs, then the result. That is the right default at a terminal, and it is what you get if you pass nothing.

At an interactive terminal, each running test's progress bar carries a live speed beside it — the average speed since that test began, so it climbs and steadies as the test runs and ends on exactly the figure the result line reports for that test. It is written in whatever `--unit`, `--unit-system` and `--unit-scale` ask for, the same as the result line, and nothing is shown beside a bar until that test's first data arrives. Output that is not going to an interactive terminal — piped, redirected, or written with `--file` — is unaffected: the live display never appears there, and the result line is printed as before.

Three alternative formats are available instead:

| Switch | Output |
| --- | --- |
| `--minimal` | The result as a single compact line. No live progress display. |
| `--csv` | A single CSV row, always including a timestamp. `--csv-delimiter` chooses the separator. |
| `--json` | A JSON object. `--json-pretty` selects the same format and indents it for reading. |

`--minimal`, `--csv` and `--json` are peers, and exactly one format may be selected. Passing two together is an error naming the conflict, so a stray or mistyped flag in a script fails loudly instead of quietly producing the wrong format. `--json-pretty` counts as selecting JSON, so `--json --json-pretty` is the one combination allowed — they are the same format, indented — while `--csv --json-pretty` and `--json-pretty --minimal` are conflicts like any other pair.

`--minimal`, `--csv` and `--json` are written for a program to read, so they carry no line breaks beyond the ones their own format defines — one CSV record per line, and the indentation `--json-pretty` asks for. That holds however narrow the destination is: a pipe, a redirect, or a terminal window narrower than the line. Only the default terminal output wraps to fit the screen.

Every format composes with skipping individual tests — `--no-latency` / `--no-download` / `--no-upload`. `--timestamp`, `--unit`, `--unit-scale` and `--unit-system` are narrower: they shape the default and minimal output only. CSV and JSON always carry a timestamp, and report their measurements as plain numbers in fixed units, so those options are accepted alongside `--csv`, `--json` and `--json-pretty` without an error and change nothing in the output — see [Numeric values in CSV](#numeric-values-in-csv) and [Numeric values in JSON](#numeric-values-in-json).

Two further options decide whether and where the result appears, and both work with any format: `--quiet` suppresses the result entirely, and `--file` writes it to disk.

`--diagnostics` is not a fifth format — it adds a second stream alongside whichever format you picked, leaving the result untouched. See [Diagnosing a failure with `--diagnostics`](#diagnosing-a-failure-with---diagnostics).

### Numeric values in CSV

CSV is read by spreadsheets, importers and logs, so every measurement is a plain number in a fixed unit, and the column name says which unit. Nothing in a cell has to be stripped or converted before it can be summed, charted or compared.

| Column | What it holds |
| --- | --- |
| `LatencyMilliseconds` | Latency as a whole number of milliseconds. |
| `DownloadSpeedBitsPerSecond` | Download speed in bits per second, rounded to the nearest whole number. |
| `UploadSpeedBitsPerSecond` | Upload speed in bits per second, rounded to the nearest whole number. |

```bash
netpace --csv
```
```
Timestamp,LatencyMilliseconds,DownloadSpeedBitsPerSecond,DownloadSucceeded,DownloadFailed,UploadSpeedBitsPerSecond,UploadSucceeded,UploadFailed,IPAddress,Hostname
1980-01-01T10:05:00+01:00,12,512600000,150,0,48200000,32,0,192.168.1.1,test-host
```

**`--unit`, `--unit-scale` and `--unit-system` do not change CSV output at all.** They shape the default and minimal output only. That is what makes a CSV log safe to append to: under the default automatic scale a formatted speed can be `Kbps` on one run and `Mbps` on the next, so a file collecting rows over weeks would mix units in one column. The numbers never shift, so `--csv` works with `--count` and `--loop` whatever the unit options say, and the numbers match [the numeric fields in JSON](#numeric-values-in-json) for the same run.

The header is the same ten columns in the same order on every run, whichever tests ran. A test you skipped with `--no-latency`, `--no-download` or `--no-upload` leaves its cells empty rather than dropping its columns, so a script reading by position is not moved along by a skipped test:

```bash
netpace --csv --no-upload
```
```
Timestamp,LatencyMilliseconds,DownloadSpeedBitsPerSecond,DownloadSucceeded,DownloadFailed,UploadSpeedBitsPerSecond,UploadSucceeded,UploadFailed,IPAddress,Hostname
1980-01-01T10:05:00+01:00,12,512600000,150,0,,,,192.168.1.1,test-host
```

A download or upload test that ran but failed entirely reports `0`, with its request counts beside it — see [Detecting failed measurements](#detecting-failed-measurements).

`--csv-delimiter` chooses the separator, for a locale or a tool that wants something other than a comma.

### Numeric values in JSON

JSON reports each measured speed and latency once, as a plain number in a fixed unit. The field name says which unit, so a saved result explains itself without reference to the command that produced it. Nothing on the command line can change those units, so a script reads the same field the same way every run.

| Field | What it holds |
| --- | --- |
| `LatencyMilliseconds` | Latency as a whole number of milliseconds. |
| `DownloadSpeedBitsPerSecond` | Download speed in bits per second, rounded to the nearest whole number. |
| `UploadSpeedBitsPerSecond` | Upload speed in bits per second, rounded to the nearest whole number. |

```bash
NetPace --json-pretty
```
```json
{
  "ServerLocation": "London",
  "ServerSponsor": "Foo Telecom",
  "ServerUrl": "http://speedtest.foo.example:8080/speedtest/upload.php",
  "Timestamp": "1980-01-01T10:05:00+01:00",
  "LatencyMilliseconds": 12,
  "DownloadSpeedBitsPerSecond": 512600000,
  "DownloadSucceeded": 150,
  "DownloadFailed": 0,
  "UploadSpeedBitsPerSecond": 48200000,
  "UploadSucceeded": 32,
  "UploadFailed": 0,
  "IPAddress": "192.168.1.1",
  "Hostname": "test-host"
}
```

**`--unit`, `--unit-scale` and `--unit-system` do not change JSON output at all.** They shape the default and minimal output only. Passing one with `--json` or `--json-pretty` is accepted without an error or warning, and the result is byte-for-byte what the same run writes without it. That is what makes a saved JSON result comparable: a script comparing runs, writing to a database or checking a threshold reads the numbers and never has to strip a unit, convert between scales, or know which switches the run was made with.

Need a speed formatted for a person to read — for a notification, say? `--minimal` writes exactly that line, and follows the unit options.

A test you skipped with `--no-latency`, `--no-download` or `--no-upload` omits every field it would have filled. A download or upload test that ran but failed entirely reports `0`, with its request counts beside it — see [Detecting failed measurements](#detecting-failed-measurements). A latency measurement that fails ends the run instead, so it reports nothing.

### Timestamps and regional settings

The timestamp has one fixed format per output mode. There is no option to change it, so a script reading CSV or JSON never has to know which format a given run chose.

| Output | Timestamp | Example |
| --- | --- | --- |
| Default, `--minimal` (with `--timestamp`) | Local time, `yyyy-MM-dd HH:mm:ss` | `2026-10-16 08:21:17` |
| `--csv`, `--json` (always present) | ISO 8601 local time to the second with its offset from UTC | `2026-10-16T08:21:17+01:00` |

The offset is what makes the machine-readable timestamp one unambiguous instant, so results logged from machines in different time zones — or from one machine either side of a clock change — sort and compare directly, and any date parser reads them without being told the format. Where the local offset is zero, that is written `+00:00`. Terminal output leaves the offset out: you already know the zone you are sitting in.

Because that timestamp is a CSV field like any other, `--csv-delimiter` refuses a character the timestamp itself contains — a digit, `-`, `T`, `:` or `+`. Such a delimiter would split the timestamp across two fields and leave the row with one more field than its header, so NetPace reports the clash and exits rather than writing a row no CSV reader can line up. `,`, `;` and tab are all unaffected.

**NetPace output does not follow the machine's regional settings.** Every value reads the same way on every machine: speeds use `.` as the decimal separator with no digit grouping, and timestamps use the Gregorian calendar with ASCII digits. A result logged on a machine set to German, Thai or any other locale is byte-for-byte comparable with one logged next to it, and a comma-delimited CSV row can never gain a field from a decimal comma. `NetPace.Core` formats speeds the same way, so a library consumer reads the same text whatever culture its thread carries.

---

## How a server is chosen

With no `--server`, NetPace picks the server for you in two steps.

**Screening.** Every server discovery returned is sent a few quick requests, all at the same time, to answer one question: did it answer, and roughly how fast? The whole pass is capped at a fixed couple of seconds, no matter how many servers were discovered or how many of them never answer. A server that answers slowly is not thrown out — it is simply ranked below the quicker ones. Only a server that does not answer at all inside the cap drops out of the running.

**Measuring.** The fastest server that answered is then measured properly, with the full latency probe, and that is the figure NetPace reports. Everything after it — latency, download, upload — runs against that same server; NetPace does not switch servers part-way through a run.

If nothing answered during screening, the run reports `Error: No servers available`. That is a network outcome rather than a fault in NetPace, so the exit code stays `0` (see [Exit codes](#exit-codes)).

The cap on screening exists to stop a single pathological server stalling a run: before it was introduced, a server taking 20–60 seconds to answer each request could hold up a test for minutes. If you want a particular server regardless, `--server <url>` skips selection entirely.

`netpace servers -l` lists each discovered server with its screening latency, so what you see there is what auto-selection would rank. A server shown with `-` could not be reached, and a run would not have used it either. Rows stay in their listed order rather than being re-sorted by latency.

## Choosing a profile

The `--profile` flag bundles per-request payload sizes, parallelism, and a total-byte cap into one switch. Pick the profile that matches your link, and NetPace adapts the traffic shape to suit it. `Medium` is the default.

| Profile | Use it when… | Total per run (down + up, approx) |
|---|---|---|
| `Tiny`   | IoT / 10 MB-month plans — minimal traffic, single small request | ≤ 1 MiB (~245 KB + ~50 KB) |
| `Small`  | Cellular / metered — small budget, modest parallelism | ≤ 12 MiB (~10 MiB + ~2 MiB) |
| `Medium` | Typical home broadband — the default | ≤ 125 MiB (~100 MiB + ~25 MiB) |
| `Large`  | Fibre / business — saturates gigabit links | ≤ 1.25 GiB (~1 GiB + ~256 MiB) |
| `Mega`   | Inter-DC / 10 Gbps — saturates fibre, see warning below | ≤ 12 GiB (~10 GiB + ~2 GiB) |

Decision guide:

- Cellular or metered IoT? → `--profile small` (or `tiny` for the most miserly plans).
- Most users / home broadband? → no flag needed (Medium default).
- Gigabit fibre or business link? → `--profile large`.
- 10 Gbps inter-DC saturation? → `--profile mega`.

You can still pin a hard cap on top of a profile — the profile sets per-request shape, `--downloadsize` / `--uploadsize` override only the total cap:

```bash
netpace --profile large --downloadsize 200
```

> [!WARNING]\
> **`--profile mega` uses undocumented OoklaServer payloads** (`5000`, `6000`, `7000`)
> which are not part of the historic Speedtest.net Flash-client array. The selected
> server may not host them; future OoklaServer releases may break this profile. If
> Mega returns short reads or errors, fall back to `--profile large`. See
> [docs/architecture/download-upload-size-controls.md](https://github.com/FrankRay78/NetPace/blob/main/docs/architecture/download-upload-size-controls.md)
> for the per-request payload tables and the fallback strategy.

---

## Detecting failed measurements

A speed test runs many small requests in parallel and reports the aggregate throughput. If some of those requests fail (a dropped connection, a TLS error, a timeout, or a server that rejects the transfer), NetPace does **not** silently treat them as zero-speed data — it counts them, so you can tell a genuinely slow link from a server that isn't transferring at all. When *every* request in a test fails, the speed reads zero, and the counts tell you it was a total failure rather than a 0 bps link.

Every output format carries the counts:

**Default / `--minimal`** — the result token is annotated only when requests failed:
```
Latency: 24 ms, Download: 512.6 Mbps, Upload: 0 bps (32 of 32 requests failed)
```

**CSV** — a `Succeeded` and `Failed` column sits next to each speed column, and a total failure reports a speed of `0` beside the counts that say so:
```
Timestamp,LatencyMilliseconds,DownloadSpeedBitsPerSecond,DownloadSucceeded,DownloadFailed,UploadSpeedBitsPerSecond,UploadSucceeded,UploadFailed,IPAddress,Hostname
1980-01-01T10:05:00+01:00,24,512600000,150,0,0,0,32,192.168.1.1,test-host
```

**JSON** — each test that ran gains integer `…Succeeded` / `…Failed` fields alongside its speed. The schema keeps one shape: a total failure reports `0` with the counts beside it, exactly as the other formats do. Only a test you skipped (`--no-download`, `--no-upload`) omits its fields entirely:
```json
{ "UploadSpeedBitsPerSecond": 0, "UploadSucceeded": 0, "UploadFailed": 32, … }
```

The counts are the whole signal — no output format adds a prose warning on top of them. `--quiet` suppresses them along with the rest of the output; use `--fail-on` to detect an all-failed measurement in that mode.

### Diagnosing a failure with `--diagnostics`

The counts tell you *that* requests failed. `--diagnostics` tells you *why*, and does it from one run — which is the whole point of the switch, because a bug report that needs a second run is a conversation rather than a report.

Diagnostics go to the error stream, never to the result stream, so the two can be captured separately:

```bash
netpace --json --diagnostics > result.json 2> diagnostics.log
```

The result is byte-for-byte what the same invocation produces without the switch, in every output format — so adding `--diagnostics` to a script that parses `--csv` or `--json` cannot break the parsing. Without the switch, nothing is written to the error stream at all. At a terminal with nothing redirected, the records appear after the result, leaving the live progress display intact.

`--diagnostics` works alongside whichever output format you chose, and is unaffected by `--quiet`. It never writes to the `--file` target.

Each line is one record, as `key=value` pairs (an excerpt — a real run also carries the `screening` and `latency` test blocks, and `server.selected` appears after them):

```
ts=1980-01-01T10:05:00.000 event=run.start version=0.25.0 runtime=".NET 10.0.0" os="Linux 6.8.0-137-generic" arch=x64
ts=1980-01-01T10:05:00.000 event=run.invocation args="--json --diagnostics"
ts=1980-01-01T10:05:05.000 event=server.selected sponsor="Foo Telecom" location="London, GB" url=http://speedtest.foo.example:8080/speedtest/upload.php selection=auto-latency latency_ms=12
ts=1980-01-01T10:05:10.000 event=test.start test=upload
ts=1980-01-01T10:05:11.412 event=request test=upload seq=1 url=http://speedtest.foo.example:8080/speedtest/upload.php status=failed bytes=0 duration_ms=1203 reason="The SSL connection could not be established, see inner exception. <- Authentication failed because the remote party sent a TLS alert: 'HandshakeFailure'." exception=HttpRequestException
ts=1980-01-01T10:05:25.000 event=test.end test=upload requests=8 succeeded=0 failed=8 cancelled=0 bytes=0
ts=1980-01-01T10:05:30.000 event=run.end exit=0
```

What the records cover:

| Event | What it says |
| --- | --- |
| `run.start` | The NetPace version, the .NET runtime, the operating system and the architecture. |
| `run.invocation` | How NetPace was invoked. |
| `server.selected` | The server used, and `selection=` — `auto-latency`, `specified` or `first-in-list`. `latency_ms` is the screening figure that chose it, and is absent when latency played no part in choosing. |
| `test.start` / `test.end` | Which tests ran — `screening`, `latency`, `download`, `upload` — and, on `test.end`, the total `requests`, how many `succeeded`, `failed` or were `cancelled`, and the `bytes` the successful ones moved. Those totals cover exactly the `request` lines above them, so the two always reconcile. |
| `request` | One per request, with its sequence number, address, outcome, bytes, duration and — where it did not succeed — the `reason` and, if an exception was involved, its `exception` type. `status=` is `ok`, `failed` or `cancelled`. |

Every line repeats enough to stand alone, so `grep status=failed diagnostics.log` is a complete triage pass. Nothing is summarised away: every measured request gets its own record, including the latency probes made while choosing a server. Two kinds of request are not recorded individually: the initial server-list fetch, whose failure surfaces as the run's error instead; and a screening request that finishes after the selection ceiling has already passed, which arrives too late for the `test.end` it would belong to and is dropped so that summary stays exact — so a candidate you expected to see may be absent rather than untried. One request appears that you did not ask for: the upload test resolves its endpoint with an empty probe POST first, which takes the opening `seq` numbers of that test and reads `bytes=0`, so `test.end requests=` counts one more than the uploads measured (more, if the server redirects several times). Record volume is driven by how many servers the feed offers — roughly three records per candidate screened — on top of the request counts your `--profile` sets, so expect a few hundred lines from a default run.

Under `--count` or `--loop`, `run.start`, `run.invocation` and `run.end` appear once, and the test and request records repeat per iteration.

### Exit codes

The exit code reports only whether **NetPace itself** functioned. Network conditions like a total outage, 100% request failure, or no servers found are *data*, not errors, and NetPace will exit `0`. Only an operational failure (for example, being unable to write the `--file` output) or a usage error (an unusable option value, or two output formats at once) exits non-zero. So a `0 bps` measurement still exits `0` by default: inspect the counts (or use `--fail-on`) to detect it.

If you want a failed measurement to fail the process, opt in with `--fail-on`:

| Value | Exits non-zero when… |
|---|---|
| `None` | Never — measurement outcomes don't affect the exit code |
| `Total` | A requested test is all-failed (no request succeeded) |
| `Partial` | Any request in a requested test failed (strict; intended for pristine-run checks) |

A measurement that never ran at all — the network was down, or no server could be reached — counts as worse than an all-failed one, so it meets either threshold.

`--fail-on` is fail-fast and uniform across single runs, `--count`, and `--loop`: NetPace exits `1` at the first measurement that meets the threshold.

```bash
# Treat a totally-failed test as a failure
netpace --fail-on Total
```

---

For more options and details, run:  
```bash
netpace --help
```

To report problems or suggest features, visit [GitHub Issues](https://github.com/FrankRay78/NetPace/issues).
