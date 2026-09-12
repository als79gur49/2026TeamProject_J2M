# Participant restart automation — isolated preflight experiments

> 이 문서는 당시 revision의 설계·관찰·검증 이력이다. 전시 초기화·재시작을 보존한 smoke·관측 구조 정리 후 현재 코드와 검증 상태는 [Exhibition Runtime Cleanup](../Architecture/Exhibition-Runtime-Cleanup.md)을 따른다. 아래 과거 명령과 삭제된 관측 도구는 현재 실행 절차가 아니다.


Target: `/mnt/d/J2M/worktrees/exhibition-reset`. The user's 2026-09-07 integrated proposal is the product design baseline. This implementation prepares its prerequisite experiments; it does **not** connect CompletedReset to the production reset coordinator/service or declare automatic Steam restart ready for deployment.

Proposed next corrections after the BuildID `25184112` native probe failure and independent re-review: [Steam 재시작 선행 실험 수정안](Participant-Restart-Probe-Correction-Plan.md). This is an unimplemented correction plan; the implementation and validation history below remains historical evidence.

## Boundaries

- Existing reset diagnostics, journal schema/mapping, product `IParticipantRestart`, GameOnly helper and Editor restart behavior remain in place.
- `Game.Exhibition.RestartExperiment` owns a separate handoff contract and a cycle runner. The same C# sources execute in Unity fake tests and the diagnostic PowerShell helper. There is no second, test-only copy of the state machine.
- `RestartPurpose.GameOnly = 0`; unknown purposes are rejected. `StartHandoff(identity, purpose)` accepts an identity retained across retry. Accepted precedes RequestExit and is not child success. Only definite non-creation permits retry. Changed, logging and menu/exit failures do not undo accepted/uncertain state.
- Normal builds have no diagnostic hook. The experiment define alone does not initiate anything. Editor, batch, Local, absent journal and Pending never install the live hook. Old Ready identity does not pin the account; the explicit experiment captures the current identity once.
- The hook does not invoke reset, progress deletion, Clear/Set/Store, account switching or `SteamAPI_RestartAppIfNecessary`. The already-running game's ordinary startup remains its existing behavior; this is not a claim that the entire game is a read-only SDK session.
- An armed diagnostic menu hides the destructive reset action. Actual trial input is Ctrl+Shift+F10 in a ready menu. Handoff then blocks the menu through the existing port/status popup. On definite preflight/non-creation failure its Restart action retries the same captured context; accepted/uncertain handoff cannot create another helper.
- The existing popup disables both buttons while busy. If the accepted/uncertain parent remains open, use the Windows close button/Alt+F4 and the external helper's recovery message. A separate in-game exit-only failure presentation belongs to the later product UI integration, not this prerequisite tool.
- Child launch gets only provider plus observation arguments. It cannot re-arm the trial. Observation links do not change Ready startup or become a restart journal.

## Build preparation

From WSL, with Unity closed for this worktree:

```bash
python3 Tools/Exhibition/Build-RestartExperiment.py --run-id <unique-id>
```

The script requires a resolved project below `/mnt/d/J2M/worktrees/`, uses the existing Windows release build CLI, temporarily adds `J2M_PARTICIPANT_RESTART_EXPERIMENT`, and verifies exact restoration of ProjectSettings. It preserves pre-existing generated Addressables companion files. Output is `/mnt/d/J2M/builds/participant-restart-preflight/<unique-id>/.staging-experiment/VectorQuake.exe`; build evidence is under `/mnt/d/J2M/evidence/participant-restart-preflight/<unique-id>/build`.

The postprocessor copies the shared C# sources, `Restart-Experiment.ps1` and an all-disabled prerequisites example into `RestartExperiment/`. Normal output refuses stale experiment artifacts. Distribution allowlists are not widened: this raw internal test artifact requires separate diagnostic packaging for Steam upload, as recorded below. Build preparation never installs, uploads, starts a player, or closes Steam.

The source/byte manifest includes uncommitted changes and the pinned SDK DLL. Existing diagnostics test results are historical, not evidence for this implementation.

## Operator prerequisites and trial selection

Actual game/Steam experiments must be scheduled with the user. A new diagnostic binary must be available at the actual Steam launch entry before lineage evidence can be collected. Installation replacement and upload are separate operations; this tool does neither. A desktop PowerShell launch or existing old diagnostic build cannot establish the new helper's Steam launch lineage.

Copy `RestartExperiment/prerequisites.example.json` to a new evidence directory. This file is an **operator review attestation**, not an automatically verified result or resume journal. Default booleans are false. Evidence paths must refer to nonempty files under `D:\J2M\evidence`. Do not mark a gate passed just to enable the next trial.

| Field | Required evidence before enabling |
| --- | --- |
| `SteamExeSha256`, `ShutdownCommandVerified` | On the target Windows installation, verify that the fixed candidate `steam.exe -shutdown` requests normal client exit. Record exact client hash, PID/start time and actual exit; command return is not client exit. No force close. |
| `GateAEvidence` | Actual Steam-launched game's helper survives parent exit and does not prevent normal client exit; include Steam gameprocess log excerpts and PID/start-time observations. |
| `DllSha256` | Built DLL equals pinned SDK165 x64 DLL (`8de54d32508e216c9135b8bf025749243d44e404c1c22a8e5fe35acecabe7a9c`). |
| `ProbeContractReviewed` | SDK165 InitFlat/export signatures, architecture and successful session cleanup reviewed. |
| `FailedInitExitReviewed` | Review the failed-Init cleanup policy: no further SDK calls or DLL unload; end the owned probe process. Only NoSteamClient(2), after verified normal process exit and valid output, allows another fresh observation within the original deadline. The legacy `FailedInitShutdownReviewed` field does not satisfy this gate. |
| `CallbackPumpReviewed` | Review the candidate's single RunCallbacks call with no registered handlers and synchronous identity queries. |
| `GateBEvidence` | Probe Init/query/Shutdown/exit, deadline supervision and Steam registration/release observations. SDK Shutdown return alone is insufficient. |

The installed App 5218360 already supplies `-j2mPlatformProvider steam` through its Steam launch configuration. Its **user launch options** must contain only the additional trial options below. Supplying the provider again produces a conflicting selection and prevents Steam initialization. For another launch entry, first inspect the effective command line; it must contain exactly one provider selection in total.

```text
-j2mRestartExperiment Survival -j2mRestartPrerequisites "D:\J2M\evidence\<run>\prerequisites.json"
```

This only arms the menu. Keep the quoted path on one line without inserting spaces into directory names. Press Ctrl+Shift+F10 deliberately after the menu is ready. The child does not inherit the trial options. Remove the trial launch options after the scheduled test. Do not create `steam_appid.txt`.

| Trial | Required gate | Actions after old game exits |
| --- | --- | --- |
| `GameOnly` | Valid original client and same exe | Same game exe once; Steam cycle and probe zero. Experimental shared lock is tested; the legacy production helper has not been migrated. |
| `Survival` (A) | Verified normal shutdown command | Request Steam exit once, observe actual client exit, log and end. No SDK, Steam restart or game launch. |
| `Probe` (B) | A evidence and all SDK review fields | Exit Steam once, restart same installation once, observe readiness using owned probes, end without launching game. |
| `FullCycle` (C) | A and B evidence | Same sequence as B, then launch same game exe once after clean probe exit. |

The helper is created with the existing adapter's `UseShellExecute=false`, `CreateNoWindow=true`, working directory and inherited environment. This does not assert detachment from Steam tracking. The helper loads SDK only through a separate probe process, after a new client exists. Environment policy v2 removes inherited Steam-prefixed variables (case-insensitive) for Steam command processes and the new-client probe/FullCycle game; only the latter two receive current SteamAppId/SteamGameId. GameOnly preserves the existing client environment and replaces those two AppID values. Environment evidence includes names/presence and AppID-match status only, never inherited values. Steam restart carries no AppID autorun argument.

If A fails through helper termination or a Steam shutdown deadlock, stop. Sleep/flag changes do not constitute proof of detachment. An external launcher ownership design requires a separate change.

## Process supervision and abort behavior

- The named mutex scope is OS user SID + Windows session + authentication/logon LUID + canonical Steam exe path. Its name contains only a hash, with no SteamID/operation ID. Both experimental purposes use this scope. Acquisition is nonblocking; contention, access errors and abandoned ownership abort before Steam/game actions.
- Parent identity includes PID and UTC StartTicks. PID reuse means the original process exited. Steam identity additionally pins canonical exe path/hash, user and session. Another client/replacement, account readiness mismatch or a same-exe manual game instance cannot cause an extra cycle.
- The owned shutdown command retains its original Process/handle. Only its exact PID/start-time is excluded from client enumeration. The command and original client share one 60-second deadline, including command creation; both must exit before Survival completes or a new Steam is requested. The command exit code is observed even while the original client remains alive. Nonzero exit, uncertain ownership or replacement aborts; disposing the command handle never kills it.
- Parent exit: 30 seconds. Steam exit: 60 seconds. New-client readiness: a single 120-second monotonic deadline. One probe gets at most 10 seconds including launch/compilation, capped by remaining readiness time. Successful-Init, clean-Shutdown not-ready results and verified NoSteamClient(2) or wire3 GlobalUserConnectionUnavailable process-exit results are re-observed after one second in a fresh process. Failed Init never queries, calls Shutdown or unloads its DLL; the owned process exits first. Other Init errors remain fatal. Qualified failed-Init results with query data, Shutdown, errors, wrong identity, nonzero process exit, incomplete output or timeout cannot be retried. Fatal result/timeout/cleanup failure ends the entire run; no client-cycle retry.
- Before SDK Init, the probe waits on stdin for the request nonce. The supervisor first assigns it to an owned kill-on-close Job Object, retains the actual process handle, then grants execution. Failed assignment denies Init. Unexpected supervisor exit closes the job; only the probe job is targeted, never Steam or another app.
- Probe stdout must contain one complete JSON result with required fields, matching nonce/PID/start-time and no fatal error. Ready requires successful Init and returned Shutdown; NoSteamClient(2) and wire3 GlobalUserConnectionUnavailable are only not-ready observations and must have no Shutdown or query data. The supervisor additionally requires normal exit and stderr accepted by the strict SDK165 output policy below. LoggedOn + actual AppID + current trial SteamID must match for readiness. No readiness claim comes from PID/window/login registry/fixed delay.
- Timeout terminates only the supervisor-owned probe handle and waits up to two seconds to confirm exit. Pipe-drain failure is fatal (one-second drain allowance). These cleanup allowances and OS call scheduling are separate from the 120-second readiness budget; it is not a hard bound on all possible OS I/O.
- Every costly observation is followed by a deadline check, including exited/ready observations. Nonpositive Delay is rejected. Probe and FullCycle launch preparation use the same monotonic clock and absolute deadline through the final Process.Start boundary, after identity/hash/environment/evidence work. GameOnly preserves its existing timing policy. This does not eliminate arbitrary external launch races or bound synchronous OS call return time.
- The cycle owns cleanup and one terminal HelperCompleted/HelperFailed recording while holding the common lock. Recording failures cannot bypass finally release. PS host reports bootstrap/cycle errors and shows the popup after release; it does not write a second cycle terminal result.

## Evidence and acceptance

Runtime evidence: `D:\J2M\evidence\participant-restart-preflight\<UTC>-<nonce>`. `request.json` freezes this trial's identity and targets. `events-<pid>.jsonl` records observer/parent/Steam/child identities, UTC, stage and monotonic elapsed time where available. `probes.jsonl` records successfully parsed native observations. `probe-attempts.jsonl` records every launch attempt, including uncertain creation, PID/StartTicks and exit code when available, bounded stdout/stderr, completion/truncation flags, parse/identity status, and primary/cleanup/collection/record errors after cleanup. `ReadyObserved` denotes only that probe's clean SDK/process readiness observation: it is not the cycle's final 120-second acceptance, HelperCompleted, Steam tracking release or Gate B success. The cycle checks its readiness deadline again after attempt recording. `probe-record-failures.jsonl` is an independent fallback for attempt-record failure; if that also fails the host error path retains the combined failure. Unknown values remain null/Unknown, never fabricated zero success values. None of these files drive automatic retries or resume. ChildMenuReady is a separate event after the new game's normal menu initialization. The request's OperationId is observation-only; no policy reads it on a later startup.


### Diagnostic wire v3

PS host, all three shared C# files and tests are deployed as one unit. All `ProbeObservation` fields are required, including nullable diagnostic strings. `WireVersion` must equal `3`; required `InitDisposition` must match the supervisor's recomputed classification; legacy, partial, trailing/multiple JSON and inconsistent results fail closed. Product reset journal schema is unchanged.

| Fields | Contract |
| --- | --- |
| `Nonce`, `Pid`, `StartTicks` | Must match the owned process and request. |
| `InitCalled`, `InitReturned`, `InitResult`, `InitDiagnostic` | Init must return; explanation is informational, bounded by the SDK165 1024-byte buffer (at most 1023 bytes decoded). No fabricated explanation for native fatal. |
| `QueryCalled`, `QueryReturned`, `AppId`, `SteamId`, `LoggedOn` | Successful Init requires returned query; readiness requires requested identity and login. |
| `ShutdownCalled`, `ShutdownReturned` | Successful Init requires attempted and returned Shutdown. Failed Init permits neither query nor Shutdown nor DLL unload. |
| `Error`, `FailureStage`, `QueryError`, `ShutdownError`, `CleanupError`, `RecordError` | Any nonempty field rejects both readiness and reobservation. First error remains primary; later errors remain separately observable. |

Clean NoSteamClient(2) has no query data/calls, login false and no Shutdown. Its `InitDiagnostic` does not itself cause rejection. SDK165 code1 with the exact Ordinal diagnostic `ConnectToGlobalUser failed.` is also eligible for bounded reobservation, with the same clean failed-Init requirements. Other failed Init results remain fatal. Output channels independently retain at most 16,384 characters while continuing to drain overflow; any overflow, reader error/noncompletion, unrecognized/ineligible stderr or abnormal/unconfirmed exit rejects the result. The two readers share one additional 1-second completion budget; owned termination confirmation shares one 2-second budget across recovery steps. There is no final unbounded task/process wait.

Retain Steam `gameprocess` logs separately. Observe helper survival, both old/new client identities, SDK tracking release, helper exit, child menu readiness, and disappearance of the running-game indication after child exit. A HelperAccepted or GameCreated event alone is not success. Missing logs mean insufficient evidence, not pass. External reports should mask account identifiers.

Automatic checks from the target worktree:

```bash
CODEX_VALIDATION_ROOT=/mnt/d/J2M/evidence/<run>/core ./run_tests.sh core
CODEX_VALIDATION_ROOT=/mnt/d/J2M/evidence/<run>/focused ./run_tests.sh full --filter 'RestartExperimentTests;ParticipantResetDiagnosticsTests;ParticipantResetServiceTests;SteamExhibitionResetProtocolTests;ExhibitionResetCoordinatorTests;FileExhibitionResetJournalTests'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'D:\J2M\worktrees\exhibition-reset\Tools\Exhibition\Tests\Restart-Experiment.Tests.ps1' -RepositoryRoot 'D:\J2M\worktrees\exhibition-reset'
```

NUnit covers monotonic handoff, retained retry identity, exact per-trial counts/order, lock/reentry, manual launches/replacement, deadlines, fatal probe failures and artifact opt-in. Windows tests compile the exact helper sources, query only their own process/token, start harmless fake PowerShell probes, and test real result pipes, timeout termination, mutex contention, PID reuse and guarded script entry. They do not run Steam/native SDK/game processes.

UI lane results, when run, are regression evidence for the shared menu port surface; they do not exercise the define-only player hook. Manual diagnostic keyboard/popup behavior still requires Windows validation. Editor Domain Reload on/off and two-Play lifecycle remain product-stage work; no Windows result substitutes for them.

Product readiness requires later integration tests for canonical Ready completion, B services/save/reconciliation/MenuReady all zero, two game replacements, legacy Pending compatibility and retry deletion/identity-read zero. Repeated reset/re-earn and API-unearned plus Overlay 0/5 evidence are explicitly outside these non-destructive prerequisite trials. Full regression is not implied by a filtered full command.

API references: [Steamworks SDK initialization/shutdown](https://partner.steamgames.com/doc/sdk/api), [BLoggedOn](https://partner.steamgames.com/doc/api/ISteamUser#BLoggedOn). These do not guarantee Overlay list refresh.

## 2026-09-08 preparation results

Final source hashes were unchanged across the following checks. Evidence root: `/mnt/d/J2M/evidence/participant-restart-preflight/20260908-implementation`.

| Check | Result | Evidence beneath root |
| --- | --- | --- |
| `./run_tests.sh core` | EditMode 254 passed; PlayMode 107 passed, 4 graphics-only skips | `verified-core/test-results` |
| Filtered `full` command above | EditMode 95 passed (35 new experiment cases); PlayMode selected 0, not lifecycle evidence | `verified-focused/test-results` |
| `./run_tests.sh ui` | EditMode 1,359 passed | `verified-ui/test-results` |
| Windows helper/process tests | 16 passed, including watchdog timeout and abrupt supervisor exit; no Steam SDK Init or game/Steam-client execution | `windows-fakes-complete.log` |
| Storage audit | `j2m-worktree-audit` passed | No new worktree created |
| Final diagnostic build | Succeeded, 0 errors, 1 Unity native-symbol upload authentication warning | Separate build evidence below |

The initial core attempt exposed an unavailable Unity `WindowsIdentity` assembly reference. It was replaced with direct token/SID queries and both Unity and Windows tests were rerun. Earlier intermediate test/build artifacts remain history; the final paths above are the evidence used here.

Final build folder: `/mnt/d/J2M/builds/participant-restart-preflight/20260908-preflight-final/.staging-experiment`.
Build evidence: `/mnt/d/J2M/evidence/participant-restart-preflight/20260908-preflight-final/build`.

Verified the player contains the opt-in hook, all shared helper sources match repository bytes, the original GameOnly helper is byte-identical, the SDK DLL matches the pinned hash, and no `steam_appid.txt` is present. ProjectSettings restoration is byte-exact. Existing diagnostic service/protocol/adapter and their checked tests match the prior diagnostic source hashes; the startup composition additionally wraps the service only under the experiment define in a Windows player.

**Not run:** actual Steam-launch Gate A/B/C, native SDK probe lifecycle/tracking, diagnostic keyboard/popup manual tests, Editor two-Play/Domain Reload matrix, destructive reset/re-earn/Overlay comparison, and the unfiltered full lane. Actual Steam trials await user scheduling and installation of this diagnostic artifact at the real Steam launch entry. At preparation time, installation, SteamPipe upload and branch activation were not performed; the later upload is recorded below. No gate attestation was enabled. This is a prepared prerequisite experiment, not proven Steam restart automation or product deployment readiness.

## SteamPipe diagnostic upload (2026-09-08)

At the user's request, rebuilt the current working copy and uploaded it to AppID `5218360`, DepotID `5218361`: **BuildID `25170814`**, depot manifest `2029142350306607251`. SteamCMD exited 0 and both app/depot logs report success. `SetLive` was omitted; branch activation and installation were not performed.

Fresh build: `/mnt/d/J2M/builds/participant-restart-preflight/20260908-steam-preflight/.staging-experiment`. The build succeeded with 0 errors and 1 native-symbol upload authentication warning. ProjectSettings restoration was byte-exact. Current exhibition source hashes matched the previously validated snapshot; validation lanes were not rerun for this unchanged-source upload.

The standard stager validated 252 base runtime files. Its allowlist intentionally omits the experiment directory, so a separate diagnostic payload copied that validated base and added exactly the five build-generated `RestartExperiment/` files listed in `prepare-payload.py`. The production stager policy and its base manifest were not modified. The final diagnostic manifest covers all 257 files (381,579,493 bytes), including the explicit additions, with SHA-256 `ef45917726535f37a7a18f9cb372c8c47177137228d450a010a2b37025d5b144`. Every payload hash matched before and after upload. The player hook, pinned SDK DLL, unchanged original helper, absent `steam_appid.txt`, and disabled prerequisite attestations were verified.

Upload payload: `/mnt/d/J2M/builds/participant-restart-preflight/20260908-steam-preflight/steam-upload/payload`.
Build/upload evidence and reproduction scripts: `/mnt/d/J2M/evidence/participant-restart-preflight/20260908-steam-preflight`, with final result at `upload/steam-upload-result.json`.

This upload makes the opt-in prerequisite experiment available as a Steam build. It does not enable automatic Steam restart after reset or establish any actual Steam/Overlay gate result. The manual/non-run items above remain outstanding.

## Survival host fixes and additional review (2026-09-08)

The operator's normal shutdown command check succeeded in `shutdown-check-20260908-020051`. This verifies the command on the recorded client hash, not helper survival. Two subsequent real Steam-launched Survival attempts stopped before requesting Steam shutdown:

- `20260907T171454090Z-e5cbecf1d1524ec7a1036f5b90efc25f`: cold PowerShell could not resolve `DataContractJsonSerializer`. Compiler references alone did not load the runtime assembly for `New-Object`. Added explicit `Add-Type -AssemblyName System.Runtime.Serialization` and a fresh-process bootstrap regression covering both serializer constructors.
- `20260907T172142181Z-3d8e53b32c6d41d9b7c8a1a5af4ae121`: Unity recorded session 0 for the game and Steam, while Windows/PowerShell reported session 15 for the same Steam PID. User SID, logon LUID and Steam start ticks agreed. Identity capture and enumeration filters now use native `ProcessIdToSessionId` on both sides. Session checks remain enforced. A Unity test independently queries the same Unity PID from fresh Windows PowerShell and compares sessions.

Additional review fixes hide the reset action while the trial is armed, guard hook notification reentry, preserve elapsed time and last new-Steam/child identities in failure logs, and correct the user launch-options instructions to avoid duplicate provider arguments. The ordinary unavailable-Steam presentation and provider conflict guard remain unchanged. No Scene/Prefab assets changed.

Validation evidence: `/mnt/d/J2M/evidence/participant-restart-preflight/20260908-review-fixes`. Windows tests: 17 passed; focused EditMode: 76 passed (including the cross-runtime session test), focused PlayMode: 0 selected; core: EditMode 254 passed, PlayMode 107 passed with 4 graphics-only skips; UI: 1,359 passed. The unfiltered full lane, diagnostic player manual UI, actual Survival rerun, SDK lifecycle/tracking, Editor two-Play and reset/Overlay trials were not run. Source hashes stayed unchanged across validation and build.

Fresh build: `/mnt/d/J2M/builds/participant-restart-preflight/20260908-reviewed/.staging-experiment`; succeeded with 0 errors and 1 native-symbol upload authentication warning, byte-exact settings restoration. Both the player DLL and the copied helper contain the fixes. A full updated diagnostic payload was uploaded to AppID `5218360` / DepotID `5218361` as **BuildID `25172135`**. SteamCMD exit 0 and app/depot success logs were verified; no SetLive, branch activation or local installation was performed.

The base stager validated 252 files; the five explicit diagnostic additions bring the payload to 257 files, 381,580,956 bytes. Diagnostic manifest SHA-256: `1f17c23a5b4923ec3896024213ed8ad0e86f726aad1d4da6fe4b5f1960730a52`. Payload hashes matched before and after upload. Evidence: `/mnt/d/J2M/evidence/participant-restart-preflight/20260908-reviewed`, final result `upload/steam-upload-result.json`.

Install the new build as a whole before repeating Survival; the earlier local PS-only patch is insufficient for the native-session correction in the player. Keep the operator's verified shutdown receipt separate from the all-disabled distributed example. Neither failed attempt passes Gate A, and no A/B/SDK attestations were enabled by this upload.

## Observed Survival success and Probe preparation (2026-09-08)

On BuildID `25172135`, trial `20260907T174442675Z-b4a8386a10b942b5a9da1431ae2a31ed` recorded ParentExited, SteamExitRequested, OriginalSteamExited, SurvivalObservationComplete and HelperCompleted. Steam exited about 11.9 seconds after the request. Game, Steam and helper were absent at the follow-up check; Steam gameprocess and bootstrap logs were copied into the trial directory. This is one observed successful Survival run, not a general detachment guarantee or a Probe/FullCycle result.

Parent-side HelperAccepted identity logging still encountered Mono's empty `Process.Modules` collection immediately after child creation. It was isolated and did not cancel the handoff. Identity path capture now uses native [QueryFullProcessImageNameW](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-queryfullprocessimagenamew), followed by canonical-path verification, rather than MainModule. A Unity regression immediately captures five newly created harmless PowerShell children.

Local SDK165 headers and its redistributable DLL match the pinned game DLL; all eight required exports and their x64 ABI were reviewed. The public [Steam API documentation](https://partner.steamgames.com/doc/api/steam_api) and SDK165 SpaceWar Main.cpp do not establish the former failed-Init Shutdown assumption. The candidate was revised: failed Init makes the observation fatal, performs no subsequent SDK query/Shutdown or FreeLibrary, and ends the owned probe process. There is no automatic trial retry. Successful Init always attempts Shutdown, even when query/evidence recording fails. DLL unload is permitted only before Init or after returned Shutdown; otherwise process exit owns cleanup. This conservative failed-Init policy may stop early during client startup/login/update. It does not guarantee eventual readiness within 120 seconds.

The operator review field is now `FailedInitExitReviewed`; the legacy `FailedInitShutdownReviewed` field cannot enable Probe. The reviewed static contract, callback use and failed-Init policy are recorded in `/mnt/d/J2M/evidence/participant-restart-preflight/20260908-probe-preparation/sdk165-review.json`. Actual native lifecycle timing and Steam tracking release remain Gate B observations. No native SDK observation was executed during preparation.

Validation under `20260908-probe-preparation`: Windows tests 17 passed; focused EditMode 85 passed and focused PlayMode 0 selected; core EditMode 254 passed and PlayMode 107 passed with 4 skips. UI was not rerun because its source hashes are unchanged from the prior 1,359-pass UI run. Unfiltered full, manual diagnostic UI, actual Probe/FullCycle, Editor lifecycle and reset/Overlay trials remain unrun. Runtime source hashes matched through validation and build.

The rebuilt player and shared helper were uploaded together as **BuildID `25183512`**, AppID `5218360`, DepotID `5218361`. Build succeeded with 0 errors and 1 native-symbol authentication warning; settings restoration was exact. SteamCMD exit 0 and app/depot success logs were verified. No branch activation or local installation was performed. The diagnostic payload has 257 files, 381,583,139 bytes, manifest SHA-256 `950e0809e7db63fa79c5cd6833d89114c9861f97289a6dd6e0db717446222816`; hashes matched before and after upload. Evidence: `/mnt/d/J2M/evidence/participant-restart-preflight/20260908-probe-ready/upload/steam-upload-result.json`.

A separate operator configuration is prepared at `D:\J2M\evidence\probe-20260908.json`, linking the observed Survival result and the SDK review. GateBEvidence stays empty, so FullCycle remains blocked. Distributed examples remain all-disabled. After installing BuildID `25183512`, replace user Steam launch options with:

```text
-j2mRestartExperiment Probe -j2mRestartPrerequisites "D:\J2M\evidence\probe-20260908.json"
```

Start through Steam, verify the Probe banner, then press Ctrl+Shift+F10 once. Expected scope: game exits, Steam exits/restarts once, the owned probe observes readiness and exits; the game is not relaunched. Record any failure and do not repeat the client cycle automatically. This candidate is not product CompletedReset integration.

## Bounded NoSteamClient re-observation (2026-09-08)

Trial `20260908T093001394Z-3360147318894d469b6d9375b852129c` on BuildID `25183512` restarted Steam successfully but returned InitResult 2 (SDK165 NoSteamClient). Init ran about one second after Steam process creation, before the new client's gameprocess startup log. This supports a startup-timing explanation, not proof that timing is the only possible connection failure. The former all-Init-failures-fatal policy stopped immediately and the generic result error hid the reason. Gate B did not pass.

The updated policy allows only NoSteamClient(2) to return not-ready after the supervisor confirms normal owned-process exit, closed output, and matching nonce/PID/start time. Its result must have no query data, Shutdown or error. Every later observation uses a fresh process under the original 120-second deadline; Steam shutdown/start still occur once. There is no same-process Init retry. FailedGeneric, VersionMismatch, unknown result codes, identity/output errors, timeout and failed cleanup remain fatal. Error dialogs now display the innermost cause, while logs retain the full exception. Existing `FailedInitExitReviewed` cleanup and the operator configuration remain applicable; the user explicitly authorized bounded fresh-process re-observation. This supersedes the previous NoSteamClient-aborts-trial behavior only.

Validation: `/mnt/d/J2M/evidence/participant-restart-preflight/20260908-readiness-reobserve`. Focused EditMode 95 passed, focused PlayMode 0 selected; Windows process tests 18 passed, including waiting for a NoSteamClient producer to exit before a new successful observation; core EditMode 254 passed and PlayMode 107 passed with 4 skips. Unity UI was not rerun because its sources are unchanged. Manual external-dialog verification, actual Steam Probe rerun, unfiltered full, FullCycle and Editor lifecycle remain unrun. The final runtime source snapshot matches the build.

The rebuilt player/helper payload was uploaded as **BuildID `25184112`**, AppID `5218360`, DepotID `5218361`. Build: 0 errors, 1 native-symbol authentication warning; settings restored exactly. SteamCMD exit 0 and app/depot success verified. Payload: 257 files, 381,585,027 bytes; manifest SHA-256 `3ae7d68ca12d4aaf97f17dadf08094872a71e7faa51aec19d88b81295c9a1211`, unchanged before/after upload. No branch activation or local installation was performed. Result: `/mnt/d/J2M/evidence/participant-restart-preflight/20260908-reobserve/upload/steam-upload-result.json`.

After activating and installing BuildID `25184112`, keep the same Probe launch options and `D:\J2M\evidence\probe-20260908.json`. Press the diagnostic shortcut once and allow up to 120 seconds of readiness observation after Steam restarts. No automatic game launch is expected in Probe mode. FullCycle remains blocked until actual Gate B evidence is reviewed.


## Probe correction candidate prepared (2026-09-08)

The earlier BuildID `25184112` launch instructions above are historical. Trial `20260908T100335465Z-e1af2658b5ea41669b165c0ce6c46cb7` replaced Steam but ended in native fatal during Init; no Init return, readiness, Shutdown return or Gate B success was established. Do not repeat that unchanged build to validate this correction. Environment inheritance is confirmed in the old source, but its causal relationship to the fatal remains unproven.

Implemented the [correction plan](Participant-Restart-Probe-Correction-Plan.md): absolute deadline/positive-delay control, role-specific environment policy, bounded versioned SDK/process diagnostics, retained shutdown-command ownership, and terminal recording under the cycle lock. Existing diagnostic and UI changes were preserved. Product CompletedReset/reset/achievement writes and Editor replacement remain outside this change.

Candidate run ID: `20260908T103903Z-probe-correction`. Raw trial build: `/mnt/d/J2M/builds/participant-restart-preflight/20260908T103903Z-probe-correction/.staging-experiment`. Complete local diagnostic payload: `/mnt/d/J2M/builds/participant-restart-preflight/20260908T103903Z-probe-correction/steam-upload/payload`. No Steam BuildID has been allocated to this candidate.

Same-source validation:

| Check | Result |
| --- | --- |
| `./run_tests.sh core` | EditMode 254 passed; PlayMode 107 passed, 4 graphics-only skips, 0 failed. |
| Specified six-fixture filtered `full` | EditMode 159 passed; PlayMode 0 selected. This is touched-cluster evidence. |
| Windows helper tests | 46 passed using exact helper sources and harmless children/fakes; actual Steam/game/native API executions 0. |
| Independent source review | No unresolved P1/P2 finding identified; reviewed source hashes match the build inputs. |
| Existing trial builder | Succeeded, 0 errors, 1 warning: missing Unity Cloud token prevented native-symbol upload. |
| Distribution staging and diagnostic payload | 252 validated base files plus 5 diagnostic files; 257 total, 381,627,057 bytes. |

Evidence root: `/mnt/d/J2M/evidence/participant-restart-preflight/20260908T103903Z-probe-correction`. See `validation-summary.json`, `independent-review.md`, `validated-source-hashes.json`, `build/`, `player-metadata-check.json`, `diagnostic-payload-manifest.json`, and `final-verification.json`. The payload manifest SHA-256 is `0d4ab793dd16c0b910df918081c18df50849e46abc00b081e02f95ce3b08bc21`.

All 39 validated source files match builder inputs and current source bytes. All four installed-helper candidates match source hashes; the legacy GameOnly helper remains byte-identical. Metadata inspection confirms the compiled player contains the opt-in hook, new deadline interfaces and diagnostic fields without executing the player. The temporary define was restored byte-exactly. Distributed prerequisite booleans remain false, gate paths empty, and `steam_appid.txt` absent.

The initial staging invocation rejected missing `build-metadata.json`. Packaging then derived that file from this build's actual `intermediate.json`, annotated the dirty branch/diagnostic define, and copied the repository's exact two third-party notices. The standard stager subsequently passed. `packaging-inputs.json` records these additions; runtime files were not patched or mixed with another build.

**Not run:** UI lane (no UI changes from this task's captured baseline), unfiltered full lane, actual Editor Domain Reload/two-Play lifecycle, manual keyboard/popup validation, Steam/client/native Probe, upload, branch activation, installation, reset/re-earn and Overlay checks. The four core skips are graphics-only tests under the headless lane. This candidate is prepared for a later coordinated Probe, not a Gate B result or proof that the native fatal is fixed. After a separately coordinated full installation/hash check, the next actual test remains one explicit Probe from an already Ready game, with automatic game launch zero and no unchanged-build repetition after failure.


### 2026-09-08: bounded global-user connection observation (wire v3)

The installed BuildID 25186256 trial `20260908T115047559Z-57e0344da9a440d0be891877310fcc3f` restarted Steam, then observed clean NoSteamClient(2) followed by clean-process FailedGeneric(1), `ConnectToGlobalUser failed.`. The second Init returned at 20:51:05 KST; Steam connection logs completed login at 20:51:08. This differs from the earlier nonreturning native fatal. It does not establish the cause or guarantee a transient condition: [ConnectToGlobalUser](https://partner.steamgames.com/doc/api/ISteamClient#ConnectToGlobalUser) can fail for an invalid pipe or absence of a global user.

This revision supersedes the earlier rule that every FailedGeneric aborts immediately: only code1 plus that exact case-sensitive, untrimmed diagnostic is classified `GlobalUserConnectionUnavailable`. The production SDK165 DLL hash pin remains required. A clean result permits only a new owned probe under the same absolute 120-second deadline; it never establishes readiness. No additional Steam restart, same-process Init retry, query, Shutdown or DLL unload follows failed Init. Persistent failure ends at the original deadline. Any other error, incomplete/overflowed output, abnormal exit, identity mismatch or cleanup/recording failure still aborts. No existing wire2 evidence is reinterpreted.

Diagnostic wire3 requires `InitDisposition`: Succeeded=0, NoSteamClient=1, GlobalUserConnectionUnavailable=2, Fatal=3. Native and supervising code share the classifier, and the supervisor verifies the field against code and diagnostic. Missing, older or inconsistent results fail closed. Deploy all helper sources and PS host together with the player. Product reset journals are unchanged.

The failure popup is bounded to a concise message plus the evidence directory. Complete attempt diagnostics remain in `probe-attempts.jsonl` and nested exception logs. Gate B remains unproven; actual successful Init, identity query and Shutdown must still be observed in a later coordinated trial. No real Steam/probe run is performed by automated validation.


### 2026-09-08: successful SDK initialization stderr policy v1

Trial `20260908T122015805Z-1a9b835366d64c4cb84e1dfcace8eadc`, installed BuildID 25186818 (257 payload hashes matched), reached successful Init, matching AppID/SteamID and login, returned query/Shutdown and owned process exit 0 on attempt 4. Its complete JSON was not contaminated. The previous any-nonblank-stderr rule rejected the two SDK minidump initialization diagnostic lines emitted in that successful path.

This correction supersedes the blanket stderr rejection only for this exact SDK165 Windows output, with request AppID and SteamID substituted as invariant decimal numbers:

```text
Setting breakpad minidump AppID = <request AppID>\r\n
SteamInternal_SetMinidumpSteamID:  Caching Steam ID:  <request SteamID> [API loaded no]\r\n
```

The notation shows literal CRLF boundaries; there are exactly two lines, in this order, with the observed spaces and trailing CRLF. Production still pins the SDK DLL hash. No trimming, case folding, line removal, substring matching or additional output is permitted. Empty stderr remains eligible. Null, whitespace-only, partial, duplicated, changed, unknown or fatal stderr is rejected. The known pair is eligible only after the full SDK result validates as ready, including request identity, login and returned Shutdown, and after normal owned exit, complete bounded output and all other error checks. It does not permit reobservation after failed Init or incomplete identity/login. Other errors never receive an exemption.

Both channels remain fully captured under their existing 16,384-character limits and shared drain budget. `probe-attempts.jsonl` adds `StderrPolicyVersion=1` and `StderrDisposition` (NotEvaluated, Empty, Sdk165InitDiagnostics, Rejected); classification alone is not overall success. SDK observation wire3 and the product reset journal are unchanged. Deploy the player and all helper artifacts as one verified payload. A result-only channel would not by itself address the stderr acceptance rule.

The historical trial remains HelperFailed and is not retrospectively promoted to Gate B success. A new coordinated trial must separately confirm final helper completion/exit and Steam tracking release. Actual Steam/native execution is excluded from automatic validation.


## Reset / Overlay trial 실패 안내 보강

재시작 helper의 실패 팝업은 request/역할 미확정 bootstrap 실패도 포함해 증거 검토 전 게임 재실행이나 cycle 반복을 권하지 않는다. reset trial은 Pending이 이미 저장됐거나 일부 초기화가 적용됐을 수 있다. 일반 실행의 Pending 복구 동작 자체는 유지되므로, 필요한 수동 복구는 자료 검토 뒤 별도로 조율한다. [수정 계획](Reset-Overlay-Trial-Correction-Plan.md)과 [trial 절차](Reset-Overlay-Trial.md)를 참조한다.
