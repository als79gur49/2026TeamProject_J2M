using System;
using UnityEngine;

namespace Game.Feature.Stages
{
    public sealed class CampaignLaunchHandoff : IEquatable<CampaignLaunchHandoff>
    {
        internal CampaignLaunchHandoff(
            int slotNumber,
            StageId stageId,
            StageNavigationKind navigationKind,
            string source,
            Guid token)
        {
            SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
            if (!stageId.IsValid)
            {
                throw new ArgumentException("Campaign launch handoff requires a canonical StageId.", nameof(stageId));
            }

            if (navigationKind == StageNavigationKind.None)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(navigationKind),
                    navigationKind,
                    "Campaign launch handoff requires a navigation kind.");
            }

            if (string.IsNullOrWhiteSpace(source))
            {
                throw new ArgumentException("Campaign launch handoff requires a source.", nameof(source));
            }

            if (token == Guid.Empty)
            {
                throw new ArgumentException("Campaign launch handoff requires a unique token.", nameof(token));
            }

            SlotNumber = slotNumber;
            StageId = stageId;
            NavigationKind = navigationKind;
            Source = source;
            Token = token;
        }

        public int SlotNumber { get; }

        public StageId StageId { get; }

        public StageNavigationKind NavigationKind { get; }

        public string Source { get; }

        public Guid Token { get; }

        public bool Matches(StageNavigationRequest request)
        {
            return request.IsValid &&
                   StageId.Equals(request.StageId) &&
                   NavigationKind == request.NavigationKind &&
                   string.Equals(Source, request.Source, StringComparison.Ordinal);
        }

        public bool Matches(CampaignLaunchHandoff other)
        {
            return other != null &&
                   Token == other.Token &&
                   SlotNumber == other.SlotNumber &&
                   StageId.Equals(other.StageId) &&
                   NavigationKind == other.NavigationKind &&
                   string.Equals(Source, other.Source, StringComparison.Ordinal);
        }

        public bool Equals(CampaignLaunchHandoff other)
        {
            return other != null && Token == other.Token;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CampaignLaunchHandoff);
        }

        public override int GetHashCode()
        {
            return Token.GetHashCode();
        }

        public override string ToString()
        {
            return
                $"CampaignLaunchHandoff(slot={SlotNumber}, stage={StageId.Value}, navigation={NavigationKind}, source={Source}, token={Token:N})";
        }
    }

    public interface ICampaignLaunchHandoffStore
    {
        bool TryBegin(
            int slotNumber,
            StageId stageId,
            StageNavigationKind navigationKind,
            string source,
            out CampaignLaunchHandoff handoff);

        bool TryPeek(out CampaignLaunchHandoff handoff);

        bool TryClear(Guid token);

        bool TryConsume(Guid token, out CampaignLaunchHandoff handoff);
    }

    public sealed class CampaignLaunchHandoffSessionStore : ICampaignLaunchHandoffStore
    {
        private static readonly object Sync = new();
        private static CampaignLaunchHandoff pending;

        public static CampaignLaunchHandoffSessionStore Instance { get; } = new();

        private CampaignLaunchHandoffSessionStore()
        {
        }

        public bool TryBegin(
            int slotNumber,
            StageId stageId,
            StageNavigationKind navigationKind,
            string source,
            out CampaignLaunchHandoff handoff)
        {
            var candidate = new CampaignLaunchHandoff(
                slotNumber,
                stageId,
                navigationKind,
                source,
                Guid.NewGuid());
            lock (Sync)
            {
                if (pending != null)
                {
                    handoff = pending;
                    return false;
                }

                pending = candidate;
                handoff = candidate;
                return true;
            }
        }

        public bool TryPeek(out CampaignLaunchHandoff handoff)
        {
            lock (Sync)
            {
                handoff = pending;
                return handoff != null;
            }
        }

        public bool TryClear(Guid token)
        {
            lock (Sync)
            {
                if (pending == null || token == Guid.Empty || pending.Token != token)
                {
                    return false;
                }

                pending = null;
                return true;
            }
        }

        public bool TryConsume(Guid token, out CampaignLaunchHandoff handoff)
        {
            lock (Sync)
            {
                if (pending == null || token == Guid.Empty || pending.Token != token)
                {
                    handoff = null;
                    return false;
                }

                handoff = pending;
                pending = null;
                return true;
            }
        }

        internal static void ResetForTests()
        {
            ResetOnSubsystemRegistration();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnSubsystemRegistration()
        {
            lock (Sync)
            {
                pending = null;
            }
        }
    }

    public sealed class CampaignRunningSlotContext : IEquatable<CampaignRunningSlotContext>
    {
        public CampaignRunningSlotContext(int slotNumber)
        {
            SaveSlotStore.ThrowIfInvalidSlotNumber(slotNumber);
            SlotNumber = slotNumber;
        }

        public int SlotNumber { get; }

        public bool Equals(CampaignRunningSlotContext other)
        {
            return other != null && SlotNumber == other.SlotNumber;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CampaignRunningSlotContext);
        }

        public override int GetHashCode()
        {
            return SlotNumber;
        }

        public override string ToString()
        {
            return $"CampaignRunningSlotContext({SlotNumber})";
        }
    }
}
