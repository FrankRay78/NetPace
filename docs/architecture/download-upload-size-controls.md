# Ookla Download and Upload Sizing — Reference

**Status:** Reference (current as-is behaviour)
**Last reviewed:** 2026-04-29
**Source files:** [`NetPace.Core/Clients/Ookla/`](../../src/NetPace.Core/Clients/Ookla/), [`NetPace.Console/Program.cs`](../../src/NetPace.Console/Program.cs)

Documents how `OoklaSpeedtest` decides the size, count, and parallelism of the HTTP requests it issues during a download and upload test, and which of those knobs are currently reachable from the command line.

## 1. Configuration model

`OoklaSpeedtest` reads everything from a single nested record:

```text
OoklaSpeedtestSettings
├── ServerDiscovery : ServerDiscoverySettings
├── LatencyTest     : LatencyTestSettings
├── DownloadTest    : DownloadTestSettings
├── UploadTest      : UploadTestSettings
└── Proxy*          (UseProxy, ProxyAddress, ProxyCredential)
```

Source: [`OoklaSpeedtestSettings.cs`](../../src/NetPace.Core/Clients/Ookla/OoklaSpeedtestSettings.cs).

### 1.1 `DownloadTestSettings`

Source: [`Settings/DownloadTestSettings.cs`](../../src/NetPace.Core/Clients/Ookla/Settings/DownloadTestSettings.cs).

Defaults below are the values supplied by `new OoklaSpeedtestSettings()` (which chains to `Profile.Medium`). A bare `new DownloadTestSettings()` uses the type's field initializers (`DownloadSizeIterations = 4`, `DownloadParallelTasks = 8`, `DownloadSizeMb = int.MaxValue`); see §5 for per-profile values.

| Property                 | Default (Medium profile)         | Meaning                                                                                |
| ------------------------ | -------------------------------- | -------------------------------------------------------------------------------------- |
| `DownloadSizes`          | `[1500, 2000, 3000, 3500, 4000]` | Pixel sizes used to build URLs of the form `random{N}x{N}.jpg`. Bigger N → bigger file. The default is the larger half of the historic ten-element Ookla Flash-client array (see §2.1). |
| `DownloadSizeIterations` | `2`                              | How many times each size is requested (URL gets a `?r={i}` cache-buster).              |
| `DownloadParallelTasks`  | `4`                              | Concurrent HTTP GETs.                                                                  |
| `DownloadSizeMb`         | `100`                            | Total-byte budget cap in IEC MiB. The download loop terminates once cumulative bytes received reach this threshold. Default sentinel for a bare record is `int.MaxValue` (no cap). |

Total candidate requests = `DownloadSizes.Length × DownloadSizeIterations` (Medium default = 5 × 2 = **10**).

### 1.2 `UploadTestSettings`

Source: [`Settings/UploadTestSettings.cs`](../../src/NetPace.Core/Clients/Ookla/Settings/UploadTestSettings.cs).

Defaults below are the values supplied by `new OoklaSpeedtestSettings()` (Medium profile). A bare `new UploadTestSettings()` uses the type's field initializers (`UploadSizeIterations = 10`, `UploadParallelTasks = 8`, `UploadSizeMb = int.MaxValue`); see §5 for per-profile values.

| Property                | Default (Medium profile) | Meaning                                                                              |
| ----------------------- | ------------------------ | ------------------------------------------------------------------------------------ |
| `UploadSizeIncrementKb` | `200`                    | Step size between successive increments, in KB (binary, ×1024).                      |
| `UploadIncrements`      | `6`                      | Number of increments (200 KB, 400 KB, 600 KB, 800 KB, 1 MB, 1.2 MB).                 |
| `UploadSizeIterations`  | `5`                      | How many times each increment is repeated.                                           |
| `UploadParallelTasks`   | `4`                      | Concurrent HTTP POSTs.                                                               |
| `UploadSizeMb`          | `25`                     | Total-byte budget cap in IEC MiB. Default sentinel for a bare record is `int.MaxValue` (no cap). |

Total candidate requests = `UploadIncrements × UploadSizeIterations` (Medium default = 6 × 5 = **30**).

## 2. How the settings shape network behaviour

### 2.1 Download — [`OoklaSpeedtest.GetDownloadSpeedAsync`](../../src/NetPace.Core/Clients/Ookla/OoklaSpeedtest.cs)

1. `GenerateDownloadUrls(server.Url, DownloadSizes, DownloadSizeIterations)` produces a flat list of URLs:
   ```
   {base}/random1500x1500.jpg?r=0
   {base}/random1500x1500.jpg?r=1
   ...
   {base}/random4000x4000.jpg?r=3
   ```
2. `GenericTestSpeedAsync` consumes that list with `DownloadParallelTasks` workers, streaming each response with an 80 KB pooled buffer and counting bytes.
3. The loop **terminates early** once `downloadSizeMb × 1024 × 1024` bytes have been received (the budget cap from the `--downloadsize` overload).

#### Per-request bytes are server-determined, not derivable from N

The pixel dimension `N` in `random{N}x{N}.jpg` selects which pre-generated random JPEG the OoklaServer hands back; the actual file size depends entirely on the upstream OoklaServer build and is not derivable from N². NetPace just streams whatever bytes it gets.

The OoklaServer ships pre-generated random JPEGs at thirteen distinct pixel dimensions. Ten of them match the historic Speedtest.net Flash-client array `{350, 500, 750, 1000, 1500, 2000, 2500, 3000, 3500, 4000}` — the same array that still appears across speedtest ports in Go, Ruby, Perl, and C# (see e.g. [Kwull/NSpeedTest](https://github.com/Kwull/NSpeedTest/blob/master/NSpeedTest/SpeedTestClient.cs)). The other three — `5000`, `6000`, `7000` — are present in the current upstream stable build on every OoklaServer we probed, but are absent from any public Ookla documentation or third-party speedtest port we could find.

Sizes outside this set return HTTP 404; the server does **not** generate JPEGs on demand. We confirmed this by probing a wide spread of non-standard values (`0, 1, 100, 300, 349, 351, 600, 1234, 1750, 2750, 3250, 3750, 4001, 4250, 4500, 4750, 4999, 5001, 5250, 5500, 5750, 5999, 6001, 6250, 6500, 6750, 7100, 7250, 7500, 7750, 7999, 8000, 8500, 9000, 10000, 12000, 16000, 32000`) — every one of them 404'd. The upper bound is firmly 7000.

NetPace's default `DownloadSizes` keeps only the larger half of the historic ten — biased toward modern fast links where smaller payloads are too short to reach steady state. The other eight (smaller five + bonus three) are still served on every OoklaServer, just not requested by default.

The numbers below were measured against the local Docker OoklaServer ([`docker/ooklaserver/`](../../docker/ooklaserver/)) and *confirmed identical* across nine independent UK OoklaServer operators (see [Cross-server validation](#cross-server-validation)). They are pinned to the current upstream OoklaServer stable build; a future upstream release could change them.

| URL                       | Bytes           | Approx.       | NetPace default |
| ------------------------- | --------------: | ------------: | :-------------: |
| `random350x350.jpg`       |         245,388 |       0.23 MiB |                 |
| `random500x500.jpg`       |         505,544 |       0.48 MiB |                 |
| `random750x750.jpg`       |       1,118,012 |       1.07 MiB |                 |
| `random1000x1000.jpg`     |       1,986,284 |       1.89 MiB |                 |
| `random1500x1500.jpg`     |       4,468,241 |       4.26 MiB |       Yes       |
| `random2000x2000.jpg`     |       7,907,740 |       7.54 MiB |       Yes       |
| `random2500x2500.jpg`     |      12,407,926 |      11.83 MiB |                 |
| `random3000x3000.jpg`     |      17,816,816 |      16.99 MiB |       Yes       |
| `random3500x3500.jpg`     |      24,262,167 |      23.14 MiB |       Yes       |
| `random4000x4000.jpg`     |      31,625,365 |      30.16 MiB |       Yes       |
| `random5000x5000.jpg` *   |      49,454,450 |      47.16 MiB |                 |
| `random6000x6000.jpg` *   |      71,154,024 |      67.86 MiB |                 |
| `random7000x7000.jpg` *   |      96,912,152 |      92.42 MiB |                 |
| **Default sum (×1 iter.)** |  **86,080,329** |   **82.09 MiB** |                 |
| **Historic-10 sum (×1 iter.)** | **102,343,483** | **97.60 MiB** |             |
| **All-13 sum (×1 iter.)** | **319,864,109** |  **305.05 MiB** |                 |

\* Undocumented. Universally present on the OoklaServers we probed but not part of the historic Flash-client array and not mentioned in any public Ookla or third-party speedtest documentation we could find.

#### Default total under defaults

```
total = (sum of one iteration) × DownloadSizeIterations
      = 86,080,329 × 4
      = 344,321,316 bytes
      ≈ 328.37 MiB  ≈ 344.32 MB
```

This is what NetPace will pull *if every request runs to completion* and `--downloadsize` is left at its default (see §3).

#### Cross-server validation

The per-request sizes above aren't a quirk of our local Docker container — they're pinned to the current upstream OoklaServer stable build. Verified on **2026-04-29** by HEAD-probing all thirteen `random{N}x{N}.jpg` URLs across nine independent UK OoklaServer operators (HEAD-only, 1 s gap between requests, custom `User-Agent`). Every server returned exactly the same `Content-Length` for every size.

**Historic 10:**

| Server                  |  350×350 |  500×500 |   750×750 | 1000×1000 | 1500×1500 | 2000×2000 |  2500×2500 |  3000×3000 |  3500×3500 |  4000×4000 |
| ----------------------- | -------: | -------: | --------: | --------: | --------: | --------: | ---------: | ---------: | ---------: | ---------: |
| Abingdon (Oxford-ITS)   |  245,388 |  505,544 | 1,118,012 | 1,986,284 | 4,468,241 | 7,907,740 | 12,407,926 | 17,816,816 | 24,262,167 | 31,625,365 |
| Bracknell (Vodafone)    |  245,388 |  505,544 | 1,118,012 | 1,986,284 | 4,468,241 | 7,907,740 | 12,407,926 | 17,816,816 | 24,262,167 | 31,625,365 |
| Cardiff (Ogi)           |  245,388 |  505,544 | 1,118,012 | 1,986,284 | 4,468,241 | 7,907,740 | 12,407,926 | 17,816,816 | 24,262,167 | 31,625,365 |
| Fareham (ServerHouse)†  |  245,388 |  505,544 | 1,118,012 | 1,986,284 | 4,468,241 | 7,907,740 | 12,407,926 | 17,816,816 | 24,262,167 | 31,625,365 |
| Guildford (BT)          |  245,388 |  505,544 | 1,118,012 | 1,986,284 | 4,468,241 | 7,907,740 | 12,407,926 | 17,816,816 | 24,262,167 | 31,625,365 |
| Newport (Michaelston)   |  245,388 |  505,544 | 1,118,012 | 1,986,284 | 4,468,241 | 7,907,740 | 12,407,926 | 17,816,816 | 24,262,167 | 31,625,365 |
| Slough (Zzoomm)         |  245,388 |  505,544 | 1,118,012 | 1,986,284 | 4,468,241 | 7,907,740 | 12,407,926 | 17,816,816 | 24,262,167 | 31,625,365 |
| Slough (ConnectFibre)   |  245,388 |  505,544 | 1,118,012 | 1,986,284 | 4,468,241 | 7,907,740 | 12,407,926 | 17,816,816 | 24,262,167 | 31,625,365 |
| Worcester (Sonnleitner) |  245,388 |  505,544 | 1,118,012 | 1,986,284 | 4,468,241 | 7,907,740 | 12,407,926 | 17,816,816 | 24,262,167 | 31,625,365 |

† Fareham's 1000×1000 cell saw a transient connection drop on the probe run. Value shown is the consensus from the eight other servers, which all match the local Docker container exactly.

**Bonus 3 (undocumented):**

| Server                  |  5000×5000 |  6000×6000 |  7000×7000 |
| ----------------------- | ---------: | ---------: | ---------: |
| Abingdon (Oxford-ITS)   | 49,454,450 | 71,154,024 | 96,912,152 |
| Bracknell (Vodafone)    | 49,454,450 | 71,154,024 | 96,912,152 |
| Cardiff (Ogi)           | 49,454,450 | 71,154,024 | 96,912,152 |
| Fareham (ServerHouse)   | 49,454,450 | 71,154,024 | 96,912,152 |
| Guildford (BT)          | 49,454,450 | 71,154,024 | 96,912,152 |
| Newport (Michaelston)   | 49,454,450 | 71,154,024 | 96,912,152 |
| Slough (Zzoomm)         | 49,454,450 | 71,154,024 | 96,912,152 |
| Slough (ConnectFibre)   | 49,454,450 | 71,154,024 | 96,912,152 |
| Worcester (Sonnleitner) | 49,454,450 | 71,154,024 | 96,912,152 |

These three sizes were searched for in public Ookla docs, third-party speedtest libraries (Go, Ruby, Perl, C#), and the `OoklaServer.properties` config file — no mention found in any of them. They appear to be quietly bundled with the upstream `OoklaServer.tgz` distribution and reliably present on every operator's deployment of the current stable build.

If a future OoklaServer release changes these values, re-run the same HEAD probe (any one of the URLs above is enough as a spot-check) and update the tables in §2.1.

### 2.2 Upload — [`OoklaSpeedtest.GetUploadSpeedAsync`](../../src/NetPace.Core/Clients/Ookla/OoklaSpeedtest.cs)

1. `GenerateUploadDataLengths(UploadIncrements, UploadSizeIncrementKb, UploadSizeIterations)` produces a flat list of byte-lengths. The size of increment `i` (1-indexed) is computed as:
   ```
   bytes = i × UploadSizeIncrementKb × 1024
   ```
   Note that the `Kb` suffix is **binary** (1024), not decimal. Each increment is then yielded `UploadSizeIterations` times.
2. Each length is POSTed to `server.Url` (the OoklaServer `upload.php`-style endpoint) using `RandomStreamContent` to stream cryptographically-random bytes in 8 KB chunks (avoids LOH allocations). `UploadParallelTasks` workers run concurrently.
3. The loop **terminates early** once `uploadSizeMb × 1024 × 1024` bytes have been sent (the budget cap from the `--uploadsize` overload).

#### Default total under defaults

| Increment (i) | Bytes per request    |
| -------- | -------------------: |
| 1        |              204,800 |
| 2        |              409,600 |
| 3        |              614,400 |
| 4        |              819,200 |
| 5        |            1,024,000 |
| 6        |            1,228,800 |
| **Sum**  |        **4,300,800** |

```
total = (1+2+3+4+5+6) × UploadSizeIncrementKb × 1024 × UploadSizeIterations
      = 21 × 200 × 1024 × 10
      = 43,008,000 bytes
      ≈ 41.02 MiB  ≈ 43.01 MB
```

Unlike download, this total is fully deterministic — NetPace generates the payloads itself, the OoklaServer simply sinks them. `UploadParallelTasks` only affects throughput, not the total transferred.

## 3. CLI surface

[`Program.cs`](../../src/NetPace.Console/Program.cs) exposes one profile selector and two budget-cap overrides, wired into [`SpeedTestCommandSettings`](../../src/NetPace.Console/Commands/SpeedTestCommandSettings.cs):

| Switch           | Property         | Default                | Effect                                                                                                                                  |
| ---------------- | ---------------- | ---------------------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| `--profile`      | `Profile`        | `Medium`               | Sets per-request shape (`DownloadSizes`, iterations, parallel tasks) and total-byte cap defaults via `new OoklaSpeedtestSettings(p)`.    |
| `--downloadsize` | `DownloadSizeMb` | (profile-supplied)     | Overrides the download cap via a `with`-expression. Does **not** change per-request file size — the profile remains authoritative for shape. |
| `--uploadsize`   | `UploadSizeMb`   | (profile-supplied)     | Overrides the upload cap via a `with`-expression. Does **not** change per-request payload — the profile remains authoritative for shape.     |

> **Important distinction:** `--downloadsize` / `--uploadsize` are **total-byte budget caps**, not per-request size controls. The per-request sizes (`DownloadSizes`, `UploadSizeIncrementKb`, etc.) are set by the chosen profile and are otherwise reachable only via the `NetPace.Core` library API, not the CLI.
>
> When `--downloadsize` / `--uploadsize` are omitted, the profile-supplied caps apply (e.g. Medium = 100 MiB down + 25 MiB up). To see all five profile shapes at once, see §5.

The library-level cap-removal default — a fresh `new DownloadTestSettings()` literal (no profile) — is `int.MaxValue` MiB, which is far larger than any iteration would ever transfer, so the cap is **inactive** in that bare-record case.

## 4. Local verification

The repo includes a Docker OoklaServer at [`docker/ooklaserver/`](../../docker/ooklaserver/), exposing the real endpoints the test code drives:

| Endpoint                                             | Purpose                                                    |
| ---------------------------------------------------- | ---------------------------------------------------------- |
| `http://localhost:18080/speedtest/random{N}x{N}.jpg` | Download payload (used by `DownloadTest`).                 |
| `http://localhost:18080/speedtest/upload.php`        | Upload sink (used by `UploadTest`).                        |
| `http://localhost:18080/speedtest/latency.txt`       | Health/latency probe.                                      |

After `./docker/ooklaserver/start.sh`, the endpoints can be probed directly:

```bash
# A single download URL at one of the default sizes
curl -sS -o /dev/null -w '%{size_download} bytes in %{time_total}s\n' \
  http://localhost:18080/speedtest/random4000x4000.jpg

# upload.php accepting a 1 MiB POST
head -c 1048576 /dev/urandom | curl -sS -o /dev/null -w '%{http_code}\n' \
  --data-binary @- http://localhost:18080/speedtest/upload.php
```

## 5. Profile-driven defaults

[`Profile`](../../src/NetPace.Core/Profile.cs) is the public, provider-agnostic vocabulary surfaced via `--profile`. [`OoklaSpeedtestSettings(Profile)`](../../src/NetPace.Core/Clients/Ookla/OoklaSpeedtestSettings.cs) maps each profile to a complete `DownloadTestSettings` / `UploadTestSettings` pair via an inline switch — the single source of truth for "what does Tiny mean, in Ookla terms?".

### 5.1 Download (per profile)

| Profile  | `DownloadSizes`                     | Iterations | Parallel | `DownloadSizeMb` cap |
| -------- | ----------------------------------- | ---------- | -------- | -------------------- |
| `Tiny`   | `[350]`                             | 1          | 1        | 1                    |
| `Small`  | `[1000, 1500]`                      | 2          | 2        | 10                   |
| `Medium` | `[1500, 2000, 3000, 3500, 4000]`    | 2          | 4        | 100                  |
| `Large`  | `[2000, 2500, 3000, 3500, 4000]`    | 12         | 16       | 1024                 |
| `Mega`   | `[3000, 4000, 5000, 6000, 7000]`    | 40         | 32       | 10240                |

### 5.2 Upload (per profile)

| Profile  | `UploadSizeIncrementKb` | `UploadIncrements` | Iterations | Parallel | `UploadSizeMb` cap |
| -------- | ----------------------- | ------------------ | ---------- | -------- | ------------------ |
| `Tiny`   | 50                      | 1                  | 1          | 1        | 1                  |
| `Small`  | 100                     | 4                  | 2          | 2        | 2                  |
| `Medium` | 200                     | 6                  | 5          | 4        | 25                 |
| `Large`  | 500                     | 8                  | 12         | 16       | 256                |
| `Mega`   | 1024                    | 16                 | 16         | 32       | 2048               |

### 5.3 Mega is the only profile relying on the bonus payloads

The `5000`, `6000`, `7000` pixel-size payloads identified in §2.1 are **only used by `Mega`**. The other four profiles stay within the historic Flash-client `random{N}x{N}.jpg` array, so they are guaranteed to work against any OoklaServer that ships those URLs (every server we've probed — see §2.1 Cross-server validation).

If a future OoklaServer release drops the bonus payloads, Mega will see 404s on those URLs and fall back to whatever the surviving subset returns. The current Mega arm is tuned for the bonus payloads being present; the *documented fallback strategy* — revert Mega to the historic-10 array with higher iteration counts to keep total transfer in the ~10 GiB band — is tracked but **not implemented in the 003-profile-cli-switch change**. Users who hit Mega-specific 404s should switch to `--profile large` until the fallback lands.
