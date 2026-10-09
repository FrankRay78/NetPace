#!/usr/bin/env bash
#
# chain.tests.sh — standalone test matrix for chain.sh.
#
# The chain's one job is gating: which stage starts, in what order, and what happens when a stage
# fails. All of that is provable from outside with a stub `claude`
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
# instead — exec, so the recorded PID is the sleeping process itself. An act-<n> file is a script
# run in the case repo before call n replies, so a case can give the repository the branch,
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
# // SCENARIO: Chain runs build, verify and raise-pr with no study pass
echo "One issue to a pull request:"
new_case
reply 1 $'Built.\nREADY branch=feature/270-x\nDone.'
reply 2 'VERIFIED branch=feature/270-x'
reply 3 $'Related work: https://github.com/o/r/pull/1\nRAISED pr=https://github.com/o/r/pull/9'
CHAIN_MODEL=test-model chain 270
ok "exits 0" '[ "$RC" = 0 ]'
ok "exactly three stages started" '[ "$(calls)" = 3 ]'
ok "stages in order: build, verify, raise-pr" 'prompt_is 1 "/build 270" && prompt_is 2 "/verify" && prompt_is 3 "/raise-pr 270"'
ok "no stage is a study pass" '! grep -qF -- "/study" "$STUB_DIR/log"'
ok "every stage counts itself out of three" '[ "$(grep -c "chain: \\[[0-9]*/3\\]" <<<"$OUTPUT")" = 6 ] && ! grep -q "chain: \\[[0-9]*/5\\]" <<<"$OUTPUT"'
ok "every stage starts a fresh session" '! grep -qF -- "--resume|" "$STUB_DIR/log"'
ok "every stage uses the one chosen model" '[ "$(grep -cF -- "--model|test-model|" "$STUB_DIR/log" 2>/dev/null)" = 3 ]'
ok "no stage is asked to prompt for permission" '[ "$(grep -cF -- "--dangerously-skip-permissions|" "$STUB_DIR/log" 2>/dev/null)" = 3 ]'
ok "no stage can read the terminal" '[ ! -f "$STUB_DIR/stdin-leak" ]'
ok "the closing line names the pull request that was raised" 'last_line | grep -qF "chain: done — https://github.com/o/r/pull/9"'
new_case
reply 1 'READY branch=feature/270-x'; reply 2 'VERIFIED branch=feature/270-x'
reply 3 'RAISED pr=https://github.com/o/r/pull/9'
chain 270
ok "with no override every stage still names the default model" '[ "$RC" = 0 ] && [ "$(grep -cF -- "--model|claude-opus-5|" "$STUB_DIR/log")" = 3 ]'
new_case
reply 1 'READY branch=feature/270-x'
chain "#270"
ok "an issue written #270 is the same issue" 'prompt_is 1 "/build 270"'
# Regression (#242): /verify reported `## VERIFIED `branch=…``, and markdown landing between
# the verdict's two words parked a verified branch as a stage with no verdict.
new_case
reply 1 'READY `branch=feature/270-x`'; reply 2 '## VERIFIED `branch=feature/270-x`'
reply 3 'RAISED pr=https://github.com/o/r/pull/9'
chain 270
ok "markdown between a verdict's two words is still that verdict" '[ "$RC" = 0 ] && [ "$(calls)" = 3 ]'

# // SCENARIO: A failing stage stops the run
echo "A failing stage stops the run:"
new_case
reply 1 'READY branch=feature/270-x'
reply 2 $'Suite failed.\nFAILED reason=suite red'
before="$(repo_state)"
chain 270
ok "exits 1" '[ "$RC" = 1 ]'
ok "no stage after verify started" '[ "$(calls)" = 2 ]'
ok "closing message names verify, 2/3 and the reason" 'closing | grep -q verify && closing | grep -qF 2/3 && closing | grep -q "suite red"'
ok "the failure reason names no study pass" '! grep -qi study <<<"$OUTPUT"'
ok "closing message says how to reopen the session" 'closing | grep -q "claude --resume"'
ok "closing message names the session to reopen" 'closing | grep -qF "claude --resume sess-2"'
ok "branch, commits and working tree untouched" '[ "$(repo_state)" = "$before" ]'
new_case
reply 1 $'READY branch=feature/270-x\nFAILED reason=half built'
chain 270
ok "a FAILED verdict outranks a success verdict in the same report" '[ "$RC" = 1 ] && [ "$(calls)" = 1 ] && closing | grep -q "half built"'
new_case
reply 1 'READY branch=feature/270-x'
reply 2 $'VERIFIED branch=feature/270-x\nRound 2 noted that the build stage can print FAILED reason=<text> and stop.'
reply 3 'RAISED pr=https://github.com/o/r/pull/9'
chain 270
ok "a report that quotes 'FAILED reason=' mid-sentence does not stop the run" '[ "$RC" = 0 ] && [ "$(calls)" = 3 ]'
new_case
reply 1 'READY branch=feature/270-x'
printf 'claude: command failed before it could start' > "$STUB_DIR/reply-2.json"
chain 270
ok "a stage that fails before its reply is read names no earlier stage's session" '[ "$RC" = 1 ] && [ "$(calls)" = 2 ] && ! closing | grep -q "sess-1" && closing | grep -q "no session id was captured"'
new_case
reply 1 'READY branch=feature/270-x'
echo 30 > "$STUB_DIR/sleep-2"
CHAIN_STAGE_TIMEOUT=1 chain 270
ok "a stalled stage names no earlier stage's session either" '[ "$RC" = 1 ] && ! closing | grep -q "sess-1" && closing | grep -q "no session id was captured"'

# // SCENARIO: A stage with no readable verdict is a failure
echo "A stage with no readable verdict is a failure:"
new_case
reply 1 'I finished.'
chain 270
ok "exits 1" '[ "$RC" = 1 ]'
ok "no later stage started" '[ "$(calls)" = 1 ]'
ok "closing message names build and the missing verdict" 'closing | grep -q build && closing | grep -q "no recognisable verdict"'
new_case
printf 'claude: command failed before it could start' > "$STUB_DIR/reply-1.json"
chain 270
ok "a reply that is not JSON says so, and shows the reply" '[ "$RC" = 1 ] && closing | grep -q "not JSON" && grep -qF "command failed before it could start" <<<"$OUTPUT"'
new_case
jq -n '{type:"result",subtype:"error_during_execution",is_error:true,result:"API Error: 529 overloaded",session_id:"sess-1"}' > "$STUB_DIR/reply-1.json"
chain 270
ok "an errored reply is reported as the error, not as a missing verdict" '[ "$RC" = 1 ] && grep -q "529 overloaded" <<<"$OUTPUT" && ! closing | grep -q "no recognisable verdict"'
new_case
jq -n '{type:"result",subtype:"success",is_error:false,result:"READY branch=feature/270-x"}' > "$STUB_DIR/reply-1.json"
reply 2 'VERIFIED branch=feature/270-x'; reply 3 'RAISED pr=https://github.com/o/r/pull/9'
chain 270
ok "a reply with no session id does not stop the run — no later stage needs one" '[ "$RC" = 0 ] && [ "$(calls)" = 3 ]'
new_case
reply 1 'READY branch=feature/270-x'; reply 2 'VERIFIED branch=feature/270-x'
reply 3 'A pull request for this branch already exists: https://github.com/o/r/pull/8'
chain 270
ok "a mentioned pull request is not a raised one" '[ "$RC" = 1 ] && closing | grep -q "no recognisable verdict" && ! last_line | grep -q "chain: done"'

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
ok "lists the three commands in run order" '[ "$(grep -oE "/(build|study|verify|raise-pr)( 270)?" <<<"$OUTPUT" | paste -sd,)" = "/build 270,/verify,/raise-pr 270" ]'
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
# Every shape chain.sh's line-anchored failure scan used to miss, the one it matched while
# capturing the trailing markup as part of the reason, and the blockquote it always read, kept as
# a guard. Each must stop the run AND yield the bare reason, so the assertion pins the whole
# closing line rather than just the exit code.
for decorated in \
  '## FAILED reason=suite red' \
  '**FAILED** reason=suite red' \
  '**FAILED reason=suite red**' \
  '- **FAILED reason=suite red**' \
  '1. FAILED reason=suite red' \
  '+ FAILED reason=suite red' \
  '`FAILED reason=suite red`' \
  'FAILED  reason=suite red' \
  '> FAILED reason=suite red'
do
  new_case
  reply 1 "Report of the build stage."$'\n'"$decorated"
  chain 270
  ok "stops at build with the bare reason: $decorated" '[ "$RC" = 1 ] && [ "$(calls)" = 1 ] && closing | grep -qxF "chain: FAILED at [1/3] build — suite red"'
done
new_case
reply 1 'READY branch=feature/270-x'
reply 2 $'FAILED reason=no commits on this branch over main\n\nNothing to verify.'
chain 270
ok "a /verify precondition stop reaches the chain as its own reason" '[ "$RC" = 1 ] && [ "$(calls)" = 2 ] && closing | grep -qF "no commits on this branch over main" && ! closing | grep -q "no recognisable verdict"'
new_case
reply 1 $'Something went wrong.\nFAILED reason='
chain 270
ok "a failure verdict with no reason still stops, and says the reason is missing" '[ "$RC" = 1 ] && [ "$(calls)" = 1 ] && closing | grep -qF "build — the stage reported a failure with no reason"'

# // SCENARIO: A failure report that mentions the success verdict is not a success
echo "A failure report that mentions the success verdict is not a success:"
new_case
reply 1 'READY branch=feature/270-x'
reply 2 $'## FAILED reason=last review round found a material problem\n\nThe branch is not VERIFIED — branch=feature/270-x stays unverified.'
chain 270
ok "a decorated failure outranks prose that trips the success scan" '[ "$RC" = 1 ] && [ "$(calls)" = 2 ] && closing | grep -qF "last review round found a material problem"'

# // SCENARIO: A word that merely contains the success word is not a success verdict
echo "A word that merely contains the success word is not a success verdict:"
new_case
reply 1 'READY branch=feature/270-x'
reply 2 'The branch is UNVERIFIED branch=feature/270-x and a human should look.'
chain 270
ok "UNVERIFIED branch= is not the verify verdict" '[ "$RC" = 1 ] && [ "$(calls)" = 2 ] && closing | grep -q "no recognisable verdict"'
new_case
reply 1 'The branch is UNREADY branch=feature/270-x.'
chain 270
ok "UNREADY branch= is not the build verdict" '[ "$RC" = 1 ] && [ "$(calls)" = 1 ] && closing | grep -q "no recognisable verdict"'

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
ok "the closing lines still sit at the tail, after the branch state" '[ "$RC" = 1 ] && closing | grep -qF "chain: FAILED at [1/3] build — suite red" && last_line | grep -q "claude --resume"'
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
reply 1 'READY branch=feature/270-x'; reply 2 'VERIFIED branch=feature/270-x'
reply 3 'RAISED pr=https://github.com/o/r/pull/9'
CHAIN_STAGE_TIMEOUT=60 chain 270
ok "a non-zero override is still accepted" '[ "$RC" = 0 ] && [ "$(calls)" = 3 ]'
new_case
before="$(repo_state)"
CHAIN_STAGE_TIMEOUT=abc chain 270
ok "an override that is not a whole number is refused before any stage starts" '[ "$RC" = 1 ] && [ "$(calls)" = 0 ] && grep -q "CHAIN_STAGE_TIMEOUT" <<<"$OUTPUT" && [ "$(repo_state)" = "$before" ]'
new_case
reply 1 'READY branch=feature/270-x'; reply 2 'VERIFIED branch=feature/270-x'
reply 3 'RAISED pr=https://github.com/o/r/pull/9'
CHAIN_STAGE_TIMEOUT= chain 270
ok "an empty override is no override" '[ "$RC" = 0 ] && [ "$(calls)" = 3 ]'

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
reply 1 'READY branch=feature/270-x'; reply 2 'VERIFIED branch=feature/270-x'
reply 3 'RAISED pr=https://github.com/o/r/pull/9'
CHAIN="$SB/case$cases/chain.sh" chain 270
ok "the run finishes on the script it started with" '[ "$RC" = 0 ] && [ "$(calls)" = 3 ] && [ "$(last_line)" = "chain: done — https://github.com/o/r/pull/9" ]'

echo ""
echo "RESULT: $pass passed, $fail failed"
[ "$fail" = 0 ]
