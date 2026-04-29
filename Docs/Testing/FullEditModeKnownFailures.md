# Full EditMode Known Failures

Captured: 2026-04-29

Source XML: `Temp/StageHardeningFullEditMode.after-provider.xml`

Official Unity path:
`TestRunnerCliBootstrap.RunEditMode` with `-codexSelection full`.
The direct Unity `-runTests` path was not the trusted artifact path for this
stage hardening pass.

## Baseline Capture Summary

- total: 1906
- passed: 1810
- failed: 95
- skipped: 1

Stages targeted result:

- `Game.Feature.Stages.Editor.Tests.dll`: 85/85 passed

Failed assembly breakdown:

- `Game.Feature.Gameplay.Tests.dll`: 61
- `Game.Integration.Replay.Tests.dll`: 8
- `Game.Integration.Simulation.Tests.dll`: 22
- `Game.TestInfrastructure.dll`: 4

The Stage Authoring hardening scope is green. The full suite remains red because
of existing non-stage-authoring failures captured in the JSON baseline.

Latest cleanup verification:

- XML: `Temp/StageAuthoringCleanupFullEditMode.xml`
- total: 1920
- passed: 1824
- failed: 95
- skipped: 1
- `Game.Feature.Stages.Editor.Tests.dll`: 99/99 passed
- baseline comparison: `PassedWithKnownFailures`
- known failures still failing: 95
- new failures: 0
- resolved known failures: 0
- StageAuthoring blocking failures: 0
- known failures with message hash drift: 6

## Baseline Files

- Machine-readable baseline:
  `Assets/_Features/Stages/Editor/Validation/Baselines/FullEditModeKnownFailures.json`
- Parser/comparison utility:
  `Assets/_Features/Stages/Editor/Validation/FullEditModeKnownFailureBaseline.cs`

The JSON baseline is generated from the XML artifact. Do not add placeholder
failures by hand. If the XML is unavailable, rerun the official bootstrap command
and regenerate the baseline from that artifact.

## Classification

The comparison utility splits a current Full EditMode XML into:

- known failures still failing
- new failures
- resolved known failures
- StageAuthoring blocking failures

A StageAuthoring failure is blocking even if its identity exists in the known
failure baseline. A full-suite failed count staying at 95 is not enough; failure
identity determines new/resolved failures, and message hash drift is reported
separately for known failures whose assertion text changed.

## Refresh Procedure

1. Run the official Full EditMode bootstrap and write a new XML under `Temp/`.
2. Compare it with `FullEditModeKnownFailures.json`.
3. Investigate all new failures and all StageAuthoring blocking failures.
4. Remove resolved failures from the baseline when intentionally accepting the
   new baseline.
5. Regenerate the JSON from the accepted XML and keep `capturedAt`, `sourceXml`,
   summary counts, assembly names, and failure message hashes in sync.
