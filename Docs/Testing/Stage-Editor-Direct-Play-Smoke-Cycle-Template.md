# Stage Editor Direct-Play Smoke Cycle Template

이 문서는 Lane B hard adoption evidence를 위한 stage-backed scene direct-play smoke cycle note template이다.

## Cycle Header

- `revision`
- `cycle id`
- `executor`
- `editor session identifier`
- `date window`
- `catalog coverage check result`
- `plain Play workflow classification`

## Stage Evidence Table

| stage id | exact menu path | slot mode | level group | initial chances | death/chance/retry result | clear result | executor | execution date/time | launch result | observed warning/fail-fast | plain Play attempted | notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `stage-0-1` | `Tools/Stages/Direct Play/Launch Stage...` | `CampaignTempSlot` | `level-0` | `<count>` | `<one chance spent and same stage retried>` | `<result>` | `<executor>` | `<YYYY-MM-DD HH:MM KST>` | `Pass/Fail/Inconclusive` | `<warning or None>` | `No / Yes-unsupported reference only` | `<notes>` |
| `stage-1-1` | `Tools/Stages/Direct Play/Launch Stage...` | `CampaignTempSlot` | `level-1` | `<count>` | `<one chance spent and same stage retried>` | `<result>` | `<executor>` | `<YYYY-MM-DD HH:MM KST>` | `Pass/Fail/Inconclusive` | `<warning or None>` | `No / Yes-unsupported reference only` | `<notes>` |
| `<stage id>` | `Tools/Stages/Direct Play/Replay Last Stage` | `CampaignTempSlot` | `<group>` | `<count>` | `<result>` | `<result>` | `<executor>` | `<YYYY-MM-DD HH:MM KST>` | `Pass/Fail/Inconclusive` | `<warning or None>` | `No / Yes-unsupported reference only` | `<notes>` |

Capture smoke records each of `--capture-stage <id>`, `--capture-stage=<id>`, and
`-captureStage <id>` with the same slot, group, chance, death, retry, and clear
fields. Record invalid-stage rejection and whether slot and launch context stayed
unchanged. Record a plain stage-scene Play attempt as unsupported and verify
the host rejects it before the first gameplay tick.

## Counter Summary

| metric | count | counted window | evidence source |
| --- | ---: | --- | --- |
| launcher bypass 정상 workflow 기록 | `<count>` | `<window>` | `<note paths>` |
| fallback 요구 issue | `<count>` | `<window>` | `<issue snapshot>` |

## Exception Note

- 다른 calendar day 분리를 만족하지 못하면 아래를 함께 적는다.
  - 최소 `4`시간 간격 여부
  - fresh editor session 여부
  - `same executor + governance reviewer co-sign` 예외 사유

## Attachment Set

- targeted validator/test pass artifact
- catalog coverage proof
- open issue review snapshot
- stage-backed manual smoke notes launcher-path only summary
