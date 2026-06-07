using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Vfx
{
    public enum GameplayVfxVisibilityMode
    {
        DefaultGameplay = 0,
        ActiveGameplayFaceOnly = 1,
        EntitySemanticActiveOnly = 2,
        VisibleSurfaceAllowed = 3,
        InactiveFaceExplicitlyAllowed = 4,
        PresentationOnly = 5,
    }

    public enum GameplayVfxVisibilityBlockReason
    {
        None = 0,
        InactiveFace = 1,
        FrontFaceInactive = 2,
        GameplayAutonomySuppressed = 3,
        EntityViewMissing = 4,
        EntityViewInactive = 5,
        AnchorProjectionFailed = 6,
        CuePolicyDisallowsVisibleSurface = 7,
        CuePolicyDisallowsInactiveFace = 8,
        MissingSemanticState = 9,
        JumpTopologySuspended = 10,
        JumpWindupSourceTopologyMismatch = 11,
    }

    public enum GameplayVfxVisibilityAllowReason
    {
        None = 0,
        DefaultGameplay = 1,
        VisibleSurfaceProjectionOptIn = 2,
        InactiveFaceExplicitOptIn = 3,
        PresentationOnly = 4,
    }

    public enum GameplayVfxVisibilityPolicySource
    {
        None = 0,
        BindingRuntimePolicy = 1,
        FallbackDefaultGameplay = 2,
    }

    public enum GameplayVfxPresentationOnlyUsageKind
    {
        None = 0,
        AllowedTopologyHelper = 1,
        MisuseCandidate = 2,
    }

    public readonly struct GameplayVfxPresentationOnlyUsageDiagnostic
    {
        public GameplayVfxPresentationOnlyUsageDiagnostic(
            GameplayVfxPresentationOnlyUsageKind kind,
            GameplayVfxCueId cueId,
            GameplayVfxVisibilityPolicySource source)
        {
            Kind = kind;
            CueId = cueId;
            Source = source;
        }

        public GameplayVfxPresentationOnlyUsageKind Kind { get; }

        public GameplayVfxCueId CueId { get; }

        public GameplayVfxVisibilityPolicySource Source { get; }

        public bool IsPresentationOnly => Kind != GameplayVfxPresentationOnlyUsageKind.None;

        public bool IsMisuseCandidate => Kind == GameplayVfxPresentationOnlyUsageKind.MisuseCandidate;
    }

    public readonly struct GameplayVfxResolvedVisibilityPolicy
    {
        public GameplayVfxResolvedVisibilityPolicy(
            GameplayVfxVisibilityMode visibilityMode,
            GameplayVfxVisibilityMode effectiveMode,
            GameplayVfxVisibilityPolicySource source)
        {
            VisibilityMode = visibilityMode;
            EffectiveMode = effectiveMode;
            Source = source;
        }

        public GameplayVfxVisibilityMode VisibilityMode { get; }

        public GameplayVfxVisibilityMode EffectiveMode { get; }

        public GameplayVfxVisibilityPolicySource Source { get; }

        public bool IsFallbackDefaultGameplay =>
            Source == GameplayVfxVisibilityPolicySource.FallbackDefaultGameplay;
    }

    public readonly struct GameplayVfxEntityVisibilityState
    {
        public GameplayVfxEntityVisibilityState(
            bool hasView,
            bool isViewActiveInHierarchy,
            bool hasSemanticState = false,
            bool isFrontFaceInactive = false,
            bool isGameplayAutonomySuppressed = false,
            bool isJumpTopologySuspended = false)
        {
            HasView = hasView;
            IsViewActiveInHierarchy = isViewActiveInHierarchy;
            HasSemanticState = hasSemanticState;
            IsFrontFaceInactive = isFrontFaceInactive;
            IsGameplayAutonomySuppressed = isGameplayAutonomySuppressed;
            IsJumpTopologySuspended = isJumpTopologySuspended;
        }

        public bool HasView { get; }

        public bool IsViewActiveInHierarchy { get; }

        public bool HasSemanticState { get; }

        public bool IsFrontFaceInactive { get; }

        public bool IsGameplayAutonomySuppressed { get; }

        public bool IsJumpTopologySuspended { get; }
    }

    public readonly struct GameplayVfxVisibilityContext
    {
        public GameplayVfxVisibilityContext(
            IReadOnlyDictionary<int, GameplayVfxEntityVisibilityState> entityStatesByEntityId,
            bool requireSourceSemanticState = false)
        {
            EntityStatesByEntityId = entityStatesByEntityId;
            RequireSourceSemanticState = requireSourceSemanticState;
        }

        public IReadOnlyDictionary<int, GameplayVfxEntityVisibilityState> EntityStatesByEntityId { get; }

        public bool RequireSourceSemanticState { get; }

        public bool TryGetEntityState(int entityId, out GameplayVfxEntityVisibilityState state)
        {
            if (entityId > 0 &&
                EntityStatesByEntityId != null &&
                EntityStatesByEntityId.TryGetValue(entityId, out state))
            {
                return true;
            }

            state = default;
            return false;
        }
    }

    public readonly struct GameplayVfxVisibilityQuery
    {
        public GameplayVfxVisibilityQuery(
            GameplayVfxRequest request,
            GameplayVfxVisibilityMode visibilityMode,
            VfxResolvedAnchor resolvedAnchor = default,
            bool hasResolvedAnchor = false)
        {
            Request = request;
            VisibilityMode = visibilityMode;
            ResolvedAnchor = resolvedAnchor;
            HasResolvedAnchor = hasResolvedAnchor;
        }

        public GameplayVfxRequest Request { get; }

        public GameplayVfxVisibilityMode VisibilityMode { get; }

        public VfxResolvedAnchor ResolvedAnchor { get; }

        public bool HasResolvedAnchor { get; }
    }

    public readonly struct GameplayVfxVisibilityDecision
    {
        private GameplayVfxVisibilityDecision(
            bool isVisible,
            GameplayVfxVisibilityBlockReason blockReason,
            GameplayVfxVisibilityAllowReason allowReason)
        {
            IsVisible = isVisible;
            BlockReason = blockReason;
            AllowReason = allowReason;
        }

        public bool IsVisible { get; }

        public GameplayVfxVisibilityBlockReason BlockReason { get; }

        public GameplayVfxVisibilityAllowReason AllowReason { get; }

        public static GameplayVfxVisibilityDecision Allow(
            GameplayVfxVisibilityAllowReason reason = GameplayVfxVisibilityAllowReason.DefaultGameplay)
        {
            return new GameplayVfxVisibilityDecision(true, GameplayVfxVisibilityBlockReason.None, reason);
        }

        public static GameplayVfxVisibilityDecision Block(GameplayVfxVisibilityBlockReason reason)
        {
            return new GameplayVfxVisibilityDecision(false, reason, GameplayVfxVisibilityAllowReason.None);
        }
    }

    public static class GameplayVfxVisibilityPolicy
    {
        public static GameplayVfxVisibilityDecision EvaluateBeforeAnchor(
            in GameplayVfxRequest request,
            VfxBindingRuntimePolicy policy,
            in GameplayVfxVisibilityContext context)
        {
            var resolvedPolicy = ResolveBindingRuntimePolicy(request, policy);
            return EvaluateBeforeAnchor(request, resolvedPolicy, context);
        }

        public static GameplayVfxVisibilityDecision EvaluateBeforeAnchor(
            in GameplayVfxRequest request,
            GameplayVfxVisibilityMode visibilityMode,
            in GameplayVfxVisibilityContext context)
        {
            return Evaluate(
                new GameplayVfxVisibilityQuery(
                    request,
                    ResolveEffectiveMode(request, visibilityMode)),
                context);
        }

        public static GameplayVfxVisibilityDecision EvaluateBeforeAnchor(
            in GameplayVfxRequest request,
            in GameplayVfxResolvedVisibilityPolicy resolvedPolicy,
            in GameplayVfxVisibilityContext context)
        {
            return Evaluate(
                new GameplayVfxVisibilityQuery(
                    request,
                    resolvedPolicy.EffectiveMode),
                context);
        }

        public static GameplayVfxVisibilityDecision EvaluateAfterAnchor(
            in GameplayVfxRequest request,
            VfxBindingRuntimePolicy policy,
            in VfxResolvedAnchor resolvedAnchor,
            in GameplayVfxVisibilityContext context)
        {
            return Evaluate(
                new GameplayVfxVisibilityQuery(
                    request,
                    ResolveEffectiveMode(request, policy.VisibilityMode),
                    resolvedAnchor,
                    hasResolvedAnchor: true),
                context);
        }

        public static GameplayVfxVisibilityMode ResolveEffectiveMode(
            in GameplayVfxRequest request,
            GameplayVfxVisibilityMode visibilityMode)
        {
            if (visibilityMode != GameplayVfxVisibilityMode.DefaultGameplay)
            {
                return visibilityMode;
            }

            return request.Anchor.Kind == VfxAnchorKind.Entity ||
                   request.Anchor.Kind == VfxAnchorKind.EntitySlot ||
                   request.Anchor.Kind == VfxAnchorKind.EntityToCell
                ? GameplayVfxVisibilityMode.EntitySemanticActiveOnly
                : GameplayVfxVisibilityMode.ActiveGameplayFaceOnly;
        }

        public static GameplayVfxResolvedVisibilityPolicy ResolveBindingRuntimePolicy(
            in GameplayVfxRequest request,
            VfxBindingRuntimePolicy policy)
        {
            return Resolve(
                request,
                policy.VisibilityMode,
                GameplayVfxVisibilityPolicySource.BindingRuntimePolicy);
        }

        public static GameplayVfxResolvedVisibilityPolicy ResolveFallbackDefaultGameplay(
            in GameplayVfxRequest request)
        {
            return Resolve(
                request,
                GameplayVfxVisibilityMode.DefaultGameplay,
                GameplayVfxVisibilityPolicySource.FallbackDefaultGameplay);
        }

        public static GameplayVfxResolvedVisibilityPolicy ResolveFinalPolicy(
            in GameplayVfxRequest request,
            bool hasBindingPolicy,
            VfxBindingRuntimePolicy policy)
        {
            return hasBindingPolicy
                ? ResolveBindingRuntimePolicy(request, policy)
                : ResolveFallbackDefaultGameplay(request);
        }

        public static GameplayVfxPresentationOnlyUsageDiagnostic ClassifyPresentationOnlyUsage(
            in GameplayVfxRequest request,
            in GameplayVfxResolvedVisibilityPolicy resolvedPolicy)
        {
            if (resolvedPolicy.VisibilityMode != GameplayVfxVisibilityMode.PresentationOnly)
            {
                return default;
            }

            var allowedTopologyHelper =
                GameplayVfxTopologyHelperExemptionPolicy.IsTopologyHelperCue(request.CueId) &&
                (request.TopologyStopMode == GameplayVfxTopologyStopMode.TopologyHelperExempt ||
                 request.TopologySpawnMode == GameplayVfxTopologySpawnMode.TopologyHelperExempt ||
                 request.Timing == VfxTimingKind.QueuedUntilTopologyTransitionEnd);

            return new GameplayVfxPresentationOnlyUsageDiagnostic(
                allowedTopologyHelper
                    ? GameplayVfxPresentationOnlyUsageKind.AllowedTopologyHelper
                    : GameplayVfxPresentationOnlyUsageKind.MisuseCandidate,
                request.CueId,
                resolvedPolicy.Source);
        }

        private static GameplayVfxResolvedVisibilityPolicy Resolve(
            in GameplayVfxRequest request,
            GameplayVfxVisibilityMode visibilityMode,
            GameplayVfxVisibilityPolicySource source)
        {
            return new GameplayVfxResolvedVisibilityPolicy(
                visibilityMode,
                ResolveEffectiveMode(request, visibilityMode),
                source);
        }

        public static GameplayVfxVisibilityDecision Evaluate(
            in GameplayVfxVisibilityQuery query,
            in GameplayVfxVisibilityContext context)
        {
            if (query.VisibilityMode == GameplayVfxVisibilityMode.PresentationOnly)
            {
                return GameplayVfxVisibilityDecision.Allow(
                    GameplayVfxVisibilityAllowReason.PresentationOnly);
            }

            var semanticDecision = EvaluateSourceEntity(query, context);
            if (!semanticDecision.IsVisible)
            {
                return semanticDecision;
            }

            if (query.VisibilityMode == GameplayVfxVisibilityMode.EntitySemanticActiveOnly)
            {
                var entityId = ResolvePrimaryEntityId(query);
                if (entityId > 0 &&
                    !context.TryGetEntityState(entityId, out _) &&
                    query.Request.CueId.Family == GameplayVfxFamily.Enemy)
                {
                    return GameplayVfxVisibilityDecision.Block(
                        GameplayVfxVisibilityBlockReason.MissingSemanticState);
                }
            }

            if (query.VisibilityMode == GameplayVfxVisibilityMode.VisibleSurfaceAllowed)
            {
                return GameplayVfxVisibilityDecision.Allow(
                    GameplayVfxVisibilityAllowReason.VisibleSurfaceProjectionOptIn);
            }

            if (query.VisibilityMode == GameplayVfxVisibilityMode.InactiveFaceExplicitlyAllowed)
            {
                return GameplayVfxVisibilityDecision.Allow(
                    GameplayVfxVisibilityAllowReason.InactiveFaceExplicitOptIn);
            }

            if (query.VisibilityMode == GameplayVfxVisibilityMode.ActiveGameplayFaceOnly ||
                query.VisibilityMode == GameplayVfxVisibilityMode.EntitySemanticActiveOnly)
            {
                if (TryResolveCell(query, out var cell, out var topology))
                {
                    var sourceAnchoredDecision = EvaluateSourceAnchoredJumpTarget(query, topology);
                    if (!sourceAnchoredDecision.IsVisible)
                    {
                        return sourceAnchoredDecision;
                    }

                    if (!topology.IsFaceActive(cell.face))
                    {
                        return GameplayVfxVisibilityDecision.Block(
                            GameplayVfxVisibilityBlockReason.InactiveFace);
                    }
                }
            }

            return GameplayVfxVisibilityDecision.Allow();
        }

        private static GameplayVfxVisibilityDecision EvaluateSourceAnchoredJumpTarget(
            in GameplayVfxVisibilityQuery query,
            CubeTopologyState validationTopology)
        {
            if (!query.Request.CueId.Equals(GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget)))
            {
                return GameplayVfxVisibilityDecision.Allow();
            }

            var metadata = query.Request.JumpTargetVisibility;
            if (!metadata.HasValue ||
                metadata.Phase != GameplayVfxJumpTargetPhase.Windup)
            {
                return GameplayVfxVisibilityDecision.Allow();
            }

            return metadata.OwnerSourceCell.face == validationTopology.BottomFace &&
                   metadata.RequestSourceTopology.BottomFace == validationTopology.BottomFace
                ? GameplayVfxVisibilityDecision.Allow()
                : GameplayVfxVisibilityDecision.Block(
                    GameplayVfxVisibilityBlockReason.JumpWindupSourceTopologyMismatch);
        }

        private static GameplayVfxVisibilityDecision EvaluateSourceEntity(
            in GameplayVfxVisibilityQuery query,
            in GameplayVfxVisibilityContext context)
        {
            var entityId = ResolvePrimaryEntityId(query);
            if (entityId <= 0 ||
                !context.TryGetEntityState(entityId, out var state))
            {
                if (RequiresSourceSemanticState(query, context))
                {
                    return GameplayVfxVisibilityDecision.Block(
                        GameplayVfxVisibilityBlockReason.MissingSemanticState);
                }

                return GameplayVfxVisibilityDecision.Allow();
            }

            if (!state.HasView)
            {
                return GameplayVfxVisibilityDecision.Block(
                    GameplayVfxVisibilityBlockReason.EntityViewMissing);
            }

            if (!state.IsViewActiveInHierarchy)
            {
                return GameplayVfxVisibilityDecision.Block(
                    GameplayVfxVisibilityBlockReason.EntityViewInactive);
            }

            if (RequiresSourceSemanticState(query, context) && !state.HasSemanticState)
            {
                return GameplayVfxVisibilityDecision.Block(
                    GameplayVfxVisibilityBlockReason.MissingSemanticState);
            }

            if (state.HasSemanticState && state.IsFrontFaceInactive)
            {
                return GameplayVfxVisibilityDecision.Block(
                    GameplayVfxVisibilityBlockReason.FrontFaceInactive);
            }

            if (state.HasSemanticState && state.IsGameplayAutonomySuppressed)
            {
                return GameplayVfxVisibilityDecision.Block(
                    GameplayVfxVisibilityBlockReason.GameplayAutonomySuppressed);
            }

            if (state.IsJumpTopologySuspended)
            {
                return GameplayVfxVisibilityDecision.Block(
                    GameplayVfxVisibilityBlockReason.JumpTopologySuspended);
            }

            return GameplayVfxVisibilityDecision.Allow();
        }

        private static bool RequiresSourceSemanticState(
            in GameplayVfxVisibilityQuery query,
            in GameplayVfxVisibilityContext context)
        {
            return context.RequireSourceSemanticState &&
                   query.Request.CueId.Equals(GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget)) &&
                   ResolvePrimaryEntityId(query) > 0;
        }

        private static int ResolvePrimaryEntityId(in GameplayVfxVisibilityQuery query)
        {
            if (query.HasResolvedAnchor && query.ResolvedAnchor.HasEntity)
            {
                return query.ResolvedAnchor.EntityId;
            }

            var anchor = query.Request.Anchor;
            if (anchor.EntityId > 0)
            {
                return anchor.EntityId;
            }

            if (anchor.SourceEntityId > 0)
            {
                return anchor.SourceEntityId;
            }

            if (anchor.TargetEntityId > 0)
            {
                return anchor.TargetEntityId;
            }

            return query.Request.SourceEntityId;
        }

        private static bool TryResolveCell(
            in GameplayVfxVisibilityQuery query,
            out SurfaceCell cell,
            out CubeTopologyState topology)
        {
            if (query.HasResolvedAnchor && query.ResolvedAnchor.HasCell)
            {
                cell = query.ResolvedAnchor.Cell;
                topology = query.ResolvedAnchor.Topology;
                return true;
            }

            var anchor = query.Request.Anchor;
            if (anchor.HasCell)
            {
                cell = anchor.Cell;
                topology = anchor.Topology;
                return true;
            }

            cell = default;
            topology = default;
            return false;
        }
    }
}
