using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Loop
{
    public static class ForwardCellProjectileDebugLog
    {
        private const string Prefix = "[FCProjectile]";
        private static readonly Dictionary<string, ShotSummary> SummariesByShot = new();

        public static bool Enabled = true;

        public static void Log(string stage, string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!Enabled)
            {
                return;
            }

            UnityEngine.Debug.Log($"{Prefix}[{stage}]{message}");
#endif
        }

        public static string BuildShotKey(
            int sourceEntityId,
            SurfaceCell targetCell,
            int impactTick,
            int impactId = 0,
            int presentationKey = 0)
        {
            var identity = impactId > 0
                ? $"#ImpactId{impactId}"
                : presentationKey > 0
                    ? $"#PresentationKey{presentationKey}"
                    : string.Empty;
            return $"Src{sourceEntityId}@Target({FormatCell(targetCell)})#ImpactTick{impactTick}{identity}";
        }

        public static string FormatCell(SurfaceCell cell)
        {
            return $"{cell.face},{cell.x},{cell.y}";
        }

        public static string FormatTopology(CubeTopologyState topology)
        {
            return topology.ToString();
        }

        public static bool IsValidArrival(PendingCellImpactResolutionKind kind)
        {
            return kind.IsValidArrival();
        }

        public static bool IsActualHit(PendingCellImpactResolutionKind kind)
        {
            return kind.IsActualHit();
        }

        public static void MarkFired(
            string shotKey,
            int sourceEntityId,
            SurfaceCell targetCell,
            int impactTick,
            int impactId)
        {
            Update(
                shotKey,
                summary =>
                {
                    summary.Fired = true;
                    summary.SourceEntityId = sourceEntityId;
                    summary.TargetCell = targetCell;
                    summary.ImpactTick = impactTick;
                    summary.ImpactId = impactId;
                });
        }

        public static void MarkDue(string shotKey)
        {
            Update(shotKey, summary => summary.DueCollected = true);
        }

        public static void MarkResolution(
            string shotKey,
            PendingCellImpactResolutionKind resolutionKind,
            bool damageGroupCreated,
            bool hpChanged,
            bool pendingRemoved)
        {
            Update(
                shotKey,
                summary =>
                {
                    summary.ResolutionKind = resolutionKind.ToString();
                    summary.IsValidArrival = IsValidArrival(resolutionKind);
                    summary.IsActualHit = IsActualHit(resolutionKind);
                    summary.DamageGroupCreated = damageGroupCreated;
                    summary.HpChanged = hpChanged;
                    summary.PendingRemoved = pendingRemoved;
                });
        }

        public static void MarkPresentation(
            string shotKey,
            int hitSignalCount,
            int arrivalSignalCount,
            bool containsShotHitSignal,
            bool containsShotArrivalSignal)
        {
            Update(
                shotKey,
                summary =>
                {
                    summary.ArrivalSignalCount = arrivalSignalCount;
                    summary.HitSignalCount = hitSignalCount;
                    summary.ContainsShotHitSignal = containsShotHitSignal;
                    summary.ContainsShotArrivalSignal = containsShotArrivalSignal;
                });
        }

        public static void MarkProductionGateForTick(
            int tickIndex,
            int arrivalSignalCount,
            int hitSignalCount,
            int releaseSignalCount,
            bool controllerWillRun,
            string reason)
        {
            foreach (var pair in SummariesByShot)
            {
                var summary = pair.Value;
                if (summary.ImpactTick != tickIndex || !summary.DueCollected)
                {
                    continue;
                }

                summary.ProductionGateSeen = true;
                summary.ControllerWillRun = controllerWillRun;
                Log(
                    "VFX_PRODUCTION_GATE",
                    $"Tick={tickIndex} Shot={pair.Key} ArrivalSignals={arrivalSignalCount} " +
                    $"HitSignals={hitSignalCount} ReleaseSignals={releaseSignalCount} " +
                    $"ControllerWillRun={controllerWillRun} Reason={reason}");
                if (!controllerWillRun)
                {
                    LogSummary(pair.Key);
                }
            }
        }

        public static void MarkController(string shotKey, int arrivalSignalCount, int hitSignalCount, int releaseSignalCount)
        {
            Update(
                shotKey,
                summary =>
                {
                    summary.ControllerCalled = true;
                    summary.ArrivalSignalCount = arrivalSignalCount;
                    summary.HitSignalCount = hitSignalCount;
                });
        }

        public static void MarkCommand(string shotKey, bool commandCreated)
        {
            Update(shotKey, summary => summary.CommandCount += commandCreated ? 1 : 0);
        }

        public static void MarkDedupe(string shotKey, bool skipped)
        {
            Update(shotKey, summary => summary.DedupeSkipped |= skipped);
        }

        public static void MarkResolve(string shotKey, bool bindingResolved, bool anchorResolved)
        {
            Update(
                shotKey,
                summary =>
                {
                    summary.BindingResolved |= bindingResolved;
                    summary.AnchorResolved |= anchorResolved;
                });
        }

        public static void MarkPool(string shotKey, bool poolPlayCalled, bool handleCreated)
        {
            Update(
                shotKey,
                summary =>
                {
                    summary.PoolPlayCalled |= poolPlayCalled;
                    summary.HandleCreated |= handleCreated;
                });
        }

        public static void MarkParticle(string shotKey)
        {
            Update(shotKey, summary => summary.ParticlePlayCalled = true);
        }

        public static void LogSummary(string shotKey)
        {
            if (string.IsNullOrEmpty(shotKey) || !SummariesByShot.TryGetValue(shotKey, out var summary))
            {
                return;
            }

            var result = ResolveResult(summary);
            if (summary.LastResult == result && summary.SummaryLogged)
            {
                return;
            }

            summary.LastResult = result;
            summary.SummaryLogged = true;
            Log(
                "SUMMARY",
                $"Shot={shotKey} Fired={summary.Fired} Due={summary.DueCollected} " +
                $"Resolution={summary.ResolutionKind} IsValidArrival={summary.IsValidArrival} " +
                $"ArrivalSignals={summary.ArrivalSignalCount} Controller={summary.ControllerCalled} " +
                $"Commands={summary.CommandCount} Dedupe={summary.DedupeSkipped} " +
                $"Binding={summary.BindingResolved} Anchor={summary.AnchorResolved} " +
                $"PoolPlay={summary.PoolPlayCalled} Handle={summary.HandleCreated} Result={result}");
        }

        private static void Update(string shotKey, Action<ShotSummary> update)
        {
            if (string.IsNullOrEmpty(shotKey) || update == null)
            {
                return;
            }

            if (!SummariesByShot.TryGetValue(shotKey, out var summary))
            {
                summary = new ShotSummary();
                SummariesByShot.Add(shotKey, summary);
            }

            update(summary);
        }

        private static string ResolveResult(ShotSummary summary)
        {
            if (summary.HandleCreated)
            {
                return "ImpactVfxCreated";
            }

            if (summary.DedupeSkipped)
            {
                return "NoVfx_Dedupe";
            }

            if (summary.CommandCount > 0 && !summary.BindingResolved)
            {
                return "NoVfx_Binding";
            }

            if (summary.CommandCount > 0 && !summary.AnchorResolved)
            {
                return "NoVfx_Anchor";
            }

            if (summary.CommandCount > 0 && !summary.PoolPlayCalled)
            {
                return "NoVfx_Pool";
            }

            if (summary.ControllerCalled && summary.CommandCount == 0)
            {
                return "NoVfx_ControllerSkipped";
            }

            if (summary.ProductionGateSeen && !summary.ControllerWillRun && summary.ContainsShotArrivalSignal)
            {
                return "NoVfx_ProductionGate";
            }

            if (summary.DueCollected && !summary.IsValidArrival)
            {
                return "NoVfx_NotValidArrival";
            }

            if (summary.DueCollected &&
                summary.IsValidArrival &&
                !summary.IsActualHit &&
                summary.ArrivalSignalCount == 0)
            {
                return "NoVfx_NoArrivalSignal";
            }

            return "Pending";
        }

        private sealed class ShotSummary
        {
            public bool Fired;
            public bool DueCollected;
            public string ResolutionKind = "None";
            public bool IsValidArrival;
            public bool IsActualHit;
            public bool DamageGroupCreated;
            public bool HpChanged;
            public bool PendingRemoved;
            public int ArrivalSignalCount;
            public int HitSignalCount;
            public bool ContainsShotHitSignal;
            public bool ContainsShotArrivalSignal;
            public bool ProductionGateSeen;
            public bool ControllerWillRun;
            public bool ControllerCalled;
            public int CommandCount;
            public bool DedupeSkipped;
            public bool BindingResolved;
            public bool AnchorResolved;
            public bool PoolPlayCalled;
            public bool HandleCreated;
            public bool ParticlePlayCalled;
            public int SourceEntityId;
            public SurfaceCell TargetCell;
            public int ImpactTick;
            public int ImpactId;
            public bool SummaryLogged;
            public string LastResult;
        }
    }
}
