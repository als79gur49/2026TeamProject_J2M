# Enemy AI Summon VFX Vocabulary Migration C1a Closeout

## Executive Verdict

C1a renames the active Summon VFX code vocabulary while preserving cue numeric values, producer semantics, and serialized production bindings.

Current acceptance status: accepted on revision `a64384f0d654e7a03cfe82cd0c932313ac2a8337`.

Acceptance summary:

- C1a Summon VFX code vocabulary migration accepted.
- Cue numeric compatibility and serialized production bindings remain unchanged.
- C1b prefab/adapter migration and C2 replay vocabulary remain deferred from the C1a scope.

## Operator-Attested Pre-Change Baseline

- Baseline evidence type: operator-attested.
- Evidence path: `TestResults/Preserved/manual-summon-slice-b-baseline-5165033c-20260620T163636KST/operator-attested-followup.md`.
- Confirmation: the operator subsequently confirmed normal production Summon behavior in actual play after the historical Slice B manual-baseline report.
- Detailed video/tick evidence: not supplied.
- C1a start gate: PASS.
- Post-C1a manual parity: PASS by operator attestation supplied for this migration chain.

## Revision and Worktree

- Starting HEAD: `5165033cd461e460ed765cd00f88abd9d835ef9f`.
- Starting short HEAD: `5165033c`.
- Acceptance HEAD: `a64384f0d654e7a03cfe82cd0c932313ac2a8337`.
- Acceptance short HEAD: `a64384f0`.
- Branch: `pr/enemy-ai-retired-melee-runtime-removal`.
- Starting worktree tracked diff: none.
- Starting diff hash: `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`.
- Starting asset/prefab/scene/meta diff: none.
- Acceptance artifact: `TestResults/Preserved/post-summon-vfx-vocabulary-c1a-a64384f0-20260620-192254/`.

## Enum Before and After

Before:

- `EnemyVfxCue.UtilityWindup = 10`.
- `EnemyVfxCue.UtilitySummonSpawn = 20`.

After:

- `EnemyVfxCue.SummonWindupWarning = 10`.
- `EnemyVfxCue.SummonedEnemySpawn = 20`.

No duplicate old/new aliases are introduced.

## Numeric Compatibility

- Cue `10` remains the Summon windup warning VFX cue.
- Cue `20` remains the summoned child enemy spawn VFX cue.
- `Enum.GetName(typeof(EnemyVfxCue), 10)` is expected to return `SummonWindupWarning`.
- `Enum.GetName(typeof(EnemyVfxCue), 20)` is expected to return `SummonedEnemySpawn`.
- Neighboring cue values remain pinned by focused tests.

## Runtime Producer and Consumer Changes

- `GameplayVfxPlanning` now emits `SummonWindupWarning` for existing `SummonWindupWarnings`.
- `GameplayVfxPlanning` now emits `SummonedEnemySpawn` for actual spawned entities with `TickVisibilityChangeKind.Spawn` and `SummonedEnemyPresentationBinding`.
- Persistent windup key shape, source entity anchor, source-cell fallback, effect index, activation sequence, ordering, and same-tick exit suppression are unchanged.
- Spawn VFX remains one-shot, anchored to the actual spawned cell/topology, and seeded from tick, spawned entity id, cue numeric value, cell, and topology.
- `GameplayVfxProductionRuntime` canonical migrated cue allowlist is updated to the new names only.

## Serialized Binding Verification

- Production binding path remains `Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/EnemyUtilitySummonSpawn_Binding.asset`.
- Production prefab path remains `Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/EnemyUtilitySummonSpawnVfx.prefab`.
- C1a does not rename or reserialize these assets.
- Serialized binding remains `family: 3`, `cueCode: 20`.
- Host default cue map must continue resolving cue `Enemy:20` to the existing binding.
- No default windup binding is added; absent default windup VFX remains absent.

## Old Vocabulary Residue

Allowed C1a residue:

- Historical Slice A/B docs and preserved evidence.
- Asset and prefab filenames `EnemyUtilitySummonSpawn_Binding.asset` and `EnemyUtilitySummonSpawnVfx.prefab`.
- Legacy host/presenter/type names scheduled for C1b or later cleanup, including `GameplayUtilityWindupVfxPresenter` and raw `utilityKind: 3` adapter vocabulary.

Not allowed:

- Active `EnemyVfxCue.UtilityWindup` enum member or source/test reference.
- Active `EnemyVfxCue.UtilitySummonSpawn` enum member or source/test reference.
- Current architecture documentation describing the old cue names as canonical.

## Unchanged Scope

- Production assets/prefabs/scenes/meta: unchanged by design.
- raw `utilityKind: 3`: unchanged.
- `EnemyUtilityScalePulsePresentationDriver`: unchanged.
- `EnemyAudioCue.Windup` and `EnemyAudioCue.Active`: unchanged.
- Effect and SourceEffectIndex replay/export vocabulary: unchanged.
- `EntitySpawnRequest`, `EntitySpawnMaterializer`, and `FinalizationBatch.SpawnEntity`: unchanged.
- Summon timing/config/phase lifecycle: unchanged.
- GravityFieldAura, Charge, and PassiveContact: unchanged.

## Focused Validation

Focused validation was run sequentially from this worktree; all listed lanes exited `0`.

| Command | Result | Totals | Artifact path |
| --- | --- | --- | --- |
| `git diff --check` | Passed | n/a | n/a |
| `./run_tests.sh core` | Passed | EditMode 189/189, PlayMode 33/33 | `TestResults/wsl-unity-core-*.xml`, `TestResults/wsl-unity-core-*.log` |
| `./run_tests.sh --integration-simulation --filter BehaviorSummon` | Passed | EditMode 31/31 | `TestResults/wsl-unity-integration-simulation-editmode.*` |
| `./run_tests.sh --integration-simulation --filter MigratedSummon` | Passed | EditMode 24/24 | `TestResults/wsl-unity-integration-simulation-editmode.*` |
| `./run_tests.sh --integration-replay --filter MigratedSummon` | Passed | EditMode 1/1 | `TestResults/wsl-unity-integration-replay-editmode.*` |
| `./run_tests.sh full --filter GameplayVfx` | Passed | EditMode 780/780, PlayMode 5/5 | `TestResults/wsl-unity-full-*.xml`, `TestResults/wsl-unity-full-*.log` |
| `./run_tests.sh full --filter EnemyViewPresentationMapperTests` | Passed | EditMode 20/20, PlayMode 0/0 | `TestResults/wsl-unity-full-*.xml`, `TestResults/wsl-unity-full-*.log` |
| `./run_tests.sh full --filter EnemyAudio` | Passed | EditMode 101/101, PlayMode 0/0 | `TestResults/wsl-unity-full-*.xml`, `TestResults/wsl-unity-full-*.log` |
| `./run_tests.sh full --filter EntitySpawnMaterializer` | Passed | EditMode 1/1, PlayMode 0/0 | `TestResults/wsl-unity-full-*.xml`, `TestResults/wsl-unity-full-*.log` |
| `./run_tests.sh --integration-simulation --filter GravityFieldAura` | Passed | EditMode 18/18 | `TestResults/wsl-unity-integration-simulation-editmode.*` |
| `./run_tests.sh full --filter StageRuntimeBuilderTests` | Passed | EditMode 69/69, PlayMode 0/0 | `TestResults/wsl-unity-full-*.xml`, `TestResults/wsl-unity-full-*.log` |
| `./run_tests.sh full --filter CampaignStageAssets_WithSummonArchetypes_HaveRuntimeAndPresentationArchetypeCatalogs` | Passed | EditMode 1/1, PlayMode 0/0 | `TestResults/wsl-unity-full-*.xml`, `TestResults/wsl-unity-full-*.log` |
| `./run_tests.sh full --filter GameplayVfxUtilityWindupMigrationTests` | Passed | EditMode 20/20, PlayMode 0/0 | `TestResults/wsl-unity-full-editmode.*`, `TestResults/wsl-unity-full-playmode.*` |

The `TestResults/wsl-*` files are the standard lane outputs and are overwritten by later `run_tests.sh` invocations unless separately preserved.

## Post-Change Operator Manual Parity

PASS by operator attestation for normal production Summon behavior after C1a, with production spawn VFX cue `20`, audio baseline, and Gravity preservation unchanged.

## Fresh Full

Fresh unfiltered full was run on revision `a64384f0d654e7a03cfe82cd0c932313ac2a8337` and preserved at:

- `TestResults/Preserved/post-summon-vfx-vocabulary-c1a-a64384f0-20260620-192254/full.log`
- `TestResults/Preserved/post-summon-vfx-vocabulary-c1a-a64384f0-20260620-192254/failure-inventory-current.tsv`
- `TestResults/Preserved/post-summon-vfx-vocabulary-c1a-a64384f0-20260620-192254/failure-identity-comparison.md`
- `TestResults/Preserved/post-summon-vfx-vocabulary-c1a-a64384f0-20260620-192254/release-gate-report.md`

Result: full lane exited `1` with the existing broad baseline red status. Compared with the latest Slice B acceptance full artifact, current failure inventory matched the previous 36 identities exactly: new `0`, removed `0`, message drift `0`.

C1a acceptance gates:

- new Summon failure identity: `0`
- new VFX/presentation failure identity: `0`
- new Gravity failure identity: `0`
- new audio failure identity: `0`
- asset/prefab/scene/meta unexpected diff before C1b: `0`

## Rollback

C1a rollback restores:

- `EnemyVfxCue.UtilityWindup = 10`.
- `EnemyVfxCue.UtilitySummonSpawn = 20`.
- VFX runtime/test references to the old symbol names.
- C1a closeout and README link changes.

Rollback should not touch production assets, prefabs, scenes, meta files, Slice A/B Summon retirement/decoupling work, raw `utilityKind: 3`, Effect/SourceEffectIndex, spawn materialization, Gravity, Charge, or PassiveContact.

## Deferred Scope

- C1b: raw `utilityKind: 3` prefab migration, driver/type neutralization, and legacy numeric adapter removal.
- C2: Effect vocabulary, SourceEffectIndex vocabulary, replay/export/hash/schema migration.
