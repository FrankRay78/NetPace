# Stages and commits the test-plan.md for the current feature branch (red phase).
# Reads feature directory from .specify/feature.json — works on any feature branch.
$ErrorActionPreference = 'Stop'

$branch = git rev-parse --abbrev-ref HEAD
$featureJson = Get-Content '.specify/feature.json' -Raw | ConvertFrom-Json
$planFile = Join-Path $featureJson.feature_directory 'test-plan.md'

if (-not (Test-Path $planFile)) {
    Write-Error "Error: $planFile not found. Run /speckit.testplan first."
    exit 1
}

git add $planFile
git commit -m "test: red phase - test plan for $branch"
#git push -u origin $branch
