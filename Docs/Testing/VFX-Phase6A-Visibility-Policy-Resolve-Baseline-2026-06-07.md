# VFX Phase 6A Visibility Policy Resolve Baseline Report

## 1. Conclusion

| Item | Result |
| --- | --- |
| Phase 6B ready | Yes, for diagnostics-only and binding-resolved policy centralization planning |
| Biggest risk | `DefaultGameplay` literals are mixed between legitimate missing-binding fallback and policy resolve paths, so blind removal would change behavior |
| Immediate candidates | Expose/report final resolved visibility policy in diagnostics; centralize binding-resolved policy handoff where controllers already resolve bindings |
| Deferred | `PresentationOnly` hardening beyond existing allowlist checks; semantic behavior changes for `VisibleSurfaceAllowed` / `InactiveFaceExplicitlyAllowed` |
| Known separate red | `PlayerMovementPlayModeTests` full, `WorldSnapshotAndPresentationTests` full, `TickReplayDeterminismTests` full |

Phase 6A is report-only. No production behavior, command shape, binding asset, prefab/material, lifecycle/pool, topology bridge, post-fx, camera shake, scene, or stage companion asset was changed.

## 2. Baseline

| Command | Result | Notes |
| --- | --- | --- |
| `git status --short --branch` | pass | `## pr/unit-validation-chase-blocked-reaction...origin/pr/unit-validation-chase-blocked-reaction [ahead 27]` |
| `git diff --stat` | pass | no output before report edit |
| `git diff --name-status` | pass | no output before report edit |
| `git diff --check` | pass | no output |
| `./run_tests.sh core` | pass | core EditMode 160/0, core feature EditMode 53/0, core PlayMode 34/0 |
| `./run_tests.sh ui` | pass | UI EditMode 651/0 |
| `./run_tests.sh core --filter GameplayVfxBindingPolicyTests` | pass | core feature EditMode 26/0; zero-count warnings outside the filter are not used as pass evidence |

Phase 5B rename preflight:

| Item | Result |
| --- | --- |
| tracked `EntityExit*` files | `EntityExitBoxDestroyShrinkVfxCommandBuilder.cs(.meta)` and `EntityExitOutOfBoundsVfxCommandBuilder.cs(.meta)` tracked |
| old exact class/path residue | none from exact old-name search |
| broad old substring search | only new `EntityExit*` names and governance vocabulary hit; not active old-class residue |
| behavior review | current files preserve candidate filters, cue selection, `SourceCloneMotion`, `DestroyShrinkEase`, `Linear`, source/target pose equality, duration constants, duplicate guard, runtime ordering, and sequence id fallback |

## 3. Visibility Usage Inventory

| ID | File / area | Policy | Current meaning | Classification | Recommended action |
| --- | --- | --- | --- | --- | --- |
| V001 | `GameplayVfxVisibility.cs` | all modes | Defines policy enum, effective-mode resolution, allow/block reasons | canonical policy | keep; Phase 6B may add diagnostics around final effective mode |
| V002 | `VfxBindingRuntimePolicy.cs` | `DefaultGameplay` default parameter | Runtime binding policy defaults to normal gameplay visibility when not authored | fallback default | keep; document as default, not deletion target |
| V003 | `VfxBindingDefinitionAsset.cs` | serialized `visibilityMode` | Binding asset carries authored visibility into `CreateRuntimePolicy()` | authored binding policy | keep; Phase 6B should prefer this resolved value wherever binding exists |
| V004 | `VfxBindingDiagnostics.cs` | `PresentationOnly` | Emits visibility mode info and requires explicit allowlist marker for `PresentationOnly` | PresentationOnly allowlist | keep; defer stronger enforcement to Phase 6C |
| V005 | `GameplayVfxHostCellAnchorProjector.cs` | `DefaultGameplay`, visible/inactive/presentation modes | Maps `DefaultGameplay` to active-face projection, allows explicit projection opt-ins | policy semantics | keep; do not merge visible surface and inactive face semantics |
| V006 | `GameplayVfxProductionRuntime.FilterByPlanningVisibility` | binding policy or `DefaultGameplay` | Uses binding-resolved policy when present; missing binding falls back to documented `DefaultGameplay` | fallback default + centralization candidate | keep fallback; Phase 6B can expose final resolved policy |
| V007 | `GameplayVfxProductionRuntime` direct command paths | `policy.VisibilityMode` | Production paths resolve binding before anchor/visibility evaluation | binding-resolved path | keep; verify diagnostics in Phase 6B |
| V008 | `PresentationMotionFollowingVfxController` | active policy or `DefaultGameplay` | Active follower policy is used when cached; fallback only when policy missing | fallback default | keep; candidate for final-policy diagnostics |
| V009 | `GameplayForwardCellProjectileVfxController` | `policy.VisibilityMode` | Projectile source/target projection and visibility use binding policy | binding-resolved path | keep; existing tests cover visible-surface and PresentationOnly behavior |
| V010 | `BoxSlideSolidStopVfxCommandBuilder` | `policy.VisibilityMode` | Builder evaluates stopper-cell visibility from binding policy | binding-resolved path | keep; covered by planner test |
| V011 | VFX binding assets under `Gameplay_Vfx/Authoring/Bindings` | `visibilityMode: 0` | Current sampled production bindings are authored as `DefaultGameplay` | authored default | keep; not a removal signal |
| V012 | `GameplayVfxBindingPolicyTests` | all modes | Test fixtures exercise binding policy, fallback, allowlist, diagnostics, and semantic split | test fixture coverage | keep; expand if Phase 6B changes diagnostics |
| V013 | `GameplayVfxLifecycleTests` | `PresentationOnly`, `DefaultGameplay` | Protects topology helper and hard-clear interaction with visibility policy | topology helper allowlist | keep separate from gameplay VFX default policy |
| V014 | docs / historical testing notes | mixed | Phase notes mention visibility policy work and known baselines | docs-only | keep active docs clear; do not treat historical notes as runtime residue |

Generated `Assets/InitTestScene*.unity` search hits and unrelated `DefaultGameplayLocomotion` hits are excluded from the primary inventory because they are runner/generated or locomotion feature-flag vocabulary, not Gameplay VFX visibility policy.

## 4. Binding Resolve Path

| Location | Authored binding read | Hardcoded default | Diagnostics | Judgment |
| --- | --- | --- | --- | --- |
| `VfxBindingDefinitionAsset.BuildRuntimePolicy()` | yes | only field default when asset value is `0` | authoring validation reports visibility mode | correct source of authored policy |
| `VfxProfileAsset` / `VfxCueMapAsset` | yes | no direct visibility override | profile/cue map validation indirectly uses runtime policy | correct composition path |
| `GameplayVfxProductionRuntime.FilterByPlanningVisibility` | yes, when resolver succeeds | yes, only when binding missing | no final resolved-policy surface beyond current behavior | legitimate fallback; Phase 6B diagnostics candidate |
| `GameplayVfxProductionRuntime` command playback paths | yes | no direct override after resolve | command carries `ResolvedVfxPlaybackCommand.Policy` | binding-resolved path |
| `GameplayVfxPresentationController` | yes | no playback when missing binding | missing binding blocks command path | binding-resolved path |
| `PresentationMotionFollowingVfxController` active followers | yes, from cached active policies | yes, if active policy cache misses | missing binding/owner counters exist | fallback should remain explicit |
| `GameplayForwardCellProjectileVfxController` | yes | no direct override after resolve | missing binding count path | binding-resolved path |
| `GameplayVfxHostCellAnchorProjector` | receives resolved mode from caller | maps `DefaultGameplay` to active face | none local | semantics helper, not binding resolver |

No evidence was found that a resolved non-default authored binding is being intentionally overwritten by `DefaultGameplay` in the inspected production paths. The practical gap is observability: final resolved policy is not consistently surfaced as diagnostics at every controller seam.

## 5. Policy Semantics

| Policy | Meaning | Allowed use | Misuse risk |
| --- | --- | --- | --- |
| `DefaultGameplay` | Normal gameplay visibility; entity anchors resolve to semantic-active checks, cell anchors resolve to active gameplay face | binding default, missing-binding fallback, ordinary gameplay cues | deleting all direct uses would remove documented fallback and change behavior |
| `VisibleSurfaceAllowed` | Allows projection to currently visible surface when explicitly authored | visible-surface cue opt-in, projectile/terrain style visibility tests | must not become inactive-face or generic bypass |
| `InactiveFaceExplicitlyAllowed` | Allows an inactive-face cue only when explicitly authored | inactive-face-specific cue opt-in | must not bypass entity semantic state, front-face inactive, or autonomy suppression |
| `PresentationOnly` | Strong bypass of gameplay visibility gate | topology/helper/presentation-only cases with allowlist diagnostics | generic gameplay cue use must remain suspicious and validated |

## 6. Test Coverage

| Test | Protected contract | Current result | Phase 6B need |
| --- | --- | --- | --- |
| `GameplayVfxBindingPolicyTests` | binding owns visibility policy, planner uses binding policy, missing binding fallback, semantic split, PresentationOnly allowlist, diagnostics | pass, 26/0 via core filter | add diagnostics assertions if final resolved policy is exposed |
| `GameplayVfxForwardCellProjectileTests` | projectile target visibility honors `DefaultGameplay`, `VisibleSurfaceAllowed`, and `PresentationOnly` | covered by prior inventory; not rerun separately in Phase 6A | rerun if projectile controller is touched in Phase 6B |
| `GameplayVfxBoxSlideSolidStopPlannerTests` | box slide solid stop uses binding visibility policy | covered by prior inventory; not rerun separately in Phase 6A | rerun if builder/controller is touched |
| `GameplayVfxEnemyMotionAttachedFollowerTests` | attached follower behavior and binding policy interaction | inventory reviewed; no direct Phase 6A change | rerun if follower policy cache changes |
| `GameplayVfxLifecycleTests` | topology helper, PresentationOnly, hard-clear, visibility suspension | inventory reviewed; no direct Phase 6A change | rerun if lifecycle diagnostics or PresentationOnly hardening changes |
| `TopologyVisualBridgeVisibilityControllerTests` | topology visual bridge visibility ownership | separated from gameplay VFX policy | keep separate; not a Phase 6B binding-resolve target |
| `TopologyTransitionPostFxTests` | topology post-fx presentation path | separated from gameplay VFX policy | keep separate; not a Phase 6B binding-resolve target |

## 7. Phase 6B Options

| Option | Scope | Risk | Recommendation |
| --- | --- | --- | --- |
| A. diagnostics-only | expose/report final resolved visibility policy in diagnostics or test helpers | low | recommended |
| B. binding resolve centralization | ensure controller/planner handoff consistently uses binding-resolved policy and explicit missing-binding fallback | medium | recommended with focused tests |
| C. PresentationOnly allowlist hardening | stricter allowlist/warning/fail-fast for PresentationOnly | medium/high | defer to Phase 6C |
| D. semantic split cleanup | behavior-sensitive changes around visible surface vs inactive face | high | defer until tests are expanded first |

Recommended Phase 6B scope is Option A+B only. Do not combine it with PresentationOnly hardening or semantic behavior changes.

## 8. Removal-Protected Items Reconfirmed

| Area | Status |
| --- | --- |
| `Gameplay_Vfx/Runtime` | keep |
| `Gameplay_VfxHost/Runtime/Pool` | keep |
| `Gameplay_Vfx/Runtime/Lifecycle` | keep |
| `Gameplay_VfxHost/Runtime/Production` | keep |
| `GameplayVfxRuntimeRoot.cs` | keep |
| `GameplayVfxHostDefaultCueMap.asset` | keep |
| `GameplayVfxCommonEmptyHost.prefab` | keep |
| `GameplayVfxProductionRuntime.cs` | keep |
| `GameplayVfxRuntimeInstaller.cs` | keep |
| topology bridge/post-fx/camera shake helpers | keep; separate from gameplay VFX visibility policy |
| stage companion assets | keep |

## 9. Known Separate Red

| Command | Result | Phase 6A blocker |
| --- | --- | --- |
| `./run_tests.sh core --filter PlayerMovementPlayModeTests` | pass, core PlayMode 13/0 | no |
| `./run_tests.sh full --filter PlayerMovementPlayModeTests` | red, full PlayMode 42 total / 15 failed | no, known separate |
| `./run_tests.sh full --filter WorldSnapshotAndPresentationTests` | red, full EditMode 100 total / 3 failed; `WorldSnapshot` constructor reflection failures | no, known separate |
| `./run_tests.sh full --filter TickReplayDeterminismTests` | red, full EditMode 65 total / 40 failed; determinism hash/replay baseline failures | no, known separate |

The known full-filter reds are not used as Phase 6A pass evidence and are not claimed fixed.

## 10. Next Step

Phase 6B: binding-resolved visibility policy centralization with diagnostics-first evidence.
