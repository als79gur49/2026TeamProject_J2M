# C-Option Deletion Closeout Checklist

## Required State

- Retired old runtime/API/trace symbols stay zero-hit in `Assets`, `Docs`, and `ProjectSettings`.
- Obsolete `LegacyMovementBoundaryAssert` helper wrappers are deleted.
- Tests that asserted obsolete wrapper existence are deleted.
- Canonical removed-fallback diagnostics remain present.
- Push/Flip runtime, input, action, box capability, presentation, and audio paths remain untouched.

This checklist intentionally does not spell retired identifiers. Use the implementation prompt acceptance searches for exact command evidence.

## Canonical Replacement Vocabulary

Keep:

- `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline`
- `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticsEnabled`
- `removedLegacyFallbackDiagnosticsEnabled`
- `RemovedLegacyFallbackDiagnosticsEnabled=`
- `PlayerLegacyFallbackRemovedFromRuntime`
- `EnemyLegacyFallbackRemovedFromRuntime`
- `ChargeLegacyFallbackRemovedFromRuntime`
- retained grid/glide boundary vocabulary where current tests require it

`RemovedLegacyFallbackDiagnosticsEnabled` only selects removed-diagnostic routing. It does not authorize covered fallback.

## Tests

Delete tests that preserve obsolete wrapper surface. Keep tests that prove canonical governance:

- `Phase8C_RemovedDiagnosticBaseline_IsCanonicalUsage`
- `Phase8C_RemovedDiagnosticBaselineAlias_IsRemoved`
- `Phase8D_RemovedLegacyFallbackDiagnosticsEnabled_IsCanonicalHelper`
- `Phase8D_RemovedDiagnosticHelperAlias_IsRemoved`
- `Phase8E_RemovedDiagnosticField_IsCanonicalApi`
- `Phase8E_TraceToken_UsesCanonicalRemovedDiagnosticVocabulary`
- removed-from-runtime canaries for player/enemy/Charge
- deterministic removed-diagnostic replay rows
- plain move -> Push suppression tests

## Docs

Docs should state:

- C안 immediate deletion is complete.
- old aliases and retired trace projection remain removed.
- canonical diagnostics vocabulary is retained.
- `RemovedLegacyFallbackDiagnosticsEnabled` is not fallback authorization.
- Push/Flip is not deletion scope.
- plain move must not start Push.

## Push/Flip No-Touch List

Do not delete or behavior-change:

- `Player/Push`
- `Player/Flip`
- `GameplayInputHost`
- `PushPressed`
- `FlipPressed`
- `PlayerActionKind.Push`
- `PlayerActionKind.Flip`
- `BoxCapabilities.Push`
- `BoxCapabilities.Flip`
- `FlipImpactPresentationSignal`
- Push/Flip action audio policy

## No-Go Triggers

- Reintroducing retired old API or retired trace projection.
- Treating `RemovedLegacyFallbackDiagnosticsEnabled` as fallback authorization.
- Removing Push/Flip feature paths.
- Removing plain move -> Push suppression coverage without replacement.
