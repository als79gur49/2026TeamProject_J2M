using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;

namespace Game.Feature.Gameplay.Tests.Replay
{
    internal sealed class TickReplayHarness
    {
        public IReadOnlyList<TickReplayFrame> Run(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics,
            IReadOnlyList<TickInput> inputs)
        {
            var pipeline = new TickPipeline(worldState, entityLogics);
            var frames = new List<TickReplayFrame>(inputs.Count);

            for (var i = 0; i < inputs.Count; i++)
            {
                var result = pipeline.RunTick(inputs[i]);
                frames.Add(
                    new TickReplayFrame(
                        result.TickIndex,
                        result.DeterminismHash,
                        result.Trace.Text,
                        string.Join(
                            ",",
                            result.FinalEntities.Select(entity =>
                                $"{entity.entityId}:{entity.position.x}:{entity.position.y}:{entity.hp}:{entity.state}:{entity.markedForDeath}")),
                        string.Join("\n", result.EventLog)));
            }

            return new ReadOnlyCollection<TickReplayFrame>(frames);
        }
    }

    internal readonly struct TickReplayFrame
    {
        public TickReplayFrame(
            int tickIndex,
            string determinismHash,
            string trace,
            string finalEntitiesDump,
            string eventLogDump)
        {
            TickIndex = tickIndex;
            DeterminismHash = determinismHash;
            Trace = trace;
            FinalEntitiesDump = finalEntitiesDump;
            EventLogDump = eventLogDump;
        }

        public int TickIndex { get; }

        public string DeterminismHash { get; }

        public string Trace { get; }

        public string FinalEntitiesDump { get; }

        public string EventLogDump { get; }
    }
}
