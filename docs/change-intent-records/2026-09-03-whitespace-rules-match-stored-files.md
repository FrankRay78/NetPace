# Whitespace Rules Must Agree With What Is Stored

**Intent:** Stop the recurring failure where a formatting rule asserts something the committed files contradict, so a clean checkout reports modified files and a routine `dotnet format` pass rewrites bytes nobody edited. Three instances of the same shape had landed by this point: `.gitattributes` gaining `*.cs text eol=lf` without renormalisation (#236/#240), and `.editorconfig` declaring `insert_final_newline = false` against a repository where 77 of 92 non-empty `.cs` files end with a newline (#249).

**Behaviour:**

- Given a clean checkout of `main`, when `dotnet format style` then `dotnet format whitespace` run over `src/NetPace.sln`, then no file is changed — and a second consecutive pair changes nothing either.
- Given any tracked file, when git decides how to store its line endings, then a `.gitattributes` rule decides it rather than the committing developer's `core.autocrlf`.
- Given a whitespace rule declared in `.editorconfig`, when the test suite runs, then every committed file the rule governs is checked against it, so the declaration and the storage cannot drift apart unnoticed.

**Constraints:**

- Verify snapshots (`*.verified.*`) are byte-exact records of program output. Their trailing whitespace and missing final newlines are content, not formatting; rewriting them would silently change what the suite asserts.
- `.specify/**` and `.claude/skills/speckit-*/**` are upstream spec-kit assets, restored verbatim by `specify init --here --force`. Formatting them would be undone on the next upgrade.
- The guard has to be cheap and deterministic. Shelling out to `dotnet format --verify-no-changes` from a unit test would cost a full compile per run.

**Decisions:**

- Set `insert_final_newline = true` rather than accepting `false`.
  - Rejected: keeping `false` and committing the 79-file strip. It agrees with the rule just as well, but leaves every file without a trailing newline — `\ No newline at end of file` in every diff that touches a last line, and a steady stream of complaints from other tooling.
  - Chose: `true`, which matches what the overwhelming majority of files already store, the POSIX convention, and git's own expectations. The residual cleanup is ~25 files rather than 79.
- Govern line endings with a `* text=auto eol=lf` wildcard plus explicit `-text` markers for binaries.
  - Rejected: enumerating each extension (`*.sln`, `*.csproj`, `*.props`, …). #240 left exactly that list ungoverned because an enumeration only covers what someone remembered to add; a new file type silently falls through.
  - Chose: the wildcard, so a type added later is governed by default and the only thing needing maintenance is the short binary list.
- Assert the invariant with repository hygiene tests that read `.editorconfig` and resolve each rule per file, rather than hard-coding the expected settings.
  - Rejected: a test asserting `insert_final_newline = true` literally. It pins the mechanism and says nothing about whether the files comply.
  - Rejected: a CI-only `dotnet format --verify-no-changes` gate. It catches the same drift but only after a push, and it does not cover the files outside the solution.
  - Chose: resolve the declared rule and check the committed bytes against it. The test's subject is the *agreement*, which is the thing that keeps breaking. A companion test asserts the C# corpus is governed by all three rules, so the compliance checks cannot go quietly vacuous by someone switching a rule off.
- Record the exemptions in `.editorconfig` itself rather than in a skip list inside the test.
  - Rejected: a list of excluded paths in the test file. That is a second copy of the same knowledge, free to drift from the first — the very failure this change exists to end.

**Date:** 2026-09-03
