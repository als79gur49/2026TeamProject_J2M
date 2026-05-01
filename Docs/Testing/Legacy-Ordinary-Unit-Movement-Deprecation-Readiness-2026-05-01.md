# Legacy Ordinary Unit Movement Deprecation Readiness v2

Date: 2026-05-01

This readiness pass does not delete legacy movement. It extends the previous boundary inventory to cover `DefaultGameplayLocomotion` host adoption, special movement classification, no-legacy canaries, and `Boundary=Unknown` policy. `MoveEntity` remains the anchor/grid transaction primitive and `MovementExpander` remains retained for grid transactions and flag-off fallback.

## Executive Decision

`GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion` is the explicit default-on gameplay bundle for normal gameplay adoption. `GameplayRuntimeFeatureFlags.None` and default struct behavior remain the flag-off rollback, golden, historical, and legacy fallback baseline. Replay harnesses must keep `None` by default; only explicit flag-on replay tests should opt into the bundle.

Jump is `UnitSpecialLocomotion`. Phase relocation remains `ScriptedRelocation`. Glide is currently a state-only special movement candidate and future kinematic migration candidate. Forced motion and knockback do not have a runtime state in this slice and are documented as future special movement requirements.

## Host Adoption Inventory

| host / installer / harness | current flag source | uses bundle | should use bundle | preserve explicit flags | recommendation |
|---|---|---:|---:|---:|---|
| `CombinedGameplayShowcaseInstaller` | explicit gameplay showcase config | yes | yes | tuning values only | call `ApplyRuntimeFeatureFlags(DefaultGameplayLocomotion)` and preserve action assist/radius tuning |
| `StageBackedGameplayShowcaseInstallerBase` | no base locomotion policy | no | no | yes | do not apply bundle globally in the base class |
| campaign scene host | installer config | no | later opt-in | yes | keep `None` until Phase B opt-in |
| editor direct play | stage-backed path | no | follows campaign opt-in | yes | no implicit default |
| `GameplaySceneHostConfiguration` | authored bool fields | explicit helper | no implicit default | yes | helper only; constructor/default fields stay false |
| `GameplayHostRuntimeFactory` | `CreateRuntimeFeatureFlags()` passthrough | no | no policy decision | yes | keep passthrough |
| `GameplayBootstrapper` / `GameplayCompositionRoot` | optional default parameter | no | no | yes | default parameter remains `default`/`None` |
| scenario factories | explicit per test | partial | representative only | yes | only readiness canaries use bundle |
| replay harness | optional default parameter | no | explicit test only | yes | keep default `None` |
| playmode host | `GameplaySceneHostConfiguration` | no | later opt-in | yes | defer broad default-on |

## Rollout Policy

| phase | scope | expected impact | rollback | golden policy | required canary |
|---|---|---|---|---|---|
| A | docs + `CombinedGameplayShowcaseInstaller` | showcase only | remove helper call | no flag-off golden change | host config + boundary inventory |
| B | campaign/dev host opt-in | gameplay scenes opt in explicitly | opt-in false | goldens remain `None` | host smoke + boundary inventory |
| C | representative scenario/replay tests | explicit bundle tests | tests return to per-flag helpers | no harness default change | default bundle no-legacy replay |
| D | broad gameplay default-on | normal gameplay default lane changes | host policy off | replay/golden excluded | full targeted locomotion suite |
| E | legacy ordinary fallback test-only | deletion-readiness only | restore fallback policy | deletion phase defines policy | deletion plan, not this slice |

## Special Movement Inventory v2

| movement | current runtime path | boundary kind | uses `MoveEntity` | uses `MovementExpander` | presentation source | hash/replay state | ordinary deletion risk | classification | future migration |
|---|---|---|---:|---:|---|---|---|---|---|
| jump start | `EnemyLogic.CommitJumpState` / `SetEnemyJumpState` | `UnitSpecialLocomotion` | no | no | jump state presentation | jump state | low | `UnitSpecialLocomotion` | none for deletion |
| jump airborne | jump state + detached board presence | `UnitSpecialLocomotion` | no | no | jump state/presence | jump state | low | `UnitSpecialLocomotion` | none for deletion |
| jump landing | jump landing contest resolution | `UnitSpecialLocomotion` | yes | no | jump landing presentation | position + jump state | low | `UnitSpecialLocomotion` | possible future kinematic |
| phase relocation | phase plan payload commit | `ScriptedRelocation` | yes | no | retained relocation/entity motion | position + phased state | low | `ScriptedRelocation` | keep scripted relocation |
| glide | `EnemyGlideRuntimeState` lifecycle | state-only candidate | no current relocation | no | state/signal only | glide state | medium if future displacement leaks | `Future Kinematic Migration Candidate` | document only now |
| forced motion / knockback | no explicit runtime state | none | no current path | no | none / presentation-only candidate | none | future risk | `Unknown / Needs Classification` | define state before implementation |
| scripted relocation | scripted/phase finalization payload | `ScriptedRelocation` | yes | no | entity motion/trace | position | low | `ScriptedRelocation` | none |
| topology transition | movement group topology materialization | `TopologyMaterialization` | yes | yes, allowed grid path | topology motion | topology + position | none | `GridTransaction` | none |
| cleanup removal | cleanup processor/result | cleanup event/result | no movement commit | no | cleanup/event/visibility | entity removal | none | cleanup grid lifecycle | none |

## Boundary Unknown Policy

Acceptable `Unknown` is limited to test-only synthetic operations, non-movement diagnostics, intentionally unclassified debug-only records, and state-only operations that do not change an anchor and do not source movement presentation.

Unacceptable `Unknown` includes any `MoveEntity` anchor change, movement presentation source, grid transaction, unit special movement, spawn, respawn, topology, scripted relocation, or ordinary Unit locomotion finalization.

Representative readiness tests must fail on unacceptable `Unknown`. Boundary metadata remains diagnostic and must not be included in canonical determinism hashes.

## Canary Coverage

- `BoundaryInventory_DefaultGameplayLocomotion_NoLegacyOrdinaryUnitMovement`
- `BoundaryInventory_GridTransactionsRemainAllowed_UnderDefaultGameplayLocomotion`
- `BoundaryInventory_FlagOff_LegacyFallbackStillAllowed`
- `BoundaryInventory_SpecialMovement_Jump_IsUnitSpecialLocomotion`
- `BoundaryInventory_PhaseRelocation_IsScriptedRelocation`
- `BoundaryInventory_Glide_IsReportedSpecialCandidate`
- `BoundaryInventory_ForcedMotion_IsReportedOrAbsent`
- `BoundaryInventory_NoUnexpectedUnknownMovement_Representative`
- `HostConfiguration_DefaultGameplayLocomotion_AppliesExpectedFlags`
- `Replay_DefaultGameplayLocomotion_NoUnexpectedLegacyOrdinaryMovement`

## Deletion Readiness Checklist

| criterion | v2 status |
|---|---|
| default bundle exists | complete |
| default bundle verified by tests | complete for host/helper and representative canaries |
| player ordinary Free2D stable | partial |
| enemy ordinary kinematic stable | partial |
| charge kinematic stable | partial |
| no-legacy ordinary canary green | partial pending validation |
| grid transaction allowlist green | partial pending validation |
| flag-off fallback baseline green | partial pending validation |
| jump inventory complete | complete for readiness |
| phase inventory complete | complete for readiness |
| glide inventory complete | partial, state-only candidate documented |
| forced motion inventory complete | partial, absent/future requirement documented |
| Unknown boundary policy complete | complete for representative policy |
| replay/golden policy complete | complete for default harness policy |
| full suite failure buckets documented | pending validation report |
| actual deletion plan ready | blocked, out of scope |

## Validation

Minimum post-change validation:

- `dotnet build Game.Feature.Gameplay.Tests.csproj -c Debug --no-restore`
- targeted `BoundaryInventoryScenarioTests`
- targeted `MovementPhaseScenarioTests`
- targeted player Free2D tests
- targeted enemy kinematic and charge tests
- targeted no-legacy replay canaries
- `git diff --check`

If the broad suite is red from unrelated failures, report readiness tests separately from unrelated failures.
