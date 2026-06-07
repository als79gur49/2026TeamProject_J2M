# GameplayPresentationAudioConfig Migration Plan

Date: 2026-06-07

## Implementation Update

Decision: IMPLEMENTED_IN_PR

Migration landed as a host configuration refactor:

- Added `GameplayPresentationAudioConfig` under `Gameplay_Host/Runtime`.
- Added `GameplayPresentationAudioConfig_CampaignV1.asset` under
  `Gameplay_Host/Authoring`.
- Migrated `GameplaySceneHostConfiguration`,
  `GameplayShowcaseSceneInstallerBase`, and `GameplayHostRuntimeFactory` to the
  grouped config reference.
- Migrated `UIAudioScene` from six direct map fields to one
  `gameplayPresentationAudioConfig` reference.

Existing lane map assets stay in place and keep their original GUIDs.

## Recommendation

Decision: INCLUDE_IN_CONFIG, SCENE_MIGRATION_REQUIRED

Create one campaign-wide config asset:

```text
Assets/_Features/Gameplay/Gameplay_Host/Authoring/GameplayPresentationAudioConfig_CampaignV1.asset
```

Config script:

```text
Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPresentationAudioConfig.cs
```

Use one serialized host field, likely named `gameplayPresentationAudioConfig` on `GameplayShowcaseSceneInstallerBase`, copied to `GameplaySceneHostConfiguration.GameplayPresentationAudioConfig`.

## Config Asset GUID Table

Decision: INCLUDE_IN_CONFIG

| Existing Field | Existing Asset | GUID | New Config Field | Keep Asset? | Scene Field Removed? |
|---|---|---|---|---:|---:|
| `gameplayAudioMap` / `GameplayAudioMap` | `Assets/_Features/Gameplay/Gameplay_Audio/Maps/GameplayAudioMap_CampaignV1.asset` | `2e17653afa1ba264a950b76bcd5ccc56` | `gameplayAudioMap` | Yes | Yes |
| `blockAudioMap` / `BlockAudioMap` | `Assets/_Features/Gameplay/Gameplay_BlockAudio/Maps/BlockAudioMap_PlayerSounds.asset` | `5a6bb3f9bcde4e6ca7487767ab9ba305` | `blockAudioMap` | Yes | Yes |
| `playerLocomotionAudioMap` / `PlayerLocomotionAudioMap` | `Assets/_Features/Gameplay/Gameplay_PlayerLocomotionAudio/Maps/PlayerLocomotionAudioMap_PlayerSounds.asset` | `6bf2bb925f794fe98b6b8e5a406e1f3d` | `playerLocomotionAudioMap` | Yes | Yes |
| `topologyAudioMap` / `TopologyAudioMap` | `Assets/_Features/Gameplay/Gameplay_TopologyAudio/Maps/TopologyAudioMap_ObjectSounds.asset` | `66d7af338d3f4e75b4f5c15d544fd731` | `topologyAudioMap` | Yes | Yes |
| `gravityFieldAudioMap` / `GravityFieldAudioMap` | `Assets/_Features/Gameplay/Gameplay_GravityFieldAudio/Maps/GravityFieldAudioMap_ObjectSounds.asset` | `20311d81a3644b22b830a914b49455d5` | `gravityFieldAudioMap` | Yes | Yes |
| `tileFeatureAudioMap` / `TileFeatureAudioMap` | `Assets/_Features/Gameplay/Gameplay_TileFeatureAudio/Maps/TileFeatureAudioMap_ObjectSounds.asset` | `b4f6650dfd7e4aecb59dcad3cabe0489` | `tileFeatureAudioMap` | Yes | Yes |

Excluded assets stay at their current ownership points:

| Asset | GUID | Owner | Decision |
|---|---|---|---|
| `Assets/_Features/Gameplay/Gameplay_ActionAudio/Profiles/Player_S1_GameplayActionAudioProfile.asset` | `42a2e109fc5141ec9e866925a0a85c3b` | `Player_S1.prefab` via `GameplayActionAudioAuthoring` | EXCLUDE_PREFAB_LOCAL |
| `Assets/_Features/UI/UI_Composition/Authoring/UiAudioCueMap_V1.asset` | `f22f968bc51249c19c72dc545d349f23` | UI installers in `UIAudioScene` and `MainMenuScene` | EXCLUDE_OTHER_OWNERSHIP |
| `Assets/_Shared/Audio/Definitions/Bgm/HorrorVol2FactoryMain_BgmProfile.asset` | `3459b5be7e9e63b4899e1356efe8f928` | scene-default BGM request path | EXCLUDE_OTHER_OWNERSHIP |
| `Assets/_Features/Stages/Content/.../*_Audio.asset` | per stage | `StageContentEntry.AudioDefinition` | EXCLUDE_OTHER_OWNERSHIP |

## Scene YAML Migration Targets

Decision: SCENE_MIGRATION_REQUIRED

Only `Assets/Scenes/UIAudioScene.unity` currently serializes the six host presentation SFX map fields.

Remove from the `CombinedGameplayShowcaseInstaller` component:

```yaml
gameplayAudioMap: {fileID: 11400000, guid: 2e17653afa1ba264a950b76bcd5ccc56, type: 2}
tileFeatureAudioMap: {fileID: 11400000, guid: b4f6650dfd7e4aecb59dcad3cabe0489, type: 2}
topologyAudioMap: {fileID: 11400000, guid: 66d7af338d3f4e75b4f5c15d544fd731, type: 2}
gravityFieldAudioMap: {fileID: 11400000, guid: 20311d81a3644b22b830a914b49455d5, type: 2}
blockAudioMap: {fileID: 11400000, guid: 5a6bb3f9bcde4e6ca7487767ab9ba305, type: 2}
playerLocomotionAudioMap: {fileID: 11400000, guid: 6bf2bb925f794fe98b6b8e5a406e1f3d, type: 2}
```

Add one field:

```yaml
gameplayPresentationAudioConfig: {fileID: 11400000, guid: <new config guid>, type: 2}
```

No migration required for:

- `Assets/Scenes/MainMenuScene.unity`: UI/BGM only, no gameplay host SFX map fields.
- `Assets/Scenes/CombinedGameplayShowcase.unity`: absent in this worktree.
- `Assets/Scenes/TutorialScene.unity`: absent in this worktree.

Stage entries named `combined-gameplay-showcase` and `tutorial-scene` exist under stage content, but those are content assets, not scene YAML roots. Their `*_Audio.asset` companions are BGM metadata and must not be folded into `GameplayPresentationAudioConfig`.

## Removal Order

Decision: TEST_UPDATE_REQUIRED

1. Introduce `GameplayPresentationAudioConfig` while keeping old fields temporarily if needed for a compatibility migration commit.
2. Add config asset and populate all six map references.
3. Update installer/configuration creation to assign `GameplayPresentationAudioConfig`.
4. Update factory to consume config and call existing typed attach methods.
5. Remove old direct fields from `GameplaySceneHostConfiguration`.
6. Remove old serialized fields from `GameplayShowcaseSceneInstallerBase`.
7. Migrate `UIAudioScene` in Unity or through controlled YAML edit, preserving `.meta` files for any new asset.
8. Update tests and docs in the same PR.

## Test Update Order

Decision: TEST_UPDATE_REQUIRED

1. Add config validation unit tests.
2. Add repository smoke for `GameplayPresentationAudioConfig_CampaignV1.asset`.
3. Update UI scene contract test to assert one config reference.
4. Update architecture tests:
   - no direct six-map sprawl in host config;
   - no action/enemy/UI/BGM fields in config;
   - no generic dispatcher.
5. Run `./run_tests.sh core`.
6. Run `./run_tests.sh ui` because `UIAudioScene` serialized fields and UI scene contract tests change.

## Rollback

Decision: SCENE_MIGRATION_REQUIRED

Rollback path:

1. Restore six direct serialized fields in `GameplayShowcaseSceneInstallerBase` and `GameplaySceneHostConfiguration`.
2. Restore `GameplayHostRuntimeFactory.AttachAudioRuntimesIfConfigured` to read the six fields directly.
3. Reinsert the six field references in `UIAudioScene`.
4. Leave map assets unchanged; they are reused by both old and new shapes.
5. Remove or ignore the config asset after scene references are restored.

Rollback risk is moderate because scene YAML field names must match the restored MonoBehaviour field names.
