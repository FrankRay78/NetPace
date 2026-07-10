---
name: speckit upgrade procedure
description: How to upgrade/re-sync the spec-kit toolkit and switch script variant (sh/ps)
type: reference
---
The `.specify/` toolkit is **stock** `github/spec-kit` (not a fork), driven by the `specify` CLI (installed via `uv` at `~/.local/bin`). Its own version scheme (`0.8.x`, `0.12.x`) is unrelated to NetPace releases.

To upgrade or switch script variant, re-run init in place:

```bash
specify init --here --force --integration claude --script sh
```

It is **additive + hash-guarded** (`.specify/integrations/speckit.manifest.json`): overwrites only unmodified base files, preserves customizations (`templates/overrides/`, `extensions/testplan`, constitution, custom `.claude/commands/speckit.*`). It does **not** delete files the new version drops — after a variant switch, manually remove orphaned old-variant scripts + their manifest entries. Verify with a clean `specify init` in a temp dir and diff `.specify/scripts`.

`--script sh` = bash (Linux/current), `--script ps` = PowerShell. See [[feedback_speckit_hooks]] for invoking the resulting scripts.
