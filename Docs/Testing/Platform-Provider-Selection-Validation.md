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
./run_tests.sh core
```

The `Game.Platform` PlayMode fixture crosses the production parser/bootstrap/host boundary
with no selection, explicit Local, absent Steam, unknown provider, missing value,
conflicting values, fake Steam success, and fake Steam unavailable inputs. Parser-only
tests are not sufficient evidence for source-only promotion.

## Dependency-absent gate

The canonical clean checkout contains no Steamworks.NET source/package, Valve native binary,
`steam_appid.txt`, or global `STEAMWORKS_NET` define. An explicit `steam` request must still
reach Foundation and end as `RequestedProviderNotRegistered`; both Local factory creation and
Local runtime initialization must remain zero.

## Dependency-present companion

When an approved private dependency is temporarily injected, no-selection still selects
Local while explicit `steam` resolves Steam. A missing native after composition must end as
`RequestedProviderUnavailable` (for example `DllMissing`) with no Local fallback. Never
commit the injected package, native binary, AppID file, scripting define, or ProjectSettings
mutation.

## Evidence and reporting

For every lane record the exact revision/tree, selected test counts, failures, Font hash,
ProjectSettings/Scene/Prefab/ScriptableObject mutation, tracked diff, dependency residue, and
tests not run with reasons. A cold import timeout is runner evidence, not a pass; retain its
artifact and report a later warm completion separately.
