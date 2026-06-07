# VFX Phase 6D Visibility Semantic Split Baseline

Date: 2026-06-07

## 1. Conclusion

| Item | Result |
| --- | --- |
| Semantic split needed | Yes, as a report-only baseline before any behavior cleanup |
| Immediate blocker | None found in Phase 6D inventory |
| Behavior cleanup candidates | Keep `VisibleSurfaceAllowed`, `InactiveFaceExplicitlyAllowed`, `PresentationOnly`, and `DefaultGameplay` definitions separate; review projection helper naming before Phase 6E |
| Tests | VFX, lifecycle, topology preservation, core, and UI lanes passed on current revision; known separate red remains separate |
| Production behavior | Not changed in Phase 6D |
| Known separate red | PlayerMovement, WorldSnapshot, and TickReplay filtered failures match the known separate counts |

## 2. Usage Inventory

| Location | Policy | Current meaning | Suspicious | Action |
| --- | --- | --- | --- | --- |
| `GameplayVfxVisibilityMode` | all modes | Canonical enum vocabulary for gameplay VFX visibility | no | keep definitions distinct |
| `GameplayVfxVisibilityPolicy.ResolveEffectiveMode` | `DefaultGameplay` | Explicit default maps entity anchors to semantic-active checks and cell anchors to active-face checks | no | keep as authored default and missing-binding fallback |
| `GameplayVfxVisibilityPolicy.ResolveFallbackDefaultGameplay` | `DefaultGameplay` | Missing binding fallback source | no | keep explicit source diagnostics |
| `GameplayVfxVisibilityPolicy.Evaluate` | `VisibleSurfaceAllowed` | Allows visible-surface projection opt-in after source semantic checks | candidate for clearer docs/tests | do not merge with inactive-face opt-in |
| `GameplayVfxVisibilityPolicy.Evaluate` | `InactiveFaceExplicitlyAllowed` | Allows inactive-face opt-in after source semantic checks | candidate for clearer docs/tests | do not treat as generic presentation bypass |
| `GameplayVfxVisibilityPolicy.Evaluate` | `PresentationOnly` | Strong presentation bypass | no Phase 6D behavior change | keep Phase 6C diagnostics-only treatment |
| `GameplayVfxHostCellAnchorProjector.ShouldAllowVisibleSurfaceProjection` | `VisibleSurfaceAllowed`, `InactiveFaceExplicitlyAllowed`, `PresentationOnly` | Projection helper permits non-active-face projection for three explicit modes | naming can blur surface vs inactive semantics | inventory for Phase 6E; no production change |
| `GameplayVfxHostAnchorResolver` | `policy.VisibilityMode` | Resolves anchors using binding policy effective mode | no | keep binding-resolved path |
| `GameplayForwardCellProjectileVfxController` | binding policy modes | Projectile source/target visibility and projection use authored binding policy | no | preserve projectile tests |
| `BoxSlideSolidStopVfxCommandBuilder` tests | `VisibleSurfaceAllowed` | Planner coverage proves visible-surface opt-in is needed for off-active target | no | preserve planner tests |
| `PresentationMotionFollowingVfxController` | binding/fallback source | Follower visibility diagnostics distinguish binding policy from fallback default | no | preserve Phase 6B/6C diagnostics |
| `VfxBindingRuntimePolicy` | `DefaultGameplay` default | Runtime policy default parameter remains normal gameplay visibility | no | do not delete default |
| Production binding assets | `visibilityMode: 0` | Current authored sample remains `DefaultGameplay` only | no immediate split issue | no asset edit in Phase 6D |
| Docs Phase 6A/6B/6C | all modes | Prior reports already warn not to collapse semantics | no | keep as provenance |

## 3. Semantic Definition

| Policy | Meaning | Allowed usage | Misuse risk |
| --- | --- | --- | --- |
| `DefaultGameplay` | Normal gameplay VFX visibility; cell anchors use active gameplay face and entity anchors use semantic-active visibility | authored default binding and missing-binding fallback | deleting or treating all defaults as missing-binding would change behavior |
| `VisibleSurfaceAllowed` | Explicit opt-in for visible-surface projection | terrain/projectile/planner cases that need visible-surface target projection | can be mistaken for inactive-face or presentation bypass |
| `InactiveFaceExplicitlyAllowed` | Explicit opt-in for inactive-face cue playback while preserving source semantic gates | inactive-face-specific cue contracts | can be overused as generic PresentationOnly substitute |
| `PresentationOnly` | Strong presentation bypass, now diagnosed by Phase 6C | topology/helper and explicitly inventoried presentation-only cases | generic gameplay cue use remains misuse candidate, not Phase 6D cleanup |

## 4. Test Coverage

| Test | Protected semantic | Reinforcement needed |
| --- | --- | --- |
| `GameplayVfxBindingPolicyTests` | Default/fallback source split, visible-surface vs inactive-face allow reasons, PresentationOnly diagnostics | Add Phase 6E tests only if behavior cleanup starts |
| `GameplayVfxForwardCellProjectileTests` | Projectile default suppression, `VisibleSurfaceAllowed` target opt-in, PresentationOnly bypass remains diagnostics-only | Existing coverage is adequate for report-only baseline |
| `GameplayVfxBoxSlideSolidStopPlannerTests` | Planner visible-surface opt-in protection | Existing coverage is adequate |
| `GameplayVfxEnemyMotionAttachedFollowerTests` | Binding-resolved policy use and PresentationOnly misuse diagnostics without blocking | Existing coverage is adequate |
| `GameplayVfxMotionFollowingTests` | Follower lifecycle preservation | Existing coverage is adequate |
| `GameplayVfxLifecycleTests` | Topology helper and hard-clear preservation | Existing coverage is adequate |
| `TopologyTransitionPostFxTests` | Topology post-fx preservation outside gameplay VFX cleanup | Existing coverage is adequate |
| `TopologyVisualBridgeVisibilityControllerTests` | Topology bridge preservation outside gameplay VFX cleanup | Existing coverage is adequate |

## 5. Validation Evidence

| Command | Result | Evidence |
| --- | --- | --- |
| `git diff --check` | pass | whitespace |
| `./run_tests.sh core` | pass | core-editmode 160/0, core-feature-editmode 59/0, core-playmode 34/0 |
| `./run_tests.sh ui` | pass | ui-editmode 651/0 |
| `./run_tests.sh core --filter GameplayVfxBindingPolicyTests` | pass | core-feature-editmode 32/0 |
| `./run_tests.sh full --filter GameplayVfxForwardCellProjectileTests` | pass | full-editmode 29/0 |
| `./run_tests.sh full --filter GameplayVfxBoxSlideSolidStopPlannerTests` | pass | full-editmode 12/0 |
| `./run_tests.sh full --filter GameplayVfxEnemyMotionAttachedFollowerTests` | pass | full-editmode 62/0 |
| `./run_tests.sh full --filter GameplayVfxMotionFollowingTests` | pass | full-editmode 19/0 |
| `./run_tests.sh full --filter GameplayVfxLifecycleTests` | pass | full-editmode 55/0 |
| `./run_tests.sh full --filter GameplayVfxArchitectureTests` | pass | full-editmode 31/0 |
| `./run_tests.sh full --filter GameplayVfxLegacyOldPathCleanupTests` | pass | full-editmode 16/0 |
| `./run_tests.sh full --filter TopologyTransitionPostFxTests` | pass | full-editmode 6/0 |
| `./run_tests.sh full --filter TopologyVisualBridgeVisibilityControllerTests` | pass | full-editmode 8/0 |

## 6. Known Separate Red

| Command | Result | Phase 6D blocker |
| --- | --- | --- |
| `./run_tests.sh full --filter PlayerMovementPlayModeTests` | red; full-playmode 42 total / 15 failed | no |
| `./run_tests.sh full --filter WorldSnapshotAndPresentationTests` | red; full-editmode 100 total / 3 failed | no |
| `./run_tests.sh full --filter TickReplayDeterminismTests` | red; full-editmode 65 total / 40 failed | no |

## 7. Phase 6E Options

| Option | Scope | Risk | Recommended |
| --- | --- | --- | --- |
| A | Rename or document projection helper vocabulary so visible-surface projection and inactive-face opt-in are not read as one semantic | low/medium | yes, start with tests/docs |
| B | Add focused tests proving `InactiveFaceExplicitlyAllowed` does not bypass source semantic state in projectile/planner paths | medium | yes before behavior cleanup |
| C | Change production filtering/projection behavior | high | no until Phase 6E has explicit behavior proposal |
| D | Combine PresentationOnly enforcement with visible/inactive semantic cleanup | high | no, keep separate from Phase 6D/6E |
