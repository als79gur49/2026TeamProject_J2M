using System;
using Game.Feature.Stages;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    public sealed class ComicSequenceFlowCoordinator :
        IComicIntroOutroFlow,
        IComicSequenceOpaqueHandoffCancellationOwner
    {
        private readonly ComicSequenceDefinition _introDefinition;
        private readonly ComicSequenceDefinition _outroDefinition;
        private readonly IComicSequenceOverlay _overlayView;
        private readonly ComicSequenceAudioFocusController _overlayAudioFocusController;
        private readonly IComicSequenceAudioFocusOwner _audioFocusController;
        private bool _audioFocusActive;
        private bool _completionDispatched;
        private ComicSequenceOpaqueHandoffToken _opaqueHandoffToken;

        public ComicSequenceFlowCoordinator(
            ComicSequenceDefinition introDefinition,
            ComicSequenceDefinition outroDefinition,
            ComicSequenceOverlayView overlayView,
            ComicSequenceAudioFocusController audioFocusController)
            : this(
                introDefinition,
                outroDefinition,
                overlayView,
                audioFocusController,
                audioFocusController)
        {
        }

        internal ComicSequenceFlowCoordinator(
            ComicSequenceDefinition introDefinition,
            ComicSequenceDefinition outroDefinition,
            IComicSequenceOverlay overlayView,
            ComicSequenceAudioFocusController overlayAudioFocusController,
            IComicSequenceAudioFocusOwner audioFocusController)
        {
            _introDefinition = introDefinition;
            _outroDefinition = outroDefinition;
            _overlayView = overlayView ?? throw new ArgumentNullException(nameof(overlayView));
            _overlayAudioFocusController = overlayAudioFocusController;
            _audioFocusController = audioFocusController;
        }

        public bool HasIntroSequence =>
            _introDefinition != null && _introDefinition.HasContent;

        public bool HasOutroSequence =>
            _outroDefinition != null && _outroDefinition.HasContent;

        public bool IsPresenting => _overlayView.IsPresenting;

        public void PresentIntro(Action<ComicSequenceResult> completion)
        {
            Present(
                _introDefinition,
                SceneTransitionIntent.ComicIntroToGameplay,
                completion);
        }

        public void PresentOutro(Action<ComicSequenceResult> completion)
        {
            Present(
                _outroDefinition,
                SceneTransitionIntent.ComicOutroToMainMenu,
                completion);
        }

        bool IComicSequenceOpaqueHandoffCancellationOwner
            .TryReleaseCancelledIntroOpaqueOwner()
        {
            var token = _opaqueHandoffToken;
            if (!token.IsValid ||
                !ComicSequenceOpaqueHandoffRegistry.TryReleaseCancelledOpaqueOwner(
                    token,
                    SceneTransitionIntent.ComicIntroToGameplay))
            {
                return false;
            }

            _opaqueHandoffToken = default;
            return true;
        }

        private void Present(
            ComicSequenceDefinition definition,
            SceneTransitionIntent intent,
            Action<ComicSequenceResult> completion)
        {
            if (definition == null || !definition.HasContent)
            {
                completion?.Invoke(new ComicSequenceResult(
                    ComicSequenceResultKind.Completed));
                return;
            }

            if (!definition.TryValidate(out var failureReason))
            {
                completion?.Invoke(new ComicSequenceResult(
                    ComicSequenceResultKind.Failed,
                    failureReason));
                return;
            }

            if (IsPresenting)
            {
                completion?.Invoke(new ComicSequenceResult(
                    ComicSequenceResultKind.Failed,
                    "A comic sequence is already being presented."));
                return;
            }

            ComicSequenceOpaqueHandoffToken handoffToken = default;
            var overlaySetupStarted = false;
            var focusSetupStarted = false;
            _completionDispatched = false;
            _audioFocusActive = false;
            try
            {
                overlaySetupStarted = true;
                _overlayView.EnsureHierarchy();
                _overlayView.SetAudioFocusController(_overlayAudioFocusController);
                if (!ComicSequenceOpaqueHandoffRegistry.TryClaim(
                        intent,
                        TerminalSessionRegistry.Authority.CurrentSceneGeneration,
                        definition.Timing.FadeColor,
                        () => _overlayView.ReleaseOpaqueHandoff(handoffToken),
                        out handoffToken))
                {
                    EndAudioFocusIfActive();
                    completion?.Invoke(new ComicSequenceResult(
                        ComicSequenceResultKind.Failed,
                        "The comic sequence opaque handoff session is already owned."));
                    return;
                }

                _opaqueHandoffToken = handoffToken;
                if (definition.AudioClip != null && _audioFocusController != null)
                {
                    focusSetupStarted = true;
                    _audioFocusController.BeginFocus(_overlayView.ComicSequenceAudioSource);
                    _audioFocusActive = true;
                }

                _overlayView.Present(
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
            ComicSequenceOpaqueHandoffToken handoffToken,
            bool overlaySetupStarted,
            bool focusSetupStarted)
        {
            var exactClaimedOwner =
                ComicSequenceOpaqueHandoffRegistry.IsExactClaimedOwner(handoffToken, intent);
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
                        "ComicSequenceOverlayAbortFailure",
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
                        "ComicSequenceAudioFocusEndFailure",
                        cleanupException);
                }
            }

            if (exactClaimedOwner)
            {
                if (!overlayAborted ||
                    !ComicSequenceOpaqueHandoffRegistry.TryAbortClaimedOwnerAfterSetupFailure(
                        handoffToken,
                        intent))
                {
                    ComicSequenceOpaqueHandoffRegistry.TryFailHoldingOpaque(
                        handoffToken,
                        overlayAborted
                            ? "The exact comic sequence setup claim could not be aborted."
                            : "The partial comic sequence overlay setup could not be aborted.");
                }
            }

            var current = ComicSequenceOpaqueHandoffRegistry.Current;
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
            ComicSequenceResult result,
            Action<ComicSequenceResult> completion)
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
