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
use process-local `core.autocrlf=false`, keeping Unity's LF serialization from
appearing as source drift on Windows without changing the invocation worktree's
Git behavior.

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
different clearly identified product remains allowed. Rejections are written to
the private external run evidence as `process-gate-rejection.json`, including PID,
parent PID, command line, and the fail-closed reason; this file is never promoted
into the shareable payload.

## Provenance and output

The wrapper rejects tracked/staged changes and permits untracked files only below
the exact roots supplied through `-AllowUntrackedRoot`. The detached build source
must have no tracked, staged, or untracked changes.

It snapshots both worktrees before and after the build. Critical configuration,
entry, policy, and wrapper files are SHA-256 canaries. Any drift prevents
promotion.

Output is written outside the repository:

```text
<OutputRoot>/<sourceSha>/Windows-x64-NonDevelopment-Mono/
  .staging-<runId>/
  failed/<runId>/
  <runId>/
```

The staging directory contains `payload/`, `files.sha256`,
`files.sha256.sha256`, and `SUCCESS.json`. Manifest entries use relative,
forward-slash paths and ordinal ordering. The three control files are excluded
from the payload manifest. `SUCCESS.json` is written only after drift and
manifest verification.

`build-metadata.json` carries the versioned manifest fields as part of schema
v1, but it does not embed the final manifest hash/count because that file itself
is a hashed payload member. The non-circular authoritative final values are in
`SUCCESS.json`. A staging `SUCCESS.json` is not success: only same-volume rename
to the final run directory followed by control verification is accepted.

Raw Unity and Player logs, absolute paths, command lines, user/machine identity,
screenshots, and save/PlayerPrefs snapshots remain in private external evidence.

## Exit ownership

C# codes occupy `0..50`; wrapper codes occupy `100..112`. Both schemas are
constants and uniqueness-tested. Failed staging content is moved, when possible,
under `failed/<runId>` with a non-deployable `FAILURE.json`.

Store backend, Store Player.log policy, application identifier signoff, build
number policy, remote/tag policy, Steam packaging, and upload remain pending.
