using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Shared.Input
{
    public sealed class KeyboardBindingSettingsService : IDisposable
    {
        private const string KeyboardGroup = "Keyboard&Mouse";
        private const string EmptyOverridePath = "";

        private static readonly string[] WasdPaths =
        {
            "<Keyboard>/w",
            "<Keyboard>/a",
            "<Keyboard>/s",
            "<Keyboard>/d",
        };

        private static readonly string[] ArrowPaths =
        {
            "<Keyboard>/upArrow",
            "<Keyboard>/downArrow",
            "<Keyboard>/leftArrow",
            "<Keyboard>/rightArrow",
        };

        private static readonly string[] ReservedPaths =
        {
            "<Keyboard>/escape",
            "<Keyboard>/f3",
            "<Keyboard>/f4",
        };

        private readonly InputActionAsset _actions;
        private readonly InputAction _moveAction;
        private readonly InputAction _uiNavigateAction;
        private readonly InputAction _pushAction;
        private readonly InputAction _flipAction;
        private readonly InputActionMap _playerMap;
        private readonly InputActionMap _uiMap;
        private readonly IKeyboardBindingStore _store;
        private InputActionRebindingExtensions.RebindingOperation _rebindOperation;
        private bool _mapWasEnabledBeforeRebind;
        private string _pendingRebindPath;
        private KeyboardBindableAction? _rebindingAction;
        private KeyboardMovementScheme _movementScheme = KeyboardMovementScheme.Wasd;

        public static event Action<KeyboardBindingSettingsSnapshot> BindingsChanged;

        public static string MoveActionPath => GameplayInputActionPaths.PlayerMove;

        public static string NavigateActionPath => GameplayInputActionPaths.UiNavigate;

        public static string PushActionPath => GameplayInputActionPaths.PlayerPush;

        public static string FlipActionPath => GameplayInputActionPaths.PlayerFlip;

        public KeyboardBindingSettingsService(InputActionAsset actions, IKeyboardBindingStore store = null)
        {
            _actions = actions != null ? actions : throw new ArgumentNullException(nameof(actions));
            _moveAction = RequireAction(_actions, GameplayInputActionPaths.PlayerMove);
            _uiNavigateAction = RequireAction(_actions, GameplayInputActionPaths.UiNavigate);
            _pushAction = RequireAction(_actions, GameplayInputActionPaths.PlayerPush);
            _flipAction = RequireAction(_actions, GameplayInputActionPaths.PlayerFlip);
            _playerMap = _moveAction.actionMap;
            _uiMap = _uiNavigateAction.actionMap;
            RequireKeyboardBinding(_pushAction, GameplayInputActionPaths.PlayerPush);
            RequireKeyboardBinding(_flipAction, GameplayInputActionPaths.PlayerFlip);
            _store = store ?? new PlayerPrefsKeyboardBindingStore();
            LoadAndApplySavedSettings();
        }

        public bool IsRebinding => _rebindOperation != null;

        public static void ApplySavedSettings(InputActionAsset actions, IKeyboardBindingStore store = null)
        {
            if (actions == null)
            {
                return;
            }

            using var service = new KeyboardBindingSettingsService(actions, store);
        }

        public KeyboardBindingSettingsSnapshot Read()
        {
            return new KeyboardBindingSettingsSnapshot(
                _movementScheme,
                _movementScheme == KeyboardMovementScheme.ArrowKeys ? "Arrow Keys" : "WASD",
                ResolveDisplayName(KeyboardBindableAction.Push),
                ResolveDisplayName(KeyboardBindableAction.Flip),
                IsRebinding,
                _rebindingAction);
        }

        public KeyboardBindingValidationStatus SetMovementScheme(KeyboardMovementScheme scheme)
        {
            var validation = ValidateMovementSchemeChange(scheme);
            if (validation != KeyboardBindingValidationStatus.Success)
            {
                return validation;
            }

            WithManagedMapsDisabled(() =>
            {
                ApplyMovementScheme(scheme);
            });

            _movementScheme = scheme;
            _store.SaveMovementScheme(scheme);
            _store.Save();
            BindingsChanged?.Invoke(Read());
            return KeyboardBindingValidationStatus.Success;
        }

        public KeyboardRebindStartResult StartRebind(
            KeyboardBindableAction action,
            Action<KeyboardRebindResult> completed)
        {
            if (IsRebinding)
            {
                return new KeyboardRebindStartResult(false, KeyboardBindingValidationStatus.AlreadyRebinding, Read());
            }

            var inputAction = ResolveBindableAction(action);
            var bindingIndex = FindKeyboardBindingIndex(inputAction);
            if (inputAction == null || bindingIndex < 0)
            {
                return new KeyboardRebindStartResult(false, KeyboardBindingValidationStatus.MissingBinding, Read());
            }

            _rebindingAction = action;
            _pendingRebindPath = null;
            var actionMap = inputAction.actionMap;
            _mapWasEnabledBeforeRebind = actionMap != null && actionMap.enabled;
            if (_mapWasEnabledBeforeRebind)
            {
                actionMap.Disable();
            }

            _rebindOperation = inputAction.PerformInteractiveRebinding(bindingIndex)
                .WithTargetBinding(bindingIndex)
                .WithExpectedControlType("Button")
                .WithControlsExcluding("<Mouse>")
                .WithCancelingThrough("<Keyboard>/escape")
                .WithActionEventNotificationsBeingSuppressed()
                .OnApplyBinding((_, path) => _pendingRebindPath = path)
                .OnCancel(_ =>
                {
                    FinishRebind(action, KeyboardBindingValidationStatus.Canceled, completed);
                })
                .OnComplete(_ =>
                {
                    var status = CompleteRebind(action, _pendingRebindPath);
                    FinishRebind(action, status, completed);
                });

            _rebindOperation.Start();
            return new KeyboardRebindStartResult(true, KeyboardBindingValidationStatus.Success, Read());
        }

        public void CancelRebind()
        {
            _rebindOperation?.Cancel();
        }

        public KeyboardBindingSettingsSnapshot ResetToDefaults()
        {
            CancelRebind();
            WithManagedMapsDisabled(() =>
            {
                ClearManagedOverrides();
                ApplyMovementScheme(KeyboardMovementScheme.Wasd);
            });

            _movementScheme = KeyboardMovementScheme.Wasd;
            _store.SaveMovementScheme(_movementScheme);
            _store.ClearBindingOverridesJson();
            _store.Save();
            var snapshot = Read();
            BindingsChanged?.Invoke(snapshot);
            return snapshot;
        }

        public void LoadAndApplySavedSettings()
        {
            var scheme = _store.TryLoadMovementScheme(out var storedScheme)
                ? storedScheme
                : KeyboardMovementScheme.Wasd;

            WithManagedMapsDisabled(() =>
            {
                ClearManagedOverrides();
                if (_store.TryLoadBindingOverridesJson(out var json))
                {
                    try
                    {
                        _actions.LoadBindingOverridesFromJson(json, removeExisting: false);
                    }
                    catch
                    {
                        ClearBindableActionOverrides();
                        _store.ClearBindingOverridesJson();
                        _store.Save();
                    }
                }

                _movementScheme = scheme;
                if (ValidateMovementSchemeChange(scheme) != KeyboardBindingValidationStatus.Success)
                {
                    ClearBindableActionOverrides();
                    _store.ClearBindingOverridesJson();
                    _store.Save();
                }

                ApplyMovementScheme(scheme);
            });
        }

        public void Dispose()
        {
            CancelRebind();
        }

        private KeyboardBindingValidationStatus CompleteRebind(KeyboardBindableAction action, string selectedPath)
        {
            if (string.IsNullOrWhiteSpace(selectedPath) || !IsKeyboardPath(selectedPath))
            {
                return KeyboardBindingValidationStatus.InvalidKey;
            }

            var validation = ValidateActionBinding(action, selectedPath);
            if (validation != KeyboardBindingValidationStatus.Success)
            {
                return validation;
            }

            var inputAction = ResolveBindableAction(action);
            var bindingIndex = FindKeyboardBindingIndex(inputAction);
            if (inputAction == null || bindingIndex < 0)
            {
                return KeyboardBindingValidationStatus.MissingBinding;
            }

            inputAction.ApplyBindingOverride(bindingIndex, selectedPath);
            _store.SaveBindingOverridesJson(BuildPushFlipOverridesJson());
            _store.Save();
            return KeyboardBindingValidationStatus.Success;
        }

        private void FinishRebind(
            KeyboardBindableAction action,
            KeyboardBindingValidationStatus status,
            Action<KeyboardRebindResult> completed)
        {
            var operation = _rebindOperation;
            _rebindOperation = null;
            _pendingRebindPath = null;
            _rebindingAction = null;

            operation?.Dispose();

            var actionMap = ResolvePlayerMap();
            if (_mapWasEnabledBeforeRebind && actionMap != null)
            {
                actionMap.Enable();
            }

            _mapWasEnabledBeforeRebind = false;
            var snapshot = Read();
            if (status == KeyboardBindingValidationStatus.Success)
            {
                BindingsChanged?.Invoke(snapshot);
            }

            var conflictingAction = status == KeyboardBindingValidationStatus.DuplicateAction
                ? (KeyboardBindableAction?)(action == KeyboardBindableAction.Push
                    ? KeyboardBindableAction.Flip
                    : KeyboardBindableAction.Push)
                : null;
            completed?.Invoke(new KeyboardRebindResult(action, status, snapshot, conflictingAction));
        }

        private KeyboardBindingValidationStatus ValidateMovementSchemeChange(KeyboardMovementScheme scheme)
        {
            var movementKeys = scheme == KeyboardMovementScheme.ArrowKeys ? ArrowPaths : WasdPaths;
            var pushPath = ResolveEffectivePath(KeyboardBindableAction.Push);
            var flipPath = ResolveEffectivePath(KeyboardBindableAction.Flip);

            foreach (var movementKey in movementKeys)
            {
                if (PathsEqual(movementKey, pushPath) || PathsEqual(movementKey, flipPath))
                {
                    return KeyboardBindingValidationStatus.MovementConflict;
                }
            }

            return KeyboardBindingValidationStatus.Success;
        }

        private KeyboardBindingValidationStatus ValidateActionBinding(KeyboardBindableAction action, string selectedPath)
        {
            if (IsReserved(selectedPath))
            {
                return KeyboardBindingValidationStatus.ReservedKey;
            }

            var otherAction = action == KeyboardBindableAction.Push
                ? KeyboardBindableAction.Flip
                : KeyboardBindableAction.Push;
            if (PathsEqual(selectedPath, ResolveEffectivePath(otherAction)))
            {
                return KeyboardBindingValidationStatus.DuplicateAction;
            }

            var activeMovementPaths = _movementScheme == KeyboardMovementScheme.ArrowKeys ? ArrowPaths : WasdPaths;
            foreach (var movementPath in activeMovementPaths)
            {
                if (PathsEqual(selectedPath, movementPath))
                {
                    return KeyboardBindingValidationStatus.MovementConflict;
                }
            }

            return KeyboardBindingValidationStatus.Success;
        }

        private void ApplyMovementScheme(KeyboardMovementScheme scheme)
        {
            ApplyMovementSchemeToAction(_moveAction, scheme);
            ApplyMovementSchemeToAction(_uiNavigateAction, scheme);
        }

        private static void ApplyMovementSchemeToAction(InputAction action, KeyboardMovementScheme scheme)
        {
            if (action == null)
            {
                return;
            }

            for (var i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (!binding.isPartOfComposite || !IsKeyboardPath(binding.path))
                {
                    continue;
                }

                if (IsWasdPath(binding.path))
                {
                    ApplyMovementPartEnabled(action, i, scheme == KeyboardMovementScheme.Wasd);
                }
                else if (IsArrowPath(binding.path))
                {
                    ApplyMovementPartEnabled(action, i, scheme == KeyboardMovementScheme.ArrowKeys);
                }
            }
        }

        private static void ApplyMovementPartEnabled(InputAction moveAction, int bindingIndex, bool isEnabled)
        {
            if (isEnabled)
            {
                moveAction.RemoveBindingOverride(bindingIndex);
                return;
            }

            moveAction.ApplyBindingOverride(bindingIndex, EmptyOverridePath);
        }

        private void ClearManagedOverrides()
        {
            ClearMovementOverrides();
            ClearBindableActionOverrides();
        }

        private void ClearMovementOverrides()
        {
            ClearMovementOverrides(_moveAction);
            ClearMovementOverrides(_uiNavigateAction);
        }

        private static void ClearMovementOverrides(InputAction action)
        {
            if (action == null)
            {
                return;
            }

            for (var i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (binding.isPartOfComposite && IsKeyboardPath(binding.path))
                {
                    action.RemoveBindingOverride(i);
                }
            }
        }

        private void ClearBindableActionOverrides()
        {
            ClearBindableActionOverride(KeyboardBindableAction.Push);
            ClearBindableActionOverride(KeyboardBindableAction.Flip);
        }

        private void ClearBindableActionOverride(KeyboardBindableAction action)
        {
            var inputAction = ResolveBindableAction(action);
            var bindingIndex = FindKeyboardBindingIndex(inputAction);
            if (inputAction != null && bindingIndex >= 0)
            {
                inputAction.RemoveBindingOverride(bindingIndex);
            }
        }

        private string BuildPushFlipOverridesJson()
        {
            var overrides = new BindingOverrideListJson();
            AddBindingOverrideJson(KeyboardBindableAction.Push, overrides.bindings);
            AddBindingOverrideJson(KeyboardBindableAction.Flip, overrides.bindings);
            return overrides.bindings.Count == 0 ? string.Empty : JsonUtility.ToJson(overrides);
        }

        private void AddBindingOverrideJson(KeyboardBindableAction action, List<BindingOverrideJson> overrides)
        {
            var inputAction = ResolveBindableAction(action);
            var bindingIndex = FindKeyboardBindingIndex(inputAction);
            if (inputAction == null || bindingIndex < 0)
            {
                return;
            }

            var binding = inputAction.bindings[bindingIndex];
            if (!binding.hasOverrides)
            {
                return;
            }

            overrides.Add(new BindingOverrideJson
            {
                action = $"{inputAction.actionMap.name}/{inputAction.name}",
                id = binding.id.ToString(),
                path = binding.overridePath ?? "null",
                interactions = binding.overrideInteractions ?? "null",
                processors = binding.overrideProcessors ?? "null",
            });
        }

        private string ResolveDisplayName(KeyboardBindableAction action)
        {
            var inputAction = ResolveBindableAction(action);
            var bindingIndex = FindKeyboardBindingIndex(inputAction);
            if (inputAction == null || bindingIndex < 0)
            {
                return string.Empty;
            }

            var path = inputAction.bindings[bindingIndex].effectivePath;
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return InputControlPath.ToHumanReadableString(
                path,
                InputControlPath.HumanReadableStringOptions.OmitDevice);
        }

        private string ResolveEffectivePath(KeyboardBindableAction action)
        {
            var inputAction = ResolveBindableAction(action);
            var bindingIndex = FindKeyboardBindingIndex(inputAction);
            if (inputAction == null || bindingIndex < 0)
            {
                return string.Empty;
            }

            return inputAction.bindings[bindingIndex].effectivePath;
        }

        private InputAction ResolveBindableAction(KeyboardBindableAction action)
        {
            return action == KeyboardBindableAction.Push ? _pushAction : _flipAction;
        }

        private InputActionMap ResolvePlayerMap()
        {
            return _playerMap;
        }

        private static int FindKeyboardBindingIndex(InputAction action)
        {
            if (action == null)
            {
                return -1;
            }

            for (var i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (binding.isComposite || binding.isPartOfComposite)
                {
                    continue;
                }

                if (IsKeyboardPath(binding.path) ||
                    IsKeyboardPath(binding.effectivePath) ||
                    ContainsKeyboardGroup(binding.groups))
                {
                    return i;
                }
            }

            return -1;
        }

        private void WithManagedMapsDisabled(Action action)
        {
            var playerMap = ResolvePlayerMap();
            var uiMap = _uiMap;
            var wasPlayerEnabled = playerMap != null && playerMap.enabled;
            var wasUiEnabled = uiMap != null && uiMap.enabled;
            if (wasPlayerEnabled)
            {
                playerMap.Disable();
            }

            if (wasUiEnabled)
            {
                uiMap.Disable();
            }

            try
            {
                action();
            }
            finally
            {
                if (wasPlayerEnabled && playerMap != null)
                {
                    playerMap.Enable();
                }

                if (wasUiEnabled && uiMap != null)
                {
                    uiMap.Enable();
                }
            }
        }

        private static bool ContainsKeyboardGroup(string groups)
        {
            return !string.IsNullOrWhiteSpace(groups) &&
                   groups.IndexOf(KeyboardGroup, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static InputAction RequireAction(InputActionAsset asset, string actionPath)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            var action = asset.FindAction(actionPath, throwIfNotFound: false);
            if (action == null)
            {
                throw new InvalidOperationException(
                    $"Required input action '{actionPath}' was not found. " +
                    "Settings/rebind cannot be initialized with a mismatched InputActionAsset.");
            }

            return action;
        }

        private static void RequireKeyboardBinding(InputAction action, string actionPath)
        {
            if (action != null)
            {
                for (var i = 0; i < action.bindings.Count; i++)
                {
                    var binding = action.bindings[i];
                    if (binding.isComposite || binding.isPartOfComposite)
                    {
                        continue;
                    }

                    if (IsKeyboardPath(binding.effectivePath) || IsKeyboardPath(binding.path))
                    {
                        return;
                    }
                }
            }

            throw new InvalidOperationException(
                $"Required keyboard binding for '{actionPath}' was not found. " +
                "Current Settings/rebind policy is keyboard-only for Push/Flip.");
        }

        private static bool IsKeyboardPath(string path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   NormalizePath(path).StartsWith("<keyboard>/", StringComparison.Ordinal);
        }

        private static bool IsWasdPath(string path)
        {
            foreach (var wasdPath in WasdPaths)
            {
                if (PathsEqual(path, wasdPath))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsArrowPath(string path)
        {
            foreach (var arrowPath in ArrowPaths)
            {
                if (PathsEqual(path, arrowPath))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsReserved(string path)
        {
            foreach (var reservedPath in ReservedPaths)
            {
                if (PathsEqual(path, reservedPath))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PathsEqual(string left, string right)
        {
            return string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.Ordinal);
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Trim().ToLowerInvariant();
        }

        [Serializable]
        private sealed class BindingOverrideListJson
        {
            public List<BindingOverrideJson> bindings = new List<BindingOverrideJson>();
        }

        [Serializable]
        private struct BindingOverrideJson
        {
            public string action;
            public string id;
            public string path;
            public string interactions;
            public string processors;
        }
    }
}
