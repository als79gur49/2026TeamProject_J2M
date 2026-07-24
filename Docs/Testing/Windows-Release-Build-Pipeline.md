# Windows x64 Non-Development Mono RC Pipeline

## Scope

This pipeline creates a disposable Windows x64 non-development Mono RC artifact.
It is not Store signoff, an IL2CPP migration, Steam packaging, or upload automation.
`PlayerProfilerCaptureCli` remains the separate Development Player workflow.

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
SHA. The first acceptance run keeps that worktree for provenance inspection.

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

`payload/build-metadata.json` schema v2 contains Unity build-time facts and no
manifest placeholders. `payload/build-report-summary.json` schema v1 contains
the BuildReport summary plus only the shareable structured-detail reference:
filename, SHA-256, error/warning record counts, and distinct error message
hashes. Full steps, original messages, normalized messages, message hashes, and
stack text are stored in private `build-report-details.json` schema v1.

After manifest generation, `artifact-provenance.json` immutably binds source
SHA/tree, metadata, report summary, private report details, payload manifest,
zero-error/count gates, and entry/policy/wrapper source hashes. `SUCCESS.json`
binds that provenance file and its SHA-256. A staging `SUCCESS.json` is not
success: only same-volume rename to the final run directory followed by
manifest, provenance, and control verification is accepted.

Raw Unity and Player logs, structured BuildReport details, invocation/build
source snapshots, absolute paths, command lines, user/machine identity,
screenshots, and save/PlayerPrefs snapshots remain in private external evidence.
The private run root also keeps `wrapper.log` and `settings-transaction.json`;
the latter records original/required settings, changed fields, applied
verification, restore attempt/result, and restored verification.

## Exit ownership

C# codes occupy `0..50`; wrapper codes occupy `100..114`. Both schemas are
constants and uniqueness-tested. Failed staging content is moved, when possible,
under `failed/<runId>` with a non-deployable `FAILURE.json`.

Store backend, Store Player.log policy, application identifier signoff, build
number policy, remote/tag policy, Steam packaging, and upload remain pending.
