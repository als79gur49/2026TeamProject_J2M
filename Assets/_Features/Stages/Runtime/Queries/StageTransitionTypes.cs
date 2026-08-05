using System;
using System.Collections.Generic;

namespace Game.Feature.Stages
{
    public enum StageTransitionKind
    {
        Unknown = 0,
        MainToGameplay = 1,
        GameplayToMain = 2,
        StageClearNext = 3,
        StageRetryManual = 4,
        DeathRetryChanceLost = 5,
        LevelFailedRestart = 6,
    }

    public enum TransitionOverlayKind
    {
        None = 0,
        GameplayEntry = 1,
        ChanceLost = 2,
        StageClear = 3,
        Restart = 4,
        MainMenuReturn = 5,
    }

    [Serializable]
    public sealed class StageTransitionProfile
    {
        public StageTransitionProfile(
            StageTransitionKind kind,
            string fromSceneName,
            string toSceneName,
            float minimumVisibleSeconds,
            bool holdSceneActivationUntilMinimumElapsed,
            bool blockInput,
            bool showProgress,
            TransitionOverlayKind overlayKind,
            float preOverlayDelaySeconds = 0f,
            bool blockInputDuringPreOverlayDelay = false,
            bool startAsyncLoadBeforeOverlay = false,
            bool requiresExplicitContentCompletion = false,
            bool requiresOpaqueTakeover = false)
        {
            Kind = kind;
            FromSceneName = Normalize(fromSceneName);
            ToSceneName = Normalize(toSceneName);
            MinimumVisibleSeconds = Math.Max(0f, minimumVisibleSeconds);
            HoldSceneActivationUntilMinimumElapsed = holdSceneActivationUntilMinimumElapsed;
            BlockInput = blockInput;
            ShowProgress = showProgress;
            OverlayKind = overlayKind;
            PreOverlayDelaySeconds = Math.Max(0f, preOverlayDelaySeconds);
            BlockInputDuringPreOverlayDelay = blockInputDuringPreOverlayDelay;
            StartAsyncLoadBeforeOverlay = startAsyncLoadBeforeOverlay;
            RequiresExplicitContentCompletion = requiresExplicitContentCompletion;
            RequiresOpaqueTakeover = requiresOpaqueTakeover;
        }

        public StageTransitionKind Kind { get; }

        public string FromSceneName { get; }

        public string ToSceneName { get; }

        public float MinimumVisibleSeconds { get; }

        public bool HoldSceneActivationUntilMinimumElapsed { get; }

        public bool BlockInput { get; }

        public bool ShowProgress { get; }

        public TransitionOverlayKind OverlayKind { get; }

        public float PreOverlayDelaySeconds { get; }

        public bool BlockInputDuringPreOverlayDelay { get; }

        public bool StartAsyncLoadBeforeOverlay { get; }

        public bool RequiresExplicitContentCompletion { get; }

        public bool RequiresOpaqueTakeover { get; }

        public StageTransitionProfile WithMinimumVisibleSeconds(float minimumVisibleSeconds)
        {
            return new StageTransitionProfile(
                Kind,
                FromSceneName,
                ToSceneName,
                minimumVisibleSeconds,
                HoldSceneActivationUntilMinimumElapsed,
                BlockInput,
                ShowProgress,
                OverlayKind,
                PreOverlayDelaySeconds,
                BlockInputDuringPreOverlayDelay,
                StartAsyncLoadBeforeOverlay,
                RequiresExplicitContentCompletion,
                RequiresOpaqueTakeover);
        }

        internal static string Normalize(string sceneName)
        {
            return (sceneName ?? string.Empty).Trim();
        }
    }

    public readonly struct StageTransitionChanceLostPayload
    {
        public StageTransitionChanceLostPayload(
            int previousRemainingChances,
            int currentRemainingChances,
            int totalChances,
            StageId currentStageId,
            StageId retryStageId,
            int deathCount,
            string source)
        {
            PreviousRemainingChances = Math.Max(0, previousRemainingChances);
            CurrentRemainingChances = Math.Max(0, currentRemainingChances);
            TotalChances = Math.Max(0, totalChances);
            CurrentStageId = currentStageId;
            RetryStageId = retryStageId;
            DeathCount = Math.Max(0, deathCount);
            Source = source ?? string.Empty;
        }

        public int PreviousRemainingChances { get; }

        public int CurrentRemainingChances { get; }

        public int TotalChances { get; }

        public StageId CurrentStageId { get; }

        public StageId RetryStageId { get; }

        public int DeathCount { get; }

        public string Source { get; }
    }

    public readonly struct StageTransitionHint
    {
        private StageTransitionHint(
            StageTransitionKind kind,
            bool hasChanceLostPayload,
            StageTransitionChanceLostPayload chanceLostPayload,
            bool hasMinimumVisibleSecondsOverride,
            float minimumVisibleSecondsOverride,
            TerminalSessionToken terminalToken)
        {
            Kind = kind;
            HasChanceLostPayload = hasChanceLostPayload;
            ChanceLostPayload = chanceLostPayload;
            HasMinimumVisibleSecondsOverride = hasMinimumVisibleSecondsOverride;
            MinimumVisibleSecondsOverride = Math.Max(0f, minimumVisibleSecondsOverride);
            TerminalToken = terminalToken;
        }

        public StageTransitionKind Kind { get; }

        public bool HasExplicitKind => Kind != StageTransitionKind.Unknown;

        public bool HasChanceLostPayload { get; }

        public StageTransitionChanceLostPayload ChanceLostPayload { get; }

        public bool HasMinimumVisibleSecondsOverride { get; }

        public float MinimumVisibleSecondsOverride { get; }

        public TerminalSessionToken TerminalToken { get; }

        public long TerminalClaimId => TerminalToken.Sequence;

        public bool HasTerminalClaim => TerminalToken.IsValid;

        public static StageTransitionHint ForKind(StageTransitionKind kind)
        {
            return new StageTransitionHint(kind, false, default, false, 0f, default);
        }

        public static StageTransitionHint ForKindWithMinimum(
            StageTransitionKind kind,
            float minimumVisibleSeconds)
        {
            return new StageTransitionHint(kind, false, default, true, minimumVisibleSeconds, default);
        }

        public static StageTransitionHint ForChanceLost(
            StageTransitionChanceLostPayload payload,
            float minimumVisibleSecondsOverride = -1f)
        {
            return new StageTransitionHint(
                StageTransitionKind.DeathRetryChanceLost,
                true,
                payload,
                minimumVisibleSecondsOverride >= 0f,
                minimumVisibleSecondsOverride,
                default);
        }

        public StageTransitionHint WithTerminalClaim(TerminalSessionToken terminalToken)
        {
            if (!terminalToken.IsValid)
            {
                throw new ArgumentOutOfRangeException(nameof(terminalToken));
            }

            return new StageTransitionHint(
                Kind,
                HasChanceLostPayload,
                ChanceLostPayload,
                HasMinimumVisibleSecondsOverride,
                MinimumVisibleSecondsOverride,
                terminalToken);
        }

    }

    public sealed class StageTransitionProfileResolver
    {
        private readonly Dictionary<StageTransitionKind, StageTransitionProfile> _profilesByKind = new();

        public StageTransitionProfileResolver(IEnumerable<StageTransitionProfile> profiles = null)
        {
            AddCanonicalAuthoredProfiles();
            if (profiles == null)
            {
                return;
            }

            foreach (var profile in profiles)
            {
                AddProfile(profile);
            }
        }

        public StageTransitionProfile Resolve(
            StageNavigationRequest request,
            string fromSceneName,
            string toSceneName)
        {
            var routePolicy =
                SceneTransitionRoutePolicyCatalog.ResolveProduction(request.TransitionIntent);
            return Resolve(routePolicy, request, fromSceneName, toSceneName);
        }

        public StageTransitionProfile Resolve(
            SceneTransitionRoutePolicy routePolicy,
            StageNavigationRequest request,
            string fromSceneName,
            string toSceneName)
        {
            if (routePolicy.Classification != SceneTransitionRouteClassification.Production ||
                routePolicy.Intent != request.TransitionIntent)
            {
                throw new InvalidOperationException(
                    $"Scene transition request intent '{request.TransitionIntent}' does not match resolved production policy '{routePolicy.Intent}'.");
            }

            var transitionKind = request.TransitionHint.HasExplicitKind
                ? request.TransitionHint.Kind
                : routePolicy.PrimaryTransitionKind;
            if (!routePolicy.AllowsTransitionKind(transitionKind))
            {
                throw new InvalidOperationException(
                    $"Transition profile '{transitionKind}' is not registered for route intent '{routePolicy.Intent}'.");
            }

            if (!_profilesByKind.TryGetValue(transitionKind, out var profile))
            {
                throw new InvalidOperationException(
                    $"Transition profile '{transitionKind}' required by route intent '{routePolicy.Intent}' is not configured.");
            }

            return ApplyOverrides(profile, request.TransitionHint);
        }

        private void AddCanonicalAuthoredProfiles()
        {
            AddProfile(new StageTransitionProfile(
                StageTransitionKind.MainToGameplay,
                string.Empty,
                string.Empty,
                0.35f,
                true,
                true,
                true,
                TransitionOverlayKind.GameplayEntry));
            AddProfile(new StageTransitionProfile(
                StageTransitionKind.GameplayToMain,
                string.Empty,
                string.Empty,
                0.25f,
                true,
                true,
                true,
                TransitionOverlayKind.MainMenuReturn));
            AddProfile(new StageTransitionProfile(
                StageTransitionKind.StageClearNext,
                string.Empty,
                string.Empty,
                0.25f,
                true,
                true,
                true,
                TransitionOverlayKind.StageClear,
                preOverlayDelaySeconds: 0.15f));
            AddProfile(new StageTransitionProfile(
                StageTransitionKind.StageRetryManual,
                string.Empty,
                string.Empty,
                0.35f,
                true,
                true,
                true,
                TransitionOverlayKind.Restart));
            AddProfile(new StageTransitionProfile(
                StageTransitionKind.DeathRetryChanceLost,
                string.Empty,
                string.Empty,
                0f,
                false,
                true,
                true,
                TransitionOverlayKind.ChanceLost,
                preOverlayDelaySeconds: 0f,
                blockInputDuringPreOverlayDelay: false,
                startAsyncLoadBeforeOverlay: true,
                requiresExplicitContentCompletion: true,
                requiresOpaqueTakeover: true));
            AddProfile(new StageTransitionProfile(
                StageTransitionKind.LevelFailedRestart,
                string.Empty,
                string.Empty,
                0.35f,
                true,
                true,
                true,
                TransitionOverlayKind.Restart,
                preOverlayDelaySeconds: 1.0f));
        }

        private void AddProfile(StageTransitionProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            if (profile.Kind != StageTransitionKind.Unknown)
            {
                _profilesByKind[profile.Kind] = profile;
            }

        }

        private static StageTransitionProfile ApplyOverrides(
            StageTransitionProfile profile,
            StageTransitionHint hint)
        {
            return hint.HasMinimumVisibleSecondsOverride
                ? profile.WithMinimumVisibleSeconds(hint.MinimumVisibleSecondsOverride)
                : profile;
        }

    }

    public sealed class StageTransitionLaunchGuard
    {
        private int _nextTransitionId;

        public bool IsTransitionInProgress { get; private set; }

        public int CurrentTransitionId { get; private set; }

        public bool TryBegin(out int transitionId)
        {
            if (IsTransitionInProgress)
            {
                transitionId = CurrentTransitionId;
                return false;
            }

            IsTransitionInProgress = true;
            CurrentTransitionId = ++_nextTransitionId;
            transitionId = CurrentTransitionId;
            return true;
        }

        public void Complete(int transitionId)
        {
            if (!IsTransitionInProgress || transitionId != CurrentTransitionId)
            {
                return;
            }

            IsTransitionInProgress = false;
            CurrentTransitionId = 0;
        }
    }
}
