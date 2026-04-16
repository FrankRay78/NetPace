#!/usr/bin/env bash
# Stages and commits the test-plan.md for the current feature branch (red phase).
# Reads feature directory from .specify/feature.json — works on any feature branch.
set -euo pipefail

BRANCH=$(git rev-parse --abbrev-ref HEAD)
FEATURE_DIR=$(python3 -c "import json; print(json.load(open('.specify/feature.json'))['feature_directory'])")
PLAN_FILE="${FEATURE_DIR}/test-plan.md"

if [[ ! -f "${PLAN_FILE}" ]]; then
  echo "Error: ${PLAN_FILE} not found. Run /speckit.testplan first." >&2
  exit 1
fi

git add "${PLAN_FILE}"
git commit -m "test: red phase - test plan for ${BRANCH}"
#git push -u origin "${BRANCH}"
