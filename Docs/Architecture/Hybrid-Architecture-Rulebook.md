# Hybrid Architecture Rulebook

## 1. 목적

이 문서는 본 Unity 프로젝트의 최종 구조 원칙을 정의한다.  
프로젝트는 `실시간 그리드 기반 박스 슬라이딩 게임`, `ScriptableObject 중심`, `SlideRequest -> SlideResolver -> MovementExecution -> BoardSnapshot` 흐름을 전제로 하며, 구조 개념은 `Core / Shared / Features` 3계층 Hybrid 방식으로 설계한다.

실제 Unity 최상단 폴더명은 정렬을 위해 다음처럼 고정한다.

- `Assets/_Core`
- `Assets/_Shared`
- `Assets/_Features`

이 구조의 핵심 기준은 중요도가 아니다.

- 왜 바뀌는가?
- 어디까지 재사용 가능한가?

분류 기준은 아래 한 줄로 요약된다.

> 패턴은 Core, 규칙은 Feature

## 2. 계층 정의

### Core

Core는 프로젝트의 기반 인프라다.

포함 대상:

- `ServiceLocator`, DI 구성
- `EventBus`
- `Result<T>`
- 공통 인터페이스
- Object Pool
- 범용 `StateMachine`
- Logger
- Save/Load 프레임워크의 범용 부분
- Utilities / Extensions

특징:

- 장르가 바뀌어도 유지된다.
- 게임 규칙을 모른다.
- "무엇을 움직일지"가 아니라 "어떻게 실행할지"만 담당한다.

### Shared

Shared는 여러 Feature가 함께 사용하는 공용 자산 계층이다.

포함 대상:

- 공통 VFX
- 공통 Material
- 공통 UI 프리셋
- 공통 Audio
- 공통 Font
- 여러 Feature가 공유하는 Enum / Constants / Table

특징:

- 특정 Feature의 규칙에 종속되면 안 된다.
- 여러 곳에서 재사용 가능해야 한다.
- 로직보다 리소스 중심이다.

### Features

Features는 실제 게임 로직과 콘텐츠를 담는 계층이다.

포함 대상:

- 플레이어 입력 해석
- 박스 슬라이딩
- Grid 점유 상태
- 충돌 / 밀기 / 정지 판정
- 타일 기믹
- 스테이지 목표
- 체크포인트 / 리셋
- 게임 UI 기능
- `SlideRequest / SlideResolver / MovementExecution / BoardSnapshot`

특징:

- 게임 규칙이 바뀌면 함께 바뀐다.
- 장르가 바뀌면 구조도 함께 바뀐다.
- 실제 콘텐츠와 룰을 담는다.

## 3. 핵심 분류 원칙

아래 기준으로만 분류한다.

- 장르가 바뀌어도 유지되면 `Core`
- 여러 Feature에서 공유하는 자산이면 `Shared`
- 게임 규칙, 도메인 로직, 콘텐츠면 `Feature`

중요하다고 해서 `Core`가 되는 것은 아니다.  
`SlideResolver`, `MovementExecution`, `BoardSnapshot`은 매우 중요하지만 전부 게임 규칙에 의존하므로 `Feature`다.

## 4. System 분류 규칙

### Feature에 둬야 하는 것

- `SlideRequest`
- `SlideResolver`
- `MovementExecution`
- `BoardSnapshot`

이유:

- 어떤 입력이 실제 슬라이드 요청이 되는지는 게임마다 다르다.
- 박스가 어디까지 미끄러지고 무엇에 막히는지는 룰이다.
- 연쇄 이동, 충돌 처리, 타일 반응 순서는 게임 규칙이다.
- 어떤 보드 상태를 저장할지는 도메인 상태에 의존한다.

### Core에 둘 수 있는 것

- `ICommand` 같은 범용 패턴
- `EventBus`
- `StateMachine`
- 실행 컨텍스트와 무관한 공통 인터페이스

즉, 패턴은 Core에 둘 수 있지만 슬라이딩 규칙은 Feature로 내려야 한다.

## 5. 의존성 규칙

허용:

- `Feature -> Core`
- `Feature -> Shared`

금지:

- `Core -> Feature`
- `Shared -> Feature`

핵심 원칙:

- 기반 계층은 도메인 규칙을 알면 안 된다.
- Shared는 특정 슬라이딩 규칙을 몰라야 한다.
- Feature는 독립적으로 추가/삭제 가능해야 한다.

## 6. 권장 폴더 구조

```text
Assets/
  _Core/
    Runtime/
      Bootstrap/
      DI/
      Events/
      Logging/
      Pools/
      SaveLoad/
      StateMachine/
      Types/
      Utilities/
    Editor/

  _Shared/
    Art/
      Materials/
      VFX/
    Audio/
      BGM/
      SFX/
    UI/
      Fonts/
      Themes/
      CommonWidgets/
    Data/
      Enums/
      Constants/
      Tables/

  _Features/
    Gameplay/
      Gameplay_Loop/
      Gameplay_Slide/
      Gameplay_Collision/
      Gameplay_BoardState/
      Gameplay_Checkpoint/
    Grid/
      Grid_Board/
      Grid_Tiles/
      Grid_Occupancy/
    Boxes/
      Box_PlayerPush/
      Box_Heavy/
      Box_GoalBox/
    Stages/
      Stage_Factory_01/
      Stage_IceHall_01/
    Gimmicks/
      Gimmick_Conveyor/
      Gimmick_IceTile/
      Gimmick_PressurePlate/
    UI/
      UI_HUD/
      UI_LevelResult/
```

## 7. Feature 내부 구조 원칙

Feature는 반드시 Vertical Slice로 구성한다.

좋은 예:

```text
Assets/_Features/Boxes/Box_Heavy/
  Box_Heavy.asset
  Box_Heavy.cs
  Box_Heavy.prefab
  Box_Heavy_HitVFX.prefab
  Box_Heavy_Icon.png
```

이 구조의 장점:

- 기능 단위로 이동, 삭제, 수정이 쉽다.
- 데이터, 로직, 리소스의 변경 이유가 일치한다.
- AI와 개발자 모두 폴더 의미를 빠르게 이해할 수 있다.

나쁜 예:

```text
Assets/Scripts/Boxes/
Assets/Prefabs/Boxes/
Assets/DataAssets/Boxes/
Assets/VFX/Boxes/
```

이 구조의 문제:

- 기능 단위가 찢어진다.
- 삭제 영향 범위를 추적하기 어렵다.
- 변경 이유가 아니라 파일 타입 기준으로 구조가 고정된다.

## 8. Gameplay Feature 내부 구조

`SlideRequest -> SlideResolver -> MovementExecution -> BoardSnapshot`은 `Assets/_Features/Gameplay` 내부에 위치해야 한다.

권장 예시:

```text
Assets/_Features/Gameplay/
  Gameplay_Slide/
    SlideRequest.asset
    SlideInputTranslator.cs
    SlidePreviewArrow.prefab

  Gameplay_Collision/
    SlideResolver.cs
    CollisionRule.asset
    PushPriorityTable.asset

  Gameplay_Loop/
    MovementExecutionRunner.cs
    ContinuousTickController.cs
    TileReactionStep.cs

  Gameplay_BoardState/
    BoardSnapshot.cs
    SnapshotFactory.cs
    OccupancyViewData.cs

  Gameplay_Checkpoint/
    CheckpointState.asset
    ResetToCheckpointCommand.cs
```

설명:

- `SlideRequest`: 입력을 실제 슬라이드 요청으로 해석
- `SlideResolver`: 충돌, 밀기, 정지, 연쇄 이동 판정
- `MovementExecution`: 이동 적용, 보간, 타일 반응 처리
- `BoardSnapshot`: 보드 상태 저장, 리셋, 리플레이, 디버그 조회

이들은 모두 실시간 슬라이딩 규칙을 담으므로 `Core`가 아니라 `Gameplay Feature` 소속이다.

## 9. ScriptableObject 배치 규칙

절대 규칙:

- `ScriptableObject`는 전역 폴더에 두지 않는다.
- `DataAssets/`, `ScriptableObjects/` 같은 루트 집합 폴더를 만들지 않는다.
- SO는 반드시 해당 Feature 내부에 둔다.

좋은 예:

- `Assets/_Features/Boxes/Box_Heavy/Box_Heavy.asset`
- `Assets/_Features/Gameplay/Gameplay_Collision/CollisionRule.asset`
- `Assets/_Features/Stages/Stage_Factory_01/Stage_Factory_01.asset`

나쁜 예:

- `Assets/DataAssets/Box_Heavy.asset`
- `Assets/ScriptableObjects/Gameplay/CollisionRule.asset`

이유:

- SO도 도메인 데이터다.
- 해당 기능과 함께 이동, 삭제, 수정되어야 한다.
- 폴더 구조가 곧 의존성과 변경 이유를 드러내야 한다.

## 10. Addressables 전략

Addressables도 계층과 Feature 경계를 따라야 한다.

원칙:

- Group는 가능하면 Feature 중심으로 나눈다.
- 공용 리소스만 `Shared` Group에 둔다.
- 로딩 단위는 "파일 타입"이 아니라 "기능 묶음" 기준으로 잡는다.

권장 예시:

- `Shared_Common_UI`
- `Shared_Common_VFX`
- `Feature_Gameplay`
- `Feature_Boxes_Heavy`
- `Feature_Stages_Factory`

레이블 권장:

- `feature:gameplay`
- `feature:boxes`
- `feature:grid`
- `feature:stages`
- `shared:ui`
- `shared:vfx`
- `content:box_heavy`

운영 원칙:

- 하나의 박스가 필요로 하는 프리팹, SO, VFX는 가능한 한 같은 Feature 경계 안에서 참조되게 한다.
- Shared 에셋은 범용 자산일 때만 분리한다.
- Feature 삭제 시 Addressables Group도 함께 정리 가능해야 한다.

## 11. asmdef 분리 전략

asmdef는 계층 의존성을 강제하는 도구로 사용한다.

권장 예시:

- `Game.Core`
- `Game.Core.Editor`
- `Game.Shared`
- `Game.Feature.Gameplay`
- `Game.Feature.Grid`
- `Game.Feature.Boxes`
- `Game.Feature.Stages`
- `Game.Feature.Gimmicks`
- `Game.Feature.UI`

참조 규칙:

- `Game.Core`: 다른 Feature 참조 금지
- `Game.Shared`: `Game.Core` 참조 가능, Feature 참조 금지
- 각 `Game.Feature.*`: `Game.Core`, `Game.Shared` 참조 가능
- Feature끼리 직접 참조는 최소화하고, 필요 시 인터페이스나 이벤트로 연결

실무 팁:

- 처음부터 지나치게 세분화하지 말고 Feature 단위 asmdef부터 시작한다.
- 의존성 역전이 필요하면 인터페이스는 `Core` 또는 별도 안정 계층으로 올린다.

## 12. 네이밍 규칙

폴더:

- 실제 계층 폴더는 `_Core`, `_Shared`, `_Features`
- Feature 하위는 `도메인_기능` 형식 사용
- 예: `Box_Heavy`, `Gameplay_Collision`, `UI_LevelResult`

클래스:

- 역할이 드러나는 명사 또는 명사구 사용
- 예: `SlideResolver`, `ContinuousTickController`, `SnapshotFactory`

SO 에셋:

- 가능하면 슬라이스 이름과 동일하게 맞춘다.
- 예: `Box_Heavy.asset`, `CollisionRule.asset`

Addressables Group:

- `Shared_*`, `Feature_*` 접두어 유지

asmdef:

- `Game.Core`, `Game.Shared`, `Game.Feature.*` 패턴 유지

## 13. 좋은 예 / 나쁜 예

좋은 예:

- 박스 타입 하나를 삭제하면 해당 폴더만 제거하면 된다.
- 슬라이딩 충돌 규칙 변경은 `Assets/_Features/Gameplay` 안에서 해결된다.
- 공통 폰트와 UI 테마는 `Assets/_Shared/UI`에 있다.
- EventBus, Pool, Logger는 `Assets/_Core`에 있다.

나쁜 예:

- `SlideResolver`가 중요하다는 이유만으로 `Core`에 들어간다.
- 모든 SO를 `Assets/DataAssets`에 모은다.
- `Scripts/`, `Prefabs/`, `Materials/`처럼 타입 기준으로 모든 리소스를 분산한다.
- Shared가 특정 박스나 타일 규칙을 알게 된다.

## 14. 최종 결론

이 프로젝트 구조의 핵심은 아래 3줄이다.

- `Core = 기반, 변하지 않는 범용 인프라`
- `Shared = 공용, 여러 Feature가 함께 쓰는 자산`
- `Feature = 게임, 실제 규칙과 콘텐츠`

판단이 애매할 때는 중요도를 보지 말고 아래 질문으로 다시 분류한다.

1. 장르가 바뀌어도 남는가?
2. 여러 Feature가 함께 쓰는가?
3. 게임 규칙 때문에 바뀌는가?

그리고 가장 중요한 문장은 다음이다.

> Feature로 사고하고, Shared로 재사용하고, Core로 고정한다.
