# Enemy AI Summon Internal Type Decoupling Closeout

Last audited revision: `f46cea2f7a10fcde3c32753d1f34a27e9619a0a4` on branch `pr/enemy-ai-retired-melee-runtime-removal`.

## Status

Slice B code is implemented, but Slice B is not accepted until the current revision has both:

- production manual play smoke on a stage containing `EnemyAi_ArchetypeSummoner`
- fresh unfiltered `./run_tests.sh full` evidence preserved from this revision

Until those gates run, the correct closeout state is: **Slice B code implemented; validation gaps must be closed**.

## Slice A Baseline

Executable Utility Summon code remains retired. `EnemyUtilityEffectKind.RetiredSummonMinion = 0` is a tombstone, `RetiredLockNearbyBoxes = 1` and `GravityFieldAura = 2` remain in place, and Behavior Summon owns production Summon execution. The spawn seam remains `EntitySpawnRequest` to `EntitySpawnMaterializer` to `FinalizationBatch.SpawnEntity`.

The compatibility vocabulary intentionally retained after Slice A remains retained here:

- replay/export `Effect=0` and `SourceEffectIndex=0`
- `UtilityWindup` and `UtilitySummonSpawn`
- `EnemyAudioCue.Windup` and `EnemyAudioCue.Active`
- `SummonCommitted` and `SummonSkipped`

## B1 DTO Decoupling

Current active DTO ownership:

- `EnemySummonAuthoring`: serialized inline authoring data owned by `EnemySummonBehaviorModuleAsset`
- `EnemySummonCompiledConfig`: immutable compiled Summon config with tick/config values
- `EnemySummonBehaviorRuntime`: runtime behavior module wrapper containing initial delay, cooldown, and compiled Summon config
- `EnemySummonBehaviorRuntimeState`: mutable per-source runtime state
- `EnemySummonBehaviorTriggerIntent`: trigger intent, not a spawn request
- `EnemySummonChildLimitPolicy`: source/effect-scoped live child limit query

Old active internal DTO names are retired from current source and tests:

- `SummonMinionAuthoring`
- `SummonMinionRuntime`
- `EnemyUtilitySummonPolicy`

Historical documentation can still mention those names as migration provenance. Current architecture documentation must not describe them as active owners.

## Serialization Evidence

Production asset:

`Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/BehaviorModules/Enemy_Summon/EnemySummonBehaviorModule_ArchetypeSummoner.asset`

Observed serialized contract:

- asset `.meta` guid: `a73bf2a62ddc4cc88cc8587d38288135`
- script guid: `94344f6fd7714fd6954a260a1d8a68c3`
- top-level serialized fields: `initialDelaySeconds`, `cooldownSeconds`, `summon`
- nested `summon` field names remain stable
- no `managedReferences` registry
- no serialized C# nested type name or assembly-qualified type name
- no production asset, prefab, scene, or `.meta` diff in Slice B implementation commits

Because `EnemySummonAuthoring` is an inline `[Serializable]` object and not `[SerializeReference]`, the rename does not persist type identity in YAML. Reimport parity is still required as an acceptance gate, and `EnemyAiProfileAssetContractTests` now includes a production asset reimport/YAML/meta stability test.

## Production Config Parity

The ArchetypeSummoner production Summon behavior must retain these values:

- `initialDelaySeconds = 10`
- `cooldownSeconds = 10`
- `spawnCountPerTrigger = 1`
- `maxAliveChildren = 2`
- `candidatePattern = 0`
- `requireNoUnitAtSpawnCell = true`
- `requireNoSolidAtSpawnCell = true`
- summoned archetype guid `8da265900dc94540a0e150fa08ff4c7f`
- `overrideHp = true`
- `hpOverride = 1`
- `windupSeconds = 1.7`
- `recoverySeconds = 0.7`
- `suppressMovementDuringWindup = true`
- `suppressMovementDuringRecover = true`

Compiled parity expected at 60 Hz:

- initial delay `600`
- cooldown `600`
- windup `102`
- recovery `42`
- spawn count `1`
- max alive `2`
- orthogonal adjacent candidate pattern
- unit and solid occupancy checks enabled
- summoned archetype id `PassiveContactMinion`
- HP override enabled with value `1`
- movement suppression enabled for windup and recover

`EnemySummonAuthoring` defaults are not the production asset contract. Any future newly-created Summon behavior asset must be reviewed separately for authoring default drift.

## B2 Presentation Decoupling

Current active Summon lifecycle carrier:

- `TickEnemySummonPresentationSignal`
- `EnemySummonPresentationPhase.WindupStarted`
- `EnemySummonPresentationPhase.RecoverStarted`
- `EnemySummonPresentationPhase.Canceled`
- `TickPresentationData.EnemySummonSignals`

`EnemySummonSignals` is presentation-only. It does not allocate entities, own `WorldState`, or mutate authoritative replay state.

`SummonWindupWarnings` remains as a compatibility presentation carrier for existing warning/audio/VFX surfaces. This is intentional and not a Slice B failure.

## Dual Carrier Responsibility

`EnemySummonSignals`:

- drives Summon-specific view state flags
- represents lifecycle transitions only
- is consumed by `EnemyViewPresentationMapper`

`SummonWindupWarnings`:

- preserves warning payloads including source cell, topology, facing, windup timing, effect index, activation sequence, and seed
- drives `EnemyAudioRequestPlanner` windup cue compatibility
- drives `EnemyVfxRequestPlanner` `UtilityWindup` compatibility

Spawn presentation remains driven by actual spawn/visibility/binding facts. `EnemyAudioCue.Active` and `UtilitySummonSpawn` must not be produced from `RecoverStarted` alone.

Focused tests now pin:

- `WindupStarted` emits once while windup warnings can persist during windup
- `RecoverStarted` emits once
- `Canceled` emits on source invalidation
- simultaneous warning plus Summon lifecycle signal does not double-emit windup audio or VFX
- mapper Summon flags reset on the next tick without a signal

## Legacy Raw Kind Adapter

`EnemyUtilityScalePulsePresentationDriver` keeps a bounded serialized compatibility seam for production prefab raw `utilityKind: 3`.

Current typed enum values:

- `EnemyUtilityPresentationKind.None = 0`
- `EnemyUtilityPresentationKind.RetiredLockNearbyBoxes = 1`
- `EnemyUtilityPresentationKind.GravityFieldAura = 2`

Raw `3` is not an active enum member. It maps only to Summon scale pulse compatibility inside `EnemyUtilityScalePulsePresentationDriver`. It must not revive Utility Summon execution and must not be confused with `EnemyUtilityEffectKind.RetiredSummonMinion = 0`.

Focused tests now pin:

- raw `3` maps to Summon pulse
- `GravityFieldAura` does not map to Summon pulse
- unknown raw values do not map to Summon pulse

Prefab migration and adapter removal are Slice C candidates, not Slice B acceptance requirements.

## Required Validation Before Acceptance

Focused lanes:

```bash
git diff --check
./run_tests.sh core
./run_tests.sh full --filter EnemyAiProfileAssetContractTests
./run_tests.sh --integration-simulation --filter BehaviorSummon
./run_tests.sh --integration-simulation --filter MigratedSummon
./run_tests.sh --integration-replay --filter MigratedSummon
./run_tests.sh full --filter EntitySpawnMaterializer
./run_tests.sh full --filter StageRuntimeBuilderTests
./run_tests.sh full --filter CampaignStageAssets_WithSummonArchetypes_HaveRuntimeAndPresentationArchetypeCatalogs
./run_tests.sh --integration-simulation --filter GravityFieldAura
./run_tests.sh full --filter ChargePassiveContact_StillUsesTargetSelection
./run_tests.sh full --filter RocketFaceChargeRuntimeContractTests
./run_tests.sh full --filter EnemyViewPresentationMapperTests
./run_tests.sh full --filter EnemyAudio
./run_tests.sh full --filter GameplayVfx
```

Manual play smoke must verify:

- initial delay, windup, recovery, trigger count, max-alive, blocked spawn, child death replenish
- warning audio once
- `UtilityWindup` once
- active audio once on committed spawn
- `UtilitySummonSpawn` once at the actual spawn
- no ghost cue/VFX on blocked, canceled, or max-alive cases
- source invalidation clears lingering Summon pulse/VFX
- production prefab raw `utilityKind: 3` pulse still works
- no missing serialized field or type reset warnings

Fresh unfiltered full:

```bash
git rev-parse HEAD
git status --short
date
./run_tests.sh full
```

Preserve fresh artifacts under:

`TestResults/Preserved/post-summon-slice-b-acceptance-<short-sha>-<timestamp>/`

Do not reuse stale full artifacts. If full is red, report touched-cluster identity separately and do not claim project-wide green.

## Rollback

B1 rollback group:

- restore old DTO declarations and references
- restore old compile/runtime consumers
- restore old child-limit policy
- restore related tests

B2 rollback group:

- remove/restore `TickEnemySummonPresentationSignal`
- remove/restore `EnemySummonPresentationPhase`
- restore `TickPresentationData` field shape
- restore producer/consumer mapping
- restore audio/VFX/test mapping
- restore previous scale-pulse compatibility behavior

Not rollback targets:

- Slice A Utility Summon executable retirement
- enum tombstones
- production Summon Behavior asset
- `EntitySpawnRequest`
- `EntitySpawnMaterializer`
- `FinalizationBatch.SpawnEntity`
- Gravity/Charge/PassiveContact behavior

## Slice C Candidates

Slice C is optional and must only be considered after Slice B acceptance:

- rename `UtilityWindup`
- rename `UtilitySummonSpawn`
- rename `Effect` / `SourceEffectIndex` vocabulary
- migrate production prefab raw `utilityKind: 3`
- remove the legacy raw numeric adapter
- add replay/audio/VFX version compatibility where needed
