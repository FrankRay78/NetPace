---
name: Audit the failure class after the second occurrence
description: When two failures share the same root-cause shape, stop patching individual instances and enumerate every site exhibiting the pattern. The third patch concedes to whack-a-mole.
type: feedback
---

When two failures share the same root-cause shape (shared resource + per-caller assumption, timing race against the same subsystem, cross-project state pollution of the same kind), **stop patching individual instances**. Audit every site that exhibits the pattern, build a small matrix of (resource, user, assumption), and fix the whole class in one pass. Patching the third instance without auditing concedes to whack-a-mole — and the user loses faith faster than the fixes accumulate.

**Why:** Fixing look-alike failures one at a time feels like progress but hides that they are one bug wearing several hats. Each individual patch is cheap; the pattern behind them is what actually needs deciding on. By the second occurrence you already have enough signal to name the shape — so the economical move is to enumerate the class then, not after the fifth "why is this still happening?".

**How to apply:** On the second failure of a shape you've seen before, before writing any fix code: enumerate every call site in the codebase that exhibits the same pattern. Produce the (resource, user, assumption) matrix and identify every conflict cell. Propose a single fix that resolves the class, not the instance. If the matrix reveals an architectural gap — the system permits violations that conventions merely ask callers to avoid — the fix may need to move into production code rather than be repeated at each call site.
