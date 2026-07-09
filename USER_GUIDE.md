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

## Choosing a profile

The `--profile` flag bundles per-request payload sizes, parallelism, and a total-byte
cap into one switch. Pick the profile that matches your link, and NetPace adapts the
traffic shape to suit it. `Medium` is the default.

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

You can still pin a hard cap on top of a profile — the profile sets per-request shape,
`--downloadsize` / `--uploadsize` override only the total cap:

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

For more options and details, run:  
```bash
NetPace --help
```

To report problems or suggest features, visit [GitHub Issues](https://github.com/FrankRay78/NetPace/issues).
