# Lane B Direct-Play Automated Validation 2026-04-22

## Scope
- Lane B hard adoption close를 위한 same-revision automated validation evidence만 정리한다.
- manual `Cycle 1` / `Cycle 2` smoke evidence는 이 note에 포함하지 않는다.
- broad backlog recovery나 `full-lane baseline recovered` claim은 다루지 않는다.

## Executed Commands
- `./run_tests.sh full`
- `python3 Tools/generate_post_stage_content_followup_artifacts.py --root /mnt/c/users/user/2026teamproject_j2m`

## Artifact List With Exact Dates
- `TestResults/wsl-unity-full-editmode.xml` (`2026-04-22 20:43:27 KST`)
- `Docs/Testing/Lane-A-Full-Baseline-Refreeze-2026-04-22.md` (`2026-04-22 20:43:50 KST`)
- `Docs/Testing/Lane-A-Live-Row-Ledger-2026-04-22.md` (`2026-04-22 20:43:50 KST`)
- `Docs/Testing/Lane-F-Claim-Vocabulary-Audit-2026-04-22.md` (`2026-04-22 20:43:50 KST`)

## Result Summary
- latest same-revision full XML result: `1248 total / 75 failed / 1172 passed`
- Lane B targeted validator/test rows inside the same full XML: `23 passed / 0 failed`
- docs/governance-specific failures for `StageCompatUsageReportingTests` and `UiGovernanceDocumentationTests`: `0`

## Targeted Validator And Test Summary
- `StageSceneBootstrapValidatorTests`: `1 passed / 0 failed`
- `StageDefaultStageIdPolicyTests`: `5 passed / 0 failed`
- `StageCompatUsageReportingTests`: `16 passed / 0 failed`
- `StageCatalogCiValidationEntryPointTests`: `1 passed / 0 failed`

## Catalog Coverage And Contract Proof
- `StageSceneBootstrapValidatorTests.ProductionScenes_UseCatalogResolvedBootstrapWithoutCompatResidue` passed.
- `StageDefaultStageIdPolicyTests.DirectPlayCatalog_ResolvesSupportedScenePaths` passed.
- `StageDefaultStageIdPolicyTests.Launcher_PrimesPendingStageIdForRegisteredScene` passed.
- `StageDefaultStageIdPolicyTests.Resolve_ConsumesPendingEditorDirectPlayStageId` passed.
- `StageCatalogCiValidationEntryPointTests.Run_WritesGovernanceAndAliasUsageValidationSections` passed.
- `StageCompatUsageReportingTests.DirectPlayContract_DocumentsLauncherOnlyWorkflow_AndRemovedRuntimeFallback` passed.
- `StageCompatUsageReportingTests.DirectPlayContract_RecordsOperationalMetrics_AndSoftHardAdoption` passed.
- `StageCompatUsageReportingTests.DirectPlayCycleTemplate_RecordsRequiredSceneFields_AndCounterSummary` passed.

## Allowed Claims
- same-revision automated validator/test evidence exists for Lane B operational adoption support.
- direct-play catalog coverage, launch-context-only contract, and reporting template assertions are green in the current full XML.

## Explicit Non-Claims
- this note does not claim Lane B hard adoption close
- this note does not replace manual `Cycle 1` / `Cycle 2` smoke evidence
- this note does not claim `full-lane baseline recovered`
- this note does not claim any broader project-wide green state
