# Enemy AI Summon Presentation Prefab Migration C1b Closeout

## Executive Verdict

C1b migrates the production Summon scale pulse prefab off the legacy raw `utilityKind: 3` adapter.

Implementation status: implemented and focused-validated.

Selected migration:

- `EnemyUtilityScalePulsePresentationDriver` -> `EnemySummonScalePulsePresentationDriver`
- script GUID preserved: `d5bc7f8cc6194d2882c9cdb56e96d289`
- raw `utilityKind: 3` removed from `EnemyView_JPeter.prefab`
- Gravity raw `utilityKind: 2` preserved on `EnemyView_DrSaturn.prefab`

## C1a Acceptance Dependency

C1a is accepted on revision `a64384f0d654e7a03cfe82cd0c932313ac2a8337`.

Fresh full artifact:

- `TestResults/Preserved/post-summon-vfx-vocabulary-c1a-a64384f0-20260620-192254/`

C1a full remained broad-baseline red, but previous/current identity comparison found new failures `0` across Summon, VFX/presentation, Gravity, and audio touched clusters.

## Runtime Changes

`EnemySummonScalePulsePresentationDriver` is Summon-specific:

- starts windup only from `StartedSummonWindupThisTick`
- starts recover only from `StartedSummonRecoverThisTick`
- clears only from `SummonCanceledThisTick` or death/disable/destroy cleanup
- ignores Utility presentation kinds, including Gravity and unknown values
- keeps semantic pause behavior and all timing/scale tuning behavior

Removed active compatibility residue:

- `LegacySummonPresentationKindValue`
- serialized `utilityKind` field on the scale pulse driver
- raw-3 comparison branch
- legacy adapter-only tests

## Prefab Before and After

Production prefab:

- `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_JPeter.prefab`

Before:

- component script GUID `d5bc7f8cc6194d2882c9cdb56e96d289`
- class `EnemyUtilityScalePulsePresentationDriver`
- `utilityKind: 3`

After:

- component script GUID unchanged
- class `EnemySummonScalePulsePresentationDriver`
- no `utilityKind: 3`
- `windupDurationSeconds: 1.7`
- `windupPeakTimeSeconds: 1.05`
- `recoverDurationSeconds: 0.7`
- `peakScaleMultiplier: 1.1`
- `windupEndScaleMultiplier: 0.75`
- `recoverEndScaleMultiplier: 1`

## Gravity Preservation

Gravity remains owned by `EnemyUtilityCooldownAuraVfxAuthoring`.

Control prefab:

- `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_DrSaturn.prefab`
- script GUID `f18cc0d83f3741f087d10f75a8d2d59c`
- `utilityKind: 2`

C1b does not change Gravity execution, presentation authoring, or serialized raw `2`.

## C1a Regression Protection

C1b keeps:

- `EnemyVfxCue.SummonWindupWarning = 10`
- `EnemyVfxCue.SummonedEnemySpawn = 20`
- production spawn VFX binding `cueCode: 20`
- audio behavior unchanged
- `Effect = 0` and `SourceEffectIndex = 0` vocabulary unchanged
- Summon gameplay timing and spawn materialization unchanged

## Test Updates

Replacement contracts added or updated:

- `EnemySummonScalePulsePresentationDriver_SummonWindupAndRecover_ScalesModelRoot`
- `EnemySummonScalePulsePresentationDriver_SummonCanceled_NormalizesScalePulse`
- `EnemySummonScalePulsePresentationDriver_IgnoresGravityUtilitySignals`
- `EnemySummonScalePulsePresentationDriver_IgnoresUnknownUtilitySignals`
- `EnemySummonScalePulsePresentationDriver_SemanticSuppression_FreezesCurrentScale`
- `EnemySummonScalePulsePresentationDriver_DisableStillNormalizesToBaseScale`
- `EnemySummonScalePulse_SummonCanceled_DoesNotRemainInWindupHold`
- `EnemyView_JPeter_UsesTypedSummonScalePulseBinding`
- `EnemyView_JPeter_HasNoLegacyUtilityKind3`
- `EnemyView_JPeter_PreservesSummonScalePulseTuning`
- `EnemyView_DrSaturn_PreservesGravityUtilityKind2`
- coordinator Summon scale pulse semantic pause and cancel tests

## Static Scan Results

Observed after implementation:

- `rg -n "utilityKind: 3" Assets --glob '*.prefab' --glob '*.asset' --glob '*.unity' --glob '!InitTestScene*.unity'`: no production hits.
- `rg -n "LegacySummonPresentationKindValue" Assets Packages`: no hits.
- `rg -n "EnemyUtilityScalePulsePresentationDriver" Assets Packages --glob '!InitTestScene*.unity'`: bounded hits only in `[MovedFrom]` and a negative prefab contract assertion.
- `rg -n "utilityKind: 2" Assets --glob '*.prefab' --glob '*.asset' --glob '*.unity' --glob '!InitTestScene*.unity'`: one DrSaturn Gravity hit remains.

Full scan capture:

- `TestResults/Preserved/post-summon-presentation-prefab-c1b-a64384f0-20260620-195950/release-gate-report.md`

## Focused Validation

| Command | Result | Totals |
| --- | --- | --- |
| `git diff --check` | Passed | n/a |
| `./run_tests.sh core` | Passed | EditMode 189/189, PlayMode 33/33 |
| `./run_tests.sh full --filter EnemyViewPresentationMapperTests` | Passed | EditMode 24/24, PlayMode 0/0 |
| `./run_tests.sh --integration-simulation --filter BehaviorSummon` | Passed | EditMode 31/31 |
| `./run_tests.sh --integration-simulation --filter MigratedSummon` | Passed | EditMode 24/24 |
| `./run_tests.sh --integration-replay --filter MigratedSummon` | Passed | EditMode 1/1 |
| `./run_tests.sh --integration-simulation --filter GravityFieldAura` | Passed | EditMode 18/18 |
| `./run_tests.sh full --filter GameplayVfx` | Passed | EditMode 780/780, PlayMode 5/5 |
| `./run_tests.sh full --filter EnemyAudio` | Passed | EditMode 101/101, PlayMode 0/0 |
| `./run_tests.sh full --filter StageRuntimeBuilderTests` | Passed | EditMode 69/69, PlayMode 0/0 |
| `./run_tests.sh full --filter EntitySpawnMaterializer` | Passed | EditMode 1/1, PlayMode 0/0 |

## Manual Play Plan

Production smoke after focused tests:

- normal JPeter Summon windup scale pulse, spawn VFX cue `20`, audio baseline, recover cleanup
- SourceInvalid cancel without lingering pulse or ghost spawn
- blocked/max-alive without ghost pulse or false cancel
- dual Summoner source isolation
- DrSaturn Gravity presentation unchanged
- no missing script, missing serialized field, prefab override warning, or console exception

## Fresh Full Artifact Plan

Fresh full was run after focused validation.

Artifact:

- `TestResults/Preserved/post-summon-presentation-prefab-c1b-a64384f0-20260620-195950/`

Result:

- full exit code: `1`
- full EditMode: 5742 total, 36 failed
- compared against C1a acceptance full: previous 36, current 36, matched 36, new 0, removed 0, message drift 0
- new Summon failure identity: `0`
- new presentation/VFX failure identity: `0`
- new Gravity failure identity: `0`
- new audio failure identity: `0`

The broad lane remains baseline red. Do not claim full green.

## Rollback

Rollback C1b only:

1. Restore old driver file/class name and `.meta` path, keeping GUID `d5bc7f8cc6194d2882c9cdb56e96d289`.
2. Restore serialized `utilityKind` field and raw-3 adapter.
3. Restore JPeter prefab `m_EditorClassIdentifier` and `utilityKind: 3`.
4. Restore legacy adapter tests and remove replacement contract tests.
5. Reimport and run focused presentation/Summon/Gravity tests.

Do not roll back C1a VFX vocabulary, Slice A/B Summon retirement/decoupling, Effect/SourceEffectIndex vocabulary, EntitySpawnRequest/materializer, Gravity, Charge, or PassiveContact.

## C2 Deferred Boundary

C1b does not modify `Effect`, `EffectIndex`, `SourceEffectIndex`, replay/export/hash/schema names, `EntitySpawnRequestSource`, `SummonedEntityState`, parser versioning, or numeric compatibility value `0`.
