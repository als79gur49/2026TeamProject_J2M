# VFX Gap2 EntityExit Presentation Admission Contract 실행 보고서

## 0. 기준점

| 항목 | 결과 |
|---|---|
| branch | `pr/vfx-orphan-asset-cleanup...origin/pr/vfx-orphan-asset-cleanup` (`ahead 16, behind 1`) |
| HEAD | `81e0e541` |
| worktree | clean before Gap2 edits; `git diff --stat`, `git diff --name-status`, and `git diff --check` were empty |
| Gap1 commit 포함 | yes; `81e0e541 test: VFX - FrontFaceInactive enemy death clone contract 고정` |

## 1. 결론

- 구현 범위: VFX runtime path family admission contract를 docs/tests로 고정.
- production behavior 변경 여부: 없음.
- path family 분리: gameplay request, EntityExit/death direct presentation, impact/disposition direct presentation, split/mixed BoxDestroySmoke, topology helper lane을 별도 family로 문서화.
- tests: command shape와 cue/source-clone admission assertions를 contract vocabulary로 보강.
- docs: `Gameplay-VFX-Governance.md`에 canonical family contract 추가.
- known separate red: touched targeted lanes에서는 없음. full lane baseline red는 별도 기준으로 유지하며, targeted 결과와 섞어 green으로 주장하지 않음.
- Gap3 진행 가능 여부: 가능. Gap3에서 non-default visibility policy를 direct EntityExit path에 적용하기 전 이 family admission contract를 참조해야 함.

## 2. 변경 파일

| 파일 | 변경 내용 | production 영향 |
|---|---|---|
| `Docs/Architecture/Gameplay-VFX-Governance.md` | presentation admission family contract 추가 | 없음 |
| `Docs/Testing/VFX-Gap2-EntityExit-Presentation-Admission-Contract-2026-06-08.md` | Gap2 실행 보고서 추가 | 없음 |
| `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/GameplayVfxArchitectureTests.cs` | family contract 문서 guard 추가 | 없음 |
| `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/GameplayVfxEnemyDeathMotionMigrationTests.cs` | Gap1 death motion assertion message 보강 | 없음 |
| `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/GameplayVfxBoxExitMigrationTests.cs` | BoxDestroyShrink exit fact admission test name/message 보강 | 없음 |
| `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/GameplayVfxReservedHookMigrationTests.cs` | OutOfBounds/ImpactTransient admission test name/message 보강 | 없음 |
| `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/GameplayVfxFlipDestroySelfMotionMigrationTests.cs` | FlipDestroySelfMotion impact/disposition admission test 추가 | 없음 |

## 3. Path Family Contract

| Family | Path | admission owner | common visibility gate 여부 | 테스트 |
|---|---|---|---|---|
| Gameplay request VFX | ForwardCellProjectile / planner / follower | source-target visibility evaluator | yes | `GameplayVfxForwardCellProjectileTests` |
| EntityExit / Death presentation VFX | EnemyDeathMotion / BoxDestroyShrink / OutOfBoundsExit | death/destroy/out-of-bounds presentation fact | no direct common gate | `GameplayVfxEnemyDeathMotionPrefabWithSourceCloneTests`, `GameplayVfxBoxExitRuntimePolicyTests`, `GameplayVfxReservedCueRuntimePolicyTests` |
| Impact / disposition presentation VFX | FlipDestroySelfMotion / ImpactTransientBreak | impact/disposition presentation fact | no direct common gate | `GameplayVfxFlipDestroySelfSourceCloneMotionTests`, `GameplayVfxReservedCueRuntimePolicyTests` |
| Split path | BoxDestroySmoke | immediate planner + delayed projector/direct | mixed | `GameplayVfxBoxExitRuntimePolicyTests`, governance doc guard |
| Topology helper | bridge/post-fx/camera shake | topology visual state / helper context | PresentationOnly helper lane | `GameplayVfxLifecycleTests`, governance doc guard |

## 4. Test Contract

| 테스트 | 검증 내용 | 결과 |
|---|---|---|
| `EnemyDeathMotion_FrontFaceInactiveSource_EmitsSourceCloneFlyAway` | FrontFaceInactive death fact still emits DeathMotion `PrefabWithSourceClone`; ForwardCellProjectile counters stay zero | pass |
| `EntityExitBoxDestroyShrink_UsesExitFactAdmission_NotLiveSourceGate` | BoxDestroy exit fact builds `DestroyShrink` `SourceCloneMotion` with source-to-source pose and existing timing | pass |
| `EntityExitOutOfBounds_UsesExitFactAdmission_NotLiveSourceGate` | OutOfBounds exit fact builds family-specific `OutOfBoundsExit` cue with source-to-source vanish shape | pass |
| `FlipDestroySelfMotion_UsesImpactDispositionAdmission_NotLiveSourceGate` | DestroySelf impact disposition builds `FlipDestroySelfMotion` `SourceCloneMotion` source-to-impact command | pass |
| `ImpactTransientBreak_UsesImpactPresentationAdmission_NotLiveSourceGate` | Impact transient fact builds `ImpactTransientBreak` source-to-impact break/fade command | pass |
| `GameplayVfxGovernance_DocumentsPresentationAdmissionFamilies` | governance doc separates admission families and BoxDestroySmoke split path | pass |

## 5. Regression Guard

| Path | common gate 추가 시 위험 | 이번 contract |
|---|---|---|
| EnemyDeathMotion | FrontFaceInactive fly-away suppression | preserved |
| BoxDestroyShrink | destroy shrink missing | preserved |
| OutOfBoundsExit | exit feedback missing | preserved |
| FlipDestroySelfMotion | destroy-self motion missing | preserved |
| ImpactTransientBreak | impact break missing | preserved |

## 6. Docs 변경

| 문서 | 변경 내용 |
|---|---|
| `Docs/Architecture/Gameplay-VFX-Governance.md` | `Presentation Admission Families` 추가; direct presentation command admission과 common live source/target evaluator를 분리 |
| `Docs/Testing/VFX-Gap2-EntityExit-Presentation-Admission-Contract-2026-06-08.md` | Gap2 implementation/evidence report 추가 |

## 7. 검증 결과

| 명령 | 결과 | evidence |
|---|---|---|
| `git diff --check` | pass | no whitespace errors in tracked diff |
| `./run_tests.sh core --filter GameplayVfxBindingPolicyTests` | pass | core-editmode total=32 failed=0; core-playmode total=0 failed=0 |
| `./run_tests.sh full --filter GameplayVfxForwardCellProjectileTests` | pass | full-editmode total=30 failed=0; full-playmode total=0 failed=0 |
| `./run_tests.sh full --filter GameplayVfxEnemyDeathMotionPrefabWithSourceCloneTests` | pass | full-editmode total=21 failed=0; full-playmode total=0 failed=0 |
| `./run_tests.sh full --filter GameplayVfxParameterizedMotionRuntimeTests` | pass | full-editmode total=64 failed=0; full-playmode total=0 failed=0 |
| `./run_tests.sh full --filter GameplayVfxBoxExitRuntimePolicyTests` | pass | full-editmode total=31 failed=0; full-playmode total=0 failed=0 |
| `./run_tests.sh full --filter GameplayVfxReservedCueRuntimePolicyTests` | pass | full-editmode total=17 failed=0; full-playmode total=0 failed=0 |
| `./run_tests.sh full --filter GameplayVfxFlipDestroySelfSourceCloneMotionTests` | pass | full-editmode total=35 failed=0; full-playmode total=0 failed=0 |
| `./run_tests.sh full --filter GameplayVfxLegacyOldPathCleanupTests` | pass | full-editmode total=16 failed=0; full-playmode total=0 failed=0 |
| `./run_tests.sh full --filter GameplayVfxArchitectureTests` | pass after docs wording fix | initial red was placeholder policy wording; rerun full-editmode total=32 failed=0; full-playmode total=0 failed=0 |
| `./run_tests.sh full --filter GameplayVfxLifecycleTests` | pass | full-editmode total=55 failed=0; full-playmode total=0 failed=0 |

## 8. 금지 항목 재확인

- visibility policy code 변경 없음: yes; production code diff 없음.
- PresentationOnly 강제 우회 없음: yes; docs/tests only assert these direct paths must not be forced through `PresentationOnly`.
- direct common gate 추가 없음: yes; `EvaluateBeforeAnchor`/`EvaluateAfterAnchor` residue search shows only existing projectile/follower/runtime helper uses.
- production runtime 변경 없음: yes; changed files are docs and editmode tests only.
- binding/asset/scene 변경 없음: yes; no asset, prefab, material, scene, stage asset, or `.meta` diff.
- lifecycle/pool/common host 변경 없음: yes; no lifecycle, pool, runtime root, cue map, or common host production diff.
- topology helper 변경 없음: yes; topology helper lane is documented only.

## 9. 다음 단계

- Gap3: production non-default visibility policy coverage.
- Gap3에서 direct EntityExit path에 non-default policy를 적용하기 전 family admission contract를 참조할 것.
