# Localization and Typography Architecture Direction

## 1. Executive Summary

Localization is implemented to a substantial production baseline. The current project has the Unity Localization package baseline, local/default Addressables settings, `en-US` and `ko-KR` Locale assets, `UI` and `Stage` String Tables, the `UnityStringTableTextResolver` production path, removal of the package-free production fallback, Settings / Pause / Main Menu / Stage display name localization, selected locale persistence, and Settings Smart String dynamic text. The production Localization Settings asset now loads with a valid SmartFormat source/formatter graph, and the Settings audio/display static shell is descriptor-backed in both locales.

Typography foundation and production wiring are implemented for Settings, Pause, and Main Menu.

The implemented typography baseline includes `LocalizedTextDescriptor`, `LocalizedTextRole`, `LocalizedTextWeight`, `LocalizedTypographyStyle`, `LocalizedTmpTextBinding`, `ILocalizedTmpFontResolver`, `TypographyStyleTag`, `FontCategory`, `GameplayUiTypographyTheme`, `LocaleFontSet`, the resolved style cache, `TypographyBinding`, Settings typography migration, Pause / Main Menu typography migration, Editor validation / preview tooling, screenshot capture tooling, and `NanumGothic SDF` glyph coverage generated from Korean String Tables.

Typography is not globally applied to every future UI surface. The current production wiring is scoped to Settings, Pause, and Main Menu. HUD/objective/save slot/inventory/audio/voice localization remains outside this migration.

Current baseline captured for this cleanup pass:

| Item | Value |
|---|---|
| Repo root | `/mnt/c/users/user/2026teamproject_j2m-ui-audio` |
| Branch | `worktree/ui-audio` |
| HEAD before cleanup | `d8bed9d8ef339a3484a44d578de841e4c9aed34e` |
| Worktree status before cleanup | Clean; branch ahead of origin by 80 commits |
| Tracked diff before cleanup | None |
| Staged diff before cleanup | None |
| Untracked files before cleanup | None |
| Cleanup policy | Trailing-whitespace cleanup plus documentation only |

## 2. Current Implemented State

### Project / Asset Foundation

| Area | Status | Current state |
|---|---|---|
| Unity Localization package | Done | Runtime composition uses Unity Localization through the composition adapter. |
| Addressables local/default settings | Done | Addressables settings are local/default; tests guard against remote catalog/path introduction. |
| `en-US` / `ko-KR` Locale | Done | Locale assets exist and are required by `UnityStringTableTextResolver`. |
| `UI` String Table | Done | `UI` collection has `en-US` and `ko-KR` tables for Settings, Pause, and Main Menu shell entries. |
| `Stage` String Table | Done | `Stage` collection has `en-US` and `ko-KR` tables for active stage display names. |
| Localization Settings active registration | Done | Active Localization Settings and its serialized SmartFormat source/formatter graph load successfully and are validated by production integration tests. |
| TMP Settings fallback unchanged | Done | `TMP Settings.asset` does not include `NanumGothic SDF` as a global fallback. |

### Production Resolver

| Area | Status | Current state |
|---|---|---|
| `UnityStringTableTextResolver` production path | Done | Composition creates the Unity adapter when production localization assets are available. |
| `PackageFreeLocalizedTextResolver` production fallback removal | Done | Production runtime source is guarded not to reference `PackageFreeLocalizedTextResolver`. |
| Package-free resolver retained as explicit seam | Done | Package-free resolver remains for tests/fixtures and package-independent boundary tests. |
| Missing key behavior | Done | Resolver falls back to default locale first, then returns `[Table:Key]` deterministic marker. |
| Fail-fast behavior | Done | Production bridge throws when Unity adapter creation fails; package-free fallback is not used in production composition. |

### Settings

| Area | Status | Current state |
|---|---|---|
| Static labels | Done | Settings title, tabs, input labels, language labels, audio headings/mute labels, display labels/actions, and back command use `LocalizedTextDescriptor`. |
| Language row | Done | Display settings owns an authored language row and cycles supported locales at runtime. |
| Runtime locale selection | Done | `IUiLocaleSelectionPort` exposes supported locales and locale switching. |
| Selected locale persistence | Done | Supported locale selections are saved; invalid persisted locale falls back to `en-US`. |
| Audio volume / muted Smart String | Done | Volume value and muted value are Smart String entries with runtime arguments. |
| Display resolution value | Done | Resolution label is resolved through a Smart String descriptor. |
| Display preview countdown | Done | Countdown text is resolved through a Smart String descriptor. |
| Input `rebind_canceled` | Done | Localized descriptor and table entries exist. |
| Input `reserved_key` | Done | Localized descriptor and table entries exist. |
| Input `movement_conflict` | Done | Localized descriptor and table entries exist. |
| Input `already_rebinding` | Done | Localized descriptor and table entries exist. |
| Invalid/default fallback policy guard | Done | Unsupported runtime locale is rejected; invalid persisted locale normalizes to default. |

### Other UI

| Area | Status | Current state |
|---|---|---|
| Pause popup static shell | Done | Pause title, description, resume, settings, retry, and main-menu labels localize and refresh on locale changes. |
| Main Menu command shell | Done | Start, Settings, and Quit command labels localize and refresh on locale changes. |

### Typography Production Wiring

| Area | Status | Current state |
|---|---|---|
| `TypographyStyleTag` | Done | Semantic typography tokens exist for governed TMP labels. |
| `FontCategory` | Done | Display, Heading, Body, UI, Utility, and Symbol categories are represented in the runtime theme model. |
| `GameplayUiTypographyTheme` | Done | ScriptableObject theme owns required locales, base rules, locale font sets, sparse overrides, and cache invalidation. |
| `LocaleFontSet` | Done | Per-locale font/material mappings are centralized by locale, category, and weight. |
| Resolved style cache | Done | `ResolvedTypographyStyleCache` resolves `locale + styleTag` to `ResolvedTmpTypographyStyle`. |
| `TypographyBinding` | Done | Prefabs/views carry style tag, sizing-source override, and optional apply-mask override without owning text keys. |
| Settings typography migration | Done | Settings governed labels are wired through typography bindings while preserving authored sizing policy. |
| Pause / Main Menu typography migration | Done | Pause and Main Menu governed labels use the same typography-binding production path. |
| Editor validation / preview tooling | Done | Theme, binding, preview, validation report, and validation menu tooling exist. |
| Screenshot capture tooling | Done | Typography preview screenshot capture tooling produced the latest 1920x1080 bilingual evidence and canonical `capture.log` manifest in `TestLogs/TypographyVisualQA/CommandLine-20260720-194045/`. Future aggregate captures write the manifest directly and reject partial PASS evidence. |
| NanumGothic glyph coverage | Done | `NanumGothic SDF` coverage was expanded from Korean String Tables and is validated by UI tests. |
| Settings Mute layout fix | Done | Mute label wrapping was corrected after visual QA. |
| Pause description visibility fix | Done | Pause description visibility was restored after visual QA. |

### Localization Blocker Closeout

| Check | Result |
|---|---|
| Production SmartFormat integration | Restored; actual Localization Settings, formatter/source graph, and bilingual Smart Strings pass integration coverage. |
| Settings static shell | Complete for the governed audio/display targets; raw action and physical key names remain intentional non-goals. |
| Localization integration tests | 22/22 passed. |
| Full UI lane | 853/853 passed. |
| Latest visual evidence | `TestLogs/TypographyVisualQA/CommandLine-20260720-194045/` at 1920x1080, with six SHA-256-addressed entries in `capture.log`. |
| Remaining closeout work | Optional P2 Korean synthetic-bold/material polish only; broader Theme sizing and optional bake are not required. |

### Stage

| Area | Status | Current state |
|---|---|---|
| `StagePresentationDefinition.displayNameKey` canonical owner | Done | Presentation definition owns the stage display-name key. |
| Legacy `displayName` fallback removal | Done | Runtime/read-model surfaces propagate display-name keys, not legacy resolved display strings. |
| Stage String Table entries | Done | Active stage display-name keys are validated against the `Stage` String Table. |
| Stage display name descriptor propagation | Done | UI flow converts display-name keys to `LocalizedTextDescriptor` for resolver-owned lookup. |
| `StageResult` title/detail/continue schema not revived | Explicit Non-goal | StageResult remains on the minimal navigation endpoint path; removed result-text schema is not reintroduced. |

## 3. Current Limitations

Typography has moved beyond the foundation-only stage for the governed Settings, Pause, and Main Menu surfaces. The project now has the theme model, locale font sets, style tags, binding authoring, resolved style cache, editor validation / preview, and screenshot capture tooling.

Current limitations are scope and polish limitations, not missing foundation pieces:

| Limitation | Status | Decision |
|---|---|---|
| Global UI coverage | Not complete | Typography is not globally applied to every future UI surface. New surfaces must opt into the same governed binding/theme path. |
| Korean real bold font / material polish | Deferred P2 | `ko-KR` currently accepts NanumGothic plus synthetic bold where mapped. ko-KR synthetic bold/material polish remains optional P2. |
| Broader Theme sizing patch | Deferred | Hybrid sizing preserves authored size/auto-size/min/max for the migrated surfaces; broader theme-owned sizing was not needed for this PR. |
| Optional bake | Deferred | Runtime switching remains canonical; bake is not approved in this PR. |
| General locale-specific Prefab Variants | Deferred | Variants remain reserved for structural layout differences, not ordinary font/text changes. |
| Binary localization DB | Deferred | Unity String Tables are not a proven bottleneck. |
| Dynamic CJK fallback | Deferred | Static atlas coverage from current String Tables remains the predictable baseline. |
| Korean Josa formatter | Deferred | Current migrated UI strings do not need generated Korean postposition logic. |

Deferred localization scope:

| Scope | Status | Reason |
|---|---|---|
| Input waiting action-label string | Deferred | Requires action-name/key display localization policy. |
| Duplicate action-label string | Deferred | Same action-label source policy as waiting text. |
| Invalid/default collapsed fallback | Deferred | Current guard handles invalid locale selection; richer user-facing fallback UX is not defined. |
| Key display names | Deferred | Needs keyboard/gamepad display-name vocabulary and platform policy. |
| Reset confirm payload | Deferred | Confirm popup still accepts raw string payloads. |
| Confirm popup payload | Deferred | Popup payload schema has not moved to descriptors. |
| HUD objective | Deferred | HUD runtime objective text is separate from the Settings/Pause/Main Menu localization baseline. |
| StageResult text schema | Deferred | StageResult result-title/detail/continue text schema intentionally remains removed. |
| Save slot runtime labels | Deferred | Save slot labels are runtime/data driven and need separate label policy. |
| Inventory/runtime item data | Deferred | Content localization needs item identity and generated text rules. |
| Audio/voice localization | Deferred | Audio/voice requires asset table, locale bundle, and Addressables duplication policy. |

## 4. Final Architecture Overview

Text side:

```text
String Table
  |
  v
LocalizedTextDescriptor
  |
  v
UnityStringTableTextResolver
  |
  v
localized string
```

Typography side:

```text
TypographyBinding on Prefab
  |
  v
TypographyStyleTag
  |
  v
Cascading TypographyTheme
  |
  v
LocaleFontSet
  |
  v
Resolved TMP style
  |
  v
LocalizedTmpTextBinding applies to TMP_Text
```

String Table owns sentences. `TypographyTheme` owns visual style. Prefabs own which `StyleTag` they use. Runtime lightly applies already-resolved text and style.

Localization and typography must stay separate:

| Concern | Owner |
|---|---|
| Sentence / key / Smart String arguments | String Table plus `LocalizedTextDescriptor` |
| Locale selection and table lookup | `UnityStringTableTextResolver` |
| Visual role and font mapping | `TypographyTheme` |
| Which style a UI label uses | Prefab-side `TypographyBinding` |
| TMP application | View-side binding/applicator |

## 5. Stage 1: Practical Production Theme

Stage 1 is implemented for the current PR scope. It preserves runtime locale switching, Unity String Tables, and the current production resolver while adding production typography structure for Settings, Pause, and Main Menu.

Stage 1 goals:

| Goal | Policy |
|---|---|
| Keep runtime locale switching | Required |
| Keep Unity String Table | Required |
| Keep `UnityStringTableTextResolver` production resolver | Required |
| Introduce `TypographyStyleTag` | Done |
| Introduce Cascading Theme | Done through `GameplayUiTypographyTheme` base rules and sparse overrides |
| Introduce `LocaleFontSet` | Done |
| Introduce `TypographyBinding` | Done |
| Introduce Editor validation / preview | Done |
| Preserve size / auto-size initially | Done through Hybrid sizing |

### TypographyStyleTag

| Tag | Role |
|---|---|
| `HeaderLarge` | Primary screen title, e.g. Settings title. |
| `HeaderMedium` | Popup title or major panel heading. |
| `HeaderSmall` | Subsection heading with less visual weight. |
| `Body` | Standard paragraph or descriptive body text. |
| `BodySmall` | Secondary description, helper text, or compact popup body. |
| `Button` | Command labels in buttons. |
| `Label` | Static field labels and row labels. |
| `Value` | Dynamic values paired with labels, e.g. current language or resolution. |
| `Status` | Runtime status, validation, warning, or transient feedback text. |
| `Tooltip` | Tooltip or hover/help text. |

### FontCategory

| Category | Role |
|---|---|
| `Display` | Large, identity-heavy title typography. |
| `Heading` | Section and popup headings. |
| `Body` | Paragraph and descriptive text. |
| `UI` | Compact labels, buttons, values, and controls. |
| `Utility` | Utility/control text that does not fit primary display/body categories. |
| `Symbol` | Icon/symbol-like TMP glyph runs when needed. |

### LocaleFontSet

Implemented `en-US` mapping policy:

| Category / Weight | Mapping |
|---|---|
| `Display` / `Bold` | Existing English Header font |
| `Heading` / `Bold` | Existing English Subtitle font |
| `Body` / `Regular` | Existing English Body font |
| `UI` / `Regular` | Existing UI label font |
| `UI` / `Bold` | Existing button/bold UI font |

Implemented `ko-KR` mapping policy:

| Category / Weight | Mapping |
|---|---|
| `Display` / `Bold` | `NanumGothic SDF` plus synthetic bold |
| `Heading` / `Bold` | `NanumGothic SDF` plus synthetic bold |
| `Body` / `Regular` | `NanumGothic SDF` |
| `UI` / `Regular` | `NanumGothic SDF` |
| `UI` / `Bold` | `NanumGothic SDF` plus synthetic bold |

### Cascading TypographyTheme

| Component | Purpose |
|---|---|
| Base Style Rules | Define default font category, weight, sizing, spacing, and default apply mask per `StyleTag`. |
| Locale Font Sets | Map locale plus font category plus weight to TMP font/material profiles. |
| Sparse Locale Overrides | Override only the locale/tag pairs that need different sizing, spacing, weight, or category. |
| Resolved Style Cache | Cache `locale + styleTag` resolution to keep runtime application light. |

Base rules define the default category, weight, sizing, and spacing for each `StyleTag`. Locale overrides must stay sparse; they should exist only when a locale genuinely differs from the base rule.

### SizingSource

| Source | Meaning |
|---|---|
| `Authored` | Prefab owns font size, auto-size, min/max, line spacing, and character spacing. |
| `Theme` | Theme owns sizing and spacing. |
| `Hybrid` | Theme applies font, material, and font style; prefab preserves font size, auto-size, min/max initially. |

Current default is `Hybrid`.

Hybrid means:

| Property | Owner |
|---|---|
| Font / material / fontStyle | Theme |
| `fontSize` / auto-size / min/max | Prefab authored value |
| Sizing rollout | Enabled only after visual QA |

### ApplyMask

| Mask | Meaning |
|---|---|
| `Font` | Apply TMP font asset. |
| `Material` | Apply TMP material preset. |
| `FontStyle` | Apply bold/italic/etc. style bits. |
| `Sizing` | Apply font size and auto-size range. |
| `LineSpacing` | Apply line spacing. |
| `CharacterSpacing` | Apply character spacing. |

Current application focuses on `Font + Material + FontStyle`, with authored sizing preserved by Hybrid policy unless a binding explicitly overrides the policy. Broader `Sizing` rollout remains deferred after visual QA.

### TypographyBinding

`TypographyBinding` is a view-side component attached to prefabs/views. It should not own text keys and should not resolve String Tables.

Fields:

| Field | Purpose |
|---|---|
| `TMP_Text target` | TMP label to style. |
| `TypographyStyleTag tag` | Semantic typography token. |
| `SizingSource` | Authored/theme/hybrid sizing ownership. |
| Optional `ApplyMask` override | Per-binding override when a prefab needs stricter application policy. |

Initial examples:

| UI element | StyleTag |
|---|---|
| Settings title | `HeaderLarge` |
| Settings tab | `HeaderMedium` |
| Settings label | `Label` |
| Settings value | `Value` |
| Settings status | `Status` |
| Pause title | `HeaderMedium` |
| Pause description | `BodySmall` |
| Main Menu command button | `Button` |

## 6. Stage 2: Deferred High-Performance Architecture

Stage 2 is not part of this PR. It becomes useful when UI scale, supported languages, localized content types, or platform packaging complexity grows.

### Optional Editor / Build-time Bake

Purpose:

| Purpose | Detail |
|---|---|
| Reduce static UI runtime cost | Pre-apply style for static UI where runtime switching is not required. |
| Improve WYSIWYG | Designers can inspect final typography in the Editor. |
| Optimize fixed-locale builds | Locale-specific builds can bake a known target locale. |

Policy:

| Policy | Reason |
|---|---|
| Must not break one-build runtime language switching | Current product model supports runtime language switching. |
| Optional optimization, not canonical path | Runtime theme resolution remains the canonical path. |

### Prefab Variant

Purpose:

| Purpose | Example |
|---|---|
| RTL language layout | Arabic/Hebrew layout or hierarchy changes. |
| Locale-specific layout | Language requiring different hierarchy or control grouping. |
| Tutorial image / localized graphic | Image content differs by locale. |
| Hierarchy itself differs | Structural UI change, not just font/label change. |

Policy:

| Policy | Reason |
|---|---|
| Do not use for ordinary label/font changes | It fragments runtime locale switching and increases prefab maintenance. |
| Use only for large structural UI differences | Variants are justified when hierarchy/layout differs by locale. |

### TMP Style Sheet

Purpose:

| Purpose | Detail |
|---|---|
| Inline emphasis | Body text can highlight words without separate labels. |
| Keyword highlight | Tutorial or tooltip terms can have consistent style. |
| Rich text style separation | Reusable inline rich text styles. |

Policy:

| Policy | Reason |
|---|---|
| Support tool only | It complements `TypographyTheme`. |
| Not replacement for Title / Subtitle / Body font system | Whole-label typography must stay governed by StyleTag/theme. |

### Korean Josa Formatter

Purpose:

| Purpose | Examples |
|---|---|
| Korean postposition formatting | 을/를, 이/가, 은/는. |
| Generated content correctness | Item, reward, inventory, tooltip, and generated content strings. |

Adoption timing:

| Timing | Reason |
|---|---|
| Introduce when item/reward/inventory/tooltip/generated content localization begins | Current Settings dynamic strings do not need josa logic. |

### Dynamic Font / Fallback

Purpose:

| Purpose | Detail |
|---|---|
| CJK expansion | Manage larger glyph sets without huge static atlases. |
| User-generated text | Support characters not known from String Tables. |
| Memory optimization | Avoid prepacking all possible glyphs when content grows. |

Current policy:

| Policy | Reason |
|---|---|
| Keep `NanumGothic SDF` static atlas based on String Table charset | Predictable, testable, and enough for the current baseline. |
| Revisit dynamic fallback when glyph count grows or user input appears | Dynamic fallback adds runtime and memory behavior that needs separate validation. |

### Addressables Analyze Guard

Purpose:

| Purpose | Detail |
|---|---|
| Prevent bundle duplication | Required when adding prefab variants, asset tables, tutorial images, or voice localization. |
| Protect local/default packaging assumptions | Localization assets currently rely on local/default Addressables policy. |

## 7. Architectural Decisions and Trade-offs

| Decision | Adopted? | Reason | Trade-off |
|---|---|---|---|
| Cascading Styles | Adopted | Prevents data explosion by letting locale/tag overrides stay sparse. | Requires validation and resolved-style caching. |
| Prefab Variant | Limited adoption | Appropriate only when locale changes the hierarchy or layout structure. | Poor fit for ordinary runtime locale switch UI and increases prefab maintenance. |
| Editor Tooling | Adopted | Complements Unity WYSIWYG and gives designers preview/validation feedback. | Requires tooling implementation and upkeep. |
| Bake | Optional adoption | Can optimize static UI or fixed-locale builds. | Can conflict with one-build runtime language switching if treated as canonical. |
| StyleTag instead of Slot | Adopted | Reusable semantic tokens scale better than one-off prefab slots. | Requires token governance and periodic audit to avoid tag sprawl. |
| Binary localization DB | Deferred | Unity String Table is not currently proven to be a bottleneck. | Revisit only if large table performance or memory becomes measurable problem. |
| Dynamic Font fallback | Deferred | String Table charset plus static `NanumGothic SDF` is more predictable today. | Revisit for larger CJK scope or user input. |
| Korean Josa formatter | Deferred | Current Settings dynamic strings do not need it. | Content localization will need it for generated Korean text. |

## 8. Data Model / Runtime Model

Implemented model shape:

```csharp
public enum TypographyStyleTag
{
    Default,
    HeaderLarge,
    HeaderMedium,
    HeaderSmall,
    Body,
    BodySmall,
    Button,
    Label,
    Value,
    Status,
    Tooltip
}

public enum FontCategory
{
    Display,
    Heading,
    Body,
    UI,
    Utility,
    Symbol
}

public enum TypographySizingSource
{
    Authored,
    Theme,
    Hybrid
}

public enum TypographySizingMode
{
    PreserveAuthored,
    Fixed,
    AutoSizeRange
}

[Flags]
public enum TypographyApplyMask
{
    None = 0,
    Font = 1 << 0,
    Material = 1 << 1,
    FontStyle = 1 << 2,
    Sizing = 1 << 3,
    LineSpacing = 1 << 4,
    CharacterSpacing = 1 << 5
}
```

Theme structure:

```text
GameplayUiTypographyTheme
  - baseRules
  - localeFontSets
  - localeOverrides
  - resolvedStyleCache
```

Runtime flow:

```text
TMP_Text + TypographyBinding
  |
  v
styleTag
  |
  v
TypographyTheme.Resolve(locale, styleTag)
  |
  v
ResolvedTmpTypographyStyle
  |
  v
LocalizedTmpTextBinding.Apply
```

The existing `LocalizedTextDescriptor` remains the text-side descriptor. Production typography identity is selected through `TypographyStyleTag` and the theme path; `LocalizedTextRole` / `LocalizedTextWeight` remain foundation metadata and compatibility inputs rather than final font identity.

## 9. Unity Editor / Prefab Authoring Policy

Prefab owns:

| Prefab-owned data | Policy |
|---|---|
| `TMP_Text` object | The prefab owns the label object and references. |
| `TypographyBinding` | The prefab/view owns the binding component. |
| `StyleTag` | The prefab declares the semantic typography role. |
| `RectTransform` / layout | Layout remains prefab-authored. |
| Initial size / auto-size | Owned by prefab when `SizingSource` is `Authored` or `Hybrid`. |

Theme owns:

| Theme-owned data | Policy |
|---|---|
| Font asset | Centralized in locale font sets. |
| Material preset | Centralized and validated with matching font asset. |
| Locale-specific font mapping | Centralized by locale/category/weight. |
| Optional sizing override | Allowed after visual QA and only when enabled by sizing policy. |
| Line spacing / character spacing | Theme-owned only when the apply mask enables it. |

Implemented Editor tooling:

| Tooling | Purpose |
|---|---|
| Missing `TypographyBinding` check | Find TMP labels that should participate in the theme but do not. |
| Missing style tag check | Prevent default/empty tags from leaking into production UI. |
| Missing locale font set check | Fail when required locale/category/weight mapping is absent. |
| Invalid font/material pair check | Prevent material presets from being paired with incompatible TMP font assets. |
| `en-US` / `ko-KR` preview | Let authors inspect both supported locales in Editor. |
| Glyph coverage check | Report characters missing from the selected static atlas. |
| Build-time validation | Stop broken theme or font mappings before player build. |
| Screenshot capture tooling | Capture Settings, Pause, and Main Menu typography previews for visual QA evidence. |

Optional Editor tooling:

| Tooling | Purpose |
|---|---|
| Static UI bake | Pre-apply static UI styles for optimization. |
| Prefab style apply button | Let authors apply previewed theme values to selected prefab labels. |
| Report export | Produce audit inventory for review and migration tracking. |

## 10. Validation and Test Strategy

Localization validation:

| Validation | Expected result |
|---|---|
| UI / Stage table completeness | Required entries exist for `en-US` and `ko-KR`. |
| Smart String placeholder validation | Placeholder count and Smart String flags match descriptor usage. |
| Missing key deterministic marker | Missing key returns `[Table:Key]`. |
| Production resolver Unity adapter only | Production composition creates `UnityStringTableTextResolver`. |
| Package-free resolver production fallback absence | Runtime production source does not reference package-free resolver. |

Typography validation:

| Validation | Expected result |
|---|---|
| Required `StyleTag` coverage | Every governed TMP label has a non-default tag. |
| `en-US` / `ko-KR` `LocaleFontSet` completeness | Required category/weight entries exist for both locales. |
| Missing style entry fail | Missing base or resolved style fails validation. |
| Invalid material/font pair warning or fail | Incompatible material preset is reported before runtime. |
| PreserveAuthored sizing keeps `fontSize` / auto-size / min/max | Hybrid or authored sizing does not overwrite prefab sizing. |
| `ko-KR` NanumGothic application | Korean locale applies `NanumGothic SDF` where mapped. |
| `en-US` original font/category preservation | English mapping preserves the existing authored hierarchy. |
| No runtime material instancing | Runtime applies shared material presets, not per-label material instances. |
| Full UI lane pass | UI lane must pass for typography migration changes, unless explicitly not run with reason. |

Editor validation:

| Validation | Expected result |
|---|---|
| Prefab `TypographyBinding` coverage | Governed prefabs report missing bindings. |
| Missing glyph report | Missing glyphs are listed with locale/string source. |
| Preview smoke | `en-US` and `ko-KR` previews render non-null fonts/materials. |

## 11. Deferred Scope

| Scope | Deferred reason |
|---|---|
| Input waiting action-label string | Needs localized action label and input binding display-name policy first. |
| Input duplicate action-label string | Same dependency as waiting action-label localization. |
| Invalid/default collapsed fallback | Requires UX decision for collapsed fallback display instead of resolver-level guard only. |
| Key display names | Needs per-platform keyboard/gamepad display-name vocabulary. |
| Reset confirm payload | Confirm popup payload still uses raw strings and needs descriptor migration. |
| Confirm popup payload | Popup-wide localization schema should be handled as a separate UI migration. |
| HUD objective | Objective text is gameplay/runtime content and needs content localization policy. |
| StageResult title/detail/continue label | Removed schema should not be revived without a new StageResult product requirement. |
| Save slot runtime labels | Save metadata needs data-driven runtime label policy and date/number formatting. |
| Inventory/runtime item data | Item identity, generated text, and plural/josa rules are not defined. |
| Audio/voice localization | Requires localized asset tables, bundle policy, and voice selection flow. |
| Binary localization DB | Unity String Table has not been shown to be a bottleneck. |
| Full build-time localized prefab bake | Runtime switching remains the canonical path. |
| General locale-specific prefab variants | Variants are reserved for structural differences, not normal font/label changes. |
| Dynamic CJK fallback | Static atlas remains more predictable for current String Table scope. |
| Korean Josa formatter | Needed later for generated Korean content, not current Settings strings. |

## 12. Migration Roadmap

| Phase | Name | Work |
|---|---|---|
| Phase 1 | Typography Audit | Done for Settings / Pause / Main Menu. |
| Phase 2 | Theme Model | Done: `StyleTag`, `FontCategory`, `LocaleFontSet`, base rules, sparse overrides, and resolved cache exist. |
| Phase 3 | Editor Validation | Done: missing tag/profile, invalid material pair, glyph coverage, preview, and report tooling exist. |
| Phase 4 | Settings Migration | Done: `TypographyBinding`, Hybrid sizing, existing `en-US` font preservation, and `ko-KR` NanumGothic path are wired. |
| Phase 5 | Pause / Main Menu Migration | Done: Pause and Main Menu use the same typography-binding pattern. |
| Phase 6 | Visual QA | Done for PR scope; closeout evidence is recorded in `Docs/Architecture/Typography-Visual-QA-Closeout.md`. |
| Phase 7 | Optional Optimization | Deferred: bake, prefab variant, dynamic fallback, Josa formatter, and Addressables Analyze remain future-only unless separately approved. |

## 13. Glossary

| Term | Meaning |
|---|---|
| `LocalizedTextDescriptor` | Text-side descriptor containing table, key, role/weight foundation metadata, and runtime arguments. |
| String Table | Unity Localization table that owns localized sentences and Smart String entries. |
| `UnityStringTableTextResolver` | Production resolver that reads Unity String Tables and applies locale fallback/missing-key policy. |
| `PackageFreeLocalizedTextResolver` | Test/fixture resolver retained as an explicit seam, not production fallback. |
| `LocalizedTypographyStyle` | Current foundation style carrying font size, line spacing, and bold flag. |
| `LocalizedTmpTextBinding` | View-side runtime binding that applies resolved text and current foundation typography/font to `TMP_Text`. |
| `TypographyStyleTag` | Implemented semantic token used by prefabs to request a typography style. |
| `TypographyTheme` | Implemented through `GameplayUiTypographyTheme`, resolving locale plus style tag to TMP font/material/style. |
| `LocaleFontSet` | Implemented per-locale mapping from font category/weight to TMP font/material profile. |
| `SizingSource` | Implemented ownership policy for prefab-authored sizing versus theme-applied sizing. |
| `ApplyMask` | Implemented bitmask controlling which style properties are applied by the theme. |
| Bake | Optional Editor/build-time process that pre-applies static typography values. |
| Prefab Variant | Locale-specific prefab hierarchy/layout variant, reserved for structural differences. |

## 14. Open Questions / Follow-up Decisions

| Question | Owner / next step |
|---|---|
| Should `LocalizedTextRole` / `LocalizedTextWeight` be retired after broader `TypographyStyleTag` migration? | Keep as foundation/compatibility metadata for now; revisit only during broader UI migration. |
| When does sizing move from Hybrid to Theme-owned? | Deferred until a broader Theme sizing patch is explicitly approved and visually validated. |
| Do Korean bold styles need real bold TMP assets instead of synthetic bold? | Deferred P2; ko-KR synthetic bold/material polish remains optional P2. |
| When should Addressables Analyze become mandatory for localization assets? | Stage 2, once asset tables, prefab variants, tutorial images, or voice assets are introduced. |
