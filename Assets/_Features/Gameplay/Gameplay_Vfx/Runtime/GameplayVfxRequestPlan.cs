using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.Vfx
{
    public sealed class GameplayVfxRequestPlan
    {
        public static readonly GameplayVfxRequestPlan Empty = new(Array.Empty<GameplayVfxRequest>());

        public GameplayVfxRequestPlan(IEnumerable<GameplayVfxRequest> requests)
        {
            if (requests == null)
            {
                throw new ArgumentNullException(nameof(requests));
            }

            var sortedRequests = new List<GameplayVfxRequest>(requests);
            sortedRequests.Sort();
            Requests = sortedRequests.AsReadOnly();
        }

        public IReadOnlyList<GameplayVfxRequest> Requests { get; }
    }

    public sealed class GameplayVfxRequestPlanBuilder
    {
        private readonly List<GameplayVfxRequest> requests = new();
        private readonly HashSet<VfxPersistentKey> persistentKeys = new();

        public void Add(GameplayVfxRequest request)
        {
            if (request.IsPersistent &&
                !request.PersistentKey.IsNone &&
                !persistentKeys.Add(request.PersistentKey))
            {
                return;
            }

            requests.Add(request);
        }

        public GameplayVfxRequestPlan Build()
        {
            return requests.Count == 0 ? GameplayVfxRequestPlan.Empty : new GameplayVfxRequestPlan(requests);
        }

        public void Clear()
        {
            requests.Clear();
            persistentKeys.Clear();
        }
    }
}
