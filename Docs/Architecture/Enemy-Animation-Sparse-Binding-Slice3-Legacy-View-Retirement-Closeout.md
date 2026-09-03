# Enemy Animation Sparse Binding — Slice 3 Legacy View Retirement Closeout

## 상태

- 완료일: 2026-09-04
- disposition: `MigratedBinding 8 / ApprovedNoBinding 2 / Deleted 4 / LegacyBlocked 0`
- live inventory: Driver 10 / Binding 8 / Timing 0
- rollback: 이 closeout, ledger/tool/test 변경 및 asset 삭제를 포함하는 Slice 3 commit 전체를 Git revert한다.

이 문서는 Slice 3 완료 시점의 historical snapshot이다. 당시 남아 있던 migration menu/tool과 synthetic
migration fixture는 후속 Slice 4A에서 퇴역했으며, current 상태는
[Migration Tool Retirement Closeout](./Enemy-Animation-Sparse-Binding-Migration-Tool-Retirement-Closeout.md)을
따른다.

## 결정과 삭제 범위

`EnemyView_Attacking`, `EnemyView_NonAttacking`, `EnemyView_Jumping`,
`EnemyView_PrototypeGravityFieldChaser`는 production catalog, Scene, Prefab, ScriptableObject에서 inbound
reference가 없는 초기 test/prototype residue로 확인했다. 현재 production은 각각 BlackEye, Startis,
Astreton, DrSaturn 전용 View를 사용하므로 네 prefab과 `.meta`를 삭제했다.

`EnemyView_Jumping`은 mesh/avatar가 없고 Controller motion과 timing reference clip이 비어 있어 정상
production visual contract가 아니었다. 이 prefab만 사용하던 inactive-compatible material 및 folder
`.meta`도 단일 참조 orphan으로 함께 삭제했다.

다음 companion asset은 이번 삭제 범위에서 유지했다.

- `EnemyAnimator_Attacking.controller`: production BlackEye가 사용하므로 삭제 금지
- `EnemyAnimator_Jump.controller`: production 참조는 없지만 현재 controller test fixture로 유지
- NonAttacking material: inactive material test fixture로 유지
- Attacking Windup/Recover clip과 material: 별도 orphan cleanup 후보

## 구현 결과

- production migration manifest 10행은 변경하지 않았다.
- Editor-only immutable disposition ledger가 historical baseline 14행과 current live/deleted 상태를 함께 고정한다.
- migration dry-run/postflight는 hardcoded Driver 14/Timing 4가 아니라 ledger의 live path를 사용한다.
- post-Slice 3 gate는 Driver 10, Binding 8, Timing 0 및 deleted asset/GUID residue 0을 요구한다.
- Attacking/NonAttacking 실파일 scaffold 기대와 Jumping invalid persistent-asset fixture를 제거했다.
- legacy migration compatibility는 기존 synthetic migration fixtures가 계속 검증한다.
- production prefab, runtime API, Driver legacy serialized field 및 public timing compatibility 타입은 수정하지 않았다.

## 검증 결과

| 명령 | 결과 |
|---|---|
| `./run_tests.sh full --filter EnemyAnimationBindingMigrationManifestTests` | EditMode 3/3, PlayMode matching 0 |
| `./run_tests.sh full --filter EnemyAnimationBindingMigrationDryRunTests` | EditMode 2/2, PlayMode matching 0 |
| `./run_tests.sh full --filter EnemyAnimation` | EditMode 111/111, PlayMode matching 0 |
| `./run_tests.sh full --filter EnemyPrefabScaffoldTests,EnemyViewPresentationAuthoringTests,EnemyViewAnimatorControllerContractTests,CampaignStageSequenceValidatorTests` | EditMode 31/31, PlayMode matching 0 |
| `./run_tests.sh full --filter EnemyViewAnimatorRuntimeCharacterizationPlayModeTests` | EditMode matching 0, PlayMode 18/18 |
| `./run_tests.sh core` | EditMode 254/254, PlayMode 111 total / 107 passed / 4 skipped / 0 failed |

Static residue 결과:

- Driver script GUID prefab reference: 10
- Binding script GUID prefab reference: 8
- Timing script GUID prefab reference: 0
- deleted prefab GUID의 `.prefab`/`.unity`/`.asset` reference: 0
- Controller/AOC/FBX/AnimationClip/Scene/ScriptableObject diff: 0

## 분리된 실패와 비주장

`EnemyInactiveMaterialAuthoringTests`는 EditMode 8개 중 기존 BlackEye package Lit fidelity 비교 2개가
실패했다. 실패 대상 BlackEye material/source/test에는 이번 Slice 3 diff가 없고, 삭제한 Jumping material과
무관하게 BlackEye direct comparison에서도 동일하게 재현됐다. 따라서 이 두 실패는 touched animation
retirement gate와 분리하며 해결됐다고 주장하지 않는다.

- broad unfiltered `full` lane은 실행하지 않았고 full/project-wide green을 주장하지 않는다.
- UI 변경이 없으므로 `ui` lane은 실행하지 않았다.
- retained controller/clip/material orphan 후보의 제거 완료를 주장하지 않는다.
- Slice 4 legacy private field 제거는 이 commit에 포함하지 않는다.

## Evidence

Evidence root:
`/mnt/d/J2M/evidence/enemy-animation-sparse-binding-slice3/20260904-legacy-view-retirement/`

최종 evidence는 inventory/reference scan, targeted XML/log, core XML/log, final diff와 non-claim note를
구분해 보존한다. 수정 과정의 compile/cache 실패 및 unrelated BlackEye material failure는 최종 green
artifact와 분리해 기록한다.
