# Cube Surface Topology Transition Implementation Plan

## 1. 목적

이 문서는 `Docs/Architecture/Cube-Surface-Topology-Transition-Blueprint.md`를 현재 Unity C# 구현에 내리기 위한 실행 계획서다.

목표는 다음 세 가지를 코드 레벨에서 안전하게 고정하는 것이다.

- topology transition을 ordinary motion과 분리된 presentation barrier로 만든다.
- topology rotation 중 입력과 tick 실행을 잠가 좌표계 충돌과 burst를 제거한다.
- transition 중 source/destination visible set 차이를 presentation이 흡수하도록 만든다.

## 2. 전제 문서

- `Docs/Architecture/Cube-Surface-Topology-Transition-Blueprint.md`
- `Docs/Architecture/Cube-Surface-Gameplay-Blueprint.md`
- `Docs/Architecture/Cube-Surface-Gameplay-Implementation-Plan.md`
- `Docs/Architecture/Cube-Surface-3D-Presentation-Blueprint.md`
- `Docs/Architecture/Cube-Surface-3D-Presentation-Implementation-Plan.md`

## 3. 현재 코드 기준 요약

현재 구현은 topology rotation의 최소 뼈대를 이미 가지고 있다.

- authoritative world state는 topology change를 즉시 commit한다.
- `TickResultBuilder`는 topology change가 있으면 `TickTopologyMotion`을 생성한다.
- `GameplayTickViewPresenter`는 board root rotation track으로 topology motion을 재생한다.
- entity view는 `GameplayBoardRoot` 하위이므로 board rotation에 자동으로 탑승한다.

현재 남아 있는 핵심 결손은 다음과 같다.

- `GameplayCubeProjector`는 gameplay-visible 2면만 projection하며 transition 전용 projection 경로가 없다.
- `GameplayTickViewPresenter`가 destination-topology-only transition visibility metadata를 아직 소비하지 않아 destination-only show와 stationary passenger board-tether를 최종 렌더링에 반영하지 않는다.

## 4. 구현 원칙

- `WorldState`는 끝까지 authoritative하다.
- topology change는 commit을 지연하지 않는다.
- presentation은 authoritative state를 늦추지 않고 표시만 보정한다.
- topology transition은 ordinary motion 위에 겹쳐 재생하지 않는다.
- transition 중 rendering 규칙은 gameplay query 규칙과 분리한다.
- 테스트는 반드시 `회전 중`과 `회전 종료 직후`를 분리해서 검증한다.

## 5. 작업 단계

### 5-1. 1단계: presenter 상태 API 추가

목표:

- presenter가 현재 presentation 상태를 외부에 노출하도록 만든다.

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/RuntimeBoardBoundsGuardTests.cs`

구현 태스크:

- `GameplayPresentationPhase` enum 추가
  - `Idle`
  - `EntityMotion`
  - `TopologyTransition`
- 아래 public API 추가
  - `CurrentPresentationPhase`
  - `IsPresentationActive`
  - `IsTopologyTransitionActive`
- phase 판정 기준을 아래 순서로 고정
  1. board rotation clip이 있으면 `TopologyTransition`
  2. local motion clip 또는 visibility clip이 있으면 `EntityMotion`
  3. 나머지는 `Idle`

완료 조건:

- 초기 상태는 `Idle`
- ordinary motion 중 `EntityMotion`
- topology rotation 중 `TopologyTransition`
- 모든 clip 종료 후 다시 `Idle`

### 5-2. 2단계: input lock과 burst 방지

목표:

- presentation active 동안 새 tick 실행을 막고 unlock 직후 burst를 제거한다.

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayInputHost.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/PlayMode/PlayerMovementPlayModeTests.cs`

구현 태스크:

- `AdvanceTime(float deltaTime)`가 presenter active 여부를 먼저 확인하도록 수정
- lock 중에도 아래 입력 상태는 유지
  - latest raw move input
  - buffered push
  - buffered flip
- lock 중 accumulated simulation time이 unlock 뒤 한 프레임에 몰리지 않도록 정책 고정
  - 권장안: lock 중 simulation advance를 멈추고 accumulated time을 tick interval 이하로 clamp
- `RunSingleTick()`도 topology transition 중 직접 실행을 막을지 여부를 명시
  - 1차 구현은 `presentation active이면 실행 불가`로 고정

완료 조건:

- lock 중 `NextTickIndex` 증가 없음
- buffered push/flip 유실 없음
- unlock 직후 정확히 다음 tick부터 재개
- unlock 직후 다중 tick burst 없음

### 5-3. 3단계: resolver topology 독점 규칙

목표:

- topology-changing tick을 ordinary movement tick과 논리적으로 분리한다.

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Movement/Runtime/Resolution/MovementResolver.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Scenario/MovementPhaseScenarioTests.cs`

구현 태스크:

- candidate에 topology change가 있으면 exclusive group으로 취급
- topology-changing group이 선택되면 같은 tick의 다른 movement group은 전부 reject
- ordinary group이 먼저 선택된 상태에서 뒤 topology-changing group이 나오면 reject
- 한 tick에 topology-changing group은 최대 1개만 허용
- reject reason 문자열을 새 정책에 맞게 고정

완료 조건:

- topology-changing tick과 일반 move/push/flip이 동시에 selected group에 들어가지 않음
- 관련 scenario test로 선택/거절 규칙이 고정됨

### 5-4. 4단계: topology motion duration 분리

목표:

- topology rotation이 push duration과 분리된 전용 timing을 사용하도록 만든다.

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/GameplayTimingProfile.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHostConfiguration.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs`
- 관련 test fixture 생성부

구현 태스크:

- `TopologyMotionDurationSeconds` 추가
- 기본값은 기존 `PushMotionDurationSeconds`와 동일
- presenter의 topology rotation clip만 새 duration을 사용하도록 변경
- 기존 push/flip timing 동작은 그대로 유지

완료 조건:

- topology motion duration만 독립 조정 가능
- 기존 ordinary motion 회귀 없음

### 5-5. 5단계: transition visibility data 추가

목표:

- topology transition 중 destination-only show metadata를 표현할 수 있게 만든다.

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPresentationData.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/TickPipelineStageOneTests.cs`

구현 태스크:

- ordinary visibility와 의미가 다른 transition 전용 struct 추가
- 최소 필드:
  - `EntityId`
  - `Mode`
  - `Cell`
  - `Topology`
  - `Facing`
- `Mode`는 우선 `ShowAtTransitionStart`를 지원
- 직접 이동 엔티티는 기존 `TickEntityMotion` 처리 우선

완료 조건:

- `TickPresentationData`가 topology transition visibility metadata를 담을 수 있음
- ordinary `Spawn/Detach/Remove`와 구분된 구조가 테스트로 고정됨

### 5-6. 6단계: TickResultBuilder 확장

목표:

- topology-changing tick에서 transition visibility metadata를 자동 생성한다.

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickResultBuilder.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/TickPipelineStageOneTests.cs`

구현 태스크:

- topology change가 있는 tick에서 pre/post visible set을 비교
- 생성 규칙:
  - pre invisible, post visible -> show
  - pre visible, post visible -> 별도 전환 metadata 불필요
- ordinary visibility와 충돌 시 우선순위 고정
  - `Remove > Detach > Spawn > TransitionVisibility`
- cleanup/remove 대상과 transition show가 동시에 걸리는 경우 ordinary visibility 우선

완료 조건:

- topology-changing tick에서 destination-only show metadata 생성
- ordinary visibility와 충돌 시 일관된 결과 유지

### 5-7. 7단계: transition projection 경로 추가

목표:

- transition 동안 active 2면 밖의 필요 엔티티도 안전하게 local pose를 계산할 수 있게 만든다.

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayCubeProjector.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/RuntimeBoardBoundsGuardTests.cs`

구현 태스크:

- gameplay query용 projection과 분리된 presentation 전용 projection API 추가
- 지원 범위는 1차 구현에서 아래 union으로 제한
  - `Source Active Faces`
  - `Destination Active Faces`
- stationary passenger도 이 API로 local pose 계산
- ordinary 상태에서는 기존 projection 결과와 동일한 값 유지

완료 조건:

- transition visible entity가 gameplay-visible이 아니어도 local pose 계산 가능
- ordinary frame에서는 기존 projector와 동일 동작

### 5-8. 8단계: presenter transition 렌더링 적용

목표:

- destination topology visible set 즉시 적용, destination-only show, stationary passenger board-tether를 실제 렌더링에 반영한다.

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/RuntimeBoardBoundsGuardTests.cs`

구현 태스크:

- transition visibility state cache 추가
- processing entity set을 아래 집합의 union으로 확장
  - committed local target poses
  - retained local target poses
  - transition start show 대상
- directly moved entity:
  - 기존 local motion clip 유지
  - start pose는 source topology 기준
  - end pose는 committed destination 기준
- stationary passenger:
  - local motion clip 없음
  - fallback local pose만 유지
  - board rotation에만 탑승
- transition 종료 시 정리:
  - transition visibility state 제거
  - committed visible set만 남김

완료 조건:

- 회전 시작 시 source-only entity/face immediate hide
- stationary passenger가 local drift 없이 보드와 함께 회전함
- transition 종료 후 destination topology 기준 visible set만 남음

### 5-9. 9단계: 테스트 보강과 쇼케이스 검증

목표:

- 회전 중과 회전 종료 직후를 분리 검증하고 통합 scene에서 회귀를 확인한다.

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/RuntimeBoardBoundsGuardTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/TickPipelineStageOneTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Scenario/MovementPhaseScenarioTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/PlayMode/PlayerMovementPlayModeTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/CubeSurfaceTraversalShowcaseInstaller.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/CombinedGameplayShowcaseInstaller.cs`

구현 태스크:

- EditMode
  - presenter phase API 검증
  - topology rotation 중 board root rotation 검증
  - source-only immediate hide 검증
  - stationary passenger board-tether 검증
  - transition 종료 후 cleanup 검증
- PlayMode
  - presentation active 동안 tick 미실행
  - buffered push/flip 유지
  - unlock 직후 burst 없음
  - transition 종료 후 새 topology 기준 이동 재개
- Showcase
  - `CubeSurfaceTraversalShowcaseInstaller`로 topology transition 고립 검증
  - `CombinedGameplayShowcaseInstaller`로 box/push/flip 혼합 회귀 확인

완료 조건:

- 회전 중과 회전 종료 직후 계약이 테스트로 분리 고정됨
- traversal showcase와 combined showcase 모두 시각 회귀 없음

## 6. 파일별 주요 변경 지점

- `GameplayTickViewPresenter`
  - 상태 API 추가
  - topology transition visibility cache 추가
  - transition projection 경로 사용
  - topology 전용 duration 사용
- `GameplayInputHost`
  - presentation lock
  - burst 방지
  - buffered interaction 유지
- `MovementResolver`
  - topology-changing tick exclusive rule
- `TickPresentationData`
  - transition visibility metadata 추가
- `TickResultBuilder`
  - destination-start show metadata 생성
- `GameplayCubeProjector`
  - transition 전용 projection 추가
- `GameplayTimingProfile`
  - `TopologyMotionDurationSeconds` 추가
- `GameplaySceneHostConfiguration`
  - topology timing 설정 노출

## 7. 검증 전략

### 7-1. EditMode

- topology-changing tick의 presentation data가 기대한 destination-start show metadata를 생성하는지 검증
- presenter phase가 `Idle -> EntityMotion -> TopologyTransition -> Idle`로 변하는지 검증
- transition 중 board root rotation과 entity local pose가 충돌하지 않는지 검증
- transition 종료 시 transition visibility state가 정리되는지 검증

### 7-2. PlayMode

- presenter active 동안 input host가 tick을 실행하지 않는지 검증
- lock 중 push/flip buffer가 살아 있는지 검증
- unlock 직후 한 프레임에 여러 tick이 몰리지 않는지 검증
- topology transition 직후 다음 이동이 새 topology 기준으로 적용되는지 검증

### 7-3. 수동 검증

- traversal showcase에서 `Floor -> Front -> Ceiling -> Back` 회전 흐름 확인
- combined showcase에서 push, item, flip이 섞인 상태로 topology transition이 들어가도 entity pop이나 이중 회전이 없는지 확인

## 8. 권장 PR 분리

### PR 1

상태:

- 완료 (2026-03-31)

- 1단계 presenter 상태 API
- 2단계 input lock
- 3단계 resolver exclusive rule

구현 메모:

- `GameplayTickViewPresenter`에 `GameplayPresentationPhase`, `CurrentPresentationPhase`, `IsPresentationActive`, `IsTopologyTransitionActive`를 추가했다.
- phase 우선순위는 `board rotation clip -> local motion/visibility clip -> idle` 순서로 고정했다.
- `GameplayInputHost`는 presenter active 동안 `AdvanceTime(...)`와 `RunSingleTick()` 모두 새 tick 실행을 막는다.
- lock 중 `_accumulatedTime`은 `SimulationTickIntervalSeconds` 이하로 clamp하여 unlock 직후 다중 tick burst를 제거했다.
- raw move input, buffered push, buffered flip은 lock 중 유지되고 unlock 뒤 첫 tick에서 정상 소비되도록 고정했다.
- `MovementResolver`는 topology-changing group을 tick-exclusive로 처리하고, 충돌 시 `Reason=TopologyExclusive|BlockedBy=...|BlockingKind=...|BlockingTopologyChange=...` 로그를 남기도록 바꿨다.
- guard/scenario/playmode test를 추가해 `Idle -> EntityMotion -> TopologyTransition -> Idle`, presentation lock, buffered interaction 유지, topology-exclusive selection 규칙을 고정했다.

### PR 2

상태:

- 완료 (2026-03-31)

- 4단계 topology duration 분리
- 5단계 transition visibility data
- 6단계 TickResultBuilder 확장

구현 메모:

- `GameplayTimingProfile`에 `TopologyMotionDurationSeconds`를 추가하고, 기존 생성 경로는 기본값으로 `PushMotionDurationSeconds`를 그대로 상속하도록 유지했다.
- `GameplaySceneHostConfiguration`에 `TopologyMotionDurationSeconds` 설정을 노출하고, 미지정 시 push duration으로 fallback되도록 고정했다.
- `GameplayTickViewPresenter`의 board rotation clip은 이제 `PushMotionDurationSeconds`가 아니라 `TopologyMotionDurationSeconds`를 사용한다.
- `TickPresentationData`에 `TickTransitionVisibilityChange`, `TickTransitionVisibilityMode`, `TransitionVisibilityChanges`를 추가해 ordinary visibility와 분리된 topology transition metadata를 담을 수 있게 했다.
- `TickResultBuilder`는 topology-changing tick에서 `PreMovementSnapshot`과 `FinalAuthoritativeSnapshot`의 gameplay-visible set을 비교해 destination-only `ShowAtTransitionStart` metadata만 자동 생성한다.
- directly moved entity와 ordinary visibility(`Remove`, `Detach`, `Spawn`) 대상은 transition visibility 생성에서 제외해 `Remove > Detach > Spawn > TransitionVisibility` 우선순위를 코드로 고정했다.
- unit/runtime test를 추가해 topology passenger destination-show 생성, ordinary visibility 우선순위, topology 전용 duration override와 default fallback을 검증했다.

### PR 3

상태:

- 완료 (2026-03-31)

- 7단계 transition projection
- 8단계 presenter transition rendering
- 9단계 테스트 및 showcase 검증

구현 메모:

- `GameplayCubeProjector`에 `TryProjectTransitionEntityCell(...)`, `TryResolveTransitionEntityRotation(...)`를 추가해 topology-changing tick 동안 `Source Active Faces + Destination Active Faces` union만 presentation 전용으로 투영할 수 있게 했다.
- visible set 해석은 transition 시작 프레임부터 destination topology를 authoritative로 사용하고, projector의 face-union projection은 directly moved entity와 ordinary visibility pose 계산용으로만 남긴다.
- transition projection은 destination topology local space를 기준으로 계산하고, ordinary frame에서 `sourceTopology == destinationTopology`이면 기존 `TryProjectEntityCell(...)` / `TryResolveEntityRotation(...)`과 동일한 결과를 반환하도록 고정했다.
- `GameplayTickViewPresenter`는 `transition visibility state cache`를 destination-start show 용도로만 유지하고, processing entity set을 `committed + retained + transition visibility` union으로 확장했다.
- topology-changing motion의 source/destination pose와 ordinary visibility retain pose도 transition projection 경로를 사용하도록 바꿔, 회전 시작 프레임의 local pose pop을 줄였다.
- topology transition 중 source-only transition state는 생성하지 않고, destination topology 기준 visible set만 렌더되도록 cleanup contract를 고정했다.
- `RuntimeBoardBoundsGuardTests`에 transition projection face-union/ordinary-equivalence, source-only immediate hide, topology-changing motion start pose projection 계약을 추가했다.
- `Game.Feature.Gameplay.Tests.csproj`는 Windows MSBuild로 빌드 통과를 확인했다. Unity batch `-runTests`는 이 환경에서 스크립트 리컴파일까지만 수행하고 result XML을 남기지 않아, 수동 showcase 검증은 별도 실행이 필요하다.

## 9. 완료 정의

아래 조건을 모두 만족하면 본 계획은 완료로 본다.

- topology transition 중 다음 tick이 실행되지 않는다.
- unlock 직후 tick burst가 발생하지 않는다.
- topology-changing tick은 ordinary movement tick과 공존하지 않는다.
- source-only visible entity가 회전 시작 시 바로 사라진다.
- stationary passenger가 local motion 없이 board rotation에만 탑승한다.
- transition 종료 후 destination topology 기준 visible set만 남는다.
- edit/play mode 테스트가 모두 통과한다.
