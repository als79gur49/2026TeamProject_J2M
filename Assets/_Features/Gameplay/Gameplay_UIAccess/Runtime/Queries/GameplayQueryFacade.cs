using System;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.Gameplay.UIAccess.Models;

namespace Game.Feature.Gameplay.UIAccess.Queries
{
    public sealed class GameplayQueryFacade : IGameplayQueryFacade, IGameplayHudQueryRevisions, IGameplayHudContentInvalidation
    {
        public GameplayQueryFacade(
            IGameplaySessionQuery session,
            IGameplayPlayerHudQuery playerHud,
            IGameplayObjectiveQuery objectives)
            : this(
                session,
                EmptyGameplayStageQuery.Instance,
                playerHud,
                objectives,
                EmptyGameplaySurfaceButtonRemainderQuery.Instance)
        {
        }

        public GameplayQueryFacade(
            IGameplaySessionQuery session,
            IGameplayStageQuery stage,
            IGameplayPlayerHudQuery playerHud,
            IGameplayObjectiveQuery objectives)
            : this(
                session,
                stage,
                playerHud,
                objectives,
                EmptyGameplaySurfaceButtonRemainderQuery.Instance)
        {
        }

        public GameplayQueryFacade(
            IGameplaySessionQuery session,
            IGameplayStageQuery stage,
            IGameplayPlayerHudQuery playerHud,
            IGameplayObjectiveQuery objectives,
            IGameplaySurfaceButtonRemainderQuery surfaceButtonRemainders)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Stage = stage ?? throw new ArgumentNullException(nameof(stage));
            PlayerHud = playerHud ?? throw new ArgumentNullException(nameof(playerHud));
            Objectives = objectives ?? throw new ArgumentNullException(nameof(objectives));
            SurfaceButtonRemainders = surfaceButtonRemainders ?? throw new ArgumentNullException(nameof(surfaceButtonRemainders));
        }

        public IGameplaySessionQuery Session { get; }

        public IGameplayStageQuery Stage { get; }

        public IGameplayPlayerHudQuery PlayerHud { get; }

        public IGameplayObjectiveQuery Objectives { get; }

        public IGameplaySurfaceButtonRemainderQuery SurfaceButtonRemainders { get; }

        public bool TryGetRevision(GameplayHudQueryKind kind, out GameplayHudQueryStamp stamp)
        {
            object query = kind switch
            {
                GameplayHudQueryKind.Stage => Stage,
                GameplayHudQueryKind.Objective => Objectives,
                GameplayHudQueryKind.PlayerHud => PlayerHud,
                GameplayHudQueryKind.SurfaceRemainder => SurfaceButtonRemainders,
                _ => null,
            };
            stamp = default;
            return query is IGameplayHudRevisionProbe probe && probe.TryGetRevision(out stamp);
        }
        public void InvalidateHudContent()
        {
            (Stage as IGameplayHudContentInvalidation)?.InvalidateHudContent();
            (SurfaceButtonRemainders as IGameplayHudContentInvalidation)?.InvalidateHudContent();
        }

        private sealed class EmptyGameplayStageQuery : IGameplayStageQuery
        {
            public static readonly EmptyGameplayStageQuery Instance = new();

            private EmptyGameplayStageQuery()
            {
            }

            public GameplayStageReadModel Read()
            {
                return default;
            }
        }

        private sealed class EmptyGameplaySurfaceButtonRemainderQuery : IGameplaySurfaceButtonRemainderQuery
        {
            public static readonly EmptyGameplaySurfaceButtonRemainderQuery Instance = new();

            private static readonly GameplaySurfaceButtonRemainderReadModel[] Empty =
            {
                new(GameplayUiFace.Floor, 0, 0),
                new(GameplayUiFace.Front, 0, 0),
                new(GameplayUiFace.Ceiling, 0, 0),
                new(GameplayUiFace.Back, 0, 0),
            };

            private EmptyGameplaySurfaceButtonRemainderQuery()
            {
            }

            public System.Collections.Generic.IReadOnlyList<GameplaySurfaceButtonRemainderReadModel> Read()
            {
                return Empty;
            }
        }
    }
}
