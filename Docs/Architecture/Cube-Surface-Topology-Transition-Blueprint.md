# Cube Surface Topology Transition Blueprint

## 1. 목적

이 문서는 플레이어가 활성 바닥면 경계를 넘어 `CubeTopologyState`가 바뀌는 순간에, 면 회전 애니메이션을 안전하게 도입하기 위한 최종 설계 기준을 정의한다.

핵심 목표는 다음 세 가지다.

- 기존 이동 애니메이션과 면 회전 애니메이션이 서로 충돌하지 않게 한다.
- 회전 중 다른 엔티티의 시각 처리 규칙을 명확히 고정한다.
- 회전 후 다음 이동이 새 topology 기준으로 일관되게 재개되도록 한다.

이 문서는 아래 문서를 전제로 한다.

- `Docs/Architecture/Cube-Surface-Gameplay-Blueprint.md`
- `Docs/Architecture/Cube-Surface-Gameplay-Implementation-Plan.md`
- `Docs/Architecture/Cube-Surface-3D-Presentation-Blueprint.md`
- `Docs/Architecture/Cube-Surface-3D-Presentation-Implementation-Plan.md`

## 2. 설계 결론

채택 방향은 단순하다.

- 면 회전은 `ordinary move`의 연장이 아니라 `Topology Transition`이라는 별도 전환 단계로 취급한다.
- topology transition은 프레젠테이션 기준 `barrier`다.
- 기존 이동 애니메이션이 끝나기 전에는 회전이 시작되지 않는다.
- 회전이 시작되면 끝나기 전까지 다음 tick은 실행되지 않는다.
- 직접 움직인 엔티티만 local motion clip을 갖고, 나머지 엔티티는 board rotation에만 탑승한다.
- authoritative state는 즉시 새 topology로 commit하되, presentation이 source/destination의 시각 차이를 흡수한다.

이 문서 전체는 이 결론을 코드 기준으로 풀어 쓴 것이다.

## 3. 현행 구조 요약

현재 구현은 이미 topology rotation의 최소 뼈대를 가지고 있다.

- 플레이어가 `BottomFace` 상단 또는 하단 경계를 넘어가면 topology가 변경된다.
- movement group은 `TopologyChangeAction`을 포함할 수 있다.
- commit 단계는 topology를 즉시 authoritative world state에 반영한다.
- presenter는 `TickTopologyMotion`을 사용해 board root를 회전시킨다.
- 엔티티 뷰는 `GameplayBoardRoot`의 자식이므로 board rotation에 자동으로 탑승한다.

즉, 이번 설계의 본질은 "회전 기능 추가"보다 "회전과 다른 motion의 경계를 명확히 정의"하는 데 있다.

## 4. 현재 문제

현재 구조에서 남아 있는 문제는 다음 네 가지다.

### 4-1. 기존 local motion과 topology rotation의 기준 좌표가 섞일 수 있다

ordinary move, push, flip의 local motion clip이 남아 있는 상태에서 topology rotation이 시작되면, 이전 topology 기준 local pose와 새로운 topology 기준 board rotation이 같은 프레임에서 섞일 수 있다.

이 경우 결과는 아래 셋 중 하나가 된다.

- 위치 점프
- 이중 회전
- 잘못된 start/end pose 보간

### 4-2. 회전 중에도 다음 tick이 실행될 수 있다

현재 `GameplayInputHost`는 presenter의 진행 상태를 보지 않고 tick을 계속 실행할 수 있다. 따라서 topology rotation이 재생되는 동안 다음 movement tick이 들어갈 여지가 있다.

### 4-3. topology-changing tick이 다른 movement tick과 같은 프레임에 섞일 수 있다

현재 movement resolver는 topology-changing group과 일반 move/push/flip group의 동시 선택을 명시적으로 금지하지 않는다.

### 4-4. 회전 시작 프레임에 source face 엔티티가 바로 사라질 수 있다

현재 visible entity set은 committed topology 기준으로만 계산된다. 따라서 source topology에서 보이던 엔티티가 회전 시작 시점에 곧바로 pop-out될 수 있다.

## 5. 설계 원칙

이번 설계는 아래 원칙을 고정한다.

1. `WorldState`는 항상 authoritative하다.
2. topology 변경은 world state에는 즉시 반영한다.
3. 프레젠테이션은 authoritative state를 늦추지 않고, 표시만 지연 또는 보정한다.
4. topology transition은 ordinary motion 위에 겹쳐 재생하지 않는다.
5. transition 중 렌더링 규칙은 gameplay query 규칙과 분리한다.
6. 테스트는 반드시 "회전 중"과 "회전 종료 직후"를 따로 검증해야 한다.

## 6. 용어 정의

### 6-1. Ordinary Motion

아래 motion을 뜻한다.

- `Move`
- `Push`
- `Flip`
- `ProjectileMove`

### 6-2. Topology Transition

아래 둘을 하나의 전환 단계로 본다.

- `TickTopologyMotion`
- transition 동안 유지되는 visibility 보정

### 6-3. Directly Moved Entity

해당 tick의 `ActionGroup.Moves`에 직접 포함된 엔티티를 뜻한다.

### 6-4. Stationary Passenger

해당 tick에 직접 움직이지는 않지만, source active faces 또는 destination active faces 위에 존재하여 회전 중 함께 보여야 하는 엔티티를 뜻한다.

## 7. 최종 상태 모델

프레젠테이션 상태는 다음 세 단계로 정의한다.

- `Idle`
- `EntityMotion`
- `TopologyTransition`

전이 규칙은 다음과 같다.

- local motion clip 또는 visibility clip만 있으면 `EntityMotion`
- topology rotation clip이 있으면 `TopologyTransition`
- 아무 clip도 없으면 `Idle`

우선순위는 다음과 같다.

1. `TopologyTransition`
2. `EntityMotion`
3. `Idle`

즉, topology rotation이 존재하면 그 순간 presenter의 대표 상태는 항상 `TopologyTransition`이다.

## 8. 사용자 체감 흐름

최종 동작 흐름은 아래 순서로 고정한다.

1. 이전 ordinary motion이 끝난다.
2. topology-changing tick이 실행된다.
3. authoritative state는 즉시 destination topology로 commit된다.
4. presenter는 source orientation에서 destination orientation으로 board root를 회전시킨다.
5. 회전 시작 프레임부터 destination topology 기준 visible set만 보여 주면서 시각 전환을 유지한다.
6. transition 종료 후 destination topology 기준 visible set만 남긴다.
7. 그 다음 tick부터 새 topology 기준 이동을 재개한다.

이 흐름을 깨는 설계는 채택하지 않는다.

## 9. 입력 및 tick 실행 정책

### 9-1. 1차 채택 정책

안전한 1차 정책은 다음과 같다.

- presenter가 `Idle`일 때만 새 tick 실행을 허용한다.
- `EntityMotion` 중에도 tick을 막는다.
- `TopologyTransition` 중에는 반드시 tick을 막는다.

이 정책은 가장 보수적이지만, 기준 좌표 충돌과 catch-up burst를 가장 쉽게 제거한다.

### 9-2. 입력 버퍼 정책

- raw move input은 막힌 동안에도 계속 최신값으로 갱신한다.
- `Push`와 `Flip`은 막힌 동안 buffer를 유지한다.
- unlock 뒤 첫 유효 tick에서 buffered interaction을 정상 소비한다.

### 9-3. accumulated time 정책

presentation lock 중에는 accumulated simulation time이 쌓여 unlock 직후 여러 tick이 한 번에 실행되면 안 된다.

따라서 input host는 아래 둘 중 하나를 반드시 보장해야 한다.

- lock 중 accumulated time을 소비하지 않고 clamp한다.
- 또는 lock 중 simulation advance 자체를 멈춘다.

어느 방식을 택하든 결과는 동일해야 한다.

- unlock 직후 tick burst 없음
- 정확히 다음 tick부터 재개

## 10. movement resolver 정책

topology-changing tick은 해당 tick의 독점 그룹으로 처리한다.

정책은 다음과 같다.

- topology change를 포함한 group이 선택되면 같은 tick의 다른 movement group은 모두 reject한다.
- 일반 movement group이 먼저 선택되었으면 뒤에 오는 topology-changing group도 reject한다.
- 하나의 tick에는 최대 1개의 topology-changing group만 허용한다.

이 규칙을 도입하면 topology-changing tick은 의미상 "전환 tick"이 되며, ordinary motion과 논리적으로 분리된다.

## 11. authoritative state와 presentation state의 역할 분리

이번 설계는 authoritative mutation을 늦추지 않는다.

즉, topology-changing tick에서 world state는 즉시 아래 기준으로 갱신된다.

- entity final cell
- entity final facing
- final topology

반면 presentation은 아래 역할만 수행한다.

- source topology에서 보이던 것을 잠시 유지
- destination topology에서 보여야 할 것을 적절히 노출
- board root rotation으로 시각적 전환 제공

이 설계가 중요한 이유는 deterministic tick logic을 흐리지 않기 때문이다.

## 12. 엔티티 분류와 처리 규칙

회전 중 엔티티는 아래 세 부류로 처리한다.

### 12-1. 직접 이동 엔티티

해당 tick의 `MoveAction`에 포함된 엔티티다.

처리:

- 기존처럼 local motion clip을 가진다.
- source pose는 source topology 기준이다.
- end pose는 destination topology 기준 committed pose다.

예:

- 경계를 넘어 이동한 플레이어
- topology-changing tick에 직접 포함된 이동 엔티티

### 12-2. 정지 승객 엔티티

해당 tick에 직접 이동하지 않지만 transition 동안 화면에 보여야 하는 엔티티다.

처리:

- local motion clip을 만들지 않는다.
- board rotation에만 탑승한다.
- destination topology 기준 visible set에 들어오면 즉시 show 처리한다.
- source-only가 되면 transition 시작 프레임부터 숨긴다.

예:

- source bottom face 위에 남아 있는 박스
- destination front face에 이미 존재하던 엔티티

### 12-3. 비관련 엔티티

source active faces와 destination active faces 어디에도 속하지 않는 엔티티다.

처리:

- transition 동안 렌더 대상에 포함하지 않는다.

## 13. 회전 중 visible set 정책

transition 중 렌더 대상은 원칙적으로 아래 집합이다.

- `Destination Active Faces`

이를 엔티티 단위로 나누면 다음 세 경우가 생긴다.

### 13-1. source-only visible

source topology에서는 보이지만 destination topology에서는 보이지 않는 엔티티다.

정책:

- transition 시작 시점에 즉시 hide한다.

### 13-2. destination-only visible

destination topology에서만 보이는 엔티티다.

정책:

- transition 시작 시점부터 즉시 노출한다.
- 필요하면 이후 threshold reveal로 미세 조정한다.

### 13-3. both visible

source와 destination 모두에서 보이는 엔티티다.

정책:

- transition 시작 시점부터 destination topology 기준 의미를 따른다.
- face/entity role, material, visibility semantics 모두 destination 기준이다.

## 14. projection 정책

현재 gameplay entity projection은 활성 2면 전용이다. topology transition에는 presentation 전용 projection 경로가 필요하다.

새 projection API는 다음 요구를 만족해야 한다.

- gameplay query와 분리된 presentation 전용이다.
- active 2면이 아니어도 transition 중 필요한 면은 투영 가능해야 한다.
- stationary passenger의 local pose를 계산할 수 있어야 한다.
- ordinary 상태에서는 기존 projection 결과와 동일해야 한다.

이로써 "게임플레이에서는 비활성 face지만 transition 중에는 보여야 하는 엔티티"를 안전하게 처리할 수 있다.

## 15. TickPresentationData 확장

현재 `TickPresentationData`는 아래 세 축을 가진다.

- entity motions
- topology motion
- visibility changes

topology transition 설계를 위해 여기에 전환용 visibility 메타데이터를 추가한다.

필요한 정보는 다음과 같다.

- entity id
- transition 시작 시 보여 줄지 여부
- destination topology 기준 transition-space pose 복원에 필요한 정보

설계상 `Spawn`, `Detach`, `Remove`와 topology transition start-show는 의미가 다르므로, 별도 전용 struct를 추가하는 쪽을 우선 채택한다.

## 16. TickResultBuilder 정책

`TickResultBuilder`는 topology change가 있는 tick에서 transition visibility 데이터를 생성해야 한다.

생성 규칙은 다음과 같다.

- post-movement committed set에 새로 나타나는 엔티티는 `show-at-transition-start`
- 직접 이동 엔티티는 기존 entity motion 처리 우선
- `Spawn`, `Detach`, `Remove`와 transition visibility가 충돌하면 기존 visibility 우선순위를 유지

즉, transition visibility는 ordinary visibility를 덮어쓰는 별도 규칙이 아니라, ordinary visibility에 보조적으로 붙는 렌더 규약이다.

## 17. GameplayTickViewPresenter 책임 변경

presenter는 아래 책임을 추가로 가진다.

### 17-1. 상태 노출

외부가 아래를 읽을 수 있어야 한다.

- 현재 presentation phase
- topology transition 진행 여부
- active presentation 여부

### 17-2. topology transition 중 processing entity set 확장

현재 committed visible set만으로는 부족하므로 아래를 합쳐야 한다.

- committed local target poses
- retained local target poses
- transition start show 대상

### 17-3. stationary passenger 처리

정지 승객 엔티티는 local motion clip 없이 fallback pose만 유지한다.

즉:

- 움직이지 않는다
- 보드와 함께 돈다
- transition 끝나면 committed visibility 규칙으로 정리된다

### 17-4. transition 종료 정리

transition이 끝나면 아래를 정리해야 한다.

- transition visibility 상태 제거
- destination topology 기준 visible set만 유지

## 18. board surface 처리 정책

1차 설계에서는 `GameplayBoardSurfaceRenderer`의 구조 변경을 최소화한다.

채택 정책은 다음과 같다.

- final topology 기준 surface는 즉시 refresh한다.
- board root는 start rotation에서 identity로 돌아오므로, 표면도 회전 과정 속에서 source orientation처럼 보인다.

따라서 1차 구현에서는 surface renderer를 별도 source/destination 이중화하지 않는다.

## 19. timing 정책

현재 topology rotation은 push duration과 같은 시간을 사용한다.

이번 설계에서는 별도 timing field를 도입한다.

- `TopologyMotionDurationSeconds`

정책:

- 기본값은 기존 push duration과 동일
- 추후 ordinary motion과 독립 조정 가능

이 변경은 회전 연출을 ordinary push보다 느리게 또는 빠르게 조절할 수 있게 해 준다.

## 20. 구현 단계

권장 구현 순서는 아래와 같다.

### 20-1. 1단계: presenter 상태 API 추가

목표:

- 현재 presentation 진행 여부를 외부에 노출

완료 기준:

- 초기 false
- ordinary motion 중 true
- topology transition 중 true
- 종료 후 false

### 20-2. 2단계: input lock과 burst 방지

목표:

- active presentation 중 tick 금지
- unlock 뒤 burst 금지

완료 기준:

- lock 중 `NextTickIndex` 증가 없음
- unlock 뒤 정확히 한 tick부터 재개

### 20-3. 3단계: resolver topology 독점 규칙

목표:

- topology-changing tick의 독점 보장

완료 기준:

- 같은 tick의 일반 move/push/flip과 공존 불가

### 20-4. 4단계: topology motion duration 분리

목표:

- topology 전용 duration 도입

완료 기준:

- presenter가 새 timing 값을 사용

### 20-5. 5단계: transition visibility data 추가

목표:

- source-only, destination-only 엔티티 표시 규칙 표현

완료 기준:

- TickPresentationData가 전환 visibility 정보를 담을 수 있음

### 20-6. 6단계: TickResultBuilder 확장

목표:

- topology-changing tick에서 전환 visibility 생성

완료 기준:

- destination-start show 데이터 생성 테스트 통과

### 20-7. 7단계: projector transition projection 추가

목표:

- transition 중 active 2면 외 face도 projection 가능

완료 기준:

- transition visible entity를 local pose로 계산 가능

### 20-8. 8단계: presenter transition 렌더링 적용

목표:

- destination topology visible set 즉시 적용
- stationary passenger board-tether 처리

완료 기준:

- 회전 시작 시 엔티티 pop 없음
- 종료 시 최종 visible set 정리 완료

### 20-9. 9단계: 테스트 보강

목표:

- 회전 중과 회전 후를 분리 검증

완료 기준:

- edit/play mode 테스트 모두 통과

## 21. 테스트 전략

테스트는 EditMode와 PlayMode로 나눈다.

### 21-1. EditMode 검증 항목

- presenter state API 정확성
- topology rotation 중 board root orientation 변화
- source-only visible 엔티티 immediate hide
- stationary passenger의 board rotation 탑승
- transition 종료 후 destination-only visible set 유지

### 21-2. PlayMode 검증 항목

- presentation active 동안 tick 미실행
- buffered push/flip 유실 없음
- unlock 직후 tick burst 없음
- transition 종료 후 다음 이동이 새 topology 기준으로 실행됨

## 22. 비채택안

이번 단계에서 채택하지 않는 방향은 다음과 같다.

- ordinary motion clip이 끝나기 전에 topology rotation을 겹쳐 시작
- topology transition 중 다음 tick 허용
- transition 중 모든 엔티티에 별도 local motion clip 부여
- world state commit 자체를 transition 종료까지 지연

이 비채택안들은 모두 복잡도 대비 이득이 작고, deterministic presentation contract를 불안정하게 만든다.

## 23. 리스크와 후속 과제

남는 리스크는 다음과 같다.

- destination-only 엔티티의 노출 타이밍은 아트 요구에 따라 즉시 노출보다 threshold reveal이 더 적합할 수 있다.
- 1차 구현의 global presentation lock은 안전하지만 다소 보수적이다.

후속 과제는 다음과 같다.

- `HasActivePresentation`과 `HasActiveTopologyTransition` 분리
- ordinary motion 중 다음 tick 허용 여부 재검토
- destination-only reveal threshold 조정
- camera easing 강화 여부 검토

## 24. 결론

이번 설계의 핵심은 아래 문장으로 요약된다.

> 면 회전은 ordinary motion 위에 겹치는 이동이 아니라, 기존 motion을 정리한 뒤 실행되고 완료 후 새 topology 기준으로 게임을 재개하는 전환 단계다.

이 원칙을 따르면 회전 중 다른 엔티티의 처리는 단순해진다.

- 직접 움직인 엔티티만 local motion을 가진다.
- 나머지는 board rotation에 탑승한다.
- visible set 해석은 transition 시작 프레임부터 destination topology를 따른다.

즉, 논리와 연출의 경계가 명확해지고, 테스트 가능한 계약으로 정리된다.
