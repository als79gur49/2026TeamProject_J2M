using System;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Application
{
    public readonly struct KeyboardBindingSettingsSnapshot
    {
        public KeyboardBindingSettingsSnapshot(
            KeyboardMovementScheme movementScheme,
            string movementDisplayName,
            string pushDisplayName,
            string flipDisplayName,
            bool isRebinding,
            KeyboardBindableAction? rebindingAction)
        {
            MovementScheme = movementScheme;
            MovementDisplayName = movementDisplayName ?? string.Empty;
            PushDisplayName = pushDisplayName ?? string.Empty;
            FlipDisplayName = flipDisplayName ?? string.Empty;
            IsRebinding = isRebinding;
            RebindingAction = rebindingAction;
        }

        public KeyboardMovementScheme MovementScheme { get; }

        public string MovementDisplayName { get; }

        public string PushDisplayName { get; }

        public string FlipDisplayName { get; }

        public bool IsRebinding { get; }

        public KeyboardBindableAction? RebindingAction { get; }
    }

    public readonly struct KeyboardRebindResult
    {
        public KeyboardRebindResult(
            KeyboardBindableAction action,
            KeyboardBindingValidationResult validationResult,
            KeyboardBindingSettingsSnapshot snapshot,
            KeyboardBindableAction? conflictingAction = null)
        {
            Action = action;
            ValidationResult = validationResult;
            Snapshot = snapshot;
            ConflictingAction = conflictingAction;
        }

        public KeyboardBindableAction Action { get; }

        public KeyboardBindingValidationResult ValidationResult { get; }

        public KeyboardBindingSettingsSnapshot Snapshot { get; }

        public KeyboardBindableAction? ConflictingAction { get; }
    }

    public readonly struct KeyboardRebindStartResult
    {
        public KeyboardRebindStartResult(
            bool started,
            KeyboardBindingValidationResult validationResult,
            KeyboardBindingSettingsSnapshot snapshot)
        {
            Started = started;
            ValidationResult = validationResult;
            Snapshot = snapshot;
        }

        public bool Started { get; }

        public KeyboardBindingValidationResult ValidationResult { get; }

        public KeyboardBindingSettingsSnapshot Snapshot { get; }
    }

    public interface IKeyboardBindingSettingsPort
    {
        KeyboardBindingSettingsSnapshot Read();

        KeyboardBindingValidationResult TrySetMovementScheme(KeyboardMovementScheme scheme);

        KeyboardRebindStartResult StartRebind(
            KeyboardBindableAction action,
            Action<KeyboardRebindResult> completed);

        void CancelRebind();

        KeyboardBindingSettingsSnapshot ResetToDefaults();

        bool IsRebinding { get; }
    }

    public sealed class NoOpKeyboardBindingSettingsPort : IKeyboardBindingSettingsPort
    {
        public static readonly NoOpKeyboardBindingSettingsPort Instance = new NoOpKeyboardBindingSettingsPort();

        private static readonly KeyboardBindingSettingsSnapshot DefaultSnapshot = new(
            KeyboardMovementScheme.Wasd,
            "WASD",
            "E",
            "Q",
            false,
            null);

        private NoOpKeyboardBindingSettingsPort()
        {
        }

        public bool IsRebinding => false;

        public KeyboardBindingSettingsSnapshot Read()
        {
            return DefaultSnapshot;
        }

        public KeyboardBindingValidationResult TrySetMovementScheme(KeyboardMovementScheme scheme)
        {
            return scheme == KeyboardMovementScheme.Wasd
                ? KeyboardBindingValidationResult.Success
                : KeyboardBindingValidationResult.MissingBinding;
        }

        public KeyboardRebindStartResult StartRebind(
            KeyboardBindableAction action,
            Action<KeyboardRebindResult> completed)
        {
            return new KeyboardRebindStartResult(
                false,
                KeyboardBindingValidationResult.MissingBinding,
                DefaultSnapshot);
        }

        public void CancelRebind()
        {
        }

        public KeyboardBindingSettingsSnapshot ResetToDefaults()
        {
            return DefaultSnapshot;
        }
    }
}
