using System;

namespace Game.Feature.Stages
{
    public static class StageDisplayNameKeys
    {
        public const string Table = "Stage";
        public const string Suffix = ".display_name";

        public static string ForStage(StageId stageId)
        {
            return stageId.IsValid ? ForStageIdValue(stageId.Value) : string.Empty;
        }

        public static string ForStageIdValue(string stageIdValue)
        {
            return string.IsNullOrWhiteSpace(stageIdValue)
                ? string.Empty
                : $"stage.{stageIdValue.Trim()}{Suffix}";
        }

        public static bool IsKeyForStage(StageId stageId, string key)
        {
            return string.Equals(ForStage(stageId), Normalize(key), StringComparison.Ordinal);
        }

        public static string Normalize(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
        }
    }
}
