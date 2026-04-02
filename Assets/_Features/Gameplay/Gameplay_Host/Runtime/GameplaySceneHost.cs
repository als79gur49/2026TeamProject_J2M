using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
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

        public void Initialize(GameplaySceneHostConfiguration configuration)
        {
            if (_runtime?.Presenter != null)
            {
                _runtime.Presenter.TopologyCommitted -= HandlePresentedTopologyCommitted;
            }

            _runtime = GameplayHostRuntimeFactory.Create(this, configuration);
            _runtime.Presenter.TopologyCommitted += HandlePresentedTopologyCommitted;
        }

        private void OnDestroy()
        {
            if (_runtime?.Presenter != null)
            {
                _runtime.Presenter.TopologyCommitted -= HandlePresentedTopologyCommitted;
            }
        }

        private void HandlePresentedTopologyCommitted(CubeTopologyState topology)
        {
            BoardSurfaceRenderer?.RefreshTopology(topology);
        }
    }
}
