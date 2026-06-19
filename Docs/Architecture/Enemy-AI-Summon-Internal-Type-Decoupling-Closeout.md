# Enemy AI Summon Internal Type Decoupling Closeout

Last audited revision: `5074716539da093e92adb4503a3f05591865659c` on branch `pr/enemy-ai-retired-melee-runtime-removal`.

## Status

Slice B code is implemented and focused automated validation is green on the current revision, but Slice B is not accepted until the current revision has production manual play smoke evidence.

- production manual play smoke on a stage containing `EnemyAi_ArchetypeSummoner`

Fresh unfiltered `./run_tests.sh full` evidence is preserved from this revision. The full lane is red with the same failure identities as the previous preserved full, and no new Slice B touched-cluster identity was found. Because production manual play was not executed in this CLI session, the correct closeout state is: **Slice B code implemented and focused-green; acceptance remains pending required manual validation**.

## 2026-06-20 Acceptance Run

Revision/worktree:

- HEAD: `5074716539da093e92adb4503a3f05591865659c`
- branch: `pr/enemy-ai-retired-melee-runtime-removal`
- diff hash: `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`
- tracked source/asset diff at run start: none
- production asset, prefab, scene, and `.meta` diff at run start: none

Static residue:

- active old DTO source/test references: `0`
- active `EnemyUtilityPresentationKind.SummonMinion` references: `0`
- `RetiredSummonMinion` references are bounded to tombstone declaration, compiler/runtime rejection, authoring rejection, and retired guard tests
- production prefab raw `utilityKind: 3` remains serialized and is handled only by the bounded `EnemyUtilityScalePulsePresentationDriver` compatibility adapter

Focused validation results:

- `git diff --check`: passed
- `./run_tests.sh full --filter EnemyAiProfileAssetContractTests`: EditMode `25` total, `0` failed; production reimport/YAML/meta stability passed
- `./run_tests.sh full --filter EnemyAiRuntimeDefinitionGuardTests`: EditMode `18` total, `0` failed
- `./run_tests.sh --integration-simulation --filter BehaviorSummon`: EditMode `31` total, `0` failed
- `./run_tests.sh --integration-simulation --filter MigratedSummon`: EditMode `24` total, `0` failed
- `./run_tests.sh --integration-replay --filter MigratedSummon`: EditMode `1` total, `0` failed
- `./run_tests.sh full --filter EnemyViewPresentationMapperTests`: EditMode `20` total, `0` failed
- `./run_tests.sh full --filter EnemyAudio`: EditMode `101` total, `0` failed
- `./run_tests.sh full --filter GameplayVfx`: EditMode `778` total, `0` failed; PlayMode `5` total, `0` failed
- `./run_tests.sh full --filter EntitySpawnMaterializer`: EditMode `1` total, `0` failed
- `./run_tests.sh full --filter StageRuntimeBuilderTests`: EditMode `69` total, `0` failed
- `./run_tests.sh full --filter CampaignStageAssets_WithSummonArchetypes_HaveRuntimeAndPresentationArchetypeCatalogs`: EditMode `1` total, `0` failed
- `./run_tests.sh --integration-simulation --filter GravityFieldAura`: EditMode `18` total, `0` failed
- `./run_tests.sh full --filter ChargePassiveContact_StillUsesTargetSelection`: EditMode `1` total, `0` failed
- `./run_tests.sh full --filter RocketFaceChargeRuntimeContractTests`: EditMode `17` total, `0` failed
- `./run_tests.sh core`: EditMode `189` total, `0` failed; PlayMode `33` total, `0` failed

B1/B2 automated evidence:

- production serialized values and compiled parity remained pinned by `EnemyAiProfileAssetContractTests`
- ForceUpdate reimport produced no production `.asset` or `.meta` dirty state
- `WindupStarted` one-shot, `RecoverStarted` one-shot, source-invalid `Canceled` one-shot, blocked non-cancel, max-alive non-cancel, and cancel dedupe are covered by focused behavior/presentation tests
- `EnemySummonSignals` remains the Summon view lifecycle carrier; `SummonWindupWarnings` remains the warning/audio/VFX compatibility carrier
- audio/VFX duplicate and ghost cue/spawn surfaces are covered by focused `EnemyAudio`, `GameplayVfx`, `BehaviorSummon`, and `MigratedSummon` tests
- legacy raw `utilityKind: 3` maps to Summon pulse; `GravityFieldAura` and unknown raw values do not map to Summon pulse

Fresh unfiltered full:

- command: `./run_tests.sh full`
- preserved folder: `TestResults/Preserved/post-summon-slice-b-acceptance-50747165-20260620-021032/`
- result: exit code `1`
- EditMode: `5736` total, `5685` passed, `36` failed, `15` skipped
- PlayMode: not run because EditMode failed; no stale PlayMode artifact was copied
- previous comparison baseline: `TestResults/Preserved/post-utility-summon-code-retirement-fdc9bf5a-20260619-214829/`
- failure identity comparison: previous `36`, current `36`, matched `36`, new `0`, removed `0`, message drift `0`
- new Slice B touched-cluster failure identities: `0`

Manual play:

- not run in this CLI session
- Slice B must remain pending until production manual play smoke records stage, Summoner entity ids, console/log evidence, capture evidence, and scenario results for normal lifecycle, source invalidation, blocked spawn, max alive, two Summoners, legacy raw adapter, Gravity, Charge, and PassiveContact

Remaining debt:

- introduce a typed `SummonSkipReason` / `SummonMaterializationOutcome` carrier to replace the bounded string fallback used for `SummonSkipped|Reason=SourceInvalid`
- keep Slice C vocabulary/schema migration deferred until Slice B acceptance is complete

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
