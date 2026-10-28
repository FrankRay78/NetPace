#!/usr/bin/env bash
#
# pr-cost-footer.sh — record one review round's token usage and API cost in a footer at the end
# of a pull request's description.
#
# `/review-pr` runs on the strongest model at high effort and fans out specialist and verifier
# subagents; `/review-followup` adds up to two more rounds. None of that spend showed anywhere,
# so the cost of reviewing a pull request could not be read off the pull request, nor compared
# between pull requests to judge whether a change to the review rules paid for itself. This adds
# one row per round to a table at the foot of the description, and keeps a Total row under it, so
# a merged pull request carries the cost of its own review cycle.
#
# WHY A ROW IS ONLY WRITTEN FOR A ROUND THAT POSTED. The action exits successfully whenever the
# model finishes its turn, so a run that posted no review is otherwise indistinguishable from one
# that did — and a follow-up refused by the three-round cap posts nothing by design. A round that
# left no review is not a review round, and charging the pull request for it would make the
# footer a record of runs rather than of reviews. The test is a `<!-- review-pr -->` review
# against the head commit, which is the same marker /review-followup finds prior rounds by.
#
# WHY THE ROW NAME COMES FROM THE FOOTER AND NOT FROM THE ROUND. Counting the rows already in the
# footer makes the next name a function of what is written down, so a re-dispatched first review
# or a repeated follow-up adds the next row rather than overwriting one. The round number the
# command reasons about (`/review-followup` step 2's cap) is about reviews posted; this is about
# rows recorded, and the two are deliberately not the same question.
#
# WHAT IT NEVER TOUCHES. Everything above the marker is the author's, and is written back
# unchanged bar trailing blank lines. Everything from the marker down belongs to this script and
# is regenerated, which is what keeps a description from collecting two footers.
#
# UNKNOWN IS A REAL ANSWER. The run's totals come from the action's `execution_file`, whose shape
# this script does not control. When it cannot be read, or carries no result message, or carries
# no usage figures, the round's cells say `unknown` and the Total says what it is missing. The
# review has already posted by then, and a plausible wrong number is worse than an admitted gap.
#
# Prerequisites: gh and jq on PATH, gh signed in with permission to edit pull requests. GH_REPO
# names the repository (`owner/name`); gh's own default applies when it is unset. Tests:
# scripts/pr-cost-footer.tests.sh. Called by .github/workflows/review-pr.yml and
# .github/workflows/review-followup.yml, which share one concurrency group so that a first
# review and a follow-up never rewrite the same description at once.
#
#   Usage:  scripts/pr-cost-footer.sh <pull-request-number> <execution-file>

set -euo pipefail

# The footer's own marker. It sits above the horizontal rule rather than below it so the split
# point is a line that renders as nothing, leaving the rendered description ending in the rule
# and the table.
MARKER='<!-- pr-cost-footer -->'
NOTE="<sub>Counts only review rounds run in GitHub Actions; a round run by hand adds no row. API cost is what these tokens would cost at Anthropic's published API prices — the review runs on a Claude subscription, so nothing was billed at that rate.</sub>"
REVIEW_MARKER='<!-- review-pr -->'

usage() { echo "usage: scripts/pr-cost-footer.sh <pull-request-number> <execution-file>" >&2; }
say() { echo "pr-cost-footer: $*"; }
die() { echo "pr-cost-footer: $*" >&2; exit 1; }

trim() {
  local s=$1
  s="${s#"${s%%[![:space:]]*}"}"
  s="${s%"${s##*[![:space:]]}"}"
  printf '%s' "$s"
}

# 1234567 -> 1,234,567. A row of millions of cache-read tokens is unreadable ungrouped.
commafy() {
  local s=$1 out=''
  while [ "${#s}" -gt 3 ]; do
    out=",${s: -3}$out"
    s=${s:0:${#s}-3}
  done
  printf '%s%s' "$s" "$out"
}

fmt_tokens() {
  if [ "$1" = unknown ]; then printf 'unknown'; else commafy "$1"; fi
}

# Cost is carried in whole cents everywhere but the cell it is printed into, so that summing the
# rows is exact integer arithmetic over the figures a reader can see.
fmt_cost() {
  if [ "$1" = unknown ]; then printf 'unknown'; return; fi
  printf '$%s.%02d' "$(commafy "$(($1 / 100))")" "$(($1 % 100))"
}

# The inverse of the two above, so an earlier round's row is read back from the table rather than
# from state hidden in a comment that could disagree with what the table shows.
parse_tokens() {
  local v=${1//,/}
  if [[ "$v" =~ ^[0-9]+$ ]]; then printf '%s' "$v"; else printf 'unknown'; fi
}

parse_cents() {
  local v=${1//,/}
  v=${v#\$}
  if [[ "$v" =~ ^[0-9]+\.[0-9][0-9]$ ]]; then
    printf '%s' "$((${v%.*} * 100 + 10#${v#*.}))"
  else
    printf 'unknown'
  fi
}

# read_usage <execution-file> — prints "<tokens>\t<cost-usd>", either value `unknown`.
#
# The action writes the run's whole message list; its final result message carries the totals.
# `modelUsage` is preferred over `usage` because it is keyed by model, and a review run spends on
# several — the orchestrator plus every specialist and verifier subagent it started — so summing
# its entries is what makes a row cover the whole run rather than one model's share of it. Every
# token the run was charged for counts, cache reads and cache writes included.
read_usage() {
  local file=$1
  if [ ! -f "$file" ]; then
    printf 'unknown\tunknown\n'
    return
  fi
  jq -r '
    def numsum(a): (a | map(select(type == "number"))) as $n
      | if ($n | length) == 0 then null else ($n | add) end;
    def msgs: if type == "array" then .
      elif (type == "object") and ((.messages | type) == "array") then .messages
      else [.] end;
    (msgs | map(select((type == "object") and (.type == "result"))) | last) as $r
    | if $r == null then "unknown\tunknown"
      else
        (if ($r.modelUsage | type) == "object" then $r.modelUsage else {} end) as $m
        | numsum([$m | to_entries[] | .value
            | (.inputTokens, .outputTokens, .cacheReadInputTokens, .cacheCreationInputTokens)]) as $mt
        | numsum([$r.usage.input_tokens, $r.usage.output_tokens,
                  $r.usage.cache_creation_input_tokens, $r.usage.cache_read_input_tokens]) as $ut
        | (if ($r.total_cost_usd | type) == "number" then $r.total_cost_usd
           else numsum([$m | to_entries[] | .value.costUSD]) end) as $cost
        | "\(($mt // $ut) // "unknown")\t\($cost // "unknown")"
      end
  ' "$file" 2>/dev/null || printf 'unknown\tunknown\n'
}

# Dollars as the execution file reports them, to whole cents. Rejects anything that is not a
# plain number, so a null, a string or an exponent becomes `unknown` rather than 0.
usd_to_cents() {
  if [[ ! "$1" =~ ^[0-9]+(\.[0-9]+)?$ ]]; then printf 'unknown'; return; fi
  awk -v v="$1" 'BEGIN { printf "%.0f", v * 100 }'
}

main() {
  local pr=${1:-} exec_file=${2:-} repo pr_json head body author reviews want
  # Two arguments, always. The execution file need not exist — `unknown` is a real answer, and
  # the review it belongs to has already posted — but the caller must still name it, so a step
  # wired up without it is caught here rather than silently recording every round as unknown.
  [ "$#" -eq 2 ] || { usage; exit 1; }
  case "$pr" in
    '' | *[!0-9]*) usage; exit 1 ;;
  esac

  # A failed lookup prints nothing to stdout, so an empty answer covers both "gh could not say"
  # and "gh said nothing".
  repo=${GH_REPO:-$(gh repo view --json nameWithOwner --jq .nameWithOwner)}
  [ -n "$repo" ] || die "could not tell which repository to work in; set GH_REPO to owner/name."

  pr_json=$(gh pr view "$pr" --repo "$repo" --json body,headRefOid) \
    || die "could not read pull request #$pr; its description is unchanged."
  head=$(jq -r '.headRefOid' <<<"$pr_json") \
    || die "GitHub's answer about pull request #$pr was unreadable; its description is unchanged."
  [ -n "$head" ] && [ "$head" != null ] \
    || die "could not tell which commit is the head of pull request #$pr; its description is unchanged."
  body=$(jq -r '.body // ""' <<<"$pr_json") \
    || die "the description of pull request #$pr was unreadable; it is unchanged."

  reviews=$(gh api --paginate "repos/$repo/pulls/$pr/reviews" \
    --jq ".[] | [.commit_id, ((.body // \"\") | contains(\"$REVIEW_MARKER\"))] | @tsv") \
    || die "could not ask GitHub which reviews pull request #$pr carries; its description is unchanged."
  want=$(printf '%s\t%s' "$head" true)
  if ! grep -qxF -- "$want" <<<"$reviews"; then
    say "no $REVIEW_MARKER review on pull request #$pr at $head — this run posted nothing, so no row is written."
    exit 0
  fi

  # Everything before the marker is the author's; everything from it down is regenerated. The
  # split is an exact line match, not a pattern, so nothing in a description can be mistaken for
  # the marker by a metacharacter.
  author=$(awk -v m="$MARKER" '$0 == m { exit } { print }' <<<"$body")

  local -a ROW_NAME=() ROW_TOKENS=() ROW_CENTS=()
  local line name tok cost rest
  while IFS= read -r line; do
    case "$line" in '|'*) ;; *) continue ;; esac
    IFS='|' read -r _ name tok cost rest <<<"$line"
    name=$(trim "${name:-}"); tok=$(trim "${tok:-}"); cost=$(trim "${cost:-}")
    case "$name" in '' | Round | Total) continue ;; esac
    if [[ "$name" =~ ^:?-+:?$ ]]; then continue; fi
    ROW_NAME+=("$name")
    ROW_TOKENS+=("$(parse_tokens "$tok")")
    ROW_CENTS+=("$(parse_cents "$cost")")
  done < <(awk -v m="$MARKER" 'f { print } $0 == m { f = 1 }' <<<"$body")

  local round_tokens round_cost round_cents
  IFS=$'\t' read -r round_tokens round_cost < <(read_usage "$exec_file")
  [[ "$round_tokens" =~ ^[0-9]+$ ]] || round_tokens=unknown
  round_cents=$(usd_to_cents "$round_cost")

  # Row 1 is the review that runs when the pull request opens; every later row is a follow-up
  # round, numbered from one as /review-followup numbers its own rounds.
  if [ "${#ROW_NAME[@]}" -eq 0 ]; then
    ROW_NAME+=("PR Review")
  else
    ROW_NAME+=("Review ${#ROW_NAME[@]}")
  fi
  ROW_TOKENS+=("$round_tokens")
  ROW_CENTS+=("$round_cents")

  local total_tokens=0 total_cents=0 known_tokens=0 known_cents=0 i
  for ((i = 0; i < ${#ROW_NAME[@]}; i++)); do
    if [ "${ROW_TOKENS[i]}" != unknown ]; then
      total_tokens=$((total_tokens + ROW_TOKENS[i]))
      known_tokens=$((known_tokens + 1))
    fi
    if [ "${ROW_CENTS[i]}" != unknown ]; then
      total_cents=$((total_cents + ROW_CENTS[i]))
      known_cents=$((known_cents + 1))
    fi
  done

  # A total that silently dropped an unknown round would read as the cost of the whole cycle, so
  # it carries what it could add and admits what it could not.
  local total_tokens_cell total_cost_cell
  if [ "$known_tokens" -eq 0 ]; then
    total_tokens_cell=unknown
  elif [ "$known_tokens" -lt "${#ROW_NAME[@]}" ]; then
    total_tokens_cell="$(fmt_tokens "$total_tokens") + unknown"
  else
    total_tokens_cell=$(fmt_tokens "$total_tokens")
  fi
  if [ "$known_cents" -eq 0 ]; then
    total_cost_cell=unknown
  elif [ "$known_cents" -lt "${#ROW_NAME[@]}" ]; then
    total_cost_cell="$(fmt_cost "$total_cents") + unknown"
  else
    total_cost_cell=$(fmt_cost "$total_cents")
  fi

  local tmp
  tmp=$(mktemp)
  # shellcheck disable=SC2064
  trap "rm -f '$tmp'" EXIT
  # One blank line between the author's last line and the marker, whatever the author left —
  # command substitution has already dropped any trailing newlines, so the gap neither closes up
  # nor grows a line per round.
  {
    if [ -n "$author" ]; then printf '%s\n\n' "$author"; fi
    printf '%s\n\n---\n\n' "$MARKER"
    printf '| Round | Tokens | API cost |\n| --- | --- | --- |\n'
    for ((i = 0; i < ${#ROW_NAME[@]}; i++)); do
      printf '| %s | %s | %s |\n' "${ROW_NAME[i]}" \
        "$(fmt_tokens "${ROW_TOKENS[i]}")" "$(fmt_cost "${ROW_CENTS[i]}")"
    done
    printf '| Total | %s | %s |\n\n%s\n' "$total_tokens_cell" "$total_cost_cell" "$NOTE"
  } > "$tmp"

  gh pr edit "$pr" --repo "$repo" --body-file "$tmp" >/dev/null </dev/null \
    || die "could not update the description of pull request #$pr; it is unchanged, and this round went unrecorded."
  say "#$pr: recorded ${ROW_NAME[-1]} as $(fmt_tokens "$round_tokens") tokens, $(fmt_cost "$round_cents")."
}

main "$@"
