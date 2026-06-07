# Audio Optional Cue Governance Audit

Date: 2026-06-07

Scope:

- `Assets/_Features/Gameplay/Gameplay_ActionAudio`
- `Assets/_Features/Gameplay/Gameplay_EnemyAudio`
- `Assets/_Features/Gameplay/Gameplay_TileFeatureAudio`
- `Assets/_Features/Gameplay/Gameplay_GravityFieldAudio`
- `Assets/_Features/Gameplay/Gameplay_Host`
- `Assets/_Features/Gameplay/Gameplay_Tests`
- `Assets/_Shared/Audio`
- `Docs/Architecture`
- `Docs/Refactor`

This is an audit only. It does not add fail-fast behavior.

## Summary

Current SFX lane ownership is still lane-specific:

```text
presentation signal
  -> lane-specific planner/controller
  -> lane-specific semantic/cue/moment
  -> lane-specific map/profile
  -> AudioBinding
  -> AudioDefinition
  -> AudioClip
```

Shared audio runtime remains semantic-agnostic. `GameplayPresentationAudioConfig` owns config-level host maps only and intentionally excludes prefab-local action/enemy profiles, UI cue maps, BGM, stage audio, runtime installers, and settings bridges.

The optional no-op risk is real in four places:

- Action audio: missing owner, missing `GameplayActionAudioAuthoring`, missing profile entry, and optional null profile entry all no-op at runtime.
- Enemy audio: missing owner, missing `EnemyAudioAuthoring`, missing profile entry, and missing cue all no-op at runtime. `ChargeActiveLoop` has validation only when authored.
- TileFeature audio: only `ButtonActivated` is required. Optional cues no-op when absent. Missing burst binding reports a diagnostic and falls back only when the representative single cue has a binding.
- GravityField audio: required set is empty. `Activated`, `Expired`, and `LockedBox` are all resolved through optional lookup; only `Activated` is authored in the canonical map.

## Action Cue Governance

Vocabulary:

- `GameplayActionKind`: `Push`, `Flip`
- `GameplayActionAudioMoment`: `Windup`, `Execute`, `Recovery`, `AssistOutOfRange`, `NoTarget`, `Invalid`
- Removed action-audio moments: `Contact`, `ImpactEnemy`, and `Blocked`

Planner emission:

- Lifecycle requests come from `TickResult.PresentationData.PlayerActionSignals`.
- Fake attempt failure requests come from `TickResult.PresentationData.PlayerActionAttemptSignals`.
- The planner emits only retained current moments. Push/Flip `Contact`, `ImpactEnemy`, and `Blocked` action-audio requests were removed because they are not used by the production path.

Runtime missing behavior:

- Missing owner view: no-op in `GameplayActionAudioPresentationController.PlayRequest`.
- Missing `GameplayActionAudioAuthoring`: no-op.
- Missing profile entry: no-op.
- Optional null profile entry: no-op. `GameplayActionAudioProfile.CollectDiagnostics` logs a warning for `Binding == null && IsOptional`.
- Non-optional null binding, duplicate entry, wrong category, loop, or non-null `AudioBinding.Policy`: validation error.

Canonical authored asset:

- `Assets/_Features/Gameplay/Gameplay_ActionAudio/Profiles/Player_S1_GameplayActionAudioProfile.asset`
- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/Player_S1.prefab` references it through `GameplayActionAudioAuthoring`.

| Moment | Planner Emits? | Authored? | Optional Flag | Current Missing Behavior | Proposed Requirement |
|---|---:|---|---|---|---|
| `Windup` | Yes, Push/Flip `StartedThisTick` | Push binding, Flip binding | No | Missing entry would no-op; invalid authored binding fails validation | REQUIRED for Push and Flip |
| `Execute` | Yes, Push/Flip `ExecutedThisTick` | No entry | None | Missing entry no-op | OPTIONAL |
| `Contact` | Removed | Removed from Player S1 profile | n/a | n/a | REMOVED from action-audio vocabulary |
| `ImpactEnemy` | Removed | Removed from Player S1 profile | n/a | n/a | REMOVED from action-audio vocabulary |
| `Blocked` | Removed | Removed from Player S1 profile | n/a | n/a | REMOVED from action-audio vocabulary |
| `Recovery` | Yes, Push/Flip executed recovery phase | No entry | None | Missing entry no-op | OPTIONAL |
| `AssistOutOfRange` | Yes, Push/Flip fake attempt feedback | Push binding, Flip binding | No | Missing entry would no-op; invalid authored binding fails validation | REQUIRED for Push and Flip |
| `NoTarget` | Yes, Push/Flip fake attempt feedback | Push binding, Flip binding | No | Missing entry would no-op; invalid authored binding fails validation | REQUIRED for Push and Flip |
| `Invalid` | Yes, Push/Flip fake attempt feedback | Push binding, Flip binding | No | Missing entry would no-op; invalid authored binding fails validation | REQUIRED for Push and Flip |

Audit note: The previous deferred/required decision for Push/Flip `Contact`, `ImpactEnemy`, and `Blocked` was replaced by deletion. These are not optional governance rows; they are no longer action-audio moments. Impact and blocked gameplay/presentation signals remain in their existing lanes.

## Enemy Cue Governance

Vocabulary:

- `None`
- `Move`
- `Death`
- `Windup`
- `Landing`
- `Active`
- `Recover`
- `ProjectileImpact`
- `ChargeActiveLoop`
- `StationaryActive`
- `PassiveContact`

Planner/controller emission:

- `EnemyAudioRequestPlanner` can request `Move`, `Death`, `Windup`, `Landing`, `Active`, `Recover`, `ProjectileImpact`, `StationaryActive`, and `PassiveContact`.
- `EnemyChargeLoopAudioPresentationController` can start `ChargeActiveLoop` from `TickEnemyChargePresentationSignal` while phase is `Active`.
- Charge also produces one-shot `Active` at active start through `EnemyAudioRequestPlanner`, but `EnemyAudioProfile_RocketFace` currently authors `ChargeActiveLoop` and not `Active`.

Runtime missing behavior:

- Missing owner view: no-op.
- Missing `EnemyAudioAuthoring`: no-op.
- Missing profile/cue entry: no-op.
- `EnemyAudioProfile.IsOptional` only affects validation of null bindings when an entry exists. Current production enemy profiles do not use optional null entries.
- Duplicate cue, empty cue, invalid binding, wrong category, non-loop one-shot policy violation, and non-null policy are validation errors.
- `ChargeActiveLoop` validation is in `EnemyAudioProfile.AppendLoopCueValidationErrors`: authored `ChargeActiveLoop` must use a looping `AudioDefinition` and an attachment slot.

Production profile authoring:

| Enemy Profile | Cue | Runtime Can Emit? | Authored? | Current Missing Behavior | Proposed Requirement |
|---|---|---:|---:|---|---|
| `EnemyAudioProfile_WallFollowerSun` / `EnemyView_Sunwheel` | `Move` | Yes | Yes | Invalid binding fails validation | REQUIRED |
| `EnemyAudioProfile_WallFollowerSun` / `EnemyView_Sunwheel` | `Death` | Yes | Yes | Invalid binding fails validation | REQUIRED |
| `EnemyAudioProfile_JumpChaserAstra` / `EnemyView_Astreton` | `Move` | Yes | Yes | Invalid binding fails validation | REQUIRED |
| `EnemyAudioProfile_JumpChaserAstra` / `EnemyView_Astreton` | `Landing` | Yes | Yes | Invalid binding fails validation | REQUIRED |
| `EnemyAudioProfile_JumpChaserAstra` / `EnemyView_Astreton` | `Death` | Yes | Yes | Invalid binding fails validation | REQUIRED |
| `EnemyAudioProfile_BlackEye` / `EnemyView_BlackEye` | `Move` | Yes | Yes | Invalid binding fails validation | REQUIRED |
| `EnemyAudioProfile_BlackEye` / `EnemyView_BlackEye` | `Active` | Yes | Yes | Invalid binding fails validation | REQUIRED for fire release |
| `EnemyAudioProfile_BlackEye` / `EnemyView_BlackEye` | `ProjectileImpact` | Yes | Yes | Invalid binding fails validation | REQUIRED for projectile arrival |
| `EnemyAudioProfile_BlackEye` / `EnemyView_BlackEye` | `Death` | Yes | Yes | Invalid binding fails validation | REQUIRED |
| `EnemyAudioProfile_LockNearbyBoxesDrS` / `EnemyView_DrSaturn` | `Move` | Yes | Yes | Invalid binding fails validation | REQUIRED |
| `EnemyAudioProfile_LockNearbyBoxesDrS` / `EnemyView_DrSaturn` | `Windup` | Yes | Yes | Invalid binding fails validation | REQUIRED for utility windup |
| `EnemyAudioProfile_LockNearbyBoxesDrS` / `EnemyView_DrSaturn` | `Active` | Yes | Yes | Invalid binding fails validation | REQUIRED for utility active |
| `EnemyAudioProfile_LockNearbyBoxesDrS` / `EnemyView_DrSaturn` | `Recover` | Yes | Yes | Invalid binding fails validation | REQUIRED for utility recover |
| `EnemyAudioProfile_LockNearbyBoxesDrS` / `EnemyView_DrSaturn` | `Death` | Yes | Yes | Invalid binding fails validation | REQUIRED |
| `EnemyAudioProfile_UtilitySummoner` / `EnemyView_JPeter` | `Move` | Yes | Yes | Invalid binding fails validation | DEFER: prefab expectation test currently lists `Windup` instead of this authored cue |
| `EnemyAudioProfile_UtilitySummoner` / `EnemyView_JPeter` | `Active` | Yes | Yes | Invalid binding fails validation | REQUIRED for summon/spawn active |
| `EnemyAudioProfile_UtilitySummoner` / `EnemyView_JPeter` | `Death` | Yes | Yes | Invalid binding fails validation | REQUIRED |
| `EnemyAudioProfile_Nebulous` / `EnemyView_Nebulous` | `Move` | Yes | Yes | Invalid binding fails validation | REQUIRED |
| `EnemyAudioProfile_Nebulous` / `EnemyView_Nebulous` | `Windup` | Yes | Yes | Invalid binding fails validation | REQUIRED |
| `EnemyAudioProfile_Nebulous` / `EnemyView_Nebulous` | `Active` | Yes | Yes | Invalid binding fails validation | REQUIRED |
| `EnemyAudioProfile_Nebulous` / `EnemyView_Nebulous` | `Recover` | Yes | Yes | Invalid binding fails validation | REQUIRED |
| `EnemyAudioProfile_Nebulous` / `EnemyView_Nebulous` | `Death` | Yes | Yes | Invalid binding fails validation | REQUIRED |
| `EnemyAudioProfile_RocketFace` / `EnemyView_RocketFace` | `Move` | Yes | Yes | Invalid binding fails validation | REQUIRED |
| `EnemyAudioProfile_RocketFace` / `EnemyView_RocketFace` | `ChargeActiveLoop` | Yes, separate loop controller | Yes | Missing cue no-ops; invalid loop/attachment fails validation when authored | REQUIRED |
| `EnemyAudioProfile_RocketFace` / `EnemyView_RocketFace` | `Death` | Yes | Yes | Invalid binding fails validation | REQUIRED |
| `EnemyAudioProfile_RocketFace` / `EnemyView_RocketFace` | `Active` | Yes, charge active start | No | Missing cue no-op | DISABLED if loop-only charge active is product policy; otherwise DEFER |
| `EnemyAudioProfile_SecBot` / `EnemyView_SecBot` | `Move` | Yes | Yes | Invalid binding fails validation | REQUIRED |
| `EnemyAudioProfile_SecBot` / `EnemyView_SecBot` | `Death` | Yes | Yes | Invalid binding fails validation | REQUIRED |
| `EnemyAudioProfile_SecBot` / `EnemyView_SecBot` | `StationaryActive` | Yes, stationary/no-motion path | Yes | Invalid binding fails validation | REQUIRED for SecBot stationary cadence |
| `EnemyAudioProfile_Startis` / `EnemyView_Startis` | `Move` | Yes | Yes | Invalid binding fails validation | REQUIRED |
| `EnemyAudioProfile_Startis` / `EnemyView_Startis` | `Death` | Yes | Yes | Invalid binding fails validation | REQUIRED |
| Non-passive-contact production profiles | `PassiveContact` | Yes for passive contact source | No | Missing cue no-op | DISABLED by current tests for JPeter, DrSaturn, Nebulous; DEFER for future passive-contact archetypes |
| Non-projectile production profiles | `ProjectileImpact` | Can emit only when projectile arrival signal exists | No | Missing cue no-op | DISABLED unless the archetype emits projectile arrivals |
| Non-jump production profiles | `Landing` | Can emit only when jump landed signal exists | No | Missing cue no-op | DISABLED unless the archetype emits jump landings |
| Non-stationary-active production profiles | `StationaryActive` | Can emit for stationary enemy state | No | Missing cue no-op | DISABLED unless product wants idle/stationary SFX |

Audit notes:

- `EnemyPrefabs_HaveValidEnemyAudioAuthoring` currently verifies authoring/profile validity and at least one cue, but does not enforce `PrefabExpectation.Cues` as an exact required set.
- The JPeter expectation lists `Windup`, `Active`, `Death`; the prefab references `EnemyAudioProfile_UtilitySummoner`, which authors `Move`, `Active`, `Death`. This is a drift candidate for the implementation PR.

## TileFeature Optional Cue Governance

Required set:

- `TileFeatureAudioCueCatalog.RequiredOneShotV1` contains only `ButtonActivated`.
- `Docs/Architecture/ADR/ADR-006-TileFeature-Overlay-Layer-Gate.md` also states `ButtonActivated` is the only required TileFeatureAudio cue.

Planner-emittable cue surface:

- `ButtonActivated`
- `DestroyTileTriggered`
- `SlideTileRedirected`
- `BarricadeBlocked`
- `BarricadeCrushed`
- `ExitOpened`
- `ExitEntered`
- `MoonBlockGenerated`
- `MoonBlockGeneratorBlocked`
- `DestroyTileActivated`
- `DestroyTileDeactivated`
- `BarricadeActivated`
- `BarricadeDeactivated`
- Coalesced burst requests: `TileFeatureOnBurst`, `TileFeatureOffBurst`

Canonical map authored entries:

- `ButtonActivated`
- `DestroyTileTriggered`
- `SlideTileRedirected`
- `ExitOpened`
- `MoonBlockGenerated`
- `DestroyTileActivated`
- `DestroyTileDeactivated`
- `BarricadeActivated`
- `BarricadeDeactivated`

Missing optional behavior:

- `ButtonActivated` uses `ResolveOrThrow` and is required.
- All other cue lookups use `TryResolveOptional` or `TryResolveMoonBlockGeneratorBlocked`.
- Missing optional binding returns false and no-ops.
- `MoonBlockGeneratorBlocked` first tries reason-specific binding, then generic `MoonBlockGeneratorBlocked`, then no-op.
- Multiple `DestroyTileActivated`/`BarricadeActivated` requests coalesce into `TileFeatureOnBurst`.
- Multiple `DestroyTileDeactivated`/`BarricadeDeactivated` requests coalesce into `TileFeatureOffBurst`.
- If a burst binding is missing, `TileFeatureAudioPresentationController` sends a diagnostic to its diagnostic sink and tries the representative single cue. If that representative single cue is also missing, playback no-ops.

| Cue | Runtime Can Emit? | Authored? | Current Missing Behavior | Proposed Requirement |
|---|---:|---:|---|---|
| `ButtonActivated` | Yes | Yes | Missing required cue fails validation | REQUIRED |
| `DestroyTileTriggered` | Yes | Yes | Missing optional no-op | OPTIONAL |
| `SlideTileRedirected` | Yes | Yes | Missing optional no-op | OPTIONAL |
| `BarricadeBlocked` | Yes | No | Missing optional no-op | OPTIONAL or DEFER content decision |
| `BarricadeCrushed` | Yes | No | Missing optional no-op | OPTIONAL or DEFER content decision |
| `ExitOpened` | Yes | Yes | Missing optional no-op | OPTIONAL |
| `ExitEntered` | Yes | No | Missing optional no-op | OPTIONAL or DEFER content decision |
| `MoonBlockGenerated` | Yes | Yes | Missing optional no-op | OPTIONAL |
| `MoonBlockGeneratorBlocked` | Yes | No | Missing optional no-op after reason-specific and generic lookup fail | OPTIONAL or DEFER content decision |
| `DestroyTileActivated` | Yes, singleton or burst representative | Yes | Missing optional no-op, or burst fallback may use representative | OPTIONAL |
| `DestroyTileDeactivated` | Yes, singleton or burst representative | Yes | Missing optional no-op, or burst fallback may use representative | OPTIONAL |
| `BarricadeActivated` | Yes, singleton or burst representative | Yes | Missing optional no-op, or burst fallback may use representative | OPTIONAL |
| `BarricadeDeactivated` | Yes, singleton or burst representative | Yes | Missing optional no-op, or burst fallback may use representative | OPTIONAL |
| `TileFeatureOnBurst` | Yes, only when multiple on-state events coalesce | No | Diagnostic plus representative single fallback; no-op if representative missing | OPTIONAL |
| `TileFeatureOffBurst` | Yes, only when multiple off-state events coalesce | No | Diagnostic plus representative single fallback; no-op if representative missing | OPTIONAL |

## GravityField Optional Cue Governance

Required set:

- `GravityFieldAudioCueCatalog.RequiredOneShotV1` is empty.
- `GravityFieldAudioMap.ValidateRequiredCuesOrThrow` currently delegates to `ValidateOrThrow` only.
- `GameplayPresentationAudioConfigValidationTests.ValidateOrThrow_DoesNotRequireGravityOptionalCues` asserts an empty gravity map is valid when used through config validation.

Why the required set is empty:

- Current architecture treats GravityField cue playback as optional Sfx one-shot feedback.
- `Docs/Architecture/GravityField-LockedTarget-Presentation-Policy.md` explicitly says `LockedBox` audio is optional and missing binding is a no-op.
- No current doc promotes `Activated` or `Expired` to production-required content.

Planner-emittable cue surface:

- `Activated`
- `Expired`
- `LockedBox`

Canonical map authored entries:

- `Activated` only.

Missing optional behavior:

- The controller resolves every request through `TryResolveOptional`.
- Missing binding returns false and no-ops.
- `LockedBox` attached playback targets the locked box entity when possible; otherwise it falls back to 2D if a binding exists.

| Cue | Runtime Can Emit? | Authored? | Current Missing Behavior | Proposed Requirement |
|---|---:|---:|---|---|
| `Activated` | Yes | Yes | Missing optional no-op | OPTIONAL, or REQUIRED only after product confirms activation feedback must always ship |
| `Expired` | Yes | No | Missing optional no-op | OPTIONAL or DEFER |
| `LockedBox` | Yes | No | Missing optional no-op | OPTIONAL per current architecture |

## Required / Optional / Disabled Decision Table

Decision tag meanings:

- REQUIRED: runtime production path emits it, and missing content is a content defect.
- OPTIONAL: play it when authored; missing content is normal.
- DISABLED: current product explicitly does not play it; an authored binding may be drift.
- DEFER: product/content decision is needed.
- TEST_ONLY: required only in fixtures.

| Lane | Cue or Moment | Decision |
|---|---|---|
| Action | Push/Flip `Windup` | REQUIRED |
| Action | Push/Flip `AssistOutOfRange`, `NoTarget`, `Invalid` | REQUIRED |
| Action | `Execute`, `Recovery` | OPTIONAL |
| Action | `Contact`, `ImpactEnemy`, `Blocked` | REMOVED from action-audio vocabulary |
| Enemy | Authored movement/death cues on production enemy profiles | REQUIRED |
| Enemy | BlackEye `Active`, `ProjectileImpact` | REQUIRED |
| Enemy | DrSaturn/Nebulous `Windup`, `Active`, `Recover` | REQUIRED |
| Enemy | JumpChaserAstra `Landing` | REQUIRED |
| Enemy | RocketFace `ChargeActiveLoop` | REQUIRED |
| Enemy | RocketFace one-shot `Active` | DISABLED if loop-only charge active is confirmed; otherwise DEFER |
| Enemy | SecBot `StationaryActive` | REQUIRED |
| Enemy | Current non-authored `PassiveContact` on JP/DrSaturn/Nebulous | DISABLED |
| Enemy | Non-archetype cue/profile combinations | DISABLED or DEFER by archetype |
| TileFeature | `ButtonActivated` | REQUIRED |
| TileFeature | Authored non-required cues | OPTIONAL |
| TileFeature | Missing enum/planner cues without canonical map entries | OPTIONAL or DEFER |
| TileFeature | `TileFeatureOnBurst`, `TileFeatureOffBurst` | OPTIONAL |
| GravityField | `Activated` | OPTIONAL or DEFER for promotion |
| GravityField | `Expired` | OPTIONAL or DEFER |
| GravityField | `LockedBox` | OPTIONAL |

## Implementation Options

Option A: strengthen existing optional flags.

- Works for `GameplayActionAudioProfile` and `EnemyAudioProfile` entries that already have `IsOptional`.
- Does not represent a missing entry as intentionally optional or disabled.
- Weak fit for TileFeature/Gravity maps because those maps have no entry-level optional flag and required state lives in catalogs.

Option B: add cue requirement tables.

- Add lane-owned requirement metadata such as `CueRequirement.Required`, `Optional`, and `Disabled`.
- Keep the metadata in each lane, not shared runtime.
- Best fit for TileFeature and GravityField maps.
- For action audio, the key must include `GameplayActionKind + GameplayActionAudioMoment`.

Option C: add archetype-specific requirement profiles.

- Add an enemy-owned requirement surface such as `EnemyAudioCueRequirementProfile`.
- This is the best fit for enemy audio because cue requirements differ by archetype/profile.
- It can express required, optional, and disabled cue sets without forcing every enum cue onto every enemy profile.

Recommended direction: combine B and C. Use lane-owned requirement tables for action, TileFeature, and GravityField; use enemy archetype/profile-specific requirements for enemy audio.

## Test Plan

Investigation PR:

- `git diff --check`

Implementation PR:

- `git diff --check`
- `./run_tests.sh core`

Targeted tests to add/update in the implementation PR:

- Action profile requirement tests for canonical player required/optional/deferred coverage.
- Enemy profile requirement tests that enforce exact required and disabled cue sets per production prefab/profile.
- TileFeature map requirement tests that keep `ButtonActivated` required and explicitly mark optional/disabled cues.
- GravityField map requirement tests that preserve empty required set unless product promotes `Activated`, `Expired`, or `LockedBox`.
- Existing architecture tests must continue to reject generic dispatchers and keep action/enemy profiles out of `GameplayPresentationAudioConfig`.

## Next PR Proposal

1. Treat removed action cues (`Contact`, `ImpactEnemy`, and `Blocked`) as closed deletion scope, not deferred optional governance.
2. Add lane-owned requirement metadata without changing shared audio runtime semantics.
3. Add enemy archetype requirement profiles or an equivalent enemy-owned requirement table.
4. Add tests that fail when a REQUIRED cue is missing or a DISABLED cue is authored.
5. Keep optional cue no-op behavior only where an explicit OPTIONAL decision exists.
