# 업적 장애 주입 및 동시성 검증 계획

## 상태와 목적

- 작성 기준: 2026-09-13 통합 검토. 통합 기준은 `8d2983d9217e870f928f8584442d56694909c112`이며, 효율 클리어 업적 13종 구현 커밋 `1b59597be8b1a996e25bbb33a552f414d644805f`를 포함한다.
- **계획 문서이며 아래 신규 장애 주입·부하 시험은 아직 실행하지 않았다.** 실패 가능성은 정적 코드 검토에 따른 가설이다.
- 대상은 레벨 클리어 5종과 현재 시도 기반 효율 클리어 13종의 제품 저장·Steam 전송 경로다.
- TDD 순서로 재현 테스트를 먼저 작성하고, 현재 코드의 실패 증거를 남긴 뒤 방어 코드를 수정한다. 이미 방어되는 사례는 통과 대조군으로 유지한다. 테스트가 통과하면 억지로 실패시키거나 결함으로 보고하지 않는다.
- 1차는 실제 제품 코드에 fake API, 저장 실패, 제어된 실행 순서를 주입한다. 실제 Steam 네트워크 단절·계정 해금·두 Player 프로세스 실행은 후속 검증이며 1차 결과로 대체하지 않는다.

## 현재 방어와 검토 가설

- `ProductAchievementCoordinator`는 lock, earned 중복 검사, 업적별 in-flight 등록으로 상태 변경과 중복 전송을 보호한다. 제품 earned/pending 저장이 성공한 뒤 publisher를 호출한다.
- `SteamAchievementPublisher`는 한 배치씩 FIFO 처리하며, 배치당 후보들을 Set한 뒤 한 번 StoreStats를 호출한다. 정확한 AppID·업적 이름·완전 해금 콜백을 확인하고 중복·늦은 콜백을 제한한다.
- StoreStats 성공 반환 후 콜백 대기는 monotonic clock 기준 30초다. 타임아웃·일부 API 예외·준비 상태 상실 시 세션을 중단하고 pending을 다음 실행까지 보존한다. 이 타임아웃은 동기 API 호출 자체의 멈춤을 취소하는 장치가 아니다.
- publisher의 상태 확인과 네이티브 호출 전체가 하나의 lock으로 보호되지는 않는다. API 호출 중 종료가 재진입하거나 다른 스레드의 종료가 끼어들면 이후 API 호출이 계속될 가능성을 검증해야 한다.
- `forceSingleInstance: 0`이고, 저장 경로에 프로세스 간 잠금·상태 병합은 확인되지 않았다. 원자적 파일 교체만으로 독립 작성자의 오래된 상태 덮어쓰기를 막을 수 있는지는 별도 문제다.
- publisher 큐 자체에 명시적 용량 제한은 없다. 다만 정상 제품 경로는 카탈로그 18종과 earned/in-flight 중복 방어로 요청 증가가 제한된다. publisher 직접 호출 폭주는 정상 gameplay 부하와 구분한다.
- 기존 자동 테스트의 통과는 실제 다중 스레드 스트레스·네트워크 단절 시험을 수행했다는 의미가 아니다.

## 1차 사례와 통과 기준

| ID | 사례 | 주입 방식 | 통과 기준 | 실행 전 예상 |
| --- | --- | --- | --- | --- |
| FI-01 | 전송 도중 종료 | A·B 배치에서 A의 SetAchievement hook 안에서 Dispose 후 실행 재개 | Dispose 반환 이후 새 SetAchievement·StoreStats 진입 0회, 배치 완료 통지 1회, pending 보존 | 실패 가능성 높음 |
| FI-02 | 동일 파일의 복수 작성자 | 독립 coordinator 두 개가 동일 빈 파일을 먼저 읽고 각각 A·B 지급 | 두 지급이 모두 성공했다면 재로드한 earned/pending에 A+B 존재 | 실패 가능성 높음 |
| FI-03 | 같은 업적 동시 요청 폭주 | 8개 작업자가 같은 업적에 총 10,000회 Earn | EarnedNew 1회, 나머지 AlreadyEarned, 제품 저장 1회·publisher 호출 1회, 예외·교착 없음 | 통과 예상, 병렬 검증 필요 |
| FI-04 | 여러 업적의 겹치는 동시 요청 | 8개 작업자가 18종의 겹치는 부분집합을 총 10,000회 EarnBatch | 최종 earned/pending 정확히 18종, 신규 지급 결과 합집합 18종·중복 없음, 전송 후보 중복 없음 | 통과 예상, 병렬 검증 필요 |
| FI-05 | 전송 직후 연결 상실·콜백 누락 | StoreStats=true 후 LoggedOn=false 및 콜백 차단, fake clock 진행 | 30초 전 성급한 완료 없음, 기한 도달 시 해당 세션 중단, pending 유지, 다음 실행 재전송 | 기존 방어 확인 대조군 |
| FI-06 | 일부 성공·중복·지연 콜백 혼합 | A 성공 콜백 반복, B 누락, 타임아웃 뒤 B 및 외부 AppID/업적 콜백 주입 | A는 Submitted, B는 미확인 실패, pending 유지, 완료 중복·다른 작업 오염·중단 세션 재활성화 없음 | 기존 방어 확인 대조군 |
| FI-07 | 제품 업적 저장 실패 | repository가 실패 결과 반환 또는 IOException 발생 | earned/pending 메모리 미반영, publisher 호출 0회, 저장된 캠페인 클리어 및 terminal 흐름 유지 | 기존 방어 확인 대조군 |
| FI-08 | pending 제거 저장 실패 | 새 실행의 AlreadySatisfied 확인 뒤 pending 제거 저장만 실패 | earned와 기존 pending 보존, 다음 실행에서 재확인·저장 성공 시 pending 제거 | 기존 방어 확인 대조군 |

### 재현 세부 조건

**FI-01 — 먼저 재진입, 다음에 실제 스레드 경합**

- 기존 `FakeSteamAchievementApi.SetAchievementAction`을 사용한다. 이미 진입한 A 호출은 허용하되 Dispose 반환 이후 B 또는 StoreStats가 새로 호출되는지 이벤트 순서로 판정한다.
- 이어서 A 호출을 barrier로 멈추고 다른 스레드에서 Dispose를 수행한 다음 재개한다. 네이티브 API를 대신하는 fake만 worker에서 실행하며 실제 Steam API의 다중 스레드 호출 시험으로 표현하지 않는다.
- 직접 publisher 시험의 완료 결과와, coordinator까지 연결했을 때 pending 보존을 구분해서 확인한다.

**FI-02 — 파일 손상과 갱신 유실을 구분**

- 격리된 같은 저장 경로에 실제 파일 repository 두 개와 coordinator 두 개를 생성한다. 둘 다 초기 상태를 읽은 뒤 A 저장 성공 → B 저장 성공 순서를 고정한다.
- 마지막 작성자의 메모리만 보지 않고 세 번째 repository로 최종 파일을 다시 읽는다. 유효한 JSON이어도 A가 사라지면 실패다.
- 1차는 독립 프로세스의 오래된 읽기 상태를 저장 계층에서 재현한다. 실제 두 Player 실행 방지 여부까지 증명하지 않는다.
- 후속 수정이 단일 실행을 강제하는 방식이면 두 번째 실행이 저장 전에 거부되는 통합 테스트도 필요하다. 두 성공 결과 뒤 데이터가 사라지는 현상 자체를 통과로 바꾸지 않는다.

**FI-03·04 — 정상 요청 경로의 부하**

- 빈 제품 문서에서 시작하고 저장·전송은 정상 응답하게 한다. unavailable publisher 또는 제어된 callback sink를 사용해 미전송 pending을 관찰한다.
- FI-04의 개별 배치 내부에는 중복 ID를 넣지 않는다. 현재 계약상 배치 내부 중복은 InvalidAchievement이며, 배치 간 중복과 다른 사례다.
- coordinator 부하 시험에서는 스레드 안전한 repository/sink 대역을 사용한다. 실제 파일 I/O 비용은 별도 측정한다.
- FI-04에서 저장 횟수는 스케줄에 따라 달라질 수 있다. 정확히 1회를 요구하지 않고 신규 지급이 있는 배치 수와 일치하며 18회 이내인지 검사한다.
- 전체 실행시간, 저장·전송 횟수, 최대 in-flight, worker 예외를 기록한다. 측정 전 임의의 FPS·밀리초 기준을 회귀 판정으로 만들지 않는다.

**FI-05~08 — 실패를 유실이나 잘못된 성공으로 처리하지 않는지 확인**

- 타임아웃은 실제 30초를 기다리지 않고 fake clock의 29.999초와 30초 경계를 사용한다. 실제 인터넷을 끊었다고 보고하지 않는다.
- FI-05에서 다음 실행을 나타내는 새 coordinator/publisher를 구성하고, locked pre-read면 재전송 및 pending 유지, 이후 새 실행에서 AlreadySatisfied면 pending 제거를 확인한다.
- FI-06은 A의 콜백 성공만으로 B 또는 전체 배치를 성공 처리하지 않는지 검사한다. Submitted는 pending 제거 근거가 아니다.
- FI-07은 저장 실패 반환과 예외를 모두 다룬다. 캠페인 저장 자체의 실패 시 지급 호출 0회인 기존 회귀 테스트도 함께 실행한다.
- FI-08의 pending 제거 저장이 실패해도 획득 사실을 취소하거나 원래 pending을 메모리에서만 지우면 실패다.

## 현재 정책상 정상인 동작

다음은 정책을 바꾸기 위한 별도 요청이 없는 한 결함 재현의 assertion으로 사용하지 않는다.

- 연결이 돌아와도 중단된 Steam publication session을 같은 application lifetime에 새 세션으로 교체하거나 자동 반복 재시도하지 않는다. pending은 다음 정상 실행에서 처리한다.
- 캠페인 저장 성공 후 제품 저장 실패 또는 두 저장 사이 종료 시, 신규 효율 업적은 과거 기록에서 복구하지 않는다. 조건을 만족하는 재클리어가 필요하다.
- 이미 제품 earned/pending이 저장된 업적의 다음 실행 전송·확인은 과거 캠페인 기록에 대한 신규 소급 지급과 다르며 유지한다.
- 기존 레벨 업적 5종의 기록 복구는 유지한다. 과거 비활성 업적 변환·삭제는 추가하지 않는다.

## 테스트 작성과 증거 수집

1. FI-01 → FI-02 → FI-03·04 → FI-05~08 순서로 테스트를 작성한다. 이 문서 작성 단계에서는 테스트나 방어 코드를 변경하지 않는다.
2. 고정된 실행 순서를 사용한다. 임의 Sleep 대신 barrier/event로 조회·저장·API 진입·종료 순서를 제어한다. 동시 요청 데이터가 필요하면 고정 seed를 사용한다.
3. fake의 카운터·호출 목록·callback 보관소는 스레드 안전하게 만든다. worker의 결과와 예외는 수집 후 테스트 스레드에서 assertion한다.
4. 모든 대기에 유한 timeout을 두고 `finally`에서 barrier 해제·worker 종료·테스트 소유 자원 정리를 보장한다. timeout 발생 시 제품 교착과 테스트 준비 실패를 구분해 기록한다.
5. 수정 전 실패 assertion, 호출 이벤트 순서, 실행 결과 XML, 기준 HEAD와 작업 트리 diff/hash를 보존한다. 재현되지 않은 가설은 미재현으로 보고한다.
6. 실패 원인을 확인한 후 필요한 방어만 수정한다. 같은 테스트를 다시 통과시키고 기존 관련 fixture와 core를 실행한다. 통과 대조군은 기존 계약을 유지하는지 확인하는 용도로 사용한다.

저장 시험과 결과 증거는 `/mnt/d/J2M/evidence/achievement-fault-injection/<run-id>/`에 둔다. 실제 사용자 Saves, 기존 테스트 증거, Steam 계정 업적을 변경하지 않는다. Player 빌드가 필요한 후속 단계의 산출물은 `/mnt/d/J2M/builds`에 둔다. 새 worktree가 필요하면 저장소의 D-drive 정책과 `j2m-worktree-add`를 따른다.

테스트 lane은 검증할 작업 트리에서 `./run_tests.sh`로 실행한다. 대상은 coordinator, publisher, publication session/integration, repository, CampaignStageFlow와 새 장애 fixture다. 정확한 fixture 목록·필터·lane별 결과를 기록하며, 관련 filtered full 통과를 broad unfiltered full 통과로 표현하지 않는다.

문서 작성 시점에는 신규 테스트·부하 측정·실제 연결 단절을 실행하지 않았다. 향후 결과는 사례별 `NotRun / ReproducedFailure / Passed / Inconclusive`로 기록하고, 의도한 정책의 관찰과 방어 결함을 구분한다.

## 후속 실환경 검증

- 1차 자동 재현 후 테스트용 Player·계정·저장 경로에서 전송 전후 연결 단절, 재접속, 종료·재실행을 검증한다.
- 두 Player 프로세스가 같은 테스트 저장 경로에 접근하는 경우를 별도로 검증한다.
- 느린 파일 저장·프로세스 중단·대용량 큐 측정은 필요 시 추가한다. 실제 디스크 전체를 채우거나 다른 프로그램의 네트워크를 끊는 방식은 사용하지 않는다.
- fake 실패 주입 통과를 실제 Steam 서버 영속성·네트워크 복구 성공으로 보고하지 않는다.

## 관련 코드와 문서

- [제품 업적 계약](../Architecture/Product-Achievement-Foundation.md)
- [제품 coordinator](../../Assets/_Features/Achievements/Achievement_Domain/Runtime/ProductAchievementCoordinator.cs)
- [Steam publisher](../../Packages/com.j2m.platform.steam/Runtime/ProductAchievements/SteamAchievementPublisher.cs)
- [Steam API 대역](../../Packages/com.j2m.platform.steam/Tests/EditMode/FakeSteamAchievementApi.cs)
- [원자적 파일 저장](../../Assets/_Features/Stages/Runtime/Campaign/Save/AtomicTextFileStore.cs)
- [검증 lane 운영](Gameplay-Test-Automation-Guide.md)
