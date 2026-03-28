using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplayInputHost : MonoBehaviour
    {
        private InputActionAsset _actions;
        private float _accumulatedTime;
        private bool _autoAdvanceTicks;
        private bool _hasBufferedThrow;
        private bool _isInitialized;
        private TickInputBuffer _inputBuffer;
        private InputRepeatCooldown _inputRepeatCooldown;
        private InputAction _interactAction;
        private bool _hasBufferedInteract;
        private InputAction _moveAction;
        private float _moveDeadzone;
        private GameplayTickViewPresenter _presenter;
        private TickRunner _runner;
        private Vector2 _sampledMoveInput;
        private float _tickIntervalSeconds;
        private InputAction _throwAction;

        public void Initialize(
            TickInputBuffer inputBuffer,
            TickRunner runner,
            GameplayTickViewPresenter presenter,
            InputActionAsset actions,
            float tickIntervalSeconds,
            float moveDeadzone,
            int initialMoveDelayTicks,
            int repeatedMoveIntervalTicks,
            bool directionChangeConsumesDelay,
            bool autoAdvanceTicks)
        {
            if (inputBuffer == null)
            {
                throw new ArgumentNullException(nameof(inputBuffer));
            }

            if (runner == null)
            {
                throw new ArgumentNullException(nameof(runner));
            }

            if (presenter == null)
            {
                throw new ArgumentNullException(nameof(presenter));
            }

            if (tickIntervalSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(tickIntervalSeconds), "Tick interval must be greater than zero.");
            }

            if (moveDeadzone < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(moveDeadzone), "Move deadzone must be zero or greater.");
            }

            UnbindActions();

            _inputBuffer = inputBuffer;
            _runner = runner;
            _presenter = presenter;
            _actions = actions;
            _tickIntervalSeconds = tickIntervalSeconds;
            _moveDeadzone = moveDeadzone;
            _autoAdvanceTicks = autoAdvanceTicks;
            _accumulatedTime = 0f;
            _hasBufferedThrow = false;
            _hasBufferedInteract = false;
            _sampledMoveInput = Vector2.zero;
            _inputRepeatCooldown = new InputRepeatCooldown(
                initialMoveDelayTicks,
                repeatedMoveIntervalTicks,
                directionChangeConsumesDelay);
            _isInitialized = true;

            BindActions();
        }

        public int AdvanceTime(float deltaTime)
        {
            EnsureInitialized();

            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be zero or greater.");
            }

            _accumulatedTime += deltaTime;

            var executedTickCount = 0;
            while (_accumulatedTime >= _tickIntervalSeconds)
            {
                _accumulatedTime -= _tickIntervalSeconds;
                RunSingleTick();
                executedTickCount++;
            }

            return executedTickCount;
        }

        public TickResult RunSingleTick()
        {
            EnsureInitialized();

            var tickIndex = _runner.NextTickIndex;
            var quantizedDirection = GridMoveInputQuantizer.Quantize(_sampledMoveInput, _moveDeadzone);
            var moveCommand = _inputRepeatCooldown.BuildCommand(tickIndex, quantizedDirection);
            var playerCommand = ResolveTickCommand(moveCommand);

            _inputBuffer.Record(new TickInput(tickIndex, playerCommand));

            var result = _runner.RunNextTick();
            _presenter.Present(result);
            return result;
        }

        public void SetAutoAdvanceTicks(bool autoAdvanceTicks)
        {
            _autoAdvanceTicks = autoAdvanceTicks;
        }

        public void SetRawMoveInput(Vector2 rawMoveInput)
        {
            _sampledMoveInput = rawMoveInput;

            if (rawMoveInput == Vector2.zero)
            {
                _inputRepeatCooldown?.Reset();
            }
        }

        public void BufferInteract()
        {
            EnsureInitialized();
            _hasBufferedInteract = true;
        }

        public void BufferPush()
        {
            BufferInteract();
        }

        public void BufferThrow()
        {
            BufferFlip();
        }

        public void BufferFlip()
        {
            EnsureInitialized();
            _hasBufferedThrow = true;
        }

        private void Update()
        {
            if (_isInitialized && _autoAdvanceTicks)
            {
                AdvanceTime(Time.deltaTime);
            }
        }

        private void OnDisable()
        {
            UnbindActions();
        }

        private void OnDestroy()
        {
            UnbindActions();
        }

        private void BindActions()
        {
            if (_actions == null)
            {
                return;
            }

            _actions.Enable();
            _moveAction = _actions.FindAction("Player/Move", throwIfNotFound: false);
            if (_moveAction == null)
            {
                throw new InvalidOperationException("GameplayInputHost requires a Player/Move action on the provided InputActionAsset.");
            }

            _interactAction = _actions.FindAction("Player/Push", throwIfNotFound: false) ??
                              _actions.FindAction("Player/Interact", throwIfNotFound: false);
            if (_interactAction == null)
            {
                throw new InvalidOperationException("GameplayInputHost requires a Player/Push action or legacy Player/Interact action on the provided InputActionAsset.");
            }

            _throwAction = _actions.FindAction("Player/Flip", throwIfNotFound: false) ??
                           _actions.FindAction("Player/Throw", throwIfNotFound: false);

            _moveAction.performed += OnMovePerformed;
            _moveAction.canceled += OnMoveCanceled;
            _interactAction.started += OnInteractStarted;
            _interactAction.performed += OnInteractPerformed;
            if (_throwAction != null)
            {
                _throwAction.started += OnThrowStarted;
                _throwAction.performed += OnThrowPerformed;
            }

            _sampledMoveInput = _moveAction.ReadValue<Vector2>();
        }

        private void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("GameplayInputHost must be initialized before use.");
            }
        }

        private void OnMoveCanceled(InputAction.CallbackContext context)
        {
            _sampledMoveInput = Vector2.zero;
            _inputRepeatCooldown?.Reset();
        }

        private void OnMovePerformed(InputAction.CallbackContext context)
        {
            _sampledMoveInput = context.ReadValue<Vector2>();
        }

        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            _hasBufferedInteract = true;
        }

        private void OnInteractStarted(InputAction.CallbackContext context)
        {
            _hasBufferedInteract = true;
        }

        private void OnThrowPerformed(InputAction.CallbackContext context)
        {
            _hasBufferedThrow = true;
        }

        private void OnThrowStarted(InputAction.CallbackContext context)
        {
            _hasBufferedThrow = true;
        }

        private void UnbindActions()
        {
            if (_moveAction != null)
            {
                _moveAction.performed -= OnMovePerformed;
                _moveAction.canceled -= OnMoveCanceled;
                _moveAction = null;
            }

            if (_interactAction != null)
            {
                _interactAction.started -= OnInteractStarted;
                _interactAction.performed -= OnInteractPerformed;
                _interactAction = null;
            }

            if (_throwAction != null)
            {
                _throwAction.started -= OnThrowStarted;
                _throwAction.performed -= OnThrowPerformed;
                _throwAction = null;
            }

            if (_actions != null)
            {
                _actions.Disable();
            }

            _hasBufferedThrow = false;
            _hasBufferedInteract = false;
        }

        private PlayerTickCommand ResolveTickCommand(PlayerTickCommand moveCommand)
        {
            var interactPressed = _hasBufferedInteract || (_interactAction != null && _interactAction.IsPressed());
            var flipPressed = _hasBufferedThrow || (_throwAction != null && _throwAction.IsPressed());

            var command = PlayerTickCommand.Create(
                moveCommand.MoveDirection,
                interactPressed && moveCommand.MoveDirection != Direction.None,
                flipPressed && moveCommand.MoveDirection != Direction.None);

            _hasBufferedInteract = false;
            _hasBufferedThrow = false;

            if (command.MoveDirection == Direction.None &&
                !command.InteractPressed &&
                !command.FlipPressed)
            {
                return PlayerTickCommand.None;
            }

            return command;
        }
    }
}
