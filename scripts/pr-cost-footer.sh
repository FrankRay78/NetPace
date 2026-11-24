#!/usr/bin/env bash
#
# pr-cost-footer.sh — record one review round's token usage and API cost in a footer at the end
# of a pull request's description.
#
# `/gaffer:review-pr` runs on the strongest model at high effort and fans out specialist and verifier
# subagents; `/gaffer:review-followup` adds up to two more rounds. None of that spend showed anywhere,
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
# against the head commit the calling workflow verified the review at, which is the same marker
# /gaffer:review-followup finds prior rounds by. That commit is passed in rather than read live: an
# author pushing between the review posting and this step would otherwise move the head away from
# the review, and the round would lose its row while the run stayed green. That
# test cannot tell this run's review from one an earlier run left at the same commit, so on its own
# it only rules out a head nobody has reviewed. The calling workflow's check — that the count of
# marked reviews went up — is what rules out a re-dispatch that posted nothing.
#
# WHY THE ROW NAME COMES FROM THE FOOTER AND NOT FROM THE ROUND. Counting the rows already in the
# footer makes the next name a function of what is written down, so a re-dispatched first review
# or a repeated follow-up adds the next row rather than overwriting one. The round number the
# command reasons about (`/gaffer:review-followup` step 2's cap) is about reviews posted; this is about
# rows recorded, and the two are deliberately not the same question.
#
# WHAT IT NEVER TOUCHES. Everything above the footer is the author's, and is written back unchanged
# bar trailing blank lines and carriage returns. Everything from the footer down belongs to this
# script and is regenerated, which is what keeps a description from collecting two footers. That
# rests on two things, both of which cost a description its contents when they were missing: the
# body is stripped of CRs before it is split, because GitHub hands back a description saved in the
# web UI with CRLF endings and the marker line would then match nothing; and the footer is found by
# the structure this script writes, not by the marker line alone, so an author who quotes the
# marker keeps the text below it.
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
#   Usage:  scripts/pr-cost-footer.sh <pull-request-number> <execution-file> <head-commit>

set -euo pipefail

# Above the horizontal rule rather than below it, so the split point renders as nothing and the
# description ends in the rule and the table.
MARKER='<!-- pr-cost-footer -->'
REVIEW_MARKER='<!-- review-pr -->'

usage() { echo "usage: scripts/pr-cost-footer.sh <pull-request-number> <execution-file> <head-commit>" >&2; }
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
    printf '%s' "$((10#${v%.*} * 100 + 10#${v#*.}))"
  else
    printf 'unknown'
  fi
}

# total_cell <formatter> <value…> — the Total row's cell for one column, formatted by <formatter>.
#
# A total that dropped an unknown round would read as the whole cycle's cost. The two columns are
# totalled separately, their gaps being independent: a run can report tokens and not cost.
total_cell() {
  local fmt=$1 sum=0 known=0 missing=0 value
  shift
  for value in "$@"; do
    # `10#` so a cell someone padded with a leading zero is read as decimal: bash reads `08` as
    # octal and aborts the script mid-run with a bare arithmetic error and no footer written.
    if [ "$value" = unknown ]; then missing=1; else sum=$((sum + 10#$value)); known=1; fi
  done
  if [ "$known" = 0 ]; then
    printf 'unknown'
  elif [ "$missing" = 1 ]; then
    printf '%s + unknown' "$("$fmt" "$sum")"
  else
    "$fmt" "$sum"
  fi
}

# The two halves of a description: everything before the footer is the author's, everything from
# the footer down is regenerated.
#
# Identified by the whole three-line shape, last occurrence winning: matching the bare marker
# split a description that merely quoted it. Exact line matches, not patterns.
split_body() {
  awk -v m="$MARKER" -v mode="$1" '
    { line[NR] = $0 }
    END {
      for (i = 1; i <= NR; i++)
        if (line[i] == m && line[i + 1] == "" && line[i + 2] == "---") s = i
      if (mode == "before") { for (i = 1; i < (s ? s : NR + 1); i++) print line[i] }
      else { for (i = s + 1; s && i <= NR; i++) print line[i] }
    }'
}
before_marker() { split_body before; }
after_marker() { split_body after; }

# read_usage <execution-file> — prints "<tokens>\t<cents>", either value `unknown`.
#
# `modelUsage` over `usage`, summed, because a review run spends on several models; cost takes the
# opposite precedence, the scalar `total_cost_usd` already covering every model. Cents round, not truncate.
#
# A file that never reached a result message needs no branch: every figure reads null and both
# cells come out `unknown` by the same route as a result carrying no usage.
#
# `-s` so the filter runs exactly once whatever the file holds. Without it an empty file gave jq
# no input, printing nothing and exiting 0, and `read` then hit EOF and killed the script.
read_usage() {
  local file=$1
  if [ ! -f "$file" ]; then
    printf 'unknown\tunknown\n'
    return
  fi
  jq -s -r '
    def numsum(a): (a | map(select(type == "number"))) as $n
      | if ($n | length) == 0 then null else ($n | add) end;
    def msgs: [ .[] | if type == "array" then .[]
      elif (type == "object") and ((.messages | type) == "array") then .messages[]
      else . end ];
    (msgs | map(select((type == "object") and (.type == "result"))) | last) as $r
    | (if ($r.modelUsage | type) == "object" then $r.modelUsage else {} end) as $m
    | numsum([$m | to_entries[] | .value
        | (.inputTokens, .outputTokens, .cacheReadInputTokens, .cacheCreationInputTokens)]) as $mt
    | numsum([$r.usage.input_tokens, $r.usage.output_tokens,
              $r.usage.cache_creation_input_tokens, $r.usage.cache_read_input_tokens]) as $ut
    | (if ($r.total_cost_usd | type) == "number" then $r.total_cost_usd
       else numsum([$m | to_entries[] | .value.costUSD]) end) as $cost
    | (if $cost == null then "unknown" else (($cost * 100) | round) end) as $cents
    | "\(($mt // $ut) // "unknown")\t\($cents)"
  ' "$file" || printf 'unknown\tunknown\n'
}

main() {
  local pr=${1:-} exec_file=${2:-} head=${3:-} repo pr_json body reviews want author
  # The execution file need not exist, `unknown` being a real answer, but the caller must name it.
  # A quoted variable that expanded to nothing satisfies this and records the round as unknown.
  [ "$#" -eq 3 ] || { usage; exit 1; }
  case "$pr" in
    '' | *[!0-9]*) usage; exit 1 ;;
  esac

  # A failed lookup prints nothing to stdout, so an empty answer covers both "gh could not say"
  # and "gh said nothing".
  repo=${GH_REPO:-$(gh repo view --json nameWithOwner --jq .nameWithOwner)}
  [ -n "$repo" ] || die "could not tell which repository to work in; set GH_REPO to owner/name."

  [ -n "$head" ] && [ "$head" != null ] \
    || die "no head commit was given for pull request #$pr; its description is unchanged."

  pr_json=$(gh pr view "$pr" --repo "$repo" --json body) \
    || die "could not read pull request #$pr; its description is unchanged."
  # Stripped of CRs: GitHub returns a description saved through the web UI with CRLF line endings,
  # and every line comparison the split makes below is exact.
  body=$(jq -r '.body // ""' <<<"$pr_json" | tr -d '\r') \
    || die "the description of pull request #$pr was unreadable; it is unchanged."

  reviews=$(gh api --paginate "repos/$repo/pulls/$pr/reviews" \
    --jq ".[] | [.commit_id, ((.body // \"\") | contains(\"$REVIEW_MARKER\"))] | @tsv") \
    || die "could not ask GitHub which reviews pull request #$pr carries; its description is unchanged."
  want="$head"$'\t'true
  if ! grep -qxF -- "$want" <<<"$reviews"; then
    say "no $REVIEW_MARKER review on pull request #$pr at $head — this run posted nothing, so no row is written."
    exit 0
  fi

  author=$(before_marker <<<"$body")

  local line name tok cost rest i tmp
  local -a row_name=() row_tokens=() row_cents=()
  while IFS= read -r line; do
    case "$line" in '|'*) ;; *) continue ;; esac
    # `rest` is read and unused on purpose: it absorbs the row's trailing pipe, so that `cost`
    # holds the cell rather than the cell plus the remainder of the line.
    IFS='|' read -r _ name tok cost rest <<<"$line"
    name=$(trim "${name:-}"); tok=$(trim "${tok:-}"); cost=$(trim "${cost:-}")
    # The heading, the separator under it and the Total row are not rounds.
    if [ -z "$name" ] || [ "$name" = Round ] || [ "$name" = Total ] \
      || [[ "$name" =~ ^:?-+:?$ ]]; then
      continue
    fi
    row_name+=("$name")
    row_tokens+=("$(parse_tokens "$tok")")
    row_cents+=("$(parse_cents "$cost")")
  done < <(after_marker <<<"$body")

  # Both are re-checked here rather than trusted: read_usage reads a file whose shape this script
  # does not own, so anything that is not a plain whole number is `unknown`, never a guess.
  local round_tokens round_cents
  IFS=$'\t' read -r round_tokens round_cents < <(read_usage "$exec_file")
  [[ "$round_tokens" =~ ^[0-9]+$ ]] || round_tokens=unknown
  [[ "$round_cents" =~ ^[0-9]+$ ]] || round_cents=unknown
  # An `unknown` row is also what a renamed action output looks like, and the footer cannot tell
  # the two apart. Saying it here puts the gap in the run's own log.
  if [ "$round_tokens" = unknown ] || [ "$round_cents" = unknown ]; then
    say "could not read this round's usage from '${exec_file:-}' — the row will say unknown."
  fi

  # These names count rows written, not the rounds /gaffer:review-followup counts, which calls the
  # first follow-up round 2 where its row here is `Review 1`.
  if [ "${#row_name[@]}" -eq 0 ]; then
    row_name+=("PR Review")
  else
    row_name+=("Review ${#row_name[@]}")
  fi
  row_tokens+=("$round_tokens")
  row_cents+=("$round_cents")

  tmp=$(mktemp)
  # shellcheck disable=SC2064
  trap "rm -f '$tmp'" EXIT
  # One blank line whatever the author left: command substitution has already dropped trailing
  # newlines, so the gap neither closes up nor grows a line per round.
  {
    if [ -n "$author" ]; then printf '%s\n\n' "$author"; fi
    printf '%s\n\n---\n\n' "$MARKER"
    printf '| Round | Tokens | API cost |\n| --- | --- | --- |\n'
    for ((i = 0; i < ${#row_name[@]}; i++)); do
      printf '| %s | %s | %s |\n' "${row_name[i]}" \
        "$(fmt_tokens "${row_tokens[i]}")" "$(fmt_cost "${row_cents[i]}")"
    done
    printf '| Total | %s | %s |\n' \
      "$(total_cell fmt_tokens "${row_tokens[@]}")" "$(total_cell fmt_cost "${row_cents[@]}")"
  } > "$tmp"

  gh pr edit "$pr" --repo "$repo" --body-file "$tmp" >/dev/null </dev/null \
    || die "could not update the description of pull request #$pr; it is unchanged, and this round went unrecorded."
  say "#$pr: recorded ${row_name[-1]} as $(fmt_tokens "$round_tokens") tokens, $(fmt_cost "$round_cents")."
}

main "$@"
