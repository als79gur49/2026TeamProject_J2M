# Enemy AI Summon Replay/Export Compatibility Plan

## 1. Decision Summary

- This is a compatibility plan only.
- Option B compile skeleton exists: `EnemyBehaviorModuleKey.Summon`, `EnemySummonBehaviorModuleAsset`, and a fixed typed Summon runtime config slot are implemented.
- Mutable Summon behavior state and runtime emission are implemented for the test-local Behavior Summon path; production asset migration is not implemented.
- Utility `SummonMinion` remains in the Utility capability lane.
- Replay/export-visible names are not changed by this document.
- Initial Option B preserves external replay/export names.
- Neutral naming requires an explicit replay/export schema or version migration.
- Full lane was not run for this plan.

## 2. Current Replay / Export / Hash Surface Inventory

| Surface | Current Name/Field | Owner | Replay-visible? | Hash-visible? | Export-visible? | Future Policy Candidate | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Summon commit event | `SummonCommitted|Source=...|Effect=...|SpawnIndex=...|Spawned=...|Pos=(x,y)|Archetype=...|Tick=...` | `EntitySpawnMaterializer` | Yes | Yes, through `EventLog` | Yes | Preserve | Event string is included in `TickResult.EventLog` and determinism hash input. |
| Summon skip event | `SummonSkipped|Source=...|Effect=...|SpawnIndex=...|Reason=...|Tick=...` | `EnemyUtilityResolver` / `EntitySpawnMaterializer` | Yes | Yes, through `EventLog` | Yes | Preserve | Current reasons include source invalid, max alive, and no candidate cell paths. |
| Utility trigger trace | `PreMovement.UtilityTriggers` with `Source`, `Effect`, `Kind=SummonMinion`, `Tick` | `TickTraceFormatter` | Yes | No direct canonical hash section | Yes, through trace export | PreserveWithAlias | Behavior migration should keep this as an initial compatibility alias. |
| Utility state trace/hash | `EnemyUtilities` / `Final.EnemyUtilities` | `WorldSnapshot`, `TickTraceFormatter`, `DeterminismHashBuilder` | Yes | Yes | Yes | RenameRequiresMigration | Current Summon timing state lives here until a paired state migration exists. |
| Summoned child metadata | `SummonedEntities`, `E=...|Source=...|Effect=...` | `SummonedEntityState` / materializer | Yes | Yes | Yes | Preserve | Used by max-alive and replay/hash parity. |
| Spawned archetype binding | `EnemyDefinitionBindings`, `E=...|Archetype=...` | `EnemyDefinitionBindingState` / materializer | Yes | Yes | Yes | Preserve | Presentation binding and determinism input. |
| Final entity export | spawned entity id, order, position, HP, team, facing, spawn tick, AI mode | `TickResultData.FinalEntities` | Yes | Yes | Yes | Preserve | Entity id allocation happens only after placement succeeds. |
| Finalization operation trace | `SpawnEntity` with `SummonSource`, `SummonEffect`, `Archetype` | `FinalizationBatch` / `TickTraceFormatter` | Yes | Indirect through final state/event log | Yes | Preserve | Diagnostic trace should remain comparable during migration. |
| Request source snapshot | `SourceEntityId`, `SourceEffectIndex`, `TriggerTick`, `OriginCell`, `SourceFacing`, `SourceTeamId` | `EntitySpawnRequestSource` | Indirect | Indirect through outputs | Indirect | Preserve | Request has no allocated entity id and must not become a live source view. |
| Request order | source id, effect index, trigger tick; materializer preserves received order | Utility trigger resolver and materializer | Yes through events/ids | Yes | Yes | Preserve | Same-tick multi-summoner ordering already characterizes this. |
| Presentation binding carrier | spawned metadata consumed by presentation resolver | Presentation layer | Presentation-visible | No direct hash change | Presentation export if traced | InternalOnly | Presentation must not mutate authoritative simulation. |

## 3. Preserved vs Migration-Required Names

| Name/Field | Current Meaning | Future Desired Meaning | Policy | Compatibility Risk | Required Evidence |
| --- | --- | --- | --- | --- | --- |
| `SummonCommitted` | Successful summon materialization event | Same external event | Do not rename | High | Replay/event/hash parity. |
| `SummonSkipped` | Failed summon materialization or source validation event | Same external event | Do not rename | High | Failed placement and source invalid parity. |
| `SummonedEntities` | Canonical child source metadata section | Same child metadata section | Do not rename | High | Hash trace parity. |
| `EnemyDefinitionBindings` | Spawned enemy archetype binding section | Same archetype binding section | Do not rename | High | Binding hash parity. |
| spawned entity id/order export | Materialization order and id allocation result | Same deterministic output | Do not rename | High | Request order replay parity. |
| `SummonedEntityState` source metadata | Child source id and source effect index | Same external shape | Do not rename | High | Source metadata export parity. |
| `EnemyDefinitionBindingState` archetype binding | Spawned child archetype id | Same binding | Do not rename | High | Archetype binding parity. |
| `PreMovement.UtilityTriggers` | Utility trigger trace section | Initial Option B compatibility alias | Keep old name through migration | Medium | `BehaviorSummon_UtilityTriggerAliasPreserved_IfPolicyA`. |
| `EnemyUtilities` Summon state | Utility-owned mutable timing state | Removed only when replaced by behavior state hash | Split hash only with baseline migration | High | No double-count and no missing-count tests. |
| future behavior state section | None today | Behavior-owned Summon state hash | New section only with migration | High | Determinism hash parity against Utility baseline. |
| `Effect=` | Utility effect index in event/trace/export text | Compatibility source slot index | Keep old name through migration | High | Replay names and metadata parity. |
| `SourceEffectIndex` | Utility effect index in request and child metadata | Compatibility source slot index | Keep old field externally | High | Export schema compatibility. |
| future neutral trigger trace name | None today | Behavior-neutral trigger trace | New name only after replay version bump | High | Explicit schema/version migration. |
| future Behavior module source vocabulary | None today | Internal Behavior source key/index/slot | Internal model only until migration | Medium | Source vocabulary tests. |

## 4. Source Metadata Vocabulary Policy

Current source metadata:

- `SourceEntityId`.
- `SourceEffectIndex`.
- `TriggerTick`.
- `SpawnIndex`.
- `OriginCell`.
- `SourceFacing`.
- `SourceTeamId`.

Future Behavior source candidates:

- `SourceEntityId`.
- `SourceBehaviorModuleKey`.
- `SourceBehaviorModuleIndex`.
- `SourceBehaviorSlot`.
- `TriggerTick`.
- `SpawnIndex`.
- `OriginCell`.
- `SourceFacing`.
- `SourceTeamId`.

Recommended policy:

- Do not rename replay/export-visible `Effect=` or `SourceEffectIndex` for initial Option B.
- Future Behavior internals may introduce a source-neutral slot concept, but the compatibility adapter must map that slot into the existing external `Effect` / `SourceEffectIndex` field.
- Keep `SummonedEntityState(sourceEntityId, sourceEffectIndex)` unchanged until a separate replay/export schema migration replaces it.
- Keep `EntitySpawnRequestSource` as captured request snapshot metadata. It must not carry an allocated entity id or a live source reference.
- If a new metadata type is introduced later, old replay compatibility must preserve the old field names and provide explicit versioned migration evidence.

Alternative considered:

- Rename `SourceEffectIndex` to `SourceSlotIndex` immediately.
- This is rejected for initial Option B because current event strings are hash-visible and replay tests assert `Effect=0` directly.

Required tests:

- `BehaviorSummon_SourceMetadataExportParity`.
- `BehaviorSummon_RequestOrderReplayParity`.
- `MigratedSummon_ExportSchemaCompatibility`.
- `MigratedSummon_NoUtilitySummonResidueButReplayNamesPreserved`.

## 5. Determinism Hash Migration Strategy

| Hash Section | Current Utility Summon Content | Future Behavior Summon Content | Policy | Risk | Required Test |
| --- | --- | --- | --- | --- | --- |
| `EnemyUtilities` | entity id, effect index, effect kind, cooldown, phase, windup start/end, active fields, active origin, recover start/end, activation sequence, movement suppression | None after migrated Summon state leaves Utility | Keep before Option B; remove Summon content only in the same reviewed migration that adds behavior state hash | Double-count or missing-count | `MigratedSummon_NoDoubleCountUtilityAndBehaviorState`. |
| `EnemySummonBehaviors` | None for Utility-owned production Summon | cooldown, phase/timing, activation sequence, and movement suppression for test-local Behavior Summon runtime state | Additive until production migration; Utility hash section remains unchanged | Hash rename drift | `BehaviorSummon_StateHashIncluded`. |
| `SummonedEntities` | child entity id, source entity id, source effect index | Same | Preserve | Child tracking drift | `BehaviorSummon_SummonedEntitiesHashParity`. |
| `EnemyDefinitionBindings` | child entity id, archetype id | Same | Preserve | Archetype binding drift | `BehaviorSummon_EnemyDefinitionBindingsHashParity`. |
| `Entities` / final entities | spawned id, position, HP, team, facing, spawn tick, AI mode | Same | Preserve | Id allocation or placement drift | `BehaviorSummon_RequestOrderReplayParity`. |
| `EventLog` | `SummonCommitted` / `SummonSkipped` strings and fields | Same external strings | Preserve | Hash changes from rename | `BehaviorSummon_ReplayNames_PreserveSummonCommittedAndSkipped`. |

Migration principles:

- Option B must keep `EnemyUtilities` unchanged until the real behavior state hash section exists.
- Removing Utility Summon state and adding Behavior Summon state must be treated as one compatibility-reviewed migration.
- `SummonedEntities` and `EnemyDefinitionBindings` must remain stable across migration.
- Event log naming is part of determinism input and must not be renamed without an explicit baseline/schema migration.
- Old/new baseline parity tests must compare determinism hashes, event log text, trace text, final entities, source metadata, and archetype bindings.

## 6. Baseline Capture Plan

| Baseline | Scenario | Captured Artifacts | Used For | Required Before Option B? |
| --- | --- | --- | --- | --- |
| Utility Summon single spawn | one source, one spawn | seed/setup, tick range, event log, phase trace, determinism hash, final entity list, child metadata, archetype binding | Default replay/export parity | Yes |
| Utility Summon multi-spawn same source | one source, multiple `SpawnIndex` values | event order, spawned ids, placements, reserved cells, final entities | Per-source request order and id allocation | Yes |
| same-tick multi-summoner | two sources commit on same tick | trigger trace, event order, spawned ids, placements | Cross-source ordering parity | Yes |
| request payload snapshot | source changes after request metadata capture | origin, facing, team, placement, event fields | Mutable request drift guard | Yes |
| max alive baseline | alive child blocks, dead/non-occupying child ignored | event absence/presence, child metadata, hash | Max-alive parity | Yes |
| source death/cancel baseline | source invalid after trigger | `SummonSkipped SourceInvalid`, final entities, hash | Source validation parity | Yes |
| topology participation suspend/cancel | source leaves/re-enters bottom face during windup | phase trace, event timing, hash | Topology participation parity | Yes |
| failed placement baseline | no candidate cell | `SummonSkipped NoCandidateCell`, no id allocation, hash | Failed placement parity | Yes |
| hazard fallback placement | risky candidates with neutral-first/fallback behavior | placement, final entities, hash | Placement resolver ownership parity | Yes |
| UtilityArchetypeSummon replay | summoned archetype with binding/defaults | event log, final entity stats, `EnemyDefinitionBindings` | Archetype binding and defaults parity | Yes |

## 7. Compatibility Options

| Option | Pros | Cons | Replay Risk | Recommendation |
| --- | --- | --- | --- | --- |
| A. External replay/export names fully preserved | Lowest drift; matches current replay tests and hash input | Utility-flavored trace labels remain | Low | Selected for initial Option B. |
| B. Dual-label compatibility period | Allows neutral naming transition | Requires export schema/versioning and duplicate surface policy | Medium | Defer to explicit schema migration. |
| C. Rename to neutral Behavior labels immediately | Cleaner architecture names | Breaks old replay/export/hash expectations | High | Reject for initial Option B. |

Decision:

- Initial Option B uses Option A.
- Do not rename `SummonCommitted` or `SummonSkipped`.
- Do not rename `SummonedEntities` or `EnemyDefinitionBindings`.
- Keep `PreMovement.UtilityTriggers` as a compatibility alias during initial Option B.
- Defer neutral naming to explicit replay/export schema migration.

## 8. Future Test Matrix

| Test | Current/Future/Migration | Purpose | Required Before Option B Implementation? |
| --- | --- | --- | --- |
| Utility Summon replay | Current already covered | Baseline replay contract | Already recorded |
| UtilityArchetypeSummon replay | Current already covered | Baseline archetype binding replay contract | Already recorded |
| same-tick multi-summoner ordering | Current already covered | Baseline request/id allocation order | Already recorded |
| request payload snapshot | Current already covered | Captured request metadata does not drift | Already recorded |
| Core lane | Current already covered | Existing touched-cluster baseline evidence | Already recorded; rerun when code changes require it |
| `BehaviorSummon_ReplayNames_PreserveSummonCommittedAndSkipped` | Future | Preserve event names and fields | Yes |
| `BehaviorSummon_SummonedEntitiesHashParity` | Future | Preserve child source metadata hash | Yes |
| `BehaviorSummon_EnemyDefinitionBindingsHashParity` | Future | Preserve archetype binding hash | Yes |
| `BehaviorSummon_SourceMetadataExportParity` | Future | Preserve `Effect` / `SourceEffectIndex` external fields | Yes |
| `BehaviorSummon_RequestOrderReplayParity` | Future | Preserve request order and spawned id allocation | Yes |
| `BehaviorSummon_FailedPlacementSummonSkippedParity` | Future | Preserve `NoCandidateCell` skip behavior | Yes |
| `BehaviorSummon_MaxAliveReplayParity` | Future | Preserve max-alive event/hash behavior | Yes |
| `BehaviorSummon_SourceDeathCancelReplayParity` | Future | Preserve source invalid skip/cancel behavior | Yes |
| `BehaviorSummon_TopologyParticipationReplayParity` | Future | Preserve topology suspend/cancel timing | Yes |
| `BehaviorSummon_UtilityTriggerAliasPreserved_IfPolicyA` | Future | Preserve old trigger trace alias | Yes |
| `BehaviorSummon_NewBehaviorTraceRequiresVersionBump_IfPolicyBOrC` | Future | Prevent unversioned neutral rename | Yes if Option B/C is chosen later |
| `MigratedSummon_ReplayParityAgainstUtilityBaseline` | Migration | Compare migrated replay to Utility baseline | Before migration close |
| `MigratedSummon_DeterminismHashParityAgainstUtilityBaseline` | Migration | Compare migrated hash to Utility baseline or approved diff | Before migration close |
| `MigratedSummon_ExportSchemaCompatibility` | Migration | Verify old export consumers remain compatible | Before migration close |
| `MigratedSummon_NoDoubleCountUtilityAndBehaviorState` | Migration | Prevent state hash duplication | Before migration close |
| `MigratedSummon_NoUtilitySummonResidueButReplayNamesPreserved` | Migration | Ensure asset source moved while external names remain | Before migration close |
| `GravityFieldAura_ReplayUnaffected` | Migration | Guard non-goal Utility effect | Before migration close |
| `RetiredLockNearbyBoxes_GuardUnaffected` | Migration | Guard retired compatibility path | Before migration close |

## 9. Dependencies

- Spawn/entity creation seam: [Enemy-AI-Summon-Spawn-Seam-Implementation-Note.md](./Enemy-AI-Summon-Spawn-Seam-Implementation-Note.md).
- Duplicate Utility/Behavior Summon guard: [Enemy-AI-Summon-Duplicate-Guard-Design.md](./Enemy-AI-Summon-Duplicate-Guard-Design.md).
- Future runtime state shape: [Enemy-AI-Summon-Behavior-Runtime-State-Design.md](./Enemy-AI-Summon-Behavior-Runtime-State-Design.md).
- Asset migration plan: [Enemy-AI-Summon-Asset-Migration-Plan.md](./Enemy-AI-Summon-Asset-Migration-Plan.md).
- Presentation/audio/VFX parity plan: [Enemy-AI-Summon-Presentation-Audio-VFX-Parity-Plan.md](./Enemy-AI-Summon-Presentation-Audio-VFX-Parity-Plan.md).
- Full lane / CI release gate policy.

## 10. Explicit Non-Goals

- No concrete `SummonBehaviorRuntime`.
- No mutable Summon runtime implementation or trigger emission.
- No Utility `SummonMinion` production asset migration.
- No replay/export-visible rename in this step.
- No `DeterminismHashBuilder` runtime behavior change.
- No `TickPipeline` runtime behavior change.
- No `EntitySpawnRequest` or `EntitySpawnMaterializer` contract change.
- No Utility whole-lane migration.
- No `GravityFieldAura` changes.
- No `RetiredLockNearbyBoxes` changes.
- No generic registry.
- No `logicModuleAssets`.
- No direct `WorldState` spawn writes.

## 11. Option B Entry Criteria

- Replay/export compatibility plan accepted.
- Asset migration plan accepted.
- Duplicate Utility/Behavior Summon guard accepted.
- Runtime state design accepted.
- Presentation/audio/VFX parity plan accepted; see [Enemy-AI-Summon-Presentation-Audio-VFX-Parity-Plan.md](./Enemy-AI-Summon-Presentation-Audio-VFX-Parity-Plan.md).
- Option B implementation slicing and validation gates accepted; see [Enemy-AI-Summon-Option-B-Implementation-Plan.md](./Enemy-AI-Summon-Option-B-Implementation-Plan.md).
- Full lane / CI release gate policy decided.

Summon replay/export compatibility is implemented for the test-local Behavior Summon runtime/emitter path. `SummonCommitted`, `SummonSkipped`, `SummonedEntities`, `EnemyDefinitionBindings`, `Effect=`, and `SourceEffectIndex` names remain preserved. Production asset migration has not started, and full lane was not run unless explicitly reported.
