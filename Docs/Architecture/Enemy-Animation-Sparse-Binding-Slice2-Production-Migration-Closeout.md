# Enemy Animation Sparse Binding — Slice 2 Production Migration Closeout

## 상태

- production migration apply: 완료 (2026-09-03)
- automated contract hardening: 완료 (2026-09-04)
- 최종 acceptance: **완료 — 사용자/asset owner가 interactive Inspector 및 migration 전/후
  visual parity 체크의 정상 동작을 확인함(2026-09-04)**
- Slice 1 checkpoint: `2fb4a2d4e92cdb1443836eeccb19ec429489d847`
- 승인된 pre-apply canonical dry-run SHA-256:
  `3be0bdf7fc739f31e67f848d3e6f653f54fbe22e670de53d90ca42bd0e0a066d`
- apply 직후 postflight SHA-256:
  `06493f6add7abb9a26d62e72c455e541b8b9232d4b4c10b980da1f7ae5ba4588`
- rollback: 이 closeout과 production prefab migration을 포함하는 Slice 2 commit 전체를 Git revert한다.

## 완료 inventory

| 항목 | 결과 |
|---|---:|
| `EnemyAnimatorDriver` prefab | 14 |
| production manifest row | 10 |
| production `MigratedBinding` | 8 |
| production `ApprovedNoBinding` | 2 |
| production root `EnemyAnimationBindingAuthoring` | 8 |
| production `EnemyAnimationTimingAuthoring` | 0 |
| global Timing Authoring prefab reference | 4 |

남은 Timing Authoring 4개는 Slice 2 범위 밖인 `EnemyView_Attacking`,
`EnemyView_NonAttacking`, `EnemyView_Jumping`,
`EnemyView_PrototypeGravityFieldChaser`에만 존재한다. Kali와 SecBot에는 binding을 추가하지 않았다.

## RocketFace 재검토 결론

RocketFace `ChargeWindup`의 timing reference와 Controller가 실제 평가하는 motion은 서로 다른 asset이다.

- timing reference: FBX sub-clip `Windup`,
  `1716406119d8be34d841f0d4eb033a2c:3060872287085348379`
- effective Controller motion: `Windup_Fix.anim`,
  `756a198ed5d59c84d8eb288b427f4916:7400000`
- 두 clip 길이: 모두 약 `0.3333333s`
- timing 결과: duration `0.4s`, speed 약 `0.8333333`
- dispatch 결과: layer 0 state `Windup`, crossfade `0.001s`, `Applied`
- 실제 평가 clip: `Windup_Fix.anim`

두 clip의 curve digest와 sampled pose는 동일하지 않았다. RF transform에서 최대 position 차이 5가 관찰됐고 rotation/scale 차이는 없었다. 이것은 migration이 만든 차이가 아니다. legacy 경로도 timing 계산에는 FBX sub-clip을 사용하고, Animator 출력에는 Controller의 `Windup_Fix.anim`을 사용했다.

따라서 validator는 timing reference가 effective motion과 다른 사실을 warning으로 보고한다. 동시에 manifest와 migration service는 State target별 effective motion GUID/local file ID를 별도 strong check로 고정한다. 이 분리는 timing reference를 실제 재생 clip으로 오인하지 않으면서 Controller 동작 drift도 놓치지 않는다.

## 구현 결과

- Editor-only immutable manifest가 production 10개 path/GUID, disposition, Animator/Controller identity,
  cue 순서, timing clip 및 State effective motion을 고정한다.
- dry-run과 apply는 같은 검사 service를 사용하며 apply는 승인 digest가 정확히 일치할 때만 열린다.
- production 8개 prefab은 root binding snapshot만 사용하며 Timing Authoring과 Driver reference를 제거했다.
- production contract는 Driver private legacy 문자열이 아니라 cue/mode/target/sustained/timing/clip matrix를 검증한다.
- synthetic legacy-only migration, partial-state hard failure, invalid clip/controller의 저장 전 실패 및 재실행 idempotency를 유지한다.
- reimport/reload 이후 Missing Script 0 및 8개 `AlreadyMigrated`/2개 `ApprovedNoBinding`을 확인한다.
- Controller, AOC, FBX, AnimationClip, Scene, ScriptableObject에는 migration diff가 없다.

## 2026-09-04 재검토 보강

High/Medium 재검토에서 자동화가 실제 내부 동작을 충분히 고정하지 못한 부분을 다음과 같이 보강했다.

- production PlayMode ledger를 campaign catalog의 정확한 10개 View와 결합했다. Startis의 Hit/Death
  Trigger, Kali/SecBot의 counter-only/no-visual, Nebulous의 Glide windup/active/recovery state와 timing을
  실제 production prefab 인스턴스로 검증한다.
- production Trigger 17개는 parameter 존재/소비 여부뿐 아니라 실행 가능한 transition의 layer,
  destination state, `If/0` condition, assigned Controller/AOC가 평가하는 destination Motion GUID/local file ID를
  고정한다. 따라서 같은 Trigger 이름이 다른 state나 다른 effective clip으로 연결되는 drift도 실패한다.
- synthetic migration은 State-only, Trigger-only, ApprovedNoBinding, invalid Controller, invalid clip,
  preflight 이후 source hash drift를 포함한다.
- apply는 각 prefab 저장 직전에 preflight SHA-256을 다시 비교한다. 실패 report는 저장 완료,
  실패 대상/단계, 미시도 대상을 구분하며 evidence 파일을 mutation 전에 생성한다.

재검토 결과 RocketFace의 FBX timing reference와 `Windup_Fix.anim` effective motion 차이는 그대로이며,
이는 migration 전후에 동일한 기존 분리 동작이다. 새로 확인한 Trigger transition도 모두 manifest의 의미와
일치했다. 다만 motion이 `<null>`인 기존 destination state(BlackEye Hit, RocketFace Hit, JPeter Hit/Death)는
그 사실 자체를 baseline으로 고정했을 뿐, 새 시각 animation이 존재한다고 해석하지 않는다.

자동화 최종 결과:

| 명령 | 결과 |
|---|---|
| `./run_tests.sh full --filter EnemyAnimation` | EditMode 112/112, PlayMode matching 0 |
| `./run_tests.sh full --filter EnemyViewAnimatorControllerContractTests` | EditMode 8/8, PlayMode matching 0 |
| `./run_tests.sh full --filter EnemyViewAnimatorRuntimeCharacterizationPlayModeTests` | EditMode matching 0, PlayMode 18/18 |
| `./run_tests.sh core` | EditMode 254/254, PlayMode 111 total / 107 passed / 4 skipped / 0 failed |

## Evidence

Evidence root:
`/mnt/d/J2M/evidence/enemy-animation-sparse-binding-slice2/`

- `20260903-approved-dry-run/02-dry-run/`: asset-owner가 승인한 pre-apply canonical report
- `20260903-production-migration/03-apply/`: 승인 digest 기반 apply와 즉시 reload/postflight
- `20260903-production-migration/04-targeted-tests/`: 최종 manifest, inventory, serialization,
  Controller, dispatch, scenario 및 인접 timing/coordinator/mapper 검증
- `20260903-production-migration/04-final-postflight/`: 최종 inventory guard 보강 후 dry-run/production contract 재검증
- `20260903-production-migration/05-production-playmode/`: production 10-view runtime characterization
- `20260903-production-migration/06-core/`: 같은 최종 code/asset 상태의 core lane
- `20260903-production-migration/07-manual-editor/`: reimport/Missing Script 및 Inspector semantic note
- `20260903-production-migration/08-final-diff/`: allowlist와 금지 asset zero-diff audit
- `20260904-hardening-followup/04-targeted-tests/`: Trigger destination/effective Motion,
  synthetic apply safety 및 EnemyAnimation 전체 묶음
- `20260904-hardening-followup/05-production-playmode/`: exact production 10-view runtime characterization
- `20260904-hardening-followup/06-core/`: 같은 최종 code/asset 상태의 core lane
- `20260904-hardening-followup/07-manual-editor/`: Slice 1 checkpoint detached worktree 준비와
  production PlayMode baseline 13/13 재현 결과, 자동 사전검사, 사용자/asset owner가 PASS로
  확인한 Inspector/visual A/B note evidence. 비교 기준 worktree는
  `/mnt/d/J2M/worktrees/enemy-animation-slice2-baseline-20260904`이며 각자의 private `Library`를 사용한다.

수정 과정의 실패 run은 삭제하지 않으며 최종 green run과 구분한다.

## 비주장

- broad unfiltered `full` lane green은 주장하지 않는다.
- UI 변경이 없으므로 `ui` lane은 실행 대상이 아니다.
- 비-production 4개 처리는 완료했다고 주장하지 않는다.
- global Timing Authoring reference 0 또는 `LegacyBlocked == 0`을 주장하지 않는다.
- 수동 결과는 사용자/asset owner의 대화 확인을 provenance로 기록한 note evidence다. Codex가
  Unity Editor 화면을 직접 관찰했거나 screenshot/video artifact가 제공되었다고 주장하지 않는다.
- 2026-09-03의 13-test 및 2026-09-04의 18-test PlayMode 결과는 automated runtime contract다.
  수동 확인 결과와 자동 runtime contract는 서로 대체하지 않고 별도 evidence로 유지한다.
