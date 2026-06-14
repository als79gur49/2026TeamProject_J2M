# Enemy Audio Requirement Test And Asset Review

Date: 2026-06-08 KST

This report covers production asset wiring, policy/binding coverage, tests, validation evidence, and remaining risks for the `EnemyAudioRequirementPolicy` / `EnemyAudioRequirementBinding` implementation.

## Summary

Decision: **PASS with WARN follow-up**

Production policy and binding assets match the implementation plan. Every expected production requirement asset exists with a `.meta` file and generated GUID. All production binding assets have empty overrides. Repository smoke tests validate production policy/binding assets and require exactly one binding per production `EnemyAudioProfile`.

No blocking asset or test issue was found.

Main risk: Startis now authors `PassiveContact`. This is expected by the policy, but it is an authored content coverage change if the cue was previously absent. Runtime missing cue no-op policy remains unchanged.

## Production Roots

Decision: **PASS**

Expected roots exist:

```text
Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/AudioRequirementPolicies/
Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/AudioRequirementBindings/
```

## Policy Assets

Decision: **PASS**

Cue values:

```text
Move=1, Death=2, Windup=3, Landing=4, Active=5, Recover=6,
ForwardCellImpact=7, ChargeActiveLoop=8, StationaryActive=9, PassiveContact=10
```

| Policy | Required cues | Optional cues | GUID | Decision |
|---|---|---|---|---|
| `EnemyAudioRequirementPolicy_Mover` | `Move`, `Death` | empty | `8e936a47608645e1aa41787b4ea2d7a4` | PASS |
| `EnemyAudioRequirementPolicy_JumpChaser` | `Move`, `Landing`, `Death` | empty | `fdee262c72e94d699b85ca4adf6cd64c` | PASS |
| `EnemyAudioRequirementPolicy_ProjectileShooter` | `Move`, `Active`, `ForwardCellImpact`, `Death` | empty | `51ef8f05b88447fe9ad909c95f74cdcc` | PASS |
| `EnemyAudioRequirementPolicy_GravityFieldUtility` | `Move`, `Windup`, `Active`, `Recover`, `Death` | empty | `ad4f71cf3d1644a6ba3fadc1c36ea56a` | PASS |
| `EnemyAudioRequirementPolicy_ChargeLoop` | `Move`, `ChargeActiveLoop`, `Death` | empty | `9a9419b7c7d54d56b5e309bc7bac39e2` | PASS |
| `EnemyAudioRequirementPolicy_StationaryCadence` | `Move`, `StationaryActive`, `Death` | empty | `fb87deded6c14efe9ac2d6cfcd37e435` | PASS |
| `EnemyAudioRequirementPolicy_PassiveContact` | `Move`, `PassiveContact`, `Death` | empty | `27ccb278b67a4602940fa0e2f13cf013` | PASS |
| `EnemyAudioRequirementPolicy_Summoner` | `Move`, `Active`, `Death` | empty | `5de286f88873438d8cd726329c5387ad` | PASS |

Policy-specific checks:

- PASS: optional lists are all empty.
- PASS: `ChargeLoop` does not require or optionalize one-shot `Active`.
- PASS: `GravityFieldUtility` includes `Active`.
- PASS: `PassiveContact` includes `PassiveContact`.
- PASS: `ProjectileShooter` includes `ForwardCellImpact`.

## Binding Assets

Decision: **PASS**

All expected bindings exist, all have `.meta` files, and all overrides are empty.

| Binding | Target profile | Policy | Binding GUID | Decision |
|---|---|---|---|---|
| `EnemyAudioRequirementBinding_WallFollowerSun` | `EnemyAudioProfile_WallFollowerSun` | `Mover` | `41094f4cd4704d3eb846ec9ca5fe3747` | PASS |
| `EnemyAudioRequirementBinding_JumpChaserAstra` | `EnemyAudioProfile_JumpChaserAstra` | `JumpChaser` | `4773c27f1b08458b945fc40216a2648a` | PASS |
| `EnemyAudioRequirementBinding_BlackEye` | `EnemyAudioProfile_BlackEye` | `ProjectileShooter` | `f42fe1a53ec34bb8af5c347761e8dfea` | PASS |
| `EnemyAudioRequirementBinding_DrSaturn` | `EnemyAudioProfile_DrSaturn` | `GravityFieldUtility` | `3f8e943a93514872ace32b67505d0037` | PASS |
| `EnemyAudioRequirementBinding_UtilitySummoner` | `EnemyAudioProfile_UtilitySummoner` | `Summoner` | `06ada9546dc644658322d113380ef98a` | PASS |
| `EnemyAudioRequirementBinding_Nebulous` | `EnemyAudioProfile_Nebulous` | `GravityFieldUtility` | `59d1dce71b7d42239440ad09bb29c512` | PASS |
| `EnemyAudioRequirementBinding_RocketFace` | `EnemyAudioProfile_RocketFace` | `ChargeLoop` | `7432084c68b14f61a72190daf547243a` | PASS |
| `EnemyAudioRequirementBinding_SecBot` | `EnemyAudioProfile_SecBot` | `StationaryCadence` | `60259abf3dcd415cb015641233f47190` | PASS |
| `EnemyAudioRequirementBinding_Startis` | `EnemyAudioProfile_Startis` | `PassiveContact` | `c799a6d984b74e38ba7e5e99376bff04` | PASS |

Binding-specific checks:

- PASS: DrSaturn target is `EnemyAudioProfile_DrSaturn`, not old `EnemyAudioProfile_LockNearbyBoxesDrS`.
- PASS: RocketFace binding uses `ChargeLoop`; one-shot `Active` remains implicit Disabled.
- PASS: Startis binding uses `PassiveContact`; `PassiveContact` binding exists in the profile.
- PASS: UtilitySummoner uses `Summoner`; expected authored cues are `Move`, `Active`, `Death`.
- PASS: no test/fixture/sample profile is targeted by a production binding.

## Production Profile Coverage

Decision: **PASS**

Repository smoke tests cover this contract:

- `AudioRepositoryAssetSmokeCoreTests.EnemyAudioProfiles_RepositoryAssets_ValidateRequirementBindings`
- `AudioLaneMapRepositorySmokeTests.AppendEnemyAudioRequirementValidationFailures`

Those tests:

- load production `EnemyAudioProfile`, `EnemyAudioRequirementPolicy`, and `EnemyAudioRequirementBinding` assets;
- call `ValidateOrThrow()` on policies and bindings;
- fail bindings that target non-production profiles;
- require each production `EnemyAudioProfile` to have exactly one binding;
- reject legacy `EnemyAudioRequirementProfile` full-matrix assets under production roots.

## Legacy Artifact Scan

Decision: **PASS**

Commands:

```text
rg -n "EnemyAudioRequirementProfile_LockNearbyBoxesDrS|EnemyAudioRequirementProfile_|LockNearbyBoxesDrS" Assets Docs ProjectSettings Packages
find Assets -name 'EnemyAudioRequirementProfile_*.asset' -o -name '*LockNearbyBoxesDrS*'
```

| Hit | Path | Context | Allowed? | Action |
|---|---|---|---|---|
| `EnemyAudioRequirementProfile_*` | `Docs/Refactor/EnemyAudioRequirement-GovernanceAudit.md` | Historical note saying full-matrix draft is superseded | Yes | None |
| `LockNearbyBoxesDrS` | `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyAnimator_LockNearbyBoxesDrS.controller` | Animator controller name | Yes, out-of-scope animator residue | None |

No active production requirement asset named `EnemyAudioRequirementProfile_*` was found.

## Startis PassiveContact

Decision: **WARN**

Observed asset state:

- `EnemyAudioProfile_Startis` entries: `Move`, `Death`, `PassiveContact`.
- `PassiveContact` uses `Starteeth_Move_Def` (`2b7a7ffe90864641bf00acfca6737656`).
- The referenced definition is `AudioCategory.Sfx` and `loop: 0`.
- Binding policy is null (`rid: -2` empty managed reference), matching the current audio binding policy reservation.

Interpretation:

- PASS: runtime missing cue no-op policy is unchanged.
- WARN: authored content coverage changed for Startis `PassiveContact` if this binding was newly added. A passive-contact signal that previously resolved to missing cue no-op can now play the authored definition.

## RocketFace ChargeActiveLoop

Decision: **PASS**

Observed asset state:

- `EnemyAudioRequirementBinding_RocketFace` uses `EnemyAudioRequirementPolicy_ChargeLoop`.
- Effective required set is `Move`, `ChargeActiveLoop`, `Death`.
- One-shot `Active` is not required or optional; it is implicit Disabled.
- `EnemyAudioProfile_RocketFace` entries are `Move`, `ChargeActiveLoop`, `Death`.
- `ChargeActiveLoop` references `RocketFace_ChargeActiveLoop_Def`.
- `RocketFace_ChargeActiveLoop_Def` is `AudioCategory.Sfx` and `loop: 1`.
- Profile attachment slot is `charge-active-loop`.

Tests:

- `EnemyAudioRequirementBindingTests.ChargeActiveLoopRequired_RequiresLoopDefinitionAndAttachment`
- `EnemyAudioRuntimeTests.EnemyAudioProfile_ValidateOrThrow_RejectsInvalidEntries`
- `EnemyAudioRuntimeTests.EnemyAudioProfile_ValidateOrThrow_AllowsAttachedChargeActiveLoop`
- `EnemyAudioRuntimeTests.PrefabExpectations` includes RocketFace `Move`, `ChargeActiveLoop`, `Death` and excludes one-shot `Active`.

## DrSaturn GravityFieldUtility

Decision: **PASS**

Observed asset state:

- `EnemyAudioProfile_DrSaturn` path and GUID are preserved:
  - path: `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/AudioProfiles/EnemyAudioProfile_DrSaturn.asset`
  - GUID: `42ebae281b3c4fffa43d495c3a98dd73`
- `EnemyAudioRequirementBinding_DrSaturn` targets that profile.
- Binding policy is `EnemyAudioRequirementPolicy_GravityFieldUtility`.
- Effective required set is `Move`, `Windup`, `Active`, `Recover`, `Death`.
- `Active` binding is present and references `DrSaturn_GravityField_Active_Def`.
- Current architecture docs describe DrSaturn by identity/GravityField-oriented utility role, not old LockNearbyBoxes-only naming.

## Test Coverage Review

Decision: **PASS with WARN**

| Area | Coverage | Decision |
|---|---|---|
| Policy duplicate required | `EnemyAudioRequirementPolicyTests.DuplicateRequiredCue_Fails` | PASS |
| Policy duplicate optional | `EnemyAudioRequirementPolicyTests.DuplicateOptionalCue_Fails` | PASS |
| Required/optional overlap | `EnemyAudioRequirementPolicyTests.CueInRequiredAndOptional_Fails` | PASS |
| Unknown/non-runtime cue | `EnemyAudioRequirementPolicyTests.UnknownCue_Fails` | PASS |
| Implicit disabled | `EnemyAudioRequirementPolicyTests.UnspecifiedCue_IsImplicitDisabled` | PASS |
| RuntimeCues public surface freeze | `EnemyAudioCueCatalog_RuntimeCues_PublicSurface_IsReviewed` | PASS |
| Missing target profile | `EnemyAudioRequirementBindingTests.MissingTargetProfile_Fails` | PASS |
| Missing policy | `EnemyAudioRequirementBindingTests.MissingPolicy_Fails` | PASS |
| Required missing binding | `RequiredCueMissingBinding_Fails` | PASS |
| Optional missing binding | `OptionalCueMissingBinding_Passes` | PASS |
| Implicit Disabled with binding | `ImplicitDisabledCueWithBinding_Fails` | PASS |
| Disabled override with binding | `DisabledOverrideWithBinding_Fails` | PASS |
| Override require/optional/disable | `OverrideCanRequireCue`, `OverrideCanOptionalizeCue`, `OverrideCanDisableCue` | PASS |
| Duplicate override | `DuplicateOverrideCue_Fails` | PASS |
| ChargeActiveLoop loop + attachment | Binding and profile validation tests | PASS |
| Production policy/binding assets validate | Repository smoke tests | PASS |
| One binding per production profile | Repository smoke tests | PASS |
| Legacy full-matrix artifact rejected | Repository smoke tests | PASS |
| Config excludes enemy profile/policy/binding | `AudioArchitectureTests.GameplayPresentationAudioConfig_ExcludesOtherOwnership` | PASS |
| Enemy requirement types enemy-lane-owned | `AudioArchitectureTests.EnemyAudioRequirementPolicy_IsEnemyLaneOwned`, `EnemyAudioRequirementBinding_IsEnemyLaneOwned` | PASS |
| Shared runtime cue-name isolation | `AudioArchitectureTests.SharedAudioRuntime_DoesNotReferenceEnemyAudioCueNames` | PASS |
| Optional + invalid authored binding | `EnemyAudioRequirementBindingTests.OptionalCueWithInvalidAuthoredBinding_Fails` | PASS |

## Validation Results

Decision: **PASS**

Commands actually run:

```text
git diff --check
./run_tests.sh core --filter EnemyAudioRequirementBindingTests
./run_tests.sh core --filter EnemyAudioRequirementPolicyTests
./run_tests.sh core
```

Results:

| Command | Result |
|---|---|
| `git diff --check` before report edits | PASS, no output |
| `./run_tests.sh core --filter EnemyAudioRequirementBindingTests` | PASS, EditMode `12 total / 0 failed`, PlayMode `0 total / 0 failed` |
| `./run_tests.sh core --filter EnemyAudioRequirementPolicyTests` | PASS, EditMode `6 total / 0 failed`, PlayMode `0 total / 0 failed` |
| `./run_tests.sh core` | PASS, EditMode `183 total / 0 failed`, PlayMode `33 total / 0 failed` |

Runner notes:

- Stratification governance runs in soft mode and emitted existing category/placement warnings.
- Targeted filtered runs emitted expected moving-average count-drop warnings.
- These warnings did not fail the executed lanes.

Not run:

| Lane | Reason |
|---|---|
| `./run_tests.sh ui` | Not run. This review did not change UI code, UI tests, UI scene assets, or UI docs. |
| `./run_tests.sh full` | Not run. Full/broad lane was not requested and documented baseline is red. |

## Issues

### FAIL

None.

### WARN

| Item | Detail | Action |
|---|---|---|
| Startis authored content coverage | `PassiveContact` is authored and can now play where missing cue previously no-oped. Runtime missing cue no-op policy is unchanged. | State this explicitly in PR/report wording. |
| PR body not inspected | `gh` unavailable, no PR URL/number provided. | Review PR body separately if merge approval depends on wording. |

### NEEDS_FOLLOWUP

| Item | Detail |
|---|---|
| Generated InitTestScene files | `Assets/InitTestScene...unity` files contain generated test inventory names, are not tracked by Git, and are already ignored by `.gitignore` line 88. They are not production requirement assets; no EnemyAudioRequirement PR cleanup is needed. |

## Final Judgment

Decision: **Approve**

Production policy/binding assets, profile coverage, tests, guardrails, and core validation support the intended governance model. No request-changes issue was found.
