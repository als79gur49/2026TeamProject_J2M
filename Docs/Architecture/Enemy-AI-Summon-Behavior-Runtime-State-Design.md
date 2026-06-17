# Enemy AI Summon Behavior Runtime State Design

## 1. Decision Summary

- This began as the runtime state design note and now also records the implemented test-local runtime/emitter parity status.
- First compile-skeleton slice is implemented: `EnemyBehaviorModuleKey.Summon`, `EnemySummonBehaviorModuleAsset`, and a fixed typed Summon runtime config slot exist.
- Mutable Summon behavior runtime state, trigger emission, request production, and materialization participation are implemented for the test-local Behavior Summon path.
- Utility `SummonMinion` remains in the Utility capability lane until an explicit asset-scoped migration exists.
- Test-local Summon behavior runtime owns timing, phase, cooldown, movement suppression, source capture, trigger eligibility, and request emission only.
- The Spawn/EntityCreation seam remains the owner of placement, materialization, entity id allocation, entity construction, metadata creation, and `FinalizationBatch.SpawnEntity`.
- Duplicate Utility Summon plus Behavior Summon remains an Option B compiler guard.
- Mutable Behavior Summon runtime state/emitter is implemented for the test-local Behavior Summon path; production Summoner assets remain Utility-owned.
- Full lane was not run for this design note.

## 2. Current Utility Summon State Inventory

Current Utility Summon is implemented through `EnemyUtilityCapabilityAsset`, `SummonMinionAuthoring`, `EnemyUtilityEffectRuntime`, `SummonMinionRuntime`, `EnemyUtilityEffectState`, `EnemyLogic`, `EnemyUtilityResolver`, `EntitySpawnRequest`, and `EntitySpawnMaterializer`.

| Current Field/Policy | Current Owner | Used For | Determinism Impact | Future Owner Candidate | Notes |
| --- | --- | --- | --- | --- | --- |
| `EnemyUtilityEffectState.effectKind` | `EnemyUtilityEffectState` | Effect slot identity and state/runtime mismatch guard | Hashed in `EnemyUtilities` | MigrationOnly | Future Behavior state should not depend on Utility effect kind, but migration must map the source identity. |
| `EnemyUtilityEffectState.cooldownTicksRemaining` | `EnemyUtilityEffectState` | Initial delay countdown, recurring cooldown, recover-time cooldown decrement | Hashed in `EnemyUtilities` | `SummonBehaviorRuntime` | Future state needs equivalent cooldown parity. |
| `EnemyUtilityEffectState.phase` | `EnemyUtilityEffectState` | `None`, `Windup`, `Recover`, shared `Active` handling | Hashed in `EnemyUtilities` | `SummonBehaviorRuntime` | Summon currently uses `Windup`, `Recover`, and `None`; `Active` is shared Utility vocabulary, not Summon-specific behavior. |
| `windupStartTick` / `windupEndTick` | `EnemyUtilityEffectState` | Windup duration, warning presentation, trigger commit timing | Hashed in `EnemyUtilities` | `SummonBehaviorRuntime` | Future state must preserve tick parity. |
| `activeStartTick` / `activeEndTickExclusive` | `EnemyUtilityEffectState` | Shared Utility active window, mainly GravityFieldAura | Hashed in `EnemyUtilities` | Not part of SummonBehavior | Summon clears these fields and does not own an active duration. |
| `activeOriginCell` | `EnemyUtilityEffectState` | Shared Utility active origin, mainly GravityFieldAura | Hashed in `EnemyUtilities` | Not part of SummonBehavior | Summon request origin is captured from source at request emission/materialization, not via this active origin field. |
| `recoverStartTick` / `recoverEndTickExclusive` | `EnemyUtilityEffectState` | Recovery phase duration and recover presentation signal | Hashed in `EnemyUtilities` | `SummonBehaviorRuntime` | Future state needs equivalent recovery parity. |
| `activationSequence` | `EnemyUtilityEffectState` | Presentation seed, update logs, cancellation logs | Hashed in `EnemyUtilities` | `SummonBehaviorRuntime` | Future state must preserve monotonic activation sequence semantics. |
| `movementSuppressionUntilTickInclusive` | `EnemyUtilityEffectState` | Movement suppression during windup/recover and one-tick windup remainder | Hashed in `EnemyUtilities` | `SummonBehaviorRuntime` | Future state should keep explicit suppression window instead of recomputing from phase only. |
| Initial delay | `EnemyUtilityEffectRuntime.InitialDelayTicks` plus initial Utility state | First activation delay | Affects state hash through cooldown remaining | `SummonBehaviorConfig` plus state initialization | Must migrate with cooldown semantics. |
| Cooldown duration | `EnemyUtilityEffectRuntime.CooldownTicks` | Cooldown reset after commit/recover | Affects state and trigger timing | `SummonBehaviorConfig` | Behavior runtime config, not mutable state. |
| Spawn count | `SummonMinionRuntime.SpawnCountPerTrigger` | Per-trigger request count | Affects events, spawned entities, hash | `SummonBehaviorConfig` / `SpawnRequest` | Request emission loops over count; materializer receives one request per spawn index. |
| Candidate pattern | `SummonMinionRuntime.CandidatePattern` | Candidate placement order vocabulary | Affects placement result | `SummonBehaviorConfig` / `SpawnRequest` | Current only supports `OrthogonalAdjacent4`; placement resolver owns selection. |
| Unit occupancy requirement | `SummonMinionRuntime.RequireNoUnitAtSpawnCell` | Placement eligibility filter | Affects placement result | `SpawnRequest` | Runtime config should be copied into request snapshot; placement owner evaluates it. |
| Solid occupancy requirement | `SummonMinionRuntime.RequireNoSolidAtSpawnCell` | Placement eligibility filter | Affects placement result | `SpawnRequest` | Runtime config should be copied into request snapshot; placement owner evaluates it. |
| Max alive count | `SummonMinionRuntime.MaxAliveChildren` | Start gate and per-spawn gate | Affects trigger and spawn count | `SummonBehaviorConfig` plus snapshot query policy | Not a spawned-child-id runtime list. Count uses `SummonedEntityState` and live child entity state. |
| Summoned archetype | `SummonMinionRuntime.SummonedArchetypeId` | Spawn defaults lookup and binding state | Affects spawned entity and hash | `SummonBehaviorConfig` / `SpawnRequest` | Defaults validation remains compiler/materializer prerequisite. |
| HP override | `SummonMinionRuntime.OverrideHp` / `HpOverride` | Spawned child HP | Affects spawned entity hash | `SummonBehaviorConfig` / `SpawnRequest` | Materializer applies override when constructing entity. |
| Windup ticks | `SummonMinionRuntime.WindupTicks` | Windup duration | Affects state hash and trigger timing | `SummonBehaviorConfig` | Positive duration required. |
| Suppress movement during windup | `SummonMinionRuntime.SuppressMovementDuringWindup` | Windup movement lock and imminent-windup prediction | Affects movement and state hash | `SummonBehaviorConfig` / `SummonBehaviorRuntime` | Runtime state stores window; config controls whether it is set. |
| Recovery ticks | `SummonMinionRuntime.RecoveryTicks` | Recovery phase duration | Affects state hash and movement | `SummonBehaviorConfig` | Non-negative duration required. |
| Suppress movement during recover | `SummonMinionRuntime.SuppressMovementDuringRecover` | Recovery movement lock | Affects movement and state hash | `SummonBehaviorConfig` / `SummonBehaviorRuntime` | Runtime state stores window; config controls whether it is set. |
| Source controllability at state progression | `EnemyLogic.CommitEnemyUtilityState` / `EnemyParticipationPolicy` | Initialize, suspend, cancel, or advance Utility state | Affects state and events | `SummonBehaviorRuntime` plus EnemyLogic participation owner | Future Behavior state lane should reuse participation decision, not invent a separate source-validity policy. |
| Topology participation loss | `EnemyLogic.SuspendEnemyUtilityForTopologyParticipationLoss` | Shift active windows while source cannot participate on current topology | Affects state hash | `SummonBehaviorRuntime` with EnemyLogic participation owner | Future state must preserve pause/shift semantics or explicitly migrate them. |
| Hard invalid participant | `EnemyLogic.CancelEnemyUtilityWindups` | Cancel windup/active/recover and reset cooldown | Affects state and presentation cancel signal | `SummonBehaviorRuntime` with EnemyLogic participation owner | Includes source death, detached/non-controllable, and marked-for-death style invalidity through participation policy. |
| Trigger intent shape | `EnemyUtilityTriggerIntent` | Source id, effect index, kind, trigger tick, runtime, optional origin | Affects request order and events | `SpawnRequest` / source-neutral trigger intent | Future vocabulary should avoid Utility-only names while preserving source index and tick metadata. |
| Trigger sorting metadata | `EnemyUtilityTriggerIntentComparer` | Sort by source entity id, effect index, trigger tick | Affects materialization order and id allocation | `SpawnRequest` emission ordering | Future Behavior emitter must preserve deterministic ordering. |
| Source validation at materialization input | `EnemyUtilityResolver.TryGetValidSource` | Skip if source invalid or not on bottom face at post-attack resolve | Affects `SummonSkipped` events and spawn output | Spawn request resolver validation policy | Behavior emission should not bypass final source validity check. |
| Request source metadata | `EntitySpawnRequestSource` | Source entity id, source effect index, trigger tick, origin, facing, team id | Affects placement, spawned entity, event output | `SpawnRequest` | Existing metadata is source-neutral enough except `SourceEffectIndex` naming may need migration vocabulary. |
| Request payload snapshot | `EntitySpawnRequest` | Request kind, source, spawn index, tick, summon config, spawn defaults | Affects materialization | `SpawnRequest` | No allocated entity id. No placement result. |
| Placement candidate selection | `EntitySpawnPlacementResolver` | Inside board, occupancy, blockers, reserved cells, neutral-first risk fallback | Affects spawn result | `EntitySpawnMaterializer` | Must not move to Summon behavior runtime. |
| Entity id allocation | `EntitySpawnMaterializer` | Allocate id after placement success | Affects spawned id/event/hash | `EntitySpawnMaterializer` | Strong contract. Failed attempts do not allocate ids. |
| `SummonedEntityState` creation | `EntitySpawnMaterializer` | Source metadata for child tracking | Hashed in `SummonedEntities` | `EntitySpawnMaterializer` | Preserved for max-alive and presentation binding. |
| `EnemyDefinitionBindingState` creation | `EntitySpawnMaterializer` | Archetype binding for spawned child | Hashed in `EnemyDefinitionBindings` | `EntitySpawnMaterializer` | Preserved for presentation and determinism. |
| `SummonCommitted` / `SummonSkipped` | `EnemyUtilityResolver` and `EntitySpawnMaterializer` | Replay/export-visible events | Replay/export impact | MigrationOnly | Preserve names unless a separate compatibility plan changes them. |
| Summon windup warning | `TickResultBuilder` over `EnemyUtilityEffectState` | Presentation warning signal | Presentation output | Presentation | Future builder should consume Behavior state after migration. |

## 3. Mutable Runtime State Shape

The compile-skeleton slice has an `EnemySummonBehaviorRuntime` config object produced by `EnemySummonBehaviorModuleAsset`. The runtime/emitter parity slice implements the test-local mutable state/emitter path using the fixed typed `EnemySummonBehaviorRuntimeState` lane. The conceptual shape below remains the design vocabulary for ownership review; production asset migration is still future gated.

```csharp
public readonly struct EnemySummonBehaviorRuntimeConfig
{
    public readonly SummonBehaviorTiming Timing;
    public readonly SummonBehaviorSpawnPolicy SpawnPolicy;
    public readonly SummonBehaviorSuppressionPolicy SuppressionPolicy;
    public readonly SummonBehaviorValidationPolicy ValidationPolicy;
}

public readonly struct SummonBehaviorTiming
{
    public readonly int InitialDelayTicks;
    public readonly int CooldownTicks;
    public readonly int WindupTicks;
    public readonly int RecoveryTicks;
}

public readonly struct SummonBehaviorSpawnPolicy
{
    public readonly int SpawnCountPerTrigger;
    public readonly int MaxAliveChildren;
    public readonly SummonCandidatePattern CandidatePattern;
    public readonly bool RequireNoUnitAtSpawnCell;
    public readonly bool RequireNoSolidAtSpawnCell;
    public readonly EnemyUnitArchetypeId SummonedArchetypeId;
    public readonly bool OverrideHp;
    public readonly int HpOverride;
}

public readonly struct SummonBehaviorSuppressionPolicy
{
    public readonly bool SuppressMovementDuringWindup;
    public readonly bool SuppressMovementDuringRecover;
}

public readonly struct SummonBehaviorValidationPolicy
{
    public readonly bool RequireSourceControllableAtCommit;
    public readonly bool RequireBottomFaceAtMaterialization;
}

public struct EnemySummonBehaviorState
{
    public SummonBehaviorPhase Phase;
    public int CooldownRemainingTicks;
    public int PhaseStartTick;
    public int PhaseEndTick;
    public int RecoveryEndTick;
    public int ActivationSequence;
    public int MovementSuppressionUntilTickInclusive;
    public SurfaceCell CapturedOriginCell;
    public Direction CapturedFacing;
    public int CapturedTeamId;
}

public enum SummonBehaviorPhase
{
    Ready = 0,
    Windup = 1,
    CommitPending = 2,
    Recover = 3,
    Cooldown = 4,
    Suspended = 5,
    Cancelled = 6,
}
```

Phase meanings:

| Phase | Meaning | Transition Condition |
| --- | --- | --- |
| `Ready` | No active windup/recover and eligible to count down or start after cooldown reaches zero | Entered after recovery completes or cooldown initializes to zero |
| `Windup` | Summon is charging and presentation warning should be visible | Entered when cooldown reaches zero, source is controllable, and max alive gate is not reached |
| `CommitPending` | Windup completed and request emission should happen once at the semantic trigger seam | Entered when current tick reaches windup end; leaves immediately after request emission |
| `Recover` | Post-commit recovery window | Entered after request emission if recovery ticks are positive |
| `Cooldown` | Waiting for next activation | Entered after commit with no recovery or after recovery/cooldown policy requires it |
| `Suspended` | Topology participation loss shifted timing windows without canceling | Used as a conceptual owner state; implementation may preserve current shifted-window semantics instead of storing a distinct phase |
| `Cancelled` | Hard invalid source canceled active timing and reset cooldown | Used for presentation/transition contract; implementation may write `Ready/Cooldown` plus cancel signal instead of storing this phase persistently |

Fields explicitly excluded:

- Allocated entity id.
- Placement result cell.
- Materialization success/failure result.
- Reserved spawn cell set.
- Spawned child entity id list.
- Direct `WorldState` or `EntityIdAllocator` reference.
- `FinalizationBatch` write ownership.

Max-alive design decision:

- Do not store spawned child ids in `EnemySummonBehaviorState`.
- Keep max-alive as runtime config plus snapshot query policy over `SummonedEntityState` and current child entity state.
- The query must count only children whose source metadata matches, whose entity exists, `hp > 0`, `markedForDeath == false`, and `boardPresence == Occupying`.
- Per-spawn planned child count remains resolver/materialization planning state, not persistent behavior runtime state.

## 4. Utility-to-Behavior Parity Matrix

| Current Utility Concept | Future Behavior Equivalent | Same? | Migration Risk | Required Test |
| --- | --- | --- | --- | --- |
| Cooldown | `CooldownRemainingTicks` plus `Timing.CooldownTicks` | Must match | High | `BehaviorSummon_DeterminismHashParity` |
| Initial delay | Initial `CooldownRemainingTicks` from `Timing.InitialDelayTicks` | Must match | Medium | `BehaviorSummon_InitialDelayParity` |
| Windup | `Phase=Windup`, `PhaseStartTick`, `PhaseEndTick` | Must match | High | `BehaviorSummon_PresentationWindupParity` |
| Recovery | `Phase=Recover`, `RecoveryEndTick` | Must match | High | `BehaviorSummon_SourceDeathCancelsOrSkipsAsUtility` |
| Movement suppression | `MovementSuppressionUntilTickInclusive` plus suppression policy | Must match | High | `BehaviorSummon_MovementSuppressionParity` |
| Trigger tick | `CommitPending` request emission tick | Must match | High | `BehaviorSummon_EmitsSpawnRequestInUtilityParityOrder` |
| Effect index / source index | Future module/source slot index mapped to request source index | Equivalent after migration | High | `BehaviorSummon_ReplayNamesPreservedOrMigrated` |
| Activation sequence | `ActivationSequence` | Must match | Medium | `BehaviorSummon_PresentationWindupParity` |
| Origin capture | Request source `OriginCell` from source snapshot at emission/materialization | Equivalent | High | `BehaviorSummon_PreservesRequestPayloadSnapshot` |
| Facing capture | Request source `SourceFacing` | Equivalent | High | `BehaviorSummon_PreservesRequestPayloadSnapshot` |
| Team capture | Request source `SourceTeamId` | Equivalent | High | `BehaviorSummon_PreservesRequestPayloadSnapshot` |
| Max alive | Snapshot query over `SummonedEntityState` and child entity state | Must match | High | `BehaviorSummon_MaxAliveParity` |
| Source death/cancel | Participation hard-invalid cancellation plus post-attack source skip | Must match | High | `BehaviorSummon_SourceDeathCancelsOrSkipsAsUtility` |
| Source leaves topology participation | Shift/suspend active windows | Must match or explicitly migrate | High | `BehaviorSummon_SourceLeavesTopologyCancelsOrSuspendsAsUtility` |
| Source boardPresence `Detached` | Hard invalid or non-controllable handling through participation policy | Must match | High | `BehaviorSummon_SourceDetachedCancelsOrSkipsAsUtility` |
| Source `markedForDeath` | Hard invalid or source invalid skip | Must match | High | `BehaviorSummon_SourceMarkedForDeathCancelsOrSkipsAsUtility` |
| Spawned child death/detach/non-occupying state | Excluded from max-alive count | Must match | High | `BehaviorSummon_MaxAliveParity` |
| Replay event naming | `SummonCommitted` / `SummonSkipped` preserved or explicitly migrated | Preserve by default | High | `BehaviorSummon_ReplayNamesPreservedOrMigrated` |
| Determinism hash | Replace Utility summon state hash with behavior state hash in one migration | Equivalent after migration | High | `BehaviorSummon_DeterminismHashParity` |
| Presentation windup warning | Builder consumes future behavior state | Must match | Medium | `BehaviorSummon_PresentationWindupParity` |
| Summon spawn binding | `SummonedEntityState` and `EnemyDefinitionBindingState` remain materializer output | Must match | High | `BehaviorSummon_AudioVfxParity` / binding parity test |

## 5. Ownership Boundary

| Concern | Future Owner | Reason |
| --- | --- | --- |
| Behavior timer/cooldown | `SummonBehaviorRuntime` state | This is behavior-owned mutable timing state. |
| Phase | `SummonBehaviorRuntime` state | Phase drives trigger eligibility and presentation. |
| Windup/recovery transitions | `SummonBehaviorRuntime` state | Current Utility state owns these windows; Option B should move only this owner surface. |
| Trigger eligibility | `SummonBehaviorRuntime` with snapshot queries | Eligibility depends on cooldown, phase, source participation, and max alive. |
| Movement suppression window | `SummonBehaviorRuntime` state | Suppression must remain deterministic and inspectable. |
| Source capture timing | Behavior emitter | Capture is part of request emission, not materialization output. |
| Request emission decision | Behavior emitter | Emitter decides whether to create spawn requests. |
| Max alive gate query dependency | Behavior emitter using snapshot query policy | Config plus snapshot query avoids persistent child lists. |
| Request ordering after emission | Trigger/request collection seam | Preserves deterministic source/module/tick ordering before materializer receives requests. |
| Placement candidate selection | `EntitySpawnPlacementResolver` | Placement legality and risk fallback are not behavior timing state. |
| Placement legality | `EntitySpawnPlacementResolver` / Spawn seam | Keeps traversal, occupancy, and placement rules separate. |
| Reserved spawn cells | Post-attack resolver/materializer planning state | Reservation is per-materialization pass, not persistent behavior state. |
| Hazard neutral-first/risk fallback | `EntitySpawnPlacementResolver` | Existing placement policy remains centralized. |
| Entity id allocation | `EntitySpawnMaterializer` | Ids are allocated only after placement succeeds. |
| Entity construction/defaults | `EntitySpawnMaterializer` | Spawn defaults and HP override are applied at construction. |
| `SummonedEntityState` creation | `EntitySpawnMaterializer` | Child source metadata remains materializer output. |
| `EnemyDefinitionBindingState` creation | `EntitySpawnMaterializer` | Archetype binding remains materializer output. |
| `FinalizationBatch.SpawnEntity` write | `EntitySpawnMaterializer` | Authoritative write path stays unchanged. |
| Duplicate Utility/Behavior Summon guard | `EnemyAiProfileCompiler` | Compiler can see both lanes and provide asset context. |
| Authoring validation | Future behavior asset/profile compiler | Asset config validation belongs at compile time. |
| Migration residue validation | Compiler/migration tests | Prevents profiles from carrying both sources after migration. |
| Windup warning visual | Presentation | Visuals consume state; they do not mutate simulation. |
| Summoned enemy presentation binding | Presentation consuming materializer metadata | Binding comes from `SummonedEntityState` and `EnemyDefinitionBindingState`. |
| Audio/VFX parity consumption | Presentation/audio/VFX layers | Runtime simulation must not play audio or VFX directly. |

Forbidden boundary:

- `SummonBehaviorRuntime` must not own `EntityIdAllocator`.
- `SummonBehaviorRuntime` must not write `WorldState` directly.
- `SummonBehaviorRuntime` must not calculate placement legality directly.
- `EntitySpawnMaterializer` must not modify Utility or Behavior phase.
- `EntitySpawnRequest` must not carry allocated entity ids.
- `EntitySpawnMaterializer` placement/materialization/id allocation/write path ownership must not change.

## 6. Tick Seam Recommendation

| Candidate Seam | Pros | Cons | Parity Risk | Recommendation |
| --- | --- | --- | --- | --- |
| A. PreMovement state lane handles phase/cooldown progression and trigger intent emission | Matches current Utility timing owner; preserves movement suppression timing | Still needs post-attack materialization handoff | Medium | Use for state progression and request/intent emission |
| B. BeforeAttack or post-attack resolve lane handles trigger materialization directly | Close to materializer and source final validation | Moves timing away from current Utility seam and risks movement/replay drift | High | Reject for state progression |
| C. Current parity split: pre-movement collects trigger intents, post-attack materializes spawn requests | Preserves Utility parity, request sorting, id allocation timing, and write path | Requires source-neutral trigger vocabulary for future Behavior source | Low | Recommended |

Recommended seam:

- Future Behavior state progression and trigger emission should run at the same semantic pre-movement state seam as current Utility Summon.
- Spawn request materialization should remain in the post-attack `EntitySpawnMaterializer` path.
- BehaviorModule introduction must not create immediate `WorldState` writes.

## 7. Determinism / Replay / Hash Contract

Current names to preserve:

- `SummonCommitted`.
- `SummonSkipped`.
- `SummonedEntities`.
- `EnemyDefinitionBindings`.
- Existing `SummonedEntityState.SourceEntityId` and `SourceEffectIndex` semantics until a migration compatibility plan replaces the source index vocabulary.

Future names requiring migration plan:

- A future behavior-state hash section such as `EnemySummonBehaviors`.
- Any replacement for `EnemyUtilities` hash lines for migrated Summon state.
- Any neutral replacement for `PreMovement.UtilityTriggers` or Utility-specific event/update names.
- Any replacement for `Effect=` in replay/export-visible Summon events.

Hash parity risks:

- Current Utility state hash includes `effectKind`, cooldown, phase, windup ticks, active fields, origin, recover ticks, activation sequence, and movement suppression.
- Future behavior state hash must not double-count state while Utility state still exists.
- Migrating Summon state from `EnemyUtilities` to future behavior hash must happen in one compatibility-reviewed migration.
- `SummonedEntities` and `EnemyDefinitionBindings` hash lines must remain stable across migration.
- Child alive-count query must not depend on runtime child-id lists that can drift from authoritative entity state.

Required replay/hash tests:

- `BehaviorSummon_ReplayNamesPreservedOrMigrated`.
- `BehaviorSummon_DeterminismHashParity`.
- `MigratedSummon_ReplayParityAgainstUtilityBaseline`.
- `BehaviorSummon_PreservesRequestPayloadSnapshot`.
- `BehaviorSummon_EmitsSpawnRequestInUtilityParityOrder`.

## 8. Asset / Compiler / Migration Outline

Future asset shape is conceptual only:

- The implemented `EnemySummonBehaviorModuleAsset` directly owns initial delay, cooldown, and nested `SummonMinionAuthoring` config for the compile skeleton.
- A future profile wrapper such as `SummonBehaviorProfile` or `EnemySummonExecutionProfile` may be added only if presentation/audio/VFX parity settings grow or multiple summon modules need shared presets.
- No production `EnemySummonBehaviorModuleAsset` asset or migration is added in this design pass.

| Current Authoring Field | Future Behavior Field | Migration Rule | Validation | Notes |
| --- | --- | --- | --- | --- |
| `EnemyUtilityEffectAuthoring.initialDelaySeconds` | `Timing.InitialDelaySeconds` | Copy exactly and compile with same tick rate | Non-negative and compiles to non-negative ticks | Preserve first activation timing. |
| `EnemyUtilityEffectAuthoring.cooldownSeconds` | `Timing.CooldownSeconds` | Copy exactly and compile with same tick rate | Positive and compiles positive | Preserve recurring activation timing. |
| `SummonMinionAuthoring.spawnCountPerTrigger` | `SpawnPolicy.SpawnCountPerTrigger` | Copy exactly | Positive | One request per spawn index. |
| `SummonMinionAuthoring.maxAliveChildren` | `SpawnPolicy.MaxAliveChildren` | Copy exactly | Positive | Snapshot query parity required. |
| `SummonMinionAuthoring.candidatePattern` | `SpawnPolicy.CandidatePattern` | Copy exactly | Supported enum | Current supported value is `OrthogonalAdjacent4`. |
| `SummonMinionAuthoring.requireNoUnitAtSpawnCell` | `SpawnPolicy.RequireNoUnitAtSpawnCell` | Copy exactly | Boolean | Materializer evaluates. |
| `SummonMinionAuthoring.requireNoSolidAtSpawnCell` | `SpawnPolicy.RequireNoSolidAtSpawnCell` | Copy exactly | Boolean | Materializer evaluates. |
| `SummonMinionAuthoring.summonedArchetype` | `SpawnPolicy.SummonedArchetypeId` | Copy archetype id | Non-null, valid config, spawn defaults exist | Missing defaults must fail compile or pre-runtime validation. |
| `SummonMinionAuthoring.overrideHp` | `SpawnPolicy.OverrideHp` | Copy exactly | Boolean | Materializer applies. |
| `SummonMinionAuthoring.hpOverride` | `SpawnPolicy.HpOverride` | Copy exactly | Positive when override enabled | No change to default HP path. |
| `SummonMinionAuthoring.windupSeconds` | `Timing.WindupSeconds` | Copy exactly and compile with same tick rate | Positive and compiles positive | Presentation parity depends on this. |
| `SummonMinionAuthoring.suppressMovementDuringWindup` | `SuppressionPolicy.SuppressMovementDuringWindup` | Copy exactly | Boolean | Must preserve imminent windup suppression behavior. |
| `SummonMinionAuthoring.recoverySeconds` | `Timing.RecoverySeconds` | Copy exactly and compile with same tick rate | Non-negative | Zero recovery remains allowed. |
| `SummonMinionAuthoring.suppressMovementDuringRecover` | `SuppressionPolicy.SuppressMovementDuringRecover` | Copy exactly | Boolean | Must preserve recovery movement lock. |
| Presentation/audio/VFX references if any | Presentation profile or binding policy | Do not invent fields; copy only existing future-authored fields | Separate presentation/audio/VFX validation | Current Utility authoring has no dedicated Summon audio/VFX references in the inspected runtime shape. |

Compiler plan:

- Keep Utility compile isolated from Behavior compile.
- Keep Behavior module asset compile isolated from Utility knowledge.
- The duplicate Utility/Behavior Summon guard is implemented in `EnemyAiProfileCompiler` after both lanes compile, as documented by `Enemy-AI-Summon-Duplicate-Guard-Design.md`.
- Validate Summon behavior authoring for positive spawn count, positive max alive, non-null archetype, available spawn defaults, positive windup, positive cooldown, non-negative initial delay, non-negative recovery, and positive HP override when enabled.
- Detect migration residue by compiled runtime source, not by YAML string scans.

Migration order:

1. Accept runtime state design.
2. Accept replay/export compatibility plan; see [Enemy-AI-Summon-Replay-Export-Compatibility-Plan.md](./Enemy-AI-Summon-Replay-Export-Compatibility-Plan.md).
3. Implement compile skeleton, typed fixed slot, module asset, and duplicate guard with tests.
4. Introduce mutable Behavior Summon state and emitter while preserving the fixed typed slot. Status: implemented for test-local Behavior Summon path.
5. Add runtime parity tests while Utility-only content remains valid.
6. Run asset-scoped migration from Utility `SummonMinion` to future Behavior Summon after accepting [Enemy-AI-Summon-Asset-Migration-Plan.md](./Enemy-AI-Summon-Asset-Migration-Plan.md).
7. Validate migrated profile has no Utility Summon residue.
8. Validate replay/hash/presentation/audio/VFX parity.

Rollback strategy:

- Keep Utility-only assets valid until migration is complete.
- Keep migration asset-scoped, not whole-lane.
- Preserve a way to restore Utility `SummonMinion` authoring from migration data until Behavior parity is accepted.
- Do not migrate GravityFieldAura or RetiredLockNearbyBoxes as part of Summon rollback.

## 9. Test Matrix

| Test | Current/Future/Migration | Purpose | Required Before Code? |
| --- | --- | --- | --- |
| Same-tick multi-summoner ordering characterization | Current already covered | Confirms deterministic request/materialization order baseline | Yes, already recorded |
| Mutable request payload drift guard | Current already covered | Confirms request snapshot metadata does not drift before materialization | Yes, already recorded |
| Utility Summon replay | Current already covered | Baseline replay contract | Yes, already recorded |
| UtilityArchetypeSummon replay | Current already covered | Baseline archetype binding replay contract | Yes, already recorded |
| Core lane | Current already covered | Existing touched-cluster baseline evidence | Already recorded; rerun only when code changes require it |
| `SummonBehaviorRuntime_StateParity_DesignAccepted` | Future | Locks accepted design before runtime code starts | Yes |
| `DuplicateGuard_UtilityAndBehaviorSummon_FailsCompile` | Current | Prevents duplicate summon sources | Already implemented in compile skeleton |
| `BehaviorSummonOnly_ProfileCompiles` | Current | Verifies Behavior-only compile config is valid after the compile skeleton | Already implemented in compile skeleton |
| `BehaviorSummon_EmitsSpawnRequestInUtilityParityOrder` | Current | Preserves deterministic request and id allocation order | Implemented for test-local runtime/emitter parity |
| `BehaviorSummon_PreservesRequestPayloadSnapshot` | Current | Preserves origin/facing/team/tick snapshot metadata | Implemented for test-local runtime/emitter parity |
| `BehaviorSummon_MaxAliveParity` | Current | Verifies alive child count and planned child gate parity | Implemented for test-local runtime/emitter parity |
| `BehaviorSummon_SourceDeathCancelsOrSkipsAsUtility` | Current | Verifies hard invalid source behavior | Implemented for test-local runtime/emitter parity |
| `BehaviorSummon_SourceLeavesTopologyCancelsOrSuspendsAsUtility` | Current | Verifies topology participation loss shift/suspend parity | Implemented for test-local runtime/emitter parity |
| `BehaviorSummon_ReplayNamesPreservedOrMigrated` | Current | Prevents accidental replay/export rename | Implemented for test-local runtime/emitter parity |
| `BehaviorSummon_DeterminismHashParity` | Current | Verifies state hash migration and child metadata hash parity | Implemented for test-local runtime/emitter parity |
| `BehaviorSummon_PresentationWindupParity` | Current | Verifies warning and phase presentation parity | Minimum windup warning parity implemented for test-local runtime/emitter parity |
| `BehaviorSummon_AudioVfxParity` | Future | Verifies presentation-consumer parity where applicable | Before migration |
| `GravityFieldAura_Unchanged` | Current | Guards non-goal Utility effect | Already implemented for compile skeleton |
| `RetiredLockNearbyBoxes_GuardUnchanged` | Current | Guards retired compatibility behavior | Already implemented for compile skeleton |
| `UtilitySummonOnly_ProfileCompilesBeforeMigration` | Migration | Ensures Utility-only assets stay valid before migration | During migration |
| `UtilitySummonRemoved_BehaviorSummonAdded_ProfileCompiles` | Migration | Confirms asset-scoped replacement succeeds | During migration |
| `UtilityAndBehaviorSummon_ProfileCompileFails` | Migration | Confirms duplicate guard catches residue | During migration |
| `MigratedSummon_ReplayParityAgainstUtilityBaseline` | Migration | Confirms migrated behavior replay compatibility | During migration |
| `MigratedSummon_AssetYamlNoUtilityResidue` | Migration | Confirms migration removed old Utility source | During migration |

## 10. Explicit Non-Goals

- No production Summon asset migration.
- Runtime summon request emission from Behavior Summon is implemented for test-local Behavior Summon fixtures only.
- No Utility `SummonMinion` asset or YAML migration.
- No Utility whole-lane migration.
- No `GravityFieldAura` changes.
- No `RetiredLockNearbyBoxes` changes.
- No `EnemyBehaviorRuntimeSet` generic registry.
- No `logicModuleAssets`.
- No replay/export-visible name changes.
- No direct `WorldState` spawn writes.
- No `EntitySpawnRequest` contract changes.
- No `EntitySpawnMaterializer` placement/materialization/id allocation/write path ownership changes.

## 11. Option B Entry Criteria

- Runtime state design accepted.
- Duplicate Utility/Behavior Summon guard accepted.
- Asset migration plan accepted; see [Enemy-AI-Summon-Asset-Migration-Plan.md](./Enemy-AI-Summon-Asset-Migration-Plan.md).
- Replay/export compatibility plan accepted; see [Enemy-AI-Summon-Replay-Export-Compatibility-Plan.md](./Enemy-AI-Summon-Replay-Export-Compatibility-Plan.md).
- Presentation/audio/VFX parity plan accepted; see [Enemy-AI-Summon-Presentation-Audio-VFX-Parity-Plan.md](./Enemy-AI-Summon-Presentation-Audio-VFX-Parity-Plan.md).
- Behavior emitter request ordering contract accepted.
- Source metadata vocabulary accepted without Utility-only coupling.
- Option B implementation slicing and validation gates accepted; see [Enemy-AI-Summon-Option-B-Implementation-Plan.md](./Enemy-AI-Summon-Option-B-Implementation-Plan.md).
- Full lane / CI release gate policy decided.

BehaviorModule Summon compile skeleton exists. Mutable Summon runtime state and trigger emission are implemented for the test-local Behavior Summon path. Production asset migration has not started, and full lane was not run unless explicitly reported.
