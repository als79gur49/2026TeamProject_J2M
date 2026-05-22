# Gameplay VFX Migration Test Classification 2026-05-22

## Scope

This note classifies the current Gameplay VFX migration test surface after the same-revision `./run_tests.sh full` runs and the first fixture/oracle cleanup slices.

Evidence:

- Command: `./run_tests.sh full`
- Result XML: `TestResults/wsl-unity-full-editmode.xml`
- Initial Unity Full EditMode: `4563 total / 141 failed / 1 skipped`
- Initial VFX-related failures: `82`
- After binding/composition fixture cleanup: `4550 total / 112 failed`
- After binding/composition fixture cleanup VFX-related failures: `53`
- After binding/composition fixture cleanup binding-composition failures: `0`
- After attached-follower fixture/oracle cleanup: `4550 total / 91 failed`
- After attached-follower fixture/oracle cleanup VFX-related failures: `32`
- After attached-follower fixture/oracle cleanup attached-follower failures: `0`
- After visibility fixture cleanup: `4550 total / 84 failed`
- After removed-default-authoring contract pruning: `4536 total / 70 failed`
- After removed-default-authoring contract pruning VFX-related failures: `11`
- After removed-default-authoring contract pruning enemy damage/death/front-face-shield/utility-windup failures: `0`
- Full PlayMode: not reached because Full EditMode failed

This note does not claim full-lane recovery or project-wide green.

## Decision Summary

Gameplay VFX migration completion does not mean the VFX runtime, planners, bindings, lifecycle, or ownership tests should be deleted. The migration converted old presenter playback into a canonical Gameplay VFX lane. The canonical lane still needs regression coverage.

The cleanup target is narrower:

- keep canonical runtime, planning, lifecycle, authoring, binding, anchor, pool, and presentation-only boundary tests
- rewrite stale tests whose oracle still expects removed default authoring or old rollout behavior
- rename `*MigrationTests` that now guard canonical behavior
- consolidate repeated flag/off/no-old-fallback tests into fewer architecture guards
- remove only temporary rollout evidence once equivalent canonical guards exist

## Classification Rules

| Class | Meaning | Action |
|---|---|---|
| Keep | Test guards current canonical VFX lane behavior | Keep, fix if failing |
| Rewrite/Rename | Behavior should stay covered, but migration wording or oracle is stale | Rename file/test and update assertions |
| Consolidate | Same rollout or old-fallback policy is repeated across many cues | Replace with shared policy tests plus cue-specific smoke |
| Remove Candidate | Test only proves a completed migration step and has no unique current invariant | Delete after equivalent canonical guard exists |

## Current Failure Buckets

| Bucket | Failed | Classification |
|---|---:|---|
| Gameplay VFX binding composition failed | 0 | Resolved by updating synthetic binding prefabs to satisfy the current `ModelRoot` prefab contract |
| Missing expected VFX request/handle/count | 5 | Remaining flip motion, forward-cell projectile, and tile-feature completion expectation drift |
| Visibility policy expected suppression failed | 0 | Resolved by correcting stale inactive-face and source-visibility fixtures |
| Attached follower fixture/oracle failures | 0 | Resolved by passing semantic visibility context from the fixture and removing identity-specific reattach assertions |
| Lifecycle / persistent tail/release drift | 4 | Keep; runtime lifecycle regressions |
| Forward-cell projectile request/lifecycle drift | 2 | Keep/Fix; materialize request projections before comparing and verify active-flight lifecycle expectations |
| Tile-feature/gravity-field assertion drift | 2 | Rewrite/Rename; current lane behavior should stay covered without stale migration wording |
| Fragile document phrase guard | 0 | Resolved by rewriting to structural doc guard |

## File-Level Classification

| File | Failed | Classification | Rationale |
|---|---:|---|---|
| `GameplayVfxArchitectureTests.cs` | 0 | Rewritten | Large phrase-lock test was replaced with section/key invariant checks. |
| `GameplayVfxAuthoringBindingTests.cs` | 0 | Keep | Current authoring and removed-default-authoring policy coverage. |
| `GameplayVfxBindingPolicyTests.cs` | 0 | Keep | Binding/request compatibility rules remain canonical. |
| `GameplayVfxBindingResolverTests.cs` | 0 | Keep | Profile/default binding precedence remains canonical. |
| `GameplayVfxCompositionTests.cs` | 0 | Keep | Composition itself is canonical. Synthetic binding prefabs now satisfy the current direct `ModelRoot` prefab policy. |
| `GameplayVfxControllerIsolationTests.cs` | 0 | Keep | Ensures VFX controller isolation from authority and unrelated host concerns. |
| `GameplayVfxCoreValueTests.cs` | 0 | Keep | Value identity/ordering tests remain canonical. |
| `GameplayVfxGameObjectPoolTests.cs` | 0 | Keep | Pool behavior remains runtime-critical. |
| `GameplayVfxHostAnchorArchitectureTests.cs` | 0 | Keep | Prevents host anchor ownership leaking into core/runtime surfaces. |
| `GameplayVfxHostAnchorResolverTests.cs` | 0 | Keep | Visibility and anchor policy remain current runtime invariants; stale inactive-face fixtures are resolved. |
| `GameplayVfxLifecycleTests.cs` | 4 | Keep/Fix | Persistent/tail/release lifecycle is canonical. Exceptions here are real risk, not migration residue. |
| `GameplayVfxMotionFollowingTests.cs` | 0 | Keep | Attached follower ownership is canonical for MotionTrack-following VFX. |
| `GameplayVfxParameterizedMotionRuntimeTests.cs` | 0 | Keep | Parameterized motion runtime remains canonical for death/flip motion VFX. |
| `GameplayVfxPoolArchitectureTests.cs` | 0 | Keep | Pool ownership and dependency boundaries remain canonical. |
| `GameplayVfxPrefabValidationTests.cs` | 0 | Keep | Prefab contract prevents authoring regressions. |
| `GameplayVfxBoxSlideTrailAdapterTests.cs` | 0 | Keep | This is augmentation/canonical adapter coverage, not migration residue. |
| `GameplayVfxBoxSlideSolidStopPlannerTests.cs` | 0 | Keep | Visibility and planner filtering remain current cue behavior; stale inactive-face fixtures are resolved. |
| `GameplayVfxEnemyJumpLandingDustTests.cs` | 0 | Keep | Current production cue coverage. |
| `GameplayVfxEnemyJumpTargetTests.cs` | 0 | Keep | Current production cue coverage. |
| `GameplayVfxEnemyMotionAttachedFollowerTests.cs` | 0 | Keep | Fixture now supplies the semantic visibility context used by production runtime, and reattach assertions no longer require a specific replacement instance identity. |
| `GameplayVfxForwardCellProjectileTests.cs` | 2 | Keep/Fix | Current projectile/forward-cell VFX behavior. Some assertions may need materialization before comparing LINQ iterators. |
| `GameplayVfxFlagRolloutPolicyTests.cs` | 0 | Consolidate | Useful during rollout, but all current flags are default-on. Keep one canonical flag policy guard and remove cue-by-cue rollout table duplication later. |
| `GameplayVfxLegacyOldPathCleanupTests.cs` | 0 | Consolidate | Keep old-fallback absence as a single architectural guard. Remove phrase-heavy and duplicated cue-specific cleanup assertions after rename/coverage consolidation. |
| `GameplayVfxPlayerDamageMigrationTests.cs` | 0 | Rewrite/Rename | Player damage VFX is now canonical. Binding fixture failures are resolved; rename remains useful later. |
| `GameplayVfxEnemyDamageMigrationTests.cs` | 0 | Rewrite/Rename | Removed-default-authoring tests remain; synthetic default-host playback checks were pruned, and missing-binding diagnostics now provide source visibility context. |
| `GameplayVfxEnemyDeathMigrationTests.cs` | 0 | Rewrite/Rename | Removed synthetic default-host death playback and snapshot-count checks; default authoring removal and no-old-path coverage remain. |
| `GameplayVfxEnemyDeathMotionMigrationTests.cs` | 0 | Rewrite/Rename | Passing but still named as migration. Rename to death motion runtime/parameterized motion tests. |
| `GameplayVfxBoxExitMigrationTests.cs` | 0 | Rewrite/Rename | Box destroy/item consume VFX are canonical. Binding-composition fixture failures are resolved; rename can happen in a later no-behavior-change slice. |
| `GameplayVfxFlipDestroySelfMotionMigrationTests.cs` | 2 | Rewrite/Rename | Composition failures are gone. Remaining failures are flag/off behavior and coexistence count drift. |
| `GameplayVfxFlipImpactBurstMigrationTests.cs` | 1 | Rewrite/Rename | Composition failures are gone. Remaining failure is duplicate guard expectation drift. |
| `GameplayVfxFlipImpactMotionTrackGateTests.cs` | 0 | Consolidate | Gate language is still useful only until MotionTrack ownership is fully canonicalized. Merge into architecture/MotionTrack tests when renamed. |
| `GameplayVfxFrontFaceShieldMigrationTests.cs` | 0 | Rewrite/Rename | Pruned synthetic active/block/windup playback and source-profile override checks for removed default authoring; missing-binding diagnostics now pass source visibility context. |
| `GameplayVfxReservedHookMigrationTests.cs` | 0 | Rewrite/Rename | Reserved hook binding fixture failure is resolved. Rename remains useful later. |
| `GameplayVfxTileFeatureGravityFieldMigrationTests.cs` | 2 | Rewrite/Rename | TileFeature/GravityField VFX lane is current behavior. Keep topology completion and PR28 controller absence checks, but avoid stale legacy symbol phrasing. |
| `GameplayVfxUtilityWindupMigrationTests.cs` | 0 | Rewrite/Rename | Pruned synthetic persistent lifecycle/profile checks for removed default authoring; missing-binding diagnostics now pass source visibility context. |

## Delete Candidates

Do not delete whole runtime suites yet. The safe delete candidates are individual assertions or rows, not entire files:

- exhaustive document phrase assertions in `GameplayVfxArchitectureTests.GovernanceDocument_ExistsAndDeclaresBoundary`
- repeated cue-by-cue flag default table assertions after one canonical default/off semantics test exists
- repeated per-cue "flag off no old fallback" tests after one shared old-fallback absence guard plus cue-specific runtime smoke exists
- migration approval wording checks such as Tier 1/2/3 rollout text once rollout history is archived
- tests whose only assertion is that a completed migration was documented, when code-level old-path absence is already covered

## Fix Priority

1. Resolve remaining request/handle count drift.
   - Remaining VFX failures are now flip motion, forward-cell projectile, lifecycle, and tile-feature/gravity-field rows.
   - Treat these as runtime/oracle classification work, not binding composition failures.

2. Keep removed-default-authoring guards narrow.
   - Enemy damage, enemy death, front-face shield, and utility windup should prove removed default authoring, missing-binding diagnostics, and old-path absence, not synthetic default-host playback.

3. Rename migration suites.
   - Prefer names such as `GameplayVfxBoxExitRuntimeTests`, `GameplayVfxFrontFaceShieldRuntimeTests`, `GameplayVfxUtilityWindupRuntimeTests`, `GameplayVfxPlayerDamageRuntimeTests`, and `GameplayVfxEnemyDeathRuntimeTests`.

4. Consolidate rollout/cleanup tests.
   - Keep one `GameplayVfxCanonicalFlagPolicyTests` and one `GameplayVfxOldPathAbsenceTests`.
   - Remove duplicated rollout-history phrase checks after the new guards are green.

## Recommended Next Slice

Create the next bounded VFX test cleanup slice with no runtime behavior change:

- materialize LINQ projections in request comparison tests where the failure is iterator-vs-array shape rather than behavior
- inspect lifecycle tail/release rows next, because they remain canonical runtime behavior
- rename `*MigrationTests` classes/files to current runtime names after the current-oracle failures are separated
- keep synthetic binding prefabs aligned with the direct `ModelRoot` prefab validation contract only for cues whose runtime playback contract still exists

After rename/oracle cleanup, runtime fixes or asset/binding restoration should be attempted only for failures that still represent current canonical behavior.
