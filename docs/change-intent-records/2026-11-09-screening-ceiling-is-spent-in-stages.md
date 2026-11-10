# The Screening Ceiling Is Spent In Stages

**Intent:** Give a link with a round trip of a second or two long enough to have a server chosen for it, without making a run on a quick link any slower.

**Behaviour:** See issue #533, and [server-screening-and-measurement](../architecture/server-screening-and-measurement.md) → *The ceiling is spent in stages, not waited out*.

**Constraints:** The hard ceiling on choosing a server must stay, for the reasons that document gives. A screening pass only ends early once every candidate has finished, so the ceiling is what ends any pass still holding a straggler — however quick the candidates that did answer were.

**Decisions:**

- Two stages, the first of two seconds by default, which was the whole ceiling before. Rejected: a single ten-second ceiling — every run with one silent candidate would then take ten seconds to choose a server where it used to take two.
- Ending the second stage as soon as one candidate answers. Rejected: the ranking would compare one server again, the illusion this design was built to remove.
- The first stage as a public setting, `ServerDiscoverySettings.FirstScreeningStageMilliseconds`. Rejected: a private constant — staging changed the ceiling from the patience given every candidate to the patience of a pass that has ranked nothing, and a consumer who had raised the ceiling so slower servers could compete, or who raised `ScreeningRequestCount` past what fits in two seconds, would have had no way back. A first stage at or above the ceiling restores a single stage.

**Date:** 2026-11-09
