# File Output Carries No Byte-Order Mark

**Intent:** Make a `--file` target start at its content, with no byte-order mark in front of it, so the scripting formats stay machine-readable on disk.

**Behaviour:** See [#447](https://github.com/FrankRay78/NetPace/issues/447). `FileConsole` opens its writer with a BOM-less `UTF8Encoding`; `Encoding.UTF8` emits the mark.

**Constraints:** A log an earlier version started still carries a mark, and appending leaves its existing first bytes alone — so the empty-log check in `SpeedTestCommand.StartsEmpty` must keep treating a mark-only file as empty.

**Decisions:**

- No option to write a byte-order mark, for any format. Rejected: a `--bom` switch, or writing one for CSV only. Some versions of Excel read a double-clicked UTF-8 CSV as a legacy code page without one, which mangles non-ASCII sponsor names — but a redirected stdout already behaves that way, a user who needs the mark can add it after the run (prepending it first does not survive: a file holding nothing but a mark counts as an empty log and is replaced), and the user guide now points spreadsheet users at *Data → From Text/CSV*. A second encoding to support would outlast the spreadsheet bug.

**Date:** 2026-11-02
