# Audio Test Coverage Plan

## Current Related Tests

### Core / EditMode

- `AudioRepositoryAssetSmokeCoreTests`
  - validates all `AudioDefinition` assets.
  - validates `BgmProfile` assets.
  - validates all `StageAudioDefinition` assets.
- `BgmFlowArchitectureTests`
  - guards Flow_Audio separation from gameplay one-shot planning.
  - guards `StagePresentationDefinition` and visual adapters from owning BGM.
- `BgmFlowRuntimeTests`
  - validates profile failures, same-profile dedupe, restart, Immediate, FadeOutIn, Crossfade fallback, and bootstrap fail-fast behavior.
- `CampaignStageFlowTests`
  - covers visual adapter background behavior.
  - covers `StageAudioRuntimeRequestSource` submitting stage gameplay requests through `BgmRequestRouter`.
- `StageContentAndClearFlowTests`
  - validates stage content requires an audio companion.
- `StageAudioDefinitionValidationTests`
  - covers explicit no-BGM slots.
  - rejects missing required gameplay slot metadata.
  - rejects profile-on-none and profile-mode-without-profile mistakes.
  - rejects non-BGM and non-loop profiles.
  - verifies resolved data assembly.

### PlayMode

- `AudioRuntimePlayModeTests`
  - validates runtime playback, hidden UI channel behavior, gameplay pause group, and BGM fade/mute behavior.
- `PersistentBgmFlowPlayModeTests`
  - validates persistent BGM flow.
- `ActualSceneBootstrapSmokePlayModeTests`
  - checks scene bootstrap and persistent BGM root constraints.

### UI EditMode

- `GameplayShellUiAudioContractTests`
  - verifies `UIAudioScene` uses the stage audio path without an enabled scene default requester.
- `MainMenuAudioSceneContractTests`
  - verifies main menu scene BGM source/profile/flow bootstrap.
- `UiAudioSfxContractTests`
  - freezes UI cue surface and validates authored UI cue map policy.

## Remaining Gaps

| Gap | Why It Matters | Proposed Test | Lane |
|---|---|---|---|
| Result/phase BGM runtime policy | metadata slots exist, but product flow ownership is not decided | add after result/phase owner decision | play/integration |
| Full lane map asset smoke | not every SFX map/profile is equally covered | `AudioLaneMapRepositorySmokeTests` | core/edit |
| BGM docs parity | stale transition docs can return | `BgmTransitionModeDocumentationParityTests` | core/edit |
| UI cue docs parity | old cue count docs can drift | `UiAudioCueDocumentationParityTests` | UI edit |

## Scene Bootstrap Coverage

Current:

- `MainMenuScene` BGM source is validated as a scene-default request.
- `UIAudioScene` stage-backed BGM path is validated.
- persistent root duplication is validated.

Policy:

- Stage-backed scenes submit gameplay BGM through `StageAudioRuntimeRequestSource`.
- Enabled `SceneBgmRequestSource` components are not allowed to coexist with stage-backed gameplay BGM unless a future scene-default-only marker is explicitly introduced and tested.

## Validation Commands

Default runtime/audio validation:

```bash
./run_tests.sh core
```

Run UI lane when UI audio docs/tests/assets are touched:

```bash
./run_tests.sh ui
```

