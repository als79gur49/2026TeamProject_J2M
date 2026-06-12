using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    [Obsolete("Partial split bridge only. New TileFeature visuals should bind handlers/profiles and route through ITileFeatureVisualCueSink.")]
    public sealed class LegacyTileFeatureVisualCueAdapter :
        MonoBehaviour,
        ITileFeatureVisualCueSink,
        IDestroyTileVisualTarget,
        IDestroyTileActivatedVisualTarget,
        IDestroyTileDeactivatedVisualTarget,
        IDestroyTileActiveStateVisualTarget,
        ITileFeatureActiveStateVisualTarget,
        ISlideTileVisualTarget,
        IBarricadeBlockedVisualTarget,
        IBarricadeCrushedVisualTarget,
        IBarricadeActivatedVisualTarget,
        IBarricadeDeactivatedVisualTarget,
        IBarricadeActiveStateVisualTarget,
        IExitOpenedVisualTarget,
        IExitEnteredVisualTarget,
        IExitOpenStateVisualTarget,
        IMoonBlockGeneratedVisualTarget,
        IMoonBlockGeneratorBlockedVisualTarget,
        IGameplayPresentationPausable
    {
        private static readonly int ButtonActivatedTrigger = Animator.StringToHash("ButtonActivated");
        private static readonly int DestroyTileTriggeredTrigger = Animator.StringToHash("DestroyTileTriggered");
        private static readonly int DestroyTileActivatedTrigger = Animator.StringToHash("DestroyTileActivated");
        private static readonly int DestroyTileDeactivatedTrigger = Animator.StringToHash("DestroyTileDeactivated");
        private static readonly int DestroyTileActiveBool = Animator.StringToHash("DestroyTileActive");
        private static readonly int DestroyTileActiveState = Animator.StringToHash("DestroyTileActiveIdle");
        private static readonly int DestroyTileInactiveState = Animator.StringToHash("DestroyTileInactiveIdle");
        private static readonly int SlideTileRedirectedTrigger = Animator.StringToHash("SlideTileRedirected");
        private static readonly int BarricadeBlockedTrigger = Animator.StringToHash("BarricadeBlocked");
        private static readonly int BarricadeCrushedTrigger = Animator.StringToHash("BarricadeCrushed");
        private static readonly int BarricadeActivatedTrigger = Animator.StringToHash("BarricadeActivated");
        private static readonly int BarricadeDeactivatedTrigger = Animator.StringToHash("BarricadeDeactivated");
        private static readonly int BarricadeActiveBool = Animator.StringToHash("BarricadeActive");
        private static readonly int BarricadeRaisedState = Animator.StringToHash("RaisedIdle");
        private static readonly int BarricadeLoweredState = Animator.StringToHash("LoweredIdle");
        private static readonly int ExitOpenedTrigger = Animator.StringToHash("ExitOpened");
        private static readonly int ExitEnteredTrigger = Animator.StringToHash("ExitEntered");
        private static readonly int ExitOpenBool = Animator.StringToHash("ExitOpen");
        private static readonly int ExitOpenedState = Animator.StringToHash("ExitOpenedIdle");
        private static readonly int ExitClosedState = Animator.StringToHash("ExitClosedIdle");
        private static readonly int MoonBlockGeneratedTrigger = Animator.StringToHash("MoonBlockGenerated");
        private static readonly int MoonBlockGeneratorBlockedTrigger = Animator.StringToHash("MoonBlockGeneratorBlocked");

        [SerializeField] private TileFeatureVisualTargetView targetView;
        [SerializeField] private Animator animator;
        [SerializeField] private TileFeatureVisualProfile[] profiles;

        private readonly ButtonTileFeatureVisualHandler buttonHandler = new();
        private readonly DestroyTileFeatureVisualHandler destroyHandler = new();
        private readonly SlideTileFeatureVisualHandler slideHandler = new();
        private readonly BarricadeTileFeatureVisualHandler barricadeHandler = new();
        private readonly ExitTileFeatureVisualHandler exitHandler = new();
        private readonly MoonBlockGeneratorTileFeatureVisualHandler moonGeneratorHandler = new();
        private readonly GenericTileFeatureVfxHandler genericHandler = new();
        private readonly HashSet<string> missingProfileWarningKeys = new();

        private bool hasBarricadeActiveImmediateState;
        private bool lastBarricadeActiveImmediateState;
        private bool hasBarricadeFallbackAnimatorActiveState;
        private bool lastBarricadeFallbackAnimatorActiveState;
        private bool hasExitFallbackAnimatorOpenState;
        private bool lastExitFallbackAnimatorOpenState;
        private IGameplayVfxPlaybackPort gameplayVfxPlaybackPort;

        public int DebugPlayButtonActivatedCount { get; private set; }
        public int DebugPlayDestroyTileTriggeredCount { get; private set; }
        public int DebugPlayDestroyTileActivatedCount { get; private set; }
        public int DebugPlayDestroyTileDeactivatedCount { get; private set; }
        public bool DebugDestroyTileActive { get; private set; }
        public bool DebugSlideTileActive { get; private set; }
        public int DebugPlaySlideTileRedirectedCount { get; private set; }
        public int DebugPlayBarricadeBlockedCount { get; private set; }
        public int DebugPlayBarricadeCrushedCount { get; private set; }
        public int DebugPlayBarricadeActivatedCount { get; private set; }
        public int DebugPlayBarricadeDeactivatedCount { get; private set; }
        public int DebugBarricadeBlockedCount => DebugPlayBarricadeBlockedCount;
        public int DebugBarricadeCrushedCount => DebugPlayBarricadeCrushedCount;
        public int DebugBarricadeActivatedCount => DebugPlayBarricadeActivatedCount;
        public int DebugBarricadeDeactivatedCount => DebugPlayBarricadeDeactivatedCount;
        public int DebugBarricadeActiveImmediateStatePlayCount { get; private set; }
        public int DebugBarricadeActiveAnimatorStatePlayCount { get; private set; }
        public int DebugLastBarricadeActiveAnimatorStateHash { get; private set; }
        public int DebugLegacyAnimatorFallbackCount { get; private set; }
        public int DebugExitLegacyAnimatorFallbackCount { get; private set; }
        public int DebugExitOpenAnimatorStatePlayCount { get; private set; }
        public int DebugLastExitOpenAnimatorStateHash { get; private set; }
        public int DebugExitProfileOpenStatePlayCount => exitHandler.DebugOpenStateAnimatorStatePlayCount;
        public int DebugLastExitProfileOpenStateHash => exitHandler.DebugLastOpenStateAnimatorStateHash;
        public int DebugPlayExitOpenedCount { get; private set; }
        public int DebugExitOpenedCount => DebugPlayExitOpenedCount;
        public int DebugPlayExitEnteredCount { get; private set; }
        public int DebugExitEnteredCount => DebugPlayExitEnteredCount;
        public bool DebugExitOpen { get; private set; }
        public int DebugMoonBlockGeneratedCount { get; private set; }
        public int DebugMoonBlockGeneratorBlockedCount { get; private set; }
        public int DebugMoonBlockGeneratorBlockedUnitCount { get; private set; }
        public int DebugMoonBlockGeneratorBlockedWallLikeSolidCount { get; private set; }
        public int DebugMoonBlockGeneratorBlockedPlacementCount { get; private set; }
        public Direction DebugLastSlideTileDirection { get; private set; } = Direction.None;
        public Direction DebugLastBarricadeBlockedDirection { get; private set; } = Direction.None;
        public int DebugLastSlideTileTargetEntityId { get; private set; }
        public int DebugLastBarricadeBlockedTargetEntityId { get; private set; }
        public int DebugLastBarricadeCrushedTargetEntityId { get; private set; }
        public int DebugLastExitEnteredPlayerEntityId { get; private set; }
        public int DebugLastMoonBlockGeneratedEntityId { get; private set; }
        public MoonBlockGeneratorBlockedPayload DebugLastMoonBlockGeneratorBlockedPayload { get; private set; }
        public bool IsGameplayPresentationPaused { get; private set; }

        public Animator Animator
        {
            get
            {
                if (animator == null)
                {
                    animator = GetComponentInChildren<Animator>(includeInactive: true);
                }

                return animator;
            }
        }

        public void ConfigureTarget(TileFeatureVisualTargetView target)
        {
            targetView = target;
        }

        public void AttachGameplayVfxPlaybackPort(IGameplayVfxPlaybackPort playbackPort)
        {
            gameplayVfxPlaybackPort = playbackPort;
        }

        public bool TryHandle(in TileFeatureVisualRequest request)
        {
            TrackDebug(request);
            var target = ResolveTarget();
            var profile = ResolveProfile(request.FeatureKind);
            var handler = ResolveHandler(request);
            WarnMissingProfileIfNeeded(request, profile, handler);
            var handled = handler.TryHandle(request, target, profile, Animator, gameplayVfxPlaybackPort);
            if (profile == null)
            {
                ApplyLegacyAnimatorFallback(request);
            }

            return handled;
        }

        public void PlayButtonActivated()
        {
            TryHandle(CreateRequest(TileFeatureVisualCueId.ButtonActivated, TileFeatureKind.Button));
        }

        public void PlayDestroyTileTriggered()
        {
            TryHandle(CreateRequest(TileFeatureVisualCueId.DestroyTileTriggered, TileFeatureKind.Destroy));
        }

        public void PlayDestroyTileActivated()
        {
            TryHandle(CreateRequest(TileFeatureVisualCueId.DestroyTileActivated, TileFeatureKind.Destroy, active: true));
        }

        public void PlayDestroyTileDeactivated()
        {
            TryHandle(CreateRequest(TileFeatureVisualCueId.DestroyTileDeactivated, TileFeatureKind.Destroy));
        }

        public void SetDestroyTileActiveImmediate(bool active)
        {
            TryHandle(CreateRequest(TileFeatureVisualCueId.DestroyTileActiveState, TileFeatureKind.Destroy, active: active));
        }

        public void SetTileFeatureActiveImmediate(TileFeatureKind kind, bool active)
        {
            switch (kind)
            {
                case TileFeatureKind.Destroy:
                    SetDestroyTileActiveImmediate(active);
                    return;
                case TileFeatureKind.Slide:
                    TryHandle(CreateRequest(TileFeatureVisualCueId.SlideTileActiveState, TileFeatureKind.Slide, active: active));
                    return;
            }
        }

        private void WarnMissingProfileIfNeeded(
            in TileFeatureVisualRequest request,
            TileFeatureVisualProfile profile,
            ITileFeatureVisualHandler handler)
        {
            if (profile != null ||
                handler == null ||
                !handler.CanHandle(request.CueId))
            {
                return;
            }

            if (!ShouldWarnMissingProfile(request))
            {
                return;
            }

            var key = $"{request.FeatureKind}:{request.CueId}";
            if (!missingProfileWarningKeys.Add(key))
            {
                return;
            }

            UnityEngine.Debug.LogWarning(
                $"{nameof(LegacyTileFeatureVisualCueAdapter)} handled {request.FeatureKind} cue {request.CueId} without a {nameof(TileFeatureVisualProfile)}. Production Animator-backed TileFeature visuals should resolve profile-local cue bindings.",
                this);
        }

        private bool ShouldWarnMissingProfile(in TileFeatureVisualRequest request)
        {
            if (request.FeatureKind == TileFeatureKind.MoonBlockGenerator)
            {
                return true;
            }

            if (request.FeatureKind != TileFeatureKind.Exit)
            {
                return false;
            }

            var resolvedAnimator = Animator;
            return resolvedAnimator != null &&
                   resolvedAnimator.runtimeAnimatorController != null;
        }

        public void PlaySlideTileRedirected(Direction direction, int targetEntityId)
        {
            TryHandle(CreateRequest(
                TileFeatureVisualCueId.SlideTileRedirected,
                TileFeatureKind.Slide,
                direction,
                targetEntityId));
        }

        public void PlayBarricadeBlocked(Direction direction, int targetEntityId)
        {
            TryHandle(CreateRequest(
                TileFeatureVisualCueId.BarricadeBlocked,
                TileFeatureKind.Barricade,
                direction,
                targetEntityId,
                active: true));
        }

        public void PlayBarricadeCrushed(int targetEntityId)
        {
            TryHandle(CreateRequest(
                TileFeatureVisualCueId.BarricadeCrushed,
                TileFeatureKind.Barricade,
                targetEntityId: targetEntityId));
        }

        public void PlayBarricadeActivated()
        {
            TryHandle(CreateRequest(TileFeatureVisualCueId.BarricadeActivated, TileFeatureKind.Barricade, active: true));
        }

        public void PlayBarricadeDeactivated()
        {
            TryHandle(CreateRequest(TileFeatureVisualCueId.BarricadeDeactivated, TileFeatureKind.Barricade));
        }

        public void SetBarricadeActiveImmediate(bool active)
        {
            TryHandle(CreateRequest(TileFeatureVisualCueId.BarricadeActiveState, TileFeatureKind.Barricade, active: active));
        }

        public void PlayExitOpened()
        {
            TryHandle(CreateRequest(TileFeatureVisualCueId.ExitOpened, TileFeatureKind.Exit, active: true));
        }

        public void PlayExitEntered(int playerEntityId)
        {
            TryHandle(CreateRequest(
                TileFeatureVisualCueId.ExitEntered,
                TileFeatureKind.Exit,
                targetEntityId: playerEntityId));
        }

        public void SetExitOpenImmediate(bool open)
        {
            TryHandle(CreateRequest(TileFeatureVisualCueId.ExitOpenState, TileFeatureKind.Exit, active: open));
        }

        public void PlayMoonBlockGenerated(int moonBlockEntityId)
        {
            TryHandle(CreateRequest(
                TileFeatureVisualCueId.MoonBlockGenerated,
                TileFeatureKind.MoonBlockGenerator,
                targetEntityId: moonBlockEntityId));
        }

        public void PlayMoonBlockGeneratorBlocked(MoonBlockGeneratorBlockedPayload payload)
        {
            var request = new TileFeatureVisualRequest(
                TileFeatureVisualCueId.MoonBlockGeneratorBlocked,
                ResolveTarget().TileId,
                ResolveTarget().Cell,
                TileFeatureKind.MoonBlockGenerator,
                moonBlockGeneratorBlockedPayload: payload);
            TryHandle(request);
        }

        public void SetPresentationPaused(bool paused)
        {
            IsGameplayPresentationPaused = paused;
        }

        internal void ResetBarricadeActiveImmediateState()
        {
            hasBarricadeActiveImmediateState = false;
            lastBarricadeActiveImmediateState = false;
            hasBarricadeFallbackAnimatorActiveState = false;
            lastBarricadeFallbackAnimatorActiveState = false;
            hasExitFallbackAnimatorOpenState = false;
            lastExitFallbackAnimatorOpenState = false;
            DebugBarricadeActiveImmediateStatePlayCount = 0;
            DebugBarricadeActiveAnimatorStatePlayCount = 0;
            DebugLastBarricadeActiveAnimatorStateHash = 0;
            DebugExitOpenAnimatorStatePlayCount = 0;
            DebugLastExitOpenAnimatorStateHash = 0;
            barricadeHandler.ResetActiveStateCache();
            exitHandler.ResetOpenStateCache();
        }

        private ITileFeatureVisualHandler ResolveHandler(in TileFeatureVisualRequest request)
        {
            switch (request.FeatureKind)
            {
                case TileFeatureKind.Button:
                    return buttonHandler;
                case TileFeatureKind.Destroy:
                    return destroyHandler;
                case TileFeatureKind.Slide:
                    return slideHandler;
                case TileFeatureKind.Barricade:
                    return barricadeHandler;
                case TileFeatureKind.Exit:
                case TileFeatureKind.Entrance:
                    return exitHandler;
                case TileFeatureKind.MoonBlockGenerator:
                    return moonGeneratorHandler;
                default:
                    return genericHandler;
            }
        }

        private TileFeatureVisualProfile ResolveProfile(TileFeatureKind featureKind)
        {
            if (profiles != null)
            {
                for (var i = 0; i < profiles.Length; i++)
                {
                    if (profiles[i] != null &&
                        profiles[i].FeatureKind == featureKind)
                    {
                        return profiles[i];
                    }
                }
            }

            return null;
        }

        private TileFeatureVisualTargetView ResolveTarget()
        {
            if (targetView == null)
            {
                targetView = GetComponent<TileFeatureVisualTargetView>();
            }

            if (targetView == null)
            {
                throw new InvalidOperationException($"{nameof(LegacyTileFeatureVisualCueAdapter)} requires a {nameof(TileFeatureVisualTargetView)}.");
            }

            return targetView;
        }

        private TileFeatureVisualRequest CreateRequest(
            TileFeatureVisualCueId cueId,
            TileFeatureKind featureKind,
            Direction direction = Direction.None,
            int targetEntityId = 0,
            bool active = false)
        {
            var target = ResolveTarget();
            return new TileFeatureVisualRequest(
                cueId,
                target.TileId,
                target.Cell,
                featureKind,
                direction,
                targetEntityId,
                active: active);
        }

        private void TrackDebug(in TileFeatureVisualRequest request)
        {
            switch (request.CueId)
            {
                case TileFeatureVisualCueId.ButtonActivated:
                    DebugPlayButtonActivatedCount++;
                    return;
                case TileFeatureVisualCueId.DestroyTileTriggered:
                    DebugPlayDestroyTileTriggeredCount++;
                    return;
                case TileFeatureVisualCueId.DestroyTileActivated:
                    DebugPlayDestroyTileActivatedCount++;
                    DebugDestroyTileActive = true;
                    return;
                case TileFeatureVisualCueId.DestroyTileDeactivated:
                    DebugPlayDestroyTileDeactivatedCount++;
                    DebugDestroyTileActive = false;
                    return;
                case TileFeatureVisualCueId.DestroyTileActiveState:
                    DebugDestroyTileActive = request.Active;
                    return;
                case TileFeatureVisualCueId.SlideTileRedirected:
                    DebugPlaySlideTileRedirectedCount++;
                    DebugLastSlideTileDirection = request.Direction;
                    DebugLastSlideTileTargetEntityId = request.TargetEntityId;
                    return;
                case TileFeatureVisualCueId.SlideTileActiveState:
                    DebugSlideTileActive = request.Active;
                    return;
                case TileFeatureVisualCueId.BarricadeBlocked:
                    DebugPlayBarricadeBlockedCount++;
                    DebugLastBarricadeBlockedDirection = request.Direction;
                    DebugLastBarricadeBlockedTargetEntityId = request.TargetEntityId;
                    MarkBarricadeActiveImmediateState(true);
                    return;
                case TileFeatureVisualCueId.BarricadeCrushed:
                    DebugPlayBarricadeCrushedCount++;
                    DebugLastBarricadeCrushedTargetEntityId = request.TargetEntityId;
                    return;
                case TileFeatureVisualCueId.BarricadeActivated:
                    DebugPlayBarricadeActivatedCount++;
                    MarkBarricadeActiveImmediateState(true);
                    return;
                case TileFeatureVisualCueId.BarricadeDeactivated:
                    DebugPlayBarricadeDeactivatedCount++;
                    MarkBarricadeActiveImmediateState(false);
                    return;
                case TileFeatureVisualCueId.BarricadeActiveState:
                    if (!hasBarricadeActiveImmediateState ||
                        lastBarricadeActiveImmediateState != request.Active)
                    {
                        DebugBarricadeActiveImmediateStatePlayCount++;
                    }

                    MarkBarricadeActiveImmediateState(request.Active);
                    return;
                case TileFeatureVisualCueId.ExitOpened:
                    DebugPlayExitOpenedCount++;
                    DebugExitOpen = true;
                    return;
                case TileFeatureVisualCueId.ExitEntered:
                    DebugPlayExitEnteredCount++;
                    DebugLastExitEnteredPlayerEntityId = request.TargetEntityId;
                    return;
                case TileFeatureVisualCueId.ExitOpenState:
                    DebugExitOpen = request.Active;
                    return;
                case TileFeatureVisualCueId.MoonBlockGenerated:
                    DebugMoonBlockGeneratedCount++;
                    DebugLastMoonBlockGeneratedEntityId = request.TargetEntityId;
                    return;
                case TileFeatureVisualCueId.MoonBlockGeneratorBlocked:
                    DebugMoonBlockGeneratorBlockedCount++;
                    DebugLastMoonBlockGeneratorBlockedPayload = request.MoonBlockGeneratorBlockedPayload;
                    TrackMoonBlockGeneratorBlockedReason(request.MoonBlockGeneratorBlockedPayload.Reason);
                    return;
            }
        }

        private void TrackMoonBlockGeneratorBlockedReason(MoonBlockGeneratorBlockedReason reason)
        {
            switch (reason)
            {
                case MoonBlockGeneratorBlockedReason.UnitOccupant:
                    DebugMoonBlockGeneratorBlockedUnitCount++;
                    return;
                case MoonBlockGeneratorBlockedReason.WallLikeSolid:
                    DebugMoonBlockGeneratorBlockedWallLikeSolidCount++;
                    return;
                case MoonBlockGeneratorBlockedReason.PlacementBlocked:
                    DebugMoonBlockGeneratorBlockedPlacementCount++;
                    return;
            }
        }

        private void MarkBarricadeActiveImmediateState(bool active)
        {
            hasBarricadeActiveImmediateState = true;
            lastBarricadeActiveImmediateState = active;
        }

        private void ApplyLegacyAnimatorFallback(in TileFeatureVisualRequest request)
        {
            var resolvedAnimator = Animator;
            if (resolvedAnimator == null ||
                resolvedAnimator.runtimeAnimatorController == null)
            {
                return;
            }

            switch (request.CueId)
            {
                case TileFeatureVisualCueId.ButtonActivated:
                    TrackLegacyAnimatorFallback(request);
                    SetTriggerIfPresent(resolvedAnimator, ButtonActivatedTrigger);
                    return;
                case TileFeatureVisualCueId.DestroyTileTriggered:
                    TrackLegacyAnimatorFallback(request);
                    SetTriggerIfPresent(resolvedAnimator, DestroyTileTriggeredTrigger);
                    return;
                case TileFeatureVisualCueId.DestroyTileActivated:
                    TrackLegacyAnimatorFallback(request);
                    SetTriggerIfPresent(resolvedAnimator, DestroyTileActivatedTrigger);
                    SetBoolIfPresent(resolvedAnimator, DestroyTileActiveBool, true);
                    PlayStateIfPresent(resolvedAnimator, DestroyTileActiveState);
                    return;
                case TileFeatureVisualCueId.DestroyTileDeactivated:
                    TrackLegacyAnimatorFallback(request);
                    SetTriggerIfPresent(resolvedAnimator, DestroyTileDeactivatedTrigger);
                    SetBoolIfPresent(resolvedAnimator, DestroyTileActiveBool, false);
                    PlayStateIfPresent(resolvedAnimator, DestroyTileInactiveState);
                    return;
                case TileFeatureVisualCueId.DestroyTileActiveState:
                    TrackLegacyAnimatorFallback(request);
                    SetBoolIfPresent(resolvedAnimator, DestroyTileActiveBool, request.Active);
                    PlayStateIfPresent(resolvedAnimator, request.Active ? DestroyTileActiveState : DestroyTileInactiveState);
                    return;
                case TileFeatureVisualCueId.SlideTileRedirected:
                    TrackLegacyAnimatorFallback(request);
                    SetTriggerIfPresent(resolvedAnimator, SlideTileRedirectedTrigger);
                    return;
                case TileFeatureVisualCueId.BarricadeBlocked:
                    TrackLegacyAnimatorFallback(request);
                    SetTriggerIfPresent(resolvedAnimator, BarricadeBlockedTrigger);
                    SetBoolIfPresent(resolvedAnimator, BarricadeActiveBool, true);
                    MarkBarricadeFallbackAnimatorActiveState(true);
                    return;
                case TileFeatureVisualCueId.BarricadeCrushed:
                    TrackLegacyAnimatorFallback(request);
                    SetTriggerIfPresent(resolvedAnimator, BarricadeCrushedTrigger);
                    return;
                case TileFeatureVisualCueId.BarricadeActivated:
                    TrackLegacyAnimatorFallback(request);
                    SetTriggerIfPresent(resolvedAnimator, BarricadeActivatedTrigger);
                    SetBoolIfPresent(resolvedAnimator, BarricadeActiveBool, true);
                    PlayBarricadeFallbackStateIfPresent(resolvedAnimator, BarricadeRaisedState);
                    MarkBarricadeFallbackAnimatorActiveState(true);
                    return;
                case TileFeatureVisualCueId.BarricadeDeactivated:
                    TrackLegacyAnimatorFallback(request);
                    SetTriggerIfPresent(resolvedAnimator, BarricadeDeactivatedTrigger);
                    SetBoolIfPresent(resolvedAnimator, BarricadeActiveBool, false);
                    PlayBarricadeFallbackStateIfPresent(resolvedAnimator, BarricadeLoweredState);
                    MarkBarricadeFallbackAnimatorActiveState(false);
                    return;
                case TileFeatureVisualCueId.BarricadeActiveState:
                    TrackLegacyAnimatorFallback(request);
                    ApplyBarricadeActiveStateFallback(resolvedAnimator, request.Active);
                    return;
                case TileFeatureVisualCueId.ExitOpened:
                    TrackLegacyAnimatorFallback(request);
                    SetTriggerIfPresent(resolvedAnimator, ExitOpenedTrigger);
                    SetBoolIfPresent(resolvedAnimator, ExitOpenBool, true);
                    MarkExitFallbackAnimatorOpenState(true);
                    return;
                case TileFeatureVisualCueId.ExitEntered:
                    TrackLegacyAnimatorFallback(request);
                    SetTriggerIfPresent(resolvedAnimator, ExitEnteredTrigger);
                    return;
                case TileFeatureVisualCueId.ExitOpenState:
                    TrackLegacyAnimatorFallback(request);
                    SetBoolIfPresent(resolvedAnimator, ExitOpenBool, request.Active);
                    ApplyExitOpenStateFallback(resolvedAnimator, request.Active);
                    return;
                case TileFeatureVisualCueId.MoonBlockGenerated:
                    TrackLegacyAnimatorFallback(request);
                    SetTriggerIfPresent(resolvedAnimator, MoonBlockGeneratedTrigger);
                    return;
                case TileFeatureVisualCueId.MoonBlockGeneratorBlocked:
                    TrackLegacyAnimatorFallback(request);
                    SetTriggerIfPresent(resolvedAnimator, MoonBlockGeneratorBlockedTrigger);
                    return;
            }
        }

        private void TrackLegacyAnimatorFallback(in TileFeatureVisualRequest request)
        {
            DebugLegacyAnimatorFallbackCount++;
            if (request.FeatureKind == TileFeatureKind.Exit)
            {
                DebugExitLegacyAnimatorFallbackCount++;
            }
        }

        private static void SetTriggerIfPresent(Animator targetAnimator, int hash)
        {
            if (HasAnimatorParameter(targetAnimator, hash, AnimatorControllerParameterType.Trigger))
            {
                targetAnimator.SetTrigger(hash);
            }
        }

        private static void SetBoolIfPresent(Animator targetAnimator, int hash, bool value)
        {
            if (HasAnimatorParameter(targetAnimator, hash, AnimatorControllerParameterType.Bool))
            {
                targetAnimator.SetBool(hash, value);
            }
        }

        private void ApplyBarricadeActiveStateFallback(Animator targetAnimator, bool active)
        {
            SetBoolIfPresent(targetAnimator, BarricadeActiveBool, active);
            if (hasBarricadeFallbackAnimatorActiveState &&
                lastBarricadeFallbackAnimatorActiveState == active)
            {
                return;
            }

            MarkBarricadeFallbackAnimatorActiveState(active);
            PlayBarricadeFallbackStateIfPresent(
                targetAnimator,
                active ? BarricadeRaisedState : BarricadeLoweredState);
        }

        private void MarkBarricadeFallbackAnimatorActiveState(bool active)
        {
            hasBarricadeFallbackAnimatorActiveState = true;
            lastBarricadeFallbackAnimatorActiveState = active;
        }

        private void ApplyExitOpenStateFallback(Animator targetAnimator, bool open)
        {
            if (hasExitFallbackAnimatorOpenState &&
                lastExitFallbackAnimatorOpenState == open)
            {
                return;
            }

            MarkExitFallbackAnimatorOpenState(open);
            PlayExitFallbackStateIfPresent(
                targetAnimator,
                open ? ExitOpenedState : ExitClosedState);
        }

        private void MarkExitFallbackAnimatorOpenState(bool open)
        {
            hasExitFallbackAnimatorOpenState = true;
            lastExitFallbackAnimatorOpenState = open;
        }

        private void PlayBarricadeFallbackStateIfPresent(Animator targetAnimator, int hash)
        {
            if (PlayStateIfPresent(targetAnimator, hash))
            {
                DebugBarricadeActiveAnimatorStatePlayCount++;
                DebugLastBarricadeActiveAnimatorStateHash = hash;
            }
        }

        private void PlayExitFallbackStateIfPresent(Animator targetAnimator, int hash)
        {
            if (PlayStateIfPresent(targetAnimator, hash))
            {
                DebugExitOpenAnimatorStatePlayCount++;
                DebugLastExitOpenAnimatorStateHash = hash;
            }
        }

        private static bool PlayStateIfPresent(Animator targetAnimator, int hash)
        {
            if (targetAnimator.HasState(0, hash))
            {
                targetAnimator.Play(hash, 0, 1f);
                targetAnimator.Update(0f);
                return true;
            }

            return false;
        }

        private static bool HasAnimatorParameter(
            Animator targetAnimator,
            int hash,
            AnimatorControllerParameterType parameterType)
        {
            var parameters = targetAnimator.parameters;
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].nameHash == hash &&
                    parameters[i].type == parameterType)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
