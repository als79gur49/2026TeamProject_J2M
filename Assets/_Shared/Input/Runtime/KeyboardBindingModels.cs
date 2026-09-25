using System;

namespace Game.Shared.Input
{
    public enum KeyboardMovementScheme
    {
        Wasd = 0,
        ArrowKeys = 1,
    }

    public enum KeyboardBindableAction
    {
        Push = 0,
        Flip = 1,
    }

    public enum KeyboardBindingValidationStatus
    {
        Success = 0,
        Canceled = 1,
        AlreadyRebinding = 2,
        MissingBinding = 3,
        InvalidKey = 4,
        ReservedKey = 5,
        DuplicateAction = 6,
        MovementConflict = 7,
    }

    public readonly struct KeyboardBindingSettingsSnapshot
    {
        public KeyboardBindingSettingsSnapshot(
            KeyboardMovementScheme movementScheme,
            string pushDisplayName,
            string flipDisplayName,
            bool isRebinding,
            KeyboardBindableAction? rebindingAction)
        {
            MovementScheme = movementScheme;
            PushDisplayName = pushDisplayName ?? string.Empty;
            FlipDisplayName = flipDisplayName ?? string.Empty;
            IsRebinding = isRebinding;
            RebindingAction = rebindingAction;
        }

        public KeyboardMovementScheme MovementScheme { get; }

        public string PushDisplayName { get; }

        public string FlipDisplayName { get; }

        public bool IsRebinding { get; }

        public KeyboardBindableAction? RebindingAction { get; }
    }

    public readonly struct KeyboardRebindResult
    {
        public KeyboardRebindResult(
            KeyboardBindableAction action,
            KeyboardBindingValidationStatus status,
            KeyboardBindingSettingsSnapshot snapshot,
            KeyboardBindableAction? conflictingAction = null)
        {
            Action = action;
            Status = status;
            Snapshot = snapshot;
            ConflictingAction = conflictingAction;
        }

        public KeyboardBindableAction Action { get; }

        public KeyboardBindingValidationStatus Status { get; }

        public KeyboardBindingSettingsSnapshot Snapshot { get; }

        public KeyboardBindableAction? ConflictingAction { get; }
    }

    public readonly struct KeyboardRebindStartResult
    {
        public KeyboardRebindStartResult(
            bool started,
            KeyboardBindingValidationStatus status,
            KeyboardBindingSettingsSnapshot snapshot)
        {
            Started = started;
            Status = status;
            Snapshot = snapshot;
        }

        public bool Started { get; }

        public KeyboardBindingValidationStatus Status { get; }

        public KeyboardBindingSettingsSnapshot Snapshot { get; }
    }

    public interface IKeyboardBindingStore
    {
        bool TryLoadMovementScheme(out KeyboardMovementScheme scheme);

        void SaveMovementScheme(KeyboardMovementScheme scheme);

        bool TryLoadBindingOverridesJson(out string json);

        void SaveBindingOverridesJson(string json);

        void ClearBindingOverridesJson();

        void Save();
    }
}
