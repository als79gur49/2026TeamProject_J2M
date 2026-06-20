# Enemy AI Summon Replay/Export Vocabulary C2 Readiness

## Executive Verdict

C1b is accepted locally. This C2 pass is read-only and does not implement vocabulary, schema, parser, hash, replay, export, spawn, prefab, Gravity, Charge, or PassiveContact changes.

Final C2 decision: `NO-OP` for external vocabulary. No internal alias is added in this final closure.

```text
C2 external vocabulary migration is closed as a NO-OP.

Effect and SourceEffectIndex are retained as shared replay,
determinism, and source-correlation compatibility contracts.
No executable Utility Summon ownership remains behind those names.
```

Confidence: high for repo-local code and tests, medium for external consumers outside this repository.

## Revision And Worktree

Start snapshot:

- HEAD: `2696e4c03296e364390974abeba7f8cc44d2f870`
- short HEAD: `2696e4c0`
- branch: `pr/enemy-ai-retired-melee-runtime-removal`
- tracked diff at audit start: none
- tracked diff hash at audit start: `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`
- `git diff --check`: PASS
- current-worktree Unity lock: none observed

One Unity batchmode process was running for a different worktree, `2026teamproject_j2m-vfx-sfx`; it is not evidence of this worktree being locked.

## C1b Closure State

C1b accepted locally:

- driver rename complete: `EnemyUtilityScalePulsePresentationDriver` to `EnemySummonScalePulsePresentationDriver`
- script GUID preserved: `d5bc7f8cc6194d2882c9cdb56e96d289`
- JPeter raw `utilityKind: 3` removed
- legacy raw-3 adapter removed
- Summon tuning preserved
- DrSaturn Gravity raw `utilityKind: 2` preserved
- focused tests green as recorded in the C1b closeout
- fresh full identity comparison: previous 36, current 36, matched 36, new 0, removed 0, message drift 0
- operator-attested manual PASS recorded at `TestResults/Preserved/post-summon-presentation-prefab-c1b-a64384f0-20260620-195950/operator-attested-manual-followup.md`
- unexpected post-play tracked content drift: none observed

Project-wide full remains red with the unchanged pre-existing 36 failure identities.

## Inventory Summary

| Identifier | Owner | Serialized or external | Summon-specific | Numeric domain | Final action | Risk | Confidence |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `Effect=` in `SummonCommitted` | `EntitySpawnMaterializer` event log | external text, hash-visible through `EventLog` | Summon event, generic label | Behavior Summon currently `0` | keep | high if renamed | high |
| `Effect=` in `SummonSkipped` | `EnemyUtilityResolver` and `EntitySpawnMaterializer` event log | external text, hash-visible through `EventLog` | Summon event, generic label | Behavior Summon currently `0` | keep | high if renamed | high |
| `SourceEffectIndex` in `EntitySpawnRequestSource` | spawn request source snapshot | internal, indirectly external through output | currently Summon request only | Behavior Summon `0` | keep | medium/high if semantic changed | high |
| `SourceEffectIndex` in `SummonedEntityState` | authoritative child metadata | internal state, trace/hash-visible as `Effect=` | Summon child metadata | tests include `0` and fixture `2` | keep | high if renamed externally or regrouped | high |
| `EffectIndex` in Utility trigger intent | Utility runtime | internal, trace-visible | not Summon-only | Utility effect list index | keep | high for Gravity | high |
| `SourceEffectIndex` in `BoxInteractionLockState` | authoritative Box lock state | trace/hash-visible as `Effect=` | not Summon | tests include `0` and `1` | keep | high | high |
| `SourceEffectIndex` in `EnemyGravityFieldAuraFieldState` | authoritative Gravity aura field | trace/hash-visible as `Effect=` | not Summon | Utility effect index, commonly `0` | keep | high | high |
| `EffectIndex` in presentation signals | `TickPresentationData`, mapper, VFX/audio host | presentation-only but externally asserted in tests | mixed Utility/Summon | Utility or compatibility index | keep | medium | high |
| `SummonCommitted` | materialization event | external text, hash-visible | Summon | n/a | keep | high | high |
| `SummonSkipped` | resolver/materializer event | external text, parser-visible, hash-visible | Summon | n/a | keep | high | high |

## Effect Semantic Audit

Behavior Summon uses a fixed compatibility value:

- `EnemyLogic.cs:32` defines `SummonBehaviorCompatibilitySourceEffectIndex = 0`.
- `EnemyLogic.cs:1388`, `1421`, `1429`, `1468`, and `1526` write Behavior Summon internal update strings with `Effect=0`.
- `EnemyLogic.cs:1540-1548` emits `EnemySummonBehaviorTriggerIntent` with that same value.
- `TickPipeline.PhaseResults.cs:812-824` copies the trigger value into `EntitySpawnRequestSource`.
- `EntitySpawnMaterializer.cs:149-165` writes `SummonCommitted` and `SummonSkipped` event-log text as `Effect={request.Source.SourceEffectIndex}`.

Therefore Behavior Summon `Effect=0` is not an active Utility effects collection lookup. It is a compatibility source slot carried into request/source metadata, child metadata, event text, trace text, and hash inputs.

The same label still has active non-Summon meanings:

- Utility runtime trace uses real Utility effect indexes in `TickTraceFormatter.cs:331-335`.
- Gravity Aura authoring/runtime uses effect indexes in `EnemyLogic.cs:1723-1739` and trigger emission at `1751-1758`.
- Box lock and Gravity field state expose `SourceEffectIndex` in `WorldSnapshot.cs:29-78` and `94-122`.

Answer to the required questions:

- actual Utility effects collection index: no for Behavior Summon, yes for remaining Utility/Gravity paths.
- fixed compatibility code: yes for Behavior Summon.
- Behavior Summon slot: effectively yes, currently constant `0`.
- activation correlation key: yes where combined with activation sequence, presentation, Gravity field id, and trace/hash.
- always `0` for current Behavior Summon production: yes in repo-local code.
- multiple Summon behaviors per source: not observed in current production path; current constant makes one compatibility source slot.
- sorting/determinism: yes, through `EnemySummonBehaviorTriggerIntentComparer`, `SourceEffectKey`, `SummonedEntityState`, `BoxInteractionLockState`, Gravity field ids, canonical hash dump, and `EventLog`.
- event log only: no.
- exact parser dependency: yes for `SummonSkipped|Reason=SourceInvalid` and `Effect=` in `TickResultBuilder`.

## SourceEffectIndex Semantic Audit

`SourceEffectIndex` is not Summon-only.

Owners and uses:

- spawn request source snapshot: `EntitySpawnRequest.cs:11-39`
- child metadata: `SummonedEntityState` in `EnemyAiRuntimeTypes.cs:1164-1175`
- max-alive grouping: `EnemySummonChildLimitPolicy` compares `SourceEffectIndex` at `EnemySummonBehaviorModuleAsset.cs:252-276`
- request ordering/grouping: `EnemySummonBehaviorTriggerIntentComparer` sorts by source, source effect, then tick at `EnemyAiRuntimeTypes.cs:1356-1375`; `SourceEffectKey` groups planned children at `TickPipeline.PhaseResults.cs:406-435` and `794-838`
- Box lock state: `WorldSnapshot.cs:29-78`
- Gravity field state and field id: `WorldSnapshot.cs:94-171`
- Box lock merge ordering: `GravityFieldRuntimeResolver.cs:506-535`
- determinism hash numeric payload: `DeterminismHashBuilder.cs:566-637`
- trace/export text: `TickTraceFormatter.cs:307-367` and `620-716`

Observed value range:

- Behavior Summon production path uses `0`.
- tests and fixtures construct nonzero values for cross-subsystem state: `ProjectedWorldFastImportCoreTests.cs:244-263` uses Box/Gravity `1` and Summoned child `2`.
- Utility/Gravity uses effect list indexes, including `0` and production Gravity raw kind `2` at the authoring layer, but `SourceEffectIndex` itself is an index/correlation value, not the raw Utility kind.

Renaming it narrowly to `SourceSummonSlot` would be inaccurate because BoxInteractionLock and EnemyGravityFieldAura state use the same field.

## Spawn Seam Audit

C2 must not change the spawn seam:

- `EntitySpawnRequest` is id-free and captures source metadata at creation: `EntitySpawnRequest.cs:42-70`.
- source metadata is copied into `EntitySpawnRequestSource` before materialization: `TickPipeline.PhaseResults.cs:812-824`.
- materializer does not reread live source fields for team/facing/origin/effect metadata.
- placement is resolved before ID allocation: `EntitySpawnMaterializer.cs:66-88`.
- failed placement returns skipped without consuming an ID: `EntitySpawnMaterializer.cs:66-78`.
- successful placement allocates ID and writes through `FinalizationBatch.SpawnEntity`: `EntitySpawnMaterializer.cs:83-112`.
- request order is preserved from sorted trigger intents: `TickPipeline.cs:626-651`.
- child metadata and enemy definition binding are created by materializer: `EntitySpawnMaterializer.cs:83-109`.

Any C2 field change that alters comparer keys, `SourceEffectKey`, max-alive grouping, metadata payload, or hash payload is not a vocabulary-only change.

## Replay And Export Format Audit

Repo-local formats found:

- textual tick trace: `TickTraceFormatter`
- determinism hash canonical dump: `DeterminismHashBuilder`
- in-memory replay harness/tests: `TickReplayDeterminismTests`, `EnemyProfileContractReplayTests`
- event log text: `TickResult.EventLog`, emitted by resolver/materializer and included in hash input
- test-local dumps: `BuildUtilityDump`, `BuildSummonedDump`, `EventLogDump`
- JSON/schema systems exist for stage save/direct play/input settings, but no repo-local production Summon replay JSON schema owner was found
- no repo-local binary replay format for Summon was found
- no repo-local stored old Summon replay sample was found in this audit

External consumers outside the repo cannot be ruled out. This is a repo-local inventory only.

## Parser Dependency Audit

Current code parses exact event/update strings:

- `TickResultBuilder.cs:2474-2497` parses `EnemySummonBehaviorWindupCanceled|...|Effect=...`.
- `TickResultBuilder.cs:2517-2552` parses `SummonSkipped|...|Reason=SourceInvalid|...|Effect=...` to create a canceled Summon presentation signal.
- `TryReadIntUpdateField` at `TickResultBuilder.cs:2500-2515` depends on exact `|Field=` delimiters.

This makes unversioned hard rename of `Effect=` a NO-GO until a typed `SummonSkipReason` or equivalent carrier replaces the current string fallback.

## Determinism And Hash Impact

Hash impact is direct:

- `DeterminismHashBuilder.cs:127-128` includes `EventLog` strings in canonical hash input.
- `DeterminismHashBuilder.cs:566-637` includes numeric `SourceEffectIndex` for Summoned entities, Box locks, and Gravity aura fields.
- `TickTraceFormatter` writes `Effect=` labels for visible trace output.

Field names are not used in the compact numeric canonical lines for `SummonedEntities`, Box locks, or Gravity fields, but `EventLog` strings include labels and are hash input. Renaming `Effect=` in event logs changes hashes even when gameplay values are identical.

## Test Inventory

| Test area | Current assertion | Field owner | C2 option impact | Required action | Risk |
| --- | --- | --- | --- | --- | --- |
| `EnemyAiScenarioTests` Behavior Summon | exact `SummonCommitted|...|Effect=0` and `SummonSkipped|...|Effect=0` | event log | Option C breaks without migration | update only in versioned slice | high |
| `EnemyAiScenarioTests` source invalid cancel | `SummonSkipped|Reason=SourceInvalid` feeds presentation cancel | parser/event | Option C breaks current parser | typed carrier first | high |
| `EnemyAiScenarioTests` hash parity | trace contains `E=41|Source=40|Effect=0` | trace/hash | Option C changes text | versioned trace/hash policy | high |
| `TickReplayDeterminismTests` Utility | `Final.EnemyUtilities` and `Effect=0` | Utility trace/hash | Option C cross-subsystem | not Summon-only | high |
| `TickReplayDeterminismTests` Box lock | `Final.BoxInteractionLocks`, `Source=40`, `Effect=0` | Box lock trace/hash | Option C cross-subsystem | versioned policy | high |
| `TickReplayDeterminismTests` Summoned metadata | `Final.SummonedEntities`, `Effect=0` | child metadata trace/hash | Option C changes visible trace | versioned policy | high |
| `EnemyProfileContractReplayTests` migrated Summon | `Source=40|Effect=0`, no Utility dump | replay/export dump | Option C breaks compatibility assertions | preserve or version | high |
| `ProjectedWorldFastImportCoreTests` | nonzero `SourceEffectIndex` preserved for Box/Gravity/Summon | authoritative import | internal rename must preserve equality | alias only | medium |
| GravityFieldAura scenario tests | `Source=40|Effect=0|Kind=GravityFieldAura` | Gravity trace/event | Option C cross-subsystem | keep for C2 | high |
| Gameplay VFX Gravity tests | `EffectIndex` values such as `2` | VFX/presentation key | not C2 Summon schema | no action | medium |

## C2 Decision Matrix

| Option | Semantic accuracy | Scope | External compatibility | Hash impact | Parser impact | Cross-subsystem impact | Rollback | Recommendation |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| A. NO-OP | accurate as compatibility contract | docs only | stable | none | none | none | docs revert | recommended |
| B. internal neutral alias only | can clarify Behavior ownership | bounded if external DTO/text unchanged | stable | none required | none required | medium if alias leaks into shared state | remove alias | acceptable only if narrow |
| C. versioned external migration | accurate if schema-owned | large dedicated slice | requires v1 read/v2 write | expected bounded drift | parser migration required | high | complex | defer |
| D. hard rename without versioning | misleading safety claim | deceptively small | breaks consumers | changes hashes | breaks parser | high | risky | reject |

## GO / NO-GO

External migration is NO-GO now because:

- `SourceEffectIndex` is a generic cross-subsystem source correlation contract, not Summon-only.
- exact event-log parsing exists for `SummonSkipped|Reason=SourceInvalid` and `Effect=`.
- schema owner/versioning for Summon replay/export was not found in repo-local code.
- old replay compatibility policy and external parser inventory are incomplete.
- hash-visible event strings would drift on label rename.

Internal-only is GO only if:

- external labels stay byte-for-byte unchanged
- `Effect=0` and `SourceEffectIndex=0` remain output compatibility values
- no duplicate source of truth is introduced
- alias scope is limited to local Behavior Summon variables or a read-only adapter property
- tests assert trace/hash/event text equality

NO-OP is the final closure decision because the existing fields are generic enough at the shared contract layer and the remaining debt can be documented without runtime risk.

## Future Implementation Allowlist

No future implementation is required for Summon closure.

Option A NO-OP:

- this readiness document
- architecture README link
- final umbrella closeout explaining compatibility vocabulary

Option B internal-only:

- requires separate approval
- local Behavior Summon variable/parameter names
- read-only alias property or mapper that writes existing `Effect` / `SourceEffectIndex`
- focused tests proving byte-for-byte trace/hash/event equality
- docs

Option C versioned migration:

- requires separate approval
- replay/export DTO and explicit schema version
- v1 reader and v2 writer
- parser/tooling migration
- `TickTraceFormatter`
- `DeterminismHashBuilder`
- event-log policy
- golden/sample replay data
- compatibility tests and rollback docs

Always keep:

- `EntitySpawnRequest` semantics
- `EntitySpawnMaterializer` ownership
- `FinalizationBatch.SpawnEntity`
- Behavior Summon timing/state
- C1a VFX cue numeric values `10` and `20`
- C1b Summon driver/prefab
- Gravity
- Charge
- PassiveContact

## Validation Strategy

If Option B is implemented later:

- prove external trace text byte-for-byte identical
- prove `Effect=0` and `SourceEffectIndex=0` remain unchanged
- prove determinism hash unchanged
- prove event parser unchanged
- run BehaviorSummon, MigratedSummon simulation/replay, EntitySpawnMaterializer, GravityFieldAura, and core

If Option C is implemented later:

- add explicit schema version
- prove v1 stored sample read compatibility
- prove v2 write semantics
- define hash version or bounded-diff policy
- migrate parser/tooling
- prove child metadata/order parity and failed placement ID non-consumption
- run focused replay/hash matrix and fresh full identity comparison

## Rollback

This audit is read-only except documentation. Runtime rollback is not applicable.

Final closure rollback is docs-only: revert this C2 readiness document, the final umbrella closeout, and the README link.

Future Option B rollback:

- remove aliases/mapper
- restore old internal references
- external schema remains unchanged

Future Option C rollback:

- restore old writer/parser/hash/trace labels
- restore model/property names
- restore tests/golden data
- restore docs

Do not roll back C1a or C1b as part of C2.

## Open Questions

- Are there consumers outside this repository that parse `Effect=` or `SourceEffectIndex`?
- Is there a planned production replay/export schema owner for Summon beyond current text trace/hash/event logs?
- Would an internal alias actually reduce maintenance cost enough to justify dual vocabulary?

These questions do not block local Summon closure because the final repository-local decision is no external migration and no new internal alias.
