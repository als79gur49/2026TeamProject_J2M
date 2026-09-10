using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Game.Feature.Stages
{
    public sealed class AtomicTextFileStore : IAtomicTextFileStore
    {
        private static readonly UTF8Encoding Utf8NoBom = new(false);

        private readonly string _rootDirectory;

        public AtomicTextFileStore(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                throw new ArgumentException("A save root directory is required.", nameof(rootDirectory));
            }

            _rootDirectory = rootDirectory;
        }

        public bool Exists(string fileName)
        {
            return File.Exists(GetPath(fileName));
        }

        public string ReadAllText(string fileName)
        {
            CleanupTempFiles(fileName);
            return File.ReadAllText(GetPath(fileName));
        }

        public void WriteAllTextAtomic(string fileName, string contents)
        {
            WriteAllTextAtomic(fileName, contents, preservePreviousAsBackup: true);
        }

        public void WriteAllTextAtomicWithoutBackup(string fileName, string contents)
        {
            WriteAllTextAtomic(fileName, contents, preservePreviousAsBackup: false);
        }

        private void WriteAllTextAtomic(
            string fileName,
            string contents,
            bool preservePreviousAsBackup)
        {
            if (contents == null)
            {
                throw new ArgumentNullException(nameof(contents));
            }

            EnsureDirectory();
            RecoverInterruptedWrite(fileName);
            var canonicalPath = GetPath(fileName);
            var tempPath = CreateTempPath(fileName);

            try
            {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, Utf8NoBom))
                {
                    writer.Write(contents);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (preservePreviousAsBackup)
                {
                    CommitTempFile(tempPath, canonicalPath, GetBackupPath(fileName));
                }
                else
                {
                    CommitTempFileWithoutBackup(tempPath, canonicalPath);
                }
            }
            finally
            {
                DeleteFileBestEffort(tempPath);
                CleanupWriteTempFilesBestEffort(fileName);
            }
        }

        public bool Delete(string fileName)
        {
            CampaignSaveCompositionProvider.RequireProductionWritesAllowed();
            return DeleteFileBestEffort(GetPath(fileName));
        }

        internal void DeleteActiveFileArtifacts(string fileName)
        {
            CampaignSaveCompositionProvider.RequireProductionWritesAllowed();
            var activeFileNames = new[]
            {
                fileName,
                fileName + ".bak",
            };

            for (var i = 0; i < activeFileNames.Length; i++)
            {
                var activeFileName = activeFileNames[i];
                DeleteFileBestEffort(GetPath(activeFileName));
                DeleteFileBestEffort(GetRollbackPath(activeFileName));
                CleanupWriteTempFilesBestEffort(activeFileName);
            }

            var remainingArtifacts = new List<string>();
            for (var i = 0; i < activeFileNames.Length; i++)
            {
                var activeFileName = activeFileNames[i];
                AddIfPresent(remainingArtifacts, GetPath(activeFileName));
                AddIfPresent(remainingArtifacts, GetRollbackPath(activeFileName));
                if (Directory.Exists(_rootDirectory))
                {
                    remainingArtifacts.AddRange(
                        Directory.GetFiles(_rootDirectory, CreateTempSearchPattern(activeFileName)));
                }
            }

            if (remainingArtifacts.Count > 0)
            {
                throw new IOException(
                    $"Active save artifacts could not be fully removed: {string.Join(", ", remainingArtifacts)}");
            }
        }

        public void EnsureDirectory()
        {
            CampaignSaveCompositionProvider.RequireProductionWritesAllowed();
            Directory.CreateDirectory(_rootDirectory);
        }

        public bool TryRestoreBackup(string fileName)
        {
            CampaignSaveCompositionProvider.RequireProductionWritesAllowed();
            var backupPath = GetBackupPath(fileName);
            if (!File.Exists(backupPath))
            {
                return false;
            }

            EnsureDirectory();
            File.Copy(backupPath, GetPath(fileName), overwrite: true);
            return true;
        }

        public bool TryQuarantine(string fileName, out string quarantinePath)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfffffff", CultureInfo.InvariantCulture);
            return TryQuarantine(fileName, $"corrupt.{timestamp}", out quarantinePath);
        }

        public bool TryQuarantine(string fileName, string suffix, out string quarantinePath)
        {
            CampaignSaveCompositionProvider.RequireProductionWritesAllowed();
            quarantinePath = string.Empty;
            if (string.IsNullOrWhiteSpace(suffix) ||
                suffix.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                suffix.Contains("/", StringComparison.Ordinal) ||
                suffix.Contains("\\", StringComparison.Ordinal))
            {
                return false;
            }

            var canonicalPath = GetPath(fileName);
            if (!File.Exists(canonicalPath))
            {
                return false;
            }

            EnsureDirectory();
            quarantinePath = Path.Combine(_rootDirectory, $"{fileName}.{suffix}");
            try
            {
                File.Move(canonicalPath, quarantinePath);
                return true;
            }
            catch
            {
                quarantinePath = string.Empty;
                return false;
            }
        }

        public void CleanupTempFiles(string fileName)
        {
            CampaignSaveCompositionProvider.RequireProductionWritesAllowed();
            RecoverInterruptedWrite(fileName);
            CleanupWriteTempFilesBestEffort(fileName);
        }

        public void RecoverInterruptedWrite(string fileName)
        {
            CampaignSaveCompositionProvider.RequireProductionWritesAllowed();
            var rollbackPath = GetRollbackPath(fileName);
            if (!File.Exists(rollbackPath))
            {
                return;
            }

            var canonicalPath = GetPath(fileName);
            if (File.Exists(canonicalPath))
            {
                if (!DeleteFileBestEffort(rollbackPath) && File.Exists(rollbackPath))
                {
                    throw new IOException(
                        $"Interrupted-write rollback '{rollbackPath}' could not be removed.");
                }

                return;
            }

            EnsureDirectory();
            File.Move(rollbackPath, canonicalPath);
        }

        private void CleanupWriteTempFilesBestEffort(string fileName)
        {
            try
            {
                if (!Directory.Exists(_rootDirectory))
                {
                    return;
                }

                var tempFiles = Directory.GetFiles(_rootDirectory, CreateTempSearchPattern(fileName));
                for (var i = 0; i < tempFiles.Length; i++)
                {
                    DeleteFileBestEffort(tempFiles[i]);
                }
            }
            catch
            {
            }
        }

        private void CommitTempFile(string tempPath, string canonicalPath, string backupPath)
        {
            if (!File.Exists(canonicalPath))
            {
                File.Move(tempPath, canonicalPath);
                return;
            }

            try
            {
                File.Replace(tempPath, canonicalPath, backupPath, ignoreMetadataErrors: true);
                return;
            }
            catch (PlatformNotSupportedException)
            {
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            CommitTempFileWithFallback(tempPath, canonicalPath, backupPath);
        }

        private static void CommitTempFileWithFallback(string tempPath, string canonicalPath, string backupPath)
        {
            var backupUpdated = false;
            try
            {
                File.Copy(canonicalPath, backupPath, overwrite: true);
                backupUpdated = true;
                File.Delete(canonicalPath);
                File.Move(tempPath, canonicalPath);
            }
            catch
            {
                if (!File.Exists(canonicalPath) && backupUpdated && File.Exists(backupPath))
                {
                    File.Copy(backupPath, canonicalPath, overwrite: true);
                }

                throw;
            }
        }

        private static void CommitTempFileWithoutBackup(string tempPath, string canonicalPath)
        {
            if (!File.Exists(canonicalPath))
            {
                File.Move(tempPath, canonicalPath);
                return;
            }

            try
            {
                File.Replace(tempPath, canonicalPath, null, ignoreMetadataErrors: true);
                return;
            }
            catch (PlatformNotSupportedException)
            {
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            var rollbackPath = canonicalPath + ".rollback";
            File.Move(canonicalPath, rollbackPath);
            try
            {
                File.Move(tempPath, canonicalPath);
                DeleteFileBestEffort(rollbackPath);
            }
            catch
            {
                if (!File.Exists(canonicalPath) && File.Exists(rollbackPath))
                {
                    File.Move(rollbackPath, canonicalPath);
                }

                throw;
            }
        }

        private string CreateTempPath(string fileName)
        {
            return Path.Combine(
                _rootDirectory,
                $"{fileName}.write.{Guid.NewGuid():N}.tmp");
        }

        private static string CreateTempSearchPattern(string fileName)
        {
            return $"{fileName}.write.*.tmp";
        }

        private string GetPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("A file name is required.", nameof(fileName));
            }

            return Path.Combine(_rootDirectory, fileName);
        }

        private string GetBackupPath(string fileName)
        {
            return GetPath(fileName + ".bak");
        }

        private string GetRollbackPath(string fileName)
        {
            return GetPath(fileName + ".rollback");
        }

        private static void AddIfPresent(ICollection<string> paths, string path)
        {
            if (File.Exists(path))
            {
                paths.Add(path);
            }
        }

        private static bool DeleteFileBestEffort(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    File.Delete(path);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }
    }
}
