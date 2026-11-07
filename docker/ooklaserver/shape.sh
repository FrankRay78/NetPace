#!/usr/bin/env bash
# Container entry point: apply the requested slow-link profile to the container's own
# network interface, then hand over to OoklaServer.
#
# Runs inside the container, not on the host. It is the entry point rather than a step
# in start.sh for two reasons: the shaping has to be re-asserted on every start of the
# container, which carries `restart: unless-stopped`, so an unattended restart has to
# re-apply the profile or come back fast while claiming to be slow. Doing it here also
# means the shaping is in place before OoklaServer accepts its first connection.
#
# The profile comes from NETPACE_LINK_PROFILE; empty means no shaping at all.
#
# Refuses to start OoklaServer unless BOTH directions are shaped. A half-shaped server
# is worse than an unshaped one: the maintainer reads a plausible number off a link
# that is only slow one way, and nothing on screen says so.

set -euo pipefail

# The container's interface on Docker's bridge network. Host traffic reaching the
# published port arrives here, so this is the interface both directions cross.
IFACE=eth0

# netem shapes egress only, so the upload direction is shaped on an intermediate
# functional block device that incoming traffic is redirected through.
IFB=ifb0

cd "$(dirname "$0")"

# shellcheck source=link-profiles.sh
. ./link-profiles.sh

PROFILE="${NETPACE_LINK_PROFILE:-}"

say() { echo "shape: $*"; }

# Anything that leaves the link less than fully shaped ends here. Nothing is torn down
# on the way out: the container exits non-zero, start.sh prints this log tail and
# removes it, and a leftover qdisc in a dead container's namespace goes with it.
die() {
  echo "shape: ERROR: $*" >&2
  echo "shape: the '$PROFILE' profile was not applied in full, so OoklaServer will not be started." >&2
  echo "shape: a server that is fast, or slow in only one direction, would be read as if it were the profile." >&2
  exit 1
}

# This is the container entry point, so the command to run arrives as arguments (the
# image's CMD). With none, every `exec "$@"` below is a silent no-op that falls through
# to the unknown-profile check and reports the wrong failure.
if [ "$#" -eq 0 ]; then
  echo "shape: ERROR: no command to run — this entry point expects one (the image's CMD supplies OoklaServer)." >&2
  exit 1
fi

if [ -z "$PROFILE" ]; then
  say "no link profile requested — serving at full local speed."
  exec "$@"
fi

if ! link_profile "$PROFILE"; then
  echo "shape: ERROR: unknown link profile '$PROFILE'." >&2
  echo "shape: available profiles: $LINK_PROFILES" >&2
  exit 1
fi

command -v tc >/dev/null 2>&1 && command -v ip >/dev/null 2>&1 \
  || die "tc or ip is not installed in this image (iproute2 is missing from the Dockerfile)"

# Word-split below, so an empty direction would install a no-op netem that still reads
# back as netem. set -u catches an unset variable, never an empty one.
[ -n "$LINK_DOWN" ] && [ -n "$LINK_UP" ] \
  || die "the '$PROFILE' profile has an empty direction in link-profiles.sh"

say "applying '$PROFILE' — $LINK_MODELS"

# Start from a known state, so a restart replaces whatever the previous run left
# rather than stacking a second qdisc on top of it.
tc qdisc del dev "$IFACE" root 2>/dev/null || true
tc qdisc del dev "$IFACE" ingress 2>/dev/null || true

# Download: traffic leaving the container. Plain egress netem on the interface.
# Unquoted on purpose — the profile string is a list of tc arguments to split.
# shellcheck disable=SC2086
tc qdisc replace dev "$IFACE" root netem $LINK_DOWN \
  || die "could not shape the download direction on $IFACE — see the tc error above. Check NET_ADMIN is granted (cap_add in docker-compose.yml), and that the host kernel's netem supports this profile's arguments"

# Upload: traffic entering the container. netem cannot shape ingress, so every
# incoming packet is mirrored to $IFB and shaped on that device's egress instead.
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

# Read both qdiscs back. Every command above reported success, but this is the one
# check that distinguishes "tc accepted the arguments" from "the link is shaped",
# and it is the claim start.sh goes on to make to the maintainer.
tc qdisc show dev "$IFACE" | grep -Eq 'netem .*(delay|rate|loss)' \
  || die "no impairment on the netem qdisc on $IFACE after shaping the download direction"
# The redirect is what carries $IFACE ingress to $IFB. Without it $IFB is a perfectly
# shaped device no packet ever crosses, and the upload direction runs at full speed.
tc filter show dev "$IFACE" parent ffff: | grep -q mirred \
  || die "no ingress redirect on $IFACE, so nothing reaches $IFB and the upload direction is unshaped"
tc qdisc show dev "$IFB" | grep -Eq 'netem .*(delay|rate|loss)' \
  || die "no impairment on the netem qdisc on $IFB after shaping the upload direction"

say "download (container egress):  netem $LINK_DOWN"
say "upload   (container ingress): netem $LINK_UP"
say "both directions shaped — starting OoklaServer."

exec "$@"
