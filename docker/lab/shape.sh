#!/usr/bin/env bash
# Entry point of the lab's link container: apply the requested slow-link profile to this
# container's own network interface, then hold the namespace open for as long as the lab runs.
#
# Runs inside the container, not on the host, so the shaping is in place before either server is
# started: each server declares this container healthy as its start condition, and this script
# only reports healthy once both directions are shaped and read back.
#
# The servers run in containers of their own and join this container's network namespace, so one
# shaped link carries both of them. Only this container is granted NET_ADMIN, so neither server
# ever holds the capability that shapes the link — there is nothing to drop before handing over,
# because nothing is handed over.
#
# The profile comes from NETPACE_LINK_PROFILE; empty means no shaping at all.
#
# Exits rather than hold the link open unless BOTH directions are shaped. A half-shaped link is
# worse than an unshaped one: the maintainer reads a plausible number off a link that is only
# slow one way, and nothing on screen says so.

set -euo pipefail

# The container's interface on Docker's bridge network. Host traffic reaching the published
# ports arrives here, so this is the interface both directions cross, for both servers.
IFACE=eth0

# netem shapes egress only, so the upload direction is shaped on an intermediate functional
# block device that incoming traffic is redirected through.
IFB=ifb0

# Written once the link is in the state asked for. The image's healthcheck tests for it and each
# server waits on that, so this file is what admits a server to the link.
#
# Overridable so that the refusal paths can be exercised off-container, where /run is not
# writable; nothing in the lab sets it, and Dockerfile.link's healthcheck tests the default.
READY="${NETPACE_LAB_READY:-/run/netpace-lab/shaped}"

cd "$(dirname "$0")"

# shellcheck source=link-profiles.sh
. ./link-profiles.sh

PROFILE="${NETPACE_LINK_PROFILE:-}"

say() { echo "shape: $*"; }

# Anything leaving the link less than fully shaped ends here. Nothing is torn down: a leftover
# qdisc goes with the dead container's namespace.
die() {
  echo "shape: ERROR: $*" >&2
  echo "shape: the '$PROFILE' profile was not applied in full, so no server will be started." >&2
  echo "shape: a link that is fast, or slow in only one direction, would be read as if it were the profile." >&2
  exit 1
}

# Holds the network namespace open, which is what gives every server in the lab a network.
# Signals are trapped because this is PID 1, where SIGTERM's default action is to ignore it.
#
# Never returns, so no caller falls through: the no-profile branch would otherwise reach the
# unknown-profile check and reject a lab that is configured correctly.
hold() {
  trap 'echo; say "stopping — the link is going away."; exit 0' TERM INT
  sleep infinity &
  wait
  exit 0
}

# A leftover from an earlier run of this container would declare the link shaped before anything
# had been shaped, and admit both servers to a fast link.
rm -f "$READY"
mkdir -p "$(dirname "$READY")"

if [ -z "$PROFILE" ]; then
  say "no link profile requested — the link runs at full local speed."
  : > "$READY"
  hold
fi

if ! link_profile "$PROFILE"; then
  echo "shape: ERROR: unknown link profile '$PROFILE'." >&2
  echo "shape: available profiles: $LINK_PROFILES" >&2
  exit 1
fi

command -v tc >/dev/null 2>&1 && command -v ip >/dev/null 2>&1 \
  || die "tc or ip is not installed in this image (iproute2 is missing from Dockerfile.link)"

# Word-split below, so an empty direction would install a no-op netem that still reads back as
# netem. set -u catches an unset variable, never an empty one.
[ -n "$LINK_DOWN" ] && [ -n "$LINK_UP" ] \
  || die "the '$PROFILE' profile has an empty direction in link-profiles.sh"

say "applying '$PROFILE' — $LINK_MODELS"

# Start from a known state, so a second run replaces whatever the previous one left rather than
# stacking a second qdisc on top of it.
tc qdisc del dev "$IFACE" root 2>/dev/null || true
tc qdisc del dev "$IFACE" ingress 2>/dev/null || true

# Download is traffic leaving the container, so plain egress netem. Unquoted on purpose: the
# profile string is a list of tc arguments to split.
# shellcheck disable=SC2086
tc qdisc replace dev "$IFACE" root netem $LINK_DOWN \
  || die "could not shape the download direction on $IFACE — see the tc error above. Check NET_ADMIN is granted (cap_add in docker-compose.yml), and that the host kernel's netem supports this profile's arguments"

# Upload: traffic entering the container. netem cannot shape ingress, so every incoming packet is
# mirrored to $IFB and shaped on that device's egress instead.
if ! ip link show "$IFB" >/dev/null 2>&1; then
  ip link add "$IFB" type ifb \
    || die "could not create $IFB — see the ip error above. The host kernel needs the ifb module for the upload direction"
fi
ip link set "$IFB" up || die "could not bring $IFB up"

tc qdisc add dev "$IFACE" handle ffff: ingress \
  || die "could not attach an ingress qdisc to $IFACE"
tc filter add dev "$IFACE" parent ffff: protocol all u32 match u32 0 0 \
     action mirred egress redirect dev "$IFB" \
  || die "could not redirect $IFACE ingress to $IFB"
# shellcheck disable=SC2086
tc qdisc replace dev "$IFB" root netem $LINK_UP \
  || die "could not shape the upload direction on $IFB"

# The one check that distinguishes "tc accepted the arguments" from "the link is shaped", which
# is the claim start.sh goes on to make to the maintainer.
tc qdisc show dev "$IFACE" | grep -Eq 'netem .*(delay|rate|loss)' \
  || die "no impairment on the netem qdisc on $IFACE after shaping the download direction"
# The redirect is what carries $IFACE ingress to $IFB. Without it $IFB is a perfectly shaped
# device no packet ever crosses, and the upload direction runs at full speed.
tc filter show dev "$IFACE" parent ffff: | grep -q mirred \
  || die "no ingress redirect on $IFACE, so nothing reaches $IFB and the upload direction is unshaped"
tc qdisc show dev "$IFB" | grep -Eq 'netem .*(delay|rate|loss)' \
  || die "no impairment on the netem qdisc on $IFB after shaping the upload direction"

say "download (link egress):  netem $LINK_DOWN"
say "upload   (link ingress): netem $LINK_UP"
say "both directions shaped — admitting the servers."

: > "$READY"
hold
