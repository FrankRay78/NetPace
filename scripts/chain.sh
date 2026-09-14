#!/usr/bin/env bash
#
# chain.sh — carry one GitHub issue from a clean main to an open pull request, unattended.
#
# Runs /build, /study, /verify, /study, /raise-pr for the named issue, each as its own headless
# `claude -p` process, starting a stage only after the previous one reported its own success
# verdict. Each study pass resumes the session of the stage it follows. The first stage that
# fails — a FAILED verdict, no recognisable verdict, a non-zero exit, or a stall past its time
# limit — stops the run, and the closing line names the stage, its position and the reason.
#
# WHY. Typed by hand, the chain spends most of its wall-clock waiting for someone to read one
# stage's report and start the next. The ordering is enforced here, outside the model, so no
# stage can start over unverified work.
#
# WHAT IT COSTS. A full run spends substantial model time — the better part of an hour — and its
# last stage opens a real pull request. The deliberate human act is starting the chain against
# one named issue. The chain itself never resets, cleans, stashes, checks out, pushes or writes
# a file; the branch and working tree are always exactly as the last stage left them.
#
# Prerequisites: git, claude, gh, jq and timeout on PATH; claude and gh signed in; a clean main.
# Tests: scripts/chain.tests.sh.
#
# Overrides: CHAIN_MODEL selects the model every stage uses. CHAIN_STAGE_TIMEOUT (seconds)
# replaces every stage's time limit.

set -uo pipefail

CHAIN_MODEL="${CHAIN_MODEL:-claude-opus-5}"

BUILD_LIMIT=7200
STUDY_LIMIT=1800
VERIFY_LIMIT=5400
RAISE_PR_LIMIT=1800
if [ -n "${CHAIN_STAGE_TIMEOUT:-}" ]; then
  BUILD_LIMIT=$CHAIN_STAGE_TIMEOUT
  STUDY_LIMIT=$CHAIN_STAGE_TIMEOUT
  VERIFY_LIMIT=$CHAIN_STAGE_TIMEOUT
  RAISE_PR_LIMIT=$CHAIN_STAGE_TIMEOUT
fi

usage() {
  echo "usage: scripts/chain.sh [--dry-run] <issue>" >&2
  echo "  <issue>    one GitHub issue number, 270 or #270" >&2
  echo "  --dry-run  list the stages that would run, and run nothing" >&2
}

usage
exit 1
