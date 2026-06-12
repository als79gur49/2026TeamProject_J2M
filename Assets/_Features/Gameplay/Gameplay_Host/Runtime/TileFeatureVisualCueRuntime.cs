using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Vfx;
using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    public enum TileFeatureVisualCueId
    {
        None = 0,
        ButtonActivated = 1,
        ButtonVisibleLoop = 2,
        ButtonActiveLoop = 3,
        DestroyTileTriggered = 20,
        DestroyTileActivated = 21,
        DestroyTileDeactivated = 22,
        DestroyTileActiveState = 23,
        DestroyTileLaserActive = 24,
        SlideTileRedirected = 40,
        SlideTileActiveState = 41,
        BarricadeBlocked = 60,
        BarricadeCrushed = 61,
        BarricadeActivated = 62,
        BarricadeDeactivated = 63,
        BarricadeActiveState = 64,
        BarricadeActiveLoop = 65,
        ExitOpened = 80,
        ExitEntered = 81,
        ExitOpenState = 82,
        ExitOpenLoop = 83,
        EntranceSpawn = 100,
        MoonBlockGenerated = 120,
        MoonBlockGeneratorBlocked = 121,
    }

    public enum TileFeatureAnimatorBindingKind
    {
        Trigger = 0,
        Bool = 1,
        State = 2,
    }

    [Serializable]
    public struct TileFeatureAnimatorBinding
    {
        public TileFeatureVisualCueId CueId;
        public TileFeatureAnimatorBindingKind Kind;
        public string ParameterOrStateName;

        public int Hash => string.IsNullOrWhiteSpace(ParameterOrStateName)
            ? 0
            : Animator.StringToHash(ParameterOrStateName);
    }

    [Serializable]
    public struct TileFeatureVisualCueBinding
    {
        public TileFeatureVisualCueId CueId;
        public TileFeatureVisualSlotId TargetSlot;
        public TileFeatureAnimatorBinding AnimatorBinding;
        public TileFeatureAnimatorBinding ActiveStateAnimatorBinding;
        public TileFeatureAnimatorBinding InactiveStateAnimatorBinding;
        public TileFeatureVfxCue GameplayVfxCue;
        public bool SendGameplayVfx;
        public bool Persistent;
    }

    [Serializable]
    public struct TileFeatureInactiveMaterialTarget
    {
        // Direct renderer references remain supported for legacy tests; production prefabs should prefer SlotId bindings.
        public TileFeatureVisualSlotId SlotId;
        public Renderer Renderer;
        [Min(0)] public int MaterialIndex;
        public Color InactiveColor;
        public float InactiveMetallic;
    }

    public sealed class TileFeatureVisualBindingDiagnostics
    {
        private readonly List<string> messages = new();

        public IReadOnlyList<string> Messages => messages;

        public bool IsValid => messages.Count == 0;

        public void Add(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                messages.Add(message);
            }
        }

        public static TileFeatureVisualBindingDiagnostics ForTarget(TileFeatureVisualTargetView target)
        {
            var diagnostics = new TileFeatureVisualBindingDiagnostics();
            if (target == null)
            {
                diagnostics.Add("Target view is missing.");
                return diagnostics;
            }

            if (target.TileId <= 0)
            {
                diagnostics.Add("TileId must be positive.");
            }

            if (!target.TryGetSlot(TileFeatureVisualSlotId.Root, out _))
            {
                diagnostics.Add("Root slot is missing.");
            }

            return diagnostics;
        }

        public static TileFeatureVisualBindingDiagnostics ForProfile(
            TileFeatureVisualProfile profile,
            TileFeatureVisualTargetView target = null)
        {
            var diagnostics = new TileFeatureVisualBindingDiagnostics();
            if (profile == null)
            {
                diagnostics.Add("Profile is missing.");
                return diagnostics;
            }

            var seen = new HashSet<TileFeatureVisualCueId>();
            var bindings = profile.CueBindings;
            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding.CueId == TileFeatureVisualCueId.None)
                {
                    diagnostics.Add("Cue binding cannot use None.");
                }

                if (!seen.Add(binding.CueId))
                {
                    diagnostics.Add($"Duplicate cue binding: {binding.CueId}.");
                }

                if (target != null &&
                    binding.TargetSlot != TileFeatureVisualSlotId.Root &&
                    !target.TryGetSlot(binding.TargetSlot, out _))
                {
                    diagnostics.Add($"Missing target slot: {binding.TargetSlot}.");
                }

                ValidateAnimatorBinding(diagnostics, binding.CueId, binding.AnimatorBinding, "animator binding");
                ValidateAnimatorBinding(
                    diagnostics,
                    binding.CueId,
                    binding.ActiveStateAnimatorBinding,
                    "active state animator binding");
                ValidateAnimatorBinding(
                    diagnostics,
                    binding.CueId,
                    binding.InactiveStateAnimatorBinding,
                    "inactive state animator binding");
            }

            var materialTargets = profile.InactiveMaterialTargets;
            if ((profile.FeatureKind == TileFeatureKind.Destroy ||
                 profile.FeatureKind == TileFeatureKind.Slide) &&
                materialTargets.Count == 0)
            {
                diagnostics.Add($"{profile.FeatureKind} profile must define inactive material targets.");
            }

            for (var i = 0; i < materialTargets.Count; i++)
            {
                var materialTarget = materialTargets[i];
                if (!TryResolveMaterialTargetRenderer(materialTarget, target, out var renderer))
                {
                    diagnostics.Add($"Invalid material target at index {i}: renderer is missing.");
                    continue;
                }

                var sharedMaterials = renderer.sharedMaterials;
                if (materialTarget.MaterialIndex < 0 ||
                    materialTarget.MaterialIndex >= sharedMaterials.Length)
                {
                    diagnostics.Add($"Invalid material target at index {i}: material index {materialTarget.MaterialIndex} is out of range.");
                    continue;
                }

                if (sharedMaterials[materialTarget.MaterialIndex] == null)
                {
                    diagnostics.Add($"Invalid material target at index {i}: material is missing.");
                }
            }

            return diagnostics;
        }

        private static void ValidateAnimatorBinding(
            TileFeatureVisualBindingDiagnostics diagnostics,
            TileFeatureVisualCueId cueId,
            in TileFeatureAnimatorBinding animatorBinding,
            string bindingLabel)
        {
            if (animatorBinding.CueId == TileFeatureVisualCueId.None)
            {
                return;
            }

            if (animatorBinding.CueId != cueId)
            {
                diagnostics.Add($"Invalid {bindingLabel} for cue {cueId}: cue mismatch.");
            }

            if (string.IsNullOrWhiteSpace(animatorBinding.ParameterOrStateName))
            {
                diagnostics.Add($"Invalid {bindingLabel} for cue {cueId}: parameter or state name is missing.");
                return;
            }

            if (animatorBinding.Hash == 0)
            {
                diagnostics.Add($"Invalid {bindingLabel} for cue {cueId}.");
            }
        }

        private static bool TryResolveMaterialTargetRenderer(
            TileFeatureInactiveMaterialTarget materialTarget,
            TileFeatureVisualTargetView target,
            out Renderer renderer)
        {
            if (materialTarget.Renderer != null)
            {
                renderer = materialTarget.Renderer;
                return true;
            }

            if (target != null &&
                target.TryGetRenderer(materialTarget.SlotId, out renderer))
            {
                return true;
            }

            renderer = null;
            return false;
        }
    }

    public readonly struct TileFeatureVisualRequest
    {
        public TileFeatureVisualRequest(
            TileFeatureVisualCueId cueId,
            int tileId,
            SurfaceCell cell,
            TileFeatureKind featureKind,
            Direction direction = Direction.None,
            int targetEntityId = 0,
            MoonBlockGeneratorBlockedPayload moonBlockGeneratorBlockedPayload = default,
            bool active = false,
            int tickIndex = 0,
            int sequenceId = 0)
        {
            CueId = cueId;
            TileId = tileId;
            Cell = cell;
            FeatureKind = featureKind;
            Direction = direction;
            TargetEntityId = targetEntityId;
            MoonBlockGeneratorBlockedPayload = moonBlockGeneratorBlockedPayload;
            Active = active;
            TickIndex = tickIndex;
            SequenceId = sequenceId;
        }

        public TileFeatureVisualCueId CueId { get; }
        public int TileId { get; }
        public SurfaceCell Cell { get; }
        public TileFeatureKind FeatureKind { get; }
        public Direction Direction { get; }
        public int TargetEntityId { get; }
        public MoonBlockGeneratorBlockedPayload MoonBlockGeneratorBlockedPayload { get; }
        public bool Active { get; }
        public int TickIndex { get; }
        public int SequenceId { get; }
    }

    public interface ITileFeatureVisualCueSink
    {
        bool TryHandle(in TileFeatureVisualRequest request);
    }

    public static class TileFeatureVisualRequestPlanner
    {
        public static bool TryCreate(in TilePresentationRequest request, out TileFeatureVisualRequest visualRequest)
        {
            var cueId = TileFeatureVisualCueId.None;
            var active = false;
            switch (request.RequestKind)
            {
                case TilePresentationRequestKind.ButtonActivated:
                    cueId = TileFeatureVisualCueId.ButtonActivated;
                    break;
                case TilePresentationRequestKind.DestroyTileTriggered:
                    cueId = TileFeatureVisualCueId.DestroyTileTriggered;
                    break;
                case TilePresentationRequestKind.DestroyTileActivated:
                    cueId = TileFeatureVisualCueId.DestroyTileActivated;
                    active = true;
                    break;
                case TilePresentationRequestKind.DestroyTileDeactivated:
                    cueId = TileFeatureVisualCueId.DestroyTileDeactivated;
                    break;
                case TilePresentationRequestKind.SlideTileRedirected:
                    cueId = TileFeatureVisualCueId.SlideTileRedirected;
                    break;
                case TilePresentationRequestKind.BarricadeBlocked:
                    cueId = TileFeatureVisualCueId.BarricadeBlocked;
                    active = true;
                    break;
                case TilePresentationRequestKind.BarricadeCrushed:
                    cueId = TileFeatureVisualCueId.BarricadeCrushed;
                    break;
                case TilePresentationRequestKind.BarricadeActivated:
                    cueId = TileFeatureVisualCueId.BarricadeActivated;
                    active = true;
                    break;
                case TilePresentationRequestKind.BarricadeDeactivated:
                    cueId = TileFeatureVisualCueId.BarricadeDeactivated;
                    break;
                case TilePresentationRequestKind.ExitOpened:
                    cueId = TileFeatureVisualCueId.ExitOpened;
                    active = true;
                    break;
                case TilePresentationRequestKind.ExitEntered:
                    cueId = TileFeatureVisualCueId.ExitEntered;
                    break;
                case TilePresentationRequestKind.MoonBlockGenerated:
                    cueId = TileFeatureVisualCueId.MoonBlockGenerated;
                    break;
                case TilePresentationRequestKind.MoonBlockGeneratorBlocked:
                    cueId = TileFeatureVisualCueId.MoonBlockGeneratorBlocked;
                    break;
                default:
                    visualRequest = default;
                    return false;
            }

            visualRequest = new TileFeatureVisualRequest(
                cueId,
                request.TileId,
                request.Cell,
                request.TileFeatureKind,
                request.Direction,
                request.TargetEntityId,
                request.MoonBlockGeneratorBlockedPayload,
                active);
            return true;
        }

        public static bool TryCreate(
            in TileFeatureVisualState visualState,
            out TileFeatureVisualRequest visualRequest)
        {
            var cueId = TileFeatureVisualCueId.None;
            switch (visualState.TileFeatureKind)
            {
                case TileFeatureKind.Destroy:
                    cueId = TileFeatureVisualCueId.DestroyTileActiveState;
                    break;
                case TileFeatureKind.Slide:
                    cueId = TileFeatureVisualCueId.SlideTileActiveState;
                    break;
                case TileFeatureKind.Barricade:
                    cueId = TileFeatureVisualCueId.BarricadeActiveState;
                    break;
                case TileFeatureKind.Exit:
                    cueId = TileFeatureVisualCueId.ExitOpenState;
                    break;
                default:
                    visualRequest = default;
                    return false;
            }

            visualRequest = new TileFeatureVisualRequest(
                cueId,
                visualState.TileId,
                visualState.Cell,
                visualState.TileFeatureKind,
                active: visualState.IsActive);
            return true;
        }
    }

    public interface IGameplayVfxPlaybackPort
    {
        void Play(in GameplayVfxRequest request);
    }
}
