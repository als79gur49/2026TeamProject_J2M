# AI Git Commit Rules

이 문서는 Unity 프로젝트에서 Git CLI로 커밋할 때 사람이든 AI든 동일한 기준으로 커밋 메시지를 만들기 위한 운영 규칙이다.  
커밋 전에 이 문서를 참고하고, 변경 내용을 여러 의도로 분리할 수 있으면 반드시 분리한다.

## 1. 핵심 철학

- 커밋은 코드 저장이 아니라 설계 기록이다.
- 커밋 단위는 파일이 아니라 의도(Intent)다.
- 판단 기준은 기능 목록이 아니라 시스템 변화다.

가장 중요한 원칙:

> 하나의 커밋은 하나의 의도만 담는다.

## 2. 커밋 메시지 형식

모든 커밋 메시지는 아래 형식을 따른다.

```text
<type>: <scope> - <summary>
```

예시:

```text
feat: Feature/Card - 카드 드로우 로직 추가
refactor: System/Skill - SkillTemplate 구조 분리
fix: System/Grid - 타일 선택 오류 수정
chore: Project - InputSystem 패키지 추가
```

## 3. type 규칙

`type`은 아래 값만 사용한다.

- `feat`: 기능 추가
- `fix`: 버그 수정
- `refactor`: 구조 변경, 동작 동일
- `chore`: 설정, 환경, 빌드, 기타 운영 변경
- `docs`: 문서 변경
- `test`: 테스트 추가 또는 수정

판단 규칙:

- 새 동작이나 새 흐름이 생기면 `feat`
- 잘못된 동작을 바로잡으면 `fix`
- 구조만 바뀌고 결과가 같으면 `refactor`
- 패키지, 프로젝트 설정, 자동화, 도구 변경이면 `chore`

## 4. scope 규칙

`scope`는 폴더명이 아니라 시스템 단위로 작성한다.

좋은 예:

- `Core`
- `System/Grid`
- `System/Skill`
- `Feature/Card`
- `UI`
- `Data`
- `Save`
- `Stage`
- `Project`

원칙:

- 실제 변경 이유가 드러나는 시스템 이름을 쓴다.
- 여러 폴더를 건드렸더라도 의도가 하나면 같은 `scope`로 묶을 수 있다.
- UI 변경은 기능 로직과 분리해 `UI` 또는 해당 UI 시스템으로 별도 커밋한다.

## 5. summary 규칙

`summary`는 한 줄로 작성하고 결과 중심으로 적는다.

허용:

- 무엇이 추가되었는지
- 무엇이 분리되었는지
- 무엇이 수정되었는지
- 어떤 오류가 해결되었는지

금지:

- `update`
- `수정`
- `작업`
- `변경사항 반영`
- 의미가 모호한 표현 전반

좋은 예:

- `카드 드로우 로직 추가`
- `Buff Duration 시스템 추가`
- `타일 선택 오류 수정`
- `SkillTemplate 구조 분리`

## 6. 커밋 단위 규칙

### Rule 1. 하나의 의도만 포함

나쁜 예:

```text
카드 기능 추가 + UI 수정 + 버그 수정
```

좋은 예:

```text
feat: Feature/Card - 카드 드로우 로직 추가
feat: UI - 카드 드로우 UI 갱신
fix: System/Grid - 타일 선택 오류 수정
```

### Rule 2. 구조 변경은 기능 추가와 분리

구조를 먼저 바꾸고 그 위에 기능을 얹었다면 반드시 나눈다.

예시:

```text
refactor: System/Skill - Buff 처리 구조 분리
feat: System/Skill - Buff Duration 시스템 추가
```

### Rule 3. Unity 특수 규칙 적용

- `Scene` 변경은 반드시 별도 커밋으로 분리한다.
- `Prefab`, `ScriptableObject` 변경은 목적을 메시지에 명시한다.
- `meta` 파일은 항상 실제 에셋과 함께 포함한다.

예시:

```text
feat: Stage - Stage1 배치 수정
feat: Data - 카드 밸런스 값 조정
```

## 7. Unity 프로젝트 기준 보조 규칙

이 프로젝트는 `Core / Shared / Features` 구조를 사용하지만, 커밋 `scope`는 물리 폴더보다 설계 의도를 우선한다.

예시:

- `_Core` 기반 인프라 변경 -> `Core`
- 그리드 판정 규칙 변경 -> `System/Grid`
- 스킬 처리 파이프라인 변경 -> `System/Skill`
- 카드 기능 추가 -> `Feature/Card`
- ScriptableObject 데이터 조정 -> `Data`
- HUD, 팝업, 인게임 표시 변경 -> `UI`

즉, 파일 위치가 아니라 시스템 변화가 `scope`를 결정한다.

## 8. CLI 커밋 워크플로우

커밋 전 고정 루틴:

### 1단계. 상태 확인

```bash
git status
```

### 2단계. 변경 내용 이해

```bash
git diff
```

원칙:

> diff를 이해하기 전에는 add하지 않는다.

### 3단계. 선택적 add

```bash
git add Assets/Scripts/Card/
git add Assets/Prefabs/UI/
```

또는:

```bash
git add -p
```

### 4단계. 커밋

```bash
git commit -m "feat: Feature/Card - 카드 드로우 로직 추가"
```

### 5단계. 푸시

```bash
git push origin main
```

## 9. 상세 커밋 메시지 규칙

필요하면 제목 아래 본문을 추가할 수 있다.

예시:

```text
feat: System/Skill - Buff Duration 시스템 추가

- Tick 기반 감소 로직 추가
- 기존 Instant 구조와 분리
- 지속시간 상태 관리 구조 도입
```

본문에 적는 내용:

- 변경 이유
- 구조 변화
- 주요 로직 변화

## 10. 체크포인트 커밋 규칙

작업 중간 저장이 꼭 필요하면 아래처럼 제한적으로 사용한다.

```text
chore: WIP - SkillSystem 구조 변경 중간 저장
```

원칙:

- `WIP`는 공유용 최종 커밋으로 남발하지 않는다.
- 가능하면 로컬 임시 체크포인트로만 사용한다.
- 병합 전에는 의도 단위 커밋으로 정리하는 것을 우선한다.

## 11. AI / Codex용 생성 규칙

AI는 변경 내용을 입력받으면 아래 원칙으로 커밋 메시지를 생성한다.

1. 커밋은 코드 변경이 아니라 설계 의도를 표현해야 한다.
2. 하나의 커밋은 하나의 의도만 포함한다.
3. 형식은 반드시 `<type>: <scope> - <summary>`를 따른다.
4. `type`은 `feat`, `fix`, `refactor`, `chore`, `docs`, `test` 중 하나만 사용한다.
5. `scope`는 Unity 시스템 단위로 작성한다.
6. `summary`는 한 줄, 결과 중심, 모호하지 않게 쓴다.
7. 기능 추가와 구조 변경은 반드시 분리한다.
8. 데이터 변경은 `Data` scope를 우선 고려한다.
9. UI 변경은 기능 로직과 분리된 별도 커밋으로 우선 고려한다.
10. Scene 변경은 반드시 별도 커밋으로 표현한다.
11. `Prefab`과 `ScriptableObject` 변경은 목적을 명시한다.
12. 출력은 항상 여러 개의 커밋으로 분리 가능한지 먼저 검토한다.

## 12. AI 입력 / 출력 예시

입력:

```text
카드 드로우 기능 추가 + UI 수정
```

출력:

```text
feat: Feature/Card - 카드 드로우 로직 추가
feat: UI - 카드 드로우 UI 갱신
```

추가 예시:

입력:

```text
SkillTemplate 구조를 나누고 Buff 지속시간 기능을 넣음
```

출력:

```text
refactor: System/Skill - SkillTemplate 구조 분리
feat: System/Skill - Buff Duration 시스템 추가
```

## 13. 최종 원칙 요약

가장 중요한 세 문장:

> 커밋은 설계 로그다.  
> CLI 순서는 사고 과정이다.  
> 좋은 커밋은 의도를 분리한다.

고정 순서:

```text
status -> diff -> add -> commit
```

이 순서를 지키면 커밋 품질을 유지하고, 구조 추적과 협업 의도 전달이 쉬워진다.
