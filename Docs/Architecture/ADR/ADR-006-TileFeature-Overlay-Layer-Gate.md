# ADR-006 TileFeature Overlay Layer Gate

- Status: Accepted
- Date: 2026-04-30
- Last updated: 2026-05-10

## Decision

TileFeature is a `SurfaceCell`-based gameplay overlay layer.

TileFeature is not Unit/Solid/Projectile occupancy. It must not be modeled as `EntityType.TileFeature`, as a Unit stack entry, as Solid occupancy, or as Projectile occupancy. TileFeature-specific kinds such as DestroyTile, SlideTile, Barricade, Exit, MoonBlock, and MoonBlockGenerator must not be added to `EntityType`.

TileFeature is not a `TerrainFlags` effect semantic. Blocking Terrain remains owned by `TerrainData` and `TerrainFlags`; non-blocker overlay behavior belongs to TileFeature or a future ADR-approved layer.

TileFeature may coexist with Unit, Box, and Projectile occupants on the same `SurfaceCell`. Wall-like solid + TileFeature requires explicit policy. TileFeature + TileFeature same-cell support is a storage capability; gameplay policy for each pair remains explicit.

`StageRuntimeBuildResult` is a gameplay-only seed. Presentation prefab and binding data are owned by `StagePresentationDefinition` or a presentation companion. VFX, audio, and UI consume facts derived from `TilePresentationEvent` or `TilePresentationRequest`; they must not call `WorldState.CreateSnapshot` to infer TileFeature state. `TickPipeline` transports presentation facts where necessary but must not execute prefab, audio, or UI work.

## Implemented Order

The implemented TileFeature pipeline followed this order and future work must preserve the same dependency direction:

1. `TileFeatureState` / `WorldState` overlay layer / `WorldSnapshot` query / hash/export
2. Stage authoring / `TileFeatureRuntimeDefinition` / activation query
3. `ProjectedWorld` TileFeature operation
4. TileEffect lazy seam
5. `FinalizationBatch` TileFeature authoritative write path
6. Button latch
7. `ButtonActivatedCondition`
8. `TickPresentationData.TileEvents`
9. `TilePresentationRequest` planner/cache
10. Visual hook
11. `StagePresentationDefinition` visual binding
12. `TileFeatureAudio` lane
13. MoonBlock identity
14. DestroyTile
15. SlideTile
16. Barricade
17. Exit
18. MoonBlockGenerator
19. MoonBlockGenerated feedback
20. MoonBlockGeneratorBlocked feedback

Dynamic TileEffect mutation must not be implemented before TileFeature state/query/export/hash exists. TileEffect-free ticks must not increase snapshot materialization budget.

## Overlap Rule

- Unit + TileFeature is allowed.
- Box + TileFeature is allowed.
- Projectile + TileFeature is allowed.
- Other Solid + TileFeature, including Wall-like solid occupants, requires an explicit future policy decision.
- TileFeature + TileFeature same-cell storage support and gameplay policy are separate decisions.
- Duplicate same-cell SlideTile is rejected by gameplay authoring policy.
- DestroyTile + SlideTile same-cell is allowed; DestroyTile wins over SlideTile redirect for destroyed boxes.
- Terrain blocker + TileFeature requires an explicit future policy decision.

## Button Policy

- Button latch is runtime state stored as `TileFeatureFlags.Activated`.
- Stage authoring must not set initial Button `Activated` state.
- `ButtonActivatedCondition` reads only the final `WorldSnapshot` `Activated` flag.
- `ButtonActivatedCondition` must not read `TilePresentationEvent` or `TilePresentationRequest` as objective completion evidence.
- `ButtonActivated` event is derived from a pre/final state transition.
- An already Activated Button must not create duplicate event, request, audio, or visual output on the next tick.
- `ButtonActivated` event `TargetEntityId` is `0`.
- `ButtonActivated` event does not carry the triggering box id yet. If needed later, add `TriggerEntityId` or an equivalent payload in a separate step.
- If topology changes later make the Button inactive, an already completed Button objective condition remains complete because it reads the latched final snapshot flag.

## MoonBlock Policy

- MoonBlock identity is `EntityType.Box + BoxArchetype.Moon`.
- Do not add `EntityType.MoonBlock`.
- Do not add `BoxCapabilities.Moon`.
- `BoxArchetype` is part of deterministic hash/export/debug trace.
- A stage may have at most one initial MoonBlock spawn.
- MoonBlock must carry `Push | Flip | Destroy` capability.
- `MoonBlockOnly` selector is identity-based and must not re-check Push capability. Push/Flip/Destroy capability is guaranteed by stage validation.
- `WorldSnapshot.TryGetBoxArchetypeAt(Vector2Int, ...)` is legacy/convenience only. TileFeature selector code must use the `SurfaceCell` overload.
- `HasMoonBlockSource` is retained as current/future naming. It currently means a generator-bound initial MoonBlock spawn; if generator-only templates are opened later, the meaning may expand by policy.
- Generator-only MoonBlock templates are intentionally closed until a later authoring policy is defined.

## DestroyTile Policy

- DestroyTile v1 targets Box and Unit through movement-derived `TileEffectEntityContact`, not final snapshot scanning.
- Stationary boxes and stationary units are not destroyed.
- Unit targets are destroyed only when ordinary Unit locomotion, locomotion anchor commit, or jump landing moves them into an active DestroyTile after movement and before attack collection.
- Player and Enemy are both Unit targets; Player death presentation/audio is transported through `TickResult` presentation facts, not direct gameplay UI/audio calls.
- Projectile and non-box solid occupants are not destroyed in v1.
- MoonBlock is a Box, so a moving MoonBlock contact is destroyed.
- DestroyTile itself is not consumed, updated, or removed.
- Contact facts are transient and are not authoritative state or direct determinism hash input.
- DestroyTile contact facts come from accepted `MoveEntity` operations only.
- Phase relocation, spawn/respawn, topology relocation, and projectile movement are not DestroyTile contact sources in v1.
- Follow-up: Ground JumpChaser landing candidate should prefer Neutral over LethalOnEnter.
- Follow-up: Ground JumpChaser should cancel or fallback when all landing candidates are LethalOnEnter.
- Follow-up: Air JumpChaser may ignore DestroyTile hazard.
- `DestroyTileTriggered` is a resolver-origin event.
- The same destroyed entity creates at most one event per tick.
- Multiple destroyed entities may create multiple events.
- DestroyTile supports `BottomFaceOnly` and `FrontFaceOnly` activation; default authoring remains `BottomFaceOnly`.
- `DestroyTileTriggered` event `TargetEntityId` is the destroyed entity id.

## SlideTile Policy

- SlideTile activation rule is `FrontFaceOnly`.
- SlideTile direction must be Up, Right, Down, or Left.
- SlideTile selector must be `BoxSelector.None`.
- SlideTile handles only `PushEnter` and `SlideEnter` contact kinds.
- `FlipLanding` and `ImpactFollowThrough` are excluded from the MVP.
- SlideTile does not perform same-tick extra movement.
- SlideTile retargets box facing only.
- For sliding boxes, `EntityState.facing` is the authoritative continuation direction. SlideTile redirect changes facing, not position.
- If the box is already facing the redirect direction, no SetFacing operation and no event are created.
- DestroyTile wins: a destroyed box is not Slide redirected.
- `SlideTileRedirected` event is emitted only from the actual state-change branch.
- `SlideTileRedirected` event carries a `Direction` payload.
- `SlideTileRedirected` event, request, audio, and visual output are presentation-only and do not directly enter the canonical determinism hash.

## Barricade Policy

Barricade box-only blocker MVP is implemented. Barricade remains a TileFeature overlay, not occupancy, terrain, or an entity type.

- Barricade activation rule is `FrontFaceOnly`.
- Barricade direction must be `None`.
- Barricade selector must be `None`.
- Barricade is a box-only movement blocker.
- Unit/player/enemy traversal is not blocked.
- Projectile movement is not blocked.
- Impact follow-through and topology relocation are not blocked by Barricade.
- Active Barricade blocks box push first step, sliding continuation entry, and flip landing entry.
- Inactive Barricade does not block existing DestroyTile or SlideTile behavior.
- Inactive Barricade does not block existing flip behavior.
- Push/Destroy first-step blocked fallback stays the existing first-step semantics.
- Flip landing blocked by active Barricade does not create hostile unit impact reservation, Unit kill/eject, DestroyTile contact, or SlideTile contact.
- Active Barricade + DestroyTile same-cell emits `BarricadeBlocked`, not `DestroyTileTriggered`.
- Active Barricade + SlideTile same-cell emits `BarricadeBlocked`, not `SlideTileRedirected`.

Barricade active-transition crush is separate from movement blocking.

- Inactive to active transition may destroy a same-cell valid Box.
- Active Barricade cells are not scanned every tick.
- Crush uses logical `CubeTopologyState`, not visual progress, presenter state, or camera state.
- Unit kill/eject and Projectile interaction are not implemented.
- MoonBlock is a Box, so it may be crushed.
- If DestroyTile and Barricade attempt to destroy the same box, DestroyTile wins.
- `BarricadeCrushed` is emitted only when an actual crush operation is created.

`BarricadeBlocked` is sourced from movement blocker facts. `BarricadeCrushed` is sourced from `TileFeatureEffectResolver.ResolveBarricadeCrushes`. They must not be merged into a generic BarricadeTriggered event.

## Exit Policy

- Exit is a TileFeature overlay.
- Exit is not `EntityType.Exit`.
- Exit is not `TerrainFlags.Exit`.
- Exit activation rule is `BottomFaceOnly`.
- Exit direction must be `None`.
- Exit selector must be `None`.
- A stage may have at most one Exit.
- Exit center cell is the canonical clear cell.
- Exit 3x3 footprint is presentation prefab plus base-tile suppression responsibility, not gameplay modeling.
- Existing goal zone remains the canonical clear target.
- In an Exit stage, the goal zone including face must exactly equal the Exit center one-cell zone.
- Exit open is derived from required non-PrimaryGoal conditions complete plus active Exit.
- Exit open is not mutable TileFeature state and must not reuse `TileFeatureFlags.Activated`.
- Player is the only Exit clear trigger.
- Enemy, projectile, MoonBlock, and Box do not trigger clear.
- Box on Exit center blocks player clear through existing solid occupancy.
- Exit clear uses the existing StageSession/evaluation/reward/progression lane.
- Exit does not directly create `StageClearResult`.

Exit presentation is presentation-only.

- `ExitOpened` source is objective-derived transition.
- `ExitOpened` emits only for required non-primary conditions incomplete to complete plus active Exit.
- A stage with no required non-primary conditions is initially open and emits no `ExitOpened`.
- `ExitEntered` source is objective clear tick plus player occupancy at active Exit center.
- Same-tick open and enter emits both events, ordered `ExitOpened` before `ExitEntered`.
- Exit events do not directly enter the canonical determinism hash.
- UI/HUD, `StageResult`, and `ObjectiveStatus` are not changed by this TileFeature pass.

## MoonBlockGenerator Policy

- MoonBlockGenerator is a TileFeature overlay.
- MoonBlockGenerator activation rule is `BottomFaceOnly`.
- Direction must be `None`.
- BoxSelector must be `None`.
- `BoundEntityId > 0` is required.
- `BoundEntityId` must reference an existing `StageSpawnKind.Box`.
- The referenced Box must be `BoxArchetype.Moon`.
- The referenced MoonBlock must have `Push | Flip | Destroy` capability.
- A stage may have at most one MoonBlockGenerator.
- Generator-only templates are not implemented.
- Generator-bound initial MoonBlock spawn is the stable id/template source.
- A live MoonBlock anywhere makes the generator a no-op.
- Missing, dead, detached, or marked MoonBlock lets an active generator attempt respawn.
- Empty generator cell respawns the MoonBlock.
- Normal/non-Moon Box at the generator cell is detached/marked destroy before MoonBlock spawn.
- Unit/player/enemy at the generator cell causes defer; no kill or eject occurs.
- Projectile is not a blocker and is not destroyed.
- Wall-like/non-box solid causes defer.
- Blocking box destroy then MoonBlock spawn ordering is deterministic.
- Final solid occupant at the generator cell must be the single MoonBlock.
- Respawn uses the same stable entity id.
- Respawn resets hp, board presence, marked/transient/timer/lock state.
- Facing, archetype, and capabilities come from the template.
- MoonBlockGenerator feedback must not use the generic RespawnedEntities presentation path.

## MoonBlockGenerated Policy

- `MoonBlockGenerated` event emits only on actual respawn success.
- Event source is MoonBlockGenerator respawn processor success fact.
- Final snapshot diffing must not create the event.
- Event is created from the success fact after `writeContext.SpawnEntity(respawnEntity)`.
- Live MoonBlock no-op emits no event.
- Inactive generator emits no event.
- Unit conflict defer emits no generated event.
- Wall-like/non-box solid defer emits no generated event.
- Invalid/skipped path emits no event.
- `TargetEntityId` is the respawned MoonBlock entity id.
- Event `Cell` is the generator cell.
- `TileFeatureKind` is `MoonBlockGenerator`.
- Blocking box id, spawn reason, and conflict replacement payload are not implemented.
- Visual target is generator-tile feedback only and must not find MoonBlock entity views directly.
- `MoonBlockGenerated` audio cue is optional.
- `ButtonActivated` remains the only required TileFeatureAudio cue.

## MoonBlockGeneratorBlocked Policy

`MoonBlockGeneratorBlocked` is debounced presentation-only feedback for generator defer cases. It does not alter respawn gameplay policy and is not gameplay authority.

- `MoonBlockGeneratorBlocked` does not alter respawn gameplay policy.
- `TilePresentationEventKind.MoonBlockGeneratorBlocked`, `TilePresentationRequestKind.MoonBlockGeneratorBlocked`, `TileFeatureAudioCue.MoonBlockGeneratorBlocked`, and optional `IMoonBlockGeneratorBlockedVisualTarget` are open.
- The event kind remains `MoonBlockGeneratorBlocked`; reason-specific event kinds are not introduced.
- `MoonBlockGeneratorBlocked` carries a presentation-only `MoonBlockGeneratorBlockedPayload`.
- Payload fields are `Reason`, `BlockingEntityId`, and `BlockedCell`.
- Reason values are `UnitOccupant`, `WallLikeSolid`, and `PlacementBlocked`.
- `MoonBlockGeneratorBlockedPayload` is transported through event, request, visual, and audio surfaces only.
- Reason-specific visual and audio feedback is payload-driven and falls back to generic blocked feedback when no reason-specific binding is configured.
- Unit/player/enemy conflict defer emits blocked feedback.
- Wall-like/non-box solid defer emits blocked feedback.
- Placement-blocked defer emits blocked feedback with `BlockingEntityId` `0`.
- Inactive generator emits no blocked event.
- Live MoonBlock no-op emits no blocked event.
- Live MoonBlock no-op and inactive generator do not emit `MoonBlockGeneratorBlocked`.
- Normal/non-Moon Box conflict destroy plus spawn success emits `MoonBlockGenerated`, not blocked.
- Projectile coexist spawn success emits `MoonBlockGenerated`, not blocked.
- Debounce key is GeneratorTileId + BlockedReason + BlockingEntityId, with `0` for no blocking entity.
- The same key does not emit repeatedly while maintained; key change may emit.
- Generator inactive, blocker cleared, live MoonBlock exists, and MoonBlockGenerated success clear debounce memory.
- Debounce memory is transient processor state, not `WorldState`, `StageRuntimeBuildResult`, snapshot, or determinism hash input.
- Public event/request payload exposes only MoonBlockGenerator-specific presentation facts and is not authoritative gameplay state.
- Audio remains optional Sfx one-shot; reason-specific binding entries are owned by `TileFeatureAudioMap`.
- No UI/HUD notification, spatial audio/Play3D, Unit kill/eject, Projectile destroy, or wall-like solid destroy is introduced.

## Presentation Rule

Current `TilePresentationEvent` source matrix:

- `ButtonActivated`: pre/final snapshot diff.
- `DestroyTileTriggered`: TileEffectResolver-origin event.
- `SlideTileRedirected`: TileEffectResolver-origin event.
- `BarricadeBlocked`: movement blocker fact.
- `BarricadeCrushed`: TileEffectResolver-origin event.
- `ExitOpened`: objective-derived transition.
- `ExitEntered`: objective clear tick plus player at active Exit center.
- `MoonBlockGenerated`: MoonBlockGenerator respawn processor success fact.
- `MoonBlockGeneratorBlocked`: MoonBlockGenerator respawn processor debounced defer fact.

`TilePresentationEvent` is a presentation-only fact and must not enter the canonical determinism hash.

`TilePresentationRequestPlanner` converts events to requests only. It must not decide gameplay, read `WorldState`, read `WorldSnapshot`, or call `CreateSnapshot`.

Coordinator request cache is replaced every tick. A no-event tick clears the cache to empty. Consumers must not consume, remove, or clear the request cache.

Dedupe belongs to event generation. Planner and consumers do not dedupe; duplicate requests intentionally produce duplicate visual/audio handling.

VFX, audio, and UI consume facts derived from `TilePresentationEvent` or `TilePresentationRequest`.

VFX, audio, and UI must not call `WorldState.CreateSnapshot` to infer TileFeature state.

`TickPipeline` must not execute prefabs or effects. Presentation code must not decide gameplay trigger, consume, or expire outcomes.

## Visual Rule

- Visual consumers read only `CurrentTilePresentationRequests`.
- Visual consumers must not parse `TickPresentationData.TileEvents` directly and must not call `TilePresentationRequestPlanner`.
- Visual consumers must not reference `WorldState`, `WorldSnapshot`, `CreateSnapshot`, `ProjectedWorld`, `FinalizationBatch`, or `TickPipeline`.
- Missing visual targets and unsupported optional target interfaces are no-op with optional diagnostics.
- Duplicate requests are not deduped.
- TileFeature visual binding is owned by `StagePresentationDefinition`.
- Direct `TileId -> VisualPrefab` binding is the MVP.
- `StageRuntimeBuildResult` must not contain TileFeature visual prefab or binding data.
- `StageDefinition` must not contain TileFeature visual prefab references.
- Transform placement is instantiate/register only; `SurfaceCell` world-position mapping is not implemented.

## Audio Rule

- TileFeatureAudio is separate from core GameplayAudio, GameplayActionAudio, and UI audio lanes.
- TileFeatureAudio reads only `CurrentTilePresentationRequests`.
- `TileFeatureAudioRequestPlanner` reads `TilePresentationRequest` values, not `TickPresentationData.TileEvents`, and must not call `TilePresentationRequestPlanner`.
- `ButtonActivated` cue is required.
- `DestroyTileTriggered`, `SlideTileRedirected`, `BarricadeBlocked`, `BarricadeCrushed`, `ExitOpened`, `ExitEntered`, `MoonBlockGenerated`, and `MoonBlockGeneratorBlocked` cues are optional.
- Only Sfx one-shot playback is allowed.
- Ui, Bgm, Voice, Ambience, Master, loop, and non-null playback policy bindings are rejected.
- Missing optional bindings are no-op.
- Null `TileFeatureAudioMap` disables tile audio.
- If a map is provided and `ButtonActivated` is missing, fail fast.
- Do not open Play3D or spatial audio APIs.
- SurfaceCell world-position audio is not implemented.
- `StagePresentationDefinition` has no TileFeature audio binding.

## StagePresentationDefinition TileFeature Binding Rule

- TileFeature visual binding is owned by `StagePresentationDefinition`.
- Direct TileId binding is the MVP.
- PresentationKey/catalog is a future step.
- Runtime configure is the source of truth. Prefab serialized TileId may be a placeholder.
- Duplicate registry policy is warning plus first-win.
- Missing visual binding has no gameplay effect.
- Runtime invalid binding is warning plus skip.
- `StagePresentationDefinition` has no TileFeature audio binding.
- TileFeature visual binding and audio binding must not be mixed.
- 3x3 Exit footprint placement is prefab authoring plus presentation base-tile suppression, not gameplay model.

## UI Boundary

- UI is not gameplay authority.
- UI must not mutate `WorldState`.
- UI must not call `WorldState.CreateSnapshot` to infer TileFeature state.
- UI must not read `TilePresentationEvent` or `TilePresentationRequest` as objective completion evidence.
- UI/HUD notifications for TileFeature are not implemented unless a separate UI.Application mapping step opens them.
- `ObjectiveStatus` and `StageResult` are not modified by this hardening pass.
- UI uses UIAccess / presentation snapshot / viewmodel flow.

## Audio Boundary

- Audio is a presentation concern.
- `WorldState`, `TickPipeline`, and EntityLogic must not call playback.
- Public playback remains 2D-only.
- No Play3D or spatial contract is open.
- Semantic meaning is owned by maps/profiles, not `AudioDefinition`.
- TileFeatureAudio remains a separate lane.
- UI audio remains hidden Ui channel and separate from TileFeatureAudio.

## TickPipeline Authority Boundary

- `WorldState` exposes no public TileFeature mutation API.
- TileEffect resolver does not directly mutate `WorldState`.
- Resolve stage uses `ProjectedWorld` / `FinalizationBatch`.
- Finalize applies authoritative `WorldState` mutation.
- Respawn phase write path uses `IWorldWriteContext`.
- `TickPipeline` does not plan `TilePresentationRequest`.
- `TickPipeline` does not execute visual/audio/UI.
- `TickPipeline` only transports presentation facts where necessary.

## TerrainFlags Boundary

`TerrainFlags` remains blocker terrain vocabulary. Future blocker-only flags may be added only when they preserve terrain blocker semantics.

Do not add Trap, Hazard, Buff, Trigger, Aura, Zone, TileFeature, MoonBlockGenerator, Barricade, Exit, or other effect semantics to `TerrainFlags`.

Any non-blocker terrain behavior or gameplay overlay effect belongs to TileFeature or another explicitly accepted future ADR. Any new `TerrainFlags` value that is not clearly blocker terrain vocabulary requires an ADR/test update before implementation.
