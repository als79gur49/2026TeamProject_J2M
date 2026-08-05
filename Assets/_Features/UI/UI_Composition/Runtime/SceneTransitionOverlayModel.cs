using Game.Feature.Stages;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    internal readonly struct SceneTransitionOverlayModel
    {
        public readonly StageTransitionKind TransitionKind;
        public readonly TransitionOverlayKind OverlayKind;
        public readonly bool BlockInput;
        public readonly bool ShowProgress;
        public readonly float Progress01;
        public readonly bool HasChanceLost;
        public readonly int PreviousRemainingChances;
        public readonly int CurrentRemainingChances;
        public readonly int TotalChances;
        public readonly int DeathCount;
        public readonly long TerminalClaimId;

        public SceneTransitionOverlayModel(
            TransitionOverlayKind overlayKind,
            bool blockInput,
            bool showProgress,
            float progress01,
            bool hasChanceLost,
            int previousRemainingChances,
            int currentRemainingChances,
            int totalChances,
            int deathCount,
            long terminalClaimId = 0)
            : this(
                StageTransitionKind.Unknown,
                overlayKind,
                blockInput,
                showProgress,
                progress01,
                hasChanceLost,
                previousRemainingChances,
                currentRemainingChances,
                totalChances,
                deathCount,
                terminalClaimId)
        {
        }

        public SceneTransitionOverlayModel(
            StageTransitionKind transitionKind,
            TransitionOverlayKind overlayKind,
            bool blockInput,
            bool showProgress,
            float progress01,
            bool hasChanceLost,
            int previousRemainingChances,
            int currentRemainingChances,
            int totalChances,
            int deathCount,
            long terminalClaimId = 0)
        {
            TransitionKind = transitionKind;
            OverlayKind = overlayKind;
            BlockInput = blockInput;
            ShowProgress = showProgress;
            Progress01 = Mathf.Clamp01(progress01);
            HasChanceLost = hasChanceLost;
            PreviousRemainingChances = Mathf.Max(0, previousRemainingChances);
            CurrentRemainingChances = Mathf.Max(0, currentRemainingChances);
            TotalChances = Mathf.Max(0, totalChances);
            DeathCount = Mathf.Max(0, deathCount);
            TerminalClaimId = terminalClaimId > 0 ? terminalClaimId : 0;
        }
    }
}
