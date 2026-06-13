# ADR-007 Runtime Terrain Truth Removal

- Status: Accepted
- Date: 2026-06-13

## Decision

Runtime gameplay terrain truth is removed.

`WorldState` and `WorldSnapshot` do not own terrain storage or expose terrain query APIs. Stage runtime build results and gameplay host configuration do not seed terrain data. There is no compatibility shim for removed terrain APIs.

Every in-bounds `SurfaceCell` is terrain-free for gameplay legality. A cell may still be blocked by the remaining gameplay seams:

- `BoardEdge`
- `Solid`
- `Unit`
- `Reservation`
- `TileFeature`
- topology transition reject reasons

Removed gameplay vocabulary:

- `TerrainData`
- `TerrainCellState`
- `TerrainKind`
- `TerrainFlags`
- `BlocksGroundTraversal`
- terrain snapshot queries
- terrain placement, traversal, and settlement blockers
- `LegalityBlockerKind.Terrain`
- `SlideStopperKind.Terrain`
- terrain canonical hash/debug/replay sections

## Consequences

Placement, traversal, and settlement remain separate legality domains. They must not be collapsed into a generic boolean check.

`SurfaceCell`, `FaceId`, `CubeTopologyState`, `BoardBounds`, `WorldState`, `WorldSnapshot`, Unit/Solid occupancy lanes, reservations, and TileFeature blockers remain part of the gameplay contract. `EntityType.Projectile = 2` remains a reserved/deprecated serialized compatibility slot only; it is not an active runtime occupancy lane.

Debug, replay, and determinism exports must not contain a gameplay `Terrain` section. Historical archive documents may mention older terrain plans, but active architecture docs and runtime source must not preserve a gameplay terrain contract.
