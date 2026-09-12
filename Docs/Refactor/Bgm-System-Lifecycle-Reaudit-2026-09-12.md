# BGM System Lifecycle Re-audit

- Date: 2026-09-12
- Status: `BGM_LC_001_IMPLEMENTED_AND_VALIDATED`
- Method: source, scene, test, and architecture-document review followed by test-first implementation
- Validation: targeted EditMode/PlayMode, production-scene PlayMode, `./run_tests.sh core`, and `./run_tests.sh ui` passed on the implementation worktree

## 1. Scope

This audit re-examines the persistent BGM flow with independent reviews focused on:

- production scene transition lifetime;
- scene and stage request arbitration;
- comic-sequence playback suppression lifetime;
- transition capability documentation;
- current automated-test coverage.

Sections 2 through 6 preserve the pre-implementation audit snapshot: that review did not change runtime code, scenes, prefabs, ScriptableObjects, or audio assets, and its findings describe the defect state observed before remediation. Section 7 records the subsequently selected decision, implementation, and validation result.

## 2. Current Runtime Flow

```text
SceneBgmRequestSource --------------------+
                                          |
StageAudioRuntimeRequestSource -----------+--> BgmRequestRouter
                                                -> BgmFlowCoordinator
                                                -> IBgmPlaybackPort
                                                -> IAudioService
                                                -> AudioPlaybackService
                                                -> persistent AudioBgmChannel

GlobalAudioFlowBootstrap
  -> GlobalAudioFlowRoot (DontDestroyOnLoad)
  -> persistent AudioRuntimeRoot + BgmRequestRouter
```

The ownership separation is otherwise clear: scene and stage objects request BGM, the persistent flow root owns continuity policy, and the shared audio runtime owns playback and mixing mechanics. `FadeOutIn` executes sequentially on one BGM source. `Crossfade` remains unsupported and falls back to `FadeOutIn` or `Immediate`.

## 3. Findings

### BGM-LC-001: Stage request survives after its source scene is destroyed

- Severity: High
- Confidence: High
- Disposition: Closed by owner-token request leases; production-scene regression validated

`BgmRequestRouter` stores one request for each `BgmRequestSourceKind`. `Submit` overwrites a slot, but the router provides no withdraw, remove, or owner-release operation. The router lives under `GlobalAudioFlowRoot`, which survives `LoadSceneMode.Single` transitions.

Stage-backed gameplay submits `StageGameplay` with priority 300 during host initialization. Destroying the stage installer disposes presentation and campaign objects but does not remove its BGM request. On returning to the main menu, `SceneBgmRequestSource` submits `SceneDefault` with priority 100. The retained stage request therefore remains the selected request.

Production sequence:

```text
MainMenuScene
  -> submit SceneDefault(100)
UIAudioScene loaded with LoadSceneMode.Single
  -> submit StageGameplay(300)
MainMenuScene loaded with LoadSceneMode.Single
  -> stage objects are destroyed
  -> persistent router retains StageGameplay(300)
  -> submit SceneDefault(100)
  -> retained StageGameplay request still wins
```

Observable outcomes:

- a stage profile can continue or be selected again in the main menu;
- an explicit stage `None` slot leaves a priority-300 stop request, so the main menu can remain silent;
- a comic transition handoff does not solve the stale selection because a later submit recalculates from the retained request set.

Evidence:

- `Assets/_Features/Flow/Flow_Audio/Runtime/BgmRequestRouter.cs:69-92,156-169`
- `Assets/_Features/Flow/Flow_Audio/Runtime/GlobalAudioFlowRoot.cs:32-43,56-82`
- `Assets/_Features/Flow/Flow_Audio/Runtime/StageAudioRuntimeRequestSource.cs:20-32`
- `Assets/_Features/Flow/Flow_Audio/Runtime/SceneBgmRequestSource.cs:17-34`
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs:307-316,427-434`
- `Assets/_Features/UI/UI_Composition/Runtime/SceneTransitionCoordinator.cs:1572-1579`
- `Assets/Scenes/MainMenuScene.unity:228-245`

Required correction contract:

- every retained request must have an explicit lifetime owner or lease;
- scene/stage teardown must withdraw only the request owned by that source instance;
- withdrawal must immediately select and apply the remaining highest-priority request;
- stale teardown from an older owner must not remove a newer request of the same source kind.

### BGM-LC-002: Playback-suppression safety depends on upper-layer callbacks

- Severity: Medium
- Confidence: Medium-High
- Disposition: Conditional lifecycle weakness, not a normal-flow leak

The normal comic-sequence path is protected. Completion and cancellation converge on `EndFocus`, setup failures restore focus when acquisition began, overlay disable/destroy reports cancellation, and `EndFocus` releases the lease from a `finally` block.

The remaining weakness is defensive teardown. `ComicSequenceAudioFocusController` itself has no `OnDisable` or `OnDestroy` cleanup, while `BgmPlaybackSuppressionLease` is released only through explicit `Dispose` or `ReleaseWithoutRestore` calls.

If the focus controller is destroyed before the overlay cancellation callback reaches the coordinator, or if an abnormal teardown bypasses the callback, the router can retain its active suppression token. While that token remains active, subsequent BGM submissions update selection without playing it, and another suppression acquisition fails fast.

Evidence:

- `Assets/_Features/UI/UI_Composition/Runtime/ComicSequenceAudioFocusController.cs:24-31,45-68,116-130`
- `Assets/_Features/UI/UI_Composition/Runtime/ComicSequenceFlowCoordinator.cs:162-170,199-213,261-279`
- `Assets/_Features/UI/UI_Composition/Runtime/ComicSequenceOverlayView.cs:959-979,1008-1020`
- `Assets/_Features/Flow/Flow_Audio/Runtime/BgmRequestRouter.cs:82-105,140-153,173-208`

Required hardening contract:

- focus-owner disable/destroy must idempotently release an owned lease;
- cleanup must preserve the distinction between restore and accepted-transition no-restore;
- direct or accidental `BeginFocus` re-entry must fail before mutating focus state;
- a lifecycle integration test must use the real focus controller and router rather than only a fake focus owner.

### BGM-LC-003: Source kind and priority can contradict each other

- Severity: Low
- Confidence: High
- Disposition: Latent API contract weakness

`BgmFlowRequest` accepts `SourceKind` and `Priority` independently. The public factory therefore permits combinations such as `SceneDefault` with `StageGameplay` priority even though the architecture documentation states that the router owns the fixed priority mapping.

Current production callers use the correct pairs, so this is not a presently observed production failure. It remains a contract-drift risk. Equal priorities also have no semantic tie-break; the current selection keeps the first candidate encountered during dictionary enumeration.

Evidence:

- `Assets/_Features/Flow/Flow_Audio/Runtime/BgmRequestRouter.cs:7-17,43-60,156-169`
- `Assets/_Features/Flow/Flow_Audio/Runtime/SceneBgmRequestSource.cs:30-33`
- `Assets/_Features/Flow/Flow_Audio/Runtime/StageAudioRuntimeRequestSource.cs:23-32`

Recommended contract:

- derive priority inside the router from `BgmRequestSourceKind`, or validate the pair at request construction;
- define a deterministic tie policy before adding another source kind.

### BGM-DOC-001: Accepted ADR conflicts with the implemented FadeOutIn capability

- Severity: Medium
- Confidence: High
- Disposition: Confirmed documentation-governance contradiction

`ADR-003-Persistent-Bgm-Ownership-Implementation-Gate.md` remains `Accepted` and says that v1 must not claim fade/crossfade completion or mistake the fade roadmap for current capability. `Bgm-Flow-V1-Guidelines.md` identifies itself as the active supporting truth-source and says that `Immediate` and `FadeOutIn` are executed v1 modes and that true single-source `FadeOutIn` is supported.

The runtime implements the latter behavior. Only the Crossfade limitation is consistent across both documents. Neither document nor the architecture index marks ADR-003 as historical or superseded.

Evidence:

- `Docs/Architecture/ADR/ADR-003-Persistent-Bgm-Ownership-Implementation-Gate.md:3,33-40,68-72`
- `Docs/Architecture/Bgm-Flow-V1-Guidelines.md:3-5,83-110,124-130`
- `Docs/Architecture/README.md`
- `Assets/_Shared/Audio/Runtime/AudioPlaybackService.cs:273-400`

Required documentation correction:

- retain ADR-003 as a historical implementation gate and explicitly mark its capability wording as superseded; or
- amend the ADR to distinguish the original gate state from the current implemented v1 state.

## 4. Test Coverage Assessment At Audit Time

Existing tests cover:

- coordinator no-op, restart, replacement, stop, and transition mapping;
- Crossfade warning and fallback;
- bootstrap, registry, and persistent-root reuse;
- `SceneDefault` to `SceneDefault` continuity;
- low-level sequential fade mechanics and interruption;
- Master/BGM volume and mute composition;
- Stage profile and explicit-none request submission;
- basic suppression restore, no-restore, concurrent rejection, and stop failure.

Missing regression scenarios:

1. production `UIAudioScene -> MainMenuScene` transition after a stage profile request;
2. production transition after a stage `None` request;
3. owner-aware request withdrawal and restoration of the next request;
4. stale owner withdrawal after a newer same-kind request has been submitted;
5. real comic focus-controller destruction and disable ordering with the real router;
6. `BeginFocus` re-entry before state mutation;
7. invalid source-kind/priority pairing and deterministic tie behavior.

Relevant existing suites:

- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Core/BgmRequestRouterCoreTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/BgmFlowRuntimeTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/PlayMode/PersistentBgmFlowPlayModeTests.cs`
- `Assets/_Features/Gameplay/Gameplay_Tests/PlayMode/AudioRuntimePlayModeTests.cs`
- `Assets/_Features/UI/UI_Tests/EditMode/ComicSequenceFlowTests.cs`

The current cross-scene PlayMode coverage uses scene-default requests and does not exercise the stage-to-menu priority lifetime. Documentation tests assert required phrases independently and therefore do not detect the ADR/guideline contradiction.

## 5. Remediation Order

1. Add owner-scoped request submission and withdrawal to `BgmRequestRouter`.
2. Bind scene and stage requester teardown to that withdrawal contract.
3. Add Stage Profile and Stage None return-to-menu regression tests.
4. Resolve ADR-003 versus current-guideline capability status.
5. Add defensive focus-controller lease cleanup and re-entry protection.
6. Internalize or validate source priority mapping.

## 6. Validation Gate For A Future Fix

A fix should not be reported as complete until all of the following pass on the same revision:

- targeted router unit tests for owner-scoped submit/withdraw semantics;
- production-scene PlayMode coverage for Stage Profile and Stage None return to main menu;
- real comic focus/router lifecycle tests for normal completion, cancellation, disable, destroy, and accepted-transition handoff;
- `./run_tests.sh core`;
- the relevant UI lane because comic focus and main-menu composition are involved;
- a broader PlayMode lane when claiming cross-scene lifecycle closure.

Until those tests run and pass, use `investigated` or `fix implemented` wording rather than claiming cross-scene regression closure.

## 7. Selected Remediation Decision

Decision: `OWNER_TOKEN_SINGLE_SLOT_REQUEST_LEASE`.

The selected first implementation should preserve the current one-slot-per-`BgmRequestSourceKind` arbitration model while adding explicit request ownership and withdrawal.

Proposed contract:

```text
request lifecycle owner
  -> Acquire(BgmFlowRequest)
  -> receives BgmRequestLease(source kind, generation token)

request lifecycle ends
  -> BgmRequestLease.Dispose()
  -> router removes the slot only when its current token still matches
  -> router re-evaluates the remaining valid requests
```

The lease must be idempotent. Profile and Stop claims must use the same ownership contract. A later request of the same source kind replaces the earlier slot and receives a new monotonically increasing token. Disposal of the older lease must then be a no-op, so delayed teardown from an old scene cannot remove a newer scene's request.

Example:

```text
Stage A acquires StageGameplay with token 41
Stage B acquires StageGameplay with token 42
Stage A is destroyed late and disposes token 41
router sees current token 42 and preserves Stage B
```

### 7.1 Ownership Placement

`SceneBgmRequestSource` should own the `SceneDefault` lease because the meaning of that claim is "this scene requests this default BGM while this scene exists." The first implementation should acquire during the existing startup path and dispose idempotently from `OnDestroy`.

`OnEnable` / `OnDisable` ownership is not selected for the first implementation. It would align the claim with component activation more strictly, but it would also redefine temporary GameObject deactivation as a BGM policy withdrawal and require explicit reacquisition behavior. The confirmed defect is tied to scene destruction, so `Start` / `OnDestroy` is the smaller and less surprising lifecycle change.

`StageAudioRuntimeRequestSource` should remain a stateless translator. It knows how to convert `StageAudioResolvedData` into a Stage Profile or Stop claim, but it does not own or observe the Stage session lifetime. Its `Apply` operation should return the acquired lease.

`StageBackedGameplaySceneInstallerBase` should hold that lease because it observes both successful host initialization and destruction of the Stage scene owner. It should dispose the lease during `OnDestroy`. If initialization can replace an existing Stage claim on the same installer, the new lease should be acquired before the old lease is disposed; token matching then prevents transient fallback and prevents old cleanup from deleting the new claim.

### 7.2 Empty-selection Playback Policy

The selected initial policy is:

```text
no valid request
  -> clear router selection
  -> do not immediately stop the current playback

explicit Stop request
  -> stop BGM
```

This distinction preserves cross-scene continuity. During `LoadSceneMode.Single`, the old requester's `OnDestroy` can run before the destination requester's startup. Immediately stopping when the last lease is disposed would introduce a stop/restart or fade churn during that temporary gap, including when both scenes request the same profile.

This policy does not make ownerless playback authoritative. It treats the absence of a claim as "do not make a new playback decision during handoff," while an explicit Stop claim remains the authoritative request for silence. It relies on the canonical-scene contract that every destination submits either a SceneDefault claim or a StageGameplay Profile/Stop claim.

Required safeguards:

- scene contract tests must verify that every canonical destination submits a BGM claim;
- actual `LoadSceneMode.Single` PlayMode tests must verify same-profile continuity and different-profile replacement;
- a missing destination claim must be observable through diagnostics rather than silently accepted as a valid steady state;
- if strict stop-on-empty semantics become necessary, introduce an explicit scene-transition handoff transaction instead of reintroducing stale claims.

### 7.3 Why This Option Was Selected

The owner-token lease was selected because it resolves both the lifecycle defect and delayed-cleanup race without changing the established priority policy or persistent playback ownership.

Trade-off summary:

| Option | Benefit | Primary cost or failure mode | Decision |
|---|---|---|---|
| Change SceneDefault priority | Very small change | Moves the stale-request failure from Stage-to-Menu to Main-to-Stage | Reject |
| `Clear(SourceKind)` on destroy | Small implementation | An old owner's late cleanup can delete a newer same-kind request | Reject except emergency hotfix |
| Clear requests from scene-transition code | Central operation | Couples audio policy to every scene-load path; failure/additive/direct-load paths can bypass it | Reject as primary fix |
| Recreate router per scene | Automatically drops stale state | Breaks persistent ownership, suppression, and no-restart continuity | Reject |
| Scene generation/epoch | Strong scene-wide invalidation | Requires a shared transition authority and complicates additive or persistent claims | Defer |
| Store all claims and arbitrate by priority/recency | Best multi-owner extensibility | Introduces tie, fallback, resurrection, and diagnostics policies not required by the current product | Defer until multi-owner demand exists |
| Owner-token, single slot per kind | Fixes lifecycle and stale teardown while preserving current semantics | Requires every production caller to retain and dispose its lease | Select |

The selected option is intentionally extensible: the token and lease types can remain if the router later changes from one slot per kind to a multi-claim collection.

### 7.4 Why The Lifecycle And Priority Questions Matter

The clarification that prompted this decision was necessary to distinguish three concepts that the current API makes easy to conflate:

1. A call to `Submit` looks like a transient signal, but the router stores it as a persistent claim.
2. The requester is scene-scoped, while the stored claim and router are application-scoped.
3. Priority chooses a winner only among valid claims; it cannot determine whether a claim is still valid.

This distinction explains why changing priorities is not a correction. With `StageGameplay > SceneDefault`, the stale Stage claim breaks Stage-to-MainMenu. With `SceneDefault > StageGameplay`, the stale MainMenu claim breaks MainMenu-to-Stage. The failure merely changes direction.

The user's questions therefore established the design criterion used here:

> Request validity must be resolved by lifecycle ownership before priority arbitration chooses among the remaining valid requests.

That criterion rules out priority tuning and global clearing, and directly supports an owner-scoped, token-checked lease.

### 7.5 Implementation And Verification Result

The selected design is implemented:

- `BgmRequestRouter.Acquire` and an idempotent `BgmRequestLease`;
- token-checked withdrawal and suppression-aware selection updates;
- lease storage and teardown in `SceneBgmRequestSource`;
- lease return from `StageAudioRuntimeRequestSource.Apply`;
- lease storage and teardown in `StageBackedGameplaySceneInstallerBase`;
- architecture guidance that describes requests as persistent claims rather than transient signals.

The production API no longer exposes the fire-and-forget `Submit` path. `Acquire` returns an idempotent lease tied to a monotonically increasing, non-zero owner token. Release removes a slot only when its token is still current. Profile and explicit Stop claims share this contract. Empty selection clears `ActiveRequest` without stopping playback, while explicit Stop remains authoritative silence. Suppression-time acquisition and withdrawal update selection without playback; normal release restores the latest valid selection, and `ReleaseWithoutRestore()` defers playback until the next acquisition.

Minimum regression matrix:

1. SceneDefault claim, then Stage Profile claim, then Stage disposal restores SceneDefault selection.
2. SceneDefault claim, then Stage Stop claim, then Stage disposal restores SceneDefault selection.
3. Same-kind A acquisition, B replacement, then late A disposal preserves B.
4. B disposal does not resurrect the replaced A claim under the selected single-slot policy.
5. Repeated disposal is harmless.
6. Suppression-time withdrawal updates selection without playback and restores only the latest valid selection when suppression ends.
7. Actual `UIAudioScene -> MainMenuScene` transition selects MainMenu BGM after both Profile and None stages.
8. Actual `MainMenuScene -> UIAudioScene` transition still selects StageGameplay.
9. Same-profile cross-scene continuity does not introduce an unwanted restart.

Validation executed from the repository runner on this implementation worktree:

- test-first red: the router/stage target failed to compile before `Acquire` and lease-returning `Apply` existed;
- targeted BGM EditMode: 145 passed, 0 failed;
- combined audio/documentation target: EditMode 153 passed, 0 failed; PlayMode 46 passed, 0 failed;
- production-scene persistent BGM target: 9 passed, 0 failed, including real `LoadSceneMode.Single` replacement and scene destruction;
- `./run_tests.sh core`: EditMode 263 passed, 0 failed; PlayMode 107 passed, 4 skipped, 0 failed;
- `./run_tests.sh ui`: EditMode 1356 passed, 0 failed.

The matrix above now covers Stage Profile and Stage None return-to-menu restoration, stale-token safety, idempotent disposal, suppression withdrawal, empty selection, same-profile continuity, different-profile authored `FadeOutIn`, and MainMenu-to-Stage priority. This closes BGM-LC-001 only. BGM-LC-002, BGM-LC-003, and BGM-DOC-001 retain their dispositions and are not expanded into this change.
