# Audio Architecture Refactor Options

## Implemented: Direct Stage Audio Companion

Stage gameplay BGM now uses `StageAudioDefinition.gameplayBgm` companions with direct `BgmProfile` references. The previous indirect stage BGM authoring path was removed instead of bridged.

| Field | Detail |
|---|---|
| Ownership | Stage content owns metadata only; `BgmFlowCoordinator` remains execution owner. |
| Runtime Path | `StageAudioRuntimeRequestSource` submits to `BgmRequestRouter`, which delegates to the flow coordinator. |
| Asset Migration | Each stage entry has a paired `*_Audio.asset`; reused BGM uses direct shared profile references. |
| Validation | Missing audio companion, invalid owner metadata, invalid gameplay slot mode/profile pairs, non-BGM profiles, and non-loop BGM definitions fail validation. |
| Scene Policy | Stage-backed scenes must not rely on enabled scene default requesters for gameplay BGM. |

StageAudioDefinition v1 supports only gameplay BGM. Stage result/failure BGM, boss/objective phase BGM, preview/menu BGM, ambience, and layered music are intentionally out of scope and not modeled.

## Follow-Up: Production `_Test` Asset Rename

Production scenes/prefabs still reference some assets with `_Test` names. Rename them only through GUID-preserving Unity moves, with path-pinned tests updated in the same change.

## Follow-Up: Audio Asset Smoke Expansion

Repository smoke now covers stage audio companions. Additional lane map/profile smoke can be expanded for block, locomotion, topology, gravity field, tile feature, enemy, and UI maps as separate focused changes.

## Follow-Up: Voice Channel Policy

`Voice` exists as an internal channel and needs product policy before runtime expansion.

## Out of Scope: Future BGM and Environment Audio

Result/failure BGM, boss/objective phase BGM, preview/menu BGM, ambience, and layered music are not part of StageAudioDefinition v1. They should be modeled only after a separate product decision, with playback execution still owned by audio flow code rather than stage content.
