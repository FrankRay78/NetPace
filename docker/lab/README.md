# The slow-link lab (Docker)

One local lab that puts **OoklaServer, LibreSpeed or both** behind the same deliberately bad network, so NetPace can be run against either provider on an identical link, from one place.

Both providers share one measurement in `NetPace.Core`. A change to how NetPace measures a slow link changes both at once, and this lab is what lets both be checked: the servers sit behind one shaped link, with one copy of the profile table, so a difference between two providers' figures can only come from the providers.

Started with no profile, the same lab gives you the full-speed local servers NetPace has always been developed against.

| Server      | Endpoint                                      | Port    |
| ----------- | --------------------------------------------- | ------- |
| OoklaServer | `/speedtest/latency.txt`, `/speedtest/random{N}x{N}.jpg`, `/speedtest/upload.php` | `18080` (TCP and UDP, every interface) |
| LibreSpeed  | `/backend/empty.php`, `/backend/garbage.php`  | `18081` (TCP, loopback only) |

Both ports are the ones each server has always had, so existing commands and documentation keep working.

---

## Contents

| File                      | Purpose                                                              |
| ------------------------- | -------------------------------------------------------------------- |
| `docker-compose.yml`      | The three containers: the link, and a server behind it for each provider. |
| `Dockerfile.link`         | Builds the link: the only image that shapes anything.                 |
| `Dockerfile.ooklaserver`  | Builds an image with the latest stable OoklaServer.                   |
| `start.sh`                | Start the lab on a profile, with either server or both, and wait until it is ready. |
| `stop.sh`                 | Stop and remove everything the lab started.                           |
| `link-profiles.sh`        | The five slow-link profiles, as `tc netem` arguments. Sourced by `start.sh` and `shape.sh`. |
| `shape.sh`                | The link container's entry point: applies the requested profile, then holds the link open. |
| `lab-properties.sh`       | Run when the OoklaServer image is built: turns off the settings that would make the server reach out to the internet on its own. |
| `lab-properties.tests.sh` | Test matrix for `lab-properties.sh`.                                  |
| `start.tests.sh`          | Test matrix for `start.sh` and the lab's wiring.                      |
| `check-slow-links.sh`     | Runs NetPace against OoklaServer on every profile in turn, and reports what each run measured and whether it completed. |

LibreSpeed needs no `Dockerfile`: it publishes an official image, which the lab uses unchanged.

---

## Prerequisites

- Docker Desktop (Windows, macOS) or Docker Engine (Linux), running. The slow-link profiles are confirmed on Linux Docker Engine only. See *Confirmed host platforms*.
- Host TCP and UDP port 18080 free, and host TCP port 18081.
- `bash` and `curl` on PATH (Git Bash, WSL, macOS, Linux are all fine).

---

## Quick start

```bash
./start.sh                        # both servers, full local speed
./start.sh --ookla                # OoklaServer alone, full local speed
./start.sh --librespeed           # LibreSpeed alone, full local speed
./start.sh satellite              # both servers behind a geostationary satellite link
./start.sh satellite --ookla      # OoklaServer alone, behind that link
./start.sh satellite --librespeed # LibreSpeed alone, behind that link
```

With no profile the link runs at full local speed. With neither `--ookla` nor `--librespeed`, both servers are started. `./start.sh --help` lists the profiles and the servers.

Both host ports are published by the link container, which every start brings up, so a single-server start still needs 18080 and 18081 free. Starting one server does not release the other's port.

When ready, the script prints the NetPace command for each server it started (on a shaped link each one carries `--profile tiny`, for the reason under *A full run moves a lot of data*):

```bash
netpace --server-url http://localhost:18080/speedtest/upload.php
netpace --provider librespeed --server-url http://localhost:18081/
```

LibreSpeed's `--server-url` names the address the install is served at, not one of its endpoints. A trailing slash is optional.

To stop everything the lab started, whichever servers those were:

```bash
./stop.sh
```

To change profile or server choice, start again. Every start replaces whatever was running with exactly the state you asked for, so there is no way to end up on yesterday's profile or with a server you did not ask for. The ready message always names the profile in effect, or says there is none.

`start.sh` rebuilds the images each time (`--build`), so an upgrade happens by starting again. Force a cache-free rebuild with:

```bash
docker compose build --no-cache
./start.sh
```

### Run the providers one at a time

Both servers sit behind **one** shared link. That is the point: it is what makes the two providers' figures comparable. It also means a run against one server while the other is being measured crosses a link the other run is already filling, so the second figure is not the link, it is the contention.

Measure one provider, then the other. The lab prints this reminder whenever it starts both.

### A full run moves a lot of data

On the default profile a run moves about 120 MiB: up to 100 MiB of downloads, plus about 20 MiB of uploads. Add `--profile tiny` while you are only checking that the lab works, and on the narrower link profiles treat it as a habit: at 9.6 kbit a single default-sized request is a wait measured in hours.

---

## Slow link profiles

NetPace is developed on fast networks, and its unit tests fake HTTP responses, so nothing in the suite exercises a poor link. These profiles close that gap: they put the local servers behind a deliberately bad network so you can run the real `netpace` against them and see what a user on that link would see.

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

Round trip, jitter and loss above are end-to-end figures. They are split evenly between the two directions, so each direction carries half of each. Rates are not split: they are per-direction figures already, because a real link's downstream and upstream rates are independent.

"Down" shapes traffic leaving the link container, which is what NetPace downloads; "up" shapes traffic entering it, which is what NetPace uploads.

| Profile      | Down (`tc netem` arguments)                                          | Up (`tc netem` arguments)                                              |
| ------------ | -------------------------------------------------------------------- | ---------------------------------------------------------------------- |
| `satellite`  | `delay 300ms loss 0.25% rate 10mbit`                                 | `delay 300ms loss 0.25% rate 2mbit`                                    |
| `3g-bloated` | `delay 1000ms 200ms distribution normal loss 0.5% rate 1mbit`        | `delay 1000ms 200ms distribution normal loss 0.5% rate 384kbit`        |
| `gprs`       | `delay 350ms 75ms distribution normal loss 0.5% rate 40kbit`         | `delay 350ms 75ms distribution normal loss 0.5% rate 20kbit`           |
| `radio`      | `delay 500ms loss 1% rate 9600bit`                                   | `delay 500ms loss 1% rate 9600bit`                                     |
| `flaky`      | `delay 100ms loss gemodel 2% 50% 30% 0% rate 2mbit`                  | `delay 100ms loss gemodel 2% 50% 30% 0% rate 1mbit`                    |

`link-profiles.sh` is the single source of these values, and the only copy of them in the lab; the table above is for reading.

`flaky` uses `netem`'s Gilbert-Elliott loss model (`loss gemodel p r 1-h 1-k`) rather than a flat percentage, so its loss arrives in clumps: 2% chance of entering a bad state, 50% of leaving it, dropping 30% of packets while in it. That averages a little over 1% per direction, but in bursts, which is what breaks a retry policy that uniform loss survives.

### The rate is the link, not the result

Each rate is the capacity of the pipe, not the throughput you will measure across it. Delay and loss cost TCP a great deal on their own: on `satellite`, a 10 Mbit pipe with a 600 ms round trip delivers a small fraction of that to a single connection, because the transfer spends its life in slow start and every loss costs most of a second to recover. That gap is not a fault in the profile, it is the thing the profiles exist to show you.

It also means two providers can report very different download figures across the *same* profile, because their request shapes differ. That is a real difference between the providers, and reading it is why they share one link.

### Automatic server selection is not slowed

Only traffic to and from the lab crosses the shaped link. The server list still comes from the public internet at full speed, so **automatic server selection is unaffected** and runs as fast as it always did. To put a measurement on the slow link you must point NetPace at the lab explicitly, with `--server-url`.

### OoklaServer's start-up traffic is turned off

Two OoklaServer settings would make the server add traffic of its own to the shaped link, and the image turns both off. OoklaServer would otherwise reach out to the internet while it starts: it attempts a Let's Encrypt certificate about 17 seconds in, and it checks for updates to itself. On a full-speed link neither is noticeable. At 9.6 kbit a single TLS handshake costs seconds, and that traffic arrives after the container is healthy and after `start.sh` has said the lab is ready, so a run started at that moment measured the server's start-up rather than the link. On `radio` its first latency probe used up NetPace's 10 second limit every time.

`lab-properties.sh` turns them off when the image is built, so the server no longer requests a certificate or checks for updates. The server log says `Server updates were disabled.` and carries no certificate lines. If a later OoklaServer release drops either setting's name from the properties file, the image build fails rather than quietly restoring the traffic.

Both servers share one link, so this mattered for LibreSpeed too: OoklaServer's start-up traffic would have crossed the link a LibreSpeed run was being measured on.

The first latency probe straight after start is still a few seconds slower than one a couple of minutes later. On `radio` it took about 6 s, against about 3 s once the lab had settled, and NetPace's limit is 10 s. The cause of that gap is not established. `start.sh` prints the ready message only after its readiness poll has read the server's answer back, so the poll's own response is not still crossing the link at that point. **A latency stage that fails outright straight after start is not explained by this gap.** Look at the link and at NetPace's limits instead.

### Confirmed host platforms

The profiles were confirmed on **Linux Docker Engine** (Ubuntu 24.04, kernel 6.8, Docker 29.9). That is the supported configuration.

Docker Desktop for Windows and macOS run containers inside a virtual machine, and the upload direction needs the host kernel's `ifb` module, which is not guaranteed there. They are **unconfirmed**. If shaping cannot be applied in full on your host you are told so and nothing starts, so an unconfirmed platform costs you a clear error, never a wrong measurement.

---

## Checking every profile

```bash
./check-slow-links.sh
```

One command for the whole check. For each row of the table below it starts the lab on that profile with OoklaServer alone, runs the NetPace built from the working tree against it with the paired NetPace profile and `--fail-on total`, and records the outcome. It then runs once against the full-speed server. A run **completes** when `--fail-on total` exits 0: every test that ran had at least one request succeed.

| Lab profile  | NetPace profile | Why |
| ------------ | --------------- | --- |
| `satellite`  | default         | A default run has to work on a link this ordinary. |
| `flaky`      | default         | The same. |
| `3g-bloated` | `small`         | At 1 Mbit the default's download alone takes well over ten minutes. |
| `gprs`       | `tiny`          | At 40 kbit only `tiny`'s byte caps finish in minutes. |
| `radio`      | `tiny`          | At 9.6 kbit only `tiny`'s byte caps finish in minutes. |

The pairing is what the check means. If a row stops finishing in a reasonable time, raise it rather than quietly moving that row to a smaller NetPace profile.

The summary names each profile, the NetPace profile used, pass or fail, the download and upload figures and the run time, and exits non-zero if any run did not complete. The figures are there because `--fail-on total` is a weak test on its own: a run can complete and still report a figure nothing like what the link carries, and only a figure beside the result shows it.

```text
LINK          NETPACE PROFILE   RESULT  DOWNLOAD       UPLOAD         RUN TIME
satellite     default           PASS    8.52 Mbit/s    1.81 Mbit/s    4m 11s
```

Each run's result and its per-request diagnostics are kept in a new timestamped folder under `runs/`, which is git-ignored; the summary names the folder. The NetPace under test is built once into `.build/`, also git-ignored, so every run in a check measures the same binary.

Nothing in the script bounds how long a run may take: a slow run on a slow link is the point, and a limit here would decide what NetPace's own limits are for. Expect tens of minutes for the whole check. Ctrl-C is safe at any point, and the lab is removed however the script ends.

It is run by hand, not in CI: it needs Docker with `NET_ADMIN`, and it takes far too long for a pull request.

### OoklaServer closes a connection that has lived too long

Reading a run on a slow link means knowing this first, because it shapes nearly every result on this page.

OoklaServer gives up on a connection once it has been open longer than `OoklaServer.maxConnectionSeconds` and closes it, whatever it was in the middle of writing. The setting is undocumented, and the stock build's value is not published; measuring it on these profiles puts it somewhere around 75 to 110 seconds. It is the server's doing rather than the client's: a plain `curl` of the same URL across the same link fails the same way, in the same place.

```text
random1500x1500.jpg -> 200 4468241 rc=0  in 26s     # 4.26 MiB, completed
random2000x2000.jpg -> 200 7907738 rc=18 in 65s     # two bytes short, connection closed
random2500x2500.jpg -> 200 10485737 rc=18 in 76s    # 10 MiB of 11.8, connection closed
```

So on a narrow link the ceiling on a single request is not how patient the client is, it is how long the server will keep the connection. Any link below roughly 13 Mbit has some of the default profile's downloads cut off this way.

Where it shows:

- **In the server log** (`docker logs netpace-lab-ooklaserver`): `connection is expired; forcibly closing`.
- **In `netpace --diagnostics`**: the request's line reads `status=failed` with `reason="The response ended prematurely..."` and `exception=HttpIOException`, whose `HttpRequestError` is `ResponseEnded`. Its `bytes=` is what had arrived before the close, which is the point: those bytes count towards the reported figure, so a cut-off request lowers the figure only by the time it spent, not by everything it carried.

**The lab keeps the stock value on purpose.** Raising it would make these profiles easier and the results prettier, and would measure something nobody has: public Ookla servers run the stock setting, so a NetPace that only works against a raised limit does not work. The limit is a condition to measure honestly under, not a lab artefact to remove.

---

## How it works, and what it refuses to do

The lab is three containers in one Compose project, `netpace-lab`:

- **`link`** holds the profile table, the `NET_ADMIN` capability and both published ports. `shape.sh` runs as its entry point and applies the profile with `tc netem`. The download direction is plain egress `netem` on the container's interface; `netem` cannot shape ingress, so the upload direction redirects incoming traffic through an `ifb` device and shapes that device's egress instead. Once both directions are read back, the container reports healthy and holds its network namespace open for as long as the lab runs.
- **`ooklaserver`** and **`librespeed`** each run their provider's server and join the link container's network namespace (`network_mode: "service:link"`). They publish nothing and are not granted `NET_ADMIN`. LibreSpeed listens on 8081 inside rather than its default 8080, because OoklaServer has 8080 and the two share one namespace; the official image's own `WEBPORT` setting moves it, so the image is used unchanged.

That shape is what gives the lab its guarantees:

**One link, one set of profiles.** Both servers' traffic crosses the same shaped interface, and only the link image carries `link-profiles.sh`, so there is no second copy of what a profile means for the two to drift apart on.

**Neither server ever holds the capability that shapes the link.** Only the `link` container is granted `NET_ADMIN`. The servers are not, so there is nothing to drop before starting them: a compromised server cannot lift its own shaping.

**No server is started on a link that is not shaped yet.** Each server declares the link container *healthy* as its start condition, and the link reports healthy only once it is in the state that was asked for: both directions shaped and read back, or deliberately unshaped when no profile was requested. A profile that cannot be applied in full exits the link container non-zero instead, so no server is ever started behind it.

**Both directions, or nothing.** A half-shaped link is worse than an unshaped one: you would read a plausible number off a link that is only slow one way, with nothing on screen to say so.

**A start brings up everything you asked for, or nothing.** Either server failing to become ready removes the whole lab and exits non-zero, naming the server that failed. A figure from a lab that only half started cannot be compared with anything, which is the whole reason for measuring the two providers here.

**An unknown profile or server name is refused before anything is built**, with the list of what does exist and a non-zero exit.

**Ctrl-C during a start removes what it started**, as a failed start does. A lab left up would be one nobody confirmed ready.

**The lab does not restart itself.** After a crash or a reboot it stays down until you start it again, so a container can never come back fast while still claiming to be slow.

**A start clears the old labs.** This lab replaces two earlier ones, which lived in `docker/ooklaserver/` and `docker/librespeed/` and have since been removed. A container either of them left running holds one of the host ports, so a start removes it first, matched on its Compose project and service labels rather than on its name alone. The old librespeed lab named its project, so that match is exact. The old ookla lab did not, so its label is the directory-derived `ooklaserver`; the match also requires the container name that lab fixed, `ooklaserver`, so a Compose project of your own is removed only if its project, service and container all carry that name.

`start.sh` polls each server through its published port, which is the address you are about to use, rather than loopback inside the container. For LibreSpeed it waits for the answer NetPace itself requires: a success status **and an empty body**. A plain web server answering at the same address with a page would satisfy a status-only check and then fail every NetPace run.

Shaping is applied inside the link container at its start rather than from the host afterwards, so it is in place before either server can accept a connection.

---

## Manual operation

Without the scripts. The profile travels as an environment variable; leave it out for the full-speed lab.

```bash
# Both servers, on a profile
NETPACE_LINK_PROFILE=satellite docker compose up -d --build

# One server
docker compose up -d --build link librespeed

# Logs
docker compose logs -f link ooklaserver librespeed

# Stop and remove everything
docker compose down
```

Verify the endpoints directly:

```bash
curl -s http://localhost:18080/speedtest/latency.txt
# Body starts with: test=test

curl -s -o /dev/null -w "%{http_code}\n" http://localhost:18080/speedtest/random1500x1500.jpg
# Expected: 200

curl -s -o /dev/null -w "%{http_code} %{size_download}\n" http://localhost:18081/backend/empty.php
# Expected: 200 0
```

On Windows `cmd.exe`, replace `/dev/null` with `NUL`.

---

## LAN access

OoklaServer's port is published on every interface, so other machines on the LAN can test against it:

1. Allow inbound TCP/UDP 18080 on the host firewall.
2. Clients point at the host's LAN IP, for example

   ```bash
   netpace --server-url http://192.168.1.50:18080/speedtest/upload.php
   ```

Throughput is bounded by host NIC, Docker's networking layer, and container CPU.

LibreSpeed's port is bound to `127.0.0.1` only, so it is reachable from this machine and not from elsewhere on the network. Change the left side of its mapping in `docker-compose.yml` if you want the same for it.

---

## Changing the host ports

Both ports are published by the `link` service, because that is the container the servers' traffic crosses. To use different host ports, edit the left side of the mappings there:

```yaml
    ports:
      - "28080:8080/tcp"
      - "28080:8080/udp"
      - "127.0.0.1:28081:8081/tcp"
```

Then point NetPace at the new ports. Update `start.sh`'s `OOKLA_PORT` and `LIBRESPEED_PORT` to match if you want the readiness checks and the printed commands to follow.

The **right** side of each mapping is the port inside the containers, and the two must stay different: both servers share one network namespace. 8081 for LibreSpeed is set by `WEBPORT` in `docker-compose.yml`. OoklaServer's ports are set by `OoklaServer.tcpPorts` and `OoklaServer.udpPorts` in its properties file, which would mean editing the file at image build time, so LibreSpeed is the cheaper of the two to move. Note that OoklaServer binds 5060 as well as 8080, so both are taken inside the namespace.

---

## Troubleshooting

### `start.sh` refuses to start a profile

A link container that stops before the servers start, followed by a log tail whose `shape: ERROR:` line names what could not be applied, means the host could not apply the profile in full. Nothing is left serving an unshaped link. Usual causes:

- The `NET_ADMIN` capability is missing. Check `cap_add` on the `link` service in `docker-compose.yml` is intact, and that no local override drops it.
- The host kernel has no `ifb` module, so the upload direction cannot be shaped. Expected on Docker Desktop for Windows and macOS; see *Confirmed host platforms* above.

The full-speed lab is unaffected: `./start.sh` with no profile still works on any host.

### `start.sh` times out waiting for a server

`start.sh` prints the last 50 log lines itself and then removes the lab, so read that tail. `docker compose logs` afterwards has nothing left to show. To keep the containers for inspection, start them manually (see *Manual operation*) rather than through the script.

Common causes: a host port conflict (another service on 18080 or 18081), an OoklaServer out-of-date warning that aborts startup, or missing AES-NI CPU instructions, which OoklaServer requires.

### "Server returned incorrect test string for latency.txt" from NetPace

A different HTTP server is bound to the host port. Find it:

- Windows: `netstat -ano | findstr :18080`
- Linux, macOS: `ss -ltnp | grep 18080`

A very common culprit on Windows is a locally running ASP.NET / Kestrel app, recognisable by a `Server: Kestrel` response header from `curl -v`.

Stop the interfering service or change the host port (see above).

### `docker build` fails downloading the OoklaServer tarball

Outbound HTTPS to `install.speedtest.net` is required. Behind a proxy:

```bash
docker compose build --build-arg HTTPS_PROXY=$HTTPS_PROXY ooklaserver
```

### A container exits immediately

Read its log:

```bash
docker compose logs link
docker compose logs ooklaserver
docker compose logs librespeed
```

To run the link container in the foreground and watch the shaping as it happens:

```bash
docker run --rm -it --cap-add NET_ADMIN -e NETPACE_LINK_PROFILE=satellite netpace-lab-link
```

---

## Appendix: inspecting the containers

No volume is mounted, so anything written inside a container is lost on `./stop.sh`.

```bash
# A shell in one of them
docker exec -it netpace-lab-ooklaserver bash
docker exec -it netpace-lab-link bash

# Single commands
docker exec netpace-lab-ooklaserver cat /opt/ookla/OoklaServer.properties
docker exec netpace-lab-link tc qdisc show dev eth0
docker exec netpace-lab-link tc qdisc show dev ifb0

# From the host
docker compose ps
docker stats netpace-lab-link
```

`tc qdisc show` inside the link container is the direct way to see what is in effect: `dev eth0` is the download direction and `dev ifb0` the upload.

A crashed container cannot be `exec`'d into. Spawn a fresh one from its image with `bash` as the entry point instead:

```bash
docker run --rm -it --entrypoint bash netpace-lab-ooklaserver
```

---

## Scope

- **Not a supported setup, and not checked in CI.** The lab is a local development aid: it needs Docker with `NET_ADMIN`, and the shaping needs a host kernel CI does not guarantee. `start.tests.sh` and `lab-properties.tests.sh` are what CI runs, and they check only what can be checked without Docker.
- **No telemetry.** LibreSpeed runs in standalone mode, with the test backend and no results database behind it, which is all NetPace uses.

---

## Licensing note

OoklaServer is distributed by Ookla for self-hosted use. Operators who wish to appear in the public Speedtest server list must register and submit their server for review. A local-only install does not require submission, and the daemon does not make inbound calls to Ookla.

[LibreSpeed](https://github.com/librespeed/speedtest) is open source under the LGPL, and is used here as an unmodified official container image.
