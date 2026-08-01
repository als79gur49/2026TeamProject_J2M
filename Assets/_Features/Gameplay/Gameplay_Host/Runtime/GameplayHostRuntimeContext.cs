using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.Host.UIAccess;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplayHostRuntimeContext
    {
        public GameplayHostRuntimeContext(
            GameplayBoardRoot boardRoot,
            GameplayBoardSurfaceRenderer boardSurfaceRenderer,
            TickInputBuffer inputBuffer,
            GameplayInputHost inputHost,
            GameplayTickViewPresenter presenter,
            GameplayTimingProfile timingProfile,
            TickRunner tickRunner,
            GameplayEntityViewRegistry viewRegistry,
            TileFeatureVisualRegistry tileFeatureVisualRegistry,
            Transform viewCameraTarget,
            WorldState worldState,
            StageObjectiveRuntimeDefinition objectiveDefinition,
            Camera viewCamera,
            Camera outputCamera,
            GameplayCameraRig viewCameraRig,
            IReadOnlyList<EntityState> presentedInitialEntities,
            GameplayHostUiAccessContext uiAccess,
            int playerRespawnDelayTicks = 1)
        {
            BoardRoot = boardRoot;
            BoardSurfaceRenderer = boardSurfaceRenderer;
            InputBuffer = inputBuffer;
            InputHost = inputHost;
            Presenter = presenter;
            TimingProfile = timingProfile;
            PlayerRespawnDelayTicks = playerRespawnDelayTicks;
            TickRunner = tickRunner;
            ViewRegistry = viewRegistry;
            TileFeatureVisualRegistry = tileFeatureVisualRegistry;
            ViewCameraTarget = viewCameraTarget;
            WorldState = worldState;
            ObjectiveDefinition = objectiveDefinition ?? StageObjectiveRuntimeDefinition.Disabled;
            ViewCamera = viewCamera;
            OutputCamera = outputCamera;
            ViewCameraRig = viewCameraRig;
            PresentedInitialEntities = presentedInitialEntities;
            UiAccess = uiAccess;
        }

        public GameplayBoardRoot BoardRoot { get; }

        public GameplayBoardSurfaceRenderer BoardSurfaceRenderer { get; }

        public TickInputBuffer InputBuffer { get; }

        public GameplayInputHost InputHost { get; }

        public GameplayTickViewPresenter Presenter { get; }

        public IReadOnlyList<EntityState> PresentedInitialEntities { get; }

        public GameplayTimingProfile TimingProfile { get; }

        public int PlayerRespawnDelayTicks { get; }

        public TickRunner TickRunner { get; }

        public Camera ViewCamera { get; }

        public Camera OutputCamera { get; }

        public GameplayCameraRig ViewCameraRig { get; }

        public Transform ViewCameraTarget { get; }

        public GameplayEntityViewRegistry ViewRegistry { get; }

        public TileFeatureVisualRegistry TileFeatureVisualRegistry { get; }

        public WorldState WorldState { get; }

        public StageObjectiveRuntimeDefinition ObjectiveDefinition { get; }

        public GameplayHostUiAccessContext UiAccess { get; }
    }
}
