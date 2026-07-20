using System;
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
            _overlayView.Play(
                clip,
                options,
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
}
