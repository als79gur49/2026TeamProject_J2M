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
        private bool _hasBufferedFlip;
        private bool _isInitialized;
        private TickInputBuffer _inputBuffer;
        private InputRepeatCooldown _inputRepeatCooldown;
        private InputAction _flipAction;
        private int _maxTicksPerFrame;
        private InputAction _moveAction;
        private float _moveDeadzone;
        private GameplayTickViewPresenter _presenter;
        private InputAction _pushAction;
        private TickRunner _runner;
        private Vector2 _sampledMoveInput;
        private float _simulationTickIntervalSeconds;
        private bool _hasBufferedPush;

        public void Initialize(
            TickInputBuffer inputBuffer,
            TickRunner runner,
            GameplayTickViewPresenter presenter,
            InputActionAsset actions,
            GameplayTimingProfile timingProfile,
            float moveDeadzone,
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

            if (moveDeadzone < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(moveDeadzone), "Move deadzone must be zero or greater.");
            }

            UnbindActions();

            _inputBuffer = inputBuffer;
            _runner = runner;
            _presenter = presenter;
            _actions = actions;
            _simulationTickIntervalSeconds = (timingProfile ?? throw new ArgumentNullException(nameof(timingProfile)))
                .SimulationTickIntervalSeconds;
            _maxTicksPerFrame = timingProfile.MaxTicksPerFrame;
            _moveDeadzone = moveDeadzone;
            _autoAdvanceTicks = autoAdvanceTicks;
            _accumulatedTime = 0f;
            _hasBufferedFlip = false;
            _hasBufferedPush = false;
            _sampledMoveInput = Vector2.zero;
            _inputRepeatCooldown = new InputRepeatCooldown(
                timingProfile,
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
            while (_accumulatedTime >= _simulationTickIntervalSeconds &&
                   executedTickCount < _maxTicksPerFrame)
            {
                _accumulatedTime -= _simulationTickIntervalSeconds;
                RunSingleTick();
                executedTickCount++;
            }

            if (executedTickCount == _maxTicksPerFrame &&
                _accumulatedTime >= _simulationTickIntervalSeconds)
            {
                _accumulatedTime = Mathf.Min(_accumulatedTime, _simulationTickIntervalSeconds);
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

        public void BufferPush()
        {
            EnsureInitialized();
            _hasBufferedPush = true;
        }

        public void BufferFlip()
        {
            EnsureInitialized();
            _hasBufferedFlip = true;
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

            _pushAction = _actions.FindAction("Player/Push", throwIfNotFound: false);
            if (_pushAction == null)
            {
                throw new InvalidOperationException("GameplayInputHost requires a Player/Push action on the provided InputActionAsset.");
            }

            _flipAction = _actions.FindAction("Player/Flip", throwIfNotFound: false);
            if (_flipAction == null)
            {
                throw new InvalidOperationException("GameplayInputHost requires a Player/Flip action on the provided InputActionAsset.");
            }

            _moveAction.performed += OnMovePerformed;
            _moveAction.canceled += OnMoveCanceled;
            _pushAction.started += OnPushStarted;
            _pushAction.performed += OnPushPerformed;
            _flipAction.started += OnFlipStarted;
            _flipAction.performed += OnFlipPerformed;

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

        private void OnPushPerformed(InputAction.CallbackContext context)
        {
            BufferPush();
        }

        private void OnPushStarted(InputAction.CallbackContext context)
        {
            BufferPush();
        }

        private void OnFlipPerformed(InputAction.CallbackContext context)
        {
            BufferFlip();
        }

        private void OnFlipStarted(InputAction.CallbackContext context)
        {
            BufferFlip();
        }

        private void UnbindActions()
        {
            if (_moveAction != null)
            {
                _moveAction.performed -= OnMovePerformed;
                _moveAction.canceled -= OnMoveCanceled;
                _moveAction = null;
            }

            if (_pushAction != null)
            {
                _pushAction.started -= OnPushStarted;
                _pushAction.performed -= OnPushPerformed;
                _pushAction = null;
            }

            if (_flipAction != null)
            {
                _flipAction.started -= OnFlipStarted;
                _flipAction.performed -= OnFlipPerformed;
                _flipAction = null;
            }

            if (_actions != null)
            {
                _actions.Disable();
            }

            _hasBufferedFlip = false;
            _hasBufferedPush = false;
        }

        private PlayerTickCommand ResolveTickCommand(PlayerTickCommand moveCommand)
        {
            var pushPressed = _hasBufferedPush || (_pushAction != null && _pushAction.IsPressed());
            var flipPressed = _hasBufferedFlip || (_flipAction != null && _flipAction.IsPressed());

            var command = PlayerTickCommand.Create(
                moveCommand.MoveDirection,
                pushPressed: pushPressed && moveCommand.MoveDirection != Direction.None,
                flipPressed: flipPressed && moveCommand.MoveDirection != Direction.None);

            _hasBufferedPush = false;
            _hasBufferedFlip = false;

            if (command.MoveDirection == Direction.None &&
                !command.PushPressed &&
                !command.FlipPressed)
            {
                return PlayerTickCommand.None;
            }

            return command;
        }
    }
}
