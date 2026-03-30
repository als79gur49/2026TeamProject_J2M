using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplaySceneHost : MonoBehaviour
    {
        private const string BoardRootObjectName = "GameplayBoardRoot";

        private GameplayBoardRoot _boardRoot;
        private GameplayBoardSurfaceRenderer _boardSurfaceRenderer;
        private GameplayInputHost _inputHost;
        private GameplayTickViewPresenter _presenter;
        private GameplayEntityViewBinder _viewBinder;
        private GameplayEntityViewRegistry _viewRegistry;
        private GameplayCameraRig _viewCameraRig;
        private Camera _viewCamera;
        private Transform _viewCameraTarget;
        private bool _snapViewCameraToTarget;

        public GameplayInputHost InputHost => _inputHost;

        public TickInputBuffer InputBuffer { get; private set; }

        public GameplayTickViewPresenter Presenter => _presenter;

        public TickRunner TickRunner { get; private set; }

        public GameplayTimingProfile TimingProfile { get; private set; }

        public GameplayBoardRoot BoardRoot => _boardRoot;

        public GameplayBoardSurfaceRenderer BoardSurfaceRenderer => _boardSurfaceRenderer;

        public GameplayEntityViewRegistry ViewRegistry => _viewRegistry;

        public Transform ViewCameraTarget => _viewCameraTarget;

        public WorldState WorldState { get; private set; }

        public void Initialize(GameplaySceneHostConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (configuration.PlayerEntityId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(configuration), "Player entity ID must be positive.");
            }

            if (!configuration.InitialBoardBounds.IsBounded)
            {
                throw new InvalidOperationException(
                    "GameplaySceneHost requires bounded InitialBoardBounds. Unbounded boards are not supported by runtime scene hosts.");
            }

            EnsureComponents();

            var initialEntities = configuration.InitialEntities ?? Array.Empty<EntityState>();
            var initialTerrain = configuration.InitialTerrain ?? GameplayTerrainData.Empty;
            TimingProfile = configuration.CreateTimingProfile();
            var normalizedInitialEntities = SessionStartEntityNormalizer.Normalize(initialEntities, TimingProfile);

            WorldState = GameplayCompositionRoot.CreateWorldState(
                normalizedInitialEntities,
                configuration.InitialBoardBounds,
                initialTerrain,
                configuration.InitialTopology);
            var initialSnapshot = SnapshotBuilder.Create(WorldState);
            var presentedInitialEntities = new List<EntityState>();
            initialSnapshot.EnumerateEntitiesOrdered(presentedInitialEntities);
            InputBuffer = new TickInputBuffer();
            var staticEntityLogics = BuildStaticEntityLogics(configuration);
            TickRunner = GameplayCompositionRoot.CreateTickRunner(
                WorldState,
                staticEntityLogics,
                InputBuffer,
                TimingProfile,
                startTickIndex: 1);

            EnsureBoardRootHierarchy();
            _boardSurfaceRenderer = _boardRoot.EnsureBoardSurfaceRenderer();
            _viewRegistry.ConfigureSearchRoot(_boardRoot.EntityRoot);
            var viewFactory = configuration.ViewFactory ??
                (configuration.AutoCreateViews
                    ? new DefaultGameplayEntityViewFactory(_boardRoot.EntityRoot, configuration.CellSize, configuration.PlayerEntityId)
                    : null);
            _viewBinder = new GameplayEntityViewBinder(_viewRegistry, viewFactory);
            ConfigureViewCamera(configuration);
            EnsureViewCameraTarget();
            _presenter.TopologyCommitted -= HandlePresentedTopologyCommitted;
            _presenter.Initialize(
                _viewBinder,
                configuration.InitialBoardBounds,
                configuration.InitialTopology,
                configuration.CellSize,
                TimingProfile,
                _boardRoot);
            _boardSurfaceRenderer.Initialize(
                configuration.InitialBoardBounds,
                configuration.CellSize,
                configuration.InitialTopology);
            _presenter.TopologyCommitted += HandlePresentedTopologyCommitted;
            ConfigureViewCameraRig(configuration.CameraSettings);
            _presenter.PresentInitial(presentedInitialEntities, configuration.InitialTopology);

            _inputHost.Initialize(
                InputBuffer,
                TickRunner,
                _presenter,
                configuration.Actions,
                TimingProfile,
                configuration.MoveDeadzone,
                configuration.DirectionChangeConsumesDelay,
                configuration.AutoAdvanceTicks);
        }

        private void EnsureComponents()
        {
            _inputHost = GetComponent<GameplayInputHost>() ?? gameObject.AddComponent<GameplayInputHost>();
            _presenter = GetComponent<GameplayTickViewPresenter>() ?? gameObject.AddComponent<GameplayTickViewPresenter>();
            _viewRegistry = GetComponent<GameplayEntityViewRegistry>() ?? gameObject.AddComponent<GameplayEntityViewRegistry>();
        }

        private void EnsureBoardRootHierarchy()
        {
            _boardRoot = FindExistingBoardRoot();
            if (_boardRoot == null)
            {
                var boardRootObject = new GameObject(BoardRootObjectName);
                boardRootObject.transform.SetParent(transform, worldPositionStays: false);
                _boardRoot = boardRootObject.AddComponent<GameplayBoardRoot>();
            }
            else
            {
                _boardRoot.transform.SetParent(transform, worldPositionStays: false);
                _boardRoot.gameObject.name = BoardRootObjectName;
            }

            _boardRoot.EnsureHierarchy();
        }

        private GameplayBoardRoot FindExistingBoardRoot()
        {
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.TryGetComponent<GameplayBoardRoot>(out var boardRoot))
                {
                    return boardRoot;
                }
            }

            var namedChild = transform.Find(BoardRootObjectName);
            if (namedChild != null)
            {
                return namedChild.GetComponent<GameplayBoardRoot>() ??
                       namedChild.gameObject.AddComponent<GameplayBoardRoot>();
            }

            return null;
        }

        private void ConfigureViewCamera(GameplaySceneHostConfiguration configuration)
        {
            _snapViewCameraToTarget = configuration.SnapViewCameraToTarget;
            _viewCamera = configuration.ViewCamera ?? (_snapViewCameraToTarget ? Camera.main : null);
        }

        private void EnsureViewCameraTarget()
        {
            _viewCameraTarget = _boardRoot != null ? _boardRoot.CameraTargetRoot : null;
        }

        private void ConfigureViewCameraRig(GameplayCameraSettings cameraSettings)
        {
            if (!_snapViewCameraToTarget || _viewCamera == null || _viewCameraTarget == null)
            {
                if (_viewCameraRig != null)
                {
                    _viewCameraRig.enabled = false;
                }

                return;
            }

            _viewCameraRig = GetComponent<GameplayCameraRig>() ?? gameObject.AddComponent<GameplayCameraRig>();
            _viewCameraRig.enabled = true;
            _viewCameraRig.ApplySettings(cameraSettings ?? GameplayCameraSettings.CreateRuntimeDefault());
            _viewCameraRig.Initialize(_viewCamera, _viewCameraTarget, _presenter.VisibleCubeBounds);
        }

        private void OnDestroy()
        {
            if (_presenter != null)
            {
                _presenter.TopologyCommitted -= HandlePresentedTopologyCommitted;
            }
        }

        private void HandlePresentedTopologyCommitted(CubeTopologyState topology)
        {
            _boardSurfaceRenderer?.RefreshTopology(topology);
        }

        private static IReadOnlyList<IEntityLogic> BuildStaticEntityLogics(GameplaySceneHostConfiguration configuration)
        {
            var entityLogics = new List<IEntityLogic>
            {
                new PlayerLogic(configuration.PlayerEntityId),
            };

            if (configuration.StaticEntityLogics == null)
            {
                return entityLogics;
            }

            for (var i = 0; i < configuration.StaticEntityLogics.Length; i++)
            {
                var entityLogic = configuration.StaticEntityLogics[i];
                if (entityLogic == null)
                {
                    throw new ArgumentException("Static entity logic collections cannot contain null entries.", nameof(configuration));
                }

                entityLogics.Add(entityLogic);
            }

            return entityLogics;
        }
    }
}
