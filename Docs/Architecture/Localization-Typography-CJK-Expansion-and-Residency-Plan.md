# Localization Typography CJK Expansion and Residency Plan

## 1. Document Status

| Item | Status |
|---|---|
| Document type | Proposed implementation and decision plan |
| Current production locales | `en-US`, `ko-KR` |
| Approved Draft scope after the en/ko gate | Japanese `ja-JP`, Simplified Chinese `zh-CN` |
| Out of scope | Traditional Chinese and an ambiguous shared `zh` locale |
| Runtime implementation | Phase 2 catalog/selection policy, Phase 3 read-only string governance, and Phase 4 production selection-policy wiring are implemented; Phase 5 option-model implementation has not started |
| Locale/font asset import | Not authorized by this document |
| Audit basis | Initial repository audit on 2026-09-13; Phase 1-4 source/execution review and Phase 5 option/autonym/font-boundary review on 2026-09-14 |
| Execution progress | Phases 0-4 complete; Phase 5 raw-autonym direction is approved and is the next gated implementation phase |

This plan supplements
[Localization-Typography-Architecture-Direction.md](./Localization-Typography-Architecture-Direction.md)
and does not replace the current-state contract or the
[KBO Dia Gothic closeout](./KBO-Dia-Gothic-Typography-Migration-Closeout.md).
It records what should be changed, why each step exists, the trade-offs, and the
evidence required before Japanese or Chinese becomes a ship-ready locale.

## 2. Objective and Non-goals

The objective is to make localization cost scale mainly with the selected
locale, rather than with every font added to the project. This matters more for
CJK fonts than for the current String Tables because large TMP atlases dominate
the likely residency cost.

This is a scalability change, not a response to a current lag complaint. In the
currently supported `en-US`/`ko-KR` flows, users report no perceptible startup,
scene-entry, or locale-switch stall. No formal timing campaign has been run, so
this observation must not be restated as a measured zero-cost result. The reason
to change the architecture is that the current strong-reference graph and
two-locale assumptions do not scale cleanly when Japanese and Chinese fonts,
tables, and selection options are added.

The plan must preserve these product behaviors:

- one Player build can switch locale at runtime;
- a locale change never exposes a partially updated mixture of text and fonts;
- a failed change keeps or restores the last usable locale;
- physical-key display text keeps its existing locale-invariant contract;
- missing content is detected before release, not by loading every locale during
  ordinary Player startup;
- UI presentation remains outside authoritative gameplay mutation.

This plan does not select translators, approve font licenses, define final
Japanese/Chinese copy, or require locale-specific prefabs. It also does not
claim a measured memory saving before same-revision Player evidence exists.

### 2.1 Measurement deferral decision

The original plan placed a full Player baseline campaign before architecture
work. That campaign is intentionally deferred for the current execution order.

Reasons:

- there is no current user-perceived stall requiring diagnosis or immediate
  latency remediation;
- the unwanted all-locale font dependency is already proven by serialized
  strong references, so a Profiler capture is not required to decide whether the
  dependency boundary should be corrected;
- the current two-locale memory result cannot predict Japanese/Chinese cost
  before their font families, glyph corpora, and atlas strategies are selected;
- the current synchronous resolver has no stable transactional transition
  markers, so a detailed probe built now would be partly replaced by the later
  FontSet coordinator;
- the repository does not currently include the Memory Profiler package or a
  localization-specific Player capture lane, making a formal campaign a
  non-trivial feature rather than a quick prerequisite.

Deferral does not mean that measurement has no value. It means measurement is
not the admission gate for fixing a known scalability boundary. Formal Player
measurement remains mandatory before `ja-JP` or `zh-CN` is promoted to
ShipReady and before any quantified latency or memory-improvement claim.

## 3. Audited Current State

The following is the current structural baseline, not the target state.

| Area | Current behavior | Consequence |
|---|---|---|
| Locale definitions | All configured Locale objects are registered | Small cost grows with locale count and is expected |
| String Tables at resolver creation | Project code synchronously checks the `en-US` and `ko-KR` `UI` and `Stage` tables | Non-selected tables can be loaded during scene/resolver creation |
| String Tables after a real locale change | Unity Localization releases cached tables and preloads the selected locale/fallbacks | String tables do not necessarily accumulate forever |
| Localization initialization | Synchronous initialization and synchronous table access are enabled | Blocking is possible in principle, but no current user-perceived stall is reported |
| Theme font references | One `GameplayUiTypographyTheme` strongly references English and Korean font sets | Loading the theme makes both locale font sets dependencies |
| Prefab font references | Production UI prefabs also serialize direct English TMP font references | Theme separation alone cannot guarantee English font unload in Korean |
| Typography cache | Locale by 19-style resolution is built once and remains small | No evidence of material per-frame cost growth |
| Locale selection UI | Some display logic is still expressed as an `en-US`/`ko-KR` choice | Adding a third locale requires data-driven options |

Important interpretation:

- `m_PreloadBehavior: 2` is selected-locale-and-fallback preload behavior in the
  installed Localization package; it is not an all-locales preload setting.
- Addressables handle release proves logical release, not immediate operating
  system RSS reduction.
- The current two Korean 2048 x 2048 Alpha8 atlases have 8 MiB of nominal atlas
  data in total. Readability may add CPU residency, but exact Player CPU/GPU
  savings require measurement.
- A YAML file's serialized text size is not a Player install-size estimate.

## 4. CJK Decisions Required Before Draft Import and Promotion

The en/ko structure-first phases do not require final CJK font, glyph, atlas,
layout, or license decisions. The locale identities `ja-JP` and `zh-CN` and the
exclusion of Traditional Chinese are already fixed for this plan. The remaining
content decisions become mandatory at Phase 19/20 Draft import and Phase 21
ShipReady promotion, not before the generic runtime architecture work begins.

### 4.1 Locale identity

The approved Draft scope is:

- Japanese: `ja-JP` Draft after the en/ko structural gate;
- Simplified Chinese: `zh-CN` Draft after Japanese is introduced independently;
- Traditional Chinese: outside the current scope and requires a new decision.

“Chinese” must not later be expanded into an ambiguous shared Locale. If
Traditional Chinese is approved, it receives a separate code, copy, FontSet,
QA, and promotion path.

The final codes must match Unity Locale assets, String Table entries, saved
preferences, Addressables keys, analytics, and platform metadata. Codes must not
be chosen independently in each subsystem.

### 4.2 Ship-ready versus draft locales

Every configured locale receives one lifecycle state:

| State | Player exposure | Validation requirement |
|---|---|---|
| `Draft` | Hidden from normal locale selection | Partial copy/assets allowed; failures remain visible to developers |
| `ShipReady` | User-selectable | Complete required tables, valid FontSet, glyph coverage, layout QA, license approval, and Player evidence required |

A Draft locale must not weaken the release gate for ShipReady locales. A
ShipReady locale must not silently depend on another CJK locale merely because
some glyphs appear visually similar.

Draft has three distinct operational states:

| State | Registration and access | Purpose |
|---|---|---|
| Authoring-known Draft | Present in the package-free catalog and authoring data, absent from production `AvailableLocales` | Allow incomplete tables, FontSet work, and deterministic validator diagnostics |
| Candidate validation Draft | Included only by an explicit development/candidate build profile and selected through a development-only entry point | Run real Player transition, visual, packing, and memory admission without exposing it in normal Settings |
| Production `ShipReady` | Registered in production `AvailableLocales`, exposed by the normal ordered option model | Shippable runtime selection and persistence |

The candidate selector must be excluded from production builds and must not
reuse command-line or hidden paths that can accidentally admit arbitrary Draft
locales. Promotion is one reviewed change that updates lifecycle metadata,
production registration, option exposure, packing/build metadata, and restart
persistence together.

The initial build-profile names are `Production`,
`LocalizationCandidate-ja-JP`, and `LocalizationCandidate-zh-CN`. A candidate
profile carries an allowlist of exactly one Draft code and enables a
development-only selector; it is never release-signed or distributed as the
production Player. A build-time check fails if a production artifact contains
the selector, a Draft allowlist, or a Draft locale registration.

### 4.3 Glyph and atlas strategy

Each locale needs an explicit choice between:

| Strategy | Advantages | Costs and risks |
|---|---|---|
| Curated static atlas | Predictable output, deterministic build, no runtime atlas mutation | Corpus maintenance; large CJK coverage can require large/multiple atlases; missing generated text must be caught early |
| Dynamic atlas | Can add glyphs used by runtime/generated content | Runtime CPU/memory spikes, writable texture requirements, cache persistence complexity, device-dependent first-use stalls |
| Static primary plus governed fallback | Keeps common UI deterministic while covering bounded exceptions | More resident dependencies and harder proof that an unexpected system font is not used |

“Curated static atlas” means that the team first defines a governed glyph
corpus, generates immutable TMP SDF atlas assets from that corpus during
authoring/build preparation, reviews the output, and ships those generated
assets. The Player neither adds glyphs on first use nor mutates the atlas at
runtime. The corpus must therefore include all String Table text, SmartFormat
outputs, numbers/punctuation, runtime-generated content, and any allowed user
input; missing glyphs are a content/validation failure, not a runtime-growth
strategy.

The current Korean static-atlas approach is not automatically the correct
Japanese/Chinese choice. The decision must include all String Table text,
SmartFormat outputs, numbers/punctuation, runtime-generated content, and any
user-entered text actually rendered by the governed UI.

### 4.4 Font license and visual policy

Before importing a production font, record:

- redistribution and game embedding permission;
- whether generated TMP SDF atlas/material assets may be distributed;
- modification, subsetting, and font-name requirements;
- attribution/notice requirements;
- supported glyph repertoire for the selected Japanese or Chinese variant;
- approved use for product identity versus ordinary UI text.

License approval and typography suitability are separate gates. A legally usable
font can still fail readability, weight hierarchy, punctuation, or brand review.

## 5. Target Runtime Architecture

```text
Common Typography Policy
  - 19 semantic style rules
  - sizing / spacing / style policy
  - no locale-specific TMP font object references
              |
              v
Locale Transition Coordinator
  - explicit candidate load handle
  - validation and 19-style cache build
  - atomic commit / rollback
  - old-handle release after binding, layout/mesh, and rendered-frame barriers
              |
              v
Localization Asset Table: TypographyFontSet
  - en-US -> English FontSet asset
  - ko-KR -> Korean FontSet asset
  - ja-JP -> Japanese FontSet asset (future)
  - zh-CN -> Simplified Chinese FontSet asset (future)
```

The recommended asset lookup is a Unity Localization Asset Table because it
already models locale selection. Runtime ownership should nevertheless keep an
explicit load handle and transition coordinator. Depending only on
`LocalizedAsset<T>.AssetChanged` makes rollback, error propagation, and the
exact old-handle release point harder to prove.

The committed runtime typography state should contain:

- committed locale identity;
- current FontSet asset and its owned handle;
- an immutable resolved cache for the 19 style tags;
- a monotonically increasing transition token or equivalent stale-result guard.

Only the coordinator commits or replaces this state. Views consume it and must
not independently load/release FontSets.

The Typography Asset Table and its entries are never preload assets. The
coordinator's explicit candidate-locale operation is the sole runtime owner of
FontSet leases; it must not be combined with `LocalizedAsset<T>.AssetChanged`
or another automatic ownership path. String Table preload policy is configured
and audited separately from FontSet Asset Table policy.

Assembly ownership is fixed as follows:

| Responsibility | Assembly boundary |
|---|---|
| Locale catalog DTOs, lifecycle and transition status/result | package-free `UI.ViewShared` |
| Settings apply/error orchestration | `UI.Application` |
| FontSet asset schema, TMP resolved style, compiler-facing contract | `UI.Screens` or another lower-level TMP-aware assembly that does not reference Composition |
| Unity Localization adapter, Asset Table load operation, lease/coordinator ownership | `UI.Composition` |
| Build Layout and font-graph validation | `UI.Composition.Editor` |

`UI.ViewShared`, `UI.Application`, and `UI.Flow` must not acquire Localization or
Addressables dependencies. Views, popups, and HUD code must not own handles,
and `AsyncOperationHandle` must not escape Composition. If direct Addressables
APIs are used, the Composition assembly reference is added explicitly without
reversing the existing Composition-to-Screens dependency direction.

## 6. Canonical Structure-first Application Sequence

The previous Stage 0-10 order is replaced by the sequence below. The main
corrections are:

- string governance validation is introduced before runtime cross-locale probes
  are removed;
- font-asset validation is introduced only after the FontSet schema exists;
- application-visible committed locale state is separated from the package raw
  selected-locale event before atomic transition work;
- the single-locale cache contract is defined before loader/coordinator commit;
- the coordinator is proven with the existing in-memory Theme adapter before
  Addressables becomes the production source;
- prefab/captured direct references are removed only after runtime font
  readiness exists;
- a real en/ko packed structural gate is required before CJK import, while the
  heavy performance campaign remains deferred;
- the first same-revision packed Player gate runs before legacy code/assets are
  retired, so failure still has a bounded composition rollback;
- Draft authoring, candidate Player validation, and production promotion use
  separate registration paths.

The required dependency order is:

~~~text
current en/ko compatibility contract
  -> pure locale catalog and selection policy
  -> string governance validator
  -> ShipReady-only runtime gate and data-driven options
  -> runtime cross-locale probe removal
  -> committed-locale application seam
  -> FontSet schema and single-locale cache
  -> in-memory coordinator and binding registry
  -> bootstrap/direct-reference cleanup
  -> shadow en/ko Addressables authoring and packing proof
  -> explicit Asset Table lease loader
  -> production cutover with legacy rollback retained
  -> en/ko packed Player pre-retirement gate
  -> rollback-window close and legacy graph retirement
  -> post-retirement structural recheck
  -> ja-JP Draft
  -> zh-CN Draft
  -> per-locale formal Player admission and ShipReady promotion
~~~

### Phase 0 — Fix scope and claim boundaries

What is being done:

- state that this is an extensibility change, not current lag remediation;
- keep en-US and ko-KR as the only current ShipReady locales;
- approve `ja-JP` and `zh-CN` as the later Draft identities while keeping
  Traditional Chinese out of scope;
- keep en-US as the project default and emergency text fallback;
- define Draft as non-selectable in normal production UI;
- define authoring-known, candidate-validation, and production ShipReady
  registration modes;
- defer the heavy pre-change Player performance campaign;
- prohibit exact MiB, millisecond, RSS, and CJK-budget claims without later
  Player evidence.

CJK fonts, glyph corpora, atlas sizes, and final layout exceptions are not
required to begin the en/ko structure work.

Policy decisions closed by this phase:

- production `AvailableLocales` contains ShipReady locales only;
- a development/candidate build profile is the only real-Player route for a
  Draft locale before promotion;
- canonical preference precedence is valid persisted ShipReady code (including
  an explicit alias), then valid Unity-selected ShipReady locale, then `en-US`;
- codes are matched by exact canonical identity plus an explicit alias table,
  not by arbitrary case or culture normalization;
- ShipReady UI/Stage content must be complete; English fallback is an emergency
  runtime exception and never satisfies release completeness;
- language display names are autonyms in stable catalog order unless a later UX
  decision explicitly adopts localized names and their N-by-N translation cost;
- the initial CJK atlas direction is curated static per locale, subject to
  reopening when the governed corpus cannot fit the approved budget.

Completion gate: the approved scope distinguishes structural evidence from
performance evidence and identifies the owner of lifecycle, fallback,
persistence, display-name, candidate-build, and promotion policy.

Phase 0 execution record (2026-09-14):

| Policy surface | Closed decision | Accountable owner |
|---|---|---|
| Lifecycle | `en-US` and `ko-KR` remain the only current `ShipReady` locales; `ja-JP` and `zh-CN` are approved only as later independent Draft identities | Product/localization owner, with UI engineering owning catalog enforcement |
| Default and emergency fallback | `en-US` remains both the project default and the emergency runtime text fallback; fallback never satisfies ShipReady completeness | UI engineering/localization |
| Preference precedence | Valid persisted ShipReady code or explicit alias, then a valid Unity-selected ShipReady locale, then `en-US`; arbitrary culture/case normalization is prohibited | UI engineering |
| Display name and ordering | Locale autonyms use stable catalog order; localized display names require a later explicit UX decision and its N-by-N copy cost | Product/localization/UX |
| Draft exposure | Authoring-known Draft locales remain outside production `AvailableLocales` and normal Settings selection | UI engineering/localization |
| Candidate Player access | Only the named development/candidate build profile may register and select its one allowlisted Draft locale; the selector and allowlist are excluded from production artifacts | Build/QA |
| ShipReady promotion | Promotion is one reviewed change spanning lifecycle metadata, production registration, Settings exposure, packing metadata, and restart persistence | Product/localization/UX for approval; UI engineering and Build/QA for atomic implementation and proof |
| CJK atlas direction | Start with a curated static atlas per locale; reopen the strategy if the governed corpus cannot meet the later approved budget | UI engineering/technical art |
| Evidence and claims | The heavy pre-change performance campaign is deferred; no exact MiB, millisecond, RSS, CJK budget, or before/after improvement claim is allowed without same-revision Player evidence | Engineering/QA performance owner |

Phase 0 is complete as a policy/documentation gate only. It changes no runtime,
locale registration, String Table, font, Addressables, Settings option, or
Player artifact. Phase 1 captures the current en/ko compatibility and dependency
inventory below before any production asset or runtime change.

### Phase 1 — Lock current en/ko compatibility and structural facts

What is being done:

- preserve current en/ko text results, option order, display names, same-locale
  no-op, unknown-locale rejection, valid preference restore, and emergency
  fallback;
- preserve the current semantic typography output for every governed style;
- inventory Scene, Catalog, Theme, prefab, captured fallback, TMP default, and
  Resources font dependency paths;
- record Localization and Addressables settings and current atlas metadata;
- map old storage-shape assertions to the new behavioral or structural tests
  that will replace them.

Why: exact assumptions such as two FontSets, seven mappings, or a 38-entry cache
describe the old storage shape and must not survive as accidental product
contracts.

Completion gate: no runtime or production asset change; current en/ko
compatibility tests and the source-state inventory are reviewable. Deliberate
normalization of invalid persisted values in Phase 6 is not treated as an en/ko
compatibility regression.

#### Phase 1 source-state inventory (2026-09-14)

Inventory basis: revision `47cbea9fa6f7a02b13e71ce41d179d5307190ca8`
plus the documentation-only working tree recorded by this plan. No runtime,
Scene, Prefab, ScriptableObject, Localization, Addressables, TMP, or font asset
was changed while producing this inventory.

Current en/ko compatibility contract:

| Surface | Current behavior to preserve through the structure-first migration | Primary executable evidence |
|---|---|---|
| Production locale set and order | `AvailableLocaleCodes` is `en-US`, then `ko-KR`; both are currently selectable and the Settings cycle wraps in that order | `SettingsLocalizationFoundationTests.PackageFreeResolver_LocaleSelectionPort_SupportsOnlyEnglishAndKorean`; `SettingsProductionLocalizationRuntimeTests.GameplayScreenRuntimeFactory_SettingsRuntime_LanguageCycleSwitchesLocaleRefreshesLabelsAndFont` |
| Display names | Settings displays the autonyms `English` and `한국어`; the presenter and view currently choose between two dedicated descriptors with a `ko-KR` branch | `UnityLocalizationStringTableIntegrationTests.RuntimeSettings_LanguageRowSwitchesThroughUnityAdapterAndKeepsKoreanFont`; production composition round-trip tests |
| Same-locale request | A request for the already committed locale succeeds without `LocaleChanged` or preference write | `UnityLocalizationStringTableIntegrationTests.CommandLineSelectedLocale_IsNotOverwritten_WhenNoPreferenceExists` |
| Unsupported request | `TrySetLocale` returns false, keeps the current locale, emits no successful persistence, and does not silently normalize another culture | `UnityLocalizationStringTableIntegrationTests.UnityStringTableTextResolver_ResolvesAndSwitchesSupportedLocales`; `UnityStringTableTextResolver_UsesExistingPersistencePolicy` |
| Startup precedence | Valid persisted en/ko overrides Unity selection; absent/invalid/malformed persistence preserves a valid Unity-selected en/ko locale; otherwise startup selects `en-US` | startup tests at lines 610-755 of `UnityLocalizationStringTableIntegrationTests.cs` |
| Text fallback | Resolution tries the committed locale, then `en-US`, then returns `[Table:Key]`; fallback is runtime recovery and not a completeness pass | `UnityStringTableTextResolver.Resolve`; missing-key and package-free parity tests |
| Text transition | Settings, Pause, Main Menu, Stage names, objectives, dynamic Settings values, and terminal copy refresh through the resolver locale event | `UnityLocalizationStringTableIntegrationTests` plus the Settings/Pause/Main Menu/HUD/terminal localization fixtures |
| Physical keys | The 10 Settings physical-key TMP targets preserve raw Input System text and every authored typography property through `en-US -> ko-KR -> en-US` | `SettingsProductionTypographyCompositionTests.SettingsPrefab_TypographyInventory_ClassifiesLocaleInvariantKeyDisplaysAndLocksGovernedTargets` and its round-trip assertions |
| Typography | All 19 semantic tags resolve for both current locales; ko-KR uses KBO Dia Gothic Light for 9 roles and Medium for 10 roles, Normal style, Hybrid sizing, and no sizing apply mask | `KboDiaGothicTypographyContractTests.ProductionTheme_ResolvesKoreanHierarchyAcrossLightAndMediumWithoutSizing` |
| English identity | Existing English font/material/style identity and prefab-authored sizing are restored after the round trip | `KboDiaGothicTypographyContractTests.ProductionTheme_EnglishSerializedContractRemainsBitForBit` and production typography composition tests |

Serialized and runtime dependency inventory:

| Path or owner | Current dependency fact | Migration implication |
|---|---|---|
| `GameplayUiTypographyTheme.asset` | One strongly referenced asset contains both required locale codes, 19 base rules, two locale FontSets, seven mappings per FontSet, and all English/Korean font and material object references | Loading the Theme admits both locale font graphs; this is the primary Phase 10-16 separation target |
| Theme consumers | `GameplayScreenPrefabCatalog`, `GameplayPopupPrefabCatalog`, `GameplayHudRoot`, `GenericLoadingOverlayContent`, and `ChanceLostOverlayContent` serialize the same Theme root | Catalog/bootstrap allowlisting must account for every root before the old Theme edge is severed |
| Korean font references | KBO Dia Gothic Medium/Light GUIDs occur in the Theme and not directly in current production UI prefabs or Scenes | Korean ownership is centralized today, but still co-resident through the all-locale Theme |
| Direct English prefab references | `SettingsScreen`, `MainMenuScreen`, `StageResultScreen`, `LevelFailedScreen`, `GameClearScreen`, `PausePopup`, `ConfirmPopup`, and `GameplayHudRoot` serialize English TMP fonts; `PlayerActionCountView` and the three World Guide prefabs also carry English font references | Phase 13 must classify bootstrap/shared exceptions and remove or replace all other direct and captured references before unloadability can be claimed |
| Scene references | The inspected production Scene YAML contains no direct KBO GUID; production font dependencies arrive through catalogs, prefabs, Theme, TMP defaults, and runtime capture paths | A Scene-only scan is insufficient evidence |
| `LocalizedTmpTextBinding` | Constructor fields capture the target's default font and material and retain them for fallback application | Runtime-created binding lifetime can retain an English font after a locale change |
| `TypographyBinding` | `CaptureAuthoredState` retains original font, material, style, sizing, and spacing in a nonserialized struct | Active, inactive, and pooled bindings must be included in the Phase 13 capture/reference audit |
| HUD restoration | `GameplayHudLocalizationBinding` and `ObjectiveHudTypographyBinding` restore `OriginalFont` and `OriginalMaterial` | HUD/objective reactivation and restoration are explicit reference-audit targets |
| TMP global default | `TMP Settings.asset` strongly references `LiberationSans SDF` and has an empty global fallback list | LiberationSans is a candidate bootstrap/shared dependency, not proof that locale fonts are isolated |
| Runtime Resources fallback | `UiCanvasElementFactory.LoadDefaultFont` falls back to `Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF")` | The common bootstrap allowlist must explicitly retain or replace this path |
| Addressables | Existing groups contain Locale assets, shared UI/Stage table data, and separate en-US/ko-KR UI/Stage String Tables. No audited font GUID is an Addressables entry | There is no current locale FontSet lease or per-locale font bundle to unload |

Current font identity ledger:

| Locale/category use | Asset GUID | Asset |
|---|---|---|
| en Display/Bold | `819507a38fa816a489de88dad2de2ce9` | `Orbitron-ExtraBold SDF` |
| en Heading/Bold | `b8aa923455f5a64468504293db7cef18` | `Exo2.0-SemiBold SDF` |
| en Body and UI/Regular | `0bafc025494aac644a55ad2ac560ca13` | `Exo2.0-Regular SDF` |
| en UI/Bold | `dec0b1c5d015b39438a16d1bffa2e9ca` | `Font_SciFiSoldier_Bold` |
| en Utility/Symbol and TMP default | `8f586378b4e144a9851e7b34d9b748ee` | `LiberationSans SDF` |
| ko Display/UI/Utility/Symbol | `40d61154fd6576b4d85c2d78460b16ad` | `KBODiaGothic-Medium SDF` |
| ko Heading/Body | `7dfd9aae81fc1d242b007a3b7a042fb0` | `KBODiaGothic-Light SDF` |

Localization, packing, and atlas facts:

- Localization Settings uses project locale `en-US`, synchronous initialization,
  synchronous String/Asset database behavior, and
  `PreloadSelectedLocaleAndFallbacks` (`m_PreloadBehavior: 2`).
- Both `UI` and `Stage` tables carry `Preload` labels in separate
  `Localization-String-Tables-en-US` and `Localization-String-Tables-ko-KR`
  groups. Locale assets are in `Localization-Locales`; shared table data is in
  `Localization-Assets-Shared`. All audited bundled schemas use local build/load
  profile variables, are included in build, use bundle cache, and currently
  serialize `m_BundleMode: 0`.
- Each KBO asset contains one static 2048 x 2048 atlas
  (`m_AtlasPopulationMode: 0`, multi-atlas disabled), has an empty fallback list,
  and currently serializes `m_IsReadable: 1`. Their nominal Alpha8 atlas total
  is 8 MiB; the YAML files are approximately 8.56 MB each and are not Player
  install-size or runtime-residency measurements.

Storage-shape assertion migration map:

| Current assertion | Phase 1 disposition | Required replacement before old assertion removal |
|---|---|---|
| production `AvailableLocaleCodes == [en-US, ko-KR]` | Preserve as the current production compatibility gate | Phase 2 catalog tests assert stable ordered ShipReady selection with synthetic N-locale/Draft fixtures; production registration remains en/ko until promotion |
| presenter/view has English and Korean descriptor fields plus a `ko-KR` ternary | Record as current two-locale storage shape, not a future contract | Phase 2/4 ordered option model resolves catalog display metadata without locale-name branches |
| Theme has exactly 2 FontSets and 7 entries per set | Preserve only while the legacy Theme is production | Phase 10 validates one candidate FontSet schema independently; Phase 14 validates each real locale closure |
| combined Theme cache count is exactly 38 | Preserve as a legacy two-locale characterization | Phase 10 asserts an immutable single-locale cache with exactly one resolved result per 19 semantic tags and no other-locale object references |
| required locale list is exactly `[en-US, ko-KR]` in Theme/editor validator | Preserve until catalog/string governance owns lifecycle | Phase 2/3 validators derive lifecycle-aware required sets from the package-free catalog |
| resolver initialization probes en/ko Settings title and one Stage key | Preserve until generic governance is proven | Phase 3 validates all governed ShipReady keys; Phase 7 removes runtime cross-locale probes while retaining selected-locale readiness |
| English Theme YAML hash is bit-for-bit fixed | Preserve English behavior through shadow migration | Phase 10/14 compile and packed-closure tests assert the independent en-US FontSet produces the same 19 resolved semantic outputs |

Validation attempt:

- `./run_tests.sh ui` was run from this worktree on 2026-09-14.
- Worktree/Unity path validation, KBO committed/candidate integrity, gameplay
  stratification soft governance, semantic-query governance, and ActionPlanId
  governance completed before the build step.
- The first Windows dotnet UI build failed before Unity tests with
  `CS0246` in `Assets/_Features/Gameplay/Gameplay_Cleanup/Runtime/RemovalProcessor.cs`:
  `CleanupStructuralScanCounts` was not resolved during that incremental build.
- Follow-up inspection confirmed that `RemovalProcessor` and
  `CleanupStructuralScanCounts` are both compiled by the same
  `Game.Feature.Gameplay` asmdef and are both included in the generated
  `Game.Feature.Gameplay.csproj`; the current namespace import is valid.
- A direct Windows dotnet build of `Game.Feature.Gameplay.csproj` then succeeded
  with `0` errors, without changing gameplay source, type accessibility,
  namespaces, or duplicating the type.
- The official `./run_tests.sh ui` lane was rerun from the same worktree. The
  Windows UI build succeeded, Unity UI EditMode passed `1384/1384`, both the KBO
  Dia Gothic Light and Medium integrity guards reported `NO_MUTATION`, and the
  runner ended with `ALL TESTS PASSED`.
- The initial failure was not reproduced and is retained as generated/
  incremental-build-state history rather than confirmed evidence of an assembly
  visibility defect. No gameplay file was changed.

Phase 1 verdict: `PASS/COMPLETE`. The source-state inventory and assertion
migration map are reviewable, no production state changed, and the same-worktree
UI lane passed on rerun. This evidence is limited to the UI lane: `core` and
`full` were not run, so it does not establish either lane or any project-wide
regression result. The Phase 1 HOLD is cleared and Phase 2 may proceed.

### Phase 2 — Introduce a package-free locale catalog and selection policy

What is being done:

- model canonical locale identity, Draft/ShipReady lifecycle, display metadata,
  stable order, optional legacy aliases, and fallback policy;
- distinguish registered, authoring-known, ShipReady, and selectable locale
  sets;
- use synthetic loc-A/loc-B/loc-C fixtures to validate N-locale ordering,
  wrapping, Draft rejection, invalid/default selection, aliases, and no-op
  behavior;
- add en-US and ko-KR catalog rows in shadow mode without changing production
  selection yet.

Why: the policy must be proven independently of Unity Locale assets and future
CJK content.

Trade-off: a catalog introduces another authored source. A validator must keep
it aligned with Localization Settings, String Tables, and later FontSets.

Completion gate: adding a synthetic third locale does not require locale-name
branches or Unity asset mutation.

Phase 2 execution record (2026-09-14):

- Added package-free `LocaleCatalogEntry`, `UiLocaleCatalog`,
  `LocaleSelectionPolicy`, and explicit lifecycle/request-result types to
  `UI.ViewShared`. The implementation imports only BCL namespaces and does not
  reference `UnityEngine`, Unity Localization, Addressables, or Unity Locale
  assets.
- The catalog owns exact canonical identities, optional exact legacy aliases,
  autonym display metadata, unique stable order, `Draft`/`ShipReady` lifecycle,
  and canonical default/emergency-fallback identities. Duplicate identities,
  alias collisions, duplicate order, and invalid fallback/default rows fail
  during construction.
- The selection policy keeps registered codes, authoring-known rows, ShipReady
  rows, and the registered-ShipReady selectable intersection distinct. Startup
  resolution follows persisted selectable identity, selected selectable
  identity, then the registered default. Direct Draft, unknown, or unregistered
  requests are rejected; aliases return canonical codes; same-locale requests
  return an explicit no-op result. Registered identities reject null/blank
  values, and aliases do not impersonate canonical Unity registration.
- `LocaleSelectionResult` construction remains assembly-owned so callers cannot
  manufacture impossible status/code combinations outside the policy.
- Added `en-US` (`English`) and `ko-KR` (`한국어`) ShipReady rows through
  the catalog factory now named `CreateProduction()`. No production resolver, `AvailableLocaleCodes`,
  Settings UI, preference behavior, Locale asset, String Table, font, or
  Addressables asset was changed.
- Tests-first focused execution reached Unity compilation and failed as expected
  with `CS0246` for the not-yet-added `UiLocaleCatalog` and
  `LocaleSelectionPolicy`. After implementation, the focused fixture passed
  `12/12`.
- Independent review follow-up added direct guards for duplicate stable order,
  invalid default/emergency fallback lifecycle, exact-case identity, Draft
  alias registration, null/blank registered identity, result-construction
  ownership, and forbidden Unity Localization/Addressables dependencies. The
  strengthened tests-first run was `15 total / 2 failed` on the two missing
  implementation guards, then passed `15/15` after they were closed.
- The latest official `./run_tests.sh ui` lane passed its Windows UI build and
  Unity UI EditMode `1399/1399`; KBO Dia Gothic Light and Medium both reported
  `NO_MUTATION`, and the runner ended with `ALL TESTS PASSED`.

Phase 2 verdict: `PASS/COMPLETE`. Synthetic `loc-A`/`loc-B`/`loc-C` tests cover
stable ordering, forward/backward wrapping, Draft rejection, invalid/default
startup handling, exact alias canonicalization, same-locale no-op, and a third
ShipReady locale added only as data. This evidence is limited to the UI lane;
`core` and `full` were not run, and no performance, memory, Player, or broad
regression result is claimed. Phase 3 may proceed.

### Phase 3 — Add generic string-governance validation

What is being done:

- enumerate every ShipReady catalog row;
- validate Locale registration, required UI/Stage tables and keys, non-empty
  values, SmartFormat and placeholder parity, display metadata, and fallback
  validity;
- derive the authoritative required-key set from governed UI/Stage table
  descriptors, and fail if shared-data keys or governed descriptors drift;
- preserve exact en/ko copy tests as separate intentional product contracts;
- prove validator failures with deliberately corrupted third-locale fixtures;
- keep the current en/ko bootstrap tool separate from this read-only validator.

Validation severity is lifecycle-aware:

- ShipReady missing/empty copy, placeholder mismatch, invalid default, alias
  collision, duplicate code, or fallback cycle is a hard release failure;
- Draft incomplete copy/font coverage is a structured non-blocking diagnostic;
- Draft duplicate identity, invalid default/fallback graph, accidental
  production registration/selection/preload, or cross-locale dependency is a
  hard failure even before promotion.

This phase does not validate future FontSet assets because their new schema does
not exist yet.

Completion gate: corrupted fixtures fail with exact locale/table/key reasons and
the real en/ko Unity adapter passes on the same source state.

Phase 3 execution record (2026-09-14):

- Added the package-free `LocalizationStringGovernanceSnapshot`, requirement,
  table/entry snapshot, normalized placeholder-signature, structured diagnostic,
  report, and pure validator model to `UI.ViewShared`. This boundary imports only
  BCL namespaces and the Phase 2 catalog; it has no Unity, Localization,
  Addressables, Locale, or String Table asset dependency.
- Added `UiLocalizationRequirementProjection` to `UI.Application`, preserving
  the existing reference direction. UI requirements come from the Settings,
  Main Menu, Terminal Result, and Scene Transition contracts plus the HUD World
  Guide, Objective HUD, Main Menu static, and Pause descriptor owners. Matching
  duplicate table/key metadata is deduplicated by the validator; conflicting
  Smart/placeholder metadata is a blocking `RequirementMetadataConflict`.
- The UI Shared Data set is compared in both directions with the distinct
  governed projection. The validator contains no literal `128` truth source;
  missing governed keys and orphan Shared Data keys receive separate stable
  diagnostic codes.
- Added a read-only `UI.Composition.Editor` adapter. It reads registered Locales,
  the real `UI` and `Stage` collections and Shared Data, locale entry values and
  Smart flags, and converts them to the same package-free snapshot. A source
  guard prohibits the listed Locale/String Table mutation, dirty/save/refresh,
  bootstrap reuse, and Addressables APIs in this adapter.
- Active Stage requirements are collected from the campaign sequence's actual
  `StageContentEntry -> PresentationDefinition.DisplayNameKey` path. The
  retained `stage.legacy-stage-5-1.display_name` key is one explicit
  compatibility-retained requirement and participates in Shared Data drift
  validation; no prefix allowlist is used.
- The adapter uses the installed Unity SmartFormatter parser. Placeholder
  signatures are selector-identity multisets, so `{0}`/`{1}` reordering and
  escaped literal braces pass, while missing, added, changed, or repeated
  selectors fail. Parser exceptions are contained as locale/table/key-aware
  `SmartFormatMalformed` diagnostics.
- ShipReady registration, table, key, value, Smart, parse, and placeholder
  diagnostics are blocking. Draft incomplete table/key/copy diagnostics are
  non-blocking, while accidental Draft production registration and any
  registered locale without a canonical catalog row are blocking.
- Duplicate requirement conflicts produce one deterministic metadata diagnostic
  independent of descriptor input order; the conflicted key is excluded from
  locale-level metadata checks to avoid order-dependent cascading diagnostics.
  Non-Smart entries are not parsed as SmartFormat, so literal unmatched braces
  remain ordinary copy while null/empty/whitespace and Smart-flag contracts are
  still validated.
- The current catalog has only global `DefaultLocaleCode` and
  `EmergencyFallbackLocaleCode`; it has no per-locale fallback graph. Both
  identities are already required to resolve to canonical ShipReady rows by
  the Phase 2 constructor, so fallback-cycle analysis is N/A rather than a new
  Phase 3 schema or duplicate validator rule.
- Draft font coverage is deferred until Phase 10 introduces the FontSet schema.
  Draft preload/selection belongs to Phase 4 and later candidate-profile work,
  while packed dependency validation belongs to Phases 14-18. Phase 3 neither
  models nor mutates those future-owned surfaces.
- Tests-first focused execution initially reached Unity compilation and failed
  as expected with `CS0246` for the not-yet-added governance types. The final
  pre-review focused `./run_tests.sh ui --filter LocalizationStringGovernance` run passed
  `35/35`, including a complete data-only `loc-C` ShipReady case, lifecycle
  severity, every requested single-defect fixture, deterministic ordering,
  SmartFormatter order/escaping behavior, and the read-only adapter guard.
- Review hardening wrapped every publicly exposed governance dictionary in a
  real read-only collection so interface downcasts cannot mutate the shared
  empty signature, locale entries, or locale-table map. The added regression
  guard and the refreshed current-structure source passed the focused lane
  `36/36`.
- `UiLocaleCatalog.CreateProduction()` drove the real adapter without a
  second required-locale array. Registered en-US/ko-KR, both real collections,
  all Shared Data keys, active Stage keys, and the retained legacy key produced
  zero diagnostics and zero blocking failures through the same validator.
  Existing exact en/ko copy, Smart-flag, and Stage compatibility tests remain
  unchanged and passed in the official UI suite.
- The final official `./run_tests.sh ui` run passed the Windows UI build and
  Unity UI EditMode `1435/1435`; both KBO Dia Gothic Light and Medium integrity
  guards reported `NO_MUTATION`, and the runner ended with `ALL TESTS PASSED`.
- `core` and `full` were not run. This UI-lane result is not project-wide or
  full-regression evidence. No Player, performance, memory, CJK content/font,
  layout, glyph, candidate-build, preload, selection, or packed-dependency
  evidence was produced.
- No Locale, String Table, Localization Settings, Addressables, font, Prefab,
  Scene, or ScriptableObject production asset was modified. The legacy
  `SettingsLocalizationAssetBootstrap` was neither changed nor executed.

Phase 3 verdict: `PASS/COMPLETE`. A third ShipReady locale is admitted to the
governance fixture through catalog/table data only, without locale-name branches
or Unity asset authoring. The read-only real en/ko adapter and synthetic damaged
fixtures use the same validator, and exact-copy contracts remain separate.
Phase 4 may proceed only as a separate requested change.

### Phase 4 — Enforce ShipReady-only runtime selection

What is being done:

- derive selectable options from ShipReady catalog rows intersected with valid
  installed Unity Locales;
- apply the same predicate to direct requests, persisted-locale restore, and
  initial Unity-selected-locale acceptance;
- fail closed when the default ShipReady locale is unavailable;
- keep Draft assets outside production `AvailableLocales`; register one Draft
  only through the explicit development/candidate build profile.

Why before Draft import: hiding a Draft option in Settings does not prevent the
current resolver, command-line selector, or package preload path from selecting
a registered Locale.

Canonical policy: do not register Draft locales in production
`AvailableLocales` until their promotion change.

Completion gate: package-free tests supply a fake registered-code snapshot and
prove a synthetic Draft is not selectable, restorable, or accepted. A separate
real production-settings asset test proves that the number of registered Draft
locales is zero; tests do not mutate the real `LocalizationSettings` asset.

#### Phase 4 pre-implementation current-gap audit

Phase 2 already introduced `UiLocaleCatalog`, `LocaleLifecycle`, and the pure
`LocaleSelectionPolicy` in `UI.ViewShared`. The policy already computes the
stable-order intersection of catalog `ShipReady` rows and registered canonical
locale codes, rejects Draft and unknown requests, canonicalizes explicit aliases,
applies persisted -> selected -> default startup precedence, and fails when the
default ShipReady locale is not registered.

At Phase 4 entry, the production `UnityStringTableTextResolver` did not yet
consume that policy. Its pre-Phase-4 implementation:

- exposes every exact code returned by `LocalizationSettings.AvailableLocales`;
- accepts any exact registered code in `TrySetLocale`;
- accepts any exact registered persisted or initial selected locale at startup;
- accepts any exact registered locale received through
  `SelectedLocaleChanged` after startup;
- keeps separate hard-coded en/ko availability checks and cross-locale table
  probes that are compatibility behavior until Phases 7-8.

This meant the pure policy was shadow evidence rather than the production
selection authority. Phase 4 closed only that wiring gap. It did not remove the
existing table probes, redesign persistence, or introduce new locale assets.

#### Phase 4 ownership and exact runtime shape

Ownership remains split as follows:

| Concern | Owner | Phase 4 rule |
|---|---|---|
| Canonical identity, lifecycle, stable order, alias policy | `UI.ViewShared` `UiLocaleCatalog` | Remains package-free and is the only lifecycle truth |
| Selection admissibility and startup precedence | `UI.ViewShared` `LocaleSelectionPolicy` | Remains pure; no Unity object or persistence write |
| Registered Locale enumeration and canonical `Locale` lookup | `UI.Composition` | Adapts Unity objects into canonical code inputs for the pure policy |
| Applying `LocalizationSettings.SelectedLocale` | `UnityStringTableTextResolver` | Happens only after a policy `Change` or an approved startup result |
| Saving an explicit successful selection | existing `IUiLocalePreferenceStore` | Saves only the canonical code after the Unity selection is applied |
| Settings cycling | existing `IUiLocaleSelectionPort` consumer | Temporarily consumes the filtered stable-order code list; Phase 5 replaces this with option data |

`UnityStringTableTextResolver` receives the production catalog from a renamed
`UiLocaleCatalog.CreateProduction()` factory at its production boundary. Phase 4
removes the former shadow-factory name and migrates all repository callers;
the catalog stops being shadow-only when it becomes the runtime selection
authority. No compatibility alias is retained because this is a repository-local
source API and leaving both names would preserve an ambiguous ownership seam.
After Unity Localization initialization, it snapshots exact registered locale
codes, constructs one `LocaleSelectionPolicy`, and publishes
`AvailableLocaleCodes` from `policy.SelectableLocales`, preserving catalog
stable order. It must not publish the raw registration order or include unknown
or Draft codes.

The resolver keeps a private canonical-code-to-`Locale` lookup for the codes
admitted by the policy. `Locale`, `LocalizationSettings`, and any package handle
remain inside `UI.Composition`; neither `UiLocaleCatalog` nor
`LocaleSelectionPolicy` gains a Unity Localization or Addressables reference.
Aliases are request/persistence compatibility inputs only. A registered alias
does not make a locale selectable: the canonical code itself must be registered,
and any successful alias request is applied and persisted as its canonical code.

The existing `IUiLocaleSelectionPort` shape is intentionally unchanged in this
phase. Its `AvailableLocaleCodes` name is retained as a compatibility surface,
but its production meaning becomes "ordered selectable ShipReady codes", not
"all codes registered in Unity". Phase 5 owns the replacement with ordered
`LocaleOptionModel` data and catalog-owned autonym metadata.

The public production factory remains parameter-free with respect to catalog
choice and always uses `CreateProduction()`. One `internal` factory overload may
accept an explicit `UiLocaleCatalog` for same-assembly/test-fixture composition.
That seam must still read the real Unity registration and use the same resolver
implementation; it may not accept a bypass boolean or a second selection
algorithm. Tests can therefore classify the real registered `ko-KR` Locale as
Draft in a synthetic catalog, prove that direct/startup/event paths reject it,
and restore the pre-test selected Locale without adding or removing anything
from the serialized production `LocalesProvider`.

#### Phase 4 decision matrix

Every production entry path uses the same policy result before changing the
resolver's current locale, Unity's selected locale, emitting `LocaleChanged`, or
writing a preference.

| Entry path | Candidate | Result | Required side effects |
|---|---|---|---|
| Explicit `TrySetLocale` | registered ShipReady, different from current | `Change` | apply canonical Unity Locale, save canonical code once, emit once |
| Explicit `TrySetLocale` | registered ShipReady, same as current | `NoOp` | return success; no Unity assignment, save, preload restart, or event |
| Explicit `TrySetLocale` | Draft, unknown, empty, case mismatch, or canonical code not registered | `Rejected` | return false; no state, save, preload, or event change |
| Startup preference | registered ShipReady or a valid alias whose canonical code is registered | accepted | wins over Unity's initial selected locale; apply canonical code without rewriting the preference |
| Startup preference | Draft, unknown, malformed, or unregistered | rejected candidate | try the initial Unity selection next; do not erase or rewrite the stored value in Phase 4 |
| Initial Unity selection | registered ShipReady | accepted | use as current when no admissible preference exists |
| Initial Unity selection | Draft, unknown, or unregistered | rejected candidate | select the catalog default instead |
| Default fallback | registered ShipReady | accepted | establish the only startup fallback |
| Default fallback | missing or unregistered | fatal initialization failure | resolver creation fails with a deterministic reason; do not choose another locale |
| Post-start `SelectedLocaleChanged` | registered ShipReady, different from current | `Change` | adopt canonical code and emit once; do not write the preference because the change was externally owned |
| Post-start `SelectedLocaleChanged` | current ShipReady | `NoOp` | no event, save, or preload restart |
| Post-start `SelectedLocaleChanged` | Draft, unknown, or unregistered | `Rejected` | restore the last approved current Unity Locale under event suppression; no resolver event or preference write |

The last row is required because filtering the Settings options alone does not
prevent another Unity startup selector or package consumer from assigning a
registered Draft after resolver initialization. The restore path uses the same
event-suppression guard as an approved assignment so it cannot recurse. The
resolver initialization gate guarantees that the last approved current/default
Locale is resolvable; failure to restore is a fail-closed invariant violation,
not permission to adopt the rejected locale.

Locale selection and locale readiness remain different concepts. Phase 4 proves
that a lifecycle-admissible, registered Locale may be selected. It does not
claim that its String Tables, FontSet, glyphs, layout, or packed dependencies are
ready; those remain protected by their own later gates. For the current
production catalog, only en-US and ko-KR are selectable, so existing rendered
behavior must remain unchanged.

#### Phase 4 implementation slices

The implementation is one bounded runtime-policy slice with tests-first
evidence:

1. Rename the shadow factory to `CreateProduction()` and migrate the
   Phase 2/3 catalog and governance callers. Extend the pure-policy fixture only
   where the current matrix is not already explicit: missing-default failure,
   Draft persisted/selected precedence,
   canonical alias target, registration deduplication, and stable selectable
   ordering.
2. Wire one `LocaleSelectionPolicy` into `UnityStringTableTextResolver` after
   Unity initialization. Replace raw registered-code publication and the private
   selection checks with policy evaluation; keep Unity object lookup and
   assignment in Composition. Add only the catalog-injecting internal factory
   seam needed to exercise the production adapter without serialized mutation.
3. Add production-adapter coverage for explicit `Change`/`NoOp`/`Rejected`,
   startup precedence, zero-write startup behavior, canonical preference writes,
   and rejected external-event restoration.
4. Add a read-only production-settings contract test that compares actual
   registered codes with the production catalog and proves that no registered
   entry resolves to `Draft`. It may read `LocalizationEditorSettings` and the
   active `LocalesProvider`; it must not add/remove Locales, dirty assets, save,
   refresh, or call the localization bootstrap tool.
5. Run the focused policy/Unity-adapter tests first, then the same-revision UI
   lane. Record focused and UI evidence separately.

Expected primary touch set:

- `Assets/_Features/UI/UI_ViewShared/Runtime/UiLocaleCatalog.cs`
- `Assets/_Features/UI/UI_Composition/Runtime/UnityStringTableTextResolver.cs`
- `Assets/_Features/UI/UI_Tests/EditMode/LocaleCatalogSelectionPolicyTests.cs`
- `Assets/_Features/UI/UI_Tests/EditMode/UnityLocalizationStringTableIntegrationTests.cs`
- `Assets/_Features/UI/UI_Tests/EditMode/LocalizationStringGovernanceUnityIntegrationTests.cs`
- this plan and the UI validation baseline only after execution evidence exists

The catalog's selection algorithm is not an automatic rewrite target: the
current policy already owns the required decisions. Aside from the production
factory rename, it changes only if a red test proves a missing pure contract. No
asmdef reference change is expected.

#### Phase 4 validation gates

The focused fixture matrix must prove all of the following on the same source
state:

- `SelectableLocales` is exactly the catalog-stable-order intersection of
  ShipReady canonical rows and exact registered canonical codes;
- a registered Draft is not published, explicitly selectable, accepted from
  persistence, accepted as the initial Unity selection, or adopted from a later
  Unity selection event;
- the catalog-injected adapter tests prove those Draft outcomes using existing
  in-memory Unity Locale objects while the serialized `LocalesProvider` and
  Localization Settings asset remain byte-unchanged;
- an unknown registered Unity locale is never promoted to an application locale;
- a valid alias resolves only when its canonical code is registered, and the
  canonical code is the apply/save/event identity;
- invalid stored data causes no startup write and falls through to approved
  selected/default precedence;
- an explicit rejected/no-op request has zero persistence and event side effects;
- a missing registered default fails resolver initialization with an actionable
  reason rather than choosing the first registered locale;
- the real production Localization Settings registration contains en-US and
  ko-KR, contains zero catalog Draft locales, and contains no uncatalogued code;
- production code continues to have no package-free runtime fallback, while
  `UI.ViewShared` remains free of Unity Localization and Addressables imports;
- the retired shadow-factory name has zero remaining source references after the
  production-authority rename;
- existing en-US -> ko-KR -> en-US String Table and typography round trips remain
  green.

Required commands after implementation:

```text
./run_tests.sh ui --filter LocaleCatalogSelectionPolicyTests
./run_tests.sh ui --filter UnityLocalizationStringTableIntegrationTests
./run_tests.sh ui --filter LocalizationStringGovernanceUnityIntegrationTests
./run_tests.sh ui
```

If the runner supports a comma-separated focused filter at execution time, the
three focused fixtures may be run in one invocation, but the resulting XML must
contain a non-zero match for every named fixture. `core`, broad `full`, Player,
performance, memory, font residency, glyph, and visual QA are not Phase 4 gates.
They must be reported as not run unless separately executed for another stated
reason.

#### Phase 4 no-touch and rollback boundary

This phase does not:

- add ja-JP, zh-CN, or any Draft Locale/String Table/font asset;
- change `Localization Settings.asset`, Addressables groups, preload behavior,
  startup selector ordering, Scene, Prefab, or ScriptableObject content;
- remove the current en/ko cross-locale String Table probes; Phase 7 owns that
  removal after the runtime gate is established;
- convert the Settings language row to arbitrary option data or add third-locale
  display copy; Phase 5 owns that UI change;
- consolidate preference cleanup/fallback writes; Phase 6 owns persistence
  policy unification;
- introduce committed-locale transition state, FontSets, lease ownership,
  Addressables loading, or residency claims.

The rollback unit is the resolver-to-policy wiring plus its new tests. Reverting
it restores the current en/ko-only production behavior but also reopens the
Draft-selection bypass, so rollback is acceptable only while production assets
and the production catalog still contain no Draft locale. Asset changes are not
part of this rollback unit.

Phase 4 is complete only when the runtime adapter consumes the pure policy on
all four entry paths, the real registration audit is read-only and green, the
focused fixtures and full UI lane pass on the same revision, and the evidence
record states the exact non-claims above. Passing the existing Phase 2 pure
policy tests alone is insufficient because it does not prove production wiring.

Phase 4 execution record (2026-09-14):

- Tests-first compilation failed as intended before production wiring: the new
  tests reported missing `CreateProduction()` and catalog-injection factory
  surfaces. The pure-policy fixture subsequently passed `17/17` after the
  production-authority rename and additional precedence/default/deduplication
  guards.
- `UnityStringTableTextResolver` now snapshots the active Unity registered
  Locales, creates one `LocaleSelectionPolicy`, publishes only the catalog-order
  selectable ShipReady codes, and keeps canonical-code-to-`Locale` lookup and
  assignment inside `UI.Composition`. The public factory always uses the
  production catalog; the sole catalog injection seam is `internal` and uses
  the same resolver and real Unity registration.
- Explicit requests, persisted restore, initial Unity selection, and later
  Unity selection events all pass through the same policy. Explicit `Change`
  applies Unity selection, saves the canonical preference, and emits once;
  `NoOp` and `Rejected` have zero save/event/assignment side effects. External
  approved changes emit once and save zero times. Draft, unknown, malformed,
  case-mismatched, Unity-origin alias, and unregistered-canonical inputs are
  rejected; post-start rejection restores the last approved Locale under event
  suppression. Alias compatibility remains limited to explicit/persisted input
  and succeeds only when its canonical target is registered.
- A missing registered catalog default now returns an actionable initialization
  failure instead of selecting an arbitrary Locale. Invalid persisted data is
  neither deleted nor rewritten and falls through to approved Unity selection,
  then the registered default.
- The read-only production audit waits only for the configured Localization
  initialization operation, then reads the active `AvailableLocales.Locales`.
  It proved en-US and ko-KR are registered, every registered code is an exact
  production ShipReady canonical row, and both the registered/Draft intersection
  and uncatalogued registered set are empty. It did not add/remove Locales,
  dirty/save/refresh assets, run the localization bootstrap, or mutate
  Addressables.
- Independent read-only architecture and adversarial reviews initially found
  Unity-origin alias acceptance, null-event bypass, Editor-inventory audit, and
  exact-canonical audit gaps. All P1 findings were closed with exact canonical
  Unity-origin adaptation, policy-driven null rejection/restoration, active
  provider inspection, and regression coverage. Final re-review found no P0/P1
  findings.
- Final focused runs passed `LocaleCatalogSelectionPolicyTests 17/17`,
  `UnityLocalizationStringTableIntegrationTests 50/50`, and
  `LocalizationStringGovernanceUnityIntegrationTests 7/7`. The final official
  `./run_tests.sh ui` run passed the Windows UI build and Unity UI EditMode
  `1445/1445`; both KBO Dia Gothic assets reported `NO_MUTATION`.
- `git diff --check` and the retired-factory source scan passed. Tracked diffs
  under `Assets/Localization` and `Assets/AddressableAssetsData` were empty.
  No Locale, String Table, Localization Settings, Addressables, font, Scene,
  Prefab, ScriptableObject, or asmdef asset was changed.
- `core`, broad `full`, Player, performance, memory, glyph, font residency, and
  visual QA were not run because they are not Phase 4 gates. This evidence is
  limited to the focused fixtures and UI lane and does not establish a
  project-wide or full-regression result. Phase 5 was not started.

Phase 4 verdict: `PASS/COMPLETE`.

### Phase 5 — Convert the Settings locale UI to ordered option data

#### Phase 5 decision status and user-recovery rationale

The option identity and display-name direction is approved as:

```text
canonical locale code + raw catalog autonym
```

An autonym is the language's name written in that language, for example
`English`, `한국어`, `日本語`, or `简体中文`. This is a recovery-oriented UX
decision: if a user accidentally selects a UI language they cannot read, the
language selector still exposes a name they can recognize when that locale is
reached. A localized exonym such as `영어` or `Korean` does not provide the same
language-independent recovery property.

This decision does not claim that the current cycle control shows every locale
at once. Phase 5 keeps the existing single current-value cycle interaction. A
user may have to cycle through multiple approved locale changes before reaching
their language. A simultaneous list or popup would improve direct discovery but
has a different multi-script font and residency cost and is not authorized by
this phase.

Phase 5 design review found no P0 issue. Implementation remains unstarted until
the tests-first slice below is executed. The approved raw-autonym decision closes
the display-name policy question; it does not close later CJK font, glyph,
transaction, packing, or residency gates.

#### Phase 5 current-gap audit

Phase 4 now exposes `AvailableLocaleCodes` as the catalog-stable-order
intersection of exact Unity registration and catalog `ShipReady` rows. That
selection result is correct but carries no display metadata. The Settings
consumer still has two-locale storage shape:

- `IUiLocaleSelectionPort` exposes only `IReadOnlyList<string>` codes;
- `SettingsScreenPayload`, `SettingsDisplayPresenter`, and
  `SettingsDisplayView` carry separate English and Korean language-name
  descriptors;
- both Presenter and View contain their own `ko-KR` branch;
- the Presenter silently chooses option index zero when its current code is not
  found in the available list;
- package-free and test implementations repeat the code-list surface.

The existing unused `LocaleOptionModel` carries a
`LocalizedTextDescriptor`. That shape is not the Phase 5 authority. Resolving
that descriptor in the current UI locale produces localized exonyms in some
cells (`영어`, `Korean`) and would require a translated language-name matrix as
the locale set grows. It therefore conflicts with the Phase 0 autonym decision
and must be replaced rather than populated.

The current Presenter index-zero fallback is also not a locale recovery policy.
Phase 4 guarantees that a production current locale is selectable. If a
non-empty option snapshot does not contain the current canonical code, silently
selecting the first option would hide an invariant violation and could cause a
preference write, `LocaleChanged`, table/font work, and Toggle audio from one
button press. Phase 5 fails closed instead of inventing that fallback.

#### Phase 5 ownership and exact runtime shape

Ownership is:

| Concern | Owner | Phase 5 rule |
|---|---|---|
| Canonical code, autonym, lifecycle, stable order | `UI.ViewShared` `UiLocaleCatalog` | Catalog is the only option metadata truth |
| Selection admissibility | existing `LocaleSelectionPolicy` | No second Settings filter or policy |
| Unity registration and projection of selectable rows | `UI.Composition` | Projects existing policy results; exposes no Unity object |
| Cycle order, current-option match, final display string | `UI.Application` Presenter/ViewModel | Uses canonical options and never interprets a locale-specific branch |
| Text rendering and typography application | `UI.Screens` View and authored `TypographyBinding` | View renders the ViewModel autonym; style belongs to the control, not the locale option |
| FontSet/cache/lease/commit readiness | Phases 9-16 owners | Not represented in the option DTO and not implemented here |

The package-free DTO and port target shape is:

```csharp
public sealed class LocaleOptionModel
{
    public LocaleOptionModel(
        string canonicalCode,
        string displayNameAutonym);

    public string CanonicalCode { get; }

    public string DisplayNameAutonym { get; }
}

public interface IUiLocaleSelectionPort
{
    string CurrentLocaleCode { get; }

    IReadOnlyList<LocaleOptionModel> AvailableLocaleOptions { get; }

    bool TrySetLocale(string localeCode);
}
```

Blank canonical codes and blank autonyms are construction failures; the model
does not normalize them to empty strings. A validated sealed reference type is
used deliberately so a default value is `null` rather than an all-blank object
that bypassed the constructor; every snapshot producer rejects null options. The
per-option allocation occurs only while the small immutable snapshot is created,
not while Settings renders or cycles.
Options contain canonical identities only. Aliases remain explicit
request/preference compatibility inputs and are never visible option identities.
The snapshot is an owned array exposed through an immutable wrapper, contains no
null option or duplicate canonical code, and preserves the order of
`LocaleSelectionPolicy.SelectableLocales`.

`UnityStringTableTextResolver` projects each selectable catalog entry to its
canonical code and catalog `DisplayName`. It does not re-enumerate raw Unity
registration, resolve a language-name String Table key, or add another lifecycle
filter. The getter returns the precomputed field only. A source/architecture
guard, rather than a warm-cache observation, proves that the getter and projection
call graph contain no `Resolve`, `PreloadTable`, or Unity table API call.
Behavioral tests separately prove zero Unity selection, preference, locale-event,
and audio mutation. Without a dedicated table-load instrument, Phase 5 claims
only source-proven "no additional preload call" and does not claim a measured
preload cardinality.

`AvailableLocaleCodes` and `AvailableLocaleOptions` must not remain as parallel
production surfaces because two lists can drift in filtering or order. The
production resolver, package-free resolver, no-op implementation, wrappers, and
test fakes move to the option surface in one slice, after which the code-list
member is removed without a compatibility alias.

Phase 5 changes only the package-free option shape of
`PackageFreeLocalizedTextResolver`. It keeps its existing supported-code set and
performs an exact lookup of each of those codes in the production catalog to
obtain autonym metadata; it does not expose every future production ShipReady row
automatically. Its current selection, startup, and persistence algorithms are
not redesigned. Phase 6 still owns supported/selectable policy unification and
the result-bearing persistence contract. Future CJK strings must not be copied
into the package-free resolver.

#### Phase 5 current cycle-label font contract

The current Settings control displays one value: the autonym of the
last-approved resolver current locale. It does not render the complete option
snapshot. Its existing locale-themed `TypographyBinding` owns the
`SettingsAction` visual role. The normal relationship is therefore:

```text
en-US current -> English -> current en-US typography path
ko-KR current -> 한국어 -> current ko-KR typography path
future ja-JP current -> 日本語 -> current ja-JP typography path
future zh-CN current -> 简体中文 -> current zh-CN typography path
```

The option DTO contains no `LocalizedTextDescriptor`, `TypographyStyleTag`,
Unity `Locale`, TMP font/material object, FontSet identity, Addressables key or
handle, preload hint, or residency hint. Text identity and typography remain
separate: the catalog owns the autonym, the Prefab/control owns its style tag,
the resolver current locale selects the applicable current typography data, and
the View applies the style. This is not the tokenized application-locale
`Committed` state introduced in Phase 9.

The former English and Korean language-name descriptors have the same fallback
semantic role/weight. Once their locale-specific selection branches are removed,
any non-theme fallback styling for the language control uses one fixed control
semantic rather than choosing a descriptor by locale identity. The existing
`LanguageEnglish` and `LanguageKorean` String Table keys and rows are not deleted
in this phase: their asset/governance cleanup is not required to remove runtime
consumers and would unnecessarily expand the mutation and rollback unit.

For the present en/ko implementation, the real integration gate preserves the
font identity round trip on the single language label. A synthetic third-locale
option proves only the N-locale data path. If no synthetic Theme/FontSet exists,
the text may otherwise appear while an old font remains applied; that is a
false-green for font readiness. Phase 5 therefore makes no Japanese/Chinese
glyph, CJK font, layout, residency, atomic rollback, or mixed-frame claim from
the synthetic option test.

An approved locale request currently changes Unity locale state before all
observers reapply typography. Failure-safe atomic locale/font commit is not
introduced here. Phase 9 separates raw and committed locale state, and Phases
10-16 own candidate FontSet validation, binding readiness, render barriers,
packing, leases, rollback, and production cutover.

#### Future simultaneous-option font decision

If UX later requires every autonym to be visible simultaneously, the selected
locale FontSet alone may not contain every script. That requirement reopens a
separate decision before the list ships:

| Strategy | Benefit | Cost and risk | Disposition |
|---|---|---|---|
| Keep one current autonym | Uses only the resolver-current typography path | Users cannot scan every option directly and may perform several real switches | Phase 5 behavior |
| Load every locale FontSet and render each row with its target font | Native target-locale form for every autonym | Defeats selected-locale-only residency and adds multi-lease lifetime/rollback complexity | Not recommended |
| Connect every full locale font through TMP global/general fallback | Simple multi-script lookup | Strong cross-locale references, hidden fallback, material/baseline drift, and lost unloadability | Prohibited direction |
| Add every autonym glyph to every locale FontSet | Avoids an extra picker loader when sources contain the glyphs | Duplicated atlas data, unsupported scripts, regional-form risk, and weaker locale closure | Not recommended |
| Localize all language names into the current UI language | One selected FontSet can render the list | Reverses the autonym recovery decision and creates N-by-N copy/review cost | Not selected |
| Explicit compact autonym-recovery font or per-script chain | Bounded, deterministic corpus without loading every full FontSet | New font/license/subsetting, regional Han form, baseline, accessibility, packing, and measured-residency work | Conditional later candidate |
| Sprite language names | Font-independent appearance | Scaling, DPI, contrast, accessibility, search/copy, and duplicate alt-text ownership | Not a primary representation |
| OS/system font fallback | Avoids an authored static asset in some environments | Platform-dependent shapes/availability, dynamic-atlas stalls, and non-deterministic release evidence | Prohibited production dependency |

The conditional recovery-font candidate is not a Phase 5 asset. If a future
simultaneous picker or selected-FontSet-failure recovery requirement is approved,
the plan must explicitly define a selector-only `AutonymRecovery` corpus and
packing/ownership exception. It must not be placed in `UI.ViewShared` or encoded
in `LocaleOptionModel`. It requires:

- exact glyph closure for every production ShipReady autonym;
- approved license and subsetting terms;
- correct Japanese/Simplified-Chinese regional forms rather than assuming one
  pan-CJK face is typographically neutral;
- baseline, weight, scale, contrast, and visual/accessibility validation;
- raw text retained as the authoritative text supplied to any future
  accessibility bridge even if decorative imagery is present;
- either an explicit base-Player bootstrap allowlist or a new dedicated picker
  bundle with a Composition-owned lease; the recovery font, atlas, and material
  must never enter `UI-Typography-Shared`, a locale bundle, or a locale FontSet's
  transitive closure;
- packed Player dependency and residency evidence before any size or memory
  claim.

If simultaneous display and font-failure recovery are not required, the
recovery asset is omitted. Avoiding an unnecessary always-resident font remains
the preferred residency result.

#### Phase 5 behavior matrix

| State or action | Display/cycle result | Required side effects |
|---|---|---|
| Zero options through the no-op seam | Empty current-language text; cycle disabled | `TrySetLocale` zero calls; no event, preference, preload, or audio |
| One option matching current | Its raw autonym is displayed; cycle disabled | `TrySetLocale` zero calls; no event, preference, preload, or audio |
| Two or more valid options and current matches exactly once | Current autonym displayed; next request follows snapshot order and wraps | Call `TrySetLocale` exactly once with the next canonical code |
| Non-empty options but current matches zero or multiple entries | ViewModel publishes empty current-language text and disables cycle during Apply/Refresh; `SelectNextLocale` returns false | `TrySetLocale` zero calls; no selection, write, event, preload, or audio |
| Underlying request rejects | Keep current option text and font | `TrySetLocale` one call; no preference/event/preload/audio effect and no Toggle audio |
| Underlying request returns true but current does not equal the requested target afterward | Treat as unsuccessful and keep/refresh from the actual current option | No Toggle audio; do not report an approved UI change |
| Approved user `Change` verified by current matching the requested target | Refresh from the port's new current option after the existing locale event | Existing Phase 4 apply/save/event cardinality; exactly one Toggle audio from the successful UI action |
| User cycle `NoOp` | Impossible for a valid unique snapshot with two or more options and an exact current match | Tests must not use this unreachable state as ordinary cycle evidence |
| External approved change | Match and display the new current autonym through the existing locale event | No preference write and no Toggle audio in Phase 5 |
| External Draft/unknown/null/unregistered event | Phase 4 restores the last approved locale; displayed autonym/font stay on it | No custom locale event, preference write, or Toggle audio |

Production Composition treats a non-empty snapshot/current mismatch as an
actionable resolver-initialization failure that includes the current code and
available canonical codes. Presenter behavior remains independently defensive:
it publishes empty/disabled state and returns false without requiring Unity
logging or another diagnostic dependency. Before Phase 6 changes its API, tests
characterize the package-free resolver's public invalid-initial and unrestricted
`SetLocale` paths as possible producers of this defensive state.

Phase 5 does not add an option-changed event. The existing locale event remains
the refresh seam. Presenter and View currently both observe localization changes,
so tests must guard against adding a third refresh route and must count visible
option/audio effects. Consolidating all raw/committed observers belongs to the
Phase 9 transition boundary unless a red Phase 5 test proves a narrower defect.

#### Phase 5 implementation slices

1. Tests-first: replace the unused descriptor-based option assumption with
   canonical/autonym construction and immutable snapshot contracts. Add
   synthetic stable-order, duplicate, Draft, alias, zero/one/three-option,
   current-missing, rejected-selection, true-without-target-change, and
   external-change cases and confirm they fail for the current
   code-only/en-ko-branch implementation. Duplicate registration and
   Draft/alias/unknown filtering use synthetic catalog/policy registered-code
   input and never mutate the real `LocalesProvider`.
2. Replace `IUiLocaleSelectionPort.AvailableLocaleCodes` with
   `AvailableLocaleOptions`; migrate the production resolver, package-free
   resolver, no-op implementation, wrappers, and every fake in one slice.
   Production projection must use the already filtered
   `LocaleSelectionPolicy.SelectableLocales` and catalog autonyms.
3. Migrate `SettingsDisplayPresenter` to exact canonical option matching and
   ordered cycle/wrap. It publishes only the final current autonym and enabled
   state to its existing ViewModel and never speculatively advances display
   state before `TrySetLocale` succeeds. Because the port returns only `bool`, a
   successful UI action also requires the post-call current code to equal the
   requested canonical target before Toggle audio is allowed.
4. Remove the English/Korean language-name fields from
   `SettingsScreenPayload`, Presenter, and View after all callers have migrated.
   Remove both `ko-KR` display-name branches. Keep the authored language control
   and its existing typography binding; do not mutate the Settings Prefab.
5. Add a real en/ko production round trip for exact option data, display text,
   font identity, external-change behavior, and side-effect cardinality. Keep
   synthetic N-locale structure evidence separate from real asset/font evidence.
6. Run the fixed focused Settings/catalog/Unity/architecture fixtures below,
   then the full UI lane. Record exact executed counts only after the XML proves
   every named fixture ran at least once.

Expected primary production touch set:

- `Assets/_Features/UI/UI_ViewShared/Runtime/LocalizedTextDescriptor.cs`
- `Assets/_Features/UI/UI_Composition/Runtime/UnityStringTableTextResolver.cs`
- `Assets/_Features/UI/UI_Application/Runtime/Settings/SettingsScreenPresenters.cs`
- `Assets/_Features/UI/UI_Screens/Runtime/ScreenModels.cs`
- `Assets/_Features/UI/UI_Screens/Runtime/SettingsDisplayView.cs`

Expected test touch set includes the Settings localization foundation,
production Settings runtime, Unity Localization integration, and architecture or
governance fixtures plus every `IUiLocaleSelectionPort` fake/wrapper. No Scene,
Prefab, ScriptableObject, asmdef, Locale, String Table, font, Localization
Settings, or Addressables asset change is expected.

#### Phase 5 validation gates

The same-revision evidence must prove:

- production options are exactly `en-US / English`, then
  `ko-KR / 한국어`;
- option autonyms are independent of the active UI language: the Korean option
  remains `한국어` in English UI and the English option remains `English` in
  Korean UI;
- a source/architecture guard proves the precomputed option getter/projection has
  no String Table resolve/preload/table-API call; behavioral counters prove its
  Unity locale assignment, preference, locale event, and audio mutations are
  zero without claiming measured preload cardinality;
- catalog stable order is preserved independently of raw registration order;
- synthetic catalog/policy registered-code input proves duplicate registration
  produces one option while Draft, unknown, and aliases are absent, without
  adding/removing anything from the real `LocalesProvider`;
- a synthetic third ShipReady option displays and cycles
  `A -> B -> C -> A` without Presenter/View source changes;
- zero/one-option and missing-current cases call `TrySetLocale` zero times;
  an underlying rejection calls it once; all retain current visible state and
  produce zero write/event/additional-preload/audio effects;
- a true result whose post-call current code does not match the requested target
  produces no Toggle audio and is not reported as an approved UI change;
- an approved user change has the existing Phase 4 apply/save/event cardinality
  and exactly one UI Toggle audio; an external approved change refreshes the
  option and real en/ko font with zero preference/audio effects;
- a Settings harness proves an external rejected/Draft/null event preserves the
  last-approved visible autonym and exact font object, produces zero Toggle audio
  and preference write, and restores `LocalizationSettings.SelectedLocale` in
  teardown without mutating the provider;
- Presenter, View, and Settings payload contain no English/Korean descriptor
  selection field or locale-specific language-name branch;
- the real en-US -> ko-KR -> en-US language label restores exact text and font
  identity through the existing `SettingsAction` binding;
- `UI.ViewShared` remains free of Unity Localization, TMP, Addressables, font,
  material, and handle types;
- tracked diffs under `Assets/Localization` and `Assets/AddressableAssetsData`
  remain empty;
- the final tracked diff matches the Phase 5 code/test/docs allowlist captured at
  phase entry; baseline-relative scans for `*.prefab`, `*.asset`, `*.mat`,
  `*.ttf`, `*.otf`, and `*.meta` prove no new asset mutation, including the
  Settings Prefab, production typography Theme, and governed font paths.

Tests that wrap a real resolver must not synthesize `LocaleChanged` for an
underlying `NoOp`; such wrappers are unsuitable for cardinality evidence. Use a
recording option port that distinguishes `Change`, `NoOp`, and `Rejected`
effects. Poison or source-absence guards must ensure that a synthetic third
option cannot pass by falling through the old English descriptor branch.

The minimum fixed focused fixtures are
`LocaleCatalogSelectionPolicyTests`, `SettingsLocalizationFoundationTests`,
`SettingsProductionLocalizationRuntimeTests`,
`UnityLocalizationStringTableIntegrationTests`, and `UiArchitectureTests`.
Add a governance fixture if its source is changed. Required commands are:

```text
./run_tests.sh ui --filter LocaleCatalogSelectionPolicyTests
./run_tests.sh ui --filter SettingsLocalizationFoundationTests
./run_tests.sh ui --filter SettingsProductionLocalizationRuntimeTests
./run_tests.sh ui --filter UnityLocalizationStringTableIntegrationTests
./run_tests.sh ui --filter UiArchitectureTests
./run_tests.sh ui
```

Every focused XML must contain at least one executed test from its requested
fixture. A full UI pass does not substitute for a missing focused fixture.
Missing XML, zero matching tests, or timeout is failure. `core`, broad `full`,
Player, performance, memory, glyph, font residency, and visual QA are not Phase
5 gates unless a later scope explicitly adds them.

#### Phase 5 no-touch, claim, and rollback boundary

Phase 5 does not:

- add or promote `ja-JP`, `zh-CN`, or another Locale;
- add or edit Locale, String Table, font, material, Theme, Addressables, Scene,
  Prefab, ScriptableObject, or asmdef assets;
- add a dropdown, popup, simultaneous option list, locale-neutral recovery icon,
  or other Settings hierarchy/layout change;
- add a global TMP fallback, cross-locale fallback graph, per-option FontSet,
  recovery font, glyph atlas, font lease, or residency hint;
- change selection/persistence algorithms or the preference interface; Phase 6
  owns that work;
- remove the retained en/ko cross-locale table probes; Phase 7 owns that work;
- separate raw and committed locale state, add transition tokens, guarantee
  atomic font rollback, or consolidate distributed observers; Phase 9 owns that
  boundary;
- introduce the FontSet compiler/cache, binding registry/readiness, rendered
  frame barrier, Addressables loader, lease, packing cutover, or residency
  evidence owned by Phases 10-17.

The rollback unit is the option DTO/port migration, production projection,
Settings Presenter/View branch removal, and their tests. The retained
English/Korean String Table rows and unchanged prefab/theme assets keep the
current asset rollback independent. Reverting this unit restores the temporary
Phase 4 code-list consumer but also restores the two-locale UI shape. If Phase 5
execution updated its execution record, UI baseline, testing guide, or
current-structure source, those evidence documents are part of the same rollback
unit and must be returned to a truthful pre-Phase-5 status.

Passing Phase 5 proves a data-driven single-current-option Settings control. It
does not prove that multiple scripts can be rendered simultaneously, that a
missing/corrupt selected FontSet has a visible recovery path, that Japanese or
Chinese glyphs/fonts are ready, that locale/font transition is atomic, that old
fonts are unloadable, that only one locale is resident, that memory/startup cost
improved, or that a screen reader announces the autonym with correct
accessibility focus order. Those claims require their later same-revision asset,
accessibility, and Player gates.

Phase 5 planning record (2026-09-14): the product rationale approved raw catalog
autonyms so a user in an accidentally selected language can recognize their own
language. Independent read-only architecture, data-model/UX, and adversarial
reviews agreed that the choice is compatible with the current single cycle
label when it uses the selected locale's existing typography path. They also
agreed that simultaneous option display would reopen a font/residency decision,
that global/full-FontSet fallback is not an acceptable shortcut, and that a
compact selector-only recovery font is only a conditional later candidate with
explicit packing, license, glyph, regional-form, accessibility, and Player
evidence. A subsequent adversarial re-review found ambiguous bool-port
cardinality, missing-current handling, default-struct validity, preload evidence,
asset-mutation scope, package-free projection, Phase 9 terminology, recovery-font
packing, accessibility claims, focused fixtures, and rollback evidence. The plan
was amended to close those documentation gaps before implementation. No Phase 5
production code, test, asset, commit, or push was performed while recording this
decision.

### Phase 6 — Unify persistence and fallback selection policy

What is being done:

- share canonical/default/invalid/Draft/alias rules across production,
  package-free, and test resolvers;
- retain the existing preference key;
- save only canonical codes;
- keep same-locale, unsupported, and Draft requests free of writes;
- use this startup precedence: valid canonical/aliased persisted ShipReady code,
  then valid Unity-selected ShipReady locale, then `en-US`;
- treat external Unity locale changes as transition requests but persist them
  only after the same complete coordinator commit used by Settings;
- rewrite an aliased code to its canonical code only after successful commit;
- delete/rewrite an invalid saved value only after the valid fallback locale has
  committed, so a failed startup attempt does not destroy rollback evidence;
- replace the write-only persistence seam with a result-bearing save contract.

The package-free resolver remains a policy-test seam and emergency/bootstrap
fallback only. It must not become a second hand-maintained store of future CJK
strings.

Persistence is deliberately not part of the visual rollback transaction. If a
save fails after the locale/font/string state has committed, the session keeps
the usable committed locale, reports a non-fatal durability warning, and warns
that restart may return to the prior preference. Rolling the entire UI back for
a storage failure would add another visible transition and is not justified for
`PlayerPrefs`. Fault-injection must prove that save failure is observable and
never reported as durable success.

Completion gate: startup precedence and save counts are identical across
production-facing and package-free policy tests; invalid, alias, external
selection, and save-failure cases have explicit results.

### Phase 7 — Remove runtime en/ko cross-locale table probes

Prerequisites:

- Phase 3 validation is enforced;
- Phase 4 prevents Draft/invalid selection;
- Phase 6 persistence/selection precedence tests pass;
- selected-locale missing-entry and lazy-English-fallback behavior is covered.

What is being done:

- remove the hard-coded ko-KR required-locale gate;
- remove the four en/ko UI/Stage sample table queries;
- retain Localization Settings, selected-locale, and minimal selected-table
  health checks;
- retain synchronous initialization because no current user-perceived stall
  requires an async table-loading slice.

“Minimal selected-table health” means opening only the selected locale's first
required UI and Stage tables and resolving the exact bootstrap keys needed by
the first screen. It is not a completeness scan. Missing table, missing key,
empty result, or SmartFormat failure may trigger the documented English
emergency fallback load and diagnostic; no other non-selected table load is
allowed. There is no implicit Korean/Japanese/Chinese-to-CJK fallback chain.

Completion gate: en/ko behavior remains correct and runtime code has no
non-selected completeness probe at cold start or scene entry. The only allowed
cross-locale load is the observed English emergency fallback after one of the
explicit failures above. This is not an async-initialization change.

### Phase 8 — Close the en/ko localization-governance gate

Required evidence:

- real en/ko generic string validation;
- Settings en -> ko -> en option and persistence round trip;
- invalid, Draft, alias, same-locale, and unsupported cases;
- current exact-copy contracts;
- approved lazy fallback behavior;
- no cross-locale startup validation request.

Invalid, Draft, and alias matrices use synthetic policy fixtures. The en/ko
round trip, copy, selected-table load, and production registration evidence use
the real adapter and assets. Alias evidence is either a configured-alias case or
an explicit assertion that the production alias table is empty.

This is a bounded localization gate, not a performance campaign.

### Phase 9 — Separate raw selected locale from committed application locale

What is being done:

- introduce a package-free transition request/status/result seam;
- make the Composition layer the only observer/owner of raw Unity selected
  locale changes;
- migrate production presenters, views, HUD paths, and typography bindings to a
  committed-locale state/event;
- distinguish accepted, applying, completed, failed, and rolled-back outcomes;
- keep the legacy in-memory Theme as the actual font source during this phase.
- record a machine-readable transition journal keyed by transition token and
  frame/update boundary.

The journal vocabulary is at least `Requested`, `CandidateLoadStarted`,
`CandidateLoaded`, `CacheValidated`, `LocaleSelected`, `Committed`,
`BindingApplied`, `LayoutMeshCompleted`, `Rendered`, `OldLeaseReleased`,
`Settled`, `Failed`, and `RolledBack`. Tests validate allowed ordering,
cardinality, and a common token across locale/string/font observations. A final
screenshot or settled state alone cannot prove the absence of a mixed frame.

Why: distributed raw LocaleChanged subscribers can otherwise observe new
strings before the candidate font/cache is committed, making atomic transition
impossible.

Completion gate: a source/architecture guard allows no raw package locale
subscription outside the approved Composition facade, and current en/ko visible
behavior is unchanged.

### Phase 10 — Define the FontSet asset schema and single-locale compiler

What is being done:

- separate common semantic/sizing/spacing/style policy from locale font objects;
- introduce an asset-compatible TypographyFontSet root that may own multiple
  fonts, materials, atlases, and an explicit fallback graph;
- compile one immutable resolved-style cache for the candidate/committed locale;
- derive required style cardinality from policy/enum definitions instead of a
  literal 19;
- add font/material, missing-style, fallback-cycle, cross-locale dependency, and
  common-policy reference validation.

`CommonTypographyPolicy` introduced here is a new font-object-free asset. The
legacy `GameplayUiTypographyTheme` remains unchanged as the temporary adapter
source through the rollback window; Phase 10 must not strip its references.

Why before the loader: candidate validity is the cache/compiler result the
coordinator must consume.

Completion gate: current en and ko data each independently compile all governed
styles, while the common policy contains no locale font/material reference.

### Phase 11 — Prove a coordinator with the existing in-memory Theme adapter

What is being done:

- implement transition tokens, duplicate-request rejection, candidate
  validation, commit, rollback, post-commit persistence result, and stale
  completion;
- inject the existing Theme data through the new FontSet/cache contract;
- fault-inject pre-commit failure, post-selection failure, binding failure,
  destruction, and rollback without introducing Addressables yet;
- keep legacy production wiring available for one-commit rollback.

Trade-off: the new and legacy contracts coexist temporarily, but lifecycle
semantics can be debugged without adding packaging failures at the same time.

Completion gate: the pure/in-memory coordinator never exposes partial committed
state, performs preference writes only after a complete UI commit, reports
durability failure without claiming success, and produces a valid transition
journal for success and every injected failure point.

### Phase 12 — Add full-lifetime binding registry and font readiness

What is being done:

- register active, inactive, pooled, dynamically created, and newly enabled
  bindings for their object lifetime;
- expose the committed current-style provider;
- require all mandatory binding acknowledgements, required TMP layout/mesh
  rebuild completion, and at least one safe rendered-frame boundary before old
  FontSet release;
- add a bounded applying/error state and reject concurrent requests in v1.

String-table async migration remains deferred. Candidate FontSet readiness is
still required because the later Addressables load is asynchronous.

Whether implementation uses explicit `ForceMeshUpdate` or keeps the transition
cover active until the next render boundary is an implementation decision. The
release barrier itself is mandatory.

Completion gate: missing registrations are detected, destroyed bindings are
removed, newly created bindings receive the current committed state, and the
transition journal proves `BindingApplied -> LayoutMeshCompleted -> Rendered ->
OldLeaseReleased` for every successful switch.

### Phase 13 — Remove prefab and captured locale-font references

What is being done:

- migrate governed prefab groups to an approved small bootstrap font;
- remove locale-specific direct font/material references;
- change TypographyBinding and LocalizedTmpTextBinding fallback capture so they
  do not retain an old locale graph;
- include HUD/objective restore paths, TMP defaults, and Resources-based defaults
  in a narrow allowlist and validator;
- keep the in-memory Theme adapter supplying the selected runtime font.

The captured-reference audit is not YAML-only. It inspects active, inactive,
pooled, and newly instantiated production objects, including
`LocalizedTmpTextBinding` defaults, `TypographyBinding` original-state capture,
HUD/objective restoration, TMP defaults, and Resources paths.

Commit boundaries: screen, popup, HUD/overlay, ScriptableObject, and Scene
changes remain separately reviewable where practical.

Completion gate: serialized and runtime-captured locale references are absent
outside the bootstrap/shared allowlist. Unloadability is not claimed yet because
the legacy Theme still owns all locale fonts.

### Phase 14 — Author en/ko FontSets and Asset Table in shadow mode

What is being done:

- create independent en-US and ko-KR FontSet assets and Asset Table entries;
- keep String Tables in their existing groups;
- place typography roots in explicit per-locale font groups rather than assuming
  generated packing is correct;
- perform a fresh Addressables packed build and save the JSON Build Layout;
- validate each locale root's transitive closure and shared-dependency allowlist;
- define exact group names, BundleMode/packing policy, and the permitted shared
  font/material dependency GUID allowlist;
- prohibit a locale FontSet closure from containing another locale's font,
  atlas, material, GUID, or local file ID;
- disable preload on the Typography Asset Table and entries, and verify that no
  automatic `LocalizedAsset<T>` owner is present;
- keep production on the in-memory adapter.

Canonical group layout:

| Group | Bundle policy | Allowed contents |
|---|---|---|
| `UI-Typography-en-US` | Pack Together, one locale closure | en-US FontSet root, fonts, atlas textures, locale-owned materials |
| `UI-Typography-ko-KR` | Pack Together, one locale closure | ko-KR FontSet root, fonts, atlas textures, locale-owned materials |
| `UI-Typography-ja-JP` | Same policy, created only in Phase 19 | ja-JP closure |
| `UI-Typography-zh-CN` | Same policy, created only in Phase 20 | zh-CN closure |
| `UI-Typography-Shared` | Pack Together | only reviewed shader/common non-font dependencies whose GUIDs are on the shared allowlist |

Font assets, atlas textures, and materials that reference an atlas are never
placed in `UI-Typography-Shared`. Existing String Table groups and their preload
settings remain unchanged. If Unity-generated localization groups require a
physical naming variation, the checked-in group-to-logical-name manifest is the
authority and must preserve the one-locale-per-bundle policy.

Trade-off: local per-locale bundles improve runtime residency isolation but may
slightly increase installation metadata/bundle overhead.

During shadow mode the legacy Theme may cause a known temporary duplicate
between the base Player dependency graph and the new Addressables font bundle.
Phase 14 forbids bundle-to-bundle duplicates; it records the temporary
base-versus-bundle duplicate for removal at cutover rather than reporting a
false failure.

Completion gate: each locale closure contains its own expected font/material/
atlas graph, no other-locale GUID/local ID, no bundle-to-bundle duplicate, and
the exact fresh catalog/build-layout hashes used by Phase 15 are retained.

### Phase 15 — Add the explicit Asset Table lease loader

What is being done:

- load a FontSet for an explicit candidate locale;
- make the Composition coordinator the sole authoritative lease owner;
- balance acquire/release through success, failure, cancellation, stale result,
  and destruction;
- avoid mixing automatic LocalizedAsset lifecycle ownership with a second
  explicit owner.

The real packed path means PlayMode `Use Existing Build` against the exact Phase
14 catalog/build-layout hash, not AssetDatabase simulation. AssetDatabase/fake
tests remain separate fast evidence.

Completion gate: fake and exact-build real packed load paths agree, no preload
or automatic secondary owner exists, and no Addressables handle escapes the
Composition boundary.

### Phase 16 — Cut production composition over to the Asset Table path

What is being done:

- replace the in-memory font adapter at one composition seam;
- perform candidate FontSet load/cache validation, string readiness,
  committed-locale swap, all-binding apply, preference save, and old-lease
  release barrier as one ordered transition;
- expose applying/failure state to Settings;
- retain legacy assets/code for bounded rollback, without keeping permanent dual
  production paths.

At cutover, production Catalog/Scene serialized references to the legacy Theme
are severed so the base Player graph no longer owns locale fonts. The legacy
adapter code and source assets remain unreferenced for a bounded rollback window;
they are not a second active runtime path.

Required focused evidence: en -> ko, ko -> en, no-op, rapid input, load and
validation failure, scene change, destruction, inactive/pool reactivation, and
zero mixed string/font committed frame.

Completion gate: `./run_tests.sh ui` passes and the focused PlayMode transition
suite is mandatory. The transition journal proves candidate load, cache/string
readiness, commit, binding, layout/mesh, render boundary, persistence result,
old-lease release, and settle ordering with no mixed token/frame.

### Phase 17 — Run the pre-retirement packed Player gate

This same-revision gate runs while the unreferenced legacy adapter/source assets
still exist for bounded rollback. It is mandatory structural evidence, not the
deferred heavy performance campaign.

~~~text
fresh Addressables build and hash capture
  -> Player build from that exact catalog
  -> en cold and ko cold launch
  -> en <-> ko
  -> one failed candidate rollback
  -> scene change and coordinator destruction
  -> bounded repeated switching
~~~

Required diagnostic capabilities:

- join Player built-in dependency data and Addressables Build Layout by
  GUID/local ID;
- inspect per-locale transitive bundle closure;
- read the coordinator lease ledger;
- inspect Addressables asset/bundle ownership or an equivalent supported
  diagnostic;
- scan live FontSet/cache/binding strong references after settle;
- capture the transition journal through the rendered-frame boundary.

Verdicts are `PASS`, `FAIL`, or `INCONCLUSIVE/HOLD`. Missing a required
diagnostic is never a pass “within available limits.” The following independent
gates must all pass:

1. coordinator-owned lease acquire/release ledger is balanced;
2. underlying Addressables asset and bundle references settle as designed;
3. old FontSet, cache, material/font binding, and captured strong references are
   absent after settle;
4. the base Player/non-Addressable closure contains only the approved
   bootstrap/shared font graph, while each locale font graph exists only in its
   intended Addressables closure;
5. no mixed committed string/font frame, exception, or preference corruption is
   present.

Logical release and reference absence do not prove immediate OS RSS reduction.
Duration and RSS may be recorded as diagnostics, but this phase makes no
performance-improvement or absolute CJK-budget claim.

Completion gate: all five gates pass on the exact built revision. Failure keeps
rollback to restoring the prior composition reference; `INCONCLUSIVE/HOLD`
blocks retirement and CJK import.

### Phase 18 — Close rollback window, retire legacy graph, and recheck

What is being done after Phase 17 passes and its observation/rollback window is
explicitly closed:

- delete the now-unreferenced in-memory font compatibility adapter and legacy
  all-locale Theme assets when their source-font retention policy permits;
- remove only the obsolete font/theme resolver path, not the active String Table
  resolver;
- remove old-locale references from cache, binding fallback, and production
  serialized graphs;
- repeat the focused UI/PlayMode tests and a bounded packed Player structural
  smoke on the retired revision.

Completion gate: the retired revision preserves Phase 17's five structural
properties and production dependency closure has no legacy locale-font edge
outside explicit bootstrap/shared policy. After this point rollback is a Git
revert/reintroduction change, not an in-Player adapter switch.

### Phase 19 — Add ja-JP as a Draft locale

Add locale metadata, String Tables, FontSet, glyph corpus, atlas strategy,
license record, Japanese line-breaking/kinsoku checks, and visual evidence as
separate bounded changes. Keep the locale outside production
`AvailableLocales` and normal Settings selection. Authoring validation emits
structured Draft incompleteness diagnostics; an explicit candidate build
profile may register and select only `ja-JP` for real Player QA.

If the real Japanese FontSet requires a schema not supported by Phase 10, keep
the locale Draft and reopen that contract instead of adding locale-specific
runtime branches.

Completion gate before starting `zh-CN`: en/ko Phase 18 evidence remains green,
Japanese Draft diagnostics are explicit, no production exposure or preload is
introduced, the bundle closure is isolated, provenance/license records exist,
and no Japanese-specific runtime branch is added. Draft visual evidence is not
ShipReady admission.

### Phase 20 — Add zh-CN as a separate Draft locale

Add Simplified Chinese metadata, tables, FontSet, glyph corpus, atlas strategy,
license record, line-breaking checks, and visual evidence independently from
Japanese. Do not use Japanese or another Chinese variant as an implicit
cross-locale font fallback. Keep `zh-CN` outside production `AvailableLocales`
and expose it only through the candidate build profile until promotion.

Adding one CJK locale at a time makes bundle/fallback regressions attributable
to the introducing change.

Completion gate: en/ko and Japanese evidence remain valid, `zh-CN` Draft
diagnostics are explicit, no production exposure/preload or cross-locale
dependency exists, provenance/license records exist, and no Chinese-specific
runtime branch is added. Draft visual evidence is not ShipReady admission.

### Phase 21 — Run formal candidate admission and promote independently

For each Draft locale:

- validate complete/non-empty tables and SmartFormat semantics;
- validate actual font, glyph, material, fallback, and license contracts;
- run layout, line-breaking, and visual QA;
- measure cold/steady residency, old-plus-new transition peak, duration, and
  repeated-switch plateau on the target Player;
- before promotion, use the development/candidate build profile to verify real
  transition, failure rollback, memory, packing, and visual behavior while the
  locale remains absent from normal Settings;
- promote only that locale in an atomic change covering lifecycle metadata,
  production `AvailableLocales` registration, normal option exposure, packing
  metadata, and restart persistence;
- after promotion, build the production profile and smoke cold start, saved
  restore, normal selection, rollback, and the absence of the development-only
  selector.

Because the heavy pre-change cohort was skipped, this phase can prove absolute
budgets and selected-locale-only residency but cannot support a statistically
strong exact before/after improvement claim.

### Optional branch — KBO atlas Read/Write experiment

The KBO Read/Write experiment is independent from the structure-first sequence
and is not a CJK import prerequisite. If approved, run it as a separate
asset-generation/Player A/B change with glyph updater, asset-integrity, rendering,
and measured memory evidence.

## 7. Japanese and Chinese Addition Sequence

Japanese and Simplified Chinese are consumers of the generalized architecture,
not fixtures used to design it. Production CJK assets are added only after the
current en/ko structure passes the Phase 18 packed structural gate.

| Order | Deliverable | Purpose |
|---|---|---|
| 1 | Phases 0-8 | Generalize locale governance, validation, selection, persistence, and runtime table probing while preserving en/ko behavior |
| 2 | Phases 9-13 | Establish committed locale state, the FontSet/cache contract, transaction semantics, binding readiness, and bootstrap-only prefab dependencies |
| 3 | Phases 14-16 | Author the real en/ko Asset Table path and cut production over while retaining an unreferenced rollback adapter |
| 4 | Phase 17 | Prove packing, three-layer ownership/release, rollback, and en/ko Player composition on the exact packed revision |
| 5 | Phase 18 | Close the rollback window, retire the legacy graph, and repeat bounded structural validation |
| 6 | Phase 19 | Add ja-JP as a hidden Draft and validate its real content/font requirements |
| 7 | Phase 20 | Add zh-CN independently as a hidden Draft |
| 8 | Phase 21 | Run candidate-build Player/visual admission, then promote each locale independently |

There is an explicit product go/no-go after Phase 8. If Japanese/Simplified
Chinese delivery is cancelled or no longer likely, stop there: no current
user-perceived stall justifies building the FontSet/Addressables lifecycle by
itself. If CJK remains planned, continue Phases 9-18 before importing CJK
assets. If Phases 9-18 have already been completed when CJK is later cancelled,
the work may stop after Phase 18. The acceptance target is that adding a Draft
requires metadata, tables, a FontSet, and evidence—not new locale-specific
runtime branches.

## 8. CJK-specific Validation Matrix

| Area | Required evidence before ShipReady |
|---|---|
| Copy completeness | All required table entries are non-empty; SmartFormat argument names and semantics match |
| Glyph coverage | Static primary/fallback chain covers the governed corpus with no undocumented system-font dependency |
| Line breaking | Japanese prohibited line-start/end punctuation and the approved `zh-CN` line-break policy are visually verified |
| Layout | Long labels, buttons, dropdowns, popups, Stage names, results, and dynamic settings status are checked at target resolutions |
| Typography | Weight hierarchy, punctuation alignment, numerals, Latin mixing, and fallback baseline are approved |
| Locale options | Display names and ordering are data-driven; no two-locale ternary remains |
| Persistence | Saved locale survives restart and an unavailable/renamed locale falls back deterministically |
| Runtime transition | Active, inactive, pooled, and newly created TMP bindings use the committed FontSet |
| Failure behavior | Missing bundle/table/font, invalid FontSet, cancellation, and rapid repeated requests roll back safely |
| Memory | Steady state, old+new transition peak, and repeated-switch plateau meet approved budgets |
| Packaging | Build Layout shows expected per-locale bundles and no unintended cross-locale dependencies |
| Licensing | Source font, derived SDF atlas/material, notices, and product-identity use are approved |

Automated UI architecture changes require `./run_tests.sh ui`. Locale transition
lifecycle that cannot be proven in EditMode should receive a focused PlayMode
test under the repository's documented escalation rules. Visual acceptance and
Player memory capture remain companion evidence; neither is replaced by a green
EditMode lane.

## 9. Principal Trade-off Summary

| Decision | Selected direction | Accepted cost |
|---|---|---|
| Initial measurement | Defer the heavy current-Player campaign; retain a structural baseline | No exact pre/post MiB or millisecond improvement claim |
| Optimization motivation | Japanese/Chinese extensibility, not current lag remediation | Current latency remains formally unmeasured until candidate-locale admission |
| Runtime verification | Selected locale only; exhaustive checks in CI | An unselected broken Draft locale is not discovered by ordinary startup |
| Draft Player QA | Development/candidate profile only before promotion | Another build profile and a strictly excluded development selector must be governed |
| Font storage | Per-locale FontSet in Asset Table | More asset authoring and lifecycle code |
| Runtime ownership | Explicit coordinator and handles | More state/error tests than event-only binding |
| Transition | Atomic swap with rollback | Temporary old+new peak memory |
| Preference failure | Keep the usable session commit and report durability warning | Restart may restore the previous saved locale |
| Concurrent input | Reject while applying in v1 | Short-lived disabled control/loading state |
| Prefab font | Small common bootstrap plus readiness gate | Small shared font always resident |
| CJK atlas | Decide per locale from corpus and budget | No single atlas strategy reused by assumption |
| Locale prefab | Shared prefab by default | Structural exceptions need a separately governed variant |
| Chinese scope | `zh-CN` Simplified Chinese only; Traditional Chinese deferred | A later Traditional Chinese decision requires separate tables, assets, QA, and promotion |

## 10. Risks, Rollback, and Stop Conditions

- If Phase 3 validation is not reliable, do not remove runtime probes in Phase
  7.
- If direct or captured prefab references remain after Phase 13, do not claim locale font
  unloadability.
- If raw Unity locale subscribers bypass the committed-state facade after Phase
  9, do not cut production over to the transactional path.
- If the single-locale compiler/cache cannot validate a candidate FontSet, do
  not add or release an Addressables lease for that candidate.
- If the Phase 14 packed layout contains a cross-locale font dependency, do not
  perform the Phase 16 production cutover.
- If a transition cannot restore the old locale after injected failure, do not
  persist the new preference or expose additional locales.
- If the Phase 17 pre-retirement packed gate is `FAIL` or `INCONCLUSIVE/HOLD`,
  do not retire the legacy rollback path or import CJK font assets.
- If the Phase 18 retired-revision recheck fails, restore by Git revert and do
  not import CJK font assets.
- If CJK font licensing does not explicitly cover the distributed SDF outputs,
  keep the locale Draft and do not ship those assets.
- If transition peak exceeds budget, first reduce FontSet dependencies/atlas
  scope; do not release the old set before commit merely to improve the number.
- If a static atlas cannot cover governed generated content within budget,
  reopen the dynamic/fallback decision instead of silently accepting missing
  glyphs.
- Each phase must be independently revertible. String validation relocation,
  font asset separation, prefab reference cleanup, and Read/Write experiments
  should not be combined into one irreversible asset change.

## 11. Open Decisions and Owners

| Decision | Required owner |
|---|---|
| Any future Traditional Chinese scope and canonical code | Product/publishing |
| Locale-specific copy ownership and sign-off schedule | Product/localization/UX |
| Japanese and Chinese font families, weights, and brand-use boundary | Art/UI/legal |
| Curated-static corpus feasibility and any approved fallback exception per locale | UI engineering/technical art |
| Startup, steady-state, transition peak, and duration budgets | Engineering/product |
| Loading/applying/error presentation | UX/UI engineering |
| Bootstrap font and direct-reference allowlist | UI engineering/technical art |
| Target devices and Player capture protocol | QA/performance engineering |
| Governed required-key descriptor ownership | UI engineering/localization |
| Candidate build profile signing/distribution and selector stripping proof | Build/QA |
| Phase 17 required Addressables/live-reference diagnostics | UI engineering/QA |

`ja-JP` and `zh-CN` are the approved Draft identities, but their font, corpus,
copy, layout, license, budget, and ShipReady decisions remain unapproved until
Phases 19-21. Phases 0-3 are complete, so the safe next action is the separately
requested Phase 4 ShipReady-only runtime-selection gate. It is not a
lag-remediation campaign and it is not production CJK font import.
