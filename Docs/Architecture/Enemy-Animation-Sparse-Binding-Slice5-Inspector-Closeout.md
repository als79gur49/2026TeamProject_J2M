# Enemy Animation Sparse Binding — Slice 5 Inspector Closeout

## 1. 상태

- 완료일: 2026-09-04
- 기준 parent revision: `5a703e6159554ca598927eb6e9762e0788d2ed8b`
- 상태: 구현, focused/core 검증, 대표 production Inspector evidence 완료
- evidence root: `/mnt/d/J2M/evidence/enemy-animation-sparse-binding-slice5/20260904-232923/`

이 문서는 sparse binding initiative의 Slice 5 Inspector evidence와 Architecture 마감을 기록한다. Slice 4B의
runtime dispatch policy, production prefab serialization, Binding snapshot 또는 public timing compatibility API를
변경하지 않는다.

## 2. 계약 분류

### StrongContract

- `EnemyAnimationBindingAuthoring.CreateSnapshot()`이 거부하는 unsupported authoring은 Inspector에서도 같은
  runtime validation 메시지를 표시한다.
- HelpBox는 stable diagnostic code를 메시지 앞에 붙이되 runtime exception message를 바꾸거나 재해석하지 않는다.
- production inventory는 Driver 10 / Binding 8 / Timing 0이다.
- Kali/SecBot은 ApprovedNoBinding이며 Timing/Binding을 새로 추가하지 않는다.
- sparse Binding, Controller/AOC, effective motion, clip identity와 presentation-only ownership을 유지한다.

### CurrentPolicy

- 대표 Inspector capture의 framing과 evidence 파일명
- Slice 5 closeout 문서 구성

## 3. 구현 결과

`EnemyAnimationBindingAuthoringEditor`의 HelpBox text assembly를 pure `FormatDiagnostic` seam으로 분리했다.
`EnemyAnimationControllerBindingValidator`는 기존처럼 `CreateSnapshot()` 예외 메시지를 `authoring.invalid`
diagnostic에 그대로 담으며 runtime validation 정책은 변경하지 않았다.

focused test는 다음 unsupported 조합에서 direct runtime exception message, structured diagnostic message 및 최종
HelpBox text가 동일한 payload를 갖는지 검증한다.

- Hit + State dispatch
- timing을 지원하지 않는 Hit의 duration override
- JumpAirborne + Trigger의 sustained state 누락
- primary State binding이 없는 구성의 non-sentinel crossfade

Prefab, Animator Controller/AOC, FBX, AnimationClip, Scene 및 ScriptableObject는 변경하지 않았다.

## 4. 수동 Inspector evidence

| View | 확인 결과 | Evidence |
|---|---|---|
| Kali | Driver에는 `animator`만 노출되고 Binding/Timing 없음 | `03-editor/kali-inspector.png` |
| Startis | Hit/Death Trigger 두 cue만 노출 | `03-editor/startis-binding-inspector.png` |
| Astreton | JumpWindup/JumpAirborne/JumpLanding State, ActionExecute/Hit/Death Trigger, crossfade `0.001` 노출 | `03-editor/astreton-binding-inspector.png` |
| DrSaturn | UtilityWindup/UtilityRecovery timing과 Hit/Death Trigger만 노출 | `03-editor/drsaturn-binding-inspector.png` |
| Nebulous | GlideWindup/GlideActive/GlideRecovery State, Windup/Recovery timing·reference clip, crossfade `0` 노출 | `03-editor/nebulous-binding-inspector.png` |

각 capture는 3440x1392 PNG다. root Inspector에 Missing Script가 보이지 않았고 production contract test의 exact
10-prefab hierarchy-wide Missing Script count 0 계약을 유지한다. capture 과정에서 prefab을 저장하지 않았으며
Git asset diff도 발생하지 않았다.

capture는 valid production authoring의 최종 Inspector 표면을 기록한다. invalid authoring HelpBox 자체의 GUI
rendering은 별도 캡처하지 않았으며, 해당 text 경로는 `DrawDiagnostics`의 `FormatDiagnostic` 호출 wiring과 focused
test의 runtime exception/diagnostic/HelpBox text 동등성으로 검증했다.

## 5. Validation

| Lane/filter | 결과 |
|---|---:|
| `EnemyAnimationBindingEditorValidationTests` | EditMode 16/16 |
| `EnemyAnimationBindingMigrationManifestTests` | EditMode 14/14 |
| `EnemyAnimationSparseBindingAssetCharacterizationTests` | EditMode 6/6 |
| `EnemyAnimationSparseBindingProductionContractTests` | EditMode 2/2 |
| `EnemyViewAnimatorControllerContractTests` | EditMode 8/8 |
| `./run_tests.sh core` | EditMode 254/254, PlayMode 111 total / 107 passed / 4 skipped / 0 failed |

각 filtered PlayMode는 matching test 0이므로 성공 evidence로 세지 않는다. 실행한 lane과 수동 Editor capture가
끝난 뒤 최종 repository audit에서 font 및 production asset Git diff는 0이다.

최종 검증 후보는 parent revision과 변경 파일 SHA-256 및 tracked patch hash를
`06-attribution/candidate-tree-manifest.txt`에 기록한다. 후속 commit은 이 manifest의 파일 hash와 동일한 tree를
포함해야 동일 검증 후보로 귀속할 수 있다.

## 6. B0 governance disposition

Slice 4B B0 clean-preflight provenance 공백은 governance owner가 2026-09-04 절차 예외로 명시적으로 수용했다.
이는 clean-preflight 수행을 소급 주장하거나 누락 증거를 생성한 것이 아니며 runtime, asset identity, rollback,
residue 및 validation StrongContract를 면제하지 않는다.

수용 기록: `/mnt/d/J2M/evidence/enemy-animation-sparse-binding-slice4b/20260904-reaudit-corrective/04-governance/owner-acceptance.txt`

## 7. Non-claims와 후속 경계

- broad unfiltered `full`은 실행하지 않았고 project-wide/full green을 주장하지 않는다.
- runtime UI/asset 변경이 없어 `ui` lane은 실행하지 않았다.
- BlackEye inactive material, manifest/ledger naming, broad full baseline recovery를 해결하지 않았다.
- `EnemyAnimationTimingAuthoring` public compatibility source/type/API를 제거하거나 새 production authoring으로
  활성화하지 않았다.
- invalid authoring HelpBox의 실제 IMGUI rendering capture는 생성하지 않았다. 이번 Slice는 formatter output을
  자동 검증하고 Inspector 호출 wiring은 source review와 compile로 확인했으며 pixel-level GUI rendering을 새
  계약으로 확장하지 않는다.
- Inspector evidence 마감은 Animator Controller/clip content polish 또는 새로운 enemy animation 기능 승인이 아니다.

이 범위로 Slice 0~5 sparse binding initiative의 승인된 구현과 Inspector evidence는 마감한다. 위 비범위 작업은
각각 별도 intent와 승인을 요구한다.
