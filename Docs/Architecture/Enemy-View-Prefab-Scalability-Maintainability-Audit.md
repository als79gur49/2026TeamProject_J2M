# Enemy View Prefab 확장성·유지보수 감사

## 문서 상태

- 감사 기준일: 2026-09-01
- 후속 구현·검증일: 2026-09-02
- 범위: campaign-main Enemy View 프리팹, animation authoring/runtime dispatch, catalog binding, 관련 EditMode/PlayMode 테스트
- 성격: 초기 read-only 구조 감사와 2026-09-02 후속 구현·검증 기록
- 초기 감사 비범위였던 프리팹 migration과 런타임 변경은 아래 구현 상태 절에서 별도로 기록한다.

이 문서는 Enemy View 프리팹의 animation 인스펙터 노출 문제를 출발점으로,
확장성과 유지보수 관점에서 함께 발견된 문제를 기록한다. 최초 정적 소스/YAML 감사 결과와
후속 구현·Unity 검증 상태를 구분하며, 각 항목의 현재 상태는 `2026-09-02 구현 상태`를 우선한다.

아래 `확인된 문제`와 inventory는 최초 감사 당시의 기준점을 보존한다. 현재 해소 여부는 바로 다음
구현 상태가 우선하며, 아직 남은 Inspector 구조 개선 항목은 후속 작업으로 취급한다.

## 2026-09-02 구현 상태

이번 구현은 Inspector를 즉시 family별 CustomEditor로 재작성하기 전에, production 계약과 legacy
death lifecycle 결합을 먼저 정리한 단계다.

완료한 항목은 다음과 같다.

- production catalog의 exact 10 ID-to-prefab mapping과 uniqueness, root driver/Animator/Controller
  wiring, View별 active state의 runtime reachability와 Motion, required/optional parameter 타입,
  authored trigger의 `Trigger` 타입과 transition 소비, 명시적 disabled binding, fallback trigger가
  없는 direct-state View의 nonnegative cross-fade mode를 확인하는 production asset 계약과,
  muted/solo transition을 소비 증거에서 제외하는 실행 의미 계약, 10-View DeathMotion pose-freeze
  safety gate를 포함한 7개 `Full` 테스트를
  추가했다. cross-fade의 정확한 tuning 값은 고정하지 않는다.
- 강화된 gate가 검출한 transition 미소비 trigger 12개를 BlackEye 3개, RocketFace 3개,
  Astreton 2개, DrSaturn 1개, JPeter 3개 프리팹에서 비웠다. Animator Controller와 FBX는
  변경하지 않았다.
- controller parameter가 없거나 해당 View에서 지원하지 않는 추가 trigger slot 13개도
  BlackEye 2개, DrSaturn 2개, JPeter 2개, RocketFace 2개, Sunwheel 5개 프리팹에서 비웠다.
- Nebulous가 실제 controller에 없는 Hit/Death trigger를 선언하지 않도록 두 binding을 비웠다.
  FBX, animation clip, Animator Controller는 변경하지 않았다.
- production Enemy View에 남아 있던 `EntityEffectPresentationAuthoring`과 dormant death cleanup/tail
  runtime을 제거했다. Enemy 사망 후 View 수명은 animation tail이 아니라 typed exit/DeathMotion
  presentation 계약이 소유한다.
- `EnemyAnimationTimingAuthoring`에서 enemy 전용 `deathAnimatorDurationSeconds`와
  `deathReferenceClip`을 serialized schema, snapshot, prefab YAML에서 제거했다. Death cue와 실제
  trigger dispatch는 유지하며, enemy Death 단계는 기본 speed `1`, duration `0`을 반환한다.
- player의 동명 authoring과 `PlayerAnimatorDriver.DeathPresentationDurationSeconds`는 유지했다.
  Player는 generic visibility Remove를 사용하므로 해당 duration만 player View hide tail에 계속
  반영한다. Enemy animation duration은 이 경로에 참여하지 않는다.
- 마이그레이션 대상 production 8개와 prototype 1개의 raw YAML에서 제거 필드가 다시 생기지
  않도록 residue 테스트를 추가했다. 삭제한 `EntityEffectPresentationAuthoring` former script GUID도
  prefab/Scene/asset/meta에 다시 등장하지 않도록 역방향 residue gate를 추가했다.
- summoned View cleanup은 `FinalEntities` 부재만 보지 않고 실제 `AtContactTime`/`AfterEntityMotion`
  retention을 존중한다. resolver가 생성한 정확한 View instance를 소유하고, exit handoff가 완료된
  뒤 동일 instance의 state/registry mapping만 멱등 release한다. replacement View는 보존하고 animation
  cache를 해당 replacement driver로 다시 결합한다. 등록 callback 실패는 생성 View와 registry mapping을
  rollback한다. 정상 exit cleanup batch와 session reset은 개별 callback·cache 실패에도 모든 owned View의
  후속 cleanup을 시도하고, reset 중 재진입 View 생성은 거부한다. runtime teardown도 모든 단계를 시도하며
  성공한 teardown만 terminal 상태로 확정한다.
- production VFX extension과 summoned resolver를 함께 사용하는 계약 테스트로 `AtContactTime`
  DeathMotion source clone이 original View cleanup보다 먼저 생성되고 중복 재생되지 않음을 고정했다.
- 실제 BlackEye, DrSaturn, RocketFace, JPeter production 프리팹을 Host에 설치한 `Full` PlayMode
  characterization 7개를 추가했다. BlackEye, RocketFace, JPeter는 fatal `AtContactTime` tick에서 typed Death cue를
  즉시 1회 받지만 original View 수명은 contact까지 별도로 유지되고, Animator는 우선순위가 앞선
  `Hit` transition으로 진입하며 destination Motion은 비어 있다. 이는 animation cue 시점과 View
  cleanup 시점이 서로 다른 현재 production 정책임을 기록한다.
- JPeter immediate death에서 확인한 clone Animator의 기본 `Idle` 재초기화는 DeathMotion source
  clone pose-freeze로 수정했다. active source의 descendant local TRS와 SkinnedMeshRenderer blendshape를
  값으로 캡처하고, inactive staging hierarchy에서 clone Animator를 비활성화한 뒤 값을 복원하고
  마지막에 clone을 활성화한다. JPeter의 visible `J` Transform을 controller 기본과 명확히 다른
  normalized `0.24` pose로 평가한 production PlayMode 계약은 Present 직후, 첫 post-frame, 이후
  12프레임 동안 pose가 유지되면서 상위 DeathMotion fly-away는 계속 진행됨을 고정한다. 이는
  Animator state/trigger나 빈 Death state를 이전하는 계약이 아니라 마지막 시각 pose를 동결하는
  `StrongContract`이며 controller/FBX/prefab은 변경하지 않는다.
- BlackEye와 DrSaturn의 ModelRoot 아래 `GameplayVfxAttachPoint`는 pose writer가 아닌 source-only marker다.
  DeathMotion safety gate는 이 marker를 허용하되 inactive clone에서 즉시 비활성화하고 제거를 예약한 뒤
  clone을 활성화하며, Player에서는 frame end에 제거를 완료한다. 실제 두 production prefab 계약은 marker가
  있어도 generic fallback이 아닌 source silhouette clone을 생성함을 고정한다.
  반면 알 수 없는 enabled `MonoBehaviour`는 계속 fail-closed한다.

이번 단계에서 의도적으로 남긴 호환성·후속 항목은 다음과 같다.

- `EnemyAnimatorDriver.PlayDeathPresentation`과
  `GameplayAnimationSyncCoordinator.BeginEnemyDeathPresentation`의 `float` 반환 signature는 기존
  호출 호환을 위한 `[Obsolete]` shim으로 유지한다. production typed path는 internal void
  `PlayDeathCue`를 사용하고 compatibility shim은 cue를 전달한 뒤 항상 `0`을 반환한다. exact public
  signature, `Obsolete.IsError == false`, cue 1회 전달과 zero duration을 reflection 계약으로 고정한다.
- public `EnemyAnimationTimingSnapshot`의 legacy constructor/death query, authoring death duration
  getter, driver death duration getter도 `[Obsolete]` compatibility shim으로 유지한다. serialized
  death field는 복원하지 않으며 snapshot/authoring shim은 disabled sentinel `-1`, driver shim은
  duration `0`을 반환한다.
- `EntityExitPresentationTiming.AfterAnimationTail` numeric value `3`은 `[Obsolete]` compatibility
  tombstone으로 유지하되 exit controller에서 명시적으로 `Immediate` cleanup으로 normalize한다.
  enum numeric compatibility를 깨는 삭제는 별도 migration으로 다룬다.
- family별 명시적 binding/조건부 Inspector와 거대 driver/coordinator 책임 분해는 아직 미착수다.
- DeathMotion clone은 Animator state/trigger를 전달하지 않고 clone 요청 시점의 마지막 시각 pose를
  동결한다. `AtContactTime`의 정확한 접촉 순간 pose 캡처와 optimized hierarchy 또는 procedural pose
  writer 지원은 이번 계약 범위가 아니며 별도 carrier/runtime 검증 후 확장한다.

검증 증거:

- production 10-View controller 계약 단독 검증
  (`./run_tests.sh full --filter EnemyViewAnimatorControllerContractTests`): 현재 EditMode 7/7,
  PlayMode matching 0. pose-freeze safety gate 추가 전 6/6 XML/log 사본은
  `/mnt/d/J2M/evidence/enemy-view-hardening-rereview/2026-09-02-final-MydYbY/controller/`에 보존되어 있다.
- death lifecycle, summoned View handoff, legacy tombstone, timing/residue 계약을 묶은 대상군 검증
  (`SummonedEnemyPresentationResolverTests`, `GameplayTimingOwnershipTests`,
  `GameplayVfxEnemyDeathMotionPrefabWithSourceCloneTests`, `EnemyViewPresentationAuthoringTests`와
  production controller fixture 및 관련 4개 단일 테스트 filter)의 pose-freeze 보완 전 결과는
  EditMode 151/151, PlayMode matching 0이었다. XML/log 사본은
  `/mnt/d/J2M/evidence/enemy-view-hardening-rereview/2026-09-02-final-MydYbY/touched-cluster/`에 보존했다.
- former script GUID를 포함한 Enemy View authoring residue 단독 검증
  (`./run_tests.sh full --filter EnemyViewPresentationAuthoringTests`): EditMode 10/10,
  PlayMode matching 0
- actual production prefab Animator characterization
  (`./run_tests.sh full --filter EnemyViewAnimatorRuntimeCharacterizationPlayModeTests`):
  현재 EditMode matching 0, PlayMode 7/7. BlackEye/RocketFace/JPeter의 empty Hit transition,
  JPeter pose-freeze, BlackEye/DrSaturn passive attach-point source clone을 검증한다. 초기 4/4 characterization과
  후속 core XML/log는 `/mnt/d/J2M/evidence/enemy-view-animator-runtime-characterization/2026-09-02-032137/`에
  보존되어 있다.
- 현재 `./run_tests.sh core`: EditMode 217/217, PlayMode 111 total / 0 failed.
  이전 107 passed / 4 skipped XML/log 사본은
  `/mnt/d/J2M/evidence/enemy-view-hardening-rereview/2026-09-02-final-MydYbY/core/`에 보존되어 있다.

filtered `full` 실행은 지정한 fixture/test 대상군만 검증한 결과이며 broad full lane은 실행하지 않았다.

## 핵심 결론

> 이 절부터 `권장 작업 순서와 gate` 전까지는 최초 감사 당시의 구조와 inventory를 보존한
> historical snapshot이다. 제거·정정된 항목의 현재 상태는 위 구현 상태가 우선한다.

문제의 직접 소유자는 `EnemyAiProfile`이 아니다. AI Profile은 행동 규칙을 compile하여 runtime
definition으로 전달하고, View 프리팹은 `PresentationId`를 통해 독립적으로 선택된다. 최초 감사
당시 넓은 인스펙터 표면은 주로 다음 두 View-side 컴포넌트에서 발생했다.

- `EnemyAnimatorDriver`: 17개 serialized field
- `EnemyAnimationTimingAuthoring`: 11개 serialized field

두 컴포넌트에는 capability에 따른 조건부 노출을 제공하는 `CustomEditor` 또는
`PropertyDrawer`가 없다. 따라서 단순 이동형, 점프형, 차지형, 글라이드형 여부와 관계없이 한
프리팹이 지원하지 않는 animation name, trigger, clip, timing까지 같은 인스펙터에 노출된다.

이것은 단순한 인스펙터 미관 문제가 아니다. 현재 구조에서는 빈 값과 `-1` sentinel이
"미지원", "자동 탐색", "fallback 사용", "직접 CrossFade 사용"을 동시에 암시한다. 그 결과
잘못된 값이 authoring 시점에 차단되지 않고, 프리팹과 Animator Controller의 계약 불일치가
런타임까지 지연될 수 있다.

## 소유권과 계약 분류

### StrongContract

아래 경계는 이번 개선에서도 유지해야 한다.

| 계약 | 의미 |
| --- | --- |
| AI와 View의 분리 | `EnemyAiProfile -> Compiler -> RuntimeDefinition`은 행동 소유권이며, animation과 prefab은 presentation 소유권이다. |
| presentation 비권위성 | Animator, VFX, audio, View lifecycle은 authoritative simulation을 변경하지 않는다. |
| typed presentation 입력 | View는 tick 결과에서 만들어진 presentation carrier/signal을 소비한다. |
| root driver 연결점 | catalog에 등록된 Enemy View는 root `EnemyAnimatorDriver`를 통해 공통 presentation 연결점을 제공한다. |

따라서 animation 필드를 AI Profile로 이동하거나 AI capability가 View를 직접 제어하도록 만드는
방향은 해결책이 아니다. 필요한 개선은 View-side 계약을 명시적으로 만들고 검증하는 것이다.

### CurrentPolicy

아래 항목은 현재 구현 방식일 뿐이며, 호환성 계획과 테스트를 갖추면 변경할 수 있다.

- 하나의 `EnemyAnimatorDriver`가 모든 animation family를 소유하는 구조
- 17개 driver field와 11개 timing field의 정확한 serialized shape
- child hierarchy에서 첫 번째 `Animator`를 자동 선택하는 정책
- 빈 문자열과 `-1`에 의존하는 지원 여부 및 dispatch mode 판정
- Attack, Jump, Charge, Glide가 일부 공용 timing slot을 공유하는 정책
- `GameplayAnimationSyncCoordinator` 한 클래스가 player/enemy animation과 여러 lifecycle을 함께 조정하는 구조

## production prefab inventory

campaign-main catalog의 production Enemy View 10개를 기준으로 조사했다. GUID와 fileID는 모두
해결되었고, stage에서 사용하는 9개 presentation ID도 catalog에 존재한다. `Kali`는 일반 stage
binding이 아니라 summon archetype을 통해 도달한다.

| View | Animator 참조 | Timing authoring | 주요 관찰 |
| --- | --- | --- | --- |
| Astreton | 명시적 | Jump/Death 중심 | 명시적 참조를 사용한다. |
| BlackEye | 명시적 | Windup/Recover/Death | 명시적 참조를 사용한다. |
| DrSaturn | 명시적 | Windup/Recover/Death | 명시적 참조를 사용한다. |
| JPeter | 자동 탐색 | 모든 값 기본값 | timing component가 실질적으로 no-op이다. |
| Kali | 자동 탐색 | 없음 | summon-only 도달 경로를 가진다. |
| Nebulous | 자동 탐색 | Windup/Recover/Death | controller와 Hit/Death trigger 계약이 맞지 않는다. |
| RocketFace | 자동 탐색 | Windup/Recover/Death | 별도 motion authoring에도 no-op 값이 남아 있다. |
| SecBot | 자동 탐색 | 없음 | death view tail을 사용한다. |
| Startis | 명시적 | Death 중심 | 명시적 참조를 사용한다. |
| Sunwheel | 명시적 | Death 중심 | 미사용 animation family 필드도 함께 직렬화된다. |

정상 catalog 연결과 실제 controller 계약 검증은 별개의 문제다. 현재 catalog가 올바른 prefab을
가리킨다는 사실만으로 state/parameter 이름과 타입까지 안전하다고 볼 수 없다.

정적 inventory에서 함께 확인된 정상 범위는 다음과 같다.

- 감사한 12개 production/prototype prefab의 외부 asset GUID는 모두 해결되었다.
- 일반 stage가 사용하는 9개 Enemy presentation ID는 production catalog에 모두 존재한다.
- summon-only Kali는 `PassiveContactMinion` archetype catalog를 통해 도달 가능하다.
- non-Kali production View 9개의 Enemy Audio profile/binding은 대응하며, Kali는 summon-only 예외다.

이 항목들은 asset reachability의 양성 증거이며 animation controller 계약의 양성 증거는 아니다.

## 확인된 문제

### F1. Nebulous의 Hit/Death trigger 계약 불일치

상태: **정적 wiring 불일치 확인**, 실제 Unity warning과 시각 결과는 실행 검증 필요

`EnemyView_Nebulous.prefab`은 다음 값을 직렬화한다.

- windup state: `Fly_Start`
- recovery state: `Fly_Done`
- hit trigger: `Hit`
- death trigger: `Death`

연결된 `EnemyAnimator_Glider.controller`에는 `Fly_Loop`, `Idle`, `Fly_Done`, `Fly_Start`,
`Move` state가 있지만 Animator parameter 목록은 비어 있다. `EnemyAnimatorDriver.SetTrigger`는
Animator와 문자열 유무만 확인하며, parameter 존재 여부나 타입을 검증하지 않는다.

따라서 현재 자산에는 Hit/Death signal과 controller가 일치하지 않는 확정된 authoring 결함이
있다. 다만 의도된 수정이 "Hit/Death animation 추가"인지 "지원하지 않는 trigger 값 제거"인지는
제품 동작 결정을 거쳐야 한다.

### F2. 프리팹 검증이 연결 존재만 확인한다

상태: **확인됨**

`EnemyViewPrefabRequirements`는 root `EnemyAnimatorDriver`와 선택적 timing 값 범위를 확인하지만
다음을 검증하지 않는다.

- `Animator` 및 Runtime Animator Controller의 존재
- state name이 실제 controller state와 일치하는지
- trigger parameter의 존재와 `Trigger` 타입
- 명시적 `Animator` 참조가 같은 View hierarchy에 속하는지
- reference clip이 실제 재생 state와 대응하는지
- direct CrossFade 대상 state가 유효한지

또한 `ResolveAnimatorStateHash`는 state를 찾지 못해도 short-name hash로 fallback할 수 있고,
named CrossFade 경로는 호출 성공으로 처리되어 trigger fallback을 억제할 수 있다. 즉, 오타나
controller drift를 현재 validation gate가 조기에 차단하지 못한다.

### F3. timing 값이 dispatch 전략까지 암묵적으로 바꾼다

상태: **확인됨**

`stateTransitionCrossFadeDurationSeconds >= 0` 설정은 단순한 전환 시간 override가 아니다.
현재 driver에서는 이 값의 존재가 named-state CrossFade 우선 경로를 활성화한다. 따라서
인스펙터상 timing처럼 보이는 필드가 trigger fallback과 direct state dispatch 중 하나를 고르는
mode switch 역할도 한다.

이 결합은 다음 문제를 만든다.

- author가 값의 실제 의미를 인스펙터에서 알기 어렵다.
- controller가 trigger-driven인지 state-name-driven인지 명시되지 않는다.
- timing을 조정하는 변경이 dispatch 동작을 바꿀 수 있다.

### F4. 공용 phase/timing slot이 capability 조합을 제한한다

상태: **확인됨**

`EnemyAnimationTimingAuthoring`은 AttackWindup, JumpWindup, JumpAirborne, Recover, Death 쌍만
제공한다. 반면 `EnemyAnimatorDriver.ResolvePresentationPhase`는 generic Attack, Charge,
Glide, GravityField utility의 여러 신호를 Windup 또는 Recovery로 접는다. `ChargeActive`에는
별도 timing override가 없다.

현재 prefab 조합에서는 동작할 수 있으나, 하나의 View가 여러 capability를 재사용하거나 신규
animation family가 추가되면 서로 다른 의미가 같은 timing slot을 경쟁한다. 신규 family 추가가
field, phase enum, snapshot, dispatch branch, cache/reset, 테스트와 prefab YAML 전반을 건드리게
되는 구조다.

### F5. 핵심 클래스의 책임이 과밀하다

상태: **확인됨**

`EnemyAnimatorDriver`는 약 1,300줄 규모로 다음 책임을 함께 가진다.

- serialized authoring과 Animator 자동 탐색
- parameter/trigger/named-state dispatch
- timing snapshot과 global Animator speed 제어
- jump topology suspend/restore
- charge, glide, hit, death, visibility 처리
- 진단 counter와 runtime state

`GameplayAnimationSyncCoordinator`도 약 1,800줄 규모이며 player/enemy mapper와 cache,
summon pulse, utility timing, topology, death delay, presentation hold를 함께 조정한다.

이 구조에서는 작은 animation family 확장도 여러 책임 영역을 동시에 변경하게 된다. 바로
분해하는 것보다 먼저 controller 계약과 lifecycle 테스트를 고정해야 하지만, 장기적으로는
Common/Action/Jump/Charge/Glide 단위 adapter 또는 binding 분리가 필요하다.

### F6. Animator 자동 탐색이 hierarchy 변경에 취약하다

상태: **확인됨**

production View 10개 중 5개만 `Animator`를 명시적으로 참조한다. 나머지 JPeter, Kali,
Nebulous, RocketFace, SecBot은 `GetComponentInChildren<Animator>()`의 첫 결과에 의존한다.

이 정책은 prefab variant나 VFX child에 Animator가 추가되었을 때 대상이 조용히 바뀔 수 있다.
자동 탐색을 유지한다면 단일 후보만 허용하는 검증이 필요하고, 그렇지 않다면 명시적 binding을
production authoring 규칙으로 정해야 한다.

### F7. no-op 또는 소비되지 않는 authoring이 남아 있다

상태: **정적 소비처 감사에서 확인**, 제거 여부는 별도 결정 필요

- JPeter의 `EnemyAnimationTimingAuthoring`은 모든 값이 기본값이라 snapshot에 영향을 주지 않는다.
- RocketFace의 `EntityMotionPresentationAuthoring`은 Move/Push/Flip 값이 모두 `-1`이라 no-op이다.
- production View 9개에 `EntityEffectPresentationAuthoring`이 있었으나, 실제 비기본 사용은 Kali와 SecBot의 `deathViewTailSeconds: 0.2`뿐이었다.
- production source 검색에서 `deathViewTailSeconds` 외 `hitEffectDurationSeconds`, `deathEffectDurationSeconds`, `deathOwnershipMode`, `deathAnchorName`의 활성 소비처를 찾지 못했다.
- `EnemyUtilityWindupPresentationAuthoring`은 production prefab/runtime 소비처 없이 테스트 또는 잔존 코드로 남아 있다.

직렬화된 필드는 단순히 숨기는 것만으로 없어지지 않는다. 제거 또는 구조 변경 시에는
`FormerlySerializedAs`, migration callback, asset migration script/검증 중 필요한 전략을 먼저
정해야 한다.

### F8. Inspector shape를 고정한 테스트가 개선을 방해한다

상태: **확인됨**

`RuntimeBoardBoundsGuardTests.EnemyAnimatorDriver_InspectorSurface_IsLimitedToCoreAuthoringFields`
는 reflection으로 serialized field의 정확한 목록을 고정한다. 이 테스트는 author가 무엇을 보게
되는지, capability별로 필드가 적절히 숨겨지는지, controller 계약이 유효한지는 검증하지 않는다.

관련 테스트 부채도 함께 존재한다.

- `EnemyPrefabScaffoldTests`는 YAML 문자열, fileID, GUID, controller block에 강하게 결합된다.
- `GameplayTimingOwnershipTests`는 11개 timing field를 reflection 문자열로 설정한다.
- `EnemyPresentationReadinessPlayModeTests`는 synthetic object와 제한된 controller만 사용하여 production 10개 View의 계약을 확인하지 않는다.
- timing authoring에는 `OnValidate` 피드백이 없어 오류가 catalog validation 또는 runtime까지 지연된다.

인스펙터 개선 시 기존 exact-field 테스트를 단순 갱신하는 것만으로는 부족하다. authoring UX,
serialization compatibility, controller contract를 각각 검증하는 테스트로 책임을 나눠야 한다.

### F9. View가 지원하는 animation capability가 명시되지 않는다

상태: **확인됨**

현재 serialized field의 존재는 해당 View가 animation family를 실제 지원한다는 뜻이 아니다.
예를 들어 Sunwheel의 controller는 Hit, Death, IsMoving, EnemyAiMode 중심의 제한된 parameter만
제공하지만 prefab에는 Windup, Jump, Attack, Recover, Charge, Glide 관련 기본 필드도 같은
driver shape로 직렬화된다.

현재 AI Profile과 View 조합이 해당 signal을 만들지 않으면 문제가 드러나지 않는다. 그러나
Profile과 PresentationId가 독립적으로 선택되는 strong contract 때문에, 향후 View 재사용이나
신규 stage binding에서 잠재 계약이 노출될 수 있다. Profile capability에서 암묵적으로 추론하지
말고 View 자체가 지원 family와 미지원 signal 정책을 선언해야 한다.

### F10. 일반 catalog와 summon archetype catalog의 검증 범위가 다르다

상태: **확인됨**

`EnemyPresentationCatalogResolver`는 catalog 전체의 ID, null, duplicate를 먼저 검사하지만,
`EnemyViewPrefabRequirements`는 실제 stage binding에서 해석된 entry에만 적용한다. 따라서
catalog에는 있으나 현재 stage에서 사용하지 않는 잘못된 View가 검증을 피할 수 있고, 여러 entity가
같은 presentation을 사용하면 같은 prefab validation이 반복된다.

반면 `EnemyPresentationArchetypeCatalogResolver`는 catalog entry별
`ValidateConfiguration`을 통해 전체 archetype을 검증한다. 두 경로의 정책 차이는 새 View가
어느 catalog로 유입되는지에 따라 readiness 보장이 달라지는 유지보수 위험이다.

### F11. serialized schema 변경 안전장치가 없다

상태: **확인됨**

`EnemyAnimatorDriver`와 `EnemyAnimationTimingAuthoring`에는 현재
`FormerlySerializedAs`, schema version, migration callback이 없다. field rename, family별
컴포넌트 분리, sentinel 제거를 바로 수행하면 기존 prefab override가 유실되거나 기본값으로
되돌아갈 수 있다.

따라서 조건부 Inspector는 먼저 기존 field를 그대로 그리는 방식으로 도입할 수 있지만,
serialized shape 자체를 바꾸는 작업은 별도 migration slice와 before/after YAML residue 검증을
필요로 한다.

## 추가 검증이 필요한 위험

아래 항목은 구조상 위험이 있으나 현재 감사만으로 runtime 결함이라고 단정하지 않는다.

### Summoned View의 death/contact hold 수명

`SummonedEnemyPresentationResolver.CleanupOwnedViews`는 final entity가 사라진 owned summoned View를
정리한다. coordinator는 exit presentation ownership을 넘긴 뒤 cleanup을 호출하지만, exit
controller가 contact/death hold에 필요한 상태를 별도로 얼마나 보존하는지 production 조합으로
확인해야 한다. 현재 테스트는 단순 제거를 다루며 summoned death/contact hold를 직접 고정하지
않는다.

필요 gate는 "summoned enemy가 사망하거나 contact 제거된 tick에서 마지막 animation/VFX hold가
완료되기 전에 View가 파괴되지 않는다"는 targeted PlayMode/EditMode characterization이다.

### Cache 무효화와 동적 driver 발견

- timing snapshot은 최초 해석 뒤 authoring 변경을 다시 읽지 않는다.
- animation sync의 driver cache는 release 호출이 누락되면 살아 있는 이전 driver를 재사용할 여지가 있다.
- semantic driver discovery는 같은 View에 나중에 추가된 child driver를 자동 재탐색하지 않는다.

현재 정상 lifecycle이 이 경로를 막을 수 있으므로, 실제 결함 판정 전에 View 교체·pooling·동적
component 추가 시나리오의 targeted test가 필요하다.

## 비-production 자산

- `EnemyView_Jumping.prefab`은 production catalog에서 참조되지 않는다. null clip과 비기본 jump timing을 포함하며, 현재 요구사항으로 등록하면 validation 실패 가능성이 있다.
- `EnemyView_PrototypeGravityFieldChaser.prefab`은 non-catalog prototype으로 문서화되어 있으며 Jumping과 큰 YAML 구조를 공유한다.

두 자산은 즉시 삭제 대상으로 분류하지 않는다. 샘플, prototype, 향후 자산 중 어떤 역할인지
inventory 결정을 내린 뒤 archive, migrate, delete 또는 validation 제외 정책을 명시해야 한다.

## 권장 작업 순서와 gate

### 1. production controller 계약을 테스트로 고정

production 10개 View를 순회하여 다음을 검사하는 Editor test를 먼저 추가한다.

- catalog prefab과 root driver 존재
- Animator 참조가 정확히 하나로 결정됨
- controller 존재
- authored state name 존재
- authored parameter name 존재 및 타입 일치
- 지원하지 않는 family의 field는 비어 있거나 명시적 disabled binding임

이 gate가 Nebulous 불일치를 먼저 실패로 보여야 한다.

### 2. Nebulous 의도를 결정하고 최소 수정

Hit/Death를 지원해야 한다면 controller state/parameter와 transition을 제공한다. 지원하지 않는다면
prefab의 해당 trigger authoring을 제거하고, signal 무시가 의도임을 binding/검증에 표현한다.

### 3. summon lifecycle characterization 추가

death/contact hold가 cleanup보다 먼저 끝나는지 targeted test로 확인한다. 실패할 때만 lifecycle
ownership을 수정한다.

### 4. no-op/dead field inventory를 닫는다

각 field와 component를 `active`, `reserved with owner`, `migrate`, `remove` 중 하나로 분류한다.
reserved 상태라면 예상 소비자와 활성화 gate를 문서화한다.

### 5. 명시적 binding과 조건부 Inspector 도입

권장 목표는 capability를 추론하는 거대한 inspector가 아니라, View가 실제 지원하는 animation
family와 dispatch mode를 명시하는 작은 binding이다.

- Common: locomotion, hit, death
- Action: windup, attack, recover
- Jump: windup, airborne, land/recover
- Charge: windup, active, recover
- Glide/Utility: 실제 지원하는 경우에만 별도 binding

`CustomEditor`는 선택된 binding만 노출하고 controller 검증 결과를 즉시 보여야 한다. 기존
serialized data를 유지할 migration 경로와 rollback을 함께 설계한다.

### 6. runtime 책임 분해

계약 테스트와 migration이 안정된 뒤 driver를 family adapter로 분리하고 coordinator가 concrete
field를 아는 범위를 줄인다. 이 단계는 인스펙터 숨김과 별개이며, 먼저 수행하면 기존 암묵 계약을
잃을 위험이 있다.

## 완료 조건

후속 구현은 최소한 아래 증거를 갖춰야 한다.

- production 10-view controller/state/parameter contract test 통과
- Nebulous Hit/Death 의도와 결과 기록
- capability별 Inspector screenshot 또는 Editor test evidence
- 기존 prefab serialized data migration 및 residue scan
- summoned death/contact lifecycle targeted test 결과
- `./run_tests.sh core` 결과와, prefab/Editor 변경에 필요한 Unity Editor 검증 결과
- 실행하지 않은 lane과 현재 baseline failure의 명시적 분리

## 감사 근거

주요 소스:

- `Assets/_Features/Gameplay/Gameplay_EnemyPresentation/Runtime/EnemyAnimatorDriver.cs`
- `Assets/_Features/Gameplay/Gameplay_EnemyPresentation/Runtime/EnemyAnimationTimingAuthoring.cs`
- `Assets/_Features/Gameplay/Gameplay_EnemyPresentation/Runtime/EnemyViewPrefabRequirements.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayAnimationSyncCoordinator.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/SummonedEnemyPresentationResolver.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayMotionTimingResolver.cs`
- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyAnimator_Glider.controller`
- `Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Enemy/Prefabs`

주요 테스트:

- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/RuntimeBoardBoundsGuardTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/EnemyPrefabScaffoldTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/GameplayTimingOwnershipTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/GameplayTickPresentationCoordinatorTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/PlayMode/EnemyPresentationReadinessPlayModeTests.cs`

최초 감사 당시에는 Unity test lane을 실행하지 않았다. 해당 최초 결론은 repository source/YAML의
정적 감사와 production catalog reachability 조사에 한정하며, 후속 실행 증거는 문서 상단 구현 상태에 기록한다.
