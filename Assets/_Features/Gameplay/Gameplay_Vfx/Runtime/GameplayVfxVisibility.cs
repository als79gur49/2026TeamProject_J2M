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
    }

    public enum GameplayVfxVisibilityAllowReason
    {
        None = 0,
        DefaultGameplay = 1,
        VisibleSurfaceProjectionOptIn = 2,
        InactiveFaceExplicitOptIn = 3,
        PresentationOnly = 4,
    }

    public readonly struct GameplayVfxEntityVisibilityState
    {
        public GameplayVfxEntityVisibilityState(
            bool hasView,
            bool isViewActiveInHierarchy,
            bool hasSemanticState = false,
            bool isFrontFaceInactive = false,
            bool isGameplayAutonomySuppressed = false)
        {
            HasView = hasView;
            IsViewActiveInHierarchy = isViewActiveInHierarchy;
            HasSemanticState = hasSemanticState;
            IsFrontFaceInactive = isFrontFaceInactive;
            IsGameplayAutonomySuppressed = isGameplayAutonomySuppressed;
        }

        public bool HasView { get; }

        public bool IsViewActiveInHierarchy { get; }

        public bool HasSemanticState { get; }

        public bool IsFrontFaceInactive { get; }

        public bool IsGameplayAutonomySuppressed { get; }
    }

    public readonly struct GameplayVfxVisibilityContext
    {
        public GameplayVfxVisibilityContext(
            IReadOnlyDictionary<int, GameplayVfxEntityVisibilityState> entityStatesByEntityId)
        {
            EntityStatesByEntityId = entityStatesByEntityId;
        }

        public IReadOnlyDictionary<int, GameplayVfxEntityVisibilityState> EntityStatesByEntityId { get; }

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
            return Evaluate(
                new GameplayVfxVisibilityQuery(
                    request,
                    ResolveEffectiveMode(request, policy.VisibilityMode)),
                context);
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
                if (TryResolveCell(query, out var cell, out var topology) &&
                    !topology.IsFaceActive(cell.face))
                {
                    return GameplayVfxVisibilityDecision.Block(
                        GameplayVfxVisibilityBlockReason.InactiveFace);
                }
            }

            return GameplayVfxVisibilityDecision.Allow();
        }

        private static GameplayVfxVisibilityDecision EvaluateSourceEntity(
            in GameplayVfxVisibilityQuery query,
            in GameplayVfxVisibilityContext context)
        {
            var entityId = ResolvePrimaryEntityId(query);
            if (entityId <= 0 ||
                !context.TryGetEntityState(entityId, out var state))
            {
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

            return GameplayVfxVisibilityDecision.Allow();
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
