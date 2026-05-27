# Flip B-1 Sprint 5 Hardening Report

Continuation note: Sprint 6 merge readiness is tracked in [Flip-B1-Sprint6-Merge-Readiness-Report-2026-05-28.md](./Flip-B1-Sprint6-Merge-Readiness-Report-2026-05-28.md). Sprint 5 remains the hardening evidence note; Sprint 6 separates hostile B-1 merge readiness from full-lane baseline red and ordinary Flip pure B-1 follow-up scope.

## 1. Summary
- Validated hostile Flip B-1 due contact behavior through automated PlayMode visual smoke using in-flight scheduled-contact fixtures that exercise the real `GameplaySceneHost`, presenter, view registry, due resolver, objective result, and presentation carriers.
- Added targeted barrier regressions for due-contact stage clear publication.
- Triaged the current full EditMode 34 failures from the pre-change full lane run. No failure is judged blocking for hostile Flip B-1.
- Not changed: ordinary Flip success migration, due resolver gameplay policy, StageResult/reward commit timing, legacy AtContactTime removal, presentation-authoritative state boundaries, target reservation, or enemy suppression.

## 2. PlayMode Visual Smoke
| Scenario | Expected | Actual | Pass/fail | Notes/screenshots |
|---|---|---|---|---|
| Moving enemy originally in contact cell, moves away before due | No pre-due damage/death/exit; enemy movement continues; due EmptyLand/FollowThrough; no death clone; box contact at due | Enemy moved on tick before due, stayed alive at moved cell, due signal resolved `EmptyLand`, box materialized at contact, no enemy damage/exit | Pass | Automated PlayMode assertion, no screenshot captured |
| Different hostile enters contact cell before due | Current hostile in contact cell is hit; original target not hit/exited | Original hostile moved away, new hostile spawned into contact cell, due signal hit new hostile id, original remained hp 3 | Pass | Automated PlayMode assertion |
| Hostile remains and dies at due | No pre-due damage/death; due hit/death; immediate exit-owned tail; no active double exposure | Pre-due tick had no enemy damage/exit, due tick emitted damage and enemy death exit with `DueContactImmediate`, enemy removed from snapshot, no duplicate active view | Pass | Automated PlayMode assertion |
| Hostile survives at due | Source box DestroySelf from due result; enemy survives; no death VFX/SFX carrier | Due signal hit hostile, enemy hp decremented and survived; no original death/exit | Pass | Existing B-1 scenario tests also cover DestroySelf box exit timing |
| Stage clear by due contact | Stage clear/reward commit same tick; StageResult frame publication waits barrier only; contact/death feedback not overwritten immediately | Due tick objective cleared same tick and carried immediate due contact/death facts. Barrier regression verifies due-contact clear registers minimum window; unrelated clear has no B-1 barrier | Pass | Automated PlayMode + EditMode barrier assertion |

## 3. Full Lane Failure Triage
Full lane evidence before Sprint 5 test additions: EditMode 4992 tests, 34 failed. PlayMode did not run because EditMode failed. B-1 interesting cases: 54 failed none. Baseline context: repository instructions document the full lane as red; failure set is outside the B-1 interesting group.

| Test | Area | Failure summary | B-1 related? | Evidence | Action |
|---|---|---|---|---|---|
| CampaignStageFlowTests.RespawnProcessor_PlayerRespawnGateSuppressesSpawnBeforeWrite | Campaign/respawn | Respawn gate expectation mismatch | No | Respawn/campaign path, no DueContactImmediate, scheduled contact, VFX/audio timing, or cleanup duplicate link | Baseline track outside B-1 |
| EnemyInactiveMaterialAuthoringTests.InactiveCompatibleDuplicates_RecordSourceAndPreserveMaterialValues | Enemy authoring/material | Inactive material duplicate metadata/value mismatch | No | Authoring/material validation only | Baseline authoring fix |
| FinalizeNoRecheckArchitectureTests.TickPipeline_CleanupRespawnDirectWritePath_IsDocumentedAndBounded | Architecture guard | Direct write documentation/bounds mismatch | No | Cleanup/respawn architecture guard, not hostile Flip due branch | Baseline architecture test |
| FlipArcSamplerTests.BoxFlipSlamSampler_LiftsOverPivotWithLateralArcThenSlamsTowardTarget | Flip visual sampler | Arc sampler numeric/path mismatch | Adjacent, not B-1 | Ordinary sampler visual math; B-1 Sprint 5 did not modify sampler | Keep as existing visual baseline |
| GameplayAudioOverlapRefactorTests.TileFeatureAudioCoalescer_MixedOnOff_MissingOneBurstBinding_OnlyThatKindFallsBack | Tile audio | Audio coalescer fallback mismatch | No | Tile feature audio, not due contact cue delay | Baseline audio |
| GameplayTickPresentationCoordinatorTests.EnemyJumpAirborneRestoreCannotMaskAdvancedAnimatorTime | Enemy jump presentation | Animator restore timing mismatch | No | Enemy jump/airborne coordinator, not B-1 due death/exit | Baseline presentation |
| GameplayTickPresentationCoordinatorTests.EnemyJumpAirborneResumeContinuesFromFrozenNormalizedTime | Enemy jump presentation | Animator normalized time resume mismatch | No | Enemy jump only | Baseline presentation |
| GameplayTickPresentationCoordinatorTests.EnemyJumpAirborneRotationFrameFinalAnimatorStateIsAirborne | Enemy jump presentation | Final animator state mismatch | No | Enemy jump topology rotation only | Baseline presentation |
| GameplayTickPresentationCoordinatorTests.EnemyJumpAirborneTopologyRotationDoesNotAdvanceAnimatorNormalizedTime | Enemy jump presentation | Animator time advanced unexpectedly | No | Enemy jump topology only | Baseline presentation |
| GameplayTickPresentationCoordinatorTests.EnemyJumpAirborneTopologyRotationSetsAnimatorPauseOnFirstFrame | Enemy jump presentation | Pause first-frame mismatch | No | Enemy jump topology only | Baseline presentation |
| GameplayTickPresentationCoordinatorTests.EnemyJumpAirborneTopologyTweenDoesNotReachExitTime | Enemy jump presentation | Tween exit time mismatch | No | Enemy jump topology only | Baseline presentation |
| GameplayTickPresentationCoordinatorTests.GravityFieldLockedTargetBoxPrefabs_AreAuthoredForLockableDimming | Gravity field/prefab | Prefab authoring mismatch | No | Gravity field prefab authoring | Baseline authoring |
| GameplayTimingOwnershipTests.GameplayHostPresentationFeed_StageClearVictoryDelay_DefersStageClearedFrame | StageResult timing | Victory delay frame publication mismatch | Adjacent, not B-1 | Same feed/barrier area, but test covers legacy StageClearVictoryDelay fixture, not due-contact barrier; new B-1 barrier tests pass in core | Baseline/adjacent; monitor |
| GameplayUiAccessRuntimeTests.GameplayUiAccess_PlayerHud_PushReadiness_ActionLock_DisablesPush | UIAccess HUD | Push readiness/action lock mismatch | No | UI HUD push readiness, no due contact | Baseline UIAccess |
| GameplayUiAccessRuntimeTests.GameplayUiAccess_PreRefreshTransientQueries_ReadPreviousCommittedHudState_BeforeTickCompletedRefresh | UIAccess HUD | Previous committed HUD state mismatch | No | Transient HUD query path | Baseline UIAccess |
| GameplayUiAccessRuntimeTests.GameplayUiAccess_PresentationFeed_MapsPlayerSlice_ForHeldMove | UIAccess presentation feed | Player slice mapping mismatch | No | Held move feed mapping, not stage result barrier | Baseline UIAccess |
| LegalityResultCanonicalizationTests.RuntimeTraversalLegalityPolicy_EvaluateDestination_WithRotation_ExportsTopologyUpdateRequirement | Legality/topology | Topology update requirement mismatch | No | Traversal legality, not B-1 settlement/due resolver | Baseline legality |
| ReservationReadModelContractTests.MovementReservationBook_CellReservationInfo_DistinguishesUnitSharedSettlementCompatibility | Reservation read model | Reservation compatibility mismatch | No | Reservation contract; Sprint 5 added no target reservation | Baseline reservation |
| TileFeatureAudioRuntimeTests.TileFeatureAudioPresentationController_BarricadeBindings_PlayDuplicatesWhenPresent | Tile audio | Duplicate binding playback mismatch | No | Barricade audio only | Baseline audio |
| TileFeatureAudioRuntimeTests.TileFeatureAudioPresentationController_ExitBindings_PlayDuplicatesWhenPresent | Tile audio | Exit duplicate binding playback mismatch | No | Tile exit audio, not enemy death/contact B-1 | Baseline audio |
| TileFeatureAudioRuntimeTests.TileFeatureAudioPresentationController_PlaybackPolicy_FallbackDuplicatesOrderAndCache | Tile audio | Playback fallback/order/cache mismatch | No | Tile feature playback policy | Baseline audio |
| TileFeatureVisualPresentationControllerTests.ButtonActivatedRequest_WithMotionContactTiming_WaitsUntilDelayElapses | Tile visual timing | Button activation timing mismatch | No | Tile button MotionContactTiming, not Flip due contact | Baseline visual timing |
| TileFeatureVisualPresentationControllerTests.DelayedExitOpenedRequest_BlocksImmediateOpenSyncUntilRequestPlays | Tile visual timing | Exit open delay mismatch | No | Tile exit visual request | Baseline visual timing |
| TopologyTransitionPostFxTests.ShowcaseCameraTopologyPresetAssets_UseAssetsDefaultVolumeProfileInsteadOfDeprecatedSettingsProfile | Topology post-fx authoring | Preset asset profile mismatch | No | Topology post-fx asset validation | Baseline authoring |
| WindupForwardCellProjectileTests.WindupForwardCellProjectile_CooldownBlocksRewindupUntilExpired | Projectile/windup | Cooldown rewindup mismatch | No | Projectile windup, no B-1 dependency | Baseline projectile |
| WorldStatePlacementInvariantTests.CreateSnapshot_DetachedBoxSharingUnitCell_RemainsMaterializableAndDoesNotBlockOccupancy | Board state placement | Detached box materialization/occupancy invariant mismatch | Adjacent, not B-1 | Board presence invariant can affect in-flight/detached semantics, but failing case is detached sharing unit cell, not scheduled flip contact | Baseline board-state monitor |
| StageAuthoringArchitectureBoundaryTests.StageResultUi_DoesNotDependOnStageRuntimeBuildResult | Stage authoring/UI boundary | Dependency boundary mismatch | No | Editor authoring boundary | Baseline stage authoring |
| StageAuthoringButtonObjectiveHelperCommandsTests.TryAddRequiredSecondaryGoal_DuplicateTileEntry_DoesNotAddAgain | Stage authoring/objective | Duplicate tile entry mismatch | No | Editor helper command | Baseline stage authoring |
| StageAuthoringExitGoalHelperCommandTests.Generation_SyncedAuthoringPassesValidatorAndPreservesVisualBindings | Stage authoring/exit | Generated authoring validation/bindings mismatch | No | Editor stage authoring | Baseline stage authoring |
| StageAuthoringExitGoalHelperCommandTests.ObjectiveHelper_RepairExitContract_GeneratedStageDefinitionValidates | Stage authoring/exit | Repair generated stage validation mismatch | No | Editor stage authoring | Baseline stage authoring |
| StageCatalogCiValidationEntryPointTests.Run_WritesGovernanceAndAliasUsageValidationSections | Stage catalog CI | Governance/alias section output mismatch | No | Editor catalog CI | Baseline catalog |
| StageCompatUsageReportingTests.KnownWarningLedger_MatchesCurrentCatalogWarningExactSet | Stage catalog compatibility | Warning ledger mismatch | No | Catalog warning ledger | Baseline catalog |
| AttackPhaseScenarioTests.Attack_OnHit_DoesNotCreateSameTickNewIntent | Attack phase scenario | On-hit same tick intent mismatch | No | Attack phase re-entry guard, not Flip due resolver | Baseline scenario |
| AttackPhaseScenarioTests.Attack_OnHit_DoesNotReenterMovementPhase | Attack phase scenario | Movement re-entry mismatch | No | Attack phase guard, not Flip scheduled contact | Baseline scenario |

## 4. Targeted Regression Results
- Added PlayMode smoke tests:
  - `FlipB1_PlayMode_MovingEnemyDoesNotFreezeBeforeDue`
  - `FlipB1_PlayMode_DueDeathUsesExitOwnedTail`
  - `FlipB1_PlayMode_StageResultDoesNotCoverDueContact`
  - `FlipB1_PlayMode_DifferentEnemyEnteringContactCellGetsHit`
  - `FlipB1_PlayMode_OriginalEnemyMovedAwayDoesNotGetHit`
- Added presentation/UI barrier tests:
  - `GameplayPresentationBarrierTracker_B1DueContactStageClear_UsesMinimumVisibilityWindow`
  - `GameplayPresentationBarrierTracker_UnrelatedStageClear_DoesNotAddB1DueContactBarrier`
- Existing targeted coverage already present from prior sprints covers DueContactImmediate timing, B-1 VFX/audio delay 0, no semantic double-play, same-tick objective commit, reward idempotence, DestroySelf due result, and legacy non-B1 AtContactTime behavior.
- Tests run: `./run_tests.sh core`
- Result: passed. EditMode 92/92, PlayMode 10/10.

## 5. StageResult Barrier Evaluation
Current value: `FlipB1DueContactStageClearBarrierSeconds = 0.12f`.

| Barrier value | Pros | Cons | Recommendation |
|---|---|---|---|
| 0.12s | Minimal latency; enough for due contact/death/exit facts to publish and start; avoids waiting for full death dissolve | Tight visual window on slow displays or heavy VFX scenes | Keep for current scope |
| 0.18s | More readable contact/death onset window | Adds noticeable result delay and is not required by automated smoke | Candidate only if manual UX review finds 0.12 too abrupt |
| 0.20s | Safest readability of first impact frames | Highest perceived delay; risks making StageResult feel sluggish | Defer unless explicit UX requirement appears |

Recommendation: keep 0.12s. It matches the preferred policy: wait for due contact fact consumption, box disposition submission/start, enemy exit ownership transfer, and a minimum visibility window; do not wait for full death dissolve.

## 6. Legacy AtContactTime Cleanup Plan
| Legacy path | Still needed? | Used by | Removal condition | Tests required |
|---|---|---|---|---|
| Legacy `AtContactTime` presentation timing mode | Yes | Non-B1 Flip/Push/Impact contact-aligned VFX/SFX | Ordinary Flip success and all legacy contact carriers migrated to pure due/immediate timing or explicit replacement | Non-B1 AtContactTime still works before migration; migrated paths prove no delay dependency |
| Pre-contact retained-dead-view allowance | Yes, B-1 excluded only | Legacy death/contact tails outside hostile B-1 | All ordinary Flip success death/contact visuals become exit-owned or equivalent | Retained-dead-view absent for B-1; retained behavior preserved for legacy paths |
| Cleanup duplicate exit/death guards | Yes | Shared cleanup and enemy exit ownership paths | Duplicate cleanup emission no longer possible from any remaining legacy branch | Death/exit no duplicate active view; cleanup duplicate suppression tests |
| Legacy contact VFX/SFX delay planners | Yes | Ordinary contact presentation, tile feature contact timing, non-B1 impact | B-1-only DueContactImmediate remains isolated and ordinary migration has replacement timing | Contact SFX/VFX delay 0 for B-1; AtContactTime delay for legacy |
| StageResult victory delay fixture | Yes | Generic StageResult feed tests | Dedicated due-contact barrier and generic victory delay semantics are separated and stable | Due-contact clear waits; unrelated clear does not wait; legacy victory delay remains covered |

Safe cleanup sequence:
1. Keep legacy `AtContactTime` and retained-dead-view paths unchanged now.
2. Add ordinary Flip pure B-1 design tests before touching runtime policy.
3. Migrate ordinary Flip success to in-flight materialization only after source/landing occupancy and cancellation policy are specified.
4. Remove legacy paths only after non-B1 Push/Impact/Flip tests prove no remaining consumer.

## 7. Ordinary Flip Pure B-1 Decision Memo
- Current status: hostile Flip B-1 is isolated to hostile impact due resolution with scheduled contact, current snapshot requery, immediate presentation carriers, and StageResult barrier.
- Migration risks: ordinary Flip success currently materializes through existing movement/contact presentation. Pure B-1 would change source cell occupancy, landing cell timing, cancellation behavior, view ownership, and legacy contact timing assumptions.
- Required changes: materialize ordinary success as in-flight source box, define due-time landing settlement requery, define cancellation/fallback for blocked landing, ensure box view motion/tail ownership, and separate ordinary success from hostile impact damage policy.
- Source/landing occupancy policy: source box should be in-flight between execute and due; landing cell should be revalidated at due; blocked landing needs explicit SafeReturn/Destroy/Cancel semantics before implementation.
- Tests needed: ordinary success in-flight materialization, landing cell becomes occupied only at due, original target movement does not corrupt landing, blocked landing fallback, non-B1 AtContactTime preservation, VFX/audio no double-play, StageResult unrelated clear no barrier.
- Recommendation: defer. Proceed next only with a dedicated migration sprint and test-first policy.

## 8. Final Readiness Judgment
Hostile Flip B-1 ready, but ordinary pure B-1 deferred.

## 9. Tests Run
- `./run_tests.sh full`
  - Result before Sprint 5 implementation: failed in Full EditMode, 4992 total / 34 failed. Full PlayMode did not run because EditMode failed. B-1 interesting cases had no failures.
- `./run_tests.sh core`
  - First run after initial PlayMode additions: failed PlayMode smoke helper assumptions.
  - Final run after fixture correction: passed. Core EditMode 92/92, Core PlayMode 10/10.
- Known unrelated failures: the 34 full EditMode failures listed in section 3.
