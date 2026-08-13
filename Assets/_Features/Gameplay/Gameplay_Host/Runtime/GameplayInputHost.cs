using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Stages;
using Game.Shared.Input;
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
        private bool _hasBufferedPush;
        private bool _isInitialized;
        private bool _isKeyboardMoveOrderTrackerActionChangeSubscribed;
        private bool _isPlayerRespawnDelayInputBlocked;
        private bool _isRebuildingKeyboardMoveOrderTracker;
        private bool _isSimulationPaused;
        private bool _isTerminalHoldActive;
        private TerminalSessionToken _terminalHoldToken;
        private TickInputBuffer _inputBuffer;
        private ITerminalSessionReadModel _terminalSession;
        private ISceneEntryPresentationReadModel _sceneEntrySession;
        private InputAction _flipAction;
        private int _maxTicksPerFrame;
        private InputAction _moveAction;
        private KeyboardMoveOrderTracker _keyboardMoveOrderTracker;
        private InputAction _pushAction;
        private PlayerMoveIntentBuffer _moveIntentBuffer;
        private float _moveDeadzone;
        private int _playerEntityId;
        private GameplayTickViewPresenter _presenter;
        private TickRunner _runner;
        private Vector2 _sampledMoveInput;
        private float _simulationTickIntervalSeconds;
        private Direction _uiHeldMoveDirection;

        public event Action<TickResult> TickCompleted;

        public event Action<StageObjectiveTickResult> ObjectiveResultUpdated;

        internal int PlayerEntityId => _playerEntityId;

        public InputActionAsset Actions => _actions;

        internal bool IsSimulationPaused => _isSimulationPaused;

        internal bool IsTerminalHoldActive => _isTerminalHoldActive;

        public void Initialize(
            TickInputBuffer inputBuffer,
            TickRunner runner,
            GameplayTickViewPresenter presenter,
            InputActionAsset actions,
            GameplayTimingProfile timingProfile,
            int playerEntityId,
            float moveDeadzone,
            bool directionChangeConsumesDelay,
            bool autoAdvanceTicks,
            ITerminalSessionReadModel terminalSession,
            ISceneEntryPresentationReadModel sceneEntrySession = null)
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

            if (terminalSession == null)
            {
                throw new ArgumentNullException(
                    nameof(terminalSession),
                    "GameplayInputHost requires the persistent terminal-session read model.");
            }

            UnbindActions();
            UnsubscribeTerminalSession();
            UnsubscribeSceneEntrySession();

            _inputBuffer = inputBuffer;
            _runner = runner;
            _terminalSession = terminalSession;
            _sceneEntrySession = sceneEntrySession ?? SceneEntryPresentationRegistry.ReadModel;
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
            _hasBufferedPush = false;
            _sampledMoveInput = Vector2.zero;
            _uiHeldMoveDirection = Direction.None;
            _isSimulationPaused = false;
            _isTerminalHoldActive = false;
            _isPlayerRespawnDelayInputBlocked = false;
            _isInitialized = true;

            SubscribeTerminalSession();
            SubscribeSceneEntrySession();
            if (_terminalSession.IsActive || _sceneEntrySession.IsActive)
            {
                ClearAllPendingInputForTerminalSession();
            }
            else
            {
                BindActions();
            }
        }

        public int AdvanceTime(float deltaTime)
        {
            EnsureInitialized();

            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), "Delta time must be zero or greater.");
            }

            if (_isSimulationPaused || IsTerminalAdmissionBlocked())
            {
                return 0;
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
            if (_isSimulationPaused || IsTerminalAdmissionBlocked() || IsPresentationLocked())
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
            RefreshPlayerRespawnDelayInputBlock(result);
            // Presentation-state queries can run during Present before completed-snapshot caches refresh on TickCompleted.
            _presenter.Present(result);
            TickCompleted?.Invoke(result);
            ObjectiveResultUpdated?.Invoke(result.ObjectiveResult);

            return result;
        }

        public void SetAutoAdvanceTicks(bool autoAdvanceTicks)
        {
            _autoAdvanceTicks = autoAdvanceTicks;
        }

        internal void SetSimulationPaused(bool isSimulationPaused)
        {
            EnsureInitialized();
            _isSimulationPaused = isSimulationPaused;
        }

        internal void EnterTerminalHold()
        {
            EnsureInitialized();
            _isTerminalHoldActive = true;
            ClearPendingPlayerInput();
            ClearPendingUiInput();
            _accumulatedTime = 0f;
        }

        internal void EnterTerminalHold(TerminalSessionToken token)
        {
            if (!token.IsValid)
            {
                throw new ArgumentException(
                    "Terminal input hold requires a valid session token.",
                    nameof(token));
            }

            EnsureInitialized();
            if (_isTerminalHoldActive && _terminalHoldToken != token)
            {
                throw new InvalidOperationException(
                    "A different terminal session already owns the explicit input hold.");
            }

            _terminalHoldToken = token;
            _isTerminalHoldActive = true;
            ClearPendingPlayerInput();
            ClearPendingUiInput();
            _accumulatedTime = 0f;
        }

        internal void ExitTerminalHold()
        {
            EnsureInitialized();
            _isTerminalHoldActive = false;
            _terminalHoldToken = default;
        }

        internal bool TryExitTerminalHold(TerminalSessionToken token)
        {
            EnsureInitialized();
            if (!_isTerminalHoldActive ||
                !_terminalHoldToken.IsValid ||
                _terminalHoldToken != token)
            {
                return false;
            }

            _isTerminalHoldActive = false;
            _terminalHoldToken = default;
            return true;
        }

        public void SetRawMoveInput(Vector2 rawMoveInput)
        {
            EnsureInitialized();
            if (IsTerminalAdmissionBlocked() || _isPlayerRespawnDelayInputBlocked)
            {
                _sampledMoveInput = Vector2.zero;
                _moveIntentBuffer?.Reset();
                return;
            }

            var previousMoveInput = _sampledMoveInput;
            _sampledMoveInput = rawMoveInput;
            var now = ResolveCurrentInputTime();
            var sampledDirection = ResolveRawMoveDirection(rawMoveInput, previousMoveInput);
            _moveIntentBuffer?.UpdateSampledDirection(sampledDirection, now);
        }

        public void BufferFlip()
        {
            EnsureInitialized();
            if (IsTerminalAdmissionBlocked() || _isPlayerRespawnDelayInputBlocked)
            {
                return;
            }

            _hasBufferedFlip = true;
        }

        public void BufferPush()
        {
            EnsureInitialized();
            if (IsTerminalAdmissionBlocked() || _isPlayerRespawnDelayInputBlocked)
            {
                return;
            }

            _hasBufferedPush = true;
        }

        internal void SetUiHeldMoveDirection(Direction direction)
        {
            EnsureInitialized();
            if (IsTerminalAdmissionBlocked() || _isPlayerRespawnDelayInputBlocked)
            {
                return;
            }

            if (!IsOrthogonalDirection(direction))
            {
                throw new ArgumentOutOfRangeException(nameof(direction), direction, "UI-held move directions must be orthogonal.");
            }

            _uiHeldMoveDirection = direction;
        }

        internal void ClearUiHeldMoveDirection()
        {
            EnsureInitialized();
            _uiHeldMoveDirection = Direction.None;
        }

        internal void ClearPendingUiInput()
        {
            EnsureInitialized();
            _uiHeldMoveDirection = Direction.None;
        }

        internal Direction PreviewPushDirection()
        {
            EnsureInitialized();

            if (IsTerminalAdmissionBlocked() || _isPlayerRespawnDelayInputBlocked)
            {
                return Direction.None;
            }

            var now = ResolveCurrentInputTime();
            var sampledDirection = ResolveSampledMoveDirection();
            _moveIntentBuffer.UpdateSampledDirection(sampledDirection, now);

            if (_uiHeldMoveDirection != Direction.None)
            {
                return _uiHeldMoveDirection;
            }

            return _moveIntentBuffer.ResolveDirection(now, out _);
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
            if (_isInitialized && !IsTerminalAdmissionBlocked())
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
            UnsubscribeTerminalSession();
            UnsubscribeSceneEntrySession();
        }

        private void BindActions()
        {
            if (_actions == null || _areActionsBound || IsTerminalAdmissionBlocked())
            {
                return;
            }

            _moveAction = _actions.FindAction(GameplayInputActionPaths.PlayerMove, throwIfNotFound: false);
            if (_moveAction == null)
            {
                throw new InvalidOperationException($"GameplayInputHost requires a {GameplayInputActionPaths.PlayerMove} action on the provided InputActionAsset.");
            }

            _flipAction = _actions.FindAction(GameplayInputActionPaths.PlayerFlip, throwIfNotFound: false);
            if (_flipAction == null)
            {
                throw new InvalidOperationException($"GameplayInputHost requires a {GameplayInputActionPaths.PlayerFlip} action on the provided InputActionAsset.");
            }

            _pushAction = _actions.FindAction(GameplayInputActionPaths.PlayerPush, throwIfNotFound: false);
            if (_pushAction == null)
            {
                throw new InvalidOperationException($"GameplayInputHost requires a {GameplayInputActionPaths.PlayerPush} action on the provided InputActionAsset.");
            }

            _moveAction.performed += OnMovePerformed;
            _moveAction.canceled += OnMoveCanceled;
            _pushAction.started += OnPushStarted;
            _flipAction.started += OnFlipStarted;
            _flipAction.performed += OnFlipPerformed;
            _moveAction.Enable();
            _flipAction.Enable();
            _pushAction.Enable();

            _areActionsBound = true;
            RebuildKeyboardMoveOrderTracker();
            SubscribeMoveActionChanges();
        }

        private void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("GameplayInputHost must be initialized before use.");
            }
        }

        private bool IsTerminalAdmissionBlocked()
        {
            return _isTerminalHoldActive ||
                   (_terminalSession?.IsActive ?? false) ||
                   (_sceneEntrySession?.IsActive ?? false) ||
                   MainMenuEntryPresentationRegistry.IsActive;
        }

        private void SubscribeTerminalSession()
        {
            if (_terminalSession != null)
            {
                _terminalSession.Changed += HandleTerminalSessionChanged;
            }
        }

        private void UnsubscribeTerminalSession()
        {
            if (_terminalSession != null)
            {
                _terminalSession.Changed -= HandleTerminalSessionChanged;
            }
        }

        private void HandleTerminalSessionChanged(TerminalSessionSnapshot snapshot)
        {
            if (!_isInitialized)
            {
                return;
            }

            if (snapshot.IsActive)
            {
                UnbindActions();
                ClearAllPendingInputForTerminalSession();
                return;
            }

            if (isActiveAndEnabled && !(_sceneEntrySession?.IsActive ?? false))
            {
                BindActions();
            }
        }

        private void SubscribeSceneEntrySession()
        {
            if (_sceneEntrySession != null)
            {
                _sceneEntrySession.Changed += HandleSceneEntrySessionChanged;
            }
        }

        private void UnsubscribeSceneEntrySession()
        {
            if (_sceneEntrySession != null)
            {
                _sceneEntrySession.Changed -= HandleSceneEntrySessionChanged;
            }
        }

        private void HandleSceneEntrySessionChanged(SceneEntryPresentationSnapshot snapshot)
        {
            if (!_isInitialized)
            {
                return;
            }

            if (snapshot.IsActive)
            {
                UnbindActions();
                ClearAllPendingInputForTerminalSession();
                return;
            }

            if (isActiveAndEnabled && !(_terminalSession?.IsActive ?? false))
            {
                ClearAllPendingInputForTerminalSession();
                BindActions();
            }
        }

        private void ClearAllPendingInputForTerminalSession()
        {
            ClearPendingPlayerInput();
            ClearPendingUiInput();
            _sampledMoveInput = Vector2.zero;
            _keyboardMoveOrderTracker?.Reset();
            _accumulatedTime = 0f;
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

        private void OnPushStarted(InputAction.CallbackContext context)
        {
            BufferPush();
        }

        private void UnbindActions()
        {
            UnsubscribeMoveActionChanges();

            if (_moveAction != null)
            {
                _moveAction.performed -= OnMovePerformed;
                _moveAction.canceled -= OnMoveCanceled;
                _moveAction.Disable();
                _moveAction = null;
            }

            _keyboardMoveOrderTracker?.Dispose();
            _keyboardMoveOrderTracker = null;

            if (_flipAction != null)
            {
                _flipAction.started -= OnFlipStarted;
                _flipAction.performed -= OnFlipPerformed;
                _flipAction.Disable();
                _flipAction = null;
            }

            if (_pushAction != null)
            {
                _pushAction.started -= OnPushStarted;
                _pushAction.Disable();
                _pushAction = null;
            }

            _areActionsBound = false;
            _hasBufferedFlip = false;
            _hasBufferedPush = false;
            _uiHeldMoveDirection = Direction.None;
            _sampledMoveInput = Vector2.zero;
            _keyboardMoveOrderTracker?.Reset();
            _moveIntentBuffer?.Reset();
        }

        private void SubscribeMoveActionChanges()
        {
            if (_isKeyboardMoveOrderTrackerActionChangeSubscribed)
            {
                return;
            }

            InputSystem.onActionChange += HandleInputActionChange;
            _isKeyboardMoveOrderTrackerActionChangeSubscribed = true;
        }

        private void UnsubscribeMoveActionChanges()
        {
            if (!_isKeyboardMoveOrderTrackerActionChangeSubscribed)
            {
                return;
            }

            InputSystem.onActionChange -= HandleInputActionChange;
            _isKeyboardMoveOrderTrackerActionChangeSubscribed = false;
        }

        private void HandleInputActionChange(object actionOrMapOrAsset, InputActionChange change)
        {
            if (!_areActionsBound ||
                change != InputActionChange.BoundControlsChanged ||
                !IsMoveBindingChange(actionOrMapOrAsset))
            {
                return;
            }

            RebuildKeyboardMoveOrderTracker();
        }

        private bool IsMoveBindingChange(object actionOrMapOrAsset)
        {
            if (_moveAction == null)
            {
                return false;
            }

            return ReferenceEquals(actionOrMapOrAsset, _moveAction) ||
                   ReferenceEquals(actionOrMapOrAsset, _moveAction.actionMap) ||
                   ReferenceEquals(actionOrMapOrAsset, _actions);
        }

        private void RebuildKeyboardMoveOrderTracker()
        {
            if (_moveAction == null || _isRebuildingKeyboardMoveOrderTracker)
            {
                return;
            }

            _isRebuildingKeyboardMoveOrderTracker = true;
            try
            {
                _keyboardMoveOrderTracker?.Dispose();
                _keyboardMoveOrderTracker = KeyboardMoveOrderTracker.Create(_moveAction, _moveDeadzone);
                _keyboardMoveOrderTracker.Enable();
                SetRawMoveInput(_moveAction.ReadValue<Vector2>());
            }
            finally
            {
                _isRebuildingKeyboardMoveOrderTracker = false;
            }
        }

        private void ClearPendingPlayerInput()
        {
            _sampledMoveInput = Vector2.zero;
            _hasBufferedFlip = false;
            _hasBufferedPush = false;
            _keyboardMoveOrderTracker?.Reset();
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
            return _presenter != null && _presenter.HasBlockingPresentation;
        }

        private PlayerTickCommand BuildPlayerCommand()
        {
            if (_isPlayerRespawnDelayInputBlocked)
            {
                ClearPendingPlayerInput();
                ClearPendingUiInput();
                return PlayerTickCommand.None;
            }

            if (IsPlayerActionAttemptPlaybackActive())
            {
                _moveIntentBuffer?.ClearBufferedDirection();
                ClearPendingPlayerActionInput();
                return PlayerTickCommand.None;
            }

            RefreshMoveInputFromAction();

            var now = ResolveCurrentInputTime();
            var sampledDirection = ResolveSampledMoveDirection();
            _moveIntentBuffer.UpdateSampledDirection(sampledDirection, now);

            var resolvedDirection = Direction.None;
            var usesBufferedDirection = false;

            if (_uiHeldMoveDirection != Direction.None)
            {
                resolvedDirection = _uiHeldMoveDirection;
            }
            else
            {
                resolvedDirection = _moveIntentBuffer.ResolveDirection(now, out usesBufferedDirection);
            }

            var heldMoveDirection = _uiHeldMoveDirection != Direction.None
                ? _uiHeldMoveDirection
                : _moveIntentBuffer.HeldDirection;

            var flipPressed = _hasBufferedFlip;
            var pushPressed = _hasBufferedPush;

            _hasBufferedFlip = false;
            _hasBufferedPush = false;

            if (pushPressed)
            {
                return PlayerTickCommand.Create(
                    resolvedDirection,
                    pushPressed: true,
                    isMoveBuffered: usesBufferedDirection,
                    heldMoveDirection: heldMoveDirection);
            }

            if (flipPressed)
            {
                return PlayerTickCommand.Create(
                    resolvedDirection,
                    flipPressed: true,
                    isMoveBuffered: usesBufferedDirection,
                    heldMoveDirection: heldMoveDirection);
            }

            if (resolvedDirection == Direction.None)
            {
                return PlayerTickCommand.None;
            }

            return PlayerTickCommand.Create(
                resolvedDirection,
                isMoveBuffered: usesBufferedDirection,
                heldMoveDirection: heldMoveDirection);
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

            var playerActionAttemptSignals = result.PresentationData.PlayerActionAttemptSignals;
            for (var i = 0; i < playerActionAttemptSignals.Count; i++)
            {
                if (playerActionAttemptSignals[i].EntityId == _playerEntityId)
                {
                    _moveIntentBuffer?.ClearBufferedDirection();
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

        private bool IsPlayerActionAttemptPlaybackActive()
        {
            return _presenter != null && _presenter.IsPlayerActionAttemptPlaybackActive(_playerEntityId);
        }

        private void ClearPendingPlayerActionInput()
        {
            _hasBufferedFlip = false;
            _hasBufferedPush = false;
        }

        private void RefreshMoveInputFromAction()
        {
            if (_moveAction == null)
            {
                return;
            }

            SetRawMoveInput(_moveAction.ReadValue<Vector2>());
        }

        private void RefreshPlayerRespawnDelayInputBlock(TickResult result)
        {
            if (result == null)
            {
                return;
            }

            var shouldBlock = false;
            var deathHoldSignals = result.PresentationData.PlayerDeathHoldSignals;
            for (var i = 0; i < deathHoldSignals.Count; i++)
            {
                if (deathHoldSignals[i].EntityId == _playerEntityId)
                {
                    shouldBlock = true;
                    break;
                }
            }

            _isPlayerRespawnDelayInputBlocked = shouldBlock;
            if (!shouldBlock)
            {
                return;
            }

            ClearPendingPlayerInput();
            ClearPendingUiInput();
        }

        private static float ResolveCurrentInputTime()
        {
            return Time.unscaledTime;
        }

        private Direction ResolveSampledMoveDirection()
        {
            if (_keyboardMoveOrderTracker != null &&
                _keyboardMoveOrderTracker.TryResolveHeldDirection(_sampledMoveInput, out var keyboardDirection))
            {
                return keyboardDirection;
            }

            var sampledDirection = GridMoveInputQuantizer.Quantize(_sampledMoveInput, _moveDeadzone);
            if (sampledDirection != Direction.None ||
                _sampledMoveInput.sqrMagnitude <= _moveDeadzone * _moveDeadzone)
            {
                return sampledDirection;
            }

            if (_moveIntentBuffer != null &&
                _moveIntentBuffer.HeldDirection != Direction.None)
            {
                return _moveIntentBuffer.HeldDirection;
            }

            return Direction.None;
        }

        private Direction ResolveRawMoveDirection(Vector2 rawInput, Vector2 previousRawInput)
        {
            if (_keyboardMoveOrderTracker != null &&
                _keyboardMoveOrderTracker.TryResolveHeldDirection(rawInput, out var keyboardDirection))
            {
                return keyboardDirection;
            }

            var sampledDirection = GridMoveInputQuantizer.Quantize(rawInput, _moveDeadzone);
            if (sampledDirection != Direction.None ||
                rawInput.sqrMagnitude <= _moveDeadzone * _moveDeadzone)
            {
                return sampledDirection;
            }

            var xBecameActive = Mathf.Abs(previousRawInput.x) <= _moveDeadzone &&
                                Mathf.Abs(rawInput.x) > _moveDeadzone;
            var yBecameActive = Mathf.Abs(previousRawInput.y) <= _moveDeadzone &&
                                Mathf.Abs(rawInput.y) > _moveDeadzone;

            if (xBecameActive != yBecameActive)
            {
                return xBecameActive
                    ? (rawInput.x > 0f ? Direction.Right : Direction.Left)
                    : (rawInput.y > 0f ? Direction.Up : Direction.Down);
            }

            var xDelta = Mathf.Abs(rawInput.x - previousRawInput.x);
            var yDelta = Mathf.Abs(rawInput.y - previousRawInput.y);
            if (xDelta > yDelta && Mathf.Abs(rawInput.x) > _moveDeadzone)
            {
                return rawInput.x > 0f ? Direction.Right : Direction.Left;
            }

            if (yDelta > xDelta && Mathf.Abs(rawInput.y) > _moveDeadzone)
            {
                return rawInput.y > 0f ? Direction.Up : Direction.Down;
            }

            return Direction.None;
        }

        private static bool IsOrthogonalDirection(Direction direction)
        {
            return direction == Direction.Up ||
                   direction == Direction.Right ||
                   direction == Direction.Down ||
                   direction == Direction.Left;
        }
    }

    internal sealed class KeyboardMoveOrderTracker : IDisposable
    {
        private readonly InputAction[] _actions;
        private readonly Direction[] _directions;
        private readonly bool[] _heldActions;
        private readonly int[] _heldDirectionCounts = new int[4];
        private readonly int[] _pressSequences = new int[4];
        private readonly float _deadzone;
        private int _nextPressSequence;

        private KeyboardMoveOrderTracker(InputAction[] actions, Direction[] directions, float deadzone)
        {
            _actions = actions;
            _directions = directions;
            _deadzone = deadzone;
            _heldActions = new bool[actions.Length];
        }

        public static KeyboardMoveOrderTracker Create(InputAction moveAction, float deadzone)
        {
            if (moveAction == null)
            {
                throw new ArgumentNullException(nameof(moveAction));
            }

            var actions = new System.Collections.Generic.List<InputAction>();
            var directions = new System.Collections.Generic.List<Direction>();
            foreach (var binding in moveAction.bindings)
            {
                if (!binding.isPartOfComposite ||
                    !TryResolveCompositeDirection(binding.name, out var direction) ||
                    !TryResolveKeyboardPath(binding.effectivePath, binding.path, out var keyboardPath))
                {
                    continue;
                }

                var action = new InputAction(
                    name: $"KeyboardMoveOrder_{direction}_{actions.Count}",
                    type: InputActionType.Button,
                    binding: keyboardPath);
                actions.Add(action);
                directions.Add(direction);
            }

            return new KeyboardMoveOrderTracker(actions.ToArray(), directions.ToArray(), deadzone);
        }

        public void Enable()
        {
            for (var i = 0; i < _actions.Length; i++)
            {
                var action = _actions[i];
                var actionIndex = i;
                action.started += context => MarkPressed(actionIndex);
                action.canceled += context => MarkReleased(actionIndex);
                action.Enable();
                if (action.IsPressed())
                {
                    MarkPressed(actionIndex);
                }
            }
        }

        public void Reset()
        {
            for (var i = 0; i < _heldActions.Length; i++)
            {
                _heldActions[i] = false;
            }

            for (var i = 0; i < _heldDirectionCounts.Length; i++)
            {
                _heldDirectionCounts[i] = 0;
                _pressSequences[i] = 0;
            }

            _nextPressSequence = 0;
        }

        public bool TryResolveHeldDirection(Vector2 rawInput, out Direction direction)
        {
            var bestDirection = Direction.None;
            var bestSequence = 0;
            for (var i = 0; i < _heldDirectionCounts.Length; i++)
            {
                if (_heldDirectionCounts[i] <= 0 || _pressSequences[i] <= bestSequence)
                {
                    continue;
                }

                var candidate = FromIndex(i);
                if (rawInput.sqrMagnitude <= _deadzone * _deadzone)
                {
                    continue;
                }

                bestDirection = candidate;
                bestSequence = _pressSequences[i];
            }

            direction = bestDirection;
            return direction != Direction.None;
        }

        public void Dispose()
        {
            for (var i = 0; i < _actions.Length; i++)
            {
                _actions[i].Dispose();
            }

            Reset();
        }

        private void MarkPressed(int actionIndex)
        {
            if (actionIndex < 0 ||
                actionIndex >= _actions.Length ||
                _heldActions[actionIndex])
            {
                return;
            }

            var direction = _directions[actionIndex];
            var index = ToIndex(direction);
            if (index < 0)
            {
                return;
            }

            _heldActions[actionIndex] = true;
            _heldDirectionCounts[index]++;
            _pressSequences[index] = ++_nextPressSequence;
        }

        private void MarkReleased(int actionIndex)
        {
            if (actionIndex < 0 ||
                actionIndex >= _actions.Length ||
                !_heldActions[actionIndex])
            {
                return;
            }

            var direction = _directions[actionIndex];
            var index = ToIndex(direction);
            if (index < 0)
            {
                return;
            }

            _heldActions[actionIndex] = false;
            _heldDirectionCounts[index] = Mathf.Max(0, _heldDirectionCounts[index] - 1);
            if (_heldDirectionCounts[index] == 0)
            {
                _pressSequences[index] = 0;
            }
        }

        private static bool TryResolveCompositeDirection(string bindingName, out Direction direction)
        {
            switch (bindingName?.ToLowerInvariant())
            {
                case "up":
                    direction = Direction.Up;
                    return true;
                case "right":
                    direction = Direction.Right;
                    return true;
                case "down":
                    direction = Direction.Down;
                    return true;
                case "left":
                    direction = Direction.Left;
                    return true;
                default:
                    direction = Direction.None;
                    return false;
            }
        }

        private static bool TryResolveKeyboardPath(string effectivePath, string fallbackPath, out string keyboardPath)
        {
            var path = !string.IsNullOrWhiteSpace(effectivePath) ? effectivePath : fallbackPath;
            if (!string.IsNullOrWhiteSpace(path) &&
                path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase))
            {
                keyboardPath = path;
                return true;
            }

            keyboardPath = null;
            return false;
        }

        private static bool IsRawDirectionActive(Vector2 rawInput, Direction direction, float deadzone)
        {
            switch (direction)
            {
                case Direction.Up:
                    return rawInput.y > deadzone;
                case Direction.Right:
                    return rawInput.x > deadzone;
                case Direction.Down:
                    return rawInput.y < -deadzone;
                case Direction.Left:
                    return rawInput.x < -deadzone;
                default:
                    return false;
            }
        }

        private static Direction FromIndex(int index)
        {
            switch (index)
            {
                case 0:
                    return Direction.Up;
                case 1:
                    return Direction.Right;
                case 2:
                    return Direction.Down;
                case 3:
                    return Direction.Left;
                default:
                    return Direction.None;
            }
        }

        private static int ToIndex(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    return 0;
                case Direction.Right:
                    return 1;
                case Direction.Down:
                    return 2;
                case Direction.Left:
                    return 3;
                default:
                    return -1;
            }
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
