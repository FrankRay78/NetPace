# NuGet publishes authenticate by trusted publishing

**Intent:** Publish `NetPace.Core` and the `NetPace` tool package without a long-lived NuGet API key stored in the repository.

**Behaviour:** Both publishing workflows push to nuget.org with a short-lived key obtained at run time from the job's GitHub OIDC token. No NuGet API key is held as a repository secret.

**Constraints:**

- The first tool-package release failed its push with a 403: the one shared `NUGET_API_KEY` had been created when only `NetPace.Core` existed, and could not push the new package IDs.
- nuget.org now binds every API key, and every trusted publishing policy, to a single workflow file. The two publishing workflows therefore could not share one credential either way.

**Decisions:**

- Chose trusted publishing over one API key per workflow. Both need a nuget.org entry per workflow file; trusted publishing replaces two stored secrets that expire and can leak with an exchange that issues a key valid for under an hour, only to the named workflow in this repository.
- Ran the login step immediately before each push, after every release gate, so the key exists only for the push.
- Kept the nuget.org profile name in a `NUGET_USER` secret, as `NuGet/login`'s documentation does, rather than in the workflow text.

**Date:** 2026-10-17
