using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Feature.Gameplay.Host
{
    [DisallowMultipleComponent]
    public sealed class TileFeatureVisualTargetView :
        MonoBehaviour,
        ITileFeatureVisualTarget,
        IDestroyTileVisualTarget,
        IDestroyTileActivatedVisualTarget,
        IDestroyTileDeactivatedVisualTarget,
        IDestroyTileActiveStateVisualTarget,
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
        ITileFeatureVisualTargetConfigurator,
        IGameplayPresentationPausable
    {
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorPropertyId = Shader.PropertyToID("_EmissionColor");
        private static readonly int MetallicPropertyId = Shader.PropertyToID("_Metallic");

        [SerializeField] private int tileId;
        [SerializeField] private SurfaceCell cell;
        [SerializeField] private Transform presentationRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private string buttonActivatedTriggerName = "ButtonActivated";
        [SerializeField] private string destroyTileTriggeredTriggerName = "DestroyTileTriggered";
        [SerializeField] private string destroyTileActivatedTriggerName = "DestroyTileActivated";
        [SerializeField] private string destroyTileDeactivatedTriggerName = "DestroyTileDeactivated";
        [SerializeField] private string destroyTileActiveBoolName = "DestroyTileActive";
        [SerializeField] private string destroyTileActiveStateName = "DestroyTileActiveIdle";
        [SerializeField] private string destroyTileInactiveStateName = "DestroyTileInactiveIdle";
        [SerializeField] private string slideTileRedirectedTriggerName = "SlideTileRedirected";
        [SerializeField] private string barricadeBlockedTriggerName = "BarricadeBlocked";
        [SerializeField] private string barricadeCrushedTriggerName = "BarricadeCrushed";
        [SerializeField] private string barricadeActivatedTriggerName = "BarricadeActivated";
        [SerializeField] private string barricadeDeactivatedTriggerName = "BarricadeDeactivated";
        [SerializeField] private string barricadeActiveBoolName = "BarricadeActive";
        [SerializeField] private string barricadeRaisedStateName = "RaisedIdle";
        [SerializeField] private string barricadeLoweredStateName = "LoweredIdle";
        [SerializeField] private string exitOpenedTriggerName = "ExitOpened";
        [SerializeField] private string exitEnteredTriggerName = "ExitEntered";
        [SerializeField] private string exitOpenBoolName = "ExitOpen";
        [SerializeField] private string exitOpenedStateName = "ExitOpenedIdle";
        [SerializeField] private string exitClosedStateName = "ExitClosedIdle";
        [SerializeField] private string moonBlockGeneratedTriggerName = "MoonBlockGenerated";
        [SerializeField] private string moonBlockGeneratorBlockedTriggerName = "MoonBlockGeneratorBlocked";
        [SerializeField] private string moonBlockGeneratorBlockedUnitTriggerName;
        [SerializeField] private string moonBlockGeneratorBlockedWallLikeSolidTriggerName;
        [SerializeField] private string moonBlockGeneratorBlockedPlacementTriggerName;
        [SerializeField] private Renderer[] destroyTileMaterialRenderers;
        [SerializeField] private Color destroyTileInactiveColor = Color.white;
        [SerializeField] private float destroyTileInactiveMetallic = 1f;
        [SerializeField] private ParticleSystem buttonActivatedParticles;
        [SerializeField] private ParticleSystem destroyTileTriggeredParticles;
        [SerializeField] private ParticleSystem slideTileRedirectedParticles;
        [SerializeField] private ParticleSystem barricadeBlockedParticles;
        [SerializeField] private ParticleSystem barricadeCrushedParticles;
        [SerializeField] private ParticleSystem exitOpenedParticles;
        [SerializeField] private ParticleSystem exitEnteredParticles;
        [SerializeField] private ParticleSystem moonBlockGeneratedParticles;
        [SerializeField] private ParticleSystem moonBlockGeneratorBlockedParticles;
        [SerializeField] private ParticleSystem moonBlockGeneratorBlockedUnitParticles;
        [SerializeField] private ParticleSystem moonBlockGeneratorBlockedWallLikeSolidParticles;
        [SerializeField] private ParticleSystem moonBlockGeneratorBlockedPlacementParticles;
        [SerializeField] private UnityEvent buttonActivatedPlayed;
        [SerializeField] private UnityEvent destroyTileTriggeredPlayed;
        [SerializeField] private UnityEvent destroyTileActivatedPlayed;
        [SerializeField] private UnityEvent destroyTileDeactivatedPlayed;
        [SerializeField] private UnityEvent slideTileRedirectedPlayed;
        [SerializeField] private UnityEvent barricadeBlockedPlayed;
        [SerializeField] private UnityEvent barricadeCrushedPlayed;
        [SerializeField] private UnityEvent barricadeActivatedPlayed;
        [SerializeField] private UnityEvent barricadeDeactivatedPlayed;
        [SerializeField] private UnityEvent exitOpenedPlayed;
        [SerializeField] private UnityEvent exitEnteredPlayed;
        [SerializeField] private UnityEvent moonBlockGeneratedPlayed;
        [SerializeField] private UnityEvent moonBlockGeneratorBlockedPlayed;
        [SerializeField] private UnityEvent moonBlockGeneratorBlockedUnitPlayed;
        [SerializeField] private UnityEvent moonBlockGeneratorBlockedWallLikeSolidPlayed;
        [SerializeField] private UnityEvent moonBlockGeneratorBlockedPlacementPlayed;

        private int _debugPlayButtonActivatedCount;
        private int _debugPlayDestroyTileTriggeredCount;
        private int _debugPlayDestroyTileActivatedCount;
        private int _debugPlayDestroyTileDeactivatedCount;
        private bool _debugDestroyTileActive;
        private int _debugPlaySlideTileRedirectedCount;
        private int _debugPlayBarricadeBlockedCount;
        private int _debugPlayBarricadeCrushedCount;
        private int _debugPlayBarricadeActivatedCount;
        private int _debugPlayBarricadeDeactivatedCount;
        private int _debugBarricadeActiveImmediateStatePlayCount;
        private int _debugPlayExitOpenedCount;
        private int _debugPlayExitEnteredCount;
        private bool _debugExitOpen;
        private int _debugPlayMoonBlockGeneratedCount;
        private int _debugPlayMoonBlockGeneratorBlockedCount;
        private int _debugMoonBlockGeneratorBlockedUnitCount;
        private int _debugMoonBlockGeneratorBlockedWallLikeSolidCount;
        private int _debugMoonBlockGeneratorBlockedPlacementCount;
        private Direction _debugLastSlideTileDirection = Direction.None;
        private Direction _debugLastBarricadeBlockedDirection = Direction.None;
        private int _debugLastSlideTileTargetEntityId;
        private int _debugLastBarricadeBlockedTargetEntityId;
        private int _debugLastBarricadeCrushedTargetEntityId;
        private int _debugLastExitEnteredPlayerEntityId;
        private int _debugLastMoonBlockGeneratedEntityId;
        private MoonBlockGeneratorBlockedPayload _debugLastMoonBlockGeneratorBlockedPayload;
        private MaterialPropertyBlock _destroyTileMaterialPropertyBlock;
        private bool _hasBarricadeActiveImmediateState;
        private bool _isGameplayPresentationPaused;
        private bool _lastBarricadeActiveImmediateState;

        public int TileId => tileId;

        public SurfaceCell Cell => cell;

        public Transform PresentationRoot => presentationRoot != null ? presentationRoot : transform;

        public int DebugPlayButtonActivatedCount => _debugPlayButtonActivatedCount;

        public int DebugPlayDestroyTileTriggeredCount => _debugPlayDestroyTileTriggeredCount;

        public int DebugPlayDestroyTileActivatedCount => _debugPlayDestroyTileActivatedCount;

        public int DebugPlayDestroyTileDeactivatedCount => _debugPlayDestroyTileDeactivatedCount;

        public bool DebugDestroyTileActive => _debugDestroyTileActive;

        public int DebugPlaySlideTileRedirectedCount => _debugPlaySlideTileRedirectedCount;

        public int DebugPlayBarricadeBlockedCount => _debugPlayBarricadeBlockedCount;

        public int DebugPlayBarricadeCrushedCount => _debugPlayBarricadeCrushedCount;

        public int DebugPlayBarricadeActivatedCount => _debugPlayBarricadeActivatedCount;

        public int DebugPlayBarricadeDeactivatedCount => _debugPlayBarricadeDeactivatedCount;

        internal int DebugBarricadeActiveImmediateStatePlayCount => _debugBarricadeActiveImmediateStatePlayCount;

        public int DebugPlayExitOpenedCount => _debugPlayExitOpenedCount;

        public Animator DebugAnimator => animator;

        public int DebugPlayExitEnteredCount => _debugPlayExitEnteredCount;

        public bool DebugExitOpen => _debugExitOpen;

        public int DebugPlayMoonBlockGeneratedCount => _debugPlayMoonBlockGeneratedCount;

        public int DebugPlayMoonBlockGeneratorBlockedCount => _debugPlayMoonBlockGeneratorBlockedCount;

        public int DebugMoonBlockGeneratorBlockedUnitCount => _debugMoonBlockGeneratorBlockedUnitCount;

        public int DebugMoonBlockGeneratorBlockedWallLikeSolidCount => _debugMoonBlockGeneratorBlockedWallLikeSolidCount;

        public int DebugMoonBlockGeneratorBlockedPlacementCount => _debugMoonBlockGeneratorBlockedPlacementCount;

        public Direction DebugLastSlideTileDirection => _debugLastSlideTileDirection;

        public Direction DebugLastBarricadeBlockedDirection => _debugLastBarricadeBlockedDirection;

        public int DebugLastSlideTileTargetEntityId => _debugLastSlideTileTargetEntityId;

        public int DebugLastBarricadeBlockedTargetEntityId => _debugLastBarricadeBlockedTargetEntityId;

        public int DebugLastBarricadeCrushedTargetEntityId => _debugLastBarricadeCrushedTargetEntityId;

        public int DebugLastExitEnteredPlayerEntityId => _debugLastExitEnteredPlayerEntityId;

        public int DebugLastMoonBlockGeneratedEntityId => _debugLastMoonBlockGeneratedEntityId;

        public MoonBlockGeneratorBlockedPayload DebugLastMoonBlockGeneratorBlockedPayload =>
            _debugLastMoonBlockGeneratorBlockedPayload;

        public MoonBlockGeneratorBlockedReason DebugLastMoonBlockGeneratorBlockedReason =>
            _debugLastMoonBlockGeneratorBlockedPayload.Reason;

        public int DebugLastMoonBlockGeneratorBlockedEntityId =>
            _debugLastMoonBlockGeneratorBlockedPayload.BlockingEntityId;

        public void Configure(int newTileId, SurfaceCell newCell)
        {
            ConfigureTileFeature(newTileId, newCell);
        }

        public void ConfigureTileFeature(int newTileId, SurfaceCell newCell)
        {
            tileId = newTileId;
            cell = newCell;
            ResetBarricadeActiveImmediateState();
        }

        internal void ConfigurePresentationRoot(Transform newPresentationRoot)
        {
            presentationRoot = newPresentationRoot != null ? newPresentationRoot : transform;
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
                PlayParticles(buttonActivatedParticles);
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
                PlayParticles(destroyTileTriggeredParticles);
            }

            destroyTileTriggeredPlayed?.Invoke();
        }

        public void PlayDestroyTileActivated()
        {
            _debugPlayDestroyTileActivatedCount++;
            SetDestroyTileActiveImmediate(true);
            SetAnimatorTrigger(destroyTileActivatedTriggerName);
            destroyTileActivatedPlayed?.Invoke();
        }

        public void PlayDestroyTileDeactivated()
        {
            _debugPlayDestroyTileDeactivatedCount++;
            SetDestroyTileActiveImmediate(false);
            SetAnimatorTrigger(destroyTileDeactivatedTriggerName);
            destroyTileDeactivatedPlayed?.Invoke();
        }

        public void SetDestroyTileActiveImmediate(bool active)
        {
            _debugDestroyTileActive = active;
            SetAnimatorBool(destroyTileActiveBoolName, active);
            PlayAnimatorStateIfPresent(active ? destroyTileActiveStateName : destroyTileInactiveStateName);
            ApplyDestroyTileMaterialState(active);
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
                PlayParticles(slideTileRedirectedParticles);
            }

            slideTileRedirectedPlayed?.Invoke();
        }

        public void PlayBarricadeBlocked(Direction direction, int targetEntityId)
        {
            _debugPlayBarricadeBlockedCount++;
            _debugLastBarricadeBlockedDirection = direction;
            _debugLastBarricadeBlockedTargetEntityId = targetEntityId;
            MarkBarricadeActiveImmediateState(active: true);

            if (animator != null &&
                animator.runtimeAnimatorController != null &&
                !string.IsNullOrWhiteSpace(barricadeBlockedTriggerName))
            {
                animator.SetTrigger(Animator.StringToHash(barricadeBlockedTriggerName));
            }

            if (barricadeBlockedParticles != null)
            {
                PlayParticles(barricadeBlockedParticles);
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
                PlayParticles(barricadeCrushedParticles);
            }

            barricadeCrushedPlayed?.Invoke();
        }

        public void PlayBarricadeActivated()
        {
            _debugPlayBarricadeActivatedCount++;
            SetAnimatorBool(barricadeActiveBoolName, true);
            MarkBarricadeActiveImmediateState(active: true);
            SetAnimatorTrigger(barricadeActivatedTriggerName);
            barricadeActivatedPlayed?.Invoke();
        }

        public void PlayBarricadeDeactivated()
        {
            _debugPlayBarricadeDeactivatedCount++;
            SetAnimatorBool(barricadeActiveBoolName, false);
            MarkBarricadeActiveImmediateState(active: false);
            SetAnimatorTrigger(barricadeDeactivatedTriggerName);
            barricadeDeactivatedPlayed?.Invoke();
        }

        public void SetBarricadeActiveImmediate(bool active)
        {
            SetAnimatorBool(barricadeActiveBoolName, active);
            if (_hasBarricadeActiveImmediateState &&
                _lastBarricadeActiveImmediateState == active)
            {
                return;
            }

            MarkBarricadeActiveImmediateState(active);
            _debugBarricadeActiveImmediateStatePlayCount++;
            PlayAnimatorStateIfPresent(active ? barricadeRaisedStateName : barricadeLoweredStateName);
        }

        public void PlayExitOpened()
        {
            _debugPlayExitOpenedCount++;
            SetExitOpenFlag(true);
            SetAnimatorTrigger(exitOpenedTriggerName);

            if (exitOpenedParticles != null)
            {
                PlayParticles(exitOpenedParticles);
            }

            exitOpenedPlayed?.Invoke();
        }

        public void SetExitOpenImmediate(bool open)
        {
            SetExitOpenFlag(open);
            PlayAnimatorStateIfPresent(open ? exitOpenedStateName : exitClosedStateName);
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
                PlayParticles(exitEnteredParticles);
            }

            exitEnteredPlayed?.Invoke();
        }

        public void PlayMoonBlockGenerated(int moonBlockEntityId)
        {
            _debugPlayMoonBlockGeneratedCount++;
            _debugLastMoonBlockGeneratedEntityId = moonBlockEntityId;

            if (animator != null &&
                animator.runtimeAnimatorController != null &&
                !string.IsNullOrWhiteSpace(moonBlockGeneratedTriggerName))
            {
                animator.SetTrigger(Animator.StringToHash(moonBlockGeneratedTriggerName));
            }

            if (moonBlockGeneratedParticles != null)
            {
                PlayParticles(moonBlockGeneratedParticles);
            }

            moonBlockGeneratedPlayed?.Invoke();
        }

        public void PlayMoonBlockGeneratorBlocked(MoonBlockGeneratorBlockedPayload payload)
        {
            _debugPlayMoonBlockGeneratorBlockedCount++;
            _debugLastMoonBlockGeneratorBlockedPayload = payload;

            if (animator != null &&
                animator.runtimeAnimatorController != null &&
                !string.IsNullOrWhiteSpace(moonBlockGeneratorBlockedTriggerName))
            {
                animator.SetTrigger(Animator.StringToHash(moonBlockGeneratorBlockedTriggerName));
            }

            if (moonBlockGeneratorBlockedParticles != null)
            {
                PlayParticles(moonBlockGeneratorBlockedParticles);
            }

            moonBlockGeneratorBlockedPlayed?.Invoke();
            PlayReasonSpecificMoonBlockGeneratorBlocked(payload.Reason);
        }

        private void PlayReasonSpecificMoonBlockGeneratorBlocked(MoonBlockGeneratorBlockedReason reason)
        {
            switch (reason)
            {
                case MoonBlockGeneratorBlockedReason.UnitOccupant:
                    _debugMoonBlockGeneratorBlockedUnitCount++;
                    PlayOptionalFeedback(
                        moonBlockGeneratorBlockedUnitTriggerName,
                        moonBlockGeneratorBlockedUnitParticles,
                        moonBlockGeneratorBlockedUnitPlayed);
                    return;
                case MoonBlockGeneratorBlockedReason.WallLikeSolid:
                    _debugMoonBlockGeneratorBlockedWallLikeSolidCount++;
                    PlayOptionalFeedback(
                        moonBlockGeneratorBlockedWallLikeSolidTriggerName,
                        moonBlockGeneratorBlockedWallLikeSolidParticles,
                        moonBlockGeneratorBlockedWallLikeSolidPlayed);
                    return;
                case MoonBlockGeneratorBlockedReason.PlacementBlocked:
                    _debugMoonBlockGeneratorBlockedPlacementCount++;
                    PlayOptionalFeedback(
                        moonBlockGeneratorBlockedPlacementTriggerName,
                        moonBlockGeneratorBlockedPlacementParticles,
                        moonBlockGeneratorBlockedPlacementPlayed);
                    return;
            }
        }

        private void PlayOptionalFeedback(
            string triggerName,
            ParticleSystem particles,
            UnityEvent played)
        {
            if (animator != null &&
                animator.runtimeAnimatorController != null &&
                !string.IsNullOrWhiteSpace(triggerName))
            {
                animator.SetTrigger(Animator.StringToHash(triggerName));
            }

            if (particles != null)
            {
                PlayParticles(particles);
            }

            played?.Invoke();
        }

        public void SetPresentationPaused(bool paused)
        {
            _isGameplayPresentationPaused = paused;
        }

        private void ApplyDestroyTileMaterialState(bool active)
        {
            var renderers = ResolveDestroyTileMaterialRenderers();
            if (renderers == null ||
                renderers.Length == 0)
            {
                return;
            }

            for (var i = 0; i < renderers.Length; i++)
            {
                var targetRenderer = renderers[i];
                if (targetRenderer == null)
                {
                    continue;
                }

                if (active)
                {
                    targetRenderer.SetPropertyBlock(null);
                    continue;
                }

                _destroyTileMaterialPropertyBlock ??= new MaterialPropertyBlock();
                targetRenderer.GetPropertyBlock(_destroyTileMaterialPropertyBlock);
                _destroyTileMaterialPropertyBlock.SetColor(BaseColorPropertyId, destroyTileInactiveColor);
                _destroyTileMaterialPropertyBlock.SetColor(ColorPropertyId, destroyTileInactiveColor);
                _destroyTileMaterialPropertyBlock.SetColor(EmissionColorPropertyId, destroyTileInactiveColor);
                _destroyTileMaterialPropertyBlock.SetFloat(MetallicPropertyId, destroyTileInactiveMetallic);
                targetRenderer.SetPropertyBlock(_destroyTileMaterialPropertyBlock);
                _destroyTileMaterialPropertyBlock.Clear();
            }
        }

        private Renderer[] ResolveDestroyTileMaterialRenderers()
        {
            if (destroyTileMaterialRenderers != null &&
                destroyTileMaterialRenderers.Length > 0)
            {
                return destroyTileMaterialRenderers;
            }

            var root = PresentationRoot;
            if (root == null)
            {
                return destroyTileMaterialRenderers;
            }

            var childRenderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (childRenderers == null ||
                childRenderers.Length == 0)
            {
                destroyTileMaterialRenderers = childRenderers;
                return destroyTileMaterialRenderers;
            }

            var count = 0;
            for (var i = 0; i < childRenderers.Length; i++)
            {
                if (IsDestroyTileMaterialRenderer(childRenderers[i]))
                {
                    count++;
                }
            }

            if (count == childRenderers.Length)
            {
                destroyTileMaterialRenderers = childRenderers;
                return destroyTileMaterialRenderers;
            }

            destroyTileMaterialRenderers = new Renderer[count];
            var index = 0;
            for (var i = 0; i < childRenderers.Length; i++)
            {
                var childRenderer = childRenderers[i];
                if (IsDestroyTileMaterialRenderer(childRenderer))
                {
                    destroyTileMaterialRenderers[index++] = childRenderer;
                }
            }

            return destroyTileMaterialRenderers;
        }

        private static bool IsDestroyTileMaterialRenderer(Renderer targetRenderer)
        {
            return targetRenderer is MeshRenderer ||
                   targetRenderer is SkinnedMeshRenderer;
        }

        private void SetAnimatorTrigger(string triggerName)
        {
            if (HasAnimatorParameter(triggerName, AnimatorControllerParameterType.Trigger, out var hash))
            {
                animator.SetTrigger(hash);
            }
        }

        private void PlayParticles(ParticleSystem particles)
        {
            if (particles == null)
            {
                return;
            }

            particles.Play(withChildren: true);
            if (_isGameplayPresentationPaused)
            {
                particles.Pause(withChildren: true);
            }
        }

        private void SetAnimatorBool(string boolName, bool value)
        {
            if (HasAnimatorParameter(boolName, AnimatorControllerParameterType.Bool, out var hash))
            {
                animator.SetBool(hash, value);
            }
        }

        private void SetExitOpenFlag(bool open)
        {
            _debugExitOpen = open;
            SetAnimatorBool(exitOpenBoolName, open);
        }

        private void ResetBarricadeActiveImmediateState()
        {
            _hasBarricadeActiveImmediateState = false;
            _lastBarricadeActiveImmediateState = false;
            _debugBarricadeActiveImmediateStatePlayCount = 0;
        }

        private void MarkBarricadeActiveImmediateState(bool active)
        {
            _hasBarricadeActiveImmediateState = true;
            _lastBarricadeActiveImmediateState = active;
        }

        private void PlayAnimatorStateIfPresent(string stateName)
        {
            if (animator == null ||
                animator.runtimeAnimatorController == null ||
                string.IsNullOrWhiteSpace(stateName))
            {
                return;
            }

            var hash = Animator.StringToHash(stateName);
            if (animator.HasState(0, hash))
            {
                animator.Play(hash, 0, 1f);
                animator.Update(0f);
            }
        }

        private bool HasAnimatorParameter(
            string parameterName,
            AnimatorControllerParameterType parameterType,
            out int hash)
        {
            hash = 0;
            if (animator == null ||
                animator.runtimeAnimatorController == null ||
                string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            hash = Animator.StringToHash(parameterName);
            var parameters = animator.parameters;
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
