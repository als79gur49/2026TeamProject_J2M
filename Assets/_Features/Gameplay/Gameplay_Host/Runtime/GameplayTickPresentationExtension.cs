using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public interface IGameplayBootstrapInstaller
    {
        void Install();
    }

    public interface IGameplayBootstrapReadiness
    {
        bool IsReady { get; }

        string DescribeReadiness();
    }

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
            bool isTopologyTransitionCompletionReconcile = false,
            GameplayCameraViewSnapshot? topologyDestinationCameraView = null)
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
            TopologyDestinationCameraView = topologyDestinationCameraView;
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

        public GameplayCameraViewSnapshot? TopologyDestinationCameraView { get; }

    }

    public interface IGameplayTickPresentationExtension
    {
        void ResetSession();

        void Present(in GameplayTickPresentationExtensionContext context);

        void UpdatePresentation(float deltaTime);

        void HardCleanup();
    }

    public interface IGameplayTopologyTransitionStartPresentationExtension
    {
        void PrepareTopologyTransitionStart(in GameplayTickPresentationExtensionContext context);
    }

    internal interface IGameplayMotionProgressPresentationExtension
    {
        void ObserveMotionProgress(IReadOnlyList<MotionTrackProgressSample> progressSamples);
    }

    public interface IGameplayPresentationPausable
    {
        void SetPresentationPaused(bool paused);
    }

    public enum GameplayStageTerminalPresentationReason
    {
        Unknown = 0,
        PlayerDeathRetry = 1,
        LevelFailed = 2,
    }

    public readonly struct GameplayStageTerminalPresentationContext
    {
        public GameplayStageTerminalPresentationContext(
            GameplayStageTerminalPresentationReason reason,
            TickResult terminalTickResult)
        {
            Reason = reason;
            TerminalTickResult = terminalTickResult;
        }

        public GameplayStageTerminalPresentationReason Reason { get; }

        public TickResult TerminalTickResult { get; }
    }

    public interface IGameplayStageTerminalPresentationExtension
    {
        void ApplyStageTerminalPresentation(in GameplayStageTerminalPresentationContext context);
    }

    internal sealed class GameplayPresentationPauseRegistry
    {
        private const string SetPresentationPausedMethodName = "SetPresentationPaused";
        private readonly Dictionary<System.Type, MethodInfo> pauseMethodsByType = new();
        private readonly List<IGameplayPresentationPausable> pausableTargets = new();
        private readonly List<ReflectedPausableTarget> reflectedPausableTargets = new();
        private readonly Dictionary<Animator, AnimatorPauseState> animatorStates = new();
        private readonly Dictionary<ParticleSystem, ParticlePauseState> particleStates = new();

        private readonly List<MonoBehaviour> behaviourBuffer = new();
        private readonly List<Animator> animatorBuffer = new();
        private readonly List<ParticleSystem> particleBuffer = new();
        private bool registrationBuffersInUse;

        public bool IsPaused { get; private set; }

        public void SetPresentationPaused(bool paused)
        {
            if (IsPaused == paused)
            {
                return;
            }

            IsPaused = paused;
            ApplyPauseStateToRegisteredTargets(paused);
        }

        public void RegisterRoot(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            // A callback may register another root or Clear this registry. Only the
            // outer owner may touch these buffers; nested calls retain array semantics.
            if (registrationBuffersInUse)
            {
                RegisterRootArrays(root);
                return;
            }

            registrationBuffersInUse = true;
            try
            {
                RegisterRootLists(root);
            }
            finally
            {
                behaviourBuffer.Clear();
                animatorBuffer.Clear();
                particleBuffer.Clear();
                registrationBuffersInUse = false;
            }
        }

        private void RegisterRootLists(GameObject root)
        {
            root.GetComponentsInChildren(true, behaviourBuffer);
            RegisterBehaviourBuffer();
            root.GetComponentsInChildren(true, animatorBuffer);
            RegisterAnimatorBuffer();
            root.GetComponentsInChildren(true, particleBuffer);
            RegisterParticleBuffer();
        }

        private void RegisterBehaviourBuffer()
        {
            for (var i = 0; i < behaviourBuffer.Count; i++)
            {
                if (behaviourBuffer[i] is IGameplayPresentationPausable pausable)
                    Register(pausable);
                else if (ReflectedPausableTarget.TryCreate(behaviourBuffer[i], this, out var reflectedTarget))
                    Register(reflectedTarget);
            }
        }

        private void RegisterAnimatorBuffer()
        {
            for (var i = 0; i < animatorBuffer.Count; i++) Register(animatorBuffer[i]);
        }

        private void RegisterParticleBuffer()
        {
            for (var i = 0; i < particleBuffer.Count; i++) Register(particleBuffer[i]);
        }

        private void RegisterRootArrays(GameObject root)
        {
            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IGameplayPresentationPausable pausable)
                {
                    Register(pausable);
                }
                else if (ReflectedPausableTarget.TryCreate(behaviours[i], this, out var reflectedTarget))
                {
                    Register(reflectedTarget);
                }
            }

            var animators = root.GetComponentsInChildren<Animator>(includeInactive: true);
            for (var i = 0; i < animators.Length; i++)
            {
                Register(animators[i]);
            }

            var particleSystems = root.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            for (var i = 0; i < particleSystems.Length; i++)
            {
                Register(particleSystems[i]);
            }
        }

        public void Clear()
        {
            pauseMethodsByType.Clear();
            // Active registration owns its buffers until its finally block completes.
            pausableTargets.Clear();
            reflectedPausableTargets.Clear();
            animatorStates.Clear();
            particleStates.Clear();
            IsPaused = false;
        }

        private MethodInfo ResolvePauseMethod(System.Type type)
        {
            if (pauseMethodsByType.TryGetValue(type, out var method))
            {
                return method;
            }

            method = type.GetMethod(
                SetPresentationPausedMethodName,
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(bool) },
                modifiers: null);
            // Cache unsupported types too. Store before registration can invoke a
            // callback that re-enters registration or clears the registry.
            pauseMethodsByType[type] = method;
            return method;
        }

        private void Register(IGameplayPresentationPausable target)
        {
            if (target == null || pausableTargets.Contains(target))
            {
                return;
            }

            pausableTargets.Add(target);
            if (IsPaused)
            {
                target.SetPresentationPaused(true);
            }
        }

        private void Register(ReflectedPausableTarget target)
        {
            if (target.Behaviour == null ||
                reflectedPausableTargets.Exists(existing => existing.Behaviour == target.Behaviour))
            {
                return;
            }

            reflectedPausableTargets.Add(target);
            if (IsPaused)
            {
                target.SetPresentationPaused(true);
            }
        }

        private void Register(Animator animator)
        {
            if (animator == null || animatorStates.ContainsKey(animator))
            {
                return;
            }

            animatorStates.Add(animator, default);
            if (IsPaused)
            {
                PauseAnimator(animator);
            }
        }

        private void Register(ParticleSystem particleSystem)
        {
            if (particleSystem == null || particleStates.ContainsKey(particleSystem))
            {
                return;
            }

            particleStates.Add(particleSystem, default);
            if (IsPaused)
            {
                PauseParticles(particleSystem);
            }
        }

        private void ApplyPauseStateToRegisteredTargets(bool paused)
        {
            RemoveDestroyedTargets();

            for (var i = 0; i < pausableTargets.Count; i++)
            {
                pausableTargets[i]?.SetPresentationPaused(paused);
            }

            for (var i = 0; i < reflectedPausableTargets.Count; i++)
            {
                reflectedPausableTargets[i].SetPresentationPaused(paused);
            }

            foreach (var animator in new List<Animator>(animatorStates.Keys))
            {
                if (paused)
                {
                    PauseAnimator(animator);
                }
                else
                {
                    ResumeAnimator(animator);
                }
            }

            foreach (var particleSystem in new List<ParticleSystem>(particleStates.Keys))
            {
                if (paused)
                {
                    PauseParticles(particleSystem);
                }
                else
                {
                    ResumeParticles(particleSystem);
                }
            }
        }

        private void PauseAnimator(Animator animator)
        {
            if (animator == null)
            {
                animatorStates.Remove(animator);
                return;
            }

            var state = animatorStates[animator];
            if (state.IsPaused)
            {
                return;
            }

            animatorStates[animator] = new AnimatorPauseState(animator.speed, isPaused: true);
            animator.speed = 0f;
        }

        private void ResumeAnimator(Animator animator)
        {
            if (animator == null)
            {
                animatorStates.Remove(animator);
                return;
            }

            var state = animatorStates[animator];
            if (!state.IsPaused)
            {
                return;
            }

            animator.speed = state.SpeedBeforePause;
            animatorStates[animator] = default;
        }

        private void PauseParticles(ParticleSystem particleSystem)
        {
            if (particleSystem == null)
            {
                particleStates.Remove(particleSystem);
                return;
            }

            var state = particleStates[particleSystem];
            if (state.IsPaused)
            {
                return;
            }

            particleStates[particleSystem] = new ParticlePauseState(
                particleSystem.isPlaying || particleSystem.isEmitting,
                isPaused: true);
            particleSystem.Pause(withChildren: true);
        }

        private void ResumeParticles(ParticleSystem particleSystem)
        {
            if (particleSystem == null)
            {
                particleStates.Remove(particleSystem);
                return;
            }

            var state = particleStates[particleSystem];
            if (!state.IsPaused)
            {
                return;
            }

            if (state.WasPlayingBeforePause)
            {
                particleSystem.Play(withChildren: true);
            }
            else
            {
                particleSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            particleStates[particleSystem] = default;
        }

        private void RemoveDestroyedTargets()
        {
            pausableTargets.RemoveAll(target => target == null || target is Object unityObject && unityObject == null);
            reflectedPausableTargets.RemoveAll(target => target.Behaviour == null);

            foreach (var animator in new List<Animator>(animatorStates.Keys))
            {
                if (animator == null)
                {
                    animatorStates.Remove(animator);
                }
            }

            foreach (var particleSystem in new List<ParticleSystem>(particleStates.Keys))
            {
                if (particleSystem == null)
                {
                    particleStates.Remove(particleSystem);
                }
            }
        }

        private readonly struct AnimatorPauseState
        {
            public AnimatorPauseState(float speedBeforePause, bool isPaused)
            {
                SpeedBeforePause = speedBeforePause;
                IsPaused = isPaused;
            }

            public float SpeedBeforePause { get; }

            public bool IsPaused { get; }
        }

        private readonly struct ParticlePauseState
        {
            public ParticlePauseState(bool wasPlayingBeforePause, bool isPaused)
            {
                WasPlayingBeforePause = wasPlayingBeforePause;
                IsPaused = isPaused;
            }

            public bool WasPlayingBeforePause { get; }

            public bool IsPaused { get; }
        }

        private readonly struct ReflectedPausableTarget
        {
            private readonly MethodInfo method;

            private ReflectedPausableTarget(MonoBehaviour behaviour, MethodInfo method)
            {
                Behaviour = behaviour;
                this.method = method;
            }

            public MonoBehaviour Behaviour { get; }

            public static bool TryCreate(
                MonoBehaviour behaviour,
                GameplayPresentationPauseRegistry registry,
                out ReflectedPausableTarget target)
            {
                target = default;
                if (behaviour == null)
                {
                    return false;
                }

                var method = registry.ResolvePauseMethod(behaviour.GetType());
                if (method == null)
                {
                    return false;
                }

                target = new ReflectedPausableTarget(behaviour, method);
                return true;
            }

            public void SetPresentationPaused(bool paused)
            {
                if (Behaviour != null)
                {
                    method.Invoke(Behaviour, new object[] { paused });
                }
            }
        }
    }

    public readonly struct GameplayInitialPresentationExtensionContext
    {
        public GameplayInitialPresentationExtensionContext(
            InitialPresentationData presentationData,
            CubeTopologyState topology,
            GameplayPresentationStateStore stateStore,
            GameplayCubeProjector projector,
            EnemyPresentationCatalog enemyPresentationCatalog = null,
            EnemyPresentationBinding[] enemyPresentationBindings = null,
            GameplayTimingProfile timingProfile = null,
            IReadOnlyList<TileFeatureVfxStyleBinding> tileFeatureVfxStyleBindings = null)
        {
            PresentationData = presentationData ?? InitialPresentationData.Empty;
            Topology = topology;
            StateStore = stateStore;
            Projector = projector;
            EnemyPresentationCatalog = enemyPresentationCatalog;
            EnemyPresentationBindings = enemyPresentationBindings ?? System.Array.Empty<EnemyPresentationBinding>();
            TimingProfile = timingProfile ?? GameplayTimingProfile.CreateDefault();
            TileFeatureVfxStyleBindings = tileFeatureVfxStyleBindings ?? System.Array.Empty<TileFeatureVfxStyleBinding>();
        }

        public InitialPresentationData PresentationData { get; }

        public CubeTopologyState Topology { get; }

        public GameplayPresentationStateStore StateStore { get; }

        public GameplayCubeProjector Projector { get; }

        public EnemyPresentationCatalog EnemyPresentationCatalog { get; }

        public EnemyPresentationBinding[] EnemyPresentationBindings { get; }

        public GameplayTimingProfile TimingProfile { get; }

        public IReadOnlyList<TileFeatureVfxStyleBinding> TileFeatureVfxStyleBindings { get; }
    }

    public interface IGameplayInitialPresentationExtension
    {
        void PresentInitial(in GameplayInitialPresentationExtensionContext context);
    }

    public interface IGameplayOutputCameraPresentationExtension
    {
        void ConfigureOutputCamera(Camera outputCamera, Transform localSpaceRoot);
    }

    public interface IGameplayPlayerAnchoredPresentationExtension
    {
        void ConfigurePlayerAnchorContext(int playerEntityId, Transform boardPresentationRoot);
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

    public enum DestroyShrinkVfxSequenceState
    {
        None = 0,
        ScheduledDelay = 1,
        SourceCloneCaptured = 2,
        Playing = 3,
        Completed = 4,
        Failed = 5,
    }

    public interface IGameplayDestroyShrinkVfxSequenceStateProvider
    {
        DestroyShrinkVfxSequenceState GetDestroyShrinkState(int sourceEntityId, int sequenceId);
    }

    public interface IGameplayTopologyTransitionCompletionPresentationExtension
    {
        void ReconcileTopologyTransitionCompleted(in GameplayTickPresentationExtensionContext context);
    }

}
