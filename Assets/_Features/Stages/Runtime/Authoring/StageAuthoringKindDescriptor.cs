using System;

namespace Game.Feature.Stages
{
    public readonly struct StageAuthoringKindDescriptor
    {
        public StageAuthoringKindDescriptor(
            StageAuthoringEntityKind kind,
            string marker,
            string displayName,
            StageAuthoringPresentationLane presentationLane,
            bool requiresPresentation,
            bool requiresPresentationBinding,
            bool supportsFacingAuthoring,
            bool isUnitLike,
            bool isStaticLike)
        {
            if (string.IsNullOrEmpty(marker))
            {
                throw new ArgumentException("Kind marker must be non-empty.", nameof(marker));
            }

            Kind = kind;
            Marker = marker;
            DisplayName = displayName ?? string.Empty;
            PresentationLane = presentationLane;
            RequiresPresentation = requiresPresentation;
            RequiresPresentationBinding = requiresPresentationBinding;
            SupportsFacingAuthoring = supportsFacingAuthoring;
            IsUnitLike = isUnitLike;
            IsStaticLike = isStaticLike;
        }

        public StageAuthoringEntityKind Kind { get; }

        public string Marker { get; }

        public string DisplayName { get; }

        public StageAuthoringPresentationLane PresentationLane { get; }

        public bool RequiresPresentation { get; }

        public bool RequiresPresentationBinding { get; }

        /// <summary>
        /// True when the grid/editor can display and rotate authoring Facing.
        /// This does not imply runtime consumption or semantic drift comparison.
        /// </summary>
        public bool SupportsFacingAuthoring { get; }

        public bool IsUnitLike { get; }

        public bool IsStaticLike { get; }
    }
}
