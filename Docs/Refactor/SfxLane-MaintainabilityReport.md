# SFX Lane Maintainability Report

## Summary

Decision: `OK_CURRENT` for lane separation; `TEST_GAP` for full asset smoke over every lane map/profile; `REFACTOR_REQUIRED` for production `_Test` names and host configuration field growth.

The project should not collapse SFX into a generic dispatcher. Current maintainability comes from feature-local vocabularies and maps/profiles:

```text
presentation signal
  -> lane-specific planner/controller
  -> lane-specific semantic/cue/moment
  -> lane-specific map/profile
  -> AudioBinding
  -> AudioDefinition
  -> AudioClip
```

Shared runtime remains unaware of gameplay semantics.

## Lane Table

| Lane | Event Source | Planner/Controller | Map/Profile | Binding Type | Category | Validation | Add New Cue Steps | Risk |
|---|---|---|---|---|---|---|---|---|
| Gameplay core one-shot | `TickResult.PresentationData` damage/exit facts | `GameplayAudioRequestPlanner` / `GameplayAudioPresentationController` | `GameplayAudioMap_CampaignV1.asset` | `AudioBinding` | `Sfx`, one-shot | required six semantics, duplicate/null/category loop checks | add semantic enum, catalog descriptor, planner, map entry, tests, docs | high governance cost by design |
| Gameplay action | `PlayerActionSignals`, `PlayerActionAttemptSignals` | `GameplayActionAudioRequestPlanner` / `GameplayActionAudioPresentationController` | `Player_S1_GameplayActionAudioProfile.asset` | `AudioBinding` via SerializeReference | `Sfx`, one-shot | duplicate/category/loop; optional null warning | add enum/planner moment or profile entry; update prefab/profile/tests | optional no-op can hide content miss |
| Enemy one-shot/loop | enemy presentation facts and final entity state | `EnemyAudioRequestPlanner` / `EnemyAudioPresentationController` / `EnemyChargeLoopAudioPresentationController` | prefab-local `EnemyAudioProfile_*` + `EnemyAudioRequirementPolicy_*` / `EnemyAudioRequirementBinding_*` | `AudioBinding` | `Sfx`; `ChargeActiveLoop` must loop and attach | duplicate/null/category; loop cue requires loop + attachment; sparse policy/binding required/optional/implicit-disabled validation | add cue enum/planner branch/profile entry/runtime-cue catalog/policy tests | explicit optional no-op only |
| Block | flip/box slide presentation facts | `BlockAudioRequestPlanner` / `BlockAudioPresentationController` | `BlockAudioMap_PlayerSounds.asset` | `AudioBinding` | `Sfx`, one-shot | required `FlipLanding`, `BoxSlideSolidStop`, `BoxSlideStarted` | enum/catalog/planner/map/tests | production naming normalized |
| Player locomotion | locomotion presentation facts | `PlayerLocomotionAudioRequestPlanner` / `PlayerLocomotionAudioPresentationController` | `PlayerLocomotionAudioMap_PlayerSounds.asset` | `AudioBinding` | `Sfx`, one-shot/loop timing via controller | required cue validation | enum/catalog/planner/map/tests | production naming normalized |
| Topology | topology motion facts | `TopologyAudioRequestPlanner` / `TopologyAudioPresentationController` | `TopologyAudioMap_ObjectSounds.asset` | `AudioBinding` | `Sfx`, one-shot | required cue validation | enum/catalog/planner/map/tests | low |
| Gravity field | enemy utility/gravity facts | `GravityFieldAudioRequestPlanner` / `GravityFieldAudioPresentationController` | `GravityFieldAudioMap_ObjectSounds.asset` | `AudioBinding` | `Sfx`, one-shot | required/optional map validation | enum/catalog/planner/map/tests | optional no-op for non-required cues |
| Tile feature | `TilePresentationRequest` | `TileFeatureAudioRequestPlanner` / `TileFeatureAudioPresentationController` | `TileFeatureAudioMap_ObjectSounds.asset` | `AudioBinding`, plus reason-specific bindings | `Sfx`, one-shot | required `ButtonActivated`, optional cues, burst fallback | request mapping, cue enum/catalog, map/tests | optional cues may silently not play |
| UI SFX | UI flow/local widget/HUD | `IUiAudioPort` / `UiAudioPortAdapter` | `UiAudioCueMap_V1.asset` | `AudioBinding` | `Ui`, one-shot | explicit all 18 cue entries, no attachment, no loops, no policy | enum, map entry, flow usage, tests | docs can stale |

## Gameplay Core One-Shot

Vocabulary owner: `GameplayAudioSemanticCatalog`.

Current required set:

- `PlayerDamage`
- `EnemyDamage`
- `EntityExitItemConsume`
- `EntityExitBoxDestroy`
- `EntityExitEnemyDeath`
- `EntityExitOutOfBounds`

Current planner emits from `TickResult.PresentationData.PlayerDamageSignals`, `PlayerDeathSignals`, `EnemyDamageSignals`, and `EntityExitSignals`. It does not read `WorldState`, `IAudioService`, or maps.

Map/planner parity:

- map has all six required semantics.
- planner can produce all six.
- no extra enum semantic is required without map coverage.

Decision: `OK_CURRENT`.

## Gameplay Action Audio

Vocabulary owners:

- `GameplayActionKind`: `Push`, `Flip`
- `GameplayActionAudioMoment`: `Windup`, `Execute`, `Recovery`, `AssistOutOfRange`, `NoTarget`, `Invalid`
- removed action-audio moments: `Contact`, `ImpactEnemy`, and `Blocked`

Profile evidence:

- `Assets/_Features/Gameplay/Gameplay_ActionAudio/Profiles/Player_S1_GameplayActionAudioProfile.asset`
- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/Player_S1.prefab`

Runtime no-op policy:

- missing owner view: no-op.
- missing `GameplayActionAudioAuthoring`: no-op.
- missing profile entry: no-op.
- duplicate entry or invalid binding: fail-fast in validation.

Intentional overlap:

- action-side `ImpactEnemy` was removed from action-audio vocabulary; core `EnemyDamage` remains in the core one-shot lane.
- there is no generic duplicate suppression by default.

Decision: `EXTENSION_READY` for profile-local cue additions, `CONTENT_GOVERNANCE_REQUIRED` for new action kinds.

## Enemy Audio

Vocabulary:

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

Authoring model:

```text
Enemy view prefab
  -> EnemyAudioAuthoring
  -> EnemyAudioProfile_*
  -> AudioBinding
```

`ChargeActiveLoop` is a special persistent loop cue and must use looping definition plus attachment slot. Other cues are SFX one-shots. Missing authoring/profile/cue can no-op at runtime, but production `EnemyAudioRequirementPolicy_*` plus sparse `EnemyAudioRequirementBinding_*` assets classify missing profile cues as required content, intentional optional no-op, or implicit disabled.

Decision: `OK_CURRENT`; production profile completeness policy is covered by enemy requirement policies/bindings and repository smoke validation.

## Block / Locomotion / Topology / Gravity / Tile Feature

These lanes use dedicated maps and controllers. They share `AudioBindingDiagnostics` but not vocabulary.

Required cue validation exists for:

- `BlockAudioCueCatalog.RequiredOneShotV1`
- `PlayerLocomotionAudioCueCatalog.RequiredOneShotV1`
- `TopologyAudioCueCatalog.RequiredOneShotV1`
- `GravityFieldAudioCueCatalog.RequiredOneShotV1`
- `TileFeatureAudioCueCatalog.RequiredOneShotV1`

Tile feature currently requires only `ButtonActivated`; other cues such as `ExitOpened`, `MoonBlockGenerated`, and burst cues are optional. Missing burst cue logs through diagnostic sink and falls back to one representative single request.

Decision: `OK_CURRENT`, with `TEST_GAP` for optional cue governance.

## UI SFX

Current `UiAudioCueId` set:

- `NavigateForward`
- `NavigateBack`
- `Confirm`
- `Cancel`
- `Select`
- `Toggle`
- `AdjustValueCommit`
- `ChanceGain`
- `ChanceLoss`
- `LastChance`
- `ObjectiveComplete`
- `TopologyShift`
- `PrimaryMenuCommand`
- `StageLaunch`
- `KeyboardMove`
- `GameClear`
- `StageClear`
- `LevelFailed`

`UiAudioCueMap_V1.asset` has one explicit binding per cue. Tests freeze this as 18 entries. Any old 7-cue documentation is stale.

UI SFX uses `AudioCategory.Ui`, therefore `AudioChannel.Ui`. Its effective user-facing mix follows `Master` and `Sfx` volume/mute plus hidden `Ui` state. `Bgm`, `Voice`, and `Ambience` are not part of that dependency.

Decision: `OK_CURRENT`; `DOC_MISMATCH` if stale docs remain.

## Duplicate and Reuse Findings

Observed intentional reuse:

- UI cues reuse shared UI click/select definitions for several feedback sounds.
- stage BGM keys `stage-0-2`, `stage-2-2`, `stage-3-2`, `stage-4-2` reuse earlier stage profiles.
- tile feature activated/deactivated barricade entries reuse the same object sound definition.

Observed governance risk:

- several production runtime assets include `_Test` in their names.
- tests pin asset paths, so rename requires coordinated test updates.

## Generic Dispatcher Ban Rationale

A generic dispatcher would move semantic meaning into strings or shared runtime, which would break:

- required semantic validation.
- feature-local vocabulary ownership.
- category/loop policy per lane.
- prefab-local action/enemy authoring.
- UI hidden-channel policy.
- BGM ownership separation.

Future lanes should add typed cue/profile layers or a grouped `GameplayPresentationAudioConfig`, not a string dispatcher.
