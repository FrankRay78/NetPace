#!/usr/bin/env bash
#
# pr-cost-footer.tests.sh — standalone test matrix for pr-cost-footer.sh.
#
# The script's job is deciding: whether this run posted a review worth recording, which row the
# round is, what the run cost, and what the rewritten pull request description must say. All of
# that is provable from outside with a stub `gh` first on PATH, so nothing reaches GitHub, no
# description is really edited and nothing is spent. Exits non-zero on any failure. Run it after
# any edit to the script.
#
# The stub holds one pull request: its description in body.md and its head commit in head, the
# reviews GitHub would list in reviews.json. A `gh pr edit` write is applied to body.md, so what
# the script did is read back from the description it changed rather than from its own output.
# The stub runs the script's own `--jq` filter against reviews.json rather than one of its own, so
# the filter deciding which reviews carry the marker is the one under test; the head-commit half of
# that decision is the script's own whole-line match on the filter's output. A gh-fail file makes
# every call fail, as an outage would; gh-fail-edit fails only the write, and gh-bad-json answers
# with something unparseable.
#
# What this matrix cannot reach is the trigger and the plumbing around it: that the
# `review-followup` label starts a run, that a fork's pull request does not, that the action's
# execution_file lands where the step passes it, and that a first review and a follow-up never
# write the footer at the same time. The tool that decides all four is GitHub Actions
# (Constitution §I, configuration/tooling), so the real workflow run is their RED step.
#
#   Usage:  scripts/pr-cost-footer.tests.sh

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT="$HERE/pr-cost-footer.sh"

SB="$(mktemp -d)"
trap 'rm -rf "$SB"' EXIT

mkdir -p "$SB/bin"
cat > "$SB/bin/gh" <<'STUB'
#!/usr/bin/env bash
{ for a in "$@"; do printf '%s|' "$a"; done; printf '\n'; } >> "$STUB_DIR/gh-log"
if [ -f "$STUB_DIR/gh-fail" ]; then echo "gh: HTTP 502: Bad Gateway" >&2; exit 1; fi
args=("$@")
opt() { local i; for ((i = 0; i < ${#args[@]}; i++)); do [ "${args[i]}" = "$1" ] && printf '%s\n' "${args[i+1]}"; done; }
case "$1 $2" in
  "pr view")
    [ -f "$STUB_DIR/gh-bad-json" ] && { printf 'not json at all\n'; exit 0; }
    jq -n --rawfile b "$STUB_DIR/body.md" --arg h "$(cat "$STUB_DIR/head")" '{body: $b, headRefOid: $h}' ;;
  "api --paginate")
    jq -r "$(opt --jq)" "$STUB_DIR/reviews.json" ;;
  "pr edit")
    [ -f "$STUB_DIR/gh-fail-edit" ] && { echo "gh: could not edit pull request" >&2; exit 1; }
    cp "$(opt --body-file)" "$STUB_DIR/body.md" ;;
  *)
    echo "stub gh: unexpected call: $*" >&2; exit 1 ;;
esac
STUB
chmod +x "$SB/bin/gh"

pass=0; fail=0; cases=0
ok() { if eval "$2"; then echo "  ok   $1"; pass=$((pass+1)); else echo "  FAIL $1 -- rc=$RC output:[$OUTPUT]"; fail=$((fail+1)); fi; }

# body <text> — the pull request description, as the author and earlier rounds left it.
body() { printf '%s' "$1" > "$STUB_DIR/body.md"; }
# reviews <json> — the reviews GitHub would list, as [{commit_id, body}].
reviews() { printf '%s' "$1" > "$STUB_DIR/reviews.json"; }
# reviewed — one marked review against the head commit, which is what a posting round leaves.
reviewed() { reviews '[{"commit_id":"HEADSHA","body":"Round summary\n<!-- review-pr -->\n"}]'; }

# usage <tokens-json> <cost-json> — the case's execution file, shaped as the action writes one:
# a message list whose final result message carries the run's totals.
usage() {
  jq -n --argjson u "$1" --argjson c "$2" \
    '[{type: "system", subtype: "init"},
      {type: "assistant"},
      ({type: "result", subtype: "success"} + $u + $c)]' > "$STUB_DIR/exec.json"
}
# The default run: one model, cache tokens included, and a cost of the kind the footer reports —
# what the tokens would come to at published API prices, which nobody is billed.
default_usage() {
  usage '{"usage": {"input_tokens": 12000, "cache_creation_input_tokens": 200000, "cache_read_input_tokens": 1000000, "output_tokens": 22567},
          "modelUsage": {"claude-opus-4-5": {"inputTokens": 12000, "outputTokens": 22567, "cacheReadInputTokens": 1000000, "cacheCreationInputTokens": 200000, "costUSD": 12.34}}}' \
    '{"total_cost_usd": 12.34}'
}

# A fresh case: an author's description with no footer, a marked review at the head commit, and
# a run that used 1,234,567 tokens at $12.34.
new_case() {
  cases=$((cases+1))
  export STUB_DIR="$SB/case$cases"
  mkdir -p "$STUB_DIR"
  body 'Closes #7.'$'\n'
  echo 'HEADSHA' > "$STUB_DIR/head"
  reviewed
  default_usage
}

# run_raw [args…] — one run of the script with exactly these arguments, none included.
# Sets OUTPUT (stdout and stderr) and RC.
run_raw() {
  OUTPUT="$(GH_REPO=o/r PATH="$SB/bin:$PATH" bash "$SCRIPT" "$@" </dev/null 2>&1)"; RC=$?
}
# run — the ordinary call: pull request 5, against the case's execution file.
run() { run_raw 5 "$STUB_DIR/exec.json"; }

now() { cat "$STUB_DIR/body.md"; }
# cell <row> <1=tokens|2=cost> — one cell of the footer table, trimmed.
cell() {
  awk -F'|' -v r="$1" -v c="$2" \
    'function t(s) { gsub(/^[ \t]+|[ \t]+$/, "", s); return s }
     NF > 2 && t($2) == r { print t($(2 + c)); exit }' "$STUB_DIR/body.md"
}
# above / footer — the description either side of the footer marker.
above() { sed -n '/^<!-- pr-cost-footer -->$/q;p' "$STUB_DIR/body.md"; }
footer() { sed -n '/^<!-- pr-cost-footer -->$/,$p' "$STUB_DIR/body.md"; }
# gap — how many blank lines sit between the author's last line and the footer marker. Counted,
# because `$(…)` strips trailing newlines: comparing `above` against the author's own text passes
# whether the gap is zero, one or ten, so it cannot hold this invariant on its own.
gap() { awk '/^<!-- pr-cost-footer -->$/ { print n + 0; exit } /^$/ { n++; next } { n = 0 }' "$STUB_DIR/body.md"; }
# footers / rules — how many footers and horizontal rules the description carries.
footers() { grep -cF '<!-- pr-cost-footer -->' "$STUB_DIR/body.md"; }
rules() { grep -cE '^---$' "$STUB_DIR/body.md"; }
# The count is 0, not the empty string, when no call has been logged at all.
writes() { local c; c=$(grep -cE '^pr\|edit\|' "$STUB_DIR/gh-log" 2>/dev/null); echo "${c:-0}"; }
rows() { grep -cE '^\| (PR Review|Review [0-9]+) \|' "$STUB_DIR/body.md"; }

# SCENARIO: First review adds the footer
echo "The first review adds the footer:"
new_case
run
ok "exits 0 and edits the description once" '[ "$RC" = 0 ] && [ "$(writes)" = 1 ]'
ok "the description ends with a horizontal rule followed by the table, and the note last" \
  'grep -A 2 -xF -- "---" "$STUB_DIR/body.md" | grep -qF "| Round | Tokens | API cost |" &&
   printf "%s" "$(now)" | tail -n 1 | grep -qi "github actions"'
ok "the table is headed Tokens and API cost" \
  'grep -qF "| Tokens | API cost |" "$STUB_DIR/body.md"'
ok "there is a PR Review row carrying the run's tokens and cost" \
  '[ "$(cell "PR Review" 1)" = "1,234,567" ] && [ "$(cell "PR Review" 2)" = "\$12.34" ]'
ok "and a Total row showing the same figures" \
  '[ "$(cell Total 1)" = "1,234,567" ] && [ "$(cell Total 2)" = "\$12.34" ]'
ok "Total is the last row" \
  '[ "$(grep -cE "^\| " "$STUB_DIR/body.md")" = 4 ] && grep -E "^\| " "$STUB_DIR/body.md" | tail -n 1 | grep -q "^| Total |"'
ok "the author's own text is still there, above the rule" \
  'grep -qF "Closes #7." <<<"$(above)"'

# SCENARIO: Follow-up round appends a row and updates the total
echo "A follow-up round appends a row and updates the total:"
new_case
run
usage '{"modelUsage": {"claude-opus-4-5": {"inputTokens": 1000, "outputTokens": 2000, "cacheReadInputTokens": 30000, "cacheCreationInputTokens": 400}}}' \
  '{"total_cost_usd": 1.11}'
run
ok "exits 0" '[ "$RC" = 0 ]'
ok "a Review 1 row appears" '[ "$(cell "Review 1" 1)" = "33,400" ] && [ "$(cell "Review 1" 2)" = "\$1.11" ]'
ok "the PR Review row is unchanged" \
  '[ "$(cell "PR Review" 1)" = "1,234,567" ] && [ "$(cell "PR Review" 2)" = "\$12.34" ]'
ok "it is below the PR Review row and above Total" \
  '[ "$(grep -nE "^\| (PR Review|Review 1|Total) \|" "$STUB_DIR/body.md" | sed -n 2p | cut -d: -f2- | cut -c1-12)" = "| Review 1 |" ]'
ok "Total is the sum of both rows, in both columns" \
  '[ "$(cell Total 1)" = "1,267,967" ] && [ "$(cell Total 2)" = "\$13.45" ]'
ok "and there is still one footer and one rule" '[ "$(footers)" = 1 ] && [ "$(rules)" = 1 ]'

echo "A second follow-up round is Review 2, and a further round carries on counting:"
usage '{"modelUsage": {"claude-opus-4-5": {"inputTokens": 33}}}' '{"total_cost_usd": 0.01}'
run
ok "the third round is Review 2" '[ "$(cell "Review 2" 1)" = "33" ]'
ok "and the total covers all three" \
  '[ "$(cell Total 1)" = "1,268,000" ] && [ "$(cell Total 2)" = "\$13.46" ]'
# Re-running the first review from the workflow's manual dispatch is a fourth posting round.
run
ok "a fourth posting round is Review 3 and is counted in the total" \
  '[ "$(cell "Review 3" 1)" = "33" ] && [ "$(cell Total 1)" = "1,268,033" ] && [ "$(rows)" = 4 ]'

# SCENARIO: Author's description is left intact
echo "The author's description is left intact:"
new_case
body 'Original body.'$'\n'
run
author=$'# Title\n\nRewritten after the first review.\n\n- a bullet\n- and `---` in a sentence\n'
printf '%s%s' "$author" "$(footer)" > "$STUB_DIR/body.md"
usage '{"modelUsage": {"m": {"inputTokens": 5}}}' '{"total_cost_usd": 0.02}'
run
ok "everything above the rule is exactly as the author left it" '[ "$(above)" = "$(printf "%s" "$author")" ]'
ok "the description carries one footer, not two" '[ "$(footers)" = 1 ] && [ "$(rules)" = 1 ]'
ok "the earlier round's row survived the author's rewrite" '[ "$(cell "PR Review" 1)" = "1,234,567" ]'

echo "A description the author never gave a trailing newline is handled the same way:"
new_case
body 'No trailing newline.'
run
ok "the footer is still separated from it by a blank line" \
  '[ "$RC" = 0 ] && grep -qF "No trailing newline." <<<"$(above)" && [ "$(gap)" = 1 ] && [ "$(cell Total 1)" = "1,234,567" ]'
run
ok "and a second round does not grow the gap" \
  '[ "$(above)" = "$(printf "No trailing newline.")" ] && [ "$(gap)" = 1 ]'

# SCENARIO: A run that posts nothing adds no row
echo "A run that posted no review adds no row:"
new_case
reviews '[{"commit_id":"OLDERSHA","body":"<!-- review-pr -->"}]'
run
ok "exits 0, changes nothing, and says why" \
  '[ "$RC" = 0 ] && [ "$(writes)" = 0 ] && [ "$(now)" = "Closes #7." ] && grep -qi "no .*review" <<<"$OUTPUT"'

new_case
reviews '[{"commit_id":"HEADSHA","body":"A review posted without the marker"}]'
run
ok "an unmarked review at the head commit is not a round, so no row is written" \
  '[ "$RC" = 0 ] && [ "$(writes)" = 0 ] && [ "$(now)" = "Closes #7." ]'

new_case
reviews '[]'
run
ok "a pull request with no reviews at all is left alone" \
  '[ "$RC" = 0 ] && [ "$(writes)" = 0 ] && [ "$(now)" = "Closes #7." ]'

echo "A capped follow-up leaves an existing footer exactly as it was:"
new_case
run
before="$(now)"
reviews '[{"commit_id":"OLDERSHA","body":"<!-- review-pr -->"}]'
run
ok "the description is byte-for-byte unchanged" '[ "$(now)" = "$before" ] && [ "$(writes)" = 1 ]'

echo "A row covers the whole run, including the subagents it started:"
new_case
# Specialist and verifier subagents run on their own models, and the result message carries one
# modelUsage entry per model: every entry counts towards the round's row.
usage '{"modelUsage": {"claude-opus-4-5": {"inputTokens": 100, "outputTokens": 200, "cacheReadInputTokens": 300, "cacheCreationInputTokens": 400, "costUSD": 1.00},
                       "claude-sonnet-4-5": {"inputTokens": 1000, "outputTokens": 2000, "cacheReadInputTokens": 3000, "cacheCreationInputTokens": 4000, "costUSD": 0.50}}}' \
  '{"total_cost_usd": 1.50}'
run
ok "every model the run used is counted" '[ "$(cell "PR Review" 1)" = "11,000" ] && [ "$(cell "PR Review" 2)" = "\$1.50" ]'

new_case
usage '{"modelUsage": {"a": {"inputTokens": 1, "costUSD": 0.25}, "b": {"inputTokens": 2, "costUSD": 0.5}}}' '{}'
run
ok "a run whose totals are only per-model still gets real figures" \
  '[ "$(cell "PR Review" 1)" = "3" ] && [ "$(cell "PR Review" 2)" = "\$0.75" ]'

new_case
usage '{"usage": {"input_tokens": 1, "output_tokens": 2, "cache_creation_input_tokens": 3, "cache_read_input_tokens": 4}}' \
  '{"total_cost_usd": 2}'
run
ok "a run reporting one combined usage total is read from that instead" \
  '[ "$(cell "PR Review" 1)" = "10" ] && [ "$(cell "PR Review" 2)" = "\$2.00" ]'

echo "Big figures are grouped, and cents are not dropped:"
new_case
usage '{"modelUsage": {"m": {"inputTokens": 1234567890}}}' '{"total_cost_usd": 1234.5}'
run
ok "tokens and dollars both read naturally" \
  '[ "$(cell "PR Review" 1)" = "1,234,567,890" ] && [ "$(cell "PR Review" 2)" = "\$1,234.50" ]'

echo "A round whose usage cannot be determined says so instead of guessing:"
new_case
rm -f "$STUB_DIR/exec.json"
run
ok "the row is written with the figures unknown" \
  '[ "$RC" = 0 ] && [ "$(cell "PR Review" 1)" = unknown ] && [ "$(cell "PR Review" 2)" = unknown ]'
ok "and the total says it is incomplete" \
  '[ "$(cell Total 1)" = unknown ] && [ "$(cell Total 2)" = unknown ]'

new_case
printf 'not json at all' > "$STUB_DIR/exec.json"
run
ok "an unreadable execution file is unknown, not an error" \
  '[ "$RC" = 0 ] && [ "$(cell "PR Review" 1)" = unknown ]'

new_case
usage '{}' '{}'
run
ok "a result message carrying no usage at all is unknown" '[ "$(cell "PR Review" 1)" = unknown ]'

new_case
jq -n '[{type: "assistant"}]' > "$STUB_DIR/exec.json"
run
ok "an execution file that never reached a result is unknown" '[ "$(cell "PR Review" 1)" = unknown ]'

# The action is asked for its execution_file output; a run where it produced none passes an
# empty path through, and the round must still be recorded rather than failing after the post.
new_case
run_raw 5 ''
ok "an execution file the action never produced is unknown, and the row is still written" \
  '[ "$RC" = 0 ] && [ "$(writes)" = 1 ] && [ "$(cell "PR Review" 1)" = unknown ]'

echo "A known round and an unknown one total to a sum that admits what is missing:"
new_case
run
rm -f "$STUB_DIR/exec.json"
run
ok "the known row is untouched" '[ "$(cell "PR Review" 1)" = "1,234,567" ]'
ok "the unknown round's own row stays unknown" '[ "$(cell "Review 1" 1)" = unknown ]'
ok "and the total carries the known sum and says it is incomplete" \
  '[ "$(cell Total 1)" = "1,234,567 + unknown" ] && [ "$(cell Total 2)" = "\$12.34 + unknown" ]'
default_usage
run
ok "a later round with real figures adds to the known sum, still flagged incomplete" \
  '[ "$(cell Total 1)" = "2,469,134 + unknown" ]'

echo "An unanswerable question fails loudly, changing nothing:"
new_case
touch "$STUB_DIR/gh-fail"
run
ok "a failed query exits non-zero and writes nothing" '[ "$RC" != 0 ] && [ "$(writes)" = 0 ]'

new_case
touch "$STUB_DIR/gh-fail-edit"
run
ok "a failed edit exits non-zero and says so" \
  '[ "$RC" != 0 ] && [ "$(now)" = "Closes #7." ] && grep -qi "description" <<<"$OUTPUT"'

echo "Wrong invocation is refused:"
new_case
run_raw
ok "no arguments exits 1 with usage, changing nothing" \
  '[ "$RC" = 1 ] && grep -q usage <<<"$OUTPUT" && [ "$(writes)" = 0 ]'
run_raw 5
ok "a pull request number with no execution file exits 1 with usage, changing nothing" \
  '[ "$RC" = 1 ] && grep -q usage <<<"$OUTPUT" && [ "$(writes)" = 0 ]'
run_raw not-a-number "$STUB_DIR/exec.json"
ok "a non-numeric pull request number exits 1 with usage, changing nothing" \
  '[ "$RC" = 1 ] && grep -q usage <<<"$OUTPUT" && [ "$(writes)" = 0 ]'
ok "and the description is still the author's" '[ "$(now)" = "Closes #7." ]'

echo "A description GitHub hands back with CRLF endings stays one footer:"
new_case
run
# GitHub returns a description saved through its web UI with CRLF line endings. While the split
# matched the marker line exactly, it matched nothing at all in such a body, and every later round
# appended a whole second footer with its own disagreeing Total.
sed -i 's/$/\r/' "$STUB_DIR/body.md"
run
ok "a second round appends a row rather than a second footer" \
  '[ "$(footers)" = 1 ] && [ "$(rules)" = 1 ] && [ "$(rows)" = 2 ]'
ok "and the earlier round is still counted in the Total" \
  '[ "$(cell "Review 1" 1)" = "1,234,567" ] && [ "$(cell Total 1)" = "2,469,134" ]'

echo "A description that quotes the footer marker keeps the text below the quotation:"
new_case
# The likeliest body to quote the marker is a pull request describing this footer. While the split
# took the bare marker line, everything below the quotation was replaced by the generated footer.
printf 'Intro.\n\n```\n<!-- pr-cost-footer -->\n```\n\nOutro that matters.\n' > "$STUB_DIR/body.md"
run
ok "the quoted marker is not a split point" \
  'grep -qF "Outro that matters." "$STUB_DIR/body.md" && [ "$(rows)" = 1 ]'
run
ok "and a later round appends to the real footer, not to the quotation" \
  '[ "$(rows)" = 2 ] && grep -qF "Outro that matters." "$STUB_DIR/body.md"'

echo "An execution file the action created but never wrote to is unknown, not a crash:"
new_case
: > "$STUB_DIR/exec.json"
run
ok "an empty execution file records the round and says why" \
  '[ "$RC" = 0 ] && [ "$(writes)" = 1 ] && [ "$(cell "PR Review" 1)" = unknown ] && grep -qi "could not read" <<<"$OUTPUT"'
new_case
printf '   \n' > "$STUB_DIR/exec.json"
run
ok "a whitespace-only execution file does the same" \
  '[ "$RC" = 0 ] && [ "$(writes)" = 1 ] && [ "$(cell "PR Review" 1)" = unknown ]'

echo "A round that reported its tokens but not its cost says so in that column only:"
new_case
usage '{"modelUsage": {"m": {"inputTokens": 500}}}' '{}'
run
ok "the tokens cell is real where the cost cell is unknown" \
  '[ "$(cell "PR Review" 1)" = "500" ] && [ "$(cell "PR Review" 2)" = unknown ]'
ok "and the Total totals the two columns independently" \
  '[ "$(cell Total 1)" = "500" ] && [ "$(cell Total 2)" = unknown ]'

echo "Every execution-file shape is read the same way:"
new_case
jq -n '{messages: [{type: "result", total_cost_usd: 3.21, modelUsage: {m: {inputTokens: 7}}}]}' > "$STUB_DIR/exec.json"
run
ok "a message list under a messages key" \
  '[ "$(cell "PR Review" 1)" = "7" ] && [ "$(cell "PR Review" 2)" = "\$3.21" ]'
new_case
jq -n '{type: "result", total_cost_usd: 4.56, modelUsage: {m: {inputTokens: 9}}}' > "$STUB_DIR/exec.json"
run
ok "a lone result object" \
  '[ "$(cell "PR Review" 1)" = "9" ] && [ "$(cell "PR Review" 2)" = "\$4.56" ]'
new_case
# One JSON document per line, the shape a streamed agent log takes. Read one document at a time,
# the filter answered once per line and only the first answer — the init message — was read.
{ jq -nc '{type: "system"}'; jq -nc '{type: "assistant"}'
  jq -nc '{type: "result", total_cost_usd: 5, modelUsage: {m: {inputTokens: 11}}}'; } > "$STUB_DIR/exec.json"
run
ok "a stream of one document per line, read whole" \
  '[ "$(cell "PR Review" 1)" = "11" ] && [ "$(cell "PR Review" 2)" = "\$5.00" ]'

echo "Where the two token sources disagree, the per-model totals win:"
new_case
# `usage` carries the orchestrator's share; `modelUsage` has an entry per model, so summing it is
# what makes the row cover the specialists and verifiers the round started.
usage '{"usage": {"input_tokens": 100}, "modelUsage": {"orchestrator": {"inputTokens": 100}, "specialist": {"inputTokens": 900}}}' \
  '{"total_cost_usd": 1}'
run
ok "the row covers every model, not the orchestrator alone" '[ "$(cell "PR Review" 1)" = "1,000" ]'

echo "Fractions of a cent are rounded, not dropped:"
new_case
usage '{"modelUsage": {"m": {"inputTokens": 10}}}' '{"total_cost_usd": 12.346}'
run
ok "a cost over the half-cent rounds up" '[ "$(cell "PR Review" 2)" = "\$12.35" ]'

echo "A review against another commit is not this round:"
new_case
# `grep -qxF`, not `grep -qF`: a whole-line match, so a longer commit id that merely starts with
# the head sha is a different commit.
reviews '[{"commit_id":"HEADSHAEXTRA","body":"<!-- review-pr -->"}]'
run
ok "a commit id containing the head sha does not count as a review at the head" \
  '[ "$RC" = 0 ] && [ "$(writes)" = 0 ] && [ "$(now)" = "Closes #7." ]'

echo "An answer that does not say which commit is the head fails loudly:"
new_case
echo 'null' > "$STUB_DIR/head"
run
ok "a null head commit exits non-zero, changing nothing" \
  '[ "$RC" != 0 ] && [ "$(writes)" = 0 ] && [ "$(now)" = "Closes #7." ]'
new_case
: > "$STUB_DIR/head"
run
ok "an empty head commit exits non-zero, changing nothing" \
  '[ "$RC" != 0 ] && [ "$(writes)" = 0 ]'
new_case
touch "$STUB_DIR/gh-bad-json"
run
ok "an unreadable answer about the pull request exits non-zero, changing nothing" \
  '[ "$RC" != 0 ] && [ "$(writes)" = 0 ] && [ "$(now)" = "Closes #7." ]'

echo "A description the author left empty still gets a footer:"
new_case
body ''
run
ok "the footer starts the description, with no blank line above it" \
  '[ "$RC" = 0 ] && [ "$(head -n 1 "$STUB_DIR/body.md")" = "<!-- pr-cost-footer -->" ]'

echo ""
echo "RESULT: $pass passed, $fail failed"
# A floor, as scripts/traceability-check.tests.sh and scripts/homebrew-formula.tests.sh carry for
# the same reason: dropping a whole block lowers the count while reporting zero failures and
# exit 0 — a matrix that shrinks towards nothing and still reads green.
[ "$pass" -ge 63 ] || { echo "FAIL: expected at least 63 assertions, ran $pass — did a block get dropped?" >&2; exit 1; }
[ "$fail" = 0 ]
