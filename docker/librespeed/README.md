# Local LibreSpeed server (Docker)

A self-contained [LibreSpeed](https://github.com/librespeed/speedtest) server for NetPace to run tests against, the way `docker/ooklaserver/` provides one for Ookla.

LibreSpeed is the open-source speed test server anyone can host. This setup runs the official image in standalone mode, so the install directory is the web root and the endpoints NetPace speaks to are `/backend/empty.php` (the ping, and where uploads are POSTed) and `/backend/garbage.php` (the downloads), on port 8080 inside the container.

Nothing is built: LibreSpeed publishes an official image, so there is no `Dockerfile` here.

---

## Contents

| File                 | Purpose                                                              |
| -------------------- | -------------------------------------------------------------------- |
| `docker-compose.yml` | Declarative container config (image, port mapping, restart policy).  |
| `start.sh`           | Start container, wait until ready, print the NetPace command.         |
| `stop.sh`            | Stop and remove the container.                                        |

---

## Prerequisites

- Docker Desktop (Windows, macOS) or Docker Engine (Linux), running.
- Host TCP port 18081 free. The OoklaServer setup uses 18080, so the two can run side by side.
- `bash` and `curl` on PATH.

---

## Quick start

```bash
bash start.sh
```

When ready, the script prints the exact NetPace command:

```bash
netpace --provider librespeed --server-url http://localhost:18081/
```

`--server-url` names the address LibreSpeed is installed at, not one of its endpoints. A trailing slash is optional. An install served under a path, say `http://nas.local/speedtest/`, is named with that path.

A full run on the default profile moves about 125 MiB over loopback. Add `--profile tiny` while you are only checking that the setup works.

To stop and remove the container:

```bash
bash stop.sh
```

---

## What the readiness check confirms

`start.sh` polls `http://localhost:18081/backend/empty.php` through the published port and waits for the answer NetPace itself requires: a success status **and an empty body**. A plain web server answering at the same address with a page would satisfy a status-only check and then fail every NetPace run, so the body is checked too.

The script gives up after 60 seconds, prints the container log, and removes the container. A server left running is one nobody confirmed was ready.

---

## Scope

- **No slow-link profiles.** The shaped-link profiles of the OoklaServer setup are not reproduced here. Use that setup when you want to measure NetPace over a modelled satellite or radio link.
- **No telemetry.** Standalone mode runs the test backend with no results database behind it, which is all NetPace uses.
- **Not a supported setup, and not checked in CI.** This is a local development aid, like the OoklaServer one beside it.
