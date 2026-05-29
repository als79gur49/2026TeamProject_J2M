# Legacy Ordinary Unit Movement Deprecation Phase 8A: Obsolete Helper Cleanup

## Decision

Phase 8A changed naming and test support only. Runtime boundary policy is unchanged: `GameplayRuntimeFeatureFlags.None` and `DefaultGameplayLocomotion` do not authorize covered player/enemy/Charge fallback. Phase 8B/8C supersedes the diagnostic preset name with `RemovedLegacyFallbackDiagnosticBaseline`; Phase 8D adds `RemovedLegacyFallbackDiagnosticsEnabled` as the canonical helper for diagnostic routing. The 2026-05-29 legacy/deprecated cleanup removed the obsolete `Allows*` helper wrappers after confirming no behavioral callers remained. `LegacyOrdinaryFallbackBaseline` and `LegacyOrdinaryFallbackEnabled` remain deprecated compatibility aliases. `EnableLegacyOrdinaryUnitFallback`, `LegacyOrdinaryFallbackBaseline`, `MoveEntity`, `MovementExpander`, retained grid transactions, `TickEntityMotionKind.Move`, `TickEntityMotionKind.ChargeMove`, and glide retained fallback are not removed or renamed by this helper-wrapper cleanup.

## Obsolete Helper Inventory

| helper | Phase 8A state | canonical replacement | action |
|---|---|---|---|
| `AllowsLegacyOrdinaryFallbackBaseline` | removed 2026-05-29 | `AssertCoveredFallbackRemovedDiagnostics` | do not restore |
| `AllowsPlayerFlagOffLegacyOrdinaryFallback` | not present | `AssertPlayerFallbackRemovedFromRuntime` | do not restore |
| `AllowsEnemyFlagOffLegacyOrdinaryFallback` | removed 2026-05-29 | `AssertEnemyFallbackRemovedFromRuntime` | do not restore |
| `AllowsChargeFlagOffLegacyFallback` | removed 2026-05-29 | `AssertChargeFallbackRemovedFromRuntime` | do not restore |
| `AllowsOnlyFlagOffCoveredFallback` | removed 2026-05-29 | `AssertCoveredFallbackRemovedDiagnostics` | do not restore |
| `AllowsFlagOffLegacyFallback` | removed 2026-05-29 | retained glide/grid helpers | do not restore |
| `AllowsRetainedGlideFallback*` | active retained glide helper | unchanged | retained exception; not covered fallback cleanup |

## Naming Policy

New covered-fallback tests and helpers must use `Removed`, `Rejected`, `Diagnostic`, `Compatibility`, or `NoCoveredFallback` wording. `Allowed`, `StillAllowed`, `FlagOffBaseline`, `FallbackAllowed`, and `Allows` are stale for covered fallback authorization and must not be used for new covered-fallback tests. Retained grid transactions may continue using `RemainAllowed` wording because they are explicitly not legacy ordinary Unit fallback.

Phase 8A adds canonical canaries:

- `Phase8A_ObsoleteHelpers_NoInternalUsage`
- `Phase8A_LegacyFallbackBaseline_IsDiagnosticCompatibilityNaming`
- `Phase8A_Phase7Canaries_StillPass`
- `Phase8A_GridTransactionsRemainAllowed`
- `Phase8A_GlideDefaultAdoptionAndFlagOffFallbackRetained`
- `Replay_Phase8A_DiagnosticBaseline_Deterministic`

Historical Phase 2/4/5/6 test method names remain for compatibility and stratification continuity. The old helper wrapper methods are removed; current tests must call canonical removed-diagnostic helpers directly.

## Replay And Golden

No golden or replay assets are rewritten in Phase 8A. The Phase 8A replay canary delegates to the deterministic Phase 7 diagnostic baseline behavior and asserts no covered fallback output. Historical fallback output migration remains future owner-approved work.

## Next Phase Candidates

- Phase 8B: add `RemovedLegacyFallbackDiagnosticBaseline` as the canonical diagnostic preset name.
- Phase 8C: migrate current internal usage to `RemovedLegacyFallbackDiagnosticBaseline` while keeping `LegacyOrdinaryFallbackBaseline` as a deprecated compatibility alias only.
- Phase 8D: add `RemovedLegacyFallbackDiagnosticsEnabled` as the canonical diagnostic helper while keeping `LegacyOrdinaryFallbackEnabled` as a deprecated compatibility alias.
- Continue presentation cleanup inventory for retained `TickEntityMotionKind.Move` and `TickEntityMotionKind.ChargeMove` without deleting retained grid presentation.
