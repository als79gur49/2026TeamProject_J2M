using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class GameplayPlayerActionCountPresentationRuntime : MonoBehaviour,
        IGameplayTickPresentationExtension,
        IGameplayMotionProgressPresentationExtension,
        IGameplayOutputCameraPresentationExtension,
        IGameplayPlayerAnchoredPresentationExtension,
        IGameplayStageTerminalPresentationExtension,
        IGameplayBootstrapReadiness
    {
        private const string MountName = "PlayerActionCountPresentationRoot";

        [SerializeField] private GameplayPlayerActionCountView counterViewPrefab;
        [SerializeField, Min(0f)] private float opaqueDurationSeconds = 1f;
        [SerializeField, Min(0.01f)] private float fadeDurationSeconds = 0.5f;
        [SerializeField, Min(0f)] private float pushRevealDelaySeconds = 0.15f;
        [SerializeField, Range(0f, 1f)] private float flipPreContactLeadNormalized = 0.03f;
        [SerializeField, Min(0f)] private float surfaceInsetDistance = 1.5f;
        [SerializeField, Min(0f)] private float cameraRightOffsetDistance = 0.25f;

        private readonly PlayerActionUseCounterState _counterState = new();
        private readonly List<PendingActionCountReveal> _pendingReveals = new();

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

        public float PushRevealDelaySeconds => pushRevealDelaySeconds;

        public float FlipPreContactLeadNormalized => flipPreContactLeadNormalized;

        public float SurfaceInsetDistance => surfaceInsetDistance;

        public float CameraRightOffsetDistance => cameraRightOffsetDistance;

        public bool IsReady =>
            counterViewPrefab != null &&
            counterViewPrefab.IsReady &&
            opaqueDurationSeconds >= 0f &&
            fadeDurationSeconds > 0f &&
            pushRevealDelaySeconds >= 0f &&
            flipPreContactLeadNormalized >= 0f &&
            flipPreContactLeadNormalized < GameplayPresentationTimingConstants.FlipImpactInteractionOnsetNormalizedTime &&
            surfaceInsetDistance >= 0f &&
            cameraRightOffsetDistance >= 0f;

        internal int Count => _counterState.Count;

        internal bool IsVisible => _counterState.IsVisible;

        internal float CurrentAlpha => _counterState.Alpha;

        internal int VisibleCount => _counterState.VisibleCount;

        internal int PendingRevealCount => _pendingReveals.Count;

        internal float FlipFloorRevealNormalizedTime =>
            GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime -
            flipPreContactLeadNormalized;

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
                pushRevealDelaySeconds < 0f ||
                flipPreContactLeadNormalized < 0f ||
                flipPreContactLeadNormalized >= GameplayPresentationTimingConstants.FlipImpactInteractionOnsetNormalizedTime ||
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
                _pendingReveals.Clear();
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
            _pendingReveals.Clear();
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
                _pendingReveals.Clear();
                _boundPlayerView = null;
                DestroyMount();
                return;
            }

            var signals = presentationData.PlayerActionSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (_counterState.TryConsume(signal))
                {
                    ScheduleReveal(signal, context.Result.TickIndex, _counterState.Count);
                }
            }

            if (_counterState.IsVisible)
            {
                EnsurePlayerViewBinding();
            }
        }

        public void UpdatePresentation(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }

            if (_counterState.IsVisible)
            {
                _counterView?.AdvanceEffect(deltaTime);
                _counterState.Advance(deltaTime, opaqueDurationSeconds, fadeDurationSeconds);
                if (!_counterState.IsVisible)
                {
                    _counterView?.Hide();
                }
                else if (EnsurePlayerViewBinding())
                {
                    _counterView.SetAlpha(_counterState.Alpha);
                }
            }

            AdvanceTimedReveals(deltaTime);
        }

        void IGameplayMotionProgressPresentationExtension.ObserveMotionProgress(
            IReadOnlyList<MotionTrackProgressSample> progressSamples)
        {
            ObserveMotionProgress(progressSamples);
        }

        internal void ObserveMotionProgress(IReadOnlyList<MotionTrackProgressSample> progressSamples)
        {
            if (progressSamples == null || progressSamples.Count == 0 || _pendingReveals.Count == 0)
            {
                return;
            }

            for (var pendingIndex = _pendingReveals.Count - 1; pendingIndex >= 0; pendingIndex--)
            {
                var pending = _pendingReveals[pendingIndex];
                if (!pending.UsesMotionProgress)
                {
                    continue;
                }

                for (var sampleIndex = 0; sampleIndex < progressSamples.Count; sampleIndex++)
                {
                    var sample = progressSamples[sampleIndex];
                    if (!pending.Matches(sample) ||
                        sample.PreviousNormalizedTime >= pending.RevealNormalizedTime ||
                        sample.CurrentNormalizedTime < pending.RevealNormalizedTime)
                    {
                        continue;
                    }

                    RevealPending(pendingIndex);
                    break;
                }
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
                _counterView.ShowCountWithIncrementEffect(_counterState.VisibleCount);
            }
            else
            {
                _counterView.ShowCount(_counterState.VisibleCount);
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
                    _counterView.ShowCount(_counterState.VisibleCount);
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

        private void ScheduleReveal(
            in TickPlayerActionPresentationSignal signal,
            int tickIndex,
            int count)
        {
            if (signal.ActiveActionKind == PlayerActionKind.Push)
            {
                _pendingReveals.Add(PendingActionCountReveal.CreateTimed(count, pushRevealDelaySeconds));
                return;
            }

            if (signal.ActiveActionKind != PlayerActionKind.Flip)
            {
                return;
            }

            var targetBoxEntityId = signal.FlipTargetBoxEntityId > 0
                ? signal.FlipTargetBoxEntityId
                : signal.TargetEntityId;
            if (targetBoxEntityId <= 0 || signal.ActionPlanId <= 0)
            {
                _pendingReveals.Add(PendingActionCountReveal.CreateTimed(count, pushRevealDelaySeconds));
                return;
            }

            ResolveFlipReveal(signal.FlipOutcome, out var progressSource, out var contactNormalizedTime);
            _pendingReveals.Add(PendingActionCountReveal.CreateMotionProgress(
                count,
                tickIndex,
                targetBoxEntityId,
                signal.ActionPlanId,
                progressSource,
                Mathf.Max(0f, contactNormalizedTime - flipPreContactLeadNormalized)));
        }

        private static void ResolveFlipReveal(
            TickPlayerFlipOutcomeKind outcome,
            out MotionTrackProgressSourceKind progressSource,
            out float contactNormalizedTime)
        {
            switch (outcome)
            {
                case TickPlayerFlipOutcomeKind.Stay:
                    progressSource = MotionTrackProgressSourceKind.OriginalViewMotion;
                    contactNormalizedTime = GameplayPresentationTimingConstants.FlipImpactInteractionOnsetNormalizedTime;
                    return;
                case TickPlayerFlipOutcomeKind.DestroySelf:
                    progressSource = MotionTrackProgressSourceKind.FlipInteraction;
                    contactNormalizedTime = GameplayPresentationTimingConstants.FlipImpactInteractionOnsetNormalizedTime;
                    return;
                default:
                    progressSource = MotionTrackProgressSourceKind.LocalMotion;
                    contactNormalizedTime = GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime;
                    return;
            }
        }

        private void AdvanceTimedReveals(float deltaTime)
        {
            if (deltaTime <= 0f || _pendingReveals.Count == 0)
            {
                return;
            }

            for (var i = _pendingReveals.Count - 1; i >= 0; i--)
            {
                var pending = _pendingReveals[i];
                if (pending.UsesMotionProgress)
                {
                    continue;
                }

                pending.RemainingDelaySeconds -= deltaTime;
                if (pending.RemainingDelaySeconds <= 0f)
                {
                    RevealPending(i);
                }
            }
        }

        private void RevealPending(int index)
        {
            var pending = _pendingReveals[index];
            _pendingReveals.RemoveAt(index);
            if (_counterState.RevealCount(pending.Count))
            {
                RefreshVisibleView(playIncrementEffect: true);
            }
        }

        private sealed class PendingActionCountReveal
        {
            private PendingActionCountReveal(
                int count,
                float remainingDelaySeconds,
                bool usesMotionProgress,
                int tickIndex,
                int boxEntityId,
                int actionPlanId,
                MotionTrackProgressSourceKind progressSource,
                float revealNormalizedTime)
            {
                Count = count;
                RemainingDelaySeconds = Mathf.Max(0f, remainingDelaySeconds);
                UsesMotionProgress = usesMotionProgress;
                TickIndex = Math.Max(0, tickIndex);
                BoxEntityId = boxEntityId;
                ActionPlanId = actionPlanId;
                ProgressSource = progressSource;
                RevealNormalizedTime = Mathf.Clamp01(revealNormalizedTime);
            }

            public int Count { get; }

            public float RemainingDelaySeconds { get; set; }

            public bool UsesMotionProgress { get; }

            public int TickIndex { get; }

            public int BoxEntityId { get; }

            public int ActionPlanId { get; }

            public MotionTrackProgressSourceKind ProgressSource { get; }

            public float RevealNormalizedTime { get; }

            public static PendingActionCountReveal CreateTimed(int count, float delaySeconds)
            {
                return new PendingActionCountReveal(
                    count,
                    delaySeconds,
                    usesMotionProgress: false,
                    tickIndex: 0,
                    boxEntityId: 0,
                    actionPlanId: 0,
                    MotionTrackProgressSourceKind.LocalMotion,
                    revealNormalizedTime: 0f);
            }

            public static PendingActionCountReveal CreateMotionProgress(
                int count,
                int tickIndex,
                int boxEntityId,
                int actionPlanId,
                MotionTrackProgressSourceKind progressSource,
                float revealNormalizedTime)
            {
                return new PendingActionCountReveal(
                    count,
                    remainingDelaySeconds: 0f,
                    usesMotionProgress: true,
                    tickIndex,
                    boxEntityId,
                    actionPlanId,
                    progressSource,
                    revealNormalizedTime);
            }

            public bool Matches(in MotionTrackProgressSample sample)
            {
                return sample.IsValid &&
                       sample.MotionKind == TickEntityMotionKind.Flip &&
                       sample.TickIndex == TickIndex &&
                       sample.EntityId == BoxEntityId &&
                       sample.SequenceOrActionPlanId == ActionPlanId &&
                       sample.SourceKind == ProgressSource;
            }
        }
    }
}
