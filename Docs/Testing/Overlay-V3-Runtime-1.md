# Overlay v3 runtime-1

`observation-v3-runtime-1` connects the compiled observation-only startup to the canonical platform host, a read-only Steam runtime, and the Windows helper. This is an implementation description, not evidence of a successful Steam refresh.

## Composition

- `ObservationV3RuntimeWire.cs` defines strict required-field DTOs and pinned byte references. Config, launch plan and prerequisites are global; preparation starts the run. The dependency direction is preparation → baseline → origin reports → request → creation → grant → client → context → receipt → ack → reports.
- `PlatformStartupDeferral` delays selection/Init until registration is sealed and a validated owner releases startup on the main thread. Cancellation cannot reset/retry initialization. The existing canonical host owns Init, callbacks and Shutdown.
- `SteamObservationNativePermit` is private/internal to the Steam assembly, with explicit Exhibition IVT composition. It is single-use and bound to the prepared owner and generation. It is not authentication against arbitrary same-process code. `ISteamObservationAchievementsReader` exposes only the getter; successful false, successful true and failed/unknown remain distinct.
- `ObservationV3Runtime` composes origin preparation/baseline and replacement handshake. `ObservationV3Flow` and `ObservationV3Presentation` expose user reports, one origin FullCycle action, and orderly exit. Existing v2 GameOnly UI is separate.
- `ObservationV3WindowsHost` holds the scope mutex on one OS thread. `ObservationV3Operations` performs bounded I/O outside the event queue and rejects expired/generation-stale completion. `Cycle.PrepareClient` shares the existing parent30/shutdown60/readiness120/probe10 policy without constructing a legacy request.
- `ObservationV3WindowsEnvironment` retains process handles, checks WMI command lines and monitors candidates. Launch uses one `SteamExeApplaunch` submission; no retry or direct-exe fallback. Returned launcher identity is not child identity. A distinct launcher must actually exit within the existing handoff deadline before the helper sends a receipt; the child then checks singleton client identity before native Init.
- `ObservationV3RuntimeHandoff` requires persisted submission evidence before receipt/native admission, serializes report storage acknowledgements and drains the submitted boundary before exit authorization. A visibility correction invalidates the previous display conclusion. Child exit, helper exit, tracking and purpose are separate observations.

## Input and availability

The existing exact, separated v3 options remain the command-line contract. Origin generates Role/Owner/Config; replacement generates Role/Owner/Request/RequestHash/Context/ContextHash/Endpoint. A verified fixed Steam launch entry contributes exactly one provider pair. Missing or duplicate provider, Unity/reset/v2 extras, wrong casing and unsupported syntax reject. `ObservationV3Arguments` provides one Windows quoting/tokenizing implementation and a semantic hash after strict option validation.

Origin config must be under D evidence storage. References are canonical absolute Path/Sha256 pairs with reparse rejection and referenced byte checks. Payload inventory, current Ready journal and participant snapshots are pinned. Baseline pin documents are read and checked, not merely referenced by well-shaped hashes. Replacement validates the entire probe input/grant/creation/result chain and the successful query/Shutdown/exit contract before requesting native admission.

`RequireLaunchAvailable` requires typed verified launch evidence, cwd and delivery state, matching fixed tokens, and SDK/Gate prerequisites. `Unknown` documentation is not live eligibility. No launch options, launch evidence, account state or baseline are fabricated by this implementation. Config/plan preparation must use independently established facts. Installed payload, actual Steam argv/cwd, event coverage and current baseline remain separate readiness gates.

The Windows process-start subscription may be denied by local permissions. This closes admission; snapshot-only observation does not replace event coverage. A native Init that never returns remains unknown; external timeout does not claim same-thread cleanup or process exit.

## Validation and artifacts

Repository lanes run from this worktree with `./run_tests.sh`. Multiple focused fixture names use `;` (not `|`). New fixtures are `ObservationV3Runtime1Tests`, `PlatformStartupDeferralTests`, and `SteamObservationRuntime1Tests`; existing startup/wire/helper/platform/save fixtures remain relevant. UI changes also require the UI lane and later explicitly authorized success-path editor/manual evidence.

Windows harmless checks:

- `Tools/Exhibition/Tests/ObservationV3.Runtime1.Tests.ps1 -EvidenceRoot <fresh D evidence directory>` compiles all ten shared C#5 sources, checks independent process identities/OS argv/handles, framed report/exit IPC and old-peer rejection, candidate snapshot rejection and event-monitor availability/fail-closed behavior. It additionally runs the production handoff through report storage and confirmed child/helper exit in independent harmless helper/launcher/child processes; native readiness and Steam submission remain explicit fake ports. It also stages and invokes the exact PS entry with an invalid runtime-1 probe input, verifying strict rejection before native access.
- `ObservationV3.Tests.ps1` preserves the earlier IPC regression suite.
- `Restart-Experiment.Tests.ps1` preserves legacy cycle/probe supervision policies. Its serializer bootstrap extraction targets the legacy Probe branch after the new discriminator.
- `python3 -m unittest discover -s Tools/Exhibition/Tests -p test_candidate_tools.py` checks candidate-tool contracts without building a game candidate.

Evidence for this implementation is under `/mnt/d/J2M/evidence/overlay-v3-runtime-1/20260909T183409Z`. Review findings are retained verbatim with supplemental resolutions. The final status artifact lists exact lane counts and limitations. Harmless OS boundary tests and fake scheduler tests do not constitute a successful whole production-host cycle, installed candidate suitability, Steam remote-state immutability, or live refresh success.

Build25212700's two prior manual input-blocking results remain preserved. Dim/menu behavior is not reclassified, and those tests are not repeated here. No actual Steam/game/native run, candidate build/upload/install, reset/reacquisition or participant mutation is performed by this implementation task.
