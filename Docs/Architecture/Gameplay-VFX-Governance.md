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

## Host Lifecycle Skeleton

This stage adds controller, registry, lifetime, pool, playback-handle, and anchor-resolver interfaces only. It does not connect production playback, instantiate authored effects, or add prefab bindings. Tests use fake pools and fake resolvers so transient, persistent, lifecycle, release, and missing-anchor behavior can be verified before a runtime playback implementation exists.

The skeleton validates that transient requests do not enter the persistent registry, persistent requests dedupe by `VfxPersistentKey`, desired-set removal enters StopEmitting / TailPlaying / ReleasedToPool lifecycle, and missing anchors obey binding policy.

## Request Vs Binding Ownership

`GameplayVfxRequest` is a semantic request. It owns the requested cue, tick/sequence/seed, anchor, timing, persistent desired-state flag, and `VfxPersistentKey`.

`GameplayVfxRequest` does not own missing-anchor policy, playback mode, stop policy, tail duration, pool sizing, required/optional policy, prefab references, or prefab validation policy. Those execution choices belong to `VfxBinding`, `VfxProfile`, and cue maps.

Persistent identity and `VfxPersistentKey` remain request-owned because aura, glow, shield loop, windup telegraph, armed tile, and other desired-state facts must reconcile by semantic presentation identity before any prefab playback policy is chosen.

## Binding/Profile Policy Ownership

`VfxBindingRuntimePolicy` owns:

- `VfxBindingRequirement`
- `VfxMissingAnchorPolicy`
- `VfxPlaybackMode`
- `VfxStopPolicy`
- default lifetime, tail, and max-concurrent hints

`VfxProfile` owns prefab-local family cue policy. `VfxCueMap` owns stage, host, or global cue policy. Stage-specific tile, terrain, and environment cues should use a stage presentation map in a future slice. Player, Box, Enemy, and Projectile common VFX should use prefab-local profiles or host default maps.

## Compatibility Rule

Persistent requests require persistent-compatible binding policy. A persistent request must have a non-None `VfxPersistentKey` and must not resolve to `OneShot` playback.

Transient requests must not resolve to `Loop`, `Follow`, or `MotionTrack` playback unless a future ADR explicitly allows transient trail semantics. `ManualStopRequired` bindings require persistent requests.

Binding missing, anchor missing, and invalid policy are distinct failure modes. Binding missing skips before anchor resolution. Anchor missing follows the resolved binding's `VfxMissingAnchorPolicy`. Invalid policy or request/policy mismatch fails fast.

## Lifecycle Ownership

Source entity removal must not imply immediate VFX destruction. Desired-state exit should stop new emission, detach when policy requires it, preserve the authored tail, and release only after lifecycle completion or hard cleanup.

## Authoring Binding Gate

This stage adds ScriptableObject authoring assets for VFX binding policy. It does not connect production playback, instantiate prefabs, implement a GameObject pool, or migrate existing presenters.

Authoring assets convert to runtime-safe policy snapshots. `GameplayVfxPresentationController`, lifecycle runtime, binding resolvers, and pools consume `VfxBindingRuntimePolicy`, `VfxCueMap`, and `VfxProfile`, not ScriptableObject or GameObject references.

## Composition Ownership Gate

This stage converts VFX authoring assets into runtime-safe binding resolvers. It does not connect production playback, add scene host fields, add stage fields, add prefab owner fields, instantiate prefabs, or implement a GameObject pool.

Composition is an authoring-side seam. `GameplayVfxBindingComposition` owns conversion from host default `VfxCueMapAsset` to runtime `VfxCueMap`, conversion from family `VfxProfileAsset` entries to runtime `VfxProfile` dictionaries, and construction of `CompositeVfxBindingResolver`.

`GameplayVfxBindingCompositionResult` owns success/failure state, the built runtime map/profile snapshots, the resolver, and merged authoring validation diagnostics. Runtime VFX core remains unaware of ScriptableObject and GameObject authoring objects.

## Composition Policy

Host default map is optional in this phase. Missing host default map plus no profiles is allowed as an empty/no-op configuration until production connection is opened.

Family profiles override the host default map. Resolution order is:

1. request family profile
2. host default map
3. missing binding

Duplicate family profiles are invalid. Null profile entries are invalid by default. Invalid host maps, invalid profiles, invalid bindings, profile family `None`, and cross-family profile bindings fail composition through merged authoring diagnostics.

Stage map composition is a future slice. This gate does not decide whether future stage maps resolve before or after host defaults or prefab-local profiles.

## Future Owner Binding

Host default map owner is future gameplay host presentation config. Player, Box, Enemy, and Projectile prefab-local profile owner is future prefab authoring. Tile, Terrain, and Stage environmental map owner is future stage presentation binding.

This stage only proves conversion and resolver composition. It does not expose host default maps in scene inspectors, add `GameplaySceneHostConfiguration` fields, add `StagePresentationDefinition` fields, or attach VFX profiles to entity prefabs.

## Binding Asset Ownership

`VfxBindingDefinitionAsset` owns a single cue binding's prefab reference, requirement policy, missing-anchor policy, playback mode, stop policy, lifetime, tail, and pool-sizing hints. `VfxBindingRuntimePolicy` remains Unity-object-free and is the runtime execution policy snapshot.

`VfxCueMapAsset` owns stage, host, or global cue-map authoring and converts to `VfxCueMap`. `VfxProfileAsset` owns prefab-local family cue profile authoring and converts to `VfxProfile`.

Required, optional, and diagnostic binding requirements are authoring/runtime binding policy, not request state. Null prefab is invalid for all v1 binding assets, including optional bindings. A future disabled/no-op authoring state must be explicit instead of using null prefabs.

## Prefab Validation

VFX prefabs must not contain gameplay authority components by default. Prefab validation inspects the root and all children before production playback can be connected.

Forbidden by default:

- Collider components
- AudioSource, because audio playback is governed by the audio lane
- NavMeshAgent
- non-kinematic Rigidbody
- gameplay-affecting or non-allowlisted custom MonoBehaviour scripts

Allowed presentation components include ParticleSystem, Renderer, Animator, TrailRenderer, and LineRenderer. Kinematic Rigidbody is diagnostic only in v1 and should still be avoided for VFX prefabs.

## Stage/Prefab Binding Boundary

This stage does not add fields to `StagePresentationDefinition`, `GameplaySceneHostConfiguration`, or entity view prefab authoring. Stage-specific tile, terrain, and environment VFX map binding is a future slice. Player, Box, Enemy, and Projectile prefab-local profile binding is a future slice. Existing presenter migration remains a future slice.

## Production Connection Gate

Before connecting Gameplay VFX to `GameplayTickPresentationCoordinator`:

1. VFX authoring validation must exist
2. prefab validation must exist
3. required/optional/diagnostic binding policy must be tested
4. no-snapshot tests must pass
5. no-authority tests must pass
6. presenter-isolation tests must pass
7. duplicate presenter conflict must be resolved
8. first production cue must not overlap existing presenters
9. pool and lifecycle runtime must be implemented

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

This stage adds authoring-side prefab references, ScriptableObject authoring, prefab validation, and authoring diagnostics. It still does not add runtime prefab references or production playback connection. Current runtime binding policy values remain runtime-safe and Unity-object-free.

## Future Implementation Order

1. Governance doc and architecture tests
2. Gameplay VFX core value types
3. family planner skeleton
4. no-snapshot / authority isolation tests
5. host-side controller/pool/lifecycle skeleton
6. authoring-side composition ownership gate
7. bounded first production slice that does not overlap existing presenters
8. existing presenter migration slice with duplicate prevention tests
9. Stage/prefab binding expansion
