# Box Impact / Flip / Jump Rulebook

## 1. 목적

이 문서는 `Push`, `Sliding Push`, `Flip`, `Enemy Jump`가 서로 충돌할 때의 authoritative gameplay 규칙을 유지보수 가능하게 고정한다.

이 문서는 아래 문서들과 함께 읽는다.

- `Docs/Architecture/UnitOverlap_ImplementationPlan.md`
- `Docs/Architecture/Player-Action-Windup-Presentation-Blueprint.md`
- `Docs/Architecture/Enemy-AI-FSM-Implementation-Plan.md`

핵심 목표는 다음과 같다.

- `Unit + Box` 중첩 금지 규칙을 유지한다.
- `Push / Slide / Flip`으로 적에게 피해를 줄 수 있게 한다.
- `Flip 실패`를 로직과 표현 모두에서 일관되게 정의한다.
- `Enemy Jump`가 same-cell unit 규칙과 box blocking 규칙을 동시에 만족하게 한다.

## 2. 최종 규칙 요약

### 2-1. 점유 규칙

- `Unit + Unit`: 허용
- `Unit + Box`: 금지
- `Box + Box`: 금지
- `Jump landing on Unit`: 허용
- `Jump landing on Box / Wall / Terrain blocker`: 금지

즉, `Unit overlap`은 unit 전용 규칙이고, `Box`는 끝까지 solid entity다.

### 2-2. Box 충돌 규칙

- `Push` 또는 `Sliding Push`가 적 유닛 칸을 향하면 `Impact + Stop`
- `Flip`이 적 유닛 landing 칸을 향하면 `Impact + No Relocation`
- 적이 죽어도 같은 tick에는 box가 그 칸으로 들어가지 않는다.
- 적이 살아남아도 box와 unit은 겹치지 않는다.
- `적이 안 죽으면 box 파괴`는 기본 규칙이 아니다.

### 2-3. Flip 규칙

- `Flip`은 액션 시작 시점에 `wind-up`을 반드시 거친다.
- execute tick에 landing이 유효하면 기존 `Flip` 이동이 일어난다.
- execute tick에 landing에 적 유닛이 있으면 `impact`만 발생하고 box는 source cell에 남는다.
- execute tick에 landing이 벽, box, terrain 등으로 막혀 있으면 `BlockedNoImpact`다.

### 2-4. Jump 규칙

- `Jump Windup`: source cell을 계속 점유한다.
- `Jump Airborne`: `Detached` 상태이며 occupancy에 참여하지 않는다.
- `Jump Landing`: unit 규칙으로 legal cell을 찾는다.
- locked target cell에 unit만 있으면 same-cell landing 허용
- locked target cell에 box / wall / blocked terrain이 있으면 fallback 탐색
- legal landing cell이 없으면 airborne 유지 후 retry

## 3. Tick 순서 기준

현재 authoritative 순서는 아래와 같다.

```text
Enemy AI
-> PreMovementState
-> Movement
-> Attack
-> Cleanup
```

이 순서 때문에 다음 사실이 항상 성립한다.

- `Enemy Jump landing`은 movement 전에 resolve된다.
- `Push / Flip / Slide` impact는 movement 단계에서 synthetic reservation으로 생성된다.
- impact damage는 attack 단계에서 적용된다.
- `hp <= 0` 또는 `markedForDeath`가 되어도 cleanup 전까지는 entity가 남아 있다.

따라서 `Flip impact`가 발생해 적을 죽였더라도 같은 tick에는 box가 landing cell로 들어갈 수 없다.

## 4. Push / Slide / Flip 상세 규칙

### 4-1. Push

- adjacent push box가 있고 next slide step이 비어 있으면 기존 push 성공
- adjacent push box의 next slide step이 적 유닛 칸이면 `Impact + Stop`
- adjacent push box의 next slide step이 비적대 unit 칸이면 `BlockedNoImpact`
- adjacent push box의 next slide step이 wall / box / terrain이면 기존 stopper 규칙 유지
- adjacent step 자체가 불가하지만 box가 `Destroy` capability를 가지면 기존 destroy fallback 유지
- impact가 발생해도 box는 unit cell에 들어가지 않는다.

### 4-2. Sliding Push

- sliding 중 다음 step이 비어 있으면 계속 이동
- sliding 중 다음 step이 적 유닛 칸이면 `Impact + Stop`
- impact가 발생한 tick에 box state는 `Idle` 또는 stop-equivalent state로 종료된다.
- hostile unit이 전혀 없고 friendly unit만 있으면 impact가 아니라 blocked다.

### 4-3. Flip

- start 시점에는 `target box`만 유효하면 action이 시작될 수 있다.
- landing cell의 success / impact / blocked 판정은 execute tick authoritative snapshot에서 확정한다.
- wind-up 중 월드가 변해 execute 시점 landing이 적 유닛 칸으로 바뀌면 `Impact + No Relocation`
- wind-up 중 landing이 wall / box / terrain으로 바뀌면 `BlockedNoImpact`
- `Impact + No Relocation`은 cancel이 아니라 execute된 실패다.
- `Impact + No Relocation`이라도 wind-up과 recovery는 정상 action으로 간주한다.

## 5. Enemy Jump 상세 규칙

### 5-1. Windup

- jump enemy는 source cell에 남아 있다.
- 이 상태의 enemy는 일반 unit과 동일하게 box impact 대상이다.

### 5-2. Airborne

- jump enemy는 `Detached` 상태다.
- occupancy에 없으므로 box impact 대상이 아니다.
- selection / target query에서도 untargetable로 취급한다.

### 5-3. Landing

- landing 시도는 `locked target exact -> target ring fallback -> source exact -> source ring fallback` 순서를 따른다.
- unit-only cell은 legal landing cell이다.
- box / wall / terrain blocked cell은 illegal landing cell이다.
- legal cell이 없으면 retry한다.

### 5-4. Landing Tick Combat

- landing tick에는 jump movement는 끝났지만 attack는 가능할 수 있다.
- same-cell landing 후 contact damage / melee는 기존 attack 규칙으로 처리한다.
- jump 자체가 box를 파괴하거나 box를 밀어내는 능력으로 해석되지는 않는다.

## 6. Presentation 규칙

### 6-1. Flip Wind-up

- `FlipWindup`은 성공/실패 여부와 무관하게 action 시작 후부터 표시한다.
- execute 전 실패 취소가 아니라면 wind-up은 정상 재생된다.

### 6-2. Flip Recovery

- 성공 flip: 기존 `FlipRecovery + flip arc motion`
- impact 실패 flip: `FlipRecovery + in-place recoil`
- blocked 실패 flip: `FlipRecovery` 또는 짧은 fail settle

중요한 점:

- `full return animation`은 추가하지 않는다.
- authoritative 상 box는 실패 시 source cell을 떠난 적이 없기 때문이다.
- 따라서 표현은 `return`이 아니라 `rebound` 또는 `settle`이어야 한다.

### 6-3. Outcome 분리

presentation에서는 아래 outcome을 render-only metadata로 해석할 수 있다.

- `SuccessMove`
- `ImpactNoMove`
- `BlockedNoImpact`

이 값은 authoritative action kind를 늘리기 위한 근거가 아니다.

## 7. 데이터 및 소유권 규칙

box impact는 projectile impact와 동일한 철학으로 다룬다.

- movement phase가 synthetic impact reservation을 만든다.
- attack phase가 damage를 적용한다.
- cleanup phase가 죽은 entity를 제거한다.

적대 판정은 `box 자체의 기본 teamId`에 기대지 않는다.

- box는 neutral spawn이 가능하다.
- sliding chain을 위해 box에는 별도의 `kinetic instigator team` 문맥이 필요하다.
- 이 값은 player push/flip execute 시 box에 기록되고, sliding 동안 유지된다.
- stop 또는 idle settle에서 자동 clear하지 않고, 다음 push/flip execute가 오면 overwrite한다.

권장 데이터 예시는 아래와 같다.

- `kineticInstigatorEntityId`
- `kineticInstigatorTeamId`

이 문맥은 `box 기본 teamId`와 다른 의미다.

## 8. 비목표

- `Unit + Box` overlap 허용
- same tick에 `impact 후 전진` 허용
- flip 실패 시 실제 landing 후 복귀하는 authoritative 이동 추가
- jump에 stomp, splash, shockwave 같은 신규 공격 의미 부여
- `적 생존 시에만 box 파괴` 같은 attack 결과 의존 fallback 추가

## 9. 기각된 대안

### 9-1. Unit + Box overlap 허용

기각 이유:

- 현재 보드 상태, 배치 정책, slide 규칙의 핵심 invariant와 충돌한다.
- box 퍼즐 규칙이 광범위하게 흔들린다.

### 9-2. Flip full return animation

기각 이유:

- authoritative 상 실패 flip의 box는 source cell을 떠난 적이 없다.
- landing 후 복귀처럼 보이는 표현은 gameplay truth를 흐린다.

### 9-3. Enemy survives then destroy box

기각 이유:

- survival 여부는 attack phase 누적 결과 이후에 확정된다.
- movement 규칙과 attack 결과를 과도하게 결합한다.
- baseline 설계는 이미 `impact + no overlap`으로 문제를 해결한다.

## 10. 유지보수 체크리스트

이 규칙을 수정하려는 경우 아래 질문에 먼저 답해야 한다.

1. `Unit + Box` 중첩을 허용하려는가, 아니면 impact만 허용하려는가?
2. `Flip 실패`를 cancel로 보는가, execute된 실패로 보는가?
3. `Jump Airborne`를 occupancy에 다시 넣으려는가?
4. `same tick impact 후 전진`을 허용하면 cleanup 순서를 바꿔야 하는가?
5. hostile 판정이 `box kinetic owner`를 기준으로 안정적으로 유지되는가?

위 질문 중 하나라도 `예`가 되면 movement, attack, cleanup, presentation, determinism hash를 함께 재검토해야 한다.
