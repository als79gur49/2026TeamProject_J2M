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
        GenericLoading = 1,
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
            TransitionOverlayKind overlayKind)
        {
            Kind = kind;
            FromSceneName = Normalize(fromSceneName);
            ToSceneName = Normalize(toSceneName);
            MinimumVisibleSeconds = Math.Max(0f, minimumVisibleSeconds);
            HoldSceneActivationUntilMinimumElapsed = holdSceneActivationUntilMinimumElapsed;
            BlockInput = blockInput;
            ShowProgress = showProgress;
            OverlayKind = overlayKind;
        }

        public StageTransitionKind Kind { get; }

        public string FromSceneName { get; }

        public string ToSceneName { get; }

        public float MinimumVisibleSeconds { get; }

        public bool HoldSceneActivationUntilMinimumElapsed { get; }

        public bool BlockInput { get; }

        public bool ShowProgress { get; }

        public TransitionOverlayKind OverlayKind { get; }

        public static StageTransitionProfile Default { get; } = new(
            StageTransitionKind.Unknown,
            string.Empty,
            string.Empty,
            0.35f,
            holdSceneActivationUntilMinimumElapsed: true,
            blockInput: true,
            showProgress: true,
            TransitionOverlayKind.GenericLoading);

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
                OverlayKind);
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
            string source,
            string title,
            string message)
        {
            PreviousRemainingChances = Math.Max(0, previousRemainingChances);
            CurrentRemainingChances = Math.Max(0, currentRemainingChances);
            TotalChances = Math.Max(0, totalChances);
            CurrentStageId = currentStageId;
            RetryStageId = retryStageId;
            DeathCount = Math.Max(0, deathCount);
            Source = source ?? string.Empty;
            Title = title ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public int PreviousRemainingChances { get; }

        public int CurrentRemainingChances { get; }

        public int TotalChances { get; }

        public StageId CurrentStageId { get; }

        public StageId RetryStageId { get; }

        public int DeathCount { get; }

        public string Source { get; }

        public string Title { get; }

        public string Message { get; }
    }

    public readonly struct StageTransitionHint
    {
        private StageTransitionHint(
            StageTransitionKind kind,
            bool hasChanceLostPayload,
            StageTransitionChanceLostPayload chanceLostPayload,
            bool hasMinimumVisibleSecondsOverride,
            float minimumVisibleSecondsOverride)
        {
            Kind = kind;
            HasChanceLostPayload = hasChanceLostPayload;
            ChanceLostPayload = chanceLostPayload;
            HasMinimumVisibleSecondsOverride = hasMinimumVisibleSecondsOverride;
            MinimumVisibleSecondsOverride = Math.Max(0f, minimumVisibleSecondsOverride);
        }

        public StageTransitionKind Kind { get; }

        public bool HasExplicitKind => Kind != StageTransitionKind.Unknown;

        public bool HasChanceLostPayload { get; }

        public StageTransitionChanceLostPayload ChanceLostPayload { get; }

        public bool HasMinimumVisibleSecondsOverride { get; }

        public float MinimumVisibleSecondsOverride { get; }

        public static StageTransitionHint ForKind(StageTransitionKind kind)
        {
            return new StageTransitionHint(kind, false, default, false, 0f);
        }

        public static StageTransitionHint ForKindWithMinimum(
            StageTransitionKind kind,
            float minimumVisibleSeconds)
        {
            return new StageTransitionHint(kind, false, default, true, minimumVisibleSeconds);
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
                minimumVisibleSecondsOverride);
        }
    }

    public sealed class StageTransitionProfileResolver
    {
        private readonly Dictionary<StageTransitionKind, StageTransitionProfile> _profilesByKind = new();
        private readonly Dictionary<string, StageTransitionProfile> _profilesByScenePair = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, StageTransitionProfile> _profilesByToScene = new(StringComparer.OrdinalIgnoreCase);
        private readonly StageTransitionProfile _defaultProfile;

        public StageTransitionProfileResolver(
            IEnumerable<StageTransitionProfile> profiles = null,
            StageTransitionProfile defaultProfile = null)
        {
            _defaultProfile = defaultProfile ?? StageTransitionProfile.Default;
            AddBuiltInDefaults();
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
            if (request.TransitionHint.HasExplicitKind &&
                TryGetKindProfile(request.TransitionHint.Kind, out var explicitProfile))
            {
                return ApplyOverrides(explicitProfile, request.TransitionHint);
            }

            var sourceKind = ResolveKindFromSource(request.Source, request.NavigationKind);
            if (sourceKind != StageTransitionKind.Unknown &&
                TryGetKindProfile(sourceKind, out var sourceProfile))
            {
                return ApplyOverrides(sourceProfile, request.TransitionHint);
            }

            var pairKey = BuildScenePairKey(fromSceneName, toSceneName);
            if (_profilesByScenePair.TryGetValue(pairKey, out var pairProfile))
            {
                return ApplyOverrides(pairProfile, request.TransitionHint);
            }

            var normalizedToScene = StageTransitionProfile.Normalize(toSceneName);
            if (!string.IsNullOrWhiteSpace(normalizedToScene) &&
                _profilesByToScene.TryGetValue(normalizedToScene, out var toSceneProfile))
            {
                return ApplyOverrides(toSceneProfile, request.TransitionHint);
            }

            return ApplyOverrides(_defaultProfile, request.TransitionHint);
        }

        public static StageTransitionKind ResolveKindFromSource(
            string source,
            StageNavigationKind navigationKind)
        {
            var normalized = (source ?? string.Empty).Trim();
            if (StartsWith(normalized, "campaign-death-retry"))
            {
                return StageTransitionKind.DeathRetryChanceLost;
            }

            if (StartsWith(normalized, "level-failed-restart-level"))
            {
                return StageTransitionKind.LevelFailedRestart;
            }

            if (StartsWith(normalized, "campaign-auto-next") ||
                StartsWith(normalized, "stage-result-continue"))
            {
                return StageTransitionKind.StageClearNext;
            }

            if (StartsWith(normalized, "stage-result-retry"))
            {
                return StageTransitionKind.StageRetryManual;
            }

            if (StartsWith(normalized, "main-menu-continue") ||
                StartsWith(normalized, "main-menu-new-game"))
            {
                return StageTransitionKind.MainToGameplay;
            }

            if (navigationKind == StageNavigationKind.NextStage)
            {
                return StageTransitionKind.StageClearNext;
            }

            return StageTransitionKind.Unknown;
        }

        private void AddBuiltInDefaults()
        {
            AddProfile(new StageTransitionProfile(
                StageTransitionKind.MainToGameplay,
                string.Empty,
                string.Empty,
                0.35f,
                true,
                true,
                true,
                TransitionOverlayKind.GenericLoading));
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
                0.45f,
                true,
                true,
                true,
                TransitionOverlayKind.StageClear));
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
                0.75f,
                true,
                true,
                true,
                TransitionOverlayKind.ChanceLost));
            AddProfile(new StageTransitionProfile(
                StageTransitionKind.LevelFailedRestart,
                string.Empty,
                string.Empty,
                0.35f,
                true,
                true,
                true,
                TransitionOverlayKind.Restart));
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

            if (!string.IsNullOrWhiteSpace(profile.FromSceneName) &&
                !string.IsNullOrWhiteSpace(profile.ToSceneName))
            {
                _profilesByScenePair[BuildScenePairKey(profile.FromSceneName, profile.ToSceneName)] = profile;
            }

            if (!string.IsNullOrWhiteSpace(profile.ToSceneName))
            {
                _profilesByToScene[profile.ToSceneName] = profile;
            }
        }

        private bool TryGetKindProfile(StageTransitionKind kind, out StageTransitionProfile profile)
        {
            if (_profilesByKind.TryGetValue(kind, out profile))
            {
                return true;
            }

            profile = _defaultProfile;
            return false;
        }

        private static StageTransitionProfile ApplyOverrides(
            StageTransitionProfile profile,
            StageTransitionHint hint)
        {
            return hint.HasMinimumVisibleSecondsOverride
                ? profile.WithMinimumVisibleSeconds(hint.MinimumVisibleSecondsOverride)
                : profile;
        }

        private static string BuildScenePairKey(string fromSceneName, string toSceneName)
        {
            return $"{StageTransitionProfile.Normalize(fromSceneName)}->{StageTransitionProfile.Normalize(toSceneName)}";
        }

        private static bool StartsWith(string value, string prefix)
        {
            return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
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
