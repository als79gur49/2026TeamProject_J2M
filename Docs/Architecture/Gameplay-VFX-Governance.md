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

- `GameplayExitPresentationController`
- `GameplayFrontFaceShieldVfxPresenter`
- `GameplayUtilityWindupVfxPresenter`
- `BoxFlipInteractionDriver` / `PresentationMotionTrack`

`GameplayTransientEffectPresenter` playback surface removed after the ImpactTransient and OutOfBounds migrations; retained helper math lives in `EnemyDeathExitEffectPlanBuilder` for current `EnemyDeathMotion`.

The new Gameplay VFX lane must not consume the same fact concurrently with these presenters. Existing presenter migration is a future slice and must include a feature flag or adapter strategy plus duplicate-prevention tests. The first production VFX slice must choose a bounded fact that does not overlap existing presenters.

Guard phrase: existing presenter migration is a future slice.

## FlipImpact MotionTrack Anchor Gate

FlipImpact is not pure VFX. `FlipImpactPresentationDisposition.Stay` is an actual box view pose override track, while `FlipImpactPresentationDisposition.DestroySelf` is a transient clone/effect path where the source-to-impact flight overlaps break and fade presentation.

Full FlipImpact migration requires MotionTrack/contact timing ownership. This gate adds narrow `FlipImpactContactVfxAnchor` metadata from `FlipImpactPresentationSignal` plus centralized FlipImpact contact timing so burst adapters can target the impact contact without reading authority state.

This gate itself does not suppress old paths, implement MotionTrack pooled playback, or complete full FlipImpact migration. `VfxAnchorKind.MotionTrack` remains unsupported by the host resolver in production.

Slice 2 uses `FlipImpactContactVfxAnchor.ImpactCell` and `VfxAnchorSlot.CellFloor` to trigger a contact burst while original-view FlipImpact motion remains owned by the host presentation track path.

## FlipImpact Contact Burst VFX Slice

Slice 2 adds a track-owned contact burst adapter. The source fact is `TickPresentationData.FlipImpactSignals`, converted through `FlipImpactContactVfxAnchorBuilder` into `FlipImpactContactVfxAnchor` contact metadata. The emitted cue is `BoxVfxCue.FlipImpactBurst`.

Anchor and lifecycle:

- anchor: `FlipImpactContactVfxAnchor.ImpactCell`
- slot: `VfxAnchorSlot.CellFloor`
- lifecycle: transient one-shot
- timing: `VfxTimingKind.ImmediateOnTickPresentation`

Feature flag:

- `EnableGameplayVfxFlipImpactBurstMigration`
- default true after the Tier 1/2 rollout batch
- flag off drops the new burst request
- flag on allows only the new contact burst request

Original-view motion:

- `BoxFlipInteractionDriver` and `PresentationMotionTrack` remain active for original box view presentation.
- `FlipImpactPresentationDisposition.Stay` is source-to-impact-to-hold/squash-to-return motion, not Gameplay VFX playback.
- `FlipImpactStayMotionCommand` extracts the Stay motion contract before `PresentationMotionTrack` samples it.
- This is not a VFX migration for Stay.

Missing binding:

- missing `FlipImpactBurst` binding is diagnostic/no-op for the new burst.
- the old FlipImpact motion path remains independent.

Relation to `BoxVfxCue.DestroySmoke`:

- the DestroySelf duplicate box smoke guard remains.
- `FlipImpactBurst` is contact feedback at the impact cell, not destroy smoke.

Future:

- extending generic `PresentationMotionTrack` beyond FlipImpact Stay
- additional multi-phase motion migrations
- MotionTrack anchor/follow VFX support
- full `VfxAnchorKind.MotionTrack` anchor playback remains future work.

## FlipImpact Stay Presentation Motion Ownership

`FlipImpactPresentationDisposition.Stay` is original-view presentation motion. The source fact is still `TickPresentationData.FlipImpactSignals`, but the host builds `FlipImpactStayMotionCommand` before creating the Stay track. The command is a presentation motion contract, not a Gameplay VFX command.

Ownership:

- `FlipImpactStayMotionCommand` preserves source/impact cells, topology, facing, local source/impact poses, duration, contact timing, post-contact hold, squash timing, return arc multiplier, arc height, and presentation seed.
- `PresentationMotionTrack` is the canonical Stay original-view motion owner and samples source -> impact -> contact hold/squash -> return arc -> exact source reset.
- `GameplayEntityPresentationApplier` applies the sampled pose to the original entity view and performs completion cleanup.
- `BoxFlipInteractionDriver` remains the grip point and visualRoot overlay/reset coupling for player hand and box interaction. It is not a VFX spawner.

Boundaries:

- `FlipImpactBurst` remains contact feedback at the impact cell for Stay and DestroySelf.
- `FlipDestroySelfMotion` remains DestroySelf clone/fade motion only and does not consume Stay.
- `BoxDestroySmoke`/`DestroyShrink` duplicate guards remain DestroySelf/exit cleanup concerns.
- `ImpactTransientBreak` remains a reserved break hook, not a Stay replacement.
- `GameplayVfxProductionRuntime` must not own or move the original box view transform for Stay.

## PresentationMotionTrack Original-View Motion Lane

`PresentationMotionTrack` is the host presentation lane for original entity view transform, pose, and scale overrides. It is not Gameplay VFX playback and does not own prefabs, materials, bindings, cue maps, VFX anchors, or pooled VFX instances.

Ownership:

- `PresentationMotionCommand` describes host-side original-view motion inputs, phases, timing, completion pose, interaction policy, and scale policy.
- `PresentationMotionSample` is the sampled local pose and visual scale multiplier that `GameplayEntityPresentationApplier` applies to the original entity view.
- `GameplayEntityPresentationApplier` owns applying sampled pose/scale to the original entity view and owns completion cleanup for `OriginalViewMotionTracks`.
- `BoxFlipInteractionDriver` remains the grip, visualRoot overlay, and reset owner. Original-view motion can suppress that overlay through `PresentationMotionInteractionPolicy.SuppressBoxInteractionOverlay`; Gameplay VFX does not make that decision.

Current concrete user:

- `FlipImpact Stay` is the first concrete `PresentationMotionTrack` user.
- FlipImpactTrack adapter was removed; `FlipImpactInstanceKey` was removed with it.
- DestroySelf cleanup bookkeeping uses entity-id membership, not `PresentationMotionInstanceKey`, because DestroySelf is exit/VFX cleanup and not Stay original-view motion ownership.

VFX boundaries:

- `FlipImpactBurst` remains contact feedback only.
- `FlipDestroySelfMotion` remains DestroySelf clone/fade VFX only and must reject Stay.
- `ParameterizedMotionVfxCommand` must not replace Stay original-view motion.
- `GameplayVfxProductionRuntime` must not move or own original entity views through `PresentationMotionTrack` or `OriginalViewMotionTracks`.

Future inventory:

| Motion type | Current owner | Can use PresentationMotionTrack later? | Risk | Include now? |
|---|---|---:|---|---:|
| successful flip motion | `MotionTrack` / `FlipInteractionTrack` host presentation paths | Yes | interaction overlay and committed movement timing overlap | No |
| box slide presentation | `MotionTrack` with `TickEntityMotionKind.BoxSlide` | Yes | slide scale/trail VFX already has separate VFX lane | No |
| unit kinematic locomotion | `KinematicPoseOverrides` / continuous locomotion carriers | Maybe | live locomotion semantics differ from finite impact motion | No |
| enemy kinematic locomotion | `KinematicPoseOverrides` / continuous locomotion carriers | Maybe | AI semantic state and glide/jump overlays interact | No |
| jump/airborne presentation | `JumpTrack` / `JumpDetachedVisibilityState` | Maybe | detached visibility and landing timing are special | No |
| death displacement / visibility tracks | `PlayerDeathDisplacementTrack`, `VisibilityTrack`, exit ownership | Low priority | visibility/exit ownership is not pure pose sampling | No |

Future slices:

- `PresentationMotionTrack Multi-User Expansion`: successful flip, box slide, unit motion, and other original-view motion candidates.
- `MotionTrack-Following VFX Support`: VFX anchor/follow support that follows original-view motion without replacing the motion owner.

## FlipImpact DestroySelf Motion VFX Migration

This slice migrates only `FlipImpactPresentationDisposition.DestroySelf` clone/fade motion ownership into the Gameplay VFX lane. The source fact is still `TickPresentationData.FlipImpactSignals`; the migration does not read `WorldState`, `WorldSnapshot`, `TickPipeline`, or any authority snapshot.

Cue and command:

- cue: `BoxVfxCue.FlipDestroySelfMotion`
- branch: `FlipImpactPresentationDisposition.DestroySelf`
- command: `FlipDestroySelfMotionVfxCommand`
- not the same cue as `BoxVfxCue.FlipImpactBurst`
- `FlipImpactBurst` remains contact feedback at the impact cell, while `FlipDestroySelfMotion` owns source-to-impact flight plus break/fade overlap.

Command fields preserve presentation metadata from `FlipImpactPresentationSignal`: `SourceActionPlanId`, box/actor/impact target ids, source/impact `SurfaceCell`, topology, source/impact facing, source/impact local pose, flight duration, contact normalized time, break start, fade duration, arc height, and presentation seed. Pose extraction uses the host presentation resolver and the committed presentation state already available to the host extension.

Timing contract:

- full source-to-impact flight uses the resolved flip flight duration
- contact normalized time is the break/fade onset threshold
- final impact-pose arrival is the flight end, not the contact threshold
- fade duration is the remaining flight duration after break onset
- cleanup uses the pooled transient VFX lifecycle plus authored binding tail

Feature flag:

- `EnableGameplayVfxFlipDestroySelfMotionMigration`
- default true after the Tier 3 rollout batch
- flag off means no `BoxVfxCue.FlipDestroySelfMotion` playback and no old clone/arc/fade fallback
- suppress compatibility gates were removed in Legacy Surface Simplification; no old fallback switch remains
- `ApplyEntityExitOwnership()` remains active and still hides/cleans the authoritative view
- missing `FlipDestroySelfMotion` binding or prefab is diagnostic/no-op with no old fallback

Visual parity:

- v1 uses a stylized clone-like prefab: `Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/FlipDestroySelfMotionVfx.prefab`
- material: `Assets/_Features/Gameplay/Gameplay_Vfx/Materials/M_FlipDestroySelfMotion_Impact.mat`
- binding: `Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/FlipDestroySelfMotion_Binding.asset`
- host default map: `Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset`
- exact source-view mesh clone/material parity remains future work

Boundaries:

- `Stay` branch `PresentationMotionTrack` sampling is unchanged
- `BoxFlipInteractionDriver` overlay behavior is unchanged
- `BoxVfxCue.FlipImpactBurst` Slice 2 behavior is unchanged
- generic `VfxAnchorKind.MotionTrack` host resolver support remains unsupported in production
- generic Box Slide or Unit movement VFX migration is future work

## Parameterized Motion VFX Generalization

Parameterized Motion VFX generalizes the FlipDestroySelf v1 source-to-impact playback into a presentation-only primitive. It does not change gameplay movement, legality, `TickPipeline`, `WorldState`, `WorldSnapshot`, `TickPresentationData`, or `FlipImpactPresentationSignal` shape.

First concrete user:

- `BoxVfxCue.FlipDestroySelfMotion`
- adapter source: `FlipDestroySelfMotionVfxCommand`
- runtime command: `ParameterizedMotionVfxCommand`
- playback: source pose to target pose with arc height, rotation slerp, break/fade onset, authored tail, and pool release

Second concrete user:

- Box Slide trail
- cue: `BoxVfxCue.SlideDustTrail`
- adapter source: `TickPresentationData.EntityMotions` filtered to `TickEntityMotionKind.BoxSlide`
- playback: `PrefabOnly` soft dust/trail emitter moved from source cell pose to destination cell pose

Third concrete user:

- Enemy Death Motion
- cue: `EnemyVfxCue.DeathMotion`
- adapter source: `TickPresentationData.EntityExitSignals` filtered to enemy death exits
- playback: source-view clone with fallback prefab flying toward the legacy camera near-plane target and fading with the old enemy death curve

### Parameterized Motion Sampler Modes

`ParameterizedMotionVfxCommand` carries an explicit sampler mode so presentation VFX can choose the correct source-to-target pose policy without changing gameplay movement carriers.

- `FlipArc`: the existing arc/tumble sampler for `FlipDestroySelfMotion` and other flip-styled source-to-impact motion. It preserves the existing arc height, tumble rotation, break/fade, clone, and material behavior.
- `Linear`: exact source-to-target interpolation for `BoxVfxCue.SlideDustTrail`. Position uses direct linear interpolation, rotation uses direct slerp, and arc height/tumble are not applied.
- `LegacyEnemyDeathFlyAway`: enemy death exit motion parity sampler. Position uses legacy ease-out cubic travel plus `sin(t*pi)` arc offset, and rotation applies seeded spin around the legacy camera-forward local axis.

Sampler modes are presentation-only. They do not change box slide movement, enemy death cleanup, `TickPipeline`, `WorldState`, `WorldSnapshot`, `TickPresentationData`, `TickEntityMotion`, `MotionTrack`, `MotionClip`, or box slide timing. `BoxSlideTrail` v1 now uses `Linear`; `EnemyDeathMotion` uses `LegacyEnemyDeathFlyAway`; exact scrape/decal primitives remain future work.

Future possible users:

- Unit movement trail
- Projectile trail

Unit movement, Projectile movement, generic gameplay motion drivers, exact scrape/decal trails, and full `VfxAnchorKind.MotionTrack` resolver support remain outside this sampler change.

## Box Slide Trail VFX Adapter

The Box Slide trail adapter is an augmentation, not a migration. It adds a soft moving dust/trail emitter for committed box slide presentation facts while leaving existing box motion presentation active.

Source and cue:

- source fact: `TickPresentationData.EntityMotions`
- filter: `TickEntityMotionKind.BoxSlide`
- cue: `BoxVfxCue.SlideDustTrail`
- one command is emitted per slide segment; multi-segment slides are not collapsed

Motion mapping:

- source `SurfaceCell` to destination `SurfaceCell` is projected through the host `GameplayCubeProjector` / `GameplayPoseResolver` path
- source and destination face/topology/facing metadata from `TickEntityMotion` is preserved through projection
- duration comes from existing box slide timing, `GameplayMotionTimingResolver.ResolveGlobalMotionDurationSeconds(TickEntityMotionKind.BoxSlide, timingProfile)`
- sequence and presentation seed are deterministic from tick, entity id, source cell, destination cell, and cue

Runtime behavior:

- feature flag: `EnableGameplayVfxBoxSlideTrail`
- default true after the Tier 1/2 rollout batch
- flag off emits no slide trail VFX and does not affect box movement
- flag on plays a `ParameterizedMotionVfxCommand` when the binding is present
- missing binding is diagnostic/no-op
- no legacy presenter bypass or suppress gate exists because there is no old slide dust/trail path to migrate

Visual and boundaries:

- prefab: `Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/BoxSlideDustTrailVfx.prefab`
- material: `Assets/_Features/Gameplay/Gameplay_Vfx/Materials/M_BoxSlideDustTrail_SoftDust.mat`
- binding: `Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/BoxSlideDustTrail_Binding.asset`
- clone mode: `PrefabOnly`
- fade mode: `AlphaOnly`
- sampler mode: `Linear`
- the v1 visual remains a moving dust emitter; exact scrape/decal trails, Unit movement trail, and Projectile trail remain future work
- this adapter does not change `TickPipeline`, `WorldState`, `WorldSnapshot`, `ProjectedWorld`, `TickPresentationData`, `TickEntityMotion`, box movement drivers, legality, settlement, or traversal

## FrontFace Shield VFX Migration

FrontFace shield VFX migration moves active shield loop, block burst, and windup warning playback ownership into the Gameplay VFX lane. The source facts are `TickPresentationData.FrontFaceShieldSources`, `TickPresentationData.FrontFaceShieldBlocks`, and `TickPresentationData.FrontFaceShieldWindupWarnings`. The migration does not read `WorldState`, `WorldSnapshot`, `TickPipeline`, or authority snapshots, and it does not change shield gameplay rules, box slide blocking, FrontFace support legality, or `TickPresentationData` shape.

Cues and lifecycle:

- `EnemyVfxCue.FrontFaceShieldActive`: persistent desired-state active shield loop keyed by source entity id.
- `EnemyVfxCue.FrontFaceShieldBlock`: transient one-shot shield contact burst anchored at the blocked cell.
- `EnemyVfxCue.FrontFaceShieldWindup`: persistent desired-state windup warning telegraph keyed by source entity id, effect index, and activation sequence.
- `EnemyVfxCue.UtilityWindup` remains separate and consumes `TickPresentationData.SummonWindupWarnings`; it must not consume FrontFace shield windup warning facts.

Feature flags:

- `EnableGameplayVfxFrontFaceShieldActiveMigration`
- `EnableGameplayVfxFrontFaceShieldBlockMigration`
- `EnableGameplayVfxFrontFaceShieldWindupMigration`
- all default true after the Tier 1/2 rollout batch and filter their cues independently.

Old presenter bypass:

- after legacy old path cleanup, old active loop creation/update is always skipped.
- after legacy old path cleanup, old block burst playback is always skipped.
- after FrontFace shield windup migration, old telegraph creation/update is always skipped.
- suppress compatibility gates were removed in Legacy Surface Simplification.
- `GameplayFrontFaceShieldVfxPresenter` is not removed. The coordinator must still call cleanup-only empty refreshes so legacy active loops and windup telegraphs cannot linger.
- Missing Gameplay VFX binding is diagnostic/no-op with no old fallback whenever the corresponding migration flag is on.
- If `EnableGameplayVfxFrontFaceShieldWindupMigration` is false, no windup warning VFX plays and no old telegraph fallback is restored.

Binding precedence remains source presentation-local profile, then family profile, then host default map. The host default bindings are `FrontFaceShieldActive_Binding.asset`, `FrontFaceShieldBlock_Binding.asset`, and `FrontFaceShieldWindup_Binding.asset`.

Windup visual assets:

- prefab: `Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/FrontFaceShieldWindupVfx.prefab`
- material: `Assets/_Features/Gameplay/Gameplay_Vfx/Materials/M_FrontFaceShieldWindup_Telegraph.mat`
- binding: `Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/FrontFaceShieldWindup_Binding.asset`
- `telegraphPrefab`, `VFX_FrontFaceShield_Telegraph`, and `M_FrontFaceShield_Telegraph.mat` are retained for deferred serialized reference and asset cleanup, not as fallback playback.

## FlipDestroySelf Source-View Clone Parity

FlipDestroySelf v1 used a stylized clone-like prefab. The generalized playback keeps that prefab as fallback but can now clone the current source presentation `ModelRoot` through `IGameplayVfxCloneSourceProvider`.

Clone policy:

- `FlipDestroySelfMotion` uses `SourceViewCloneWithPrefabFallback`
- source clone lookup is by `GameplayVfxRequest.SourceEntityId`
- lookup reads the host presentation state store, not scene-global searches
- missing source clone falls back to the authored prefab
- missing binding or prefab remains diagnostic/no-op with no old fallback

Material ownership:

- shared material mutation is forbidden
- cloned renderer materials are instanced before alpha fade
- `_BaseColor` and `_Color` are supported alpha properties
- materials without alpha properties fall back to scale-only visual fade behavior without crashing
- material instances and cloned source objects are destroyed on pool release or hard cleanup

The original authoritative source view is never moved or faded by this path. `ApplyEntityExitOwnership()` still hides and cleans the authoritative view through the existing host presentation flow.

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
- default true after the Tier 1/2 rollout batch

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

## Enemy Jump Landing Dust Cue

The second production Gameplay VFX cue is `EnemyVfxCue.JumperLandingDust`.

Source fact:

- `TickPresentationData.EnemyJumpSignals`

Trigger:

- landing completion only
- `TickEnemyJumpPresentationOutcome.Landed`
- `TickEnemyJumpPresentationOutcome.CrushedBoxAndLanded`

Non-trigger:

- windup start
- airborne start
- retry or continuation

Anchor:

- landing `PresentationTargetCell`
- `VfxAnchorKind.Cell`
- `VfxAnchorSlot.CellFloor`
- `SurfaceCell(face, x, y)` must be preserved.

Lifecycle:

- transient one-shot request
- no persistent key
- no trail, follow, or topology-transition-aware queue behavior

Binding:

- prefab: `Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/JumperLandingDustVfx.prefab`
- material: `Assets/_Features/Gameplay/Gameplay_Vfx/Materials/M_JumperLandingDust_SoftDust.mat`
- binding: `Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/JumperLandingDust_Binding.asset`
- host default map: `Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset`
- v1 production registration is host default only; enemy presentation-local profiles still override when they explicitly contain the dust cue.

V1 policy:

- playback is `OneShot`
- stop policy is `AuthoredDuration`
- lifetime is `0.45` seconds
- tail is `0.30` seconds
- initial pool size authoring hint is `4`
- max concurrent instances is `8`

Feature flag:

- `EnableEnemyJumpLandingDustVfx`
- default true after the Tier 1/2 rollout batch
- independent from `EnableEnemyJumpTargetVfx`

Cue distinction:

- `EnemyVfxCue.JumperLandingTarget` is the pre-jump target marker at windup start.
- `EnemyVfxCue.JumperLandingDust` is the post-landing dust burst at landing completion.
- Existing `EnemyVfxCue.JumpLanding` remains generic/legacy and is not reused for this production dust binding.

Existing presenter overlap:

- the checked presenter paths are `GameplayExitPresentationController`, `GameplayFrontFaceShieldVfxPresenter`, `GameplayUtilityWindupVfxPresenter`, `BoxFlipInteractionDriver`, and `PresentationMotionTrack`; `EnemyDeathExitEffectPlanBuilder` is retained helper math, not an old playback presenter.
- these presenters do not consume `JumperLandingDust`; existing presenter migration remains out of scope.

## Player Damage Hit Burst Migration

The first existing presenter migration slice is `PlayerVfxCue.Damage` from `TickPresentationData.PlayerDamageSignals`.

Ownership:

- new path: `PlayerVfxRequestPlanner` emits one `PlayerVfxCue.Damage` request for a non-fatal `TookDamageThisTick` signal.
- old path: Player damage direct hit prefab fallback.
- suppress compatibility gates were removed in Legacy Surface Simplification.
- production flag: `GameplayVfxProductionRuntime.EnableGameplayVfxDamageBurstMigration`.

Flag policy:

- default true after the Tier 1/2 rollout batch means the Gameplay VFX lane owns playback.
- after legacy old path cleanup, setting `EnableGameplayVfxDamageBurstMigration` false disables `PlayerVfxCue.Damage` and does not restore the old presenter hit prefab.
- old presenter path is skipped regardless of the flag value.
- missing binding under the true flag is diagnostic/no-op; it must not fall back to the old presenter path.

Default binding:

- prefab: `Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/PlayerDamageBurstVfx.prefab`
- binding: `Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/PlayerDamageBurst_Binding.asset`
- host default map: `Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset`
- playback is `OneShot`, stop policy is `AuthoredDuration`, lifetime is `0.28` seconds, tail is `0.20` seconds.

Duplicate-prevention tests for this slice must cover flag off no VFX after legacy cleanup, flag on new-only, same fact not double-playing, missing binding no old fallback, and no authority/snapshot materialization impact.

## Enemy Damage VFX Lane Slice

`EnemyVfxCue.Damage` is the enemy damage hit burst slice from `TickPresentationData.EnemyDamageSignals`.

Ownership:

- new path: `EnemyVfxRequestPlanner` emits one `EnemyVfxCue.Damage` request for a non-fatal `TookDamageThisTick` signal.
- current old visual response: `EnemyAnimatorDriver` consumes the mapped enemy damage state and fires the Hit animation trigger.
- bypass: no old transient presenter bypass is added for enemy damage in this slice because current source has no enemy transient hit burst presenter path.
- production flag: `GameplayVfxProductionRuntime.EnableGameplayVfxEnemyDamageBurstMigration`.

Flag policy:

- default true after the Tier 1/2 rollout batch means the Gameplay VFX lane owns the enemy damage burst.
- true means the Gameplay VFX lane owns the enemy damage burst.
- player damage and enemy damage flags are independent.
- missing binding under the true flag is diagnostic/no-op; it must not fall back to a legacy transient presenter path.
- rollback is setting `EnableGameplayVfxEnemyDamageBurstMigration` false.

Source fact and suppression:

- source signal is `TickPresentationData.EnemyDamageSignals`.
- trigger is `TookDamageThisTick` with a positive enemy entity id.
- same-entity `TickPresentationData.EntityExitSignals` suppress the damage burst and represent the death/exit tick source fact.
- anchor is `VfxAnchor.ForEntity(entityId, VfxAnchorSlot.EntityCenter)`.
- `SourceEntityId`, `SequenceId`, and `PresentationSeed` use the damaged enemy entity id.

Default binding:

- prefab: `Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/EnemyDamageBurstVfx.prefab`
- material: `Assets/_Features/Gameplay/Gameplay_Vfx/Materials/M_EnemyDamageBurst_Red.mat`
- binding: `Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/EnemyDamageBurst_Binding.asset`
- host default map: `Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset`
- playback is `OneShot`, stop policy is `AuthoredDuration`, lifetime is `0.30` seconds, tail is `0.20` seconds, initial pool size is `4`, and max concurrent instances is `12`.

Duplicate-prevention tests for this slice cover flag filtering, missing binding diagnostic/no-op, source profile precedence, host fallback, no authority/snapshot materialization, and checked presenter files not directly referencing `EnemyVfxCue.Damage`.

## Box Exit VFX Migration

The box exit migration slice moves `BoxVfxCue.DestroySmoke`, `BoxVfxCue.DestroyShrink`, and `BoxVfxCue.ItemConsume` from old entity-exit transient playback to the Gameplay VFX lane.

Ownership:

- source fact: `TickPresentationData.EntityExitSignals`.
- destroy smoke trigger: `EntityType.Box` and `TickEntityExitCause.BoxDestroy`; the current enum alias `DestroyedByImpact = BoxDestroy` is treated as box destroy when it is not already owned by impact transient or flip impact destroy-self presentation.
- destroy shrink trigger: `EntityType.Box` and `TickEntityExitCause.BoxDestroy`; it uses the same source fact and duplicate guards as destroy smoke.
- item consume trigger: `EntityType.Box` and `TickEntityExitCause.ItemConsume`.
- non-triggers: enemy death, non-box exits, item consume for destroy smoke/shrink, and box destroy for item consume.
- destroy smoke anchor: `VfxAnchor.ForCell(signal.SourceCell, signal.Topology, VfxAnchorSlot.CellFloor)`.
- destroy shrink resolves the source exit pose through `GameplayPoseResolver` and plays a parameterized source-view clone/fallback at the source cell center.
- lifecycle: transient one-shot request with no persistent key.

Migration flags and bypass:

- `GameplayVfxProductionRuntime.EnableGameplayVfxBoxDestroySmokeMigration` gates `BoxVfxCue.DestroySmoke` playback.
- `GameplayVfxProductionRuntime.EnableGameplayVfxBoxDestroyShrinkMigration` gates `BoxVfxCue.DestroyShrink` playback and defaults true as a Tier 2 default-on candidate.
- `GameplayVfxProductionRuntime.EnableGameplayVfxItemConsumeBurstMigration` gates `BoxVfxCue.ItemConsume` playback.
- after legacy old path cleanup, old BoxDestroy shrink/fade playback is always skipped; `EnableGameplayVfxBoxDestroyShrinkMigration` false means no shrink VFX.
- `EnableGameplayVfxBoxDestroySmokeMigration` gates smoke only and does not own shrink playback.
- after legacy old path cleanup, old item consume playback is always skipped; `EnableGameplayVfxItemConsumeBurstMigration` false means no item consume VFX.
- `GameplayExitPresentationController.ApplyEntityExitOwnership()` remains active; view visibility and state cleanup are not bypassed.
- missing binding under either migration flag is diagnostic/no-op and must not fall back to old entity exit playback.

BoxDestroy composite combinations:

- smoke off / shrink off: no destroy VFX, cleanup only.
- smoke on / shrink off: `BoxVfxCue.DestroySmoke` only.
- smoke off / shrink on: `BoxVfxCue.DestroyShrink` only.
- smoke on / shrink on: `BoxVfxCue.DestroyShrink` plus `BoxVfxCue.DestroySmoke`.
- missing shrink binding, prefab, or source pose is diagnostic/no-op with no old shrink/fade fallback while the shrink flag is on.
- missing smoke binding affects smoke only and does not block shrink.

Default bindings:

- destroy smoke prefab: `Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/BoxDestroySmokeVfx.prefab`
- destroy smoke material: `Assets/_Features/Gameplay/Gameplay_Vfx/Materials/M_BoxDestroySmoke_SoftGray.mat`
- destroy smoke binding: `Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/BoxDestroySmoke_Binding.asset`
- destroy shrink prefab: `Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/BoxDestroyShrinkVfx.prefab`
- destroy shrink material: `Assets/_Features/Gameplay/Gameplay_Vfx/Materials/M_BoxDestroyShrink_Fade.mat`
- destroy shrink binding: `Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/BoxDestroyShrink_Binding.asset`
- item consume prefab: `Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/ItemConsumeBurstVfx.prefab`
- item consume material: `Assets/_Features/Gameplay/Gameplay_Vfx/Materials/M_ItemConsumeBurst_Gold.mat`
- item consume binding: `Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/ItemConsumeBurst_Binding.asset`
- host default map: `Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset`

Excluded from this slice:

- `EnemyDeathBurst` / enemy death clone motion migration.
- flip impact migration and persistent VFX.
- TileFeature, Terrain, UtilityWindup, and Shield VFX.

## Enemy Death Burst Migration

The enemy death burst migration slice adds a one-shot death burst to the Gameplay VFX lane. It is not the owner of the legacy enemy view clone/arc/fade fly-away replacement.
The old clone/arc/fade enemy death fly-away is now owned by the separate Enemy Death Motion migration.

Ownership:

- source fact: `TickPresentationData.EntityExitSignals`.
- trigger: `EntityType.Unit` and `TickEntityExitCause.Killed` / `TickEntityExitCause.EnemyDeath`.
- non-triggers: box destroy, item consume, enemy damage without exit, player damage, and non-unit exits.
- anchor: `VfxAnchor.ForCell(signal.SourceCell, signal.Topology, VfxAnchorSlot.CellCenter)`.
- anchor rationale: the exit tick may no longer have a stable live entity pose, while `SourceCell` is carried by the exit presentation fact.
- lifecycle: transient one-shot request with no persistent key.

Migration flag and bypass:

- `GameplayVfxProductionRuntime.EnableGameplayVfxEnemyDeathBurstMigration` gates `EnemyVfxCue.Death` playback.
- `EnableGameplayVfxEnemyDeathBurstMigration` does not suppress the old `GameplayExitPresentationController` enemy death exit VFX playback.
- `GameplayExitPresentationController.ApplyEntityExitOwnership()` remains active; view visibility and state cleanup are not bypassed.
- missing burst binding under the migration flag is diagnostic/no-op and does not affect old enemy death clone/arc/fade playback unless the Death Motion flag is also on.

Default binding:

- prefab: `Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/EnemyDeathBurstVfx.prefab`
- material: `Assets/_Features/Gameplay/Gameplay_Vfx/Materials/M_EnemyDeathBurst_DarkRed.mat`
- binding: `Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/EnemyDeathBurst_Binding.asset`
- host default map: `Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset`
- playback is `OneShot`, stop policy is `AuthoredDuration`, lifetime is `0.35` seconds, tail is `0.25` seconds, initial pool size is `4`, and max concurrent instances is `8`.

Visual parity:

- v1 is a burst augmentation.
- clone-motion parity is owned by `Enemy Death Motion VFX Migration`.

Excluded from this slice:

- box destroy and item consume migration, already covered by `Box Exit VFX Migration`.
- flip impact migration, persistent death VFX, utility windup migration, and shield migration.
- `TickPresentationData`, `TickPipeline`, `WorldState`, and `WorldSnapshot` shape or authority changes.

## Enemy Death Motion VFX Migration

Enemy Death Motion moves the legacy enemy death fly-away clone/fade visual to parameterized Gameplay VFX while keeping `EnemyVfxCue.Death` as a separate one-shot burst.

Ownership:

- source fact: `TickPresentationData.EntityExitSignals`.
- trigger: `EntityType.Unit` and `TickEntityExitCause.Killed` / `TickEntityExitCause.EnemyDeath`.
- cue: `EnemyVfxCue.DeathMotion`.
- adapter: `EnemyDeathMotionVfxCommand` to `ParameterizedMotionVfxCommand`.
- source pose: `GameplayPoseResolver.TryResolveEntityExitSignalLocalPose(...)` from `SourceCell`, `Topology`, `Facing`, and `EntityType`.
- target pose: legacy `EnemyDeathExitEffectPlanBuilder` camera near-plane target, using output camera and presentation local-space root only.
- player/source actor pose is optional camera-plane bias and is read from presentation state, not authoritative world state.

Legacy motion spec locked for parity:

- duration: `GameplayTimingProfile.EnemyDeathEffectDurationSeconds`.
- target: camera-forward near-plane fly-away with seeded plane jitter.
- optional target bias: player pose first, then `SourceActorEntityId` pose.
- arc: seeded `0.1..0.2` cells along camera-up local direction.
- spin: seeded signed `240..420` degrees around camera-forward local direction.
- position curve: ease-out cubic source to target plus `sin(t*pi)` arc offset.
- fade: starts at normalized time `0.12`, lasts the remaining `0.88`, alpha is `1 - fadeT^2`.
- scale: uniform `1.0 -> 0.88`.

Runtime policy:

- `GameplayVfxProductionRuntime.EnableGameplayVfxEnemyDeathMotionMigration` gates `EnemyVfxCue.DeathMotion`.
- suppress compatibility gates were removed in Legacy Surface Simplification; no enemy death old fallback switch remains.
- `EnableGameplayVfxEnemyDeathBurstMigration` and `EnableGameplayVfxEnemyDeathMotionMigration` are independent.
- flag combinations:
  - burst off / motion off: no enemy death VFX.
  - burst on / motion off: `EnemyVfxCue.Death` burst only.
  - burst off / motion on: `EnemyVfxCue.DeathMotion` only.
  - burst on / motion on: `EnemyVfxCue.DeathMotion` plus `EnemyVfxCue.Death`.
- missing DeathMotion binding, prefab, source pose, output camera, or target context is diagnostic/no-op with no old fly-away fallback.
- missing source clone uses the fallback prefab through `SourceViewCloneWithPrefabFallback`.
- `GameplayExitPresentationController.ApplyEntityExitOwnership()` remains active; source view cleanup is not bypassed.

Default binding:

- prefab: `Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/EnemyDeathMotionVfx.prefab`
- material: `Assets/_Features/Gameplay/Gameplay_Vfx/Materials/M_EnemyDeathMotion_Fade.mat`
- binding: `Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/EnemyDeathMotion_Binding.asset`
- host default map: `Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset`
- playback is parameterized one-shot, stop policy is `AuthoredDuration`, authored lifetime is `0`, tail is `0.2` seconds, initial pool size is `4`, and max concurrent instances is `8`.

Boundaries:

- no `TickPipeline`, `WorldState`, `WorldSnapshot`, `ProjectedWorld`, `FinalizationBatch`, or `DeterminismHashBuilder` changes.
- no `TickPresentationData` or `TickEntityExitPresentationSignal` shape change.
- no gameplay death rule, cleanup rule, camera rig, projectile trail, unit movement trail, TileFeature, Terrain, or stage content changes.

## Utility Windup VFX Migration

The utility windup migration slice moves summon utility windup warning playback from `GameplayUtilityWindupVfxPresenter` to the Gameplay VFX lane as persistent desired state.

Ownership:

- source fact: `TickPresentationData.SummonWindupWarnings`.
- active desired state: each `TickSummonWindupWarningSignal` present in the current tick.
- end, cancel, and source death/exit: the signal is absent or suppressed by same-tick `EntityExitSignals`, so the persistent desired key is absent and the registry stops the handle.
- cue: `EnemyVfxCue.UtilityWindup`.
- anchor: source entity center with source-cell fallback, matching the legacy source-view-attached warning default.
- lifecycle: persistent request with `VfxPersistentKey` built from cue, entity anchor kind, source entity id, effect index, and activation sequence.

Migration flag and bypass:

- `GameplayVfxProductionRuntime.EnableGameplayVfxUtilityWindupMigration` gates `EnemyVfxCue.UtilityWindup` playback and defaults true after the Tier 1/2 rollout batch.
- after legacy old path cleanup, old `GameplayUtilityWindupVfxPresenter` summon warning spawning is always skipped.
- suppress compatibility gates were removed in Legacy Surface Simplification.
- the coordinator still calls a cleanup-only empty refresh so legacy summon warning instances cannot linger.
- missing binding under the migration flag is diagnostic/no-op and must not fall back to the old presenter.
- setting `EnableGameplayVfxUtilityWindupMigration` false disables `EnemyVfxCue.UtilityWindup` and does not restore old warning spawning.

Default binding:

- prefab: `Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/EnemyUtilityWindupTelegraphVfx.prefab`
- material: `Assets/_Features/Gameplay/Gameplay_Vfx/Materials/M_EnemyUtilityWindupTelegraph_Amber.mat`
- binding: `Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/EnemyUtilityWindupTelegraph_Binding.asset`
- host default map: `Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset`
- playback is `Loop`, stop policy is `StopEmittingThenRelease`, tail is `0.30` seconds, initial pool size is `4`, and max concurrent instances is `8`.

Boundaries:

- no one-shot fallback is allowed for this cue.
- no MotionTrack anchor, Screen/BoardLocal anchor, topology queue, stage map, TileFeature, Terrain, or full FlipImpact MotionTrack migration is included.
- `TickPresentationData`, `TickPipeline`, `WorldState`, and `WorldSnapshot` shape or authority changes remain forbidden.

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

## ImpactTransient Break VFX Migration

ImpactTransient Break migrates a dormant/reserved source-to-impact break/fade hook from old transient presenter playback to the Gameplay VFX lane.

Source fact:

- `TickPresentationData.ImpactTransientSignals`
- signal fields are `EntityId`, `EntityType`, `SourceCell`, `ImpactCell`, `Topology`, `Facing`, and `PresentationSeed`
- normal `TickResultBuilder.BuildImpactTransientPresentation` still clears the list; no gameplay producer is added

Cue and playback:

- cue: `BoxVfxCue.ImpactTransientBreak`
- trigger: valid `TickImpactTransientPresentationSignal` with `EntityType.Box`
- lifecycle: transient one-shot, no persistent key
- command: `ParameterizedMotionVfxCommand`
- playback: source-view clone with prefab fallback
- motion: source pose to impact pose using `ParameterizedMotionVfxSamplerMode.FlipArc`
- fade: `ScaleAndAlpha`, break start at normalized `0.62`, duration `max(BoxDestroyEffectDurationSeconds, FlipMotionDurationSeconds)`

Duplicate relationship:

- existing `BoxVfxExitSignalGuards` still treats ImpactTransient as the owner of same-entity box destroy visuals.
- a same-entity BoxDestroy exit does not also emit duplicate BoxDestroy smoke/shrink when an ImpactTransient signal owns the break/fade.

Flag and fallback:

- flag: `EnableGameplayVfxImpactTransientBreakMigration`
- default true
- flag off means no ImpactTransient break VFX and no old presenter fallback
- old `GameplayTransientEffectPresenter.PlayImpactBreakEffect` playback surface is removed
- missing binding, prefab, anchor, source pose, or impact pose is diagnostic/no-op
- missing binding no fallback applies to this reserved hook

## OutOfBounds Exit VFX Migration

`TickEntityExitCause.OutOfBounds` remains a dormant/reserved entity exit cause. Current normal gameplay pipeline code does not produce an OutOfBounds exit signal, but synthetic tests, tools, or future custom code can still inject `TickEntityExitPresentationSignal` with `OutOfBounds`.

Source fact:

- `TickPresentationData.EntityExitSignals`
- trigger: `TickEntityExitCause.OutOfBounds`
- signal fields include `ExitedEntityId`, `EntityType`, `SourceCell`, `Topology`, `Facing`, `SourceActorEntityId`, optional `AnchorEntityId`, `PresentationSeed`, and `ExitCause`
- no OutOfBounds gameplay producer is added
- `TickEntityExitCause.OutOfBounds` is not deleted or deprecated in this slice

Cue mapping:

- `EntityType.Box` -> `BoxVfxCue.OutOfBoundsExit`
- `EntityType.Unit` -> `EnemyVfxCue.OutOfBoundsExit`
- no `PlayerVfxCue.OutOfBoundsExit` is added because the carrier has no player-vs-enemy unit role field
- unsupported entity types are diagnostic/no-op

Playback:

- lifecycle: transient one-shot, no persistent key
- command: `ParameterizedMotionVfxCommand`
- playback: source-view clone with prefab fallback
- motion: source pose to source pose using `ParameterizedMotionVfxSamplerMode.Linear`
- fade: `DestroyShrinkEase` over `ItemConsumeEffectDurationSeconds`
- anchor: source cell center

Flag and fallback:

- flag: `EnableGameplayVfxOutOfBoundsExitMigration`
- default true
- flag off means no OutOfBounds VFX and no old presenter fallback
- old `GameplayExitPresentationController.PlayExitEffect` playback surface is removed for OutOfBounds
- missing binding, prefab, anchor, or source pose is diagnostic/no-op
- missing binding no fallback applies to this reserved hook
- if no normal producer exists, tests use synthetic presentation facts

## Gameplay VFX Legacy Old Path Cleanup

After runtime default-on and legacy finalization, all current Gameplay VFX migration cues use the Gameplay VFX lane as the canonical playback path. For all current migrated cues, flag off means that VFX is off; it does not mean old presenter fallback. Cleanup, visibility, transform reset, and motion ownership responsibilities remain in their existing presentation owners.

Cleaned legacy direct playback:

| Old path | New VFX | Cleanup status | Flag-off semantics | Notes |
|---|---|---|---|---|
| Player damage direct hit prefab fallback | `PlayerVfxCue.Damage` | old hit playback disabled | no damage VFX | direct hit prefab fallback removed |
| BoxDestroy old entity exit transient track | `BoxVfxCue.DestroyShrink` + `BoxVfxCue.DestroySmoke` | old shrink/fade track removed; `ApplyEntityExitOwnership()` retained | shrink flag off disables shrink; smoke flag off disables smoke | smoke does not own shrink suppression |
| ItemConsume old entity exit transient track | `BoxVfxCue.ItemConsume` | old consume fade track removed; `ApplyEntityExitOwnership()` retained | no item consume VFX | cleanup remains exit ownership |
| `GameplayUtilityWindupVfxPresenter.RefreshSummonWarnings` | `EnemyVfxCue.UtilityWindup` | old spawn disabled; cleanup-only empty refresh retained | no utility windup VFX | presenter kept for legacy instance disposal |
| `GameplayFrontFaceShieldVfxPresenter.RefreshActiveSources` | `EnemyVfxCue.FrontFaceShieldActive` | old active-loop spawn disabled; cleanup-only empty refresh retained | no active shield VFX | active loop old fallback removed |
| `GameplayFrontFaceShieldVfxPresenter.PlayBlockBursts` | `EnemyVfxCue.FrontFaceShieldBlock` | old block burst disabled | no block burst VFX | one-shot old fallback removed |
| `GameplayFrontFaceShieldVfxPresenter.RefreshWindupWarnings` | `EnemyVfxCue.FrontFaceShieldWindup` | old telegraph spawn disabled; cleanup-only empty refresh retained | no FrontFace shield windup VFX | `telegraphPrefab` and old telegraph assets retained for deferred cleanup |
| enemy killed old entity exit transient track | `EnemyVfxCue.DeathMotion` + `EnemyVfxCue.Death` | old fly-away track removed; `ApplyEntityExitOwnership()` retained; `EnemyDeathExitEffectPlanBuilder` retained for DeathMotion target math | no death motion VFX when motion flag is off; no burst VFX when burst flag is off | death flags control new VFX playback only |
| old flip destroy-self clone/fade transient track | `BoxVfxCue.FlipDestroySelfMotion` | old clone/fade track removed; DestroySelf entity membership bookkeeping retained | no flip destroy-self motion VFX | `PresentationMotionTrack` Stay branch remains unchanged |
| old impact break transient track | `BoxVfxCue.ImpactTransientBreak` | old impact break playback removed; duplicate ownership retained | no ImpactTransient break VFX | no normal producer added |
| OutOfBounds old entity exit transient track | `BoxVfxCue.OutOfBoundsExit` / `EnemyVfxCue.OutOfBoundsExit` | old OutOfBounds fade track removed; `ApplyEntityExitOwnership()` retained | no OutOfBounds VFX | dormant/reserved hook only; no producer added |

No stale old transient playback fallback remains: `GameplayTransientEffectPresenter`, `ImpactBreakEffectTrack`, `EntityExitEffectTrack`, `PlayImpactBreakEffect`, and `PlayExitEffect` playback APIs are removed.

## Active Transient Effect Count Cleanup

The old `GameplayTransientEffectPresenter` and its transient tracks were removed. `ActiveTransientEffectCount` compatibility surface was removed with them; old transient activity is no longer a public presentation concept and does not participate in presentation phase decisions.

Current Gameplay VFX runtime activity must not be mapped into the removed property. If runtime VFX diagnostics need a public count later, introduce a separate API such as `GameplayVfxRuntimeDiagnostics`; that future API must not reuse the old property name or old transient activity vocabulary.

Retained cleanup and motion responsibilities remain:

- `ApplyEntityExitOwnership`
- `EnemyDeathExitEffectPlanBuilder`
- `GameplayTransientEffectTrackUtility.SafeDestroy`
- `PresentationMotionTrack` Stay original-view motion
- `BoxFlipInteractionDriver`

Remaining old canonical presentation responsibilities:

| Old path | New VFX | Cleanup status | Flag-off semantics | Notes |
|---|---|---|---|---|
| `BoxFlipInteractionDriver` and `PresentationMotionTrack` Stay branch | none | retained | not a VFX fallback | transform/grip/reset and motion sampling ownership remain |

Legacy Surface Simplification removed the suppress compatibility gates and the `IGameplayPresentationMigrationGate` interface. Cleaned and high-risk old presenter fallbacks remain absent; no compatibility alias now defines rollback to an old presenter path. In this document, old fallback = none for all current migrated Gameplay VFX cues.

Serialized reference cleanup status:

- removed fields: `EntityEffectPresentationAuthoring.hitVfxPrefab`, `EntityEffectPresentationAuthoring.deathVfxPrefab`, `EnemyUtilityWindupPresentationAuthoring.summonWindupWarningPrefab`, `EnemyFrontFaceShieldPresentationAuthoring.activeLoopPrefab`, and `EnemyFrontFaceShieldPresentationAuthoring.blockBurstPrefab`.
- retained fields: `telegraphPrefab`, `deathViewTailSeconds`, timing overrides, ownership mode, and death anchor.
- removed-field YAML residue is cleaned for the active/block FrontFaceShield prefab references and stale null hit/death prefab keys.
- old FrontFaceShield active/block prefab and material assets were removed after GUID reference scans confirmed zero external references. The active/block replacements are `FrontFaceShieldActiveVfx` and `FrontFaceShieldBlockVfx` in the current Gameplay VFX lane.
- `VFX_FrontFaceShield_Telegraph` and `M_FrontFaceShield_Telegraph.mat` are retained for a deferred FrontFaceShield windup serialized reference cleanup and old telegraph asset removal slice after GUID reference scans confirm zero required references.

Authority and carrier boundaries remain unchanged: no `TickPipeline`, `WorldState`, `WorldSnapshot`, `ProjectedWorld`, `FinalizationBatch`, `DeterminismHashBuilder`, `TickPresentationData`, `TickEntityExitPresentationSignal`, `TickEntityMotion`, or `TickResultBuilder` changes are part of legacy old path cleanup.

## Gameplay VFX Flag Rollout Policy

Every current Gameplay VFX flag is a long-term default-true candidate once its targeted tests pass. Tier 1 and Tier 2 flags are default-on after the Tier 1/2 rollout batch, based on targeted tests, visual spot check/manual visual approval, no missing binding / missing anchor / missing source pose / missing target context diagnostics in target scenes, no double-play with legacy presenters, successful targeted VFX regression tests, and a rollback path through the same flag. Tier 3 flags are approved in the Tier 3 rollout batch after parity review.

After Tier 3 rollout, all current Gameplay VFX flags are runtime default-on. Scene-local overrides may still opt out. Flag off disables that VFX and does not restore old presenter fallback.

Migration flags own only new Gameplay VFX lane playback. When a migration flag is on and its binding, prefab, anchor, source pose, or target context is missing, the new path reports diagnostic/no-op and does not fall back to the old presenter path. After legacy old path cleanup, all current migrated cue flags use canonical/off semantics: flag off disables that VFX and does not restore old presenter fallback.

Augmentation flags do not own legacy fallback or suppress gates. They may add Gameplay VFX lane playback alongside existing presentation behavior, but missing binding remains diagnostic/no-op and must not create a new old-path ownership rule. `EnableGameplayVfxBoxSlideTrail`, enemy damage burst, and enemy jump cue flags have no legacy suppress gate.

High-risk parameterized motion and clone/source-view VFX required manual parity approval before default-on rollout. `EnableGameplayVfxEnemyDeathBurstMigration`, `EnableGameplayVfxEnemyDeathMotionMigration`, and `EnableGameplayVfxFlipDestroySelfMotionMigration` are approved in the Tier 3 rollout batch. Their old presenter fallbacks are finalized and removed; the flags now control only new VFX playback.

Scene-local overrides are separate from runtime defaults:

- `Assets/Scenes/CombinedGameplayShowcase.unity` is a jump VFX visual review scene override with `EnableEnemyJumpTargetVfx` and `EnableEnemyJumpLandingDustVfx` on; Tier 3 flags use runtime default-on for broad VFX review unless explicitly added for rollback review.
- `Assets/Scenes/UIAudioScene.unity` is an explicit visual review scene override with all current Gameplay VFX flags on; this is not production default policy and covers documented high-risk flag combinations for review only.
- `Assets/Scenes/TutorialScene.unity` remains production-safe/off for Gameplay VFX flags through explicit scene-local false overrides.

Enemy death legacy fallback is finalized. Suppress compatibility gates were removed. `EnableGameplayVfxEnemyDeathBurstMigration` controls only `EnemyVfxCue.Death`, and `EnableGameplayVfxEnemyDeathMotionMigration` controls only `EnemyVfxCue.DeathMotion`. Both enemy death Tier 3 flags are runtime default-on after the Tier 3 rollout batch. Burst + Motion simultaneous output remains visually monitored. The supported combinations are:

- burst off / motion off: no enemy death VFX.
- burst on / motion off: `EnemyVfxCue.Death` only.
- burst off / motion on: `EnemyVfxCue.DeathMotion` only.
- burst on / motion on: `EnemyVfxCue.DeathMotion` plus `EnemyVfxCue.Death`.
- missing DeathMotion binding, prefab, source pose, output camera, or target context is diagnostic/no-op with no old fly-away fallback.

FlipDestroySelf legacy fallback is finalized. Suppress compatibility gates were removed. `EnableGameplayVfxFlipDestroySelfMotionMigration` controls only `BoxVfxCue.FlipDestroySelfMotion`; flag off means no flip destroy-self motion VFX and no old clone/fade fallback.

Box destroy suppress ownership is cleaned up. Old BoxDestroy shrink/fade playback is disabled independently of `EnableGameplayVfxBoxDestroyShrinkMigration`. `EnableGameplayVfxBoxDestroySmokeMigration` gates smoke only and does not own shrink playback. The shrink flag is runtime default-on as a Tier 2 candidate; setting it false now means no shrink VFX.

| Flag | Cue | Type | Actual Default | Tier | Candidate | Approval Gate |
|---|---|---|---|---|---|---|
| `EnableGameplayVfxDamageBurstMigration` | `PlayerVfxCue.Damage` | Migration | True | Tier 1 | Yes | targeted tests + visual spot check |
| `EnableGameplayVfxEnemyDamageBurstMigration` | `EnemyVfxCue.Damage` | Augmentation-style VFX lane | True | Tier 1 | Yes | targeted tests + visual spot check |
| `EnableGameplayVfxBoxDestroySmokeMigration` | `BoxVfxCue.DestroySmoke` | Migration | True | Tier 1 | Yes | targeted tests + visual spot check |
| `EnableGameplayVfxBoxDestroyShrinkMigration` | `BoxVfxCue.DestroyShrink` | Migration / parameterized clone motion | True | Tier 2 | Yes | manual visual approval + targeted regression |
| `EnableGameplayVfxItemConsumeBurstMigration` | `BoxVfxCue.ItemConsume` | Migration | True | Tier 1 | Yes | targeted tests + visual spot check |
| `EnableEnemyJumpLandingDustVfx` | `EnemyVfxCue.JumperLandingDust` | Augmentation | True | Tier 1 | Yes | targeted tests + visual spot check |
| `EnableGameplayVfxBoxSlideTrail` | `BoxVfxCue.SlideDustTrail` | Augmentation / parameterized motion | True | Tier 1 | Yes | targeted tests + density visual spot check |
| `EnableGameplayVfxImpactTransientBreakMigration` | `BoxVfxCue.ImpactTransientBreak` | Migration / reserved parameterized clone motion | True | Tier 2 | Yes | manual visual approval + targeted reserved-hook regression |
| `EnableGameplayVfxOutOfBoundsExitMigration` | `BoxVfxCue.OutOfBoundsExit / EnemyVfxCue.OutOfBoundsExit` | Migration / reserved parameterized clone motion | True | Tier 2 | Yes | manual visual approval + targeted reserved-hook regression |
| `EnableEnemyJumpTargetVfx` | `EnemyVfxCue.JumperLandingTarget` | Augmentation | True | Tier 2 | Yes | manual visual approval + targeted regression |
| `EnableGameplayVfxUtilityWindupMigration` | `EnemyVfxCue.UtilityWindup` | Migration | True | Tier 2 | Yes | manual visual approval + targeted regression |
| `EnableGameplayVfxFrontFaceShieldActiveMigration` | `EnemyVfxCue.FrontFaceShieldActive` | Migration | True | Tier 2 | Yes | manual visual approval + targeted regression |
| `EnableGameplayVfxFrontFaceShieldBlockMigration` | `EnemyVfxCue.FrontFaceShieldBlock` | Migration | True | Tier 2 | Yes | manual visual approval + targeted regression |
| `EnableGameplayVfxFrontFaceShieldWindupMigration` | `EnemyVfxCue.FrontFaceShieldWindup` | Migration | True | Tier 2 | Yes | manual visual approval + targeted regression |
| `EnableGameplayVfxFlipImpactBurstMigration` | `BoxVfxCue.FlipImpactBurst` | Migration | True | Tier 2 | Yes | manual visual approval + targeted regression |
| `EnableGameplayVfxEnemyDeathBurstMigration` | `EnemyVfxCue.Death` | Migration burst | True | Tier 3 | Yes | approved in Tier 3 rollout batch; requires post-rollout visual monitoring + rollback review |
| `EnableGameplayVfxEnemyDeathMotionMigration` | `EnemyVfxCue.DeathMotion` | Migration / parameterized motion | True | Tier 3 | Yes | approved in Tier 3 rollout batch; requires post-rollout visual monitoring + rollback review |
| `EnableGameplayVfxFlipDestroySelfMotionMigration` | `BoxVfxCue.FlipDestroySelfMotion` | Migration / parameterized clone motion | True | Tier 3 | Yes | approved in Tier 3 rollout batch; requires post-rollout visual monitoring + rollback review |

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
