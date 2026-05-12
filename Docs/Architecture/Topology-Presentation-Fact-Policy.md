# Topology Presentation Fact Policy

이 문서는 `TickResultBuilder`의 presentation data build boundary에서 topology transition을 하나의 canonical fact로 정규화하는 정책을 고정한다.

## Purpose

`TickPresentationData`를 만드는 presentation boundary에는 현재 두 종류의 topology transition 관측 경로가 존재한다.

- Settle / legacy topology path
  - `PreMovementSnapshot.Topology`가 source topology다.
  - `PostMovementSnapshot.Topology`와 `FinalAuthoritativeSnapshot.Topology`가 destination topology다.
- 2D Free native topology path
  - topology가 Plan phase의 Free2D batch에서 먼저 materialize될 수 있다.
  - 이 경우 `PreMovementSnapshot`, `PostMovementSnapshot`, `FinalAuthoritativeSnapshot`이 이미 destination topology를 가질 수 있다.
  - source topology는 `Free2DTopologyTransition` metadata와 rotation kind로 복원해야 한다.

presentation consumer가 각자 snapshot diff나 Free2D metadata를 직접 해석하면 같은 tick 안에서도 서로 다른 source/destination topology를 볼 수 있다. 실제로 topology motion은 synthetic source topology를 복원했지만, Barricade active transition event는 snapshot topology만 비교하면서 Free2D native 경로에서 event가 누락될 수 있었다.

따라서 topology-dependent presentation data는 `TickResultBuilder` 내부의 canonical topology transition fact를 통해 source/destination topology를 공유해야 한다.

## Canonical Fact

canonical fact는 `TickPresentationDataBuilder` 내부 private helper다. runtime public API나 `TickPresentationData` shape로 노출하지 않는다.

현재 fact가 표현하는 정보는 다음과 같다.

- transition 존재 여부
- source topology
- destination topology
- rotation kind
- transition source
  - `SnapshotDiff`
  - `Free2DNative`
  - `None`

`HasTransition`은 `RotationKind != None` 같은 계산 속성으로 정의하지 않는다. no-transition과 transition-without-motion 가능성을 구분할 수 있도록 explicit bool로 유지한다. 현재 구현은 기존 `BuildTopologyMotion`이 인정하던 `SnapshotDiff`와 `Free2DNative` 범위만 canonical fact로 옮긴다.

## Resolution Priority

`ResolveTopologyTransitionFact(context)`는 아래 우선순위를 따른다.

1. `PreMovementSnapshot.Topology != PostMovementSnapshot.Topology`이면 `SnapshotDiff` fact를 반환한다.
2. snapshot topology가 이미 같을 때만 Free2D native metadata fallback을 확인한다.
3. Free2D metadata는 `MovementExecutionBoundaryKind.Free2DTopologyTransition`이고 `RotationKind != None`인 경우만 인정한다.
4. Free2D source topology는 destination topology와 rotation kind로 synthetic 복원한다.
5. synthetic source topology와 destination topology가 같으면 no transition으로 둔다.
6. SnapshotDiff와 Free2D metadata가 동시에 있으면 SnapshotDiff가 이긴다.

이 우선순위는 중복 motion/event 생성을 막고, Settle / legacy path의 기존 timing을 유지하기 위한 것이다.

## Consumer Rule

topology에 의존하는 presentation consumer는 snapshot diff나 Free2D metadata를 직접 해석하지 않는다.

다음 조건에 해당하면 canonical topology transition fact를 사용해야 한다.

- topology source/destination pair가 필요한 presentation data를 만든다.
- topology 회전에 따라 active/inactive 상태가 달라지는 tile feature, entity visibility, board motion, one-shot event를 계산한다.
- Free2D native path에서 snapshots-already-match 상황을 올바르게 처리해야 한다.
- Settle / legacy path와 Free2D native path를 같은 presentation 의미로 비교해야 한다.

현재 fact consumer는 다음과 같다.

- `BuildTopologyMotion`
  - fact의 source/destination/rotation으로 `TickTopologyMotion`을 만든다.
  - fact가 없으면 기존 respawn topology motion fallback을 유지한다.
- `AddBarricadeActiveStateTransitionEvents`
  - fact가 있으면 previous active는 fact source topology 기준으로 계산한다.
  - fact가 있으면 final active는 fact destination topology 기준으로 계산한다.
  - fact가 없으면 기존처럼 `PreMovementSnapshot.Topology`와 `FinalAuthoritativeSnapshot.Topology`를 사용한다.

새 topology-dependent presentation consumer를 추가할 때는 먼저 canonical fact를 받을 수 있는 build path에 붙인다. consumer 내부에서 `PreMovementSnapshot.Topology`, `PostMovementSnapshot.Topology`, `FinalAuthoritativeSnapshot.Topology`, Free2D metadata를 조합해 별도 source/destination을 만들지 않는다.

## Non-Goals

이 정책은 movement execution path를 병합하지 않는다.

- Settle / legacy movement execution과 2D Free native movement execution은 계속 별도 path다.
- 2D Free native의 Plan phase topology materialization은 유지한다.
- continuous local offset, velocity, anchor remap semantics는 유지한다.
- topology completion 이후 immediate sync를 대체 해결책으로 넣지 않는다.
- Animator controller, stage asset, public presentation data shape는 이 정책의 변경 대상이 아니다.

canonical fact는 presentation build boundary의 normalization layer다. authoritative movement semantics나 topology materialization timing을 재정의하지 않는다.

## Testing Contract

이 정책을 변경할 때는 최소한 아래 동작을 보존해야 한다.

- SnapshotDiff path에서 기존 Barricade activated/deactivated event timing이 presentation start로 유지된다.
- Free2D native snapshots-already-match path에서 synthetic source topology를 사용해 `TickTopologyMotion`이 만들어진다.
- Free2D native snapshots-already-match path에서 Barricade activated/deactivated event가 누락되지 않는다.
- SnapshotDiff와 Free2D metadata가 동시에 있는 synthetic case에서 SnapshotDiff가 우선한다.
- topology motion과 topology-dependent event가 같은 source/destination pair를 본다.

관련 regression tests:

- `TickResultBuilder_Free2DTopologyTransition_MetadataSynthesizesTopologyMotionWhenSnapshotsAlreadyMatch`
- `TickResultBuilder_Free2DTopologyTransition_BarricadeFrontFace_DeactivatedWhenSnapshotsAlreadyMatch`
- `TickResultBuilder_Free2DTopologyTransition_BarricadeFrontFace_ActivatedWhenSnapshotsAlreadyMatch`
- `TickResultBuilder_TopologyTransitionFact_SnapshotDiffWinsOverFree2DMetadata`

## Extension Guideline

canonical fact를 public runtime type으로 승격하는 것은 기본 선택지가 아니다. 여러 production owner가 동일 fact를 직접 소비해야 하고 private builder helper로는 dependency direction을 유지하기 어려울 때만 승격을 검토한다.

fact source를 늘릴 때는 아래를 함께 정의해야 한다.

- 새 source가 snapshot diff보다 우선하는지, fallback인지
- source/destination topology를 어떻게 복원하는지
- no-transition과 transition-without-motion을 어떻게 구분하는지
- 기존 `BuildTopologyMotion`과 topology-dependent event가 중복 생성되지 않는지
- Settle / legacy path와 Free2D native path의 event timing이 유지되는지
- Core regression test와 stratification governance entry
