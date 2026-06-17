# Enemy AI Summon Asset-Scoped Migration Closeout

## Scope

This closeout records the asset-scoped migration of the production `EnemyAi_ArchetypeSummoner` profile from Utility-owned `SummonMinion` authoring to BehaviorModule-owned `Summon` authoring.

Non-goals preserved:

- No Utility whole-lane migration.
- No GravityFieldAura migration.
- No RetiredLockNearbyBoxes migration or deletion.
- No replay/export rename.
- No presentation/audio/VFX cue rename.
- No neutral cue label migration or dual-label compatibility.
- No generic registry or `logicModuleAssets` introduction.
- No direct `WorldState` spawn write.
- No `EntitySpawnRequest` or `EntitySpawnMaterializer` contract change.
- No tuning changes.

## Migrated Asset Allowlist

Production assets changed by this migration:

- `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/BehaviorModules/Enemy_Summon.meta`
- `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/BehaviorModules/Enemy_Summon/EnemySummonBehaviorModule_ArchetypeSummoner.asset`
- `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/BehaviorModules/Enemy_Summon/EnemySummonBehaviorModule_ArchetypeSummoner.asset.meta`
- `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/Profiles/Enemy_ArchetypeSummoner/EnemyAi_ArchetypeSummoner.asset`
- `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/Capabilities/Enemy_UtilitySummoner/EnemyCapability_ArchetypeSummoner.asset`

Tests and docs changed by this migration:

- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/EnemyAiProfileAssetContractTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Scenario/JPeterUtilitySummonRuntimeContractTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Replay/EnemyProfileContractReplayTests.cs`
- `Docs/Architecture/Enemy-AI-Summon-Asset-Scoped-Migration-Closeout.md`
- `Docs/Architecture/README.md`

## Before / After Asset Shape

Before:

- `EnemyAi_ArchetypeSummoner.asset`
  - `capabilityAssets`: 2 entries.
  - Summoner Utility capability guid `44788e5c202648d0bae1e8b5be647816`.
  - common passive-contact capability guid `bf094ca1fd8f4a378e18159ddaf0d4f0`.
  - `behaviorModuleAssets: []`.
- `EnemyCapability_ArchetypeSummoner.asset`
  - `effects[0].kind: 0`.
  - active Utility `SummonMinion` source.

After:

- `EnemyAi_ArchetypeSummoner.asset`
  - `capabilityAssets`: unchanged 2 entries.
  - Summoner Utility capability reference retained.
  - common passive-contact capability reference retained.
  - `behaviorModuleAssets`: exactly one entry, guid `a73bf2a62ddc4cc88cc8587d38288135`.
- `EnemyCapability_ArchetypeSummoner.asset`
  - `effects: []`.
  - no active Utility `SummonMinion` source.
- `EnemySummonBehaviorModule_ArchetypeSummoner.asset`
  - new production `EnemySummonBehaviorModuleAsset`.
  - asset-scoped name and location under `BehaviorModules/Enemy_Summon`.

## Utility To Behavior Field Mapping

The migrated Behavior module copies the production Utility source values exactly:

| Field | Migrated value |
| --- | --- |
| `initialDelaySeconds` | `10` |
| `cooldownSeconds` | `10` |
| `summon.spawnCountPerTrigger` | `1` |
| `summon.maxAliveChildren` | `2` |
| `summon.candidatePattern` | `0` |
| `summon.requireNoUnitAtSpawnCell` | `true` |
| `summon.requireNoSolidAtSpawnCell` | `true` |
| `summon.summonedArchetype` | guid `8da265900dc94540a0e150fa08ff4c7f` |
| `summon.overrideHp` | `true` |
| `summon.hpOverride` | `1` |
| `summon.windupSeconds` | `1.7` |
| `summon.suppressMovementDuringWindup` | `true` |
| `summon.recoverySeconds` | `0.7` |
| `summon.suppressMovementDuringRecover` | `true` |

External compatibility remains `Effect=0` / `SourceEffectIndex=0`. No new asset field was introduced for source effect index compatibility.

## Capability Decision

Decision: keep `EnemyCapability_ArchetypeSummoner.asset` and keep the profile capability reference, but remove the active Utility Summon source by making `effects: []`.

Reason: the compiler and asset contract allow an empty utility capability. Keeping the asset/reference avoids broader profile capability ownership churn and keeps this migration asset-scoped. The common passive-contact capability remains attached.

## Guard And Residue Result

Typed contract tests now assert:

- `EnemyAi_ArchetypeSummoner` has exactly one Behavior Summon module.
- `EnemyAi_ArchetypeSummoner` has no active Utility `SummonMinion` source.
- the migrated Utility capability has zero effects.
- no `logicModuleAssets` entry is introduced.
- GravityFieldAura remains active `kind: 2` with unchanged timing/settings.
- RetiredLockNearbyBoxes guard and enum value remain unchanged.

Residue scan result:

- `rg -n "SummonMinion|kind: 0|logicModuleAssets|behaviorModuleAssets|a73bf2a62ddc4cc88cc8587d38288135" ...EnemyAi_ArchetypeSummoner... ...EnemyCapability_ArchetypeSummoner.asset`
  - only the expected `behaviorModuleAssets` entry and new behavior guid were found.
  - no active Utility `SummonMinion`, no `kind: 0`, and no `logicModuleAssets` were found in the migrated profile/capability.
- `GravityFieldAura` scan still shows active `kind: 2`; the nested `summon:` block remains inactive serialized payload.
- RetiredLockNearbyBoxes scan found only runtime enum/retired guard/test references, not active production asset authoring.

## Replay / Hash / Export Result

`EnemyProfileContractReplayTests` now reads the migrated production profile and asserts:

- `Final.EnemySummonBehaviors` is present for the migrated source.
- no migrated active Utility `SummonMinion` owner remains.
- `Final.SummonedEntities` remains present.
- `Final.EnemyDefinitionBindings` remains present.
- `SummonCommitted` remains preserved.
- `Effect=0` and `SourceEffectIndex=0` remain preserved.
- no Utility + Behavior double-count occurs.

Hash text equality is intentionally not required because ownership moved from `EnemyUtilities` to `EnemySummonBehaviors`.

## Presentation / Audio / VFX Result

Production migrated scenario coverage now runs through the migrated profile path and preserves:

- `TickSummonWindupWarningSignal`
- `TickSummonedEnemyPresentationBinding`
- `TickVisibilityChange(Spawn)`
- `EnemyAudioCue.Windup`
- `EnemyAudioCue.Active`
- `UtilityWindup`
- `UtilitySummonSpawn`

No presentation/audio/VFX cue names or replay/export names were changed.

## Validation Results

Targeted and core lanes:

| Command | Result |
| --- | --- |
| `git diff --check` | Passed. |
| `./run_tests.sh --integration-simulation --filter BehaviorSummon` | Passed, `total=27 failed=0`. |
| `./run_tests.sh --integration-simulation --filter EnemyUtilitySummon_` | Passed, `total=12 failed=0`. |
| `./run_tests.sh --integration-simulation --filter MigratedSummon` | Passed, `total=24 failed=0`. |
| `./run_tests.sh --integration-replay --filter UtilitySummon` | Passed, `total=1 failed=0`. |
| `./run_tests.sh --integration-replay --filter UtilityArchetypeSummon` | Passed, `total=1 failed=0`. |
| `./run_tests.sh --integration-replay --filter MigratedSummon` | Passed, `total=1 failed=0`. |
| `./run_tests.sh full --filter EnemyAiProfileAssetContractTests` | Passed, `total=24 failed=0`. |
| `./run_tests.sh core` | Passed, EditMode `total=189 failed=0`, PlayMode `total=33 failed=0`. |

Full / CI:

- `./run_tests.sh full` was run on this revision and stopped in EditMode with `total=5739 failed=53`.
- The migration-specific `GravityFieldAura_Unchanged` assertion failure from the first full run was fixed and did not recur in the final full run.
- Full PlayMode did not run because full EditMode failed.
- No CI release gate was run in this slice.

## Rollback Path

Rollback file allowlist:

- `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/BehaviorModules/Enemy_Summon/EnemySummonBehaviorModule_ArchetypeSummoner.asset`
- `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/BehaviorModules/Enemy_Summon/EnemySummonBehaviorModule_ArchetypeSummoner.asset.meta`
- `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/BehaviorModules/Enemy_Summon.meta`
- `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/Profiles/Enemy_ArchetypeSummoner/EnemyAi_ArchetypeSummoner.asset`
- `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/Capabilities/Enemy_UtilitySummoner/EnemyCapability_ArchetypeSummoner.asset`
- the migrated production test files listed in this document.
- this closeout document and the README index entry.

Rollback order:

1. Remove or restore the new `Enemy_Summon` folder meta and `EnemySummonBehaviorModule_ArchetypeSummoner` asset/meta.
2. Restore `EnemyAi_ArchetypeSummoner.asset` to `behaviorModuleAssets: []`.
3. Restore `EnemyCapability_ArchetypeSummoner.asset` active Utility `effects[0].kind: 0` SummonMinion block with the copied values above.
4. Confirm GravityFieldAura remains unchanged.
5. Confirm RetiredLockNearbyBoxes guard tests still pass.
6. Confirm no production `EnemySummonBehaviorModuleAsset` remains in rollback state.

## Open Questions

- No migration-blocking open question remains for this asset-scoped slice.
- Broad full-lane failures remain outside this slice and are not claimed closed here.
