# GravityField Locked Target Presentation Policy

GravityField is `EntityType.Box + BoxArchetype.GravityField`, not a TileFeature. Its locked target presentation data stays on the GravityField lane and must not use `TilePresentationEvent`, `TilePresentationRequest`, or `TileFeatureAudioCue`.

## MVP Surface

- `GravityFieldVisualState.LockedTargetEntityIds` is the continuous presentation read model for boxes selected by an active GravityField lock operation.
- Target dimming consumes the previous/current `LockedTargetEntityIds` read model diff and does not require a one-shot event.
- `GravityFieldPresentationEventKind.LockedBox`, `GravityFieldPresentationRequestKind.LockedBox`, and `GravityFieldAudioCue.LockedBox` are intentionally not implemented in this MVP.
- LockedBox event/audio remains closed unless one-shot feedback is explicitly required.
- `MaterialPropertyBlock`-based actual dimming remains a future presentation-only step.
- UI/HUD, spatial audio, `Play3D`, and environmental destroy immunity are intentionally not implemented in this MVP.

## Source Fact Policy

- Locked target facts are resolver-origin facts emitted while `GravityFieldRuntimeResolver` selects eligible targets for active GravityField lock planning.
- The read model must not be inferred from a final snapshot diff.
- Facts are produced for eligible stationary same-face 3x3 Box targets selected by an active eligible emitter, including the emitter itself when current gameplay lock behavior selects it.
- Unit, Projectile, non-box, dead, detached, marked, sliding, phased, invisible, inactive, charging, and otherwise ineligible targets are excluded.

## Debounce Policy

- Before opening one-shot `LockedBox` event/audio, answer these reevaluation questions:
  - Is target dimming alone sufficient UX?
  - Is a separate sound required when a lock first applies?
  - How many sounds should play when multiple targets lock simultaneously?
  - Is one sound per active window sufficient?
  - Should leaving and re-entering during the same active window replay?
  - Should overlapping emitters play separately per emitter-target pair?
  - Should multiple target locks use per-target audio or emitter-level audio?
- Future one-shot `LockedBox` event/audio, if opened, must debounce by `EmitterEntityId + TargetEntityId + ActiveWindow`.
- `Charging -> Active` starts a new active window.
- `Active -> Charging`, ineligible reset, and emitter destroyed/detached clear active-window memory.
- The same emitter-target pair emits at most once per active window.
- A target leaving and re-entering range during the same active window must not re-emit the one-shot.
- A later active window may emit again for the same emitter-target pair.
- Multiple target ids must emit in deterministic order.
- Multiple emitters use independent emitter-target pairs.
- Future `LockedBox` audio is optional Sfx one-shot only: missing binding is a no-op; no loop/hum; no spatial audio; no `Play3D`.
- The current MVP uses only a continuous read model, so there is no repeated event/audio spam path.

## State And Architecture

- Locked target presentation data is presentation-only and does not enter the canonical determinism hash.
- Gameplay lock behavior remains owned by authoritative `BoxInteractionLockState`.
- Visual/audio consumers must not call `WorldState.CreateSnapshot`.
- `TickPipeline` transports facts but must not execute prefab, audio, UI, or material work.
