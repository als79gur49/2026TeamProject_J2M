using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Loop;
using Game.Shared.Audio;

namespace Game.Feature.Gameplay.Audio
{
    public sealed class GameplayAudioPresenter
    {
        private readonly IAudioService audioService;
        private readonly GameplayAudioMap audioMap;
        private readonly IGameplayAudioCueProjector projector;

        public GameplayAudioPresenter(
            IAudioService audioService,
            GameplayAudioMap audioMap,
            IGameplayAudioCueProjector projector)
        {
            this.audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            this.audioMap = audioMap ?? throw new ArgumentNullException(nameof(audioMap));
            this.projector = projector ?? throw new ArgumentNullException(nameof(projector));

            this.audioMap.ValidateOrThrow();
        }

        public void Present(TickResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var cues = projector.Project(result.PresentationData, result.FinalTopology);
            if (cues == null || cues.Count == 0)
            {
                return;
            }

            for (var i = 0; i < cues.Count; i++)
            {
                PlayCue(cues[i]);
            }
        }

        private void PlayCue(in GameplayAudioCue cue)
        {
            var binding = audioMap.ResolveOrThrow(cue.SemanticId);
            var resolvedSlot = cue.AttachmentSlot.IsEmpty
                ? binding.AttachmentSlot
                : cue.AttachmentSlot;

            if (cue.Owner != null && !resolvedSlot.IsEmpty)
            {
                audioService.PlayAttached(binding.Definition, cue.Owner, resolvedSlot, cue.Context);
                return;
            }

            audioService.Play2D(binding.Definition, cue.Context);
        }
    }
}
