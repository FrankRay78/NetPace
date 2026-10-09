#!/usr/bin/env bash
#
# chain.sh — carry one GitHub issue from a clean main to an open pull request, unattended.
#
# Runs /build, /study, /verify, /study, /raise-pr for the named issue, each as its own headless
# `claude -p` process, starting a stage only after the previous one reported its own success
# verdict on the last line of its report. Each study pass resumes the session of the stage it
# follows. The first stage that fails — a FAILED verdict, a verdict the chain cannot read, a
# non-zero exit, or a stall past its time limit — stops the run, and the closing line names the
# stage, its position and the reason.
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
# 7200s (2h), sized from a measured run rather than a per-round multiplier. On #328's branch the
# unattended chain ran three full rounds, each with fixes and a suite re-run, in about 48 minutes,
# and a follow-up round cost roughly a third of round one; verify.md's four rounds, the last of
# which never fixes, should land near an hour. The limit is the stage's only stall detector, so
# the headroom is deliberately about twice that projection and no more.
VERIFY_LIMIT=${CHAIN_STAGE_TIMEOUT:-7200}
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
# Zero is a whole number and passes to `timeout` happily, where it means *no limit* — which
# silently removes the only stall detector any stage has, so a hung run is never reported at all.
# Tested for a non-empty value rather than through ${VAR:-0}, so an unset or empty override, which
# the limits above already treat as no override, is not read as a zero one.
if [ -n "${CHAIN_STAGE_TIMEOUT:-}" ]; then
  case "$CHAIN_STAGE_TIMEOUT" in *[!0-9]*)
    echo "chain: refused — CHAIN_STAGE_TIMEOUT must be a whole number of seconds; got '$CHAIN_STAGE_TIMEOUT'." >&2; exit 1 ;;
  esac
  if [ "$CHAIN_STAGE_TIMEOUT" -eq 0 ]; then
    echo "chain: refused — CHAIN_STAGE_TIMEOUT=0 means no time limit, so a hung stage would never be reported; no stage was started." >&2; exit 1
  fi
fi

# HOW A STAGE'S VERDICT IS READ: BY POSITION. The report's final non-blank line is the verdict,
# and no other line of the report is read at all — so nothing the report quotes, in a code block,
# a list, a table or a quotation, can be taken for the stage's own decision. Before #345 the chain
# scanned the whole report for verdict-shaped prose, and that reading was wrong in both directions:
# #333's own run was stopped at a /build that had succeeded, over a failure verdict the report
# showed as RED evidence inside a code block; and, the dangerous direction, a failed /verify whose
# report also carried a success-shaped phrase was read as a success and carried through to a real
# pull request. Every added pattern brought its own exceptions, and dropping fenced blocks before
# the scan was tried and reverted (#333, dd1cdfe/d2addd8) because an unclosed fence hid correctly
# written verdicts. Position cannot be faked by anything the report quotes, and it removes pattern
# tolerance instead of adding to it.
#
# The one tolerance kept is decoration ON that line, because the verdict is written by a model and
# prompts asking for a plain line do not always get one: one leading heading, blockquote, bullet or
# numbered-item marker — one, not a stack of them — and a run of `*`, `_` or backticks wrapping the
# verdict. #242 was a verified branch parked over ``## VERIFIED `branch=…` ``. Anything else — a
# verdict fenced, buried mid-sentence, or sharing its line with anything else at all — is no
# verdict, and the run stops saying so rather than guessing. That is the cheap direction to be
# wrong in: a false stop costs the rest of one run, a false success opens a pull request off
# unverified work.
#
# Two consequences of that last clause, both of which cost a false success before they were closed:
# the success patterns are anchored at BOTH ends with a non-empty payload, so a line that opens with
# the verdict word and then contradicts itself (`VERIFIED branch=x, FAILED reason=suite red`, or
# `VERIFIED branch=x — except the suite is red`) is not a success. Anchoring only the start left the
# failure test — which matches `^FAILED` and so cannot see a `FAILED` mid-line — unable to catch it.
# And only up to three leading spaces are stripped, because four spaces or a tab open markdown's
# other kind of code block: stripping those indiscriminately let an indented block quoting a success
# verdict sit last and be read as the stage's own, which is precisely what position is meant to
# prevent.

# One leading markdown marker, with its whitespace. `-`, `*` and `+` count as a bullet only when
# whitespace follows, so the `**` opening a bold verdict is not read as one and left half-stripped.
VERDICT_MARKER='^(#+[[:space:]]+|>[[:space:]]*|[-+*][[:space:]]+|[0-9]+[.)][[:space:]]+)'
# A run of emphasis characters opening the verdict line.
VERDICT_EMPHASIS='^([*_`]+)'
# A stage's own failure verdict, and its reason. Whitespace and punctuation are tolerated between
# the two words, which is where a model's markup lands. Unanchored at the end, unlike the four
# success patterns below: the reason is free prose and runs to the end of the line by definition.
FAIL_VERDICT='^FAILED[^[:alnum:]]*reason=(.*)$'
# The three success verdicts whose payload is a single token. Anchored at both ends, and the payload
# must be non-empty: `VERIFIED branch=` is a truncated report, not a verified branch, and anything
# after the payload means the line is not just the verdict.
READY_VERDICT='^READY[^[:alnum:]]*branch=[^[:space:]]+$'
STUDIED_VERDICT='^STUDIED[^[:alnum:]]*issue=[0-9]+[^[:alnum:]]+rows=[0-9]+[^[:alnum:]]*$'
VERIFIED_VERDICT='^VERIFIED[^[:alnum:]]*branch=[^[:space:]]+$'
# raise-pr's verdict. Unlike the other four the payload is a URL, so the pattern carries that URL's
# shape and the line must still open with the verdict: a bare URL is not a verdict, because the
# commonest failure at that stage — a pull request already open for the branch — reports an error
# whose text contains a perfectly good pull request URL. The URL must be bare: a markdown autolink
# (`<…>`) or link (`[…](…)`) around it is not read, and raise-pr.md says so.
PR_VERDICT='^RAISED[^[:alnum:]]*pr=(https://github\.com/[^[:space:]]+/pull/[0-9]+)[^[:alnum:]]*$'

# read_verdict <report> — the report's verdict line, normalised, left in $STAGE_VERDICT. Empty when
# the report has no non-blank line at all, which is no verdict like any other unreadable line.
read_verdict() {
  local line='' l open
  # The final non-blank line. The carriage-return strip is belt-and-braces rather than load-bearing
  # — `[[:space:]]` already covers \r both in the blank test below and in the trailing-space strip —
  # but it keeps the \r off the reason at the point the line is chosen, where it is easiest to see.
  while IFS= read -r l || [ -n "$l" ]; do
    l=${l%$'\r'}
    case $l in *[![:space:]]*) line=$l ;; esac
  done <<<"$1"
  # Up to three leading spaces only: four, or a tab, is markdown's indented code block, and a
  # quotation that can be indented into position is a quotation that can fake being the verdict.
  line=$(sed -E -e 's/^ {0,3}//' -e "s/$VERDICT_MARKER//" -e 's/[[:space:]]+$//' <<<"$line")
  if [[ $line =~ $VERDICT_EMPHASIS ]]; then
    open=${BASH_REMATCH[1]}
    line=${line#"$open"}
    # The closing run is stripped only when the same run closes the line that it opened, so a
    # reason ending in a backtick keeps it — including under a balanced wrap, where the run strips
    # the wrapper's backtick and leaves the reason's. The one shape that loses a character is an
    # unbalanced wrap, where the single trailing backtick is both at once; telling those apart
    # needs a markdown parser, and the reason still arrives.
    line=${line%"$open"}
    # The marker strip above ran before the emphasis run was removed, so re-strip the space a
    # shape like `** FAILED reason=…**` leaves behind — otherwise the line is unreadable and the
    # stage's own reason is lost with it.
    line=${line#"${line%%[![:space:]]*}"}
  fi
  STAGE_VERDICT=$line
}

# branch_state — what a stopping run leaves behind: the commits the current branch holds over
# main, and whether the working tree is dirty. Read-only, like everything else the chain does to
# the repository. Printed *before* fail()'s closing lines, not after, so the two closing lines stay
# the last thing in the output: chain.tests.sh reads them from the tail, and so does a person.
branch_state() {
  local branch commits status
  if ! branch=$(git rev-parse --abbrev-ref HEAD 2>/dev/null) || [ -z "$branch" ]; then
    echo "chain: the branch state could not be read; git did not name a branch."
  elif ! commits=$(git log main..HEAD --oneline 2>/dev/null); then
    echo "chain: the commits $branch holds over main could not be read."
  elif [ -z "$commits" ]; then
    echo "chain: $branch holds no commits over main."
  else
    echo "chain: $branch holds $(grep -c '' <<<"$commits") commit(s) over main:"
    sed 's/^/  /' <<<"$commits"
  fi
  if ! status=$(git status --porcelain 2>/dev/null); then
    echo "chain: the working tree state could not be read."
  elif [ -z "$status" ]; then
    echo "chain: the working tree is clean."
  else
    echo "chain: the working tree has uncommitted changes ($(grep -c '' <<<"$status") path(s))."
  fi
}

# fail <position> <name> <reason> — the branch state, the closing message, then stop. Nothing is
# undone: the branch and working tree stay exactly as the failed stage left them, for diagnosis.
# The state lines exist because a stage killed at its time limit leaves no report at all, and a
# /verify stopped mid-loop can leave green, committed, unread fix commits behind — so the closing
# output has to say whether there is work on the branch before anyone continues it by hand.
fail() {
  branch_state
  echo "chain: FAILED at [$1/5] $2 — $3"
  if [ -n "${STAGE_SESSION:-}" ]; then
    echo "chain: no later stage ran; reopen that stage with \`claude --resume $STAGE_SESSION\`."
  else
    echo "chain: no later stage ran; no session id was captured, so reopen the most recent headless session in this repo with \`claude --resume\`."
  fi
  exit 1
}

# run_stage <position> <name> <prompt> <success verdict ERE> <limit> [session id to resume]
# Prints the stage's report, and leaves the report text in $STAGE_RESULT, the normalised verdict
# line in $STAGE_VERDICT and the session id in $STAGE_SESSION — globals the caller reads (the study
# stages resume the session, and the closing line reads the last stage's verdict). The success ERE
# is matched against the verdict line alone, anchored at both ends, so neither a longer word merely
# ending in the verdict word (`UNVERIFIED branch=…`) nor a verdict sharing its line with anything
# else (`VERIFIED branch=x, FAILED reason=…`) is that verdict.
run_stage() {
  local pos=$1 name=$2 prompt=$3 verdict=$4 limit=$5 resume=${6:-}
  local reply rc reason reply_type is_error subtype started elapsed
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
  started=$SECONDS
  reply=$(timeout --kill-after=60 "$limit" claude -p "$prompt" --model "$CHAIN_MODEL" --output-format json --dangerously-skip-permissions ${resume:+--resume "$resume"} </dev/null)
  rc=$?
  elapsed=$((SECONDS - started))
  # 124 is timeout's own report that the limit was reached. 137 is the stage killed by SIGKILL,
  # which `timeout --kill-after` also produces once its TERM has gone unheeded — so the elapsed
  # time, not the exit code, is what tells a stall from a kill that arrived from outside (an
  # out-of-memory kill is the real case). Calling that a stall sends whoever reads the closing line
  # hunting a hung stage that never existed, when what they need to look at is the machine. A stage
  # that exits 124 on its own account is still reported as a stall: telling that apart needs
  # --preserve-status, which is not worth it.
  if [ "$rc" = 137 ] && [ "$elapsed" -lt "$limit" ]; then
    fail "$pos" "$name" "killed (exit 137) after ${elapsed}s, before its ${limit}s time limit"
  fi
  case $rc in
    0) ;;
    124|137) fail "$pos" "$name" "stalled — exceeded ${limit}s" ;;
    125|126|127) fail "$pos" "$name" "the stage could not be launched (exit $rc)" ;;
    *) fail "$pos" "$name" "claude exited with $rc" ;;
  esac
  # Parsed once, checking that it is JSON at all: a plain-text error from claude would otherwise
  # become an empty report and be misdiagnosed below as a stage that produced no verdict. `jq -e`
  # on `type` rather than on the document itself, so a reply of `null` or `false` — valid JSON,
  # which jq -e reports as a falsy last output — is not blamed on the parser. The type that same
  # parse printed is what the shape check below reads, so the reply is never parsed twice.
  if ! reply_type=$(jq -e -r 'type' 2>/dev/null <<<"$reply"); then
    printf '%s\n' "$reply" >&2
    fail "$pos" "$name" "reply was not JSON (raw reply above)"
  fi
  # Every field below is read off an object. A reply that is valid JSON but an array, a string or a
  # number yields nothing for any of them, which would otherwise surface as a stage that produced
  # no verdict — blaming the stage for a reply shape it never chose.
  if [ "$reply_type" != object ]; then
    printf '%s\n' "$reply" >&2
    fail "$pos" "$name" "reply was not a JSON object (raw reply above)"
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
  # The verdict, read by position — see the block above read_verdict. What stops a failed stage
  # starting the next one is position, not this test order: `^FAILED` and the four success patterns
  # are mutually exclusive on one normalised line, so neither ordering could read both. Failure is
  # tested first only so the stage's own reason is what the closing line reports.
  read_verdict "$STAGE_RESULT"
  if [[ $STAGE_VERDICT =~ $FAIL_VERDICT ]]; then
    reason=$(sed -E -e 's/^[[:space:]]+//' -e 's/[[:space:]]+$//' <<<"${BASH_REMATCH[1]}")
    # A bare `FAILED reason=` is a malformed report, but the stage still failed. Say so, rather
    # than closing with a dangling dash that reads like the reason was lost in transit.
    fail "$pos" "$name" "${reason:-the stage reported a failure with no reason}"
  fi
  # Neither verdict readable: the run stops, and says that rather than naming a reason it does not
  # have. Reported distinctly from a stage that failed with a reason, because they send whoever
  # reads the closing line to different places — the stage's own work, or the way it wrote its
  # report. A verdict the chain cannot read is never retried and never guessed at.
  [[ $STAGE_VERDICT =~ $verdict ]] \
    || fail "$pos" "$name" "no readable verdict — the last line of the report is not a verdict"
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

# Everything from the first stage on is one brace group, closed by its own `exit`. Bash reads a
# script from disk as it runs it, but parses a whole compound command before running any of it —
# so a stage that edits this file in place, as any issue whose work is the chain itself must,
# cannot change what the rest of the run does. Without the group the run carried on from its old
# position in the new file and executed whatever text it landed in (#348).
{
  run_stage 1 build "/build $issue" "$READY_VERDICT" "$BUILD_LIMIT"
  build_session=$STAGE_SESSION
  # The study passes exist to study what the stage before them saw. A reply with no session id
  # would resume nothing and study a fresh session, which reports an honest, empty success.
  [ -n "$build_session" ] || fail 1 build "reply carried no session id, so the study pass could not resume it"
  run_stage 2 study "/study $issue" "$STUDIED_VERDICT" "$STUDY_LIMIT" "$build_session"
  run_stage 3 verify /verify "$VERIFIED_VERDICT" "$VERIFY_LIMIT"
  verify_session=$STAGE_SESSION
  [ -n "$verify_session" ] || fail 3 verify "reply carried no session id, so the study pass could not resume it"
  run_stage 4 study "/study $issue" "$STUDIED_VERDICT" "$STUDY_LIMIT" "$verify_session"
  run_stage 5 raise-pr "/raise-pr $issue" "$PR_VERDICT" "$RAISE_PR_LIMIT"

  # Re-matched rather than read out of run_stage's own match, so the URL reported is demonstrably
  # the one on the verdict line this script just accepted.
  pr_url=''
  [[ $STAGE_VERDICT =~ $PR_VERDICT ]] && pr_url=${BASH_REMATCH[1]}
  echo "chain: done — $pr_url"
  exit 0
}
