# Gameplay Enemy Audio Governance

Enemy audio is prefab-local presentation authoring.

```text
Enemy view prefab
  -> EnemyAudioAuthoring
  -> EnemyAudioProfile_*
  -> AudioBinding
```

Enemy audio requirement governance uses archetype-level policies plus sparse profile bindings.

```text
EnemyAudioRequirementPolicy
  -> requiredCues
  -> optionalCues
  -> unspecified runtime cue = implicit Disabled

EnemyAudioRequirementBinding
  -> target EnemyAudioProfile
  -> policy
  -> sparse overrides
```

Requirement policy/binding assets sit beside production enemy audio profiles and define the profile/archetype-owned cue contract. They are content governance metadata, not playback data, and are not part of `GameplayPresentationAudioConfig`. Runtime missing owner, authoring, profile, and cue no-op policy remains unchanged.

Authored content coverage can still change independently from runtime no-op policy. Startis now authors `PassiveContact` as required production content, so `PassiveContact` signals that previously resolved to a missing-cue no-op can now play the authored definition.

## Requirement Vocabulary

- `Required`: runtime can emit this cue and production content must provide a valid binding.
- `Optional`: runtime may emit this cue, but a missing binding is an intentional no-op.
- `Disabled`: this profile/archetype intentionally does not use this cue, and must not carry a binding. Disabled cues are implicit when omitted from policy/override data.

Validation rules:

- Required + missing binding fails.
- Required + invalid binding fails.
- Optional + missing binding is valid.
- Optional + invalid non-null binding fails.
- Disabled + missing binding is valid.
- Disabled + binding fails.
- Duplicate cue binding fails.
- Wrong category or loop policy fails.
- Validation still checks every `EnemyAudioCueCatalog.RuntimeCues` value even though policy/binding authoring is sparse.

`Move`, `Death`, `Windup`, `Landing`, `Active`, `Recover`, `ProjectileImpact`, `StationaryActive`, and `PassiveContact` are SFX one-shots and must not loop. `ChargeActiveLoop` is a persistent SFX loop, must use a looping definition, must provide an attachment slot, and the controller owns handle lifecycle.

## Production Requirements

Production policy assets live under `AudioRequirementPolicies/`; production binding assets live under `AudioRequirementBindings/`. Every production `EnemyAudioProfile_*` must have exactly one `EnemyAudioRequirementBinding_*`.

| Profile / Archetype | Required | Disabled Decision |
|---|---|---|
| `EnemyAudioProfile_WallFollowerSun` / Sunwheel | `Move`, `Death` | Other cues disabled. |
| `EnemyAudioProfile_JumpChaserAstra` / Astreton | `Move`, `Landing`, `Death` | Non-jump cues disabled. |
| `EnemyAudioProfile_BlackEye` / BlackEye | `Move`, `Active`, `ProjectileImpact`, `Death` | `ProjectileImpact` is required for projectile arrival; non-projectile cues disabled. |
| `EnemyAudioProfile_DrSaturn` / DrSaturn | `Move`, `Windup`, `Active`, `Recover`, `Death` | Profile is named by enemy identity, not by the LockNearbyBoxes utility; `Active` is preserved for GravityField-oriented utility evaluation. |
| `EnemyAudioProfile_UtilitySummoner` / JPeter | `Move`, `Active`, `Death` | `Windup` disabled; previous test expectation drift is closed in favor of authored `Move`. |
| `EnemyAudioProfile_Nebulous` / Nebulous | `Move`, `Windup`, `Active`, `Recover`, `Death` | `PassiveContact` disabled for this utility profile. |
| `EnemyAudioProfile_RocketFace` / RocketFace | `Move`, `ChargeActiveLoop`, `Death` | One-shot `Active` disabled; charge active audio is loop-only. |
| `EnemyAudioProfile_SecBot` / SecBot | `Move`, `StationaryActive`, `Death` | Other cues disabled. |
| `EnemyAudioProfile_Startis` / Startis | `Move`, `PassiveContact`, `Death` | `PassiveContact` is required because Startis uses the NonAttacking passive-contact gameplay profile. |

Startis `PassiveContact` is an authored content coverage change, not a runtime no-op policy change. The runtime still no-ops missing enemy audio cues; Startis production authoring now provides the cue that was previously allowed to be absent.

No current production enemy cue is classified `Optional`. Optional remains available for future archetypes where a runtime cue is intentionally allowed to no-op.

## Boundaries

- Enemy audio requiredness is profile/archetype-owned.
- Enemy profiles, requirement policies, and requirement bindings stay prefab-local or enemy-lane-owned.
- Presentation, UI, BGM, stage audio, action audio, TileFeature audio, and GravityField audio are separate lanes.
- Do not introduce an untyped dispatcher or fallback lookup for enemy audio.

Adding a new `EnemyAudioCue` requires updating `EnemyAudioCueCatalog.RuntimeCues`, updating planner/controller emission, deciding whether each production policy should require, optionalize, or implicitly disable the cue, and updating policy tests and docs.
