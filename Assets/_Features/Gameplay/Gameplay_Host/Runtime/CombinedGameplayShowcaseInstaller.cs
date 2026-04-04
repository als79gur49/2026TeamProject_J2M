using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public class CombinedGameplayShowcaseInstaller : StageBackedGameplayShowcaseInstallerBase
    {
        [Serializable]
        private struct EnemyAnimationTimingOverride
        {
            public int EntityId;
            public float AttackWindupAnimatorDurationSeconds;
            public float RecoverAnimatorDurationSeconds;
            public float StateTransitionCrossFadeDurationSeconds;

            public bool Matches(int entityId)
            {
                return EntityId > 0 &&
                       EntityId == entityId;
            }

            public void ApplyTo(GameplayEntityView view)
            {
                if (view == null)
                {
                    throw new ArgumentNullException(nameof(view));
                }

                var authoring = view.GetComponent<EnemyAnimationTimingAuthoring>() ??
                                view.gameObject.AddComponent<EnemyAnimationTimingAuthoring>();
                authoring.ApplyOverrides(
                    AttackWindupAnimatorDurationSeconds,
                    RecoverAnimatorDurationSeconds,
                    StateTransitionCrossFadeDurationSeconds);

                if (view.GetComponentInChildren<Animator>() == null)
                {
                    view.gameObject.AddComponent<Animator>();
                }
            }
        }

        private sealed class EnemyAnimationTimingOverrideViewFactory : IGameplayEntityViewFactory, IPlayerViewPrefabSource
        {
            private readonly IGameplayEntityViewFactory _innerFactory;
            private readonly EnemyAnimationTimingOverride[] _overrides;

            public EnemyAnimationTimingOverrideViewFactory(
                IGameplayEntityViewFactory innerFactory,
                EnemyAnimationTimingOverride[] overrides)
            {
                _innerFactory = innerFactory ?? throw new ArgumentNullException(nameof(innerFactory));
                _overrides = overrides == null || overrides.Length == 0
                    ? Array.Empty<EnemyAnimationTimingOverride>()
                    : (EnemyAnimationTimingOverride[])overrides.Clone();
            }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                var view = _innerFactory.CreateView(entity);

                if (entity.type != EntityType.Unit ||
                    entity.aiMode == EnemyAiMode.None)
                {
                    return view;
                }

                for (var i = 0; i < _overrides.Length; i++)
                {
                    if (!_overrides[i].Matches(entity.entityId))
                    {
                        continue;
                    }

                    _overrides[i].ApplyTo(view);
                    break;
                }

                return view;
            }

            public GameplayEntityView PlayerViewPrefab =>
                (_innerFactory as IPlayerViewPrefabSource)?.PlayerViewPrefab;
        }

        [SerializeField] private GameplayEntityView playerViewPrefab;
        [SerializeField] private EnemyAnimationTimingOverride[] enemyAnimationTimingOverrides =
            CreateDefaultEnemyAnimationTimingOverrides();

        protected override IGameplayEntityViewFactory CreateViewFactory(GameplayBoardRoot boardRoot)
        {
            if (!AutoCreateViews || boardRoot == null)
            {
                return null;
            }

            IGameplayEntityViewFactory baseFactory = playerViewPrefab != null
                ? new CombinedGameplayShowcasePlayerPrefabViewFactory(
                    boardRoot.EntityRoot,
                    PlayerEntityId,
                    playerViewPrefab,
                    CellSize)
                : new GameplayBoxCapabilityLabelViewFactory(boardRoot.EntityRoot, CellSize, PlayerEntityId);

            return enemyAnimationTimingOverrides == null || enemyAnimationTimingOverrides.Length == 0
                ? baseFactory
                : new EnemyAnimationTimingOverrideViewFactory(baseFactory, enemyAnimationTimingOverrides);
        }

        protected override GameplayEntityView ResolvePlayerViewPrefab()
        {
            return playerViewPrefab;
        }

        protected override GameplayShowcaseOverlayContent CreateShowcaseOverlayContent()
        {
            return new GameplayShowcaseOverlayContent(
                "Combined Gameplay Showcase",
                "Boxes + Enemy Variants",
                "Move: WASD   Push: E   Flip: Q",
                new[]
                {
                    "Floor charger starts in the traversal lane to demo Patrol -> Chase -> Charge immediately.",
                    "Front-face scout uses the non-attacking profile so surface-transition chase behavior stays visible.",
                    "Floor striker near spawn uses a 2-tick wind-up melee profile plus tuned wind-up/recover timing so those beats can be inspected without blocking the charger lane.",
                });
        }

        private static EnemyAnimationTimingOverride[] CreateDefaultEnemyAnimationTimingOverrides()
        {
            return new[]
            {
                new EnemyAnimationTimingOverride
                {
                    EntityId = 52,
                    AttackWindupAnimatorDurationSeconds = 0.35f,
                    RecoverAnimatorDurationSeconds = 0.5f,
                    StateTransitionCrossFadeDurationSeconds = 0.08f,
                },
            };
        }
    }
}
