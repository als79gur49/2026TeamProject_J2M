# EnemyLogic 행동별 실행 책임 추출 계획

## 1. 상태와 결정

- 작성일: 2026-09-25 KST.
- 상태: **Summon 구현·동등성 검증 수행, 엄격한 core 전체 선택 목록 gate 미완료**. 실제 결과와 미완료 사유는 [Summon 추출 결과](./EnemyLogic-Summon-Extraction-Closeout.md)에 기록했다.
- 소스 검토 기준: `2f07eb44078d3d66ef0c5eb1be9781ff186d59d7`, `worktree/ui-audio-m1-continuation`.
- 이 계획의 초안 작성 시작 시 working tree는 clean이었다. 이후 동일 HEAD의 원래 production으로 baseline을 확보하고 candidate를 검증했다.
- 선택: 기존 프로필 조합을 유지하면서 행동별 실행 클래스를 추출한다. 첫 실행 범위는 **Summon 하나**다.
- 독립 resolver/interface/result 타입의 사전 파일 이동과 `partial` 분리는 선행 단계에 포함하지 않는다. 동일한 증분 작업·커밋·검증 조건에서 소환의 의존성이나 실행 순서 위험을 줄인다는 근거가 없었다.
- 아래 실행 단계는 계획 당시의 절차다. 실제 완료/미완료 판정은 §10과 결과 문서를 따른다.

실행 방식은 **현재 계약 확인 → 변경 전 동작 고정 → 소환 실행기 추출 → 동작·구조를 각각 검증**으로 정한다. 기존 동작 보존은 Green → Refactor → Green을 기본으로 하고, 새 실행기 경계의 의미 있는 계약에는 필요한 범위의 TDD를 적용한다.

## 2. 근거와 현재 구조

우선 읽을 문서:

- [Tick simulation canonical spec](./Tick-Simulation-Canonical-Spec.md)
- [Summon ownership final closeout](./Enemy-AI-Summon-Behavior-Ownership-Final-Closeout.md)
- [Summon replay/export compatibility](./Enemy-AI-Summon-Replay-Export-Vocabulary-C2-Readiness.md)
- [Gameplay test automation guide](../Testing/Gameplay-Test-Automation-Guide.md)
- [Git commit rules](../../AI_GIT_COMMIT_RULES.md)

현재 Summon ownership은 final closeout과 실제 구현을 따른다. 이전 Phase 1 문서의 “Charge만 BehaviorModule”, “Summon은 Utility” 설명을 현재 구현에 다시 적용하지 않는다. 현재 behavior key는 Charge, Summon, Glide다.

소스 경로는 모두 `Assets/_Features/Gameplay` 기준이다. 아래 줄 번호는 검토 SHA의 위치이며, 구현 시에는 심볼로 다시 찾는다.

| 소스 | 현재 책임 및 검토 지점 |
| --- | --- |
| `Gameplay_EnemyAI/Runtime/EnemyLogic.cs` | 파일 4,283줄, `EnemyLogic` 본체 약 3,160줄. Summon 억제·시작 예측은 763–826, 상태 진행·취소·trigger·보조 함수는 1328–1649에 위치 |
| `Gameplay_EnemyAI/Runtime/EnemyAiRuntimeTypes.cs` | `EnemySummonBehaviorRuntime`은 불변 실행 설정, `EnemySummonBehaviorRuntimeState`는 별도 가변 상태 carrier |
| `Gameplay_EnemyAI/Runtime/EnemyEntityLogicFactory.cs` | 프로필의 runtime definition을 사용하는 공통 Logic 생성 |
| `Gameplay_Entities/Runtime/SnapshotEntityLogicProvider.cs` | 엔티티별 동일 단계의 중복 Logic 소유권 검사 |
| `Gameplay_Loop/Runtime/TickPipeline.cs` | Tick마다 Logic 집합 구성, pre-movement batch 기록·projection·후속 resolve 조정 |
| `Gameplay_BoardState/Runtime/IWorldWriteContext.cs` | 상태 반영을 위한 기존 commit interface |

소환 실행의 주된 입력은 entity ID, 불변 소환 runtime, `WorldSnapshot`, `EntityState`, Tick 입력, commit context다. 같은 파일의 resolver/transition 타입을 옮겨야 추출할 수 있는 의존 관계는 없다.

## 3. 목표 구조와 범위

| 항목 | 현재 | 첫 slice 완료 상태 |
| --- | --- | --- |
| 소환 정책과 상태 진행 | `EnemyLogic` 여러 메서드 | `EnemySummonExecutor` 한 책임으로 응집 |
| Tick 진입과 행동 간 순서 | `EnemyLogic` | 같은 진입점과 순서에서 실행기에 위임 |
| 가변 소환 상태 | `WorldState` / snapshot carrier | 동일한 저장소·조회 경계·schema |
| 프로필 authoring | Core / Brain / Capabilities / Behaviors | 동일한 에셋·compiler·typed slot |
| 상태 반영 | commit context / batch / projected world | 기존 경로 및 관측 시점 |
| 요청 후 배치·생성 | 후속 resolve / materializer | 동일한 입력·배치·ID 할당·실패 처리 |
| 표현 | Tick 결과 기반 신호·오디오·VFX | 같은 신호와 순서 |

`EnemySummonExecutor`는 **제안명인 internal static class**로 시작한다. 기존 `EnemyLogic`의 `_summonBehavior` 설정 참조를 사용하고 메서드에 entity ID와 runtime을 명시한다. 클래스 수준의 가변 상태, 엔티티별 캐시, 새 per-Tick 실행기 객체를 만들지 않는다. 함수 지역 변수와 기존 작업 버퍼는 허용하며 할당 최적화는 별도 작업으로 둔다.

초기 외부 호출면은 다음 네 책임으로 제한한다. 최종 이름·인자 순서는 코드 작성 시 조정할 수 있지만 책임은 유지한다.

| 제안 API | 역할 | 입력·출력 경계 |
| --- | --- | --- |
| `Commit` | 초기화, 정상 진행, 유효성에 따른 중단·취소, trigger 기록 | snapshot, Tick, source, entity ID, runtime, 기존 context, update log |
| `Cancel` | source lookup 실패 시 이미 존재하는 상태 취소 | existing state, entity ID, runtime, context, update log. 유효한 source를 요구하지 않음 |
| `ShouldSuppressActive` | 현재 상태의 이동·방향 억제 판단 | snapshot/source/entity ID/runtime/Tick 입력, bool 반환, null runtime이면 false, write 없음 |
| `ShouldSuppressImminent` | 이번 Tick의 시작 예측과 최대 자식 수 조건에 따른 억제 판단 | 기존 snapshot/source/entity ID/runtime 입력, bool 반환, null runtime이면 false, write 없음 |

기존 두 억제 질의의 OR 위치와 short-circuit 순서를 유지한다. 현재 OR 호출부는 소환 runtime의 null 여부를 검사하지 않으므로 두 query 내부의 null → false 계약을 보존한다. 상태 조회의 기존 `_entityId`와 child-limit 조회의 `source.entityId` 사용도 그대로 보존한다. 두 질의 API는 writer, update log, trigger sink를 입력으로 받지 않는다. `EnemyLogic` 전체 객체나 모든 gameplay 서비스를 가진 새 context를 전달하지 않는다. 실행기에서 `EnemyLogic`를 역참조하지 않는다. 기존 commit context와 `IEnemyUtilityTriggerSink`를 재사용하고 첫 slice에서 범용 executor 인터페이스나 새 scheduler를 만들지 않는다. 기존 coordinator의 argument validation과 commit/cancel 호출부의 모듈-null guard도 유지한다.

현재 범위 밖: resolver 사전 이동, 다른 행동 추출, 공통 FSM/Behavior Tree, 범용 module registry, Capability/Behavior authoring 통합, enum/schema 변경, 밸런스 조정, Scene/Prefab/ScriptableObject migration, replay 명칭 변경, TickPipeline 재설계. `EnemyAiProfileCompiler`, `EnemyAiRuntimeTypes`, `WorldState`, 기존 소환 resolver와 `EntitySpawnMaterializer`의 production 변경도 첫 slice에 포함하지 않는다.

## 4. 반드시 보존할 계약과 현재 정책

`StrongContract`는 canonical 문서·공개 경계·결정성·기존 계약 테스트가 보호하는 사항이다. `CurrentPolicy`는 구현에서 확인되는 정책으로 이번 동작 보존 리팩터링에서 유지하지만 영구 제품 규칙으로 새로 승격하지 않는다. 분류나 기대 결과가 불명확하면 해당 항목의 근거를 먼저 확인한다.

| ID | 분류 | 보존 사항 |
| --- | --- | --- |
| C01 | StrongContract | authoritative state는 `WorldState`, 조회는 `WorldSnapshot`, 변경은 기존 commit/batch 경로로 전달 |
| C02 | StrongContract | 각 진입점이 읽는 snapshot과 batch 적용 시점 유지. 앞 실행기의 기록을 뒤 실행기가 즉시 읽도록 변경하지 않음 |
| C03 | StrongContract | entity 및 소환 요청 처리 순서, 자식 ID·metadata, `SourceEffectIndex = 0` / `Effect=0` 호환 유지 |
| C04 | StrongContract | 소환 배치는 `SurfaceCell(face,x,y)`와 기존 placement/resolve 경계 유지. 기존 resolver가 post-attack snapshot의 유효 source 위치·방향·team을 사용하며 이 책임을 실행기로 이동하지 않음 |
| C05 | StrongContract | 소환 trigger 뒤 source 무효화·배치 실패는 기존 후속 경로가 처리. 실행기는 직접 spawn/ID 할당을 하지 않음 |
| C06 | StrongContract | 표현 신호와 이벤트는 Tick 결과로 전달. 실행기는 View/audio/VFX를 직접 호출하지 않음 |
| C07 | CurrentPolicy | Recover 종료 Tick은 상태를 clear한 뒤 return. 같은 호출에서 cooldown 감소나 새 Windup 시작을 추가하지 않음 |
| C08 | CurrentPolicy | Recovery 0 또는 Recover 중에도 Windup 억제의 inclusive 종료 Tick 잔여가 존재할 수 있음 |
| C09 | CurrentPolicy | 기존 상태가 있고 hard-invalid가 아닌 source의 topology 중단은 cooldown을 동결하고 유효한 종료 Tick·억제 종료 Tick을 한 Tick 이동. start Tick과 activation sequence 보존 |
| C10 | CurrentPolicy | 취소할 active phase/잔여 억제가 없으면 cancellation은 no-op. 무효화마다 무조건 cooldown reset하지 않음 |
| C11 | CurrentPolicy | context가 `IEnemyUtilityTriggerSink`가 아니면 emit은 return하지만 상위 상태 진행·로그는 계속되는 기존 동작 유지 |
| C12 | CurrentPolicy | 배치 실패 후 기존 Recover/쿨다운 진행 유지. 기대값을 바꿔 다른 재시도 정책으로 전환하지 않음 |
| C13 | CurrentPolicy | 소환 상태가 없고 source가 controllable하지 않으면 초기화·로그·write·trigger 없이 return. 기존 phase None 상태의 cancellation no-op와 구분 |
| C14 | StrongContract | 이동·방향 억제는 pending blocked reaction 소비 여부도 통제. 기존 snapshot에서 판단하며 소비·만료 처리, locomotion cooldown과 이벤트 순서 보존 |

기존 update/event 문자열·분류와 activation sequence의 증가 시점도 이 slice에서 보존한다. 이름 정리나 로그 축약은 의미 변경 여부를 별도로 검토한다.

호출부 검토표:

| 위치 | 현재 처리 | 추출 후 확인 |
| --- | --- | --- |
| `CommitPreMovementState`: enemy lookup 실패 | Utility 취소 뒤 저장된 Summon 상태가 있으면 취소, return | source 없이 취소 가능; 기존 guard와 순서 유지 |
| 같은 함수: topology 비참여 | Jump suspend → Utility → Summon → return | 공통 dispatcher의 호출 순서와 실행기 내부 hard-invalid 우선순위 유지 |
| 같은 함수: 정상 진입 | patrol init → Jump → Charge → Glide → Utility → Summon → controllable 재확인 → pending blocked reaction 준비 → attack/locomotion cooldown 처리 | Summon의 위치, 후속 소비 판단, 입력 snapshot 유지 |
| `ShouldSuppressAutonomousMovementAndFacing` | 기존 조건들의 OR 중 Summon active → imminent 순서 | 두 질의의 순서·순수성·최대 자식 수 판정 유지. 아래 세 consumer 모두 확인 |
| `CollectMovementIntents` | 억제 시 일반 이동 의도 생성을 생략 | 기존 pre-movement 적용 후 snapshot에서 판단 |
| `ResolvePatrolFacing` | 억제 시 자율 patrol facing 변경을 억제 | AI transition 시점의 snapshot과 판단 순서 유지 |
| `TryPreparePendingEnemyBlockedReaction` | 선행 유효성 검사 뒤 억제 상태면 소비 보류. 소비 시 cooldown 0, pending clear, 관련 facing/chase 처리 | pre-movement batch 기록 직후에도 전달받은 기존 snapshot을 사용. 만료·불일치 등 앞선 clear 조건과 구분 |
| `EmitEnemySummonBehaviorTriggerIntent` | 공유 trigger sink에 source metadata와 config 전달 | sink 판정·내용·횟수·기록 순서 유지 |

## 5. 테스트 설계: 현재 동작과 목표 구조

### 5.1 기존 테스트를 우선 재사용

| 영역 | 기존 fixture / test | 실행 범위와 한계 |
| --- | --- | --- |
| 초기 지연·Windup·Recover·쿨다운 | `EnemyAiScenarioTests.BehaviorSummon_InitialDelayCooldownWindupRecoveryParity` | Extended, 명시적 fixture Tick assertion |
| 이동·방향 억제 | `BehaviorSummon_MovementSuppressionParity`, `BehaviorSummon_WindupDefaultPolicy_DoesNotImplicitlySuppressMovement` | Extended. 전자는 시작/hold 4 Tick을 검사하며 해제 Tick은 추가 확인 대상 |
| 다중 source·생성 순서 | `BehaviorSummon_EmitsSpawnRequestInUtilityParityOrder`, `BehaviorSummon_SameTickMultiSummoner*` | Extended, metadata·표현·audio/VFX 순서 |
| topology·취소·최대 자식 | `BehaviorSummon_SourceLeavesTopologyCancelsOrSuspendsAsUtility`, `BehaviorSummon_MaxAliveParity` | 기존 topology는 Windup 중심, max-alive는 Stationary fixture |
| production profile·배치·죽음 | `MigratedSummonRuntimeContractTests` | Extended. live profile 사용, fixture와 콘텐츠 identity 기록 |
| 소환된 자식 | `KaliSummonedUnitRuntimeContractTests` | 생성 이후 runtime 연결의 인접 회귀 검사 |
| 결정성과 고정 결과 | `EnemyProfileContractReplayTests` | fixture에 Core/Extended 혼재. migrated summon은 Core, 동일 구현 2회 실행과 일부 기대값 비교 |
| compile/조합·profile | `EnemyAiRuntimeDefinitionGuardTests`, `EnemyAiProfileAssetContractTests` | 기존 guard 유지. source/asset 변경이 없는 slice에서 새로운 튜닝 수치 고정은 추가하지 않음 |
| 막힘 반응 | `EnemyKinematicContinuationBlockedReactionCoreTests` | 기존 Core fixture에는 Summon 조합이 없음. S09의 소환과 결합한 경계는 별도 coverage 확인 |

### 5.2 추출 전 보강할 경계 후보

아래 후보별로 기존 assertion을 대조하여 중복이 없을 때 추가한다. 실행 기록에는 아래 세부 case ID별로 입력, 관측 Tick/호출, 기대 경계, 실제 assertion/test 이름, baseline/candidate capture 경로를 매핑한다. 한 행에 test 이름 하나를 적는 것만으로 여러 변형을 완료 처리하지 않는다. 테스트 fixture가 설정한 값은 exact assertion을 사용하되 production tuning 값을 새 영구 golden으로 잠그지 않는다.

| ID | 시나리오 | 확인할 결과 |
| --- | --- | --- |
| S01 | initial delay 2 / cooldown 3 / windup 1 / recovery 2 | 상태와 activation sequence, 시작·종료 Tick, 생성 횟수, cooldown 경계 |
| S02 | Windup/Recover 억제 옵션 조합 및 Recovery 0 | 시작 예정·실행·Recover 종료·첫 해제 Tick의 이동 의도와 facing, inclusive 억제 종료값 |
| S03 | 이동 가능한 적이 최대 자식 수에 도달 | Windup 미시작, 잘못된 imminent 억제 없음, 이동·방향 결과 |
| S04 | Windup 및 Recover 중 topology 이탈·재개 | 종료 Tick 연장, cooldown 동결, start/sequence 유지, 정상 재개 |
| S05 | lookup 실패·Detached·HP 0·marked-for-death·Dead | 취소 조건/no-op 구분, 상태/이벤트/요청 횟수. lookup 실패 경로는 필요 시 직접 Logic 진입점으로 검사 |
| S06 | 같은 profile의 여러 source, 입력 엔티티 순서 변경 | source별 상태 격리와 canonical 요청·자식 ID·이벤트 순서 |
| S07 | 실행 Tick 이동·회전, 배치 차단, trigger 후 source 무효화 | 확정된 pose에서 배치, ghost 없음, 기존 회복 정책, 취소/skip 표현 |
| S08 | 같은 snapshot에서 억제 질의 반복 및 trigger sink 부재 | 질의는 state/요청을 변경하지 않음; sink 부재의 기존 상태 진행과 로그 유지 |
| S09 | 유효 pending blocked reaction과 소환 억제의 결합 | imminent/active/해제 Tick의 pending 유지·소비, locomotion cooldown, facing/chase 결과와 소비 이벤트 |
| S10 | Summon runtime이 없는 profile | 두 query의 false 계약, 기존 일반 이동·순찰 경로, 소환 state/trigger 미생성 |

P1에서 펼칠 최소 case 목록:

- S01: 위에 명시한 timing fixture의 전체 경계. 기존 test 재사용 여부를 기록한다.
- S02a–d: Windup/Recover 억제 `(true,true)`, `(true,false)`, `(false,true)`, `(false,false)`와 양수 Recovery. S02e–h: 같은 네 조합과 Recovery 0. 각 case에서 시작 예정, 실행, 존재하는 Recover 경계, 첫 해제 시점을 명시한다.
- S03: 이동 가능한 max-alive 차단 상태와 한도가 해제된 후의 시작을 구분한다.
- S04a/b: 기존 Windup/Recover 상태의 이탈·재개. S04c: state가 없는 topology 비참여 source에서 무출력·미초기화를 확인한다.
- S05: lookup 실패, Detached, HP 0, marked-for-death, Dead 각각을 식별한다. 각 무효 경로에서 state 부재, 기존 Windup/Recover 취소, 기존 None+잔여 억제 0의 no-op를 구분한다. public provider가 해당 경로를 생성하지 않으면 기존 Logic/context seam을 사용한다.
- S06: 서로 다른 현재 state를 가진 두 source가 같은 runtime을 사용하도록 하고 입력 엔티티 순서의 두 변형을 비교한다.
- S07a/b/c: 실행 Tick 이동·회전, 배치 차단, trigger 이후 source 무효화를 각각 매핑한다.
- S08a/b: 같은 snapshot에서 public Logic 경로를 반복 호출하는 무부작용 검사와 sink 부재의 직접 context 검사를 분리한다. baseline에는 새 executor API를 요구하지 않는다.
- S09a–c: imminent, active, 첫 해제 Tick을 Patrol/Chase 각각에 적용한다. 충분한 reaction expiry와 일치하는 mode/source, settled pose, active attack 없음 등 앞선 guard를 통과하는 조건을 명시하여 소환 억제 효과를 격리한다. pending state와 cooldown을 함께 관측한다.
- S10: 기존 비소환 profile 테스트를 우선 재사용하여 null runtime 경로를 매핑한다.

도달 불가능하거나 fixture 조건상 해당하지 않는 조합은 코드 근거와 제외 이유를 기록한다. 필요한 case를 생략한 채 상위 S 행만 완료로 표시하지 않는다.

현재 코드와 다른 기대가 나오면 production 추출을 시작하기 전에 원인을 분류한다. 기존 결함이면 별도 bugfix와 근거를 분리하고, 리팩터링의 기대값을 임의 변경하지 않는다.

### 5.3 Characterization, TDD, 구조 검증의 역할

1. 기존 public Logic / pipeline seam에서 S01–S10의 관측 결과를 고정한다. 새 characterization은 기존 구현에서 green이어야 한다.
2. 필요한 테스트에 한해 assertion 검출력을 확인한다. 예: imminent 억제를 빠뜨리는 임시 변이를 해당 test가 검출하는지 확인한다. mutation diff·실패 이유·복구 후 green을 기록하며 전체 mutation framework는 만들지 않는다.
3. 새 실행기 seam의 상태 진행, 반복 질의의 무부작용, source 격리 등 의미 있는 계약에는 tests-first를 적용한다. 새 계약의 구현이나 별도 결함 수정에 Red → Green을 사용한다. 이미 정상인 로직을 옮기는 작업에 인위적인 stub/red 단계를 추가하지 않는다. 새 타입이 없어서 난 compile failure는 행동 assertion-red 증거로 취급하지 않는다.
4. 직접 실행기 테스트가 통과해도 실제 `EnemyLogic` 위임을 통과하는 기존 integration 테스트를 유지한다. test 전용 정상 경로만 검사하는 상황을 방지한다.
5. 구조 완료는 책임 이동과 의존성 검토로 확인한다. `EnemyLogic`에 소환 phase/timing 판단의 복제본이 남지 않고, 실행기에 지속 가변 상태·직접 world write·presentation 호출이 없어야 한다. 줄 수나 private 메서드 이름만으로 성공을 판정하지 않는다.

테스트 계층은 운영 가이드를 따른다. snapshot/context를 실행하는 조합 테스트는 Integration, reflection/source 경계 검사는 Infrastructure, 순수 결정적 입력/출력 검사는 Core 배치 대상으로 분리한다. 실행 tier의 Core 승격은 별도 정책을 따르며 모든 신규 테스트를 일괄 Core로 분류하지 않는다.

## 6. 변경 전후 비교의 기준 결과 확보

기존 `EnemyProfileContractReplayTests`의 2회 실행 비교는 같은 구현의 결정성 검증이다. 변경 전후 동등성 증거로 단독 사용하지 않는다. 기존 dump에는 `movementSuppressionUntilTickInclusive`와 presentation 전체가 포함되지 않으므로 그대로 복사하여 충분한 oracle이라고 선언하지 않는다.

절차:

1. production 소스가 기준 상태인 동안 test 전용 capture helper와 S01–S10 fixture를 준비한다. helper는 두 버전에 공통인 기존 Logic/pipeline을 실행하고 관측값만 직렬화한다. 소환 알고리즘의 별도 복사본을 만들지 않는다.
2. capture helper·입력·설정·정규화 규칙을 검토하고 baseline runtime에서 실행한다. 가능한 경우 테스트 전용 변경을 독립 리비전으로 고정한다. 미커밋이면 HEAD뿐 아니라 patch 및 신규 파일 내용 hash까지 기록한다.
3. baseline 출력과 manifest를 동결한 후 production 추출을 시작한다. candidate에서도 같은 공통 helper/fixture/config를 사용한다. helper 수정이 필요하면 **원래 baseline production 소스**에 공통 test/helper patch만 적용하여 baseline을 다시 확보한 뒤 비교한다. 새 executor를 참조하는 candidate 전용 test는 baseline에 가져오지 않는다. candidate runtime의 출력으로 baseline을 재생성하지 않는다. 재확보할 때에도 기존 artifact는 보존하고 새 디렉터리를 사용한다.
4. candidate 결과를 별도 경로에 기록하고 비교한다. baseline 자동 덮어쓰기나 candidate 결과를 새 기대값으로 승인하는 경로를 만들지 않는다.

Tick별 최소 capture:

- entity ID, face 포함 위치, facing, alive/board presence, AI mode.
- 모든 소환 state field: phase, cooldown, windup/recover start/end, activation sequence, `movementSuppressionUntilTickInclusive`.
- 소환 source의 이동 의도, 관측 가능한 trigger/spawn 요청과 자식 ID·archetype·source metadata.
- S09에서는 pending blocked reaction의 존재/전체 payload, locomotion cooldown, 소비·clear 이벤트 및 관련 facing/chase 결과.
- 순서를 보존한 이벤트와 소환 표현 신호/경고/visibility/binding. 기존 adapter가 제공하는 audio/VFX 결과는 관련 fixture assertion으로 보완.
- determinism hash 및 진단용 raw trace. hash만 일치한다고 전체 동등성을 판정하지 않음.

S05의 lookup 실패와 S08의 sink 부재 등 직접 Logic/context seam으로 검사하는 사례는 기록된 context write, trigger, update log 및 호출 전후 snapshot을 비교한다. 해당 seam에 없는 `TickResult`나 presentation을 인위적으로 만들지 않는다. 각 case의 비교 필드와 제외 이유를 manifest에 명시한다.

일반 데이터는 기존 ordered enumeration을 사용한다. 이벤트·요청을 사후 정렬하여 순서 차이를 숨기지 않는다. 환경 경로·wall clock 등 비결정적 메타데이터만 사전에 정한 규칙으로 분리한다. mismatch 보고에는 scenario ID, 최초 다른 Tick, field/sequence 위치, before/after를 남긴다.

값 비교에 앞서 **capture 완전성**을 검사한다. P1 manifest에 세부 case ID 집합, 각 case의 기대 Tick/호출 목록과 record 수, 관측 schema와 필수 field, 명시적 제외를 고정한다. 양측 각각에서 빈 파일, case/record 누락·중복·예상 밖 추가, schema 불일치, 필수 field 누락을 실패시킨 후 값·순서를 비교한다. state가 없다는 관측과 state field가 누락된 오류도 구분한다. 정상 no-op는 명시적인 record와 길이 0의 write/trigger 목록으로 표현한다. XML의 test 실행 여부와 capture의 완전성을 별도로 검증한다.

capture 구현은 테스트 코드에서 NUnit output 또는 명시적 exporter로 제한한다. Windows Unity에서 접근할 실제 출력 경로와 XML/log 포함 여부를 확인한다. WSL 전용 환경변수가 Unity에 자동 전달된다고 가정하지 않는다. 장기 회귀는 작은 의미 assertion으로 남기고 이번 상세 capture는 D-drive 증거로 보관한다.

## 7. 실행 단계와 완료 조건

| 단계 | 수행 | 종료 조건 |
| --- | --- | --- |
| P0 기준 확인 | status/diff/HEAD, source·test inventory, test filter 목록, runner 경로와 evidence root, 명령·시각·exit·staged/untracked 내용까지 수집할 manifest 준비 | 실제 revision/config와 검증 목록 및 증거 수집 절차 고정. 해당 cluster의 기존 실패가 있으면 분류 완료 |
| P1 현재 동작 고정 | 기존 core/공통 targeted 실행, S01–S10 세부 case 매핑·필요한 보강, 공통 capture helper 및 baseline 확보 | 공통 contract tests green, oracle identity·capture 완전성 확보, 설명되지 않은 실패 없음 |
| P2 소환 추출 | internal static 실행기와 `.meta`, 기존 4개 책임 위임, 소환 전용 helper/상수 이동, 경로 기반 검사 보완 | compile 및 focused tests green, 모든 호출부 연결, 소환 정책 중복 제거 |
| P3 동등성·경계 검증 | 동일 입력의 공통 capture 비교, 공통 회귀 세트·candidate 전용 계약/구조 세트와 core 실행 | S01–S10 capture 완전성 및 비교 차이 0, 목표 ownership 확인, required tests 실제 실행 |
| P4 기록·후속 판단 | 파일/테스트/결과/미실행 이유/남은 위험 기록, 구조 지도 갱신 | Summon 범위 완료 여부 명시. 다른 행동은 별도 inventory와 계획으로 진행 |

P2 내부에서는 소환 전용 코드의 이동과 호출부 치환을 한 review 단위로 유지한다. helper 공통화, query 병합, 알고리즘 개선, allocation 최적화를 함께 넣지 않는다. 현재 source-invalid, topology, normal, suppression 경로가 모두 연결되기 전에는 추출 완료로 보지 않는다.

예상 변경 파일:

- `Gameplay_EnemyAI/Runtime/EnemyLogic.cs`.
- 신규 `Gameplay_EnemyAI/Runtime/EnemySummonExecutor.cs`와 `.meta`.
- coverage 공백이 있는 기존/신규 소환 test 및 test 전용 capture helper와 `.meta`.
- `GameplayCameraShakeMixerFoundationArchitectureTests` 등 기존 파일 기반 guard의 검사 대상. 순찰 guard는 실제 이동 범위를 확인하여 coverage가 감소하는 경우만 보완.
- 이 계획과 구현 결과 문서, architecture index.

독립 변경 단위는 현재 동작 테스트/기준 확보와 production 추출·직접 계약 검증으로 나눈다. 구체 커밋은 `AI_GIT_COMMIT_RULES.md`와 commit-push-workflow에 따라 staged diff를 검토한 뒤 수행한다. 최초 계획·구현 요청에는 commit/push가 포함되지 않았으며, 이후 별도 커밋 요청에 따라 진행한다.

커밋 시 pre-commit hook도 `run_tests.sh core`를 자동 실행한다. §8 함수의 환경변수는 runner 한 호출에만 적용되므로 이후 `git commit`에 자동으로 남지 않는다. 실제 커밋 명령에도 별도의 새 D evidence root와 동일한 결과/log/capture/build root 환경을 전달하여 hook이 상속하도록 하고 명령·대상 source identity·결과를 기록한다. P0에서 실제 hooks path와 내용을 확인한다. hook 우회나 수정으로 처리하지 않는다.

## 8. 검증 명령과 증거 보관

모든 Unity lane은 검증할 worktree에서 `./run_tests.sh`로 실행한다. 같은 Unity 프로젝트의 lane은 순차 실행한다. 병렬 작업은 문서·소스 리뷰에 사용한다.

기존 C-drive worktree는 legacy 경로로 유지할 수 있다. 새 worktree가 필요하면 `j2m-worktree-add`로 `/mnt/d/J2M/worktrees` 아래 만들고 D 여유 30 GiB, resolved project path, private Library를 확인한다. storage compliance 검토에는 `j2m-worktree-audit`를 사용한다. C 여유가 10 GiB 미만이면 알린다.

아래 함수는 **이후 실행·경로 분리를 위한 예시**이며 완전한 evidence manifest 수집기는 아니다. P0에서 아래 설명의 파일 identity·실행 결과 수집까지 준비한 뒤 baseline 검증을 시작한다. 호출마다 새 evidence 디렉터리를 만든다. 이 검증에 사용하는 runner root 변수를 명시하여 기존 환경의 C-drive 출력 override가 적용되지 않게 한다. Player build가 필요해지는 경우 출력은 별도 D builds root로 보낸다.

```bash
(
set -e
mkdir -p /mnt/d/J2M/evidence/enemy-summon-extraction

run_enemy_check() {
    local check_stage="$1"
    local check_label="$2"
    shift 2
    local check_root
    check_root="$(mktemp -d "/mnt/d/J2M/evidence/enemy-summon-extraction/${check_stage}-${check_label}-XXXXXX")" || return
    git rev-parse HEAD > "$check_root/head.txt"
    git status --short --branch > "$check_root/status.txt"
    git diff --binary > "$check_root/working.patch"
    CODEX_VALIDATION_ROOT="$check_root" \
    TEST_RESULTS_ROOT="$check_root/test-results" \
    TEST_LOG_ROOT="$check_root/test-logs" \
    CAPTURE_ROOT="$check_root/captures" \
    PLAYER_BUILD_ROOT="/mnt/d/J2M/builds/enemy-summon-extraction/$(basename "$check_root")" \
    ./run_tests.sh "$@"
}

run_enemy_check baseline config --print-config
run_enemy_check baseline core core
run_enemy_check baseline targeted full --filter 'BehaviorSummon,MigratedSummonRuntimeContractTests,EnemyProfileContractReplayTests,KaliSummonedUnitRuntimeContractTests'
)
```

`working.patch`만으로 untracked/staged 내용까지 완전히 보존할 수는 없다. 실행 manifest에 전체 touched source/test/helper와 fixture input의 content hash, staged diff, 신규 파일 내용도 저장한다. candidate 실행에서는 stage를 `candidate`로 바꾼다. 각 명령의 exit code·시각·XML·선택된 test 이름/수·실패 이유를 manifest에 기록한다. 위 subshell 예시는 준비 또는 runner 실패 시 후속 호출을 중단한다. 이 반환값을 무시하여 다음 단계로 진행하지 말고 §9에 따라 분류한다.

검사 집합을 다음처럼 구분한다.

| 집합 | 내용 | 시점·동일성 조건 |
| --- | --- | --- |
| 공통 회귀·capture | 위 기존 fixture, S01–S10 characterization, 기존 public Logic/pipeline 기반 capture | P1 baseline과 P3 candidate에서 같은 공통 test/helper 소스·입력·case/선택 목록 사용 |
| candidate 전용 | 새 executor 직접 계약 테스트, 추출 후 새 구조를 검사하는 guard | P2/P3에서 필수 실행. baseline은 대상 API/구조 부재로 미실행 |
| core gate | 각 revision의 현재 core lane | P1/P3에서 실행. 신규 테스트로 전체 선택 목록이 달라질 수 있으므로 공통 목록의 동등성 조건과 구분 |

P2 반복 중에는 변경한 계약과 해당 소환 fixture만 실행하고, 구현 완료 시 공통·candidate 전용 required 세트와 core를 현재 candidate에 대해 실행한다. baseline과 candidate를 같은 리비전의 green 주장으로 합치지 않고 전후 비교 자료로 구분한다.

추가 required 검사:

- 새 characterization/capture test의 실제 fixture filter는 공통 목록에 추가하고 P1부터 실행한다. 새 executor 직접 test는 candidate 전용 목록에 추가한다.
- 경로 기반 guard 변경 시 `full --filter GameplayCameraShakeMixerFoundationArchitectureTests` 및 실제 수정한 순찰/구조 fixture를 candidate에서 실행한다. baseline에서는 존재하는 기존 guard를 실행하고, 새 파일을 요구하는 후보 guard는 이식하지 않는다.
- S09 관련 기존 `EnemyKinematicContinuationBlockedReactionCoreTests`는 core gate에서 실행하며, 새 Summon 결합 사례는 공통 characterization filter에 포함한다.
- compiler/profile/schema까지 변경해야 하는 새로운 근거가 생기면 scope를 재검토하고 해당 guard fixture를 추가한다.

`core --filter`는 category 범위를 넓히지 않는다. 상세 Summon 검증에는 `full --filter`를 사용한다. 여러 이름의 OR filter는 지원된다. 공통 집합의 baseline/candidate 선택 목록을 동일하게 유지하며 candidate 전용 집합은 별도 기록한다. XML에서 개별 필수 test의 실행 여부를 확인한다. 한 stage의 0 test는 다른 stage의 실제 match와 구분하며, 필요한 test가 0개이거나 skipped면 해당 요구는 미검증이다.

실행하지 않을 기본 범위: broad unfiltered full, UI lane, 별도 Player/성능/수동 시각 검증. 이유는 첫 slice가 소환 runtime 책임 추출이고 해당 presentation asset/UI/성능 계약을 변경하지 않기 때문이다. runtime 검증에서 해당 위험이 드러나면 필요한 범위만 추가한다. 문서상 full baseline red를 현재 변경의 회귀 근거로 사용하지 않는다.

## 9. 중단·실패 분류·복구

- baseline touched-cluster failure: extraction 이전 결과로 보존하고 원인·기존 정책·테스트 오류를 분류한다. 설명되지 않은 관련 실패 상태에서 production 추출을 시작하지 않는다.
- candidate의 새 failure 또는 before/after 차이: 해당 slice를 미완료로 두고 첫 차이의 입력·호출 순서·snapshot·state field부터 조사한다. 기대값 재생성으로 통과시키지 않는다.
- 환경/build/timeout/XML 미생성/미선택: assertion failure와 구분하고 검증 미완료로 기록한다.
- 새로운 gameplay policy 또는 state schema 변경 필요: 순수 추출 범위를 벗어나므로 설계와 검증 범위를 먼저 다시 정한다.
- 복구는 자기 변경의 해당 slice만 대상으로 한다. 사용자 diff와 기존 ahead commit을 보존하며 광범위 reset/clean을 사용하지 않는다. baseline artifact는 유지한다.
- 로그·상태·미해결 차이를 남기고 필요한 수정과 검증을 완료한다. 단순히 어렵거나 테스트가 오래 걸린다는 이유로 계약을 완화하지 않는다.

## 10. 완료 체크리스트

- [x] P0/P1 기준 SHA, source/test/config identity와 실행 결과 확보.
- [x] S01–S10의 모든 필수 세부 case가 입력·경계·assertion/test·양측 capture에 연결됨. 49-case [추적표](/mnt/d/J2M/evidence/enemy-summon-extraction/case-traceability-v3.json)에 record별 경로와 assertion 위치를 기록하고 제외 조합은 코드 근거를 기록.
- [x] baseline capture가 원래 production에서 확보되고 candidate와 같은 공통 harness/input을 사용함. 공통 변경 후 원래 source로 재확보.
- [x] `EnemyLogic`의 네 외부 호출 책임이 모두 실행기로 연결됨.
- [x] 소환 phase/timing/취소/예측 정책의 중복 구현 없음.
- [x] 지속 가변 상태·새 provider phase owner·직접 world write·presentation 호출 추가 없음. 기존 argument/null guard 보존, 실행기에서 coordinator 역참조 없음, 질의 API에 writer/log/sink 없음.
- [x] 양측 capture의 49 case·97 record/Tick·하위 직렬화 field·case별 source 존재 조건과 공통 선택 테스트 95개의 완전성 통과 후 선택한 관측 범위의 before/after 차이 0. 비교하지 못한 범위는 결과 문서에 기록.
- [ ] core, 공통 required targeted, candidate 전용 계약/구조 검사가 같은 candidate에서 실제 실행되어 통과. 공통 95개와 계약·구조 21개는 통과했으나 headless core PlayMode 4개 skip, 보충 그래픽 검증 1개 실패로 엄격한 전체 선택 목록 gate 미완료.
- [x] 새 `.meta`와 소스가 짝을 이루고 authoring/schema/에셋의 의도하지 않은 변경 없음.
- [x] 실행/미실행 테스트와 이유, evidence 경로, 남은 위험을 [구현 결과](./EnemyLogic-Summon-Extraction-Closeout.md)에 기록.

이 체크리스트의 완료는 Summon 추출 범위에 한정한다. 전체 EnemyLogic 분해, 모든 행동 조합, broad full 검증이나 성능 개선 완료를 뜻하지 않는다. 이후 Glide → Jump → Charge는 후보 순서이며 각 행동의 실제 결합과 검증 비용을 다시 평가한다.

## 11. 병렬 검토 및 실행 기록

| 항목 | 현재 상태 |
| --- | --- |
| 테스트 전략 1차 검토 | `plan_test_review`: 초안 검토 및 보완 완료. 이후 재검토에서 확인한 항목은 아래 R01–R07에 별도 기록 |
| 추출 경계 1차 검토 | `plan_boundary_review`: 초안 검토 및 보완 완료. 이후 재검토에서 확인한 항목은 아래 R01–R07에 별도 기록 |
| runtime 계획 재검토 | `reaudit_runtime_plan`: R02/R06/R07 수정 후 좁은 재검토 완료, 지적 항목 해소 확인 |
| 테스트 계획 재검토 | `reaudit_test_plan`: R01/R03/R04 수정 후 좁은 재검토 완료, 지적 항목 해소 확인 |
| 실행 절차 재검토 | `reaudit_workflow_plan`: R01/R05 및 셸 실패 전파 보완 확인, 지적 항목 해소 확인 |
| production 변경 | Summon 네 책임을 `EnemySummonExecutor`로 추출. 실제 diff·경계 검토는 결과 문서 참조 |
| baseline/candidate Unity 검증 | 강화된 공통 95/95와 capture 49/97 동등, candidate 계약·구조 21/21. core PlayMode 4 skip과 그래픽 보충 1 실패; 엄격한 완료 gate 미충족 |
| 문서 검증 | 초안 단계의 `git diff --check`, 상대 링크·index 검사와 현재 결과 문서 검증은 서로 다른 실행 기록. 현재 결과는 아래 연결 문서 참조 |

2026-09-25 1차 병렬 검토 반영:

- 테스트 검토: 억제 해제 Tick·Recovery 0·Recover topology 중단·이동 가능한 max-alive 사례를 S02–S04에 반영. 기존 replay dump의 누락 필드를 capture 항목에 추가. helper 변경 시 원래 baseline runtime에서 재확보하도록 명시. 직접 Logic/context 사례의 비교 대상을 별도 정의. P0 manifest 준비와 OR filter를 명시.
- 경계 검토: 네 API, entity ID 입력, coordinator guard와 호출 순서, query write-port 부재, 역참조 금지, 선택적 trigger sink 의미, inclusive 억제·취소 no-op·종료 Tick 정책을 반영. post-attack resolver와 materializer의 책임 및 첫 slice의 production 변경 제외 범위를 명시.
- 두 검토 모두 읽기 전용으로 진행했다. 이는 계획 검토 결과이며 runtime 안전성이나 테스트 통과의 실행 증거는 아니다.

2026-09-25 독립 재검토에서 다음 문제를 확인하고 수정했다. P2는 실행 전에 고쳐야 할 절차·검증 공백, P3는 계약·완료 판정의 명확화다. 아래 상태는 문서 수정과 검토에 관한 것이며 실제 구현 검증을 뜻하지 않는다.

| ID | 우선도 | 발견 내용 | 반영 및 확인 |
| --- | --- | --- | --- |
| R01 | P2 | baseline에도 새 executor 직접 테스트를 포함한 동일 목록을 요구하여 대상 API 부재와 충돌 | §6/§8에서 공통·candidate 전용·core 구분, baseline에는 공통 patch만 적용. 테스트·절차 검토자가 해소 확인 |
| R02 | P2 | 억제 query가 pending blocked reaction 소비·cooldown에 미치는 경로 누락 | C14, consumer 호출표, S09와 capture 추가. runtime 검토자가 해소 확인 |
| R03 | P2 | S 행 하나에 여러 변형을 묶어 일부 assertion만으로 완료 처리 가능 | §5.2 세부 case와 입력/경계/assertion/capture 매핑 및 제외 근거 요구. 테스트 검토자가 해소 확인 |
| R04 | P3 | 양측에서 같은 capture가 빠져도 값 비교가 일치할 수 있음 | case/record/schema 완전성 검사를 동등성 비교의 선행 gate로 추가. 테스트 검토자가 해소 확인 |
| R05 | P2 | runner 호출의 지역 환경이 후속 commit hook에 전달되지 않아 기본 C 출력 사용 가능 | §7에 commit별 D root 환경 상속과 hook 증거 수집 추가. 절차 검토자가 해소 확인 |
| R06 | P3 | query 내부 null guard를 coordinator guard와 혼동할 여지 | 두 API의 null → false 명시, S10 비소환 profile 회귀 매핑. runtime 검토자가 해소 확인 |
| R07 | P3 | 상태 부재 source의 미초기화와 existing inactive state의 취소 no-op 구분 부족 | C13, S04c/S05, capture의 absent/missing 구분 추가. runtime 검토자가 해소 확인 |

셸 예시도 subshell `set -e`로 준비·runner 실패 후 다음 호출을 막도록 보완했다. 재검토에서는 internal 테스트 접근성, OR filter 지원, 현재 runtime의 Recover/topology/선택적 sink 정책과 정합성도 확인했다. 수정 후 각 검토자의 확인은 해당 지적 범위에 한정한다.

실제 실행의 명령·identity·실패와 미실행 사유는 [Summon 추출 결과](./EnemyLogic-Summon-Extraction-Closeout.md)에 연결했다. 이 표의 초기 R01–R07은 계획 검토 기록이며, 구현 검토와 Unity 실행 증거는 결과 문서에서 구분한다.
