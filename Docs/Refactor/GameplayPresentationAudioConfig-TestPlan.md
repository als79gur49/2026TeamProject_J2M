# GameplayPresentationAudioConfig Test Plan

Date: 2026-06-07

## Implementation Update

Decision: IMPLEMENTED_IN_PR

The implementation PR adds focused config validation tests, repository smoke
coverage for `GameplayPresentationAudioConfig_CampaignV1.asset`, UI scene
contract coverage for the grouped serialized reference, and architecture guards
for no generic dispatcher and no ownership drift.

Required validation evidence remains:

- `git diff --check`
- guardrail scans
- `./run_tests.sh core`
- `./run_tests.sh ui`

Do not claim full/broad regression green unless that lane actually runs and
passes on the same revision.

## Existing Tests

Decision: TEST_UPDATE_REQUIRED

| Test | Lane | Keep / Update | Notes |
|---|---|---|---|
| `AudioRepositoryAssetSmokeCoreTests` | core | Keep | Continue validating `GameplayAudioMap`, action profiles, BGM profiles, and `StageAudioDefinition`. |
| `AudioLaneMapRepositorySmokeTests` | core | Update | Add `GameplayPresentationAudioConfig` repository validation once the asset exists. Existing lane map/profile validation remains. |
| `GameplayShellUiAudioContractTests` | ui | Update | Replace direct `gameplayAudioMap` scene field assertion with config asset assertion. Verify config points to canonical map GUIDs/paths. |
| `GameplayShellUiAudioContractTests.UiAudioScene_UsesCoLocatedAudioRuntimeInstaller_OnCanonicalBootstrapRoot` | ui | Keep | Same-root `AudioRuntimeInstaller` remains required. |
| `AudioArchitectureTests.GameplayPresentationAudioConfig_IsDeferred_AndNoHostConfigAudioFieldSprawlWasAdded` | core/extended | Replace | Convert from deferred guard to positive config-boundary guard. |
| `AudioArchitectureTests.SharedAudioAssembly_DoesNotReferenceGameplayOrUiMappedSeamAssemblies` | core/extended | Keep | Ensures config does not move into shared runtime. |
| `BgmFlowArchitectureTests.GameplaySceneHostConfiguration_DoesNotExposeBgmFlowOwnershipFields` | core/extended | Keep | Config must not introduce `BgmProfile`, `GlobalAudioFlowBootstrap`, or `Bgm` fields. |
| `BlockAudioRuntimeTests` | core/extended | Keep | Controller remains typed and receives `BlockAudioMap`. |
| `PlayerLocomotionAudioRuntimeTests` | core/extended | Keep | Controller remains typed and receives `PlayerLocomotionAudioMap`. |
| `TopologyAudioRuntimeTests` | core/extended | Keep | Controller remains typed and receives `TopologyAudioMap`. |
| `GravityFieldAudioRuntimeTests` | core/extended | Keep | Optional no-op behavior remains lane-owned. |
| `TileFeatureAudioRuntimeTests` | core/extended | Keep | Optional/burst no-op behavior remains lane-owned. |
| `GameplayActionAudioRuntimeTests` | core/extended | Keep | Action profile remains prefab-local and excluded. |
| `EnemyAudioRuntimeTests` | core/extended | Keep | Enemy profiles remain prefab-local and excluded. |
| `UiAudioSfxContractTests` | ui | Keep | UI SFX remains `UiAudioCueMap` owned by UI composition. |
| `UiRepositoryPrefabCatalogSmokeTests.UiAudioCueMaps_RepositoryAssets_AllCuesExplicitUiOneShot` | ui | Keep | Confirms UI cue map entries are explicit Ui one-shots with null policy. |

## New / Updated Test Candidates

Decision: TEST_UPDATE_REQUIRED

| Test | Lane | Type | Purpose |
|---|---|---|---|
| `GameplayPresentationAudioConfigValidationTests_NullLaneMap_FailsFast` | core | unit | Each of the six lane map fields is required. |
| `GameplayPresentationAudioConfigValidationTests_DelegatesToRequiredLaneValidators` | core | unit | Config calls each lane's existing required validator and preserves error messages. |
| `GameplayPresentationAudioConfigValidationTests_DoesNotRequireGravityOptionalCues` | core | unit | Empty `GravityFieldAudioCueCatalog.RequiredOneShotV1` stays optional. |
| `GameplayPresentationAudioConfigValidationTests_DoesNotRequireTileFeatureOptionalOrBurstCues` | core | unit | Only `ButtonActivated` is required for tile feature audio. |
| `GameplayPresentationAudioConfig_RepositoryAsset_ValidatesAllLaneMaps` | core | asset smoke | Load `GameplayPresentationAudioConfig_CampaignV1.asset`, call `ValidateOrThrow`, and verify all six canonical asset paths. |
| `GameplaySceneHostConfiguration_RequiresPresentationAudioConfig` | core/extended | architecture | Host config exposes grouped config instead of six individual map fields. |
| `GameplayHostRuntimeFactory_UsesPresentationAudioConfigButStillCreatesTypedControllers` | core/extended | orchestration/architecture | Factory resolves the config then calls existing typed presenter attach methods. |
| `GameplayShellUiAudioContractTests_UIAudioScene_ReferencesGameplayPresentationAudioConfig` | ui | scene contract | `UIAudioScene` serializes one config reference, not six lane map fields. |
| `AudioArchitectureTests_NoGenericAudioDispatcherIntroduced` | core/extended | source guard | Reject `GenericAudioDispatcher`, string-key dispatch, or config-owned playback/planning. |
| `AudioArchitectureTests_GameplayPresentationAudioConfig_ExcludesOtherOwnership` | core/extended | architecture | Config does not expose action profiles, enemy prefab-local profiles, enemy requirement policy/binding assets, UI cue maps, BGM/stage metadata, runtime installers, or settings bridge types. |

## Smoke Coverage Update

Decision: TEST_UPDATE_REQUIRED

Add config asset smoke beside existing lane repository smoke:

```text
Load all production GameplayPresentationAudioConfig assets
  -> Assert not empty
  -> ValidateOrThrow()
  -> Assert canonical CampaignV1 config references:
       GameplayAudioMap_CampaignV1
       BlockAudioMap_PlayerSounds
       PlayerLocomotionAudioMap_PlayerSounds
       TopologyAudioMap_ObjectSounds
       GravityFieldAudioMap_ObjectSounds
       TileFeatureAudioMap_ObjectSounds
```

Keep existing lane smoke tests because config validation should delegate, not replace lane asset coverage.

## Scene Contract Update

Decision: SCENE_MIGRATION_REQUIRED

`GameplayShellUiAudioContractTests` should update from:

```text
CombinedGameplayShowcaseInstaller.gameplayAudioMap == GameplayAudioMap_CampaignV1
```

to:

```text
CombinedGameplayShowcaseInstaller.gameplayPresentationAudioConfig == GameplayPresentationAudioConfig_CampaignV1
GameplayPresentationAudioConfig_CampaignV1.gameplayAudioMap == GameplayAudioMap_CampaignV1
GameplayPresentationAudioConfig_CampaignV1.blockAudioMap == BlockAudioMap_PlayerSounds
GameplayPresentationAudioConfig_CampaignV1.playerLocomotionAudioMap == PlayerLocomotionAudioMap_PlayerSounds
GameplayPresentationAudioConfig_CampaignV1.topologyAudioMap == TopologyAudioMap_ObjectSounds
GameplayPresentationAudioConfig_CampaignV1.gravityFieldAudioMap == GravityFieldAudioMap_ObjectSounds
GameplayPresentationAudioConfig_CampaignV1.tileFeatureAudioMap == TileFeatureAudioMap_ObjectSounds
```

The UI lane test should avoid becoming a gameplay lane behavior test. Prefer serialized property/path checks over direct controller behavior checks.

## Guardrail Scan

Decision: TEST_UPDATE_REQUIRED

Run source scans in architecture tests or manual PR evidence:

```text
GenericAudioDispatcher
AudioDispatcher
string.*Audio
audio.*string
AudioManager.Instance
FindObjectOfType<.*Audio
FindAnyObjectByType<.*Audio
new AudioManager
.PlayBgm(
```

Expected interpretation:

- Shared runtime tests may call `AudioManager.PlayBgm` directly.
- Flow_Audio may call BGM APIs.
- Gameplay host SFX controllers and config must not call `PlayBgm`, use string dispatch, or access `AudioManager.Instance`.

## Validation Lanes

Decision: TEST_UPDATE_REQUIRED

For implementation PR:

- Run `./run_tests.sh core` because Host, lane validation, and architecture tests are touched.
- Run `./run_tests.sh ui` because `UIAudioScene` serialized fields and UI scene contract tests are touched.
- Do not claim full/project-wide green unless a full lane actually runs and passes on the same revision.
