# Legacy Ordinary Unit Movement Deprecation Phase 8C: Legacy Alias Usage Cleanup

Date: 2026-05-02

## Decision

Phase 8C changed tests and documentation only. `the removed diagnostic baseline preset (historical, deleted)` is the canonical preset for deterministic removed-fallback diagnostics. The old `GameplayRuntimeFeatureFlags.RemovedDiagnosticBaselineAlias` alias has since been removed.

Runtime validation semantics are unchanged. Covered player, enemy, and Charge fallback attempts reject with removed diagnostics under the canonical preset. `GameplayRuntimeFeatureFlags.None` still rejects covered fallback with `LegacyOrdinaryFallbackRequiresExplicitBaseline`. Phase 8D supersedes the helper naming inventory with `deleted legacy fallback diagnostic flag`; Phase 8E makes that name the canonical runtime field and removes old-name compatibility projection.

`TickPipeline`, `MovementExpander`, `MoveEntity`, retained grid transactions, `TickEntityMotionKind.Move`, `TickEntityMotionKind.ChargeMove`, glide retained fallback, replay assets, and golden files are not changed in Phase 8C.

## Old Alias Usage Inventory

| usage location | usage kind | current text/symbol | canonical replacement | allowed to keep? | action |
|---|---|---|---|---|---|
| `GameplayRuntimeFeatureFlags.cs` | alias definition | `RemovedDiagnosticBaselineAlias => removed diagnostic baseline preset (historical, deleted)` | `removed diagnostic baseline preset (historical, deleted)` | no | removed |
| `BoundaryInventoryScenarioTests.cs` | alias absence test | `Phase8C_RemovedDiagnosticBaselineAlias_IsRemoved` | canonical absence canary | yes | keep |
| scenario and replay current diagnostics | canonical usage | old alias direct calls | `removed diagnostic baseline preset (historical, deleted)` | no | migrated |
| retained grid transaction tests | unrelated retained behavior | old alias direct calls | canonical or default non-old lane | no | migrated away from old alias |
| docs current policy | current preset wording | old alias as current baseline | `removed diagnostic baseline preset (historical, deleted)` | no | update wording |
| docs historical notes | historical preset name | `RemovedDiagnosticBaselineAlias` | `removed diagnostic baseline preset (historical, deleted)` | yes | mark historical/removed |
| obsolete helper surface | test-support compatibility surface | old helper wording | canonical removed-diagnostic helpers | no | deleted by C안 final cleanup |

## Canonical Migration Policy

New tests, replay helpers, reports, and current-policy documentation must use `the removed diagnostic baseline preset (historical, deleted)` when they intentionally need deterministic removed diagnostics.

Allowed old alias contexts:

- historical documentation explaining the pre-Phase8B name
- historical canary provenance when marked as removed policy

Disallowed wording patterns:

- old alias name paired with `allows fallback`
- old alias name paired with `is the baseline`
- legacy fallback baseline wording paired with `remains allowed`

## Test And Replay Coverage

Phase 8C adds or updates these canaries:

- `Phase8C_RemovedDiagnosticBaseline_IsCanonicalUsage`
- `Phase8C_RemovedDiagnosticBaselineAlias_IsRemoved`
- `Phase8C_CurrentPolicyDocs_UseRemovedDiagnosticBaseline`
- `Phase8C_AllowedOldAliasUsage_IsLimited`
- `Phase8C_GridTransactionsRemainAllowed`
- `Phase8C_GlideDefaultAdoptionAndFlagOffFallbackRetained`
- `Replay_Phase8C_RemovedDiagnosticBaseline_DiagnosticsDeterministic`
- `Replay_Phase8C_DefaultGameplay_NoCoveredFallback`

The replay canaries compare deterministic trace/state output under `removed diagnostic baseline preset (historical, deleted)` and assert removed diagnostics without `Boundary=LegacyFallback`. No replay or golden artifact is rewritten.

## Phase 8D Follow-up

- `deleted legacy fallback diagnostic flag` is the canonical helper property for removed diagnostic routing.
- `RemovedDiagnosticHelperAlias` has been removed; use `deleted legacy fallback diagnostic flag`.
- `deleted legacy fallback diagnostic flag` is the canonical runtime field after Phase 8E.
- `RemovedDiagnosticBaselineAlias` has been removed; use `removed diagnostic baseline preset (historical, deleted)`.
- Continue retained presentation inventory for `TickEntityMotionKind.Move` and `TickEntityMotionKind.ChargeMove` without changing retained grid transaction or glide policy.
