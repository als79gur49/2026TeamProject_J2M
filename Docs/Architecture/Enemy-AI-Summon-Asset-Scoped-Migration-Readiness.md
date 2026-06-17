# Enemy AI Summon Asset-Scoped Migration Readiness

## 1. Decision Summary

- This is readiness / dry-run only.
- Production assets are not migrated by this document.
- Production Summoner remains Utility-owned until an approved asset-scoped migration slice.
- Migration must use an exact allowlist, field-by-field value copy, duplicate-source guard sequencing, replay/hash review, production baseline capture, residue scans, presentation/audio/VFX parity validation, and a source-control rollback path.
- Full lane was not run for this readiness pass unless separately reported.

## 2. Current Production Asset Allowlist

| Asset | Current Role | Migration Role | Decision | Notes |
| --- | --- | --- | --- | --- |
| `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/Profiles/Enemy_ArchetypeSummoner/EnemyAi_ArchetypeSummoner.asset` | Production Summoner profile | Migrated profile candidate | Include in exact allowlist | Currently references Utility capability assets and has `behaviorModuleAssets: []`. |
| `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/Capabilities/Enemy_UtilitySummoner/EnemyCapability_ArchetypeSummoner.asset` | Active Utility Summon capability | Source field inventory for future Behavior Summon asset | Include in exact allowlist | Contains active `kind: 0` Summon source at Utility `effects[0]`. |
| `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/Capabilities/GravityFieldAura/EnemyCapability_GravityFieldAura.asset` | Active Utility Gravity capability | None | Exclude | Active `kind` is `2`; serialized nested `summon:` payload is inactive and must not be migrated. |
| Production `EnemyAI/BehaviorModules` directories | Production Behavior module content | Destination family check only | Read-only check | Charge assets exist; no production `EnemySummonBehaviorModuleAsset` is present under `Assets/_Features/Stages/Content`. |

Read-only scan policy:

- Active Utility Summon production content is limited to the exact Summoner allowlist above.
- `GravityFieldAura` remains excluded even though its serialized payload contains an inactive `summon:` block.
- `RetiredLockNearbyBoxes` is not a migration target. If active `kind: 1` appears in Utility capability context, treat it as a separate retired-guard issue.
- `logicModuleAssets` must remain absent.

## 3. Current Utility Summon Field Inventory

| Current Utility Field | Current Value | Active? | Future Behavior Field | Migration Rule | Validation |
| --- | --- | --- | --- | --- | --- |
| `kind` | `0` | Yes | Behavior module presence | Replace only in the approved migration slice | Migrated profile must not have both active Utility Summon and Behavior Summon. |
| `initialDelaySeconds` | `10` | Yes | `timing.initialDelaySeconds` | Copy exact seconds | Non-negative; compile to equivalent ticks. |
| `cooldownSeconds` | `10` | Yes | `timing.cooldownSeconds` | Copy exact seconds | Positive; compile to equivalent ticks. |
| `spawnCountPerTrigger` | `1` | Yes | `spawnPolicy.spawnCountPerTrigger` | Copy exact integer | Positive. |
| `maxAliveChildren` | `2` | Yes | `spawnPolicy.maxAliveChildren` | Copy exact integer | Positive; preserve current max-alive behavior. |
| `candidatePattern` | `0` | Yes | `spawnPolicy.candidatePattern` | Copy exact enum value | Supported enum; current value maps to the existing adjacent candidate pattern. |
| `requireNoUnitAtSpawnCell` | `1` | Yes | `spawnPolicy.requireNoUnitAtSpawnCell` | Copy exact boolean | Placement/materializer parity. |
| `requireNoSolidAtSpawnCell` | `1` | Yes | `spawnPolicy.requireNoSolidAtSpawnCell` | Copy exact boolean | Placement/materializer parity. |
| `summonedArchetype` | `guid: 8da265900dc94540a0e150fa08ff4c7f` | Yes | `spawnPolicy.summonedArchetype` | Copy exact reference | Resolves to `EnemyUnitArchetype_PassiveContactMinion`. |
| `overrideHp` | `1` | Yes | `spawnPolicy.overrideHp` | Copy exact boolean | HP override parity. |
| `hpOverride` | `1` | Yes | `spawnPolicy.hpOverride` | Copy exact integer | Positive because override is enabled. |
| `windupSeconds` | `1.7` | Yes | `timing.windupSeconds` | Copy exact seconds | Positive; presentation/audio/VFX windup parity. |
| `suppressMovementDuringWindup` | `1` | Yes | `suppressionPolicy.suppressMovementDuringWindup` | Copy exact boolean | Movement suppression parity. |
| `recoverySeconds` | `0.7` | Yes | `timing.recoverySeconds` | Copy exact seconds | Non-negative; recovery timing parity. |
| `suppressMovementDuringRecover` | `1` | Yes | `suppressionPolicy.suppressMovementDuringRecover` | Copy exact boolean | Recovery suppression parity. |
| Source effect index | Utility effect list index `0` | Yes | Compatibility source slot / `SourceEffectIndex` | Preserve externally | Not a direct YAML field; event/export `Effect=` and child metadata compatibility must remain stable. |

Current implemented Behavior asset serialization uses `EnemySummonBehaviorModuleAsset.initialDelaySeconds`, `cooldownSeconds`, and nested `summon.*`. The future conceptual policy grouping above is the dry-run mapping vocabulary, not permission to create or edit production assets in this step.

## 4. Dry-Run Field Mapping

| Current Utility Field | Future Behavior Field | Copy Rule | Compile Validation | Parity Test | Notes |
| --- | --- | --- | --- | --- | --- |
| `initialDelaySeconds` | `timing.initialDelaySeconds` | Copy exact seconds | Non-negative; same tick conversion | migrated timing parity | Enclosing Utility effect field. |
| `cooldownSeconds` | `timing.cooldownSeconds` | Copy exact seconds | Positive; same tick conversion | migrated cooldown/hash parity | Enclosing Utility effect field. |
| `spawnCountPerTrigger` | `spawnPolicy.spawnCountPerTrigger` | Copy exact integer | Positive | spawn request order parity | Emits one request per spawn index. |
| `maxAliveChildren` | `spawnPolicy.maxAliveChildren` | Copy exact integer | Positive | max-alive parity | Do not invent new max-alive policy during migration. |
| `candidatePattern` | `spawnPolicy.candidatePattern` | Copy exact enum | Supported enum | placement order parity | Candidate selection remains placement resolver owned. |
| `requireNoUnitAtSpawnCell` | `spawnPolicy.requireNoUnitAtSpawnCell` | Copy exact boolean | Boolean | placement filter parity | Materializer evaluates occupancy. |
| `requireNoSolidAtSpawnCell` | `spawnPolicy.requireNoSolidAtSpawnCell` | Copy exact boolean | Boolean | placement filter parity | Materializer evaluates occupancy. |
| `summonedArchetype` | `spawnPolicy.summonedArchetype` | Copy exact asset reference / compiled id | Non-null valid archetype | archetype binding parity | Binding creation remains materializer owned. |
| `overrideHp` | `spawnPolicy.overrideHp` | Copy exact boolean | Boolean | HP parity | Materializer applies HP selection. |
| `hpOverride` | `spawnPolicy.hpOverride` | Copy exact integer | Positive when override enabled | HP parity | Value remains `1`. |
| `windupSeconds` | `timing.windupSeconds` | Copy exact seconds | Positive; same tick conversion | windup signal parity | Drives warning/audio/VFX timing. |
| `suppressMovementDuringWindup` | `suppressionPolicy.suppressMovementDuringWindup` | Copy exact boolean | Boolean | suppression parity | Preserve windup movement block. |
| `recoverySeconds` | `timing.recoverySeconds` | Copy exact seconds | Non-negative | recovery parity | Value remains `0.7`. |
| `suppressMovementDuringRecover` | `suppressionPolicy.suppressMovementDuringRecover` | Copy exact boolean | Boolean | suppression parity | Preserve recover movement block. |
| Utility effect index | Compatibility source slot | Preserve external `Effect=` / `SourceEffectIndex` semantics | Duplicate-source and replay/export validation | source metadata parity | Behavior path currently uses compatibility source effect index `0`. |
| Placement/materialization/id allocation | `EntitySpawnMaterializer` | Do not migrate into asset | Request remains id-free | materialization parity | `FinalizationBatch.SpawnEntity` remains the authoritative write path. |

## 5. Duplicate Guard Migration Sequence

| Sequence | Pros | Cons | Recommendation |
| --- | --- | --- | --- |
| Two-step invalid intermediate: attach Behavior Summon while Utility Summon remains, compile, then remove Utility source | Proves the guard fires in production-like content | Leaves an intermediate compile-invalid production state | Do not use for production migration. Use fixtures to test this state. |
| Atomic asset-scoped replacement: create Behavior asset from Utility values, remove Utility Summon source from the migrated profile/capability, attach Behavior Summon, then compile once | Avoids a duplicate-source production state and keeps migration bounded to the allowlist | Requires careful manual or tool transaction discipline | Selected. |

Duplicate guard contract:

- Same profile with Utility `SummonMinion` and Behavior `Summon` must fail fast.
- Current compiler message includes the profile name, Utility effect index such as `Utility.effects[0]`, Behavior module key/name, and remediation.
- The guard must not classify `GravityFieldAura` as duplicate Summon.
- Retired Utility `kind: 1` remains governed by the retired guard, not the Summon duplicate guard.

## 6. Replay / Hash No-Double-Count Policy

| Hash/Event Surface | Before Migration | After Migration | Expected Diff | Required Test |
| --- | --- | --- | --- | --- |
| `EnemyUtilities` | Contains Utility Summon timing/state for active Summoner | No active Summon state for the migrated source | State-owner section may lose migrated Summon entry | no-double-count / no-missing-count migrated hash test |
| `EnemySummonBehaviors` | Absent for production Summoner | Contains migrated Behavior Summon timing/state | New state-owner section entry | Behavior state hash presence test |
| `SummonedEntities` | Materializer-owned child metadata with `SourceEffectIndex` | Same external child metadata | No semantic drift expected | child metadata parity |
| `EnemyDefinitionBindings` | Materializer-owned archetype binding | Same external archetype binding | No semantic drift expected | archetype binding parity |
| `EventLog` | `SummonCommitted` / `SummonSkipped` with `Effect=` | Same external names and fields | No rename expected | replay name parity |
| Final entities | Spawned ids, positions, HP, team, facing, spawn tick, AI mode | Same outputs for approved baseline scenarios | No semantic drift expected | final entity parity |
| Presentation/audio/VFX | Current signal/cue names from Utility-compatible facts | Same names and semantics | No rename expected | presentation/audio/VFX parity |

Policy:

- Exact determinism hash equality may not be required if state ownership moves from `EnemyUtilities` to `EnemySummonBehaviors`.
- Any hash diff caused by state-owner section change must be approved, bounded, and backed by no-double-count / no-missing-count evidence.
- `SummonedEntities`, `EnemyDefinitionBindings`, spawned ids/order, placements, event names, `Effect=`, and `SourceEffectIndex` must remain compatibility-preserved unless a separate schema migration is approved.

## 7. Baseline Capture Plan

| Baseline | Scenario | Captured Artifacts | Compare Against | Required Before Migration? |
| --- | --- | --- | --- | --- |
| Utility production single spawn | One production Summoner commits one spawn | seed/setup/stage id, tick range, event log, trace, hash, final entities, child metadata, bindings, spawned id, placement | Migrated Behavior-only Summoner | Yes |
| Utility production max-alive block | Alive child count blocks spawn | event log, absence of committed spawn, child metadata, hash | Migrated max-alive behavior | Yes |
| Utility production source invalid/cancel | Source invalidates before commit if stageable | `SummonSkipped`, windup cleanup, no committed spawn | Migrated invalidation behavior | Yes when stageable |
| Utility production failed placement | Candidate cells unavailable if stageable | `SummonSkipped NoCandidateCell`, no id allocation, no spawn presentation/audio/VFX | Migrated failed placement behavior | Yes when stageable |
| Utility production same-tick ordering | Multiple summoners commit on same tick if stageable | request order, event order, spawned ids, placements, presentation/audio/VFX order | Migrated ordering behavior | Yes when stageable |
| Utility production presentation/audio/VFX | Windup and successful spawn presentation path | `TickSummonWindupWarningSignal`, `TickSummonedEnemyPresentationBinding`, `TickVisibilityChange(Spawn)`, `EnemyAudioCue.Windup`, `EnemyAudioCue.Active`, `UtilityWindup`, `UtilitySummonSpawn` | Migrated presentation/audio/VFX path | Yes |

Each baseline must also capture `EnemyUtilities`, absence of production `EnemySummonBehaviors` for the source, `Effect=`, `SourceEffectIndex`, `OriginCell`, `SourceFacing`, and `SourceTeamId` where exposed.

## 8. Residue Scan Policy

Forbidden residue after the migrated profile:

- Active Utility `SummonMinion` on the migrated profile.
- Active Utility effect `kind: 0` in the migrated profile capability allowlist.
- More than one production Behavior Summon module on the migrated profile.
- Any duplicate Utility + Behavior Summon source on the same migrated profile.
- `GravityFieldAura` content changes.
- `RetiredLockNearbyBoxes` migration, deletion, or numeric-slot reuse.
- `logicModuleAssets` or a generic behavior registry.
- Replay/export-visible name changes.
- Presentation/audio/VFX cue or asset name changes.

Scan commands:

```bash
rg -n "SummonMinion|kind: 0" Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/Profiles/Enemy_ArchetypeSummoner Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/Capabilities/Enemy_UtilitySummoner/EnemyCapability_ArchetypeSummoner.asset
rg -n "EnemySummonBehaviorModule|EnemySummonBehaviorModuleAsset" Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI
rg -n "behaviorModuleAssets:" Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/Profiles/Enemy_ArchetypeSummoner/EnemyAi_ArchetypeSummoner.asset
rg -n "GravityFieldAura|kind: 2" Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/Capabilities/GravityFieldAura/EnemyCapability_GravityFieldAura.asset
rg -n "RetiredLockNearbyBoxes|kind: 1" Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI
rg -n "logicModuleAssets" Assets
rg -n "UtilityWindup|UtilitySummonSpawn|EnemyAudioCue\\.Windup|EnemyAudioCue\\.Active" Assets/_Features/Gameplay
```

False-positive notes:

- Numeric `kind` scans require surrounding Utility capability context.
- A nested `summon:` block inside active `kind: 2` Gravity content is inactive serialized payload.
- Absence of `kind: 1` is not permission to delete or repurpose the retired slot.
- Production migration must use the exact asset-path allowlist in this document.

## 9. Rollback Plan

| Failure Point | Rollback Action | Validation After Rollback |
| --- | --- | --- |
| Behavior asset values are wrong | Delete or revert the created Behavior asset and keep Utility-only source | Field inventory scan matches this document. |
| Duplicate guard failure | Remove Behavior module reference or restore Utility-only baseline shape | Profile compiles as Utility-only; duplicate residue scan passes. |
| Replay/hash diff outside approved bounds | Revert migrated profile/capability/Behavior asset changes | Utility baseline replay/hash artifacts match the captured baseline. |
| Presentation/audio/VFX parity failure | Revert migrated assets and cue-binding changes if any were attempted | Existing cue names and baseline requests remain unchanged. |
| Residue scan failure | Revert to source-control baseline and rerun all residue scans | No active migrated residue remains. |
| Full lane / CI failure | Revert the migration slice or hold it behind a release-gate decision | Rerun touched scans and targeted lanes after rollback. |

Rollback principles:

- Source control is the authoritative recovery path.
- Utility-only production source remains the fallback.
- Rollback restores the original `capabilityAssets` / `behaviorModuleAssets` shape and the active Utility `SummonMinion` source.
- Rerun residue scans and Utility baseline validation after rollback.

## 10. Validation / CI Gate Plan

Readiness / dry-run validation:

```bash
git status --short --branch
git diff --name-status
git diff --stat
git diff --check
git diff --check origin/main
rg -n "EnemyCapability_ArchetypeSummoner|EnemyAi_ArchetypeSummoner|behaviorModuleAssets:|capabilityAssets:" Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI
rg -n "kind: 0|SummonMinion|summon:|initialDelaySeconds|cooldownSeconds|spawnCountPerTrigger|maxAliveChildren|summonedArchetype|windupSeconds|recoverySeconds" Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/Capabilities/Enemy_UtilitySummoner/EnemyCapability_ArchetypeSummoner.asset
rg -n "EnemySummonBehaviorModuleAsset|EnemySummonBehaviorModule|EnemyBehaviorModuleKey\\.Summon" Assets/_Features/Stages/Content
rg -n "GravityFieldAura|kind: 2|RetiredLockNearbyBoxes|kind: 1|logicModuleAssets" Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI
git diff --name-only -- Assets/_Features/Stages/Content
git diff --name-only -- Assets/_Features/Gameplay Assets/_Features/Gameplay/Gameplay_Tests
rg -n "[[:blank:]]$" Docs/Architecture/Enemy-AI-Summon-Asset-Scoped-Migration-Readiness.md Docs/Architecture/Enemy-AI-Summon-*.md Docs/Architecture/README.md
rg -n "<forbidden readiness claim pattern>" Docs/Architecture/Enemy-AI-Summon-Asset-Scoped-Migration-Readiness.md
rg -n "logicModuleAssets" Assets
```

Actual production migration validation:

```bash
git diff --check
./run_tests.sh --integration-simulation --filter BehaviorSummon
./run_tests.sh --integration-simulation --filter EnemyUtilitySummon_
./run_tests.sh --integration-replay --filter UtilitySummon
./run_tests.sh --integration-replay --filter UtilityArchetypeSummon
./run_tests.sh core
./run_tests.sh full
```

The real migration slice must also add exact migrated asset contract tests, residue scans, migrated replay/hash parity tests, and migrated presentation/audio/VFX parity tests. If `./run_tests.sh full` is not run, do not claim broad full-lane success.

## 11. Explicit Non-Goals

- No production asset migration in this step.
- No Utility `SummonMinion` removal in this step.
- No production `EnemySummonBehaviorModuleAsset` creation in this step.
- No replay/export-visible rename.
- No presentation/audio/VFX rename.
- No `GravityFieldAura` migration.
- No `RetiredLockNearbyBoxes` migration/delete.
- No generic registry.
- No `logicModuleAssets`.
- No `EntitySpawnRequest` contract change.
- No `EntitySpawnMaterializer` contract change.
- No direct `WorldState` spawn write.

## 12. Follow-up

- Approve an actual asset-scoped production migration slice.
- Create the production Behavior Summon asset only in that migration slice.
- Replace the Utility source atomically or through another explicitly approved sequence.
- Run residue scans.
- Run migrated replay/hash/presentation/audio/VFX parity.
- Run full lane or an approved CI release gate if required.

Summon asset-scoped migration readiness is documented as dry-run only. Production Utility Summon migration has not started. Behavior Summon remains validated through test-local parity paths. Full lane was not run unless explicitly reported.
