# Legacy Ordinary Unit Movement Deprecation Readiness v3

Date: 2026-05-01
Runtime validation update: 2026-05-02

This readiness pass does not delete legacy movement. It closes the `Special Movement Inventory v3`, adds deletion-readiness canary expectations, and drafts the future `Legacy Ordinary Unit Movement` deletion phases. `MoveEntity` remains the anchor/grid transaction primitive. `MovementExpander` remains retained for grid transactions and removed-diagnostic compatibility coverage.

Phase 1, `Covered Locomotion Fallback Isolation`, is runtime green for its targeted Unity canaries. It isolates player ordinary, enemy ordinary, Charge active, and active glide locomotion from legacy ordinary fallback under `DefaultGameplayLocomotion` and explicit flag-on lanes. It does not delete fallback branches, does not delete `MoveEntity`, does not delete `MovementExpander`, and does not rewrite replay/golden files.

Phase 2, `Player Legacy Ordinary Fallback Pilot`, scoped deletion readiness to the player ordinary fallback branch only. It pinned the player source, flag reachability, leak guard, `MovementExpander` legacy candidate, `LegacyFallback` boundary, and legacy `TickEntityMotionKind.Move` presentation without deleting the branch. Phase 3 moved that fallback authorization from `GameplayRuntimeFeatureFlags.None` to `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline`.

Phase 2B, `Enemy Ordinary Fallback Pilot`, scoped deletion readiness to the enemy ordinary fallback branch only. It pinned the enemy source, enemy kinematic flag reachability, leak guard, `MovementExpander` legacy candidate, `LegacyFallback` boundary, and legacy `TickEntityMotionKind.Move` presentation without deleting the branch. Phase 3 moved that fallback authorization from `GameplayRuntimeFeatureFlags.None` to `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline`.

Phase 2C, `Charge Active Fallback Pilot`, scoped deletion readiness to the Charge active fallback branch only. It pinned the active Charge source, Charge kinematic flag reachability, leak guard, legacy active-step consumption, `LegacyFallback` boundary, and legacy `TickEntityMotionKind.ChargeMove` presentation without deleting the branch. Phase 3 moved that fallback authorization from `GameplayRuntimeFeatureFlags.None` to `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline`.

Phase 3, `Explicit Legacy/Test-Only Fallback Policy`, is complete for runtime/test contract. `GameplayRuntimeFeatureFlags.None` no longer authorizes player ordinary, enemy ordinary, or Charge active covered fallback. Those fallback paths require `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline`, and unauthorized attempts are blocked with `LegacyOrdinaryFallbackRequiresExplicitBaseline`.

Phase 4, `Player Fallback Removal Pilot`, removes player ordinary fallback authorization even from `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline`. Player attempts under that preset are rejected with `PlayerLegacyFallbackRemovedFromRuntime`. At Phase 4, enemy ordinary fallback and Charge active fallback were still explicitly retained under the baseline.

Phase 5, `Enemy Fallback Removal Pilot`, removes enemy ordinary fallback authorization even from `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline`. Enemy attempts under that preset are rejected with `EnemyLegacyFallbackRemovedFromRuntime`. At Phase 5, player ordinary fallback remained removed and Charge active fallback was still explicitly retained under the baseline.

Phase 6, `Charge Fallback Removal Pilot`, removes Charge active fallback authorization even from `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline`. Charge attempts under that preset are rejected with `ChargeLegacyFallbackRemovedFromRuntime`. Player and enemy fallback remain removed, flag-off glide fallback remains a retained exception, and retained grid transactions remain allowed.

Phase 7, `Fallback Cleanup Readiness`, keeps `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline` as a diagnostic compatibility preset only. Phase 8A, `Obsolete Helper / Test Naming Cleanup`, aligns helper, test, and documentation vocabulary with that removed-diagnostic policy while keeping runtime boundary behavior unchanged. Obsolete covered-fallback `Allows*` wrappers remain for compatibility, but internal tests should use canonical `Assert*Removed*` helpers.

Phase 8B, `Diagnostic Baseline Rename Readiness`, added `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline` as the canonical preset name for deterministic removed diagnostics. The old `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackBaseline` alias is now removed; runtime boundary behavior is unchanged.

Phase 8C, `Legacy Diagnostic Alias Usage Cleanup Readiness`, migrated current internal tests, replay helpers, and current-policy documentation to `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline`. The old alias is removed and may only remain in historical documentation mentions; runtime boundary behavior is unchanged.

Phase 8D, `Diagnostic Flag / Helper Naming Cleanup Readiness`, added `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticsEnabled` as the canonical helper property for removed-fallback diagnostic routing. The old `GameplayRuntimeFeatureFlags.LegacyOrdinaryFallbackEnabled` helper alias is now removed, and `GameplayRuntimeFeatureFlags.EnableLegacyOrdinaryUnitFallback` remains the underlying compatibility field pending owner decision; runtime boundary behavior is unchanged.

Phase 8E, `Underlying Diagnostic Field Rename / Removal Readiness`, inventories `GameplayRuntimeFeatureFlags.EnableLegacyOrdinaryUnitFallback` as an underlying compatibility field and keeps it in place. The canonical helper remains `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticsEnabled`; field rename/delete and new canonical field addition are blocked pending a later compatibility decision. Runtime boundary behavior, scene serialization exposure, trace token text, replay/golden files, retained grid transactions, and glide policy are unchanged.

After Phase 8E, the project stops extending the micro-phase chain and moves to `Legacy Compatibility Layer Consolidation`. The next goal is a single inventory and prioritization package for compatibility presets, helpers, fields, trace vocabulary, presentation remnants, tests, docs, and replay/golden policy. Covered fallback authorization remains removed; compatibility layer cleanup remains pending. `ChargeMove` presentation cleanup readiness recorded producer current runtime unreachable for default, `None`, removed-diagnostic, and Charge kinematic flag-on lanes; the follow-up deletion and verification packages removed enum/consumer/authoring/timing support and confirmed active gameplay C# has no deleted-symbol references. The detailed results are documented in `Legacy-Ordinary-Unit-Movement-Decommission-ChargeMove-Presentation-Cleanup-Readiness-2026-05-02.md` and `Legacy-Ordinary-Unit-Movement-Decommission-ChargeMove-Deletion-Verification-And-Residue-Report-2026-05-02.md`. `MoveEntity`, `MovementExpander`, retained grid transactions, and glide retained fallback remain protected.

`TickEntityMotionKind.Move` is an ownership-narrowing target, not a deletion target. Retained grid/generic presentation remains protected while covered fallback residue is inventoried separately.

## Executive Decision

`Legacy Ordinary Unit Movement` is the only deletion target: an ordinary `Unit` `MovementCommandKind.Move` reaches legacy expansion, commits through `MoveEntity`, and presents as legacy `TickEntityMotionKind.Move` or `TickEntityMotionKind.ChargeMove`.

`Legacy Grid Transaction` is retained: `MoveEntity` can still materialize box, topology, spawn, respawn, cleanup, scripted relocation, and anchor-normalization semantics when those paths are not ordinary Unit locomotion. `DefaultGameplayLocomotion` is an explicit readiness/adoption bundle, not deletion. `GameplayRuntimeFeatureFlags.None` and default struct behavior remain no-advanced-locomotion lanes, but no longer authorize covered legacy ordinary fallback. `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline` is the canonical preset for deterministic removed-fallback diagnostics, and `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticsEnabled` is the canonical helper for removed diagnostic routing.

Jump and phase relocation are not deletion blockers. Glide explicit flag-on active kinematic locomotion is stable after v1.1 targeted coverage, but Glide Default Adoption Readiness v1 keeps `EnableEnemyGlideKinematicLocomotion` out of `DefaultGameplayLocomotion`. Forced motion and knockback have runtime vocabulary through `MotionMode.Forced` and `ForcedMotionOp.Knockback`, but no gameplay producer in this readiness slice; any future producer must define a special/kinematic boundary before shipping.

## Current Validated Readiness

| area | status | decision |
|---|---|---|
| Boundary v1 | complete | `LocomotionAnchorCommit` and `UnitOrdinaryLocomotion` remain the suppression boundaries |
| `DefaultGameplayLocomotion` bundle | complete | explicit opt-in bundle only |
| targeted readiness canary | complete | Unity XML green is recorded as a deletion precondition, not deletion approval |
| full `EnemyAiScenarioTests` historical failures | partial | separated from deletion readiness unless same-row behavior regresses |
| no-legacy ordinary canary | complete for representative coverage | special replay coverage added before deletion |
| Phase 1 covered locomotion fallback isolation | complete | player ordinary, enemy ordinary, and Charge active fallback leaks are hard regressions under default/flag-on lanes |
| special inventory | partial for v3 | forced/knockback closed as no gameplay producer; glide explicit flag-on and default active kinematic are stable; flag-off fallback remains retained |
| Phase 2 player fallback pilot | complete for scoped canaries | player fallback branch was pinned before policy separation |
| Phase 2B enemy fallback pilot | complete for scoped canaries | enemy fallback branch was pinned before policy separation |
| Phase 2C Charge fallback pilot | complete for scoped canaries | Charge fallback branch was pinned before policy separation |
| Phase 3 explicit fallback policy | complete for scoped canaries | `None` no longer authorizes covered fallback; explicit baseline now exists only for removed-diagnostic compatibility after Phase 6 |
| Phase 4 player fallback removal pilot | complete for scoped canaries | player runtime fallback is blocked even under explicit baseline |
| Phase 5 enemy fallback removal pilot | complete for scoped canaries | enemy runtime fallback is blocked even under explicit baseline |
| Phase 6 Charge fallback removal pilot | complete for scoped canaries | Charge runtime fallback is blocked even under explicit baseline |
| Phase 7 diagnostic compatibility preset readiness | complete for scoped canaries | explicit baseline remains only for deterministic removed diagnostics |
| Phase 8A obsolete helper/test naming cleanup | complete for scoped canaries | canonical removed-diagnostic helper names are stable; obsolete wrappers are retained for compatibility |
| Phase 8B diagnostic baseline rename readiness | complete for scoped canaries | `RemovedLegacyFallbackDiagnosticBaseline` is canonical; `LegacyOrdinaryFallbackBaseline` is removed |
| Phase 8C legacy alias usage cleanup readiness | complete for scoped canaries | current tests/docs use `RemovedLegacyFallbackDiagnosticBaseline`; old alias retained only in historical docs |
| Phase 8D diagnostic helper naming cleanup readiness | complete for scoped canaries | current runtime/tests/docs use `RemovedLegacyFallbackDiagnosticsEnabled`; `LegacyOrdinaryFallbackEnabled` is removed |
| Phase 8E underlying field readiness | complete for scoped canaries | `EnableLegacyOrdinaryUnitFallback` remains a compatibility field; rename/delete is deferred pending field/config/trace migration approval |
| Legacy Compatibility Layer Consolidation | next unified package | inventory compatibility presets/helpers/field/trace/presentation/tests/docs/replay policy; no runtime deletion and no micro-phase extension |
| actual deletion | partial | player, enemy, and Charge runtime fallback authorization is removed; glide retained fallback, grid transactions, `MoveEntity`, and `MovementExpander` remain retained |

## Phase 1 Runtime Validation

Phase 1 runtime validation is based on Unity Test Runner XML, not `dotnet test` or `dotnet build` alone.

| XML | scope | result | Phase 1 decision |
|---|---|---|---|
| `TestResults/scoped-deletion-prep-simulation.xml` | `Game.Integration.Simulation.Tests` | `total=426`, `passed=417`, `failed=9` | scoped deletion prep boundary canaries passed; remaining failures are outside the covered fallback isolation contract |
| `TestResults/scoped-deletion-prep-replay.xml` | `Game.Integration.Replay.Tests` | `total=93`, `passed=87`, `failed=6` | scoped deletion prep replay canaries passed; remaining failures are existing unrelated replay/golden drift |

Targeted fixture status from the XML:

| fixture / canary group | XML status | decision |
|---|---|---|
| `BoundaryInventoryScenarioTests` | 35 passed, 0 failed | scoped branch inventory canaries green |
| `MovementPhaseScenarioTests` | 88 passed, 0 failed | grid branch and expander retained canaries green |
| `PlayerContinuousLocomotionScenarioTests` | 54 passed, 0 failed | player Free2D covered fallback isolation green |
| `PlayerKinematicLocomotionScenarioTests` | 28 passed, 1 failed | Phase 1 named canaries are green; `PlayerSameFaceContinuousLocomotion_FlagOn_BoxBlocksOrdinaryMove` is tracked as non-Phase-1 expectation failure |
| `EnemyAiScenarioTests` | 132 passed, 2 failed | enemy ordinary/charge Phase 1 subset is green; two historical EnemyAi rows remain separated |
| `PlayerContinuousLocomotionReplayTests` | 7 passed, 0 failed | player replay coverage green |
| `EnemyKinematicLocomotionReplayTests` | 16 passed, 0 failed | enemy/charge/glide scoped replay coverage green |

Failure classification for the remaining red rows:

| test name | expected | actual | world state changed? | presentation changed? | replay hash changed? | event/trace changed? | diagnostic reason | boundary kind | classification | recommended fix |
|---|---|---|---|---|---|---|---|---|---|---|
| `PlayerKinematicLocomotionScenarioTests.PlayerSameFaceContinuousLocomotion_FlagOn_BoxBlocksOrdinaryMove` | kinematic block reason present | `Reason=KinematicTraversalBlocked` not found | no Phase 1 evidence | no Phase 1 evidence | N/A | yes, expectation text | none | none | `TraceOnlyExpectationDrift` | update this expectation only in the owning player kinematic slice after confirming the intended blocker trace vocabulary |
| `EnemyAiScenarioTests.EnemyAi_MoveOccupancy_BlocksAttackUntilKinematicCommitUnlocksRange` | contact is blocked before kinematic commit and allowed at commit | current kinematic contract replaces the old immediate unlock tick | yes, enemy AI timing | yes | N/A | yes | none | none | `SupersededByKinematicContract` | old pre-Phase5 cadence test was removed during enemy AI kinematic contract cleanup |
| `EnemyAiScenarioTests.EnemyAi_KinematicPatrolChaseAttackRecover_CurrentContract` | patrol/chase/action/recover use kinematic commit and action-state gates | current kinematic contract replaces the old pinned first-3-tick sequence | yes, EnemyAi timing | yes | N/A | yes | none | none | `SupersededByKinematicContract` | old pre-Phase5 cadence test was removed during enemy AI kinematic contract cleanup |
| replay/golden failures in `TickReplayDeterminismTests` | pinned golden substrings | stale substrings or invalid historical state | yes/no by test | yes/no by test | yes | yes | none | mixed | `ExistingUnrelatedFailure` | do not auto-migrate goldens in Phase 1 |

## Scoped Deletion Preparation

This pass is `Scoped Fallback Branch Inventory & Test-Only Isolation Preparation`. Phase 6 now blocks player, enemy, and Charge covered fallback from the runtime path. `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline` is the current diagnostic preset for reproducing those removed diagnostics. It does not delete `MoveEntity`, `MovementExpander`, retained grid transactions, or flag-off glide fallback.

`DefaultGameplayLocomotion` and `GameplayRuntimeFeatureFlags.None` must have no covered player ordinary, enemy ordinary, or Charge active legacy fallback. `DefaultGameplayLocomotion` now includes active glide kinematic locomotion; `GameplayRuntimeFeatureFlags.None` and explicit flag-off keep active glide fallback as a rollback/historical baseline.

## Covered Fallback Branch Inventory

| branch | file / method | current flag condition | how branch is reached | replacement path | retained baseline? | can be removed now? | deletion blocker | tests covering allowed use | tests covering forbidden use | recommended future phase |
|---|---|---|---|---|---|---|---|---|---|---|
| player legacy ordinary fallback | `TickPipeline.ValidateLegacyExpansionIntents`, `TryResolveForbiddenLegacyUnitOrdinaryMovement`, `MovementExpander.ExpandMoveLike/ExpandMove`, `PlayerLogic.CollectMovementIntents` | removed from runtime; `RemovedLegacyFallbackDiagnosticBaseline` reproduces diagnostics | `PlayerLogic` emits ordinary `RawMovementIntent(Move)` -> `MoveIntent`; validation rejects before legacy expansion commit | `UnitContinuousLocomotionState` or player kinematic fallback | no, blocked with `PlayerLegacyFallbackRemovedFromRuntime` | player pilot complete | replay/golden cleanup remains | `Phase4_RemovedDiagnosticBaseline_PlayerFallbackRemoved` | `Phase3_None_NoPlayerEnemyChargeLegacyFallback`, default canaries | player presentation/helper cleanup |
| enemy legacy ordinary fallback | `TickPipeline.TryResolveForbiddenLegacyUnitOrdinaryMovement`, `IsEnemyKinematicStartParticipant`, `MovementExpander`, `EnemyLogic.ResolveBaselineGroundLocomotion` | removed from runtime; `RemovedLegacyFallbackDiagnosticBaseline` reproduces diagnostics; glide flag-off fallback is separate retained exception | enemy AI/script emits ordinary `Move` and is not handled by kinematic planner | enemy ordinary `UnitKinematicRuntimeState(MotionMode.Voluntary)` plus `TickKinematicMotionTrack` | no, blocked with `EnemyLegacyFallbackRemovedFromRuntime` | enemy pilot complete | replay/golden cleanup remains | `Phase5_RemovedDiagnosticBaseline_EnemyFallbackRemoved` | `Phase3_None_NoPlayerEnemyChargeLegacyFallback`, default canaries | enemy presentation/helper cleanup |
| legacy Charge active fallback | `EnemyLogic.ResolveBaselineGroundLocomotion`, `TryBuildEnemyChargeKinematicStartPayload`, `TickResultBuilder.TryResolveMotionKind` | removed from runtime; `RemovedLegacyFallbackDiagnosticBaseline` reproduces diagnostics; runtime builder no longer infers `ChargeMove` | active Charge emits ordinary `Move` ignoring units -> validation rejects before legacy expansion commit | `UnitKinematicRuntimeState(MotionMode.Charge)` plus kinematic presentation | no, blocked with `ChargeLegacyFallbackRemovedFromRuntime` | Charge pilot complete | Charge replay/golden cleanup remains | `Phase6_RemovedDiagnosticBaseline_ChargeFallbackRemoved` | default charge kinematic canaries, `Phase3_None_NoPlayerEnemyChargeLegacyFallback`, `ChargeMoveIsolation_RuntimeBuilder_DoesNotInferChargeMove` | Charge enum/consumer cleanup only after owner approval |
| legacy Unit `TickEntityMotionKind.Move` generation | `TickResultBuilder.TryResolveMotionKind`, `ShouldSuppressLegacyMotionForLocomotion` | generated for unsuppressed `MoveEntity` with `MovementSemanticKind.Move/Item` | removed diagnostic compatibility or retained grid/item/topology path reaches presentation builder | `TickContinuousLocomotionTrack` or `TickKinematicMotionTrack` for covered locomotion | yes for retained grid, no for covered fallback | no | retained grid presentation allowlist | grid transaction canaries, removed-diagnostic fallback canaries | `ScopedDeletionPrep_LegacyUnitMotionPresentation_IsOnlyFallbackOrGrid` | Phase 2B presentation narrowing |
| removed legacy Charge entity-motion generation | historical synthetic `TickPresentationData` / host fixtures | removed | tests no longer construct the removed Charge entity-motion enum; runtime `TickResultBuilder` no longer infers it from active Charge state | Charge kinematic presentation track plus `TickEnemyChargePresentationSignal` | no for covered runtime fallback after Phase 6 | consumer deletion complete | golden policy for historical replay assets only | deletion canaries assert no enum/timing/authoring references; runtime matrix canaries assert no legacy Charge entity motion | `ScopedDeletionPrep_ChargeLegacyFallback_RemovedByPhase6` | historical docs only |
| stale ordinary Unit move test helpers | `LegacyMovementBoundaryAssert`, scenario wrappers | test-only | broad helpers can confuse `MoveEntity` with legacy fallback | scoped helpers based on `LegacyFallback`, legacy presentation, and diagnostics | test-only retained | no deletion | helper callers still cover retained paths | existing helper callers | new `ScopedDeletionPrep_*` wrappers | Phase 1.5 helper isolation |
| removed diagnostic baseline | `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline`; helper `RemovedLegacyFallbackDiagnosticsEnabled` | player, enemy, and Charge fallback authorization removed | canonical preset/helper keeps diagnostic routing reproducible while validation rejects covered fallback | player/enemy/Charge replacement paths | no covered fallback retained | no | replay/golden contract and field rename approval | `Replay_Phase8D_RemovedDiagnosticHelper_DiagnosticsDeterministic` | `Replay_Phase3_None_NoCoveredFallback` | field/trace cleanup |
| glide default / flag-off fallback | `DefaultGameplayLocomotion`, `IsEnemyActiveGlideKinematicParticipant`, glide policy tests | default includes glide flag; `None` off | default active glide uses kinematic path; flag-off active glide can emit retained fallback | active glide kinematic path | flag-off only | default yes; flag-off no | default glide adoption complete | glide default/flag-off canaries | explicit glide flag-on canary | flag-off rollback baseline retained |
| grid transaction branches | `IsAllowedLegacyGridTransactionIntent`, `ResolveMovementExecutionBoundaryKind`, placement/finalization processors | any flag lane | push/flip/item/topology/spawn/respawn/cleanup/scripted relocation | no replacement planned | yes | no | not deletion target | `ScopedDeletionPrep_GridTransactions_AreNotDeletionCandidates` | no covered diagnostics | retained |
| movement expander retained grid branch | `MovementExpander.Expand`, `ExpandMoveLike`, `ExpandMove` | grid transaction or flag-off fallback | allowed intent reaches expander; forbidden covered IDs are rejected | keep for grid and `None` | yes | no | expander owns grid expansion | `ScopedDeletionPrep_MovementExpander_GridBranchIsRetained` | forbidden intent diagnostic canary | retained |

## Flag-Off/Test-Only Fallback Policy

`LegacyMovementBoundaryAssert` helpers for this phase must not ban `MoveEntity`. They assert only `MovementExecutionBoundaryKind.LegacyFallback`, legacy `TickEntityMotionKind.Move`, legacy `TickEntityMotionKind.ChargeMove`, and `LegacyUnitOrdinaryMovementDetected` diagnostics. `NoCoveredFallbackInDefaultGameplayLocomotion`, `AssertCoveredFallbackRemovedDiagnostics`, `AssertPlayerFallbackRemovedFromRuntime`, `AssertEnemyFallbackRemovedFromRuntime`, `AssertChargeFallbackRemovedFromRuntime`, `AllowsRetainedGlideFallbackOnlyWhenGlideFlagOff`, `NoLegacyUnitPresentationForCoveredEntities`, `LegacyFallbackIsOnlyForAllowedEntities`, and `GridTransactionBranchesRemainAllowed` are the scoped helper vocabulary for new tests.

## Deletion Candidate vs Retained Path

| path | decision | reason | required tests before deletion |
|---|---|---|---|
| player legacy ordinary fallback | removed from runtime pilot | Phase 4 blocks player fallback even under explicit baseline | player removed diagnostic, replay no-fallback canary, future helper/presentation cleanup |
| enemy legacy ordinary fallback | removed from runtime pilot | Phase 5 blocks enemy fallback even under explicit baseline | enemy removed diagnostic, replay no-fallback canary, future helper/presentation cleanup |
| Charge legacy fallback | removed from runtime pilot | Phase 6 blocks Charge fallback even under explicit baseline | Charge removed diagnostic, replay no-fallback canary, future helper/presentation cleanup |
| glide flag-off fallback | retained exception | glide flag remains off under `None` and explicit rollback configs | fallback deletion decision only |
| `MoveEntity` | retained primitive | anchor/grid transaction primitive | never delete in ordinary fallback phase |
| `MovementExpander` | retained component | grid transactions and explicit fallback baseline still use it | protect grid branch canaries |
| grid transaction branches | retained | not ordinary Unit locomotion | boundary-kind and presentation canaries |

## Definitions

- `Legacy Ordinary Unit Movement`: ordinary `Unit` `MovementCommandKind.Move` -> `MovementExpander` -> immediate `MoveEntity` anchor change -> legacy `TickEntityMotionKind.Move` or charge legacy `ChargeMove` presentation.
- `Legacy Grid Transaction`: retained semantic grid transaction that may use `MoveEntity` but is not ordinary Unit locomotion.
- `Unit Continuous Locomotion`: player ordinary movement backed by `UnitContinuousLocomotionState`.
- `Unit Kinematic Locomotion`: segment movement backed by `UnitKinematicRuntimeState`, including enemy ordinary, player fallback, and charge active steps.
- `Unit Special Locomotion`: non-ordinary unit movement such as charge, jump, phase-like movement, glide, and future forced/knockback.
- `Legacy Fallback`: historical runtime path now removed for covered player/enemy/Charge fallback. `RemovedLegacyFallbackDiagnosticBaseline` is the canonical preset for deterministic removed diagnostics; `LegacyOrdinaryFallbackBaseline` is removed alias vocabulary.
- `Covered Locomotion`: Phase 1 no-fallback scope: player ordinary movement, enemy ordinary movement, and Charge active movement.
- `Retained Exception`: Phase 1 fallback that remains intentionally allowed: flag-off active glide and grid transactions. Removed-diagnostic compatibility presets do not authorize covered fallback.

## DefaultGameplayLocomotion Adoption

| host / installer / harness | current flag source | uses bundle? | should use bundle now? | preserve `None`? | risk | tests | recommendation |
|---|---|---:|---:|---:|---|---|---|
| `StageBackedGameplaySceneInstaller` | explicit config hook | yes | yes | yes | showcase active glide now uses kinematic path | `StageBackedGameplaySceneInstaller_DefaultBundle_IncludesGlideKinematic` | keep bundle; glide is enabled by default adoption |
| `StageBackedGameplaySceneInstallerBase` | base config | no | no | yes | over-broad scene opt-in | host config tests | do not apply globally |
| campaign scene host | installer/config authored flags | no/partial | later | yes | campaign behavior drift | playmode smoke later | keep explicit opt-in only |
| editor direct play | scene config defaults | no | no | yes | hidden default-on | host default tests | keep `None` unless authored |
| `GameplaySceneHostConfiguration` default | bool fields false | helper only | no implicit default | yes | baseline drift | `HostConfiguration_DefaultGameplayLocomotion_AppliesExpectedFlags` | default constructor remains `None` |
| `GameplayHostRuntimeFactory` | `configuration.CreateRuntimeFeatureFlags()` | passthrough | no policy | yes | hidden policy in factory | runtime factory tests | keep passthrough |
| `GameplayBootstrapper` / `GameplayCompositionRoot` | optional `default` parameter | no | no | yes | replay/golden break | structure/core tests | keep default `None` |
| scenario factories | explicit per test | partial | readiness scenarios only | yes | accidental broad migration | targeted scenarios | explicit bundle only |
| replay harness | explicit optional flags | no default bundle | explicit canaries only | yes | golden migration | replay tests | default `None` |
| playmode host | scene config | no | later opt-in | yes | user-facing drift | playmode host smoke | defer broad default-on |

## Glide Default Adoption Readiness v1

| area | current status | if glide flag included impact | risk | tests required | recommendation |
|---|---|---|---|---|---|
| `DefaultGameplayLocomotion` | glide included | default active glide switches to kinematic | default gameplay shift | `DefaultGameplayLocomotion_IncludesGlideKinematic` | adopted in v2 |
| `StageBackedGameplaySceneInstaller` | uses bundle | showcase glide auto-enables | showcase behavior surprise | `StageBackedGameplaySceneInstaller_DefaultBundle_IncludesGlideKinematic` | intentional default adoption |
| campaign host | explicit/authored flags | only bundle-applied hosts would change | campaign drift | host config smoke if touched | no change |
| replay harness | default `None` | accidental golden churn | baseline drift | replay default `None` assertions | keep `None` |
| default gameplay replay canary | player/enemy/Charge/glide focused | active glide trace/hash changes intentionally | kinematic canary clarity | `Replay_DefaultGameplayLocomotion_GlideKinematic_IsDeterministic` | default glide kinematic check |
| flag-off fallback canary | maintained | unaffected by inclusion | accidental fallback break | `BoundaryInventory_GlideFlagOff_FallbackStillRetained` | keep |
| boundary inventory canary | default glide canary exists | default no-legacy includes active glide | adoption completeness | `BoundaryInventory_DefaultGameplayLocomotion_GlideActiveKinematic` | default canary includes active glide kinematic |
| readiness checklist | glide partial | included would become complete candidate | premature deletion | `LegacyOrdinaryUnitMovement_DeprecationReadiness_Report` | block default adoption |
| broad full suite | targeted green | hidden regression possible | broad regression | targeted XML plus broad separation | no default inclusion before broad gate |
| docs / rollout | v1.1 explicit | inclusion doc churn | policy drift | rollout, ADR, readiness docs | record Option B |

## Special Movement Inventory v3

| movement | current runtime path | current state carrier | boundary kind | uses `MoveEntity`? | uses `MovementExpander`? | presentation source | hash/replay state | ordinary deletion risk | current classification | future migration need | tests covering it | recommended next action |
|---|---|---|---|---:|---:|---|---|---|---|---|---|---|
| jump start | `EnemyLogic.CommitJumpState` -> `SetEnemyJumpState` | `EnemyJumpRuntimeState(Windup)` | `UnitSpecialLocomotion` | no | no | jump state presentation | enemy jump state hash included | low | Safe UnitSpecialLocomotion | none for deletion | `BoundaryInventory_SpecialMovement_Jump_IsUnitSpecialLocomotion` | keep documented |
| jump airborne | jump state transition + detached presence | `EnemyJumpRuntimeState(Airborne)` + board presence | `UnitSpecialLocomotion` | no | no | jump state/presence | jump state + presence | low | Safe UnitSpecialLocomotion | none for deletion | jump inventory canary | keep documented |
| jump landing | `ResolvePlanJumpLandings` / landing contest | `EnemyJumpRuntimeState` + position | `UnitSpecialLocomotion` | yes | no | jump landing presentation | position + jump state | low | Safe UnitSpecialLocomotion | possible future kinematic, not deletion blocker | jump scenario + replay canary | retain |
| phase relocation | `ResolvePlanEnemyPhaseRelocations` | phase relocation payload + phased state | `ScriptedRelocation` | yes | no | retained relocation/entity motion | position + phased state | low | Safe Retained Grid Transaction | none before deletion | phase relocation canary + replay canary | retain |
| phase state lifecycle | phased state enter/sustain/exit | `PhasedRuntimeState` | state-only or `ScriptedRelocation` when relocating | relocation only | no | state/trace | phased state hash | low | Safe Retained Grid Transaction | none | phase scenario tests | document non-ordinary lifecycle |
| glide start | `EnemyLogic.CommitGlideState` | `EnemyGlideRuntimeState(Windup/Active)` | state-only | no | no | `TickEnemyGlidePresentationSignal` face-normal lift | enemy glide state hash included; glide presentation excluded | low for start tick | State-only Special Candidate | none for start tick | `BoundaryInventory_Glide_NoLegacyOrdinaryMoveLeak`, `GlidePresentation_Windup_RisesAlongFaceNormal` | keep state-only start canary |
| glide active/state-only lifecycle | glide ticking/target suppression plus active chase kinematic path | `EnemyGlideRuntimeState(Active/LandingPending/Recovery/Cooldown)` + optional `UnitKinematicRuntimeState(MotionMode.Voluntary)` | default/flag-on active chase uses `LocomotionAnchorCommit`; flag-off paths keep documented `LegacyFallback` ordinary move | default/flag-on no; flag-off yes | default/flag-on no; flag-off yes | default/flag-on horizontal `TickKinematicMotionTrack` plus additive `TickEnemyGlidePresentationSignal` height; flag-off legacy horizontal move baseline retained | glide state and UnitKinematics hash included for default/flag-on; glide presentation and boundary metadata excluded | explicit flag-on stable; default adoption complete | Flag-Gated Kinematic Candidate | keep explicit flag and flag-off fallback rollback baseline | `ExplicitGlideFlag_ActiveGlide_NoLegacyOrdinaryMove`, `BoundaryInventory_DefaultGameplayLocomotion_GlideActiveKinematic`, `BoundaryInventory_GlideFlagOff_FallbackStillRetained`, `Replay_DefaultGameplayLocomotion_GlideKinematic_IsDeterministic`, `Replay_EnemyGlideActiveKinematicLocomotion_IsDeterministic`, `GlideActive_Kinematic_ReplayContactAndLandingDeterministic` | do not delete flag-off fallback |
| glide end | recovery/cooldown/clear | `EnemyGlideRuntimeState` | state-only | no | no | `TickEnemyGlidePresentationSignal` recovery/terminal zero | glide state hash/clear; glide presentation excluded | low | State-only Special Candidate | none before deletion | `GlidePresentation_Recovery_DescendsAndSupportsDip`, `GlidePresentation_Clear_DoesNotLeaveStaleHoverSignal` | keep presentation-only stale-clear coverage |
| forced motion | no gameplay producer; vocabulary exists | `MotionMode.Forced`, `ForcedMotionOp` vocabulary | none today | no current path | no | none | no current gameplay footprint | future high | Future Runtime State Needed | explicit state producer + boundary before feature | `BoundaryInventory_ForcedMotion_NoRuntimeProducerYet` | keep canary |
| knockback | no gameplay producer; enum value exists | `ForcedMotionOp.Knockback` vocabulary | none today | no current path | no | none | no current gameplay footprint | future high | Future Runtime State Needed | explicit special/kinematic boundary before feature | forced inventory canary | keep canary |
| scripted relocation | phase/scripted finalization payload | relocation payload | `ScriptedRelocation` | yes | no | entity motion/trace | position | low | Safe Retained Grid Transaction | none | phase relocation canary | retain |
| topology transition | movement group topology materialization, including Player Free2D local-zero and approach-zero-settle topology handoff | topology + position | `TopologyMaterialization` | yes | yes, allowed grid branch | topology motion | topology + anchor | none | Safe Retained Grid Transaction | none | grid transaction canary + `MovementPhaseScenarioTests` | retain allowlist |
| cleanup removal | cleanup lifecycle | destroy/cleanup result | cleanup lifecycle | no movement commit | no | cleanup/event/visibility | entity removal | none | Safe Retained Grid Transaction | none | spawn/respawn/cleanup canary | retain |
| spawn | spawn/summon/placement path | entity creation metadata | `SpawnRespawnPlacement` or spawn metadata | placement only | no | spawn visibility/event | entity state | none | Safe Retained Grid Transaction | none | spawn replay + respawn tests | retain |
| respawn | `TickPipeline.RespawnProcessor` | respawn placement record | `SpawnRespawnPlacement` | placement op yes when materialized | no | visibility/event/placement | entity state | none | Safe Retained Grid Transaction | none | `InactiveFaceResidualUnitTests`, boundary inventory | retain |

## Classification Decisions

| classification | movements | reason |
|---|---|---|
| Safe Retained Grid Transaction | phase relocation, scripted relocation, topology, cleanup, spawn, respawn | not ordinary Unit movement; may need `MoveEntity` or retained presentation |
| Safe UnitSpecialLocomotion | jump start, airborne, landing | explicit `UnitSpecialLocomotion`; no legacy ordinary leak |
| State-only Special Candidate | glide start/end | state commits do not require ordinary movement |
| Future Runtime State Needed | forced motion, knockback | vocabulary exists, but no gameplay producer/boundary policy |
| Potential Legacy Ordinary Leak | glide active chase fallback when glide flag is off or default bundle is used | v1 adoption keeps this documented baseline until default adoption or an equivalent replacement is approved |
| Needs Tests | forced no-producer, jump/phase/glide replay | covered by v3 canaries |
| Needs Implementation Before Deletion | glide default adoption or equivalent replacement policy | ordinary fallback deletion must not proceed while default gameplay still documents active glide fallback |

## Legacy Deletion Candidates

| fallback branch | file / method | current flag condition | replacement path | retained baseline? | can be removed now? | deletion blocker | tests required | recommended phase |
|---|---|---|---|---|---|---|---|---|
| player legacy ordinary fallback | `TickPipeline` movement validation/finalization path for player `Move` | removed from runtime; `RemovedLegacyFallbackDiagnosticBaseline` reproduces diagnostics | `UnitContinuousLocomotionState` / player kinematic fallback | no, blocked with `PlayerLegacyFallbackRemovedFromRuntime` | pilot complete | replay/golden cleanup unresolved | player default/`None` no-fallback, removed diagnostic baseline, player replay | cleanup candidate |
| enemy legacy ordinary fallback | `TickPipeline.ValidateLegacyExpansionIntents`, `BuildForbiddenLegacyUnitOrdinaryIntentIds`, `MovementExpander` ordinary Unit branch | removed from runtime; `RemovedLegacyFallbackDiagnosticBaseline` reproduces diagnostics; glide exception boundary remains separate | enemy ordinary `UnitKinematicRuntimeState` | no, blocked with `EnemyLegacyFallbackRemovedFromRuntime` | pilot complete | replay/golden cleanup unresolved | enemy default/`None` no-fallback, removed diagnostic baseline, enemy replay | cleanup candidate |
| legacy charge active fallback | `TickPipeline` charge movement path and historical synthetic Charge entity-motion fixtures | removed from runtime; `RemovedLegacyFallbackDiagnosticBaseline` reproduces diagnostics; runtime builder no longer infers legacy Charge entity motion | charge kinematic `MotionMode.Charge` plus charge presentation signal | no, blocked with `ChargeLegacyFallbackRemovedFromRuntime` | pilot complete; consumer deletion complete | charge replay/golden cleanup unresolved | charge active default/`None`/diagnostic/charge-kinematic/all-kinematic no-fallback, charge replay | historical docs only |
| stale ordinary Unit move test helper | `LegacyMovementBoundaryAssert` and scenario/replay helper callers | test-only | Phase 1 boundary helpers and explicit retained-grid assertions | no runtime baseline | no | helper callers still cover retained paths | helper usage inventory and targeted helper replacement tests | Phase 2 cleanup candidate |
| legacy Unit motion presentation generation | `TickResultBuilder` legacy Unit motion generation | retained grid/glide or historical fallback result generation | kinematic/presentation tracks or retained grid presentation | retained for grid/glide and historical compatibility, not covered baseline output | no | presentation retained paths must stay visible | presentation suppression canary and retained-grid presentation tests | Phase 7 presentation cleanup inventory |
| removed diagnostic baseline | `GameplayRuntimeFeatureFlags.RemovedLegacyFallbackDiagnosticBaseline` callers | player/enemy/Charge fallback authorization removed | diagnostic compatibility flag shape until preset cleanup | no covered fallback retained | no | rollback/golden contract | player/enemy/Charge removed baseline canaries | cleanup candidate |
| glide flag-off fallback | glide active fallback path | `GameplayRuntimeFeatureFlags.None`, explicit flag-off | default/flag-on glide kinematic | flag-off only | no | rollback baseline | flag-off replay and default/explicit glide kinematic canaries | fallback deletion decision |

## Retained Paths

| retained path | why retained | deletion guard |
|---|---|---|
| `MoveEntity` primitive | anchor/grid transaction primitive | never delete in ordinary fallback phase |
| `MovementExpander` grid transaction branch | push/flip/item/topology retained transactions | branch-level tests |
| box push / flip / item | `BoxActionMovement` presentation must remain | grid transaction canary |
| topology materialization | `TopologyMaterialization` retained | topology scenario tests |
| spawn / respawn placement | placement, not ordinary locomotion | `SpawnRespawnPlacement` tests |
| cleanup removal | lifecycle removal | cleanup event tests |
| scripted relocation / phase relocation | `ScriptedRelocation` retained | phase relocation canary |
| continuous/kinematic anchor normalization | `LocomotionAnchorCommit` with suppressed legacy motion | no legacy presentation canary |
| explicit historical baseline | rollback/golden comparison until preset cleanup policy changes | dedicated removed-fallback diagnostics |

## Canary Coverage

Current canaries are sufficient for default bundle representative no-legacy checks, retained grid transactions, flag-off fallback, jump/phase basic boundaries, and boundary metadata hash exclusion. v3 adds:

- `DeprecationPhase1_DefaultGameplayLocomotion_PlayerEnemyCharge_NoLegacyFallback`
- `DeprecationPhase1_DefaultGameplayLocomotion_GlideFallback_IsRetainedException`
- `DeprecationPhase1_ExplicitGlideFlag_NoLegacyFallback`
- `DeprecationPhase1_LegacyBaseline_PlayerEnemyChargeRemoved`
- `DeprecationPhase1_GridTransactionsRemainAllowed`
- `DeprecationPhase1_MoveEntityPrimitiveStillAllowed`
- `DeprecationPhase1_MovementExpanderGridBranchStillAllowed`
- `DeprecationPhase1_NoCoveredLocomotionLegacyPresentation`
- `Replay_DeprecationPhase1_DefaultGameplayLocomotion_NoCoveredLegacyFallback`
- `Replay_DeprecationPhase1_GlideRetainedException_IsDeterministic`
- `BoundaryInventory_Glide_NoLegacyOrdinaryMoveLeak`
- `BoundaryInventory_DefaultGameplayLocomotion_GlideActiveKinematic`
- `BoundaryInventory_GlideFlagOff_FallbackStillRetained`
- `ExplicitGlideFlag_ActiveGlide_NoLegacyOrdinaryMove`
- `DefaultGameplayLocomotion_IncludesGlideKinematic`
- `Replay_DefaultGameplayLocomotion_GlideKinematic_IsDeterministic`
- `BoundaryInventory_ForcedMotion_NoRuntimeProducerYet`
- `BoundaryInventory_SpecialMovement_ReplayCanary`
- `LegacyOrdinaryUnitMovement_DeprecationReadiness_Report` v3 inventory assertions

The glide no-leak canary is intentionally limited to the state-only start tick under `DefaultGameplayLocomotion`. A separate default active-glide canary documents that the default bundle includes glide and uses the kinematic path. The explicit active-glide canary proves the standalone opt-in flag resolves the legacy ordinary blocker. The flag-off canary preserves the rollback baseline. The mega-canary remains intentionally split. Separate canaries give clearer failure ownership across player ordinary, enemy ordinary, charge, grid transactions, jump, phase, glide, and future forced motion.

## Deletion Readiness Checklist

| criterion | status | v3 decision |
|---|---|---|
| default bundle exists | complete | locked |
| default bundle targeted validation green | complete | targeted results recorded outside broad failures |
| player ordinary Free2D stable | complete for targeted | not sufficient alone for deletion |
| enemy ordinary kinematic stable | complete for targeted | continue canary |
| charge kinematic stable | complete for targeted | continue canary |
| no-legacy ordinary canary green | complete | special replay canary added |
| covered locomotion fallback isolated | complete | default/flag-on player ordinary, enemy ordinary, and Charge active fallback leaks are blocked |
| grid transaction allowlist green | complete | retained |
| explicit fallback baseline green | complete after Phase 6 | player, enemy, and Charge fallback removed; `RemovedLegacyFallbackDiagnosticBaseline` retained for deterministic diagnostics |
| jump inventory complete | complete | replay canary added |
| phase inventory complete | complete | replay canary added |
| glide kinematic explicit flag-on stable | complete | v1.1 targeted coverage resolves the explicit active glide blocker |
| default bundle glide adoption | complete | `DefaultGameplayLocomotion` includes active glide kinematic |
| glide inventory complete | partial | state-only start, explicit active kinematic, and default active kinematic are covered; flag-off active fallback remains a documented deletion blocker |
| forced motion inventory complete | complete for no-producer path | no runtime feature |
| Unknown boundary policy complete | complete representative | split canaries retained |
| replay/golden policy complete | partial | deletion still requires policy |
| full suite failure buckets documented | partial | unrelated failures reported separately |
| special movement risk resolved | partial | flag-off fallback deletion and replay/golden migration policy remain separate |
| actual deletion plan ready | blocked | unblock only after replay/golden policy is approved and a separate scoped deletion change is reviewed |

Actual deletion remains blocked.

## Actual Deletion Plan Draft

| phase | scope | precondition | rollback | tests | blockers | non-goals |
|---|---|---|---|---|---|---|
| Phase 1 | covered locomotion fallback isolation | player/enemy/Charge/glide kinematic lanes stable | keep legacy branches | Phase 1 boundary/replay canaries | flag-off glide fallback remains | no deletion |
| Phase 2 | glide default adoption | Glide Default Adoption v2 complete | set glide flag off or use `None` | default policy, host, replay policy tests | broad/default adoption gate | no fallback removal |
| Phase 3 | explicit fallback policy | scoped tests green | keep diagnostic compatibility preset after Phase 6 | replay suite | historical removed-diagnostic dependency | no auto baseline update |
| Phase 4 | player legacy ordinary fallback branch removal | player free2D/kinematic green, goldens migrated | restore fallback branch | player scenario/replay | explicit baseline retirement unresolved | no grid branch deletion |
| Phase 5 | enemy legacy ordinary fallback branch removal | enemy kinematic green | restore fallback branch | enemy scenario/replay | EnemyAi historical failures unclear | no charge fallback deletion |
| Phase 6 | legacy Charge fallback removal | Charge kinematic green | restore Charge legacy fallback | charge targeted/replay | charge goldens | no non-charge movement |
| Phase 7 | glide fallback deletion decision | Phase 2/3 policy resolved | keep explicit glide flag off rollback path | glide boundary/replay/showcase canaries | flag-off active glide fallback policy unresolved | no hidden fallback deletion |
| Phase 8 | final cleanup and canary enforcement | all deletion phases green | relax enforcement temporarily | no-legacy mandatory canaries | broad suite masking | no unrelated suite cleanup |

## Phase 2 Decision Prep

| option | direction | enables | blocks / risk | Phase 1 decision |
|---|---|---|---|---|
| Option A | include `EnableEnemyGlideKinematicLocomotion` in `DefaultGameplayLocomotion` | full ordinary fallback deletion can be reevaluated with glide no longer retained by default | default gameplay behavior shift, host policy churn, replay/golden migration | chosen in default adoption v2 |
| Option B | keep glide as a retained exception | historical fallback baseline remains available through flag-off/`None` | flag-off active glide fallback remains outside deletion target | retained only as rollback baseline |

## Risk Register

| risk | mitigation |
|---|---|
| special movement misclassified as safe | v3 inventory requires state carrier, boundary, presentation, hash, tests |
| glide has hidden displacement path | `BoundaryInventory_Glide_NoLegacyOrdinaryMoveLeak` covers state-only start; `ExplicitGlideFlag_ActiveGlide_NoLegacyOrdinaryMove` covers explicit flag-on active chase; `BoundaryInventory_DefaultGameplayLocomotion_GlideActiveKinematic` documents default adoption; v1.1 contact, LandingPending, active-end, hit/death, provenance, and replay tests cover stabilization; `BoundaryInventory_GlideFlagOff_FallbackStillRetained` preserves the rollback baseline |
| forced motion appears later without boundary kind | no-producer canary and ADR rule requiring a boundary before feature implementation |
| default bundle applied too broadly | `GameplaySceneHostConfiguration`, bootstrapper, composition root, and replay defaults stay `None` |
| replay/golden accidental migration | explicit bundle only in canary replays |
| deleting fallback before special inventory complete | Phase 0 gate blocks deletion |
| grid transaction accidentally deleted | retained path table + grid transaction canary |
| `MoveEntity` misuse mistaken for legacy movement | Phase 1 helpers assert boundary kind and presentation, not operation kind alone |
| `MovementExpander` removal attempted too early | retained path table and `DeprecationPhase1_MovementExpanderGridBranchStillAllowed` keep the branch explicit |
| docs say deletion but runtime only isolates | Phase 1 status is documented as isolation complete / deletion pending |
| full suite unrelated failures hide deletion regression | report targeted readiness tests separately |
| docs inventory diverges from runtime | v3 report test mirrors key inventory labels |

## Validation

Minimum post-change validation:

- `dotnet build Game.Feature.Gameplay.Tests.csproj -c Debug --no-restore`
- Unity XML `TestResults/phase1-simulation.xml`: `BoundaryInventoryScenarioTests`, `MovementPhaseScenarioTests`, `PlayerContinuousLocomotionScenarioTests`, `PlayerKinematicLocomotionScenarioTests`, and enemy ordinary/charge `EnemyAiScenarioTests` subset
- Unity XML `TestResults/phase1-replay.xml`: `EnemyKinematicLocomotionReplayTests` and `PlayerContinuousLocomotionReplayTests`
- `git diff --check`

If a broad suite is red from unrelated failures, report v3 readiness tests separately from existing unrelated rows.
