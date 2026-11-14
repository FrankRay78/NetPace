# Moved: the local LibreSpeed server now lives in the slow-link lab

The LibreSpeed setup that was here is part of [`docker/lab/`](../lab/README.md), the one lab that puts OoklaServer, LibreSpeed or both behind the same shaped link. LibreSpeed can now be run on a slow link, which it could not be here.

Nothing to run here. From `docker/lab/`:

```bash
./start.sh --librespeed           # LibreSpeed alone, full local speed
./start.sh satellite --librespeed # LibreSpeed alone, behind a satellite link
./start.sh satellite              # both providers, behind the same link
./stop.sh                         # stop everything the lab started
```

The server is still at `http://localhost:18081/`, and the first start removes any container this directory's setup left running.

Why one lab: both providers share one measurement in `NetPace.Core`, so a change to how NetPace measures a slow link changes both at once. Two sets of profiles to keep in step would make any difference between the providers' figures impossible to read. See [`docker/lab/README.md`](../lab/README.md).
