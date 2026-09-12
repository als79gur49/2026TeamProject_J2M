// Read-only legacy schema 1 seam. No dependency on Unity or reset Load/Save.
using System;
using System.IO;
using System.Linq;

namespace Game.Exhibition.RestartExperiment
{
    public sealed class ObservationV3Journal
    {
        public int SchemaVersion;
        public string OperationId, State;
        public uint AppId;
        public ulong SteamId;
        public string MappingVersion;
    }
    public sealed class ObservationV3JournalSnapshot
    {
        private readonly byte[] bytes;
        private readonly string path, hash;
        internal ObservationV3JournalSnapshot(string path, byte[] bytes)
        { this.path = path; this.bytes = (byte[])bytes.Clone(); hash = ObservationV3RuntimeWire.Hash(this.bytes); }
        public ObservationRef Reference { get { return new ObservationRef { Path = path, Sha256 = hash }; } }
        public ObservationV3Journal Value { get { return ObservationV3RuntimeWire.Parse<ObservationV3Journal>(bytes); } }
    }
    public static class ObservationV3JournalReader
    {
        public const string MappingVersion = "level-clear-v1";
        public static ObservationV3JournalSnapshot Read(string path, uint appId, ulong? steamId = null)
        {
            path = ObservationV3RuntimeWire.Canonical(path, null);
            byte[] bytes;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length <= 0 || stream.Length > 1048576) throw new IOException("InvalidJournalSize");
                bytes = new byte[(int)stream.Length]; int offset = 0;
                while (offset < bytes.Length) { int read = stream.Read(bytes, offset, bytes.Length - offset); if (read == 0) throw new IOException("JournalReadIncomplete"); offset += read; }
                if (stream.ReadByte() != -1) throw new IOException("JournalChangedDuringRead");
            }
            var result = new ObservationV3JournalSnapshot(path, bytes); var value = result.Value;
            ObservationV3RuntimeWire.Require(value.SchemaVersion == 1 && value.State == "Ready" && value.MappingVersion == MappingVersion &&
                value.AppId == appId && value.SteamId != 0 && appId != 0 && (!steamId.HasValue || value.SteamId == steamId.Value), "ReadyJournalMismatch");
            ObservationV3Wire.Id(value.OperationId); return result;
        }
        public static void Validate(ObservationV3ReadySnapshot ready, ObservationV3ParticipantSnapshot participants, uint appId, ulong steamId)
        {
            var current = Read(ready.Journal.Path, appId, steamId); var value = current.Value;
            ObservationV3RuntimeWire.Require(ObservationV3RuntimeWire.SameRef(current.Reference, ready.Journal) && value.State == ready.State &&
                value.OperationId == ready.OperationId && value.MappingVersion == ready.MappingVersion && value.AppId == ready.AppId && value.SteamId == ready.SteamId,
                "ReadyJournalContentChanged");
            ObservationV3RuntimeWire.Require(participants.Files.Count(f => string.Equals(f.Path, current.Reference.Path, StringComparison.OrdinalIgnoreCase)) == 1 &&
                participants.Files.Any(f => ObservationV3RuntimeWire.SameRef(f, current.Reference)), "ParticipantJournalEntryMismatch");
        }
        public static void RequireUnchanged(ObservationV3JournalSnapshot snapshot, uint appId, ulong steamId)
        { ObservationV3RuntimeWire.Require(ObservationV3RuntimeWire.SameRef(snapshot.Reference, Read(snapshot.Reference.Path, appId, steamId).Reference), "ReadyJournalChangedDuringPreparation"); }
    }
}
