# Enemy AI Summon Asset Migration Plan

## 1. Decision Summary

- This is a migration plan only.
- Option B compile skeleton exists: `EnemyBehaviorModuleKey.Summon`, `EnemySummonBehaviorModuleAsset`, and a fixed typed Summon runtime config slot are implemented.
- No mutable Summon behavior state, runtime emission, or production asset migration is implemented.
- Utility `SummonMinion` remains valid until a future explicit asset-scoped migration.
- Future migration must be field-by-field, guarded, replay/export-reviewed, and parity-tested.
- `GravityFieldAura` and `RetiredLockNearbyBoxes` are excluded from Summon migration.
- Production assets were scanned but not migrated.
- Full lane was not run for this plan.

## 2. Current Utility Summon Authoring Inventory

Current Utility Summon authoring is split across the enclosing utility effect and the nested Summon payload.

| Current Field | Serialized? | Current Authoring Owner | Compiled Runtime Field | Used By | Future Owner Candidate | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `kind` | yes | `EnemyUtilityEffectAuthoring` | `EnemyUtilityEffectRuntime.Kind` | Utility compile, resolver dispatch, state kind guard | MigrationOnly | `SummonMinion = 0` identifies the current active Utility summon source. |
| `initialDelaySeconds` | yes | `EnemyUtilityEffectAuthoring` | `InitialDelayTicks` | Initial Utility cooldown state | `SummonBehaviorTiming` | Copy exact seconds and compile with the same tick rate. |
| `cooldownSeconds` | yes | `EnemyUtilityEffectAuthoring` | `CooldownTicks` | Re-trigger cooldown | `SummonBehaviorTiming` | Positive recurring timing contract. |
| `summon` | yes | `EnemyUtilityEffectAuthoring` | `EnemyUtilityEffectRuntime.Summon` | Summon runtime payload | MigrationOnly | Nested serialized object; only active when `kind == SummonMinion`. |
| `gravityFieldAura` | yes | `EnemyUtilityEffectAuthoring` | `EnemyUtilityEffectRuntime.GravityFieldAura` | Gravity runtime payload | Excluded | Not part of Summon migration. |
| `spawnCountPerTrigger` | yes | `SummonMinionAuthoring` | `SummonMinionRuntime.SpawnCountPerTrigger` | Request emission loop | `SummonBehaviorSpawnPolicy` / `SpawnRequest` | Behavior emitter loops; materializer receives one request per spawn index. |
| `maxAliveChildren` | yes | `SummonMinionAuthoring` | `SummonMinionRuntime.MaxAliveChildren` | Start and per-spawn gate | `SummonBehaviorSpawnPolicy` / `SummonBehaviorValidationPolicy` | Config plus snapshot query policy; not a runtime child-id list. |
| `candidatePattern` | yes | `SummonMinionAuthoring` | `SummonMinionRuntime.CandidatePattern` | Candidate placement order | `SummonBehaviorSpawnPolicy` / `SpawnRequest` | Current supported enum is `OrthogonalAdjacent4`. |
| `requireNoUnitAtSpawnCell` | yes | `SummonMinionAuthoring` | `SummonMinionRuntime.RequireNoUnitAtSpawnCell` | Placement filter | `SummonBehaviorSpawnPolicy` / `SpawnRequest` | Materializer/placement resolver evaluates it. |
| `requireNoSolidAtSpawnCell` | yes | `SummonMinionAuthoring` | `SummonMinionRuntime.RequireNoSolidAtSpawnCell` | Placement filter | `SummonBehaviorSpawnPolicy` / `SpawnRequest` | Materializer/placement resolver evaluates it. |
| `summonedArchetype` | yes | `SummonMinionAuthoring` | `SummonMinionRuntime.SummonedArchetypeId` | Spawn defaults lookup and binding | `SummonBehaviorSpawnPolicy` / `SpawnRequest` | Future config stores authoring reference or compiled archetype id; materializer keeps binding creation. |
| `overrideHp` | yes | `SummonMinionAuthoring` | `SummonMinionRuntime.OverrideHp` | Child HP selection | `SummonBehaviorSpawnPolicy` / `SpawnRequest` | Materializer applies override when constructing entity. |
| `hpOverride` | yes | `SummonMinionAuthoring` | `SummonMinionRuntime.HpOverride` | Child HP value | `SummonBehaviorSpawnPolicy` / `SpawnRequest` | Positive only when override is enabled. |
| `windupSeconds` | yes | `SummonMinionAuthoring` | `SummonMinionRuntime.WindupTicks` | Windup timing and warning | `SummonBehaviorTiming` | Positive and compiles to positive ticks. |
| `suppressMovementDuringWindup` | yes | `SummonMinionAuthoring` | `SummonMinionRuntime.SuppressMovementDuringWindup` | Movement suppression window | `SummonBehaviorSuppressionPolicy` | Runtime state stores the resulting suppression window. |
| `recoverySeconds` | yes | `SummonMinionAuthoring` | `SummonMinionRuntime.RecoveryTicks` | Recovery timing | `SummonBehaviorTiming` | Non-negative; zero remains valid. |
| `suppressMovementDuringRecover` | yes | `SummonMinionAuthoring` | `SummonMinionRuntime.SuppressMovementDuringRecover` | Recovery suppression window | `SummonBehaviorSuppressionPolicy` | Runtime state stores the resulting suppression window. |
| Source effect index | no direct asset field | Utility effect list index | `EnemyUtilityTriggerIntent.EffectIndex`, `EntitySpawnRequestSource.SourceEffectIndex`, `SummonedEntityState.SourceEffectIndex` | Ordering, replay events, max-alive tracking | MigrationOnly | Keep compatibility during migration; source-neutral replacement requires replay/export plan. |
| Request origin/facing/team snapshot | no | Resolver emits from source snapshot | `EntitySpawnRequestSource.OriginCell`, `SourceFacing`, `SourceTeamId` | Placement and spawned entity defaults | `SpawnRequest` | Request snapshot metadata remains in the spawn seam. |
| Placement result and allocated entity id | no | Materializer only | Materializer local result | Spawn finalization and events | `EntitySpawnMaterializer` | Must not move into Behavior config or runtime state. |
| Presentation/audio/VFX refs | no current Summon fields | none in `SummonMinionAuthoring` | none | Presentation consumers use separate bindings | PresentationProfile | Add only through a separate presentation/audio/VFX plan. |
| Retired Utility slot | yes as enum value only | `EnemyUtilityEffectKind.RetiredLockNearbyBoxes` | compile fail-fast | Retired compatibility guard | Excluded | `kind: 1` remains reserved and must not be migrated or deleted. |

## 3. Current Production Asset Findings

Scanned paths:

- `Assets/_Features/Stages/Content`
- `Assets/_Features/Gameplay`
- `Docs/Architecture`

The requested paths `Assets/_Features/Stages/Stage_CombinedGameplayShowcase` and `Assets/_Features/Stages/Stage_TutorialScene` do not exist in this worktree.

| Finding | Result |
| --- | --- |
| Utility Summon production assets | One active asset: `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/Capabilities/Enemy_UtilitySummoner/EnemyCapability_ArchetypeSummoner.asset`, with `kind: 0`, `initialDelaySeconds: 10`, `cooldownSeconds: 10`, `spawnCountPerTrigger: 1`, `maxAliveChildren: 2`, `candidatePattern: 0`, unit and solid requirements enabled, archetype guid `8da265900dc94540a0e150fa08ff4c7f`, HP override enabled with value `1`, `windupSeconds: 1.7`, windup suppression enabled, `recoverySeconds: 0.7`, recovery suppression enabled. |
| Utility Summon profile use | `EnemyAi_ArchetypeSummoner.asset` references `EnemyCapability_ArchetypeSummoner.asset` and has `behaviorModuleAssets: []`. |
| Gravity assets | One active Utility Gravity asset: `EnemyCapability_GravityFieldAura.asset`, with `kind: 2`, referenced by `EnemyAi_GravityFieldChaser.asset`. It is excluded from Summon migration. |
| Gravity serialized Summon residue | `EnemyCapability_GravityFieldAura.asset` contains a nested `summon:` default block because `EnemyUtilityEffectAuthoring` serializes both nested payloads. Since active `kind` is `2`, this is not active Summon content and must not be migrated. |
| Retired residue | No active Utility capability asset with `kind: 1` was found in the Utility capability asset allowlist. Numeric `kind: 1` appears in other enum contexts and must be treated as a false-positive unless surrounded by `EnemyUtilityCapabilityAsset` context. |
| Behavior Summon symbols | Runtime and test code now contain the compile-skeleton `EnemyBehaviorModuleKey.Summon`, `EnemySummonBehaviorModuleAsset`, and compiled Summon behavior runtime config. Production stage content has no active `EnemySummonBehaviorModuleAsset` instances, and `logicModuleAssets` has no matches. |
| Behavior module assets | `behaviorModuleAssets` is empty for the Summoner profile. The only non-empty production profile observed is Charge, referencing `EnemyChargeBehaviorModule_Standard.asset`. |
| Presentation/audio/VFX related assets | Utility Summoner has separate audio requirement/profile assets. These are not fields on current Summon authoring and require a later presentation/audio/VFX parity plan. |

## 4. Future Behavior Summon Asset Shape

Pseudo-code only. Do not add these C# types until future Option B implementation is explicitly approved.

```csharp
EnemySummonBehaviorModuleAsset
{
    EnemySummonBehaviorTiming timing;
    EnemySummonSpawnPolicy spawnPolicy;
    EnemySummonSuppressionPolicy suppressionPolicy;
    EnemySummonValidationPolicy validationPolicy;
    OptionalPresentationProfile presentationProfile; // only if a future parity plan requires it
}

EnemySummonBehaviorTiming
{
    float initialDelaySeconds;
    float cooldownSeconds;
    float windupSeconds;
    float recoverySeconds;
}

EnemySummonSpawnPolicy
{
    int spawnCountPerTrigger;
    int maxAliveChildren;
    SummonCandidatePattern candidatePattern;
    bool requireNoUnitAtSpawnCell;
    bool requireNoSolidAtSpawnCell;
    EnemyUnitArchetypeAsset summonedArchetype;
    bool overrideHp;
    int hpOverride;
}

EnemySummonSuppressionPolicy
{
    bool suppressMovementDuringWindup;
    bool suppressMovementDuringRecover;
}

EnemySummonValidationPolicy
{
    bool requireSourceControllableAtCommit;
    bool requireBottomFaceAtMaterialization;
    SourceInvalidationPolicy sourceInvalidationPolicy;
    TopologyParticipationPolicy topologyParticipationPolicy;
}
```

Fields explicitly excluded:

- Allocated entity id.
- Placement result cell.
- Materialization success/failure result.
- Reserved spawn cell set.
- Spawned child entity id list.
- Direct `WorldState`, `EntityIdAllocator`, or `FinalizationBatch` reference.
- `GravityFieldAura` fields.
- `RetiredLockNearbyBoxes` fields.
- Generic `logicModuleAssets` or registry fields.

## 5. Field-by-Field Migration Mapping

| Current Utility Field | Future Behavior Field | Migration Rule | Validation Rule | Parity Test | Notes |
| --- | --- | --- | --- | --- | --- |
| `kind == SummonMinion` | Behavior module presence | Replace only after future Behavior asset is created and attached | Profile must not contain Utility Summon and Behavior Summon together | `UtilitySummonAndBehaviorSummon_ProfileCompileFails` | Duplicate guard owns the invalid overlap. |
| `initialDelaySeconds` | `timing.initialDelaySeconds` | Copy exact seconds value | Non-negative and compiles to non-negative ticks | `BehaviorSummon_InitialDelayParity` | Comes from enclosing Utility effect. |
| `cooldownSeconds` | `timing.cooldownSeconds` | Copy exact seconds value | Positive and compiles to positive ticks | `BehaviorSummon_DeterminismHashParity` | Comes from enclosing Utility effect. |
| `spawnCountPerTrigger` | `spawnPolicy.spawnCountPerTrigger` | Copy exact integer | Positive | `BehaviorSummon_EmitsSpawnRequestInUtilityParityOrder` | Emits one request per spawn index. |
| `maxAliveChildren` | `spawnPolicy.maxAliveChildren` | Copy exact integer | Positive; preserve current policy where `maxAliveChildren < spawnCountPerTrigger` can skip later spawn indices | `BehaviorSummon_MaxAliveParity` | Config plus snapshot query, not runtime child list. |
| `candidatePattern` | `spawnPolicy.candidatePattern` | Copy enum value exactly | Supported enum; current supported value is `OrthogonalAdjacent4` | `BehaviorSummon_EmitsSpawnRequestInUtilityParityOrder` | Placement resolver owns candidate selection. |
| `requireNoUnitAtSpawnCell` | `spawnPolicy.requireNoUnitAtSpawnCell` | Copy boolean exactly | Boolean | `BehaviorSummon_PlacementPolicyParity` | Spawn request snapshots policy; materializer evaluates it. |
| `requireNoSolidAtSpawnCell` | `spawnPolicy.requireNoSolidAtSpawnCell` | Copy boolean exactly | Boolean | `BehaviorSummon_PlacementPolicyParity` | Spawn request snapshots policy; materializer evaluates it. |
| `summonedArchetype` | `spawnPolicy.summonedArchetype` / compiled archetype id | Copy asset reference or compiled id exactly | Non-null, valid archetype config, spawn defaults exist | `MigratedSummon_FieldMappingMatchesUtilityBaseline` | Materializer keeps `EnemyDefinitionBindingState` creation. |
| `overrideHp` | `spawnPolicy.overrideHp` | Copy boolean exactly | Boolean | `MigratedSummon_FieldMappingMatchesUtilityBaseline` | Materializer applies HP selection. |
| `hpOverride` | `spawnPolicy.hpOverride` | Copy exact integer | Positive when override enabled | `MigratedSummon_FieldMappingMatchesUtilityBaseline` | Default HP path remains spawn defaults. |
| `windupSeconds` | `timing.windupSeconds` | Copy exact seconds and compile with same tick rate | Positive and compiles to positive ticks | `MigratedSummon_PresentationWindupParity` | Presentation warning parity depends on this. |
| `suppressMovementDuringWindup` | `suppressionPolicy.suppressMovementDuringWindup` | Copy boolean exactly | Boolean | `BehaviorSummon_MovementSuppressionParity` | Preserve imminent windup suppression behavior. |
| `recoverySeconds` | `timing.recoverySeconds` | Copy exact seconds and compile with same tick rate | Non-negative | `BehaviorSummon_MovementSuppressionParity` | Zero recovery remains valid. |
| `suppressMovementDuringRecover` | `suppressionPolicy.suppressMovementDuringRecover` | Copy boolean exactly | Boolean | `BehaviorSummon_MovementSuppressionParity` | Preserve recovery suppression behavior. |
| Utility effect index | Future source/module index compatibility field | During migration, map Utility effect index to the future behavior source index used by request metadata | Replay/export compatibility plan must approve vocabulary | `BehaviorSummon_ReplayNamesPreservedOrMigrated` | Do not rename `Effect=` in this plan. |
| `SourceEffectIndex` in child metadata | Future source index compatibility field | Preserve semantics until replay/export migration chooses a neutral vocabulary | Must continue to support max-alive query parity | `BehaviorSummon_MaxAliveParity` | `SummonedEntityState` remains materializer output. |
| Request `OriginCell`, `SourceFacing`, `SourceTeamId` | `EntitySpawnRequestSource` | Keep captured request snapshot metadata in request emission | Request payload must not drift before materialization | `BehaviorSummon_PreservesRequestPayloadSnapshot` | Not Behavior asset fields. |
| Placement/materialization/id allocation | `EntitySpawnMaterializer` | Do not migrate | Failed placement must not allocate ids; successful placement allocates after cell selection | `BehaviorSummon_EmitsSpawnRequestInUtilityParityOrder` | `FinalizationBatch.SpawnEntity` remains authoritative write path. |
| Presentation/audio/VFX refs | Presentation profile or existing binding assets | Do not invent fields; copy only if a future presentation plan introduces equivalent refs | Separate presentation/audio/VFX parity plan required | `MigratedSummon_AudioVfxParity` | Current Summon authoring has no direct refs. |
| `GravityFieldAura` fields | none | Do not migrate | Gravity assets remain unchanged | `GravityFieldAura_Unchanged` | Out of scope. |
| `RetiredLockNearbyBoxes` | none | Do not migrate or delete | `kind: 1` remains compile fail-fast | `RetiredLockNearbyBoxes_GuardUnchanged` | Reserved compatibility slot. |

## 6. Validation Parity Rules

Field validation:

- `initialDelaySeconds >= 0`.
- `cooldownSeconds > 0`.
- `spawnCountPerTrigger > 0`.
- `maxAliveChildren > 0`.
- Preserve current max-alive parity instead of inventing `maxAlive >= spawnCount` unless a separate policy decision changes it.
- `candidatePattern` must be supported.
- Occupancy requirement booleans copy exactly.
- `summonedArchetype` must be non-null, configuration-valid, and resolvable to spawn defaults.
- `hpOverride > 0` when `overrideHp` is enabled.
- `windupSeconds > 0` and compiles to positive ticks.
- `recoverySeconds >= 0`.

Compiler and residue validation:

- Future Utility Summon plus Behavior Summon on the same `EnemyAiProfile` must fail fast.
- The duplicate guard must ignore `GravityFieldAura`.
- `RetiredLockNearbyBoxes` must continue to fail through the retired Utility guard, not through the Summon duplicate guard.
- `kind: 1` must not compile to active runtime.
- `logicModuleAssets` must remain absent unless a separate approved phase introduces it.

Spawn/materialization parity:

- `EntitySpawnRequest` must not carry an allocated entity id.
- `EntitySpawnMaterializer` must preserve received request order.
- Entity id allocation remains after placement success.
- Failed placement does not allocate ids.
- `FinalizationBatch.SpawnEntity` remains the authoritative write path.
- `SummonedEntityState` and `EnemyDefinitionBindingState` remain materializer outputs.

## 7. Migration Order and Rollback

| Step | Action | Preconditions | Validation | Rollback |
| --- | --- | --- | --- | --- |
| 1 | Capture baseline Utility Summon assets, replay/hash output, and presentation/audio/VFX behavior | Current Utility-only Summon compiles | Record exact asset paths and baseline evidence | No changes yet |
| 2 | Accept replay/export compatibility plan and presentation/audio/VFX parity plan | This asset migration plan accepted | Follow-up plans define naming/hash/source vocabulary and presentation cues | Keep Utility lane unchanged |
| 3 | Add future Behavior Summon implementation in a separate phase | Option B approved; duplicate guard design accepted | Behavior-only tests pass; no production migration yet | Revert future implementation phase only |
| 4 | Add duplicate Utility/Behavior guard | Both lanes are visible to `EnemyAiProfileCompiler` | Duplicate state fails with profile, Utility effect index, and Behavior module source | Disable/remove Behavior module reference |
| 5 | Create Behavior Summon asset from Utility fields | Future asset type exists | Field mapping matches Utility baseline | Delete created Behavior asset |
| 6 | Attach Behavior Summon to profile | Duplicate guard available | Temporary duplicate state is detected before compile or avoided by tool sequencing | Remove Behavior module reference |
| 7 | Remove Utility `SummonMinion` from the same profile | Behavior asset attached and field parity checked | Profile has Behavior Summon only; duplicate guard no longer fires | Restore Utility Summon entry and remove Behavior reference |
| 8 | Run residue scan | Profile migration completed | No active Utility Summon residue for migrated profile; Gravity unchanged; retired guard unchanged | Restore baseline asset state from source control |
| 9 | Run parity tests | Residue scan passed | Replay/hash/spawn/order/presentation/audio/VFX parity passes | Revert migrated profile and Behavior asset |
| 10 | Preserve rollback evidence until parity accepted | Migration evidence complete | Rollback path documented and tested | Return profile to Utility-only state |

## 8. Duplicate Guard Integration

Duplicate guard dependency:

- The future guard depends on `Enemy-AI-Summon-Duplicate-Guard-Design.md`.
- It belongs in `EnemyAiProfileCompiler` after capability and behavior module compilation, before runtime definition creation.
- Detection should use compiled runtime sources, not raw YAML string scans.

Duplicate avoidance order:

- Preferred manual sequence is create Behavior asset, verify field mapping, remove Utility Summon from the profile, then compile.
- If a tool stages both sources temporarily, it must validate and remove the old Utility source before invoking profile compile.
- Asset-scoped migration must operate from an exact profile/path allowlist, not a whole Utility-lane sweep.

Error message example:

```text
Enemy AI profile 'EnemyAi_ArchetypeSummoner' cannot author both Utility SummonMinion effect at Utility.effects[0] and Behavior Summon module 'EnemySummonBehaviorModule_Standard'. Summon migration must be asset-scoped; remove one source before compile.
```

The message must include profile name/path when available, Utility effect index, Behavior module asset name, and remediation guidance. It must not mention `GravityFieldAura` as duplicate Summon and must not treat `RetiredLockNearbyBoxes` as active Summon.

## 9. YAML Residue Policy

After migration of a profile:

- The migrated profile must not contain an active Utility `SummonMinion` effect.
- The migrated profile must contain exactly one future Behavior Summon module.
- The migrated profile must not contain both Utility Summon and Behavior Summon.
- `GravityFieldAura` capability assets remain valid and unchanged.
- `RetiredLockNearbyBoxes` enum slot remains reserved and must not be deleted.
- `kind: 1` remains fail-fast if authored in Utility capability context.
- `behaviorModuleAssets` must not receive placeholder variants.
- `logicModuleAssets` remains absent.

Scan commands:

```bash
rg -n "SummonMinion|kind: 0" <migrated asset paths>
rg -n "EnemySummonBehaviorModule" <migrated asset paths>
rg -n "GravityFieldAura|kind: 2" <migration allowlist paths>
rg -n "RetiredLockNearbyBoxes|kind: 1" <migration allowlist paths>
rg -n "logicModuleAssets" Assets
```

False-positive notes:

- Numeric `kind` scans must always be reviewed with surrounding context.
- `kind: 0`, `kind: 1`, and `kind: 2` appear in other enum contexts outside Utility effects.
- A nested `summon:` block inside a `kind: 2` Utility effect is serialized inactive payload, not active Utility Summon.
- Production migration must use exact asset path allowlists.

## 10. Replay / Export Compatibility Dependency

This document does not complete the replay/export compatibility plan. The accepted prerequisite is [Enemy-AI-Summon-Replay-Export-Compatibility-Plan.md](./Enemy-AI-Summon-Replay-Export-Compatibility-Plan.md).

Preserved by default:

- `SummonCommitted`.
- `SummonSkipped`.
- `SummonedEntities`.
- `EnemyDefinitionBindings`.
- Existing materializer-owned `SummonedEntityState` and `EnemyDefinitionBindingState` semantics.

Migration-required decisions:

- Whether `Effect=` remains in replay/export-visible Summon event lines.
- Whether `SourceEffectIndex` becomes a source-neutral module/source index.
- How migrated Summon state replaces Utility-specific hash lines without double-counting.
- Whether `PreMovement.UtilityTriggers` or other Utility-specific labels need compatibility shims.

Required follow-up:

- A replay/export compatibility plan must be accepted before Option B implementation or asset migration starts.

Validation wording note:

- The targeted Summon/Utility/replay/core validation is already recorded by the spawn seam implementation note, including `./run_tests.sh core` passed with EditMode 183 + PlayMode 33.
- This asset migration plan does not claim a newly run full lane or ui lane.
- Do not derive broad project status or full-lane success from this plan.

## 11. Presentation / Audio / VFX Dependency

This document does not complete the presentation/audio/VFX plan. The accepted prerequisite is [Enemy-AI-Summon-Presentation-Audio-VFX-Parity-Plan.md](./Enemy-AI-Summon-Presentation-Audio-VFX-Parity-Plan.md).

Parity requirements:

- Windup warning parity.
- Summoned enemy presentation binding parity.
- Visibility spawn-change parity.
- Audio windup and active summon cue parity.
- VFX `UtilityWindup` / `UtilitySummonSpawn` parity.
- If existing `Utility*` names remain, document the compatibility reason.
- If names are neutralized, require a migration plan and compatibility tests.

Required follow-up:

- A presentation/audio/VFX parity plan must be accepted before Option B implementation or asset migration starts; see [Enemy-AI-Summon-Presentation-Audio-VFX-Parity-Plan.md](./Enemy-AI-Summon-Presentation-Audio-VFX-Parity-Plan.md).

## 12. Future Test Matrix

| Test | Current/Future/Migration | Purpose | Required Before Option B Implementation? |
| --- | --- | --- | --- |
| Same-tick multi-summoner ordering characterization | Current already covered | Baseline request and id allocation order | Already recorded |
| Mutable request payload drift guard | Current already covered | Baseline request snapshot stability | Already recorded |
| Utility Summon replay | Current already covered | Baseline replay contract | Already recorded |
| UtilityArchetypeSummon replay | Current already covered | Baseline archetype binding replay contract | Already recorded |
| Core lane | Current already covered | Current touched-cluster baseline | Already recorded; rerun when code changes require it |
| `BehaviorSummonOnly_ProfileCompiles_AfterOptionBExists` | Future | Behavior-only profile is valid after real module exists | Yes |
| `UtilitySummonAndBehaviorSummon_ProfileCompileFails` | Future | Duplicate guard fails fast | Yes |
| `DuplicateGuard_MessageIncludesProfileAndBothSources` | Future | Error identifies profile, Utility effect index, Behavior module asset | Yes |
| `BehaviorSummon_EmitsSpawnRequestInUtilityParityOrder` | Future | Preserves materialization order and id allocation order | Yes |
| `BehaviorSummon_PreservesRequestPayloadSnapshot` | Future | Preserves origin/facing/team/tick snapshot metadata | Yes |
| `BehaviorSummon_MaxAliveParity` | Future | Preserves max-alive count and planned child gate behavior | Yes |
| `BehaviorSummon_SourceDeathCancelsOrSkipsAsUtility` | Future | Preserves hard-invalid source behavior | Yes |
| `BehaviorSummon_SourceLeavesTopologyCancelsOrSuspendsAsUtility` | Future | Preserves topology participation loss behavior | Yes |
| `UtilitySummonOnly_ProfileCompilesBeforeMigration` | Migration | Utility-only assets stay valid before migration | No; required before migration |
| `MigratedSummon_ProfileHasBehaviorModuleOnly` | Migration | Migrated profile has only future Behavior Summon source | No; required before migration close |
| `MigratedSummon_ProfileHasNoUtilitySummonResidue` | Migration | Old Utility Summon source removed | No; required before migration close |
| `MigratedSummon_FieldMappingMatchesUtilityBaseline` | Migration | Future asset fields match Utility baseline | No; required before migration close |
| `MigratedSummon_ReplayParityAgainstUtilityBaseline` | Migration | Replay output parity | No; required before migration close |
| `MigratedSummon_DeterminismHashParity` | Migration | Hash parity after state migration | No; required before migration close |
| `MigratedSummon_PresentationWindupParity` | Migration | Warning presentation parity | No; required before migration close |
| `MigratedSummon_AudioVfxParity` | Migration | Audio and VFX parity | No; required before migration close |
| `GravityFieldAura_Unchanged` | Migration | Non-goal Gravity asset remains unchanged | No; required before migration close |
| `RetiredLockNearbyBoxes_GuardUnchanged` | Migration | Retired guard remains unchanged | No; required before migration close |

## 13. Explicit Non-Goals

- No mutable Summon runtime implementation or trigger emission.
- No production asset migration.
- No Utility whole-lane migration.
- No `GravityFieldAura` migration.
- No `RetiredLockNearbyBoxes` migration or deletion.
- No generic registry.
- No `logicModuleAssets`.
- No direct `WorldState` spawn writes.
- No `EntitySpawnRequest` or `EntitySpawnMaterializer` contract changes.
- No replay/export-visible name changes.
- No presentation/audio/VFX binding migration.

## 14. Option B Entry Criteria

- Asset migration plan accepted.
- Duplicate Utility/Behavior Summon guard accepted.
- Runtime state design accepted.
- Replay/export compatibility plan accepted; see [Enemy-AI-Summon-Replay-Export-Compatibility-Plan.md](./Enemy-AI-Summon-Replay-Export-Compatibility-Plan.md).
- Presentation/audio/VFX parity plan accepted; see [Enemy-AI-Summon-Presentation-Audio-VFX-Parity-Plan.md](./Enemy-AI-Summon-Presentation-Audio-VFX-Parity-Plan.md).
- Behavior emitter request ordering contract accepted.
- Source metadata vocabulary accepted without Utility-only coupling.
- Option B implementation slicing and validation gates accepted; see [Enemy-AI-Summon-Option-B-Implementation-Plan.md](./Enemy-AI-Summon-Option-B-Implementation-Plan.md).
- Full lane / CI release gate policy decided.

Summon asset migration remains future gated work after the compile skeleton. No production assets were migrated. Full lane was not run unless explicitly reported.
