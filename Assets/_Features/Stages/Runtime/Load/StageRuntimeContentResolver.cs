using System;

namespace Game.Feature.Stages
{
    public sealed class StageRuntimeContentResolver
    {
        private readonly StageLoadStrategyFactory strategyFactory;

        public StageRuntimeContentResolver(StageLoadStrategyFactory strategyFactory = null)
        {
            this.strategyFactory = strategyFactory ?? new StageLoadStrategyFactory();
        }

        public ResolvedStageContent Resolve(StageLoadRequest request)
        {
            var strategy = strategyFactory.Create(request.SourceMode);
            if (strategy == null)
            {
                throw new InvalidOperationException(
                    $"No stage load strategy exists for {request.SourceMode}.");
            }

            return strategy.Resolve(request);
        }
    }
}
