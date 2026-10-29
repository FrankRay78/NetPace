#!/usr/bin/env bash
#
# stage-flags.sh — print the claude flags for one stage, read from stage-models.tsv beside it.
#
#   Usage:  scripts/stage-flags.sh <stage>      e.g. `--model opus --effort high`
#
# A stage with no row, a row missing its effort, or a stage written twice fails with nothing on
# stdout, so a runner stops rather than start claude on whatever the default happens to be.
# Tests: scripts/stage-flags.tests.sh.

set -uo pipefail

TABLE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/stage-models.tsv"
stage=${1:-}
[ -n "$stage" ] || { echo "usage: stage-flags.sh <stage>" >&2; exit 2; }

rows=$(awk -v s="$stage" '$1 !~ /^#/ && $1 == s' "$TABLE")
count=$(grep -c . <<<"$rows")
[ "$count" = 1 ] || { echo "stage-flags: stage '$stage' has $count rows in $TABLE, not 1" >&2; exit 1; }

read -r _ model effort extra <<<"$rows"
if [ -z "$effort" ] || [ -n "$extra" ]; then
  echo "stage-flags: stage '$stage' in $TABLE needs exactly a model and an effort" >&2
  exit 1
fi
echo "--model $model --effort $effort"
