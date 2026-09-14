# Gameplay 최적화 통합 Bundle 러프 A/B 계획

- 작성일: 2026-09-14 KST
- 상태: **Historical plan — 실행 전 기준 보존; 후속 캠페인 실행 완료(결과별 판정은 상이)**
- 목적: 통합된 최적화 묶음 전체의 방향성과 명백한 회귀 신호를 제한된 비용으로 확인
- A: `47cbea9fa6f7a02b13e71ce41d179d5307190ca8`
- 계획 당시 B: `b77765bde18f0575db00bc3633be3d00d284980b`
- 실제 비교 B: `cc29c09e379005295d9fd6c49e3528251dedbbf9` (capture assembly 복구 포함)
- 현재 통합 상태: [Gameplay 최적화 main 통합 Closeout](./Gameplay-Optimization-Main-Integration-Closeout-2026-09-14.md)

## 실행 후 기록의 경계

아래 §1–13은 당시 승인 범위·원 계획을 보존한다. 현재 작업 지시로 재실행하지 않는다. 제품명 단축, capture assembly 복구, 이후 별도 승인된 CPU 정책 구현은 원 계획을 소급 변경한 내용이 아니라 후속 조치다.

- 초기/r2는 비교 미완료, strict 4-3 r3는 편차 초과로 판정 불가, strict 4-2는 GPU count 제외로 미완료였다.
- 별도 새 `cpu-tick-v1` 캠페인에서 4-2와 4-3을 각각 A–B–B–A로 실행했고 두 캠페인 모두 개선 신호였다. 기존 실패·판정 불가 표본을 새 정책의 정식 표본으로 재분류하지 않았다.
- GPU 판정 분리의 현재 계약은 [CPU/Tick 측정 정책](../Testing/Gameplay-CPU-Tick-Admission-Policy.md)이 소유한다. 기본값 `strict-v1`, 필수 CPU/Tick·identity 검증, 잘못된 GPU 데이터 거부를 유지한다.
- 리비전별 실행·검증·증거 경로와 약 25%라는 수치의 해석 한계는 [Closeout §4](./Gameplay-Optimization-Main-Integration-Closeout-2026-09-14.md#4-검증과-측정-결과)를 따른다. 아래 당시 검증 요약을 최신 HEAD 재실행 근거로 쓰지 않는다.

## 1. 왜 이 방식으로 진행하는가

개별 최적화는 이미 여러 차례, 전체로는 수십 회 측정했지만 관측된 변화가 실행 편차와 런타임 노이즈 안에 머물러 명확한 증감을 반복해서 확인하지 못했다. 더 많은 동일 형태의 micro A/B를 반복해도 비용만 증가하고 결론이 명확해질 가능성이 낮다.

19개 통합 commit은 Snapshot, Cleanup, Enemy AI, Presentation, Pause, HUD와 Save처럼 서로 다른 경로를 건드린다. 일부 변경은 비용을 줄이고 다른 변경은 cache 유지·불변성·계측 비용을 추가할 수 있어 개별 효과의 합이 최종 Player 비용과 같다고 볼 수 없다. 실제 PC 플레이에서는 `stage-4-3`이 동시 활성·갱신되는 전체 오브젝트가 가장 많은 구간으로 확인됐고, 눈에 띄는 frame drop과 전체 Stage 중 가장 낮은 FPS가 관찰됐다. 현재 필요한 판단은 각 패치의 인과적 기여가 아니라 **그 문제가 가장 강하게 드러난 후반부 workload에서 배포 후보인 통합 묶음 전체가 순이익인지, 최소한 명백한 회귀가 없는지**다.

각 변경을 하나씩 되돌리거나 toggle을 새로 만들면 다음 문제가 생긴다.

- 상호 의존된 Snapshot/Cleanup/Wall 계약을 분해하면서 실제 배포 코드와 다른 비교군이 만들어질 수 있다.
- 여러 worktree와 Unity `Library`, Player build를 동시에 운용하면 저장공간·프로세스·열 상태·작업자 비용이 커진다.
- 비교용 toggle과 역이식 자체가 새 구현 차이를 만들어 측정 귀속을 더 흐릴 수 있다.
- 기존 `gameplay-performance` lane은 build, source identity, artifact와 admission을 함께 보존하지만 호출마다 build와 1회 실행이 결합돼 있다.

따라서 이번 작업은 두 revision에 동일한 Stage 전환만 적용한 runner로 전체 차이를 직렬 교차 실행하는 **저비용 directional diagnostic**으로 제한한다. 제품 runtime, Player probe와 validator는 바꾸지 않는다. 측정 runner에서는 build/runtime/manifest의 Stage literal 세 곳만 양쪽에 byte-identical하게 바꾸고 측정 후 원복한다. 이는 formal benchmark, 통계적 유의성 검정, 개별 최적화 성능 증명이 아니다.

## 2. 비교 범위

`A -> B`는 통합 직전 main부터 통합 완료 main까지의 19개 commit 전체다.

- 포함: Pause reflection cache, Cleanup candidate index/ordered executor, immutable Tile index, same-epoch Snapshot reuse, CrossLOS copy 제거, authored Wall identity, static Wall cache, committed-frame scratch reuse, Snapshot 진단·allocation test, HUD/Save/Chance 정리와 query cache
- 미포함: A에 이미 존재하는 driver lookup cache `261f2df07`, Pause buffer reuse `c150d0cc3`
- 함께 섞인 계약 변화: authored Wall identity와 HUD 표시/조회 경로 정리

그러므로 결과를 “순수 최적화 19개의 합”이라고 부르지 않는다. 정확한 표현은 **두 revision 사이 통합 bundle의 해당 workload상 순효과**다.

## 3. 현재 실행 기반과 Stage 선택 이유

두 revision의 아래 측정 파일은 Git blob이 모두 동일하다.

- `run_tests.sh`
- `Tools/gameplay_performance_admission.py`
- `Tools/gameplay_performance_campaign.py`
- Cleanup admission/calibration/evidence manifest와 workload contract
- `GameplayPerformancePlayerProbe.cs`

같은 runner·probe·schema를 사용하므로 측정 도구 차이를 A/B 변화로 오인할 위험이 낮다. `Tools/gameplay_performance_campaign.py`는 과거 5-state/20-slot SHA가 고정돼 있어 이번 2-state bundle 비교에는 그대로 사용하지 않는다.

기존 `./run_tests.sh gameplay-performance`가 즉시 제공하는 범위는 다음과 같다.

- Release-like Windows Mono Player
- Direct3D11, 1920x1080
- canonical gameplay shell을 통한 `stage-1-1` 로드
- render-idle 및 gameplay-neutral-tick phase
- whole-Tick stopwatch, frame/CPU/GPU/GC 지표
- 내부 warmup과 sample 수 고정
- build/source/artifact identity와 raw metrics 보존

### 3.1 공통 runner가 왜 `stage-1-1`인가

`stage-1-1`은 공통 runner가 지원할 수 있는 유일한 Stage가 아니다. `PlayerProfilerCaptureCli`와 Player launch path는 유효한 `--capture-stage` 값을 받아 같은 canonical gameplay shell에서 해당 Stage를 로드한다. 현재 값은 lane 최초 도입 commit `e354325d66`에서 다음 세 위치에 함께 상수로 들어간 역사적 기본값이다.

- Player build의 `--capture-stage stage-1-1`
- Player runtime의 `--capture-stage stage-1-1`
- 결과 manifest의 `Stage=stage-1-1`

확인 가능한 공식 근거는 이 값이 canonical shell capture와 과거 결과를 이어 주는 **historical continuity workload**라는 점까지다. 최초 작성자가 왜 다른 초기 Stage가 아니라 정확히 `stage-1-1`을 골랐는지 설명한 별도 설계 기록은 찾지 못했다. 따라서 `stage-1-1`이 가장 무겁거나 모든 최적화를 대표해서 선택됐다고 추정하지 않는다. 공통 runner라는 이름도 모든 Stage를 자동 대표한다는 뜻이 아니다.

### 3.2 실플레이 관찰과 `4-2`·`4-3` 비교

`stage-4-3`을 primary로 선택한 직접적인 이유는 실제 PC 플레이에서 확인한 체감 최악 구간이기 때문이다. Stage를 검토했을 때 `4-3`은 렌더링과 simulation에서 동시에 유지·갱신되는 전체 오브젝트가 가장 많았고, 그 상황에서 frame drop과 전체 Stage 중 가장 낮은 FPS가 관찰됐다. 따라서 최적화 전후 검토도 부하가 작은 Stage가 아니라 기존 문제가 실제로 나타난 `4-3`의 고부하 콘텐츠를 동일하게 사용해 비교해야 한다.

아래 `4-2`의 Box 109개는 특정 authored entity 종류의 개수이고, `4-3`의 “전체 오브젝트가 가장 많음”은 실제 플레이 시 함께 활성화되는 적, 소환 가능 객체, 표시 객체와 기타 runtime 객체를 포괄한 관찰이다. 두 표현은 집계 범위가 다르므로 모순으로 취급하지 않는다. 다만 현재 문서에는 당시 실플레이의 정확한 FPS 수치와 원본 capture가 연결돼 있지 않으므로, “최저 FPS”는 workload 선택을 위한 정성적 관찰 근거로만 사용한다. 이번 neutral workload도 당시의 입력·조작 상황을 그대로 재현하지는 않는다. 이번 A/B의 정량 판정은 같은 `4-3` Stage 기반에서 새 campaign이 보존하는 whole-Tick과 frame/CPU/GPU raw metrics로 내린다.

| 후보 | 강하게 행사하는 경로 | 기존 역할·특성 | 이번 용도 |
|---|---|---|---|
| `stage-4-2` | Box 109개, TileFeature 64개를 포함한 Tile index·entity/Cleanup 경로 | 비교·비용 귀속 workload이며 기존 측정 변동폭이 작았음 | Stage 민감도와 원인 영역 확인용 보조 workload |
| `stage-4-3` | Enemy 17개와 AI 및 Summon 가능 경로를 포함한 CrossLOS·Snapshot·Presentation 복합 경로 | 최종 Stage의 worst-case acceptance workload이며 whole-Tick 비용이 더 컸음 | **단일 primary workload** |

동일 조건의 2026-09-10 최종본 진단에서 whole-Tick p95 중심값은 `4-2` 7.327ms, `4-3` 8.226ms였고, median도 각각 5.281ms와 6.378ms였다. Stage 간 수치 자체를 최적화 효과로 해석할 수는 없지만, `4-3`이 더 무거운 방향이라는 정량 결과가 실제 PC의 frame drop·최저 FPS 관찰과 일관되므로 제한된 한 Stage의 선택 근거를 보강한다. 별도 worktree의 historical late-stage 계약과 2026-09-10 evidence도 `4-3`을 worst-case acceptance, `4-2`를 비교·비용 귀속 workload로 분류한다. 현재 main에 그 formal late-stage lane이 존재한다는 뜻은 아니다.

A와 B 사이 `stage-4-2`·`stage-4-3`의 본체, Authoring, Entry, Presentation, Audio asset tree에는 변경이 없다. 따라서 이번 비교에서 Stage 콘텐츠 자체의 drift를 bundle delta로 오인할 가능성은 낮다. 실행 전에는 이 경로의 tree hash도 다시 기록한다.

`4-3`이 항상 더 안정적이라는 뜻은 아니다. 과거 세 실행의 whole-Tick p95 범위는 `4-3` 7.969~8.841ms, `4-2` 7.315~7.393ms였다. `4-3`의 범위/중앙값 비율은 약 10.6%로 이번 계획의 고노이즈 기준 10%에 걸릴 수 있다. 그럼에도 이번 목적은 가장 안정적인 micro workload보다 후반 복합 부하에서 bundle 전체의 명백한 회귀를 찾는 것이므로 primary는 `4-3`으로 둔다.

따라서 최소 campaign의 workload는 **`stage-4-3` neutral Tick**으로 고정한다. 이 선택은 “모든 Stage의 평균”을 얻기 위한 것이 아니라, 최적화 후 실제 최저 FPS 구간의 방향이 개선됐는지 또는 더 악화됐는지를 우선 확인하기 위한 것이다. `4-2`를 처음부터 함께 실행하면 build/run 비용이 두 배가 되므로 기본 campaign에는 넣지 않는다. `4-2`는 §13의 조건을 만족할 때만 별도 보조 campaign으로 실행하고, `4-3` 표본과 합쳐 하나의 통계로 계산하지 않는다.

### 3.3 Stage 전환 방법

현재 main에는 `gameplay-late-stage-performance` lane이나 Stage 환경 인자가 없다. 실행 전 양쪽 clean worktree에 같은 고정 patch를 적용해 `gameplay-performance` 함수의 위 세 `stage-1-1` 값만 `stage-4-3`으로 바꾼다. `Stage=S3-A`는 gameplay StageId가 아니라 Cleanup evidence lifecycle 단계명이므로 바꾸지 않는다.

patch 파일과 SHA-256, 적용 전 원본 `run_tests.sh` SHA-256, 적용 후 runner SHA-256을 campaign index에 기록한다. 양쪽 원본 blob이 동일하므로 적용 후 runner SHA도 같아야 한다. 그 밖의 `stage-1-1` 문자열, 제품 코드, probe, validator는 바꾸지 않는다. 최종 manifest의 requested Stage와 runtime log의 primed launch-context Stage가 모두 `stage-4-3`인지 확인한다. 현재 probe는 resolved content StageId를 metrics identity에 기록하지 않으므로 이를 독립적인 runtime content identity 증명으로 주장하지 않는다. campaign 종료 후 두 runner를 원본 SHA와 byte-for-byte 일치하도록 복원한다.

호출마다 Windows .NET UI build와 Player build를 다시 수행하는 구조는 유지한다. build-once/run-many 구현을 함께 넣으면 새 tooling 구현의 효과가 A/B에 섞이므로, 이번 목표가 러프한 방향 확인인 만큼 **.NET UI build 4회와 Player build 4회의 비용을 받아들인다.**

runner가 출력하는 `GAMEPLAY_PERFORMANCE:PASS` 또는 성능 lane `PASS`는 capture/instrumentation과 자료 수집 성공을 뜻한다. `budgetVerdict=NOT_CONFIGURED`이며 B가 A보다 빨라졌거나 성능 budget을 통과했다는 뜻이 아니다. bundle 성능 판정은 §8~9 계산으로만 결정한다.

`stage-4-3` neutral workload는 Snapshot, Cleanup, Enemy AI, Presentation과 일부 HUD hot path를 실행하며 Summon 가능 경로를 포함한다. 이번 100-Tick neutral capture에서 실제 Summon 실행 여부는 현재 probe가 직접 보고하지 않는다. 또한 neutral 입력이므로 Push/Flip 같은 조작 경로와 Save/Chance 변화를 충분히 대표하지 않는다. 따라서 “전체”는 revision delta 전체를 뜻하며 모든 변경이 같은 비율로 행사된다는 뜻이 아니다.

## 4. 작업 공간과 저장 정책

새 worktree를 만들지 않고 기존 D worktree를 사용한다.

| 상태 | worktree | 요구 HEAD |
|---|---|---|
| A | `/mnt/d/J2M/worktrees/gameplay-opt-replay-main-20260914` | detached `47cbea9fa` |
| B | `/mnt/d/J2M/worktrees/gameplay-opt-main-port-20260913` | `b77765bde` |

두 worktree의 Unity `Library`는 공유하지 않는다. 측정은 동시에 실행하지 않고 한 번에 하나의 Unity/Player 프로세스만 실행한다. 새 evidence는 `/mnt/d/J2M/evidence/gameplay-opt-bundle-ab-20260914/`, build는 `/mnt/d/J2M/builds/gameplay-opt-bundle-ab-20260914/` 아래 run별 root에 둔다. 실제 artifact root는 runner가 그 아래 생성하는 UUID campaign exclusive 디렉터리다.

계획 작성 시 `j2m-worktree-audit`는 PASS였고 D 여유 공간은 약 705 GiB였다. 실행 직전에도 audit, D 30 GiB 이상, C 10 GiB 미만 경고 조건을 다시 확인한다.

## 5. 고정 실행 설정

| 설정 | 값 |
|---|---|
| 해상도 | `1920x1080` |
| backend | `Mono` |
| graphics API | `Direct3D11` |
| stage | `stage-4-3` |
| phase | `gameplay-neutral-tick` |
| warmup frames | `120` |
| sample frames | `600` |
| Tick interval | `6` frames, 목표 100 Tick |
| VSync | `0` |
| target frame rate | `-1` |

전원 모드, 해상도, quality, 백그라운드 프로그램과 장비를 campaign 도중 바꾸지 않는다. 각 run 전 현재 프로젝트 Unity/Player 프로세스가 없음을 확인하고 같은 idle 간격을 둔다. 각 lane 내부의 warmup을 사용하며 관측값을 본 뒤 추가 warmup이나 sample 수를 바꾸지 않는다.

`600 frame / interval 6 / 100 Tick`은 기존 `4-2`·`4-3` 후반 Stage 측정에서 실제로 완료된 설정을 재사용한다. 기존 초안의 `1200 frame / interval 1`은 `4-3` world를 1,200 Tick 진행시켜 terminal 상태 진입이나 A/B의 실행 Tick 수 차이를 만들 수 있고, 과거 worst-case 결과와 workload 의미도 달라지므로 채택하지 않는다.

idle 간격은 이전 Unity/Player 프로세스가 완전히 종료된 것을 확인한 시점부터 60초로 고정한다. 실제 종료 시각과 다음 run 시작 시각을 결과에 기록한다. 이 간격은 열·OS cache 편향을 제거한다고 주장하지 않으며 양쪽에 같은 조건을 적용하기 위한 비용 제한 규칙이다.

## 6. 최소 campaign

최소 campaign은 revision별 2회, 총 4회의 직렬 실행이다.

| 순서 | 상태 | run ID |
|---:|---|---|
| 1 | A | `A1` |
| 2 | B | `B1` |
| 3 | B | `B2` |
| 4 | A | `A2` |

`A-B-B-A`는 A를 모두 실행한 뒤 B를 실행할 때 생기는 시간·열·파일 cache 편향을 양쪽에 나누기 위한 순서다. 두 worktree를 동시에 측정하지 않는다.

campaign 시작 시 Stage patch 적용 전 다음을 fail-closed로 확인한다. `run_tests.sh` 자체는 dirty worktree를 거부하지 않고 fingerprint만 기록하므로 clean 검사는 별도로 수행한다.

```bash
j2m-worktree-audit
df -BG /mnt/d /mnt/c
git status --porcelain
git rev-parse HEAD
git rev-parse 'HEAD^{tree}'
```

두 worktree 모두 patch 적용 전 `git status --porcelain` 출력이 비어 있어야 한다. A/B HEAD가 §4의 SHA와 다르거나 D 여유가 30 GiB 미만이면 실행하지 않는다. C 여유가 10 GiB 미만이면 경고를 기록한다.

그 뒤 승인된 Stage patch를 양쪽에 적용하고 다음을 확인한다.

- `git status --porcelain`에는 `run_tests.sh` 한 파일만 나타남
- `git diff -- run_tests.sh`에는 `gameplay-performance`의 build/runtime/manifest Stage 세 값만 나타남
- A/B의 patch SHA-256과 적용 후 `run_tests.sh` SHA-256이 각각 동일함
- 각 slot 직전 diff가 승인된 patch와 byte-for-byte 동일함

이 patch는 측정 workload 선택을 위한 허용된 dirty input이다. 제품 source delta에 포함하거나 최적화로 세지 않는다. 위 allowlist 밖의 변경이 하나라도 있으면 실행하지 않는다.

각 run은 해당 worktree에서 다음 환경값을 같은 숫자로 고정하고 실행한다. evidence/build root의 마지막 run ID만 바꾼다.

```bash
GAMEPLAY_PERFORMANCE_EVIDENCE_ROOT=/mnt/d/J2M/evidence/gameplay-opt-bundle-ab-20260914/captures/<RUN_ID> \
GAMEPLAY_PERFORMANCE_BUILD_ROOT=/mnt/d/J2M/builds/gameplay-opt-bundle-ab-20260914/<RUN_ID> \
GAMEPLAY_PERFORMANCE_WIDTH=1920 \
GAMEPLAY_PERFORMANCE_HEIGHT=1080 \
GAMEPLAY_PERFORMANCE_WARMUP_FRAMES=120 \
GAMEPLAY_PERFORMANCE_SAMPLE_FRAMES=600 \
GAMEPLAY_PERFORMANCE_TICK_INTERVAL=6 \
./run_tests.sh gameplay-performance
```

위 명령은 승인된 `stage-4-3` patch가 먼저 적용된 상태에서만 실행한다. 현재 lane의 Cleanup `strategy A` 표시는 revision 내부 synthetic calibration 전략이다. A/B revision 상태명과 같은 의미가 아니며, 실제 neutral Tick은 각 revision의 production 경로를 실행한다.

각 호출의 exit status를 반드시 캡처하고 `<RUN_ID> -> runner가 생성한 UUID artifact 디렉터리` 매핑을 별도 campaign index에 기록한다. shell의 `set -e`로 lane exit `1`을 곧바로 전체 campaign 중단으로 바꾸지 않는다. Cleanup evidence `HOLD`와 primary performance admission을 §7에 따라 먼저 분리한다.

## 7. 유효성 확인과 재실행 규칙

성능값을 보기 전에 다음을 확인한다.

- run의 HEAD가 계획한 A 또는 B와 정확히 일치
- Stage patch 외 tracked diff 없음과 고정된 untracked 입력 fingerprint
- A/B의 Stage patch 및 적용 후 runner SHA-256 일치
- Player artifact, runtime tree, runner, validator와 metrics hash 존재
- 설정·`stage-4-3`·warmup·sample·Tick interval 일치
- `performance-admission-report.json`이 raw 성능 자료를 유효하게 수용
- Player 정상 종료와 필요한 metrics phase 존재

네 run을 집계하기 전 campaign 차원의 교차 검사를 추가한다.

- Unity/OS/CPU/GPU, backend, configuration, graphics API와 quality 일치
- actual resolution, stage, warmup, sample과 Tick interval 일치
- `gameplay-neutral-tick`의 attempted/executed Tick 수 일치
- 최종 manifest의 requested Stage와 runtime log의 primed launch-context Stage가 모두 `stage-4-3`임
- phase별 valid CPU/GPU sample 수와 counter availability 기록
- 각 RUN_ID 아래 ADMITTED artifact가 정확히 하나이며 retry artifact를 임의로 덮어쓰지 않음

현재 metrics는 실제 neutral world의 entity/enemy/summon cardinality를 제공하지 않는다. Cleanup calibration cardinality는 synthetic workload이므로 이를 대신하지 않는다. 따라서 workload 동등성은 A/B의 `stage-4-3` content tree hash 일치, requested/primed Stage marker와 attempted/executed Tick 수 일치까지만 확인하며, runtime cardinality를 직접 검증했다고 주장하지 않는다.

campaign 종료 시 양쪽 `run_tests.sh`를 적용 전 SHA-256과 일치하도록 복원하고, 다시 `git status --porcelain`이 비어 있음을 확인한다. 복원이 확인되지 않으면 수치는 보존하되 campaign 상태를 `실행 불완전`으로 둔다.

잘못된 revision, build 실패, crash, metrics 누락, 설정 불일치처럼 값 확인 전에 판정 가능한 infrastructure 실패만 같은 slot에서 최대 2회 재시도한다. 모든 실패 attempt와 exit status를 보존한다. 이후에도 admission되지 않으면 campaign을 `실행 불완전`으로 종료한다. 유효하지만 느리거나 불리한 run은 제외하거나 교체하지 않는다.

기존 Cleanup evidence lifecycle이 allocation liveness 등의 이유로 `HOLD`가 되더라도 performance admission과 raw metrics가 유효할 수 있다. 이번 bundle 진단은 historical S3-A official campaign이 아니므로 그 Hold를 PASS로 바꾸지 않고, raw bundle 결과와 historical Hold를 별도 필드로 함께 기록한다. performance admission 자체가 실패하면 해당 run은 bundle 계산에 사용하지 않는다.

첫 4회가 §9의 고노이즈 또는 임계 방향 상충 조건을 만족하면 관측값에 유리한 run만 추가하지 않는다. 필요할 경우 `B3-A3-A4-B4` 전체 4-run block을 한 번만 추가한다. 최종 bundle delta는 A1~A4와 B1~B4 전체 cohort의 median으로 다시 계산하고, pair3=`B3/A3-1`, pair4=`B4/A4-1`도 기록한다. 최초 4-run 결과는 보존하되 최종 판정은 8-run cohort만 사용한다. 두 번째 block도 불명확하면 `판정 불가`로 종료하고 반복을 계속 늘리지 않는다.

## 8. 지표와 계산

주 지표는 `gameplay-neutral-tick.tickWallMilliseconds.p95`다.

보조 지표:

- whole-Tick median과 p99
- CPU main frame p95
- frame interval p95
- render-idle p95
- GPU p95
- GC counter availability와 단위가 유효한 경우의 allocation 값
- 실제 Tick/sample 수

상태별로 A1/A2 및 B1/B2의 raw p95, 최소·최대와 중앙값을 기록한다. 짝수 개 표본의 median은 정렬된 두 중앙값의 산술평균으로 계산한다. bundle 변화율은 반올림 전 값으로 계산하고 표시할 때만 반올림한다.

```text
bundleDeltaPercent = (median(B p95) / median(A p95) - 1) * 100
```

시간 인접 pair도 별도로 계산한다.

```text
pair1 = (B1 / A1 - 1) * 100
pair2 = (B2 / A2 - 1) * 100
```

pair2는 시간 순서상 `B2 -> A2`지만 계산값은 다른 pair와 동일하게 B가 A에 대해 얼마나 변했는지를 나타낸다.

상태별 p95 중앙값은 정밀 통계량이 아니라 러프한 중심값이다. 신뢰구간이나 통계적 유의성을 의미하지 않는다.

## 9. 사전 고정 판정

`±5%`는 이번 문서에서 새로 정의한 **rough diagnostic decision band**다. 기존 Cleanup의 5% 비회귀 ceiling, driver microbenchmark의 5% 규칙, 공식 성능 budget을 승계하거나 대체하지 않는다.

판정 우선순위는 다음과 같이 고정한다.

1. performance admission 실패 → `실행 불완전`
2. 기능 또는 workload 비교 불가 → 성능 판정 중단
3. 어느 상태 cohort든 `(max p95 - min p95) / median p95 * 100 > 10%` → 최초 4-run이면 추가 block 1회, 8-run 최종 cohort이면 `판정 불가`
4. 최종 cohort에 `-5%` 이하 pair와 `+5%` 이상 pair가 함께 존재 → 최초 4-run이면 추가 block, 8-run이면 판정 불가
5. 위 조건이 없을 때 아래 개선·중립·회귀 band 적용

| 조건 | bundle 판정 |
|---|---|
| `bundleDeltaPercent <= -5%`, 최종 cohort의 모든 pair가 음수 | 개선 신호 |
| `abs(bundleDeltaPercent) < 5%`, 최종 cohort의 모든 pair가 `-5% < pair < +5%` | 중립 / 명확한 증감 없음 |
| `bundleDeltaPercent >= +5%`이고 최종 cohort의 모든 pair가 양수 | 회귀 의심 신호 |
| 최초 4-run에서 어느 pair든 `+5%` 이상이지만 위 회귀 조건 미충족 | 잠정 회귀 의심; 추가 block에서 같은 방향의 독립 pair가 재현되는지 확인 |
| 추가 block 후에도 cohort spread가 10% 초과하거나 임계 방향 상충 | 판정 불가 |
| 위 행에 정확히 속하지 않는 그 밖의 혼합 조건 | 추가 block 1회; 이후에도 혼합이면 판정 불가 |

cohort spread 10%는 통계적 유의성 기준이 아니라 추가 block 여부를 정하는 비용 제한 규칙이다. 최종 cohort의 모든 pair가 ±5% 안이고 bundle도 ±5% 안이면 작은 부호 차이가 있어도 중립으로 종료한다.

과거 Cleanup의 `7.104 -> 6.820 ms`, 약 `-3.99%`는 이 계획에서도 중립 대역이다. 기존 공식 해석인 5% 비회귀 통과를 속도 향상 확정으로 바꾸지 않는다.

## 10. 기능·동작 근거와 성능 판정 분리

A/B 사이에는 authored Wall과 HUD 계약 변화가 있어 raw hash 무조건 동일을 요구할 수 없다. 이번 Player lane도 최종 simulation hash나 runtime world cardinality를 제공하지 않는다. 따라서 성능 campaign 전에 기존 core/replay 결과와 다음 조건을 연결한다.

- 같은 requested/primed stage, 입력과 attempted/executed Tick 수
- A/B의 `stage-4-3` content tree hash 및 각 revision 반복 run의 설정 일치
- runtime entity/enemy/summon cardinality는 현재 probe로 확인하지 못했다는 제한
- 알려진 replay 1건은 A/B 모두 같은 기대값 실패로 baseline residue에 분리
- B에서만 새 gameplay failure 또는 비교 불가능한 workload 변화가 발견되면 성능 숫자는 참고로 보존하되 bundle 판정을 중단
- A-only failure/B-pass는 기능 개선 후보일 뿐 성능 개선 근거로 사용하지 않음

현재 연결 가능한 검증은 B core EditMode `290/290`, PlayMode `112/112`, UI `1414/1414`, replay `140/141`과 A에서 동일하게 재현한 replay 1건이다. 이는 기존 bounded 근거일 뿐 이번 campaign 자체의 A/B 전체 기능 parity gate는 아니다. 이 근거를 project-wide/full green으로 표현하지 않는다.

## 11. 결과 기록 필수 항목

- campaign ID와 실행 시각
- 목적과 비공식 directional diagnostic 상태
- A/B commit, tree, branch/detached 상태, patch 전 clean fingerprint, 승인된 runner-only dirty fingerprint와 복원 후 clean fingerprint
- runner/probe/validator/workload/artifact SHA-256
- `stage-4-3` content tree hash와 A/B 동일성
- Unity, OS, CPU, GPU, power mode, 해상도, quality, backend
- run 순서, 고정 60초 idle의 실제 종료·시작 시각, warmup, sample과 Tick interval
- run ID와 UUID artifact 디렉터리 매핑 및 lane exit status
- 모든 시도 및 제외·재시도 이유
- raw evidence/build 경로와 hash
- run별 median/p95/p99, 상태별 중심값·범위, pair와 bundle delta
- frame/CPU/GPU 및 GC availability
- historical Cleanup terminal status와 bundle 성능 판정 분리
- 기능 baseline/replay 연결
- 최종 판정과 아래 비주장
- 실행한 테스트, 실행하지 않은 테스트와 이유

## 12. 허용 주장과 비주장

허용 예시:

> 동일 장비의 `stage-4-3` neutral workload에서 B의 whole-Tick p95 중심값은 A 대비 X%였으며, 최종 cohort의 pair들은 Y% ... Z%였다. 이는 19개 통합 bundle의 러프한 개선/중립/회귀 신호이며 개별 최적화의 기여나 통계적 유의성을 의미하지 않는다.

다음은 주장하지 않는다.

- 개별 최적화 또는 특정 patch가 X% 개선·악화했다.
- 모든 Stage, 장비 또는 실제 FPS가 같은 비율로 개선됐다.
- 결과가 통계적으로 유의하다.
- 유효성 확인이 없는 GC 값이 0 allocation을 증명한다.
- bundle 결과가 historical Cleanup S3 Hold를 해제하거나 Cleanup 단독 효과를 증명한다.
- 19개 개선율을 과거 측정과 합하거나 곱할 수 있다.
- broad full을 실행하지 않고 project-wide/full-lane green이다.

## 13. 후속 확장 조건

명확한 개선 또는 저노이즈 중립이면 기본 `4-3` campaign은 종료한다. `4-2`는 더 많은 Stage를 측정했다는 모양을 갖추기 위해 자동 추가하지 않는다. 다만 다음 중 하나가 발생해 Stage 민감도 확인이 실제 판단을 바꿀 때만, 동일한 A-B-B-A 규칙의 **별도 `stage-4-2` 보조 campaign**을 승인할 수 있다.

1. `4-3`이 최대 8-run 이후에도 고노이즈 또는 방향 상충으로 `판정 불가`다.
2. `4-3`에서 회귀 의심이 재현됐고, 후반 Stage 전반인지 `4-3`의 AI/Summon 가능 경로에 한정되는지 구분해야 한다.
3. 같은 Stage content hash인데도 `4-3`의 attempted/executed Tick 수가 A/B 사이 달라 비교가 중단됐으며, `4-2`에서는 동등한 Tick workload를 구성할 수 있다.
4. `4-3`에서 개선 신호가 나왔고 그 신호가 Tile/entity-heavy workload까지 넓게 유지되는지 확인할 필요가 있다. 단, `4-3`이 저노이즈 중립이면 비용 제한 원칙에 따라 종료한다.

`4-2` 결과는 `4-3`과 별도 표·별도 판정으로 남긴다. 서로 반대 방향이면 평균으로 상쇄하지 않고 `Stage 의존적 / 원인 분할 필요`로 판정한다. 그 뒤에도 원인 범위가 반드시 필요할 때만 전체 19개가 아니라 Simulation과 Presentation/UI 두 bundle로 한 번 분할하거나, build-once/run-many 외부 orchestration을 별도 승인 범위에서 검토한다.

`4-2` 보조 campaign은 먼저 양쪽 runner를 원본 SHA로 복원한 뒤, 같은 세 literal만 `stage-1-1 -> stage-4-2`로 바꾼 별도 Stage-only patch를 사용한다. `4-3` patch 위에 누적 변경하지 않는다. patch/runner SHA, allowlist, slot 전 diff, requested/primed Stage 확인과 종료 후 원복 계약은 §3.3·§6·§7을 동일하게 적용한다.

이번 문서가 승인하는 도구 변경은 §3.3의 임시 `4-3` Stage-only patch, 위 조건부 `4-2` Stage-only patch와 각 측정 후 원복뿐이다. 영구 runner/runtime 변경, 새 계측 구현 또는 추가 worktree 생성은 승인하지 않으며 그 밖의 후속 확장은 현재 계획의 결과를 확인한 뒤 결정한다.
