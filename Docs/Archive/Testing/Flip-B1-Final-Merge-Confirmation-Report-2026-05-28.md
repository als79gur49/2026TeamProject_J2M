# Flip B-1 Final Merge Confirmation Report

Archive note: ordinary Flip B-1 and B-1-specific legacy timing cleanup are now tracked by [Flip-B1-Legacy-Timing-Cleanup-8B-2026-05-29.md](../../Testing/Flip-B1-Legacy-Timing-Cleanup-8B-2026-05-29.md). This dated report remains historical evidence for the hostile-impact-only merge checkpoint; its ordinary-Flip deferral rows are superseded for current status.

## 1. Summary
- Final readiness judgment: merge under hostile-impact-only scope.
- Scope: hostile impact Flip only.
- What was not changed: ordinary Flip success, Push/other impact behavior, due resolver gameplay policy, legacy `AtContactTime` paths, StageResult/reward authoritative commit timing, target reservation, and enemy suppression.
- Motion contract: B-1 hostile Flip uses the existing Flip `MotionTrack` duration unchanged. B-1 does not stretch or shrink the Flip motion to fit `DueTick`; presentation scheduling aligns the existing Flip contact/slam key with the B-1 visual impact due tick.
- Hit timing contract: authoritative hit/damage/death is anchored to the action-normalized visual impact due tick, `ActionStartTick + RoundToInt(FlipInputLockDurationTicks * 0.936)`, not the incorrect execute-based formula `DueTick = ExecuteTick + 12`.
- Timing correction: `ExecuteTick` was already `ActionStartTick + 23`; therefore `ExecuteTick + 12 = ActionStartTick + 35`, and `35 / 57 = 0.614`, which is the old no-hit point.
- Manual UX method: closest available visual smoke evidence through batch PlayMode smoke. No hand-operated GUI pass was performed.
- Full lane remains known baseline red. Do not report full green or project-wide green for this revision.

## 2. Final Scope
- Hostile impact Flip only.
- At this hostile-impact-only checkpoint, ordinary Flip success was unchanged; this is superseded by the 8B cleanup note for current ordinary Flip status.
- No target reservation.
- No enemy suppression.
- No `ImpactReservation` or `AttackInputNormalizer` synthetic hit.
- No `MotionTrack`, sampler, duration, or due resolver policy change.

## 3. Final Timing Contract
- Execute tick schedules only: source box becomes `BoxInFlight` and `ScheduledFlipContact` is created.
- Old `0.614` action-normalized timing is a no-hit point.
- Visual impact due tick resolves contact.
- Due tick is action-start based, not execute-based.
- Correct: `DueTick = ActionStartTick + RoundToInt(FlipInputLockDurationTicks * 0.936)`.
- Incorrect: `DueTick = ExecuteTick + 12`.

Actual input end-to-end trace:

- `ActionStartTick = 1`
- `ExecuteTick = 24`
- `ScheduledContactCreatedTick = 24`
- old broken timing tick = `36`
- `DueTick = 54`
- `ActionDurationTicks = 57`
- `ActionNormAtExecute = 23 / 57 = 0.404`
- `ActionNormAtOldBrokenTiming = 35 / 57 = 0.614`
- `ActionNormAtDue = 53 / 57 = 0.930`
- configured target normalized visual impact = `0.936`

| Event | Tick | ActionNorm | Result |
|---|---:|---:|---|
| action start | 1 | 0.000 | input branch |
| execute / schedule | 24 | 0.404 | BoxInFlight + ScheduledFlipContact |
| old broken timing | 36 | 0.614 | no hit |
| visual impact due | 54 | 0.930 | B1ScheduledContactDue hit |

| Timing | DamagePath | Result |
|---|---|---|
| old 0.614 point | none | no damage |
| due 0.930 point | B1ScheduledContactDue | damage/death/disposition |

Old broken timing at tick `36`:

- HP unchanged
- `markedForDeath = false`
- target not removed
- source box still `InFlight`
- `ScheduledFlipContact` still pending
- no `FlipDueContactSignals`
- no `EnemyDamageSignals`
- no enemy exit signal

Due tick at tick `54`:

- due resolver runs
- current `WorldSnapshot` requery
- hostile survives case: HP `3 -> 2`, source box `DestroySelf`
- hostile dies case: target removed, source box materializes at landing if settlement allowed
- scheduled contact removed
- `DueContactImmediate` emitted
- `DamagePath = B1ScheduledContactDue`

## 4. Manual UX Smoke

| Scenario | Expected | Actual | Pass/Fail | Notes |
|---|---|---|---|---|
| moving enemy moves away before due | due 전 enemy freeze 없음; due 전 damage/death/exit 없음; due 전 death VFX/SFX 없음; due tick에서 contact cell empty면 EmptyLand / FollowThrough; original enemy는 damage/death/exit 없음 | `FlipB1_PlayMode_MovingEnemyDoesNotFreezeBeforeDue` passed. Pre-due ticks had no damage or exit, enemy moved to the new cell, due contact resolved `EmptyLand`, original enemy survived at full HP, and source box materialized at contact cell. | Pass | Closest visual smoke evidence from PlayMode batch. |
| different hostile enters contact cell before due | original enemy가 아니라 current contact cell hostile이 hit; hit VFX/SFX는 due target 기준; original enemy는 damage/exit 없음 | `FlipB1_PlayMode_DifferentEnemyEnteringContactCellGetsHit` passed. Due contact hit entity `41`, damage signal targeted `41`, original entity `40` had no exit and remained at full HP. | Pass | Current snapshot requery verified by PlayMode smoke and scenario coverage. |
| hostile remains and dies at due | due 전 death 없음; due contact 순간 hit/death VFX/SFX; death는 exit-owned tail; original + clone 이중 노출 없음; StageResult가 contact/death를 즉시 덮지 않음 | `FlipB1_PlayMode_DueDeathUsesExitOwnedTail` and `FlipB1_PlayMode_StageResultDoesNotCoverDueContact` passed. Pre-due had no damage/exit, due tick emitted damage and immediate `DueContactImmediate` death exit, active duplicate view count stayed bounded, and stage clear facts remained visible before StageResult publication. | Pass | No hand-operated subjective visual pass; carrier and view-registry assertions passed. |
| hostile survives at due | source box DestroySelf; box destroy VFX/SFX due result 기준; Destroy capability와 무관하게 Flip-induced DestroySelf 동작; enemy death VFX/SFX 없음 | `FlipB1HostileImpact_DueTick_HostileSurvives_SourceBoxDestroySelf`, `FlipB1Presentation_DueDestroySelf_BoxExitUsesImmediateDueFact`, and `FlipB1HostileImpact_DueTick_DestroySelfDoesNotRequireDestroyCapability` passed in the targeted scenario subset. Enemy survived with HP reduced, source box was removed by due-result DestroySelf, and no enemy death exit was emitted. | Pass | Automated carrier evidence, not a hand-operated VFX/SFX audition. |
| due contact clears stage | authoritative stage clear/reward commit은 same tick; StageResult frame publication만 barrier 후; 0.12s barrier가 contact/death visibility에 충분한지 확인 | `FlipB1_PlayMode_StageResultDoesNotCoverDueContact` passed. Sprint 6 readiness evidence also records the B-1 barrier guard as passed. Due tick cleared objective same tick and used immediate due contact/death facts. | Pass | Keep `0.12s`; no evidence supports automatic tuning in this pass. |

Barrier recommendation: keep `0.12s` for merge. If later hand-operated UX finds the first contact/death frames too abrupt, compare `0.18s` first and `0.20s` only if still needed. Do not change the barrier in this confirmation pass.

## 5. Adjacent Watchlist

| Test | Area | Could relate to B-1? | Evidence | Decision |
|---|---|---|---|---|
| `GameplayHostPresentationFeed_StageClearVictoryDelay_DefersStageClearedFrame` | StageResult timing | Indirect only | Latest full artifact still fails this generic victory-delay fixture with `System.Reflection.TargetInvocationException`. It is adjacent to StageResult publication, but not the `DueContactImmediate` B-1 barrier path. B-1 stage-clear smoke passed. | Not B-1 blocking; keep generic fixture repair separate. |
| `WorldStatePlacementInvariantTests.CreateSnapshot_DetachedBoxSharingUnitCell_RemainsMaterializableAndDoesNotBlockOccupancy` | WorldState / occupancy | Low-to-medium adjacency | Latest full artifact still fails detached box sharing unit cell representability: `Debug spawn entity 20 is not representable at Floor(0,0)`. This monitors occupancy semantics, but the failure is detached sharing, not `BoxInFlight` or scheduled due contact. B-1 foundation tests passed. | Not B-1 blocking; keep occupancy owner follow-up. |
| `FlipArcSamplerTests.BoxFlipSlamSampler_LiftsOverPivotWithLateralArcThenSlamsTowardTarget` | Flip visual sampler | Visual adjacency only | Latest full artifact still fails sampler numeric expectation: expected slam midpoint `> 0.75f`. B-1 did not change sampler math or due resolver policy. B-1 contact readability smoke passed through PlayMode carrier/view assertions. | Not B-1 blocking; keep visual sampler follow-up. |

B-1 relation judgment: none of the three watchlist failures directly targets StageResult B-1 barrier behavior, `BoxInFlight` scheduled-contact semantics, due presentation timing, or a Sprint 1-6 direct dependency that failed in B-1 evidence.

## 6. Merge Note

# Flip Hostile Impact B-1 Merge Note

## Scope
- hostile impact Flip only
- ordinary Flip success unchanged at this hostile-impact-only checkpoint; superseded by the 8B cleanup note
- push/other impact unchanged
- no target reservation
- no enemy suppression
- no `ImpactReservation` / `AttackInputNormalizer` synthetic hit

## Behavior Change
- execute tick no damage/death/removal
- source box InFlight
- ScheduledFlipContact
- due tick current snapshot requery
- due result damage/death/disposition
- `DamagePath = B1ScheduledContactDue`

## Timing Change
- correct due tick formula: `ActionStartTick + RoundToInt(FlipInputLockDurationTicks * 0.936)`
- incorrect old formula removed: `ExecuteTick + 12`
- old `0.614` action-normalized point is no-hit
- due `0.930` action-normalized point resolves the scheduled contact

## Presentation Change
- DueContactImmediate
- no legacy AtContactTime delay for B-1 due facts
- existing Flip MotionTrack duration unchanged
- no B-1 duration remap, stretch, or shrink
- presentation scheduling aligns the existing Flip contact/slam key with the B-1 due tick
- no pre-contact death clone
- due death exit-owned tail
- StageResult UI publication barrier

## Tests
- core green: Core EditMode `92 total / 0 failed`; Core PlayMode `11 total / 0 failed`
- B-1 targeted/smoke green: 5 B-1 PlayMode smoke tests passed; current `FlipB1HostileImpactScenarioTests` cases passed, with only legacy characterization tests skipped
- full lane known baseline red: `4992 total / 34 failed`
- full PlayMode not run due EditMode failures
- manual UX result: closest PlayMode visual smoke pass; no hand-operated GUI pass

## Not Included
- ordinary Flip pure B-1
- global legacy AtContactTime removal, which remains out of scope after 8B because Push/non-B1 paths still use it
- full lane baseline fix
- StageResult authoritative commit change

## Risks
- manual UX judgment is based on automated PlayMode smoke rather than hand-operated viewing
- `0.12s` barrier may still need future subjective tuning
- adjacent watchlist remains outside B-1 scope
- future ordinary pure B-1 migration still has occupancy and presentation risks

## Final Recommendation
Merge under hostile-impact-only scope.

## 7. Test Gap Note
- Seeded `ScheduledFlipContact` tests are resolver coverage only.
- Actual input tests are required to verify action-start based timing.
- Previous tests missed the `ExecuteTick + 12` mistake because seeded tests bypassed `MovementExpander` schedule creation.

## 8. Legacy Cleanup TODO

| Legacy path | Still needed? | Used by | Removal condition | Required tests |
|---|---|---|---|---|
| legacy `AtContactTime` death VFX delay | Yes | Non-B1 death/contact-aligned VFX paths | All remaining non-B1 consumers migrate to explicit immediate/due timing or equivalent replacement | B-1 no-delay VFX coverage; non-B1 `AtContactTime` VFX preservation before migration |
| legacy `AtContactTime` death SFX delay | Yes | Non-B1 death/contact-aligned SFX paths | All remaining non-B1 consumers migrate to explicit immediate/due timing or equivalent replacement | B-1 no-delay SFX coverage; non-B1 `AtContactTime` SFX preservation before migration |
| retained-dead-view path | Yes, B-1 excluded | Legacy visual tails outside hostile B-1 | Ordinary Flip and other legacy death/contact paths no longer require pre-contact retained active views | B-1 no retained-dead-view; legacy retained behavior preserved until removed |
| post-contact exit-owned tail | Yes | Due death, non-B1 death tails, exit presentation ownership | Replacement tail ownership is proven for every remaining death/exit carrier | Due death exit-owned tail; no active duplicate view; tail lifecycle cleanup |
| cleanup generic hide suppression | Yes | Shared cleanup and duplicate-hide protection | No remaining path can emit duplicate cleanup/hide for presentation-owned exits | Duplicate exit/death suppression and cleanup no-double-hide tests |
| non-B1 Flip/Push impact presentation | Yes | Ordinary Flip success, legacy Flip impact presentation, Push/sliding Push impacts | Ordinary pure B-1 migration and Push/non-B1 impact replacement are separately specified and tested | Ordinary success unchanged; blocked Flip unchanged; Push/other impact unchanged; non-B1 `AtContactTime` preservation |

## 9. Ordinary Flip Pure B-1 Epic
- Keep ordinary success in-flight materialization in the deferred epic.
- Keep due-time landing requery in the deferred epic.
- Keep source/landing occupancy policy in the deferred epic.
- Keep empty landing occupied before due policy in the deferred epic.
- Keep blocked landing fallback in the deferred epic.
- Keep VFX/SFX double-play prevention in the deferred epic.
- Keep legacy non-B1 `AtContactTime` preservation tests in the deferred epic.
- Recommendation: defer until hostile B-1 is merged.

## 10. Deferred Work
- Manual visual smoke.
- Full lane baseline red recovery.
- Ordinary Flip pure B-1.
- Global legacy `AtContactTime` cleanup; B-1-specific timing cleanup is tracked by the 8B cleanup note.

## 11. Tests Run

| Command | Result | Failures |
|---|---|---|
| `./run_tests.sh core` | Passed. Core EditMode `92 total / 0 failed`; Core PlayMode `11 total / 0 failed`. | None |
| B-1 PlayMode smoke subset | Passed as part of `./run_tests.sh core`: `FlipB1_PlayMode_MovingEnemyDoesNotFreezeBeforeDue`, `DifferentEnemyEnteringContactCellGetsHit`, `OriginalEnemyMovedAwayDoesNotGetHit`, `DueDeathUsesExitOwnedTail`, `StageResultDoesNotCoverDueContact`. | None |
| `./run_tests.sh --integration-simulation` | Red overall: Unity integration-simulation EditMode `705 total / 2 failed`; B-1 scenario subset passed with current B-1 tests green and legacy characterization tests skipped. | `AttackPhaseScenarioTests.Attack_OnHit_DoesNotCreateSameTickNewIntent`; `AttackPhaseScenarioTests.Attack_OnHit_DoesNotReenterMovementPhase`. These are not B-1 related and are already represented in full-lane baseline context. |
| Adjacent watchlist individual tests | Not run as isolated tests; repo runner does not provide individual-test selection. Latest full artifact was inspected instead. | The three watchlist failures remain adjacent, not direct B-1 blockers. |
| `./run_tests.sh ui` | Not run. | Not required for the hostile Flip B-1 final confirmation because B-1 UI publication evidence is covered by core PlayMode smoke and StageResult barrier tests. |
| `./run_tests.sh full` | Not rerun. Latest artifact remains full EditMode known baseline red: `4992 total / 34 failed`; full PlayMode did not run because EditMode failed. | Full lane is known baseline red; rerunning it would not provide a merge-ready all-green claim and must not be reported as green. |

## 12. Final Recommendation
Merge under hostile-impact-only scope.
