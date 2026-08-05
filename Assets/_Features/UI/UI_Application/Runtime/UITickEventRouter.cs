using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Gameplay.UIAccess.Presentation;

namespace Game.Feature.UI.Application
{
    public sealed class UITickEventRouter
    {
        private static readonly IComparer<UITickEvent> EventComparer = Comparer<UITickEvent>.Create(CompareEvents);

        public IReadOnlyList<UITickEvent> Route(GameplayPresentationFrame frame)
        {
            if (frame.TickIndex <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frame), "Tick events require a positive frame tick index.");
            }

            var events = new List<UITickEvent>(7);
            if (frame.Topology.HasValue)
            {
                events.Add(CreateGlobalEvent(frame.TickIndex, UITickEventKind.TopologyTransitionStarted));
            }

            if (frame.Player.HasValue)
            {
                AppendPlayerEvents(frame.TickIndex, frame.Player.Value, events);
            }

            if (frame.StageEvent.HasValue &&
                frame.StageEvent.Value.EventKind == GameplayStageEventKind.Cleared)
            {
                events.Add(new UITickEvent(
                    CreateGlobalEvent(frame.TickIndex, UITickEventKind.StageCleared).Key,
                    terminalToken: frame.StageEvent.Value.TerminalToken));
            }

            events.Sort(EventComparer);
            return events;
        }

        private static void AppendPlayerEvents(
            int tickIndex,
            GameplayPlayerPresentationSlice player,
            ICollection<UITickEvent> destination)
        {
            if (player.PlayerEntityId <= 0)
            {
                return;
            }

            if (player.StartedThisTick &&
                player.ActiveActionKind != GameplayUiActionKind.None)
            {
                destination.Add(CreatePlayerEvent(
                    tickIndex,
                    UITickEventKind.PlayerActionStarted,
                    player.PlayerEntityId,
                    player.ActiveActionKind,
                    player.ActiveActionSequence,
                    GameplayUiActionResolutionKind.None));
            }

            if (!player.CanceledThisTick &&
                player.ExecutedThisTick &&
                player.ResolutionKind != GameplayUiActionResolutionKind.None)
            {
                destination.Add(CreatePlayerEvent(
                    tickIndex,
                    UITickEventKind.PlayerActionResolved,
                    player.PlayerEntityId,
                    player.ActiveActionKind,
                    player.ActiveActionSequence,
                    player.ResolutionKind));
            }

            if (!player.CanceledThisTick &&
                player.CompletedThisTick &&
                player.ActiveActionKind != GameplayUiActionKind.None)
            {
                destination.Add(CreatePlayerEvent(
                    tickIndex,
                    UITickEventKind.PlayerActionCompleted,
                    player.PlayerEntityId,
                    player.ActiveActionKind,
                    player.ActiveActionSequence,
                    GameplayUiActionResolutionKind.None));
            }

            if (player.CanceledThisTick &&
                player.ActiveActionKind != GameplayUiActionKind.None)
            {
                destination.Add(CreatePlayerEvent(
                    tickIndex,
                    UITickEventKind.PlayerActionCanceled,
                    player.PlayerEntityId,
                    player.ActiveActionKind,
                    player.ActiveActionSequence,
                    GameplayUiActionResolutionKind.None));
            }

            if (player.TookDamageThisTick)
            {
                destination.Add(
                    new UITickEvent(
                        new UITickEventKey(
                            tickIndex,
                            UITickEventKind.PlayerDamaged,
                            player.PlayerEntityId,
                            player.ActiveActionKind,
                            player.ActiveActionSequence,
                            GameplayUiActionResolutionKind.None),
                        player.DamageAmount));
            }
        }

        private static UITickEvent CreateGlobalEvent(
            int tickIndex,
            UITickEventKind eventKind)
        {
            return new UITickEvent(
                new UITickEventKey(
                    tickIndex,
                    eventKind,
                    actorEntityId: 0,
                    GameplayUiActionKind.None,
                    actionSequence: 0,
                    GameplayUiActionResolutionKind.None));
        }

        private static UITickEvent CreatePlayerEvent(
            int tickIndex,
            UITickEventKind eventKind,
            int actorEntityId,
            GameplayUiActionKind actionKind,
            int actionSequence,
            GameplayUiActionResolutionKind resolutionKind)
        {
            return new UITickEvent(
                new UITickEventKey(
                    tickIndex,
                    eventKind,
                    actorEntityId,
                    actionKind,
                    actionSequence,
                    resolutionKind));
        }

        private static int CompareEvents(UITickEvent left, UITickEvent right)
        {
            var priority = GetPriority(left.EventKind).CompareTo(GetPriority(right.EventKind));
            if (priority != 0)
            {
                return priority;
            }

            var actorCompare = left.ActorEntityId.CompareTo(right.ActorEntityId);
            if (actorCompare != 0)
            {
                return actorCompare;
            }

            var actionCompare = ((int)left.ActionKind).CompareTo((int)right.ActionKind);
            if (actionCompare != 0)
            {
                return actionCompare;
            }

            var sequenceCompare = left.ActionSequence.CompareTo(right.ActionSequence);
            if (sequenceCompare != 0)
            {
                return sequenceCompare;
            }

            return ((int)left.ResolutionKind).CompareTo((int)right.ResolutionKind);
        }

        private static int GetPriority(UITickEventKind eventKind)
        {
            return eventKind switch
            {
                UITickEventKind.TopologyTransitionStarted => 0,
                UITickEventKind.PlayerActionStarted => 1,
                UITickEventKind.PlayerActionResolved => 2,
                UITickEventKind.PlayerActionCompleted => 3,
                UITickEventKind.PlayerActionCanceled => 4,
                UITickEventKind.PlayerDamaged => 5,
                UITickEventKind.StageCleared => 6,
                _ => int.MaxValue,
            };
        }
    }
}
