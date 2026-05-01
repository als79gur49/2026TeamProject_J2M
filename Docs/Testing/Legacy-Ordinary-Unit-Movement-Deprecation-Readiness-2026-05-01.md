# Legacy Ordinary Unit Movement Deprecation Readiness v3

Date: 2026-05-01

This readiness pass does not delete legacy movement. It closes the `Special Movement Inventory v3`, adds deletion-readiness canary expectations, and drafts the future `Legacy Ordinary Unit Movement` deletion phases. `MoveEntity` remains the anchor/grid transaction primitive. `MovementExpander` remains retained for grid transactions and flag-off fallback.

## Executive Decision

`Legacy Ordinary Unit Movement` is the only deletion target: an ordinary `Unit` `MovementCommandKind.Move` reaches legacy expansion, commits through `MoveEntity`, and presents as legacy `TickEntityMotionKind.Move` or `TickEntityMotionKind.ChargeMove`.

`Legacy Grid Transaction` is retained: `MoveEntity` can still materialize box, topology, spawn, respawn, cleanup, scripted relocation, and anchor-normalization semantics when those paths are not ordinary Unit locomotion. `DefaultGameplayLocomotion` is an explicit readiness/adoption bundle, not deletion. `GameplayRuntimeFeatureFlags.None` and default struct behavior remain rollback, golden, historical, and flag-off fallback baselines.

Jump and phase relocation are not deletion blockers. Glide is state-only today and must not leak into legacy ordinary movement. Forced motion and knockback have runtime vocabulary through `MotionMode.Forced` and `ForcedMotionOp.Knockback`, but no gameplay producer in this readiness slice; any future producer must define a special/kinematic boundary before shipping.

## Current Validated Readiness

| area | status | decision |
|---|---|---|
| Boundary v1 | complete | `LocomotionAnchorCommit` and `UnitOrdinaryLocomotion` remain the suppression boundaries |
| `DefaultGameplayLocomotion` bundle | complete | explicit opt-in bundle only |
| targeted readiness canary | complete | targeted green is recorded as a deletion precondition, not deletion approval |
| full `EnemyAiScenarioTests` historical failures | partial | separated from deletion readiness unless same-row behavior regresses |
| no-legacy ordinary canary | complete for representative coverage | special replay coverage added before deletion |
| special inventory | partial for v3 | forced/knockback closed as no gameplay producer; glide active fallback remains documented risk |
| actual deletion | blocked | no fallback removal until v3 docs/tests are green, glide active policy is decided, and replay/golden policy is decided |

## Definitions

- `Legacy Ordinary Unit Movement`: ordinary `Unit` `MovementCommandKind.Move` -> `MovementExpander` -> immediate `MoveEntity` anchor change -> legacy `TickEntityMotionKind.Move` or charge legacy `ChargeMove` presentation.
- `Legacy Grid Transaction`: retained semantic grid transaction that may use `MoveEntity` but is not ordinary Unit locomotion.
- `Unit Continuous Locomotion`: player ordinary movement backed by `UnitContinuousLocomotionState`.
- `Unit Kinematic Locomotion`: segment movement backed by `UnitKinematicRuntimeState`, including enemy ordinary, player fallback, and charge active steps.
- `Unit Special Locomotion`: non-ordinary unit movement such as charge, jump, phase-like movement, glide, and future forced/knockback.
- `Legacy Fallback`: flag-off, rollback, historical, and golden baseline path kept by policy until the deletion phase changes it explicitly.

## DefaultGameplayLocomotion Adoption

| host / installer / harness | current flag source | uses bundle? | should use bundle now? | preserve `None`? | risk | tests | recommendation |
|---|---|---:|---:|---:|---|---|---|
| `CombinedGameplayShowcaseInstaller` | explicit config hook | yes | yes | no | showcase-only behavior shift | `CombinedGameplayShowcaseInstaller_Configuration_UsesDefaultGameplayLocomotionBundle` | keep bundle |
| `StageBackedGameplayShowcaseInstallerBase` | base config | no | no | yes | over-broad scene opt-in | host config tests | do not apply globally |
| campaign scene host | installer/config authored flags | no/partial | later | yes | campaign behavior drift | playmode smoke later | keep explicit opt-in only |
| editor direct play | scene config defaults | no | no | yes | hidden default-on | host default tests | keep `None` unless authored |
| `GameplaySceneHostConfiguration` default | bool fields false | helper only | no implicit default | yes | baseline drift | `HostConfiguration_DefaultGameplayLocomotion_AppliesExpectedFlags` | default constructor remains `None` |
| `GameplayHostRuntimeFactory` | `configuration.CreateRuntimeFeatureFlags()` | passthrough | no policy | yes | hidden policy in factory | runtime factory tests | keep passthrough |
| `GameplayBootstrapper` / `GameplayCompositionRoot` | optional `default` parameter | no | no | yes | replay/golden break | structure/core tests | keep default `None` |
| scenario factories | explicit per test | partial | readiness scenarios only | yes | accidental broad migration | targeted scenarios | explicit bundle only |
| replay harness | explicit optional flags | no default bundle | explicit canaries only | yes | golden migration | replay tests | default `None` |
| playmode host | scene config | no | later opt-in | yes | user-facing drift | playmode host smoke | defer broad default-on |

## Special Movement Inventory v3

| movement | current runtime path | current state carrier | boundary kind | uses `MoveEntity`? | uses `MovementExpander`? | presentation source | hash/replay state | ordinary deletion risk | current classification | future migration need | tests covering it | recommended next action |
|---|---|---|---|---:|---:|---|---|---|---|---|---|---|
| jump start | `EnemyLogic.CommitJumpState` -> `SetEnemyJumpState` | `EnemyJumpRuntimeState(Windup)` | `UnitSpecialLocomotion` | no | no | jump state presentation | enemy jump state hash included | low | Safe UnitSpecialLocomotion | none for deletion | `BoundaryInventory_SpecialMovement_Jump_IsUnitSpecialLocomotion` | keep documented |
| jump airborne | jump state transition + detached presence | `EnemyJumpRuntimeState(Airborne)` + board presence | `UnitSpecialLocomotion` | no | no | jump state/presence | jump state + presence | low | Safe UnitSpecialLocomotion | none for deletion | jump inventory canary | keep documented |
| jump landing | `ResolvePlanJumpLandings` / landing contest | `EnemyJumpRuntimeState` + position | `UnitSpecialLocomotion` | yes | no | jump landing presentation | position + jump state | low | Safe UnitSpecialLocomotion | possible future kinematic, not deletion blocker | jump scenario + replay canary | retain |
| phase relocation | `ResolvePlanEnemyPhaseRelocations` | phase relocation payload + phased state | `ScriptedRelocation` | yes | no | retained relocation/entity motion | position + phased state | low | Safe Retained Grid Transaction | none before deletion | phase relocation canary + replay canary | retain |
| phase state lifecycle | phased state enter/sustain/exit | `PhasedRuntimeState` | state-only or `ScriptedRelocation` when relocating | relocation only | no | state/trace | phased state hash | low | Safe Retained Grid Transaction | none | phase scenario tests | document non-ordinary lifecycle |
| glide start | `EnemyLogic.CommitGlideState` | `EnemyGlideRuntimeState(Windup/Active)` | state-only | no | no | `TickEnemyGlidePresentationSignal` face-normal lift | enemy glide state hash included; glide presentation excluded | low for start tick | State-only Special Candidate | none for start tick | `BoundaryInventory_Glide_NoLegacyOrdinaryMoveLeak`, `GlidePresentation_Windup_RisesAlongFaceNormal` | keep state-only start canary |
| glide active/state-only lifecycle | glide ticking/target suppression plus opt-in active chase kinematic path | `EnemyGlideRuntimeState(Active/LandingPending/Recovery/Cooldown)` + optional `UnitKinematicRuntimeState(MotionMode.Voluntary)` | flag-on active chase uses `LocomotionAnchorCommit`; flag-off keeps documented `LegacyFallback` ordinary move | flag-on no; flag-off yes | flag-on no; flag-off yes | flag-on horizontal `TickKinematicMotionTrack` plus additive `TickEnemyGlidePresentationSignal` height; flag-off legacy horizontal move baseline retained | glide state and UnitKinematics hash included; glide presentation and boundary metadata excluded | medium until default adoption | Flag-Gated Kinematic Candidate | keep explicit flag-on before deletion; default bundle adoption pending | `BoundaryInventory_Glide_ActiveKinematic_NoLegacyOrdinaryMove`, `BoundaryInventory_Glide_ActiveLegacyFallback_FlagOff_IsDocumented`, `GlideActive_Kinematic_PreservesSolidBypass`, `Replay_EnemyGlideActiveKinematicLocomotion_IsDeterministic` | do not delete flag-off baseline; include glide flag in default bundle only after readiness approval |
| glide end | recovery/cooldown/clear | `EnemyGlideRuntimeState` | state-only | no | no | `TickEnemyGlidePresentationSignal` recovery/terminal zero | glide state hash/clear; glide presentation excluded | low | State-only Special Candidate | none before deletion | `GlidePresentation_Recovery_DescendsAndSupportsDip`, `GlidePresentation_Clear_DoesNotLeaveStaleHoverSignal` | keep presentation-only stale-clear coverage |
| forced motion | no gameplay producer; vocabulary exists | `MotionMode.Forced`, `ForcedMotionOp` vocabulary | none today | no current path | no | none | no current gameplay footprint | future high | Future Runtime State Needed | explicit state producer + boundary before feature | `BoundaryInventory_ForcedMotion_NoRuntimeProducerYet` | keep canary |
| knockback | no gameplay producer; enum value exists | `ForcedMotionOp.Knockback` vocabulary | none today | no current path | no | none | no current gameplay footprint | future high | Future Runtime State Needed | explicit special/kinematic boundary before feature | forced inventory canary | keep canary |
| scripted relocation | phase/scripted finalization payload | relocation payload | `ScriptedRelocation` | yes | no | entity motion/trace | position | low | Safe Retained Grid Transaction | none | phase relocation canary | retain |
| topology transition | movement group topology materialization | topology + position | `TopologyMaterialization` | yes | yes, allowed grid branch | topology motion | topology + anchor | none | Safe Retained Grid Transaction | none | grid transaction canary + `MovementPhaseScenarioTests` | retain allowlist |
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
| Potential Legacy Ordinary Leak | glide active chase fallback | v3 canary confirms active glide can still produce `LegacyFallback` ordinary `MoveEntity`/`TickEntityMotionKind.Move` |
| Needs Tests | forced no-producer, jump/phase/glide replay | covered by v3 canaries |
| Needs Implementation Before Deletion | glide active fallback policy | ordinary fallback deletion must first decide whether active glide suppresses ordinary movement or receives explicit special/kinematic displacement |

## Legacy Deletion Candidates

| candidate path | file / method | current flag condition | current tests | replacement path | deletion risk | deletion phase | blocker |
|---|---|---|---|---|---|---|---|
| player legacy ordinary fallback | `TickPipeline` legacy `MoveIntent` expansion path for player `Move` | `GameplayRuntimeFeatureFlags.None` or no player locomotion flag | `BoundaryInventory_FlagOff_LegacyFallbackStillAllowed`, player kinematic/free2D tests | `UnitContinuousLocomotionState` / player kinematic fallback | golden/rollback break | Phase 4 | replay/golden migration policy |
| enemy legacy ordinary fallback | `TickPipeline.ValidateLegacyExpansionIntents` allowed fallback + `MovementExpander` ordinary Unit branch | enemy flags off | enemy ordinary fallback assertions | `UnitKinematicRuntimeState` enemy ordinary | historical EnemyAi baseline break | Phase 5 | enemy targeted + replay canaries |
| legacy charge active fallback | charge active `MoveEntity` -> `TickEntityMotionKind.ChargeMove` | `EnableEnemyChargeKinematicLocomotion=false` | charge fallback tests, replay charge canary | charge kinematic `MotionMode.Charge` | presentation/golden break | Phase 6 | charge migration policy |
| stale ordinary Unit `Move` test helpers | scenario/replay helpers asserting Unit ordinary `TickEntityMotionKind.Move` | test-dependent | `MovementPhaseScenarioTests`, `EnemyAiScenarioTests` rows | explicit continuous/kinematic or retained grid expectations | retained path over-delete | Phase 7 | candidate inventory grep |
| presentation-only legacy Unit motion generation | `TickResultBuilder.TryResolveMotionKind` for Unit ordinary `Move` when not retained grid | global presentation builder | presentation unit tests | retained-grid or test-only fallback only | hidden grid presentation regression | Phase 7 | retained-path allowlist tests |

## Retained Paths

| retained path | why retained | deletion guard |
|---|---|---|
| `MoveEntity` primitive | anchor/grid transaction primitive | never delete in ordinary fallback phase |
| `MovementExpander` grid transaction branch | push/flip/item/topology and flag-off baseline | branch-level tests |
| box push / flip / item | `BoxActionMovement` presentation must remain | grid transaction canary |
| topology materialization | `TopologyMaterialization` retained | topology scenario tests |
| spawn / respawn placement | placement, not ordinary locomotion | `SpawnRespawnPlacement` tests |
| cleanup removal | lifecycle removal | cleanup event tests |
| scripted relocation / phase relocation | `ScriptedRelocation` retained | phase relocation canary |
| continuous/kinematic anchor normalization | `LocomotionAnchorCommit` with suppressed legacy motion | no legacy presentation canary |
| flag-off historical baseline | rollback/golden until policy changes | dedicated fallback tests |

## Canary Coverage

Current canaries are sufficient for default bundle representative no-legacy checks, retained grid transactions, flag-off fallback, jump/phase basic boundaries, and boundary metadata hash exclusion. v3 adds:

- `BoundaryInventory_Glide_NoLegacyOrdinaryMoveLeak`
- `BoundaryInventory_Glide_ActiveLegacyFallback_FlagOff_IsDocumented`
- `BoundaryInventory_Glide_ActiveKinematic_NoLegacyOrdinaryMove`
- `BoundaryInventory_ForcedMotion_NoRuntimeProducerYet`
- `BoundaryInventory_SpecialMovement_ReplayCanary`
- `LegacyOrdinaryUnitMovement_DeprecationReadiness_Report` v3 inventory assertions

The glide no-leak canary is intentionally limited to the state-only start tick. A separate active-glide canary documents the current ordinary fallback leak so deletion readiness does not mistake glide for fully state-only movement. The mega-canary remains intentionally split. Separate canaries give clearer failure ownership across player ordinary, enemy ordinary, charge, grid transactions, jump, phase, glide, and future forced motion.

## Deletion Readiness Checklist

| criterion | status | v3 decision |
|---|---|---|
| default bundle exists | complete | locked |
| default bundle targeted validation green | complete | targeted results recorded outside broad failures |
| player ordinary Free2D stable | complete for targeted | not sufficient alone for deletion |
| enemy ordinary kinematic stable | complete for targeted | continue canary |
| charge kinematic stable | complete for targeted | continue canary |
| no-legacy ordinary canary green | complete | special replay canary added |
| grid transaction allowlist green | complete | retained |
| flag-off fallback baseline green | complete | preserved until policy phase |
| jump inventory complete | complete | replay canary added |
| phase inventory complete | complete | replay canary added |
| glide inventory complete | partial | state-only start is covered; active chase fallback is a documented deletion blocker |
| forced motion inventory complete | complete for no-producer path | no runtime feature |
| Unknown boundary policy complete | complete representative | split canaries retained |
| replay/golden policy complete | partial | deletion still requires policy |
| full suite failure buckets documented | partial | unrelated failures reported separately |
| special movement risk resolved | partial | glide active fallback must be resolved before ordinary fallback deletion |
| actual deletion plan ready | blocked | unblock only after v3 lands green, glide active policy is decided, and replay/golden policy is approved |

Actual deletion remains blocked.

## Actual Deletion Plan Draft

| phase | scope | precondition | rollback | tests | blockers | non-goals |
|---|---|---|---|---|---|---|
| Phase 0 | readiness inventory complete | v3 docs/tests green | revert docs/tests only | build, boundary, replay canaries | special inventory incomplete | no deletion |
| Phase 1 | runtime default bundle opt-in for normal gameplay hosts | host policy approved | disable config opt-in | host config + smoke | replay/golden policy | no composition-root default change |
| Phase 2 | flag-off fallback marked test-only | fallback test inventory complete | keep production flag-off | fallback tests | rollback owner unclear | no fallback removal |
| Phase 3 | replay/golden migration policy decided | golden owners approve | keep `None` goldens | replay suite | historical baseline dependency | no auto baseline update |
| Phase 4 | player legacy ordinary fallback removal | player free2D/kinematic green, goldens migrated | restore fallback branch | player scenario/replay | `None` policy unresolved | no grid branch deletion |
| Phase 5 | enemy legacy ordinary fallback removal | enemy kinematic green | restore fallback branch | enemy scenario/replay | EnemyAi historical failures unclear | no charge fallback deletion |
| Phase 6 | legacy charge fallback removal | charge kinematic green | restore charge legacy fallback | charge targeted/replay | charge goldens | no non-charge movement |
| Phase 7 | obsolete legacy presentation tests cleanup | deletion complete | restore test expectations | presentation + movement tests | retained motion tests red | no retained grid tests removal |
| Phase 8 | final canary enforcement | all deletion phases green | relax enforcement temporarily | no-legacy mandatory canaries | broad suite masking | no unrelated suite cleanup |

## Risk Register

| risk | mitigation |
|---|---|
| special movement misclassified as safe | v3 inventory requires state carrier, boundary, presentation, hash, tests |
| glide has hidden displacement path | `BoundaryInventory_Glide_NoLegacyOrdinaryMoveLeak` covers state-only start; `BoundaryInventory_Glide_ActiveKinematic_NoLegacyOrdinaryMove` covers explicit flag-on active chase; `BoundaryInventory_Glide_ActiveLegacyFallback_FlagOff_IsDocumented` preserves the rollback baseline |
| forced motion appears later without boundary kind | no-producer canary and ADR rule requiring a boundary before feature implementation |
| default bundle applied too broadly | `GameplaySceneHostConfiguration`, bootstrapper, composition root, and replay defaults stay `None` |
| replay/golden accidental migration | explicit bundle only in canary replays |
| deleting fallback before special inventory complete | Phase 0 gate blocks deletion |
| grid transaction accidentally deleted | retained path table + grid transaction canary |
| full suite unrelated failures hide deletion regression | report targeted readiness tests separately |
| docs inventory diverges from runtime | v3 report test mirrors key inventory labels |

## Validation

Minimum post-change validation:

- `dotnet build Game.Feature.Gameplay.Tests.csproj -c Debug --no-restore`
- targeted `BoundaryInventoryScenarioTests`
- targeted `Replay_DefaultGameplayLocomotion_NoUnexpectedLegacyOrdinaryMovement`
- targeted special movement canaries
- targeted `MovementPhaseScenarioTests` if movement boundary code changes
- `git diff --check`

If a broad suite is red from unrelated failures, report v3 readiness tests separately from existing unrelated rows.
