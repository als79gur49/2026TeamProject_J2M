# Gameplay 최적화 main 통합 Closeout

- 작성일: 2026-09-14 KST
- 상태: **선별 제품 최적화 main 통합 및 원격 반영 완료**
- 최초 제품 통합 revision: `b77765bde18f0575db00bc3633be3d00d284980b`
- 2026-09-14 후속 병합·push 확인 revision: `40a16e6139b3a6d7ba7fe8eabc26b54db0089976`
- 후속 revision tree: `2cad057bb3d854c3612b9e73a00a9bf38180cfd6`
- 원격: 위 확인 시점에 로컬 `main`과 `origin/main`이 후속 revision과 일치. 이후 문서 커밋으로 이동할 수 있는 ref의 영구 상태를 뜻하지 않는다.
- 조사 기간: 2026-08-16 00:00 KST ~ 2026-09-14 조사 시점

## 1. 현재 결론

최근 30일의 로컬·원격 ref를 갱신해 다시 조사한 결과, 통합 대상으로 선별한 제품 최적화와 별도 요청된 2026-08-29 Cleanup candidate index/ordered executor는 모두 `main`에 반영됐다. 2026-09-14 push 확인 시점의 `main`, `origin/main`, `perf/gameplay-opt-main-port-20260913`은 `40a16e613`과 같은 tree를 가리켰다.

이 결론은 저장소에 남은 모든 실험·계측 branch를 제품 코드로 채택했다는 뜻이 아니다. 비채택 비교안, capture 전용 계측, evidence 도구와 인접 기능 수정은 아래 §5처럼 별도로 분류한다.

## 2. 통합 방식과 반영 범위

기준 `main` `47cbea9fa6f7a02b13e71ce41d179d5307190ca8` 위에 의도별 이식 commit을 구성하고, 통합 branch `perf/gameplay-opt-main-port-20260913`의 `b77765bde`를 로컬 `main`에 fast-forward한 뒤 일반 push로 `origin/main`에 반영했다. merge commit이나 force push는 만들지 않았다.

| 영역 | 현재 main 반영 내용 | main commit |
|---|---|---|
| Pause | reflection metadata cache | `4e9d1189e` |
| Cleanup | immutable ordered candidate index | `e6ac8e5e5` |
| Cleanup | ordered candidate executor | `153d9fa15` |
| Snapshot | Tile cell immutable index 공유 | `73f44d872` |
| Snapshot | 동일 mutation epoch snapshot 재사용 | `b431a6b77` |
| Enemy AI | CrossLOS entity copy 제거 | `3e6b28099` |
| Stage/Wall | authored Wall identity와 Solid 계약 | `b0497c5c0` |
| Presentation | static Wall target cache | `0b7e42615` |
| Presentation | committed-frame scratch 재사용 | `e3812f1e0` |
| Simulation test | snapshot 구조 진단 | `323e889b7` |
| HUD | 비표시 PlayerStatus 경로 제거 | `a4ec81e2a` |
| HUD | Objective 상태 API 단일화 | `c2ca21309` |
| HUD | 비표시 PlayerHud 조회 제거 | `ed8c68b97` |
| HUD | Chance 표시 상태 단일화 | `d92b39f42` |
| Save/HUD | 공유 검증 세션 | `d16db3b46` |
| HUD | Chance 변경 lifecycle | `77eefb0b0` |
| HUD | dependency revision query 재사용 | `1ce175687` |
| 문서 | 제거 후 UI/HUD 구조 동기화 | `3a04407f0` |
| Simulation test | allocation 계측 fail-open 방지 | `b77765bde` |

후속으로 아래 두 커밋을 `main`에 fast-forward 병합하고 일반 push했다. 최초 제품 최적화 19개와 측정 지원 변경을 구분한다.

| 후속 영역 | 반영 내용 | commit |
|---|---|---|
| Capture Diagnostics | 측정 Player 컴파일에 필요한 assembly·UI 참조와 `.meta` 복구. 구현은 `VECTORQUAKE_CAPTURE_BUILD` 조건부 | `cc29c09e3` |
| 측정 도구 | 명시적 `cpu-tick-v1`, GPU coverage 분리, CLI/context/canonical 판정 연결·테스트·문서. 기본값은 `strict-v1` 유지 | `40a16e613` |

기존 `main`에 이미 있던 driver lookup cache `261f2df07`과 Pause 검색 List/재진입 buffer 재사용 `c150d0cc3`도 최종 revision의 조상이다.

## 3. Cleanup historical Hold와 현재 production 상태

2026-08-27~29 Slice 3 문서의 `Hold — valid evidence incomplete`는 당시 official S3-A Player evidence의 적격성 판정이다. 그 판정을 소급해 PASS로 바꾸지 않는다. 이후 통합에서는 제품 구현을 최신 `main` 계약에 맞춰 별도로 채택했다.

- 원본 candidate index `a2ee11779`는 `e6ac8e5e5`와 stable patch-id가 같다.
- 원본 ordered executor `7899692d5`는 최신 `CleanupSlice3Diagnostics`와 reference-oracle 구조에 맞춘 `153d9fa15`로 적응 이식됐다.
- 현재 executor는 removal, timer, immediate-transition candidate의 immutable span을 ordered 순회한다.
- 중립 256 entity 측정의 candidate visit `0/0/0`, whole-Tick p95 `7.104 -> 6.820 ms` 약 `-3.99%`는 참고 결과다. 공식 판정은 속도 향상 확정이 아니라 `5%` 비회귀 통과였다.

따라서 historical evidence Hold를 현재 production이 full scan이라는 뜻으로 읽으면 안 된다. 동시에 현재 반영 사실을 새로운 official Player 성능 개선 증명으로 확대해서도 안 된다.

## 4. 검증과 측정 결과

### 리비전별 기능 검증

최초 제품 통합 `b77765bde`의 기록에는 Snapshot focused `2/2`, HUD focused `87/87`, Save/Chance/UIAccess focused `99/99`, Replay `140/141`이 있다. Replay 1건은 기준 main에서도 같은 기대값 실패로 재현됐다. 이는 당시 bounded 근거이며 후속 HEAD에서 모든 focused 테스트를 다시 실행했다는 뜻은 아니다.

후속 `40a16e613`에 대응하는 검증은 다음과 같다. Core는 해당 커밋을 생성한 pre-commit에서 동일 소스/index로 실행했고, UI는 커밋 후 병합 전에 실행했다. 두 lane 모두 D의 해당 작업트리 `./run_tests.sh`를 사용했다.

| 명령 | 실제 결과 | evidence |
|---|---|---|
| `./run_tests.sh core` | EditMode 290 통과; PlayMode 108 통과·4 건너뜀·실패 0 | `/mnt/d/J2M/evidence/cpu-tick-policy-commit-20260914` |
| `./run_tests.sh ui` | EditMode 1414 통과·건너뜀 0·실패 0 | `/mnt/d/J2M/evidence/cpu-tick-main-merge-20260914` |
| 정책 도구 테스트 | admission 30, v4 hardening 11, manifest 19: 총 60 통과 | `/mnt/d/J2M/evidence/cpu-tick-policy-20260914` |

정책 테스트는 커밋 전 동일 구현에서 실행한 근거다. 최초 core 기록의 `112/112` 표현을 후속 PlayMode 112개 전부 통과로 읽지 않는다. 최신 XML의 통과 108·건너뜀 4를 따른다. broad unfiltered full, 수동 Editor Prefab 시각 검증은 이번 후속에서 실행하지 않았다. 저장소 전체 회귀 종료를 주장하지 않는다.

### 통합 묶음 A/B

[원 A/B 계획](./Gameplay-Optimization-Bundle-AB-Plan-2026-09-14.md)은 실행 전 기준을 보존한다. 실제 비교는 A=`47cbea9fa`, B=`cc29c09e3`으로, B에 최초 19개 통합과 capture assembly 복구가 포함됐다. 새 CPU 캠페인은 이후 `40a16e613`로 커밋된 동일 정책 도구를 양쪽에 적용했다. 따라서 최종 커밋 SHA 자체를 원 측정의 B SHA로 바꿔 기록하지 않는다.

| 캠페인 | 실행·관측 | 판정 | evidence 폴더(`/mnt/d/J2M/evidence/` 아래) |
|---|---|---|---|
| 초기 4-3 / r2 | 저장 경로 문제 / capture assembly 누락으로 비교 미완료 | INCOMPLETE | `gameplay-opt-bundle-ab-20260914`, `gameplay-opt-bundle-ab-20260914-r2` |
| strict 4-3 r3 | 채택 8회, Tick p95 −27.60%; A 편차 12.73% | INDETERMINATE. 이전 decisive 표현은 재감사에서 정정 | `gameplay-opt-bundle-ab-20260914-r3/reaudit-20260914` |
| strict 4-2 | A1 채택, B는 GPU count 조건으로 제외. 추가 승인 retry도 제외 | INCOMPLETE | `gameplay-opt-bundle-42-20260914` |
| cpu-tick-v1 4-2 | 첫 시도 4회 채택, A–B–B–A | IMPROVEMENT_SIGNAL | `gameplay-opt-cpu42-20260914` |
| cpu-tick-v1 4-3 | 첫 시도 4회 채택, A–B–B–A | IMPROVEMENT_SIGNAL | `gameplay-opt-cpu43-20260914` |

| 새 CPU 캠페인 지표 | 4-2 A → B (ms) | 변화 | 4-3 A → B (ms) | 변화 |
|---|---:|---:|---:|---:|
| Tick p95 | 7.204 → 5.318 | −26.17% | 8.351 → 6.224 | −25.47% |
| Tick median | 5.282 → 3.612 | −31.61% | 6.502 → 4.252 | −34.60% |
| CPU main p95 | 8.034 → 5.809 | −27.69% | 9.364 → 7.119 | −23.97% |
| 프레임 간격 p95 | 8.039 → 5.816 | −27.66% | 9.375 → 7.124 | −24.01% |

각 수치는 실행별 요약값을 상태별로 중앙값 집계한 것이다. 4-2 Tick p95 편차 A/B는 0.23%/5.82%, 4-3은 2.60%/5.76%다. 상세 원값·해시·계산은 각 폴더의 `result.md`, `cohort-4.json`, `measurement-identity.json`에 고정됐다. 원본 로그와 빌드는 Git에 포함하지 않으며 build는 `/mnt/d/J2M/builds/`에 보존한다.

### 해석 한계와 남은 검증

허용하는 결론은 **동일 장비의 두 neutral-tick 부하에서 통합 후 Tick p95가 약 25% 감소하는 개선 신호**다. 소수점 개선율은 계산 정밀도이며 효과의 확정 정밀도가 아니다.

- 동일 i5-13500/RTX 4060 Ti, Unity 6000.3.11f1, Windows Mono ReleaseLikeCapture, D3D11, 1920×1080, VSync 0, targetFrameRate −1. 실행 전 60초 대기, warmup 120, phase당 600 frame, 6 frame당 1 Tick, 실행당 100 Tick이다.
- 스테이지별 A 2회/B 2회이며 단일 장비·짧은 구간이다. 장시간 발열·GC·간헐 지연, 이동·전투·전환·UI 조작이 섞인 전체 플레이를 대표하지 않는다.
- 콘텐츠 tree·Stage marker·Tick 수는 대조했지만 실제 world entity/enemy/summon cardinality와 최종 simulation hash 동등성은 이 capture에서 직접 입증하지 않았다.
- ±5% band와 10% 편차 gate는 사전 운영 기준이며 신뢰구간·통계적 유의성 기준이 아니다. 4-3 보조 Tick p99의 B 편차는 10.81%여서 안정적 개선율로 확정하지 않는다.
- 프레임 간격 p95도 감소했으나 그 역수를 평균 FPS 증가율로 해석하지 않는다. GPU 여유가 있어도 전체 CPU 프레임 중 Tick 비중과 빈도에 따라 효과가 달라진다. GPU 개선·비회귀와 전체 allocation 감소는 판정하지 않았다.
- 정책은 GPU partial을 CPU 실패와 분리했고 CPU/Tick·identity·잘못된 GPU 데이터 검증은 유지했다. 4-2에는 GPU partial이 있었고 4-3은 양 phase 모두 600/600이었다. 샘플 수 충족은 서로 다른 GPU frame이나 위상 정합성의 증명이 아니다.
- 각 실행의 성능 데이터는 채택됐지만 별도 cleanup 최종 상태는 `HOLD_CLEANUP_ADMISSION`이다. 기존 strict 판정이나 historical Cleanup Hold를 소급해 성공으로 바꾸지 않는다.
- 캠페인·리비전·지표가 다른 이전 측정을 합산하지 않는다. 개별 최적화 기여, 실제 평균 FPS 증가율, 모든 스테이지·장비의 동일 효과는 미검증이다. 더 정밀한 주장이 필요하면 대표 입력 재현, 긴 수집 구간, 충분한 독립 반복·별도 세션 확인이 후속 과제다.

기존 Replay 근거는 `/mnt/d/J2M/evidence/gameplay-opt-replay-baseline-20260914/manifest.md`, Snapshot allocation의 제한된 cached-request 검증은 `/mnt/d/J2M/evidence/gameplay-opt-snapshot-allocation-20260914/manifest.md`에 있다. 후자를 전체 Tick allocation 감소로 확대하지 않는다.

## 5. 최근 30일 미포함 항목 재분류

`git fetch --all` 후 2026-08-16~2026-09-14에 도달 가능한 로컬·원격 ref, `main`에 없는 commit, 최적화/성능/캐시/할당/복사/진단 관련 제목과 문서를 조사했다.

### 제품 최적화로 추가 이식할 누락

발견하지 못했다. 2026-08-16~28 구간의 `main` 미포함 commit은 7개이며 Glide 실행 정책 분리, 배포 단축키, URP/plugin release 수정으로 구성돼 이번 gameplay 최적화 범위가 아니다. 2026-08-29 이후 선별된 Simulation·Presentation·HUD·Pause 제품 변경은 현재 코드에 반영되거나 적응 이식됐다.

원본 HUD 이력의 비표시 필드·조회·통지 제거, Objective overload 제거, 진단 비활성 시 객체 생성 방지는 현재 이식 commit들에 합쳐져 있다. 원본 SHA가 직접 조상이 아니라는 이유만으로 누락으로 판정하지 않았다.

### 의도적으로 채택하지 않은 대안

- `codex/pause-comparison-c-20260911`: Pause 검색 비교 C안. 선택된 B안과 상호 대안이므로 미채택.

### 제품 최적화가 아닌 진단·계측·증거 branch

- `perf/motion-detail-20260910`
- `diagnostic/gpu-timing-20260909`
- `diagnostics/projection-list-copy-ab-20260906`
- `refactor/cleanup-slice3-redesign`에 남은 capture/evidence 전용 변경
- `measurement/late-stage-guard-20260909-a`, `measurement/late-stage-guard-20260909-b`

`78525d129`의 explicit diagnostics policy와 `fec269b52`의 bounded `LiveCompact` carrier도 capture/진단 운반 기능이다. authoritative simulation 최적화로 보지 않아 이번 production port에서 제외했다. 향후 상시 진단이 필요할 때 별도 기능·비용 검토 대상으로 유지한다.

### 최적화와 인접하지만 별도 기능 수정

- `fix/pause-reinitialize`의 `62bfd4b0f`: 재초기화 시 Pause 상태와 복원 정보를 보존하는 기능 수정이다. 최적화 누락은 아니며, 전용 T11/PlayMode 계약과 함께 별도 병합 판단이 필요하다.

## 6. 문서 우선순위

현재 통합 상태는 이 closeout이 소유한다. 아래 문서는 작성 당시 입력, 승인 경계와 측정 판정을 보존하는 historical 자료다.

- [원 통합 계획](./Gameplay-Optimization-Integration-Plan-2026-09-11.md)
- [원 실행 프롬프트](./Gameplay-Optimization-Integration-Execution-Prompt-2026-09-11.md)
- [Slice 3 Goal Plan](./Gameplay-Wall-Tick-Cost-Optimization-Slice3-Goal-Plan.md)
- [Slice 3 S3-A 재감사 수정안](./Gameplay-Wall-Tick-Cost-Optimization-Slice3-S3A-Reaudit-Remediation-Plan.md)

과거 문서의 `Hold`, `full scan`, `미실행` 문장은 당시 상태를 설명한다. 현재 branch/production 상태와 충돌하면 이 closeout을 우선한다.
