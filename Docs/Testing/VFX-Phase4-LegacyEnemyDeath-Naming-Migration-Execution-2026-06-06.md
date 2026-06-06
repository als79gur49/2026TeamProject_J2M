# VFX Phase 4 LegacyEnemyDeath Naming Migration 실행 보고서

## 1. 결론

- rename 완료: `ParameterizedMotionVfxFadeMode.EnemyDeathFade`, `ParameterizedMotionVfxSamplerMode.EnemyDeathFlyAway`로 active runtime vocabulary를 교체했다.
- behavior 변경 여부: 없음. enemy death motion command, fade curve/timing, sampler trajectory, source clone/common host, binding policy, lifecycle/pool 동작은 유지했다.
- serialized 영향: Unity YAML scene/prefab/asset/controller/anim/material/VFX/shadergraph 검색에서 이전/신규 death naming residue는 0건이었다.
- tests: broad `core`, broad `ui`, required VFX targeted, topology preservation evidence는 통과했다.
- known separate red: `full --filter PlayerMovementPlayModeTests`는 42개 중 15개 실패로 기존 topology/player-movement fixture red와 동일한 범주다.
- 보류: Phase 5 exit-specific command builder naming/consolidation, DefaultGameplay visibility policy resolve 일원화, scene 삭제, PlayerMovement full fixture red 수정.

## 2. Baseline

| 명령 | 결과 | 비고 |
| --- | --- | --- |
| `git status --short --branch` | clean | `pr/unit-validation-chase-blocked-reaction...origin/pr/unit-validation-chase-blocked-reaction [ahead 25]` |
| `git rev-parse --short HEAD` | `4a37f154` | Phase 3B closeout commit |
| `git log --oneline -5` | 확인 | HEAD `4a37f154 refactor: vfx - remove migrated cue serialization flags` |
| `git diff --stat` | clean | 시작 시 변경 없음 |
| `git diff --name-status` | clean | 시작 시 변경 없음 |
| `git diff --check` | pass | 시작 시 whitespace error 없음 |
| `rg enableGameplayVfx.*Migration` | 0건 | `Assets ProjectSettings Packages`, `Assets/Scenes` 모두 0건 |

## 3. 참조 인벤토리

| 기존 이름 | 파일 | 종류 | 조치 |
| --- | --- | --- | --- |
| `LegacyEnemyDeath` | `ParameterizedMotionVfxCommand.cs` | runtime enum/fade switch | `EnemyDeathFade`로 rename, numeric value `4` 유지 |
| `LegacyEnemyDeath` | `EnemyDeathMotionVfxCommand.cs` | runtime adapter | `EnemyDeathFade`를 emit하도록 rename |
| `LegacyEnemyDeath` | `GameplayVfxPooledInstance.cs` | runtime fade/shadow branch | `EnemyDeathFade` 비교로 rename |
| `LegacyEnemyDeathFlyAway` | `ParameterizedMotionVfxCommand.cs` | runtime sampler enum/switch/helper | `EnemyDeathFlyAway`로 rename, numeric value `2` 유지 |
| `LegacyEnemyDeathFlyAway` | VFX/unit tests | tests | enum name, test method, assertion vocabulary 갱신 |
| `LegacyEnemyDeath*` | governance tests | tests | active residue guard를 split literal로 유지 |
| `LegacyEnemyDeath*` | docs | docs | active vocabulary 제거, historical/pre-Phase4 용도로만 기록 |

## 4. Rename Mapping

| 기존 | 신규 | 이유 |
| --- | --- | --- |
| `LegacyEnemyDeath` | `EnemyDeathFade` | behavior-preserving rename이며 active vocabulary에서 `Legacy` 단어만 제거한다. |
| `LegacyEnemyDeathFlyAway` | `EnemyDeathFlyAway` | 현재 enemy death motion path를 유지하면서 sampler 의미를 그대로 드러낸다. |

더 큰 fact-based exit command builder consolidation은 Phase 5로 보류했다.

## 5. Serialized 영향

| 검색 | 결과 | 조치 |
| --- | --- | --- |
| `LegacyEnemyDeath` in scenes | 0건 | migration shim 불필요 |
| `LegacyEnemyDeath` in prefabs/assets | 0건 | reserialize 불필요 |
| `EnemyDeathFlyAway` in serialized files | 0건 | serialized string/name 영향 없음 |

Enum numeric values를 유지했으므로, 향후 Unity가 enum int로 저장한 숨은 reference가 있더라도 semantic mapping은 보존된다.

## 6. Behavior 보존 확인

| 항목 | 결과 |
| --- | --- |
| enemy death motion command | `EnemyDeathMotionVfxCommand` 유지, parameterized motion command shape 유지 |
| fade behavior | curve/timing/alpha/scale math 변경 없음 |
| sampler behavior | ease-out arc, spin, duration branch math 변경 없음 |
| source clone/common host | `GameplayVfxEnemyDeathMotionPrefabWithSourceCloneTests` pass |
| binding policy | `GameplayVfxBindingPolicyTests` pass |
| lifecycle/pool | `GameplayVfxLifecycleTests` pass |
| determinism/presentation isolation | `EnemyViewIsolationTests` pass, replay/snapshot full-filter reds are unrelated baseline failures로 분리 기록 |

## 7. 테스트 변경

| 테스트 | 기존 contract | 새 contract | 결과 |
| --- | --- | --- | --- |
| `GameplayVfxEnemyDeathMotionMigrationTests` | death motion command가 이전 enum을 emit | `EnemyDeathFade`/`EnemyDeathFlyAway` emit | broad `core` pass |
| `GameplayVfxParameterizedMotionRuntimeTests` | 이전 sampler/fade 이름으로 behavior 검증 | 신규 이름으로 동일 behavior 검증 | broad `core` pass |
| `GameplayVfxArchitectureTests` | 이전 active vocabulary residue guard | split literal guard로 active contiguous residue 방지 | broad `core` pass |
| `GameplayVfxLegacyOldPathCleanupTests` | old path cleanup guard | split literal guard로 active contiguous residue 방지 | broad `core` pass |

## 8. Docs 변경

| 문서 | 변경 내용 |
| --- | --- |
| `Docs/Architecture/Gameplay-VFX-Governance.md` | active sampler vocabulary를 `EnemyDeathFlyAway`로 갱신 |
| `Docs/Testing/VFX-Phase3B-Gate-Migration-YAML-Targeted-Evidence-2026-06-06.md` | Phase 4를 historical/pre-Phase4 naming cleanup으로 표현 |
| 이 문서 | Phase 4 rename 결과, serialized 영향, known separate red 기록 |

## 9. 제거 금지 항목 재확인

- Gameplay_Vfx runtime: 삭제 없음
- Gameplay_VfxHost pool/lifecycle/production: 삭제 없음
- `GameplayVfxRuntimeRoot.cs`: 삭제 없음
- `GameplayVfxHostDefaultCueMap.asset`: 삭제 없음
- `GameplayVfxCommonEmptyHost.prefab`: 삭제 없음
- topology bridge/post-fx/camera shake: 수정/삭제 없음
- Stage companion assets: 수정/삭제 없음
- EnemyAiProfile / enemy AI runtime definition assets: 수정/삭제 없음

## 10. 최종 검증

| 명령 | 결과 | 실행 수 | 비고 |
| --- | --- | --- | --- |
| `./run_tests.sh core` | pass | 247 total, 0 failed | broad core |
| `./run_tests.sh ui` | pass | 651 total, 0 failed | broad UI |
| `./run_tests.sh core --filter GameplayVfxFlagRolloutPolicyTests` | pass | 6 total, 0 failed | feature editmode |
| `./run_tests.sh core --filter GameplayVfxBindingPolicyTests` | pass | 26 total, 0 failed | feature editmode |
| `./run_tests.sh core --filter GameplayVfxSceneRuntimeRootPlayModeTests` | pass | 5 total, 0 failed | playmode |
| `./run_tests.sh full --filter GameplayVfxEnemyDeathMotionPrefabWithSourceCloneTests` | pass | 20 total, 0 failed | full editmode |
| `./run_tests.sh full --filter GameplayVfxEnemyMotionAttachedFollowerTests` | pass | 60 total, 0 failed | full editmode |
| `./run_tests.sh full --filter GameplayVfxMotionFollowingTests` | pass | 19 total, 0 failed | full editmode |
| `./run_tests.sh full --filter GameplayVfxLifecycleTests` | pass | 55 total, 0 failed | full editmode |
| `./run_tests.sh core --filter WorldSnapshotAndPresentationTests` | no-match | 0 total | reran full filter |
| `./run_tests.sh full --filter WorldSnapshotAndPresentationTests` | fail | 100 total, 3 failed | unrelated `WorldSnapshot` constructor baseline failure |
| `./run_tests.sh core --filter EnemyViewIsolationTests` | no-match | 0 total | reran full filter |
| `./run_tests.sh full --filter EnemyViewIsolationTests` | pass | 4 total, 0 failed | full editmode |
| `./run_tests.sh core --filter TickReplayDeterminismTests` | no-match | 0 total | reran full filter |
| `./run_tests.sh full --filter TickReplayDeterminismTests` | fail | 65 total, 40 failed | unrelated determinism/replay baseline failure |
| `./run_tests.sh full --filter TopologyTransitionPostFxTests` | pass | 6 total, 0 failed | topology evidence |
| `./run_tests.sh full --filter TopologyVisualBridgeVisibilityControllerTests` | pass | 8 total, 0 failed | topology evidence |
| `git diff --check` | pass | n/a | whitespace check |

## 11. Known Separate Red

| 명령 | 결과 | Phase 4 blocker 여부 |
| --- | --- | --- |
| `./run_tests.sh core --filter PlayerMovementPlayModeTests` | pass, 13/13 | no |
| `./run_tests.sh full --filter PlayerMovementPlayModeTests` | fail, 15/42 | no. Existing topology/player-movement fixture red; Phase 4 diff did not touch that cluster. |

## 12. 다음 단계

- Phase 5: exit-specific command builder naming/consolidation
- Phase 6: DefaultGameplay visibility policy resolve 일원화
- 별도: PlayerMovement full fixture red 수정
- 별도: CombinedGameplayShowcase.unity / TutorialScene.unity scene 삭제는 raw path/GUID/doc/archive policy 재검토 후 진행
