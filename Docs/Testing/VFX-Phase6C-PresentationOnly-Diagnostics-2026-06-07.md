# VFX Phase 6C PresentationOnly Diagnostics

Date: 2026-06-07

## 1. Conclusion

| Item | Result |
| --- | --- |
| Allowlist inventory | `PresentationOnly` production binding assets are not currently authored; active uses are runtime semantics, tests, and docs |
| Diagnostics added | Runtime classifier distinguishes allowed topology helper context from misuse candidate |
| Behavior change | None intended; `PresentationOnly` still bypasses gameplay visibility gates |
| Misuse candidate | Generic gameplay/projectile/follower `PresentationOnly` requests are counted as diagnostics-only candidates |
| Fail-fast | Deferred; no new production blocking was added |
| Known separate red | PlayerMovement, WorldSnapshot, and TickReplay filters remain separate and must not be hidden |
| Next step | Review diagnostics inventory before considering behavior-sensitive fail-fast |

## 2. PresentationOnly Inventory

| Location | Meaning | Allowlist status | Action |
| --- | --- | --- | --- |
| `GameplayVfxVisibilityPolicy` | `PresentationOnly` visibility bypass | runtime semantic | preserved |
| `GameplayVfxTopologyHelperExemptionPolicy` | topology helper cue/context gate | allowlisted helper context | preserved |
| `VfxBindingDiagnostics` | authoring validation and compatibility diagnostics | explicit helper preferred; legacy name marker warning | hardened diagnostics-only |
| `GameplayVfxProductionRuntime` planning visibility | final policy source evaluation | records misuse/allowed counts | behavior preserved |
| `PresentationMotionFollowingVfxController` | follower start/active visibility evaluation | records misuse/allowed counts | behavior preserved |
| `GameplayForwardCellProjectileVfxController` | projectile visibility evaluation | records misuse/allowed counts | behavior preserved |
| `GameplayVfxForwardCellProjectileTests` | existing projectile bypass fixture | misuse candidate diagnostic | behavior preserved |
| Docs/tests | governance and fixtures | docs-only/test fixture | updated |

## 3. Diagnostics

| Item | Content | Test |
| --- | --- | --- |
| `GameplayVfxPresentationOnlyUsageKind.AllowedTopologyHelper` | topology helper cue with helper topology context | `GameplayVfxBindingPolicyTests` |
| `GameplayVfxPresentationOnlyUsageKind.MisuseCandidate` | non-allowlisted gameplay cue using `PresentationOnly` | `GameplayVfxBindingPolicyTests`, projectile/follower tests |
| authoring legacy marker warning | name-based `PresentationOnly` compatibility marker remains non-blocking | `GameplayVfxBindingPolicyTests` |
| projectile runtime counters | `PresentationOnly` projectile still spawns and records misuse candidate | `GameplayVfxForwardCellProjectileTests` |
| follower runtime counters | `PresentationOnly` follower still starts and records misuse candidate | `GameplayVfxEnemyMotionAttachedFollowerTests` |

## 4. Removal Guard Recheck

| Item | Status |
| --- | --- |
| topology helpers | preserved |
| Gameplay VFX runtime | preserved |
| pool/lifecycle | preserved |
| cue map/common host/runtime root | preserved |
| scene/prefab/material/binding assets | not deleted |

## 5. Final Verification

| Command | Result | Evidence classification |
| --- | --- | --- |
| `git diff --check` | pass | whitespace |
| `./run_tests.sh core` | pass; core-editmode 160/0, core-feature-editmode 59/0, core-playmode 34/0 | broad gate |
| `./run_tests.sh ui` | pass; ui-editmode 651/0 | UI preservation |
| `./run_tests.sh core --filter GameplayVfxBindingPolicyTests` | pass; core-feature-editmode 32/0 | binding/diagnostics contract |
| `./run_tests.sh full --filter TopologyTransitionPostFxTests` | pass; full-editmode 6/0 | topology preservation |
| `./run_tests.sh full --filter TopologyVisualBridgeVisibilityControllerTests` | pass; full-editmode 8/0 | topology preservation |
| `./run_tests.sh full --filter GameplayVfxArchitectureTests` | pass; full-editmode 31/0 | architecture guard |
| `./run_tests.sh full --filter GameplayVfxForwardCellProjectileTests` | pass; full-editmode 29/0 | projectile runtime diagnostics |
| `./run_tests.sh full --filter GameplayVfxBoxSlideSolidStopPlannerTests` | pass; full-editmode 12/0 | VFX planner preservation |
| `./run_tests.sh full --filter GameplayVfxEnemyMotionAttachedFollowerTests` | pass; full-editmode 62/0 | follower runtime diagnostics |

## 6. Known Separate Red

| Command | Result | Phase 6C blocker |
| --- | --- | --- |
| `./run_tests.sh full --filter PlayerMovementPlayModeTests` | red; full-playmode 42 total / 15 failed, matches known separate pattern | no |
| `./run_tests.sh full --filter WorldSnapshotAndPresentationTests` | red; full-editmode 100 total / 3 failed, matches known separate pattern | no |
| `./run_tests.sh full --filter TickReplayDeterminismTests` | red; full-editmode 65 total / 40 failed, matches known separate pattern | no |
