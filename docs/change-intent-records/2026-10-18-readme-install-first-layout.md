# README install-first layout

**Intent:** Put what NetPace does and how to install it on the first screen, so a visitor arriving from a search or a package listing can copy one command without scrolling past prose.

**Behaviour:** Specified by issue [#405](https://github.com/FrankRay78/NetPace/issues/405) — title, badges, pitch, link bar, screenshot, Install, Quick start, then the rest.

**Constraints:**

- The pre-1.0 notice is named by the constitution (§VII), so it stays in the README — moved below Install rather than dropped.
- The `--help` snapshot stays in Usage under the CLAUDE.md CLI-docs rule.
- No install fact may be lost: the update command, `dnx`, tool-package platform coverage, the three binary variants and building from source all remain in the README or behind its link to `USER_GUIDE.md#installing-netpace`.

**Decisions:**

- Install channels are one short markdown table with a row per channel, so winget, Homebrew or Scoop each become one more row. Rejected starship-style collapsible per-OS blocks: with two channels they cost more markup than they save, and a new channel would mean editing every OS block rather than adding a line.
- The one-line `dotnet tool install -g netpace` sits *above* the table as well as in it. The duplication is deliberate — the table answers "which channel is mine", the bare command answers "what do I type", and a reader on the first screen should not have to read a table to find the latter.
- Install detail (update, `dnx`, AOT-per-platform, binary variants, build from source) stayed in the README below the table instead of moving wholesale to the User Guide. The paragraphs are short, and a returning user looking for the update command should find it where they installed from.
- Usage's two worked examples moved into Quick start rather than being duplicated; Usage keeps the User Guide link and the `--help` snapshot.
- Report Bug / Request Feature moved from the top into Contributing, where a reader who wants to file something is already looking.

**Date:** 2026-10-18
