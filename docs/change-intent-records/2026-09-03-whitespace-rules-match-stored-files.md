# Whitespace Rules Must Agree With What Is Stored

**Intent:** Stop the recurring failure where a formatting rule asserts something the stored files contradict, so a clean checkout reports modified files and a routine `dotnet format` pass rewrites bytes nobody edited.

**Behaviour:** `.gitattributes` and `.editorconfig`, each exemption carrying its reason beside it; `dotnet format --verify-no-changes` over `src/NetPace.sln` gates every pull request, so a rule that merely oscillates cannot satisfy it either.

**Constraints:** Verify snapshots (`*.verified.*`) are byte-exact records of program output, so their trailing whitespace and missing final newlines are content rather than formatting, and a rule that rewrote them would change what the suite asserts.

**Decisions:**

- Govern line endings with a `* text=auto eol=lf` wildcard plus explicit `-text` markers for binaries.
  - Rejected: enumerating each extension (`*.sln`, `*.csproj`, `*.props`, …). An enumeration covers only what someone remembered to add, so a new file type falls silently through — which is how the earlier attempt left that list ungoverned in the first place.
  - Chose: the wildcard, so a type added later is governed by default and the only thing needing maintenance is the short binary list.
- Set `insert_final_newline = true` rather than accepting the `false` the file declared.
  - Rejected: keeping `false` and stripping the final newline from every file that has one. It agrees with the rule just as well, but leaves `\ No newline at end of file` in every diff that touches a last line, and a steady stream of complaints from other tooling.
  - Chose: `true`, which matches what the overwhelming majority of files already store, the POSIX convention, and git's own expectations — and makes the residual cleanup the smaller of the two.

**Date:** 2026-09-03
