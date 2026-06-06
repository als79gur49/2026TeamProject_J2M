# Stage BGM Integration Audit

## Current Decision

Decision: `IMPLEMENTED_DIRECT_STAGE_AUDIO_COMPANION`.

Stage BGM metadata now lives on a direct authored companion:

```text
StageContentEntry
  -> StageAudioDefinition
  -> StageAudioAssembler
  -> StageAudioResolvedData
  -> StageSceneCompositionData.Audio
  -> StageAudioRuntimeRequestSource
  -> BgmRequestRouter
  -> BgmFlowCoordinator
  -> IBgmPlaybackPort
  -> IAudioService.PlayBgm
```

`StagePresentationDefinition` owns visual/text presentation only. Stage content does not play BGM directly; it only names playback profile metadata through `BgmProfile` references. Runtime execution remains owned by `BgmFlowCoordinator`.

## Runtime Policy

`BgmRequestRouter` arbitrates active request sources by priority:

| Source | Priority |
|---|---:|
| `SceneDefault` | 100 |
| `StageGameplay` | 300 |
| `StageResult` | 400 |
| `Cutscene` | 500 |

Stage-backed gameplay submits through `StageAudioRuntimeRequestSource` only. Scene default BGM sources are valid for scene-default-only scenes such as main menu, but an enabled scene default requester in a stage-backed scene is treated as a scene contract error.

## Authoring Policy

Each stage entry must have an `AudioDefinition` companion asset. The companion must explicitly choose either:

- `StageBgmSlotMode.None` for no gameplay BGM.
- `StageBgmSlotMode.Profile` with a non-null `BgmProfile`.

Profiles referenced by stage audio companions must validate as BGM and must point at loop-enabled BGM definitions. Reused BGM is represented by multiple stage audio companions directly referencing the same `BgmProfile`.

## Campaign Main Content

Current direct profile reuse:

| Stage | Gameplay BGM |
|---|---|
| `combined-gameplay-showcase` | explicit none |
| `tutorial-scene` | explicit none |
| `stage-0-1` | `Stage0-1_BgmProfile` |
| `stage-0-2` | `Stage0-1_BgmProfile` |
| `stage-1-1` | `Stage1-1_BgmProfile` |
| `stage-2-1` | `Stage2-1_BgmProfile` |
| `stage-2-2` | `Stage2-1_BgmProfile` |
| `stage-3-1` | `Stage3-1_BgmProfile` |
| `stage-3-2` | `Stage3-1_BgmProfile` |
| `stage-4-1` | `Stage4-1_BgmProfile` |
| `stage-4-2` | `Stage4-1_BgmProfile` |
| `stage-5-1` | explicit none |

## Validation

Coverage now centers on:

- required `StageAudioDefinition` companion validation.
- explicit no-BGM slot validation.
- profile-on-none and missing-profile rejection.
- non-BGM and non-loop profile rejection.
- resolved data assembly.
- stage-backed runtime request submission through `BgmRequestRouter`.
- scene default requester rejection in stage-backed scene contracts.
- repository smoke for stage audio companion assets.

