# Audio System Usage Graph

## Runtime Roots

```text
AudioRuntimeInstaller
  -> AudioRuntimeRoot
  -> AudioManager
  -> AudioPlaybackService
  -> AudioMixingService
```

Feature code should consume `IAudioService` through explicit composition seams. It must not use scene-global lookup or singleton audio access.

## BGM Flow

```text
SceneBgmRequestSource
  -> BgmRequestRouter
  -> BgmFlowCoordinator
  -> IBgmPlaybackPort
  -> IAudioService.PlayBgm
```

```text
StageContentEntry
  -> StageAudioDefinition
  -> StageAudioAssembler
  -> StageAudioRuntimeRequestSource
  -> BgmRequestRouter
  -> BgmFlowCoordinator
```

`BgmProfile` and `BgmTransitionMode` are shared data-only audio authoring types. `BgmFlowCoordinator` remains in Flow_Audio and owns BGM execution policy.

## Stage Presentation

```text
StageContentEntry
  -> StagePresentationDefinition
  -> StagePresentationAssembler
  -> StageVisualRuntimeAdapter
```

Stage visual presentation owns display text, preview/background, entity/static presentation catalogs and bindings, and result text. It does not own BGM metadata or playback execution.

## Gameplay SFX

```text
TickResult.PresentationData
  -> GameplayAudioRequestPlanner
  -> GameplayAudioPresentationController
  -> GameplayAudioMap
  -> IGameplayAudioPlaybackPort
  -> IAudioService.Play2D / PlayAttached
```

Gameplay SFX remains presentation-only. `WorldState`, `TickPipeline`, entity logic, and committers must not play audio directly.

## UI SFX

```text
UI view / flow
  -> IUiAudioPort
  -> UiAudioCueMap
  -> IAudioService
```

UI application code remains insulated from shared audio channel details by composition adapters.

## Guardrails

- Stage content can reference `BgmProfile` metadata only through `StageAudioDefinition`.
- `StageAudioRuntimeRequestSource` and `SceneBgmRequestSource` are requesters, not playback owners.
- `BgmRequestRouter` owns request priority: `SceneDefault=100`, `StageGameplay=300`, `StageResult=400`, `Cutscene=500`.
- `BgmFlowCoordinator` is the only BGM execution owner above shared runtime playback.
- Direct scene or visual-adapter BGM playback calls are forbidden.

