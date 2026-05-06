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
        ISlideTileVisualTarget,
        IBarricadeBlockedVisualTarget,
        IBarricadeCrushedVisualTarget,
        IExitOpenedVisualTarget,
        IExitEnteredVisualTarget,
        ITileFeatureVisualTargetConfigurator
    {
        [SerializeField] private int tileId;
        [SerializeField] private SurfaceCell cell;
        [SerializeField] private Animator animator;
        [SerializeField] private string buttonActivatedTriggerName = "ButtonActivated";
        [SerializeField] private string destroyTileTriggeredTriggerName = "DestroyTileTriggered";
        [SerializeField] private string slideTileRedirectedTriggerName = "SlideTileRedirected";
        [SerializeField] private string barricadeBlockedTriggerName = "BarricadeBlocked";
        [SerializeField] private string barricadeCrushedTriggerName = "BarricadeCrushed";
        [SerializeField] private string exitOpenedTriggerName = "ExitOpened";
        [SerializeField] private string exitEnteredTriggerName = "ExitEntered";
        [SerializeField] private ParticleSystem buttonActivatedParticles;
        [SerializeField] private ParticleSystem destroyTileTriggeredParticles;
        [SerializeField] private ParticleSystem slideTileRedirectedParticles;
        [SerializeField] private ParticleSystem barricadeBlockedParticles;
        [SerializeField] private ParticleSystem barricadeCrushedParticles;
        [SerializeField] private ParticleSystem exitOpenedParticles;
        [SerializeField] private ParticleSystem exitEnteredParticles;
        [SerializeField] private UnityEvent buttonActivatedPlayed;
        [SerializeField] private UnityEvent destroyTileTriggeredPlayed;
        [SerializeField] private UnityEvent slideTileRedirectedPlayed;
        [SerializeField] private UnityEvent barricadeBlockedPlayed;
        [SerializeField] private UnityEvent barricadeCrushedPlayed;
        [SerializeField] private UnityEvent exitOpenedPlayed;
        [SerializeField] private UnityEvent exitEnteredPlayed;

        private int _debugPlayButtonActivatedCount;
        private int _debugPlayDestroyTileTriggeredCount;
        private int _debugPlaySlideTileRedirectedCount;
        private int _debugPlayBarricadeBlockedCount;
        private int _debugPlayBarricadeCrushedCount;
        private int _debugPlayExitOpenedCount;
        private int _debugPlayExitEnteredCount;
        private Direction _debugLastSlideTileDirection = Direction.None;
        private Direction _debugLastBarricadeBlockedDirection = Direction.None;
        private int _debugLastSlideTileTargetEntityId;
        private int _debugLastBarricadeBlockedTargetEntityId;
        private int _debugLastBarricadeCrushedTargetEntityId;
        private int _debugLastExitEnteredPlayerEntityId;

        public int TileId => tileId;

        public SurfaceCell Cell => cell;

        public int DebugPlayButtonActivatedCount => _debugPlayButtonActivatedCount;

        public int DebugPlayDestroyTileTriggeredCount => _debugPlayDestroyTileTriggeredCount;

        public int DebugPlaySlideTileRedirectedCount => _debugPlaySlideTileRedirectedCount;

        public int DebugPlayBarricadeBlockedCount => _debugPlayBarricadeBlockedCount;

        public int DebugPlayBarricadeCrushedCount => _debugPlayBarricadeCrushedCount;

        public int DebugPlayExitOpenedCount => _debugPlayExitOpenedCount;

        public int DebugPlayExitEnteredCount => _debugPlayExitEnteredCount;

        public Direction DebugLastSlideTileDirection => _debugLastSlideTileDirection;

        public Direction DebugLastBarricadeBlockedDirection => _debugLastBarricadeBlockedDirection;

        public int DebugLastSlideTileTargetEntityId => _debugLastSlideTileTargetEntityId;

        public int DebugLastBarricadeBlockedTargetEntityId => _debugLastBarricadeBlockedTargetEntityId;

        public int DebugLastBarricadeCrushedTargetEntityId => _debugLastBarricadeCrushedTargetEntityId;

        public int DebugLastExitEnteredPlayerEntityId => _debugLastExitEnteredPlayerEntityId;

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

        public void PlaySlideTileRedirected(Direction direction, int targetEntityId)
        {
            _debugPlaySlideTileRedirectedCount++;
            _debugLastSlideTileDirection = direction;
            _debugLastSlideTileTargetEntityId = targetEntityId;

            if (animator != null &&
                animator.runtimeAnimatorController != null &&
                !string.IsNullOrWhiteSpace(slideTileRedirectedTriggerName))
            {
                animator.SetTrigger(Animator.StringToHash(slideTileRedirectedTriggerName));
            }

            if (slideTileRedirectedParticles != null)
            {
                slideTileRedirectedParticles.Play(withChildren: true);
            }

            slideTileRedirectedPlayed?.Invoke();
        }

        public void PlayBarricadeBlocked(Direction direction, int targetEntityId)
        {
            _debugPlayBarricadeBlockedCount++;
            _debugLastBarricadeBlockedDirection = direction;
            _debugLastBarricadeBlockedTargetEntityId = targetEntityId;

            if (animator != null &&
                animator.runtimeAnimatorController != null &&
                !string.IsNullOrWhiteSpace(barricadeBlockedTriggerName))
            {
                animator.SetTrigger(Animator.StringToHash(barricadeBlockedTriggerName));
            }

            if (barricadeBlockedParticles != null)
            {
                barricadeBlockedParticles.Play(withChildren: true);
            }

            barricadeBlockedPlayed?.Invoke();
        }

        public void PlayBarricadeCrushed(int targetEntityId)
        {
            _debugPlayBarricadeCrushedCount++;
            _debugLastBarricadeCrushedTargetEntityId = targetEntityId;

            if (animator != null &&
                animator.runtimeAnimatorController != null &&
                !string.IsNullOrWhiteSpace(barricadeCrushedTriggerName))
            {
                animator.SetTrigger(Animator.StringToHash(barricadeCrushedTriggerName));
            }

            if (barricadeCrushedParticles != null)
            {
                barricadeCrushedParticles.Play(withChildren: true);
            }

            barricadeCrushedPlayed?.Invoke();
        }

        public void PlayExitOpened()
        {
            _debugPlayExitOpenedCount++;

            if (animator != null &&
                animator.runtimeAnimatorController != null &&
                !string.IsNullOrWhiteSpace(exitOpenedTriggerName))
            {
                animator.SetTrigger(Animator.StringToHash(exitOpenedTriggerName));
            }

            if (exitOpenedParticles != null)
            {
                exitOpenedParticles.Play(withChildren: true);
            }

            exitOpenedPlayed?.Invoke();
        }

        public void PlayExitEntered(int playerEntityId)
        {
            _debugPlayExitEnteredCount++;
            _debugLastExitEnteredPlayerEntityId = playerEntityId;

            if (animator != null &&
                animator.runtimeAnimatorController != null &&
                !string.IsNullOrWhiteSpace(exitEnteredTriggerName))
            {
                animator.SetTrigger(Animator.StringToHash(exitEnteredTriggerName));
            }

            if (exitEnteredParticles != null)
            {
                exitEnteredParticles.Play(withChildren: true);
            }

            exitEnteredPlayed?.Invoke();
        }
    }
}
