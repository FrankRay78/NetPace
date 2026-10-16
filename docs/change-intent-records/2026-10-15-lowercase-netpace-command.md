# Lowercase `netpace` command

**Intent:** Make the command users type lowercase on every platform, so Linux and macOS users type it the way they type every other tool.

**Behaviour:** Specified in [#373](https://github.com/FrankRay78/NetPace/issues/373) — the program in every release archive, the usage line in `--help`, and every command example in the documentation name the command `netpace`.

**Constraints:**

- `NetPace` stays the product name, the library and NuGet package (`NetPace.Core`), the project names and the repository.
- Pre-1.0, so no deprecation period is owed (Constitution VII).

**Decisions:**

- Renamed the console `AssemblyName` to `netpace` rather than keeping the assembly `NetPace` and renaming only inside the "Create archive" step. Renaming at the archive step would leave the local `dotnet publish` output and the release archive disagreeing about the program's name, so the thing a maintainer runs locally would not be the thing users get.
- Kept the figlet banner as `NetPace`. It identifies the product, not the command; the usage line below it is what tells a user what to type.
- Did not ship a transitional `NetPace` alias alongside `netpace`. Two names for one command doubles what the docs must explain. Linux users with scripts or cron jobs calling `NetPace` must update them.
- Left the project file names (`NetPace.Console.csproj`) and the default root namespace alone. Only the produced program's name is user-facing; renaming the projects would churn the solution for no user-visible gain. The root namespace is load-bearing: the figlet resource is looked up as `NetPace.Console.slant.flf`, so renaming it to match the assembly would break the banner.

**Date:** 2026-10-15
