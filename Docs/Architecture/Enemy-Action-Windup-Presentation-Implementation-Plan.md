# Enemy Action Wind-up Presentation Implementation Plan

## 1. 목적

이 문서는 `Docs/Architecture/Enemy-Action-Windup-Presentation-Blueprint.md`를 실제 Unity C# 구현 작업으로 내리기 위한 실행 계획서다.

목표는 적 공격에 wind-up 및 recovery presentation을 붙이는 것 자체가 아니라, 아래 구조 규칙을 먼저 코드 레벨에서 고정하는 것이다.

- 적 macro AI와 action timing을 분리한다.
- 적 공격 execute tick을 authoritative하게 저장한다.
- 기존 `Recover` authoritative cooldown을 유지한다.
- animation은 계속 presentation-only 계층으로 남긴다.

이 계획서는 다음 문서를 따른다.

- `Docs/Architecture/Enemy-Action-Windup-Presentation-Blueprint.md`
- `Docs/Architecture/Enemy-AI-FSM-Blueprint.md`
- `Docs/Architecture/Enemy-AI-FSM-Implementation-Plan.md`
- `Docs/Architecture/Gameplay-Timing-Ownership-Blueprint.md`
- `Docs/Architecture/Gameplay-Timing-Ownership-Implementation-Plan.md`
- `Docs/Architecture/Player-Action-Windup-Presentation-Blueprint.md`

## 2. 현재 상태

2026-04-04 기준 현재 코드베이스는 다음 상태다.

- `EnemyAiMode`와 `aiStateTimer`가 적 macro FSM과 recover cooldown을 소유한다.
- `EnemyLogic`은 `Attack` 상태일 때 같은 tick attack phase에서 바로 attack intent를 만든다.
- `EnemyAiProfile`는 logic-only 구조다.
- `EnemyViewPresentationMapper`는 `DidAttack`, `TookDamage`, `DidDie`, `IsMoving` 정도만 전달한다.
- `EnemyAnimatorDriver`는 parameter / trigger mapping만 가진다.
- 적군에는 player `activeAction`에 대응하는 별도 action timing state가 없다.

즉 현재 구조는 "적이 공격한다"는 사실은 표현할 수 있지만, 다음은 아직 표현할 수 없다.

- wind-up 시작
- wind-up 진행
- execute tick 분리
- wind-up cancel
- recovery 진입 beat

## 3. 구현 원칙

- `EnemyAiMode`에 `AttackWindup`를 추가하는 방식은 최종안으로 채택하지 않는다.
- `EntityPhaseState/stateTimer`를 적 action timing의 canonical source로 쓰지 않는다.
- `Recover`와 `aiStateTimer`는 계속 recovery authoritative source로 유지한다.
- 적 action timing은 player와 유사하게 별도 runtime state로 저장한다.
- view는 최종 presentation signal과 final entity state만 읽는다.
- animation event는 damage authority가 아니다.
- default profile에서 wind-up이 `0`이면 기존 동작과 동일해야 한다.

## 4. 최종 산출물

이번 작업의 최종 산출물은 다음과 같다.

- 신규 `EnemyActionKind`
- 신규 `EnemyActionRuntimeState`
- 신규 `EnemyActionTransition`
- `WorldState` / `WorldSnapshot`의 enemy action state 저장 경로
- determinism hash / trace / replay dump 반영
- `EnemyAiProfile`의 wind-up config 추가
- 신규 `IEnemyActionStateLogic`와 pipeline hook
- `EnemyLogic`의 execute tick 기반 attack intent gating
- 신규 `TickEnemyActionPresentationSignal`
- `EnemyViewPresentationMapper`와 `EnemyAnimatorDriver` 확장
- 관련 unit / scenario / presentation 테스트

## 5. 비범위

아래 기능은 이번 작업의 1차 범위에 포함하지 않는다.

- `EnemyAiMode`에서 `Attack` 또는 `Recover` 제거
- Blackboard 분리 저장소 도입
- ranged cast, leap, roar 같은 신규 적 액션 추가
- animation event 기반 hit authority
- 적 전용 VFX / SFX authoring 시스템
- `EnemyAnimationTimingAuthoring` 대규모 도입

## 6. 단계별 작업 계획

### 6-1. 1단계: authoritative enemy action state 추가

목표는 적 공격 wind-up과 execute timing을 저장할 별도 authoritative state를 도입하는 것이다.

신규 후보 파일:

- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyActionState.cs`

권장 타입:

- `EnemyActionKind`
- `EnemyActionRuntimeState`
- `EnemyActionTransition`
- `EnemyActionQueries`

수정 대상 파일:

- `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldState.cs`
- `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldSnapshot.cs`
- `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/IWorldWriteContext.cs`
- `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/IWorldStateMutationPort.cs`
- `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldStateWriteContext.cs`
- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/DeterminismHashBuilder.cs`
- debug trace 관련 파일

작업:

- entity ID 기준 `EnemyActionRuntimeState` 저장 dictionary 추가
- snapshot에서 read-only 조회 가능하게 노출
- ordered enumeration helper 추가
- hash / trace / replay dump에 새 상태 반영

완료 조건:

- enemy action timing state가 authoritative data로 저장된다.
- 같은 initial state면 같은 execute schedule이 복원된다.

진행 상태:

- 2026-04-04 구현 완료
- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyActionState.cs`를 추가해 `EnemyActionKind`, `EnemyActionRuntimeState`, `EnemyActionTransition`, `EnemyActionQueries`를 도입했다.
- `WorldState` / `WorldSnapshot` / `IWorldWriteContext` 경로에 enemy action state 저장소와 read-only 조회, ordered enumeration helper를 추가했다.
- `DeterminismHashBuilder`, `TickTraceFormatter`, `TickReplayHarness`에 enemy action state dump를 연결해 hash / trace / replay dump가 새 상태를 포함하도록 반영했다.
- `TickReplayDeterminismTests`에 snapshot 보존과 hash / replay dump 포함 여부를 검증하는 회귀 테스트를 추가했다.
- 검증은 Unity batchmode script compilation 성공과 `TickReplayDeterminismTests` edit mode 실행(`24 passed, 0 failed`) 기준으로 확인했다.

### 6-2. 2단계: `EnemyAiProfile`에 authoritative wind-up config 추가

목표는 wind-up tick의 canonical source를 logic profile에 두는 것이다.

신규 또는 변경 후보:

- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyAiConfig.cs`

권장 추가 구조:

```csharp
[Serializable]
public struct EnemyAttackTimingSettings
{
    [SerializeField] private int windupTicks;
}
```

작업:

- `EnemyAiProfile` serialized surface에 wind-up config 추가
- validation 추가
- runtime definition으로 전달
- default 값은 `0`

완료 조건:

- wind-up이 logic-only config로 표현된다.
- default profile은 기존 즉시 공격 동작을 유지한다.

주의:

- `recoverTicks`는 계속 기존 authoritative source를 유지한다.
- animation tuning field는 여기에 넣지 않는다.

진행 상태:

- 2026-04-04 구현 완료
- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyAiConfig.cs`에 `EnemyAttackTimingSettings`를 추가하고 `EnemyAiRuntimeDefinition`이 해당 값을 검증 및 보관하도록 연결했다.
- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyAiProfile.cs` serialized surface에 `attackTimingSettings`를 추가해 profile이 logic-only wind-up tick을 runtime definition으로 전달하도록 반영했다.
- 기존 구형 `EnemyAiConfig` 경로는 wind-up 기본값 `0`을 유지하도록 맞춰 default melee profile의 즉시 공격 동작이 깨지지 않게 했다.
- `GameplayTimingOwnershipTests`, `EnemyLogicTests`에 serialized contract, default `0`, custom wind-up 전달, 음수 validation 회귀 테스트를 추가했다.
- 검증은 Unity `6000.3.11f1` batchmode script compilation 성공 로그 기준으로 확인했다. CLI `-runTests`는 현재 환경에서 결과 XML을 남기지 않아 새 테스트 실행 결과는 후속 확인이 필요하다.

### 6-3. 3단계: enemy action timing phase 추가

목표는 macro AI transition과 attack execute schedule 계산을 분리하는 것이다.

신규 후보 파일:

- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyActionStateLogic.cs`

신규 후보 인터페이스:

```csharp
public enum EnemyActionStage
{
    BeforeAttackCollection = 0,
    AfterAttack = 1,
}

public interface IEnemyActionStateLogic : IEntityLogic
{
    void CommitEnemyActionState(
        WorldSnapshot snapshot,
        in TickInput input,
        EnemyActionStage stage,
        IEnemyActionCommitContext writeContext,
        List<EnemyActionTransition> transitions);
}
```

수정 대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/IEntityLogic.cs`
- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyLogic.cs`
- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/GameplayEntityLogicProviderFactory.cs`
- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs`

작업:

- `BeforeAttack` AI transition 이후, attack collect 이전에 enemy action state commit stage 추가
- `Attack` 진입 시 `EnemyActionRuntimeState` 시작
- execute 전 target invalid면 cancel
- attack phase 이후 action state clear 또는 executed mark 정리

완료 조건:

- `EnemyAiMode.Attack`과 `EnemyActionRuntimeState`가 분리된 책임으로 동작한다.
- wind-up 중에는 attack intent가 생성되지 않는다.

진행 상태:

- 2026-04-04 구현 완료
- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/IEntityLogic.cs`에 `EnemyActionStage`, `IEnemyActionStateLogic`, `EntityLogicSet.EnemyActionStateLogics`를 추가해 적 action timing phase를 pipeline 소유권에 올렸다.
- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyActionStateLogic.cs`와 `EnemyActionStateEntityLogicFactory`를 추가해 `Attack` 진입 시 action state 시작, locked target 유지, cancel fallback, after-attack executionAttempted mark를 별도 로직으로 분리했다.
- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs`에 `BeforeAttackCollection` / `AfterAttack` enemy action hook를 추가했고, `Assets/_Features/Gameplay/Gameplay_Debug/Runtime/TickTraceBuilder.cs`, `Assets/_Features/Gameplay/Gameplay_Debug/Runtime/TickTraceFormatter.cs`에 enemy action transition trace dump를 연결했다.
- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyLogic.cs`의 AI resolver는 active action의 locked target이 유효할 때만 `Attack`을 유지하고, target 상실 시 `Chase` 또는 `Patrol`로 fallback 하도록 조정했다.
- 검증은 Unity `6000.3.11f1` batchmode script compilation 성공 로그 기준으로 확인했다. 현재 환경에서는 `-runTests`가 result XML 없이 종료되어 edit mode 테스트 실행 결과는 별도 후속 확인이 필요하다.

### 6-4. 4단계: `EnemyLogic` attack intent를 execute tick 기반으로 변경

목표는 적 공격이 `EnemyAiMode.Attack`이 아니라 exact execute tick에 의해 발생하도록 만드는 것이다.

수정 대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyLogic.cs`

작업:

- `CollectAttackIntents`가 `EnemyActionRuntimeState`를 읽도록 변경
- 조건은 `IsActive && executeTick == currentTick && !executionAttempted`
- execute 이후 `AfterAttack`에서 기존 `Recover` 전이 유지
- non-attacking profile과 charge profile 회귀 확인

완료 조건:

- `windupTicks > 0`인 적은 telegraph 후에만 공격한다.
- `windupTicks == 0`이면 기존 즉시 execute와 동일하다.

진행 상태:

- 2026-04-04 구현 완료
- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyActionState.cs`의 `StartAction`은 zero-windup 시작 시에도 `executionAttempted`를 미리 세우지 않도록 조정했고, `CanExecute` helper를 추가해 execute tick gating 조건을 공용화했다.
- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyLogic.cs`의 `CollectAttackIntents`는 이제 `EnemyActionRuntimeState`의 `executeTick`, `executionAttempted`, `lockedTargetEntityId`를 읽어 exact execute tick에서만 intent를 생성한다.
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/EnemyLogicTests.cs`에 no-active-action no-op, wind-up start tick no attack, execute tick only attack, locked target loss cancel 회귀 케이스를 추가했다.
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/AttackInputNormalizationTests.cs`는 새 `EnemyActionPhaseResult` trace signature에 맞게 갱신했다.
- 검증은 Unity `6000.3.11f1` batchmode script compilation 성공 로그 기준으로 확인했다. CLI `-runTests`는 현재 환경에서 결과 XML을 남기지 않아 새 테스트 실행 결과는 후속 확인이 필요하다.
- 2026-04-05 follow-up
- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyLogic.cs`의 `CollectAttackIntents`에서 잔여 `EnemyAiMode.Attack` gate를 제거해 attack intent authority가 `EnemyActionRuntimeState.CanExecute`와 locked target validation에만 걸리도록 정리했다.
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/EnemyLogicTests.cs`에 action-state-only execute gating, zero-windup immediate execute, non-attacking profile no-op, charging profile no-op 회귀 테스트를 추가해 4단계 완료 조건을 직접 고정했다.
- Windows `dotnet build Game.Feature.Gameplay.Tests.csproj -c Debug`로 수정된 test assembly 빌드 통과를 다시 확인했다. Unity `6000.3.11f1` batchmode `-runTests`는 이 환경에서 이번에도 스크립트 리컴파일 후 종료되어 실제 EditMode 실행 결과 XML은 남기지 못했다.

### 6-5. 5단계: 적군 presentation signal 추가

목표는 view가 raw snapshot 비교 없이 wind-up / execute / recovery beat를 해석할 수 있게 만드는 것이다.

신규 또는 변경 파일:

- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPresentationData.cs`
- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickResultBuilder.cs`

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

작업:

- action transition과 final runtime state로 `StartedThisTick`, `CanceledThisTick` 계산
- attack phase selected group source ID로 `ExecutedThisTick` 계산
- AI transition `Attack -> Recover`로 `StartedRecoveryThisTick` 계산
- `TickPresentationData`에 enemy action signal collection 추가

완료 조건:

- 적군 presentation도 player처럼 명시적 signal을 가진다.
- animator driver가 raw phase result 세부를 직접 알 필요가 없다.

### 6-6. 6단계: host mapper / driver 확장

목표는 새 signal을 적군 presentation state에 통합하는 것이다.

수정 대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyViewPresentationMapper.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyAnimatorDriver.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayAnimationSyncCoordinator.cs`

작업:

- `EnemyViewPresentationState`에 active action kind / wind-up / execute / recovery beat 추가
- mapper가 motion, damage, death, action signal, final ai mode를 함께 merge
- driver는 기존 `Attack/Hit/Death` trigger를 유지하되 wind-up / recovery enter signal도 optional parameter 또는 trigger로 확장

완료 조건:

- 적 animator가 wind-up 시작, execute, recovery 시작을 구분해 표현 가능하다.
- animator가 없어도 logic 결과는 동일하다.

### 6-7. 7단계: optional `EnemyAnimationTimingAuthoring` hook 정리

목표는 향후 적별 clip tuning 확장 포인트를 열어두되, 이번 작업의 logic 범위와 분리하는 것이다.

신규 후보 파일:

- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyAnimationTimingAuthoring.cs`

권장 필드:

- `attackWindupAnimatorDurationSeconds = -1f`
- `recoverAnimatorDurationSeconds = -1f`
- `stateTransitionCrossFadeDurationSeconds`

작업:

- 이번 단계에서는 optional hook와 ownership 규칙만 고정
- 실제 도입은 animator가 붙은 enemy prefab이 생길 때 진행

완료 조건:

- 적 animation tuning이 필요해져도 `EnemyAiProfile`을 오염시키지 않는다.

### 6-8. 8단계: 테스트 보강

목표는 새 구조의 ownership과 timing contract를 테스트로 고정하는 것이다.

수정 또는 신규 후보 파일:

- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/EnemyLogicTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Scenario/EnemyAiScenarioTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/GameplayTimingOwnershipTests.cs`
- 적 view / mapper / presenter 관련 unit test

필수 테스트:

- default `windupTicks == 0`에서 즉시 execute 유지
- `windupTicks > 0`일 때 start tick에는 attack intent가 없음
- execute tick에서만 attack intent가 생성
- wind-up 중 target 상실 시 cancel
- execute 후 `Recover`와 `aiStateTimer` 진입
- recovery 중 movement / attack no-op
- enemy action state가 determinism hash와 trace에 반영
- `EnemyAiProfile` serialized surface가 logic-only contract를 유지
- view signal이 logic 결과를 바꾸지 않음

완료 조건:

- 구조적 회귀를 테스트가 막는다.

### 6-9. 9단계: showcase / prefab / scene 검토

목표는 실제 presentation 경로가 눈으로 확인 가능한 최소 샘플을 만드는 것이다.

검토 대상:

- `Assets/Scenes/CombinedGameplayShowcase.unity`
- 적군 view factory 경로
- 필요 시 attacking enemy sample profile

작업:

- current showcase의 non-attacking / charging enemy 외에 wind-up demo용 attacking enemy 추가 여부 결정
- animator가 없는 primitive fallback에서도 signal counter 기반 검증 가능하도록 유지
- 실제 animator prefab이 붙는 경우 optional timing authoring 연결 경로 확인

완료 조건:

- 적 wind-up / execute / recover presentation이 최소 1개 showcase 또는 test prefab에서 검증 가능하다.

### 6-10. Deferred: recovery cadence를 `EnemyActionRuntimeState`로 이동 검토

이 단계는 이번 작업의 즉시 범위가 아니다.

목표는 post-attack recovery cadence를 `Recover + aiStateTimer`에서 `EnemyActionRuntimeState` 계층으로 옮길 수 있는지 검토하고, 가능하면 `aiStateTimer`를 charge 전용 필드로 축소하는 것이다.

검토 배경:

- 현재 recovery countdown은 enemy AI phase에서 직접 감소하므로 timing이 명확하다.
- generic `state/stateTimer`는 cleanup auto-transition semantics 때문에 enemy recover authoritative source로 부적합하다.
- 다만 `aiStateTimer`가 `Recover`와 `Charge`를 같이 싣고 있어 장기적으로 의미가 섞일 여지가 있다.

후속 migration 후보:

- `EnemyActionRuntimeState`에 `recoveryEndTick` 추가
- movement / attack gating을 `aiMode == Recover`가 아니라 action recovery cadence 기준으로 재해석
- `EnemyAiMode.Recover`를 유지할지, derived state로 바꿀지 별도 결정
- migration 완료 후 `aiStateTimer`를 charge 전용 필드로 축소 또는 rename 검토

진입 조건:

- enemy wind-up / execute runtime state가 snapshot, hash, trace, tests까지 안정화되어 있을 것
- current `Recover + aiStateTimer` contract가 테스트로 충분히 고정되어 있을 것
- charging enemy와 standard melee enemy의 ownership 경계가 문서화되어 있을 것

주의:

- 이 migration을 wind-up 도입과 같은 PR에 묶지 않는다.
- recovery ownership 변경과 charge timer 축소는 한 번에 하지 않고, 단계적으로 검증한다.
- 새 action cadence 요구가 생겨도 `aiStateTimer`에 의미를 추가로 싣지 않는다.

## 7. 권장 구현 순서

가장 안전한 순서는 다음과 같다.

1. enemy action state 저장소 추가
2. profile wind-up config 추가
3. pipeline hook와 action state logic 추가
4. attack intent gating 변경
5. presentation signal 추가
6. host mapper / driver 확장
7. 테스트 보강
8. showcase 검토

이 순서를 권장하는 이유는 다음과 같다.

- authoritative state와 pipeline 순서를 먼저 고정해야 presentation이 안전하다.
- default `windupTicks == 0` 호환성을 먼저 확보해야 회귀를 줄일 수 있다.
- signal 계층은 runtime state가 안정된 뒤에 올리는 것이 가장 단순하다.

## 8. 리뷰 체크리스트

이 구조 이후 PR 리뷰에서는 아래 항목을 확인한다.

- wind-up 값이 animation authoring이 아니라 logic profile에 들어갔는가
- execute tick이 exact authoritative state로 저장되는가
- `EnemyAiMode`가 macro FSM 외 의미까지 떠안지 않는가
- `EntityPhaseState`가 적 action timing canonical source로 오용되지 않는가
- recovery authoritative source가 `Recover + aiStateTimer`와 중복되지 않는가
- `aiStateTimer`에 recover / charge 외 새 action cadence 의미가 추가되지 않았는가
- view signal이 logic 결과를 바꾸지 않는가
- default profile이 기존 즉시 공격 동작을 유지하는가

## 9. 향후 확장 규칙

이 계획이 완료된 이후 새 enemy action이 들어오면 아래 절차로 확장한다.

1. action timing이 gameplay authority인지 먼저 판정
2. authoritative config는 `EnemyAiProfile` 계층에 추가
3. per-instance schedule은 `EnemyActionRuntimeState`에 추가
4. render-only beat는 `TickEnemyActionPresentationSignal`에 추가
5. animator tuning은 필요 시 `EnemyAnimationTimingAuthoring`에 추가

추가 원칙:

- 새 enemy cadence 요구를 `aiStateTimer`에 누적하지 않는다.
- `aiStateTimer`는 현행 `Recover`와 `Charge`까지만 유지하고, 그 이후 확장은 별도 runtime state를 우선 검토한다.

예:

- melee wind-up tick 증가: `EnemyAiProfile`
- ranged cast execute tick: `EnemyActionRuntimeState`
- cast start / cast release trigger: `TickEnemyActionPresentationSignal`
- cast clip speed override: `EnemyAnimationTimingAuthoring`

## 10. 최종 결론

이번 작업의 핵심은 적 공격을 "즉시 attack intent"에서 "macro attack state + separate action timing state" 구조로 바꾸는 것이다.

최종 구조는 다음과 같다.

```text
EnemyAiMode
  + EnemyActionRuntimeState
  + TickEnemyActionPresentationSignal
  -> EnemyViewPresentationMapper
  -> EnemyAnimatorDriver
```

이 구조를 채택하면 다음을 동시에 만족할 수 있다.

- 적군에도 player 수준의 wind-up / execute / recovery presentation 추가
- 기존 `Recover` authoritative cooldown 유지
- `EnemyAiProfile` logic-only ownership 유지
- animation event 비권위 원칙 유지
