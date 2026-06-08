# Enemy Audio Requirement Policy/Binding Implementation Plan

Date: 2026-06-08 KST

This plan supersedes the earlier full-matrix `EnemyAudioRequirementProfile` draft. The canonical model is archetype-level `EnemyAudioRequirementPolicy` plus sparse `EnemyAudioRequirementBinding`.

## Model

- `EnemyAudioRequirementPolicy` lists `Required` and `Optional` cues only.
- Any runtime cue not named by the policy or a binding override is effective `Disabled`.
- `EnemyAudioRequirementBinding` connects one production `EnemyAudioProfile_*` to one policy and may contain sparse overrides for exceptions.
- Validation stays exhaustive over `EnemyAudioCueCatalog.RuntimeCues`: required missing binding fails, optional missing binding passes, and disabled authored binding fails.
- Runtime missing owner, authoring, profile, and cue behavior remains no-op.

## Production Seed

| Binding | Target Profile | Policy | Effective Required |
| --- | --- | --- | --- |
| `EnemyAudioRequirementBinding_WallFollowerSun` | `EnemyAudioProfile_WallFollowerSun` | `Mover` | `Move`, `Death` |
| `EnemyAudioRequirementBinding_JumpChaserAstra` | `EnemyAudioProfile_JumpChaserAstra` | `JumpChaser` | `Move`, `Landing`, `Death` |
| `EnemyAudioRequirementBinding_BlackEye` | `EnemyAudioProfile_BlackEye` | `ProjectileShooter` | `Move`, `Active`, `ProjectileImpact`, `Death` |
| `EnemyAudioRequirementBinding_DrSaturn` | `EnemyAudioProfile_DrSaturn` | `GravityFieldUtility` | `Move`, `Windup`, `Active`, `Recover`, `Death` |
| `EnemyAudioRequirementBinding_UtilitySummoner` | `EnemyAudioProfile_UtilitySummoner` | `Summoner` | `Move`, `Active`, `Death` |
| `EnemyAudioRequirementBinding_Nebulous` | `EnemyAudioProfile_Nebulous` | `GravityFieldUtility` | `Move`, `Windup`, `Active`, `Recover`, `Death` |
| `EnemyAudioRequirementBinding_RocketFace` | `EnemyAudioProfile_RocketFace` | `ChargeLoop` | `Move`, `ChargeActiveLoop`, `Death` |
| `EnemyAudioRequirementBinding_SecBot` | `EnemyAudioProfile_SecBot` | `StationaryCadence` | `Move`, `StationaryActive`, `Death` |
| `EnemyAudioRequirementBinding_Startis` | `EnemyAudioProfile_Startis` | `PassiveContact` | `Move`, `PassiveContact`, `Death` |

Initial overrides are empty. Optional lists are empty until a product decision needs an intentional optional no-op.

## Guardrails

- Enemy audio remains prefab-local through `EnemyAudioAuthoring -> EnemyAudioProfile_*`.
- Requirement policy/binding assets are enemy-lane-owned content governance metadata and are excluded from `GameplayPresentationAudioConfig`.
- Do not make all `EnemyAudioCue` values globally required.
- Do not add a generic dispatcher, string-key audio lookup, or shared runtime enemy cue dependency.
- Do not change ActionAudio, TileFeatureAudio, GravityFieldAudio, UI, BGM, StageAudio, or shared runtime.
- RocketFace uses `ChargeActiveLoop`; one-shot `Active` remains implicit disabled.

## Tests And Validation

- Unit tests cover policy duplicate/overlap/unknown cue validation and implicit disabled behavior.
- Binding tests cover missing target/policy, required/optional/disabled behavior, overrides, duplicate overrides, and `ChargeActiveLoop` loop/attachment validation.
- Repository smoke tests validate all production policies/bindings, require exactly one binding per production `EnemyAudioProfile`, and reject legacy full-matrix requirement assets in production roots.
- Architecture tests keep policy/binding in the enemy audio assembly and keep `GameplayPresentationAudioConfig` free of enemy profile/requirement ownership.
- Required validation: `git diff --check`, guardrail scans, and `./run_tests.sh core`.
