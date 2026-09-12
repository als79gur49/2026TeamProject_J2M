# UI Current Structure Source

This file is the external current-structure source for the completed UI cleanup wave. It records the current runtime shape for documentation regeneration and stale-token audits. It is not a runtime implementation spec and does not authorize code, prefab, catalog, settings, audio, or display-path changes.

## Canonical Runtime Structure

- Root shell layers:
  - `HudLayer`
  - `ScreenLayer`
  - `PopupLayer`
- `DiagnosticsLayer` is absent.

- `ScreenId`:
  - `None`
  - `Gameplay`
  - `Settings`
  - `StageResult`
  - `LevelFailed`
  - `GameClear`

- `PopupId`:
  - `None`
  - `Pause`
  - `Confirm`
  - `DemoStageControl`

- Current HUD members:
  - `Pause`
  - `StageInfo`
  - `ObjectiveHud`
  - `ChancePanel`
  - `SurfaceBeltIndicator`
  - `PlayerStatus`

- HUD responsibility:
  - HUD is a display consumer of mapped UI presentation state.
  - HUD is not a gameplay command owner.
  - HUD may raise bounded UI-owned requests such as pause flow, but it must not dispatch gameplay Push/Flip commands.
  - `PlayerStatus` displays current player status/readiness state only; it does not own Push/Flip command routing.
  - `SurfaceBeltIndicator` authors one number-free 32x32 `NormalBadge` only to the left of the centered `Cell_0` visual. Its frame and fill share the Objective completion gold, and the current sector's `HasAnyRemaining` value selects fully lit or dim inactive alpha. Initial binding is immediate; later state changes use local DOTween color/scale transitions, while entry into a new active sector plays one runtime-isolated All In 1 Shine and identical binds do not replay it. Neighboring sector cells author no badge.
  - The in-game Stage Name resolves `HeaderLarge` through `GameplayUiTypographyTheme` in both locales: en-US uses Orbitron ExtraBold and ko-KR uses KBO Dia Gothic Medium, prefab-authored sizing remains unchanged, and the target adds TMP `UpperCase` presentation without mutating localized source strings. World Guide and transition-label default-locale restoration remain separate contracts.

## Preserved Classification Decisions

- `DemoStageControl` is not a gameplay popup catalog entry.
- `DemoStageControl` is a catalog-less runtime assist popup.
- `DemoStageControl` is a build-included tester/demo/showcase assist feature for tester assist clear, hard-section bypass, showcase navigation, and stage browsing.
- `DemoStageControl` is not a deletion candidate and is not a dev-only compile exclusion target.
- Future public-release hiding or disabling for `DemoStageControl` requires a separate product/build configuration decision.
- `TooltipPopup` was retired from the current popup vocabulary after PR-TT1 found no production caller. Settings display hover hint remains as a local inline pointer-hover affordance and does not use `PopupId.Tooltip`.
- Reward popup is not current popup vocabulary. Stage reward/progression vocabulary remains stage-owned content/system vocabulary, not a UI popup route.
- Stage clear routes through `MinimalStageCompletionReadModel -> StageResult`.
- `StageResult` is a minimal stage-completion navigation endpoint. It no longer carries or displays title/summary/detail result text; continue, retry, and next-stage paths remain `StageNavigationRequest` intent boundaries.
- `GameClear` is a result-only terminal screen with title and main label bindings only; retired authored restart/detail compatibility objects are not current contract.

- `ActionBar` is removed retired HUD proof residue. It is not a current HUD member.
- `ActionBarView` and `ActionBarPresenter` are not current display components.
- If an ActionBar-like display is reintroduced later, it must be documented as display-only and must not restore Push/Flip command injection.
- `Help` and `Inventory` are not current gameplay screens.
- UI diagnostics are removed unused runtime feature residue:
  - no `DiagnosticsLayer`
  - no `UiArchitectureDiagnostics`
  - no `DiagnosticsOverlay`
  - no F3/F4 diagnostics overlay input path
  - not a hidden, retained, or protected runtime path

## Canonical Runtime Paths

- The shared intro/outro comic path remains `ComicSequenceDefinition -> ComicSequenceFlowCoordinator -> ComicSequenceOverlayView` and accepts sprite-sequence content only.
- The Comic runtime path has two distinct authoring classifications. The fixed `ComicSequenceOverlayView` base shell (`Background`, page/final viewports, final image, black-fade layer, `CanvasGroup`, and `AudioSource`) is still created from primitives by both production installers and remains a prefab migration candidate. The variable non-interactive panel Image pool is sized and positioned from each `ComicSequenceDefinition` and remains runtime infrastructure. No documented fixed-shell runtime-generation exception is currently approved.
- Comic entry keeps the overlay background transparent while its dedicated fade layer covers the visible source scene to opaque black. Only after full cover does the overlay fix its background to black, configure the first comic content, and begin the initial reveal; the authored enter-fade duration therefore remains a real presentation transition rather than an invisible fade over an already-black root.
- Comic audio focus consumes an opaque `BgmRequestRouter` playback-suppression lease through the same-root `GlobalAudioFlowBootstrap`. Cancellation, setup failure, stale ownership, and immediate route rejection restore the router's current highest-priority request; only a synchronously accepted scene route commits the handoff without restarting the source-scene BGM. UI composition does not capture or resubmit BGM requests or profiles.
- The intro definition owns two pages with normalized 1920x1080 reference rectangles (currently 5 panels then 6 panels), followed by a final before/after sprite pair. Pointer left-click or one UI Submit advances one state; the background is not a `Button`, so one Submit cannot traverse both paths.
- Main Menu composes the authored intro definition. Gameplay composes the authored one-page outro definition through `_outroComicSequence`; after final clear, the Game Clear Main action presents six cumulative panels in the fixed `1-1 -> 1-2 -> 1-3 -> 1-4 -> 1-5 -> 1-6` order, then routes through `ComicOutroToMainMenu` and marks `OutroComicCompleted` only after the route is synchronously accepted. A completed slot continues to use the regular `ReturnToMainMenu` path without replaying the outro.
- Main Menu save access is globally gated: unsupported-version or corrupt profiles replace all slot cards with retry and destructive full-reset actions, while IO/authorization failures expose retry only. A durable pending reset is also a global retry-only gate: startup skips legacy migration when resume fails, every campaign save write remains blocked, and Retry resumes the same reset before normal slot access returns. Reset revalidates the failure, archives canonical/backup files, writes one empty current-schema profile, and clears active launch state; it is not a per-slot delete path or a schema migration.
- Main Menu Continue reserves its application-session handoff before calling the Stages-owned `ICampaignContinuePreparationPort`. The command contains only expected slot/stage/persisted-group and the resolved target group; UI never submits a complete slot replacement. Missing, completed, or stale preparation clears only the original token, refreshes the slot panel, and does not route, while a returned committed state must match the reserved stage and resolved group before routing.
- Main Menu slot cards are projected only from `CampaignSlotEntry`, `CampaignSlotLaunchEvaluation`, and `CampaignSlotActionPolicy`. The presentation input rejects mismatched entry/evaluation identity or a policy not derived from that evaluation. Profile load failures stay on the global blocked-recovery path; sequence/catalog launch failures stay on occupied per-slot restart/delete cards. The retired combined validation service/result/status and corrected mutable clone are not current UI dependencies.
- Main Menu save-slot typography keeps ordinal fallback only for each SaveSlotCard's eight-target contract. The non-card `BlockedSaveRecovery` title, detail, retry label, and reset label require authored `TypographyBinding` tags `HeaderMedium`, `Body`, `Button`, and `Button`; the shared theme resolves locale font/material while authored sizing is preserved.
- `VideoClip`, `VideoPlayer`, `CinematicVideoOverlayView`, `SlotCinematicDefinition`, and the VQ intro/outro MP4 assets are retired and are not current dependencies.
- Comic-sequence layout is resolution-independent: reference rectangles are converted to anchors under a 16:9 fitted viewport, and source sprites retain aspect ratio. Non-16:9 displays therefore use fitted letterboxing rather than stretching.
- Panel sprites are pre-cut assets whose aspect ratios match their authored reference rectangles; replacing a panel sprite preserves layout when the replacement uses the same cut/aspect. A full 1920x1080 scene dropped into a non-16:9 panel is intentionally fitted and will letterbox because the current path has no per-panel crop/mask authoring.
- Current panel pixel dimensions are approximately native for the 1920x1080 reference layout and will upscale at 2560x1440. For 1440p-quality replacements, author each pre-cut panel at least 1.334 times its reference-rectangle width and height (or use a larger same-aspect source); importer `maxTextureSize=4096` only prevents import downscaling and does not create source detail.
- Push/Flip physical gameplay commands flow through the gameplay input route, not UI HUD command injection.
- `RequestPush`, `RequestFlip`, `BufferUiPush`, and `BufferUiFlip` are removed UI command-route vocabulary and are not current paths.
- Settings/rebind Push/Flip UI remains active for binding display, override, save, and restore through the shared `GameplayInputActionPaths` input contract.
- Settings/rebind setup fails fast when required action paths or Push/Flip keyboard bindings are missing; Flip remains keyboard-only and no gamepad binding is added.
- Settings production composition is shared: `MainMenuUiFlowInstaller` and `GameplayUiFlowInstaller` both use `GameplayScreenPrefabCatalog -> SettingsScreenRuntimeBuilder`.
- `GameplayScreenPrefabCatalog.SettingsPrefab` and `SettingsTypographyTheme` are the authoritative Settings asset sources. Main Menu popup typography is not a Settings theme fallback.
- `MainMenuSettingsRuntime` is a thin overlay adapter for Back/popup action forwarding, open/dispose, and navigation target exposure; it does not assemble Settings presenters or child runtime behavior.
- Main Menu Settings does not have a remaining runtime-generated visible-screen hierarchy. `MainMenuSettingsOverlayController.EnsureOverlayLayer()` creates only the modal ordering root, technical pointer blocker, and Content mount around the authored `SettingsScreen.prefab`; these are runtime infrastructure, not independent navigation actions or prefab migration debt.
- Main Menu popup content is also prefab-first. `MainMenuUiFlowInstaller.EnsurePopupLayerView()` creates only the technical Backdrop/Content mount, while `GameplayPopupRuntimeFactory` instantiates the authored `ConfirmPopup.prefab` for the player-visible action hierarchy. The Backdrop Button follows popup consume/close policy and is not an independent keyboard navigation target.
- Settings movement selection is one authored `MovementInputRow/WASDKeyDisplay` toggle. Pointer click uses the authored button feedback and toggles `WASD <-> Arrow Keys`; keyboard focus uses `Input.Movement.Toggle`, its authored `SelectionFrame`, and one Enter submit. The retired movement Slider, separate Arrow/WASD display groups, active Lights, toggle label, and movement-current text are not current contract.
- Settings language selection is the authored `Display.Language.Button` focus node. Pointer click and keyboard Submit both invoke `SettingsDisplayView.ClickLanguageCycle` exactly once, the existing authored `SelectionFrame` reveals focus, and the node is skipped when language selection is unavailable.
- Settings locale changes refresh shell, Audio, Display, and Input strings while preserving active preview/rebind/status ViewModels. The Settings runtime owns and releases the locale subscription.
- Settings font, shared material, and font style come from `GameplayUiTypographyTheme.Resolve(locale, styleTag)` in production; the legacy `_koreanSettingsFont` composition path is absent.
- Settings typography inventory is closed over every authored TMP target: 45 TMP targets, 45 unique valid binding targets, and 45 manifest classifications; the governed localized subset is 22 static plus 11 dynamic/special targets.
- The 35 governed localized Settings targets use Settings-specific semantic profiles where shared tags would change other UI: en-US resolves exactly to each prefab-authored font/material/fontStyle, while ko-KR resolves through the current all-19-role KBO Dia Gothic Light/Medium font/material mapping with Normal style and authored sizing preserved.
- Current Korean typography layout contracts are Pause title width `160` with its authored visual center preserved, Settings audio value Rect/preferred width `140`, and Display status height `28` with `14 / Auto / 10-14`; the Display status may use two Korean lines without clipping.
- Pause campaign progression is a prefab-authored, clamped horizontal `ScrollRect` of stage preview images. Each entry renders only its stage image: line, node background, group badge, state overlay, current frame, and progression selection frame are absent. The selected image reserves twice the normal width so `HorizontalLayoutGroup` moves adjacent images away, Left/Right changes the clamped selection, and a selected click or Submit opens a pause-owned full-canvas preview. The preview overlay owns its Close button as a one-slot local navigation domain with an authored `SelectionFrame`: every open begins with frame and keyboard-selected scale hidden, the first subsequent Submit or Navigate reveals Close, pointer and the following focused Submit share one close-request path, Cancel remains the nested-state shortcut, and close/focus loss clears the effects before returning to the preserved progression selection. Actual pointer hover remains available from open. The current stage only chooses the initial selection; after browsing there is no separate current-stage decoration. Strip and preview stage names share the selected `LocalizedTextDescriptor`, and production localization composition exclusively owns the rendered localized copy.
- KBO Dia Gothic committed source identity is guarded from `HEAD` Git blobs before Unity runs. Unity-loaded font/material identity, glyph/fallback, theme roles, and rendering remain the runtime contract; importer-derived working-file hashes and ScaleRatio values are diagnostics rather than production source inputs.
- Retired Nanum and Climate Crisis KR font/material assets are not current runtime dependencies; ko-KR role resolution is fully KBO Dia Gothic Medium/Light.
- Settings resolution dropdown caption, authored item template, and generated live item labels use the same theme. An open list is restyled in place; numeric/symbol resolution option strings remain the current raw locale-neutral exception, and future localized options require descriptor-backed option models.
- Settings tooltip on/off and large text on/off accessibility toggles are removed residue. `AccessibilitySettingsStore` is not a current runtime composition dependency.
- Scene transition UI uses only `SceneTransitionOverlayShell` plus `SceneTransitionOverlayContentCatalog`.
- Scene transition semantic ids are preserved, but semantic ids and physical content prefab files are not one-to-one.
- `StageClearNext` resolves exactly to the physical `GenericLoadingOverlayContent` prefab; this is the authored StageAdvance content primitive, not a fallback.
- `DeathRetryChanceLost` resolves exactly to the dedicated `ChanceLostOverlayContent` prefab because it owns slot/effect-driven chance-loss visuals and does not expose dynamic previous/current/total/death chance text bindings; its current authored slots use explicit inspector-bound slot roots, and `ChanceSlotView*` name fallback exists only as a safety net.
- Other canonical transition kinds use typed Iris executors and do not infer content through an overlay-kind or generic catalog fallback.
- Scene transition content base views expose only root group and progress text as required inspector bindings; title/message/progress bar/animator base bindings are not current contract.
- Scene transition overlay models carry semantic identity, input/progress state, and typed chance-loss numeric state only. Generic `Title` and `Message` payload/model members are retired because no transition renderer consumed them; the coordinator does not resolve player-facing transition copy.
- `LevelFailedRestart` has no current dedicated message/text content contract; the old LevelFailed-only transition message field was removed as stale residue.
- `SceneTransitionOverlayView`, `UI/SceneTransitionOverlayView`, generated fallback, and legacy overlay fallback are not current paths.
- `UiNavigationInputRouter` is a resolver-only input router initialized through `IUiNavigationTargetResolver`.
- `UiNavigationInputRouter` must not regain `PopupController`, `PopupLayerView`, or `MainMenuScreenView` direct legacy overloads.

## Protected Boundaries

- Do not modify runtime code for this source regeneration.
- Do not modify prefabs or catalogs for this source regeneration.
- Do not change `DemoStageControl` runtime behavior.
- Do not simplify or reroute StageResult, Pause/Confirm popup, settings, audio, display, or UI bridge paths.
- Do not reintroduce a Main Menu-only Settings presenter/view composition, `_settingsScreenPrefab`, `_koreanSettingsFont`, or `PopupPrefabCatalog.TypographyTheme` as a Settings asset source.
- Do not restore StageResult result title/summary/detail schema or title/detail labels without a new product decision.
- Do not restore Settings tooltip on/off or large text on/off toggles without a separate product decision.
- Do not revive `ActionBar`, diagnostics runtime UI, or `SceneTransitionOverlayView`.
- Do not restore the MP4/VideoPlayer path; new intro/outro content is authored as `ComicSequenceDefinition` sprite sequences.
- Do not restore `Help` or `Inventory` as current gameplay screens.

## Deferred Policy Items

- UI audio user settings policy: UI SFX remains authored/routed through hidden `Ui`, but its effective user-facing mix follows `Master` and `Sfx` volume/mute plus hidden `Ui` state. Settings still exposes only `Main`, `Bgm`, and `Sfx`; there is no visible UI volume/mute row. `Voice` and `Ambience` do not follow `Sfx`.
- Inventory is not active authoritative UI. Reintroduction requires a separate `InventoryReadModel`, `InventoryCommandPort`, and permission model design.
- Diagnostics behavior is unchanged. Current work may strengthen production residue guards, but must not add a new diagnostics runtime feature.
- TMP, localization, accessibility, and layout modernization are separate contract work. Do not treat Text/TMP swaps as part of this UI architecture refactor.
