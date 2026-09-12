# Overlay handoff observation implementation

> 이 문서는 당시 revision의 설계·관찰·검증 이력이다. 전시 초기화·재시작을 보존한 smoke·관측 구조 정리 후 현재 코드와 검증 상태는 [Exhibition Runtime Cleanup](../Architecture/Exhibition-Runtime-Cleanup.md)을 따른다. 아래 과거 명령과 삭제된 관측 도구는 현재 실행 절차가 아니다.


> Historical implementation: 아래는 기존 Build25203840의 구현·검증 이력이며 새 최소안의 요구사항이 아니다. [필수 기능만 유지하는 수정안](./Overlay-Handoff-Observation-Correction-Plan.md)의 로컬 구현 및 검증 상태는 [최소안 구현 기록](./Overlay-Handoff-Observation-Minimal-Implementation.md)을 참조한다. 새 절차를 기존 설치본에 적용하지 않는다.

Latest workflow correction: **BuildID 25203840** uploaded; branch/installation/live trial pending. [Validation, upload and operator steps](/mnt/d/J2M/evidence/overlay-handoff-observation/20260909T080144Z-observation-workflow/upload/upload-summary.md).

2026-09-09 historical receipt correction: candidate uploaded as **BuildID 25202548**, AppID 5218360 / DepotID 5218361. Upload and post-upload payload verification succeeded. Branch activation, installation and actual Overlay comparison were not performed. [Upload evidence and next tests](/mnt/d/J2M/evidence/overlay-handoff-observation/20260909T063211Z-observation-reviewed/upload/upload-summary.md).

This opt-in diagnostic observes one GameOnly replacement without participant reset. The design contract is [Overlay-Handoff-Observation-Plan.md](./Overlay-Handoff-Observation-Plan.md). The old reset trial and Build25190245 do not implement this diagnostic.

## Startup and ownership

The diagnostic recognizes `-j2mOverlayHandoffObservation <config>` before configuration/filesystem composition. Batch observation is inhibited and explicitly rejected before participant composition. Invalid, duplicate, mixed and unsupported observation options remain blocked; they do not fall through to normal product startup. The effective command line must select exactly one Steam provider. Steam user launch options must not duplicate a provider already supplied by the application's Steam launch configuration.

The dedicated composition holds the existing participant session lock, reads the Ready record and participant file manifests, and constructs only the observation flow/runtime/panel. Product achievement startup/reconciliation, Steam publication/maintenance writes, campaign access and participant save mutation/recovery remain inhibited for the session. No reset coordinator or reset adapter is created by this composition. Existing native Init, callbacks and normal Shutdown keep their original owner.

`StartupAlreadyStarted` records ineligibility if the product/campaign/platform/native path already started. Prior history is not undone or relabelled as zero writes. Retained save stores and normal service/publication resume paths are also guarded after observation entry.

## Observation and handoff

- Roles are `OriginObserver` and `ReplacementObserver`. Child arguments are `-j2mOverlayHandoffContext` and `-j2mOverlayHandoffRequest`. The helper rejects mixed reset/observation requests, invalid versions/roles and context hash changes.
- Observation receipt production hashes the actual child executable before writing the receipt; the consumer checks that hash against its own identity. The reset receipt path keeps its existing capture policy.
- SDK and child receipt waits have separate monotonic 30-second budgets. Native-ready time fixes a single five-minute observation deadline per role; the Overlay readiness window ends after the first 30 seconds of that budget. Expiry prevents new sampling and handoff, including after expensive validation/claim writes and directly before helper creation.
- The existing GameOnly environment/launch/cycle implementation is reused. It waits at most 30 seconds for the original process to exit, then calls `StartGame(null)`. There is still no separate deadline for its subsequent environment/hash/client checks and game creation. FullCycle is not part of this diagnostic.
- `handoff.claimed.json` is single-use, including failed/partial claims. No retry follows claim consumption or uncertain creation. An accepted helper proceeds when the origin exits; closing the origin does not cancel that accepted handoff. If creation is confirmed but its evidence write fails, acceptance and the collection error remain separate; the origin still exits without retry.
- Both roles must record the panel request timestamp before a normal screen report can be accepted. This records UI intent, not the physical key time.
- Both roles must import an external OS PNG/JPEG capture before reporting `opened`. The operator selects the actual local file by copying its path; balanced Windows quotes, spaces, Unicode and double extensions such as `.png.png` are accepted. The application copies at most 32 MiB to a unique D evidence file without renaming or modifying the source. No manually assembled run path or manual evidence copy is required. Source path/read/format mistakes remain editable in the same run and never extend the original deadline. Destination evidence write/acceptance failures remain terminal.
- Import and report checkpoint operations exclude duplicate reporting and handoff. Results become available for handoff only after the report checkpoint completes and the deadline/cancellation guard passes. Late file copies cannot be accepted after expiry/cancellation; exit never waits indefinitely for a copy. The accepted D capture hash is rechecked before handoff. `not-visible` and `inconclusive` can be reported without a capture. API values, activation and module presence do not replace the user's report.
- A completed user report is not automatically a successful comparison. Final participant/SDK checks and matching helper terminal evidence remain required. Child process exit and release of Steam tracking must still be reviewed externally after exit.

## Evidence

Each run writes to `D:\J2M\evidence\overlay-handoff-observation\<UTC>-<runId>`. Each role has its own event stream and Steam log checkpoints. Context, original participant/SDK manifests, claim, helper request/creation identity and child receipt remain separate artifacts.

The existing Steam runtime owns one finite subscription. It performs real Overlay getters without smoke, retains false→true→false and query errors/recovery, and timestamps activation callbacks at receipt. API/frame snapshots are combined, at most once per second and 300 per role; activation events retain the first 512 and count drops. Each evidence string is capped at 2,048 characters. Disposed/expired subscriptions do not start more diagnostic queries; late returns are not fresh evidence. Normal callback pumping continues. A definite native callback fault is propagated to the observation flow immediately; a disposed subscription marks its last successful sample stale.

Participant JSON/backup file sets and SHA256 are compared before preparation, before handoff, after child preparation and before normal exit. Raw file reads avoid repository `Load()` methods that can restore backups or clean temporary files. Hash equality proves final bytes at checkpoints, not absence of intervening writes; boundary tests provide separate no-write evidence. The existing session lock and Unity/Steam logs/cache are outside that JSON/backup claim.

Steam log copies use only `gameprocess_log.txt`, `gameoverlay_ui.txt` and `gameoverlay_renderer.txt`: at most 2 MiB tail per file, five checkpoints per role, one outstanding filesystem job (including log-directory creation), and at most one extra second of checkpoint waiting. Copy metadata records bytes, timestamps, truncation, failures and renderer PID eligibility. Outstanding reads cause skipped checkpoints, never an unbounded exit wait. Optional Steam log failure does not invalidate SDK readiness or justify a hook diagnosis.

## Build and operational boundary

`Tools/Exhibition/Build-RestartExperiment.py --overlay-observation --validated-sources <manifest> --run-id <id>` builds a fresh candidate under D using the existing diagnostic builder and restores temporary defines. Its source manifest includes the changed platform/native/save boundaries and candidate tools.

`Tools/Exhibition/Prepare-OverlayObservationCandidate.py` checks source/define provenance, reads compiled connections through Cecil, runs the existing distribution stager, verifies raw→inspection→final DLL/EXE equality and the full payload, and prepares an actual config from an explicitly selected account configuration and the current Ready journal. Config fields are `Version=1`, `AppId=5218360`, numeric `SteamId`, `PayloadManifestPath` and `EvidenceRoot`. It separately compares the currently installed payload with the new candidate.

Candidate/config preparation does not upload, change the Steam branch, install or execute the game. The generated start-options document is for a later launch after installation and full manifest/provider verification. Do not apply these arguments to Build25190245.

## Validation record

Current correction evidence: `/mnt/d/J2M/evidence/overlay-handoff-fixes/20260909-review-fixes`.

- Corrected focused full `focused-02`: EditMode 380 passed; PlayMode 2 graphical-panel fixtures skipped.
- Windows helper fake suite `helper-01.log`: 88 passed, including real harmless-process receipt capture/serialization/consumer verification; no Steam/game/native API execution.
- Same-source core: EditMode 254 passed; PlayMode 107 passed, 4 skipped. UI: 1,359 passed. All final runners exited 0; source hashes remained unchanged.
- First correction attempt retained 379 passes and one synchronous EditMode async-context timeout; the test context was corrected before the final passing run.
- Historical candidate `20260909T055218Z-observation` and its original config are superseded by the corrected candidate described in the correction evidence. Do not use that candidate for a live comparison: structural build/payload checks did not detect the subsequently reviewed receipt mismatch.
- Corrected candidate `20260909T063211Z-observation-reviewed` passed build/compiled wiring/raw→final/full-payload verification (257 files). Temporary defines and original Addressables XML/meta were restored. New config: `D:\J2M\evidence\overlay-handoff-observation-reviewed.json`. Current installed payload differs. Upload completed as recorded above; branch activation, installation and live execution were not performed.
- Unfiltered full, interactive Editor lifecycle, manual mouse/keyboard/scroll review and actual Steam Overlay comparison have not run. Batch skips do not validate visual/input behavior.

The original reset trial remains interrupted at Overlay non-display. Achievement display refresh and FullCycle comparison remain separate unfinished questions.

## Workflow correction following the three live origin attempts

The 16:06, 16:09 and 16:33 screenshots demonstrate visible original Overlay screens. None of those runs reached a replacement: missing/invalid capture input caused terminal failure. The third run contained `origin.png.png` while the expected manually entered name was absent. These failed runs remain failed; later external review files do not retroactively create successful runtime reports.

The correction separates recoverable operator input from mandatory evidence integrity, automatically imports the external capture, increases panel text/button readability and gates replacement on completed report collection. Evidence: `/mnt/d/J2M/evidence/overlay-handoff-workflow-fix/20260909`. Validation/build/upload state for this correction is recorded there separately from Build25202548. The steps below apply only after the corrected candidate is installed.

1. Start the configured diagnostic from Steam. Wait for `OriginObserver / Observing`.
2. Click **Overlay 열기 요청 시각 기록**, then press **Shift+Tab** once.
3. If Overlay is visible, capture the visible screen with Windows and save the PNG/JPEG to the Desktop with its original name. Close Overlay with **Shift+Tab**.
4. In File Explorer, select that actual image file, **Shift+right-click → Copy as path**. Return to the game.
5. Click **클립보드 경로 붙여넣기**, then **캡처 불러오기 (증거 폴더로 자동 복사)**. Wait for **캡처 연결 완료: [actual filename]**. If an input error appears, correct the selected file/path and repeat this step within the remaining original five minutes.
6. If the screen actually opened, click **열림 — opened**, wait for the result to finish saving, then click **게임만 교체 (한 번)** once. Do not launch a second game manually. If Overlay was absent or uncertain, record the corresponding result and exit; no replacement is allowed.
7. Wait for the automatically launched `ReplacementObserver / Observing`. Repeat steps 2–5 with a new screenshot of the child. Record the actual result (`opened`, `not-visible`, or `inconclusive`) and click **진단 종료 (게임 닫기)**. A negative/uncertain result does not require a screenshot.
8. Preserve the run directory for review of final save/SDK checks, helper terminal, normal child exit and Steam tracking release. Original/child screen reports alone do not close those checks.

Do not rename captures, type an expected filename manually or copy files into a run directory. If a run has already reached `Failed` or `ObservationExpired`, exit it; the correction does not revive old runs.

Workflow correction validation: focused EditMode 391 passed (2 graphical PlayMode fixtures skipped); core EditMode 254 passed and PlayMode 107 passed (4 skipped); UI 1,359 passed. Runners exited 0 on unchanged source hashes. The 12 helper-related source files are unchanged from the prior 88-pass Windows fake suite; that suite was not rerun for this input/panel correction. Unfiltered full and manual graphical/input/Steam trials remain not run.
