# Stage Editor Direct-Play Adoption Checklist

이 문서는 stage-backed gameplay shell의 launcher-only direct-play 운영 정착 checklist다. runtime fallback을 부활시키지 않고, UX/tooling/documentation으로 friction을 완화하는 데만 사용한다.

## Supported Menu Parity

- exact menu path:
  - `Tools/Stages/Direct Play/Launch Stage...`
  - `Tools/Stages/Direct Play/Replay Last Stage`
- supported stage id quick-launch entries:
  - `mechanics-showcase`
  - `onboarding`
  - `stage-0-1`
  - `stage-1-1`
- onboarding 문서, smoke checklist, bug reproduction note는 위 exact menu path와 exact stage ids를 그대로 사용한다.

## Onboarding Checklist

- `Tools/Stages/Direct Play/Launch Stage...`에서 stage id를 선택한다.
- launcher가 `UIAudioScene` canonical gameplay shell을 열고 launch context를 주입하는지 확인한다.
- 같은 editor session에서 반복 재현은 `Replay Last Stage`를 우선 사용한다.
- plain Play는 supported workflow가 아니라 unsupported reference case로만 기록한다.

## Smoke Checklist

- direct-play smoke/manual flow는 launcher 경유로만 기록한다.
- plain Play를 눌렀다면 warning/fail-fast guidance만 기록하고 success evidence로 취급하지 않는다.
- supported stage id coverage는 direct-play catalog `100%`여야 한다.
- smoke note는 stage id, canonical shell, used menu path, observed warning/fail-fast, result를 함께 적는다.

## Smoke Cycle Standard

- Lane B smoke evidence는 `cycle note` 단위로 남긴다.
- 한 cycle note는 same revision, same checkpoint window, same executor session에서 수행한 supported stage `4`건을 함께 기록한다.
- cycle header는 아래 필드를 모두 포함한다.
  - `revision`
  - `cycle id`
  - `executor`
  - `editor session identifier`
  - `date window`
  - `catalog coverage check result`
  - `plain Play workflow classification`
- stage evidence row는 아래 필드를 모두 포함한다.
  - `stage id`
  - `canonical shell`
  - `exact menu path`
  - `executor`
  - `execution date/time`
  - `launch result`
  - `observed warning/fail-fast`
  - `plain Play attempted`
  - `notes`
- `stage id`는 exact stage id만 사용한다.
  - `mechanics-showcase`
  - `onboarding`
  - `stage-0-1`
  - `stage-1-1`
- `exact menu path`는 아래 둘 중 하나만 허용한다.
  - `Tools/Stages/Direct Play/Launch Stage...`
  - `Tools/Stages/Direct Play/Replay Last Stage`
- `launch result`는 `Pass`, `Fail`, `Inconclusive`만 사용한다.
- `plain Play attempted`는 `No` 또는 `Yes-unsupported reference only`만 사용한다.

## Cycle Cadence And Executor Rules

- `두 번 연속 smoke/report cycle`은 같은 adoption checkpoint window 안의 `Cycle 1`, `Cycle 2` 두 건으로만 판정한다.
- checkpoint window는 최대 `7` calendar days다.
- same checkpoint window는 `7 calendar days`를 넘기지 않는다.
- `Cycle 2`는 `Cycle 1` close draft가 작성된 뒤 수행해야 한다.
- 최소 분리 기준:
  - 다른 work session
  - 다른 dated note
- 기본 권장 기준:
  - 다른 calendar day
  - 다른 executor `2`명
- 팀 운영상 같은 날짜 실행만 가능하면 예외 기준은 아래 둘을 모두 만족해야 한다.
  - 최소 `4`시간 간격
  - fresh editor session
- executor를 분리하지 못하면 `same executor + governance reviewer co-sign`을 허용하되 hard adoption close note에 예외 사유를 남긴다.

## Counter Rules

- hard adoption close note에는 `Counter Summary` 표를 반드시 포함한다.
- `Counter Summary` 표의 열은 아래 네 개로 고정한다.
  - `metric`
  - `count`
  - `counted window`
  - `evidence source`
- `launcher bypass 정상 workflow 기록 0`의 계수 기준:
  - checkpoint window 안의 direct-play 관련 smoke note
  - bug reproduction note
  - close note
  - onboarding example
  위 문서 중 plain Play 또는 launcher bypass를 `supported/success workflow`로 적은 사례 수
- `fallback 요구 issue 0`의 계수 기준:
  - checkpoint window 시점의 open backlog 중
  - `defaultStageId` 부활
  - scene-local default
  - plain Play supported 승격
  - runtime fallback 추가
  를 요구하는 row 수

## Soft Adoption Evidence

- direct-play catalog coverage `100%`
- onboarding 문서와 menu entry 일치
- smoke checklist가 launcher-only workflow를 명시
- plain Play unsupported 경고 문구가 고정
- `Cycle 1` 4-stage smoke note
- validator/test/doc alignment proof

## Hard Enforcement Evidence

- `Cycle 2` 4-stage smoke note
- 두 번 연속 smoke/report cycle에서 launcher bypass를 정상 workflow로 기록한 사례 `0`
- direct-play 관련 open issue 중 fallback 요구 `0`
- stage-backed manual smoke note가 모두 launcher path를 명시
- validator/test/doc에서 plain Play unsupported 해석이 일치
- `stage-backed manual smoke notes launcher-path only summary`

## Minimum Evidence Set

- soft adoption 종료 최소 evidence set:
  - `catalog coverage 100% proof`
  - `doc/menu/onboarding/checklist parity proof`
  - `validator/test/doc alignment proof`
  - `Cycle 1` 4-stage smoke note
- hard adoption close 최소 evidence set:
  - soft adoption evidence set 전체
  - `Cycle 2` 4-stage smoke note
  - `bypass count 0`
  - `fallback-request count 0`
  - `stage-backed manual smoke notes launcher-path only summary`

## Close Note Attachment Set

- Lane B 운영 close note는 아래 evidence를 첨부 대상으로 고정한다.
  - `Cycle 1` note
  - `Cycle 2` note
  - targeted validator/test pass artifact
  - catalog coverage proof
  - `Counter Summary`
  - open issue review snapshot
- plain Play는 어떤 경우에도 success evidence로 세지지 않는다.
- plain Play를 시도했다면 `Yes-unsupported reference only`로만 기록한다.

## Friction Management

- 허용 완화책:
  - menu shortcut discoverability
  - `Replay Last Stage`
  - onboarding examples
  - smoke checklist 개선
- 금지:
  - `defaultStageId` 성격의 fallback 부활
  - scene-local default 값 대체
  - unsupported plain Play를 지원 workflow로 승격
  - adoption friction을 이유로 canonical runtime contract 변경

## Supporting Template

- smoke cycle note는 [Stage-Editor-Direct-Play-Smoke-Cycle-Template.md](./Stage-Editor-Direct-Play-Smoke-Cycle-Template.md)를 따른다.
