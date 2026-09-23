# e58 TileFeature Cell-Index Optimization Decision

## Decision

`e58f47c89` (`TileFeature cell index carrier` reuse)는 이 통합 후보에 포함하지 않는다.
이 문서는 구현 채택 기록이 아니라 병합 거절 근거다.

## Local structural effect

e58은 fast import와 동일-cell TileFeature 상태 변경에서 tile-feature cell-index carrier의 재구축을 피한다.
테스트 계측은 이 국소 효과를 확인한다.

- `ProjectedWorld_FastImport_DoesNotUseSlowBaseSpawnPath`는 fast import에서 `SnapshotOwnedTileFeatureCellIndexBuildCount == 0`을 확인한다.
- 동일-cell 상태 변경은 재구축 0회, cell membership 변경은 재구축 경로를 사용한다는 계약을 e58의 focused tests가 고정했다.

이는 cell-index 재구축 수 감소의 증거이며, 전체 gameplay CPU 또는 tick wall-time 개선을 뜻하지 않는다.

## Player measurement evidence

모든 아래 비교는 `stage-4-3`, release-like Player, `cpu-tick-v1` admission을 사용했다.
cleanup S3-A HOLD는 performance/tick admission과 별도의 기존 calibration 상태이므로 결과 판정에서 분리한다.

| Campaign | Samples per side | Tick-wall median | CPU main p95 |
| --- | ---: | ---: | ---: |
| 600-frame initial ABBA | 2 | +11.28% | +11.07% |
| 600-frame additional ABBA | 2 | -2.59% | -2.25% |
| 600-frame combined | 4 | +4.57% | +4.62% |
| 900-frame ABBA | 2, 150 ticks/run | +3.26% | +6.74% |

Evidence roots:

- `/mnt/d/J2M/evidence/five-commit-individual-abba2-20260921/e58-tile`
- `/mnt/d/J2M/evidence/e58-3bff-targeted-abba2-20260922/e58`
- `/mnt/d/J2M/evidence/e58-3bff-long-abba-20260922/e58`

The first 1,800-frame attempt is excluded: the Player hit an unrelated stage-launch-context conflict before producing metrics. The completed 900-frame campaign is the longer valid observation.

## Rationale and follow-up

The local reconstruction reduction did not produce a reliable workload-level improvement. The combined 600-frame result and the independent longer capture both worsen the representative tick and CPU-main measures. Therefore this implementation is not merged.

The four-optimization candidate deliberately omits e58 code and its e58-specific TileFeature carrier tests. Any later attempt must start from a new candidate SHA and establish fresh same-revision functional and performance evidence.
