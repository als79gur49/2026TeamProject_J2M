using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
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
            Transform viewCameraTarget,
            WorldState worldState,
            Camera viewCamera,
            GameplayCameraRig viewCameraRig,
            IReadOnlyList<EntityState> presentedInitialEntities)
        {
            BoardRoot = boardRoot;
            BoardSurfaceRenderer = boardSurfaceRenderer;
            InputBuffer = inputBuffer;
            InputHost = inputHost;
            Presenter = presenter;
            TimingProfile = timingProfile;
            TickRunner = tickRunner;
            ViewRegistry = viewRegistry;
            ViewCameraTarget = viewCameraTarget;
            WorldState = worldState;
            ViewCamera = viewCamera;
            ViewCameraRig = viewCameraRig;
            PresentedInitialEntities = presentedInitialEntities;
        }

        public GameplayBoardRoot BoardRoot { get; }

        public GameplayBoardSurfaceRenderer BoardSurfaceRenderer { get; }

        public TickInputBuffer InputBuffer { get; }

        public GameplayInputHost InputHost { get; }

        public GameplayTickViewPresenter Presenter { get; }

        public IReadOnlyList<EntityState> PresentedInitialEntities { get; }

        public GameplayTimingProfile TimingProfile { get; }

        public TickRunner TickRunner { get; }

        public Camera ViewCamera { get; }

        public GameplayCameraRig ViewCameraRig { get; }

        public Transform ViewCameraTarget { get; }

        public GameplayEntityViewRegistry ViewRegistry { get; }

        public WorldState WorldState { get; }
    }
}
