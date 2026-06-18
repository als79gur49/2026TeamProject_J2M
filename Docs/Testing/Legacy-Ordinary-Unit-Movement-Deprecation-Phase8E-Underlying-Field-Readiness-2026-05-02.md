# Legacy Ordinary Unit Movement Deprecation Phase 8E: Canonical Diagnostics Field Migration

Date: 2026-05-02

## Executive Decision

C안 immediate deletion is complete.
RemovedLegacyFallbackDiagnosticsEnabled is the canonical runtime flag.
The old fallback-authored API shape is not retained as an alias, wrapper, constructor parameter, named argument, replay projection, or trace projection.

This flag does not authorize covered player ordinary, enemy ordinary, or Charge active fallback.
It only controls removed-fallback diagnostic routing:

- `false` routes covered fallback attempts to `LegacyOrdinaryFallbackRequiresExplicitBaseline`.
- `true` routes covered fallback attempts to `PlayerOrdinaryMoveRejectedBeforeLegacyExpansion`, `EnemyLegacyFallbackRemovedFromRuntime`, or `ChargeLegacyFallbackRemovedFromRuntime`.

## Current State

`GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline` remains the canonical preset for deterministic removed-fallback diagnostics.
`GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticsEnabled` is the canonical field on the runtime flag struct.
`GameplaySceneHostConfiguration` does not expose this diagnostics field as a scene-authored setting.
Scene hosts still create/apply only the gameplay locomotion flags; tests and replay helpers opt into removed diagnostics through `RemovedLegacyFallbackDiagnosticBaseline`.

Trace vocabulary now uses `RemovedLegacyFallbackDiagnosticsEnabled=`.
No compatibility trace projection is retained.
No compatibility API projection is retained.

## Protected Runtime Behavior

Ordinary fallback remains removed for covered player, enemy, and Charge paths.
Retained grid transactions, `MoveEntity`, `MovementExpander`, and glide flag-off fallback remain outside this API cleanup.
Push and Flip are still explicit player actions: plain movement must not start Push, Push starts only from `PushPressed`, and Flip starts only from `FlipPressed`.

## Validation Policy

This migration requires the core lane and touched replay/boundary canaries because public runtime flag shape and replay trace vocabulary changed.
Expected evidence includes:

- `git diff --check`
- `./run_tests.sh core`
- targeted replay/boundary/action-plan tests when feasible

## Non-Goals

This change does not remove Push/Flip functionality, input actions, command fields, box capabilities, interaction drivers, action-audio policy, retained grid transactions, or movement expansion infrastructure.
