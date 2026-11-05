#!/usr/bin/env bash
#
# clear-workflow-labels.tests.sh — standalone test matrix for clear-workflow-labels.sh.
#
# The script's job is deciding: whether the pull request was merged at all, which issues it
# closes in this repository, which of those issues carry a workflow label, and what to leave
# alone. All of that is provable from outside with a stub `gh` first on PATH, so nothing reaches
# GitHub, no label is really removed and nothing is spent. Exits non-zero on any failure. Run it
# after any edit to the script.
#
# The stub holds one pull request in pr.json — whether it was merged, and the issues it closes
# with their repository and labels — and the repository's issues in issues.json. Label writes are
# applied to issues.json, so what the script did is read back from the state it changed rather
# than from its own output. A gh-fail file makes every call fail, as an outage would; a
# gh-fail-edit-<n> file fails only the write to that one issue.
#
# The merged check lives in the script rather than in the workflow's `if:` so that it is provable
# here. The one thing this matrix cannot reach is the trigger itself — that a merge into main
# starts a run at all, and that a fork's pull request does not — because the tool that decides it
# is GitHub Actions (CONVENTIONS.md, Test-driven development, configuration/tooling).
#
#   Usage:  scripts/clear-workflow-labels.tests.sh

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT="$HERE/clear-workflow-labels.sh"

SB="$(mktemp -d)"
trap 'rm -rf "$SB"' EXIT

mkdir -p "$SB/bin"
cat > "$SB/bin/gh" <<'STUB'
#!/usr/bin/env bash
{ for a in "$@"; do printf '%s|' "$a"; done; printf '\n'; } >> "$STUB_DIR/gh-log"
if [ -f "$STUB_DIR/gh-fail" ]; then echo "gh: HTTP 502: Bad Gateway" >&2; exit 1; fi
args=("$@")
opts() { local i; for ((i = 0; i < ${#args[@]}; i++)); do [ "${args[i]}" = "$1" ] && printf '%s\n' "${args[i+1]}"; done; }
case "$1 $2" in
  "api graphql")
    jq -c '{data: {repository: {pullRequest: {merged: .merged, closingIssuesReferences: {nodes:
      (.closing | map({number, repository: {nameWithOwner: .repo},
                        labels: {nodes: (.labels | map({name: .}))}}))}}}}}' "$STUB_DIR/pr.json" ;;
  "issue edit")
    n=$3
    [ -f "$STUB_DIR/gh-fail-edit-$n" ] && { echo "gh: could not edit issue #$n" >&2; exit 1; }
    removed=$(opts --remove-label | jq -Rs -c 'split("\n") | map(select(length > 0))')
    jq --argjson n "$n" --argjson r "$removed" \
      'map(if .number == $n then .labels -= $r else . end)' \
      "$STUB_DIR/issues.json" > "$STUB_DIR/issues.tmp" && mv "$STUB_DIR/issues.tmp" "$STUB_DIR/issues.json" ;;
  *)
    echo "stub gh: unexpected call: $*" >&2; exit 1 ;;
esac
STUB
chmod +x "$SB/bin/gh"

pass=0; fail=0; cases=0
ok() { if eval "$2"; then echo "  ok   $1"; pass=$((pass+1)); else echo "  FAIL $1 -- rc=$RC output:[$OUTPUT]"; fail=$((fail+1)); fi; }

# A fresh case: an empty stub directory, a merged pull request closing nothing, no issues.
new_case() {
  cases=$((cases+1))
  export STUB_DIR="$SB/case$cases"
  mkdir -p "$STUB_DIR"
  echo '{"merged": true, "closing": []}' > "$STUB_DIR/pr.json"
  echo '[]' > "$STUB_DIR/issues.json"
}

# pr <merged> <closing-json> — the pull request under test, as GitHub would answer for it.
pr() { jq -n --argjson m "$1" --argjson c "$2" '{merged: $m, closing: $c}' > "$STUB_DIR/pr.json"; }
# issues <json> — the repository's issues, as [{number, labels}].
issues() { printf '%s' "$1" > "$STUB_DIR/issues.json"; }

# run [args…] — one run of the script. Sets OUTPUT (stdout and stderr) and RC.
run() {
  OUTPUT="$(GH_REPO=o/r PATH="$SB/bin:$PATH" bash "$SCRIPT" "$@" </dev/null 2>&1)"; RC=$?
}

labels_of() { jq -r --argjson n "$1" '.[] | select(.number == $n) | .labels | join(",")' "$STUB_DIR/issues.json"; }
# The count is 0, not the empty string, when no call has been logged at all.
writes() { local c; c=$(grep -cE '^issue\|edit\|' "$STUB_DIR/gh-log" 2>/dev/null); echo "${c:-0}"; }
# asked <field>=<value> — true when the GraphQL call carried that argument.
asked() { grep -qF "|$1|" "$STUB_DIR/gh-log"; }

echo "A merged pull request clears the workflow labels off the issue it closes:"
new_case
pr true '[{"number":5,"repo":"o/r","labels":["ready","feature"]}]'
issues '[{"number":5,"labels":["ready","feature"]}]'
run 5
ok "exits 0" '[ "$RC" = 0 ]'
ok "the workflow label is gone" '[ "$(labels_of 5)" = feature ]'
ok "and GitHub was asked about that pull request, in this repository" \
  'asked number=5 && asked owner=o && asked name=r'

new_case
pr true '[{"number":9,"repo":"o/r","labels":["ready","review","refine","confirm","needs answers","parked","needs triage","bug","documentation"]}]'
issues '[{"number":9,"labels":["ready","review","refine","confirm","needs answers","parked","needs triage","bug","documentation"]}]'
run 9
ok "all seven go, in one write" '[ "$RC" = 0 ] && [ "$(writes)" = 1 ]'
ok "and every other label is left exactly as it was" '[ "$(labels_of 9)" = "bug,documentation" ]'

echo "Every issue the merge closes is cleared:"
new_case
pr true '[{"number":4,"repo":"o/r","labels":["confirm"]},{"number":6,"repo":"o/r","labels":["needs answers","bug"]}]'
issues '[{"number":4,"labels":["confirm"]},{"number":6,"labels":["needs answers","bug"]},{"number":7,"labels":["ready"]}]'
run 4
ok "both closed issues are cleared" '[ "$RC" = 0 ] && [ -z "$(labels_of 4)" ] && [ "$(labels_of 6)" = bug ]'
ok "an issue the pull request does not close keeps its labels" '[ "$(labels_of 7)" = ready ]'

echo "Nothing to do is not a failure:"
new_case
pr true '[{"number":3,"repo":"o/r","labels":["bug","feature"]}]'
issues '[{"number":3,"labels":["bug","feature"]}]'
run 3
ok "an issue carrying none of the seven is left as it is, with no write at all" \
  '[ "$RC" = 0 ] && [ "$(writes)" = 0 ] && [ "$(labels_of 3)" = "bug,feature" ]'

# A workflow label name is matched whole: `ready for release` is somebody else's label.
new_case
pr true '[{"number":12,"repo":"o/r","labels":["ready for release","reviewed","ready"]}]'
issues '[{"number":12,"labels":["ready for release","reviewed","ready"]}]'
run 12
ok "a label that merely contains a workflow label name is left alone" \
  '[ "$RC" = 0 ] && [ "$(labels_of 12)" = "ready for release,reviewed" ]'

new_case
pr true '[]'
issues '[{"number":2,"labels":["ready"]}]'
run 11
ok "a merged pull request that closes no issue completes without error, changing nothing" \
  '[ "$RC" = 0 ] && [ "$(writes)" = 0 ] && [ "$(labels_of 2)" = ready ]'

echo "A pull request closed without being merged changes no labels:"
new_case
pr false '[{"number":5,"repo":"o/r","labels":["ready","feature"]}]'
issues '[{"number":5,"labels":["ready","feature"]}]'
run 5
ok "exits 0 and says so" '[ "$RC" = 0 ] && grep -qi "merge" <<<"$OUTPUT"'
ok "the labels are untouched" '[ "$(writes)" = 0 ] && [ "$(labels_of 5)" = "ready,feature" ]'

echo "An issue in another repository is not touched:"
new_case
pr true '[{"number":5,"repo":"someone/else","labels":["ready"]},{"number":8,"repo":"o/r","labels":["parked"]}]'
issues '[{"number":5,"labels":["ready"]},{"number":8,"labels":["parked"]}]'
run 5
ok "only this repository's issue is cleared" \
  '[ "$RC" = 0 ] && [ "$(writes)" = 1 ] && [ "$(labels_of 5)" = ready ] && [ -z "$(labels_of 8)" ]'

echo "A failed removal is ignored and stops nothing:"
new_case
pr true '[{"number":4,"repo":"o/r","labels":["ready"]},{"number":6,"repo":"o/r","labels":["review"]}]'
issues '[{"number":4,"labels":["ready"]},{"number":6,"labels":["review"]}]'
touch "$STUB_DIR/gh-fail-edit-4"
run 4
ok "the run still succeeds" '[ "$RC" = 0 ]'
ok "the issue that could not be cleared keeps its labels" '[ "$(labels_of 4)" = ready ]'
ok "and the other issue is cleared anyway" '[ -z "$(labels_of 6)" ]'
# Tolerating the failure is only defensible because it is reported, so the report is asserted
# too: without this, deleting the warning entirely leaves the matrix green.
ok "the write was attempted, and the failure is reported as a warning naming the issue" \
  '[ "$(writes)" = 2 ] && grep -q "::warning::" <<<"$OUTPUT" && grep -q "#4" <<<"$OUTPUT"'

echo "An unanswerable question fails loudly:"
new_case
issues '[{"number":5,"labels":["ready"]}]'
touch "$STUB_DIR/gh-fail"
run 5
ok "a failed query exits non-zero and changes nothing" \
  '[ "$RC" != 0 ] && [ "$(writes)" = 0 ] && [ "$(labels_of 5)" = ready ]'

# A pull request number that resolves to nothing answers with a null `merged` and exit 0, so the
# script must refuse rather than guess which way a merge went.
new_case
pr null '[{"number":5,"repo":"o/r","labels":["ready"]}]'
issues '[{"number":5,"labels":["ready"]}]'
run 5
ok "an answer that does not say whether it merged exits non-zero and changes nothing" \
  '[ "$RC" != 0 ] && [ "$(writes)" = 0 ] && [ "$(labels_of 5)" = ready ]'

echo "Wrong invocation is refused:"
new_case
issues '[{"number":5,"labels":["ready"]}]'
run
ok "no pull request number exits 1 with usage, changing nothing" \
  '[ "$RC" = 1 ] && grep -q usage <<<"$OUTPUT" && [ "$(labels_of 5)" = ready ]'
run not-a-number
ok "a non-numeric argument exits 1 with usage, changing nothing" \
  '[ "$RC" = 1 ] && grep -q usage <<<"$OUTPUT" && [ "$(labels_of 5)" = ready ]'

echo ""
echo "RESULT: $pass passed, $fail failed"
[ "$fail" = 0 ]
