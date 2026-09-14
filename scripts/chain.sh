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

# Per-stage time limits in seconds, conservative until tuned from real runs.
BUILD_LIMIT=${CHAIN_STAGE_TIMEOUT:-7200}
STUDY_LIMIT=${CHAIN_STAGE_TIMEOUT:-1800}
VERIFY_LIMIT=${CHAIN_STAGE_TIMEOUT:-5400}
RAISE_PR_LIMIT=${CHAIN_STAGE_TIMEOUT:-1800}

usage() {
  echo "usage: scripts/chain.sh [--dry-run] <issue>" >&2
  echo "  <issue>: one GitHub issue number, 270 or #270" >&2
  echo "  --dry-run: list the stages that would run, and run nothing" >&2
}

dry_run=0
if [ "${1:-}" = --dry-run ]; then dry_run=1; shift; fi
if [ $# -ne 1 ]; then usage; exit 1; fi
issue=${1#\#}
case "$issue" in ''|*[!0-9]*) usage; exit 1 ;; esac

# Report mode reaches no git or claude command, so it cannot change anything.
if [ "$dry_run" = 1 ]; then
  echo "chain: dry run for issue #$issue — nothing will be run"
  echo "  1. build: /build $issue"
  echo "  2. study: /study $issue (resumes build's session)"
  echo "  3. verify: /verify"
  echo "  4. study: /study $issue (resumes verify's session)"
  echo "  5. raise-pr: /raise-pr $issue"
  exit 0
fi

PR_URL='https://github\.com/[^[:space:]]+/pull/[0-9]+'

# fail <position> <name> <reason> — the closing message, then stop. Nothing is undone: the
# branch and working tree stay exactly as the failed stage left them, for diagnosis.
fail() {
  echo "chain: FAILED at [$1/5] $2 — $3"
  echo "chain: no later stage ran; reopen the failed session with \`claude --resume\` (most recent headless session in this repo)."
  exit 1
}

# run_stage <position> <name> <prompt> <verdict ERE> <limit> [session id to resume]
# Prints the stage's report and, on success, leaves its reply in STAGE_RESULT and STAGE_SESSION.
# The verdict is searched for anywhere in the report: it is not reliably the last line.
run_stage() {
  local pos=$1 name=$2 prompt=$3 verdict=$4 limit=$5 resume=${6:-}
  local reply rc reason
  echo "chain: [$pos/5] $name — starting"
  # The prompt must be the positional straight after -p, and stdin must be redirected, or the
  # call stalls on the terminal (both recorded in plugin-report.sh).
  reply=$(timeout --kill-after=60 "$limit" claude -p "$prompt" --model "$CHAIN_MODEL" --output-format json --dangerously-skip-permissions ${resume:+--resume "$resume"} </dev/null)
  rc=$?
  STAGE_RESULT=$(jq -r '.result // empty' <<<"$reply" 2>/dev/null)
  STAGE_SESSION=$(jq -r '.session_id // empty' <<<"$reply" 2>/dev/null)
  [ -n "$STAGE_RESULT" ] && printf '%s\n' "$STAGE_RESULT"
  case $rc in
    0) ;;
    124|137) fail "$pos" "$name" "stalled — exceeded ${limit}s" ;;
    *) fail "$pos" "$name" "claude exited with $rc" ;;
  esac
  # A stage's own FAILED verdict wins even when a success verdict appears in the same report.
  reason=$(grep -oE 'FAILED reason=.*' <<<"$STAGE_RESULT" | head -n 1)
  [ -n "$reason" ] && fail "$pos" "$name" "${reason#FAILED reason=}"
  grep -qE -- "$verdict" <<<"$STAGE_RESULT" || fail "$pos" "$name" "no recognisable verdict"
  echo "chain: [$pos/5] $name — ok"
}

# The only preconditions checked here: /build checks the fetch, unpushed commits and the issue
# itself, and a missing tool fails the first stage at once.
if [ -n "$(git status --porcelain)" ]; then
  echo "chain: refused — working tree is not clean; no stage was started."
  exit 1
fi
branch=$(git rev-parse --abbrev-ref HEAD)
if [ "$branch" != main ]; then
  echo "chain: refused — $branch is checked out, not main; no stage was started."
  exit 1
fi

run_stage 1 build "/build $issue" 'READY branch=' "$BUILD_LIMIT"
build_session=$STAGE_SESSION
run_stage 2 study "/study $issue" 'STUDIED issue=' "$STUDY_LIMIT" "$build_session"
run_stage 3 verify /verify 'VERIFIED branch=' "$VERIFY_LIMIT"
verify_session=$STAGE_SESSION
run_stage 4 study "/study $issue" 'STUDIED issue=' "$STUDY_LIMIT" "$verify_session"
run_stage 5 raise-pr "/raise-pr $issue" "$PR_URL" "$RAISE_PR_LIMIT"

echo "chain: done — $(grep -oE -- "$PR_URL" <<<"$STAGE_RESULT" | tail -1)"
exit 0
