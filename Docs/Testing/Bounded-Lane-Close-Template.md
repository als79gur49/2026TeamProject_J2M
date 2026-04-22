# Bounded Lane Close Template

이 문서는 post-stage-content bounded lane close note minimum common format을 정의한다.

## Required Sections

- `Scope`
- `Executed Commands`
- `Artifact List With Exact Dates`
- `Result Summary`
- `Allowed Claims`
- `Explicit Non-Claims`
- `Open Functional Backlog / Handoff`
- `Open Risks`

## Artifact Pairing Rules

- 하나의 claim은 same revision, same execution window, same lane artifact만 조합한다.
- 다른 날짜 artifact는 historical comparison 또는 pinned baseline reference로만 쓴다.
- `2026-04-22 core/ui`와 `2026-04-21 full`을 하나의 broad recovery claim 근거로 합치지 않는다.

## Example Skeleton

```md
# <Lane Name> Close Note <YYYY-MM-DD>

## Scope
- bounded scope only

## Executed Commands
- `<command 1>`
- `<command 2>`

## Artifact List With Exact Dates
- `<artifact path>` (`YYYY-MM-DD`)

## Result Summary
- `<what passed / failed / remained open>`

## Allowed Claims
- `<claim>`

## Explicit Non-Claims
- `<what this close does not imply>`

## Open Functional Backlog / Handoff
- `source lane`
- `target lane`
- `reason`
- `blocking claim`
- `required evidence`

## Open Risks
- `<risk>`
```

## Review Rule

- lane feature owner가 draft를 작성한다.
- architecture/governance reviewer가 wording 적합성을 리뷰한다.
- wording/doc/CI green만으로 functional lane closure를 주장하지 않는다.
