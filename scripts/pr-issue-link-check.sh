#!/usr/bin/env bash
#
# pr-issue-link-check.sh — every pull request must close an open issue (issue #332).
#
# The confirmed decision defines "linked" narrowly, and this check enforces exactly that reading:
#
#     PR body   Closes #332
#        ↓  the issue must exist, be an ISSUE, be OPEN, and live in THIS repository
#     pass
#
# A bare `#332` reference does not count, and neither does a Development-sidebar link: the sidebar
# is not the body, and this check only ever reads the body. The reason is that the closing keyword
# is the only one of the three that makes merging the pull request close the issue — the outcome
# the gate exists to guarantee, rather than a decoration that looks like it.
#
# WHICH KEYWORDS COUNT. The decision names `Closes`/`Fixes`/`Resolves`; this check accepts all nine
# forms GitHub itself honours (close/closes/closed, fix/fixes/fixed, resolve/resolves/resolved), in
# any case. The decision's parenthetical names the canonical spellings, and the test it is really
# setting is "merging this will close an issue" — so GitHub's own grammar is the authority. Rejecting
# `Fixed #332`, which GitHub does link and close, would fail a correctly-linked pull request.
#
# WHAT IS DELIBERATELY NOT A LINK. GitHub does not create a closing link from inside a code block,
# so neither does this check: fenced blocks and inline-code spans are stripped before matching. That
# is not pedantry — this repo's own prompts and docs quote `Closes #<N>` constantly, so a pull
# request whose body *describes* the convention would otherwise satisfy the gate it is describing.
# A keyword inside a longer word ("Precloses") is not a keyword either, for the same reason.
#
# EXEMPTION — `dependabot[bot]` only, decided before the body is read, because a dependency bump has
# no issue to name and nobody writes its body. There is no label-based exemption and no bypass
# actor: the requirement binds everyone, the repository owner included.
#
# DESIGN RULE: fail CLOSED — the same rule as traceability-check.sh, and for the same reason. If it
# cannot reach GitHub, cannot resolve the repository, cannot read the body, or is missing `gh`/`jq`,
# it fails and says which. A gate that passes without having run gives exactly the false comfort it
# exists to remove. Note the distinction it draws: GitHub answering "nothing at that number" is an
# ANSWER (that reference does not link, and is reported as such), while auth, network and rate-limit
# failures are the check not running (fail, with different wording). The two must never be conflated
# — telling an author to add a keyword they already wrote sends them to fix something that is fine.
#
# Not a Claude Code hook — no .claude/settings.json entry. Two ways to run it:
#   In CI       .github/workflows/pr-issue-link.yml, on every pull request to main, including on
#               `edited`, so fixing the body re-runs the check without a new push.
#   Locally     bash scripts/pr-issue-link-check.sh [pr-number]
#               With nothing injected it resolves the pull request for the current branch.
#
# Every input is injectable, so a local run or a test can target any pull request: the body as
# $PR_ISSUE_LINK_BODY, the author as $PR_ISSUE_LINK_AUTHOR, the repository as $PR_ISSUE_LINK_REPO.
# Verify any edit with pr-issue-link-check.tests.sh.

set -uo pipefail

# Honours CLAUDE_PROJECT_DIR as the filesystem-reading hooks do, so a test matrix can point this at
# a sandbox tree by env var instead of relocating the script.
REPO_ROOT="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"

say() { printf 'pr-issue-link-check: %s\n' "$*"; }
die() { printf 'pr-issue-link-check: FAIL — %s\n' "$*" >&2; exit 1; }

# --- fail-closed preconditions ----------------------------------------------------
for tool in gh jq grep sed awk tr mktemp; do
  command -v "$tool" >/dev/null 2>&1 \
    || die "'$tool' is not available, so the check cannot run. It is a merge gate, so it fails rather than reporting an unverified pass."
done

TMP="$(mktemp -d)" || die "could not create a temporary directory."
trap 'rm -rf "$TMP"' EXIT

# --- the repository ---------------------------------------------------------------
# Passed in by the workflow rather than resolved with `gh repo view`: one fewer API call, and on a
# fork the value is the base repository, which is where the issue lives.
REPO_SLUG="${PR_ISSUE_LINK_REPO:-}"
if [ -z "$REPO_SLUG" ]; then
  REPO_SLUG="$(cd "$REPO_ROOT" && gh repo view --json nameWithOwner -q .nameWithOwner 2>"$TMP/err")" \
    || die "could not resolve this repository with 'gh repo view': $(tr '\n' ' ' < "$TMP/err")"
  [ -n "$REPO_SLUG" ] || die "'gh repo view' named no repository, so the body's references cannot be checked."
fi

# --- the pull request's body and author -------------------------------------------
# In CI both arrive from the event payload, so the body — untrusted content — is read from the
# environment and never interpolated into shell text. Locally neither is set and the pull request is
# resolved from the current branch, which is how a human asks "is my PR linked?" before review.
PR_NUMBER="${1:-}"
AUTHOR="${PR_ISSUE_LINK_AUTHOR:-}"
BODY="${PR_ISSUE_LINK_BODY-}"

if [ -n "$PR_NUMBER" ] || [ -z "$AUTHOR" ]; then
  if ! PR_RESPONSE="$(cd "$REPO_ROOT" && gh pr view ${PR_NUMBER:+"$PR_NUMBER"} --json body,author -R "$REPO_SLUG" 2>"$TMP/err")"; then
    die "could not resolve the pull request with 'gh pr view': $(tr '\n' ' ' < "$TMP/err")"
  fi
  # A reply the check cannot read is not an unlinked pull request. Checked field by field, because
  # an absent or null body would otherwise read as an empty one — i.e. a confident wrong verdict.
  printf '%s' "$PR_RESPONSE" | jq -e 'has("body") and (.body | type == "string")' >/dev/null 2>&1 \
    || die "'gh pr view' returned no readable body, so the pull request's references cannot be read. A reply of that shape means the field changed or the reply was partial, not that the body is empty."
  printf '%s' "$PR_RESPONSE" | jq -e '.author.login | type == "string"' >/dev/null 2>&1 \
    || die "'gh pr view' returned no readable author, so the dependabot exemption cannot be decided."
  BODY="$(printf '%s' "$PR_RESPONSE" | jq -r '.body')" || die "could not read the pull request body."
  AUTHOR="$(printf '%s' "$PR_RESPONSE" | jq -r '.author.login')" || die "could not read the pull request author."
fi

# --- the exemption ----------------------------------------------------------------
# Before the body is read: a dependency bump has no issue to name, so an unparseable bump body is
# still exempt rather than an outage.
if [ "$AUTHOR" = 'dependabot[bot]' ]; then
  say "dependabot[bot] is exempt — nothing to check."
  exit 0
fi

# --- the body, as GitHub reads it -------------------------------------------------
# CRLF first: GitHub serves CRLF for a body edited in the web UI, and a trailing \r would otherwise
# ride along into every reference the check extracts.
printf '%s' "$BODY" | tr -d '\r' > "$TMP/body" \
  || die "could not read the pull request body."

# Fenced blocks, dropped by their own delimiter: only the delimiter that opened a block may close
# it, so a `~~~` block quoting a ``` line stays one block. A block still open at EOF would hide
# every reference below it, so it is a malformed body, which fails rather than passing as unlinked.
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
  3) die "this pull request body has an unterminated code fence, so any reference below it cannot be read. Close the fence — an unreadable body is not an unlinked one." ;;
  *) die "could not scan the pull request body for code fences." ;;
esac

# Inline code spans go too, and for the same reason as fences: a line reading "the body must carry
# \`Closes #<N>\`" documents the rule rather than obeying it, and GitHub links neither.
sed -E 's/`[^`]*`//g' "$TMP/prose" > "$TMP/plain" \
  || die "could not strip inline code from the pull request body."

# --- the closing references -------------------------------------------------------
# Three reference shapes, because all three are things GitHub links: bare `#N`, cross-repository
# `owner/repo#N`, and a full issue URL. The last two are extracted rather than ignored so a
# reference to ANOTHER repository can be rejected by name — silently not matching it would report
# "no closing keyword" about a body that plainly has one.
#
# The keyword must stand on its own, hence the leading `(^|[^[:alnum:]_])`: without it "Precloses"
# contains a keyword GitHub does not honour. The separator accepts whitespace, or a colon with
# whitespace after it, but never nothing — `Closes#1` is not a link.
KEYWORD='(close[sd]?|fix(e[sd])?|resolve[sd]?)'
SLUG_RE='[A-Za-z0-9._-]+/[A-Za-z0-9._-]+'
REF="((${SLUG_RE})#|https?://github\\.com/(${SLUG_RE})/issues/|#)[0-9]+"

# `grep`'s exit 1 is the legitimate "this body names nothing"; anything above it is the scan itself
# failing, and that must not be read as "no references" — a locale abort or a missing tool would
# otherwise surface as a verdict about the author's body.
CANDIDATES="$(grep -oiE "(^|[^[:alnum:]_])${KEYWORD}([[:space:]]*:)?[[:space:]]+${REF}" "$TMP/plain")"
case $? in
  0) ;;
  1) CANDIDATES="" ;;
  *) die "could not scan the pull request body for closing keywords." ;;
esac

# --- the verdict ------------------------------------------------------------------
# Every rejected reference is reported with its own reason, not just the first: a body carrying two
# stale references should name both, so one round of edits fixes it.
REJECTED=""
SEEN=""

while IFS= read -r candidate; do
  [ -n "$candidate" ] || continue

  # The trailing digits are the number in all three shapes; `10#` drops any leading zeros so a
  # `#0401` never reports as a different reference from `#401`.
  NUM="$(printf '%s' "$candidate" | sed -E 's/^.*[^0-9]([0-9]+)$/\1/')" \
    || die "could not read the issue number out of a closing reference."
  NUM=$((10#$NUM))

  QUAL=""
  if [[ "$candidate" =~ https?://github\.com/($SLUG_RE)/issues/[0-9]+$ ]]; then
    QUAL="${BASH_REMATCH[1]}"
  elif [[ "$candidate" =~ ($SLUG_RE)#[0-9]+$ ]]; then
    QUAL="${BASH_REMATCH[1]}"
  fi

  if [ -n "$QUAL" ]; then LABEL="$QUAL#$NUM"; else LABEL="#$NUM"; fi

  # One reference, one lookup and one line of report, however many times the body repeats it.
  case "$SEEN" in
    *"<$LABEL>"*) continue ;;
  esac
  SEEN="$SEEN<$LABEL>"

  # GitHub slugs are case-insensitive, so `o/R#1` and `O/r#1` name the same repository.
  if [ -n "$QUAL" ] && [ "${QUAL,,}" != "${REPO_SLUG,,}" ]; then
    REJECTED+="$LABEL — names another repository, not $REPO_SLUG"$'\n'
    continue
  fi

  if ! RESPONSE="$(gh issue view "$NUM" --json url,state -R "$REPO_SLUG" 2>"$TMP/err")"; then
    ERR="$(tr '\n' ' ' < "$TMP/err")"
    # Read the stderr, not the exit code: both of these exit non-zero, and they are opposite
    # answers. "Could not resolve" is GitHub definitively saying nothing exists at that number — an
    # answer about this reference. Anything else is the check failing to run.
    if printf '%s' "$ERR" | grep -qi 'could not resolve to an issue or pull request'; then
      REJECTED+="$LABEL — no issue at that number in $REPO_SLUG"$'\n'
      continue
    fi
    die "could not retrieve issue #$NUM from $REPO_SLUG: $ERR"
  fi

  URL="$(printf '%s' "$RESPONSE" | jq -r '.url // empty' 2>/dev/null)"
  STATE="$(printf '%s' "$RESPONSE" | jq -r '.state // empty' 2>/dev/null)"
  { [ -n "$URL" ] && [ -n "$STATE" ]; } \
    || die "could not parse 'gh issue view' output for #$NUM."

  # GitHub numbers issues and pull requests from one sequence, so `gh issue view` resolves a PULL
  # REQUEST at that number and exits 0 rather than failing. The url is what tells them apart, and
  # closing a pull request is not closing an issue.
  case "$URL" in
    */issues/*) ;;
    *) REJECTED+="$LABEL — #$NUM is a pull request, not an issue"$'\n'; continue ;;
  esac

  if [ "$STATE" != OPEN ]; then
    REJECTED+="$LABEL — issue is $STATE, not open"$'\n'
    continue
  fi

  say "OK — this pull request closes issue #$NUM ($URL)."
  exit 0
done <<< "$CANDIDATES"

{
  printf 'pr-issue-link-check: FAIL — this pull request body carries no closing keyword naming an open issue in %s.\n' "$REPO_SLUG"
  if [ -n "$REJECTED" ]; then
    printf 'Closing references were found, and none of them links:\n'
    printf '%s' "$REJECTED" | sed 's/^/  - /'
  fi
  printf '\n'
  printf "Issue #332: add a line to the body naming the issue this pull request closes — 'Closes #<N>',\n"
  printf "or 'Fixes'/'Resolves' — where #<N> is an open issue in this repository. A bare '#<N>'\n"
  printf 'reference does not count, nor does a Development-sidebar link, nor a reference inside a code\n'
  printf 'block. Only dependabot[bot] is exempt; there is no label exemption and no bypass actor.\n'
} >&2
exit 1
