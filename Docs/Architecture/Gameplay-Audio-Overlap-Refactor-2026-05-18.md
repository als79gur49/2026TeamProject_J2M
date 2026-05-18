# Gameplay Audio Overlap Refactor - 2026-05-18

## Summary

This change reduces gameplay SFX overlap without changing audio ownership boundaries.

- Shared Audio Runtime remains semantic-free.
- Gameplay feature planners and maps still own their own cue vocabularies.
- `GameplaySfxArbiter` lives in `Gameplay_Host`, at the presentation seam.
- UI audio and persistent BGM flow do not pass through gameplay SFX arbitration.
- `AudioBinding.Policy` remains reserved and must stay null in v1.

## Runtime Changes

- `AudioDefinition` now resolves an `AudioClipSelection`, allowing random clip variants to carry per-variant volume and pitch trims.
- `RandomAudioDefinition` diagnoses empty variants, null clips, invalid weights, and invalid trims; invalid trims sanitize to neutral `1f`.
- TileFeature activation/deactivation spam is coalesced by same-tick On/Off burst kind before presentation playback.
- `AudioVoicePolicy` defines reusable priority, cooldown, per-owner, per-group, duplicate, and overflow data outside Shared Audio.
- `EnemyMoveCadenceGate` is backed by the common voice policy shape while preserving its existing jitter and cadence behavior.
- `GameplaySfxArbiter` filters gameplay SFX requests across gameplay presentation controllers before playback.
- `GameplaySfxArbiter` v1 is a presentation batch admission filter, not a full live voice manager.
- `AudioPlaybackService` logs source-pool acquisition failure and returns an invalid handle as a semantic-free fallback.

## GameplaySfxArbiter v1 Scope

GameplaySfxArbiter v1 is a presentation batch admission filter, not a full live voice manager.
It handles:

- exact duplicate suppression
- priority-based admission
- global/per-owner cooldown within the current admission model
- group max admissions per presentation batch

It does not handle yet:

- live active voice accounting
- lower-priority live voice stealing
- runtime attenuation curves unless explicitly implemented
- UI/BGM ducking
- source-pool-level semantic prioritization

In GameplaySfxArbiter v1, `MaxVoicesGlobal` is interpreted as max admissions per presentation batch.
It does not count already-playing AudioSources.

GameplaySfxArbiter v1은 전체 live voice manager가 아니라 presentation batch admission filter다.
이미 재생 중인 AudioSource까지 포함해 voice 수를 계산하지 않으며,
낮은 priority live voice를 중단하는 기능도 아직 없다.

## Guardrails

- `WorldState`, `TickPipeline`, committers, and entity logic must not call `IAudioService`.
- Shared Audio must not reference gameplay, UI, BGM flow, or host assemblies.
- UI and BGM paths must not depend on `GameplaySfxArbiter`.
- Action audio remains layered by default; only exact duplicate same-tick requests are collapsed.
- Player critical feedback uses higher-priority policy and is not dropped by tile feature spam.
- `AudioBinding.Policy` remains null/reserved in v1.
- Unsupported overflow modes must not pass silently: `AttenuateThenDrop` is reserved for v2 attenuation curves, and `StealLowerPriority` is reserved for future live voice stealing.
- TileFeature On/Off burst bindings should be authored, but missing burst bindings must diagnose and fall back to one representative single request.
