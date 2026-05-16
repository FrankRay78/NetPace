# Template Overrides

Files in this directory replace the equivalent files in `.specify/templates/`
when speckit reads templates. They sit at the **top** of the resolution stack
and are never touched by `specify init --here --force` upgrades.

Resolution order (highest → lowest precedence):

1. `.specify/templates/overrides/`  ← here
2. `.specify/presets/<id>/`
3. `.specify/extensions/<id>/`
4. `.specify/templates/`  ← core (overwritten by `--force`)

## Current overrides

| File | Why IMS overrides it |
|------|----------------------|
| `spec-template.md` | Bakes in the `**Scenario: [name]**` label convention required by [Constitution principle VIII](../../memory/constitution.md) for AC-to-test traceability. |

To add a new override: drop a file with the same name as the core template
into this directory. To stop overriding: delete the file (core wins again).
