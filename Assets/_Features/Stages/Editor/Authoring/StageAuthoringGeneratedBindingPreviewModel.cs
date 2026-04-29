using System;
using Game.Feature.Gameplay.BoardState;
using UnityEditor;

namespace Game.Feature.Stages.Editor
{
    internal sealed class StageAuthoringGeneratedBindingPreviewModel
    {
        public static readonly StageAuthoringGeneratedBindingPreviewModel Empty = new(
            requiresBinding: false,
            StageAuthoringEntityKind.Player,
            "None",
            string.Empty,
            hasMappedEntityId: false,
            isPreviewEntityId: false,
            entityId: 0,
            string.Empty,
            expectedBindingExists: false,
            actualBindingExists: false,
            isSynced: false,
            isMissing: false,
            isDrifted: false,
            wrongKind: false,
            "No presentation binding required.",
            MessageType.Info,
            Array.Empty<string>(),
            default,
            Direction.None);

        public StageAuthoringGeneratedBindingPreviewModel(
            bool requiresBinding,
            StageAuthoringEntityKind kind,
            string bindingKindLabel,
            string stableGuid,
            bool hasMappedEntityId,
            bool isPreviewEntityId,
            int entityId,
            string presentationId,
            bool expectedBindingExists,
            bool actualBindingExists,
            bool isSynced,
            bool isMissing,
            bool isDrifted,
            bool wrongKind,
            string statusLabel,
            MessageType statusMessageType,
            string[] relatedIssueMessages,
            SurfaceCell cell,
            Direction facing)
        {
            RequiresBinding = requiresBinding;
            Kind = kind;
            BindingKindLabel = bindingKindLabel ?? string.Empty;
            StableGuid = stableGuid ?? string.Empty;
            HasMappedEntityId = hasMappedEntityId;
            IsPreviewEntityId = isPreviewEntityId;
            EntityId = entityId;
            PresentationId = presentationId ?? string.Empty;
            ExpectedBindingExists = expectedBindingExists;
            ActualBindingExists = actualBindingExists;
            IsSynced = isSynced;
            IsMissing = isMissing;
            IsDrifted = isDrifted;
            WrongKind = wrongKind;
            StatusLabel = statusLabel ?? string.Empty;
            StatusMessageType = statusMessageType;
            RelatedIssueMessages = relatedIssueMessages ?? Array.Empty<string>();
            Cell = cell;
            Facing = facing;
        }

        public bool RequiresBinding { get; }

        public StageAuthoringEntityKind Kind { get; }

        public string BindingKindLabel { get; }

        public string StableGuid { get; }

        public bool HasMappedEntityId { get; }

        public bool IsPreviewEntityId { get; }

        public int EntityId { get; }

        public string PresentationId { get; }

        public bool ExpectedBindingExists { get; }

        public bool ActualBindingExists { get; }

        public bool IsSynced { get; }

        public bool IsMissing { get; }

        public bool IsDrifted { get; }

        public bool WrongKind { get; }

        public string StatusLabel { get; }

        public MessageType StatusMessageType { get; }

        public string[] RelatedIssueMessages { get; }

        public SurfaceCell Cell { get; }

        public Direction Facing { get; }
    }
}
