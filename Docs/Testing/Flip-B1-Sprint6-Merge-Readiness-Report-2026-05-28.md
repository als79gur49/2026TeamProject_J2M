# Flip B-1 Sprint 6 Merge Readiness Report

## 1. Summary
- Readiness judgment: Hostile Flip B-1 is ready under the current hostile-impact-only scope, but still requires manual UX confirmation before making a final release-polish claim.
- Scope: hostile impact Flip only. The B-1 path uses `BoxInFlight`, `ScheduledFlipContact`, due tick current snapshot requery, and `DueContactImmediate` presentation markers.
- What was not changed: ordinary Flip success, non-hostile blocked Flip, Push/other impact policy, due resolver gameplay policy, StageResult/reward authoritative commit timing, legacy `AtContactTime` removal, target reservation, and enemy suppression.
- Merge note: full lane remains known baseline red. Do not describe this sprint as full green or project-wide green.

## 2. Merge Readiness Checklist
| Item | Status | Evidence |
|---|---|---|
| hostile impact execute tick no damage/death/removal | Pass | `FlipB1HostileImpact_ExecuteTick_DoesNotDamageTarget`, `DoesNotMarkTargetForDeath`, `DoesNotRemoveTarget` |
| source box InFlight + ScheduledFlipContact | Pass | `FlipB1HostileImpact_ExecuteTick_SourceBoxBecomesInFlight`, `SchedulesContact`, B-1 foundation tests |
| due tick current snapshot requery | Pass | `FlipB1HostileImpact_DueTick_RequeriesCurrentSnapshot` and current-occupant PlayMode smoke |
| original target moved away -> not hit | Pass | `FlipB1HostileImpact_DueTick_OriginalHostileMovedAway_EmptyLand`, `FlipB1_PlayMode_OriginalEnemyMovedAwayDoesNotGetHit` |
| different hostile entered -> current occupant hit | Pass | `FlipB1HostileImpact_DueTick_RequeriesCurrentSnapshot`, `FlipB1_PlayMode_DifferentEnemyEnteringContactCellGetsHit` |
| due death -> exit-owned tail | Pass | `FlipB1Presentation_DueDamageAndDeathUseImmediateExitOwnedTail`, `FlipB1_PlayMode_DueDeathUsesExitOwnedTail` |
| pre-contact death clone absent | Pass | `FlipB1Presentation_BeforeDue_NoEnemyDeathCloneOrRetainedDeadFacts` |
| DueContactImmediate no legacy AtContactTime delay | Pass | due contact, VFX, SFX, and death exit assertions use `DueContactImmediate` and normalized contact time `0` |
| StageResult barrier applies only for B-1 due contact stage clear | Pass | `GameplayPresentationBarrierTracker_B1DueContactStageClear_UsesMinimumVisibilityWindow`, `UnrelatedStageClear_DoesNotAddB1DueContactBarrier` |
| ordinary Flip success unchanged | Pass | `FlipB1HostileImpact_OrdinarySuccess_RemainsSameTickMaterialize_InPhase1` |
| non-hostile blocked Flip unchanged | Pass | `FlipB1HostileImpact_FriendlyBlockedFlip_DoesNotUseB1HostilePath` |
| push/other impact unchanged | Pass | Sprint 6 docs-only pass adds no Push/other runtime changes; current changed files remain hostile B-1 scoped |
| core green | Pass | Sprint 6 rerun `./run_tests.sh core`: Core EditMode `92/92`, Core PlayMode `10/10` |
| B-1 targeted green | Pass | B-1 core foundation + five PlayMode visual smoke tests are green in the Sprint 6 core rerun |
| full lane red documented as baseline/non-blocking | Pass | Full EditMode `4992 total / 34 failed`; Full PlayMode not run; no B-1 interesting failures |

## 3. Full Lane Failure Triage
Current full artifact: `TestResults/wsl-unity-full-editmode.xml`, start `2026-05-27 15:11:26Z`, EditMode `4992 total / 34 failed`. Full PlayMode did not run because EditMode failed. No B-1 interesting case failed and no B-1 blocking failure was identified.

| Test | Area | Failure summary | B-1 related? | Evidence | Action |
|---|---|---|---|---|---|
| CampaignStageFlowTests.RespawnProcessor_PlayerRespawnGateSuppressesSpawnBeforeWrite | Campaign/respawn | Respawn gate expectation mismatch | No | Respawn/campaign path, no scheduled Flip contact or due presentation marker | Baseline track outside B-1 |
| EnemyInactiveMaterialAuthoringTests.InactiveCompatibleDuplicates_RecordSourceAndPreserveMaterialValues | Enemy authoring/material | Inactive duplicate material does not match source candidate | No | Asset authoring validation only | Baseline authoring owner |
| FinalizeNoRecheckArchitectureTests.TickPipeline_CleanupRespawnDirectWritePath_IsDocumentedAndBounded | Architecture guard | Direct write count expected `2`, actual `3` | No | Cleanup/respawn architecture guard, not hostile due branch | Baseline architecture owner |
| FlipArcSamplerTests.BoxFlipSlamSampler_LiftsOverPivotWithLateralArcThenSlamsTowardTarget | Flip visual sampler | Slam midpoint below expected threshold | Adjacent, not B-1 | Visual sampler math; B-1 did not change sampler | Watchlist; no Sprint 6 fix |
| GameplayAudioOverlapRefactorTests.TileFeatureAudioCoalescer_MixedOnOff_MissingOneBurstBinding_OnlyThatKindFallsBack | Tile audio | Expected same audio definition instance | No | Tile audio binding identity, not due contact cue timing | Baseline audio owner |
| GameplayTickPresentationCoordinatorTests.EnemyJumpAirborneRestoreCannotMaskAdvancedAnimatorTime | Enemy jump presentation | Animator hash/time expectation mismatch | No | Enemy jump/airborne only | Baseline presentation owner |
| GameplayTickPresentationCoordinatorTests.EnemyJumpAirborneResumeContinuesFromFrozenNormalizedTime | Enemy jump presentation | Animator hash/time expectation mismatch | No | Enemy jump/airborne only | Baseline presentation owner |
| GameplayTickPresentationCoordinatorTests.EnemyJumpAirborneRotationFrameFinalAnimatorStateIsAirborne | Enemy jump presentation | Final animator state mismatch | No | Enemy jump topology rotation only | Baseline presentation owner |
| GameplayTickPresentationCoordinatorTests.EnemyJumpAirborneTopologyRotationDoesNotAdvanceAnimatorNormalizedTime | Enemy jump presentation | Animator normalized time advanced unexpectedly | No | Enemy jump topology rotation only | Baseline presentation owner |
| GameplayTickPresentationCoordinatorTests.EnemyJumpAirborneTopologyRotationSetsAnimatorPauseOnFirstFrame | Enemy jump presentation | Pause first-frame mismatch | No | Enemy jump topology rotation only | Baseline presentation owner |
| GameplayTickPresentationCoordinatorTests.EnemyJumpAirborneTopologyTweenDoesNotReachExitTime | Enemy jump presentation | Tween exit timing mismatch | No | Enemy jump topology tween only | Baseline presentation owner |
| GameplayTickPresentationCoordinatorTests.GravityFieldLockedTargetBoxPrefabs_AreAuthoredForLockableDimming | Gravity field/prefab | MoonBox material uses unexpected shader/material | No | Gravity field prefab authoring | Baseline asset owner |
| GameplayTimingOwnershipTests.GameplayHostPresentationFeed_StageClearVictoryDelay_DefersStageClearedFrame | StageResult timing | Generic victory delay fixture throws stage completion StageId error | Adjacent, not B-1 | Same feed area, but generic victory delay; B-1 barrier tests pass | Watchlist; no Sprint 6 fix |
| GameplayUiAccessRuntimeTests.GameplayUiAccess_PlayerHud_PushReadiness_ActionLock_DisablesPush | UIAccess HUD | Push readiness/action lock expectation mismatch | No | HUD push readiness, no due contact | Baseline UIAccess owner |
| GameplayUiAccessRuntimeTests.GameplayUiAccess_PreRefreshTransientQueries_ReadPreviousCommittedHudState_BeforeTickCompletedRefresh | UIAccess HUD | Previous committed HUD state count missing | No | Transient HUD query path | Baseline UIAccess owner |
| GameplayUiAccessRuntimeTests.GameplayUiAccess_PresentationFeed_MapsPlayerSlice_ForHeldMove | UIAccess presentation feed | Held move player slice mapping false | No | Held move feed, not StageResult barrier | Baseline UIAccess owner |
| LegalityResultCanonicalizationTests.RuntimeTraversalLegalityPolicy_EvaluateDestination_WithRotation_ExportsTopologyUpdateRequirement | Legality/topology | Expected `Allowed`, actual `Blocked` | No | Traversal legality, not B-1 due resolver | Baseline topology owner |
| ReservationReadModelContractTests.MovementReservationBook_CellReservationInfo_DistinguishesUnitSharedSettlementCompatibility | Reservation read model | Expected blocker `Unit`, actual `None` | No | Reservation read model; Sprint 6 adds no reservation | Baseline occupancy owner |
| TileFeatureAudioRuntimeTests.TileFeatureAudioPresentationController_BarricadeBindings_PlayDuplicatesWhenPresent | Tile audio | Expected same `Sfx_False_Def` instance | No | Barricade audio only | Baseline audio owner |
| TileFeatureAudioRuntimeTests.TileFeatureAudioPresentationController_ExitBindings_PlayDuplicatesWhenPresent | Tile audio | Expected same `Sfx_False_Def` instance | No | Exit tile audio, not B-1 death/contact | Baseline audio owner |
| TileFeatureAudioRuntimeTests.TileFeatureAudioPresentationController_PlaybackPolicy_FallbackDuplicatesOrderAndCache | Tile audio | Fallback duplicate ordering/cache mismatch | No | Tile feature playback policy | Baseline audio owner |
| TileFeatureVisualPresentationControllerTests.ButtonActivatedRequest_WithMotionContactTiming_WaitsUntilDelayElapses | Tile visual timing | Expected one delayed request, actual zero | No | Tile button motion-contact timing, not Flip due contact | Baseline visual owner |
| TileFeatureVisualPresentationControllerTests.DelayedExitOpenedRequest_BlocksImmediateOpenSyncUntilRequestPlays | Tile visual timing | Expected one delayed request, actual zero | No | Tile exit-open request timing | Baseline visual owner |
| TopologyTransitionPostFxTests.ShowcaseCameraTopologyPresetAssets_UseAssetsDefaultVolumeProfileInsteadOfDeprecatedSettingsProfile | Topology post-fx authoring | Expected volume profile GUID missing | No | Topology asset validation | Baseline asset owner |
| WindupForwardCellProjectileTests.WindupForwardCellProjectile_CooldownBlocksRewindupUntilExpired | Projectile/windup | Expected `ForwardCellProjectile`, actual `None` | No | Enemy projectile/windup, no B-1 dependency | Baseline enemy AI owner |
| WorldStatePlacementInvariantTests.CreateSnapshot_DetachedBoxSharingUnitCell_RemainsMaterializableAndDoesNotBlockOccupancy | Board state placement | Detached box sharing unit cell rejected by placement invariant | Adjacent, not B-1 | Occupancy invariant can monitor in-flight semantics, but failing case is detached sharing unit cell | Watchlist; no Sprint 6 fix |
| StageAuthoringArchitectureBoundaryTests.StageResultUi_DoesNotDependOnStageRuntimeBuildResult | Stage authoring/UI boundary | UI test file references `StageRuntimeBuildResult` | No | Editor boundary guard, not B-1 barrier | Baseline stage/UI owner |
| StageAuthoringButtonObjectiveHelperCommandsTests.TryAddRequiredSecondaryGoal_DuplicateTileEntry_DoesNotAddAgain | Stage authoring/objective | Duplicate tile entry add expectation mismatch | No | Editor helper command | Baseline stage authoring owner |
| StageAuthoringExitGoalHelperCommandTests.Generation_SyncedAuthoringPassesValidatorAndPreservesVisualBindings | Stage authoring/exit | Generated Exit uses invalid activation rule | No | Editor generated stage validation | Baseline stage authoring owner |
| StageAuthoringExitGoalHelperCommandTests.ObjectiveHelper_RepairExitContract_GeneratedStageDefinitionValidates | Stage authoring/exit | Repaired Exit uses invalid activation rule | No | Editor repair helper validation | Baseline stage authoring owner |
| StageCatalogCiValidationEntryPointTests.Run_WritesGovernanceAndAliasUsageValidationSections | Stage catalog CI | Expected CI result `0`, actual `1` | No | Stage catalog governance | Baseline catalog owner |
| StageCompatUsageReportingTests.KnownWarningLedger_MatchesCurrentCatalogWarningExactSet | Stage catalog compatibility | Expected empty known warning set, actual warnings | No | Catalog warning ledger | Baseline catalog owner |
| AttackPhaseScenarioTests.Attack_OnHit_DoesNotCreateSameTickNewIntent | Attack phase scenario | Expected one intent, actual two | No | Attack phase re-entry, not Flip scheduled contact | Baseline tick owner |
| AttackPhaseScenarioTests.Attack_OnHit_DoesNotReenterMovementPhase | Attack phase scenario | Expected one movement phase entry, actual two | No | Attack phase guard, not Flip due resolver | Baseline tick owner |

Adjacent watchlist analysis:

| Test | Directly connected to B-1 changed files? | Present before Sprint 5? | Related possibility | Additional regression needed? |
|---|---|---|---|---|
| `GameplayHostPresentationFeed_StageClearVictoryDelay_DefersStageClearedFrame` | Indirect only; Sprint 5 touched `GameplayHostPresentationFeed`, but this row uses generic victory delay | Yes, listed in `Full-Lane-Baseline-P2-4-2026-05-26.md` | Possible StageResult publication adjacency, but not `DueContactImmediate`; B-1 barrier tests pass | No immediate B-1 regression; keep generic fixture repair separate |
| `WorldStatePlacementInvariantTests.CreateSnapshot_DetachedBoxSharingUnitCell_RemainsMaterializableAndDoesNotBlockOccupancy` | Indirect only through board presence/occupancy vocabulary | Yes, listed in 2026-04-22 handoff and 2026-05-26 baseline | Low-to-medium monitor for `BoxInFlight`, but failure is detached box sharing unit cell, not scheduled contact | No immediate B-1 regression; keep occupancy owner follow-up |
| `FlipArcSamplerTests.BoxFlipSlamSampler_LiftsOverPivotWithLateralArcThenSlamsTowardTarget` | No direct runtime policy link; B-1 did not change sampler | Yes, listed in `Full-Lane-Baseline-P2-4-2026-05-26.md` | Visual adjacency only for Flip presentation; not due resolver or StageResult barrier | No immediate B-1 regression; manual UX smoke should watch contact readability |

## 4. Manual UX Smoke Checklist
Manual UX pass status: pending. Automated PlayMode smoke covers the five scenario facts, but this report does not claim manual visual approval.

| Scenario | no enemy freeze before due | no death VFX/SFX before due | contact VFX/SFX timing natural | death exit-owned tail natural | box destroy/rebound/follow-through natural | StageResult not too fast |
|---|---|---|---|---|---|---|
| moving enemy moves away before due | Automated pass; manual pending | Automated pass; manual pending | Manual pending | n/a | Automated pass; manual pending | n/a |
| different hostile enters contact cell | Automated pass; manual pending | Automated pass; manual pending | Manual pending | n/a | Manual pending | n/a |
| hostile remains and dies | Automated pass; manual pending | Automated pass; manual pending | Manual pending | Manual pending | Manual pending | n/a |
| hostile survives and source box DestroySelf | Automated pass; manual pending | Automated pass; manual pending | Manual pending | n/a | Manual pending | n/a |
| due contact clears stage | Automated pass; manual pending | Automated pass; manual pending | Manual pending | Manual pending | Manual pending | Manual pending |

Barrier recommendation:

| Barrier | Recommendation |
|---|---|
| `0.12s` | Keep for current scope. It delays StageResult publication enough to start due contact/death/exit feedback without delaying authoritative stage/reward commit. |
| `0.18s` | Compare only if manual UX finds the first contact frames too abrupt. |
| `0.20s` | Compare only if readability is still poor; this risks making result presentation feel sluggish. |

## 5. Documentation Updates
Files changed for Sprint 6:
- `Docs/Testing/Flip-B1-Sprint6-Merge-Readiness-Report-2026-05-28.md`
- `Docs/Testing/Flip-B1-Sprint5-Hardening-Report-2026-05-28.md`
- `Docs/Architecture/Gameplay-Rules-Appendix.md`

Architecture notes:
- B-1 applies only to hostile impact Flip.
- Ordinary Flip success remains same-tick materialization.
- No target reservation and no enemy suppression are introduced.
- Hostile impact uses `BoxInFlight + ScheduledFlipContact`, then due tick current snapshot requery.
- `DueContactImmediate` is the B-1 due contact presentation timing marker.
- Legacy `AtContactTime` remains for non-B1 paths.
- StageResult barrier delays only UI publication; reward/stage authoritative commit is not delayed.

## 6. Legacy Cleanup Plan
| Legacy path | Still needed? | Used by | Removal condition | Required tests |
|---|---|---|---|---|
| legacy `AtContactTime` death VFX delay | Yes | Non-B1 death/contact-aligned VFX paths | All remaining non-B1 consumers migrate to explicit immediate/due timing or an equivalent replacement | B-1 no-delay coverage; non-B1 `AtContactTime` VFX preservation before migration |
| legacy `AtContactTime` death SFX delay | Yes | Non-B1 death/contact-aligned SFX paths | All remaining non-B1 consumers migrate to explicit immediate/due timing or an equivalent replacement | B-1 no-delay SFX coverage; non-B1 `AtContactTime` SFX preservation before migration |
| retained-dead-view path | Yes, B-1 excluded | Legacy visual tails outside hostile B-1 | Ordinary Flip and other legacy death/contact paths no longer require pre-contact retained active views | B-1 no retained-dead-view; legacy retained behavior preserved until removed |
| post-contact exit-owned tail | Yes | Due death, non-B1 death tails, exit presentation ownership | Replacement tail ownership is proven for every remaining death/exit carrier | Due death exit-owned tail, no active duplicate view, tail lifecycle cleanup |
| cleanup generic hide suppression | Yes | Shared cleanup and duplicate-hide protection | No remaining path can emit duplicate cleanup/hide for a presentation-owned exit | Duplicate exit/death suppression and cleanup no-double-hide tests |
| non-B1 Flip/Push impact presentation | Yes | Ordinary Flip success, legacy Flip impact presentation, Push/sliding Push impacts | Ordinary pure B-1 migration and Push/non-B1 impact replacement are separately specified and tested | Ordinary success unchanged, blocked Flip unchanged, Push/other impact unchanged, non-B1 `AtContactTime` preservation |

## 7. Ordinary Flip Pure B-1 Epic
Recommendation: defer until hostile Flip B-1 is merged and monitored.

Required work:
- ordinary success in-flight materialization for the source box between execute and due
- due-time landing requery using the current snapshot
- explicit source/landing occupancy policy while the box is in flight
- policy for an empty landing becoming occupied before due
- blocked landing fallback semantics, including return, destroy, or cancel behavior
- VFX/SFX double-play prevention across legacy and pure B-1 carriers
- non-B1 `AtContactTime` preservation tests before migration
- migration risk review for view ownership, settlement legality, cleanup ordering, and replay/determinism traces

## 8. Tests Run
| Command | Result | Failures |
|---|---|---|
| `./run_tests.sh core` | Passed: Core EditMode `92 total / 0 failed`; Core PlayMode `10 total / 0 failed` | None |
| B-1 targeted tests | Passed as part of core: B-1 foundation coverage plus hostile due-contact PlayMode smoke | None |
| PlayMode smoke subset | Passed as part of core PlayMode: five B-1 visual smoke tests | None |
| `./run_tests.sh full` | Not rerun for Sprint 6 docs-only pass; current artifact is known baseline red | Current full EditMode `4992 total / 34 failed`; Full PlayMode not run |

## 9. Final Judgment
Hostile Flip B-1 ready, but requires manual UX confirmation.
