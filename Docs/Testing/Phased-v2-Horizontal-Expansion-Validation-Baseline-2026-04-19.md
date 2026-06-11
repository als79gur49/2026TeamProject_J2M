# Phased v2 Horizontal Expansion Validation Baseline 2026-04-19

이 문서는 `Phased v2` horizontal expansion validation에서 `SystemPreMovementValidation` second source를 추가한 뒤의 최소 evidence를 남긴다.

## Commands

```bash
./run_tests.sh core
./run_tests.sh full
python3 Tools/check_gameplay_test_stratification.py --root /mnt/c/users/user/2026teamproject_j2m --mode strict
```

## Before / After Summary

| Command | Scope | Before | After | Delta | Disposition |
| --- | --- | --- | --- | --- | --- |
| `./run_tests.sh core` | mandatory gate | Core EditMode `13/13`, Core PlayMode `2/2`, green | Core EditMode `13/13`, Core PlayMode `2/2`, green | unchanged | pass |
| `./run_tests.sh full` | Unity Full EditMode | `1017 total / 90 failed` | `1024 total / 89 failed` | `+7 total`, `-1 failed` | pass; full baseline remains red, but horizontal-expansion row set is green |
| `./run_tests.sh full` | exact horizontal-expansion touched rows: new scenario/source lifecycle, metadata, reservation-read sequence, lock-retention guard, finalize/doc governance | pre-change rows absent | `15/15` passed, `0` failed | `+15 exact rows`, `0` direct failures | pass |
| `./run_tests.sh full` | unrelated pre-existing failures outside exact touched rows | `90` | `89` | `-1` | pass; no new unrelated failure introduced by this slice |
| `python3 Tools/check_gameplay_test_stratification.py --root /mnt/c/users/user/2026teamproject_j2m --mode strict` | governance | red; existing unit execution-placement debt + override/category mismatch debt | red; same pre-existing debt remains, and `SystemPreMovementValidation` execution row no longer appears as a new unit-placement error | no new horizontal-expansion governance failure | nongating |

## Exact touched rows

- `Game.Feature.Gameplay.Tests.Scenario.SystemPreMovementValidationScenarioTests.SystemPreMovementValidationOwner_EntersAtPlanSnapshot_And_IsVisibleToMovementCollection_SameTick`
- `Game.Feature.Gameplay.Tests.Unit.ModifierCapabilityGeneralizationTests.SystemPreMovementValidationSource_SustainsAcrossWindow_And_ClearsWhenWindowCloses`
- `Game.Feature.Gameplay.Tests.Unit.ModifierCapabilityGeneralizationTests.SystemPreMovementValidationSource_ForeignOwnerConflict_Throws`
- `Game.Feature.Gameplay.Tests.Unit.ModifierCapabilityGeneralizationTests.SystemPreMovementValidationSource_ForcedCancel_WhenEntityHpDropsToZero`
- `Game.Feature.Gameplay.Tests.Unit.ModifierCapabilityGeneralizationTests.RuntimeTraversalLegalityPolicy_EvaluateDestination_SystemValidationPhasedActor_IgnoresUnitBlocker`
- `Game.Feature.Gameplay.Tests.Unit.ModifierCapabilityGeneralizationTests.WorldSnapshot_SystemValidationPhasedCarrier_SuppressesFreshTargetSelection_WithoutEnemyLockRetention`
- `Game.Feature.Gameplay.Tests.Unit.SpatialStateResolverTruthTableTests.SystemPreMovementValidationSource_RemainsReservationReadFree`
- `Game.Feature.Gameplay.Tests.Unit.SpatialStateResolverTruthTableTests.PhasedSourceMetadataCatalog_ActiveOwners_HaveCoverage_And_StageDefaults`
- Retired in PR E: `EnemyPhaseRelocationReservationReadPath_RemainsCellOnly`
- Retired in PR E: `EnemyPhaseRelocationPlanner_And_Finalizer_RemainClosedMinimalValidatorSeams`
- `Game.Feature.Gameplay.Tests.Unit.ModifierCapabilityGeneralizationTests.EnemyActionStateTargeting_CurrentEnemyLockPath_RetainsLockedTarget_WhileFreshSelectionStaysSuppressed`
- `Game.Feature.Gameplay.Tests.Unit.ModifierCapabilityGeneralizationTests.EnemyActionStateTargeting_TryResolveStartAction_PrefersUnphasedFreshSelection_And_DoesNotReuseCurrentEnemyLockRetention`
- `Game.Feature.Gameplay.Tests.Unit.TraverseSettleDocumentationGovernanceTests.PhasedDocs_DescribeStageDefaults_And_DeferredLockTaxonomy_AsClosedContracts`
- `Game.Feature.Gameplay.Tests.Unit.TraverseSettleDocumentationGovernanceTests.CanonicalSpec_DescribesTraverseSettleSpatialState_And_ReservedFutureStates`
- `Game.Feature.Gameplay.Tests.Unit.FinalizeNoRecheckArchitectureTests.TickPipeline_RunFinalizePhase_And_FinalizationBatchApplyTo_DoNotReevaluateLegality`

## Artifacts

- `TestResults/wsl-unity-core-editmode.xml`
- `TestResults/wsl-unity-core-playmode.xml`
- `TestResults/wsl-unity-full-editmode.xml`
- `TestResults/wsl-unity-full-editmode.log`

## Notes

- `./run_tests.sh full` still stops at Full EditMode because the repo baseline remains red; Full PlayMode was not reached in this slice.
- strict stratification governance remains red because of pre-existing unit execution-placement debt and existing override/category mismatches.
- new horizontal-expansion execution proof was moved to simulation assembly so it does not add a new unit execution-placement governance error.

## Retired Validator Handoff

- `Role=RetiredBaselineValidatorOnly`
- `LockDependency=CurrentEnemyLockPathOnly`
- `ChooserLocality=SameFace|StraightLine|Behind+1|SingleTerminal`
- `Reservation=TerminalCellOnlyPreSettle`
- `ForbiddenGeneralization=NoRetarget|NoAlternate|NoFallback|NoSameTickCombat`
- `NotEvidenceFor=GeneralizedPhaseMovement|Pathfinding|NonClaimOccupancy|TerminalPhaseSettle`
