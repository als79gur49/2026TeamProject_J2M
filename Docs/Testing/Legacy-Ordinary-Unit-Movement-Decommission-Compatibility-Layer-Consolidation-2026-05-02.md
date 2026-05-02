# Legacy Ordinary Unit Movement Decommission: Compatibility Layer Consolidation

Date: 2026-05-02

## Executive Decision

This is the unified `Legacy Compatibility Layer Consolidation` package.
The project stops extending the tiny Phase 8F/8G/8H chain and treats the remaining fallback compatibility layer as one inventory and prioritization target.
Covered fallback authorization is already removed for covered player ordinary, enemy ordinary, and Charge active fallback.
This package does not change runtime validation semantics.
`EnableLegacyOrdinaryUnitFallback`, `LegacyOrdinaryFallbackBaseline`, `LegacyOrdinaryFallbackEnabled`, and the `LegacyFallback=` trace token remain compatibility surface.
`TickEntityMotionKind.Move` is not deleted in this package.
The follow-up Charge presentation package removed the legacy Charge entity-motion enum, timing, authoring, host consumers, and synthetic compatibility.
Current Charge presentation is `TickKinematicMotionTrack(MotionMode.Charge)` plus `TickEnemyChargePresentationSignal`.
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
| legacy Charge entity-motion presentation | removed presentation enum | removed | no | current Charge uses kinematic track plus signal | record historical removal | golden/presentation drift | presentation and golden owner | Removed |
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
| Charge presentation removal record | do now | Charge entity-motion compatibility is removed | keep current-policy docs on kinematic track plus signal |
| removed diagnostic replay canary consolidation | do now | protects no golden rewrite | add consolidation replay canary |
| duplicate wrapper test cleanup candidates | inventory only | deletion expands scope | record candidates and blockers |
| `EnableLegacyOrdinaryUnitFallback` rename | defer | API, constructor, and golden churn | require compatibility field package approval |
| `LegacyFallback=` rename | defer | trace/golden rewrite required | require trace vocabulary owner approval |
| replay/golden rewrite | defer | consolidation is no-rewrite | require golden owner approval |
| alias/helper removal | defer | compatibility API is retained | require all callers and historical references cleanup |
| `TickEntityMotionKind.Move` cleanup | defer | retained grid presentation dependency | narrow grid presentation ownership first |
| legacy Charge entity-motion deletion | done | producer isolation and consumer deletion completed | keep no-output canaries |

## Charge Presentation Removal Record

Producer inventory result:

| lane | current result |
|---|---|
| `TickResultBuilder.TryResolveMotionKind` | active Charge no longer changes `MovementSemanticKind.Move`; generic Move remains `TickEntityMotionKind.Move` |
| `DefaultGameplayLocomotion` | Charge active step uses `TickKinematicMotionTrack(MotionMode.Charge)` plus `TickEnemyChargePresentationSignal` |
| `EnableEnemyChargeKinematicLocomotion` | Charge active step uses kinematic payload/track |
| `GameplayRuntimeFeatureFlags.None` | covered Charge fallback attempt rejects before legacy expansion |
| `RemovedLegacyFallbackDiagnosticBaseline` | covered Charge fallback attempt rejects with `ChargeLegacyFallbackRemovedFromRuntime` |
| `AllKinematicLocomotionEnabled` | Charge active step uses the same kinematic track/signal path |

Consumer deletion result:

| area | deleted | retained |
|---|---|---|
| presentation enum/data | legacy Charge entity-motion value | `TickEntityMotionKind.Move` |
| timing | dedicated Charge entity-motion duration config | generic motion timing and Charge kinematic/AI timing |
| authoring | per-entity Charge entity-motion duration override | generic entity motion authoring |
| host/presenter | Charge entity-motion interpolation branch | kinematic track and Charge signal handling |
| tests | synthetic Charge entity-motion compatibility fixtures | no-output, kinematic track, Charge signal, replay canaries |

Current-policy docs must describe Charge presentation as kinematic track plus Charge signal.
Historical docs may mention the old `ChargeMove` term only as removed legacy vocabulary.
Synthetic Charge entity-motion compatibility is no longer retained behavior.

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
| `TickReplayDeterminismTests` | Charge entity-motion synthetic fixture removed | canonical hash unaffected | no | no for consolidation | keep canonical presentation-only hash tests |
| player replay | no covered fallback output; removed diagnostic deterministic | `RemovedLegacyFallbackDiagnosticBaseline` rejects | no | no | add no-rewrite statement |
| enemy replay | no covered ordinary fallback output; glide exception separate | deterministic removed diagnostics | no | no | keep replay helper |
| charge replay | no legacy Charge entity-motion output | `ChargeLegacyFallbackRemovedFromRuntime` | no | no | keep no-output canary |
| boundary inventory | diagnostics and protected grid branches | Phase 8E canaries exist | no | no | extend with consolidation doc canaries |
| movement phase | fallback absence plus retained grid movement | current canaries mixed | no | no | inventory only |
| presentation tests | `Move` consumer contracts plus Charge kinematic track/signal | host/presentation contracts remain | no | no for this deletion | keep canonical canaries |

## Next Recommended Implementation Package

The next implementation package should continue narrowing retained generic `Move` presentation ownership without touching grid transactions.
It must not include `TickEntityMotionKind.Move` deletion, `MoveEntity` deletion, `MovementExpander` deletion, retained grid transaction rewrites, glide default adoption, or replay/golden auto-rewrite.

## Risk Register

| risk | mitigation |
|---|---|
| scope expands back into micro phases | one consolidation document and no Phase 8F label |
| accidental runtime semantics change | runtime files are not edited in this package |
| historical Charge entity-motion golden drift | no golden rewrite; owner approval required before migration |
| `Move` presentation deleted despite retained grid use | Move inventory records retained dependency and no immediate deletion |
| glide policy accidentally changed | glide retained fallback remains separate and default adoption is excluded |
| `MoveEntity` / `MovementExpander` mistaken as legacy fallback | retained bucket explicitly protects both |
| docs inventory becomes stale | canaries assert required sections and decisions |
| replay/golden churn | no golden rewrite; replay canary asserts deterministic diagnostics |
