# File Output Carries No Byte-Order Mark

**Intent:** Make a `--file` target start at its content, with no byte-order mark in front of it, so the scripting formats stay machine-readable on disk.

**Behaviour:** See [#447](https://github.com/FrankRay78/NetPace/issues/447). `FileConsole` opens its writer with a BOM-less `UTF8Encoding`; `Encoding.UTF8` emits the mark.

**Constraints:** A log an earlier version started still carries a mark, and appending leaves its existing first bytes alone — so the empty-log check in `SpeedTestCommand.StartsEmpty` must keep treating a mark-only file as empty.

**Decisions:**

- No option to write a byte-order mark. Rejected: a `--bom` switch, or a mark for CSV only. Some Excel versions misread a double-clicked UTF-8 CSV without one, but redirected stdout already lacks one, a user can add it after the run, and the user guide points spreadsheet users at *Data → From Text/CSV*. A second encoding would outlast the spreadsheet bug.

**Date:** 2026-11-02
