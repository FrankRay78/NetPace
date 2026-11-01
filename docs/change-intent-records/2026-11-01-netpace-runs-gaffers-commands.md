# NetPace runs the gaffer plugin's commands, tracking its `main`

**Date:** 2026-11-01

## Intent

NetPace's harness commands come from the [gaffer](https://github.com/FrankRay78/gaffer-claude) plugin: the project settings declare its marketplace and enable it, `scripts/chain.sh` starts its stages as `/gaffer:build`, `/gaffer:verify` and `/gaffer:raise-pr` with the plugin loaded from a clone the runner fast-forwards to gaffer's `main` before each firing, and the four Claude CI workflows clone gaffer's `main` per job and read the command from that clone. Nothing headless depends on whatever copy is installed on a machine, and nothing in the checkout can choose which gaffer reviews a pull request — except the workflow file itself: `review-pr` and `review-followup` run on `pull_request`, for which GitHub runs the workflow file from the pull request, so a pull request that edits the clone step chooses the gaffer, and the prompt, for its own review.

## Alternatives rejected

- **Pinning a gaffer commit** in settings, the chain and the workflows: a safer pace, at the cost of a bump pull request in NetPace for every command fix, while NetPace is the plugin's only consumer. Tracking `main` means a fix reaches NetPace on its merge in gaffer, whose `main` takes changes only by a checked pull request. Tighten to a `stable` branch, then a pin, if a second consumer or a bad merge makes the case.
- **Installing the plugin on the build machine** and letting the chain use that copy: a headless run would then depend on when someone last ran the install command. A clone the runner updates itself is a known commit, logged with each firing.
- **Invoking `/gaffer:<name>` as the whole prompt in CI**, as the chain does: the workflows need environmental deltas the chain does not, and reading the command file from the clone by path is the mechanism they already use for `.claude/commands/`, so only the path changes.

## Constraints the code will not make obvious

The `extraKnownMarketplaces` entry makes Claude Code fetch gaffer's marketplace when the project is opened and trusted, and `enabledPlugins` enables it, but neither installs it: each machine runs `claude plugin install gaffer@gaffer-claude` once, after which every clone offers the commands; CI loads it again by clone, and loading the same plugin twice is tolerated.
