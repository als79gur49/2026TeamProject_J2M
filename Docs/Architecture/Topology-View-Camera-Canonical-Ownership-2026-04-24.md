# Topology-View-Camera Canonical Ownership 2026-04-24

This document is the slice-local supporting truth-source for the current topology/view/camera runtime ownership map. It closes the completed cleanup as a documentation and governance step; it is not a new refactor plan.

## Overview

- The topology-view-camera cleanup is closed at the current structure.
- This document fixes canonical ownership for the runtime contracts, camera runtime, shake runtime, post-fx runtime, bootstrap/helper lanes, and remaining scaffold surface.
- This document does not reopen `GameplayHostRuntimeFactory`, `GameplayShowcaseSceneScaffold`, `GameplayCameraRig`, shake tuning, post-fx tuning, or scene/prefab authoring.

## Final ownership map

| Surface | Canonical owner | Locked responsibility |
| --- | --- | --- |
| `TopologyTransitionVisualState` | runtime contract | shared topology-transition visual state contract |
| `GameplayPresentationPhase` | runtime contract | presentation-phase enum contract |
| `TopologyRotationVisualMapping` | runtime contract | topology-rotation mapping contract |
| `CameraDistanceMode` | runtime contract | camera-distance ownership contract |
| `GameplayCameraRig` | camera runtime | authored baseline capture/use, unshaken pose resolution, final hierarchy apply, cached shake apply, and direct camera pose apply |
| `TopologyTransitionCameraShakeProfile` | shake runtime | pure authored shake profile |
| `TopologyTransitionCameraShakeController` | shake runtime | topology-transition shake evaluator |
| `TopologyTransitionCameraShakeResult` | shake runtime | pure helper value object for local position/rotation deltas |
| `TopologyTransitionPostFxProfile` | post-fx runtime | pure authored post-fx profile |
| `TopologyTransitionDistortionProfile` | post-fx runtime | co-located authored distortion profile |
| `TopologyTransitionPostFxController` | post-fx runtime | URP runtime adapter, runtime volume clone owner, and authoritative source-profile immutability owner |
| `GameplayHostTopologyVisualRuntimeBootstrap` | bootstrap/helper lane | host-side attach/wiring glue |
| `GameplayShowcaseSceneCameraBootstrap` | bootstrap/helper lane | scene camera bootstrap glue |
| `GameplayShowcaseSceneScaffold` | remaining scaffold | scene orchestration, board-root ensure/find, legacy-label cleanup, and wrapper-only `ConfigureDefaultSceneCamera(...)` |

## Runtime slices

- Contracts stay file-local to the `Gameplay_Host/Runtime/Contracts` slice and remain the shared vocabulary between presenter, host wiring, camera runtime, and post-fx runtime.
- `GameplayCameraRig` is the only runtime owner that may combine authored baseline capture, presented orbit resolution, hierarchy pose application, cached shake application, and direct camera pose application into one final camera-output pipeline.
- Shake runtime stays split into data, evaluator, and pure result value:
  - `TopologyTransitionCameraShakeProfile` is authored data only.
  - `TopologyTransitionCameraShakeController` evaluates shake envelopes from `TopologyTransitionVisualState`.
  - `TopologyTransitionCameraShakeResult` carries the computed pose delta only.
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

- `GameplayHostTopologyVisualRuntimeBootstrap` is the host-side glue owner only. It wires the view camera, camera rig, presenter camera runtime attachment, and post-fx controller attachment without reclaiming shake math or post-fx tuning behavior.
- `GameplayShowcaseSceneCameraBootstrap` is the showcase-scene glue owner only. It wires scene output cameras, Cinemachine path setup, authored scene camera pose capture, and default blend/bootstrap behavior without reclaiming runtime evaluation behavior.
- `GameplayShowcaseSceneScaffold` remains above both helpers as the scene orchestrator. Its responsibilities are limited to installer-scaffold orchestration, board-root ensure/find, legacy world-label cleanup, and wrapper-only `ConfigureDefaultSceneCamera(...)`.

## Authoring And Preset Authority

- `GameplayCameraTopologyAuthoring` is the scene-local authority entrypoint.
- `GameplayCameraTopologyPreset` owns stage-scoped shared tuning only.
- `GameplayCameraSettings` is shared tuning only.
- `GameplayCameraBaselineAuthoringPolicy` is the dedicated scene-local authored-baseline policy type.
- `GameplayCameraTopologyInlineSharedTuning` is the explicit inline-only shared-tuning fallback lane.
- `configureMainCamera` remains local.
- `UseAuthoredSceneCameraPose/Lens` live in scene-local baseline policy, not `GameplayCameraSettings`.
- `Preset` mode ignores inline shared-tuning values for runtime snapshot resolution.
- Candidate A is complete:
  - authored-baseline usage flags no longer live in `GameplayCameraSettings`
  - scene-local baseline policy is carried explicitly through authoring snapshot, host configuration, bootstrap glue, and rig resolution
  - preset `cameraSettings` now serializes shared tuning only
- Candidate B is complete:
  - non-authoritative inline shared tuning no longer lives as five root fields on `GameplayCameraTopologyAuthoring`
  - scene-local authoring now serializes one explicit `inlineSharedTuning` block for `Inline` mode only
  - `Preset` mode inspector UX hides the inline lane and keeps the referenced preset as the only authoritative shared-tuning source

## Explicit non-ownership

- scaffold is not camera-bootstrap behavior owner
- rig is not shake/post-fx math owner
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
