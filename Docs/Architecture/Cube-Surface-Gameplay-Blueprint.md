# Cube Surface Gameplay Blueprint

## 1. 목적

이 문서는 큐브 4면 기반 플레이 구조와 박스 상호작용 규칙의 최종 설계 기준을 정의한다.

이 문서는 아래 문서의 상위 구조 원칙을 따른다.

- `Docs/Architecture/Hybrid-Architecture-Rulebook.md`
- `Docs/Architecture/Deterministic-Tick-Simulation-Blueprint.md`
- `AI_HYBRID_STRUCTURE_RULES.md`

이 문서의 목적은 "기능을 임시로 붙이는 방법"이 아니라, 현재 `Assets/_Features/Gameplay` 틱 시뮬레이션 구조 안에서 큐브 면 전환, 활성 면, 박스 규칙을 어떻게 일관된 규칙 체계로 고정할지를 정의하는 것이다.

## 2. 핵심 철학

- 월드는 정육면체 4면 `바닥, 앞벽, 천장, 뒷벽`으로 정의한다.
- 실제 시뮬레이션은 항상 `현재 바닥 + 현재 앞벽` 2면만 대상으로 한다.
- 플레이어만 큐브 회전을 트리거할 수 있다.
- 박스는 회전을 트리거하지 않는 보드 위 객체다.
- `Item`, `Push`, `Flip`은 모두 이동 계층의 규칙으로 처리한다.
- `Attack` 단계는 전투, 투사체, 지연공격만 담당한다.
- 삭제는 `Cleanup`에서 수행하되, 점유 상실은 더 이른 단계에서 일어날 수 있다.
- 조합 박스는 다중 결과를 동시에 내지 않고, 고정 우선순위로 단일 결과를 낸다.

## 3. 월드 모델

### 3-1. 면 정의

- 면 집합은 `Floor`, `Front`, `Ceiling`, `Back` 네 개다.
- 순환 관계는 다음과 같다.
  - `Next(Floor) = Front`
  - `Next(Front) = Ceiling`
  - `Next(Ceiling) = Back`
  - `Next(Back) = Floor`
- `Prev`는 역방향 순환이다.

### 3-2. 활성 면

- 월드 상태는 `BottomFace` 하나를 authoritative하게 보관한다.
- `FrontFace`는 항상 `Next(BottomFace)`로 계산한다.
- 활성 면은 `BottomFace`, `FrontFace`뿐이다.
- 비활성 면은 타일 비가시, 엔티티 비활성, 충돌/공격/지연효과 판정 제외 상태다.

### 3-3. 위치 표현

- 엔티티 위치는 평면 `Vector2Int`가 아니라 면 포함 좌표 `SurfaceCell(Face, x, y)`로 표현한다.
- 좌표 축 정의:
  - `x`: 좌 -> 우
  - `y`: 이전 면 경계 -> 다음 면 경계
- 각 면의 경계 의미:
  - `y = 0`: `Prev(face)`와 닿는 경계
  - `y = H - 1`: `Next(face)`와 닿는 경계

## 4. 면 전환 규칙

### 4-1. 플레이어 회전

- 플레이어가 `BottomFace`의 윗경계에서 `Up` 이동하면 전방 회전이 발생한다.
- 플레이어가 `BottomFace`의 아랫경계에서 `Down` 이동하면 후방 회전이 발생한다.
- 플레이어 체감상 이는 순간이동이 아니라 "인접한 벽면으로 연속 이동"이다.

### 4-2. 전방 회전 테이블

- 조건:
  - 현재 위치 `SurfaceCell(BottomFace, x, H - 1)`
  - 입력 방향 `Up`
- 결과:
  - `BottomFace' = Next(BottomFace)`
  - `FrontFace' = Next(FrontFace)`
  - `Position' = SurfaceCell(BottomFace', x, 0)`
  - `Facing' = Up`

### 4-3. 후방 회전 테이블

- 조건:
  - 현재 위치 `SurfaceCell(BottomFace, x, 0)`
  - 입력 방향 `Down`
- 결과:
  - `BottomFace' = Prev(BottomFace)`
  - `FrontFace' = BottomFace`
  - `Position' = SurfaceCell(BottomFace', x, H - 1)`
  - `Facing' = Down`

### 4-4. 좌우 경계

- 좌우 면은 게임 공간에 포함하지 않는다.
- 따라서 `Left`, `Right` 경계는 회전 대상이 아니며 일반 경계로 처리한다.

## 5. 박스 경계 통과 규칙

- 박스는 회전을 발생시키지 않는다.
- 박스는 활성 면 사이의 공유 경계인 `Bottom <-> Front`만 통과할 수 있다.
- 경계 통과 테이블:
  - `SurfaceCell(BottomFace, x, H - 1)`에서 `Up` 슬라이드 -> `SurfaceCell(FrontFace, x, 0)`로 이어서 슬라이드 지속
  - `SurfaceCell(FrontFace, x, 0)`에서 `Down` 슬라이드 -> `SurfaceCell(BottomFace, x, H - 1)`로 이어서 슬라이드 지속
- 그 외 경계는 정상 stopper다.

## 6. 상호작용 우선순위

- 고정 우선순위는 다음과 같다.
  - `Item -> Push -> Flip`
- 이 우선순위는 박스 속성 조합 시 항상 동일하게 적용한다.
- 한 입력에서 하나의 결과만 확정한다.

## 7. 박스 속성 정의

- `Item`
  - 플레이어가 `Move`로 해당 박스 칸에 접촉하면 아이템을 획득한다.
  - 플레이어는 최종적으로 그 칸에 들어간다.
- `Push`
  - 플레이어가 이동하려는 칸의 박스를 진행 방향으로 슬라이딩시킨다.
- `Flip`
  - 플레이어 옆 1칸의 박스를 플레이어 반대편 1칸으로 옮긴다.
  - `Flip`은 앞벽 경계를 넘을 수 없다.
- `Destroy`
  - 단독 파괴 속성이 아니라 `Push` 실패 시 파괴를 허용하는 보조 속성이다.
  - 즉 `Push + Destroy` 조합일 때만 "밀 수 없으면 파괴"가 성립한다.

## 8. 이동 단계 규칙

### 8-1. 일반 이동

- 플레이어 기본 입력은 `Move`다.
- `Push`와 `Flip`은 `Move`와 분리된 별도 입력으로 취급한다.
- 이동 단계는 입력 종류에 따라 아래 규칙을 적용한다.
  - `Move`: 면 전환 포함 목표 칸 계산 후 `Item` 또는 일반 이동 판정
  - `Push`: 인접 `Push` 박스 slide 판정
  - `Flip`: 인접 `Flip` 박스 재배치 판정

### 8-2. Item

- `Move` 목표 칸이 `Item` 박스면 `Item`이 우선한다.
- 결과:
  - 플레이어 아이템 획득
  - 플레이어가 그 칸으로 이동
  - 박스는 보드 점유를 즉시 잃는다
  - 박스는 `Cleanup`에서 제거된다

### 8-3. Push

- `Push` 입력이 인접 `Push` 박스를 가리키면 박스를 진행 방향으로 민다.
- 박스는 첫 stopper 직전까지 슬라이딩한다.
- stopper 후보:
  - 활성 면 경계 중 허용되지 않은 경계
  - 지형
  - 블로킹 엔티티
- 예외:
  - `Bottom <-> Front` 공유 경계에서는 정지하지 않고 계속 슬라이드한다
- 실패 규칙:
  - 밀 수 없고 대상 박스가 `Push + Destroy`면 파괴
  - `Destroy`가 없으면 단순 실패
- 일반 `Move`는 `Push` 박스를 자동으로 밀지 않는다.

### 8-4. Flip

- `Flip` 입력은 국소 재배치다.
- 박스는 플레이어 반대편 1칸으로 이동한다.
- 플레이어는 제자리에 남는다.
- 앞벽 경계 넘김은 불가하다.

## 9. 점유와 삭제 규칙

- `Cleanup`은 실제 엔티티 제거 시점이다.
- 그러나 게임 의미상 점유 상실은 더 이른 단계에서 확정될 수 있다.
- `Item`으로 획득된 박스는 같은 틱 안에 보드 점유를 즉시 잃는다.
- `Push + Destroy` 실패 박스도 파괴가 확정되면 보드 점유를 잃을 수 있다.
- 이를 위해 엔티티는 "삭제 예약"과 "보드 점유 상태"를 분리해서 표현한다.

## 10. 단계별 책임 분리

- `Movement`
  - 면 전환
  - 활성 면 기준 이동 판정
  - `Item`
  - `Push`
  - `Flip`
  - 파괴 예약과 점유 상실 처리
- `Attack`
  - 전투
  - 투사체
  - 지연공격
- `Cleanup`
  - 삭제 예약 엔티티 제거
  - 상태 타이머
  - 상태 전이

## 11. 채택 정책

- 박스 상호작용은 더 이상 `Attack` 단계의 예외 규칙으로 두지 않는다.
- 큐브 면 전환은 view 효과가 아니라 board-state의 authoritative 규칙이다.
- 설계 우선순위는 다음과 같다.
  - 규칙 일관성
  - 결정론 유지
  - 점유/삭제 모순 제거
  - view 표현은 그 다음 단계에서 맞춘다
