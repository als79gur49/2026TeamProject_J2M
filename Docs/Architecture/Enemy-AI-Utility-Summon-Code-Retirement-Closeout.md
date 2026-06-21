# Enemy AI Utility Summon Code Retirement Closeout

Date: 2026-06-19

## Goal

Retire executable Utility Summon code while preserving serialization and presentation compatibility. After this slice, Utility has one active execution effect, `GravityFieldAura`; `RetiredSummonMinion` and `RetiredLockNearbyBoxes` are fail-fast tombstones. Behavior Summon remains the only Summon runtime owner.

## Before And After

Before this slice, no production Utility Summon asset was active, but the codebase could still author, compile, progress, emit, and convert Utility Summon execution paths. After this slice:

- Utility authoring no longer owns a summon payload.
- Utility compile cannot create a Summon runtime config.
- Serialized kind `0` maps to `RetiredSummonMinion` and fails with migration guidance.
- Utility runtime no longer advances Summon delay, cooldown, windup, recovery, max-alive gating, movement suppression, trigger emission, or spawn-request conversion.
- Behavior Summon remains unchanged as the production Summon runtime owner.

## Enum Tombstone

`EnemyUtilityEffectKind` keeps numeric compatibility:

- `RetiredSummonMinion = 0`
- `RetiredLockNearbyBoxes = 1`
- `GravityFieldAura = 2`

The active `SummonMinion` enum symbol was removed. Kind `0` is not executable and reports: `Utility Summon is retired; use EnemySummonBehaviorModuleAsset.`

## Authoring And Compiler

`EnemyUtilityEffectAuthoring.summon` was removed from the Utility authoring union. New Utility effects default to `GravityFieldAura` instead of the retired tombstone. `SummonMinionAuthoring` remains because `EnemySummonBehaviorModuleAsset` still uses it as the shared Behavior Summon DTO.

The compiler now has explicit cases for:

- `RetiredSummonMinion`: fail-fast migration guidance.
- `RetiredLockNearbyBoxes`: existing retired guard.
- `GravityFieldAura`: active Utility compile path.

The cross-lane Utility Summon plus Behavior Summon duplicate guard was removed because Utility Summon compile success no longer exists. Duplicate Behavior Summon, duplicate Charge Behavior, and null Behavior module guards remain.

## Runtime And Trigger Path

`EnemyLogic` no longer contains Utility Summon progression, movement suppression, topology/source handling, max-alive checks, or trigger creation. Generic Utility runtime iteration and GravityFieldAura progression remain.

`TickPipeline.PhaseResults` no longer converts Utility triggers into `EntitySpawnRequest`. Behavior Summon trigger conversion remains the canonical Summon request path. `EntitySpawnRequest`, `EntitySpawnMaterializer`, placement legality, ID allocation ownership, failed-placement ID non-consumption, `SummonedEntityState`, `EnemyDefinitionBindingState`, and `FinalizationBatch.SpawnEntity` were not changed.

## Catalog And Stage Detection

Gameplay host Summon archetype reference collection is Behavior-only through `EnemySummonBehaviorModuleAsset`. Utility capabilities are no longer scanned for Summon archetypes, and GravityFieldAura does not create a Summon catalog dependency.

Stage Summon detectors now treat Behavior Summon as the only canonical Summon source. Legacy Utility Summon detector compatibility was removed.

## Presentation And Replay Compatibility

Execution dependencies on Utility Summon were removed, but external compatibility vocabulary remains:

- `Effect=0`
- `SourceEffectIndex=0`
- `EnemyUtilityPresentationKind.SummonMinion`
- `UtilityWindup`
- `UtilitySummonSpawn`
- `SummonCommitted`
- `SummonSkipped`
- `Final.EnemySummonBehaviors`
- `Final.SummonedEntities`
- `Final.EnemyDefinitionBindings`
- `Final.EnemyUtilities` for GravityFieldAura

Behavior Summon continues to emit windup warning, spawn binding, audio, VFX, replay, and hash data through the compatibility vocabulary.

## Test Migration Matrix

Utility execution tests were removed or replaced after their semantic contracts were covered elsewhere:

- Utility compile success and Utility plus Behavior duplicate tests became retired kind `0` fail-fast tests.
- Utility Summon delay, cooldown, windup, recovery, source death, topology suspension, max-alive, movement suppression, presentation, audio, VFX, replay, and hash contracts are covered by BehaviorSummon and MigratedSummon tests.
- Shared spawn contracts remain as EntitySpawnMaterializer or Behavior Summon tests, including payload snapshot isolation, failed placement, deterministic order, `SummonedEntityState`, and `EnemyDefinitionBindingState`.
- Stage detector tests now assert Behavior Summon true, GravityFieldAura false, empty Utility false, and non-Summon/null false.

The former `JPeterUtilitySummonRuntimeContractTests` file was renamed to `MigratedSummonRuntimeContractTests` with its `.meta` file preserved.

## Preserved Lanes

GravityFieldAura remains the only active Utility execution effect. Its production asset keeps `kind: 2`, timing values, and gravity payload; only an inactive legacy `summon:` YAML block was removed.

RetiredLockNearbyBoxes remains a fail-fast tombstone. Charge Behavior and PassiveContact were not changed.

## Static Scans

Executable residue scan over `Assets` returned no hits for:

- `EnemyUtilityEffectKind.SummonMinion`
- `case EnemyUtilityEffectKind.SummonMinion`
- `ValidateNoDuplicateSummonSources`
- `ResolveSummonMinion`
- `EnemyUtilitySummonTrigger`

`RetiredSummonMinion` appears only in the enum, explicit runtime/authoring rejection, and retired guard tests. Production `campaign-main` has no `kind: 0` Utility asset hits. `SummonMinionAuthoring` and `SummonMinionRuntime` remain as shared Behavior/spawn DTO names for Slice B.

## Validation

Automated lanes run on this worktree:

- `git diff --check`: passed.
- `./run_tests.sh core`: passed, Core EditMode `189 total / 0 failed`, Core PlayMode `33 total / 0 failed`.
- `./run_tests.sh full --filter EnemyAiProfileAssetContractTests`: passed, EditMode `24 total / 0 failed`, PlayMode `0 total / 0 failed`.
- `./run_tests.sh full --filter EnemyAiRuntimeDefinitionGuardTests`: passed, EditMode `18 total / 0 failed`, PlayMode `0 total / 0 failed`.
- `./run_tests.sh --integration-simulation --filter BehaviorSummon`: passed, EditMode `28 total / 0 failed`.
- `./run_tests.sh --integration-simulation --filter MigratedSummon`: passed, EditMode `24 total / 0 failed`.
- `./run_tests.sh --integration-replay --filter MigratedSummon`: passed, EditMode `1 total / 0 failed`.
- `./run_tests.sh full --filter EntitySpawnMaterializer`: passed, EditMode `1 total / 0 failed`, PlayMode `0 total / 0 failed`.
- `./run_tests.sh full --filter StageRuntimeBuilderTests`: passed, EditMode `69 total / 0 failed`, PlayMode `0 total / 0 failed`.
- `./run_tests.sh full --filter CampaignStageAssets_WithSummonArchetypes_HaveRuntimeAndPresentationArchetypeCatalogs`: passed, EditMode `1 total / 0 failed`, PlayMode `0 total / 0 failed`.
- `./run_tests.sh full --filter GameplaySceneHostConfiguration_CreateEnemy`: passed, EditMode `9 total / 0 failed`, PlayMode `0 total / 0 failed`.
- `./run_tests.sh --integration-simulation --filter GravityFieldAura`: passed, EditMode `18 total / 0 failed`.
- `./run_tests.sh full --filter ChargePassiveContact_StillUsesTargetSelection`: passed, EditMode `1 total / 0 failed`, PlayMode `0 total / 0 failed`.
- `./run_tests.sh full --filter RocketFaceChargeRuntimeContractTests`: passed, EditMode `17 total / 0 failed`, PlayMode `0 total / 0 failed`.

- Fresh unfiltered `./run_tests.sh full`: red, exit code `1`, Full EditMode `5727 total / 36 failed`, Full PlayMode not run because EditMode failed. Preserved at `TestResults/Preserved/post-utility-summon-code-retirement-fdc9bf5a-20260619-214829/`.
- Full failure inventory did not report new touched-cluster failures in the Enemy/Summon/Gravity/Charge retirement lane; broad unrelated failures remain separated in the preserved `failure-inventory.txt` and `release-gate-report.md`.
- Manual play smoke was not run in this CLI-only slice.

## Rollback

Rollback is code-retirement rollback only:

1. Restore removed or renamed Utility Summon tests and `.meta` files.
2. Restore `SummonMinion = 0`, Utility authoring payload, compile branch, duplicate guard, runtime progression, trigger emission, request conversion, host/catalog Utility scan, stage detector legacy branch, and replay expectations.
3. Reimport/compile Unity and rerun focused lanes.

Production Summon asset migration is not rolled back by this code rollback. `EnemySummonBehaviorModule_ArchetypeSummoner`, `EnemyAi_ArchetypeSummoner`, PassiveContact, Charge Behavior, GravityFieldAura active payload, `EntitySpawnRequest`, `EntitySpawnMaterializer`, and `FinalizationBatch.SpawnEntity` are not rollback targets.

## Follow-Up Debt

Slice B can decouple internal names and presentation vocabulary:

- Rename or move `SummonMinionAuthoring`.
- Rename or move `SummonMinionRuntime`.
- Decouple `EnemyUtilityPresentationKind.SummonMinion` from Behavior Summon presentation.
- Evaluate external schema/cue names only in a separate compatibility-governed slice.
