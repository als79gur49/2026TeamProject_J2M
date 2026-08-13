# Topology-View-Camera Canonical Ownership 2026-04-24

This document is the slice-local supporting truth-source for the current topology/view/camera runtime ownership map. It closes the completed cleanup as a documentation and governance step; it is not a new refactor plan.

## Overview

- The topology-view-camera cleanup is closed at the current structure.
- This document fixes canonical ownership for the runtime contracts, camera runtime, shake runtime, post-fx runtime, bootstrap/helper lanes, and remaining scaffold surface.
- M0 camera-output contract locking narrows `GameplayCameraRig` to a semantic-free additive-pose port without changing shake tuning, post-fx tuning, or scene/prefab authoring.

## Final ownership map

| Surface | Canonical owner | Locked responsibility |
| --- | --- | --- |
| `TopologyTransitionVisualState` | runtime contract | shared topology-transition visual state contract |
| `GameplayPresentationPhase` | runtime contract | presentation-phase enum contract |
| `TopologyRotationVisualMapping` | runtime contract | topology-rotation mapping contract |
| `CameraDistanceMode` | runtime contract | camera-distance ownership contract |
| `IGameplayCameraAdditivePosePort` | camera runtime contract | semantic-free absolute local additive position/rotation apply and identity reset |
| `GameplayCameraRig` | camera runtime | authored baseline capture/use, unshaken pose resolution, single final additive-effects-root write, and direct camera pose apply |
| `GameplayTickViewPresenter` | presentation runtime | topology evaluator/profile ownership, additive-pose handoff, and pause/disable/destroy/rig-replacement reset |
| `TopologyTransitionCameraShakeProfile` | shake runtime | pure authored shake profile |
| `TopologyTransitionCameraShakeController` | shake runtime | topology-transition shake evaluator |
| `TopologyTransitionCameraShakeResult` | shake runtime | pure helper value object for local position/rotation deltas |
| `TopologyTransitionPostFxProfile` | post-fx runtime | pure authored post-fx profile |
| `TopologyTransitionDistortionProfile` | post-fx runtime | co-located authored distortion profile |
| `TopologyTransitionPostFxController` | post-fx runtime | URP runtime adapter, runtime volume clone owner, and authoritative source-profile immutability owner |
| `GameplayResolvedCameraStartupPlan` | startup-plan lane | internal resolved startup meaning payload |
| `GameplayCameraStartupPlanComposer` | startup-plan lane | one-time startup resolution composer |
| `GameplayHostTopologyVisualRuntimeBootstrap` | bootstrap/helper lane | host-side attach/wiring glue that consumes resolved startup data |
| `GameplayShowcaseSceneCameraBootstrap` | bootstrap/helper lane | scene camera bootstrap glue for capture/wiring only |
| `GameplayShowcaseSceneScaffold` | remaining scaffold | scene orchestration, board-root ensure/find, legacy-label cleanup, and wrapper-only `ConfigureDefaultSceneCamera(...)` |

## Runtime slices

- Contracts stay file-local to the `Gameplay_Host/Runtime/Contracts` slice and remain the shared vocabulary between presenter, host wiring, camera runtime, and post-fx runtime.
- `GameplayCameraRig` is the only runtime owner that may combine authored baseline capture, presented orbit resolution, hierarchy pose application, cached semantic-free additive pose application, and direct camera pose application into one final camera-output pipeline.
- `CameraPoseRoot` remains the unshaken base-pose owner and `CameraEffectsRoot` remains the additive-pose target. Additive application is absolute per frame and is never recaptured as a new base pose.
- Shake runtime stays split into data, evaluator, and pure result value:
  - `TopologyTransitionCameraShakeProfile` is authored data only.
  - `TopologyTransitionCameraShakeController` evaluates shake envelopes from `TopologyTransitionVisualState`.
  - `TopologyTransitionCameraShakeResult` carries the computed pose delta only.
  - `GameplayTickViewPresenter` passes that result through `IGameplayCameraAdditivePosePort`; `GameplayCameraRig` does not depend on the topology evaluator, profile, or visual-state contract.
- Pause, presenter disable/destroy, hard cleanup, scene teardown, rig detach/replacement, and rig disable/destroy reset the additive pose to zero position and identity rotation without advancing topology presentation time.
- Post-fx runtime stays split into authored data and runtime adapter:
  - `TopologyTransitionPostFxProfile` and `TopologyTransitionDistortionProfile` hold authored runtime inputs.
  - `TopologyTransitionPostFxController` owns runtime clone creation, output-camera post-processing enablement, profile application, and source-asset immutability preservation.
- Current guardrails across phases `1A` through `7B-A` are locked by the existing architecture test families rather than by prose alone:
  - `TopologyVisualContractsExtractionArchitectureTests`
  - `CameraDistanceModeOwnershipArchitectureTests`
  - `TopologyTransitionCameraShakeProfileExtractionArchitectureTests`
  - `TopologyTransitionCameraShakeControllerExtractionArchitectureTests`
  - `TopologyTransitionPostFxProfileExtractionArchitectureTests`
  - `TopologyTransitionPostFxControllerExtractionArchitectureTests`
  - `GameplayHostRuntimeFactoryBootstrapCleanupArchitectureTests`
  - `GameplayShowcaseSceneScaffoldBootstrapCleanupArchitectureTests`
  - `GameplayCameraRigFinalCleanupArchitectureTests`
  - `GameplayShowcaseSceneScaffoldFinalCleanupArchitectureTests`

## Bootstrap/helper lanes

- `GameplayHostTopologyVisualRuntimeBootstrap` is the host-side glue owner only. It consumes `GameplayResolvedCameraStartupPlan`, configures the topology shake profile on the presenter, wires the semantic-free camera rig, presenter camera runtime attachment, and post-fx controller attachment, and does not perform a second startup resolution pass.
- `GameplayShowcaseSceneCameraBootstrap` is the showcase-scene glue owner only. It wires the Cinemachine path, captures authored scene camera pose, applies cut-blend defaults, and does not own startup settings application, shake handoff, or output-camera post-processing enablement.
- `GameplayShowcaseSceneScaffold` remains above both helpers as the scene orchestrator. Its responsibilities are limited to installer-scaffold orchestration, board-root ensure/find, legacy world-label cleanup, and wrapper-only `ConfigureDefaultSceneCamera(...)`.

## Startup Path

- Startup meaning is resolved once through the internal `GameplayResolvedCameraStartupPlan`.
- `GameplayCameraStartupPlanComposer` in `GameplayHostRuntimeFactory` is the canonical startup composition point for both installer-driven startup and direct `GameplaySceneHost.Initialize(...)` callers.
- `GameplayShowcaseSceneCameraBootstrap` captures and wires scene camera state only.
- `GameplayHostTopologyVisualRuntimeBootstrap` consumes resolved startup data only.
- `GameplayCameraRig` remains the final apply owner.
- Startup sign-off stays touched-cluster no-new-regression against same-revision baseline context, not full-suite green.

## Authoring And Preset Authority

- `GameplayCameraTopologyAuthoring` is the scene-local authority entrypoint.
- `GameplayCameraTopologyPreset` owns stage-scoped shared tuning only.
- `GameplayCameraTopologySharedTuning` is the canonical shared-tuning schema for camera topology.
- `GameplayCameraSettings` is shared tuning only.
- `GameplayCameraBaselineAuthoringPolicy` is the dedicated scene-local authored-baseline policy type.
- `inlineSharedTuning` on `GameplayCameraTopologyAuthoring` is the explicit inline-mode field using the canonical shared-tuning schema.
- `configureMainCamera` remains local.
- `UseAuthoredSceneCameraPose/Lens` live in scene-local baseline policy, not `GameplayCameraSettings`.
- `Preset` mode ignores inline shared-tuning values for runtime snapshot resolution.
- Candidate A is complete:
  - authored-baseline usage flags no longer live in `GameplayCameraSettings`
  - scene-local baseline policy is carried explicitly through authoring snapshot, host configuration, bootstrap glue, and rig resolution
  - preset `cameraSettings` content remains shared tuning only inside the canonical `sharedTuning` block
- Candidate B is complete:
  - non-authoritative inline shared tuning no longer lives as five root fields on `GameplayCameraTopologyAuthoring`
  - scene-local authoring serializes one explicit `inlineSharedTuning` block for `Inline` mode only, using the canonical shared schema
  - `GameplayCameraTopologyPreset` serializes one explicit `sharedTuning` block using the same canonical shared schema
  - `Preset` mode inspector UX hides the inline lane and keeps the referenced preset as the only authoritative shared-tuning source
  - the temporary `GameplayCameraTopologySceneMigrationTool` has been removed because this schema is now canonical

## Explicit non-ownership

- scaffold is not camera-bootstrap behavior owner
- rig is not shake/post-fx math owner and does not know topology/gameplay shake semantics
- post-fx controller is not gameplay authority owner
- bootstrap helpers are glue owners, not behavior owners
- sign-off is phase-local no-new-regression, not full-suite green
- this slice does not treat wrapper-only scene utilities as justification to move camera/bootstrap behavior back into scaffold code
- this slice does not treat runtime controller extraction as permission to redesign authored profile semantics

## Validation/sign-off rule

- `Topology-view-camera cleanup phases are signed off by touched-cluster no-new-regression against same-revision baseline context, not by full-suite green.`
- `Lane A live ledger is the baseline context for remaining touched-cluster failures.`
- `Only new failure rows or changed failure shapes count against closure.`
- The closure evidence for this slice is bounded to the current revision guardrails and touched-cluster readout:
  - `TestResults/phase7b-a-scaffold-final-cleanup.xml` (`4/4` passed)
  - `TestResults/phase7b-a-touched-cluster.xml` (`137 total / 3 failed / 134 passed`)
  - `TestResults/phase6a-targeted-editmode.xml` for prior same-revision comparison
  - `TestResults/wsl-dotnet-full.log` as the latest available supporting build artifact

## Remaining carryover reds

| Row | Current shape | Prior current-workstream shape | Historical note | Classification | Closure disposition |
| --- | --- | --- | --- | --- | --- |
| `RuntimeBoardBoundsGuardTests.GameplaySceneHost_AutoCreateViewsFalse_UsesConfiguredPlayerControlTiming` | `Expected: True / But was: False` | same shape in `phase6a-targeted-editmode.xml` | same row existed in Lane A baseline context | `pre-existing red` | `Carryover accepted for topology-view-camera closure` |
| `RuntimeBoardBoundsGuardTests.GameplaySceneHost_Initialize_UsesConfiguredPlayerControlTiming_WhenPlayerPrefabHasAnimationTimingAuthoringOnly` | `Expected: True / But was: False` | same shape in `phase6a-targeted-editmode.xml` | same row existed in Lane A baseline context | `pre-existing red` | `Carryover accepted for topology-view-camera closure` |
| `TopologyTransitionPostFxTests.GameplaySceneHost_Initialize_TopologyTransitionPostFx_CreatesRuntimeVolumeCloneFromAuthoritativeAsset` | `Expected: Low / But was: High` | same shape in `phase6a-targeted-editmode.xml` | older full-lane context used `Expected: 1.0f / But was: 1.04999995f`, so historical shape drift is explicit | `pre-existing red` within the current revision window | `Carryover accepted for topology-view-camera closure` |

- No bounded reproducer is required for closure because the current revision already has a stable same-shape confirmation for all three rows across the two current-workstream artifacts.
- If any future same-revision rerun introduces a fourth row or materially changes one of the three shapes above, that row must be reopened as no longer accepted carryover.

## M1 generic impulse and common mixer foundation

M1 adds a bounded presentation-only camera-feedback lane without changing the M0 topology waveform or camera execution ownership.

```text
TopologyTransitionVisualState
  -> TopologyTransitionCameraShakeController
  -> TopologyCameraShakeContributionAdapter
                                      \
                                       -> GameplayCameraShakeMixer
                                      /   -> IGameplayCameraAdditivePosePort
CameraShakeImpulseRequest            /    -> GameplayCameraRig
  -> CameraShakeImpulseEvaluator ---/     -> Direct / Cinemachine hierarchy
```

- Topology remains a progress-driven continuous evaluated source. It is never converted into a gameplay impulse request.
- Gameplay uses discrete `CameraShakeImpulseRequest` values with canonical identity from tick, semantic, source entity, and sequence/action-plan fields. There is no caller-defined request key and no request-local delay.
- Exact contact timing remains owned by the feature presentation track that submits the request. M1 connects no Push, Flip, Damage, lethal, or enemy-jump production semantic.
- `GameplayCameraShakeMixer` owns active gameplay impulse time, exact-identity dedupe, semantic/source cooldown, canonical ordering, priority residual policy, topology overlap suppression, vector-magnitude saturation, runtime motion level, and lifecycle reset.
- The gameplay impulse waveform is analytic: `normalized = clamp(elapsed / duration)`, `attack = sin(PI/2 * clamp(elapsed / attackSeconds))` (or `1` when attack is zero), `decay = clamp01(curve(normalized))`, and each axis is `amplitude * attack * decay * sin(2*PI*cycles*normalized + stablePhase(identity, axis))`.
- Stable phase uses an explicit FNV-style integer mix plus fixed integer avalanche over the canonical request identity. It uses no random source, `Time.time`, real-time clock, or frame-count phase and is not an authoritative determinism input.
- Gameplay rotation contributions are accumulated as small local Euler-degree vectors in canonical order, capped once, then converted through one `Quaternion.Euler(...)` call. Quaternion multiplication is not used for gameplay source stacking.
- Canonical order is priority descending, semantic ascending, source entity ascending, sequence/action-plan ascending, then tick ascending. The primary contributes fully; same-tier residual is `0.5`, one-tier-lower residual is `0.25`, and more distant residual is `0.1`.
- Topology-only output at default `Full` motion level preserves the evaluator's original position and quaternion exactly. Mixer caps are above the frozen topology production envelope and do not retune it.
- While topology is active, Light and Medium gameplay requests are discarded immediately and are not queued. Heavy uses per-axis absolute-dominance arbitration against topology, followed by the common caps; it is never raw-added to topology.
- Global vector-magnitude caps are `0.08` local-position units and `3` local-rotation degrees. Magnitude caps preserve contribution direction instead of independently distorting axes.
- Pause cancels short gameplay impulses and returns identity immediately. Topology continues to follow the existing frozen-progress pause contract and is sampled again only when presentation updates resume.
- Hard cleanup clears gameplay impulses, dedupe/cooldown state, topology contribution, mixer time, and final output. Rig replacement keeps mixer state but resets both old and newly attached rigs; the new rig receives the current mix on the next presentation application.
- `CameraMotionLevel.Full` is the runtime default. `Reduced` applies position `0.5` and rotation `0.4` multipliers without changing duration. `Off` removes only final visual contribution while request lifecycle and presentation time continue.
- Settings persistence and Settings UI integration are intentionally absent. M1 provides only the runtime setter seam.
- `GameplayCameraShakeProfile` is a validated `ScriptableObject` type. M1 itself authored no production asset; the current P0 production state described below now supplies the canonical profile and composition selected by M2/M3.
- Camera feedback stays outside `WorldState`, `WorldSnapshot`, `TickPipeline`, authoritative event logs, and determinism hash inputs. `GameplayCameraRig` remains semantic-free and remains the single final additive writer.

## P0 production semantic closure

- Production requests are planned from typed presentation facts and presenter-owned visual milestones. Camera-specific contracts are not added to authoritative gameplay state.
- The current production semantic set is fixed to:
  - `PushSlideLaunch`
  - `FlipFloorLanding`
  - `FlipHostileImpact` with `Stay`, `DestroySelf`, and `FollowThrough` variants
  - `PlayerDamageImpact`
  - `PlayerLethalImpact`
  - `HeavyEnemyJumpLanding`
- Push submits at the visible slide-track start. Flip floor/follow-through submits at the final landing milestone, while hostile Stay and DestroySelf submit at the shared hostile-contact milestone. FollowThrough initial contact stays silent so one physical outcome produces one primary impact.
- Accepted same-tick player damage is aggregated to at most one request. A lethal outcome replaces same-tick nonlethal camera feedback and remains visible through the death-reaction/focusing/holding path until terminal closing handoff resets the mixer and rig.
- Heavy enemy landing eligibility is authored on the presentation prefab. The planner waits for the landing-completion track, projects the presentation anchor through the unshaken camera pose, and consumes non-heavy, off-screen, or missing-view outcomes without a global fallback.
- Topology transition shake remains a continuous evaluated contribution. It is not converted to a gameplay impulse and retains its M0 waveform and topology/orbit/post-fx/input-lock ownership boundaries.

## Current production data and accessibility boundary

- The canonical gameplay profile is `GameplayCameraShakeProfile_CampaignV1.asset`, GUID `31fd6c7e4f99468b975714901c1512ba`.
- Its required production key set is eight rows: Push/Default, FlipFloor/Default, the three FlipHostile variants, PlayerDamage/Default, PlayerLethal/Default, and HeavyEnemyJump/Default.
- Campaign topology presets reference the canonical profile. `EnemyView_Astreton.prefab` is the current Heavy landing authoring target; non-heavy enemy presentation authoring remains `None` by default.
- `CameraMotionLevel.Full` is the runtime default. `Reduced` scales only additive position/rotation contributions and `Off` returns identity output while request lifecycle continues.
- `CameraMotionLevel` currently covers gameplay and topology additive shake only. It does not control topology orbit, board transition, or post-fx. Settings UI and persistence remain intentionally deferred.
