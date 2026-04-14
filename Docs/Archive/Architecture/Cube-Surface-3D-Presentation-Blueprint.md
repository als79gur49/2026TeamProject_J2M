> Archived historical document.
> This file is not part of the active truth-source chain. Start with [Docs/Architecture/README.md](../../Architecture/README.md).
> Archive index: [Docs/Archive/README.md](../README.md).

# Cube Surface 3D Presentation Blueprint

## 1. 목적

이 문서는 현재 `Assets/_Features/Gameplay`의 큐브 4면 authoritative 로직을 유지한 채, 테스트 씬과 런타임 표시 계층을 2D strip 표현에서 3D cube 표현으로 전환하기 위한 최종 설계 기준을 정의한다.

이 문서는 아래 문서의 규칙을 전제로 한다.

- `Docs/Architecture/Cube-Surface-Gameplay-Blueprint.md`
- `Docs/Architecture/Deterministic-Tick-Simulation-Blueprint.md`
- `Docs/Architecture/Cube-Surface-Gameplay-Implementation-Plan.md`
- `Docs/Architecture/Cube-Surface-3D-Presentation-Implementation-Plan.md`

핵심 목표는 규칙 변경이 아니라 표시 계약 변경이다.

- `WorldState`, `TickPipeline`, phase rule은 유지한다.
- `View`와 scene composition만 3D cube 기준으로 재정의한다.
- 현재 4 gameplay faces `Floor`, `Front`, `Ceiling`, `Back`는 그대로 유지한다.
- 실제 시뮬레이션 대상은 계속 `BottomFace + FrontFace` 두 면뿐이다.

## 2. 최종 채택 방향

### 2-1. 채택

- 로직은 4면 authoritative 구조를 유지한다.
- View는 큐브 shell 기준 3D 공간에 투영한다.
- 활성 2면의 엔티티만 gameplay-visible 상태로 렌더링한다.
- 비활성 2면은 기본 화면에서 렌더링하지 않는다.
- 비활성 2면 shell이 필요하면 debug mode에서만 선택적으로 렌더링한다.
- topology 변경은 world mutation이 아니라 presentation rotation으로 표현한다.
- 카메라는 perspective를 사용하되, 입력 의미는 기존 board-relative 규칙을 유지한다.
- 카메라는 front wall을 정면에 가깝게 유지하되, floor도 함께 보이는 상부 전방 시점으로 구성한다.

### 2-2. 비채택

- 6면 gameplay 확장
- 카메라 기준 이동 입력 재해석
- Tick 내부에 Unity physics 추가
- wall, floor, entity를 collider 기반 물리 객체로 재설계
- View에서 inactive face 엔티티를 gameplay object처럼 노출

## 3. 핵심 철학

- `TickResult`만으로 presentation이 완결되어야 한다.
- presenter는 여전히 authoritative state를 바꾸지 않는다.
- 3D 전환은 `projection contract`, `pose contract`, `camera contract`를 명확히 분리해서 구현한다.
- 엔티티와 보드 surface는 같은 공간 규칙을 공유해야 한다.
- 테스트는 2D strip 좌표가 아니라 `cell -> local pose -> world pose` 규약을 검증한다.

## 4. 월드 표시 모델

### 4-1. gameplay cube 정의

게임플레이 큐브는 4면만 authoritative하다.

- `BottomFace`: 현재 플레이 가능한 바닥 면
- `FrontFace`: 현재 보이는 전면 벽
- `TopVisibleFace`: `Next(FrontFace)` = 현재 천장 면
- `BackVisibleFace`: `Prev(BottomFace)` = 현재 뒷면

이 중 gameplay-visible은 `BottomFace`, `FrontFace`만이다.

### 4-2. inactive face 표시 정책

- inactive face의 board shell은 기본 정책에서 렌더링하지 않는다.
- inactive face의 엔티티는 기본 정책에서 렌더링하지 않는다.
- inactive face shell이나 엔티티를 보여주고 싶다면 별도 debug mode로 제한한다.

이 정책으로 현재 snapshot query와 visibility 정책을 보존한다.

### 4-3. 셀 중심 규약

모든 `SurfaceCell`은 해당 face 위의 "타일 중심점" 하나를 가진다.

- planar center:
  - `u = x - MinX`
  - `v = y - MinY`
- cell center local offset:
  - `((u + 0.5f) * CellSize, (v + 0.5f) * CellSize)`

이 중심점은 entity와 floor tile이 공유한다.

## 5. 공간 좌표 계약

### 5-1. 좌표 계층

좌표는 3단계로 나눈다.

1. `Cell Local`
- 특정 face 평면 안의 셀 중심 좌표

2. `Board Local`
- 큐브 중심을 원점으로 하는 로컬 3D 좌표

3. `World`
- board root transform이 적용된 최종 월드 좌표

### 5-2. pose 계약

모든 view object는 world pose를 직접 계산하지 않는다.

- presenter는 `Board Local Pose`를 계산한다.
- `GameplayBoardRoot`가 이를 world로 승격한다.

기존 `GameplayEntityView.ApplyPose(worldPosition, worldRotation)`는 아래 방향으로 개편한다.

- 선택안 A
  - `ApplyLocalPose(Vector3 localPosition, Quaternion localRotation)`
  - view는 board root의 child로 유지
- 선택안 B
  - `ApplyPose(GameplayPoseSpace space, Vector3 position, Quaternion rotation)`
  - 초기 전환 비용이 크므로 비채택

채택안은 A다.

### 5-3. face frame 정의

각 face는 다음 로컬 frame을 가진다.

- `Origin`
  - face 좌하단이 아니라 face 중심
- `Right`
  - 셀의 +x 방향
- `Up`
  - 셀의 +y 방향
- `Normal`
  - face 바깥쪽 법선

`BottomFace`, `FrontFace`, `TopVisibleFace`, `BackVisibleFace`의 frame은 현재 `CubeTopologyState` 기준으로 계산한다.

추가 기준:

- `BottomFace`는 cube의 내부 바닥면으로 해석한다.
- `BottomFace`의 바깥쪽 법선은 `Down`, 내부 방향은 `Up`이다.
- `BottomFace`의 `+Y`는 `FrontFace` shared edge 방향이며, world `+Z`와 일치한다.
- `FrontFace`의 `MinY` 행은 floor shared edge에 닿는 하단 행이고, `+Y`는 그 edge에서 위로 올라간다.

### 5-4. depth 규약

surface와 entity의 겹침을 피하기 위해 법선 방향 오프셋을 고정한다.

- floor/base tile center:
  - face plane 기준 두께 절반만큼 배치
- unit interior mount:
  - `EntitySurfaceOffset = 0.08f * CellSize`
- projectile interior mount:
  - `ProjectileSurfaceOffset = 0.18f * CellSize`
- wall thickness:
  - `0.9f * CellSize`
- wall height:
  - `1.0f * CellSize`

핵심은 surface마다 다른 깊이 규약을 쓴다는 점이다.

- surface tile은 face plane에서 cube 내부 방향으로 반 두께만큼 들어간다.
- entity root는 face plane에서 cube 내부 방향으로 오프셋된다.
- unit, box, wall, projectile model center는 모두 cube 내부 방향으로 절반 두께만큼 더 이동해 실제 메시가 cube 내부 쪽에 놓이게 한다.
- 즉 "면 위에 붙는다"의 의미는 cube 외부가 아니라 cube 내부 공간 기준이다.

## 6. 새 presentation 구성요소

### 6-1. `GameplayBoardRoot`

신규 MonoBehaviour.

책임:

- board shell, entity root, camera target root의 공통 부모
- topology motion 시 로컬 회전 애니메이션 적용
- board-local -> world 변환의 단일 기준점 제공

권장 구조:

```text
GameplaySceneHost
  GameplayBoardRoot
    BoardSurfaceRoot
    EntityRoot
    CameraTargetRoot
```

### 6-2. `GameplayCubeProjector`

기존 `GameplaySurfaceProjector` 대체 타입.

책임:

- `SurfaceCell + topology -> Board Local Pose` 계산
- 활성 face center, cube center, visible bounds 계산
- scene label이나 debug gizmo가 재사용 가능한 projection API 제공

public contract 초안:

```csharp
public readonly struct ProjectedCellPose
{
    public Vector3 LocalPosition { get; }
    public Quaternion LocalRotation { get; }
    public Vector3 Normal { get; }
}

public sealed class GameplayCubeProjector
{
    public GameplayCubeProjector(BoardBounds boardBounds, float cellSize);
    public bool TryProjectEntityCell(
        SurfaceCell cell,
        CubeTopologyState topology,
        EntityType entityType,
        out ProjectedCellPose pose);
    public bool TryProjectSurfaceCell(
        SurfaceCell cell,
        CubeTopologyState topology,
        out ProjectedCellPose pose);
    public Bounds GetVisibleCubeBounds(CubeTopologyState topology);
    public Vector3 GetCubeCenter();
}
```

중요 규칙:

- entity projection은 active face만 허용
- surface projection은 active face 또는 debug-visible face 정책에 따라 분리
- `gridOrigin`은 제거한다
- cube center 기반 대칭 좌표계를 사용한다

### 6-3. `GameplayBoardSurfaceRenderer`

신규 MonoBehaviour.

책임:

- 현재 topology에 대응하는 cube shell visual 생성/갱신
- 각 face별 floor/wall plane tile cube 생성
- 비활성 face 장식 렌더링 여부 관리

초기 정책:

- 프리팹이 없으므로 primitive cube로 생성
- 런타임 재생성보다 pool 재사용 우선
- material은 face 역할별로 분리
- 기본 표시 면은 `BottomFace`, `FrontFace` 두 개만 사용
- `TopVisibleFace`, `BackVisibleFace`는 debug mode에서만 선택적으로 사용

권장 material role:

- active bottom face
- active front face
- wall entity
- unit
- box
- projectile
- debug decorative top face
- debug decorative back face

### 6-4. `GameplayCameraRig`

신규 MonoBehaviour 또는 host 내부 helper.

책임:

- perspective camera positioning
- cube center 추적
- topology rotation 동안 framing 유지
- 필요 시 damping 제공

카메라 계약:

- 입력 의미는 board-relative로 유지
- 카메라는 항상 cube를 보는 observer다
- 회전 시 camera가 큐브를 따라 움직이기보다, board root 회전을 따라 중심을 본다

초기 권장값:

- projection: perspective
- pitch: `18 degrees`
- yaw: `0 degrees`
- distance: board diagonal 기반 자동 계산

### 6-5. `GameplayEntityView` 확장

기존 `GameplayEntityView`는 entityId만 저장하고 world pose를 직접 적용한다.

변경 책임:

- `ApplyLocalPose` 지원
- optional child visual root 지원
- entity root transform과 model root transform 분리

권장 구조:

```text
GameplayEntityView
  ModelRoot
```

용도:

- entity facing rotation과 flip arc 보간은 `GameplayEntityView`
- mesh 방향 보정은 `ModelRoot`

## 7. 기존 타입 변경 설계

### 7-1. `GameplayTickViewPresenter`

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs`

변경 원칙:

- strip continuity anchor 개념 제거
- topology motion은 board root rotation track으로 치환
- entity motion track은 board-local pose 기준으로 유지

현재 제거 대상:

- `ContinuityAnchor`
- `GetActiveStripCenter`
- `GetContinuityStepOffset`
- `StripCenterChanged`
- Y축 offset 기반 topology interpolation

신규 상태:

- `_committedBoardRotation`
- `_presentedBoardRotation`
- `_boardRotationTrack`
- `_projector : GameplayCubeProjector`

신규 presenter 책임:

- final topology를 board root rotation으로 샘플링
- entity source/destination cell을 board-local pose로 변환
- entity motion과 topology motion을 서로 독립된 track으로 관리

중요 규칙:

- entity motion은 여전히 `TickPresentationData`만 사용
- topology motion이 있더라도 cell projection 기준은 `SourceTopology`와 `DestinationTopology`를 정확히 따른다
- view interpolation은 deterministic ordering을 바꾸지 않는다

### 7-2. `DefaultGameplayEntityViewFactory`

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/DefaultGameplayEntityViewFactory.cs`

변경 원칙:

- `PrimitiveType.Quad` 제거
- `PrimitiveType.Cube` 기반 visual 생성
- entity type별 scale profile 분리

초기 scale 규약:

- Unit:
  - `(0.78, 0.78, 0.78) * CellSize`
- Box:
  - `(0.82, 0.82, 0.82) * CellSize`
- Projectile:
  - `(0.35, 0.35, 0.55) * CellSize`
- Wall entity:
  - `(0.96, 0.96, 0.96) * CellSize`

추가 규칙:

- collider는 계속 제거
- material 인스턴싱은 유지
- player만 별도 색상 유지
- box와 wall은 model center를 cube 내부 쪽으로 이동시켜 outer shell 밖으로 새지 않게 한다

### 7-3. `GameplaySceneHost`

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHost.cs`

신규 초기화 순서:

1. `GameplayBoardRoot` 확보
2. `GameplayEntityViewRegistry`를 entity root에 연결
3. `GameplayBoardSurfaceRenderer` 초기화
4. `GameplayTickViewPresenter` 초기화
5. `GameplayCameraRig` 초기화
6. `PresentInitial`

host 신규 책임:

- board root lifetime 관리
- board surface renderer에 topology 변경 전달
- camera rig target을 cube center 기준으로 업데이트

### 7-4. `GameplayShowcaseSceneInstallerBase`

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayShowcaseSceneInstallerBase.cs`

변경 원칙:

- orthographic camera 설정 제거
- perspective camera 기본값 채택
- `CalculateCenteredGridOrigin` 제거 또는 deprecated 처리
- 새 `CreateDefaultCameraRigSettings` 제공

grid origin이 사라지는 이유:

- 3D cube는 strip layout이 아니라 center-origin cube layout이 기준이기 때문이다.

### 7-5. `GameplayShowcaseSceneBuilder`

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Host/Editor/GameplayShowcaseSceneBuilder.cs`

변경 원칙:

- `ProjectFloorCell` 제거
- 라벨은 world-space text가 아니라 screen-space UI 또는 board-local anchor로 재구성
- scene 생성 시 `GameplayBoardRoot`와 camera rig 기본 구조를 함께 세팅

권장 채택:

- showcase annotation은 scene UI canvas로 이동
- 런타임 보드와 분리해서 배치한다

## 8. topology rotation 표현 설계

### 8-1. 채택 표현

topology motion은 "보드가 굴러간다"로 표현한다.

- authoritative topology는 여전히 tick 결과에만 존재
- view는 `SourceTopology -> DestinationTopology` 사이를 회전 보간한다
- 카메라는 큐브 중심을 본다

### 8-2. 회전 축

- `Forward`
  - board local X축 기준 음/양 회전 중 하나로 고정
- `Backward`
  - 반대 회전

정확한 부호는 visual 기준으로 정하되, 테스트와 일치해야 한다.

### 8-3. duration

- topology rotation duration은 기존 push motion duration을 재사용하거나 별도 설정 가능
- 1차 채택은 기존 `PushMotionDurationSeconds` 재사용

## 9. motion 설계

### 9-1. Move

- source cell과 destination cell을 각각 대응 topology로 projection한다
- 직선 보간

### 9-2. Push Slide

- `Move`와 같은 직선 보간
- cross-face일 경우도 source/destination local pose만 다를 뿐 동일한 clip로 처리

### 9-3. Flip

- 기존 arc motion은 유지
- arc는 board local up이 아니라 source/destination midpoint와 face normal을 조합한 local arc 축 기준으로 계산한다

이유:

- 3D 공간에서 월드 Y축 arc를 쓰면 face orientation이 바뀔 때 부자연스럽다.

### 9-4. Projectile

- projectile은 더 높은 surface offset 사용
- direction facing은 진행 방향 기준으로 회전

## 10. board shell 설계

### 10-1. tile 생성

각 visible face는 `BoardBounds` 범위만큼 tile cube를 가진다.

- tile count per face:
  - `Width * Height`
- visible shell total:
  - 기본값: `2 * Width * Height`
  - debug decorative 포함: `4 * Width * Height`

### 10-2. 타일 종류

- Bottom tile
- Front tile
- Debug Top decorative tile
- Debug Back decorative tile

### 10-3. wall 표현

초기 설계는 wall entity를 따로 surface shell과 합치지 않는다.

이유:

- 현재 wall은 authoritative entity이며 blocker semantics를 가진다.
- wall을 정적 geometry로 옮기면 scene authoring과 entity query가 갈라진다.

따라서 1차는 다음을 채택한다.

- 바닥/면 shell은 static visual
- wall은 entity cube visual

## 11. 테스트 설계

### 11-1. 수정 대상

- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/RuntimeBoardBoundsGuardTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/PlayMode/PlayerMovementPlayModeTests.cs`

### 11-2. 제거할 2D 가정

다음 검증은 제거 또는 대체한다.

- front face가 bottom face 위쪽 Y에 놓인다
- continuity anchor가 Y축으로 이동한다
- camera target이 strip center를 따른다

단계 0에서 교체 매핑을 아래처럼 고정한다.

- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/RuntimeBoardBoundsGuardTests.cs`
  - `GameplaySurfaceProjector_ProjectsFrontFaceAboveBottomFace`
    - `GameplayCubeProjector_ProjectsBottomFaceToHorizontalPlane`
    - `GameplayCubeProjector_ProjectsFrontFaceToVerticalPlane`
  - `GameplayTickViewPresenter_PresentsOnlyBottomAndFrontFaces`
    - `GameplayTickViewPresenter_PresentsOnlyActiveFaceEntitiesIn3D`
  - `GameplayTickViewPresenter_ShiftsContinuityAnchorOnForwardRotation`
    - `GameplayTickViewPresenter_TopologyMotion_RotatesBoardRoot`
  - `GameplayTickViewPresenter_ShiftsContinuityAnchorOnBackwardRotation`
    - `GameplayTickViewPresenter_TopologyMotion_RotatesBoardRoot`
  - `GameplayTickViewPresenter_TopologyMotion_MidpointInterpolatesContinuityAnchor`
    - `GameplayTickViewPresenter_TopologyMotion_RotatesBoardRoot`
  - `GameplaySceneHost_InterpolatesCameraTargetWithPresentedTopologyMotion`
    - `GameplaySceneHost_CameraRigTracksCubeCenter`
- `Assets/_Features/Gameplay/Gameplay_Tests/PlayMode/PlayerMovementPlayModeTests.cs`
  - move/push/flip/repeat/spawn/blocked-cell 시나리오 커버리지는 유지한다.
  - direct `GetViewPosition(...)=new Vector3(...)` 단정은 strip-space oracle이므로
    `cell -> board-local pose -> world pose` 또는 active-face visibility oracle로 교체한다.

### 11-3. 신규 검증

필수 테스트:

1. `GameplayCubeProjector_ProjectsBottomFaceToHorizontalPlane`
- bottom face cell이 수평면 위에 배치되는지 검증

2. `GameplayCubeProjector_ProjectsFrontFaceToVerticalPlane`
- front face cell이 수직면 위에 배치되는지 검증

3. `GameplayCubeProjector_RejectsInactiveFaceEntityProjection`
- inactive face entity는 기본 visible projection에서 실패하는지 검증

4. `GameplayTickViewPresenter_PresentsOnlyActiveFaceEntitiesIn3D`
- active face 엔티티만 렌더링되는지 검증

5. `GameplayTickViewPresenter_TopologyMotion_RotatesBoardRoot`
- topology 변경 시 board root rotation이 보간되는지 검증

6. `GameplaySceneHost_CameraRigTracksCubeCenter`
- camera target이 strip center가 아니라 cube center를 따르는지 검증

7. `DefaultGameplayEntityViewFactory_CreatesCubeBasedViews`
- primitive type 변경과 scale contract 검증

### 11-4. 유지될 테스트

- `RuntimeBoardBoundsGuardTests`
  - bounded board guard
  - world factory guard
  - projectile cadence normalization
- `GameplayViewProjectionTests`
  - detach/remove visibility sequencing
  - move/push/projectile/flip motion interpolation
  - committed world query와 presented motion의 분리 검증
- `PlayerMovementPlayModeTests`
  - input cadence
  - push/flip priority
  - repeat lock
  - spawned entity visibility
  - blocked-cell drift 방지

이들은 logic contract이므로 대체로 유지한다.

## 12. 구현 단계

### 12-1. 1단계: projection contract 교체

- `GameplayCubeProjector` 추가
- presenter를 board-local pose 기준으로 개편
- 관련 unit test 추가

완료 조건:

- active face entity를 3D local pose로 배치 가능
- strip continuity 개념 제거

### 12-2. 2단계: board root와 topology rotation

- `GameplayBoardRoot` 추가
- presenter topology motion을 rotation track으로 교체
- camera target 연동

완료 조건:

- player surface traversal 시 큐브가 회전하는 연출이 동작

### 12-3. 3단계: entity cube 전환

- `DefaultGameplayEntityViewFactory`를 cube 기반으로 교체
- entity scale/material profile 분리

완료 조건:

- 테스트 씬의 유닛, 박스, 벽, 투사체가 모두 cube 기반으로 보임

### 12-4. 4단계: board surface renderer 추가

- `GameplayBoardSurfaceRenderer` 추가
- active face shell 시각화
- inactive face shell은 debug toggle로만 허용

완료 조건:

- 바닥과 벽면이 cube tile 기반 3D shell로 표시됨

### 12-5. 5단계: camera와 showcase scene 재구성

- installer camera 정책 교체
- scene builder annotation 구조 교체
- showcase scene 재생성

완료 조건:

- `CubeSurfaceTraversalShowcase`
- `BoxInteractionShowcase`
- `CombinedGameplayShowcase`

세 씬이 3D cube presentation 기준으로 정상 동작

## 13. 마이그레이션 정책

- 2D strip 구현은 한 번에 제거하지 않는다.
- projector 교체 전에는 기존 테스트를 보존한다.
- 새 3D presenter가 안정화되면 strip 전용 API를 제거한다.
- 한 단계에서 로직과 view를 동시에 크게 바꾸지 않는다.

## 14. 리스크와 대응

### 14-1. pose space 충돌

리스크:

- world pose 직접 적용과 board root 회전이 중복될 수 있다.

대응:

- entity pose를 board-local 기준으로 단일화한다.

### 14-2. inactive face 혼동

리스크:

- 4면 모두 보이면 플레이어가 gameplay-active로 오해할 수 있다.

대응:

- inactive face는 기본 화면에서 아예 숨긴다
- 필요 시 debug에서만 decorative material 사용
- 엔티티는 기본 렌더링하지 않는다

### 14-3. 테스트 대량 파손

리스크:

- Y축 strip 기준 테스트가 대거 깨진다.

대응:

- projection 전용 unit test를 먼저 추가한 뒤 presenter 교체

## 15. 최종 설계 결론

이번 전환의 authoritative 기준은 다음 세 줄로 요약된다.

- 로직은 현재 4면 deterministic tick 구조를 유지한다.
- view는 `SurfaceCell -> Board Local Pose -> World Pose`의 3D cube 계약으로 재정의한다.
- 기본 화면은 활성 2면만 노출하고, bottom/front seam은 약 1칸 gap으로 벌리며, box/wall은 cube 내부에 mount하되 small reveal로 edge readability를 유지하고, `FrontFace`는 world `+Z`에 놓이며, 카메라는 world `-Z` 쪽에서 front wall과 floor를 함께 보는 전방 상부 시점으로 고정한다.
