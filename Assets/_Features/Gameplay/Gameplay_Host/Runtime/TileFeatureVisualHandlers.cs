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

        protected void ApplyInactiveMaterialState(TileFeatureVisualProfile profile, bool active)
        {
            if (profile == null)
            {
                return;
            }

            var targets = profile.InactiveMaterialTargets;
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var renderer = target.Renderer;
                if (renderer == null)
                {
                    continue;
                }

                var materialIndex = Mathf.Max(0, target.MaterialIndex);
                if (active)
                {
                    renderer.SetPropertyBlock(null, materialIndex);
                    continue;
                }

                materialPropertyBlock ??= new MaterialPropertyBlock();
                renderer.GetPropertyBlock(materialPropertyBlock, materialIndex);
                materialPropertyBlock.SetColor(BaseColorPropertyId, target.InactiveColor);
                materialPropertyBlock.SetColor(ColorPropertyId, target.InactiveColor);
                materialPropertyBlock.SetColor(EmissionColorPropertyId, target.InactiveColor);
                materialPropertyBlock.SetFloat(MetallicPropertyId, target.InactiveMetallic);
                renderer.SetPropertyBlock(materialPropertyBlock, materialIndex);
                materialPropertyBlock.Clear();
            }
        }

        private static GameplayVfxRequest CreateGameplayVfxRequest(
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

        private static void ApplyAnimatorBinding(
            Animator animator,
            in TileFeatureAnimatorBinding binding,
            bool active)
        {
            if (animator == null ||
                animator.runtimeAnimatorController == null ||
                binding.Hash == 0)
            {
                return;
            }

            switch (binding.Kind)
            {
                case TileFeatureAnimatorBindingKind.Trigger:
                    if (HasAnimatorParameter(animator, binding.Hash, AnimatorControllerParameterType.Trigger))
                    {
                        animator.SetTrigger(binding.Hash);
                    }

                    return;
                case TileFeatureAnimatorBindingKind.Bool:
                    if (HasAnimatorParameter(animator, binding.Hash, AnimatorControllerParameterType.Bool))
                    {
                        animator.SetBool(binding.Hash, active);
                    }

                    return;
                case TileFeatureAnimatorBindingKind.State:
                    if (animator.HasState(0, binding.Hash))
                    {
                        animator.Play(binding.Hash, 0, 1f);
                        animator.Update(0f);
                    }

                    return;
            }
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
                ApplyInactiveMaterialState(profile, request.Active);
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
                ApplyInactiveMaterialState(profile, request.Active);
            }

            return true;
        }
    }

    public sealed class BarricadeTileFeatureVisualHandler : TileFeatureVisualHandlerBase
    {
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
    }

    public sealed class ExitTileFeatureVisualHandler : TileFeatureVisualHandlerBase
    {
        public ExitTileFeatureVisualHandler() : base(TileFeatureKind.Exit) { }

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
