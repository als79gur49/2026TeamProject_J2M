using System;
using Game.Feature.UI.Application;
using Game.Feature.UI.Screens;
using KeyboardBindingSettingsService = Game.Shared.Input.KeyboardBindingSettingsService;
using SharedKeyboardBindableAction = Game.Shared.Input.KeyboardBindableAction;
using SharedKeyboardBindingSettingsSnapshot = Game.Shared.Input.KeyboardBindingSettingsSnapshot;
using SharedKeyboardBindingValidationStatus = Game.Shared.Input.KeyboardBindingValidationStatus;
using SharedKeyboardMovementScheme = Game.Shared.Input.KeyboardMovementScheme;
using SharedKeyboardRebindResult = Game.Shared.Input.KeyboardRebindResult;
using SharedKeyboardRebindStartResult = Game.Shared.Input.KeyboardRebindStartResult;

namespace Game.Feature.UI.Composition
{
    internal sealed class KeyboardBindingSettingsPortAdapter : IKeyboardBindingSettingsPort
    {
        private readonly KeyboardBindingSettingsService _service;

        public KeyboardBindingSettingsPortAdapter(KeyboardBindingSettingsService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public bool IsRebinding => _service.IsRebinding;

        public KeyboardBindingSettingsSnapshot Read()
        {
            return ToUi(_service.Read());
        }

        public KeyboardBindingValidationResult TrySetMovementScheme(KeyboardMovementScheme scheme)
        {
            return ToUi(_service.SetMovementScheme(ToShared(scheme)));
        }

        public KeyboardRebindStartResult StartRebind(
            KeyboardBindableAction action,
            Action<KeyboardRebindResult> completed)
        {
            SharedKeyboardRebindStartResult result = _service.StartRebind(
                ToShared(action),
                sharedResult => completed?.Invoke(ToUi(sharedResult)));
            return new KeyboardRebindStartResult(
                result.Started,
                ToUi(result.Status),
                ToUi(result.Snapshot));
        }

        public void CancelRebind()
        {
            _service.CancelRebind();
        }

        public KeyboardBindingSettingsSnapshot ResetToDefaults()
        {
            return ToUi(_service.ResetToDefaults());
        }

        private static KeyboardBindingSettingsSnapshot ToUi(SharedKeyboardBindingSettingsSnapshot snapshot)
        {
            return new KeyboardBindingSettingsSnapshot(
                ToUi(snapshot.MovementScheme),
                snapshot.MovementDisplayName,
                snapshot.PushDisplayName,
                snapshot.FlipDisplayName,
                snapshot.IsRebinding,
                snapshot.RebindingAction.HasValue ? ToUi(snapshot.RebindingAction.Value) : (KeyboardBindableAction?)null);
        }

        private static KeyboardRebindResult ToUi(SharedKeyboardRebindResult result)
        {
            return new KeyboardRebindResult(
                ToUi(result.Action),
                ToUi(result.Status),
                ToUi(result.Snapshot));
        }

        private static KeyboardMovementScheme ToUi(SharedKeyboardMovementScheme scheme)
        {
            return scheme == SharedKeyboardMovementScheme.ArrowKeys
                ? KeyboardMovementScheme.ArrowKeys
                : KeyboardMovementScheme.Wasd;
        }

        private static SharedKeyboardMovementScheme ToShared(KeyboardMovementScheme scheme)
        {
            return scheme == KeyboardMovementScheme.ArrowKeys
                ? SharedKeyboardMovementScheme.ArrowKeys
                : SharedKeyboardMovementScheme.Wasd;
        }

        private static KeyboardBindableAction ToUi(SharedKeyboardBindableAction action)
        {
            return action == SharedKeyboardBindableAction.Flip
                ? KeyboardBindableAction.Flip
                : KeyboardBindableAction.Push;
        }

        private static SharedKeyboardBindableAction ToShared(KeyboardBindableAction action)
        {
            return action == KeyboardBindableAction.Flip
                ? SharedKeyboardBindableAction.Flip
                : SharedKeyboardBindableAction.Push;
        }

        private static KeyboardBindingValidationResult ToUi(SharedKeyboardBindingValidationStatus status)
        {
            switch (status)
            {
                case SharedKeyboardBindingValidationStatus.Success:
                    return KeyboardBindingValidationResult.Success;
                case SharedKeyboardBindingValidationStatus.Canceled:
                    return KeyboardBindingValidationResult.Canceled;
                case SharedKeyboardBindingValidationStatus.AlreadyRebinding:
                    return KeyboardBindingValidationResult.AlreadyRebinding;
                case SharedKeyboardBindingValidationStatus.MissingBinding:
                    return KeyboardBindingValidationResult.MissingBinding;
                case SharedKeyboardBindingValidationStatus.InvalidKey:
                    return KeyboardBindingValidationResult.InvalidKey;
                case SharedKeyboardBindingValidationStatus.ReservedKey:
                    return KeyboardBindingValidationResult.ReservedKey;
                case SharedKeyboardBindingValidationStatus.DuplicateAction:
                    return KeyboardBindingValidationResult.DuplicateAction;
                case SharedKeyboardBindingValidationStatus.MovementConflict:
                    return KeyboardBindingValidationResult.MovementConflict;
                default:
                    return KeyboardBindingValidationResult.InvalidKey;
            }
        }
    }
}
