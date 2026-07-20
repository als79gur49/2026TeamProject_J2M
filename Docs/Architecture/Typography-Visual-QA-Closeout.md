# Typography Visual QA Closeout

## Scope

This closeout records the visual QA result for the UI Localization + Typography migration PR scope, including the Settings static-shell completion and SmartFormat production-integration restoration. It does not approve package changes, TMP Settings fallback changes, Addressables remote setup, or broader UI typography rollout.

Evidence folder:

```text
TestLogs/TypographyVisualQA/CommandLine-20260720-194045/
```

The evidence set contains 1920x1080 Settings, Pause, and Main Menu captures for `en-US` and `ko-KR`. Settings capture validation applied all 22 governed descriptors in each locale. Its canonical machine-readable manifest is `capture.log`.

The 2026-07-20 manifest was reconstructed without changing the PNGs. It cross-checks the successful split Unity logs against the six actual PNG files, records byte sizes, dimensions, localized descriptor counts, and SHA-256 hashes, and uses `6e5cd13fda59778aaa48f8db3047bb5c2188ccdb` as the reconstruction HEAD. The old split logs did not record per-output glyph/tofu results, so those manifest fields are explicitly `NOT_RECORDED`; the completed bilingual visual review remains the manual evidence for that historical capture.

Future aggregate captures write `capture.log` directly from `TypographyPreviewScreenshotUtility` after all required target/locale results and guarded-asset checks are collected. Unity stdout must use a different raw log filename; a partial capture or failed validation writes `overall_result=FAIL` and cannot produce a PASS manifest.

## P0 Closeout

| Item | Result |
|---|---|
| Horizontal mirror | Resolved |
| `ko-KR` English-only rendering | Resolved |
| Pause placeholder title | Resolved |
| Pause / Main Menu `ko-KR` tofu | Resolved |
| Settings `ko-KR` authored English shell | Resolved |
| Production SmartFormat integration | Restored |
| Full UI lane | 854/854 PASS |

## Visual Fixes

| Item | Result |
|---|---|
| Settings Mute wrapping | Fixed |
| Settings audio/display static shell | Descriptor/String Table path complete |
| Pause description visibility | Fixed |

## Validation Alignment

| Evidence | Result |
|---|---|
| Localization integration | 23/23 PASS |
| Settings production runtime | 20/20 PASS |
| Typography | 44/44 PASS |
| UI Architecture | 58/58 PASS |
| Typography preview screenshot manifest | 2/2 PASS |
| Full UI | 854/854 PASS |
| Core EditMode | 197/197 PASS |
| Core PlayMode | 92/92 PASS |

## Smart Metadata Closeout

The final Smart metadata correction did not change localized copy, layout, runtime display output, or screenshot assets. Existing visual evidence remains valid.

| Check | Final state |
|---|---|
| Shared UI entries | 46 |
| Smart entries | 5 |
| Non-Smart entries | 41 |
| Violations | 0 |
| `ui.settings.input.rebind_canceled` | Static localized status; no runtime argument; non-Smart |

## Remaining

| Item | Decision |
|---|---|
| `ko-KR` synthetic bold/material polish | P2; optional and not required for this PR closeout |
| Screenshot evidence resolution | 1920x1080 |
| Broader Theme sizing | Not needed for this PR; Hybrid sizing remains the scoped policy |
| Optional bake | Not approved |
