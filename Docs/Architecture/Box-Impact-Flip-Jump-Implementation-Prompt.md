# Implementation Prompt: Box Impact / Flip Failure / Enemy Jump

아래 프롬프트는 현재 `Assets/_Features/Gameplay` 코드베이스에 `Push / Sliding Push / Flip`의 enemy impact 규칙과 `Enemy Jump` 상호작용 규칙을 구현하기 위한 작업 지시문이다.

---

당신은 이 저장소의 gameplay simulation 코드를 수정하는 시니어 엔지니어다.

## 목표

현재 구조의 핵심 invariant는 유지한다.

- `Unit + Unit` same-cell 허용
- `Unit + Box` same-cell 금지
- `Box`는 끝까지 solid entity

이 전제를 깨지 말고, 아래 gameplay 규칙을 구현하라.

### 구현할 최종 규칙

1. `Push` 또는 `Sliding Push`가 적 유닛 칸으로 향하면 `Impact + Stop`
2. `Flip`이 적 유닛 landing 칸으로 향하면 `Impact + No Relocation`
3. 적이 죽어도 같은 tick에는 box가 그 칸으로 들어가지 않는다.
4. `Flip`은 wind-up을 그대로 유지하고, 실패 시 full return animation은 추가하지 않는다.
5. `Enemy Jump Windup`은 source cell 점유 유지
6. `Enemy Jump Airborne`은 detached, untargetable, non-occupying
7. `Enemy Jump Landing`은 unit 규칙으로 same-cell landing 허용, box/wall/terrain은 fallback 또는 retry

## 산출물

이번 작업의 완료 조건은 아래 세 가지다.

1. gameplay simulation 코드가 위 규칙대로 동작한다.
2. edit mode 테스트가 회귀를 고정한다.
3. 문서와 presentation signal이 authoritative truth와 모순되지 않는다.

## 절대 금지

- `Unit + Box` overlap 허용으로 문제를 해결하지 말 것
- `Flip 실패`를 authoritative teleport 후 return으로 구현하지 말 것
- cleanup 이전에 죽은 target cell로 box를 전진시키지 말 것
- jump에 box 파괴나 stomp 의미를 임의로 추가하지 말 것
- `적이 안 죽으면 box 파괴`를 기본 규칙으로 넣지 말 것

이 마지막 항목은 의도적이다.

- survival 여부는 attack phase 누적 결과 이후에야 안정적으로 결정된다.
- baseline 구현은 `impact + no overlap`이며, conditional destroy fallback은 이번 작업 범위가 아니다.

## 구현 전략

`projectile impact`와 동일한 철학을 사용하라.

- movement phase가 synthetic impact reservation을 생성
- attack phase가 synthetic impact를 damage action으로 확장
- cleanup phase가 죽은 entity를 제거

단, projectile 전용 하드코딩을 그대로 복붙하지 말고, box impact를 병렬 지원하도록 구조를 확장하라.

권장 구현 순서는 아래와 같다.

1. query와 data model을 확장한다.
2. movement expansion에서 `Push / Sliding Push / Flip` impact group을 만든다.
3. movement commit에서 generic synthetic impact reservation을 생성한다.
4. attack expansion이 box impact를 damage로 확장하게 한다.
5. presentation을 outcome 기반으로 최소 수정한다.
6. jump regression 테스트를 고정한다.

## 필요한 코드 수정

### 1. box kinetic ownership 문맥 추가

`EntityState`에 box sliding 동안 사용할 kinetic ownership 문맥을 추가하라.

권장 필드:

- `kineticInstigatorEntityId`
- `kineticInstigatorTeamId`

의미:

- player가 push/flip으로 box를 작동시킨 순간의 owner 정보
- sliding 동안 유지
- neutral static box의 기본 team과 구분되는 kinetic context

이 필드 추가에 따라 아래도 함께 갱신하라.

- determinism hash
- tick trace / debug dump
- replay / fuzz dump
- entity factory helper 및 테스트 data builder

대상 후보 파일:

- `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/EntityState.cs`
- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/DeterminismHashBuilder.cs`
- `Assets/_Features/Gameplay/Gameplay_Debug/Runtime/TickTraceFormatter.cs`
- 관련 replay / fuzz 테스트 유틸

만약 위 부가 경로가 현재 저장소에 존재하지 않거나 이미 다른 방식으로 deterministic dump를 구성하고 있다면, 동일한 책임을 가진 실제 파일에 반영하라.

### 2. hostile-only unit impact target query 추가

현재 `TryPickImpactTargetAt`는 hostile fallback 규칙이 projectile 용도에 맞춰져 있다.
box impact에서는 `hostile unit only`를 명시적으로 고르도록 query를 분리하라.

새 query 요구사항:

- same cell의 stacked unit 중 hostile만 후보로 본다.
- entityId 오름차순으로 결정론적 선택
- hostile unit이 없으면 `false`
- solid occupant를 impact target으로 선택하지 않는다.
- `Detached`, inactive face, non-selectable entity는 기존 gameplay query 정책과 동일하게 제외한다.

대상 후보 파일:

- `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/Queries/SnapshotReadQueries.cs`
- `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldSnapshot.cs`

### 3. movement expansion에 box impact 경로 추가

아래 세 경로에 enemy impact를 추가하라.

- `TryExpandPush`
- `ExpandSlidingPushBoxMove`
- `ExpandFlip`

세부 요구사항:

- box next cell / landing cell이 hostile unit cell이면 `MoveAction`을 만들지 말 것
- 대신 synthetic impact reservation을 만들 수 있는 action group을 생성할 것
- sliding 중 impact가 발생한 경우 box는 stop state로 종료할 것
- flip impact가 발생한 경우 box는 source cell에 남을 것
- wall / box / terrain blocker는 기존처럼 impact 없이 blocked 처리할 것
- hostile unit과 friendly unit이 stacked 되어 있으면 hostile을 우선 맞출 것
- hostile unit이 전혀 없고 friendly unit만 있으면 impact를 만들지 말고 blocked 취급할 것

권장 구현:

- `ActionGroupKind.BoxImpact` 추가
- `ActionGroup`에 generic impact target assignment를 추가하거나 box impact target 저장 API를 추가
- `MovementCommitter`가 `BoxImpact` group을 만나면 transient impact reservation을 생성
- `ProjectileImpact`와 달리 `BoxImpact`는 state changes를 계속 commit할 수 있게 할 것

실제 행동 결과는 아래 셋 중 하나로 정규화하라.

- `SuccessMove`
- `ImpactNoMove`
- `BlockedNoImpact`

대상 후보 파일:

- `Assets/_Features/Gameplay/Gameplay_Model/Runtime/Groups/ActionGroupKind.cs`
- `Assets/_Features/Gameplay/Gameplay_Model/Runtime/Groups/ActionGroup.cs`
- `Assets/_Features/Gameplay/Gameplay_Movement/Runtime/Expansion/MovementExpander.cs`
- `Assets/_Features/Gameplay/Gameplay_Movement/Runtime/Commit/MovementCommitter.cs`

### 4. box impact damage source 규칙

impact reservation source는 `box entity`를 사용하라.

단, target hostile 판정에는 `box.kineticInstigatorTeamId`를 사용하라.

의도:

- sliding chain에서도 같은 hostile 기준이 유지된다.
- projectile처럼 source entity가 제거되어야 하는 구조가 아니므로, box impact는 source self-damage를 만들지 않는다.

damage amount는 1로 시작하되, 상수는 projectile과 분리하라.

예:

- `ProjectileImpactDamageAmount = 1`
- `BoxImpactDamageAmount = 1`

box impact는 projectile과 달리 source self-damage를 만들지 않는다.

### 5. push / flip 성공 시 kinetic context 기록

player가 push/flip을 성공적으로 실행해 box를 작동시킨 경우 box에 kinetic owner를 기록하라.

규칙:

- `Push` 성공 시 target box에 owner 기록
- `Flip` 성공 시 target box에 owner 기록
- `Sliding Push`는 기존 owner 유지
- `Stop` 또는 idle settle에서 owner를 유지할지 clear할지는 문서 주석으로 명확히 결정할 것

권장:

- sliding 종료 후에도 마지막 kinetic owner를 유지해도 gameplay 문제는 없지만,
- 새 push/flip 시 항상 overwrite되게 하라.

핵심은 sliding chain 전체가 동일한 적대 판정 기준을 유지하는 것이다.

### 6. flip execute failure를 cancel이 아니라 executed failure로 유지

현재 presentation signal은 `Started / Executed / Completed / Canceled`다.
이번 작업에서는 authoritative `PlayerActionKind`를 늘리지 말고, `FlipFail` 같은 새 action kind도 추가하지 말라.

대신 아래 규칙을 구현하라.

- wind-up이 시작되면 `FlipWindup`
- execute tick에 success move면 기존 `FlipRecovery`
- execute tick에 `ImpactNoMove`여도 `ExecutedThisTick = true`, `FlipRecovery`
- execute tick에 `BlockedNoImpact`여도 `ExecutedThisTick = true`, `FlipRecovery`
- pre-execute invalidation만 `CanceledThisTick`

즉, execute tick에 outcome이 나왔다면 return animation 없이 recovery로 간다.

authoritative `PlayerActionKind`는 기존 `None / Push / Flip`만 유지하라.

### 7. presentation은 full return이 아니라 rebound

full return animation을 추가하지 말고, 필요하면 render-only outcome 기반 반동 표현만 추가하라.

허용:

- player `FlipRecovery` 유지
- box in-place recoil / tilt / shake
- enemy hit reaction

비허용:

- box가 landing cell까지 갔다가 source로 돌아오는 authoritative 또는 presentation track

만약 presentation outcome 분기가 필요하면 render-only metadata를 추가하라.

예:

- `BoxInteractionOutcome.SuccessMove`
- `BoxInteractionOutcome.ImpactNoMove`
- `BoxInteractionOutcome.BlockedNoImpact`

이 값은 `TickPresentationData` 또는 movement presentation data에만 존재해야 한다.

즉, outcome은 렌더링 해석용이고 gameplay truth가 아니다.

### 8. enemy jump 규칙은 유지하되 regression 보호

jump는 아래 규칙을 깨지 말라.

- windup 중 source 점유 유지
- airborne 중 detached / untargetable
- locked target unit cell landing 허용
- locked target box cell landing 불가, fallback
- legal cell 없으면 retry
- landing tick에는 movement 억제 가능하지만 attack는 기존 규칙 유지

box impact와 jump의 상호작용은 아래로 고정하라.

- windup enemy는 impact 대상
- airborne enemy는 impact 대상 아님
- just-landed enemy는 일반 unit처럼 impact 대상

이 규칙을 만족시키기 위해 jump landing 순서나 cleanup 순서를 바꾸지 말라.

## 테스트 요구사항

반드시 edit mode 테스트를 추가하거나 갱신하라.

### movement / attack

1. `Push_BoxNextStepHasHostileUnit_CreatesImpactAndStopsBeforeUnitCell`
2. `SlidingPush_BoxHitsHostileUnit_CreatesImpactAndStopsSliding`
3. `Flip_LandingHasHostileUnit_CreatesImpactAndBoxRemainsAtSource`
4. `Flip_LandingHasWallOrBox_IsBlockedWithoutImpact`
5. `Impact_KillsEnemy_BoxStillDoesNotAdvanceSameTick`
6. `Impact_TargetCellHasFriendlyOnly_DoesNotDamageFriendly`
7. `Impact_TargetCellHasStackedFriendlyAndHostile_PicksHostileDeterministically`

### player presentation

8. `FlipImpactFailure_StillEntersRecoveryWithoutReturnTrack`
9. `FlipBlockedFailure_StillEntersRecoveryWithoutReturnTrack`
10. `FlipPreExecuteInvalidation_CancelsBeforeExecute`

### enemy jump

11. `JumpWindupEnemy_CanBeHitByBoxImpact`
12. `JumpAirborneEnemy_IsIgnoredByBoxImpact`
13. `JumpLandingThenBoxImpact_SameTick_UsesLandedOccupancy`
14. `JumpLandingOnUnitCell_StillAllowedAfterNewImpactRules`
15. `JumpLandingOnBoxCell_StillFallsBackOrRetries`

## 검증 메모

구현 후 아래를 확인하라.

- `Attack_DeadAfterDamage_StillOccupiesUntilCleanup` 성질이 유지되는가
- `EnemyAi_JumpWindup_KeepsSourceCellOccupied` 성질이 유지되는가
- `EnemyAi_JumpAirborne_SetsDetached_AndBecomesUntargetable` 성질이 유지되는가
- `EnemyLogic_JumpLandingTick_SuppressesMovementButNotAttack` 성질이 유지되는가

## 최종 원칙

이 작업의 정답은 `Unit + Box overlap`이 아니라 `kinetic impact + legal occupancy 유지`다.

### determinism / trace

16. kinetic owner fields가 determinism hash에 반영되는지 검증
17. trace / replay dump에 kinetic owner와 box impact event가 안정적으로 노출되는지 검증

## 문서 반영

코드 변경과 함께 아래 문서의 사실을 깨지 않도록 맞추고, 필요한 경우 링크나 주석을 추가하라.

- `Docs/Architecture/Box-Impact-Flip-Jump-Rulebook.md`
- `Docs/Architecture/UnitOverlap_ImplementationPlan.md`
- `Docs/Architecture/Player-Action-Windup-Presentation-Blueprint.md`

## 최종 산출물 요건

- 코드가 현재 tick order와 occupancy invariant를 깨지 않아야 한다.
- `Unit + Box` overlap을 허용하지 않아야 한다.
- `Flip impact`는 damage만 주고 box는 source cell에 남아야 한다.
- `Enemy Jump` 기존 fallback/retry 규칙이 유지되어야 한다.
- full return animation 없이도 failure presentation이 읽히게 해야 한다.

작업 후에는 어떤 테스트를 추가/수정했고, 어떤 규칙을 어디에 구현했는지 요약하라.

---

이 프롬프트의 기준 규칙은 `Docs/Architecture/Box-Impact-Flip-Jump-Rulebook.md`를 authoritative 문서로 삼는다.
