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

    public readonly struct EditorDirectPlayContext : IEquatable<EditorDirectPlayContext>
    {
        public EditorDirectPlayContext(
            EditorDirectPlayMode mode,
            StageId stageId,
            int remainingChances,
            bool suppressCampaignFlow)
        {
            Mode = mode;
            StageId = stageId;
            RemainingChances = remainingChances;
            SuppressCampaignFlow = suppressCampaignFlow;
        }

        public EditorDirectPlayMode Mode { get; }

        public StageId StageId { get; }

        public int RemainingChances { get; }

        public bool SuppressCampaignFlow { get; }

        public bool UsesTemporaryCampaignState => Mode == EditorDirectPlayMode.CampaignTempSlot;

        public bool IsCampaignMode =>
            Mode == EditorDirectPlayMode.CampaignTempSlot ||
            Mode == EditorDirectPlayMode.CampaignProductionSlot;

        public EditorDirectPlayContext ForStage(StageId stageId)
        {
            return Mode == EditorDirectPlayMode.None
                ? None
                : new EditorDirectPlayContext(
                    Mode,
                    stageId,
                    RemainingChances,
                    SuppressCampaignFlow);
        }

        public bool Equals(EditorDirectPlayContext other)
        {
            return Mode == other.Mode &&
                   StageId.Equals(other.StageId) &&
                   RemainingChances == other.RemainingChances &&
                   SuppressCampaignFlow == other.SuppressCampaignFlow;
        }

        public override bool Equals(object obj)
        {
            return obj is EditorDirectPlayContext other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Mode;
                hash = (hash * 397) ^ StageId.GetHashCode();
                hash = (hash * 397) ^ RemainingChances;
                hash = (hash * 397) ^ SuppressCampaignFlow.GetHashCode();
                return hash;
            }
        }

        public static EditorDirectPlayContext None =>
            new(EditorDirectPlayMode.None, StageId.None, 0, suppressCampaignFlow: false);

        public static EditorDirectPlayContext CreateNonCampaign(StageId stageId)
        {
            return new EditorDirectPlayContext(
                EditorDirectPlayMode.NonCampaign,
                stageId,
                0,
                suppressCampaignFlow: true);
        }

        public static EditorDirectPlayContext CreateCampaignTempSlot(StageId stageId, int remainingChances)
        {
            return new EditorDirectPlayContext(
                EditorDirectPlayMode.CampaignTempSlot,
                stageId,
                remainingChances,
                suppressCampaignFlow: false);
        }
    }

    public static class EditorDirectPlayContextStore
    {
        private const int SchemaVersion = 2;

        private static Func<string> readCurrentJson;
        private static Action<string> writeCurrentJson;
        private static Action clearCurrentJson;
        private static Action<EditorDirectPlayContext> contextUpdated;
        private static EditorDirectPlayContext fallbackContext = EditorDirectPlayContext.None;
        private static bool hasFallbackContext;
        private static long ownershipGeneration;

        public static long OwnershipGeneration => ownershipGeneration;

        public static EditorDirectPlayContext GetCurrentOrNone()
        {
            return TryGetCurrent(out var context) ? context : EditorDirectPlayContext.None;
        }

        public static bool TryGetCurrent(out EditorDirectPlayContext context)
        {
            if (readCurrentJson == null && hasFallbackContext)
            {
                context = fallbackContext;
                return context.Mode != EditorDirectPlayMode.None;
            }

            var json = readCurrentJson != null ? readCurrentJson() : string.Empty;
            if (!string.IsNullOrWhiteSpace(json))
            {
                var dto = JsonUtility.FromJson<EditorDirectPlayContextDto>(json);
                if (dto != null && dto.SchemaVersion == SchemaVersion)
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
            unchecked
            {
                ownershipGeneration++;
            }

            if (writeCurrentJson != null)
            {
                writeCurrentJson(JsonUtility.ToJson(ToDto(context)));
                contextUpdated?.Invoke(context);
                return;
            }

            fallbackContext = context;
            hasFallbackContext = context.Mode != EditorDirectPlayMode.None;
        }

        public static void Clear()
        {
            unchecked
            {
                ownershipGeneration++;
            }

            if (clearCurrentJson != null)
            {
                clearCurrentJson();
            }

            fallbackContext = EditorDirectPlayContext.None;
            hasFallbackContext = false;
        }

        public static void ClearTemporaryCampaignState()
        {
            CampaignSaveCompositionProvider.ClearTemporaryCampaignState();
        }

        private static EditorDirectPlayContextDto ToDto(EditorDirectPlayContext context)
        {
            return new EditorDirectPlayContextDto
            {
                SchemaVersion = SchemaVersion,
                Mode = (int)context.Mode,
                StageId = context.StageId.IsValid ? context.StageId.Value : string.Empty,
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
                dto.RemainingChances,
                dto.SuppressCampaignFlow);
        }

        public static void ConfigureEditorStore(
            Func<string> readJson,
            Action<string> writeJson,
            Action clearJson,
            Action<EditorDirectPlayContext> onContextUpdated = null)
        {
            readCurrentJson = readJson;
            writeCurrentJson = writeJson;
            clearCurrentJson = clearJson;
            contextUpdated = onContextUpdated;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload()
        {
            fallbackContext = EditorDirectPlayContext.None;
            hasFallbackContext = false;
        }

        [Serializable]
        private sealed class EditorDirectPlayContextDto
        {
            public int SchemaVersion;
            public int Mode;
            public string StageId;
            public int RemainingChances;
            public bool SuppressCampaignFlow;
        }
    }
}
