# GravityField Locked Target Presentation Policy

GravityField is `EntityType.Box + BoxArchetype.GravityField`, not a TileFeature. Its locked target presentation data stays on the GravityField lane and must not use `TilePresentationEvent`, `TilePresentationRequest`, or `TileFeatureAudioCue`.

## Presentation Surface

- `GravityFieldVisualState.LockedTargetEntityIds` is the continuous presentation read model for boxes selected by an active GravityField lock operation.
- Target dimming consumes the previous/current `LockedTargetEntityIds` read model diff and remains independent of one-shot events.
- `GravityFieldPresentationEventKind.LockedBox`, `GravityFieldPresentationRequestKind.LockedBox`, and `GravityFieldAudioCue.LockedBox` are implemented on the GravityField lane for first lock acquisition feedback.
- `GravityFieldLockedBoxPayload` carries `EmitterEntityId`, `TargetEntityId`, `EmitterCell`, and `TargetCell`.
- LockedBox one-shot feedback is presentation-only and complements target dimming; it does not replace the continuous read model.
- `MaterialPropertyBlock`-based actual dimming remains a future presentation-only step.
- UI/HUD, spatial audio, `Play3D`, and environmental destroy immunity remain intentionally unimplemented.

## Source Fact Policy

- Locked target facts are resolver-origin facts emitted while `GravityFieldRuntimeResolver` selects eligible targets for active GravityField lock planning.
- The read model must not be inferred from a final snapshot diff.
- Facts are produced for eligible stationary same-face 3x3 Box targets selected by an active eligible emitter, including the emitter itself when current gameplay lock behavior selects it.
- Unit, Projectile, non-box, dead, detached, marked, sliding, phased, invisible, inactive, charging, and otherwise ineligible targets are excluded.

## Debounce Policy

- One-shot `LockedBox` event/audio debounces by `EmitterEntityId + TargetEntityId + ActiveWindow`.
- `Charging -> Active` starts a new active window.
- `Active -> Charging`, ineligible reset, and emitter destroyed/detached clear active-window memory.
- The same emitter-target pair emits at most once per active window.
- A target leaving and re-entering range during the same active window must not re-emit the one-shot.
- A later active window may emit again for the same emitter-target pair.
- Multiple target ids must emit in deterministic order.
- Multiple emitters use independent emitter-target pairs.
- `LockedBox` audio is optional Sfx one-shot only: missing binding is a no-op; no loop/hum; no spatial audio; no `Play3D`.
- Repeated one-shot dedupe belongs to resolver event generation, not request planners or consumers.

## State And Architecture

- Locked target presentation data is presentation-only and does not enter the canonical determinism hash.
- LockedBox one-shot state is transient resolver/pipeline memory and does not enter `WorldState`, `EntityState`, `StageDefinition`, `StageRuntimeBuildResult`, or the determinism hash.
- Gameplay lock behavior remains owned by authoritative `BoxInteractionLockState`.
- Environmental destroy immunity is not implemented by LockedBox one-shot feedback.
- Visual/audio consumers must not call `WorldState.CreateSnapshot`.
- `TickPipeline` transports facts but must not execute prefab, audio, UI, or material work.
- GravityField remains `EntityType.Box + BoxArchetype.GravityField`, not `EntityType.GravityField` or `TileFeatureKind.GravityField`.
