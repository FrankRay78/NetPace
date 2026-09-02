---
name: Explain tradeoffs in plain language before asking the user to decide
description: When surfacing a technical decision, explain each option in plain, jargon-free language — consequences and analogies over internals — before asking the user to choose; and when you quantify a claim, decompose it rather than reaching for a flattering multiplier.
type: feedback
---

When NetPace work reaches a genuine decision point and you put it to the user (an `AskUserQuestion`, a "which approach?" prompt, a spec tradeoff), explain each option in **plain, jargon-free language first**: what it means for them, what it costs, what it unlocks — using analogies over internals. Lead with the consequence, not the mechanism. The user should be able to choose well without first having to decode framework names, protocol details, or implementation jargon.

**Why:** Frank's standing preference is to decide on the basis of *consequences*, not internals. Decisions framed in implementation vocabulary ("System.CommandLine vs a hand-rolled parser", "PreToolUse vs Stop hook", "bundle the amendment vs split the PR") push the translation work onto the reader and make the tradeoff harder to weigh, not easier. A one-line plain-language framing per option — *"this keeps one clean history but mixes a rule change with tooling; that keeps them separate at the cost of a second PR"* — lets the user exercise judgment on the thing that actually matters to them.

**How to apply:**
- Before an `AskUserQuestion`, write each option's `description` as a plain-language consequence ("what you get / what it costs"), not a restatement of the mechanism. Keep the jargon term available for those who want it, but after the plain framing, not instead of it.
- Prefer a concrete analogy or a before/after over an internals walkthrough when the internals aren't the point of the decision.
- **Do not over-claim, either.** Plain language is not licence for a flattering round number. When an option's cost or benefit is quantified, decompose the estimate — say what it is made of — rather than reaching for a headline multiplier. A calibrated "~30% fewer re-reads" is worth more than a loose "2x faster", because the user can check the parts and tell you which one is wrong.
- This is the *how you present* companion to surfacing tool/architecture tradeoffs as explicit user choices rather than burying them — surface the choice, and frame it in plain language. Pairs with [[feedback_decisiveness_over_hedging]]: frame the choice plainly, and only surface it at all when it is genuinely still open.
