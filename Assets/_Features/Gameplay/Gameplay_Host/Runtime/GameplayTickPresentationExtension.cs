using System.Collections.Generic;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public readonly struct GameplayTickPresentationExtensionContext
    {
        public GameplayTickPresentationExtensionContext(
            TickResult result,
            CubeTopologyState topology,
            GameplayPresentationStateStore stateStore,
            GameplayCubeProjector projector,
            EnemyPresentationCatalog enemyPresentationCatalog = null,
            EnemyPresentationBinding[] enemyPresentationBindings = null,
            GameplayTimingProfile timingProfile = null,
            IReadOnlyList<TileFeatureVfxStyleBinding> tileFeatureVfxStyleBindings = null,
            int topologyTransitionEpoch = 0,
            bool isTopologyTransitionCompletionReconcile = false)
        {
            Result = result;
            Topology = topology;
            StateStore = stateStore;
            Projector = projector;
            EnemyPresentationCatalog = enemyPresentationCatalog;
            EnemyPresentationBindings = enemyPresentationBindings ?? System.Array.Empty<EnemyPresentationBinding>();
            TimingProfile = timingProfile ?? GameplayTimingProfile.CreateDefault();
            TileFeatureVfxStyleBindings = tileFeatureVfxStyleBindings ?? System.Array.Empty<TileFeatureVfxStyleBinding>();
            TopologyTransitionEpoch = topologyTransitionEpoch;
            IsTopologyTransitionCompletionReconcile = isTopologyTransitionCompletionReconcile;
        }

        public TickResult Result { get; }

        public CubeTopologyState Topology { get; }

        public GameplayPresentationStateStore StateStore { get; }

        public GameplayCubeProjector Projector { get; }

        public EnemyPresentationCatalog EnemyPresentationCatalog { get; }

        public EnemyPresentationBinding[] EnemyPresentationBindings { get; }

        public GameplayTimingProfile TimingProfile { get; }

        public IReadOnlyList<TileFeatureVfxStyleBinding> TileFeatureVfxStyleBindings { get; }

        public int TopologyTransitionEpoch { get; }

        public bool IsTopologyTransitionCompletionReconcile { get; }
    }

    public interface IGameplayTickPresentationExtension
    {
        void ResetSession();

        void Present(in GameplayTickPresentationExtensionContext context);

        void UpdatePresentation(float deltaTime);

        void HardCleanup();
    }

    public interface IGameplayOutputCameraPresentationExtension
    {
        void ConfigureOutputCamera(Camera outputCamera, Transform localSpaceRoot);
    }

    public readonly struct GameplayPresentationMotionVfxContext
    {
        public GameplayPresentationMotionVfxContext(
            int tickIndex,
            object trackState,
            GameplayPresentationStateStore stateStore,
            GameplayCubeProjector projector,
            GameplayTimingProfile timingProfile)
        {
            TickIndex = tickIndex;
            TrackState = trackState;
            StateStore = stateStore;
            Projector = projector;
            TimingProfile = timingProfile ?? GameplayTimingProfile.CreateDefault();
        }

        public int TickIndex { get; }

        public object TrackState { get; }

        public GameplayPresentationStateStore StateStore { get; }

        public GameplayCubeProjector Projector { get; }

        public GameplayTimingProfile TimingProfile { get; }
    }

    public interface IGameplayPresentationMotionVfxExtension
    {
        void RefreshPresentationMotionVfx(in GameplayPresentationMotionVfxContext context);
    }

    public interface IGameplayTopologyTransitionCompletionPresentationExtension
    {
        void ReconcileTopologyTransitionCompleted(in GameplayTickPresentationExtensionContext context);
    }

}
