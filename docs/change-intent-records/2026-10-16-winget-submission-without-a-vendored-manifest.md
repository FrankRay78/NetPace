# winget submission runs wingetcreate directly, against no vendored manifest

**Intent:** Give Windows users a one-line install and a working `winget upgrade`, automated per release (#390).

**Behaviour:** `docs/RELEASING.md`, *winget package*. The trigger choice and runner are already argued in the decision table there; only the alternatives rejected on the way are recorded here.

**Constraints:**
- The manifest lives in `microsoft/winget-pkgs`, not here, and is moved forward by a tool — this repo never holds the authoritative copy.
- Acceptance is reviewed by Microsoft's moderators, so the pipeline can only be held to opening the pull request.
- The package identifier and the `netpace` moniker are user-typed contracts (#390, *Confirmed decisions*).

**Decisions:**
- **`wingetcreate` directly, not the `winget-releaser` third-party action.** The action is a thinner call site but puts a third party on the path that holds the maintainer's `winget-pkgs` credentials; `wingetcreate` is Microsoft's own tool for this and the one whose CI contract the community repository documents.
- **No copy of the manifest committed here.** A vendored copy was considered for reviewability, and rejected: `wingetcreate` updates the upstream copy, so ours would read as authoritative while going stale from the first automated release onward. `docs/RELEASING.md` specifies the manifest's shape instead, which is what the hand-authored first submission needs and cannot drift into looking current.
- **Installer URLs resolved from the release, in a tested script, not built by interpolation in the workflow.** The archive-naming convention is owned by `release-binaries.yml`; reading the release's own asset list means a naming drift or a missing archive fails before submission rather than producing a manifest that 404s. The extraction buys a `*.tests.sh` matrix that `shell-tests.yml` already discovers, so the invariant is gated per pull request instead of discovered at release time.
- **A failed submission raises an issue.** A `workflow_run`-triggered failure emails nobody, so a red run in the Actions tab is the silent pass the acceptance criteria rule out.

**Date:** 2026-10-16
