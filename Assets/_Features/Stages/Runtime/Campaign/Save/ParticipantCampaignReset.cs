using System;
using System.IO;

namespace Game.Feature.Stages
{
    /// <summary>Destructive maintenance only; never constructs a facade or resumes profile recovery.</summary>
    public static class ParticipantCampaignReset
    {
        public static void Clear(ISavePathProvider paths)
        {
            if (paths == null) throw new ArgumentNullException(nameof(paths));
            CampaignHudReadRegistry.Reset(CampaignHudReadRegistry.FileKey(paths.SaveRootPath));
            var store = new AtomicTextFileStore(paths.SaveRootPath);
            foreach (var name in new[] { FileCampaignProfileRepository.ProfileFileName,
                CampaignLocalLaunchStateRepository.FileName, CampaignSaveRecoveryService.PendingResetFileName })
            {
                store.DeleteActiveFileArtifacts(name);
                if (!Directory.Exists(paths.SaveRootPath)) continue;
                // Quarantined/rejected copies are participant data too, not reset journals or settings.
                foreach (var path in Directory.GetFiles(paths.SaveRootPath, name + ".*"))
                    File.Delete(path);
                if (store.Exists(name) || Directory.GetFiles(paths.SaveRootPath, name + ".*").Length != 0)
                    throw new IOException("Participant campaign artifacts remain: " + name);
            }
            var handoff = CampaignLaunchHandoffSessionStore.Instance;
            if (handoff.TryPeek(out var pending)) handoff.TryClear(pending.Token);
        }
    }
}
