using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Vfx;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public interface ITileFeatureVisualHandler
    {
        TileFeatureKind FeatureKind { get; }

        bool CanHandle(TileFeatureVisualCueId cueId);

        bool TryHandle(
            in TileFeatureVisualRequest request,
            ITileFeatureVisualTarget target,
            TileFeatureVisualProfile profile,
            Animator animator,
            IGameplayVfxPlaybackPort gameplayVfxPlaybackPort);
    }

    public abstract class TileFeatureVisualHandlerBase : ITileFeatureVisualHandler
    {
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorPropertyId = Shader.PropertyToID("_EmissionColor");
        private static readonly int MetallicPropertyId = Shader.PropertyToID("_Metallic");

        private MaterialPropertyBlock materialPropertyBlock;

        protected TileFeatureVisualHandlerBase(TileFeatureKind featureKind)
        {
            FeatureKind = featureKind;
        }

        public TileFeatureKind FeatureKind { get; }

        public abstract bool CanHandle(TileFeatureVisualCueId cueId);

        public virtual bool TryHandle(
            in TileFeatureVisualRequest request,
            ITileFeatureVisualTarget target,
            TileFeatureVisualProfile profile,
            Animator animator,
            IGameplayVfxPlaybackPort gameplayVfxPlaybackPort)
        {
            if (!CanHandle(request.CueId))
            {
                return false;
            }

            if (profile != null &&
                profile.TryGetCueBinding(request.CueId, out var binding))
            {
                ApplyAnimatorBinding(animator, binding.AnimatorBinding, request.Active);
                if (binding.SendGameplayVfx &&
                    gameplayVfxPlaybackPort != null)
                {
                    gameplayVfxPlaybackPort.Play(CreateGameplayVfxRequest(request, binding));
                }
            }

            return true;
        }

        protected static bool IsAny(TileFeatureVisualCueId cueId, params TileFeatureVisualCueId[] supported)
        {
            for (var i = 0; i < supported.Length; i++)
            {
                if (supported[i] == cueId)
                {
                    return true;
                }
            }

            return false;
        }

        protected void ApplyInactiveMaterialState(
            TileFeatureVisualProfile profile,
            ITileFeatureVisualTarget visualTarget,
            bool active)
        {
            if (profile == null)
            {
                UnityEngine.Debug.LogWarning($"{FeatureKind} tile visual profile is missing; inactive material state cannot be applied.");
                return;
            }

            var targets = profile.InactiveMaterialTargets;
            if (targets.Count == 0)
            {
                UnityEngine.Debug.LogWarning($"{FeatureKind} tile visual profile has no inactive material targets.");
                return;
            }

            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (!TryResolveInactiveMaterialRenderer(target, visualTarget, out var renderer))
                {
                    UnityEngine.Debug.LogWarning($"{FeatureKind} tile visual profile has a missing inactive material renderer at index {i}.");
                    continue;
                }

                var materialIndex = target.MaterialIndex;
                var sharedMaterials = renderer.sharedMaterials;
                if (materialIndex < 0 ||
                    materialIndex >= sharedMaterials.Length)
                {
                    UnityEngine.Debug.LogWarning(
                        $"{FeatureKind} tile visual profile has invalid material index {materialIndex} at inactive material target {i}.");
                    continue;
                }

                var material = sharedMaterials[materialIndex];
                if (material == null)
                {
                    UnityEngine.Debug.LogWarning(
                        $"{FeatureKind} tile visual profile has a missing material at inactive material target {i}.");
                    continue;
                }

                if (active)
                {
                    renderer.SetPropertyBlock(null, materialIndex);
                    continue;
                }

                materialPropertyBlock ??= new MaterialPropertyBlock();
                materialPropertyBlock.Clear();
                renderer.GetPropertyBlock(materialPropertyBlock, materialIndex);
                if (material.HasProperty(BaseColorPropertyId))
                {
                    materialPropertyBlock.SetColor(BaseColorPropertyId, target.InactiveColor);
                }

                if (material.HasProperty(ColorPropertyId))
                {
                    materialPropertyBlock.SetColor(ColorPropertyId, target.InactiveColor);
                }

                if (material.HasProperty(EmissionColorPropertyId))
                {
                    materialPropertyBlock.SetColor(EmissionColorPropertyId, target.InactiveColor);
                }

                if (material.HasProperty(MetallicPropertyId))
                {
                    materialPropertyBlock.SetFloat(MetallicPropertyId, target.InactiveMetallic);
                }

                renderer.SetPropertyBlock(materialPropertyBlock, materialIndex);
            }
        }

        private static bool TryResolveInactiveMaterialRenderer(
            in TileFeatureInactiveMaterialTarget target,
            ITileFeatureVisualTarget visualTarget,
            out Renderer renderer)
        {
            if (target.Renderer != null)
            {
                renderer = target.Renderer;
                return true;
            }

            if (visualTarget is TileFeatureVisualTargetView targetView &&
                targetView.TryGetRenderer(target.SlotId, out renderer))
            {
                return true;
            }

            renderer = null;
            return false;
        }

        protected static GameplayVfxRequest CreateGameplayVfxRequest(
            in TileFeatureVisualRequest request,
            in TileFeatureVisualCueBinding binding)
        {
            var cueId = GameplayVfxCueId.From(binding.GameplayVfxCue);
            var persistentKey = binding.Persistent
                ? new VfxPersistentKey(
                    cueId,
                    VfxAnchorKind.Cell,
                    tileId: request.TileId,
                    cell: request.Cell,
                    hasCell: true,
                    effectIndex: (int)request.CueId)
                : default;

            return new GameplayVfxRequest(
                tickIndex: request.TickIndex,
                sequenceId: request.SequenceId != 0 ? request.SequenceId : request.TileId,
                presentationSeed: request.SequenceId != 0 ? request.SequenceId : request.TileId,
                sourceEntityId: request.TargetEntityId,
                cueId: cueId,
                anchor: VfxAnchor.ForCell(request.Cell, default, VfxAnchorSlot.CellCenter),
                timing: VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent: binding.Persistent,
                persistentKey: persistentKey);
        }

        protected static bool ApplyAnimatorBinding(
            Animator animator,
            in TileFeatureAnimatorBinding binding,
            bool active)
        {
            if (animator == null ||
                animator.runtimeAnimatorController == null ||
                binding.Hash == 0)
            {
                return false;
            }

            switch (binding.Kind)
            {
                case TileFeatureAnimatorBindingKind.Trigger:
                    if (HasAnimatorParameter(animator, binding.Hash, AnimatorControllerParameterType.Trigger))
                    {
                        animator.SetTrigger(binding.Hash);
                        return true;
                    }

                    return false;
                case TileFeatureAnimatorBindingKind.Bool:
                    if (HasAnimatorParameter(animator, binding.Hash, AnimatorControllerParameterType.Bool))
                    {
                        animator.SetBool(binding.Hash, active);
                        return true;
                    }

                    return false;
                case TileFeatureAnimatorBindingKind.State:
                    if (animator.HasState(0, binding.Hash))
                    {
                        animator.Play(binding.Hash, 0, 1f);
                        animator.Update(0f);
                        return true;
                    }

                    return false;
            }

            return false;
        }

        private static bool HasAnimatorParameter(
            Animator animator,
            int hash,
            AnimatorControllerParameterType parameterType)
        {
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

    public sealed class ButtonTileFeatureVisualHandler : TileFeatureVisualHandlerBase
    {
        public ButtonTileFeatureVisualHandler() : base(TileFeatureKind.Button) { }

        public override bool CanHandle(TileFeatureVisualCueId cueId)
        {
            return IsAny(
                cueId,
                TileFeatureVisualCueId.ButtonActivated,
                TileFeatureVisualCueId.ButtonVisibleLoop,
                TileFeatureVisualCueId.ButtonActiveLoop);
        }
    }

    public sealed class DestroyTileFeatureVisualHandler : TileFeatureVisualHandlerBase
    {
        public DestroyTileFeatureVisualHandler() : base(TileFeatureKind.Destroy) { }

        public override bool CanHandle(TileFeatureVisualCueId cueId)
        {
            return IsAny(
                cueId,
                TileFeatureVisualCueId.DestroyTileTriggered,
                TileFeatureVisualCueId.DestroyTileActivated,
                TileFeatureVisualCueId.DestroyTileDeactivated,
                TileFeatureVisualCueId.DestroyTileActiveState,
                TileFeatureVisualCueId.DestroyTileLaserActive);
        }

        public override bool TryHandle(
            in TileFeatureVisualRequest request,
            ITileFeatureVisualTarget target,
            TileFeatureVisualProfile profile,
            Animator animator,
            IGameplayVfxPlaybackPort gameplayVfxPlaybackPort)
        {
            if (!base.TryHandle(request, target, profile, animator, gameplayVfxPlaybackPort))
            {
                return false;
            }

            if (request.CueId == TileFeatureVisualCueId.DestroyTileActivated ||
                request.CueId == TileFeatureVisualCueId.DestroyTileDeactivated ||
                request.CueId == TileFeatureVisualCueId.DestroyTileActiveState)
            {
                ApplyInactiveMaterialState(profile, target, request.Active);
            }

            return true;
        }
    }

    public sealed class SlideTileFeatureVisualHandler : TileFeatureVisualHandlerBase
    {
        public SlideTileFeatureVisualHandler() : base(TileFeatureKind.Slide) { }

        public override bool CanHandle(TileFeatureVisualCueId cueId)
        {
            return IsAny(
                cueId,
                TileFeatureVisualCueId.SlideTileRedirected,
                TileFeatureVisualCueId.SlideTileActiveState);
        }

        public override bool TryHandle(
            in TileFeatureVisualRequest request,
            ITileFeatureVisualTarget target,
            TileFeatureVisualProfile profile,
            Animator animator,
            IGameplayVfxPlaybackPort gameplayVfxPlaybackPort)
        {
            if (!base.TryHandle(request, target, profile, animator, gameplayVfxPlaybackPort))
            {
                return false;
            }

            if (request.CueId == TileFeatureVisualCueId.SlideTileActiveState)
            {
                ApplyInactiveMaterialState(profile, target, request.Active);
            }

            return true;
        }
    }

    public sealed class BarricadeTileFeatureVisualHandler : TileFeatureVisualHandlerBase
    {
        private bool hasActiveState;
        private bool lastActiveState;

        public BarricadeTileFeatureVisualHandler() : base(TileFeatureKind.Barricade) { }

        public override bool CanHandle(TileFeatureVisualCueId cueId)
        {
            return IsAny(
                cueId,
                TileFeatureVisualCueId.BarricadeBlocked,
                TileFeatureVisualCueId.BarricadeCrushed,
                TileFeatureVisualCueId.BarricadeActivated,
                TileFeatureVisualCueId.BarricadeDeactivated,
                TileFeatureVisualCueId.BarricadeActiveState,
                TileFeatureVisualCueId.BarricadeActiveLoop);
        }

        public void ResetActiveStateCache()
        {
            hasActiveState = false;
            lastActiveState = false;
        }

        public override bool TryHandle(
            in TileFeatureVisualRequest request,
            ITileFeatureVisualTarget target,
            TileFeatureVisualProfile profile,
            Animator animator,
            IGameplayVfxPlaybackPort gameplayVfxPlaybackPort)
        {
            if (!CanHandle(request.CueId))
            {
                return false;
            }

            if (request.CueId == TileFeatureVisualCueId.BarricadeActiveState)
            {
                ApplyActiveState(request, profile, animator, gameplayVfxPlaybackPort);
                return true;
            }

            if (!base.TryHandle(request, target, profile, animator, gameplayVfxPlaybackPort))
            {
                return false;
            }

            switch (request.CueId)
            {
                case TileFeatureVisualCueId.BarricadeBlocked:
                case TileFeatureVisualCueId.BarricadeActivated:
                    ApplyActiveBoolBinding(profile, animator, active: true);
                    MarkActiveState(true);
                    break;
                case TileFeatureVisualCueId.BarricadeDeactivated:
                    ApplyActiveBoolBinding(profile, animator, active: false);
                    MarkActiveState(false);
                    break;
            }

            return true;
        }

        private void ApplyActiveState(
            in TileFeatureVisualRequest request,
            TileFeatureVisualProfile profile,
            Animator animator,
            IGameplayVfxPlaybackPort gameplayVfxPlaybackPort)
        {
            if (profile != null &&
                profile.TryGetCueBinding(request.CueId, out var binding))
            {
                ApplyAnimatorBinding(animator, binding.AnimatorBinding, request.Active);
                if (binding.SendGameplayVfx &&
                    gameplayVfxPlaybackPort != null)
                {
                    gameplayVfxPlaybackPort.Play(CreateGameplayVfxRequest(request, binding));
                }

                if (hasActiveState &&
                    lastActiveState == request.Active)
                {
                    return;
                }

                MarkActiveState(request.Active);
                ApplyAnimatorBinding(
                    animator,
                    request.Active ? binding.ActiveStateAnimatorBinding : binding.InactiveStateAnimatorBinding,
                    request.Active);
                return;
            }

            MarkActiveState(request.Active);
        }

        private static void ApplyActiveBoolBinding(
            TileFeatureVisualProfile profile,
            Animator animator,
            bool active)
        {
            if (profile != null &&
                profile.TryGetCueBinding(TileFeatureVisualCueId.BarricadeActiveState, out var activeStateBinding))
            {
                ApplyAnimatorBinding(animator, activeStateBinding.AnimatorBinding, active);
            }
        }

        private void MarkActiveState(bool active)
        {
            hasActiveState = true;
            lastActiveState = active;
        }
    }

    public sealed class ExitTileFeatureVisualHandler : TileFeatureVisualHandlerBase
    {
        private bool hasOpenState;
        private bool lastOpenState;

        public ExitTileFeatureVisualHandler() : base(TileFeatureKind.Exit) { }

        public int DebugOpenStateAnimatorStatePlayCount { get; private set; }

        public int DebugLastOpenStateAnimatorStateHash { get; private set; }

        public void ResetOpenStateCache()
        {
            hasOpenState = false;
            lastOpenState = false;
            DebugOpenStateAnimatorStatePlayCount = 0;
            DebugLastOpenStateAnimatorStateHash = 0;
        }

        public override bool CanHandle(TileFeatureVisualCueId cueId)
        {
            return IsAny(
                cueId,
                TileFeatureVisualCueId.ExitOpened,
                TileFeatureVisualCueId.ExitEntered,
                TileFeatureVisualCueId.ExitOpenState,
                TileFeatureVisualCueId.ExitOpenLoop,
                TileFeatureVisualCueId.EntranceSpawn);
        }

        public override bool TryHandle(
            in TileFeatureVisualRequest request,
            ITileFeatureVisualTarget target,
            TileFeatureVisualProfile profile,
            Animator animator,
            IGameplayVfxPlaybackPort gameplayVfxPlaybackPort)
        {
            if (!CanHandle(request.CueId))
            {
                return false;
            }

            if (request.CueId == TileFeatureVisualCueId.ExitOpenState)
            {
                ApplyOpenState(request, profile, animator, gameplayVfxPlaybackPort);
                return true;
            }

            if (!base.TryHandle(request, target, profile, animator, gameplayVfxPlaybackPort))
            {
                return false;
            }

            if (request.CueId == TileFeatureVisualCueId.ExitOpened)
            {
                ApplyOpenBoolBinding(profile, animator, open: true);
                MarkOpenState(true);
            }

            return true;
        }

        private void ApplyOpenState(
            in TileFeatureVisualRequest request,
            TileFeatureVisualProfile profile,
            Animator animator,
            IGameplayVfxPlaybackPort gameplayVfxPlaybackPort)
        {
            if (profile != null &&
                profile.TryGetCueBinding(request.CueId, out var binding))
            {
                ApplyAnimatorBinding(animator, binding.AnimatorBinding, request.Active);
                if (binding.SendGameplayVfx &&
                    gameplayVfxPlaybackPort != null)
                {
                    gameplayVfxPlaybackPort.Play(CreateGameplayVfxRequest(request, binding));
                }

                if (hasOpenState &&
                    lastOpenState == request.Active)
                {
                    return;
                }

                MarkOpenState(request.Active);
                if (ApplyAnimatorBinding(
                    animator,
                    request.Active ? binding.ActiveStateAnimatorBinding : binding.InactiveStateAnimatorBinding,
                    request.Active))
                {
                    DebugOpenStateAnimatorStatePlayCount++;
                    DebugLastOpenStateAnimatorStateHash = request.Active
                        ? binding.ActiveStateAnimatorBinding.Hash
                        : binding.InactiveStateAnimatorBinding.Hash;
                }

                return;
            }

            MarkOpenState(request.Active);
        }

        private static void ApplyOpenBoolBinding(
            TileFeatureVisualProfile profile,
            Animator animator,
            bool open)
        {
            if (profile != null &&
                profile.TryGetCueBinding(TileFeatureVisualCueId.ExitOpenState, out var openStateBinding))
            {
                ApplyAnimatorBinding(animator, openStateBinding.AnimatorBinding, open);
            }
        }

        private void MarkOpenState(bool open)
        {
            hasOpenState = true;
            lastOpenState = open;
        }
    }

    public sealed class MoonBlockGeneratorTileFeatureVisualHandler : TileFeatureVisualHandlerBase
    {
        public MoonBlockGeneratorTileFeatureVisualHandler() : base(TileFeatureKind.MoonBlockGenerator) { }

        public override bool CanHandle(TileFeatureVisualCueId cueId)
        {
            return IsAny(
                cueId,
                TileFeatureVisualCueId.MoonBlockGenerated,
                TileFeatureVisualCueId.MoonBlockGeneratorBlocked);
        }
    }

    public sealed class GenericTileFeatureVfxHandler : TileFeatureVisualHandlerBase
    {
        public GenericTileFeatureVfxHandler() : base(TileFeatureKind.Unknown) { }

        public override bool CanHandle(TileFeatureVisualCueId cueId)
        {
            return cueId != TileFeatureVisualCueId.None;
        }
    }
}
