using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    internal static class StageAuthoringGridCellStyleUtility
    {
        public static readonly Color PlayerTint = new(0.30f, 0.72f, 0.95f, 1f);
        public static readonly Color EnemyTint = new(0.95f, 0.36f, 0.36f, 1f);
        public static readonly Color BoxTint = new(0.95f, 0.72f, 0.28f, 1f);
        public static readonly Color WallTint = new(0.58f, 0.58f, 0.58f, 1f);

        public static Color DimTint(Color baseTint)
        {
            return Color.Lerp(Color.white, baseTint, 0.28f);
        }

        public static bool TryGetTint(
            StagePlacedEntityAuthoring placement,
            StageAuthoringEntityKind? focusedKind,
            out Color tint)
        {
            if (!TryGetTint(placement, out tint))
            {
                return false;
            }

            if (!focusedKind.HasValue || IsFocusedKind(placement.Kind, focusedKind))
            {
                return true;
            }

            tint = DimTint(tint);
            return true;
        }

        public static bool TryGetTint(StagePlacedEntityAuthoring placement, out Color tint)
        {
            if (placement == null)
            {
                tint = Color.white;
                return false;
            }

            return TryGetTint(placement.Kind, out tint);
        }

        public static bool TryGetTint(StageAuthoringEntityKind kind, out Color tint)
        {
            switch (kind)
            {
                case StageAuthoringEntityKind.Player:
                    tint = PlayerTint;
                    return true;
                case StageAuthoringEntityKind.Enemy:
                    tint = EnemyTint;
                    return true;
                case StageAuthoringEntityKind.Box:
                    tint = BoxTint;
                    return true;
                case StageAuthoringEntityKind.Wall:
                    tint = WallTint;
                    return true;
                default:
                    tint = Color.white;
                    return false;
            }
        }

        public static bool IsFocusedKind(StageAuthoringEntityKind kind, StageAuthoringEntityKind? focusedKind)
        {
            return focusedKind.HasValue && kind == focusedKind.Value;
        }
    }
}
