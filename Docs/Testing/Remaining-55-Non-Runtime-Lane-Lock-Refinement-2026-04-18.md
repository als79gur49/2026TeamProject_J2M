# Remaining 55 Non-Runtime Lane Lock Refinement 2026-04-18

This document is the current operating plan for the open `55` failures in `./run_tests.sh full`.

Checkpoint:
- Source of truth: `TestResults/wsl-unity-full-editmode.xml`
- Current result: `944 total / 55 failed`
- `Movement_ItemBox_LosesBoardPresenceBeforeCleanupRemoval` is closed against the current canonical `TryPickImpactTargetAt(...)` contract.
- Open `runtime-authoritative bug`: `0`

Truth-source and no-touch rules:
- Use `current runtime + canonical docs + current XML` as truth-source.
- Do not widen `runtime/core`.
- Do not restore old API, constructor, component, trace, dump, or literal contracts.
- Do not reopen the closed authoritative/core axes.
- Use one primary lane per failing row.
- `Deferred classification` defaults to `0` and is allowed only under the limited gate defined below.

## Stable Judgments

- The open backlog is a non-runtime backlog, not an implementation backlog.
- `WorldState` authoritative ownership, commit-path writes, deterministic tick execution, and `TickResult -> TickPresentationData -> ViewPresenter` remain fixed.
- Stale lanes are locked before host/view lanes so runtime-noise is removed first.
- `consumer/view` and `topology/view/post-fx` stay separate even when the same test fixture lives in the same file.
- Runtime promotion requires a current-caller authoritative snapshot, query, or final-state mismatch. No open row currently meets that bar.

## Lane Counts

| Lane | Count | Execution Split |
| --- | ---: | --- |
| `stale-contract` | `14` | `11` public-contract drift + `3` fixture/seed/helper drift |
| `stale-literal / trace / comparer drift` | `19` | `17` trace/dump/serialized literal drift + `2` comparer/order wording drift |
| `consumer/view` | `10` | semantic presentation production or presenter/driver mapping |
| `topology/view/post-fx` | `12` | projector/scene graph/camera/rig/view-factory/post-fx |

## Refined Step Structure

1. `Checkpoint Freeze`
2. `stale-contract lock`
3. `Step 2-A: public contract drift`
4. `Step 2-B: fixture / seed / helper drift`
5. `stale-literal lock`
6. `Step 3-A: trace / dump / serialized literal drift`
7. `Step 3-B: comparer / order wording drift`
8. `limited deferred gate`
9. `consumer/view lock`
10. `topology/view/post-fx lock`
11. `verification + ledger sync`

## Step Details

### Step 1. Checkpoint Freeze
- Purpose: freeze the current open set at `55` and keep `runtime-authoritative bug = 0`.
- Primary signal: the current full XML fail list.
- Secondary signal: the historical closure of `Movement_ItemBox_LosesBoardPresenceBeforeCleanupRemoval`.
- Failure mechanisms: row-count drift, stale snapshot reuse, outdated runtime-row carryover.
- Key checks: open fail list matches the current lane split; the pre-close `56` snapshot is not reused as the live backlog ledger.
- Do not touch: runtime/core, canonical docs reinterpretation, query vocabulary.
- Exit condition: `14 / 19 / 10 / 12` matches the current XML.
- Reclassify when: a new authoritative snapshot/query/final-state mismatch appears.

### Step 2-A. Public Contract Drift
- Purpose: lock old public API shape, validation source, authoring contract, and timing contract expectations into `stale-contract`.
- Primary signal: current caller is green and the failure is isolated to reflection, overload, constructor, `ParamName`, optional authoring, or old duration expectations.
- Secondary signal: adjacent current production/runtime caller already uses the new seam.
- Failure mechanisms: public seam drift, validation-source drift, optional authoring drift, old timing-oracle drift.
- Key checks: old expectation is not required by current caller and not required by the canonical docs.
- Do not touch: public/runtime seams, overload restoration, component requirement restoration, duration-contract rollback.
- Exit condition: the row is assigned to `stale-contract/public-contract`.
- Reclassify when: helper or synthetic-state setup is the actual failure source, or current caller evidence is no longer green.

Representative rows:
- `TickPipelineStructureCoreTests.*`
- `EnemyAiRuntimeDefinition_Negative*`
- `EntityEffectPresentationAuthoringTests.*Player_S1.prefab*`
- `GameplayTimingOwnershipTests.PlayerAnimatorDriver_WithAnimatorOverride_PrefersAnimatorDurationOverResolvedMotionDuration`

### Step 2-B. Fixture / Seed / Helper Drift
- Purpose: lock obsolete synthetic state, stale helper-built snapshots, and impossible seeded runtime states into `stale-contract` without mixing them with public contract drift.
- Primary signal: the failure depends on target-less pending action state, stale helper seed shape, or obsolete enemy-action seed shape.
- Secondary signal: the same runtime path stays green when exercised through a valid current caller.
- Failure mechanisms: impossible seeded runtime state, stale helper assumptions, fixture-only snapshot construction.
- Key checks: the failure cannot be reproduced through the current caller or a valid current-world setup.
- Do not touch: runtime widening to support obsolete synthetic states.
- Exit condition: the row is assigned to `stale-contract/fixture-cleanup`.
- Reclassify when: the row actually asserts an old public contract, or when current caller evidence is insufficient and the limited deferred gate is needed.

Representative rows:
- `PlayerMovementInputTests.PlayerControlStateLogic_ActiveAction_AdvancesExecutionAndCompletion`
- `PlayerMovementInputTests.PlayerLogic_ConfiguredTimingSnapshot_OnlyExecutesOnSnapshotExecuteTick`
- `TickReplayDeterminismTests.DeterminismHash_EnemyActionState_IsIncludedInCanonicalState`

### Step 3-A. Trace / Dump / Serialized Literal Drift
- Purpose: lock stale trace token, replay dump wording, removed entity spawn literal, and serialized asset literal drift into `stale-literal`.
- Primary signal: final snapshot, cleanup, hash/event, and spawn semantics are already green while the failure sits only on token, dump, or serialized literal wording.
- Secondary signal: direct semantic guard rows around the same feature remain green.
- Failure mechanisms: `Kind=*` token drift, replay dump wording drift, spawn dump drift, serialized stage/prefab/showcase literal drift.
- Key checks: final entity set, cleanup outcome, replay hash/event semantics, and ForwardCell presentation semantics remain correct.
- Do not touch: trace formatter, replay writer, runtime literal vocabulary.
- Exit condition: the row is assigned to `stale-literal/trace-dump-serialized-literal`.
- Reclassify when: final entity, cleanup, hash/event, or spawn semantics are no longer green.

Direct targets:
- `Replay_ItemScenario_*`
- `Replay_CompositeItemAttackScenario_*`
- `Replay_PlayerControlState_*`
- `Replay_PassiveContactScenario_*`
- `Attack_RemovedEntityIntent_*`
- movement `Kind=Push/Flip/Item` rows
- `MechanicsShowcaseStage_*`
- `StageRuntimeBuilder_*`
- `EnemyPrefabScaffoldTests.*`

### Step 3-B. Comparer / Order Wording Drift
- Purpose: isolate wording-only ordering rows from rows that may still hide a real ordering regression.
- Primary signal: local comparer surface or reject wording is wrong, but accepted winner, final occupancy, and impact target semantics stay correct.
- Secondary signal: downstream guard coverage is strong enough to prove the order truth is still intact.
- Failure mechanisms: reject wording drift, comparer-order wording drift, ambiguous order assertions.
- Key checks: downstream winner, occupancy, and impact target semantics remain green.
- Do not touch: comparer/runtime ordering code, resolve ordering policy.
- Exit condition: the row is assigned to `stale-literal/comparer-order-wording`.
- Reclassify when: downstream guards are missing or the order truth itself becomes ambiguous.

Direct targets:
- `Movement_SameDestination_OnlyHigherPriorityWins`
- `ImpactReservationComparer_PreservesFaceBeforePlanarOrder`

Guard tests:
- `Movement_EdgeReservation_StillRejectsDestinationConflict`
- `Movement_UnitSharedMove_RejectsLaterCandidateWhenPushAlreadyReservedDestination`
- `Replay_ForwardCellImpactScenario_ProducesSamePerTickHashTraceAndEventLog`

### Step 4. Limited Deferred Gate
- Purpose: prevent forced lane locks while keeping `Deferred classification` rare and short-lived.
- Primary signal: only one of three allowed ambiguity classes is present.
- Secondary signal: the missing evidence is identifiable and collectible in the adjacent step.
- Failure mechanisms: insufficient comparer/order guard coverage, mixed `PresentationData` vs geometry oracle, mixed public-contract vs fixture drift.
- Key checks: one reason per row, maximum `3` deferred rows total, and a concrete evidence request attached to the row.
- Do not touch: runtime promotion, generic parking-lot carryover.
- Exit condition: the row is rechecked at the end of the adjacent step and forced into a primary lane.
- Reclassify when: the needed control or guard evidence becomes available.

Allowed reasons:
- downstream winner/occupancy guard is still insufficient for a comparer/order row
- `PresentationData` and geometry oracles fail together and first-wrong-oracle is not yet proven
- public-contract drift and fixture drift are both plausible and current caller evidence does not yet split them

Required evidence per deferred row:
- `1` current-caller control
- `1` guard/control test adjacent to the suspected oracle
- `1` minimum missing oracle capture

Runtime protection:
- deferred rows are not runtime candidates
- runtime promotion still requires current-caller authoritative snapshot/query/final-state mismatch

### Step 5. Consumer/View Lock
- Purpose: lock rows whose first wrong oracle is semantic presentation production or presenter/driver mapping.
- Primary signal: wrong `TickPresentationData`, signal timing, motion kind, or driver state.
- Secondary signal: rendered mismatch that is plausibly downstream of the semantic signal drift.
- Failure mechanisms: builder output drift, signal-timing drift, presenter/driver mapping drift.
- Key checks: authoritative snapshot, cleanup, and final entities remain acceptable before classifying.
- Do not touch: projector, camera, scene graph, board-root geometry.
- Exit condition: the row is assigned to `consumer/view`.
- Reclassify when: `PresentationData` is acceptable and the first wrong oracle is rendered pose, board root, camera, or scene graph.

Representative rows:
- `WorldSnapshotAndPresentationTests.TickPresentationDataBuilder_*`
- `GameplayTickViewPresenter_Present_*`
- `GameplayTickPresentationCoordinatorTests.*`
- `PlayerControl_LocomotionPresentationSignal_*`

### Step 6. Topology/View/Post-Fx Lock
- Purpose: lock rows whose first wrong oracle is projection, rendered pose, scene graph, board root, camera, rig, view-factory wiring, or post-fx state.
- Primary signal: rendered pose, visible face set, board-root identity, camera target/orbit, scaffold attachment, or runtime volume clone mismatch.
- Secondary signal: benign semantic presentation variance that does not explain the rendered failure.
- Failure mechanisms: projector pose drift, board-surface topology drift, scene-graph drift, camera/rig drift, view-factory drift, post-fx drift.
- Key checks: `PresentationData` is already acceptable or is not the primary oracle.
- Do not touch: logic/view seam, authoritative runtime, presentation builder semantics.
- Exit condition: the row is assigned to `topology/view/post-fx`.
- Reclassify when: `PresentationData` or driver output is the first wrong oracle.

Representative rows:
- `GameplayCubeProjector_*`
- `GameplayBoardSurfaceRenderer_TopologyTransition_*`
- `GameplaySceneHost_CameraTarget_*`
- `GameplaySceneHost_TopologyTransition_CameraOrbit*`
- `GameplayCameraRig_*`
- `DefaultGameplayEntityViewFactory_*`
- `CombinedGameplayShowcaseInstaller_*`
- `TopologyTransitionPostFxTests.*`

### Step 7. Verification And Ledger Sync
- Purpose: finish with `55` rows mapped to one primary lane or a short-lived limited deferred gate, then sync the backlog destinations.
- Primary signal: stale rows move into the stale ledger; host/view rows remain in the dated lane snapshot or the follow-up host/view backlog.
- Secondary signal: lane counts and representative rows still match the current XML.
- Failure mechanisms: stale-lane leakage into host/view backlog, deferred carryover, historical snapshot reuse.
- Key checks: current XML, lane counts, representative row mapping, stale ledger notes.
- Do not touch: runtime fixups, trace/literal pre-fixes, test rewrites.
- Exit condition: runtime-authoritative open rows remain `0` and no deferred row survives beyond this step.
- Reclassify when: a row's failure surface moves into current-caller authoritative mismatch.

## Lane Boundary Rules

### `stale-contract`

`public-contract` drift:
- current caller/public seam already uses the new contract
- failure is tied to old overload, old constructor, old `ParamName` source, old authoring/component requirement, or old duration expectation
- destination: `stale-contract/public-contract`

`fixture-cleanup` drift:
- failure depends on stale helper, stale seed, or impossible synthetic state
- current caller path does not require that state
- destination: `stale-contract/fixture-cleanup`

Reassignment:
- if a supposed public-contract row only fails because of obsolete helper state, move it to `fixture-cleanup`
- if a supposed fixture row is actually asserting an obsolete public seam or authoring contract, move it to `public-contract`

### `stale-literal`

`trace-dump-serialized-literal` drift:
- final snapshot, cleanup, hash/event, and spawn semantics are green
- failure lives only in token, dump wording, or serialized asset literal
- destination: `stale-literal/trace-dump-serialized-literal`

`comparer-order-wording` drift:
- accepted winner, final occupancy, and impact target semantics stay green
- failure lives only in reject wording or local comparer surface
- destination: `stale-literal/comparer-order-wording`

Deferred instead of stale-literal:
- guard coverage is too weak to prove winner/occupancy truth
- the direct target also exposes final-state risk

## `consumer/view` vs `topology/view/post-fx`

`consumer/view` first-wrong-oracle:
- `TickPresentationData`
- signal timing
- motion kind
- presenter/driver state

`topology/view/post-fx` first-wrong-oracle:
- projected or rendered pose
- board-root identity
- visible face set
- camera target/orbit
- scene graph or scaffold attachment
- runtime post-fx clone state

Mixed-row procedure:
1. Freeze source snapshot and final entities.
2. Check builder-level `TickPresentationData` or equivalent semantic oracle.
3. If that output is wrong first, classify as `consumer/view`.
4. If that output is acceptable, inspect projected pose, board root, scene graph, camera, then post-fx.
5. If only the rendered/scene-side oracle is wrong, classify as `topology/view/post-fx`.

Tie-break:
- if `PresentationData` and geometry are both wrong, `consumer/view` wins first
- if `PresentationData` is acceptable and pose/board-root/camera is wrong, `topology/view/post-fx` wins
- if snapshots and final entities are green but the scene graph is wrong, `topology/view/post-fx` wins

## Deferred Classification Rules

Default:
- `Deferred classification = 0`

Allowed:
- maximum `3` rows at a time
- one deferred reason per row
- only the three limited-gate reasons listed in Step 4

Not allowed:
- generic "unclear" rows
- pure token-only rows
- obvious current-caller-green old API expectation rows
- obvious `PresentationData`-wrong-first rows
- obvious rendered-pose-wrong-first rows

Recheck timing:
- Step 2 deferred rows must be forced closed before Step 2 ends
- Step 3 deferred rows must be forced closed before Step 3 ends
- Step 5/6 deferred rows must be forced closed before Step 6 ends
- no deferred row carries past Step 7

## Backlog Destinations

- `stale-contract/public-contract`
- `stale-contract/fixture-cleanup`
- `stale-literal/trace-dump-serialized-literal`
- `stale-literal/comparer-order-wording`
- `consumer/view`
- `topology/view/post-fx`

## Escalation Signals

Keep the current lane-lock order unless one of the following happens:
- Step 3-A can no longer explain replay dump rows, ForwardCell presentation literal rows, and serialized asset literal rows with the same semantic guard set
- Step 5/6 still leaves mixed rows after the first-wrong-oracle procedure
- a stale-lane row moves from wording/contract drift to current-caller authoritative mismatch

If that happens:
- split Step 3-A into `trace/dump` and `serialized asset literal`
- split Step 5/6 into `builder vs presenter` and `projector/board-surface vs camera/rig vs view-factory/post-fx`
- still do not widen runtime/core without a fresh current-caller authoritative mismatch
