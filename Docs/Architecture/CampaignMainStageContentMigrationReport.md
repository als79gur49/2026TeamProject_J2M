# Campaign Main Stage Content Migration Report

Generated: 2026-05-09

## Summary
- Policy applied: "초기에는 전부 Campaign에 넣고, 이후 필요해지면 Level/Stage로 내린다."
- Campaign root: `Assets/_Features/Stages/Content/Campaigns/campaign-main`
- Current migrated campaign asset count: 206 non-meta assets.
- Stage companion folders migrated: 11 catalog stage folders.
- Campaign `_Shared` migrated support asset count: 123 non-meta assets.
- Level/stage `_Shared` and `_Overrides`: not created.

## Moved Assets
- Catalog assets moved to `Campaign/Catalog` and renamed:
  - `CampaignMain_StageCatalog.asset`
  - `CampaignMain_StageCatalogProvider.asset`
  - `CampaignMain_StageIdAliasTable.asset`
  - `CampaignMain_StageSequence.asset`
- Stage companion folders moved to `Levels/level-01/Stages`:
  - `combined-gameplay-showcase`
  - `stage-0-1`
  - `stage-1-1`
  - `stage-2-1`
  - `stage-2-2`
  - `stage-3-1`
  - `stage-3-2`
  - `stage-4-1`
  - `stage-4-2`
  - `stage-5-1`
  - `tutorial-scene`
- Stage condition assets moved to `_Shared/Gameplay/Conditions`.
- Enemy AI profiles/core/brain/capabilities/catalogs moved to `_Shared/Gameplay/EnemyAI`.
- Enemy/static/board/tile/topology/VFX presentation support moved to `_Shared/Presentation`.
- Catalog-referenced enemy view prefabs previously under `Gameplay_Entities/Runtime` moved to `_Shared/Presentation/Enemy/Prefabs`.

## Legacy Folders
- Deleted/emptied legacy support roots:
  - `Assets/_Features/Stages/Stage_CombinedGameplayShowcase`
  - `Assets/_Features/Stages/Stage_TutorialScene`
- Deleted/emptied loose content roots:
  - `Assets/_Features/Stages/Content/combined-gameplay-showcase`
  - `Assets/_Features/Stages/Content/tutorial-scene`
  - `Assets/_Features/Stages/Content/stage-0-1`
  - `Assets/_Features/Stages/Content/stage-1-1`
  - `Assets/_Features/Stages/Content/stage-2-1`
  - `Assets/_Features/Stages/Content/stage-2-2`
  - `Assets/_Features/Stages/Content/stage-3-1`
  - `Assets/_Features/Stages/Content/stage-3-2`
  - `Assets/_Features/Stages/Content/stage-4-1`
  - `Assets/_Features/Stages/Content/stage-4-2`
  - `Assets/_Features/Stages/Content/stage-5-1`

## Variants
- No support assets were merged by value.
- Stage-named variants were kept as Campaign variants under `_Shared`, including:
  - `EnemyPresentationCatalog_CombinedGameplayShowcase.asset`
  - `EnemyPresentationCatalog_TutorialScene.asset`
  - `StaticPresentationCatalog_CombinedGameplayShowcase.asset`
  - `StaticPresentationCatalog_TutorialScene.asset`
  - `GameplayCameraTopologyPreset_CombinedGameplayShowcase.asset`
  - `GameplayCameraTopologyPreset_TutorialScene.asset`

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
- `CampaignMain_StageCatalog` now contains 11 campaign stage entries, including `tutorial-scene`.
- `StageEditorDirectPlayCatalog` maps:
  - `Assets/Scenes/CombinedGameplayShowcase.unity` -> `combined-gameplay-showcase`
  - `Assets/Scenes/TutorialScene.unity` -> `tutorial-scene`
  - `Assets/Scenes/UIAudioScene.unity` -> `tutorial-scene`
- `StageRuntimeContentResolver` smoke checked launch-context resolution for `combined-gameplay-showcase` and `tutorial-scene`.

## Addressables
- Not applicable: `com.unity.addressables` is not installed/configured, so no package dependency or groups were added.

## Verification
- `StageCampaignMainMigrationRunner.ExecuteFromCommandLine`: passed.
- `StageCampaignMainContentSmokeCheck.RunFromCommandLine`: passed.
- `StageCatalogCiValidationEntryPoint.RunFromCommandLine`: passed.
- Full EditMode via `TestRunnerCliBootstrap.RunEditMode -codexSelection full`: executed 3606 tests, 3493 passed, 112 failed, 1 skipped. The remaining failures are existing gameplay/replay/authoring baseline failures outside the campaign path governance pass.
- Requested classes passing in the full run:
  - `StageContentAndClearFlowTests`
  - `StageDefaultStageIdPolicyTests`
  - `StageSceneBootstrapValidatorTests`
  - `StageLoadSourceModeArchitectureTests`
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

## Remaining Issues
- No campaign governance errors remain.
- No old stage support/content root assets remain.
- No Level `_Shared`, Stage `_Shared`, or Stage `_Overrides` folders remain.
- Full EditMode still has unrelated gameplay/replay baseline failures listed in `TestResults/stage-migration-full-editmode-final2.xml`.
