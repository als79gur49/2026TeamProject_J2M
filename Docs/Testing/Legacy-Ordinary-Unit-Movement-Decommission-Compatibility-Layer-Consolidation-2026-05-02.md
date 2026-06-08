# Legacy Ordinary Unit Movement Decommission: Compatibility Layer Consolidation

Date: 2026-05-02

## Executive Decision

This is the unified `Legacy Compatibility Layer Consolidation` package.
The project stops extending the tiny Phase 8F/8G/8H chain and treats the remaining fallback compatibility layer as one inventory and prioritization target.
Covered fallback authorization is already removed for covered player ordinary, enemy ordinary, and Charge active fallback.

Removed fallback diagnostics canonical migration is complete.
`RemovedLegacyFallbackDiagnosticsEnabled` is the canonical runtime flag, and `RemovedLegacyFallbackDiagnosticsEnabled=` is the canonical trace token.
No old-name compatibility projection remains.
The diagnostics flag does not re-enable ordinary fallback; it only selects the explicit-baseline diagnostic or the removed player/enemy/Charge diagnostic.

`TickEntityMotionKind.Move` is not deleted in this package.
`TickEntityMotionKind.Move` is an ownership-narrowing target, not a deletion target; retained grid/generic presentation remains protected.
The follow-up Charge presentation package removed the legacy Charge entity-motion enum, timing, authoring, host consumers, and synthetic compatibility.
Current Charge presentation is `TickKinematicMotionTrack(MotionMode.Charge)` plus `TickEnemyChargePresentationSignal`.
`MoveEntity`, `MovementExpander`, retained grid transactions, and glide flag-off fallback are protected and are not ordinary fallback cleanup targets.

## Move Presentation Inventory

`TickEntityMotionKind.Move` is retained for retained grid transaction and generic presentation paths.
The current boundary is ownership narrowing between fallback-only producers and retained grid producers; it is an ownership-narrowing target, not a deletion target.
`TickEntityMotionKind.Move` is not a deletion candidate in this consolidation.
The retained grid transaction boundary kinds remain protected for topology, box/action, spawn/respawn, cleanup, scripted relocation, anchor normalization.
That inventory prevents ordinary fallback confusion while preserving required presentation behavior.
glide flag-off fallback remains separate after default adoption.
The default adoption approved record remains separate; glide default adoption must include its own rollback policy.

## Current State Summary

Phase 3 moved covered fallback diagnostics out of `GameplayRuntimeFeatureFlags.None` and into the explicit diagnostic baseline path.
Phase 4 removed player covered fallback authorization.
Phase 5 removed enemy ordinary covered fallback authorization.
Phase 6 removed Charge active covered fallback authorization.
Covered attempts now reject with `PlayerLegacyFallbackRemovedFromRuntime`, `EnemyLegacyFallbackRemovedFromRuntime`, or `ChargeLegacyFallbackRemovedFromRuntime`.
Phase 8B and Phase 8C made `RemovedLegacyFallbackDiagnosticBaseline` the canonical preset.
Phase 8D made `RemovedLegacyFallbackDiagnosticsEnabled` the canonical helper.
Phase 8E completes the underlying field and trace vocabulary migration to canonical removed-fallback diagnostics naming.

## Retained And Removed Inventory

| item | kind | current status | action |
|---|---|---|---|
| `RemovedLegacyFallbackDiagnosticBaseline` | preset | canonical removed-diagnostic preset | keep |
| `RemovedLegacyFallbackDiagnosticsEnabled` | runtime flag | canonical removed-diagnostic routing field | keep |
| `RemovedLegacyFallbackDiagnosticsEnabled=` | trace token | canonical diagnostic routing trace token | keep |
| removed diagnostic reasons | diagnostics | current runtime contract | keep canaries |
| obsolete helper surface | test helpers | deleted by C안 final cleanup | keep canonical removed-diagnostic helpers |
| `TickEntityMotionKind.Move` | presentation enum | retained by grid/item/topology paths | defer ownership narrowing |
| legacy Charge entity-motion presentation | removed presentation enum | removed | keep removal record |
| `MoveEntity` | runtime primitive | retained | exclude from ordinary fallback cleanup |
| `MovementExpander` | expansion component | retained | exclude from ordinary fallback cleanup |
| retained grid transaction boundary kinds | boundary metadata | retained | protect with canaries |
| glide flag-off fallback | exception | separate rollback policy | retain until separate owner decision |

## Do Now / Defer / Retained Buckets

| bucket item | this patch decision | reason | next action |
|---|---|---|---|
| stale docs/test wording final cleanup | do now | current-policy docs must not imply covered fallback authorization | lock wording in this doc, readiness, and ADR |
| current-policy stale fallback-authorization wording detection | do now | historical docs remain, current docs should be precise | keep removed-diagnostic vocabulary |
| removed diagnostic replay canary consolidation | do now | protects canonical trace vocabulary | keep replay canary |
| duplicate wrapper test cleanup candidates | inventory only | deletion expands scope | record candidates and blockers |
| replay/golden rewrite | completed for current trace vocabulary | trace token moved to canonical diagnostics naming | keep deterministic canaries |
| `TickEntityMotionKind.Move` cleanup | defer | retained grid presentation dependency | narrow grid presentation ownership first |
| legacy Charge entity-motion deletion | done | producer isolation and consumer deletion completed | keep no-output canaries |

## Charge Presentation Removal Record

| lane | current result |
|---|---|
| `TickResultBuilder.TryResolveMotionKind` | active Charge no longer changes `MovementSemanticKind.Move`; generic Move remains `TickEntityMotionKind.Move` |
| `DefaultGameplayLocomotion` | Charge active step uses `TickKinematicMotionTrack(MotionMode.Charge)` plus `TickEnemyChargePresentationSignal` |
| `EnableEnemyChargeKinematicLocomotion` | Charge active step uses kinematic payload/track |
| `GameplayRuntimeFeatureFlags.None` | covered Charge fallback attempt rejects before legacy expansion |
| `RemovedLegacyFallbackDiagnosticBaseline` | covered Charge fallback attempt rejects with `ChargeLegacyFallbackRemovedFromRuntime` |
| `AllKinematicLocomotionEnabled` | Charge active step uses the same kinematic track/signal path |

Current-policy docs must describe Charge presentation as kinematic track plus Charge signal.
Historical docs may mention the old `ChargeMove` term only as removed legacy vocabulary.
Synthetic Charge entity-motion compatibility is no longer retained behavior.
