> Operational guide.
> Canonical architecture and gameplay behavior references start at [Docs/Architecture/README.md](../Architecture/README.md), with [Docs/Architecture/Tick-Simulation-Canonical-Spec.md](../Architecture/Tick-Simulation-Canonical-Spec.md) and [Docs/Architecture/Gameplay-Rules-Appendix.md](../Architecture/Gameplay-Rules-Appendix.md) as the active spec/rules source.

# Gameplay Test Automation Guide / 게임플레이 테스트 자동화 가이드

> This document is intentionally bilingual. Korean guidance is added for local developer readability, and the English original is preserved to avoid meaning drift during translation.
>
> 이 문서는 의도적으로 한영 병기 형태를 유지한다. 한국어 설명은 로컬 개발자 가독성을 위한 것이고, 번역 과정에서 의미가 달라지는 일을 막기 위해 영어 원문을 함께 남긴다.

## Current Validation Baseline / 현재 검증 기준점
### 한국어
- 현재 환경에서는 `./run_tests.sh core`와 `./run_tests.sh full`이 실제로 실행 가능하다.
- Stage 9 UI hardening 검증용으로 `./run_tests.sh ui` 경로를 유지한다. 이 경로는 UI EditMode assembly만 대상으로 하는 집중 검증용이며 Stage 4–8 seam preservation evidence를 담당한다.
- 이 섹션의 baseline row는 pinned snapshot reference다. 서로 다른 날짜 artifact를 한 validation claim으로 합산하는 근거가 아니다.
- 현재 기준점은 다음과 같다.
  - `./run_tests.sh core`: green, Core EditMode `13 total / 0 failed`, Core PlayMode `2 total / 0 failed`
  - `./run_tests.sh ui`: Climate PR2 code-head reference green on 2026-07-26 KST, Windows UI build `0` errors, Unity UI EditMode `1060 total / 0 failed`; code-head visual evidence is `TestLogs/TypographyVisualQA/CommandLine-20260726-052954/` at revision `840a5cd2fe0c1460a0a47fc34d8e020f73c76abf`, with six canonical PNGs and three separate `Diagnostics/` PNGs
  - `./run_tests.sh full`: red, Unity Full EditMode `703 total / 101 failed`
  - Unity Full PlayMode는 EditMode failure 때문에 아직 실행되지 않았다.
- 현재 fullscreen cursor confinement touched slice는 2026-08-15 KST에 `./run_tests.sh ui`로 재검증했으며, Windows UI build와 Unity UI EditMode `1386 total / 0 failed`가 통과했다. 이는 위 pinned snapshot row를 대체하거나 서로 다른 날짜 artifact를 합산하는 주장이 아니다.
- 현재 KBO Dia Gothic typography migration은 2026-09-05 KST에 `./run_tests.sh ui`와 `./run_tests.sh core`로 검증했다. UI는 Windows build와 Unity EditMode `1348 total / 0 failed`, core는 EditMode `217/0`과 PlayMode `111 total / 107 passed / 4 skipped / 0 failed`가 통과했다. ko-KR의 큰 글자·강조 역할 10개는 Medium, 나머지 제목·본문 역할 9개는 Light를 사용하고, 기존 theme 참조 GUID와 TMP material/atlas local ID를 보존했다. managed Korean glyph의 missing/fallback은 각각 `0`이다. 이 결과는 위 pinned snapshot row를 대체하지 않는다.
- 현재 localization string-governance Phase 3 review hardening은 2026-09-14 KST에 `./run_tests.sh ui --filter LocalizationStringGovernance`로 `36/36`, 공식 `./run_tests.sh ui`로 Windows UI build와 Unity EditMode `1435/1435`를 통과했다. package-free lifecycle validator와 read-only Unity adapter가 data-only 세 번째 ShipReady locale 및 실제 en-US/ko-KR UI/Stage 상태를 같은 규칙으로 검증하고, 공개 dictionary view는 downcast mutation을 거부하며, 기존 exact-copy 계약은 별도로 유지된다. core/full, Player, 성능, 메모리, CJK 콘텐츠/폰트 검증은 실행하지 않았고 이 결과는 pinned snapshot 또는 project-wide green을 뜻하지 않는다.
- 현재 localization/typography CJK Phase 4 runtime-selection은 2026-09-14 KST에 focused policy/Unity adapter/production registration fixture `17/17`, `50/50`, `7/7`과 공식 `./run_tests.sh ui`의 Windows UI build 및 Unity EditMode `1445/1445`를 통과했다. 네 locale 진입 경로는 하나의 package-free policy를 사용하고, active provider의 등록 locale은 exact canonical ShipReady en-US/ko-KR뿐이며, production Localization/Addressables asset tracked diff는 비어 있다. core, broad full, Player, 성능, 메모리, glyph, font residency, visual QA는 실행하지 않았으며 이 결과는 project-wide green을 뜻하지 않는다.
- 현재 localization/typography CJK Phase 5 ordered-autonym option runtime은 2026-09-14 KST에 focused catalog/foundation/production/integration/architecture/governance fixture `21/21`, `74/74`, `42/42`, `53/53`, `62/62`, `12/12`와 공식 `./run_tests.sh ui`의 Windows UI build 및 Unity EditMode `1464/1464`를 통과했다. Catalog가 canonical identity/raw autonym/lifecycle/order를 소유하고 package-free supported list는 membership으로만 사용되며, Settings는 unique current match와 actual-current 확인을 거쳐 raw autonym 하나를 current-locale typography로 표시한다. 최초 structural red `61 total / 1 failed`와 behavior red `20 total / 1 failed` 뒤의 supplemental replay는 retrospective였다는 chronology deviation을 기록하며, 사용자가 이를 명시적으로 승인해 closeout을 진행했다. core, broad full, Player, 성능, 메모리, glyph, font residency, packed Addressables, visual QA는 실행하지 않았고 이 결과는 project-wide green을 뜻하지 않는다.
- 현재 localization/typography CJK Phase 6 unified persistence/fallback runtime은 2026-09-14 KST에 focused catalog/foundation/production/integration/architecture/governance fixture `29/29`, `82/82`, `42/42`, `65/65`, `66/66`, `12/12`와 공식 `./run_tests.sh ui`의 Windows UI build 및 Unity EditMode `1496/1496`를 통과했다. 구조 red는 `65 total / 3 failed`, behavior red는 `57 total / 5 failed`였고 후자의 네 의도된 persistence assertion-red 외 full-injection factory-count 동반 실패 하나도 실행 기록에 공개했다. core, broad full, Player, 성능, 메모리, glyph, font residency, packed Addressables, visual QA는 실행하지 않았으며 PlayerPrefs 성공은 fsync 또는 atomic durability를 보장하지 않고 이 결과는 project-wide green을 뜻하지 않는다.
- 현재 four-locale Slice A2 selected-startup-health runtime은 2026-09-16 KST에 structural assertion-red `67 total / 1 failed`, valid behavior assertion-red `70 total / 4 failed`, focused integration `70/70`, architecture `67/67`, 실제 Unity string-governance `7/7`, 그리고 공식 `./run_tests.sh ui`의 Windows UI build 및 Unity EditMode `1504/1504`를 통과했다. startup health는 선택된 canonical locale의 Settings title과 canonical Stage display-name 두 항목만 exact lookup하며 emergency-English fallback을 사용하지 않는다. 일반 Resolve의 fallback, SmartFormat, nested descriptor와 Phase 6 apply/health/report-or-rewrite/subscription 순서는 유지된다. 최초 behavior-red `70 total / 5 failed`는 기존 factory-count guard 실패가 섞여 INVALID로 보존했고 clean rerun만 유효 red로 사용했다. 기존 soft-governance warning 출력은 informational로 남으며 warning closure를 주장하지 않는다. PlayMode escalation, core, broad full, Player/build, Memory Profiler/RSS, Addressables/Build Layout, 성능, glyph/font residency, visual QA는 실행하지 않았고 이 결과는 project-wide green을 뜻하지 않는다.
- 현재 Terminal 완료 후 HUD Pause 입력 복구 slice는 2026-09-20 KST에 tests-first로 검증했다. 수정 전 회귀 테스트는 Terminal 완료 직후 HUD Pause 활성 기대에서 실패했고, 구현 후 Coordinator payload/Application presenter/공개면 guard/Installer 통합의 focused `4/0`과 `./run_tests.sh ui`의 Windows build 및 Unity EditMode `1424/0`이 통과했다. Flow 상태는 명시적 `IUIFlowPresentationSource`의 단일 immutable snapshot으로만 공개되고 Composition의 이벤트 순서 의존 pull 동기화는 제거되었다. pre-commit core는 EditMode `290/0`, PlayMode `112/0`이 통과했다. 수동 Player 마우스 검증과 broad `full` lane은 실행하지 않았다.
- 현재 approved-localization Apply transaction hardening은 2026-09-22 KST에 공식 `./run_tests.sh ui`의 Windows UI build 및 Unity EditMode `1528/1528`을 통과했다. Apply는 canonical preflight 뒤 Shared Data/en-US/ko-KR/승인 target semantic manifest를 동결하고, targeted save/import 뒤 전체 managed object graph clean, immutable asset 및 모든 managed `.meta`, source font hash, font persistent identity, Addressables 전체 tree와 root `.meta`를 다시 검사한다. rollback은 변경된 byte와 영향받은 path만 한 번 복구하고 오류를 집계하며 재오염 시 workspace-unsafe로 실패한다. 실제 production Apply E2E fault injection, exact-SHA D-drive 격리 자동화, Player/build, Memory Profiler/RSS, broad full은 실행하지 않았고 이 결과는 해당 범위나 project-wide green을 뜻하지 않는다.
- 현재 comic-sequence 전환 slice는 2026-08-19 KST에 기존 MP4/VideoPlayer 전용 런타임과 70-method legacy mixed suite를 제거하고 13개 공통 라우팅 테스트 및 확장된 만화 회귀 guard로 대체했다. 2026-08-21 KST에는 표시 검증용 임시 아웃트로 Definition을 제거했지만, 2026-09-03 KST 제품 콘텐츠 follow-up에서 실제 6패널 아웃트로 Definition을 추가하고 Gameplay production 씬에 다시 연결했다. 최종 클리어의 Game Clear Main 입력은 `1-1 -> 1-2 -> 1-3 -> 1-4 -> 1-5 -> 1-6` 누적 순서로 재생한 뒤 `ComicOutroToMainMenu`로 전환하고 성공 시 `OutroComicCompleted`를 기록한다. 같은 working tree에서 `./run_tests.sh ui`의 Windows UI build와 Unity UI EditMode `1340 total / 0 failed`, filtered `full`의 EditMode `0` 및 actual-scene PlayMode `1 total / 0 failed`가 통과했으며, 이는 위 pinned snapshot이나 broad full-lane 결과를 대체하지 않는다.
- 현재 comic-sequence enter-fade follow-up은 2026-08-22 KST에 `./run_tests.sh ui`로 재검증했으며, Windows UI build와 Unity UI EditMode `1348 total / 0 failed`가 통과했다. 진입 시작에는 overlay background를 투명하게 유지해 source scene이 보이도록 하고, dedicated black layer가 authored duration 동안 불투명해진 뒤에만 background를 검정으로 고정하고 첫 페이지 reveal을 시작한다. 이 수치는 위 pinned snapshot row를 대체하지 않는다.
- 현재 runtime-generated shell 분류에서 Main Menu popup layer와 Main Menu Settings overlay의 runtime-created root/backdrop/blocker/content mount는 독립적인 player action을 갖지 않는 infrastructure다. 실제 Confirm과 Settings hierarchy는 각각 authored `ConfirmPopup.prefab`과 `SettingsScreen.prefab`을 사용하므로 migration inventory에서 제외한다. 2026-09-13 KST에 고정 `ComicSequenceOverlayView` base shell은 canonical authored prefab으로 전환했고 Main Menu와 Gameplay production installer가 동일 asset을 직접 참조한다. `EnsureHierarchy()`는 더 이상 고정 hierarchy를 생성하거나 수리하지 않고 잘못된 authored reference를 fail-fast 검증하며, Definition에 따라 수량과 배치가 달라지는 non-interactive panel pool만 runtime infrastructure로 유지한다. direct pointer/Submit exactly-once와 Cancel/lower-layer 차단 계약을 보강했고, `./run_tests.sh ui`의 Windows build 및 Unity EditMode `1384/0`, filtered `full` actual-scene PlayMode `3/0`, `./run_tests.sh core` EditMode `282/0` 및 PlayMode `111 total / 107 passed / 4 skipped / 0 failed`가 통과했다. broad unfiltered `full`과 수동 Editor/Player 시각 검증은 실행하지 않았다.
- 현재 campaign PlayerPrefs retirement / JSON save integration slice는 2026-08-23 KST에 같은 리비전으로 재검증했다. `./run_tests.sh ui`의 Windows UI build와 Unity UI EditMode `1353 total / 0 failed`, `./run_tests.sh core`의 EditMode `217 total / 0 failed`와 PlayMode `109 total / 0 failed`, save architecture/adapter/PlayerPrefs-removal filtered EditMode `54/12/6 total / 0 failed`가 통과했다. UI는 intent·confirmation·recovery presentation만 소유하고 Stages가 JSON persistence·active-slot/local launch state·atomic file lifecycle을 소유한다. 명시적 DirectPlay 임시 상태 삭제는 rollback을 복구하지 않으며, 이 증거는 pinned snapshot을 대체하거나 broad full-lane green을 주장하지 않는다.
- 아래 2026-08-24 typed-committer 및 slot clone/canonicalization 두 행은 당시 과도기 구조의 역사 기록이다. complete replacement, removed mapper, shared canonicalization 설명은 current Phase 5 runtime composition을 설명하지 않는다.
- 현재 campaign save typed-committer/strict-chance follow-up은 2026-08-24 KST의 동일 code 상태에서 재검증했다. `./run_tests.sh core`는 EditMode `217 total / 0 failed`, PlayMode `109 total / 0 failed`; `./run_tests.sh ui`는 Windows UI build와 Unity UI EditMode `1353 total / 0 failed`가 통과했다. Save service/adapter/architecture/mapper/slot-validation-and-DirectPlay 다섯 fixture를 지정한 filtered `full` EditMode는 `209 total / 0 failed`가 통과했다. Gameplay death/clear는 Stages planner + typed committer의 단일 profile transaction을 사용하고, query/lifecycle/maintenance/comic/diagnostic port가 분리되었으며, 임의 slot lambda mutation과 nullable partial maintenance replacement는 완전한 validated slot-document replacement로 대체되었다. 첫 공개 schema 2의 저장·런타임 목숨은 `1..3` 단일 계약이고, 마지막 목숨 소진은 중간 `0` 없이 level-group 첫 stage와 목숨 `3`을 원자적으로 저장한다. DirectPlay invalid chance는 side effect 전에 거절하며 maintenance replacement는 malformed nested state를 normalization 전에 거절하고 기존 슬롯을 보존한다. 같은 필터의 PlayMode는 matching test가 없어 `0`이었고 broad `full` lane은 실행하지 않았으므로 broad recovery claim은 하지 않는다.
- 현재 campaign slot clone/canonicalization follow-up은 2026-08-24 KST의 동일 working-tree code 상태에서 재검증했다. `./run_tests.sh core`는 EditMode `217 total / 0 failed`, PlayMode `109 total / 0 failed`; `./run_tests.sh ui`는 Windows UI build와 Unity UI EditMode `1353 total / 0 failed`가 통과했다. `CampaignSlotMapperTests`, `CampaignSaveSlotStoreAdapterTests`, `SaveSlotValidationAndDirectPlayTests`를 각각 지정한 filtered `full` EditMode는 `25/39/33 total / 0 failed`가 통과했고 각 filtered PlayMode에는 matching test가 없어 `0`이었다. `SaveSlotData.Clone()`과 nested clone은 raw null/invalid/duplicate/order를 보존하는 exact deep copy가 되었고, raw slot의 Invalid/Empty/ValidNonEmpty 분류 및 허용된 canonicalization은 Production/Transient가 공유하는 저장 경계로 이동했다. 겉모양이 비어 있어도 invalid envelope는 Corrupted로 남고 malformed nested shape는 clone 예외 없이 진단 결과에 보존된다. broad `full` lane은 실행하지 않았으므로 broad recovery claim은 하지 않는다.
- 현재 campaign retired pre-release save compatibility cleanup은 2026-08-25 KST의 동일 working-tree code 상태에서 재검증했다. tests-first `SaveSlotValidationAndDirectPlayTests`는 old auto-repair와 runtime policy source가 남아 있어 EditMode `34 total / 2 failed`로 의도대로 red였고, 제거 후 `34/0`이 통과했다. `SaveSlotValidationAndDirectPlayTests,CampaignStageFlowTests` filtered `full`은 EditMode `118/0`, matching PlayMode `0`; `./run_tests.sh core`는 EditMode `217/0`, PlayMode `109/0`; `./run_tests.sh ui`는 Windows UI build와 Unity UI EditMode `1353/0`이 통과했다. 공개 이전 `stage-5-1` cursor는 이제 current final stage로 자동 보정되지 않고 ordinary sequence-missing result로 fail-closed하며 저장 sync/write를 요청하지 않는다. Catalog-only `legacy-stage-5-1` content와 alias/catalog governance는 유지된다. broad unfiltered `full`은 실행하지 않았으므로 broad recovery claim은 하지 않는다.
- 현재 campaign Continue narrow-preparation slice는 2026-08-25 KST의 동일 working-tree code 상태에서 재검증했다. tests-first `CampaignSaveServiceTests`는 새 command/port/result가 없어 Windows build에서 예상한 compile error `7`건으로 중단되었고, 구현 후 EditMode `35/0`, matching PlayMode `0`이 통과했다. `PendingLaunchSlotProviderTests`는 EditMode `29/0`, matching PlayMode `0`; 최종 seven-fixture filtered `full`은 EditMode `286/0`, matching PlayMode `0`; `./run_tests.sh core`는 EditMode `217/0`, PlayMode `109/0`; `./run_tests.sh ui`는 Windows UI build와 Unity UI EditMode `1352/0`이 통과했다. MainMenu Continue는 이제 expected slot/stage/persisted-group을 가진 narrow preparation command만 사용하고, service가 mutation 경계에서 precondition을 다시 확인해 필요한 경우 `CurrentLevelGroupId` 하나만 저장한다. stale/missing/completed는 no-write이고 실패 cleanup은 원래 handoff token만 제거해 newer reservation을 보존한다. broad unfiltered `full`과 별도 manual Player/build smoke는 실행하지 않았으므로 broad recovery claim은 하지 않는다.
- 현재 campaign MainMenu separated launch-result slice는 2026-08-25 KST의 동일 working-tree code 상태에서 재검증했다. tests-first architecture+MainMenu fixture는 mapper가 아직 legacy combined validation signature를 요구해 Windows build `CS1503` 2건으로 의도대로 red였고, 구현 후 첫 focused gate는 EditMode `113/0`, matching PlayMode `0`이 통과했다. 최종 evaluator/domain/MainMenu/composition/localization/architecture nine-fixture filtered `full`은 EditMode `403/0`, matching PlayMode `0`; `./run_tests.sh core`는 EditMode `217/0`, PlayMode `109/0`; `./run_tests.sh ui`는 Windows UI build와 Unity UI EditMode `1352/0`; current docs 갱신 후 architecture+validation fixture는 EditMode `147/0`, matching PlayMode `0`이 통과했다. MainMenu mapper는 immutable entry, launch evaluation, action policy만 받아 card를 만들고 combined validation service/result/status와 corrected mutable clone은 제거되었다. Profile load failure는 global blocked screen, sequence/catalog failure는 occupied per-slot restart/delete card로 유지되며 completed stale-group, confirmation, exact handoff ownership도 보존된다. broad unfiltered `full`과 manual Player/build smoke는 실행하지 않았으므로 broad recovery claim은 하지 않는다.
- 현재 campaign save 장기 구조 개선 Phase 5 closeout은 2026-08-25 KST의 동일 working-tree code 상태에서 재검증했다. README/pre-release policy의 removed mapper/canonicalizer/full-replacement 설명을 immutable state, common transition engine, strict state mapper, separated evaluator/action policy, explicit raw boundary의 실제 composition으로 교정하고 architecture documentation guard를 보강했다. 먼저 `CampaignSaveArchitectureV2Tests` filtered `full`이 EditMode `114/0`, matching PlayMode `0`; transition/parser/mapper/service/adapter/recovery/DirectPlay/achievement/production-entry 15-fixture filtered `full`이 EditMode `472/0`, matching PlayMode `0`; `./run_tests.sh core`가 EditMode `217/0`, PlayMode `109/0`; `./run_tests.sh ui`가 Windows UI build와 Unity UI EditMode `1352/0`을 통과했다. old runtime shim, raw-carrier consumer, saved-chance zero, generated scene source audit와 `git diff --check`도 통과했다. broad unfiltered `full`과 manual Player/build smoke는 실행하지 않았으므로 project-wide/full recovery claim은 하지 않는다.
- 현재 blocked-save recovery slice는 2026-08-20 KST에 `./run_tests.sh ui`로 fail-closed 재검증했으며, Windows UI build와 Unity UI EditMode `1338 total / 0 failed`가 통과했다. 버전 불일치/손상은 retry와 명시적 전체 초기화를 제공하고, IO/권한 실패 및 미완료 reset은 retry만 제공한다. 미완료 reset 동안 모든 campaign save write는 차단된다. 이 수치는 위 pinned snapshot row를 대체하지 않는다.
- 현재 blocked-save typography follow-up은 2026-08-20 KST에 `./run_tests.sh ui`로 재검증했으며, Windows UI build와 Unity UI EditMode `1341 total / 0 failed`가 통과했다. `BlockedSaveRecovery` 제목/설명/두 action은 authored `TypographyBinding`으로 각각 `HeaderMedium`/`Body`/`Button`/`Button`을 명시한다. 순번 기반 style fallback은 SaveSlotCard의 8-target 계약에만 제한되며, en-US/ko-KR font/material round-trip과 authored sizing 보존을 검증한다. 이 수치는 위 pinned snapshot row를 대체하지 않는다.
- 현재 Gameplay Stage Name typography follow-up은 2026-08-20 KST에 `./run_tests.sh ui`로 재검증했으며, Windows UI build와 Unity UI EditMode `1341 total / 0 failed`가 통과했다. Stage Name은 양 locale 모두 `HeaderLarge` Theme를 사용하여 en-US는 Orbitron ExtraBold, ko-KR은 KBO Dia Gothic Medium을 해석하고 authored sizing을 보존하며, localized source string을 바꾸지 않고 TMP `UpperCase` 표시를 적용한다. World Guide와 transition label의 en-US Prefab 복원 계약은 변경하지 않았다. 이 수치는 위 pinned snapshot row를 대체하지 않는다.
- 2026-08-20 KST의 Pause progression 정보형 stepper 결과(`1341 total / 0 failed`)는 과거 근거다. 현재 계약은 장식 없는 stage-preview 이미지 전용 수평 ScrollRect, current stage 기반 최초 선택, 선택 이미지 2배 확대, Left/Right 선택, 클릭·Submit 전체화면 미리보기다. 이 과거 수치는 위 pinned snapshot row를 대체하지 않는다.
- 현재 Pause preview Close keyboard-accessibility follow-up은 2026-09-13 KST에 main 통합 working tree의 `./run_tests.sh ui`와 `./run_tests.sh core`로 재검증했다. UI는 Windows build와 Unity EditMode `1376 total / 0 failed`, core는 EditMode `282 total / 0 failed`와 PlayMode `111 total / 0 failed`가 통과했다. 확대 overlay는 authored `SelectionFrame`을 가진 one-slot local navigation domain을 소유하고, pointer와 focused Submit은 동일 Close request를 정확히 한 번 실행하며 Cancel은 nested-state shortcut으로 남는다. 모든 preview open은 이전 Pause keyboard focus 공개 여부와 관계없이 frame과 keyboard-selected 확대가 숨겨진 새 focus cycle로 시작한다. 이후 첫 Submit 또는 Navigate는 Close focus만 reveal하고 다음 Submit이 닫으며, close/focus loss는 효과를 제거하고 progression selection을 보존한다. 실제 pointer hover는 일반 Button 계약대로 유지한다. 별도 수동 Editor/Player 시각 검증과 broad `full` lane은 실행하지 않았고, 이 결과는 pinned snapshot 또는 broad full-lane 결과를 대체하지 않는다.
- 현재 SurfaceBelt center remainder badge slice는 2026-09-04 KST에 `./run_tests.sh ui`로 재검증했으며, Windows UI build와 Unity UI EditMode `1344 total / 0 failed`가 통과했다. 중앙 `Cell_0` visual의 왼쪽만 숫자 없는 단일 32x32 `NormalBadge`를 소유하고, 외곽/내곽은 Objective 완료 골드로 통일하며 현재 sector의 `HasAnyRemaining`이 참이면 활성, 거짓이면 흐린 비활성 알파를 사용한다. 첫 bind는 즉시 안정 상태를 적용하고, 이후 활성/비활성 전환은 DOTween 색상·스케일 전환을 사용하며 새 활성 sector 진입은 런타임 복제한 All In 1 UI-mask material의 one-shot Shine으로 확인한다. 동일 sector/동일 상태는 연출을 재시작하지 않고 이웃 sector cell은 badge를 소유하지 않는다. 별도 PlayMode 및 수동 인게임 시각 검증은 실행하지 않았고, 이 수치는 pinned snapshot을 대체하지 않는다.
- 2차 UI canonical 보정 보고서에 기록된 UI red 사유는 Windows `dotnet build` 단계의 `SurfaceBeltButtonBadgeStyleProfile`, `SurfaceBeltButtonBadgeGroupView`, `EnemyTargetEligibilityResult`, `PendingEnemyBlockedReaction` 누락 compile error였으나, 2026-06-10 KST 현재 재실행에서는 재현되지 않았다.
- 삭제 후보는 별도 제품 결정, 현재 lane evidence, baseline note 갱신이 같은 변경에 포함될 때만 제거한다.
- 후속 PR은 per-class fail histogram 기준으로 direct touched cluster와 unrelated baseline cluster를 분리해 판정한다.
- Settings 이동 키 UI의 현재 계약은 단일 `WASDKeyDisplay` 토글이다. 마우스 클릭은 버튼 피드백과 함께 WASD/방향키 표시를 전환하고, 키보드는 `SelectionFrame`이 강조된 `Input.Movement.Toggle`에서 Enter 한 번으로 전환한다. 기존 Slider/Light/이중 표시 그룹은 retired residue로 취급한다.
- 자세한 baseline은 [Full-EditMode-Baseline-2026-04-13.md](./Full-EditMode-Baseline-2026-04-13.md)를 따른다.
- UI freeze evidence는 [UI-EditMode-Baseline-2026-04-15.md](./UI-EditMode-Baseline-2026-04-15.md)를 따른다. 이 문서는 test count ledger가 아니라 structural delta, guard evolution, runner warning status, PlayMode escalation status를 함께 기록해야 한다.
- UI current-structure source는 repo root의 [UI-Current-Structure-Source.md](../../UI-Current-Structure-Source.md)를 따른다. UI lane scope, interpretation, canonical identity list, retired/residue wording, 또는 stale-token audit 기준이 바뀌면 baseline note와 이 source를 같은 변경에서 함께 갱신해야 한다.
- `UIAudioScene` canonical shell 런타임 UI smoke가 필요할 때는 [GameplayShell-Manual-Runtime-Smoke-Plan.md](./GameplayShell-Manual-Runtime-Smoke-Plan.md)를 사용한다. 이 문서는 자동화 lane을 대체하지 않고 canonical runtime integration의 수동 companion evidence를 정의한다.
- generated stratification report는 더 이상 governance truth-source가 아니다.
- 현재 campaign save post-closeout corrective slice는 2026-08-25 KST의 동일 working tree에서 재검증했다. receipt absence는 null 또는 모든 field가 기본값인 JsonUtility residue만 허용하며 populated absent payload는 normalization 전에 fail-closed한다. 사용되지 않던 mutable performance policy를 제거하고 canonical state construction과 common transition engine을 실제 runtime owner로 문서화했다. tests-first focused gate는 EditMode `126/4`로 의도대로 red였고, 구현 후 architecture `117/0`, 명시적 15-fixture touched cluster `564/0`, matching PlayMode `0`, `./run_tests.sh core` EditMode `217/0` 및 PlayMode `109 total / 105 passed / 4 skipped / 0 failed`, `./run_tests.sh ui` Windows build 및 EditMode `1352/0`이 통과했다. 상세 XML/log는 `/mnt/d/J2M/evidence/20260825-055226-campaign-save-corrective-closeout/`에 보존했다. broad unfiltered `full`과 manual Player/build smoke는 실행하지 않았으므로 해당 범위는 claim하지 않는다.
- 현재 campaign-save exact serializer-residue follow-up은 2026-08-25 KST 동일 working tree에서 재검증했다. tests-first focused gate는 EditMode `140/7`로 red였으며, 6건은 기존 빈 문자열 허용 경계, 1건은 실제 파일에서 `PresentWithoutPayload`가 보존되지 않는 문제를 드러냈다. Unity `JsonUtility` characterization 결과 빈 nested object는 `(null, null)`, null nested object의 writer round-trip은 `(string.Empty, string.Empty)`을 만들므로 version/source `0`과 두 exact paired string shape만 serializer residue로 허용한다. 직접 in-memory mixed null/empty, whitespace, 실제 값은 거부하고, physical Save→JSON→Load는 absent/present-null/present-payload를 보존한다. 최종 focused `140/0`, architecture `123/0`, 15-fixture touched cluster `577/0`, core EditMode `217/0`, PlayMode `109 total / 105 passed / 4 skipped / 0 failed`, UI Windows build + EditMode `1352/0`을 검증했다. 상세 XML/log는 `/mnt/d/J2M/evidence/20260825-065534-campaign-save-exact-receipt-residue/`에 보존한다. broad unfiltered `full`과 manual Player/build smoke는 실행하지 않았으므로 해당 범위는 claim하지 않는다.
- 현재 campaign-save raw receipt mapper boundary closeout은 2026-08-25 KST 동일 구현 상태에서 재검증했다. tests-first focused gate는 EditMode `179/4`로 red였고, outbound raw mapper가 `null` receipt string을 빈 문자열로 바꿔 exact pair 보존과 mixed-pair fail-closed를 우회하던 네 계약 가드만 실패했다. outbound projection이 원문자열을 보존하도록 수정하고, valid false-presence payload, non-zero version/source residue, physical payload field, no-coalesce architecture guard를 추가했다. 최종 focused `179/0`, architecture `127/0`, 15-fixture touched cluster `590/0`, 각 matching PlayMode `0`, core EditMode `217/0`, PlayMode `109 total / 105 passed / 4 skipped / 0 failed`, UI Windows build + EditMode `1352/0`을 검증했다. 상세 tests-first/final XML·log·metrics·source hash는 `/mnt/d/J2M/evidence/20260825-075401-campaign-save-receipt-raw-boundary-closeout/`에 보존한다. broad unfiltered `full`과 manual Player/build smoke는 실행하지 않았으므로 해당 범위는 claim하지 않는다.

- 현재 campaign-save final structural audit closeout은 2026-08-25 KST 동일 working tree에서 검증했다. tests-first raw-mapper+architecture gate는 중복 SlotNumber collection 두 경로와 누락된 English closeout guard만 EditMode `160/3`으로 red였다. Public `FromDocuments`와 `ToRawSlots`는 API를 유지하되 duplicate slot을 `ArgumentException`으로 fail-closed하고, receipt mixed-pair matrix는 `ToEntry`와 `ToEntries` wrapper까지 직접 고정한다. 최종 focused `181/0`, architecture `127/0`, 15-fixture touched cluster `592/0`, 각 matching PlayMode `0`, core EditMode `217/0`, PlayMode `109 total / 105 passed / 4 skipped / 0 failed`, UI Windows build + EditMode `1352/0`을 검증했다. 상세 tests-first/final XML·log·metrics·source hash는 `/mnt/d/J2M/evidence/20260825-084058-campaign-save-final-structure-closeout/`에 보존한다. broad unfiltered `full`과 manual Player/build smoke는 실행하지 않았으므로 해당 범위는 claim하지 않는다.
- 현재 campaign-save 독립 감사 corrective closeout은 2026-08-25 KST 동일 working-tree 상태에서 재검증했다. `DemoStageControlTests`에 잘못 이식된 empty-record 기대를 선택 stage의 미시도/미완료/0회 snapshot record 생성과 이전 record 보존 계약으로 교정했고, Demo bridge가 launch context를 직접 등록하지 않는 실제 ownership에 맞게 인접 테스트 이름도 정정했다. Production runtime, planner/engine, diagnostic/profile failure, raw DTO boundary는 변경하지 않았다. 교정된 단일 test `1/0`, Demo fixture `18/0`, architecture `127/0`, 각 matching PlayMode `0`, core EditMode `217/0`, PlayMode `109 total / 105 passed / 4 skipped / 0 failed`, UI Windows build + EditMode `1352/0`을 검증했다. 상세 XML·log·source hash는 `/mnt/d/J2M/evidence/20260825-110252-campaign-save-independent-audit-corrective-closeout/`에 보존한다. broad unfiltered `full`, ActualScene Full-category PlayMode, manual Player/build smoke는 실행하지 않았으므로 해당 범위는 claim하지 않는다.

### English Original
- In the current environment, both `./run_tests.sh core` and `./run_tests.sh full` are runnable.
- `./run_tests.sh ui` remains the targeted Stage 9 UI hardening path for the UI EditMode assembly only, preserving Stage 4–8 seams on one Unity-backed evidence lane.
- The baseline rows in this section are pinned snapshot references. They are not permission to merge artifacts from different dates into one validation claim.
- The current baseline is:
  - `./run_tests.sh core`: green, Core EditMode `13 total / 0 failed`, Core PlayMode `2 total / 0 failed`
  - `./run_tests.sh ui`: Climate PR2 code-head reference green on 2026-07-26 KST, Windows UI build `0` errors, Unity UI EditMode `1060 total / 0 failed`; code-head visual evidence is `TestLogs/TypographyVisualQA/CommandLine-20260726-052954/` at revision `840a5cd2fe0c1460a0a47fc34d8e020f73c76abf`, with six canonical PNGs and three separate `Diagnostics/` PNGs
  - `./run_tests.sh full`: red, Unity Full EditMode `703 total / 101 failed`
  - Unity Full PlayMode has not run yet because EditMode failed first.
- The current fullscreen cursor confinement touched slice was rerun with `./run_tests.sh ui` on 2026-08-15 KST; the Windows UI build and Unity UI EditMode `1386 total / 0 failed` passed. This does not replace the pinned snapshot row above or combine artifacts from different dates.
- The current KBO Dia Gothic typography migration was validated with `./run_tests.sh ui` and `./run_tests.sh core` on 2026-09-05 KST. UI passed the Windows build and Unity EditMode `1348 total / 0 failed`; core passed EditMode `217/0` and PlayMode `111 total / 107 passed / 4 skipped / 0 failed`. Ten large-text/emphasis roles in ko-KR use Medium, while the remaining nine heading/body roles use Light; existing theme-reference GUIDs and TMP material/atlas local IDs remain stable. Managed Korean glyph missing/fallback counts are both `0`. This does not replace the pinned snapshot row above.
- Localization string-governance Phase 3 review hardening passed `36/36` under `./run_tests.sh ui --filter LocalizationStringGovernance` and the Windows UI build plus Unity EditMode `1435/1435` under the official `./run_tests.sh ui` on 2026-09-14 KST. The package-free lifecycle validator and read-only Unity adapter apply the same rules to a data-only third ShipReady locale and the real en-US/ko-KR UI/Stage state, public dictionary views reject downcast mutation, and exact-copy contracts remain separate. Core/full, Player, performance, memory, and CJK content/font validation were not run; this does not replace the pinned snapshot or establish project-wide green.
- Localization/typography CJK Phase 4 runtime selection passed focused policy, Unity adapter, and production-registration fixtures `17/17`, `50/50`, and `7/7`, plus the Windows UI build and Unity EditMode `1445/1445` under the official `./run_tests.sh ui` on 2026-09-14 KST. All four locale ingress paths use one package-free policy; Unity-origin identities require exact canonical registration, rejected events restore the last approved Locale, and the active production provider contains only exact catalogued ShipReady en-US/ko-KR registrations. Core, broad full, Player, performance, memory, glyph, font residency, and visual QA were not run; this does not establish project-wide green.
- Localization/typography CJK Phase 5 ordered-autonym options passed focused catalog, foundation, production, integration, architecture, and governance fixtures `21/21`, `74/74`, `42/42`, `53/53`, `62/62`, and `12/12`, plus the Windows UI build and Unity EditMode `1464/1464` under the official `./run_tests.sh ui` on 2026-09-14 KST. The catalog owns canonical identity, raw autonym, lifecycle, and order; package-free supported codes are membership-only; and Settings renders one uniquely matched current autonym with current-locale typography after actual-current verification. The record retains the initial structural red `61 total / 1 failed`, behavior red `20 total / 1 failed`, and the retrospective limitation of supplemental replay; the user explicitly approved that chronology deviation for closeout. Core, broad full, Player, performance, memory, glyph, font residency, packed Addressables, and visual QA were not run; this does not establish project-wide green.
- Localization/typography CJK Phase 6 unified persistence/fallback passed focused catalog, foundation, production, integration, architecture, and governance fixtures `29/29`, `82/82`, `42/42`, `65/65`, `66/66`, and `12/12`, plus the Windows UI build and Unity EditMode `1496/1496` under the official `./run_tests.sh ui` on 2026-09-14 KST. Structural red was `65 total / 3 failed`; behavior red was `57 total / 5 failed`, including four intended persistence failures and one disclosed factory-count companion failure. Core, broad full, Player, performance, memory, glyph, font residency, packed Addressables, and visual QA were not run. PlayerPrefs success is not fsync or atomic-durability evidence, and this does not establish project-wide green.
- Four-locale Slice A2 selected-startup health passed structural assertion-red `67 total / 1 failed`, valid behavior assertion-red `70 total / 4 failed`, focused integration `70/70`, architecture `67/67`, real Unity string-governance `7/7`, and the Windows UI build plus Unity EditMode `1504/1504` under the official `./run_tests.sh ui` on 2026-09-16 KST. Startup health now exact-lookups only the selected canonical locale's Settings title and canonical Stage display name without emergency-English fallback; ordinary Resolve fallback, SmartFormat, nested descriptors, and Phase 6 ordering remain unchanged. The first behavior-red `70 total / 5 failed` is retained as INVALID because an existing factory-count guard also failed; only the clean rerun is accepted. Existing soft-governance warnings remain informational and no warning closure is claimed. PlayMode escalation, core, broad full, Player/build, Memory Profiler/RSS, Addressables/Build Layout, performance, glyph/font residency, and visual QA were not run; this does not establish project-wide green.
- The terminal-completion HUD Pause input-recovery slice was validated tests-first on 2026-09-20 KST. Before implementation, the regression failed on the expected HUD Pause re-enable immediately after terminal completion. After implementation, the focused Coordinator-payload, application-presenter, public-surface, and Installer-integration cluster passed `4/0`; `./run_tests.sh ui` passed the Windows build and Unity UI EditMode `1424/0`; and pre-commit core passed EditMode `290/0` and PlayMode `112/0`. Flow state is now exposed only as one immutable snapshot through explicit `IUIFlowPresentationSource`, and the composition-owned event-order-dependent pull synchronization is removed. Manual Player mouse validation and the broad `full` lane were not run.
- The current comic-sequence transition slice removed the MP4/VideoPlayer-only runtime and a 70-method legacy mixed suite on 2026-08-19 KST, replacing it with 13 shared-routing tests and expanded comic regression guards. The temporary presentation-only outro definition was removed on 2026-08-21 KST, then the 2026-09-03 KST production-content follow-up added the actual six-panel outro definition and reconnected the Gameplay production scene. After final clear, the Game Clear Main action presents the cumulative `1-1 -> 1-2 -> 1-3 -> 1-4 -> 1-5 -> 1-6` sequence, routes through `ComicOutroToMainMenu`, and records `OutroComicCompleted` after successful route acceptance. On the same working tree, `./run_tests.sh ui` passed the Windows UI build and Unity UI EditMode `1340 total / 0 failed`; filtered `full` ran `0` matching EditMode tests and passed the actual-scene PlayMode `1 total / 0 failed`. This does not replace the pinned snapshot or constitute a broad full-lane result.
- The current comic-sequence enter-fade follow-up was rerun with `./run_tests.sh ui` on 2026-08-22 KST; the Windows UI build and Unity UI EditMode `1348 total / 0 failed` passed. Entry keeps the overlay background transparent so the source scene remains visible while the dedicated black layer becomes opaque over the authored duration, then fixes the background to black before the first-page reveal begins. This does not replace the pinned snapshot row above.
- In the current runtime-generated-shell classification, the Main Menu popup layer and Main Menu Settings overlay create only infrastructure roots, backdrops/blockers, and content mounts with no independent player action. Their actual Confirm and Settings hierarchies come from the authored `ConfirmPopup.prefab` and `SettingsScreen.prefab`, so they remain outside the migration inventory. On 2026-09-13 KST, the fixed `ComicSequenceOverlayView` base shell moved to one canonical authored prefab referenced directly by both production installers. `EnsureHierarchy()` now fail-fast validates authored references instead of generating or repairing the fixed hierarchy; only the Definition-sized, non-interactive panel pool remains runtime infrastructure. Direct pointer/Submit exactly-once and Cancel/lower-layer blocking contracts were added. The Windows UI build and Unity UI EditMode `1384/0`, filtered `full` actual-scene PlayMode `3/0`, and `core` EditMode `282/0` plus PlayMode `111 total / 107 passed / 4 skipped / 0 failed` passed. Broad unfiltered `full` and manual Editor/Player visual validation were not run.
- The current campaign PlayerPrefs-retirement / JSON-save-integration slice was rerun on the same revision on 2026-08-23 KST. The Windows UI build and Unity UI EditMode `1353 total / 0 failed` passed under `./run_tests.sh ui`; `./run_tests.sh core` passed EditMode `217 total / 0 failed` and PlayMode `109 total / 0 failed`; and the save-architecture, adapter, and PlayerPrefs-removal filtered EditMode fixtures passed `54/12/6 total / 0 failed`. UI owns intent, confirmation, and recovery presentation, while Stages owns JSON persistence, active-slot/local launch state, and atomic file lifecycle. Explicit DirectPlay temporary-state deletion does not recover rollback residue. This evidence neither replaces the pinned snapshot nor claims a broad full-lane green result.
- The following two 2026-08-24 typed-committer and slot-clone/canonicalization rows are historical records of the transitional structure. Their complete-replacement, removed-mapper, and shared-canonicalization wording does not describe the current Phase 5 runtime composition.
- The campaign-save typed-committer/strict-chance follow-up was rerun on the same code state on 2026-08-24 KST. `./run_tests.sh core` passed EditMode `217 total / 0 failed` and PlayMode `109 total / 0 failed`; `./run_tests.sh ui` passed the Windows UI build and Unity UI EditMode `1353 total / 0 failed`. A filtered `full` EditMode run covering the save service, adapter, architecture, mapper, and slot-validation-and-DirectPlay fixtures passed `209 total / 0 failed`. Gameplay death/clear use a Stages planner plus typed committer in one profile transaction; query/lifecycle/maintenance/comic/diagnostic ports are separated; and arbitrary slot-lambda mutation plus nullable partial maintenance replacement have been replaced by complete validated slot-document replacement. First-public schema 2 has one `1..3` persisted/runtime chance contract, and exhausting the last chance atomically stores the level-group first stage plus `3` without an intermediate zero. Invalid DirectPlay chances are rejected before side effects, while maintenance replacement rejects malformed nested state before normalization and preserves the prior slot. The same filter had `0` matching PlayMode tests, and the broad `full` lane was not run, so this is not a broad recovery claim.
- The campaign-slot clone/canonicalization follow-up was rerun on the same working-tree code state on 2026-08-24 KST. `./run_tests.sh core` passed EditMode `217 total / 0 failed` and PlayMode `109 total / 0 failed`; `./run_tests.sh ui` passed the Windows UI build and Unity UI EditMode `1353 total / 0 failed`. Separate filtered `full` EditMode runs for `CampaignSlotMapperTests`, `CampaignSaveSlotStoreAdapterTests`, and `SaveSlotValidationAndDirectPlayTests` passed `25/39/33 total / 0 failed`, with `0` matching PlayMode tests in each filter. `SaveSlotData.Clone()` and nested clones are now exact deep copies that preserve raw null/invalid/duplicate/order state, while raw Invalid/Empty/ValidNonEmpty classification and allowed canonicalization live at one persistence boundary shared by Production and Transient stores. Empty-shaped invalid envelopes remain Corrupted, and malformed nested shapes survive diagnostic cloning without an exception. The broad `full` lane was not run, so this is not a broad recovery claim.
- The retired pre-release campaign-save compatibility cleanup was rerun on the same working-tree code state on 2026-08-25 KST. The tests-first `SaveSlotValidationAndDirectPlayTests` run was intentionally red at EditMode `34 total / 2 failed` while the old auto-repair and runtime policy source remained, then passed `34/0` after removal. A filtered `full` run for `SaveSlotValidationAndDirectPlayTests,CampaignStageFlowTests` passed EditMode `118/0` with `0` matching PlayMode tests; `./run_tests.sh core` passed EditMode `217/0` and PlayMode `109/0`; and `./run_tests.sh ui` passed the Windows UI build and Unity UI EditMode `1353/0`. The pre-release `stage-5-1` cursor is no longer auto-repaired to the current final stage; it fails closed through the ordinary sequence-missing result without requesting save synchronization or a write. Catalog-only `legacy-stage-5-1` content and alias/catalog governance remain intact. The broad unfiltered `full` lane was not run, so this is not a broad recovery claim.
- The campaign Continue narrow-preparation slice was rerun on the same working-tree code state on 2026-08-25 KST. The tests-first `CampaignSaveServiceTests` run stopped at the Windows build with the expected `7` compile errors because the new command/port/result did not yet exist, then passed EditMode `35/0` with `0` matching PlayMode tests after implementation. `PendingLaunchSlotProviderTests` passed EditMode `29/0` with `0` matching PlayMode tests; the final seven-fixture filtered `full` run passed EditMode `286/0` with `0` matching PlayMode tests; `./run_tests.sh core` passed EditMode `217/0` and PlayMode `109/0`; and `./run_tests.sh ui` passed the Windows UI build and Unity UI EditMode `1352/0`. MainMenu Continue now uses only a narrow preparation command carrying expected slot/stage/persisted-group identity. The service rechecks those preconditions at the mutation boundary and persists only `CurrentLevelGroupId` when synchronization is required. Stale, missing, and completed cases do not write, while failure cleanup removes only the original handoff token and preserves a newer reservation. The broad unfiltered `full` lane and separate manual Player/build smoke were not run, so this is not a broad recovery claim.
- The campaign MainMenu separated-launch-result slice was rerun on the same working-tree code state on 2026-08-25 KST. The tests-first architecture and MainMenu fixture stopped at the Windows build with the intended two `CS1503` errors while the mapper still required the legacy combined-validation signature; the first implemented focused gate then passed EditMode `113/0` with `0` matching PlayMode tests. The final nine-fixture evaluator/domain/MainMenu/composition/localization/architecture filtered `full` run passed EditMode `403/0` with `0` matching PlayMode tests; `./run_tests.sh core` passed EditMode `217/0` and PlayMode `109/0`; `./run_tests.sh ui` passed the Windows UI build and Unity UI EditMode `1352/0`; and the post-document architecture-plus-validation fixture passed EditMode `147/0` with `0` matching PlayMode tests. The MainMenu mapper now consumes only an immutable entry, launch evaluation, and action policy; the combined validation service/result/status and corrected mutable clone are removed. Profile-load failures remain on the global blocked screen, while sequence/catalog failures remain occupied per-slot restart/delete cards. Completed stale-group presentation, confirmations, and exact handoff ownership are preserved. The broad unfiltered `full` lane and manual Player/build smoke were not run, so this is not a broad recovery claim.
- The campaign-save long-term structural-remediation Phase 5 closeout was rerun on the same working-tree code state on 2026-08-25 KST. The README and pre-release policy were reconciled from removed mapper/canonicalizer/full-replacement wording to the actual immutable-state, common-transition-engine, strict-state-mapper, separated-evaluator/action-policy, and explicit-raw-boundary composition, with stronger architecture documentation guards. The filtered `full` run for `CampaignSaveArchitectureV2Tests` first passed EditMode `114/0` with `0` matching PlayMode tests; the 15-fixture transition/parser/mapper/service/adapter/recovery/DirectPlay/achievement/production-entry filtered `full` run passed EditMode `472/0` with `0` matching PlayMode tests; `./run_tests.sh core` passed EditMode `217/0` and PlayMode `109/0`; and `./run_tests.sh ui` passed the Windows UI build and Unity UI EditMode `1352/0`. Old-runtime-shim, raw-carrier-consumer, saved-chance-zero, generated-scene source audits and `git diff --check` also passed. The broad unfiltered `full` lane and manual Player/build smoke were not run, so this is not a project-wide or full-recovery claim.
- The current blocked-save recovery slice was rerun fail-closed with `./run_tests.sh ui` on 2026-08-20 KST; the Windows UI build and Unity UI EditMode `1338 total / 0 failed` passed. Unsupported/corrupt profiles expose retry plus explicit full reset, while IO/authorization failures and incomplete resets expose retry only. All campaign save writes stay blocked while a reset is pending. This does not replace the pinned snapshot row above.
- The current blocked-save typography follow-up was rerun with `./run_tests.sh ui` on 2026-08-20 KST; the Windows UI build and Unity UI EditMode `1341 total / 0 failed` passed. Authored `TypographyBinding` components assign `HeaderMedium`/`Body`/`Button`/`Button` to the `BlockedSaveRecovery` title, detail, and two actions. Ordinal style fallback is restricted to the eight-target SaveSlotCard contract, with en-US/ko-KR font/material round-trip and authored-sizing preservation covered. This does not replace the pinned snapshot row above.
- The current Gameplay Stage Name typography follow-up was rerun with `./run_tests.sh ui` on 2026-08-20 KST; the Windows UI build and Unity UI EditMode `1341 total / 0 failed` passed. Stage Name now uses the `HeaderLarge` theme in both locales, resolving Orbitron ExtraBold for en-US and KBO Dia Gothic Medium for ko-KR while preserving authored sizing, and adds TMP `UpperCase` presentation without mutating localized source strings. The en-US prefab-restoration contracts for World Guide and transition labels remain unchanged. This does not replace the pinned snapshot row above.
- The 2026-08-20 KST Pause-progression informational-stepper result (`1341 total / 0 failed`) is historical evidence. The current contract is a decoration-free horizontal ScrollRect containing only stage-preview images, with the current stage determining the initial selection, doubled selected-image size, Left/Right selection, and click/Submit full-canvas preview. The historical count does not replace the pinned snapshot row above.
- The current SurfaceBelt center-remainder-badge slice was rerun with `./run_tests.sh ui` on 2026-09-04 KST; the Windows UI build and Unity UI EditMode `1344 total / 0 failed` passed. Only centered `Cell_0` owns one number-free 32x32 `NormalBadge` to the left of its visual; frame and fill share the Objective completion gold, and the current sector's `HasAnyRemaining` selects fully lit or dim inactive alpha. The first bind applies a stable state immediately; later active/inactive changes use DOTween color/scale transitions, and entry into a new active sector plays a one-shot Shine through a runtime-cloned All In 1 UI-mask material. Same-sector/same-state binds do not replay the effect, and neighboring cells own no badge. Separate PlayMode and manual in-game visual validation were not run, and this does not replace the pinned snapshot row above.
- The second UI canonical correction report recorded a UI red reason at Windows `dotnet build` for missing `SurfaceBeltButtonBadgeStyleProfile`, `SurfaceBeltButtonBadgeGroupView`, `EnemyTargetEligibilityResult`, and `PendingEnemyBlockedReaction` compile symbols, but that failure was not reproduced on the 2026-06-10 KST rerun.
- UI deletion candidates are removed only when the product decision, current lane evidence, and baseline note update land in the same change.
- Current H03 HUD retirement evidence treats `PlayerStatus` and the unused legacy `ObjectiveConditionRowView` as absent; the visible `ObjectiveHudRowView` path and the five canonical HUD slices remain protected.
- Follow-up PRs are judged by per-class fail histograms split into direct touched clusters and unrelated baseline clusters.
- The current Settings movement-key contract is one `WASDKeyDisplay` toggle: pointer click toggles WASD/arrow visuals with button feedback, and keyboard Enter submits once from the focused `Input.Movement.Toggle` SelectionFrame. The former Slider, Lights, and dual display groups are retired residue.
- The current Settings language-cycle contract is one authored `Display.Language.Button` node: pointer click and keyboard Submit invoke the same action exactly once, the authored `SelectionFrame` reveals focus, and unavailable language selection is excluded from traversal. The focused tests-first run was `108 total / 2 failed`; the implemented rerun was `108 total / 0 failed`. No PlayMode escalation was required because the route and serialized focus contract are fully exercised in EditMode; manual Editor navigation inspection was not run.
- See [Full-EditMode-Baseline-2026-04-13.md](./Full-EditMode-Baseline-2026-04-13.md) for the pinned baseline.
- Use [UI-EditMode-Baseline-2026-04-15.md](./UI-EditMode-Baseline-2026-04-15.md) for Stage 9 UI hardening evidence, including structural delta and guard-evolution interpretation.
- Use root [UI-Current-Structure-Source.md](../../UI-Current-Structure-Source.md) as the UI current-structure source. When UI lane scope, interpretation, canonical identity lists, retired/residue wording, or stale-token audit policy changes, update the baseline note and this source in the same change.
- Use [GameplayShell-Manual-Runtime-Smoke-Plan.md](./GameplayShell-Manual-Runtime-Smoke-Plan.md) when a `UIAudioScene` canonical shell UI smoke pass is needed; it is the manual companion for canonical runtime-integration evidence and does not replace the automated lanes.
- Use [Display-Settings-Build-Validation-Checklist.md](./Display-Settings-Build-Validation-Checklist.md) for display-settings-specific real-build validation. Editor-only execution is not sufficient evidence for fullscreen/window correctness.
- The generated stratification report is no longer an active governance truth source.
- The current campaign-save post-closeout corrective slice was rerun on the same working tree on 2026-08-25 KST. Receipt absence accepts only null or a JsonUtility residue whose fields are all default-valued; populated absent payload fails closed before normalization. The unused mutable performance policy was removed, and canonical-state construction plus the common transition engine are documented as the actual runtime owners. The tests-first focused gate was intentionally red at EditMode `126/4`; after implementation, architecture `117/0`, the explicit 15-fixture touched cluster `564/0` with `0` matching PlayMode tests, `./run_tests.sh core` EditMode `217/0` and PlayMode `109 total / 105 passed / 4 skipped / 0 failed`, and `./run_tests.sh ui` Windows build plus EditMode `1352/0` passed. Detailed XML/log artifacts are preserved under `/mnt/d/J2M/evidence/20260825-055226-campaign-save-corrective-closeout/`. The broad unfiltered `full` lane and manual Player/build smoke were not run, so those scopes are not claimed.
- The current campaign-save exact serializer-residue follow-up was rerun on the same working tree on 2026-08-25 KST. The tests-first focused gate was red at EditMode `140/7`: six failures exposed the former empty-string acceptance boundary, and one exposed loss of physical-file `PresentWithoutPayload`. Unity `JsonUtility` characterization showed that an empty nested object produces `(null, null)`, while writer round-trip of a null nested object produces `(string.Empty, string.Empty)`. Receipt residue therefore accepts only version/source `0` with those two exact paired string shapes. Direct in-memory mixed null/empty, whitespace, and populated values fail closed, while physical Save→JSON→Load preserves absent, present-null, and present-payload states. Final focused `140/0`, architecture `123/0`, the 15-fixture touched cluster `577/0`, core EditMode `217/0`, PlayMode `109 total / 105 passed / 4 skipped / 0 failed`, and the UI Windows build plus EditMode `1352/0` passed. Detailed XML/log artifacts are preserved under `/mnt/d/J2M/evidence/20260825-065534-campaign-save-exact-receipt-residue/`. The broad unfiltered `full` lane and manual Player/build smoke were not run, so those scopes are not claimed.
- The current campaign-save raw receipt mapper boundary closeout was rerun on the same implementation state on 2026-08-25 KST. The tests-first focused gate was red at EditMode `179/4`; the only failures showed that the outbound raw mapper converted null receipt strings to empty strings before validation, breaking exact-pair preservation and allowing both mixed-pair directions to bypass fail-closed validation. The outbound projection now preserves the original strings, with added coverage for a structurally valid false-presence payload, non-zero version/source residue, physical payload fields, and the no-coalesce architecture guard. Final focused `179/0`, architecture `127/0`, the 15-fixture touched cluster `590/0`, and each matching PlayMode `0` passed; core passed EditMode `217/0` and PlayMode `109 total / 105 passed / 4 skipped / 0 failed`; and the UI Windows build plus EditMode `1352/0` passed. Detailed tests-first/final XML, logs, metrics, and source hashes are preserved under `/mnt/d/J2M/evidence/20260825-075401-campaign-save-receipt-raw-boundary-closeout/`. The broad unfiltered `full` lane and manual Player/build smoke were not run, so those scopes are not claimed.
- The current campaign-save final structural audit closeout was rerun on the same working tree on 2026-08-25 KST. The tests-first raw-mapper and architecture gate was intentionally red at EditMode `160/3`, with only the two duplicate-SlotNumber collection paths and the missing English closeout guard failing. Public `FromDocuments` and `ToRawSlots` retain their signatures but now reject duplicate slot numbers with `ArgumentException`, while the mixed-receipt matrix directly covers the `ToEntry` and `ToEntries` wrappers. Final focused `181/0`, architecture `127/0`, the 15-fixture touched cluster `592/0`, and each matching PlayMode `0` passed; core passed EditMode `217/0` and PlayMode `109 total / 105 passed / 4 skipped / 0 failed`; and the UI Windows build plus EditMode `1352/0` passed. Detailed tests-first/final XML, logs, metrics, and source hashes are preserved under `/mnt/d/J2M/evidence/20260825-084058-campaign-save-final-structure-closeout/`. The broad unfiltered `full` lane and manual Player/build smoke were not run, so those scopes are not claimed.
- The campaign-save independent-audit corrective closeout was rerun on the same working-tree state on 2026-08-25 KST. The migrated empty-record expectation in `DemoStageControlTests` was corrected to require creation of an unattempted, uncleared, zero-count snapshot record for the selected stage while preserving the previous record. The adjacent test name was also reconciled with the actual ownership contract in which the Demo bridge does not register launch context directly. Production runtime, planner/engine, diagnostic/profile-failure handling, and the raw DTO boundary were unchanged. The corrected single test passed `1/0`, the Demo fixture `18/0`, architecture `127/0`, and each matching PlayMode `0`; core passed EditMode `217/0` and PlayMode `109 total / 105 passed / 4 skipped / 0 failed`; and the UI Windows build plus EditMode `1352/0` passed. Detailed XML, logs, and source hashes are preserved under `/mnt/d/J2M/evidence/20260825-110252-campaign-save-independent-audit-corrective-closeout/`. The broad unfiltered `full` lane, ActualScene Full-category PlayMode, and manual Player/build smoke were not run, so those scopes are not claimed.

## Visual runner interruption contract / visual runner 중단 계약
### 한국어
- `typography-visual`과 `typography-hud-visual`은 8개 guarded path의 baseline이 완성된 직후 `EXIT`, `INT`, `TERM` cleanup trap을 설치한다.
- 정상 capture는 모든 guarded path를 restore 전에 관측하고 lane verdict를 확정한 뒤 restore한다.
- 중단 capture는 `INTERRUPTED`로 기록하며, runner-owned PID/PGID를 우선 종료한 뒤 idempotent cleanup으로 baseline을 복원한다.
- Primary PID/PGID 종료 결과와 무관하게 exact-projectPath fallback scan을 항상 실행한다. Primary와 fallback은 대체 분기가 아니라 순차 cleanup 단계다.
- Fallback은 `/proc/uptime` monotonic clock을 기준으로 primary 종료 후 `6000ms` startup grace 전체를 `200ms` 간격으로 관찰한다. Grace 중 empty scan은 조기 종료 조건이 아니며, deadline scan에 나타난 eligible candidate도 종료한다.
- Startup grace가 완료되고 exact-path candidate가 없는 상태에서만 quiet completion을 판정한다. Quiet period는 마지막 candidate 종료 시점(한 번도 없으면 grace 완료 시점)부터 `400ms`이며, quiet 중 candidate가 나타나면 종료 후 deadline을 다시 계산한다.
- Fallback hard timeout은 `12000ms`다. Timeout 또는 final inventory에서 post-start exact-path Unity가 남으면 `FAILED_RUNNER_OWNED_PROCESS_REMAINS`로, survivor는 없지만 quiet가 완료되지 않으면 cleanup failure로 처리한 뒤 asset restore를 best effort로 수행한다.
- 단일 `SIGINT`/`SIGTERM`은 handler가 관측한 해당 signal을 보존해 각각 `130`/`143`을 반환한다. 첫 handler latch 완료가 확인된 뒤 두 번째 signal을 전달하는 순차 경우에는 `FIRST_OBSERVED_SIGNAL_WINS`를 적용한다.
- Bash가 외부 command를 기다리는 동안 non-real-time `INT`와 `TERM`이 함께 pending되면 trap dispatch order는 `CO_PENDING_SIGNAL_ORDER_UNSPECIFIED`다. POSIX가 이 pending signal들의 전달 순서를 보장하지 않으므로 pure Bash runner는 실제 arrival order를 복원한다고 주장하지 않는다. 이 경우 최종 status는 `130` 또는 `143`이며, `0`, 원래 command status, cleanup failure status만 반환하는 false-success/failure masking은 허용하지 않는다.
- cleanup 중 handler가 관측한 signal은 restore를 재진입하거나 중단하지 않는다. cleanup은 one-shot으로 끝까지 수행되고, 최종 status 우선순위에는 first-observed interruption status가 사용된다.
- guard phase는 `RUNNING`, `CLEANING`, `FINALIZING`, `DONE`으로 구분한다. `EXIT` cleanup은 진입 직후 재귀가 차단되고 정확히 한 번만 실행되며, `INT`/`TERM` trap은 최종 `exit`까지 유지한다.
- `FINALIZING`에서 final status snapshot 전후로 handler가 signal을 관측하면 first-observed status `130`/`143`으로 즉시 종료한다. 이 경로는 process cleanup, mutation observation, asset restore를 다시 실행하지 않는다.
- 최종 status 우선순위는 first-observed interruption status, 원래 command nonzero, cleanup failure, `0` 순서다.
- Unity fallback 종료는 lane 시작 후 나타난 non-preexisting process 중 argv에서 정확히 파싱한 `-projectPath`가 canonical current project path와 같은 process에만 적용한다. 부분 문자열, 유사/prefix/suffix path, 다른 argument의 path는 ownership 근거가 아니다.
- Lifecycle evidence는 각 output directory의 `runner-cleanup-lifecycle.log`에 `signal_order_contract=FIRST_OBSERVED_SEQUENTIAL_CO_PENDING_UNSPECIFIED`, first-observed signal/status, INT/TERM observed mask, 보수적인 co-pending 판정(`false` 또는 `unknown`), final interruption status, cleanup status, owned PID/PGID, fallback/candidate exact-match 판정과 startup-grace/quiet/hard-timeout monotonic timestamp 및 completion 상태를 기록한다.

### English Original
- `typography-visual` and `typography-hud-visual` install `EXIT`, `INT`, and `TERM` cleanup traps immediately after all eight guarded-path baselines are complete.
- A normal capture observes every guarded path and fixes the lane verdict before any restore.
- An interrupted capture records `INTERRUPTED`, terminates the runner-owned PID/PGID first, and restores the baseline through one idempotent cleanup path.
- The exact-projectPath fallback scan always runs after primary PID/PGID termination, regardless of the primary result. Primary termination and fallback are sequential cleanup stages, not alternative branches.
- Fallback uses the `/proc/uptime` monotonic clock to observe the full `6000ms` startup grace after primary termination at `200ms` intervals. Empty scans during grace never end polling early, and an eligible candidate on the deadline scan is still terminated.
- Quiet completion is evaluated only after startup grace completes with no exact-path candidate. The `400ms` quiet period starts at the last candidate termination (or at grace completion when none appeared), and a candidate during quiet is terminated and resets its deadline.
- The fallback hard timeout is `12000ms`. A timeout or a post-start exact-path Unity process in the final inventory fails cleanup as `FAILED_RUNNER_OWNED_PROCESS_REMAINS`; a timeout with no survivor but incomplete quiet also fails cleanup before best-effort asset restore.
- A single `SIGINT` or `SIGTERM` is retained when its handler observes it and returns `130` or `143`, respectively. When a synchronization barrier confirms that the first handler has latched before the second signal is sent, `FIRST_OBSERVED_SIGNAL_WINS`.
- If non-real-time `INT` and `TERM` are both pending while Bash waits for an external command, trap dispatch order is `CO_PENDING_SIGNAL_ORDER_UNSPECIFIED`. POSIX does not guarantee delivery order for those pending signals, so the pure Bash runner does not claim to reconstruct actual arrival order. The final status may be `130` or `143`, but never `0`, the original command status, or only a cleanup-failure status.
- A signal observed during cleanup neither re-enters nor aborts restore. Cleanup remains one-shot and completes before final status selection uses the first-observed interruption status.
- Guard phases are `RUNNING`, `CLEANING`, `FINALIZING`, and `DONE`. Recursive `EXIT` cleanup is disabled on entry and cleanup runs exactly once, while the `INT` and `TERM` traps remain installed through the final `exit`.
- A signal observed before or after the final-status snapshot in `FINALIZING` immediately exits with the first-observed status `130` or `143`. This path does not repeat process cleanup, mutation observation, or asset restore.
- Final status precedence is first-observed interruption status, original command nonzero, cleanup failure, then `0`.
- Unity fallback termination is limited to non-preexisting processes first observed after lane start whose exactly parsed `-projectPath` argv token canonically equals the current project path. Substrings, similar/prefix/suffix paths, and paths found in other arguments do not establish ownership.
- `runner-cleanup-lifecycle.log` records `signal_order_contract=FIRST_OBSERVED_SEQUENTIAL_CO_PENDING_UNSPECIFIED`, the first-observed signal/status, INT/TERM observed mask, conservative co-pending state (`false` or `unknown`), final interruption status, cleanup status, owned PID/PGID, fallback/candidate exact-match decisions, and startup-grace/quiet/hard-timeout monotonic timestamps and completion state in each capture output directory.

## 1. Overview / 개요
### 한국어
- 이 시스템은 WSL에서 테스트를 오케스트레이션하면서 실제 빌드와 실행은 Windows `dotnet`과 Unity에서 수행하도록 고정한 게임플레이 테스트 운영 체계다.
- `Core`는 일상 개발과 pre-commit에서 사용하는 빠르고 결정적인 안전 계층이다.
- `Full`은 런타임 동작, 구조 검증, 에셋 민감 검증까지 포함하는 전체 검증 경로다.
- `Governance`는 테스트 배치 규칙을 자동으로 강제해서 사람의 기억이나 관습에 의존하지 않게 만든다.

### English Original
- This system gives the project one repeatable way to build, run, classify, and validate gameplay tests from WSL while executing Unity and `dotnet` on Windows.
- `Core` is the fast deterministic safety layer used for everyday development and pre-commit gating.
- `Full` is the broader validation path that includes runtime behavior, structure checks, and asset-sensitive coverage.
- `Governance` automatically enforces test boundaries so placement rules do not depend on memory or team habit.

## UI baseline governance / UI baseline governance

### PR #221 main integration: death input and Push/Flip ownership (2026-09-25)

- Merged the main branch's permanent player-death input/tick block with the Push/Flip press-time direction capture and interaction-playback lock. The obsolete respawn-delay gate is retired; the Host-owned admission-policy/feed cleanup and deleted UI movement Gateway remain unchanged.
- Same integration working tree: `./run_tests.sh core` passed EditMode `293/0` and PlayMode `109 passed / 4 graphics skips / 0 failed`; focused death-flow `full` passed EditMode `1/0`; focused policy and Push/Flip `full` passed EditMode `11/0` and PlayMode `10/0`; `./run_tests.sh ui` passed the Windows build and EditMode `1623/0`. Evidence: `/mnt/d/J2M/evidence/pr221-main-integration-20260925/`.
- The first Core attempt stopped on a stale Unity-generated project file referring to the removed RespawnProcessor; a runner cold-checkout import refreshed the generated project before the successful rerun. Manual Editor/Player input/death review, Player build, dedicated graphics evidence and unfiltered full were not run.

### UI EventSystem navigation action direct access (2026-09-25)

- The Main Menu and Gameplay EventSystem installers now obtain `InputSystemUIInputModule` directly and clear its public `move`, `submit`, and `cancel` action references. The nine reflection-name probes and module type lookup are removed. `UiNavigationInputRouter` still dispatches navigation while the Input System module retains pointer input.
- One UI lifecycle case was added for module reactivation and reapplication; the existing Gameplay UI composition case now checks that navigation actions are null and point/left-click/scroll references remain connected. No tests were removed, renamed, merged or split; no prefab migration or new product ownership. The selected input actions and UI navigation router were not changed.
- UI Windows build and EditMode passed `1623/0`. Navigation action cleanup is repeated when either installer ensures the EventSystem; the test verifies the module's default actions can return on reactivation and be cleared again without dropping pointer references. Evidence: `/mnt/d/J2M/evidence/ui-navigation-module-20260925-015324/validation-summary.md`. Manual Editor/Player interaction and broad full were not run.

### Demo hotkey keyboard bridge simplification (2026-09-25)

- Replaced F10/BackQuote reflection reads with null-safe `Keyboard.current` access. Removed keyboard/key property metadata, including the Escape metadata used only to discover `wasPressedThisFrame`. Key selection, rebind/transition gates, panel toggling and the separate navigation utility are unchanged.
- Added two UI input cases for the selected key, ignored alternate/Escape keys, press/hold/repress, missing keyboard and reconnection. They use the Input System package's isolated `InputTestFixture`; only the Editor UI test assembly adds `Unity.InputSystem.TestFramework`. No cases removed, renamed, merged or split; no prefab migration or product ownership shift. The diagnostics guard now checks the retained BackQuote entrypoint instead of the removed reflection field name.
- Windows builds and UI EditMode passed `1622/0`; core EditMode passed `293/0`, PlayMode `108 passed / 4 graphics skips / 0 failed`. The initial UI run passed the existing 1620 cases but failed both new press-edge cases in the unisolated Editor input environment; the isolated rerun passed both. Core's runner detected and removed two generated InitTestScene files, then completed successfully. Existing category/compiler warnings remain; font guards reported `NO_MUTATION`.
- Evidence: `/mnt/d/J2M/evidence/keyboard-bridge-20260925-012711/validation-summary.md`. Manual hardware/Player checks and broad full were not run; virtual-device input and existing UI flow coverage bound this change. Baseline count literals are synchronized after measurement and checked by the UI documentation filter.

### Player action and UI movement retirement (2026-09-25)

- Removed seven unused Player actions and their 21 bindings while preserving all remaining action/binding IDs, the UI map and input asset meta. A fixed saved JSON fixture was captured through the production rebind service before deletion; both movement schemes restore it after import without clearing settings.
- Moved shared admission-policy disposal to the Host-owned UIAccess context, including repeated-dispose and failed-composition cleanup. Retired the UI movement gateway, held-direction override and unused acceptance DTO; retained query gates, snapshot windows, physical movement buffering and presentation direction DTOs. Pause does not acquire a new physical-input reset.
- UI inventory: four production binding save/load cases and one installer recreation/lifetime case added; the obsolete HUD-to-gateway dependency test was renamed to a gateway-absence guard. No UI tests removed, merged or split; no prefab migration. Gameplay gateway-driven tests now exercise raw input/query contracts, one UI-held Core case was retired and a physical-held Extended case added, with new disposal and Pause characterization coverage. Four existing PlayMode cases now load the production input asset.
- Executed: pre-removal binding capture `2/0`; post-removal keyboard fixture `22/0`; B1 ownership/Pause `14/0`; B2 query/lifecycle/campaign/architecture `184/0`; input selection EditMode `7/0`, PlayMode `38/0`; core EditMode `293/0`, PlayMode `108 passed / 4 graphics skips / 0 failed`; UI Windows build and EditMode `1620/0`. The B1/B2 filters selected zero PlayMode cases and do not establish PlayMode coverage.
- Initial fixture-constant and PlayMode assembly-reference compile failures were corrected before successful reruns; logs are retained. All invoked font guards reported `NO_MUTATION`; existing category and untouched-code warnings remain. Manual Editor/Player interaction, broad unfiltered full and dedicated graphics evidence lanes were not run. Evidence: `/mnt/d/J2M/evidence/input-retirement-20260925-000826/validation-summary.md`.

### Input reset cleanup follow-up (2026-09-24)

- Removed the unused Escape bridge method while preserving the reflection metadata consumed by F10/BackQuote. UI pending-input clearing delegates to the existing held-direction clear method; terminal clearing relies on the existing player-input reset, and action unbinding no longer attempts to reset a disposed/null tracker. The pre-initialization-safe unbind lifecycle is preserved.
- Test inventory is unchanged. Initial UI validation found one documentation assertion still pinned to the earlier `1609` count after the preceding cleanup recorded `1615`; both existing count assertions now match the measured baseline. Runtime behavior assertions are unchanged. This closes the prior post-validation documentation update mismatch rather than weakening the guard.
- `core` passed Windows builds, EditMode `293/0`, and PlayMode `108 passed / 4 graphics-related skips / 0 failed`. Focused full selection for `GameplayUiAccessRuntimeTests`, `GameplayHostCommandAdmissionPolicyTests`, and `GameplayInputHost_Reenable_RebindsInputActions` passed the solution build, EditMode `83/0`, and PlayMode `1/0`. The UI rerun passed its Windows build and EditMode `1615/0` after the documentation guard correction; the initial `1614 passed / 1 failed` result is retained. Input runtime sources remained unchanged throughout all runs.
- All invoked SDF guards reported `NO_MUTATION`; source-category mismatch and untouched-code compiler warnings remain in the logs. Separate `core-feature-gate`, broad unfiltered full, graphics evidence lanes, and manual Editor/Player smoke were not run for this bounded cleanup. Evidence: `/mnt/d/J2M/evidence/input-reset-cleanup-20260924-141032-f86c0f/validation-summary.md`.

### Input unused-surface cleanup (2026-09-24)

- Scope: retire the ineffective `directionChangeConsumesDelay` wiring and scene value, unused `ClearBefore`, `IsRawDirectionActive`, parameterless `ExitTerminalHold`, required-action arrays, keyboard snapshot movement display string, and service path forwarding properties.
- UI test inventory is unchanged; only assertions for retired payload/API members and snapshot constructor arguments were removed. The existing gameplay PlayMode case is renamed to `GameplayInputHost_FlipAfterDirectionChange_PreservesProjectedViewState` with its behavior assertions preserved.
- Same-implementation validation: `core` passed Windows builds, EditMode `293/0`, and PlayMode `112 total / 108 passed / 4 skipped / 0 failed`; the four skips require dedicated graphics/render evidence. Actual `UIAudioScene` bootstrap cases passed. `ui` passed the Windows build and EditMode `1615/0`.
- `full --filter PlayerMovementPlayModeTests` passed the solution build, matched zero EditMode tests, and ran PlayMode `83 total / 76 passed / 7 failed`. All seven failures are camera visual-evidence entry preconditions: four require a graphics device and three require `-cameraShakeVisualOutput`, neither supplied by this ordinary headless command. They fail before the input scenario runs. The renamed Flip-after-direction-change case passed. The fixture run remains failed; it is not a full-lane pass.
- Governance reported source-category mismatch warnings; build logs retain warnings in untouched test/vendor code. All invoked font integrity guards reported `NO_MUTATION`. Evidence, failure messages, source hashes, and preserved pre-existing-file hashes: `/mnt/d/J2M/evidence/input-unused-cleanup-20260924-135019-a7d26a/validation-summary.md`.
- Not run: separate `core-feature-gate`, broad unfiltered `full`, dedicated camera visual lanes, and manual Editor/Player smoke. The executed lanes cover this deletion; broader graphics/manual validation is outside this slice. No full-regression claim is made.

### 한국어
- UI baseline note는 단순 count bump 문서가 아니다.
- Stage 9 이후에는 다음 항목을 함께 기록해야 한다.
  - added / removed / renamed / merged / split tests
  - replaced weak guards / obsolete guards
  - responsibility shifts between layers
  - bounded UI layer migration이 완료될 때 prefab migration inventory 축소와 sunset proof
  - runner warning changes
  - PlayMode escalation status
- `Docs/Testing/UI-EditMode-Baseline-2026-04-15.md`와 이 가이드는 같은 변경에서 함께 갱신해야 한다.
- root `UI-Current-Structure-Source.md`도 current UI structure나 stale-token audit 기준이 바뀌는 변경에서는 함께 갱신해야 한다.
- Scene transition payload decommission evidence는 `StageTransitionChanceLostPayload`와 `SceneTransitionOverlayModel`의 generic `Title` / `Message` 부재, coordinator resolver 부재, typed progress/chance-loss 보존, canonical content routing과 production-prefab smoke를 함께 검증해야 한다.
- Pause progression evidence는 canonical sequence와 현지화된 이름, 단일 inactive image template, line/frame/overlay/node-background 부재, current stage 기반 최초 선택, 선택 이미지의 2배 layout 폭과 인접 이미지 non-overlap, 좌우 선택/스크롤 clamp, 첫 클릭 선택과 재클릭·Submit 확대, 확대 중 command 차단, Cancel 우선 닫기, placeholder catalog, 그리고 screenshot preview의 실제 campaign payload 바인딩을 함께 검증해야 한다.

### English Original
- The UI baseline note is not a count-only ledger.
- After Stage 9 it must record:
  - added / removed / renamed / merged / split tests
  - replaced weak guards / obsolete guards
  - responsibility shifts between layers
  - prefab migration inventory shrinkage and sunset proof when a bounded UI layer completes migration
  - runner warning changes
  - PlayMode escalation status
- `Docs/Testing/UI-EditMode-Baseline-2026-04-15.md` and this guide must be updated together in the same change.
- Root `UI-Current-Structure-Source.md` must also be updated in the same change when current UI structure or stale-token audit policy changes.
- Scene-transition payload decommission evidence must jointly verify the absence of generic `Title` / `Message` members from `StageTransitionChanceLostPayload` and `SceneTransitionOverlayModel`, the absence of coordinator copy resolvers, preservation of typed progress/chance-loss state, canonical content routing, and a production-prefab smoke.
- Pause-progression evidence must jointly cover the canonical sequence and localized names, one inactive image template, absence of line/frame/overlay/node-background decoration, current-stage initial selection, doubled selected-image layout width with non-overlapping adjacent images, clamped horizontal selection/scrolling, first-click selection and second-click/Submit expansion, command blocking while expanded, Cancel-first close behavior, the placeholder catalog, and real campaign-payload binding in screenshot preview.

## Gameplay audio verification wording / Gameplay audio verification wording
### 한국어
- gameplay audio refactor 검증 결과는 실행한 lane 범위만 말해야 한다.
- 아래 네 reporting level만 공식적으로 사용한다.
  - `build verified`
    - claim 가능 조건: relevant build가 통과했을 때
    - imply하지 않는 것: runtime orchestration correctness, regression closure
    - approved example: `Gameplay audio changes are build verified.`
  - `core lane validated`
    - claim 가능 조건: `./run_tests.sh core` 또는 동등한 core lane이 통과했을 때
    - imply하지 않는 것: full gameplay-wide regression closure
    - approved example: `The new gameplay audio structure/contracts are validated in core lanes.`
  - `targeted orchestration/architecture validated`
    - claim 가능 조건: targeted gameplay audio governance/orchestration/architecture tests가 함께 통과했을 때
    - imply하지 않는 것: unrelated gameplay regression closure
    - approved example: `Gameplay audio host orchestration and governance contracts are validated by targeted architecture tests.`
  - `full gameplay-wide regression validated`
    - claim 가능 조건: broader gameplay-wide validation lane가 실제로 실행되어 pass했을 때
    - imply하지 않는 것: none beyond the executed full lane itself
    - approved example: `Gameplay-wide regression coverage has been validated on the full lane.`
- disallowed wording:
  - `all gameplay-wide regressions are closed`
    - build + core lane + targeted governance/orchestration tests만으로는 이 표현을 사용할 수 없다.

### English Original
- Gameplay-audio refactor validation must report only the lanes that were actually executed.
- Use only these four reporting levels.
  - `build verified`
    - may be claimed when the relevant build passes
    - does not imply runtime orchestration correctness or regression closure
    - approved example: `Gameplay audio changes are build verified.`
  - `core lane validated`
    - may be claimed when `./run_tests.sh core` or an equivalent core lane passes
    - does not imply full gameplay-wide regression closure
    - approved example: `The new gameplay audio structure/contracts are validated in core lanes.`
  - `targeted orchestration/architecture validated`
    - may be claimed when targeted gameplay-audio governance/orchestration/architecture tests pass
    - does not imply unrelated gameplay regression closure
    - approved example: `Gameplay audio host orchestration and governance contracts are validated by targeted architecture tests.`
  - `full gameplay-wide regression validated`
    - may be claimed only when the broader gameplay-wide validation lane actually ran and passed
    - does not imply anything beyond that executed full lane
    - approved example: `Gameplay-wide regression coverage has been validated on the full lane.`
- Disallowed wording:
  - `all gameplay-wide regressions are closed`
    - build verification plus core lanes plus targeted governance/orchestration tests is not enough to use this claim.

## Stage content refactor reporting wording / Stage content refactor reporting wording
### 한국어
- Stage Content Layer Refactor P3 sunset 결과는 실제로 실행한 lane와 고정한 architecture/CI contract만 말해야 한다.
- 아래 네 reporting level만 공식적으로 사용한다.
  - `P3 sunset validated`
    - claim 가능 조건: canonical runtime path, launcher-only direct-play contract, governed known-warning/alias compatibility, duplicate legacy asset removal이 코드/테스트/문서에 반영됐을 때
    - imply하지 않는 것: broad project-wide green, unrelated gameplay/UI regression closure
    - approved example: `Stage content P3 sunset is validated on the canonical path and governance lanes.`
  - `core lane validated`
    - claim 가능 조건: `./run_tests.sh core` 또는 동등한 core lane이 통과했을 때
    - imply하지 않는 것: full project-wide regression closure
    - approved example: `Stage content sunset is validated in core lanes.`
  - `targeted architecture/CI validated`
    - claim 가능 조건: `StageCatalogCiValidationEntryPoint.Run`과 targeted editor/runtime architecture tests가 통과했을 때
    - imply하지 않는 것: broad backlog closure, unrelated baseline recovery
    - approved example: `Stage catalog governance and launcher contracts are validated by targeted architecture/CI lanes.`
  - `broad project-wide regression validated`
    - claim 가능 조건: broader `full` 또는 동등한 project-wide validation lane이 실제로 실행되어 pass했을 때
    - imply하지 않는 것: none beyond that executed broad lane
    - approved example: `Broad project-wide regression coverage has been validated on the full lane.`
- disallowed wording:
  - `project-wide green`
  - `broad green`
  - `all stage-content regressions are closed`
  - `full regression is closed`
    - `core` + targeted architecture/CI evidence만으로는 위 표현을 사용할 수 없다.
- Stage-content close note는 [Stage-Content-P3-Sunset-2026-04-22.md](./Stage-Content-P3-Sunset-2026-04-22.md)를 따른다.
- stage editor direct-play launcher contract는 [Stage-DefaultStageId-Editor-Direct-Play-Contract.md](./Stage-DefaultStageId-Editor-Direct-Play-Contract.md)를 따른다.

### English Original
- Stage Content Layer Refactor P3 sunset reporting must describe only the lanes that actually ran and the architecture/CI contracts that were explicitly locked.
- Use only these four reporting levels.
  - `P3 sunset validated`
    - may be claimed when the canonical runtime path, launcher-only direct-play contract, governed known-warning/alias compatibility, and duplicate legacy asset removal are reflected in code, tests, and docs
    - does not imply broad project-wide green or unrelated gameplay/UI regression closure
    - approved example: `Stage content P3 sunset is validated on the canonical path and governance lanes.`
  - `core lane validated`
    - may be claimed when `./run_tests.sh core` or an equivalent core lane passes
    - does not imply full project-wide regression closure
    - approved example: `Stage content sunset is validated in core lanes.`
  - `targeted architecture/CI validated`
    - may be claimed when `StageCatalogCiValidationEntryPoint.Run` and the targeted editor/runtime architecture tests pass
    - does not imply broad-backlog closure or unrelated baseline recovery
    - approved example: `Stage catalog governance and launcher contracts are validated by targeted architecture/CI lanes.`
  - `broad project-wide regression validated`
    - may be claimed only when the broader `full` lane or an equivalent project-wide validation lane actually ran and passed
    - does not imply anything beyond that executed broad lane
    - approved example: `Broad project-wide regression coverage has been validated on the full lane.`
- Disallowed wording:
  - `project-wide green`
  - `broad green`
  - `all stage-content regressions are closed`
  - `full regression is closed`
    - core plus targeted architecture/CI evidence is not enough to use those claims.
- Follow [Stage-Content-P3-Sunset-2026-04-22.md](./Stage-Content-P3-Sunset-2026-04-22.md) for the stage-content close note template.
- Follow [Stage-DefaultStageId-Editor-Direct-Play-Contract.md](./Stage-DefaultStageId-Editor-Direct-Play-Contract.md) for the stage editor direct-play launcher contract.

## Display settings verification wording / Display settings verification wording
### 한국어
- display settings 검증 결과는 실행한 lane와 실제 build/manual validation 범위만 말해야 한다.
- 아래 네 reporting level만 공식적으로 사용한다.
  - `build verified`
    - claim 가능 조건: relevant build가 통과했을 때
  - `core lane validated`
    - claim 가능 조건: relevant automated core/targeted lane가 통과했을 때
  - `targeted display architecture validated`
    - claim 가능 조건: display runtime/service/presenter/composition contract tests가 통과했을 때
  - `real-build manual display validation completed`
    - claim 가능 조건: [Display-Settings-Build-Validation-Checklist.md](./Display-Settings-Build-Validation-Checklist.md) 범위를 실제 build에서 확인했을 때
- editor-only 실행만으로 fullscreen/window correctness를 주장하면 안 된다.

### English Original
- Display-settings validation must report only the automated lanes and real-build manual checks that actually ran.
- Use only these four reporting levels.
  - `build verified`
    - may be claimed when the relevant build passes
  - `core lane validated`
    - may be claimed when the relevant automated core or targeted lane passes
  - `targeted display architecture validated`
    - may be claimed when the display runtime/service/presenter/composition contract tests pass
  - `real-build manual display validation completed`
    - may be claimed only when the scope in [Display-Settings-Build-Validation-Checklist.md](./Display-Settings-Build-Validation-Checklist.md) was checked in a real build
- Editor-only execution is insufficient evidence for fullscreen/window correctness.

## UI SFX verification wording / UI SFX verification wording
### 한국어
- UI SFX v1 결과는 hidden `Ui` authored channel, `Sfx` setting-dependent effective mix policy, targeted UI lane 범위만 말해야 한다.
- 아래 세 reporting level만 공식적으로 사용한다.
  - `build verified`
    - claim 가능 조건: relevant build가 통과했을 때
  - `ui lane validated`
    - claim 가능 조건: `./run_tests.sh ui` 또는 동등한 focused UI lane이 통과했을 때
  - `targeted UI SFX architecture validated`
    - claim 가능 조건: cue ownership, hidden-`Ui` authored channel policy, `Sfx` setting-dependent effective mix, cue-map validation, slider commit dedupe, screen/popup lifecycle contract tests가 통과했을 때
- placeholder `Ui` asset authoring은 wiring evidence일 뿐 final content polish claim이 아니다.
- hover, disabled/no-op, backdrop-consume feedback는 v1 scope 밖이다.

### English Original
- UI SFX v1 reporting must describe only the hidden-`Ui` authored channel, the `Sfx` setting-dependent effective mix policy, and the UI-focused validation lanes that actually ran.
- Use only these three reporting levels.
  - `build verified`
    - may be claimed when the relevant build passes
  - `ui lane validated`
    - may be claimed when `./run_tests.sh ui` or an equivalent focused UI lane passes
  - `targeted UI SFX architecture validated`
    - may be claimed when cue-ownership, hidden-`Ui` authored-channel policy, `Sfx` setting-dependent effective mix, cue-map-validation, slider-commit-dedupe, and screen/popup lifecycle contract tests pass
- Placeholder `Ui` asset authoring is wiring evidence only; it does not claim final content polish.
- Hover, disabled/no-op, and backdrop-consume feedback remain out of scope in v1.
## Persistent BGM flow reporting wording / Persistent BGM flow reporting wording
### 한국어
- persistent BGM flow v1 결과는 ownership continuity와 실제 검증한 transition 범위만 말해야 하며 true Crossfade completion을 암시하면 안 된다.
- 아래 네 reporting level만 공식적으로 사용한다.
  - `build verified`
    - claim 가능 조건: relevant build가 통과했을 때
  - `targeted persistent BGM ownership validated`
    - claim 가능 조건: coordinator/registry/bootstrap/ownership boundary targeted tests가 통과했을 때
  - `cross-scene continuity validated`
    - claim 가능 조건: persistent lifetime + same-profile continuity가 scene change를 포함해 검증됐을 때
  - `real transition-effects validation completed`
    - claim 가능 조건: 실제 transition effect runtime behavior가 구현되고 그 범위가 별도로 검증됐을 때
- approved sentence template:
  - `Persistent BGM ownership, cross-scene continuity, and single-source FadeOutIn are validated; Crossfade remains reserved.`
- v1 ownership continuity와 single-source FadeOutIn support는 true Crossfade support completion과 동일하지 않다.
- presentation-exclusive playback suppression evidence는 router가 suppression 중 최신 request selection을 유지하면서 playback을 보류하는지, source-scene 종료에서 현재 selection을 복원하는지, 동기 승인된 transition handoff에서 이전 BGM을 재시작하지 않는지를 함께 검증해야 한다.

### English Original
- Persistent BGM flow v1 reporting must stay scoped to ownership continuity and the transition effects actually validated; it must not imply completed Crossfade support.
- Use only these four reporting levels.
  - `build verified`
    - may be claimed when the relevant build passes
  - `targeted persistent BGM ownership validated`
    - may be claimed when targeted coordinator, registry, bootstrap, and ownership-boundary tests pass
  - `cross-scene continuity validated`
    - may be claimed when persistent lifetime and same-profile continuity were validated across scene change
  - `real transition-effects validation completed`
    - may be claimed only after real transition-effect runtime behavior exists and that scope was validated
- Approved sentence template:
  - `Persistent BGM ownership, cross-scene continuity, and single-source FadeOutIn are validated; Crossfade remains reserved.`
- v1 ownership continuity plus single-source FadeOutIn support is not equivalent to completed Crossfade support.
- Presentation-exclusive playback-suppression evidence must jointly verify that the router keeps the latest request selection without playback while suppressed, restores the current selection when the source scene remains, and does not restart the previous BGM after a synchronously accepted transition handoff.

## Targeted gameplay-audio integration validation / 타겟 게임플레이 오디오 통합 검증
### 한국어
- 이 pass는 gameplay audio host-orchestration이 인접 presentation/runtime boundary와 정상적으로 합성되는지 검증하는 targeted integration validation이다.
- 이 pass가 검증하는 것:
  - host ordering vs VFX / exit ownership timing
  - attachment vs 2D fallback at exit boundaries
  - bootstrap / authored map invariants
  - settings / mixing coexistence with gameplay one-shot SFX
  - UI / BGM separation from gameplay host orchestration
- 이 pass가 검증하지 않는 것:
  - full gameplay-wide regression closure
- approved example:
  - `Gameplay audio host-orchestration is validated against adjacent presentation and runtime boundaries via targeted integration tests.`

### English Original
- This pass is a targeted integration validation that proves gameplay-audio host orchestration composes correctly with adjacent presentation and runtime boundaries.
- This pass validates:
  - host ordering vs VFX / exit ownership timing
  - attachment vs 2D fallback at exit boundaries
  - bootstrap / authored map invariants
  - settings / mixing coexistence with gameplay one-shot SFX
  - UI / BGM separation from gameplay host orchestration
- This pass does not validate:
  - full gameplay-wide regression closure
- Approved example:
  - `Gameplay audio host-orchestration is validated against adjacent presentation and runtime boundaries via targeted integration tests.`

## PlayMode escalation triggers / PlayMode escalation triggers
### 한국어
- UI PlayMode는 EditMode만으로 ownership behavior를 신뢰성 있게 검증할 수 없을 때만 추가한다.
- 허용 trigger:
  - real play loop가 필요한 runtime-only input routing
  - screen/popup/HUD ownership에 영향을 주는 scene lifecycle ordering / activation timing
  - EditMode 결과를 무효화할 수 있는 domain reload / play-loop behavior
- 허용되지 않는 trigger:
  - “PlayMode에서 한번 보면 좋겠다”
  - mapper / policy / reflection / presenter interaction / direct EditMode composition test

### English Original
- UI PlayMode coverage is added only when EditMode cannot credibly verify the ownership behavior being protected.
- Allowed triggers:
  - runtime-only input routing that depends on the real play loop
  - scene lifecycle ordering or activation timing that materially affects screen/popup/HUD ownership
  - domain reload or play-loop behavior that can invalidate an EditMode-only result
- Disallowed trigger:
  - “it would be nice to see it in PlayMode”
  - mapper / policy / reflection / presenter interaction / direct EditMode composition tests

## 2. Why This System Exists / 왜 이 시스템이 존재하는가
### 한국어
- Core는 반드시 결정적으로 유지되어야 한다. 그래야 로컬 게이트 실패가 “실제 로직 회귀”를 뜻하지, 광범위한 구조 변경이나 에셋 문제를 뜻하지 않게 된다.
- 실행 기반 테스트는 Core에서 분리해야 한다. 런타임이 조합된 테스트는 느리고 범위가 넓으며, 리팩터링 중에는 불안정성이 더 커지기 때문이다.
- Governance는 수동 규율로는 유지할 수 없는 경계를 자동으로 검증하기 위해 필요하다. 잘못 배치된 테스트, 오래된 stratification 산출물, 계층 위반을 시스템이 직접 잡아야 한다.

### English Original
- Core must stay deterministic so a failed local gate means a real logic regression, not a broad architecture or asset issue.
- Execution-heavy tests are separated from Core because runtime-composed behavior is slower, wider in scope, and less stable during active refactors.
- Governance is required because manual policing does not scale; the system must detect misplaced tests, stale stratification data, and boundary violations automatically.

## 3. Test Stratification Model / 테스트 계층 모델
### 한국어
#### 실행 티어
- `Core`
  - 의미: 가장 빠르고 신호 밀도가 높은 로컬 검증 경로
  - 포함 대상: 결정적 안전성 검증, 잠금된 대표 시나리오
  - 제외 대상: 넓은 회귀 스윕, 불안정한 에셋 검증, 느린 탐색성 검증
- `Extended`
  - 의미: 개발 중 유용하지만 주 로컬 게이트는 아닌 넓은 검증 경로
  - 포함 대상: 상세 변형 케이스, 구조 검증, 비최소 시나리오 검증
  - 제외 대상: Full에 남겨야 하는 대규모 광역 검증
- `Full`
  - 의미: 전체 검증 경로
  - 포함 대상: 나머지 전체 테스트, 희귀 엣지 케이스, 대형 런타임 조합, 에셋 민감 검증
  - 제외 대상: 일상 커밋 게이트로 사용하는 행위

#### 배치 계층
- `Core`
  - 결정적 입력/출력 로직만 포함한다.
- `Infrastructure`
  - 구조, 시그니처, 생성자 형태, DI/wiring, 리플렉션 기반 검증만 포함한다.
- `Integration`
  - 런타임 실행, 다중 시스템 상호작용, 파이프라인 흐름, 조합된 게임플레이 시나리오를 포함한다.

### English Original
#### Execution tiers
- `Core`
  - Meaning: fastest high-signal path used for frequent local validation.
  - Belongs here: deterministic safety coverage and locked representative scenarios.
  - Must not include: broad regression sweeps, unstable asset checks, or slow exploratory coverage.
- `Extended`
  - Meaning: broader validation that is useful during development but not the main local gate.
  - Belongs here: variation/detail coverage, structural checks, and non-minimal scenario coverage.
  - Must not include: massive broad sweeps better reserved for Full.
- `Full`
  - Meaning: complete validation path.
  - Belongs here: all remaining tests, rare edges, large runtime combinations, and asset-sensitive coverage.
  - Must not be treated as the day-to-day commit gate.

#### Placement layers
- `Core`
  - Deterministic input/output logic only.
- `Infrastructure`
  - Structure, signatures, constructor shape, DI/wiring, and reflection-only validation.
- `Integration`
  - Runtime execution, multi-system behavior, pipeline flow, and composed gameplay scenarios.

## 4. Execution Flow / 실행 흐름
### 한국어
```text
WSL CLI
  -> run_tests.sh
    -> governance check
    -> Windows dotnet build
    -> Windows Unity.exe -executeMethod
    -> TestRunnerApi bootstrap
    -> XML result write
    -> shell XML validation + failure summary + metrics
```

- Unity는 CLI `-runTests`에 의존하지 않는다.
- Unity는 `TestRunnerCliBootstrap.RunEditMode` 또는 `TestRunnerCliBootstrap.RunPlayMode`를 통해 실행된다.
- `run_tests.sh`는 실행한 worktree의 WSL path를 Windows path로 변환하고, Unity `-projectPath`가 같은 worktree를 가리키는지 먼저 검증한다.
- bootstrap이 테스트 실행, XML 기록, Unity exit code를 직접 관리한다.
- shell은 이후 XML을 검증하고, 실패 테스트를 출력하고, stage metrics를 기록한다.

### English Original
```text
WSL CLI
  -> run_tests.sh
    -> governance check
    -> Windows dotnet build
    -> Windows Unity.exe -executeMethod
    -> TestRunnerApi bootstrap
    -> XML result write
    -> shell XML validation + failure summary + metrics
```

- Unity does not rely on CLI `-runTests`.
- Unity is invoked through `TestRunnerCliBootstrap.RunEditMode` or `TestRunnerCliBootstrap.RunPlayMode`.
- `run_tests.sh` converts the current worktree WSL path to a Windows path and first verifies that Unity `-projectPath` targets that same worktree.
- The bootstrap owns test execution, XML writing, and Unity exit codes.
- The shell then validates the XML, prints failed tests, and emits stage metrics.

## 5. CLI Usage / CLI 사용법
### 한국어
#### 공개 명령
```bash
./run_tests.sh core
./run_tests.sh ui
./run_tests.sh kbo-glyph-update
./run_tests.sh typography-visual
./run_tests.sh gameplay-performance
./run_tests.sh cleanup-s3-capture-smoke
./run_tests.sh full
./run_tests.sh --print-config
./run_tests.sh --dry-run core
./run_tests.sh --dry-run typography-visual
./run_tests.sh core --filter <test-or-fixture>
./run_tests.sh core --test-filter <test-or-fixture>
```

- `./run_tests.sh --print-config`
  - 현재 worktree path 계산과 Unity project root 구조만 검증한다.
  - governance, dotnet, Unity를 실행하지 않는다.
  - 테스트 pass가 아니므로 `ALL TESTS PASSED`를 출력하지 않는다.
- `./run_tests.sh --dry-run <lane>`
  - path 검증 후 실행될 dotnet/Unity command를 출력한다.
  - governance, dotnet, Unity를 실행하지 않는다.
  - Unity command의 `-projectPath`가 현재 worktree Windows path인지 확인하는 용도다.
- `./run_tests.sh core`
  - 일반적인 로컬 개발 루프에서 사용한다.
  - governance 검사 후 Windows `dotnet` core build, Unity Core EditMode, Unity Core feature gate EditMode, Unity Core PlayMode를 순서대로 실행한다.
  - Core feature gate EditMode는 broad feature EditMode가 아니라 명시적으로 core gate에 승격된 `Phase3BGate` 테스트만 실행한다.
  - 각 Unity invocation은 shared KBO Dia Gothic SDF integrity guard 안에서 실행된다. Guard는 invocation 전 canonical tracked 상태만 소유하고, 실행 전후 전체 byte가 같은 `NO_MUTATION`만 통과시킨다. Pre-existing 또는 unexpected mutation은 덮어쓰지 않고 실패시킨다.
  - pre-commit 훅이 사용하는 명령이다.
- `./run_tests.sh ui`
  - Stage 9 이후 UI architecture hardening 및 Stage 4–8 seam preservation 검증에 사용한다.
  - Unity 시작 전에 KBO Dia Gothic Medium/Light TTF/SDF의 `HEAD` Git blob, GUID, material/atlas localID를 검사한다. working-file canonical hash와 TMP 계산 비율 `0.9/1/0.73125` 및 Unity 빈 scalar 공백을 source 계약으로 고정한다.
  - UI EditMode도 core와 동일한 shared KBO Dia Gothic SDF integrity guard를 사용하며, per-invocation evidence에는 before/imported/final SHA, classification, changed-field signature, restore 결과가 기록된다.
  - governance 검사 후 Windows `dotnet` UI test build, Unity UI EditMode assembly 실행만 수행한다.
  - `TestResults/wsl-dotnet-ui.log`, `TestResults/wsl-unity-ui-editmode.log`, `TestResults/wsl-unity-ui-editmode.xml`을 남긴다.
  - `core`를 대체하지 않으며, UI slice를 넓히기 전 targeted evidence를 얻기 위한 명령이다.
- `./run_tests.sh kbo-glyph-update`
  - committed KBO Dia Gothic Medium/Light source identity를 preflight한 뒤 현재 관리 ko-KR String Table corpus로 두 canonical TMP atlas를 재생성한다.
  - 두 runtime font를 source TTF에서 원자적으로 갱신하며 missing/fallback 0과 preserved GUID/material/atlas identity를 강제한다.
  - 테스트 lane이 아니며 filter를 받지 않는다.
- `./run_tests.sh typography-visual`
  - committed revision에서 Settings/Pause/Main Menu의 en-US/ko-KR/ja-JP/zh-CN 1920x1080 evidence를 timestamp 기반 새 디렉터리에 생성한다.
  - current worktree/Unity path, 동일 프로젝트 process, revision gate, guarded KBO Dia Gothic asset 복원, manifest PASS fields, Settings 35 및 localized 20/20, 12개 canonical PNG byte size/SHA-256을 검증한다.
  - 네 locale의 localized TMP에 대해 overflow, bounded glyph-mesh-to-authored-rect, glyph-mesh-to-capture-frame 검사를 적용한다. 2 UI unit 이하의 font side-bearing은 허용하지만 TMP overflow는 허용하지 않는다.
  - ko-KR Settings Audio muted, Settings Display status, ConfirmPopup 진단 PNG를 `Diagnostics/`에 추가 생성한다. 이 파일들은 canonical root의 exact 12-file manifest와 분리되며, 진단 capture failure는 해당 slice를 실패시킨다.
  - ja-JP/zh-CN은 reserved-key와 rebinding-prompt Settings 상태를 대표 동적 진단으로 추가한다. 이는 모든 동적 상태나 지원 해상도의 전수 visual QA를 뜻하지 않는다.
  - raw Unity log와 canonical `capture.log`을 분리하고, 기존 output은 overwrite하지 않으며 실패 output도 진단을 위해 보존한다.
  - `./run_tests.sh --dry-run typography-visual`은 실제 Unity path, current worktree project path, execute method, output/log/manifest path, 1920x1080, isolated slice 인자를 출력한다.
- `./run_tests.sh gameplay-performance`
  - `stage-1-1` canonical gameplay shell을 1920x1080 PC 품질의 Windows Mono Player로 실행해 `render-idle`과 `gameplay-neutral-tick`을 각각 600프레임 측정한다.
  - Player는 `BuildOptions.None`을 사용하되 측정용 capture define과 Frame Timing Stats만 임시 활성화하므로 `ReleaseLikeCapture`이지 store 배포 artifact와 byte-identical한 production release는 아니다.
  - VSync와 target frame cap을 끄고 frame interval, CPU main/render, GPU, 실제 tick wall time, draw calls의 median/P95/P99/max를 기록한다. Non-Development Player에서 GC Profiler counter가 비활성일 때는 availability를 false로 명시한다.
  - 제품 성능 예산이 아직 고정되지 않았으므로 `GAMEPLAY_PERFORMANCE:PASS`는 capture/instrumentation 성공만 뜻하고 metrics의 `budgetVerdict`는 `NOT_CONFIGURED`로 남긴다.
  - Unity build가 건드릴 수 있는 ProjectSettings, Scriptable Build Pipeline 설정, PC render pipeline asset, Addressables settings/Windows metadata와 generated `link.xml`은 build 전 존재 여부와 byte snapshot으로 복원한다. 기존 사용자 변경을 canonical state로 간주하여 덮어쓰지 않는다.
  - 새 evidence는 `/mnt/d/J2M/evidence/gameplay-performance`, build는 `/mnt/d/J2M/builds/gameplay-performance` 아래 timestamp 디렉터리에 저장한다. 이 lane은 성능 수집이며 `core`, `ui`, `full` 회귀 검증을 대체하지 않는다.
- `./run_tests.sh cleanup-s3-capture-smoke`
  - `VECTORQUAKE_CAPTURE_BUILD` Player에서 Cleanup S3-A actual producer JSON을 생성하고 reference cardinality/parity, v4 context, performance/Cleanup admission, calibration, final manifest transport를 한 경계로 검증한다.
  - 공식 성능 capture나 성능 verdict가 아니다. evidence와 build는 각각 `/mnt/d/J2M/evidence/cleanup-s3-capture-smoke/<uuid>`와 `/mnt/d/J2M/builds/cleanup-s3-capture-smoke/<uuid>`의 exclusive leaf에 저장하며 `gameplay-performance` namespace를 사용하지 않는다.
  - allocation signal이 유효하면 final v4 manifest는 미승인 full-scan expectation 때문에 authoritative `HOLD`를 유지한다. 현재 머신처럼 allocation liveness가 `0`이면 exact allocation-only Cleanup rejection/Hold만 허용하며 reference sub-contract는 계속 green이어야 한다.
  - wrapper 성공은 `Cleanup S3 capture smoke: PASS (non-official; authoritative manifest remains HOLD)`만 출력한다. 이는 `core`, `ui`, `full`, 공식 capture를 대체하지 않는다.
- `./run_tests.sh full`
  - 안정화 직전, 통합 직전, 혹은 넓은 회귀를 조사할 때 사용한다.
  - governance 검사 후 Windows solution build, Unity Full EditMode, Unity Full PlayMode를 실행한다.
  - 첫 실패 stage에서 즉시 중단된다.
- `--filter` / `--test-filter`
  - 두 옵션은 동일하며 Unity bootstrap의 `-codexTestFilter`로 전달된다.
  - `core --filter X`는 lane-preserving이다. broad `core` lane에 포함되는 테스트 중 `X`와 매치되는 테스트만 실행하며, category 또는 gate 범위를 풀지 않는다.
  - filtered run에서는 일부 Unity stage가 `0`개를 실행할 수 있다. shell은 전체 core lane 합산 match가 `0`일 때만 fail-fast한다.
  - fixture 전체 실행이 필요한 PlayMode 테스트는 `full --filter X`로 실행한다. 예: `./run_tests.sh full --filter PlayerMovementPlayModeTests`.
  - broad core evidence와 fixture-wide targeted evidence는 서로 다른 claim으로 보고해야 한다.

- `UNITY_TEST_TIMEOUT_SECONDS`는 Unity test bootstrap watchdog의 초 단위 예산이다(기본 `285`). runner의 외부 timeout은 이 값에 `15`초를 더한다(기본 `300`); 강제 종료의 추가 `10`초 grace와 asset/evidence cleanup은 유지한다.
  - 넓은 suite 또는 초기 import에 기본 예산이 부족하면 `UNITY_TEST_TIMEOUT_SECONDS=900 ./run_tests.sh full`처럼 실행별로 명시한다. 양의 정수만 허용하며 `--print-config`/`--dry-run`으로 실제 두 예산을 확인한다.
  - runner가 bootstrap 인자로 전달하므로 이 설정은 `WSLENV` 추가가 필요 없다. bootstrap은 duration과 절대 deadline을 SessionState에 보존해 domain reload가 예산을 다시 시작하지 않게 한다.
  - timeout 종료 또는 XML 미생성은 테스트 통과가 아니다. capture/S3/visual interruption의 별도 timeout·종료 계약을 바꾸지 않는다.

#### 종료 코드
- `0`: 성공
- `1`: 테스트 실패 또는 shell 검증 실패
- `2`: Unity 내부 bootstrap/runner/automation infrastructure 오류

#### 고급/내부 모드
- 통합 계층만 따로 실행하는 internal integration mode가 존재한다.
- 일반 개발 워크플로의 기본 경로는 아니며 `core`와 `full`을 대체해서는 안 된다.

### English Original
#### Public commands
```bash
./run_tests.sh core
./run_tests.sh ui
./run_tests.sh kbo-glyph-update
./run_tests.sh typography-visual
./run_tests.sh gameplay-performance
./run_tests.sh cleanup-s3-capture-smoke
./run_tests.sh full
./run_tests.sh --print-config
./run_tests.sh --dry-run core
./run_tests.sh --dry-run typography-visual
./run_tests.sh core --filter <test-or-fixture>
./run_tests.sh core --test-filter <test-or-fixture>
```

- `./run_tests.sh --print-config`
  - Validates current worktree path calculation and Unity project root shape only.
  - Does not run governance, dotnet, or Unity.
  - Does not print `ALL TESTS PASSED` because it is not a test pass.
- `./run_tests.sh --dry-run <lane>`
  - Validates paths, then prints the dotnet/Unity commands that would run.
  - Does not run governance, dotnet, or Unity.
  - Use it to confirm Unity `-projectPath` is the current worktree Windows path.
- `./run_tests.sh core`
  - Use for normal local development.
  - Runs governance first, then Windows `dotnet` core build, Unity Core EditMode, Unity Core feature gate EditMode, and Unity Core PlayMode.
  - Core feature gate EditMode is not broad feature EditMode. It runs only tests explicitly promoted into the `Phase3BGate` core gate.
  - Every Unity invocation runs inside the shared KBO Dia Gothic SDF integrity guard. The guard owns only a canonical tracked pre-state, requires byte-identical pre/post state (`NO_MUTATION`), and fails without overwriting pre-existing or unexpected mutations.
  - This is the command used by pre-commit.
- `./run_tests.sh ui`
  - Use for targeted Stage 9 UI hardening and Stage 4–8 seam-preservation validation.
  - Before Unity starts, validates KBO Dia Gothic Medium/Light TTF/SDF `HEAD` Git blobs, GUIDs, and material/atlas local IDs. Working-file canonical hashes, TMP-calculated ScaleRatio values `0.9/1/0.73125`, and Unity empty-scalar whitespace define the serialized source contract.
  - UI EditMode uses the same shared KBO Dia Gothic SDF integrity guard as core, with per-invocation evidence for before/imported/final SHA, classification, changed-field signature, and restore outcome.
  - Runs governance first, then Windows `dotnet` build for `Game.Feature.UI.Tests.csproj`, then Unity EditMode with the `ui` selection in `TestRunnerCliBootstrap`.
  - Writes `TestResults/wsl-dotnet-ui.log`, `TestResults/wsl-unity-ui-editmode.log`, and `TestResults/wsl-unity-ui-editmode.xml`.
  - It does not replace `core`; it exists to provide explicit Unity-side evidence for the UI assembly before broader UI expansion.
- `./run_tests.sh kbo-glyph-update`
  - Preflights committed KBO Dia Gothic Medium/Light source identity, then regenerates both canonical TMP atlases from the managed ko-KR String Table corpus.
  - Atomically updates both runtime fonts from their source TTFs, enforcing zero missing glyphs, zero fallback dependency, and preserved GUID/material/atlas identity.
  - This is an asset-generation lane, not a test lane, and it does not accept filters.
- `./run_tests.sh typography-visual`
  - Generates timestamped 1920x1080 Settings/Pause/Main Menu evidence for en-US, ko-KR, ja-JP, and zh-CN from a committed revision.
  - Validates the current worktree/Unity path, same-project process exclusion, revision gate, guarded KBO Dia Gothic asset restoration, manifest PASS fields, exact Settings 35 and localized 20/20 counts, and all 12 canonical PNG byte sizes/SHA-256 hashes.
  - Applies localized TMP overflow, bounded glyph-mesh-to-authored-rect, and glyph-mesh-to-capture-frame checks in all four locales. Font side bearings up to two UI units are tolerated, but TMP overflow is not.
  - It also creates ko-KR Settings Audio muted, Settings Display status, and ConfirmPopup diagnostics under `Diagnostics/`. They remain outside the exact 12-file canonical root manifest, and a diagnostic capture failure fails its slice.
  - ja-JP and zh-CN add reserved-key and rebinding-prompt Settings captures as representative dynamic diagnostics. This is not exhaustive visual QA across every dynamic state or supported resolution.
  - Separates raw Unity logs from canonical `capture.log`, refuses existing output directories, and retains failed output for diagnostics.
  - `./run_tests.sh --dry-run typography-visual` prints the real Unity/current-worktree paths, execute method, output/log/manifest paths, 1920x1080 resolution, and isolated slice arguments without launching Unity.
- `./run_tests.sh gameplay-performance`
  - Runs the canonical `stage-1-1` gameplay shell in a 1920x1080 PC-quality Windows Mono Player and samples 600 frames each for `render-idle` and `gameplay-neutral-tick`.
  - The Player uses `BuildOptions.None` with only the capture define and Frame Timing Stats temporarily enabled, so it is a `ReleaseLikeCapture`, not a byte-identical store production artifact.
  - Disables VSync and the target frame cap, then records median/P95/P99/max for frame interval, CPU main/render, GPU, actual tick wall time, and draw calls. When the GC Profiler counter is unavailable in a non-Development Player, the manifest records availability as false.
  - Because no product performance budget is pinned yet, `GAMEPLAY_PERFORMANCE:PASS` means capture/instrumentation success only and `budgetVerdict` remains `NOT_CONFIGURED`.
  - Restores ProjectSettings, Scriptable Build Pipeline settings, the PC render-pipeline asset, Addressables settings/Windows metadata, and generated `link.xml` to their pre-build existence state and byte snapshots, treating pre-existing user changes as the state to preserve.
  - Stores timestamped evidence under `/mnt/d/J2M/evidence/gameplay-performance` and builds under `/mnt/d/J2M/builds/gameplay-performance`. This performance capture does not replace `core`, `ui`, or `full` regression validation.
- `./run_tests.sh cleanup-s3-capture-smoke`
  - Produces actual Cleanup S3-A JSON in a `VECTORQUAKE_CAPTURE_BUILD` Player and validates reference cardinality/parity, v4 context, performance/Cleanup admission, calibration, and final-manifest transport as one cross-boundary chain.
  - This is not an official performance capture or performance verdict. It uses exclusive UUID leaves under `/mnt/d/J2M/evidence/cleanup-s3-capture-smoke` and `/mnt/d/J2M/builds/cleanup-s3-capture-smoke`, never the `gameplay-performance` namespace.
  - With a valid allocation signal, the final v4 manifest remains authoritatively `HOLD` only because the full-scan expectation is unapproved. If allocation liveness is `0`, only the exact allocation-only Cleanup rejection/Hold envelope is accepted and the reference sub-contract must still be green.
  - The only wrapper success line is `Cleanup S3 capture smoke: PASS (non-official; authoritative manifest remains HOLD)`. It does not replace `core`, `ui`, `full`, or an official capture.
- `./run_tests.sh full`
  - Use before stabilization, integration, or when investigating broader regressions.
  - Runs governance first, then Windows solution build, then Unity Full EditMode and Full PlayMode.
  - Stops on the first failing stage.
- `--filter` / `--test-filter`
  - The two options are aliases and are forwarded to the Unity bootstrap as `-codexTestFilter`.
  - `core --filter X` is lane-preserving. It runs only tests matching `X` inside the broad `core` lane and does not remove category or gate scope.
  - A filtered run may execute `0` tests in some Unity stages. The shell fails fast only when the aggregate match count across the core lane is `0`.
  - Use `full --filter X` when the full PlayMode fixture is the intended evidence. Example: `./run_tests.sh full --filter PlayerMovementPlayModeTests`.
  - Broad core evidence and fixture-wide targeted evidence must be reported as separate claims.

- `UNITY_TEST_TIMEOUT_SECONDS` is the Unity test-bootstrap watchdog budget in seconds (default `285`). The outer runner timeout adds `15` seconds (default `300`); the additional `10`-second kill grace and asset/evidence cleanup remain in place.
  - When a broad suite or initial import exceeds the default budget, opt in per run, for example `UNITY_TEST_TIMEOUT_SECONDS=900 ./run_tests.sh full`. Only positive integers are accepted; inspect both budgets with `--print-config`/`--dry-run`.
  - The runner forwards an explicit bootstrap argument, so this setting needs no `WSLENV` entry. The bootstrap persists the duration and absolute deadline in SessionState so domain reload does not restart the budget.
  - A timeout or missing XML is not a test pass. Separate capture/S3/visual-interruption timeout and termination contracts are unchanged.

#### Exit codes
- `0`: success.
- `1`: test failure or shell-side validation failure.
- `2`: runner/bootstrap/infrastructure error inside Unity or the automation layer.

#### Advanced/internal modes
- Internal integration selections exist for targeted CI/debug runs.
- They are not the normal developer workflow and should not replace `core` or `full`.

## 6. Pre-commit Behavior / Pre-commit 동작
### 한국어
- 로컬 Git pre-commit 훅은 `./run_tests.sh core`를 실행한다.
- 0이 아닌 종료 코드는 모두 커밋을 막는다.
- 의도는 엄격하지만 범위는 좁다.
  - 결정적 안전 계층의 회귀는 반드시 커밋을 막아야 한다.
  - 전체 스위트의 불안정성은 일상 로컬 커밋 게이트가 되어서는 안 된다.

### English Original
- The local Git pre-commit hook runs `./run_tests.sh core`.
- Any nonzero exit blocks the commit.
- The intent is strict but narrow:
  - deterministic safety regressions must block commits
  - full-suite instability must not become the everyday local gate

## 7. Governance System / Governance 시스템
### 한국어
- Governance가 검사하는 대상:
  - 테스트 배치 경계
  - Core purity 규칙
  - gameplay semantic query migration boundary
  - Integration 밖에 놓인 execution-based test
  - source category / override inventory 일관성
  - PlayMode Core category count / cap
- active truth-source:
  - `./run_tests.sh core`
  - `./run_tests.sh full`
  - pinned baseline doc
  - touched cluster readout
  - grep gate for removed structural vocabulary
  - semantic query migration source-scan gate
- 규칙 정의 위치:
  - `Tools/gameplay_test_stratification_lib.py`
  - `Tools/check_gameplay_semantic_query_migration.py`
- 규칙 소비 위치:
  - `Tools/check_gameplay_test_stratification.py`
  - `Tools/generate_gameplay_test_stratification.py --check`
  - `Tools/semantic_query_migration_allowlist.json`
- runner integration:
  - `run_tests.sh`는 checker를 실행하지만 `gameplay_test_stratification_lib.py`를 직접 import하지 않는다.
  - `TestRunnerCliBootstrap`의 Core PlayMode selection은 persisted manifest가 아니라 `assemblyNames + categoryNames("Core")`를 사용한다.
- trace/log interpretation rule:
  - governance-facing canonical structured trace surface는 `Plan=` / `SourcePlan=`다.
  - free-form `G=`는 compatibility token in free-form event log이며 current `ActionPlanId` value를 mirror하지만 old semantic GroupId revival이 아니다.
  - 새 테스트/도구는 `G=` 대신 `ActionPlanId` / `SourceActionPlanId` 또는 `Plan=` / `SourcePlan=`를 읽어야 한다.
- historical/non-canonical:
  - [Docs/Archive/Architecture/Gameplay-Test-Stratification.md](../Archive/Architecture/Gameplay-Test-Stratification.md)
- 모드:
  - 로컬 기본값: `soft`
  - CI 기본값: `strict`
  - 명시적 override: `STRATIFICATION_GOVERNANCE_MODE=soft|strict`

### English Original
- Governance checks:
  - test placement boundaries
  - Core purity rules
  - gameplay semantic query migration boundary
  - execution-based tests outside Integration
  - source category / override inventory consistency
  - PlayMode Core category count / cap
- active truth sources:
  - `./run_tests.sh core`
  - `./run_tests.sh full`
  - the pinned baseline doc
  - touched-cluster readouts
  - grep gates for removed structural vocabulary
  - the semantic query migration source-scan gate
- Rule source:
  - `Tools/gameplay_test_stratification_lib.py`
  - `Tools/check_gameplay_semantic_query_migration.py`
- Rule consumers:
  - `Tools/check_gameplay_test_stratification.py`
  - `Tools/generate_gameplay_test_stratification.py --check`
  - `Tools/semantic_query_migration_allowlist.json`
- Runtime wiring stage note:
  - `Tools/semantic_query_migration_allowlist.json` is expected to be empty after post-semantic-migration runtime wiring lands.
  - Any new entry is a temporary quarantine and must be removed in the same staged migration thread that introduced it.
- Runner integration:
  - `run_tests.sh` invokes the checker but no longer imports `gameplay_test_stratification_lib.py` directly.
  - `TestRunnerCliBootstrap` now uses `assemblyNames + categoryNames("Core")` for Core PlayMode selection instead of a persisted manifest.
- Trace/log interpretation rule:
  - the governance-facing canonical structured trace surface is `Plan=` / `SourcePlan=`
  - free-form `G=` is a compatibility token in the free-form event log; it mirrors the current `ActionPlanId` value and is not an old semantic GroupId revival
  - new tests/tools must read `ActionPlanId` / `SourceActionPlanId` or `Plan=` / `SourcePlan=`, not `G=`
- Historical/non-canonical:
  - [Docs/Archive/Architecture/Gameplay-Test-Stratification.md](../Archive/Architecture/Gameplay-Test-Stratification.md)
- Modes:
  - local default: `soft`
  - CI default: `strict`
  - explicit override: `STRATIFICATION_GOVERNANCE_MODE=soft|strict`

## 8. Governance Philosophy / Governance 철학
### 한국어
- Governance는 자동 경계 강제 시스템이다.
- 개발자가 모든 테스트의 위치를 수동으로 기억할 필요가 없도록 만드는 것이 목적이다.
- 시스템은 다음을 유지하기 위해 존재한다.
  - Core는 순수하게
  - Integration은 동작 중심으로
  - Infrastructure는 구조 중심으로
- Governance 실패에 대한 기본 대응은 규칙 약화가 아니라 테스트 이동 또는 분리다.

### English Original
- Governance is automatic boundary enforcement.
- Developers should not have to manually remember where every test belongs.
- The system exists to keep:
  - Core pure
  - Integration behavioral
  - Infrastructure structural
- The correct response to a governance failure is usually to move or split a test, not to weaken the rule.

## 9. Test Classification Rules / 테스트 분류 규칙
### 한국어
- `Core`
  - 결정적 입력/출력 로직만 허용
  - 파이프라인 실행 금지
  - composition root 또는 bootstrap wiring 금지
  - non-public reflection 금지
- `Infrastructure`
  - 구조/시그니처/리플렉션 전용
  - 게임플레이 시뮬레이션 금지
  - 런타임 동작 결과 검증 금지
- `Integration`
  - 런타임 실행 포함
  - 다중 시스템 동작 포함
  - 파이프라인 흐름, tick progression, 조합된 게임플레이 시나리오 포함

### English Original
- `Core`
  - deterministic input/output logic only
  - no pipeline execution
  - no composition root or bootstrap wiring
  - no non-public reflection
- `Infrastructure`
  - structure/signature/reflection only
  - no gameplay simulation
  - no runtime behavior assertions
- `Integration`
  - runtime execution
  - multi-system behavior
  - pipeline flow, tick progression, and composed gameplay scenarios

## 10. Test Classification Decision Table / 테스트 분류 결정표
### 한국어
| 테스트가 다음에 해당하면... | 배치 위치 |
| --- | --- |
| `RunTick`, `TickPipeline`, `TickRunner`, `CreateTickPipeline`, `CreateTickRunner`를 호출한다 | `Integration` |
| `BindingFlags`, `GetFields`, `GetConstructors`, 생성자 검사, DI/wiring 검사, composition 검사를 사용한다 | `Infrastructure` |
| 결정적 입력/출력만 검증한다 | `Core` |

- 애매하면 `Core`보다 `Integration`을 우선한다.

### English Original
| If the test... | Put it in... |
| --- | --- |
| Calls `RunTick`, `TickPipeline`, `TickRunner`, `CreateTickPipeline`, or `CreateTickRunner` | `Integration` |
| Uses `BindingFlags`, `GetFields`, `GetConstructors`, constructor checks, DI/wiring checks, or composition checks | `Infrastructure` |
| Validates deterministic input/output only | `Core` |

- When in doubt, prefer `Integration` over `Core`.

## 11. Ambiguous Cases / 애매한 경우 처리
### 한국어
- 실행 + 구조 검증이 섞인 테스트
  - 반드시 분리한다. 실행은 Integration, 구조/wiring은 Infrastructure에 둔다.
- helper 함수가 실행을 숨기는 경우
  - helper를 경유해도 분류는 바뀌지 않는다.
  - helper가 결국 pipeline flow를 실행하면 그 테스트는 execution-based test다.
- 부분 파이프라인 실행
  - 전체 시나리오가 아니어도 실행이면 실행이다.
  - 일부 phase만 실행해도 Integration이다.
- 강한 규칙:
  - 확신이 없으면 pure하다고 입증되기 전까지 `Integration`으로 분류한다.

### English Original
- Mixed execution + structure test
  - Split it. Execution stays in Integration. Structure/wiring stays in Infrastructure.
- Helper functions hiding execution
  - Helper indirection does not change classification.
  - If a helper eventually runs pipeline flow, the test is still execution-based.
- Partial pipeline execution
  - Partial execution is still execution.
  - A test does not need a full scenario to qualify as Integration.
- Hard rule:
  - If unclear, treat the test as `Integration` until proven pure.

## 12. Migration Rules / 테스트 이동 규칙
### 한국어
- 실행 테스트 -> `Integration`
- reflection / constructor / DI / composition 테스트 -> `Infrastructure`
- 결정적 로직 테스트 -> `Core`
- mixed test는 허용되지 않는다.
- 하나의 테스트가 런타임 실행과 구조 검증을 동시에 수행하면:
  - 동작 검증은 Integration으로
  - 구조 검증은 Infrastructure로 분리한다.

### English Original
- Execution test -> `Integration`
- Reflection / constructor / DI / composition test -> `Infrastructure`
- Deterministic logic test -> `Core`
- No mixed tests allowed.
- If one test mixes runtime execution and structure validation:
  - split the behavior assertions into Integration
  - split the structure assertions into Infrastructure

## 13. Real Examples / 실제 예시
### 한국어
#### Before
- 하나의 테스트가 `RunTick`을 호출하고 동시에 reflection으로 constructor shape도 검사한다.

#### After
- `Integration` test
  - `RunTick`을 실행한다.
  - 결과 동작, 상태, 이벤트 로그, phase output을 검증한다.
- `Infrastructure` test
  - 생성자 공개 여부, provider wiring, type shape를 검증한다.
- `Core` test
  - 순수 comparer, normalizer, ordering rule, buffer behavior를 결정적 입력으로 검증한다.

### English Original
#### Before
- One test calls `RunTick` and also checks constructor shape with reflection.

#### After
- `Integration` test
  - runs `RunTick`
  - verifies resulting behavior, state, event log, or phase output
- `Infrastructure` test
  - checks constructor visibility, provider wiring, or type shape
- `Core` test
  - validates a pure comparer, normalizer, ordering rule, or buffer behavior with deterministic inputs

## 13-1. Assertion Contract Rules / 테스트 assertion 계약 규칙
### 한국어
- 테스트 assertion은 먼저 아래 세 범주 중 무엇을 검증하는지 구분해야 한다.
  - `Structural contract`: 깨지면 wiring, ownership, prefab 구조, 참조 경계가 깨지는 값이다. 정확한 참조나 축, 필수 component 존재 여부는 고정해도 된다.
  - `Behavior contract`: 알고리즘 의미나 런타임 동작이 깨지는 값이다. 테스트가 직접 설정한 fixture 값은 exact assertion으로 검증해도 된다.
  - `Tuning value`: prefab, ScriptableObject, asset authoring에서 감각적으로 조정될 수 있는 연출, 밸런스, 타이밍 값이다. 기본값은 exact assertion으로 고정하지 않는다.
- prefab / ScriptableObject / asset 검증에서 numeric tuning 값을 검사할 때는 exact equality보다 유효 범위, 양수 여부, null 아님, 참조 연결, fallback 미사용 같은 계약을 우선한다.
- exact numeric assertion은 테스트가 직접 설정한 fixture 값이거나, 문서화된 locked design profile / timing preset / constant contract일 때만 사용한다.
- locked tuning을 검증해야 한다면 테스트 이름, assertion message, 관련 변경 설명이 그 값이 튜닝 자유도가 아니라 의도적으로 잠긴 계약임을 드러내야 한다.
- 테스트 이름은 assertion 범위를 벗어나면 안 된다. 예를 들어 `Binds...ToModelRoot` 테스트는 binding과 ownership만 검증하고 연출 튜닝값을 고정하지 않는다.

### English Original
- Test assertions must first classify what they are protecting.
  - `Structural contract`: values that would break wiring, ownership, prefab structure, or reference boundaries if changed. Exact references, axes, and required component presence may be pinned.
  - `Behavior contract`: values that would break algorithm semantics or runtime behavior. Fixture values set directly by the test may use exact assertions.
  - `Tuning value`: presentation, balance, or timing values authored on prefabs, ScriptableObjects, or assets for iteration. Do not pin these with exact assertions by default.
- For prefab / ScriptableObject / asset tests, prefer contract checks such as valid ranges, positive values, non-null references, connected references, and no fallback use over exact numeric equality for tuning values.
- Use exact numeric assertions only for values set by the test fixture itself, or for documented locked design profiles, timing presets, or constant contracts.
- When a locked tuning value must be tested, the test name, assertion message, and change description must make it clear that the value is an intentionally locked contract rather than ordinary tuning.
- Test names must not overreach their assertion scope. For example, a `Binds...ToModelRoot` test should verify binding and ownership, not pin presentation tuning values.

## 14. Failure Output & Debugging / 실패 출력과 디버깅
### 한국어
- 테스트 실행이 성공으로 인정되려면 XML은 다음을 모두 만족해야 한다.
  - 파일이 존재한다.
  - 비어 있지 않다.
  - `<test-run`을 포함한다.
  - `total="..."`을 포함한다.
  - 총 테스트 수가 0이 아니다.
- shell은 다음을 출력한다.
  - 실패한 테스트 이름
  - 첫 줄 실패 이유
  - stage별 metric
- 로그와 결과 파일은 검증 중인 현재 worktree의 `TestResults/` 아래에 기록된다.
- 일반적으로 확인하는 파일:
  - Unity 로그
  - Unity XML 결과
  - Windows `dotnet` 로그

#### Failure Types
- `Core failure`
  - 결정적 로직 회귀
  - 즉시 수정해야 한다.
- `Integration failure`
  - 런타임 또는 시스템 동작 문제
- `Infrastructure failure`
  - 구조, 시그니처, 생성자, wiring 계약 위반

### English Original
- A test run is only accepted when the XML:
  - exists
  - is non-empty
  - contains `<test-run`
  - contains `total="..."`
  - reports a nonzero test count
- The shell prints:
  - failed test names
  - first-line failure reasons
  - per-stage metrics
- Logs and results are written under the current worktree's `TestResults/`.
- Typical files include:
  - Unity logs
  - Unity XML results
  - Windows `dotnet` logs

#### Failure Types
- `Core failure`
  - deterministic logic regression
  - fix immediately
- `Integration failure`
  - runtime/system behavior issue
- `Infrastructure failure`
  - architecture, signature, constructor, or wiring contract violation

## 15. Interpreting Governance Failures / Governance 실패 해석
### 한국어
- Governance 실패는 보통 “테스트가 잘못된 계층에 있다”는 뜻이다.
- 이것이 곧 테스트 대상 로직이 틀렸다는 뜻은 아니다.
- 기본 대응 순서:
  - 테스트를 이동한다.
  - 테스트를 분리한다.
  - 그 뒤에도 실패하면, 그때 로직 문제를 다시 본다.

### English Original
- Governance failure means the test is in the wrong layer.
- It does not necessarily mean the tested logic is wrong.
- Expected response:
  - move the test
  - split the test
  - only then revisit logic if the test is correctly placed and still failing

## 16. Metrics & Reliability Signals / 메트릭과 신뢰성 신호
### 한국어
- 각 Unity stage는 다음을 출력한다.
  - 총 테스트 수
  - 실패 테스트 수
  - failure ratio
  - duration seconds
- 자동화는 다음과 같은 의심스러운 실행 패턴에 대해 warning을 낸다.
  - 실행된 테스트 수가 예상보다 급감함
  - 최근 성공 실행 대비 비정상적으로 짧은 실행 시간
- 이 warning은 “로직 버그 확정”이 아니라 “실행 신뢰성 신호”다.

### English Original
- Each Unity stage prints:
  - total tests
  - failed tests
  - failure ratio
  - duration in seconds
- The automation also emits warnings for suspicious runs, such as:
  - unexpected drops in executed test count
  - unexpectedly short duration compared with recent successful runs
- These warnings are reliability signals, not automatic proof of a logic bug.

## 17. Current Known Limitations / 현재 알려진 제한 사항
### 한국어
- PlayMode는 아직 완전히 stratified되지 않았다.
- Full 실행은 Core 게이트 밖의 런타임 문제나 에셋 문제로 여전히 실패할 수 있다.
- `core`만이 일상 개발에서 사용하는 엄격한 게이트다.
- 이것은 의도된 설계다.
  - Core는 개발 속도를 최적화한다.
  - Full은 더 넓은 검증 경로로 남는다.

### English Original
- PlayMode is not fully stratified yet.
- Full runs may still fail because of runtime or asset issues outside the Core gate.
- `core` is the only strict day-to-day gate.
- This is intentional:
  - Core optimizes developer velocity
  - Full remains the broader validation path

## 18. Recommended Workflow / 권장 워크플로
### 한국어
1. `./run_tests.sh core`를 자주 실행한다.
2. 실패하면 먼저 실패 유형을 분류한다.
3. 로직 버그를 수정하거나 잘못 배치된 테스트를 이동/분리한다.
4. `core`를 다시 실행한다.
5. 주기적으로, 그리고 안정화/통합 전에 `full`을 실행한다.

- Core 실패는 미루지 않는다.
- 리팩터링 중 Full 실패는 즉시 차단 신호라기보다 참고 신호가 될 수 있다.

### English Original
1. Run `./run_tests.sh core` frequently.
2. If it fails, classify the failure first.
3. Fix the logic bug or move/split the misplaced test.
4. Rerun `core`.
5. Run `full` periodically and before stabilization or integration.

- Core failures should not be deferred.
- Full failures during active refactors can be informative without blocking local progress.

## 19. Do / Don’t Rules / 해야 할 것과 하지 말아야 할 것
### 한국어
#### Do
- Core를 결정적으로 유지한다.
- mixed test를 분리한다.
- execution test를 Integration으로 이동한다.
- governance failure를 먼저 배치 문제로 해석한다.

#### Don’t
- execution test를 Core에 넣지 않는다.
- reflection-only test를 Integration에 넣지 않는다.
- category filtering을 실행 모델로 기대하지 않는다.
- classification을 고치지 않고 governance를 우회하지 않는다.

### English Original
#### Do
- Keep Core deterministic.
- Split mixed tests.
- Move execution tests into Integration.
- Treat governance failures as placement problems first.

#### Don’t
- Put execution tests in Core.
- Put reflection-only tests in Integration.
- Rely on category filtering as the execution model.
- Bypass governance instead of fixing classification.

## 20. Future Evolution / 향후 발전 방향
### 한국어
- PlayMode stratification의 추가 정교화
- Integration 범위의 확장
- internal integration selection을 활용한 CI 병렬화

### English Original
- Fuller PlayMode stratification.
- Broader Integration expansion.
- CI parallelization through internal integration selections.

## 21. Post-stage-content bounded lane reporting / post-stage-content bounded lane reporting
### 한국어
- post-stage-content 후속은 하나의 giant refactor가 아니라 bounded lane 집합으로 보고한다.
- lane별 claim은 실제로 실행한 lane evidence만 말해야 한다.
- post-stage-content bounded lane 운영 상세는 [Post-Stage-Content-Bounded-Lane-Operations.md](./Post-Stage-Content-Bounded-Lane-Operations.md)를 따른다.
- close note minimum common format은 [Bounded-Lane-Close-Template.md](./Bounded-Lane-Close-Template.md)를 따른다.
- direct-play adoption 운영 checklist는 [Stage-Editor-Direct-Play-Adoption-Checklist.md](./Stage-Editor-Direct-Play-Adoption-Checklist.md)를 따른다.
- direct-play smoke cycle evidence format은 [Stage-Editor-Direct-Play-Smoke-Cycle-Template.md](./Stage-Editor-Direct-Play-Smoke-Cycle-Template.md)를 따른다.
- 공식 claim vocabulary:
  - `core lane validated`
    - claim 가능 조건: same revision `./run_tests.sh core` 또는 동등한 core lane pass
    - imply하지 않는 것: `ui lane validated`, `full-lane baseline recovered`, `broad project-wide green`
  - `ui lane validated`
    - claim 가능 조건: same revision `./run_tests.sh ui` 또는 동등한 UI lane pass
    - imply하지 않는 것: core lane, full lane, broad project-wide recovery
  - `targeted architecture/CI validated`
    - claim 가능 조건: same revision targeted architecture tests + CI/validator commands가 명시적으로 pass
    - imply하지 않는 것: unrelated backlog closure, broad lane recovery
  - `full-lane baseline recovered`
    - claim 가능 조건: same revision `./run_tests.sh full` green, post-stage-content live recovery stream `A1/A2/A3/A4` open row `0`, cross-lane blocking handoff `0`
    - imply하지 않는 것: build/manual companion lane를 포함한 `broad project-wide green`
  - `broad project-wide green`
    - claim 가능 조건: `full-lane baseline recovered` + required companion automation/build/manual lane가 same revision, same execution window에서 모두 validated
    - imply하지 않는 것: none beyond that exact executed window
- disallowed wording:
  - `full-lane green`
  - `all regressions are closed`
  - `full regression is closed`
  - `project-wide green` without the bounded evidence set above
- direct-play hard adoption evidence:
  - `Cycle 1`, `Cycle 2` cycle note를 같은 checkpoint window에 남긴다.
  - checkpoint window는 최대 `7` calendar days다.
  - `Counter Summary`는 `launcher bypass 정상 workflow 기록`, `fallback 요구 issue`를 함께 계수한다.
- Lane A recovery ledger:
  - same-revision full XML이 lane A live oracle이다.
  - row ledger는 `status`, `classification date`, `source artifact`, `first wrong oracle`, `owner lane`, `current owner`, `required evidence`, `next action`, `last reviewed at`를 최소로 남긴다.
  - handoff acceptance는 current same-revision artifact 없이는 기록하지 않는다.

### English Original
- Post-stage-content follow-up must be reported as a bounded-lane set, not as one giant refactor.
- Lane claims must describe only the evidence that actually ran for that lane.
- See [Post-Stage-Content-Bounded-Lane-Operations.md](./Post-Stage-Content-Bounded-Lane-Operations.md) for bounded-lane operating details.
- See [Bounded-Lane-Close-Template.md](./Bounded-Lane-Close-Template.md) for the minimum common close-note format.
- See [Stage-Editor-Direct-Play-Adoption-Checklist.md](./Stage-Editor-Direct-Play-Adoption-Checklist.md) for the direct-play adoption checklist.
- See [Stage-Editor-Direct-Play-Smoke-Cycle-Template.md](./Stage-Editor-Direct-Play-Smoke-Cycle-Template.md) for the direct-play smoke cycle evidence format.
- Official claim vocabulary:
  - `core lane validated`
    - may be claimed when the same-revision `./run_tests.sh core` or an equivalent core lane passed
    - does not imply `ui lane validated`, `full-lane baseline recovered`, or `broad project-wide green`
  - `ui lane validated`
    - may be claimed when the same-revision `./run_tests.sh ui` or an equivalent UI lane passed
    - does not imply core, full-lane, or broad project-wide recovery
  - `targeted architecture/CI validated`
    - may be claimed when the same-revision targeted architecture tests plus CI/validator commands explicitly passed
    - does not imply unrelated backlog closure or broad recovery
  - `full-lane baseline recovered`
    - may be claimed only when the same-revision `./run_tests.sh full` is green, live recovery streams `A1/A2/A3/A4` are all closed, and cross-lane blocking handoffs are `0`
    - does not imply `broad project-wide green`
  - `broad project-wide green`
    - may be claimed only when `full-lane baseline recovered` plus the required companion automation/build/manual lanes are all validated in the same revision and same execution window
    - does not imply anything beyond that exact executed window
- Disallowed wording:
  - `full-lane green`
  - `all regressions are closed`
  - `full regression is closed`
  - `project-wide green` without the bounded evidence set above
- Direct-play hard-adoption evidence:
  - keep `Cycle 1` and `Cycle 2` notes inside the same checkpoint window
  - the checkpoint window is capped at `7` calendar days
  - `Counter Summary` tracks both `launcher bypass recorded as supported workflow` and `fallback-request issue` counts
- Lane A recovery ledger:
  - the same-revision full XML is the live oracle
  - the row ledger keeps `status`, `classification date`, `source artifact`, `first wrong oracle`, `owner lane`, `current owner`, `required evidence`, `next action`, and `last reviewed at` at minimum
  - handoff acceptance must not be recorded without current same-revision evidence

## 22. Bounded-lane close governance / bounded-lane close governance
### 한국어
- owner:
  - wording table owner: architecture/governance doc owner
  - close template owner: lane feature owner, architecture/governance reviewer co-sign
  - doc tests owner: lane feature owner, cross-lane vocabulary review는 governance owner
  - CI assertion owner: tools/CI maintainer
- false positive / wording drift procedure:
  - 먼저 executed artifact와 close note를 대조해 tool false positive인지 실제 wording drift인지 분리한다.
  - wording drift면 wording table과 doc tests를 같은 change에서 갱신한다.
  - tool false positive면 CI/assertion rule만 좁혀 수정한다.
  - 기능 backlog를 숨기기 위해 claim 수준을 낮추거나 wording rule을 삭제하지 않는다.
- truth-source priority:
  - `Gameplay-Test-Automation-Guide.md`
  - `Bounded-Lane-Close-Template.md`
  - `Post-Stage-Content-Bounded-Lane-Operations.md`
  - actual close note / example
- 모든 bounded lane close note는 최소한 아래 섹션을 가져야 한다.
  - scope
  - executed commands
  - artifact list with exact dates
  - result summary
  - allowed claims
  - explicit non-claims
  - open functional backlog / handoff
  - open risks
- governance lane close note는 아래 섹션을 추가로 가져야 한다.
  - reviewed truth sources
  - drift triage summary
  - claim vocabulary audit
  - template alignment result
- artifact pairing rule:
  - 같은 claim은 same revision, same execution window, same lane artifact만 조합한다.
  - 서로 다른 날짜 artifact는 historical comparison 용도로만 쓴다.
  - `2026-04-22 core/ui`와 `2026-04-21 full`을 하나의 broad recovery claim 근거로 합치면 안 된다.
- governance guard:
  - wording/doc/CI green만으로 functional lane closure를 주장하지 않는다.
  - close note에는 반드시 `open functional backlog / handoff`를 남긴다.
  - `governance hygiene green alone does not close Lane B, Lane A, or any functional lane`
- claim vocabulary audit artifact:
  - searched paths
  - disallowed phrases
  - match count
  - allowed phrase spot-check
  - revision
  - date/time

### English Original
- Owner:
  - wording table owner: architecture/governance doc owner
  - close template owner: lane feature owner, with architecture/governance reviewer co-sign
  - doc tests owner: lane feature owner, with governance owner reviewing cross-lane vocabulary
  - CI assertion owner: tools/CI maintainer
- False positive / wording drift procedure:
  - first compare the executed artifacts and the close note to split tool false positives from real wording drift
  - if it is wording drift, update the wording table and doc tests in the same change
  - if it is a tool false positive, narrow only the CI/assertion rule
  - do not lower claim strength or delete wording rules to hide a functional backlog
- Truth-source priority:
  - `Gameplay-Test-Automation-Guide.md`
  - `Bounded-Lane-Close-Template.md`
  - `Post-Stage-Content-Bounded-Lane-Operations.md`
  - actual close note / example
- Every bounded-lane close note must contain at least:
  - scope
  - executed commands
  - artifact list with exact dates
  - result summary
  - allowed claims
  - explicit non-claims
  - open functional backlog / handoff
  - open risks
- Governance-lane close notes additionally require:
  - reviewed truth sources
  - drift triage summary
  - claim vocabulary audit
  - template alignment result
- Artifact pairing rule:
  - one claim may combine only the same revision, same execution window, and same lane artifacts
  - artifacts from different dates are comparison-only
  - `2026-04-22 core/ui` plus `2026-04-21 full` must not be collapsed into one broad recovery claim
- Governance guard:
  - wording/doc/CI green alone is not enough to claim functional lane closure
  - every close note must keep an `open functional backlog / handoff` section
  - `governance hygiene green alone does not close Lane B, Lane A, or any functional lane`
- Claim-vocabulary audit artifact:
  - searched paths
  - disallowed phrases
  - match count
  - allowed phrase spot-check
  - revision
  - date/time

> Final principle: This guide is the operational source of truth for gameplay test execution and structure.
>
> 최종 원칙: 이 가이드는 게임플레이 테스트 실행과 구조에 대한 운영상의 단일 기준 문서다.

### Participant reset Editor session validation

Use `UNITY_EDITMODE_ASYNC=1 UNITY_GRAPHICS=1 ./run_tests.sh full --filter ParticipantEditorSessionTests` for the participant reset `EnterPlayMode`/`ExitPlayMode` lifecycle test. The opt-in switches the EditMode runner to asynchronous execution; the default synchronous lanes exclude yielding Editor tests. Check the XML includes the named test rather than treating zero selected tests as evidence. This test disables Domain Reload, uses fake Steam native callbacks and an in-memory production-cache sentinel, and restores Editor play settings afterwards.

### CPU/Tick-primary performance admission

`GAMEPLAY_PERFORMANCE_ADMISSION_POLICY=cpu-tick-v1 ./run_tests.sh gameplay-performance` selects the explicit CPU/Tick-primary policy. Default `strict-v1` retains exact GPU coverage requirements. CPU-primary records incomplete GPU coverage separately without relaxing CPU/Tick, identity or malformed-data checks. See [CPU/Tick admission policy](./Gameplay-CPU-Tick-Admission-Policy.md) for campaign and evidence rules.
