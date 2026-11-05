# CodeQL removed

**Intent:** Stop paying for analysis that finds nothing. In its whole history CodeQL's C# analysis raised no security finding — every one of its 189 alerts was a code-quality note (casts to self, undisposed locals mostly in tests, catch-all handlers), and the open ones went unread. NetPace is a CLI with no web or server surface: the only untrusted input it reads is responses from the speed-test servers it chooses to contact.

**Behaviour:** `.github/workflows/codeql.yml` is deleted, so no CodeQL check runs on pull requests, pushes or a schedule. Dependency advisories stay gated by `dotnet.yml` and Dependabot ([`2026-07-13-dependency-supply-chain-hardening`](2026-07-13-dependency-supply-chain-hardening.md)).

**Decisions:**

- Rejected keeping it for its quality notes: the build's own analyzers and review cover code quality, and an unread alert list is noise.
- Rejected switching to GitHub's default setup: it is the same analysis with less control.
- Re-add static security analysis if NetPace gains a surface that serves requests, such as a web API or a listening port.

**Date:** 2026-11-03
