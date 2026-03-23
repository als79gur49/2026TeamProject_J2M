using System;

namespace Game.Feature.Gameplay.Loop
{
    public sealed class TickRunner
    {
        private readonly TickInputBuffer _inputBuffer;
        private readonly TickPipeline _pipeline;

        public TickRunner(
            TickPipeline pipeline,
            TickInputBuffer inputBuffer,
            int startTickIndex = 1)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _inputBuffer = inputBuffer ?? throw new ArgumentNullException(nameof(inputBuffer));

            if (startTickIndex <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startTickIndex), "TickRunner requires a positive starting tick index.");
            }

            NextTickIndex = startTickIndex;
        }

        public int NextTickIndex { get; private set; }

        public TickResult RunNextTick()
        {
            return RunTick(_inputBuffer.ConsumeOrDefault(NextTickIndex));
        }

        public TickResult RunTick(in TickInput input)
        {
            if (input.TickIndex != NextTickIndex)
            {
                throw new InvalidOperationException("TickRunner requires monotonic tick execution.");
            }

            var result = _pipeline.RunTick(input);
            if (result.TickIndex != input.TickIndex)
            {
                throw new InvalidOperationException("TickPipeline returned a mismatched tick index.");
            }

            NextTickIndex = result.TickIndex + 1;
            return result;
        }
    }
}
