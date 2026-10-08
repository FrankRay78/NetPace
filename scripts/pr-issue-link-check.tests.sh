#!/usr/bin/env bash
#
# pr-issue-link-check.tests.sh — case matrix for scripts/pr-issue-link-check.sh (issue #332).
#
# The check reads three things it cannot be handed on stdin: the pull request's body and author
# (injected by the workflow, or fetched with `gh` on a local run) and the state of each issue the
# body names (over the network, via `gh`). Every case therefore stubs `gh` on PATH and points the
# real, unmodified check at that stub. No case touches the network or this repo's own files.
#
# TWO THINGS THIS FILE IS CAREFUL ABOUT:
#
#   1. Passing is not one outcome, it is two. "dependabot is exempt" and "checked and linked" both
#      exit 0, so a check stubbed to `exit 0` on line one would satisfy a bare `rc == 0`. Every
#      passing case therefore asserts on WHICH verdict was reached, not just the code.
#   2. Failing is not one outcome either. "no closing keyword" and "the check could not run" are
#      opposite answers, and the second must never be reported as the first — a merge gate that
#      turns an outage into "add a closing keyword" sends the author to fix something that is not
#      broken. The fail-closed cases assert the wording, not just the code.
#
# Fixture issue numbers are in the 400s and the fixture repository is `o/r`, so no case can be
# confused with a real NetPace issue.
#
# Exits non-zero on any failure. Run after any edit to the check.
#
#   Usage:  scripts/pr-issue-link-check.tests.sh

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CHECK="$HERE/pr-issue-link-check.sh"
BASH_BIN="$(command -v bash)"

# One parent-scoped root holds every sandbox, so cleanup cannot be defeated by a subshell.
ROOT="$(mktemp -d)"
cleanup() { chmod -R u+rwX "$ROOT" 2>/dev/null; rm -rf "$ROOT"; }
trap cleanup EXIT

pass=0; fail=0; cases=0
ok() { if eval "$2"; then echo "  ok   $1"; pass=$((pass+1)); else echo "  FAIL $1 -- rc=$RC out:[$(printf '%s' "$OUTPUT" | head -3 | tr '\n' '/')]"; fail=$((fail+1)); fi; }

# A broken fixture is not a test result. Every setup step is checked through this, because an
# unchecked one is worse than useless: with the `gh` stub missing, every case asserting
# `failed && names "…"` could still pass while verifying nothing about the discrimination it claims.
setup_fail() { echo "  FIXTURE SETUP FAILED (case $cases): $*" >&2; exit 1; }

# new_case — a fresh stub dir and a `gh` stub earlier on PATH than the real one. The stub exits
# non-zero with GitHub's own "could not resolve" wording for an unknown number, so the check's
# answer/outage distinction is exercised against the real string rather than a paraphrase.
new_case() {
  cases=$((cases+1))
  SB="$ROOT/case$cases"
  STUB_DIR="$SB/stub"
  WORK="$SB/work"
  # Reset the verdict globals. They are what `ok` reads, so a stale value would let a lost or
  # mis-edited `run` line make the next assertion silently re-assert the PREVIOUS case's result.
  RC=99
  OUTPUT=""
  mkdir -p "$STUB_DIR" "$SB/bin" "$WORK" || setup_fail "mkdir"
  cat > "$SB/bin/gh" <<'STUB'
#!/usr/bin/env bash
case "$1" in
  repo)
    if [ -f "$STUB_DIR/repo-unreachable" ]; then cat "$STUB_DIR/repo-unreachable" >&2; exit 1; fi
    cat "$STUB_DIR/repo"; exit 0 ;;
  issue)
    n="$3"
    if [ -f "$STUB_DIR/issue-$n.json" ]; then cat "$STUB_DIR/issue-$n.json"; exit 0; fi
    if [ -f "$STUB_DIR/issue-unreachable-$n" ]; then cat "$STUB_DIR/issue-unreachable-$n" >&2; exit 1; fi
    echo "GraphQL: Could not resolve to an issue or pull request with the number of $n. (repository.issue)" >&2
    exit 1 ;;
  pr)
    if [ -f "$STUB_DIR/pr-unreachable" ]; then cat "$STUB_DIR/pr-unreachable" >&2; exit 1; fi
    if [ -f "$STUB_DIR/pr.json" ]; then cat "$STUB_DIR/pr.json"; exit 0; fi
    echo "no pull requests found for branch" >&2; exit 1 ;;
esac
echo "gh: unexpected stub invocation: $*" >&2
exit 1
STUB
  chmod +x "$SB/bin/gh" || setup_fail "chmod gh stub"
  printf 'o/r\n' > "$STUB_DIR/repo" || setup_fail "write repo stub"
}

# issue N STATE — the stub's reply for issue N.
issue() { jq -n --arg s "$2" --arg u "https://github.com/o/r/issues/$1" '{url:$u,state:$s}' > "$STUB_DIR/issue-$1.json" || setup_fail "issue $1"; }
# pull N — the stub's reply when N is a pull request. GitHub numbers issues and pull requests from
# one sequence, so `gh issue view` resolves a pull request and exits 0; the url tells them apart.
pull() { jq -n --arg u "https://github.com/o/r/pull/$1" '{url:$u,state:"OPEN"}' > "$STUB_DIR/issue-$1.json" || setup_fail "pull $1"; }
# raw N JSON — the stub's reply for N, verbatim, for malformed-response cases.
raw() { printf '%s' "$2" > "$STUB_DIR/issue-$1.json" || setup_fail "raw $1"; }
# issue_unreachable N MESSAGE — the stub cannot answer for N (auth, network, rate limit).
issue_unreachable() { printf '%s\n' "$2" > "$STUB_DIR/issue-unreachable-$1" || setup_fail "unreachable $1"; }
# pr BODY AUTHOR — the stub's reply for the pull request the check resolves on a local run.
pr() { jq -n --arg b "$1" --arg a "$2" '{body:$b,author:{login:$a}}' > "$STUB_DIR/pr.json" || setup_fail "pr"; }
pr_raw() { printf '%s' "$1" > "$STUB_DIR/pr.json" || setup_fail "pr raw"; }
pr_unreachable() { printf '%s\n' "$1" > "$STUB_DIR/pr-unreachable" || setup_fail "pr unreachable"; }
repo_slug() { printf '%s\n' "$1" > "$STUB_DIR/repo" || setup_fail "repo slug"; }
repo_unreachable() { printf '%s\n' "$1" > "$STUB_DIR/repo-unreachable" || setup_fail "repo unreachable"; }

# run BODY AUTHOR — the CI path: body, author and repository injected as the workflow injects them.
run() {
  OUTPUT="$(env STUB_DIR="$STUB_DIR" PATH="$SB/bin:$PATH" CLAUDE_PROJECT_DIR="$WORK" \
    PR_ISSUE_LINK_REPO=o/r PR_ISSUE_LINK_BODY="$1" PR_ISSUE_LINK_AUTHOR="$2" \
    bash "$CHECK" 2>&1)"; RC=$?
}

# run_unqualified BODY AUTHOR — no repository injected, so the check resolves it with `gh repo view`.
run_unqualified() {
  OUTPUT="$(env STUB_DIR="$STUB_DIR" PATH="$SB/bin:$PATH" CLAUDE_PROJECT_DIR="$WORK" \
    PR_ISSUE_LINK_BODY="$1" PR_ISSUE_LINK_AUTHOR="$2" \
    bash "$CHECK" 2>&1)"; RC=$?
}

# run_local [ARG…] — the local path: no body or author injected, so the check resolves the pull
# request itself. This is how a human asks "is my PR linked?" before raising it for review.
run_local() {
  OUTPUT="$(env STUB_DIR="$STUB_DIR" PATH="$SB/bin:$PATH" CLAUDE_PROJECT_DIR="$WORK" \
    PR_ISSUE_LINK_REPO=o/r bash "$CHECK" "$@" 2>&1)"; RC=$?
}

passed() { [ "$RC" = 0 ]; }
failed() { [ "$RC" = 1 ]; }
# A pass must say WHICH pass it is: "exempt" and "checked and linked" are different answers.
linked() { passed && printf '%s' "$OUTPUT" | grep -qF 'OK'; }
exempt() { passed && printf '%s' "$OUTPUT" | grep -qF 'exempt'; }
names()  { printf '%s' "$OUTPUT" | grep -qF -- "$1"; }

echo "pr-issue-link-check.tests.sh"
echo ""

echo "A closing keyword naming an open issue in this repository is a link:"
new_case
issue 401 OPEN
run 'Closes #401' FrankRay78
ok "a body closing an open issue passes"              'linked'
ok "the verdict names the issue it linked to"         'names "#401"'
new_case
issue 401 OPEN
run $'## Summary\n\nTightens the latency probe.\n\nCloses #401\n\nCo-Authored-By: someone' FrankRay78
ok "the keyword is found anywhere in a real body"     'linked'
new_case
issue 401 OPEN
run 'This change closes #401 and nothing else.' FrankRay78
ok "a keyword mid-sentence counts"                   'linked'
new_case
issue 401 OPEN
run 'Closes: #401' FrankRay78
ok "a colon after the keyword counts"                'linked'
new_case
issue 401 OPEN
run 'Closes   #401' FrankRay78
ok "extra whitespace before the reference counts"    'linked'

echo ""
echo "Every closing keyword GitHub honours counts, in any case:"
new_case
issue 401 OPEN
run 'Fixes #401' FrankRay78
ok "Fixes"                                           'linked'
new_case
issue 401 OPEN
run 'Resolves #401' FrankRay78
ok "Resolves"                                        'linked'
new_case
issue 401 OPEN
run 'close #401' FrankRay78
ok "close"                                           'linked'
new_case
issue 401 OPEN
run 'closed #401' FrankRay78
ok "closed"                                          'linked'
new_case
issue 401 OPEN
run 'fix #401' FrankRay78
ok "fix"                                             'linked'
new_case
issue 401 OPEN
run 'FIXED #401' FrankRay78
ok "FIXED"                                           'linked'
new_case
issue 401 OPEN
run 'resolve #401' FrankRay78
ok "resolve"                                         'linked'
new_case
issue 401 OPEN
run 'ReSoLvEd #401' FrankRay78
ok "ReSoLvEd"                                        'linked'

echo ""
echo "A reference GitHub would not honour is not a link:"
new_case
issue 401 OPEN
run 'Refs #401' FrankRay78
ok "a bare Refs reference fails"                     'failed && names "no closing keyword"'
new_case
issue 401 OPEN
run 'See #401 for background.' FrankRay78
ok "a bare #N reference fails"                       'failed && names "no closing keyword"'
new_case
issue 401 OPEN
run '' FrankRay78
ok "an empty body fails"                             'failed && names "no closing keyword"'
new_case
issue 401 OPEN
# The link is only in the Development sidebar, which is not the body. The confirmed decision is
# explicit that a sidebar link alone does not count, and the check only ever reads the body.
run 'No issue mentioned anywhere in this body.' FrankRay78
ok "a body naming no issue at all fails"             'failed && names "no closing keyword"'
new_case
issue 401 OPEN
# The keyword must stand on its own. "Precloses" contains "closes" but GitHub does not link it.
run 'Precloses #401' FrankRay78
ok "a keyword inside a longer word fails"            'failed && names "no closing keyword"'
new_case
issue 401 OPEN
run '#401 closes the last gap.' FrankRay78
ok "a keyword AFTER the reference fails"             'failed && names "no closing keyword"'
new_case
issue 401 OPEN
# GitHub does not link a reference inside a code block, and this repo's own prompts quote the
# convention constantly — a body describing it must not satisfy the gate by accident.
run $'Write the body like this:\n\n```\nCloses #401\n```\n' FrankRay78
ok "a reference inside a fenced block fails"         'failed && names "no closing keyword"'
new_case
issue 401 OPEN
run 'The body must carry `Closes #401` before merge.' FrankRay78
ok "a reference inside inline code fails"            'failed && names "no closing keyword"'
new_case
issue 401 OPEN
run $'```\nCloses #401\n```\n\nCloses #401\n' FrankRay78
ok "a fenced example beside a real link still passes" 'linked'

echo ""
echo "The number must be an open issue in THIS repository:"
new_case
run 'Closes #499' FrankRay78
ok "a number with no issue behind it fails"          'failed && names "#499"'
ok "…and says the issue does not exist"              'names "no issue at that number"'
new_case
issue 402 CLOSED
run 'Closes #402' FrankRay78
ok "a closed issue fails"                            'failed && names "#402"'
ok "…and says it is closed"                          'names "CLOSED"'
new_case
pull 403
run 'Closes #403' FrankRay78
ok "a number that is a pull request fails"           'failed && names "pull request, not an issue"'
new_case
issue 401 OPEN
run 'Closes other/elsewhere#401' FrankRay78
ok "a cross-repository reference fails"              'failed && names "another repository"'
new_case
issue 401 OPEN
run 'Closes https://github.com/other/elsewhere/issues/401' FrankRay78
ok "a cross-repository URL fails"                    'failed && names "another repository"'
new_case
issue 401 OPEN
run 'Closes o/r#401' FrankRay78
ok "an owner/repo reference to THIS repo passes"     'linked'
new_case
issue 401 OPEN
run 'Closes https://github.com/o/r/issues/401' FrankRay78
ok "a full URL to THIS repo passes"                  'linked'
new_case
repo_slug 'O/R'
issue 401 OPEN
run_unqualified 'Closes o/r#401' FrankRay78
ok "the repository slug is matched case-insensitively" 'linked'
new_case
issue 404 CLOSED
issue 401 OPEN
run 'Closes #404, and Closes #401' FrankRay78
ok "one valid link among several is enough"          'linked'
new_case
issue 404 CLOSED
issue 405 CLOSED
run 'Closes #404 and Fixes #405' FrankRay78
ok "every rejected reference is reported, not just the first" 'failed && names "#404" && names "#405"'

echo ""
echo "dependabot[bot] is the only exempt author:"
new_case
run 'Bumps actions/checkout from 4 to 5.' 'dependabot[bot]'
ok "dependabot with no closing keyword passes"       'exempt'
ok "…and says which author it exempted"              'names "dependabot[bot]"'
new_case
# The exemption is decided before the body is read, so a dependabot body the check could not parse
# is still exempt rather than an outage.
run $'```\nunterminated fence\n' 'dependabot[bot]'
ok "dependabot is exempt before the body is parsed"  'exempt'
new_case
run 'Bumps something.' 'renovate[bot]'
ok "another bot is NOT exempt"                       'failed && names "no closing keyword"'
new_case
run 'Bumps something.' 'dependabot'
ok "dependabot without the [bot] suffix is NOT exempt" 'failed && names "no closing keyword"'
new_case
run 'Routine maintenance.' FrankRay78
ok "the repository owner is NOT exempt"              'failed && names "no closing keyword"'

echo ""
echo "The local path resolves the pull request itself:"
new_case
issue 401 OPEN
pr 'Closes #401' FrankRay78
run_local
ok "a linked PR on the current branch passes"        'linked'
new_case
issue 401 OPEN
pr 'Closes #401' FrankRay78
run_local 77
ok "a PR named by number passes"                     'linked'
new_case
pr 'Refs #401' FrankRay78
run_local
ok "an unlinked PR on the current branch fails"      'failed && names "no closing keyword"'
new_case
pr 'Bumps actions/checkout from 4 to 5.' 'dependabot[bot]'
run_local
ok "the exemption applies on the local path too"     'exempt'

echo ""
echo "Fail-closed guards — a gate that cannot run must not report green:"
new_case
issue 401 OPEN
# PATH is emptied so the check cannot find `gh`. The interpreter is named by absolute path, since
# an emptied PATH would otherwise stop `bash` itself resolving and report 127 — the check never
# running, read as the check's own verdict. Asserting rc=1 rather than non-zero is what pins that.
mkdir -p "$SB/empty" || setup_fail "mkdir empty"
OUTPUT="$(env STUB_DIR="$STUB_DIR" PATH="$SB/empty" PR_ISSUE_LINK_REPO=o/r \
  PR_ISSUE_LINK_BODY='Closes #401' PR_ISSUE_LINK_AUTHOR=FrankRay78 "$BASH_BIN" "$CHECK" 2>&1)"; RC=$?
ok "missing tooling fails closed"                    'failed'
new_case
issue 401 OPEN
# A single missing tool, rather than an emptied PATH. The emptied-PATH case above hides `gh` first,
# so it never reaches the parsing stages — where a missing `tr` must not report a linked body as
# carrying no closing keyword.
mkdir -p "$SB/notr" || setup_fail "mkdir notr"
for t in gh jq grep sed awk mktemp rm cat dirname env printf chmod; do
  p="$(command -v "$t")" && ln -sf "$p" "$SB/notr/$t"
done
ln -sf "$SB/bin/gh" "$SB/notr/gh" || setup_fail "link gh stub"
OUTPUT="$(env STUB_DIR="$STUB_DIR" PATH="$SB/notr" PR_ISSUE_LINK_REPO=o/r \
  PR_ISSUE_LINK_BODY='Closes #401' PR_ISSUE_LINK_AUTHOR=FrankRay78 "$BASH_BIN" "$CHECK" 2>&1)"; RC=$?
ok "one missing tool fails closed, not quietly"      'failed && names "is not available"'
new_case
issue_unreachable 401 'gh: HTTP 401: Bad credentials'
run 'Closes #401' FrankRay78
ok "an auth failure is an outage, not a missing link" 'failed && names "could not retrieve issue #401" && ! names "no closing keyword"'
new_case
issue_unreachable 401 'error connecting to api.github.com'
run 'Closes #401' FrankRay78
ok "a network failure is an outage too"              'failed && names "could not retrieve issue #401"'
new_case
raw 401 '{"state":"OPEN"}'
run 'Closes #401' FrankRay78
ok "a reply with no url field fails closed"          'failed && names "could not parse"'
new_case
raw 401 '{"url":"https://github.com/o/r/issues/401"}'
run 'Closes #401' FrankRay78
ok "a reply with no state field fails closed"        'failed && names "could not parse"'
new_case
raw 401 'not json at all'
run 'Closes #401' FrankRay78
ok "an unparseable issue reply fails closed"         'failed && names "could not parse"'
new_case
issue 401 OPEN
# A body the check cannot read is not a body carrying no link: an unterminated fence hides every
# line below it, so telling the author to add a keyword they already wrote is the wrong verdict.
run $'```\nCloses #401\n' FrankRay78
ok "an unterminated code fence fails closed"         'failed && names "unterminated code fence" && ! names "no closing keyword"'
new_case
repo_unreachable 'gh: could not determine the current repository'
run_unqualified 'Closes #401' FrankRay78
ok "an unresolvable repository fails closed"         'failed && names "could not resolve this repository"'
new_case
pr_unreachable 'gh: HTTP 401: Bad credentials'
run_local
ok "an unresolvable pull request fails closed"       'failed && names "could not resolve the pull request"'
new_case
pr_raw '{"author":{"login":"FrankRay78"}}'
run_local
ok "a PR reply with no body field fails closed"      'failed && names "no readable body"'
new_case
pr_raw '{"body":"Closes #401"}'
run_local
ok "a PR reply with no author fails closed"          'failed && names "no readable author"'

echo ""
echo "Carriage returns — GitHub serves CRLF for a body edited in the web UI:"
new_case
issue 401 OPEN
run $'## Summary\r\n\r\nCloses #401\r' FrankRay78
ok "a CRLF body still matches its keyword"           'linked'
new_case
issue 401 OPEN
run $'```\r\nCloses #401\r\n```\r' FrankRay78
ok "…and a CRLF fence still hides what it fences"    'failed && names "no closing keyword"'

echo ""
echo "----------------------------------------"
echo "cases: $cases   passed: $pass   failed: $fail"
[ "$fail" -eq 0 ] || exit 1
