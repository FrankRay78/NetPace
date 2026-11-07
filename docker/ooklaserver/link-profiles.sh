#!/usr/bin/env bash
# The five fixed slow-link profiles the local OoklaServer can be started behind.
#
# Sourced, never run: by start.sh on the host, to validate the requested name and
# report what is in effect, and by shape.sh inside the container, to apply it. One
# copy of the table so the two can never disagree about what a profile means.
#
# Each profile gives netem arguments for both directions, named from NetPace's point
# of view on the host:
#
#   LINK_DOWN   traffic leaving the container   — what NetPace downloads
#   LINK_UP     traffic entering the container  — what NetPace uploads
#
# Round trip, jitter and loss are stated end to end in the profile table and split
# evenly between the two directions here, so each direction carries half of each.
# Rates are not split: they are per-direction figures already, because a real link's
# downstream and upstream rates are independent, which is the whole point of shaping
# the two separately.
#
# No associative arrays and no `local -n`: macOS ships bash 3.2, and this file is
# sourced by a host script that has to run there.

# The profile names, in table order. The only valid arguments to start.sh.
LINK_PROFILES="satellite 3g-bloated gprs radio flaky"

# link_profile <name>
#
# Sets LINK_MODELS, LINK_DOWN and LINK_UP for <name>. Returns 1 and leaves them
# untouched for a name that is not one of LINK_PROFILES, which is how both callers
# detect an unknown profile.
link_profile() {
  case "$1" in
    satellite)
      # Geostationary orbit is about 36,000 km up, so the hop alone costs ~240 ms
      # each way; consumer satellite broadband lands around 600 ms round trip.
      LINK_MODELS="geostationary satellite: ~600 ms round trip, 10 Mbit down / 2 Mbit up, ~0.5% loss"
      LINK_DOWN="delay 300ms loss 0.25% rate 10mbit"
      LINK_UP="delay 300ms loss 0.25% rate 2mbit"
      ;;
    3g-bloated)
      # A congested 3G cell with full queues: the bandwidth is poor but the latency
      # is what hurts, and it swings as the queues fill and drain. The jitter is
      # deliberately large relative to the delay for that reason.
      LINK_MODELS="congested 3G with full queues: ~2 s round trip with jitter, 1 Mbit down / 384 kbit up, ~1% loss"
      LINK_DOWN="delay 1000ms 200ms distribution normal loss 0.5% rate 1mbit"
      LINK_UP="delay 1000ms 200ms distribution normal loss 0.5% rate 384kbit"
      ;;
    gprs)
      # 2G packet data. Narrow enough that a single default-sized NetPace request
      # would take many minutes; use --profile tiny with this one.
      LINK_MODELS="2G data: ~700 ms round trip with jitter, 40 kbit down / 20 kbit up, ~1% loss"
      LINK_DOWN="delay 350ms 75ms distribution normal loss 0.5% rate 40kbit"
      LINK_UP="delay 350ms 75ms distribution normal loss 0.5% rate 20kbit"
      ;;
    radio)
      # A low-rate symmetric radio link — the slowest profile, and the one most
      # likely to expose a missing timeout rather than a slow result.
      LINK_MODELS="low-rate radio link: ~1 s round trip, 9.6 kbit each way, ~2% loss"
      LINK_DOWN="delay 500ms loss 1% rate 9600bit"
      LINK_UP="delay 500ms loss 1% rate 9600bit"
      ;;
    flaky)
      # Usable on paper but keeps failing. The loss is bursty rather than uniform,
      # which is what breaks a retry policy that uniform loss survives: netem's
      # Gilbert-Elliott model (`loss gemodel p r 1-h 1-k`) alternates between a good
      # state and a lossy one. p=2% into the bad state, r=50% out of it, so the link
      # is bad about 3.8% of the time and drops 30% of packets while it is — about
      # 1.2% loss per direction on average, arriving in clumps.
      LINK_MODELS="a usable link that keeps failing: ~200 ms round trip, 2 Mbit down / 1 Mbit up, burst loss averaging ~2%"
      LINK_DOWN="delay 100ms loss gemodel 2% 50% 30% 0% rate 2mbit"
      LINK_UP="delay 100ms loss gemodel 2% 50% 30% 0% rate 1mbit"
      ;;
    *)
      return 1
      ;;
  esac
}
