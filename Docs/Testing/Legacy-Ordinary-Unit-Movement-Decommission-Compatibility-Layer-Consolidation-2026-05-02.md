# Legacy Ordinary Unit Movement Decommission: Compatibility Layer Consolidation

Date: 2026-05-02

## Executive Decision

This is the unified `Legacy Compatibility Layer Consolidation` package.
The project stops extending the tiny Phase 8F/8G/8H chain and treats the remaining fallback compatibility layer as one inventory and prioritization target.
Covered fallback authorization is already removed for covered player ordinary, enemy ordinary, and Charge active fallback.
This package does not change runtime validation semantics.
`EnableLegacyOrdinaryUnitFallback`, `LegacyOrdinaryFallbackBaseline`, `LegacyOrdinaryFallbackEnabled`, and the `LegacyFallback=` trace token remain compatibility surface.
`TickEntityMotionKind.Move` and `TickEntityMotionKind.ChargeMove` are not deleted in this package.
`TickEntityMotionKind.ChargeMove` is the first concrete cleanup candidate, but the immediate action is inventory-to-action, not immediate deletion.
`MoveEntity`, `MovementExpander`, retained grid transactions, and glide retained fallback are protected and are not ordinary fallback cleanup targets.
No replay or golden files are rewritten in this consolidation.

## Current State Summary

Phase 3 moved covered fallback diagnostics out of `GameplayRuntimeFeatureFlags.None` and into the explicit diagnostic baseline path.
Phase 4 removed player covered fallback authorization.
Phase 5 removed enemy ordinary covered fallback authorization.
Phase 6 removed Charge active covered fallback authorization.
Covered attempts now reject with `PlayerLegacyFallbackRemovedFromRuntime`, `EnemyLegacyFallbackRemovedFromRuntime`, or `ChargeLegacyFallbackRemovedFromRuntime`.
Phase 7 and Phase 8A aligned helper and test vocabulary around removed diagnostics while keeping obsolete `Allows*` wrappers as compatibility wrappers.
Phase 8B and Phase 8C made `RemovedLegacyFallbackDiagnosticBaseline` the canonical preset while preserving `LegacyOrdinaryFallbackBaseline` as a deprecated compatibility alias.
Phase 8D made `RemovedLegacyFallbackDiagnosticsEnabled` the canonical helper while preserving `LegacyOrdinaryFallbackEnabled` as a deprecated compatibility alias.
Phase 8E kept `EnableLegacyOrdinaryUnitFallback` as the underlying compatibility diagnostic field and kept the `LegacyFallback=` trace token for golden stability.

## Compatibility Layer Inventory

| item | kind | current status | still needed? | reason | cleanup action | risk | owner / blocker | recommended bucket |
|---|---|---|---|---|---|---|---|---|
| `RemovedLegacyFallbackDiagnosticBaseline` | preset | canonical removed-diagnostic preset | yes | deterministic removed diagnostics | keep canonical in docs and tests | low | none | Never delete / Retained |
| `LegacyOrdinaryFallbackBaseline` | preset alias | deprecated compatibility alias | yes for now | historical tests, docs, and migration naming | confirm current-policy new usage stays out | alias deletion churn | replay/migration owner | Defer |
| `RemovedLegacyFallbackDiagnosticsEnabled` | helper | canonical helper | yes | routes explicit-baseline gate vs removed reasons | keep helper usage | low | none | Never delete / Retained |
| `LegacyOrdinaryFallbackEnabled` | helper alias | deprecated delegate alias | yes for now | compatibility API | confirm internal usage remains definition/historical only | external/test churn | API owner | Defer |
| `EnableLegacyOrdinaryUnitFallback` | field | underlying compatibility field | yes for now | struct shape, named arguments, trace/golden stability | document no rename/delete | constructor churn | runtime API and replay owner | Defer |
| `LegacyFallback=` trace token | trace vocabulary | kept for golden stability | yes for now | avoids deterministic trace churn | draft owner decision only | golden churn | replay/golden owner approval | Defer |
| removed diagnostic reasons | diagnostics | current runtime contract | yes | proves covered fallback authorization is removed | keep canaries | reason rename churn | runtime/test owner | Never delete / Retained |
| obsolete `Allows*` wrappers | test helpers | obsolete compatibility wrappers | yes for now | historical wrapper references | preserve no-internal-caller inventory | helper deletion churn | test owner | Defer |
| Phase 3-8 wrapper tests | tests | historical compatibility tests | partial | migration history and canaries | list duplicate cleanup candidates only | stale naming | test owner | Do now inventory |
| fallback-related docs wording | docs | historical/current wording mixed | partial | avoid current-policy confusion | scan current docs for stale fallback-allowed wording | historical over-editing | docs owner | Do now |
| `TickEntityMotionKind.Move` | presentation enum | retained by grid/item/topology paths | yes | `MovementSemanticKind.Move/Item` presentation | no immediate deletion | retained presentation break | presentation/grid owner | Defer |
| `TickEntityMotionKind.ChargeMove` | presentation enum | producer, host, and tests remain | maybe | direct cleanup candidate after Charge authorization removal | inventory-to-action | golden/presentation break | presentation and golden owner | Do now inventory, deletion Defer |
| `MoveEntity` | runtime primitive | retained | yes | anchor/grid transaction primitive | exclude from cleanup | catastrophic runtime break | grid owner | Never delete / Retained |
| `MovementExpander` | expansion component | retained | yes | grid transactions and retained lanes | exclude from cleanup | grid branch regression | movement owner | Never delete / Retained |
| retained grid transaction boundary kinds | boundary metadata | retained | yes | topology, box/action, spawn/respawn, cleanup, scripted relocation, anchor normalization | protect with canary | ordinary fallback confusion | grid owner | Never delete / Retained |
| glide retained fallback | exception | separate policy | yes for now | default adoption not approved | keep separate from consolidation | accidental glide policy shift | glide owner | Never delete / Retained |

## Do Now / Defer / Retained Buckets

| bucket item | this patch decision | reason | next action |
|---|---|---|---|
| stale docs/test wording final cleanup | do now | current-policy docs must not imply fallback allowed | lock wording in this doc, readiness, and ADR |
| old alias/helper usage 0 confirmation | do now | alias/helper removal needs internal usage state | keep report canary as definition/historical only |
| current-policy stale "fallback allowed" detection | do now | historical docs remain, current docs should be precise | limit to readiness and consolidation docs |
| `ChargeMove` presentation usage inventory | do now | most direct cleanup candidate after Charge removal | add producer/consumer/test/golden blocker table |
| removed diagnostic replay canary consolidation | do now | protects no golden rewrite | add consolidation replay canary |
| duplicate wrapper test cleanup candidates | inventory only | deletion expands scope | record candidates and blockers |
| `EnableLegacyOrdinaryUnitFallback` rename | defer | API, constructor, and golden churn | require compatibility field package approval |
| `LegacyFallback=` rename | defer | trace/golden rewrite required | require trace vocabulary owner approval |
| replay/golden rewrite | defer | consolidation is no-rewrite | require golden owner approval |
| alias/helper removal | defer | compatibility API is retained | require all callers and historical references cleanup |
| `TickEntityMotionKind.Move` cleanup | defer | retained grid presentation dependency | narrow grid presentation ownership first |
| `TickEntityMotionKind.ChargeMove` deletion | defer | inventory first | next package confirms reachable, historical, and golden usage |

## ChargeMove Presentation Inventory

| usage location | producer | consumer | still reachable after Phase 6? | retained/historical only? | tests covering absence | tests still expecting presence | golden/replay risk | action recommendation |
|---|---|---|---|---|---|---|---|---|
| `TickResultBuilder.TryResolveMotionKind` / `ShouldUseChargeMovePresentation` | committed `MoveEntity` plus active enemy charge without authoritative Charge pose | `TickEntityMotion` | no for covered fallback | runtime branch remains | Phase 6 and Phase 2C no-charge tests | world/presentation unit tests | medium | inventory-to-action, no deletion |
| `TickPresentationData.TickEntityMotionKind.ChargeMove` | enum value | host and presentation tests | data type reachable if producer emits | not purely historical yet | replay no-charge canaries | authoring/timing tests | enum serialization risk | defer deletion |
| `EntityMotionPresentationAuthoring` / timing config | authoring config | timing resolver | consumer only | retained while enum exists | none | override tests | prefab/config churn | keep until enum decision |
| `MotionTrack` / `GameplayMotionTimingResolver` | host playback | presentation interpolation | consumer only | retained while enum exists | none | coordinator tests | host playback churn | keep |
| `WorldSnapshotAndPresentationTests` | builder scenario | test | synthetic reachable | historical/presentation compatibility | none | expects `ChargeMove` | test rewrite risk | classify as presentation compatibility |
| `EnemyAiScenarioTests` | flag-off/legacy charge scenarios | scenario tests | must verify after Phase 6 scope | likely retained/historical mix | Phase 6 no-charge tests | multiple `ChargeMove` assertions | high | next package investigates first |
| `TickReplayDeterminismTests` | presentation-only hash test | replay hash | synthetic | retained hash contract | canonical hash unaffected | expects `ChargeMove` | low-medium | keep until trace/golden policy |
| `EnemyKinematicLocomotionReplayTests` | replay canaries | replay | no for covered fallback | absence canaries current | `Replay_Phase6_DefaultGameplay_NoChargeMove` | historical wrappers | medium | add consolidation canary |

Phase 6 means covered player/enemy/Charge fallback must not produce `ChargeMove`.
`RemovedLegacyFallbackDiagnosticBaseline` reproduces removed diagnostics, not fallback output, so it must not produce `ChargeMove`.
Default gameplay and Charge kinematic flag-on lanes must use `TickKinematicMotionTrack` plus `TickEnemyChargePresentationSignal`, not `ChargeMove`.
Flag-off/default `None` covered Charge fallback is removed after Phase 6.
No retained grid transaction is currently approved as a `ChargeMove` deletion blocker, but host, presentation, tests, and golden/replay policy remain blockers before deletion.

## Move Presentation Inventory

| usage location | producer | consumer | retained dependency | fallback-only branch remains? | tests expecting `Move` | action recommendation |
|---|---|---|---|---|---|---|
| `TickResultBuilder.TryResolveMotionKind` | `MovementSemanticKind.Move/Item` | `TickEntityMotion` | item, grid, and topology-style presentation | covered ordinary fallback is blocked by validation | many scenario/unit tests | no deletion |
| `ShouldSuppressLegacyMotionForLocomotion` | `MoveEntity` metadata | suppresses locomotion anchor/unit ordinary presentation | `LocomotionAnchorCommit`, `UnitOrdinaryLocomotion` | suppression boundary remains | boundary canaries | retain |
| `GameplayTickPresentationCoordinatorTests` | synthetic motion | host presentation | generic movement playback | not fallback-specific | many `Move` tests | keep |
| `MovementPhaseScenarioTests` / runtime board tests | retained movement scenarios | scenario validation | grid, item, topology, materialization | some old fallback names are historical | multiple `Move` expectations | inventory only |
| presentation timing/authoring | config consumer | host duration | generic move presentation | no | authoring/timing tests | keep |

`TickEntityMotionKind.Move` is tied to retained grid transaction and generic presentation paths, so it is not a deletion candidate in this consolidation.
The next `Move` work is ownership narrowing between fallback-only producers and retained grid producers, not enum deletion.

## Replay / Golden Matrix

| artifact/test area | expected legacy output | current Phase 8E behavior | golden rewrite needed? | owner approval needed? | recommendation |
|---|---|---|---|---|---|
| `TickReplayDeterminismTests` | presentation may include synthetic `ChargeMove` hash-neutral data | canonical hash unaffected | no | no for consolidation | keep as presentation-only |
| player replay | no covered fallback output; removed diagnostic deterministic | `RemovedLegacyFallbackDiagnosticBaseline` rejects | no | no | add no-rewrite statement |
| enemy replay | no covered ordinary fallback output; glide exception separate | deterministic removed diagnostics | no | no | keep replay helper |
| charge replay | no covered `ChargeMove` fallback output | `ChargeLegacyFallbackRemovedFromRuntime` | no | no | add consolidation canary |
| boundary inventory | diagnostics and protected grid branches | Phase 8E canaries exist | no | no | extend with consolidation doc canaries |
| movement phase | fallback absence plus retained grid movement | current canaries mixed | no | no | inventory only |
| presentation tests | `Move`/`ChargeMove` consumer contracts | host/presentation contracts remain | no | yes before deletion | no deletion; classify blockers |

## Next Recommended Implementation Package

The next implementation package should start with `TickEntityMotionKind.ChargeMove` usage inventory-to-action.
That package should answer which `ChargeMove` producer paths remain reachable, which tests are synthetic or historical, whether any golden owner approves vocabulary changes, and whether presentation authoring/timing consumers can be retired.
It must not delete `ChargeMove` before retained, historical, and golden blockers are resolved.
It must not include `TickEntityMotionKind.Move` deletion, `MoveEntity` deletion, `MovementExpander` deletion, retained grid transaction rewrites, glide default adoption, or replay/golden auto-rewrite.

## Risk Register

| risk | mitigation |
|---|---|
| scope expands back into micro phases | one consolidation document and no Phase 8F label |
| accidental runtime semantics change | runtime files are not edited in this package |
| `ChargeMove` deleted before golden approval | inventory says inventory-to-action and deletion deferred |
| `Move` presentation deleted despite retained grid use | Move inventory records retained dependency and no immediate deletion |
| glide policy accidentally changed | glide retained fallback remains separate and default adoption is excluded |
| `MoveEntity` / `MovementExpander` mistaken as legacy fallback | retained bucket explicitly protects both |
| docs inventory becomes stale | canaries assert required sections and decisions |
| replay/golden churn | no golden rewrite; replay canary asserts deterministic diagnostics |
