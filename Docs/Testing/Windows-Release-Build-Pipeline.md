## 2026-09-11: 격리된 worktree의 동시 Unity 작업

배포 프로세스 검사는 Git worktree 전체가 아니라 invocation/build 프로젝트와 해당 배포 OutputRoot를 보호한다. 명시적인 절대 `-projectPath`를 가진 별도 프로젝트와 전용 Library/Temp, 독립 출력은 허용한다. 같은 프로젝트/중첩 경로, 공유 Library/Temp를 포함한 reparse 경로, 보호 경로를 가리키는 명시적 출력·로그·결과 인자, 경로를 해석할 수 없는 Unity는 거부한다. 경로 비교는 Windows 인자 해석, 절대 경로 정규화, 구분자 경계 기준이다.

빌드 PID와 같은 프로젝트를 사용하는 자식 Unity(worker), 허용된 Unity의 직계 CrashHandler는 허용한다. 부모를 확인할 수 없는 CrashHandler와 실행 중인 VectorQuake 제품은 기존처럼 거부한다. 프로젝트 Library와 OutputRoot의 파일 lease는 배포 도구끼리의 중복 실행을 막으며 성공/실패 모두 해제한다. lease는 임의의 외부 프로그램에 대한 강제 잠금이 아니므로 빌드 중 보호 프로젝트·출력을 수동으로 수정하지 않는다.

시작 전/직후/종료 후 검사와 소스 HEAD/tree·dirty 상태·설정·빌드 결과·산출물 hash 검사는 유지한다. 공유 `origin/main` 및 ahead/behind 변화는 출처 메타데이터로 남기되 소스 drift로 판정하지 않는다. 프로세스 인자에 드러나지 않는 사용자 정의 외부 쓰기까지 검증하는 보장은 없으며, 별도 작업은 자신의 프로젝트와 출력만 사용해야 한다. CPU/RAM 경합이나 Unity 라이선스 문제는 별도 실행 결과이며 프로세스 존재만으로 실패 처리하지 않는다.

검증: `Tools/Build/Tests/Build-WindowsRelease.Tests.ps1`의 격리/충돌/worker/lease/공유 원격 참조 테스트와 현재 worktree의 `./run_tests.sh core`. 이 수정은 배포 PowerShell에 한정되며 UI/게임 소스 변경은 없다. 실제 빌드·Steam 업로드 결과는 `/mnt/d/J2M/evidence/exhibition-concurrent-build-20260911` 및 `/mnt/d/J2M/evidence/exhibition-upload-20260911`의 실행 기록으로 구분한다. 아래 문서의 과거 전체 Unity 차단 설명은 이 절로 대체한다.

# Windows x64 Canonical Mono Store Pipeline

## Scope

This pipeline creates the versioned Windows x64 canonical Mono Store artifact.
`StoreDistributable` means the build passed repository payload/privacy readiness;
it is not Steam shipping signoff, depot upload completion, or overall Store signoff.
The backend-comparison seam remains available only through the explicit
`BackendComparison` intent and creates separately named internal artifacts.
It is not an IL2CPP migration, Steam packaging, or upload automation.
`PlayerProfilerCaptureCli` remains the separate Development Player workflow.

The zero-error artifact from source
`ae0ca73edbf84a708d90fcffd00d9387913c93fd`, run
`20260724T140105269Z`, is an immutable reference for the evidence-contract
correction that follows it. The correction must create a new committed revision
and new run; it must not rewrite that artifact or any of its private evidence.
Reference status does not mean Store acceptance.

## Fixed canonical Store contract

- Store configuration schema: `1.0`
- Store configuration ID: `windows-x64-store-mono-logon-v1`
- Configuration path: `Windows-x64-Store-Mono-LogOn`
- Build intent: `CanonicalStore`
- Target and architecture: `StandaloneWindows64`, `x86_64`
- Build options: `BuildOptions.None`
- Backend and stripping: `Mono2x`, `ManagedStrippingLevel.Disabled`
- Player log: enabled
- Log policy ID: `local-player-log-no-auto-upload-v1`
- Automatic full-log upload and custom telemetry: disabled
- Payload audience: `StoreDistributable`
- Development, profiler connection, deep profiling, debugging, debugger wait,
  and forced assertions: disabled
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

The release entry also treats the `com.unity.collections 2.6.2` copies of
`System.IO.Hashing.dll` and `System.Runtime.CompilerServices.Unsafe.dll` as
test-only managed plugins. Their Windows Player compatibility is disabled only
inside a separate build transaction and restored in `finally`. Because PackageCache
import settings are immutable, the transaction uses each importer's transient
`SetIncludeInBuildDelegate` callback instead of rewriting package metadata. The
callback denies inclusion for the build and is replaced in `finally` with the
captured effective decision for the remainder of the batch process; the effective
include decision is verified before and after. Missing importers, package-version
drift, apply failure, or restore failure fail closed. A successful Player must
contain neither DLL in `VectorQuake_Data/Managed` nor either name in
`ScriptingAssemblies.json`.

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

The shared Store identity is bound through `build-metadata.json`,
`configuration-summary.json`, `artifact-provenance.json`, and `SUCCESS.json`.
Every file must agree on configuration schema/ID, intent, backend,
`scriptingBackend`, stripping, Player.log, log-policy ID, automatic-upload flag,
and payload audience. A matching Store ID with different effective values is
rejected.

## Windows distribution target contract

Build configuration and distribution target are orthogonal identities. The existing
`windows-x64-store-mono-logon-v1` ID continues to describe backend/logging policy and
must not be interpreted as Steam identity. Every canonical or backend-comparison
invocation must pass exactly one `-DistributionTarget`; omission and unknown values
fail closed.

| Target | Provider selection mode | Expected provider | Canonical arguments |
|---|---|---|---|
| `direct-windows` (`DirectWindows`) | `DefaultWhenUnspecified` | `local` | none |
| `steam-windows` (`SteamWindows`) | `ExternalLaunchArgumentRequired` | `steam` | `-j2mPlatformProvider`, `steam` |

The repository expectation for Steamworks admin is therefore derived as
`VectorQuake.exe -j2mPlatformProvider steam`. The authoritative value remains the
ordered token sequence in `WindowsDistributionTargetPolicy`; the joined text is a
human-readable derivative. `-j2mPlatformProvider=steam` is not the canonical
production launch contract even though the generic runtime parser accepts it.

Distribution metadata is evidence, not a runtime selection source. With no selector,
the runtime continues to choose Local. A SteamWindows executable started directly
from Explorer without the expected arguments is a diagnostic/unsupported Steam
production path; supported Steam production launch is Steam Library launch with the
canonical argument tokens. Steam initialization failure continues to fail closed
without Local fallback.

The promoted-payload contracts are:

- `DirectWindows`: forbids `steam_api64.dll`,
  `com.rlabrecque.steamworks.net.dll`, `steam_appid.txt`,
  `System.IO.Hashing.dll`, and `System.Runtime.CompilerServices.Unsafe.dll`.
- `SteamWindows`: requires `steam_api64.dll` and the managed binding
  `com.rlabrecque.steamworks.net.dll`, and forbids `steam_appid.txt`,
  `System.IO.Hashing.dll`, and `System.Runtime.CompilerServices.Unsafe.dll`.

`ValidatePromotedArtifactInventory` remains the canonical final-inventory validation
seam and is called by `WindowsDistributionStager`. The stager starts from one raw
Unity Player, performs include-list copy into a fresh external transaction directory,
applies the shared SteamPipe deny policy as a secondary guard, and creates separate
DirectWindows and SteamWindows promoted payloads. It never mutates PluginImporter
state, removes a package from the project, or edits the raw build.

The distribution command is intentionally separate from the build command:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File "Tools\Build\Stage-WindowsDistribution.ps1" `
  -SourceBuildRoot "D:\J2M\builds\raw\payload" `
  -DistributionTarget "steam-windows" `
  -OutputRoot "D:\J2M\builds\distribution\<sha>\steam-windows\<run>"
```

The output is a run root with `payload/` for shipping bytes and `evidence/` for
`distribution-manifest.json` plus `SUCCESS.json`. Every payload file is hashed from
the destination, compared with its source hash, ordinal-sorted by forward-slash
relative path, and validated against the target contract. `SUCCESS.json` is written
only after runtime completeness, denied-content, copy-integrity, source-immutability,
and promoted-artifact checks pass; the transaction is then renamed atomically to the
requested fresh output root. Actual Steamworks admin/AppID comparison and SteamPipe
upload remain deferred.

## Backend intent boundary

| Intent | Backend | Result | Configuration identity |
|---|---|---|---|
| `CanonicalStore` | `Mono` | allowed | `windows-x64-store-mono-logon-v1` |
| `CanonicalStore` | `IL2CPP` | rejected | none |
| `BackendComparison` | `Mono` | `MonoControl` | non-canonical comparison |
| `BackendComparison` | `IL2CPP` | `IL2CPPCandidate` | non-canonical comparison |

Omitting both intent and backend selects canonical Mono. Comparison artifacts
use `InternalRc` and retain the historical backend-specific configuration
directories. An IL2CPP candidate is never labeled with the canonical Store ID.

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
58-case baseline; the immutable record is authoritative for its actual selected
count.

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

This license-remediation execution did not run the wrapper's internal
detached-build path because it conflicts with the J2M storage contract
(`/mnt/d/J2M/worktrees` through `j2m-worktree-add`). The current
`C:\VQBuildSources` plus direct `git worktree add` implementation is not valid
new-build evidence under that contract. This revision therefore has focused
PowerShell/Unity policy evidence only and makes no current-revision distributable
claim. A later formal build must use the prepared-worktree flow below.

The immutable `c9c826e4f6d42862e9f65df223f0e7e4b3c91728` and
`65ab02cb8b86b64490ed3ec3a1587d3e4d3bb461` artifacts remain historical evidence
only: they contain both forbidden test DLLs and predate the corrected public-notice
contract, so they must not be re-promoted or described as distributable.

After the implementation is committed, the invocation worktree is clean, and an
exact-revision worktree has been created through `j2m-worktree-add`:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File "D:\J2M\worktrees\prepared-release\Tools\Build\Build-WindowsRelease.ps1" `
  -RepositoryRoot "D:\J2M\worktrees\prepared-release" `
  -PreparedBuildSourceRoot "D:\J2M\worktrees\prepared-release" `
  -OutputRoot "D:\J2M\builds" `
  -UnityExe "C:\Users\user\Desktop\6000.3.11f1\Editor\Unity.exe" `
  -BuildIntent "CanonicalStore" `
  -Backend "Mono" `
  -DistributionTarget "steam-windows" `
  -PayloadAudience "StoreDistributable"
```

The wrapper does not pass `-quit`; `WindowsReleaseBuildCli` owns the Unity exit.
When `-PreparedBuildSourceRoot` is omitted, its legacy internal path creates and
preserves a new detached worktree at the exact committed source SHA under the
short deterministic root `C:\VQBuildSources`; that path is not valid for new J2M
build evidence. The wrapper rejects
a detached path whose maximum predicted path across the known critical
Collections, URP Surface Cache, and URP/APV importer suffixes would exceed the
legacy 259-character Windows budget. This avoids relying on machine-wide long
path registry policy while preserving clean-import determinism. The first
acceptance run keeps that worktree for provenance inspection.

For storage-policy compliant J2M worktrees, pre-create the exact detached source
with `j2m-worktree-add` under `D:\J2M\worktrees` and pass it through
`-PreparedBuildSourceRoot`. The wrapper then validates the prepared worktree's
clean status, HEAD, tree, canaries, and path budget without invoking direct
`git worktree add`; its `Library` remains private to that worktree. The legacy
internal creation path remains available only for grandfathered release sources.

The clean Windows build may generate the established Addressables residue set
(`ProfileDataSourceSettings.asset`, `link.xml`, `Windows.meta`, and the Windows
content-state pair). Before Unity starts, the wrapper snapshots tracked paths
from the exact source revision. It removes an established candidate only when
that immutable ownership snapshot says the file is untracked, handles mixed
directories file-by-file, and prunes only an empty Windows residue directory.
Tracked candidates are preserved without restore, with each ownership decision
recorded in private evidence. Any tracked change or new generated path still
fails the source-drift gate.
Payload manifests hash destination bytes through Windows extended-length paths,
so deeply nested Addressables bundles remain inside the deterministic SHA-256 gate.

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

Every case includes all configured critical package/importer suffixes and uses
their maximum predicted length. Git `core.longpaths=true` remains a
process-local checkout aid; it is not evidence that Unity's importer can consume
a path above this budget.

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

Output is written outside the repository. The default `OutputRoot` is
`D:\J2M\builds`. The full source SHA and RunId own the physical path; the
configuration, backend, and distribution target remain bound in
`build-metadata.json`, `configuration-summary.json`, `artifact-provenance.json`,
and `SUCCESS.json`. A RunId is unique across targets and backends for the same
source SHA. An existing final, staging, failed, or private run path is rejected
before any run evidence is written:

```text
<OutputRoot>/<sourceSha>/
  .private/<runId>/
  .staging-<runId>/
  failed/<runId>/
  <runId>/
```

The wrapper checks the predicted longest known payload path in staging and the
final directory against the 259-character legacy Windows budget before creating
run evidence. It scans every generated staging item after the build and rejects
any actual path above that budget before promotion. For source
`2a8edd5352546ac1f9379c935283264015a0de50` and run
`20260926T095233405Z` under the default output root, the previously longest
MonoScript bundle path would be 232 characters in staging and 223 after
promotion. Future bundle names remain subject to the actual-file scan.
Existing artifacts under the former configuration/target directory structure
remain immutable and are not moved or renamed.

The staging directory contains `payload/`, `files.sha256`,
`files.sha256.sha256`, `artifact-provenance.json`, and `SUCCESS.json`. Manifest
entries use relative, forward-slash paths and ordinal ordering. These four
control files are excluded from the payload manifest so the final binding is
non-circular.

Every Direct Windows and Steam Windows distributable must contain two committed
root public notices beside `VectorQuake.exe`:

- `payload/ThirdPartyNotices.txt` is organized into exactly two top-level parts.
  `PART I - Required License and Legal Notices` contains the Unity Player
  reference, open-source, runtime UPM, font, and target-conditional Steam legal
  notices. `PART II - Licensed Third-Party Asset Disclosure` identifies the
  currently confirmed commercial assets, including the build-referenced AllSky
  skybox, as a transparency disclosure. It records that the individual or legal
  entity responsible for the product holds the applicable use and integrated
  distribution rights; it neither claims ownership of the underlying assets nor
  transfers or sublicenses them to recipients. It also does not prove that
  assets without provenance metadata have been fully inventoried. The UPM
  inventory includes the eleven UCL packages whose
  assemblies were present in prior Windows `ManagedStripped` evidence:
  Collections, Addressables, AI Navigation, Input System, Shader Graph, Splines,
  Timeline, uGUI, URP Config, Profiling Core, and Scriptable Build Pipeline.
  Their package copyrights and one canonical Unity Companion License v1.4 body
  are included alongside the bundled notices, including
  Cinemachine's bundled Clipper/Boost notice and Unity.Mathematics 1.3.3's
  Ashima Arts / Stefan Gustavson Noise MIT notice.
  The font inventory includes Noto Sans JP Regular/Bold and Noto Sans SC
  Regular/Bold, their generated TMP Static SDF assets, the Adobe copyright and
  Google Noto trademark notice, and the canonical SIL OFL 1.1 body. It also
  records the 2026-09-23 release-owner confirmation that the required KBO/KBOP
  clearance for distributing the KBO Dia Gothic TMP rendering assets was
  obtained while retaining the CI/BI, standalone-sale, and source-modification
  restrictions.
- `payload/UnityPlayerThirdPartyNotices.pdf` is Unity's unmodified
  Player/Windows/Mono notice for Unity `6000.3.11f1`. Its decoded content covers
  Unity Player components such as Mono, HarfBuzz, and ICU, but does not contain
  the project-added UPM package names or the Unity Companion License; it does not
  replace the package entries in `ThirdPartyNotices.txt`.

Before Unity starts, the wrapper requires both notices to be tracked `100644`
regular blobs at the exact source revision, rejects reparse points, and compares
each checked-out file with its committed Git blob. The TXT is decoded as strict
UTF-8. Its two classification parts and required subordinate sections occur
exactly once in canonical order;
the component, source, copyright, provider, package-version, and reserved-font
inventory is complete; and each explicitly delimited notice block matches its
approved normalized SHA-256. Repeated generic MIT wording is permitted because
license integrity is scoped to the owning component block rather than counted
globally. The wrapper also requires the committed `Packages/packages-lock.json`
blob to match the working file and binds every UPM package represented in the
public notice to its approved version. A package update without the matching
notice update therefore fails before Unity starts. The canonical UCL URL must
occur exactly eighteen times, once for each UCL package entry; legacy underscore
URLs are rejected. The PDF must be exactly
`132262` bytes, begin with `%PDF-`, and match
SHA-256 `7bed0e6f6646552f9262903b62863c693074ac89ccf6291ead7e876033a29623`.

Empty, incomplete, duplicated, reordered, body-mutated, non-blob, modified, or
wrong-version notices fail the `public-notices` stage with wrapper exit code
`117`. After Unity succeeds and its build evidence is accepted, the wrapper
copies both preflight-approved notices and rechecks their SHA-256 values. An
existing destination or source/destination mismatch prevents payload promotion.

The TXT remains subject to the Store text-privacy gate. The PDF is allowed only
under its exact canonical filename and pinned binary identity; arbitrary or
nested PDFs are not promoted. Both notices are included in `files.sha256`, and
later distribution staging preserves only their exact root-relative names with
canonical casing. The common TXT may describe target-specific components.
Steamworks.NET's MIT license and Valve's Steamworks SDK redistributable remain
separate so the MIT grant is not presented as covering `steam_api64.dll`.

These two files are the only licensing documents intentionally promoted by the
pipeline. Purchase receipts, seat records, historical commercial EULAs,
internal asset audits, and package-local source documentation remain private and
are not copied into the distributable payload. Changing Unity version, platform,
or scripting backend requires replacing the Unity Player PDF and its pinned
identity in the same change.

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

`payload/build-metadata.json` schema v4 contains Unity build-time facts and no
manifest placeholders. `payload/build-report-summary.json` schema v4 contains
the BuildReport summary plus only the shareable structured-detail reference:
filename, SHA-256, error/warning record counts, and distinct error message
hashes. Full steps, original messages, normalized messages, message hashes, and
stack text remain in private `build-report-details.json` schema v3. The summary is
schema v4. Metadata, summary, details, provenance, configuration summary, and
`SUCCESS.json` carry the same distribution target, selection mode, expected provider,
ordered launch arguments, required/forbidden artifact expectations, and derived
launch text. Any mismatch rejects promotion as an identity failure.
`configuration-summary.json` now has an explicit schema v2 for this expanded
identity surface.

After manifest generation, `artifact-provenance.json` immutably binds source
SHA/tree, metadata, report summary, private report details, payload manifest,
zero-error/count gates, and entry/policy/wrapper source hashes. `SUCCESS.json`
binds that provenance file and its SHA-256. A staging `SUCCESS.json` is not
success: only same-volume rename to the final run directory followed by
manifest, provenance, and control verification is accepted.

In `SUCCESS.json` schema v4, the canonical payload-manifest filename property is
`payloadManifestFile`. The legacy alias `payloadManifest` is not emitted or
accepted for a new schema-v4 artifact. Existing immutable artifacts retain the
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

`Player.log` and `Player-prev.log` are rejected by name anywhere in a
`StoreDistributable` payload. Actual smoke logs belong only below the source
SHA's sibling `.private/<runId>/smoke/` evidence root and the private
`VectorQuake-QA-Telemetry` archive. See
[`Docs/Support/Windows-Player-Log-Policy.md`](../Support/Windows-Player-Log-Policy.md)
for user-facing location, submission, privacy, retention, and redaction policy.

## Automatic-upload and production-log audit

The initial Store contract combines package inventory, service configuration,
project-owned runtime initialization-source inspection, production assembly
tests, and actual-smoke log inspection. The current audited state is:

- Unity Cloud Diagnostics reporting: disabled in
  `ProjectSettings/UnityConnectSettings.asset`
- Unity Analytics: disabled; no project-owned initialization
- Performance Reporting: disabled
- third-party crash reporter: no package or runtime initialization
- custom Player.log uploader/background telemetry sender: none
- user-submitted support logs: allowed only through a private support channel

Built-in Unity analytics/webrequest modules are not treated as active services
without an enabled service configuration and runtime initialization path.
Project-owned runtime logging contains no credential, PII, account ID, or save
document body. The standalone campaign seed-import success message currently
includes its local seed path; this is a path-minimization backlog, not sensitive
data or an automatic-upload path. Unity/package engine lines may also contain
system information and local paths and are classified separately from
project-owned privacy defects.

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
- Source `40b1a1aef2d36170015c19215488a4d7fdbeb91d`, run
  `20260724T171700000Z`, remains the immutable first StoreDistributable Mono
  reference.
- Source `9b38241f606c3c71d64577411c1100fe7c5a4700`, Mono run
  `20260725T061500000Z`, remains the immutable backend-comparison control.
  The same source's IL2CPP failed candidates and their failure evidence remain
  immutable comparison evidence; they are not canonical Store artifacts.

New evidence is written under a new source revision and RunId. Existing payload,
manifest, provenance, `SUCCESS.json`, failure quarantine, build logs, and smoke
evidence must never be edited, relabeled, deleted, or reused as the new run's
evidence.

## Exit ownership

C# codes occupy `0..50`; wrapper codes occupy `100..119`. Both schemas are
constants and uniqueness-tested. Failed staging content is moved, when possible,
under `failed/<runId>` with a non-deployable `FAILURE.json`.

Application identifier signoff, build number policy, remote/tag policy, Steam
packaging, depot/upload automation, runtime buffer-disposal and JobTempAlloc
diagnostics, and formal historical audio-instability closeout remain pending.
Mobile/IL2CPP expansion is reconsidered only after Windows launch stabilization.
Completion of this configuration/logging freeze does not complete overall Store
signoff.
