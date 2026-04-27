using System;

namespace Game.Feature.Stages
{
    public readonly struct CampaignRuntimeActivation
    {
        public CampaignRuntimeActivation(
            bool isActive,
            bool enableCampaignFlow,
            bool isSuppressedByEditorDirectPlay,
            bool hasActiveSlot)
        {
            IsActive = isActive;
            EnableCampaignFlow = enableCampaignFlow;
            IsSuppressedByEditorDirectPlay = isSuppressedByEditorDirectPlay;
            HasActiveSlot = hasActiveSlot;
        }

        public bool IsActive { get; }

        public bool EnableCampaignFlow { get; }

        public bool IsSuppressedByEditorDirectPlay { get; }

        public bool HasActiveSlot { get; }
    }

    public static class CampaignRuntimeActivationPolicy
    {
        public static CampaignRuntimeActivation Evaluate(
            bool enableCampaignFlow,
            ActiveSlotProvider activeSlotProvider)
        {
            return Evaluate(enableCampaignFlow, activeSlotProvider, EditorDirectPlayContextStore.GetCurrentOrNone());
        }

        public static CampaignRuntimeActivation Evaluate(
            bool enableCampaignFlow,
            ActiveSlotProvider activeSlotProvider,
            EditorDirectPlayContext editorDirectPlayContext)
        {
            var isSuppressed = editorDirectPlayContext.SuppressCampaignFlow;
            var hasActiveSlot = activeSlotProvider != null && activeSlotProvider.HasActiveSlot;
            return new CampaignRuntimeActivation(
                enableCampaignFlow && !isSuppressed && hasActiveSlot,
                enableCampaignFlow,
                isSuppressed,
                hasActiveSlot);
        }
    }
}
