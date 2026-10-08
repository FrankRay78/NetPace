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
# A bare `#332` reference does not count, and neither does a Development-sidebar link. The reasons
# are different and it is worth keeping them apart. A bare reference genuinely does not close
# anything on merge. A sidebar link DOES — but it lives outside the body, so it is invisible both to
# this check and to a reviewer reading the diff, and issue #332's confirmed decision excludes it on
# that ground. Do not "fix" this by trusting the sidebar: the exclusion is deliberate, not an
# oversight about what GitHub does.
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
# no issue to name and nobody writes its body. This check has no label-based exemption and no bypass
# actor of its own. Binding everyone including the repository owner additionally needs the live
# ruleset changed, and that is a post-merge step rather than something this branch ships — see the
# CIR for which two steps are outstanding.
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
# Injectable inputs, so a local run or a test can target any pull request: the body as
# $PR_ISSUE_LINK_BODY, the author as $PR_ISSUE_LINK_AUTHOR, the repository as $PR_ISSUE_LINK_REPO.
# The body and the author are one unit — supplying the author alone is a fail-closed error rather
# than an empty body — and passing a pull request number always re-fetches both from GitHub.
# Verify any edit with pr-issue-link-check.tests.sh.

set -uo pipefail

# The directory `gh` resolves the repository from — this check reads no repository file of its own.
# Named by CLAUDE_PROJECT_DIR as the hooks are, so a test matrix can keep `gh` off the real repo.
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

if [ -n "$PR_NUMBER" ]; then
  case "$PR_NUMBER" in
    *[!0-9]*) die "'$PR_NUMBER' is not a pull request number." ;;
  esac
fi

# The injected pair is all-or-nothing, and this is a fail-closed precondition rather than a default.
# An injected author with no injected body used to leave BODY empty and report "carries no closing
# keyword" — a verdict about a body that was never supplied, sending the author to fix something
# already correct. That is the one conflation the whole design rule above exists to prevent, and the
# guard belongs here because the `gh pr view` path below has its own and this path had none.
if [ -n "$AUTHOR" ] && [ -z "${PR_ISSUE_LINK_BODY+set}" ]; then
  die "PR_ISSUE_LINK_AUTHOR is set but PR_ISSUE_LINK_BODY is not, so no body was supplied to check. A body the check never received is not a body without a closing keyword."
fi
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

# GitHub renders code in FOUR shapes and links a closing reference in none of them, so all four go
# before matching. Dropping only some of them is the worst outcome available: the gate then reports
# green for a body whose reference GitHub never linked, and merging closes nothing.
#
# Block shapes go first, line by line, because they are decided by a line's own indentation and
# that is destroyed once spans collapse:
#
#   fenced      ``` or ~~~ , dropped by their own delimiter: only the delimiter that opened a block
#               may close it, so a `~~~` block quoting a ``` line stays one block, and a longer
#               opening fence is not closed by a shorter one. A block still open at EOF would hide
#               every reference below it, so that is a malformed body — it fails rather than
#               passing as unlinked.
#   indented    four spaces (or a tab, expanded to the same four-column stop). The list exception is
#               the whole difficulty: inside a list, that same indentation is list CONTENT, which
#               GitHub renders as prose and does link. So an indented line only counts as code when
#               no list is open and a blank line preceded it — which is CommonMark's own rule, and
#               the reason this cannot be a bare "drop anything indented by four".
awk '
  function expand(s,   out, i, c, col) {
    out = ""; col = 0
    for (i = 1; i <= length(s); i++) {
      c = substr(s, i, 1)
      if (c == "\t") { do { out = out " "; col++ } while (col % 4 != 0) }
      else { out = out c; col++ }
    }
    return out
  }
  {
    line = expand($0)

    if (match(line, /^[[:space:]]*(```+|~~~+)/)) {
      marker = substr(line, RSTART, RLENGTH)
      gsub(/[^`~]/, "", marker)
      if (fence == "") { fence = marker; next }
      if (substr(marker, 1, 1) == substr(fence, 1, 1) && length(marker) >= length(fence)) fence = ""
      next
    }
    if (fence != "") next

    if (line ~ /^[[:space:]]*$/) { blank = 1; print ""; next }

    indent = match(line, /[^ ]/) - 1
    if (indent == 0) inlist = 0
    if (line ~ /^ ? ? ?([-*+]|[0-9]+[.)])( |$)/) inlist = 1

    if (indent >= 4 && !inlist && (blank || incode)) { incode = 1; blank = 0; next }
    incode = 0; blank = 0
    print line
  }
  END { if (fence != "") exit 3 }
' "$TMP/body" > "$TMP/prose"
case $? in
  0) ;;
  3) die "this pull request body has an unterminated code fence, so any reference below it cannot be read. Close the fence — an unreadable body is not an unlinked one." ;;
  *) die "could not scan the pull request body for code fences." ;;
esac

# Then the span shapes, over the whole text at once because both may cross a line break:
#
#   code span   a run of N backticks closed by a run of exactly N, which is why a plain
#               `s/`[^`]*`//g` is not enough — against ``Closes #1`` it matches the two adjacent
#               backticks as an EMPTY span, deletes them, and leaves the reference behind.
#   HTML        <!-- ... -->, which GitHub does not render and therefore does not link. A pull
#               request template is mostly commented-out instructions, so this is the shape most
#               likely to quote the convention.
#
# Each is replaced by one \001 rather than deleted, so the text either side does not become
# adjacent: "Closes `x` #1" must not collapse into something matching "Closes #1".
awk '
  { text = text $0 "\n" }
  END {
    sent = sprintf("%c", 1)
    n = length(text); out = ""; i = 1
    while (i <= n) {
      if (substr(text, i, 1) != "`") { out = out substr(text, i, 1); i++; continue }
      j = i; while (j <= n && substr(text, j, 1) == "`") j++
      run = j - i
      k = j; found = 0
      while (k <= n) {
        if (substr(text, k, 1) == "`") {
          m = k; while (m <= n && substr(text, m, 1) == "`") m++
          if (m - k == run) { found = 1; break }
          k = m
        } else k++
      }
      if (found) { out = out sent; i = m; continue }
      out = out substr(text, i, run); i = j
    }
    while ((p = index(out, "<!--")) > 0) {
      rest = substr(out, p + 4)
      q = index(rest, "-->")
      if (q == 0) exit 4
      out = substr(out, 1, p - 1) sent substr(rest, q + 3)
    }
    printf "%s", out
  }
' "$TMP/prose" > "$TMP/plain"
case $? in
  0) ;;
  4) die "this pull request body has an unterminated HTML comment, so any reference after it cannot be read. Close the comment — an unreadable body is not an unlinked one." ;;
  *) die "could not strip code spans from the pull request body." ;;
esac

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

  # The trailing digits are the number in all three shapes, and they are kept EXACTLY as written.
  # Normalising them was the earlier mistake: `$((10#$NUM))` turned `#0401` into a lookup of 401 and
  # reported the body linked, when GitHub renders `#0401` as plain text and links nothing — and on a
  # very long run of digits the same arithmetic wrapped, naming an issue the body never mentioned.
  # A reference GitHub will not resolve is rejected under the number the author actually typed.
  NUM="$(printf '%s' "$candidate" | sed -E 's/^.*[^0-9]([0-9]+)$/\1/')" \
    || die "could not read the issue number out of a closing reference."

  QUAL=""
  if [[ "$candidate" =~ https?://github\.com/($SLUG_RE)/issues/[0-9]+$ ]]; then
    QUAL="${BASH_REMATCH[1]}"
  elif [[ "$candidate" =~ ($SLUG_RE)#[0-9]+$ ]]; then
    QUAL="${BASH_REMATCH[1]}"
  fi

  # $QUAL is empty for a bare reference, which leaves the label as the `#N` that was written.
  LABEL="$QUAL#$NUM"

  # One reference, one lookup and one line of report, however many times the body repeats it.
  case "$SEEN" in
    *"<$LABEL>"*) continue ;;
  esac
  SEEN="$SEEN<$LABEL>"

  # Shapes GitHub itself will not resolve, rejected before a lookup rather than normalised into one.
  case "$NUM" in
    0) REJECTED+="$LABEL — #0 is not an issue number"$'\n'; continue ;;
    0*) REJECTED+="$LABEL — written with a leading zero, which GitHub does not link"$'\n'; continue ;;
  esac
  if [ "${#NUM}" -gt 9 ]; then
    REJECTED+="$LABEL — too many digits to be an issue number"$'\n'
    continue
  fi

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

  # The url is checked for EXACT identity, not for an `/issues/` substring, because `gh issue view`
  # answers 0 in two ways that are not this repository's open issue at this number:
  #
  #   a pull request    GitHub numbers issues and pull requests from one sequence, so the lookup
  #                     resolves a PR at that number rather than failing. Closing a pull request is
  #                     not closing an issue.
  #   a transfer        an issue moved to another repository still resolves here, through GitHub's
  #                     redirect, and answers with its NEW home. A substring test sees `/issues/`,
  #                     reports the body linked, and prints another repository's url in its own
  #                     success line — while merging closes nothing here.
  #
  # Equality settles both at once. This is the same test `.claude/commands/raise-pr.md` already
  # applies for the same reason, so the two now agree about what a link is.
  if [ "${URL,,}" != "https://github.com/${REPO_SLUG,,}/issues/$NUM" ]; then
    case "$URL" in
      */pull/*) REJECTED+="$LABEL — #$NUM is a pull request, not an issue"$'\n' ;;
      *) REJECTED+="$LABEL — resolves to $URL, which is not an issue in $REPO_SLUG"$'\n' ;;
    esac
    continue
  fi

  # `gh` answers `OPEN` today, but comparing case-sensitively would reject an open issue with the
  # self-contradicting "issue is open, not open" if that ever changed.
  if [ "${STATE^^}" != OPEN ]; then
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
  printf "or any of 'close'/'fix'/'resolve' including their past tenses, in any case — where #<N> is an\n"
  printf "open issue in this repository, written at its exact number. A bare '#<N>' reference does not\n"
  printf 'count, nor does a Development-sidebar link, nor a reference inside a code block, a code span\n'
  printf 'or an HTML comment. Only dependabot[bot] is exempt; there is no label exemption.\n'
} >&2
exit 1
