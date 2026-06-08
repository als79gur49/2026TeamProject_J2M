# Input Residual Report

## Physical Input Recheck

`Assets/InputSystem_Actions.inputactions` currently defines:

- `Player/Push`: `<Keyboard>/e`, `<Gamepad>/buttonNorth`
- `Player/Flip`: `<Keyboard>/q`

`GameplayInputHost` treats all Push/Flip physical input as one-tick buffered action requests. It requires both `Player/Push` and `Player/Flip` actions at bind time through the shared `GameplayInputActionPaths` contract.

Verdict:

- `Player/Push`: `KEEP_CURRENTLY_USED`
- `Player/Flip`: `KEEP_CURRENTLY_USED`
- Current product input policy: Push/Flip gameplay actions are keyboard-driven for settings/rebind; the existing Push controller binding remains authored, and Flip stays keyboard-only.

## InputActionAsset Recheck

No generated wrapper code or obsolete generated InputAction collection was found. `GameplayInputHost` resolves required gameplay actions through `GameplayInputActionPaths`:

- `Player/Move`
- `Player/Flip`
- `Player/Push`

The settings service uses the same shared path owner for:

- `Player/Move`
- `UI/Navigate`
- `Player/Push`
- `Player/Flip`

The previous production `HARDCODED_PATH_RISK` is closed for these paths: `GameplayInputHost` and `KeyboardBindingSettingsService` no longer own separate hardcoded copies. Missing required settings actions fail fast during settings binding service setup instead of degrading into an empty binding display.

## Binding Policy Recheck

Push and Flip are asymmetric:

- Push supports keyboard and gamepad.
- Flip is keyboard-only.

Conclusion:

- This is the current product decision, not an unresolved input gap.
- Do not add a controller binding for Flip in this cleanup PR.
- Settings/rebind remains scoped to keyboard bindings.

## GameplayInputHost Route Recheck

Active physical route:

```text
InputAction Player/Push started -> BufferPush -> BuildPlayerCommand pushPressed
InputAction Player/Flip started/performed -> BufferFlip -> BuildPlayerCommand flipPressed
```

The Flip `started` + `performed` subscription has duplicate semantic effect because both callbacks only set `_hasBufferedFlip = true`. It may be deliberate Input System compatibility, but no local comment explains the asymmetry with Push.

Classification: physical Push/Flip route is retained. The previous UI Push/Flip gameplay action command injection route is removed by current keyboard-only product policy.

## CommandGateway UI Surface Recheck

The gameplay UI command gateway previously exposed Push/Flip gameplay action command injection requests, but production UI callers were not found under `Assets/_Features/UI`.

Observed callers:

- Tests and fakes were the active callers.
- `GameplayUiFlowPorts` stores `IGameplayCommandGateway`.
- `HUDRootPresenter` currently does not call the gateway.
- No touch/mobile/assist action button was found.
- Settings/rebind Push/Flip UI is not this route; settings rows and keyboard rebind behavior remain active.

Classification: `REMOVED_BY_PRODUCT_DECISION`.

Risk: interface deletion affects UI architecture tests and gameplay-host tests; those tests migrate to physical input, direct command coverage, or HUD ownership guards.

## Settings/Rebind Recheck

Settings input rows are active:

- `SettingsScreen.prefab`: `PushInputRow`, `FlipInputRow`, `PushKeyDisplay`, `FlipKeyDisplay`
- `SettingsInputView`: binds Push/Flip change buttons and display text.
- `SettingsInputPresenter`: starts Push/Flip rebind flows.
- `KeyboardBindingSettingsService`: applies overrides, validates conflicts, serializes Push/Flip override JSON, and restores through `ApplySavedSettings`.
- `GameplayHostRuntimeFactory`: applies saved settings to runtime actions.

Verdict: `KEEP_CURRENTLY_USED`.

Limitations:

- Keyboard-only rebind.
- Missing required actions are setup defects and fail fast during `KeyboardBindingSettingsService` initialization.
- Push/Flip settings rows require keyboard bindings; Flip remains keyboard-only and no gamepad binding is required or added.

## Prompt/Help Recheck

No active `HelpScreen` Push/Flip prompt artifact was found.

`ActionBar` is gone as runtime UI. Current mentions are retired vocabulary in docs.

Verdict:

- HelpScreen Push/Flip prompt: no deletion target remains.
- ActionBar Push/Flip vocabulary: docs-only cleanup if wording is not explicitly documenting retired status.

## Tests-Only Input Paths

Direct command tests are common and mostly current governance:

- `PlayerTickCommand.Push(...)`: 69 calls in 11 gameplay test files.
- `PlayerTickCommand.Flip(...)`: 63 calls in 13 gameplay test files.
- `PlayerTickCommand.Create(... pushPressed: true)`: 2 calls.
- `PlayerTickCommand.Create(... flipPressed: true)`: 0 calls.

Classification:

- Broad Push/Flip command tests: `KEEP_TEST_GOVERNANCE`
- Direct no-direction `Create(... pushPressed: true)` tests: `REFACTOR_DUPLICATE_TEST` candidate after physical/playmode coverage review

## Input Residue Delete Candidates

| Candidate | Classification | Action |
| --- | --- | --- |
| Docs-only Push contact threshold wording | `DELETE_NOW_UNUSED` | Trim or mark historical |
| Old HelpScreen prompt mentions | `DELETE_NOW_UNUSED` | Remove stale docs if any new occurrence appears |
| ActionBar wording not marked retired | `DELETE_NOW_UNUSED` | Remove or rewrite as retired vocabulary |
| UI Push/Flip gameplay action command injection route | `REMOVED_BY_PRODUCT_DECISION` | Product has no touch/mobile/assist action surface; settings/rebind UI remains |
| Flip controller binding absence | `DOCUMENTED_CURRENT_POLICY` | Keep Flip keyboard-only |
| Hardcoded settings paths | `REFACTORED_CURRENT_CONTRACT` | Centralized through `GameplayInputActionPaths`; settings setup now fails fast for missing required actions |
