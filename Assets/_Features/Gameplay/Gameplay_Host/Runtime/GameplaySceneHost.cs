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
        private GameplayInputHost _inputHost;
        private GameplayTickViewPresenter _presenter;
        private GameplayEntityViewBinder _viewBinder;
        private GameplayEntityViewRegistry _viewRegistry;
        private Camera _viewCamera;
        private Transform _viewCameraTarget;
        private bool _snapViewCameraToTarget;

        public GameplayInputHost InputHost => _inputHost;

        public TickInputBuffer InputBuffer { get; private set; }

        public GameplayTickViewPresenter Presenter => _presenter;

        public TickRunner TickRunner { get; private set; }

        public GameplayTimingProfile TimingProfile { get; private set; }

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

            WorldState = GameplayCompositionRoot.CreateSessionStartWorldState(
                initialEntities,
                configuration.InitialBoardBounds,
                initialTerrain,
                configuration.InitialTopology,
                TimingProfile);
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

            _viewRegistry.Rebuild();
            var viewFactory = configuration.ViewFactory ??
                (configuration.AutoCreateViews
                    ? new DefaultGameplayEntityViewFactory(_viewRegistry.transform, configuration.CellSize, configuration.PlayerEntityId)
                    : null);
            _viewBinder = new GameplayEntityViewBinder(_viewRegistry, viewFactory);
            ConfigureViewCamera(configuration);
            EnsureViewCameraTarget();
            _presenter.StripCenterChanged -= HandleStripCenterChanged;
            _presenter.StripCenterChanged += HandleStripCenterChanged;
            _presenter.Initialize(
                _viewBinder,
                configuration.InitialBoardBounds,
                configuration.InitialTopology,
                configuration.GridOrigin,
                configuration.CellSize,
                TimingProfile);
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

        private void ConfigureViewCamera(GameplaySceneHostConfiguration configuration)
        {
            _snapViewCameraToTarget = configuration.SnapViewCameraToTarget;
            _viewCamera = configuration.ViewCamera ?? (_snapViewCameraToTarget ? Camera.main : null);
        }

        private void EnsureViewCameraTarget()
        {
            if (_viewCameraTarget != null)
            {
                return;
            }

            var targetObject = new GameObject("GameplayViewCameraTarget");
            targetObject.transform.SetParent(transform, worldPositionStays: false);
            _viewCameraTarget = targetObject.transform;
        }

        private void HandleStripCenterChanged(Vector3 stripCenter)
        {
            if (_viewCameraTarget != null)
            {
                _viewCameraTarget.position = stripCenter;
            }

            if (_snapViewCameraToTarget && _viewCamera != null)
            {
                var cameraPosition = stripCenter;
                cameraPosition.z = _viewCamera.transform.position.z;
                _viewCamera.transform.position = cameraPosition;
            }
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
