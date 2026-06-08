# Audio Optional Cue Governance Implementation Plan

Date: 2026-06-07

This plan follows `Docs/Refactor/AudioOptionalCue-GovernanceAudit.md`. It is for a later implementation PR, not this audit change.

## Goals

- Make optional no-op behavior explicit by cue.
- Fail fast for missing REQUIRED production content.
- Allow OPTIONAL cues to no-op only when the lane policy explicitly says so.
- Reject or report DISABLED cue authoring so unused content does not drift into production profiles.
- Preserve lane-specific ownership and keep shared runtime semantic-agnostic.

## Non-Goals

- Do not make every optional cue required.
- Do not introduce a generic audio dispatcher.
- Do not put action/enemy profiles into `GameplayPresentationAudioConfig`.
- Do not touch UI cue maps, BGM, `StageAudioDefinition`, or shared audio runtime semantics.
- Do not put gameplay semantic meaning into `AudioDefinition`.

## Recommended Option

Use a combined Option B + Option C approach.

Option B for lane-local maps/profiles:

```csharp
public enum AudioCueRequirement
{
    Required = 0,
    Optional = 1,
    Disabled = 2,
}
```

The enum name can be lane-specific if a shared enum would imply shared semantic ownership. The key point is that requirement policy stays in the lane assemblies.

Option C for enemy audio:

```text
Enemy view prefab
  -> EnemyAudioAuthoring
  -> EnemyAudioProfile
  -> EnemyAudioCueRequirementProfile or lane-owned archetype policy table
```

Enemy requirements need to be archetype-specific because `BlackEye`, `RocketFace`, `SecBot`, jump, utility, and simple movers have different valid cue surfaces.

## Action Audio Implementation

Recommended model:

- Add an action-lane requirement table keyed by `GameplayActionKind + GameplayActionAudioMoment`.
- Keep `GameplayActionAudioProfile.IsOptional` as entry-local binding validation only, or migrate it to the new requirement model.
- Add validation API on `GameplayActionAudioProfile`, for example:
  - `ValidateRequirementsOrThrow(IReadOnlyList<GameplayActionAudioRequirement> requirements)`
  - Required: entry must exist and resolve a valid binding.
  - Optional: entry may be absent; if present, binding must be valid unless optional-null remains explicitly allowed.
  - Disabled: entry must be absent, or must be explicitly rejected.

Initial proposed decisions:

| Action/Moment | Requirement |
|---|---|
| Push `Windup` | REQUIRED |
| Flip `Windup` | REQUIRED |
| Push/Flip `AssistOutOfRange` | REQUIRED |
| Push/Flip `NoTarget` | REQUIRED |
| Push/Flip `Invalid` | REQUIRED |
| `Execute`, `Recovery` | OPTIONAL |
| `Contact`, `ImpactEnemy`, `Blocked` | REMOVED from action-audio vocabulary |

Implementation order:

1. Keep Push/Flip `Contact`, `ImpactEnemy`, and `Blocked` out of action-audio vocabulary and profile authoring.
2. Add requirement metadata in `Gameplay_ActionAudio`.
3. Update `GameplayActionAudioProfile` validation to support requirements without changing planner emission.
4. Add canonical player profile tests.
5. Keep runtime missing owner/authoring no-op behavior unchanged unless a later prefab-level production bootstrap check is requested.

## Enemy Audio Implementation

Implemented model:

- Add archetype-level `EnemyAudioRequirementPolicy` assets plus sparse `EnemyAudioRequirementBinding` assets keyed by production enemy profile identity.
- Keep it prefab-local or enemy-lane-owned. Do not move it into `GameplayPresentationAudioConfig`.
- Validation runs in repository smoke tests first. Runtime missing owner/authoring no-op behavior is unchanged.

Requirement rules:

- Required cue must have exactly one valid binding.
- Optional cue may be absent; if present, binding must be valid.
- Unspecified cue is implicit Disabled and must be absent from the target profile. This catches accidental content drift without full disabled rows.
- `ChargeActiveLoop` keeps special validation: looping definition and attachment slot required.

Initial production policy candidates:

| Profile | Required Cues | Disabled / Deferred Notes |
|---|---|---|
| `EnemyAudioProfile_WallFollowerSun` | `Move`, `Death` | Other cues disabled unless archetype changes |
| `EnemyAudioProfile_JumpChaserAstra` | `Move`, `Landing`, `Death` | Non-jump cues disabled |
| `EnemyAudioProfile_BlackEye` | `Move`, `Active`, `ProjectileImpact`, `Death` | `ProjectileImpact` required for projectile arrival |
| `EnemyAudioProfile_DrSaturn` | `Move`, `Windup`, `Active`, `Recover`, `Death` | DrSaturn profile is identity-named; `Active` remains authored for GravityField-oriented utility evaluation. |
| `EnemyAudioProfile_UtilitySummoner` | `Move`, `Active`, `Death` | JPeter expectation drift resolved; `Windup` disabled |
| `EnemyAudioProfile_Nebulous` | `Move`, `Windup`, `Active`, `Recover`, `Death` | `PassiveContact` disabled by current test |
| `EnemyAudioProfile_RocketFace` | `Move`, `ChargeActiveLoop`, `Death` | one-shot `Active` is disabled; charge active audio is loop-only |
| `EnemyAudioProfile_SecBot` | `Move`, `StationaryActive`, `Death` | Other cues disabled |
| `EnemyAudioProfile_Startis` | `Move`, `PassiveContact`, `Death` | PassiveContact is required for NonAttacking contact damage |

Implementation order:

1. Fix or document the JPeter/UtilitySummoner expectation drift.
2. Add enemy requirement policy/binding assets in the enemy lane.
3. Add tests that load production bindings and assert required, optional, and implicit disabled cue behavior.
4. Keep `EnemyAudioRequestPlanner` unchanged unless disabled cues should stop being emitted for a specific archetype.
5. Keep `ChargeActiveLoop` loop/attachment tests and add requirement coverage for RocketFace.

## TileFeature Audio Implementation

Recommended model:

- Add a TileFeature lane requirement catalog.
- Keep `ButtonActivated` required.
- Explicitly mark all other current enum cues as OPTIONAL or DEFER.
- Do not require burst cues just because coalescer can emit them.

Initial decisions:

| Cue | Requirement |
|---|---|
| `ButtonActivated` | REQUIRED |
| `DestroyTileTriggered`, `SlideTileRedirected`, `ExitOpened`, `MoonBlockGenerated` | OPTIONAL |
| `DestroyTileActivated`, `DestroyTileDeactivated`, `BarricadeActivated`, `BarricadeDeactivated` | OPTIONAL |
| `TileFeatureOnBurst`, `TileFeatureOffBurst` | OPTIONAL |
| `BarricadeBlocked`, `BarricadeCrushed`, `ExitEntered`, `MoonBlockGeneratorBlocked` | OPTIONAL or DEFER |

Implementation order:

1. Add explicit requirement catalog in `Gameplay_TileFeatureAudio`.
2. Update `TileFeatureAudioMap.ValidateRequiredCuesOrThrow` or add a new `ValidateRequirementsOrThrow`.
3. Preserve current burst fallback diagnostics.
4. Add tests that optional missing cues no-op only because the catalog marks them OPTIONAL.
5. Add tests that DISABLED cues fail when authored if any cue is explicitly disabled.

## GravityField Audio Implementation

Recommended model:

- Add a GravityField lane requirement catalog even if all current cues remain OPTIONAL.
- Preserve empty required set unless product promotes a cue.
- Keep `LockedBox` optional per `Docs/Architecture/GravityField-LockedTarget-Presentation-Policy.md`.

Initial decisions:

| Cue | Requirement |
|---|---|
| `Activated` | OPTIONAL or DEFER for promotion |
| `Expired` | OPTIONAL or DEFER |
| `LockedBox` | OPTIONAL |

Implementation order:

1. Add explicit GravityField requirement catalog.
2. Update validation tests so an empty required set is intentional, not accidental.
3. Add tests for authored optional cue validation.
4. Add tests for disabled cue rejection only if product marks a cue DISABLED.

## Validation Plan

Investigation/documentation PR:

```bash
git diff --check
```

Implementation PR:

```bash
git diff --check
./run_tests.sh core
```

If UI docs/tests are touched:

```bash
./run_tests.sh ui
```

Recommended targeted test clusters for implementation:

- `GameplayActionAudioRuntimeTests`
- `EnemyAudioRuntimeTests`
- `TileFeatureAudioRuntimeTests`
- `GravityFieldAudioRuntimeTests`
- `GameplayPresentationAudioConfigValidationTests`
- `AudioArchitectureTests`
- `AudioRepositoryAssetSmokeCoreTests`

## PR Split

Recommended split:

1. Docs-only audit and plan.
2. Action audio requirement metadata and canonical player validation.
3. Enemy audio archetype requirement profile/table and production prefab tests.
4. TileFeature/GravityField explicit optional requirement catalogs.

Keep asset authoring changes in the same PR as the requirement rule that needs them, and state the purpose of each ScriptableObject/profile edit.
