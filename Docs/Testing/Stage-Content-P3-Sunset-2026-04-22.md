# Stage Content P3 Sunset 2026-04-22

이 문서는 Stage Content Layer Refactor P3 sunset의 bounded close evidence만 기록한다. production canonical path, gameplay-only builder/result boundary, canonical `StagePresentationDefinition` presentation source, `StageNavigationRequest` / `StageId` continue/retry contract는 유지했다. broad backlog, terrain/occupancy semantics, BGM playback wiring, full project-wide recovery는 이 close 범위에 넣지 않는다.

## Commands

```bash
./run_tests.sh core
StageCatalogCiValidationEntryPoint.Run
```

## Result

- `./run_tests.sh core`: green
- `StageCatalogCiValidationEntryPoint.Run`: green, `Temp/StageCatalogValidation/stage-catalog-validation.md`에 `Stage catalog CI validation passed.` 기록
- broad project-wide validation lane은 이 close note의 근거로 사용하지 않았다.

## Removed

- `LegacyStagePresentationEditorBridge`
- `StageCatalogEditorSeamValidator`
- runtime placeholder `LegacyStagePresentationBridge.cs`
- runtime `defaultStageId` fallback surface
- grandfather gameplay asset registry
- `gameplay.name-drift` exact known-warning rows
- StageId alias rows
- duplicate legacy gameplay asset copies

## Conscious Exceptions

- empty `StageIdAliasTable.asset`
- empty `StageAliasGovernanceLedger.asset`
- empty `StageCatalogKnownWarningLedger.asset`
- stage-specific presentation support asset folders
- broad backlog and unrelated project-wide regression lanes

## Preserved Strengths

- production runtime path는 계속 `StageId -> StageCatalogResolver -> StageContentEntry -> GameplayDefinition/PresentationDefinition` 단일 경로다.
- `StageRuntimeBuilder` / `StageRuntimeBuildResult`는 계속 gameplay-only 경계를 유지한다.
- production runtime에 compat mode를 다시 넣지 않았다.
- runtime `ResolveLegacy(...)`를 되돌리지 않았다.
- continue/retry는 계속 `StageNavigationRequest` / `StageId` 계약으로 동작한다.

## Reporting Wording

허용 claim:

- `P3 sunset validated`
- `core lane validated`
- `targeted architecture/CI validated`

금지 claim:

- `project-wide green`
- `broad green`
- `all stage-content regressions are closed`
- `full regression is closed`
