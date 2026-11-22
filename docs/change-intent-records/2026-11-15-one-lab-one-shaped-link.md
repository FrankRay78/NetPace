# One Lab, One Shaped Link

**Intent:** Let both providers be measured on an identical slow link, so a difference between their figures can only come from the providers.

**Behaviour:** See issue #551 and [`docker/lab/README.md`](../../docker/lab/README.md). Gated by running the real lab, which needs Docker with `NET_ADMIN` and a host kernel carrying `netem` and `ifb`.

**Constraints:**

- Both servers share one network namespace, so their inside ports must differ. LibreSpeed's `WEBPORT` moves its listener; OoklaServer's ports are set in its properties file, so moving them means editing that file at build time. OoklaServer holds 5060 as well as 8080. A third provider must take an inside port free of all three, and keep its own host port.
- Shaping needs `NET_ADMIN`, which Compose cannot grant conditionally.

**Decisions:**

- One shaping container, with each server joining its network namespace. Rejected: a shaping container per server, and copying the profiles into the LibreSpeed lab. Both leave two shaped links to keep in step, and any drift makes a provider difference unreadable.
- The servers are not granted `NET_ADMIN`. Rejected: granting `NET_ADMIN` and dropping it before `exec`, as the single-server lab did. Not holding it is simpler than dropping it.
- Server choice as `--ookla` / `--librespeed` flags. Rejected: Compose profiles, which would put a second meaning on the word the link profiles already own.

**Date:** 2026-11-15
