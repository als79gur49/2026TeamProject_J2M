# Campaign Main Stage Content Migration Report

> Historical snapshot — not current authority. Current Campaign membership/order,
> eligibility, Player inclusion, and closeout status are defined in
> `Campaign-Stage-Sequence-Authority.md`. Counts and supported-stage lists below
> describe the 2026-05-09 migration checkpoint only.
> Mentions of retired-save compatibility below are checkpoint history. The
> runtime `stage-5-1` auto-repair policy was removed on 2026-08-25; archived
> `legacy-stage-5-1` content and alias/catalog governance remain current.

Generated: 2026-05-09

## Summary
- Policy applied: "초기에는 전부 Campaign에 넣고, 이후 필요해지면 Level/Stage로 내린다."
- Campaign root: `Assets/_Features/Stages/Content/Campaigns/campaign-main`
- Current migrated campaign asset count: 374 non-meta assets.
- Stage companion folders migrated: 10 catalog stage folders.
- Campaign `_Shared` migrated support asset count: 318 non-meta assets.
- Level/stage `_Shared` and `_Overrides`: not created.

## Moved Assets
- Catalog assets moved to `Campaign/Catalog` and renamed:
  - `CampaignMain_StageCatalog.asset`
  - `CampaignMain_StageCatalogProvider.asset`
  - `CampaignMain_StageIdAliasTable.asset`
  - `CampaignMain_StageSequence.asset`
- Stage companion folders moved to `Levels/level-01/Stages`:
  - `legacy-stage-5-1`
  - `stage-0-1`
  - `stage-0-2`
  - `stage-1-1`
  - `stage-2-1`
  - `stage-2-2`
  - `stage-3-1`
  - `stage-3-2`
  - `stage-4-1`
  - `stage-4-2`
- Stage condition assets moved to `_Shared/Gameplay/Conditions`.
- Enemy AI profiles/core/brain/capabilities/catalogs moved to `_Shared/Gameplay/EnemyAI`.
- Enemy/static/board/tile/topology/VFX presentation support moved to `_Shared/Presentation`.
- Catalog-referenced enemy view prefabs previously under `Gameplay_Entities/Runtime` moved to `_Shared/Presentation/Enemy/Prefabs`.

## Legacy Folders
- Deleted/emptied legacy support roots:
  - the deleted legacy combined gameplay showcase stage folder under `Assets/_Features/Stages`
  - the deleted legacy tutorial scene stage folder under `Assets/_Features/Stages`
- Deleted/emptied loose content roots:
  - `Assets/_Features/Stages/Content/legacy-stage-5-1`
  - `Assets/_Features/Stages/Content/stage-0-1`
  - `Assets/_Features/Stages/Content/stage-0-2`
  - `Assets/_Features/Stages/Content/stage-1-1`
  - `Assets/_Features/Stages/Content/stage-2-1`
  - `Assets/_Features/Stages/Content/stage-2-2`
  - `Assets/_Features/Stages/Content/stage-3-1`
  - `Assets/_Features/Stages/Content/stage-3-2`
  - `Assets/_Features/Stages/Content/stage-4-1`
  - `Assets/_Features/Stages/Content/stage-4-2`

## Variants
- No support assets were merged by value.
- Stage-named variants were kept as Campaign variants under `_Shared`, including:
  - `EnemyPresentationArchetypeCatalog_CampaignMainEnemy.asset`
  - `StaticPresentationCatalog_CampaignMainStatic.asset`
  - `StaticPresentationCatalog_MechanicsShowcase.asset`
  - `GameplayCameraTopologyPreset_CampaignMainFastPostFx.asset`
  - `GameplayCameraTopologyPreset_CampaignMainQualityPostFx.asset`

## Stage Id Rename

| Old id | New id | Notes |
| --- | --- | --- |
| `stage-5-1` | `legacy-stage-5-1` | catalog-preserved legacy content, excluded from sequence/direct-play |

Aliases are recorded in `CampaignMain_StageIdAliasTable.asset` and `StageAliasGovernanceLedger.asset`.

## Current Stage Classification

- Sequence/current campaign stages:
  - `stage-0-1`
  - `stage-0-2`
  - `stage-1-1`
  - `stage-2-1`
  - `stage-2-2`
  - `stage-3-1`
  - `stage-3-2`
  - `stage-4-1`
  - `stage-4-2`
- Direct-play supported stages:
  - `stage-0-1`
  - `stage-1-1`
- Catalog-only legacy/archived stages:
  - `legacy-stage-5-1`

`legacy-stage-5-1` is catalog-preserved legacy/archived content.
It is intentionally excluded from the campaign sequence and editor direct-play catalog.
At this checkpoint, the old `stage-5-1` id remained for alias/governance and retired-save compatibility.
Current runtime uses it only in alias/governance and historical evidence; it is not a save migration source.

## Shared Asset Rename Map

| Old | New |
| --- | --- |
| `TileFeaturePresentationCatalog_CombinedGameplayShowcase` | `TileFeaturePresentationCatalog_CampaignMainBoard` |
| `BoardTilePresentationCatalog_CombinedGameplayShowcase` | `BoardTilePresentationCatalog_CampaignMainBoard` |
| `BoardTileStyleCatalog_CombinedGameplayShowcase` | `BoardTileStyleCatalog_CampaignMainBoard` |
| `BoardTileOverlayCatalog_CombinedGameplayShowcase` | `BoardTileOverlayCatalog_CampaignMainBoard` |
| `BoardRoot_CombinedGameplayShowcase` | `BoardRoot_CampaignMainBoard` |
| `BoardPresentationProfile_CombinedGameplayShowcase` | `BoardPresentationProfile_CampaignMainBoard` |
| `EnemyUnitArchetypeCatalog_CombinedGameplayShowcase` | `EnemyUnitArchetypeCatalog_CampaignMainEnemy` |
| `EnemyPresentationArchetypeCatalog_CombinedGameplayShowcase` | `EnemyPresentationArchetypeCatalog_CampaignMainEnemy` |
| `StaticPresentationCatalog_TutorialScene` | `StaticPresentationCatalog_CampaignMainStatic` |
| `GameplayCameraTopologyPreset_CombinedGameplayShowcase` | `GameplayCameraTopologyPreset_CampaignMainFastPostFx` |
| `GameplayCameraTopologyPreset_TutorialScene` | `GameplayCameraTopologyPreset_CampaignMainQualityPostFx` |

All renamed `.asset` / `.prefab` files retained their `.meta` files and GUIDs.

## Governance Ledger Note

`StageCatalogKnownWarningLedger.asset` was updated because the governance validator checks expected path/name/stage id in addition to issue code and asset GUID.
The ledger update is therefore part of the rename contract, not unrelated churn.

## Deferred Follow-up

`StageBackedGameplaySceneInstaller` is the concrete scene bootstrap installer used by `UIAudioScene`.
It is a scene/bootstrap component, not a StageCatalog id, StageContentEntry id, direct-play id, or alias id.
The old `CombinedGameplayShowcaseInstaller` concrete symbol was renamed with its MonoScript `.cs.meta` GUID preserved.
`StageBackedGameplaySceneInstallerBase` is the abstract base for stage-backed gameplay scene bootstrap installers.
The base class rename removes the old `Showcase` vocabulary from the current bootstrap composition contract without changing StageCatalog ids, StageContentEntry assets, alias governance, direct-play catalog entries, stage sequence, or save compatibility.

Follow-up issue:
`Bootstrap symbol rename: CombinedGameplayShowcaseInstaller -> StageBackedGameplaySceneInstaller` and base vocabulary cleanup complete.

## Old Token Allowlist

Allowed remaining runtime/governance old-token hits:
- intentional alias/governance:
  - `stage-5-1`
- historical save compatibility at this checkpoint:
  - retired completed `stage-5-1` (runtime auto-repair removed 2026-08-25)
- forbidden legacy path validators:
  - deleted combined gameplay showcase stage path audits
  - deleted tutorial scene stage path audits

Allowed docs/history old-token hits:
- historical docs/archive only

Not allowed:
- live StageCatalog canonical ids using old ids
- direct-play catalog using old ids
- StageContentEntry folder/file/object names using old ids
- shared presentation/support asset `m_Name` using old scene-derived names
- current architecture docs describing old ids as canonical

## Runtime Code Kept
- `Assets/_Features/Stages/Runtime`
- `Assets/_Features/Stages/Editor`
- `Assets/_Features/Gameplay`
- `Assets/_Features/UI`
- `Assets/_Features/Flow/Flow_Audio`
- `Assets/_Shared/Audio`
- `Assets/_Shared/AudioContracts`

## Validators
- Added `StageCampaignContentGovernanceValidator`.
- Added `StageCampaignMainContentSmokeCheck` for campaign catalog/provider/direct-play/resolver/reference-path smoke verification.
- Added Campaign governance to build/preprocess validation and StageCatalog CI validation.
- Updated canonical stage content validation to use `StageContentPaths.CampaignLevel01StagesRoot`.
- Updated direct-play/editor tooling to use `StageContentPaths` instead of legacy hardcoded catalog paths.

## Reference Repairs
- `CampaignMain_StageCatalog` now contains 10 catalog entries, including the 9-stage current campaign sequence and catalog-only legacy/archived `legacy-stage-5-1`.
- `StageEditorDirectPlayCatalog` declares `Assets/Scenes/UIAudioScene.unity` as the canonical gameplay shell and supports quick-launch stage ids `stage-0-1` and `stage-1-1`.
- `StageRuntimeContentResolver` smoke checked launch-context resolution for `stage-0-1` and `stage-1-1`.

## Addressables
- Not applicable: `com.unity.addressables` is not installed/configured, so no package dependency or groups were added.

## Verification
- `StageCampaignMainMigrationRunner.ExecuteFromCommandLine`: passed.
- `StageCampaignMainContentSmokeCheck.RunFromCommandLine`: passed.
- `StageCatalogCiValidationEntryPoint.RunFromCommandLine`: passed.
- Phase 1.1 `StageCampaignMainContentSmokeCheck.RunFromCommandLine`: passed, log `TestResults/campaign-phase-1-1/campaign-main-smoke.log`.
- Phase 1.1 `StageCatalogCiValidationEntryPoint.RunFromCommandLine`: passed, log `TestResults/campaign-phase-1-1/stage-catalog-ci-after-ledger.log`.
- Phase 1.1 target class EditMode reruns:
  - `StageAuthoringExitGoalHelperCommandTests`: 33 total, 33 passed, 0 failed, 0 skipped.
  - `FullEditModeKnownFailureBaselineTests`: 8 total, 8 passed, 0 failed, 0 skipped.
  - `CombinedGameplayShowcaseInstallerTests`: 26 total, 16 passed, 10 failed, 0 skipped; campaign active-slot mismatch failures are resolved.
  - `GameplayCameraTopologyAuthoringExtractionArchitectureTests`: 17 total, 17 passed, 0 failed, 0 skipped.
- Phase 1.1 Full EditMode via `TestRunnerCliBootstrap.RunEditMode -codexSelection full`: executed 3606 tests, 3510 passed, 95 failed, 1 skipped. The remaining failures are existing gameplay/replay/UI/simulation baseline failures outside Campaign path governance repair.
- Requested classes passing in the full run:
  - `StageContentAndClearFlowTests`
  - `StageDefaultStageIdPolicyTests`
  - `StageSceneBootstrapValidatorTests`
  - `StageLoadArchitectureTests`
  - `GameplayUiFlowIntegrationTests`
  - `UiArchitectureTests`
  - `AudioArchitectureTests`
  - `StageCatalogCiValidationEntryPointTests`
  - `SaveSlotValidationAndDirectPlayTests`
  - `TopologyTransitionPostFxTests`
  - `GameplayVfxLegacyOldPathCleanupTests`
- Requested classes still failing in the full run:
  - `StageRuntimeBuilderTests`: 1 failure.
  - `EnemyAiProfileAssetContractTests`: 2 failures.
  - `EnemyViewIsolationTests`: 3 failures.
  - `TickReplayDeterminismTests`: 8 failures.

## Phase 1.1 Follow-up
- Fixture changes:
  - `StageAuthoringExitGoalHelperCommandTests` now creates its test `StageContentEntry` under `StageContentPaths.CampaignLevel01StagesRoot` and cleans up only the generated per-test stage folder plus generated condition asset.
  - The shared-condition rejection test now uses a valid Campaign owner path before validating shared-condition ownership.
  - `StageBackedGameplaySceneInstallerTests` now launch current direct-play stages matching `StageEditorDirectPlayCatalog`, and seed deterministic Campaign temp slot/direct-play launch state.
  - `GameplayCameraTopologyAuthoringExtractionArchitectureTests` now validates the canonical shell scene and stage topology preset assets separately before building configuration.
- Production code changes: none. Campaign owner-path validation, direct-play mapping, active-slot validation, `StageDefinition` bootstrap policy, and `defaultStageId` removal remain unchanged.
- Asset/reference changes: no runtime stage, gameplay, Campaign structure, or scene asset references were changed. Only the Full EditMode known-failure baseline JSON was rebuilt.
- Newly passing target classes:
  - `StageAuthoringExitGoalHelperCommandTests`
  - `FullEditModeKnownFailureBaselineTests`
  - `GameplayCameraTopologyAuthoringExtractionArchitectureTests`
- Remaining target-class baseline group:
  - `CombinedGameplayShowcaseInstallerTests`: 10 showcase asset/expectation failures remain; the previous campaign active-slot mismatch group is no longer present.
- Full EditMode known-failure ledger:
  - Baseline file: `Assets/_Features/Stages/Editor/Validation/Baselines/FullEditModeKnownFailures.json`.
  - Source XML: `TestResults/campaign-phase-1-1/full-editmode-final.xml`.
  - Summary: 3606 total, 3510 passed, 95 failed, 1 skipped.
  - Known failures by assembly: `Game.Feature.Gameplay.Tests.dll` 59, `Game.Integration.Simulation.Tests.dll` 19, `Game.Integration.Replay.Tests.dll` 8, `Game.Feature.UI.Tests.dll` 5, `Game.TestInfrastructure.dll` 4.
  - `Game.Feature.Stages.Editor.Tests.dll` entries: 0.
- Campaign governance checks:
  - `StageCampaignMainContentSmokeCheck.RunFromCommandLine`: passed.
  - `StageCatalogCiValidationEntryPoint.RunFromCommandLine`: passed.
  - Forbidden roots remain absent: old `Assets/_Features/Stages/Stage_*` support tree, loose `Assets/_Features/Stages/Content/<stage-id>` roots, Level `_Shared`, Stage `_Shared`, and Stage `_Overrides`.

## Phase 1 Pre-Follow-up Target Failure Triage
- Final baseline file: `TestResults/stage-migration-full-editmode-final2.xml`.
- Final baseline count: 3606 total, 3493 passed, 112 failed, 1 skipped.
- Target rerun file: `TestResults/stage-migration-triage/target-classes.xml`.
- Target rerun count: 123 total, 109 passed, 14 failed, 0 skipped.
- No target failure was confirmed as a Campaign migration path/reference/value side-effect.
- No code, test assertion, runtime asset, replay baseline, `StageDefinition`, `EnemyAiProfile`, patrol, or brain asset repair was made in this follow-up.
- `StageRuntimeBuilderTests.StageRuntimeBuilder_CombinedShowcaseStageBuild_ReflectsCurrentConfiguredContract`: classified `C` existing baseline. The test already loads via `StageContentPaths`; the failure is current showcase asset contract drift (`BoxSpawns.Length` expected 12, actual 13, including current extra box `EntityId 201`), not old path/reference breakage.
- `EnemyAiProfileAssetContractTests.EnemyAiProfileAssets_PatrolPilotRollout_MatchesExpectedPatrolKinds`: classified `C` existing baseline. `EnemyBrain_JumpChaser.asset` currently points at the non-attacking random-walk patrol asset; the same JumpChaser random-walk drift appears in older pre-Campaign results under the deleted legacy combined gameplay showcase stage folder.
- `EnemyAiProfileAssetContractTests.EnemyAiProfileAssets_ForwardArchetypes_RetainForwardPatrolKind`: classified `C` existing baseline for the same JumpChaser patrol-kind drift.
- `EnemyViewIsolationTests.GameplayTickViewPresenter_PresentingEnemyFrames_DoesNotChangeLaterTickAuthoritativeResults`: classified `C` existing gameplay/presentation baseline. The test uses generated in-memory test profiles and views, not Campaign presentation assets or moved prefab paths.
- `EnemyViewIsolationTests.GameplayTickViewPresenter_PresentingEnemyMotionAuthoring_DoesNotChangeLocomotionCooldownAuthority`: classified `C` existing gameplay/presentation baseline. The test uses generated in-memory test profiles and views, not Campaign presentation assets or moved prefab paths.
- `EnemyViewIsolationTests.GameplayTickViewPresenter_PresentingSummonedEnemyArchetypeViews_DoesNotChangeLaterTickAuthoritativeResults`: classified `C` existing gameplay/presentation baseline. The test uses generated in-memory archetype/profile/prefab objects, not Campaign presentation catalogs or moved prefab paths.
- `TickReplayDeterminismTests.DeterminismHash_BoxInteractionLockState_IsIncludedInCanonicalState`: classified `C` existing replay baseline. The failure is canonical trace content for box interaction locks and does not load Campaign assets.
- `TickReplayDeterminismTests.DeterminismHash_EnemyJumpState_IsIncludedInCanonicalState`: classified `C` existing gameplay/replay baseline. The failure is an invalid spatial-state source combination, not an asset path or catalog reference.
- `TickReplayDeterminismTests.Replay_EnemyUtilityLockScenario_ProducesStablePerTickHashTraceAndBlockedPush`: classified `C` existing replay baseline. The failure is lock trace/event expectation drift and does not load Campaign assets.
- `TickReplayDeterminismTests.Replay_PassiveContactScenario_ProducesStableHashTraceAndPlayerDamage`: classified `C` existing replay baseline. The failure is passive-contact damage trace state and does not load Campaign assets.
- `TickReplayDeterminismTests.Replay_PlayerMoveIntoUnitStackedScenario_ProducesSameHashTraceAndEventLog`: classified `C` existing replay baseline. The failure is event-log expectation drift and does not load Campaign assets.
- `TickReplayDeterminismTests.Replay_PushBoxBoardEdgeScenario_ProducesSameHashTraceAndEventLog`: classified `C` existing replay baseline. The failure is push movement/final entity expectation drift and does not load Campaign assets.
- `TickReplayDeterminismTests.Replay_PushBoxEntityStopperScenario_ProducesSameHashTraceAndEventLog`: classified `C` existing replay baseline. The failure is player push control/action trace expectation drift and does not load Campaign assets.
- `TickReplayDeterminismTests.Replay_PushBoxTerrainStopperScenario_ProducesSameHashTraceAndEventLog`: classified `C` existing replay baseline. The failure is push movement/final entity expectation drift and does not load Campaign assets.

## Changed Assets/References
- Phase 1.1 rebuilt `Assets/_Features/Stages/Editor/Validation/Baselines/FullEditModeKnownFailures.json` from `TestResults/campaign-phase-1-1/full-editmode-final.xml`.
- Phase 1.1 changed test fixtures only; no runtime Campaign/stage/gameplay assets or scene references were changed.
- No Campaign Phase 1 structure was reverted.
- No Level `_Shared`, Stage `_Shared`, Stage `_Overrides`, old `Assets/_Features/Stages/Stage_*` support tree, or loose `Assets/_Features/Stages/Content/<stage-id>` root was created.
- No `StageDefinition` direct-reference bootstrap, `defaultStageId` fallback, Campaign-owned UI runtime, Campaign-owned audio runtime, or Campaign-content gameplay runtime ownership change was introduced.

## Phase 1 Pre-Follow-up Baseline Failure Inventory
The historical inventory below was captured before Phase 1.1 fixture stabilization and is retained as triage context. The current strict ledger is `Assets/_Features/Stages/Editor/Validation/Baselines/FullEditModeKnownFailures.json`.
- `AttackInputNormalizationTests` (1):
  - `ImpactReservationComparer_PreservesFaceBeforePlanarOrder`: Expected and actual are both `SurfaceCell[3]`; first value expected `Floor(0,1)` but was `Front(0,0)`.
- `AttackPhaseScenarioTests` (3):
  - `Attack_RemovedEntityIntent_SpawnsRemovedEntityDuringCommit`: historical removed entity trace expectation mismatch.
  - `Attack_OnHit_DoesNotCreateSameTickNewIntent`: expected 1, actual 2.
  - `Attack_OnHit_DoesNotReenterMovementPhase`: expected 1, actual 2.
- `CampaignStageFlowTests` (1):
  - `RespawnProcessor_PlayerRespawnGateSuppressesSpawnBeforeWrite`: expected non-negative index, actual -1.
- `CombinedGameplayShowcaseInstallerTests` (13):
  - `CombinedGameplayShowcaseInstaller_Configuration_CarriesStageTileFeatureVisualBindings`: expected empty int array, actual one extra value.
  - `CombinedGameplayShowcaseInstaller_Configuration_UsesStageDefinitionEnemyUnitArchetypeCatalog`: campaign active slot stage mismatch for launch stage `stage-1-1`.
  - `CombinedGameplayShowcaseInstaller_Configuration_UsesStagePresentationDefinitionEnemyPresentationArchetypeCatalog`: campaign active slot stage mismatch for launch stage `stage-1-1`.
  - `CombinedGameplayShowcaseInstaller_ConfiguredShowcaseEnemy_TimingAuthoringFeedsPresenterDriver`: expected true, actual false.
  - `CombinedGameplayShowcaseInstaller_DefaultBundle_IncludesGlideKinematic`: campaign active slot stage mismatch for launch stage `stage-1-1`.
  - `CombinedGameplayShowcaseInstaller_ViewFactory_AttachesTimingAuthoringToConfiguredShowcaseEnemy`: expected true, actual false.
  - `MechanicsShowcaseStage_BuildsEnemyPresentationBindingForConfiguredShowcaseEnemy`: expected 6, actual 5.
  - `MechanicsShowcaseStage_BuildsEnemyProfileOverrideForConfiguredShowcaseEnemy`: expected 6, actual 5.
  - `MechanicsShowcaseStage_BuildsJumpShowcaseProfileOverride`: expected true, actual false.
  - `MechanicsShowcaseStage_BuildsRandomWalkPilotProfileOverrideForNonAttackingEnemy`: expected true, actual false.
  - `MechanicsShowcaseStage_BuildsRandomWalkPilotProfileOverrideForWindupMeleeEnemy`: expected true, actual false.
  - `MechanicsShowcaseStage_PlacesConfiguredShowcaseEnemy`: expected configured enemy on floor face, actual false.
  - `MechanicsShowcaseStage_PlacesJumpShowcaseEnemyOnFarFloorLane`: expected true, actual false.
- `DisplayArchitectureBoundaryTests` (1):
  - `SettingsChildView_Sources_DoNotRebuildAuthoredControlsAtRuntime`: forbidden source token `ResolutionHoverHintText` remains present.
- `EnemyAiProfileAssetContractTests` (2):
  - `EnemyAiProfileAssets_ForwardArchetypes_RetainForwardPatrolKind`: expected `Forward`, actual `RandomWalk`.
  - `EnemyAiProfileAssets_PatrolPilotRollout_MatchesExpectedPatrolKinds`: JumpChaser Campaign profile patrol kind expected `Forward`, actual `RandomWalk`.
- `EnemyLogicTests` (1):
  - `EnemyLogic_RandomWalkPatrolState_CommitsOnlyOnKinematicMovementCommit`: expected true, actual false.
- `EnemyPatrolPhase3DocumentationTests` (1):
  - `EnemyPatrolSourceGovernance_EnemyLogic_DoesNotDirectlyBranchOnForwardOrRandomWalkOutsideProposalSeam`: forbidden `PatrolStrategyKind.RandomWalk` token found in `EnemyLogic`.
- `EnemyPrefabScaffoldTests` (1):
  - `EnemyViewNonAttackingPrefab_UsesMoveOnlyLocomotionAuthoringAlongsideEnemyAnimationTiming`: prefab YAML expected `moveMotionDurationSeconds: 1`, actual value differs.
- `EnemyViewIsolationTests` (3):
  - `GameplayTickViewPresenter_PresentingEnemyFrames_DoesNotChangeLaterTickAuthoritativeResults`: expected `Recover`, actual `Chase`.
  - `GameplayTickViewPresenter_PresentingEnemyMotionAuthoring_DoesNotChangeLocomotionCooldownAuthority`: expected `(1, 0)`, actual `(0, 0)`.
  - `GameplayTickViewPresenter_PresentingSummonedEnemyArchetypeViews_DoesNotChangeLaterTickAuthoritativeResults`: expected `Floor(2,0)`, actual `Floor(1,0)`.
- `FinalizeNoRecheckArchitectureTests` (2):
  - `RespawnProcessor_Process_UsesCanonicalAuthoritativePlacementLegality`: expected method signature was not found.
  - `TickPipeline_RunFinalizePhase_And_FinalizationBatchApplyTo_DoNotReevaluateLegality`: expected method signature was not found.
- `FullEditModeKnownFailureBaselineTests` (1):
  - `FullEditModeBaseline_DefaultBaselineContainsExtractedFailureList`: expected 95, actual 93.
- `GameplayCameraTopologyAuthoringExtractionArchitectureTests` (3):
  - `ShowcaseScenes_PreserveSerializedConfigureMainCameraMeaning`: campaign active slot stage mismatch for launch stage `stage-1-1`.
  - `ShowcaseScenes_PreserveSerializedTopologyRotationTweenEaseMeaning`: campaign active slot stage mismatch for launch stage `stage-1-1`.
  - `ShowcaseScenes_PreserveSerializedTopologyRotationVisualMappingMeaning`: campaign active slot stage mismatch for launch stage `stage-1-1`.
- `GameplayHostCommandAdmissionPolicyTests` (2):
  - `AdmissionPolicy_CommittedControllableActorAccessor_ReusesSameWindowFact_AndRefreshesOnTickCompleted`: expected cached actor state to change, actual remained `Floor(0,0)`.
  - `AdmissionPolicy_Dispose_StopsTickCompletedRefresh_AndLeavesCachedReferenceUnchanged_WithoutFreshnessAssertions`: expected state string to differ, actual matched unchanged state.
- `GameplayTickPresentationCoordinatorTests` (2):
  - `EnemyPupilVisualController_WindupAttackRecover_AnimatesBorderSequence`: expected greater than `0.219999999f`, actual equal.
  - `GameplayTickViewPresenter_PlayerRespawnBeforeDeathHide_Completes_ClearsDeathAnimationState`: expected true, actual false.
- `GameplayTimingOwnershipTests` (1):
  - `PlayerAnimatorDriver_WithAnimatorOverride_PrefersAnimatorDurationOverResolvedMotionDuration`: expected `0.400000006f`, actual `0.25f`.
- `GameplayUiAccessRuntimeTests` (4):
  - `GameplayUiAccess_PlayerHud_ExposesRecoveryCooldown_AsRecoveryOnlySemantic`: expected `Push`, actual `None`.
  - `GameplayUiAccess_PlayerHud_PushReadiness_ActionLock_DisablesPush`: expected false, actual true.
  - `GameplayUiAccess_PreRefreshTransientQueries_ReadPreviousCommittedHudState_BeforeTickCompletedRefresh`: expected at least 1, actual 0.
  - `GameplayUiAccess_PresentationFeed_MapsPlayerSlice_ForHeldMove`: expected true, actual false.
- `GameplayVfxTileFeatureGravityFieldMigrationTests` (1):
  - `Coordinator_DoesNotReferencePr28VisualControllersOrVfxController`: forbidden `TileFeatureVisualPresentationController` reference remains present.
- `GameplayViewProjectionTests` (13):
  - `EnemyAnimatorDriver_InspectorSurface_IsLimitedToCoreAuthoringFields`: inspector field set mismatch.
  - `GameplayBoardSurfaceRenderer_TopologyTransition_StartWorldPosesMatchDestinationVisibleSurfacePoses`: expected `1.71500003f`, actual `1.75f`.
  - `GameplayBoardSurfaceRenderer_TopologyTransition_UsesDestinationVisibleFacesAtStart`: expected `0.980000019f`, actual `1.0f`.
  - `GameplayCubeProjector_ProjectsBottomFaceToHorizontalPlane`: expected `-1.91999996f`, actual `-1.63f`.
  - `GameplayCubeProjector_ProjectsFrontFaceToVerticalPlane`: expected `1.91999996f`, actual `1.62999976f`.
  - `GameplaySceneHost_CameraTarget_StaysOnCubeCenterDuringTopologyTransition`: expected `(-0.50, -0.50, 1.13)`, actual `(-0.50, -0.82, 0.81)`.
  - `GameplaySceneHost_MoveMotion_KeepsWorldQueriesOnCommittedDestinationWhileViewInterpolates`: expected entity query result for `10`, actual empty.
  - `GameplaySceneHost_PushMotion_KeepsWorldQueriesOnCommittedDestinationWhileViewInterpolates`: expected true, actual false.
  - `GameplaySceneHost_TopologyTransition_CameraOrbitPreservesScreenContinuityAtStart`: expected `(-0.50, -0.50, 1.13)`, actual `(-0.50, -1.13, 0.50)`.
  - `GameplayTickViewPresenter_Present_MapsEnemyAttackHitAndMoveSignalsToAnimatorDrivers`: expected 1, actual 0.
  - `GameplayTickViewPresenter_Present_PlayerActionSignals_HoldPushAndFlipUntilPresentationDurationExpires`: expected `Push`, actual `Idle`.
  - `GameplayTickViewPresenter_TopologyMotion_MaintainsBoardRootIdentity`: expected greater than `0.5f`, actual `0.499999911f`.
- `ImpactDispositionArchitectureTests` (1):
  - `ImpactDispositionSymbols_StayWithinNarrowPushFlipRuntimeHostAndTestBoundary`: `ImpactDisposition` use-site found outside the narrow runtime/host/test boundary.
- `LegalityContextGovernanceTests` (1):
  - `LegalityActorRef_CarriesResolvedSpatialState_NotRawSpatialSources`: unexpected `GlideState` field present.
- `MainMenuHubTests` (1):
  - `MainMenuScreenRuntime_KeepsSaveSlotCardsInSerializedOrder`: expected `16.0f`, actual `50.0f`.
- `MovementPhaseScenarioTests` (1):
  - `Movement_MoveFromFrontBottomEdge_FailsWithoutRotation`: expected true, actual false.
- `PlayerControlScenarioTests` (8):
  - `PlayerControl_BlockedMove_UpdatesFacingWithoutMoving`: expected true, actual false.
  - `PlayerControl_CustomPushInputLock_IgnoresNewInputsUntilActionCompletes`: sequence contained no elements.
  - `PlayerControl_ExplicitPushWithoutAdjacentPushTarget_RemainsNoOp`: expected empty intents, actual move intent present.
  - `PlayerControl_LocomotionPresentationSignal_InputReleaseDuringCooldown_DropsWalkLoop`: expected true, actual false.
  - `PlayerControl_LocomotionPresentationSignal_StaysTrueDuringCooldownGapAndDropsWhenBlocked`: expected true, actual false.
  - `PlayerControl_MoveCooldown_CannotBeBypassedByTapSpam`: expected true, actual false.
  - `PlayerControl_MoveCooldown_OneTick_BlocksImmediateNextTick`: expected true, actual false.
  - `PlayerControl_MoveOccupancy_BlocksFlipStartUntilFirstUnlockedTick`: expected true, actual false.
- Historical removed player same-face locomotion tests (3):
  - flag-off baseline contact timing: contact timing debug expectation mismatch.
  - flag-off discrete fallback expectation: expected `Floor(1,0)`, actual `Floor(0,0)`.
  - flag-on box block expectation: expected true, actual false.
- `RuntimeBoardBoundsGuardScenarioTests` (1):
  - `GameplaySceneHost_Initialize_WithoutPlayerPrefabAuthoritativeSource_UsesDefaultPlayerControlTiming`: expected one `MoveCommitted` event, actual empty collection.
- `RuntimeBoardBoundsGuardTests` (2):
  - `GameplaySceneHost_AutoCreateViewsFalse_UsesConfiguredPlayerControlTiming`: expected true, actual false.
  - `GameplaySceneHost_Initialize_UsesConfiguredPlayerControlTiming_WhenPlayerPrefabHasAnimationTimingAuthoringOnly`: expected true, actual false.
- `SpatialStateResolverTruthTableTests` (2):
  - `PhasedWritePath_And_FactoryReferences_AreConstrainedToAllowlistedFiles`: allowlisted file set mismatch.
  - `ReservedSpatialStates_AreConstrainedToAllowlistedFiles_AndKeepAttachedProducerClosed`: reserved-state read allowlist mismatch.
- `StageAuthoringExitGoalHelperCommandTests` (10):
  - `ObjectiveHelper_CreatePrimaryGoal_CreatesConditionAssetAtExpectedPath`: owner `StageContentEntry` must live under the Campaign level stage root before creating Exit objective assets.
  - `ObjectiveHelper_CreatePrimaryGoal_DoesNotOverwriteExistingAsset`: expected existing-asset message, actual Campaign owner path validation message.
  - `ObjectiveHelper_CreatePrimaryGoal_ReusesExpectedPathAsset`: owner `StageContentEntry` must live under the Campaign level stage root before creating Exit objective assets.
  - `ObjectiveHelper_CreatePrimaryGoal_SetsZoneId`: owner `StageContentEntry` must live under the Campaign level stage root before creating Exit objective assets.
  - `ObjectiveHelper_CreatePrimaryGoal_UsesPlayerAtAnyZone`: owner `StageContentEntry` must live under the Campaign level stage root before creating Exit objective assets.
  - `ObjectiveHelper_DoesNotTouchStagePresentationDefinition`: owner `StageContentEntry` must live under the Campaign level stage root before creating Exit objective assets.
  - `ObjectiveHelper_GridWindowCreateFlow_IsAvailableForTests`: owner `StageContentEntry` must live under the Campaign level stage root before creating Exit objective assets.
  - `ObjectiveHelper_PreservesTileFeatureVisualBindings`: owner `StageContentEntry` must live under the Campaign level stage root before creating Exit objective assets.
  - `ObjectiveHelper_RejectsSharedConditionAsset`: expected shared-condition rejection, actual Campaign owner path validation message.
  - `ObjectiveHelper_RepairExitContract_GeneratedStageDefinitionValidates`: owner `StageContentEntry` must live under the Campaign level stage root before creating Exit objective assets.
- `StageObjectiveSystemTests` (3):
  - `Conditions_ClearWithinTimeLimit_UsesCeilDeadlineFromTiming(0.5f,10.0f,20,21)`: time-limit-only objective policy rejected.
  - `Conditions_ClearWithinTimeLimit_UsesCeilDeadlineFromTiming(0.25f,10.0f,40,41)`: time-limit-only objective policy rejected.
  - `Conditions_ClearWithinTimeLimit_UsesCeilDeadlineFromTiming(0.3f,10.0f,34,35)`: time-limit-only objective policy rejected.
- `StageRuntimeBuilderTests` (1):
  - `StageRuntimeBuilder_CombinedShowcaseStageBuild_ReflectsCurrentConfiguredContract`: expected 12 box spawns, actual 13.
- `TickPipelineStructureCoreTests` (4):
  - `GameplayCompositionRoot_AndBootstrapper_ExposeExplicitGeneralAndPlayerTimingOverloads`: expected non-null method, actual null.
  - `MovementCommitter_ConsumesCanonicalPlayerControlTimingSnapshot`: expected true, actual false.
  - `TickPipeline_CanBeExtendedWithInjectedEntityLogicProvider`: expected non-null method, actual null.
  - `TickPipeline_DoesNotExposeDefaultCompositionConstructors`: constructor type set mismatch.
- `TickReplayDeterminismTests` (8):
  - `DeterminismHash_BoxInteractionLockState_IsIncludedInCanonicalState`: expected box interaction lock trace, actual trace omitted expected lock entry.
  - `DeterminismHash_EnemyJumpState_IsIncludedInCanonicalState`: invalid spatial state source combination for airborne occupying entity.
  - `Replay_EnemyUtilityLockScenario_ProducesStablePerTickHashTraceAndBlockedPush`: expected box interaction lock trace, actual trace omitted expected lock entry.
  - `Replay_PassiveContactScenario_ProducesStableHashTraceAndPlayerDamage`: expected player damage dump, actual empty.
  - `Replay_PlayerMoveIntoUnitStackedScenario_ProducesSameHashTraceAndEventLog`: expected semantic move event, actual false.
  - `Replay_PushBoxBoardEdgeScenario_ProducesSameHashTraceAndEventLog`: expected pushed box at `(2,0)`, actual remained at `(1,0)`.
  - `Replay_PushBoxEntityStopperScenario_ProducesSameHashTraceAndEventLog`: expected push action control dump, actual action `None`.
  - `Replay_PushBoxTerrainStopperScenario_ProducesSameHashTraceAndEventLog`: expected pushed box at `(2,0)`, actual remained at `(1,0)`.
- `WorldSnapshotAndPresentationTests` (6):
  - `TickPresentationDataBuilder_BuildsEnemyJumpSignal_ForAirborneStartAndRetry`: expected true, actual false.
  - `TickPresentationDataBuilder_BuildsEnemyJumpSignal_ForWindupStart`: expected true, actual false.
  - `TickPresentationDataBuilder_FlipDestroySelf_SeparatesLogicalNoMoveFromFlipImpactSignal`: expected 1, actual 0.
  - `TickPresentationDataBuilder_GravityFieldVisualStates_ActiveIncludesDeterministicThreeByThreeArea`: expected 9 cells, actual empty.
  - `TickPresentationDataBuilder_GravityFieldVisualStates_BoundedEdgeExcludesOutOfBoundsArea`: expected 4 cells, actual empty.
  - `TickPresentationDataBuilder_UsesPostMovementSnapshotForFollowThroughMotion_AndPostAttackSnapshotForEnemyDeath`: debug spawn entity 40 not representable at `Floor(2,0)`.
- `WorldStatePlacementInvariantTests` (1):
  - `CreateSnapshot_DetachedBoxSharingUnitCell_RemainsMaterializableAndDoesNotBlockOccupancy`: debug spawn box entity 20 not representable at unit-occupied `Floor(0,0)`.

## Remaining Issues
- No campaign governance errors remain.
- No old stage support/content root assets remain.
- No Level `_Shared`, Stage `_Shared`, or Stage `_Overrides` folders remain.
- Full EditMode still has 95 unrelated known baseline failures listed in `TestResults/campaign-phase-1-1/full-editmode-final.xml` and mirrored by `Assets/_Features/Stages/Editor/Validation/Baselines/FullEditModeKnownFailures.json`.
