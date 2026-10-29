# The CSV Header Is Decided Per Destination

**Intent:** Let a scheduled run append CSV to one log without a header line between every pair of rows, and give `>>` users a switch that leaves the header out entirely.

**Behaviour:** See [issue #463](https://github.com/FrankRay78/NetPace/issues/463), whose confirmed decisions settled the switch name and where the file check lives.

**Constraints:** The terminal and a `--file` target had always received byte-identical output through `CompositeAnsiConsole`. They now differ by one line, so a writer needing that distinction unpacks the composite and judges each destination by `IPersistentConsole.StartedEmpty`.

**Decisions:**

- Whether the file already holds rows is read once, before it is opened. Rejected: asking per write — a run that appends is itself adding content, so every later answer describes its own output.
- A new switch. Rejected: dropping the header by default, as `speedtest-cli` and Ookla's CLI do — a single CSV test at a terminal would lose its column names.
- Rejected: checking an existing log's header against the current columns. A pre-1.0 column change is met by starting a new log, which `USER_GUIDE.md` advises.

**Date:** 2026-10-29
