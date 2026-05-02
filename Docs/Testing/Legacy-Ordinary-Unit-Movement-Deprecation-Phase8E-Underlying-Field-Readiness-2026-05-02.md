# Legacy Ordinary Unit Movement Deprecation Phase 8E: Underlying Field Readiness

Date: 2026-05-02

## Executive Decision

Phase 8E does not rename or delete `EnableLegacyOrdinaryUnitFallback`.
The field remains the underlying compatibility field for removed-fallback diagnostic routing.
It does not authorize covered player, enemy, or Charge fallback.
No new canonical field such as `EnableRemovedLegacyFallbackDiagnostics` is added in Phase 8E.
Runtime validation semantics, replay behavior, and trace token text are unchanged.
`RemovedLegacyFallbackDiagnosticsEnabled` remains the preferred helper for current runtime and test policy.
`LegacyOrdinaryFallbackEnabled` and `LegacyOrdinaryFallbackBaseline` remain compatibility aliases.
Option B is a future consideration, not a Phase 8E implementation.

## Current Phase Status

Phase 8B added `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline` as the canonical preset for deterministic removed diagnostics.
Phase 8C migrated current internal usage to that canonical preset while keeping `LegacyOrdinaryFallbackBaseline` as a deprecated compatibility alias.
Phase 8D added `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticsEnabled` as the canonical helper while keeping `LegacyOrdinaryFallbackEnabled` as a deprecated compatibility alias.

The remaining misleading symbol is the underlying field and constructor parameter `EnableLegacyOrdinaryUnitFallback`.
It is risky to rename or delete immediately because it participates in the public struct shape, preset construction, tests, docs, and trace/golden vocabulary decisions.

## Field Usage Inventory

| symbol / usage | location | usage kind | current meaning | rename risk | serialized/config risk | can migrate now? | recommended action | future phase |
|---|---|---|---|---|---|---|---|---|
| `enableLegacyOrdinaryUnitFallback` | `GameplayRuntimeFeatureFlags.cs` | constructor parameter | diagnostic routing input | high: named-argument and constructor churn | low: runtime struct only | no | keep and document | Option B phase |
| `EnableLegacyOrdinaryUnitFallback` assignment | `GameplayRuntimeFeatureFlags.cs` | backing assignment | compatibility field storage | medium | low | no | keep | Option B/C review |
| `EnableLegacyOrdinaryUnitFallback` property | `GameplayRuntimeFeatureFlags.cs` | public property | removed-specific diagnostic routing | high: public flag shape churn | low-medium if projected into config later | no | keep as compatibility-only | Option B/C/D |
| `RemovedLegacyFallbackDiagnosticBaseline` | `GameplayRuntimeFeatureFlags.cs` | preset initialization | deterministic removed diagnostics | medium | low | no | keep canonical preset | alias cleanup later |
| `LegacyOrdinaryFallbackBaseline` | `GameplayRuntimeFeatureFlags.cs` | compatibility preset alias | deprecated preset alias | low | low | no | keep | separate alias cleanup |
| `RemovedLegacyFallbackDiagnosticsEnabled` | `GameplayRuntimeFeatureFlags.cs`, `TickPipeline.cs` | helper property / validation read | canonical diagnostic-routing helper | low | low | done | keep preferred read | none |
| `LegacyOrdinaryFallbackEnabled` | `GameplayRuntimeFeatureFlags.cs` | compatibility helper alias | deprecated helper alias | low | low | no | keep | obsolete/remove later |
| explicit-baseline gate | `TickPipeline.ValidateLegacyExpansionIntents` | validation helper read | false routes to `LegacyOrdinaryFallbackRequiresExplicitBaseline` | high | none | no | keep helper read | none |
| removed reasons | `TickPipeline.TryResolveForbiddenLegacyUnitOrdinaryMovement` | diagnostic routing | true routes to player/enemy/Charge removed reasons | high | none | no | keep semantics | none |
| `LegacyFallback=` | `TickPipeline.FormatLocomotionFeatureFlags` | trace formatter token | diagnostic routing trace bit | high: replay/golden churn | none | no | keep token text | trace cleanup phase |
| scene host flags | `GameplaySceneHostConfiguration` | config creation/apply | advanced locomotion flags only | medium | high if added accidentally | no | do not expose legacy diagnostic field | future Option B only with approval |
| showcase installer | `CombinedGameplayShowcaseInstaller` | runtime flag application | applies `DefaultGameplayLocomotion` | low | scene YAML churn if changed | no | no change | none |
| replay harness flags | `EnemyKinematicLocomotionReplayTests` | preset injection | deterministic removed diagnostics | medium | none | no | keep preset usage | replay migration phase |
| docs | Phase 3-8E readiness docs and ADR | policy documentation | compatibility meaning | low | none | yes | document inventory and options | next phase decision |

## Serialization / Config Exposure Findings

`GameplaySceneHostConfiguration` does not expose `EnableLegacyOrdinaryUnitFallback` as a public scene host field.
`CreateRuntimeFeatureFlags()` passes only the current seven locomotion flags and leaves the legacy diagnostic constructor parameter at its default `false`.
`ApplyRuntimeFeatureFlags()` writes only the scene-authored locomotion flags and does not persist the legacy diagnostic bit.
`CombinedGameplayShowcaseInstaller` applies `DefaultGameplayLocomotion` and does not set the diagnostic compatibility field.
Scene YAML under `Assets/Scenes` does not contain `EnableLegacyOrdinaryUnitFallback`.
Replay and migration tests use `RemovedLegacyFallbackDiagnosticBaseline`; they do not directly author a scene/config bool for this field.

Expected policy: general scene hosts do not expose this field; tests, replay, and migration helpers may carry it through the diagnostic preset.

## Trace / Golden Impact Findings

`LegacyFallback=` is emitted by `TickPipeline.FormatLocomotionFeatureFlags()`.
The token value is already read through `RemovedLegacyFallbackDiagnosticsEnabled`, not by directly reading `_runtimeFeatureFlags.EnableLegacyOrdinaryUnitFallback`.
Renaming the token would churn deterministic trace comparisons and possible golden outputs.
The token can remain even if a future phase adds a cleaner canonical field.
Trace vocabulary cleanup is a separate future phase.
No golden files are rewritten in Phase 8E.

## Option Matrix

| option | change | pros | cons | runtime risk | serialization risk | replay/golden risk | test churn | recommendation |
|---|---|---|---|---|---|---|---|---|
| Option A | Keep field indefinitely | safest compatibility path | misleading name remains | low | low | low | low | acceptable fallback |
| Option B | Add `EnableRemovedLegacyFallbackDiagnostics` later and keep old field as compatibility | gradual migration and clearer naming | duplicate shape complexity | medium | medium | low | medium | recommended future consideration |
| Option C | Rename old field | clean API | constructor, named-argument, and compatibility churn | high | medium-high | medium | high | do not do in Phase 8E |
| Option D | Remove field and diagnostic routing preset | final cleanup | loses deterministic removed-diagnostic replay/migration support | high | medium | high | high | only after replay/golden migration approval |

## Recommendation

Phase 8E keeps `EnableLegacyOrdinaryUnitFallback` as the compatibility field and records the inventory.
Current runtime, tests, and docs should prefer `RemovedLegacyFallbackDiagnosticsEnabled` when describing diagnostic routing.
Future cleanup should consider Option B first, but only after an explicit migration plan for public flag shape, tests, docs, and trace/golden vocabulary.

## Next Phase Candidates

- Keep `EnableLegacyOrdinaryUnitFallback` indefinitely if compatibility stability is more valuable than naming cleanup.
- Add `EnableRemovedLegacyFallbackDiagnostics` and keep `EnableLegacyOrdinaryUnitFallback` as a compatibility projection.
- Rename the old field only with explicit constructor/config compatibility approval.
- Remove the diagnostic routing field only after replay/golden migration no longer needs deterministic removed diagnostics.

## Non-Goals

Phase 8E does not change `MovementExpander`, `MoveEntity`, `TickEntityMotionKind.Move`, `TickEntityMotionKind.ChargeMove`, retained grid transactions, glide retained fallback, runtime validation semantics, or replay/golden files.
