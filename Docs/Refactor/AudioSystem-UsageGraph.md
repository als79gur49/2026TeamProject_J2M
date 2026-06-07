# AudioSystem Current Usage Graph

Investigation date: 2026-06-06 KST

Scope checked:

- `Assets/_Shared/Audio`
- `Assets/_Shared/AudioContracts`
- `Assets/_Features/Flow/Flow_Audio`
- `Assets/_Features/Gameplay/Gameplay_Audio`
- `Assets/_Features/Gameplay/Gameplay_ActionAudio`
- `Assets/_Features/Gameplay/Gameplay_Host`
- `Assets/_Features/UI/UI_Application`
- `Assets/_Features/UI/UI_Composition`
- `Assets/_Features/UI/UI_Flow`
- `Assets/_Features/UI/UI_Screens`
- `Assets/_Features/UI/UI_Tests`
- `Assets/_Features/Gameplay/Gameplay_Tests`
- `Assets/_Features/Stages`
- `Assets/Scenes`
- `.asset`, `.prefab`, `.unity`, `.meta`, `.asmdef` serialized references

Commands and evidence used:

- `git status --short --branch`
- `git diff --stat`
- `rg` searches for `AudioManager`, `IAudioService`, `PlayBgm`, `Play3D`, `UiAudioCueId`, `BgmFlowCoordinator`, gameplay audio semantics, action moments, `Legacy|Deprecated|Obsolete|Compat|Old|V0|Test|Sample|_Test`
- `.meta` GUID reverse lookup for audio clips, definitions, maps, profiles, scenes, and prefabs
- Targeted source reads for shared runtime, BGM flow, gameplay one-shot, gameplay action audio, UI SFX, settings bridge, and stage BGM integration

## Shared Runtime Graph

```text
Feature requester or narrow port
  -> IAudioService / IAudioSettingsService / IAudioPlaybackPauseService
  -> AudioRuntimeInstaller same-root access seam
  -> AudioRuntimeRoot
  -> AudioManager
  -> AudioPlaybackService + AudioMixingService
  -> pooled AudioSource / single BGM lane / live playback registry
```

Current canonical runtime code:

| Type | Path | Assembly | Public API | Serialized | Unity Object | Runtime entry | Decision |
|---|---|---|---:|---:|---|---:|---|
| `IAudioService` | `Assets/_Shared/Audio/Runtime/IAudioService.cs` | `Game.Shared.Audio` | yes | no | no | port | KEEP_CANONICAL |
| `AudioRuntimeInstaller` | `Assets/_Shared/Audio/Runtime/AudioRuntimeInstaller.cs` | `Game.Shared.Audio` | yes | yes | `MonoBehaviour` | `Awake/Install` | KEEP_CANONICAL |
| `AudioRuntimeRoot` | `Assets/_Shared/Audio/Runtime/AudioRuntimeRoot.cs` | `Game.Shared.Audio` | yes | yes | `MonoBehaviour` | `InitializeRuntime` | KEEP_CANONICAL |
| `AudioManager` | `Assets/_Shared/Audio/Runtime/AudioManager.cs` | `Game.Shared.Audio` | yes | yes | `MonoBehaviour` | runtime service | KEEP_CANONICAL |
| `AudioPlaybackService` | `Assets/_Shared/Audio/Runtime/AudioPlaybackService.cs` | `Game.Shared.Audio` | internal | no | no | playback impl | KEEP_CANONICAL |
| `AudioMixingService` | `Assets/_Shared/Audio/Runtime/AudioMixingService.cs` | `Game.Shared.Audio` | internal | no | no | settings/mix impl | KEEP_CANONICAL |
| `PlayerPrefsAudioSettingsStore` | `Assets/_Shared/Audio/Runtime/PlayerPrefsAudioSettingsStore.cs` | `Game.Shared.Audio` | internal | no | no | default persistence | KEEP_CANONICAL |
| `AudioRuntimeExternalRootRegistry` | `Assets/_Shared/Audio/Runtime/AudioRuntimeExternalRootRegistry.cs` | `Game.Shared.Audio` | internal/public debug | no | no | persistent BGM bootstrap plumbing | KEEP_RESERVED |

Observed runtime policies:

- Public playback surface is `Play2D`, `PlayAttached`, request-based `PlayBgm`, `Stop`, `StopBgm`.
- `rg` found no runtime `Play3D`, `FindObjectOfType<.*Audio`, `FindAnyObjectByType<.*Audio`, `AudioManager.Instance`, or feature-side `new AudioManager`.
- `AudioManager` fails fast before `AudioRuntimeRoot.InitializeRuntime`.
- `AudioRuntimeInstallerBindingMode.PreferRegisteredPersistentRuntime` is used by persistent BGM bootstrap scenes; `LocalOnly` remains the default non-canonical scene behavior.
- `AudioCategory.Master` is enum value `0` and is rejected by `AudioDefinitionCategoryRules`; no repository `.asset` audio definition uses `category: 0`.

## BGM Graph

```text
Scene entry or stage presentation metadata
  -> SceneBgmRequestSource or StagePresentationRuntimeAdapter
  -> BgmProfile or StageBgmProfileCatalog
  -> IBgmFlowCoordinator
  -> BgmFlowCoordinator
  -> IBgmPlaybackPort
  -> IAudioService.PlayBgm(AudioBgmPlaybackRequest)
  -> AudioManager / AudioPlaybackService
  -> single BGM AudioSource lane
```

Canonical scene bootstrap:

```text
MainMenuScene / UIAudioScene root
  -> AudioRuntimeInstaller(PreferRegisteredPersistentRuntime)
  -> GlobalAudioFlowBootstrap
  -> GlobalAudioFlowRoot
  -> persistent AudioRuntimeRoot + BgmFlowCoordinator
```

Key findings:

- `SceneBgmRequestSource` exists in both `Assets/Scenes/MainMenuScene.unity` and `Assets/Scenes/UIAudioScene.unity`.
- `GlobalAudioFlowBootstrap` exists in both scenes and is co-located with `AudioRuntimeInstaller`.
- `UIAudioScene` also serializes `stageBgmProfileCatalog`.
- `StagePresentationRuntimeAdapter` reads `StagePresentationDefinition.BgmReference`, resolves it through `StageBgmProfileCatalog`, and delegates to `IBgmFlowCoordinator`; it does not own fade implementation.
- `BgmTransitionMode.Immediate` and `FadeOutIn` are executed. `Crossfade` is explicitly reserved and falls back with a warning.
- `HorrorVol2FactoryMain_Test_BgmProfile` is referenced by `UIAudioScene` `SceneBgmRequestSource`; `_Test` in the name is not enough to delete it.

## Gameplay Core One-Shot SFX Graph

```text
TickResult.PresentationData damage/exit facts
  -> GameplayAudioRequestPlanner
  -> GameplayAudioPresentationController
  -> GameplayAudioMap_UI-Audio_Test
  -> embedded AudioBinding
  -> IGameplayAudioPlaybackPort
  -> IAudioService.PlayAttached or Play2D fallback
  -> AudioManager / AudioPlaybackService
```

Governed semantic set:

- `PlayerDamage`
- `EnemyDamage`
- `EntityExitItemConsume`
- `EntityExitBoxDestroy`
- `EntityExitEnemyDeath`
- `EntityExitOutOfBounds`

Key findings:

- `GameplayAudioSemanticCatalog.RequiredOneShotV1` derives from descriptor metadata, not a hand-maintained separate list.
- `GameplayAudioRequestPlanner` reads `TickResult.PresentationData`; no `WorldState` or mutable simulation reads were found.
- `GameplayAudioMap_UI-Audio_Test` has all six governed semantics and is serialized in `UIAudioScene`.
- `GameplayHostRuntimeFactory` requires same-root `AudioRuntimeInstaller` when `GameplayAudioMap` is assigned and does not use scene-global fallback.

## Gameplay Action SFX Graph

```text
TickResult.PresentationData.PlayerActionSignals / PlayerActionAttemptSignals
  -> GameplayActionAudioRequestPlanner
  -> GameplayActionAudioPresentationController
  -> owner GameplayEntityView
  -> prefab-local GameplayActionAudioAuthoring
  -> Player_S1_GameplayActionAudioProfile_Test
  -> embedded AudioBinding
  -> IGameplayAudioPlaybackPort
  -> IAudioService.PlayAttached or Play2D fallback
```

Action/moment vocabulary:

- actions: `Push`, `Flip`
- moments: `Windup`, `Execute`, `Contact`, `ImpactEnemy`, `Blocked`, `Recovery`, `AssistOutOfRange`, `NoTarget`, `Invalid`

Key findings:

- `Player_S1_GameplayActionAudioProfile_Test` is serialized by `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/Player_S1.prefab`; `_Test` in the name is not enough to delete it.
- Missing owner view, missing `GameplayActionAudioAuthoring`, and missing optional profile entry are current no-op policy.
- `Execute` and `Recovery` are valid enum members but optional in the current production profile.
- No same-tick duplicate suppression exists; this is governed as intentional layering.

## UI SFX Graph

```text
UI flow outcome / local widget / HUD / transition overlay event
  -> UIFlowCoordinator or local runtime controller
  -> IUiAudioPort
  -> UiAudioPortAdapter
  -> UiAudioCueMap_V1
  -> embedded AudioBinding
  -> IAudioService.Play2D
  -> AudioManager / AudioPlaybackService
  -> hidden Ui channel
```

Current `UiAudioCueId` set in code:

- `NavigateForward`
- `NavigateBack`
- `Confirm`
- `Cancel`
- `Select`
- `Toggle`
- `AdjustValueCommit`
- `ChanceGain`
- `ChanceLoss`
- `LastChance`
- `ObjectiveComplete`
- `TopologyShift`
- `PrimaryMenuCommand`
- `StageLaunch`
- `KeyboardMove`
- `GameClear`
- `StageClear`
- `LevelFailed`

Key findings:

- `UiAudioCueMap_V1` has one explicit entry for every current `UiAudioCueId` and is serialized in both `MainMenuScene` and `UIAudioScene`.
- `UiAudioPortAdapter` calls `IAudioService.Play2D` only.
- `UiAudioCueMap` validation requires `AudioCategory.Ui`, disallows loops, disallows null binding, and rejects attachment slots.
- UI cue count has expanded beyond the old seven-cue prompt list. The additional cues are runtime/test covered by flow, HUD, transition, keyboard, stage result, and main menu paths; do not delete them as stale without a product decision and test update.

## Settings Bridge Graph

```text
Settings UI view Main/Bgm/Sfx controls
  -> UI.Application IAudioSettingsPort
  -> AudioSettingsPortAdapter
  -> UIAudioChannelMapper
  -> IAudioSettingsService
  -> AudioManager
  -> AudioMixingService
  -> PlayerPrefsAudioSettingsStore on FlushSettings
```

Key findings:

- Visible settings channel enum is `Main`, `Bgm`, `Sfx`.
- `UIAudioChannelMapper` maps `Main -> AudioChannel.Master`, `Bgm -> AudioChannel.Bgm`, `Sfx -> AudioChannel.Sfx`.
- `AudioSettingsPortAdapter` uses the central mapper and does not inline shared `AudioChannel` literals.
- `AudioSettingsLifecycleRelay` flushes on pause/quit; settings changes apply immediately through `SetChannelVolume` / `SetChannelMuted`.
- `Ui`, `Voice`, and `Ambience` are hidden internal channels and are persisted by shared runtime; this is not deletion evidence.

## Stage BGM Symbolic Graph

```text
Stage content entry
  -> StagePresentationDefinition.BgmReference
  -> StageBgmReference(bgmKey)
  -> StagePresentationRuntimeAdapter
  -> StageBgmProfileCatalog
  -> BgmProfile
  -> IBgmFlowCoordinator.RequestSceneDefault
```

Key findings:

- `StageBgmReference` is a symbolic content contract, not a runtime playback owner.
- `StagePresentationDefinition.BgmReference` is validated by `StageCatalogValidator`.
- `StageBackedGameplayShowcaseInstallerBase` serializes `stageBgmProfileCatalog` and `globalAudioFlowBootstrap`, then delegates through `StagePresentationRuntimeAdapter`.
- This path is integrated, not purely dead metadata. Any cleanup here should be treated as refactor/governance work, not asset deletion.

## Scene / Bootstrap Graph

```text
MainMenuScene
  -> AudioRuntimeInstaller
  -> GlobalAudioFlowBootstrap
  -> SceneBgmRequestSource(MainMenu_BgmProfile)
  -> MainMenuUiFlowInstaller(UiAudioCueMap_V1)

UIAudioScene
  -> AudioRuntimeInstaller
  -> GlobalAudioFlowBootstrap
  -> SceneBgmRequestSource(HorrorVol2FactoryMain_Test_BgmProfile)
  -> GameplayUiFlowInstaller(UiAudioCueMap_V1)
  -> GameplaySceneHostConfiguration audio maps
```

Serialized scene findings:

- `MainMenuScene` has `AudioRuntimeInstaller`, `GlobalAudioFlowBootstrap`, and `SceneBgmRequestSource`.
- `UIAudioScene` has `SceneBgmRequestSource`, `GameplayAudioMap`, `TopologyAudioMap`, `GravityFieldAudioMap`, `BlockAudioMap`, `PlayerLocomotionAudioMap`, `AudioRuntimeInstaller`, and `GlobalAudioFlowBootstrap`.
- No scene-level `AudioManager.Instance`, direct `PlayBgm`, `Play3D`, or global lookup residue was found by targeted source search.

