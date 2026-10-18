#!/usr/bin/env bash
#
# clear-workflow-labels.sh — take the issue-workflow labels off every issue a merged pull
# request closed.
#
# The seven labels below say where an *open* issue sits in the draft → review → confirm → build
# chain. Once a merge closes the issue they mean nothing, but they stay on it, so a label view
# such as "all `ready` issues" answers with closed ones too unless whoever asked remembered
# `is:open`. This runs once per merge and clears them. Every other label is left alone.
#
# WHY GITHUB IS ASKED WHICH ISSUES CLOSE. `closingIssuesReferences` is the same question
# .github/workflows/pr-issue-link.yml puts, and for the same reason: it covers a closing keyword
# in the body and an issue attached through the Development sidebar, and it excludes a bare `#N`
# or `Refs #N`, which closes nothing and whose labels must therefore survive. Working it out from
# the body instead would get both ends of that wrong.
#
# WHY THE MERGE CHECK IS HERE. A pull request closed without being merged must change no labels.
# The workflow could filter that out in its `if:`, but then the rule would live where only a real
# merge can exercise it; asking GitHub for `merged` in the query it already makes costs one field
# and makes the rule provable from clear-workflow-labels.tests.sh.
#
# WHAT IT COSTS. One GraphQL query, plus one label write per closed issue that still carries a
# workflow label — none for an issue that carries none.
#
# A write that fails is reported on the run's log and otherwise ignored: the next issue is still
# cleared and the run still succeeds. A label left behind is a tidy-up missed, not damage, and it
# is not worth failing a run after the merge has already happened. A query that fails is the
# other way round — nothing can be decided without it, so the run fails and says so.
#
# Prerequisites: gh and jq on PATH, gh signed in with permission to edit issues. GH_REPO names
# the repository (`owner/name`); gh's own default applies when it is unset. Tests:
# scripts/clear-workflow-labels.tests.sh. Called by .github/workflows/clear-workflow-labels.yml.
#
#   Usage:  scripts/clear-workflow-labels.sh <pull-request-number>

# No -e: a failed label write is reported by name below rather than killing the run silently.
set -uo pipefail

# The issue-chain labels, and only those. Adding a label here is all it takes to have merges
# clear it too.
WORKFLOW_LABELS=(ready review refine confirm "needs answers" parked "needs triage")

usage() { echo "usage: scripts/clear-workflow-labels.sh <pull-request-number>" >&2; }
say() { echo "clear-workflow-labels: $*"; }
die() { echo "clear-workflow-labels: $*" >&2; exit 1; }

main() {
  local pr=${1:-} repo response merged closing n label wanted
  case "$pr" in
    '' | *[!0-9]*) usage; exit 1 ;;
  esac

  repo=${GH_REPO:-$(gh repo view --json nameWithOwner --jq .nameWithOwner)} \
    || die "could not tell which repository to work in; set GH_REPO to owner/name."
  [ -n "$repo" ] || die "could not tell which repository to work in; set GH_REPO to owner/name."

  response=$(gh api graphql \
    -f query='query($owner: String!, $name: String!, $number: Int!) {
      repository(owner: $owner, name: $name) {
        pullRequest(number: $number) {
          merged
          closingIssuesReferences(first: 100) {
            nodes { number repository { nameWithOwner } labels(first: 100) { nodes { name } } }
          }
        }
      }
    }' \
    -f owner="${repo%/*}" -f name="${repo#*/}" -F number="$pr") \
    || die "could not ask GitHub which issues pull request #$pr closes; no labels changed."

  merged=$(jq -r '.data.repository.pullRequest.merged' <<<"$response") \
    || die "GitHub's answer about pull request #$pr was unreadable; no labels changed."
  case "$merged" in
    true) ;;
    false) say "pull request #$pr was closed without being merged — no labels changed."; exit 0 ;;
    *) die "could not tell whether pull request #$pr was merged; no labels changed." ;;
  esac

  # One line per closed issue in this repository: its number, then its labels, tab-separated.
  # An issue in another repository is dropped — this run's token speaks for this one only.
  closing=$(jq -r --arg repo "$repo" '.data.repository.pullRequest.closingIssuesReferences.nodes[]
      | select(.repository.nameWithOwner == $repo)
      | [(.number | tostring)] + (.labels.nodes | map(.name)) | @tsv' <<<"$response") \
    || die "the list of issues pull request #$pr closes was unreadable; no labels changed."

  if [ -z "$closing" ]; then
    say "pull request #$pr closes no issue in $repo — nothing to do."
    exit 0
  fi

  local -a fields remove
  while IFS=$'\t' read -r -a fields; do
    [ "${#fields[@]}" -gt 0 ] || continue
    n=${fields[0]}
    remove=()
    for label in "${fields[@]:1}"; do
      for wanted in "${WORKFLOW_LABELS[@]}"; do
        [ "$label" = "$wanted" ] && remove+=(--remove-label "$label")
      done
    done
    if [ "${#remove[@]}" -eq 0 ]; then
      say "#$n carries no workflow label — left as it is."
      continue
    fi
    # One write per issue, so a partial removal cannot leave the issue half-cleared.
    if gh issue edit "$n" "${remove[@]}" >/dev/null </dev/null; then
      say "#$n: cleared ${remove[*]//--remove-label /}"
    else
      say "#$n: could not clear its workflow labels — left as it is, and the run carries on."
    fi
  done <<<"$closing"
}

main "$@"
