using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Objectives;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplayInputHost : MonoBehaviour
    {
        private const float DefaultMoveBufferDurationSeconds = 0.125f;

        private InputActionAsset _actions;
        private float _accumulatedTime;
        private bool _areActionsBound;
        private bool _autoAdvanceTicks;
        private bool _hasBufferedFlip;
        private bool _isInitialized;
        private TickInputBuffer _inputBuffer;
        private InputAction _flipAction;
        private int _maxTicksPerFrame;
        private InputAction _moveAction;
        private PlayerMoveIntentBuffer _moveIntentBuffer;
        private float _moveDeadzone;
        private int _playerEntityId;
        private GameplayTickViewPresenter _presenter;
        private TickRunner _runner;
        private Vector2 _sampledMoveInput;
        private float _simulationTickIntervalSeconds;

        public event Action<TickResult> TickCompleted;

        public event Action<StageObjectiveTickResult> ObjectiveResultUpdated;

        public event Action StageCleared;

        public void Initialize(
            TickInputBuffer inputBuffer,
            TickRunner runner,
            GameplayTickViewPresenter presenter,
            InputActionAsset actions,
            GameplayTimingProfile timingProfile,
            int playerEntityId,
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

            if (playerEntityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerEntityId), "GameplayInputHost requires a positive player entity ID.");
            }

            UnbindActions();

            _inputBuffer = inputBuffer;
            _runner = runner;
            _presenter = presenter;
            _actions = actions;
            _playerEntityId = playerEntityId;
            _simulationTickIntervalSeconds = (timingProfile ?? throw new ArgumentNullException(nameof(timingProfile)))
                .SimulationTickIntervalSeconds;
            _maxTicksPerFrame = timingProfile.MaxTicksPerFrame;
            _moveDeadzone = moveDeadzone;
            _moveIntentBuffer = new PlayerMoveIntentBuffer(DefaultMoveBufferDurationSeconds);
            _autoAdvanceTicks = autoAdvanceTicks;
            _accumulatedTime = 0f;
            _hasBufferedFlip = false;
            _sampledMoveInput = Vector2.zero;
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

            if (IsPresentationLocked())
            {
                AccumulateLockedTime(deltaTime);
                return 0;
            }

            _accumulatedTime += deltaTime;

            var executedTickCount = 0;
            while (_accumulatedTime >= _simulationTickIntervalSeconds &&
                   executedTickCount < _maxTicksPerFrame)
            {
                if (IsPresentationLocked())
                {
                    ClampAccumulatedTime();
                    break;
                }

                _accumulatedTime -= _simulationTickIntervalSeconds;
                RunSingleTickUnlocked();
                executedTickCount++;

                if (IsPresentationLocked())
                {
                    ClampAccumulatedTime();
                    break;
                }
            }

            if (executedTickCount == _maxTicksPerFrame &&
                _accumulatedTime >= _simulationTickIntervalSeconds)
            {
                ClampAccumulatedTime();
            }

            return executedTickCount;
        }

        public TickResult RunSingleTick()
        {
            EnsureInitialized();
            if (IsPresentationLocked())
            {
                return null;
            }

            return RunSingleTickUnlocked();
        }

        private TickResult RunSingleTickUnlocked()
        {
            var tickIndex = _runner.NextTickIndex;
            var playerCommand = BuildPlayerCommand();

            _inputBuffer.Record(new TickInput(tickIndex, playerCommand));

            var result = _runner.RunNextTick();
            ApplyAcceptedBufferedInput(result);
            _presenter.Present(result);
            TickCompleted?.Invoke(result);
            ObjectiveResultUpdated?.Invoke(result.ObjectiveResult);

            if (result.ObjectiveResult.ClearedThisTick)
            {
                StageCleared?.Invoke();
            }

            return result;
        }

        public void SetAutoAdvanceTicks(bool autoAdvanceTicks)
        {
            _autoAdvanceTicks = autoAdvanceTicks;
        }

        public void SetRawMoveInput(Vector2 rawMoveInput)
        {
            _sampledMoveInput = rawMoveInput;
            var now = ResolveCurrentInputTime();
            var sampledDirection = GridMoveInputQuantizer.Quantize(rawMoveInput, _moveDeadzone);
            _moveIntentBuffer?.UpdateSampledDirection(sampledDirection, now);
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

        private void OnEnable()
        {
            if (_isInitialized)
            {
                BindActions();
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
            if (_actions == null || _areActionsBound)
            {
                return;
            }

            _actions.Enable();
            _moveAction = _actions.FindAction("Player/Move", throwIfNotFound: false);
            if (_moveAction == null)
            {
                throw new InvalidOperationException("GameplayInputHost requires a Player/Move action on the provided InputActionAsset.");
            }

            _flipAction = _actions.FindAction("Player/Flip", throwIfNotFound: false);
            if (_flipAction == null)
            {
                throw new InvalidOperationException("GameplayInputHost requires a Player/Flip action on the provided InputActionAsset.");
            }

            _moveAction.performed += OnMovePerformed;
            _moveAction.canceled += OnMoveCanceled;
            _flipAction.started += OnFlipStarted;
            _flipAction.performed += OnFlipPerformed;

            _areActionsBound = true;
            SetRawMoveInput(_moveAction.ReadValue<Vector2>());
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
            SetRawMoveInput(Vector2.zero);
        }

        private void OnMovePerformed(InputAction.CallbackContext context)
        {
            SetRawMoveInput(context.ReadValue<Vector2>());
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

            _areActionsBound = false;
            _hasBufferedFlip = false;
            _sampledMoveInput = Vector2.zero;
            _moveIntentBuffer?.Reset();
        }

        private void AccumulateLockedTime(float deltaTime)
        {
            _accumulatedTime += deltaTime;
            ClampAccumulatedTime();
        }

        private void ClampAccumulatedTime()
        {
            _accumulatedTime = Mathf.Min(_accumulatedTime, _simulationTickIntervalSeconds);
        }

        private bool IsPresentationLocked()
        {
            return _presenter != null && _presenter.IsTopologyTransitionActive;
        }

        private PlayerTickCommand BuildPlayerCommand()
        {
            var now = ResolveCurrentInputTime();
            var sampledDirection = GridMoveInputQuantizer.Quantize(_sampledMoveInput, _moveDeadzone);
            _moveIntentBuffer.UpdateSampledDirection(sampledDirection, now);

            var resolvedDirection = _moveIntentBuffer.ResolveDirection(now, out var usesBufferedDirection);
            var flipPressed = _hasBufferedFlip || (_flipAction != null && _flipAction.IsPressed());
            _hasBufferedFlip = false;

            if (resolvedDirection == Direction.None)
            {
                return PlayerTickCommand.None;
            }

            if (flipPressed)
            {
                return PlayerTickCommand.Create(
                    resolvedDirection,
                    flipPressed: true,
                    isMoveBuffered: usesBufferedDirection);
            }

            return PlayerTickCommand.Move(resolvedDirection, isMoveBuffered: usesBufferedDirection);
        }

        private void ApplyAcceptedBufferedInput(TickResult result)
        {
            if (result == null)
            {
                return;
            }

            var playerActionSignals = result.PresentationData.PlayerActionSignals;
            for (var i = 0; i < playerActionSignals.Count; i++)
            {
                var signal = playerActionSignals[i];
                if (signal.EntityId != _playerEntityId)
                {
                    continue;
                }

                if (signal.StartedThisTick || signal.CompletedThisTick || signal.CanceledThisTick)
                {
                    _moveIntentBuffer.ClearBufferedDirection();
                    return;
                }
            }

            var entityMotions = result.PresentationData.EntityMotions;
            for (var i = 0; i < entityMotions.Count; i++)
            {
                if (entityMotions[i].EntityId == _playerEntityId)
                {
                    _moveIntentBuffer.ClearBufferedDirection();
                    return;
                }
            }
        }

        private static float ResolveCurrentInputTime()
        {
            return Time.unscaledTime;
        }
    }

    internal sealed class PlayerMoveIntentBuffer
    {
        private Direction _bufferedDirection;
        private float _bufferedUntilTime;
        private readonly float _bufferDurationSeconds;

        public PlayerMoveIntentBuffer(float bufferDurationSeconds)
        {
            if (bufferDurationSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferDurationSeconds), "Move intent buffer duration must be zero or greater.");
            }

            _bufferDurationSeconds = bufferDurationSeconds;
            Reset();
        }

        public Direction HeldDirection { get; private set; }

        public void UpdateSampledDirection(Direction direction, float now)
        {
            Expire(now);
            HeldDirection = direction;

            if (direction != Direction.None)
            {
                Buffer(direction, now);
            }
        }

        public Direction ResolveDirection(float now, out bool usesBufferedDirection)
        {
            Expire(now);

            if (HeldDirection != Direction.None)
            {
                usesBufferedDirection = false;
                return HeldDirection;
            }

            if (_bufferedDirection != Direction.None)
            {
                usesBufferedDirection = true;
                return _bufferedDirection;
            }

            usesBufferedDirection = false;
            return Direction.None;
        }

        public void ClearBufferedDirection()
        {
            _bufferedDirection = Direction.None;
            _bufferedUntilTime = 0f;
        }

        public void Reset()
        {
            HeldDirection = Direction.None;
            ClearBufferedDirection();
        }

        private void Buffer(Direction direction, float now)
        {
            _bufferedDirection = direction;
            _bufferedUntilTime = now + _bufferDurationSeconds;
        }

        private void Expire(float now)
        {
            if (_bufferedDirection != Direction.None &&
                now > _bufferedUntilTime)
            {
                ClearBufferedDirection();
            }
        }
    }
}
