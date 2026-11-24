#!/usr/bin/env bash
#
# traceability-check.sh — enforces the scenario-label → test-marker rule (CONVENTIONS.md,
# Acceptance criteria).
#
# The rule makes a scenario LABEL on a GitHub issue the traceability key that links an acceptance
# criterion to the test that verifies it:
#
#     issue   **Scenario: Server list is screened before measuring**
#        ↓  exact match, one way
#     test    // SCENARIO: Server list is screened before measuring
#
# This check enforces that one edge for ONE issue: the one the current branch implements. Every
# `**Scenario: X**` label in that issue must have at least one matching marker in a committed
# test file. Nothing historical is judged — the markers left by already-merged issues name
# labels this issue does not carry, and are simply not looked at.
#
# The direction matters and is deliberate. Label → marker only: a marker no label names is not a
# violation here, because a scenario can legitimately be covered by a test that already existed,
# and because the reverse direction would false-fail on every clean branch. "A marker must not be
# invented" is the other half of the rule and stays a judgement call for review.
#
# SCOPE — one issue, read off the branch. The `<prefix>/<N>-<slug>` shape `/gaffer:build` produces gives
# the number, exactly as /gaffer:raise-pr already parses it. A branch carrying no number has
# nothing to check and passes: a pull request raised outside the issue flow is valid.
#
# DESIGN RULE: fail CLOSED — the exception the .claude/hooks/ convention names, not an exception
# to it. That convention (.claude/hooks/README.md) is fail-open by default, because a false block
# inside an interactive turn can lock out the tools that would fix it, and fail-closed wherever
# silence would read as a pass. This is a merge gate, so it is squarely the second case: if it
# cannot fetch the issue, cannot find the repo, or is missing `gh`/`jq`, it fails and says why. A
# gate that passes without having run gives exactly the false comfort it exists to remove. Note
# the distinction it draws around a missing issue —
# GitHub answering "nothing at that number" is an ANSWER (pass, nothing to check); auth, network
# and rate-limit failures are the check not running (fail).
#
# Not a Claude Code hook — no .claude/settings.json entry. Two ways to run it:
#   In CI       .github/workflows/traceability.yml, on every pull request to main. The single
#               point of enforcement, so it sees the branch's final state however it got there.
#   Locally     bash scripts/traceability-check.sh [branch]
#               Run it before raising a PR (CONVENTIONS.md, Acceptance criteria), so a dropped marker is caught first.
#
# Both inputs are injectable, so a local run or a test can target any issue without renaming a
# branch: the branch as `$1` or $TRACEABILITY_BRANCH, the repository as $TRACEABILITY_REPO.
# Verify any edit with traceability-check.tests.sh.

set -uo pipefail

# Honours CLAUDE_PROJECT_DIR as the filesystem-reading hooks do, so a test matrix can point this
# at a sandbox tree by env var instead of relocating the script.
REPO_ROOT="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"

say()  { printf 'traceability-check: %s\n' "$*"; }
die()  { printf 'traceability-check: FAIL — %s\n' "$*" >&2; exit 1; }
# Leading/trailing whitespace only; internal spacing is part of the key.
trim() { sed -E 's/^[[:space:]]+//; s/[[:space:]]+$//'; }

# --- fail-closed preconditions ----------------------------------------------------
for tool in git gh jq grep sed awk tr mktemp; do
  command -v "$tool" >/dev/null 2>&1 \
    || die "'$tool' is not available, so the check cannot run. It is a merge gate, so it fails rather than reporting an unverified pass."
done

git -C "$REPO_ROOT" rev-parse --git-dir >/dev/null 2>&1 \
  || die "'$REPO_ROOT' is not a git repository, so the committed test files cannot be listed."

TMP="$(mktemp -d)" || die "could not create a temporary directory."
trap 'rm -rf "$TMP"' EXIT

# --- the issue this branch implements ---------------------------------------------
BRANCH="${1:-${TRACEABILITY_BRANCH:-}}"
if [ -z "$BRANCH" ]; then
  BRANCH="$(git -C "$REPO_ROOT" rev-parse --abbrev-ref HEAD 2>/dev/null)" \
    || die "could not read the current branch name."
fi
[ -n "$BRANCH" ] || die "could not read the current branch name."

# Only the LEADING number of the segment after the prefix, so `feature/net10-upgrade` yields
# nothing: a number guessed from elsewhere checks the wrong issue with full confidence.
ISSUE=""
if [[ "$BRANCH" == */* ]] && [[ "${BRANCH#*/}" =~ ^0*([0-9]+)- ]]; then
  ISSUE="${BASH_REMATCH[1]}"
fi

if [ -z "$ISSUE" ]; then
  say "no issue number in branch '$BRANCH' — nothing to check."
  exit 0
fi

REPO_SLUG="${TRACEABILITY_REPO:-}"
if [ -z "$REPO_SLUG" ]; then
  REPO_SLUG="$(cd "$REPO_ROOT" && gh repo view --json nameWithOwner -q .nameWithOwner 2>"$TMP/err")" \
    || die "could not resolve this repository with 'gh repo view': $(cat "$TMP/err")"
  [ -n "$REPO_SLUG" ] || die "'gh repo view' named no repository, so issue #$ISSUE cannot be fetched."
fi

if ! RESPONSE="$(gh issue view "$ISSUE" --json body,url -R "$REPO_SLUG" 2>"$TMP/err")"; then
  ERR="$(cat "$TMP/err")"
  # Read the stderr, not the exit code: both exit non-zero and they are opposite answers.
  # "Could not resolve" is an answer; anything else is the check failing to run.
  if printf '%s' "$ERR" | grep -qi 'could not resolve to an issue or pull request'; then
    say "#$ISSUE does not exist in $REPO_SLUG — nothing to check."
    exit 0
  fi
  die "could not retrieve issue #$ISSUE from $REPO_SLUG: $ERR"
fi

URL="$(printf '%s' "$RESPONSE" | jq -r '.url // empty' 2>/dev/null)"
[ -n "$URL" ] || die "could not parse 'gh issue view' output for #$ISSUE."

# GitHub numbers issues and pull requests from one sequence, so `gh issue view` resolves a PULL
# REQUEST at that number and exits 0 rather than failing. The url is what tells them apart.
case "$URL" in
  */issues/"$ISSUE") ;;
  *) say "#$ISSUE is not an issue in $REPO_SLUG (it resolves to $URL) — nothing to check."; exit 0 ;;
esac

# --- the labels -------------------------------------------------------------------
# A label is a whole line opening `**Scenario:` and closing `**`. Fenced and inline code never
# match, so an example of the convention does not declare a scenario.
#
# One checked stage at a time, not a pipeline: `grep` exits 1 for the legitimate "no labels", so
# `pipefail` would mask every other failure in the chain behind it as a silent pass.

printf '%s' "$RESPONSE" | jq -e 'has("body") and (.body | type == "string")' >/dev/null 2>&1 \
  || die "'gh issue view' returned no readable body for #$ISSUE, so its labels cannot be read. A response of that shape means the field changed or the reply was partial, not that the issue is unlabelled."

printf '%s' "$RESPONSE" | jq -r '.body' | tr -d '\r' > "$TMP/body" \
  || die "could not read the body of issue #$ISSUE."

# Dropped by their own delimiter, so a `~~~` block quoting ``` stays one block. A block still
# open at EOF would hide every label below it, so it fails rather than passing as unlabelled.
awk '
  match($0, /^[[:space:]]*(```+|~~~+)/) {
    marker = substr($0, RSTART, RLENGTH)
    gsub(/[[:space:]]/, "", marker)
    if (fence == "") { fence = marker; next }
    if (substr(marker, 1, 1) == substr(fence, 1, 1) && length(marker) >= length(fence)) fence = ""
    next
  }
  fence == "" { print }
  END { if (fence != "") exit 3 }
' "$TMP/body" > "$TMP/prose"
case $? in
  0) ;;
  3) die "issue #$ISSUE has an unterminated code fence, so any label below it cannot be read. Close the fence — an unreadable body is not an unlabelled one." ;;
  *) die "could not scan the body of issue #$ISSUE for code fences." ;;
esac

# `grep`'s exit 1 is the legitimate "no labels"; anything above it is the scan itself failing.
RAW_LABELS="$(grep -E '^[[:space:]]*\*\*Scenario:.+\*\*[[:space:]]*$' "$TMP/prose")"
case $? in
  0) ;;
  1) RAW_LABELS="" ;;
  *) die "could not scan the body of issue #$ISSUE for scenario labels." ;;
esac

if [ -z "$RAW_LABELS" ]; then
  say "issue #$ISSUE carries no '**Scenario:**' labels — nothing to check. Labels are optional (CONVENTIONS.md, Acceptance criteria)."
  exit 0
fi

# Whitespace around the delimiters goes; internal spacing is part of the key.
LABELS="$(printf '%s\n' "$RAW_LABELS" \
  | sed -E 's/^[[:space:]]*\*\*Scenario:[[:space:]]*//; s/[[:space:]]*\*\*[[:space:]]*$//')" \
  || die "could not normalise the scenario labels of issue #$ISSUE."

# --- the markers ------------------------------------------------------------------
# Committed test files only. `HEAD` rather than the working tree is what makes "committed" true,
# and docs are excluded because testing.md restates the rule verbatim.
#
# `:(glob)` stops `*` at a `/`, pinning `*Tests*` to an immediate child of src/; the second
# pathspec keeps git's default matching, so a `*.tests.sh` anywhere counts.
#
# `--no-line-number --no-column` override the user's own grep.lineNumber and grep.column, either
# of which would prepend digits the trim below cannot strip.
#
# The anchor keeps a mid-line mention from registering, but its reach is one line: a continuation
# line opening at column 0 with `//` or `#` does register, which is why the tests use an escape.
#
# Exit 1 is "no markers anywhere", a real state the verdict answers. Anything above it is the scan
# failing, which must not read as "no markers" and fail a marker that is sitting right there.
MARKER_LINES="$(git -C "$REPO_ROOT" grep -hE --no-line-number --no-column '^[[:space:]/#]*SCENARIO:' \
  HEAD -- ':(glob)src/*Tests*/**/*.cs' '*.tests.sh' 2>"$TMP/grep-err")"
SCAN_RC=$?

# The exit status alone is not enough: an unreadable tracked file makes `git grep` print to stderr
# and still exit 0, omitting that file's markers and failing a marker that is sitting right there.
[ -s "$TMP/grep-err" ] \
  && die "the scan of the committed test files reported an error, so the marker set is incomplete and no verdict is possible: $(tr '\n' ' ' < "$TMP/grep-err")"
case "$SCAN_RC" in
  0|1) ;;
  *) die "could not scan the committed test files for markers, so no verdict is possible." ;;
esac

MARKERS="$(printf '%s\n' "$MARKER_LINES" \
  | sed -E 's|^[[:space:]/#]*SCENARIO:[[:space:]]*||' \
  | trim)"

# --- the verdict ------------------------------------------------------------------
# Both verdicts carry the marker count: "0 markers scanned" alongside a list of unmatched labels
# reads nothing like "72 scanned".
scanned="$(printf '%s' "$MARKERS" | grep -c . || true)"

missing=""
total=0
while IFS= read -r label; do
  [ -n "$label" ] || continue
  total=$((total + 1))
  grep -qxF -- "$label" <<< "$MARKERS" || missing+="$label"$'\n'
done <<< "$LABELS"

if [ -n "$missing" ]; then
  {
    printf 'traceability-check: FAIL — issue #%s has labelled scenarios with no matching marker in a committed test file:\n' "$ISSUE"
    printf '%s' "$missing" | sed 's/^/  - /'
    printf '\n'
    printf "CONVENTIONS.md, Acceptance criteria: give each one a test carrying 'SCENARIO: <name>' in\n"
    printf 'the test file'"'"'s own comment syntax, matching the label character for character. If a label\n'
    printf 'names a scenario no test should verify — one an external tool or gate decides — remove the\n'
    printf 'label from the issue.\n'
    printf 'Scanned %s marker(s) in committed test files.\n' "$scanned"
    printf 'Issue: %s\n' "$URL"
  } >&2
  exit 1
fi

say "OK — all $total labelled scenario(s) in issue #$ISSUE have a matching test marker ($scanned marker(s) scanned)."
