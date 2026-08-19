using System;
using Game.Feature.Stages;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    public sealed class ComicCinematicFlowCoordinator :
        ICinematicSequencePlayer,
        ICinematicOpaqueHandoffCancellationOwner
    {
        private readonly ComicCinematicSequenceDefinition _introDefinition;
        private readonly ComicCinematicSequenceDefinition _outroDefinition;
        private readonly IComicCinematicPlaybackOverlay _overlayView;
        private readonly CinematicAudioFocusController _overlayAudioFocusController;
        private readonly ICinematicAudioFocusOwner _audioFocusController;
        private bool _audioFocusActive;
        private bool _completionDispatched;
        private CinematicOpaqueHandoffToken _opaqueHandoffToken;

        public ComicCinematicFlowCoordinator(
            ComicCinematicSequenceDefinition introDefinition,
            ComicCinematicSequenceDefinition outroDefinition,
            ComicCinematicOverlayView overlayView,
            CinematicAudioFocusController audioFocusController)
            : this(
                introDefinition,
                outroDefinition,
                overlayView,
                audioFocusController,
                audioFocusController)
        {
        }

        internal ComicCinematicFlowCoordinator(
            ComicCinematicSequenceDefinition introDefinition,
            ComicCinematicSequenceDefinition outroDefinition,
            IComicCinematicPlaybackOverlay overlayView,
            CinematicAudioFocusController overlayAudioFocusController,
            ICinematicAudioFocusOwner audioFocusController)
        {
            _introDefinition = introDefinition;
            _outroDefinition = outroDefinition;
            _overlayView = overlayView ?? throw new ArgumentNullException(nameof(overlayView));
            _overlayAudioFocusController = overlayAudioFocusController;
            _audioFocusController = audioFocusController;
        }

        public bool HasIntroContent =>
            _introDefinition != null && _introDefinition.HasContent;

        public bool HasOutroContent =>
            _outroDefinition != null && _outroDefinition.HasContent;

        public bool IsPlaying => _overlayView.IsPlaying;

        public void PlayIntro(Action<CinematicPlaybackCompletion> completion)
        {
            Play(
                _introDefinition,
                SceneTransitionIntent.CinematicToGameplay,
                completion);
        }

        public void PlayOutro(Action<CinematicPlaybackCompletion> completion)
        {
            Play(
                _outroDefinition,
                SceneTransitionIntent.CinematicToMainMenu,
                completion);
        }

        bool ICinematicOpaqueHandoffCancellationOwner
            .TryReleaseCancelledIntroOpaqueOwner()
        {
            var token = _opaqueHandoffToken;
            if (!token.IsValid ||
                !CinematicOpaqueHandoffRegistry.TryReleaseCancelledOpaqueOwner(
                    token,
                    SceneTransitionIntent.CinematicToGameplay))
            {
                return false;
            }

            _opaqueHandoffToken = default;
            return true;
        }

        private void Play(
            ComicCinematicSequenceDefinition definition,
            SceneTransitionIntent intent,
            Action<CinematicPlaybackCompletion> completion)
        {
            if (definition == null || !definition.HasContent)
            {
                completion?.Invoke(new CinematicPlaybackCompletion(
                    CinematicPlaybackCompletionKind.Completed));
                return;
            }

            if (!definition.TryValidate(out var failureReason))
            {
                completion?.Invoke(new CinematicPlaybackCompletion(
                    CinematicPlaybackCompletionKind.Failed,
                    failureReason));
                return;
            }

            if (IsPlaying)
            {
                completion?.Invoke(new CinematicPlaybackCompletion(
                    CinematicPlaybackCompletionKind.Failed,
                    "A comic cinematic is already playing."));
                return;
            }

            CinematicOpaqueHandoffToken handoffToken = default;
            var overlaySetupStarted = false;
            var focusSetupStarted = false;
            _completionDispatched = false;
            _audioFocusActive = false;
            try
            {
                overlaySetupStarted = true;
                _overlayView.EnsureHierarchy();
                _overlayView.SetAudioFocusController(_overlayAudioFocusController);
                if (!CinematicOpaqueHandoffRegistry.TryClaim(
                        intent,
                        TerminalSessionRegistry.Authority.CurrentSceneGeneration,
                        definition.Timing.FadeColor,
                        () => _overlayView.ReleaseOpaqueHandoff(handoffToken),
                        out handoffToken))
                {
                    EndAudioFocusIfActive();
                    completion?.Invoke(new CinematicPlaybackCompletion(
                        CinematicPlaybackCompletionKind.Failed,
                        "The comic cinematic opaque handoff session is already owned."));
                    return;
                }

                _opaqueHandoffToken = handoffToken;
                if (definition.AudioClip != null && _audioFocusController != null)
                {
                    focusSetupStarted = true;
                    _audioFocusController.BeginFocus(_overlayView.CinematicAudioSource);
                    _audioFocusActive = true;
                }

                _overlayView.Play(
                    definition,
                    handoffToken,
                    result => CompleteOnce(result, completion));
            }
            catch (Exception setupException)
            {
                CleanupFailedSetup(
                    setupException,
                    intent,
                    handoffToken,
                    overlaySetupStarted,
                    focusSetupStarted);
                throw;
            }
        }

        private void CleanupFailedSetup(
            Exception setupException,
            SceneTransitionIntent intent,
            CinematicOpaqueHandoffToken handoffToken,
            bool overlaySetupStarted,
            bool focusSetupStarted)
        {
            var exactClaimedOwner =
                CinematicOpaqueHandoffRegistry.IsExactClaimedOwner(handoffToken, intent);
            var overlayAborted = !overlaySetupStarted;
            if (overlaySetupStarted && (!handoffToken.IsValid || exactClaimedOwner))
            {
                try
                {
                    overlayAborted = _overlayView.AbortSetupAfterFailure(handoffToken);
                }
                catch (Exception cleanupException)
                {
                    AttachCleanupFailure(
                        setupException,
                        "ComicCinematicOverlayAbortFailure",
                        cleanupException);
                }
            }

            if (focusSetupStarted)
            {
                try
                {
                    _audioFocusActive = false;
                    _audioFocusController?.EndFocus();
                }
                catch (Exception cleanupException)
                {
                    AttachCleanupFailure(
                        setupException,
                        "ComicCinematicAudioFocusEndFailure",
                        cleanupException);
                }
            }

            if (exactClaimedOwner)
            {
                if (!overlayAborted ||
                    !CinematicOpaqueHandoffRegistry.TryAbortClaimedOwnerAfterSetupFailure(
                        handoffToken,
                        intent))
                {
                    CinematicOpaqueHandoffRegistry.TryFailHoldingOpaque(
                        handoffToken,
                        overlayAborted
                            ? "The exact comic cinematic setup claim could not be aborted."
                            : "The partial comic cinematic overlay setup could not be aborted.");
                }
            }

            var current = CinematicOpaqueHandoffRegistry.Current;
            if (!current.IsActive || current.Token != handoffToken)
            {
                _opaqueHandoffToken = default;
            }
        }

        private void EndAudioFocusIfActive()
        {
            if (!_audioFocusActive)
            {
                return;
            }

            _audioFocusActive = false;
            _audioFocusController?.EndFocus();
        }

        private static void AttachCleanupFailure(
            Exception setupException,
            string key,
            Exception cleanupException)
        {
            if (!setupException.Data.Contains(key))
            {
                setupException.Data[key] = cleanupException;
            }
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
            EndAudioFocusIfActive();
            completion?.Invoke(result);
        }
    }
}
