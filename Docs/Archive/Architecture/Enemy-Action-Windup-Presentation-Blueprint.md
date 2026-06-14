> Archived historical document.
> This file is not part of the active truth-source chain. Start with [Docs/Architecture/README.md](../../Architecture/README.md).
> Archive index: [Docs/Archive/README.md](../README.md).

# Enemy Action Wind-up Presentation Blueprint

## 1. 목적

이 문서는 현재 `Assets/_Features/Gameplay`의 결정론적 Tick 시뮬레이션 구조 위에서 적군의 wind-up 및 recovery presentation을 어떻게 추가할지에 대한 최종 기준을 정의한다.

핵심 목표는 다음 네 가지다.

- 적 공격에 telegraph 가능한 wind-up을 추가하되, gameplay authority를 유지한다.
- 기존 `EnemyAiMode` macro FSM과 action timing을 분리한다.
- recovery presentation을 기존 `Recover` authoritative 규칙과 충돌 없이 해석한다.
- `Logic -> Presentation Data -> View` 단방향 구조를 유지한다.

이 문서는 아래 문서의 상위 구조 원칙을 따른다.

- `Docs/Architecture/Hybrid-Architecture-Rulebook.md`
- `Docs/Architecture/Deterministic-Tick-Simulation-Blueprint.md`
- `Docs/Architecture/Enemy-AI-FSM-Blueprint.md`
- `Docs/Architecture/Gameplay-Timing-Ownership-Blueprint.md`
- `Docs/Architecture/Player-Action-Windup-Presentation-Blueprint.md`

## 2. 적용 대상

현재 코드베이스에서 이 문서와 직접 연결되는 핵심 지점은 다음과 같다.

- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyLogic.cs`
- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyAiConfig.cs`
- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyAiMode.cs`
- `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldState.cs`
- `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldSnapshot.cs`
- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs`
- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPresentationData.cs`
- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickResultBuilder.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyViewPresentationMapper.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyAnimatorDriver.cs`

정리하면 현재 구조는 다음 상태다.

- 적 macro decision은 `EnemyAiMode`가 가진다.
- 공격 성공 여부와 피해 적용은 attack phase가 authoritative하게 결정한다.
- 공격 후 cooldown은 `Recover`와 `aiStateTimer`가 소유한다.
- 적 view는 `DidAttack`, `TookDamage`, `DidDie`, `IsMoving` 정도만 해석한다.
- 적군에는 player의 `activeAction.executeTick`에 대응하는 별도 action timing 상태가 없다.

## 3. 현재 구조의 문제

현재 적군 presentation에는 아래 세 가지 문제가 있다.

### 3-1. 공격 telegraph용 wind-up을 authoritative하게 표현할 저장소가 없다

현재 적은 `BeforeAttack` 단계에서 사거리에 들어오면 같은 tick의 attack phase에서 바로 `RawAttackIntent`를 생산한다.

즉:

- 공격 commitment 시작 시점
- 실제 execute 시점
- wind-up 취소 시점

을 서로 다른 데이터로 표현할 수 없다.

### 3-2. `EnemyAiMode`만으로는 macro FSM과 action timing을 동시에 설명하기 어렵다

`EnemyAiMode`는 현재 다음 의미를 가진다.

- `Patrol`
- `Chase`
- `Attack`
- `Recover`
- `Charge`
- `Dead`

이 값은 tactical decision layer다.

여기에 `AttackWindup` 같은 세부 action-timing 상태를 계속 추가하면 아래 문제가 생긴다.

- macro FSM과 action execution FSM이 섞인다.
- `Charge`, ranged cast, leap 같은 변형이 늘수록 enum이 비대해진다.
- animator용 세부 표현 요구가 AI decision state를 오염시킨다.

### 3-3. `EntityPhaseState/stateTimer`만으로는 execute boundary를 안정적으로 표현하기 어렵다

현재 `EntityPhaseState`는 generic lifecycle 용도다.

- `Acting`
- `Cooldown`
- `Sliding`

또한 cleanup에서 `stateTimer` 감소와 `Acting/Cooldown -> Idle` 자동 전이가 이미 존재한다.

이 구조는 다음 요구와 잘 맞지 않는다.

- wind-up 시작 후 N tick 뒤 정확히 execute
- execute 전에 cancel 가능
- execute 이후에는 기존 `Recover` authoritative cooldown과 연결

즉 `state/stateTimer`는 보조 제약으로는 유용하지만, 적 공격 wind-up의 canonical source로 쓰기에는 semantics가 너무 generic하다.

## 4. 배제한 대안

### 4-1. `EnemyAiMode.AttackWindup` 추가

장점:

- 현재 구조에서 빠르게 구현 가능하다.

단점:

- decision FSM과 action timing FSM이 섞인다.
- 적 archetype이 늘수록 `EnemyAiMode`가 과도하게 세분화된다.
- timing ownership 문서에서 요구하는 "logic state와 presentation state의 분리"를 장기적으로 해친다.

따라서 이 문서는 `AttackWindup` enum 추가를 최종안으로 채택하지 않는다.

### 4-2. `EntityPhaseState.Acting/Cooldown`만 사용

장점:

- 공용 제약 계층을 재사용할 수 있다.

단점:

- cleanup semantics와 충돌한다.
- execute tick을 별도 필드 없이 정확히 표현하기 어렵다.
- 적 공격 timing과 removed entity/slide 같은 generic phase timer가 같은 계층에 섞인다.

따라서 이 문서는 `EntityPhaseState` 단독 사용을 최종안으로 채택하지 않는다.

### 4-3. recovery까지 전부 `EnemyActionRuntimeState`로 이동

장점:

- player `activeAction`와 더 대칭적이다.

단점:

- 현재 `Recover`와 `aiStateTimer`가 이미 authoritative cooldown으로 자리잡고 있다.
- `EnemyAiProfile`의 `recover tick`과 action runtime state가 중복 source가 된다.
- 기존 `Patrol -> Chase -> Attack -> Recover` FSM 문서와 테스트를 한 번에 뒤집어야 한다.

따라서 이번 설계에서는 recovery의 authoritative source는 계속 `Recover`와 `aiStateTimer`에 둔다.

## 5. 최종 채택안

최종 구조는 아래 네 계층으로 분리한다.

### 5-1. Macro AI Layer

적의 tactical decision은 계속 `EnemyAiMode`가 소유한다.

예:

- `Patrol`
- `Chase`
- `Attack`
- `Recover`
- `Charge`
- `Dead`

중요한 점:

- `Attack`은 더 이상 "이번 tick에 즉시 공격이 발생한다"를 의미하지 않는다.
- `Attack`은 "적이 공격 action에 commit되어 있으며 execute 여부는 별도 action timing state가 결정한다"를 의미한다.

### 5-2. Enemy Action Timing Layer

적 공격 wind-up 및 execute timing은 신규 `EnemyActionRuntimeState`가 소유한다.

권장 최소 구조는 다음과 같다.

```csharp
public enum EnemyActionKind
{
    None = 0,
    Melee = 1,
}

public struct EnemyActionRuntimeState
{
    public EnemyActionKind kind;
    public int sequence;
    public int lockedTargetEntityId;
    public Direction direction;
    public int startTick;
    public int executeTick;
    public bool executionAttempted;

    public bool IsActive => kind != EnemyActionKind.None;
}
```

권장 전이 도우미는 다음과 같다.

```csharp
public readonly struct EnemyActionTransition
{
    public int EntityId { get; }
    public EnemyActionKind PreviousKind { get; }
    public EnemyActionKind CurrentKind { get; }
    public int PreviousSequence { get; }
    public int CurrentSequence { get; }
    public bool StartedThisTick { get; }
    public bool CanceledThisTick { get; }
}
```

이 계층의 책임은 다음과 같다.

- attack commitment 시작
- wind-up 중 target / facing lock
- exact execute tick 소유
- execute 전 cancel 판정

이 계층이 소유하지 않는 것은 다음과 같다.

- animator state 이름
- clip speed
- crossfade
- post-attack recover tick

### 5-3. Recovery Layer

post-attack cooldown은 계속 `EnemyAiMode.Recover`와 `aiStateTimer`가 소유한다.

즉 recovery authoritative source는 아래 둘이다.

- `EnemyAiMode.Recover`
- `EntityState.aiStateTimer`

이 문서에서 recovery presentation은 새 timing source를 만들지 않고, 기존 authoritative recover 상태를 해석한다.

### 5-4. Recovery Ownership Migration Review

현재 채택안은 recovery authoritative source를 계속 `Recover + aiStateTimer`에 둔다.

이 판단의 이유는 다음과 같다.

- recovery countdown은 enemy AI phase에서 직접 줄이고, 같은 tick의 movement / attack gating 판단 전에 반영되어야 한다.
- generic `EntityPhaseState.stateTimer`는 cleanup 단계에서 감소하며, `Acting/Cooldown -> Idle` 자동 전이 규칙을 가진다.
- 따라서 generic phase timer를 enemy recover에 그대로 쓰면 off-by-one 보정, cleanup-driven auto-return, AI 분기 규칙 누락 문제가 생긴다.

즉 현재 단계에서는 `Recover + aiStateTimer`가 가장 단순하고 안정적인 authoritative source다.

다만 장기적으로는 recovery cadence를 `EnemyActionRuntimeState`로 옮길 여지가 있다.

권장 migration 후보 형태는 다음과 같다.

```csharp
public struct EnemyActionRuntimeState
{
    public EnemyActionKind kind;
    public int sequence;
    public int lockedTargetEntityId;
    public Direction direction;
    public int startTick;
    public int executeTick;
    public int recoveryEndTick;
    public bool executionAttempted;

    public bool IsActive => kind != EnemyActionKind.None;
}
```

이 migration이 제공하는 장점은 다음과 같다.

- wind-up, execute, recovery를 하나의 action timeline으로 묶을 수 있다.
- player `activeAction`와 더 대칭적인 구조가 된다.
- `aiStateTimer`를 recovery에서 분리한 뒤 charge 전용 필드로 축소하거나 rename할 수 있다.

이 migration의 비용은 다음과 같다.

- `EnemyAiMode.Recover`의 의미를 재정의하거나 derived state로 바꿔야 한다.
- recovery gating을 `EnemyActionRuntimeState` 기반으로 다시 짜야 한다.
- snapshot, hash, trace, presentation signal, 테스트 계약을 함께 수정해야 한다.

따라서 이 문서의 현재 결정은 다음과 같다.

- 이번 설계에서는 recovery authoritative source를 계속 `Recover + aiStateTimer`에 둔다.
- `EnemyActionRuntimeState` 기반 recovery migration은 후속 리팩터 후보로만 기록한다.
- `aiStateTimer`에는 새 action cadence 의미를 추가로 싣지 않는다.

### 5-5. Presentation Layer

적군 view는 `TickResult`와 `TickPresentationData`만 읽는다.

권장 구조:

```text
EnemyActionRuntimeState
  + EnemyAiMode
  + TickPresentationData
  -> TickEnemyActionPresentationSignal
  -> EnemyViewPresentationMapper
  -> EnemyAnimatorDriver
```

여기서 중요한 점은 다음과 같다.

- view는 `EnemyActionRuntimeState`를 직접 mutate하지 않는다.
- animation event가 execute authority가 되지 않는다.
- recover presentation은 final `aiMode`와 transition signal을 해석해 만든다.

## 6. 최종 동작 규칙

### 6-1. 공격 시작

`BeforeAttack` stage에서 AI가 `Chase -> Attack` 또는 이에 준하는 attack commitment를 결정하면, 그 시점에 `EnemyActionRuntimeState`를 시작한다.

시작 시점에 저장하는 값:

- `kind`
- `sequence`
- `lockedTargetEntityId`
- `direction`
- `startTick`
- `executeTick = startTick + windupTicks`

### 6-2. wind-up 진행

wind-up 중에는 다음 규칙을 따른다.

- `EnemyAiMode`는 `Attack` 상태를 유지한다.
- movement intent는 생산하지 않는다.
- attack intent도 아직 생산하지 않는다.
- target이 사라지거나 규칙상 무효가 되면 action state를 cancel하고 AI는 `Chase` 또는 `Patrol`로 복귀한다.

### 6-3. execute

attack phase의 raw intent 생성은 아래 조건에서만 허용한다.

- `EnemyActionRuntimeState.IsActive`
- `CurrentTickIndex == executeTick`
- `executionAttempted == false`

즉 `EnemyAiMode.Attack`이라는 이유만으로는 공격이 발생하지 않는다.

### 6-4. execute 이후

execute가 발생한 tick의 `AfterAttack` stage에서 기존 규칙대로 `Recover`로 전이한다.

즉:

```text
Attack action execute
  -> attack commit
  -> EnemyAiMode.Recover
  -> aiStateTimer = recoverTicks
```

이후 recovery presentation은 final `aiMode == Recover`와 `aiStateTimer`로 해석한다.

## 7. Timing Ownership 규칙

### 7-1. `EnemyAiProfile`

적 gameplay authority는 계속 `EnemyAiProfile`이 가진다.

새로운 authoritative wind-up 값은 이 계층에 둔다.

권장 추가 구조:

```csharp
[Serializable]
public struct EnemyAttackTimingSettings
{
    [SerializeField] private int windupTicks;
}
```

권장 ownership은 다음과 같다.

- `windupTicks`: `EnemyAiProfile`
- `recoverTicks`: 기존 `EnemyAiProfile`
- attack range / detect range / priority: 기존 `EnemyAiProfile`

### 7-2. `EnemyActionRuntimeState`

이 타입은 configuration이 아니라, configuration에서 파생된 runtime schedule이다.

즉 소유권은 아래와 같이 구분한다.

- canonical config: `EnemyAiProfile`
- per-tick resolved schedule: `EnemyActionRuntimeState`

### 7-3. `EnemyAnimationTimingAuthoring`

현재 구현은 optional `EnemyAnimationTimingAuthoring`를 통해 wind-up / recovery animator duration override와 state-transition crossfade를 `EnemyAnimatorDriver`가 소비한다. 적별 animation-only hold나 더 큰 presentation state machine이 필요해지면 같은 계층에서 확장한다.

이 계층에 둘 수 있는 값:

- `attackWindupAnimatorDurationSeconds`
- `recoverAnimatorDurationSeconds`
- `stateTransitionCrossFadeDurationSeconds`
- animator state / trigger names

이 계층에 두면 안 되는 값:

- `windupTicks`
- `recoverTicks`
- damage execute tick

## 8. Presentation Signal 계약

player와 같은 품질의 presentation 안정성을 확보하려면 적군도 별도 signal을 만드는 편이 좋다.

권장 타입:

```csharp
public readonly struct TickEnemyActionPresentationSignal
{
    public int EntityId { get; }
    public EnemyActionKind ActiveActionKind { get; }
    public int ActiveActionSequence { get; }
    public bool StartedThisTick { get; }
    public bool CanceledThisTick { get; }
    public bool ExecutedThisTick { get; }
    public bool StartedRecoveryThisTick { get; }
}
```

해석 규칙:

- `StartedThisTick`: action runtime state transition에서 계산
- `CanceledThisTick`: execute 전 action clear에서 계산
- `ExecutedThisTick`: attack phase selected group source ID로 계산
- `StartedRecoveryThisTick`: `EnemyAiMode.Attack -> Recover` 전이로 계산

## 9. `EnemyViewPresentationState` 권장 형태

최종 mapper가 animator driver로 넘기는 상태는 아래 형태를 권장한다.

```csharp
public readonly struct EnemyViewPresentationState
{
    public int EntityId { get; }
    public int TickIndex { get; }
    public EnemyAiMode AiMode { get; }
    public EnemyActionKind ActiveActionKind { get; }
    public bool IsMoving { get; }
    public bool StartedWindupThisTick { get; }
    public bool ExecutedThisTick { get; }
    public bool StartedRecoveryThisTick { get; }
    public bool TookDamage { get; }
    public bool DidDie { get; }
}
```

현재 `DidAttack` 하나만 가진 구조보다 이 값이 더 좋은 이유는 다음과 같다.

- wind-up 시작 beat와 execute beat를 분리할 수 있다.
- recover enter beat를 별도 trigger로 만들 수 있다.
- future ranged cast / roar / charge-up도 같은 signal 구조를 재사용할 수 있다.

## 10. Tick Pipeline 권장 순서

현재 pipeline 순서를 크게 뒤집지 않고 아래 형태를 권장한다.

```text
initialSnapshot
-> Enemy AI BeforeMovement
-> PreMovementState
-> Movement
-> Enemy AI BeforeAttack
-> Enemy Action State BeforeAttackCollection
-> Attack Collect / Resolve / Commit
-> Enemy Action State AfterAttack
-> Enemy AI AfterAttack
-> Cleanup
-> TickPresentationData build
```

핵심 원칙:

- macro AI 전이와 action timing state 전이는 분리한다.
- attack collection 직전 시점에 execute 여부가 확정되어야 한다.
- cleanup의 generic `stateTimer` 처리와 적 action timing을 직접 결합하지 않는다.

## 11. 테스트 기준

이 문서 이후 적 wind-up / recovery presentation은 아래를 검증해야 한다.

### 11-1. authoritative logic 테스트

- `windupTicks == 0`이면 기존과 동일하게 즉시 execute
- `windupTicks > 0`이면 start tick에는 attack intent가 생성되지 않음
- `executeTick`에서만 attack intent가 생성됨
- wind-up 중 target 상실 시 cancel
- execute 이후 `Recover`로 전이
- recovery 중 movement / attack intent가 생성되지 않음

### 11-2. determinism 테스트

- `EnemyActionRuntimeState`가 snapshot, hash, trace에 반영됨
- 같은 initial state에서 같은 execute tick이 재생성됨
- view 유무와 관계없이 execute / recover 결과가 동일함

### 11-3. presentation 테스트

- wind-up 시작 tick에 `StartedWindupThisTick`가 정확히 1회 발생
- execute tick에 `ExecutedThisTick`가 정확히 1회 발생
- recover 시작 tick에 `StartedRecoveryThisTick`가 정확히 1회 발생
- trigger 유무가 logic 결과를 바꾸지 않음

## 12. 최종 결론

적군 wind-up 및 recovery presentation의 올바른 구조는 다음과 같다.

```text
EnemyAiMode
  + EnemyActionRuntimeState
  + TickEnemyActionPresentationSignal
  -> EnemyViewPresentationMapper
  -> EnemyAnimatorDriver
```

핵심 원칙은 세 줄로 요약된다.

- macro decision은 `EnemyAiMode`가 가진다.
- wind-up과 exact execute timing은 `EnemyActionRuntimeState`가 가진다.
- recovery authoritative cadence는 계속 `Recover + aiStateTimer`가 가진다.

이 원칙을 지키면 적군도 player와 같은 수준의 wind-up / execute / recovery presentation 품질을 얻으면서, 기존 FSM과 timing ownership 규칙을 동시에 유지할 수 있다.
