# Move Presentation Ownership Narrowing

Date: 2026-05-02

## Executive Decision

`TickEntityMotionKind.Move` remains retained.
This package is an ownership narrowing pass, not an enum deletion pass.
Covered player ordinary, enemy ordinary, and Charge active fallback authorization is already removed; those attempts now reject with deterministic removed-fallback diagnostics.
Retained generic movement and retained grid transaction presentation may still use `TickEntityMotionKind.Move`.
`MoveEntity`, `MovementExpander`, topology materialization, box/action movement, spawn/respawn placement, cleanup removal, scripted relocation, and glide retained fallback are protected.
Kinematic and continuous locomotion replacements must present through `TickContinuousLocomotionTrack`, `TickKinematicMotionTrack`, and Charge signals instead of entity `Move`.
Replay/golden files are not rewritten by this pass.

## Producer Inventory

| producer location | source operation | boundary kind | semantic kind | current runtime reachable? | retained path? | fallback residue? | expected presentation | tests covering it | cleanup action | blocker |
|---|---|---|---|---|---|---|---|---|---|---|
| `TickPipeline.ValidateLegacyExpansionIntents` player ordinary | ordinary Unit `Move` legacy expansion attempt | `LegacyFallback` | `Move` | removed diagnostic only | no | yes | reject, no entity `Move`, replacement track when enabled | `MoveOwnership_PlayerContinuous_DoesNotEmitEntityMove`, player replay | wording cleanup only | historical names |
| enemy ordinary fallback | ordinary enemy `Move` legacy expansion attempt | `LegacyFallback` | `Move` | removed diagnostic only | no | yes | reject, no entity `Move`, kinematic track | `MoveOwnership_EnemyKinematic_DoesNotEmitEntityMove`, enemy replay | wording cleanup only | glide flag-off retained exception |
| Charge active fallback | active Charge legacy expansion attempt | `LegacyFallback` | `Move` after ChargeMove deletion | removed diagnostic only | no | yes | reject, no entity `Move`, Charge kinematic track and signal | `MoveOwnership_ChargeKinematic_DoesNotEmitEntityMove` | canary only | historical Charge docs |
| player Free2D anchor normalization | `MoveEntity` anchor commit | `LocomotionAnchorCommit` | `Move` | yes | replacement path | no | suppress entity `Move`, emit continuous track | `MoveOwnership_PlayerContinuous_DoesNotEmitEntityMove` | protect | topology handoff |
| player kinematic anchor commit | `KinematicAnchorCommitted` | `LocomotionAnchorCommit` | `Move` | yes | replacement path | no | suppress entity `Move`, emit kinematic track | `MoveOwnership_PlayerKinematic_DoesNotEmitEntityMove` | protect | fallback naming overlap |
| enemy kinematic anchor commit | `KinematicAnchorCommitted` | `LocomotionAnchorCommit` | `Move` | yes | replacement path | no | suppress entity `Move`, emit kinematic track | `MoveOwnership_EnemyKinematic_DoesNotEmitEntityMove` | protect | glide exception |
| Charge kinematic anchor commit | `KinematicAnchorCommitted` | `LocomotionAnchorCommit` | `Move` | yes | replacement path | no | suppress entity `Move`, emit Charge track and signal | `MoveOwnership_ChargeKinematic_DoesNotEmitEntityMove` | protect | no ChargeMove reintroduction |
| topology handoff | topology materialization action group | `TopologyMaterialization` | `Move` or topology semantic | yes | yes | no | retained topology/generic presentation | `MoveOwnership_GridTransactions_Retained` | protect | over-suppression |
| box push / flip / item / impact | action group move writes | `BoxActionMovement` | `Push`, `Flip`, `Item`, sometimes `Move` | yes | yes | no | push/flip/slide or item-to-`Move` presentation | `MoveOwnership_BoxActionMovement_RetainsRequiredMovePresentation`, `MoveOwnership_ItemOrGridMovement_Retained` | protect | item maps to `Move` |
| spawn / respawn | placement records | `SpawnRespawnPlacement` | placement or `Move` | yes | yes | no | retained placement/visibility presentation | boundary inventory | document retained | visibility-led presentation |
| cleanup | cleanup removal | `CleanupRemoval` | usually none or retained movement-linked | yes if emitted | yes | no | retained cleanup/visibility behavior | world snapshot cleanup tests | document retained | sparse direct coverage |
| scripted / phase relocation | phase relocation payload | `ScriptedRelocation` | `Move` | yes | yes | no | retained entity `Move` if relocation emits motion | `Replay_MoveOwnership_RetainedMoveStillDeterministic` | protect | replay drift |
| synthetic presentation tests | direct `TickEntityMotion` construction | none | enum `Move` | test-only | generic consumer only | no | generic motion consumer coverage | `MoveOwnership_GenericMovePresentation_Retained` | keep when labelled synthetic | golden blocker |
| historical fallback wrappers | helper/doc strings | `LegacyFallback` wording | `Move` wording | test/doc only | no | yes | diagnostics only | doc canaries | cleanup candidate later | no golden rewrite |

## Consumer Inventory

| consumer location | use kind | required by retained path? | fallback-only? | synthetic/test-only? | removal risk | tests covering it | action recommendation |
|---|---|---:|---:|---:|---|---|---|
| `TickPresentationData.TickEntityMotionKind.Move` | presentation enum | yes | no | no | high | host/unit/replay tests | retain |
| `TickResultBuilder.TryResolveMotionKind` | semantic-to-motion mapping | yes | no | no | high; item/grid break | `MoveOwnership_GenericMovePresentation_Retained`, movement phase | retain |
| `TickResultBuilder.ShouldSuppressLegacyMotionForLocomotion` | suppression boundary | yes | no | no | high | `MoveOwnership_SuppressionBoundary_IsCurrent` | retain current matrix |
| `GameplayMotionTimingResolver` Move branch | timing | yes | no | no | medium | coordinator tests | retain |
| `EntityMotionPresentationAuthoring` Move override | authoring override | yes | no | no | medium | authoring/coordinator tests | retain |
| `GameplayTrackPlanner.RefreshMotionClips` | generic clip planning | yes | no | no | high | `MoveOwnership_GenericMovePresentation_Retained` | retain |
| `GameplayEntityPresentationApplier` | local motion application | yes | no | no | high | coordinator tests | retain |
| `MotionTrack` Move interpolation | linear constant sampling | yes | no | no | medium | coordinator tests | retain |
| `WorldSnapshotAndPresentationTests` direct `Move` fixtures | builder and synthetic coverage | yes for builder | partly | yes | medium | unit canaries | keep labelled retained/synthetic |
| `GameplayTickPresentationCoordinatorTests` direct `Move` fixtures | host generic consumer | yes | no | yes fixture source | high | coordinator canary | keep |
| replay tests | determinism and no-covered fallback | yes | partly | no | high | `Replay_MoveOwnership_*` | add canaries, no rewrite |

## Suppression Boundary Inventory

| boundary kind | suppress legacy Move? | reason | current tests | risk |
|---|---:|---|---|---|
| `LocomotionAnchorCommit` | yes | kinematic/continuous anchor commits must not duplicate entity `Move` | `MoveOwnership_SuppressionBoundary_IsCurrent` | leaks covered `Move` |
| `UnitOrdinaryLocomotion` | yes | ordinary Unit replacement/fallback boundary suppresses entity `Move` | `MoveOwnership_SuppressionBoundary_IsCurrent` | leaks covered `Move` |
| `BoxActionMovement` | no | retained grid/action presentation | movement phase canaries | over-suppression |
| `TopologyMaterialization` | no | retained topology handoff | boundary inventory | over-suppression |
| `SpawnRespawnPlacement` | no | retained placement/visibility branch | boundary inventory | indirect presentation |
| `CleanupRemoval` | no | retained cleanup/visibility branch | world snapshot cleanup tests | sparse direct coverage |
| `ScriptedRelocation` | no | retained phase/script relocation | replay canary | replay drift |
| `LegacyFallback` | no runtime authorization for covered fallback | removed diagnostics reject before retained presentation | removed diagnostic canary | synthetic confusion |
| `Unknown` | no automatic suppression | unknown must be inventoried, not silently suppressed | unknown boundary inventory | hidden producer leak |

## Retained vs Cleanup Candidate Buckets

| bucket | contents | action |
|---|---|---|
| retained generic presentation | enum `Move`, timing resolver, authoring override, planner/applier, `MotionTrack` | keep and test |
| retained grid transaction presentation | topology, box/action, item-to-`Move`, spawn/respawn, cleanup, scripted relocation | protect with canaries |
| kinematic/continuous replacement | player Free2D, player kinematic, enemy kinematic, Charge kinematic | assert no entity `Move`, assert tracks/signals |
| removed fallback residue | covered player/enemy/Charge fallback attempts | diagnostic-only cleanup candidate |
| synthetic/test-only compatibility | direct `TickEntityMotionKind.Move` fixtures | keep only for generic consumer coverage |
| historical/golden blocker | replay/golden names and historical docs | document, no rewrite |
| cleanup candidate | fallback-only helper wording and dead diagnostic wrappers | only after canaries are green |

## Replay / Golden Matrix

| replay/golden area | expected Move output | retained or fallback residue? | current test | owner approval needed? | action |
|---|---|---|---|---:|---|
| player replay | no covered entity `Move`; continuous state deterministic | fallback residue absent | `Replay_MoveOwnership_NoCoveredFallbackMove` | no | keep canary |
| enemy replay | no ordinary enemy entity `Move`; kinematic deterministic | fallback residue absent | `Replay_MoveOwnership_NoCoveredFallbackMove` | no | keep canary |
| charge replay | no entity `Move`; Charge track/signal deterministic | fallback residue absent | Charge replay canaries | no | keep ChargeMove-deletion canaries |
| movement phase | grid transaction `Move` may remain | retained | `MoveOwnership_GridTransactions_Retained` | no | protect |
| presentation coordinator | synthetic generic `Move` remains accepted | retained/synthetic | coordinator canary | no | label as generic consumer |
| world snapshot/presentation | builder emits retained `Move`, suppresses locomotion | retained and replacement | `MoveOwnership_SuppressionBoundary_IsCurrent` | no | protect |
| determinism hash | retained `Move` must not destabilize hash | retained | replay canary | maybe if hashes change | no rewrite |

## Risk Register

| risk | mitigation |
|---|---|
| Move deletion attempted too early | enum and consumers explicitly retained |
| retained grid transaction suppressed accidentally | suppression matrix and grid canaries |
| generic Move presentation broken | world snapshot and coordinator canaries |
| kinematic/continuous track confused with entity Move | replacement tests assert track/signal and no entity `Move` |
| replay/golden drift | no rewrite; targeted replay canaries |
| source grep overmatches docs/historical names | inventory buckets separate historical strings |
| fallback residue hidden in synthetic tests | synthetic fixtures are consumer coverage only |
| broad unrelated failures hide ownership regressions | report targeted `MoveOwnership_*` results separately |

## Final Recommendation

TickEntityMotionKind.Move remains retained.
Immediate cleanup target is not enum deletion.
Next action is fallback-only producer/test naming cleanup or suppression boundary tightening only if inventory canaries find leaks.
