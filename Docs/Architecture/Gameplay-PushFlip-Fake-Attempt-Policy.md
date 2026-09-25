# Gameplay Push/Flip Fake Attempt Policy

이 문서는 Push/Flip 실패 입력을 fake attempt로 처리하는 현재 구현의 active supporting truth-source다.

작성일: 2026-05-09

관련 커밋:

- `678d8680` `feat: System/Gameplay - Push/Flip 실패 attempt 분리`
- `66c48748` `feat: System/Presentation - Push/Flip fake attempt 애니메이션 연결`
- `1724d23c` `feat: Feature/GameplayAudio - Push/Flip fake attempt 실패음 추가`
- `2f5e6b21` `fix: Gameplay/ActionAttempt - Push Flip 실패 시 이동 생성 차단`
- `e21c849d` `fix: Gameplay/Presentation - fake Push Flip 애니메이션 hold 유지`
- `ad3b18fc` `fix: Gameplay/Input - fake Push Flip 재생 중 이동 입력 억제`
- `3004ada4` `test: Gameplay/ActionAttempt - Push Flip fake attempt 회귀 검증 추가`

## 1. Problem

Push/Flip 입력은 이동보다 우선하는 action attempt다.

기존 문제는 실패 Push/Flip 입력이 실제 action start나 Free2D ActionAssist로 성립하지 않는 경우에도 movement path가 계속 열려 있었다는 점이다.

대표 증상:

- Push no target + held move에서 ordinary movement 또는 Free2D local locomotion이 생성됨
- moving 중 Push/Flip 입력 시 same-face kinematic continuation이 유지됨
- fake attempt signal이 한 tick만 존재해 Push/Flip fake animation이 Walk/Idle에 즉시 덮임
- fake animation hold 중 다음 tick held move가 다시 command로 생성되어 player가 이동함

따라서 수정의 핵심은 `attempt classification -> movement consume -> presentation/audio signal -> host-local playback hold -> input gate` 순서를 고정하는 것이다.

## 2. Required Behavior

실제 Push/Flip이 가능한 경우:

- 기존 `PlayerControlState.activeAction` 흐름을 사용한다
- 기존 `TickPlayerActionPresentationSignal`을 생성한다
- 기존 action lifecycle audio와 Flip interaction / box motion / impact presentation을 유지한다
- fake attempt signal을 생성하지 않는다

Free2D ActionAssist가 가능한 경우:

- 기존 `queuedFree2DAction -> AlignToAnchor -> actual action` 흐름을 사용한다
- fake attempt signal을 생성하지 않는다
- ActionAssist 성공 player를 fake consumed set에 넣지 않는다
- stage-backed production 설정은 중심에서 축별 `0.421875` cell(`1728` fixed units)까지 포함한다. 전체 타일 폭 기준으로는 `84.375%` 보조 구간이다
- stage-backed production 충돌 반경은 `0.28125` cell(`1152` fixed units)이며, 인접 solid 앞의 중심 clamp는 `0.21875` cell(`896` fixed units)이다
- framework 기본값인 Action Assist `0.125` cell과 collision radius `0`은 historical/default composition contract를 위해 유지한다

둘 다 불가능한 경우:

- `activeAction`을 만들지 않는다
- `queuedFree2DAction`을 만들지 않는다
- `AlignToAnchor`, `MoveEntity`, `EntityMotion`을 만들지 않는다
- ordinary move, Free2D local locomotion, topology approach settle, same-face kinematic continuation을 막는다
- 별도 `TickPlayerActionAttemptPresentationSignal`과 failure audio request를 생성한다. 허용된 실패 입력은 별도 facing 정책에 따라 player 방향만 기록할 수 있다
- fake animation은 simulation state가 아니라 host-local presentation hold로 끝까지 재생한다

## 3. Runtime Structure

Implementation map:

- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs`
  - pre-movement attempt classification
  - failed interaction facing decision after attempt classification
  - movement consume set wiring
  - Free2D local locomotion and same-face kinematic continuation skip
- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPresentationData.cs`
  - `PlayerActionAttemptFeedbackKind`
  - `TickPlayerActionAttemptPresentationSignal`
  - `PlayerActionAttemptSignals`
- `Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickResultBuilder.cs`
  - tick-local resolution to fake attempt presentation signal conversion
- `Assets/_Features/Gameplay/Gameplay_PlayerControl/Runtime/PlayerControlState.cs`
  - pure ActionAssist candidate query used to distinguish target presence from range/settle failure
- `Assets/_Features/Gameplay/Gameplay_PlayerControl/Runtime/PlayerControlStateLogic.cs`
  - actual action start and ordinary movement facing writes; failed interaction facing is deferred to Plan
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerViewPresentationMapper.cs`
  - player-only mapping from attempt signal to view presentation state
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayAnimationSyncCoordinator.cs`
  - host-local active action / action attempt visual hold ownership
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerAnimatorDriver.cs`
  - explicit playback request and phase override support
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayInputHost.cs`
  - Push/Flip press-time direction capture, active action/fake playback input gate, and buffered input clearing
- `Assets/_Features/Gameplay/Gameplay_ActionAudio/Runtime/GameplayActionAudioRequestPlanner.cs`
  - fake failure audio request planning from `PlayerActionAttemptSignals`

### Simulation attempt classification

`TickPipeline`은 movement intent 수집 전에 Push/Flip input을 선판정한다.

역할:

- actual action 또는 queued Free2D action이 가능한지 먼저 확인한다
- 실패 attempt인 경우 tick-local `PlayerActionAttemptResolution`을 생성한다
- `ConsumesMovement=true`면 해당 player id를 `consumedPlayerActionAttemptEntityIds`에 등록한다
- movement intent 수집 후 consumed player movement intent를 제거한다
- Free2D local locomotion과 same-face kinematic continuation builder에 consumed set을 전달한다

이 구조가 필요한 이유:

- movement consume은 presentation layer에서 결정할 수 없다
- fake attempt는 movement보다 우선하는 input classification이므로 movement plan보다 앞에서 확정되어야 한다
- same tick에서 raw movement, native Free2D, kinematic continuation이 각자 따로 생기는 것을 공통적으로 막아야 한다

### Presentation signal split

`TickPresentationData`는 actual action과 fake attempt를 분리한다.

- actual action: `TickPlayerActionPresentationSignal`
- fake attempt: `TickPlayerActionAttemptPresentationSignal`

`TickResultBuilder`는 `PlayerActionAttemptResolution` 중 `EmitsFakePresentation=true`인 항목만 `PlayerActionAttemptSignals`로 변환한다.

이 구조가 필요한 이유:

- fake attempt는 active action lifecycle이 아니다
- `ActionPlanId`, `Started/Executed/Recovery/Completed` 같은 lifecycle fact를 위조하면 Flip track, box motion, blocked/success audio와 섞인다
- audio/presentation consumer가 fake failure와 actual action을 독립적으로 판단할 수 있어야 한다

### Host playback hold

`GameplayAnimationSyncCoordinator`는 player visual hold source를 구분한다.

- `ActiveAction`: actual Push/Flip action hold
- `ActionAttempt`: fake Push/Flip attempt hold

fake attempt signal은 tick-local trigger이고, 지속 시간은 host-local hold가 관리한다.

우선순위:

1. Death
2. actual active action
3. current fake action attempt hold
4. walk / active walk motion
5. idle

이 구조가 필요한 이유:

- fake attempt signal 자체를 여러 tick 저장하면 simulation state가 오염된다
- signal을 한 tick만 사용하면 다음 frame/tick의 Walk/Idle이 animation을 끊는다
- player animation playback duration은 host presentation concern이므로 host-local hold가 적절한 owner다

### Input gate during interaction playback

`GameplayInputHost`는 `TickResult.PresentationData.PlayerActionSignals`에서 authoritative active action 입력 잠금을 갱신하고, presenter에서 player별 active action/fake attempt playback 여부를 조회한다.

active action 또는 fake attempt hold 중에는:

- player command를 `PlayerTickCommand.None`으로 반환한다
- move buffer를 clear한다
- pending Push/Flip action buffer를 clear한다
- simulation tick 자체는 멈추지 않는다

이 구조가 필요한 이유:

- visual hold는 simulation action lifecycle보다 늦게 끝날 수 있으므로 다음 tick simulation pipeline만으로 held move 재진입을 막을 수 없다
- player 입력만 막아야 하며 world/enemy tick은 계속 진행되어야 한다
- topology presentation lock과 fake attempt playback lock은 의미가 다르므로 같은 blocking presentation path에 섞지 않는다

### Audio mapping

actual action audio는 `PlayerActionSignals`만 읽는다.

fake failure audio는 `PlayerActionAttemptSignals`만 읽는다.

fake failure moment:

- `AssistOutOfRange`
- `NoTarget`
- `Invalid`

이 구조가 필요한 이유:

- fake failure는 actual `Windup`이나 removed action-audio moments인 `Execute`, `Recovery`, `Contact`, `ImpactEnemy`, `Blocked`를 합성하지 않는다
- success/blocked audio와 failure audio를 같은 lifecycle path에서 중복 재생하지 않는다
- audio-specific data를 `TickResult`에 추가하지 않고 presentation facts를 소비한다

세부 audio governance는 [Gameplay-Action-Audio-Governance.md](./Gameplay-Action-Audio-Governance.md)를 따른다.

## 4. Direction Policy

fake attempt animation에는 direction이 필요하다.

action direction selection for Push/Flip, ActionAssist, and fake attempt classification:

1. currently held move direction
2. explicit command move direction when `IsMoveBuffered` is false
3. authoritative entity facing
4. `Direction.None` when no cardinal direction exists

Input System action callback에서 들어온 Push/Flip은 해당 입력 업데이트가 시작되기 전에 이미 유지 중이던 방향을 최초 callback에서 캡처한다. 같은 입력 업데이트 중 새로 들어온 방향은 action 방향으로 사용하지 않는다. 캡처 시 방향이 `None`이었다는 사실도 보존하고, 이 경우 simulation에서 authoritative facing을 사용한다. 뒤따른 방향 입력은 이미 캡처된 action 방향을 바꾸지 않는다. 직접 호출하는 UI/테스트용 `BufferPush`·`BufferFlip`은 호출 시점의 유지 방향을 캡처한다. 명시적 테스트·리플레이 command는 캡처 값이 없을 때 기존 우선순위를 유지한다.

The `0.125s` released movement buffer is not a currently held interaction direction. A fake visual may still use `Direction.Up` when all action-direction sources are invalid.

중요한 경계:

- fake visual fallback `Direction.Up`은 presentation/audio feedback용이며 actual target resolution에 사용하지 않는다
- fake visual fallback `Direction.Up`은 authoritative facing에도 기록하지 않는다
- target capability, settlement, lock, landing, and action timing requirements remain unchanged
- direction이 없다는 이유로 fake attempt signal을 누락하지 않는다

실패 입력의 방향 기록은 fake signal이 아니라 simulation의 별도 입력 정책이다. PreMovement 시점에 이전 active action이 없고 action start gate가 허용했으며 실제 cardinal 방향이 있을 때만 Plan의 실패 분류 이후 `FinalizationBatch`에 기록한다. 일반 실패는 정착 여부와 무관하게 player facing을 요청 방향으로 기록한다. 잠금 판정으로 fake 화면 재생이 생략되면 방향을 바꾸지 않는다. Flip 착지 차단은 기존처럼 PreMovement 시점에 정착했을 때만 방향을 바꾼다. 잠금과 착지 차단이 중첩되어 기존 분류상 일반 실패가 되는 경우는 잠금 생략 예외에 포함하지 않는다.

미정착 player에 authoritative `UnitContinuousLocomotionState`가 있으면 같은 배치에서 그 state의 `facing` 필드만 동기화한다. offset, velocity, mode, sequence, last move direction은 그대로 두고 상태가 없으면 새로 만들지 않는다. 이 facing 기록은 이동·ActionAssist 정착·실제 Push/Flip 실행을 만들지 않는다.

## 5. Forbidden Paths For Fake Attempt

fake attempt는 아래 path를 타면 안 된다.

- `PlayerControlState.activeAction` 생성
- `QueueFree2DAction` 호출
- `queuedFree2DAction` 저장
- `CreateAlignToAnchorState` 또는 AlignToAnchor 실행
- `SetUnitContinuousLocomotionState`를 통한 settle/snap 유도
- `MoveEntity` 생성
- `TickEntityMotion` 생성
- `TickPlayerActionPresentationSignal`에 fake signal 삽입
- Flip interaction track, box motion, movement presentation, FlipImpactSignal에 연결
- actual action success/blocked audio path 재사용

현재 의도된 owner boundary:

- simulation: attempt classification, movement consume, 실패 입력 facing 정책
- tick presentation: fake attempt signal emission
- host presentation: fake animation playback hold
- host input: fake playback 중 player command gate
- action audio: fake failure moment request

## 6. Why This Shape Was Chosen

Alternative 1: fake attempt를 `PlayerActionSignals`에 넣기

- rejected
- active action lifecycle을 위조하게 된다
- actual action track, Flip impact, box motion, blocked/success audio와 섞인다

Alternative 2: fake attempt를 `PlayerControlState`에 저장하기

- rejected
- failure feedback은 simulation state가 아니다
- replay/determinism state에 presentation-only duration이 들어갈 위험이 있다

Alternative 3: presentation layer에서 movement를 막기

- rejected
- movement 생성 여부는 simulation pipeline plan phase에서 결정되어야 한다
- same tick movement, Free2D settle, kinematic continuation을 이미 만든 뒤에는 금지 경로를 되돌리기 어렵다

Chosen shape:

- simulation은 tick-local resolution으로 movement consume을 확정한다
- `TickPresentationData`는 actual action과 fake attempt를 분리한다
- host는 fake animation duration만 local hold로 관리한다
- input host는 fake hold 중 player command만 막는다

이 방식은 fake attempt를 simulation state로 만들지 않으면서도 animation이 끝까지 재생되고, held move 재진입을 막을 수 있다.

## 7. Known Weaknesses And Caution Points

### `TickPipeline` responsibility growth

`TickPipeline`이 attempt classification, ActionAssist queue check, movement consume 전달을 직접 수행한다.

위험:

- Push/Flip 이외 action attempt가 늘어나면 `TickPipeline`이 더 커진다
- classification policy와 movement planner wiring이 섞일 수 있다

권장 후속 정리:

- 내부 helper 또는 전용 classifier로 `PlayerActionAttemptResolution` 생성 정책을 분리한다
- movement consume set wiring은 plan phase orchestration에 남기되, feedback kind 판단은 별도 pure policy로 옮긴다

### Attempt classifier policy is single-sourced

`TryCreatePlayerActionAttemptResolution`은 actual call site가 사용하는 단일 classifier다.

- queue 판정과 실패 feedback 판정은 모두 authoritative runtime snapshot의 `ActionAssistSettleWindowUnits`를 사용한다
- stage-backed production tuning과 framework default가 달라도 같은 tick에서 queue와 feedback의 범위 해석이 갈라지지 않는다
- locked target 판정은 현재 `tickIndex`를 사용하므로 만료된 lock을 고정 tick으로 오판하지 않는다

유지 조건:

- 새 overload에 window 또는 lock 정책을 복제하지 않는다
- production tuning 변경 시 stage installer, authoritative conversion, Push/Flip boundary, locked-target feedback 테스트를 함께 갱신한다

### Presentation hold is host-local state

fake animation hold는 simulation snapshot에 저장되지 않는다.

의도:

- presentation-only playback duration을 deterministic simulation state에 넣지 않는다

주의:

- save/load 또는 scene reload 중 fake animation hold는 복원 대상이 아니다
- replay verification은 signal emission과 movement absence를 검증하고, host playback duration은 host tests로 검증해야 한다

### Input depends on presentation hold query

`GameplayInputHost`가 presenter를 통해 player별 active action 또는 fake attempt playback active 여부를 조회한다.

이유:

- authoritative action 종료 후에도 남을 수 있는 visual hold와 fake hold 동안 held move가 다시 command로 생성되는 것을 막기 위한 host-local gate다

위험:

- input layer가 presentation runtime 상태를 조회하므로 dependency direction이 민감하다
- 이 query를 topology transition blocking과 섞으면 전체 tick lock처럼 오해될 수 있다

규칙:

- `IsPlayerInteractionPlaybackActive`는 해당 player의 `ActiveAction`/`ActionAttempt` hold만 player command gate에 제공한다
- topology transition active / blocking presentation path와 합치지 않는다
- `IsPlayerActionAttemptPlaybackActive`는 기존 fake attempt 전용 관측 의미를 유지한다

### Direction preservation vs visual facing

fake attempt signal과 presentation state는 direction을 보존한다.

주의:

- fake signal의 direction 자체는 simulation facing write가 아니다. 별도의 실패 입력 정책만 authoritative facing write를 결정한다
- host에서 실제 visual facing override로 사용할 때도 presentation-only 경로여야 한다
- direction을 movement, box motion, Flip interaction target resolution에 재사용하면 안 된다

검토 필요:

- fake Push/Flip animation이 방향-sensitive clip 또는 facing parameter를 요구하는 경우, animator-facing 적용 경로가 별도 presentation-only로 보장되는지 확인해야 한다

### Audio semantics are intentionally narrow

fake failure audio는 `AssistOutOfRange`, `NoTarget`, `Invalid`만 생성한다.

주의:

- fake attempt에서 `Windup`이나 removed action-audio moments인 `Execute`, `Recovery`, `Contact`, `ImpactEnemy`, `Blocked`를 합성하지 않는다
- actual blocked는 existing `PlayerActionSignals` blocked path에서만 발생해야 한다
- "맨땅 실패"와 "blocked actual action"은 서로 다른 의미다

### Held input repeat

fake attempt 생성은 one-shot Push/Flip command 기준이어야 한다.

주의:

- held Flip/Push 상태를 매 tick fake attempt로 해석하면 animation/audio가 반복된다
- fake hold 중 input gate는 pending action buffers를 clear해 반복 fake trigger를 막는다
- fake hold 종료 후 키가 계속 눌린 상태에서 새 input edge가 들어오는지는 input action semantics에 따른다

## 8. Extension Rules

새 action attempt kind를 추가할 때:

- `PlayerActionAttemptFeedbackKind`를 action-specific success/failure lifecycle로 확장하지 않는다
- actual lifecycle이 있으면 `PlayerActionSignals`, fake failure면 `PlayerActionAttemptSignals`를 사용한다
- movement consume은 simulation plan phase에서 결정한다
- host playback duration은 host-local hold로 둔다
- audio는 fake failure moment만 추가하고 lifecycle moments를 합성하지 않는다

새 feedback kind를 추가할 때:

- `TickPresentationData` enum
- attempt classifier
- action audio moment mapping
- audio profile coverage
- host mapper state preservation
- tests

위 항목을 함께 갱신해야 한다.

## 9. Regression Coverage

현재 주요 coverage:

- Free2D Push/Flip no target with held move
- Free2D ActionAssist out of range with nearby target
- Free2D ActionAssist success keeps queued action / align flow
- Grid Push no target with held move
- Push/Flip success keeps actual action signal and has no fake signal
- actual blocked keeps blocked action signal/audio and has no fake signal
- moving same-face Push/Flip consumes kinematic continuation
- fake Push/Flip animation hold survives next tick Walk/Idle
- fake hold transitions windup to recovery without `PlayerActionSignals`
- real action start, death, new fake attempt interruption behavior
- fake hold active input gate suppresses held move command
- actual Flip playback hold suppresses held move commands after action completion while world ticks continue, then admits input after playback ends
- Push/Flip press followed by Down at timestamp gaps of 5, 20, or 50 ms preserves the facing-side actual target within one input update; Flip also covers separate input updates, and a no-target Flip keeps its fake visual facing
- `Player_S1` playback after the first tick covers Down at 5, 20, and 50 ms for actual Flip, plus 50 ms actual Push and fake Flip; it checks action/attempt signals, box result, model or root facing where the phase permits it, and held-move suppression
- Flip playback release admits the held direction and the player eventually moves, while every tick during the hold remains free of that move command
- fake attempt signal clears stale move buffer
- fake attempt audio maps to failure moments only

Validation record from commit preparation:

- pre-commit core hook passed on each final commit
- Unity core EditMode: `47/47`
- Unity core PlayMode: `2/2`
- final hook output: `ALL TESTS PASSED`
