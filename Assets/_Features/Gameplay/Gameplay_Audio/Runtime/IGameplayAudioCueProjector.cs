using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Audio
{
    public interface IGameplayAudioCueProjector
    {
        IReadOnlyList<GameplayAudioCue> Project(
            TickPresentationData presentationData,
            CubeTopologyState finalTopology);
    }
}
