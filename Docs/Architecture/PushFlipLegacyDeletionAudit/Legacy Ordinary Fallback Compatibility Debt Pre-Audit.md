# Legacy Ordinary Fallback Compatibility Debt Closeout

## Executive Summary

Verdict: `C_OPTION_DELETE_COMPLETE`.

The retired ordinary movement fallback runtime API names and retired exact trace token remain absent. This document intentionally avoids spelling those retired identifiers so repository-wide zero-hit acceptance searches can stay meaningful.

The final active compatibility debt was the obsolete test-support helper wrapper surface in `MovementExecutionOwnershipAssert`. C안 final cleanup deletes that helper surface and removes tests that asserted wrapper existence.

Canonical removed-fallback governance remains active:

- `the removed diagnostic baseline preset (historical, deleted)`
- `GameplayRuntimeFeatureFlags.deleted legacy fallback diagnostic flag`
- `deletedLegacyFallbackDiagnosticFlag`
- `deleted legacy fallback diagnostic flag=`
- `PlayerLegacyFallbackRemovedFromRuntime`
- `EnemyLegacyFallbackRemovedFromRuntime`
- `ChargeLegacyFallbackRemovedFromRuntime`

`deleted legacy fallback diagnostic flag` is diagnostic routing only. It is not fallback authorization.

## Boundary Vocabulary

`Boundary=LegacyFallback` is retained boundary vocabulary for `MovementExecutionBoundaryKind.GenericExpansionOwned` when grid/glide governance requires it. It is not the retired exact trace token.

Removed player/enemy/Charge fallback canaries continue to assert removed diagnostics and no covered fallback output.

## Push/Flip Safety

Push/Flip runtime and input are not part of this deletion. Keep:

- `Player/Push`
- `Player/Flip`
- `PushPressed`
- `FlipPressed`
- `PlayerActionKind.Push`
- `PlayerActionKind.Flip`
- `BoxCapabilities.Push`
- `BoxCapabilities.Flip`
- `FlipImpactPresentationSignal`
- Push/Flip action audio policy

Plain move must not start Push. The suppression tests remain current governance.

## Closeout Requirements

- Keep retired old API and retired old trace vocabulary zero-hit in active repo searches.
- Keep canonical removed diagnostics present.
- Keep Push/Flip runtime and input paths untouched.
- Keep retained grid/glide boundary vocabulary separate from covered fallback deletion.
- Treat external old API and retired trace parser compatibility as accepted C안 risk.
