# Enemy AI Summon Option B Implementation Plan

## 1. Decision Summary

- This is an implementation plan only, not implementation.
- First implementation slice is implemented: the compile skeleton adds the real Behavior Summon key, typed runtime slot, module asset, config compile path, and duplicate Utility/Behavior Summon guard.
- Runtime/emitter parity slice is implemented for the test-local Behavior Summon path: mutable behavior state progresses, emits id-free spawn requests with captured source metadata, and materializes through the existing Spawn/EntityCreation seam.
- Full presentation/audio/VFX parity slice is implemented for the test-local Behavior Summon path while preserving current names and cue semantics.
- Initial Option B preserves the Spawn/EntityCreation seam, external replay/export names, and presentation/audio/VFX cue semantics.
- Production asset migration is a separate gated phase after implementation and parity gates.
- Full lane was not run for this plan.

## 2. Prerequisite Gate Status

| Gate | Status | Source Doc |
|---|---|---|
| Spawn seam implementation note | Tracked prerequisite | [Enemy-AI-Summon-Spawn-Seam-Implementation-Note.md](./Enemy-AI-Summon-Spawn-Seam-Implementation-Note.md) |
| Same-tick ordering characterization | Already recorded | [Enemy-AI-Summon-Spawn-Seam-Implementation-Note.md](./Enemy-AI-Summon-Spawn-Seam-Implementation-Note.md) |
| Mutable request payload drift guard | Already recorded | [Enemy-AI-Summon-Spawn-Seam-Implementation-Note.md](./Enemy-AI-Summon-Spawn-Seam-Implementation-Note.md) |
| Duplicate Utility/Behavior Summon guard design | Tracked prerequisite | [Enemy-AI-Summon-Duplicate-Guard-Design.md](./Enemy-AI-Summon-Duplicate-Guard-Design.md) |
| Behavior Summon runtime state shape design | Tracked prerequisite | [Enemy-AI-Summon-Behavior-Runtime-State-Design.md](./Enemy-AI-Summon-Behavior-Runtime-State-Design.md) |
| Summon asset migration plan | Tracked prerequisite | [Enemy-AI-Summon-Asset-Migration-Plan.md](./Enemy-AI-Summon-Asset-Migration-Plan.md) |
| Replay/export compatibility plan | Tracked prerequisite | [Enemy-AI-Summon-Replay-Export-Compatibility-Plan.md](./Enemy-AI-Summon-Replay-Export-Compatibility-Plan.md) |
| Presentation/audio/VFX parity plan | Tracked prerequisite | [Enemy-AI-Summon-Presentation-Audio-VFX-Parity-Plan.md](./Enemy-AI-Summon-Presentation-Audio-VFX-Parity-Plan.md) |

## 3. Implementation Slices

| Slice | Purpose | Allowed Changes | Explicit Non-Goals | Required Tests | Merge Gate | Rollback |
|---|---|---|---|---|---|---|
| 0. Pre-implementation hygiene / docs lock | Lock this plan and cross-links | Docs and architecture index links only | No runtime, test, schema, asset, or production YAML edits | `git diff --check`; Summon docs trailing whitespace scan; forbidden symbol scan | Docs-only diff | Revert docs-only commit |
| 1. Compiler guard infrastructure timing | Define where the actual duplicate guard lands | Guard helper design may be refined; actual guard only when real Behavior Summon key/runtime exists in this or next slice | No placeholder key, fake runtime, or production migration | Existing Utility-only compile remains valid | No future symbols unless implementation slice starts | Revert docs or guard-helper-only commit |
| 2-3. Compile skeleton | Add the real Behavior Summon lane entrypoint without key-only placeholder | Add `EnemyBehaviorModuleKey.Summon`, fixed typed `EnemyBehaviorRuntimeSet` slot, `HasSummon`, `TryGetSummon(out ...)`, `EnemySummonBehaviorModuleAsset`, config validation, compiler switch, and duplicate Utility/Behavior Summon guard | No generic registry; no `logicModuleAssets`; no production assets; no materialization changes | Behavior-only compile, duplicate guard, invalid field validation, Utility/Gravity/Retired guard tests | Touched compiler tests plus no production asset diff | Revert key/slot/compiler/asset-test commit |
| 4. Summon behavior runtime state and emitter | Implemented for test-local Behavior Summon path | Implemented windup/recovery/cooldown/suppression/max-alive/request emission parity; emits id-free spawn requests with captured source metadata | No direct `WorldState` spawn write; no `EntitySpawnRequest` or `EntitySpawnMaterializer` contract change | Behavior Summon delay, cooldown, order, payload, max-alive, source invalidation, topology, movement suppression, hash, and replay-name parity tests | `BehaviorSummon`, Utility Summon replay/simulation filters, and `./run_tests.sh core` passed | Revert runtime emitter/state commit |
| 5. Duplicate guard actual enforcement | Fail fast on Utility Summon plus Behavior Summon | Enforce compiler guard after real Behavior Summon exists; message includes profile, Utility effect index, Behavior module asset/key | Do not classify `GravityFieldAura` or `RetiredLockNearbyBoxes` as Summon | Duplicate fail-fast tests; Gravity/Retired exclusions | Guard tests green before migration | Revert guard commit if no migrated assets depend on it |
| 6. Replay/export compatibility preservation | Preserve external replay/export surface | Preserve `SummonCommitted`, `SummonSkipped`, `SummonedEntities`, `EnemyDefinitionBindings`, `Effect=`, `SourceEffectIndex`, and initial `PreMovement.UtilityTriggers` alias | No unversioned neutral rename; no standalone hash behavior change | Behavior Summon replay names/hash/source metadata tests; no double-count tests | Replay filters green | Revert compatibility adapter/hash commit |
| 7. Presentation/audio/VFX parity | Implemented for test-local Behavior Summon path | Preserves `TickSummonWindupWarningSignal`, `TickSummonedEnemyPresentationBinding`, `TickVisibilityChange(Spawn)`, `EnemyAudioCue.Windup`, `EnemyAudioCue.Active`, `UtilityWindup`, and `UtilitySummonSpawn` | No cue, prefab, audio, VFX, or presentation asset rename | Windup, binding, visibility, audio, VFX, negative, ordering, fallback, and no authoritative mutation parity tests | `BehaviorSummon`, Utility Summon, replay filters, and `./run_tests.sh core` passed | Revert test-local parity test/runtime adapter commit |
| 8. Asset-scoped migration preparation only | Prepare migration safely | Checklist, dry-run tool, allowlist, residue scans | No production asset migration | Dry-run/residue scan tests if tool exists | Allowlist reviewed | Revert tool/checklist commit |
| 9. Asset-scoped production migration | Migrate exact approved profiles | Migrate only allowlisted profile(s); remove Utility Summon from migrated profile; add Behavior Summon | No whole Utility lane migration; no Gravity/Retired migration | Asset contract, residue, replay/hash, presentation/audio/VFX parity tests | Full lane or CI release gate per policy | Revert asset commit from source control |

## 4. Proposed File Impact Map

| Area | Likely Files | Change Type | Slice | Risk |
|---|---|---|---|---|
| Behavior key/runtime set | `EnemyAiRuntimeTypes.cs` | enum, concrete runtime, typed slot | 2, 4 | Medium |
| Behavior authoring base | `EnemyAiAuthoringAssets.cs` | base type reference point, usually unchanged | 3 | Low |
| Behavior asset | `EnemySummonBehaviorModuleAsset.cs` | new authoring type | 3 | Medium |
| Compiler | `EnemyAiProfileCompiler.cs` | Summon switch branch, duplicate guard | 2, 5 | High |
| Enemy logic | `EnemyLogic.cs` | Behavior timing/state/request emission | 4 | High |
| Phase result / trigger path | `TickPipeline.PhaseResults.cs` | should preserve materialization path; adapter only if needed | 4, 6 | High |
| Spawn request | `EntitySpawnRequest.cs` | should remain unchanged if current source snapshot is sufficient | 4 | High if changed |
| Spawn materializer | `EntitySpawnMaterializer.cs` | should remain unchanged | 4 | High if changed |
| Determinism hash | `DeterminismHashBuilder.cs` | replay/hash slice only | 6 | High |
| Result presentation | `TickResultBuilder.cs` | compatibility presentation adapter only | 7 | High |
| Scenario tests | `EnemyAiScenarioTests.cs` | runtime parity coverage | 4, 5 | Medium |
| Replay tests | `TickReplayDeterminismTests.cs` | replay/hash parity coverage | 6, 9 | High |
| Asset contract tests | `EnemyAiProfileAssetContractTests.cs` | compiler/asset/migration guard coverage | 3, 5, 9 | Medium |
| Architecture docs | `Docs/Architecture/*.md` | plan and cross-link updates | 0 | Low |

## 5. Contract Guardrails

- `EntitySpawnRequest` has no allocated entity id.
- Entity id allocation remains in `EntitySpawnMaterializer` after placement succeeds.
- Failed placement does not consume ids.
- `FinalizationBatch.SpawnEntity` remains the authoritative write path.
- Tiles remain canonical `SurfaceCell(face, x, y)`.
- Utility-only Summon content remains valid until explicit migration.
- Behavior-only Summon content becomes valid only after the module implementation slice.
- Utility+Behavior duplicate profiles fail compile after the duplicate guard slice.
- `GravityFieldAura` is not duplicate Summon.
- `RetiredLockNearbyBoxes` remains protected by the retired guard, not the duplicate Summon guard.
- Initial Option B preserves external replay/export names.
- Initial Option B preserves current presentation/audio/VFX names and cue semantics.
- No generic registry unless a separate phase approves it.
- Full lane or CI release gate is required before broad release claims.

## 6. Compiler / Asset Contract Plan

- Slice 2 adds the real `Summon` behavior key and extends the fixed typed slot pattern already used by Charge.
- Keep `EnemyBehaviorRuntimeSet` fixed typed: `Charge` plus future `Summon`, with `TryGetSummon(out ...)`.
- Generic registry is not needed because the current compiler resolves known behavior module keys into typed runtime slots and validates duplicate keys in the switch.
- Slice 3 adds the real Summon behavior module asset and compile-time field validation.
- Slice 5 enforces duplicate Utility/Behavior Summon fail-fast after real Summon behavior exists and before production migration.
- Duplicate guard error text must include the profile name, Utility Summon effect index, and Behavior module asset/key.

## 7. Runtime Emission Plan

- Move only behavior timing, state progression, suppression, max-alive precheck, and request emission ownership to Behavior runtime.
- Keep entity creation, placement, id allocation, spawned metadata, and finalization writes in the Spawn/EntityCreation seam.
- Behavior emitter must emit `EntitySpawnRequest` in Utility parity order.
- Trigger intents may capture emission-time source pose for trace/debug compatibility, but request source metadata must capture `OriginCell`, `SourceFacing`, and `SourceTeamId` from the resolve-time valid source before materialization can consume the immutable request.
- Initial Option B maps Behavior source identity into compatibility `SourceEffectIndex` / `Effect=` vocabulary rather than renaming external output.
- Source death, invalid source, and topology participation must cancel, skip, or suspend with Utility parity.

## 8. Spawn Seam Preservation Plan

- `EntitySpawnRequest` remains id-free.
- `EntitySpawnMaterializer` remains the placement, id allocation, spawned metadata, and `FinalizationBatch.SpawnEntity` owner.
- Received request order remains materialization order.
- Reserved spawn cells and planned child counts remain deterministic for same-tick multi-summoner cases.
- Direct `WorldState` spawn writes are forbidden.
- `EntitySpawnRequest` or `EntitySpawnMaterializer` contract changes require a separate Spawn seam review and are not part of the preferred Option B path.

## 9. Replay / Export Preservation Plan

- Initial Option B uses the existing compatibility policy: external names are preserved.
- Preserve `SummonCommitted`, `SummonSkipped`, `SummonedEntities`, `EnemyDefinitionBindings`, `Effect=`, and `SourceEffectIndex`.
- Preserve `PreMovement.UtilityTriggers` as an initial compatibility alias.
- Do not introduce a neutral replay/export name without an explicit schema/version migration.
- `EnemyUtilities` hash and future Behavior Summon state hash must be paired in one reviewed migration to avoid double-count or missing-count.

## 10. Presentation / Audio / VFX Preservation Plan

- Preserve `TickSummonWindupWarningSignal`.
- Preserve `TickSummonedEnemyPresentationBinding`.
- Preserve `TickVisibilityChange(Spawn)` timing and ordering.
- Preserve `EnemyAudioCue.Windup` and `EnemyAudioCue.Active` semantics.
- Preserve `EnemyVfxCue.UtilityWindup` and `EnemyVfxCue.UtilitySummonSpawn`.
- Presentation, audio, and VFX remain presentation-only and must not mutate authoritative simulation.
- Neutral cue or signal names require a separate asset/schema migration, not initial Option B.

## 11. Asset Migration Boundary

- Production asset migration is forbidden through Slice 8.
- Slice 9 may migrate only exact allowlisted profile(s) after implementation, duplicate guard, replay/export, and presentation/audio/VFX parity gates pass.
- Migration removes Utility `SummonMinion` from migrated profiles and adds Behavior Summon.
- No whole Utility lane migration.
- No `GravityFieldAura` migration.
- No `RetiredLockNearbyBoxes` migration or revival.

## 12. Test Plan

| Test | Slice | Purpose | Required Before Merge? | Required Before Production Migration? |
|---|---|---|---|---|
| `BehaviorSummonOnly_ProfileCompiles_AfterOptionBExists` | 3 | Behavior-only compile validity after real asset exists | Yes | Yes |
| `UtilitySummonAndBehaviorSummon_ProfileCompileFails` | 5 | Duplicate source fail-fast | Yes | Yes |
| `DuplicateGuard_MessageIncludesProfileAndBothSources` | 5 | Actionable compiler error | Yes | Yes |
| `GravityFieldAuraWithBehaviorSummon_DoesNotTriggerSummonDuplicateGuard` | 5 | Exclude Gravity aura | Yes | Yes |
| `RetiredLockNearbyBoxes_StillFailsByRetiredGuard_NotDuplicateGuard` | 5 | Preserve retired guard | Yes | Yes |
| `BehaviorSummon_InitialDelayParity` | 4 | Utility timing parity | Yes | Yes |
| `BehaviorSummon_CooldownWindupRecoveryParity` | 4 | Runtime state parity | Yes | Yes |
| `BehaviorSummon_EmitsSpawnRequestInUtilityParityOrder` | 4 | Request ordering | Yes | Yes |
| `BehaviorSummon_PreservesRequestPayloadSnapshot` | 4 | Payload drift guard | Yes | Yes |
| `BehaviorSummon_MaxAliveParity` | 4 | Child limit parity | Yes | Yes |
| `BehaviorSummon_SourceDeathCancelsOrSkipsAsUtility` | 4 | Source invalidation parity | Yes | Yes |
| `BehaviorSummon_SourceLeavesTopologyCancelsOrSuspendsAsUtility` | 4 | Topology participation parity | Yes | Yes |
| `BehaviorSummon_MovementSuppressionParity` | 4 | Suppression parity | Yes | Yes |
| `BehaviorSummon_ReplayNames_PreserveSummonCommittedAndSkipped` | 6 | Event name compatibility | Yes | Yes |
| `BehaviorSummon_SummonedEntitiesHashParity` | 6 | Summoned metadata hash parity | Yes | Yes |
| `BehaviorSummon_EnemyDefinitionBindingsHashParity` | 6 | Archetype binding hash parity | Yes | Yes |
| `BehaviorSummon_SourceMetadataExportParity` | 6 | Source metadata compatibility | Yes | Yes |
| `BehaviorSummon_UtilityTriggerAliasPreserved_IfPolicyA` | 6 | Initial alias preservation | Yes | Yes |
| `MigratedSummon_NoDoubleCountUtilityAndBehaviorState` | 6, 9 | Hash migration safety | No | Yes |
| `BehaviorSummon_WindupWarningSignalParity` | 7 | Windup presentation parity | Yes | Yes |
| `BehaviorSummon_SummonedEnemyPresentationBindingParity` | 7 | Binding parity | Yes | Yes |
| `BehaviorSummon_SpawnVisibilityChangeParity` | 7 | Spawn visibility parity | Yes | Yes |
| `BehaviorSummon_AudioWindupCueParity` | 7 | Windup audio parity | Yes | Yes |
| `BehaviorSummon_AudioActiveSummonCueParity` | 7 | Active summon audio parity | Yes | Yes |
| `BehaviorSummon_UtilityWindupVfxParity` | 7 | Persistent VFX parity | Yes | Yes |
| `BehaviorSummon_UtilitySummonSpawnVfxParity` | 7 | Spawn VFX parity | Yes | Yes |
| `BehaviorSummon_PresentationDoesNotMutateAuthoritativeState` | 7 | Presentation boundary | Yes | Yes |
| `UtilitySummonOnly_ProfileCompilesBeforeMigration` | 9 | Utility-only remains valid | No | Yes |
| `MigratedSummon_ProfileHasBehaviorModuleOnly` | 9 | Asset-scoped migration target shape | No | Yes |
| `MigratedSummon_ProfileHasNoUtilitySummonResidue` | 9 | Residue prevention | No | Yes |
| `MigratedSummon_FieldMappingMatchesUtilityBaseline` | 9 | Field mapping parity | No | Yes |
| `MigratedSummon_ReplayParityAgainstUtilityBaseline` | 9 | Replay parity | No | Yes |
| `MigratedSummon_DeterminismHashParity` | 9 | Hash parity | No | Yes |
| `MigratedSummon_PresentationWindupParity` | 9 | Presentation parity | No | Yes |
| `MigratedSummon_AudioVfxParity` | 9 | Audio/VFX parity | No | Yes |
| `GravityFieldAura_Unchanged` | 9 | Non-target asset guard | No | Yes |
| `RetiredLockNearbyBoxes_GuardUnchanged` | 9 | Retired guard preservation | No | Yes |

## 13. Validation / CI Gate Plan

Docs-only slice:

- `git diff --check`
- `rg -n "EnemyBehaviorModuleKey\\.Summon|EnemySummonBehaviorModuleAsset|SummonBehaviorModule|EnemySummonBehaviorRuntime|SummonBehaviorRuntime|logicModuleAssets" Assets`
- `rg -n "[[:blank:]]$" Docs/Architecture/Enemy-AI-Summon-*.md Docs/Architecture/README.md`

Compiler/runtime slices:

- `./run_tests.sh --integration-simulation --filter EnemyUtilitySummon_`
- `./run_tests.sh --integration-replay --filter UtilitySummon`
- `./run_tests.sh --integration-replay --filter UtilityArchetypeSummon`
- `./run_tests.sh core`

Replay/hash slice:

- `./run_tests.sh --integration-replay --filter UtilitySummon`
- `./run_tests.sh --integration-replay --filter BehaviorSummon`
- `./run_tests.sh --integration-replay --filter MigratedSummon`

Presentation/audio/VFX slice:

- Run relevant presentation/audio/VFX parity filters.
- Run `./run_tests.sh core`.
- Run `./run_tests.sh ui` only if UI-facing runtime or prefab paths change.

Production migration slice:

- Run asset contract tests.
- Run residue scans.
- Run replay/hash/presentation parity filters.
- Run `./run_tests.sh full` or the CI release gate required by policy.

If full lane is not run, do not report broad validation success.

## 14. Risk Matrix

| Risk | Impact | Mitigation | Slice Gate |
|---|---|---|---|
| Duplicate Utility/Behavior source | Double summon, ordering drift, hash drift | Compiler fail-fast before migration | 5 |
| Request ordering drift | Different ids, cells, replay output | Same received request order and parity tests | 4 |
| Request payload drift | Source changes affect later materialization | Captured source metadata tests | 4 |
| Id allocation drift | Failed placement consumes ids or order shifts | Keep allocation in materializer after placement success | 4 |
| Max-alive drift | Extra or missing children | Parity tests against Utility baseline | 4 |
| Source invalidation/topology drift | Summon occurs from invalid source | Source death/topology parity tests | 4 |
| Determinism hash double-count/missing-count | Replay mismatch | Paired hash migration only | 6 |
| Replay/export rename | Consumer breakage | Preserve names for initial Option B | 6 |
| Presentation/audio/VFX cue rename | Missing bindings or changed playback | Preserve current names and cue semantics | 7 |
| Production asset residue | Duplicate source after migration | Allowlist and residue scans | 9 |
| `GravityFieldAura` accidental migration | Non-goal behavior drift | Exclusion tests and scans | 5, 9 |
| `RetiredLockNearbyBoxes` revival | Retired behavior returns | Retired guard test | 5, 9 |
| Generic registry overengineering | Scope creep and compile contract churn | Keep fixed typed slots | 2 |
| Full lane not run | Overstated validation | Record not-run status and require release gate | 9 |

## 15. Rollback Strategy

- Key/type slice rollback: revert key, typed slot, compiler switch, and tests in one commit.
- Asset type slice rollback: revert asset/config type and test-local fixtures.
- Runtime emitter slice rollback: revert behavior state/emitter changes; Utility-only assets remain valid.
- Duplicate guard rollback: revert guard only when no migrated production assets depend on Behavior Summon.
- Replay/hash slice rollback: revert compatibility/hash adapters and replay tests together.
- Presentation/audio/VFX slice rollback: revert presentation adapters without changing assets.
- Production asset migration rollback: revert the asset-scoped migration commit from source control.
- Utility-only fallback remains available until final asset-scoped migration is accepted.
- No whole-lane Utility migration, no Gravity migration, and no Retired migration are allowed.

## 16. Explicit Non-Goals

- No production migration in the compile-skeleton slice.
- No generic registry.
- No Utility whole-lane migration.
- No `GravityFieldAura` changes.
- No `RetiredLockNearbyBoxes` changes.
- No replay/export rename.
- No presentation/audio/VFX rename.
- No direct `WorldState` write.
- No Behavior Summon production asset migration, and no production TickPipeline ownership migration.
- No production `EnemySummonBehaviorModuleAsset` instances before asset-scoped migration.
- No `logicModuleAssets`.

## 17. Option B Implementation Entry Criteria

- This plan is accepted.
- Prerequisite docs are tracked.
- The first implementation slice is explicitly approved.
- Duplicate guard, key/runtime, replay/export, presentation/audio/VFX, and migration gates are assigned to separate reviewable slices.
- Full lane / CI release gate policy is decided before production migration or release.

Option B compile skeleton, the test-local Behavior Summon runtime/emitter parity slice, and the test-local full presentation/audio/VFX parity slice are implemented. Production Summon remains Utility-owned, replay/export and presentation/audio/VFX names remain preserved, neutral naming remains future schema/asset migration only, and production asset migration is still future gated work. Full lane was not run unless explicitly reported.
