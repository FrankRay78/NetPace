# Error Colour On A Windows Console

**Intent:** Let the error line render in colour on a classic Windows console when the result is redirected, without ever putting escape bytes in the result.

**Behaviour:** See issue #567. The decision is `Program.ErrorAnsiSupport`: a Windows terminal detects, because only detection switches virtual terminal processing on for the error handle, and everything else is told outright, so Spectre.Console writes escapes to the writer it was given and to no other stream.

**Constraints:**

- Spectre.Console 0.57.1's negative-ANSI fallback is what makes the condition safe. Wherever it cannot confirm ANSI support it sets colour through `System.Console`'s colour properties instead. On Unix .NET writes those to standard output wherever that is a terminal, stamping the error console's colours onto the live result, which is why detecting is confined to a Windows terminal; on Windows they set the output handle's colour attributes and write no bytes, so a redirected result prints unstyled instead. Revisit on any upgrade that changes that fallback.
- Nobody has reproduced the fault on a real Windows machine, so the Windows half is checked by hand, as the issue's confirmed decisions record.

**Decisions:**

- Detect on a Windows terminal only. Rejected: detecting everywhere. A Unix terminal Spectre.Console's `TERM` list has not caught up with would then colour standard output, which is worse than the cosmetic flaw being fixed.
- Reuse Spectre.Console's detector. Rejected: calling `SetConsoleMode` ourselves — it repeats a call the library already makes against the error handle, and puts a P/Invoke in a project that has none.

**Date:** 2026-11-17
