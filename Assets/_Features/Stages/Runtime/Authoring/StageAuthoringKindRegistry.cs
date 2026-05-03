using System;
using System.Collections.Generic;

namespace Game.Feature.Stages
{
    public static class StageAuthoringKindRegistry
    {
        public static readonly StageAuthoringKindDescriptor Player = new(
            StageAuthoringEntityKind.Player,
            "P",
            "Player",
            StageAuthoringPresentationLane.None,
            requiresPresentation: false,
            requiresPresentationBinding: false,
            supportsFacingAuthoring: true,
            isUnitLike: true,
            isStaticLike: false);

        public static readonly StageAuthoringKindDescriptor Enemy = new(
            StageAuthoringEntityKind.Enemy,
            "E",
            "Enemy",
            StageAuthoringPresentationLane.Enemy,
            requiresPresentation: true,
            requiresPresentationBinding: true,
            supportsFacingAuthoring: true,
            isUnitLike: true,
            isStaticLike: false);

        public static readonly StageAuthoringKindDescriptor Box = new(
            StageAuthoringEntityKind.Box,
            "B",
            "Box",
            StageAuthoringPresentationLane.Static,
            requiresPresentation: true,
            requiresPresentationBinding: true,
            supportsFacingAuthoring: true,
            isUnitLike: false,
            isStaticLike: true);

        public static readonly StageAuthoringKindDescriptor Wall = new(
            StageAuthoringEntityKind.Wall,
            "W",
            "Wall",
            StageAuthoringPresentationLane.Static,
            requiresPresentation: true,
            requiresPresentationBinding: true,
            supportsFacingAuthoring: true,
            isUnitLike: false,
            isStaticLike: true);

        private static readonly StageAuthoringKindDescriptor[] AllDescriptors =
        {
            Player,
            Enemy,
            Box,
            Wall,
        };

        private static readonly IReadOnlyList<StageAuthoringKindDescriptor> ReadOnlyDescriptors =
            Array.AsReadOnly(AllDescriptors);

        public static IReadOnlyList<StageAuthoringKindDescriptor> Descriptors => ReadOnlyDescriptors;

        public static StageAuthoringKindDescriptor Get(StageAuthoringEntityKind kind)
        {
            return kind switch
            {
                StageAuthoringEntityKind.Player => Player,
                StageAuthoringEntityKind.Enemy => Enemy,
                StageAuthoringEntityKind.Box => Box,
                StageAuthoringEntityKind.Wall => Wall,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown stage authoring entity kind."),
            };
        }

        public static bool TryGet(StageAuthoringEntityKind kind, out StageAuthoringKindDescriptor descriptor)
        {
            switch (kind)
            {
                case StageAuthoringEntityKind.Player:
                    descriptor = Player;
                    return true;
                case StageAuthoringEntityKind.Enemy:
                    descriptor = Enemy;
                    return true;
                case StageAuthoringEntityKind.Box:
                    descriptor = Box;
                    return true;
                case StageAuthoringEntityKind.Wall:
                    descriptor = Wall;
                    return true;
                default:
                    descriptor = default;
                    return false;
            }
        }

        public static bool TryGetMarker(StageAuthoringEntityKind kind, out string marker)
        {
            if (TryGet(kind, out var descriptor))
            {
                marker = descriptor.Marker;
                return true;
            }

            marker = string.Empty;
            return false;
        }

        public static StageAuthoringPresentationLane GetPresentationLane(StageAuthoringEntityKind kind)
        {
            return TryGet(kind, out var descriptor)
                ? descriptor.PresentationLane
                : StageAuthoringPresentationLane.None;
        }

        public static bool RequiresPresentation(StageAuthoringEntityKind kind)
        {
            return TryGet(kind, out var descriptor) && descriptor.RequiresPresentation;
        }

        public static bool RequiresPresentationBinding(StageAuthoringEntityKind kind)
        {
            return TryGet(kind, out var descriptor) && descriptor.RequiresPresentationBinding;
        }
    }
}
