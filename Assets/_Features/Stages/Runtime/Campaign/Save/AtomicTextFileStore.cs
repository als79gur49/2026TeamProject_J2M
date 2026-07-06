using System;
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
            if (contents == null)
            {
                throw new ArgumentNullException(nameof(contents));
            }

            EnsureDirectory();
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

                CommitTempFile(tempPath, canonicalPath, GetBackupPath(fileName));
            }
            finally
            {
                DeleteFileBestEffort(tempPath);
                CleanupTempFiles(fileName);
            }
        }

        public bool Delete(string fileName)
        {
            return DeleteFileBestEffort(GetPath(fileName));
        }

        public void EnsureDirectory()
        {
            Directory.CreateDirectory(_rootDirectory);
        }

        public bool TryRestoreBackup(string fileName)
        {
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
            quarantinePath = string.Empty;
            var canonicalPath = GetPath(fileName);
            if (!File.Exists(canonicalPath))
            {
                return false;
            }

            EnsureDirectory();
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfffffff", CultureInfo.InvariantCulture);
            quarantinePath = Path.Combine(_rootDirectory, $"{fileName}.corrupt.{timestamp}");
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

        private string CreateTempPath(string fileName)
        {
            var prefix = Path.GetFileNameWithoutExtension(fileName);
            return Path.Combine(_rootDirectory, $"{prefix}.{Guid.NewGuid():N}.tmp");
        }

        private static string CreateTempSearchPattern(string fileName)
        {
            return $"{Path.GetFileNameWithoutExtension(fileName)}.*.tmp";
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
