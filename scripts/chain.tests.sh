#!/usr/bin/env bash
#
# chain.tests.sh — standalone test matrix for chain.sh.
#
# The chain's one job is gating: which stage starts, in what order, resuming which session, and
# what happens when a stage fails. All of that is provable from outside with a stub `claude`
# first on PATH, so no model is called and nothing is spent. Every case runs the chain inside a
# throwaway git repo under a mktemp sandbox, so your checkout is never touched. Exits non-zero
# on any failure. Run it after any edit to the chain.
#
# Reopening a real session and a real end-to-end run need a real model, so they are checked by
# hand rather than faked here. That is the one scenario with no marker below — "A failed stage
# can be reopened".
#
#   Usage:  scripts/chain.tests.sh

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CHAIN="$HERE/chain.sh"

SB="$(mktemp -d)"
trap 'rm -rf "$SB"' EXIT
# A developer's ambient overrides would otherwise reach every case; the stall case sets its own.
unset CHAIN_MODEL CHAIN_STAGE_TIMEOUT

# Anything on the chain's stdin that reaches the stub proves the stage's own `</dev/null` is
# gone — the omission chain.sh records as making an unattended call stall on the terminal.
printf 'this must never reach a stage\n' > "$SB/stdin-data"

# The stub claude. One log line per invocation, arguments separated by `|` so a lost quote is
# visible as a new field rather than hidden by space-joining. The reply is the canned JSON the
# case wrote for that call number. A sleep-<n> file makes call n record its PID and sleep
# instead — exec, so the recorded PID is the sleeping process itself. A kill-<n> file makes call n
# kill itself with SIGKILL, which reaches the chain as exit 137 from a stage killed from outside
# rather than at its time limit (an out-of-memory kill is the real case). An act-<n> file is a
# script run in the case repo before call n replies, so a case can give the repository the branch,
# commits and working-tree state a real stage would have left behind.
mkdir -p "$SB/bin"
cat > "$SB/bin/claude" <<'STUB'
#!/usr/bin/env bash
{ for a in "$@"; do printf '%s|' "$a"; done; printf '\n'; } >> "$STUB_DIR/log"
if IFS= read -r -t 0.1 _ <&0 2>/dev/null; then printf 'leaked\n' >> "$STUB_DIR/stdin-leak"; fi
n=$(grep -c '' "$STUB_DIR/log")
if [ -f "$STUB_DIR/act-$n" ]; then bash "$STUB_DIR/act-$n" || exit 1; fi
if [ -f "$STUB_DIR/sleep-$n" ]; then
  echo $$ > "$STUB_DIR/pid-$n"
  exec sleep "$(cat "$STUB_DIR/sleep-$n")"
fi
if [ -f "$STUB_DIR/kill-$n" ]; then kill -KILL $$; fi
cat "$STUB_DIR/reply-$n.json"
STUB
chmod +x "$SB/bin/claude"

pass=0; fail=0; cases=0
ok() { if eval "$2"; then echo "  ok   $1"; pass=$((pass+1)); else echo "  FAIL $1 -- rc=$RC output:[$OUTPUT]"; fail=$((fail+1)); fi; }

# A fresh repo on main with one commit, and an empty stub directory, per case.
new_case() {
  cases=$((cases+1))
  REPO="$SB/case$cases/repo"
  export STUB_DIR="$SB/case$cases/stub"
  mkdir -p "$REPO" "$STUB_DIR"
  git -C "$REPO" init -q -b main
  git -C "$REPO" config user.name chain-tests
  git -C "$REPO" config user.email chain-tests@example.invalid
  git -C "$REPO" config commit.gpgsign false
  git -C "$REPO" commit -q --allow-empty -m init
}

# reply <n> <result text> — the JSON reply for call n.
reply() {
  jq -n --arg r "$2" --arg s "sess-$1" \
    '{type:"result",subtype:"success",is_error:false,result:$r,session_id:$s}' > "$STUB_DIR/reply-$1.json"
}

# chain <args…> — run the chain from inside the case repo. Its stdin carries data on purpose;
# see stdin-data above. Sets OUTPUT (stdout and stderr) and RC, which every assertion reads.
chain() { OUTPUT="$(cd "$REPO" && PATH="$SB/bin:$PATH" bash "$CHAIN" "$@" <"$SB/stdin-data" 2>&1)"; RC=$?; }

# grep -c '' rather than `wc -l`, whose output is right-padded on BSD/macOS (Principle IV).
calls() { if [ -f "$STUB_DIR/log" ]; then grep -c '' "$STUB_DIR/log"; else echo 0; fi; }
call() { sed -n "$1p" "$STUB_DIR/log" 2>/dev/null; }
repo_state() { git -C "$REPO" rev-parse HEAD; git -C "$REPO" rev-parse --abbrev-ref HEAD; git -C "$REPO" status --porcelain; }

# The prompt on call n is this, and it is the single positional straight after -p. The position
# is pinned deliberately: chain.sh records (from plugin-report.sh) that any other placement makes
# the call stall, and the `|` field separator is what makes a lost quote visible here.
prompt_is() { call "$1" | grep -qF -- "-p|$2|"; }

# The chain's closing message: its last two lines, after any stage report.
closing() { printf '%s\n' "$OUTPUT" | tail -n 2; }
# The single closing line, so an assertion cannot be satisfied by the echoed stage report.
last_line() { printf '%s\n' "$OUTPUT" | tail -n 1; }

# // SCENARIO: One issue to a pull request
echo "One issue to a pull request:"
new_case
reply 1 $'Built.\nEverything committed.\nREADY branch=feature/270-x'
reply 2 'STUDIED issue=270 rows=0'
reply 3 'VERIFIED branch=feature/270-x'
reply 4 'STUDIED issue=270 rows=1'
reply 5 $'Related work: https://github.com/o/r/pull/1\nRAISED pr=https://github.com/o/r/pull/9'
CHAIN_MODEL=test-model chain 270
ok "exits 0" '[ "$RC" = 0 ]'
ok "exactly five stages started" '[ "$(calls)" = 5 ]'
ok "stages in order: build, study, verify, study, raise-pr" 'prompt_is 1 "/build 270" && prompt_is 2 "/study 270" && prompt_is 3 "/verify" && prompt_is 4 "/study 270" && prompt_is 5 "/raise-pr 270"'
ok "first study resumes build's session" 'call 2 | grep -qF -- "--resume|sess-1|"'
ok "second study resumes verify's session" 'call 4 | grep -qF -- "--resume|sess-3|"'
ok "build, verify and raise-pr start fresh sessions" '[ "$(calls)" = 5 ] && ! call 1 | grep -qF -- "--resume|" && ! call 3 | grep -qF -- "--resume|" && ! call 5 | grep -qF -- "--resume|"'
ok "every stage uses the one chosen model" '[ "$(grep -cF -- "--model|test-model|" "$STUB_DIR/log" 2>/dev/null)" = 5 ]'
ok "no stage is asked to prompt for permission" '[ "$(grep -cF -- "--dangerously-skip-permissions|" "$STUB_DIR/log" 2>/dev/null)" = 5 ]'
ok "no stage can read the terminal" '[ ! -f "$STUB_DIR/stdin-leak" ]'
ok "the closing line names the pull request that was raised" 'last_line | grep -qF "chain: done — https://github.com/o/r/pull/9"'
new_case
reply 1 'READY branch=feature/270-x'; reply 2 'STUDIED issue=270 rows=0'; reply 3 'VERIFIED branch=feature/270-x'
reply 4 'STUDIED issue=270 rows=1'; reply 5 'RAISED pr=https://github.com/o/r/pull/9'
chain 270
ok "with no override every stage still names the default model" '[ "$RC" = 0 ] && [ "$(grep -cF -- "--model|claude-opus-5|" "$STUB_DIR/log")" = 5 ]'
new_case
reply 1 'READY branch=feature/270-x'
chain "#270"
ok "an issue written #270 is the same issue" 'prompt_is 1 "/build 270"'
# Regression (#242): /verify reported `## VERIFIED `branch=…``, and markdown landing between
# the verdict's two words parked a verified branch as a stage with no verdict.
new_case
reply 1 'READY `branch=feature/270-x`'; reply 2 '**STUDIED** issue=270 rows=0'; reply 3 '## VERIFIED `branch=feature/270-x`'
reply 4 'STUDIED `issue=270 rows=1`'; reply 5 'RAISED pr=https://github.com/o/r/pull/9'
chain 270
ok "markdown between a verdict's two words is still that verdict" '[ "$RC" = 0 ] && [ "$(calls)" = 5 ]'

# // SCENARIO: A failing stage stops the run
echo "A failing stage stops the run:"
new_case
reply 1 'READY branch=feature/270-x'
reply 2 'STUDIED issue=270 rows=0'
reply 3 $'Suite failed.\nFAILED reason=suite red'
before="$(repo_state)"
chain 270
ok "exits 1" '[ "$RC" = 1 ]'
ok "no stage after verify started" '[ "$(calls)" = 3 ]'
ok "closing message names verify, 3/5 and the reason" 'closing | grep -q verify && closing | grep -qF 3/5 && closing | grep -q "suite red"'
ok "closing message says how to reopen the session" 'closing | grep -q "claude --resume"'
ok "closing message names the session to reopen" 'closing | grep -qF "claude --resume sess-3"'
ok "branch, commits and working tree untouched" '[ "$(repo_state)" = "$before" ]'
new_case
reply 1 $'READY branch=feature/270-x\nFAILED reason=half built'
chain 270
ok "the final line decides, so a success line above a failure verdict is a failure" '[ "$RC" = 1 ] && [ "$(calls)" = 1 ] && closing | grep -q "half built"'
new_case
reply 1 'READY branch=feature/270-x'
reply 2 $'Row 1 records that the build stage can print FAILED reason=<text> and stop.\nSTUDIED issue=270 rows=1'
reply 3 'VERIFIED branch=feature/270-x'
reply 4 'STUDIED issue=270 rows=1'
reply 5 'RAISED pr=https://github.com/o/r/pull/9'
chain 270
ok "a study pass that quotes 'FAILED reason=' mid-sentence does not stop the run" '[ "$RC" = 0 ] && [ "$(calls)" = 5 ]'
new_case
reply 1 'READY branch=feature/270-x'
reply 2 'STUDIED issue=270 rows=0'
printf 'claude: command failed before it could start' > "$STUB_DIR/reply-3.json"
chain 270
ok "a stage that fails before its reply is read names no earlier stage's session" '[ "$RC" = 1 ] && [ "$(calls)" = 3 ] && ! closing | grep -q "sess-2" && closing | grep -q "no session id was captured"'
new_case
reply 1 'READY branch=feature/270-x'
printf 'claude: command failed before it could start' > "$STUB_DIR/reply-2.json"
chain 270
ok "a resuming stage that fails that early names the session it resumed" '[ "$RC" = 1 ] && [ "$(calls)" = 2 ] && closing | grep -qF "claude --resume sess-1"'
new_case
reply 1 'READY branch=feature/270-x'
reply 2 'STUDIED issue=270 rows=0'
echo 30 > "$STUB_DIR/sleep-3"
CHAIN_STAGE_TIMEOUT=1 chain 270
ok "a stalled stage names no earlier stage's session either" '[ "$RC" = 1 ] && ! closing | grep -q "sess-2" && closing | grep -q "no session id was captured"'

# // SCENARIO: A stage with no readable verdict is a failure
echo "A stage with no readable verdict is a failure:"
new_case
reply 1 'I finished.'
chain 270
ok "exits 1" '[ "$RC" = 1 ]'
ok "no later stage started" '[ "$(calls)" = 1 ]'
ok "closing message names build and the missing verdict" 'closing | grep -q build && closing | grep -q "no readable verdict"'
new_case
printf 'claude: command failed before it could start' > "$STUB_DIR/reply-1.json"
chain 270
ok "a reply that is not JSON says so, and shows the reply" '[ "$RC" = 1 ] && closing | grep -q "not JSON" && grep -qF "command failed before it could start" <<<"$OUTPUT"'
new_case
printf '["READY branch=feature/270-x"]' > "$STUB_DIR/reply-1.json"
chain 270
ok "a reply that is JSON but not an object says so, and shows the reply" '[ "$RC" = 1 ] && [ "$(calls)" = 1 ] && closing | grep -q "not a JSON object" && grep -qF "READY branch=feature/270-x" <<<"$OUTPUT"'
new_case
jq -n '{type:"result",subtype:"error_during_execution",is_error:true,result:"API Error: 529 overloaded",session_id:"sess-1"}' > "$STUB_DIR/reply-1.json"
chain 270
ok "an errored reply is reported as the error, not as a missing verdict" '[ "$RC" = 1 ] && grep -q "529 overloaded" <<<"$OUTPUT" && ! closing | grep -q "no readable verdict"'
new_case
jq -n '{type:"result",subtype:"success",is_error:false,result:"READY branch=feature/270-x"}' > "$STUB_DIR/reply-1.json"
chain 270
ok "a reply with no session id stops rather than studying a fresh session" '[ "$RC" = 1 ] && [ "$(calls)" = 1 ] && grep -q "session id" <<<"$OUTPUT"'
new_case
reply 1 'READY branch=feature/270-x'; reply 2 'STUDIED issue=270 rows=0'; reply 3 'VERIFIED branch=feature/270-x'
reply 4 'STUDIED issue=270 rows=1'
reply 5 'A pull request for this branch already exists: https://github.com/o/r/pull/8'
chain 270
ok "a mentioned pull request is not a raised one" '[ "$RC" = 1 ] && closing | grep -q "no readable verdict" && ! last_line | grep -q "chain: done"'

# // SCENARIO: A stalled stage ends the run
echo "A stalled stage ends the run:"
new_case
echo 30 > "$STUB_DIR/sleep-1"
CHAIN_STAGE_TIMEOUT=1 chain 270
ok "exits 1" '[ "$RC" = 1 ]'
ok "no later stage started" '[ "$(calls)" = 1 ]'
ok "closing message names build as stalled" 'closing | grep -q build && closing | grep -q stalled'
ok "the stalled process was ended" '[ -s "$STUB_DIR/pid-1" ] && ! kill -0 "$(cat "$STUB_DIR/pid-1")" 2>/dev/null'

# // SCENARIO: Wrong starting point is refused
echo "Wrong starting point is refused:"
new_case
before="$(repo_state)"
chain
ok "no issue: exits 1 with usage, no stage started, nothing changed" '[ "$RC" = 1 ] && [ "$(calls)" = 0 ] && grep -q usage <<<"$OUTPUT" && [ "$(repo_state)" = "$before" ]'
new_case
before="$(repo_state)"
chain abc
ok "an issue that is not a number: exits 1 with usage, no stage started" '[ "$RC" = 1 ] && [ "$(calls)" = 0 ] && grep -q usage <<<"$OUTPUT" && [ "$(repo_state)" = "$before" ]'
new_case
touch "$REPO/stray.txt"
before="$(repo_state)"
chain 270
ok "dirty tree: exits 1 naming the dirty tree, no stage started, nothing changed" '[ "$RC" = 1 ] && [ "$(calls)" = 0 ] && grep -q "not clean" <<<"$OUTPUT" && [ "$(repo_state)" = "$before" ]'
new_case
git -C "$REPO" checkout -q -b feature/x
before="$(repo_state)"
chain 270
ok "not on main: exits 1 naming the branch, no stage started, nothing changed" '[ "$RC" = 1 ] && [ "$(calls)" = 0 ] && grep -q "feature/x" <<<"$OUTPUT" && grep -q "not main" <<<"$OUTPUT" && [ "$(repo_state)" = "$before" ]'
new_case
OUTPUT="$(cd "$SB" && PATH="$SB/bin:$PATH" bash "$CHAIN" 270 <"$SB/stdin-data" 2>&1)"; RC=$?
ok "outside a repository: refuses naming git, rather than reading it as a clean tree" '[ "$RC" = 1 ] && grep -q "could not read this repository" <<<"$OUTPUT" && [ "$(calls)" = 0 ]'

# // SCENARIO: Asked what it would do
echo "Asked what it would do:"
new_case
before="$(repo_state)"
chain --dry-run 270
ok "exits 0" '[ "$RC" = 0 ]'
ok "lists the five commands in run order" '[ "$(grep -oE "/(build|study|verify|raise-pr)( 270)?" <<<"$OUTPUT" | paste -sd,)" = "/build 270,/study 270,/verify,/study 270,/raise-pr 270" ]'
ok "no stage started" '[ "$(calls)" = 0 ]'
ok "branch, commits and working tree untouched" '[ "$(repo_state)" = "$before" ]'
new_case
touch "$REPO/stray.txt"
before="$(repo_state)"
chain --dry-run 270
ok "a dirty tree is still told what would run" '[ "$RC" = 0 ] && [ "$(calls)" = 0 ] && [ "$(repo_state)" = "$before" ]'
new_case
git -C "$REPO" checkout -q -b feature/x
before="$(repo_state)"
chain --dry-run 270
ok "a feature branch is still told what would run" '[ "$RC" = 0 ] && [ "$(calls)" = 0 ] && [ "$(repo_state)" = "$before" ]'

# // SCENARIO: A decorated failure verdict still stops the run
echo "A decorated failure verdict still stops the run:"
# The decoration a model has actually been seen to wrap a verdict in: a heading, bold, a bullet,
# a numbered item, backticks, a blockquote, a double space — each on the report's last line, which
# is the only line the chain reads. Each must stop the run AND yield the bare reason, so the
# assertion pins the whole closing line rather than just the exit code.
for decorated in \
  '## FAILED reason=suite red' \
  '**FAILED** reason=suite red' \
  '**FAILED reason=suite red**' \
  '- **FAILED reason=suite red**' \
  '1. FAILED reason=suite red' \
  '+ FAILED reason=suite red' \
  '`FAILED reason=suite red`' \
  '_FAILED reason=suite red_' \
  'FAILED  reason=suite red' \
  '  FAILED reason=suite red  ' \
  '> FAILED reason=suite red'
do
  new_case
  reply 1 "Report of the build stage."$'\n'"$decorated"
  chain 270
  ok "stops at build with the bare reason: $decorated" '[ "$RC" = 1 ] && [ "$(calls)" = 1 ] && closing | grep -qxF "chain: FAILED at [1/5] build — suite red"'
done
new_case
reply 1 'READY branch=feature/270-x'
reply 2 'STUDIED issue=270 rows=0'
reply 3 $'Nothing to verify.\n\nFAILED reason=no commits on this branch over main'
chain 270
ok "a /verify precondition stop reaches the chain as its own reason" '[ "$RC" = 1 ] && [ "$(calls)" = 3 ] && closing | grep -qF "no commits on this branch over main" && ! closing | grep -q "no readable verdict"'
new_case
reply 1 $'Something went wrong.\nFAILED reason='
chain 270
ok "a failure verdict with no reason still stops, and says the reason is missing" '[ "$RC" = 1 ] && [ "$(calls)" = 1 ] && closing | grep -qF "build — the stage reported a failure with no reason"'

# // SCENARIO: A failure report that mentions the success verdict is not a success
echo "A failure report that mentions the success verdict is not a success:"
new_case
reply 1 'READY branch=feature/270-x'
reply 2 'STUDIED issue=270 rows=0'
reply 3 $'The branch is not VERIFIED — branch=feature/270-x stays unverified.\n\n## FAILED reason=last review round found a material problem'
chain 270
ok "a decorated failure outranks prose that names the success verdict" '[ "$RC" = 1 ] && [ "$(calls)" = 3 ] && closing | grep -qF "last review round found a material problem"'

# // SCENARIO: A word that merely contains the success word is not a success verdict
echo "A word that merely contains the success word is not a success verdict:"
new_case
reply 1 'READY branch=feature/270-x'
reply 2 'STUDIED issue=270 rows=0'
reply 3 'The branch is UNVERIFIED branch=feature/270-x and a human should look.'
chain 270
ok "UNVERIFIED branch= is not the verify verdict" '[ "$RC" = 1 ] && [ "$(calls)" = 3 ] && closing | grep -q "no readable verdict"'
new_case
reply 1 'The branch is UNREADY branch=feature/270-x.'
chain 270
ok "UNREADY branch= is not the build verdict" '[ "$RC" = 1 ] && [ "$(calls)" = 1 ] && closing | grep -q "no readable verdict"'
new_case
reply 1 'READY branch=feature/270-x'
reply 2 'The issue is UNSTUDIED issue=270 so far.'
chain 270
ok "UNSTUDIED issue= is not the study verdict" '[ "$RC" = 1 ] && [ "$(calls)" = 2 ] && closing | grep -q "no readable verdict"'

# SCENARIO: A report that quotes a failure verdict does not stop a healthy stage
echo "A report that quotes a failure verdict does not stop a healthy stage:"
# Everything a report has been seen to show a failure verdict *inside*: a backtick fence, a tilde
# fence, an indented block, a bullet, a numbered item, a blockquote and a markdown table row
# (/study's native output). None of them is the report's last line, so none of them is the verdict.
new_case
reply 1 'RED evidence — the verdicts this stage can print:

```
FAILED reason=suite red
```

~~~
FAILED reason=the issue is unbuildable as written
~~~

    FAILED reason=an indented code block

- FAILED reason=a bullet
1. FAILED reason=a numbered item
> FAILED reason=a blockquote

| Verdict | Meaning |
|---|---|
| FAILED reason=<text> | the stage stopped |

READY branch=feature/270-x'
reply 2 'STUDIED issue=270 rows=0'
reply 3 'VERIFIED branch=feature/270-x'
reply 4 '| Reviewer: the chain read `FAILED reason=suite red` out of a code block | Execution | fixed |

STUDIED issue=270 rows=1'
reply 5 'RAISED pr=https://github.com/o/r/pull/9'
chain 270
ok "quoted failure verdicts in fences, lists, tables and quotations do not stop the run" '[ "$RC" = 0 ] && [ "$(calls)" = 5 ] && [ "$(last_line)" = "chain: done — https://github.com/o/r/pull/9" ]'
# The shape that made dropping fenced blocks before the scan unworkable (#333, reverted d2addd8):
# an unclosed fence hid every line beneath it, including a correctly written verdict.
new_case
reply 1 'RED evidence:

```
FAILED reason=suite red

READY branch=feature/270-x'
reply 2 'STUDIED issue=270 rows=0'; reply 3 'VERIFIED branch=feature/270-x'
reply 4 'STUDIED issue=270 rows=1'; reply 5 'RAISED pr=https://github.com/o/r/pull/9'
chain 270
ok "an unclosed fence above the verdict does not hide it" '[ "$RC" = 0 ] && [ "$(calls)" = 5 ]'

# SCENARIO: A failed stage never starts the next stage
echo "A failed stage never starts the next stage:"
new_case
reply 1 'READY branch=feature/270-x'; reply 2 'STUDIED issue=270 rows=0'
reply 3 $'Round four: the branch is VERIFIED branch=feature/270-x in every respect but one.\nFAILED reason=last review round found a material problem'
chain 270
ok "a failure verdict last is a failure however the report reads above it" '[ "$RC" = 1 ] && [ "$(calls)" = 3 ] && closing | grep -qxF "chain: FAILED at [3/5] verify — last review round found a material problem"'
# The four reports confirmed to have passed a failed /verify through to a raised pull request: each
# pairs a failure line the old scan missed with success-shaped prose the old scan matched. None of
# them is a readable verdict now, so each stops the run instead of starting /raise-pr.
for shaped in \
  $'Verdict: FAILED reason=suite red\n\nNot VERIFIED, branch=feature/270-x' \
  $'### 1. FAILED reason=suite red\n\nnot VERIFIED branch=feature/270-x' \
  $'FAILED: reason = suite red\n\nnot VERIFIED branch=feature/270-x' \
  $'Result: NOT-VERIFIED branch=feature/270-x'
do
  new_case
  reply 1 'READY branch=feature/270-x'; reply 2 'STUDIED issue=270 rows=0'
  reply 3 "$shaped"
  chain 270
  ok "no later stage starts after: ${shaped//$'\n'/; }" '[ "$RC" = 1 ] && [ "$(calls)" = 3 ] && ! grep -q "chain: done" <<<"$OUTPUT"'
done

# SCENARIO: A report with no clear verdict stops the run
echo "A report with no clear verdict stops the run:"
new_case
reply 1 $'Built it.\n\n```\nREADY branch=feature/270-x\n```'
chain 270
ok "a fenced verdict is not a verdict, and the closing line says the verdict could not be read" '[ "$RC" = 1 ] && [ "$(calls)" = 1 ] && closing | grep -qxF "chain: FAILED at [1/5] build — no readable verdict — the last line of the report is not a verdict"'
new_case
reply 1 $'READY branch=feature/270-x\n\nRun /verify next.'
chain 270
ok "a closing sentence after the verdict stops the run" '[ "$RC" = 1 ] && [ "$(calls)" = 1 ] && closing | grep -q "no readable verdict"'
new_case
reply 1 'Verdict: FAILED reason=suite red'
chain 270
ok "an unreadable verdict is reported distinctly from a stage that failed with a reason" '[ "$RC" = 1 ] && closing | grep -q "no readable verdict" && ! closing | grep -qF "suite red"'

# SCENARIO: The stated reason is the stage's reason
echo "The stated reason is the stage's reason:"
new_case
reply 1 $'Something went wrong.\nFAILED reason=   '
chain 270
ok "a whitespace-only reason is reported as missing, with no dangling dash" '[ "$RC" = 1 ] && closing | grep -qxF "chain: FAILED at [1/5] build — the stage reported a failure with no reason"'
new_case
reply 1 $'Formatting failed.\nFAILED reason=the tool that failed is `dotnet format`'
chain 270
ok "a reason that legitimately ends in a backtick keeps it" '[ "$RC" = 1 ] && closing | grep -qxF "chain: FAILED at [1/5] build — the tool that failed is \`dotnet format\`"'
new_case
reply 1 $'Built on Windows.\r\nFAILED reason=suite red\r'
chain 270
ok "a CRLF report leaves no carriage return in the reason" '[ "$RC" = 1 ] && closing | grep -qxF "chain: FAILED at [1/5] build — suite red"'
new_case
reply 1 $'Study table:\n| FAILED reason=suite red |'
chain 270
ok "a table-row verdict is unreadable rather than a reason with a stray pipe" '[ "$RC" = 1 ] && closing | grep -q "no readable verdict" && ! closing | grep -qF "|"'

# SCENARIO: A raised pull request is reported as raised
echo "A raised pull request is reported as raised:"
# Decoration on the last stage's verdict used to be the one kind tolerated nowhere, so a raised
# pull request was reported as a failure *after* it was already open.
for raised in \
  'RAISED pr=https://github.com/o/r/pull/9' \
  '**RAISED pr=https://github.com/o/r/pull/9**' \
  '**RAISED** pr=https://github.com/o/r/pull/9' \
  '- RAISED pr=https://github.com/o/r/pull/9' \
  '`RAISED pr=https://github.com/o/r/pull/9`' \
  '## RAISED pr=https://github.com/o/r/pull/9' \
  '> RAISED pr=https://github.com/o/r/pull/9'
do
  new_case
  reply 1 'READY branch=feature/270-x'; reply 2 'STUDIED issue=270 rows=0'; reply 3 'VERIFIED branch=feature/270-x'
  reply 4 'STUDIED issue=270 rows=1'
  reply 5 "An earlier attempt left https://github.com/o/r/pull/8 behind."$'\n'"$raised"
  chain 270
  ok "the run ends naming the pull request: $raised" '[ "$RC" = 0 ] && [ "$(last_line)" = "chain: done — https://github.com/o/r/pull/9" ]'
done

# SCENARIO: A stage killed from outside is not called a stall
echo "A stage killed from outside is not called a stall:"
new_case
touch "$STUB_DIR/kill-1"
chain 270
ok "exits 1 naming the external kill, not a stall" '[ "$RC" = 1 ] && [ "$(calls)" = 1 ] && closing | grep -qF "exit 137" && ! closing | grep -q stalled'
ok "no later stage started" '[ "$(calls)" = 1 ]'

# // SCENARIO: A stopped run says what state the branch is in
echo "A stopped run says what state the branch is in:"
new_case
cat > "$STUB_DIR/act-1" <<'ACT'
git checkout -q -b feature/270-x
printf 'one\n' > a.txt; git add a.txt; git commit -q -m "Refs #270: add the first thing"
printf 'two\n' > b.txt; git add b.txt; git commit -q -m "Refs #270: add the second thing"
printf 'wip\n' > c.txt
{ git rev-parse HEAD; git rev-parse --abbrev-ref HEAD; git status --porcelain; } > "$STUB_DIR/state-after-act"
ACT
reply 1 $'Built, then the suite went red.\nFAILED reason=suite red'
chain 270
ok "names the branch and how many commits it holds over main" 'grep -qF "feature/270-x holds 2 commit(s) over main" <<<"$OUTPUT"'
ok "lists those commits" 'grep -qF "add the first thing" <<<"$OUTPUT" && grep -qF "add the second thing" <<<"$OUTPUT"'
ok "says the working tree has uncommitted changes" 'grep -q "working tree has uncommitted changes" <<<"$OUTPUT"'
ok "the closing lines still sit at the tail, after the branch state" '[ "$RC" = 1 ] && closing | grep -qF "chain: FAILED at [1/5] build — suite red" && last_line | grep -q "claude --resume"'
ok "reporting the state changed nothing in the repository" '[ "$(repo_state)" = "$(cat "$STUB_DIR/state-after-act")" ]'
new_case
cat > "$STUB_DIR/act-1" <<'ACT'
git checkout -q -b feature/270-x
printf 'one\n' > a.txt; git add a.txt; git commit -q -m "Refs #270: add the first thing"
printf 'wip\n' > b.txt
ACT
echo 30 > "$STUB_DIR/sleep-1"
# A longer limit than the other stall cases: this stub does git work before it sleeps.
CHAIN_STAGE_TIMEOUT=3 chain 270
ok "a stage killed at its time limit still has its branch state reported" '[ "$RC" = 1 ] && closing | grep -q stalled && grep -qF "feature/270-x holds 1 commit(s) over main" <<<"$OUTPUT" && grep -q "working tree has uncommitted changes" <<<"$OUTPUT"'

# // SCENARIO: A stopped run with nothing left behind says so
echo "A stopped run with nothing left behind says so:"
new_case
reply 1 $'Could not build it.\nFAILED reason=issue unbuildable as written'
before="$(repo_state)"
chain 270
ok "says the branch holds no commits over main and the tree is clean" '[ "$RC" = 1 ] && grep -q "holds no commits over main" <<<"$OUTPUT" && grep -q "working tree is clean" <<<"$OUTPUT"'
ok "branch, commits and working tree untouched" '[ "$(repo_state)" = "$before" ]'

# // SCENARIO: A zero time limit is refused
echo "A zero time limit is refused:"
new_case
before="$(repo_state)"
CHAIN_STAGE_TIMEOUT=0 chain 270
ok "exits 1 naming the override, no stage started, nothing changed" '[ "$RC" = 1 ] && [ "$(calls)" = 0 ] && grep -q "CHAIN_STAGE_TIMEOUT" <<<"$OUTPUT" && [ "$(repo_state)" = "$before" ]'
new_case
before="$(repo_state)"
CHAIN_STAGE_TIMEOUT=0 chain --dry-run 270
ok "a zero override is still told what would run" '[ "$RC" = 0 ] && [ "$(calls)" = 0 ] && [ "$(repo_state)" = "$before" ]'
new_case
reply 1 'READY branch=feature/270-x'; reply 2 'STUDIED issue=270 rows=0'; reply 3 'VERIFIED branch=feature/270-x'
reply 4 'STUDIED issue=270 rows=1'; reply 5 'RAISED pr=https://github.com/o/r/pull/9'
CHAIN_STAGE_TIMEOUT=60 chain 270
ok "a non-zero override is still accepted" '[ "$RC" = 0 ] && [ "$(calls)" = 5 ]'
new_case
before="$(repo_state)"
CHAIN_STAGE_TIMEOUT=abc chain 270
ok "an override that is not a whole number is refused before any stage starts" '[ "$RC" = 1 ] && [ "$(calls)" = 0 ] && grep -q "CHAIN_STAGE_TIMEOUT" <<<"$OUTPUT" && [ "$(repo_state)" = "$before" ]'
new_case
reply 1 'READY branch=feature/270-x'; reply 2 'STUDIED issue=270 rows=0'; reply 3 'VERIFIED branch=feature/270-x'
reply 4 'STUDIED issue=270 rows=1'; reply 5 'RAISED pr=https://github.com/o/r/pull/9'
CHAIN_STAGE_TIMEOUT= chain 270
ok "an empty override is no override" '[ "$RC" = 0 ] && [ "$(calls)" = 5 ]'

# // SCENARIO: A stage that rewrites the chain script does not derail the run
# Bash reads a script from disk as it runs it, so a stage that edits chain.sh in place — any issue
# whose work is the chain itself — used to leave the run executing whatever text sat at its old
# position in the new file (#348, which parked #345 after a build that had succeeded). The case
# runs a copy, and its first stage overwrites that copy in place with nothing but `exit 97`.
echo "A stage that rewrites the chain script does not derail the run:"
new_case
cp "$CHAIN" "$SB/case$cases/chain.sh"
cat > "$STUB_DIR/act-1" <<ACT
for _ in \$(seq 1 4000); do echo 'exit 97'; done > "$SB/case$cases/chain.sh"
ACT
reply 1 'READY branch=feature/270-x'; reply 2 'STUDIED issue=270 rows=0'; reply 3 'VERIFIED branch=feature/270-x'
reply 4 'STUDIED issue=270 rows=1'; reply 5 'RAISED pr=https://github.com/o/r/pull/9'
CHAIN="$SB/case$cases/chain.sh" chain 270
ok "the run finishes on the script it started with" '[ "$RC" = 0 ] && [ "$(calls)" = 5 ] && [ "$(last_line)" = "chain: done — https://github.com/o/r/pull/9" ]'

echo ""
echo "RESULT: $pass passed, $fail failed"
[ "$fail" = 0 ]
