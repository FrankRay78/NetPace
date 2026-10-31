#!/usr/bin/env bash
#
# chain.sh — carry one GitHub issue from a clean main to an open pull request, unattended.
#
# Runs /build, /verify, /raise-pr for the named issue, each as its own headless `claude -p`
# process, starting a stage only after the previous one reported its own success verdict at the
# end of its report. Every stage starts a fresh session and inherits no context. The first
# stage that fails — a failure verdict, a verdict the chain cannot read, a non-zero exit, or a
# stall past its time limit — stops the run, and the closing line names the stage, its position
# and the reason.
#
# WHY. Typed by hand, the chain spends most of its wall-clock waiting for someone to read one
# stage's report and start the next. The ordering is enforced here, outside the model, so no
# stage can start over unverified work.
#
# One failure is told apart from the rest: a stage the account's own Claude usage limit cut off,
# which is not a fault in the issue and needs no diagnosis. It is recognised from the reply's
# typed error field alone, the closing line names the limit and quotes the reply's reset wording,
# and the chain exits 75 rather than 1 so a runner can tell that stop from every other one
# without reading the prose.
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
# Each stage's model and effort come from the stage table, scripts/stage-models.tsv.
# Overrides: CHAIN_MODEL replaces every stage's model; the effort stays the table's.
# CHAIN_STAGE_TIMEOUT (seconds) replaces every stage's time limit.

# No -e: the `reply=$(...); rc=$?` dispatch below depends on a non-zero claude not killing the
# script, so the stage can be reported by name instead of the run dying silently.
set -uo pipefail

CHAIN_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Per-stage time limits in seconds, conservative until tuned from real runs.
BUILD_LIMIT=${CHAIN_STAGE_TIMEOUT:-7200}
# 7200s (2h), sized from a measured run. On #268's branch, about 2,400 added lines, /verify's
# review and its fixes took about 40 minutes from start to commit. The limit is the stage's only
# stall detector, so the headroom is deliberately about three times that and no more.
VERIFY_LIMIT=${CHAIN_STAGE_TIMEOUT:-7200}
RAISE_PR_LIMIT=${CHAIN_STAGE_TIMEOUT:-1800}

# The chain's own exit code for a stage the account's usage limit cut off, so a runner can tell
# that stop from every other failure without parsing the closing line's prose. 75 is sysexits'
# EX_TEMPFAIL — the failure that clears on its own — and collides with none of the codes the chain
# already reports: timeout's 124, the shell's 125-127, and 137 for a stage killed by SIGKILL.
USAGE_LIMIT_EXIT=75
# And for a stage the stage table does not answer: the fault is in the table, not the issue being
# built, so a runner must not park the issue for it. 78 is sysexits' EX_CONFIG.
STAGE_TABLE_EXIT=78

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
  echo "  2. verify: /verify"
  echo "  3. raise-pr: /raise-pr $issue"
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

# stage_model_effort <stage> — the stage's model and effort, space-separated, from the stage table.
# Every stage's is read here, before any stage starts, so a stage the table does not answer stops
# the run with nothing done, and a stage that rewrites the table cannot change a later stage's.
stage_model_effort() {
  local flags model effort
  flags=$(bash "$CHAIN_DIR/stage-flags.sh" "$1") || return 1
  read -r _ model _ effort <<<"$flags"
  echo "${CHAIN_MODEL:-$model} $effort"
}
table_refused() {
  echo "chain: refused — the stage table has no usable row for $1; no stage was started." >&2
  exit "$STAGE_TABLE_EXIT"
}
BUILD_MODEL_EFFORT=$(stage_model_effort build) || table_refused build
VERIFY_MODEL_EFFORT=$(stage_model_effort verify) || table_refused verify
RAISE_PR_MODEL_EFFORT=$(stage_model_effort raise-pr) || table_refused raise-pr

# HOW A STAGE'S VERDICT IS READ: BY POSITION. The verdict is whatever ENDS the report — the final
# non-blank line, or the fenced block that closes the report — and no other part of the report is
# read at all, so nothing the report quotes above it, in a code block, a list, a table or a
# quotation, can be taken for the stage's own decision. Before #345 the chain scanned the whole
# report for verdict-shaped prose, and that reading was wrong in both directions: #333's own run
# was stopped at a /build that had succeeded, over a failure verdict the report showed as RED
# evidence inside a code block; and, the dangerous direction, a failed /verify whose report also
# carried a success-shaped phrase was read as a success and carried through to a real pull request.
# Every added pattern brought its own exceptions, and dropping fenced blocks before the scan was
# tried and reverted (#333, dd1cdfe/d2addd8) because an unclosed fence hid correctly written
# verdicts. Position cannot be faked by anything the report quotes, and it removes pattern
# tolerance instead of adding to it.
#
# WHAT IT READS THERE: one JSON object, the verdict docs/verdict-schema.md states — a schema
# version, an outcome, and that outcome's payload. The version is checked before anything else is
# read, and one this script does not know stops the run naming it, so a verdict the chain cannot
# read is never guessed at. Fields it does not know are ignored, so a prompt can add to the verdict
# without a change here. The text verdict this replaced (`READY branch=…` and its kin) is prose to
# this script and is reported as no verdict: the prompts and the chain changed together.
#
# The one tolerance kept is decoration AROUND the object, because the verdict is written by a model
# and prompts asking for a bare line do not always get one: one leading heading, blockquote, bullet
# or numbered-item marker — one, not a stack of them — a run of `*`, `_` or backticks wrapping the
# line, an indent of up to three spaces, and a code fence around the object when that fence closes
# the report. #242 was a verified branch parked over a heading and backticks on a text verdict.
# Anything else — a verdict buried mid-sentence, sharing its line with anything else, or followed by
# anything but blank lines — is not a JSON object, so it is no verdict, and the run stops saying so
# rather than guessing. That is the cheap direction to be wrong in: a false stop costs the rest of
# one run, a false success opens a pull request off unverified work.
#
# Two consequences of that last clause, both of which cost a false success on the text verdict this
# replaced: a success verdict that also carries a reason contradicts itself and is not a success —
# the JSON shape of `VERIFIED branch=x, FAILED reason=suite red`, the hedge that once passed the
# gate. And only up to three leading spaces are stripped, because four spaces or a tab open
# markdown's other kind of code block: stripping those indiscriminately let an indented block
# quoting a success verdict sit last and be read as the stage's own, which is precisely what
# position is meant to prevent. jq tolerates leading whitespace, so the bare line must open with
# the object's own brace once the three spaces are gone.

# The verdicts this script reads are written by the stage prompts: .claude/commands/build.md
# (ready), verify.md (verified) and raise-pr.md (raised), and all three write failed. Their shape
# is docs/verdict-schema.md's, and this is the one version of it this script reads. A change to
# the schema is a change to what those prompts tell the model to write, and the reverse.
VERDICT_SCHEMA=1

# One leading markdown marker, with its whitespace. `-`, `*` and `+` count as a bullet only when
# whitespace follows, so the `**` opening a bold verdict is not read as one and left half-stripped.
VERDICT_MARKER='^(#+[[:space:]]+|>[[:space:]]*|[-+*][[:space:]]+|[0-9]+[.)][[:space:]]+)'
# A run of emphasis characters opening the verdict line.
VERDICT_EMPHASIS='^([*_`]+)'
# A code fence, after up to three spaces of indent. The opening fence may carry an info string
# (```json); the closing one carries nothing.
FENCE_OPEN='^ {0,3}(`{3,}|~{3,})[[:space:]]*[[:alnum:]_-]*[[:space:]]*$'
FENCE_CLOSE='^ {0,3}(`{3,}|~{3,})[[:space:]]*$'
# raise-pr's payload: a bare pull request URL and nothing around it. The commonest failure at that
# stage — a pull request already open for the branch — reports an error whose text contains a
# perfectly good pull request URL, which is why the URL is read from the verdict's own field and
# never from the prose. A markdown autolink (`<…>`) or link (`[…](…)`) around it is not read, and
# raise-pr.md says so.
PR_URL='^https://github\.com/[^[:space:]]+/pull/[0-9]+$'

# read_verdict <report> — the text that ends the report, left in $STAGE_VERDICT: the final
# non-blank line with its decoration stripped, or the body of the fenced block that closes the
# report. Empty when the report has no non-blank line at all, or ends on a closing fence nothing
# opened — no verdict, like any other unreadable ending. Whether the text IS a verdict is decided
# by stage_verdict below; this only chooses which text is read.
read_verdict() {
  local -a lines=()
  local l n i line open
  # The carriage-return strip is belt-and-braces rather than load-bearing — `[[:space:]]` already
  # covers \r in the blank test below and in the trailing-space strip — but it keeps the \r off the
  # verdict at the point the lines are read, where it is easiest to see.
  while IFS= read -r l || [ -n "$l" ]; do
    lines+=("${l%$'\r'}")
  done <<<"$1"
  n=${#lines[@]}
  while [ "$n" -gt 0 ] && [[ ! ${lines[n-1]} =~ [^[:space:]] ]]; do n=$((n-1)); done
  STAGE_VERDICT=''
  [ "$n" -gt 0 ] || return 0
  if [[ ${lines[n-1]} =~ $FENCE_CLOSE ]]; then
    # The fence that closes the report. Its whole body is the verdict, so a block holding more than
    # the one object is unreadable rather than read for the last line in it.
    for ((i = n - 2; i >= 0; i--)); do
      if [[ ${lines[i]} =~ $FENCE_OPEN ]]; then
        STAGE_VERDICT=$(printf '%s\n' "${lines[@]:i+1:n-i-2}")
        return 0
      fi
    done
    return 0
  fi
  # Up to three leading spaces only: four, or a tab, is markdown's indented code block, and a
  # quotation that can be indented into position is a quotation that can fake being the verdict.
  line=$(sed -E -e 's/^ {0,3}//' -e "s/$VERDICT_MARKER//" -e 's/[[:space:]]+$//' <<<"${lines[n-1]}")
  if [[ $line =~ $VERDICT_EMPHASIS ]]; then
    open=${BASH_REMATCH[1]}
    line=${line#"$open"}
    # The closing run is stripped only when the same run closes the line that it opened. The
    # object's own last character is always its closing brace, so a reason ending in a backtick
    # is inside the object and never confused with the wrapper.
    line=${line%"$open"}
    # The marker strip above ran before the emphasis run was removed, so re-strip the space a
    # shape like `** {…}**` leaves behind — otherwise the line is unreadable and the stage's own
    # reason is lost with it.
    line=${line#"${line%%[![:space:]]*}"}
  fi
  # The indent rule above is only a rule if the leftover whitespace is not forgiven here: jq reads
  # an object past any amount of leading whitespace, so a four-space indent would otherwise pass.
  case $line in '{'*) STAGE_VERDICT=$line ;; esac
}

# verdict_string <field> — the verdict's <field> when it is a JSON string, and nothing otherwise, so
# a number, object or null in a field the chain reads is treated as that field missing.
verdict_string() {
  jq -r --arg f "$1" 'if (.[$f] | type) == "string" then .[$f] else empty end' <<<"$STAGE_VERDICT"
}

# stage_verdict <position> <name> <success outcome> — read $STAGE_VERDICT as the verdict, leaving the
# payload the stage reported in $STAGE_PAYLOAD, or stop the run with the reason the verdict gives or
# the reason it cannot be read. Every refusal that is not the stage's own reason, and not an unknown
# schema version, is reported as `no readable verdict` with what was wrong after the dash: the two
# send whoever reads the closing line to different places — the stage's own work, or the way it
# wrote its report. A verdict the chain cannot read is never retried and never guessed at.
stage_verdict() {
  local pos=$1 name=$2 success=$3 verdict_type schema outcome reason
  # One JSON object and nothing else: two objects, an array, a string, prose, the old text
  # verdict and an empty report all stop here. `jq -e` exits 4 on no input, which covers the empty
  # case without a test of its own.
  verdict_type=$(jq -e -r 'type' 2>/dev/null <<<"$STAGE_VERDICT") || verdict_type=''
  if [ "$verdict_type" != object ]; then
    fail "$pos" "$name" "no readable verdict — the report does not end with a JSON verdict"
  fi
  # The version before anything else, printed as the JSON it was written as, so a version written
  # as a string shows its quotes in the closing line rather than passing as the number it is not.
  schema=$(jq -r 'if .schema == null then empty else (.schema | tojson) end' <<<"$STAGE_VERDICT")
  if [ -z "$schema" ]; then
    fail "$pos" "$name" "no readable verdict — the JSON ending the report carries no schema version"
  fi
  if [ "$schema" != "$VERDICT_SCHEMA" ]; then
    fail "$pos" "$name" "the verdict has schema version $schema, which this chain does not read"
  fi
  outcome=$(verdict_string outcome)
  if [ -z "$outcome" ]; then
    fail "$pos" "$name" "no readable verdict — the verdict names no outcome"
  fi
  # Failure is read before success only so the stage's own reason is what the closing line
  # reports; the outcome is one string, so no ordering could read both.
  if [ "$outcome" = failed ]; then
    # The reason lands on the one closing line chain-next.sh reads, so a line break inside the JSON
    # string — legal, and a plausible thing to write — is flattened to a space with the rest of the
    # whitespace runs, rather than cutting the parked reason off at its first line.
    reason=$(verdict_string reason | tr -s '[:space:]' ' ' | sed -E -e 's/^ //' -e 's/ $//')
    # A failure with no reason is a malformed report, but the stage still failed. Say so, rather
    # than closing with a dangling dash that reads like the reason was lost in transit.
    fail "$pos" "$name" "${reason:-the stage reported a failure with no reason}"
  fi
  if [ "$outcome" != "$success" ]; then
    fail "$pos" "$name" "no readable verdict — the verdict's outcome is \"$outcome\", not \"$success\" or \"failed\""
  fi
  if [ "$(jq -r 'has("reason")' <<<"$STAGE_VERDICT")" = true ]; then
    fail "$pos" "$name" "no readable verdict — a $success verdict that also carries a reason contradicts itself"
  fi
  # The payload, and its shape. An empty or missing one is a truncated report, not a verified
  # branch or an opened pull request, and must not read as a verdict for whatever the reader assumes.
  case $success in
    raised)
      STAGE_PAYLOAD=$(verdict_string pr)
      [[ $STAGE_PAYLOAD =~ $PR_URL ]] \
        || fail "$pos" "$name" "no readable verdict — the raised verdict names no pull request URL" ;;
    *)
      STAGE_PAYLOAD=$(verdict_string branch)
      [[ $STAGE_PAYLOAD =~ ^[^[:space:]]+$ ]] \
        || fail "$pos" "$name" "no readable verdict — the $success verdict names no branch" ;;
  esac
}

# usage_limit_reason <reply> — the reason line for a stage the account's usage limit stopped, or
# nothing (and a non-zero return) when the reply does not say one did.
#
# Read from the reply's typed `api_error` field ALONE, never from the report text. claude sets it
# to usage_limit_reached only when the account's own limit — a session, weekly or per-model plan
# limit, or a spend cap — refused the request, and no other refusal sharing its rate_limit error
# sets it. The text is the wrong source for the same reason the verdict is read by position: a
# report may quote a limit message it merely read about, and such a report must not be mistaken
# for a limit stop.
#
# claude writes the reply and exits non-zero on an API error, so this is read before the exit code
# is dispatched on; a reply that is absent, not JSON, or carries no such field leaves every other
# failure reported exactly as before.
usage_limit_reason() {
  local detail
  [ "$(jq -r '.api_error // empty' 2>/dev/null <<<"$1")" = usage_limit_reached ] || return 1
  # The limit's own message, quoted verbatim and not converted: it carries the reset time in the
  # reader's own timezone ("You've hit your session limit · resets 3:20pm (Europe/Berlin)"), and
  # nothing else in a result reply does — the typed error's reset field rides on the assistant
  # message, not on the result object the chain reads. Its last non-blank line only, because the
  # closing line is one line and a multi-line reason would break the runner's reading of it.
  #
  # The carriage returns go through `tr`, not the `sed` below, and must stay there: BSD sed reads
  # `s/\r$//` as a literal `r`, so on macOS it would eat the message's last character — quoting a
  # reset time one digit short. The trailing-space strip looks like it covers \r on its own, and
  # does under GNU sed, which is exactly how the bug hides.
  detail=$(jq -r '.result // empty' 2>/dev/null <<<"$1" | tr -d '\r' | sed -e 's/[[:space:]]*$//' -e '/^$/d' | tail -n 1)
  if [ -n "$detail" ]; then
    echo "the Claude usage limit was reached — $detail"
  else
    echo "the Claude usage limit was reached; the reply gave no reset time"
  fi
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

# fail <position> <name> <reason> [exit code] — the branch state, the closing message, then stop,
# with 1 unless the caller names another code. Nothing is undone: the branch and working tree
# stay exactly as the failed stage left them, for diagnosis. The state lines exist because a
# stage killed at its time limit leaves no report at all, and a stage that stops part-way can
# leave commits or uncommitted edits behind — so the closing output has to say whether there is
# work on the branch before anyone continues it by hand.
fail() {
  branch_state
  echo "chain: FAILED at [$1/3] $2 — $3"
  if [ -n "${STAGE_SESSION:-}" ]; then
    echo "chain: no later stage ran; reopen that stage with \`claude --resume $STAGE_SESSION\`."
  else
    echo "chain: no later stage ran; no session id was captured, so reopen the most recent headless session in this repo with \`claude --resume\`."
  fi
  exit "${4:-1}"
}

# run_stage <position> <name> <prompt> <success outcome> <limit> <model and effort>
# Prints the stage's report, and leaves the report text in $STAGE_RESULT, the verdict text in
# $STAGE_VERDICT, the payload it carried in $STAGE_PAYLOAD and the session id in $STAGE_SESSION —
# globals: the caller reads the last stage's payload for the `chain: done` line, and fail() reads
# the session id to name the session to reopen. The success outcome is matched against the
# verdict's outcome field exactly, so neither another stage's success nor a word merely containing
# the outcome (`unverified`) is that verdict.
run_stage() {
  local pos=$1 name=$2 prompt=$3 success=$4 limit=$5 model effort
  read -r model effort <<<"$6"
  local reply rc reply_type is_error subtype started elapsed limit_reason
  # Cleared before the call, so a stage that fails before the parse below — a stall, a launch
  # failure, a reply that is not JSON — does not leave the previous stage's id in place and send
  # the reader to an already-finished session. Empty is the "no session id was captured" fallback.
  STAGE_SESSION=
  echo "chain: [$pos/3] $name — starting"
  # The prompt must be the positional straight after -p, and stdin must be redirected, or the
  # call stalls on the terminal (both recorded in plugin-report.sh). The prompt stays quoted
  # because a variadic flag would otherwise eat its second word as a separate positional.
  started=$SECONDS
  reply=$(timeout --kill-after=60 "$limit" claude -p "$prompt" --model "$model" --effort "$effort" --output-format json --dangerously-skip-permissions </dev/null)
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
  # Before the exit code is dispatched on, because the limit's own exit code is an ordinary 1 and
  # would otherwise be reported as "claude exited with 1" — which is all the parked comment used
  # to say, and the reason the cause had to be dug out of the session transcript by hand.
  if limit_reason=$(usage_limit_reason "$reply"); then
    # The report and the session id, read here because this failure stops the run above the place
    # every other reply-bearing failure reads them. Without the report the limit's own message
    # reaches neither the output nor the log the parked comment points at, leaving only the one
    # line the reason could carry; without the session id the closing line claims no id was
    # captured, over a reply that carried one — and this is the failure whose session is most
    # worth reopening once the limit has reset.
    STAGE_RESULT=$(jq -r '.result // empty' 2>/dev/null <<<"$reply")
    STAGE_SESSION=$(jq -r '.session_id // empty' 2>/dev/null <<<"$reply")
    [ -n "$STAGE_RESULT" ] && printf '%s\n' "$STAGE_RESULT"
    fail "$pos" "$name" "$limit_reason" "$USAGE_LIMIT_EXIT"
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
  # The verdict, read by position — see the block above read_verdict — and then as the JSON it is.
  # Anything but this stage's success outcome with its payload stops the run inside stage_verdict,
  # with the stage's own reason where it gave one.
  read_verdict "$STAGE_RESULT"
  stage_verdict "$pos" "$name" "$success"
  echo "chain: [$pos/3] $name — ok"
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
  run_stage 1 build "/build $issue" ready "$BUILD_LIMIT" "$BUILD_MODEL_EFFORT"
  run_stage 2 verify /verify verified "$VERIFY_LIMIT" "$VERIFY_MODEL_EFFORT"
  run_stage 3 raise-pr "/raise-pr $issue" raised "$RAISE_PR_LIMIT" "$RAISE_PR_MODEL_EFFORT"

  # The payload the last stage's verdict was accepted on, so the URL reported is demonstrably the
  # one this script just checked against the pull request shape.
  echo "chain: done — $STAGE_PAYLOAD"
  exit 0
}
