using System;
using System.Threading;
using Game.Feature.Stages;
using UnityEngine;
using UnityEngine.Video;

namespace Game.Feature.UI.Composition
{
    public interface ISlotCinematicPlayer
    {
        bool HasIntroClip { get; }

        bool HasOutroClip { get; }

        bool IsPlaying { get; }

        void PlayIntro(Action<CinematicPlaybackCompletion> completion);

        void PlayOutro(Action<CinematicPlaybackCompletion> completion);

        void RequestSkip();
    }

    public sealed class CinematicFlowCoordinator : ISlotCinematicPlayer
    {
        private readonly CinematicAudioFocusController _audioFocusController;
        private readonly SlotCinematicDefinition _definition;
        private readonly CinematicVideoOverlayView _overlayView;
        private bool _completionDispatched;

        public CinematicFlowCoordinator(
            SlotCinematicDefinition definition,
            CinematicVideoOverlayView overlayView,
            CinematicAudioFocusController audioFocusController)
        {
            _definition = definition;
            _overlayView = overlayView ?? throw new ArgumentNullException(nameof(overlayView));
            _audioFocusController = audioFocusController;
        }

        public bool HasIntroClip => _definition != null && _definition.IntroClip != null;

        public bool HasOutroClip => _definition != null && _definition.OutroClip != null;

        public bool IsPlaying => _overlayView != null && _overlayView.IsPlaying;

        public void PlayIntro(Action<CinematicPlaybackCompletion> completion)
        {
            Play(SlotCinematicKind.Intro, completion);
        }

        public void PlayOutro(Action<CinematicPlaybackCompletion> completion)
        {
            Play(SlotCinematicKind.Outro, completion);
        }

        public void RequestSkip()
        {
            _overlayView.RequestSkip();
        }

        private void Play(
            SlotCinematicKind kind,
            Action<CinematicPlaybackCompletion> completion)
        {
            var clip = _definition != null ? _definition.GetClip(kind) : null;
            if (clip == null)
            {
                completion?.Invoke(new CinematicPlaybackCompletion(
                    CinematicPlaybackCompletionKind.Completed));
                return;
            }

            if (IsPlaying)
            {
                completion?.Invoke(new CinematicPlaybackCompletion(
                    CinematicPlaybackCompletionKind.Failed,
                    "A cinematic is already playing."));
                return;
            }

            _completionDispatched = false;
            var options = _definition.CreatePlaybackOptions();
            _overlayView.EnsureHierarchy(options);
            _overlayView.SetAudioFocusController(_audioFocusController);
            _audioFocusController?.BeginFocus(_overlayView.CinematicAudioSource);
            var intent = kind == SlotCinematicKind.Intro
                ? SceneTransitionIntent.CinematicToGameplay
                : SceneTransitionIntent.CinematicToMainMenu;
            CinematicOpaqueHandoffToken handoffToken = default;
            if (!CinematicOpaqueHandoffRegistry.TryClaim(
                    intent,
                    TerminalSessionRegistry.Authority.CurrentSceneGeneration,
                    options.FadeSettings.FadeColor,
                    () => _overlayView.ReleaseOpaqueHandoff(handoffToken),
                    out handoffToken))
            {
                _audioFocusController?.EndFocus();
                completion?.Invoke(new CinematicPlaybackCompletion(
                    CinematicPlaybackCompletionKind.Failed,
                    "The cinematic opaque handoff session is already owned."));
                return;
            }

            _overlayView.Play(
                clip,
                options,
                handoffToken,
                result => CompleteOnce(result, completion));
        }

        private void CompleteOnce(
            CinematicPlaybackCompletion result,
            Action<CinematicPlaybackCompletion> completion)
        {
            if (_completionDispatched)
            {
                return;
            }

            _completionDispatched = true;
            _audioFocusController?.EndFocus();
            completion?.Invoke(result);
        }
    }

    internal enum CinematicOpaqueHandoffPhase
    {
        Inactive = 0,
        Claimed = 1,
        CinematicOpaqueRendered = 2,
        PersistentCoverRendered = 3,
        Released = 4,
        FailedHoldingOpaque = 5,
    }

    internal readonly struct CinematicOpaqueHandoffToken :
        IEquatable<CinematicOpaqueHandoffToken>
    {
        internal CinematicOpaqueHandoffToken(long value)
        {
            Value = value;
        }

        internal long Value { get; }
        internal bool IsValid => Value > 0;
        public bool Equals(CinematicOpaqueHandoffToken other) =>
            Value == other.Value;
        public override bool Equals(object obj) =>
            obj is CinematicOpaqueHandoffToken other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => IsValid ? Value.ToString() : "none";
        public static bool operator ==(
            CinematicOpaqueHandoffToken left,
            CinematicOpaqueHandoffToken right) => left.Equals(right);
        public static bool operator !=(
            CinematicOpaqueHandoffToken left,
            CinematicOpaqueHandoffToken right) => !left.Equals(right);
    }

    internal readonly struct CinematicOpaqueHandoffSnapshot
    {
        internal CinematicOpaqueHandoffSnapshot(
            bool isActive,
            CinematicOpaqueHandoffToken token,
            SceneTransitionIntent intent,
            CinematicOpaqueHandoffPhase phase,
            long sourceSceneGeneration,
            Color opaqueColor,
            string failureReason)
        {
            IsActive = isActive;
            Token = token;
            Intent = intent;
            Phase = phase;
            SourceSceneGeneration = sourceSceneGeneration;
            opaqueColor.a = 1f;
            OpaqueColor = opaqueColor;
            FailureReason = failureReason ?? string.Empty;
        }

        internal bool IsActive { get; }
        internal CinematicOpaqueHandoffToken Token { get; }
        internal SceneTransitionIntent Intent { get; }
        internal CinematicOpaqueHandoffPhase Phase { get; }
        internal long SourceSceneGeneration { get; }
        internal Color OpaqueColor { get; }
        internal string FailureReason { get; }
    }

    internal static class CinematicOpaqueHandoffRegistry
    {
        private static CinematicOpaqueHandoffSnapshot _current;
        private static Action _releaseOpaqueOwner;
        private static long _nextToken;

        internal static CinematicOpaqueHandoffSnapshot Current => _current;
        internal static bool IsActive => _current.IsActive;

        internal static bool TryClaim(
            SceneTransitionIntent intent,
            long sourceSceneGeneration,
            Color opaqueColor,
            Action releaseOpaqueOwner,
            out CinematicOpaqueHandoffToken token)
        {
            token = default;
            if (_current.IsActive ||
                sourceSceneGeneration <= 0 ||
                releaseOpaqueOwner == null ||
                (intent != SceneTransitionIntent.CinematicToGameplay &&
                 intent != SceneTransitionIntent.CinematicToMainMenu) ||
                !IsFinite(opaqueColor))
            {
                return false;
            }

            var routePolicy =
                SceneTransitionRoutePolicyCatalog.ResolveProduction(intent);
            if (routePolicy.Status != SceneTransitionRouteStatus.Canonical ||
                !routePolicy.TargetRequiresOpaqueOwnerTransfer ||
                !routePolicy.ImplementsOpaqueOwnerTransfer)
            {
                return false;
            }

            token = new CinematicOpaqueHandoffToken(
                Interlocked.Increment(ref _nextToken));
            _releaseOpaqueOwner = releaseOpaqueOwner;
            _current = new CinematicOpaqueHandoffSnapshot(
                true,
                token,
                intent,
                CinematicOpaqueHandoffPhase.Claimed,
                sourceSceneGeneration,
                opaqueColor,
                string.Empty);
            return true;
        }

        internal static bool TryAcknowledgeCinematicOpaqueRendered(
            CinematicOpaqueHandoffToken token)
        {
            if (!Matches(token) ||
                _current.Phase != CinematicOpaqueHandoffPhase.Claimed)
            {
                return false;
            }

            Publish(
                CinematicOpaqueHandoffPhase.CinematicOpaqueRendered,
                string.Empty);
            return true;
        }

        internal static bool TryTransferToPersistentCover(
            CinematicOpaqueHandoffToken token)
        {
            if (!Matches(token) ||
                _current.Phase !=
                CinematicOpaqueHandoffPhase.CinematicOpaqueRendered ||
                _releaseOpaqueOwner == null)
            {
                return false;
            }

            Publish(
                CinematicOpaqueHandoffPhase.PersistentCoverRendered,
                string.Empty);
            try
            {
                _releaseOpaqueOwner.Invoke();
            }
            catch (Exception exception)
            {
                Publish(
                    CinematicOpaqueHandoffPhase.FailedHoldingOpaque,
                    exception.Message);
                return false;
            }

            _releaseOpaqueOwner = null;
            _current = new CinematicOpaqueHandoffSnapshot(
                false,
                _current.Token,
                _current.Intent,
                CinematicOpaqueHandoffPhase.Released,
                _current.SourceSceneGeneration,
                _current.OpaqueColor,
                string.Empty);
            return true;
        }

        internal static bool TryFailHoldingOpaque(
            CinematicOpaqueHandoffToken token,
            string failureReason)
        {
            if (!Matches(token))
            {
                return false;
            }

            Publish(
                CinematicOpaqueHandoffPhase.FailedHoldingOpaque,
                string.IsNullOrWhiteSpace(failureReason)
                    ? "Cinematic opaque ownership transfer failed."
                    : failureReason);
            return true;
        }

        internal static void ResetForTests()
        {
            _current = default;
            _releaseOpaqueOwner = null;
            _nextToken = 0;
        }

        private static bool Matches(CinematicOpaqueHandoffToken token)
        {
            return _current.IsActive &&
                   token.IsValid &&
                   _current.Token == token;
        }

        private static void Publish(
            CinematicOpaqueHandoffPhase phase,
            string failureReason)
        {
            _current = new CinematicOpaqueHandoffSnapshot(
                true,
                _current.Token,
                _current.Intent,
                phase,
                _current.SourceSceneGeneration,
                _current.OpaqueColor,
                failureReason);
        }

        private static bool IsFinite(Color color)
        {
            return !float.IsNaN(color.r) && !float.IsInfinity(color.r) &&
                   !float.IsNaN(color.g) && !float.IsInfinity(color.g) &&
                   !float.IsNaN(color.b) && !float.IsInfinity(color.b) &&
                   !float.IsNaN(color.a) && !float.IsInfinity(color.a);
        }
    }
}
