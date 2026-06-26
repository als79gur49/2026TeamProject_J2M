using System;
using Game.Feature.Gameplay.ActionAudio;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class GameplayActionAudioPresentationController
    {
        private readonly GameplayPresentationStateStore _stateStore;

        private IGameplayAudioPlaybackPort _playbackPort;

        public GameplayActionAudioPresentationController(GameplayPresentationStateStore stateStore)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        }

        public void AttachRuntime(IGameplayAudioPlaybackPort playbackPort)
        {
            _playbackPort = playbackPort ?? throw new ArgumentNullException(nameof(playbackPort));
        }

        public void DetachRuntime()
        {
            _playbackPort = null;
        }

        public void ResetSession()
        {
        }

        internal bool TryPlayBridgeRequest(
            in GameplayActionAudioPlaybackRequest request,
            out GameplayActionAudioPlaybackResult result)
        {
            if (_playbackPort == null)
            {
                result = new GameplayActionAudioPlaybackResult(GameplayActionAudioPlaybackResultKind.PortMissing);
                return false;
            }

            if (!IsSupportedMoment(request.Moment))
            {
                result = new GameplayActionAudioPlaybackResult(GameplayActionAudioPlaybackResultKind.UnsupportedMoment);
                return false;
            }

            if (!TryResolveLiveOwner(request.OwnerEntityId, out var ownerView))
            {
                result = new GameplayActionAudioPlaybackResult(GameplayActionAudioPlaybackResultKind.OwnerViewMissing);
                return false;
            }

            if (!ownerView.TryGetComponent<GameplayActionAudioAuthoring>(out var authoring) ||
                authoring == null)
            {
                result = new GameplayActionAudioPlaybackResult(GameplayActionAudioPlaybackResultKind.AuthoringMissing);
                return false;
            }

            if (authoring.Profile == null)
            {
                result = new GameplayActionAudioPlaybackResult(GameplayActionAudioPlaybackResultKind.ProfileMissing);
                return false;
            }

            authoring.Profile.ValidateOrThrow();
            var resolveStatus = authoring.Profile.ResolveEntryStatus(
                request.Action,
                request.Moment,
                out var binding);
            switch (resolveStatus)
            {
                case GameplayActionAudioProfileResolveStatus.Resolved:
                    PlayResolvedBinding(binding, ownerView, request.Context);
                    result = new GameplayActionAudioPlaybackResult(GameplayActionAudioPlaybackResultKind.Succeeded);
                    return true;
                case GameplayActionAudioProfileResolveStatus.EntryMissing:
                    result = new GameplayActionAudioPlaybackResult(
                        GameplayActionAudioPlaybackResultKind.OptionalProfileEntryMissing);
                    return false;
                case GameplayActionAudioProfileResolveStatus.OptionalBindingMissing:
                case GameplayActionAudioProfileResolveStatus.BindingMissing:
                case GameplayActionAudioProfileResolveStatus.None:
                default:
                    result = new GameplayActionAudioPlaybackResult(GameplayActionAudioPlaybackResultKind.BindingMissing);
                    return false;
            }
        }

        private void PlayResolvedBinding(
            AudioBinding binding,
            GameplayEntityView ownerView,
            in AudioPlaybackContext context)
        {
            if (binding.HasAttachmentSlot)
            {
                _playbackPort.PlayAttached(binding.Definition, ownerView, binding.AttachmentSlot, context);
                return;
            }

            _playbackPort.Play2D(binding.Definition, context);
        }

        private static bool IsSupportedMoment(GameplayActionAudioMoment moment)
        {
            switch (moment)
            {
                case GameplayActionAudioMoment.Windup:
                case GameplayActionAudioMoment.AssistOutOfRange:
                case GameplayActionAudioMoment.NoTarget:
                case GameplayActionAudioMoment.Invalid:
                    return true;
                default:
                    return false;
            }
        }

        private bool TryResolveLiveOwner(int ownerEntityId, out GameplayEntityView ownerView)
        {
            ownerView = null;
            if (!_stateStore.ViewsByEntityId.TryGetValue(ownerEntityId, out ownerView) ||
                ownerView == null ||
                !ownerView.gameObject.activeInHierarchy)
            {
                ownerView = null;
                return false;
            }

            return true;
        }
    }
}
