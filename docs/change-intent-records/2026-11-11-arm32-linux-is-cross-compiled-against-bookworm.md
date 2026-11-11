# 32-bit ARM Linux Is Cross-Compiled Against a Bookworm Sysroot

**Intent:** Ship a native 32-bit ARM Linux archive when no hosted runner can either build or run one, extending [the Linux native AOT release](2026-05-02-linux-native-aot-release.md) to armhf boards.

**Behaviour:** See issue [#547](https://github.com/FrankRay78/NetPace/issues/547). Mechanics in [docs/RELEASING.md](../RELEASING.md), *32-bit ARM Linux cross-compilation*.

**Constraints:** The sysroot's glibc fixes the oldest system the binary will start on, so choosing it chooses NetPace's published support floor for these boards. GitHub hosts no ARM32 runner and its ARM64 runners do not execute ARM32 code, so this is the only AOT target neither built nor smoke-tested on its own architecture.

**Decisions:**

- A Debian 12 armhf sysroot. Rejected: the Ubuntu runner's own armhf ports packages, which link against a far newer glibc, leaving a binary that will not start on a Bookworm board at all.
- `docker export` of an emulated `debian:12` container. Rejected: `debootstrap` and dotnet/runtime's `build-rootfs.sh`, each adding a tool or a repository to assemble the filesystem the official image already is.
- `lld`. Rejected: the runner's own `ld.bfd`, which is configured for x86 alone and refuses the ARM emulation mode.
- Native-only, with no `standalone` or `net10` archive. Rejected: all three variants, two more large downloads per release for a minority architecture, one of them needing a runtime these boards would rather not install. The accepted cost is that nothing remains to measure the archive against, so the size contract gives way to a presence check.

**Date:** 2026-11-11
