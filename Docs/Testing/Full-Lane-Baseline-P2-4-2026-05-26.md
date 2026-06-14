# Full Lane Baseline P2-4 Inventory 2026-05-26

이 문서는 Gameplay VFX SourceCloneMotion P0-P2-3 후속 P2-4 범위의 full lane broad baseline 정리 기록이다.

P2-4의 목표는 full lane을 green으로 만드는 것이 아니라, 현재 full EditMode에 남은 39개 실패를 증거 기반으로 분류하고 안전한 후속 순서를 고정하는 것이다.

## 요약 결론

- `./run_tests.sh core`는 green이다.
- `./run_tests.sh full`은 red이며 Unity Full EditMode에서 `4841 total / 39 failed`다.
- Unity Full PlayMode는 EditMode 실패 때문에 실행되지 않았다.
- 이번 P2-4에서 즉시 수정 가능한 Group 1 `SafeToFixNow` 항목은 확인되지 않았다.
- VFX P0-P2-3 runtime regression guard는 full 안에서 통과했다.
- runtime behavior, binding asset, prefab reference, enum numeric value, default cue map, common host, meta/GUID, runtime-created host는 변경하지 않는다.

근거 파일:

- `TestResults/wsl-unity-full-editmode.xml`
- `TestResults/wsl-unity-core-editmode.xml`
- `TestResults/wsl-unity-core-playmode.xml`

## 테스트 결과

| command | result | notes |
|---|---|---|
| `git status --short --branch` | clean tracked worktree | `## main...origin/main [ahead 10]` |
| `git diff --stat` | empty | no tracked local diff before P2-4 doc |
| `git diff --check` | passed | no whitespace errors |
| `./run_tests.sh core` | passed | Core EditMode `63 total / 0 failed`; Core PlayMode `5 total / 0 failed` |
| `./run_tests.sh full` | failed | Full EditMode `4841 total / 39 failed`; Full PlayMode not run |

Failure count 변화:

- 수정 전 full failure count: `39`
- 수정 후 full failure count: `39`
- 이번 P2-4에서 수정한 항목 수: `0` runtime/code/asset fixes, `1` documentation inventory
- 남은 항목 수: `39`
- full green 여부: no
- full green이 아닌 이유: 39개 broad baseline failure가 남아 있고, P2-4 범위에서는 high-risk runtime/asset/binding 변경을 하지 않는다.
- 새 failure 여부: no; post-document full rerun produced the same 39 failed FQNs.
- latest full XML timestamp: `2026-05-25 17:40:40Z` to `2026-05-25 17:42:58Z`

## VFX 회귀 확인

| guard | full result |
|---|---|
| `DestroyShrink_Plays_WithSourceClone_WhenPrefabIsMissing` | passed |
| `FlipDestroySelfMotion_Plays_WithSourceClone_WhenPrefabIsMissing` | passed |
| `ImpactTransientBreak_Plays_WithSourceClone_WhenPrefabIsMissing` | passed |
| `OutOfBoundsExit_Box_Plays_WithSourceClone_WhenPrefabIsMissing` | passed |
| `OutOfBoundsExit_Enemy_Plays_WithSourceClone_WhenPrefabIsMissing` | passed |
| `EnemyDeathMotion_UsesFallbackPrefab_WhenSourceViewIsUnavailable_IfPolicyAllows` | passed |
| `EnemyDeathMotion_SeparatesMissingSourceView_FromMissingPrefab` | passed |
| `PrefabOnlyCue_ReportsMissingPrefab_WhenPrefabIsNull` | passed |
| SourceCloneMotion common host path guards | passed |
| P2-2 SourceCloneMotion host ADR guard | passed |
| P2-3 VFX test naming policy guard | passed |

## Full Failure Inventory

| # | test full name | test class | test method | domain | failure type | failure summary | likely cause | related recent diff | owner area | risk | recommended action |
|---:|---|---|---|---|---|---|---|---|---|---|---|
| 1 | `Game.Feature.Gameplay.Tests.Unit.CampaignStageFlowTests.RespawnProcessor_PlayerRespawnGateSuppressesSpawnBeforeWrite` | `CampaignStageFlowTests` | `RespawnProcessor_PlayerRespawnGateSuppressesSpawnBeforeWrite` | Stage | G. IndependentBroadBaseline | expected index `>= 0`, actual `-1` at `CampaignStageFlowTests.cs:324` | stage respawn gate expectation or scenario fixture drift | UnrelatedBaseline | Stage runtime | DoNotFixInThisPass | NeedsOwnerDecision; inspect stage respawn ordering separately |
| 2 | `Game.Feature.Gameplay.Tests.Unit.EnemyInactiveMaterialAuthoringTests.InactiveCompatibleDuplicates_RecordSourceAndPreserveMaterialValues` | `EnemyInactiveMaterialAuthoringTests` | `InactiveCompatibleDuplicates_RecordSourceAndPreserveMaterialValues` | Gameplay VFX | D. AssetReferenceExpectationStale | inactive duplicate material did not match any source candidate | enemy inactive-compatible material asset drift | UnrelatedBaseline | Enemy presentation assets | High | NeedsOwnerDecision; asset/source duplicate review |
| 3 | `Game.Feature.Gameplay.Tests.Unit.FinalizeNoRecheckArchitectureTests.TickPipeline_CleanupRespawnDirectWritePath_IsDocumentedAndBounded` | `FinalizeNoRecheckArchitectureTests` | `TickPipeline_CleanupRespawnDirectWritePath_IsDocumentedAndBounded` | Gameplay Architecture | B. ArchitectureGuardExpectationStale | expected direct write path count `2`, actual `3` | cleanup/respawn architecture guard count stale or new path needs documentation | UnrelatedBaseline | Tick architecture | Medium | NeedsSmallTargetedFix after source path audit |
| 4 | `Game.Feature.Gameplay.Tests.Unit.FlipArcSamplerTests.BoxFlipSlamSampler_LiftsOverPivotWithLateralArcThenSlamsTowardTarget` | `FlipArcSamplerTests` | `BoxFlipSlamSampler_LiftsOverPivotWithLateralArcThenSlamsTowardTarget` | Gameplay Core | G. IndependentBroadBaseline | expected slam midpoint `> 0.75f`, actual `0.597545147f` | flip arc timing/math baseline drift | UnrelatedBaseline | Gameplay host motion | DoNotFixInThisPass | NeedsOwnerDecision; avoid expectation-only update |
| 5 | `Game.Feature.Gameplay.Tests.Unit.GameplayAudioOverlapRefactorTests.TileFeatureAudioCoalescer_MixedOnOff_MissingOneBurstBinding_OnlyThatKindFallsBack` | `GameplayAudioOverlapRefactorTests` | `TileFeatureAudioCoalescer_MixedOnOff_MissingOneBurstBinding_OnlyThatKindFallsBack` | Audio | G. IndependentBroadBaseline | expected same audio definition instance, got different instance with same display | audio test fixture or binding identity drift | UnrelatedBaseline | Gameplay audio | DoNotFixInThisPass | DoNotTouchInP2_4; audio owner |
| 6 | `Game.Feature.Gameplay.Tests.Unit.GameplayTickPresentationCoordinatorTests.GravityFieldLockedTargetBoxPrefabs_AreAuthoredForLockableDimming` | `GameplayTickPresentationCoordinatorTests` | `GravityFieldLockedTargetBoxPrefabs_AreAuthoredForLockableDimming` | Gameplay TileFeature | D. AssetReferenceExpectationStale | MoonBox material uses `Universal Render Pipeline/Lit`, expected `GravityFieldLockableBoxLit` | gravity-field lockable dimming prefab/material authoring drift | UnrelatedBaseline | TileFeature presentation assets | High | NeedsOwnerDecision; asset authoring validation |
| 7 | `Game.Feature.Gameplay.Tests.Unit.GameplayTimingOwnershipTests.GameplayHostPresentationFeed_StageClearVictoryDelay_DefersStageClearedFrame` | `GameplayTimingOwnershipTests` | `GameplayHostPresentationFeed_StageClearVictoryDelay_DefersStageClearedFrame` | Stage | E. RuntimeBehaviorRegression | `Stage completion requires a valid StageContentEntry StageId` | fixture now lacks required stage id after stage runtime fallback hardening | UnrelatedBaseline | Stage runtime / timing | High | NeedsOwnerDecision; fix fixture or stage completion contract in targeted work |
| 8 | `Game.Feature.Gameplay.Tests.Unit.GameplayUiAccessRuntimeTests.GameplayUiAccess_PlayerHud_PushReadiness_ActionLock_DisablesPush` | `GameplayUiAccessRuntimeTests` | `GameplayUiAccess_PlayerHud_PushReadiness_ActionLock_DisablesPush` | UI | E. RuntimeBehaviorRegression | expected push disabled `False`, actual `True` | HUD action-lock readiness mapping drift | UnrelatedBaseline | Gameplay UIAccess | High | NeedsOwnerDecision; UI runtime owner |
| 9 | `Game.Feature.Gameplay.Tests.Unit.GameplayUiAccessRuntimeTests.GameplayUiAccess_PreRefreshTransientQueries_ReadPreviousCommittedHudState_BeforeTickCompletedRefresh` | `GameplayUiAccessRuntimeTests` | `GameplayUiAccess_PreRefreshTransientQueries_ReadPreviousCommittedHudState_BeforeTickCompletedRefresh` | UI | E. RuntimeBehaviorRegression | expected previous committed HUD state count `>= 1`, actual `0` | transient query / refresh ordering drift | UnrelatedBaseline | Gameplay UIAccess | High | NeedsOwnerDecision; UI runtime owner |
| 10 | `Game.Feature.Gameplay.Tests.Unit.GameplayUiAccessRuntimeTests.GameplayUiAccess_PresentationFeed_MapsPlayerSlice_ForHeldMove` | `GameplayUiAccessRuntimeTests` | `GameplayUiAccess_PresentationFeed_MapsPlayerSlice_ForHeldMove` | UI | E. RuntimeBehaviorRegression | expected mapped player slice `True`, actual `False` | presentation feed player slice mapping drift | UnrelatedBaseline | Gameplay UIAccess | High | NeedsOwnerDecision; UI runtime owner |
| 11 | `Game.Feature.Gameplay.Tests.Unit.GameplayVfxArchitectureTests.CoreSource_DoesNotReferenceSnapshotOrProductionRuntimeTypes` | `GameplayVfxArchitectureTests` | `CoreSource_DoesNotReferenceSnapshotOrProductionRuntimeTypes` | Gameplay VFX | B. ArchitectureGuardExpectationStale | source contains forbidden `GameObject` token | `GameplayVfxLifetimeTrace.cs` in VFX runtime references `GameObject`, `Renderer`, `ParticleSystem`; file predates current diff | PossiblyRelated | Gameplay VFX architecture | Medium | NeedsSmallTargetedFix; decide whether trace file is core or host/runtime boundary |
| 12 | `Game.Feature.Gameplay.Tests.Unit.GameplayVfxTileFeatureGravityFieldMigrationTests.Authoring_DefaultCueMapContainsExitSliderAndRemainingGravityBindings` | `GameplayVfxTileFeatureGravityFieldMigrationTests` | `Authoring_DefaultCueMapContainsExitSliderAndRemainingGravityBindings` | Gameplay VFX | C. DefaultCueMapExpectationStale | expected `EntranceSpawn` default lifetime `0.6f`, actual `5.0f` | default cue map changed in recent VFX range; `TileFeature_EntranceSpawn_Binding.asset` itself was not changed | PossiblyRelated | Gameplay VFX / TileFeature VFX | Medium | NeedsSmallTargetedFix only after owner confirms intended lifetime policy |
| 13 | `Game.Feature.Gameplay.Tests.Unit.LegalityResultCanonicalizationTests.RuntimeTraversalLegalityPolicy_EvaluateDestination_WithRotation_ExportsTopologyUpdateRequirement` | `LegalityResultCanonicalizationTests` | `RuntimeTraversalLegalityPolicy_EvaluateDestination_WithRotation_ExportsTopologyUpdateRequirement` | Topology | E. RuntimeBehaviorRegression | expected `Allowed`, actual `Blocked` | traversal/topology legality result drift | UnrelatedBaseline | Topology / legality | High | NeedsOwnerDecision; topology legality targeted pass |
| 14 | `Game.Feature.Gameplay.Tests.Unit.ReservationReadModelContractTests.MovementReservationBook_CellReservationInfo_DistinguishesUnitSharedSettlementCompatibility` | `ReservationReadModelContractTests` | `MovementReservationBook_CellReservationInfo_DistinguishesUnitSharedSettlementCompatibility` | Gameplay Core | E. RuntimeBehaviorRegression | expected blocker `Unit`, actual `None` | reservation read model compatibility drift | UnrelatedBaseline | Occupancy / reservation | High | NeedsOwnerDecision; occupancy owner |
| 15 | `Game.Feature.Gameplay.Tests.Unit.TileFeatureAudioRuntimeTests.TileFeatureAudioPresentationController_BarricadeBindings_PlayDuplicatesWhenPresent` | `TileFeatureAudioRuntimeTests` | `TileFeatureAudioPresentationController_BarricadeBindings_PlayDuplicatesWhenPresent` | Audio | G. IndependentBroadBaseline | expected same `Sfx_False_Def` instance, got different same-named instance | audio binding identity/fixture drift | UnrelatedBaseline | TileFeature audio | DoNotFixInThisPass | DoNotTouchInP2_4; audio owner |
| 16 | `Game.Feature.Gameplay.Tests.Unit.TileFeatureAudioRuntimeTests.TileFeatureAudioPresentationController_ExitBindings_PlayDuplicatesWhenPresent` | `TileFeatureAudioRuntimeTests` | `TileFeatureAudioPresentationController_ExitBindings_PlayDuplicatesWhenPresent` | Audio | G. IndependentBroadBaseline | expected same `Sfx_False_Def` instance, got different same-named instance | audio binding identity/fixture drift | UnrelatedBaseline | TileFeature audio | DoNotFixInThisPass | DoNotTouchInP2_4; audio owner |
| 17 | `Game.Feature.Gameplay.Tests.Unit.TileFeatureAudioRuntimeTests.TileFeatureAudioPresentationController_PlaybackPolicy_FallbackDuplicatesOrderAndCache` | `TileFeatureAudioRuntimeTests` | `TileFeatureAudioPresentationController_PlaybackPolicy_FallbackDuplicatesOrderAndCache` | Audio | G. IndependentBroadBaseline | expected `ButtonActivated:30`, actual `ButtonActivated:10` | audio fallback duplicate ordering/cache expectation drift | UnrelatedBaseline | TileFeature audio | DoNotFixInThisPass | DoNotTouchInP2_4; audio owner |
| 18 | `Game.Feature.Gameplay.Tests.Unit.TileFeatureEffectResolverTests.DestroyTile_Pipeline_MovingEnemyKilledBeforeAttack_UsesEnemyDeathExit` | `TileFeatureEffectResolverTests` | `DestroyTile_Pipeline_MovingEnemyKilledBeforeAttack_UsesEnemyDeathExit` | Gameplay TileFeature | E. RuntimeBehaviorRegression | expected one enemy death exit payload, actual `0` | destroy-tile enemy death exit presentation changed or fixture lost signal | PossiblyRelated | TileFeature / enemy death presentation | High | NeedsOwnerDecision; possible VFX-adjacent failure, investigate before changing |
| 19 | `Game.Feature.Gameplay.Tests.Unit.TileFeatureVisualPresentationControllerTests.ButtonActivatedRequest_WithMotionContactTiming_WaitsUntilDelayElapses` | `TileFeatureVisualPresentationControllerTests` | `ButtonActivatedRequest_WithMotionContactTiming_WaitsUntilDelayElapses` | Gameplay TileFeature | E. RuntimeBehaviorRegression | expected request count `1`, actual `0` | visual presentation delay/barrier behavior drift | UnrelatedBaseline | TileFeature presentation | High | NeedsOwnerDecision |
| 20 | `Game.Feature.Gameplay.Tests.Unit.TileFeatureVisualPresentationControllerTests.DelayedExitOpenedRequest_BlocksImmediateOpenSyncUntilRequestPlays` | `TileFeatureVisualPresentationControllerTests` | `DelayedExitOpenedRequest_BlocksImmediateOpenSyncUntilRequestPlays` | Gameplay TileFeature | E. RuntimeBehaviorRegression | expected request count `1`, actual `0` | delayed exit-open presentation barrier drift | UnrelatedBaseline | TileFeature presentation | High | NeedsOwnerDecision |
| 21 | `Game.Feature.Gameplay.Tests.Unit.TopologyTransitionPostFxTests.ShowcaseCameraTopologyPresetAssets_UseAssetsDefaultVolumeProfileInsteadOfDeprecatedSettingsProfile` | `TopologyTransitionPostFxTests` | `ShowcaseCameraTopologyPresetAssets_UseAssetsDefaultVolumeProfileInsteadOfDeprecatedSettingsProfile` | Topology | D. AssetReferenceExpectationStale | expected default volume profile GUID text missing | topology post-fx asset reference drift | UnrelatedBaseline | Topology presentation assets | High | NeedsOwnerDecision; asset owner |
| 22 | `Game.Feature.Gameplay.Tests.Unit.WindupForwardCellProjectileTests.WindupForwardCellProjectile_CooldownBlocksRewindupUntilExpired` | `WindupForwardCellProjectileTests` | `WindupForwardCellProjectile_CooldownBlocksRewindupUntilExpired` | Enemy AI | E. RuntimeBehaviorRegression | expected `ForwardCellProjectile`, actual `None` | windup projectile cooldown behavior drift | UnrelatedBaseline | Enemy AI utility | High | NeedsOwnerDecision |
| 23 | `Game.Feature.Gameplay.Tests.Unit.WorldStatePlacementInvariantTests.CreateSnapshot_DetachedBoxSharingUnitCell_RemainsMaterializableAndDoesNotBlockOccupancy` | `WorldStatePlacementInvariantTests` | `CreateSnapshot_DetachedBoxSharingUnitCell_RemainsMaterializableAndDoesNotBlockOccupancy` | Gameplay Core | E. RuntimeBehaviorRegression | debug spawn rejected detached box sharing unit cell | occupancy representability invariant drift | UnrelatedBaseline | WorldState / occupancy | High | NeedsOwnerDecision |
| 24 | `Game.Feature.Stages.Editor.Tests.StageAuthoringArchitectureBoundaryTests.StageResultUi_DoesNotDependOnStageRuntimeBuildResult` | `StageAuthoringArchitectureBoundaryTests` | `StageResultUi_DoesNotDependOnStageRuntimeBuildResult` | UI | B. ArchitectureGuardExpectationStale | UI test file references `StageRuntimeBuildResult` | stage result UI boundary guard detected dependency | UnrelatedBaseline | UI / Stage boundary | Medium | NeedsSmallTargetedFix if boundary owner confirms target shape |
| 25 | `Game.Feature.Stages.Editor.Tests.StageAuthoringButtonObjectiveHelperCommandsTests.TryAddRequiredSecondaryGoal_DuplicateTileEntry_DoesNotAddAgain` | `StageAuthoringButtonObjectiveHelperCommandsTests` | `TryAddRequiredSecondaryGoal_DuplicateTileEntry_DoesNotAddAgain` | Stage | E. RuntimeBehaviorRegression | expected duplicate add `False`, actual `True` | stage objective helper duplicate handling drift | UnrelatedBaseline | Stage authoring | High | NeedsOwnerDecision |
| 26 | `Game.Feature.Stages.Editor.Tests.StageAuthoringExitGoalHelperCommandTests.Generation_SyncedAuthoringPassesValidatorAndPreservesVisualBindings` | `StageAuthoringExitGoalHelperCommandTests` | `Generation_SyncedAuthoringPassesValidatorAndPreservesVisualBindings` | Stage | E. RuntimeBehaviorRegression | generated Exit must use `ActiveFaceOnly` activation | stage exit helper generated invalid gameplay stage | UnrelatedBaseline | Stage authoring | High | NeedsOwnerDecision |
| 27 | `Game.Feature.Stages.Editor.Tests.StageAuthoringExitGoalHelperCommandTests.ObjectiveHelper_RepairExitContract_GeneratedStageDefinitionValidates` | `StageAuthoringExitGoalHelperCommandTests` | `ObjectiveHelper_RepairExitContract_GeneratedStageDefinitionValidates` | Stage | E. RuntimeBehaviorRegression | generated Exit must use `ActiveFaceOnly` activation | stage exit repair helper leaves invalid activation rule | UnrelatedBaseline | Stage authoring | High | NeedsOwnerDecision |
| 28 | `Game.Feature.Stages.Editor.Tests.StageCatalogCiValidationEntryPointTests.Run_WritesGovernanceAndAliasUsageValidationSections` | `StageCatalogCiValidationEntryPointTests` | `Run_WritesGovernanceAndAliasUsageValidationSections` | Stage | G. IndependentBroadBaseline | expected CI result `0`, actual `1` | stage catalog CI emits validation warning/failure | UnrelatedBaseline | Stage catalog CI | DoNotFixInThisPass | DoNotTouchInP2_4; depends on stage catalog warning set |
| 29 | `Game.Feature.Stages.Editor.Tests.StageCompatUsageReportingTests.KnownWarningLedger_MatchesCurrentCatalogWarningExactSet` | `StageCompatUsageReportingTests` | `KnownWarningLedger_MatchesCurrentCatalogWarningExactSet` | Stage | D. AssetReferenceExpectationStale | expected empty warning set, actual multiple stage validation issues | stage known-warning ledger stale or catalog invalid | UnrelatedBaseline | Stage catalog | High | NeedsOwnerDecision |
| 30 | `Game.Feature.Gameplay.Tests.Replay.TickReplayDeterminismTests.Replay_FlipBoxScenario_ProducesSameHashTraceAndEventLog` | `TickReplayDeterminismTests` | `Replay_FlipBoxScenario_ProducesSameHashTraceAndEventLog` | Tick Simulation | G. IndependentBroadBaseline | expected trace with unit facing `Left`; actual facing `Right` plus box archetype field | replay golden trace stale or runtime changed | UnrelatedBaseline | Replay determinism | DoNotFixInThisPass | DoNot update golden without owner review |
| 31 | `Game.Feature.Gameplay.Tests.Replay.TickReplayDeterminismTests.Replay_ItemScenario_ProducesSameHashTraceAndEventLog` | `TickReplayDeterminismTests` | `Replay_ItemScenario_ProducesSameHashTraceAndEventLog` | Tick Simulation | G. IndependentBroadBaseline | expected `Boundary=BoxActionMovement`; actual trace lacks it | replay golden trace stale or movement boundary changed | UnrelatedBaseline | Replay determinism | DoNotFixInThisPass | DoNot update golden without owner review |
| 32 | `Game.Feature.Gameplay.Tests.Replay.TickReplayDeterminismTests.Replay_PushBoxBoardEdgeScenario_ProducesSameHashTraceAndEventLog` | `TickReplayDeterminismTests` | `Replay_PushBoxBoardEdgeScenario_ProducesSameHashTraceAndEventLog` | Tick Simulation | G. IndependentBroadBaseline | expected sliding box trace; actual first entity trace differs | replay golden trace stale or push behavior changed | UnrelatedBaseline | Replay determinism | DoNotFixInThisPass | DoNot update golden without owner review |
| 33 | `Game.Feature.Gameplay.Tests.Replay.TickReplayDeterminismTests.Replay_PushBoxEntityStopperScenario_ProducesSameHashTraceAndEventLog` | `TickReplayDeterminismTests` | `Replay_PushBoxEntityStopperScenario_ProducesSameHashTraceAndEventLog` | Tick Simulation | G. IndependentBroadBaseline | expected `True`, actual `False` | replay golden/event assertion drift | UnrelatedBaseline | Replay determinism | DoNotFixInThisPass | DoNot update golden without owner review |
| 34 | `Game.Feature.Gameplay.Tests.Replay.TickReplayDeterminismTests.Replay_PushBoxTerrainStopperScenario_ProducesSameHashTraceAndEventLog` | `TickReplayDeterminismTests` | `Replay_PushBoxTerrainStopperScenario_ProducesSameHashTraceAndEventLog` | Tick Simulation | G. IndependentBroadBaseline | historical terrain stopper trace | superseded by ADR-007 terrain truth removal; do not use as current contract | UnrelatedBaseline | Replay determinism | SupersededByADR007 | current replay blocker coverage uses board edge/solid vocabulary |
| 35 | `Game.Feature.Gameplay.Tests.Scenario.AttackPhaseScenarioTests.Attack_OnHit_DoesNotCreateSameTickNewIntent` | `AttackPhaseScenarioTests` | `Attack_OnHit_DoesNotCreateSameTickNewIntent` | Tick Simulation | E. RuntimeBehaviorRegression | expected count `1`, actual `2` | same-tick attack re-entry/new intent behavior drift | UnrelatedBaseline | Tick simulation / attack | High | NeedsOwnerDecision |
| 36 | `Game.Feature.Gameplay.Tests.Scenario.AttackPhaseScenarioTests.Attack_OnHit_DoesNotReenterMovementPhase` | `AttackPhaseScenarioTests` | `Attack_OnHit_DoesNotReenterMovementPhase` | Tick Simulation | E. RuntimeBehaviorRegression | expected count `1`, actual `2` | attack hit re-enters movement phase or duplicate signal | UnrelatedBaseline | Tick simulation / attack | High | NeedsOwnerDecision |
| 37 | `Game.Feature.Gameplay.Tests.Scenario.BoundaryInventoryScenarioTests.BoundaryInventory_GlideChaserAsset_DefaultGameplay_ActiveGlideKinematicMovesOverSolid` | `BoundaryInventoryScenarioTests` | `BoundaryInventory_GlideChaserAsset_DefaultGameplay_ActiveGlideKinematicMovesOverSolid` | Enemy AI | E. RuntimeBehaviorRegression | expected `48`, actual `41` | enemy glide boundary inventory drift | UnrelatedBaseline | Enemy glide | High | NeedsOwnerDecision |
| 38 | `Game.Feature.Gameplay.Tests.Scenario.EnemyAiScenarioTests.GlideActive_Kinematic_ContactAtCommit` | `EnemyAiScenarioTests` | `GlideActive_Kinematic_ContactAtCommit` | Enemy AI | E. RuntimeBehaviorRegression | expected contact at commit `True`, actual `False` | enemy glide contact timing drift | UnrelatedBaseline | Enemy glide | High | NeedsOwnerDecision |
| 39 | `Game.Feature.Gameplay.Tests.Scenario.EnemyAiScenarioTests.GlideActive_Kinematic_NoContactBeforeCommit` | `EnemyAiScenarioTests` | `GlideActive_Kinematic_NoContactBeforeCommit` | Enemy AI | E. RuntimeBehaviorRegression | debug shows same-cell contact and damage before commit | enemy glide contact timing regression or expectation drift | UnrelatedBaseline | Enemy glide | High | NeedsOwnerDecision |

## 상세 분석: GameplayVfxArchitectureTests

Failure:

- `Game.Feature.Gameplay.Tests.Unit.GameplayVfxArchitectureTests.CoreSource_DoesNotReferenceSnapshotOrProductionRuntimeTypes`
- message: expected source not to contain `GameObject`
- key stack: `GameplayVfxArchitectureTests.cs:477`

확인 결과:

- `Assets/_Features/Gameplay/Gameplay_Vfx/Runtime/GameplayVfxLifetimeTrace.cs`가 `GameObject`, `ParticleSystem`, `Renderer`를 참조한다.
- 해당 파일은 `origin/main..HEAD` 최근 VFX SourceCloneMotion diff의 변경 파일이 아니다.
- 최근 diff에서 변경된 것은 guard/doc/test 확장 쪽이며, core source scan의 boundary 정의가 기존 runtime trace file을 포함하면서 실패가 드러난 형태다.
- `WorldState`, `WorldSnapshot`, `TickPipeline`, `CreateSnapshot` 쪽 authority token 실패는 현재 message의 1차 원인이 아니다.

판정:

- type: B. ArchitectureGuardExpectationStale
- related recent diff: PossiblyRelated
- risk: Medium

권장 조치:

- P2-4에서는 수정하지 않는다.
- 후속 targeted 작업에서 `GameplayVfxLifetimeTrace`가 VFX core source에 남아도 되는 diagnostic utility인지, host/runtime 쪽으로 이동해야 하는 production Unity dependency인지 결정한다.
- 단순 allowlist 추가는 마지막 수단이다.

## 상세 분석: GameplayVfxTileFeatureGravityFieldMigrationTests

Failure:

- `Game.Feature.Gameplay.Tests.Unit.GameplayVfxTileFeatureGravityFieldMigrationTests.Authoring_DefaultCueMapContainsExitSliderAndRemainingGravityBindings`
- message: expected `0.600000024f`, actual `5.0f`
- key stack: `GameplayVfxTileFeatureGravityFieldMigrationTests.cs`

확인 결과:

- 실패 지점은 `TileFeature_EntranceSpawn_Binding.asset`의 `DefaultLifetimeSeconds` expectation이다.
- 실제 binding 값은 `defaultLifetimeSeconds: 5`, `tailSeconds: 5`다.
- `origin/main..HEAD`에서 `GameplayVfxHostDefaultCueMap.asset`은 SourceCloneMotion/reserved cue 추가로 변경됐다.
- `TileFeature_EntranceSpawn_Binding.asset` 자체는 최근 VFX SourceCloneMotion diff에서 변경되지 않았다.
- test name은 `DefaultCueMapContainsExitSliderAndRemainingGravityBindings`지만 실제 assertion 범위에는 entrance spawn lifetime/prefab validation도 포함되어 있어 migration/history expectation이 넓게 남아 있다.

판정:

- type: C. DefaultCueMapExpectationStale
- related recent diff: PossiblyRelated
- risk: Medium

권장 조치:

- P2-4에서는 asset 값을 바꾸지 않는다.
- 후속 targeted 작업에서 entrance spawn intended lifetime이 `0.6f`인지 `5.0f`인지 VFX/TileFeature owner가 결정한다.
- 현재 runtime policy 중심으로 테스트를 분리하거나 이름을 좁히는 것은 가능하지만, expected 값만 근거 없이 갱신하지 않는다.

## 수정 그룹

### Group 1: SafeToFixNow

현재 39개 중 확실한 Group 1 항목은 없다.

- stale FQN rename fallout: 없음
- stale test class/method reference: 없음
- doc guard string-only update: 없음
- stratification override update: 없음

### Group 2: NeedsSmallTargetedFix

| item | reason | next action |
|---|---|---|
| `GameplayVfxArchitectureTests.CoreSource_DoesNotReferenceSnapshotOrProductionRuntimeTypes` | architecture guard boundary or file placement decision needed | decide trace utility boundary; then move file or adjust guard |
| `GameplayVfxTileFeatureGravityFieldMigrationTests.Authoring_DefaultCueMapContainsExitSliderAndRemainingGravityBindings` | default cue expectation vs actual binding policy mismatch | confirm intended `EntranceSpawn` lifetime and split stale migration assertion |
| `FinalizeNoRecheckArchitectureTests.TickPipeline_CleanupRespawnDirectWritePath_IsDocumentedAndBounded` | architecture guard expected count stale or new path undocumented | audit third path and update code/doc/test consistently |
| `StageAuthoringArchitectureBoundaryTests.StageResultUi_DoesNotDependOnStageRuntimeBuildResult` | boundary guard found UI dependency | remove dependency if violation; otherwise update boundary contract |

### Group 3: NeedsOwnerDecision

Includes runtime/asset behavior ambiguity in Stage, TileFeature, Topology, Enemy AI, UI, Gameplay Core, and presentation assets:

- Stage respawn, stage authoring, stage catalog warning failures
- UIAccess runtime failures
- TileFeature presentation and gravity-field material failures
- Topology legality and post-fx asset failures
- Enemy AI glide and utility projectile failures
- Occupancy/reservation/world snapshot invariant failures
- Attack phase runtime failures

### Group 4: DoNotTouchInP2_4

Includes unrelated broad baseline and high-risk stale golden/asset/audio rows:

- Audio binding identity/order rows
- Replay determinism golden rows
- Broad enemy glide scenario rows
- High-risk asset reference rows requiring editor/owner validation

## 수정한 항목

| test/file | 수정 전 문제 | 수정 내용 | runtime/asset 영향 여부 | 관련 테스트 |
|---|---|---|---|---|
| `Docs/Testing/Full-Lane-Baseline-P2-4-2026-05-26.md` | full 39개 실패가 P2-4 기준으로 별도 inventory화되어 있지 않음 | full failure inventory, VFX guard status, grouping, risk/action 기록 추가 | 없음 | `git diff --check`, `./run_tests.sh core`, `./run_tests.sh full` |

## 수정하지 않은 항목

| test/file | 이유 | 필요한 owner decision | 다음 조치 |
|---|---|---|---|
| VFX architecture guard failure | boundary definition or file ownership decision required | `GameplayVfxLifetimeTrace` core/runtime placement | targeted architecture pass |
| VFX TileFeature/Gravity default cue failure | binding lifetime policy unclear; asset edit prohibited in P2-4 | `EntranceSpawn` intended lifetime and test naming/scope | targeted VFX/TileFeature pass |
| Audio failures | unrelated broad baseline and identity/order policy risk | audio duplicate binding policy | audio owner pass |
| Replay golden failures | broad deterministic traces can hide runtime behavior changes | whether traces reflect intended runtime | replay owner pass |
| Stage/UI/Topology/EnemyAI runtime failures | high-risk runtime behavior areas | domain owners must confirm intended behavior | separate targeted lanes |
| Asset reference failures | prefab/material/profile edits require editor/manual evidence | asset owner intent | asset-specific validation pass |

## 안정성 확인

| item | changed in P2-4 |
|---|---|
| runtime code behavior | no |
| binding asset | no |
| prefab reference | no |
| enum numeric value | no |
| default cue map | no |
| common host | no |
| meta/GUID | no |
| runtime-created host implementation | no |
| P2-3 test naming cleanup rollback | no |

## 남긴 TODO

- VFX architecture owner: decide `GameplayVfxLifetimeTrace` placement or guard boundary.
- VFX/TileFeature owner: decide `TileFeature_EntranceSpawn_Binding.asset` intended `DefaultLifetimeSeconds`.
- TileFeature owner: investigate enemy death exit payload loss in destroy-tile pipeline before calling it baseline.
- Audio owner: review duplicate binding identity/order policy.
- Replay owner: review five stale/different deterministic traces; do not bulk-update expected strings without runtime sign-off.
- Stage owner: resolve stage authoring exit activation and catalog warning failures.
- UI owner: resolve `GameplayUiAccessRuntimeTests` mapping/readiness failures.
- Enemy AI owner: resolve glide contact timing and windup forward-cell projectile cooldown failures.

## P2-5 Addendum: Gameplay VFX Lifetime Trace Boundary

P2-5 resolved the Gameplay VFX architecture guard failure:

- `Game.Feature.Gameplay.Tests.Unit.GameplayVfxArchitectureTests.CoreSource_DoesNotReferenceSnapshotOrProductionRuntimeTypes`

판정:

- `GameplayVfxLifetimeTrace` was mixed responsibility before P2-5.
- Core-safe cue/request/policy trace formatting remains in `Gameplay_Vfx/Runtime/GameplayVfxLifetimeTrace.cs`.
- Unity object hierarchy, particle, renderer, transform, and Unity object-context inspection moved to `Gameplay_VfxHost/Runtime/Diagnostics/GameplayVfxLifetimeUnityTrace.cs`.
- This is implementation option B, pure trace formatter plus Unity adapter split. It is not a single-file allowlist.

Guard boundary:

- `Gameplay_Vfx/Runtime` remains the core source scan target for authority/runtime tokens.
- `GameObject`, `Renderer`, `MonoBehaviour`, and `ParticleSystem` are still forbidden in that core scan.
- `Gameplay_VfxHost/Runtime/Diagnostics` is the allowed host-runtime diagnostics location for UnityEngine object inspection.
- Runtime behavior, binding assets, prefab references, enum numeric values, default cue map, common host, and runtime-created host implementation were not changed.

Test evidence:

| command | result | notes |
|---|---|---|
| `git diff --check` | passed | no whitespace errors |
| `"/mnt/c/Program Files/dotnet/dotnet.exe" build Game.Feature.Gameplay.Tests.csproj -c Debug --no-restore` | passed | 0 warnings, 0 errors |
| `./run_tests.sh core` | passed | Core EditMode `63 total / 0 failed`; Core PlayMode `5 total / 0 failed` |
| `./run_tests.sh full` | failed | Full EditMode `4843 total / 38 failed`; Full PlayMode not run because EditMode remains red |

Failure count 변화:

- P2-5 수정 전 full failure count: `39`
- P2-5 수정 후 full failure count: `38`
- Added architecture boundary tests: `2`, both passed.
- Removed failure: `GameplayVfxArchitectureTests.CoreSource_DoesNotReferenceSnapshotOrProductionRuntimeTypes`
- New failure 여부: no

Passed targeted architecture evidence:

- `GameplayVfxArchitectureTests.CoreSource_DoesNotReferenceSnapshotOrProductionRuntimeTypes`
- `GameplayVfxArchitectureTests.RuntimeDiagnostics_MayReferenceUnityRuntimeObjects`
- `GameplayVfxArchitectureTests.GameplayVfxLifetimeTrace_IsHostRuntimeDiagnostic_NotCorePolicy`

VFX regression evidence from the same full EditMode XML:

- `DestroyShrink_Plays_WithSourceClone_WhenPrefabIsMissing`: passed
- `FlipDestroySelfMotion_Plays_WithSourceClone_WhenPrefabIsMissing`: passed
- `ImpactTransientBreak_Plays_WithSourceClone_WhenPrefabIsMissing`: passed
- `OutOfBoundsExit_Box_Plays_WithSourceClone_WhenPrefabIsMissing`: passed
- `OutOfBoundsExit_Enemy_Plays_WithSourceClone_WhenPrefabIsMissing`: passed
- `EnemyDeathMotion_UsesFallbackPrefab_WhenSourceViewIsUnavailable_IfPolicyAllows`: passed
- `EnemyDeathMotion_SeparatesMissingSourceView_FromMissingPrefab`: passed
- `PrefabOnlyCue_ReportsMissingPrefab_WhenPrefabIsNull`: passed
- SourceCloneMotion common host path guards: passed
- P2-2 SourceCloneMotion host ADR guard: passed
- P2-3 VFX test naming policy guard: passed

Remaining TODO after P2-5:

- VFX/TileFeature owner: `GameplayVfxTileFeatureGravityFieldMigrationTests.Authoring_DefaultCueMapContainsExitSliderAndRemainingGravityBindings` / `TileFeature_EntranceSpawn_Binding.asset` lifetime policy targeted pass.
- TileFeature owner: investigate enemy death exit payload loss in destroy-tile pipeline before calling it baseline.
- Remaining full baseline failures stay assigned to their P2-4 owner areas.

## P2-6 Addendum: TileFeature EntranceSpawn Lifetime Policy

P2-6 resolved the stale `EntranceSpawn` lifetime expectation in:

- `Game.Feature.Gameplay.Tests.Unit.GameplayVfxTileFeatureGravityFieldMigrationTests.Authoring_DefaultCueMapContainsExitSliderAndRemainingGravityBindings`

판정:

- Intended `TileFeature_EntranceSpawn_Binding.asset` active lifetime is `5.0f`, not `0.6f`.
- The binding was introduced in `2cb9aa54` with `defaultLifetimeSeconds: 5` and `tailSeconds: 5`.
- The prefab introduced with the same commit contains multiple child ParticleSystems: `SparkTrail.lengthInSec: 0.6`, `HolyMuzzle.lengthInSec: 5`, and `Sparks.lengthInSec: 0.1`.
- The old `0.6f` expectation came from the broad migration test added in `7fbcba29`; it matched one child sub-effect, not the whole cue-level authored lifetime.
- `EntranceSpawn` remains `PrefabOnly + ExplicitPrefabRequired + AuthoredDuration`; it does not use SourceCloneMotion, a source clone, or the common empty host path.

수정 방향:

- Selected option: C + evidence-based B.
- The default cue map membership test was renamed to `GameplayVfxTileFeatureGravityField_DefaultCueMap_ContainsExitSliderAndRemainingGravityBindings` and now validates map membership only.
- EntranceSpawn binding policy is covered by `TileFeatureEntranceSpawn_BindingPolicy_UsesFiveSecondAuthoredDuration`.
- EntranceSpawn prefab validation and five-second particle evidence are covered by `TileFeatureEntranceSpawn_PrefabPolicy_IsActualVisualPrefab`.
- Runtime behavior, binding asset values, prefab references, enum numeric values, default cue map entries, common host, and runtime-created host implementation were not changed.

Value evidence:

| value | source | meaning | confidence | decision impact |
|---|---|---|---|---|
| `0.6f` | `7fbcba29` test assertion; `TileFeature_EntranceSpawn_Vfx.prefab` `SparkTrail.lengthInSec` | stale expectation or one child sub-effect duration | medium | not used as cue-level lifetime |
| `5.0f` | `2cb9aa54` binding initial authoring; `TileFeature_EntranceSpawn_Vfx.prefab` `HolyMuzzle.lengthInSec` | authored cue active lifetime | high | policy expected value |
| `tailSeconds: 5` | `2cb9aa54` binding initial authoring | release tail window | medium-high | preserved as current authored policy |

Test evidence:

| command | result | notes |
|---|---|---|
| `git diff --check` | passed | whitespace check clean |
| `"/mnt/c/Program Files/dotnet/dotnet.exe" build Game.Feature.Gameplay.Tests.csproj -c Debug --no-restore` | passed | 0 warnings, 0 errors |
| `./run_tests.sh core` | passed | Core EditMode `63/0`; Core PlayMode `5/0` |
| `./run_tests.sh full` | failed baseline red | Full EditMode `4845` total, `37` failed, `4807` passed, `1` skipped; Full PlayMode did not run because EditMode was red |

Failure count 변화:

- P2-5 full baseline: `38` failures.
- P2-6 full lane after split: `37` failures.
- Target stale lifetime failure removed; no new VFX regression was observed in the full EditMode XML.

Remaining TODO after P2-6:

- TileFeature owner: investigate enemy death exit payload loss in destroy-tile pipeline before calling it baseline.
- Remaining full baseline failures stay assigned to their P2-4 owner areas.
