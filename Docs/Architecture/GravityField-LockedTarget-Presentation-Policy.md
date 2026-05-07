# GravityField Locked Target Presentation Policy

GravityField is `EntityType.Box + BoxArchetype.GravityField`, not a TileFeature. Its locked target presentation data stays on the GravityField lane and must not use `TilePresentationEvent`, `TilePresentationRequest`, or `TileFeatureAudioCue`.

## MVP Surface

- `GravityFieldVisualState.LockedTargetEntityIds` is the current presentation read model for boxes selected by an active GravityField lock operation.
- `GravityFieldPresentationEventKind.LockedBox`, `GravityFieldPresentationRequestKind.LockedBox`, and `GravityFieldAudioCue.LockedBox` are intentionally not implemented in this MVP.
- Target dimming, target material mutation, UI/HUD, spatial audio, `Play3D`, and environmental destroy immunity are intentionally not implemented in this MVP.

## Source Fact Policy

- Locked target facts are resolver-origin facts emitted while `GravityFieldRuntimeResolver` selects eligible targets for active GravityField lock planning.
- The read model must not be inferred from a final snapshot diff.
- Facts are produced for eligible stationary same-face 3x3 Box targets selected by an active eligible emitter, including the emitter itself when current gameplay lock behavior selects it.
- Unit, Projectile, non-box, dead, detached, marked, sliding, phased, invisible, inactive, charging, and otherwise ineligible targets are excluded.

## Debounce Policy

- Future one-shot `LockedBox` event/audio, if opened, must debounce by emitter-target pair within a single active window.
- A target leaving and re-entering range during the same active window must not re-emit the one-shot.
- A later active window may emit again for the same emitter-target pair.
- The current MVP uses only a continuous read model, so there is no repeated event/audio spam path.

## State And Architecture

- Locked target presentation data is presentation-only and does not enter the canonical determinism hash.
- Gameplay lock behavior remains owned by authoritative `BoxInteractionLockState`.
- Visual/audio consumers must not call `WorldState.CreateSnapshot`.
- `TickPipeline` transports facts but must not execute prefab, audio, UI, or material work.
