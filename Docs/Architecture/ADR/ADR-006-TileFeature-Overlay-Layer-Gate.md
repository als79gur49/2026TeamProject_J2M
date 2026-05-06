# ADR-006 TileFeature Overlay Layer Gate

- Status: Accepted
- Date: 2026-04-30

## Decision

TileFeature is a SurfaceCell-based gameplay overlay layer.

TileFeature is not Unit/Solid/Projectile occupancy. It must not be modeled as `EntityType.TileFeature`, as a Unit stack entry, as Solid occupancy, or as Projectile occupancy.

Blocking Terrain remains owned by `TerrainData` and `TerrainFlags`. Overlay TileFeature state is owned by a future separate `TileFeatureLayer`.

Overlay TileFeature state is allowed to overlap Unit, Box, and Projectile occupants on the same `SurfaceCell` unless a later explicit TileFeature policy rejects that overlap.

Other Solid occupants, including Wall-like solid occupants, require an explicit future TileFeature overlap policy.

Solid overlap is policy-controlled; Box overlap is allowed by default because the known requirement explicitly includes box overlap.

## Non-Goals For This Phase

- No TileEffect runtime implementation.
- No TileFeature runtime storage implementation.
- No Stage authoring implementation.
- No VFX runtime implementation.
- No `postTileEffectSnapshot` in `TickPipeline`.
- No interface, delegate, or executor-based phase chain.

## Vocabulary

- Blocking Terrain: terrain blocker state owned by `TerrainData` and `TerrainFlags`.
- Overlay TileFeature: deterministic gameplay overlay state keyed by `SurfaceCell`.
- Unit-owned TileFeature: overlay TileFeature with owner/source attribution in TileFeature state.
- TileEffect: future deterministic gameplay resolve for TileFeature triggers, consumption, expiry, and mutation.
- TilePresentationEvent: future presentation-only fact derived from authoritative tick data.

## Required Future State Surface

Future runtime implementation must preserve the following dependency order. Dynamic TileEffect mutation must not be implemented before TileFeature state/query/export/hash exists.

State surface phase:

1. `TileFeatureState`
2. `WorldState` TileFeatureLayer
3. `WorldSnapshot` tile query
4. ordered tile export
5. `DeterminismHashBuilder` tile hash
6. `StageRuntimeBuildResult.InitialTileFeatures`

Dynamic mutation phase:

7. `ProjectedWorld` tile operation support
8. TileEffect lazy seam

Presentation phase:

9. `TickPresentationData.TileEvents`
10. VFX presentation-only lane

TileEffect consume, expire, spawn, or move mutation must not be implemented before `ProjectedWorld` tile operation support exists.

## Overlap Rule

- Unit + TileFeature is allowed.
- Box + TileFeature is allowed.
- Projectile + TileFeature is allowed.
- Other Solid + TileFeature, including Wall-like solid occupants, requires an explicit future policy decision.
- TileFeature + TileFeature same-cell storage support and gameplay policy are separate decisions.
- Duplicate same-cell SlideTile is rejected by gameplay authoring policy.
- DestroyTile + SlideTile same-cell is allowed; DestroyTile wins over SlideTile redirect for destroyed boxes.
- Terrain blocker + TileFeature requires an explicit future policy decision.

## Implemented TileFeature Policies

Button:

- Button latch is runtime state stored as `TileFeatureFlags.Activated`.
- Stage authoring must not set initial Button `Activated` state.
- `ButtonActivatedCondition` reads only the final `WorldSnapshot` `Activated` flag.
- `ButtonActivatedCondition` must not read `TilePresentationEvent` or `TilePresentationRequest` as objective completion evidence.
- `ButtonActivated` `TilePresentationEvent` is derived from a pre/final state transition.
- An already Activated Button must not create duplicate event, request, audio, or visual output on the next tick.
- `ButtonActivated` event `TargetEntityId` is `0`.
- `ButtonActivated` event does not carry the triggering box id yet. If needed later, add `TriggerEntityId` or an equivalent payload in a separate step.

MoonBlock:

- MoonBlock identity is `EntityType.Box + BoxArchetype.Moon`.
- Do not add `EntityType.MoonBlock`.
- Do not add `BoxCapabilities.Moon`.
- `BoxArchetype` is part of deterministic hash/export/debug trace.
- A stage may have at most one initial MoonBlock spawn.
- MoonBlock must carry `Push | Flip | Destroy` capability.
- `MoonBlockOnly` selector is identity-based and must not re-check Push capability. Push/Flip/Destroy capability is guaranteed by stage validation.
- `HasMoonBlockSpawn` currently means initial MoonBlock spawn. If MoonBlockGenerator is added later, rename or extend this to `HasMoonBlockSource` or `HasMoonBlockProvider`.
- `WorldSnapshot.TryGetBoxArchetypeAt(Vector2Int, ...)` is legacy/convenience only. TileFeature and MoonBlock selector code must use the `SurfaceCell` overload.

DestroyTile:

- DestroyTile uses movement-derived `TileEffectBoxContact`, not final snapshot scanning.
- Stationary boxes are not destroyed.
- Unit, Projectile, and non-box solid occupants are not destroyed.
- MoonBlock is a Box, so a moving MoonBlock contact is destroyed.
- DestroyTile itself is not consumed, updated, or removed.
- Contact facts are transient and are not authoritative state or direct determinism hash input.
- DestroyTile contact facts come from accepted `MoveEntity` operations only.
- Post-attack follow-through, flip landing, spawn/respawn, and topology relocation are not DestroyTile contact sources.
- `DestroyTileTriggered` is a resolver-origin event.
- The same destroyed box creates at most one event per tick.
- Multiple destroyed boxes may create multiple events.
- DestroyTile activation rule is `BottomFaceOnly`.
- `DestroyTileTriggered` event `TargetEntityId` is the destroyed box id.

SlideTile:

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

## Future TileFeatureState Draft

The first runtime type should remain SurfaceCell-based and carry owner/source attribution:

```csharp
public readonly struct TileFeatureState
{
    public int TileId;
    public SurfaceCell Cell;
    public TileFeatureKind Kind;
    public TileFeatureFlags Flags;
    public int SourceEntityId;
    public int OwnerEntityId;
    public int TeamId;
    public int LifetimeTicks;
    public int Charges;
}
```

This ADR does not implement the type.

## Unit-Owned Tile Policies

- StaticOwned
- FollowOwner
- LeaveTrail
- PulseAroundOwner

The first implementation should prioritize StaticOwned.

## Snapshot Budget Rule

TileEffect-free ticks must add zero snapshot materialization. `postTileEffectSnapshot` must not be eagerly created.

A future TileEffect seam belongs after final movement/jump/phase relocation resolution and before final attack plan input collection. The attack input read surface must be materialized lazily only when TileEffect operations exist.

Empty TileEffect batches must not dirty `ProjectedWorld`.

Future TileEffect implementation should prefer a call-site guard that does not call `ProjectedWorld.ApplyBatch` when the TileEffect batch is empty, unless `ProjectedWorld.ApplyBatch` semantics are explicitly changed and snapshot budget tests are updated.

## Presentation Rule

TilePresentationEvent source is currently hybrid:

- `ButtonActivated`: pre/final snapshot diff.
- `DestroyTileTriggered`: TileEffectResolver-origin event.
- `SlideTileRedirected`: TileEffectResolver-origin event.

`TilePresentationEvent` is a presentation-only fact and must not enter the canonical determinism hash.

`TilePresentationRequestPlanner` converts `TickPresentationData.TileEvents` to requests. It must not decide gameplay, read `WorldState`, read `WorldSnapshot`, or call `CreateSnapshot`.

Coordinator request cache is replaced every tick. A no-event tick clears the cache to empty. Consumers must not consume, remove, or clear the request cache.

Dedupe belongs to event generation. Planner and consumers do not dedupe; duplicate requests intentionally produce duplicate visual/audio handling.

VFX, audio, and UI read `TilePresentationEvent` facts derived from `TickPresentationData`.

VFX, audio, and UI must not call `WorldState.CreateSnapshot` to infer TileFeature state.

`TickPipeline` must not execute prefabs or effects. Presentation code must not decide gameplay trigger, consume, or expire outcomes.

## Visual Rule

- Visual consumers read only `CurrentTilePresentationRequests`.
- Visual consumers must not parse `TickPresentationData.TileEvents` directly and must not call `TilePresentationRequestPlanner`.
- Visual consumers must not reference `WorldState`, `WorldSnapshot`, `CreateSnapshot`, `ProjectedWorld`, `FinalizationBatch`, or `TickPipeline`.
- Missing visual targets and unsupported optional target interfaces are no-op with optional diagnostics.
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
- `DestroyTileTriggered` and `SlideTileRedirected` cues are optional.
- Only Sfx one-shot playback is allowed.
- Ui, Bgm, Voice, Ambience, Master, loop, and non-null playback policy bindings are rejected.
- Missing optional bindings are no-op.
- Null `TileFeatureAudioMap` disables tile audio.
- If a map is provided and `ButtonActivated` is missing, fail fast.
- Do not open Play3D or spatial audio APIs.
- SurfaceCell world-position audio is not implemented.

## StagePresentationDefinition TileFeature Binding Rule

- TileFeature visual binding is owned by `StagePresentationDefinition`.
- Direct TileId binding is the MVP.
- PresentationKey/catalog is a future step.
- Runtime configure is the source of truth. Prefab serialized TileId may be a placeholder.
- Duplicate registry policy is warning plus first-win.
- Missing visual binding has no gameplay effect.
- Runtime invalid binding is warning plus skip.
- `StagePresentationDefinition` has no TileFeature audio binding yet.
- TileFeature visual binding and audio binding must not be mixed.

## Barricade Policy

Barricade box-only blocker MVP is implemented. Barricade remains a TileFeature overlay, not occupancy, terrain, or an entity type. Active Barricade blocks box push first-step and sliding-continuation entry only when active through `FrontFaceOnly`; unit, enemy, projectile, flip landing, impact follow-through, and topology relocation traversal ignore Barricade.

Barricade presentation is implemented as feedback-only metadata. `BarricadeBlocked` is sourced from movement blocker facts because a blocked box never enters the Barricade cell and no `TileEffectBoxContact` exists. `BarricadeCrushed` is sourced from `TileFeatureEffectResolver.ResolveBarricadeCrushes` only after an actual box destroy operation is created.

Barricade events flow through `TilePresentationEvent`, `TilePresentationRequest`, optional tile visual interfaces, and optional TileFeatureAudio Sfx cues. Events do not mutate gameplay state, do not enter the determinism hash, and presentation consumers must not call `WorldState.CreateSnapshot`.

## TerrainFlags Boundary

`TerrainFlags` remains blocker terrain vocabulary. Future blocker-only flags may be added only when they preserve terrain blocker semantics.

Do not add Trap, Hazard, Buff, Trigger, Aura, Zone, TileFeature, or other effect semantics to `TerrainFlags`.

Any non-blocker terrain behavior or gameplay overlay effect belongs to TileFeature or another explicitly accepted future ADR. Any new `TerrainFlags` value that is not clearly blocker terrain vocabulary requires an ADR/test update before implementation.
