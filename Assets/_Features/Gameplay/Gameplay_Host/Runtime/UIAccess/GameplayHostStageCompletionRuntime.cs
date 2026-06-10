using System;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Stages;

namespace Game.Feature.Gameplay.Host.UIAccess
{
    internal sealed class GameplayHostStageCompletionRuntime
    {
        private readonly StageContentEntry _entry;
        private readonly StageSessionTracker _sessionTracker = new();
        private bool _completionInProgress;

        public GameplayHostStageCompletionRuntime(
            StageContentEntry entry,
            IStageCompletionProfileStore profileStore = null)
        {
            _entry = entry;
            _sessionTracker.Start(ResolveStageId(entry));
        }

        public MinimalStageCompletionReadModel CurrentMinimalStageCompletion { get; private set; }

        public StageCompletionReadModel CurrentStageCompletion { get; private set; }

        public bool IsCompletionInProgress => _completionInProgress;

        public MinimalStageCompletionReadModel ProcessTick(TickResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            _sessionTracker.Advance(result);
            if (!_sessionTracker.TryCreateClearResult(out var clearResult))
            {
                return CurrentMinimalStageCompletion;
            }

            return CompleteStage(clearResult);
        }

        public MinimalStageCompletionReadModel ForceClearCurrentStage()
        {
            if (CurrentMinimalStageCompletion != null)
            {
                throw new InvalidOperationException("Current stage already has a completion result.");
            }

            if (!_sessionTracker.TryEmitForcedClear(out var clearResult))
            {
                throw new InvalidOperationException("Current stage cannot emit another terminal clear result.");
            }

            return CompleteStage(clearResult);
        }

        private MinimalStageCompletionReadModel CompleteStage(StageClearResult clearResult)
        {
            if (!clearResult.StageId.IsValid)
            {
                throw new InvalidOperationException(
                    "Stage completion requires a valid StageContentEntry StageId. Runtime default stage fallback is not allowed.");
            }

            if (_completionInProgress)
            {
                throw new InvalidOperationException("Stage completion is already in progress.");
            }

            _completionInProgress = true;
            try
            {
                CurrentMinimalStageCompletion = MinimalStageCompletionReadModelBuilder.Build(_entry, clearResult);
                return CurrentMinimalStageCompletion;
            }
            finally
            {
                _completionInProgress = false;
            }
        }

        private static StageId ResolveStageId(StageContentEntry entry)
        {
            return entry != null && entry.StageId.IsValid
                ? entry.StageId
                : StageId.None;
        }
    }
}
