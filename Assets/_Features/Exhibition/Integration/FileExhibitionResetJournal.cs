using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Game.Exhibition.Integration
{
    public sealed class FileExhibitionResetJournal : IExhibitionResetJournal
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
            Load();
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            var temporary = _path + ".tmp";
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
        }
    }
}
