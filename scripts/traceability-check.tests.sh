#!/usr/bin/env bash
#
# traceability-check.tests.sh — case matrix for scripts/traceability-check.sh (issue #319).
#
# The check reads three things it cannot be handed on stdin: the branch name, the issue body
# (over the network, via `gh`) and the repository's committed test files. Every case therefore
# builds a throwaway git repo, commits fixture test files into it, stubs `gh` on PATH, and points
# the real, unmodified check at that sandbox via CLAUDE_PROJECT_DIR. No case touches the network
# or this repo's own files.
#
# TWO THINGS THIS FILE IS CAREFUL ABOUT:
#
#   1. Passing is not one outcome, it is four. "nothing to check" (no issue number, no labels,
#      not an issue) and "checked and clean" both exit 0, so a check stubbed to `exit 0` on line
#      one would satisfy a bare `rc == 0`. Every passing case therefore asserts on WHICH verdict
#      was reached, not just the code.
#   2. Fixture scenario names are fictional ("Widget hums when wound"). A fixture naming a real
#      #319 label would be a marker in a committed *.tests.sh — i.e. this file would start
#      satisfying the very labels it is testing the detection of.
#
# Exits non-zero on any failure. Run after any edit to the check.
#
#   Usage:  scripts/traceability-check.tests.sh

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CHECK="$HERE/traceability-check.sh"
BASH_BIN="$(command -v bash)"

# One parent-scoped root holds every sandbox, so cleanup cannot be defeated by a subshell.
ROOT="$(mktemp -d)"
cleanup() { chmod -R u+rwX "$ROOT" 2>/dev/null; rm -rf "$ROOT"; }
trap cleanup EXIT

pass=0; fail=0; cases=0
ok() { if eval "$2"; then echo "  ok   $1"; pass=$((pass+1)); else echo "  FAIL $1 -- rc=$RC out:[$(printf '%s' "$OUTPUT" | head -3 | tr '\n' '/')]"; fail=$((fail+1)); fi; }

# new_case — a fresh sandbox repo on main, a stub dir, and a `gh` stub earlier on PATH than the
# real one. The stub serves from $STUB_DIR and exits non-zero with GitHub's own "could not
# resolve" wording for an unknown number, so the check's answer/outage distinction is exercised
# against the real string rather than a paraphrase.
# setup_fail — a broken fixture is not a test result. Every setup step below is checked through
# this, because an unchecked one is worse than useless: with the fixtures missing, a sandbox has
# no committed test files, so every case asserting `failed && names "…"` still passes while
# verifying nothing about the discrimination it claims to test.
setup_fail() { echo "  FIXTURE SETUP FAILED (case $cases): $*" >&2; exit 1; }

new_case() {
  cases=$((cases+1))
  SB="$ROOT/case$cases"
  REPO="$SB/repo"
  STUB_DIR="$SB/stub"
  # Reset the verdict globals. They are what `ok` reads, so leaving the previous case's values in
  # place let a lost or mis-edited `check` line make the next assertion silently re-assert the
  # PREVIOUS case's result — deleting a `check` line left the matrix fully green.
  RC=99
  OUTPUT=""
  mkdir -p "$REPO" "$STUB_DIR" "$SB/bin" || setup_fail "mkdir"
  git -C "$REPO" init -q -b main || setup_fail "git init"
  git -C "$REPO" config user.name traceability-tests || setup_fail "git config name"
  git -C "$REPO" config user.email traceability-tests@example.invalid || setup_fail "git config email"
  git -C "$REPO" config commit.gpgsign false || setup_fail "git config gpgsign"
  git -C "$REPO" commit -q --allow-empty -m init || setup_fail "initial commit"
  cat > "$SB/bin/gh" <<'STUB'
#!/usr/bin/env bash
if [ "$1" = repo ]; then
  if [ -f "$STUB_DIR/repo-unreachable" ]; then cat "$STUB_DIR/repo-unreachable" >&2; exit 1; fi
  cat "$STUB_DIR/repo"; exit 0
fi
if [ "$1" = issue ]; then
  n="$3"
  if [ -f "$STUB_DIR/issue-$n.json" ]; then cat "$STUB_DIR/issue-$n.json"; exit 0; fi
  if [ -f "$STUB_DIR/unreachable-$n" ]; then cat "$STUB_DIR/unreachable-$n" >&2; exit 1; fi
  echo "GraphQL: Could not resolve to an issue or pull request with the number of $n. (repository.issue)" >&2
  exit 1
fi
echo "gh: unexpected stub invocation: $*" >&2
exit 1
STUB
  chmod +x "$SB/bin/gh" || setup_fail "chmod gh stub"
  printf 'o/r\n' > "$STUB_DIR/repo" || setup_fail "write repo stub"
}

# write_file PATH CONTENT — write a file into the sandbox repo WITHOUT committing it.
write_file() {
  mkdir -p "$REPO/$(dirname "$1")" || setup_fail "mkdir for $1"
  printf '%s\n' "$2" > "$REPO/$1" || setup_fail "write $1"
}

# commit_file PATH CONTENT — write a file into the sandbox repo and commit it.
commit_file() {
  write_file "$1" "$2"
  git -C "$REPO" add -- "$1" || setup_fail "git add $1"
  git -C "$REPO" commit -q -m "add $1" || setup_fail "git commit $1"
}

# issue N BODY — the stub's reply for issue N.
issue() { jq -n --arg b "$2" --arg u "https://github.com/o/r/issues/$1" '{body:$b,url:$u}' > "$STUB_DIR/issue-$1.json" || setup_fail "issue $1"; }
# raw N JSON — the stub's reply for N, verbatim, for malformed-response cases.
raw()   { printf '%s' "$2" > "$STUB_DIR/issue-$1.json" || setup_fail "raw $1"; }
# pull N BODY — the stub's reply for a number that is a pull request, as `gh issue view` really
# answers. The body carries a label on purpose: with an empty one the check exits 0 down the
# no-labels route, so the case passed without the URL discrimination ever running.
pull()  { jq -n --arg b "$2" --arg u "https://github.com/o/r/pull/$1" '{body:$b,url:$u}' > "$STUB_DIR/issue-$1.json" || setup_fail "pull $1"; }
# unreachable N MESSAGE — the stub cannot answer for N (auth, network, rate limit).
unreachable() { printf '%s\n' "$2" > "$STUB_DIR/unreachable-$1" || setup_fail "unreachable $1"; }

# check [BRANCH] [ENV…] — run the check against the sandbox. The branch arrives as the positional
# argument the check accepts, so a case can target any issue without renaming a branch.
check() {
  local branch="$1"; shift
  OUTPUT="$(env CLAUDE_PROJECT_DIR="$REPO" STUB_DIR="$STUB_DIR" PATH="$SB/bin:$PATH" "$@" bash "$CHECK" ${branch:+"$branch"} 2>&1)"; RC=$?
}

passed()  { [ "$RC" = 0 ]; }
failed()  { [ "$RC" = 1 ]; }
# A pass must say WHICH pass it is: "nothing to check" and "checked and clean" are different
# answers. `nothing` therefore takes the verdict it expects — all FOUR quiet passes print the
# shared words "nothing to check", so matching only those proved the exit code and nothing else:
# a numberless branch that fell through to a nonexistent issue satisfied it just as well.
clean()   { passed && printf '%s' "$OUTPUT" | grep -qF 'OK'; }
nothing() { passed && printf '%s' "$OUTPUT" | grep -qF 'nothing to check' && printf '%s' "$OUTPUT" | grep -qF -- "$1"; }
names()   { printf '%s' "$OUTPUT" | grep -qF -- "$1"; }

echo "traceability-check.tests.sh"
echo ""

# SCENARIO: Every labelled scenario has a matching test marker
echo "Every labelled scenario has a matching test marker:"
new_case
commit_file "src/NetPace.Core.Tests/WidgetTests.cs" '    // SCENARIO: Widget hums when wound'
issue 401 '## Capability

**Scenario: Widget hums when wound**
Given a wound widget, when it is released, then it hums.'
check feature/401-widget-hum
ok "a label matched by a C# marker passes"         'clean'
ok "the report names the issue it checked"         'names "#401"'
new_case
# Built with an escape rather than a literal newline: as a two-line single-quoted string, the
# continuation began at column 0 with `//`, which made this fixture a live marker in a committed
# `*.tests.sh` — the gate's own file contributed `Widget chimes on the hour'` to the real marker
# set. §VIII calls a marker that names no label worse than no marker at all.
commit_file "src/NetPace.Core.Tests/WidgetTests.cs" $'// SCENARIO: Widget hums when wound\n// SCENARIO: Widget chimes on the hour'
issue 402 '**Scenario: Widget hums when wound**

**Scenario: Widget chimes on the hour**'
check feature/402-widget
ok "several labels all matched pass together"      'clean'
ok "…and the verdict counts them, not just one"   'names "all 2 labelled"'
new_case
commit_file "src/NetPace.Core.Tests/WidgetTests.cs" '//   SCENARIO:   Widget hums when wound   '
issue 403 '   **Scenario:   Widget hums when wound**   '
check feature/403-widget
ok "surrounding whitespace is trimmed on both ends" 'clean'

# SCENARIO: A labelled scenario with no matching marker fails the check
echo ""
echo "A labelled scenario with no matching marker fails the check:"
new_case
commit_file "src/NetPace.Core.Tests/WidgetTests.cs" '// SCENARIO: Widget hums when wound'
issue 404 '**Scenario: Widget hums when wound**

**Scenario: Widget chimes on the hour**'
check feature/404-widget
ok "an unmatched label fails"                      'failed'
ok "…and the failure names it"                     'names "Widget chimes on the hour"'
ok "…without naming the label that matched"        '! names "Widget hums when wound"'
new_case
issue 405 '**Scenario: Widget hums when wound**

**Scenario: Widget chimes on the hour**'
check feature/405-widget
ok "every unmatched label is named, not just the first" 'failed && names "Widget hums when wound" && names "Widget chimes on the hour"'
new_case
write_file "src/NetPace.Core.Tests/WidgetTests.cs" '// SCENARIO: Widget hums when wound'
issue 406 '**Scenario: Widget hums when wound**'
check feature/406-widget
ok "an UNTRACKED marker does not count"            'failed && names "Widget hums when wound"'
new_case
# Staged and never committed. This case is the reason the scan reads HEAD rather than the working
# tree: `git grep` over the worktree counted this file, so "committed test file" — the wording in
# §VIII, CLAUDE.md and testing.md — was not what the gate actually required.
write_file "src/NetPace.Core.Tests/WidgetTests.cs" '// SCENARIO: Widget hums when wound'
git -C "$REPO" add -A || setup_fail "stage 442"
issue 442 '**Scenario: Widget hums when wound**'
check feature/442-widget
ok "a STAGED, uncommitted marker does not count"   'failed && names "Widget hums when wound"'
new_case
commit_file "docs/conventions/testing.md" '// SCENARIO: Widget hums when wound'
issue 407 '**Scenario: Widget hums when wound**'
check feature/407-widget
ok "a marker in a doc does not count"              'failed && names "Widget hums when wound"'
new_case
commit_file "src/NetPace.Core/Widget.cs" '// SCENARIO: Widget hums when wound'
issue 408 '**Scenario: Widget hums when wound**'
check feature/408-widget
ok "a marker in production code does not count"    'failed && names "Widget hums when wound"'
new_case
# The mention ends the line, so only the leading anchor can reject it. The trailing `";` of the
# previous fixture (`var note = "SCENARIO: …";`) was what broke the match, which meant removing
# both marker anchors from the check left this case green.
commit_file "src/NetPace.Core.Tests/WidgetTests.cs" '// a prose mention of SCENARIO: Widget hums when wound'
issue 409 '**Scenario: Widget hums when wound**'
check feature/409-widget
ok "a mid-line mention is not a marker"            'failed && names "Widget hums when wound"'

# SCENARIO: A near-miss marker does not count
echo ""
echo "A near-miss marker does not count:"
new_case
commit_file "src/NetPace.Core.Tests/WidgetTests.cs" '// SCENARIO: widget hums when wound'
issue 410 '**Scenario: Widget hums when wound**'
check feature/410-widget
ok "a marker differing in case fails"              'failed && names "Widget hums when wound"'
new_case
commit_file "src/NetPace.Core.Tests/WidgetTests.cs" '// SCENARIO: Widget hums when wound.'
issue 411 '**Scenario: Widget hums when wound**'
check feature/411-widget
ok "a marker differing in punctuation fails"       'failed && names "Widget hums when wound"'
new_case
commit_file "src/NetPace.Core.Tests/WidgetTests.cs" '// SCENARIO: Widget hums  when wound'
issue 412 '**Scenario: Widget hums when wound**'
check feature/412-widget
ok "internal spacing is load-bearing"              'failed && names "Widget hums when wound"'
new_case
commit_file "src/NetPace.Core.Tests/WidgetTests.cs" '// SCENARIO: Widget hums when wound and chimes'
issue 413 '**Scenario: Widget hums when wound**'
check feature/413-widget
ok "a marker that merely CONTAINS the label fails"  'failed && names "Widget hums when wound"'

# SCENARIO: A marker in a shell test counts the same as one in a C# test
echo ""
echo "A marker in a shell test counts the same as one in a C# test:"
new_case
commit_file "scripts/thing.tests.sh" '# // SCENARIO: Widget hums when wound'
issue 414 '**Scenario: Widget hums when wound**'
check feature/414-widget
ok "a '# // SCENARIO:' marker in a shell test counts" 'clean'
new_case
commit_file ".claude/hooks/thing.tests.sh" '# SCENARIO: Widget hums when wound'
issue 415 '**Scenario: Widget hums when wound**'
check feature/415-widget
ok "a '# SCENARIO:' marker in a shell test counts"    'clean'
new_case
commit_file "scripts/thing.sh" '# SCENARIO: Widget hums when wound'
issue 416 '**Scenario: Widget hums when wound**'
check feature/416-widget
ok "…but a non-test shell script is not a test file"  'failed && names "Widget hums when wound"'

# SCENARIO: An issue with no scenario labels passes
echo ""
echo "An issue with no scenario labels passes:"
new_case
issue 417 '## Summary

Make the widget hum.

## Acceptance criteria

- [ ] The widget hums when wound.'
check feature/417-widget
ok "an issue with no labels passes with nothing to check" 'nothing "carries no"'
new_case
# The label sits bare on its own line inside the fence. With the `issue:  ` prefix it carried
# before, the check's line anchor rejected it whether or not the fence was stripped, so replacing
# the whole fence-handling stage with `cat` left this case green — it tested nothing.
issue 418 '```
**Scenario: Widget hums when wound**
```'
check feature/418-widget
ok "a label inside a fenced block is not a label"     'nothing "carries no"'
new_case
issue 419 'Write it as `**Scenario: Widget hums when wound**` on its own line.'
check feature/419-widget
ok "a label inside inline code is not a label"        'nothing "carries no"'
new_case
issue 420 '**Scenario:** labels are optional.'
check feature/420-widget
ok "a bare '**Scenario:**' mention names nothing"     'nothing "carries no"'
new_case
# A `~~~` block quoting a ``` line: a bare toggle closed on the inner line and dropped the two
# real labels below. Only the delimiter that opened the block may close it.
commit_file "src/NetPace.Core.Tests/WidgetTests.cs" '// SCENARIO: Widget hums when wound'
issue 430 '~~~
issue:  **Scenario: Example inside a fence**
```
~~~

**Scenario: Widget hums when wound**

**Scenario: Widget chimes on the hour**'
check feature/430-widget
ok "a nested fence does not hide the labels below it" 'failed && names "Widget chimes on the hour"'
ok "…and the fenced example is not itself a label"   '! names "Example inside a fence"'
new_case
# An unterminated fence is a malformed body, not an unlabelled one: the labels below it are
# unreadable, so the gate must fail rather than report nothing to check.
issue 431 '## Notes

```
pasted output, closing fence forgotten

**Scenario: Widget hums when wound**'
check feature/431-widget
ok "an unterminated fence fails rather than passing" 'failed && names "unterminated code fence"'

# SCENARIO: A branch with no issue number passes
echo ""
echo "A branch with no issue number passes:"
new_case
check feature/net10-upgrade
ok "a branch with no number passes with nothing to check" 'nothing "no issue number in branch"'
ok "…and 'net10' is never read as issue 10"               '! names "#10"'
new_case
check chore/docs-tidy
ok "a prefixed branch with no number passes"              'nothing "no issue number in branch"'
new_case
check main
ok "an unprefixed branch name passes"                     'nothing "no issue number in branch"'
new_case
commit_file "src/NetPace.Core.Tests/WidgetTests.cs" '// SCENARIO: Widget hums when wound'
issue 421 '**Scenario: Widget hums when wound**'
check "" TRACEABILITY_BRANCH=feature/421-widget
ok "the branch may arrive by env var instead"             'clean'
new_case
commit_file "src/NetPace.Core.Tests/WidgetTests.cs" '// SCENARIO: Widget hums when wound'
issue 432 '**Scenario: Widget hums when wound**'
check feature/432-widget TRACEABILITY_BRANCH=feature/999-ignored
ok "the positional branch wins over the env var"          'clean && names "#432"'
new_case
commit_file "src/NetPace.Core.Tests/WidgetTests.cs" '// SCENARIO: Widget hums when wound'
issue 433 '**Scenario: Widget hums when wound**'
# The leading number of the segment, with any leading zeros dropped.
check feature/0433-widget
ok "a zero-padded number resolves to the issue"           'clean && names "#433"'
new_case
# The invocation CI actually uses: TRACEABILITY_REPO is set, which short-circuits `gh repo view`.
# Nothing exercised this path, so the merge gate's own code path had no coverage at all.
commit_file "src/NetPace.Core.Tests/WidgetTests.cs" '// SCENARIO: Widget hums when wound'
issue 434 '**Scenario: Widget hums when wound**'
rm -f "$STUB_DIR/repo"
check feature/434-widget TRACEABILITY_REPO=o/r
ok "the repository may be passed in, as CI passes it"     'clean'
new_case
# The documented local invocation: no branch argument, read off HEAD with `git rev-parse`.
commit_file "src/NetPace.Core.Tests/WidgetTests.cs" '// SCENARIO: Widget hums when wound'
issue 435 '**Scenario: Widget hums when wound**'
git -C "$REPO" checkout -q -b feature/435-widget || setup_fail "branch 435"
check ""
ok "the branch falls back to the checked-out HEAD"        'clean && names "#435"'
new_case
# A label with no marker, so a pass is only reachable through the URL check. With the empty body
# this case used to carry, exit 0 came from the no-labels route and deleting the whole URL
# discrimination left the matrix green.
pull 422 '**Scenario: Widget hums when wound**'
check feature/422-widget
ok "a number that is a pull request passes"               'nothing "is not an issue"'
new_case
check feature/423-widget
ok "a number that resolves to nothing passes"             'nothing "does not exist"'

# SCENARIO: An issue that cannot be retrieved fails the check
echo ""
echo "An issue that cannot be retrieved fails the check:"
new_case
unreachable 424 'gh: HTTP 401: Bad credentials (https://api.github.com/graphql)'
check feature/424-widget
ok "an auth failure fails rather than passing"       'failed'
ok "…and reports gh's own reason"                    'names "Bad credentials"'
new_case
unreachable 425 'error connecting to api.github.com: dial tcp: lookup api.github.com: no such host'
check feature/425-widget
ok "a network failure fails rather than passing"     'failed && names "no such host"'
new_case
unreachable 426 'gh: API rate limit exceeded for user ID 1.'
check feature/426-widget
ok "a rate limit fails rather than passing"          'failed && names "rate limit"'
new_case
printf 'gh: HTTP 403\n' > "$STUB_DIR/repo-unreachable"
check feature/427-widget
ok "an unresolvable repository fails rather than passing" 'failed && names "HTTP 403"'

echo ""
echo "Fail-closed guards — a gate that cannot run must not report green:"
new_case
commit_file "src/NetPace.Core.Tests/WidgetTests.cs" '// SCENARIO: Widget hums when wound'
issue 428 '**Scenario: Widget hums when wound**'
# PATH is emptied so the check cannot find `gh`. The interpreter is named by absolute path, since
# an emptied PATH would otherwise stop `bash` itself resolving and report 127 — the check never
# running, read as the check's own verdict. Asserting rc=1 rather than non-zero is what pins that.
mkdir -p "$SB/empty"
OUTPUT="$(env CLAUDE_PROJECT_DIR="$REPO" STUB_DIR="$STUB_DIR" PATH="$SB/empty" "$BASH_BIN" "$CHECK" feature/428-widget 2>&1)"; RC=$?
ok "missing tooling fails closed"                    'failed'
new_case
rm -rf "$REPO/.git"
issue 429 '**Scenario: Widget hums when wound**'
# GIT_CEILING_DIRECTORIES stops `rev-parse --git-dir` walking upward out of the sandbox. Without
# it this case silently depended on $TMPDIR not sitting inside a working tree: under one, the
# sandbox resolved to the OUTER repo and the case still reported `failed`, for the wrong reason.
# Asserting the wording as well as the code is what pins which failure this is.
check feature/429-widget GIT_CEILING_DIRECTORIES="$ROOT"
ok "a repo root that is not a git repository fails closed" 'failed && names "is not a git repository"'
new_case
# A single missing tool, rather than an emptied PATH. The emptied-PATH case above hides `gh`
# first, so it never reached the extraction pipelines — where a missing `tr` used to report a
# labelled issue as unlabelled and exit 0.
commit_file "src/NetPace.Core.Tests/WidgetTests.cs" '// SCENARIO: Widget hums when wound'
issue 436 '**Scenario: Widget hums when wound**'
mkdir -p "$SB/notr" || setup_fail "mkdir notr"
for t in git gh jq grep sed awk xargs mktemp rm cat dirname env printf chmod; do
  p="$(command -v "$t")" && ln -sf "$p" "$SB/notr/$t"
done
ln -sf "$SB/bin/gh" "$SB/notr/gh" || setup_fail "link gh stub"
OUTPUT="$(env CLAUDE_PROJECT_DIR="$REPO" STUB_DIR="$STUB_DIR" PATH="$SB/notr" "$BASH_BIN" "$CHECK" feature/436-widget 2>&1)"; RC=$?
ok "one missing tool fails closed, not quietly"      'failed && names "is not available"'
new_case
# A reply the check cannot read is not an unlabelled issue. Each of these reported "carries no
# labels" and exited 0 before the body was validated.
raw 437 '{"url":"https://github.com/o/r/issues/437"}'
check feature/437-widget
ok "a reply with no body field fails closed"         'failed && names "no readable body"'
new_case
raw 438 '{"body":null,"url":"https://github.com/o/r/issues/438"}'
check feature/438-widget
ok "a null body fails closed"                        'failed && names "no readable body"'
new_case
raw 439 'not json at all'
check feature/439-widget
ok "an unparseable reply fails closed"               'failed && names "could not parse"'
new_case
# A marker scan that cannot complete must not become a §VIII verdict: coming back short looks
# exactly like a missing marker, and would accuse the author of omitting one that is committed.
# An unborn HEAD is the cheapest way to induce it. Note this hole used to be reachable far more
# easily — scanning the working tree, an unreadable tracked file made `git grep` print
# `error: failed to stat …` and still exit 0. Reading HEAD removed that route, since the blobs
# come from the object store rather than the worktree.
commit_file "src/NetPace.Core.Tests/WidgetTests.cs" '// SCENARIO: Widget hums when wound'
issue 443 '**Scenario: Widget hums when wound**'
git -C "$REPO" update-ref -d HEAD || setup_fail "unborn HEAD 443"
check feature/443-widget
ok "a marker scan that cannot complete fails closed" 'failed && ! names "no matching marker"'

echo ""
echo "Carriage returns — GitHub serves CRLF for a body edited in the web UI:"
new_case
commit_file "src/NetPace.Core.Tests/WidgetTests.cs" '// SCENARIO: Widget hums when wound'
issue 440 $'**Scenario: Widget hums when wound**\r'
check feature/440-widget
ok "a CRLF body still matches its marker"            'clean'
new_case
issue 441 $'**Scenario: Widget chimes on the hour**\r'
check feature/441-widget
ok "…and an unmatched CRLF label is named without the \\r" 'failed && names "Widget chimes on the hour"'

echo ""
echo "----------------------------------------"
echo "cases: $cases   passed: $pass   failed: $fail"
# A matrix that silently shrinks is the failure mode shell-tests.yml guards against in its own
# discovery step: discovering nothing is a failure, not a pass. Deleting a whole block here
# otherwise reported fewer cases, zero failures, and exit 0.
[ "$cases" -ge 46 ] || { echo "FAIL: expected at least 46 cases, ran $cases — did a block get dropped?" >&2; exit 1; }
[ "$fail" -eq 0 ] || exit 1
