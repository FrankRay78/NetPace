#!/usr/bin/env bash
#
# pr-review-costs.tests.sh — standalone test matrix for pr-review-costs.sh.
#
# The script's job is deciding which closed pull requests the page lacks, and what each one's row
# says: the lines it changed split into code, tests and docs, its first review's risk tier and
# verdict, and its review rounds' cost. All of that is provable from outside with a stub `gh` first
# on PATH and the script's `--page` mode, so nothing reaches GitHub and the committed page is never
# touched. Exits
# non-zero on any failure.
#
# The stub serves closed pull requests from prs.json, and each one's changed files and reviews from
# files-<n>.json and reviews-<n>.json, running the script's own `--jq` filters against them so the
# filter picking out marked reviews is the one under test. Every call is logged, which is how a
# case proves a listed pull request was not fetched again. A gh-fail-files file fails the files call.
#
# What this matrix does not run is the default page path, which would write into the repository;
# a real run, read in the diff, is its check.
#
#   Usage:  scripts/pr-review-costs.tests.sh

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT="$HERE/pr-review-costs.sh"

SB="$(mktemp -d)"
trap 'rm -rf "$SB"' EXIT

mkdir -p "$SB/bin"
cat > "$SB/bin/gh" <<'STUB'
#!/usr/bin/env bash
{ for a in "$@"; do printf '%s|' "$a"; done; printf '\n'; } >> "$STUB_DIR/gh-log"
args=("$@")
opt() { local i; for ((i = 0; i < ${#args[@]}; i++)); do [ "${args[i]}" = "$1" ] && printf '%s\n' "${args[i+1]}"; done; }
case "$1 $2" in
  "pr list") cat "$STUB_DIR/prs.json" ;;
  "api --paginate")
    n=$(grep -oE 'pulls/[0-9]+' <<<"$3" | cut -d/ -f2)
    case "$3" in
      */files)
        [ -f "$STUB_DIR/gh-fail-files" ] && { echo "gh: HTTP 502: Bad Gateway" >&2; exit 1; }
        jq -r "$(opt --jq)" "$STUB_DIR/files-$n.json" ;;
      */reviews) jq -r "$(opt --jq)" "$STUB_DIR/reviews-$n.json" ;;
    esac ;;
  *) echo "stub gh: unexpected call: $*" >&2; exit 1 ;;
esac
STUB
chmod +x "$SB/bin/gh"

pass=0; fail=0; OUTPUT=; RC=
ok() { if eval "$2"; then echo "  ok   $1"; pass=$((pass+1)); else echo "  FAIL $1 -- rc=$RC output:[$OUTPUT]"; fail=$((fail+1)); fi; }

fresh() { export STUB_DIR="$SB/case"; rm -rf "$STUB_DIR"; mkdir -p "$STUB_DIR"; PAGE="$STUB_DIR/page.md"; }
run() { OUTPUT="$(PATH="$SB/bin:$PATH" bash "$SCRIPT" --page "$PAGE" 2>&1)"; RC=$?; }
# row <n> — the page's row for pull request <n>.
row() { grep -E "^\| \[#$1\]" "$PAGE"; }
# col <n> <k> — the k-th cell (1-based) of pull request <n>'s row, trimmed.
# Escaped pipes in a title are set aside first, so a title is one cell however it reads.
cells_of() { sed 's/\\|/\x01/g' | awk -F'|' -v k="$1" '{ s = $(k + 1); gsub(/^ +| +$/, "", s); gsub(/\x01/, "\\|", s); print s }'; }
col() { row "$1" | cells_of "$2"; }

FOOTER='\n\n<!-- pr-cost-footer -->\n\n---\n\n| Round | Tokens | API cost |\n| --- | --- | --- |'
# pr <n> <closed> <merged-or-null> <footer rows> — one closed pull request, as gh pr list gives it.
pr() {
  local body="Why${4:+$FOOTER$4}"
  jq -n --argjson n "$1" --arg c "$2" --argjson m "$3" --arg b "$(printf "$body")" \
    '{number: $n, title: (if $n == 20 then "Change | 20" else "Change \($n)" end), url: "https://example.test/pull/\($n)", closedAt: $c, mergedAt: $m, body: $b}'
}
prs() { jq -s . > "$STUB_DIR/prs.json"; }
files() { printf '%s' "$2" > "$STUB_DIR/files-$1.json"; }
reviews() { printf '%s' "$2" > "$STUB_DIR/reviews-$1.json"; }
VERDICT='[{"body":"Summary\nRisk tier: High · Merge with no human review: no\n<!-- review-pr -->"}]'
ONE_ROUND='\n| PR Review | 6,432,360 | $5.34 |\n| Total | 6,432,360 | $5.34 |'

echo "A new page:"
fresh
{ pr 10 2026-11-01T10:00:00Z '"2026-11-01T10:00:00Z"' "$ONE_ROUND"
  pr 12 2026-11-03T10:00:00Z '"2026-11-03T10:00:00Z"' "$ONE_ROUND"
  pr 11 2026-11-02T10:00:00Z '"2026-11-02T10:00:00Z"' "$ONE_ROUND"
  pr 9 2026-10-30T10:00:00Z '"2026-10-30T10:00:00Z"' ''; } | prs
for n in 10 11 12; do files $n '[]'; reviews $n "$VERDICT"; done
run
ok "succeeds and names the rows it added" '[ "$RC" = 0 ] && grep -qF "added 3 row(s)" <<<"$OUTPUT" && grep -qF "#10" <<<"$OUTPUT" && grep -qF "#12" <<<"$OUTPUT"'
ok "lists them newest first" '[ "$(grep -oE "^\| \[#[0-9]+" "$PAGE" | tr -d "|[# " | paste -sd,)" = "12,11,10" ]'
ok "leaves out a pull request with no cost footer" '! row 9 >/dev/null && ! grep -q "pulls/9/" "$STUB_DIR/gh-log"'
ok "opens with a note telling agents it is not guidance" '[ "$(head -c 13 "$PAGE")" = "<!-- Agents: " ]'
ok "carries a header row" 'grep -qF "| PR | Title | Closed | Code | Tests | Docs | Risk | No-human merge | Review |" "$PAGE"'

echo "A second run:"
sed -i 's/| High |/| Low (hand-corrected) |/' "$PAGE"
: > "$STUB_DIR/gh-log"
{ pr 10 2026-11-01T10:00:00Z '"2026-11-01T10:00:00Z"' "$ONE_ROUND"
  pr 12 2026-11-03T10:00:00Z '"2026-11-03T10:00:00Z"' "$ONE_ROUND"
  pr 11 2026-11-02T10:00:00Z '"2026-11-02T10:00:00Z"' "$ONE_ROUND"
  pr 13 2026-11-02T12:00:00Z '"2026-11-02T12:00:00Z"' "$ONE_ROUND"; } | prs
files 13 '[]'; reviews 13 "$VERDICT"
run
ok "adds only the pull request not yet listed" '[ "$RC" = 0 ] && grep -qF "added 1 row(s)" <<<"$OUTPUT" && grep -qE ": #13\.$" <<<"$OUTPUT"'
ok "does not fetch a listed pull request again" '! grep -qE "pulls/(10|11|12)/" "$STUB_DIR/gh-log"'
ok "keeps a hand-corrected row as it was" '[ "$(col 10 7)" = "Low (hand-corrected)" ]'
ok "slots the new row in by its closed date" '[ "$(grep -oE "^\| \[#[0-9]+" "$PAGE" | tr -d "|[# " | paste -sd,)" = "12,13,11,10" ]'
run
ok "a run with nothing new changes nothing" '[ "$RC" = 0 ] && grep -qF "nothing new" <<<"$OUTPUT" && [ "$(grep -c "^| \[#" "$PAGE")" = 4 ]'

echo "One pull request's row:"
fresh
pr 20 2026-11-04T09:00:00Z null $'\n| PR Review | 10,508,842 | $10.24 |\n| Review 1 | 3,140,638 | $2.96 |\n| Review 2 | 2,782,760 | $2.67 |\n| Total | 16,432,240 | $15.87 |' | prs
files 20 '[{"filename":"src/NetPace.Core/A.cs","additions":90,"deletions":40},{"filename":"scripts/x.sh","additions":9,"deletions":2},
  {"filename":"src/NetPace.Core.Tests/ATests.cs","additions":900,"deletions":100},{"filename":"src/NetPace.Console.Tests/Expectations/x.verified.txt","additions":5,"deletions":0},
  {"filename":"scripts/x.tests.sh","additions":50,"deletions":0},{"filename":"docs/a.md","additions":22,"deletions":13}]'
reviews 20 '[{"body":"an author reply, no marker"},{"body":"`Risk tier: Medium · Merge with no human review: yes`\n<!-- review-pr -->"},{"body":"Risk tier: High · Merge with no human review: no\n<!-- review-pr -->"}]'
run
ok "the number and the title are cells of their own" '[ "$(col 20 1)" = "[#20](https://example.test/pull/20)" ] && [ "$(col 20 2)" = "Change \\| 20 *(not merged)*" ]'
ok "code size is lines added plus deleted for everything not a test or docs" '[ "$(col 20 4)" = "141" ]'
ok "tests count test projects, their snapshots and shell matrices, grouped in thousands" '[ "$(col 20 5)" = "1,055" ]'
ok "docs count Markdown" '[ "$(col 20 6)" = "35" ]'
ok "risk and verdict come from the first marked review, backticks or not" '[ "$(col 20 7)" = Medium ] && [ "$(col 20 8)" = yes ]'
ok "each round lands in its own column" '[ "$(col 20 9)" = "\$10.24" ] && [ "$(col 20 10)" = "\$2.96" ] && [ "$(col 20 11)" = "\$2.67" ]'
ok "the total is the footer's, tokens in millions" '[ "$(col 20 12)" = "\$15.87" ] && [ "$(col 20 13)" = "16.4M" ]'
ok "the closed date is the day it closed" '[ "$(col 20 3)" = 2026-11-04 ]'

echo "Gaps:"
fresh
pr 30 2026-11-04T09:00:00Z '"2026-11-04T09:00:00Z"' $'\n| PR Review | unknown | unknown |\n| Total | unknown | unknown |' | prs
files 30 '[{"filename":"README.md","additions":1,"deletions":1}]'
reviews 30 '[]'
run
ok "a kind of file not touched is an empty cell" '[ -z "$(col 30 4)" ] && [ -z "$(col 30 5)" ] && [ "$(col 30 6)" = 2 ]'
ok "no marked review leaves risk and verdict empty" '[ -z "$(col 30 7)" ] && [ -z "$(col 30 8)" ]'
ok "an unknown cost stays unknown rather than a guess" '[ "$(col 30 9)" = unknown ] && [ "$(col 30 12)" = unknown ] && [ "$(col 30 13)" = unknown ]'
ok "and an empty cell keeps every later cell in its column" '[ "$(row 30 | grep -o "|" | wc -l)" = 14 ]'

echo "The totals row:"
fresh
{ pr 50 2026-11-04T09:00:00Z '"2026-11-04T09:00:00Z"' $'\n| PR Review | 2,000,000 | $5.34 |\n| Review 1 | 1,000,000 | $1.10 |\n| Total | 3,000,000 | $6.44 |'
  pr 51 2026-11-03T09:00:00Z '"2026-11-03T09:00:00Z"' $'\n| PR Review | 1,500,000 | $2.66 |\n| Total | 1,500,000 | $2.66 |'
  pr 52 2026-11-02T09:00:00Z null $'\n| PR Review | unknown | unknown |\n| Total | unknown | unknown |'; } | prs
files 50 '[{"filename":"a.cs","additions":10,"deletions":2},{"filename":"x.tests.sh","additions":5,"deletions":0}]'
files 51 '[{"filename":"b.cs","additions":3,"deletions":4}]'
files 52 '[{"filename":"c.md","additions":1,"deletions":1}]'
reviews 50 "$VERDICT"; reviews 51 '[{"body":"Risk tier: Low · Merge with no human review: yes\n<!-- review-pr -->"}]'; reviews 52 '[]'
run
TOTAL() { grep -F "| **Total" "$PAGE" | cells_of "$1"; }
ok "sits last" '[ "$(tail -1 "$PAGE" | cut -c1-13)" = "| **Total** |" ]'
ok "sums each size column" '[ "$(TOTAL 4)" = "**19**" ] && [ "$(TOTAL 5)" = "**5**" ] && [ "$(TOTAL 6)" = "**2**" ]'
ok "leaves title, date, risk and verdict empty" '[ "$(TOTAL 1)" = "**Total**" ] && [ -z "$(TOTAL 2)$(TOTAL 3)$(TOTAL 7)$(TOTAL 8)" ]'
ok "sums each round, and owns an unknown rather than hide it" '[ "$(TOTAL 9)" = "**\$8.00 + unknown**" ] && [ "$(TOTAL 10)" = "**\$1.10**" ] && [ -z "$(TOTAL 11)" ]'
ok "sums total cost and tokens" '[ "$(TOTAL 12)" = "**\$9.10 + unknown**" ] && [ "$(TOTAL 13)" = "**4.5M + unknown**" ]'
sed -i 's/| \$2.66 |  |  | \$2.66 |/| $3.66 |  |  | $3.66 |/' "$PAGE"
run
ok "is recomputed from the rows as they stand, once" '[ "$(TOTAL 12)" = "**\$10.10 + unknown**" ] && [ "$(grep -c "| \*\*Total" "$PAGE")" = 1 ]'

echo "The CSV beside the page:"
CSV="${PAGE%.md}.csv"
# csv <n> <k> — the k-th field of pull request <n>'s CSV line; titles hold no comma in these fixtures.
csv() { grep -E "^$1," "$CSV" | sed 's/"//g' | cut -d, -f"$2"; }
ok "is linked from the page, above the table" '[ "$(grep -n "(page.csv)" "$PAGE" | cut -d: -f1)" -lt "$(grep -n "^| PR |" "$PAGE" | cut -d: -f1)" ]'
ok "holds a header and one line per row, newest first, with no totals" '[ "$(cut -d, -f1 "$CSV" | paste -sd,)" = "PR,50,51,52" ]'
ok "carries plain numbers: no dollar sign, no thousands separator" '[ "$(csv 50 11)" = 5.34 ] && [ "$(csv 50 14)" = 6.44 ] && [ "$(csv 50 6)" = 12 ] && [ "$(csv 50 15)" = 3.0 ]'
ok "reads a hand correction made on the page" '[ "$(csv 51 14)" = 3.66 ]'
ok "leaves an unknown cost empty rather than break a sum" '[ -z "$(csv 52 11)" ] && [ -z "$(csv 52 14)" ] && [ -z "$(csv 52 15)" ]'
ok "states merged as true or false, with the title bare" '[ "$(csv 50 4)" = true ] && [ "$(csv 52 4)" = false ] && [ "$(csv 52 3)" = "Change 52" ]'

echo "A failing call:"
fresh
pr 40 2026-11-04T09:00:00Z '"2026-11-04T09:00:00Z"' "$ONE_ROUND" | prs
reviews 40 "$VERDICT"; touch "$STUB_DIR/gh-fail-files"
printf 'existing page\n' > "$PAGE"
run
ok "fails, naming the pull request" '[ "$RC" != 0 ] && grep -qF "#40" <<<"$OUTPUT"'
ok "and leaves the page as it was" '[ "$(cat "$PAGE")" = "existing page" ]'

echo
echo "$pass passed, $fail failed"
[ "$fail" -eq 0 ]
