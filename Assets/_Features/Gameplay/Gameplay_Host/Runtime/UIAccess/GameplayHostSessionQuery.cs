using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Gameplay.UIAccess.Queries;
using Game.Feature.Stages;

namespace Game.Feature.Gameplay.Host.UIAccess
{
    internal sealed class GameplayHostSessionQuery : IGameplaySessionQuery
    {
        private readonly GameplayHostCommandAdmissionPolicy _admissionPolicy;
        private readonly GameplayHostPauseService _pauseService;
        private readonly TickRunner _tickRunner;

        public GameplayHostSessionQuery(
            TickRunner tickRunner,
            GameplayHostPauseService pauseService,
            GameplayHostCommandAdmissionPolicy admissionPolicy)
        {
            _tickRunner = tickRunner;
            _pauseService = pauseService;
            _admissionPolicy = admissionPolicy;
        }

        public GameplaySessionReadModel Read()
        {
            return new GameplaySessionReadModel(
                _tickRunner?.NextTickIndex ?? 0,
                _pauseService != null && _pauseService.IsPaused,
                _admissionPolicy != null && _admissionPolicy.CanAcceptActionableCommands(),
                _tickRunner?.CurrentObjectiveResult?.IsCleared ?? false);
        }
    }

    internal sealed class GameplayHostStageQuery : IGameplayStageQuery, IGameplayHudRevisionedQuery<GameplayStageReadModel>, IGameplayHudContentInvalidation
    {
        private readonly StageContentEntry _stageContentEntry;
        private long _contentGeneration;
        public void InvalidateHudContent() => _contentGeneration++;
        public bool TryGetRevision(out GameplayHudQueryStamp stamp)
        {
            stamp = new GameplayHudQueryStamp(this, _stageContentEntry, generation: _contentGeneration);
#if UNITY_EDITOR
            return false;
#else
            return true;
#endif
        }
        public GameplayHudQueryRead<GameplayStageReadModel> ReadWithRevision()
        {
            var supported = TryGetRevision(out var before);
            var value = Read();
            TryGetRevision(out var after);
            return new GameplayHudQueryRead<GameplayStageReadModel>(value, before, supported && before.Equals(after));
        }

        public GameplayHostStageQuery(StageContentEntry stageContentEntry)
        {
            _stageContentEntry = stageContentEntry;
        }

        public GameplayStageReadModel Read()
        {
            if (_stageContentEntry == null)
            {
                return default;
            }

            var stageId = _stageContentEntry.StageId;
            var presentation = StagePresentationAssembler.Resolve(_stageContentEntry.PresentationDefinition);
            var displayNameKey = StageDisplayNameKeys.RequireForStage(stageId, presentation.DisplayNameKey);
            return new GameplayStageReadModel(stageId, displayNameKey);
        }
    }
}
