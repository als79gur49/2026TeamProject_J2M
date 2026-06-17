# Summon Behavior Runtime / Emitter Parity Slice Closeout Result

## 1. Summary

- Closeout completed? yes
- Runtime behavior changed in this pass? no
- Test code changed in this pass? yes, presentation/audio/VFX parity coverage only
- Production assets changed? no
- Runtime/emitter implemented? yes, test-local Behavior path
- Production migration started? no
- Full presentation/audio/VFX parity completed? yes, for the test-local Behavior Summon path
- Full lane run? no

## 2. Files Changed

| File | Change Type | Purpose |
| --- | --- | --- |
| `Docs/Architecture/Enemy-AI-Summon-Behavior-Runtime-Emitter-Parity-Closeout.md` | Closeout report | Records runtime/emitter parity status, preserved contracts, validation evidence, scans, and follow-up gates. |
| `Docs/Architecture/Enemy-AI-Summon-Behavior-Runtime-State-Design.md` | Status wording | Replaces stale future-only runtime/emitter wording with implemented test-local status while keeping production migration future-gated. |
| `Docs/Architecture/Enemy-AI-Summon-Duplicate-Guard-Design.md` | Status wording | Updates compile-skeleton-only wording now that runtime/emitter parity exists for the test-local Behavior path. |
| `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Scenario/EnemyAiScenarioTests.cs` | Test coverage | Adds test-local Behavior Summon presentation/audio/VFX parity assertions. |
| `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Scenario/Game.Integration.Simulation.Tests.asmdef` | Test assembly references | Allows scenario tests to inspect enemy audio and VFX planner request surfaces. |
| `Docs/Architecture/Enemy-AI-Summon-Spawn-Seam-Implementation-Note.md` | Status wording | Reclassifies implemented runtime/emitter checks as completed before production migration rather than remaining Option B implementation work. |
| `Docs/Architecture/Enemy-AI-Summon-Option-B-First-Code-Slice-Closeout.md` | Status wording | Clarifies that the first code slice is compile skeleton and the follow-up runtime/emitter parity slice is now implemented. |
| `Docs/Architecture/README.md` | Index link | Adds this closeout report to the Summon architecture index and removes stale future-follow-up wording. |

## 3. Runtime / Emitter Status

- state lane: `WorldState` / `WorldSnapshot` have a fixed typed `EnemySummonBehaviorRuntimeState` lane.
- progression owner: `EnemyLogic` progresses test-local Behavior Summon initial delay, cooldown, windup, recovery, movement suppression, source invalidation, and topology suspension/cancel behavior.
- trigger intent: Behavior Summon emits a pre-movement trigger intent with captured source id, compatibility effect index, trigger tick, origin `SurfaceCell`, facing, team, and `SummonMinionRuntime` payload.
- request emission: `TickPipeline.PhaseResults` converts Behavior Summon intents to id-free `EntitySpawnRequest` values in post-attack resolve.
- materialization owner: `EntitySpawnMaterializer` remains the owner of placement, entity id allocation, entity construction, `SummonedEntityState`, `EnemyDefinitionBindingState`, and `FinalizationBatch.SpawnEntity`.
- hash/trace: `DeterminismHashBuilder` and `TickTraceFormatter` include Behavior Summon mutable state coverage.
- presentation minimum: `TickResultBuilder` preserves minimum windup warning compatibility through `TickSummonWindupWarningSignal` and existing utility presentation vocabulary.

## 4. Docs Updated

| Doc | Update |
| --- | --- |
| `Enemy-AI-Summon-Behavior-Runtime-State-Design.md` | Records test-local runtime state/emitter implementation while preserving production migration as future work. |
| `Enemy-AI-Summon-Duplicate-Guard-Design.md` | Updates guard status and test status to include runtime/emitter parity coverage now implemented. |
| `Enemy-AI-Summon-Spawn-Seam-Implementation-Note.md` | Keeps Spawn/EntityCreation seam as materialization owner and moves implemented parity checks out of remaining-work wording. |
| `Enemy-AI-Summon-Option-B-First-Code-Slice-Closeout.md` | Keeps the first slice closeout compile-skeleton scoped and points to this runtime/emitter closeout. |
| `Docs/Architecture/README.md` | Adds the dedicated runtime/emitter closeout to the active Summon supporting truth-source list. |

## 5. Contracts Preserved

- [x] Production Summoner remains Utility-owned
- [x] No production Behavior Summon asset
- [x] EntitySpawnRequest remains id-free
- [x] EntitySpawnMaterializer owns placement/id/write
- [x] no direct WorldState spawn write
- [x] replay/export names unchanged
- [x] presentation/audio/VFX names unchanged
- [x] Gravity/Retired unchanged
- [x] no generic registry
- [x] no logicModuleAssets

## 6. Validation

Already run / recorded:

| Command | Result |
| --- | --- |
| `./run_tests.sh --integration-simulation --filter BehaviorSummon` | Passed, 11 tests. |
| `./run_tests.sh --integration-simulation --filter EnemyUtilitySummon_` | Passed, 12 tests. |
| `./run_tests.sh --integration-replay --filter UtilitySummon` | Passed, 2 tests. |
| `./run_tests.sh --integration-replay --filter UtilityArchetypeSummon` | Passed, 1 test. |
| `./run_tests.sh core` | Passed, EditMode 189 + PlayMode 33. |
| `./run_tests.sh core --filter EnemyAiRuntimeDefinitionGuardTests` | Passed, EditMode 18. |
| `./run_tests.sh full --filter EnemyAiProfileAssetContractTests` | Passed, EditMode 21. |
| `git diff --check` | Passed. |

Newly run in closeout:

| Command | Result |
| --- | --- |
| `git status --short --branch` | Worktree was clean before closeout edits except branch ahead status. |
| `git diff --name-status` | No pre-closeout worktree diff. |
| `git diff --stat` | No pre-closeout worktree diff. |
| `git diff --check` | Passed for pre-closeout hygiene. |
| Stale Summon status phrase scan across `Docs/Architecture/Enemy-AI-Summon-*.md` and `Docs/Architecture/README.md` | No stale future-only runtime/emitter status phrases remain outside historical context. |
| `git diff --name-only -- Assets/_Features/Stages/Content` | No production content diff. |
| `rg -n "EnemySummonBehaviorModuleAsset\|EnemySummonBehaviorModule\|EnemyBehaviorModuleKey\\.Summon" Assets/_Features/Stages/Content` | No production Behavior Summon asset/key matches. |
| `rg -n "behaviorModuleAssets:" Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI` | Existing production profile fields remain; Summoner profile remains `behaviorModuleAssets: []`. |
| `rg -n "logicModuleAssets" Assets` | No matches. |
| `rg -n "EntityIdAllocator\|FinalizationBatch\\.SpawnEntity\|WorldState.*Spawn" Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime` | No matches in Enemy AI runtime. |
| `git diff --name-only -- Assets/_Features/Gameplay/Gameplay_Loop/Runtime/EntitySpawnRequest.cs Assets/_Features/Gameplay/Gameplay_Loop/Runtime/EntitySpawnMaterializer.cs` | No closeout diff. |
| `rg -n "SummonCommitted\|SummonSkipped\|SummonedEntities\|EnemyDefinitionBindings\|SourceEffectIndex\|Effect=" Assets/_Features/Gameplay` | Existing replay/export vocabulary remains present. |
| `rg -n "TickSummonWindupWarningSignal\|TickSummonedEnemyPresentationBinding\|EnemyAudioCue\\.Windup\|EnemyAudioCue\\.Active\|UtilityWindup\|UtilitySummonSpawn" Assets/_Features/Gameplay` | Existing presentation/audio/VFX vocabulary remains present. |
| `git diff --name-only -- Assets/_Features/Gameplay/Gameplay_Host Assets/_Features/Gameplay/Gameplay_Audio Assets/_Features/Gameplay/Gameplay_ActionAudio Assets/_Features/UI Assets/_Shared/Audio` | No closeout diff. |

Newly run for external review after closeout:

| Command | Result |
| --- | --- |
| `./run_tests.sh --integration-simulation --filter BehaviorSummon` | Passed, 11 tests. |
| `./run_tests.sh --integration-simulation --filter EnemyUtilitySummon_` | Passed, 12 tests. |
| `./run_tests.sh --integration-replay --filter UtilitySummon` | Passed, 2 tests. |
| `./run_tests.sh --integration-replay --filter UtilityArchetypeSummon` | Passed, 1 test. |

Newly run for the presentation/audio/VFX full parity slice:

| Command | Result |
| --- | --- |
| `git diff --check` | Passed. |
| `./run_tests.sh --integration-simulation --filter BehaviorSummon` | Passed, 27 tests. |
| `./run_tests.sh --integration-simulation --filter EnemyUtilitySummon_` | Passed, 12 tests. |
| `./run_tests.sh --integration-replay --filter UtilitySummon` | Passed, 2 tests. |
| `./run_tests.sh --integration-replay --filter UtilityArchetypeSummon` | Passed, 1 test. |
| `./run_tests.sh core` | Passed, EditMode 189 + PlayMode 33. |

Not run:

| Command | Result |
| --- | --- |
| `./run_tests.sh full` | Not run. Full lane was not required for this docs-only closeout and must not be claimed green. |
| `./run_tests.sh ui` | Not run. This closeout did not change UI code or UI assets. |

## 7. Follow-up

- production asset-scoped migration later
- no-double-count verification during production migration
- full lane / CI release gate

Summon Behavior runtime/emitter parity and full presentation/audio/VFX parity are closed out for the test-local Behavior Summon path. Production Utility Summon migration has not started. Spawn/EntityCreation materialization remains in the existing seam. Replay/export and presentation/audio/VFX names remain preserved. Full lane was not run unless explicitly reported.

Final verdict: closed for test-local Behavior Summon runtime/emitter and presentation/audio/VFX parity; production migration remains future gated.
