# Enemy AI FSM Blueprint

## 1. 목적

이 문서는 현재 `Assets/_Features/Gameplay`의 결정론적 Tick 시뮬레이션 구조 위에서 적 AI FSM을 어떻게 설계할지에 대한 최종 기준을 정의한다.

이 문서는 아래 문서의 상위 구조 원칙을 따른다.

- `Docs/Architecture/Hybrid-Architecture-Rulebook.md`
- `Docs/Architecture/Deterministic-Tick-Simulation-Blueprint.md`
- `Docs/Architecture/Cube-Surface-Gameplay-Blueprint.md`

핵심 목표는 다음 두 가지다.

- 적 AI의 판단 로직을 결정론적으로 유지한다.
- 애니메이션과 연출이 로직을 오염시키지 않도록 `Logic -> View` 단방향 구조를 강제한다.

이 문서는 "적이 어떻게 보일지"가 아니라 "적이 무엇을 결정하고 어떤 authoritative 결과를 남길지"를 정의한다.

## 2. 적용 대상

현재 코드베이스에서 이 문서와 직접 연결되는 핵심 지점은 다음과 같다.

- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/IEntityLogic.cs`
- `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/EntityState.cs`
- `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/EntityPhaseState.cs`
- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPresentationData.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayEntityView.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHostConfiguration.cs`

정리하면:

- 적 판단은 `IEntityLogic` 계층에서 수행한다.
- authoritative 상태는 `WorldState`와 `EntityState`가 가진다.
- 시각 표현은 `TickPresentationData`와 `GameplayTickViewPresenter`가 소비한다.

## 3. 핵심 철학

- `WorldState`가 유일한 authoritative truth다.
- `EntityLogic`은 snapshot을 읽고 intent만 생산한다.
- 적 FSM은 애니메이션 상태를 모른다.
- View는 Tick 결과를 해석해서 보여줄 뿐, 로직 전이에 영향을 주지 않는다.
- 보간, blend, hit flash, animation trigger, VFX, SFX는 모두 View 책임이다.
- 적의 현재 상태는 `MonoBehaviour` 내부 임시 변수에 저장하지 않는다.

한 줄 요약:

> 적 FSM은 "무엇을 할지"를 결정하고, View는 "어떻게 보일지"만 결정한다.

## 4. 금지 규칙

아래 구조는 금지한다.

- FSM에서 `Animator`를 직접 제어
- FSM 전이를 `normalizedTime`, clip name, blend value에 의존
- `Animator` 현재 상태를 읽고 공격 판정/무적/이동 가능 여부를 결정
- `MonoBehaviour Update()` 내부 지역 상태를 authoritative FSM 상태처럼 사용
- Logic이 보간 위치를 기준으로 충돌이나 판정을 수행

금지 예시:

```csharp
if (animator.GetCurrentAnimatorStateInfo(0).IsName("Attack"))
{
    canMove = false;
}
```

```csharp
if (moveLerpT > 0.5f)
{
    logicalCell = nextCell;
}
```

이런 구조는 replay, determinism, 테스트 안정성을 모두 깨뜨린다.

## 5. 최종 구조

```text
WorldSnapshot
  -> EnemyLogic
      -> FSM state resolve
      -> RawMovementIntent / RawAttackIntent / StateChange
  -> Committer
      -> WorldState 갱신
  -> TickResult / TickPresentationData 생성
  -> GameplayTickViewPresenter
      -> GameplayEntityView / Animator Driver / VFX
```

더 단순하게 표현하면 다음과 같다.

```text
Logic Layer
  Enemy FSM
  Sensor
  Decision
  Intent Production

View Layer
  Motion interpolation
  Animation mapping
  Hit effect / death effect
  Facing visuals
```

의존 방향은 항상 다음만 허용한다.

```text
Logic -> Presentation Data -> View
```

다음 방향은 금지한다.

```text
View -> Logic
Animator -> FSM
```

## 6. 적 FSM의 책임

적 FSM은 다음만 책임진다.

- 현재 snapshot에서 적이 무엇을 해야 하는지 판단
- 상태 전이 판단
- 이동 intent 생성
- 공격 intent 생성
- cooldown, recover, 상태 타이머 같은 결정론 상태 사용
- 접촉 피해, 자폭, 도주, 순찰 같은 룰 처리

적 FSM은 다음을 책임지지 않는다.

- 공격 모션 재생 시점
- 걷기 애니메이션 재생 여부
- hit stop 연출
- 보간 좌표 계산
- blend tree 파라미터 계산
- topology change 트리거

### 6-1. 적은 면 회전을 트리거하지 않는다

적 이동은 `Move` intent를 생산할 수 있지만, 그 결과로 `CubeTopologyState`를 바꾸면 안 된다.

이 제약은 밸런스 조정이 아니라 구조 규칙이다.

- 면 회전은 `Cube-Surface-Gameplay-Blueprint.md`에서 정의한 플레이어 전용 진행 기믹이다.
- topology change는 active face 집합을 바꾸는 global state mutation이다.
- 적이 이를 직접 유발하면 플레이어 입력 없이 월드 규칙이 바뀌어 퍼즐 해석과 전투 해석이 동시에 흔들린다.
- 적 추적이 topology change까지 포함하면 "한 칸 추적"이 아니라 "월드 전환 유발"로 의미가 커져 FSM 설명 가능성이 급격히 떨어진다.
- off-screen 적이나 비활성 면 경계의 적이 회전을 만들면 플레이어가 원인과 결과를 읽기 어렵다.

따라서 적의 이동 규칙은 현재 active topology 안에서만 성립해야 한다.

구현 계약:

- 적 movement query는 player traversal query와 분리한다.
- 적은 active face 내부 ordinary move만 시도한다.
- 경계 이동이 topology change를 요구하면 그 step은 실패 또는 정지로 해석한다.
- `EnemyMovementPolicy`는 `rotationKind != None`인 결과를 허용하지 않는다.

## 7. 상태 레이어 분리

FSM 상태와 애니메이션 상태는 완전히 다른 레이어다.

예시:

- FSM 상태: `Patrol`, `Chase`, `Attack`, `Recover`, `Dead`
- 애니메이션 상태: `Idle`, `Walk`, `Run`, `AttackA`, `Hit`, `Death`

중요한 점:

- `Patrol`이어도 animation은 `Idle`일 수 있다.
- `Chase`여도 실제 한 Tick 동안 움직이지 못하면 animation은 `Idle`일 수 있다.
- `Attack` 상태라도 연출은 `AttackA`, `AttackB`, `Cast`, `Roar` 등 다양할 수 있다.

즉, FSM 상태는 gameplay rule이고 animation 상태는 presentation detail이다.

## 8. 상태 저장 원칙

현재 코드베이스는 snapshot 기반이다. 따라서 적 FSM의 지속 상태는 결정론적 데이터로 보관해야 한다.

권장 방식은 아래 둘 중 하나다.

### 8-1. `EntityState` 확장

적 AI에 필요한 최소 상태를 `EntityState` 또는 그에 대응하는 authoritative entity record에 명시적으로 추가한다.

예시:

- `EnemyAiMode aiMode`
- `int aiStateTimer`
- `int targetEntityId`
- `Direction desiredFacing`

장점:

- replay와 hash에 쉽게 포함된다.
- snapshot만으로 현재 적 상태를 복원할 수 있다.

단점:

- `EntityState`가 AI 전용 필드로 비대해질 수 있다.
- 일부 적에게만 의미 있는 필드가 공용 구조에 섞이기 쉽다.
- 적 종류가 늘수록 공통 상태와 AI 내부 기억의 경계가 흐려질 수 있다.

### 8-2. 결정론적 Blackboard 별도 저장

엔티티별 AI blackboard를 `WorldState`의 일부로 저장한다.

예시:

- `Dictionary<int, EnemyAiBlackboardState>`

장점:

- 전투용 엔티티 상태와 AI 내부 상태를 분리할 수 있다.

단점:

- snapshot, hash, trace, commit 경로를 별도로 설계해야 한다.
- 엔티티 상태와 blackboard 상태의 불일치 위험이 있다.
- 초기 구현 비용과 검증 비용이 크다.

필수 조건:

- Tick snapshot에서 동일하게 읽을 수 있어야 한다.
- determinism hash에 포함되어야 한다.
- commit 경로를 통해서만 수정되어야 한다.

권장 결론:

- 초기 버전은 가능한 한 `EntityState` 확장 또는 이에 준하는 authoritative record 확장으로 간단하게 간다.
- `MonoBehaviour` private field에 FSM 상태를 보관하는 방식은 채택하지 않는다.

### 8-3. 비교 평가표

| 비교 항목 | `EntityState` 확장 | 결정론적 Blackboard 별도 저장 |
| --- | --- | --- |
| 기본 개념 | AI 상태를 엔티티 공통 상태에 직접 포함 | AI 상태를 `WorldState` 내부 별도 저장소로 분리 |
| 구현 난이도 | 낮음 | 높음 |
| 현재 코드와의 적합성 | 매우 높음 | 중간 |
| snapshot 복원 | 쉬움 | 별도 snapshot 설계 필요 |
| determinism hash 반영 | 쉬움 | 별도 정렬 및 직렬화 필요 |
| 디버깅 | 쉬움. 엔티티 하나만 보면 됨 | 다소 복잡. 엔티티와 blackboard를 함께 봐야 함 |
| 데이터 응집도 | 낮아질 수 있음. AI 필드가 공용 구조를 오염시킬 수 있음 | 높음. AI 내부 기억을 별도로 유지 가능 |
| 적 종류 확장성 | 중간. 종류가 많아질수록 불리 | 높음. 다양한 적 메모리 구조 대응이 쉬움 |
| 구조 단순성 | 높음 | 낮음 |
| 초기 유지보수 비용 | 낮음 | 높음 |
| 장기 확장성 | 제한적 | 우수 |
| 대표 위험 | `EntityState` 비대화, 의미 없는 필드 누적 | commit/hash/snapshot 누락, 상태 불일치 |

### 8-4. 향후 확장성 평가

`EntityState` 확장은 지금 코드베이스와 가장 잘 맞는다.

- `EntityState`가 이미 snapshot의 중심 상태다.
- replay와 determinism hash가 엔티티 필드 중심으로 구성되어 있다.
- 초기 적 1종에서 3종 정도까지는 가장 빠르게 안정화할 수 있다.

하지만 아래 상황이 오면 한계가 빨리 드러난다.

- 적 종류가 늘어난다.
- 각 적이 서로 다른 내부 기억 구조를 가진다.
- 순찰 인덱스, 마지막 목격 위치, 의심 수치, 경로 캐시 같은 값이 늘어난다.

이 시점부터는 Blackboard 분리가 유리해진다.

- 공통 전투 상태와 AI 내부 기억을 분리할 수 있다.
- 적 종류별 상태 구조를 무리 없이 늘릴 수 있다.
- 향후 유닛 타입별 specialization에 대응하기 쉽다.

결론적으로:

- 단기 확장성은 `EntityState` 확장이 우수하다.
- 장기 확장성은 Blackboard 분리가 우수하다.

### 8-5. 필드 배치 기준

| 필드 종류 | 권장 위치 | 이유 |
| --- | --- | --- |
| 현재 FSM 모드 `EnemyAiMode` | `EntityState` | 거의 모든 AI 판단에서 공통적으로 참조 |
| 현재 타겟 ID `targetEntityId` | `EntityState` 또는 Blackboard | 범용성이 높으면 `EntityState`, 적 종류별 의미 차이가 크면 Blackboard |
| 행동 쿨다운, 회복 시간 | 가능하면 기존 `stateTimer` | 이미 authoritative timer 체계가 존재 |
| 마지막 목격 위치 | Blackboard | 적 내부 기억 성격이 강함 |
| 순찰 경로 인덱스 | Blackboard | 일부 적에게만 필요 |
| 경계 수치, 의심 수치 | Blackboard | AI 내부 메모리이므로 공통 엔티티 상태로 두기 부적절 |
| 로직 판정용 방향 값 | `EntityState` | 실제 판정에 쓰인다면 authoritative state여야 함 |
| Animator 관련 값 | View | logic 오염 방지 |

### 8-6. 최종 채택 방침

초기 버전은 다음 순서로 간다.

1. `EntityState` 또는 이에 준하는 authoritative entity record에 최소 필드만 추가한다.
2. `EnemyAiMode`와 정말 필요한 최소 참조값만 먼저 도입한다.
3. `stateTimer`로 표현 가능한 시간 제약은 기존 구조를 재사용한다.
4. 적 종류별 내부 기억이 누적되기 시작하면 Blackboard를 도입한다.

즉, 현재 프로젝트의 권장 선택은 다음과 같다.

- 지금 바로 구현 시작: `EntityState` 소규모 확장
- 적 종류 증가와 내부 기억 복잡화 이후: Blackboard 분리 검토

## 9. 권장 FSM 상태 집합

초기 표준 적 FSM은 아래 집합을 기준으로 한다.

### 9-1. `Patrol`

- 랜덤 배회 또는 경로 순찰
- 타겟 미감지 상태
- 타겟 감지 시 `Chase`

### 9-2. `Chase`

- 플레이어 또는 목표 엔티티 쪽으로 이동
- 공격 사거리 진입 시 `Attack`
- 타겟 상실 시 `Patrol`

### 9-3. `Attack`

- 공격 intent 생성
- 공격 직후 `Recover` 또는 `Cooldown`

### 9-4. `Recover`

- 일정 Tick 동안 행동 제한
- 종료 후 `Chase` 또는 `Patrol`

### 9-5. `Dead`

- logic상 더 이상 행동하지 않음
- 제거는 `Cleanup` 규칙을 따른다.

## 10. 상태 전이 기준

상태 전이는 반드시 authoritative 정보만 사용한다.

허용되는 입력:

- `WorldSnapshot`의 엔티티 위치
- 거리 계산
- 시야 규칙
- 팀 정보
- `hp`
- `state`, `stateTimer`
- deterministic blackboard 값
- 공격 쿨다운 Tick

금지되는 입력:

- Animator clip name
- animation progress
- `GameObject.activeSelf`
- transform 보간 좌표
- particle 재생 여부
- 적 자신의 이동을 위해 topology change가 가능한지 여부

예시 전이표:

| 현재 상태 | 조건 | 다음 상태 |
| --- | --- | --- |
| Patrol | 플레이어 감지 | Chase |
| Chase | 공격 가능 거리 진입 | Attack |
| Chase | 타겟 상실 | Patrol |
| Attack | 공격 intent 생성 완료 | Recover |
| Recover | cooldown 종료 | Chase |

## 11. 권장 코드 구조

초기 구현에서는 아래 구조를 권장한다.

```text
Assets/_Features/Gameplay/
  Gameplay_Entities/
    Runtime/
      EnemyLogic.cs
      EnemyEntityLogicFactory.cs
      EnemyAiMode.cs
      EnemyAiConfig.cs
      EnemyTargetSelector.cs
      EnemyMovementPolicy.cs
      EnemyCombatPolicy.cs
```

View 확장은 다음처럼 둔다.

```text
Assets/_Features/Gameplay/
  Gameplay_Host/
    Runtime/
      EnemyAnimatorDriver.cs
      EnemyViewPresentationMapper.cs
```

규칙:

- 적 AI 판단은 `Gameplay_Entities`에 둔다.
- Animator 관련 타입은 `Gameplay_Host` 또는 presentation 계층에 둔다.
- FSM이 view assembly를 참조하면 안 된다.
- `EnemyLogic`은 coordinator로 두고, 타겟 선택/이동/공격 규칙은 helper policy로 분리한다.

## 12. 권장 인터페이스 구조

현재 구조상 적 로직은 `IEntityLogic` 기반으로 붙는 것이 자연스럽다.

```csharp
public sealed class EnemyLogic :
    IMovementEntityLogic,
    IAttackEntityLogic,
    IEntityLogicSourceBinding
{
    public int ControlledEntityId { get; }

    public void CollectMovementIntents(
        WorldSnapshot snapshot,
        in TickInput input,
        List<RawMovementIntent> buffer)
    {
        // FSM 상태를 읽고 이동 intent만 생산
    }

    public void CollectAttackIntents(
        WorldSnapshot snapshot,
        in TickInput input,
        List<RawAttackIntent> buffer)
    {
        // FSM 상태를 읽고 공격 intent만 생산
    }
}
```

핵심은 이 로직이 `Animator`, `Transform`, `GameObject`를 몰라야 한다는 점이다.

초기 구조 보정에서는 아래 책임 분리를 권장한다.

- `EnemyLogic`: authoritative 상태 조회, mode 분기, helper 호출, intent enqueue
- `EnemyAiConfig`: 감지 범위, 공격 범위, priority, recover tick 같은 rule data
- `EnemyTargetSelector`: 결정론적 타겟 선택
- `EnemyMovementPolicy`: `Patrol`, `Chase` 이동 규칙. topology change를 발생시키는 step은 생성하지 않음
- `EnemyCombatPolicy`: 공격 가능 판정과 공격 intent 생성

이 보정은 범용 FSM 프레임워크 도입이 아니라, 4단계 상태 전이 추가 전에 책임을 얇게 나누는 최소 구조 정리다.

## 13. 적 종류 확장 방식

예외적 적군 생성이 쉬우려면 "큰 FSM 하나"가 아니라 "상태 집합 + 정책 조합" 구조를 사용해야 한다.

즉, 적 변형은 아래 둘 중 하나로 표현한다.

- 특정 상태를 제거
- 특정 policy를 교체

### 13-1. 배회만 하는 유닛

- 상태 집합: `Patrol`
- 제거 상태: `Chase`, `Attack`, `Recover`
- policy: 랜덤 이동 또는 경로 순찰

```text
Patrol only enemy
= PatrolState + WanderMovePolicy
```

### 13-2. 추적은 하지만 공격하지 않는 유닛

- 상태 집합: `Patrol`, `Chase`
- 공격 policy 없음

```text
Chaser
= PatrolState + ChaseState
```

### 13-3. 충돌 피해만 주는 유닛

- 상태 집합: `Patrol`, `Chase`
- `AttackState` 대신 `ContactDamagePolicy`
- 피해 발생은 접촉 판정 결과로 계산

```text
Contact damage enemy
= PatrolState + ChaseState + ContactDamagePolicy
```

### 13-4. 자폭 유닛

- 상태 집합: `Chase`, `Recover`
- 정책: `ContactDamagePolicy + SelfDestructPolicy`

### 13-5. 고정 포탑

- 상태 집합: `Attack`, `Recover`
- 이동 logic 없음
- 시야 안에 타겟이 있으면 공격 intent만 생성

이 구조의 장점은 적 예외를 위해 공용 FSM 전체를 뒤틀지 않아도 된다는 점이다.

## 14. 정책 기반 조합 예시

권장 정책 분리는 다음과 같다.

- `ITargetSelectionPolicy`
- `IMovementDecisionPolicy`
- `IAttackDecisionPolicy`
- `IContactEffectPolicy`
- `IStateTransitionPolicy`

예시:

```text
MeleeGuard
= NearestPlayerTargetPolicy
+ ChaseMovementPolicy
+ MeleeAttackPolicy
+ StandardRecoverPolicy

Slime
= NearestPlayerTargetPolicy
+ ChaseMovementPolicy
+ ContactDamagePolicy

Wanderer
= NoTargetPolicy
+ RandomPatrolMovementPolicy
```

주의:

- policy는 view 연출 정책이 아니라 gameplay rule 정책이다.
- animation clip 선택 정책은 별도 presentation mapper에서 처리한다.

## 15. 공격 처리 규칙

공격은 반드시 logic에서 authoritative하게 결정한다.

허용:

- 공격 사거리 진입 시 `RawAttackIntent` 생성
- 공격 성공 여부를 snapshot과 resolver 결과로 판정
- 공격 후 `EntityPhaseState.Acting` 또는 `Cooldown` 전환

금지:

- 애니메이션 이벤트가 들어와야 데미지를 적용
- sword swing clip 끝날 때까지 논리적으로 공격을 유예
- hit frame을 clip timeline으로 authoritative 판정

초기 채택 규칙:

- 데미지와 판정은 logic이 만든 intent와 commit 결과로 확정한다.
- animation event는 있더라도 VFX/SFX 동기화용 보조 신호로만 사용한다.

## 16. 접촉 피해 규칙

공격 버튼 없이 몸통 접촉으로 피해를 주는 적은 별도 attack animation을 authoritative source로 쓰면 안 된다.

권장 구조:

- 적 이동 결과 또는 인접 판정 결과로 접촉 여부 판단
- 접촉 시 synthetic attack intent 또는 movement transient 결과를 통해 피해 생성
- View는 그 결과를 보고 bump, flash, hit animation을 재생

즉:

```text
Collision or overlap result
  -> damage rule
  -> commit
  -> presentation
```

이 순서를 지켜야 한다.

## 17. View 계층 설계

View는 authoritative state를 바꾸지 않는다.

권장 책임:

- `TickPresentationData.EntityMotions`를 이용한 위치 보간
- facing rotation 표현
- attack/hit/death 연출 트리거
- 적 종류별 material, mesh, animator parameter 매핑

예시 View Mapper:

```csharp
public sealed class EnemyAnimatorDriver : MonoBehaviour
{
    public void Apply(EnemyPresentationState state)
    {
        // SetBool("IsMoving", ...)
        // SetTrigger("Attack", ...)
        // SetInteger("Mode", ...)
    }
}
```

중요:

- View는 `TickResult` 또는 presentation state만 읽는다.
- View는 `WorldState`를 직접 수정하지 않는다.
- View는 "지금 공격 애니메이션 중이므로 logic을 멈춰야 한다" 같은 결정을 내리면 안 된다.

## 18. 이동 보간 규칙

이동 보간은 반드시 View에서 처리한다.

올바른 구조:

```text
Logic:
  (3, 3) -> (4, 3) 이동 확정

View:
  source cell에서 destination cell로 lerp
```

잘못된 구조:

```text
Logic:
  40%는 이전 칸
  60%는 다음 칸
```

현재 코드베이스에서도 이 철학은 이미 반영되어 있다.

- `TickPresentationData`는 render-only metadata다.
- `GameplayTickViewPresenter`는 committed state와 motion track을 분리한다.

즉, 적 AI도 이 규칙을 그대로 따라야 한다.

## 19. 상태 타이머와 phase state 사용

현재 `EntityState`에는 이미 다음 값이 존재한다.

- `state`
- `stateTimer`

이는 적 AI의 공격 후 회복, 이동 불가 시간, wind-up, stun 같은 간단한 제약을 표현하는 데 활용할 수 있다.

권장 규칙:

- 일반 행동 가능 상태는 `Idle`
- 공격 직후 또는 연속행동 제한은 `Acting`, `Cooldown`
- 이동 특수 상태는 별도 rule이 필요할 때만 추가

중요:

- `stateTimer`는 로직 제어값이다.
- "애니메이션이 아직 안 끝났으니 timer를 늘린다" 같은 역방향 연결은 금지한다.

## 20. 초기 구현 권장안

초기 버전은 과도한 범용 FSM 프레임워크를 만들지 않는다.

권장 구현 순서는 다음과 같다.

1. `EnemyAiMode` enum 정의
2. `EnemyLogic` 구현
3. `EnemyEntityLogicFactory` 또는 `StaticEntityLogics` 등록 경로 추가
4. `EntityState` 또는 별도 blackboard에 FSM 지속 상태 추가
5. `EnemyAiConfig`로 감지 범위, 공격 범위, cooldown 설정 분리
6. `EnemyPresentationState` 또는 mapper 작성
7. View에 `EnemyAnimatorDriver` 추가

초기 목표는 다음 적 하나를 완성하는 것이다.

- `Patrol -> Chase -> Attack -> Recover` 근접 적

그 다음 변형 순서는 다음이 적절하다.

- 배회 전용 적
- 충돌 피해 적
- 고정 포탑
- 자폭 적

## 21. 샘플 설계

### 21-1. 근접 적

```text
State:
  Patrol
  Chase
  Attack
  Recover

Transition:
  Patrol -> Chase if target detected
  Chase -> Attack if in range
  Chase -> Patrol if target lost
  Attack -> Recover after attack intent issued
  Recover -> Chase when cooldown done
```

### 21-2. 충돌 피해 슬라임

```text
State:
  Patrol
  Chase

Policy:
  ContactDamagePolicy

No:
  AttackState
  Attack animation authority
```

### 21-3. 무지성 배회 유닛

```text
State:
  Patrol

Policy:
  RandomPatrolMovementPolicy
```

## 22. 테스트 기준

적 AI FSM은 아래 수준에서 검증해야 한다.

### 22-1. EditMode 단위 테스트

- 타겟 미감지 시 `Patrol` 유지
- 감지 시 `Chase` 전이
- 사거리 진입 시 attack intent 생성
- cooldown 중 attack intent 미생성
- 타겟 상실 시 `Patrol` 복귀
- 배회 전용 유닛이 절대 `Chase`로 가지 않음
- 접촉 피해 유닛이 attack animation 없이 피해를 적용

### 22-2. Replay / Determinism 테스트

- 동일한 initial state와 input에서 같은 Tick 결과 보장
- AI blackboard 변화가 determinism hash에 반영
- view 유무와 관계없이 Tick 결과 동일

### 22-3. Presentation 테스트

- motion track이 committed cell을 오염시키지 않음
- attack/hit/death trigger가 로직 판정을 바꾸지 않음

## 23. 안티 패턴

다음 구조는 채택하지 않는다.

- `EnemyType` enum 하나로 모든 적 행동을 거대 switch 문에서 처리
- 모든 적이 `Patrol/Chase/Attack`를 반드시 가지는 구조
- AI 상태를 `MonoBehaviour` private field에만 저장
- 데미지를 animation event에서 authoritative 적용
- 이동 중간 보간 좌표를 충돌 판정에 사용
- Animator state name으로 FSM 전이

## 24. 최종 결론

이 프로젝트에서 적 AI FSM의 올바른 구조는 다음이다.

```text
EnemyLogic(FSM)
  -> intent production
  -> authoritative state update
  -> TickPresentationData
  -> EnemyView / Animator
```

핵심 원칙은 세 줄로 요약된다.

- Logic은 authoritative truth만 다룬다.
- View는 지연된 시각 표현만 다룬다.
- 적 변형은 상태 집합과 policy 조합으로 만든다.

이 원칙을 지키면 다음 적군을 모두 같은 구조로 수용할 수 있다.

- 배회 전용 적
- 추적 적
- 근접 공격 적
- 충돌 피해 적
- 자폭 적
- 고정 포탑

즉, 예외적 적군 생성은 어려워지지 않는다. 오히려 FSM과 View를 분리할수록 쉬워진다.
