#!/usr/bin/env bash
#
# chain-next.tests.sh — standalone test matrix for chain-next.sh.
#
# The runner's job is choosing: which issue it builds, from what starting point, and what it
# leaves behind when the chain fails. All of that is provable from outside with a stub `gh` first
# on PATH and a stub `scripts/chain.sh` committed to a throwaway origin, so no model is called,
# nothing reaches GitHub and nothing is spent. Every case runs the runner from inside its own
# dedicated clone under a mktemp sandbox — the shape the systemd unit runs it in — so your
# checkout is never touched. Exits non-zero on any failure. Run it after any edit to the runner.
#
# The stub chain stands in for chain.sh: it refuses to start anywhere but a clean main and,
# standing in for /build's stop on an uninspected earlier branch, fails at build when a
# feature/<N>-* branch is already present. That is what makes "the second run starts
# cleanly" and "an un-parked issue is built from scratch" observable: a runner that got the
# starting point wrong would see the stub refuse.
#
# The installed systemd timer and a real run against GitHub need the build machine, so they are
# checked by hand.
#
#   Usage:  scripts/chain-next.tests.sh

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RUNNER="$HERE/chain-next.sh"

SB="$(mktemp -d)"
trap 'kill $(jobs -p) 2>/dev/null; rm -rf "$SB"' EXIT
# A developer's ambient overrides would otherwise reach every case.
unset CHAIN_NEXT_CLONE CHAIN_NEXT_STATE_DIR

# The stub gh. One log line per invocation, arguments separated by `|`. Issues live in
# issues.json as [{number, state, labels:[names]}] and are filtered by whatever --state and
# --label the runner asks for, so a runner that forgot to ask would see closed or unready ones.
# Open pull requests live in prs.json; blockers in blocked-<n>.json. A gh-fail file makes every
# call fail, as an outage would; gh-fail-<command> fails only that command (gh-fail-pr, gh-fail-api). Label and comment writes are applied to the stub's own state,
# so a parked issue stays parked for the next firing.
mkdir -p "$SB/bin"
cat > "$SB/bin/gh" <<'STUB'
#!/usr/bin/env bash
{ for a in "$@"; do printf '%s|' "$a"; done; printf '\n'; } >> "$STUB_DIR/gh-log"
if [ -f "$STUB_DIR/gh-fail" ] || [ -f "$STUB_DIR/gh-fail-$1" ]; then echo "gh: HTTP 502: Bad Gateway" >&2; exit 1; fi
args=("$@")
opt() { local i; for ((i = 0; i < ${#args[@]}; i++)); do [ "${args[i]}" = "$1" ] && { printf '%s' "${args[i+1]}"; return; }; done; }
case "$1 $2" in
  "issue list")
    state=$(opt --state); label=$(opt --label)
    jq -c --arg s "$state" --arg l "$label" '
      map(select(($s == "" or $s == "all" or (.state | ascii_downcase) == $s)
        and ($l == "" or (.labels | index($l)))))
      | map({number, state, labels: (.labels | map({name: .}))})' "$STUB_DIR/issues.json" ;;
  "pr list")
    if [ -f "$STUB_DIR/prs.json" ]; then cat "$STUB_DIR/prs.json"; else echo '[]'; fi ;;
  "issue edit")
    n=$3; add=$(opt --add-label)
    jq --argjson n "$n" --arg l "$add" 'map(if .number == $n then .labels += [$l] else . end)' \
      "$STUB_DIR/issues.json" > "$STUB_DIR/issues.tmp" && mv "$STUB_DIR/issues.tmp" "$STUB_DIR/issues.json" ;;
  "issue comment")
    printf '%s' "$(opt --body)" > "$STUB_DIR/comment-$3.txt" ;;
  *)
    if [ "$1" = api ]; then
      for a in "$@"; do
        if [[ "$a" =~ issues/([0-9]+)/dependencies/blocked_by ]]; then
          f="$STUB_DIR/blocked-${BASH_REMATCH[1]}.json"
          if [ -f "$f" ]; then cat "$f"; else echo '[]'; fi
          exit 0
        fi
      done
    fi
    echo "stub gh: unexpected call: $*" >&2; exit 1 ;;
esac
STUB
chmod +x "$SB/bin/gh"
# chain.sh needs claude; the stub chain never calls it, but the runner checks it is there.
printf '#!/bin/sh\nexit 0\n' > "$SB/bin/claude"
chmod +x "$SB/bin/claude"

# The stub chain, committed to each case's origin as scripts/chain.sh so the runner meets it the
# way it meets the real one: as whatever main holds after the reset. It records the starting
# point it was handed, then behaves like a chain run: a feature branch, a commit, and either a
# pushed branch with a linked pull request or a FAILED closing line.
cat > "$SB/stub-chain.sh" <<'STUB'
#!/usr/bin/env bash
n=$1
echo "$n" >> "$STUB_DIR/chain-log"
k=$(grep -c '' "$STUB_DIR/chain-log")
{ echo "branch=$(git rev-parse --abbrev-ref HEAD)"; echo "head=$(git rev-parse HEAD)"; echo "status=$(git status --porcelain --ignored | paste -sd,)"; } > "$STUB_DIR/start-$k"
if [ -f "$STUB_DIR/chain-sleep" ]; then sleep "$(cat "$STUB_DIR/chain-sleep")"; fi
if [ -n "$(git status --porcelain)" ] || [ "$(git rev-parse --abbrev-ref HEAD)" != main ]; then
  echo "chain: refused — not a clean main"; exit 1
fi
if [ -n "$(git for-each-ref --format='%(refname)' "refs/heads/feature/$n-*")" ]; then
  echo "chain: [1/3] build — starting"
  echo "chain: FAILED at [1/3] build — feature/$n-work already exists"; exit 1
fi
git checkout -q -b "feature/$n-work"
git commit -q --allow-empty -m "Refs #$n: stub work"
mkdir -p obj && echo build-output > obj/out.txt && echo half-written > "wip-$n.txt"
echo "chain: [1/3] build — ok"
if [ -f "$STUB_DIR/push-then-fail-$n" ]; then
  git push -q -u origin "feature/$n-work"
  echo "chain: FAILED at [3/3] raise-pr — gh pr create failed"; exit 1
fi
if [ -f "$STUB_DIR/fail-$n" ]; then
  echo "chain: FAILED at [2/3] verify — suite red"
  echo "chain: no later stage ran; reopen that stage with \`claude --resume sess-3\`."
  exit 1
fi
git push -q -u origin "feature/$n-work"
prs="$STUB_DIR/prs.json"; [ -f "$prs" ] || echo '[]' > "$prs"
jq --argjson n "$n" '. += [{number: (100 + $n), headRefName: "feature/\($n)-work", closingIssuesReferences: [{number: $n}]}]' \
  "$prs" > "$prs.tmp" && mv "$prs.tmp" "$prs"
echo "chain: done — https://github.com/o/r/pull/$((100 + n))"
STUB

pass=0; fail=0; cases=0
ok() { if eval "$2"; then echo "  ok   $1"; pass=$((pass+1)); else echo "  FAIL $1 -- rc=$RC output:[$OUTPUT]"; fail=$((fail+1)); fi; }

# commit_all <repo> <message> — commit every change as the test identity.
commit_all() { git -C "$1" add -A && git -C "$1" -c user.name=t -c user.email=t@example.invalid -c commit.gpgsign=false commit -q -m "$2"; }

# A fresh case: a bare origin whose main carries the runner under test and the stub chain; the
# runner's dedicated clone of it, marked as such; an interactive clone that must never change;
# and empty stub and state directories.
new_case() {
  cases=$((cases+1))
  local d="$SB/case$cases"
  export STUB_DIR="$d/stub"
  ORIGIN="$d/origin.git"; SEED="$d/seed"; CLONE="$d/runner"; OTHER="$d/interactive"; STATE="$d/state"
  mkdir -p "$STUB_DIR" "$SEED/scripts"
  echo '[]' > "$STUB_DIR/issues.json"
  git init -q --bare -b main "$ORIGIN"
  git init -q -b main "$SEED"
  git -C "$SEED" remote add origin "$ORIGIN"
  cp "$RUNNER" "$SEED/scripts/chain-next.sh" 2>/dev/null
  cp "$SB/stub-chain.sh" "$SEED/scripts/chain.sh"
  printf 'obj/\n' > "$SEED/.gitignore"
  commit_all "$SEED" init
  git -C "$SEED" push -q origin main
  git clone -q "$ORIGIN" "$CLONE"
  git -C "$CLONE" config user.name chain-next-tests
  git -C "$CLONE" config user.email chain-next-tests@example.invalid
  git -C "$CLONE" config commit.gpgsign false
  git -C "$CLONE" config chain-next.dedicated true
  git clone -q "$ORIGIN" "$OTHER"
}

# issues <json> — the repository's issues, as [{number, state, labels}].
issues() { printf '%s' "$1" > "$STUB_DIR/issues.json"; }
unpark() { jq --argjson n "$1" 'map(if .number == $n then .labels -= ["parked"] else . end)' "$STUB_DIR/issues.json" > "$STUB_DIR/i.tmp" && mv "$STUB_DIR/i.tmp" "$STUB_DIR/issues.json"; }

# fire [args…] — one firing of the runner, run as the systemd unit runs it: the clone's own copy,
# from outside the clone. Sets OUTPUT (stdout and stderr) and RC, which every assertion reads.
fire() {
  OUTPUT="$(cd "$SB" && CHAIN_NEXT_CLONE="$CLONE" CHAIN_NEXT_STATE_DIR="$STATE" PATH="$SB/bin:$PATH" bash "$CLONE/scripts/chain-next.sh" "$@" </dev/null 2>&1)"; RC=$?
}

chain_calls() { if [ -f "$STUB_DIR/chain-log" ]; then paste -sd, "$STUB_DIR/chain-log"; fi; }
gh_writes() { { grep -E '^issue\|(edit|comment)\|' "$STUB_DIR/gh-log" 2>/dev/null || true; } | grep -c ''; }
start_of() { grep "^$2=" "$STUB_DIR/start-$1" | cut -d= -f2-; }
# Everything about a checkout a firing could change: commit, branch, tree (ignored files too),
# and every local branch.
checkout_state() { git -C "$1" rev-parse HEAD; git -C "$1" rev-parse --abbrev-ref HEAD; git -C "$1" status --porcelain --ignored; git -C "$1" for-each-ref --format='%(refname) %(objectname)' refs/heads; }
labels_of() { jq -r --argjson n "$1" '.[] | select(.number == $n) | .labels | join(",")' "$STUB_DIR/issues.json"; }
origin_main() { git -C "$ORIGIN" rev-parse main; }

# // SCENARIO: Asked what it would pick
echo "Asked what it would pick:"
new_case
issues '[{"number":2,"state":"CLOSED","labels":["ready"]},{"number":3,"state":"OPEN","labels":[]},{"number":7,"state":"OPEN","labels":["ready"]},{"number":5,"state":"OPEN","labels":["ready"]}]'
git -C "$CLONE" checkout -q -b feature/1-left-behind
echo stray > "$CLONE/stray.txt"
before="$(checkout_state "$CLONE")"
fire --dry-run
ok "exits 0" '[ "$RC" = 0 ]'
ok "names the lowest-numbered eligible issue" 'grep -qF "#5" <<<"$OUTPUT" && ! grep -qF "#7" <<<"$OUTPUT"'
ok "no chain started" '[ -z "$(chain_calls)" ]'
ok "no label or comment written" '[ "$(gh_writes)" = 0 ]'
ok "the dedicated clone is not reset, even from a feature branch with a dirty tree" '[ "$(checkout_state "$CLONE")" = "$before" ]'
ok "no state written: no lock, no log" '[ ! -e "$STATE" ]'
new_case
issues '[{"number":3,"state":"OPEN","labels":[]}]'
fire --dry-run
ok "with nothing eligible, says so and exits 0" '[ "$RC" = 0 ] && grep -qi "nothing" <<<"$OUTPUT" && [ -z "$(chain_calls)" ]'

# // SCENARIO: Blocked and already-raised issues are skipped
echo "Blocked and already-raised issues are skipped:"
new_case
issues '[{"number":1,"state":"CLOSED","labels":["ready"]},{"number":2,"state":"OPEN","labels":["bug"]},{"number":3,"state":"OPEN","labels":["ready","parked"]},{"number":4,"state":"OPEN","labels":["ready"]},{"number":5,"state":"OPEN","labels":["ready"]},{"number":6,"state":"OPEN","labels":["ready"]},{"number":8,"state":"OPEN","labels":["ready"]},{"number":9,"state":"OPEN","labels":["ready"]}]'
echo '[{"number":40,"state":"open"}]' > "$STUB_DIR/blocked-4.json"
echo '[{"number":50,"headRefName":"some-other-name","closingIssuesReferences":[{"number":5}]},{"number":60,"headRefName":"feature/6-thing","closingIssuesReferences":[]}]' > "$STUB_DIR/prs.json"
echo '[{"number":41,"state":"closed"}]' > "$STUB_DIR/blocked-8.json"
fire --dry-run
ok "closed, not-ready, parked, blocked and already-raised issues are passed over" '[ "$RC" = 0 ] && grep -qF "#8" <<<"$OUTPUT"'
fire
ok "and a real firing builds the first one that is none of those" '[ "$RC" = 0 ] && [ "$(chain_calls)" = 8 ]'
new_case
issues '[{"number":4,"state":"OPEN","labels":["ready"]}]'
echo '[{"number":40,"state":"open"}]' > "$STUB_DIR/blocked-4.json"
fire
ok "an issue whose only blocker is open is not built" '[ "$RC" = 0 ] && [ -z "$(chain_calls)" ]'
echo '[{"number":40,"state":"closed"}]' > "$STUB_DIR/blocked-4.json"
fire
ok "and is picked up once its blocker closes, with nothing restarted" '[ "$RC" = 0 ] && [ "$(chain_calls)" = 4 ]'

# // SCENARIO: A ready issue becomes a pull request
echo "A ready issue becomes a pull request:"
new_case
issues '[{"number":12,"state":"OPEN","labels":["ready"]}]'
# What an earlier run could leave behind: a feature branch checked out, a dirty tree, build output.
git -C "$CLONE" checkout -q -b feature/1-left-behind
echo stray > "$CLONE/stray.txt"
mkdir -p "$CLONE/obj" && echo stale > "$CLONE/obj/stale.txt"
# main has moved on since the clone was made.
echo newer > "$SEED/newer.txt"; commit_all "$SEED" newer; git -C "$SEED" push -q origin main
other_before="$(checkout_state "$OTHER")"
fire
ok "exits 0" '[ "$RC" = 0 ]'
ok "the chain ran once, against the issue" '[ "$(chain_calls)" = 12 ]'
ok "it started on main" '[ "$(start_of 1 branch)" = main ]'
ok "at the latest main" '[ "$(start_of 1 head)" = "$(origin_main)" ]'
ok "with nothing left of the earlier run: no change, no stray file, no build output" '[ -z "$(start_of 1 status)" ]'
ok "the issue ends with an open pull request linked to it" '[ "$(jq "[.[] | select(.closingIssuesReferences[].number == 12)] | length" "$STUB_DIR/prs.json")" = 1 ]'
ok "the closing line names the pull request" 'grep -qF "https://github.com/o/r/pull/112" <<<"$OUTPUT"'
ok "the issue is not parked" '[ "$(labels_of 12)" = ready ] && [ "$(gh_writes)" = 0 ]'
ok "the chain's output is kept in a per-issue log outside the clone" 'ls "$STATE"/logs/12-*.log >/dev/null 2>&1 && grep -qF "chain: done" "$STATE"/logs/12-*.log'
ok "no other checkout changed" '[ "$(checkout_state "$OTHER")" = "$other_before" ]'
new_case
issues '[{"number":12,"state":"OPEN","labels":["ready"]}]'
# The runner's own source changes on main: the reset rewrites the file it is running from.
{ head -n 1 "$RUNNER"; printf '# %s\n' $(seq 1 400); tail -n +2 "$RUNNER"; } > "$SEED/scripts/chain-next.sh"
commit_all "$SEED" "runner changed"; git -C "$SEED" push -q origin main
fire
ok "a reset that rewrites the runner's own source does not derail the firing" '[ "$RC" = 0 ] && [ "$(chain_calls)" = 12 ] && grep -qF "pull/112" <<<"$OUTPUT"'
new_case
issues '[{"number":12,"state":"OPEN","labels":["ready"]}]'
# A branch merged and deleted on GitHub, and a kept earlier attempt with no upstream at all.
git -C "$CLONE" checkout -q -b feature/3-merged
git -C "$CLONE" commit -q --allow-empty -m merged
git -C "$CLONE" push -q -u origin feature/3-merged
git -C "$CLONE" branch -q attempt/4-20260101T000000Z
git -C "$CLONE" checkout -q main
git -C "$ORIGIN" branch -D feature/3-merged >/dev/null
fire
ok "local branches whose upstream is gone are deleted" '[ "$RC" = 0 ] && ! git -C "$CLONE" rev-parse -q --verify refs/heads/feature/3-merged >/dev/null'
ok "kept attempt branches are not" 'git -C "$CLONE" rev-parse -q --verify refs/heads/attempt/4-20260101T000000Z >/dev/null'

# // SCENARIO: A failing issue is parked and not retried
echo "A failing issue is parked and not retried:"
new_case
issues '[{"number":8,"state":"OPEN","labels":["ready"]}]'
touch "$STUB_DIR/fail-8"
fire
ok "exits non-zero" '[ "$RC" != 0 ]'
ok "the issue is marked parked" '[ "$(labels_of 8)" = "ready,parked" ]'
ok "the comment names the failed stage and its reason" 'grep -qF "verify" "$STUB_DIR/comment-8.txt" && grep -qF "2/3" "$STUB_DIR/comment-8.txt" && grep -qF "suite red" "$STUB_DIR/comment-8.txt"'
log=$(ls "$STATE"/logs/8-*.log 2>/dev/null | head -n 1)
ok "the comment names the log, which holds the chain's full output" '[ -n "$log" ] && grep -qF "$log" "$STUB_DIR/comment-8.txt" && grep -qF "claude --resume sess-3" "$log"'
kept=$(git -C "$CLONE" for-each-ref --format='%(refname:short)' 'refs/heads/attempt/8-*')
ok "the attempt's work is kept on an attempt branch, which the comment names" '[ -n "$kept" ] && grep -qF "$kept" "$STUB_DIR/comment-8.txt" && [ "$(git -C "$CLONE" log -1 --format=%s "$kept")" = "Refs #8: stub work" ]'
ok "no feature branch for the issue is left behind" '[ -z "$(git -C "$CLONE" for-each-ref "refs/heads/feature/8-*")" ]'
ok "nothing is pushed" '[ -z "$(git -C "$ORIGIN" for-each-ref refs/heads/attempt refs/heads/feature)" ]'
fire
ok "a later firing does not select it" '[ "$RC" = 0 ] && [ "$(chain_calls)" = 8 ]'
unpark 8; rm "$STUB_DIR/fail-8"
fire
ok "once un-parked it is picked up, with nothing restarted" '[ "$RC" = 0 ] && [ "$(chain_calls)" = 8,8 ]'
ok "and built from scratch on a clean main" '[ "$(start_of 2 branch)" = main ] && [ -z "$(start_of 2 status)" ] && grep -qF "pull/108" <<<"$OUTPUT"'
ok "the earlier attempt's work is still there" 'git -C "$CLONE" rev-parse -q --verify "refs/heads/$kept" >/dev/null'
ok "the retry has a log of its own; the first is kept" '[ "$(ls "$STATE"/logs/8-*.log | grep -c "")" = 2 ] && [ -f "$log" ]'
new_case
issues '[{"number":8,"state":"OPEN","labels":["ready"]}]'
touch "$STUB_DIR/push-then-fail-8"
fire
ok "a branch pushed without a pull request is named in the comment and left on GitHub" '[ "$RC" != 0 ] && grep -qF "feature/8-work" "$STUB_DIR/comment-8.txt" && git -C "$ORIGIN" rev-parse -q --verify refs/heads/feature/8-work >/dev/null'
kept=$(git -C "$CLONE" for-each-ref --format='%(refname:short)' 'refs/heads/attempt/8-*')
# As the comment advises, someone removes the pushed branch by hand; then another issue is built.
git -C "$ORIGIN" branch -D feature/8-work >/dev/null
issues '[{"number":8,"state":"OPEN","labels":["ready","parked"]},{"number":9,"state":"OPEN","labels":["ready"]}]'
fire
ok "the kept attempt survives its pushed branch being removed from GitHub" '[ "$RC" = 0 ] && [ -n "$kept" ] && git -C "$CLONE" rev-parse -q --verify "refs/heads/$kept" >/dev/null'
new_case
issues '[{"number":8,"state":"OPEN","labels":["ready"]}]'
# A leftover branch that cannot be set aside: a branch named `attempt` blocks every attempt/* name.
git -C "$CLONE" branch -q feature/8-work
git -C "$CLONE" branch -q attempt
fire
ok "a leftover branch that cannot be set aside stops the firing before any chain, parking nothing" '[ "$RC" != 0 ] && [ -z "$(chain_calls)" ] && [ "$(labels_of 8)" = ready ]'
new_case
issues '[{"number":8,"state":"OPEN","labels":["ready"]}]'
# A run killed mid-way (a reboot, a stopped service) leaves its branch but parks nothing.
git -C "$CLONE" checkout -q -b feature/8-work
git -C "$CLONE" commit -q --allow-empty -m "killed mid-run"
fire
ok "an earlier run's leftover branch does not stop the next run" '[ "$RC" = 0 ] && grep -qF "pull/108" <<<"$OUTPUT" && [ -n "$(git -C "$CLONE" for-each-ref "refs/heads/attempt/8-*")" ]'

# // SCENARIO: Overlapping triggers do not start a second run
echo "Overlapping triggers do not start a second run:"
new_case
issues '[{"number":20,"state":"OPEN","labels":["ready"]},{"number":21,"state":"OPEN","labels":["ready"]}]'
echo 3 > "$STUB_DIR/chain-sleep"
( cd "$SB" && CHAIN_NEXT_CLONE="$CLONE" CHAIN_NEXT_STATE_DIR="$STATE" PATH="$SB/bin:$PATH" bash "$CLONE/scripts/chain-next.sh" </dev/null > "$SB/first.out" 2>&1; echo $? > "$SB/first.rc" ) &
first=$!
for _ in $(seq 1 50); do [ -f "$STUB_DIR/chain-log" ] && break; sleep 0.1; done
during="$(checkout_state "$CLONE")"
fire
ok "the second firing exits 0" '[ "$RC" = 0 ]'
ok "and starts no chain" '[ "$(chain_calls)" = 20 ]'
ok "and leaves the in-progress run's checkout alone" '[ "$(checkout_state "$CLONE")" = "$during" ]'
wait "$first"
ok "the in-progress run finishes unaffected" '[ "$(cat "$SB/first.rc")" = 0 ] && grep -qF "pull/120" "$SB/first.out"'

# // SCENARIO: Back to back without human help
echo "Back to back without human help:"
new_case
issues '[{"number":11,"state":"OPEN","labels":["ready"]},{"number":10,"state":"OPEN","labels":["ready"]}]'
fire
ok "the first firing builds the lower-numbered issue" '[ "$RC" = 0 ] && [ "$(chain_calls)" = 10 ]'
ok "and leaves its feature branch checked out" '[ "$(git -C "$CLONE" rev-parse --abbrev-ref HEAD)" = feature/10-work ]'
fire
ok "the second firing builds the other" '[ "$RC" = 0 ] && [ "$(chain_calls)" = 10,11 ]'
ok "starting cleanly on main" '[ "$(start_of 2 branch)" = main ] && [ -z "$(start_of 2 status)" ] && [ "$(start_of 2 head)" = "$(origin_main)" ]'
ok "both issues end with a pull request" '[ "$(jq length "$STUB_DIR/prs.json")" = 2 ]'
fire
ok "a third firing finds nothing left and exits 0" '[ "$RC" = 0 ] && [ "$(chain_calls)" = 10,11 ]'

echo "Nothing to do is not an error:"
new_case
issues '[{"number":3,"state":"OPEN","labels":["ready","parked"]}]'
git -C "$CLONE" checkout -q -b feature/1-left-behind
echo stray > "$CLONE/stray.txt"
before="$(checkout_state "$CLONE")"
fire
ok "exits 0, no chain" '[ "$RC" = 0 ] && [ -z "$(chain_calls)" ]'
ok "changes nothing: no reset, no label, no comment" '[ "$(checkout_state "$CLONE")" = "$before" ] && [ "$(gh_writes)" = 0 ]'

echo "A failed GitHub query selects nothing:"
new_case
issues '[{"number":12,"state":"OPEN","labels":["ready"]}]'
touch "$STUB_DIR/gh-fail"
before="$(checkout_state "$CLONE")"
fire
ok "exits non-zero and says why" '[ "$RC" != 0 ] && grep -qF "502" <<<"$OUTPUT"'
ok "no chain, no reset" '[ -z "$(chain_calls)" ] && [ "$(checkout_state "$CLONE")" = "$before" ]'
new_case
issues '[{"number":12,"state":"OPEN","labels":["ready"]}]'
printf 'not json' > "$STUB_DIR/blocked-12.json"
fire
ok "an unreadable blocker list selects nothing rather than assuming unblocked" '[ "$RC" != 0 ] && [ -z "$(chain_calls)" ]'
new_case
issues '[{"number":12,"state":"OPEN","labels":["ready"]}]'
touch "$STUB_DIR/gh-fail-api"
fire
ok "a failed blocker lookup selects nothing rather than assuming unblocked" '[ "$RC" != 0 ] && [ -z "$(chain_calls)" ]'
new_case
issues '[{"number":12,"state":"OPEN","labels":["ready"]}]'
echo '[{"number":112,"headRefName":"feature/12-work","closingIssuesReferences":[{"number":12}]}]' > "$STUB_DIR/prs.json"
touch "$STUB_DIR/gh-fail-pr"
fire
ok "a failed pull request lookup selects nothing rather than assuming none is open" '[ "$RC" != 0 ] && [ -z "$(chain_calls)" ]'

echo "A machine fault is not blamed on the issue:"
new_case
issues '[{"number":12,"state":"OPEN","labels":["ready"]}]'
mv "$ORIGIN" "$ORIGIN.away"
fire
ok "an unreachable origin starts no chain and parks nothing" '[ "$RC" != 0 ] && [ -z "$(chain_calls)" ] && [ "$(labels_of 12)" = ready ]'
mv "$ORIGIN.away" "$ORIGIN"
fire
ok "and the next firing builds the issue" '[ "$RC" = 0 ] && [ "$(chain_calls)" = 12 ]'
issues '[{"number":12,"state":"OPEN","labels":["ready"]},{"number":13,"state":"OPEN","labels":["ready"]}]'
# Every PATH entry that holds a claude, dropped, with the stub gh kept.
mkdir -p "$SB/noclaude" && ln -sf "$SB/bin/gh" "$SB/noclaude/gh"
noclaude="$SB/noclaude:$(tr : '\n' <<<"$PATH" | while read -r d; do [ -x "$d/claude" ] || printf '%s:' "$d"; done)"
OUTPUT="$(cd "$SB" && CHAIN_NEXT_CLONE="$CLONE" CHAIN_NEXT_STATE_DIR="$STATE" PATH="$noclaude" bash "$CLONE/scripts/chain-next.sh" </dev/null 2>&1)"; RC=$?
ok "claude missing from PATH starts no chain, parks nothing and says so" '[ "$RC" != 0 ] && grep -qF claude <<<"$OUTPUT" && [ "$(chain_calls)" = 12 ] && [ "$(labels_of 13)" = ready ]'

echo "Only its own checkout is ever touched:"
new_case
issues '[{"number":12,"state":"OPEN","labels":["ready"]}]'
git -C "$OTHER" checkout -q -b feature/2-mine
echo mine > "$OTHER/mine.txt"
other_before="$(checkout_state "$OTHER")"
OUTPUT="$(cd "$OTHER" && CHAIN_NEXT_CLONE="$OTHER" CHAIN_NEXT_STATE_DIR="$STATE" PATH="$SB/bin:$PATH" bash "$CLONE/scripts/chain-next.sh" </dev/null 2>&1)"; RC=$?
ok "a checkout not marked as the runner's is refused" '[ "$RC" != 0 ] && grep -qF "$OTHER" <<<"$OUTPUT" && [ -z "$(chain_calls)" ]'
ok "and left exactly as it was" '[ "$(checkout_state "$OTHER")" = "$other_before" ]'
git -C "$CLONE" worktree add -q "$SB/case$cases/wt" -b wt-branch
OUTPUT="$(cd "$SB" && CHAIN_NEXT_CLONE="$SB/case$cases/wt" CHAIN_NEXT_STATE_DIR="$STATE" PATH="$SB/bin:$PATH" bash "$CLONE/scripts/chain-next.sh" </dev/null 2>&1)"; RC=$?
ok "a worktree of the runner's clone is refused too" '[ "$RC" != 0 ] && [ -z "$(chain_calls)" ]'
fire
ok "while a run in its own clone leaves the other checkout alone" '[ "$RC" = 0 ] && [ "$(chain_calls)" = 12 ] && [ "$(checkout_state "$OTHER")" = "$other_before" ]'

echo "Wrong invocation is refused:"
new_case
fire --bogus
ok "an unknown argument exits 1 with usage, starting nothing" '[ "$RC" = 1 ] && grep -q usage <<<"$OUTPUT" && [ -z "$(chain_calls)" ]'

echo ""
echo "RESULT: $pass passed, $fail failed"
[ "$fail" = 0 ]
