using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Game.Platform.Steam;
using Game.Platform.Steam.ProductAchievements;
using UnityEngine;

namespace Game.Exhibition.Integration
{
    public enum ParticipantResetDiagnosticStage { SteamResetVerified, BeforeServices, MenuReady }

    public interface IParticipantResetDiagnostics
    {
        void SteamResetVerified(string[] names, SteamCallbackResult storeResult);
        void Capture(ParticipantResetDiagnosticStage stage);
    }

    /// <summary>Observers must never participate in reset success, failure, or retry policy.</summary>
    internal static class ParticipantResetDiagnosticBoundary
    {
        internal static void Verified(IParticipantResetDiagnostics observer, string[] names, SteamCallbackResult result)
        {
            try { observer?.SteamResetVerified(names, result); }
            catch (Exception) { /* Diagnostic failure cannot fail a committed Steam reset. */ }
        }

        internal static void Capture(IParticipantResetDiagnostics observer, ParticipantResetDiagnosticStage stage)
        {
            try { observer?.Capture(stage); }
            catch (Exception) { /* Diagnostic failure cannot gate normal services or menu entry. */ }
        }
    }

    internal sealed class ParticipantResetDiagnostics : IParticipantResetDiagnostics
    {
        internal delegate bool ReadAchievement(string name, out bool achieved);

        [Serializable]
        internal sealed class AchievementRow
        {
            public string name;
            public bool querySucceeded;
            public bool achieved;
            public string error;
        }

        [Serializable]
        internal sealed class Observation
        {
            public string utc, sessionId, stage, executable, build, initialJournalState;
            public int processId;
            public string journalState, operationId, journalError, identityError;
            public uint appId, recordedAppId;
            public string steamId, recordedSteamId;
            public bool identityReadSucceeded, hasRecordedIdentity, identityMatches;
            public string storeResult;
            public AchievementRow[] achievements;
        }

        private readonly Func<ResetRecord> initialRecord, readRecord;
        private readonly Func<ResetIdentity> readIdentity;
        private readonly ReadAchievement readAchievement;
        private readonly Action<Observation> write;
        private readonly string[] names;
        private readonly HashSet<ParticipantResetDiagnosticStage> observed = new HashSet<ParticipantResetDiagnosticStage>();
        private readonly string sessionId = Guid.NewGuid().ToString("N");

        internal ParticipantResetDiagnostics(Func<ResetRecord> initialRecord, Func<ResetRecord> readRecord,
            Func<ResetIdentity> readIdentity, ReadAchievement readAchievement, string[] names, Action<Observation> write)
        {
            this.initialRecord = initialRecord;
            this.readRecord = readRecord;
            this.readIdentity = readIdentity;
            this.readAchievement = readAchievement;
            this.names = (string[])names.Clone();
            this.write = write;
        }

        internal static IParticipantResetDiagnostics Create(Func<ResetRecord> initial, Func<ResetRecord> journal)
        {
#if J2M_PARTICIPANT_RESET_DIAGNOSTICS
            // Compilation opt-in survives same-exe relaunch; batch automation never binds real Steam.
            if (Application.isBatchMode) return null;
            try
            {
                var mapped = new List<string>();
                foreach (var entry in SteamAchievementMapping.Production.Entries)
                    mapped.Add(entry.ExpectedSteamApiName.Value);
                using var process = Process.GetCurrentProcess();
                var pid = process.Id;
                var exe = process.MainModule.FileName;
                var build = Application.buildGUID + "/" + Application.version;
                var output = Path.Combine(@"D:\J2M\evidence\participant-reset-diagnostics",
                    DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ") + "-" + pid, "observations.jsonl");
                var identity = new SteamExhibitionResetAdapter();
                return new ParticipantResetDiagnostics(initial, journal, identity.GetIdentity,
                    Steamworks.SteamUserStats.GetAchievement, mapped.ToArray(), row =>
                    {
                        row.processId = pid; row.executable = exe; row.build = build;
                        Directory.CreateDirectory(Path.GetDirectoryName(output));
                        File.AppendAllText(output, JsonUtility.ToJson(row) + Environment.NewLine);
                    });
            }
            catch (Exception) { return null; }
#else
            return null;
#endif
        }

        public void SteamResetVerified(string[] verifiedNames, SteamCallbackResult storeResult)
        {
            CaptureCore(ParticipantResetDiagnosticStage.SteamResetVerified, verifiedNames, storeResult.ToString());
        }

        public void Capture(ParticipantResetDiagnosticStage stage) => CaptureCore(stage, null, null);

        private void CaptureCore(ParticipantResetDiagnosticStage stage, string[] verifiedNames, string storeResult)
        {
            // Set before any delegate to prevent duplicate observations even with observer re-entry.
            if (!observed.Add(stage)) return;
            var row = new Observation { utc = DateTime.UtcNow.ToString("o"), sessionId = sessionId,
                stage = stage.ToString(), storeResult = storeResult };
            try { row.initialJournalState = initialRecord()?.State ?? "Absent"; }
            catch (Exception e) { row.initialJournalState = "Unreadable"; row.journalError = e.Message; }
            try
            {
                var record = readRecord();
                row.journalState = record?.State ?? "Absent";
                row.operationId = record?.OperationId;
                row.hasRecordedIdentity = record != null;
                row.recordedAppId = record?.AppId ?? 0;
                row.recordedSteamId = record?.SteamId.ToString();
            }
            catch (Exception e) { row.journalState = "Unreadable"; row.journalError = e.Message; }
            try
            {
                var identity = readIdentity();
                row.appId = identity.AppId; row.steamId = identity.SteamId.ToString();
                row.identityReadSucceeded = true;
                row.identityMatches = row.hasRecordedIdentity && row.appId == row.recordedAppId &&
                    row.steamId == row.recordedSteamId;
            }
            catch (Exception e) { row.identityError = e.Message; }

            var sampleNames = verifiedNames ?? names;
            row.achievements = new AchievementRow[sampleNames.Length];
            for (var i = 0; i < sampleNames.Length; i++)
            {
                var achievement = new AchievementRow { name = sampleNames[i] };
                row.achievements[i] = achievement;
                if (verifiedNames != null)
                {
                    // Reuse the protocol's successful readback, without performing another native query.
                    achievement.querySucceeded = true;
                }
                else if (!row.identityReadSucceeded) achievement.error = "Steam identity unavailable; query skipped.";
                else
                {
                    try
                    {
                        achievement.querySucceeded = readAchievement(sampleNames[i], out achievement.achieved);
                        if (!achievement.querySucceeded) achievement.error = "GetAchievement returned false.";
                    }
                    catch (Exception e) { achievement.error = e.Message; }
                }
            }
            write(row);
        }
    }
}
