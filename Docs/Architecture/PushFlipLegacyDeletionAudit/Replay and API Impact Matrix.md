# Replay and API Impact Matrix

## Runtime API Surface

Current production API surface stays canonical:

- `the removed diagnostic baseline preset (historical, deleted)`
- `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticsEnabled`
- `removedLegacyFallbackDiagnosticsEnabled`
- `PlayerLegacyFallbackRemovedFromRuntime`
- `EnemyLegacyFallbackRemovedFromRuntime`
- `ChargeLegacyFallbackRemovedFromRuntime`

The retired old API surface remains absent. No compatibility overload, alias, or projection is restored.

## Test-Support Surface

The obsolete `LegacyMovementBoundaryAssert` fallback helper wrappers are deleted. Current tests use canonical removed-diagnostic helpers and retained grid/glide helpers.

Deleting wrapper surface does not change simulation behavior, replay state, or determinism hashes.

## Replay / Trace

Canonical trace vocabulary stays:

- `RemovedLegacyFallbackDiagnosticsEnabled=`

`Boundary=LegacyFallback` is retained boundary vocabulary for `MovementExecutionBoundaryKind.LegacyFallback`. It is not the retired exact trace projection.

Current replay canaries continue to compare deterministic state, trace text, removed diagnostics, and absence of covered fallback boundary output.

## External Compatibility Risk

C안 accepts breakage for external source callers or parser consumers that still depend on retired old API or retired trace vocabulary.

Mitigation is documentation and canonical migration guidance only:

- use `the removed diagnostic baseline preset (historical, deleted)` for deterministic removed diagnostics
- consume `RemovedLegacyFallbackDiagnosticsEnabled=` for canonical trace routing
- do not restore old projection aliases

## Rollback

Rollback restores the deleted test-support wrappers and wrapper-existence tests only if C안 is cancelled.

Rollback must not restore retired production API, retired constructor naming, or retired trace projection unless the C안 decision itself is reversed.
