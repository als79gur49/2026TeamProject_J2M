# UI/Audio M1 Continuation — 3-PR 실행 프롬프트

> Active execution input. 이 문서는
> [3-PR 분리 보정 계획](./UI-Audio-M1-Continuation-Three-PR-Remediation-Plan.md)을 실제 작업 세션에서
> 수행하기 위한 프롬프트다. 이 문서를 작성하거나 검토하는 행위 자체는 branch 생성, commit, push, PR 생성
> 또는 merge 승인이 아니다. 아래 본문을 새 작업 세션에 전달했을 때만 명시된 실행 권한이 생긴다.

아래 본문을 새 작업 세션에 그대로 전달할 수 있다.

## 실행 프롬프트

UI/Audio M1 continuation의 17개 선형 커밋을 A, B, C 세 PR로 분리하고, 각 PR의 strict governance
신규 오류를 0개로 보정한 뒤 `main` 대상 draft PR을 순서대로 준비하라.

한 번에 세 PR을 병렬로 만들지 않는다. A가 `main`에 merge된 사실을 검증한 뒤 B를 시작하고, B가 merge된
사실을 검증한 뒤 C를 시작한다. 각 단계에서 PR을 ready-to-merge 상태까지 만들고 보고한 뒤 멈춘다.
사용자가 agent에게 해당 PR merge를 명시적으로 지시하면 8장의 just-in-time gate 후 merge할 수 있고, 사용자가
직접 merge했다면 그 완료 사실을 검증한다. 어느 경우든 실제 `MERGED` 상태 전에는 다음 단계로 진행하지 않는다.

### 최종 목표

다음 endpoint SHA를 원형 그대로 최종 `main` ancestry에 남긴다.

1. PR A: `3bf1adb62e4f98d3e54c8f764744a990038acdf3`
2. PR B: `d7f0164bb74108d5ca50aacd9c1b50feb25d4d42`
3. PR C: `0649e82ad77b1649880ebf237266adf93ca4b81d`

각 PR은 직전 `main` 대비 strict 신규 오류 0개, 같은 revision의 functional/manual evidence, required CI,
actionable unresolved review 0개와 mergeability 확인이 있어야 ready-to-merge로 판정한다. 기존 broad baseline
실패를 touched-cluster 실패와 섞지 말고 `project-wide green`, `full regression closed`, `all regressions fixed`,
`full lane green`을 근거 없이 주장하지 않는다.

### 실행 권한과 금지 범위

이 프롬프트는 다음 작업을 허용한다.

- read-only repository/GitHub 조사와 `git fetch`
- 명시된 원 endpoint를 보호하는 local ref, D 드라이브 Git bundle 및 SHA manifest 생성
- `j2m-worktree-add`를 통한 D 드라이브 전용 PR worktree와 branch 생성
- 계획서가 정한 endpoint merge, test/governance/docs 보정 및 base drift 시 최신 main의 non-rebase merge
- 의도별 local commit, non-force push, draft PR 생성과 PR 설명/ready 상태 갱신
- 해당 PR HEAD의 CI, review, mergeability 및 validation evidence 확인

다음 작업은 추가 사용자 승인 없이는 금지한다.

- PR merge 또는 merge queue 진입
- rebase, cherry-pick, squash, force push와 이미 공유된 commit 재작성
- review thread resolve/dismiss, review dismissal 또는 branch protection 변경
- remote backup branch 생성
- branch/worktree 삭제 또는 원 작업 worktree 정리
- 계획 범위를 벗어난 production, Prefab, ScriptableObject, Scene 또는 asset 수정
- TestCase discovery parser 후속 문제의 구현

예상하지 못한 변경이나 충돌이 생기면 임의로 범위를 넓히지 말고 해당 단계에서 멈춰 증거와 함께 보고한다.

### 먼저 읽을 문서와 skill

작업 시작 전에 다음을 완전히 읽는다.

1. 현재 적용되는 `AGENTS.md`
2. `Docs/Architecture/README.md`
3. `Docs/Architecture/UI-Audio-M1-Continuation-Three-PR-Remediation-Plan.md`
4. `Docs/Testing/Gameplay-Test-Automation-Guide.md`
5. `AI_GIT_COMMIT_RULES.md`
6. `.agents/skills/pr-workflow/SKILL.md`
7. `.agents/skills/commit-push-workflow/SKILL.md`
8. `.agents/skills/gameplay-contract-hardening/SKILL.md`

보정 계획이 execution scope와 수치의 기준이다. source, Git graph 또는 최신 `main`이 계획과 다르면 계획의
수치를 억지로 맞추지 말고 drift를 재산출한다.

## 1. 공통 preflight

### 1.1 현재 상태와 사용자 변경 보호

원 작업 worktree에서 다음을 실행한다.

```bash
set -euo pipefail
git status --short --branch
git diff --stat
git diff
git branch --show-current
git rev-parse --show-toplevel
git remote -v
```

현재 legacy C-drive worktree의 아래 변경은 이 작업의 승인된 docs 입력이며 사용자 변경으로 취급한다.

- `Docs/Architecture/README.md`
- `Docs/Architecture/UI-Audio-M1-Continuation-Three-PR-Remediation-Plan.md`
- `Docs/Architecture/UI-Audio-M1-Continuation-Three-PR-Execution-Prompt.md`

이 파일을 원 worktree에서 revert, reset, clean, move 또는 삭제하지 않는다. 그 밖의 변경이 있으면 소유권과
중첩 여부를 먼저 분류하고, 겹치면 작업을 멈춰 보고한다. 원 worktree에서 branch switch나 merge를 하지 않는다.

### 1.2 저장소와 merge 방법 확인

먼저 GitHub 접근 수단을 확인한다.

```bash
set -euo pipefail
if command -v gh >/dev/null 2>&1; then
    gh auth status
else
    printf 'gh unavailable; authenticated GitHub connector required\n'
fi
```

`gh`가 없거나 인증되지 않았지만 현재 세션에 인증된 GitHub connector가 있다면, 이후 모든 `gh` 조회·PR·CI·
review·merge 명령을 그 connector의 동등한 작업으로 일관되게 대체하고 사용한 interface를 evidence에 기록한다.
`gh`와 connector를 한 단계 안에서 혼용하지 않는다. 둘 다 사용할 수 없으면 도구를 임의로 설치하거나
branch/worktree를 만들지 말고 preflight blocker로 보고한다.

```bash
set -euo pipefail
REPOSITORY=$(gh repo view --json nameWithOwner --jq '.nameWithOwner')
test "$REPOSITORY" = "als79gur49/2026TeamProject_J2M"
ORIGIN_URL=$(git remote get-url origin)
case "$ORIGIN_URL" in
    git@github.com:als79gur49/2026TeamProject_J2M.git|https://github.com/als79gur49/2026TeamProject_J2M.git) ;;
    *) exit 1 ;;
esac
test "$(gh repo view "$REPOSITORY" --json defaultBranchRef --jq '.defaultBranchRef.name')" = "main"
test "$(gh api "repos/$REPOSITORY" --jq '.allow_merge_commit')" = "true"
MERGE_QUEUE_RULE_COUNT=$(gh api --paginate "repos/$REPOSITORY/rules/branches/main?per_page=100" --jq '[.[] | select(.type == "merge_queue")] | length' | awk '{sum += $1} END {print sum + 0}')
test "$MERGE_QUEUE_RULE_COUNT" -eq 0
git fetch origin main
BASELINE_MAIN_SHA=6a271d1b3058192de1eb4ca4e0c9027327203e9e
ORIGINAL_BASE_SHA=5d338c54a890bb5225ddda9d769f880846b8f1ca
A_ENDPOINT_SHA=3bf1adb62e4f98d3e54c8f764744a990038acdf3
B_ENDPOINT_SHA=d7f0164bb74108d5ca50aacd9c1b50feb25d4d42
C_ENDPOINT_SHA=0649e82ad77b1649880ebf237266adf93ca4b81d
git cat-file -e "${BASELINE_MAIN_SHA}^{commit}"
git cat-file -e "${A_ENDPOINT_SHA}^{commit}"
git cat-file -e "${B_ENDPOINT_SHA}^{commit}"
git cat-file -e "${C_ENDPOINT_SHA}^{commit}"
test "$(git merge-base "$BASELINE_MAIN_SHA" "$A_ENDPOINT_SHA")" = "$ORIGINAL_BASE_SHA"
git merge-base --is-ancestor "$ORIGINAL_BASE_SHA" "$A_ENDPOINT_SHA"
git merge-base --is-ancestor "$A_ENDPOINT_SHA" "$B_ENDPOINT_SHA"
git merge-base --is-ancestor "$B_ENDPOINT_SHA" "$C_ENDPOINT_SHA"
git rev-parse origin/main
```

다음을 확인한다.

- repository가 의도한 J2M repository임
- base branch가 `main`임
- GitHub의 `Create a merge commit`이 허용됨
- `main`에 적용되는 ruleset, classic branch protection과 PR merge menu가 merge queue를 요구하지 않음
- 세 endpoint commit object가 모두 로컬에 존재함
- endpoint 순서가 A -> B -> C의 선형 ancestry임

merge commit이 비활성화되어 있거나 repository/base identity가 다르거나 적용 ruleset/classic protection을
완전히 확인할 수 없거나 merge queue가 필수이면 branch를 만들지 말고 멈춘다. connector 또는 GitHub 설정
화면으로 확인한 classic protection/merge menu 결과도 preflight evidence에 남긴다. squash/rebase 또는 queue로
대체하지 않는다.

### 1.3 main drift 재검증

계획 기준 main은 `6a271d1b3058192de1eb4ca4e0c9027327203e9e`다. 실행 시점의 `origin/main`이 이 SHA와
다르면 세 endpoint를 최신 main과 각각 virtual merge하고 다음을 다시 계산한다.

- text/semantic conflict
- A/B/C 원 file count와 상호 overlap
- main, A, A+B, A+B+C의 정규화된 strict 오류 집합
- 단계별 신규/해결 오류
- 최신 main에서 추가로 겹치는 production/test/docs 파일

기존 계획의 remediation만으로 각 단계의 head-only strict 오류를 0개로 만들 수 있다는 증거가 없거나,
CameraShake의 `sourceTickIndex`, completed-motion cleanup, README link 등 의미 충돌이 생기면 작업을 멈춘다.
수치가 달라도 remediation 범위와 계약이 유지되면 갱신된 기준을 preflight evidence에 기록하고 계속한다.

### 1.4 원 endpoint와 docs 보호

다음 원 HEAD를 local backup ref로 고정한다.

```bash
set -euo pipefail
ORIGINAL_HEAD=0649e82ad77b1649880ebf237266adf93ca4b81d
BACKUP_REF=refs/backup/ui-audio-m1-continuation-original
if EXISTING_BACKUP=$(git rev-parse --verify --quiet "$BACKUP_REF"); then
    test "$EXISTING_BACKUP" = "$ORIGINAL_HEAD"
else
    git update-ref "$BACKUP_REF" "$ORIGINAL_HEAD"
fi
git show-ref refs/backup/ui-audio-m1-continuation-original
```

기존 backup ref가 다른 SHA를 가리키면 덮어쓰지 않고 중단한다. ref가 확인된 뒤 다음 형태로 timestamped D
evidence를 만든다.

```bash
set -euo pipefail
BACKUP_REF=refs/backup/ui-audio-m1-continuation-original
ORIGINAL_HEAD=0649e82ad77b1649880ebf237266adf93ca4b81d
A_ENDPOINT_SHA=3bf1adb62e4f98d3e54c8f764744a990038acdf3
B_ENDPOINT_SHA=d7f0164bb74108d5ca50aacd9c1b50feb25d4d42
C_ENDPOINT_SHA=0649e82ad77b1649880ebf237266adf93ca4b81d
BACKUP_ROOT="/mnt/d/J2M/evidence/ui-audio-m1-continuation/preflight/$(date -u +%Y%m%dT%H%M%SZ)"
test ! -e "$BACKUP_ROOT"
mkdir -p "$BACKUP_ROOT/docs"
git bundle create "$BACKUP_ROOT/original-endpoint.bundle" "$BACKUP_REF"
git bundle verify "$BACKUP_ROOT/original-endpoint.bundle" >"$BACKUP_ROOT/bundle-verify.log" 2>&1
test "$(git bundle list-heads "$BACKUP_ROOT/original-endpoint.bundle" "$BACKUP_REF" | awk '{print $1}')" = "$ORIGINAL_HEAD"
sha256sum "$BACKUP_ROOT/original-endpoint.bundle" >"$BACKUP_ROOT/original-endpoint.bundle.sha256"
cp Docs/Architecture/README.md "$BACKUP_ROOT/docs/README.md"
cp Docs/Architecture/UI-Audio-M1-Continuation-Three-PR-Remediation-Plan.md "$BACKUP_ROOT/docs/"
cp Docs/Architecture/UI-Audio-M1-Continuation-Three-PR-Execution-Prompt.md "$BACKUP_ROOT/docs/"
sha256sum "$BACKUP_ROOT"/docs/* >"$BACKUP_ROOT/docs.sha256"
git status --porcelain=v2 --branch >"$BACKUP_ROOT/git-status.txt"
git diff --stat >"$BACKUP_ROOT/git-diff-stat.txt"
git diff >"$BACKUP_ROOT/git-diff.patch"
printf 'origin_main=%s\na_endpoint=%s\nb_endpoint=%s\nc_endpoint=%s\noriginal_head=%s\noriginal_tree=%s\n' \
    "$(git rev-parse origin/main)" "$A_ENDPOINT_SHA" "$B_ENDPOINT_SHA" "$C_ENDPOINT_SHA" "$ORIGINAL_HEAD" \
    "$(git rev-parse "${ORIGINAL_HEAD}^{tree}")" >"$BACKUP_ROOT/sha-manifest.txt"
```

`/mnt/d/J2M/evidence/ui-audio-m1-continuation/preflight/<timestamp>/` 아래에 다음이 보존되어야 한다.

- 위 local ref를 포함한 Git bundle과 `git bundle verify` 결과
- 원 HEAD, tree SHA, 세 endpoint SHA와 당시 `origin/main` SHA manifest
- bundle SHA-256
- 세 docs 입력 파일의 byte copy와 SHA-256
- preflight status/diff 출력

remote backup branch는 만들지 않는다.

### 1.5 storage gate

```bash
set -euo pipefail
j2m-worktree-audit
df -BG /mnt/c /mnt/d
```

- audit가 PASS가 아니면 중단한다.
- D 여유 공간이 30 GiB 미만이면 새 worktree를 만들지 않는다.
- C 여유 공간이 10 GiB 미만이면 경고하고 Unity 검증 전에 사용자에게 보고한다.
- 새 worktree는 `/mnt/d/J2M/worktrees/` 아래에만 만들고 `j2m-worktree-add`만 사용한다.
- Unity 검증 전에 `realpath`로 project path가 `/mnt/d/J2M/worktrees/`로 시작하는지 다시 확인한다.
- worktree별 `Library`는 공유하지 않는다.

## 2. 공통 PR 작업 규칙

### 2.1 한 단계씩 새 worktree 생성

앞 PR이 merge된 뒤 최신 main을 fetch하고 다음 형태로 새 branch/worktree를 만든다.

먼저 선택한 exact `BRANCH_NAME`에 대해 local branch, registered worktree, remote branch와 기존 open/closed PR이
모두 없는지 확인한다. 하나라도 있으면 생성·재사용·삭제하지 말고 identity와 상태를 보고한다.

```bash
set -euo pipefail
REPOSITORY=$(gh repo view --json nameWithOwner --jq '.nameWithOwner')
test "$REPOSITORY" = "als79gur49/2026TeamProject_J2M"
ORIGIN_URL=$(git remote get-url origin)
case "$ORIGIN_URL" in
    git@github.com:als79gur49/2026TeamProject_J2M.git|https://github.com/als79gur49/2026TeamProject_J2M.git) ;;
    *) exit 1 ;;
esac
git fetch origin main
j2m-worktree-audit
D_FREE_GIB=$(df -BG --output=avail /mnt/d | tail -n 1 | tr -dc '0-9')
test "$D_FREE_GIB" -ge 30
test -z "$(git branch --list "<branch-name>")"
test -z "$(git ls-remote --heads origin "refs/heads/<branch-name>")"
test "$(gh pr list --repo "$REPOSITORY" --state all --head "<branch-name>" --json number --jq 'length')" -eq 0
j2m-worktree-add <worktree-name> --new <branch-name> origin/main
j2m-worktree-audit
```

권장 이름은 다음과 같다.

| PR | worktree name | branch |
|---|---|---|
| A | `ui-audio-m1-pr-a` | `worktree/ui-audio-m1-pr-a` |
| B | `ui-audio-m1-pr-b` | `worktree/ui-audio-m1-pr-b` |
| C | `ui-audio-m1-pr-c` | `worktree/ui-audio-m1-pr-c` |

같은 이름의 branch 또는 worktree가 이미 있으면 재사용하거나 삭제하지 말고 상태를 조사해 보고한다.

새 worktree에서 다음을 고정한다.

```bash
set -euo pipefail
PR_BASE_SHA=$(git rev-parse origin/main)
test "$(git rev-parse HEAD)" = "$PR_BASE_SHA"
git rev-parse --show-toplevel
WORKTREE_PATH=$(realpath "$(git rev-parse --show-toplevel)")
case "$WORKTREE_PATH" in /mnt/d/J2M/worktrees/*) ;; *) exit 1 ;; esac
git status --short --branch
git diff --stat
```

### 2.2 exec 사이 PR 상태 보존

Codex의 별도 shell/exec 호출 사이에 shell 변수가 유지된다고 가정하지 않는다. PR별 stable path에
`state.env`를 만들고 다음 값을 안전하게 single-quote한 assignment로 기록한다.

- `REPOSITORY`와 `ORIGIN_URL`
- `PR_ID`와 생성 후의 `PR_NUMBER`
- `PR_BASE_SHA`, 현재 `PR_HEAD_SHA`, `PR_HEAD_TREE`
- `FULL_ENDPOINT_SHA`
- `BRANCH_NAME`, resolved `WORKTREE_PATH`
- 현재 head에 대응하는 `PR_EVIDENCE_ROOT`
- `PR_TITLE`, `PR_BODY_FILE`, `PR_BODY_SHA256`
- `PR_MERGE_TITLE`, `PR_MERGE_BODY_FILE`, `PR_MERGE_BODY_SHA256`

state path는 다음처럼 PR별로 고정한다.

```text
/mnt/d/J2M/evidence/ui-audio-m1-continuation/pr-a/state.env
/mnt/d/J2M/evidence/ui-audio-m1-continuation/pr-b/state.env
/mnt/d/J2M/evidence/ui-audio-m1-continuation/pr-c/state.env
```

PR과 GitHub merge commit 제목은 다음처럼 고정한다.

| PR | `PR_TITLE` | `PR_MERGE_TITLE` |
|---|---|---|
| A | `feat: Gameplay/EnemyAI - PassiveContactMinion ownership 분리` | `feat: Gameplay/EnemyAI - PassiveContactMinion ownership PR 통합` |
| B | `feat: Gameplay/EnemyView - production presentation 분리` | `feat: Gameplay/EnemyView - production presentation PR 통합` |
| C | `feat: Gameplay/EnemyAnimation - sparse binding migration 분리` | `feat: Gameplay/EnemyAnimation - sparse binding migration PR 통합` |

새 worktree의 첫 state 초기화는 2.1 shell 변수가 남아 있다고 가정하지 않고 별도 단일 exec에서 전부 다시
계산한다. 먼저 `PR_BODY_FILE`에는 7장의 필수 section을 가진 template을, `PR_MERGE_BODY_FILE`에는 해당 PR의
변경 이유/결과를 설명하는 두 bullet 이상을 만든다. 그 뒤 다음 block의 `PR_ID` 하나만 선택한 literal로 지정한다.

```bash
set -euo pipefail
PR_ID='pr-a' # 이 단계에 맞게 pr-a, pr-b, pr-c 중 정확히 하나
REPOSITORY='als79gur49/2026TeamProject_J2M'
ORIGIN_URL=$(git remote get-url origin)
case "$ORIGIN_URL" in
    git@github.com:als79gur49/2026TeamProject_J2M.git|https://github.com/als79gur49/2026TeamProject_J2M.git) ;;
    *) exit 1 ;;
esac
case "$PR_ID" in
    pr-a)
        BRANCH_NAME='worktree/ui-audio-m1-pr-a'
        FULL_ENDPOINT_SHA='3bf1adb62e4f98d3e54c8f764744a990038acdf3'
        PR_TITLE='feat: Gameplay/EnemyAI - PassiveContactMinion ownership 분리'
        PR_MERGE_TITLE='feat: Gameplay/EnemyAI - PassiveContactMinion ownership PR 통합'
        ;;
    pr-b)
        BRANCH_NAME='worktree/ui-audio-m1-pr-b'
        FULL_ENDPOINT_SHA='d7f0164bb74108d5ca50aacd9c1b50feb25d4d42'
        PR_TITLE='feat: Gameplay/EnemyView - production presentation 분리'
        PR_MERGE_TITLE='feat: Gameplay/EnemyView - production presentation PR 통합'
        ;;
    pr-c)
        BRANCH_NAME='worktree/ui-audio-m1-pr-c'
        FULL_ENDPOINT_SHA='0649e82ad77b1649880ebf237266adf93ca4b81d'
        PR_TITLE='feat: Gameplay/EnemyAnimation - sparse binding migration 분리'
        PR_MERGE_TITLE='feat: Gameplay/EnemyAnimation - sparse binding migration PR 통합'
        ;;
    *) exit 1 ;;
esac
test "$(git branch --show-current)" = "$BRANCH_NAME"
WORKTREE_PATH=$(realpath "$(git rev-parse --show-toplevel)")
case "$WORKTREE_PATH" in /mnt/d/J2M/worktrees/*) ;; *) exit 1 ;; esac
PR_BASE_SHA=$(git rev-parse origin/main)
PR_HEAD_SHA=$(git rev-parse HEAD)
test "$PR_HEAD_SHA" = "$PR_BASE_SHA"
PR_HEAD_TREE=$(git rev-parse 'HEAD^{tree}')
PR_NUMBER=''
STATE_DIR="/mnt/d/J2M/evidence/ui-audio-m1-continuation/$PR_ID"
STATE_FILE="$STATE_DIR/state.env"
PR_EVIDENCE_ROOT="$STATE_DIR/$PR_HEAD_SHA"
PR_BODY_FILE="$STATE_DIR/pr-body.md"
PR_MERGE_BODY_FILE="$STATE_DIR/merge-body.md"
test -s "$PR_BODY_FILE" && test -s "$PR_MERGE_BODY_FILE"
PR_BODY_SHA256=$(sha256sum "$PR_BODY_FILE" | awk '{print $1}')
PR_MERGE_BODY_SHA256=$(sha256sum "$PR_MERGE_BODY_FILE" | awk '{print $1}')
test ! -e "$STATE_FILE"
mkdir -p "$STATE_DIR" "$PR_EVIDENCE_ROOT"
STATE_TMP=$(mktemp "$STATE_DIR/.state.env.XXXXXX")
export REPOSITORY ORIGIN_URL PR_ID PR_NUMBER PR_BASE_SHA PR_HEAD_SHA PR_HEAD_TREE
export FULL_ENDPOINT_SHA BRANCH_NAME WORKTREE_PATH PR_EVIDENCE_ROOT PR_TITLE PR_BODY_FILE PR_BODY_SHA256
export PR_MERGE_TITLE PR_MERGE_BODY_FILE PR_MERGE_BODY_SHA256
python3 - "$STATE_TMP" <<'PY'
import os, sys

names = (
    "REPOSITORY", "ORIGIN_URL", "PR_ID", "PR_NUMBER", "PR_BASE_SHA", "PR_HEAD_SHA",
    "PR_HEAD_TREE", "FULL_ENDPOINT_SHA", "BRANCH_NAME", "WORKTREE_PATH", "PR_EVIDENCE_ROOT",
    "PR_TITLE", "PR_BODY_FILE", "PR_BODY_SHA256", "PR_MERGE_TITLE", "PR_MERGE_BODY_FILE",
    "PR_MERGE_BODY_SHA256",
)
def shell_quote(value):
    return "'" + value.replace("'", "'\"'\"'") + "'"
with open(sys.argv[1], "w", encoding="utf-8") as stream:
    for name in names:
        stream.write(f"{name}={shell_quote(os.environ[name])}\n")
PY
mv "$STATE_TMP" "$STATE_FILE"
source "$STATE_FILE"
test "$(git rev-parse HEAD)" = "$PR_HEAD_SHA"
test "$(git rev-parse 'HEAD^{tree}')" = "$PR_HEAD_TREE"
test "$(realpath "$(git rev-parse --show-toplevel)")" = "$WORKTREE_PATH"
```

`PR_MERGE_BODY_FILE`에는 해당 PR의 변경 이유와 결과를 설명하는 최소 두 bullet을 고정한다. A는 mutable
authoring graph 소유권과 runtime compile policy 보존, B는 prefab/controller/VFX presentation 계약과 A 이후
순차 통합, C는 sparse cue migration/retirement와 B 이후 22-file overlap 해소를 각각 기록한다. 단순 파일 목록은
허용하지 않는다. `PR_BODY_FILE`은 7장의 전체 PR evidence 설명을 담는다.

각 후속 shell 호출은 `set -euo pipefail` 뒤 해당 exact state file만 `source`하고, 필수 변수가 non-empty인지
확인한다. 그 다음 repository identity, current branch, resolved worktree path, current HEAD/tree가 state와 정확히
일치하는지 assert한 뒤에만 작업한다. PR 작업 전 공통 preamble의 의미는 다음과 같다.

```bash
set -euo pipefail
STATE_FILE="/mnt/d/J2M/evidence/ui-audio-m1-continuation/<exact-pr-id>/state.env"
test -f "$STATE_FILE"
source "$STATE_FILE"
: "${REPOSITORY:?}" "${ORIGIN_URL:?}" "${PR_ID:?}" "${PR_BASE_SHA:?}" "${PR_HEAD_SHA:?}"
: "${PR_HEAD_TREE:?}" "${FULL_ENDPOINT_SHA:?}" "${BRANCH_NAME:?}" "${WORKTREE_PATH:?}"
: "${PR_EVIDENCE_ROOT:?}" "${PR_TITLE:?}" "${PR_BODY_FILE:?}"
: "${PR_BODY_SHA256:?}" "${PR_MERGE_TITLE:?}" "${PR_MERGE_BODY_FILE:?}" "${PR_MERGE_BODY_SHA256:?}"
test -s "$PR_BODY_FILE" && test -s "$PR_MERGE_BODY_FILE"
test "$(sha256sum "$PR_BODY_FILE" | awk '{print $1}')" = "$PR_BODY_SHA256"
test "$(sha256sum "$PR_MERGE_BODY_FILE" | awk '{print $1}')" = "$PR_MERGE_BODY_SHA256"
test "$(git remote get-url origin)" = "$ORIGIN_URL"
test "$(git branch --show-current)" = "$BRANCH_NAME"
test "$(realpath "$(git rev-parse --show-toplevel)")" = "$WORKTREE_PATH"
case "$WORKTREE_PATH" in /mnt/d/J2M/worktrees/*) ;; *) exit 1 ;; esac
test "$(git rev-parse HEAD)" = "$PR_HEAD_SHA"
test "$(git rev-parse 'HEAD^{tree}')" = "$PR_HEAD_TREE"
git merge-base --is-ancestor "$PR_BASE_SHA" "$PR_HEAD_SHA"
test "$PR_EVIDENCE_ROOT" = "/mnt/d/J2M/evidence/ui-audio-m1-continuation/$PR_ID/$PR_HEAD_SHA"
if command -v gh >/dev/null 2>&1; then
    test "$(gh repo view "$REPOSITORY" --json nameWithOwner --jq '.nameWithOwner')" = "$REPOSITORY"
fi
```

`<exact-pr-id>`는 해당 단계의 `pr-a`, `pr-b`, `pr-c` 중 하나로 바꾼 literal path여야 한다. 이후 code block은
가독성을 위해 이 preamble을 반복 표기하지 않지만, 별도 exec로 실행할 때마다 반드시 먼저 적용한다.

commit, base drift, PR 생성 등으로 값이 바뀌면 같은 filesystem의 temporary file을 사용해 `state.env`를
원자적으로 교체하고 즉시 다시 source/assert한다. `PR_TITLE`/body 두 파일과 해시는 첫 endpoint 작업 전에
생성·기록한다. PR 생성 전에는 `PR_NUMBER=''`만 허용하고, 생성 직후 exact number를 원자 갱신한다.

같은 final head의 phase provenance는 다음 immutable artifact로 남긴다.

- final validation 완료 직후 `context-validation.env`
- PR 생성과 `PR_NUMBER` 반영 직후 `context-pr-created.env`
- ready 전환 및 remote/CI/review 확인 시도별 `pr-ready/<attempt-id>/pr-ready.json`
- merge 직전 JIT gate 시도별 `pre-merge/<approval-id>-<attempt-id>/context-pre-merge.env`
- merge API 시도별 `merge-result/<approval-id>-<attempt-id>/merge-result.json`

validation/PR-created는 head당 한 번만 만들고, CI/review/JIT 재확인은 새 UTC timestamp와 process/random suffix를
결합한 attempt ID를 사용한다. 기존 attempt directory는 덮어쓰지 않는다. 각 phase마다
`phase-index/<UTC>-<phase>-<attempt-id>.sha256`이라는 새 index record를 추가하고, 그 record에는 artifact 상대
경로와 SHA-256을 기록한다. index filename collision이면 중단한다. 이 디렉터리가 append-only phase index다.

각 파일은 먼저 같은 evidence filesystem의 temporary file에 쓰고, 대상이 없음을 확인한 뒤 rename하며, 바로
옆 `<name>.sha256`을 만든다. env snapshot은 그 시점의 `state.env` byte copy다. phase record의 JSON/context에는 최소
`repository`, `prNumber`, `baseSha`, `headSha`, `headTree`, `branch`, `worktreePath`, `evidenceRoot`를 넣는다.
merge 결과에는 `mergeMethod=merge`, exact merge title/body SHA-256, API의 `merged`와 merge SHA도 넣는다.
모든 phase에서 repository/base/head/tree/branch/worktree/evidence root가 `context-validation.env`와 같은지
assert한다. PR 생성 전이라 validation snapshot의 PR number가 비어 있는 차이만 허용한다. snapshot은 context
provenance이며 test pass 자체가 아니다. head 또는 base가 바뀌면 새 `PR_EVIDENCE_ROOT`에서 validation부터
다시 수행하며 이전 phase artifact를 수정하거나 새 revision에 재사용하지 않는다.

### 2.3 endpoint merge

해당 단계의 full endpoint SHA를 다음처럼 통합한다.

```bash
set -euo pipefail
git merge --no-ff --no-commit "$FULL_ENDPOINT_SHA"
git diff --cached --stat
git diff --cached --name-status
git diff --cached --check -- '*.cs' '*.py' '*.json' '*.md' '*.asmdef' '*.sh'
```

staged file set이 계획의 원 range와 일치하는지 확인하고 Scene, Prefab, ScriptableObject, asset 및 `.meta`의
목적을 분류한다. 예상 밖 파일, delete/rename, conflict 또는 semantic drift가 있으면 commit하지 말고 멈춘다.

endpoint merge commit도 `AI_GIT_COMMIT_RULES.md`의 제목과 최소 두 개 body bullet을 만족시킨다. commit 후에는
다음을 확인한다.

```bash
set -euo pipefail
git merge-base --is-ancestor "$FULL_ENDPOINT_SHA" HEAD
git show -s --format='%H %P' HEAD
git log -1 --stat
git status --short --branch
```

pre-commit hook을 우회하지 않는다. hook 또는 `core`가 실패하면 unrelated baseline인지 touched regression인지
분리하고, touched failure를 해결하기 전에는 push하지 않는다.

### 2.4 변경·staging·commit 규칙

- 보정 계획에 적힌 exact file과 method만 수정한다.
- unrelated baseline을 함께 정리하지 않는다.
- production/content endpoint 변경과 remediation을 한 commit으로 합치지 않는다.
- 명시적 path로 stage하고 매 commit 전에 `git diff --staged`를 전부 검토한다.
- Scene은 별도 commit으로 유지하고 asset과 Unity-generated `.meta`를 짝지어 다룬다.
- Prefab/ScriptableObject 변경은 원 endpoint에 포함된 목적을 commit/PR 설명에 적는다.
- 기존 user change를 revert하거나 예상 밖 Unity import mutation을 정상 변경으로 흡수하지 않는다.

모든 commit 직후 `git show -s --format='%H%n%P%n%B' HEAD`, `git log -1 --stat`과 status를 확인하고 state의
HEAD/tree를 원자 갱신한다. final first-parent 순서는 A가 endpoint merge -> remediation -> docs, B가 endpoint
merge -> remediation, C가 endpoint merge -> category -> governance다. base drift sync merge가 필요하면 그 뒤의
별도 commit이어야 한다. 각 제목과 본문 최소 두 bullet을 사람이 diff와 함께 확인한다.

## 3. PR A 실행

### 3.1 endpoint와 commit

- endpoint: `3bf1adb62e4f98d3e54c8f764744a990038acdf3`
- 원 범위: 2 commits, 17 files

endpoint merge commit 제목:

```text
feat: Gameplay/EnemyAI - PassiveContactMinion ownership endpoint 통합
```

본문에는 profile별 mutable authoring graph 소유권 도입 이유, 원 두 commit의 SHA ancestry 보존 및 runtime
compile 정책 비변경을 최소 두 bullet로 기록한다.

### 3.2 A remediation commit

`EnemyAiProfileAssetContractTests.PassiveContactMinion_Profile_OwnsDistinctMutableAuthoringGraph`의 primary
category만 `Core`에서 `Extended`로 바꾼다. production asset 값, compiled tuning 또는 unrelated baseline은
수정하지 않는다.

commit은 계획서 3.4의 다음 제목과 본문을 사용한다.

```text
test: Gameplay/EnemyAI - asset contract category governance 정합화
```

### 3.3 A docs commit

원 worktree에서 보존한 현재 내용을 사용해 다음 세 파일을 A worktree에 반영한다.

- `Docs/Architecture/UI-Audio-M1-Continuation-Three-PR-Remediation-Plan.md`
- `Docs/Architecture/UI-Audio-M1-Continuation-Three-PR-Execution-Prompt.md`
- `Docs/Architecture/README.md`의 두 문서 index 항목

README 전체를 원 worktree 버전으로 덮어쓰지 말고 최신 main에 index hunk만 적용한다. 두 신규 문서는
preflight SHA manifest와 byte 단위로 대조하고, README는 최신 main을 보존한 상태에서 두 index 항목과 설명이
동일한지 대조한다.

commit 제목:

```text
docs: Gameplay/Presentation - 3-PR remediation 실행 계약 기록
```

본문에는 분리 이유, SHA 보존/strict delta/evidence gate 및 문서가 실행 승인과 runtime truth를 대체하지 않는다는
경계를 최소 두 bullet로 기록한다.

### 3.4 A 검증

먼저 final commit 뒤의 SHA로 evidence 변수를 초기화하고 6장의 strict/base-range gate를 통과한 다음 실행한다.

```bash
set -euo pipefail
PR_HEAD_SHA=$(git rev-parse HEAD)
PR_HEAD_TREE=$(git rev-parse 'HEAD^{tree}')
export PR_EVIDENCE_ROOT="/mnt/d/J2M/evidence/ui-audio-m1-continuation/pr-a/$PR_HEAD_SHA"
STRATIFICATION_GOVERNANCE_MODE=soft CODEX_VALIDATION_ROOT="$PR_EVIDENCE_ROOT/core" ./run_tests.sh core
STRATIFICATION_GOVERNANCE_MODE=soft CODEX_VALIDATION_ROOT="$PR_EVIDENCE_ROOT/full-enemy-ai-profile" ./run_tests.sh full --filter EnemyAiProfileAssetContractTests
```

ScriptableObject별 owner path, exclusive mutable graph, asset/`.meta` pairing, GUID 보존 및 compiled tuning parity를
같은 HEAD에서 확인한다.

## 4. PR B 실행

PR A가 merge되었고 A final head와 endpoint가 새 `origin/main`의 ancestor임을 확인한 뒤에만 시작한다.

### 4.1 endpoint와 commit

- endpoint: `d7f0164bb74108d5ca50aacd9c1b50feb25d4d42`
- 원 범위: 3 commits, 52 files

endpoint merge commit 제목:

```text
feat: Gameplay/EnemyView - production presentation endpoint 통합
```

본문에는 production prefab/controller/VFX presentation 계약의 목적, A 이후 순차 통합 이유와 원 SHA ancestry
보존을 기록한다.

### 4.2 B remediation commit

보정 계획 4장의 exact 수정만 수행한다.

- `EnemyViewAnimatorControllerContractTests`의 fixture-level `Full`을 제거하고 7개 method에 직접 명시
- `EnemyViewAnimatorControllerContractTests.cs`를 `FULL_FILE_NAMES`에 추가
- architecture/coordinator/VFX 신규 method 10개를 `Extended`로 변경
- resolver 신규 method 1개를 `Full`로 변경
- repository residue scan FQN 하나만 reason을 가진 locked `Full` override로 등록
- 이미 `Full`인 `EnemyPrefab_SparseBindingAuthorsDeathWithoutTiming` source는 불필요하게 수정하지 않음

commit은 계획서 4.4의 다음 제목과 본문을 사용한다.

```text
test: Gameplay/EnemyView - presentation test tier governance 정합화
```

production C#, Prefab, Controller, clip 또는 ScriptableObject를 remediation commit에서 변경하지 않는다.

### 4.3 B 검증

먼저 final commit 뒤의 SHA로 evidence 변수를 초기화하고 6장의 strict/base-range gate를 통과한 다음 실행한다.

```bash
set -euo pipefail
PR_HEAD_SHA=$(git rev-parse HEAD)
PR_HEAD_TREE=$(git rev-parse 'HEAD^{tree}')
export PR_EVIDENCE_ROOT="/mnt/d/J2M/evidence/ui-audio-m1-continuation/pr-b/$PR_HEAD_SHA"
STRATIFICATION_GOVERNANCE_MODE=soft CODEX_VALIDATION_ROOT="$PR_EVIDENCE_ROOT/core" ./run_tests.sh core
STRATIFICATION_GOVERNANCE_MODE=soft CODEX_VALIDATION_ROOT="$PR_EVIDENCE_ROOT/full-presentation-contracts-primary" ./run_tests.sh full --filter EnemyViewAnimatorControllerContractTests,EnemyViewPresentationAuthoringTests,GameplayPresentationOrchestrationArchitectureTests,GameplayTickPresentationCoordinatorTests,GameplayVfxParameterizedMotionRuntimeTests,SummonedEnemyPresentationResolverTests
STRATIFICATION_GOVERNANCE_MODE=soft CODEX_VALIDATION_ROOT="$PR_EVIDENCE_ROOT/full-presentation-contracts-secondary" ./run_tests.sh full --filter EnemyAudioRuntimeTests,FlipInteractionPlannerInternalTests,GameplayTimingOwnershipTests,GameplayVfxEnemyDeathMotionPrefabWithSourceCloneTests,GameplayVfxPlayerDamageMigrationTests,GameplayViewProjectionTests,PresentationPoseArbitrationTests
STRATIFICATION_GOVERNANCE_MODE=soft CODEX_VALIDATION_ROOT="$PR_EVIDENCE_ROOT/full-animator-playmode" ./run_tests.sh full --filter EnemyViewAnimatorRuntimeCharacterizationPlayModeTests
```

production prefab/controller contract, actual Animator PlayMode, latest main의 CameraShake `sourceTickIndex`와
completed-motion cleanup 보존을 확인하고 Prefab 목적과 manual/editor evidence를 기록한다.

## 5. PR C 실행

PR B가 merge되었고 A/B final head와 두 endpoint가 새 `origin/main`의 ancestor임을 확인한 뒤에만 시작한다.

### 5.1 endpoint와 commit

- endpoint: `0649e82ad77b1649880ebf237266adf93ca4b81d`
- 원 범위: 12 commits, 85 files

endpoint merge commit 제목:

```text
feat: Gameplay/EnemyAnimation - sparse binding migration endpoint 통합
```

본문에는 sparse cue binding/migration/retirement 범위, B와 22개 파일이 겹쳐 순차 통합한 이유 및 원 12개
commit ancestry 보존을 기록한다.

### 5.2 C category commit

보정 계획 5.1의 exact 8개 파일에서 fixture-level primary category 10개를 제거하고 총 85개 test method에
category를 직접 둔다. `EnemyViewAnimatorControllerContractTests.cs`에서 A+B 대비 신규 1개와 rename 1개로
생긴 새 FQN 2개에도 `Full`을 명시해 전체 대상이 87개가 되게 한다. 보정 후 Controller fixture의 최종 8개
method가 모두 method-level `Full`인지 확인한다.

- Core: 13
- Scenario: 34
- Infrastructure: 38
- Controller: 2

현재 checker가 발견하지 못하는 계획서의 8개 `TestCase`/`TestCaseSource` 전용 method도 빠짐없이 명시한다.
TestCase discovery parser 자체는 수정하지 않는다.

commit은 계획서 5.5의 다음 제목과 본문을 사용한다.

```text
test: Gameplay/EnemyAnimation - sparse binding 테스트 실행 티어 명시
```

### 5.3 C governance commit

보정 계획 5.2~5.4를 그대로 구현한다.

- canonical Full인 C 여섯 파일을 `FULL_FILE_NAMES`에 추가
- exact Core path 두 개와 exact Host import만 허용하는 shared helper 추가
- `is_core_candidate`와 `check_core_assembly_rules`가 같은 helper를 사용하게 함
- Infrastructure allowlist에 exact `Game.Feature.Gameplay.EnemyPresentation.Editor` assembly 하나만 추가
- 임의 Editor/runtime Host reference는 계속 거부
- exact EditorTools import-path guard를 구현하거나, 구현하지 않으면 assembly 전체 reference라는 잔여 구조
  리스크를 PR C에 명시
- `Tools/test_gameplay_test_stratification_lib.py`에 계획서가 정한 boundary regression test 추가

boundary test는 다음으로 실행 가능해야 한다.

```bash
set -euo pipefail
python3 Tools/test_gameplay_test_stratification_lib.py
```

commit은 계획서 5.5의 다음 제목과 본문을 사용한다.

```text
chore: Project - animation test governance 경계 정합화
```

### 5.4 C 검증

먼저 final commit 뒤의 SHA로 evidence 변수를 초기화하고 6장의 strict/base-range gate를 통과한 다음 실행한다.

```bash
set -euo pipefail
PR_HEAD_SHA=$(git rev-parse HEAD)
PR_HEAD_TREE=$(git rev-parse 'HEAD^{tree}')
export PR_EVIDENCE_ROOT="/mnt/d/J2M/evidence/ui-audio-m1-continuation/pr-c/$PR_HEAD_SHA"
python3 Tools/test_gameplay_test_stratification_lib.py
STRATIFICATION_GOVERNANCE_MODE=soft CODEX_VALIDATION_ROOT="$PR_EVIDENCE_ROOT/core" ./run_tests.sh core
STRATIFICATION_GOVERNANCE_MODE=soft CODEX_VALIDATION_ROOT="$PR_EVIDENCE_ROOT/full-binding-infrastructure" ./run_tests.sh full --filter EnemyAnimationBindingSnapshotTests,EnemyAnimationCueCatalogTests,EnemyAnimationBindingAuthoringTests,EnemyAnimationBindingEditorValidationTests,EnemyAnimationBindingMigrationManifestTests,EnemyAnimationSparseBindingProductionContractTests,EnemyAnimationBindingMigrationAssetCharacterizationTests,EnemyAnimationSparseBindingAssetCharacterizationTests
STRATIFICATION_GOVERNANCE_MODE=soft CODEX_VALIDATION_ROOT="$PR_EVIDENCE_ROOT/full-binding-runtime" ./run_tests.sh full --filter EnemyAnimationBindingDispatchTests,EnemyAnimationSparseBindingRuntimeScenarioTests,GameplayTimingOwnershipTests,EnemyViewAnimatorControllerContractTests
STRATIFICATION_GOVERNANCE_MODE=soft CODEX_VALIDATION_ROOT="$PR_EVIDENCE_ROOT/full-inherited-touched" ./run_tests.sh full --filter EnemyPrefabScaffoldTests,EnemyViewPresentationAuthoringTests,GameplayTickPresentationCoordinatorTests,GameplayViewProjectionTests
STRATIFICATION_GOVERNANCE_MODE=soft CODEX_VALIDATION_ROOT="$PR_EVIDENCE_ROOT/full-animation-playmode" ./run_tests.sh full --filter EnemyViewAnimatorRuntimeCharacterizationPlayModeTests,EnemyPresentationReadinessPlayModeTests
```

production 10 View의 sparse binding/controller/clip identity, Missing Script 0, retired YAML/GUID residue를 확인한다.
같은 final HEAD에서 Kali, Startis, Astreton, DrSaturn, Nebulous exact 5-View Inspector matrix와 PNG를 생성하고,
capture 전후 tracked diff 0과 HEAD/tree/fingerprint manifest를 기록한다. Inspector 검증 중 Prefab, Controller,
clip을 저장하지 않는다.

## 6. 공통 strict delta와 evidence gate

각 PR final HEAD에서 `PR_BASE_SHA` 기준 committed 범위를 검사한다.

```bash
set -euo pipefail
mkdir -p "$PR_EVIDENCE_ROOT/governance"
git diff --stat "$PR_BASE_SHA"...HEAD
git diff --name-status "$PR_BASE_SHA"...HEAD
git diff --check "$PR_BASE_SHA"...HEAD -- '*.cs' '*.py' '*.json' '*.md' '*.asmdef' '*.sh'
git log --no-merges --oneline "$PR_BASE_SHA"..HEAD
git log --first-parent --format='%H%x09%P%n%B%n---' "$PR_BASE_SHA"..HEAD >"$PR_EVIDENCE_ROOT/governance/first-parent-commits.txt"
```

Unity serialized YAML은 이 whitespace hard gate에서 제외하고 YAML residue, GUID/fileID, reimport 및 manual/editor
검증으로 판정한다.

strict와 inventory는 `TestResults/.governance`가 없는 fresh base/head archive에서 실행한다. 계획서 7.1의
archive 명령을 그대로 사용해 각 command stdout/stderr와 exit code를 보존한다.

판정 기준:

- 동적 `Core candidate debt ... streak` 행을 제외한 정규화 집합에서 head-only 오류 0
- candidate count 비증가
- raw strict/generate nonzero는 기존 baseline과 분리
- 기존 오류의 우연한 삭제/rename을 신규 오류와 상계하지 않음

functional lane은 위 strict delta가 통과한 뒤에만 `STRATIFICATION_GOVERNANCE_MODE=soft`로 실행한다.
`run_tests.sh`는 lane별 고정 결과 파일명을 사용하므로 PR root 자체를 반복 사용하지 않는다. 위 PR별 명령에
지정된 서로 다른 core/filtered 하위 `CODEX_VALIDATION_ROOT`를 유지해 XML/log가 덮어써지지 않게 한다.

Unity lane 전에 각 D worktree에서 다음 path preflight를 실행하고 `PROJECT_PATH_WSL`/`PROJECT_PATH_WIN`이
현재 resolved worktree와 일치하는지 기록한다. 이 결과를 test pass로 보고하지 않는다.

```bash
set -euo pipefail
./run_tests.sh --print-config
./run_tests.sh --dry-run core
```

PR별 검증 절에서 초기화한 `PR_HEAD_SHA`, `PR_HEAD_TREE`, `PR_EVIDENCE_ROOT`가 현재 HEAD와 일치하는지
확인한 뒤 functional lane을 실행한다. base drift나 추가 commit으로 HEAD가 바뀌면 세 값을 새 final head로
다시 초기화하고 새 evidence root에서 전체 gate를 반복한다.

실행 전후 HEAD, tree와 clean status를 기록한다. tracked 변경이 발생하면 원인을 조사하고, 예상 밖 mutation을
되돌리기 위해 destructive command를 사용하지 않는다. 안전하게 원상복구할 수 없으면 해당 evidence를 무효로
표시하고 멈춘다. 서로 다른 revision이나 과거 Slice evidence를 하나의 통과 주장으로 합치지 않는다.

UI source/asset 변경이 없으면 `./run_tests.sh ui`는 실행하지 않고 이유를 PR에 기록한다. broad unfiltered
`full`을 실행하지 않았거나 baseline red로 중단되면 그 사실과 touched-cluster 결과를 분리해 적는다.

## 7. push와 draft PR

모든 intended commit과 local 검증이 끝난 뒤 다음을 확인한다.

```bash
set -euo pipefail
git status --short --branch
git branch --show-current
git log --oneline --decorate "$PR_BASE_SHA"..HEAD
git diff --stat "$PR_BASE_SHA"...HEAD
test -z "$(git status --porcelain --untracked-files=all)"
```

다음처럼 upstream/remote identity를 fail-closed로 확인하고 non-force push한다. 최초 push도 exact remote ref를
사용하며 다른 이름의 기존 branch를 upstream으로 채택하지 않는다.

```bash
set -euo pipefail
if UPSTREAM=$(git rev-parse --abbrev-ref '@{upstream}' 2>/dev/null); then
    test "$UPSTREAM" = "origin/$BRANCH_NAME"
    git rev-list --left-right --count '@{upstream}...HEAD'
    git push origin "HEAD:refs/heads/$BRANCH_NAME"
else
    test -z "$(git ls-remote --heads origin "refs/heads/$BRANCH_NAME")"
    git push --set-upstream origin "HEAD:refs/heads/$BRANCH_NAME"
fi
REMOTE_HEAD_SHA=$(git ls-remote --heads origin "refs/heads/$BRANCH_NAME" | awk '{print $1}')
test "$REMOTE_HEAD_SHA" = "$PR_HEAD_SHA"
PR_URL=$(gh pr create --repo "$REPOSITORY" --base main --head "$BRANCH_NAME" --draft --title "$PR_TITLE" --body-file "$PR_BODY_FILE")
test -n "$PR_URL"
PR_NUMBER=$(gh pr list --repo "$REPOSITORY" --state open --head "$BRANCH_NAME" --json number --jq 'if length == 1 then .[0].number else error("expected exactly one PR") end')
test -n "$PR_NUMBER"
test "$(gh api "repos/$REPOSITORY/pulls/$PR_NUMBER" --jq '.base.ref')" = "main"
test "$(gh api "repos/$REPOSITORY/pulls/$PR_NUMBER" --jq '.base.sha')" = "$PR_BASE_SHA"
test "$(gh api "repos/$REPOSITORY/pulls/$PR_NUMBER" --jq '.head.ref')" = "$BRANCH_NAME"
test "$(gh api "repos/$REPOSITORY/pulls/$PR_NUMBER" --jq '.head.sha')" = "$PR_HEAD_SHA"
mkdir -p "$PR_EVIDENCE_ROOT/remote"
git diff --name-status "$PR_BASE_SHA" "$PR_HEAD_SHA" >"$PR_EVIDENCE_ROOT/remote/local-base-head-name-status.txt"
gh api "repos/$REPOSITORY/compare/$PR_BASE_SHA...$PR_HEAD_SHA" \
    --jq '{status, ahead_by, behind_by, base_sha: .base_commit.sha, head_sha: .commits[-1].sha}' \
    >"$PR_EVIDENCE_ROOT/remote/github-compare.json"
test "$(gh api "repos/$REPOSITORY/compare/$PR_BASE_SHA...$PR_HEAD_SHA" --jq '.base_commit.sha')" = "$PR_BASE_SHA"
test "$(gh api "repos/$REPOSITORY/compare/$PR_BASE_SHA...$PR_HEAD_SHA" --jq '.commits[-1].sha')" = "$PR_HEAD_SHA"
sha256sum "$PR_EVIDENCE_ROOT/remote/local-base-head-name-status.txt" \
    "$PR_EVIDENCE_ROOT/remote/github-compare.json" >"$PR_EVIDENCE_ROOT/remote/manifest.sha256"
```

PR 생성 직후 `PR_NUMBER`를 state에 원자 반영하고 다시 source/assert한다. force push하지 않는다.

PR 설명에는 반드시 다음을 포함한다.

- repository, PR number, base/head branch, draft/ready 상태
- final head SHA와 tree SHA
- 원 commit range/endpoint와 추가 remediation/docs commit
- production, Prefab, ScriptableObject, asset 변경 목적
- strict base/head 오류 집합과 head-only 0 증거
- 실행한 command, exit, XML/log/evidence 경로
- 실행하지 않은 `ui`, broad `full`, manual lane과 이유
- 기존 baseline과 touched-cluster 결과의 분리
- merge commit 방식 요구, endpoint ancestry 및 rollback 단위
- CI, actionable unresolved review, mergeability/conflict 상태
- 알려진 bounded deviation과 후속 TestCase parser 문제

PR 생성 후 위 exact remote base/head 및 compare manifest가 local base-range와 같은 commit pair인지 확인한다.
GitHub PR files API도 모든 page를 조회해 예상 file count/name-status와 대조하고 mismatch면 중단한다. local
evidence와 PR 설명이 완성되고 remote head/base가 일치하면 draft를 ready-for-review로 전환한다. 그 뒤 required
CI가 final head SHA에서 끝날 때까지
확인하고 실패 job/log를 분석한다. actionable review가 남으면 임의로 thread를 resolve하지 말고 수정·보류·결정
필요 항목으로 분류한다.

```bash
set -euo pipefail
if test "$(gh pr view --repo "$REPOSITORY" "$PR_NUMBER" --json isDraft --jq '.isDraft')" = "true"; then
    gh pr ready --repo "$REPOSITORY" "$PR_NUMBER"
fi
gh pr checks --repo "$REPOSITORY" "$PR_NUMBER" --required --watch
test "$(gh pr view --repo "$REPOSITORY" "$PR_NUMBER" --json baseRefName --jq '.baseRefName')" = "main"
test "$(gh pr view --repo "$REPOSITORY" "$PR_NUMBER" --json headRefName --jq '.headRefName')" = "$BRANCH_NAME"
test "$(gh pr view --repo "$REPOSITORY" "$PR_NUMBER" --json headRefOid --jq '.headRefOid')" = "$PR_HEAD_SHA"
test "$(gh pr view --repo "$REPOSITORY" "$PR_NUMBER" --json state --jq '.state')" = "OPEN"
test "$(gh pr view --repo "$REPOSITORY" "$PR_NUMBER" --json isDraft --jq '.isDraft')" = "false"
test "$(gh pr view --repo "$REPOSITORY" "$PR_NUMBER" --json mergeable --jq '.mergeable')" = "MERGEABLE"
MERGE_STATE=$(gh pr view --repo "$REPOSITORY" "$PR_NUMBER" --json mergeStateStatus --jq '.mergeStateStatus')
case "$MERGE_STATE" in CLEAN|HAS_HOOKS|UNSTABLE) ;; *) exit 1 ;; esac
READY_ATTEMPT_ID="$(date -u +%Y%m%dT%H%M%SZ)-$$-$RANDOM"
READY_DIR="$PR_EVIDENCE_ROOT/pr-ready/$READY_ATTEMPT_ID"
test ! -e "$READY_DIR"
mkdir -p "$READY_DIR"
READY_TMP=$(mktemp "$READY_DIR/.pr-ready.json.XXXXXX")
gh pr view --repo "$REPOSITORY" "$PR_NUMBER" --json baseRefName,headRefName,headRefOid,isDraft,state,mergeable,mergeStateStatus,reviewDecision >"$READY_TMP"
mv "$READY_TMP" "$READY_DIR/pr-ready.json"
cp "$STATE_FILE" "$READY_DIR/context.env"
sha256sum "$READY_DIR/pr-ready.json" "$READY_DIR/context.env" >"$READY_DIR/artifacts.sha256"
mkdir -p "$PR_EVIDENCE_ROOT/phase-index"
READY_INDEX="$PR_EVIDENCE_ROOT/phase-index/${READY_ATTEMPT_ID}-pr-ready.sha256"
test ! -e "$READY_INDEX"
(cd "$PR_EVIDENCE_ROOT" && sha256sum "pr-ready/$READY_ATTEMPT_ID/artifacts.sha256") >"$READY_INDEX"
```

review thread는 GraphQL 또는 connector로 모든 page를 조회해 final context에 저장하고, unresolved thread 중
actionable 항목이 0인지 별도로 판정한다. `reviewDecision`이 `CHANGES_REQUESTED` 또는 `REVIEW_REQUIRED`이면
ready-to-merge로 보고하지 않는다. optional check 실패로 `UNSTABLE`인 경우 required check 결과와 분리해
원인과 merge 영향 여부를 명시한다.

ready 전과 사용자에게 merge 승인을 요청하기 직전에 `origin/main`을 다시 fetch한다. 새 main이 검증에 사용한
`PR_BASE_SHA`와 다르면 rebase하지 않고 다음 절차를 수행한다.

1. 최신 main을 PR branch에 `--no-ff --no-commit`으로 merge한다.
2. staged diff, parent 의도와 conflict/semantic drift를 검토한다.
3. conflict나 예상 밖 scope가 없을 때만 body가 있는 `chore: Project - PR base 최신 main 동기화` commit으로
   완료한다.
4. `PR_BASE_SHA`를 방금 통합한 main SHA로 갱신한다.
5. strict delta, functional/manual evidence, push, remote diff와 required CI를 새 final head에서 모두 다시 수행한다.

base drift merge가 conflict를 만들거나 기존 remediation 판단을 바꾸면 commit하지 말고 멈춘다. 검증 뒤 main이
다시 움직이면 같은 절차를 반복하며, 이전 revision evidence를 새 head에 귀속하지 않는다.

모든 조건이 충족되면 PR을 ready-to-merge로 판정한다. 그 뒤에는 merge하지 말고 사용자에게 다음을 보고한다.

- PR URL과 identity
- final head/tree
- commit 목록과 endpoint ancestor 확인
- CI/review/mergeability
- strict delta와 functional/manual evidence
- skipped lane과 잔여 리스크
- 다음 PR 진행을 위해 필요한 merge 승인

## 8. 단계 사이 merge와 완료 확인

이 프롬프트만으로는 merge 권한이 없다. ready 보고 후 사용자가 agent에게 해당 PR merge를 명시적으로
지시하면 다음 just-in-time gate를 다시 확인한다.

- GitHub head SHA가 검증한 `PR_HEAD_SHA`와 동일함
- required CI가 그 head에서 성공함
- actionable unresolved review가 0임
- PR이 ready이고 mergeable하며 base가 `main`임
- `origin/main`이 검증 base에서 움직이지 않았음

하나라도 달라졌으면 merge하지 않는다. base drift는 7장의 main 동기화와 전체 재검증을 다시 수행한다. 모두
유지될 때만 GitHub `Create a merge commit` 방식으로 merge하고, squash/rebase/branch 삭제는 사용하지 않는다.
사용자가 직접 merge했다고 알린 경우에는 merge 명령을 실행하지 않고 아래 완료 검사만 수행한다.

agent에게 merge가 명시적으로 승인되고 just-in-time gate가 모두 유지될 때의 명령은 다음 하나다. 승인 직후
해당 user turn/message reference와 UTC 시각을 `MERGE_APPROVAL_ID`, `MERGE_APPROVAL_REF`,
`MERGE_APPROVAL_AT_UTC`, `MERGE_APPROVAL_TEXT_SHA256`, `MERGE_APPROVAL_REPOSITORY`,
`MERGE_APPROVAL_PR_NUMBER`, `MERGE_APPROVAL_HEAD_SHA`로 `state.env`에 원자 기록한다. platform turn/message ID가
있으면 path-safe slug를 approval ID로 사용한다. 없으면 exact 승인문 SHA-256을 deterministic ID로 쓰고
`MERGE_APPROVAL_REF=approval-sha256:<hash>`로 기록한다. 승인 원문에 민감정보가 있을 수 있으므로 원문은
evidence에 복사하지 않고 hash와 platform reference만 남긴다.
모두 non-empty여야 하고 현재 repository/PR number/head와 같아야 한다. A 승인은 B/C에, 한 head 승인은 drift 후
새 head에 재사용하지 않는다.

바로 직전에 review thread 모든 page를 다시 조회·저장하고 unresolved thread를 분류해
`JIT_ACTIONABLE_UNRESOLVED_COUNT`와 `JIT_REVIEW_HEAD_SHA`도 state에 원자 기록하고 다시 source/assert한다. 이 값과
review evidence의 head SHA가 현재 `PR_HEAD_SHA`에 귀속되지 않으면 실행하지 않는다. classic protection/PR
merge menu의 queue 비요구 여부도 같은 시점에 다시 확인한다.

```bash
set -euo pipefail
: "${JIT_ACTIONABLE_UNRESOLVED_COUNT:?}"
: "${JIT_REVIEW_HEAD_SHA:?}"
: "${MERGE_APPROVAL_ID:?}" "${MERGE_APPROVAL_REF:?}" "${MERGE_APPROVAL_AT_UTC:?}"
: "${MERGE_APPROVAL_TEXT_SHA256:?}" "${MERGE_APPROVAL_REPOSITORY:?}"
: "${MERGE_APPROVAL_PR_NUMBER:?}" "${MERGE_APPROVAL_HEAD_SHA:?}"
test "$JIT_ACTIONABLE_UNRESOLVED_COUNT" -eq 0
test "$JIT_REVIEW_HEAD_SHA" = "$PR_HEAD_SHA"
test "$MERGE_APPROVAL_REPOSITORY" = "$REPOSITORY"
test "$MERGE_APPROVAL_PR_NUMBER" = "$PR_NUMBER"
test "$MERGE_APPROVAL_HEAD_SHA" = "$PR_HEAD_SHA"
case "$MERGE_APPROVAL_ID" in *[!A-Za-z0-9._-]*|'') exit 1 ;; esac
test "${#MERGE_APPROVAL_TEXT_SHA256}" -eq 64
case "$MERGE_APPROVAL_TEXT_SHA256" in *[!0-9a-f]*) exit 1 ;; esac
git fetch origin main
test "$(git rev-parse origin/main)" = "$PR_BASE_SHA"
test "$(gh api "repos/$REPOSITORY" --jq '.allow_merge_commit')" = "true"
test "$(gh pr view --repo "$REPOSITORY" "$PR_NUMBER" --json state --jq '.state')" = "OPEN"
test "$(gh pr view --repo "$REPOSITORY" "$PR_NUMBER" --json isDraft --jq '.isDraft')" = "false"
test "$(gh pr view --repo "$REPOSITORY" "$PR_NUMBER" --json baseRefName --jq '.baseRefName')" = "main"
test "$(gh pr view --repo "$REPOSITORY" "$PR_NUMBER" --json headRefName --jq '.headRefName')" = "$BRANCH_NAME"
test "$(gh pr view --repo "$REPOSITORY" "$PR_NUMBER" --json headRefOid --jq '.headRefOid')" = "$PR_HEAD_SHA"
gh pr checks --repo "$REPOSITORY" "$PR_NUMBER" --required --watch
test "$(gh pr view --repo "$REPOSITORY" "$PR_NUMBER" --json mergeable --jq '.mergeable')" = "MERGEABLE"
MERGE_STATE=$(gh pr view --repo "$REPOSITORY" "$PR_NUMBER" --json mergeStateStatus --jq '.mergeStateStatus')
case "$MERGE_STATE" in CLEAN|HAS_HOOKS|UNSTABLE) ;; *) exit 1 ;; esac
REVIEW_DECISION=$(gh pr view --repo "$REPOSITORY" "$PR_NUMBER" --json reviewDecision --jq '.reviewDecision')
case "$REVIEW_DECISION" in ''|APPROVED) ;; *) exit 1 ;; esac
MERGE_QUEUE_RULE_COUNT=$(gh api --paginate "repos/$REPOSITORY/rules/branches/main?per_page=100" --jq '[.[] | select(.type == "merge_queue")] | length' | awk '{sum += $1} END {print sum + 0}')
test "$MERGE_QUEUE_RULE_COUNT" -eq 0
MERGE_BODY_BULLETS=$(awk '/^- / { count++ } END { print count + 0 }' "$PR_MERGE_BODY_FILE")
test "$MERGE_BODY_BULLETS" -ge 2
MERGE_ATTEMPT_ID="$(date -u +%Y%m%dT%H%M%SZ)-$$-$RANDOM"
PRE_MERGE_DIR="$PR_EVIDENCE_ROOT/pre-merge/$MERGE_APPROVAL_ID-$MERGE_ATTEMPT_ID"
MERGE_RESULT_DIR="$PR_EVIDENCE_ROOT/merge-result/$MERGE_APPROVAL_ID-$MERGE_ATTEMPT_ID"
if test -d "$PR_EVIDENCE_ROOT/merge-result"; then
    test -z "$(find "$PR_EVIDENCE_ROOT/merge-result" -mindepth 1 -maxdepth 1 -type d \
        -name "$MERGE_APPROVAL_ID-*" -print -quit)"
fi
test ! -e "$PRE_MERGE_DIR" && test ! -e "$MERGE_RESULT_DIR"
mkdir -p "$PRE_MERGE_DIR" "$MERGE_RESULT_DIR" "$PR_EVIDENCE_ROOT/phase-index"
cp "$STATE_FILE" "$PRE_MERGE_DIR/context-pre-merge.env"
sha256sum "$PRE_MERGE_DIR/context-pre-merge.env" >"$PRE_MERGE_DIR/context-pre-merge.env.sha256"
PRE_INDEX="$PR_EVIDENCE_ROOT/phase-index/${MERGE_ATTEMPT_ID}-pre-merge.sha256"
test ! -e "$PRE_INDEX"
(cd "$PR_EVIDENCE_ROOT" && sha256sum "pre-merge/$MERGE_APPROVAL_ID-$MERGE_ATTEMPT_ID/context-pre-merge.env") >"$PRE_INDEX"
API_STDOUT="$MERGE_RESULT_DIR/transport-stdout.json"
API_STDERR="$MERGE_RESULT_DIR/transport-stderr.log"
API_RC_FILE="$MERGE_RESULT_DIR/transport.rc"
RECOVERY_VIEW="$MERGE_RESULT_DIR/recovery-pr-view.json"
set +e
gh api --method PUT "repos/$REPOSITORY/pulls/$PR_NUMBER/merge" \
    --raw-field sha="$PR_HEAD_SHA" \
    --raw-field merge_method=merge \
    --raw-field commit_title="$PR_MERGE_TITLE" \
    --raw-field commit_message="$(<"$PR_MERGE_BODY_FILE")" >"$API_STDOUT" 2>"$API_STDERR"
API_RC=$?
gh pr view --repo "$REPOSITORY" "$PR_NUMBER" \
    --json state,mergeCommit,baseRefName,headRefName,headRefOid >"$RECOVERY_VIEW" 2>>"$API_STDERR"
RECOVERY_RC=$?
set -e
printf 'merge_api_rc=%s\nrecovery_query_rc=%s\n' "$API_RC" "$RECOVERY_RC" >"$API_RC_FILE"
MERGE_RESULT_TMP=$(mktemp "$MERGE_RESULT_DIR/.merge-result.json.XXXXXX")
python3 - "$API_STDOUT" "$RECOVERY_VIEW" "$MERGE_RESULT_TMP" "$API_RC" "$RECOVERY_RC" \
    "$REPOSITORY" "$PR_NUMBER" "$PR_BASE_SHA" \
    "$PR_HEAD_SHA" "$PR_HEAD_TREE" "$BRANCH_NAME" "$WORKTREE_PATH" "$PR_EVIDENCE_ROOT" \
    "$PR_MERGE_TITLE" "$PR_MERGE_BODY_SHA256" "$MERGE_APPROVAL_ID" "$MERGE_APPROVAL_REF" \
    "$MERGE_APPROVAL_AT_UTC" "$MERGE_APPROVAL_TEXT_SHA256" <<'PY'
import json, sys

(api_path, recovery_path, out_path, api_rc, recovery_rc,
 repository, pr_number, base_sha, head_sha, head_tree,
 branch, worktree, evidence_root, title, body_sha, approval_id, approval_ref,
 approval_at, approval_text_sha) = sys.argv[1:]
def read_json(path):
    try:
        with open(path, encoding="utf-8") as stream:
            return json.load(stream)
    except (OSError, json.JSONDecodeError):
        return {}
api_response = read_json(api_path)
recovery_view = read_json(recovery_path)
api_merged = int(api_rc) == 0 and api_response.get("merged") is True and bool(api_response.get("sha"))
recovered = (
    not api_merged and int(recovery_rc) == 0 and recovery_view.get("state") == "MERGED"
    and bool((recovery_view.get("mergeCommit") or {}).get("oid"))
    and recovery_view.get("baseRefName") == "main"
    and recovery_view.get("headRefName") == branch
    and recovery_view.get("headRefOid") == head_sha
)
merge_sha = api_response.get("sha", "") if api_merged else (
    (recovery_view.get("mergeCommit") or {}).get("oid", "") if recovered else ""
)
result = {
    "repository": repository, "prNumber": int(pr_number), "baseSha": base_sha,
    "headSha": head_sha, "headTree": head_tree, "branch": branch,
    "worktreePath": worktree, "evidenceRoot": evidence_root,
    "mergeMethod": "merge", "mergeTitle": title, "mergeBodySha256": body_sha,
    "approvalId": approval_id, "approvalRef": approval_ref, "approvalAtUtc": approval_at,
    "approvalTextSha256": approval_text_sha,
    "transportRc": int(api_rc), "recoveryQueryRc": int(recovery_rc),
    "resultSource": "api" if api_merged else ("post-transport-query" if recovered else "unmerged"),
    "merged": bool(api_merged or recovered), "mergeSha": merge_sha,
    "apiResponse": api_response, "recoveryView": recovery_view,
}
with open(out_path, "w", encoding="utf-8") as stream:
    json.dump(result, stream, ensure_ascii=False, sort_keys=True, indent=2)
    stream.write("\n")
PY
mv "$MERGE_RESULT_TMP" "$MERGE_RESULT_DIR/merge-result.json"
sha256sum "$MERGE_RESULT_DIR/merge-result.json" >"$MERGE_RESULT_DIR/merge-result.json.sha256"
sha256sum "$MERGE_RESULT_DIR/merge-result.json" "$API_STDOUT" "$API_STDERR" \
    "$API_RC_FILE" "$RECOVERY_VIEW" "$MERGE_RESULT_DIR/merge-result.json.sha256" \
    >"$MERGE_RESULT_DIR/artifacts.sha256"
RESULT_INDEX="$PR_EVIDENCE_ROOT/phase-index/${MERGE_ATTEMPT_ID}-merge-result.sha256"
test ! -e "$RESULT_INDEX"
(cd "$PR_EVIDENCE_ROOT" && sha256sum "merge-result/$MERGE_APPROVAL_ID-$MERGE_ATTEMPT_ID/artifacts.sha256") >"$RESULT_INDEX"
read -r MERGED API_MERGE_SHA < <(python3 - "$MERGE_RESULT_DIR/merge-result.json" <<'PY'
import json, sys
with open(sys.argv[1], encoding="utf-8") as stream:
    result = json.load(stream)
print(str(result.get("merged", False)).lower(), result.get("mergeSha", ""))
PY
)
test "$MERGED" = "true"
test -n "$API_MERGE_SHA"
```

merge API 응답을 보존한 직후 `MERGE_ATTEMPT_ID`, absolute `MERGE_RESULT_FILE`, 그 파일의
`MERGE_RESULT_SHA256`, `API_MERGE_SHA`를 `state.env`에 원자 추가하고 다시 source/assert한다. 별도 exec의
완료 검사는 shell 변수 지속에 기대지 않고 이 네 값을 state에서 읽으며, result file과 hash를 먼저 검증한다.

CLI의 auto-merge/merge-queue 전환을 사용하지 않는다. connector 경로도 expected head SHA, merge method
`merge`, exact title/body를 받는 즉시 merge API만 사용한다. merge 호출의 rc/stdout/stderr가 실패·유실·timeout을
나타내더라도 같은 승인으로 merge API를 다시 호출하지 않는다. 위 read-only recovery 조회에서 이미 `MERGED`이고
merge SHA가 확인될 때만 parent/tree 완료 검사로 복구한다. 그렇지 않으면 attempt evidence를 보존하고 fail-closed로
중단한다. connector 경로도 원 merge 호출 결과와 별도 post-call PR 조회를 같은 방식으로 보존한다.

실제 PR state가 `MERGED`가 될 때까지 다음 PR로 진행하지 않는다. 완료 후 실제 PR merge commit이
`origin/main` ancestry에 있음을 확인한다.

```bash
set -euo pipefail
: "${MERGE_ATTEMPT_ID:?}" "${MERGE_RESULT_FILE:?}" "${MERGE_RESULT_SHA256:?}" "${API_MERGE_SHA:?}"
test "$MERGE_RESULT_FILE" = "$PR_EVIDENCE_ROOT/merge-result/$MERGE_APPROVAL_ID-$MERGE_ATTEMPT_ID/merge-result.json"
test "$(sha256sum "$MERGE_RESULT_FILE" | awk '{print $1}')" = "$MERGE_RESULT_SHA256"
git fetch origin main
MAIN_SHA=$(git rev-parse origin/main)
test "$(gh pr view --repo "$REPOSITORY" "$PR_NUMBER" --json state --jq '.state')" = "MERGED"
PR_MERGE_SHA=$(gh pr view --repo "$REPOSITORY" "$PR_NUMBER" --json state,mergedAt,mergeCommit --jq '.mergeCommit.oid')
test -n "$PR_MERGE_SHA"
test "$PR_MERGE_SHA" = "$API_MERGE_SHA"
git merge-base --is-ancestor "$PR_MERGE_SHA" "$MAIN_SHA"
read -r FIRST_PARENT SECOND_PARENT EXTRA_PARENT <<< "$(git show -s --format='%P' "$PR_MERGE_SHA")"
test -n "$FIRST_PARENT" && test -n "$SECOND_PARENT" && test -z "$EXTRA_PARENT"
test "$FIRST_PARENT" = "$PR_BASE_SHA"
test "$SECOND_PARENT" = "$PR_HEAD_SHA"
test "$(git show -s --format='%s' "$PR_MERGE_SHA")" = "$PR_MERGE_TITLE"
MERGE_COMMIT_BODY_BULLETS=$(git show -s --format='%b' "$PR_MERGE_SHA" | awk '/^- / { count++ } END { print count + 0 }')
test "$MERGE_COMMIT_BODY_BULLETS" -ge 2
test "$(git show -s --format='%b' "$PR_MERGE_SHA")" = "$(<"$PR_MERGE_BODY_FILE")"
git merge-base --is-ancestor "$PR_HEAD_SHA" "$PR_MERGE_SHA"
git merge-base --is-ancestor "$FULL_ENDPOINT_SHA" "$PR_MERGE_SHA"
test "$(git rev-parse "${PR_MERGE_SHA}^{tree}")" = "$PR_HEAD_TREE"
```

merge commit의 first parent가 직전 main인지 확인한다. tree가 PR head와 다르면 기존 evidence를 병합 결과에
귀속하지 말고 영향 범위에 비례해 merge tree에서 재검증한다. endpoint ancestor 검사는 A 후 A, B 후 A+B,
C 후 A+B+C 누적으로 수행한다.

## 9. rollback과 중단 조건

전체 stack rollback은 merge commit first parent를 확인한 후 C, B, A 역순이다.

```text
PR C merge revert -> PR B merge revert -> PR A merge revert
```

실제 revert는 별도 사용자 승인이 필요하다. 승인 후에도 legacy worktree나 `main`에서 직접 revert/push하지
않는다. 최신 `origin/main`을 fetch하고 storage/free-space gate를 다시 통과한 뒤 `j2m-worktree-add`로
`/mnt/d/J2M/worktrees/` 아래 새 rollback worktree와 전용 branch를 만든다. 실제 GitHub merge SHA의 두 parent와
first-parent chain을 먼저 검증한다. 승인된 범위가 전체 stack이면 C, B, A의 full merge SHA를 역순으로 각각
`git revert --no-commit -m 1 <full-merge-sha>` 하되, 각 단계의 staged diff/name-status/check를 검토한 다음
전체 rollback을 하나의 명확한 intent로 규칙에 맞는 제목과 최소 두 body bullet을 가진 commit으로 만든다.
동일 revision에서 strict delta, `core`, 관련 touched filter와 필요한 manual evidence를 재수집한 뒤 non-force
push하고 별도 rollback PR을 연다. rollback PR도 이 문서의 remote identity, CI/review, exact-head gate를 따른다.

Slice 4A 부분 rollback은 C governance/category commit을 통째로 먼저 되돌리지 않고 계획서 8장의
`--no-commit` 절차와 rollback ledger를 따른다. 대상 historical commit은 축약하지 않은
`115602d35713c94647c624b107d22e5f0836be2c`, `df82bea239d04300d011c8118e0ba2924f016bd2`를 사용한다.

다음 중 하나가 발생하면 현재 단계에서 멈추고 상태를 보존한 채 보고한다.

- merge commit 방식 사용 불가
- 최신 main drift로 remediation 또는 file scope가 달라짐
- 예상 밖 staged/working-tree 변경이나 user change 중첩
- endpoint merge conflict 또는 semantic conflict
- D storage/worktree path 규칙 위반
- strict head-only 오류가 0이 아님
- touched-cluster test 실패 또는 test infrastructure 오류
- Unity 실행 후 예상 밖 tracked mutation
- required CI 실패, unresolved actionable review 또는 mergeability 불명
- 원 SHA ancestry/tree 보존 실패

중단 보고에는 재현 command, exit code, 관련 SHA/path와 다음 안전한 선택지를 포함한다. 실패를 숨기기 위해
규칙, 테스트, allowlist 또는 evidence 기준을 약화하지 않는다.
