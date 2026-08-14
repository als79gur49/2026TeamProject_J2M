using System;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class GameplayPlayerActionCountPresentationRuntime : MonoBehaviour,
        IGameplayTickPresentationExtension,
        IGameplayOutputCameraPresentationExtension,
        IGameplayPlayerAnchoredPresentationExtension,
        IGameplayStageTerminalPresentationExtension,
        IGameplayBootstrapReadiness
    {
        private const string MountName = "PlayerActionCountPresentationRoot";

        [SerializeField] private GameplayPlayerActionCountView counterViewPrefab;
        [SerializeField, Min(0f)] private float opaqueDurationSeconds = 1f;
        [SerializeField, Min(0.01f)] private float fadeDurationSeconds = 0.5f;
        [SerializeField, Min(0f)] private float surfaceInsetDistance = 1.5f;
        [SerializeField, Min(0f)] private float cameraRightOffsetDistance = 0.25f;

        private readonly PlayerActionUseCounterState _counterState = new();

        private GameplayPlayerActionCountView _counterView;
        private GameplayPresentationStateStore _stateStore;
        private GameplayEntityView _boundPlayerView;
        private Camera _outputCamera;
        private Transform _boardPresentationRoot;
        private Transform _mount;
        private int _playerEntityId;

        public GameplayPlayerActionCountView CounterViewPrefab => counterViewPrefab;

        public float OpaqueDurationSeconds => opaqueDurationSeconds;

        public float FadeDurationSeconds => fadeDurationSeconds;

        public float SurfaceInsetDistance => surfaceInsetDistance;

        public float CameraRightOffsetDistance => cameraRightOffsetDistance;

        public bool IsReady =>
            counterViewPrefab != null &&
            counterViewPrefab.IsReady &&
            opaqueDurationSeconds >= 0f &&
            fadeDurationSeconds > 0f &&
            surfaceInsetDistance >= 0f &&
            cameraRightOffsetDistance >= 0f;

        internal int Count => _counterState.Count;

        internal bool IsVisible => _counterState.IsVisible;

        internal float CurrentAlpha => _counterState.Alpha;

        internal int PlayerEntityId => _playerEntityId;

        internal GameplayPlayerActionCountView CounterView => _counterView;

        internal Transform Mount => _mount;

        public string DescribeReadiness()
        {
            if (counterViewPrefab == null)
            {
                return $"{nameof(GameplayPlayerActionCountPresentationRuntime)} requires a counter view prefab.";
            }

            if (!counterViewPrefab.IsReady)
            {
                return $"{nameof(GameplayPlayerActionCountPresentationRuntime)} counter view prefab is missing required references.";
            }

            if (opaqueDurationSeconds < 0f ||
                fadeDurationSeconds <= 0f ||
                surfaceInsetDistance < 0f ||
                cameraRightOffsetDistance < 0f)
            {
                return $"{nameof(GameplayPlayerActionCountPresentationRuntime)} has invalid presentation timing or surface inset values.";
            }

            return $"{nameof(GameplayPlayerActionCountPresentationRuntime)} is ready.";
        }

        public void ConfigurePlayerAnchorContext(int playerEntityId, Transform boardPresentationRoot)
        {
            if (playerEntityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerEntityId));
            }

            if (boardPresentationRoot == null)
            {
                throw new ArgumentNullException(nameof(boardPresentationRoot));
            }

            var contextChanged = _playerEntityId != playerEntityId ||
                                 _boardPresentationRoot != boardPresentationRoot;
            _playerEntityId = playerEntityId;
            _boardPresentationRoot = boardPresentationRoot;
            _counterState.ConfigurePlayerEntityId(playerEntityId);

            if (contextChanged)
            {
                _boundPlayerView = null;
                DestroyMount();
            }
        }

        public void ConfigureOutputCamera(Camera outputCamera, Transform localSpaceRoot)
        {
            _ = localSpaceRoot;
            _outputCamera = outputCamera;
            _boundPlayerView = null;
            if (_counterState.IsVisible)
            {
                RefreshVisibleView();
            }
        }

        public void ResetSession()
        {
            _counterState.Reset();
            _stateStore = null;
            _boundPlayerView = null;
            DestroyMount();
        }

        public void Present(in GameplayTickPresentationExtensionContext context)
        {
            if (_playerEntityId <= 0 || _boardPresentationRoot == null)
            {
                return;
            }

            _stateStore = context.StateStore;
            var presentationData = context.Result.PresentationData;
            if (TryResetForCanonicalPlayer(presentationData))
            {
                _boundPlayerView = null;
                DestroyMount();
                return;
            }

            var signals = presentationData.PlayerActionSignals;
            var changed = false;
            for (var i = 0; i < signals.Count; i++)
            {
                changed |= _counterState.TryConsume(signals[i]);
            }

            if (changed)
            {
                RefreshVisibleView(playIncrementEffect: true);
            }
            else if (_counterState.IsVisible)
            {
                EnsurePlayerViewBinding();
            }
        }

        public void UpdatePresentation(float deltaTime)
        {
            if (!_counterState.IsVisible)
            {
                return;
            }

            _counterView?.AdvanceEffect(deltaTime);
            _counterState.Advance(deltaTime, opaqueDurationSeconds, fadeDurationSeconds);
            if (!_counterState.IsVisible)
            {
                _counterView?.Hide();
                return;
            }

            if (EnsurePlayerViewBinding())
            {
                _counterView.SetAlpha(_counterState.Alpha);
            }
        }

        public void ApplyStageTerminalPresentation(in GameplayStageTerminalPresentationContext context)
        {
            if (context.Reason != GameplayStageTerminalPresentationReason.PlayerDeathRetry &&
                context.Reason != GameplayStageTerminalPresentationReason.LevelFailed)
            {
                return;
            }

            ResetSession();
        }

        public void HardCleanup()
        {
            ResetSession();
        }

        private void OnDestroy()
        {
            DestroyMount();
        }

        private bool TryResetForCanonicalPlayer(TickPresentationData presentationData)
        {
            var deathSignals = presentationData.PlayerDeathSignals;
            for (var i = 0; i < deathSignals.Count; i++)
            {
                if (_counterState.TryReset(deathSignals[i]))
                {
                    return true;
                }
            }

            var spawnSignals = presentationData.EntitySpawnSignals;
            for (var i = 0; i < spawnSignals.Count; i++)
            {
                if (_counterState.TryReset(spawnSignals[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshVisibleView(bool playIncrementEffect = false)
        {
            if (!EnsurePlayerViewBinding())
            {
                return;
            }

            if (playIncrementEffect)
            {
                _counterView.ShowCountWithIncrementEffect(_counterState.Count);
            }
            else
            {
                _counterView.ShowCount(_counterState.Count);
            }
            _counterView.SetAlpha(_counterState.Alpha);
        }

        private bool EnsurePlayerViewBinding()
        {
            if (_playerEntityId <= 0 ||
                _stateStore == null ||
                !_stateStore.ViewsByEntityId.TryGetValue(_playerEntityId, out var playerView) ||
                playerView == null)
            {
                _boundPlayerView = null;
                _counterView?.Hide();
                return false;
            }

            EnsureCounterView();
            if (_boundPlayerView != playerView)
            {
                _boundPlayerView = playerView;
                _counterView.Bind(
                    playerView.transform,
                    surfaceInsetDistance,
                    cameraRightOffsetDistance,
                    _outputCamera);
                if (_counterState.IsVisible)
                {
                    _counterView.ShowCount(_counterState.Count);
                }
            }

            return true;
        }

        private void EnsureCounterView()
        {
            if (_counterView != null)
            {
                return;
            }

            if (!IsReady)
            {
                throw new InvalidOperationException(DescribeReadiness());
            }

            EnsureMount();
            _counterView = Instantiate(counterViewPrefab, _mount, worldPositionStays: false);
            _counterView.name = counterViewPrefab.name;
            _counterView.Hide();
        }

        private void EnsureMount()
        {
            if (_mount != null)
            {
                return;
            }

            var mountObject = new GameObject(MountName);
            _mount = mountObject.transform;
            _mount.SetParent(_boardPresentationRoot, worldPositionStays: false);
        }

        private void DestroyMount()
        {
            _counterView = null;
            if (_mount == null)
            {
                return;
            }

            var mountObject = _mount.gameObject;
            _mount = null;
            if (Application.isPlaying)
            {
                Destroy(mountObject);
            }
            else
            {
                DestroyImmediate(mountObject);
            }
        }
    }
}
