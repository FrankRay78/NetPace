#!/usr/bin/env bash
# Turn off the OoklaServer settings that make the server reach out to the internet on its own.
#
#   bash lab-properties.sh /opt/ookla/OoklaServer.properties
#
# Run once at image build time, over the copy of the properties file the container serves from.
#
# Why the lab wants them off: on a slow-link profile the server's own outbound traffic crosses
# the same shaped link NetPace is measuring. At 9.6 kbit a single TLS handshake costs seconds,
# and OoklaServer attempts a Let's Encrypt certificate about 17 seconds after start — after the
# container is healthy and `start.sh` has said the lab is ready. A NetPace run started at that
# moment then measures the server's start-up rather than the link, and on the `radio` profile its
# first latency probe ran out the 10 s limit every time. Nothing in the lab needs either setting:
# it serves plain HTTP and never terminates TLS, and is rebuilt from the upstream bundle whenever
# the image is.
#
# Fails rather than report success it cannot stand behind. Each setting must already be named in
# the file, commented or not, so a release that drops a setting's name fails the image build
# instead of quietly restoring the traffic.

set -uo pipefail

# The settings to turn off, each a key the upstream properties file documents.
OUTBOUND_KEYS=(OoklaServer.ssl.useLetsEncrypt OoklaServer.enableAutoUpdate)

say() { echo "lab-properties: $*"; }
die() {
  echo "lab-properties: ERROR: $*" >&2
  echo "lab-properties: the server's own outbound traffic was not turned off, so it would cross the shaped link." >&2
  exit 1
}

if [ "$#" -ne 1 ]; then
  echo "lab-properties: ERROR: expected exactly one argument, the properties file to edit." >&2
  echo "Usage: bash lab-properties.sh <properties-file>" >&2
  exit 2
fi

PROPS="$1"
[ -f "$PROPS" ] || die "'$PROPS' is not a file."
[ -w "$PROPS" ] || die "'$PROPS' is not writable."

for key in "${OUTBOUND_KEYS[@]}"; do
  # A key is a dotted name, and the dots are regex wildcards unless escaped. Harmless against
  # this file's own keys, but a pattern that means what it says is one less thing to reason about.
  pattern="${key//./\\.}"

  grep -qE "^[[:space:]]*#?[[:space:]]*${pattern}[[:space:]]*=" "$PROPS" \
    || die "'$PROPS' does not mention $key, so this version of OoklaServer may have renamed or dropped it. Check the setting's name in OoklaServer.properties.default and update OUTBOUND_KEYS."

  # Drop any assignment already in force, so what is appended below is the only one. A commented
  # line is left where it is: it is the upstream documentation of the setting.
  sed -i -E "/^[[:space:]]*${pattern}[[:space:]]*=/d" "$PROPS" \
    || die "could not edit '$PROPS'."

  printf '\n# Turned off for the NetPace slow-link lab: see lab-properties.sh.\n%s = false\n' \
    "$key" >> "$PROPS" \
    || die "could not append to '$PROPS'."

  # Count the active assignments, so a delete that missed one cannot leave the server picking
  # between two.
  assignments="$(grep -cE "^[[:space:]]*${pattern}[[:space:]]*=[[:space:]]*false[[:space:]]*$" "$PROPS")"
  [ "$assignments" = 1 ] \
    || die "after editing, '$PROPS' holds $assignments assignment(s) of $key set to false, expected exactly 1."

  say "$key = false"
done

say "the server's own outbound traffic is off."
