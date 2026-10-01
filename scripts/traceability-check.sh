#!/usr/bin/env bash
#
# traceability-check.sh — Constitution §VIII enforcement (issue #319).
#
# §VIII makes a scenario LABEL on a GitHub issue the traceability key that links an acceptance
# criterion to the test that verifies it:
#
#     issue   **Scenario: Server list is screened before measuring**
#        ↓  exact match, one way
#     test    // SCENARIO: Server list is screened before measuring
#
# This check enforces that one edge for ONE issue: the one the current branch implements. Every
# `**Scenario: X**` label in that issue must have at least one matching marker in a committed
# test file. Nothing historical is judged — the ~50 markers left by already-merged issues name
# labels this issue does not carry, and are simply not looked at.
#
# The direction matters and is deliberate. Label → marker only: a marker no label names is not a
# violation here, because a scenario can legitimately be covered by a test that already existed,
# and because the reverse direction would false-fail on every clean branch. "A marker must not be
# invented" is the other half of §VIII and stays a judgement call for review.
#
# SCOPE — one issue, read off the branch. The `<prefix>/<N>-<slug>` shape `/build` produces gives
# the number, exactly as /raise-pr and /study already parse it. A branch carrying no number has
# nothing to check and passes: a pull request raised outside the issue flow is valid.
#
# DESIGN RULE: fail CLOSED — the opposite of the .claude/hooks/ convention, and deliberately so.
# Those are advisory nudges inside an interactive turn, where a false block can lock out the tools
# that would fix it. This is a merge gate: if it cannot fetch the issue, cannot find the repo, or
# is missing `gh`/`jq`, it fails and says why. A gate that passes without having run gives exactly
# the false comfort it exists to remove. Note the distinction it draws around a missing issue —
# GitHub answering "nothing at that number" is an ANSWER (pass, nothing to check); auth, network
# and rate-limit failures are the check not running (fail).
#
# Not a Claude Code hook — no .claude/settings.json entry. Two ways to run it:
#   In CI       .github/workflows/traceability.yml, on every pull request to main. The single
#               point of enforcement, so it sees the branch's final state however it got there.
#   Locally     bash scripts/traceability-check.sh [branch]
#               Run it before raising a PR (CLAUDE.md), so a dropped marker is caught first.
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
for tool in git gh jq grep sed awk; do
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

# Only the `<prefix>/<N>-<slug>` shape, and only the LEADING number of the segment after the
# prefix. Anchoring the digits to the start of that segment is what keeps `feature/net10-upgrade`
# from yielding 10 — the same restriction /raise-pr and /study apply, for the same reason: a
# number guessed from elsewhere in the name checks the wrong issue with full confidence.
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
  # Read the stderr, not the exit code: both of these exit non-zero, and they are opposite
  # answers. "Could not resolve" is GitHub definitively saying nothing exists at that number —
  # an answer, so there is nothing to trace. Anything else is the check failing to run.
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
# A label is a whole line that opens with `**Scenario:` and closes with `**`. Fenced blocks are
# dropped first and inline code never matches the line anchor, so neither counts: §VIII itself
# carries a fenced example, and this issue's own body quotes the syntax in backticks — both
# describe the convention rather than declaring a scenario.
LABELS="$(printf '%s\n' "$(printf '%s' "$RESPONSE" | jq -r '.body // empty')" \
  | tr -d '\r' \
  | awk '/^[[:space:]]*(```|~~~)/ { fenced = !fenced; next } !fenced' \
  | trim \
  | grep -E '^\*\*Scenario:.+\*\*$' \
  | sed -E 's/^\*\*Scenario:[[:space:]]*//; s/\*\*$//' \
  | trim)"

if [ -z "$LABELS" ]; then
  say "issue #$ISSUE carries no '**Scenario:**' labels — nothing to check. Labels are optional under §VIII."
  exit 0
fi

# --- the markers ------------------------------------------------------------------
# Committed test files only, in two shapes: any `*.tests.sh`, and `*.cs` inside a `*Tests*`
# project under src/. `git ls-files` is what makes "committed" true — an untracked scratch file
# must not satisfy a label, because it is not what merges. Production code and docs are excluded
# for the same reason: docs/conventions/testing.md restates the §VIII rule verbatim, so a
# docs-wide scan would let the rule's own description satisfy it.
test_files() {
  git -C "$REPO_ROOT" ls-files -z -- 'src' '*.tests.sh' 2>/dev/null \
    | tr '\0' '\n' \
    | grep -E '\.tests\.sh$|^src/[^/]*Tests[^/]*/.+\.cs$'
}

# The text after `SCENARIO:`, trimmed. The prefix class accepts all three comment shapes in use
# (`// `, `# // `, `# `) without enumerating them, and the anchor is what keeps a mention inside
# a string literal or an `ok "…"` assertion from registering as a marker.
MARKERS=""
FILES="$(test_files)"
if [ -n "$FILES" ]; then
  MARKERS="$(printf '%s\n' "$FILES" \
    | sed "s|^|$REPO_ROOT/|" \
    | tr '\n' '\0' \
    | xargs -0 grep -hE '^[[:space:]/#]*SCENARIO:' 2>/dev/null \
    | sed -E 's|^[[:space:]/#]*SCENARIO:[[:space:]]*||' \
    | trim)"
fi

# --- the verdict ------------------------------------------------------------------
missing=""
total=0
while IFS= read -r label; do
  [ -n "$label" ] || continue
  total=$((total + 1))
  printf '%s\n' "$MARKERS" | grep -qxF -- "$label" || missing+="$label"$'\n'
done <<< "$LABELS"

if [ -n "$missing" ]; then
  {
    printf 'traceability-check: FAIL — issue #%s has labelled scenarios with no matching marker in a committed test file:\n' "$ISSUE"
    printf '%s' "$missing" | sed 's/^/  - /'
    printf '\n'
    printf "Constitution §VIII: give each one a test carrying 'SCENARIO: <name>' in the test file's own\n"
    printf 'comment syntax, matching the label character for character. If a label names a scenario no\n'
    printf 'test should verify — one an external tool or gate decides — remove the label from the issue.\n'
    printf 'Issue: %s\n' "$URL"
  } >&2
  exit 1
fi

say "OK — all $total labelled scenario(s) in issue #$ISSUE have a matching test marker."
