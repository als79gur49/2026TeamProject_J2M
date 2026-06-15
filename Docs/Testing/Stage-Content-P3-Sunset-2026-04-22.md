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
- removed runtime `defaultStageId` fallback detector/surface
- grandfather gameplay asset registry
- `gameplay.name-drift` exact known-warning rows
- StageId alias rows
- duplicate legacy gameplay asset copies

## Conscious Exceptions

- governed `StageIdAliasTable.asset` compatibility aliases
- empty `StageAliasGovernanceLedger.asset`
- empty `StageCatalogKnownWarningLedger.asset`
- stage-specific presentation support asset folders
- broad backlog and unrelated project-wide regression lanes

## Preserved Strengths

- production runtime path는 계속 `StageId -> StageCatalogResolver -> StageContentEntry -> GameplayDefinition/PresentationDefinition/AudioDefinition` 단일 경로다.
- catalog grouping/sort/default availability metadata는 `StageContentEntry`가 직접 소유하며 Progression companion이나 unlock-rule graph가 아니다.
- `StageRuntimeBuilder` / `StageRuntimeBuildResult`는 계속 gameplay-only 경계를 유지한다.
- presentation binding normalization owner는 `StagePresentationAssembler` /
  `StagePresentationBindingNormalizer`다. Enemy/static binding은 `EntityId`
  기준으로 정렬하고, TileFeature direct binding은 direct presentation resolve
  path에서 authored order를 보존한다.
- normalizer는 validation owner가 아니다. duplicate/missing/stale/catalog
  diagnostics는 `StageCatalogValidator`와 presentation binding integrity
  validator가 담당한다.
- production runtime에 compat mode를 다시 넣지 않았다.
- runtime `ResolveLegacy(...)`를 되돌리지 않았다.
- continue/retry는 계속 `StageNavigationRequest` / `StageId` 계약으로 동작한다.

## Finding 1 Hardening Note

- `StageRuntimeBuilder.cs` must not reference presentation binding/prefab/catalog
  tokens. `EnemyAiProfileOverride` export remains because it is gameplay seed
  data; the `Game.Feature.Gameplay.Host` namespace placement is a separate
  follow-up debt.
- The currently observed BoxSpawns literal mismatches in
  `StageRuntimeBuilderTests` are not part of the presentation-leakage fix. They
  are stage asset contract issues and should be classified separately before any
  test expected values or serialized stage assets are changed.

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
