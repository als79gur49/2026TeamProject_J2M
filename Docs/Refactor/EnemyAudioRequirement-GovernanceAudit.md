# Enemy Audio Requirement Governance Audit

Date: 2026-06-08 KST

This is a docs-only investigation. It does not approve a runtime implementation by itself.

## Scope

Reviewed paths:

- `Assets/_Features/Gameplay/Gameplay_EnemyAudio`
- `Assets/_Features/Gameplay/Gameplay_EnemyAI`
- `Assets/_Features/Gameplay/Gameplay_Entities`
- `Assets/_Features/Gameplay/Gameplay_Host`
- `Assets/_Features/Gameplay/Gameplay_Tests`
- `Assets/_Shared/Audio`
- `Docs/Architecture`
- `Docs/Refactor`

Out of scope:

- `GameplayPresentationAudioConfig` ownership changes
- ActionAudio, TileFeatureAudio, GravityFieldAudio, UI, BGM, StageAudio changes
- Shared runtime changes
- Generic dispatcher work

## Contract Classification

Strong contracts:

- Enemy audio profile authoring stays prefab-local through `EnemyAudioAuthoring -> EnemyAudioProfile_*`.
- Enemy audio must not be added to `GameplayPresentationAudioConfig`.
- `ChargeActiveLoop` is a loop cue. It must use a looping `AudioDefinition` and an attachment slot when authored.
- Shared audio runtime must not know gameplay-specific cue names such as `PassiveContact` or `ProjectileImpact`.

Current policy:

- Runtime missing owner view, missing `EnemyAudioAuthoring`, missing profile, and missing cue entry no-op.
- `EnemyAudioProfile.IsOptional` only permits a null binding when an entry exists; current production profiles do not use optional null entries.
- `EnemyAudioRequestPlanner` is signal-driven and does not special-case enemy prefab/profile names.
- The old candidate `EnemyAudioRequirementProfile_*` full-matrix assets/tests were implementation draft work, not audit source of truth. The active implementation uses `EnemyAudioRequirementPolicy_*` plus sparse `EnemyAudioRequirementBinding_*`.
- Startis `PassiveContact` is production-authored content coverage. Runtime missing cue no-op policy remains unchanged, but Startis `PassiveContact` signals can now play because the cue is authored.

## Runtime Can-Emit Surface

`EnemyAudioRequestPlanner` can create one-shot requests for:

- `Move`: enemy `TickEntityMotion` move or kinematic voluntary locomotion start.
- `Death`: enemy death/killed `EntityExitSignal`.
- `Windup`: enemy action start, lock-nearby-boxes utility windup, gravity-field-aura utility windup, summon windup start, glide windup start.
- `Landing`: enemy jump landed.
- `Active`: normal enemy action execution, gravity-field-aura attack/active start, summoned enemy spawn source, glide active start, charge active start.
- `Recover`: enemy action recovery, lock-nearby-boxes recovery, gravity-field-aura recovery, glide recovery.
- `ProjectileImpact`: valid forward-cell projectile arrival, hit or miss, deduped by arrival identity.
- `StationaryActive`: any final enemy with no motion fact and no other planned request, then filtered by face/activity and cadence in the controller.
- `PassiveContact`: passive-contact action execution, except receiver-cooldown and player-invincible rejection paths.

`EnemyChargeLoopAudioPresentationController` can start/refresh/stop `ChargeActiveLoop` while `TickEnemyChargePresentationSignal.Phase == Active`.

## Current Missing Behavior

- One-shot controller: missing owner view, missing authoring, missing profile entry, and missing cue all return without playing.
- Loop controller: missing owner view, missing authoring, missing `ChargeActiveLoop`, invalid handle, or inactive/stale signal returns/stops without playing.
- Profile validation catches duplicate cues, empty cue, invalid binding/category/policy, looping one-shot definitions, and invalid `ChargeActiveLoop` loop/attachment authoring.
- Missing cue is not a runtime warning or runtime failure today.
- Requirement policy/binding validation moves production `Required`/implicit-`Disabled` mismatches into repository validation, while preserving runtime no-op behavior.
- Optional requiredness permits missing bindings as intentional no-op only. If an optional cue is authored, the binding must still satisfy normal `EnemyAudioProfile` validation.

## Production Profile and Prefab Inventory

| Enemy Profile | Prefab | Authored cues |
| --- | --- | --- |
| `EnemyAudioProfile_WallFollowerSun` | `EnemyView_Sunwheel` | `Move`, `Death` |
| `EnemyAudioProfile_JumpChaserAstra` | `EnemyView_Astreton` | `Move`, `Landing`, `Death` |
| `EnemyAudioProfile_BlackEye` | `EnemyView_BlackEye` | `Move`, `Active`, `ProjectileImpact`, `Death` |
| `EnemyAudioProfile_DrSaturn` | `EnemyView_DrSaturn` | `Move`, `Windup`, `Active`, `Recover`, `Death` |
| `EnemyAudioProfile_UtilitySummoner` | `EnemyView_JPeter` | `Move`, `Active`, `Death` |
| `EnemyAudioProfile_Nebulous` | `EnemyView_Nebulous` | `Move`, `Windup`, `Active`, `Recover`, `Death` |
| `EnemyAudioProfile_RocketFace` | `EnemyView_RocketFace` | `Move`, `ChargeActiveLoop`, `Death` |
| `EnemyAudioProfile_SecBot` | `EnemyView_SecBot` | `Move`, `Death`, `StationaryActive` |
| `EnemyAudioProfile_Startis` | `EnemyView_Startis` | `Move`, `Death`, `PassiveContact` |

Enemy prefabs under the same production presentation folder without an `EnemyAudioAuthoring` profile reference in the audit scan:

- `EnemyView_Jumping`
- `EnemyView_Kali`
- `EnemyView_LockNearbyBoxes`

Those prefabs are outside this profile-level requirement table unless production content decides to give them `EnemyAudioProfile_*` authoring.

## Decision Table

Decision values:

- `REQUIRED`: production runtime path can emit and missing binding is a content defect.
- `OPTIONAL`: play when authored; missing is acceptable.
- `DISABLED`: this enemy/archetype intentionally does not use the cue.
- `DEFER`: product/content decision or runtime/content mismatch must be resolved first.
- `TEST_ONLY`: test fixture only, not production content.

### WallFollowerSun / Sunwheel

| Enemy Profile | Prefab | Cue | Runtime Can Emit? | Authored? | Current Missing Behavior | Proposed Requirement | Reason |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `EnemyAudioProfile_WallFollowerSun` | `EnemyView_Sunwheel` | `Move` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | Locomotion path is production-authored. |
| `EnemyAudioProfile_WallFollowerSun` | `EnemyView_Sunwheel` | `Death` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | Enemy-local death suppresses generic enemy damage only when authored. |
| `EnemyAudioProfile_WallFollowerSun` | `EnemyView_Sunwheel` | `Windup` | No current production Sunwheel path found | No | Runtime no-op if requested | `DISABLED` | No authored windup surface for this mover. |
| `EnemyAudioProfile_WallFollowerSun` | `EnemyView_Sunwheel` | `Landing` | No jump path found | No | Runtime no-op if requested | `DISABLED` | Non-jump archetype. |
| `EnemyAudioProfile_WallFollowerSun` | `EnemyView_Sunwheel` | `Active` | No current production Sunwheel action-audio path found | No | Runtime no-op if requested | `DISABLED` | No authored active surface. |
| `EnemyAudioProfile_WallFollowerSun` | `EnemyView_Sunwheel` | `Recover` | No current production Sunwheel path found | No | Runtime no-op if requested | `DISABLED` | No authored recover surface. |
| `EnemyAudioProfile_WallFollowerSun` | `EnemyView_Sunwheel` | `ProjectileImpact` | No projectile path found | No | Runtime no-op if requested | `DISABLED` | Non-projectile archetype. |
| `EnemyAudioProfile_WallFollowerSun` | `EnemyView_Sunwheel` | `ChargeActiveLoop` | No charge path found | No | Loop controller no-op if requested | `DISABLED` | Non-charge archetype. |
| `EnemyAudioProfile_WallFollowerSun` | `EnemyView_Sunwheel` | `StationaryActive` | Generic stationary candidate can be planned | No | Runtime no-op | `DISABLED` | Current product authoring uses move/death only. |
| `EnemyAudioProfile_WallFollowerSun` | `EnemyView_Sunwheel` | `PassiveContact` | Passive-contact capability can produce signals | No | Runtime no-op | `DISABLED` | Not passive-contact-only audio policy; keep future variants separate. |

### JumpChaserAstra / Astreton

| Enemy Profile | Prefab | Cue | Runtime Can Emit? | Authored? | Current Missing Behavior | Proposed Requirement | Reason |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `EnemyAudioProfile_JumpChaserAstra` | `EnemyView_Astreton` | `Move` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | Jump chaser still has locomotion motion facts. |
| `EnemyAudioProfile_JumpChaserAstra` | `EnemyView_Astreton` | `Landing` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | Jump landing signal is the archetype-specific audio cue. |
| `EnemyAudioProfile_JumpChaserAstra` | `EnemyView_Astreton` | `Death` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | Production death cue is authored. |
| `EnemyAudioProfile_JumpChaserAstra` | `EnemyView_Astreton` | `Windup` | No current one-shot jump windup binding | No | Runtime no-op if requested | `DISABLED` | Jump windup is not authored as enemy audio. |
| `EnemyAudioProfile_JumpChaserAstra` | `EnemyView_Astreton` | `Active` | No current jump active audio binding | No | Runtime no-op if requested | `DISABLED` | Landing is the authored jump accent. |
| `EnemyAudioProfile_JumpChaserAstra` | `EnemyView_Astreton` | `Recover` | No recover path found | No | Runtime no-op if requested | `DISABLED` | Non-recover archetype for audio policy. |
| `EnemyAudioProfile_JumpChaserAstra` | `EnemyView_Astreton` | `ProjectileImpact` | No projectile path found | No | Runtime no-op if requested | `DISABLED` | Non-projectile archetype. |
| `EnemyAudioProfile_JumpChaserAstra` | `EnemyView_Astreton` | `ChargeActiveLoop` | No charge path found | No | Loop controller no-op if requested | `DISABLED` | Non-charge archetype. |
| `EnemyAudioProfile_JumpChaserAstra` | `EnemyView_Astreton` | `StationaryActive` | Generic stationary candidate can be planned | No | Runtime no-op | `DISABLED` | Not the SecBot stationary cadence archetype. |
| `EnemyAudioProfile_JumpChaserAstra` | `EnemyView_Astreton` | `PassiveContact` | Common passive-contact capability may exist | No | Runtime no-op | `DISABLED` | Not passive-contact-only audio policy. |

### BlackEye

| Enemy Profile | Prefab | Cue | Runtime Can Emit? | Authored? | Current Missing Behavior | Proposed Requirement | Reason |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `EnemyAudioProfile_BlackEye` | `EnemyView_BlackEye` | `Move` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | Locomotion path is authored. |
| `EnemyAudioProfile_BlackEye` | `EnemyView_BlackEye` | `Active` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | Fire release remains distinct from projectile impact. |
| `EnemyAudioProfile_BlackEye` | `EnemyView_BlackEye` | `ProjectileImpact` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | Projectile arrival path emits hit/miss impact audio. |
| `EnemyAudioProfile_BlackEye` | `EnemyView_BlackEye` | `Death` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | Production death cue is authored. |
| `EnemyAudioProfile_BlackEye` | `EnemyView_BlackEye` | `Windup` | Windup projectile/action start can emit | No | Runtime no-op | `DISABLED` | Product authoring has no BlackEye windup cue. |
| `EnemyAudioProfile_BlackEye` | `EnemyView_BlackEye` | `Landing` | No jump path found | No | Runtime no-op | `DISABLED` | Non-jump archetype. |
| `EnemyAudioProfile_BlackEye` | `EnemyView_BlackEye` | `Recover` | No authored recover surface | No | Runtime no-op | `DISABLED` | Fire/impact profile only. |
| `EnemyAudioProfile_BlackEye` | `EnemyView_BlackEye` | `ChargeActiveLoop` | No charge path found | No | Loop controller no-op | `DISABLED` | Non-charge archetype. |
| `EnemyAudioProfile_BlackEye` | `EnemyView_BlackEye` | `StationaryActive` | Generic stationary candidate can be planned | No | Runtime no-op | `DISABLED` | Not stationary cadence archetype. |
| `EnemyAudioProfile_BlackEye` | `EnemyView_BlackEye` | `PassiveContact` | Common passive-contact capability may exist | No | Runtime no-op | `DISABLED` | Projectile profile owns impact, not contact audio. |

### DrSaturn

| Enemy Profile | Prefab | Cue | Runtime Can Emit? | Authored? | Current Missing Behavior | Proposed Requirement | Reason |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `EnemyAudioProfile_DrSaturn` | `EnemyView_DrSaturn` | `Move` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | Locomotion path is authored. |
| `EnemyAudioProfile_DrSaturn` | `EnemyView_DrSaturn` | `Windup` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | Utility windup signal is production-authored. |
| `EnemyAudioProfile_DrSaturn` | `EnemyView_DrSaturn` | `Recover` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | Utility recover signal is production-authored. |
| `EnemyAudioProfile_DrSaturn` | `EnemyView_DrSaturn` | `Death` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | Production death cue is authored. |
| `EnemyAudioProfile_DrSaturn` | `EnemyView_DrSaturn` | `Active` | GravityField active-start path to be re-confirmed in implementation PR | Yes | Invalid binding fails profile validation | `REQUIRED` | Authored/test-expected cue is preserved; classify by DrSaturn's GravityField-oriented utility role, not by the previous LockNearbyBoxes-specific asset name. |
| `EnemyAudioProfile_DrSaturn` | `EnemyView_DrSaturn` | `Landing` | No jump path found | No | Runtime no-op | `DISABLED` | Non-jump archetype. |
| `EnemyAudioProfile_DrSaturn` | `EnemyView_DrSaturn` | `ProjectileImpact` | No projectile path found | No | Runtime no-op | `DISABLED` | Non-projectile archetype. |
| `EnemyAudioProfile_DrSaturn` | `EnemyView_DrSaturn` | `ChargeActiveLoop` | No charge path found | No | Loop controller no-op | `DISABLED` | Non-charge archetype. |
| `EnemyAudioProfile_DrSaturn` | `EnemyView_DrSaturn` | `StationaryActive` | Generic stationary candidate can be planned | No | Runtime no-op | `DISABLED` | Not stationary cadence archetype. |
| `EnemyAudioProfile_DrSaturn` | `EnemyView_DrSaturn` | `PassiveContact` | Passive-contact capability can produce signals | No | Runtime no-op | `DISABLED` | Existing test policy says JP/DrSaturn/Nebulous do not author passive contact by default. |

### UtilitySummoner / JPeter

| Enemy Profile | Prefab | Cue | Runtime Can Emit? | Authored? | Current Missing Behavior | Proposed Requirement | Reason |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `EnemyAudioProfile_UtilitySummoner` | `EnemyView_JPeter` | `Move` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | Authored cue; current expectation drift should follow profile. |
| `EnemyAudioProfile_UtilitySummoner` | `EnemyView_JPeter` | `Active` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | Summoned enemy spawn emits active cue for the source summoner. |
| `EnemyAudioProfile_UtilitySummoner` | `EnemyView_JPeter` | `Death` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | Production death cue is authored. |
| `EnemyAudioProfile_UtilitySummoner` | `EnemyView_JPeter` | `Windup` | Summon windup warning can emit | No | Runtime no-op | `DISABLED` | Current content policy resolves old expectation drift in favor of no JPeter windup audio. |
| `EnemyAudioProfile_UtilitySummoner` | `EnemyView_JPeter` | `Landing` | No jump path found | No | Runtime no-op | `DISABLED` | Non-jump archetype. |
| `EnemyAudioProfile_UtilitySummoner` | `EnemyView_JPeter` | `Recover` | Summon recover signal exists, planner does not map it to audio | No | Runtime no-op | `DISABLED` | No authored recover audio for summon profile. |
| `EnemyAudioProfile_UtilitySummoner` | `EnemyView_JPeter` | `ProjectileImpact` | No projectile path found | No | Runtime no-op | `DISABLED` | Non-projectile archetype. |
| `EnemyAudioProfile_UtilitySummoner` | `EnemyView_JPeter` | `ChargeActiveLoop` | No charge path found | No | Loop controller no-op | `DISABLED` | Non-charge archetype. |
| `EnemyAudioProfile_UtilitySummoner` | `EnemyView_JPeter` | `StationaryActive` | Generic stationary candidate can be planned | No | Runtime no-op | `DISABLED` | Not stationary cadence archetype. |
| `EnemyAudioProfile_UtilitySummoner` | `EnemyView_JPeter` | `PassiveContact` | Passive-contact capability can produce signals | No | Runtime no-op | `DISABLED` | Existing test policy says JP/DrSaturn/Nebulous do not author passive contact by default. |

### Nebulous

| Enemy Profile | Prefab | Cue | Runtime Can Emit? | Authored? | Current Missing Behavior | Proposed Requirement | Reason |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `EnemyAudioProfile_Nebulous` | `EnemyView_Nebulous` | `Move` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | Locomotion path is authored. |
| `EnemyAudioProfile_Nebulous` | `EnemyView_Nebulous` | `Windup` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | GravityFieldAura windup path is production. |
| `EnemyAudioProfile_Nebulous` | `EnemyView_Nebulous` | `Active` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | GravityFieldAura attack/active path emits active. |
| `EnemyAudioProfile_Nebulous` | `EnemyView_Nebulous` | `Recover` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | GravityFieldAura recovery path is production. |
| `EnemyAudioProfile_Nebulous` | `EnemyView_Nebulous` | `Death` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | Production death cue is authored. |
| `EnemyAudioProfile_Nebulous` | `EnemyView_Nebulous` | `Landing` | No jump path found | No | Runtime no-op | `DISABLED` | Non-jump archetype. |
| `EnemyAudioProfile_Nebulous` | `EnemyView_Nebulous` | `ProjectileImpact` | No projectile path found | No | Runtime no-op | `DISABLED` | Non-projectile archetype. |
| `EnemyAudioProfile_Nebulous` | `EnemyView_Nebulous` | `ChargeActiveLoop` | No charge path found | No | Loop controller no-op | `DISABLED` | Non-charge archetype. |
| `EnemyAudioProfile_Nebulous` | `EnemyView_Nebulous` | `StationaryActive` | Generic stationary candidate can be planned | No | Runtime no-op | `DISABLED` | Not stationary cadence archetype. |
| `EnemyAudioProfile_Nebulous` | `EnemyView_Nebulous` | `PassiveContact` | Passive-contact capability can produce signals | No | Runtime no-op | `DISABLED` | Existing test policy says JP/DrSaturn/Nebulous do not author passive contact by default. |

### RocketFace

| Enemy Profile | Prefab | Cue | Runtime Can Emit? | Authored? | Current Missing Behavior | Proposed Requirement | Reason |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `EnemyAudioProfile_RocketFace` | `EnemyView_RocketFace` | `Move` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | Locomotion path is authored. |
| `EnemyAudioProfile_RocketFace` | `EnemyView_RocketFace` | `ChargeActiveLoop` | Yes, separate loop controller | Yes | Missing loop no-ops; invalid loop/attachment fails profile validation | `REQUIRED` | Charge active phase is loop-only for this archetype. |
| `EnemyAudioProfile_RocketFace` | `EnemyView_RocketFace` | `Death` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | Production death cue is authored. |
| `EnemyAudioProfile_RocketFace` | `EnemyView_RocketFace` | `Active` | Charge active start can emit one-shot candidate | No | Runtime no-op | `DISABLED` | Product policy should be loop-only charge active audio. Do not require one-shot `Active` beside `ChargeActiveLoop`. |
| `EnemyAudioProfile_RocketFace` | `EnemyView_RocketFace` | `Windup` | Charge windup signal exists but planner does not emit one-shot windup | No | Runtime no-op | `DISABLED` | Charge audio starts at active loop. |
| `EnemyAudioProfile_RocketFace` | `EnemyView_RocketFace` | `Landing` | No jump path found | No | Runtime no-op | `DISABLED` | Non-jump archetype. |
| `EnemyAudioProfile_RocketFace` | `EnemyView_RocketFace` | `Recover` | Charge recover signal exists but planner does not emit one-shot recover | No | Runtime no-op | `DISABLED` | Loop stop carries recover transition for audio. |
| `EnemyAudioProfile_RocketFace` | `EnemyView_RocketFace` | `ProjectileImpact` | No projectile path found | No | Runtime no-op | `DISABLED` | Charge is not the forward-cell projectile lane. |
| `EnemyAudioProfile_RocketFace` | `EnemyView_RocketFace` | `StationaryActive` | Generic stationary candidate can be planned | No | Runtime no-op | `DISABLED` | Not stationary cadence archetype. |
| `EnemyAudioProfile_RocketFace` | `EnemyView_RocketFace` | `PassiveContact` | Passive-contact capability may exist | No | Runtime no-op | `DISABLED` | Current charge profile audio is move/loop/death only. |

### SecBot

| Enemy Profile | Prefab | Cue | Runtime Can Emit? | Authored? | Current Missing Behavior | Proposed Requirement | Reason |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `EnemyAudioProfile_SecBot` | `EnemyView_SecBot` | `Move` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | Locomotion cue is authored. |
| `EnemyAudioProfile_SecBot` | `EnemyView_SecBot` | `StationaryActive` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | SecBot is the explicit stationary cadence archetype. |
| `EnemyAudioProfile_SecBot` | `EnemyView_SecBot` | `Death` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | Production death cue is authored. |
| `EnemyAudioProfile_SecBot` | `EnemyView_SecBot` | `Windup` | No current production path found | No | Runtime no-op | `DISABLED` | No windup authoring. |
| `EnemyAudioProfile_SecBot` | `EnemyView_SecBot` | `Landing` | No jump path found | No | Runtime no-op | `DISABLED` | Non-jump archetype. |
| `EnemyAudioProfile_SecBot` | `EnemyView_SecBot` | `Active` | Generic action active could exist but not current SecBot policy | No | Runtime no-op | `DISABLED` | StationaryActive is the active-like audio surface. |
| `EnemyAudioProfile_SecBot` | `EnemyView_SecBot` | `Recover` | No recover path found | No | Runtime no-op | `DISABLED` | No recover authoring. |
| `EnemyAudioProfile_SecBot` | `EnemyView_SecBot` | `ProjectileImpact` | No projectile path found | No | Runtime no-op | `DISABLED` | Non-projectile archetype. |
| `EnemyAudioProfile_SecBot` | `EnemyView_SecBot` | `ChargeActiveLoop` | No charge path found | No | Loop controller no-op | `DISABLED` | Non-charge archetype. |
| `EnemyAudioProfile_SecBot` | `EnemyView_SecBot` | `PassiveContact` | Passive-contact capability may exist | No | Runtime no-op | `DISABLED` | Not passive-contact-only audio policy. |

### Startis

| Enemy Profile | Prefab | Cue | Runtime Can Emit? | Authored? | Current Missing Behavior | Proposed Requirement | Reason |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `EnemyAudioProfile_Startis` | `EnemyView_Startis` | `Move` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | Locomotion cue is authored. |
| `EnemyAudioProfile_Startis` | `EnemyView_Startis` | `PassiveContact` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | Startis uses NonAttacking passive-contact damage; missing contact cue is a content defect. |
| `EnemyAudioProfile_Startis` | `EnemyView_Startis` | `Death` | Yes | Yes | Invalid binding fails profile validation | `REQUIRED` | Production death cue is authored. |
| `EnemyAudioProfile_Startis` | `EnemyView_Startis` | `Windup` | No current production path found | No | Runtime no-op | `DISABLED` | Passive-contact-only, not windup attack audio. |
| `EnemyAudioProfile_Startis` | `EnemyView_Startis` | `Landing` | No jump path found | No | Runtime no-op | `DISABLED` | Non-jump archetype. |
| `EnemyAudioProfile_Startis` | `EnemyView_Startis` | `Active` | Passive contact maps to `PassiveContact`, not `Active` | No | Runtime no-op | `DISABLED` | Tests guard against passive contact falling back to active/summon audio. |
| `EnemyAudioProfile_Startis` | `EnemyView_Startis` | `Recover` | No recover path found | No | Runtime no-op | `DISABLED` | No recover authoring. |
| `EnemyAudioProfile_Startis` | `EnemyView_Startis` | `ProjectileImpact` | No projectile path found | No | Runtime no-op | `DISABLED` | Non-projectile archetype. |
| `EnemyAudioProfile_Startis` | `EnemyView_Startis` | `ChargeActiveLoop` | No charge path found | No | Loop controller no-op | `DISABLED` | Non-charge archetype. |
| `EnemyAudioProfile_Startis` | `EnemyView_Startis` | `StationaryActive` | Generic stationary candidate can be planned | No | Runtime no-op | `DISABLED` | Startis active contact audio is event-driven, not idle cadence. |

## Direct Answers

`ChargeActiveLoop` is required only for `EnemyAudioProfile_RocketFace` / `EnemyView_RocketFace` among audited production profiles.

`Active` one-shot and `ChargeActiveLoop` loop should not coexist as required on RocketFace. Runtime can produce a charge active-start one-shot candidate, but product policy should make RocketFace active audio loop-only and mark one-shot `Active` disabled.

Passive-contact-only enemy audio should require `PassiveContact`. In current production, this applies to Startis. JP/DrSaturn/Nebulous are guarded by tests as no default passive-contact authoring. WallFollower/Sunwheel has passive-contact capability evidence but is not treated as passive-contact-only audio content in this audit.

Projectile enemy audio should require `ProjectileImpact` when the production archetype emits forward-cell projectile arrivals. In current production, this applies to BlackEye.

`Move` and `Death` are production-required for every audited production profile because every profile authors them and runtime can emit those paths. `Windup`, `Recover`, `Landing`, `Active`, `ProjectileImpact`, `ChargeActiveLoop`, `StationaryActive`, and `PassiveContact` are archetype-specific.

JPeter / UtilitySummoner expectation drift is a test expectation issue if the test still expects `Windup`. The production profile authors `Move`, `Active`, and `Death`; `Windup` should be disabled unless product explicitly wants summon windup audio.

DrSaturn `Active` is preserved as required under the GravityFieldUtility production policy. Do not classify DrSaturn as LockNearbyBoxes-only or rename the production binding back to the old LockNearbyBoxes-specific draft.

## Implementation Input Summary

- Use an enemy-lane archetype policy plus sparse binding table keyed by production profile identity.
- Do not make all `EnemyAudioCue` values required.
- Keep runtime no-op behavior unless a later product decision asks for runtime fail-fast.
- Enforce production required/disabled mismatches in repository smoke validation first.
- Keep test fixture profiles outside production requirement coverage unless explicitly marked `TEST_ONLY`.
