using UnityEditor;

namespace Game.Feature.Stages.Editor
{
    [InitializeOnLoad]
    internal static class StageEditorSessionStores
    {
        private const string PendingEditorStageIdSessionKey =
            "Game.Feature.Stages.PendingEditorDirectPlayStageId";
        private const string DirectPlayContextSessionKey =
            "Game.Feature.Stages.DirectPlay.Context";

        static StageEditorSessionStores()
        {
            StageCatalogValidator.ConfigureDefaultAssetMetadataProvider(EditorAssetMetadataProvider.Instance);

            StageLaunchContextStore.ConfigurePendingEditorDirectPlayStore(
                PrimePendingEditorDirectPlay,
                TryPeekPendingEditorDirectPlay,
                TryConsumePendingEditorDirectPlay,
                ClearPendingEditorDirectPlay);

            EditorDirectPlayContextStore.ConfigureEditorStore(
                () => SessionState.GetString(DirectPlayContextSessionKey, string.Empty),
                json => SessionState.SetString(DirectPlayContextSessionKey, json),
                () => SessionState.EraseString(DirectPlayContextSessionKey));
        }

        private static void PrimePendingEditorDirectPlay(StageId stageId)
        {
            SessionState.SetString(PendingEditorStageIdSessionKey, stageId.Value);
        }

        private static bool TryPeekPendingEditorDirectPlay(out StageId stageId)
        {
            var rawStageId = SessionState.GetString(PendingEditorStageIdSessionKey, string.Empty);
            if (StageId.TryCreate(rawStageId, out stageId))
            {
                return true;
            }

            stageId = StageId.None;
            return false;
        }

        private static bool TryConsumePendingEditorDirectPlay(out StageId stageId)
        {
            if (TryPeekPendingEditorDirectPlay(out stageId))
            {
                ClearPendingEditorDirectPlay();
                return true;
            }

            return false;
        }

        private static void ClearPendingEditorDirectPlay()
        {
            SessionState.EraseString(PendingEditorStageIdSessionKey);
        }

        private sealed class EditorAssetMetadataProvider : IStageValidationAssetMetadataProvider
        {
            public static readonly EditorAssetMetadataProvider Instance = new();

            public string GetAssetPath(UnityEngine.Object asset)
            {
                return asset == null ? string.Empty : AssetDatabase.GetAssetPath(asset);
            }

            public string GetAssetGuid(UnityEngine.Object asset)
            {
                var path = GetAssetPath(asset);
                return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            }
        }
    }
}
