---
name: feedback_discuss_before_menu
description: A decision that needs research or judgement is discussed in prose first; "please advise" / "I need help" means stop the multiple-choice prompts
metadata:
  type: feedback
---

When a decision needs exploration — research into comparable tools, the project's own rules, the costs of each option — do the research and raise it as a written assessment with a recommendation, then let Frank answer in his own words. Use `AskUserQuestion` only to confirm a decision whose options are already clear. If an answer asks for advice, explanation or more thought, reply in prose — never with another set of options.

**Why:** drafting backlog item 8 (#370), the JSON units decision was put to Frank as a multiple-choice prompt three times. He asked for advice twice ("we need to think about this some more", "I need help — can you explain") and got another menu each time. He called it a very poor experience. The first question also asked a surface choice (replace or add?) before the underlying one (which unit?).

**How to apply:** before any `AskUserQuestion`, ask whether Frank could answer it now without more information. If not, research and discuss first. Related: [[feedback_one_decision_per_turn]], [[feedback_plain_language_decisions]].
