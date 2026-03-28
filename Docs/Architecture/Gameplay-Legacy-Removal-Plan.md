# Gameplay Legacy Removal Plan

## 1. 목적

이 문서는 현재 gameplay runtime에 남아 있는 legacy API, alias, compatibility path를 어떤 순서로 제거할지 정의한다.

현재 authoritative runtime 의미는 아래 두 가지로 이미 고정되어 있다.

- 박스 `Push`는 "한 번에 stopper 직전까지 이동"이 아니라 "`Sliding` 상태로 진입한 뒤 매 tick 다음 1칸씩 이동"이다.
- canonical 용어는 `Push` / `Flip` / `Item` / `Destroy`이며, `Interact` / `Throw` / `BoxSlide`는 과거 명명 흔적이다.

문제는 runtime 동작이 아니라, 삭제되지 않은 legacy query와 alias가 계속 남아 있어 코드 읽기, 테스트 해석, 문서 유지보수를 어렵게 만든다는 점이다.

이 계획서는 다음 세 가지 목표를 가진다.

- dead semantic을 runtime에서 완전히 제거한다.
- source-level alias를 줄여 canonical 이름만 남긴다.
- runtime behavior 변경과 compatibility cleanup을 분리해 regressions를 추적 가능하게 만든다.

## 2. 삭제 원칙

- 삭제는 의미 단위로 끊는다. terminal query 제거, naming alias 제거, input compatibility 제거, unbounded helper 제거를 한 PR에 섞지 않는다.
- authoritative runtime 경로는 먼저 보존하고, legacy 경로만 제거한다.
- public API 삭제는 repo 내부 callsite를 0으로 만든 뒤 진행한다.
- 문서, 테스트, 코드 중 하나만 먼저 남기지 않는다. 각 phase에서 세 층을 함께 정리한다.
- unbounded helper 삭제는 push semantic cleanup과 별도 트랙으로 본다.

## 3. 현재 정리 대상

### 3-1. 완료: terminal slide query 계층 제거

2026-03-29 기준 이 계층은 제거 완료되었다. 아래 항목은 실제 제거 범위 기록이다.

제거 완료 범위:

- `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldQueryService.cs`
  - `TryGetSurfaceBoxSlideDestination(...)`
  - `TryGetLegacySurfaceBoxSlideDestination(...)`
  - `TryGetBoxSlideDestination(...)`
  - `TryGetLegacyBoxSlideDestination(...)`
  - `TryGetUnboundedSurfaceBoxSlideDestination(...)`
  - `TryGetLegacyPlanarBoxSlideDestination(...)`
- `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldSnapshot.cs`
  - `TryGetSurfaceBoxSlideDestination(...)`
  - `TryGetLegacySurfaceBoxSlideDestination(...)`
  - `TryGetBoxSlideDestination(...)`
  - `TryGetLegacyBoxSlideDestination(...)`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/WorldSurfaceQueryTests.cs`
  - `WorldSnapshot_LegacySurfaceBoxSlideDestination_*`
  - `WorldSnapshot_LegacyBoxSlideDestination_*`
- 문서
  - `Docs/Architecture/Cube-Surface-Gameplay-Implementation-Plan.md`
  - `Docs/Architecture/Deterministic-Tick-Simulation-Blueprint.md`
  - `Docs/Architecture/Deterministic-Tick-Simulation-Implementation-Plan.md`

제거 이후 authoritative slide query는 아래 하나만 남긴다.

- `WorldSnapshot.TryResolveNextSurfaceBoxSlideStep(...)`
- `WorldQueryService.TryResolveNextSurfaceBoxSlideStep(...)`

예상 파손:

- repo 내부에서는 주로 테스트와 문서 참조가 깨진다.
- 외부 assembly가 위 public 메서드를 호출 중이면 compile break가 난다.
- `Vector2Int` 기반 terminal query를 임시 어댑터처럼 쓰던 외부 도구가 있으면 face-crossing 정보를 잃는 기존 호출부가 사라진다.

완료 조건:

- slide 관련 public API에서 terminal ray-scan 의미가 더 이상 노출되지 않는다.
- unit/scenario test는 모두 next-step semantic만 검증한다.
- 문서 어디에도 `TryGetSurfaceBoxSlideDestination` / `TryGetBoxSlideDestination`를 canonical API처럼 설명하지 않는다.

### 3-2. 완료: interaction alias 계층 제거

2026-03-29 기준 이 계층은 제거 완료되었다. 아래 항목은 실제 제거 범위 기록이다.

제거 완료 범위:

- `Assets/_Features/Gameplay/Gameplay_Movement/Runtime/Collection/RawMovementIntent.cs`
  - `MovementCommandKind.Interact`
  - `MovementCommandKind.Throw`
- `Assets/_Features/Gameplay/Gameplay_Model/Runtime/Groups/ActionGroupKind.cs`
  - `Throw`
  - `BoxSlide`
- `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/EntityState.cs`
  - `BoxCapabilities.Pushable`
  - `BoxCapabilities.Throwable`
  - `BoxCapabilities.LootOnInteractDestroy`
- `Assets/_Features/Gameplay/Gameplay_Movement/Runtime/Intents/MoveIntent.cs`
  - `InteractMoveIntent`
  - `ThrowIntent`
- canonical callsite 치환
  - `TickPipeline`의 `InteractMoveIntent` 생성 -> `PushIntent`
  - `MovementPhaseScenarioTests`의 helper 생성 -> `PushIntent`
  - `PlayerLogic`은 movement core에서 `PushPressed` 의미를 직접 읽도록 정리
  - `MovementCommitter`의 내부 `Interaction` 네이밍을 `Flip` 기준으로 정리
- 문서
  - `Docs/Architecture/Cube-Surface-Gameplay-Implementation-Plan.md`
  - `Docs/Architecture/Deterministic-Tick-Simulation-Blueprint.md`
  - `Docs/Architecture/Deterministic-Tick-Simulation-Implementation-Plan.md`

제거 이후 canonical 이름:

- `PushIntent`
- `FlipIntent`
- `BoxCapabilities.Push` / `Flip` / `Item` / `Destroy`
- `ActionGroupKind.Push` / `Flip` / `Item`

남겨 둔 비범위:

- `PlayerTickCommand.InteractPressed` / `ThrowPressed`
- `GameplayInputHost.BufferInteract()` / `BufferThrow()`
- `Player/Interact` / `Player/Throw` input action fallback

위 input compatibility alias는 3-3 단계에서 별도로 제거한다.

예상 파손:

- enum alias는 값은 같아도 이름이 사라지므로 source compile break가 바로 난다.
- `InteractMoveIntent` 삭제 시 constructor symbol이 끊긴다.
- 문서와 테스트에 old vocabulary를 그대로 남기면 semantic drift가 다시 생긴다.

완료 조건:

- movement/group/capability 레벨에서 canonical enum 이름만 남는다.
- box interaction 관련 type 이름이 runtime semantics와 동일하다.
- 문서와 테스트 명명에서 `BoxSlide`, `InteractPushable`, `Throwable` 같은 옛 표현이 더 이상 주 경로를 설명하지 않는다.

### 3-3. 높은 파급도: input compatibility alias 제거

이 계층은 code cleanup 자체는 단순하지만, 실제 input asset 호환성과 연결되어 있어 더 위험하다.

삭제 대상:

- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/PlayerTickCommand.cs`
  - `InteractPressed`
  - `ThrowPressed`
  - `Interact(...)`
  - `Throw(...)`
  - `Create(..., interactPressed:, ...)`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayInputHost.cs`
  - `BufferInteract()`
  - `BufferThrow()`
  - `"Player/Interact"` fallback
  - `"Player/Throw"` fallback

동시에 canonical 이름으로 바꿔야 하는 대상:

- `InteractPressed` -> `PushPressed`
- `ThrowPressed` -> `FlipPressed`
- `Player/Interact` -> `Player/Push`
- `Player/Throw` -> `Player/Flip`

직접 영향 파일:

- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/PlayerLogic.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayInputHost.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/PlayerMovementInputTests.cs`
- `Docs/Architecture/Deterministic-Tick-Simulation-Implementation-Plan.md`

예상 파손:

- named argument `interactPressed:` / `throwPressed:` 사용부가 전부 compile break된다.
- 외부 InputActionAsset이 아직 `Player/Interact`, `Player/Throw`를 쓰고 있으면 runtime input이 죽는다.
- showcase scene, installer, test bootstrap이 legacy action name을 가정하고 있으면 scene-level bug가 난다.

선행 조건:

- input asset에서 `Player/Push`, `Player/Flip`가 실제로 존재하고 연결되어 있어야 한다.
- scene/installer/test fixture가 canonical action name으로만 초기화되는지 먼저 확인해야 한다.

완료 조건:

- host와 simulation 경계 모두 `Push` / `Flip` 이름만 사용한다.
- `PlayerTickCommand` public surface에서 `Interact` / `Throw` 흔적이 사라진다.
- repo 내 입력 경계 문서가 canonical action name만 설명한다.

### 3-4. 별도 트랙: legacy unbounded helper 제거

이 계층은 legacy이긴 하지만, push naming cleanup과는 다른 주제다. 같은 작업으로 묶으면 diff가 커지고 실패 원인 추적이 어려워진다.

삭제 후보:

- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/GameplayCompositionRoot.cs`
  - `CreateLegacyUnboundedWorldState(...)`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/TestSupport/GameplayWorldStateTestFactory.cs`
  - `CreateLegacyUnbounded(...)`

주요 의존 테스트:

- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/RuntimeBoardBoundsGuardTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/TickPipelineStageOneTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Scenario/MovementPhaseScenarioTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Replay/TickReplayDeterminismTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Fuzz/FuzzScenarioDefinition.cs`

예상 파손:

- replay/fuzz/scenario test의 일부 fixture가 bounded factory로 이주하기 전까지 대량 compile break가 난다.
- runtime guard test는 "internal legacy helper가 남아 있음"을 전제로 작성되어 있어 테스트 의도 자체를 바꿔야 한다.

권장 정책:

- unbounded helper는 별도 문서 또는 별도 PR로 다룬다.
- 먼저 "runtime은 bounded-only"를 유지한 채 test fixture migration부터 끝내고 제거한다.

## 4. 권장 삭제 순서

### 4-1. 1차 PR: terminal slide query 제거

- `TryGetSurfaceBoxSlideDestination` 계층 삭제
- 관련 legacy test 삭제
- 문서에서 terminal query를 historical note로도 남기지 않도록 정리

이 단계의 목적은 "지금 dead semantic인 공개 query"를 먼저 없애는 것이다. 가장 작은 위험으로 가장 큰 모호성을 줄인다.

### 4-2. 2차 PR: interaction naming alias 제거

- enum/type/property 이름을 canonical로 고정
- `InteractMoveIntent` 제거 및 `PushIntent`로 정규화
- 문서와 테스트 명명 동기화

이 단계의 목적은 source code vocabulary를 현재 runtime 의미와 일치시키는 것이다.

### 4-3. 3차 PR: input compatibility 제거

- host fallback action 삭제
- `PlayerTickCommand` alias surface 삭제
- input asset migration 확인

이 단계는 runtime asset 호환 검증이 필요하므로 마지막에 둔다.

### 4-4. 4차 PR: unbounded helper 제거

- test fixture migration 완료 후 helper 삭제
- runtime guard test 재작성

이 단계는 semantic cleanup이 아니라 test platform cleanup에 가깝다.

## 5. 삭제 시 공통 체크리스트

- `rg` 기준으로 삭제 대상 symbol의 repo 내 callsite가 0인지 확인
- public API 삭제 전, 외부 assembly 또는 scene asset 의존 가능성을 확인
- 테스트 이름이 새 canonical semantics를 설명하는지 확인
- 문서에서 old term이 "현재 authoritative behavior"처럼 읽히지 않는지 확인
- `MovementExpander`, `PlayerLogic`, `GameplayInputHost`, `WorldSnapshot`이 동일한 vocabulary를 쓰는지 확인

## 6. 최종 목표 상태

삭제가 완료되면 아래 상태가 되어야 한다.

- slide query는 `TryResolveNextSurfaceBoxSlideStep` 하나만 authoritative하게 남는다.
- movement vocabulary는 `Move`, `Push`, `Flip`, `Item`, `Destroy`만 남는다.
- input vocabulary는 `Player/Move`, `Player/Push`, `Player/Flip`만 남는다.
- runtime world 생성은 bounded-only이며, unbounded는 별도 테스트 전략이 정리된 뒤 완전히 제거된다.

이 상태가 되어야 cube-surface runtime, deterministic 문서, 테스트 명명, input 경계가 모두 같은 의미 체계를 공유한다.
