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
- Player actor-facing:
  - `PlayerControlStateLogic` owns authoritative contact-facing and result-facing writes for explicit Player Push/Flip actions.
  - `CommitFlipResultFacingOnExecute` commits Flip result-facing on the execute tick.
  - `ImpactTravelGeometry` and BoxImpact rematerialization must not write Player actor-facing.
- Result Turn:
  - Result Turn is presentation-only and reads the committed contact/result-facing pair.
  - delay is `FlipWindup`; active duration is `FlipRecovery`.
  - one-shot signal absence does not cancel an active track.
  - same/older `ActionSequence` does not restart or replace a track; newer `ActionSequence` replaces it.
  - natural completion applies the result endpoint before cleanup, and the next frame/tick continues to use committed result-facing.

## BoxImpact Roles
- `ActorEntityId` is the action actor that initiated Push/Flip. It is not a generic rematerialization target.
- `ImpactSourceEntityId` is the moving box/impact source and is the only entity that disposition move/facing/state rematerialization may update.
- `TargetEntityIds` are hostile impact targets handed to Attack.
- `ImpactTravelGeometry` stores source/impact/follow-through cells and travel direction only. It must not carry Player-facing, actor-facing, or state-write target authority.

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

## Flip Outcome Final State
| Outcome | Player final cell | Player final facing | Box final cell | Box final facing | Box presence | Box death | Enemy result |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Empty successful Flip | unchanged actor cell | result-facing | landing cell | travel/result direction | present | no | none |
| Lethal FollowThrough | unchanged actor cell | result-facing | follow-through/landing accepted cell | travel/result direction | present | no | impacted enemies removed by cleanup |
| Lethal Stay | unchanged actor cell | result-facing | source cell | source/result direction retained by source pose commit | present | no | impacted enemies killed, landing denied |
| Nonlethal DestroySelf | unchanged actor cell | result-facing | source/impact presentation only, no committed follow-through | source/result direction retained until removal | removed by destruction | yes | surviving targets remain |
| FlipLandingBlocked | unchanged actor cell | recovery/result-facing contract from Player action | source cell | unchanged by BoxImpact | present | no | no hostile impact handoff |

- The Player final-facing source for every Flip outcome is `PlayerControlStateLogic`, not BoxImpact geometry.
- Box final cell/facing/presence is owned by the BoxImpact disposition payload and impact-source pose commit.
- A value not represented by the current source operation should be treated as unspecified rather than inferred from presentation.

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
- Result Turn intermediate visual rotation, presentation track lifetime, and view `Transform` values are not canonical determinism hash inputs.

## Player Ordinary Locomotion Boundary
- Player ordinary locomotion is Free2D-only.
- `MovementExpander` remains available for explicit Push/Flip/Item semantics and other non-player ordinary movement branches, but Player ordinary Move must not regain Kinematic/Discrete legacy fallback.
- A blocked Player ordinary Move can leave final state unchanged without requiring a legacy-fallback diagnostic string.

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
