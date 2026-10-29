# The Call-Chain Trigger Searches The Name Callers Actually Use

**Intent:** Make `/review-pr`'s call-chain trigger fire on a change to shared code in any repository, without naming a language, layout or component kind.

**Behaviour:** See issue #467 for the two signals, the `CALL-CHAIN:` line and the specialist's brief.

**Constraints:**

- The trigger and the specialist brief are intended to port unchanged into other repositories, so neither signal may lean on one repo's vocabulary.
- The `CALL-CHAIN:` line is all the coordinator reads to decide the pass, so triage emits it whichever way the answer went.

**Decisions:**

- Signal 2 falls back to the changed member's name where callers never name the enclosing unit. Rejected: searching the enclosing unit's name alone, as first specified. A static extension class is never written at its call sites, so that search found zero dependents for the issue's own worked example — a method three console writers call — and the pass would have been skipped on exactly the change it exists to catch. The fallback keeps the original reason for preferring the unit name, by skipping any member name common enough to match almost everything.
- Two structural signals about the diff. Rejected: triggering on component kinds or folder names — that ties the trigger to one repo's vocabulary, so a shared component named otherwise is missed and a folder that only shares a name fires it spuriously.

**Date:** 2026-10-29
