# AI Hybrid Structure Rules

이 파일은 AI가 이 Unity 프로젝트의 폴더 구조와 배치 규칙을 일관되게 따르기 위한 운영 규칙이다.  
새 세션의 AI는 이 문서를 우선 읽고, 아래 규칙을 깨지 않는 방식으로 폴더와 파일을 생성해야 한다.

## 1. 프로젝트 전제

- 장르: 실시간 그리드 기반 박스 슬라이딩 게임
- 데이터 중심: ScriptableObject
- 게임 흐름: `SlideRequest -> SlideResolver -> MovementExecution -> BoardSnapshot`
- 구조 개념: `Core / Shared / Features`
- 실제 최상단 폴더명: `_Core / _Shared / _Features`

## 2. 절대 규칙

### 계층 규칙

- `Core`는 `Feature`를 참조하면 안 된다.
- `Shared`는 `Feature`를 참조하면 안 된다.
- `Feature`는 `Core`를 사용할 수 있다.
- `Feature`는 `Shared`를 사용할 수 있다.

### 분류 규칙

- 장르가 바뀌어도 유지되면 `Core`
- 여러 Feature가 공유하면 `Shared`
- 게임 규칙, 콘텐츠, 도메인 상태면 `Feature`

### System 규칙

- `SlideRequest`는 `Feature`
- `SlideResolver`는 `Feature`
- `MovementExecution`은 `Feature`
- `BoardSnapshot`은 `Feature`
- `ICommand`, `EventBus`, `StateMachine`은 `Core` 가능

### 금지 규칙

- 전역 `ScriptableObjects/` 폴더 금지
- 전역 `DataAssets/` 폴더 금지
- 타입 기반 구조 혼합 금지
- `Core -> Feature` 의존 금지
- `Shared -> Feature` 의존 금지

### 필수 규칙

- 모든 Feature는 Vertical Slice로 구성한다.
- Feature는 독립적으로 삭제 가능해야 한다.
- Feature 내부에 데이터 + 로직 + 리소스를 함께 둔다.
- SO는 반드시 해당 Feature 폴더 안에 둔다.

## 3. AI 판단 기준

AI는 새 파일 또는 폴더를 만들기 전에 아래 순서로 판단한다.

1. 이것이 범용 패턴인가, 게임 규칙인가?
2. 여러 Feature가 함께 쓰는 공용 자산인가?
3. 장르가 바뀌어도 유지되는가?
4. 삭제 시 특정 Feature와 함께 사라져야 하는가?

결론:

- 패턴이면 `Core`
- 공용 자산이면 `Shared`
- 규칙이면 `Feature`

## 4. 폴더 생성 기본형

AI가 구조를 새로 만들거나 확장할 때는 아래 기본형을 우선 사용한다.

```text
Assets/
  _Core/
    Runtime/
    Editor/

  _Shared/
    Art/
    Audio/
    UI/
    Data/

  _Features/
    Gameplay/
    Grid/
    Boxes/
    Stages/
    Gimmicks/
    UI/
```

## 5. Gameplay 배치 규칙

실시간 슬라이딩 규칙은 반드시 `Assets/_Features/Gameplay` 아래에 둔다.

```text
Assets/_Features/Gameplay/
  Gameplay_Loop/
  Gameplay_Slide/
  Gameplay_Collision/
  Gameplay_BoardState/
  Gameplay_Checkpoint/
```

다음은 `Core`로 올리지 않는다.

- 슬라이드 입력 해석
- 충돌 해결
- 밀기 순서 계산
- 정지 지점 판정
- 타일 반응 순서
- 보드 상태 스냅샷 정의

## 6. ScriptableObject 규칙

AI는 SO를 만들 때 전역 데이터 폴더를 만들지 않는다.

좋은 예:

```text
Assets/_Features/Boxes/Box_Heavy/Box_Heavy.asset
Assets/_Features/Gameplay/Gameplay_Collision/CollisionRule.asset
Assets/_Features/Stages/Stage_Factory_01/Stage_Factory_01.asset
```

나쁜 예:

```text
Assets/DataAssets/Box_Heavy.asset
Assets/ScriptableObjects/CollisionRule.asset
```

## 7. Vertical Slice 템플릿

새 박스, 타일 기믹, 스테이지, UI 기능을 추가할 때는 아래 템플릿을 따른다.

```text
Assets/_Features/<FeatureGroup>/<SliceName>/
  <SliceName>.cs
  <SliceName>.asset
  <SliceName>.prefab
  <SliceName>_VFX.prefab
  <SliceName>_Icon.png
```

필요한 파일만 생성하고, 없는 타입을 억지로 만들지는 않는다.  
하지만 생성한 리소스는 가능한 한 같은 Slice 폴더에 유지한다.

## 8. Addressables 규칙

- Group는 Feature 단위 또는 Shared 단위로 나눈다.
- 파일 타입 기준 Group 분리는 피한다.
- Feature 삭제 시 해당 Group도 함께 제거 가능해야 한다.

권장 예:

- `Shared_Common_UI`
- `Shared_Common_VFX`
- `Feature_Gameplay`
- `Feature_Boxes_Heavy`
- `Feature_Stages_Factory`

## 9. asmdef 규칙

AI가 asmdef를 만들 때는 아래 패턴을 따른다.

- `Game.Core`
- `Game.Core.Editor`
- `Game.Shared`
- `Game.Feature.Gameplay`
- `Game.Feature.Grid`
- `Game.Feature.Boxes`
- `Game.Feature.Stages`
- `Game.Feature.Gimmicks`
- `Game.Feature.UI`

참조 방향:

- `Game.Shared -> Game.Core` 가능
- `Game.Feature.* -> Game.Core`, `Game.Shared` 가능
- `Game.Core -> Game.Feature.*` 금지
- `Game.Shared -> Game.Feature.*` 금지

## 10. 네이밍 규칙

- 실제 계층 폴더: `_Core`, `_Shared`, `_Features`
- Slice 폴더: `도메인_기능`
- 예: `Box_Heavy`, `Gameplay_Collision`, `UI_LevelResult`
- asmdef: `Game.Feature.<Name>`
- Addressables Group: `Feature_*`, `Shared_*`

## 11. AI 실행 지침

AI는 사용자가 구조 설계, 폴더 생성, 파일 배치를 요청할 때 아래 절차를 따른다.

1. 먼저 파일의 변경 이유를 분류한다.
2. 개념적으로 `Core / Shared / Features` 중 하나를 결정한다.
3. `Feature`라면 반드시 Vertical Slice 폴더를 먼저 잡는다.
4. SO는 해당 Slice 내부에 생성한다.
5. 실시간 슬라이딩 규칙은 무조건 `Assets/_Features/Gameplay` 아래에 둔다.
6. 타입 기반 전역 폴더를 추가하지 않는다.
7. 의존성이 역전되면 구조를 다시 조정한다.

## 12. 한 줄 원칙

가장 중요한 문장:

> 패턴은 Core, 규칙은 Feature

전체 구조 요약:

> Feature로 사고하고, Shared로 재사용하고, Core로 고정한다.
