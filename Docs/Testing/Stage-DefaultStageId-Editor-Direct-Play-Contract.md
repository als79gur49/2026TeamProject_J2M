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
  - canonical gameplay shell scene path와 quick-launch supported `StageId`를 선언한다.
- `StageEditorDirectPlayLauncher`
  - `Launch Stage...`
  - `Replay Last Stage`
  - supported stage quick-launch menu
  - Play mode 진입 전에 pending launch context를 주입한다.
- `StageLaunchContextStore`
  - pending editor direct-play stage id를 1회 소비하고 current launch context로 승격한다.

## Disallowed Interpretation

- scene open 후 바로 Play 하는 workflow를 supported direct-play path로 취급하지 않는다.
- `defaultStageId`를 다른 이름의 scene-local runtime fallback으로 치환하지 않는다.
- continue/retry의 canonical source를 `StageNavigationRequest` / `StageId`에서 scene-local default 값으로 되돌리지 않는다.

## Required Readiness

- canonical gameplay shell scene과 quick-launch stage ids는 `StageEditorDirectPlayCatalog`에 등록되어야 한다.
- `StageSceneBootstrapValidator`는 direct-play catalog coverage와 `defaultStageId` residue absence를 함께 검증한다.
- direct-play smoke/manual flow는 launcher 경유로만 기록한다.
- fail-fast message는 `Tools/Stages/Direct Play/Launch Stage...` 사용법을 안내해야 한다.

## Operational Metrics

- `catalog coverage`
  - canonical gameplay shell direct-play catalog coverage `100%`
- `workflow compliance`
  - manual smoke / close note / bug reproduction note에서 stage-backed scene direct-play 실행 경로가 launcher 경유로만 기록된다.
  - plain Play 재현은 unsupported reference case로만 기록한다.
- `warning consistency`
  - plain Play warning과 fail-fast message는 같은 menu path `Tools/Stages/Direct Play/Launch Stage...`를 안내해야 한다.
- `onboarding parity`
  - onboarding 문서, smoke checklist, menu entry, supported stage list는 같은 stage 세트와 같은 용어를 사용해야 한다.

## Supported Stage Labels

- `Combined Gameplay Showcase`
- `Tutorial Scene`

위 exact stage labels는 onboarding parity와 smoke note에 그대로 사용한다.

## Soft Adoption

- 목표:
  - launcher-only contract를 팀 기본 workflow로 정착시킨다.
- 완료 기준:
  - catalog coverage `100%`
  - onboarding 문서와 menu entry 일치
  - smoke checklist가 launcher-only workflow를 명시
  - plain Play unsupported 경고 문구가 고정
- 허용 friction 완화책:
  - menu shortcut discoverability
  - `Replay Last Stage`
  - onboarding examples
  - smoke checklist 개선

## Hard Enforcement

- 목표:
  - launcher bypass를 supported workflow가 아니라 unsupported misuse로 다룬다.
- 완료 기준:
  - 두 번 연속 smoke/report cycle에서 launcher bypass를 정상 workflow로 기록한 사례 `0`
  - direct-play 관련 open issue 중 fallback 요구 `0`
  - stage-backed manual smoke note가 모두 launcher path를 명시
  - validator/test/doc에서 plain Play unsupported 해석이 일치
- enforcement 방식:
  - runtime fallback 추가가 아니라 warning, checklist, triage policy, close wording으로 고정한다.

## Evidence Format

- hard adoption evidence는 `Cycle 1`, `Cycle 2` 두 cycle note로 남긴다.
- 두 cycle은 같은 adoption checkpoint window 안에 있어야 한다.
- checkpoint window는 최대 `7` calendar days다.
- same checkpoint window는 `7 calendar days`를 넘기지 않는다.
- `Cycle 2`는 `Cycle 1` close draft가 작성된 뒤 수행해야 한다.
- 기본 분리 기준은 다른 calendar day다.
- 같은 날짜에 실행하면 최소 `4`시간 간격과 fresh editor session을 함께 남긴다.
- 서로 다른 executor `2`명을 권장한다.
- executor를 분리하지 못하면 `same executor + governance reviewer co-sign`을 예외로 허용하되 close note에 명시한다.
- hard adoption close note는 `Counter Summary` 표를 포함해야 한다.
- `Counter Summary`는 아래 metric을 같은 checkpoint window에서 계수한다.
  - launcher bypass 정상 workflow 기록
  - fallback 요구 issue
- plain Play는 어떤 경우에도 success evidence가 아니다.

## Completion Declaration

- `운영 정착 완료`는 아래가 모두 참일 때만 사용한다.
  - `Soft Adoption` 완료
  - `Hard Enforcement` 완료
  - same revision 또는 same checkpoint window에서 doc/menu/validator/smoke evidence가 정합

## Friction Management

- friction 완화는 구조 rollback이 아니라 UX/tooling/documentation으로만 해결한다.
- 금지:
  - `defaultStageId` 성격의 fallback 부활
  - scene-local default 값 대체
  - unsupported plain Play를 지원 workflow로 승격
  - adoption friction을 이유로 canonical runtime contract 변경

## Supporting Documents

- [Stage-Editor-Direct-Play-Adoption-Checklist.md](./Stage-Editor-Direct-Play-Adoption-Checklist.md)
- [Stage-Editor-Direct-Play-Smoke-Cycle-Template.md](./Stage-Editor-Direct-Play-Smoke-Cycle-Template.md)

## Reporting Rule

- direct-play 관련 변경은 `editor direct-play launcher contract` 또는 `defaultStageId sunset`으로만 보고한다.
- `runtime fallback support` 또는 `production recovery path` 같은 표현은 금지한다.
