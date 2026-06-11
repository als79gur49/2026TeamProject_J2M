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
  - `Tooltip`
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

## Preserved Classification Decisions

- `DemoStageControl` is not a gameplay popup catalog entry.
- `DemoStageControl` is a catalog-less runtime assist popup.
- `DemoStageControl` is a build-included tester/demo/showcase assist feature for tester assist clear, hard-section bypass, showcase navigation, and stage browsing.
- `DemoStageControl` is not a deletion candidate and is not a dev-only compile exclusion target.
- Future public-release hiding or disabling for `DemoStageControl` requires a separate product/build configuration decision.
- Reward popup is not current popup vocabulary. Stage reward/progression vocabulary remains stage-owned content/system vocabulary, not a UI popup route.
- Stage clear routes through `MinimalStageCompletionReadModel -> StageResult`.

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

- Push/Flip physical gameplay commands flow through the gameplay input route, not UI HUD command injection.
- `RequestPush`, `RequestFlip`, `BufferUiPush`, and `BufferUiFlip` are removed UI command-route vocabulary and are not current paths.
- Settings/rebind Push/Flip UI remains active for binding display, override, save, and restore through the shared `GameplayInputActionPaths` input contract.
- Settings/rebind setup fails fast when required action paths or Push/Flip keyboard bindings are missing; Flip remains keyboard-only and no gamepad binding is added.
- Settings tooltip on/off and large text on/off accessibility toggles are removed residue. `AccessibilitySettingsStore` is not a current runtime composition dependency.
- Scene transition UI uses only `SceneTransitionOverlayShell` plus `SceneTransitionOverlayContentCatalog`.
- `SceneTransitionOverlayView`, `UI/SceneTransitionOverlayView`, generated fallback, and legacy overlay fallback are not current paths.
- `UiNavigationInputRouter` is a resolver-only input router initialized through `IUiNavigationTargetResolver`.
- `UiNavigationInputRouter` must not regain `PopupController`, `PopupLayerView`, or `MainMenuScreenView` direct legacy overloads.

## Protected Boundaries

- Do not modify runtime code for this source regeneration.
- Do not modify prefabs or catalogs for this source regeneration.
- Do not change `DemoStageControl` runtime behavior.
- Do not simplify or reroute StageResult, Pause/Confirm/Tooltip popup, settings, audio, display, or UI bridge paths.
- Do not restore Settings tooltip on/off or large text on/off toggles without a separate product decision.
- Do not revive `ActionBar`, diagnostics runtime UI, or `SceneTransitionOverlayView`.
- Do not restore `Help` or `Inventory` as current gameplay screens.

## Deferred Policy Items

- UI audio user settings policy: UI SFX remains authored/routed through hidden `Ui`, but its effective user-facing mix follows `Master` and `Sfx` volume/mute plus hidden `Ui` state. Settings still exposes only `Main`, `Bgm`, and `Sfx`; there is no visible UI volume/mute row. `Voice` and `Ambience` do not follow `Sfx`.
- Inventory is not active authoritative UI. Reintroduction requires a separate `InventoryReadModel`, `InventoryCommandPort`, and permission model design.
- Diagnostics behavior is unchanged. Current work may strengthen production residue guards, but must not add a new diagnostics runtime feature.
- TMP, localization, accessibility, and layout modernization are separate contract work. Do not treat Text/TMP swaps as part of this UI architecture refactor.
