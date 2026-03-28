# Cube Surface Gameplay Implementation Plan

## 1. 목적

이 문서는 `Docs/Architecture/Cube-Surface-Gameplay-Blueprint.md`를 현재 Unity C# 구현으로 내리기 위한 실행 계획서다.

목표는 다음 세 가지를 코드 레벨에서 먼저 고정하는 것이다.

- 큐브 4면 구조를 authoritative board-state로 반영한다.
- `Item`, `Push`, `Flip`을 `Movement` 단계로 통합한다.
- `Cleanup` 지연 삭제와 즉시 점유 상실을 동시에 표현할 수 있게 만든다.

## 2. 구현 원칙

- 현재 틱 순서 `Movement -> Attack -> Cleanup`는 유지한다.
- 기존 시스템을 한 번에 갈아엎지 않고 `BoardState -> Movement -> Cleanup -> Attack 정리 -> View` 순으로 옮긴다.
- 먼저 데이터 표현을 바꾸고, 그 위에 규칙을 다시 얹는다.
- 테스트를 먼저 고쳐서 새 규칙을 authoritative하게 만든다.

## 3. 작업 단계

### 3-1. 1단계: 보드 상태 타입 확장

목표는 큐브 면과 면 포함 좌표를 표현할 수 있게 하는 것이다.

작업:

- `FaceId` 추가
- `SurfaceCell` 추가
- `CubeTopologyState` 추가
- `CubeRotationKind` 추가
- `EntityBoardPresence` 추가
- `EntityBoardPresence`는 `Occupying`, `Detached`만 표현하고 삭제 예약 의미를 포함하지 않도록 정리
- `EntityState`의 `position`을 `SurfaceCell`로 교체
- `BoxCapabilities`를 `Push`, `Flip`, `Item`, `Destroy` 기준으로 재정의

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/EntityState.cs`
- 신규 `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/FaceId.cs`
- 신규 `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/SurfaceCell.cs`
- 신규 `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/CubeTopologyState.cs`
- 신규 `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/CubeRotationKind.cs`
- 신규 `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/EntityBoardPresence.cs`

완료 조건:

- 런타임과 테스트가 `SurfaceCell`을 컴파일 가능한 상태
- `EntityState`가 새 위치 모델을 사용

### 3-2. 2단계: 점유와 스냅샷 질의 전환

목표는 활성 면, 비활성 면, 분리된 점유/삭제 예약 규칙을 중앙 질의 계층에서 강제하는 것이다.

체크리스트 기준으로 이 절은 `2단계`와 `3단계`를 함께 다룬다. 아래 세부 설계는 특히 `3단계: 면 전환 질의와 활성 면 필터링 구현`을 구현 기준으로 고정한다.

작업:

- `WorldState` 점유 키를 `SurfaceCell`로 전환
- `WorldSnapshot` 질의를 `SurfaceCell` 기반으로 전환
- 비활성 면 엔티티 제외 로직 추가
- `Detached` 엔티티 점유 제외 로직 추가
- 플레이어 1칸 이동 시 면 전환 여부를 계산하는 중앙 함수 추가
- 박스 슬라이드 시 `Bottom <-> Front` 경계 예외를 반영하는 중앙 함수 추가

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldState.cs`
- `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldSnapshot.cs`
- `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldQueryService.cs`
- `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/SnapshotBuilder.cs`

완료 조건:

- 면 전환 포함 1칸 전진 계산 가능
- 활성 면 필터링이 중앙 질의에서 일관되게 적용

#### 3-2-1. 설계 목표

- `WorldState`는 4면 전체 엔티티와 점유를 authoritative하게 저장한다.
- `WorldSnapshot`은 현재 `CubeTopologyState` 기준의 gameplay-visible 질의만 노출한다.
- 플레이어 면 전환 계산과 박스의 `Bottom <-> Front` 경계 예외는 `WorldQueryService` 한 곳에만 둔다.
- `MovementExpander`와 이후 단계는 면 경계 산술을 직접 하지 않고 snapshot query 결과만 사용한다.
- 이 단계는 topology를 "계산"만 하고 실제 변경 커밋은 다음 단계로 넘긴다.

#### 3-2-2. 책임 분해

- `WorldState`
  - 모든 face의 `entitiesById`, `unitOccupancy`, `projectileOccupancy`를 보관한다.
  - spawn/move legality 검사는 gameplay filter가 아니라 authoritative placement 규칙으로 수행한다.
  - inactive face 엔티티도 저장 가능해야 하므로 active-face 정책을 직접 소유하지 않는다.
- `WorldSnapshot`
  - phase 코드가 호출하는 read facade다.
  - 활성 면 필터, 면 전환, 박스 surface slide 질의를 public API로 노출한다.
  - `Vector2Int` overload는 마이그레이션 호환용으로만 유지한다.
- `WorldQueryService`
  - active/inactive face 판정, `Detached` 제외, 회전 경계 계산, shared edge slide 예외를 중앙화한다.
  - read-side query와 write-side placement validation이 같은 helper를 재사용하게 만들어 read/write 규칙이 갈라지지 않게 한다.
- `SnapshotBuilder`
  - 단순 snapshot 생성 wrapper로 유지한다.
  - topology-aware 정책은 넣지 않는다.

#### 3-2-3. 고정 API

이 단계에서 외부 phase가 의존할 질의 API는 아래 시그니처로 고정한다.

```csharp
public bool TryGetUnitAt(SurfaceCell cell, out EntityState entity);
public bool TryGetProjectileAt(SurfaceCell cell, out EntityState entity);
public bool IsTerrainBlockedForUnit(SurfaceCell cell);
public bool IsBlockedForUnit(SurfaceCell cell);
public bool TryResolvePlayerStep(
    SurfaceCell origin,
    Direction direction,
    out SurfaceCell destination,
    out CubeRotationKind rotationKind,
    out CubeTopologyState updatedTopology);
public bool TryGetSurfaceBoxSlideDestination(
    SurfaceCell origin,
    Vector2Int delta,
    out SurfaceCell destination,
    out SlideStopper stopper);
```

호환을 위해 아래 API는 유지하되 신규 규칙 구현의 주 경로로 쓰지 않는다.

- `TryGetUnitAt(Vector2Int cell, out EntityState entity)`
- `TryGetProjectileAt(Vector2Int cell, out EntityState entity)`
- `IsTerrainBlockedForUnit(Vector2Int cell)`
- `IsBlockedForUnit(Vector2Int cell)`
- `TryResolvePlayerStep(SurfaceCell origin, Vector2Int delta, out SurfaceCell destination, out CubeRotationKind rotationKind, out CubeTopologyState updatedTopology)`
- `TryGetBoxSlideDestination(Vector2Int origin, Vector2Int delta, out Vector2Int destination, out SlideStopper stopper)`

#### 3-2-4. 활성 면 필터 정책

핵심 규칙은 "월드 저장은 전체 4면, gameplay 질의는 활성 2면"이다.

- 활성 면 판단은 `CubeTopologyState.BottomFace`와 `CubeTopologyState.FrontFace`만 사용한다.
- `TryGetUnitAt`, `TryGetProjectileAt`, `IsTerrainBlockedForUnit`, `IsBlockedForUnit`, `TryGetUnitBlocker`, `TryGetPlacementBlocker`, `EnumerateUnitOccupancyOrdered`, `EnumerateProjectileOccupancyOrdered`, `BlocksMovement`, `CanBeTargetedForNewSelection`은 모두 active-face policy를 적용한다.
- `TryGetEntity(int entityId, out EntityState entity)`와 `EnumerateEntitiesOrdered`는 active-face filter를 적용하지 않는다.
  - 이유: `Cleanup`, determinism hash, debug dump는 비활성 면 엔티티도 authoritative하게 봐야 한다.
- `markedForDeath == true`는 삭제 예약이지만, 아직 board 위에 남아 있는 상태로 취급한다.
  - 따라서 `boardPresence == Occupying`이고 active face 위라면 movement blocker와 selection 대상 여부 판정에 계속 영향을 줄 수 있다.
- `boardPresence == Detached`는 gameplay 점유를 즉시 잃은 상태다.
  - occupancy dictionary에 저장하지 않고, query layer도 방어적으로 한 번 더 제외한다.
- `markedForDeath`와 `boardPresence`는 독립 축이다.
  - `Detached` 자체는 제거 조건이 아니고, 실제 삭제 여부는 `markedForDeath` 또는 `hp <= 0`로만 결정한다.
- `TerrainData`와 `BoardBounds`는 이번 단계에서도 planar data로 유지한다.
  - 즉 terrain과 bounds는 `SurfaceCell.face`별로 따로 들지 않고, 같은 `(x, y)` 규칙을 모든 face에 공통 적용한다.

#### 3-2-5. placement query 모드 분리

active-face filter는 모든 placement 검사에 동일하게 적용하면 안 된다. 따라서 placement query는 두 모드로 분리한다.

- `Authoritative`
  - 사용처: `WorldState.SpawnEntity`, `WorldState.MoveEntityTo`
  - inactive face도 합법 위치로 검사할 수 있어야 한다.
  - `Detached`만 제외하고, 나머지 occupying entity는 face와 무관하게 blocker가 된다.
- `Gameplay`
  - 사용처: `WorldSnapshot.TryGetPlacementBlocker`, `TryGetUnitBlocker`, movement/attack phase 질의
  - inactive face cell이면 즉시 "질의 대상 아님"으로 처리한다.
  - active face 위의 occupying entity만 blocker가 된다.

이 분리로 "inactive face 엔티티는 저장 가능하지만 gameplay에는 보이지 않음"이라는 요구를 충족한다.

#### 3-2-6. 플레이어 1칸 전진 질의 알고리즘

`TryResolvePlayerStep`는 geometry/topology query일 뿐이며 occupancy나 terrain은 여기서 보지 않는다.

알고리즘:

1. 입력 방향을 cardinal `Vector2Int`로 변환하고, 대각선/0벡터면 예외로 중단한다.
2. `origin.face`가 활성 면이 아니면 실패한다.
3. 현재 위치가 `BottomFace`이고 `Up` 입력이며 `y == MaxInclusive.y`면 전방 회전을 계산한다.
4. 현재 위치가 `BottomFace`이고 `Down` 입력이며 `y == MinInclusive.y`면 후방 회전을 계산한다.
5. 그 외에는 같은 face에서 `origin + delta`를 계산하고 bounds 안이면 성공, 아니면 실패한다.

전방 회전 결과:

- `rotationKind = Forward`
- `updatedTopology = topology.Rotate(Forward)`
- `destination = SurfaceCell(updatedTopology.BottomFace, origin.x, MinInclusive.y)`

후방 회전 결과:

- `rotationKind = Backward`
- `updatedTopology = topology.Rotate(Backward)`
- `destination = SurfaceCell(updatedTopology.BottomFace, origin.x, MaxInclusive.y)`

추가 규칙:

- `Left`, `Right` 경계는 회전 대상이 아니다.
- `FrontFace`의 윗경계/아랫경계는 플레이어 회전을 일으키지 않는다.
- `BoardBounds.Unbounded`에서는 회전 질의를 열지 않고 기존 planar 1칸 이동 의미를 유지한다.
- 이 단계에서는 topology를 반환만 하고 월드 상태를 바꾸지 않는다. 실제 `BottomFace` 갱신은 movement commit 단계의 책임이다.

#### 3-2-7. 박스 활성 면 슬라이드 질의 알고리즘

`TryGetSurfaceBoxSlideDestination`는 활성 면 사이 shared edge 예외만 반영하고, 박스 스스로 topology를 바꾸지는 않는다.

bounded board 알고리즘:

1. `origin.face`가 활성 면이 아니면 실패한다.
2. `delta`는 orthogonal 1칸 방향만 허용한다.
3. `current = origin`에서 시작해 다음 칸을 반복 계산한다.
4. 다음 칸 계산은 `TryGetNextSurfaceBoxSlideCell` helper가 담당한다.
5. helper는 아래 두 경우에만 face를 바꾼다.
   - `SurfaceCell(BottomFace, x, MaxY)`에서 `Up` -> `SurfaceCell(FrontFace, x, MinY)`
   - `SurfaceCell(FrontFace, x, MinY)`에서 `Down` -> `SurfaceCell(BottomFace, x, MaxY)`
6. 그 외 경계 이탈은 모두 `BoardEdge` stopper로 끝낸다.
7. 다음 칸이 terrain이면 현재 칸을 최종 destination으로 확정하고 `Terrain` stopper를 반환한다.
8. 다음 칸에 active-face occupying entity가 있으면 현재 칸을 최종 destination으로 확정하고 `Entity` stopper를 반환한다.
9. blocker가 없으면 `current = next`로 갱신하고 반복한다.

추가 규칙:

- projectile layer는 slide stopper에서 제외한다.
- `markedForDeath` 엔티티는 stopper가 될 수 있다.
- `Detached` 엔티티는 stopper가 될 수 없다.
- `FrontFace -> Ceiling`, `BottomFace -> Back` 같은 나머지 경계 전이는 열지 않는다.

unbounded board 호환 규칙:

- `BoardBounds.Unbounded`에서는 기존 planar ray-scan 의미를 유지한다.
- 이 모드에서는 face-crossing slide를 지원하지 않는다.
- stopper를 하나도 찾지 못하면 기존 API 의미를 유지하기 위해 `false`를 반환한다.

#### 3-2-8. 레거시 `Vector2Int` 호환 정책

- `Vector2Int` 기반 query는 모두 `SurfaceCell.FromPlanar(cell, topology.BottomFace)`로 해석한다.
- 즉 레거시 query는 "현재 바닥면 평면 질의"만 표현할 수 있다.
- `TryGetBoxSlideDestination(Vector2Int, ...)`는 결과를 planar 좌표로만 돌려주므로 face 정보가 소실된다.
- 따라서 `MovementExpander`, `MovementResolver`, `MovementCommitter`가 topology나 cross-face 결과를 해석해야 하는 단계에서는 반드시 `SurfaceCell` API로 옮겨야 한다.
- 레거시 overload는 기존 테스트와 임시 호출부를 깨지 않기 위한 마이그레이션 어댑터로만 유지한다.

#### 3-2-9. 구현 체크포인트

- `WorldState` 생성과 mutation은 inactive face 엔티티를 정상 보관할 수 있어야 한다.
- snapshot gameplay query는 inactive face와 `Detached`를 일관되게 숨겨야 한다.
- 플레이어 1칸 이동 query는 `destination + rotationKind + updatedTopology`를 한 번에 계산해야 한다.
- 박스 slide query는 `Bottom <-> Front` 공유 경계만 예외로 열고 나머지는 stopper로 닫아야 한다.
- 어느 phase도 면 경계 산술이나 active-face filtering을 직접 구현하지 않아야 한다.

#### 3-2-10. 테스트 기준

이 단계에서 최소한 아래 테스트를 고정한다.

- `WorldSnapshot_TryGetUnitAt_IgnoresInactiveFaceOccupant`
- `WorldSnapshot_TryGetPlacementBlocker_AppliesGameplayFaceAndBoardPresencePolicy`
- `WorldSnapshot_BlocksMovementUntilCleanupEvenWhenEntityIsMarkedForDeath`
- `WorldSnapshot_TryResolvePlayerStep_RotatesForwardFromBottomTopEdge`
- `WorldSnapshot_TryResolvePlayerStep_RotatesBackwardFromBottomBottomEdge`
- `WorldSnapshot_TryResolvePlayerStep_DoesNotRotateFromFrontFaceOrSideEdge`
- `WorldSnapshot_TryGetSurfaceBoxSlideDestination_CrossesBottomFrontSharedEdge`
- `WorldSnapshot_TryGetSurfaceBoxSlideDestination_StopsAtOtherBoardEdges`
- `WorldSnapshot_TryGetSurfaceBoxSlideDestination_IgnoresDetachedButStopsOnMarkedForDeath`
- `WorldSnapshot_TryGetBoxSlideDestination_UsesBottomFaceAsLegacyDefault`

세부 완료 조건:

- movement phase가 topology-aware step query만 호출해도 플레이어 전방/후방 회전 판정을 재현할 수 있음
- box interaction phase가 shared edge 예외를 직접 하드코딩하지 않아도 됨
- determinism/debug용 전체 entity enumeration과 gameplay용 filtered occupancy enumeration이 분리되어 있음

### 3-3. 3단계: 이동 입력 정리

목표는 플레이어 입력이 최종 규칙과 일치하도록 만드는 것이다.

작업:

- `Move`를 기본 입력으로 유지
- 현재 `Throw` 입력을 `Flip` 의미로 치환
- `Interact`는 임시 호환 경로만 남기거나 제거 계획 수립
- 플레이어 이동 의도는 `Move`와 `Flip`만 생산하도록 정리

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/PlayerTickCommand.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayInputHost.cs`
- `Assets/_Features/Gameplay/Gameplay_Entities/Runtime/PlayerLogic.cs`

완료 조건:

- 플레이어 입력이 `Item`, `Push`를 위해 별도 `Attack` intent를 만들지 않음
- `Flip`만 별도 movement command로 남음

### 3-4. 4단계: Movement 규칙 통합

목표는 `Item -> Push -> Flip` 우선순위를 이동 단계에 직접 구현하는 것이다.

작업:

- `MovementExpander`에 단일 판정 순서 구현
- `Item` 처리:
  - 플레이어를 대상 칸으로 이동
  - 박스를 점유에서 제거
  - 박스 삭제 예약
- `Push` 처리:
  - 슬라이드 성공
  - 실패 시 `Push + Destroy`면 파괴
  - 실패 시 `Destroy` 없으면 무효
- `Flip` 처리:
  - 반대편 1칸 이동
  - 앞벽 경계 넘김 금지
- 플레이어 면 전환 시 `TopologyChangeAction` 또는 동등 개념 추가

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Movement/Runtime/Expansion/MovementExpander.cs`
- `Assets/_Features/Gameplay/Gameplay_Model/Runtime/Groups/ActionGroupKind.cs`
- `Assets/_Features/Gameplay/Gameplay_Model/Runtime/Actions/MoveAction.cs`
- 신규 `Assets/_Features/Gameplay/Gameplay_Model/Runtime/Actions/TopologyChangeAction.cs`

완료 조건:

- 박스 상호작용의 authoritative 판단이 `Movement`에만 존재
- `Item`, `Push`, `Flip`이 같은 우선순위 규칙을 공유

### 3-5. 5단계: Movement Commit 확장

목표는 이동 커밋이 좌표 이동뿐 아니라 보드 위상 변경, 점유 상실, 삭제 예약까지 반영하는 것이다.

작업:

- `SurfaceCell` 기반 이동 커밋
- 플레이어 면 전환 시 `BottomFace` 갱신
- `Item` 박스의 `Detached` 전환
- `Item` 박스의 삭제 예약 반영
- `Push + Destroy` 실패 박스의 `Detached` 전환과 삭제 예약 반영
- `boardPresence`와 `markedForDeath`를 같은 커밋 경로에서 다루되 의미는 분리
- 신규 이벤트 로그 추가

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Movement/Runtime/Commit/MovementCommitter.cs`
- `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldStateWriteContext.cs`
- `Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/IWorldWriteContext.cs`

완료 조건:

- 커밋만이 topology, 위치, 점유, 삭제 예약을 변경
- `Detached`는 점유 상실만, `markedForDeath`는 삭제 예약만 의미함
- `Cleanup` 이전에도 점유가 논리적으로 비워질 수 있음

### 3-6. 6단계: Cleanup 보강

목표는 `Cleanup`을 "삭제 예약 소비 + 생존 엔티티 후처리" 단계로 고정하고, 점유 상태 예외를 제거하는 것이다.

작업:

- `hp <= 0` 또는 `markedForDeath` 엔티티만 제거
- `boardPresence == Detached`는 제거 조건으로 해석하지 않음
- 제거 대상 선별과 생존 엔티티 후처리 순서를 고정
- 상태 타이머와 상태 전이는 제거 후 생존 엔티티에만 적용
- 제거 정책이 점유 정책을 직접 읽지 않도록 정리

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Cleanup/Runtime/CleanupProcessor.cs`
- `Assets/_Features/Gameplay/Gameplay_Cleanup/Runtime/RemovalProcessor.cs`

완료 조건:

- `Cleanup`이 `boardPresence` 값에 의존하지 않고 제거 대상을 결정함
- 같은 틱에 점유를 잃은 엔티티도 삭제 예약이 있으면 `Cleanup`에서 실제 삭제됨
- 향후 `Detached but alive` 상태가 추가되어도 `Cleanup` 수정이 필요 없음

### 3-7. 7단계: Attack 단계 정리

목표는 공격 단계에서 박스 상호작용 책임을 제거하는 것이다.

작업:

- `InteractLootDestroy` 경로 제거 또는 비활성화
- `Attack`은 전투/투사체/지연공격만 유지

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Attack/Runtime/Expansion/AttackExpander.cs`
- `Assets/_Features/Gameplay/Gameplay_Attack/Runtime/Commit/AttackCommitter.cs`
- 관련 테스트 파일

완료 조건:

- 박스 관련 authoritative 처리 경로가 `Attack`에 남아 있지 않음

### 3-8. 8단계: View와 좌표 변환

목표는 `SurfaceCell`과 활성 면 상태를 실제 화면에 표현하는 것이다.

작업:

- `SurfaceCell -> WorldPosition` 변환기 추가
- 비활성 면 엔티티 숨김
- 면 전환 시 카메라/뷰 기준 동기화

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayEntityView.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHost.cs`

완료 조건:

- 활성 면만 보이고, 면 전환 결과가 시각적으로 일관됨

### 3-9. 9단계: 샘플 데이터와 테스트 전면 갱신

목표는 규칙을 테스트로 고정하는 것이다.

작업:

- 초기 엔티티 배치를 `SurfaceCell`로 변경
- 박스 속성 이름 변경 반영
- 기존 movement/attack/cleanup 테스트 수정
- 신규 시나리오 테스트 추가

필수 신규 테스트:

- 플레이어 전방 회전
- 플레이어 후방 회전
- `Bottom <-> Front` 박스 슬라이드 지속
- `Flip` 앞벽 경계 금지
- `Item` 획득 후 같은 틱 플레이어 진입
- `Item` 박스는 `Cleanup` 전까지 엔티티는 남아도 점유는 잃는지
- `Push + Destroy` 실패 파괴
- `Push` 단독 실패 유지
- 조합 박스 우선순위 `Item -> Push -> Flip`

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/PlayerMovementInputTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Scenario/MovementPhaseScenarioTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Scenario/AttackPhaseScenarioTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Scenario/CleanupPhaseScenarioTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/PlayMode/PlayerMovementPlayModeTests.cs`

완료 조건:

- 규칙이 테스트로 문서화되고, 리그레션 체크가 가능함

## 4. 권장 구현 순서 체크리스트

- [ ] 1단계: `SurfaceCell`, `FaceId`, `CubeTopologyState` 추가
- [ ] 2단계: `EntityState`, `WorldState`, `WorldSnapshot`를 `SurfaceCell` 기준으로 교체
- [ ] 3단계: 면 전환 질의와 활성 면 필터링 구현
- [ ] 4단계: `PlayerTickCommand`, `PlayerLogic`, `GameplayInputHost`를 `Move + Flip` 중심으로 정리
- [ ] 5단계: `MovementExpander`에 `Item -> Push -> Flip` 통합
- [ ] 6단계: `MovementCommitter`에 topology 변경, `Detached` 전환, 삭제 예약 커밋 추가
- [ ] 7단계: `Cleanup`이 점유 상태와 무관하게 삭제 예약만 소비하도록 수정
- [ ] 8단계: `Attack`에서 박스 상호작용 제거
- [ ] 9단계: view 좌표 변환과 활성 면 표시 반영
- [ ] 10단계: 샘플 씬과 테스트 전체 갱신

## 5. 마이그레이션 메모

- 현재 `Throwable`는 최종 의미상 `Flip`으로 옮긴다.
- 현재 `LootOnInteractDestroy`는 최종 의미상 `Item`으로 옮긴다.
- 현재 `InteractLootDestroy` 공격 경로는 임시 호환 단계 이후 제거 대상이다.
- 현재 `Vector2Int` 중심 테스트는 모두 `SurfaceCell` 중심 시나리오로 바뀐다.

## 6. 완료 정의

아래 조건을 모두 만족하면 이 구현 계획은 완료다.

- 큐브 4면 구조가 authoritative board-state로 동작한다.
- 활성 면 2개만 논리적으로 시뮬레이션된다.
- 플레이어만 면 전환을 트리거한다.
- 박스는 `Bottom <-> Front` 경계만 넘고 회전을 트리거하지 않는다.
- `Item`, `Push`, `Flip`이 모두 `Movement`에서 처리된다.
- `Item -> Push -> Flip` 우선순위가 테스트로 고정된다.
- 점유 상실과 실제 삭제가 분리되어도 모순이 없다.
