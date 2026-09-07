using System;
using System.Threading.Tasks;
using Game.Platform.Steam;
using Game.Platform.Steam.ProductAchievements;
using Steamworks;

namespace Game.Exhibition.Integration
{
    /// <summary>Steam maintenance binding owned by one participant-reset application session.</summary>
    public sealed class SteamExhibitionResetAdapter : IExhibitionSteamReset
    {
        private static int sessionGeneration;
        private readonly int ownerGeneration = sessionGeneration;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSessionGeneration() { unchecked { sessionGeneration++; } }

        public ResetIdentity GetIdentity()
        {
            if (ownerGeneration != sessionGeneration)
                throw new InvalidOperationException("The reset belongs to a previous Play session. Restart to resume Pending.");
            if (!SteamAchievementMaintenanceAccess.IsAvailable || !SteamUser.BLoggedOn())
                throw new InvalidOperationException("Steam 연결과 현재 계정 로그인을 확인해 주세요.");
            var identity = new ResetIdentity(SteamUtils.GetAppID().m_AppId, SteamUser.GetSteamID().m_SteamID);
            return identity;
        }

        public async Task ResetAsync(ResetIdentity expected)
        {
            void ValidateIdentity()
            {
                var actual = GetIdentity();
                if (actual.AppId != expected.AppId || actual.SteamId != expected.SteamId)
                    throw new InvalidOperationException("초기화를 시작한 Steam 계정과 AppID로 로그인해 주세요.");
            }
            ValidateIdentity();
            SteamExhibitionResetProtocol protocol = null;
            var lease = SteamAchievementMaintenanceAccess.Acquire(
                observation => protocol?.ObserveStatsStored(observation), observation => { });
            try
            {
                var entries = SteamAchievementMapping.Production.Entries;
                var names = new System.Collections.Generic.List<string>();
                foreach (var entry in entries) names.Add(entry.ExpectedSteamApiName.Value);
                protocol = new SteamExhibitionResetProtocol(
                    lease.Api, SteamUserStats.ClearAchievement, ValidateIdentity,
                    names.ToArray(), expected.AppId, lease.MarkFailed, lease.Dispose,
                    TimeSpan.FromSeconds(30));
            }
            catch
            {
                lease.MarkFailed();
                lease.Dispose();
                throw;
            }
            await protocol.RunAsync();
        }
    }
}
