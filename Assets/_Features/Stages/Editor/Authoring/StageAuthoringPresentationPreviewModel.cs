using System;
using UnityEditor;

namespace Game.Feature.Stages.Editor
{
    internal sealed class StageAuthoringPresentationPreviewModel
    {
        public static readonly StageAuthoringPresentationPreviewModel Empty = new(
            requiresPresentation: false,
            StageAuthoringEntityKind.Player,
            "None",
            string.Empty,
            catalogAssigned: false,
            catalogHasEntries: false,
            entryFound: false,
            catalogAsset: null,
            viewPrefab: null,
            string.Empty,
            string.Empty,
            "No presentation required.",
            MessageType.Info,
            Array.Empty<string>());

        public StageAuthoringPresentationPreviewModel(
            bool requiresPresentation,
            StageAuthoringEntityKind kind,
            string presentationKindLabel,
            string presentationId,
            bool catalogAssigned,
            bool catalogHasEntries,
            bool entryFound,
            UnityEngine.Object catalogAsset,
            UnityEngine.Object viewPrefab,
            string catalogName,
            string viewPrefabName,
            string statusLabel,
            MessageType statusMessageType,
            string[] warningMessages,
            UnityEngine.Object vfxProfileAsset = null,
            string vfxProfileStatusLabel = "",
            MessageType vfxProfileStatusMessageType = MessageType.Info)
        {
            RequiresPresentation = requiresPresentation;
            Kind = kind;
            PresentationKindLabel = presentationKindLabel ?? string.Empty;
            PresentationId = presentationId ?? string.Empty;
            CatalogAssigned = catalogAssigned;
            CatalogHasEntries = catalogHasEntries;
            EntryFound = entryFound;
            CatalogAsset = catalogAsset;
            ViewPrefab = viewPrefab;
            CatalogName = catalogName ?? string.Empty;
            ViewPrefabName = viewPrefabName ?? string.Empty;
            StatusLabel = statusLabel ?? string.Empty;
            StatusMessageType = statusMessageType;
            WarningMessages = warningMessages ?? Array.Empty<string>();
            VfxProfileAsset = vfxProfileAsset;
            VfxProfileStatusLabel = vfxProfileStatusLabel ?? string.Empty;
            VfxProfileStatusMessageType = vfxProfileStatusMessageType;
        }

        public bool RequiresPresentation { get; }

        public StageAuthoringEntityKind Kind { get; }

        public string PresentationKindLabel { get; }

        public string PresentationId { get; }

        public bool HasPresentationId => !string.IsNullOrEmpty(PresentationId);

        public bool CatalogAssigned { get; }

        public bool CatalogHasEntries { get; }

        public bool EntryFound { get; }

        public UnityEngine.Object CatalogAsset { get; }

        public UnityEngine.Object ViewPrefab { get; }

        public string CatalogName { get; }

        public string ViewPrefabName { get; }

        public string StatusLabel { get; }

        public MessageType StatusMessageType { get; }

        public string[] WarningMessages { get; }

        public UnityEngine.Object VfxProfileAsset { get; }

        public string VfxProfileStatusLabel { get; }

        public MessageType VfxProfileStatusMessageType { get; }
    }
}
