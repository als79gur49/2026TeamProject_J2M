> Archived historical document.
> This file is not part of the active truth-source chain. Start with [Docs/Architecture/README.md](../../Architecture/README.md).
> Archive index: [Docs/Archive/README.md](../README.md).

> Non-canonical historical blueprint.
> Canonical action runtime and presentation boundary are documented in [Tick-Simulation-Canonical-Spec.md](../../Architecture/Tick-Simulation-Canonical-Spec.md) and [Gameplay-Rules-Appendix.md](../../Architecture/Gameplay-Rules-Appendix.md).

# Player Action Wind-up Presentation Blueprint

## 1. 목적

이 문서는 현재 `Assets/_Features/Gameplay`의 결정론적 Tick 시뮬레이션 구조 위에서 플레이어의 `Idle / Walk / Push / Flip` 표현과 `Push / Flip` wind-up 규칙을 어떻게 분리해서 설계할지에 대한 최종 기준을 정의한다.

이 문서는 아래 문서의 상위 구조 원칙을 따른다.

- `Docs/Architecture/Hybrid-Architecture-Rulebook.md`
- `Docs/Architecture/Deterministic-Tick-Simulation-Blueprint.md`
- `Docs/Architecture/Cube-Surface-Gameplay-Blueprint.md`
- `Docs/Architecture/Cube-Surface-3D-Presentation-Blueprint.md`

핵심 목표는 다음 세 가지다.

- `Push / Flip`에 mandatory wind-up을 넣되, gameplay authority를 유지한다.
- `Logic -> Presentation Data -> View` 단방향 구조를 유지한다.
- `StartedThisTick` 같은 표현용 signal을 만들기 위해 불필요한 authoritative state를 늘리지 않는다.

## 2. 적용 대상

현재 코드베이스에서 이 문서와 직접 연결되는 핵심 지점은 다음과 같다.

- `Assets/_Features/Gameplay/Gameplay_PlayerControl/Runtime/PlayerControlState.cs`
- `Assets/_Features/Gameplay/Gameplay_PlayerControl/Runtime/PlayerControlStateLogic.cs`
- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/PlayerLogic.cs`
- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs`
- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPresentationData.cs`
- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickResultBuilder.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs`

정리하면:

- authoritative action 상태는 `PlayerControlState`가 가진다.
- action 시작과 진행은 `PreMovementState`가 갱신한다.
- 실제 `Push / Flip` 실행 intent는 `PlayerLogic`이 execute tick에만 생산한다.
- 최종 animation state 해석은 `GameplayTickViewPresenter`와 player animator driver가 담당한다.

## 3. 핵심 철학

- `Push / Flip`은 animation이 아니라 gameplay action이다.
- `Push / Flip`에는 반드시 wind-up이 존재한다.
- `Push / Flip` 실행 시점은 logic이 tick 단위로 결정한다.
- `Idle / Walk / Push / Flip`는 final animation state이며 authoritative state가 아니다.
- `Walk`는 입력 기반이 아니라 실제 이동 결과 기반이다.
- `Push` 접촉 누적 중이지만 아직 action이 시작되지 않았다면 표현은 `Idle`이다.
- `View`는 `TickResult`와 presentation data만 읽고, gameplay logic을 호출하지 않는다.

한 줄 요약:

> wind-up과 execute 시점은 logic이 결정하고, `Idle / Walk / Push / Flip`의 최종 표현은 presenter가 해석한다.

## 4. 용어 정리

이 문서에서는 아래 세 가지를 엄격히 구분한다.

### 4-1. authoritative state

`WorldState` 또는 그에 준하는 runtime-owned state에 저장되는 실제 gameplay truth다.

예:

- `PlayerControlState`
- `PlayerActionRuntimeState`
- cooldown
- wind-up
- recovery

### 4-2. snapshot

특정 phase 경계에서 읽기 위해 생성하는 일시적 read-only 복사본이다.

중요한 점:

- snapshot은 새로운 authoritative state가 아니다.
- snapshot은 phase boundary를 관찰하기 위한 읽기 도구다.

### 4-3. presentation signal

snapshot 비교 또는 phase 결과를 바탕으로 만든 render-only metadata다.

예:

- `StartedThisTick`
- `ExecutedThisTick`
- `CompletedThisTick`
- `FailedThisTick`

## 5. 최종 상태 레이어

플레이어 쪽 상태는 아래 두 레이어로 분리한다.

### 5-1. gameplay action layer

authoritative state다.

예:

- `None`
- `Push`
- `Flip`

권장 runtime state:

```csharp
public enum PlayerActionKind
{
    None = 0,
    Push = 1,
    Flip = 2,
}

public struct PlayerActionRuntimeState
{
    public PlayerActionKind kind;
    public int sequence;
    public Direction direction;
    public int targetEntityId;
    public int startTick;
    public int executeTick;
    public int recoveryEndTick;
    public bool executionAttempted;
}
```

### 5-2. final animation layer

presentation state다.

예:

- `Idle`
- `Walk`
- `Push`
- `Flip`

이 값은 `PlayerControlState`에 저장하지 않는다.
이 값은 `GameplayTickViewPresenter`가 action signal과 motion 결과를 합쳐 해석한다.

별도 규칙:

- `Death`는 `PlayerActionKind`가 아니라 player presentation override다.
- death 감지는 cleanup remove tick을 기준으로 한다.
- death가 참이면 `Push / Flip / Walk / Idle`보다 우선한다.

## 6. 최종 동작 규칙

### 6-1. Idle

아래 경우는 모두 `Idle`로 본다.

- 입력이 없다.
- 이동 입력은 있으나 실제 이동이 commit되지 않았다.
- push 가능한 박스에 접촉해 `pushContactTicks`를 누적 중이지만 threshold에 도달하지 않았다.
- flip 입력이 없고, active action도 없다.

즉, 입력 유지나 접촉 누적만으로는 `Walk`, `Push`, `Flip`가 되지 않는다.

### 6-2. Walk

`Walk`는 입력 state가 아니라 실제 이동 결과를 기준으로 판단한다.

- 이번 tick에 플레이어 move motion이 commit되었거나
- presenter가 active motion track을 보유한 경우

이 기준을 유지해야 blocked move에서도 `Walk`로 잘못 보이지 않는다.

### 6-3. Push / Flip

`Push / Flip`는 action이 시작된 후부터만 표시한다.

- action 시작 전 접촉 누적 단계: `Idle`
- action 시작 후 wind-up: `Push` 또는 `Flip`
- execute 후 recovery: `Push` 또는 `Flip`
- action 종료 후 move motion이 없으면 `Idle`
- execute tick에 `Flip` outcome이 `ImpactNoMove` 또는 `BlockedNoImpact`여도 `ExecutedThisTick = true`이며 recovery로 간다.
- pre-execute invalidation으로 action 자체가 사라진 경우에만 `CanceledThisTick = true`다.

### 6-4. Death

- death는 `PlayerActionKind.None / Push / Flip` 체계에 섞지 않는다.
- death tick에는 cleanup remove를 기반으로 `DidDie` presentation fact를 만든다.
- 최종 animation state 해석 우선순위는 `Death -> Push / Flip -> Walk -> Idle`이다.
- player view hide tail은 death clip이 끝나기 전에는 꺼지지 않아야 한다.

## 7. 현재 파이프라인의 시점 문제

현재 `TickPipeline`의 핵심 흐름은 아래와 같다.

```text
initialSnapshot
-> Enemy AI phase commit
-> snapshotAfterEnemyAi
-> PreMovementState phase commit
-> preMovementSnapshot
-> Movement phase
-> postMovementSnapshot
```

여기서 중요한 점은 현재 이름의 `preMovementSnapshot`이 실제로는

> "PreMovementState 이전 snapshot"

이 아니라

> "PreMovementState 적용 후, Movement 직전 snapshot"

이라는 점이다.

즉, `preMovementSnapshot`은 이미 action 시작 결과가 반영된 상태다.

## 8. 왜 `StartedThisTick`을 만들기 어려운가

예를 들어 tick 10에 `Push` action이 시작된다고 하자.

tick 9 종료 시:

```text
action.kind = None
```

tick 10의 `PreMovementState` 적용 후:

```text
action.kind = Push
action.startTick = 10
action.executeTick = 13
```

이 상태에서 `TickResultBuilder`가 `preMovementSnapshot`만 보면 아래 둘을 구분할 수 없다.

- tick 10: 이번 tick에 막 시작된 `Push`
- tick 11: 이전 tick부터 계속 진행 중인 `Push`

즉, `StartedThisTick`은 단일 상태 조회가 아니라 전이 감지다.

필요한 비교는 아래다.

```text
before: None
after: Push
```

따라서 `StartedThisTick`을 만들려면 "action 시작 전"과 "action 시작 후"를 둘 다 볼 수 있어야 한다.

## 9. 이 문제를 해결하는 두 가지 방법

### 9-1. 선택안 A: phase 이전 snapshot을 presentation build context에 추가

가장 작은 수정은 `snapshotAfterEnemyAi`를 `TickPresentationBuildContext`에 추가하는 것이다.

의미는 다음과 같다.

- `snapshotAfterEnemyAi`: `PreMovementState` 적용 전
- `preMovementSnapshot`: `PreMovementState` 적용 후

그러면 builder는 둘을 비교해 `StartedThisTick`을 계산할 수 있다.

장점:

- 구현이 직관적이다.
- 기존 builder 비교 패턴과 잘 맞는다.
- `TickResultBuilder`에서 signal을 직접 구성하기 쉽다.

단점:

- presentation을 위해 pipeline context에 snapshot 하나가 더 추가된다.
- naming이 명확하지 않으면 phase 경계가 더 헷갈릴 수 있다.

중요한 점:

- 이것은 새로운 authoritative state를 추가하는 것이 아니다.
- 이미 존재하는 phase boundary snapshot을 presentation까지 전달하는 것이다.

### 9-2. 선택안 B: `PreMovementStatePhaseResult`를 typed delta로 확장

더 구조적으로 응집도 높은 방법은 `PreMovementStatePhaseResult`에 문자열 로그뿐 아니라 typed action transition을 담는 것이다.

예시:

```csharp
internal readonly struct PlayerActionTransition
{
    public int EntityId { get; }
    public PlayerActionKind PreviousKind { get; }
    public PlayerActionKind CurrentKind { get; }
    public int PreviousSequence { get; }
    public int CurrentSequence { get; }
    public bool StartedThisTick { get; }
}
```

이 방식에서는 `PreMovementState` phase가 직접

- 시작
- 유지
- 취소
- 완료 후보

같은 action transition 의미를 계산하고,
`TickResultBuilder`는 그 typed result만 presentation data로 옮긴다.

장점:

- phase 의미가 phase 내부에 응집된다.
- builder가 phase 이전 snapshot까지 알 필요가 없다.
- "signal을 만들기 위해 snapshot을 더 퍼뜨린다"는 느낌이 줄어든다.

단점:

- `PreMovementStatePhaseResult`가 단순 debug log가 아니라 실제 contract가 된다.
- phase 결과 타입 설계가 조금 더 필요하다.

## 10. 상태 폭증 우려에 대한 정리

사용자 우려는 아래 질문으로 요약할 수 있다.

> `PreMovementState` 이전 상태까지 요구하면 불필요한 state가 너무 많아지는 것 아닌가?

이 질문에 대한 답은 다음과 같다.

### 10-1. snapshot은 state explosion이 아니다

phase boundary snapshot은 persistent state가 아니다.

- replay hash를 위해 저장되는 것도 아니다.
- `WorldState` 필드가 늘어나는 것도 아니다.
- 그 tick 안에서 비교용으로 읽는 관찰 지점일 뿐이다.

따라서 snapshot 추가는 "새 gameplay state 추가"와 동일하지 않다.

### 10-2. 진짜 state explosion은 authoritative record를 늘릴 때 발생한다

예를 들어 아래는 피해야 한다.

- `Idle / Walk / Push / Flip`를 authoritative state로 저장
- Animator transition mode를 `PlayerControlState`에 저장
- blend duration을 world state로 저장

이런 값은 gameplay truth가 아니라 presentation detail이다.

### 10-3. 현재 문제는 state 증가가 아니라 boundary 관측 부족이다

이번 문제의 본질은 다음과 같다.

- `StartedThisTick`은 전이 정보다.
- 전이 정보에는 before/after가 필요하다.
- 현재 builder가 after만 보고 있다.

즉, state 자체가 너무 많아서 생긴 문제가 아니라 phase boundary를 builder가 충분히 관찰하지 못해서 생긴 문제다.

## 11. 최종 권장안

이 문서는 최종적으로 아래 방향을 권장한다.

### 11-1. authoritative gameplay state

`PlayerControlState`에는 gameplay action runtime state만 둔다.

- `PlayerActionRuntimeState`
- push contact accumulation
- cooldown
- recovery

여기에 final animation state나 blend policy를 넣지 않는다.

### 11-2. presentation signal 생성 방식

초기 구현 우선순위는 아래와 같다.

1. 장기 권장안:
   - `PreMovementStatePhaseResult`를 typed delta 기반으로 확장
   - `StartedThisTick` 같은 transition signal을 phase 내부에서 계산

2. 단기 최소 수정안:
   - `snapshotAfterEnemyAi`를 `TickPresentationBuildContext`에 추가
   - builder가 before/after snapshot 비교로 signal 생성

즉, 설계적으로는 선택안 B를 우선 권장하고, 작업량을 줄여야 하면 선택안 A를 허용한다.

## 12. Presenter 해석 규칙

최종 animation state는 `GameplayTickViewPresenter`가 아래 순서로 해석한다.

1. active action이 `Flip`이면 `Flip`
2. 아니고 active action이 `Push`이면 `Push`
3. 아니고 플레이어에게 실제 move motion이 있으면 `Walk`
4. 아니면 `Idle`

이 규칙을 유지하면:

- 접촉 누적 중: `Idle`
- blocked move: `Idle`
- wind-up 중: `Push` 또는 `Flip`
- 실제 이동 성공: `Walk`

이라는 요구사항을 일관되게 만족할 수 있다.

## 13. 금지 규칙

아래 구조는 금지한다.

- `StartedThisTick`를 만들기 위해 `Idle / Walk / Push / Flip`를 authoritative state로 저장
- `Animator` 상태나 clip timeline을 읽어 action 시작 여부를 판단
- blend duration을 `PlayerControlState`에 저장
- `View`가 wind-up 완료를 판단해 gameplay execute를 호출

금지 예시:

```csharp
if (animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 0.3f)
{
    ExecutePush();
}
```

```csharp
playerControlState.animationState = PlayerAnimationState.Push;
```

이런 구조는 determinism, replay, phase 책임 경계를 모두 오염시킨다.

## 14. 구현 순서 권장

1. `PlayerControlState`에 `PlayerActionRuntimeState` 추가
2. `PlayerControlStateLogic`에 wind-up/recovery/action start 규칙 추가
3. `PlayerLogic`을 execute tick intent producer로 단순화
4. `PreMovementStatePhaseResult`를 typed delta로 확장
5. `TickPresentationData`에 player action presentation signal 추가
6. `GameplayTickViewPresenter`에서 final `Idle / Walk / Push / Flip` 해석 추가
7. `PlayerAnimatorDriver`를 `Gameplay_Host`에 추가

## 15. 최종 정리

이 문서의 핵심 결론은 아래 세 줄이다.

- `PreMovementState`는 movement 이전 phase일 뿐, action 시작 이전 snapshot 자체가 아니다.
- `StartedThisTick`은 전이 정보이므로 before/after 비교 또는 typed delta가 필요하다.
- 이 문제를 해결하기 위해 authoritative state를 늘릴 필요는 없고, phase boundary 관찰 또는 phase result contract 확장으로 충분하다.

한 줄 요약:

> `StartedThisTick` 문제는 state explosion 문제가 아니라 phase transition 관측 문제이며, 최종 권장안은 `PreMovementStatePhaseResult`의 typed delta 확장이다.
