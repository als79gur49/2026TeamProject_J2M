# Stage Content P2 Close Hardening 2026-04-22

이 문서는 Stage Content Layer Refactor P2 close hardening의 bounded close evidence만 기록한다. production runtime canonical path, runtime compat removal, gameplay-only builder/result boundary, canonical `StagePresentationDefinition` presentation source, `StageNavigationRequest` / `StageId` continue/retry contract는 이 단계에서 유지했다. terrain/occupancy semantics, BGM playback wiring, broad backlog recovery는 이번 close 범위에 넣지 않는다.

## Scope

- `LegacyStagePresentationBridge`를 editor-only seam으로 분리하고 runtime validator 책임과 editor seam validator 책임을 분리한다.
- grandfather `gameplay.name-drift` warning 2건을 exact known-warning set으로 잠근다.
- `defaultStageId`를 editor direct-play fallback으로만 표현하는 explicit policy/factory guard를 고정한다.
- alias table과 alias governance ledger를 exact-sync governance로 묶는다.
- P2 close evidence와 broad project-wide backlog를 분리한다.

## Commands

```bash
./run_tests.sh core
"/mnt/c/Users/user/Desktop/6000.3.11f1/Editor/Unity.exe" -batchmode -projectPath "C:\Users\user\2026TeamProject_J2M" -quit -executeMethod "Game.Feature.Stages.Editor.StageCatalogCiValidationEntryPoint.Run" -logFile "C:\Users\user\2026TeamProject_J2M\TestResults\stage-catalog-ci.log"
```

## Result

- `./run_tests.sh core`: green, Core EditMode `13 total / 0 failed`, Core PlayMode `2 total / 0 failed`
- `StageCatalogCiValidationEntryPoint.Run`: green, `TestResults/stage-catalog-ci.log`에 `Stage catalog CI validation passed.` 기록
- `./run_tests.sh full`: 이 close note의 근거로 실행하지 않았다.

## Hardening Delta

- runtime assembly는 더 이상 legacy presentation bridge symbol을 계약 surface로 사용하지 않는다.
- `LegacyStagePresentationEditorBridge`와 `StageCatalogEditorSeamValidator`가 editor asmdef에서 seam comparison을 담당한다.
- `StageCatalogKnownWarningLedger.asset`는 exact `gameplay.name-drift` 2건만 허용한다.
- `StageAliasGovernanceLedger.asset`와 `StageAliasGovernanceUpdater`가 alias table mutation을 governance metadata와 함께 묶는다.
- `StageLoadRequest`는 `StageLoadFallbackPolicy.None`과 `StageLoadFallbackPolicy.EditorDirectPlayOnly`만 허용한다.

## Preserved Strengths

- production runtime canonical path는 계속 `StageId -> StageCatalogResolver -> StageContentEntry -> GameplayDefinition/PresentationDefinition` 단일 경로다.
- runtime compat mode와 runtime `ResolveLegacy(...)`는 부활하지 않았다.
- `StageRuntimeBuilder` / `StageRuntimeBuildResult` gameplay-only 경계는 유지된다.
- production presentation source는 canonical `StagePresentationDefinition`만 사용한다.
- continue/retry는 계속 `StageNavigationRequest` / `StageId` 계약으로 동작한다.

## Exact Known-Warning Set

현재 허용 warning은 정확히 아래 두 row뿐이다.

| IssueCode | AssetGuid | ExpectedAssetPath | ExpectedAssetName | ExpectedStageId | RemovalGate |
| --- | --- | --- | --- | --- | --- |
| `gameplay.name-drift` | `768e58af510a487eafd9bf00b45b4ca0` | `Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Stage_CombinedGameplayShowcase.asset` | `Stage_CombinedGameplayShowcase` | `combined-gameplay-showcase` | `P3-B grandfather canonicalization` |
| `gameplay.name-drift` | `17f422e552184a24d8a666d77d93ab18` | `Assets/_Features/Stages/Stage_TutorialScene/Stage_TutorialSecne.asset` | `Stage_TutorialSecne` | `tutorial-scene` | `P3-B grandfather canonicalization` |

규칙:

- warning이 해소되면 같은 변경에서 ledger row를 제거한다.
- asset path만 바뀌고 GUID가 유지되면 ledger path만 같은 변경에서 갱신한다.
- GUID가 바뀌면서 warning이 유지되면 grandfather registry와 known-warning ledger를 같은 변경에서 함께 갱신한다.
- 새 known-warning row 추가는 P2 hardening 범위에서 허용하지 않는다.

## Alias Governance

- runtime source-of-truth는 계속 `StageIdAliasTable.asset`이다.
- editor governance source-of-truth는 `StageAliasGovernanceLedger.asset`이다.
- alias mutation은 `StageCatalogMigrationTool`, `StageIdRenameTool`, `StageAliasGovernanceUpdater` 경로로만 허용한다.
- CI는 alias exact-set sync, metadata completeness, approved mutation surface만 허용한다.

## defaultStageId Contract

- `defaultStageId`는 production runtime fallback이 아니다.
- `StageLoadRequest.CreateLaunchContextOnly(...)`가 canonical runtime path다.
- `StageLoadRequest.CreateEditorDirectPlayFallback(...)`는 editor direct-play/test fallback에만 사용한다.
- `StageRuntimeContentResolver`는 launch context가 없고 `Application.isEditor == false`인 경우 fallback을 즉시 거부한다.

## Reporting Wording

허용 claim:

- `P2 close validated`
- `core lane validated`
- `targeted architecture/CI validated`

금지 claim:

- `project-wide green`
- `broad green`
- `all stage-content regressions are closed`
- `full regression is closed`

## P3 Entry Gates

- `P3-A bridge sunset`
  - non-canonical gameplay asset의 legacy `PresentationId` residue가 `0`
  - editor seam validator mismatch가 `0`
  - migration tool이 bridge 없이 seed 가능한 대안 설계가 승인됨
- `P3-B grandfather canonicalization`
  - 두 grandfather asset의 rename/canonicalization 작업안이 준비됨
  - known-warning ledger와 grandfather registry 변경안이 리뷰 승인됨
- `P3-C defaultStageId sunset`
  - 지원되는 direct-play 진입점이 모두 `StageLaunchContextStore` 주입 경로를 가짐
  - stage-backed scene smoke/test가 fallback 없이 green
  - `CreateEditorDirectPlayFallback(...)` 호출이 showcase/editor test 경로로만 제한됨
- `P3-D alias prune`
  - alias ledger row별 owner prune 승인이 완료됨
  - canonical `StageId`만 쓰는 소비자 테스트가 green
  - hardening window 동안 alias count 증가가 없음
- `P3-E broad verification`
  - 최신 `full` baseline snapshot이 갱신됨
  - 직접 touched cluster와 unrelated baseline cluster가 분리됨
  - bounded validation 목표가 문서화됨

## Non-Goals

- production runtime canonical path를 되돌리지 않는다.
- runtime compat mode를 부활시키지 않는다.
- runtime `ResolveLegacy(...)`를 되돌리지 않는다.
- `StageRuntimeBuilder` / `StageRuntimeBuildResult` gameplay-only 경계를 깨지 않는다.
- canonical `StagePresentationDefinition` 외 presentation source를 production path에 다시 넣지 않는다.
- `StageNavigationRequest` / `StageId` 계약을 `ScreenId.Gameplay` 하드코딩으로 되돌리지 않는다.
- broad backlog를 이유로 P2 범위를 다시 넓히지 않는다.

