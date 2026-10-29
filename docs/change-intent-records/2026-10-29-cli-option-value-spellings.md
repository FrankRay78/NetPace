# CLI option value spellings live beside the parser, not in the library's enums

**Intent:** Spell every fixed-choice option value one way wherever a user meets it — the help, the docs, and the message for a rejected value — so what the help shows is what a user can type.

**Behaviour:** Issue #462 states it; `src/NetPace.Console/OptionValues.cs` is the list.

**Constraints:** `SpeedUnit`, `SpeedScale`, `SpeedUnitSystem` and `Profile` are published `NetPace.Core` members, read by library consumers who never see a command line.

**Decisions:**

- *One `OptionValueSet<T>` per option, consulted by the help, the parser and the rejection message.* Rejected: renaming `SpeedUnit.BitsPerSecond` to `Bits`, so the CLI could keep binding enum names directly. It reads worse at `GetSpeedString(SpeedUnit.Bits, …)`, and the next short command-line word would drag another public member with it.
- *A custom parser per option, which binds the token and composes the rejection message.* Rejected: System.CommandLine's enum binding — its message names the internal type (`NetPace.Core.Profile`) and lists values in PascalCase, and nothing in it would let `bits` mean `BitsPerSecond`.

**Date:** 2026-10-29
