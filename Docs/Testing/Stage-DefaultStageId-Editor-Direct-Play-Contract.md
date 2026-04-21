# Stage defaultStageId Editor Direct-Play Contract

이 문서는 `defaultStageId`를 production fallback이 아니라 editor direct-play/test fallback으로만 취급하는 계약을 고정한다.

## Canonical Rule

- canonical runtime path는 `StageLaunchContextStore`가 제공하는 `StageId`를 사용한다.
- production runtime source-of-truth는 launch context다.
- `defaultStageId`는 launch context가 없는 editor direct-play/test 상황에서만 제한적으로 사용한다.

## Code-Level Guard

- `StageLoadRequest.CreateLaunchContextOnly(...)`
  - canonical runtime request factory
  - `defaultStageId`를 사용하지 않는다.
- `StageLoadRequest.CreateEditorDirectPlayFallback(...)`
  - editor direct-play/test fallback factory
  - 유효한 `defaultStageId`가 없으면 생성 자체가 실패한다.
- `StageRuntimeContentResolver.Resolve(...)`
  - launch context가 있으면 항상 그것을 우선 사용한다.
  - launch context가 없고 fallback policy가 `None`이면 즉시 실패한다.
  - launch context가 없고 fallback policy가 `EditorDirectPlayOnly`여도 `Application.isEditor == false`이면 즉시 실패한다.

## Allowed Call Sites

- `StageBackedGameplayShowcaseInstallerBase`와 동등한 editor direct-play showcase path
- editor test path

그 외 production runtime path는 `CreateLaunchContextOnly(...)`만 사용해야 한다.

## Disallowed Interpretation

- `defaultStageId`를 production runtime convenience path로 승격하지 않는다.
- `defaultStageId`를 scene-global fallback lookup으로 해석하지 않는다.
- continue/retry의 canonical source를 `StageNavigationRequest` / `StageId`에서 다시 scene-local default 값으로 되돌리지 않는다.

## Sunset Criteria

`defaultStageId` fallback 제거는 아래 조건이 모두 충족될 때만 P3-C에서 수행한다.

- 지원되는 direct-play 진입점이 모두 `StageLaunchContextStore` 주입 경로를 가진다.
- stage-backed scene smoke/test가 fallback 없이 green이다.
- `CreateEditorDirectPlayFallback(...)` 호출이 showcase/editor test 경로에서만 남아 있고 제거 계획이 승인됐다.
- 팀 규약과 관련 문서가 manual scene play without launch context를 더 이상 지원하지 않는다고 명시한다.

## Reporting Rule

- `defaultStageId` 관련 변경은 `editor direct-play fallback hardening`으로만 보고한다.
- `runtime fallback support` 또는 `production recovery path` 같은 표현은 금지한다.
