using System;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Stages;

namespace Game.Feature.Gameplay.Host.UIAccess
{
    internal sealed class GameplayHostStageCompletionRuntime
    {
        private readonly StageCompletionCommitter _committer;
        private readonly StageContentEntry _entry;
        private readonly IStageCompletionProfileStore _profileStore;
        private readonly StageSessionTracker _sessionTracker = new();

        public GameplayHostStageCompletionRuntime(
            StageContentEntry entry,
            IStageCompletionProfileStore profileStore = null)
        {
            _entry = entry;
            _profileStore = profileStore ?? new InMemoryStageCompletionProfileStore();
            _committer = new StageCompletionCommitter(_profileStore);
            _sessionTracker.Start(ResolveStageId(entry));
        }

        public StageCompletionReadModel CurrentStageCompletion { get; private set; }

        public StageCompletionReadModel ProcessTick(TickResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            _sessionTracker.Advance(result);
            if (!_sessionTracker.TryCreateClearResult(out var clearResult))
            {
                return CurrentStageCompletion;
            }

            if (!clearResult.StageId.IsValid)
            {
                throw new InvalidOperationException(
                    "Stage completion requires a valid StageContentEntry StageId. Runtime default stage fallback is not allowed.");
            }

            var clearEvaluationResult = StageClearEvaluator.Evaluate(_entry?.ClearEvaluationDefinition, clearResult);
            var preUpdateProgress = LoadCurrentProgress(clearResult.StageId);
            var rewardGrantResult = RewardEvaluator.Evaluate(_entry?.RewardDefinition, clearEvaluationResult, preUpdateProgress);
            var transaction = StageCompletionTransactionBuilder.Build(
                clearResult,
                clearEvaluationResult,
                rewardGrantResult,
                preUpdateProgress);
            var commitResult = _committer.Commit(transaction);

            var updatedProgress = commitResult.CommittedNow || commitResult.AlreadyCommitted
                ? LoadCurrentProgress(clearResult.StageId)
                : transaction.ProgressPatch.ApplyTo(preUpdateProgress);
            CurrentStageCompletion = StageCompletionReadModelBuilder.Build(
                _entry,
                clearResult,
                clearEvaluationResult,
                rewardGrantResult,
                updatedProgress);
            return CurrentStageCompletion;
        }

        private PlayerStageProgress LoadCurrentProgress(StageId stageId)
        {
            var snapshot = _profileStore.Load() ?? new StageCompletionProfileSnapshot();
            return snapshot.ProgressByStageId.TryGetValue(stageId, out var progress) && progress != null
                ? progress.Clone()
                : PlayerStageProgress.CreateEmpty(stageId);
        }

        private static StageId ResolveStageId(StageContentEntry entry)
        {
            return entry != null && entry.StageId.IsValid
                ? entry.StageId
                : StageId.None;
        }

        private sealed class InMemoryStageCompletionProfileStore : IStageCompletionProfileStore
        {
            private StageCompletionProfileSnapshot _snapshot = new();

            public StageCompletionProfileSnapshot Load()
            {
                return _snapshot.Clone();
            }

            public void Save(StageCompletionProfileSnapshot snapshot)
            {
                _snapshot = snapshot?.Clone() ?? new StageCompletionProfileSnapshot();
            }
        }
    }
}
