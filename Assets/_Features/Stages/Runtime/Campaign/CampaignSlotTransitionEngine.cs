using System;
using System.Collections.Generic;

namespace Game.Feature.Stages
{
    internal enum CampaignSlotTransitionFailureKind
    {
        None = 0,
        InvalidCurrentState = 1,
        InvalidPlan = 2,
        StalePrecondition = 3,
    }

    internal enum CampaignSlotTransitionReasonCode
    {
        None = 0,
        CurrentStateInvalid = 1,
        DeathPlanInvalid = 2,
        DeathPreconditionChanged = 3,
        StageClearRequestInvalid = 4,
        StageClearPreconditionChanged = 5,
        DeathCounterOverflow = 6,
        StageClearRequestNull = 7,
        ComicCommandInvalid = 8,
    }

    internal enum CampaignComicCompletionKind
    {
        Intro = 1,
        Outro = 2,
    }

    internal readonly struct CampaignComicCompletionCommand
    {
        internal CampaignComicCompletionCommand(CampaignComicCompletionKind completionKind)
        {
            CompletionKind = completionKind;
        }

        internal CampaignComicCompletionKind CompletionKind { get; }
    }

    internal readonly struct CampaignSlotTransitionResult
    {
        private CampaignSlotTransitionResult(
            CampaignSlotState slot,
            CampaignSlotTransitionFailureKind failureKind,
            CampaignSlotTransitionReasonCode reasonCode,
            int? previousRemainingChances)
        {
            Slot = slot;
            FailureKind = failureKind;
            ReasonCode = reasonCode;
            PreviousRemainingChances = previousRemainingChances;
        }

        public bool Succeeded =>
            FailureKind == CampaignSlotTransitionFailureKind.None &&
            Slot != null;

        public CampaignSlotTransitionFailureKind FailureKind { get; }

        public CampaignSlotTransitionReasonCode ReasonCode { get; }

        public CampaignSlotState Slot { get; }

        public int? PreviousRemainingChances { get; }

        public static CampaignSlotTransitionResult Success(
            CampaignSlotState slot,
            int? previousRemainingChances = null)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            return new CampaignSlotTransitionResult(
                slot,
                CampaignSlotTransitionFailureKind.None,
                CampaignSlotTransitionReasonCode.None,
                previousRemainingChances);
        }

        public static CampaignSlotTransitionResult Failure(
            CampaignSlotTransitionFailureKind failureKind,
            CampaignSlotTransitionReasonCode reasonCode)
        {
            if (failureKind == CampaignSlotTransitionFailureKind.None)
            {
                throw new ArgumentOutOfRangeException(nameof(failureKind));
            }

            if (reasonCode == CampaignSlotTransitionReasonCode.None)
            {
                throw new ArgumentOutOfRangeException(nameof(reasonCode));
            }

            return new CampaignSlotTransitionResult(
                slot: null,
                failureKind,
                reasonCode,
                previousRemainingChances: null);
        }
    }

    /// <summary>
    /// Applies campaign slot gameplay transitions without observing clocks, repositories,
    /// Unity runtime state, or composition-specific storage.
    /// </summary>
    internal static class CampaignSlotTransitionEngine
    {
        public static CampaignSlotTransitionResult ApplyDeath(
            CampaignSlotState current,
            CampaignDeathTransitionPlan plan,
            string committedAtUtc)
        {
            if (!IsValid(plan))
            {
                return CampaignSlotTransitionResult.Failure(
                    CampaignSlotTransitionFailureKind.InvalidPlan,
                    CampaignSlotTransitionReasonCode.DeathPlanInvalid);
            }

            if (current == null)
            {
                return InvalidCurrentState();
            }

            if (!current.CurrentStageId.Equals(plan.ExpectedCurrentStageId) ||
                current.RemainingChances != plan.ExpectedRemainingChances)
            {
                return CampaignSlotTransitionResult.Failure(
                    CampaignSlotTransitionFailureKind.StalePrecondition,
                    CampaignSlotTransitionReasonCode.DeathPreconditionChanged);
            }

            if (current.TotalDeaths == int.MaxValue)
            {
                return CampaignSlotTransitionResult.Failure(
                    CampaignSlotTransitionFailureKind.InvalidCurrentState,
                    CampaignSlotTransitionReasonCode.DeathCounterOverflow);
            }

            var next = new CampaignSlotState(
                current.SlotNumber,
                plan.Route.NextStageId,
                plan.PersistedLevelGroupId,
                plan.Route.RemainingChances,
                current.CampaignCompleted,
                current.Receipt,
                current.IntroComicCompleted,
                current.OutroComicCompleted,
                current.NormalStagePerformanceRecords,
                current.TotalDeaths + 1,
                committedAtUtc,
                current.StageClearProfile);
            return CampaignSlotTransitionResult.Success(next);
        }

        public static CampaignSlotTransitionResult ApplyStageClear(
            CampaignSlotState current,
            CampaignStageClearCommitRequest request,
            string committedAtUtc)
        {
            if (request == null)
            {
                return CampaignSlotTransitionResult.Failure(
                    CampaignSlotTransitionFailureKind.InvalidPlan,
                    CampaignSlotTransitionReasonCode.StageClearRequestNull);
            }

            if (!IsValid(request))
            {
                return CampaignSlotTransitionResult.Failure(
                    CampaignSlotTransitionFailureKind.InvalidPlan,
                    CampaignSlotTransitionReasonCode.StageClearRequestInvalid);
            }

            if (current == null)
            {
                return InvalidCurrentState();
            }

            if (!current.CurrentStageId.Equals(request.Plan.CompletedStageId))
            {
                return CampaignSlotTransitionResult.Failure(
                    CampaignSlotTransitionFailureKind.StalePrecondition,
                    CampaignSlotTransitionReasonCode.StageClearPreconditionChanged);
            }

            var previousRemainingChances = current.RemainingChances;
            var remainingChances = request.Plan.RestoresChances
                ? CampaignSaveSlotPolicy.DefaultRemainingChances
                : current.RemainingChances;
            var next = new CampaignSlotState(
                current.SlotNumber,
                request.Plan.PersistedStageId,
                request.Plan.PersistedLevelGroupId,
                remainingChances,
                request.Plan.IsCampaignCompleted,
                ApplyReceipt(current.Receipt, request.CompletionReceipt),
                current.IntroComicCompleted,
                current.OutroComicCompleted,
                UpsertPerformance(
                    current.NormalStagePerformanceRecords,
                    request.PerformanceRecord),
                current.TotalDeaths,
                committedAtUtc,
                current.StageClearProfile);
            return CampaignSlotTransitionResult.Success(
                next,
                previousRemainingChances);
        }

        public static CampaignSlotTransitionResult ApplyComicCompletion(
            CampaignSlotState current,
            CampaignComicCompletionCommand command,
            string committedAtUtc)
        {
            if (command.CompletionKind != CampaignComicCompletionKind.Intro &&
                command.CompletionKind != CampaignComicCompletionKind.Outro)
            {
                return CampaignSlotTransitionResult.Failure(
                    CampaignSlotTransitionFailureKind.InvalidPlan,
                    CampaignSlotTransitionReasonCode.ComicCommandInvalid);
            }

            if (current == null)
            {
                return InvalidCurrentState();
            }

            var next = new CampaignSlotState(
                current.SlotNumber,
                current.CurrentStageId,
                current.CurrentLevelGroupId,
                current.RemainingChances,
                current.CampaignCompleted,
                current.Receipt,
                current.IntroComicCompleted ||
                command.CompletionKind == CampaignComicCompletionKind.Intro,
                current.OutroComicCompleted ||
                command.CompletionKind == CampaignComicCompletionKind.Outro,
                current.NormalStagePerformanceRecords,
                current.TotalDeaths,
                committedAtUtc,
                current.StageClearProfile);
            return CampaignSlotTransitionResult.Success(next);
        }

        private static CampaignSlotTransitionResult InvalidCurrentState()
        {
            return CampaignSlotTransitionResult.Failure(
                CampaignSlotTransitionFailureKind.InvalidCurrentState,
                CampaignSlotTransitionReasonCode.CurrentStateInvalid);
        }

        private static CampaignReceiptState ApplyReceipt(
            CampaignReceiptState current,
            NormalCampaignCompletionReceipt offered)
        {
            if (offered == null ||
                current.Presence != CampaignReceiptPresence.Absent)
            {
                return current;
            }

            return CampaignReceiptState.PresentWithPayload(
                new CampaignCompletionReceiptState(
                    offered.Version,
                    StageId.CreateOrThrow(offered.CompletedStageId),
                    offered.StageRunId,
                    offered.ClearSource));
        }

        private static IReadOnlyList<CampaignStagePerformanceState> UpsertPerformance(
            IReadOnlyList<CampaignStagePerformanceState> current,
            NormalStagePerformanceRecord offered)
        {
            if (offered == null)
            {
                return current;
            }

            var records = new List<CampaignStagePerformanceState>(current.Count + 1);
            var found = false;
            for (var index = 0; index < current.Count; index++)
            {
                var record = current[index];
                if (!record.StageId.Equals(offered.StageId))
                {
                    records.Add(record);
                    continue;
                }

                found = true;
                records.Add(offered.BestCombinedPushFlipUses <
                            record.BestCombinedPushFlipUses
                    ? new CampaignStagePerformanceState(
                        offered.Version,
                        offered.StageId,
                        offered.BestCombinedPushFlipUses)
                    : record);
            }

            if (!found)
            {
                records.Add(new CampaignStagePerformanceState(
                    offered.Version,
                    offered.StageId,
                    offered.BestCombinedPushFlipUses));
            }

            return records;
        }

        private static bool IsValid(CampaignDeathTransitionPlan plan)
        {
            return plan.ExpectedCurrentStageId.IsValid &&
                   CampaignSaveSlotPolicy.IsValidRemainingChances(
                       plan.ExpectedRemainingChances) &&
                   plan.Route.NextStageId.IsValid &&
                   plan.Route.RouteKind != StageRetryRouteKind.None &&
                   CampaignSaveSlotPolicy.IsValidRemainingChances(
                       plan.Route.RemainingChances) &&
                   !string.IsNullOrWhiteSpace(plan.PersistedLevelGroupId);
        }

        private static bool IsValid(CampaignStageClearCommitRequest request)
        {
            return request != null &&
                   request.Plan.CompletedStageId.IsValid &&
                   request.Plan.PersistedStageId.IsValid &&
                   !string.IsNullOrWhiteSpace(request.Plan.PersistedLevelGroupId) &&
                   (request.CompletionReceipt == null ||
                    (request.CompletionReceipt.IsStructurallyValid &&
                     string.Equals(
                         request.CompletionReceipt.CompletedStageId,
                         request.Plan.CompletedStageId.Value,
                         StringComparison.Ordinal))) &&
                   (request.PerformanceRecord == null ||
                    (request.PerformanceRecord.IsStructurallyValid &&
                     request.PerformanceRecord.StageId.Equals(
                         request.Plan.CompletedStageId)));
        }
    }
}
