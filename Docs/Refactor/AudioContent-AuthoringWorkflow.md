# Audio Content Authoring Workflow

## Principles

Audio content should enter runtime through authored data and feature requesters:

```text
AudioClip
  -> AudioDefinition
  -> BgmProfile / AudioBinding / lane profile
  -> feature content companion or map
  -> runtime requester/controller
  -> IAudioService
```

Raw clips should not be direct runtime references. `AudioDefinition` owns playback identity and category. Profiles, maps, and companion definitions own feature meaning. Stage content may reference a `BgmProfile`, but it must not execute playback.

## Add Stage Gameplay BGM

1. Add the clip under `Assets/_Shared/Audio/Clips/Bgm`.
2. Create a `SingleAudioDefinition` or `RandomAudioDefinition` under `Assets/_Shared/Audio/Definitions/Bgm`.
3. Set `AudioCategory.Bgm` and `loop: true`.
4. Create or reuse a `BgmProfile` under `Assets/_Shared/Audio/Definitions/Bgm`.
5. Assign the BGM definition to the profile.
6. Open the stage's `*_Audio.asset`.
7. Set `gameplayBgm.mode` to `Profile` and assign the profile.
8. For stages without gameplay BGM, set `gameplayBgm.mode` to `None` and leave `profile` empty.

Runtime path:

```text
StageAudioDefinition.gameplayBgm
  -> StageAudioAssembler
  -> StageAudioResolvedData
  -> StageAudioRuntimeRequestSource
  -> BgmRequestRouter
  -> BgmFlowCoordinator
```

Do not add scene-local direct `IAudioService.PlayBgm` calls. Do not make visual presentation assets own BGM.

StageAudioDefinition v1 supports only gameplay BGM. Stage result/failure BGM, boss/objective phase BGM, preview/menu BGM, ambience, and layered music are intentionally out of scope and not modeled.

## Add Gameplay Core SFX Semantic

Use this only for core damage/entity-exit one-shot semantics. Do not add Push/Flip/action/locomotion/UI/BGM here.

1. Add enum value to `GameplayAudioSemanticId`.
2. Add descriptor to `GameplayAudioSemanticCatalog`.
3. Decide if it belongs in `RequiredOneShotV1`.
4. Add planner emission in `GameplayAudioRequestPlanner`.
5. Create SFX clip under `Assets/_Shared/Audio/Clips/Sfx`.
6. Create `AudioDefinition` under `Assets/_Shared/Audio/Definitions/Sfx` with `AudioCategory.Sfx` and `loop: false`.
7. Add `AudioBinding` entry to the gameplay audio map.
8. Update architecture, planner, controller, and asset smoke tests.

## Add Gameplay Action Moment SFX

Use this for Push/Flip action-side sounds.

Current GameplayActionAudioMoment v1 vocabulary is `Windup`, `AssistOutOfRange`, `NoTarget`, and `Invalid`. `Execute`, `Recovery`, `Contact`, `ImpactEnemy`, and `Blocked` were removed from action-audio vocabulary; gameplay execute/recovery timeline facts, impact, and blocked gameplay/presentation signals remain in their existing lanes.

1. If using an existing moment, add a profile entry only.
2. If adding a new moment, update `GameplayActionAudioMoment`, `GameplayActionAudioMomentCatalog`, and `GameplayActionAudioRequestPlanner`.
3. Create an SFX definition with `AudioCategory.Sfx` and `loop: false`.
4. Add `AudioBinding` to `GameplayActionAudioProfile`.
5. Mark intentionally optional entries with `IsOptional`.
6. Add prefab/profile coverage for production-required sounds.

## Add Host Presentation Lane Cue

For block, locomotion, topology, gravity field, or tile feature:

1. Add cue enum value to the lane type file.
2. Add formatting/catalog entry.
3. Decide required vs optional.
4. Add planner mapping from presentation signal/request.
5. Add map/profile entry with `AudioBinding`.
6. Add runtime controller and asset validation coverage.

Do not place feature meaning in `AudioDefinition`.

## Add Enemy Audio Cue

1. Add enum value to `EnemyAudioCue`.
2. Add formatting in `EnemyAudioCueCatalog`.
3. Add planner emission from enemy presentation facts.
4. Add profile entries to relevant `EnemyAudioProfile_*` assets.
5. For persistent loops, require loop-enabled definitions, attachment slots, and handle lifecycle ownership.
6. Update prefab/profile tests.

## Add UI Cue

1. Add value to `UiAudioCueId`.
2. Create UI definition under `Assets/_Shared/Audio/Definitions/Ui` with `AudioCategory.Ui` and `loop: false`.
3. Add explicit entry to `UiAudioCueMap_V1.asset`.
4. Trigger playback through `IUiAudioPort`; do not expose shared `AudioChannel` in UI application code.
5. Update cue-surface, authored-map, and relevant flow/widget cue tests.
