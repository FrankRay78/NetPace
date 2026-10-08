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
# Every invocation is recorded so a case can assert on the ARGUMENTS, not just the reply. Without
# this, dropping `-R "$REPO_SLUG"` or the pull request number from the real check leaves the whole
# matrix green: the stub answers the same either way, so the targeting that matters on a fork is
# exactly what no assertion sees.
printf '%s\n' "$*" >> "$STUB_DIR/argv"
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
# issue_elsewhere N SLUG — N was transferred out of o/r. `gh issue view` follows GitHub's redirect
# and answers with the issue's NEW home, exit 0, state OPEN, so only the url reveals that merging
# here closes nothing.
issue_elsewhere() { jq -n --arg u "https://github.com/$2/issues/55" '{url:$u,state:"OPEN"}' > "$STUB_DIR/issue-$1.json" || setup_fail "issue_elsewhere $1"; }
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

# run_on_path DIR BODY AUTHOR — as `run`, but with PATH replaced by DIR, for the cases where a tool
# the check needs is absent. The interpreter is named by absolute path, since a PATH without `bash`
# on it would otherwise stop `bash` itself resolving and report 127 — the check never running, read
# as the check's own verdict.
run_on_path() {
  OUTPUT="$(env STUB_DIR="$STUB_DIR" PATH="$1" CLAUDE_PROJECT_DIR="$WORK" PR_ISSUE_LINK_REPO=o/r \
    PR_ISSUE_LINK_BODY="$2" PR_ISSUE_LINK_AUTHOR="$3" "$BASH_BIN" "$CHECK" 2>&1)"; RC=$?
}

passed() { [ "$RC" = 0 ]; }
failed() { [ "$RC" = 1 ]; }
# A pass must say WHICH pass it is: "exempt" and "checked and linked" are different answers.
linked() { passed && printf '%s' "$OUTPUT" | grep -qF 'OK'; }
exempt() { passed && printf '%s' "$OUTPUT" | grep -qF 'exempt'; }
names()  { printf '%s' "$OUTPUT" | grep -qF -- "$1"; }
# asked ARGS — some `gh` invocation's argument string contained this substring. A superset of flags
# satisfies it, so it pins that an argument was PASSED, not that the argument list was exactly this.
asked()  { grep -qF -- "$1" "$STUB_DIR/argv"; }
# never_asked — no `gh` call was made at all, which is what a reference rejected on its own shape
# (a leading zero, too many digits, another repository) must cost.
never_asked() { ! [ -s "$STUB_DIR/argv" ]; }

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
# The separator accepts whitespace, or a colon with whitespace after it, but never nothing.
run 'Closes#401' FrankRay78
ok "a keyword with no separator fails"               'failed && names "no closing keyword"'
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
# A code span is a run of N backticks closed by a run of exactly N. A single-backtick matcher reads
# the two adjacent backticks here as an EMPTY span, deletes them, and leaves the reference exposed.
run 'The body must carry ``Closes #401`` before merge.' FrankRay78
ok "a reference inside a double-backtick span fails"  'failed && names "no closing keyword"'
new_case
issue 401 OPEN
run 'Write ``````Closes #401`````` in the body.' FrankRay78
ok "…and inside a six-backtick span"                 'failed && names "no closing keyword"'
new_case
issue 401 OPEN
# An unmatched run is not a span, so it must not swallow the rest of the body.
run 'A stray ` backtick, then Closes #401' FrankRay78
ok "an unmatched backtick does not hide a real link" 'linked'
new_case
issue 401 OPEN
# Four spaces is an indented code block in GFM, and GitHub links nothing inside one.
run $'Write the body like this:\n\n    Closes #401\n' FrankRay78
ok "a reference in an indented code block fails"     'failed && names "no closing keyword"'
new_case
issue 401 OPEN
run $'Write the body like this:\n\n\tCloses #401\n' FrankRay78
ok "…and in a tab-indented one"                      'failed && names "no closing keyword"'
new_case
issue 401 OPEN
# The list exception, and the reason the rule above cannot be "drop anything indented by four":
# inside a list that indentation is CONTENT, which GitHub renders as prose and does link.
run $'- the first change\n\n    Closes #401\n' FrankRay78
ok "an indented line inside a list still links"      'linked'
new_case
issue 401 OPEN
run $'- outer\n    - Closes #401\n' FrankRay78
ok "…and so does a nested bullet"                    'linked'
new_case
issue 401 OPEN
# Indented code is measured four columns past the list item's CONTENT column, not past column 0.
# For "- item" that column is 2, so four and five spaces are content and six is code. Every
# expectation in this block was taken from GitHub's own renderer, not from reading the spec.
run $'- item\n\n     Closes #401\n' FrankRay78
ok "five spaces in a list is still content"          'linked'
new_case
issue 401 OPEN
run $'- item\n\n      Closes #401\n' FrankRay78
ok "six spaces in a list is code"                    'failed && names "no closing keyword"'
new_case
issue 401 OPEN
run $'- item\n\n        Closes #401\n' FrankRay78
ok "…and so is eight"                                'failed && names "no closing keyword"'
new_case
issue 401 OPEN
run $'1. item\n\n   Closes #401\n' FrankRay78
ok "an ordered list continuation links"              'linked'
new_case
issue 401 OPEN
# The tab stop is four columns, and inside a list that is what decides the verdict: one tab reaches
# column 4, short of this item's code threshold of 6, so GitHub renders it as content and links it.
# An eight-column stop would make the same line code — which is why the plain tab cases above, where
# any stop clears the bar, cannot pin the width on their own.
run $'- item\n\n\tCloses #401\n' FrankRay78
ok "one tab in a list is content, not code"          'linked'
new_case
issue 401 OPEN
run $'- item\n\n\t\tCloses #401\n' FrankRay78
ok "…and two tabs is code"                           'failed && names "no closing keyword"'
new_case
issue 401 OPEN
# Only `1.` may interrupt a paragraph, so this "2." is prose and does not raise the code threshold.
run $'text\n2. not a list\n\n    Closes #401\n' FrankRay78
ok "a 2. line mid-paragraph does not open a list"    'failed && names "no closing keyword"'
new_case
issue 401 OPEN
run $'- item\n\n```\nx\n```\n\n    Closes #401\n' FrankRay78
ok "a fence at column 0 closes the list"             'failed && names "no closing keyword"'
new_case
issue 401 OPEN
run $'- item\n\n para\n\n    Closes #401\n' FrankRay78
ok "a 1-3 space paragraph closes the list"           'failed && names "no closing keyword"'
new_case
issue 401 OPEN
# What matters is whether the LAST EMITTED LINE was paragraph text, not whether it was blank. These
# four positions differ between the two readings, and GitHub starts a code block in all of them.
run '    Closes #401' FrankRay78
ok "an indented block at the start of the body fails" 'failed && names "no closing keyword"'
new_case
issue 401 OPEN
run $'# Heading\n    Closes #401\n' FrankRay78
ok "…and one straight after a heading"               'failed && names "no closing keyword"'
new_case
issue 401 OPEN
run $'---\n    Closes #401\n' FrankRay78
ok "…and one after a thematic break"                 'failed && names "no closing keyword"'
new_case
issue 401 OPEN
run $'```\nx\n```\n    Closes #401\n' FrankRay78
ok "…and one after a closed fence"                   'failed && names "no closing keyword"'
new_case
issue 401 OPEN
# An indented chunk cannot interrupt a paragraph, so this one is prose and GitHub links it.
run $'some text\n    Closes #401\n' FrankRay78
ok "an indented line inside a paragraph still links" 'linked'
new_case
issue 401 OPEN
# A blockquote renders as its own block: quoted prose links, quoted code does not.
run '> Closes #401' FrankRay78
ok "a blockquoted reference links"                   'linked'
new_case
issue 401 OPEN
run $'> ```\n> Closes #401\n> ```\n' FrankRay78
ok "a blockquoted fence still fences"                'failed && names "no closing keyword"'
new_case
issue 401 OPEN
run $'> text\n>\n>     Closes #401\n' FrankRay78
ok "a blockquoted indented block is still code"      'failed && names "no closing keyword"'
new_case
issue 401 OPEN
# Two unbalanced backticks in separate paragraphs are prose, not a span across the blank line.
# Pairing them deleted every line between, including the keyword doing its job.
run $'Run `make\n\nCloses #401\n\nNote the ` character.\n' FrankRay78
ok "unpaired backticks across paragraphs still link" 'linked'
new_case
issue 401 OPEN
run $'Do not write `Closes\n#401` in the body.\n' FrankRay78
ok "…but a span inside one paragraph still hides it" 'failed && names "no closing keyword"'
new_case
issue 401 OPEN
# The opening fence may be indented up to three spaces, and the closer need not match its indent.
# Without the marker cleanup this reports an unterminated fence on a well-formed body.
run $'  ```\nCloses #401\n```\n' FrankRay78
ok "an indented opening fence still fences"          'failed && names "no closing keyword"'
ok "…and is not reported as unterminated"            '! names "unterminated"'
new_case
issue 401 OPEN
# A pull request template is mostly commented-out instructions, so this is the shape most likely to
# quote the convention. GitHub does not render an HTML comment and does not link inside one.
run '<!-- Closes #401 -->' FrankRay78
ok "a reference inside an HTML comment fails"        'failed && names "no closing keyword"'
new_case
issue 401 OPEN
run $'<!--\nCloses #401\n-->\n' FrankRay78
ok "…including one spanning several lines"           'failed && names "no closing keyword"'
new_case
issue 401 OPEN
run '<!-- a note --> Closes #401' FrankRay78
ok "a closed HTML comment beside a real link passes" 'linked'
new_case
issue 401 OPEN
# An opener with no closer is literal text to GitHub, not a comment that swallows the rest of the
# body — verified against GitHub's renderer, which still links a reference sitting above one.
run $'Closes #401\n<!-- oops\n' FrankRay78
ok "an unterminated comment leaves a real link alone" 'linked'
new_case
issue 401 OPEN
run 'Closes <!-- not a comment at all #401' FrankRay78
ok "…and is not itself a reference"                  'failed && names "no closing keyword"'
new_case
issue 401 OPEN
# A comment may contain backticks — a pull request template routinely does. Stripping spans first
# would pair those backticks across the `-->` and misread a terminated comment as unterminated.
run $'<!-- a `b` c -->\nCloses #401 `d` e\n' FrankRay78
ok "a comment containing backticks still closes"     'linked'
new_case
issue 401 OPEN
# A span that collapses must not let the text either side become adjacent.
run 'Closes `the thing` #401' FrankRay78
ok "a span between keyword and reference breaks it"  'failed && names "no closing keyword"'
new_case
issue 401 OPEN
run $'```\nCloses #401\n```\n\nCloses #401\n' FrankRay78
ok "a fenced example beside a real link still passes" 'linked'
new_case
issue 401 OPEN
# The fence state machine: only the delimiter that opened a block may close it, so this stays one
# block rather than ending at the ``` line. A boolean toggle would end it there and expose the ref.
run $'~~~\n```\nCloses #401\n~~~\n' FrankRay78
ok "a tilde fence quoting a backtick line stays shut" 'failed && names "no closing keyword"'
new_case
issue 401 OPEN
run $'````\n```\nCloses #401\n````\n' FrankRay78
ok "a long fence is not closed by a shorter one"     'failed && names "no closing keyword"'
new_case
issue 401 OPEN
run $'```text\nCloses #401\n```\n' FrankRay78
ok "a fence with an info string still fences"        'failed && names "no closing keyword"'

echo ""
echo "The number must be an open issue in THIS repository:"
new_case
run 'Closes #499' FrankRay78
ok "a number with no issue behind it fails"          'failed && names "#499"'
ok "…and says the issue does not exist"              'names "no issue at that number"'
new_case
issue_elsewhere 401 other/elsewhere
# A bare #401 that was transferred out of o/r still resolves here through GitHub's redirect, exit 0
# and OPEN, so a `*/issues/*` substring test reports the body linked and prints the OTHER
# repository's url in its own success line — while merging closes nothing in o/r.
run 'Closes #401' FrankRay78
ok "a transferred issue fails"                       'failed && names "#401"'
ok "…and names the repository it resolved to"        'names "not an issue in o/r"'
ok "…and never claims the body is linked"            '! names "OK"'
new_case
issue 401 OPEN
# GitHub DOES resolve a leading-zero reference — verified against its own renderer, where `#0401`
# and `#00401` both autolink to 401. Rejecting them would fail a pull request that really does
# close its issue, so they are stripped to the number GitHub would resolve and looked up.
run 'Closes #0401' FrankRay78
ok "a leading-zero reference is resolved, not refused" 'linked'
ok "…and the lookup uses the number without zeros"   'asked "issue view 401 --json url,state -R o/r"'
new_case
issue 401 OPEN
run 'Closes #00401' FrankRay78
ok "…however many zeros"                             'linked'
new_case
issue 401 OPEN
run 'Closes #99999999999999999999401' FrankRay78
# `$((10#…))` used to wrap this to 200376420520689065 and look THAT up, naming an issue the body
# never mentioned. The digits are now counted, not evaluated.
ok "a reference with too many digits fails"          'failed && names "too many digits"'
ok "…and never names a number the body lacks"        '! names "#200376420520689065"'
ok "…and spends no lookup on it"                     'never_asked'
new_case
issue 401 OPEN
run 'Closes #0' FrankRay78
ok "zero is rejected by name"                        'failed && names "zero is not an issue number"'
new_case
issue 401 OPEN
run 'Closes #000' FrankRay78
ok "…and so is a run of zeros"                       'failed && names "zero is not an issue number"'
new_case
issue 401 OPEN
run 'Closes #401' FrankRay78
# The repository is injected precisely so a fork checks the BASE repo, where the issue lives.
# Dropping `-R` leaves every other case green, because the stub answers the same either way.
ok "the lookup is targeted at the injected repo"     'linked && asked "issue view 401 --json url,state -R o/r"'
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
# The number must actually reach `gh`. Dropping it leaves the check silently reading the CURRENT
# branch's pull request while this case still reports green.
ok "…and the number reaches the lookup"              'asked "pr view 77 --json body,author -R o/r"'
new_case
issue 401 OPEN
pr 'Closes #401' FrankRay78
run_local 'not-a-number'
ok "a non-numeric PR argument fails closed"          'failed && names "is not a pull request number"'
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
# PATH is emptied so the check cannot find `gh`. Asserting rc=1 rather than non-zero is what pins
# that the check ran and rendered a verdict, rather than never starting.
mkdir -p "$SB/empty" || setup_fail "mkdir empty"
run_on_path "$SB/empty" 'Closes #401' FrankRay78
ok "missing tooling fails closed"                    'failed && names "is not available"'
new_case
issue 401 OPEN
# A single missing tool, rather than an emptied PATH — here `tr`, which the precondition loop refuses
# on before any parsing begins. Asserting the tool BY NAME is the point: `names "is not available"`
# alone passes for any tool in that loop, so a symlink this loop silently failed to create would leave
# the case green while testing a different tool than it claims.
mkdir -p "$SB/notr" || setup_fail "mkdir notr"
for t in gh jq grep sed awk mktemp rm cat dirname env printf chmod; do
  p="$(command -v "$t")" || setup_fail "command -v $t"
  ln -sf "$p" "$SB/notr/$t" || setup_fail "link $t"
done
ln -sf "$SB/bin/gh" "$SB/notr/gh" || setup_fail "link gh stub"
run_on_path "$SB/notr" 'Closes #401' FrankRay78
ok "one missing tool fails closed, not quietly"      'failed && names "'"'"'tr'"'"' is not available"'
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
new_case
issue 401 OPEN
# An injected author with no injected body: the workflow's env key renamed, or a trigger whose
# payload lacks the field. Reporting "no closing keyword" here would send the author to fix a body
# that was never supplied — the answer/outage conflation, in the one place that had no guard.
OUTPUT="$(env STUB_DIR="$STUB_DIR" PATH="$SB/bin:$PATH" CLAUDE_PROJECT_DIR="$WORK" \
  PR_ISSUE_LINK_REPO=o/r PR_ISSUE_LINK_AUTHOR=FrankRay78 bash "$CHECK" 2>&1)"; RC=$?
ok "an author with no body at all fails closed"      'failed && names "no body was supplied"'
ok "…and does not call it an unlinked body"          '! names "no closing keyword"'
new_case
issue 401 OPEN
# Set-but-empty is a real body that genuinely carries no link, and must keep saying so.
run '' FrankRay78
ok "an injected empty body is still unlinked"        'failed && names "no closing keyword"'

echo ""
# GitHub serves CRLF for a body edited in the web UI, so every verdict must be the same as it is for
# the LF form. These cases pin that equivalence, which is the user-visible property. They do NOT
# isolate `tr -d '\r'` — the scanning stages absorb a stray \r on their own, so deleting the `tr`
# leaves them green. It stays because it normalises the input once rather than relying on each stage
# to keep doing so; do not read these cases as proving it is load-bearing.
echo "Carriage returns — a web-UI body must reach the same verdict as its LF form:"
new_case
issue 401 OPEN
run $'## Summary\r\n\r\nCloses #401\r' FrankRay78
ok "a CRLF body still matches its keyword"           'linked'
new_case
issue 401 OPEN
run $'```\r\nCloses #401\r\n```\r' FrankRay78
ok "…and a CRLF fence still hides what it fences"    'failed && names "no closing keyword"'
new_case
issue 401 OPEN
run $'Like this:\r\n\r\n    Closes #401\r\n' FrankRay78
ok "…and a CRLF indented block still hides it"       'failed && names "no closing keyword"'
new_case
issue 401 OPEN
run $'<!--\r\nCloses #401\r\n-->\r\n' FrankRay78
ok "…and a CRLF HTML comment still hides it"         'failed && names "no closing keyword"'
new_case
issue 401 OPEN
run $'- the first change\r\n\r\n    Closes #401\r\n' FrankRay78
ok "…and a CRLF list continuation still links"       'linked'

echo ""
echo "----------------------------------------"
# Cases and assertions are counted separately because a case may assert more than once — a bare
# "passed: 59" beside "cases: 55" reads as a counting bug to anyone checking the gate's own gate.
echo "cases: $cases   assertions: $pass passed, $fail failed"
[ "$fail" -eq 0 ] || exit 1
