using System;
using System.Collections.Generic;
using Game.Feature.Stages;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    internal enum GameplayEntrySourceCloseVisualKind
    {
        ExistingStageAdvanceCover = 0,
        ExistingDefeatIris = 1,
        RetryIris = 2,
        MainMenuIris = 3,
        CinematicOpaqueOwner = 4,
    }

    internal enum GameplayEntryFocusPolicy
    {
        PlayerThenAuthoredThenScreenCenter = 0,
        StrongPlayerProjection = 1,
        AuthoredThenScreenCenter = 2,
    }

    internal enum GameplayEntryInputReleasePolicy
    {
        OpeningCompleted = 0,
    }

    internal readonly struct GameplayEntryTransitionVisualSnapshot
    {
        internal GameplayEntryTransitionVisualSnapshot(
            SceneTransitionIntent intent,
            GameplayEntrySourceCloseVisualKind sourceCloseVisualKind,
            Color sourceCloseColor,
            Color holdColor,
            Color destinationOpenColor,
            GameplayEntryFocusPolicy sourceFocusPolicy,
            GameplayEntryFocusPolicy destinationFocusPolicy,
            bool requireSourceOpaqueRenderAcknowledgement,
            bool requireDestinationRenderAcknowledgement,
            GameplayEntryInputReleasePolicy inputReleasePolicy)
        {
            Intent = intent;
            SourceCloseVisualKind = sourceCloseVisualKind;
            SourceCloseColor = RequireOpaque(sourceCloseColor, nameof(sourceCloseColor));
            HoldColor = RequireOpaque(holdColor, nameof(holdColor));
            DestinationOpenColor = RequireOpaque(destinationOpenColor, nameof(destinationOpenColor));
            SourceFocusPolicy = sourceFocusPolicy;
            DestinationFocusPolicy = destinationFocusPolicy;
            RequireSourceOpaqueRenderAcknowledgement =
                requireSourceOpaqueRenderAcknowledgement;
            RequireDestinationRenderAcknowledgement =
                requireDestinationRenderAcknowledgement;
            InputReleasePolicy = inputReleasePolicy;
        }

        internal SceneTransitionIntent Intent { get; }
        internal GameplayEntrySourceCloseVisualKind SourceCloseVisualKind { get; }
        internal Color SourceCloseColor { get; }
        internal Color HoldColor { get; }
        internal Color DestinationOpenColor { get; }
        internal GameplayEntryFocusPolicy SourceFocusPolicy { get; }
        internal GameplayEntryFocusPolicy DestinationFocusPolicy { get; }
        internal bool RequireSourceOpaqueRenderAcknowledgement { get; }
        internal bool RequireDestinationRenderAcknowledgement { get; }
        internal GameplayEntryInputReleasePolicy InputReleasePolicy { get; }

        private static Color RequireOpaque(Color color, string parameterName)
        {
            if (!IsFinite(color.r) ||
                !IsFinite(color.g) ||
                !IsFinite(color.b) ||
                !IsFinite(color.a))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            color.a = 1f;
            return color;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    [CreateAssetMenu(
        fileName = "TerminalIrisMotionProfile",
        menuName = "Game/UI/Terminal Iris Motion Profile")]
    public sealed class TerminalIrisMotionProfile : ScriptableObject
    {
        [Serializable]
        public sealed class EdgeSettings
        {
            [SerializeField] private float _edgeAntiAliasScale;
            [SerializeField] private float _minimumAAPixels;
            [SerializeField] private float _artisticFeatherHalfWidthPixels;
            [SerializeField] private float _rimWidthPixels;
            [SerializeField] private float _rimSoftnessPixels;
            [SerializeField] private float _rimFadeOutPixels;
            [SerializeField] private Color _rimColor;

            internal TerminalIrisRuntimeEdgeSettings CreateRuntimeSettings()
            {
                return new TerminalIrisRuntimeEdgeSettings(
                    _edgeAntiAliasScale,
                    _minimumAAPixels,
                    _artisticFeatherHalfWidthPixels,
                    _rimWidthPixels,
                    _rimSoftnessPixels,
                    _rimFadeOutPixels,
                    _rimColor);
            }
        }

        [Serializable]
        public sealed class ClosePreset
        {
            [SerializeField] private float _focusDuration;
            [SerializeField] private float _holdDuration;
            [SerializeField] private float _closeDuration;
            [SerializeField] private Vector2 _fallbackCenter;
            [SerializeField] private float _fallbackRadius;
            [SerializeField] private float _minimumFocusRadius;
            [SerializeField] private float _focusPadding;
            [SerializeField] private float _finalClosedOvershootPixels;
            [SerializeField] private EdgeSettings _edge;
            [SerializeField] private TerminalIrisEasing _focusEasing;
            [SerializeField] private TerminalIrisEasing _closeEasing;

            internal TerminalIrisRuntimePreset CreateRuntimePreset(
                string presetName,
                TerminalIrisRuntimeOpenPreset? revealPreset)
            {
                try
                {
                    return new TerminalIrisRuntimePreset(
                        _focusDuration,
                        _holdDuration,
                        _closeDuration,
                        _fallbackCenter,
                        _fallbackRadius,
                        _minimumFocusRadius,
                        _focusPadding,
                        _finalClosedOvershootPixels,
                        RequireEdgeSettings(),
                        _focusEasing,
                        _closeEasing,
                        revealPreset);
                }
                catch (Exception exception) when (
                    exception is ArgumentException ||
                    exception is InvalidOperationException)
                {
                    throw new InvalidOperationException(
                        $"TerminalIrisMotionProfile.{presetName} is invalid: {exception.Message}",
                        exception);
                }
            }

            private TerminalIrisRuntimeEdgeSettings RequireEdgeSettings()
            {
                if (_edge == null)
                {
                    throw new InvalidOperationException(
                        "Terminal Iris close Edge settings are required.");
                }

                return _edge.CreateRuntimeSettings();
            }
        }

        [Serializable]
        public sealed class OpenPreset
        {
            [SerializeField] private float _preOpenHoldDuration;
            [SerializeField] private float _openingDuration;
            [SerializeField] private float _fullOpenMargin;
            [SerializeField] private float _finalClosedOvershootPixels;
            [SerializeField] private EdgeSettings _edge;
            [SerializeField] private TerminalIrisEasing _openingEasing;

            internal TerminalIrisRuntimeOpenPreset CreateRuntimePreset(string presetName)
            {
                try
                {
                    return new TerminalIrisRuntimeOpenPreset(
                        _preOpenHoldDuration,
                        _openingDuration,
                        _fullOpenMargin,
                        _finalClosedOvershootPixels,
                        RequireEdgeSettings(),
                        _openingEasing);
                }
                catch (Exception exception) when (
                    exception is ArgumentException ||
                    exception is InvalidOperationException)
                {
                    throw new InvalidOperationException(
                        $"TerminalIrisMotionProfile.{presetName} is invalid: {exception.Message}",
                        exception);
                }
            }

            private TerminalIrisRuntimeEdgeSettings RequireEdgeSettings()
            {
                if (_edge == null)
                {
                    throw new InvalidOperationException(
                        "Terminal Iris open Edge settings are required.");
                }

                return _edge.CreateRuntimeSettings();
            }
        }

        [SerializeField] private ClosePreset _victoryClose;
        [SerializeField] private ClosePreset _defeatClose;
        [SerializeField] private ClosePreset _retryClose;
        [SerializeField] private ClosePreset _gameplayEntryClose;
        [SerializeField] private OpenPreset _defeatReveal;
        [SerializeField] private OpenPreset _stageEntryOpen;
        [SerializeField] private Color _retryTransitionColor = Color.black;
        [SerializeField] private Color _gameplayEntryTransitionColor =
            new Color(0f, 0.25133762f, 0.4811321f, 1f);

        internal TerminalIrisMotionProfileResolver CreateResolver()
        {
            return new TerminalIrisMotionProfileResolver(this);
        }

        internal void ValidateOrThrow()
        {
            RequirePreset(_victoryClose, nameof(VictoryClose));
            RequirePreset(_defeatClose, nameof(DefeatClose));
            RequirePreset(_retryClose, nameof(RetryClose));
            RequirePreset(_gameplayEntryClose, nameof(GameplayEntryClose));
            RequirePreset(_defeatReveal, nameof(DefeatReveal));
            RequirePreset(_stageEntryOpen, nameof(StageEntryOpen));

            var defeatReveal = _defeatReveal.CreateRuntimePreset(nameof(DefeatReveal));
            _victoryClose.CreateRuntimePreset(nameof(VictoryClose), revealPreset: null);
            _defeatClose.CreateRuntimePreset(nameof(DefeatClose), defeatReveal);
            _retryClose.CreateRuntimePreset(nameof(RetryClose), revealPreset: null);
            _gameplayEntryClose.CreateRuntimePreset(
                nameof(GameplayEntryClose),
                revealPreset: null);
            _stageEntryOpen.CreateRuntimePreset(nameof(StageEntryOpen));
        }

        public ClosePreset VictoryClose => _victoryClose;

        public ClosePreset DefeatClose => _defeatClose;

        public ClosePreset RetryClose => _retryClose;

        public ClosePreset GameplayEntryClose => _gameplayEntryClose;

        public OpenPreset DefeatReveal => _defeatReveal;

        public OpenPreset StageEntryOpen => _stageEntryOpen;

        internal GameplayEntryTransitionVisualSnapshot CreateRetryTransitionVisualSnapshot(
            SceneTransitionIntent intent)
        {
            var sourceKind = intent switch
            {
                SceneTransitionIntent.DeathRetry =>
                    GameplayEntrySourceCloseVisualKind.ExistingDefeatIris,
                SceneTransitionIntent.ManualRetry =>
                    GameplayEntrySourceCloseVisualKind.RetryIris,
                SceneTransitionIntent.DemoStageRelaunch =>
                    GameplayEntrySourceCloseVisualKind.RetryIris,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(intent),
                    intent,
                    "Retry transition style only owns M2 Retry intents."),
            };
            return new GameplayEntryTransitionVisualSnapshot(
                intent,
                sourceKind,
                _retryTransitionColor,
                _retryTransitionColor,
                _retryTransitionColor,
                GameplayEntryFocusPolicy.PlayerThenAuthoredThenScreenCenter,
                GameplayEntryFocusPolicy.StrongPlayerProjection,
                requireSourceOpaqueRenderAcknowledgement: true,
                requireDestinationRenderAcknowledgement: true,
                GameplayEntryInputReleasePolicy.OpeningCompleted);
        }

        internal GameplayEntryTransitionVisualSnapshot CreateGameplayEntryTransitionVisualSnapshot()
        {
            return new GameplayEntryTransitionVisualSnapshot(
                SceneTransitionIntent.GameplayEntry,
                GameplayEntrySourceCloseVisualKind.MainMenuIris,
                _gameplayEntryTransitionColor,
                _gameplayEntryTransitionColor,
                _gameplayEntryTransitionColor,
                GameplayEntryFocusPolicy.AuthoredThenScreenCenter,
                GameplayEntryFocusPolicy.StrongPlayerProjection,
                requireSourceOpaqueRenderAcknowledgement: true,
                requireDestinationRenderAcknowledgement: true,
                GameplayEntryInputReleasePolicy.OpeningCompleted);
        }

        internal static GameplayEntryTransitionVisualSnapshot CreateCinematicTransitionVisualSnapshot(
            Color authoredBlack)
        {
            return new GameplayEntryTransitionVisualSnapshot(
                SceneTransitionIntent.CinematicToGameplay,
                GameplayEntrySourceCloseVisualKind.CinematicOpaqueOwner,
                authoredBlack,
                authoredBlack,
                authoredBlack,
                GameplayEntryFocusPolicy.AuthoredThenScreenCenter,
                GameplayEntryFocusPolicy.StrongPlayerProjection,
                requireSourceOpaqueRenderAcknowledgement: true,
                requireDestinationRenderAcknowledgement: true,
                GameplayEntryInputReleasePolicy.OpeningCompleted);
        }

        private static void RequirePreset(object preset, string presetName)
        {
            if (preset == null)
            {
                throw new InvalidOperationException(
                    $"TerminalIrisMotionProfile.{presetName} is required.");
            }
        }
    }

    internal static class GameplayEntryTransitionVisualSnapshotRegistry
    {
        private static readonly SceneTransitionIntent[] ProductionIntentKeys =
        {
            SceneTransitionIntent.StageAdvance,
            SceneTransitionIntent.DeathRetry,
            SceneTransitionIntent.ManualRetry,
            SceneTransitionIntent.DemoStageRelaunch,
            SceneTransitionIntent.GameplayEntry,
            SceneTransitionIntent.CinematicToGameplay,
        };
        private const string IrisProfileResourcePath =
            "UI/Transitions/TerminalIrisMotionProfile";
        private static SceneEntrySessionToken _token;
        private static GameplayEntryTransitionVisualSnapshot? _snapshot;

        internal static IReadOnlyList<SceneTransitionIntent> SupportedProductionIntents =>
            Array.AsReadOnly(ProductionIntentKeys);

        internal static GameplayEntryTransitionVisualSnapshot Capture(
            SceneEntrySessionToken token,
            SceneTransitionRoutePolicy routePolicy,
            Color? stageAdvanceColor = null,
            Color? cinematicColor = null)
        {
            if (!token.IsValid ||
                routePolicy.Classification != SceneTransitionRouteClassification.Production ||
                routePolicy.Status != SceneTransitionRouteStatus.Canonical ||
                routePolicy.DestinationKind != SceneTransitionDestinationKind.Gameplay ||
                routePolicy.DestinationExecutorKind !=
                SceneTransitionDestinationExecutorKind.GameplayEntry ||
                Array.IndexOf(ProductionIntentKeys, routePolicy.Intent) < 0 ||
                !routePolicy.ImplementsSceneTransitionSession ||
                !routePolicy.ImplementsDestinationReadiness ||
                !routePolicy.ImplementsDestinationRenderAcknowledgement ||
                !routePolicy.ImplementsInputAdmission)
            {
                throw new InvalidOperationException(
                    $"Gameplay entry visual capture rejected incomplete route {routePolicy.Intent}.");
            }

            if (_snapshot.HasValue)
            {
                if (_token == token && _snapshot.Value.Intent == routePolicy.Intent)
                {
                    return _snapshot.Value;
                }

                throw new InvalidOperationException(
                    $"Gameplay entry visual token {_token} still owns the immutable snapshot.");
            }

            var captured = routePolicy.Intent switch
            {
                SceneTransitionIntent.StageAdvance =>
                    CreateStageAdvanceSnapshot(
                        routePolicy.Intent,
                        stageAdvanceColor ??
                        throw new InvalidOperationException(
                            "StageAdvance visual capture requires its immutable Result color.")),
                SceneTransitionIntent.GameplayEntry =>
                    RequireIrisProfile().CreateGameplayEntryTransitionVisualSnapshot(),
                SceneTransitionIntent.CinematicToGameplay =>
                    TerminalIrisMotionProfile.CreateCinematicTransitionVisualSnapshot(
                        cinematicColor ??
                        throw new InvalidOperationException(
                            "CinematicToGameplay visual capture requires the authored cinematic opaque color.")),
                _ => RequireIrisProfile().CreateRetryTransitionVisualSnapshot(routePolicy.Intent),
            };
            _token = token;
            _snapshot = captured;
            return captured;
        }

        internal static GameplayEntryTransitionVisualSnapshot Require(
            SceneEntrySessionToken token,
            SceneTransitionIntent intent)
        {
            if (!_snapshot.HasValue || _token != token || _snapshot.Value.Intent != intent)
            {
                throw new InvalidOperationException(
                    $"Gameplay entry visual snapshot does not match token {token} / intent {intent}.");
            }

            return _snapshot.Value;
        }

        internal static void Clear(SceneEntrySessionToken token)
        {
            if (_snapshot.HasValue && _token == token)
            {
                _token = default;
                _snapshot = null;
            }
        }

        internal static void ResetForTests()
        {
            _token = default;
            _snapshot = null;
        }

        private static GameplayEntryTransitionVisualSnapshot CreateStageAdvanceSnapshot(
            SceneTransitionIntent intent,
            Color color)
        {
            return new GameplayEntryTransitionVisualSnapshot(
                intent,
                GameplayEntrySourceCloseVisualKind.ExistingStageAdvanceCover,
                color,
                color,
                color,
                GameplayEntryFocusPolicy.PlayerThenAuthoredThenScreenCenter,
                GameplayEntryFocusPolicy.StrongPlayerProjection,
                requireSourceOpaqueRenderAcknowledgement: true,
                requireDestinationRenderAcknowledgement: true,
                GameplayEntryInputReleasePolicy.OpeningCompleted);
        }

        private static TerminalIrisMotionProfile RequireIrisProfile()
        {
            var profile = Resources.Load<TerminalIrisMotionProfile>(
                IrisProfileResourcePath);
            return profile != null
                ? profile
                : throw new InvalidOperationException(
                    $"Terminal Iris profile was not found at Resources/{IrisProfileResourcePath}.");
        }
    }

    internal enum MainMenuSourceCloseVisualKind
    {
        GameplayScreenCenterIris = 0,
        CinematicOpaqueOwner = 1,
    }

    internal readonly struct MainMenuTransitionVisualSnapshot
    {
        internal MainMenuTransitionVisualSnapshot(
            SceneTransitionIntent intent,
            MainMenuSourceCloseVisualKind sourceCloseVisualKind,
            Color sourceCloseColor,
            Color holdColor,
            Color destinationOpenColor)
        {
            Intent = intent;
            SourceCloseVisualKind = sourceCloseVisualKind;
            SourceCloseColor = RequireOpaque(sourceCloseColor);
            HoldColor = RequireOpaque(holdColor);
            DestinationOpenColor = RequireOpaque(destinationOpenColor);
        }

        internal SceneTransitionIntent Intent { get; }
        internal MainMenuSourceCloseVisualKind SourceCloseVisualKind { get; }
        internal Color SourceCloseColor { get; }
        internal Color HoldColor { get; }
        internal Color DestinationOpenColor { get; }

        private static Color RequireOpaque(Color color)
        {
            if (float.IsNaN(color.r) || float.IsInfinity(color.r) ||
                float.IsNaN(color.g) || float.IsInfinity(color.g) ||
                float.IsNaN(color.b) || float.IsInfinity(color.b) ||
                float.IsNaN(color.a) || float.IsInfinity(color.a))
            {
                throw new ArgumentOutOfRangeException(nameof(color));
            }

            color.a = 1f;
            return color;
        }
    }

    internal static class MainMenuTransitionVisualPolicy
    {
        private static readonly SceneTransitionIntent[] ProductionIntentKeys =
        {
            SceneTransitionIntent.ReturnToMainMenu,
            SceneTransitionIntent.CinematicToMainMenu,
        };
        private static readonly Color NeutralBlack = new(0f, 0f, 0f, 1f);
        private static MainMenuEntrySessionToken _token;
        private static MainMenuTransitionVisualSnapshot? _snapshot;

        internal static IReadOnlyList<SceneTransitionIntent> SupportedProductionIntents =>
            Array.AsReadOnly(ProductionIntentKeys);

        internal static MainMenuTransitionVisualSnapshot Capture(
            MainMenuEntrySessionToken token,
            SceneTransitionRoutePolicy routePolicy,
            Color? cinematicOpaqueColor = null)
        {
            if (!token.IsValid ||
                routePolicy.Status != SceneTransitionRouteStatus.Canonical ||
                routePolicy.DestinationKind != SceneTransitionDestinationKind.MainMenu ||
                routePolicy.DestinationExecutorKind !=
                SceneTransitionDestinationExecutorKind.MainMenuEntry ||
                Array.IndexOf(ProductionIntentKeys, routePolicy.Intent) < 0 ||
                !routePolicy.ImplementsSceneTransitionSession ||
                !routePolicy.ImplementsDestinationReadiness ||
                !routePolicy.ImplementsDestinationRenderAcknowledgement ||
                !routePolicy.ImplementsInputAdmission)
            {
                throw new InvalidOperationException(
                    $"Main Menu visual capture rejected incomplete route {routePolicy.Intent}.");
            }

            if (_snapshot.HasValue)
            {
                if (_token == token && _snapshot.Value.Intent == routePolicy.Intent)
                {
                    return _snapshot.Value;
                }

                throw new InvalidOperationException(
                    $"Main Menu visual token {_token} still owns the immutable snapshot.");
            }

            var color = routePolicy.Intent == SceneTransitionIntent.CinematicToMainMenu
                ? cinematicOpaqueColor ??
                  throw new InvalidOperationException(
                      "CinematicToMainMenu requires the authored cinematic opaque color.")
                : NeutralBlack;
            var sourceKind = routePolicy.Intent == SceneTransitionIntent.CinematicToMainMenu
                ? MainMenuSourceCloseVisualKind.CinematicOpaqueOwner
                : MainMenuSourceCloseVisualKind.GameplayScreenCenterIris;
            var captured = new MainMenuTransitionVisualSnapshot(
                routePolicy.Intent,
                sourceKind,
                color,
                color,
                color);
            _token = token;
            _snapshot = captured;
            return captured;
        }

        internal static MainMenuTransitionVisualSnapshot Require(
            MainMenuEntrySessionToken token,
            SceneTransitionIntent intent)
        {
            if (!_snapshot.HasValue ||
                _token != token ||
                _snapshot.Value.Intent != intent)
            {
                throw new InvalidOperationException(
                    $"Main Menu visual snapshot does not match token {token} / intent {intent}.");
            }

            return _snapshot.Value;
        }

        internal static void Clear(MainMenuEntrySessionToken token)
        {
            if (_snapshot.HasValue && _token == token)
            {
                _token = default;
                _snapshot = null;
            }
        }

        internal static void ResetForTests()
        {
            _token = default;
            _snapshot = null;
        }
    }

    internal sealed class TerminalIrisMotionProfileResolver
    {
        private readonly TerminalIrisMotionProfile _profile;

        internal TerminalIrisMotionProfileResolver(TerminalIrisMotionProfile profile)
        {
            _profile = profile != null
                ? profile
                : throw new InvalidOperationException(
                    "Production UI composition requires a serialized TerminalIrisMotionProfile.");
            _profile.ValidateOrThrow();
        }

        internal TerminalIrisRuntimePreset ResolveClose(TerminalTransitionKind kind)
        {
            _profile.ValidateOrThrow();
            return kind switch
            {
                TerminalTransitionKind.Victory =>
                    _profile.VictoryClose.CreateRuntimePreset(
                        nameof(TerminalIrisMotionProfile.VictoryClose),
                        revealPreset: null),
                TerminalTransitionKind.Defeat =>
                    _profile.DefeatClose.CreateRuntimePreset(
                        nameof(TerminalIrisMotionProfile.DefeatClose),
                        _profile.DefeatReveal.CreateRuntimePreset(
                            nameof(TerminalIrisMotionProfile.DefeatReveal))),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(kind),
                    kind,
                    "Terminal Iris close preset requires Victory or Defeat."),
            };
        }

        internal TerminalIrisRuntimeOpenPreset ResolveStageEntryOpen()
        {
            _profile.ValidateOrThrow();
            return _profile.StageEntryOpen.CreateRuntimePreset(
                nameof(TerminalIrisMotionProfile.StageEntryOpen));
        }

        internal TerminalIrisRuntimePreset ResolveGameplayEntryClose()
        {
            _profile.ValidateOrThrow();
            return _profile.GameplayEntryClose.CreateRuntimePreset(
                nameof(TerminalIrisMotionProfile.GameplayEntryClose),
                revealPreset: null);
        }

        internal TerminalIrisRuntimePreset ResolveGameplayEntrySourceClose(
            SceneTransitionIntent intent)
        {
            if (intent != SceneTransitionIntent.GameplayEntry &&
                intent != SceneTransitionIntent.ReturnToMainMenu)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(intent),
                    intent,
                    "GameplayEntryClose only owns GameplayEntry and ReturnToMainMenu source-close routes.");
            }

            return ResolveGameplayEntryClose();
        }

        internal TerminalIrisRuntimePreset ResolveRetryClose(SceneTransitionIntent intent)
        {
            if (intent != SceneTransitionIntent.ManualRetry &&
                intent != SceneTransitionIntent.DemoStageRelaunch)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(intent),
                    intent,
                    "RetryClose only owns ManualRetry and DemoStageRelaunch source-close routes.");
            }

            _profile.ValidateOrThrow();
            return _profile.RetryClose.CreateRuntimePreset(
                nameof(TerminalIrisMotionProfile.RetryClose),
                revealPreset: null);
        }

        internal GameplayEntryTransitionVisualSnapshot ResolveRetryVisual(
            SceneTransitionIntent intent)
        {
            _profile.ValidateOrThrow();
            return _profile.CreateRetryTransitionVisualSnapshot(intent);
        }
    }
}
