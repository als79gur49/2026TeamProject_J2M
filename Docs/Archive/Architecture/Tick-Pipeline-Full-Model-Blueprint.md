> Archived historical document.
> This file is not part of the active truth-source chain. Start with [Docs/Architecture/README.md](../../Architecture/README.md).
> Archive index: [Docs/Archive/README.md](../README.md).

> Non-canonical target blueprint.
> Canonical current behavior and boundaries are documented in [Tick-Simulation-Canonical-Spec.md](../../Architecture/Tick-Simulation-Canonical-Spec.md).

# Tick Pipeline Full Model Blueprint

## 1. 목적

이 문서는 기존 Tick 기반 시뮬레이션을 확장한 `Full Model (Plan -> Resolve -> Finalize)`의 목표 구조와 설계 철학을 정의한다.

이 문서는 코드 수정을 직접 지시하지 않는다. 대신 아래 목적을 가진다.

- `Full Model`의 구조와 철학을 이해한다.
- 기존 `Movement -> Attack -> Cleanup` 구조와의 차이를 명확히 인지한다.
- 왜 이 모델이 필요한지 설명할 수 있게 한다.
- 이후 작성할 리팩토링 문서의 판단 기준으로 사용한다.

이 문서는 아래 문서들과 함께 읽는다.

- `Docs/Architecture/Deterministic-Tick-Simulation-Blueprint.md`
- `Docs/Architecture/Box-Impact-Flip-Jump-Rulebook.md`

중요한 위치 지정:

- 현재 구현의 authoritative runtime은 아직 기존 Tick Pipeline에 있다.
- 이 문서는 현재 구현 설명서가 아니라, 앞으로 파이프라인을 재구성하기 위한 target blueprint다.
- 후속 리팩토링 문서는 이 문서를 기준으로 작성한다.

## 2. 현재 구조와 한계

현재 TickPipeline의 핵심 구조는 아래와 같다.

```text
Movement -> Attack -> Cleanup
```

그리고 각 phase 내부는 대체로 아래 흐름을 따른다.

```text
Intent Collect -> Resolve -> Commit
```

이 구조의 암묵적 전제는 아래와 같다.

> 각 Phase는 자신의 결과를 그 단계에서 즉시 확정할 수 있다.

예를 들면 아래와 같다.

- `Movement`에서 위치가 확정된다.
- `Attack`에서 데미지가 확정된다.
- `Cleanup`에서 제거가 확정된다.

이 방식은 phase 경계가 단순하고 상호작용이 제한적일 때는 유지보수하기 쉽다. 하지만 결과가 다른 phase의 해석에 의해 뒤집히기 시작하면 구조적 한계가 드러난다.

## 3. 왜 Full Model이 필요한가

현재 `Push`, `Flip` 요구사항은 더 이상 순수한 movement 문제로만 다룰 수 없다.

특히 아래 같은 상황이 생기면 문제가 발생한다.

- 이동 도중 공격 결과에 따라 최종 이동 가능 여부가 달라진다.
- 대상이 살아남으면 blocker로 남고, 죽으면 통과가 가능해진다.
- `Shield`, `Counter`, `Revive`, `Trap` 같은 반응 규칙이 중간 결과를 바꾼다.
- 한 시스템이 만든 결과가 다른 시스템에서 다시 재해석된다.

즉, 아래와 같은 의존성이 생긴다.

```text
Movement result <- Attack / Reaction result
```

기존 구조에서는 이런 의존성이 phase-local 분기와 예외 처리로 퍼지기 쉽다.

결과적으로:

- "아직 확정되지 않은 상태"를 표현하기 어렵다.
- phase 간 의존성이 코드 전반으로 누수된다.
- 규칙이 추가될수록 조건 분기와 예외 commit이 증가한다.
- 순서가 곧 규칙이 되어, 설계보다 실행 순서가 결과를 지배하게 된다.

현재까지의 `Push` / `Flip` 규칙은 기존 pipeline 제약 안에서 우회적으로 정리할 수 있었지만, 앞으로 attack 결과가 movement 결과를 바꾸는 요구를 수용하려면 pipeline 자체를 바꾸는 편이 더 일관적이다.

## 4. Full Model의 핵심 관점

Full Model은 문제를 아래와 같이 재정의한다.

> 결과는 즉시 확정되지 않는다.
> 결과는 여러 단계의 해석을 거쳐 최종 확정된다.

기존 관점은 아래에 가깝다.

```text
Movement -> 확정
Attack -> 확정
```

Full Model은 아래와 같다.

```text
Plan -> Resolve -> Finalize -> 확정
```

핵심 차이는 "행동이 발생했다"와 "결과가 확정됐다"를 같은 단계로 보지 않는 데 있다.

## 5. 목표 Pipeline

Full Model의 목표 pipeline은 아래와 같다.

```text
Input
-> PlanPhase
-> ResolvePhase
-> FinalizePhase
-> CleanupPhase
-> RespawnPhase
```

핵심 규칙은 아래와 같다.

- `PlanPhase`는 의도를 계획으로 바꾼다.
- `ResolvePhase`는 상호작용을 해석한다.
- `FinalizePhase`만 최종 결과를 확정하고 `WorldState`를 변경한다.
- `CleanupPhase`와 `RespawnPhase`는 확정된 결과 이후의 후처리를 담당한다.

## 6. 각 Phase의 의미

### 6-1. PlanPhase

역할:

- "무엇을 하려고 하는가"를 정의한다.
- 아직 결과는 확정하지 않는다.

주요 책임:

- Intent 수집
- `ActionPlan` 생성
- 이동 경로 계산
- reservation 계산
- `Contest` 생성

이 단계의 산출물은 결과가 아니라 "후속 해석 대상"이다.

### 6-2. ResolvePhase

역할:

- 계획들 사이의 상호작용을 해석한다.

주요 책임:

- 데미지 계산
- 생존 여부 후보 계산
- 반응 처리 (`shield`, `counter`, `revive` 등)
- 충돌 상태에 대한 해석 결과 생성

중요:

- 이 단계에서도 결과는 완전히 확정되지 않는다.
- 이 단계의 산출물은 "최종 판단에 필요한 해석 결과"다.

### 6-3. FinalizePhase

역할:

- `Plan`과 `Resolution`을 모두 반영해 최종 결과를 확정한다.

주요 책임:

- 위치 확정
- 이동 성공/실패 확정
- 사망/생존 확정
- 실제 `WorldState` 변경

중요:

- authoritative mutation은 이 단계에서만 수행한다.
- 어떤 phase도 이 단계 이전에 단독으로 최종 결과를 확정하지 않는다.

### 6-4. CleanupPhase

역할:

- 확정된 결과의 후처리를 수행한다.

주요 책임:

- 사망 제거
- invalid 상태 정리
- overlap 해소
- 다음 tick을 위한 정리 작업

### 6-5. RespawnPhase

역할:

- 제거 이후의 재생성, 보충, spawn 후속 규칙을 처리한다.

중요:

- respawn은 interaction 해석의 일부가 아니라, finalized world를 입력으로 받는 후처리 규칙으로 둔다.

## 7. 핵심 개념

### 7-1. ActionPlan

`Intent`의 확장된 형태다.

예:

- 이동 계획
- 공격 계획
- 밀기 계획
- 반응 계획

핵심 성질:

- `ActionPlan`은 "무엇을 하려는가"를 표현한다.
- 아직 "무슨 결과가 났는가"를 표현하지 않는다.

### 7-2. Contest

`Contest`는 아직 결과가 확정되지 않은 충돌 상태를 뜻한다.

예:

- 이 타일에 진입 가능한가
- 이 대상이 죽는가 살아남는가
- 반응 규칙이 개입하는가
- 이 이동은 유지되는가 취소되는가

핵심 의미는 아래와 같다.

> 결정 전 상태를 명시적으로 모델링한다.

기존 구조에서 암묵적 분기로 흩어지던 문제를, Full Model은 `Contest`라는 데이터로 끌어올린다.

### 7-3. Resolution

`ResolvePhase`의 결과물이다.

예:

- 데미지 결과
- 생존 여부 후보
- 반응 적용 결과
- contest별 우선 해석 결과

`Resolution`은 중요하지만 여전히 최종 mutation은 아니다.

### 7-4. Finalization

모든 `Plan`과 `Resolution`을 바탕으로 authoritative 결과를 만드는 단계다.

예:

- 최종 위치 확정
- 최종 사망/생존 확정
- 실제 점유 상태 갱신
- 실제 월드 데이터 변경

## 8. 설계 원칙

### 8-1. One Source of Truth

- `WorldState`만 authoritative state다.
- 다른 모든 데이터는 계산 중에만 존재하는 임시 결과다.

### 8-2. Deferred Mutation

- `Plan`과 `Resolve`는 `WorldState`를 직접 바꾸지 않는다.
- 모든 mutation은 `Finalize`에서만 수행한다.

### 8-3. Determinism

- 동일 입력은 항상 동일 결과를 만들어야 한다.
- 자료구조 순회 순서나 우발적 commit 순서가 결과를 바꾸면 안 된다.
- 정렬, 우선순위, tie-break 기준은 안정적으로 고정한다.

### 8-4. Phase Separation

- `Plan = 의도 정의`
- `Resolve = 상호작용 해석`
- `Finalize = 결과 확정`

phase의 책임을 섞지 않는 것이 중요하다.

### 8-5. Explicit Unfinalized State

- "아직 모른다"
- "조건에 따라 바뀔 수 있다"
- "여러 시스템이 동시에 개입한다"

이런 상태를 암묵적 예외가 아니라 explicit model로 표현해야 한다.

## 9. 기존 구조와의 본질적 차이

기존 구조의 핵심은 아래와 같다.

```text
각 Phase가 자기 결과를 확정한다
```

Full Model의 핵심은 아래와 같다.

```text
어떤 Phase도 단독으로 최종 결과를 확정하지 않는다
-> Finalize에서만 authoritative하게 확정한다
```

차이를 정리하면 아래와 같다.

- 기존 구조는 `phase-local commit` 중심이다.
- Full Model은 `cross-phase interpretation + final commit` 중심이다.
- 기존 구조는 결과 뒤집힘을 예외 규칙으로 다룬다.
- Full Model은 결과 뒤집힘 가능성을 기본 구조로 받아들인다.
- 기존 구조는 순서 의존성이 강하다.
- Full Model은 순서보다 데이터 흐름과 해석 단계를 더 중요하게 본다.

## 10. Push / Flip 요구사항과의 관계

이 문서가 필요한 직접적인 이유는 `Push`, `Flip` 요구사항이 더 이상 현재 pipeline에 자연스럽게 들어가지 않기 때문이다.

핵심은 아래와 같다.

- `Push`와 `Flip`의 결과가 attack 결과에 의해 변경될 수 있다.
- 즉, movement phase에서 outcome을 확정해 버리면 이후 해석이 구조적으로 어색해진다.
- 반대로 attack phase가 movement 결과를 되돌리기 시작하면 phase ownership이 무너진다.

따라서 앞으로의 구조는 아래 방향을 따라야 한다.

- `Push`, `Flip`, `Attack`, `Reaction`을 각각 고립된 phase 결과로 보지 않는다.
- 이들을 공통 `ActionPlan`과 `Contest`로 올린다.
- 상호작용은 `ResolvePhase`에서 중앙 해석한다.
- 실제 이동 성공/실패와 생존/사망은 `FinalizePhase`에서 함께 확정한다.

즉, 문제는 `Push` / `Flip` 기능 자체가 아니라, 그 기능이 요구하는 상호작용 밀도다.

## 11. 이 모델이 특히 필요한 조건

아래 조건이 늘어날수록 Full Model의 필요성이 커진다.

- 여러 시스템이 같은 tick에 동시에 상호작용하는 경우
- 결과가 다른 시스템에 의해 뒤집히는 경우
- 반응형 규칙이 누적되는 경우
- 확정되지 않은 중간 상태를 명시적으로 다뤄야 하는 경우
- 규칙이 계속 확장될 예정인 경우

대표 예시는 아래와 같다.

- `Push`
- `Flip`
- `Crush`
- `Shield`
- `Counter`
- `Revive`
- `Trap`

## 12. 이 문서가 다루지 않는 것

이 문서는 아래 내용을 정의하지 않는다.

- 실제 코드 파일 배치
- 구체적인 타입 이름과 인터페이스 이름
- migration 순서
- 테스트 추가 순서
- 현재 코드베이스를 어떤 단계로 나눠 리팩토링할지에 대한 실행 계획

이 내용은 이후 별도의 리팩토링 문서에서 정의한다.

## 13. 한 줄 핵심 정의

> Full Model은 "즉시 확정" 구조를 "단계적 확정" 구조로 바꾸는 설계다.
