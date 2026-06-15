# Enemy AI Current Structure Source

This document records the current Enemy AI authoring and runtime structure after
the Phase 1 behavior-module split. It is the supporting source for documentation
refreshes and stale-token audits. It is not a Phase 2 design.

## Profile Root

`EnemyAiProfile` has four root authoring lanes:

- `coreAuthoring`
- `brainAuthoring`
- `capabilityAssets`
- `behaviorModuleAssets`

`capabilityAssets` remains active. Phase 1 did not migrate Utility, Summon, or
all capabilities into behavior modules.

## Runtime Definition

`EnemyAiRuntimeDefinition` resolves through:

`EnemyAiProfile -> EnemyAiProfileCompiler -> EnemyAiRuntimeDefinition -> GameplayEntityLogicProviderFactory -> TickPipeline`

The runtime shape is:

- `Core`
  - common settings
  - locomotion timing
- `Brain`
  - state resolver
  - patrol
  - detection
  - chase
- `Capabilities`
  - combat
  - movement skill
  - passive contact
  - utility
- `Behaviors`
  - Charge behavior runtime

`Core` owns enemy-common values and locomotion only. Charge timing does not live
in the core lane.

## Charge Behavior

Charge is modeled as:

`resolver requirement + ChargeBehaviorModule execution profile + EnemyChargeRuntimeState + presentation/determinism lane`

The current Charge pieces are:

- `ChargingEnemyStateResolverAsset`
  - declares `RequiresChargeBehavior`
  - does not own an execution profile
- `EnemyChargeBehaviorModuleAsset`
  - owns the behavior-module entry
  - references an execution profile
- `EnemyChargeExecutionProfile`
  - reusable timing profile
  - converts authoring seconds to runtime ticks
- `EnemyChargeBehaviorRuntime`
  - runtime timing source for windup, active step cadence, and recover
- `EnemyChargeRuntimeState`
  - authoritative mutable charge state

The only current production Charge content is:

- `EnemyChargeExecutionProfile_Standard`
- `EnemyChargeBehaviorModule_Standard`

Future Charge timing variants remain allowed through the reusable
`EnemyChargeExecutionProfile` type and the
`EnemyChargeBehaviorModuleAsset -> EnemyChargeExecutionProfile` reference, but
variant assets must be created only when production content actually needs them.
Do not keep unused timing-only placeholder assets as supported archetypes.

The resolver decides mode-transition meaning. `EnemyLogic` combines resolver
decisions with `EnemyChargeBehaviorRuntime` timing and commits authoritative
charge state.

## Capability Boundary

Capabilities are still bounded semantic lanes:

- Combat
- MovementSkill
- PassiveContact
- Utility

Summon remains in the Utility capability path. GravityFieldAura remains a
Utility/board-modifier style capability. Phase 1 does not deprecate Utility and
does not remove `capabilityAssets`.

## BehaviorModule Boundary

Behavior modules are for stateful or special behavior execution that is coupled
to an AI mode/resolver requirement. Phase 1 implements only Charge.

`EnemyBehaviorRuntimeSet` intentionally stays as fixed typed slots while Charge
is the only behavior module. It is not a generic dictionary registry.

## Phase 2 Boundary

A unified logic-module asset root is reserved as a possible Phase 2 umbrella if
capability and behavior-module lanes later prove to duplicate the same tick seam.
It is not part of the current contract.

Do not start Phase 2 until one of these triggers is observed:

- Behavior module keys grow to at least three total keys.
- Shield or Summon actually moves into BehaviorModule.
- Utility needs to split into UtilityEffect, UtilityBehavior, BoardModifier, and
  RetiredSupport.
- Capability and BehaviorModule duplicate the same tick seam.
- The four-field root creates real YAML, inspector, or override UX confusion.
- Fixed typed slots in `EnemyBehaviorRuntimeSet` create maintenance cost.
