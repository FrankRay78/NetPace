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

---

## Prerequisites

- Docker Desktop (Windows/macOS) or Docker Engine (Linux), running.
- Host TCP and UDP port 18080 free.
- `bash` and `curl` on PATH (Git Bash, WSL, macOS, Linux — all fine).

---

## Quick start

```bash
./start.sh
```

When ready, the script prints the exact NetPace command:

```bash
NetPace --server http://localhost:18080/speedtest/upload.php
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
   NetPace --server http://192.168.1.50:18080/speedtest/upload.php
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
NetPace --server http://localhost:28080/speedtest/upload.php
```

Update `start.sh` (the polling URL) to match if you want the readiness check to continue working.

---

## Troubleshooting

### `start.sh` times out waiting for OoklaServer

Check the logs:

```bash
docker compose logs --tail=100 ooklaserver
```

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
