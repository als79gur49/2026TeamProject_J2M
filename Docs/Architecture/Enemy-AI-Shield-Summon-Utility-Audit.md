# Enemy AI Shield, Summon, and Utility Audit

This note records the follow-up investigation path after Phase 1. It is
classification work only. It does not move Summon, deprecate Utility, implement
Shield, or start Phase 2.

## Shield Owner Matrix

| Candidate | Owner Signal | BehaviorModule Fit |
| --- | --- | --- |
| EnemyShieldBehavior | enemy-owned defensive state controlled by AI mode/resolver | possible future candidate |
| BoardShieldModifier | cell, face, traversal, passive contact, or box/solid modifier | likely BoardEffect or Utility/board modifier |
| BoxSlideShield | legacy/retired concept | not reusable |
| PresentationShield | visual-only barrier | not a behavior module |

Shield must be classified by these questions before implementation:

- Does it couple to enemy AI mode or resolver decisions?
- Does it own authoritative runtime state?
- Does it enter determinism hash?
- Does it block combat damage?
- Does it block passive contact?
- Does it alter movement or traversal legality?
- Does it alter box or solid interaction?
- Is it visual-only?

## Summon and Utility Matrix

| Existing behavior | Current owner | Follow-up classification |
| --- | --- | --- |
| SummonMinion | Utility capability | possible future BehaviorModule only if AI-mode coupling is proven |
| GravityFieldAura | Utility capability | likely UtilityEffect or BoardModifier if split becomes necessary |
| RetiredLockNearbyBoxes | retired compatibility guard | cleanup/guard only |
| RetiredFrontFaceSupport | retired compatibility guard | cleanup/guard only |

Summon and Utility stay in the capability lane until a concrete migration trigger
exists. Utility remains an active lane.

Post-Option-C note: Summon spawn/entity creation materialization has been
extracted behind the `EntitySpawnRequest` / `EntitySpawnMaterializer` seam, but
Summon remains a Utility capability. Utility still owns trigger timing, effect
state, cooldown, windup, recovery, max-alive checks, and source validation.
Option C does not start SummonBehaviorModule migration.

## Investigation Questions

- Does Summon couple to enemy AI mode or resolver decisions?
- Does Summon own timed runtime state?
- Does Summon create spawn finalization operations?
- Does Summon affect determinism hash?
- If Summon moves later, can Utility lose that behavior cleanly?
- Is GravityFieldAura better modeled as a BoardModifier than a BehaviorModule?
- Does Utility need to split into UtilityEffect, UtilityBehavior, BoardModifier,
  and RetiredSupport?

## Phase 2 Triggers

Do not enter Phase 2 until at least one trigger is real:

- Behavior module keys grow to at least three total keys.
- Shield or Summon actually moves into BehaviorModule.
- Utility needs a real split across effect, behavior, board modifier, and retired
  support responsibilities.
- Capability and BehaviorModule duplicate the same tick seam.
- The four-field profile root creates authoring or override confusion.
- Fixed typed behavior slots create maintenance cost.

## Non-Goals

- Do not remove `capabilityAssets`.
- Do not deprecate Utility.
- Do not move Summon into BehaviorModule in this audit.
- Do not implement Shield in this audit.
- Do not introduce a unified logic-module serialized root.
- Do not replace `EnemyBehaviorRuntimeSet` with a dictionary registry.
- Do not add a generic world-state update behavior hook.
- Do not create traversal/contact policy assets before actual Charge variation
  requires them.
- Do not treat timing-only Charge variants as permission to mix traversal,
  contact, or passive-contact policy into a Charge variation before a concrete
  production requirement exists.
