# Audio Structure Scalability Audit

## Current Shape

The BGM lane is now split into data, request arbitration, and execution:

```text
BgmProfile / BgmTransitionMode
  -> shared data-only audio authoring types

StageAudioDefinition
  -> stage-owned BGM profile metadata

BgmRequestRouter
  -> request source priority

BgmFlowCoordinator
  -> BGM continuity, dedupe, restart, and transition policy

IAudioService
  -> playback execution
```

This keeps stage content scalable without making stage the playback owner.

## Stage Audio

`StageAudioDefinition` is a required companion on `StageContentEntry`. StageAudioDefinition v1 supports only gameplay BGM. Stage result/failure BGM, boss/objective phase BGM, preview/menu BGM, ambience, and layered music are intentionally out of scope and not modeled.

Validation is fail-fast for:

- missing stage audio companion.
- invalid owner metadata.
- `None` slot with a profile.
- `Profile` slot without a profile.
- non-BGM profile.
- non-loop BGM definition.

## Request Priority

`BgmRequestRouter` priority:

| Request | Priority |
|---|---:|
| `SceneDefault` | 100 |
| `StageGameplay` | 300 |

Stage-backed gameplay submits through `StageAudioRuntimeRequestSource`; scene-default requesters are not the stage gameplay BGM path.

## Scalability Notes

- Direct profile references are acceptable for current stage BGM volume and remove string-key drift.
- Result/failure, boss/objective phase, preview/menu, ambience, and layered music behavior should be introduced only through a separate product decision and must not be inferred from v1 stage audio.
- Crossfade remains future multi-source playback capability; current executed transitions are `Immediate` and single-source `FadeOutIn`.
- SFX semantic growth should continue through lane maps/profiles, not through `AudioDefinition` category expansion or stage-level ad hoc fields.

## Validation Lanes

Use:

```bash
./run_tests.sh core
./run_tests.sh ui
```

The project-wide full lane has unrelated baseline risk; report touched-cluster lane results separately.
