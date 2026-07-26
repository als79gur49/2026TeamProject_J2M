# Typography Audit

## 2026-07-26 Climate PR2 current-state override

The current production decision is recorded in
[Climate-Crisis-KR-Typography-Migration-Closeout.md](./Climate-Crisis-KR-Typography-Migration-Closeout.md).
It supersedes the Nanum candidate mappings, synthetic-bold risks, open
questions, and “next implementation” wording retained later in this audit.

- ko-KR uses the canonical Climate Crisis KR font/material with Normal style
  for all 19 semantic roles.
- Authored sizing is preserved. SettingsStatus remains `14 / Auto / 10-14`
  at height `28` and may use two lines.
- Pause title width is `160` with center preserved; audio values use effective
  width `140`.
- en-US identities remain base-authored; Generic Button stays SciFiSoldier and
  MainMenuCommand stays Orbitron.
- Managed ko-KR glyph coverage is 116/116 with no fallback dependency.
- Nanum TTF/SDF/SyntheticBold assets remain tracked for retention/history but
  are not used by current Climate runtime role mapping.

Older sections remain useful provenance for the preceding localization PR and
must not be interpreted as the current mapping.

## 1. Verdict

Verdict: PASS_WITH_NOTES

This audit was performed as a read-only source, prefab YAML, and TMP asset inspection before Cascading Typography Theme implementation. The audited runtime surfaces are Settings, Pause popup, and Main Menu command shell. No production code, prefab, scene, TMP Settings, String Table, Addressables, package, stage asset, or font asset change was made.

The source of truth for the existing English hierarchy is the actual TMP authoring in:

- `Assets/_Features/UI/UI_Screens/Prefabs/SettingsScreen.prefab`
- `Assets/_Features/UI/UI_Popups/Prefabs/PausePopup.prefab`
- `Assets/_Features/UI/UI_Screens/Prefabs/MainMenuScreen.prefab`
- referenced TMP font assets under `Assets/Synty/InterfaceSciFiSoldierHUD/Fonts/`

Key conclusion: the current English authoring is not a clean three-font hierarchy. It has a clear Display layer through Orbitron, a common Settings label/value layer through Exo SemiBold, a Pause button layer through Exo Regular, and a Settings button/key/status layer through `Font_SciFiSoldier_Bold`. `LiberationSans SDF` is still present in smaller utility/dropdown/helper text.

Post-audit decision: physical keyboard binding display names are locale-independent raw Input System output. Settings uses explicit `TypographyLocaleParticipation.LocaleInvariant` authoring for exactly 13 key-display TMP targets. Runtime application and Editor preview treat those bindings as successful no-ops, while rebinding remains allowed to replace the displayed raw value.

## 2. Baseline

| Item | Value |
|---|---|
| repo root | `/mnt/c/Users/user/2026teamproject_j2m-ui-audio` |
| branch | `worktree/ui-audio` |
| HEAD | `b68a5e1d07f6dae645cbbfbf34ba13d53f446131` |
| status before | `## worktree/ui-audio...origin/worktree/ui-audio [ahead 64]` |
| tracked diff before | none |
| staged diff before | none |
| untracked before | none |
| NanumGothic SDF dirty state | clean; no status entry for `Assets/_Shared/UI/Fonts/NanumGothic SDF.asset` |
| unrelated dirty diff | none |

Commands run for baseline:

```bash
git rev-parse --show-toplevel
git branch --show-current
git rev-parse HEAD
git status --short --branch
git diff --stat
git diff --cached --stat
git status --porcelain -- 'Assets/_Shared/UI/Fonts/NanumGothic SDF.asset'
```

## 3. Existing English Font Hierarchy

| Category | Candidate TMP_FontAsset | MaterialPreset | Evidence | Confidence |
|---|---|---|---|---|
| Display/Bold | `Assets/Synty/InterfaceSciFiSoldierHUD/Fonts/Orbitron/Orbitron-ExtraBold SDF.asset` | Embedded `Orbitron-ExtraBold Atlas Material`, fileID `-6419728470944652023` | Settings title/tabs/back, Pause title, Main Menu start/settings/quit | High |
| Heading/Bold | `Assets/Synty/InterfaceSciFiSoldierHUD/Fonts/Orbitron/Orbitron-ExtraBold SDF.asset` for current localized tabs; `Exo2.0-SemiBold SDF.asset` for row/section labels | Orbitron embedded material or Exo SemiBold embedded material | Settings tabs are Orbitron; Settings section row labels are Exo SemiBold | Medium |
| Body/Regular | `Assets/Synty/InterfaceSciFiSoldierHUD/Fonts/exo-2-0/Exo2.0-SemiBold SDF.asset` currently used for Pause description; `Exo2.0-Regular SDF.asset` used for Pause buttons | Embedded `Exo2.0-SemiBold Atlas Material` or `Exo2.0-Regular Atlas Material` | Pause description is Exo SemiBold despite `Body/Regular` descriptor; Pause buttons are Exo Regular | Medium |
| UI/Regular | `Assets/Synty/InterfaceSciFiSoldierHUD/Fonts/exo-2-0/Exo2.0-Regular SDF.asset` | Embedded `Exo2.0-Regular Atlas Material`, fileID `2007113569021227681` | Pause resume/settings/retry/main menu button labels | High |
| UI/Bold | `Assets/Synty/InterfaceSciFiSoldierHUD/Fonts/Font_SciFiSoldier_Bold.asset` | Embedded `SairaCondensed-SemiBold Atlas Material`, fileID `6254369423063020181` | Settings display/input action buttons, keycap labels, dynamic status/value fields | High |
| UI/Utility | `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset` | Embedded `LiberationSans SDF Material`, fileID `2180264` | Resolution hover hint, preview countdown, TMP dropdown arrow, mute toggle labels, save-slot shell text outside requested command shell | High |

Notes:

- `Font_SciFiSoldier_Header.asset` and `Font_SciFiSoldier_Body.asset` exist but were not used by the audited Settings/Pause/Main Menu command shell TMP_Text references.
- `Exo2.0-SemiBold SDF.asset` is the dominant Settings row label/value font. Treating it as Heading/Bold for all headings would not match authored Pause buttons or Main Menu commands.
- `LiberationSans SDF` should not become the primary theme UI font, but it needs either an explicit Utility/Symbol rule or migration replacement decisions for TMP dropdown/helper text.

## 4. Korean Font Baseline

| Category | Candidate | Status | Risk | Recommendation |
|---|---|---|---|---|
| Display/Bold | `NanumGothic SDF` | Existing TMP font asset, 2048x2048 atlas, static population, no fallback table | synthetic bold risk | Use as temporary ko-KR Display/Bold only with QA gate; consider a real Korean bold SDF later |
| Heading/Bold | `NanumGothic SDF` | Existing TMP font asset and currently guarded by font validation tests | synthetic bold risk | Accept for first migration, but keep sparse ko-KR override option open |
| Body/Regular | `NanumGothic SDF` | Suitable baseline candidate for Korean localized body text | acceptable candidate | Use for ko-KR Body/Regular |
| UI/Regular | `NanumGothic SDF` | Suitable baseline candidate for labels, values, compact UI | acceptable candidate | Use for ko-KR UI/Regular |
| UI/Bold | `NanumGothic SDF` | Only one Korean SDF asset exists | synthetic bold risk | Use synthetic bold initially; prefer adding a true Nanum Gothic Bold asset before final polish |

Observed asset details:

| Item | Value |
|---|---|
| TTF | `Assets/_Shared/UI/Fonts/NanumGothic.ttf` |
| TMP font asset | `Assets/_Shared/UI/Fonts/NanumGothic SDF.asset` |
| Source font GUID | `9efe96b63470e314280dc43c0aa565db` |
| Atlas | `2048 x 2048` |
| Atlas population | `Static` (`m_AtlasPopulationMode: 0`) |
| Material | Embedded `NanumGothic SDF Material`, fileID `2769584723452840789` |
| Fallback table | Empty |
| Global TMP fallback | Existing tests state project TMP Settings must not include NanumGothic as global fallback |

## 5. TMP Text Surface Audit

| Surface | Object/Field | Current Font | Material | Size | AutoSize | Min/Max | Proposed StyleTag | SizingSource | Risk |
|---|---|---|---|---|---|---|---|---|---|
| Settings | `_titleLabel` / `SettingsHeader/Title` | Orbitron ExtraBold | Orbitron embedded | 27.1 | on | 17/30 | HeaderLarge | Hybrid | Theme fixed size could alter header fit |
| Settings | `_audioTabButtonLabel`, `_displayTabButtonLabel`, `_inputTabButtonLabel` | Orbitron ExtraBold | Orbitron embedded | 20 | on | 15/20 | HeaderMedium | Hybrid | Tabs use Display font, not Exo heading |
| Settings | `_backButtonLabel` | Orbitron ExtraBold | Orbitron embedded | 20 | on | 10/20 | Button | Hybrid | Button font differs from other Settings buttons |
| Settings | `_mainRow.Label`, `_bgmRow.Label`, `_sfxRow.Label` | Exo SemiBold | Exo SemiBold embedded | 18.6-20 | on | 14/20 | Label | Hybrid | BGM authored size differs |
| Settings | `_mainRow.Value`, `_bgmRow.Value`, `_sfxRow.Value` | Exo SemiBold | Exo SemiBold embedded | 13-18 | on | 10/18 | Value | Hybrid | Dynamic percent/muted strings need autosize preserved |
| Settings | `_currentDisplayLabel`, `_resolutionLabel`, `_fullscreenLabel`, `_languageLabel` | Exo SemiBold | Exo SemiBold embedded | 19-20 | on | 14-16/19-20 | Label | Hybrid | Row layout sensitive |
| Settings | `_currentDisplayValue` | Exo SemiBold | Exo SemiBold embedded | 18 | on | 10/18 | Value | Hybrid | Resolution text length varies |
| Settings | `_languageCycleButtonLabel` | Font_SciFiSoldier_Bold | SairaCondensed embedded | 18 | on | 10/18 | Button | Hybrid | Locale names may clip in ko-KR |
| Settings | `_displayStatusLabel` | Exo Regular | Exo Regular embedded | 14 | on | 10/14 | Status | Hybrid | Dynamic validation/status text |
| Settings | `_previewCountdownLabel` | LiberationSans SDF | LiberationSans embedded | 11 | off | 10/40 | Status | Authored initially | Very small countdown/control overlay |
| Settings | `_resolutionHoverHintLabel` | LiberationSans SDF | LiberationSans embedded | 12 | off | 10/40 | BodySmall | Authored initially | Helper text can wrap/clip |
| Settings | `_applyButtonLabel`, `_revertButtonLabel` | Font_SciFiSoldier_Bold | SairaCondensed embedded | 18 | on | 10/18 | Button | Hybrid | Command labels need localized width check |
| Settings | `_movementLabel`, `_pushLabel`, `_flipLabel` | Exo SemiBold | Exo SemiBold embedded | 20 | on | 10-16/20 | Label | Hybrid | Input row layout sensitive |
| Settings | `_movementToggleLabel` | Exo SemiBold | Exo SemiBold embedded | 13 | on | 10/13 | Label | Hybrid | Compact control label |
| Settings | `_movementCurrentText`, `_pushCurrentText`, `_flipCurrentText` | Font_SciFiSoldier_Bold | SairaCondensed embedded | 15 | off | 10/40 | Value | LocaleInvariant | Raw binding names preserve authored typography |
| Settings | `_pushKeyDisplayLabel`, `_flipKeyDisplayLabel` | Font_SciFiSoldier_Bold | SairaCondensed embedded | 24 | on | 6/24 | Value | LocaleInvariant | Rebinding still updates text; existing keycap autosizing is preserved |
| Settings | Movement `WASDKeyDisplay` 6 TMP + `ArrowKeyDisplay` 2 TMP | Authored key-display fonts | Authored shared materials | authored | authored | authored | Value | LocaleInvariant | Nested physical-key displays use the same explicit participation contract |
| Settings | `_pushChangeButtonLabel`, `_flipChangeButtonLabel`, `_resetButtonLabel` | Font_SciFiSoldier_Bold | SairaCondensed embedded | 18-20 | on | 10/18-20 | Button | Hybrid | Button width and state frame coupling |
| Settings | `_statusText` | Font_SciFiSoldier_Bold | SairaCondensed embedded | 15 | on | 10/20 | Status | Hybrid | Dynamic input validation strings |
| Pause | `_titleLabel` | Orbitron ExtraBold | Orbitron embedded | 30 | on | 14/30 | HeaderMedium | Hybrid | Uses TMP bold style flag plus bold font |
| Pause | `_descriptionLabel` | Exo SemiBold | Exo SemiBold embedded | 18 | on | 12/18 | Body | Hybrid | Body descriptor currently maps to semi-bold font |
| Pause | `_resumeButtonLabel`, `_settingsButtonLabel`, `_retryButtonLabel`, `_mainMenuButtonLabel` | Exo Regular | Exo Regular embedded | 20 | on | 10/20 | Button | Hybrid | Font differs from Settings buttons and Main Menu commands |
| Main Menu | `_startButtonLabel`, `_settingsButtonLabel`, `_quitButtonLabel` | Orbitron ExtraBold | Orbitron embedded | 30 | on | 18/30 | Button | Hybrid | Command shell buttons use Display font |

Localized descriptor keys observed:

| Surface | Element | Descriptor key | Current role/weight |
|---|---|---|---|
| Settings | title | `ui.settings.title` | Title/Bold |
| Settings | tabs | `ui.settings.audio`, `ui.settings.display`, `ui.settings.input` | Subtitle/Bold |
| Settings | input labels | `ui.settings.input.movement_keys`, `ui.settings.input.use_arrow_keys`, `ui.settings.input.push`, `ui.settings.input.flip` | Label/Regular |
| Settings | buttons | `ui.settings.input.change`, `ui.settings.input.reset_input`, `ui.common.back`, language value keys | Button/Regular |
| Settings | dynamic values/status | `ui.settings.audio.volume_value`, `ui.settings.audio.volume_value_muted`, `ui.settings.display.resolution_value`, `ui.settings.display.preview_countdown`, input status keys | Label/Regular |
| Pause | title/description | `ui.pause.title`, `ui.pause.description` | Title/Bold, Body/Regular |
| Pause | buttons | `ui.pause.resume`, `ui.common.settings`, `ui.pause.retry`, `ui.pause.main_menu` | Button/Regular |
| Main Menu | commands | `ui.main_menu.start`, `ui.common.settings`, `ui.main_menu.quit` | Button/Regular |

## 6. Proposed StyleTag Mapping

| Surface | Text element | Proposed StyleTag | Reason | Notes |
|---|---|---|---|---|
| Settings | screen title | HeaderLarge | Primary screen identity text | Maps to Display/Bold in en-US |
| Settings | Audio/Display/Input tabs | HeaderMedium | Major section navigation | Existing font is Orbitron, not Exo |
| Settings | Back button | Button | Command label | Current authoring uses Orbitron; allow per-surface QA |
| Settings | audio row labels | Label | Static row labels | Exo SemiBold authoring |
| Settings | audio percent/muted values | Value | Dynamic value paired with a label | Smart String values |
| Settings | display row labels | Label | Static row labels | Exo SemiBold authoring |
| Settings | resolution/current language values | Value | Dynamic value paired with a label | Preserve autosize |
| Settings | resolution hover hint | BodySmall | Compact helper body text | Keep Authored sizing initially |
| Settings | display status/countdown | Status | Runtime validation/transient text | Countdown may remain authored |
| Settings | apply/revert/language cycle buttons | Button | Command labels | Font_SciFiSoldier_Bold authoring |
| Settings | input row labels | Label | Static row labels | Exo SemiBold authoring |
| Settings | input current/keycap text | Value + LocaleInvariant participation | StyleTag remains semantic metadata; locale theme application is intentionally skipped | Rebinding may replace raw text |
| Settings | input change/reset buttons | Button | Command labels | Font_SciFiSoldier_Bold authoring |
| Settings | input status | Status | Runtime validation/transient text | Dynamic string length risk |
| Pause | popup title | HeaderMedium | Modal title below full screen title scale | Current size 30 |
| Pause | description | Body | Descriptive text | Existing font is Exo SemiBold |
| Pause | resume/settings/retry/main menu | Button | Command labels | Existing font is Exo Regular |
| Main Menu | start/settings/quit | Button | Command labels | Existing command shell uses Orbitron Display font |

## 7. Sizing Policy Recommendation

| StyleTag | Initial SizingSource | Reason | Later Candidate |
|---|---|---|---|
| HeaderLarge | Hybrid | Preserve authored size/autosize while swapping font/material by locale | Theme/Fixed after visual QA |
| HeaderMedium | Hybrid | Settings tabs and Pause title have different scale/fitting needs | Theme/Fixed after QA |
| Body | Hybrid | Avoid popup body clipping across locales | Maybe Theme after QA |
| BodySmall | Authored initially | Helper text is small and layout-specific | Theme only after helper text QA |
| Button | Hybrid | Preserve authored AutoSize ranges and button widths | AutoSizeRange candidate |
| Label | Hybrid | Row layouts are sensitive to label width | Maybe Authored for dense rows |
| Value | Hybrid | Dynamic values vary by locale and runtime content | AutoSizeRange candidate |
| Status | Hybrid | Dynamic status text length varies | AutoSizeRange or Authored |

Initial migration rule: Theme should own Font, Material, and FontStyle. Size, autosize, min/max, line spacing, and character spacing should remain authored unless a tag is explicitly promoted after visual QA.

## 8. Proposed Theme Model

Recommended first-stage asset shape:

```text
GameplayUiTypographyTheme
  BaseRules:
    HeaderLarge
    HeaderMedium
    Body
    BodySmall
    Button
    Label
    Value
    Status

  LocaleFontSets:
    en-US
    ko-KR

  SparseOverrides:
    ko-KR HeaderLarge
    ko-KR HeaderMedium
    optional per-surface Button overrides after QA
```

### Proposed Base Rules

| StyleTag | FontCategory | Weight | ApplyMask | SizingSource | Notes |
|---|---|---|---|---|---|
| HeaderLarge | Display | Bold | Font, Material, FontStyle | Hybrid | Settings title; en-US Orbitron, ko-KR NanumGothic synthetic bold initially |
| HeaderMedium | Heading | Bold | Font, Material, FontStyle | Hybrid | Pause title and Settings tabs; current en-US may need Orbitron override for tabs |
| Body | Body | Regular | Font, Material, FontStyle | Hybrid | Pause description; current source uses Exo SemiBold, so QA needed |
| BodySmall | Body | Regular | Font, Material, FontStyle | Authored | Hover hints and compact helper text |
| Button | UI | Bold or Regular by surface | Font, Material, FontStyle | Hybrid | Current buttons split across Orbitron, Exo Regular, and Font_SciFiSoldier_Bold |
| Label | UI | Regular | Font, Material, FontStyle | Hybrid | Settings row labels currently Exo SemiBold with TMP style flag |
| Value | UI | Regular | Font, Material, FontStyle | Hybrid | Dynamic values and key/current binding values |
| Status | UI | Regular | Font, Material, FontStyle | Hybrid | Display/input status and countdown |

Recommended en-US locale font set:

| FontCategory / Weight | TMP_FontAsset | Material |
|---|---|---|
| Display / Bold | Orbitron ExtraBold SDF | Orbitron embedded material |
| Heading / Bold | Exo2.0-SemiBold SDF by default; allow Orbitron override for Settings tabs | Exo SemiBold or Orbitron embedded material |
| Body / Regular | Exo2.0-Regular SDF default; note current Pause body uses Exo SemiBold | Exo Regular embedded material |
| UI / Regular | Exo2.0-Regular SDF | Exo Regular embedded material |
| UI / Bold | Font_SciFiSoldier_Bold | SairaCondensed embedded material |
| Utility / Regular | LiberationSans SDF | LiberationSans embedded material |

Recommended ko-KR locale font set:

| FontCategory / Weight | TMP_FontAsset | Material |
|---|---|---|
| Display / Bold | NanumGothic SDF | NanumGothic SDF Material; synthetic bold initially |
| Heading / Bold | NanumGothic SDF | NanumGothic SDF Material; synthetic bold initially |
| Body / Regular | NanumGothic SDF | NanumGothic SDF Material |
| UI / Regular | NanumGothic SDF | NanumGothic SDF Material |
| UI / Bold | NanumGothic SDF | NanumGothic SDF Material; synthetic bold initially |
| Utility / Regular | NanumGothic SDF | NanumGothic SDF Material |

## 9. Editor Tooling Requirements

| Tool | Required | Purpose |
|---|---|---|
| TypographyBinding missing check | Yes | Find localized TMP_Text surfaces without a style tag |
| Missing StyleTag in Theme check | Yes | Fail when authored tags have no theme rule |
| Missing LocaleFontSet check | Yes | Fail when a supported locale cannot resolve a font set |
| Invalid FontAsset/Material pair check | Yes | Prevent mismatched font/material assignments |
| en-US / ko-KR preview | Yes | Let designers inspect visual drift before prefab migration |
| Glyph coverage check | Yes | Validate localized UI strings against resolved locale font assets |
| Synthetic bold warning | Yes | Surface ko-KR bold quality risk explicitly |
| Optional bake | No | Useful later, not required for first runtime migration |
| Report export | No | Useful for migration tracking |
| Batch token assignment helper | No | Useful after StyleTag vocabulary stabilizes |

## 10. Risk Matrix

| Risk | Severity | Mitigation |
|---|---|---|
| Existing English font hierarchy is not cleanly three-tiered | High | Model Display, Heading, Body, UI, and Utility categories; allow sparse overrides after QA |
| Settings/Pause/Main Menu current fonts differ | High | Do not force one Button font immediately; preserve authored sizing and audit visual deltas |
| NanumGothic synthetic bold quality may be insufficient | High | Accept only as interim baseline; add true Korean bold SDF or locale overrides before polish |
| MaterialPreset pair is embedded subobject, not separate `.mat` for most used fonts | Medium | Store font/material pair together by asset reference and material reference; validate pair consistency |
| Theme-owned autosize could break layouts | High | Initial `SizingSource = Hybrid`; do not overwrite size/autosize until QA |
| StyleTag misuse could blur semantic differences | Medium | Add editor validation and preview; document StyleTag usage in authoring guide |
| Editor preview absence would hurt designer UX | Medium | Make preview and missing-tag checks part of the first tooling slice |
| `LiberationSans SDF` utility text is still present | Medium | Add Utility handling or explicitly migrate helper/dropdown text later |
| Pause body currently uses Exo SemiBold despite Body/Regular descriptor | Medium | Treat Body/Regular mapping as provisional and inspect visual output in first implementation QA |

## 11. Open Questions

1. Should Settings tabs remain Display/Orbitron through a `HeaderMedium` surface override, or should all `HeaderMedium` move to Exo SemiBold?
2. Should Main Menu command buttons use `Button` with a Display font override, or should a separate `CommandButton` StyleTag be introduced?
3. Should Settings button labels use `Font_SciFiSoldier_Bold` as UI/Bold while Pause buttons remain UI/Regular, or should one button font be selected after visual QA?
4. Should `LiberationSans SDF` helper/dropdown text become a first-class `Utility` category, or should those surfaces migrate to Exo/Nanum in the aggressive pass?
5. Is synthetic bold acceptable for ko-KR HeaderLarge/HeaderMedium/Button in the first production pass?
6. Resolved: keycap/current binding labels retain the existing `Value` StyleTag, while `TypographyLocaleParticipation.LocaleInvariant` is the controlling contract. `Value` is not reinterpreted as a keycap-only locale font and no dedicated tag is required for this decision.
7. Should theme validation inspect runtime-created TMP dropdown template labels, or only authored prefab references?

## 12. Locale-Independent Key Display Closeout

The Settings inventory remains exactly 51 TMP targets: 25 `LocalizedStatic`, 11 `LocalizedDynamic`, 13 `LocaleInvariantKeyDisplay`, and 2 `Decorative`. The former raw-normal exception classification is retired; locale-invariant targets are actively checked for an exact serialized target, one binding per TMP, and `LocaleParticipation == LocaleInvariant`. The other 36 governed targets remain `LocaleThemed`.

Locale switching must preserve each key display's string, font, `fontSharedMaterial`, `fontStyle`, `fontSize`, autosizing flag and range, line spacing, and character spacing across `en-US -> ko-KR -> en-US`. Editor preview follows the same decision and reports actually applied versus intentionally skipped bindings separately.

Word-shaped raw display names such as `Space`, `Left Shift`, `Enter`, `Numpad Enter`, and `Print Screen` follow the same contract. Existing Push/Flip keycap autosizing remains enabled; layout changes are justified only if overflow or wrapping is reproduced.

World Guide movement key TMPs plus Push E and Flip Q remain outside the locale typography path and are unchanged. Updating World Guide E/Q after Settings rebinding is deferred as a separate synchronization feature. Waiting and duplicate action-label sentences also remain separate localization-policy work.

### 2026-07-22 P2 contract hardening

- `TypographyStyleTag` enum validity is now a structural validator contract for both `LocaleThemed` and `LocaleInvariant`; the generic validator does not force every invariant binding to `Value`.
- The Settings production composition guard separately requires all 13 `LocaleInvariantKeyDisplay` bindings to use `TypographyStyleTag.Value`.
- Null-theme Editor preview counts valid invariant targets as skipped before theme resolution, reports one missing-theme error only when a themed binding (or an empty diagnostic root) needs it, and creates no invariant snapshot or mutation.
- Scene Selection preview and restore remove selected descendants when an ancestor is selected. Prefab assets are deduplicated by asset path only for the current call, so independent Scene roots/instances remain distinct and repeated calls still apply.
- Settings live capture and schema-v1 manifest generation both require `typography_bindings=38`; schema and split-log columns are unchanged.

Corrected canonical evidence was generated from revision `bb0f21e2e73f232aaf3fb02833b8e72f88dc526c` through `./run_tests.sh typography-visual` at `TestLogs/TypographyVisualQA/CommandLine-20260724-214429/`. Its schema-v1 manifest records `RECONSTRUCTED_FROM_SPLIT_LOGS`, six 1920x1080 PASS entries, Settings `38` applied / `13` skipped, localized `22/22` for both locales, guarded assets PASS, and verified PNG byte sizes/SHA-256 hashes. The wrapper also preserved the Nanum content hash and pre-existing diff hash.

Main Menu Start, Settings, and Quit now use the dedicated `MainMenuCommand` role. en-US resolves Display/Bold to the origin/main-authored Orbitron ExtraBold font/material, ko-KR retains the NanumGothic locale override, and sizing remains `30 / Auto / 18-30`. The generic `Button -> UI/Bold -> Font_SciFiSoldier_Bold` rule is unchanged. Independent production-composition tests pin all three labels through `en-US -> ko-KR -> en-US`.

Compared with `TestLogs/TypographyVisualQA/CommandLine-20260722-210829/`, five captures are byte-identical and only `MainMenu_en-US.png` intentionally differs. The older directory is retained as historical defective evidence because it contains the incidental SciFiSoldier Main Menu result and did not prove origin/main parity. `TestLogs/TypographyVisualQA/CommandLine-20260720-194045/` remains the separate 51-binding historical evidence. Origin/main lacks the same deterministic capture runner, so cross-revision pixel parity is `NOT_AVAILABLE`; exact runtime identity parity is `PASS`.

Final validation: Settings production localization runtime `25/25` PASS, Settings production typography composition `3/3` PASS, typography fixtures `55/55` PASS, and `./run_tests.sh ui` `872/872` PASS on 2026-07-22 KST.

## 13. Historical Next Implementation Prompt Draft

Implement the first Cascading Typography Theme slice without changing String Tables or localization keys.

Scope:

- Add `TypographyStyleTag`, `FontCategory`, `TypographyWeight`, `SizingSource`, `SizingMode`, and apply-mask types.
- Add `GameplayUiTypographyTheme` with base rules, locale font sets, sparse locale overrides, and resolved style cache.
- Add runtime resolver/applicator path that can apply Font, Material, and FontStyle while preserving authored size/autosize by default.
- Add prefab-side `TypographyBinding` component or serialized binding model for Settings, Pause, and Main Menu command shell only.
- Keep initial sizing policy `Hybrid` for all migrated tags except helper/keycap surfaces that remain authored.
- Use en-US mapping from this audit and ko-KR NanumGothic SDF baseline.
- Add editor validation for missing binding, missing tag, missing locale font set, invalid font/material pair, and glyph coverage.
- Do not claim full lane green unless the full lane is run and passes on the same revision.

Suggested first migrated surfaces:

1. Settings title/tabs/back and Settings row labels/values/status/buttons.
2. Pause title/body/buttons.
3. Main Menu start/settings/quit command labels.

Validation:

- `./run_tests.sh ui --filter UiArchitectureTests`
- targeted localization/typography tests for en-US and ko-KR resolver output
- manual or editor preview evidence for Settings/Pause/Main Menu in en-US and ko-KR
