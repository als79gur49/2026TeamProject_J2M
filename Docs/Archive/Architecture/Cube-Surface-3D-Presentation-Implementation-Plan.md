> Archived historical document.
> This file is not part of the active truth-source chain. Start with [Docs/Architecture/README.md](../../Architecture/README.md).
> Archive index: [Docs/Archive/README.md](../README.md).

# Cube Surface 3D Presentation Implementation Plan

## 1. 목적

이 문서는 `Docs/Architecture/Cube-Surface-3D-Presentation-Blueprint.md`를 실제 Unity 구현 작업으로 내리기 위한 실행 계획서다.

목표는 다음 세 가지를 코드 레벨에서 안전하게 고정하는 것이다.

- 현재 4면 authoritative gameplay 로직을 유지한다.
- 2D strip 기반 view 계약을 3D cube 기반 view 계약으로 교체한다.
- 테스트 씬, 런타임 presenter, camera, scene builder를 단계적으로 전환한다.

이 계획서는 "한 번에 3D 씬을 만들기"가 아니라, 현재 동작을 깨지 않는 작은 작업 단위로 migration하는 것을 우선한다.

## 2. 구현 원칙

- `TickPipeline`, `WorldState`, phase semantics는 바꾸지 않는다.
- 먼저 pose space와 projection contract를 분리하고, 그 다음 visual geometry를 바꾼다.
- camera, board shell, entity visual을 한 PR에 섞지 않는다.
- 기존 테스트를 무시하고 밀어붙이지 않는다. 각 단계마다 새 contract를 테스트로 고정한다.
- inactive face policy는 끝까지 유지한다.
  - 엔티티는 active face만 렌더링
  - inactive face shell은 기본값에서 렌더링하지 않고 debug mode에서만 허용

### 2-1. 2026-03-31 시각 수정 기준

이번 수정에서 추가로 고정할 presentation 기준은 아래 세 가지다.

- 기본 화면에는 `BottomFace`, `FrontFace`만 보인다. `TopVisibleFace`, `BackVisibleFace`는 debug mode가 아니면 렌더링하지 않는다.
- box와 wall visual은 cube 외부가 아니라 cube 내부 방향으로 mount 배치한다. entity anchor는 interior offset을 쓰고, model center는 추가로 반 두께만큼 내부로 이동한다.
- 카메라는 front wall을 정면에 가깝게 유지하되 floor도 함께 보이는 front-biased elevated angle을 사용한다. 구현 기본값은 `pitch = 18`, `yaw = 0`이다.

## 3. 구현 범위

### 3-1. 1차 범위

- entity pose를 board-local 기준으로 전환
- `GameplayCubeProjector` 추가
- active face entity를 3D cube 위에 표시
- topology motion을 board rotation으로 표현
- perspective camera rig 적용
- 유닛/박스/벽/투사체를 cube 기반 primitive로 표시
- active board shell 표시
- inactive shell debug toggle 유지
- showcase scene builder와 showcase scene 갱신

### 3-2. 비범위

- 6면 gameplay 확장
- input semantics 재설계
- Unity physics 기반 충돌 전환
- wall entity를 정적 terrain 데이터로 이전
- prefab/FBX 기반 아트 파이프라인 구축

## 4. 작업 분해 개요

구현은 아래 8개 작업 단위로 나눈다.

1. pose space 기반 정리
2. projection contract 분리
3. cube projector 구현
4. board rotation + camera rig
5. cube entity visual 전환
6. board surface renderer 추가
7. showcase scene / builder 전환
8. cleanup / legacy 제거

권장 방식은 단계별 PR 분리다.

## 5. 작업 단계

### 5-1. 0단계: 사전 가드레일 정리

목표는 현재 2D strip 가정이 어디에 박혀 있는지 고정하고, 이후 단계에서 어떤 테스트를 대체해야 하는지 명확히 만드는 것이다.

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/RuntimeBoardBoundsGuardTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/PlayMode/PlayerMovementPlayModeTests.cs`
- `Docs/Architecture/Cube-Surface-3D-Presentation-Blueprint.md`

구현 태스크:

- 2D strip 전제 테스트 목록 정리
- 유지할 logic test와 교체할 presentation test 구분
- 새 3D contract 테스트 명세를 TODO 주석이나 문서 항목으로 고정

검증:

- 어떤 테스트를 유지하고 어떤 테스트를 대체할지 문서상 명확해야 한다

완료 조건:

- 후속 단계에서 "깨진 테스트를 나중에 보자"는 상태가 남지 않는다

### 5-2. 1단계: Pose Space 기반 정리

목표는 world pose 직접 적용 구조를 board-local pose 구조로 바꾸는 것이다. 이 단계에서는 아직 3D cube geometry를 도입하지 않아도 된다.

진행 상태:

- 완료 (2026-03-30)

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayEntityView.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHost.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs`
- 신규 `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayBoardRoot.cs`

구현 태스크:

- `GameplayBoardRoot` 추가
- host가 아래 hierarchy를 보장하도록 변경

```text
GameplaySceneHost
  GameplayBoardRoot
    BoardSurfaceRoot
    EntityRoot
    CameraTargetRoot
```

- `GameplayEntityView.ApplyLocalPose(...)` 추가
- 기존 `ApplyPose(...)`는 migration 동안만 임시 호환 경로로 두고, 최종 cleanup 단계에서 제거
- `GameplayEntityViewRegistry`와 view factory parent를 `EntityRoot` 기준으로 연결
- presenter 내부 pose 캐시를 "world pose"가 아니라 "board-local pose" 의미로 재정의
- board root가 identity일 때 기존 시각 결과가 바뀌지 않도록 유지

검증:

- 기존 presentation test 중 strip 좌표 가정이 아닌 것들은 계속 통과
- board root가 identity인 상태에서 기존 view 위치가 유지

완료 조건:

- entity transform 적용 기준이 board-local로 단일화
- 이후 단계에서 board root rotation을 붙일 수 있는 구조가 된다

구현 결과 메모:

- `GameplayBoardRoot`를 추가했고, `GameplaySceneHost`가 아래 hierarchy를 런타임에서 보장하도록 반영했다.
- `GameplayEntityView.ApplyLocalPose(...)`를 추가했고, migration 동안 남겨 둔 `ApplyPose(...)` 호환 경로는 8단계 cleanup에서 제거했다.
- entity view 생성 parent를 `EntityRoot`로 옮겼고, `GameplayEntityViewRegistry`도 `EntityRoot`를 검색 기준으로 재구성하도록 정리했다.
- presenter의 pose 캐시와 적용 경로는 `board-local pose + topology continuity local offset` 의미로 유지되며, board root가 identity일 때 기존 시각 결과가 유지된다.
- guard test를 추가해 board root hierarchy 생성, entity root parent 연결, local pose 적용을 고정했다.

### 5-3. 2단계: Projection Contract 분리

목표는 presenter가 strip-specific 산술을 직접 아는 구조를 끊고, projection 결과를 pose struct로 받는 구조로 바꾸는 것이다.

진행 상태:

- 완료 (2026-03-30)

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs`
- 신규 `Assets/_Features/Gameplay/Gameplay_Host/Runtime/ProjectedCellPose.cs`
- 임시 또는 신규 `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayStripProjector.cs`

구현 태스크:

- `ProjectedCellPose` 추가
  - `LocalPosition`
  - `LocalRotation`
  - `Normal`
- 기존 `GameplaySurfaceProjector` 역할을 새 contract로 이전
- presenter의 `TryResolveLocalPose`가 `ProjectedCellPose`를 사용하도록 수정
- motion start/end pose 계산이 projector contract만 의존하도록 정리

이 단계의 포인트는 "projector를 바꾸기 쉬운 presenter"를 만드는 것이다.

검증:

- strip 기반 projector를 유지해도 기존 시각 동작이 동일
- `ProjectedCellPose` 중심 테스트 추가

완료 조건:

- presenter 내부에서 `x/y strip offset` 직접 계산 코드가 사라진다

구현 결과 메모:

- `ProjectedCellPose`를 추가해 projector가 `LocalPosition`, `LocalRotation`, `Normal`을 반환하는 공통 pose 계약을 고정했다.
- 기존 strip projection 책임을 `GameplayStripProjector`로 분리했고, strip continuity/center 계산도 presenter 밖으로 이동시켰다.
- `GameplayTickViewPresenter`의 local pose 해석은 projector가 반환한 pose contract만 사용하도록 정리했고, entity facing 회전은 projector local rotation 위에 합성되도록 분리했다.
- guard test를 갱신해 `ProjectedCellPose`의 normal/local rotation 계약과 strip projector projection 결과를 고정했다.

### 5-4. 3단계: Cube Projector 구현

목표는 active face entity를 실제 3D cube 위에 올리는 것이다. 이 단계에서는 topology motion을 아직 즉시 전환으로 두어도 된다.

진행 상태:

- 완료 (2026-03-30)

대상 파일:

- 신규 `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayCubeProjector.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/RuntimeBoardBoundsGuardTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/PlayMode/PlayerMovementPlayModeTests.cs`

구현 태스크:

- `GameplayCubeProjector` 추가
- `BoardBounds`, `CellSize`, `CubeTopologyState` 기준 face frame 계산 구현
- `TryProjectEntityCell(...)` 구현
  - active face만 허용
- `TryProjectSurfaceCell(...)` 구현
  - active face 기본 지원 + inactive face debug 지원 분리
- presenter가 strip projector 대신 cube projector를 사용하도록 전환
- strip projection 기준 unit test를 3D face projection 기준 테스트로 교체

검증:

- bottom face cell은 수평면 위에 배치
- front face cell은 수직면 위에 배치
- inactive face entity projection은 실패
- active face entity만 3D로 표시

완료 조건:

- 엔티티가 3D cube 위에 올바른 face orientation으로 표시된다
- strip Y-offset 전제가 presenter에서 제거된다

구현 결과 메모:

- `GameplayCubeProjector`를 추가했고, `Bottom / Front / TopVisible / BackVisible` face slot에 대한 board-local face frame, cube center, visible bounds 계산을 런타임 helper로 고정했다.
- entity projection은 active face만 허용하고, surface projection은 active face 기본 + inactive debug shell 확장으로 분리했다.
- `GameplayTickViewPresenter`는 strip projector 대신 cube projector를 사용하도록 전환했고, topology presentation은 4단계 board rotation 전까지 `snap` 동작으로 유지했다.
- 기존 `StripCenterChanged` 호환 경로는 남겨두되, 이 단계부터는 strip center가 아니라 cube center를 내보내도록 정리했다.
- edit mode guard test는 `GameplayCubeProjector_ProjectsBottomFaceToHorizontalPlane`, `GameplayCubeProjector_ProjectsFrontFaceToVerticalPlane`, `GameplayCubeProjector_RejectsInactiveFaceEntityProjection`, `GameplayTickViewPresenter_PresentsOnlyActiveFaceEntitiesIn3D` 기준으로 교체했다.
- play mode 입력 시나리오 테스트의 view oracle도 strip-space 고정값 대신 `GameplayCubeProjector` 기반 projected pose 비교로 바꿨다.

### 5-5. 4단계: Board Rotation과 Camera Rig

목표는 topology 변경을 strip scroll이 아니라 board rotation으로 보이게 만드는 것이다.

진행 상태:

- 완료 (2026-03-30)

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHost.cs`
- 신규 `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayCameraRig.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayShowcaseSceneInstallerBase.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/RuntimeBoardBoundsGuardTests.cs`

구현 태스크:

- presenter의 `ContinuityAnchor` 제거
- topology motion track을 board root rotation track으로 교체
- `StripCenterChanged`를 제거하고 cube center 기반 target 갱신 이벤트 또는 직접 갱신으로 변경
- `GameplayCameraRig` 추가
  - perspective camera 기준
  - cube center 추적
  - rotation 중 framing 유지
- camera 기본 구도를 floor + front wall 동시 가시 angle로 재설정
- installer의 기본 camera 설정을 orthographic -> perspective로 전환

검증:

- forward/backward topology motion이 board root rotation으로 보간
- camera target이 cube center를 추적
- 기존 이동/공격 로직은 영향 없음

완료 조건:

- 플레이어 face traversal 시 큐브가 회전하는 연출이 동작
- strip continuity 관련 API와 테스트가 제거 가능 상태가 된다

구현 결과 메모:

- `GameplayTickViewPresenter`에서 `ContinuityAnchor`, `StripCenterChanged`, strip continuity offset 적용 경로를 제거했고, topology motion을 `board root` 회전 track으로 바꿨다.
- topology 변경 시 presenter는 destination topology 기준 local pose를 즉시 커밋하고, `CubeRotationKind`에 따라 `board root`를 `X`축 기준 `-90 / +90 -> identity`로 보간해 큐브가 회전하는 연출을 만든다.
- `GameplayBoardRoot.ApplyPresentationRotation(...)`를 추가해 cube center를 pivot으로 회전할 때도 `CameraTargetRoot`의 world position이 고정되도록 정리했다.
- `GameplaySceneHost`는 presenter에 `GameplayBoardRoot`를 직접 연결하고, camera target 이벤트 구독 대신 board root의 cube center target을 그대로 사용하도록 바꿨다.
- 신규 `GameplayCameraRig`를 추가했고, host가 perspective camera를 rig에 연결해 cube center를 계속 바라보도록 구성했다.
- 기본 camera yaw를 `0`, pitch를 `18`로 조정해 world `+Z`에 놓인 front wall이 화면을 과도하게 가리지 않도록 world `-Z` 쪽에서 floor와 함께 보는 전방 상부 구도를 만들었다.
- `GameplayCameraRig`는 `AutoFit`과 `Manual` distance 모드를 함께 지원하도록 분리했고, showcase scaffold는 기본적으로 manual distance preset을 써서 FOV 조정이 곧바로 camera position 변경으로 이어지지 않게 했다.
- `GameplayCubeProjector`는 active bottom/front seam에 `1 cell` gap을 두고 face를 안쪽으로 분리하며, `GameplayEntityVisualProfile`은 cube 내부 mount를 유지한 채 small reveal만 남겨 seam edge box가 다음 면에서도 읽히도록 보정했다.
- viewport regression test를 추가해 기본 camera pose에서 floor/front 모두 `+X`가 screen right로 투영되는지 고정했다.
- showcase/sample installer의 기본 camera 설정을 orthographic에서 perspective로 바꿨다.
- edit mode guard test를 `GameplayTickViewPresenter_TopologyMotion_InterpolatesBoardRootRotation`, `GameplaySceneHost_CameraTarget_StaysOnCubeCenterDuringBoardRotation`, `GameplaySceneHost_Initialize_UsesPerspectiveCameraRigAndTracksCubeCenter` 기준으로 갱신했다.

### 5-6. 5단계: Cube Entity Visual 전환

목표는 테스트 씬의 유닛, 박스, 벽, 투사체를 square/quad에서 cube 기반 visual로 교체하는 것이다.

진행 상태:

- 완료 (2026-03-30)

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/DefaultGameplayEntityViewFactory.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayEntityView.cs`
- 필요 시 신규 `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayEntityVisualProfile.cs`

구현 태스크:

- `PrimitiveType.Quad` 제거
- `PrimitiveType.Cube` 기반 entity visual 생성
- entity type별 scale profile 적용
  - unit
  - box
  - removed entity
  - wall entity
- facing과 model 축 정렬이 어긋나면 `ModelRoot` child를 추가해 보정
- material/color 정책 유지 또는 role별 세분화
- box와 wall은 cube 내부 방향 mount profile 사용

검증:

- 플레이어, 박스, 벽, 투사체가 cube 기반으로 생성
- collider 제거 정책은 유지
- cross-face motion 시 visual orientation이 무너지지 않음

완료 조건:

- 테스트 씬 내부 square visual이 cube visual로 완전히 대체된다

구현 결과 메모:

- `DefaultGameplayEntityViewFactory`에서 `PrimitiveType.Quad`를 제거하고, root view 아래 `ModelRoot -> Cube primitive` 구조를 생성하도록 전환했다.
- `GameplayEntityView`에 `ModelRoot`와 `ConfigureModelRoot(...)`를 추가해 presenter가 적용하는 board-local pose와 모델 자체의 보정/오프셋을 분리했다.
- 신규 `GameplayEntityVisualProfile`을 추가해 `Unit / Box / RemovedEntity / Wall(EntityType.None)`별 cube scale과 depth profile을 고정했다.
- entity projection anchor는 face plane 바깥이 아니라 cube 내부 방향 offset을 사용하고, box/wall 포함 모든 model center는 추가로 반 두께만큼 내부로 이동시켜 shell 바깥으로 새지 않게 한다.
- visual primitive는 모두 collider를 제거한 `Cube`로 생성되며, player/unit/box/removed entity/wall role별 color 정책은 유지했다.
- edit mode guard test에 `GameplayEntityView_ConfigureModelRoot_CreatesDedicatedModelPivot`, `DefaultGameplayEntityViewFactory_CreatesCubeEntityVisualProfilesWithoutColliders`, `GameplayEntityVisualProfile_BoxVisualRecedesIntoFaceInterior`, `GameplayEntityVisualProfile_WallVisualRecedesIntoFaceInterior`를 추가해 model pivot, cube primitive, collider 제거, interior mount 계약을 고정했다.

### 5-7. 6단계: Board Surface Renderer 추가

목표는 바닥과 벽면을 3D cube shell로 표시하는 것이다.

진행 상태:

- 완료 (2026-03-30)

대상 파일:

- 신규 `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayBoardSurfaceRenderer.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHost.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayBoardRoot.cs`

구현 태스크:

- board shell renderer 추가
- `BoardSurfaceRoot` 아래 visible tile pool 구성
- active face tile 기본 구성
- inactive face tile은 debug mode에서만 선택적으로 구성
- topology 변경 시 shell orientation 갱신
- wall entity는 1차에서 board shell과 합치지 않고 entity로 유지

검증:

- active faces가 cube tile 형태로 보임
- inactive faces는 기본 화면에서 보이지 않음
- entity cube와 surface tile이 z-fighting 없이 공존

완료 조건:

- 바닥과 벽면이 더 이상 "빈 공간 위의 엔티티"처럼 보이지 않는다

구현 결과 메모:

- `GameplayBoardSurfaceRenderer`를 추가했고, `BoardSurfaceRoot/VisibleTilePool` 아래에서 visible face 타일을 primitive cube pool로 유지하도록 구현했다.
- surface tile은 `GameplayCubeProjector.TryProjectSurfaceCell(...)` 결과를 재사용해 기본적으로 active bottom/front만 배치하고, inactive top/back는 debug toggle에서만 확장 가능하도록 정리한다.
- tile은 face plane 기준으로 절반 두께만 cube 안쪽으로 밀어 넣어 배치해서 entity visual과 z-fighting 없이 공존하도록 정리했다.
- inactive face는 기본값에서 생성하지 않고, debug 노출이 필요할 때만 별도 unlit material role을 적용한다.
- `GameplayBoardRoot`가 `BoardSurfaceRenderer` 보장을 담당하고, `GameplaySceneHost`는 초기화 시 renderer를 구성한 뒤 presenter topology commit에 맞춰 shell face assignment를 즉시 갱신하도록 연결했다.
- guard test에 `GameplayBoardSurfaceRenderer_CreatesExpectedVisibleFaceTiles`를 추가했고, host 초기화/board rotation 테스트에도 surface renderer 연결과 topology refresh를 검증하도록 보강했다.

### 5-8. 7단계: Showcase Scene / Builder 전환

목표는 테스트 씬과 자동 scene builder를 3D presentation 구조에 맞게 바꾸는 것이다.

진행 상태:

- 완료 (2026-03-30)

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Host/Editor/GameplayShowcaseSceneBuilder.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/CubeSurfaceTraversalShowcaseInstaller.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxInteractionShowcaseInstaller.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/CombinedGameplayShowcaseInstaller.cs`
- 신규 `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayShowcaseOverlay.cs`
- 신규 `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayShowcaseOverlayContent.cs`
- 신규 `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayShowcaseSceneScaffold.cs`
- 신규 `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/GameplayShowcaseScaffoldTests.cs`
- `Assets/Scenes/CubeSurfaceTraversalShowcase.unity`
- `Assets/Scenes/BoxInteractionShowcase.unity`
- legacy combined gameplay showcase scene asset

구현 태스크:

- `ProjectFloorCell(...)` 기반 라벨 배치 제거
- showcase annotation을 camera-facing UI 또는 board-local anchor 방식으로 변경
- scene builder가 `GameplayBoardRoot`와 camera rig를 기본 구성하도록 수정
- showcase scene 재생성

검증:

- 세 showcase scene이 3D 기준으로 열리고 플레이 가능
- annotation이 보드와 충돌하지 않음

완료 조건:

- 테스트 씬 자동 생성 도구가 새 3D contract를 사용한다

구현 결과 메모:

- `GameplayShowcaseSceneBuilder`는 더 이상 `ProjectFloorCell(...)`와 `TextMesh` world label을 생성하지 않고, showcase root마다 `GameplayShowcaseSceneScaffold`를 통해 `GameplayBoardRoot`, `GameplayCameraRig`, overlay presenter를 기본 scaffold로 심도록 전환했다.
- 새 `GameplayShowcaseOverlay` / `GameplayShowcaseOverlayContent`를 추가해서 showcase annotation을 board 위 world label이 아니라 screen-space overlay 패널로 표현하도록 바꿨다.
- `GameplayShowcaseSceneInstallerBase`는 showcase host 구성에서 cube-centered board-local contract만 사용하도록 정리했고, 남아 있던 `CalculateCenteredGridOrigin(...)` helper는 8단계 cleanup에서 제거했다.
- 각 showcase installer는 scene별 overlay 문구를 직접 제공하도록 변경했고, builder와 runtime이 같은 annotation source를 공유하도록 정리했다.
- `GameplayShowcaseSceneScaffold`는 기존 showcase scene에 남아 있던 legacy `Label_*` `TextMesh` root를 제거하고, prebuilt scene이 아직 재생성되지 않았더라도 play 진입 시 새 3D showcase scaffold를 자동 보장하도록 정리했다.
- edit mode test `GameplayShowcaseSceneScaffold_EnsureInstallerScaffold_Creates3DScaffoldAndRemovesLegacyLabels`, `GameplayPresentationCleanup_RemovesLegacyGridOriginContracts`를 통해 showcase scaffold와 legacy grid-origin 제거 계약을 고정했다.

### 5-9. 8단계: Cleanup / Legacy 제거

목표는 migration 중 남겨 둔 strip 전용 API와 compatibility path를 제거하는 것이다.

진행 상태:

- 완료 (2026-03-30)

대상 파일:

- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayShowcaseSceneInstallerBase.cs`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHostConfiguration.cs`
- 관련 테스트와 문서

구현 태스크:

- `gridOrigin` 제거
- strip-specific projector와 helper 제거
- `ContinuityAnchor` 관련 dead code 제거
- strip center 기반 camera target 개념 제거
- 문서와 테스트를 최종 terminology로 동기화

검증:

- repo 내 strip-specific public contract callsite 0
- 새 3D presentation contract만 남음

완료 조건:

- 3D presentation migration이 구조적으로 완료된다

구현 결과 메모:

- `GameplaySceneHostConfiguration.GridOrigin`, `GameplayTickViewPresenter.Initialize(..., gridOrigin, ...)`, `GameplayBoardSurfaceRenderer.Initialize(..., cubeCenter, ...)`를 제거해 presentation 좌표 기준을 `board-local cube center = Vector3.zero` 하나로 고정했다.
- `GameplayCubeProjector`의 translation overload를 제거했고, cube center는 `GameplayBoardRoot`의 world transform만으로 승격되도록 정리했다.
- `GameplayStripProjector`, `GameplayShowcaseSceneInstallerBase.CalculateCenteredGridOrigin(...)`, `GameplayEntityView.ApplyPose(...)`를 삭제해 strip/compatibility public surface를 정리했다.
- sample scene과 showcase/runtime test fixture에서 legacy `gridOrigin` 직렬화/설정을 제거했고, world offset이 필요한 검증은 host/root transform 기준으로 전환했다.
- edit mode test `GameplayPresentationCleanup_RemovesLegacyGridOriginContracts`를 추가했고, runtime/playmode guard test도 projector local pose + board root world transform 계약에 맞춰 갱신했다.

## 6. 권장 PR 분리

권장 PR 순서는 아래와 같다.

### 6-1. PR 1

- 0단계
- 1단계

성격:

- 무동작 또는 최소 동작 변경의 구조 정리

### 6-2. PR 2

- 2단계
- 3단계

성격:

- projection contract 전환
- strip -> 3D cell projection 전환

### 6-3. PR 3

- 4단계

성격:

- topology motion과 camera 계약 전환

### 6-4. PR 4

- 5단계
- 6단계

성격:

- cube entity visual
- board shell visual

### 6-5. PR 5

- 7단계
- 8단계

성격:

- scene authoring path 완성
- legacy 제거

## 7. 테스트 분해

### 7-1. 유지 대상

- `TickPipeline` unit/scenario test
- movement/attack/cleanup phase test
- determinism replay test
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/RuntimeBoardBoundsGuardTests.cs`
  - `GameplaySceneHost_Initialize_UnboundedBoard_Throws`
  - `GameplayCompositionRoot_CreateWorldState_RejectsUnboundedBoard`
  - `GameplaySceneHost_Initialize_NormalizesPreExistingRemovedEntityCadence`
  - `GameplayCompositionRoot_DeclaresOnlyBoundedWorldFactory`
  - detach/remove visibility sequencing test
  - move/push/removed entity/flip interpolation test
  - committed world query vs presented motion 분리 test
- `Assets/_Features/Gameplay/Gameplay_Tests/PlayMode/PlayerMovementPlayModeTests.cs`
  - move/push/flip input priority 시나리오
  - repeat cadence / direction delay 시나리오
  - spawned entity visibility 시나리오
  - blocked-cell drift 방지 시나리오
  - 단, direct `GetViewPosition(...)=new Vector3(...)` 단정은 유지 대상이 아니라
    이후 3D oracle로 교체할 strip-space 표현 의존부다.

### 7-2. 교체 대상

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
  - 테스트 시나리오는 유지한다.
  - strip-space exact position oracle만 `cell -> board-local pose -> world pose` 또는
    active-face visibility oracle로 교체한다.

### 7-3. 신규 테스트 목록

반드시 추가할 테스트:

- `GameplayCubeProjector_ProjectsBottomFaceToHorizontalPlane`
- `GameplayCubeProjector_ProjectsFrontFaceToVerticalPlane`
- `GameplayCubeProjector_FrontFaceRows_RiseAwayFromFloor`
- `GameplayCubeProjector_RejectsInactiveFaceEntityProjection`
- `GameplayTickViewPresenter_PresentsOnlyActiveFaceEntitiesIn3D`
- `GameplayTickViewPresenter_TopologyMotion_RotatesBoardRoot`
- `GameplaySceneHost_CameraRigTracksCubeCenter`
- `DefaultGameplayEntityViewFactory_CreatesCubeEntityVisualProfilesWithoutColliders`
- `GameplayEntityVisualProfile_BoxVisualRecedesIntoFaceInterior`
- `GameplayEntityVisualProfile_WallVisualRecedesIntoFaceInterior`
- `GameplayBoardSurfaceRenderer_CreatesExpectedVisibleFaceTiles`
- `Movement_MoveAcrossBottomTopEdge_FailsWhenRotatedDestinationHasWallBlocker`
- `Movement_MoveAcrossBottomTopEdge_FailsWhenRotatedDestinationTerrainBlocked`

## 8. 수동 검증 체크리스트

각 단계 이후 최소한 아래 항목을 수동 확인한다.

1. 플레이어가 `Bottom -> Front` 회전 시 정상적으로 이어 보이는가
2. 박스가 `Bottom <-> Front` 경계를 넘을 때 visual snapping이 없는가
3. inactive face 엔티티가 기본 화면에 보이지 않는가
4. inactive face shell이 기본 화면에 노출되지 않는가
5. box와 wall이 cube 외부가 아니라 내부 공간 쪽에 정상적으로 mount되는가
6. 카메라가 front wall과 floor를 동시에 보여 주면서도 회전 중 플레이 영역을 잃지 않는가
7. showcase scene에서 입력, push, flip, removed entity이 모두 기존 규칙대로 동작하는가

## 9. 리스크 관리

### 9-1. 가장 큰 리스크

- presenter와 board root의 pose space가 이중 적용될 수 있다
- topology motion과 entity motion 보간이 서로 다른 기준 좌표를 쓸 수 있다
- oblique camera, inverted face frame, exterior-mounted entity visual이 플레이 공간 인지를 흐릴 수 있다

### 9-2. 대응 방식

- pose contract는 무조건 board-local 하나로 단일화
- topology motion 테스트를 projector 테스트보다 먼저 추가하지 않는다
- inactive face는 기본값에서 숨기고, face frame과 entity depth를 interior mount 기준으로 고정한다
- camera 기본 yaw/pitch를 front wall + floor 동시 가시 preset으로 고정한다

## 10. 최종 완료 정의

이 migration은 아래 조건을 모두 만족할 때 완료로 본다.

- gameplay logic은 기존 deterministic tick 구조를 유지한다
- active face entity는 3D cube 위에 일관되게 렌더링된다
- topology motion은 board rotation으로 보인다
- camera는 perspective cube view 기준으로 안정적으로 동작한다
- showcase scenes는 3D 구조로 재생성된다
- strip-specific presentation contract는 제거된다
