# Enemy Animation Sparse Binding — Slice 4B Legacy Inspector Retirement Plan

## 1. 문서 상태

- 작성일: 2026-09-04
- 기준 revision: `2617268f586551a06f74f27c897ee8cb93984de6`
- 대상: Slice 4B — `EnemyAnimatorDriver` legacy private serialized Inspector surface retirement
- 상태: 구현 및 재감사 보정 완료; B0 clean-preflight provenance 공백의 governance owner 절차 예외 수용 완료
- 선행 완료:
  - production View migration 완료
  - legacy View 4개 삭제 완료
  - Slice 4A one-time migration tool 퇴역 완료
  - current inventory: Driver 10 / Binding 8 / Timing 0
  - disposition: MigratedBinding 8 / ApprovedNoBinding 2 / Deleted 4 / LegacyBlocked 0

이 계획은 private serialized field를 지우는 단순 source cleanup이 아니다. production prefab 10개에 남은
legacy YAML을 제거하면서도 sparse binding, ApprovedNoBinding, legacy-only runtime compatibility를 동시에
보존하는 atomic decommission intent를 정의한다.

## 2. 제안 결론

이 계획이 별도로 승인되면 `EnemyAnimatorDriver`의 `animator`를 제외한 legacy state/trigger/timing serialized
field 16개를 제거한다.
unbound Driver의 legacy cue fallback은 `EnemyAnimationTimingAuthoring` component가 첫 resolution 전에 존재할
때만 활성화한다. Timing component가 없는 unbound Driver는 cue signal counter와 optional Animator parameter
동기화는 유지하지만 state/trigger cue command는 `Unsupported`로 처리한다.

이 정책으로 다음 세 경로를 분리한다.

| Binding | Timing Authoring | cue dispatch 정책 |
|---|---|---|
| 있음 | 없음/있음 | 새 Binding만 사용; missing cue의 legacy fallback 금지 |
| 없음 | 있음 | public compatibility component를 명시적 opt-in으로 사용하는 legacy 경로 |
| 없음 | 없음 | cue command 없음; Kali/SecBot ApprovedNoBinding 및 primitive fallback 경로 |

`IsMoving`, `EnemyAiMode`, `EnemyActionKind`, jump/charge phase와 같은 optional parameter 동기화는 cue dispatch
gate와 별개다. Kali/SecBot의 이동 parameter 동작을 끄지 않는다.

## 3. 현재 사실과 production 위험

### 3.1 제거 대상 field

현재 Driver에는 다음 legacy serialized field가 있다.

- timing reference 1개:
  - `animationTimingAuthoring`
- state name 8개:
  - `windupStateName`
  - `jumpWindupStateName`
  - `jumpAirborneStateName`
  - `chargeActiveStateName`
  - `recoveryStateName`
  - `glideWindupStateName`
  - `glideActiveStateName`
  - `glideRecoveryStateName`
- trigger name 7개:
  - `windupTriggerName`
  - `jumpWindupTriggerName`
  - `jumpAirborneTriggerName`
  - `attackTriggerName`
  - `recoveryTriggerName`
  - `hitTriggerName`
  - `deathTriggerName`

`animator`는 Driver가 직접 소유하는 current authoring field이므로 유지한다.

### 3.2 serialized residue

production prefab 10개 모두 위 16개 key를 직렬화한다. baseline은 prefab당 16행, 총 160행이다. C# field만
삭제하면 stale YAML이 남을 수 있으므로 exact allowlist를 Unity에서 reserialize해야 한다.

### 3.3 Kali/SecBot blocker

Kali와 SecBot은 `ApprovedNoBinding`이며 Timing component도 없다. 현재 legacy cue 이름이 빈 문자열이라
Hit/Death counter는 증가하지만 Animator cue command는 발생하지 않는다. 그러나 두 Controller에는 실제
`Hit`와 `Death` trigger가 있다. field 제거 후 기본 이름을 모든 unbound Driver에 무조건 적용하면 production
동작이 바뀐다.

따라서 "field를 const로 바꾸기"만으로는 충분하지 않다. Timing component 존재 여부로 legacy mode를
명시적으로 제한하는 runtime gate가 먼저 필요하다.

### 3.4 synthetic production-executor smoke

`EnemyPresentationReadinessPlayModeTests`의 production-executor smoke는 Binding과 Timing 없이 BlackEye
Controller를 붙인 synthetic Driver로 7개 playback request의 accepted/mapped count와 signal을 검사한다.
coordinator는 Driver 내부 dispatch 성공 여부를 반환받지 않으므로 이 count는 low-level Animator command 7개의
성공 증거가 아니다. 이 테스트가 고정하는 StrongContract는 production executor ownership/diagnostics이며,
unbound/no-Timing fallback 자체가 아니다. fixture에 `EnemyAnimationTimingAuthoring`을 명시적으로 추가해
executor 계약은 유지하고 legacy opt-in을 드러낸다. 실제 state/trigger 결과는 cue별 Driver runtime test가
별도로 고정한다.

### 3.5 dynamic primitive fallback

`DefaultGameplayEntityViewFactory`는 catalog/prefab이 없는 enemy unit에 Driver를 동적으로 추가하지만 Animator,
Binding, Timing을 추가하지 않는다. 이 경로는 cue command가 없는 안전한 primitive presentation으로 유지한다.

## 4. 계약 분류

### 4.1 StrongContract — 유지

- production semantic manifest 10행과 disposition ledger 14행
- resolved inventory Driver 10 / Binding 8 / Timing 0
- MigratedBinding 8개의 exact binding snapshot
- Kali/SecBot ApprovedNoBinding 및 damage/death cue no-command
- valid new Binding의 missing-cue legacy fallback 금지
- Controller/AOC, Animator path, effective motion, clip GUID/local file ID
- runtime signal counter, optional parameter, timing, resync, topology, Death/DeathMotion 동작
- `EnemyAnimationTimingAuthoring` public type/API/source compatibility
- Timing component로 명시적으로 opt-in한 legacy-only runtime compatibility
- primitive fallback Driver의 Animator-unavailable/no-command 안전성
- presentation-only ownership; authoritative simulation 무변경

### 4.2 CurrentPolicy — 제거

- Driver의 legacy private serialized timing reference
- Driver의 per-cue state/trigger serialized name 15개
- 위 16개 field의 Inspector 노출
- production prefab의 해당 Driver YAML key 160행
- private field 이름을 직접 주입하거나 current Inspector 목록으로 고정한 test fixture

### 4.3 비범위

- `EnemyAnimationTimingAuthoring` public type/API 또는 `.meta` 제거
- production Binding 8개 내용 변경
- Kali/SecBot에 Binding 또는 새 marker component 추가
- checked-in/permanent migration menu, semantic apply service/report 또는 reusable serialized mutation helper 재도입
- manifest/ledger migration 계열 이름 변경
- Animator Controller/AOC/FBX/AnimationClip 변경
- Scene/ScriptableObject/catalog/profile 변경
- Enemy AI, Tick pipeline, View lifetime, DeathMotion 변경
- BlackEye inactive material 문제 해결
- Slice 5 Inspector screenshot/architecture 최종 마감

## 5. Runtime 설계

### 5.1 binding 우선순위

`TryResolveAnimationBinding`의 snapshot cache와 exclusive binding 정책을 유지한다. Binding이 있으면 Timing
component가 함께 존재해도 legacy timing/dispatch를 읽지 않는다.

### 5.2 legacy opt-in

첫 timing resolution에서 root의 `EnemyAnimationTimingAuthoring`을 `GetComponent`로 찾는다. component가 있으면
snapshot을 cache하고 legacy compatibility를 활성화한다. component가 없으면 no-legacy 상태를 cache한다.
component를 첫 resolution 뒤 동적으로 추가하는 동작은 현재도 cache 때문에 지원되지 않으므로 새 계약으로
확장하지 않는다.

legacy-opt-in 판정은 `TryResolveLegacyDispatchBinding` 한 곳에만 두지 않는다. 일반 cue dispatch,
`TryResolveRestorableState`, JumpAirborne 특별 분기, debug/restoration fallback, topology preserve/restore와
resync가 모두 같은 cached 판정을 사용해야 한다. no-binding/no-Timing 경로가 state 이름을 직접 해석하는 우회
경로를 남기지 않는다.

`animationTimingAuthoring` serialized field와 `Reset()`의 해당 할당은 제거한다. 필요하면 runtime cache용
nonserialized private reference를 별도 이름으로 둘 수 있지만 Inspector/serialized surface에는 노출하지 않는다.

### 5.3 legacy target vocabulary

legacy compatibility에서 필요한 기본 state/trigger 이름은 private `const string`으로 유지할 수 있다. 이 상수는
Timing opt-in이 확인된 경로에서만 사용한다. production Binding이나 ApprovedNoBinding의 implicit fallback
source가 되어서는 안 된다.

### 5.4 no-binding/no-timing 동작

- cue signal counter와 `LastPresentationState`는 현행대로 갱신한다.
- optional Animator parameter 동기화는 현행대로 수행한다.
- timing은 duration 0, speed 1 기본값을 유지한다.
- state/trigger cue dispatch 및 restorable-state command는 `Unsupported`/false다.
- pending state command를 만들지 않는다.
- JumpAirborne ensure/preserve/restore/resync 및 topology snapshot을 만들지 않는다.
- `LastCrossFadedStateName`은 empty이며 Animator state/transition을 바꾸지 않는다.
- Animator 또는 Controller가 없어도 예외를 내지 않는다.

### 5.5 public compatibility

`EnemyAnimationTimingAuthoring`, `EnemyAnimationTimingSnapshot`, obsolete public timing/death members의 source
shape는 이번 Slice에서 제거하지 않는다. fixture는 reflection으로 Driver field를 연결하지 않고 component를
Driver보다 먼저 root에 추가한다.

## 6. Read-only audit 설계

### 6.1 Driver block-aware YAML audit

raw token을 Assets 전체에서 단순 검색하면 Player driver의 `animationTimingAuthoring`, `hitTriggerName` 등과
충돌한다. permanent audit은 `.prefab`, `.unity`, `.asset`의 YAML MonoBehaviour block을 읽고 다음 순서로
검사한다.

1. `--- !u!114` block 경계를 식별한다.
2. `m_Script` GUID가 `EnemyAnimationBindingMigrationManifest.DriverScriptGuid`인 block만 선택한다.
3. 해당 block에 16개 retired property key가 하나라도 있으면 path/key 진단으로 실패한다.
4. 다른 script block의 동일 property 이름은 무시한다.
5. UTF-8 BOM을 허용한 text decode 후 `%YAML` header와 `--- !u!` document header가 모두 있는 입력만 Unity
   text YAML로 판정한다. invalid byte sequence, binary, empty, header 누락/malformed 입력은 명시적인
   `UnsupportedTextAsset` 결과와 path를 남기며 정상 무잔존 결과로 세지 않는다.
6. binary/non-text asset의 component 존재는 resolved inventory로 보완한다. production allowlist asset이
   `UnsupportedTextAsset`이면 residue 0을 증명할 수 없으므로 Hold한다.

helper는 read-only pure input 또는 row/provider 주입을 허용해 synthetic invalid YAML을 검사한다. Prefab을
저장하거나 reserialize하는 API를 audit helper에 추가하지 않는다.

### 6.2 negative coverage

- Driver GUID block에 retired key 16개 중 하나가 있으면 각각 실패
- 인접한 Player/다른 MonoBehaviour block의 동일 key는 통과
- LF/CRLF와 마지막 block EOF 처리
- invalid UTF-8/binary, empty, `%YAML`/document header 누락과 malformed block은 명시적으로 실패 또는
  `UnsupportedTextAsset`으로 분류되며 silent pass하지 않음
- allowlist 밖 Prefab Variant가 Driver를 상속하는 기존 negative test 유지
- deleted GUID 5개 inbound reference negative test 유지

## 7. Tests-first 전환

### 7.1 runtime policy tests

- actual Kali prefab: `IsMoving` parameter 갱신과 Hit/Death counter 증가, transition/state/trigger command 없음
- actual SecBot prefab: 동일
- synthetic no-binding/no-Timing Driver: `None`을 제외한 모든 cue를 parameterized 입력으로 검사하고
  `Unsupported`, state/trigger/pending command 없음
- synthetic no-binding/no-Timing Driver의 JumpAirborne: `TryRestoreCueState`, base-animation ensure,
  preserve/restore topology, resync가 false이고 snapshot/transition/`LastCrossFadedStateName` 없음
- no-binding/no-Timing을 한 번 resolution한 뒤 Timing component를 동적으로 추가해도 legacy mode가 켜지지
  않는 negative-cache 계약
- synthetic legacy Driver + Timing + default trigger/state Controller: legacy cue 적용
- synthetic legacy Driver + Timing: windup/recovery/jump/glide timing 및 resync 유지
- new Binding + contradictory Timing: Binding만 사용
- new Binding missing cue: `Unsupported`, legacy fallback 0
- primitive fallback Driver: cue 입력이 예외 없이 no-command
- optional parameter sync: no-binding/no-Timing에서도 `IsMoving` 등 지원 parameter 유지
- production-executor smoke: fixture에 Timing을 명시하고 7개 playback request accepted/mapped,
  ownership/diagnostic count와 signal 유지; low-level command 성공으로 표현하지 않음

### 7.2 structural tests

- `EnemyAnimatorDriver` serialized field exact set은 `animator` 하나
- 16개 retired field reflection lookup은 null
- C# source 및 Driver YAML block residue 0
- Timing component serialized asset reference 0
- resolved inventory 10/8/0
- manifest/ledger 10/14 및 disposition 8/2/4/0

### 7.3 test 전환 원칙

- `attackTriggerName`, `deathTriggerName`, `animationTimingAuthoring`을 Driver에 reflection 주입하는 테스트 제거
- private field를 못 찾게 바꾸는 데 그치지 않고 동일한 runtime 결과를 component/binding fixture로 표현
- production-executor smoke는 Timing opt-in을 명시
- blank `deathTriggerName` fixture는 no-binding/no-Timing no-command 계약으로 교체
- test-only migration serializer 또는 mutation service를 만들지 않음
- C# lexical/syntax-aware caller audit은 multiline invocation과 `SetField`, `SetPrivateField`,
  `SetSerializedField` alias를 포함해 16개 identifier 전부를 분류한다. intentional negative-coverage string은
  exact test file/fixture allowlist로 구분하고 match count와 허용 위치를 evidence에 남긴다.

Gate: 새 policy tests는 current implementation에서 예상된 tests-first failure를 내야 하고, unrelated compile
failure나 owning-stage matching count 0은 성공 evidence로 인정하지 않는다.

## 8. Asset cleanup 방식

### 8.1 exact allowlist

다음 production prefab full path/GUID pair 10개만 reserialize 대상으로 허용한다.

| Prefab path | GUID |
|---|---|
| `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_BlackEye.prefab` | `d25f546e192650048aeec864272891c2` |
| `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_Startis.prefab` | `d2fcbcdf8d3b4dd4b88dd02f4fb843ae` |
| `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_RocketFace.prefab` | `4b046e9ae49c42b4388ff25a2353c3f7` |
| `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_Astreton.prefab` | `7aae229c82bf72e49c105c163ee2d676` |
| `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_DrSaturn.prefab` | `8fa155d3aa7c4ea4bb582c5fb364e801` |
| `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_JPeter.prefab` | `7fa07cb2cf222ca4194569602c56204a` |
| `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_Sunwheel.prefab` | `40031cd173e7ca9902ba3c3d971f4239` |
| `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_Kali.prefab` | `6eced58e0a1a4a6881cc0c15aa61f402` |
| `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_SecBot.prefab` | `fca7744895a64016aab93a7e38807d41` |
| `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs/EnemyView_Nebulous.prefab` | `18e3b2aebc74a9b43b5f5cda65339d6b` |

실행 직전 manifest Rows가 정확히 10개인지, path가 production root 아래인지, path/GUID가 위 표와 양방향
일치하는지, GUID 중복과 path 중복이 없는지, 각 `GUIDToAssetPath` 결과가 선언 path와 같은지 검증한다. 하나라도
다르면 Hold하며 basename이나 wildcard로 대상을 복구 추정하지 않는다.

### 8.2 실행 방식

Driver compile 성공 후 Unity 공식 `AssetDatabase.ForceReserializeAssets(paths,
ForceReserializeAssetsOptions.ReserializeAssets)`를 direct user action으로 정확히 한 번 호출한다. parameterless
호출, `ReserializeMetadata`, `ReserializeAssetsAndMetadata`, wildcard와 폴더 입력은 금지한다.

실행 seam은 같은 Editor assembly에 두는 transient, uncommitted `MenuItem` source로 한정한다. 이 source는
manifest Rows에서 path를 얻고 8.1의 count/root/path/GUID/duplicate 검증을 모두 통과한 배열만 API에 전달한다.
자동 import callback이나 test runner에서 실행하지 않고 사용자가 메뉴를 직접 선택한다. 실행 전 source와
SHA-256을 evidence에 저장하고, 실행 직후 transient C#과 Unity가 만든 `.meta`를 삭제한 뒤 Editor assembly를
다시 compile한다. transient menu/type/path 및 reserialize API caller residue 0을 commit gate로 둔다. checked-in
apply service나 test-side prefab mutation path는 만들지 않으며 raw YAML line 삭제도 사용하지 않는다.

이 command는 Binding/Timing 값을 변환하는 migration implementation이 아니라 검증된 exact path 배열을 Unity의
assets-only reserialize API에 전달하는 일회성 operator seam이다. semantic mutation, field mapping, prefab별
save loop나 reusable helper를 포함하지 않으며 source는 같은 phase 안에서 제거한다. 따라서 Slice 4A에서
퇴역시킨 checked-in/reusable semantic migration surface를 복구하지 않는다.

operation 직후 mechanical diff는 변경 asset set이 allowlist prefab 10개와 exact match하고, 각 Driver block에서
anchored retired key 16행씩 총 160행 삭제, 추가행 0, `.meta`와 그 밖의 asset diff 0이어야 한다. 제거된 모든
line이 올바른 Driver script GUID block 안의 retired key인지 검사한다. component/order/local-file-ID 등 다른
content가 바뀌거나 retired key가 남으면 즉시 Hold하고 더 저장하지 않는다.

### 8.3 preserved identity

- prefab GUID와 `.meta`
- root/Driver/Animator local file ID
- root component 수와 순서
- Animator explicit/fallback path
- Binding component와 serialized snapshot
- Controller/AOC GUID 및 local file ID
- effective motion과 clip GUID/local file ID
- material, renderer, VFX/audio authoring

## 9. 실행 순서

### Phase B0 — Preflight

- HEAD/branch, clean status, diff-stat 원문 저장
- production prefab 10개 SHA-256 저장
- Driver block별 16-key matrix와 총 160행 baseline 저장
- manifest 10개 full path/GUID 및 `GUIDToAssetPath` 양방향 exact match 저장
- resolved inventory 10/8/0 저장
- Kali/SecBot Controller의 Hit/Death parameter 존재와 no-command PlayMode baseline 저장
- production-executor/legacy fixture/direct field caller 목록 저장
- deleted GUID 5개 inbound reference 0 저장

Gate: 사용자 변경이 없고, 현재 10/8/0과 160행 baseline이 재현되며, exact caller 목록이 있어야 한다.

### Phase B1 — Tests first

- legacy opt-in/no-opt-in runtime matrix 추가
- 모든 non-`None` cue와 JumpAirborne restore/preserve/resync 우회 경로 negative coverage 추가
- first-resolution no-Timing negative cache와 actual Kali/SecBot `IsMoving` coverage 추가
- production-executor fixture를 Timing opt-in 구조로 전환
- Inspector exact surface test를 `animator` only 기대값으로 전환
- block-aware YAML audit와 synthetic negative cases 추가
- current implementation에서 의도한 failure를 evidence로 저장

Gate: policy/residue assertion이 owning stage에서 실제로 실행되고 예상 원인으로 실패해야 한다.

### Phase B2 — Runtime decommission

- Driver legacy serialized field 16개 제거
- Timing component lookup/cache와 legacy opt-in gate 구현
- legacy target constant를 opt-in 경로로 한정
- reflection fixture를 component/binding fixture로 전환

Gate: asset 저장 전 compile, pure/runtime policy test가 통과하고 Kali/SecBot contract가 유지되어야 한다.

### Phase B3 — Production prefab reserialize

- reviewed transient direct-user-action Editor command의 source/hash를 evidence에 저장
- manifest path/GUID 10쌍을 검증한 뒤 assets-only API로 exact 10 prefab을 reserialize
- transient C#과 `.meta`를 삭제하고 compile 및 symbol/caller residue 0 확인
- diff가 prefab 10개, 16행씩 총 160행 삭제, 추가행/다른 asset/`.meta` 0인지 즉시 검사
- reimport 후 Missing Script 0과 component identity 검사
- Driver block residue 0 검사

Gate: production 10개 외 asset diff 0, `.meta` diff 0, protected identity 변화 0이어야 한다.

### Phase B4 — Targeted 및 core validation

- manifest/inventory/production contract
- binding editor validation/dispatch/runtime scenarios
- timing ownership 및 production executor
- Controller/effective motion 및 production PlayMode characterization
- core lane

Gate: touched cluster failure 0. unrelated known baseline은 분리하고 full/project-wide green을 주장하지 않는다.

### Phase B5 — Editor evidence와 closeout handoff

- exact reserialize operation과 reimport 결과 기록
- Kali/SecBot Inspector에 Binding/Timing/legacy field가 없는지 확인
- migrated View에서 Driver에는 `animator`만 보이는지 표본 확인
- Slice 5 screenshot 대상과 미실행 항목을 명시
- Slice 4B closeout/evidence index 작성

Gate: asset 변경 목적과 수동/editor 확인 결과가 기록되어야 한다. 최종 screenshot polish는 Slice 5로 넘길 수
있지만 수동 reimport/Inspector 확인 자체는 생략하지 않는다.

### Phase B6 — 독립 재감사

- no-binding/no-Timing과 legacy Timing opt-in 정책 drift
- Driver source/YAML residue
- resolved inventory/variant inheritance
- deleted GUID inbound reference
- production snapshot/Controller/clip identity
- protected asset unexpected diff
- BlackEye baseline 및 다른 High/Medium finding의 base revision 존재 여부와 Slice 4B 귀속

Gate: Slice 4B에 귀속되거나 runtime/asset/rollback 계약을 훼손하는 unresolved High/Medium finding이 하나라도
있으면 Hold한다. 기준 revision에 이미 존재했고 Slice 4B diff와 무관함을 증명한 finding만 별도 후속 intent로
분리할 수 있다.

## 10. Validation

구현 시 fixture 이름을 다시 확인하고 다음 순서로 실행한다.

```bash
./run_tests.sh full --filter EnemyAnimationBindingMigrationManifestTests
./run_tests.sh full --filter EnemyAnimationSparseBindingProductionContractTests
./run_tests.sh full --filter EnemyAnimationSparseBindingAssetCharacterizationTests
./run_tests.sh full --filter EnemyAnimationBindingEditorValidationTests
./run_tests.sh full --filter EnemyAnimationBindingDispatchTests
./run_tests.sh full --filter EnemyAnimationSparseBindingRuntimeScenarioTests
./run_tests.sh full --filter GameplayTimingOwnershipTests
./run_tests.sh full --filter RuntimeBoardBoundsGuardTests
./run_tests.sh full --filter EnemyPresentationReadinessPlayModeTests
./run_tests.sh full --filter EnemyViewAnimatorControllerContractTests
./run_tests.sh full --filter EnemyViewAnimatorRuntimeCharacterizationPlayModeTests
./run_tests.sh core
```

추가 static gates:

```bash
rg -n "animationTimingAuthoring|windupStateName|jumpWindupStateName|jumpAirborneStateName|chargeActiveStateName|recoveryStateName|glideWindupStateName|glideActiveStateName|glideRecoveryStateName|windupTriggerName|jumpWindupTriggerName|jumpAirborneTriggerName|attackTriggerName|recoveryTriggerName|hitTriggerName|deathTriggerName" Assets/_Features/Gameplay/Gameplay_EnemyPresentation/Runtime/EnemyAnimatorDriver.cs
rg -n "animationTimingAuthoring|windupStateName|jumpWindupStateName|jumpAirborneStateName|chargeActiveStateName|recoveryStateName|glideWindupStateName|glideActiveStateName|glideRecoveryStateName|windupTriggerName|jumpWindupTriggerName|jumpAirborneTriggerName|attackTriggerName|recoveryTriggerName|hitTriggerName|deathTriggerName" Assets/_Features/Gameplay/Gameplay_Tests
rg -n "ForceReserializeAssets|Slice4B.*Reserialize|LegacyInspector.*Reserialize" Assets
git diff --check
```

첫 번째와 세 번째 `rg`는 결과 0이어야 한다. 두 번째는 lexical/syntax-aware caller audit의 후보 입력이며
intentional negative coverage 외 match 0이어야 한다. YAML residue의 authoritative gate는 동일 이름을 쓰는
다른 component의 false positive를 피하기 위해 block-aware permanent audit 결과를 사용한다.

UI asset/runtime UI를 변경하지 않으므로 `ui` lane은 요구하지 않는다. broad unfiltered `full`을 실행하지
않으면 그대로 기록하고 broad green을 주장하지 않는다.

## 11. Evidence

신규 evidence root 후보:

`/mnt/d/J2M/evidence/enemy-animation-sparse-binding-slice4b/<run-id>/`

- `00-preflight/`: revision/status/diff, prefab hashes, 160-key matrix, caller graph
- `01-tests-first/`: policy 및 block-aware residue expected failure
- `02-runtime/`: compile과 focused runtime policy 결과
- `03-prefab-reserialize/`: exact operation, before/after hashes와 semantic diff
- `04-targeted/`: manifest/inventory/dispatch/timing/Controller XML과 logs
- `05-production-playmode/`: production executor와 View characterization
- `06-core/`: core XML과 logs
- `07-editor/`: reimport/Missing Script/Inspector manual evidence
- `08-final-audit/`: residue, protected diff, remaining findings, non-claims

success artifact, tests-first expected failure, runner/cache 문제, unrelated BlackEye baseline을 분리한다. 최종
tested commit/tree는 commit 후 재검증하거나 code/asset tree 귀속을 별도로 명시해 자기참조를 피한다.
Slice 4B의 새 preflight/evidence는 Slice 4A에서 실행하지 못한 R0 evidence를 소급 생성하거나 치유하지 않는다.
그 provenance gap의 strict governance owner waiver/후속 판정은 별도 기록으로 남긴다.

## 12. Hold 조건

- current baseline이 Driver 10 / Binding 8 / Timing 0이 아님
- production Driver YAML baseline이 10 prefab x 16 key가 아님
- manifest full path/GUID가 exact table과 다르거나 중복/root escape/`GUIDToAssetPath` mismatch가 있음
- Kali/SecBot no-command가 현재 재현되지 않음
- no-binding/no-Timing 정책이 production-executor ownership 계약을 보존할 수 없음
- legacy Timing opt-in fixture가 private serialized field 없이 표현되지 않음
- optional parameter sync가 cue fallback gate와 함께 꺼짐
- Binding missing cue가 legacy 경로로 내려감
- production prefab diff가 retired 16-key 제거를 초과함
- 변경 prefab set이 exact 10개가 아니거나 16행씩 총 160행 삭제/추가 0 조건을 만족하지 않음
- transient reserialize source/`.meta`/menu/API caller residue가 남음
- checked-in/reusable semantic migration surface가 다시 생김
- prefab/meta/local file ID/component order/Binding snapshot 변경
- Controller/AOC/FBX/AnimationClip/Scene/ScriptableObject diff
- block-aware audit가 다른 component의 동일 key를 false positive로 판정함
- targeted/core/production runtime 회귀
- owning-stage matching test count 0
- 사용자 변경과 Slice 4B diff를 안전하게 분리할 수 없음

Hold가 발생하면 field를 일부 되살리거나 migration tool을 재도입하지 않는다. no-binding opt-out 또는 legacy
opt-in seam을 먼저 재설계하고 별도 승인을 받는다.

## 13. Rollback과 commit 경계

readiness plan은 구현보다 먼저 별도 docs commit으로 둘 수 있다. 실제 Slice 4B 구현은 다음을 하나의 atomic
decommission commit으로 묶는다.

- Driver field/runtime gate 변경
- private-field fixture 전환과 permanent audit
- production prefab 10개 reserialize
- Slice 4B implementation status

부분 rollback은 허용하지 않는다. Slice 4B rollback은 implementation commit 전체의 Git revert이며 Driver
field, tests, prefab YAML 160행을 함께 복원해야 한다. raw YAML 수동 복원, prefab 일부만 revert, field만 다시
추가하는 방식은 rollback으로 인정하지 않는다.

권장 구현 commit 제목:

```text
refactor: Gameplay/EnemyAnimation - legacy Driver authoring 표면 퇴역
```

본문에는 private field 제거 이유, Timing opt-in policy, Kali/SecBot no-command 보존, prefab 10개 reserialize,
protected identity와 validation evidence를 기록한다.

## 14. 완료 Gate

- Driver serialized field는 `animator` 하나
- checked-in/reusable semantic migration menu/service/helper residue 0
- legacy private field C# symbol residue 0
- Driver YAML block의 retired 16-key residue 0
- production prefab 10개 reserialize diff가 retired key 제거에 한정
- Driver 10 / Binding 8 / Timing 0
- disposition 8/2/4/0 및 LegacyBlocked 0
- Kali/SecBot counter 유지 및 cue command 0
- legacy Timing opt-in runtime fixture 통과
- production-executor playback request accepted/mapped 및 ownership/diagnostic contract 통과
- valid Binding missing-cue legacy fallback 0
- optional Animator parameter 동작 유지
- production binding snapshot/Controller/AOC/effective motion/clip identity 유지
- reimport 후 Missing Script 0
- deleted GUID inbound reference 0
- Controller/AOC/FBX/AnimationClip/Scene/ScriptableObject unexpected diff 0
- targeted production PlayMode와 core failure 0
- manual Editor/reimport evidence 기록
- remaining findings와 non-claims 기록
- Slice 4B 귀속 또는 runtime/asset/rollback 계약 관련 unresolved High/Medium finding 0

이 Gate가 닫히기 전에는 Slice 4B 또는 전체 sparse binding initiative가 완료됐다고 주장하지 않는다. Slice 5,
BlackEye material, manifest rename, broad full recovery를 자동으로 시작하거나 해결됐다고 주장하지 않는다.

## 15. 구현 Closeout

### 15.1 구현 결과

2026-09-04 구현 tree에서 Driver의 legacy private serialized field 16개를 제거하고 serialized field를
`animator` 하나로 제한했다. Binding이 없는 Driver는 첫 timing resolution에서 root
`EnemyAnimationTimingAuthoring` 존재 여부를 snapshot으로 고정한다. Timing이 없으면 cue counter와 optional
Animator parameter는 유지하지만 dispatch, pending state, JumpAirborne restore/topology/resync command는 만들지
않는다. Binding이 있으면 기존 exclusive sparse binding 정책을 유지한다.

production prefab은 allowlist 10개만 변경됐으며 각 Driver block에서 retired key 16행씩 총 160행이 삭제됐다.
추가행, `.meta`, Controller/AOC/FBX/AnimationClip/Scene/ScriptableObject 변경은 없다. permanent block-aware
audit은 UTF-8 BOM, LF/CRLF, EOF block, 다른 MonoBehaviour의 동명 property, invalid UTF-8/binary/malformed YAML을
구분하며 production Driver block residue 0을 고정한다.

### 15.2 operator seam 편차와 처리

최초 승인된 `AssetDatabase.ForceReserializeAssets(paths, ReserializeAssets)` direct-user 메뉴는 exact 10개
path/GUID 검증과 호출에는 성공했지만 파일을 다시 쓰지 않아 prefab diff 0, retired key 160개 잔존으로
Hold했다. 해당 transient source와 `.meta`를 제거한 뒤 사용자의 별도 승인을 받아 exact prefab save seam으로
재설계했다.

재설계 seam은 같은 10개 path/GUID와 Driver serialized field set을 검증하고 각 prefab을
`PrefabUtility.LoadPrefabContents`/`SaveAsPrefabAsset`으로 저장했다. 각 저장 직후 원문 Driver block에서 retired
16행만 제거한 예상 text와 실제 file을 exact 비교해 다른 변경이 있으면 다음 prefab 전에 중단하도록 했다.
10개 모두 내부 검증을 통과했으며 seam source/hash와 최초 no-op 기록을 evidence에 보존한 뒤 source와 `.meta`,
menu/API caller를 제거했다. checked-in mutation surface는 남기지 않았다.

### 15.3 validation 결과

| Lane/filter | 결과 |
|---|---:|
| `EnemyAnimationBindingMigrationManifestTests` | EditMode 14/14 |
| `EnemyAnimationSparseBindingProductionContractTests` | EditMode 2/2 |
| `EnemyAnimationSparseBindingAssetCharacterizationTests` | EditMode 6/6 |
| `EnemyAnimationBindingEditorValidationTests` | EditMode 12/12 |
| `EnemyAnimationBindingDispatchTests` | EditMode 30/30 |
| `EnemyAnimationSparseBindingRuntimeScenarioTests` | EditMode 19/19 |
| `GameplayTimingOwnershipTests` | EditMode 94/94 |
| `RuntimeBoardBoundsGuardTests` | EditMode 26/26 |
| `EnemyPresentationReadinessPlayModeTests` | PlayMode 2/2 |
| `EnemyViewAnimatorControllerContractTests` | EditMode 8/8 |
| `EnemyViewAnimatorRuntimeCharacterizationPlayModeTests` | PlayMode 18/18 |
| `./run_tests.sh core` | EditMode 254/254, PlayMode 111 total / 107 passed / 4 skipped / 0 failed |

targeted filter의 owning stage count 0은 성공 evidence로 사용하지 않았다. broad unfiltered `full`과 `ui` lane은
실행하지 않았으며 project-wide/full green을 주장하지 않는다. UI runtime/asset 변경이 없어 `ui`는 비대상이다.

### 15.4 evidence와 non-claims

evidence root는
`/mnt/d/J2M/evidence/enemy-animation-sparse-binding-slice4b/20260904-093328/`이다. tests-first expected
failure, 최초 ForceReserialize no-op source/hash와 before/after no-diff, 승인된 prefab save source/hash와 exact diff,
targeted/core XML/log, Editor Inspector screenshot, final static audit를 분리했다. 최초 no-op의 operation excerpt와
Editor 완료 marker는 남지 않았으므로 evidence root만으로 menu invocation 성공까지 독립 증명하지는 않는다.

Kali, SecBot, BlackEye, JPeter Inspector에서 Driver의 `animator`-only 표면, Kali/SecBot의
no-Binding/no-Timing, JPeter의 별도 Binding과 inspected prefab의 Missing Script 부재를 수동 확인했다.
재감사 보정 후 production contract는 allowlist 10개 전체 hierarchy의 Missing Script count 0도 직접 검사한다.
최종 commit/tree 귀속은 commit hook의 core 재검증 및 closeout evidence로 분리한다.
Slice 5 screenshot polish, BlackEye material, manifest rename, broad full recovery 및 Slice 4A R0 provenance gap은
이번 구현의 완료 주장에 포함하지 않는다.

### 15.5 독립 재감사 보정과 B0 provenance

독립 재감사에서 malformed YAML delimiter가 정상 document 뒤에 있을 때 이전 block에 흡수되어 `Clean`으로
분류될 수 있는 문제를 확인했다. permanent audit은 이제 모든 column-zero YAML document delimiter를 먼저 세고,
class ID와 numeric anchor 및 optional `stripped`만 허용하는 Unity header grammar와 exact 비교한다. wrong tag,
generic delimiter, nonnumeric/trailing anchor negative case와 다른 MonoBehaviour block의 retired key 16개 전부에 대한
clean case를 추가했다.

재감사 corrective evidence는
`/mnt/d/J2M/evidence/enemy-animation-sparse-binding-slice4b/20260904-reaudit-corrective/`에 분리한다. 최초 새
negative case는 기존 parser에서 owning fixture `14 total / 1 failed`로 예상대로 red였고, parser와 caller
allowlist 보정 후 manifest+production contract는 EditMode `16 total / 0 failed`다. Corrective tree의 core는
EditMode `254/254`, PlayMode `111 total / 107 passed / 4 skipped / 0 failed`로 다시 확인했다.

원 evidence의 `00-preflight/revision-status-diff.txt`는 base revision은 맞지만 clean 착수 시점이 아니라 구현
touch set 22개가 이미 dirty인 시점에 저장됐다. 따라서 그 artifact로는 착수 당시 clean status와 사용자 변경
부재를 증명하지 않는다. Parent commit blob, baseline prefab SHA, before YAML, atomic parent-to-commit diff로 최종
scope와 160행 결과는 재구성할 수 있지만 시간 순서상 clean-preflight evidence는 소급 생성하지 않는다. 이
provenance gap은 runtime/asset 결과와 분리하며 strict 완료 판정에는 governance owner의 명시적 수용이 필요하다.

### 15.6 B0 governance owner 판정

2026-09-04 governance owner는 위 B0 clean-preflight provenance 공백을 runtime/asset 결과와 분리된 절차 예외로
명시적으로 수용했다. 이 판정은 clean-preflight가 수행됐다고 소급 주장하거나 누락 증거를 생성한 것으로 간주하지
않는다. parent commit blob, baseline prefab SHA, before YAML, atomic parent-to-commit diff와 최종 validation으로
재구성된 결과 계약은 유지하며 runtime, asset identity, rollback 및 residual-audit Gate는 면제하지 않는다.

이 판정으로 Slice 4B의 B0 owner gate는 닫혔다. Slice 5 Inspector evidence/문서 마감은 별도 승인과 실행 범위를
요구하는 후속 단계이며 이 owner 판정만으로 자동 착수하지 않는다.

### 15.7 post-push YAML fail-closed 보정

2026-09-05 post-push 독립 재검토에서 document header의 numeric anchor가 생략된 입력과 delimiter 뒤 공백이
없는 `---!u!114 &2` 입력이 `Clean`으로 silent pass할 수 있는 Medium finding을 확인했다. production prefab의
현재 YAML은 모두 정상 anchor를 사용하므로 기존 160행 제거 결과나 runtime/Inspector 계약에는 영향이 없었지만,
permanent residue audit의 fail-closed StrongContract를 충족하지 못하므로 보정 전 상태는 Hold로 판정했다.

audit grammar는 모든 정상 Unity document header에서 numeric anchor를 필수로 요구하고, column zero에서
`---`로 시작하는 모든 delimiter 후보 수가 정상 header 수와 정확히 일치해야만 block scan을 진행하도록
보강했다. anchor 누락과 no-space delimiter를 unsupported-input matrix에 추가했으며, 기존 parser에서는 owning
fixture가 EditMode `14 total / 13 passed / 1 failed`로 예상된 tests-first red를 냈다. 보정 후 같은 fixture는
EditMode `14/14`를 통과했고 production Driver block residue 0을 다시 확인했다. matching PlayMode는 0건이므로
성공 근거로 사용하지 않았다. 같은 working tree의 core는 EditMode `254/254`, PlayMode
`111 total / 107 passed / 4 skipped / 0 failed`다.

corrective evidence는
`/mnt/d/J2M/evidence/enemy-animation-sparse-binding-slice4b/20260905-0123-yaml-audit-fail-closed/`에
tests-first, final targeted, core artifact를 분리해 보존한다. 이 보정은 audit source, negative fixture와 본
closeout 기록만 변경하며 prefab, `.meta`, runtime dispatch 및 Inspector authoring에는 변경을 만들지 않는다.
이 결과로 Slice 4B 귀속 unresolved High/Medium finding은 0이며 fail-closed residue audit Gate를 닫는다. broad
unfiltered `full`, `ui`, BlackEye material baseline과 별도 Low review finding은 이 보정의 완료 주장에 포함하지
않는다.
