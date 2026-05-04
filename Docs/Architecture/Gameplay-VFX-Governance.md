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

## First Production Cue Gate

The first production Gameplay VFX cue is `EnemyVfxCue.JumperLandingTarget`.

Source fact:

- `TickPresentationData.EnemyJumpSignals`

Trigger:

- jump windup start only
- `StartedWindupThisTick`
- `TickEnemyJumpPresentationOutcome.WindupStarted`

Non-trigger:

- airborne start
- landed
- retry or continuation

Anchor:

- `PresentationTargetCell`
- `VfxAnchorKind.Cell`
- `VfxAnchorSlot.CellFloor`
- `SurfaceCell(face, x, y)` must be preserved. Do not flatten the target to planar coordinates.

Lifecycle:

- v1 is a transient one-shot request.
- Persistent landing telegraph desired state is a future slice.

Binding:

- default owner is the host default `VfxCueMap`.
- enemy presentation-local ownership is active through `EnemyPresentationCatalogEntry.VfxProfileAsset`.
- a null enemy catalog profile keeps the host default fallback.

Feature flag:

- `EnableEnemyJumpTargetVfx`
- default false

Non-goals:

- no existing presenter migration
- no TileFeature runtime
- no persistent telegraph
- no `StagePresentationDefinition` VFX field
- no `TickPresentationData` shape change

## JumperLandingTarget V1 Visual Tuning

`EnemyVfxCue.JumperLandingTarget` now has a first shared production marker asset:

- prefab: `Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/JumperLandingTargetVfx.prefab`
- material: `Assets/_Features/Gameplay/Gameplay_Vfx/Materials/M_JumperLandingTarget_RedOrange.mat`
- binding: `Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/JumperLandingTarget_Binding.asset`
- host default map: `Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset`

V1 policy:

- transient one-shot marker only
- anchor is `PresentationTargetCell` with `VfxAnchorSlot.CellFloor`
- playback is `OneShot`
- stop policy is `AuthoredDuration`
- lifetime is `0.55` seconds
- tail is `0.25` seconds
- initial pool size authoring hint is `4`
- max concurrent instances is `8`

Visual tuning:

- low-poly geometric floor marker
- red/orange danger palette
- roughly 80% of one cell footprint
- small prefab-local surface lift to avoid floor z-fighting
- no audio, collider, dynamic Rigidbody, NavMeshAgent, or gameplay-affecting script

The host connection for manual verification is limited to `Assets/Scenes/CombinedGameplayShowcase.unity`, where `GameplayVfxProductionRuntime` is attached to the existing host object with `EnableEnemyJumpTargetVfx` enabled and the host default cue map assigned.

Future work remains out of scope for this slice: persistent telegraph desired state, jump execute/cancel/death/retarget stop logic, prefab-local jumper profile ownership, stage/tile/terrain VFX, and existing presenter migration.

## Prefab-local Profile Owner Gate

Prefab-local or presentation-local `VfxProfile` override requires request source identity. `GameplayVfxRequest.SourceEntityId` is the canonical source identity field for request-time profile lookup.

`PresentationSeed` must not be used as source identity. The seed remains a visual variation / replay-like variant input, not a source ownership contract.

Missing or invalid source identity means profile lookup is skipped and host default fallback remains available. A source id is profile-resolvable only when `SourceEntityId > 0`.

The active enemy VFX profile owner is `EnemyPresentationCatalogEntry.VfxProfileAsset`. The source entity presentation profile is resolved by using `GameplayVfxRequest.SourceEntityId` to look up `EntityId -> PresentationId -> EnemyPresentationCatalogEntry.VfxProfileAsset`, then converting the asset with `BuildRuntimeProfile()` during production setup. A null catalog profile is valid and means host default fallback remains in effect.

Enemy presentation catalog types live in `Game.Feature.Gameplay.EnemyPresentation`. That assembly may reference VFX authoring for the catalog-owned `VfxProfileAsset`; gameplay core must not reference VFX authoring.

StagePresentationDefinition binding override is a future stage-specific override, not the v1 owner. Prefab component authoring is possible, but it is not canonical until explicitly chosen because it depends on live views and cannot by itself distinguish the same prefab under different presentation identities.

## Request Identity Rule

`GameplayVfxRequest.SourceEntityId` participates in request equality, ordering, hashing, and diagnostics. It is request semantic identity for source-aware presentation resolution.

`SourceEntityId` must be positive to be profile-resolvable. `SourceEntityId <= 0` means source unavailable.

VFX planners may copy source entity id from presentation facts such as `TickPresentationData.EnemyJumpSignals`. Source id is presentation-derived and must not cause WorldState, WorldSnapshot, TickPipeline, ProjectedWorld, FinalizationBatch, DeterminismHashBuilder, or CreateSnapshot reads.

`PresentationSeed` remains separate from `SourceEntityId`. Do not substitute seed values for source identity when resolving prefab-local or presentation-local profiles.

## Profile Resolver Rule

Short-term request-time VFX binding precedence is:

1. request-local profile from source entity / presentation identity
2. existing family profile
3. host default map
4. missing binding

Long-term VFX binding precedence is:

1. prefab / presentation-local profile
2. stage map
3. future level map
4. future campaign map
5. host default map
6. missing binding

The request-time resolver must remain runtime-safe: it returns `VfxProfile` / `VfxBindingRuntimePolicy` data only and must not know GameObject, ScriptableObject, catalog assets, stage assets, or host objects. Production setup may read catalog/profile assets to build the runtime provider and prefab lookup map; per-request binding resolution consumes only runtime profiles and cue maps.

## VFX Planner Dependency Rule

Gameplay VFX planners may read presentation carriers such as `TickPresentationData`.

Gameplay VFX planners must not read authoritative simulation types or authority construction APIs:

- `WorldState`
- `WorldSnapshot`
- `TickPipeline`
- `ProjectedWorld`
- `FinalizationBatch`
- `DeterminismHashBuilder`
- `CreateSnapshot`

Planner inputs must remain presentation-derived. Planner output is `GameplayVfxRequest` data only and must not materialize snapshots.

## Production Runtime Dependency Rule

`Game.Feature.Gameplay.Vfx.Host` remains the anchor and pool primitive assembly. It must not reference Authoring, Loop, stage bindings, `TickPresentationData`, or concrete production presentation carriers.

`Game.Feature.Gameplay.Vfx.ProductionRuntime` is the only VFX assembly allowed to know both production presentation carriers and VFX authoring or prefab lookup. It may reference Authoring, `Game.Feature.Gameplay.Vfx.Host`, and the host extension seam, but it must not reference authoritative simulation types or snapshot creation APIs.

`Gameplay_Host` uses the `IGameplayTickPresentationExtension` seam. The host factory may attach already co-located enabled extensions, but production bootstrap does not create `GameplayVfxProductionRuntime`, does not add VFX authoring fields to scene configuration, and must not directly depend on concrete VFX production runtime types unless a future bootstrap slice explicitly opens that dependency.

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

## Host Anchor Resolver Gate

This stage resolves VFX anchors through host presentation seams. It does not instantiate prefabs, implement a runtime pool, control particles, or connect production playback.

The host anchor resolver decides whether a `GameplayVfxRequest.Anchor` can be resolved to a logical `VfxResolvedAnchor`. resolver true/false is independent from missing-anchor policy. Missing-anchor handling remains owned by binding/controller policy, and the resolver does not read required/optional binding state.

Cell anchors use `SurfaceCell` plus committed or fallback topology and preserve `SurfaceCell(face, x, y)`. V1 supports `CellFloor`, `CellCenter`, and `CellAboveOccupant` as host-projectable cell slots. Projection failure returns false.

Entity anchors use live host entity presentation or last-known host presentation state when available. V1 supports `EntityCenter`. Entity anchors may fallback to fallback `SurfaceCell` when the live or known entity anchor is unavailable. `EntityFeet`, `EntityHead`, `EntityFront`, `EntityBack`, and `HitPoint` are unsupported in v1 unless a future slice defines and tests their host pose semantics.

`CellToEntity` validates the source cell and target entity, then resolves to the source cell in v1. `EntityToCell` resolves to the source entity when available and otherwise uses the anchor fallback cell. Path, line, and beam rendering are future pooled-runtime work.

`MotionTrack`, `BoardLocal`, and `Screen` anchors are future unless implemented with tests. MotionTrack actual support is a future slice.

Topology transition policy for v1 is committed/fallback topology only. General VFX remains non-blocking, topology transition remains the special blocking presentation lane, and transition-aware VFX anchors are future unless explicitly supported with tests. `QueuedUntilTopologyTransitionEnd` scheduling is not implemented in this gate.

## Anchor Resolver Ownership

Core `VfxAnchor` remains logical request data. The host resolver uses `GameplayCubeProjector`, `GameplayPresentationStateStore`, `GameplayEntityViewRegistry`, or narrower adapters to verify host-side projectability.

Runtime controller still consumes logical `VfxResolvedAnchor` until a pooled runtime requires host pose materialization. World position, rotation, and transform materialization are future pooled runtime work and must not leak into the core VFX public surface.

The host resolver must not read WorldState, WorldSnapshot, TickPipeline, ScriptableObject authoring assets, GameObject prefab bindings, or stage presentation bindings. It must not call WorldState.CreateSnapshot. It must not add fields to `GameplaySceneHostConfiguration`, `StagePresentationDefinition`, or entity prefab authoring, and it must not connect `GameplayTickPresentationCoordinator` or `GameplayTickViewPresenter`.

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

## Pooled Runtime Gate

This stage introduces a pooled GameObject runtime for Gameplay VFX. It remains disconnected from production tick presentation and is only available through test-only or manual command invocation.

The pooled runtime consumes `ResolvedVfxPlaybackCommand` only. It uses explicit `GameplayVfxRuntimeRoot` ownership under a caller-provided scene or host transform. It must not create a global singleton, search the scene for a lazy runtime, or call `DontDestroyOnLoad`.

The pooled runtime must not read WorldState, WorldSnapshot, or TickPipeline, and must not call WorldState.CreateSnapshot. Anchor input remains the logical `VfxResolvedAnchor`; v1 pooled playback does not materialize world-space anchor poses.

The host runtime does not reference VFX authoring assets. Prefab access is isolated behind `IVfxPrefabProvider`, and production authoring-to-provider binding is a future slice. Pool runtime must not bypass prefab validation policy; authoring validation remains the gate for rejecting gameplay colliders, AudioSource, NavMeshAgent, dynamic Rigidbody, and gameplay-affecting scripts.

## Pooling Policy

One-shot transient effects lease prefab instances under `OneShotRoot` and return them to `PoolRoot` after authored lifetime plus tail. Persistent loop/follow effects lease under `PersistentRoot`; desired-state dedupe by `VfxPersistentKey` remains owned by the persistent handle registry before the pool.

StopEmitting and Detach do not immediately destroy the GameObject. `StopEmittingThenRelease` stops new emission, enters `TailPlaying`, and releases after `TailSeconds`. `DetachThenStopEmittingThenRelease` first moves the instance to `TailRoot`, then stops new emission, preserves the tail, and releases after `TailSeconds`.

`ManualStopRequired` and `HardCleanupOnly` do not auto-release during normal `Advance`. HardCleanup is reserved for scene unload, host dispose, pool dispose, or emergency cleanup and clears active, tail, and inactive pooled instances.

`MaxConcurrentInstances` is enforced per `GameplayVfxCueId`. A value of `0` means unlimited. In v1, over-limit transient requests are skipped and counted in pool diagnostics. Over-limit persistent requests return no handle; the persistent registry must not register or mark desired state for a null handle. Required/fail-fast over-limit policy is future work.

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

1. pooled runtime tests must pass
2. no-snapshot tests must pass
3. authoring prefab validation tests must pass
4. anchor resolver tests must pass
5. first production cue must be selected
6. duplicate presenter conflict must be resolved
7. feature flag must exist
8. presenter-isolation tests must pass
9. first production cue must not overlap existing presenters

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
