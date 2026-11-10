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

Report the result as one named line per figure:
```bash
netpace --simple
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
netpace --server-url https://speedtest.example.com/
```

Show speeds in bytes/sec, using IEC units (KiB, MiB, GiB):  
```bash
netpace --unit bytes --unit-system IEC
```

Watch one dated line per run scroll past, the way `ping` is read:  
```bash
netpace --minimal --loop
```

The timestamp has one fixed format per output mode, with no option to change it — see [Timestamps and regional settings](#timestamps-and-regional-settings).

Limit download to 50 MiB and upload to 20 MiB (for low bandwidth connections):  
```bash
netpace --downloadsize 50 --uploadsize 20
```

Run the test over a single connection, the way an ordinary file transfer behaves:  
```bash
netpace --connections 1
```

---

## Choosing an output format

With no format switch, NetPace renders the rich terminal output: the server as soon as it is known, then a live table that fills in as each test finishes. That is the right default at a terminal, and it is what you get if you pass nothing.

```txt
Hey! Broadband
http://speedtest.heybroadband.example/speedtest/upload.php

    Test         Done       Speed  Latency  Jitter
✓   Pinging      100%                 3 ms    1 ms
✓   Downloading  100%  93.12 Mbps    60 ms    4 ms
⠹   Uploading     30%   17.9 Mbps    38 ms    3 ms
```

Idle latency is the `Pinging` row; the latency and jitter beside a transfer are the line's latency *while that transfer was running*, so you can read one against the other straight down the column. That comparison is what shows bufferbloat — a link whose latency climbs under load. The figures are live: each one appears as soon as there is something to report and ends on the figure the measurement finally reports. A cell stays blank where nothing was measured, never zero.

Speeds follow `--unit`, `--unit-system` and `--unit-scale`. Every column sizes itself to the widest value it has been given, and a window too narrow for the table wraps it rather than dropping a column.

The default layout is for a person watching a terminal. It is not a format to scrape, and it may change in any release — a script reads `--minimal`, `--simple`, `--csv` or `--json`, all four of which are stable. Wherever default output cannot redraw — a pipe, a redirect, a `--file` target, or a terminal that cannot animate — it writes the `--simple` lines instead, and nothing else: no table, no server, and no control codes. Each destination is judged on its own, so `netpace --file results.txt` at a terminal shows the table on screen and writes the simple lines to the file.

Four alternative formats are available instead:

| Switch | Output |
| --- | --- |
| `--minimal` | One line per run, opening with the time it was taken. No live table, and no under-load latency probes. |
| `--simple` | One named line per figure — `Ping`, `Download`, `Upload`. No live table, and no under-load latency probes. |
| `--csv` | A single CSV row, always including a timestamp. `--no-csv-header` leaves the header row out — see [Collecting CSV in a log](#collecting-csv-in-a-log). |
| `--json` | A JSON object. `--json-pretty` selects the same format and indents it for reading. |

`--minimal`, `--simple`, `--csv` and `--json` are peers, and exactly one format may be selected. Passing two together is an error naming the conflict, so a stray or mistyped flag in a script fails loudly instead of quietly producing the wrong format. `--json-pretty` counts as selecting JSON, so `--json --json-pretty` is the one combination allowed — they are the same format, indented — while `--csv --json-pretty` and `--json-pretty --simple` are conflicts like any other pair.

`--minimal`, `--simple`, `--csv` and `--json` are written for a program to read, so they carry no line breaks beyond the ones their own format defines — one `--minimal` line per run, one `--simple` figure per line, one CSV record per line, and the indentation `--json-pretty` asks for. That holds however narrow the destination is: a pipe, a redirect, or a terminal window narrower than the line — and it holds for default output too, which writes those very lines wherever it cannot redraw. Only the live table wraps to fit the screen.

Every format composes with skipping individual tests — `--no-latency` / `--no-download` / `--no-upload`. `--no-latency` means no latency anywhere: no idle measurement, no latency or jitter in any format, and no probes sent while a transfer runs. `--unit`, `--unit-scale` and `--unit-system` are narrower: they shape the default, minimal and simple output only. `--device-id` and `--custom-field` are narrower the other way — they add fields to CSV and JSON only, and are described under [The fields CSV and JSON carry](#the-fields-csv-and-json-carry). CSV and JSON always carry a timestamp, and report their measurements as plain numbers in fixed units, so the unit options are accepted alongside `--csv`, `--json` and `--json-pretty` without an error and change nothing in the output — see [Numeric values in CSV](#numeric-values-in-csv) and [Numeric values in JSON](#numeric-values-in-json).

Two further options decide whether and where the result appears, and both work with any format: `--quiet` suppresses the result entirely, and `--file` writes it to disk.

`--diagnostics` is not another format — it adds a second stream alongside whichever format you picked, leaving the result untouched. See [Diagnosing a failure with `--diagnostics`](#diagnosing-a-failure-with---diagnostics).

### Minimal output

`--minimal` writes one line per run, opening with the time the run was taken:

```txt
2026-10-16 08:21:17, Latency: 12 ms, Download: 93.12 Mbps, Upload: 18.4 Mbps
```

That is the format to leave running: `netpace --minimal --loop` scrolls one dated line past per run, and a column of them is read down the page for a slow period, the way `ping` is. The time is always there, in local time as `yyyy-MM-dd HH:mm:ss`, because a line in a running log says little without it; there is no switch to add or remove it. Speeds follow `--unit`, `--unit-scale` and `--unit-system` and read exactly as `--simple` and the default output show them for the same run. A test skipped with `--no-latency` / `--no-download` / `--no-upload` contributes no field at all, failed requests are noted on their own figure, and `--count`, `--loop` and `--file` each write one line per run.

A run that faults writes a bare `Error: <message>` line instead, with no time in front of it: this format is a quick shorthand for someone watching a terminal, not a detailed log. Reach for `--csv` or `--json` where a run has to be recorded in full, and for `--simple` where a script reads one figure at a time.

### Simple output

`--simple` writes one line per figure, each labelled, in the order the tests ran:

```txt
Ping: 12 ms
Download: 93.12 Mbps
Upload: 18.4 Mbps
```

A script reads any of them the same way — `netpace --simple | grep '^Download:' | awk '{print $2}'` gives the download speed's number. Anchor the match to the start of the line: a run that faults writes an `Error:` message to the same stream, and an unanchored `grep Download` can pick a word out of that message instead of a figure. Speeds are auto-scaled and follow `--unit`, `--unit-scale` and `--unit-system`, so a figure can read `1.2 Gbps` on a fast line; pin the unit with `--unit-scale Mega` where a script compares numbers across runs. These three lines are the whole output and carry no time, matching what `speedtest-cli --simple` writes; `--minimal` is the format with a timestamp on every line. A test skipped with `--no-latency` / `--no-download` / `--no-upload` contributes no line at all, and runs under `--count` and `--loop` follow one another with no separator. Failed requests are noted on their own test's line. The format shows no jitter, server or under-load figure, and sends no under-load latency probes; scripts wanting more use CSV or JSON.

These are also the lines default output writes wherever it cannot redraw, so `netpace > results.txt` and `netpace --simple > results.txt` leave the same file. The one difference is the probes: default output sends the under-load latency probes whichever destination it ends up writing to, so a redirected run carries traffic that only `--diagnostics` reports, while `--simple` sends none.

### What `--file` writes

A file never gets the live table: a `--file` target cannot redraw, so default output writes it the `--simple` lines — which is what keeps control codes out of a log. At a terminal the two happen together, the table on screen and the lines in the file.

`--file` writes **UTF-8 text with no byte-order mark**, in every format and in both `--file-mode` settings. Nothing is written in front of the content, so a `--json` file starts at its opening brace and a `--csv` file starts at its first column name: a single run's JSON file is read as it stands by a strict parser, and `cut`, `awk` and `head` see that first column name as the first field of the first line.

A log an earlier version of NetPace started keeps the byte-order mark it already has, because appending does not rewrite the start of a file. Start a new log if you want one without it.

**Opening a CSV result in a spreadsheet:** import it rather than double-clicking it. In Excel, use *Data → From Text/CSV* and choose **UTF-8** as the file origin. Without a byte-order mark to tip it off, a double-clicked file may be read as a legacy code page instead, which garbles non-ASCII text such as an accented server sponsor or location name. LibreOffice Calc asks for the character set on open, where UTF-8 is the default.

**Reading a result in Windows PowerShell 5.1:** read the file with `Get-Content -Encoding UTF8`, or use `Import-Csv`, which assumes UTF-8. Without a byte-order mark, a plain `Get-Content` reads it in the system's legacy code page and garbles the same non-ASCII text. PowerShell 7 needs neither.

### The fields CSV and JSON carry

**CSV and JSON carry the same fields under the same names.** A field one of them records, the other records too, spelled identically, so a log in either format reads the same way and neither has to be interpreted against the other. Only the shape for something missing differs: CSV always has every built-in column and leaves the cell empty, while JSON leaves the field out.

Every result in both formats says which server it was measured against — `ServerLocation`, `ServerSponsor` and `ServerUrl` — so a drop in speed part-way through a long log can be told apart from a change of server.

#### A label for the machine: `--device-id`

Collecting logs from several machines into one place needs every row to say which machine it came from, in a label you chose rather than a hostname or IP address that may change under you:

```bash
netpace --csv --device-id kitchen-pi
```

`DeviceId` then carries the label exactly as given. The CSV column is always there and holds an empty cell when you gave no label; the JSON field appears only when you did.

#### Anything else worth recording: `--custom-field`

`--custom-field key=value` records a pair of your own on every result — an ISP plan, a site name, a connection type — and may be given as many times as you like:

```bash
netpace --csv --custom-field site=home --custom-field plan=500M
```

In CSV each pair becomes one more column after every built-in column, in the order you gave them: the key names the column and the value fills the cell. In JSON the pairs are grouped in one `CustomFields` object, so a custom key can never replace a built-in field:

```json
{ "...": "...", "CustomFields": { "site": "home", "plan": "500M" } }
```

The value is everything after the first `=`, so it may contain `=` itself and may be empty. An argument with no `=`, or with nothing before the first one, is refused with a message naming the expected form, and nothing is measured.

NetPace does not police the keys. A key spelled like a built-in field, or one you give twice, is recorded as given, and each format resolves that in its own shape: CSV writes a second column of that name, while JSON — which cannot hold one key twice — keeps the last value given. A repeated key therefore reads back differently from the two formats, so it is worth avoiding.

Neither option reaches the default or simple output. Both describe the run for a log rather than the measurement, and the default output at a terminal already names the server; passing either alongside those formats is accepted without an error and changes nothing in what you see. A malformed `--custom-field` is still refused in every mode.

#### When the columns change, start a new log

`--file` appends by default, and NetPace writes the header only into a log that is still empty (see [Collecting CSV in a log](#collecting-csv-in-a-log)), so an appended log keeps the header its first run wrote. Changing the set of `--custom-field` pairs, or upgrading to a NetPace that writes more columns of its own, therefore leaves later rows sitting under a header that no longer describes them, with nothing in the file to mark where the columns changed. Changing `--device-id` alone changes a cell, not a column. **Start a new log whenever the columns change** — nothing stops you doing otherwise, and the result is a file no CSV reader can make sense of as a whole.

### Numeric values in CSV

CSV is read by spreadsheets, importers and logs, so every measurement is a plain number in a fixed unit, and the column name says which unit. Nothing in a cell has to be stripped or converted before it can be summed, charted or compared.

| Column | What it holds |
| --- | --- |
| `LatencyMilliseconds` | Latency measured on an idle line, as a whole number of milliseconds. |
| `JitterMilliseconds` | Idle jitter in milliseconds: how uneven those round trips were. Empty whenever `LatencyMilliseconds` is. |
| `DownloadSpeedBitsPerSecond` | Download speed in bits per second, rounded to the nearest whole number. |
| `DownloadLatencyMilliseconds` | Average latency measured *while the download ran* — latency under load — in milliseconds. Directly comparable with `LatencyMilliseconds`, which is the same line idle. |
| `DownloadLowestLatencyMilliseconds` | The quickest of the probes behind that average, in milliseconds. |
| `DownloadHighestLatencyMilliseconds` | The slowest of them, in milliseconds — the figure a stuttering line shows up in. |
| `DownloadJitterMilliseconds` | Jitter of those probes, in milliseconds. |
| `UploadSpeedBitsPerSecond` | Upload speed in bits per second, rounded to the nearest whole number. |
| `UploadLatencyMilliseconds`, `UploadLowestLatencyMilliseconds`, `UploadHighestLatencyMilliseconds`, `UploadJitterMilliseconds` | The same four figures, measured while the upload ran. |

```bash
netpace --csv
```
```
Timestamp,LatencyMilliseconds,JitterMilliseconds,DownloadSpeedBitsPerSecond,DownloadSucceeded,DownloadFailed,DownloadLatencyMilliseconds,DownloadLowestLatencyMilliseconds,DownloadHighestLatencyMilliseconds,DownloadJitterMilliseconds,UploadSpeedBitsPerSecond,UploadSucceeded,UploadFailed,UploadLatencyMilliseconds,UploadLowestLatencyMilliseconds,UploadHighestLatencyMilliseconds,UploadJitterMilliseconds,IPAddress,Hostname,ServerLocation,ServerSponsor,ServerUrl,DeviceId
1980-01-01T10:05:00+01:00,12,3,512600000,150,0,48,31,96,9,48200000,32,0,52,33,104,11,192.168.1.1,test-host,London,Foo Telecom,http://speedtest.foo.example:8080/speedtest/upload.php,
```

**Latency under load is where bufferbloat shows.** A line whose idle latency is 12 ms and whose download latency is 48 ms is filling a buffer somewhere between you and the server; the highest figure is what a video call hears. Each direction's four figures sit beside that direction's speed, so one row holds the whole picture of one run.

**An unmeasured latency figure is an empty cell, never `0`.** A direction's four cells are empty where the transfer measured nothing under load — the transfer ended before the first probe was due, no probe answered, or every one of the transfer's own requests failed — as well as where the test did not run at all. The lowest and highest are also empty where no run of probe times was kept. Nothing charting these columns can mistake a line that was not measured for a perfect one.

**`--no-latency` empties only `LatencyMilliseconds` and `JitterMilliseconds`.** It skips the idle measurement, not the probes taken while a transfer runs, so each direction's four under-load figures are still reported; only `--no-download` or `--no-upload` empties them.

**`--unit`, `--unit-scale` and `--unit-system` do not change CSV output at all.** They shape the default, minimal and simple output only. That is what makes a CSV log safe to append to: under the default automatic scale a formatted speed can be `Kbps` on one run and `Mbps` on the next, so a file collecting rows over weeks would mix units in one column. The numbers never shift, so `--csv` works with `--count` and `--loop` whatever the unit options say, and the numbers match [the numeric fields in JSON](#numeric-values-in-json) for the same run.

The built-in columns are the same columns in the same order on every run, whichever tests ran, with one column per `--custom-field` pair after them — so two runs given different custom fields do not share a header. A test you skipped with `--no-latency`, `--no-download` or `--no-upload` leaves its cells empty rather than dropping its columns, so a script reading by position is not moved along by a skipped test:

```bash
netpace --csv --no-upload
```
```
Timestamp,LatencyMilliseconds,JitterMilliseconds,DownloadSpeedBitsPerSecond,DownloadSucceeded,DownloadFailed,DownloadLatencyMilliseconds,DownloadLowestLatencyMilliseconds,DownloadHighestLatencyMilliseconds,DownloadJitterMilliseconds,UploadSpeedBitsPerSecond,UploadSucceeded,UploadFailed,UploadLatencyMilliseconds,UploadLowestLatencyMilliseconds,UploadHighestLatencyMilliseconds,UploadJitterMilliseconds,IPAddress,Hostname,ServerLocation,ServerSponsor,ServerUrl,DeviceId
1980-01-01T10:05:00+01:00,12,3,512600000,150,0,48,31,96,9,,,,,,,,192.168.1.1,test-host,London,Foo Telecom,http://speedtest.foo.example:8080/speedtest/upload.php,
```

A download or upload test that ran but failed entirely reports `0`, with its request counts beside it — see [Detecting failed measurements](#detecting-failed-measurements).

CSV output is always comma-separated — there is no option to choose another separator, so every log NetPace writes has one shape. A value that would otherwise disturb the row is written as CSV requires: any field containing a comma or a double quote — most often a server's sponsor or location name, a `--device-id` label or a `--custom-field` value — is wrapped in double quotes, with any double quote of its own written twice, so a standard CSV reader hands back the original text and the row still has one field per column. A line break inside any field is written as a space, so every record stays on one line.

### Collecting CSV in a log

A run on a schedule that appends every result to one file should leave that file with a single header line at the top and a data row for each run. `--file` does that with no extra option: it appends by default, and NetPace writes the header only when the target does not exist yet or is empty.

```bash
# From cron, every fifteen minutes
netpace --csv --file /var/log/netpace.csv
```

After two runs, `/var/log/netpace.csv` holds:

```
Timestamp,LatencyMilliseconds,JitterMilliseconds,DownloadSpeedBitsPerSecond,DownloadSucceeded,DownloadFailed,DownloadLatencyMilliseconds,DownloadLowestLatencyMilliseconds,DownloadHighestLatencyMilliseconds,DownloadJitterMilliseconds,UploadSpeedBitsPerSecond,UploadSucceeded,UploadFailed,UploadLatencyMilliseconds,UploadLowestLatencyMilliseconds,UploadHighestLatencyMilliseconds,UploadJitterMilliseconds,IPAddress,Hostname,ServerLocation,ServerSponsor,ServerUrl,DeviceId
1980-01-01T10:05:00+01:00,12,3,512600000,150,0,48,31,96,9,48200000,32,0,52,33,104,11,192.168.1.1,test-host,London,Foo Telecom,http://speedtest.foo.example:8080/speedtest/upload.php,
1980-01-01T10:20:00+01:00,14,4,498300000,150,0,61,34,148,14,47100000,32,0,58,35,121,12,192.168.1.1,test-host,London,Foo Telecom,http://speedtest.foo.example:8080/speedtest/upload.php,
```

The terminal is a fresh stream on every run, so it still labels its columns even when the file does not: the two destinations differ by exactly that one line. `--file-mode overwrite` replaces the file rather than adding to it, so an overwritten log always starts with the header.

Redirecting with `>>` is different, because the shell opens the file and NetPace never sees it — an empty log and one holding a thousand rows look the same from inside NetPace. Turn the header off with `--no-csv-header` there, and write it yourself once if you want one:

```bash
netpace --csv --no-csv-header >> ~/netpace.csv
```

`--no-csv-header` leaves the header out of everything: the terminal, redirected or piped standard output, and a `--file` target even when it is new or empty, on every iteration of `--loop` and `--count`. It is accepted alongside the other formats and changes nothing there.

**Start a new log if a run failed before it wrote a row.** NetPace decides to write the header by whether the target is empty, never by reading what is in it, so any content keeps the header out — including an error message from a run whose network was down, which is written to the log alongside the terminal. A log whose first line is an error rather than the column names will collect data rows under no header for as long as you keep appending to it. Move it aside and let the next run start a fresh one.

**Start a new log after an upgrade that changes the columns.** While NetPace is below 1.0.0 the CSV columns may gain or move fields between releases, and nothing checks an existing log's header against the columns being written. Rows from a newer version appended to an older log sit under a header that no longer describes them. After an upgrade that changes the columns, move the old log aside and let NetPace start a fresh one.

### Numeric values in JSON

JSON reports each measured speed and latency once, as a plain number in a fixed unit. The field name says which unit, so a saved result explains itself without reference to the command that produced it. Nothing on the command line can change those units, so a script reads the same field the same way every run.

| Field | What it holds |
| --- | --- |
| `LatencyMilliseconds` | Latency measured on an idle line, as a whole number of milliseconds. |
| `JitterMilliseconds` | Idle jitter in milliseconds: how uneven those round trips were. Present exactly when `LatencyMilliseconds` is. |
| `DownloadSpeedBitsPerSecond` | Download speed in bits per second, rounded to the nearest whole number. |
| `DownloadLatencyMilliseconds` | Average latency measured *while the download ran* — latency under load — in milliseconds. Directly comparable with `LatencyMilliseconds`, which is the same line idle. |
| `DownloadLowestLatencyMilliseconds` | The quickest of the probes behind that average, in milliseconds. |
| `DownloadHighestLatencyMilliseconds` | The slowest of them, in milliseconds — the figure a stuttering line shows up in. |
| `DownloadJitterMilliseconds` | Jitter of those probes, in milliseconds. |
| `UploadSpeedBitsPerSecond` | Upload speed in bits per second, rounded to the nearest whole number. |
| `UploadLatencyMilliseconds`, `UploadLowestLatencyMilliseconds`, `UploadHighestLatencyMilliseconds`, `UploadJitterMilliseconds` | The same four figures, measured while the upload ran. |

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
  "JitterMilliseconds": 3,
  "DownloadSpeedBitsPerSecond": 512600000,
  "DownloadSucceeded": 150,
  "DownloadFailed": 0,
  "DownloadLatencyMilliseconds": 48,
  "DownloadLowestLatencyMilliseconds": 31,
  "DownloadHighestLatencyMilliseconds": 96,
  "DownloadJitterMilliseconds": 9,
  "UploadSpeedBitsPerSecond": 48200000,
  "UploadSucceeded": 32,
  "UploadFailed": 0,
  "UploadLatencyMilliseconds": 52,
  "UploadLowestLatencyMilliseconds": 33,
  "UploadHighestLatencyMilliseconds": 104,
  "UploadJitterMilliseconds": 11,
  "IPAddress": "192.168.1.1",
  "Hostname": "test-host"
}
```

The same nine figures CSV carries, under the same names and as the same numbers for the same run. **A figure that was not measured is left out rather than reported as `0`** — a direction's four fields are absent where the transfer measured nothing under load or the test did not run, and the lowest and highest are absent where no run of probe times was kept. `JitterMilliseconds` goes whenever `LatencyMilliseconds` does, so `--no-latency` leaves out both — and only those two: each direction's four under-load fields are still reported, and only `--no-download` or `--no-upload` leaves them out.

**`--unit`, `--unit-scale` and `--unit-system` do not change JSON output at all.** They shape the default, minimal and simple output only. Passing one with `--json` or `--json-pretty` is accepted without an error or warning, and the result is byte-for-byte what the same run writes without it. That is what makes a saved JSON result comparable: a script comparing runs, writing to a database or checking a threshold reads the numbers and never has to strip a unit, convert between scales, or know which switches the run was made with.

Need a speed formatted for a person to read — for a notification, say? `--simple` writes exactly that figure on its own line, and `--minimal` writes the whole run as one line; both follow the unit options.

A test you skipped with `--no-latency`, `--no-download` or `--no-upload` omits every field it would have filled. A download or upload test that ran but failed entirely reports `0`, with its request counts beside it — see [Detecting failed measurements](#detecting-failed-measurements). A latency measurement that fails ends the run instead, so it reports nothing.

### Timestamps and regional settings

The timestamp has one fixed format per output mode, and whether a format carries one at all is fixed too. There is no option to add or remove it, so a script reading a NetPace result never has to know what a given run was asked for. The default and `--simple` output carry no timestamp: the default is read live, and `--simple` keeps the shape `speedtest-cli --simple` writes.

| Output | Timestamp | Example |
| --- | --- | --- |
| `--minimal` (always present) | Local time, `yyyy-MM-dd HH:mm:ss` | `2026-10-16 08:21:17` |
| `--csv`, `--json` (always present) | ISO 8601 local time to the second with its offset from UTC | `2026-10-16T08:21:17+01:00` |

The offset is what makes the machine-readable timestamp one unambiguous instant, so results logged from machines in different time zones — or from one machine either side of a clock change — sort and compare directly, and any date parser reads them without being told the format. Where the local offset is zero, that is written `+00:00`. The `--minimal` line leaves the offset out: someone watching their own terminal already knows the zone they are sitting in.

**NetPace output does not follow the machine's regional settings.** Every value reads the same way on every machine: speeds use `.` as the decimal separator with no digit grouping, and timestamps use the Gregorian calendar with ASCII digits. A result logged on a machine set to German, Thai or any other locale is byte-for-byte comparable with one logged next to it, and a comma-delimited CSV row can never gain a field from a decimal comma. `NetPace.Core` formats speeds the same way, so a library consumer reads the same text whatever culture its thread carries.

---

## How a server is chosen

With no `--server-url`, NetPace picks the server for you in two steps.

**Screening.** Every server discovery returned is sent a few quick requests, all at the same time, to answer one question: did it answer, and roughly how fast? Screening normally ends within about two seconds, no matter how many servers were discovered or how many of them never answer. If no server has been ranked by then — as on a satellite link or a congested mobile connection — it waits up to ten seconds, so a slow link can still have a server chosen. A server that answers slowly is not thrown out — it is simply ranked below the quicker ones. Only a server that has not answered by the time screening ends drops out of the running.

**Measuring.** The fastest server that answered is then measured properly, with the full latency probe, and that is the figure NetPace reports. Everything after it — latency, download, upload — runs against that same server; NetPace does not switch servers part-way through a run.

If nothing answered during screening, the run reports `Error: No servers available`. That is a network outcome rather than a fault in NetPace, so the exit code stays `0` (see [Exit codes](#exit-codes)).

The cap on screening exists to stop a single pathological server stalling a run: before it was introduced, a server taking 20–60 seconds to answer each request could hold up a test for minutes. If you want a particular server regardless, `--server-url <url>` skips selection entirely.

`netpace servers -l` lists each discovered server with its screening latency, the same figure auto-selection ranks on. It gives every server the full ten seconds to answer, though, where a run stops waiting after about two seconds once a quicker server has answered, so a server that is slow to answer can show a latency here and still be passed over by a run. A server shown with `-` could not be reached at all, and a run would not have used it either. Rows stay in their listed order rather than being re-sorted by latency. Press Ctrl+C part-way through and the rows that had not answered yet stay blank instead: nothing had been decided about those servers.

## Choosing a profile

The `--profile` flag bundles per-request payload sizes, parallelism, and a total-byte cap into one switch. Pick the profile that matches your link, and NetPace adapts the traffic shape to suit it. `medium` is the default.

| Profile | Use it when… | Total per run (down + up, approx) |
|---|---|---|
| `tiny`   | IoT / 10 MB-month plans — minimal traffic, single small request | ≤ 1 MiB (~245 KB + ~50 KB) |
| `small`  | Cellular / metered — small budget, modest parallelism | ≤ 12 MiB (~10 MiB + ~2 MiB) |
| `medium` | Typical home broadband — the default | ≤ 125 MiB (~100 MiB + ~25 MiB) |
| `large`  | Fibre / business — saturates gigabit links | ≤ 1.25 GiB (~1 GiB + ~256 MiB) |
| `mega`   | Inter-DC / 10 Gbps — saturates fibre, see warning below | ≤ 12 GiB (~10 GiB + ~2 GiB) |

Decision guide:

- Cellular or metered IoT? → `--profile small` (or `tiny` for the most miserly plans).
- Most users / home broadband? → no flag needed (`medium` default).
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
> `mega` returns short reads or errors, fall back to `--profile large`. See
> [docs/architecture/download-upload-size-controls.md](https://github.com/FrankRay78/NetPace/blob/main/docs/architecture/download-upload-size-controls.md)
> for the per-request payload tables and the fallback strategy.

---

## Choosing how many connections

How hard a test pushes the line decides what it reads. A single connection may never fill a fast link, so the speed reads low. Many connections fill it, which is what a saturation test is for. `--connections <n>` lets you set that load yourself, as a whole number from 1 to 32:

```bash
netpace --connections 1
```

`--connections` overrides one part of the profile and nothing else. The profile still decides the per-request payload sizes, how many requests there are, and the total-byte caps:

```bash
netpace --profile large --connections 2
```

That runs Large's payloads and Large's 1 GiB download cap, but never more than two transfers at once in either direction. Leave `--connections` out and the profile's own count applies, exactly as before: Tiny 1, Small 2, Medium 4, Large 16, Mega 32.

The number is a **maximum**, not a target. A profile only has so many requests to make — Tiny makes a single download request — so a small profile can finish without ever reaching the maximum you asked for. A profile needs at least as many requests as the count you ask for: on the download test that is 4 requests for Small, 10 for Medium, 60 for Large and 200 for Mega, so counts of 16 or 32 need Large or Mega to be reached at all.

Reach for it when:

- You are tuning your router's queue settings (SQM) and need every run to push the line the same way, so the before and after are comparable.
- You want the speed of one ordinary file transfer: that is `--connections 1`.
- You are on a fast link with a small data allowance — use Small or above with a count no higher than the profile's request count, such as `netpace --profile small --connections 4`. Small's whole download plan is only about 12 MiB, so it cannot overshoot by much whatever count you set. Avoid a high count on Large or Mega with a small `--downloadsize`: as the note below explains, that can move several times the cap.

> [!NOTE]\
> More connections means more data in flight when a cap is reached. NetPace stops scheduling transfers and cancels those still running once the total-byte cap is hit, so the bytes actually transferred can overshoot the profile's cap (or `--downloadsize` / `--uploadsize`) by roughly what was in flight at that moment — and that overshoot grows with the connection count. On a metered connection, choose the profile and the connection count together.

---

## Choosing a colour theme

A theme is a standing preference rather than a per-run choice, so you set it once with the `NETPACE_THEME` environment variable — in your shell profile, say — and every command picks it up:

```bash
export NETPACE_THEME=green
```

| Theme | What it looks like |
|---|---|
| `default` | NetPace's own colours. What you get when `NETPACE_THEME` is unset or empty. |
| `plain` | No colour and no bold anywhere. The live table still says how each test went, by its mark and its failed count. |
| `green` | A green-led look, in mid-tone greens chosen to read on a light terminal as well as a dark one. |

Names are matched without regard to case, so `GREEN` and `green` are the same request. Set the variable to something that is not a theme and NetPace says so on the error stream, naming the theme it is drawing in instead — the run is never stopped by a typo. `--quiet` suppresses that notice along with everything else.

The theme reaches every screen a person reads: the speed test, `servers` in all its forms, and `--help`. It never reaches output another program parses — `--csv` and `--json` are byte-for-byte the same whatever the theme is. `--file` output is always written without colour, and `--simple` has no colour to change.

Colour never carries meaning on its own in anything NetPace composes itself, and there is no exception. A failure is marked by the word `Error:`, and in the live table each test carries a mark of its own — `✓` for a clean run, `!` where some requests failed, `✗` where every one did or the measurement could not be taken at all. Where a test reports how many of its requests failed, a `Failed` column appears beside the marks with the count. So the table reads the same where you cannot tell the colours apart or have turned colour off entirely. Where the terminal cannot draw those marks either, `OK`, `!` and `X` stand in for them.

If you have set `NO_COLOR` — the [convention](https://no-color.org/) for asking every program on the machine to stop colouring its output — that wins, and NetPace draws the `plain` theme whatever `NETPACE_THEME` says.

---

## Detecting failed measurements

A speed test runs many small requests in parallel and reports the aggregate throughput. If some of those requests fail (a dropped connection, a TLS error, a timeout, or a server that rejects the transfer), NetPace does **not** silently treat them as zero-speed data — it counts them, so you can tell a genuinely slow link from a server that isn't transferring at all. When *every* request in a test fails, the speed reads zero, and the counts tell you it was a total failure rather than a 0 bps link.

Every output format carries the counts:

**Default** — a `Failed` column appears as soon as a test finishes with failures, and the test's mark says which kind of failure it was. A test whose every request failed reports a zero speed, and no latency under load: its probes never saw a loaded line, so those cells stay blank.
```
    Test         Done       Speed  Latency  Jitter    Failed
✓   Pinging      100%                24 ms    3 ms
!   Downloading  100%  512.6 Mbps    60 ms    4 ms  1 of 150
✗   Uploading    100%       0 bps                   32 of 32
```

**`--simple`** — the same annotation, on the line of the test it belongs to:
```
Ping: 24 ms
Download: 512.6 Mbps
Upload: 0 bps (32 of 32 requests failed)
```

**CSV** — a `Succeeded` and `Failed` column sits next to each speed column, and a total failure reports a speed of `0` beside the counts that say so. Its latency-under-load cells are empty rather than `0`: a transfer whose every request failed never loaded the line, so there was nothing to measure.
```
Timestamp,LatencyMilliseconds,JitterMilliseconds,DownloadSpeedBitsPerSecond,DownloadSucceeded,DownloadFailed,DownloadLatencyMilliseconds,DownloadLowestLatencyMilliseconds,DownloadHighestLatencyMilliseconds,DownloadJitterMilliseconds,UploadSpeedBitsPerSecond,UploadSucceeded,UploadFailed,UploadLatencyMilliseconds,UploadLowestLatencyMilliseconds,UploadHighestLatencyMilliseconds,UploadJitterMilliseconds,IPAddress,Hostname,ServerLocation,ServerSponsor,ServerUrl,DeviceId
1980-01-01T10:05:00+01:00,24,5,512600000,150,0,61,34,148,14,0,0,32,,,,,192.168.1.1,test-host,London,Foo Telecom,http://speedtest.foo.example:8080/speedtest/upload.php,
```

**JSON** — each test that ran gains integer `…Succeeded` / `…Failed` fields alongside its speed. A total failure reports `0` with the counts beside it, exactly as the other formats do, and leaves out its latency-under-load fields, since nothing was measured. Only a test you skipped (`--no-download`, `--no-upload`) omits its fields entirely:
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

The result is byte-for-byte what the same invocation produces without the switch, in every output format — so adding `--diagnostics` to a script that parses `--csv` or `--json` cannot break the parsing. Without the switch, nothing is written to the error stream at all, except the notice about an unrecognised `NETPACE_THEME` (see [Choosing a colour theme](#choosing-a-colour-theme)). At a terminal with nothing redirected, the records appear after the result, leaving the live table intact.

`--diagnostics` works alongside whichever output format you chose, and is unaffected by `--quiet`. It never writes to the `--file` target.

Each line is one record, as `key=value` pairs (an excerpt — a real run also carries the `screening` and `latency` test blocks, with `server.selected` between them: the choice is recorded as soon as it is made, which is before the chosen server's latency is measured). If `NETPACE_THEME` names no theme, its one-line notice comes first on the same stream, ahead of the records:

```
ts=1980-01-01T10:05:00.000 event=run.start version=0.25.0 runtime=".NET 10.0.0" os="Linux 6.8.0-137-generic" arch=x64
ts=1980-01-01T10:05:00.000 event=run.invocation args="--json --diagnostics"
ts=1980-01-01T10:05:05.000 event=server.selected sponsor="Foo Telecom" location="London, GB" url=http://speedtest.foo.example:8080/speedtest/upload.php selection=auto-latency latency_ms=12
ts=1980-01-01T10:05:10.000 event=test.start test=upload
ts=1980-01-01T10:05:11.412 event=request test=upload seq=1 url=http://speedtest.foo.example:8080/speedtest/upload.php status=failed bytes=0 duration_ms=1203 reason="The SSL connection could not be established, see inner exception. <- Authentication failed because the remote party sent a TLS alert: 'HandshakeFailure'." exception=HttpRequestException
ts=1980-01-01T10:05:11.662 event=probe test=upload seq=1 url=http://speedtest.foo.example:8080/speedtest/latency.txt status=ok bytes=9 duration_ms=58
ts=1980-01-01T10:05:25.000 event=test.end test=upload requests=8 succeeded=0 failed=8 cancelled=0 bytes=0
ts=1980-01-01T10:05:30.000 event=run.end exit=0
```

What the records cover:

| Event | What it says |
| --- | --- |
| `run.start` | The NetPace version, the .NET runtime, the operating system and the architecture. |
| `run.invocation` | How NetPace was invoked. |
| `server.selected` | The server used, and `selection=` — `auto-latency`, `specified` or `first-in-list`. `latency_ms` is the screening figure that chose it, and is absent when latency played no part in choosing. |
| `test.start` / `test.end` | Which tests ran — `screening`, `latency`, `download`, `upload` — and, on `test.end`, the total `requests`, how many `succeeded`, `failed` or were `cancelled`, and the `bytes` the successful ones moved. Those totals cover exactly the `request` lines above them, so the two always reconcile. `probe` lines are not among them. |
| `request` | One per request, with its sequence number, address, outcome, bytes, duration and — where it did not succeed — the `reason` and, if an exception was involved, its `exception` type. `status=` is `ok`, `failed` or `cancelled`. |
| `probe` | One per latency probe sent *during* a download or upload, to measure the latency of the line while it is loaded. It carries a `request` line's fields, `status=` included, and is numbered in its own run of `seq` numbers, so it never displaces one of the transfer's requests — `event=` and `seq=` together identify a record within a test, since a probe and a transfer request can share a number. `--simple` and `--no-latency` both send no under-load probes, so a run with either carries no `probe` line at all. |

Every line repeats enough to stand alone, so `grep status=failed diagnostics.log` is a complete triage pass. Nothing is summarised away: every measured request gets its own record, including the latency probes made while choosing a server. Two kinds of request are not recorded individually: the initial server-list fetch, whose failure surfaces as the run's error instead; and a screening request that finishes after the selection ceiling has already passed, which arrives too late for the `test.end` it would belong to and is dropped so that summary stays exact — so a candidate you expected to see may be absent rather than untried. One request appears that you did not ask for: the upload test resolves its endpoint with an empty probe POST first, which takes the opening `seq` numbers of that test and reads `bytes=0`, so `test.end requests=` counts one more than the uploads measured (more, if the server redirects several times).

The download and upload tests also each send a small latency probe at most about four times a second while they run, to measure the latency of the line under load, and every one of those gets a `probe` line of its own — except under `--simple` or `--no-latency`, which send none. They are deliberately not `request` lines: a probe is something the test does alongside its measurement rather than part of it, so `test.end` totals none of them and keeps reconciling with the transfer's own `request` lines. A probe that was still waiting when the transfer ended reads `status=cancelled reason="transfer ended"`, and one that errored reads `status=failed` — neither fails the transfer, and a run of them means the under-load figure came from fewer probes than went out, or from none. These probe lines carry the raw under-load samples; the average and the jitter computed from them are on each transfer's row of the live table, and the lowest and highest are available only to `NetPace.Core` consumers, on the transfer result. A transfer shorter than the probe interval produces no probe line at all. The probes made while *choosing* the server, and then measuring it, are a different thing: there the probes are the measurement, so the `screening` and `latency` tests record them as ordinary `request` lines and count them.

Record volume is driven by how many servers the feed offers — roughly three records per candidate screened — on top of the request counts your `--profile` sets and up to about four probe lines per second of transfer, so expect a few hundred lines from a default run.

Under `--count` or `--loop`, `run.start`, `run.invocation` and `run.end` appear once, and the test and request records repeat per iteration.

### Exit codes

The exit code reports only whether **NetPace itself** functioned. Network conditions like a total outage, 100% request failure, or no servers found are *data*, not errors, and NetPace will exit `0`. Only an operational failure (for example, being unable to write the `--file` output) or a usage error (an unusable option value, or two output formats at once) exits non-zero. So a `0 bps` measurement still exits `0` by default: inspect the counts (or use `--fail-on`) to detect it.

**The rule covers every command, not just a speed test.** `netpace servers`, `netpace servers --latency` and `netpace servers --fastest` each report an empty server list, and a failed or timed-out attempt to fetch it — and exit `0` for both. A list no server answered on only arises where screening runs: `--fastest` reports it, `--latency` shows `-` against each row, and both exit `0`. Pressing Ctrl+C exits `0` as well, wherever you press it, and reports no error — it leaves whatever had already been drawn on screen. A script that wants to know whether servers were found reads the output, not the exit code.

If you want a failed measurement to fail the process, opt in with `--fail-on`. It applies to a speed test; the `servers` command has no opt-in of its own:

| Value | Exits non-zero when… |
|---|---|
| `none` | Never — measurement outcomes don't affect the exit code |
| `total` | A requested test is all-failed (no request succeeded) |
| `partial` | Any request in a requested test failed (strict; intended for pristine-run checks) |

A measurement that never ran at all — the network was down, or no server could be reached — counts as worse than an all-failed one, so it meets either threshold.

`--fail-on` is fail-fast and uniform across single runs, `--count`, and `--loop`: NetPace exits `1` at the first measurement that meets the threshold.

```bash
# Treat a totally-failed test as a failure
netpace --fail-on total
```

---

For more options and details, run:  
```bash
netpace --help
```

To report problems or suggest features, visit [GitHub Issues](https://github.com/FrankRay78/NetPace/issues).
