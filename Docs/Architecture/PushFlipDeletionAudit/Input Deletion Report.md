# Input Deletion Report

## Push Physical Input Status

`Push` physical input is alive.

Evidence:

- `Assets/InputSystem_Actions.inputactions` has action `Player/Push`.
- Bindings found: `<Keyboard>/e` and `<Gamepad>/buttonNorth`.
- `Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayInputHost.cs` resolves `Player/Push`, subscribes `_pushAction.started`, buffers Push, and emits `PlayerTickCommand.PushPressed`.
- `Assets/_Shared/Input/Runtime/KeyboardBindingSettingsService.cs` uses `PushActionPath = "Player/Push"` for display/rebinding.
- PlayMode tests press the keyboard Push binding and verify edge-triggered Push behavior.

Classification: `REMOVED_BY_PRODUCT_DECISION`.

Delete action: remove the UI Push/Flip action request methods, input buffers, fakes, and route-only tests.

## Flip Physical Input Status

`Flip` physical input is alive for keyboard.

Evidence:

- `Assets/InputSystem_Actions.inputactions` has action `Player/Flip`.
- Binding found: `<Keyboard>/q`.
- The inspected action entry keeps Flip keyboard-only by current product policy.
- `GameplayInputHost` resolves `Player/Flip`, subscribes `started` and `performed`, buffers Flip, and emits `PlayerTickCommand.FlipPressed`.
- `KeyboardBindingSettingsService` uses `FlipActionPath = "Player/Flip"`.
- PlayMode tests press the keyboard Flip binding and verify tick-boundary buffering/repeat-lock behavior.

Classification: `KEEP_CURRENTLY_USED` for keyboard Flip; `DOCUMENTED_CURRENT_POLICY` for no controller binding.

Delete action: none.

## InputActionAsset Status

`Assets/InputSystem_Actions.inputactions` is production-bound.

Evidence:

- Asset GUID: `052faaac586de48259a63d0c4782560b`.
- GUID references were found in project settings and scenes, including `ProjectSettings/ProjectSettings.asset`, `ProjectSettings/EditorBuildSettings.asset`, `Assets/Scenes/UIAudioScene.unity`, and `Assets/Scenes/MainMenuScene.unity`.
- `GameplayInputHost` receives and enables the action asset at runtime.

Classification: `KEEP_CURRENTLY_USED`.

Delete action: none.

## Key Binding Status

| Binding | Status | Classification | Delete action |
| --- | --- | --- | --- |
| Push `<Keyboard>/e` | Current runtime binding | KEEP_CURRENTLY_USED | None |
| Push `<Gamepad>/buttonNorth` | Current runtime binding | KEEP_CURRENTLY_USED | None |
| Flip `<Keyboard>/q` | Current runtime binding | KEEP_CURRENTLY_USED | None |
| Flip controller binding | Not observed | DOCUMENTED_CURRENT_POLICY | Do not add in this cleanup PR |
| Old Push key binding | No separate old binding found | No artifact | None |
| Old Flip key binding | No separate old binding found | No artifact | None |

## Generated Wrapper Status

No generated input wrapper is active.

Evidence:

- `Assets/InputSystem_Actions.inputactions.meta` has wrapper generation disabled (`generateWrapperCode: 0`) with empty wrapper class/path/namespace fields.
- Search did not find a generated `IInputActionCollection` wrapper exposing Push/Flip.

Classification: no artifact.

Delete action: none.

## GameplayInputHost Route Status

`GameplayInputHost` Push/Flip route is canonical and active.

Runtime behavior:

- `BindActions()` finds `Player/Move`, `Player/Flip`, and `Player/Push`.
- Missing actions throw, so Push/Flip are hard dependencies of the current host.
- Push buffers on `started`.
- Flip buffers on `started` and `performed`.
- `BuildPlayerCommand()` maps buffered Push/Flip to `PlayerTickCommand.Create(... pushPressed: true)` or `flipPressed: true`.
- Push takes priority over Flip when both are buffered.
- UI buffered action direction can override sampled move direction.
- Fake attempt / respawn states clear buffers and block command emission.

Classification: `KEEP_CURRENTLY_USED`.

Delete action: none.

## CommandGateway Route Status

Historical state: the gameplay host command gateway exposed UI Push/Flip action requests.

Evidence:

- Current cleanup removes those UI action request methods.
- UI-held movement remains in `IGameplayCommandGateway`.
- Runtime tests migrate to physical input or direct command coverage.
- UI architecture tests keep HUD display separated from command ownership.

Classification: `KEEP_CURRENTLY_USED`.

Delete action: none.

## Help/Prompt Status

No active HelpScreen Push/Flip prompt artifact was found.

Active prompt-like surface:

- Settings/rebind UI displays and changes Push/Flip bindings through `KeyboardBindingSettingsService`.
- `SettingsScreen.prefab` contains current `PushInputRow`, `FlipInputRow`, `PushKeyDisplay`, and `FlipKeyDisplay`.

Residue:

- `SettingsScreen.prefab` previously contained inactive duplicate Push/Flip change-button object names.
- Serialized reference checks found no active external/object-name lookup dependency, so the duplicate objects were deleted. Current `PushInputRow`, `FlipInputRow`, `PushKeyDisplay`, and `FlipKeyDisplay` remain.

Classification:

- Settings/rebind Push/Flip: `KEEP_CURRENTLY_USED`.
- Settings legacy Push/Flip duplicate buttons: `REMOVED_DUPLICATE`, P2 complete.

## UI/HUD Command Ownership Status

UI does not own simulation authority.

Evidence:

- UI action requests flow through `IGameplayCommandGateway` to `GameplayInputHost`.
- HUD display query path is `GameplayHostPlayerHudQuery` and `UIPresentationSnapshot` data.
- Current docs say ActionBar was retired; current runtime symbols point to HUDRoot/PlayerStatus query paths, not an active `ActionBarPresenter`.
- `PlayerStatusPresenter` is display-side and does not directly mutate gameplay state.

Classification:

- UI command gateway: `KEEP_CURRENTLY_USED`.
- ActionBar Push/Flip wording: `RETIRED_VOCABULARY` docs cleanup, not code deletion.

## Input Delete Candidates

| Priority | Candidate | Classification | Required action |
| --- | --- | --- | --- |
| P2 | `SettingsScreen.prefab` legacy Push change button | REMOVED_DUPLICATE | Deleted inactive duplicate object; current row retained. |
| P2 | `SettingsScreen.prefab` legacy Flip change button | REMOVED_DUPLICATE | Deleted inactive duplicate object; current row retained. |
| P2 | ActionBar Push/Flip vocabulary in docs | RETIRED_VOCABULARY | Consolidate docs around actual HUDRoot/PlayerStatus query path. |
| P3 | Flip keyboard-only binding policy | DOCUMENTED_CURRENT_POLICY | Product input decision; not a deletion candidate. |

Non-candidates:

- Push InputAction.
- Flip InputAction.
- Push/Flip key bindings.
- `GameplayInputHost` routes.
- `GameplayHostCommandGateway` routes.
- `PlayerTickCommand.PushPressed` / `FlipPressed`.
- Generated input wrapper, old action map, old key binding, and HelpScreen prompt were not present as active artifacts.
