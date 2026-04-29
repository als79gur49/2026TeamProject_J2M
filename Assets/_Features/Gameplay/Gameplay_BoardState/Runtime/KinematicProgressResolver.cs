using System;

namespace Game.Feature.Gameplay.BoardState
{
    public readonly struct KinematicProgressResolution
    {
        public KinematicProgressResolution(
            SurfaceCell anchorCell,
            KinematicOffset2 localOffset,
            bool isAnchorCommitTick,
            bool isSettled,
            int remainingTicks,
            int remainingDistanceUnits,
            int progressUnits)
        {
            AnchorCell = anchorCell;
            LocalOffset = localOffset;
            IsAnchorCommitTick = isAnchorCommitTick;
            IsSettled = isSettled;
            RemainingTicks = remainingTicks;
            RemainingDistanceUnits = remainingDistanceUnits;
            ProgressUnits = progressUnits;
        }

        public SurfaceCell AnchorCell { get; }

        public KinematicOffset2 LocalOffset { get; }

        public bool IsAnchorCommitTick { get; }

        public bool IsSettled { get; }

        public int RemainingTicks { get; }

        public int RemainingDistanceUnits { get; }

        public int ProgressUnits { get; }
    }

    public static class KinematicProgressResolver
    {
        public static KinematicProgressResolution ResolvePose(
            SurfaceCell currentAnchor,
            int stepDirectionX,
            int stepDirectionY,
            int elapsedTicks,
            int totalTicks,
            int unitsPerCell = KinematicFixed.UnitsPerCell)
        {
            if (unitsPerCell <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(unitsPerCell), "Units per cell must be greater than zero.");
            }

            if (totalTicks < 2 || (totalTicks % 2) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalTicks), "Kinematic total ticks must be an even value of at least two.");
            }

            if (!IsCardinalDirection(stepDirectionX, stepDirectionY))
            {
                throw new ArgumentException("Kinematic step direction must be cardinal.");
            }

            if (elapsedTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedTicks), "Elapsed ticks must be greater than zero.");
            }

            var commitTick = totalTicks / 2;
            if (elapsedTicks >= totalTicks)
            {
                return new KinematicProgressResolution(
                    currentAnchor,
                    KinematicOffset2.Zero,
                    isAnchorCommitTick: false,
                    isSettled: true,
                    remainingTicks: 0,
                    remainingDistanceUnits: 0,
                    progressUnits: unitsPerCell);
            }

            if (elapsedTicks == commitTick)
            {
                return new KinematicProgressResolution(
                    currentAnchor + new UnityEngine.Vector2Int(stepDirectionX, stepDirectionY),
                    CreateCommitOffset(stepDirectionX, stepDirectionY),
                    isAnchorCommitTick: true,
                    isSettled: false,
                    remainingTicks: totalTicks - elapsedTicks,
                    remainingDistanceUnits: unitsPerCell / 2,
                    progressUnits: unitsPerCell / 2);
            }

            var progressUnits = ResolveProgressUnits(elapsedTicks, totalTicks, unitsPerCell);
            var localUnits = elapsedTicks < commitTick
                ? progressUnits
                : progressUnits - unitsPerCell;

            return new KinematicProgressResolution(
                currentAnchor,
                CreateAxisOffset(stepDirectionX, stepDirectionY, localUnits),
                isAnchorCommitTick: false,
                isSettled: false,
                remainingTicks: totalTicks - elapsedTicks,
                remainingDistanceUnits: Math.Max(0, unitsPerCell - progressUnits),
                progressUnits: progressUnits);
        }

        public static int ResolveProgressUnits(int elapsedTicks, int totalTicks, int unitsPerCell = KinematicFixed.UnitsPerCell)
        {
            if (elapsedTicks >= totalTicks)
            {
                return unitsPerCell;
            }

            return (int)(((long)unitsPerCell * elapsedTicks + (totalTicks / 2)) / totalTicks);
        }

        private static bool IsCardinalDirection(int x, int y)
        {
            return Math.Abs(x) + Math.Abs(y) == 1;
        }

        private static KinematicOffset2 CreateAxisOffset(int stepDirectionX, int stepDirectionY, int localUnits)
        {
            return new KinematicOffset2(
                KinematicFixed.FromRaw(stepDirectionX == 0 ? 0 : localUnits * stepDirectionX),
                KinematicFixed.FromRaw(stepDirectionY == 0 ? 0 : localUnits * stepDirectionY));
        }

        private static KinematicOffset2 CreateCommitOffset(int stepDirectionX, int stepDirectionY)
        {
            return new KinematicOffset2(
                KinematicFixed.FromRaw(CreateCommitAxisOffset(stepDirectionX)),
                KinematicFixed.FromRaw(CreateCommitAxisOffset(stepDirectionY)));
        }

        private static int CreateCommitAxisOffset(int stepDirection)
        {
            if (stepDirection > 0)
            {
                return KinematicFixed.MinLocalOffset;
            }

            if (stepDirection < 0)
            {
                return KinematicFixed.MaxPositiveLocalOffset;
            }

            return 0;
        }
    }
}
