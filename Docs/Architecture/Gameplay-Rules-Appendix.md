# Gameplay Rules Appendix

이 문서는 canonical spec의 보조 문서다. 구조 vocabulary가 아니라 gameplay rule text를 기록한다.

## Push
- 관련 코드:
  - `Assets/_Features/Gameplay/Gameplay_Movement/Runtime/Expansion/MovementExpander.cs`
  - `Assets/_Features/Gameplay/Gameplay_Movement/Runtime/Commit/MovementCommitter.cs`
  - `Assets/_Features/Gameplay/Gameplay_PlayerControl/Runtime/PlayerControlState.cs`
- rule:
  - Push start precheck와 pending revalidation은 execute-time Push expansion과 같은 BoxSlide hostile impact query vocabulary를 사용한다.
  - push execute tick에서 다음 칸이 비어 있으면 box를 이동 commit한다.
  - 다음 칸이 유닛 점유 cell이면 `impact`다.
  - Movement는 impact cell의 targetable unit 전체에 대해 `ImpactReservation`을 만들고, Attack이 same-tick damage를 적용한다.
  - Push impact는 simple blocked failure가 아니라 Movement -> Attack handoff contract다.
  - all targets die + landing accepted면 current runtime lethal follow-through formalization으로 same-tick advance를 commit한다.
  - all targets die + suppressed Barricade occupant cleared면 `BarricadeReassertCrush`로 incoming box를 제거하고 FollowThrough하지 않는다.
  - any target survives면 `Stay`다.
  - all targets die + landing denied면 `Stay`다.
  - `Destroy` capability는 Push first-step blocked fallback이다.
  - `Destroy` fallback은 sliding continuation blocked path에 재적용하지 않는다.
  - active Barricade는 PlayerControl이 동일한 tile-definition context를 받기 전까지 execute-time policy다.

## Flip
- 관련 코드:
  - `Assets/_Features/Gameplay/Gameplay_Movement/Runtime/Expansion/MovementExpander.cs`
  - `Assets/_Features/Gameplay/Gameplay_PlayerControl/Runtime/PlayerControlStateLogic.cs`
- rule:
  - BoxFlip precheck는 execute-time drift를 막기 위해 BoxFlip landing policy와 hostile impact query를 사용한다.
  - flip execute tick에서 landing cell을 다시 판정한다.
  - landing cell이 유닛 점유 cell이면 `impact`다.
  - Flip impact는 simple blocked failure가 아니라 Movement -> Attack handoff contract다.
  - current contract에서 flip impact는 impact-result-dependent action uplift다.
  - all targets die + landing accepted면 `FollowThrough`다.
  - all targets die + suppressed Barricade occupant cleared면 `BarricadeReassertCrush`로 incoming box를 제거하고 FollowThrough하지 않는다.
  - any target survives면 `DestroySelf`다.
  - all targets die + landing denied면 `Stay`다.
  - landing cell이 wall/solid box, board edge, reservation conflict, tile feature, or topology rule로 막히면 `blocked`다.
  - `blocked`에서는 impact가 생기지 않는다.

## Push / Flip Impact Disposition Table
- `ImpactDisposition`은 narrow internal Push/Flip-only contract, not a generalized impact framework다.
- 허용 family는 current `Push`, `Sliding Push`, `Flip` hostile `BoxImpact` path뿐이다.
- `ForwardCellImpact`, jump landing, melee/contact, delayed effect, item consume, broader impact family generalization은 이번 단계 non-goal이다.

| Family | Attack outcome | Settlement outcome | Disposition | Note |
| --- | --- | --- | --- | --- |
| Push / Sliding Push | any target survives | not asked | `Stay` | current runtime lethal follow-through formalization |
| Push / Sliding Push | all targets die | landing accepted | `FollowThrough` | current runtime lethal follow-through formalization |
| Push / Sliding Push | all targets die | suppressed Barricade occupant cleared | `BarricadeReassertCrush` | Barricade immediately reasserts and crushes incoming box |
| Push / Sliding Push | all targets die | landing denied | `Stay` | current runtime lethal follow-through formalization |
| Flip | any target survives | not asked | `DestroySelf` | impact-result-dependent action uplift |
| Flip | all targets die | landing accepted | `FollowThrough` | impact-result-dependent action uplift |
| Flip | all targets die | suppressed Barricade occupant cleared | `BarricadeReassertCrush` | Barricade immediately reasserts and crushes incoming box |
| Flip | all targets die | landing denied | `Stay` | current contract decision |

- `Flip lethal but landing denied = Stay` is a current contract decision for the current Push/Flip impact-disposition plan. It is not a generalized impact principle.
- Transient collision/break is a presentation-only track.

## Barricade Active Solid Invariant
- This is a tile-effect invariant, not a MovementExpander, SurfaceSlideQueries, or settlement legality rule.
- A topology-active/effective-active Barricade never allows terminal valid Box Solid occupancy on the same `SurfaceCell`.
- A same-cell live Unit/enemy suppressor defers Box crush; when that Unit leaves and the Barricade remains topology-active, the remaining Box is crushed even without a new inactive-to-active transition.

## Traverse vs Settle
- `Traverse`는 actor가 이동 step 또는 topology transition을 통과할 수 있는지 묻는다.
- `Settle`는 통과 후 terminal cell에 끝날 수 있는지 묻는다.
- runtime legality owner는 다음처럼 분리한다.
  - `Placement`: spawn/respawn/debug authoritative placement legality
  - `Traversal`: movement step legality
  - `Settlement`: landing/follow-through legality
- 대표 distinction:
  - traverse는 allowed지만 settlement는 blocked일 수 있다.
  - traverse가 blocked면 settlement는 묻지 않는다.

## SpatialState
- current live runtime producer가 emit하는 state는 `Anchored`, `Airborne`, `Phased`다.
- `Airborne`는 jump owner가 만든 explicit non-anchored state일 때만 인정한다.
- `Phased`는 `WorldState` authoritative carrier를 가지며 internal pre-movement owner가 live runtime에서 emit할 수 있다.
- current production gameplay concrete live source는 `PlayerControlStateLogic`의 player flip windup window다.
- horizontal expansion validation을 위한 additional owner는 internal `SystemPreMovementValidationLogic` 하나만 허용한다. 이것은 internal validation owner, not public scripted framework다.
- `Attached`는 아직 reserved future state이며 legality/query consumer도 열지 않는다.
- `Anchored`는 기본 spatial mode다. `Detached`라고 해서 자동으로 `Airborne`가 되지 않는다.

## Phased
- 이번 단계에서 고정하는 것:
  - `Phased` live seam은 traversal에서 `Unit`/`Solid` blocker bypass capability를 이해한다.
  - `Phased` live seam은 fresh target acquisition suppression을 이해한다.
  - current enemy current-lock path만 `existing lock retention`을 narrow hook로 사용한다.
  - `Phased`는 `BoardEdge`, `Reservation`, `TileFeature`, or topology bypass를 뜻하지 않는다.
  - current live profile은 `ClaimsAuthoritativeOccupancy=true`와 active-face visibility, anchored-like terminal settle을 `StageDefault`로 사용한다. 이것은 current implementation default이지 future invariant가 아니다.
  - current live enter/sustain/exit owner는 `MovementPreMovement`, `SystemPreMovementValidation`, `DebugForced`로 metadata table에 고정한다.
  - `RetiredEnemyPreMovement`는 retired `PhaseThroughLockedTarget` compatibility slot이며 current runtime producer가 아니다.
  - retired locked-target cross-through validator와 phase relocation path는 current movement, targeting, trace, replay contract로 취급하지 않는다.
- 이번 단계에서 고정하지 않는 것:
  - future non-claim / overlap model
  - future visibility variants
  - future settlement overlap semantics
  - multi-source arbitration / generalized phase framework
  - attack-owned / delayed-effect / public scripted-debug source
- interpretation rule:
  - current live owner는 internal `PreMovementState` write-path다.
  - carrier truth-source는 `PhasedRuntimeState` 하나뿐이며 caller-local bool 조합으로 추론하지 않는다.
  - earliest live observation point는 pre-movement batch 이후 `planSnapshot`이다.
  - sibling pre-movement logic는 same-pass enter/exit를 관측하지 못한다.
  - later clear/cancel은 이후 snapshot부터만 보이고 earlier snapshot을 retroactive하게 바꾸지 않는다.
  - current live source가 다른 owner와 충돌하면 explicit clear/replace ordering 없이는 공존하지 않는다.
  - `SystemPreMovementValidation` source path는 reservation을 읽지 않는다.
  - `cell-only`는 terminal cell이 하나로 고정된 뒤 `GetCellStatus(...)` 한 번만 읽고, 그 결과를 바로 `SettlementContext.ReservationStatus`로 넘기는 contract를 뜻한다. direct `edge/entity/topology-exclusive` reservation read와 multi-cell speculative read는 out-of-scope다.

### Retired PhaseThrough Note
- `PhaseThroughLockedTarget`, locked-target cross-through validator, and `EnemyPhaseRelocation` are retired synthetic/runtime residue.
- The numeric enum/owner/diagnostic slots remain reserved for compatibility, but they do not define current gameplay behavior.
- Current trace/replay contracts should not require `EnemyPhaseRelocation` or `PhaseEnter/Exit` from the retired enemy owner.
- string ordering, incidental formatting, internal ids, helper names, inline token count는 implementation detail이다.

## Targetability
- targetability participation은 traversal/settlement blocker vocabulary와 별도 축이다.
- current seam rule:
  - base default는 spatial fact에서 시작한다.
  - current v1 default는 `SpatialStateSemantics`가 제공하고 effective seam은 `ModifierQuery`를 거친다.
  - blocker enum에 targetability-only kind를 추가하지 않는다.
  - current `existing lock 유지`는 current enemy lock path에만 한정한다.
  - future `impact-only suppression`, `detection-only suppression`, broader `existing lock 유지`는 `ModifierQuery` typed-evidence hook에서만 연다.
- fresh acquisition aggregation rule:
  - status: 2026-09-16 A의 Unit-only gameplay contract는 유지한다. B의 Direct-only ordered Unit cache는 stage-4-2/4-3 재계측에서 채택 이득을 입증하지 못해 CurrentPolicy에서 철회했고, 두 selector가 기존 ordered-entity span을 직접 필터하는 CurrentPolicy로 교체·검증했다.
  - tests-first 실행에서 `EnemyTargetSelectorContractTests` filtered Full EditMode `23/13`과 낮은 ID blocker BlackEye `1/1` red를 재현했으나 후속 실행이 공용 TestResults를 덮어써 당시 raw XML/log는 보존되지 않았다. 최종 구현 상태에서는 selector/BlackEye combined filtered Full EditMode `79/0`(`31/0` + `48/0`), EnemyLogic/Modifier/scenario/replay combined `435/0`(`183/0` + `23/0` + `229/0`) 및 core EditMode `290 passed / 0 failed`, PlayMode `112 total / 108 passed / 4 skipped / 0 failed`를 확인했다. matching filtered PlayMode는 `0`이었고 broad unfiltered `full`은 실행하지 않았다.
  - aggregate candidate domain은 모든 `EntityType.Unit` record다. non-Unit은 선택과 aggregate rejection attribution 양쪽에서 제외한다.
  - Unit-only는 occupying-only와 동의어가 아니다. source 자신, same-team, dead, `markedForDeath`, Detached/non-occupying, inactive-face, `Airborne`, `Phased` Unit도 candidate-specific eligibility가 판정하기 전에는 aggregate input에서 제거하지 않는다.
  - non-Unit entity의 존재나 entity ID ordering은 selected Unit, aggregate rejection reason, Enemy AI transition에 영향을 주면 안 된다.
  - Unit candidate의 deterministic order와 equal-distance tie break는 entity ID 오름차순이다.
  - 성공 result는 최종 선택된 Unit의 accepted eligibility result다. 뒤쪽 reject candidate가 성공 result를 덮어쓰지 않는다.
  - 실패 result는 Unit candidate에 대해서만 current rejection capture policy를 적용하고, capture된 Unit rejection이 없을 때 target ID `0`의 `TargetMissing`을 사용한다.
  - `FreshSelectionSuppressedBySpatialState` 우선 규칙은 유지한다.
  - clean B-stage closure evidence: detached `d05a47761`에서 cache/selector/BlackEye combined filtered Full EditMode `84/0`, EnemyLogic/Modifier/BlackEye `254/0`을 재현했다. core는 D-drive detached B worktree의 `/mnt/d/J2M/evidence/ordered-unit-cache-20260915/remeasure-preflight-clean-B-d05a47761/core`에서 다시 실행해 EditMode `290 passed / 0 failed`, PlayMode `112 total / 108 passed / 4 skipped / 0 failed`를 확인했다. 보강 commit `0541831a6`에서는 lazy-first-read isolation을 포함한 cache/selector/BlackEye combined `85/0`을 clean detached worktree에서 확인했다.
  - 공통 schema-7 attribution harness overlay를 적용한 A/B D-drive worktree에서도 `/mnt/d/J2M/evidence/ordered-unit-cache-20260915/remeasure-harness-A-7ec6c80b1/core`와 `remeasure-harness-B-d05a47761/core`가 각각 core EditMode `290/0`, PlayMode `112 total / 108 passed / 4 skipped / 0 failed`로 통과했다. overlay source SHA-256은 `a19f2a4cc2601f49d5bb26be87aea04431712e3935c940f09c3b8938e229b9e4`, runner SHA-256은 `2a9d190222db8ef50630d142536750c5d54122184ce07a23db2f382dcbedbd16`, harness SHA-256은 `1cd2e1be5135984d0eb50553b12f4018c4eab96710d46ae7e33061bd63c2e0ab`다.
  - A/B byte parity oracle은 모든 observable의 포괄 비교가 아니라 trailing Box/Wall 성공 4건의 selected target/accepted result와 BlackEye 1건의 mode/action/pending impact/full trace/determinism hash, 총 5개 출력에 한정된다. 나머지 blocked rejection, trailing rejected Unit, Unit state 의미, tie, custom strategy는 기대값 기반 contract tests로 검증한다.
  - raw XML/log 보존 범위는 `/mnt/d/J2M/evidence/ordered-unit-cache-20260915/A/selector-blackeye`, `clean-B-d05a47761/selector-blackeye-cache`, `clean-B-d05a47761/related-targeted`, `clean-hardened-0541831a6/selector-blackeye-cache`, `remeasure-preflight-clean-B-d05a47761/core`, `remeasure-harness-A-7ec6c80b1/core`, `remeasure-harness-B-d05a47761/core`다. 기존 `clean-B-d05a47761/core`는 project-path 귀속이 잘못되어 closure evidence에서 제외하며, 앞서 실행한 scenario/replay `229/0` raw artifact는 이 bundle에 포함되지 않는다.
  - B 철회 및 ordered-entity span 직접 필터 구현 후 현재 working tree에서 selector/BlackEye combined filtered Full EditMode `79/0`, EnemyLogic/Modifier combined `206/0`, Enemy AI scenario/replay combined `229/0`, core EditMode `290/0`, core PlayMode `112 total / 108 passed / 4 skipped / 0 failed`를 확인했다. 각 filtered matching PlayMode는 `0`이었고 후속 실행이 공용 TestResults를 덮어써 개별 targeted raw XML은 별도 보존하지 않았다. broad unfiltered `full`은 실행하지 않았다.
- scope boundary:
  - specific-target evaluation, current enemy locked-target retention, local engagement hold, combat action validation, passive contact validation은 이 aggregate-domain 변경 대상이 아니다.
  - `_stackedUnitsByCell`/`EnumerateUnitsAt(...)`의 cell-local occupancy query를 global fresh-acquisition candidate source로 사용하지 않는다.
  - ordered Unit cache는 targetability, occupancy, visibility 의미의 owner가 아닌 과거 B 성능 실험이었다. 해당 cache의 lazy/two-pass/array/span/Direct-only 표현과 전용 diagnostics는 gameplay contract가 아니며 철회 대상이다.
  - 모든 Unit record 포함, non-Unit 제외, entity-ID 결정성, selected/rejection result 의미는 StrongContract다. 이를 구현하는 candidate source와 자료구조는 observable contract를 보존하는 한 CurrentPolicy다.
  - 현재 CurrentPolicy는 두 selector가 `WorldSnapshot.GetOrderedEntitiesForRead()`의 기존 entity-ID 정렬 span을 직접 순회하면서 non-Unit을 즉시 제외하는 방식이다. 별도 Unit cache와 정렬을 만들지 않고, Nearest의 임시 `List<EntityState>` 할당·복사도 제거한다.
  - 2026-09-16 performance evidence는 `/mnt/d/J2M/evidence/ordered-unit-cache-20260915/remeasure-performance`와 `remeasure-2-performance`에 보존한다. 동일한 1920x1080 release-like Player, warmup `120`, phase당 `600` frame, tick interval `6`, 각 run `100` Tick 조건으로 두 campaign을 각각 A-B-B-A 순서로 stage-4-3과 stage-4-2에서 실행했다. 합계 16 run 모두 `cpu-tick-v1` performance admission과 schema-7 tick attribution은 `ADMITTED`였다.
  - stage별 A/B 각 4개 run summary의 산술평균에서 B의 EnemyAI plan median은 stage-4-3 `0.058938 ms`로 A `0.045275 ms` 대비 `+30.18%`, stage-4-2 `0.043200 ms`로 A `0.028538 ms` 대비 `+51.38%`였다. 두 stage 모두 B의 네 plan median 최솟값이 A의 네 최댓값보다 높아 두 campaign에서 같은 방향을 재현했다.
  - 전체 Tick median은 누적 평균에서 stage-4-3 `+2.62%`, stage-4-2 `+4.18%`였지만, stage-4-3 campaign별 방향이 `+13.34%`와 `-8.03%`로 뒤집혀 system-level 차이는 측정 노이즈에 민감했다. Direct-only cache의 전체 Tick 성능 향상은 입증되지 않았고 Plan 국소 회귀는 반복됐으므로 현 B 구현은 철회한다. stage-4-3 후속 BeforeAttack read 이득은 과거 실험의 국소 결과로 보존하며 범용 채택 근거로 사용하지 않는다.
  - gameplay-performance wrapper의 최종 상태는 16 run 모두 `HOLD_CLEANUP_ADMISSION`이다. 이는 측정/attribution 실패가 아니라 별도 Cleanup S3 calibration이 현재 indexed-only 실행을 full-scan strategy로 기대한 mismatch와 allocation counter probe `0`을 거부했기 때문이다. 그러므로 성능 및 attribution `ADMITTED`와 전체 wrapper `HOLD`를 분리해 보고한다.
  - ordered-entity span CurrentPolicy(C)는 `/mnt/d/J2M/evidence/ordered-entity-span-20260916/remeasure-performance`에서 같은 runner/harness와 측정 조건으로 stage-4-3/4-2 각 4회, 총 8회 재계측했다. 8회 모두 performance와 schema-7 attribution은 `ADMITTED`였고 wrapper는 위와 같은 독립 Cleanup S3 사유로 `HOLD_CLEANUP_ADMISSION`이었다. C capture 내부의 revision, worktree hash, runtime-tree hash, runner hash, harness hash는 각각 하나로 일치했다.
  - C의 EnemyAI Plan median 4-run 평균은 stage-4-3 `0.041962 ms`, stage-4-2 `0.028862 ms`였다. 과거 동일 프로토콜 B 평균 대비 각각 `-28.80%`, `-33.19%`이고 A 대비 `-7.32%`, `+1.14%`여서 B의 반복된 first-read Plan 회귀를 제거하고 대체로 A 수준을 회복했다.
  - raw tick별 Plan+BeforeAttack 합산 median은 stage-4-3 `0.082800 ms`로 A/B 대비 각각 `-9.36%`/`-9.51%`, stage-4-2 `0.033100 ms`로 A/B 대비 `+0.46%`/`-31.26%`였다. C는 stage-4-3에서 B의 후속 BeforeAttack cache-hit 이득을 반환하지만 결합 median은 악화되지 않았고, 결합 p95는 A 대비 `+0.87%`로 사실상 같은 범위였다.
  - C 전체 Tick median은 과거 A 대비 stage-4-3 `-14.64%`, stage-4-2 `-2.47%`였으나 C 측정이 새 A control과 interleave되지 않은 후속 campaign이고 기존 whole-Tick 방향도 noise-sensitive했으므로 system-level speedup 근거로 사용하지 않는다. 국소 attribution은 C를 낮은 복잡도의 CurrentPolicy로 유지하고 B를 복구하지 않을 decision evidence다. 상세 표는 `/mnt/d/J2M/evidence/ordered-entity-span-20260916/remeasure-summary.md`에 보존한다.
- Deferred lock taxonomy:
  - current `existing lock 유지`는 `EnemyActionStateTargeting.TryResolveLockedTarget(...)` current enemy path 전용 stage-scoped exception이다.
  - future taxonomy 후보 이름은 `detection lock`, `impact lock`, `scripted/debug lock`, `UI/presentation selection lock`, `future AI pursuit lock`까지만 기록한다. semantics는 이번 단계에서 열지 않는다.
  - 다음 단계 implementer는 current helper가 generic lock framework의 seed라고 가정하면 안 된다.
  - 다음 단계 implementer는 다른 lock 종류가 current enemy retention helper를 재사용해도 된다고 가정하면 안 된다.
  - 다음 단계 implementer는 `FreshSelectionSuppressedWithCurrentEnemyLockRetention` enum이 taxonomy 완성형이라고 가정하면 안 된다.
  - 새 lock class를 열려면 새 evidence type, 새 use-site decision, 새 tests가 필요하며 current helper rename/generalization으로 해결하지 않는다.

## Airborne Jump Landing
- `Airborne` actor는 traversal 중에는 일반 occupancy blocker로 취급되지 않는다.
- jump landing settlement에서는 requested terminal state가 `Anchored`로 돌아오며 landing cell legality를 다시 판정한다.
- anchored blocker가 landing cell을 차지하고 있으면 settlement가 blocked일 수 있다.

## Reservation
- `FrozenMovementReservationExport` contract는 유지한다.
- 이번 단계 reservation generalization 범위는 legality read seam용 `ReservationQuery` adapter까지만 허용한다.
- reservation detail 확장이 필요해져도 current public blocker vocabulary는 `Reservation` top-level kind를 유지한다.
- reservation read sequence의 primary truth는 semantic contract다. inline token count는 secondary sentinel일 뿐이며 helper extraction이 생겨도 terminal single-cell pre-settle read contract가 유지되어야 한다.
- semantic contract is the primary source-of-truth; inline token count is only a secondary sentinel.

## Common Execute Outcomes
- canonical public rule text는 `success / impact / blocked` 축을 사용한다.
- 이 축은 semantic summary다. internal resolver는 더 많은 payload를 가질 수 있다.
- implementation에서 유지해야 하는 payload 관심사:
  - `createdImpactReservation`
  - `boxRelocated`
  - `resolvedCell`
  - `enteredRecovery`
  - `rejectReason`

## Recovery
- push/flip의 `impact`와 `blocked`는 둘 다 execute attempted failure다.
- 둘 다 cancel이 아니라 recovery로 진입한다.
- view는 `TickPresentationData.PlayerActionSignals`만 보고 recovery를 표현한다.

### Push/Flip Impact Handoff Note
- Push change is formalization, not a new framework.
- Do not redesign Push around a new disposition framework; formalize what runtime already does.
- Flip change is a narrow impact-result-dependent uplift.
- `Flip lethal but landing denied = Stay` is a current contract decision.
- Transient collision/break is presentation-only and must not be used as gameplay truth.

## Cleanup Occupancy Policy
- `markedForDeath` 또는 `hp <= 0` entity는 Cleanup 전까지 snapshot query에 남아 있을 수 있다.
- placement query와 impact target selection은 동일한 질문이 아니므로 분리해서 해석한다.
- verification should prefer:
  - `EnumerateUnitsAt(...)`
  - `TryPickImpactTargetAt(...)`
  - `TryGetUnitTraversalBlocker(...)`

## Determinism
- deterministic contract는 자료구조 iteration order가 아니라 explicit ordering policy로 보장한다.
- regression validation은 hash, trace equality, final entities, event log를 우선 본다.
- trace section name이나 synthetic normalized input token은 canonical contract가 아니다.

## Stage Objective
- `StageDefinition.Zones`는 stage-local spatial registry다. zone data 자체는 objective special field가 아니다.
- Objective goal은 `StageObjectiveAuthoring.ConditionEntries`에 `PlayerAtAnyZoneConditionAsset`을 연결하고 entry `Role=PrimaryGoal`로 표시한다.
- Objective clear 공식은 `Required=true` condition entry가 모두 satisfied인 것이다.
- `GoalReached`는 유지되는 UI/read-model field지만 source는 `Role=PrimaryGoal` condition status의 `IsSatisfied`다.
- `PrimaryGoal` entry가 없으면 `GoalReached=false`이며 required conditions만으로 clear될 수 있다.
- legacy `GoalZoneIds`, `GoalZones`, `IsPlayerOnGoal`, `RequirePlayerOnGoalWithAllConditions` 경로는 제거되었다.

## Topology Visual Bridges
- topology visual bridge는 scene-side presentation helper다.
- bridge는 gameplay collision, movement, query, authoritative wall/entity/runtime data에 참여하지 않는다.
- `StageRuntimeBuilder`, `WorldSnapshot`, wall entity export와 연결하지 않는다.
- v1 visibility rule은 logical `FaceId` pair를 사용한다.
- topology transition 중에는 `destinationTopology` active face만 기준으로 bridge visibility를 평가한다.
- outgoing seam bridge는 회전 시작 시점에 즉시 사라질 수 있다. incoming seam bridge만 transition 동안 유지한다.
- authored bridge에 collider를 붙여 gameplay 의미를 주지 않는다. collider가 필요해 보이면 별도 후속 설계로 다룬다.
- 이 controller는 presenter-derived presentation-only sync다. authoritative state owner가 아니다.
- TODO future option A: logical `FaceId` pair 유지
- TODO future option B: projector slot-pair visibility rule
- TODO future option C: hard on/off 대신 alpha fade
- TODO future option D: per-bridge policy override

## Result Carrier Metadata
- `DamageResolutionRecord`, `DestroyResolutionRecord`, `DelayedAttackEffectRecord`는 semantic result와 provenance metadata를 함께 운반할 수 있지만, 두 층을 같은 의미로 읽으면 안 된다.
- semantic field:
  - `SourceId`, `SourceKind`, `TargetId`, `Amount`, `Accepted`, `RejectReason`, `Condition`, `FinalHp`
- provenance / correlation field:
  - `ActionPlanId`
  - `IntentId`
  - `LocalActionIndex`
  - `SourceActionPlanId`
  - `EffectSequence`
- canonical runtime reader는 plan-level correlation에서 `GroupId`가 아니라 `ActionPlanId` 계열을 사용한다.
- `GroupId` / `SourceActionGroupId` wording은 removed compatibility diagnostics surface일 뿐 canonical rule text가 아니다.
- source-of-truth reading order는 typed runtime carrier -> canonical structured trace `Plan=` / `SourcePlan=` -> free-form compatibility log `G=`다.
- free-form `G=` token은 human-readable compatibility surface일 뿐이며 canonical parser input이 아니다. current `ActionPlanId` value를 mirror하지만 obsolete alias token이지 old semantic GroupId revival이 아니다.
- trace/debug/log를 볼 때 primary source-of-truth는 `ActionPlanId` / `SourceActionPlanId`와 canonical structured trace `Plan=` / `SourcePlan=`다.
- `TickEntityMotion`은 committed logical movement 전용이다.
- `Flip DestroySelf` / `Flip Stay`는 fake `TickEntityMotionKind.Flip`을 만들지 않고 `FlipImpactPresentationSignal`로 common pre-impact flip arc를 전달한다.
- `FlipImpactPresentationSignal`은 gameplay authority가 아니라 presentation-only carrier다.
- `Flip FollowThrough`는 기존 `EntityMotion Flip` path를 유지한다.
- `Flip DestroySelf`는 transient clone/effect로 break/fade overlap branch를 재생하고, `Flip Stay`는 actual box view override track으로 source -> impact -> source return을 표현한다.
- box contact, destroy break start, player release, stay recoil branch timing은 centralized flip-impact timing setting에서 공유한다.
- `Flip DestroySelf`에서 shared contact normalized time은 break/release onset threshold다. final impact pose arrival은 full flip flight duration 시점이며, destroy break/fade는 그 onset부터 flight와 overlap될 수 있다.
