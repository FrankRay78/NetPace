#!/usr/bin/env bash
#
# pr-review-costs.sh — add a row per newly closed pull request to docs/metrics/pr-review-costs.md
# (and the same rows to pr-review-costs.csv beside it),
# collecting what each one's automated review cycle cost beside how big and how risky it was.
#
# Every reviewed pull request carries its review rounds' cost in a footer at the end of its
# description (scripts/pr-cost-footer.sh), but one pull request at a time cannot show whether a
# review rule change paid for itself, or how cost tracks with size and risk. This gathers the
# footers into one table, newest first, with the lines each pull request changed and the risk tier
# and no-human verdict its first review gave.
#
# WHY IT ONLY ADDS. A row already on the page is kept exactly as it is: the script never fetches
# that pull request again, so a row corrected by hand stays corrected, and a run costs one API
# call per new pull request rather than one per pull request ever closed. Only the text around the
# table is regenerated. A closed pull request with no cost footer is not listed — it was closed
# before footers existed, or never reviewed, and a row of blanks would read as a free review.
#
# WHOSE FIGURES. Only the maintainer's pull requests from a branch of this repository are listed,
# and only reviews the review bot posted are read: those are the only ones the review workflows
# run on, and anyone can open a pull request, or post a review, that types a footer or a risk line
# by hand. A footer is recognised by the shape scripts/pr-cost-footer.sh writes it in, so a
# description that merely quotes the marker is not mistaken for one.
#
# WHY THE FIRST REVIEW'S RISK. The tier is judged on the whole pull request against main and
# rarely moves between rounds; the first review's tier is the one the change arrived with, before
# any follow-up commits answered it. The verdict is taken from the same review so the two cells
# describe one moment.
#
# HOW LINES ARE SPLIT. A changed file is a test when it sits under a `*.Tests` project directory
# (snapshots included) or is a `*.tests.sh` matrix; documentation when it is any other `*.md`;
# code otherwise — C#, shell, workflows and configuration alike.
#
# The helpers below hand several cells back on one line separated by \037 (ASCII unit separator)
# rather than a tab: a cell may be empty, and `read` collapses a run of tabs into one.
#
# WHY IN THE CODEBASE, AND WHY AGENTS SKIP IT. The file is data for the maintainer, written only
# by this script and committed like any other change; the wiki's narrow layout made the table hard
# to read. It cites a pull request number on every row, which CONVENTIONS.md's rule that issue
# numbers live only in change-intent records allows for it and its CSV, and it opens with a note
# telling agents it is not guidance. Rationale: CIR
# docs/change-intent-records/2026-11-05-review-costs-live-in-the-codebase.md.
#
# Prerequisites: gh signed in, jq on PATH. GH_REPO names the repository (`owner/name`);
# gh's own default applies when it is unset. Tests: scripts/pr-review-costs.tests.sh.
#
#   Usage:  scripts/pr-review-costs.sh               update docs/metrics/pr-review-costs.md
#           scripts/pr-review-costs.sh --page <file> update the page at <file> instead
#
# Nothing is committed: review the diff and commit the file as you would any other change.

set -euo pipefail
# Rows are built inside command substitutions, where bash otherwise drops `set -e`: a failed fetch
# would fall through to a row of blanks written to the page as if it were the answer.
shopt -s inherit_errexit

PAGE_PATH='docs/metrics/pr-review-costs.md'
FOOTER_MARKER='<!-- pr-cost-footer -->'
REVIEW_MARKER='<!-- review-pr -->'
# Who the review workflows run for, and who posts their reviews (.github/workflows/review-pr.yml).
MAINTAINER='FrankRay78'
REVIEW_BOT='github-actions[bot]'
HEADER='| PR | Title | Closed | Code | Tests | Docs | Risk | No-human merge | Review | Follow-up 1 | Follow-up 2 | Total cost | Total tokens |'
SEPARATOR='| --- | --- | --- | ---: | ---: | ---: | --- | --- | ---: | ---: | ---: | ---: | ---: |'

# The awk helpers every reader of a row shares. A row is split on its pipes once escaped pipes in a
# title are set aside, so an empty cell is still a cell; cells(row, c) fills c[1..] with the trimmed
# cells and returns how many there are. commafy groups a whole number in thousands.
AWK_LIB='
  function cells(line, c,   raw, n, i, k) {
    gsub(/\\\|/, "\001", line)
    n = split(line, raw, "|"); k = 0
    for (i = 2; i < n; i++) { s = raw[i]; gsub(/^[ \t]+|[ \t]+$/, "", s); gsub(/\001/, "\\|", s); c[++k] = s }
    return k
  }
  function commafy(n,   s, out) {
    s = sprintf("%d", n); out = ""
    while (length(s) > 3) { out = "," substr(s, length(s) - 2) out; s = substr(s, 1, length(s) - 3) }
    return s out
  }
'

usage() { echo "usage: scripts/pr-review-costs.sh [--page <file>]" >&2; }
say() { echo "pr-review-costs: $*"; }
die() { echo "pr-review-costs: $*" >&2; exit 1; }

# cell <text> — a table cell's contents: one line, no pipe that would split the cell.
cell() { printf '%s' "$1" | tr '\n\r' '  ' | sed 's/|/\\|/g'; }

# lines_changed <tsv: path, additions, deletions> — <code>, <tests> and <docs>, each the lines added
# plus the lines deleted, as a measure of size; empty for a kind the pull request did not touch.
lines_changed() {
  awk -F'\t' "$AWK_LIB"'
    {
      k = "code"
      if ($1 ~ /(^|\/)[^\/]*\.Tests\// || $1 ~ /\.tests\.sh$/) k = "tests"
      else if ($1 ~ /\.md$/) k = "docs"
      size[k] += $2 + $3
    }
    function fmt(k) { return size[k] ? commafy(size[k]) : "" }
    END { printf "%s\037%s\037%s\n", fmt("code"), fmt("tests"), fmt("docs") }
  '
}

# footer_costs <body> — <review>, <follow-up 1>, <follow-up 2>, <total cost> and <total tokens>,
# read from the cost footer. Two follow-up columns: a third is exceptional, so a later round gets no
# column of its own, though the footer's total still counts it.
#
# A figure the footer could not report (its `unknown`) is an empty cell, and a total the footer
# marks as partial keeps only the part it does state: the table records what was measured and
# leaves out what was not, rather than carry a second notion of "missing" beside an empty cell.
#
# The footer is the marker line, a blank line, then a horizontal rule — the last such occurrence, as
# scripts/pr-cost-footer.sh finds its own — so an example quoted above it is not read. Fails, with
# nothing printed, when the body has no footer.
footer_costs() {
  printf '%s\n' "$1" | tr -d '\r' | awk -v marker="$FOOTER_MARKER" '
    function trim(s) { gsub(/^[ \t]+|[ \t]+$/, "", s); return s }
    function cost(s) { return match(s, /^\$[0-9][0-9,]*(\.[0-9]+)?/) ? substr(s, 1, RLENGTH) : "" }
    function tokens(s) {
      if (!match(s, /^[0-9][0-9,]*/)) return ""
      s = substr(s, 1, RLENGTH); gsub(/,/, "", s)
      return sprintf("%.1fM", s / 1000000)
    }
    { line[NR] = $0 }
    END {
      for (i = 1; i <= NR; i++)
        if (trim(line[i]) == marker && trim(line[i + 1]) == "" && trim(line[i + 2]) == "---") s = i
      if (!s) exit 1
      for (i = s + 3; i <= NR; i++) {
        if (line[i] !~ /^\|/) continue
        split(line[i], c, "|"); name = trim(c[2]); price = cost(trim(c[4]))
        if (name == "PR Review") r[0] = price
        else if (name ~ /^Review [12]$/) r[substr(name, 8) + 0] = price
        else if (name == "Total") { tc = price; tt = tokens(trim(c[3])) }
      }
      for (i = 0; i <= 2; i++) printf "%s\037", (i in r) ? r[i] : ""
      printf "%s\037%s\n", tc, tt
    }
  '
}

# first_verdict <first review's body> — "<risk>\t<no-human>" from its
# `Risk tier: X · Merge with no human review: Y` line, or empty for each when there is none. Earlier
# reviews wrapped the line in backticks, so a leading one is allowed.
first_verdict() {
  local line risk human
  line=$(grep -m1 -E '^`?Risk tier: ' <<<"$1" | tr -d '`' || true)
  risk=$(sed -nE 's/^Risk tier: ([^·]+).*/\1/p' <<<"$line" | sed 's/[[:space:]]*$//')
  human=$(sed -nE 's/.*Merge with no human review: *([A-Za-z]+).*/\1/p' <<<"$line")
  printf '%s\037%s\n' "$risk" "$human"
}

# build_row <pr-json> — the table row for one closed pull request.
build_row() {
  local pr=$1 n url title closed merged files review code tests docs risk human
  n=$(jq -r .number <<<"$pr")
  url=$(jq -r .url <<<"$pr")
  title=$(jq -r .title <<<"$pr")
  closed=$(jq -r '.closedAt[0:10]' <<<"$pr")
  merged=$(jq -r '.mergedAt // ""' <<<"$pr")

  files=$(gh api --paginate "repos/{owner}/{repo}/pulls/$n/files" \
    --jq '.[] | [.filename, .additions, .deletions] | @tsv') \
    || die "could not read the files changed by #$n."
  # One JSON string per line, so the first review is the first line however many lines its body
  # has; --jq runs per page, so it cannot pick the first itself.
  review=$(gh api --paginate "repos/{owner}/{repo}/pulls/$n/reviews" \
    --jq ".[] | select(.user.login == \"$REVIEW_BOT\" and (.body | contains(\"$REVIEW_MARKER\"))) | .body | @json") \
    || die "could not read the reviews of #$n."
  review=$(head -n1 <<<"$review" | jq -r '.')

  IFS=$'\037' read -r code tests docs < <(printf '%s' "$files" | lines_changed)
  IFS=$'\037' read -r risk human < <(first_verdict "$review")
  local r0 r1 r2 total tokens
  IFS=$'\037' read -r r0 r1 r2 total tokens < <(footer_costs "$(jq -r .body <<<"$pr")")

  local title_cell; title_cell=$(cell "$title")
  [ -n "$merged" ] || title_cell+=" *(not merged)*"

  printf '| %s | %s | %s | %s | %s | %s | %s | %s | %s | %s | %s | %s | %s |\n' \
    "[#$n]($url)" "$title_cell" "$closed" "$code" "$tests" "$docs" "$risk" "$human" \
    "$r0" "$r1" "$r2" "$total" "$tokens"
}

# totals_row <rows> — the bold Total row under the table, summed from the rows as they stand on
# the page, so a row corrected by hand is counted as corrected. Only the numeric columns are
# summed — size and cost — and every other cell is left empty. A cell that is not a plain figure is
# not counted. Tokens are the sum of the rounded figures shown, so the total is to within a few
# tenths of a million.
totals_row() {
  printf '%s\n' "$1" | awk "$AWK_LIB"'
    function money(s, k) {
      gsub(/,/, "", s)
      if (s ~ /^\$[0-9]+(\.[0-9]+)?$/) { seen[k] = 1; cents[k] += int(substr(s, 2) * 100 + 0.5) }
    }
    function bold(s) { return s == "" ? "" : "**" s "**" }
    function fmtsize(k) { return seen[k] ? bold(commafy(size[k])) : "" }
    function fmtmoney(k) {
      return seen[k] ? bold(sprintf("$%s.%02d", commafy(int(cents[k] / 100)), cents[k] % 100)) : ""
    }
    {
      cells($0, c)
      for (k = 4; k <= 6; k++) if (c[k] != "") { seen[k] = 1; v = c[k]; gsub(/,/, "", v); size[k] += v }
      for (k = 9; k <= 12; k++) money(c[k], k)
      if (c[13] ~ /^[0-9]+(\.[0-9]+)?M$/) { mtok += substr(c[13], 1, length(c[13]) - 1); seen[13] = 1 }
    }
    END {
      printf "| **Total** | | | %s | %s | %s | | | %s | %s | %s | %s | %s |\n",
        fmtsize(4), fmtsize(5), fmtsize(6),
        fmtmoney(9), fmtmoney(10), fmtmoney(11), fmtmoney(12),
        seen[13] ? bold(sprintf("%.1fM", mtok)) : ""
    }
  '
}

# write_csv <sorted rows> — the same rows as CSV, for a spreadsheet: one header line, then one line
# per pull request, with no totals row (a spreadsheet sums for itself). Numbers are plain — no
# dollar sign, no thousands separator — and tokens are in millions, as precise as the table holds
# them, so the CSV never claims a precision the page does not have. A cell that is not a plain
# number is left empty rather than break a column's sum. Every text field is quoted, so a comma in
# one — a risk cell corrected by hand, say — moves no later field; and one a spreadsheet would read
# as a formula (=, +, - or @ first) is opened with a quote mark, which a spreadsheet shows as text.
# Derived from the page's rows,
# so a correction made to a row by hand reaches the CSV too.
write_csv() {
  printf '%s\n' 'PR,Url,Title,Merged,Closed,CodeLines,TestLines,DocsLines,Risk,NoHumanMerge,ReviewCost,FollowUp1Cost,FollowUp2Cost,TotalCost,TotalTokensMillions'
  [ -z "$1" ] || printf '%s\n' "$1" | awk "$AWK_LIB"'
    function q(s) { if (s ~ /^[=+@-]/) s = "\047" s; gsub(/"/, "\"\"", s); return "\"" s "\"" }
    function num(s) { gsub(/,/, "", s); return s ~ /^[0-9]+$/ ? s : "" }
    function money(s) { gsub(/,/, "", s); return s ~ /^\$[0-9]+(\.[0-9]+)?$/ ? substr(s, 2) : "" }
    {
      cells($0, c)
      match(c[1], /#[0-9]+/); n = substr(c[1], RSTART + 1, RLENGTH - 1)
      match(c[1], /\(.*\)/); url = substr(c[1], RSTART + 1, RLENGTH - 2)
      title = c[2]; merged = "true"
      if (sub(/ \*\(not merged\)\*$/, "", title)) merged = "false"
      gsub(/\\\|/, "|", title)
      tok = (c[13] ~ /^[0-9]+(\.[0-9]+)?M$/) ? substr(c[13], 1, length(c[13]) - 1) : ""
      printf "%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s\n", n, url, q(title), merged, q(c[3]),
        num(c[4]), num(c[5]), num(c[6]), q(c[7]), q(c[8]),
        money(c[9]), money(c[10]), money(c[11]), money(c[12]), tok
    }
  '
}

# write_page <file> <rows> — the whole page: fixed prose, the header, then every row newest first;
# and beside it, <file> with a .csv extension holding the same rows.
write_page() {
  local file=$1 rows=$2 csv sorted=''
  csv="${file%.md}.csv"
  # Newest first by the Closed column, then by number, so the order does not depend on which
  # run added a row.
  [ -z "$rows" ] || sorted=$(printf '%s\n' "$rows" \
    | awk "$AWK_LIB"'{ cells($0, c); match(c[1], /#[0-9]+/); printf "%s\t%010d\t%s\n", c[3], substr(c[1], RSTART + 1, RLENGTH - 1), $0 }' \
    | sort -t$'\t' -k1,1r -k2,2r | cut -f3-)
  {
    cat <<'PROSE'
<!-- Agents: this file is generated data for the maintainer, not guidance. Do not read it for rules, conventions or context, and do not edit it; `scripts/pr-review-costs.sh` writes it. -->

# PR review costs

What each closed pull request's automated review cycle cost, newest first, beside how much it changed and how risky its first review judged it.

Generated by [`scripts/pr-review-costs.sh`](../../scripts/pr-review-costs.sh). Each run adds a row for every closed pull request that carries a review cost footer and is not yet listed; a row already here is never rewritten, so a correction made to a row by hand survives. Text outside the table is regenerated on every run.

- **Code / Tests / Docs** — size, as lines added plus lines deleted. Tests are files under a `*.Tests` project and `*.tests.sh` matrices; docs are any other Markdown; code is everything else.
- **Risk** and **No-human merge** — the risk tier and the merge-with-no-human-review verdict from the first `/gaffer:review-pr` round.
- **Review** — the first review's API cost; **Follow-up 1–2** — the next two rounds'. A third follow-up is exceptional and has no column. **Total cost** and **Total tokens** cover every round.
- **Total (bottom row)** — size and cost summed from the rows above as they stand, recomputed on every run; pull requests closed unmerged are included, since their reviews were paid for.
- An empty cell means there was nothing to record: a kind of file not touched, a round not run, a figure the cost footer could not report, or a review that gave no risk tier. Where the footer could report only some rounds, the total is the part it did report.

PROSE
    printf 'The same rows as CSV, for a spreadsheet: [%s](%s)\n\n' "$(basename "$csv")" "$(basename "$csv")"
    printf '%s\n%s\n' "$HEADER" "$SEPARATOR"
    [ -z "$sorted" ] || printf '%s\n' "$sorted"
    [ -z "$sorted" ] || totals_row "$sorted"
  } > "$file"
  write_csv "$sorted" > "$csv"
}

# update_page <file> — add the missing rows to the page at <file>, creating it if absent. Prints
# the numbers it added, one per line.
update_page() {
  local file=$1 existing='' known prs n added=() rows
  [ -f "$file" ] && existing=$(tr -d '\r' < "$file" | grep -E '^\| \[#[0-9]+\]' || true)
  # grep finds nothing on a new page; jq -s still answers [] for it.
  known=$(grep -oE '^\| \[#[0-9]+\]' <<<"$existing" | grep -oE '[0-9]+' | jq -s . || true)

  prs=$(gh pr list --state closed --limit 1000 \
    --json number,title,url,closedAt,mergedAt,body,author,isCrossRepository) \
    || die "could not list the closed pull requests."
  prs=$(jq -c --arg m "$FOOTER_MARKER" --arg a "$MAINTAINER" --argjson known "$known" '.[]
    | select(.author.login == $a and (.isCrossRepository | not))
    | select((.body // "") | contains($m))
    | select(.number as $n | $known | index($n) | not)' <<<"$prs") \
    || die "could not read the list of closed pull requests."

  rows=$existing
  while IFS= read -r pr; do
    [ -n "$pr" ] || continue
    # The marker test above is a cheap first cut; a body that only quotes the marker is dropped here.
    footer_costs "$(jq -r .body <<<"$pr")" >/dev/null || continue
    n=$(jq -r .number <<<"$pr")
    rows+="${rows:+$'\n'}$(build_row "$pr")"
    added+=("$n")
  done <<<"$prs"

  write_page "$file" "$rows"
  [ "${#added[@]}" -eq 0 ] || printf '%s\n' "${added[@]}"
}

main() {
  local page=''
  case "${1:-}" in
    --page) [ -n "${2:-}" ] || { usage; exit 2; }; page=$2 ;;
    '') ;;
    *) usage; exit 2 ;;
  esac
  command -v gh >/dev/null || die "gh is not on PATH."
  command -v jq >/dev/null || die "jq is not on PATH."

  # The default page is found from the script's own place, so the run works from any directory.
  [ -n "$page" ] || page="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/$PAGE_PATH"
  mkdir -p "$(dirname "$page")"
  local added; added=$(update_page "$page")
  if [ -n "$added" ]; then say "added $(wc -l <<<"$added") row(s) to $page: $(sed 's/^/#/' <<<"$added" | paste -sd' ')."
  else say "nothing new to add to $page."; fi
}

main "$@"
