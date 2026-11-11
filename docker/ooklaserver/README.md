# Local OoklaServer (Docker)

A self-contained Ookla Speedtest server for NetPace to run tests against.

OoklaServer is Ookla's official speed-test daemon. It exposes the HTTP-Legacy endpoints used by NetPace — `/speedtest/latency.txt`, `/speedtest/random{N}x{N}.jpg`, and `/speedtest/upload.php` — on port 8080.

---

## Contents

| File                 | Purpose                                             |
| -------------------- | --------------------------------------------------- |
| `Dockerfile`         | Builds an image with the latest stable OoklaServer. |
| `docker-compose.yml` | Declarative container config (port mapping, restart policy, healthcheck). |
| `start.sh`           | Build image, start container, wait until ready, print NetPace command. |
| `stop.sh`            | Stop and remove the container.                      |
| `link-profiles.sh`   | The five slow-link profiles, as `tc netem` arguments. Sourced by `start.sh` and `shape.sh`. |
| `shape.sh`           | Container entry point: applies the requested profile, then starts OoklaServer. |
| `lab-properties.sh`  | Run when the image is built: turns off the OoklaServer settings that would make the server reach out to the internet on its own. |
| `lab-properties.tests.sh` | Test matrix for `lab-properties.sh`.           |

---

## Prerequisites

- Docker Desktop (Windows/macOS) or Docker Engine (Linux), running. The slow-link profiles are confirmed on Linux Docker Engine only — see *Confirmed host platforms*.
- Host TCP and UDP port 18080 free.
- `bash` and `curl` on PATH (Git Bash, WSL, macOS, Linux — all fine).

---

## Quick start

```bash
./start.sh
```

When ready, the script prints the exact NetPace command:

```bash
netpace --server-url http://localhost:18080/speedtest/upload.php
```

To stop:

```bash
./stop.sh
```

Re-running `start.sh` rebuilds the image each time (`--build`), so container upgrades happen by stopping and starting again. Force a clean image pull with:

```bash
docker compose build --no-cache
./start.sh
```

---

## Slow link profiles

NetPace is developed on fast networks, and its unit tests fake HTTP responses, so nothing in the suite exercises a poor link. These profiles close that gap: they put the same local OoklaServer behind a deliberately bad network so you can run the real `netpace` against it and see what a user on that link would see.

```bash
./start.sh satellite
```

`./start.sh` with no argument still gives you the full-speed local server. `./start.sh --help` lists the profiles.

To change profile, start again on another; every start replaces whatever was running with exactly the state you asked for, so there is no way to end up on yesterday's profile. The ready message always names the profile in effect, or says there is none.

The five profiles are fixed. There is deliberately no way to set values of your own: a profile you can edit is a profile nobody else can reproduce.

### What each models

| Profile      | Models                              | Round trip                | Down / up         | Loss         |
| ------------ | ----------------------------------- | ------------------------- | ----------------- | ------------ |
| `satellite`  | Geostationary satellite             | about 600 ms              | 10 Mbit / 2 Mbit  | about 0.5%   |
| `3g-bloated` | Congested 3G with full queues       | about 2 s, with jitter    | 1 Mbit / 384 kbit | about 1%     |
| `gprs`       | 2G data                             | about 700 ms, with jitter | 40 kbit / 20 kbit | about 1%     |
| `radio`      | Low-rate radio link                 | about 1 s                 | 9.6 kbit each way | about 2%     |
| `flaky`      | A usable link that keeps failing    | about 200 ms              | 2 Mbit / 1 Mbit   | ~2%, bursty  |

### Exact per-direction values

Round trip, jitter and loss above are end-to-end figures. They are split evenly between the two directions, so each direction carries half of each. Rates are not split — they are per-direction figures already, because a real link's downstream and upstream rates are independent.

"Down" shapes traffic leaving the container, which is what NetPace downloads; "up" shapes traffic entering it, which is what NetPace uploads.

| Profile      | Down (`tc netem` arguments)                                          | Up (`tc netem` arguments)                                              |
| ------------ | -------------------------------------------------------------------- | ---------------------------------------------------------------------- |
| `satellite`  | `delay 300ms loss 0.25% rate 10mbit`                                 | `delay 300ms loss 0.25% rate 2mbit`                                    |
| `3g-bloated` | `delay 1000ms 200ms distribution normal loss 0.5% rate 1mbit`        | `delay 1000ms 200ms distribution normal loss 0.5% rate 384kbit`        |
| `gprs`       | `delay 350ms 75ms distribution normal loss 0.5% rate 40kbit`         | `delay 350ms 75ms distribution normal loss 0.5% rate 20kbit`           |
| `radio`      | `delay 500ms loss 1% rate 9600bit`                                   | `delay 500ms loss 1% rate 9600bit`                                     |
| `flaky`      | `delay 100ms loss gemodel 2% 50% 30% 0% rate 2mbit`                  | `delay 100ms loss gemodel 2% 50% 30% 0% rate 1mbit`                    |

`link-profiles.sh` is the single source of these values; the table above is a copy for reading.

`flaky` uses `netem`'s Gilbert-Elliott loss model (`loss gemodel p r 1-h 1-k`) rather than a flat percentage, so its loss arrives in clumps: 2% chance of entering a bad state, 50% of leaving it, dropping 30% of packets while in it. That averages a little over 1% per direction, but in bursts — which is what breaks a retry policy that uniform loss survives.

### The rate is the link, not the result

Each rate is the capacity of the pipe, not the throughput you will measure across it. Delay and loss cost TCP a great deal on their own: on `satellite`, a 10 Mbit pipe with a 600 ms round trip delivers a small fraction of that to a single connection, because the transfer spends its life in slow start and every loss costs most of a second to recover. That gap is not a fault in the profile — it is the thing the profiles exist to show you.

### Automatic server selection is not slowed

Only traffic to and from the container crosses the shaped link. The server list still comes from the public internet at full speed, so **automatic server selection is unaffected** and runs as fast as it always did. To put a measurement on the slow link you must point NetPace at the container explicitly:

```bash
netpace --server-url http://localhost:18080/speedtest/upload.php --profile tiny
```

`--profile tiny` is worth the habit here. On the narrower profiles NetPace's default transfer sizes take a very long time — at 9.6 kbit, a single default-sized request is a wait measured in hours.

### The server makes no traffic of its own

The server itself adds no traffic to the shaped link. OoklaServer would otherwise reach out to the internet while it starts: it attempts a Let's Encrypt certificate about 17 seconds in, and it checks for updates to itself. On a full-speed link neither is noticeable. At 9.6 kbit a single TLS handshake costs seconds, and that traffic arrives after the container is healthy and after `start.sh` has said the lab is ready, so a run started at that moment measured the server's start-up rather than the link. On `radio` its first latency probe used up NetPace's 10 second limit every time.

The image turns both settings off, in `lab-properties.sh` when the image is built, so nothing in the lab reaches the internet once it is running. The server log says `Server updates were disabled.` and carries no certificate lines. If a later OoklaServer release drops either setting's name from the properties file, the image build fails rather than quietly restoring the traffic.

What still crosses the link is the lab's own readiness poll: `start.sh` confirms the published port by fetching `latency.txt` over it, and on the narrowest profiles those bytes are still draining when it prints the ready message. On `radio` that costs the first latency probe a few seconds: about 6 s, against NetPace's 10 second limit. **So the readiness poll accounts for a few seconds of the first probe, not for a latency stage that fails outright.** Look at the link and at NetPace's limits instead.

### Confirmed host platforms

The profiles were confirmed on **Linux Docker Engine** (Ubuntu 24.04, kernel 6.8, Docker 29.9). That is the supported configuration.

Docker Desktop for Windows and macOS run containers inside a virtual machine, and the upload direction needs the host kernel's `ifb` module, which is not guaranteed there. They are **unconfirmed**. If shaping cannot be applied in full on your host you are told so and nothing starts — see *How it works, and what it refuses to do* below — so an unconfirmed platform costs you a clear error, never a wrong measurement.

### How it works, and what it refuses to do

`shape.sh` runs as the container's entry point and applies the profile with `tc netem` before OoklaServer accepts its first connection. The container needs the `NET_ADMIN` capability, which `docker-compose.yml` grants; `shape.sh` drops it again before starting OoklaServer, so the server never holds it, with or without a profile. The download direction is plain egress `netem` on the container's interface; `netem` cannot shape ingress, so the upload direction redirects incoming traffic through an `ifb` device and shapes that device's egress instead.

Shaping is applied at container start, not from the host afterwards, because it has to be re-asserted on every start and the container carries `restart: unless-stopped`. An unattended restart therefore comes back on the same profile rather than coming back fast while still claiming to be slow.

**OoklaServer is not started unless both directions are shaped.** If either fails, the container exits non-zero, `start.sh` prints the log tail and removes the container, and you get a non-zero exit. A half-shaped server is worse than an unshaped one: you would read a plausible number off a link that is only slow one way, with nothing on screen to say so.

An unknown profile name is refused before anything is built, with the list of available profiles and a non-zero exit.

The container's healthcheck uses loopback, which shaping does not touch, so it stays fast and honest on every profile and reports on OoklaServer itself rather than on the link. `start.sh` polls the published port instead — the address you are about to use — as a liveness check on it.

---

## Manual operation

Without the scripts:

```bash
# Build and start
docker compose up -d --build

# Logs
docker compose logs -f ooklaserver

# Stop and remove
docker compose down
```

Verify the HTTP-Legacy endpoints directly:

```bash
curl -s http://localhost:18080/speedtest/latency.txt
# Body starts with: test=test

curl -s -o /dev/null -w "%{http_code}\n" http://localhost:18080/speedtest/random1500x1500.jpg
# Expected: 200
```

On Windows `cmd.exe`, replace `/dev/null` with `NUL`.

---

## LAN access

To let other machines on the LAN test against the container:

1. Allow inbound TCP/UDP 18080 on the host firewall.
2. Clients point at the host's LAN IP, e.g.

   ```bash
   netpace --server-url http://192.168.1.50:18080/speedtest/upload.php
   ```

Throughput is bounded by host NIC, Docker's networking layer, and container CPU.

---

## Changing the host port

The container listens on 8080 internally and publishes on host port 18080 by default (chosen to avoid the common 8080 conflict with other ASP.NET/dev tooling). To use a different host port, edit the left side of the mappings in `docker-compose.yml`:

```yaml
    ports:
      - "28080:8080/tcp"
      - "28080:8080/udp"
```

Then point NetPace at the new port:

```bash
netpace --server-url http://localhost:28080/speedtest/upload.php
```

Update `start.sh` (the polling URL) to match if you want the readiness check to continue working.

---

## Troubleshooting

### `start.sh` refuses to start a profile

A container that stops or restarts before becoming ready, followed by a log tail whose `shape: ERROR:` line names the direction that could not be shaped, means the host could not apply the profile in full. The container is removed rather than left serving an unshaped link. Usual causes:

- The `NET_ADMIN` capability is missing — check `cap_add` in `docker-compose.yml` is intact, and that no local override drops it.
- The host kernel has no `ifb` module, so the upload direction cannot be shaped. Expected on Docker Desktop for Windows and macOS; see *Confirmed host platforms* above.

The full-speed server is unaffected — `./start.sh` with no profile still works on any host.

### `start.sh` times out waiting for OoklaServer

`start.sh` prints the last 50 log lines itself and then removes the container, so read that tail — `docker compose logs` afterwards has nothing left to show. To keep the container for inspection, start it manually with `NETPACE_LINK_PROFILE=<profile> docker compose up -d --build` instead — leave the variable out for the full-speed server. Without it the container starts unshaped, so a timeout that only the profile causes will not reproduce. Ctrl-C during `start.sh`'s wait also removes the container, as a failed start does.

Common causes: host port conflict (another service on 18080), OoklaServer out-of-date warning that aborts startup, or missing AES-NI CPU instructions (required by OoklaServer).

### "Server returned incorrect test string for latency.txt" from NetPace

A different HTTP server is bound to the host port. Find it:

- Windows: `netstat -ano | findstr :18080`
- Linux/macOS: `ss -ltnp | grep 18080`

A very common culprit on Windows is a locally running ASP.NET / Kestrel app — you can recognise it by a `Server: Kestrel` response header from `curl -v`.

Stop the interfering service or change the host port (see above).

### `docker build` fails downloading the tarball

Outbound HTTPS to `install.speedtest.net` is required. Behind a proxy:

```bash
docker build \
  --build-arg HTTPS_PROXY=$HTTPS_PROXY \
  -t local-ooklaserver .
```

### Container exits immediately

Run in the foreground to see the error directly:

```bash
docker run --rm -it -p 8080:8080 local-ooklaserver

# The same, behind a profile
docker run --rm -it -p 8080:8080 --cap-add NET_ADMIN -e NETPACE_LINK_PROFILE=<profile> local-ooklaserver
```

---

## Appendix: inspecting the container

Quick reference for poking around the running container. No volume is mounted by default, so anything written inside the container is lost on `./stop.sh`.

### Open a shell

```bash
docker exec -it ooklaserver bash
```

Drops you in at `/opt/ookla`. `cd`, `ls`, `cat`, etc. all work. `exit` or Ctrl-D leaves the container running.

### Run single commands

```bash
docker exec ooklaserver ls -la /opt/ookla
docker exec ooklaserver cat /opt/ookla/OoklaServer.properties
docker exec ooklaserver ps -ef
```

### Copy files in/out

```bash
# container → host
docker cp ooklaserver:/opt/ookla/OoklaServer.properties ./props-backup

# host → container (changes apply after docker restart)
docker cp ./tweaked.properties ooklaserver:/opt/ookla/OoklaServer.properties
docker restart ooklaserver
```

### Inspect from the host

```bash
docker ps                    # running containers
docker inspect ooklaserver   # mounts, networks, env, health (JSON)
docker logs -f ooklaserver   # stream logs
docker stats ooklaserver     # CPU / mem / net / IO live
```

### Shell into a dead container

`docker exec` needs a *running* container. If yours has crashed, spawn a fresh one from the image with `bash` as the entry point:

```bash
docker run --rm -it --entrypoint bash local-ooklaserver
```

---

## Licensing note

OoklaServer is distributed by Ookla for self-hosted use. Operators who wish to appear in the public Speedtest server list must register and submit their server for review. A local-only install does not require submission; the daemon does not make inbound calls to Ookla.
