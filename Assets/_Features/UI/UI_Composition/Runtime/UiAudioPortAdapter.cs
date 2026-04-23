using System;
using Game.Feature.UI.Application;
using Game.Shared.Audio;

namespace Game.Feature.UI.Composition
{
    internal sealed class UiAudioPortAdapter : IUiAudioPort
    {
        private readonly IAudioService _audioService;
        private readonly UiAudioCueMap _cueMap;

        public UiAudioPortAdapter(IAudioService audioService, UiAudioCueMap cueMap)
        {
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _cueMap = cueMap ?? throw new ArgumentNullException(nameof(cueMap));
            _cueMap.ValidateOrThrow();
        }

        public void Play(UiAudioCueId cueId)
        {
            var binding = _cueMap.ResolveBindingOrThrow(cueId);
            _audioService.Play2D(
                binding.Definition,
                new AudioPlaybackContext(debugTag: $"UI/{cueId}"));
        }
    }
}
