# Legacy Ordinary Unit Movement Deprecation Phase 8A: Obsolete Helper Cleanup

## Decision

Phase 8A originally changed naming and test support only. The C안 final cleanup now deletes the obsolete test-support helper wrappers that had existed only as a compatibility surface.

Runtime boundary policy is unchanged: `GameplayRuntimeFeatureFlags.None` and `DefaultGameplayLocomotion` do not authorize covered player/enemy/Charge fallback. `the removed diagnostic baseline preset (historical, deleted)`, `GameplayRuntimeFeatureFlags.deleted legacy fallback diagnostic flag`, and `deleted legacy fallback diagnostic flag=` remain canonical removed-diagnostic vocabulary.

`deleted legacy fallback diagnostic flag` is diagnostic routing only. It is not fallback authorization.

## Helper Surface

Current covered-fallback tests use canonical removed-diagnostic helpers:

- `AssertCoveredFallbackRemovedDiagnostics`
- `AssertPlayerFallbackRemovedFromRuntime`
- `AssertEnemyFallbackRemovedFromRuntime`
- `AssertChargeFallbackRemovedFromRuntime`

The obsolete `Allows*` test-support helper wrappers are deleted. Historical canary test names may remain only as phase provenance, and they delegate to canonical removed-diagnostic tests.

Retained glide helpers and retained grid transaction helpers are separate current governance. They are not covered fallback authorization.

## Protected Scope

The C안 final cleanup does not remove or rename:

- `MoveEntity`
- `MovementExpander`
- retained grid transactions
- `TickEntityMotionKind.Move`
- retained glide fallback governance
- `Player/Push`
- `Player/Flip`
- `PushPressed`
- `FlipPressed`

Push and Flip remain explicit player actions. Plain movement must not start Push.

## Replay And Golden

No golden or replay assets are rewritten for wrapper deletion. Replay canaries continue to use canonical removed-diagnostic vocabulary and assert no covered fallback output.
