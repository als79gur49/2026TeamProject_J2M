# Enemy Glide Contract Cleanup Preparation - 2026-05-25

## Summary

Current `GlideOverSolid` is a layered legacy contract, not one canonical behavior.
It combines solid traversal, visual overflight, authoritative Unit anchor writes, `LandingPending` solid-overlap preservation, active-origin kinematic continuation, and anchor commit materialization under one capability name.
This slice keeps behavior unchanged and records the cleanup map before B-style authority removal and later C2 no-solid-anchor work.

## Glide terminology map

- `Glide traversal`: movement candidate evaluation may ignore a solid blocker.
- `Glide visual overflight`: presentation can show a glide path crossing a solid cell; this does not imply `MoveEntity`.
- `Glide authoritative anchor`: the Unit occupancy anchor stored by `WorldState` at a `SurfaceCell`.
- `Glide solid anchor`: an authoritative Unit anchor on the same `SurfaceCell` as a solid occupant.
- `Glide landing`: Active terminal intended cell and `LandingPending` preserve cell are distinct meanings.
- `LandingPendingCell`: current contract uses it as a representable solid-overlap preserve cell; target C2 narrows it to visual/egress context.
- `locked-step terminal`: current representability exception for `LandingPending` solid overlap; target C2 removal candidate.
- `active-origin segment`: a kinematic segment started during Active that can remain Active-kind after the glide phase has advanced.
- `GlideActiveKinematicAnchorCommit`: current boundary reason that can materialize authoritative `MoveEntity`.
- `GlidePresentationSignal`: target visual-only signal that must not imply authoritative movement.

## Current vs Target Contract Table

| Contract | Current behavior | Current owner | Current tests | Keep for now? | B-style handling | C2 handling | Risk if kept | Risk if removed |
|---|---|---|---|---|---|---|---|---|
| Active state can ignore solid traversal blocker | Active glide traversal bypasses solid blockers | `WorldPlacementPolicy`, traversal legality | `Glider_Active*`, `GlideActive_*Solid*` | Yes | Keep | Keep as traversal-only | Solid-anchor confusion remains | Active chase regressions |
| Active state can anchor Unit on solid cell | Active glider can occupy solid cell | `WorldPlacementPolicy`, `WorldState` representability | `Glide_CurrentContract_ActiveCanAnchorOnSolid` | Yes | Inventory only | Remove | Unit+Solid overlap persists | Existing old-contract tests fail |
| `LandingPending` can represent Unit+Solid overlap | Pending cell plus actor/locked terminal can be representable | `GlideSolidAnchorRepresentability`, `WorldPlacementPolicy` | guard parity/current-contract matrix | Yes | Prepare removal | Remove | Pending authority persists | Existing egress/preserve tests fail |
| active-origin segment survives into `LandingPending` | Active-started pose can commit as Active kind during pending | `TickPipeline.TryResolveEnemyGlideKinematicContinuationKind` | current-contract active-origin tests | Yes | Remove after prep | Remove | Stale commit risk | Current presentation smoothness tests fail |
| active-origin segment survives into Recovery/Cooldown | Active kind can survive after Active if terminal is not solid-bound | same | `GlideActive_Kinematic_ActiveEndsWhileNonSettled_CompletesSegmentWithoutSnap` | Yes | Remove after prep | Remove | Phase boundary drift | Current continuation tests fail |
| `GlideActiveKinematicAnchorCommit` materializes `MoveEntity` | AnchorChanged kinematic outcomes call `FinalizationBatch.MoveEntity` | `MaterializeMovementOperations` | `Glide_CurrentContract_AnchorCommitMaterializesMoveEntity` | Yes | Split carrier naming later | Replace with explicit authority intent | Misread as presentation | Current commit tests fail |
| TickPipeline guard duplicates representable rule | Guard and placement used equivalent private predicates | `TickPipeline`, `WorldPlacementPolicy` | parity/current-contract matrix | No | Shared helper extracted | Predicate deleted | Drift | Immediate guard mismatch possible |
| locked-step terminal can make overlap representable | Pending cell plus actor current/locked terminal allows overlap | shared helper | locked-step matrix | Yes | Inventory only | Remove | Old exception remains | Matrix tests fail |
| Presentation track shares anchor information with commit | `TickKinematicMotionTrack` carries anchor cells also used by commit flow | `TickPresentationData`, `TickPipeline` | presentation current-contract tests | Yes | Design split | Split visual track from authority intent | Coupled model persists | Visual regressions |
| `WorldState.MoveEntityTo` lacks operation metadata | Placement sees current entity/glide state only | `WorldState` | placement invariant tests | Yes | Keep invariant | Keep invariant | Need state-based exceptions | Metadata bypass risk avoided |

## Test classification table

| Class | Tests |
|---|---|
| A. Keep invariant | timing compile/lifecycle tests, traversal still blocks edge/topology/reservations/TileFeature, non-glide placement invariants, `WorldState_MoveEntityTo_LandingPendingGlider_CannotOccupyNonPendingSolidCell` |
| B. stage-3-1 regression/safety | `StageRuntimeBuilder_Stage31Build_MaterializesGlider241AndWall238Contract`; do not move wall 238 or enemy 241 in `stage-3-1.asset` |
| C. Old-contract tests | active solid anchor, matching-wall allow, `LandingPending` preserve, active-origin continuation into pending/recovery/cooldown, locked-step terminal matrix |
| D. Trace-output-config fragile | rejected reason substring and rejected reason sequence tests around `GlideLandingPendingAnchorNotRepresentable` |
| E. Determinism/hash | rejected reason hash stability, `DeterminismHash_EnemyGlideState_IsIncludedInCanonicalState`, `DeterminismHash_EnemyGlideLockedTarget_IsIncludedInCanonicalState` |
| F. Target-contract needed | no final Unit+Solid overlap, pending no `MoveEntity`, no stale active-origin commit, stage-3-1 no wall anchor, repeated glide determinism |
| G. Delete/rewrite candidates | all tests that assert Active solid anchor, pending solid overlap preserve, or active-origin carry-over after B/C2 migration |

## LandingPending predicate duplication decision

Decision: extract the shared predicate now.

- Added `GlideSolidAnchorRepresentability.CanRepresentLandingPendingSolidAnchor(...)` in the BoardState placement policy file.
- `WorldPlacementPolicy.ShouldIgnoreSolidOccupantForGlide` keeps the Active bypass branch unchanged and delegates only the `LandingPending` representability predicate.
- `TickPipeline.ShouldBlockLandingPendingActiveGlideAnchorCommit` delegates to the same predicate after its existing Active-kind, anchor-changed, pending, and solid-destination guards.
- Behavior-neutral proof is covered by `Glide_CurrentContract_LandingPendingRepresentableMatrix_UsesSharedPredicate` plus the existing guard/world-state parity tests.

## Carrier separation design

- Visual-only glide track: entity id, source visual cell, target visual cell, height, progress, overflight path, phase; never creates `MoveEntity`.
- Authoritative anchor commit intent: entity id, source cell, destination cell, current glide phase, legality/settlement basis, and whether it may create `MoveEntity`.
- Rejected/block reason: structured deterministic code first; trace text is derived diagnostics.
- Determinism state: `EnemyGlideRuntimeState`, authoritative kinematic state, and final anchors stay canonical; visual-only height/path/progress stay out of hash unless explicitly made authoritative later.

## Current-contract inventory tests

Added behavior-neutral tests with `CurrentContract` names:

- `Glide_CurrentContract_ActiveCanAnchorOnSolid`
- `Glide_CurrentContract_LandingPendingRepresentableMatrix_UsesSharedPredicate`
- `Glide_CurrentContract_ActiveOriginSurvivesStateBoundary`
- `Glide_CurrentContract_AnchorCommitMaterializesMoveEntity`
- `Glide_CurrentContract_PresentationSignalDoesNotOwnWorldState`

These tests intentionally document old contracts and are rewrite/delete candidates during B-style and C2 migration.

## Target test plan

| Test | Purpose | Given/When/Then | Current | After B | After C2 | Core? | Trace? | Hash? |
|---|---|---|---|---|---|---|---|---|
| `Glide_Target_PendingDoesNotCreateMoveEntity` | Pending authority removal | Pending overlap, tick, no pending `MoveEntity` | Fail | Pass | Pass | Yes | No | Optional |
| `Glide_Target_NoStaleActiveOriginCommitAfterBoundary` | Drop active-origin carry-over | Active-started pose crosses phase boundary, no Active commit | Fail | Pass | Pass | Yes | No | Optional |
| `Glide_Target_LandingPending_NoSolidAnchorCommit` | Remove pending solid anchor | Pending with solid destination, blocked/no commit | Fail | Pass | Pass | Yes | No | Optional |
| `Glide_Target_Recovery_NoActiveOriginCommit` | No Active commit in recovery | Recovery/cooldown with stale pose, no Active commit | Partial | Pass | Pass | Yes | No | Optional |
| `Stage31_Target_NoStaleActiveCommitToFloor37Wall` | stage-3-1 safety | enemy 241/wall 238 scenario, no wall anchor | Likely fail | Pass | Pass | Yes | No | Yes |
| `Glide_Target_TraversesOverSolid_ButAnchorsOnlyOnNonSolid` | C2 traversal-only | active glide crosses solid, final anchor non-solid | Fail | Maybe fail | Pass | Yes | No | Optional |
| `Glide_Target_PresentationPathCanCrossSolid_WithoutWorldStateOverlap` | visual-only overflight | visual path crosses solid, no Unit+Solid state | Fail | Maybe fail | Pass | Yes | No | Optional |
| `Glide_Target_WorldStateNeverAllowsGliderUnitSolidOverlap` | hard invariant | direct `MoveEntity` to solid for glider throws | Fail | Maybe fail | Pass | Yes | No | No |
| `Glide_Target_ActiveSolidCell_IsTraversalOnlyNotAnchor` | active solid cell no anchor | active traversal across wall, anchor stays legal | Fail | Maybe fail | Pass | Yes | No | Optional |
| `Glide_Target_Stage31_GliderCannotMaterializeWall238Anchor` | asset regression | stage-3-1 run, enemy 241 never anchors wall 238 | Fail | Maybe pass | Pass | Yes | No | Yes |
| `Glide_Target_DeterminismStableAfterNoSolidAnchorContract` | replay stability | repeated no-solid-anchor run, stable hash | Fail | Maybe pass | Pass | Yes | No | Yes |

Target tests must remain docs-only or `[Explicit]` until the matching behavior change lands.

## Core lane promotion candidates

- No final Unit+Solid overlap.
- Pending does not create `MoveEntity`.
- No stale active-origin commit after state boundary.
- stage-3-1 shape does not materialize wall 238 anchor.
- Determinism hash stable for repeated glide run.

## Recommended implementation sequence

1. B-style cleanup first: remove pending movement authority and active-origin carry-over after current-contract inventory is green.
2. Split presentation-only track from authoritative anchor commit intent.
3. Move to C2: `GlideOverSolid` means traversal and presentation overflight only.
4. Promote the no-solid-anchor target tests into core once C2 is implemented.

Direct C2 is not recommended yet because it breaks Active solid anchor, `LandingPending` overlap preserve, egress-from-solid, matching-wall allow, guard matrix, and stage-3-1 regression coverage at once.

## Behavior change status

- Production behavior changed: no intended behavior change.
- Test expectation changed: no existing expectation changed.
- `WorldState` invariant changed: no.
- `WorldPlacementPolicy` widened: no.
