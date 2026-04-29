using System;
using UnityEngine;

namespace Game.Feature.Stages
{
    public enum EditorDirectPlayMode
    {
        None = 0,
        NonCampaign = 1,
        CampaignTempSlot = 2,
        CampaignProductionSlot = 3,
    }

    public readonly struct EditorDirectPlayContext
    {
        public EditorDirectPlayContext(
            EditorDirectPlayMode mode,
            StageId stageId,
            string saveSlotStoreKey,
            string activeSlotProviderKey,
            int remainingChances,
            bool suppressCampaignFlow)
        {
            Mode = mode;
            StageId = stageId;
            SaveSlotStoreKey = saveSlotStoreKey ?? string.Empty;
            ActiveSlotProviderKey = activeSlotProviderKey ?? string.Empty;
            RemainingChances = remainingChances;
            SuppressCampaignFlow = suppressCampaignFlow;
        }

        public EditorDirectPlayMode Mode { get; }

        public StageId StageId { get; }

        public string SaveSlotStoreKey { get; }

        public string ActiveSlotProviderKey { get; }

        public int RemainingChances { get; }

        public bool SuppressCampaignFlow { get; }

        public bool HasCustomSaveNamespace =>
            !string.IsNullOrWhiteSpace(SaveSlotStoreKey) &&
            !string.IsNullOrWhiteSpace(ActiveSlotProviderKey);

        public bool IsCampaignMode =>
            Mode == EditorDirectPlayMode.CampaignTempSlot ||
            Mode == EditorDirectPlayMode.CampaignProductionSlot;

        public static EditorDirectPlayContext None =>
            new(EditorDirectPlayMode.None, StageId.None, string.Empty, string.Empty, 0, suppressCampaignFlow: false);

        public static EditorDirectPlayContext CreateNonCampaign(StageId stageId)
        {
            return new EditorDirectPlayContext(
                EditorDirectPlayMode.NonCampaign,
                stageId,
                string.Empty,
                string.Empty,
                0,
                suppressCampaignFlow: true);
        }

        public static EditorDirectPlayContext CreateCampaignTempSlot(StageId stageId, int remainingChances)
        {
            return new EditorDirectPlayContext(
                EditorDirectPlayMode.CampaignTempSlot,
                stageId,
                EditorDirectPlayContextStore.TempSaveSlotStoreKey,
                EditorDirectPlayContextStore.TempActiveSlotProviderKey,
                remainingChances,
                suppressCampaignFlow: false);
        }
    }

    public static class EditorDirectPlayContextStore
    {
        public const string TempSaveSlotStoreKey = "Game.Feature.Stages.DirectPlay.TempSaveSlots";
        public const string TempActiveSlotProviderKey = "Game.Feature.Stages.DirectPlay.TempActiveSaveSlot";

        private static Func<string> readCurrentJson;
        private static Action<string> writeCurrentJson;
        private static Action clearCurrentJson;

        public static EditorDirectPlayContext GetCurrentOrNone()
        {
            return TryGetCurrent(out var context) ? context : EditorDirectPlayContext.None;
        }

        public static bool TryGetCurrent(out EditorDirectPlayContext context)
        {
            var json = readCurrentJson != null ? readCurrentJson() : string.Empty;
            if (!string.IsNullOrWhiteSpace(json))
            {
                var dto = JsonUtility.FromJson<EditorDirectPlayContextDto>(json);
                if (dto != null)
                {
                    context = FromDto(dto);
                    return context.Mode != EditorDirectPlayMode.None;
                }
            }

            context = EditorDirectPlayContext.None;
            return false;
        }

        public static void SetCurrent(EditorDirectPlayContext context)
        {
            writeCurrentJson?.Invoke(JsonUtility.ToJson(ToDto(context)));
        }

        public static void Clear()
        {
            clearCurrentJson?.Invoke();
        }

        public static void ClearTempDirectPlaySave()
        {
            PlayerPrefs.DeleteKey(TempSaveSlotStoreKey);
            PlayerPrefs.DeleteKey(TempActiveSlotProviderKey);
            PlayerPrefs.Save();
        }

        private static EditorDirectPlayContextDto ToDto(EditorDirectPlayContext context)
        {
            return new EditorDirectPlayContextDto
            {
                Mode = (int)context.Mode,
                StageId = context.StageId.IsValid ? context.StageId.Value : string.Empty,
                SaveSlotStoreKey = context.SaveSlotStoreKey,
                ActiveSlotProviderKey = context.ActiveSlotProviderKey,
                RemainingChances = context.RemainingChances,
                SuppressCampaignFlow = context.SuppressCampaignFlow,
            };
        }

        private static EditorDirectPlayContext FromDto(EditorDirectPlayContextDto dto)
        {
            var mode = Enum.IsDefined(typeof(EditorDirectPlayMode), dto.Mode)
                ? (EditorDirectPlayMode)dto.Mode
                : EditorDirectPlayMode.None;
            var stageId = StageId.TryCreate(dto.StageId, out var parsedStageId)
                ? parsedStageId
                : StageId.None;
            return new EditorDirectPlayContext(
                mode,
                stageId,
                dto.SaveSlotStoreKey,
                dto.ActiveSlotProviderKey,
                dto.RemainingChances,
                dto.SuppressCampaignFlow);
        }

        public static void ConfigureEditorStore(
            Func<string> readJson,
            Action<string> writeJson,
            Action clearJson)
        {
            readCurrentJson = readJson;
            writeCurrentJson = writeJson;
            clearCurrentJson = clearJson;
        }

        [Serializable]
        private sealed class EditorDirectPlayContextDto
        {
            public int Mode;
            public string StageId;
            public string SaveSlotStoreKey;
            public string ActiveSlotProviderKey;
            public int RemainingChances;
            public bool SuppressCampaignFlow;
        }
    }
}
