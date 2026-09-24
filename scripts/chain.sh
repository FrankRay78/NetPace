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
# a file; the branch and working tree are always exactly as the last stage left them, unless a
# stalled stage left background work of its own still running (see the time-limit note below).
# Every stage runs with --dangerously-skip-permissions, so a call an `ask` rule would have
# stopped is instead denied silently: a stage can carry on degraded and still report success.
#
# Prerequisites: git, claude, gh, jq and timeout on PATH; claude and gh signed in; a clean main.
# All five are checked before the first stage starts, so a missing one names itself.
# Tests: scripts/chain.tests.sh.
#
# Overrides: CHAIN_MODEL selects the model every stage uses. CHAIN_STAGE_TIMEOUT (seconds)
# replaces every stage's time limit.

# No -e: the `reply=$(...); rc=$?` dispatch below depends on a non-zero claude not killing the
# script, so the stage can be reported by name instead of the run dying silently.
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

# An override that is not a whole number reaches `timeout` as a bad argument, which would surface
# as an unhelpful launch failure blamed on the stage. Reject it here, where the cause is obvious.
case "${CHAIN_STAGE_TIMEOUT:-0}" in ''|*[!0-9]*)
  echo "chain: refused — CHAIN_STAGE_TIMEOUT must be a whole number of seconds." >&2; exit 1 ;;
esac

# raise-pr's verdict. Unlike the other four the payload is a URL, so it is pinned to its own
# structured line: a bare URL anywhere in the report would also match the text of a /raise-pr
# that declined to open one and merely quoted the PR that already exists.
PR_VERDICT='^RAISED pr=https://github\.com/[^[:space:]]+/pull/[0-9]+'

# fail <position> <name> <reason> — the closing message, then stop. Nothing is undone: the
# branch and working tree stay exactly as the failed stage left them, for diagnosis.
fail() {
  echo "chain: FAILED at [$1/5] $2 — $3"
  if [ -n "${STAGE_SESSION:-}" ]; then
    echo "chain: no later stage ran; reopen that stage with \`claude --resume $STAGE_SESSION\`."
  else
    echo "chain: no later stage ran; no session id was captured, so reopen the most recent headless session in this repo with \`claude --resume\`."
  fi
  exit 1
}

# run_stage <position> <name> <prompt> <verdict ERE> <limit> [session id to resume]
# Prints the stage's report, and leaves the report text in $STAGE_RESULT and the session id in
# $STAGE_SESSION — globals the caller reads (the study stages resume them, and the closing line
# reads the last report). A success verdict is searched for anywhere in the report: it is not
# reliably the last line.
run_stage() {
  local pos=$1 name=$2 prompt=$3 verdict=$4 limit=$5 resume=${6:-}
  local reply rc reason is_error subtype
  # Until the reply is parsed the only session this stage has is the one it resumed, so that is
  # what STAGE_SESSION holds. Without this a stage that fails before the parse below — a stall, a
  # launch failure, a reply that is not JSON — leaves the previous stage's id in place, and the
  # closing line sends them to an already-finished session. Fresh stages reset to empty, which is
  # the "no session id was captured" fallback; the study passes keep the id they were resuming.
  STAGE_SESSION=$resume
  echo "chain: [$pos/5] $name — starting"
  # The prompt must be the positional straight after -p, and stdin must be redirected, or the
  # call stalls on the terminal (both recorded in plugin-report.sh). The prompt stays quoted
  # because a variadic flag would otherwise eat its second word as a separate positional.
  reply=$(timeout --kill-after=60 "$limit" claude -p "$prompt" --model "$CHAIN_MODEL" --output-format json --dangerously-skip-permissions ${resume:+--resume "$resume"} </dev/null)
  rc=$?
  # A stage that exits 124 on its own account is reported as a stall too: telling them apart
  # needs --preserve-status and a wall-clock check, which is not worth it.
  case $rc in
    0) ;;
    124|137) fail "$pos" "$name" "stalled — exceeded ${limit}s" ;;
    125|126|127) fail "$pos" "$name" "the stage could not be launched (exit $rc)" ;;
    *) fail "$pos" "$name" "claude exited with $rc" ;;
  esac
  # Parsed once, checking that it is JSON at all: a plain-text error from claude would otherwise
  # become an empty report and be misdiagnosed below as a stage that produced no verdict.
  if ! jq -e . >/dev/null 2>&1 <<<"$reply"; then
    printf '%s\n' "$reply" >&2
    fail "$pos" "$name" "reply was not JSON (raw reply above)"
  fi
  is_error=$(jq -r '.is_error // false' <<<"$reply")
  subtype=$(jq -r '.subtype // empty' <<<"$reply")
  STAGE_RESULT=$(jq -r '.result // empty' <<<"$reply")
  STAGE_SESSION=$(jq -r '.session_id // empty' <<<"$reply")
  [ -n "$STAGE_RESULT" ] && printf '%s\n' "$STAGE_RESULT"
  # claude reports an API error, an interrupted run or an exhausted turn limit inside the JSON
  # while still exiting 0. Without this the run dies at the verdict check instead, blaming the
  # stage for a missing token when in truth the stage never ran.
  if [ "$is_error" = true ]; then
    fail "$pos" "$name" "claude reported an error${subtype:+ ($subtype)} — ${STAGE_RESULT:-no detail}"
  fi
  # A stage's own FAILED verdict wins even when a success verdict appears in the same report.
  # It must open its own line: /study's job is recording what went wrong, so its report quotes
  # the phrase in ordinary prose, and an unanchored scan would abort a healthy run over it.
  reason=$(grep -m1 -oE '^[[:space:]]*[*_>-]*[[:space:]]*FAILED reason=.*' <<<"$STAGE_RESULT")
  if [ -n "$reason" ]; then fail "$pos" "$name" "${reason#*FAILED reason=}"; fi
  # Deliberately not anchored, unlike the two scans above: only /raise-pr's prompt pins its verdict
  # to a line of its own, so the other four may arrive decorated as markdown, and an anchor would
  # abort a healthy run over a bullet. A report quoting someone else's success verdict is covered
  # by the FAILED scan running first.
  grep -qE -- "$verdict" <<<"$STAGE_RESULT" || fail "$pos" "$name" "no recognisable verdict"
  echo "chain: [$pos/5] $name — ok"
}

# Every tool the run depends on, named before an hour of model time is spent. Without this a
# missing jq reads as a stage with no verdict and a missing git as a clean tree on no branch —
# each blaming the model for a tool that was never installed.
for tool in git claude gh jq timeout; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    echo "chain: refused — $tool is not on PATH; no stage was started."
    exit 1
  fi
done

# Only the starting point is checked here: /build checks the fetch, unpushed commits and the
# issue itself. git's own exit code is tested, so an unreadable repository says so rather than
# passing the clean-tree test on empty output and refusing on a blank branch name.
if ! tree_status=$(git status --porcelain 2>&1); then
  echo "chain: refused — git could not read this repository: $tree_status"
  exit 1
fi
if [ -n "$tree_status" ]; then
  echo "chain: refused — working tree is not clean; no stage was started."
  exit 1
fi
if ! branch=$(git rev-parse --abbrev-ref HEAD 2>&1); then
  echo "chain: refused — git could not determine the current branch: $branch"
  exit 1
fi
if [ "$branch" != main ]; then
  echo "chain: refused — $branch is checked out, not main; no stage was started."
  exit 1
fi

run_stage 1 build "/build $issue" 'READY branch=' "$BUILD_LIMIT"
build_session=$STAGE_SESSION
# The study passes exist to study what the stage before them saw. A reply with no session id
# would resume nothing and study a fresh session, which reports an honest, empty success.
[ -n "$build_session" ] || fail 1 build "reply carried no session id, so the study pass could not resume it"
run_stage 2 study "/study $issue" 'STUDIED issue=' "$STUDY_LIMIT" "$build_session"
run_stage 3 verify /verify 'VERIFIED branch=' "$VERIFY_LIMIT"
verify_session=$STAGE_SESSION
[ -n "$verify_session" ] || fail 3 verify "reply carried no session id, so the study pass could not resume it"
run_stage 4 study "/study $issue" 'STUDIED issue=' "$STUDY_LIMIT" "$verify_session"
run_stage 5 raise-pr "/raise-pr $issue" "$PR_VERDICT" "$RAISE_PR_LIMIT"

pr_url=$(grep -m1 -oE -- "$PR_VERDICT" <<<"$STAGE_RESULT")
echo "chain: done — ${pr_url#RAISED pr=}"
exit 0
