# Machine-readable output is never wrapped

**Intent:** Stop NetPace inserting line breaks into `--json`, `--json-pretty`, `--csv` and `--minimal` output, which made piped JSON unparseable and split CSV header names mid-word.

**Behaviour:** Specified by #377 — a record a program reads carries only the breaks its own format defines, whatever its destination's width.

**Constraints:**

- The default human-readable output must go on wrapping to fit the screen.
- `--file` output was already unaffected (`FileConsole` reports no width limit), and had to stay byte-identical.

**Decisions:**

- *Chosen — lift `console.Profile.Width` once, where the run picks its writer.* One statement covers all four formats and the error lines the command writes once it is running. Error lines written before the command is constructed - a parse or validation failure - are still written at the unlifted width. The writer declares the property (`IConsoleWriter.MachineReadable`) rather than the command type-testing it, so a new writer states its own answer.
- *Rejected — a non-wrapping `IAnsiConsole` decorator, as `FileConsole` does it.* A decorator cannot change the width the console it wraps renders at; it would have to re-render segments itself, duplicating `FileConsole`'s rendering for no gain over setting the width.
- *Rejected — writing machine-readable output straight to stdout, bypassing Spectre.* Removes the wrapping and `--file`, `--quiet` and the composite console with it.
- The mutation of the injected console's profile is deliberate and load-bearing: `CompositeAnsiConsole` delegates `Profile` to its primary, so the terminal leg and the composite share one profile object. A run selects its format once, so nothing later needs the original width back.

**Date:** 2026-10-15
