# Gameplay Wall Tick Cost Optimization — Slice 1 Goal Plan

- 상태: Goal complete — corrected focused/core/replay evidence와 고정 5-state/15-run 성능 acceptance 통과 / runtime retain
- 작성일: 2026-08-26
- 초기 계획 감사 기준 revision: `5d338c54a890bb5225ddda9d769f880846b8f1ca`
- 사후 감사 대상 runtime revision: `29d26ab18b0023a2a7815786f71dfbd2efd5c083`
- 상위 문서: [Gameplay Wall Tick Cost Optimization Plan](./Gameplay-Wall-Tick-Cost-Optimization-Plan.md)
- 사후 감사 및 복구 계획: [Slice 1 Post-Closeout Audit and Recovery Plan](./Gameplay-Wall-Tick-Cost-Optimization-Slice1-Post-Closeout-Audit-2026-08-27.md)
- 적용 가드레일: `gameplay-contract-hardening`

> Current truth: 2026-08-27 독립 사후 감사로 기존 C2 performance verdict와 당시 완료 선언을 철회한 뒤, phase별 admission을 동결한 새 고정 5-state/15-run campaign을 처음부터 수행했다. 아래 historical 기록은 provenance로 유지하며, 현재 판정은 문서 끝의 `2026-08-27 — recovery campaign complete` closeout이 supersede한다.

## 1. Goal 정의

이 문서에서 말하는 **Slice 1**은 상위 문서의 초기 저위험 작업을 한 번의 Goal로 묶은 실행 단위다.

1. **S1-A — tests-first 계약과 diagnostics-only 계측**
2. **S1-B — `FinalEntities` trusted owned read-only 공유**
3. **S1-C — 기본 Factory의 `EntityType` 후보 캐시**

상위 문서와의 번호 매핑은 다음과 같다.

| 이 Goal | 상위 계획 |
|---|---|
| S1-A | Slice 0 중 FinalEntities/Factory bounded diagnostics |
| S1-B | Slice 1 — trusted `FinalEntities` sharing |
| S1-C | Slice 2 — default Factory candidate prefilter |

Goal에 입력할 권장 objective는 다음과 같다.

> Wall을 권위 상태나 Solid occupancy에서 제거하지 않고, 내부 Builder 경로의 `FinalEntities` 중복 복사 2회와 기본 Entity Logic Factory의 불필요한 Wall 탐색을 줄인다. 기존 general/internal `IEnumerable<EntityState>` 생성자의 방어적 복사, entity-major 및 Factory 등록 순서, static logic 우선권, Custom Factory 호환성, FinalEntities/EventLog/hash/FullCanonical trace/replay 결과를 유지하고, diagnostics-only 구조 계측과 focused/core/replay 및 동일 조건 성능 증거로 변경을 검증한다.

이 Goal은 단순한 조건문 추가가 아니다. 내부 소유권 계약과 확장 가능한 Factory capability를 명시하고, 그 계약을 테스트와 계측으로 고정하는 소규모 구조 개선이다. 다만 범위를 제한하여 Cleanup, trace 정책, snapshot 저장 구조에는 손대지 않는다.

## 2. 최종 판정

**구현 가능**하다. 현재 호출 경로와 assembly visibility를 기준으로 별도 공개 API 파괴 없이 변경할 수 있다.

| 패키지 | 판정 | 기대 효과 | 주된 위험 |
|---|---|---|---|
| S1-A | 승인 | 최적화가 실제 경로에 적용됐는지 수치로 고정 | 진단 코드 자체 비용·전역 상태 오염 |
| S1-B | 단계적 승인, 먼저 구현 | B1에서 마지막 복사, B2에서 Builder 이후 복사를 제거 | 가변 backing storage 유출과 과거 결과 변이 |
| S1-C | 승인, B 이후 구현 | Wall/Box/Unit에 대한 불필요한 기본 `CanCreate` 호출 제거 | Factory 순서·Custom Factory·미래 enum 호환성 변화 |

S1-B와 S1-C는 서로 기능적으로 독립적이다. B를 먼저 완료하고 측정한 뒤 C를 적용한다. 어느 한쪽이 계약 또는 성능 gate를 통과하지 못하면 그 패키지만 롤백할 수 있어야 한다.

### 2.1 다중 감사에서 확정한 쟁점

세 독립 검토가 구현/API, tests-first 검증, 적대적 consumer/lifetime 관점으로 수행됐다.

- 보수적 검토는 이미 단독 소유가 확실한 `TickResultData -> TickResult` B1만 먼저 적용할 것을 권고했다.
- 적대적 검토는 전용 carrier나 `_finalEntities` 필드 타입 변경이 기존 reflection PlayMode fixture 6개를 깨뜨린다는 반례를 찾았다.
- 교차 검토 결과, 구체 필드 타입을 유지하고 Builder 단일 call site를 source guard로 고정하면 B2 ownership transfer도 구현 가능하다고 판정했다.

따라서 최종 결정은 **B1과 B2를 순차 checkpoint로 수행하되, B2가 ownership/lifetime gate를 통과하지 못하면 B1만 안전한 부분 결과로 남길 수 있는 것**이다. 단, 현재 objective는 copy 0을 요구하므로 이 경우 Slice 1 Goal은 완료가 아니다. objective를 B1-only로 공식 수정하거나 B2를 재설계해야 한다. B2를 이름 없는 overload나 자동 type trust로 구현하는 것은 승인하지 않는다.

## 3. 현재 경로와 비용

### 3.1 `FinalEntities`

현재 정상 Tick 결과 경로는 다음과 같다.

```text
final WorldSnapshot
  -> TickResultBuilder: ordered List<EntityState> 생성               (materialization 1)
  -> TickResultData(IEnumerable): 새 List + ReadOnlyCollection       (copy 1)
  -> TickPipeline
  -> TickResult(IEnumerable): 새 List + ReadOnlyCollection           (copy 2)
  -> presentation / audio / VFX / replay / hash 소비자
```

관련 소유자는 다음과 같다.

- `Gameplay_Loop/Runtime/TickResultBuilder.cs`
- `Gameplay_Loop/Runtime/TickResult.cs`
- `Gameplay_Loop/Runtime/TickPipeline.cs`

첫 ordered materialization은 최종 스냅샷의 정렬된 결과를 얻기 위해 필요하다. Slice 1은 이름 있는 internal ownership 경로에서만 Builder가 List를 exact `ReadOnlyCollection`으로 감싼 뒤 그 wrapper의 소유권을 이전하고, 일반 `IEnumerable` 경로의 방어 복사는 그대로 둔다. B1은 이미 단독 소유가 확정된 `TickResultData`에서 `TickResult`로 넘어가는 마지막 복사를 제거하고, 최종 재설계 B2는 Builder가 더 이상 사용하지 않는 List의 exact wrapper를 `TickResultData`가 인수해 첫 복사를 제거한다.

### 3.2 Entity Logic Factory

`SnapshotEntityLogicProvider.Build`는 entity ID 순서로 모든 entity를 순회하고, 각 entity마다 등록 순서대로 모든 Factory의 `CanCreate`를 호출한다. 기본 구성은 다음 네 Factory다.

- `EnemyEntityLogicFactory`
- `EnemyActionStateEntityLogicFactory`
- `EnemyCombatEntityLogicFactory`
- `SlidingBoxEntityLogicFactory`

현재 생성 stage 13개의 초기 배치 2,073개 중 Wall은 954개다. Wall은 기본 동적 logic을 만들지 않지만 기본 Factory 네 개의 후보 검사를 모두 받는다. Wall이 가장 많은 `stage-0-1`의 179개 Wall만 계산해도 Tick당 716번의 기본 후보 검사가 발생한다.

이 수치는 제거 가능한 구조 비용이다. 그러나 전체 Tick이나 Full trace의 주 비용이라고 단정하지 않는다. S1-C는 bounded micro-optimization이며, 큰 개선을 과장하지 않고 계측 결과로 유지 여부를 결정한다.

## 4. 보존해야 할 계약

### 4.1 StrongContract

- `WorldState`가 유일한 권위 mutable gameplay state owner다.
- Wall은 entity 및 Solid occupancy blocker로 계속 존재한다.
- 저장·조회 단위는 `SurfaceCell(face, x, y)`다.
- `FinalEntities`의 값과 entity ID 순서를 변경하지 않는다.
- entity-major 순회와 Factory 등록 순서를 변경하지 않는다.
- static logic은 dynamic Factory logic보다 먼저 소유권을 가진다.
- 동일 entity/phase의 첫 logic owner 우선권을 유지한다.
- 범위를 선언하지 않은 Custom `IEntityLogicFactory`는 모든 `EntityType`을 계속 관찰한다.
- general/internal `IEnumerable<EntityState>` 생성자는 입력 컬렉션 변경으로부터 결과를 보호한다.
- 이전 Tick의 `TickResult`는 이후 Tick 실행 뒤에도 불변이다.
- EventLog, occupancy, determinism hash, FullCanonical trace 및 replay 관찰 결과를 변경하지 않는다.
- 진단 카운터는 권위 상태, 실행 순서, hash, canonical trace에 포함하지 않는다.

### 4.2 CurrentPolicy

- 내부 Builder가 만든 컬렉션도 다시 방어 복사하는 방식.
- 모든 기본 Factory가 모든 entity type의 `CanCreate`를 받는 방식.
- Factory capability를 `CanCreate` 하나로만 표현하는 방식.

Slice 1은 CurrentPolicy만 변경한다.

## 5. 명시적 Non-goal

- Wall 또는 `EntityType.None`을 snapshot, occupancy, hash, trace, presentation에서 제외하지 않는다.
- `EntityType.None`을 immutable Wall의 동의어로 만들지 않는다.
- Enemy 0.1초 주기화, inactive face, Ceiling/Back Face 최적화를 포함하지 않는다.
- Cleanup full scan/candidate index를 변경하지 않는다.
- Off/LiveCompact/FullCanonical 정책이나 formatter/hash 실행 정책을 변경하지 않는다.
- entity logic instance, entity ID eligibility, `CanCreate` 결과, profile binding, AI mode를 캐시하지 않는다.
- `TryResolveEnemyGlidePresentationSettings`의 Factory 탐색 경로를 후보 캐시에 연결하지 않는다.
- public `IEntityLogicFactory`에 새 멤버를 추가하지 않는다.
- gameplay Scene, Prefab, ScriptableObject, stage content 자산을 변경하지 않는다. 새 C# 파일의 Unity `.meta`만 함께 추가한다.
- EventLog collection sharing, `FinalEntities` storage pooling, private reflection fixture의 대규모 개편을 포함하지 않는다.
- multithreaded TickPipeline 또는 `AsyncLocal` 전환을 포함하지 않는다.

## 6. 구현 패키지

### 6.1 S1-A — tests-first 계약과 계측

#### 구현 원칙

BoardState 소유인 기존 `SnapshotMaterializationDiagnostics`를 비대하게 만들지 않고, Gameplay Loop 소유의 `GameplayTickWorkloadDiagnostics`를 별도 internal 파일로 둔다. 기존과 동일한 `[ThreadStatic]` opt-in capture 모델을 사용하며, 상시 로거나 gameplay singleton을 만들지 않는다.

추가할 최소 카운터는 다음과 같다.

- `FinalEntityEnumerationCount`와 enumerated item 수
- `FinalEntityDefensiveCopyCount`와 copied item 수
- `FinalEntityOwnedWrapperCreationCount`와 wrapped item 수
- `TickResultFinalEntityTrustedShareCount`와 shared item 수
- provider Build 및 entity 방문 수
- 전체 Factory opportunity, prefilter skip, 실제 `CanCreate`, `Create`, accepted/conflict-rejected 수
- `None`, `Unit`, `Box`, unknown type별 candidate/probe/skip 수
- provider construction 중 candidate cache 생성 수

최소 record surface에는 다음 의미가 포함돼야 한다.

```csharp
RecordFinalEntityEnumeration(int itemCount);
RecordFinalEntityDefensiveCopy(int itemCount);
RecordOwnedFinalEntityWrapperCreated(int itemCount);
RecordTickResultFinalEntitiesShared(int itemCount);
RecordEntityLogicCandidateCacheConstructed();
RecordEntityLogicBuild(in EntityLogicBuildMetrics metrics);
```

카운터의 단위는 **현재 capture scope 안에서 발생한 횟수**다. capture가 없을 때는 현재처럼 null 조건 분기만 거치며, Dictionary 생성·문자열 포맷·로그 출력이 발생하면 안 된다.

고정된 type bucket은 필드로 보관한다. 사용자 Factory type별 Dictionary나 문자열 key를 매 probe마다 갱신하지 않는다. Provider 진입 때 capture 활성 여부를 한 번 읽고 local integer로 집계한 뒤 Build당 한 번 aggregate record를 전달한다. capture가 꺼진 경로에서 문자열, Dictionary, List를 새로 만들면 안 된다.

`BeginCapture()`의 중첩 scope 복원 동작을 보존한다. 모든 테스트는 `using`으로 scope를 닫아야 하며, capture 밖 `Current`가 이전 상태를 보지 않는지 확인한다.

`[ThreadStatic]`은 현재 single-thread Tick 전제에 맞지만 thread migration은 지원하지 않는다. `[Conditional]`, `UNITY_EDITOR`, `UNITY_INCLUDE_TESTS`로 알고리즘 자체를 갈라 Player와 테스트가 서로 다른 dispatch 경로를 사용하게 만들지 않는다.

카운터 의미와 정상 완료식은 다음으로 고정한다.

- final entity enumeration은 Tick 전체 snapshot 열거가 아니라 `TickResultBuilder`의 결과 조립 경로만 센다.
- `FactoryOpportunityCount = EntityVisitedCount * RegisteredFactoryCount`.
- `CanCreateProbeCount = FactoryOpportunityCount - PrefilterSkipCount`.
- `CreatedLogicCount`는 `CanCreate == true` 뒤 `Create`가 성공한 횟수다.
- `AcceptedLogicCount + ConflictRejectedLogicCount = CreatedLogicCount`.
- `None + Unit + Box + Unknown` 각 bucket의 합은 전체 aggregate와 같아야 한다.
- `FinalEntityOwnedWrapperCreationCount`는 B2 production Builder 경로에서 Tick당 1이고, 일반 enumerable 경로에서는 0이다.
- `EntityLogicCandidateCacheConstructionCount`는 capture 안에서 생성한 provider instance당 1이다.
- Factory 예외로 중단된 Build는 위 정상 완료식 acceptance 대상에서 제외한다.

Build 중 candidate list allocation 0은 전체-frame GC 수치로 추정하지 않는다. Core source architecture guard가 다음을 직접 고정한다.

- candidate list 생성 helper는 provider constructor에서만 호출된다.
- `Build`는 cached list 또는 원본 `_entityLogicFactories`만 선택한다.
- `Build` 안에는 candidate Factory `List`, 배열, LINQ materialization이 없다.

#### tests-first 항목

`SnapshotBudgetGuardTests` 또는 동일 diagnostics assembly의 새 focused fixture에 추가한다. 아래 항목을 한 번에 추가하지 않고 A baseline, B1, B2, C의 각 checkpoint 직전에 해당 assertion만 추가한다.

1. 기존 내부 경로가 ordered enumeration 1, defensive copy 2임을 변경 전 baseline으로 기록한다.
2. S1-B1 적용 뒤 ordered enumeration 1, defensive copy 1, trusted share 1이 되도록 budget을 고정한다.
3. S1-B2 적용 뒤 ordered enumeration 1, owned wrapper creation 1, defensive copy 0, trusted share 1이 되도록 budget을 고정한다.
4. capture 사용 여부가 `FinalEntities`, EventLog, hash, phase trace를 바꾸지 않음을 비교한다.
5. prefiltered/unscoped Factory의 aggregate type별 probe와 created 수가 실제 호출 기록과 일치하는지 검증한다.
6. capture 종료 후 다음 테스트에 count가 누출되지 않음을 검증한다.

S1-A에서는 최적화 전 의미를 비교할 test-only reference도 준비한다.

- Factory: 원래 full-scan과 같은 unscoped-delegating Factory 구성. `EnemyEntityLogicFactory`처럼 보조 capability를 가진 Factory는 resolver-capable adapter가 `IEnemyGlidePresentationSettingsResolver`와 first-success 순서까지 함께 위임한다. 단순 `IEntityLogicFactory` wrapper로 capability를 잃게 만들지 않는다.
- FinalEntities: 같은 ordered source로 만든 general-copy `TickResultData`/`TickResult` 경로.

변경 후 replay를 두 번 실행하는 결정성 테스트만으로 pre/post parity를 주장하지 않는다. S1-B는 owned 경로와 general-copy reference의 FinalEntities/EventLog/hash/FullCanonical trace를 같은 fixture에서 비교한다. S1-C는 다음 세 계약을 분리한다.

- logic set, accepted phase owner, FinalEntities, EventLog, hash, FullCanonical trace는 legacy-equivalent full-scan과 동일하다.
- prefilter 미구현 Custom Factory의 `CanCreate/Create` sequence는 legacy와 정확히 동일하다.
- opt-in 기본 Factory의 sequence는 legacy sequence에서 `MayCreateForEntityType == false`인 `CanCreate` 호출만 제거한 sequence와 동일하다. Wall 기본 probe 0은 이 차이의 의도된 결과다.

계측 API의 실제 이름은 repository vocabulary에 맞게 조정할 수 있지만 의미와 acceptance 값은 바꾸지 않는다. 현재 해당 component 전용 profiler marker는 없고 `gameplay-performance`는 `RunSingleTick()` 전체 wall-clock을 측정한다. 이 seam은 구조적 호출량과 복사량만 기록하며 stopwatch 수치를 CI 계약으로 만들지 않는다.

#### 변경 예상 파일

- 추가: `Gameplay_Loop/Runtime/GameplayTickWorkloadDiagnostics.cs` 및 Unity `.meta`
- 수정 hook: `Gameplay_Loop/Runtime/TickResultBuilder.cs`
- 수정 hook: `Gameplay_Loop/Runtime/TickResult.cs`
- 수정 hook: `Gameplay_Entities/Runtime/SnapshotEntityLogicProvider.cs`
- 추가 테스트: `Gameplay_Tests/EditMode/Unit/TickWorkDiagnosticsTests.cs` 및 Unity `.meta`

### 6.2 S1-B — `FinalEntities` trusted owned read-only 공유

#### 권장 신뢰 경계와 B1/B2 순서

`TickResultData`의 현재 저장소를 trusted provenance로 사용한다.

```csharp
// TickResultData
internal ReadOnlyCollection<EntityState> OwnedFinalEntities => _finalEntities;
internal static TickResultData CreateFromOwnedFinalEntities(
    ReadOnlyCollection<EntityState> ownedFinalEntities,
    ...);

// TickResult
internal static TickResult CreateFromOwnedData(
    ...,
    TickResultData tickResultData,
    ...);
```

두 named factory가 호출하는 private constructor는 일반 overload와 구분되는 private ownership marker/token 인자를 반드시 가진다. 첫 인자 타입만 `ReadOnlyCollection<EntityState>`으로 다른 overload는 만들지 않는다. 그래야 향후 exact-type internal caller가 overload resolution으로 trusted 경로를 우발적으로 선택하지 않는다.

`TickResultData`는 일반 경로에서 임의 입력을 새 private `List<EntityState>`로 복사한 뒤 `ReadOnlyCollection<EntityState>`으로 감싸고, backing List를 노출하지 않는다. `EntityState`는 현재 reference field가 없는 value struct이고 생성 뒤 mutation API도 없다. 따라서 **`TickResultData`가 소유한 구체 collection**만 `TickResult`가 신뢰할 수 있다.

B1은 이 기존 단독 소유 storage를 `TickResult`와 공유한다. 최종 재설계 B2는 `TickResultBuilder`가 새로 생성하고 ordered enumeration을 끝낸 뒤 더는 사용하지 않는 `List<EntityState>`를 Builder 안에서 정확히 한 번 `AsReadOnly()`로 감싼다. Builder는 이 exact `ReadOnlyCollection<EntityState>`만 `CreateFromOwnedFinalEntities`로 넘기며, named factory는 wrapper를 다시 만들거나 mutable List 참조를 외부로 반환하지 않는다.

B2의 owned factory는 production에서 `TickResultBuilder.Build` 한 곳만 호출할 수 있다. `TickResultOwnershipCoreTests`의 source architecture guard는 `Gameplay_Tests`를 제외한 production `.cs`에서 qualified invocation `TickResultData.CreateFromOwnedFinalEntities(`가 정확히 한 번이고 위치가 `TickResultBuilder.cs`인지 pin한다. 다른 caller가 필요해지면 ownership/lifetime 감사를 다시 수행한다.

다음과 같은 runtime type 기반 자동 신뢰는 금지한다.

```csharp
if (finalEntities is ReadOnlyCollection<EntityState> readOnly)
{
    _finalEntities = readOnly;
}
```

외부 caller가 mutable List를 `ReadOnlyCollection`으로 감싼 뒤 원본 List를 바꿀 수 있기 때문이다. 신뢰 경계는 타입 모양이 아니라 `TickResultData` provenance와 이름 있는 internal factory로 표현한다.

`TickResult._finalEntities`의 구체 타입은 `ReadOnlyCollection<EntityState>`로 유지한다. 현재 최소 6개 PlayMode fixture가 reflection으로 이 private field에 같은 구체 타입을 주입하므로, 이를 전용 carrier 또는 일반 `IReadOnlyList`로 바꾸는 것은 불필요한 touched surface와 테스트 파손을 만든다.

해당 regression fixture는 다음과 같다.

- `BoxMotionReadinessPlayModeTests`
- `ActualSceneBootstrapSmokePlayModeTests`
- `DamageDeathVfxProductionDefaultPlayModeTests`
- `EnemyPresentationReadinessPlayModeTests`
- `GameplayAudioIntegrationPlayModeTests`
- `PlayerActionAnimationReadinessPlayModeTests`

#### 생성 경로

```text
TickResultBuilder
  1. 새 List 생성
  2. finalSnapshot.EnumerateEntitiesOrdered(list)
  3. exact ReadOnlyCollection wrapper를 한 번 생성
  4. list를 더 사용하지 않음
  5. TickResultData.CreateFromOwnedFinalEntities(wrapper, ...)로 소유권 이전

TickResultData
  - 일반 IEnumerable 생성자: 기존처럼 새 private List에 방어 복사 유지
  - named owned factory: Builder가 만든 exact ReadOnlyCollection을 ownership token constructor에 전달
  - internal OwnedFinalEntities: 소유한 ReadOnlyCollection을 구체 타입으로 반환
  - 기존 FinalEntities: IReadOnlyList로만 노출

TickPipeline
  - 기존 일반 생성자 대신 TickResult.CreateFromOwnedData(...) 호출

TickResult
  - 기존 일반 IEnumerable 생성 경로: 방어 복사 유지
  - named trusted factory/ownership-token private constructor: TickResultData.OwnedFinalEntities 그대로 보관
  - EventLog, completed phases, phase trace는 기존 방어 복사 유지
  - public FinalEntities: IReadOnlyList로만 노출
```

`bool skipCopy`, public marker, `IReadOnlyList`/`ReadOnlyCollection` 자동 감지는 사용하지 않는다. B1을 먼저 green으로 만든 뒤 B2를 적용한다. B2가 과거 결과 불변·reflection fixture·hash/trace parity를 통과하지 못하면 B2만 제거하고 B1을 유지한다. backing List/array pooling과 다음 Tick 재사용은 금지한다.

#### 변경 예상 파일

- 수정: `Gameplay_Loop/Runtime/TickResultBuilder.cs`
- 수정: `Gameplay_Loop/Runtime/TickResult.cs`
- 수정: `Gameplay_Loop/Runtime/TickPipeline.cs`
- 추가 테스트: `Gameplay_Tests/EditMode/Core/TickResultOwnershipCoreTests.cs` 및 Unity `.meta`

#### tests-first 항목

1. **일반 생성자 방어 복사**: mutable List로 `TickResultData`/`TickResult`를 만든 뒤 원본을 변경해도 결과가 그대로다.
2. **trusted wrapper identity**: trusted path의 `TickResultData.FinalEntities`와 최종 `TickResult.FinalEntities`가 같은 instance다.
3. **Builder ownership transfer**: Builder 경로의 defensive copy count는 0이고 owned wrapper는 1개다.
4. **과거 결과 불변**: Tick N 결과를 보관하고 Tick N+1에서 spawn/move/remove를 수행한 뒤 N의 값과 순서가 같다.
5. **mutation 차단**: `IList<EntityState>` 경로의 Add/Remove/index set이 `NotSupportedException`을 내고 두 결과가 변하지 않는다.
6. **값·순서 parity**: 최종 snapshot ordered enumeration, `TickResultData.FinalEntities`, `TickResult.FinalEntities`가 동일하다.
7. **canonical parity**: 변경 전 고정 fixture와 동일한 determinism hash 및 FullCanonical `TickTrace.Text`를 만든다.
8. **call-site allowlist**: owned factory의 production caller가 `TickResultBuilder.Build` 한 곳뿐이다.
9. **legacy parity**: 같은 ordered source의 general-copy reference와 owned 경로가 FinalEntities/EventLog/hash/FullCanonical trace에서 동일하다.

Reference equality 검사는 public API 계약이 아니라 내부 성능 계약이므로 internal test assembly에서만 수행한다. private field 타입을 reflection으로 사용하는 기존 PlayMode fixture도 focused regression 대상으로 식별해 실행한다.

### 6.3 S1-C — 기본 Factory `EntityType` 후보 캐시

#### capability 계약

기존 public interface는 변경하지 않고 internal opt-in interface를 추가한다.

```csharp
internal interface IEntityLogicFactoryEntityTypePrefilter
{
    bool MayCreateForEntityType(EntityType entityType);
}
```

`MayCreate`는 “이 type이면 반드시 생성한다”가 아니라 “기존 `CanCreate`를 호출할 후보일 수 있다”는 보수적 superset 의미다. 기본 Factory가 선언할 범위는 다음과 같다.

| Factory | 지원 type |
|---|---|
| `EnemyEntityLogicFactory` | `EntityType.Unit` |
| `EnemyActionStateEntityLogicFactory` | `EntityType.Unit` |
| `EnemyCombatEntityLogicFactory` | `EntityType.Unit` |
| `SlidingBoxEntityLogicFactory` | `EntityType.Box` |

prefilter는 `CanCreate`를 대체하지 않는다. candidate list에 들어온 뒤에도 profile, archetype, capability 등 기존 세부 조건을 `CanCreate`가 판정한다. predicate는 provider lifetime 동안 pure/stable해야 한다.

#### 캐시 생성과 fallback

`SnapshotEntityLogicProvider` 생성자에서 원래 Factory 목록의 read-only 복사본을 먼저 확정하고, 알려진 `EntityType`별 ordered candidate list를 한 번 만든다.

```text
각 known EntityType에 대해, 원래 등록 순서로 Factory 순회:
  - prefilter 미구현 Factory => 포함
  - prefilter 구현 + MayCreateForEntityType(type) => 포함
  - prefilter 구현 + false => 제외

Build의 각 entity에 대해:
  - known type => 미리 만든 ordered candidate list
  - unknown enum value => 원래 전체 Factory list
```

주의사항:

- `EntityType`의 현재 값은 flag 집합이 아니다. `None=0`, `Unit=1`, `Box=3`이므로 raw bit mask/index trick을 사용하지 않는다.
- `Enum.GetValues` 결과만 무조건 신뢰하지 말고 명시적으로 known values를 다룬다. 미래 enum 값은 전체 목록 fallback으로 안전하게 동작한다.
- prefilter 없는 Custom Factory는 Wall/`None`, Unit, Box, unknown을 모두 계속 받는다.
- 각 candidate list는 원래 등록 순서를 보존한다.
- 동일 Factory instance가 중복 등록돼 있으면 중복도 보존한다.
- cache는 provider instance별 private derived state이며 static/global로 공유하지 않는다.
- Build는 entity-major 순서를 유지한다. type별로 entity를 재그룹화하지 않는다.
- static logic 처리와 phase owner index는 변경하지 않는다.
- `TryResolveEnemyGlidePresentationSettings`는 원래 전체 `_entityLogicFactories`를 계속 순회한다.
- snapshot은 매 Tick 새로 읽으므로 runtime Wall spawn/remove에 stale entity cache가 생기지 않는다.

#### 변경 예상 파일

- 수정: `Gameplay_Entities/Runtime/IEntityLogic.cs` — 기존 public Factory interface 인접 위치에 internal prefilter 추가
- 수정: `Gameplay_Entities/Runtime/SnapshotEntityLogicProvider.cs`
- 수정: `Gameplay_Entities/Runtime/SlidingBoxLogic.cs`
- 수정: `Gameplay_EnemyAI/Runtime/EnemyEntityLogicFactory.cs`
- 수정: `Gameplay_EnemyAI/Runtime/EnemyActionStateLogic.cs`
- 수정: `Gameplay_Tests/EditMode/Core/SnapshotEntityLogicProviderCoreTests.cs`

#### tests-first 항목

1. unscoped Custom Factory가 Wall/`EntityType.None`을 계속 관찰한다.
2. prefiltered Factory의 `CanCreate`는 unsupported type에 호출되지 않는다.
3. prefiltered와 unscoped Factory 혼합 시 entity-major/Factory 등록 순서가 동일하다.
4. 후보 필터링 후에도 동일 phase의 첫 owner가 동일하다.
5. static logic이 dynamic logic보다 먼저 owner가 된다.
6. 네 기본 Factory의 prefilter가 `CanCreate == true` 가능한 모든 type의 superset인지 pin한다.
7. runtime Wall spawn/remove 및 same-ID `None -> Unit -> Box` 재사용 뒤 다음 Build 결과가 최신 snapshot과 같다.
8. `(EntityType)int.MaxValue` 같은 unknown 값은 전체 Factory fallback을 사용한다.
9. glide presentation resolver는 후보 캐시와 무관하게 원래 Factory 순서를 유지한다.
10. 진단 count에서 기본 Wall probe는 0이고 unscoped Wall probe는 기존 값과 같다.
11. prefilter predicate는 provider 생성 시 audited type 3개에 대해서만 평가되고 Build 두 번 뒤에도 호출 수가 늘지 않는다.
12. resolver capability를 보존한 legacy-equivalent full-scan provider와 optimized provider를 같은 fixture로 실행했을 때 logic set, accepted owner, glide settings, FinalEntities, EventLog, hash, FullCanonical `TickTrace.Text`가 동일하다.
13. unscoped recorder Factory의 `CanCreate(entity, factory)`와 성공 시 `Create(entity, factory)` 전체 sequence가 legacy와 동일하다.
14. opt-in 기본 Factory sequence는 legacy sequence에 `MayCreateForEntityType` predicate를 적용한 결과와 동일하며, unsupported 기본 `CanCreate`만 빠진다.
15. 같은 Factory instance의 중복 등록은 candidate cache에서 dedupe되지 않고 원래 상대 순서와 호출 횟수를 유지한다.
16. `EntityLogicCandidateCacheConstructionCount`는 capture 안에서 생성한 provider당 1이고 Build 두 번 뒤에도 늘지 않는다.
17. `CandidateFactoryCache_IsConstructorOnly_AndBuildDoesNotMaterializeCandidates` source guard가 constructor-only helper 호출과 Build의 cached/original-list 선택을 고정한다.

11번 테스트는 `None`, `Unit`, `Box`에 대한 생성 시 3회 호출을 pin한다. 12번의 replay 비교는 같은 코드 두 번의 결정성만 보는 테스트를 넘어 최적화 전 full-scan 의미와 최적화 후 prefilter 의미를 직접 비교한다. 13번과 14번은 “모든 dispatch 동일”이라는 모순된 조건을 사용하지 않고 Custom side effect 보존과 의도적 기본 probe 제거를 분리한다.

확장 규칙은 correctness-safe default를 우선한다.

- 새 Factory는 prefilter 미구현 상태로 추가하며, 이때 모든 entity type을 관찰한다.
- 성능상 opt-in이 필요할 때만 `MayCreateForEntityType` superset 계약 테스트와 함께 prefilter를 구현한다.
- 새 `EntityType`은 먼저 원본 전체 Factory fallback으로 동작하고, audited known cache key와 테스트를 함께 추가할 때만 최적화한다.
- candidate cache는 provider constructor에서 완성하며 `Build` 중 lazy 생성·변경하지 않는다.

## 7. 실행 순서와 각 Checkpoint

### Checkpoint 0 — 시작 상태 고정

```bash
git status --short --branch
git diff --stat
./run_tests.sh --print-config
```

- 기존 사용자 변경을 목록화하고 덮어쓰지 않는다.
- 현재 worktree는 정책상 grandfathered C-drive legacy path다. 새 worktree를 만들지 않는 한 이동 대상이 아니다.
- 새 worktree가 필요해지면 `/mnt/d/J2M/worktrees` 아래에서 `j2m-worktree-add`만 사용한다.
- S1-A hook을 추가하기 전에 성능 baseline을 동일 명령으로 3회 수집한다.

### Checkpoint 1 — S1-A diagnostics red/green 및 독립 close

1. 테스트가 컴파일될 수 있는 최소 no-op diagnostics carrier/API만 먼저 둔다. 이 scaffolding은 counter property와 capture scope를 정의하지만 production hook이나 최적화 알고리즘을 추가하지 않는다.
2. `TickWorkDiagnosticsTests`에 A의 baseline accounting와 capture 비간섭 테스트만 추가한다. B1/B2/C의 아직 존재하지 않는 API를 직접 참조하는 테스트는 이 단계에 넣지 않는다.
3. 아래 filtered full을 실행하여 counter가 0인 assertion red를 확인한다. missing-symbol compile red 또는 matching test 0은 유효한 tests-first evidence가 아니다.
4. Builder/Result/Provider에 aggregate hook을 연결하고 A 테스트를 green으로 만든다.
5. focused와 core가 green인 상태에서 A commit을 만든다. red revision은 commit하지 않는다.
6. 그 A-only revision에서 diagnostics capture Off 성능을 3회 수집한다. gate 실패 시 A commit을 rollback하고 원인을 분리한다.

```bash
./run_tests.sh full --filter 'TickWorkDiagnosticsTests;SnapshotBudgetGuardTests'
./run_tests.sh core
```

### Checkpoint 2 — S1-B1 trusted assignment red/green

1. compile-only `TickResult.CreateFromOwnedData` scaffold를 추가하되 기존 enumerable-copy constructor로 위임해 semantic을 바꾸지 않는다.
2. B1에 필요한 `TickResultData -> TickResult` identity, defensive-origin, mutation 차단 테스트만 추가한다.
3. 기존 재복사 때문에 reference identity와 `copy=1/share=1` budget이 실패하는 assertion red를 확인한다. compile red는 유효 evidence가 아니다.
4. ownership-token private constructor를 구현해 named factory만 trusted assignment를 사용하게 한다. EventLog 및 다른 컬렉션 복사는 변경하지 않는다.
5. focused/core/replay가 green인 상태에서 B1 commit을 만든다.
6. A+B1 상태 성능을 3회 수집한다.

```bash
./run_tests.sh core --filter TickResultOwnershipCoreTests
./run_tests.sh full --filter 'TickWorkDiagnosticsTests;WorldSnapshotAndPresentationTests'
./run_tests.sh --integration-replay --filter TickReplayDeterminismTests
./run_tests.sh core
```

### Checkpoint 3 — S1-B2 Builder ownership red/green

1. compile-only `TickResultData.CreateFromOwnedFinalEntities` scaffold를 추가하되 기존 enumerable-copy constructor로 위임해 semantic을 바꾸지 않는다.
2. B1이 green인 뒤에만 B2의 `copy=0/wrapper=1`, previous-result lifetime, production call-site allowlist 테스트를 추가한다.
3. 기존 Builder-to-Data 복사 때문에 B2 budget이 실패하는 assertion red를 확인한다. compile red는 유효 evidence가 아니다.
4. Builder가 ordered List를 exact `ReadOnlyCollection`으로 한 번만 감싸고, ownership-token private constructor를 사용하는 named factory가 그 wrapper를 그대로 인수하게 한다.
5. six reflection fixture, focused/core/replay를 모두 green으로 만든 뒤 B2 commit을 만든다.
6. A+B1+B2 상태 성능을 3회 수집한다.

```bash
./run_tests.sh core --filter TickResultOwnershipCoreTests
./run_tests.sh full --filter 'TickWorkDiagnosticsTests;WorldSnapshotAndPresentationTests'
./run_tests.sh full --filter 'BoxMotionReadinessPlayModeTests;ActualSceneBootstrapSmokePlayModeTests;DamageDeathVfxProductionDefaultPlayModeTests;EnemyPresentationReadinessPlayModeTests;GameplayAudioIntegrationPlayModeTests;PlayerActionAnimationReadinessPlayModeTests'
./run_tests.sh --integration-replay --filter TickReplayDeterminismTests
./run_tests.sh core
```

### Checkpoint 4 — S1-C Factory prefilter red/green

1. compile-only internal `IEntityLogicFactoryEntityTypePrefilter` interface를 먼저 추가하되 provider는 아직 소비하지 않는다.
2. A/B가 green인 뒤 C의 prefilter, resolver-capable legacy adapter, Custom sequence, default filtered sequence 테스트를 추가한다.
3. 기존 provider가 unsupported 기본 Factory까지 호출하는 assertion red를 확인한다. 전체 legacy/default dispatch 동일성이나 compile red는 red 조건으로 사용하지 않는다.
4. 기본 Factory opt-in과 provider-instance-local candidate cache를 구현한다.
5. 기본 Wall probe 0, unscoped Custom sequence 불변, semantic/hash/trace/glide parity를 green으로 만든다.
6. focused/core/replay가 green인 상태에서 C commit을 만든다.
7. A+B1+B2+C 상태 성능을 3회 수집한다.

```bash
./run_tests.sh core --filter SnapshotEntityLogicProviderCoreTests
./run_tests.sh full --filter TickWorkDiagnosticsTests
./run_tests.sh --integration-replay --filter TickReplayDeterminismTests
./run_tests.sh core
```

새 fixture 이름이 달라지면 실제 이름으로 filter를 갱신한다. aggregate matching test 0은 통과로 취급하지 않는다.

### Checkpoint 5 — Goal closeout

- A-only 대 baseline, B1 대 A, B2 대 B1, C 대 B2, 최종 대 baseline을 각각 비교한다.
- 구조 gate, 기능 gate, 성능 gate를 package/checkpoint별로 판정한다.
- deterministic structural reduction은 이 Slice에서 유효한 측정 benefit이다. 해당 구조 gate가 증명되고 p95 비악화 gate를 통과하면 wall-clock 개선이 noise 안이라는 이유만으로 제거하지 않는다.
- 구조 감소가 증명되지 않거나 p95가 악화되거나 유지보수 계약을 지키지 못한 패키지는 제거한다.
- 최종 diff, generated asset, source mutation guard, evidence path를 확인한다.

## 8. 검증 매트릭스

### 8.1 이 문서 작성 전 확보된 baseline

| 명령 | 결과 |
|---|---|
| `./run_tests.sh core --filter SnapshotEntityLogicProviderCoreTests` | EditMode 7 passed, 0 failed; matching PlayMode 없음 |
| `./run_tests.sh full --filter 'CleanupPhaseScenarioTests;SnapshotBudgetGuardTests;WorldSnapshotAndPresentationTests'` | EditMode 130 passed, 0 failed; matching PlayMode 없음 |
| `./run_tests.sh --integration-replay --filter TickReplayDeterminismTests` | EditMode 59 passed, 0 failed |

이는 변경 전 기준선이며 미래 구현의 통과 증거가 아니다.

### 8.2 구현 후 필수 명령

```bash
./run_tests.sh core --filter SnapshotEntityLogicProviderCoreTests
./run_tests.sh core --filter TickResultOwnershipCoreTests
./run_tests.sh full --filter 'TickWorkDiagnosticsTests;SnapshotBudgetGuardTests;WorldSnapshotAndPresentationTests'
./run_tests.sh full --filter 'BoxMotionReadinessPlayModeTests;ActualSceneBootstrapSmokePlayModeTests;DamageDeathVfxProductionDefaultPlayModeTests;EnemyPresentationReadinessPlayModeTests;GameplayAudioIntegrationPlayModeTests;PlayerActionAnimationReadinessPlayModeTests'
./run_tests.sh --integration-replay --filter TickReplayDeterminismTests
./run_tests.sh core
```

필요 시 실제 새 fixture 이름으로 filter를 대체한다. 위 6개 filtered PlayMode fixture는 `_finalEntities` concrete type seam 때문에 S1-B의 필수 gate이며, 실행하지 못하면 Slice 1 Goal을 완료 처리하지 않는다. 한 번의 결합 filter가 wrapper timeout에 걸리면 동일 revision에서 fixture별로 분리 실행해 전부 통과시킨다. 추가 consumer surface를 건드리면 관련 PlayMode fixture를 더한다.

### 8.3 기능 acceptance

- focused 신규 테스트 전부 통과.
- `./run_tests.sh core` 통과.
- replay determinism fixture 통과.
- FinalEntities 값/순서, EventLog, hash, FullCanonical trace가 baseline fixture와 동일.
- previous result immutability와 general/internal enumerable constructor defensive copy가 유지.
- entity-major/Factory order, static precedence, first phase owner가 유지.
- unknown enum 및 unscoped Custom Factory fallback이 유지.

프로젝트의 broad full baseline은 문서상 red다. unfiltered full을 같은 revision에서 실행해 실제 통과하지 않은 이상 `project-wide green`, `full regression closed` 같은 표현을 사용하지 않는다.

## 9. 성능 검증

### 9.1 측정 방법

공식 wrapper만 사용한다.

```bash
GAMEPLAY_PERFORMANCE_SAMPLE_FRAMES=1200 \
GAMEPLAY_PERFORMANCE_TICK_INTERVAL=1 \
./run_tests.sh gameplay-performance
```

이 lane은 `stage-1-1`, ReleaseLike Windows Mono Player의 `render-idle` 및 `gameplay-neutral-tick`을 측정하며 위 명령에서는 phase당 1,200 frame과 매 frame Tick을 사용한다. evidence는 `/mnt/d/J2M/evidence/gameplay-performance`에 저장한다. capture 성공은 성능 예산 통과를 뜻하지 않는다.

다음 다섯 상태를 각각 동일 PC/해상도/품질/샘플 수에서 3회 수집한다. 비교값은 각 상태의 **세 run p95 중 중앙값**이다.

1. 변경 전 current-HEAD baseline
2. S1-A only, diagnostics capture Off
3. S1-A + S1-B1
4. S1-A + S1-B1 + S1-B2
5. S1-A + S1-B1 + S1-B2 + S1-C

2026-08-27 사후 감사 이후 재측정은 [Post-Closeout Audit and Recovery Plan](./Gameplay-Wall-Tick-Cost-Optimization-Slice1-Post-Closeout-Audit-2026-08-27.md)의 고정 block 순서를 따른다. 각 revision의 full measurement warm-up은 정확히 1회만 사전 폐기하고, official 15 run을 시작한 뒤 admission을 통과한 값은 p95를 본 후 제외하거나 warm-up으로 재분류하지 않는다.

각 증거에 revision, worktree diff hash, metrics path를 남긴다. 서로 다른 revision의 절대 수치를 한 revision의 동시 비교처럼 표현하지 않는다.

각 run은 p95 집계 전에 다음 identity/표본 동일성 preflight를 통과해야 한다. requested와 actual resolution은 모두 `1920x1080`이어야 하고, 두 phase의 CPU/GPU 표본은 각각 1,200이어야 한다. 위 고정 명령은 매 frame Tick을 요청하므로 `gameplay-neutral-tick`의 `attemptedTicks`, `executedTicks`, `tickWallMilliseconds.count`도 모두 1,200이어야 한다. 하나라도 다르면 해당 run은 p95 비교 증거로 사용하지 않고 같은 사전 지정 slot에서 다시 수집한다. 실패 evidence와 사유는 삭제하지 않는다.

```bash
slice1_metrics_path=/absolute/path/to/performance-metrics.json
slice1_expected_revision=/full/planned/revision
python3 - "$slice1_metrics_path" "$slice1_expected_revision" <<'PY'
import json
import math
import sys

with open(sys.argv[1], encoding="utf-8") as stream:
    metrics = json.load(stream)

phases = {phase.get("phase"): phase for phase in metrics.get("phases", [])}
idle = phases.get("render-idle", {})
gameplay = phases.get("gameplay-neutral-tick", {})
valid = (
    metrics.get("developmentBuild") is False
    and metrics.get("revision") == sys.argv[2]
    and metrics.get("requestedResolution") == [1920, 1080]
    and metrics.get("actualResolution") == [1920, 1080]
    and metrics.get("warmupFrames") == 120
    and metrics.get("sampleFramesPerPhase") == 1200
    and metrics.get("gameplayTickIntervalFrames") == 1
    and metrics.get("vSyncCount") == 0
    and metrics.get("targetFrameRate") == -1
    and len(metrics.get("phases", [])) == 2
    and set(phases) == {"render-idle", "gameplay-neutral-tick"}
    and all(
        phase.get("sampleCount") == 1200
        and phase.get("validCpuMainSamples") == 1200
        and phase.get("validGpuSamples") == 1200
        for phase in (idle, gameplay)
    )
    and idle.get("attemptedTicks") == 0
    and idle.get("executedTicks") == 0
    and idle.get("tickWallMilliseconds", {}).get("count") == 0
    and gameplay.get("attemptedTicks") == 1200
    and gameplay.get("executedTicks") == 1200
    and gameplay.get("tickWallMilliseconds", {}).get("count") == 1200
    and isinstance(gameplay.get("tickWallMilliseconds", {}).get("p95"), (int, float))
    and math.isfinite(gameplay.get("tickWallMilliseconds", {}).get("p95"))
    and gameplay.get("tickWallMilliseconds", {}).get("p95") > 0
)
if not valid:
    print("invalid Slice 1 metrics-local sample admission", file=sys.stderr)
    raise SystemExit(1)

print("valid Slice 1 metrics-local sample; external identity admission pending")
PY
```

위 snippet은 metrics 한 파일의 하위 조건만 검사하며 formal admission 완료를 뜻하지 않는다. formal external validator는 metrics path, manifest path, planned runtime revision, expected clean diff hash, campaign identity baseline을 입력으로 받아 manifest `HEAD`, clean diff, Unity/OS/CPU/GPU/quality/backend cohort까지 함께 비교해야 한다. campaign 시작 전에 validator content SHA-256과 immutable `campaign-plan.json` SHA-256을 고정하고 모든 admission record가 두 hash를 참조해야 한다. `run_tests.sh`가 필수 dependency로 확인하는 `python3`만 사용한다. 2026-08-27 감사 시점의 wrapper는 CPU/GPU를 phase별로 구조 검증하지 않고 actual resolution 및 Tick 표본 동일성도 강제하지 않으므로, historical revision을 측정할 때는 full external validator의 판정을 p95 집계 전에 evidence에 남긴다.

판정 pair와 metric은 다음으로 고정한다.

| 판정 | candidate / baseline | JSON metric |
|---|---|---|
| A hook overhead | state 2 / state 1 | `.phases[] | select(.phase == "gameplay-neutral-tick") | .tickWallMilliseconds.p95` |
| B1 | state 3 / state 2 | 동일 |
| B2 | state 4 / state 3 | 동일 |
| C | state 5 / state 4 | 동일 |
| 최종 | state 5 / state 1 | 동일 |

각 상태의 세 p95 값 중 중앙값끼리 비교한다. 유효 GC/allocation sample이 존재하면 같은 adjacent pair와 final pair를 사용한다.

### 9.2 구조 acceptance

- 내부 Builder 경로 ordered materialization: 1
- 내부 Builder 경로 defensive `FinalEntities` copy: 0
- owned read-only wrapper creation: 1
- `TickResultData -> TickResult` trusted assignment: 1
- 기본 Factory의 Wall/`None` `CanCreate` probe: 0
- 기본 Factory actual probe 공식: `3 * UnitCount + 1 * BoxCount + 4 * UnknownCount`; 현재 known live roster에서는 Unknown이 0이다.
- unscoped Custom Factory의 Wall/`None` probe: baseline과 동일
- candidate cache construction: capture 안에서 생성한 provider instance당 1회
- Build당 candidate list allocation: 0, Core source architecture guard로 검증
- created logic 집합과 phase owner 순서: baseline과 동일

### 9.3 시간/할당 acceptance

- A/B1/B2/C 각 adjacent pair와 최종 pair의 Tick p95가 비교 baseline 중앙값 대비 5% 넘게 악화되지 않는다.
- 유효 GC sample이 존재하면 trace-off sustained allocation이 비악화여야 한다.
- B는 해당 경로의 entity collection allocation/복사량 감소가 구조 계측으로 확인되어야 한다.
- C는 Factory probe 감소가 구조 계측으로 확인되어야 한다.
- wall-clock 개선은 3회 변동 범위와 함께 보고하며, noise 범위 수치를 확정적 개선으로 주장하지 않는다.

현재 gameplay-performance lane에서 GC counter가 unavailable이면 allocation 개선을 추정하거나 GC 비악화를 합격 처리하지 않는다. 구조 카운터와 source guard를 primary gate로 사용하고, 별도 Development Profiler evidence를 확보하거나 allocation 범위가 미검증임을 closeout에 명시한다. `stage-1-1` 고정 lane은 S1-C 같은 micro-optimization의 wall-clock 판정력이 낮으므로 C의 시간값은 참고/5% 비악화 gate이며 기본 Wall probe 0이 primary acceptance다.

이 Slice에서는 실제 element copy와 기본 Factory probe의 deterministic reduction 자체를 유효한 measured benefit으로 정의한다. 따라서 구조 gate 통과 + semantic parity + p95 비악화를 만족하면 wall-clock 차이가 run-to-run noise 안이어도 retain할 수 있다. 구조 감소가 없는 단순 추상화는 이 예외를 적용받지 않는다.

## 10. Stop / Rollback Gate

다음 중 하나라도 발생하면 해당 패키지를 중단하고 직전 checkpoint로 되돌린다.

- mutable backing List/array가 ownership 경계 밖으로 노출됨.
- 과거 `TickResult`가 다음 Tick 뒤 변함.
- general/internal enumerable constructor의 방어 복사가 사라짐.
- FinalEntities/EventLog/hash/FullCanonical trace/replay 값 또는 순서가 달라짐.
- unscoped Custom Factory가 어떤 type을 더 이상 보지 못함.
- unknown enum이 전체 Factory fallback을 사용하지 않음.
- static precedence 또는 first phase owner가 달라짐.
- `MayCreateForEntityType`가 entity마다 매 Tick 호출되어 단순 virtual call 치환에 그침.
- `TryResolveEnemyGlidePresentationSettings`가 필터링되어 resolver 동작이 달라짐.
- `_finalEntities` field type이 `ReadOnlyCollection<EntityState>`에서 변경됨.
- named owned/trusted factory 외의 caller가 ownership-token constructor에 도달하거나 exact-type overload가 trusted path를 자동 선택함.
- diagnostics capture Off에서 새 collection 또는 string allocation이 생김.
- candidate cache가 static/global이거나 `Build` 중 생성·변경됨.
- 새/unknown Factory가 prefilter 미구현 상태에서 모든 entity를 관찰하지 못함.
- unscoped Custom Factory의 exact `CanCreate/Create` recorder sequence가 legacy reference와 다름.
- opt-in 기본 Factory sequence가 legacy reference에 `MayCreateForEntityType` predicate를 적용한 sequence와 다름.
- core 또는 touched-cluster 회귀를 원인 분리하지 못함.
- 해당 adjacent pair 또는 최종 pair의 Tick p95가 비교 baseline보다 5% 넘게 악화됨.
- diagnostics Off인데도 계측 hook 때문에 지속 allocation이 생김.

Rollback 순서는 C prefilter cache, B2 Builder ownership transfer, B1 `TickResultData -> TickResult` sharing, A diagnostics hook 순이다. 계약 테스트는 회귀 방지 가치가 있으면 별도 test commit으로 유지할 수 있다.

## 11. Commit 분리

권장 intent는 다음과 같다.

1. `refactor: Gameplay - Tick workload diagnostics seam add`
2. `refactor: Gameplay - Final result trusted assignment add`
3. `refactor: Gameplay - Final entity list ownership transfer`
4. `refactor: Gameplay - Entity logic Factory type prefilter add`
5. `docs: Gameplay - Wall tick Slice 1 evidence close out`

tests-first red는 각 production 작업 전에 로컬 evidence로 확인하되 red revision을 commit하지 않는다. 첫 commit에는 runtime diagnostics seam/hooks와 그 정확성·비간섭 테스트를 함께 넣어 독립 green/revert가 가능하게 한다.

각 commit 전 staged diff를 검토하고 `AI_GIT_COMMIT_RULES.md` 형식의 의미 있는 body를 작성한다. scene/asset 변경은 예상되지 않으며 생성되면 의도하지 않은 변경인지 먼저 조사한다.

## 12. Goal 진행 기록 형식

자동 continuation 또는 인수인계 때 다음 블록을 갱신한다.

```text
Goal objective:
Current package: S1-A | S1-B1 | S1-B2 | S1-C | closeout
Revision / branch:
Pre-existing user changes preserved:
Tests added first:
Focused commands and exact pass/fail counts:
Core result:
Replay result:
Performance evidence paths:
Structural counts before -> after:
Wall-clock/GC observations and noise range:
Open risks:
Next safe action:
```

Goal 완료는 구현이 끝났다는 선언이 아니라 다음을 모두 만족한 상태다.

- 각 패키지가 `retain` 또는 `reject`로 명시적으로 판정됨.
- retained 패키지는 해당 기능·구조·성능 gate를 모두 통과함.
- rejected 패키지는 최종 tree에서 제거되고 rollback 이유와 증거가 기록됨.
- B2 또는 S1-C를 reject하면서 원래 objective를 유지하는 경우에는 Goal 완료 불가이며 objective scope amendment와 근거 기록이 필요함.
- focused, core, replay 검증을 변경 revision에서 통과.
- baseline/A/B1/B2/C 각 상태의 3회 성능 증거와 adjacent/final 변동 범위 기록.
- 패키지별 유지/롤백 판정 기록.
- 관련 문서와 실제 코드 상태가 일치.
- 실행하지 않은 full/PlayMode/manual 범위를 명시.

## 13. 남는 한계와 다음 Slice 진입 조건

Slice 1이 성공해도 Full trace가 모든 entity를 반복 포맷하는 비용, snapshot 복사, Cleanup full scan은 남는다. 따라서 사용자 체감 렉이 사라진다고 보장하지 않는다.

다음 큰 Slice로 이동하는 조건은 다음과 같다.

- Slice 1 closeout 완료.
- 같은 revision 계열의 component attribution에서 남은 지배 비용이 식별됨.
- trace-on hitch가 주 문제라면 Cleanup보다 diagnostics policy split을 우선 재평가.
- Cleanup scan이 지배 비용으로 확인되면 상위 계획의 ordered candidate indexes를 별도 Goal로 시작.

Slice 1의 성공 기준은 Wall의 의미를 약화시키는 것이 아니라, **동일한 결과를 더 적은 내부 복사와 더 적은 불필요한 기본 Factory probe로 만드는 것**이다.

## 14. Goal 진행 기록

### 2026-08-26 — S1-A retained

- Goal objective: 내부 Builder 경로의 `FinalEntities` 복사 2회와 기본 Factory의 불필요한 Wall 탐색을 줄이되 authoritative/replay/hash 계약을 유지한다.
- Current package: S1-B1 준비. S1-A는 `retain`.
- Revision / branch: `2dc8e5c53`, `codex/third-party-license-inventory`.
- Pre-existing user changes preserved: 이 Goal plan, 상위 optimization plan, `Docs/Architecture/README.md`의 두 링크 변경을 runtime checkpoint commit에 포함하지 않았다.
- Tests added first: compile-safe no-op diagnostics carrier 뒤 `TickWorkDiagnosticsTests` 3개를 추가했다. 첫 focused EditMode는 `7 total / 3 failed`로 assertion red였고 missing-symbol/build red가 아니었다.
- Focused green: `./run_tests.sh full --filter 'TickWorkDiagnosticsTests;SnapshotBudgetGuardTests'`는 EditMode `7/0`, matching PlayMode `0`으로 통과했다.
- Core result: commit 전과 commit hook에서 각각 `./run_tests.sh core`가 EditMode `217/0`, PlayMode `111/0`으로 통과했다.
- Replay result: S1-A checkpoint에서는 미실행. B1 이후 필수 replay fixture를 실행한다.
- Baseline performance evidence: `20260826T135259Z`, `20260826T135602Z`, `20260826T135824Z` 아래 `performance-metrics.json`; p95 `7.699235 / 7.348720 / 6.985725 ms`, median `7.348720 ms`.
- S1-A performance evidence: `20260826T141514Z`, `20260826T141753Z`, `20260826T142012Z` 아래 `performance-metrics.json`; p95 `7.188575 / 6.674165 / 7.251020 ms`, median `7.188575 ms`.
- Tick sample preflight: baseline/A 여섯 run 모두 `sampleFramesPerPhase=1200`, interval `1`, attempted/executed/count `1200/1200/1200`으로 개별 admission 통과.
- Structural counts before -> after: 계측 부재 -> Builder enumeration `1`, defensive FinalEntities copy `2`, owned wrapper `0`, trusted share `0`; 3 entity × 2 Factory fixture에서 visits `3`, opportunities/probes `6/6`, created/accepted/conflict `6/6/0`, `None/Unit/Box` bucket 각각 visits `1`, probes `2`.
- Wall-clock/GC observations: A median은 baseline 대비 `-2.179223%`; baseline range `6.985725..7.699235 ms`, A range `6.674165..7.251020 ms`. 5% 악화 gate 통과. release-like lane의 unavailable GC counter로 allocation 비악화는 판정하지 않았다.
- Open risks: diagnostics capture-off allocation은 source shape와 성능 gate로만 확인했으며 별도 Development Profiler allocation evidence는 없다. broad unfiltered full은 문서상 red이고 실행하지 않았다.
- Next safe action: B1 named factory를 기존 enumerable-copy constructor로 위임하는 semantic no-op scaffold로 추가하고, identity/copy/share/defensive-origin 계약의 assertion red를 확인한다.

### 2026-08-27 — S1-B1 retained

- Current package: S1-B2 준비. S1-B1은 `retain`.
- Revision / branch: `79889c4b7`, `codex/third-party-license-inventory`.
- Pre-existing user changes preserved: Goal/상위 plan과 README 링크 변경은 B1 runtime checkpoint commit에도 포함하지 않았다.
- Tests added first: `TickResult.CreateFromOwnedData`를 기존 enumerable-copy constructor에 위임하는 semantic no-op scaffold 뒤 `TickResultOwnershipCoreTests` 3개를 추가했다. 첫 focused Core EditMode는 `3 total / 1 failed`로 wrapper identity assertion red였고 Windows build는 통과했다.
- Focused green: ownership Core `3/0`; `TickWorkDiagnosticsTests;WorldSnapshotAndPresentationTests` filtered full EditMode `102/0`, matching PlayMode `0`.
- Core result: commit 전과 commit hook에서 `./run_tests.sh core` EditMode `220/0`, PlayMode `111/0` 통과.
- Replay result: `./run_tests.sh --integration-replay --filter TickReplayDeterminismTests` EditMode `59/0` 통과.
- Structural counts before -> after: internal Builder path enumeration `1 -> 1`, defensive FinalEntities copy `2 -> 1`, owned wrapper `0 -> 0`, trusted TickResult share `0 -> 1`. General `IEnumerable<EntityState>` constructors는 mutable source 변경 뒤에도 각각 원래 값을 유지했다.
- Initial sequential B1 capture: `20260826T143641Z`, `20260826T144013Z`, `20260826T144319Z`; p95 `10.099025 / 9.065730 / 8.384020 ms`, median `9.065730 ms`. 모두 Tick sample admission은 통과했지만 값이 run 순서대로 급락해 nonstationary load window로 분류했고 retain 판정에는 사용하지 않았다.
- Same-time control: A revision `20260826T144731Z`가 `7.467520 ms`, 직후 B1 revision `20260826T145141Z`가 `7.015890 ms`여서 초기 sequential triplet의 B1-specific `+26.113%` 가설이 재현되지 않았다.
- Stabilized interleaved A evidence: `20260826T144731Z`, `20260826T145457Z`, `20260826T150101Z`; p95 `7.467520 / 7.046135 / 8.111825 ms`, median `7.467520 ms`, range `7.046135..8.111825 ms`.
- Stabilized interleaved B1 evidence: `20260826T145141Z`, `20260826T145756Z`, `20260826T150415Z`; p95 `7.015890 / 7.251195 / 7.862125 ms`, median `7.251195 ms`, range `7.015890..7.862125 ms`.
- Tick sample preflight: stabilized A/B1 여섯 run 모두 interval `1`, attempted/executed/count `1200/1200/1200`으로 개별 admission 통과.
- Wall-clock/GC observations: stabilized B1 median은 stabilized A 대비 `-2.896879%`; 5% 악화 gate 통과. GC counter unavailable로 allocation은 구조 카운터를 primary evidence로 사용했다.
- Open risks: 초기 sequential set은 Tick 수는 같았지만 wall-clock stationarity가 없었다. 후속 checkpoint 성능은 처음부터 revision을 번갈아 측정해 같은 confound를 줄인다. broad unfiltered full은 실행하지 않았다.
- Next safe action: B2 named owned-list factory를 기존 enumerable-copy constructor에 위임하는 scaffold로 추가하고, Builder copy `0`, owned wrapper `1`, previous-result lifetime, production caller allowlist의 assertion red를 확인한다.

### 2026-08-27 — S1-B2 rejected and rolled back

- Current package: S1-B2는 `reject`; 최종 tree는 S1-B1 경계로 복귀했다.
- Revision / branch: 구현 `19aa14745`, rollback `bfe16e8dd`, `codex/third-party-license-inventory`.
- Pre-existing user changes preserved: Goal/상위 plan과 README 링크 변경은 구현 및 rollback commit에 포함하지 않았다.
- Tests added first: named owned-list scaffold 뒤 Builder copy `0`, owned wrapper `1`, previous-result lifetime, production caller allowlist assertion red를 확인했다. 해당 red run의 전체 count는 후속 Unity run으로 `TestResults`가 교체되어 closeout 시점에 복구하지 못했다.
- Focused green before performance decision: ownership Core `6/0`; `TickWorkDiagnosticsTests;WorldSnapshotAndPresentationTests` EditMode `102/0`; replay `59/0`; broad core EditMode `223/0`, PlayMode `111/0`; six reflection-sensitive fixture PlayMode `101/0`.
- Fixture alignment retained separately: Terminal submit fixture는 `a8a21c26f`의 hidden-selection-first 계약에 맞춰 `54a3cf6dc`에서 보정했고 focused `2/0`, UI lane `1340/0`을 통과했다. topology camera smoke는 mixer activity와 finite non-zero offset을 검사하도록 `d24a7ed2d`에서 phase-stable하게 보정했다.
- Structural counts before -> after while B2 was present: Builder enumeration `1 -> 1`, defensive FinalEntities copy `1 -> 0`, owned wrapper `0 -> 1`, trusted share `1 -> 1`. previous-result lifetime, exact field type, single production caller, hash/trace parity gate를 통과했다.
- Alternating retained-B1 evidence: `20260826T162204Z`, `20260826T162822Z`, `20260826T163536Z`; p95 `7.132975 / 6.930090 / 8.217495 ms`, median `7.132975 ms`, range `6.930090..8.217495 ms`.
- Alternating B2 evidence: `20260826T162526Z`, `20260826T163128Z`, `20260826T163919Z`; p95 `6.897785 / 9.715985 / 8.858880 ms`, median `8.858880 ms`, range `6.897785..9.715985 ms`.
- Tick sample preflight: official alternating B1/B2 여섯 run 모두 interval `1`, attempted/executed/count `1200/1200/1200`으로 개별 admission 통과.
- Wall-clock/GC observations: B2 median은 adjacent B1 median 대비 `+24.196145%`로 5% 악화 gate를 실패했다. GC counter는 unavailable이었다. 구조 및 의미 gate 통과와 무관하게 §10에 따라 B2를 rollback했다.
- Final structural state after rollback: Builder enumeration `1`, defensive FinalEntities copy `1`, owned wrapper `0`, trusted share `1`.
- Open risks: objective의 Builder-path copy `0` 요구는 충족되지 않는다. B2를 다시 진행하려면 현재 구현 재적용이 아니라 별도 설계와 새 성능 evidence가 필요하다.
- Next safe action: B2와 기능적으로 독립적인 S1-C를 retained B1 위에서 구현·검증하되, B2 reject로 원래 Goal 완료는 이미 scope amendment 또는 redesign 전까지 불가함을 유지한다.

### 2026-08-27 — S1-C rejected and rolled back

- Current package: S1-C는 `reject`; 최종 runtime은 retained S1-A + S1-B1이다.
- Revision / branch: 구현 `567cc08cc`, rollback `a61c1f658`, `codex/third-party-license-inventory`.
- Pre-existing user changes preserved: Goal/상위 plan과 README 링크 변경은 C 구현 및 rollback commit에 포함하지 않았다.
- Tests added first: compile-only internal prefilter interface와 7-test focused fixture를 추가했다. 첫 compile-success run은 EditMode `7 total / 5 failed`였으며 missing cache/기본 opt-in assertion red와 `WorldState`가 unknown enum placement를 거부한 두 fixture-construction failure가 섞여 있었다. unknown fallback은 placement 계약을 바꾸지 않고 provider private candidate resolver를 통한 raw context dispatch로 교정했다.
- Focused green before performance decision: 새 prefilter Core `7/0`; 기존 `SnapshotEntityLogicProviderCoreTests` `7/0`; `TickWorkDiagnosticsTests` `3/0`; replay `59/0`; broad core EditMode `227/0`, PlayMode `111/0`.
- Semantic/architecture coverage while C was present: unscoped fallback, unsupported default skip, entity-major/Factory order, first/static phase ownership, four default conservative supersets, fresh snapshot and same-ID type reuse, unknown full-list fallback, glide resolver full-list behavior, legacy full-scan FinalEntities/EventLog/hash/FullCanonical parity, custom/default recorder sequence, duplicate registration, provider-local constructor-only cache를 고정했다.
- Structural counts before -> after while C was present: 3 entity x 5 Factory fixture에서 opportunities `15 -> 15`, probes `15 -> 7`, prefilter skips `0 -> 8`, `None` probes `5 -> 1`, 기본 `None` probes `4 -> 0`, unscoped Custom probes `3 -> 3`, candidate cache construction `0 -> 1/provider`. Build 내부 candidate materialization은 source guard상 `0`이었다.
- Alternating retained-B1 control evidence: `20260826T171559Z`, `20260826T172333Z`, `20260826T173250Z`; p95 `8.633495 / 8.744485 / 8.819150 ms`, median `8.744485 ms`, range `8.633495..8.819150 ms`.
- Alternating S1-C evidence: `20260826T171202Z`, `20260826T171940Z`, `20260826T172707Z`; p95 `8.296235 / 10.724550 / 11.959825 ms`, median `10.724550 ms`, range `8.296235..11.959825 ms`.
- Tick sample preflight: alternating B1/C 여섯 run 모두 sample frames `1200`, interval `1`, attempted/executed/count `1200/1200/1200`으로 개별 admission 통과.
- Wall-clock/GC observations: C median은 adjacent retained B1 control 대비 `+22.643586%`로 5% 악화 gate를 실패했다. GC sample은 count `0`, p95 `-1`로 unavailable이었다. §10에 따라 C runtime, interface, opt-in, C-only tests를 모두 rollback했다.
- Final structural state after rollback: 기본 Factory probe는 full scan baseline으로 복귀해 위 fixture 기준 probes `15`, skips `0`, candidate cache construction `0`; 따라서 기본 Wall probe `0` objective는 충족되지 않는다.
- Final closeout validation on `a61c1f658`: ownership Core EditMode `3/0`; diagnostics/world filtered full EditMode `102/0`, matching PlayMode `0`; replay EditMode `59/0`; six reflection-sensitive fixture EditMode `0`, PlayMode `101/0`; rollback commit hook broad core EditMode `220/0`, PlayMode `111/0`.
- Package decisions: S1-A `retain`, S1-B1 `retain`, S1-B2 `reject/removed`, S1-C `reject/removed`.
- Final retained performance: stabilized B1 median `7.251195 ms` versus initial baseline median `7.348720 ms`, `-1.327102%`. 서로 다른 시간대 절대값이며 같은 revision의 동시 비교처럼 해석하지 않는다. B2/C 판정은 각각 위 alternating adjacent controls를 사용했다.
- Open risks and not-run scope: release-like GC/allocation counter가 unavailable이라 sustained allocation 비악화는 검증하지 못했다. broad unfiltered full과 manual/editor asset validation은 실행하지 않았다. Scene/Prefab/ScriptableObject/asset runtime 변경은 없었다.
- Goal status: 원래 objective는 Builder-path copy `0`과 기본 Wall probe `0`을 모두 요구하지만 B2와 C가 성능 gate로 reject되어 충족되지 않는다. §12에 따라 objective scope amendment 또는 두 패키지의 별도 redesign/evidence 없이는 Goal을 완료로 표시할 수 없다.
- Next safe action: objective를 retained S1-A + S1-B1 결과로 축소 승인하거나, B2/C 각각에 대해 현재 rejected 구현과 다른 설계의 후속 Goal을 정의한다.

### 2026-08-27 — redesigned S1-B2 and S1-C retained; Goal complete (historical, superseded)

> Superseded: 후속 독립 감사에서 C2 official triplet의 actual resolution 불일치와 고정 15-run protocol 이탈이 확인됐다. 아래 구조·기능 결과와 당시 산술은 provenance로 유지하지만, redesigned B2/C2 performance pass 및 Goal complete 선언은 철회한다. 현재 판정과 복구 순서는 [Post-Closeout Audit and Recovery Plan](./Gameplay-Wall-Tick-Cost-Optimization-Slice1-Post-Closeout-Audit-2026-08-27.md)을 따른다.

- Goal objective: Wall의 authoritative entity/Solid occupancy 의미를 바꾸지 않고 Builder 경로 `FinalEntities` defensive copy를 `0`으로 줄이고, 기본 Factory의 Wall/`None` probe를 `0`으로 줄이면서 결과·순서·hash·trace·replay 계약을 유지한다.
- Final revision / branch: `29d26ab18`, `codex/third-party-license-inventory`.
- Pre-existing user changes preserved: 이 Goal plan, 상위 optimization plan, `Docs/Architecture/README.md`의 링크 변경은 runtime/test checkpoint와 분리했고 closeout 문서 commit에서만 함께 다룬다.
- Package decisions: S1-A `retain` (`2dc8e5c53`), S1-B1 `retain` (`79889c4b7`), 첫 B2 `reject/removed` (`19aa14745` / `bfe16e8dd`), redesigned B2 `retain` (`4498148ad`), 첫 C `reject/removed` (`567cc08cc` / `a61c1f658`), 첫 concrete-array redesign `reject/removed` (`ddacf8586` / `1767f1f80`), inline/zero-candidate C2 `retain` (`29d26ab18`).

#### Redesigned S1-B2

- 설계: Builder가 ordered `List<EntityState>`를 한 번 materialize한 직후 exact `ReadOnlyCollection<EntityState>` wrapper를 만들고, 이름 있는 `TickResultData.CreateFromOwnedFinalEntities` 경로만 ownership token constructor에 도달한다. general/internal `IEnumerable<EntityState>` 생성자는 계속 defensive copy한다.
- Tests-first red: named factory가 general-copy 생성자로 위임된 semantic no-op 상태에서 filtered full `9 total / 3 failed`; wrapper identity/copy, diagnostics budget, production caller allowlist가 assertion red였고 build red는 아니었다.
- Focused/contract green: combined ownership fixtures EditMode `10/0`; diagnostics/world EditMode `102/0`; replay `59/0`; reflection-sensitive six-fixture PlayMode `101/0`.
- Structural counts: Builder ordered enumeration `1`, defensive copy `0`, owned wrapper creation `1`, trusted share `1`. 과거 결과 lifetime, exact field type, single production caller, owned/general-copy FinalEntities/EventLog/hash/FullCanonical trace parity를 통과했다.
- B1 control evidence: `20260826T183846Z`, `20260826T184452Z`, `20260826T185142Z`; p95 `7.571040 / 8.019165 / 8.385855 ms`, median `8.019165 ms`, range `7.571040..8.385855 ms`.
- Redesigned B2 evidence: `20260826T184144Z`, `20260826T184818Z`, `20260826T185501Z`; p95 `7.877540 / 9.110740 / 7.766425 ms`, median `7.877540 ms`, range `7.766425..9.110740 ms`.
- Decision: adjacent median `-1.766082%`; 5% non-regression gate 통과. GC counter는 unavailable이므로 allocation 비악화를 별도 합격으로 주장하지 않는다.

#### Redesigned S1-C

- 첫 concrete-array redesign `ddacf8586`은 구조·의미 gate를 통과했지만 B2 median `6.571880 ms` 대비 C median `6.993560 ms`, `+6.416429%`로 시간 gate를 실패해 `1767f1f80`에서 제거했다.
- 최종 C2 설계: provider constructor에서 `None`/`Unit`/`Box`용 concrete `IEntityLogicFactory[]`를 한 번 만든다. Build는 known type을 inline switch로 선택하며 후보가 없는 Wall/`None`은 `EntityLogicCreationContext` construction 전에 빠져나간다. unknown enum은 full registration array, unscoped Factory는 모든 known array, glide resolver는 원래 read-only registration order를 사용한다.
- Tests-first red: compile-only prefilter interface 뒤 split Core fixture는 `4/4 failed`; split full fixture는 EditMode `6/6 failed`. full scan, default opt-in 부재, private resolver 부재에 대한 assertion red였고 build red는 아니었다.
- Focused green: split Core/Scenario/Infrastructure EditMode `6/0`; diagnostics EditMode `4/0`; existing provider Core `7/0`; final deterministic replay `59/0`.
- Structural counts: 3 entity x 5 Factory fixture의 opportunities `15`, probes `15 -> 7`, skips `0 -> 8`, 기본 `None` probe `4 -> 0`, unscoped Custom probe `3 -> 3`, cache construction `1/provider`, Build candidate allocation `0`. duplicate registration, same-ID type reuse, static/first phase ownership, legacy full-scan tick/hash/trace parity, glide 및 unknown fallback을 유지했다.
- B2 control evidence: `20260826T193032Z`, `20260826T193648Z`, `20260826T194232Z`; p95 `6.571880 / 7.106730 / 6.379365 ms`, median `6.571880 ms`, range `6.379365..7.106730 ms`.
- C2 evidence: `20260826T200124Z`, `20260826T200536Z`, `20260826T200711Z`; p95 `7.123190 / 5.678465 / 5.715655 ms`, median `5.715655 ms`, range `5.678465..7.123190 ms`.
- Tick sample preflight: B2/C2 여섯 admitted run 모두 sample frames `1200`, interval `1`, attempted/executed/count `1200/1200/1200`. C2 warm-up `20260826T195900Z`는 판정에서 제외했다. `20260826T200329Z`는 gameplay Tick count는 같았지만 runner identity failure로 판정 집합에서 제외하고 재수집했다.
- Historical decision, invalidated: 당시 adjacent median을 `-13.028616%`로 계산해 5% non-regression pass로 판정했다. 후속 감사에서 official C2 세 run 중 두 run의 actual resolution이 `1080x1080`임을 확인했으므로 이 triplet과 performance verdict는 formal acceptance에서 제외한다. GC counter는 unavailable이었다.

#### Historical closeout table (not one fixed five-state cohort)

| Decision pair | Baseline median | Candidate median | Delta | Verdict |
|---|---:|---:|---:|---|
| initial baseline -> S1-A | `7.348720 ms` | `7.188575 ms` | `-2.179223%` | historical pair pass |
| stabilized S1-A -> S1-B1 | `7.467520 ms` | `7.251195 ms` | `-2.896879%` | pair-specific pass |
| same-session S1-B1 -> redesigned S1-B2 | `8.019165 ms` | `7.877540 ms` | `-1.766082%` | supporting only; B2 provisional |
| same-session redesigned S1-B2 -> C2 | `6.571880 ms` | `5.715655 ms` | `-13.028616%` | invalid resolution |
| initial baseline -> final C2 arithmetic | `7.348720 ms` | `5.715655 ms` | `-22.222441%` | invalid final evidence |

- 각 adjacent 판정은 해당 시점의 서로 다른 control/candidate triplet을 사용했으므로 원래 요구한 하나의 15-run cohort가 아니다. 마지막 initial-to-final 값은 서로 다른 시간대의 절대값 산술 비교이고 C2 resolution도 불일치하므로 formal acceptance로 사용하지 않는다.
- Final core on `29d26ab18`: commit hook EditMode `225/0`, PlayMode `111/0`.
- Final replay: `./run_tests.sh --integration-replay --filter TickReplayDeterminismTests` EditMode `59/0`.
- Broad replay audit: unfiltered replay EditMode `141 total / 1 failed`; `EnemyProfileContractReplayTests.Replay_MigratedSummonProfile_SummonedMetadataAndPlacementRemainDeterministic`의 기존 summon placement expectation이며 untouched B2 `4498148ad`에서도 동일하게 재현됐다. touched-cluster regression으로 분류하지 않는다.
- Storage/evidence: `j2m-worktree-audit` PASS. 성능 비교 worktree는 `/mnt/d/J2M/worktrees/slice1-redesign-perf`, evidence는 `/mnt/d/J2M/evidence/gameplay-performance`, builds는 `/mnt/d/J2M/builds/gameplay-performance`에 있다. worktree별 private `Library`를 사용했다.
- Final structural state: Builder enumeration `1`, defensive FinalEntities copy `0`, owned wrapper `1`, trusted share `1`; 기본 Wall/`None` probe `0`; unscoped/unknown compatibility 및 authoritative Wall/Solid occupancy는 유지된다.
- Not-run/limits: broad unfiltered `full`은 문서상 baseline red이므로 실행하지 않았다. Scene/Prefab/ScriptableObject 변경과 manual/editor asset validation 대상은 없다. release-like GC allocation counter가 unavailable해 sustained allocation 비악화는 미검증이며 구조 계측과 source guard가 primary evidence다.
- Historical Goal status, superseded: 당시 objective의 Builder copy `0`과 기본 Wall probe `0` 및 recorded gate가 닫혔다고 판단해 완료 처리했다. 2026-08-27 사후 감사로 performance acceptance가 재개방됐으며 현재 Goal은 완료가 아니다.

### 2026-08-27 — independent audit correction; Goal reopened

- Goal objective: 변경 없음. Builder copy `0`, 기본 Wall/`None` probe `0`, authoritative/replay/hash 계약 유지, 동일 조건 성능 비악화를 모두 요구한다.
- Current package: performance revalidation. Production runtime은 `provisional retain candidate`이고 final retain은 보류한다.
- Audit method: 요구사항, 성능 evidence, production 계약/테스트를 세 서브 에이전트가 독립 재검토한 뒤 상호 반론 검토했다.
- Blocking finding: C2 official run `20260826T200536Z`, `20260826T200711Z`는 requested `1920x1080`과 달리 actual `1080x1080`이다. 기존 C2 median `5.715655 ms`, `-13.028616%`, pass는 무효다.
- Protocol finding: closeout은 원래의 하나의 5-state/15-run cohort 대신 pair-specific control triplet을 사용했다. local B1 -> B2 `-1.766082%`는 supporting evidence지만 strict fixed chain의 B1 -> B2 `+8.637818%`를 대체하지 않는다.
- Runner finding: `validGpuSamples`는 phase별 JSON 검사가 아니라 전역 문자열 검색이며 actual resolution도 admission하지 않는다. corrected runner/external validator 없이 새 p95를 formal evidence로 사용하지 않는다.
- Structural/semantic state retained: Builder enumeration `1`, defensive copy `0`, owned wrapper `1`, trusted share `1`, 기본 Wall/`None` probe `0`; 감사한 production 경로에서 authoritative Wall/Solid occupancy 및 순서 계약의 차단 위반은 발견되지 않았다.
- Pending focused evidence: actual Build unknown fallback, observable legacy accepted-owner parity, glide fail-then-success/first-success ordering, diagnostics execution tests의 Integration stratification, A-only replay.
- Required campaign: baseline `5d338c54a`, A `2dc8e5c53`, retained B1 `bfe16e8dd`, redesigned B2 `4498148ad`, C2 `29d26ab18`을 한 번의 사전 고정 counterbalanced campaign에서 상태별 3회, 총 15 admitted run으로 재측정한다. B1 production runtime은 original B1 `79889c4b7`과 동일하다.
- Admission: requested/actual `1920x1080`, phase별 CPU/GPU `1200`, gameplay Tick `1200/1200/1200`, revision/manifest/clean diff, Unity/quality/hardware identity를 p95 확인 전에 자동 검증한다. state별 full-run warm-up은 정확히 1회만 사전 폐기한다.
- Open risks: C2는 현재 pass/reject 판정 불가다. B2도 원 protocol 기준 provisional이다. GC/allocation은 계속 미검증이며 broad unfiltered `full`은 실행되지 않았다.
- Next safe action: admission/runner와 focused proof gap을 먼저 보강하고, [사후 감사 복구 계획](./Gameplay-Wall-Tick-Cost-Optimization-Slice1-Post-Closeout-Audit-2026-08-27.md)의 5-state/15-run 순서로 재측정한다. 같은 cohort의 네 adjacent 및 final delta가 모두 `<= +5%`일 때만 새 closeout에서 Goal complete를 재판정한다.

### 2026-08-27 — recovery campaign complete; Goal complete

- Goal objective: Wall의 authoritative entity/Solid occupancy 의미를 바꾸지 않고 Builder 경로 `FinalEntities` defensive copy를 `0`, 기본 Factory의 Wall/`None` probe를 `0`으로 줄이며 general-copy, 결과 lifetime, 순서, hash, trace, replay 계약을 유지한다.
- Working revision / branch: 주 작업트리 HEAD `e1b8238` / `codex/third-party-license-inventory`; 측정 runtime은 immutable plan의 baseline `5d338c54a`, A `2dc8e5c53`, B1 `bfe16e8dd`, B2 `4498148ad`, C2 `29d26ab18`이다. commit/push는 수행하지 않았다.
- Pre-existing changes preserved: recovery 준비 allowlist의 Goal Plan, README, Post-Closeout Audit, Recovery Goal Prompt 변경을 보존했고, 관련 없는 user change를 stage/revert하지 않았다.
- Admission tests-first: 초기 validator fixture `12 total / 10 failed / 2 error` assertion red에서 시작했다. 외부 validator, immutable campaign plan/index, retry 연속성, artifact hash 불변성, median-of-three aggregation regression을 구현한 뒤 Python suite `19/19` green, `py_compile`, `bash -n`을 통과했다.
- Automation hardening: current HEAD Player probe는 requested/actual resolution이 실제로 일치한 뒤 측정을 시작하며, `run_tests.sh gameplay-performance`는 두 phase 각각 CPU/GPU/sample `1200`, gameplay Tick `1200/1200/1200`, finite positive p95를 구조적으로 검사한다. historical SHA에는 이 patch를 섞지 않았고 external validator를 authoritative admission으로 사용했다.
- Focused proof closure: 실제 `Build` unknown branch의 full-registration fallback, legacy observable accepted-owner/order parity, glide fail-then-success 및 first-success ordering을 직접 고정했다. legacy-owner/glide Core proof는 재감사 후 composition root helper 대신 raw `WorldSnapshot`을 사용하도록 교정해 pure deterministic Core로 유지했다. `TickPipeline.RunTick` diagnostics 실행 테스트는 Unit에서 Scenario로 이동했다.
- Durable focused evidence: 8-fixture 명령 `TEST_RESULTS_ROOT=/mnt/d/J2M/evidence/slice1-recovery-closeout-20260827T113317Z/focused-editmode ./run_tests.sh full --filter 'SnapshotEntityLogicProviderPrefilterCoreTests;SnapshotEntityLogicProviderPrefilterSimulationTests;SnapshotEntityLogicProviderPrefilterInfrastructureTests;TickWorkDiagnosticsTests;TickWorkDiagnosticsSimulationTests;TickResultOwnershipCoreTests;TickResultOwnershipSimulationTests;TickResultOwnershipInfrastructureTests'`는 EditMode `20 total / 20 passed / 0 failed / 0 skipped`였다. six-fixture 명령은 별도 `focused-playmode` root에서 PlayMode `101 total / 97 passed / 0 failed / 4 skipped`였고 topology runtime gate test도 `Passed`였다. 두 root를 분리해 후속 0-match stage가 앞선 XML을 덮지 않게 했다.
- Functional validation: 교정 후 고유 `core` root에서 다시 실행한 `./run_tests.sh core`는 EditMode `228 total / 228 passed / 0 failed / 0 skipped`, PlayMode `111 total / 107 passed / 0 failed / 4 skipped`였다. current replay는 `59/59`, pinned A-only `2dc8e5c53` replay도 `59/59`였다. 테스트 중 드러난 topology PlayMode order dependency는 실제 destination topology와 동기식 presentation 계약에 맞춰 수정한 후 최종 fixture group이 green이다.
- Structural counters: Builder ordered enumeration `1`, Builder defensive FinalEntities copy `0`, owned read-only wrapper creation `1`, trusted backing share `1`, 기본 provider Wall/`None` probe `0`. authoritative Wall/Solid occupancy, unscoped/unknown fallback, entity-major/Factory order, static/first-owner 및 previous-result lifetime 계약은 유지된다.
- Campaign freeze: campaign ID `slice1-recovery-20260827T084937Z`; plan SHA-256 `f5bf38ca65deb29d5403d67e01d8c257be6bd0699edaee38ec6d38a0f51d5f3b`; validator SHA-256 `0e9947a9780f49a2513684b175c2db50064408805e23e20d97bffa510b7ac813`; campaign tool SHA-256 `be90eaf2a04f1982f3ac1db077ebf365aa1bec64c82d37453d737491016d4f31`. 종료 시 해시는 동결 값과 같았다.
- Admission result: 계획된 warm-up `5/5`, official `15/15`가 admitted 되었고 index issues는 `0`이다. 총 `29` attempt 중 `20` admitted, `9` rejected였다. rejected는 GPU sample `1198/1199` 네 건과 missing/interrupted runtime artifact 다섯 건이며 모두 같은 slot의 연속 attempt로 보존했다. admitted artifact의 metrics/manifest hash는 사후 검증에서도 일치했다.

| State | Official raw Tick p95 (ms) | Median (ms) | Range (ms) |
|---|---|---:|---:|
| baseline | `6.038355 / 6.152735 / 6.268645` | `6.152735` | `6.038355..6.268645` |
| A | `6.238940 / 6.072130 / 9.177435` | `6.238940` | `6.072130..9.177435` |
| B1 | `6.359005 / 5.938340 / 6.074515` | `6.074515` | `5.938340..6.359005` |
| B2 | `5.917215 / 6.087410 / 6.057070` | `6.057070` | `5.917215..6.087410` |
| C2 | `5.944025 / 6.070845 / 6.039985` | `6.039985` | `5.944025..6.070845` |

| Fixed-cohort gate | Delta | Verdict |
|---|---:|---|
| baseline -> A | `+1.401084%` | pass |
| A -> B1 | `-2.635464%` | pass |
| B1 -> B2 | `-0.287183%` | pass |
| B2 -> C2 | `-0.282067%` | pass |
| baseline -> C2 | `-1.832518%` | pass |

- Performance interpretation: 사전 고정한 raw-double median-of-three 규칙에서 다섯 gate가 모두 `<= +5%`다. A의 세 번째 값 `9.177435 ms`와 범위 겹침을 그대로 보존하므로 speedup은 주장하지 않으며, fixed-cohort 시간 비악화만 판정한다. `gcAllocationVerdict`는 계측 부재로 `UNVERIFIED`다.
- Evidence: campaign root `/mnt/d/J2M/evidence/gameplay-performance/slice1-recovery-20260827T084937Z`; immutable plan `campaign-plan.json`; verified index `campaign-index.json`; raw aggregation `aggregate.json`; captures/admissions는 같은 root 아래에 있다. 보존된 closeout validation은 `/mnt/d/J2M/evidence/slice1-recovery-closeout-20260827T113317Z/{focused-editmode,focused-playmode,core}`에 있고 각 root가 자체 XML/log/font-mutation evidence를 가진다. build output은 `/mnt/d/J2M/builds/gameplay-performance/slice1-recovery-20260827T084937Z`에 있다. `j2m-worktree-audit`는 PASS였고 `/mnt/d/J2M/worktrees/slice1-redesign-perf`의 private `Library`를 사용했다.
- Not run / limits: broad unfiltered `full`은 문서화된 red baseline과 scoped recovery 요구 때문에 실행하지 않았다. Scene/Prefab/ScriptableObject/asset 변경이 없어 manual/editor asset validation 대상은 없다. GC/allocation 비악화는 미검증이다.
- Final decision: S1-A, S1-B1, redesigned S1-B2, C2를 `retain`한다. 구조·의미·focused/core/replay와 고정 cohort의 다섯 시간 gate가 닫혔으므로 Slice 1 Goal을 다시 `complete`로 판정한다. 후속 대형 최적화는 별도 Goal로 시작한다.
