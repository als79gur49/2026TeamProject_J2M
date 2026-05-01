# Legacy Ordinary Unit Movement Deprecation Readiness v1

Date: 2026-05-01

This readiness pass does not delete legacy movement. It inventories the remaining ordinary Unit movement fallback paths, keeps grid transaction paths explicitly retained, and adds canaries that must stay green before any later deletion pass.

## Definitions

- Legacy ordinary Unit locomotion: an ordinary Unit `MovementCommandKind.Move` reaches `MovementExpander`, commits through `MoveEntity`, and presents as legacy `TickEntityMotionKind.Move` or legacy `TickEntityMotionKind.ChargeMove`.
- Unit continuous locomotion: player ordinary Free2D movement stored in `UnitContinuousLocomotionState` and presented through `TickContinuousLocomotionTrack`.
- Unit kinematic locomotion: segment-progress Unit movement stored in `UnitKinematicRuntimeState` and presented through `TickKinematicMotionTrack`.
- Unit special locomotion: jump, phase, glide, charge, forced motion, and knockback style Unit movement that is not ordinary locomotion.
- Grid transaction: immediate canonical grid, anchor, placement, removal, topology, box/action, or scripted relocation materialization. `MoveEntity` remains valid here.
- Legacy fallback: flag-off rollback, golden baseline, and historical tests that intentionally keep the old ordinary path.

## Inventory

| path | entity | boundary | flag condition | presentation | remain | deprecation target | replacement |
|---|---|---|---|---|---|---|---|
| player Free2D ordinary | Unit/player | `UnitOrdinaryLocomotion`, anchor `LocomotionAnchorCommit` | `EnablePlayerFree2DLocalLocomotion` | `TickContinuousLocomotionTrack` | yes | no | target default |
| player kinematic fallback | Unit/player | `UnitOrdinaryLocomotion`, anchor `LocomotionAnchorCommit` | free2D off, kinematic on | `TickKinematicMotionTrack` | yes | no | fallback |
| player legacy fallback | Unit/player | `LegacyFallback` | player locomotion flags off | `TickEntityMotionKind.Move` | temporary | yes | Free2D/kinematic |
| enemy ordinary kinematic | Unit/enemy | `UnitOrdinaryLocomotion`, anchor `LocomotionAnchorCommit` | `EnableEnemySameFaceContinuousLocomotion` | `TickKinematicMotionTrack` | yes | no | target default |
| enemy legacy fallback | Unit/enemy | `LegacyFallback` | enemy kinematic off | `TickEntityMotionKind.Move` | temporary | yes | enemy kinematic |
| charge kinematic | Unit/enemy | `UnitSpecialLocomotion`, anchor `LocomotionAnchorCommit` | `EnableEnemyChargeKinematicLocomotion` | kinematic track + charge signal | yes | no | target default |
| charge legacy fallback | Unit/enemy | `LegacyFallback` | charge kinematic off | `TickEntityMotionKind.ChargeMove` | temporary | yes | charge kinematic |
| jump landing | Unit/enemy | `UnitSpecialLocomotion` | jump skill | jump presentation | yes | no | future special inventory |
| phase relocation | Unit/enemy | `ScriptedRelocation` | phase skill | retained relocation | yes | no | keep scripted relocation |
| glide | Unit/enemy | state-only special inventory | glide skill | state/signal only | yes | no | future special inventory |
| forced/knockback | Unit/future | none yet | n/a | none | n/a | no current path | future special state |
| box push/flip/slide/impact | Box/Unit | `BoxActionMovement` | any | retained entity motion | yes | no | none |
| item movement | Box item | `BoxActionMovement` | any | retained item/move motion | yes | no | none |
| topology transition | Unit/player | `TopologyMaterialization` | any | topology motion retained | yes | no | none |
| spawn/respawn | Unit | `SpawnRespawnPlacement` | any | placement/visibility | yes | no | none |
| cleanup/removal | any | cleanup phase result/event | any | exit/cleanup signal | yes | no | none |
| anchor normalization | Unit | `LocomotionAnchorCommit` | locomotion flags on | suppressed legacy move | yes | no | none |

## Flag Defaults

`GameplayRuntimeFeatureFlags.None` remains the flag-off baseline. `GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion` is the explicit default-on gameplay bundle for readiness canaries and rollout hosts:

- `EnablePlayerFree2DLocalLocomotion`
- `EnablePlayerFree2DActionAssist`
- `EnablePlayerSameFaceContinuousLocomotion`
- `EnablePlayerStoppableKinematicLocomotion`
- `EnableEnemySameFaceContinuousLocomotion`
- `EnableEnemyChargeKinematicLocomotion`

Existing tests that rely on default struct behavior must keep passing explicit `None`. Replay and golden baselines keep flag-off cases until the deletion phase defines a replacement policy.

## Deletion Readiness Checklist

| criterion | v1 status |
|---|---|
| flag-on player/enemy/charge no legacy ordinary diagnostic | partial, covered by `BoundaryInventory_PlayerEnemyCharge_NoLegacyOrdinaryUnitMovement` |
| player ordinary default is Free2D | partial, explicit default helper exists |
| enemy ordinary default is kinematic | partial, explicit default helper exists |
| charge default is kinematic | partial, explicit default helper and showcase flag exist |
| grid transaction allowlist green | partial, covered by `BoundaryInventory_GridTransactionsRemainAllowed` |
| jump/phase/glide inventory complete | partial, covered by `BoundaryInventory_SpecialMovement_JumpPhaseGlide_ClassifiedOrReported` |
| meaningful `Unknown` movement boundary closed | partial, distributed canaries retained |
| flag-off fallback policy documented | partial, covered by `BoundaryInventory_FlagOff_LegacyFallbackStillAllowed` |
| replay/golden policy documented | partial, existing rollout docs retained |
| deletion report fails on new ordinary path | partial, `LegacyOrdinaryUnitMovement_DeprecationReadiness_Report` records the allowlist |

## Validation

Minimum post-change validation:

- `dotnet build Game.Feature.Gameplay.Tests.csproj -c Debug --no-restore`
- targeted boundary inventory tests
- targeted player Free2D tests
- targeted enemy kinematic and charge tests
- targeted replay no-legacy tests
- `git diff --check`

If a broad suite is red from unrelated existing failures, report the readiness tests separately from the unrelated failures.
