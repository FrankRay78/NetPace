# Moved: the local OoklaServer now lives in the slow-link lab

The OoklaServer setup that was here is part of [`docker/lab/`](../lab/README.md), the one lab that puts OoklaServer, LibreSpeed or both behind the same shaped link.

Nothing to run here. From `docker/lab/`:

```bash
./start.sh --ookla              # OoklaServer alone, full local speed
./start.sh satellite --ookla    # OoklaServer alone, behind a satellite link
./start.sh satellite            # both providers, behind the same link
./stop.sh                       # stop everything the lab started
```

The server is still at `http://localhost:18080/`, the profiles are still the same five, and the first start removes any container this directory's setup left running.

Why one lab: both providers share one measurement in `NetPace.Core`, so a change to how NetPace measures a slow link changes both at once. Two sets of profiles to keep in step would make any difference between the providers' figures impossible to read. See [`docker/lab/README.md`](../lab/README.md).
