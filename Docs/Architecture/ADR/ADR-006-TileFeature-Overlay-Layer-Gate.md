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
- TileFeature + TileFeature may be multi-allowed by future policy.
- Terrain blocker + TileFeature requires an explicit future policy decision.

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

VFX, audio, and UI read `TilePresentationEvent` facts derived from `TickPresentationData`.

VFX, audio, and UI must not call `WorldState.CreateSnapshot` to infer TileFeature state.

`TickPipeline` must not execute prefabs or effects. Presentation code must not decide gameplay trigger, consume, or expire outcomes.

## TerrainFlags Boundary

`TerrainFlags` remains blocker terrain vocabulary. Future blocker-only flags may be added only when they preserve terrain blocker semantics.

Do not add Trap, Hazard, Buff, Trigger, Aura, Zone, TileFeature, or other effect semantics to `TerrainFlags`.

Any non-blocker terrain behavior or gameplay overlay effect belongs to TileFeature or another explicitly accepted future ADR. Any new `TerrainFlags` value that is not clearly blocker terrain vocabulary requires an ADR/test update before implementation.
