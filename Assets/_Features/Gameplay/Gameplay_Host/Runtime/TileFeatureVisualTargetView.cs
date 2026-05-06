using Game.Feature.Gameplay.BoardState;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class TileFeatureVisualTargetView :
        MonoBehaviour,
        ITileFeatureVisualTarget,
        IDestroyTileVisualTarget,
        ITileFeatureVisualTargetConfigurator
    {
        [SerializeField] private int tileId;
        [SerializeField] private SurfaceCell cell;
        [SerializeField] private Animator animator;
        [SerializeField] private string buttonActivatedTriggerName = "ButtonActivated";
        [SerializeField] private string destroyTileTriggeredTriggerName = "DestroyTileTriggered";
        [SerializeField] private ParticleSystem buttonActivatedParticles;
        [SerializeField] private ParticleSystem destroyTileTriggeredParticles;
        [SerializeField] private UnityEvent buttonActivatedPlayed;
        [SerializeField] private UnityEvent destroyTileTriggeredPlayed;

        private int _debugPlayButtonActivatedCount;
        private int _debugPlayDestroyTileTriggeredCount;

        public int TileId => tileId;

        public SurfaceCell Cell => cell;

        public int DebugPlayButtonActivatedCount => _debugPlayButtonActivatedCount;

        public int DebugPlayDestroyTileTriggeredCount => _debugPlayDestroyTileTriggeredCount;

        public void Configure(int newTileId, SurfaceCell newCell)
        {
            ConfigureTileFeature(newTileId, newCell);
        }

        public void ConfigureTileFeature(int newTileId, SurfaceCell newCell)
        {
            tileId = newTileId;
            cell = newCell;
        }

        public void PlayButtonActivated()
        {
            _debugPlayButtonActivatedCount++;

            if (animator != null &&
                animator.runtimeAnimatorController != null &&
                !string.IsNullOrWhiteSpace(buttonActivatedTriggerName))
            {
                animator.SetTrigger(Animator.StringToHash(buttonActivatedTriggerName));
            }

            if (buttonActivatedParticles != null)
            {
                buttonActivatedParticles.Play(withChildren: true);
            }

            buttonActivatedPlayed?.Invoke();
        }

        public void PlayDestroyTileTriggered()
        {
            _debugPlayDestroyTileTriggeredCount++;

            if (animator != null &&
                animator.runtimeAnimatorController != null &&
                !string.IsNullOrWhiteSpace(destroyTileTriggeredTriggerName))
            {
                animator.SetTrigger(Animator.StringToHash(destroyTileTriggeredTriggerName));
            }

            if (destroyTileTriggeredParticles != null)
            {
                destroyTileTriggeredParticles.Play(withChildren: true);
            }

            destroyTileTriggeredPlayed?.Invoke();
        }
    }
}
