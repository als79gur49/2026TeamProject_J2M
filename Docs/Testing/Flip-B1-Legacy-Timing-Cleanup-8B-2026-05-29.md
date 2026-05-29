# Flip B-1 Legacy Timing Cleanup 8B

## Scope
- This cleanup is documentation and test-suite hygiene only.
- No runtime gameplay behavior, `DueTick`, MotionTrack timing, audio/UI architecture, trace/export vocabulary, Push/non-B1 paths, or AttackPhase baseline behavior was changed.
- `AtContactTime`, `FlipImpactSignals`, and `FlipImpactPresentationSignal` remain global carriers because Push/non-B1 presentation still uses them.

## 8A Audit Basis
- B-1 due contact paths are covered by tests that assert `DueContactImmediate`, normalized contact time `0`, and no legacy `FlipImpactSignals` fallback on due facts.
- Non-B1 preservation is covered by `NonB1AtContactTimeCarrier_PreservesLegacyTimingModeAndContactFraction`.
- Ordinary Flip B-1 coverage now verifies execute-time `BoxInFlight`, due-time materialization / fallback / destroy outcomes, immediate terminal presentation, and no legacy `AtContactTime` or `FlipImpactSignals` use on ordinary B-1 due facts.

## Cleaned In 8B
- Removed ignored same-tick hostile Flip characterization tests that asserted obsolete `ImpactReservation`, same-tick damage/disposition, and legacy `FlipImpactSignals` behavior.
- Updated the current gameplay rules appendix so B-1 scope includes ordinary Flip success / landing and explicitly preserves Push/non-B1 legacy timing carriers.
- Marked earlier dated B-1 notes as superseded for ordinary-Flip and legacy-timing cleanup status while keeping them as historical evidence.
- Archived the superseded hostile-impact checkpoint notes under [Docs/Archive/Testing](../Archive/Testing/README.md); those notes are provenance only and no longer define current B-1 status.

## Intentionally Kept
- `EntityExitPresentationTiming.AtContactTime` and `GameplayPresentationTimingMode.AtContactTime`.
- `TickPresentationData.FlipImpactSignals` and `FlipImpactPresentationSignal`.
- Contact-delay planners in gameplay audio, enemy audio, Gameplay VFX, and enemy-death motion command builders.
- Push, sliding Push, non-hostile blocked Flip, non-B1 impact presentation, and global FlipImpact VFX/motion paths.

## Current Status
- Ordinary Flip B-1 cleanup is complete for the B-1-specific legacy timing scope: current ordinary B-1 due facts use `DueContactImmediate` and do not rely on legacy `AtContactTime`, `FlipImpactSignals`, or `ImpactReservation`.
- Global legacy timing cleanup is not complete and was intentionally not attempted in this 8B pass because Push/non-B1 paths still consume those carriers.

## Validation
| Command | Result | Failures |
|---|---|---|
| `git diff --check` | Passed | None |
| `./run_tests.sh core` | Passed. Core EditMode `94 total / 0 failed`; Core PlayMode `15 total / 0 failed`. | None |
| `./run_tests.sh --integration-replay` | Passed. Unity integration-replay EditMode `150 total / 0 failed`. | None |
| `./run_tests.sh --integration-simulation` | Red overall. Unity integration-simulation EditMode `737 total / 2 failed`. | `AttackPhaseScenarioTests.Attack_OnHit_DoesNotCreateSameTickNewIntent`; `AttackPhaseScenarioTests.Attack_OnHit_DoesNotReenterMovementPhase`. These are the known AttackPhase baseline failures and are not B-1-specific cleanup failures. |
