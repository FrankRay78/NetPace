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

if [ $# -ne 1 ]; then usage; exit 1; fi
issue=${1#\#}
case "$issue" in ''|*[!0-9]*) usage; exit 1 ;; esac

PR_URL='https://github\.com/[^[:space:]]+/pull/[0-9]+'

# run_stage <position> <name> <prompt> <verdict ERE> <limit> [session id to resume]
# Prints the stage's report and, on success, leaves its reply in STAGE_RESULT and STAGE_SESSION.
# The verdict is searched for anywhere in the report: it is not reliably the last line.
run_stage() {
  local pos=$1 name=$2 prompt=$3 verdict=$4 limit=$5 resume=${6:-}
  local reply
  echo "chain: [$pos/5] $name — starting"
  # The prompt must be the positional straight after -p, and stdin must be redirected, or the
  # call stalls on the terminal (both recorded in plugin-report.sh).
  reply=$(timeout --kill-after=60 "$limit" claude -p "$prompt" --model "$CHAIN_MODEL" --output-format json --dangerously-skip-permissions ${resume:+--resume "$resume"} </dev/null)
  STAGE_RESULT=$(jq -r '.result // empty' <<<"$reply")
  STAGE_SESSION=$(jq -r '.session_id // empty' <<<"$reply")
  printf '%s\n' "$STAGE_RESULT"
  grep -qE -- "$verdict" <<<"$STAGE_RESULT" || exit 1
  echo "chain: [$pos/5] $name — ok"
}

run_stage 1 build "/build $issue" 'READY branch=' "$BUILD_LIMIT"
build_session=$STAGE_SESSION
run_stage 2 study "/study $issue" 'STUDIED issue=' "$STUDY_LIMIT" "$build_session"
run_stage 3 verify /verify 'VERIFIED branch=' "$VERIFY_LIMIT"
verify_session=$STAGE_SESSION
run_stage 4 study "/study $issue" 'STUDIED issue=' "$STUDY_LIMIT" "$verify_session"
run_stage 5 raise-pr "/raise-pr $issue" "$PR_URL" "$RAISE_PR_LIMIT"

echo "chain: done — $(grep -oE -- "$PR_URL" <<<"$STAGE_RESULT" | tail -1)"
exit 0
