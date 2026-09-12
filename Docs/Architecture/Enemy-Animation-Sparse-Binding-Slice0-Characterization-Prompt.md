# Enemy Animation Sparse Binding Slice 0 실행 프롬프트

> Active execution prompt. 이 문서는
> [Enemy Animation Sparse Binding 구체 구현 계획](./Enemy-Animation-Sparse-Binding-Implementation-Plan.md)의
> Slice 0만 실행하기 위한 입력이다. Sparse binding runtime/schema 또는 prefab migration을 시작하는
> 프롬프트가 아니다.

아래 본문을 새 작업 세션에 그대로 전달할 수 있다.

## 실행 프롬프트

Enemy Animation Sparse Binding 계획의 **Slice 0 — 현행 Characterization**을 수행하라.

### Objective

production animation 구조를 변경하기 전에 현재 `EnemyAnimatorDriver`, timing authoring,
Utility playback track과 실제 Enemy View prefab/Animator Controller의 결합을 테스트로 고정한다.

이번 Slice의 결과는 다음 Slice에서 sparse cue binding을 구현할 때 지켜야 할 회귀 기준점이다.
따라서 새 설계를 미리 구현하거나 테스트 기대값을 계획의 추정에 맞추지 말고, 현재 runtime과 asset에서
실제로 관찰되는 동작만 기록해야 한다.

Slice 0 완료는 “모든 animation 문제가 해결됨”을 뜻하지 않는다. 아래 characterization과 검증이
같은 revision에서 통과하고, 불확실한 asset 의미가 명시적으로 남았음을 뜻한다.

### 먼저 읽을 문서

작업 시작 전에 다음 문서를 완전히 읽는다.

1. `AGENTS.md`
2. `Docs/Architecture/README.md`
3. `Docs/Architecture/Enemy-View-Prefab-Scalability-Maintainability-Audit.md`
4. `Docs/Architecture/Enemy-Animation-Sparse-Binding-Implementation-Plan.md`
5. `Docs/Testing/Gameplay-Test-Automation-Guide.md`
6. `AI_GIT_COMMIT_RULES.md`

구체 구현 계획은 future target이며 현재 runtime truth가 아니다. 현행 동작 판단은 source, production
prefab/Controller/FBX, existing tests의 관찰 결과를 우선한다.

### 시작 규칙

1. `git status --short --branch`와 `git diff --stat`을 실행한다.
2. 현재 diff를 읽고 사용자 변경을 보존한다.
3. 이 작업과 겹치는 기존 수정이 있으면 임의로 되돌리거나 덮어쓰지 않는다.
4. repository 검색은 `rg`/`rg --files`를 우선 사용한다.
5. 테스트 변경 전에 production 10-view와 Driver/Timing GUID prefab inventory가 계획서의 현재 baseline과
   일치하는지 read-only로 재확인한다.
6. commit/push는 별도 요청이 없으면 수행하지 않는다.

### 계약 분류

#### StrongContract

이번 Slice에서 다음 동작을 테스트로 고정한다.

- production catalog의 exact 10-view identity와 View prefab/Animator Controller 결합
- animation presentation이 `EnemyAiProfile`에서 추론되지 않는 경계
- presentation이 authoritative gameplay state를 변경하지 않는 경계
- 기존 signal counter, 같은 tick의 관찰 가능한 semantic 처리 순서/결과, 중복 억제
- timing 계산과 one-shot dispatch가 독립적으로 공존하는 동작
- state-mode active phase resync가 signal count를 증가시키지 않는 동작
- active `SyncRuntimeState`의 playback suppression이 계산된 speed/duration은 유지하고 실제 `Animator.speed`만 `0`으로 만드는 동작
- Utility timing이 일반 state resolver가 아니라 Utility playback track에 의해 적용되고 종료 후 복원되는 동작
- Death의 계산된 presentation duration `0`과 speed `1`; 실제 Animator speed는 unsuppressed일 때 `1`, suppressed일 때 `0`
- Jump pose/state/normalized-time 보존과 DeathMotion pose capture 및 View lifetime 소유권

#### CurrentPolicy

다음은 관찰 대상이지만 이 Slice에서 새 구조로 고정하거나 개선하지 않는다.

- state/trigger 문자열이 Driver의 private serialized field에 있는 현재 schema
- Timing Authoring의 flat serialized field
- crossfade override 존재 여부가 Trigger/State branch를 선택하는 현재 구현
- 테스트가 legacy private field를 reflection/SerializedObject로 구성하는 방식

#### Migration parity baseline

다음은 canonical architecture에 영구 고정하는 Strong Contract가 아니라 sparse migration 동안 before/after
동등성을 보장할 baseline이다.

- pending state slot 한 개와 last-write-wins
- queue 시점의 `LastCrossFadedStateName` 갱신
- production prefab의 현재 exact timing/crossfade 값
- global crossfade override가 Trigger/State branch를 고르는 현재 방식
- Utility playback track의 현재 내부 적용/복원 방식

### 변경 허용 범위

우선 다음 기존 테스트 파일을 검토한다.

- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/EnemyViewAnimatorControllerContractTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/GameplayTimingOwnershipTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/GameplayTickPresentationCoordinatorTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/PlayMode/EnemyViewAnimatorRuntimeCharacterizationPlayModeTests.cs`

신규 asset/reflection 구조 검증은 `EditMode/TestSupport/Infrastructure`, EditMode runtime scenario는
`EditMode/Scenario`, 실제 Animator/prefab 실행은 기존 PlayMode Integration 위치에 둔다. 기존 `Unit`
fixture에 서로 다른 test layer를 계속 누적하지 않는다. 이 디렉터리들은 서로 다른 asmdef일 수 있으므로
각 placement assembly의 책임과 reference를 먼저 확인하고 해당 위치에 새 characterization fixture/helper를
추가한다. 직접 사용하는 runtime/test-support 타입의 reference가 없다면 production asmdef가 아니라 필요한
최소 test-only asmdef reference 변경만 허용하며 이유를 보고한다. 새 Unity asset을 만들면 `.meta`를 함께
다루되 가능하면 기존 test-local runtime controller/clip 생성 helper를 재사용한다.

테스트 seam이 없어 production code 변경이 필요해 보이면 즉시 구현하지 않는다. 먼저 기존 public/internal
관찰값, Animator state/parameter, signal counter, AssetDatabase/SerializedObject로 검증할 수 있는지
끝까지 확인하고, 그래도 불가능하면 필요한 최소 seam과 이유를 blocker로 보고한다.

### 절대 변경하지 않을 범위

- `EnemyAnimatorDriver`, `EnemyAnimationTimingAuthoring`, `GameplayAnimationSyncCoordinator` production 구현
- production 또는 prototype Enemy View prefab YAML
- Animator Controller, AnimatorOverrideController, FBX, AnimationClip
- Scene, Stage catalog/profile/authoring asset
- Death logic, View exit timing, DeathMotion VFX pose capture
- sparse cue enum/binding/catalog/Inspector/migration tool
- legacy serialized field/API 제거 또는 rename
- persistent prefab asset 대상 `SerializedObject.ApplyModifiedProperties`, `SaveAssets`, `SaveAsPrefabAsset`

characterization이 계획의 가정과 다르게 나오더라도 production을 고쳐 테스트를 맞추지 않는다. 관찰된
차이와 재현 테스트를 보고하고 Slice 1을 Hold한다.

## Work Package A — Baseline inventory 재확인

read-only 검사로 다음을 기록한다.

- production catalog entry가 exact 10개인지
- production ID → prefab path가 현재 contract와 일치하는지
- `EnemyAnimatorDriver` script GUID를 참조하는 prefab 수와 경로
- `EnemyAnimationTimingAuthoring` script GUID를 참조하는 prefab 수와 경로
- 두 GUID의 Scene/`.asset` 직접 참조 여부
- non-production 4개 prefab의 현재 경로
- 각 대상의 Animator explicit reference, resolved child Animator, Controller/OverrideController identity

계획 baseline `Driver 14`, `Timing 12`, `production 10`, `non-production 4`, Scene/`.asset` 직접 참조 없음과
다르면 숫자를 억지로 맞추지 않는다. 실제 inventory와 차이를 보고하고 이후 asset-specific 기대값 추가를
Hold한다.

## Work Package B — Astreton 실제 dispatch 계약

실제 `EnemyView_Astreton.prefab`과 연결 Controller를 사용해 다음을 고정한다.

1. JumpWindup 시작은 `JumpWindup` state crossfade 경로를 사용한다.
2. JumpAirborne 시작은 `JumpAirborne` state crossfade 경로를 사용한다.
3. landing/complete는 `Move` state를 요청한다.
4. Animator가 구동 불가능한 순간의 state 요청은 pending되고 재활성화 시 한 번 적용된다.
5. pending state가 새 state 요청으로 교체되는 현재 last-write 동작을 보존한다.
6. jump signal counter는 실제 signal 횟수만 증가하며 resync/pending consume으로 다시 증가하지 않는다.

실제 prefab instance에서는 최종 Animator state, `LastCrossFadedStateName`, crossfade `0.001` migration
baseline, signal counter를 관찰한다. 별도의 synthetic trap Controller에서는 crossfade override가 있을 때
trigger fallback branch가 선택되지 않는 Driver 정책을 반증 가능하게 검증한다. Synthetic 결과를 실제
Astreton Controller에서 `SetTrigger`가 몇 번 호출됐다는 증거로 표현하지 않는다.

## Work Package C — `EnemyView_Jumping` 분리 Characterization

현재 prefab에는 duration override가 있으나 유효한 reference clip이 없어 정상 `Apply` 전에 validation
예외가 발생할 수 있다. 따라서 아래 두 검증을 분리한다.

### C1. 실제 asset의 invalid 상태

실제 prefab을 수정하지 않고 다음을 AssetDatabase/SerializedObject 및 validation 호출로 고정한다.

- jump 최초 진입용 trigger 이름
- sustained/resync용 airborne state 이름
- authored jump duration 값
- reference clip null 여부
- crossfade sentinel 값
- 이 조합이 현재 timing validation에서 어떤 exception/diagnostic으로 거절되는지

이 테스트의 성공은 “Jumping이 정상 재생된다”가 아니라 “현재 asset이 명시한 invalid 상태를 정확히
검출한다”는 뜻이다.

### C2. Synthetic valid fixture의 trigger + state 역할

실제 prefab을 repair하지 않고 test-local 유효한 Animator/Driver fixture를 구성한다.

- 최초 airborne signal에서 trigger branch가 선택되고 semantic signal counter가 한 번 증가한다.
- 지속/복원/resync에서는 별도 airborne state가 사용된다.
- resync는 signal counter를 증가시키지 않는다.
- trigger는 Animator 비활성 구간에 pending되지 않는다.
- state ensure/resync는 기존 pending/state 복원 규칙을 따른다.

C1과 C2를 합쳐 실제 Jumping이 `SetTrigger`를 정확히 한 번 호출하거나 정상 visual behavior를 보였다고
주장하지 않는다. 실제 prefab의
visual characterization은 asset 소유자가 repair/override 제거/archive 중 하나를 결정한 이후 단계다.

## Work Package D — DrSaturn Utility 계약

실제 `EnemyView_DrSaturn.prefab`과 production GravityFieldAura 연결을 사용해 다음을 고정한다.

- Utility windup은 `Windup` semantic signal을 만들고 synthetic trap Controller에서 trigger branch를 선택한다.
- Utility recovery는 `Recover` semantic signal을 만들고 synthetic trap Controller에서 trigger branch를 선택한다.
- migration 동등성 baseline에서 두 presentation duration은 각각 `0.5s`다.
- Utility timing은 logic phase를 직접 따라가는 일반 resolver가 아니라 Utility playback track이 소유한다.
- track이 끝나면 `RestorePresentationTiming()`을 통해 non-Utility baseline timing으로 돌아간다.
- actual Driver에 Utility state만 직접 `Apply`하면 일반 timing resolver가 Utility timing을 지속 소유하지 않는다.
- 같은 state를 Coordinator 경로로 적용하면 Utility track이 `0.5s`를 적용하고 종료 뒤 baseline을 복원한다.

DrSaturn을 Utility로 분류하는 근거는 production stage/profile 연결과 runtime carrier를 함께 사용한다.
Animator contract를 `EnemyAiProfile`만 보고 추론하지 않는다.

## Work Package E — Timing과 one-shot dispatch 독립성

test-local fixture로 다음 same-tick 조합을 고정한다.

- Recovery phase가 활성인 tick에 Hit signal이 함께 들어온다.
- Recovery cue가 Animator speed와 presentation duration을 결정한다.
- Hit semantic signal counter가 정확히 한 번 증가하고 synthetic trap Controller에서 trigger branch가 선택된다.
- Hit dispatch가 Recovery timing을 speed `1`/duration `0`으로 지우지 않는다.
- Death의 계산값은 speed `1`, duration `0`이며 실제 Animator speed는 suppression 여부에 따라 `1` 또는 `0`이다.

Death는 서로 다른 세 경로를 별도 test로 구분한다.

1. shared tick `Apply` + Death suppression mask: Death semantic signal/cue가 억제됨
2. unsuppressed shared tick `Apply`: 현행 Death 우선순위와 semantic signal 결과
3. typed death playback: Utility track을 먼저 제거한 뒤 `PlayDeathCue()` 실행

Timing과 dispatch를 하나의 “마지막 cue” assertion으로 합치지 않는다. 계산된 timing과 one-shot signal을
각각 검증한다.

## Work Package F — Resync와 playback suppression

기존 characterization을 유지하거나 보강해 다음 경로가 같은 계약을 따르는지 확인한다.

- ActionWindup, ActionRecovery
- JumpWindup, JumpAirborne
- ChargeWindup, ChargeActive, ChargeRecovery
- GlideWindup, GlideActive, GlideRecovery
- `SyncRuntimeState`
- `SyncHiddenRuntimeState`
- `ResyncAnimatorStateFromLastPresentation`
- `RestorePresentationTiming`
- Utility track timing 적용과 종료

검증할 공통 결과:

- resync는 state를 복원하지만 signal counter를 증가시키지 않는다.
- Trigger-only cue는 일반 resync 대상이 아니다.
- suppression 중 `CurrentAnimatorSpeed`와 `CurrentPresentationDurationSeconds`는 유지된다.
- active `SyncRuntimeState(... playbackSuppressed:true)`에서는 실제 `Animator.speed`만 `0`이다.
- `SyncHiddenRuntimeState`는 계산된 property와 Jump topology snapshot만 갱신하고 실제 Animator playback은
  변경하지 않는다.
- active suppression 해제 후 계산된 speed가 복원되고 one-shot signal이 재발행되지 않는다.

## Work Package G — Sunwheel unused crossfade

실제 `EnemyView_Sunwheel.prefab`에 남은 crossfade 값이 Hit/Death trigger를 state dispatch로 바꾸거나
임의 state command를 만들지 않는다는 현재 의미를 고정한다. 단순히 값이 존재한다는 것보다 실제로
state command가 발생하지 않고 Hit/Death semantic signal이 유지되는지를 검증한다. 실제 trigger branch
선택 여부는 동일 authoring의 synthetic trap Controller에서 별도로 확인한다.

## Production prefab 안전 규칙

- actual prefab은 AssetDatabase로 읽은 persistent object를 직접 구동하지 않고 반드시 instance clone을
  만들어 실행한다.
- Animator state 변경, Controller 대체, Driver `Apply`는 clone에만 수행한다.
- 모든 test는 `finally`에서 clone과 test-local Controller/clip을 `DestroyImmediate`한다.
- Jumping C1은 persistent asset의 read-only `SerializedObject` 조회와 side-effect 없는 validation 호출만
  허용한다.
- persistent asset에는 `ApplyModifiedProperties`, `SaveAssets`, `SaveAsPrefabAsset`을 호출하지 않는다.
- 테스트 전후 production prefab/Controller/FBX/Scene diff를 비교해 Slice 0이 새 asset 변경을 만들지
  않았음을 확인한다.

## 테스트 작성 규칙

- characterization test는 현재 구현에서 green이어야 한다.
- 유일한 invalid asset 검증은 “예외가 발생해야 green”인 명시적 negative contract다.
- 계획상 원하는 미래 동작을 현재 기대값으로 작성하지 않는다.
- exact prefab timing 값은 migration 전후 비교용 baseline으로만 명명한다. 장기 tuning lock처럼 표현하지 않는다.
- production Controller/parameter/state 검증은 `[Category("Full")]` Infrastructure test로 둔다.
- actual prefab Animator 실행은 Integration/PlayMode `[Category("Full")]`로 둔다.
- synthetic runtime behavior는 기존 fixture의 분류를 따르되 reflection을 사용하면 Core로 내리지 않는다.
- Core에는 private-field reflection, AssetDatabase, prefab/Controller 의존 테스트를 추가하지 않는다.
- 기존 broad test를 복제하지 말고 각 신규 test가 이번 Slice에서 추가로 고정하는 한 가지 계약을 이름에
  드러낸다.
- fragile한 frame/time sleep 대신 Animator update와 결정적 상태 전이를 사용한다.
- 테스트 때문에 production에 debug flag나 test-only public API를 추가하지 않는다.

## 서브 에이전트 검토

구현과 1차 검증 후 최소 두 개의 read-only 서브 에이전트 검토를 수행한다.

1. Runtime reviewer
   - timing/dispatch, pending, resync, suppression, Utility track 계약을 검토
   - 테스트가 implementation detail만 고정하거나 실제 회귀를 놓치는지 확인
2. Asset/test reviewer
   - production prefab/Controller/FBX 결합과 Jumping invalid asset 검증을 확인
   - production 10-view와 GUID inventory가 실제 자산에서 유도됐는지 확인

서브 에이전트는 파일을 수정하지 않는다. 지적을 그대로 적용하지 말고 source/asset 근거로 재검증한 뒤
필수 보완만 반영한다. 보완 후 targeted fixture를 다시 실행한다.

## Validation

각 fixture는 수정 직후 개별 실행하고 reviewer 보완 후 동일 code state에서 다시 실행한다.

```bash
./run_tests.sh full --filter EnemyViewAnimatorControllerContractTests
./run_tests.sh full --filter GameplayTimingOwnershipTests
./run_tests.sh full --filter GameplayTickPresentationCoordinatorTests
./run_tests.sh full --filter EnemyViewAnimatorRuntimeCharacterizationPlayModeTests
./run_tests.sh core
```

위 목록 외에 새 fixture를 추가했다면 모든 신규 fixture에 대해 각각 다음 명령을 실행한다.

```bash
./run_tests.sh full --filter <NewFixtureName>
```

`<NewFixtureName>`은 placeholder 그대로 실행하지 않고 실제 fixture 이름으로 치환한다. 신규 fixture마다
owning stage의 matching count가 `0`보다 큰지 확인하지 않으면 Slice 0 완료로 판정할 수 없다.

규칙:

- filter 전체 aggregate matching count가 `0`이면 evidence로 인정하지 않는다.
- 해당 fixture가 속해야 하는 owning stage의 matching count가 `0`이어도 인정하지 않는다.
- fixture가 속하지 않는 반대 stage의 matching `0`은 정상으로 기록한다.
- touched fixture의 EditMode/PlayMode 결과를 각각 기록한다.
- 실패가 기존 broad baseline인지 새 characterization failure인지 분리한다.
- broad unfiltered `full`은 이 Slice의 기본 요구가 아니다. 실행하지 않았으면 명시한다.
- `ui` lane은 runtime UI 변경이 없으므로 기본 요구가 아니다. UI 파일을 건드렸다면 실행하거나 미실행
  이유를 기록한다.
- Unity를 직접 호출하지 않고 현재 worktree의 `./run_tests.sh`만 사용한다.
- 새 evidence는 `/mnt/d/J2M/evidence/enemy-animation-sparse-binding-slice0/<timestamp-run-id>/` 아래에
  보존해 재실행이 이전 결과를 덮지 않게 한다.
- evidence에는 final HEAD, `git status --short`, diff hash, 수정한 test/source hash를 기록한다. dirty
  working tree에서는 HEAD만 같다고 동일 code state라고 주장하지 않는다.
- `git diff --check`를 마지막에 실행한다.

## 즉시 Hold하고 사용자 결정을 요청할 조건

- Astreton이 state crossfade와 trigger를 동시에 사용하거나 계획과 다른 dispatch를 보이는 경우
- Jumping의 raw asset과 Unity resolved 값이 달라 어떤 값이 의도인지 결정해야 하는 경우
- DrSaturn이 실제 production에서 GravityFieldAura가 아닌 다른 semantic으로 사용되는 경우
- `SetTrigger` 실제 호출 횟수/순서를 요구하여 production instrumentation 없이는 입증할 수 없는 경우
- 같은 tick semantic signal 순서나 Death 우선순위를 바꿔야만 테스트가 통과하는 경우
- characterization을 위해 production runtime seam 변경이 필요한 경우
- production prefab, Controller, FBX 또는 Scene 수정이 필요한 경우
- 사용자 변경과 동일한 test/code 구간이 충돌해 안전한 분리가 불가능한 경우
- inventory가 계획 baseline과 달라 migration 범위가 바뀌는 경우

Hold 상태에서는 production을 수정하거나 Slice 1을 시작하지 않는다. 관찰값, 재현 방법, 가능한 선택지와
각 영향만 보고한다.

## Slice 0 완료 조건

다음을 모두 만족해야 `Ready for Slice 1`로 판정한다.

- inventory 결과가 계획 baseline과 일치하고 migration baseline이 확정됨
- Astreton state/landing/pending은 실제 prefab instance로, state 선택 시 trigger fallback이 배제되는 정책은
  synthetic trap Controller로 분리해 고정됨
- Jumping actual asset invalid 계약과 synthetic trigger/state 역할이 분리되어 고정됨
- DrSaturn Utility windup/recovery/timing/restore 계약이 production 기반으로 고정됨
- same-tick Recovery+Hit와 Death 우선순위가 고정됨
- 일반 resync와 playback suppression 계약이 유지됨
- Sunwheel unused crossfade 의미가 고정됨
- targeted fixture에 matching test가 존재하고 모두 통과함
- 같은 code state의 `./run_tests.sh core`가 통과함. infrastructure/environment 또는 기존 문제로
  실행·통과하지 못해도 판정은 `Hold`
- Slice 0이 새로 만든 production code/prefab/Controller/FBX/Scene diff가 없음
- 시작 전 사용자 소유 diff와 종료 시 diff가 별도로 열거되어 보존됨
- 두 서브 에이전트의 필수 지적이 재검증·반영됨

하나라도 충족되지 않으면 `Hold`다. 일부 테스트가 통과했다는 이유만으로 Slice 1을 자동 시작하지 않는다.

## 최종 보고 형식

최종 답변에는 다음만 명확히 포함한다.

1. 판정: `Ready for Slice 1` 또는 `Hold`
2. 추가/수정한 characterization 계약
3. 실제로 관찰된 Astreton, Jumping, DrSaturn 결과
4. inventory 결과
5. 변경 파일
6. 실행한 명령과 test count/result
7. 실행하지 않은 lane과 이유
8. Slice 0이 production asset/code를 변경하지 않았다는 확인과 시작 전 사용자 소유 diff 목록
9. 남은 불확실성, blocker, 다음 단계

`project-wide green`, `full regression closed`, `all regressions fixed`, `full lane green`은 matching broad
lane이 실제로 통과하지 않았다면 사용하지 않는다.
