# VFX Phase 6B Binding-resolved Visibility Policy Centralization

Date: 2026-06-07

## 1. Conclusion

| Item | Result |
| --- | --- |
| Implementation scope | Binding-resolved visibility policy source and diagnostics for gameplay VFX planning and motion follower runtime paths |
| Behavior change | Narrow, intended fix: new motion/attached follower starts no longer run a pre-start fallback visibility check before binding resolve |
| DefaultGameplay handling | Kept as explicit missing-binding fallback; not deleted |
| Authored binding policy handling | Binding runtime policy is the first source for final resolved visibility when available |
| PresentationOnly handling | No hardening, fail-fast, allowlist expansion, or new bypass behavior added |
| VisibleSurfaceAllowed / InactiveFaceExplicitlyAllowed | Existing semantics preserved |
| Tests | Broad core/ui passed; VFX and topology targeted filters passed |
| Known separate red | PlayerMovement, WorldSnapshot, and TickReplay filters remain red and are not Phase 6B blockers |
| Next steps | Phase 6C PresentationOnly hardening; Phase 6D semantic split cleanup if needed |

## 2. Baseline

| Command | Result | Notes |
| --- | --- | --- |
| `git status --short --branch` | clean at start | Branch ahead 28 |
| `git diff --stat` | no output at start | clean worktree |
| `git diff --name-status` | no output at start | clean worktree |
| `git diff --check` | pass | no whitespace errors |
| `Docs/Testing/VFX-Phase6A-Visibility-Policy-Resolve-Baseline-2026-06-07.md` tracked | pass | Phase 6A report was already committed/tracked |
| `./run_tests.sh core` | pass | 160/0 EditMode, 53/0 feature EditMode, 34/0 PlayMode |
| `./run_tests.sh ui` | pass | 651/0 UI EditMode |
| `./run_tests.sh core --filter GameplayVfxBindingPolicyTests` | pass | 26/0 feature EditMode |
| `./run_tests.sh full --filter PlayerMovementPlayModeTests` | red | 42 total / 15 failed, known separate |
| `./run_tests.sh full --filter WorldSnapshotAndPresentationTests` | red | 100 total / 3 failed, known separate |
| `./run_tests.sh full --filter TickReplayDeterminismTests` | red | 65 total / 40 failed, known separate |

## 3. Changed Files

| File | Change | Behavior impact | Notes |
| --- | --- | --- | --- |
| `Assets/_Features/Gameplay/Gameplay_Vfx/Runtime/GameplayVfxVisibility.cs` | Added resolved policy/source model and final policy resolver helpers | Centralizes policy source evaluation | `DefaultGameplay` fallback remains explicit |
| `Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs` | Planning visibility now resolves final policy once and records source diagnostics | No filtering semantic change when binding exists/missing | Adds binding/fallback counters |
| `Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/PresentationMotionFollowingVfxController.cs` | Runtime follower diagnostics and start-path binding policy resolution | New follower starts avoid pre-binding fallback gating | Active cached-policy fallback remains explicit |
| `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/GameplayVfxBindingPolicyTests.cs` | Added final policy source/fallback tests | Test-only | Verifies authored default vs fallback default distinction |
| `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/GameplayVfxEnemyMotionAttachedFollowerTests.cs` | Added runtime follower final policy diagnostics test | Test-only | Verifies authored non-default policy reaches runtime |
| `Docs/Architecture/Gameplay-VFX-Governance.md` | Documented visibility policy resolve contract | Docs-only | Defers PresentationOnly hardening |
| `Docs/Testing/VFX-Phase6A-Visibility-Policy-Resolve-Baseline-2026-06-07.md` | Added Phase 6B follow-up note | Docs-only | Keeps Phase 6A report-only framing |

## 4. Binding Resolve Path

| Location | Previous policy source | New policy source | Fallback | Diagnostics |
| --- | --- | --- | --- | --- |
| `GameplayVfxVisibilityPolicy` | Caller passed raw mode/policy | `GameplayVfxResolvedVisibilityPolicy` with source | `ResolveFallbackDefaultGameplay` | Source and fallback flag |
| `GameplayVfxProductionRuntime.FilterByPlanningVisibility` | Binding policy if resolved, otherwise raw `DefaultGameplay` | `ResolveFinalPolicy(request, hasBindingPolicy, policy)` | yes, missing binding only | `PlanningVisibilityBindingResolvedCount`, `PlanningVisibilityFallbackDefaultCount`, last policy |
| `PresentationMotionFollowingVfxController.TryStartAttached` | Binding policy, no source diagnostics | Binding runtime policy via resolver helper | no start fallback; missing binding remains missing binding | binding resolved counter and last policy |
| `PresentationMotionFollowingVfxController.TryStartAttachedFollower` | Binding policy, no source diagnostics | Binding runtime policy via resolver helper | no start fallback; missing binding remains missing binding | binding resolved counter and last policy |
| Active follower visibility checks | Active cached policy or raw `DefaultGameplay` if cache missing | Cached policy or explicit fallback source | yes, cache missing only | fallback counter distinguishes cache miss |

## 5. DefaultGameplay Classification

| Location | Existing meaning | Final meaning | Verdict |
| --- | --- | --- | --- |
| `VfxBindingRuntimePolicy` default constructor parameters | Authored/runtime default when no specific mode provided | Authored default | keep |
| `GameplayVfxVisibilityPolicy.ResolveFallbackDefaultGameplay` | missing-binding default | explicit fallback default | keep |
| `GameplayVfxProductionRuntime` planning visibility | mixed binding/default branch | final resolved policy with source | fixed |
| `GameplayVfxHostCellAnchorProjector` | maps default policy to active gameplay face | policy semantics helper | keep |
| VFX binding assets with `visibilityMode: 0` | authored default | authored default | keep |
| Unit tests using `GameplayVfxVisibilityMode.DefaultGameplay` | fixtures and contract checks | final policy/fallback contract tests | keep/updated |
| Docs mentioning `DefaultGameplay` | inventory/governance | updated to explicit fallback and authored default distinction | keep |
| Unresolved hardcode | none found in Phase 6B touched VFX paths | none | pass |

## 6. PresentationOnly Status

| Location | Meaning | Phase 6B action | Phase 6C candidate |
| --- | --- | --- | --- |
| `VfxBindingDiagnostics` | existing allowlist diagnostics | unchanged | yes, hardening follow-up |
| `GameplayVfxVisibilityPolicy` | bypass visibility gate | unchanged | yes, misuse diagnostics |
| `GameplayVfxHostCellAnchorProjector` | projection helper semantics | unchanged | no behavior change in 6B |
| `GameplayVfxBindingPolicyTests` | allowlist/topology/helper contracts | preserved | yes, extend in 6C |
| `GameplayVfxForwardCellProjectileTests` | existing PresentationOnly projectile contract | preserved | possible misuse audit |
| `GameplayVfxLifecycleTests` | topology helper exemption | preserved | no deletion/hardening in 6B |
| Docs | deferred scope | updated | yes |

## 7. Test Contract Changes

| Test | Previous contract | New contract | Result |
| --- | --- | --- | --- |
| `GameplayVfxBindingPolicyTests` | Binding policy and fallback behavior | Authored default and fallback default are distinct final policy sources | pass, 28/0 feature EditMode |
| `GameplayVfxBindingPolicyTests` | Planner hardcoded default fallback behavior | Planner records final resolved policy source | pass |
| `GameplayVfxEnemyMotionAttachedFollowerTests` | Attached follower binding behavior | Runtime follower diagnostics expose authored final visibility policy | pass, 61/0 EditMode |
| `GameplayVfxForwardCellProjectileTests` | Projectile visibility behavior | unchanged | pass, 29/0 EditMode |
| `GameplayVfxBoxSlideSolidStopPlannerTests` | Planner behavior | unchanged | pass, 12/0 EditMode |
| `GameplayVfxSceneRuntimeRootPlayModeTests` | Runtime root scene creation | unchanged | pass, 5/0 PlayMode |
| `GameplayVfxArchitectureTests` | VFX architecture guards | unchanged | pass, 31/0 EditMode |
| `GameplayVfxLegacyOldPathCleanupTests` | old path cleanup guards | unchanged | pass, 16/0 EditMode |
| `TopologyTransitionPostFxTests` | topology post-fx behavior | unchanged | pass, 6/0 EditMode |
| `TopologyVisualBridgeVisibilityControllerTests` | topology visual bridge visibility | unchanged | pass, 8/0 EditMode |

## 8. Docs Changes

| Document | Change |
| --- | --- |
| `Docs/Architecture/Gameplay-VFX-Governance.md` | Added visibility policy resolve governance: binding runtime policy first, explicit fallback default, PresentationOnly deferred |
| `Docs/Testing/VFX-Phase6A-Visibility-Policy-Resolve-Baseline-2026-06-07.md` | Added Phase 6B follow-up note without changing Phase 6A report-only conclusion |
| `Docs/Testing/VFX-Phase6B-Binding-Resolved-Visibility-Policy-Centralization-2026-06-07.md` | Added this execution report |

## 9. Removal Guard Recheck

| Item | Status |
| --- | --- |
| `Assets/_Features/Gameplay/Gameplay_Vfx/Runtime` | preserved |
| `Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Pool` | preserved |
| `Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Lifecycle` | preserved |
| `Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production` | preserved |
| `GameplayVfxRuntimeRoot.cs` | preserved |
| `GameplayVfxHostDefaultCueMap.asset` | preserved |
| `GameplayVfxCommonEmptyHost.prefab` | preserved |
| topology bridge/post-fx/camera shake | preserved |
| Stage companion assets | preserved |
| enemy/static presentation catalog | preserved |
| EnemyAiProfile / enemy AI runtime definition assets | preserved |

## 10. Final Verification

| Command | Result | Execution count | Evidence classification |
| --- | --- | --- | --- |
| `git diff --check` | pass | n/a | final whitespace check |
| `./run_tests.sh core` | pass | 160/0, 55/0, 34/0 | broad gate |
| `./run_tests.sh ui` | pass | 651/0 | broad UI gate |
| `./run_tests.sh core --filter GameplayVfxBindingPolicyTests` | pass | 28/0 feature EditMode | VFX final policy contract |
| `./run_tests.sh full --filter GameplayVfxForwardCellProjectileTests` | pass | 29/0 EditMode | VFX behavior preservation |
| `./run_tests.sh full --filter GameplayVfxBoxSlideSolidStopPlannerTests` | pass | 12/0 EditMode | VFX behavior preservation |
| `./run_tests.sh full --filter GameplayVfxEnemyMotionAttachedFollowerTests` | pass | 61/0 EditMode | VFX runtime diagnostics |
| `./run_tests.sh full --filter GameplayVfxSceneRuntimeRootPlayModeTests` | pass | 5/0 PlayMode | runtime root preservation |
| `./run_tests.sh full --filter GameplayVfxArchitectureTests` | pass | 31/0 EditMode | architecture guard |
| `./run_tests.sh full --filter GameplayVfxLegacyOldPathCleanupTests` | pass | 16/0 EditMode | cleanup guard |
| `./run_tests.sh full --filter TopologyTransitionPostFxTests` | pass | 6/0 EditMode | topology preservation |
| `./run_tests.sh full --filter TopologyVisualBridgeVisibilityControllerTests` | pass | 8/0 EditMode | topology preservation |
| `rg -n "DefaultGameplay" ...` | reviewed | n/a | residue classified |
| `rg -n "PresentationOnly" ...` | reviewed | n/a | residue classified |

## 11. Known Separate Red

| Command | Result | Phase 6B blocker |
| --- | --- | --- |
| `./run_tests.sh full --filter PlayerMovementPlayModeTests` | red, 42 total / 15 failed | no; PlayerMovement/topology input cluster not touched |
| `./run_tests.sh full --filter WorldSnapshotAndPresentationTests` | red, 100 total / 3 failed, `WorldSnapshot` constructor reflection failures | no; WorldSnapshot cluster not touched |
| `./run_tests.sh full --filter TickReplayDeterminismTests` | red, 65 total / 40 failed, determinism hash/replay baseline failures | no; TickReplay cluster not touched |

## 12. Next Steps

| Phase | Scope |
| --- | --- |
| Phase 6C | PresentationOnly allowlist and misuse diagnostics hardening |
| Phase 6D | VisibleSurfaceAllowed vs InactiveFaceExplicitlyAllowed semantic split cleanup if needed |
| Separate | PlayerMovement full-filter red |
| Separate | WorldSnapshot full-filter red |
| Separate | TickReplay full-filter red |
