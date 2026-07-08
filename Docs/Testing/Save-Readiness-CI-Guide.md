# Save Readiness CI Guide

## Scope

`CampaignProfileReadiness` is a diagnostics/readiness-only report for Save Architecture V2. It records the current campaign profile metadata state so CI can collect the markdown as evidence, but report findings do not block builds or releases.

This guide does not add a repository CI workflow. GitHub Actions or another CI system should be added only after CI ownership, runner environment, and artifact preservation behavior are explicitly decided in a later phase.

## Recommended Command

Use the existing filtered full lane:

```bash
./run_tests.sh full --filter CampaignProfileReadiness
```

Do not add a new `run_tests.sh` lane yet. Do not add a new GitHub Actions workflow yet.

Reason:

- No repo-defined CI workflow currently exists.
- External CI ownership is unknown.
- The broad unfiltered full lane has known baseline red and must not be used as this readiness gate.
- The filtered command runs the readiness report and policy coverage without treating unrelated full-lane failures as readiness regressions.

## Artifact Policy

Recommended artifact glob:

```text
TestLogs/SaveReadiness/**/CampaignProfileReadiness.md
```

Policy:

- The artifact is a CI upload target, not a source asset.
- The artifact must not be generated under `Assets/`.
- The artifact must not be generated under `Application.persistentDataPath` or any save root.
- The artifact must not be committed.
- `/TestLogs/SaveReadiness/` is ignored by Git.
- The current artifact test may delete the generated output after asserting its contract, so persistent artifact upload needs the CI command step to preserve the file or a follow-up artifact-preservation phase.

## Failure Policy

CI may fail for:

- Compile error.
- Test failure.
- Report writer contract violation.
- Invalid artifact path accepted by the writer.
- Forbidden wording.
- Forbidden runtime consumer.
- Report generation exception.

CI and release must not fail for valid diagnostics findings:

- Profile missing.
- Profile corrupt.
- Profile stale.
- `LastPlayedSlotNumber` mismatch.
- `importedSourceHash` mismatch.
- Reset tombstone present.
- Deleted guards present.
- Any valid diagnostics warning emitted by the readiness report.

## Non-Goals

This phase does not:

- Promote report warnings to release or build gates.
- Decide Steam Cloud upload/source file selection.
- Wire the report into MainMenu, Gameplay, DemoStageControl, runtime UI, or production composition.
- Change `SaveSlotStore()` default PlayerPrefs behavior.
- Enable production `profile.json` writes.
- Integrate `CampaignSaveServiceFactory` or `CampaignSaveService` into production flow.
- Add `.github/workflows` files.
- Add a new `run_tests.sh` lane.
