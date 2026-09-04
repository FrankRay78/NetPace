# Whitespace Rules Must Agree With What Is Stored

**Intent:** Stop the recurring failure where a formatting rule asserts something the stored files contradict, so a clean checkout reports modified files and a routine `dotnet format` pass rewrites bytes nobody edited. Three instances of the same shape had landed by this point: `.gitattributes` gaining `*.cs text eol=lf` without renormalisation (#236/#240), and `.editorconfig` declaring `insert_final_newline = false` against a repository where 77 of the 92 non-empty `.cs` files end with a newline (#249).

**Behaviour:**

- Given a clean checkout, when `dotnet format` runs over `src/NetPace.sln`, then no file is changed — and because the check is `--verify-no-changes`, a rule that merely oscillates cannot satisfy it either.
- Given a pull request, when CI runs, then that check gates the build, so the drift is caught before it reaches anyone's feature branch rather than discovered by whoever ships next.
- Given any tracked file, when git decides how to store its line endings, then a `.gitattributes` rule decides it rather than the committing developer's `core.autocrlf`.

**Constraints:**

- Verify snapshots (`*.verified.*`) are byte-exact records of program output. Their trailing whitespace and missing final newlines are content, not formatting; an editor honouring `.editorconfig` would silently rewrite them and change what the suite asserts.
- `.specify/**` and `.claude/skills/speckit-*/**` are upstream spec-kit assets, restored verbatim by `specify init --here --force`. Formatting them would be undone on the next upgrade.
- The guard has to stay proportionate to the problem. This is a one-line disagreement in a config file, not a subsystem.

**Decisions:**

- Set `insert_final_newline = true` rather than accepting `false`.
  - Rejected: keeping `false` and committing the strip of every file that currently ends with a newline. It agrees with the rule just as well, but leaves the whole repository without trailing newlines — `\ No newline at end of file` in every diff that touches a last line, and a steady stream of complaints from other tooling.
  - Chose: `true`, which matches what the overwhelming majority of files already store, the POSIX convention, and git's own expectations. It also makes the residual cleanup the smaller of the two.
- Govern line endings with a `* text=auto eol=lf` wildcard plus explicit `-text` markers for binaries.
  - Rejected: enumerating each extension (`*.sln`, `*.csproj`, `*.props`, …). #240 left exactly that list ungoverned because an enumeration only covers what someone remembered to add; a new file type silently falls through.
  - Chose: the wildcard, so a type added later is governed by default and the only thing needing maintenance is the short binary list.
- Enforce the invariant with `dotnet format --verify-no-changes` in CI.
  - Rejected: hand-written repository hygiene tests that parse `.editorconfig` and check each tracked file against the rules they resolve. This was built and then deleted. It needed a glob-to-regex translator and an `.editorconfig` parser — roughly 600 lines of test infrastructure with its own bugs — to cover three of the keys `.editorconfig` declares, and it silently reported success whenever a pattern failed to match.
  - Chose: the formatter's own verification. It understands every rule in the file — `charset`, `indent_size`, the `dotnet_*` and `csharp_*` style rules, the analyzers — not the three a bespoke reader could be taught, and it is the same tool whose behaviour the issue is about, so it cannot disagree with itself. Measured at 26 seconds, against `main` it exits 2 and against this branch it exits 0.
- Record the exemptions in `.editorconfig` itself, with the reason beside each.
  - Rejected: leaving the rules silently disagreeing with the snapshot and vendored files. They would have shown up as churn the first time anyone opened one in an EditorConfig-aware editor.
  - Chose: explicit sections, so the disagreement is settled in the file that declares the rule rather than discovered later.

**Date:** 2026-09-03
