# Legacy Ordinary Unit Movement Deprecation Phase 8A: Obsolete Helper Cleanup

## Decision

Phase 8A changes naming and test support only. Runtime boundary policy is unchanged: `GameplayRuntimeFeatureFlags.None` and `DefaultGameplayLocomotion` do not authorize covered player/enemy/Charge fallback, and `LegacyOrdinaryFallbackBaseline` is diagnostic compatibility only. Phase 8B supersedes the preset name with `RemovedLegacyFallbackDiagnosticBaseline`; `LegacyOrdinaryFallbackBaseline` remains a compatibility alias. `EnableLegacyOrdinaryUnitFallback`, `LegacyOrdinaryFallbackBaseline`, `MoveEntity`, `MovementExpander`, retained grid transactions, `TickEntityMotionKind.Move`, `TickEntityMotionKind.ChargeMove`, and glide retained fallback are not removed or renamed in Phase 8A.

## Obsolete Helper Inventory

| helper | Phase 8A state | canonical replacement | action |
|---|---|---|---|
| `AllowsLegacyOrdinaryFallbackBaseline` | obsolete wrapper, definition only | `AssertCoveredFallbackRemovedDiagnostics` | keep for compatibility; no internal callers |
| `AllowsPlayerFlagOffLegacyOrdinaryFallback` | not present | `AssertPlayerFallbackRemovedFromRuntime` | do not restore |
| `AllowsEnemyFlagOffLegacyOrdinaryFallback` | obsolete wrapper, definition only | `AssertEnemyFallbackRemovedFromRuntime` | keep for compatibility; no internal callers |
| `AllowsChargeFlagOffLegacyFallback` | obsolete wrapper, definition only | `AssertChargeFallbackRemovedFromRuntime` | keep for compatibility; no internal callers |
| `AllowsOnlyFlagOffCoveredFallback` | obsolete wrapper, definition only | `AssertCoveredFallbackRemovedDiagnostics` | keep for compatibility; no internal callers |
| `AllowsFlagOffLegacyFallback` | historical compatibility wrapper | covered fallback helpers above | do not use for covered fallback assertions |
| `AllowsRetainedGlideFallback*` | active retained glide helper | unchanged | retained exception; not covered fallback cleanup |

## Naming Policy

New covered-fallback tests and helpers must use `Removed`, `Rejected`, `Diagnostic`, `Compatibility`, or `NoCoveredFallback` wording. `Allowed`, `StillAllowed`, `FlagOffBaseline`, `FallbackAllowed`, and `Allows` are stale for covered fallback authorization and must not be used for new covered-fallback tests. Retained grid transactions may continue using `RemainAllowed` wording because they are explicitly not legacy ordinary Unit fallback.

Phase 8A adds canonical canaries:

- `Phase8A_ObsoleteHelpers_NoInternalUsage`
- `Phase8A_LegacyFallbackBaseline_IsDiagnosticCompatibilityNaming`
- `Phase8A_Phase7Canaries_StillPass`
- `Phase8A_GridTransactionsRemainAllowed`
- `Phase8A_GlidePolicyUnchanged`
- `Replay_Phase8A_DiagnosticBaseline_Deterministic`

Historical Phase 2/4/5/6 wrapper names remain for compatibility and stratification continuity. They are historical names for removed diagnostics, not current fallback allowance policy.

## Replay And Golden

No golden or replay assets are rewritten in Phase 8A. The Phase 8A replay canary delegates to the deterministic Phase 7 diagnostic baseline behavior and asserts no covered fallback output. Historical fallback output migration remains future owner-approved work.

## Next Phase Candidates

- Phase 8B: add `RemovedLegacyFallbackDiagnosticBaseline` as the canonical diagnostic preset name.
- Decide whether the `LegacyOrdinaryFallbackBaseline` compatibility alias is removed after replay migration.
- Continue presentation cleanup inventory for retained `TickEntityMotionKind.Move` and `TickEntityMotionKind.ChargeMove` without deleting retained grid presentation.
