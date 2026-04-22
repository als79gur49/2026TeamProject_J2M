# Stage Editor Direct-Play Launcher Contract

이 문서는 stage-backed scene의 editor direct-play를 `defaultStageId` fallback이 아니라 launcher-driven launch context 주입으로만 허용하는 계약을 고정한다.

## Canonical Rule

- canonical runtime path는 `StageLaunchContextStore`가 제공하는 `StageId`를 사용한다.
- production runtime source-of-truth는 launch context다.
- `defaultStageId` runtime fallback는 제거됐다.

## Supported Workflow

- `StageLoadRequest.CreateLaunchContextOnly(...)`
  - canonical runtime request factory
  - stage bootstrap은 launch context가 없으면 즉시 실패한다.
- `StageEditorDirectPlayCatalog`
  - 지원되는 stage-backed scene path를 canonical `StageId`에 매핑한다.
- `StageEditorDirectPlayLauncher`
  - `Launch Current Scene`
  - `Replay Last Stage-Backed Scene`
  - Play mode 진입 전에 pending launch context를 주입한다.
- `StageLaunchContextStore`
  - pending editor direct-play stage id를 1회 소비하고 current launch context로 승격한다.

## Disallowed Interpretation

- scene open 후 바로 Play 하는 workflow를 supported direct-play path로 취급하지 않는다.
- `defaultStageId`를 다른 이름의 scene-local runtime fallback으로 치환하지 않는다.
- continue/retry의 canonical source를 `StageNavigationRequest` / `StageId`에서 scene-local default 값으로 되돌리지 않는다.

## Required Readiness

- 지원되는 stage-backed scene은 모두 `StageEditorDirectPlayCatalog`에 등록되어야 한다.
- `StageSceneBootstrapValidator`는 direct-play catalog coverage와 `defaultStageId` residue absence를 함께 검증한다.
- direct-play smoke/manual flow는 launcher 경유로만 기록한다.
- fail-fast message는 `Tools/Stages/Direct Play/Launch Current Scene` 사용법을 안내해야 한다.

## Reporting Rule

- direct-play 관련 변경은 `editor direct-play launcher contract` 또는 `defaultStageId sunset`으로만 보고한다.
- `runtime fallback support` 또는 `production recovery path` 같은 표현은 금지한다.
