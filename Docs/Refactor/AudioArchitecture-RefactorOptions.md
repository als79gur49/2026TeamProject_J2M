# Audio Architecture Refactor Options

## Implemented: Direct Stage Audio Companion

Stage gameplay BGM now uses `StageAudioDefinition` companions with direct `BgmProfile` references. The previous indirect stage BGM authoring path was removed instead of bridged.

| Field | Detail |
|---|---|
| Ownership | Stage content owns metadata only; `BgmFlowCoordinator` remains execution owner. |
| Runtime Path | `StageAudioRuntimeRequestSource` submits to `BgmRequestRouter`, which delegates to the flow coordinator. |
| Asset Migration | Each stage entry has a paired `*_Audio.asset`; reused BGM uses direct shared profile references. |
| Validation | Missing audio companion, invalid slot mode/profile pairs, non-BGM profiles, and non-loop BGM definitions fail validation. |
| Scene Policy | Stage-backed scenes must not rely on enabled scene default requesters for gameplay BGM. |

## Follow-Up: Production `_Test` Asset Rename

Production scenes/prefabs still reference some assets with `_Test` names. Rename them only through GUID-preserving Unity moves, with path-pinned tests updated in the same change.

## Follow-Up: Audio Asset Smoke Expansion

Repository smoke now covers stage audio companions. Additional lane map/profile smoke can be expanded for block, locomotion, topology, gravity field, tile feature, enemy, and UI maps as separate focused changes.

## Follow-Up: BGM/Ambience/Voice Future Channel Policy

`Voice` and `Ambience` channels exist but need product policy before runtime expansion. Any future ambience/layered BGM/ducking work must keep playback execution in audio flow code, not stage content.

## Follow-Up: Result and Phase BGM Policy

`StageAudioDefinition` already has metadata slots for preview, result, and phase BGM. Runtime request ownership for result and phase transitions should be added only when product flow semantics are decided.
