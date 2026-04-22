# Stage Editor Direct-Play Adoption Checklist

이 문서는 stage-backed scene의 launcher-only direct-play 운영 정착 checklist다. runtime fallback을 부활시키지 않고, UX/tooling/documentation으로 friction을 완화하는 데만 사용한다.

## Supported Menu Parity

- exact menu path:
  - `Tools/Stages/Direct Play/Launch Current Scene`
  - `Tools/Stages/Direct Play/Replay Last Stage-Backed Scene`
- supported scene menu labels:
  - `Combined Gameplay Showcase`
  - `Tutorial Scene`
  - `UI Audio Scene`
- onboarding 문서, smoke checklist, bug reproduction note는 위 exact menu path와 exact scene labels를 그대로 사용한다.

## Onboarding Checklist

- saved stage-backed scene을 연다.
- plain Play가 아니라 `Tools/Stages/Direct Play/Launch Current Scene`를 사용한다.
- 같은 editor session에서 반복 재현은 `Replay Last Stage-Backed Scene`를 우선 사용한다.
- plain Play는 supported workflow가 아니라 unsupported reference case로만 기록한다.

## Smoke Checklist

- direct-play smoke/manual flow는 launcher 경유로만 기록한다.
- plain Play를 눌렀다면 warning/fail-fast guidance만 기록하고 success evidence로 취급하지 않는다.
- supported stage-backed scene coverage는 direct-play catalog `100%`여야 한다.
- smoke note는 current scene label, used menu path, observed warning/fail-fast, result를 함께 적는다.

## Soft Adoption Evidence

- direct-play catalog coverage `100%`
- onboarding 문서와 menu entry 일치
- smoke checklist가 launcher-only workflow를 명시
- plain Play unsupported 경고 문구가 고정

## Hard Enforcement Evidence

- 두 번 연속 smoke/report cycle에서 launcher bypass를 정상 workflow로 기록한 사례 `0`
- direct-play 관련 open issue 중 fallback 요구 `0`
- stage-backed manual smoke note가 모두 launcher path를 명시
- validator/test/doc에서 plain Play unsupported 해석이 일치

## Friction Management

- 허용 완화책:
  - menu shortcut discoverability
  - `Replay Last Stage-Backed Scene`
  - onboarding examples
  - smoke checklist 개선
- 금지:
  - `defaultStageId` 성격의 fallback 부활
  - scene-local default 값 대체
  - unsupported plain Play를 지원 workflow로 승격
  - adoption friction을 이유로 canonical runtime contract 변경
