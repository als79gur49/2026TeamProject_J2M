using System;
using System.IO;
using UnityEngine;

namespace Game.Feature.Stages
{
    public sealed class TemporaryCampaignSavePathProvider : SavePathProviderBase
    {
        private static readonly string PlayerProcessScopeId = Guid.NewGuid().ToString("N");
        private static bool playerQuitCleanupRegistered;

        public TemporaryCampaignSavePathProvider()
            : base(ResolveSaveRootPath())
        {
            RegisterPlayerQuitCleanup();
        }

        private static string ResolveSaveRootPath()
        {
            return ResolveSaveRootPath(
                Application.isEditor,
                Application.dataPath,
                Application.temporaryCachePath,
                PlayerProcessScopeId);
        }

        internal static string ResolveSaveRootPath(
            bool isEditor,
            string dataPath,
            string temporaryCachePath,
            string playerProcessScopeId)
        {
            if (isEditor)
            {
                var projectRoot = Path.GetFullPath(Path.Combine(dataPath, ".."));
                return Path.Combine(
                    projectRoot,
                    "Library",
                    "J2M",
                    "DirectPlayCampaign",
                    ApplicationPersistentDataSavePathProvider.SavesDirectoryName);
            }

            if (!Guid.TryParseExact(playerProcessScopeId, "N", out _))
            {
                throw new ArgumentException(
                    "Player temporary campaign scope must be a process-lifetime GUID.",
                    nameof(playerProcessScopeId));
            }

            var playerCaptureRoot = Path.GetFullPath(Path.Combine(
                temporaryCachePath,
                "J2M",
                "PlayerCaptureCampaign"));
            var saveRoot = Path.GetFullPath(Path.Combine(
                playerCaptureRoot,
                playerProcessScopeId,
                ApplicationPersistentDataSavePathProvider.SavesDirectoryName));
            var containmentPrefix = playerCaptureRoot.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!saveRoot.StartsWith(containmentPrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Player temporary campaign save root escaped its governed cache directory.");
            }

            return saveRoot;
        }

        internal static bool TryDeletePlayerProcessScope(
            string saveRootPath,
            string temporaryCachePath)
        {
            if (string.IsNullOrWhiteSpace(saveRootPath) ||
                string.IsNullOrWhiteSpace(temporaryCachePath))
            {
                return false;
            }

            var governedRoot = Path.GetFullPath(Path.Combine(
                temporaryCachePath,
                "J2M",
                "PlayerCaptureCampaign"));
            var normalizedSaveRoot = Path.GetFullPath(saveRootPath);
            var runDirectory = Directory.GetParent(normalizedSaveRoot);
            if (runDirectory == null ||
                !string.Equals(
                    Path.GetFileName(normalizedSaveRoot),
                    ApplicationPersistentDataSavePathProvider.SavesDirectoryName,
                    StringComparison.Ordinal) ||
                !Guid.TryParseExact(runDirectory.Name, "N", out _) ||
                !string.Equals(
                    runDirectory.Parent?.FullName,
                    governedRoot,
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (Directory.Exists(runDirectory.FullName))
            {
                CampaignHudReadRegistry.Reset(CampaignHudReadRegistry.FileKey(saveRootPath));
                Directory.Delete(runDirectory.FullName, recursive: true);
            }

            return true;
        }

        private void RegisterPlayerQuitCleanup()
        {
            if (Application.isEditor || playerQuitCleanupRegistered)
            {
                return;
            }

            playerQuitCleanupRegistered = true;
            var saveRootPath = SaveRootPath;
            var temporaryCachePath = Application.temporaryCachePath;
            Application.quitting += () =>
            {
                try
                {
                    TryDeletePlayerProcessScope(saveRootPath, temporaryCachePath);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"Temporary campaign save cleanup failed for '{saveRootPath}': {exception.Message}");
                }
            };
        }
    }
}
