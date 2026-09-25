using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.Host.UIAccess;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplaySceneHost : MonoBehaviour
    {
        private GameplayHostRuntimeContext _runtime;

        public GameplayInputHost InputHost => _runtime?.InputHost;

        public int PlayerEntityId => _runtime?.InputHost != null
            ? _runtime.InputHost.PlayerEntityId
            : 0;

        public GameplayTickViewPresenter Presenter => _runtime?.Presenter;

        public TickRunner TickRunner => _runtime?.TickRunner;

        public GameplayTimingProfile TimingProfile => _runtime?.TimingProfile;

        public GameplayBoardRoot BoardRoot => _runtime?.BoardRoot;

        public GameplayBoardSurfaceRenderer BoardSurfaceRenderer => _runtime?.BoardSurfaceRenderer;

        public GameplayEntityViewRegistry ViewRegistry => _runtime?.ViewRegistry;

        public Transform ViewCameraTarget => _runtime?.ViewCameraTarget;

        public Camera ViewCamera => _runtime?.ViewCamera;

        public Camera OutputCamera => _runtime?.OutputCamera;

        public WorldState WorldState => _runtime?.WorldState;

        public GameplayHostUiAccessContext UiAccess => _runtime?.UiAccess;

        public bool HasStrongGameplayEntryRuntime =>
            _runtime?.WorldState != null &&
            _runtime.TickRunner != null &&
            _runtime.InputHost != null &&
            _runtime.ViewRegistry != null &&
            _runtime.InputHost.PlayerEntityId > 0;

        public StageObjectiveRuntimeDefinition ObjectiveDefinition => _runtime?.ObjectiveDefinition ?? StageObjectiveRuntimeDefinition.Disabled;

        public StageObjectiveTickResult CurrentObjectiveResult => TickRunner?.CurrentObjectiveResult ?? StageObjectiveTickResult.NoObjective;

        public void Initialize(GameplaySceneHostConfiguration configuration)
        {
            _runtime = GameplayHostRuntimeFactory.Create(this, configuration);
        }

        private void OnDestroy()
        {
            _runtime?.UiAccess?.Dispose();
        }
    }
}
