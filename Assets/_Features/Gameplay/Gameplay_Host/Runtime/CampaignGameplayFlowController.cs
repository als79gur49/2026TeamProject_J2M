using System;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Host.UIAccess;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Stages;
using Game.Product.Achievements.CampaignIntegration;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class TerminalArbitrationOwner
    {
        private readonly PersistentTerminalSessionAuthority _authority;
        private TerminalClaimResult? _acceptedClaim;

        public TerminalArbitrationOwner(PersistentTerminalSessionAuthority authority = null)
        {
            _authority = authority ?? TerminalSessionRegistry.Authority;
        }

        public TerminalClaimResult? AcceptedClaim => _acceptedClaim;

        public TerminalClaimResult Arbitrate(TickResult result, int playerEntityId)
        {
            if (result == null || playerEntityId <= 0)
            {
                return TerminalClaimResult.Reject(
                    TerminalTransitionKind.Victory,
                    TerminalClaimRejectionReason.InvalidRequest);
            }

            var hasDeath = ContainsPlayerDeathSignal(result, playerEntityId);
            var hasClear = result.ObjectiveResult != null && result.ObjectiveResult.ClearedThisTick;
            if (!hasDeath && !hasClear)
            {
                return TerminalClaimResult.Reject(
                    TerminalTransitionKind.Victory,
                    TerminalClaimRejectionReason.InvalidRequest);
            }

            var requestedKind = hasDeath
                ? TerminalTransitionKind.Defeat
                : TerminalTransitionKind.Victory;
            if (_acceptedClaim.HasValue)
            {
                return TerminalClaimResult.Reject(
                    requestedKind,
                    TerminalClaimRejectionReason.TerminalAlreadyClaimed,
                    _acceptedClaim.Value.Token);
            }

            var sourceSceneGeneration = EnsureSceneGeneration();
            var accepted = _authority.TryClaim(new TerminalClaimRequest(
                requestedKind,
                sourceSceneGeneration,
                requestedKind == TerminalTransitionKind.Victory
                    ? TerminalDestinationKind.SameSceneStageResult
                    : TerminalDestinationKind.ReloadedGameplay));
            _acceptedClaim = accepted;
            return accepted;
        }

        public TerminalClaimResult RejectSameTickVictory(TerminalClaimResult acceptedDefeat)
        {
            if (!acceptedDefeat.Accepted ||
                acceptedDefeat.TerminalKind != TerminalTransitionKind.Defeat)
            {
                throw new ArgumentException(
                    "Same-tick victory rejection requires an accepted defeat claim.",
                    nameof(acceptedDefeat));
            }

            return TerminalClaimResult.Reject(
                TerminalTransitionKind.Victory,
                TerminalClaimRejectionReason.LowerPrioritySameTick,
                acceptedDefeat.Token);
        }

        public TerminalClaimResult ClaimVictory()
        {
            if (_acceptedClaim.HasValue)
            {
                return TerminalClaimResult.Reject(
                    TerminalTransitionKind.Victory,
                    TerminalClaimRejectionReason.TerminalAlreadyClaimed,
                    _acceptedClaim.Value.Token);
            }

            var accepted = _authority.TryClaim(new TerminalClaimRequest(
                TerminalTransitionKind.Victory,
                EnsureSceneGeneration(),
                TerminalDestinationKind.SameSceneStageResult));
            _acceptedClaim = accepted;
            return accepted;
        }

        private long EnsureSceneGeneration()
        {
            return _authority.CurrentSceneGeneration > 0
                ? _authority.CurrentSceneGeneration
                : _authority.RegisterSceneBootstrap(0, "terminal-arbitration");
        }

        private static bool ContainsPlayerDeathSignal(TickResult result, int playerEntityId)
        {
            var signals = result.PresentationData.PlayerDeathSignals;
            for (var i = 0; i < signals.Count; i++)
            {
                if (signals[i].EntityId == playerEntityId && signals[i].DidDieThisTick)
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal sealed class CampaignGameplayFlowController : IDisposable
    {
        private readonly CampaignChanceDisplayOverride _chanceDisplayOverride;
        private readonly GameplaySceneHost _host;
        private readonly CampaignRunningSlotContext _runningSlotContext;
        private readonly IStageLaunchRouter _stageLaunchRouter;
        private readonly ICampaignSaveQuery _saveSlotStore;
        private readonly ICampaignProgressionCommitter _progressionCommitter;
        private readonly CampaignStageSequenceResolver _sequenceResolver;
        private readonly CampaignProgressionTransitionPlanner _progressionPlanner;
        private readonly ITerminalTransitionPort _terminalTransitionPort;
        private readonly EditorDirectPlayContext _editorDirectPlayContext;
        private readonly ICampaignStageAchievementIntegration
            _campaignStageAchievementIntegration;
        private readonly TerminalArbitrationOwner _terminalArbiter = new();
        private GameplayHostPresentationFeed _presentationFeed;
        private bool _handledClear;
        private bool _handledDeath;

        public CampaignGameplayFlowController(
            GameplaySceneHost host,
            ICampaignSaveQuery saveSlotStore,
            ICampaignProgressionCommitter progressionCommitter,
            CampaignRunningSlotContext runningSlotContext,
            CampaignStageSequenceResolver sequenceResolver,
            IStageLaunchRouter stageLaunchRouter,
            CampaignChanceDisplayOverride chanceDisplayOverride,
            ITerminalTransitionPort terminalTransitionPort,
            EditorDirectPlayContext? editorDirectPlayContext = null,
            ICampaignStageAchievementIntegration campaignStageAchievementIntegration = null)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _saveSlotStore = saveSlotStore ?? throw new ArgumentNullException(nameof(saveSlotStore));
            _progressionCommitter = progressionCommitter ??
                throw new ArgumentNullException(nameof(progressionCommitter));
            _runningSlotContext = runningSlotContext ?? throw new ArgumentNullException(nameof(runningSlotContext));
            _sequenceResolver = sequenceResolver ?? throw new ArgumentNullException(nameof(sequenceResolver));
            _stageLaunchRouter = stageLaunchRouter ?? throw new ArgumentNullException(nameof(stageLaunchRouter));
            _chanceDisplayOverride = chanceDisplayOverride;
            _terminalTransitionPort = terminalTransitionPort ??
                throw new ArgumentNullException(nameof(terminalTransitionPort));
            _editorDirectPlayContext = editorDirectPlayContext ??
                EditorDirectPlayContextStore.GetCurrentOrNone();
            _campaignStageAchievementIntegration =
                campaignStageAchievementIntegration ??
                UnavailableCampaignStageAchievementIntegration.Instance;
            _progressionPlanner = new CampaignProgressionTransitionPlanner(_sequenceResolver);
        }

        public void Bind()
        {
            if (_host.InputHost == null)
            {
                throw new InvalidOperationException("Campaign gameplay flow requires an initialized GameplayInputHost.");
            }

            _presentationFeed = _host.UiAccess?.PresentationFeed as GameplayHostPresentationFeed;
            if (_presentationFeed == null)
            {
                throw new InvalidOperationException(
                    "Campaign gameplay flow requires the canonical GameplayHostPresentationFeed.");
            }

            _presentationFeed.ConfigureTerminalArbiter(_terminalArbiter);
            _presentationFeed.TerminalClaimAccepted += HandleTerminalClaimAccepted;
        }

        public void Dispose()
        {
            if (_presentationFeed != null)
            {
                _presentationFeed.TerminalClaimAccepted -= HandleTerminalClaimAccepted;
            }
        }

        private void HandleTerminalClaimAccepted(TerminalClaimAcceptedContext context)
        {
            var result = context.Result;
            var readModel = context.StageCompletion;
            var claim = context.Claim;
            if (!claim.Accepted)
            {
                return;
            }

            if (claim.TerminalKind == TerminalTransitionKind.Defeat)
            {
                if (!_handledDeath)
                {
                    HandlePlayerDeath(result, claim);
                }

                return;
            }

            if (!_handledClear)
            {
                HandleAcceptedStageClear(
                    result,
                    readModel,
                    claim,
                    context.AttemptMetrics);
            }
        }

        // Narrow deterministic seam retained for existing headless campaign tests.
        // Production ownership enters through GameplayHostPresentationFeed.TerminalClaimAccepted.
        private void HandleTickCompleted(TickResult result)
        {
            var claim = _terminalArbiter.Arbitrate(result, _host.InputHost.PlayerEntityId);
            if (claim.Accepted)
            {
                HandleTerminalClaimAccepted(new TerminalClaimAcceptedContext(
                    result,
                    stageCompletion: null,
                    claim,
                    new StageAttemptMetricsSnapshot(0)));
            }
        }

        private void HandleStageClearCommitted(
            TickResult result,
            MinimalStageCompletionReadModel readModel)
        {
            var claim = _terminalArbiter.Arbitrate(result, _host.InputHost.PlayerEntityId);
            if (claim.Accepted)
            {
                HandleTerminalClaimAccepted(new TerminalClaimAcceptedContext(
                    result,
                    readModel,
                    claim,
                    new StageAttemptMetricsSnapshot(0)));
            }
        }

        private void HandleStageClear(
            TickResult result,
            MinimalStageCompletionReadModel readModel)
        {
            var claim = _terminalArbiter.ClaimVictory();
            if (claim.Accepted)
            {
                HandleAcceptedStageClear(
                    result,
                    readModel,
                    claim,
                    new StageAttemptMetricsSnapshot(0));
            }
        }

        private void HandlePlayerDeath(TickResult result, TerminalClaimResult claim)
        {
            _handledDeath = true;

            var runningSlotNumber = _runningSlotContext.SlotNumber;
            var entry = _saveSlotStore.LoadSlot(runningSlotNumber);
            if (entry == null || entry.IsEmpty)
            {
                throw new InvalidOperationException(
                    $"Campaign slot '{runningSlotNumber}' is empty or missing.");
            }

            var slot = entry.State;
            var plan = _progressionPlanner.PlanDeath(slot);
            var route = plan.Route;
            var previousRemainingChances = plan.ExpectedRemainingChances;
            var deathCount = slot.TotalDeaths + 1;
            _progressionCommitter.CommitDeath(runningSlotNumber, plan);

            _host.InputHost.EnterTerminalHold(claim.Token);
            _chanceDisplayOverride?.Set(
                route.RouteKind == StageRetryRouteKind.ReturnToLevelGroupFirstStage
                    ? 0
                    : route.RemainingChances,
                CampaignSaveSlotPolicy.DefaultRemainingChances,
                GameplayChanceAudioPolicy.SuppressChanceChangeCue);
            if (route.RouteKind == StageRetryRouteKind.ReturnToLevelGroupFirstStage)
            {
                _host.Presenter?.ApplyStageTerminalPresentation(
                    GameplayStageTerminalPresentationReason.LevelFailed,
                    result);
                BeginLevelFailedTerminal(route, claim);
                return;
            }

            _host.Presenter?.ApplyStageTerminalPresentation(
                GameplayStageTerminalPresentationReason.PlayerDeathRetry,
                result);
            var request = new StageNavigationRequest(
                route.NextStageId,
                StageNavigationKind.Retry,
                "campaign-death-retry",
                StageTransitionHint.ForChanceLost(new StageTransitionChanceLostPayload(
                    previousRemainingChances,
                    route.RemainingChances,
                    CampaignSaveSlotPolicy.DefaultRemainingChances,
                    slot.CurrentStageId,
                    route.NextStageId,
                    deathCount,
                    "campaign-death-retry")),
                SceneTransitionIntent.DeathRetry,
                _editorDirectPlayContext.ForStage(route.NextStageId));
            TerminalTransitionPlayback playback;
            try
            {
                if (!_terminalTransitionPort.TryBegin(
                        CreateTerminalRequest(claim, TerminalTransitionDestinationMode.SceneHandoff),
                        out playback))
                {
                    throw new InvalidOperationException(
                        $"Accepted terminal token {claim.Token} could not start the required Defeat Iris.");
                }
            }
            catch
            {
                _host.Presenter?.CompleteStageTerminalCameraHandoff();
                if (TryRecoverIrisSetupFailure(claim.Token))
                {
                    _stageLaunchRouter.Launch(request);
                }

                throw;
            }

            BindDefeatCameraHandoff(
                playback,
                claim.Token,
                () => _host.Presenter?.CompleteStageTerminalCameraHandoff());

            request = request.WithTransitionHint(
                request.TransitionHint.WithTerminalClaim(claim.Token));
            _stageLaunchRouter.Launch(request);
        }

        private void BeginLevelFailedTerminal(
            StageRetryRouteResult route,
            TerminalClaimResult claim)
        {
            if (!TerminalSessionRegistry.Authority.TrySetDestinationKind(
                    claim.Token,
                    TerminalDestinationKind.SameSceneLevelFailed))
            {
                throw new InvalidOperationException(
                    $"Accepted terminal token {claim.Token} could not bind the LevelFailed destination.");
            }

            TerminalTransitionPlayback playback;
            try
            {
                if (!_terminalTransitionPort.TryBegin(
                        CreateTerminalRequest(claim, TerminalTransitionDestinationMode.SameScene),
                        out playback))
                {
                    throw new InvalidOperationException(
                        $"Accepted terminal token {claim.Token} could not start the required Defeat Iris.");
                }
            }
            catch
            {
                _host.Presenter?.CompleteStageTerminalCameraHandoff();
                if (TryRecoverIrisSetupFailure(claim.Token))
                {
                    PublishLevelFailed(route, claim.Token);
                }

                throw;
            }

            BindDefeatCameraHandoff(
                playback,
                claim.Token,
                () => _host.Presenter?.CompleteStageTerminalCameraHandoff());

            void HandleBlackReached(TerminalTransitionPlayback completedPlayback)
            {
                completedPlayback.BlackReached -= HandleBlackReached;
                if (completedPlayback.Request.Token != claim.Token)
                {
                    return;
                }

                PublishLevelFailed(route, claim.Token);
            }

            playback.BlackReached += HandleBlackReached;
        }

        internal static void BindDefeatCameraHandoff(
            TerminalTransitionPlayback playback,
            TerminalSessionToken token,
            Action completeHandoff)
        {
            if (completeHandoff == null)
            {
                throw new ArgumentNullException(nameof(completeHandoff));
            }

            if (playback == null)
            {
                completeHandoff();
                return;
            }

            void HandleStateChanged(TerminalTransitionPlayback changedPlayback)
            {
                if (changedPlayback.Request.Token != token ||
                    (changedPlayback.State != TerminalTransitionState.Closing &&
                     changedPlayback.State != TerminalTransitionState.Black &&
                     changedPlayback.State != TerminalTransitionState.Cancelled &&
                     changedPlayback.State != TerminalTransitionState.Completed))
                {
                    return;
                }

                changedPlayback.StateChanged -= HandleStateChanged;
                completeHandoff();
            }

            playback.StateChanged += HandleStateChanged;
            HandleStateChanged(playback);
        }

        private void PublishLevelFailed(
            StageRetryRouteResult route,
            TerminalSessionToken token)
        {
            if (_presentationFeed == null)
            {
                throw new InvalidOperationException("Campaign level failed flow requires a gameplay presentation feed.");
            }

            _presentationFeed.PublishLevelFailed(new GameplayLevelFailedReadModel(
                GameplayLevelFailureReason.ChancesExhausted,
                new StageNavigationRequest(
                    route.NextStageId,
                    StageNavigationKind.Retry,
                    "level-failed-restart-level",
                    StageTransitionHint.ForKind(StageTransitionKind.LevelFailedRestart),
                    SceneTransitionIntent.ManualRetry,
                    _editorDirectPlayContext.ForStage(route.NextStageId)),
                token));
        }

        private void HandleAcceptedStageClear(
            TickResult result,
            MinimalStageCompletionReadModel readModel,
            TerminalClaimResult claim,
            StageAttemptMetricsSnapshot attemptMetrics)
        {
            _handledClear = true;
            _host.InputHost.EnterTerminalHold(claim.Token);

            var completedStageId = readModel != null && readModel.StageId.IsValid
                ? readModel.StageId
                : ResolveCurrentSlotStageId();
            if (!completedStageId.IsValid)
            {
                throw new InvalidOperationException("Campaign clear flow could not resolve the completed stage id.");
            }

            var runningSlotNumber = _runningSlotContext.SlotNumber;
            var normalCompletion = TryCreateNormalCampaignCompletionFact(
                result,
                readModel,
                claim,
                completedStageId,
                out var completionFact)
                ? completionFact
                : (StageId?)null;
            var normalStageClear = TryCreateNormalCampaignStageClearFact(
                result,
                readModel,
                claim,
                completedStageId,
                attemptMetrics,
                out var normalStageClearFact)
                ? normalStageClearFact
                : (NormalCampaignStageClearFact?)null;
            var transitionPlan = _progressionPlanner.PlanStageClear(completedStageId);
            if (transitionPlan.IsCampaignCompleted)
            {
                if (!TerminalSessionRegistry.Authority.TrySetDestinationKind(
                        claim.Token,
                        TerminalDestinationKind.SameSceneGameClear))
                {
                    throw new InvalidOperationException(
                        $"Accepted terminal token {claim.Token} could not bind the GameClear destination.");
                }

                var commit = _progressionCommitter.CommitStageClear(
                    runningSlotNumber,
                    CreateStageClearCommitRequest(
                        transitionPlan,
                        normalCompletion,
                        normalStageClear));
                if (normalStageClear.HasValue)
                {
                    TryEarnCampaignStageAchievements(commit.Slot);
                }
            }
            else
            {
                if (!TerminalSessionRegistry.Authority.TrySetDestinationKind(
                        claim.Token,
                        TerminalDestinationKind.SameSceneStageResult))
                {
                    throw new InvalidOperationException(
                        $"Accepted terminal token {claim.Token} could not bind the StageResult destination.");
                }

                var commit = _progressionCommitter.CommitStageClear(
                    runningSlotNumber,
                    CreateStageClearCommitRequest(
                        transitionPlan,
                        normalCompletion,
                        normalStageClear));
                if (transitionPlan.RestoresChances)
                {
                    _chanceDisplayOverride?.Set(
                        commit.PreviousRemainingChances,
                        CampaignSaveSlotPolicy.DefaultRemainingChances,
                        GameplayChanceAudioPolicy.SuppressChanceChangeCue);
                }

                if (normalStageClear.HasValue)
                {
                    TryEarnCampaignStageAchievements(commit.Slot);
                }
            }

            BeginVictoryTerminal(claim);
        }

        private static CampaignStageClearCommitRequest CreateStageClearCommitRequest(
            CampaignStageClearTransitionPlan transitionPlan,
            StageId? normalCompletion,
            NormalCampaignStageClearFact? normalStageClear)
        {
            NormalStagePerformanceRecord performanceRecord = null;
            if (normalStageClear.HasValue)
            {
                performanceRecord = new NormalStagePerformanceRecord
                {
                    Version = NormalStagePerformanceRecord.CurrentVersion,
                    StageId = normalStageClear.Value.StageId,
                    BestCombinedPushFlipUses = normalStageClear.Value.CombinedPushFlipUses,
                };
            }

            return new CampaignStageClearCommitRequest
            {
                Plan = transitionPlan,
                CompletionReceipt = normalCompletion.HasValue
                    ? NormalCampaignCompletionReceiptPolicy.CreateV2(
                        normalCompletion.Value)
                    : null,
                PerformanceRecord = performanceRecord,
            };
        }

        private void TryEarnCampaignStageAchievements(CampaignSlotState committedSlot)
        {
            try
            {
                _campaignStageAchievementIntegration.TryEarnFromCommittedSlot(
                    committedSlot,
                    _sequenceResolver);
            }
            catch
            {
                // Durable performance records remain the startup recovery source.
            }
        }

        private bool TryCreateNormalCampaignStageClearFact(
            TickResult result,
            MinimalStageCompletionReadModel readModel,
            TerminalClaimResult claim,
            StageId completedStageId,
            StageAttemptMetricsSnapshot metrics,
            out NormalCampaignStageClearFact completion)
        {
            completion = default;
            if (!claim.Accepted ||
                claim.TerminalKind != TerminalTransitionKind.Victory ||
                result == null ||
                result.ObjectiveResult == null ||
                !result.ObjectiveResult.ClearedThisTick ||
                readModel == null ||
                !readModel.StageId.Equals(completedStageId) ||
                readModel.FinalTickIndex != result.TickIndex ||
                _editorDirectPlayContext.Mode != EditorDirectPlayMode.None ||
                !_sequenceResolver.Contains(completedStageId))
            {
                return false;
            }

            completion = new NormalCampaignStageClearFact(
                completedStageId,
                metrics.CombinedPushFlipUses);
            return true;
        }

        private bool TryCreateNormalCampaignCompletionFact(
            TickResult result,
            MinimalStageCompletionReadModel readModel,
            TerminalClaimResult claim,
            StageId completedStageId,
            out StageId completion)
        {
            completion = default;
            if (!claim.Accepted ||
                claim.TerminalKind != TerminalTransitionKind.Victory ||
                result == null ||
                result.ObjectiveResult == null ||
                !result.ObjectiveResult.ClearedThisTick ||
                readModel == null ||
                !readModel.StageId.Equals(completedStageId) ||
                readModel.FinalTickIndex != result.TickIndex ||
                _editorDirectPlayContext.Mode != EditorDirectPlayMode.None ||
                !_sequenceResolver.Contains(completedStageId) ||
                !_sequenceResolver.IsFinal(completedStageId))
            {
                return false;
            }

            completion = completedStageId;
            return true;
        }

        private void BeginVictoryTerminal(TerminalClaimResult claim)
        {
            TerminalTransitionPlayback playback;
            try
            {
                if (!_terminalTransitionPort.TryBegin(
                        CreateTerminalRequest(claim, TerminalTransitionDestinationMode.SameScene),
                        out playback))
                {
                    throw new InvalidOperationException(
                        $"Accepted terminal token {claim.Token} could not start the required Victory Iris.");
                }
            }
            catch
            {
                if (TryRecoverIrisSetupFailure(claim.Token))
                {
                    _presentationFeed?.ReleaseStageClearTerminalGate(claim.Token);
                }

                throw;
            }

            void HandleBlackReached(TerminalTransitionPlayback completedPlayback)
            {
                completedPlayback.BlackReached -= HandleBlackReached;
                if (completedPlayback.Request.Token != claim.Token)
                {
                    return;
                }

                if (_presentationFeed == null ||
                    !_presentationFeed.ReleaseStageClearTerminalGate(claim.Token))
                {
                    throw new InvalidOperationException(
                        $"Victory terminal token {claim.Token} reached black without a matching StageCleared gate.");
                }
            }

            playback.BlackReached += HandleBlackReached;
        }

        private TerminalTransitionRequest CreateTerminalRequest(
            TerminalClaimResult claim,
            TerminalTransitionDestinationMode destinationMode)
        {
            if (!claim.Accepted)
            {
                throw new ArgumentException(
                    "Terminal Iris requests require an accepted canonical claim.",
                    nameof(claim));
            }

            return new TerminalTransitionRequest(
                claim.TerminalKind,
                _host.InputHost.PlayerEntityId,
                claim.Token,
                destinationMode);
        }

        private bool TryRecoverIrisSetupFailure(TerminalSessionToken token)
        {
            var session = TerminalSessionRegistry.Current;
            return !session.IsActive &&
                   session.Token == token &&
                   session.Phase == TerminalSessionPhase.FailedBeforeCover &&
                   _host.InputHost.TryExitTerminalHold(token);
        }

        private StageId ResolveCurrentSlotStageId()
        {
            var entry = _saveSlotStore.LoadSlot(_runningSlotContext.SlotNumber);
            return entry == null || entry.IsEmpty
                ? StageId.None
                : entry.State.CurrentStageId;
        }

    }
}
