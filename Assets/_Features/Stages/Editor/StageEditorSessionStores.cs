using System;
using UnityEditor;

namespace Game.Feature.Stages.Editor
{
    [InitializeOnLoad]
    internal static class StageEditorSessionStores
    {
        private const string PendingEditorStageIdSessionKey =
            "Game.Feature.Stages.PendingEditorDirectPlayStageId";
        private const string PendingEditorTokenSessionKey =
            "Game.Feature.Stages.PendingEditorDirectPlayToken";
        private const string PendingEditorSlotSessionKey =
            "Game.Feature.Stages.PendingEditorDirectPlaySlot";
        private const string PendingEditorNavigationSessionKey =
            "Game.Feature.Stages.PendingEditorDirectPlayNavigation";
        private const string PendingEditorSourceSessionKey =
            "Game.Feature.Stages.PendingEditorDirectPlaySource";
        private const string DirectPlayContextSessionKey =
            "Game.Feature.Stages.DirectPlay.Context";

        static StageEditorSessionStores()
        {
            StageCatalogValidator.ConfigureDefaultAssetMetadataProvider(EditorAssetMetadataProvider.Instance);

            StageLaunchContextStore.ConfigurePendingEditorDirectPlayStore(
                PrimePendingEditorDirectPlay,
                TryPeekPendingEditorDirectPlay,
                TryConsumePendingEditorDirectPlay,
                TryClearPendingEditorDirectPlay,
                ClearPendingEditorDirectPlay);

            EditorDirectPlayContextStore.ConfigureEditorStore(
                () => SessionState.GetString(DirectPlayContextSessionKey, string.Empty),
                json => SessionState.SetString(DirectPlayContextSessionKey, json),
                () => SessionState.EraseString(DirectPlayContextSessionKey));
        }

        private static void PrimePendingEditorDirectPlay(StageLaunchContext context)
        {
            SessionState.SetString(PendingEditorStageIdSessionKey, context.StageId.Value);
            SessionState.SetString(PendingEditorTokenSessionKey, context.Token.ToString("N"));
            SessionState.SetInt(PendingEditorSlotSessionKey, context.SlotNumber);
            SessionState.SetInt(PendingEditorNavigationSessionKey, (int)context.NavigationKind);
            SessionState.SetString(PendingEditorSourceSessionKey, context.Source);
        }

        private static bool TryPeekPendingEditorDirectPlay(out StageLaunchContext context)
        {
            var rawStageId = SessionState.GetString(PendingEditorStageIdSessionKey, string.Empty);
            var rawToken = SessionState.GetString(PendingEditorTokenSessionKey, string.Empty);
            var slotNumber = SessionState.GetInt(PendingEditorSlotSessionKey, -1);
            var navigationValue = SessionState.GetInt(PendingEditorNavigationSessionKey, 0);
            var source = SessionState.GetString(PendingEditorSourceSessionKey, string.Empty);
            if (StageId.TryCreate(rawStageId, out var stageId) &&
                Guid.TryParse(rawToken, out var token) &&
                token != Guid.Empty &&
                slotNumber == 0 &&
                navigationValue == (int)StageNavigationKind.Continue &&
                string.Equals(source, "editor-direct-play", StringComparison.Ordinal))
            {
                context = new StageLaunchContext(
                    token,
                    slotNumber,
                    stageId,
                    StageNavigationKind.Continue,
                    source);
                return true;
            }

            context = null;
            return false;
        }

        private static bool TryConsumePendingEditorDirectPlay(out StageLaunchContext context)
        {
            if (TryPeekPendingEditorDirectPlay(out context) &&
                TryClearPendingEditorDirectPlay(context))
            {
                return true;
            }

            context = null;
            return false;
        }

        private static bool TryClearPendingEditorDirectPlay(StageLaunchContext expected)
        {
            if (expected == null ||
                !TryPeekPendingEditorDirectPlay(out var current) ||
                !current.Equals(expected))
            {
                return false;
            }

            ClearPendingEditorDirectPlay();
            return true;
        }

        private static void ClearPendingEditorDirectPlay()
        {
            SessionState.EraseString(PendingEditorStageIdSessionKey);
            SessionState.EraseString(PendingEditorTokenSessionKey);
            SessionState.EraseInt(PendingEditorSlotSessionKey);
            SessionState.EraseInt(PendingEditorNavigationSessionKey);
            SessionState.EraseString(PendingEditorSourceSessionKey);
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

    internal readonly struct EditorDirectPlayLaunchOwnershipRecord : IEquatable<EditorDirectPlayLaunchOwnershipRecord>
    {
        public EditorDirectPlayLaunchOwnershipRecord(
            EditorDirectPlayMode mode,
            StageLaunchContext expectedRuntimeContext)
        {
            Mode = mode;
            ExpectedRuntimeContext = expectedRuntimeContext;
        }

        public EditorDirectPlayMode Mode { get; }

        public StageLaunchContext ExpectedRuntimeContext { get; }

        public StageId StageId => ExpectedRuntimeContext?.StageId ?? StageId.None;

        public bool IsValid =>
            Mode != EditorDirectPlayMode.None &&
            Enum.IsDefined(typeof(EditorDirectPlayMode), Mode) &&
            ExpectedRuntimeContext != null &&
            ExpectedRuntimeContext.SlotNumber == 0 &&
            ExpectedRuntimeContext.NavigationKind == StageNavigationKind.Continue &&
            string.Equals(
                ExpectedRuntimeContext.Source,
                "editor-direct-play",
                StringComparison.Ordinal);

        public bool Matches(EditorDirectPlayContext context)
        {
            return IsValid &&
                   context.Mode == Mode &&
                   context.StageId.Equals(ExpectedRuntimeContext.StageId);
        }

        public bool Equals(EditorDirectPlayLaunchOwnershipRecord other)
        {
            return Mode == other.Mode &&
                   Equals(ExpectedRuntimeContext, other.ExpectedRuntimeContext);
        }

        public override bool Equals(object obj)
        {
            return obj is EditorDirectPlayLaunchOwnershipRecord other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Mode * 397) ^ (ExpectedRuntimeContext?.GetHashCode() ?? 0);
            }
        }
    }

    internal static class EditorDirectPlayLaunchOwnershipStore
    {
        private const string ModeSessionKey =
            "Game.Feature.Stages.DirectPlay.Ownership.Mode";
        private const string StageIdSessionKey =
            "Game.Feature.Stages.DirectPlay.Ownership.StageId";
        private const string TokenSessionKey =
            "Game.Feature.Stages.DirectPlay.Ownership.Token";
        private const string SlotSessionKey =
            "Game.Feature.Stages.DirectPlay.Ownership.Slot";
        private const string NavigationSessionKey =
            "Game.Feature.Stages.DirectPlay.Ownership.Navigation";
        private const string SourceSessionKey =
            "Game.Feature.Stages.DirectPlay.Ownership.Source";

        public static bool TrySetCurrent(EditorDirectPlayLaunchOwnershipRecord ownership)
        {
            if (!ownership.IsValid)
            {
                throw new ArgumentException(
                    "Editor DirectPlay ownership requires a valid exact runtime context.",
                    nameof(ownership));
            }

            if (TryPeek(out _))
            {
                return false;
            }

            var context = ownership.ExpectedRuntimeContext;
            SessionState.SetInt(ModeSessionKey, (int)ownership.Mode);
            SessionState.SetString(StageIdSessionKey, context.StageId.Value);
            SessionState.SetString(TokenSessionKey, context.Token.ToString("N"));
            SessionState.SetInt(SlotSessionKey, context.SlotNumber);
            SessionState.SetInt(NavigationSessionKey, (int)context.NavigationKind);
            SessionState.SetString(SourceSessionKey, context.Source);
            return true;
        }

        public static bool TryPeek(out EditorDirectPlayLaunchOwnershipRecord ownership)
        {
            var modeValue = SessionState.GetInt(ModeSessionKey, 0);
            var rawStageId = SessionState.GetString(StageIdSessionKey, string.Empty);
            var rawToken = SessionState.GetString(TokenSessionKey, string.Empty);
            var slotNumber = SessionState.GetInt(SlotSessionKey, -1);
            var navigationValue = SessionState.GetInt(NavigationSessionKey, 0);
            var source = SessionState.GetString(SourceSessionKey, string.Empty);
            if (!Enum.IsDefined(typeof(EditorDirectPlayMode), modeValue) ||
                modeValue == (int)EditorDirectPlayMode.None ||
                navigationValue != (int)StageNavigationKind.Continue ||
                !StageId.TryCreate(rawStageId, out var stageId) ||
                !Guid.TryParse(rawToken, out var token) ||
                token == Guid.Empty ||
                slotNumber != 0 ||
                !string.Equals(source, "editor-direct-play", StringComparison.Ordinal))
            {
                ownership = default;
                return false;
            }

            ownership = new EditorDirectPlayLaunchOwnershipRecord(
                (EditorDirectPlayMode)modeValue,
                new StageLaunchContext(
                    token,
                    slotNumber,
                    stageId,
                    StageNavigationKind.Continue,
                    source));
            return ownership.IsValid;
        }

        public static bool TryClear(EditorDirectPlayLaunchOwnershipRecord expected)
        {
            if (!expected.IsValid ||
                !TryPeek(out var current) ||
                !current.Equals(expected))
            {
                return false;
            }

            Clear();
            return true;
        }

        internal static void ResetForTests()
        {
            Clear();
        }

        private static void Clear()
        {
            SessionState.EraseInt(ModeSessionKey);
            SessionState.EraseString(StageIdSessionKey);
            SessionState.EraseString(TokenSessionKey);
            SessionState.EraseInt(SlotSessionKey);
            SessionState.EraseInt(NavigationSessionKey);
            SessionState.EraseString(SourceSessionKey);
        }
    }
}
