# UI Authoring and Navigation Guide

This document is the supporting implementation guide for prefab authoring and player interaction in the UI layer.

The canonical rules remain in [UI-Architecture-Guidelines.md](./UI-Architecture-Guidelines.md). If this guide conflicts with that document, the canonical UI architecture wins and this guide must be corrected.

## 1. Scope

Use this guide when adding or substantially redesigning a player-visible screen, popup, HUD slice, widget, or actionable control.

The goals are:

- make authored prefabs the default source of fixed visual structure;
- keep runtime composition focused on instantiation, validation, binding, dynamic population, and lifecycle;
- give every desktop player-facing action equivalent pointer and keyboard access;
- preserve explicit focus, modal, and navigation-domain behavior.

## 2. Prefab-First Decision Matrix

| UI shape | Default | Notes |
| --- | --- | --- |
| Fixed Screen, Popup, or HUD hierarchy | Authored prefab | Register canonical screens and popups in their catalog. |
| Fixed child widget or action button | Author inside the owning prefab | Create a reusable widget prefab only when independent reuse justifies it. |
| Variable-length collection | Authored item template plus runtime clone or pool | Runtime owns population and binding, not the item's fixed visual hierarchy. |
| Procedural non-interactive visual or VFX geometry | Runtime creation allowed | Disable raycast participation and define cleanup or pooling. |
| EventSystem, coordinator, service, or invisible mount root | Runtime creation allowed | It must not silently grow player-visible styling or actions. |
| Technical backdrop, pointer blocker, or drag-capture surface | Runtime creation allowed | It is not a navigation action; keyboard-equivalent modal behavior belongs to Cancel or Back policy. |
| Fixed player-visible interactive runtime hierarchy | Documented exception only | Record target, reason, owner, supported inputs, and removal or review condition. |

Existing runtime-generated UI is not automatically a preferred precedent. `DemoStageControl` is a build-included catalog-less runtime assist popup and should be tracked as an explicit migration debt or exception rather than copied as the default authoring model.

## 3. Canonical Runtime Composition

The normal production flow is:

```text
Authored Prefab
  -> Catalog or serialized composition reference
  -> Instantiate under the owning layer ContentRoot
  -> Validate root View and serialized references
  -> Bind Presenter and ViewModel
  -> Populate variable data
  -> Connect lifecycle and dispose safely
```

Screen and popup factories fail fast when canonical prefab authoring is missing or has the wrong root View. Do not recover by rebuilding the same fixed hierarchy from primitive `GameObject`, `Image`, `Button`, or TMP objects.

Prefer direct serialized references for roots, controls, labels, child views, navigation groups, profiles, and templates. Name lookup may be a documented safety fallback; it is not the primary production binding contract.

## 4. Actionable Controls

A player-facing actionable control represents an independent semantic action visible to the player. Examples include command buttons, toggles, dropdowns, sliders, Back, and Close.

The following are not actionable controls merely because they use `UnityEngine.UI.Button` or another `Selectable`:

- modal backdrops;
- pointer blockers or consume surfaces;
- drag-capture surfaces;
- inactive templates;
- implementation-only hotspots with no independent player action.

Under the current desktop production input profile, every visible and enabled actionable control must support:

- pointer activation;
- reachability from its owning navigation target;
- visible keyboard focus;
- canonical Submit activation;
- exactly one semantic action per accepted input.

These behaviors are the required contract. The component hierarchy in the next section is the default authoring recipe, not a requirement that every valid control use the same concrete components. A control may use `SelectionFrame`, a native selected transition, or another explicit focus treatment as long as the complete contract is preserved and tested.

Pointer and Submit paths converge on one semantic method or event such as `ClickBack`, `ClickConfirm`, or a bounded adapter action. Submit does not have to call `Button.onClick.Invoke()`.

Cancel or Escape is a shortcut and nested-state exit mechanism. It does not replace focus and Submit support for a visible Back or Close action.

## 5. Standard Button Recipe

The default text or icon action button uses:

```text
Button Root
  - Image or another authored target graphic
  - Button
  - UiHoverScaleEffect
  - Label and TypographyBinding, when the control has text
  - SelectionFrame with UiSelectionVisualProfile
```

Author the selection visual so it does not intercept pointer input. It must be hidden visually until navigation focus is revealed and cleared when focus is lost. This may be achieved by inactive authoring or lifecycle initialization; one serialized `activeSelf` value is not a universal contract.

`SelectionFrame` is the default focus treatment, not the only valid treatment. A selected image scale, color, outline, or native selected transition is acceptable when it is unambiguous, independently testable, and does not rely on pointer hover to communicate keyboard focus.

`UiHoverScaleEffect` is the default pointer hover, pressed, and submit-feedback behavior. Touch-only profiles may use pressed or touch feedback instead of hover. Icon-only actions do not require an empty label.

Do not use `UiCanvasElementFactory.CreateButton()` for a new production action. Its primitive result does not establish the project hover, focus, navigation, localization, or typography contracts.

## 6. Navigation Routing

`UiNavigationInputRouter` is the canonical Navigate, Submit, and Cancel input seam. It resolves targets in this order:

```text
Popup
  -> Modal Overlay
  -> Screen
  -> Optional HUD
```

A blocking upper layer without a navigation target must block lower-layer activation rather than allow accidental fallthrough.

The current focus reveal contract is:

- Navigate reveals focus and then dispatches the same movement.
- The first Submit while focus is hidden reveals focus without activating the selected action.
- A later Submit activates the selected action.
- Changing targets removes focus from the old target.
- Cancel does not reveal focus before applying nested-state or fallback behavior.

Unity `Selectable.navigation` and EventSystem selection are not the source of truth. The owning `IUiNavigationTarget` must be able to reach and submit the control through the project router.

## 7. Choosing a Navigation Model

### Simple button collections

Use `UiSelectableButtonGroup` for small linear collections. Its serialized slot order must match visible layout order. Configure whether the group wraps and whether hidden or non-interactable slots are skipped.

### Settings controls

Despite its name, `UiFocusGraphNavigator` is currently a Settings-oriented navigator rather than a general arbitrary UI graph. It supports the fixed Header, Audio, Display, and Input regions, including slider edit mode and dropdown list mode. It also looks up the three Settings tab IDs (`Header.AudioTab`, `Header.DisplayTab`, and `Header.InputTab`) directly, and Header Left/Right movement both changes focus and activates the newly focused tab.

Use it for the current Settings compound-input model. Before reusing it for a new compound screen, either remove those hard-coded region, ID, and activation assumptions or make and review an explicit extension whose domain behavior remains clear.

### Multi-domain screens

Main Menu Save Slots and Pause progression use screen-local domains and explicit transitions. A custom navigator is acceptable when it declares and tests:

- the default domain and focus;
- entry and exit for every domain;
- remembered or fallback focus;
- behavior when the selected control becomes invalid;
- every directional boundary as wrap, consume, cross-domain transition, or not-handled.

### Single-action screens

A screen with one action does not need artificial direction movement. The action must become the default focus and accept Submit.

## 8. Cancel and Nested State

Cancel follows the active UI state from the inside out. Examples include:

```text
Dropdown list or slider edit
  -> Inner preview or local overlay
  -> Popup
  -> Screen Back policy
  -> Coordinator fallback
```

Do not duplicate this policy inside individual Button handlers. A technical backdrop may dismiss or consume pointer input according to popup policy without becoming a keyboard focus node.

## 9. Localization and Typography

For every authored text target, decide explicitly whether it participates in localization and which semantic typography role it uses.

- Prefer authored `TypographyBinding` metadata.
- Preserve authored sizing unless the approved typography contract says otherwise.
- Runtime typography application is acceptable when it is the established canonical path for that control.
- Dynamic item templates carry their typography metadata into clones.
- Do not create player-facing TMP labels at runtime merely to avoid prefab authoring.

## 10. Validation Checklist

For a new or materially changed production UI:

- [ ] Existing screen, popup, HUD, or reusable widget prefabs were reviewed before adding runtime construction.
- [ ] Fixed player-visible hierarchy is authored in the owning prefab or covered by a documented exception.
- [ ] Canonical Screen or Popup catalog registration is complete where applicable.
- [ ] The instantiated root has the expected View and direct layer ownership.
- [ ] Required serialized references fail fast when missing.
- [ ] Dynamic collections use an authored template when their item count varies.
- [ ] Every active actionable control is reachable and has a defined exit or boundary behavior.
- [ ] Hidden and non-interactable controls cannot retain actionable focus.
- [ ] Pointer and Submit converge on the same semantic action exactly once.
- [ ] Keyboard focus is visible and clears on target or focus loss.
- [ ] Back and Close actions are not keyboard-reachable only through Cancel or Escape.
- [ ] Technical backdrops and blockers stay outside action navigation.
- [ ] Localization and typography participation is explicit.
- [ ] Prefab changes include purpose and manual Editor validation evidence when applicable.
- [ ] `./run_tests.sh ui` passes, or the report states why it was not run.

## 11. Test Strategy

Prefer behavior and stable authored contracts over serialized implementation trivia.

Required focused evidence includes:

- pointer action behavior;
- navigation reachability and exit;
- Submit semantic parity and exactly-once execution;
- hidden or disabled control exclusion;
- focus indicator visibility and cleanup;
- modal target priority and lower-layer blocking;
- nested Cancel precedence;
- catalog completeness, root View identity, and required serialized references.

Avoid repository-wide assertions that every Unity `Button` has the same child count or exact hierarchy. Do not freeze Unity YAML text, incidental sibling counts, hover tuning values, or one universal SelectionFrame `activeSelf` value.

Repository guards may prohibit new production calls to known primitive interactive helpers such as `UiCanvasElementFactory.CreateButton()`. Runtime infrastructure and documented exceptions should be classified explicitly instead of blocked by a blanket `AddComponent<Button>` ban.

## 12. Current Exceptions, Resolved Findings, and Migration Debt

Classify these paths by the functional contract in Section 4, not by the presence or absence of one component such as `SelectionFrame`.

| Path | Current behavior | Classification and follow-up |
| --- | --- | --- |
| Settings `LanguageCycleButton` | Pointer, hover, `SelectionFrame`, graph reachability, and Submit parity are present. The earlier missing `SettingsFocusGraphBinding` node was fixed by registering `Display.Language.Button`. | Resolved finding, not remaining debt. Keep the pointer/Submit exactly-once and unavailable-control skip tests. |
| `DemoStageControlPanelView` | The fixed panel and all six Buttons are created at runtime. `HandleNavigate()` and `HandleSubmit()` always return `false`; only pointer actions and Cancel-to-close are available. | Highest-priority prefab-first and keyboard-accessibility debt. Migrate the fixed hierarchy to an authored prefab and provide an explicit navigation target for all six actions, or approve and document a bounded exception with equivalent keyboard behavior. |
| Pause preview Close button | The preview overlay owns a single-slot Close navigation group with an authored `SelectionFrame`. Every open resets its local reveal cycle, the first subsequent Submit or Navigate reveals Close, pointer click and the following focused Submit converge on the same close request, and Cancel remains the nested-state shortcut. Actual pointer hover is not suppressed. | Resolved finding. Keep hidden-open, reveal-only first input, exactly-once Close, submit feedback, focus cleanup, and progression-focus restoration tests. |
| Save Slot recovery Retry/Reset | Directional navigation and Submit select and invoke the actions. Focus is shown through the Buttons' native selection transition instead of `SelectionFrame`. | Contract-compatible non-standard focus treatment, not debt solely because `SelectionFrame` is absent. Retain tests that prove visible focus, hidden/unavailable action skipping, and exactly-once Submit behavior. |
| Pause progression marker | Click and keyboard Submit open the preview. Selected-image sizing is the focus/selection treatment; there is no standard `SelectionFrame`. | Contract-compatible specialized control. Preserve unambiguous size-based selection plus pointer/keyboard parity tests rather than forcing the standard button hierarchy. |
| Remaining runtime-generated Main Menu popup, Settings overlay, and Comic fixed shells | Fixed runtime hierarchy remains in several production paths. | Review each path as infrastructure, documented exception, or prefab migration candidate; do not copy it as precedent without that classification. |

The remaining actionable debt is therefore the Demo Stage Control migration and the unclassified fixed runtime shells. The resolved Settings language and Pause preview Close paths, plus the two explicit alternative focus treatments, must not be reported as missing keyboard support merely because their navigation ownership or visuals differ from the standard recipe.
