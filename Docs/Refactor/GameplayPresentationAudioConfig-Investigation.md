# GameplayPresentationAudioConfig Investigation

Date: 2026-06-07

## Implementation Update

Decision: IMPLEMENTED_IN_PR

`GameplayPresentationAudioConfig` now groups the six typed gameplay host
presentation SFX maps as data + validation only. It is not a dispatcher,
planner, controller factory, service locator, or playback owner.

`GameplaySceneHostConfiguration` and `GameplayShowcaseSceneInstallerBase` now use
one grouped config reference instead of six direct host presentation SFX map
fields. `GameplayHostRuntimeFactory` validates the config and still passes typed
maps into the existing lane-specific presenter attach methods.

Action/enemy prefab-local profiles, UI cue maps, BGM profiles,
`StageAudioDefinition`, runtime installers, settings bridges, and UI channel
mappers remain outside this config.

## Baseline Scan

Decision: TEST_UPDATE_REQUIRED

Current worktree baseline:

- `git status --short --branch`: `## worktree/ui-audio...origin/worktree/ui-audio [ahead 3]`; untracked `Docs/Refactor/SfxLane-MaintainabilityReport.md`.
- `git diff --stat`: no tracked diff before this investigation.
- `git diff --check`: no whitespace errors.
- Removed BGM string-key path check found no `StagePresentationDefinition.BgmReference`, `StageBgmProfileCatalog`, `StageBgmReference`, `KnownBgmKeys`, or `TryValidateBgmKey`.
- Production `_Test` audio path check found `_Test` tokens only in historical docs/refactor reports, not active production asset paths.
- `AudioLaneMapRepositorySmokeTests` exists and covers block, locomotion, topology, gravity field, tile feature, and enemy audio profile assets.
- UI audio repository smoke checks `AudioBinding.Policy == null` in `UiRepositoryPrefabCatalogSmokeTests`; `UiAudioSfxContractTests` also rejects non-null `AudioBinding.Policy`.

BASELINE_MISMATCH: none for active code/assets. Historical docs still mention old `_Test` assets and should not be interpreted as active production paths.

## Current Host Audio Field Inventory

Decision tags: INCLUDE_IN_CONFIG, EXCLUDE_OTHER_OWNERSHIP, SCENE_MIGRATION_REQUIRED, TEST_UPDATE_REQUIRED

`GameplaySceneHostConfiguration` currently exposes six audio map fields directly. `GameplayShowcaseSceneInstallerBase` mirrors the same six serialized scene fields and copies them into the runtime configuration.

| Field | Field Type | Asset Path | GUID | Referenced By | Used By Factory | Controller Consumer | Decision |
|---|---|---|---|---|---|---|---|
| `GameplayAudioMap` | `GameplayAudioMap` | `Assets/_Features/Gameplay/Gameplay_Audio/Maps/GameplayAudioMap_CampaignV1.asset` | `2e17653afa1ba264a950b76bcd5ccc56` | `Assets/Scenes/UIAudioScene.unity`; path-pinned gameplay/UI tests | `AttachAudioRuntimesIfConfigured` checks `configuration.GameplayAudioMap` and calls `presenter.AttachGameplayAudioRuntime` | `GameplayAudioPresentationController`; also opens playback port for action/enemy prefab-local audio controllers | INCLUDE_IN_CONFIG, SCENE_MIGRATION_REQUIRED, TEST_UPDATE_REQUIRED |
| `TileFeatureAudioMap` | `TileFeatureAudioMap` | `Assets/_Features/Gameplay/Gameplay_TileFeatureAudio/Maps/TileFeatureAudioMap_ObjectSounds.asset` | `b4f6650dfd7e4aecb59dcad3cabe0489` | `Assets/Scenes/UIAudioScene.unity` | `presenter.AttachTileFeatureAudioRuntime` | `TileFeatureAudioPresentationController` | INCLUDE_IN_CONFIG, SCENE_MIGRATION_REQUIRED, TEST_UPDATE_REQUIRED |
| `TopologyAudioMap` | `TopologyAudioMap` | `Assets/_Features/Gameplay/Gameplay_TopologyAudio/Maps/TopologyAudioMap_ObjectSounds.asset` | `66d7af338d3f4e75b4f5c15d544fd731` | `Assets/Scenes/UIAudioScene.unity` | `presenter.AttachTopologyAudioRuntime` | `TopologyAudioPresentationController` | INCLUDE_IN_CONFIG, SCENE_MIGRATION_REQUIRED, TEST_UPDATE_REQUIRED |
| `GravityFieldAudioMap` | `GravityFieldAudioMap` | `Assets/_Features/Gameplay/Gameplay_GravityFieldAudio/Maps/GravityFieldAudioMap_ObjectSounds.asset` | `20311d81a3644b22b830a914b49455d5` | `Assets/Scenes/UIAudioScene.unity` | `presenter.AttachGravityFieldAudioRuntime` | `GravityFieldAudioPresentationController` | INCLUDE_IN_CONFIG, SCENE_MIGRATION_REQUIRED, TEST_UPDATE_REQUIRED |
| `BlockAudioMap` | `BlockAudioMap` | `Assets/_Features/Gameplay/Gameplay_BlockAudio/Maps/BlockAudioMap_PlayerSounds.asset` | `5a6bb3f9bcde4e6ca7487767ab9ba305` | `Assets/Scenes/UIAudioScene.unity` | `presenter.AttachBlockAudioRuntime` | `BlockAudioPresentationController` | INCLUDE_IN_CONFIG, SCENE_MIGRATION_REQUIRED, TEST_UPDATE_REQUIRED |
| `PlayerLocomotionAudioMap` | `PlayerLocomotionAudioMap` | `Assets/_Features/Gameplay/Gameplay_PlayerLocomotionAudio/Maps/PlayerLocomotionAudioMap_PlayerSounds.asset` | `6bf2bb925f794fe98b6b8e5a406e1f3d` | `Assets/Scenes/UIAudioScene.unity` | `presenter.AttachPlayerLocomotionAudioRuntime` | `PlayerLocomotionAudioPresentationController` | INCLUDE_IN_CONFIG, SCENE_MIGRATION_REQUIRED, TEST_UPDATE_REQUIRED |
| `AudioRuntimeInstaller` | Scene component, not a `GameplaySceneHostConfiguration` field | same root as `CombinedGameplayShowcaseInstaller` in `UIAudioScene`; same root as main menu UI installer in `MainMenuScene` | script GUID `5eec5bd69526f65458aba6ce130324f1` | `UIAudioScene`, `MainMenuScene` | Required by factory when any host audio map is assigned; provides `IAudioService` and settings services | Shared runtime bootstrap, not a lane controller | EXCLUDE_OTHER_OWNERSHIP |
| `StageAudioRuntimeRequestSource` / BGM | Runtime helper field in `StageBackedGameplayShowcaseInstallerBase`, not serialized in host config | stage audio companions under `Assets/_Features/Stages/Content/.../*_Audio.asset` | per stage asset | `StageContentEntry.AudioDefinition` | stage-backed installer resolves `StageAudioDefinition` and submits through `BgmRequestRouter` | `BgmFlowCoordinator`, not gameplay SFX controllers | EXCLUDE_OTHER_OWNERSHIP |
| `UiAudioCueMap` | UI installer field, not host config | `Assets/_Features/UI/UI_Composition/Authoring/UiAudioCueMap_V1.asset` | `f22f968bc51249c19c72dc545d349f23` | `UIAudioScene`, `MainMenuScene` | not consumed by `GameplayHostRuntimeFactory` | `UiAudioPortAdapter` / UI composition | EXCLUDE_OTHER_OWNERSHIP |

## Host Factory Consumption Graph

Decision: INCLUDE_IN_CONFIG, REQUIRES_ASMDEF_REVIEW

Current graph:

```text
GameplayShowcaseSceneInstallerBase serialized fields
  -> GameplaySceneHostConfiguration
  -> GameplayHostRuntimeFactory.Create(...)
  -> AttachAudioRuntimesIfConfigured(...)
  -> AudioRuntimeInstaller.Install()
  -> GameplayAudioPlaybackPortAdapter
  -> GameplayTickViewPresenter.Attach*AudioRuntime(...)
  -> GameplayTickPresentationCoordinator.Attach*AudioRuntime(...)
  -> lane-specific presentation controller
  -> lane-specific map/profile
  -> AudioBinding
  -> IGameplayAudioPlaybackPort
```

Current typed attach edges:

```text
GameplayAudioMap
  -> GameplayAudioPresentationController
  -> GameplayAudioSemanticCatalog.RequiredOneShotV1

GameplayAudioMap also opens the playback runtime for:
  -> GameplayActionAudioPresentationController
  -> EnemyAudioPresentationController
  -> EnemyChargeLoopAudioPresentationController
These controllers still read prefab-local authoring/profile data from owner views.

BlockAudioMap
  -> BlockAudioPresentationController
  -> BlockAudioCueCatalog.RequiredOneShotV1

PlayerLocomotionAudioMap
  -> PlayerLocomotionAudioPresentationController
  -> PlayerLocomotionAudioCueCatalog.RequiredOneShotV1

TopologyAudioMap
  -> TopologyAudioPresentationController
  -> TopologyAudioCueCatalog.RequiredOneShotV1

GravityFieldAudioMap
  -> GravityFieldAudioPresentationController
  -> GravityFieldAudioCueCatalog.RequiredOneShotV1

TileFeatureAudioMap
  -> TileFeatureAudioPresentationController
  -> TileFeatureAudioCueCatalog.RequiredOneShotV1
```

Config introduction must preserve:

- Controllers receive typed maps directly after factory/config resolution.
- `GameplayPresentationAudioConfig` owns references and validation only.
- `GameplayPresentationAudioConfig` must not create controllers, plan requests, dispatch by string, or own playback.
- No generic audio dispatcher should be introduced.

## Include / Exclude Lane Decisions

| Candidate | Include? | Reason | Ownership | Serialized Today | Migration Impact |
|---|---:|---|---|---|---|
| `GameplayAudioMap` | Yes | Host presentation one-shot SFX map; currently a direct host config field. | Gameplay host presentation SFX | `UIAudioScene` `gameplayAudioMap` | Move scene field into config asset. |
| `BlockAudioMap` | Yes | Host presentation block lane map; typed controller already exists. | Gameplay host presentation SFX | `UIAudioScene` `blockAudioMap` | Move scene field into config asset. |
| `PlayerLocomotionAudioMap` | Yes | Host presentation locomotion lane map; typed controller already exists. | Gameplay host presentation SFX | `UIAudioScene` `playerLocomotionAudioMap` | Move scene field into config asset. |
| `TopologyAudioMap` | Yes | Host presentation topology lane map; typed controller already exists. | Gameplay host presentation SFX | `UIAudioScene` `topologyAudioMap` | Move scene field into config asset. |
| `GravityFieldAudioMap` | Yes | Host presentation gravity field lane map; optional cue policy stays lane-owned. | Gameplay host presentation SFX | `UIAudioScene` `gravityFieldAudioMap` | Move scene field into config asset. |
| `TileFeatureAudioMap` | Yes | Host presentation tile feature lane map; optional/burst cues stay lane-owned. | Gameplay host presentation SFX | `UIAudioScene` `tileFeatureAudioMap` | Move scene field into config asset. |
| `GameplayActionAudioProfile` | No | Player prefab-local authoring profile. Global config would violate authoring ownership. | Player prefab-local presentation | `Player_S1.prefab` via `GameplayActionAudioAuthoring` | None; keep prefab-local. EXCLUDE_PREFAB_LOCAL |
| `EnemyAudioProfile` | No | Enemy prefab-local authoring profile. | Enemy prefab-local presentation | Enemy view prefabs via `EnemyAudioAuthoring` | None; keep prefab-local. EXCLUDE_PREFAB_LOCAL |
| `UiAudioCueMap` | No | UI_Composition owns UI SFX cue vocabulary and port adapter. | UI composition | `UIAudioScene`, `MainMenuScene` UI installers | None for gameplay config. EXCLUDE_OTHER_OWNERSHIP |
| `BgmProfile` | No | Shared BGM data used by Flow_Audio and StageAudioDefinition. | Flow_Audio / Stage audio metadata | scene BGM requester and stage audio companions | None for gameplay SFX config. EXCLUDE_OTHER_OWNERSHIP |
| `StageAudioDefinition` | No | Stage content companion owns gameplay BGM metadata only. | Stage content / Flow_Audio request path | `StageContentEntry.AudioDefinition` | None for gameplay SFX config. EXCLUDE_OTHER_OWNERSHIP |
| `AudioRuntimeInstaller` | No | Bootstrap seam and service installer, not semantic lane data. | Shared audio runtime bootstrap | `UIAudioScene`, `MainMenuScene` | Keep same-root requirement. EXCLUDE_OTHER_OWNERSHIP |
| `AudioSettingsPortAdapter` | No | UI settings bridge. | UI composition/settings | constructed by UI installers | None. EXCLUDE_OTHER_OWNERSHIP |
| `UIAudioChannelMapper` | No | UI-visible channel to shared runtime mapping. | UI composition/settings | code only | None. EXCLUDE_OTHER_OWNERSHIP |

## Assembly Dependency Analysis

Decision: REQUIRES_ASMDEF_REVIEW

Current assembly shape:

- `Game.Feature.Gameplay.Host` already references `Game.Feature.Gameplay.Audio`, `BlockAudio`, `PlayerLocomotionAudio`, `TopologyAudio`, `GravityFieldAudio`, `TileFeatureAudio`, `ActionAudio`, `EnemyAudio`, `Game.Shared.Audio`, and `Game.Feature.Flow.Audio`.
- Each included lane assembly references only `Game.Feature.Gameplay` and `Game.Shared.Audio`.
- `Game.Shared.Audio` has no gameplay/UI references.
- `Game.Feature.UI.Composition` currently references `Game.Feature.Gameplay.Host`, but UI audio ownership remains in UI composition and should not consume the new config.
- `Game.Core.Tests` and `Game.Feature.Gameplay.Tests` already reference all included lane assemblies and Host.
- `Game.Feature.UI.Tests` references Host but not the individual gameplay lane assemblies.

Recommended script location:

```text
Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPresentationAudioConfig.cs
```

Recommended asset location:

```text
Assets/_Features/Gameplay/Gameplay_Host/Authoring/GameplayPresentationAudioConfig_CampaignV1.asset
```

Rationale:

- Placing the config type in `Gameplay_Host` adds no new lane assembly references because Host already owns those dependencies.
- A separate `Gameplay_PresentationAudioConfig` assembly is possible but unnecessary unless the team wants to reduce Host source size. It would need one-way references to all six lane map assemblies, and Host would need to reference it.
- Do not place this type in `Game.Shared.Audio`; that would force shared runtime to know gameplay lanes.
- Do not place this type in gameplay core assembly; it is host presentation authoring, not authoritative simulation.
- Do not require UI assemblies to understand lane map types. UI scene contract tests can inspect serialized references/path without owning lane behavior.

## Scene / Prefab Serialized Reference Impact

Decision: SCENE_MIGRATION_REQUIRED

Scene inventory:

- `Assets/Scenes/UIAudioScene.unity` exists and serializes `CombinedGameplayShowcaseInstaller` on `UIAudioSceneBootstrapRoot`.
- `Assets/Scenes/MainMenuScene.unity` exists and has no `GameplaySceneHostConfiguration` audio map fields.
- `Assets/Scenes/CombinedGameplayShowcase.unity` does not exist in this worktree.
- `Assets/Scenes/TutorialScene.unity` does not exist in this worktree.

`UIAudioScene` currently serializes these fields directly on `CombinedGameplayShowcaseInstaller`:

| Scene Field | GUID |
|---|---|
| `gameplayAudioMap` | `2e17653afa1ba264a950b76bcd5ccc56` |
| `tileFeatureAudioMap` | `b4f6650dfd7e4aecb59dcad3cabe0489` |
| `topologyAudioMap` | `66d7af338d3f4e75b4f5c15d544fd731` |
| `gravityFieldAudioMap` | `20311d81a3644b22b830a914b49455d5` |
| `blockAudioMap` | `5a6bb3f9bcde4e6ca7487767ab9ba305` |
| `playerLocomotionAudioMap` | `6bf2bb925f794fe98b6b8e5a406e1f3d` |

Config asset migration should remove those six scene fields and replace them with one serialized `GameplayPresentationAudioConfig` reference on the showcase installer/configuration path.

Recommended authoring scope: campaign-wide config, not scene-specific config. The same production maps are campaign-level SFX lane maps, and scene count is not 1:1 with stage content. Stage content already owns stage-specific gameplay data and BGM companions separately.

Prefab-local exclusions:

- `Player_S1.prefab` references `Player_S1_GameplayActionAudioProfile.asset` by GUID `42a2e109fc5141ec9e866925a0a85c3b`; do not move into config.
- Enemy prefabs reference `EnemyAudioProfile_*` assets through `EnemyAudioAuthoring`; do not move into config.

## Validation Contract Proposal

Decision: INCLUDE_IN_CONFIG, TEST_UPDATE_REQUIRED

Expected `GameplayPresentationAudioConfig.ValidateOrThrow()` behavior:

- Null config asset on host should fail fast once the host migrates to required grouped config.
- Null `GameplayAudioMap` should fail fast.
- Null `BlockAudioMap` should fail fast.
- Null `PlayerLocomotionAudioMap` should fail fast.
- Null `TopologyAudioMap` should fail fast.
- Null `GravityFieldAudioMap` should fail fast.
- Null `TileFeatureAudioMap` should fail fast.
- Call existing validators directly:
  - `GameplayAudioMap.ValidateRequiredSemanticsOrThrow(GameplayAudioSemanticCatalog.RequiredOneShotV1)`
  - `BlockAudioMap.ValidateRequiredCuesOrThrow(BlockAudioCueCatalog.RequiredOneShotV1)`
  - `PlayerLocomotionAudioMap.ValidateRequiredCuesOrThrow(PlayerLocomotionAudioCueCatalog.RequiredOneShotV1)`
  - `TopologyAudioMap.ValidateRequiredCuesOrThrow(TopologyAudioCueCatalog.RequiredOneShotV1)`
  - `GravityFieldAudioMap.ValidateRequiredCuesOrThrow(GravityFieldAudioCueCatalog.RequiredOneShotV1)`
  - `TileFeatureAudioMap.ValidateRequiredCuesOrThrow(TileFeatureAudioCueCatalog.RequiredOneShotV1)`
- Preserve existing optional cue policy.
- Preserve `AudioBinding.Policy == null` reservation through existing `AudioBindingDiagnostics`.
- Preserve `AudioCategory.Master` reserved-category rejection through existing `AudioDefinitionCategoryRules`.
- Do not validate UI, BGM, action profile, enemy profile, runtime installer, or settings bridge through this config.

## Optional Cue Policy Impact

Decision: INCLUDE_IN_CONFIG, EXCLUDE_PREFAB_LOCAL

| Lane | Required Contract | Optional Contract | Config Impact |
|---|---|---|---|
| Gravity field | `GravityFieldAudioCueCatalog.RequiredOneShotV1` is empty; validator currently only calls `ValidateOrThrow`. | `Activated`, `Expired`, `LockedBox` are optional via `TryResolveOptional`; missing binding no-ops. | Config must reuse lane validator and must not require all enum cues. |
| Tile feature | Required set is only `ButtonActivated`. | Destroy/slide/barricade/exit/moon-block/burst cues are optional; reason-specific moon-block bindings are optional but validated when authored. | Config must require only `ButtonActivated` plus authored binding validity. |
| Gameplay action | Profile allows empty/partial profiles and explicit optional null entries. | Missing `GameplayActionAudioAuthoring` and missing optional entries are no-op. | Excluded, no config impact. |
| Enemy audio | Profile validates authored entries; missing optional cue can no-op. `ChargeActiveLoop` has special loop/attachment policy when authored. | Prefab-local runtime authoring. | Excluded, no config impact. |

## Test Impact

Decision: TEST_UPDATE_REQUIRED

Existing related tests:

| Test | Lane | Current Impact |
|---|---|---|
| `AudioRepositoryAssetSmokeCoreTests` | core | Keep; still validates `GameplayAudioMap`, action profiles, BGM profiles, and stage audio definitions. |
| `AudioLaneMapRepositorySmokeTests` | core | Keep; add config asset repository validation if config asset is created. |
| `GameplayShellUiAudioContractTests` | ui | Update scene contract from six direct fields to one config reference, while still verifying the config resolves to canonical maps. |
| `AudioArchitectureTests` | core/extended | Replace deferred config guard with positive config boundary guard; add no dispatcher/no ownership drift checks. |
| `BgmFlowArchitectureTests` | core/extended | Keep; ensure Host config still exposes no BGM fields. |
| `BlockAudioRuntimeTests` | core/extended | Keep; controller still typed. |
| `PlayerLocomotionAudioRuntimeTests` | core/extended | Keep; controller still typed. |
| `TopologyAudioRuntimeTests` | core/extended | Keep; controller still typed. |
| `GravityFieldAudioRuntimeTests` | core/extended | Keep optional no-op behavior. |
| `TileFeatureAudioRuntimeTests` | core/extended | Keep optional/burst no-op behavior. |
| `GameplayActionAudioRuntimeTests` | core/extended | Keep; ensure prefab-local exclusion. |
| `EnemyAudioRuntimeTests` | core/extended | Keep; ensure prefab-local exclusion. |
| `UiAudioSfxContractTests` | ui | Keep; UI cue map remains UI-owned. |

Recommended new/updated tests:

| Test | Lane | Purpose |
|---|---|---|
| `GameplayPresentationAudioConfigValidationTests` | core | Null config/map fail-fast and validator delegation. |
| `GameplayPresentationAudioConfig_RepositoryAsset_ValidatesAllLaneMaps` | core | Load `GameplayPresentationAudioConfig_CampaignV1.asset` and call `ValidateOrThrow`. |
| `GameplaySceneHostConfiguration_RequiresPresentationAudioConfig` | core/extended | Host config no longer exposes six individual map fields and requires grouped config. |
| `GameplayHostRuntimeFactory_UsesPresentationAudioConfigButStillCreatesTypedControllers` | core/extended | Factory resolves config and calls typed presenter attach paths; no generic dispatcher. |
| `GameplayShellUiAudioContractTests_UIAudioScene_ReferencesGameplayPresentationAudioConfig` | ui | `UIAudioScene` references one config asset and the config points to canonical GUIDs. |
| `AudioArchitectureTests_NoGenericAudioDispatcherIntroduced` | core/extended | Source/type scan for `GenericAudioDispatcher`, string-key audio dispatch, and config runtime ownership drift. |

## Migration Risk

Decision: SCENE_MIGRATION_REQUIRED, REQUIRES_ASMDEF_REVIEW, TEST_UPDATE_REQUIRED

| Risk | Cause | Impact | Mitigation |
|---|---|---|---|
| Scene YAML missing reference | `GameplayShowcaseSceneInstallerBase` serialized field shape changes. | Runtime scene broken. | Unity migration plus UI scene contract tests. |
| asmdef cycle | Config references lane assemblies from the wrong assembly. | Compile fail. | Put config in Host or a one-way config assembly; never in shared runtime/core. |
| Generic dispatcher drift | Config starts planning/dispatching playback. | Architecture boundary collapse. | Config is data + validation only; controllers remain typed. |
| Prefab-local ownership intrusion | Action/enemy profiles moved into global config. | Authoring model collapse. | Exclude action/enemy profiles. |
| Optional cue fail-fast drift | Config treats every enum cue as required. | Content behavior changes. | Reuse lane validators and required catalogs only. |
| Path-pinned tests fail | New config path and removed scene fields. | Test red. | Update tests in same PR as migration. |
| AudioRuntimeInstaller requirement weakened | Config hides whether maps are assigned. | Runtime can miss audio services. | Factory should require same-root installer when config has any lane map, and config should require all maps. |

## Recommended Implementation Plan

Decision: TEST_UPDATE_REQUIRED

1. Add `GameplayPresentationAudioConfig` in Host runtime with six typed map references and `ValidateOrThrow`; add focused validation tests.
2. Add `GameplayPresentationAudioConfig_CampaignV1.asset` with the six existing production map GUIDs.
3. Replace six serialized fields in `GameplayShowcaseSceneInstallerBase` and `GameplaySceneHostConfiguration` with one config field; expose read-only/typed accessors or factory-local local variables as needed.
4. Update `GameplayHostRuntimeFactory.AttachAudioRuntimesIfConfigured` to validate config and pass typed maps to existing typed presenter attach methods.
5. Migrate `UIAudioScene` from six direct map fields to one config asset reference.
6. Update scene contract, architecture, and repository smoke tests.
7. Run `./run_tests.sh core`; also run `./run_tests.sh ui` because `UIAudioScene` and UI scene contracts are touched.

Suggested commit split:

1. `feat: Gameplay/Audio - presentation audio config 추가`
2. `refactor: Gameplay/Host - host audio map fields grouped config로 이관`
3. `test: Gameplay/Audio - config migration guard 검증 추가`

## Guardrails To Preserve

- Audio remains presentation-only.
- `WorldState`, `TickPipeline`, committers, and entity logic do not play audio directly.
- Shared audio runtime must not know gameplay/UI lane semantics.
- Host SFX, action audio, enemy audio, UI SFX, and BGM remain separate ownership lanes.
- Semantic meaning stays in typed lane maps/profiles, not `AudioDefinition`.
- `GameplayPresentationAudioConfig` is not a dispatcher, planner, controller factory, service locator, or playback owner.
