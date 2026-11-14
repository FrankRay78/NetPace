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
