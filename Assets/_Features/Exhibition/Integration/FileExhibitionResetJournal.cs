using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Game.Exhibition.Integration
{
    public sealed class FileExhibitionResetJournal : IExhibitionResetJournal, IExhibitionResetJournalMaintenance
    {
        private readonly string _path;

        public FileExhibitionResetJournal(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A journal path is required.", nameof(path));
            _path = Path.GetFullPath(path);
        }

        public ResetRecord Load()
        {
            if (!File.Exists(_path))
            {
                if (HasCompanion()) throw new IOException("The reset record is missing but an interrupted write remains.");
                return null;
            }
            ResetRecord record;
            try { record = JsonUtility.FromJson<ResetRecord>(File.ReadAllText(_path)); }
            catch (ArgumentException exception) { throw new IOException("The reset record is invalid.", exception); }
            Validate(record);
            return record;
        }

        public void Save(ResetRecord record)
        {
            Validate(record);
            // Read the canonical record before changing anything; never recover an old Pending copy.
            var existing = Load();
            if (record.MappingVersion != ExhibitionResetCoordinator.MappingVersion ||
                existing?.MappingVersion == ExhibitionResetCoordinator.PreviousMappingVersion)
                throw new IOException(ExhibitionResetCoordinator.IncompatibleMappingMessage);
            WriteCanonical(record);
        }

        private void WriteCanonical(ResetRecord record, bool legacyReplacement = false)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            var temporary = legacyReplacement
                ? Path.Combine(ArchiveDirectory, record.OperationId + ".replacement.tmp")
                : _path + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(JsonUtility.ToJson(record));
                writer.Flush();
                stream.Flush(true);
            }
            // Windows File.Replace is atomic. Do not fall back to delete-then-move.
            if (File.Exists(_path)) File.Replace(temporary, _path, null);
            else File.Move(temporary, _path);
            // A stale companion must never participate in a later canonical read.
            foreach (var suffix in new[] { ".bak", ".rollback", ".tmp" })
                File.Delete(_path + suffix);
        }

        private string ArchiveDirectory => _path + ".archive";

        public bool HasArchivedRecord
        {
            get
            {
                if (!Directory.Exists(ArchiveDirectory)) return false;
                bool found = false;
                foreach (var path in Directory.GetFiles(ArchiveDirectory, "*.json"))
                {
                    var record = ReadArchive(path);
                    if (record.MappingVersion != ExhibitionResetCoordinator.PreviousMappingVersion ||
                        Path.GetFileNameWithoutExtension(path) != record.OperationId)
                        throw new IOException("Unexpected archived reset record.");
                    found = true;
                }
                return found;
            }
        }

        public void ArchiveLegacyReady(ResetRecord expected)
        {
            RequireLegacy(expected, ResetRecord.Ready);
            CheckCanonicalAndCompanions(expected);
            Archive(expected);
            // Remove verified stale companions first: interruption leaves the canonical Ready recoverable.
            foreach (var suffix in new[] { ".bak", ".rollback", ".tmp" }) File.Delete(_path + suffix);
            File.Delete(_path);
        }

        public void ReplaceLegacyPending(ResetRecord expected, ResetRecord replacement)
        {
            RequireLegacy(expected, ResetRecord.Pending);
            Validate(replacement);
            if (replacement.MappingVersion != ExhibitionResetCoordinator.MappingVersion ||
                replacement.State != ResetRecord.Pending || replacement.OperationId == expected.OperationId ||
                replacement.AppId != expected.AppId || replacement.SteamId != expected.SteamId)
                throw new IOException("Invalid replacement reset request.");
            CheckCanonicalAndCompanions(expected);
            Archive(expected);
            // Atomic replace keeps the old Pending canonical until the new request is committed.
            WriteCanonical(replacement, true);
        }

        private void CheckCanonicalAndCompanions(ResetRecord expected)
        {
            if (!SameRecord(Load(), expected)) throw new IOException("The reset record changed during maintenance.");
            foreach (var suffix in new[] { ".bak", ".rollback", ".tmp" })
            {
                var path = _path + suffix;
                if (!File.Exists(path)) continue;
                var companion = ReadArchive(path);
                if (companion.OperationId != expected.OperationId || companion.AppId != expected.AppId ||
                    companion.SteamId != expected.SteamId || companion.MappingVersion != expected.MappingVersion)
                    throw new IOException("An unexpected reset companion requires investigation.");
            }
        }

        private void Archive(ResetRecord expected)
        {
            // Snapshot the canonical bytes only after validating the expected operation; preserve
            // unknown JSON fields and formatting as evidence rather than serializing a reduced model.
            var original = File.ReadAllBytes(_path);
            ResetRecord captured;
            try { captured = JsonUtility.FromJson<ResetRecord>(Encoding.UTF8.GetString(original).TrimStart('\uFEFF')); }
            catch (ArgumentException exception) { throw new IOException("The canonical reset changed during archival.", exception); }
            Validate(captured);
            if (!SameRecord(captured, expected)) throw new IOException("The reset record changed during archival.");
            Directory.CreateDirectory(ArchiveDirectory);
            var target = Path.Combine(ArchiveDirectory, expected.OperationId + ".json");
            if (File.Exists(target))
            {
                var archived = File.ReadAllBytes(target);
                if (!SameBytes(archived, original)) throw new IOException("The reset archive conflicts with the canonical record.");
                return;
            }
            var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(original, 0, original.Length); stream.Flush(true);
                }
                File.Move(temporary, target);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static bool SameBytes(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            for (int index = 0; index < left.Length; index++) if (left[index] != right[index]) return false;
            return true;
        }

        private static ResetRecord ReadArchive(string path)
        {
            ResetRecord record;
            try { record = JsonUtility.FromJson<ResetRecord>(File.ReadAllText(path)); }
            catch (ArgumentException exception) { throw new IOException("The archived reset record is invalid.", exception); }
            Validate(record);
            return record;
        }

        private static void RequireLegacy(ResetRecord record, string state)
        {
            Validate(record);
            if (record.MappingVersion != ExhibitionResetCoordinator.PreviousMappingVersion || record.State != state)
                throw new IOException("Only a known previous reset record can be archived.");
        }

        private static bool SameRecord(ResetRecord actual, ResetRecord expected) => actual != null &&
            actual.SchemaVersion == expected.SchemaVersion && actual.OperationId == expected.OperationId &&
            actual.State == expected.State && actual.MappingVersion == expected.MappingVersion &&
            actual.AppId == expected.AppId && actual.SteamId == expected.SteamId;

        private bool HasCompanion()
        {
            foreach (var suffix in new[] { ".bak", ".rollback", ".tmp" })
                if (File.Exists(_path + suffix)) return true;
            return false;
        }

        private static void Validate(ResetRecord record)
        {
            if (record == null || record.SchemaVersion != 1 ||
                !Guid.TryParseExact(record.OperationId, "N", out _) ||
                (record.State != ResetRecord.Pending && record.State != ResetRecord.Ready) ||
                record.AppId == 0 || record.SteamId == 0 || string.IsNullOrWhiteSpace(record.MappingVersion))
                throw new IOException("The exhibition reset record is corrupt or unsupported.");
            if (record.MappingVersion != ExhibitionResetCoordinator.MappingVersion &&
                record.MappingVersion != ExhibitionResetCoordinator.PreviousMappingVersion)
                throw new IOException(ExhibitionResetCoordinator.IncompatibleMappingMessage);
        }
    }
}
