using System;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Gameplay.UIAccess.Queries;
using Game.Feature.UI.Application;

namespace Game.Feature.UI.Tests
{
    internal sealed class FakeGameplayCommandGateway : IGameplayCommandGateway
    {
        public int ClearHeldMoveDirectionCallCount { get; private set; }

        public int RequestFlipCallCount { get; private set; }

        public int SetHeldMoveDirectionCallCount { get; private set; }

        public Func<GameplayUiDirection, GameplayCommandAcceptance> OnRequestFlip { get; set; } =
            _ => GameplayCommandAcceptance.Accept();

        public Func<GameplayUiDirection, GameplayCommandAcceptance> OnSetHeldMoveDirection { get; set; } =
            _ => GameplayCommandAcceptance.Accept();

        public Func<GameplayCommandAcceptance> OnClearHeldMoveDirection { get; set; } =
            () => GameplayCommandAcceptance.Accept();

        public GameplayCommandAcceptance SetHeldMoveDirection(GameplayUiDirection direction)
        {
            SetHeldMoveDirectionCallCount++;
            return OnSetHeldMoveDirection(direction);
        }

        public GameplayCommandAcceptance ClearHeldMoveDirection()
        {
            ClearHeldMoveDirectionCallCount++;
            return OnClearHeldMoveDirection();
        }

        public GameplayCommandAcceptance RequestFlip(GameplayUiDirection direction)
        {
            RequestFlipCallCount++;
            return OnRequestFlip(direction);
        }
    }

    internal sealed class FakeGameplayPauseService : IGameplayPauseService, IUiFlowPauseService
    {
        public event Action<bool> PauseChanged;

        public bool IsPaused { get; private set; }

        public int PauseCallCount { get; private set; }

        public int ResumeCallCount { get; private set; }

        public void Pause()
        {
            PauseCallCount++;
            if (IsPaused)
            {
                return;
            }

            IsPaused = true;
            PauseChanged?.Invoke(true);
        }

        public void Resume()
        {
            ResumeCallCount++;
            if (!IsPaused)
            {
                return;
            }

            IsPaused = false;
            PauseChanged?.Invoke(false);
        }

        public void Toggle()
        {
            if (IsPaused)
            {
                Resume();
                return;
            }

            Pause();
        }
    }

    internal sealed class FakeGameplayPresentationFeed : IGameplayPresentationFeed
    {
        public event Action<GameplayPresentationFrame> FramePublished;

        public event Action<GameplayPresentationState> StateChanged;

        public GameplayPresentationState CurrentState { get; private set; } =
            new GameplayPresentationState(new GameplayUiTopology(GameplayUiFace.Floor), false, false, false);

        public void PublishFrame(GameplayPresentationFrame frame)
        {
            FramePublished?.Invoke(frame);
        }

        public void PublishState(GameplayPresentationState state)
        {
            CurrentState = state;
            StateChanged?.Invoke(state);
        }
    }

    internal sealed class FakeGameplayQueryFacade : IGameplayQueryFacade
    {
        private readonly MutableObjectiveQuery _objectiveQuery;
        private readonly MutablePlayerHudQuery _playerHudQuery;
        private readonly MutableSessionQuery _sessionQuery;

        public FakeGameplayQueryFacade(
            GameplaySessionReadModel session,
            GameplayPlayerHudReadModel playerHud,
            GameplayObjectiveReadModel objective)
        {
            _sessionQuery = new MutableSessionQuery(session);
            _playerHudQuery = new MutablePlayerHudQuery(playerHud);
            _objectiveQuery = new MutableObjectiveQuery(objective);
        }

        public IGameplaySessionQuery Session => _sessionQuery;

        public IGameplayPlayerHudQuery PlayerHud => _playerHudQuery;

        public IGameplayObjectiveQuery Objectives => _objectiveQuery;

        public void SetSession(GameplaySessionReadModel session)
        {
            _sessionQuery.Value = session;
        }

        public void SetPlayerHud(GameplayPlayerHudReadModel playerHud)
        {
            _playerHudQuery.Value = playerHud;
        }

        public void SetObjective(GameplayObjectiveReadModel objective)
        {
            _objectiveQuery.Value = objective;
        }

        public static GameplayPlayerHudReadModel CreateDefaultPlayerHud()
        {
            return new GameplayPlayerHudReadModel(
                isAvailable: true,
                playerEntityId: 10,
                currentHp: 3,
                facing: GameplayUiDirection.Up,
                activeActionKind: GameplayUiActionKind.None,
                activeActionDirection: GameplayUiDirection.None,
                activeTargetEntityId: 0,
                isActionInProgress: false,
                isActionInRecoveryPhase: false,
                canMoveThisTick: true,
                canStartActionThisTick: true);
        }

        private sealed class MutableSessionQuery : IGameplaySessionQuery
        {
            public MutableSessionQuery(GameplaySessionReadModel value)
            {
                Value = value;
            }

            public GameplaySessionReadModel Value { get; set; }

            public GameplaySessionReadModel Read()
            {
                return Value;
            }
        }

        private sealed class MutablePlayerHudQuery : IGameplayPlayerHudQuery
        {
            public MutablePlayerHudQuery(GameplayPlayerHudReadModel value)
            {
                Value = value;
            }

            public GameplayPlayerHudReadModel Value { get; set; }

            public GameplayPlayerHudReadModel Read()
            {
                return Value;
            }
        }

        private sealed class MutableObjectiveQuery : IGameplayObjectiveQuery
        {
            public MutableObjectiveQuery(GameplayObjectiveReadModel value)
            {
                Value = value;
            }

            public GameplayObjectiveReadModel Value { get; set; }

            public GameplayObjectiveReadModel Read()
            {
                return Value;
            }
        }
    }

    internal static class UiTestPortFactory
    {
        public static GameplayUiFlowPorts CreatePorts(
            FakeGameplayCommandGateway commandGateway = null,
            FakeGameplayQueryFacade queryFacade = null,
            FakeGameplayPresentationFeed presentationFeed = null,
            FakeGameplayPauseService pauseService = null)
        {
            commandGateway ??= new FakeGameplayCommandGateway();
            queryFacade ??= new FakeGameplayQueryFacade(
                new GameplaySessionReadModel(1, false, true, false),
                FakeGameplayQueryFacade.CreateDefaultPlayerHud(),
                new GameplayObjectiveReadModel(false, false, false, false));
            pauseService ??= new FakeGameplayPauseService();
            return new GameplayUiFlowPorts(
                commandGateway,
                queryFacade,
                CreatePresentationSource(queryFacade, presentationFeed, pauseService),
                pauseService);
        }

        public static GameplayUiPresentationSource CreatePresentationSource(
            FakeGameplayQueryFacade queryFacade = null,
            FakeGameplayPresentationFeed presentationFeed = null,
            FakeGameplayPauseService pauseService = null)
        {
            queryFacade ??= new FakeGameplayQueryFacade(
                new GameplaySessionReadModel(1, false, true, false),
                FakeGameplayQueryFacade.CreateDefaultPlayerHud(),
                new GameplayObjectiveReadModel(false, false, false, false));
            presentationFeed ??= new FakeGameplayPresentationFeed();
            pauseService ??= new FakeGameplayPauseService();
            return new GameplayUiPresentationSource(queryFacade, presentationFeed, pauseService);
        }
    }
}
