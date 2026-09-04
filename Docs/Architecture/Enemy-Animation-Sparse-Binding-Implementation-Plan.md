# Enemy Animation Sparse Binding 구체 구현 계획

## 1. 문서 상태와 목표

- 작성일: 2026-09-03
- 상태: Slice 0~4B 구현, targeted/core 검증 및 수동 Editor evidence 완료; Slice 5는 별도 후속
- 선행 문서: [Enemy View Prefab Scalability and Maintainability Audit](./Enemy-View-Prefab-Scalability-Maintainability-Audit.md)
- 기준점: production 10-view Animator Controller 계약 테스트

이 문서는 `EnemyAnimatorDriver`와 `EnemyAnimationTimingAuthoring`에 모든 적이 사용하지 않는 애니메이션 필드까지 노출되는 문제를 해결하기 위한 실행 계획이다. 목표는 production의 현재 시각 동작을 바꾸지 않으면서 각 Enemy View가 실제 사용하는 cue만 직렬화하고 Inspector에 표시하는 것이다.

최종 선택은 **프리팹 로컬 sparse cue binding**이다. 공용 Driver에 기능별 bool flag를 계속 추가하지 않고, 각 Enemy View 루트에 선택적으로 붙는 `EnemyAnimationBindingAuthoring`이 그 View가 사용하는 cue 항목만 가진다. 근미래 규모와 기존 프로젝트 관례를 고려해 ScriptableObject 프로필, 범용 모듈 레지스트리, 별도 compiler 계층은 도입하지 않는다.

## 2. 최종 재검토 결론

### 2.1 채택 사항

1. `EnemyAnimatorDriver`는 Animator 실행과 현재 signal 처리 순서를 소유한다.
2. `EnemyAnimationBindingAuthoring`은 View별 cue, Animator 대상 이름, 선택적 timing만 소유한다.
3. `EnemyAnimationCueCatalog`가 cue별 허용 dispatch 방식, timing 허용 여부, 지속 state 필요 여부를 코드 정책으로 관리한다.
4. Inspector는 실제 binding entry와 catalog metadata에 따라 필요한 필드만 표시한다.
5. production 10개 프리팹을 명시적 manifest로 먼저 이관하고, 비-production 4개는 별도 확인 후
   `Deleted` disposition으로 퇴역시킨다.
6. 기존 public compatibility type/API는 이번 작업에서 삭제하지 않는다.

### 2.2 기존 초안에서 보완한 사항

- **Timing과 dispatch를 분리한다.** 같은 tick의 Recovery timing과 Hit trigger를 함께 보존해야 한다.
- `UtilityRecovery`를 추가한다. DrSaturn의 Aura 동작은 Action이 아니라 Utility 계약이다.
- `JumpLanding`을 추가한다. jump 완료 시 `Move` state 복귀와 pending-state 동작을 보존해야 한다.
- Jump의 최초 trigger와 재동기화용 state 이름을 한 필드로 합치지 않는다. 필요한 경우 `sustainedStateName`을 별도로 둔다.
- v1 crossfade는 entry별 값이 아니라 **컴포넌트 단위 기본 state crossfade**로 둔다. 현행 View별 전역 값과 가장 가깝고 중복도 적다.
- 새 binding component가 존재하면 새 경로만 사용한다. cue 단위 legacy fallback은 허용하지 않는다.
- `EnemyView_Jumping`은 자동 이관하지 않는다. trigger 진입/state 재동기화와 불완전한 timing authoring을
  먼저 특성화한 뒤, production 비참여 및 invalid legacy fixture임을 근거로 Slice 3에서 삭제한다.

## 3. 선택 이유

### 3.1 전문적 설명

현재 문제는 animation capability의 부재가 아니라 **하나의 직렬화 표면에 모든 capability의 scalar 필드가 평탄화**되어 있다는 점이다. bool capability flag로 표시만 제어하면 cue가 늘 때마다 flag, 대상 이름, timing, validation 분기가 함께 증가하며 잘못된 조합도 계속 직렬화할 수 있다.

sparse binding은 capability를 별도 flag가 아닌 **binding entry의 존재 자체**로 표현한다. Inspector 크기는 전체 cue 수가 아니라 해당 View가 실제 쓰는 cue 수에 비례한다. 동시에 cue semantics는 enum과 catalog로 제한하여 임의 문자열 모듈보다 강한 검증을 유지한다.

프로젝트에는 Enemy Audio/VFX cue map의 선택적 cue 목록과 root authoring의 validation/snapshot 패턴이 이미 존재한다. 가장 가까운 prefab-local sparse 선례는 `EnemySemanticParticleEffectController.bindings`이고, 조건부 Custom Inspector 선례는 `GameplayCameraTopologyAuthoringEditor`다. 따라서 이 구조는 새로운 범용 프레임워크보다 현재 코드베이스 관례에 가깝다. `PlayerAnimatorDriver`의 flat schema는 Player 형상이 사실상 하나인 계약에는 맞지만 서로 다른 10개 production View를 가진 Enemy에는 그대로 확장하지 않는다. Animation은 Controller/FBX/Prefab 결합이 강하므로 현재는 공유 ScriptableObject로 분리할 실익보다 공유 asset 변경의 파급 위험이 더 크다.

### 3.2 쉬운 설명

현재는 적 하나가 공격, 점프, 돌진, 활공을 전혀 하지 않아도 그 칸을 전부 보고 있어야 한다. flag를 추가하면 잠시 숨길 수 있지만 새 기능마다 숨김 스위치도 계속 늘어난다.

새 방식에서는 Startis가 `Hit`, `Death` 두 줄만 가지고, Nebulous가 `GlideWindup`, `GlideActive`, `GlideRecovery` 세 줄만 가진다. 필요한 줄이 곧 기능 목록이므로 별도 스위치가 필요 없다. 다만 현재 규모보다 지나치게 큰 시스템은 만들지 않고 각 프리팹 안의 작은 목록으로 제한한다.

## 4. 계약 분류

### 4.1 Strong Contract — 보존 대상

- production catalog의 10개 View와 각 Animator Controller/FBX 결합
- animation presentation과 `EnemyAiProfile`의 독립성
- presentation이 authoritative simulation을 변경하지 않는 원칙
- 현재 signal 순서, 중복 억제, signal counter 처리
- Hit와 Death가 같은 tick에 들어올 때의 호출 순서와 횟수
- 현재 timing 우선순위와 speed/duration 결과
- jump topology suspend/resume 시 pose, state, normalized time 복원
- 일반 Animator 재활성화/resync에서는 마지막 state를 다시 진입한다. normalized time 보존은 jump topology suspend/resume 경로에만 한정한다.
- playback suppression 중 계산된 speed/duration은 유지하고 실제 `Animator.speed`만 `0`으로 만들며, 해제 시 계산된 speed를 복원하는 규칙
- Animator 비활성 시 state만 한 건 pending하고 trigger는 pending하지 않는 규칙
- 새 state가 기존 pending state를 덮어쓰는 규칙
- Death에서 Animator speed `1`, presentation duration `0` 유지
- 사망 View 수명은 exit/DeathMotion이 소유하며 animation timing이 연장하지 않는 규칙
- DeathMotion VFX의 pose freeze/capture 동작
- Utility windup/recovery presentation track이 logic phase보다 오래 유지될 수 있는 동작
- `EnemyAnimationTimingAuthoring`, `EnemyAnimationTimingSnapshot`, 기존 phase 기반 public API의 source compatibility

### 4.2 Current Policy — 교체 가능

- 모든 state/trigger 문자열이 `EnemyAnimatorDriver`에 있는 구조
- 모든 timing 필드가 `EnemyAnimationTimingAuthoring`에 항상 노출되는 구조
- global crossfade 값의 존재로 Trigger/State 경로가 암묵적으로 결정되는 규칙
- private serialized field 이름을 그대로 고정하는 reflection 테스트

## 5. 목표 데이터 모델

### 5.1 안정적인 cue 값

직렬화 안전성을 위해 enum 값은 명시적으로 고정한다. 이후 재정렬과 값 재사용을 금지하며 제거된 값은 tombstone으로 남긴다.

```csharp
public enum EnemyAnimationCue
{
    None = 0,
    ActionWindup = 10,
    ActionExecute = 11,
    ActionRecovery = 12,
    JumpWindup = 20,
    JumpAirborne = 21,
    JumpLanding = 22,
    ChargeWindup = 30,
    ChargeActive = 31,
    ChargeRecovery = 32,
    GlideWindup = 40,
    GlideActive = 41,
    GlideRecovery = 42,
    UtilityWindup = 50,
    UtilityRecovery = 51,
    Hit = 90,
    Death = 91,
}
```

### 5.2 Binding entry

각 `EnemyAnimationCueBinding`은 다음을 가진다.

- `cue`: 의미 계약. 한 Authoring 내 중복 불가.
- `primaryDispatchMode`: `Trigger` 또는 `State`.
- `targetName`: 최초 dispatch 대상 parameter/state 이름.
- `sustainedStateName`: 최초 진입은 trigger지만 복원/resync에는 state가 필요한 cue에서만 사용.
- `animatorDurationSeconds`: timing 지원 cue만 표시. 유한한 `-1`만 duration override 없음 sentinel이고 override 값은 유한한 양수만 허용한다. `0`, `-1`보다 작은 값, `NaN`, `Infinity`는 오류다.
- `referenceClip`: timing 지원 cue만 표시. clip 길이 검증/fallback에 사용.

`EnemyAnimationBindingAuthoring`은 다음 필드와 API를 가진다. 기존 sparse authoring 관례에 맞춰 entry는 nullable class/List가 아니라 `[Serializable] struct`와 고정 배열을 사용한다.

```csharp
[SerializeField] private float defaultStateCrossFadeDurationSeconds = -1f;
[SerializeField]
private EnemyAnimationCueBinding[] bindings =
    Array.Empty<EnemyAnimationCueBinding>();

public void Validate();
public EnemyAnimationBindingSnapshot CreateSnapshot();
```

- Enemy View 루트의 선택적 component다.
- binding이 필요 없는 Kali/SecBot에는 붙이지 않는다.
- 실제 `CrossFadeInFixedTime`을 사용하는 primary `State` binding이 있을 때만 crossfade 필드를 표시한다. resync 전용 `sustainedStateName`만으로는 표시하지 않는다.
- primary State binding이 있으면 crossfade는 유한한 `0` 이상을 명시해야 한다. State가 전혀 없을 때만 `-1`을 허용한다. `-1`은 trigger fallback이나 암묵적 0초 전환을 뜻하지 않는다.
- runtime은 매 frame list를 찾지 않고 검증된 immutable lookup을 사용한다.
- prefab composition은 초기화 시점에 고정하며 runtime 동적 add/remove는 지원하지 않는다.
- root에는 이 component가 정확히 0개 또는 1개만 존재할 수 있다. child 배치, disabled component, 중복 component, null/empty binding list는 component가 존재하는 경우 hard failure다.

### 5.3 Cue catalog 정책

Catalog는 `RequiresResolvableState`와 `RequiresSeparateSustainedStateWhenPrimaryTrigger`를 구분한다. State primary는 `targetName`으로 resolvable-state 요구를 만족하고, Trigger primary인 JumpAirborne만 별도 `sustainedStateName`을 요구한다.

| Cue | 허용 primary dispatch | Timing | 별도 `sustainedStateName` 요구 |
|---|---|---:|---:|
| ActionWindup | Trigger, State | 허용 | 아니오 |
| ActionExecute | Trigger | 금지 | 아니오 |
| ActionRecovery | Trigger, State | 허용 | 아니오 |
| JumpWindup | Trigger, State | 허용 | 아니오 |
| JumpAirborne | Trigger, State | 허용 | Trigger 방식일 때만 예 |
| JumpLanding | State | 금지 | 아니오 |
| ChargeWindup | Trigger, State | 허용 | 아니오 |
| ChargeActive | State | 금지 | 아니오 |
| ChargeRecovery | Trigger, State | 허용 | 아니오 |
| GlideWindup | State | 허용 | 아니오 |
| GlideActive | State | 금지 | 아니오 |
| GlideRecovery | State | 허용 | 아니오 |
| UtilityWindup | Trigger, State | 허용 | 아니오 |
| UtilityRecovery | Trigger, State | 허용 | 아니오 |
| Hit | Trigger | 금지 | 아니오 |
| Death | Trigger | 금지 | 아니오 |

Inspector와 runtime validation은 같은 catalog를 사용한다. Hit/Death/ActionExecute/ChargeActive/GlideActive/JumpLanding은 timing 필드를 숨기는 데 그치지 않고 YAML이나 코드로 잘못 들어온 timing 값도 거부한다.

`JumpAirborne`이 State 방식이면 `targetName`이 지속 state 이름이다. Trigger 방식이면 `sustainedStateName`을 별도로 요구한다. 이 규칙으로 production Astreton과 비-production Jumping을 모두 표현한다.

State 방식의 `targetName`은 그 자체가 복원 가능한 state다. 따라서 `JumpLanding`, `ChargeActive`, `GlideActive`는 별도 `sustainedStateName`을 요구하지 않는다. 즉시 전환이 필요하면 crossfade를 `0`으로 명시하며, state binding과 `-1` crossfade의 조합은 거부한다.

### 5.4 Timing truth table

현행 reference clip fallback을 그대로 보존한다.

| Authored duration | Reference clip | 결과 |
|---:|---|---|
| `-1` | 없음 | presentation duration `0`, speed `1` |
| `-1` | 있음 | presentation duration은 자연 clip 길이, speed `1` |
| 유한한 양수 | positive-length clip 있음 | 지정 duration, speed = `clipLength / duration` |
| 유한한 양수 | 없음 또는 유효하지 않은 clip | validation error |
| 그 외 값 | 무관 | validation error |

Death와 timing 금지 cue는 항상 duration `0`, speed `1`이며 위 authoring 조합 자체를 허용하지 않는다.

## 6. Runtime 구조

### 6.1 Timing과 dispatch 분리

Driver의 한 번의 `Apply`에서 다음 두 결정을 독립적으로 수행한다.

```text
EnemyViewPresentationState
  ├─ ResolveActiveTimingCue(state)
  │    └─ ApplyAnimatorTimingForCue(cue)
  └─ 기존 signal 순서/억제 조건
       └─ DispatchCue(cue)
```

Recovery가 유지되는 tick에 Hit가 들어오면 Recovery cue가 speed/duration을 결정하고 Hit cue는 trigger만 발생시킨다. `DispatchCue(Hit)`가 timing까지 바꾸면 현행 동작이 깨지므로 금지한다.

`ResolveActiveTimingCue`는 base presentation state를 `Death/reset > Jump > Charge > Glide > Action > None` 우선순위로 해석하며 `UtilityWindup`/`UtilityRecovery`를 절대 반환하지 않는다. Utility timing은 `EnemyUtilityAnimationPlaybackTrack`이 활성인 동안만 exact cue override로 적용한다. track 종료 시 `RestorePresentationTiming()`이 비-Utility baseline을 복원한다. 이 분리가 없으면 Utility speed가 재적용되거나 presentation tail이 잘릴 수 있다. Death suppression은 두 경로보다 우선한다.

`Apply` 전체를 generic cue loop로 다시 쓰지 않는다. 현재 조건, signal counter, 중복 억제, locomotion/one-shot 상호작용, Death 억제 순서를 유지하고 최종 Animator 호출만 cue lookup으로 교체한다.

추가할 의미 단위 API:

```csharp
EnemyAnimationCue ResolveActiveTimingCue(in EnemyViewPresentationState state);
void ApplyAnimatorTimingForCue(EnemyAnimationCue cue);
EnemyAnimationDispatchResult DispatchCue(EnemyAnimationCue cue);
float GetPresentationDurationSeconds(EnemyAnimationCue cue);
void ApplyPresentationCueTiming(EnemyAnimationCue cue);
```

`EnemyAnimationDispatchResult`는 `Unsupported`, `Applied`, `Queued`, `AnimatorUnavailable`을 구분한다. Utility presentation track은 generic phase가 아니라 `UtilityWindup`/`UtilityRecovery`의 정확한 cue를 보관한다.

일반 resync 계약도 cue lookup으로 보존한다.

- State-mode active phase는 signal count를 올리지 않고 resync할 수 있다.
- JumpAirborne/ChargeActive/GlideActive와 State 방식 Windup/Recovery/JumpWindup, Glide windup/recovery가 현행 대상이다.
- Trigger-only cue는 일반 resync하지 않는다.
- Utility dispatch/timing은 Utility track 소유이며 일반 state resync 대상이 아니다.
- `SyncRuntimeState`, `SyncHiddenRuntimeState`, `RestorePresentationTiming`, Utility timing 적용/종료 모두 suppression 상태의 계산값/실제 Animator speed 분리를 지킨다.

### 6.2 Pending state

pending state는 문자열 한 개가 아니라 `Cue`, `StateName`, `CrossFadeSeconds`를 고정한 payload로 저장한다.

- state만 queue하고 trigger는 queue하지 않는다.
- slot은 기존처럼 하나다.
- 새 state가 이전 값을 덮어쓴다.
- `LastCrossFadedStateName`은 기존 계약대로 queue 시점에 갱신한다.
- queue 뒤 authoring 값이 바뀌어도 이미 만든 command는 변하지 않는다.

### 6.3 Public compatibility

이번 작업에서 `EnemyAnimationTimingAuthoring`, `EnemyAnimationTimingSnapshot`, 기존 phase 기반 duration/timing 메서드는 삭제하지 않는다. 새 runtime 호출자는 cue API로 이동하고 기존 API는 Driver/coordinator 쪽 adapter로 남긴다. 기존 public timing source 자체는 가능한 한 변경하지 않는다.

source symbol/signature와 현재 production View의 behavior compatibility는 보존한다. 다만 새 cue authoring 자체는 Action과 Utility 등 여러 family의 공존을 허용한다. 모호한 generic phase adapter가 실제 호출될 때만 명시적 오류를 내거나 `LastPresentationState` 기반 결정 규칙을 사용하며, legacy API 때문에 새 schema 전체를 validation failure로 만들지 않는다. 어느 adapter 규칙을 택할지는 기존 호출자 characterization test로 먼저 고정한다. public legacy API 제거는 별도 사용처 조사와 obsolete 기간, 승인이 필요한 후속 작업이다.

## 7. Production 10-view 목표 계약

| View | Cue | Mode / target | Migration timing | Crossfade |
|---|---|---|---|---:|
| BlackEye | ActionWindup | State / `Windup` | 현행 값 유지 | 0.001 |
|  | ActionRecovery | State / `Recover` | 현행 값 유지 |  |
|  | Hit / Death | Trigger / `Hit`, `Death` | 금지 |  |
| Startis | Hit / Death | Trigger / `Hit`, `Death` | 금지 | 숨김 |
| RocketFace | ChargeWindup | State / `Windup` | 0.4s | 0.001 |
|  | ChargeActive | State / `Charge` | 금지 |  |
|  | ChargeRecovery | State / `Recover` | 0.4s |  |
|  | Hit / Death | Trigger / `Hit`, `Death` | 금지 |  |
| Astreton | JumpWindup | State / `JumpWindup` | 0.5s | 0.001 |
|  | JumpAirborne | State / `JumpAirborne` | 1.35s |  |
|  | JumpLanding | State / `Move` | 금지 |  |
|  | ActionExecute / Hit / Death | Trigger / `Attack`, `Hit`, `Death` | 금지 |  |
| DrSaturn | UtilityWindup | Trigger / `Windup` | 0.5s | 숨김 |
|  | UtilityRecovery | Trigger / `Recover` | 0.5s |  |
|  | Hit / Death | Trigger / `Hit`, `Death` | 금지 |  |
| JPeter | Hit / Death | Trigger / `Hit`, `Death` | 금지 | 숨김 |
| Sunwheel | Hit / Death | Trigger / `Hit`, `Death` | 금지 | 숨김 |
| Kali | binding component 없음 | - | - | - |
| SecBot | binding component 없음 | - | - | - |
| Nebulous | GlideWindup | State / `Fly_Start` | 0.25s | 0 |
|  | GlideActive | State / `Fly_Loop` | 금지 |  |
|  | GlideRecovery | State / `Fly_Done` | 0.25s |  |

표의 “현행”은 새 이름을 만들라는 뜻이 아니다. manifest에는 실제 prefab 문자열과 clip GUID/fileID를 기록한다. Animator Controller와 FBX는 이번 작업에서 수정하지 않는다.

표의 exact timing/crossfade 값은 **같은 revision의 migration 전후 동등성 baseline**이다. 장기 production 계약은 cue/mode/target, 값의 finite/허용 범위, clip 연결, fallback 정책을 검증한다. 이후 정상 animation tuning을 막는 영구 exact assertion으로 사용하지 않는다. 값을 장기 고정해야 하는 예외는 별도 `locked migration timing baseline` 근거와 테스트 이름을 요구한다.

### 7.1 Astreton 선행 특성화

현재 Astreton은 trigger 문자열도 직렬화하지만 crossfade override 때문에 runtime이 state 경로를 선택한다. 이관 전에 PlayMode test로 다음을 고정한다.

- JumpWindup/JumpAirborne에서 실제 state crossfade가 발생한다.
- 같은 순간 대응 jump trigger write는 발생하지 않는다.
- landing/complete에서 `Move` state command가 발생한다.
- Animator 비활성 구간이면 state command가 현행 규칙대로 pending된다.

이 결과가 확정된 뒤 새 Astreton binding에는 미사용 jump trigger를 넣지 않는다.

## 8. Prefab inventory와 이관 경계

Slice 0 착수 시점의 GUID 기반 migration baseline:

- `EnemyAnimatorDriver` 참조 prefab: 14개
- `EnemyAnimationTimingAuthoring` 참조 prefab: 12개
- Scene 및 `.asset` 직접 참조: 확인되지 않음
- production: 10개
- 비-production: `EnemyView_Attacking`, `EnemyView_NonAttacking`, `EnemyView_Jumping`, `EnemyView_PrototypeGravityFieldChaser`

이 숫자는 historical migration allowlist baseline이다. Slice 3 완료 후 current live inventory는 Driver 10,
Binding 8, Timing 0이며 baseline 14개는 disposition ledger로 계속 추적한다.

### 8.1 `EnemyView_Jumping` blocker

`EnemyView_Jumping`은 다음 이유로 legacy private field 제거의 명시적 blocker다.

- 최초 airborne 진입은 trigger를 사용할 수 있다.
- 지속/복원/resync에는 별도 state 이름이 필요하다.
- timing override가 있으나 reference clip이 비어 있고 crossfade도 sentinel이라 의도를 자동 추론할 수 없다.

현재 prefab은 invalid timing authoring 때문에 정상 `Apply` 경로가 먼저 예외를 낼 수 있다. 따라서 현 상태를 곧바로 정상 visual contract로 고정하지 않는다. 먼저 raw prefab/AssetDatabase test로 trigger 이름, sustained state 이름, invalid timing 값을 기록하고 synthetic valid fixture로 trigger 최초 진입과 state resync 메커니즘을 증명한다. asset 결정을 반영해 prefab을 유효하게 만든 뒤에만 실제 prefab PlayMode visual characterization을 추가한다.

그 뒤 asset 소유자가 다음 중 하나를 선택한다.

1. 정확한 clip/timing으로 repair
2. override를 제거하고 Controller 기본 속도 사용
3. 미사용 prototype이면 archive/delete를 별도 변경으로 수행
4. 결론이 없으면 legacy 경로를 유지하고 최종 private field 제거를 연기

Slice 3에서는 사용자/asset owner가 3번 삭제를 승인했다. production catalog와 serialized asset에서 inbound
reference가 없고 전용 Astreton View가 현재 Jump 계약을 소유하므로 해당 prefab은 repair 없이 삭제했다.

## 9. 직렬화와 migration

`FormerlySerializedAs`는 여러 scalar를 다른 component의 배열 entry로 이동할 수 없다. 비어 있지 않은 필드만 보고 cue를 추론해도 실제 runtime 선택과 달라질 수 있으므로 path/GUID별 명시적 manifest를 사용한다.

Manifest는 migration 도구와 계약 테스트가 함께 읽는 **체크인된 machine-readable Editor 전용 immutable table**로 둔다. 외부 로그나 문서 표를 실행 source of truth로 사용하지 않는다. schema version을 명시한다.

Manifest 기록 항목:

- prefab path/GUID, production 여부, baseline disposition, 승인 상태
- raw YAML field 존재 여부와 Unity 로드 후 resolved value
- Driver의 TimingAuthoring object reference 및 같은 root 여부
- Animator explicit-null 여부, resolved Animator transform path/local file ID
- Controller 또는 OverrideController GUID/local file ID
- 기존 Driver state/trigger/parameter 값
- timing duration, reference clip GUID/fileID, crossfade
- 목표 cue/mode/target/sustained-state/timing
- expected Controller graph/state/parameter 계약, binding entry 순서, migration schema version

Prefab 단위 read 규칙:

- 새 component 없음: legacy만 사용
- 새 component 있음: new만 사용
- 새 component가 비었거나 불완전함: hard failure
- cue 단위 legacy fallback: 금지
- root 이외의 배치, disabled/중복 component: hard failure이며 legacy fallback 금지

Editor migration 도구는 `PrefabUtility.LoadPrefabContents`를 사용해 다음 순서를 따른다.

1. path/GUID allowlist 확인
2. 기존 값을 메모리 snapshot으로 읽음
3. dry-run semantic diff 출력
4. 새 component 구성
5. catalog validation과 manifest 비교
6. 성공한 prefab만 저장
7. reload 후 Animator/Controller/clip GUID/fileID와 binding 재검증

dry-run, validation, 수동 확인 evidence는 storage policy에 따라 `/mnt/d/J2M/evidence/<change-id>/` 아래에 새로 저장한다.

production 10개를 먼저 이관한다. 비-production 4개는 characterization과 owner 결정을 마친 뒤
`Deleted`로 기록한다. 모든 14개 disposition이 해결되기 전에는 legacy Driver private field를 삭제하지 않는다.

`EnemyAnimatorDriver.cs.meta` GUID는 유지한다. `EnemyAnimationTimingAuthoring`의 prefab 참조가 0이 되어도 public compatibility source와 `.meta`는 보존한다. Residue 검사는 다음처럼 scope를 제한한다.

- Timing script GUID: `.prefab`, `.unity`, `.asset` 참조 0. compatibility `.meta`는 제외.
- Driver legacy field: Driver script GUID의 MonoBehaviour YAML block 안에서만 retired token 0.
- active C#: mutation tool 제거 후 old private field string 사용처 0. compatibility source와 archive 문서는 allowlist.
- final Driver reflection: serialized field allowlist는 `animator` 하나.

YAML은 residue 검출용으로만 사용하며 semantic validation은 AssetDatabase를 사용한다.

Baseline 14개는 live prefab count가 아니라 disposition ledger로 추적한다.

- `MigratedBinding`: 새 binding으로 이관
- `ApprovedNoBinding`: animation cue가 없어 component 없이 승인. Kali/SecBot이 해당
- `Archived` / `Deleted`: 별도 승인된 자산 처분
- `LegacyBlocked`: 아직 legacy 의존

항상 `baseline disposition count == 14`여야 한다. 실제 live Driver prefab 수는 `MigratedBinding + ApprovedNoBinding + LegacyBlocked`와 일치해야 하며 Slice 4 진입 조건은 `LegacyBlocked == 0`이다.

## 10. 구현 Slice와 Gate

### Slice 0 — 현행 Characterization

실행 시에는 [Slice 0 실행 프롬프트](./Enemy-Animation-Sparse-Binding-Slice0-Characterization-Prompt.md)를 사용한다.

- Astreton state crossfade/no-trigger/landing/pending 계약
- Jumping raw asset의 trigger/state/invalid timing 구조 진단
- synthetic valid fixture의 trigger 최초 진입 + state resync 계약
- Recovery timing과 same-tick Hit dispatch 동시 유지 계약
- Sunwheel의 미사용 crossfade가 trigger를 state로 바꾸지 않는 계약
- 실제 DrSaturn prefab의 GravityFieldAura windup/recover trigger와 0.5초 migration baseline, Hit/Death 순서
- 일반 sustained-state resync가 signal count를 증가시키지 않는 계약
- playback suppression이 계산된 timing을 보존하고 실제 Animator speed만 정지/복원하는 계약

Gate: 관찰이 실제 Controller/Prefab과 일치해야 Slice 1로 진행한다. Jumping 처리 방침이 미정이어도 production 작업은 가능하지만 Slice 4는 불가하다.

### Slice 1 — Schema와 호환 runtime

테스트를 먼저 작성하고 다음을 추가한다.

- cue enum/numeric stability, dispatch mode/result, cue catalog
- binding entry, optional Authoring, immutable snapshot, Editor-only Custom Inspector
- duplicate/빈 target/허용되지 않은 mode·timing/sustained-state 누락 검증
- duration/reference-clip truth table과 crossfade sentinel/finite 값 검증
- prefab-exclusive legacy/new 선택
- timing resolve와 dispatch 분리
- payload 기반 pending state
- cue 기반 duration/timing API와 phase compatibility adapter
- `EnemyViewPrefabRequirements`의 optional binding validation
- Enemy Presentation 소유의 Editor-only asmdef(`includePlatforms: [Editor]`, runtime Gameplay assembly 참조)
- Editor-only Controller graph validation과 Inspector 진단

Runtime structural validation과 Editor Controller validation을 분리한다. Editor 층은 Trigger parameter가 존재하며 실제 `Trigger` 타입인지, 실행 가능한 transition에서 소비되는지, state/sustained state가 runtime과 같은 name-resolution 규칙으로 실제 layer에 존재하는지 검사한다. Direct `AnimatorController`와 Animator에 최종 할당된 effective `AnimatorOverrideController`를 지원한다. Unity가 AOC-from-AOC 생성 시 base Controller로 flatten하는 동작은 synthetic characterization으로 고정하며 실제 nested AOC chain 지원은 주장하지 않는다. Controller graph는 최종 assigned controller가 가리키는 base `AnimatorController`에서 검증하고 reference clip은 assigned runtime controller의 effective `animationClips` membership만 검증한다. Custom Inspector에는 structural/Controller 진단을 함께 표시하고 Editor API는 Player assembly에 포함하지 않는다.

AOC constructor flattening 또는 assigned controller의 effective `animationClips`가 synthetic characterization과 다르면 Slice 1을 Hold하고 Controller/clip 정책을 다시 결정한다.

Gate: prefab YAML은 수정하지 않은 상태에서 legacy-only 테스트가 유지되고 synthetic new-binding 테스트가 통과해야 한다.

### Slice 2 — Production 10개 migration

구체적인 manifest schema, dry-run/apply 순서, production allowlist, evidence와 rollback 절차는 [Enemy-Animation-Sparse-Binding-Slice2-Production-Migration-Plan.md](./Enemy-Animation-Sparse-Binding-Slice2-Production-Migration-Plan.md)를 따른다.

상태: **완료 — production apply, automated contract hardening, 사용자/asset owner 수동
Inspector 및 visual parity 확인 완료(2026-09-04)**. 최종 inventory, RocketFace
timing-reference/effective-motion 재검토, Trigger destination/effective Motion 계약과 수동 확인은
[Slice 2 Production Migration Closeout](./Enemy-Animation-Sparse-Binding-Slice2-Production-Migration-Closeout.md)에 기록한다.

- exact manifest와 dry-run evidence 작성
- production 10개에 새 binding 적용
- Kali/SecBot에는 component를 추가하지 않음
- production prefab의 기존 timing component 제거
- contract test를 private field가 아닌 cue/mode/target/timing matrix로 전환
- Controller/FBX diff 없음 확인

Gate: production contract, runtime characterization, utility timing, DeathMotion pose freeze, jump topology test를 통과해야 한다.

### Slice 3 — 비-production 4개 처리

- 완료: Attacking, NonAttacking, Jumping, PrototypeGravityFieldChaser를 production 비참여 legacy
  test/prototype residue로 확정하고 prefab 및 `.meta`를 삭제했다.
- Jumping 전용 inactive-compatible material도 단일 참조 orphan으로 함께 삭제했다.
- production manifest 10행은 유지하고 별도 immutable disposition ledger에 `Deleted` 4행을 기록했다.
- live inventory는 Driver 10, Binding 8, Timing 0이며 `LegacyBlocked == 0`이다.
- 공유 production controller와 test fixture로 남은 controller/clip/material은 연쇄 삭제하지 않았다.

Gate: baseline 14개 모두 disposition이 있고 `LegacyBlocked == 0`이다. live Driver 10개는
`MigratedBinding 8 + ApprovedNoBinding 2`와 일치하며 Timing Authoring prefab reference는 0이다.

### Slice 4A — One-time migration tool 퇴역

- 완료: production dry-run/apply menu, apply service, report/status/digest, serialized mutation helper와
  migration-only synthetic serialization test를 제거했다.
- production semantic manifest 10행과 disposition ledger 14행은 current asset identity만 담는 read-only
  계약으로 유지한다.
- resolved prefab inventory와 Prefab Variant inheritance, deleted GUID inbound reference를 permanent audit으로
  전환했다.
- production prefab과 protected asset 및 runtime API는 변경하지 않았다.

Gate: resolved Driver 10, Binding 8, Timing 0, deleted GUID residue 0, retired symbol residue 0과 production
Controller/runtime characterization을 만족해야 한다. 상세 결과는
[Migration Tool Retirement Closeout](./Enemy-Animation-Sparse-Binding-Migration-Tool-Retirement-Closeout.md)에 기록한다.

### Slice 4B — Legacy private Inspector 표면 제거

상태: **구현 및 독립 재감사 code/report 보정 완료(2026-09-04); Slice 4B B0 clean-preflight provenance owner 판정 대기**.

구체적인 fallback 정책, tests-first, prefab reserialize, evidence 및 rollback 절차는
[Slice 4B Legacy Inspector Retirement Plan](./Enemy-Animation-Sparse-Binding-Slice4B-Legacy-Inspector-Retirement-Plan.md)을
따른다. 계획 검토만으로 구현을 자동 승인하지 않는다.

구현 결과와 최초 ForceReserialize no-op 후 별도 승인된 exact prefab save seam, validation count 및 non-claim은
위 Slice 4B 문서의 구현 Closeout에 기록한다.

2026-09-04 runtime, asset/test, governance 서브 에이전트 재검토에서 High finding은 없었고, JumpAirborne
restorable-state 우회 차단, exact path/GUID와 assets-only reserialize 절차, 160-line exact diff,
attributable High/Medium Hold 기준을 필수 조건으로 계획에 반영했다.

- Driver의 이관 완료 state/trigger/timing private serialized field 제거
- Driver Inspector에는 Driver 자체 책임만 남김
- scope가 제한된 retired YAML/C# token 잔존 검사
- Timing Authoring component asset 참조 0 확인
- public compatibility source/type/API 유지
- Slice 4A의 read-only manifest/audit을 유지하며 mutation service를 재도입하지 않음

Gate: baseline 14 disposition ledger, live count 식, `LegacyBlocked == 0`, semantic AssetDatabase 검증이 모두 맞아야 한다. public type 제거는 포함하지 않는다. Legacy private field 제거는 Slice 4A가 자동 승인하지 않으며 별도 decommission intent로 진행한다.

### Slice 5 — Inspector evidence/문서 마감

- unsupported 조합은 HelpBox와 runtime validation에서 같은 오류로 표시
- 대표 View 수동 확인/screenshot evidence
- Architecture 문서 상태와 후속 링크 갱신

## 11. 예상 변경 범위

초기 구현 시 예상한 파일군(historical; current file-existence inventory가 아님):

- `EnemyAnimationBindingTypes.cs`
- `EnemyAnimationCueCatalog.cs`
- `EnemyAnimationBindingAuthoring.cs`
- `Editor/EnemyAnimationBindingAuthoringEditor.cs`
- `Editor/EnemyAnimationBindingMigrationTool.cs` — Slice 2 one-time implementation; Slice 4A에서 퇴역하여 현재는 부재
- `Editor/Game.Feature.Gameplay.EnemyPresentation.Editor.asmdef` — `includePlatforms: [Editor]`
- `EnemyAnimationBindingAuthoringTests.cs`
- `EnemyAnimationBindingDispatchTests.cs`

수정 파일군:

- `EnemyAnimatorDriver.cs`
- `EnemyAnimationTimingAuthoring.cs` — 원칙적으로 기능 변경 없음; 별도 승인된 compatibility annotation만 허용
- `EnemyViewPrefabRequirements.cs`
- Utility presentation timing/coordinator
- `EnemyViewAnimatorControllerContractTests.cs`
- `RuntimeBoardBoundsGuardTests.cs`
- `GameplayTimingOwnershipTests.cs`
- `GameplayTickPresentationCoordinatorTests.cs`
- `EnemyPrefabScaffoldTests.cs`
- `EnemyViewPresentationAuthoringTests.cs`
- Enemy View runtime characterization PlayMode tests
- production 10개와 승인된 비-production Enemy View prefab
- 관련 Architecture 문서

비범위:

- Animator Controller/FBX/Scene 변경
- Enemy AI profile/compiler 변경
- Death logic, View exit 시간, DeathMotion pose capture 변경
- 범용 presentation module/compiler registry
- animation binding ScriptableObject
- Driver 전체 분해 리팩터링
- public legacy timing type 제거

## 12. 검증 계획

Test-first 순서:

1. Astreton/Jumping 현행 characterization
2. cue numeric stability와 catalog policy
3. Authoring validation/snapshot
4. dispatch result와 pending payload
5. timing/dispatch 독립성
6. production 10 semantic contract matrix
7. Utility exact cue timing
8. migration inventory/serialization residue

계층 분류:

- 배치 `Core`, Category `Core`: cue catalog/numeric stability, reflection 없는 deterministic validation/snapshot
- 배치 `Infrastructure`, Category `Full`: reflection/serialized shape, prefab inventory, GUID/fileID, YAML residue, Controller graph
- 배치 `Integration`, Category `Extended` 또는 `Full`: Animator dispatch/timing/Utility track
- 배치 `Integration`, Category `Full`: 실제 production prefab PlayMode characterization

실행 tier(Category)와 테스트 배치 계층을 별도 축으로 기록한다. Core test가 private field reflection으로 값을 주입하지 않도록 internal test factory/constructor를 제공하고, reflection이 필요하면 Infrastructure로 옮긴다.

private field 이름을 고정하던 테스트는 Driver 자체 field, Binding Authoring의 binding array/crossfade, production semantic matrix를 각각 검증하도록 바꾼다.

기존 timing/Driver reflection injection과 synthetic fixture는 새 binding snapshot을 구성하도록 전환한다. Startis/BlackEye에 TimingAuthoring component가 반드시 있다고 가정하는 테스트와 raw timing YAML 기대값도 Slice 2에서 새 disposition/semantic 계약으로 교체한다.

```bash
./run_tests.sh full --filter EnemyViewAnimatorControllerContractTests
./run_tests.sh full --filter EnemyAnimationBindingAuthoringTests
./run_tests.sh full --filter EnemyAnimationBindingDispatchTests
./run_tests.sh full --filter GameplayTimingOwnershipTests
./run_tests.sh full --filter EnemyViewAnimatorRuntimeCharacterizationPlayModeTests
./run_tests.sh core
```

Utility fixture가 별도이면 `EnemyViewPresentationMapperTests`, `GameplayTickPresentationCoordinatorTests`도 targeted 실행한다. Inspector 변경은 runtime UI 변경이 아니므로 `ui` lane은 기본 요구가 아니며 UI asset을 실제 변경할 때만 실행한다.

문서에는 실제 실행한 lane만 기록한다. 기존 full baseline이 red이면 touched-cluster 결과와 무관한 baseline 실패를 분리하고 broad/full green을 주장하지 않는다.

수동 Editor 표본:

- Kali: animation binding component 없음
- Startis: Hit/Death만 표시, timing/crossfade 숨김
- Astreton: Jump state/landing과 ActionExecute/Hit/Death 표시
- DrSaturn: UtilityWindup/UtilityRecovery 표시
- Nebulous: Glide 3 state와 View-level crossfade만 표시

또한 reimport 후 Missing Script 없음, Controller/FBX diff 없음, clip GUID/fileID 유지, 주요 애니메이션 시각 결과를 확인한다.

## 13. Commit과 rollback

권장 commit 분리:

1. `test: enemy animation - characterize state and trigger contracts`
2. `refactor: enemy animation - add sparse cue binding runtime`
3. `refactor: enemy animation - migrate production view prefabs`
4. `refactor: enemy animation - retire unused legacy view prefabs`
5. `refactor: enemy animation - remove retired inspector fields`
6. `refactor: enemy animation - finalize cue based inspector validation`
7. `docs: enemy animation - close sparse binding migration`

실제 commit 제목은 저장소 규칙의 system scope를 사용한다. 예: `refactor: Gameplay/EnemyAnimation - sparse cue binding runtime 도입`. 각 commit body에는 이유·방식·개선점을 설명하는 두 개 이상의 bullet을 넣고, Prefab commit에는 각 Prefab 변경 목적을 명시한다.

Slice 1 runtime/schema는 legacy-only prefab을 지원해야 한다. Slice 2 rollback은 새 component만 지우는 수동 조작이 아니라 **production prefab migration commit 전체의 Git revert**다. 이 revert가 새 component 제거와 기존 TimingAuthoring component/reference/value 복구를 함께 수행해야 한다.

Slice 4 이후 전체 rollback 순서는 legacy schema decommission commit, 비-production deletion commit,
production migration commit의 역순 Git revert다. raw YAML 수동 복원이나 component enable/disable은
rollback으로 인정하지 않는다. Legacy private field 제거와 필수 prefab reserialize는 하나의 atomic
decommission intent로 묶는다.

## 14. 완료 조건

- production 10개 View의 Controller/FBX 결합과 시각 동작 유지
- production cue/mode/target/timing contract 통과
- 각 Inspector에 실제 cue만 노출
- Kali/SecBot에 불필요한 binding component 없음
- same-tick Recovery timing + Hit dispatch 유지
- Astreton landing/pending/topology restore 유지
- 일반 active state resync와 playback suppression timing 계약 유지
- DrSaturn은 Utility cue로 실행
- Nebulous의 `Fly_Start`/`Fly_Loop`/`Fly_Done`과 timing 유지
- Death timing authoring 금지, View lifetime/DeathMotion pose freeze 불변
- baseline 14 disposition 완료 및 `LegacyBlocked == 0`
- Jumping을 포함한 비-production 4개 삭제 결정과 증거 기록
- Controller/FBX/Scene에 의도하지 않은 diff 없음
- targeted tests와 `core` 결과를 같은 revision 기준으로 기록

## 15. 최초 착수 작업

가장 먼저 Slice 0의 두 characterization test를 추가한다.

1. Astreton이 실제로 Jump state crossfade를 사용하고 대응 trigger를 쓰지 않으며 landing 시 `Move`를 요청하는지 고정한다.
2. `EnemyView_Jumping`은 먼저 raw asset 구조와 invalid timing을 기록하고 synthetic valid fixture로 trigger 최초 진입/state resync 메커니즘을 증명한다. asset repair/override 제거 결정 전에는 현재 prefab을 정상 visual contract라고 주장하지 않는다.

이 결과로 sparse entry의 실제 표현력과 비-production migration 경계를 확정한다. 다음 Slice는 prefab을 건드리지 않는 호환 runtime/schema 추가다.

`EnemyAnimationTimingAuthoring`의 asset 참조가 0이 된 뒤에는 compatibility-only type임을 문서와 Editor에서 명확히 하고 새 active authoring으로 다시 추가하지 못하게 한다. Add Component 메뉴 숨김이나 `[Obsolete]` 적용은 잔존 호출자/경고 영향을 확인한 뒤 별도 compatibility 단계로 수행하며, 이번 migration의 public source 삭제와 혼동하지 않는다.
