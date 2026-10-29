#!/usr/bin/env bash
#
# stage-flags.tests.sh — standalone test matrix for stage-flags.sh and the stage table it reads.
#
# The script turns a stage name into the model and effort flags a runner passes to claude. The
# lookup is proved against copies of the script beside fixture tables, so no case depends on the
# models the real table happens to name today. The last group checks the real table against the
# runners that read it: every stage a runner names has a row, and no runner names a model or an
# effort of its own, which is what keeps the table the one place the answer is written. Exits
# non-zero on any failure.
#
#   Usage:  scripts/stage-flags.tests.sh

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/.." && pwd)"
LOOKUP="$HERE/stage-flags.sh"

SB="$(mktemp -d)"
trap 'rm -rf "$SB"' EXIT

pass=0; fail=0; OUTPUT=; RC=
ok() { if eval "$2"; then echo "  ok   $1"; pass=$((pass+1)); else echo "  FAIL $1 -- rc=$RC output:[$OUTPUT]"; fail=$((fail+1)); fi; }

# table <contents> — a fresh copy of the script beside a fixture table holding <contents>.
table() { rm -rf "$SB/t"; mkdir -p "$SB/t"; cp "$LOOKUP" "$SB/t/stage-flags.sh"; printf '%s' "$1" > "$SB/t/stage-models.tsv"; }
# lookup <stage> — run the fixture copy. Sets OUTPUT (stdout and stderr) and RC.
lookup() { OUTPUT="$(bash "$SB/t/stage-flags.sh" "$@" 2>&1)"; RC=$?; }

echo "A stage in the table:"
table $'# stage  model  effort\nbuild    opus    high\nraise-pr\tsonnet\tmedium\n'
lookup build
ok "prints its model and effort as flags" '[ "$RC" = 0 ] && [ "$OUTPUT" = "--model opus --effort high" ]'
lookup raise-pr
ok "a tab-separated row reads the same" '[ "$RC" = 0 ] && [ "$OUTPUT" = "--model sonnet --effort medium" ]'

echo "A stage the table does not answer:"
lookup deploy
ok "fails, naming the stage" '[ "$RC" != 0 ] && grep -qF "deploy" <<<"$OUTPUT"'
ok "and prints no flags a runner could pass on" '! grep -qF -- "--model" <<<"$OUTPUT"'
lookup "#"
ok "a comment is not a stage" '[ "$RC" != 0 ]'
lookup
ok "no stage named at all fails" '[ "$RC" != 0 ]'
table $'build  opus\n'
lookup build
ok "a row with no effort fails rather than leave it to the default" '[ "$RC" != 0 ]'
table $'build  opus  high\nbuild  sonnet  low\n'
lookup build
ok "a stage written twice fails rather than pick one" '[ "$RC" != 0 ]'

echo "The real table and the runners that read it:"
# Every stage name a runner looks up: the workflows' calls to the script and the chain's stages.
names=$(cd "$REPO" && { git grep -hoE 'stage-flags\.sh"? [a-z-]+' -- .github/workflows | awk '{print $2}'; \
  grep -oE '^  run_stage [0-9]+ [a-z-]+' scripts/chain.sh | awk '{print $3}'; } | sort -u)
ok "the runners name at least one stage" '[ -n "$names" ]'
missing=
for n in $names; do bash "$LOOKUP" "$n" >/dev/null 2>&1 || missing="$missing $n"; done
OUTPUT=$missing
ok "every stage a runner names has a row" '[ -z "$missing" ]'
# A model or effort written as a literal value anywhere a runner is written is an answer the table
# does not give; a flag whose value is a variable is the runner passing the table's answer on. The
# test matrices are left out, as their stubs record the flags the chain passed, and so is the
# lookup itself, whose usage line shows what it prints.
OUTPUT=$(cd "$REPO" && git grep -nE -- "--(model|effort)[ =]+['\"]?[A-Za-z0-9]" -- .github/workflows 'scripts/*.sh' ':!*.tests.sh' ':!scripts/stage-flags.sh')
ok "no runner names a model or effort of its own" '[ -z "$OUTPUT" ]'

echo ""
echo "RESULT: $pass passed, $fail failed"
[ "$fail" = 0 ]
