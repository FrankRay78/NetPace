# A Format Selector Is Not A Verbosity Level

**Intent:** Stop `--verbosity` presenting a format choice as a level on a scale. Its three values were three unrelated things: `Minimal` selected a different console writer, `Normal` was the absence of any choice, and `Debug` added two prose lines. Because the option took one value, `--verbosity Minimal --verbosity Debug` was not expressible — so a user could not ask for terse output *and* extra detail at once. That combination is what the `--diagnostics` work (#268) needs, so the switch had to be split before that work could have a sane surface.

**Behaviour:**

- Given no format switch, when NetPace runs, then it renders the rich terminal output — unchanged from `--verbosity Normal`.
- Given `--minimal`, when NetPace runs, then it writes the compact single-line result, byte-identical to what `--verbosity Minimal` produced, and composes with timestamp, unit, scale, system and test-skipping options the same way.
- Given two output-format switches, when NetPace parses them, then it exits `1` naming the conflict, rather than silently picking one.
- Given `--verbosity` with any value or none, when NetPace parses it, then it exits `1` saying the switch has been removed and pointing at `--help`.
- Given any invocation at all, when NetPace writes its output, then no bytes-downloaded or bytes-uploaded prose line appears.

**Constraints:**

- A breaking change to the CLI surface at version 1.0.0. Principle VII scopes semantic versioning to the `NetPace.Core` NuGet surface, which this does not touch, so whether the CLI break warrants a major bump is a maintainer call before release.
- `--verbosity` is a documented switch that may be sitting in someone's script or cron job. A generic `Unrecognized command or argument` tells that person nothing about what happened to it.
- `--json-pretty` is not a second format. It shapes the JSON format, so `--json --json-pretty` had to keep working while `--csv --json-pretty` became an error.

**Decisions:**

1. **Rejected: consolidate `--csv`, `--json` and `--minimal` into one `--format <value>` option.** It is the tidier surface, and it is the shape the retired `--verbosity` was reaching for. Rejected because it breaks two further switches that work correctly today in order to solve a problem this change does not have — the three peer flags are already unambiguous once only one may be chosen. Chose three boolean peers.

2. **Rejected: keep `--verbosity` as a deprecated alias that works for one release.** The usual kindness for a documented switch, and it was considered. Rejected because `--minimal` replaces only one of the three former values: an alias would have to guess what `Normal` and `Debug` now mean, and `Debug`'s behaviour is being deleted outright, not relocated. A switch that silently does something other than what it used to is worse than one that stops. Chose removal with an explanatory error.

3. **Rejected: delete the option and let the parser report it.** Simplest, and no code to carry. Rejected because the parser's generic unrecognised-argument message is exactly the outcome constraint two rules out. Chose a hidden `--verbosity` option, still registered so the parser recognises it, carrying a validator that rejects every invocation. Its argument arity is zero-or-one and its type is `string` so that no former value, and no value at all, can reach a built-in parse error ahead of the intended message — the error is identical in all five cases. `CustomHelpProvider` now filters `Hidden` options, because it had been rendering every registered option regardless.

4. **The error names no replacement.** `--minimal` would be the obvious thing to suggest, but it is right only for the person who was passing `Minimal`. Anyone who was passing `Normal` or `Debug` would be sent to a switch that does not do what they asked for. `--help` is the honest destination.

5. **Rejecting two format switches rides along with this change, deliberately.** It is a second behaviour change, and on its own it would not justify a break: today `--csv --json` quietly picks one and ignores the other, so a stray or mistyped flag in a script produces the wrong format indefinitely with nothing to signal it. Since the switch surface is already breaking here, fixing it now costs users one break instead of two.

6. **The Debug prose is removed, not relocated.** It is a weaker version of what `--diagnostics` (#268) will provide, and keeping it anywhere on stdout would make it impossible for that work to state that stdout is unaffected by the diagnostics switch.

**Date:** 2026-10-07
