# NetPace User Guide

Run a simple speed test using the nearest available server:  
```bash
NetPace
```

Write test results to a file, while also displaying them in the console:
```bash
NetPace --file results.txt
```

Produce CSV output with test results in megabits, suitable for parsing:
```bash
NetPace --csv --csv-header-units --unit-scale Mega
```

Report the result as a single compact line:
```bash
NetPace --minimal
```

Run download test only:  
```bash
NetPace --no-upload
```

Run upload test only:  
```bash
NetPace --no-download
```

Run speed tests continuously, with a 15 minute delay between each:
```bash
NetPace --loop --delay 00:15:00
```

Run 3 tests in a row, with 30 seconds delay between each:  
```bash
NetPace --count 3 --delay 00:00:30
```

List the nearest speed test servers:  
```bash
NetPace servers
```

Use a specific server URL for testing:  
```bash
NetPace --server https://speedtest.example.com/
```

Show speeds in bytes/sec, using IEC units (KiB, MiB, GiB):  
```bash
NetPace --unit BytesPerSecond --unit-system IEC
```

Add a timestamp in custom date format:  
```bash
NetPace --timestamp --datetimeformat "dd/MM/yyyy HH:mm"
```

Limit download to 50 MiB and upload to 20 MiB (for low bandwidth connections):  
```bash
NetPace --downloadsize 50 --uploadsize 20
```

---

## Choosing an output format

With no format switch, NetPace renders the rich terminal output: a live progress display while each test runs, then the result. That is the right default at a terminal, and it is what you get if you pass nothing.

Three alternative formats are available instead:

| Switch | Output |
| --- | --- |
| `--minimal` | The result as a single compact line. No live progress display. |
| `--csv` | A single CSV row, always including a timestamp. `--csv-header-units` and `--csv-delimiter` shape it. |
| `--json` | A JSON object. `--json-pretty` selects the same format and indents it for reading. |

`--minimal`, `--csv` and `--json` are peers, and exactly one format may be selected. Passing two together is an error naming the conflict, so a stray or mistyped flag in a script fails loudly instead of quietly producing the wrong format. `--json-pretty` counts as selecting JSON, so `--json --json-pretty` is the one combination allowed — they are the same format, indented — while `--csv --json-pretty` and `--json-pretty --minimal` are conflicts like any other pair.

Every format composes with the result-shaping options — `--timestamp`, `--unit`, `--unit-scale`, `--unit-system`, and skipping individual tests with `--no-latency` / `--no-download` / `--no-upload`. `--quiet` suppresses the result entirely whichever format you chose, while `--file` writes it to disk.

`--diagnostics` is not a fifth format — it adds a second stream alongside whichever format you picked, leaving the result untouched. See [Diagnosing a failure with `--diagnostics`](#diagnosing-a-failure-with---diagnostics).

---

## How a server is chosen

With no `--server`, NetPace picks the server for you in two steps.

**Screening.** Every server discovery returned is sent a few quick requests, all at the same time, to answer one question: did it answer, and roughly how fast? The whole pass is capped at a fixed couple of seconds, no matter how many servers were discovered or how many of them never answer. A server that answers slowly is not thrown out — it is simply ranked below the quicker ones. Only a server that does not answer at all inside the cap drops out of the running.

**Measuring.** The fastest server that answered is then measured properly, with the full latency probe, and that is the figure NetPace reports. Everything after it — latency, download, upload — runs against that same server; NetPace does not switch servers part-way through a run.

If nothing answered during screening, the run reports `Error: No servers available`. That is a network outcome rather than a fault in NetPace, so the exit code stays `0` (see [Exit codes](#exit-codes)).

The cap on screening exists to stop a single pathological server stalling a run: before it was introduced, a server taking 20–60 seconds to answer each request could hold up a test for minutes. If you want a particular server regardless, `--server <url>` skips selection entirely.

`NetPace servers -l` lists each discovered server with its screening latency, so what you see there is what auto-selection would rank. A server shown with `-` could not be reached, and a run would not have used it either. Rows stay in their listed order rather than being re-sorted by latency.

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
NetPace --profile large --downloadsize 200
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

A speed test runs many small requests in parallel and reports the aggregate throughput. If some of those requests fail (a dropped connection, a TLS error, a timeout, or a server that rejects the transfer), NetPace does **not** silently treat them as zero-speed data — it counts them, so you can tell a genuinely slow link from a server that isn't transferring at all. When *every* request in a test fails, the speed reads `0 bps`, and the counts tell you it was a total failure rather than a 0 bps link.

Every output format carries the counts:

**Default / `--minimal`** — the result token is annotated only when requests failed:
```
Latency: 24 ms, Download: 512.6 Mbps, Upload: 0 bps (32 of 32 requests failed)
```

**CSV** — a `Succeeded` and `Failed` column sits next to each speed column:
```
Timestamp,Latency,Download,DownloadSucceeded,DownloadFailed,Upload,UploadSucceeded,UploadFailed,IPAddress,Hostname
```

**JSON** — each test that ran gains integer `…Succeeded` / `…Failed` fields alongside its speed. The schema keeps one shape: a total failure reports `0 bps` with the counts beside it, exactly as normal and CSV output do. Only a test you skipped (`--no-download`, `--no-upload`) omits its fields entirely:
```json
{ "UploadSpeed": "0 bps", "UploadSucceeded": 0, "UploadFailed": 32, … }
```

The counts are the whole signal — no output format adds a prose warning on top of them. `--quiet` suppresses them along with the rest of the output; use `--fail-on` to detect an all-failed measurement in that mode.

### Diagnosing a failure with `--diagnostics`

The counts tell you *that* requests failed. `--diagnostics` tells you *why*, and does it from one run — which is the whole point of the switch, because a bug report that needs a second run is a conversation rather than a report.

Diagnostics go to the error stream, never to the result stream, so the two can be captured separately:

```bash
NetPace --json --diagnostics > result.json 2> diagnostics.log
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

The exit code reports only whether **NetPace itself** functioned. Network conditions like a total outage, 100% request failure, or no servers found are *data*, not errors, and NetPace will exit `0`. Only an operational failure (for example, being unable to write the `--file` output) exits non-zero. So a `0 bps` measurement still exits `0` by default: inspect the counts (or use `--fail-on`) to detect it.

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
NetPace --fail-on Total
```

---

For more options and details, run:  
```bash
NetPace --help
```

To report problems or suggest features, visit [GitHub Issues](https://github.com/FrankRay78/NetPace/issues).
