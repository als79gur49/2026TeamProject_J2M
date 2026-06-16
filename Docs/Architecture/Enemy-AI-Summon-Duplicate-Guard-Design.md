# Enemy AI Summon Duplicate Guard Design

## 1. Decision Summary

- Duplicate Utility `SummonMinion` plus future Behavior Summon authoring must fail fast.
- This guard is an Option B prerequisite, not an Option B implementation.
- No `SummonBehaviorModule`, `EnemyBehaviorModuleKey.Summon`, `EnemySummonBehaviorModuleAsset`, Summon runtime, or Utility migration is introduced by this document.
- The primary guard belongs in `EnemyAiProfileCompiler` after capability and behavior module compilation, where both lanes and profile context are available.
- Asset contract tests should cover the compiler contract. Runtime definition validation can be a secondary safety net only if useful source context is preserved.

## 2. Current State

- `EnemyAiProfile` has four root authoring lanes: `coreAuthoring`, `brainAuthoring`, `capabilityAssets`, and `behaviorModuleAssets`.
- Utility Summon remains in the capability lane as `EnemyUtilityEffectKind.SummonMinion`.
- `GravityFieldAura` remains a Utility/board-modifier style effect and is not part of the Summon duplicate guard.
- `RetiredLockNearbyBoxes` remains a retired serialized compatibility slot and already fails through the retired Utility guard.
- BehaviorModule lane is currently Charge-only. Behavior Summon does not exist.
- Option C already extracted spawn/entity creation materialization through `EntitySpawnRequest` and `EntitySpawnMaterializer`.
- `EntitySpawnRequest` does not carry an allocated entity id; id allocation happens during materialization after placement succeeds.
- Same-tick multi-summoner ordering and mutable spawn request payload drift guards are already characterized.

## 3. Current Compile Path

```text
EnemyAiProfile.CreateRuntimeDefinition
  -> EnemyAiProfileCompiler.Compile
    -> validate profile + simulation tick rate
    -> profile.CoreAuthoring.Compile
    -> profile.BrainAuthoring.Compile
    -> CompileCapabilities(profile.name, profile.CapabilityAssets)
       -> compile each capability asset
       -> validate one runtime per capability family
       -> Utility compiles EnemyUtilityEffectAuthoring entries
          -> SummonMinion compiles active runtime
          -> GravityFieldAura compiles active runtime
          -> RetiredLockNearbyBoxes throws retired guard
    -> CompileBehaviors(profile.name, profile.BehaviorModuleAssets)
       -> compile each behavior module asset
       -> validate duplicate behavior module key
       -> currently supports Charge only
    -> ValidateBehaviorRequirements(profile, behaviors)
       -> charge resolver requires Charge behavior module
    -> new EnemyAiRuntimeDefinition(core, brain, capabilities, behaviors)
       -> runtime definition validates core, brain, capabilities, behaviors, and Charge requirement
```

## 4. Why Duplicate Guard Is Required

If a future Behavior Summon module is added while legacy Utility `SummonMinion` remains authored on the same `EnemyAiProfile`, the same enemy can gain two summon sources in one tick. That creates policy drift risk across trigger timing, max-alive checks, spawn request ordering, entity id allocation, `SummonCommitted` / `SummonSkipped` event output, `SummonedEntityState` metadata, determinism hash input, replay/export contracts, and asset migration safety.

The invalid state is specifically:

```text
same EnemyAiProfile
  has Utility capability effect kind SummonMinion
  has future Behavior Summon module
```

Utility-only Summon content remains valid. Future Behavior Summon-only content becomes valid only after Option B introduces the real module key, runtime state, emitter, tests, and migration contract.

## 5. Detection Model

### Utility Summon Detection

Detect Utility Summon from compiled runtime effects, not from a serialized YAML scan:

```csharp
private static bool ContainsUtilitySummonMinion(EnemyCapabilityRuntimeSet capabilities)
{
    if (!capabilities.TryGetUtility(out var utility))
    {
        return false;
    }

    for (var i = 0; i < utility.Effects.Count; i++)
    {
        if (utility.Effects[i].Kind == EnemyUtilityEffectKind.SummonMinion)
        {
            return true;
        }
    }

    return false;
}
```

For future error messages, use a helper that also returns the first Utility effect index:

```csharp
private static bool TryFindUtilitySummonMinion(
    EnemyCapabilityRuntimeSet capabilities,
    out int effectIndex)
```

Detection results:

| Current authored state | Utility Summon duplicate-guard presence |
| --- | --- |
| No Utility capability | false |
| Utility capability with no `SummonMinion` effects | false |
| Utility capability with one or more `SummonMinion` effects | true |
| Utility capability with `GravityFieldAura` only | false |
| Utility capability with `RetiredLockNearbyBoxes` | existing retired guard fails first |

### Future Behavior Summon Detection

Do not add placeholders in the current codebase. When Option B introduces real Behavior Summon support, use the actual typed runtime set API:

```csharp
var hasBehaviorSummon = behaviors.Contains(EnemyBehaviorModuleKey.Summon);
```

or, if the fixed typed slot pattern continues:

```csharp
var hasBehaviorSummon = behaviors.TryGetSummon(out var summon);
```

`EnemyBehaviorModuleKey.Summon`, `TryGetSummon`, and a Summon runtime slot must be added only as part of the future Option B implementation.

## 6. Recommended Guard Location

Primary location: `EnemyAiProfileCompiler` after `CompileCapabilities(...)` and `CompileBehaviors(...)`, before `EnemyAiRuntimeDefinition` creation.

Reasons:

- It can see both compiled lanes.
- It still has `EnemyAiProfile` and behavior/capability asset context for a useful error message.
- It fails before runtime logic or spawn materialization can observe two summon sources.
- It keeps `EnemyBehaviorModuleAsset` compile isolated from Utility knowledge.
- It keeps `EnemyUtilityCapabilityAsset` compile isolated from BehaviorModule knowledge.

Future compiler shape:

```csharp
var capabilities = CompileCapabilities(profile.name, profile.CapabilityAssets, simulationTicksPerSecond);
var behaviors = CompileBehaviors(profile.name, profile.BehaviorModuleAssets, simulationTicksPerSecond);
ValidateNoDuplicateSummonSources(profile, capabilities, behaviors);
ValidateBehaviorRequirements(profile, behaviors);
return new EnemyAiRuntimeDefinition(core, brain, capabilities, behaviors);
```

Secondary safety net: asset contract tests should verify that duplicate authoring fails at profile compile time and that non-Summon Utility effects do not trigger the duplicate guard.

Optional runtime validation: `EnemyAiRuntimeDefinition.Validate(...)` may add a no-context safety net only if it can preserve a useful message. It must not replace the compiler guard because runtime definitions lack authoring effect index and asset-name context.

Rejected locations:

| Candidate | Decision |
| --- | --- |
| `EnemyBehaviorModuleAsset.Compile(...)` | Not suitable; Behavior modules do not know Utility capabilities. |
| `EnemyUtilityCapabilityAsset.Compile(...)` | Not suitable; Utility capabilities do not know Behavior modules. |
| Asset contract tests only | Useful coverage, not a replacement for compiler fail-fast. |

## 7. Error Message Contract

The future fail-fast message must include:

- Profile asset name, and path when the test/validation context can provide it.
- Utility `SummonMinion` source.
- Utility effect index, for example `Utility.effects[0]`.
- Behavior Summon source.
- Behavior module key and module asset name.
- Remediation guidance: remove Utility `SummonMinion`, do not add Behavior Summon, or run an explicit asset-scoped migration.

Example:

```text
Enemy AI profile 'EnemyAi_ArchetypeSummoner' cannot author both Utility SummonMinion effect at Utility.effects[0] and Behavior Summon module 'EnemySummonBehaviorModule_Standard'. Summon migration must be asset-scoped; remove one source before compile.
```

The duplicate Summon message must not mention `GravityFieldAura`, must not treat `RetiredLockNearbyBoxes` as Summon, and must not collapse into a generic duplicate behavior-module message.

## 8. Future Test Matrix

| Test | Current or Future | Purpose |
| --- | --- | --- |
| `UtilitySummonOnly_ProfileCompiles` | Current | Existing Utility Summon content remains valid. |
| `NoScopeViolation_NoSummonBehaviorSymbolsInAssets` | Current | Confirms current implementation does not add Option B runtime symbols. |
| `GravityRetiredGuards_Unchanged` | Current | Confirms existing GravityFieldAura and retired guard behavior remains unchanged. |
| `BehaviorSummonOnly_ProfileCompiles_AfterOptionBExists` | Future | Behavior Summon-only content is valid after the real Option B module exists. |
| `UtilitySummonAndBehaviorSummon_ProfileCompileFails` | Future | Primary compiler duplicate guard fails fast. |
| `DuplicateGuard_MessageIncludesProfileAndBothSources` | Future | Error includes profile, Utility effect index, and Behavior module source. |
| `GravityFieldAuraWithBehaviorSummon_DoesNotTriggerSummonDuplicateGuard` | Future | GravityFieldAura stays excluded from the Summon duplicate guard. |
| `RetiredLockNearbyBoxes_StillFailsByRetiredGuard_NotDuplicateGuard` | Future | Retired Utility kind still fails through the retired guard. |
| `MultipleUtilitySummonEffects_AllowedOrRejectedByExistingUtilityPolicy` | Future | Duplicate guard does not invent a new intra-Utility policy. |
| `UtilitySummonMigration_RemovedOldUtility_AddsBehavior_Passes` | Future | Asset-scoped migration removes the old source before adding the new one. |
| `ReplayNamesPreserved_AfterMigration` | Future | `SummonCommitted` and `SummonSkipped` names remain stable. |
| `SpawnRequestOrdering_Preserved_AfterBehaviorEmitter` | Future | Behavior emitter preserves deterministic spawn request and id allocation ordering. |

Current tests can cover Utility-only compile behavior, no-scope-violation symbol scans, and unchanged Gravity/retired guard behavior. Behavior-only, duplicate Utility+Behavior, migration parity, and replay/export parity tests must wait until the future Summon BehaviorModule exists.

## 9. Non-Goals

- No `SummonBehaviorModule`.
- No `EnemyBehaviorModuleKey.Summon`.
- No `EnemySummonBehaviorModuleAsset`.
- No `EnemySummonBehaviorRuntime`.
- No Utility `SummonMinion` asset or YAML migration.
- No whole-lane Utility migration.
- No `GravityFieldAura` migration or behavior change.
- No `RetiredLockNearbyBoxes` migration, deletion, or duplicate Summon treatment.
- No `EnemyBehaviorRuntimeSet` generic registry.
- No `logicModuleAssets`.
- No replay/export-visible name changes.
- No direct `WorldState` spawn writes.
- No `EntitySpawnRequest` or `EntitySpawnMaterializer` contract changes.

## 10. Option B Entry Criteria

Before Option B starts:

- Duplicate Utility/Behavior Summon guard design is accepted.
- BehaviorModule Summon runtime state shape is designed.
- Behavior emitter request ordering contract is designed.
- Source metadata vocabulary is not Utility-only.
- Asset migration plan for `SummonMinion` authoring is written.
- Replay/export compatibility plan is written.
- Presentation, audio, and VFX parity tests are planned.
- Full lane / CI release gate policy is decided.

Duplicate Utility/Behavior Summon guard is designed as an Option B prerequisite. SummonBehaviorModule migration has not started. Full lane was not run unless explicitly reported.
