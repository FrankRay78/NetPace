---
name: Provider-agnostic types must not depend on concrete providers
description: NetPace.Core domain enums (Profile, SpeedUnit, etc.) never gain methods or extensions returning provider-specific types — dependency flows provider→shared, never reverse.
type: project
---

Provider-agnostic types in `NetPace.Core` (e.g. `Profile`, `SpeedUnit`, `SpeedScale`, `SpeedUnitSystem`) must never gain methods, extensions, or factory methods that return provider-specific types. The dependency direction is always concrete-provider → shared-vocabulary, never the reverse — even when the inverse syntax (e.g. `profile.ToOoklaSpeedtestSettings()`) reads more naturally.

**Why:** During the issue #174 design (`--profile` switch, April 2026), Claude proposed an extension method on `Profile` that returned an Ookla-specific settings record. The user rejected this as architecturally inverted and volunteered it as the most surprising assumption Claude had made. The principle: a provider-agnostic vocabulary type cannot reach forward into a concrete provider; only the concrete provider may reach back into the shared vocabulary.

**How to apply:** When designing any conversion/factory/extension that bridges a `NetPace.Core` domain type and a provider-specific type, put the bridge on the *provider* side (constructor on the provider's settings record, static factory on the provider's settings record, or extension method whose `this` is the provider type). Never put it on the shared type. This rule applies to any future provider added (M-Lab, NDT, etc.) — they each extend their own types with knowledge of `Profile`, never the other way round.
