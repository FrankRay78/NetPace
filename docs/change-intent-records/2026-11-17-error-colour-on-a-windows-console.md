# Error Colour On A Windows Console

**Intent:** Let the error line render in colour on a classic Windows console when the result is redirected, without ever putting escape bytes in the result.

**Behaviour:** See issue #567. The decision is `Program.ErrorAnsiSupport`, whose remarks carry how it works.

**Constraints:**

- Spectre.Console 0.57.1 falls back to a backend that sets colour through `System.Console`'s own colour properties whenever ANSI support comes back negative, and on Unix .NET writes those to standard output. So detection may only be asked for where a negative answer costs nothing: on Windows those properties are console API calls that write no bytes. Revisit the condition on an upgrade that changes that fallback.
- Nobody has reproduced the fault on a real Windows machine, so the Windows half is checked by hand, as the issue's confirmed decisions record.

**Decisions:**

- Detect on a Windows terminal only. Rejected: detecting everywhere. A Unix terminal whose `TERM` names no ANSI-capable type would then colour standard output, which is worse than the cosmetic flaw being fixed.
- Reuse Spectre.Console's detector. Rejected: calling `SetConsoleMode` ourselves — it repeats a call the library already makes against the error handle, and puts a P/Invoke in a project that has none.

**Date:** 2026-11-17
