# Platform Provider Selection Validation

## Scope

This lane validates the always-compiled provider request contract independently from any
optional store SDK or native payload. It does not approve native storage, dependency
delivery, licenses, production promotion, or Overlay behavior.

## Canonical request matrix

| Arguments | Factory state | Expected final result | Local initialization |
| --- | --- | --- | --- |
| none | any | `DefaultLocalSelected` | once |
| explicit `local` | any | `ExplicitProviderSelected(local)` | once |
| explicit provider | matching factory | `ExplicitProviderSelected(provider)` | zero |
| explicit provider | no matching factory | `RequestedProviderNotRegistered` | zero |
| explicit provider | matching runtime unavailable | `RequestedProviderUnavailable` | zero |
| missing/empty/invalid value | any | `InvalidProviderSelection` | zero |
| duplicate same/different value | any | `ConflictingProviderSelection` | zero |

`FallbackUsed` is true only for no-selection Default Local. Explicit Local is a direct
selection and reports false.

## Focused commands

Run from the worktree being validated:

```bash
./run_tests.sh full --filter Game.Platform
./run_tests.sh full --filter PlatformRuntime
./run_tests.sh full --filter PlatformProviderNeutrality
./run_tests.sh full --filter SteamPlatform
./run_tests.sh full --filter SteamworksNet
./run_tests.sh core
```

The `Game.Platform` PlayMode fixture crosses the production parser/bootstrap/host boundary
with no selection, explicit Local, absent Steam, unknown provider, missing value,
conflicting values, fake Steam success, and fake Steam unavailable inputs. Parser-only
tests are not sufficient evidence for source-only promotion.

## Dependency-absent gate

Run this gate in a disposable copy/worktree after removing the optional Steamworks.NET
adapter package and `com.j2m.thirdparty.steamworksnet`. The dependency-absent project must
contain no Steamworks.NET source/package, Valve native binary, `steam_appid.txt`, or global
`STEAMWORKS_NET` define. An explicit `steam` request must still reach Foundation and end as
`RequestedProviderNotRegistered`; both Local factory creation and Local runtime
initialization must remain zero.

## Dependency-present companion

The production dependency-present project owns Steamworks.NET through the single embedded
package `com.j2m.thirdparty.steamworksnet@2025.165.0-j2m.1`. It contains the complete managed
Runtime from official upstream commit `3c236146fe55e48eb8776a6b912a7675ef591b08`
and exactly one Windows x64 `steam_api64.dll`, byte-identical to the validated Valve SDK 1.65
redistributable. It excludes upstream Editor automation, AppID 480, global scripting-define
mutation, Windows x86, and non-Windows native payloads.

No-selection still selects Local while explicit `steam` resolves Steam. Without an AppID,
native load and entry-point resolution must complete without load exceptions, initialization
false must become `RequestedProviderUnavailable`, and Local fallback must remain unused.

## Evidence and reporting

For every lane record the exact revision/tree, selected test counts, failures, Font hash,
ProjectSettings/Scene/Prefab/ScriptableObject mutation, tracked diff, dependency residue, and
tests not run with reasons. A cold import timeout is runner evidence, not a pass; retain its
artifact and report a later warm completion separately.
