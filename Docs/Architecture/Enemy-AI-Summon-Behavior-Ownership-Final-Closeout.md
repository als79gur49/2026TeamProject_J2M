# Enemy AI Summon Behavior Ownership Final Closeout

## Executive Verdict

Enemy AI Summon behavior ownership refactor is closed locally.

Production Summon execution, configuration, runtime state, and presentation are Behavior-owned. Executable Utility Summon paths and production legacy bindings are removed. Shared replay/source-correlation compatibility vocabulary is intentionally retained.

Project-wide full remains red with the unchanged pre-existing failure identities outside this Summon closure scope.

Confidence: high for repository-local source, assets, prefabs, tests, and preserved evidence. External consumers outside this repository were not inventoried.

## Revision And Worktree

Audit start:

- HEAD: `2696e4c03296e364390974abeba7f8cc44d2f870`
- short HEAD: `2696e4c0`
- branch: `pr/enemy-ai-retired-melee-runtime-removal`
- tracked diff hash at audit start: `215e691789772086db08e39e8b8564bd62949316a60ec76a0476dfa4b10fa4a6`
- tracked dirty files at audit start: docs only
- untracked add candidate: `Docs/Architecture/Enemy-AI-Summon-Replay-Export-Vocabulary-C2-Readiness.md`
- asset/prefab/scene/meta dirty files: none observed

C1b implementation is committed at HEAD `2696e4c0`. C1b closeout evidence wording, C2 readiness wording, and this final umbrella closeout are current documentation-only worktree changes.

## Final Production Shape

```text
EnemyAi_ArchetypeSummoner
├─ capabilityAssets
│  └─ common PassiveContact
└─ behaviorModuleAssets
   └─ EnemySummonBehaviorModule_ArchetypeSummoner

EnemyUtilityEffectKind
├─ RetiredSummonMinion = 0
├─ RetiredLockNearbyBoxes = 1
└─ GravityFieldAura = 2

Presentation
├─ TickEnemySummonPresentationSignal
├─ EnemySummonScalePulsePresentationDriver
├─ EnemyVfxCue.SummonWindupWarning = 10
└─ EnemyVfxCue.SummonedEnemySpawn = 20

Compatibility
├─ Effect=0
├─ SourceEffectIndex=0
├─ SummonCommitted
└─ SummonSkipped
```

Production asset graph evidence:

- `EnemyAi_ArchetypeSummoner.asset` has one capability, common PassiveContact GUID `bf094ca1fd8f4a378e18159ddaf0d4f0`.
- `EnemyAi_ArchetypeSummoner.asset` has one behavior module, `EnemySummonBehaviorModule_ArchetypeSummoner` GUID `a73bf2a62ddc4cc88cc8587d38288135`.
- no `logicModuleAssets` entry exists in the production Summoner profile.
- no legacy `EnemyCapability_ArchetypeSummoner` production reference remains.
- runtime contract tests assert `TryGetSummonBehavior == true`, `TryGetUtility == false`, and `TryGetPassiveContact == true`.

Production Summon tuning remains:

| Field | Value |
| --- | --- |
| `initialDelaySeconds` | `10` |
| `cooldownSeconds` | `10` |
| `spawnCountPerTrigger` | `1` |
| `maxAliveChildren` | `2` |
| `candidatePattern` | `0` |
| `requireNoUnitAtSpawnCell` | `true` |
| `requireNoSolidAtSpawnCell` | `true` |
| `summonedArchetype` | GUID `8da265900dc94540a0e150fa08ff4c7f` |
| `overrideHp` | `true` |
| `hpOverride` | `1` |
| `windupSeconds` | `1.7` |
| `recoverySeconds` | `0.7` |
| `suppressMovementDuringWindup` | `true` |
| `suppressMovementDuringRecover` | `true` |

Compiled parity remains pinned by existing asset contract tests: initial delay `600`, cooldown `600`, windup `102`, recovery `42`, spawn count `1`, max alive `2`, orthogonal adjacent candidate pattern, unit/solid occupancy checks, archetype `PassiveContactMinion`, HP override `1`, and windup/recover suppression enabled.

## Slice Timeline

- Asset migration moved production ArchetypeSummoner Summon authoring from Utility to `EnemySummonBehaviorModuleAsset` while preserving tuning, spawn materialization, replay text, and source metadata.
- Legacy capability cleanup removed the empty production `EnemyCapability_ArchetypeSummoner` asset/reference, leaving only common PassiveContact plus the Summon behavior module.
- Utility executable retirement removed Utility Summon authoring, compile success, runtime progression, movement suppression, max-alive gating, trigger emission, and Utility-to-`EntitySpawnRequest` conversion.
- Internal type decoupling retired active `SummonMinionAuthoring`, `SummonMinionRuntime`, and `EnemyUtilitySummonPolicy` source/test ownership in favor of `EnemySummonAuthoring`, `EnemySummonCompiledConfig`, and `EnemySummonChildLimitPolicy`.
- Presentation decoupling introduced `TickEnemySummonPresentationSignal` and `EnemySummonPresentationPhase` for view lifecycle while retaining `SummonWindupWarnings` for warning/audio/VFX compatibility.
- C1a renamed active VFX code vocabulary from `UtilityWindup` / `UtilitySummonSpawn` enum members to `SummonWindupWarning = 10` / `SummonedEnemySpawn = 20`, preserving numeric serialized bindings.
- C1b migrated the production JPeter prefab to `EnemySummonScalePulsePresentationDriver`, preserved script GUID `d5bc7f8cc6194d2882c9cdb56e96d289`, removed raw `utilityKind: 3`, removed the legacy numeric adapter, and preserved DrSaturn Gravity raw `utilityKind: 2`.
- C2 is closed as a NO-OP for external vocabulary. `Effect`, `EffectIndex`, `SourceEffectIndex`, `SummonCommitted`, and `SummonSkipped` remain compatibility contracts.

Older readiness and design documents remain historical provenance for earlier states. This final closeout is the current-state umbrella for Summon ownership.

## Runtime Ownership

Behavior Summon owns timing, state, trigger emission, source invalidation cancellation, movement suppression windows, and max-alive decisions for production Summon.

The spawn seam remains unchanged:

- `EntitySpawnRequest` is id-free and captures source metadata at request creation.
- `EntitySpawnMaterializer` owns placement, failed-placement skip, ID allocation after successful placement, child entity construction, `SummonedEntityState`, and `EnemyDefinitionBindingState`.
- authoritative spawn writes go through `FinalizationBatch.SpawnEntity`.
- failed placement does not consume an entity ID.
- request order is preserved from sorted Behavior Summon trigger intents.
- no direct `WorldState` spawn mutation was introduced.

Utility still exists as a framework because Gravity uses it. The only active Utility execution kind is `GravityFieldAura = 2`. `RetiredSummonMinion = 0` and `RetiredLockNearbyBoxes = 1` are fail-fast tombstones, not executable gameplay features.

## Presentation Ownership

`EnemySummonSignals` owns Summon view lifecycle:

- `WindupStarted`
- `RecoverStarted`
- `Canceled`

`SummonWindupWarnings` remains the warning/audio/VFX compatibility carrier. It is still consumed by audio and VFX planners for windup warning behavior.

Presentation audit result:

- `SourceInvalid` skip paths can produce `Canceled`.
- blocked/no-candidate and max-alive skip paths do not produce false `Canceled` lifecycle signals.
- duplicate canceled signal dedupe is preserved by `(EntityId, EffectIndex)`.
- mapper Summon flags reset on ticks without a Summon lifecycle signal.
- audio/VFX planners consume their existing carriers without double-consuming lifecycle and warning signals.
- presentation remains presentation-only and does not mutate authoritative simulation.

## Static Residue Results

Utility Summon executable residue scan:

- active executable hits: `0` for `EnemyUtilityEffectKind.SummonMinion`, `case EnemyUtilityEffectKind.SummonMinion`, `ValidateNoDuplicateSummonSources`, `ResolveSummonMinion`, and `EnemyUtilitySummonTrigger`.
- broad unfiltered `Assets` scan can find ignored `Assets/InitTestScene*.unity` generated test-scene residue containing old method strings; those files are git-ignored and not active source, production content, or tracked closure evidence.
- `RetiredSummonMinion` appears only in enum declaration, explicit authoring/runtime rejection, retired guard tests, and governance/history metadata.
- production `campaign-main` has no active Utility `kind: 0` Summon asset.

DTO residue scan:

- active source/test hits: `0` for `SummonMinionAuthoring`, `SummonMinionRuntime`, and `EnemyUtilitySummonPolicy`.
- current source uses `EnemySummonAuthoring`, `EnemySummonCompiledConfig`, and `EnemySummonChildLimitPolicy`.
- remaining old DTO mentions are historical docs or migration provenance.

VFX vocabulary scan:

- active source/test hits: `0` for `EnemyVfxCue.UtilityWindup` and `EnemyVfxCue.UtilitySummonSpawn`.
- `EnemyVfxCue.SummonWindupWarning = 10` and `EnemyVfxCue.SummonedEnemySpawn = 20` are the active enum names.
- production spawn binding remains `cueCode: 20`.
- old strings remain only in historical docs, historical asset filenames, preserved evidence, or migration test names.

C1b prefab/driver scan:

- production `utilityKind: 3` hits: `0`.
- `LegacySummonPresentationKindValue` hits: `0`.
- old driver symbol hits are bounded to `[MovedFrom]` and negative assertions.
- JPeter prefab `m_Script` uses GUID `d5bc7f8cc6194d2882c9cdb56e96d289` and has no `utilityKind` field.
- Summon pulse tuning is preserved.
- no missing script/reference was observed in the audited YAML.

Gravity preservation scan:

- DrSaturn prefab retains `utilityKind: 2`.
- Gravity driver/component and Utility runtime remain separate from Summon presentation.
- Summon driver ignores Gravity and unknown Utility signals.
- C1b raw `2` residue matches the expected allowlist.

## C2 Compatibility Decision

C2 external vocabulary migration is a NO-OP.

Retained surfaces:

- `Effect=0`
- `EffectIndex`
- `SourceEffectIndex=0`
- `SummonCommitted`
- `SummonSkipped`
- `Final.EnemySummonBehaviors`
- `Final.SummonedEntities`
- `Final.EnemyDefinitionBindings`

Decision basis:

- Behavior Summon `Effect=0` is a fixed compatibility source slot, not a live Utility effect-list lookup.
- `SourceEffectIndex` is a shared source-correlation contract used by Summon child metadata, request ordering/grouping, max-alive grouping, Box interaction locks, Gravity aura fields, trace, and hash paths.
- `Effect=` event text is hash-visible through `EventLog`.
- `TickResultBuilder` still parses exact `SummonSkipped|Reason=SourceInvalid|...|Effect=...` text to create the bounded SourceInvalid `Canceled` fallback.
- no repo-local schema owner/versioning path was found that would make an unversioned hard rename safe.

No internal alias is added in this final closure. A future internal-only alias may be considered only if external text/hash/parser output remains byte-for-byte unchanged and receives separate approval.

## Evidence

Focused C1b evidence:

| Lane | Result |
| --- | --- |
| `./run_tests.sh core` | EditMode `189/189`, PlayMode `33/33` |
| `./run_tests.sh full --filter EnemyViewPresentationMapperTests` | EditMode `24/24`, PlayMode `0/0` |
| `./run_tests.sh --integration-simulation --filter BehaviorSummon` | EditMode `31/31` |
| `./run_tests.sh --integration-simulation --filter MigratedSummon` | EditMode `24/24` |
| `./run_tests.sh --integration-replay --filter MigratedSummon` | EditMode `1/1` |
| `./run_tests.sh --integration-simulation --filter GravityFieldAura` | EditMode `18/18` |
| `./run_tests.sh full --filter GameplayVfx` | EditMode `780/780`, PlayMode `5/5` |
| `./run_tests.sh full --filter EnemyAudio` | EditMode `101/101`, PlayMode `0/0` |
| `./run_tests.sh full --filter StageRuntimeBuilderTests` | EditMode `69/69`, PlayMode `0/0` |
| `./run_tests.sh full --filter EntitySpawnMaterializer` | EditMode `1/1`, PlayMode `0/0` |

Manual evidence:

- path: `TestResults/Preserved/post-summon-presentation-prefab-c1b-a64384f0-20260620-195950/operator-attested-manual-followup.md`
- timestamp: `2026-06-20 21:35:07 KST (+0900)`
- target: production Summoner / JPeter view
- evidence type: operator-attested manual follow-up
- operator statement: production manual play confirmed normal operation
- video capture: not supplied
- tick log capture: not supplied
- observed verdict: PASS

Fresh full identity comparison:

- path: `TestResults/Preserved/post-summon-presentation-prefab-c1b-a64384f0-20260620-195950/`
- command: `./run_tests.sh full`
- exit code: `1`
- EditMode: `5742 total / 36 failed`
- PlayMode artifact present with `0 total / 0 failed`
- previous C1a failed identities: `36`
- current failed identities: `36`
- matched: `36`
- new: `0`
- removed: `0`
- message drift: `0`
- new Summon/presentation/VFX/Gravity/audio touched-cluster failures: `0`

Project-wide state:

- full lane remains red.
- the 36 failures are unchanged pre-existing identities.
- Summon local acceptance is separated from project-wide release state.
- no project-wide green, full regression closed, or full lane green claim is made.

## Documentation Consistency

Current final-state docs are:

- this final closeout
- `Enemy-AI-Summon-Presentation-Prefab-Migration-C1b-Closeout.md`
- `Enemy-AI-Summon-Replay-Export-Vocabulary-C2-Readiness.md`
- the `Docs/Architecture/README.md` index entry pointing to this final closeout

Historical docs may still describe their original before-shape or intermediate slice state, including older "current" wording that was true for that slice. They should not be deleted or renamed retroactively. When they conflict with this final umbrella, this final closeout supersedes them for current Summon ownership.

## Non-Goals

This closure does not:

- rename `Effect`, `EffectIndex`, or `SourceEffectIndex`
- add internal aliases
- add replay schema versioning
- add dual-read or dual-write behavior
- change replay/event/hash labels
- restore Utility Summon executable paths
- remove `RetiredSummonMinion` or `RetiredLockNearbyBoxes`
- change `GravityFieldAura`
- change `EntitySpawnRequest`, `EntitySpawnMaterializer`, or `FinalizationBatch.SpawnEntity`
- change Summon tuning
- change Charge or PassiveContact
- modify runtime source, assets, prefabs, scenes, `.meta`, or test expectations
- fix unrelated full-lane failures

## Future Optional Work

No required Summon follow-up implementation remains.

Optional debt, only with explicit new approval:

- typed `SummonSkipReason` / `SummonMaterializationOutcome`
- removal of exact event-log string parsing
- internal-only alias for `Effect` / `SourceEffectIndex`, with external byte-for-byte compatibility
- historical asset/test filename cleanup

Do not start without explicit approval:

- external `Effect` rename
- external `SourceEffectIndex` rename
- replay schema version migration
- dual-write
- hash text migration
- Utility framework removal

## Rollback Boundaries

This final closure is docs-only. Rollback is limited to:

- `Docs/Architecture/Enemy-AI-Summon-Behavior-Ownership-Final-Closeout.md`
- `Docs/Architecture/Enemy-AI-Summon-Replay-Export-Vocabulary-C2-Readiness.md`
- `Docs/Architecture/Enemy-AI-Summon-Presentation-Prefab-Migration-C1b-Closeout.md`, if final wording was adjusted
- `Docs/Architecture/README.md`

Runtime, asset, prefab, scene, `.meta`, test, Gravity, spawn materialization, Charge, PassiveContact, replay/hash/parser, and Utility tombstone rollback is out of scope for this docs-only closure.
