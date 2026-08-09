using System;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Host.UIAccess;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Stages;

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
        private readonly ICampaignSaveSlotStore _saveSlotStore;
        private readonly CampaignStageSequenceResolver _sequenceResolver;
        private readonly StageRetryChanceTracker _retryChanceTracker;
        private readonly ITerminalTransitionPort _terminalTransitionPort;
        private readonly EditorDirectPlayContext _editorDirectPlayContext;
        private readonly TerminalArbitrationOwner _terminalArbiter = new();
        private GameplayHostPresentationFeed _presentationFeed;
        private bool _handledClear;
        private bool _handledDeath;

        public CampaignGameplayFlowController(
            GameplaySceneHost host,
            ICampaignSaveSlotStore saveSlotStore,
            CampaignRunningSlotContext runningSlotContext,
            CampaignStageSequenceResolver sequenceResolver,
            IStageLaunchRouter stageLaunchRouter,
            CampaignChanceDisplayOverride chanceDisplayOverride,
            ITerminalTransitionPort terminalTransitionPort,
            EditorDirectPlayContext? editorDirectPlayContext = null)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _saveSlotStore = saveSlotStore ?? throw new ArgumentNullException(nameof(saveSlotStore));
            _runningSlotContext = runningSlotContext ?? throw new ArgumentNullException(nameof(runningSlotContext));
            _sequenceResolver = sequenceResolver ?? throw new ArgumentNullException(nameof(sequenceResolver));
            _stageLaunchRouter = stageLaunchRouter ?? throw new ArgumentNullException(nameof(stageLaunchRouter));
            _chanceDisplayOverride = chanceDisplayOverride;
            _terminalTransitionPort = terminalTransitionPort ??
                throw new ArgumentNullException(nameof(terminalTransitionPort));
            _editorDirectPlayContext = editorDirectPlayContext ??
                EditorDirectPlayContextStore.GetCurrentOrNone();
            _retryChanceTracker = new StageRetryChanceTracker(_sequenceResolver);
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

        private void HandleTerminalClaimAccepted(
            TickResult result,
            MinimalStageCompletionReadModel readModel,
            TerminalClaimResult claim)
        {
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
                HandleAcceptedStageClear(result, readModel, claim);
            }
        }

        // Narrow deterministic seam retained for existing headless campaign tests.
        // Production ownership enters through GameplayHostPresentationFeed.TerminalClaimAccepted.
        private void HandleTickCompleted(TickResult result)
        {
            var claim = _terminalArbiter.Arbitrate(result, _host.InputHost.PlayerEntityId);
            if (claim.Accepted)
            {
                HandleTerminalClaimAccepted(result, null, claim);
            }
        }

        private void HandleStageClearCommitted(
            TickResult result,
            MinimalStageCompletionReadModel readModel)
        {
            var claim = _terminalArbiter.Arbitrate(result, _host.InputHost.PlayerEntityId);
            if (claim.Accepted)
            {
                HandleTerminalClaimAccepted(result, readModel, claim);
            }
        }

        private void HandleStageClear(
            TickResult result,
            MinimalStageCompletionReadModel readModel)
        {
            var claim = _terminalArbiter.ClaimVictory();
            if (claim.Accepted)
            {
                HandleAcceptedStageClear(result, readModel, claim);
            }
        }

        private void HandlePlayerDeath(TickResult result, TerminalClaimResult claim)
        {
            _handledDeath = true;

            var runningSlotNumber = _runningSlotContext.SlotNumber;
            var slot = _saveSlotStore.LoadSlot(runningSlotNumber);
            var route = _retryChanceTracker.ResolveDeathRoute(slot);
            var previousRemainingChances = slot.RemainingChances <= 0
                ? SaveSlotStore.DefaultRemainingChances
                : slot.RemainingChances;
            var deathCount = slot.TotalDeaths + 1;
            var routeLevelGroupId = _sequenceResolver.GetLevelGroupId(route.NextStageId);
            _saveSlotStore.UpdateSlot(
                runningSlotNumber,
                mutableSlot =>
                {
                    mutableSlot.CurrentStageId = route.NextStageId;
                    mutableSlot.CurrentLevelGroupId = routeLevelGroupId;
                    mutableSlot.RemainingChances = route.RemainingChances;
                    mutableSlot.TotalDeaths += 1;
                    mutableSlot.LastPlayedAt = DateTimeOffset.UtcNow.ToString("O");
                });

            _host.InputHost.EnterTerminalHold(claim.Token);
            _chanceDisplayOverride?.Set(
                route.RouteKind == StageRetryRouteKind.ReturnToLevelGroupFirstStage
                    ? 0
                    : route.RemainingChances,
                SaveSlotStore.DefaultRemainingChances,
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
                    SaveSlotStore.DefaultRemainingChances,
                    slot.CurrentStageId,
                    route.NextStageId,
                    deathCount,
                    "campaign-death-retry")),
                SceneTransitionIntent.DeathRetry,
                _editorDirectPlayContext.ForStage(route.NextStageId));
            try
            {
                if (!_terminalTransitionPort.TryBegin(
                        CreateTerminalRequest(claim, TerminalTransitionDestinationMode.SceneHandoff),
                        out _))
                {
                    throw new InvalidOperationException(
                        $"Accepted terminal token {claim.Token} could not start the required Defeat Iris.");
                }
            }
            catch
            {
                if (TryRecoverIrisSetupFailure(claim.Token))
                {
                    _stageLaunchRouter.Launch(request);
                }

                throw;
            }

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
                if (TryRecoverIrisSetupFailure(claim.Token))
                {
                    PublishLevelFailed(route, claim.Token);
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

                PublishLevelFailed(route, claim.Token);
            }

            playback.BlackReached += HandleBlackReached;
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
            TerminalClaimResult claim)
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
            if (_sequenceResolver.IsFinal(completedStageId))
            {
                if (!TerminalSessionRegistry.Authority.TrySetDestinationKind(
                        claim.Token,
                        TerminalDestinationKind.SameSceneGameClear))
                {
                    throw new InvalidOperationException(
                        $"Accepted terminal token {claim.Token} could not bind the GameClear destination.");
                }

                var receiptCreation = NormalCampaignCompletionReceiptPolicy.Evaluate(
                    _editorDirectPlayContext,
                    readModel?.Result,
                    completedStageId,
                    _sequenceResolver);
                _saveSlotStore.UpdateSlot(
                    runningSlotNumber,
                    mutableSlot =>
                    {
                        mutableSlot.CurrentStageId = completedStageId;
                        mutableSlot.CurrentLevelGroupId = _sequenceResolver.GetLevelGroupId(completedStageId);
                        mutableSlot.CampaignCompleted = true;
                        if (receiptCreation.IsEligible &&
                            !mutableSlot.HasNormalCampaignCompletionReceipt &&
                            mutableSlot.NormalCampaignCompletionReceipt == null)
                        {
                            mutableSlot.HasNormalCampaignCompletionReceipt = true;
                            mutableSlot.NormalCampaignCompletionReceipt =
                                receiptCreation.Receipt.Clone();
                        }

                        mutableSlot.LastPlayedAt = DateTimeOffset.UtcNow.ToString("O");
                    });
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

                if (!_sequenceResolver.TryGetNext(completedStageId, out var nextStageId))
                {
                    throw new InvalidOperationException(
                        $"Campaign sequence could not resolve a next stage for '{completedStageId.Value}'.");
                }

                var completedLevelGroupId = _sequenceResolver.GetLevelGroupId(completedStageId);
                var nextLevelGroupId = _sequenceResolver.GetLevelGroupId(nextStageId);
                var restoresChances = !string.Equals(
                    completedLevelGroupId,
                    nextLevelGroupId,
                    StringComparison.Ordinal);
                var currentSceneRemainingChances = SaveSlotStore.DefaultRemainingChances;
                _saveSlotStore.UpdateSlot(
                    runningSlotNumber,
                    mutableSlot =>
                    {
                        if (restoresChances)
                        {
                            currentSceneRemainingChances = mutableSlot.RemainingChances <= 0
                                ? SaveSlotStore.DefaultRemainingChances
                                : Math.Min(
                                    mutableSlot.RemainingChances,
                                    SaveSlotStore.DefaultRemainingChances);
                            mutableSlot.RemainingChances = SaveSlotStore.DefaultRemainingChances;
                        }

                        mutableSlot.CurrentStageId = nextStageId;
                        mutableSlot.CurrentLevelGroupId = nextLevelGroupId;
                        mutableSlot.LastPlayedAt = DateTimeOffset.UtcNow.ToString("O");
                    });
                if (restoresChances)
                {
                    _chanceDisplayOverride?.Set(
                        currentSceneRemainingChances,
                        SaveSlotStore.DefaultRemainingChances,
                        GameplayChanceAudioPolicy.SuppressChanceChangeCue);
                }
            }

            BeginVictoryTerminal(claim);
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
            return _saveSlotStore.LoadSlot(_runningSlotContext.SlotNumber).CurrentStageId;
        }

    }
}
