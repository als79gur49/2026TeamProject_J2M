using Game.Feature.Gameplay.UIAccess.Contracts;
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
        private readonly Action<Exception> _claimFailed;
        private readonly Action _claimFinished;
        internal bool IsClaiming { get; private set; }

        public TerminalArbitrationOwner(PersistentTerminalSessionAuthority authority = null,
            Action<Exception> claimFailed = null, Action claimFinished = null)
        {
            _authority = authority ?? TerminalSessionRegistry.Authority;
            _claimFailed = claimFailed;
            _claimFinished = claimFinished;
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
            return Claim(new TerminalClaimRequest(
                requestedKind,
                sourceSceneGeneration,
                requestedKind == TerminalTransitionKind.Victory
                    ? TerminalDestinationKind.SameSceneStageResult
                    : TerminalDestinationKind.ReloadedGameplay));
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

            return Claim(new TerminalClaimRequest(
                TerminalTransitionKind.Victory,
                EnsureSceneGeneration(),
                TerminalDestinationKind.SameSceneStageResult));
        }

        private TerminalClaimResult Claim(TerminalClaimRequest request)
        {
            IsClaiming = true;
            try
            {
                var claim = _authority.TryClaim(request, accepted => _acceptedClaim = accepted);
                _acceptedClaim = claim;
                return claim;
            }
            catch (Exception exception)
            {
                if (_claimFailed == null) throw;
                _claimFailed(exception);
                return TerminalClaimResult.Reject(request.TerminalKind, TerminalClaimRejectionReason.InvalidRequest);
            }
            finally
            {
                IsClaiming = false;
                _claimFinished?.Invoke();
            }
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
        private readonly TerminalArbitrationOwner _terminalArbiter;
        private GameplayHostPresentationFeed _presentationFeed;
        private bool _handledClear;
        private bool _handledDeath;
        private bool _disposed;
        private bool _terminalSaveSucceeded;
        private bool _handlingTerminal;
        private Exception _runFailure;
        private bool _failureReported;
        private int _lastSurvivalTick = -1;
        private CampaignSlotState _lastSavedSlot;
        private readonly ICampaignRecoveryObservation _recoveryObservation;


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
            ICampaignStageAchievementIntegration campaignStageAchievementIntegration = null,
            CampaignSlotState initialSlot = null,
            ICampaignRecoveryObservation recoveryObservation = null)
        {
            _terminalArbiter = new TerminalArbitrationOwner(
                claimFailed: AbandonForRecovery, claimFinished: FinishAbandonment);
            _lastSavedSlot = initialSlot;
            _recoveryObservation = recoveryObservation;
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
            _presentationFeed.SurvivalTickReady += HandleSurvivalTick;
            if (_recoveryObservation != null) _recoveryObservation.BackupRecovered += HandleBackupRecovered;
            try { _lastSavedSlot ??= _saveSlotStore.LoadSlot(_runningSlotContext.SlotNumber)?.State; }
            catch (Exception exception) { AbandonForRecovery(exception); }
        }

        public void Dispose()
        {
            _disposed = true;
            if (_recoveryObservation != null) _recoveryObservation.BackupRecovered -= HandleBackupRecovered;
            if (_presentationFeed != null)
            {
                _presentationFeed.TerminalClaimAccepted -= HandleTerminalClaimAccepted;
                _presentationFeed.SurvivalTickReady -= HandleSurvivalTick;
            }
        }

        private void HandleSurvivalTick(TickResult result)
        {
            if (_disposed || _host.InputHost.IsCampaignRunAbandoned || _handledClear || _handledDeath ||
                result == null || result.TickIndex <= _lastSurvivalTick || _lastSavedSlot?.GameMode != GameMode.Casual) return;
            _lastSurvivalTick = result.TickIndex;
            foreach (var entity in result.FinalEntities)
            {
                if (entity.entityId != _host.PlayerEntityId || entity.hp <= 0 || entity.markedForDeath) continue;
                if (entity.hp == _lastSavedSlot.ResumeHp) return;
                try
                {
                    var committed = _progressionCommitter.CommitSurvival(_runningSlotContext.SlotNumber,
                        new CampaignSurvivalCommitRequest(_lastSavedSlot.CurrentStageId, _lastSavedSlot.ResumeHp, entity.hp));
                    _lastSavedSlot = committed.Slot;
                }
                catch (Exception exception) { AbandonForRecovery(exception); }
                return;
            }
        }

        private void AbandonForRecovery(Exception exception)
        {
            if (_runFailure != null) return;
            _runFailure = exception;
            _host.InputHost.AbandonCampaignRun();
            // An Iris candidate is owned before its phase notification, so it can stop before Show.
            // Claimed cleanup and the failure popup wait until the original notification returns.
            try
            {
                var claim = _terminalArbiter.AcceptedClaim;
                if (claim.HasValue && claim.Value.Accepted)
                    _terminalTransitionPort.TryAbortSetup(claim.Value.Token,
                        new TerminalFailure("CampaignRunAbandoned", exception.Message));
            }
            catch (Exception cleanupException)
            {
                UnityEngine.Debug.LogWarning($"Campaign local Iris cleanup failed: {cleanupException.Message}");
            }
            finally { FinishAbandonment(); }
        }

        private void FinishAbandonment()
        {
            if (_runFailure == null || _failureReported || _terminalArbiter.IsClaiming || _handlingTerminal) return;
            _failureReported = true;
            var claim = _terminalArbiter.AcceptedClaim;
            try
            {
                if (claim.HasValue && claim.Value.Accepted)
                {
                    var failure = new TerminalFailure("CampaignRunAbandoned", _runFailure.Message);
                    _terminalTransitionPort.TryAbortSetup(claim.Value.Token, failure);
                    TerminalSessionRegistry.Authority.TryAbortClaimBeforeTransition(claim.Value.Token, failure);
                }
            }
            catch (Exception cleanupException)
            {
                UnityEngine.Debug.LogWarning($"Campaign terminal cleanup failed: {cleanupException.Message}");
            }
            finally
            {
                if (claim.HasValue && claim.Value.Accepted) _host.InputHost.TryExitTerminalHold(claim.Value.Token);
                UnityEngine.Debug.LogWarning($"Campaign run stopped ({(_terminalSaveSucceeded ? "after commit" : "save outcome unavailable")}): {_runFailure.Message}");
                if (!_disposed) _presentationFeed?.PublishCampaignFailure();
            }
        }

        private void HandleBackupRecovered()
        {
            if (!_disposed)
                AbandonForRecovery(new InvalidOperationException("Campaign save recovered from backup during gameplay."));
        }

        private CampaignSlotEntry LoadRunningSlot()
        {
            var loaded = _saveSlotStore.LoadAllWithReport();
            CampaignSaveSlotStoreAdapter.ThrowIfCampaignAccessBlocked(loaded.Report);
            if (loaded.Report.Status == CampaignSaveLoadStatus.BackupRecovered || _host.InputHost.IsCampaignRunAbandoned)
                throw new InvalidOperationException("The campaign run cannot use a recovered save.");
            return Array.Find(loaded.Slots, entry => entry.SlotNumber == _runningSlotContext.SlotNumber);
        }

        private void HandleTerminalClaimAccepted(TerminalClaimAcceptedContext context)
        {
            if (_disposed || _host.InputHost.IsCampaignRunAbandoned) return;
            _handlingTerminal = true;
            try { HandleTerminalClaimAcceptedCore(context); }
            catch (Exception exception) { AbandonForRecovery(exception); }
            finally
            {
                _handlingTerminal = false;
                FinishAbandonment();
            }
        }

        private void HandleTerminalClaimAcceptedCore(TerminalClaimAcceptedContext context)
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
                HandleTerminalClaimAccepted(new TerminalClaimAcceptedContext(
                    result, readModel, claim, new StageAttemptMetricsSnapshot(0)));
            }
        }

        private void HandlePlayerDeath(TickResult result, TerminalClaimResult claim)
        {
            _handledDeath = true;
            using var chanceUpdate = _chanceDisplayOverride?.BeginUpdate();

            var runningSlotNumber = _runningSlotContext.SlotNumber;
            var entry = LoadRunningSlot();
            if (entry == null || entry.IsEmpty)
            {
                throw new InvalidOperationException(
                    $"Campaign slot '{runningSlotNumber}' is empty or missing.");
            }

            var slot = entry.State;
            chanceUpdate?.ObserveBeforeMutation(slot);
            var plan = _progressionPlanner.PlanDeath(slot);
            var route = plan.Route;
            var previousRemainingChances = plan.ExpectedRemainingChances;
            var deathCount = slot.TotalDeaths + 1;
            _lastSavedSlot = _progressionCommitter.CommitDeath(runningSlotNumber, plan).Slot;
            _terminalSaveSucceeded = true;
            if (_disposed || _host.InputHost.IsCampaignRunAbandoned) return;

            _host.InputHost.EnterTerminalHold(claim.Token);
            if (slot.GameMode == GameMode.Hardcore) _chanceDisplayOverride?.Set(
                route.RouteKind == StageRetryRouteKind.ReturnToCampaignFirstStage
                    ? 0
                    : route.RemainingChances,
                CampaignSaveSlotPolicy.DefaultRemainingChances,
                GameplayChanceAudioPolicy.SuppressChanceChangeCue);
            chanceUpdate?.Complete();
            if (route.RouteKind == StageRetryRouteKind.ReturnToLevelGroupFirstStage ||
                route.RouteKind == StageRetryRouteKind.ReturnToCampaignFirstStage)
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
            if (_disposed || _host.InputHost.IsCampaignRunAbandoned) return;
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
                throw;
            }

            if (_disposed || _host.InputHost.IsCampaignRunAbandoned) return;
            BindDefeatCameraHandoff(
                playback,
                claim.Token,
                () => _host.Presenter?.CompleteStageTerminalCameraHandoff());

            request = request.WithTransitionHint(
                request.TransitionHint.WithTerminalClaim(claim.Token));
            if (_disposed || _host.InputHost.IsCampaignRunAbandoned) return;
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

            if (_disposed || _host.InputHost.IsCampaignRunAbandoned) return;
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
                throw;
            }

            if (_disposed || _host.InputHost.IsCampaignRunAbandoned) return;
            BindDefeatCameraHandoff(
                playback,
                claim.Token,
                () => _host.Presenter?.CompleteStageTerminalCameraHandoff());

            void HandleBlackReached(TerminalTransitionPlayback completedPlayback)
            {
                completedPlayback.BlackReached -= HandleBlackReached;
                if (_disposed || _host.InputHost.IsCampaignRunAbandoned || completedPlayback.Request.Token != claim.Token)
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
                route.RouteKind == StageRetryRouteKind.ReturnToCampaignFirstStage
                    ? GameplayLevelFailureReason.CampaignChancesExhausted
                    : GameplayLevelFailureReason.CasualDeath,
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
            using var chanceUpdate = _chanceDisplayOverride?.BeginUpdate();
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

                if (_disposed || _host.InputHost.IsCampaignRunAbandoned) return;
                var commit = _progressionCommitter.CommitStageClear(
                    runningSlotNumber,
                    CreateStageClearCommitRequest(
                        transitionPlan,
                        normalCompletion,
                        normalStageClear));
                _terminalSaveSucceeded = true;
                _lastSavedSlot = commit.Slot;
                chanceUpdate?.Complete();
                if (normalStageClear.HasValue)
                {
                    TryEarnCampaignStageAchievements(commit.Slot, normalStageClear.Value);
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

                if (_disposed || _host.InputHost.IsCampaignRunAbandoned) return;
                var commit = _progressionCommitter.CommitStageClear(
                    runningSlotNumber,
                    CreateStageClearCommitRequest(
                        transitionPlan,
                        normalCompletion,
                        normalStageClear));
                _terminalSaveSucceeded = true;
                _lastSavedSlot = commit.Slot;
                if (transitionPlan.RestoresChances && commit.PreviousRemainingChances.HasValue)
                {
                    _chanceDisplayOverride?.Set(
                        commit.PreviousRemainingChances.Value,
                        CampaignSaveSlotPolicy.DefaultRemainingChances,
                        GameplayChanceAudioPolicy.SuppressChanceChangeCue);
                }

                chanceUpdate?.Complete();
                if (normalStageClear.HasValue)
                {
                    TryEarnCampaignStageAchievements(commit.Slot, normalStageClear.Value);
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

        private void TryEarnCampaignStageAchievements(
            CampaignSlotState committedSlot,
            NormalCampaignStageClearFact currentClear)
        {
            try
            {
                _campaignStageAchievementIntegration.TryEarnFromCommittedClear(
                    committedSlot,
                    _sequenceResolver,
                    currentClear);
            }
            catch
            {
                // Level clears recover from records; efficient clears require another qualifying clear.
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
            if (_disposed || _host.InputHost.IsCampaignRunAbandoned) return;
            if (!_terminalTransitionPort.TryBegin(
                    CreateTerminalRequest(claim, TerminalTransitionDestinationMode.SameScene),
                    out var playback))
            {
                throw new InvalidOperationException(
                    $"Accepted terminal token {claim.Token} could not start the required Victory Iris.");
            }

            void HandleBlackReached(TerminalTransitionPlayback completedPlayback)
            {
                completedPlayback.BlackReached -= HandleBlackReached;
                if (_disposed || _host.InputHost.IsCampaignRunAbandoned || completedPlayback.Request.Token != claim.Token)
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

        private StageId ResolveCurrentSlotStageId()
        {
            var entry = LoadRunningSlot();
            return entry == null || entry.IsEmpty
                ? StageId.None
                : entry.State.CurrentStageId;
        }

    }
}
