# Gameplay VFX Governance

## Decision

Gameplay VFX is a presentation-only lane. It is not tile-only, and it serves Player, Box, Enemy, TileFeature, Terrain, Projectile, and Objective/Stage domains.

Gameplay VFX consumes `TickResult.PresentationData`-derived presentation facts. Gameplay VFX must not read WorldState, WorldSnapshot, or TickPipeline, and must not call WorldState.CreateSnapshot. TickPipeline must not execute prefabs or visual effects.

## Architecture Shape

The intended expansion shape is:

```text
TickResult.PresentationData
  -> family-specific planners
  -> GameplayVfxRequestPlan
  -> future host-side GameplayVfxPresentationController
  -> future VfxAnchorResolver
  -> future VfxPersistentHandleRegistry
  -> future VfxPool
  -> future VfxLifetimeRunner
```

Family-specific planners are:

- `PlayerVfxRequestPlanner`
- `BoxVfxRequestPlanner`
- `EnemyVfxRequestPlanner`
- `TileFeatureVfxRequestPlanner`
- `TerrainVfxRequestPlanner`
- `ProjectileVfxRequestPlanner`
- `ObjectiveStageVfxRequestPlanner`

The family planners preserve domain-specific presentation facts and translate them into common request values. They must not collapse gameplay domains into a generic string dispatcher.

## Non-Goals For This Phase

- No production playback connection.
- No prefab instantiate/playback implementation.
- No runtime `VfxPool` implementation.
- No existing presenter migration.
- No TileFeature runtime implementation.
- No TileEffect seam implementation.
- No `TickPresentationData.TileEvents` implementation.
- No `StagePresentationDefinition` VFX binding implementation.

## Existing Presenter Coexistence

Existing VFX-like presenters remain the production playback path for their current facts:

- `GameplayTransientEffectPresenter`
- `GameplayExitPresentationController`
- `GameplayFrontFaceShieldVfxPresenter`
- `GameplayUtilityWindupVfxPresenter`
- `BoxFlipInteractionDriver` / `FlipImpactTrack`

The new Gameplay VFX lane must not consume the same fact concurrently with these presenters. Existing presenter migration is a future slice and must include a feature flag or adapter strategy plus duplicate-prevention tests. The first production VFX slice must choose a bounded fact that does not overlap existing presenters.

Guard phrase: existing presenter migration is a future slice.

## Transient Vs Persistent

Transient event VFX examples:

- damage burst
- death burst
- box destroy smoke
- flip impact burst
- projectile hit
- tile trap triggered

Persistent desired state VFX examples:

- aura
- armed tile glow
- hazard idle
- shield active loop
- windup telegraph
- status effect aura
- continuous charge trail

One-shot effects are event based. Loop, aura, glow, and telegraph effects are persistent desired state and must reconcile by `VfxPersistentKey`. Desired-set exit stops an active persistent key through StopEmitting -> TailPlaying -> ReleasedToPool.

## Anchor Policy

Anchor vocabulary:

- Cell
- Entity
- EntitySlot
- MotionTrack
- CellToEntity
- EntityToCell
- BoardLocal
- Screen

Anchor slots:

- CellFloor
- CellCenter
- CellAboveOccupant
- EntityFeet
- EntityCenter
- EntityHead
- EntityFront
- EntityBack
- HitPoint
- MotionPath

Fallback order:

1. explicit anchor from presentation fact
2. live entity view / slot anchor
3. last-known presentation state
4. fallback SurfaceCell + topology
5. missing-anchor policy

Cell-anchored VFX must preserve `SurfaceCell(face, x, y)`. Do not flatten SurfaceCell to planar `Vector2Int`.

## Timing Policy

Timing vocabulary:

- ImmediateOnTickPresentation
- AtMotionStart
- DuringMotion
- AtMotionContact
- AtMotionEnd
- OnStateEnter
- OnStateExit
- Delayed
- QueuedUntilTopologyTransitionEnd

General VFX is non-blocking. Topology transition is the special blocking presentation lane. Ordinary smoke, dust, trail, and aura effects must not block tick progression.

## Lifecycle Policy

Lifecycle vocabulary:

- Spawned
- Active
- StopEmitting
- Detached
- TailPlaying
- ReleasedToPool
- HardCleanup

Stop policy vocabulary:

- NaturalCompletion
- AuthoredDuration
- StopEmittingThenRelease
- DetachThenStopEmittingThenRelease
- ManualStopRequired
- HardCleanupOnly

Smoke, dust, and trail effects must not be destroyed immediately when the source entity disappears. Entity removal VFX should detach or spawn under a VFX runtime root. HardCleanup is only for scene unload, host dispose, pool dispose, or emergency cleanup. Source entity lifecycle and VFX tail lifecycle are separate.

## Binding Ownership

Ownership defaults:

- Tile / Terrain / Stage environmental cue: `StagePresentationDefinition` or stage presentation map.
- Player common VFX: player prefab-local profile or host default profile.
- Box common VFX: box/static entity presentation profile or host default profile.
- Enemy-specific VFX: enemy prefab-local profile.
- Projectile VFX: projectile prefab/profile.
- Global fallback: host-level default VFX map.

Do not put every player, box, enemy, and projectile VFX binding into `StagePresentationDefinition`. Stage should own stage-specific tile, terrain, and environment bindings, not the global cue vocabulary.

## Prefab Validation Policy

Future validation rules:

- gameplay collider / gameplay collision layer forbidden
- non-kinematic Rigidbody forbidden
- gameplay-affecting MonoBehaviour forbidden
- AudioSource forbidden by default unless explicitly governed
- allowed: ParticleSystem, Renderer, Animator, presentation-only allowlisted scripts

## Future Implementation Order

1. Governance doc and architecture tests
2. Gameplay VFX core value types
3. family planner skeleton
4. no-snapshot / authority isolation tests
5. host-side controller/pool/lifecycle skeleton
6. bounded first production slice that does not overlap existing presenters
7. existing presenter migration slice with duplicate prevention tests
8. Stage/prefab binding expansion
