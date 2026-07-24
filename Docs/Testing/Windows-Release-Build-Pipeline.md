# Windows x64 Non-Development Mono RC Pipeline

## Scope

This pipeline creates a disposable Windows x64 non-development Mono RC artifact.
It is not Store signoff, an IL2CPP migration, Steam packaging, or upload automation.
`PlayerProfilerCaptureCli` remains the separate Development Player workflow.

The zero-error artifact from source
`ae0ca73edbf84a708d90fcffd00d9387913c93fd`, run
`20260724T140105269Z`, is an immutable reference for the evidence-contract
correction that follows it. The correction must create a new committed revision
and new run; it must not rewrite that artifact or any of its private evidence.
Reference status does not mean Store acceptance.

## Fixed contract

- Configuration: `Windows-x64-NonDevelopment-Mono-RC`
- Target and architecture: `StandaloneWindows64`, `x86_64`
- Build options: `BuildOptions.None`
- Backend and stripping: Mono, `ManagedStrippingLevel.Disabled`
- Player log and warning stack trace: enabled, `ScriptOnly`
- Incremental GC: required to remain enabled
- Scenes, in exact order:
  1. `Assets/Scenes/MainMenuScene.unity`
  2. `Assets/Scenes/UIAudioScene.unity`
- Unity: the running editor version must exactly match
  `ProjectSettings/ProjectVersion.txt`

The entry snapshots the effective backend, stripping, Player.log, and warning
stack-trace settings. It applies the RC values only for the build and verifies
restoration in `finally`. Restoration failure takes precedence over build success.
When all effective settings already match the RC contract, the transaction skips
both setters and restoration so absent-default ProjectSettings keys are not
materialized as configuration drift.

Build acceptance is strict:

```text
BuildResult == Succeeded
AND BuildReport.summary.totalErrors == 0
AND structured errorRecordCount == totalErrors
AND C# entry exit == 0
AND Unity process exit == 0
AND wrapper evidence validation succeeds
```

`BuildResult.Succeeded` with one or more recorded errors exits through
`BuildErrorsRecorded`. Settings restoration failure remains higher priority than
that build outcome. The wrapper independently re-reads metadata, the shareable
report summary, and private structured details before it can create
`SUCCESS.json` or promote an artifact.

## Test-first gates

Run these before any Player build:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File "Tools\Build\Tests\Build-WindowsRelease.Tests.ps1"
```

```bash
./run_tests.sh full --filter WindowsReleaseBuildPipelineTests
./run_tests.sh core
git diff --check
find Assets -name "InitTestScene*" -print
```

The focused PowerShell harness uses temporary files and synthetic process/Git
snapshots. It does not invoke Unity or modify the repository.

The harness must write immutable execution evidence for the full selected suite
before its result can be cited. The shareable result records:

- schema version and suite name
- exact command and PowerShell version
- source SHA and source tree
- test script relative path and SHA-256 from that committed source
- selected, passed, failed, and skipped counts
- UTC start and completion timestamps
- result status

Machine, operator, absolute-path, and raw console details remain in private
evidence. The shareable result file and its SHA-256 sidecar are preserved with
the revision's other test evidence. Counting `Invoke-Case` declarations or
re-reporting an earlier console result is not execution evidence. No correction
revision may claim its selected/pass count until its own result file exists,
hashes correctly, names that exact committed revision, and records every selected
case as passed with failed 0 and skipped 0. The correction expands the historical
58-case baseline; its expected suite size is 73 cases.

All wrapper-owned Git commands use the process-local
`git -c core.longpaths=true` option. This permits the exact-SHA detached checkout
to materialize long Unity asset paths without changing repository, global, or
system Git configuration. Detached checkout and snapshot commands additionally
use process-local `core.autocrlf=false` and `core.eol=lf`, keeping the checkout
and Unity's serialization byte-stable without changing the invocation
worktree's Git behavior. Status porcelain uses NUL-delimited records, preserving
spaces and other quoted-path characters while every untracked file is checked
against the exact allowlist.

## Invocation

After the implementation is committed and the invocation worktree is clean:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File "Tools\Build\Build-WindowsRelease.ps1" `
  -RepositoryRoot "C:\Users\user\2026TeamProject_J2M" `
  -UnityExe "C:\Users\user\Desktop\6000.3.11f1\Editor\Unity.exe"
```

The wrapper does not pass `-quit`; `WindowsReleaseBuildCli` owns the Unity exit.
It creates and preserves a new detached worktree at the exact committed source
SHA under the short deterministic root `C:\VQBuildSources`. The wrapper rejects
a detached path whose known longest URP/APV importer path would exceed the
legacy 259-character Windows budget. This avoids relying on machine-wide long
path registry policy while preserving clean-import determinism. The first
acceptance run keeps that worktree for provenance inspection.

Path-budget tests must exercise the full predicted critical path, not only the
source-root string. The required matrix is:

| Case | Required result |
|---|---|
| Predicted critical path exactly 259 characters | accept |
| Predicted critical path exactly 260 characters | reject before worktree creation |
| Root containing spaces, within budget | accept |
| Root containing Unicode, within budget | accept without lossy normalization |
| Alternate valid source SHA values | recompute the full path and enforce the same boundary |
| Alternate valid RunId values | recompute the full path and enforce the same boundary |
| Any root/SHA/RunId combination above budget | reject before worktree creation |

Every case includes the configured critical package/importer suffix. Git
`core.longpaths=true` remains a process-local checkout aid; it is not evidence
that Unity's importer can consume a path above this budget.

The path budget is a source-preparation contract, not an error exception. With
the former Documents-based default, the clean-import path for
`TraceRenderingLayerMask.urtshader` was 264 characters while Windows long paths
were disabled. Its scripted importer failed before creating the ComputeShader
and RayTracingShader subassets; later URP validation then reported the host-type
mismatch. The short root keeps the same package, importer, and Global Settings
references intact and prevents that import failure without suppressing errors.

Before Unity starts, every repository-family or unattributed `Unity.exe`, every
`VectorQuake.exe`, and every orphan/unattributed CrashHandler is rejected. After
the wrapper starts Unity, only that exact Unity PID and its directly parented
`UnityCrashHandler64.exe` are added to the allow set. A CrashHandler owned by a
different clearly identified product remains allowed. Accepted and rejected
processes are written to private `process-preflight.json`,
`process-poststart.json`, and `process-postbuild.json`, including PID, parent PID,
classification, command line, and any fail-closed reason. These files are never
promoted into the shareable payload.

## Provenance and output

The wrapper rejects tracked/staged changes and permits untracked files only below
the exact roots supplied through `-AllowUntrackedRoot`. The detached build source
must have no tracked, staged, or untracked changes.

It snapshots both worktrees before and after the build. Critical configuration,
entry, policy, and wrapper files are SHA-256 canaries. Any drift prevents
promotion. Tracked and staged cleanliness uses Git content diffs, while porcelain
status is used for the exact untracked allowlist. This keeps byte-identical
Unity rewrites from becoming timestamp-only false positives without permitting
any content or canary drift.

Output is written outside the repository:

```text
<OutputRoot>/<sourceSha>/Windows-x64-NonDevelopment-Mono/
  .staging-<runId>/
  failed/<runId>/
  <runId>/
```

The staging directory contains `payload/`, `files.sha256`,
`files.sha256.sha256`, `artifact-provenance.json`, and `SUCCESS.json`. Manifest
entries use relative, forward-slash paths and ordinal ordering. These four
control files are excluded from the payload manifest so the final binding is
non-circular.

A distributable payload must not contain a
`*_BurstDebugInformation_DoNotShip` directory or file beneath one. It must also
not contain inspectable text or control content that exposes an absolute
operator, workspace, detached-source, private-log, or telemetry path. The
payload gate scans before manifest creation and fails closed on either condition;
it does not omit the offending entry only from `files.sha256`, redact it in
place, or reinterpret it as a warning. Store packaging must repeat the gate over
the exact candidate bytes. A payload that has not passed this policy may remain
private reference evidence, but it cannot be described as distributable,
Store-ready, or Store accepted.

`payload/build-metadata.json` schema v2 contains Unity build-time facts and no
manifest placeholders. `payload/build-report-summary.json` schema v2 contains
the BuildReport summary plus only the shareable structured-detail reference:
filename, SHA-256, error/warning record counts, and distinct error message
hashes. Full steps, original messages, normalized messages, message hashes, and
stack text remain in private `build-report-details.json` schema v1. The summary
schema bump adds the correction's cross-binding fields without promoting raw
details or changing the private-details schema.

After manifest generation, `artifact-provenance.json` immutably binds source
SHA/tree, metadata, report summary, private report details, payload manifest,
zero-error/count gates, and entry/policy/wrapper source hashes. `SUCCESS.json`
binds that provenance file and its SHA-256. A staging `SUCCESS.json` is not
success: only same-volume rename to the final run directory followed by
manifest, provenance, and control verification is accepted.

In `SUCCESS.json` schema v2, the canonical payload-manifest filename property is
`payloadManifestFile`. The legacy alias `payloadManifest` is not emitted or
accepted for a new schema-v2 artifact. Existing immutable artifacts retain the
bytes and schema they were created with; they are not migrated in place.

Before `SUCCESS.json` creation and again from the final promoted directory, the
wrapper cross-binds the following values rather than validating each file in
isolation:

- `runId`, `artifactId`, `sourceSha`, and `sourceTree`
- target/configuration identity
- BuildResult, total error count, total warning count, structured error count,
  and structured warning count
- metadata, report-summary, and private report-details filenames and SHA-256
- `payloadManifestFile`, manifest SHA-256, and payload file count
- provenance filename and SHA-256
- zero-error and count-consistency gates

The shared identity and counts must agree across build metadata, report summary,
private report details, artifact provenance, and `SUCCESS.json` wherever each
schema carries them. A missing field, schema mismatch, count mismatch, hash
mismatch, or identity mismatch rejects staging and final post-promotion
verification.

Entry, policy, and wrapper hashes are recomputed from the exact committed
detached source identified by `sourceSha`, not trusted from an earlier JSON field
or from uncommitted invocation-worktree bytes. The recomputed Git-source hashes
must match build metadata and provenance before promotion and must be recomputed
and checked again after promotion. The detached source tree must equal
`sourceTree`.

Raw Unity and Player logs, structured BuildReport details, invocation/build
source snapshots, absolute paths, command lines, user/machine identity,
screenshots, and save/PlayerPrefs snapshots remain in private external evidence.
The private run root also keeps `wrapper.log` and `settings-transaction.json`;
the latter records original/required settings, changed fields, applied
verification, restore attempt/result, and restored verification.

This boundary is content-based, not filename-based. A raw/private value embedded
in a generated debug text file is still private and is forbidden from the
promoted distributable payload. Shareable JSON may carry relative filenames,
counts, verdicts, and SHA-256 bindings, but not raw stack text, command lines,
absolute private paths, operator identity, machine identity, save data, or
PlayerPrefs values.

## Actual Player smoke

Build success alone is not an actual Player smoke verdict. The smoke must launch
the executable from the final promoted artifact and bind its evidence to the
artifact's `sourceSha`, `sourceTree`, artifact RunId, executable relative path
and SHA-256, plus a distinct smoke RunId.

The required route is:

```text
MainMenu
-> Start
-> Slot 1 explicit Continue
-> loading
-> Lab-01
-> gameplay input
-> normal exit
```

The private smoke bundle retains timestamped UI/Continue/gameplay evidence,
process snapshots, raw `Player.log`/`Player-prev.log`, parser input/output,
screenshots, and exit evidence. Its shareable summary records the route gates,
explicit Slot 1 Continue, gameplay input, normal exit, profiler/debugger wait
state, crash/unhandled-exception state, parser version and counts, and hashes or
references to the private bundle. An older RC, a Development Player, or a
different artifact RunId cannot satisfy this gate.

The correction revision has not passed this gate merely by documenting it. A
new zero-error artifact and its exact final executable must complete the route
before the correction can claim actual Player smoke success.

## Historical artifact classification

Existing artifacts and their control files are immutable evidence:

- Source `591312a6691679a4ca3aa8ed32124ee7247709ca`, run
  `20260724T074847350Z`, recorded three BuildReport errors. It is
  `FIRST_ARTIFACT_REFERENCE_ONLY`. Its legacy `SUCCESS.json` and
  `promotionReady` value do not override the current strict zero-error contract.
  It is never RC accepted, distributable, Store-ready, or Store accepted.
- Source `ae0ca73edbf84a708d90fcffd00d9387913c93fd`, run
  `20260724T140105269Z`, is the zero-error reference artifact for the correction
  slice. It remains unchanged and is not evidence that the later correction
  revision, distributable-payload gate, or Store signoff has passed.

New evidence is written under a new source revision and RunId. Existing payload,
manifest, provenance, `SUCCESS.json`, failure quarantine, build logs, and smoke
evidence must never be edited, relabeled, deleted, or reused as the new run's
evidence.

## Exit ownership

C# codes occupy `0..50`; wrapper codes occupy `100..115`. Both schemas are
constants and uniqueness-tested. Failed staging content is moved, when possible,
under `failed/<runId>` with a non-deployable `FAILURE.json`.

Store backend, Store Player.log policy, application identifier signoff, build
number policy, remote/tag policy, Steam packaging, depot/upload automation,
runtime buffer-disposal and JobTempAlloc diagnostics, and formal historical
audio-instability closeout remain pending. Completion of the evidence-contract
correction permits a later Store-signoff audit; it does not complete Store
signoff.
