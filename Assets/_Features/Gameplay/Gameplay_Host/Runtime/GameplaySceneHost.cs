using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Objectives;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplaySceneHost : MonoBehaviour
    {
        private GameplayHostRuntimeContext _runtime;

        public GameplayInputHost InputHost => _runtime?.InputHost;

        public TickInputBuffer InputBuffer => _runtime?.InputBuffer;

        public GameplayTickViewPresenter Presenter => _runtime?.Presenter;

        public TickRunner TickRunner => _runtime?.TickRunner;

        public GameplayTimingProfile TimingProfile => _runtime?.TimingProfile;

        public GameplayBoardRoot BoardRoot => _runtime?.BoardRoot;

        public GameplayBoardSurfaceRenderer BoardSurfaceRenderer => _runtime?.BoardSurfaceRenderer;

        public GameplayEntityViewRegistry ViewRegistry => _runtime?.ViewRegistry;

        public Transform ViewCameraTarget => _runtime?.ViewCameraTarget;

        public WorldState WorldState => _runtime?.WorldState;

        public StageObjectiveRuntimeDefinition ObjectiveDefinition => _runtime?.ObjectiveDefinition ?? StageObjectiveRuntimeDefinition.Disabled;

        public StageObjectiveTickResult CurrentObjectiveResult => TickRunner?.CurrentObjectiveResult ?? StageObjectiveTickResult.NoObjective;

        public void Initialize(GameplaySceneHostConfiguration configuration)
        {
            _runtime = GameplayHostRuntimeFactory.Create(this, configuration);
        }
    }
}
