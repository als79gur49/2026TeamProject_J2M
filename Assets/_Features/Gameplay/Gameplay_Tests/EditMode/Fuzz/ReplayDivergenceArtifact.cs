using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.Gameplay.Tests.Replay;

namespace Game.Feature.Gameplay.Tests.Fuzz
{
    internal sealed class ReplayDivergenceArtifact
    {
        private readonly ReadOnlyCollection<TickReplayFrame> _firstRunFrames;
        private readonly ReadOnlyCollection<TickReplayFrame> _secondRunFrames;

        public ReplayDivergenceArtifact(
            int seed,
            string scenarioDump,
            IReadOnlyList<TickReplayFrame> firstRunFrames,
            IReadOnlyList<TickReplayFrame> secondRunFrames,
            int firstDivergentFrameIndex,
            int firstDivergentTick,
            string reason,
            string firstHash,
            string secondHash)
        {
            if (scenarioDump == null)
            {
                throw new ArgumentNullException(nameof(scenarioDump));
            }

            if (firstRunFrames == null)
            {
                throw new ArgumentNullException(nameof(firstRunFrames));
            }

            if (secondRunFrames == null)
            {
                throw new ArgumentNullException(nameof(secondRunFrames));
            }

            if (reason == null)
            {
                throw new ArgumentNullException(nameof(reason));
            }

            if (firstHash == null)
            {
                throw new ArgumentNullException(nameof(firstHash));
            }

            if (secondHash == null)
            {
                throw new ArgumentNullException(nameof(secondHash));
            }

            Seed = seed;
            ScenarioDump = scenarioDump;
            FirstDivergentFrameIndex = firstDivergentFrameIndex;
            FirstDivergentTick = firstDivergentTick;
            Reason = reason;
            FirstHash = firstHash;
            SecondHash = secondHash;
            _firstRunFrames = new ReadOnlyCollection<TickReplayFrame>(new List<TickReplayFrame>(firstRunFrames));
            _secondRunFrames = new ReadOnlyCollection<TickReplayFrame>(new List<TickReplayFrame>(secondRunFrames));
        }

        public int Seed { get; }

        public string ScenarioDump { get; }

        public int FirstDivergentFrameIndex { get; }

        public int FirstDivergentTick { get; }

        public string Reason { get; }

        public string FirstHash { get; }

        public string SecondHash { get; }

        public IReadOnlyList<TickReplayFrame> FirstRunFrames => _firstRunFrames;

        public IReadOnlyList<TickReplayFrame> SecondRunFrames => _secondRunFrames;

        public bool TryGetFirstRunDivergentFrame(out TickReplayFrame frame)
        {
            if (FirstDivergentFrameIndex >= 0 && FirstDivergentFrameIndex < _firstRunFrames.Count)
            {
                frame = _firstRunFrames[FirstDivergentFrameIndex];
                return true;
            }

            frame = default;
            return false;
        }

        public bool TryGetSecondRunDivergentFrame(out TickReplayFrame frame)
        {
            if (FirstDivergentFrameIndex >= 0 && FirstDivergentFrameIndex < _secondRunFrames.Count)
            {
                frame = _secondRunFrames[FirstDivergentFrameIndex];
                return true;
            }

            frame = default;
            return false;
        }
    }
}
