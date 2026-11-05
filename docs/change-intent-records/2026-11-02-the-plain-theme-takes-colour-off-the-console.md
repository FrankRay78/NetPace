# The Plain Theme Takes Colour Off The Console

**Intent:** Make a theme that draws no colour draw none anywhere, including the parts of the screen Spectre.Console colours itself.

**Behaviour:** See [#459](https://github.com/FrankRay78/NetPace/issues/459).

**Constraints:**

- Spectre.Console 0.57.1 reads `NO_COLOR` but only removes colour: bold and other emphasis still reach the screen.
- Spectre's progress bar tells its finished part from its remaining part by colour, and falls back to blank space for the remainder only when the console reports no colour support.

**Decisions:**

- `plain` sets the console's colour system to `NoColors`. Rejected: setting every slot to `Style.Plain` — the bar then draws one unbroken run of glyphs with nothing to show how far it has got.
- Rejected: a dim remaining style. It separates the two parts without colour, but dim is emphasis, which `plain` exists not to use.
- `NO_COLOR` selects `plain`. Rejected: leaving it to Spectre, which would leave the theme's emphasis in place.
- A line drawn in several styles is composed as one renderable. Rejected: writing each run in turn — the console wraps each renderable on its own, and the result line overran a narrow terminal.

**Date:** 2026-11-02
