# NetPace runs the gaffer plugin's commands, tracking its `main`

**Date:** 2026-11-01

## Intent

NetPace's harness commands come from the [gaffer](https://github.com/FrankRay78/gaffer-claude) plugin: the project settings declare its marketplace and enable it, `scripts/chain.sh` starts its stages as `/gaffer:build`, `/gaffer:verify` and `/gaffer:raise-pr` with the plugin loaded from a clone the runner fast-forwards to gaffer's `main` before each firing, and the four Claude CI workflows clone gaffer's `main` per job and read the command from that clone. Nothing headless depends on whatever copy is installed on a machine, and nothing in a pull request's head can choose which gaffer reviews it.

## Alternatives rejected

- **Pinning a gaffer commit** in settings, the chain and the workflows: a safer pace, at the cost of a bump pull request in NetPace for every command fix, while NetPace is the plugin's only consumer. Tracking `main` means a fix reaches NetPace on its merge in gaffer, whose `main` takes changes only by a checked pull request. Tighten to a `stable` branch, then a pin, if a second consumer or a bad merge makes the case.
- **Installing the plugin on the build machine** and letting the chain use that copy: a headless run would then depend on when someone last ran the install command. A clone the runner updates itself is a known commit, logged with each firing.
- **Invoking `/gaffer:<name>` as the whole prompt in CI**, as the chain does: the workflows need environmental deltas the chain does not, and reading the command file from the clone by path is the mechanism they already use for `.claude/commands/`, so only the path changes.

## Constraints the code will not make obvious

A verdict-schema bump lands in `chain.sh` before the writers change in gaffer, because the chain reads whatever `main` holds the next firing. The `extraKnownMarketplaces` entry makes Claude Code fetch gaffer when the project is opened and trusted, so a fresh clone needs no install step; CI loads it again by clone, and loading the same plugin twice is tolerated.
