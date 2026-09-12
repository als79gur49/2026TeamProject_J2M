# PR #204 통합 범위와 검증 기록

## 식별 정보

- PR: `#204`, base `main`, head `codex/main-conflict-resolution-20260912`
- 통합 commit: `cbb2cafd935b3268c2a1fb76f2495b1087349550`
- 통합 commit 부모: head `3c71601b0cd812cee7c752319fb59eadd9ed667a`, main `80a203573f2c760b2b3b1ed23bcd4734d64487a8`
- 검증한 staged source tree: `aae96b7b9bacf3a0f4658f23e52239182835ec92`
- 최종 자동 검증은 위 tree에서 실행했다. merge commit은 그 tree를 그대로 기록했으며, 이후 문서 commit들은 검증 결과와 PR 설명만 보강했다.
- 최신 원격 main `4c4d88c455dc8a0505b3aaf490fe425085fea5b5`에 BGM lifecycle 11개 파일이 추가되어 현재 head에 재통합했다. 최신 통합 revision과 재검증 결과는 아래 `최신 main 재통합 검증`에 별도로 기록한다.

## 실제 PR 범위

PR은 main 대비 123개 파일을 포함한다. 기존 head에 누적된 32개 commit과 main 통합 해결, 검증 문서 보강을 함께 검토해야 한다. 아래 변경군은 commit 역사가 아니라 `git diff main...head` 최종 net diff를 기준으로 한다.

- Enemy animation Cue binding, View replacement와 Prefab migration
- TickResult/presentation ownership 및 EntityLogic prefilter
- Cleanup Slice 3 진단과 evidence Python pipeline
- gameplay performance probe/campaign과 pause buffer 재사용
- primitive presentation fallback 제거 및 presentation driver cache
- Destination Iris 테스트와 UI performance probe
- TestRunner timeout, KBO/S3 병합 보존 및 관련 계약/테스트

이 문서는 위 변경군의 전체 기능 리뷰나 성능 개선 완료를 주장하지 않는다. PR은 Draft로 유지하며 사람 리뷰를 별도로 받아야 한다. 현재 저장소에는 `.github/workflows`가 없고 PR status context·check run도 0개이다. GitHub combined status의 `pending`은 실행 중인 CI가 아니라 등록된 context가 없는 상태이며, 자동 check를 필수로 하려면 Unity runner를 포함한 CI 기반을 별도로 설계해야 한다.

## 커밋 역사 규칙 예외

기존 head에는 2026-08-27에 생성된 `perf:` 2개와 `revert:` 3개 commit이 있다. 2026-03-21부터 적용된 `AI_GIT_COMMIT_RULES.md`와 현재 `Tools/git-hooks/commit-msg`의 허용 type에는 두 type이 없으므로 규칙 예외가 맞다. 해당 commit은 이미 push된 기존 역사이며 교정은 브랜치 재작성과 force push를 필요로 한다. 이 PR은 그 역사를 조용히 재작성하지 않고 Ready 전 repository owner의 예외/처리 판단 항목으로 남긴다.

## 최신 main 재통합 검증

- 최신 main: `4c4d88c455dc8a0505b3aaf490fe425085fea5b5`
- 추가 범위: BGM request router/source, gameplay scene installer, BGM core/unit/PlayMode tests, BGM architecture 문서 11개 파일
- 직접 텍스트 충돌: 없음
- 재검증 staged tree: `413e5ad7f28b87863333960732d0217d2a3fb8c7` (결과 기록 문서 갱신 전)
- BGM/Gameplay filtered full: EditMode 141/141 통과, PlayMode 9 total = 8 통과 + 1 실패. `PersistentBgmFlowPlayModeTests.ActualUIAudioDifferentProfileTransition_PreservesAuthoredFadeOutIn`이 실패했다.
- 실패 case는 통합 tree의 격리 재실행과 최신 main 전용 worktree `/mnt/d/J2M/worktrees/latest-main-bgm-baseline-20260912`의 동일 재실행에서 같은 case·같은 `Expected: same as <World_0-1 (UnityEngine.AudioClip)>` 메시지로 재현됐다. 최신 main baseline으로 분류하며 통합 회귀로 계산하지 않는다.
- core: EditMode 282/282 통과, PlayMode 111 total = 107 통과 + graphics 4 skip, 실패 0.
- evidence: `latest-main-bgm-filtered-01`, `latest-main-bgm-isolated-01`, `latest-main-bgm-baseline-isolated-01`, `latest-main-core-01`.
- 위 실행은 새 C#/asset 통합 tree에서 완료했다. 이 결과를 기록하는 문서 diff는 runtime 및 asset을 변경하지 않는다.

## 최종 자동 검증

모든 명령은 `/mnt/d/J2M/worktrees/main-conflict-20260912`의 `./run_tests.sh`로 실행했다. 로컬 원본 log/XML은 `/mnt/d/J2M/evidence/main-conflict-20260912` 아래에 있으며 저장소에는 대용량 결과를 복사하지 않았다.

| lane | 결과 | 로컬 결과 디렉터리 |
|---|---|---|
| focused full | EditMode 51/51, PlayMode 12/12 통과 | `terminal-death-unsupported-fix-filtered-01/results` |
| core | EditMode 273/273 통과, PlayMode 107 통과·graphics 4 skip | `terminal-death-unsupported-fix-core-01/results` |
| ui | 1356/1356 통과 | `pr-open-final-ui-01/results` |
| broad full | EditMode 8871 total = 8823 통과·31 실패·17 skip | `pr-open-final-broad-02/results` |

최종 broad의 31개 실패는 이전 통합 broad 및 부모 main baseline과 test fullname과 failure message가 모두 동일하다. 새 touched 영역 실패는 확인되지 않았다. EditMode가 red여서 broad PlayMode는 runner 규칙에 따라 실행되지 않았다.

첫 PR 직전 broad 실행 `pr-open-final-broad-01`은 기본 watchdog 285초에서 exit 124로 종료됐다. source mutation은 없었다. 문서화된 선택적 제한 `UNITY_TEST_TIMEOUT_SECONDS=900`으로 별도 경로 `pr-open-final-broad-02`에서 재실행해 위 결과를 확보했다.

focused XML에서 supported/unsupported Death replacement, inactive fatal Hit, pause 해제 직후 suppressed Death와 동시 State cue를 포함한 신규 case가 실제 실행되어 통과했다. 실제 DrSaturn Prefab PlayMode도 실행됐다.

## 증거 한계

- 최종 실행 디렉터리에는 별도 `execution.json`이나 commit manifest가 없다. tree 동일성은 실행 직전 staged tree, 올바른 WSL/Windows project path, 실행 전후 `GitDiffEmpty=YES`, merge commit tree의 일치로 확인했다.
- broad PlayMode, 별도 Player build, 수동 화면 캡처와 사람의 전체 PR 리뷰는 완료하지 않았다.
- GitHub에서 로컬 XML을 직접 열 수 없으므로 위 수치와 provenance를 tracked 요약으로 제공한다. CI artifact가 생성되면 그 결과를 우선한다.
