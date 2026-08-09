using System;
using System.IO;
using Game.Product.Achievements;
using UnityEngine;

namespace Game.Product.Achievements.Infrastructure
{
    public sealed class FileProductAchievementRepository : IAchievementDocumentRepository
    {
        public const string AchievementFileName = "achievements.json";

        private readonly IAchievementTextStore _textStore;

        public FileProductAchievementRepository(IAchievementTextStore textStore)
        {
            _textStore = textStore ?? throw new ArgumentNullException(nameof(textStore));
        }

        public AchievementDocumentLoadResult Load()
        {
            try
            {
                _textStore.CleanupTempFiles(AchievementFileName);
                if (!_textStore.Exists(AchievementFileName))
                {
                    return LoadWithoutPrimary();
                }

                var primaryResult = TryRead(
                    _textStore.ReadAllText(AchievementFileName),
                    out var primaryDocument);
                switch (primaryResult)
                {
                    case DocumentReadResult.Valid:
                        return new AchievementDocumentLoadResult(
                            AchievementDocumentLoadStatus.Loaded,
                            primaryDocument,
                            "achievements.json loaded.");

                    case DocumentReadResult.SchemaInvalid:
                        return new AchievementDocumentLoadResult(
                            AchievementDocumentLoadStatus.SchemaInvalid,
                            null,
                            "achievements.json schema is invalid.");

                    case DocumentReadResult.UnsupportedVersion:
                        return new AchievementDocumentLoadResult(
                            AchievementDocumentLoadStatus.UnsupportedVersion,
                            null,
                            "achievements.json uses a newer unsupported schema version.");
                }

                if (TryReadBackup(out var backupDocument) == DocumentReadResult.Valid)
                {
                    if (!_textStore.TryRestoreBackup(AchievementFileName))
                    {
                        return new AchievementDocumentLoadResult(
                            AchievementDocumentLoadStatus.IoFailed,
                            null,
                            "achievements.json.bak was valid but could not be restored.");
                    }

                    return new AchievementDocumentLoadResult(
                        AchievementDocumentLoadStatus.BackupRecovered,
                        backupDocument,
                        "achievements.json.bak recovered achievements.json.");
                }

                if (_textStore.TryQuarantine(AchievementFileName, out _))
                {
                    return new AchievementDocumentLoadResult(
                        AchievementDocumentLoadStatus.CorruptQuarantined,
                        null,
                        "achievements.json was corrupt and quarantined.");
                }

                return new AchievementDocumentLoadResult(
                    AchievementDocumentLoadStatus.CorruptNoFallback,
                    null,
                    "achievements.json was corrupt and no usable fallback was available.");
            }
            catch (UnauthorizedAccessException exception)
            {
                return new AchievementDocumentLoadResult(
                    AchievementDocumentLoadStatus.Unauthorized,
                    null,
                    exception.Message);
            }
            catch (IOException exception)
            {
                return new AchievementDocumentLoadResult(
                    AchievementDocumentLoadStatus.IoFailed,
                    null,
                    exception.Message);
            }
        }

        private AchievementDocumentLoadResult LoadWithoutPrimary()
        {
            var backupReadResult = TryReadBackup(out var backupDocument);
            switch (backupReadResult)
            {
                case DocumentReadResult.Missing:
                    return new AchievementDocumentLoadResult(
                        AchievementDocumentLoadStatus.Missing,
                        ProductAchievementDocument.CreateEmpty(),
                        "achievements.json is missing; an empty product-global document is active.");

                case DocumentReadResult.Valid:
                    if (!_textStore.TryRestoreBackup(AchievementFileName))
                    {
                        return new AchievementDocumentLoadResult(
                            AchievementDocumentLoadStatus.IoFailed,
                            null,
                            "achievements.json.bak was valid but could not be restored.");
                    }

                    return new AchievementDocumentLoadResult(
                        AchievementDocumentLoadStatus.BackupRecovered,
                        backupDocument,
                        "achievements.json.bak recovered the missing achievements.json.");

                case DocumentReadResult.SchemaInvalid:
                    return new AchievementDocumentLoadResult(
                        AchievementDocumentLoadStatus.SchemaInvalid,
                        null,
                        "achievements.json is missing and achievements.json.bak schema is invalid.");

                case DocumentReadResult.UnsupportedVersion:
                    return new AchievementDocumentLoadResult(
                        AchievementDocumentLoadStatus.UnsupportedVersion,
                        null,
                        "achievements.json is missing and achievements.json.bak uses an unsupported schema version.");

                default:
                    return new AchievementDocumentLoadResult(
                        AchievementDocumentLoadStatus.CorruptNoFallback,
                        null,
                        "achievements.json is missing and achievements.json.bak is corrupt.");
            }
        }

        public AchievementDocumentSaveResult Save(ProductAchievementDocument document)
        {
            var validation = ProductAchievementDocumentNormalizer.TryNormalize(
                document,
                out var normalized);
            if (validation == AchievementDocumentValidationStatus.SchemaInvalid)
            {
                return new AchievementDocumentSaveResult(
                    AchievementDocumentSaveStatus.SchemaInvalid,
                    "Product achievement document schema is invalid.");
            }

            if (validation == AchievementDocumentValidationStatus.UnsupportedVersion)
            {
                return new AchievementDocumentSaveResult(
                    AchievementDocumentSaveStatus.UnsupportedVersion,
                    "Product achievement document schema version is unsupported.");
            }

            try
            {
                var json = JsonUtility.ToJson(normalized);
                _textStore.WriteAllTextAtomic(AchievementFileName, json);
                return AchievementDocumentSaveResult.Saved();
            }
            catch (UnauthorizedAccessException exception)
            {
                return new AchievementDocumentSaveResult(
                    AchievementDocumentSaveStatus.Unauthorized,
                    exception.Message);
            }
            catch (IOException exception)
            {
                return new AchievementDocumentSaveResult(
                    AchievementDocumentSaveStatus.IoFailed,
                    exception.Message);
            }
            catch (Exception exception)
            {
                return new AchievementDocumentSaveResult(
                    AchievementDocumentSaveStatus.Failed,
                    exception.Message);
            }
        }

        private DocumentReadResult TryReadBackup(out ProductAchievementDocument document)
        {
            document = null;
            const string backupFileName = AchievementFileName + ".bak";
            if (!_textStore.Exists(backupFileName))
            {
                return DocumentReadResult.Missing;
            }

            return TryRead(_textStore.ReadAllText(backupFileName), out document);
        }

        private static DocumentReadResult TryRead(
            string rawJson,
            out ProductAchievementDocument document)
        {
            document = null;
            if (!JsonSyntaxValidator.IsValid(rawJson))
            {
                return DocumentReadResult.Corrupt;
            }

            SchemaVersionProbe schemaVersionProbe;
            try
            {
                schemaVersionProbe = JsonUtility.FromJson<SchemaVersionProbe>(rawJson);
            }
            catch (ArgumentException)
            {
                return DocumentReadResult.Corrupt;
            }

            if (schemaVersionProbe == null || schemaVersionProbe.SchemaVersion <= 0)
            {
                return DocumentReadResult.SchemaInvalid;
            }

            ProductAchievementDocument deserialized;
            try
            {
                deserialized = JsonUtility.FromJson<ProductAchievementDocument>(rawJson);
            }
            catch (ArgumentException)
            {
                return DocumentReadResult.Corrupt;
            }

            var validation = ProductAchievementDocumentNormalizer.TryNormalize(
                deserialized,
                out document);
            switch (validation)
            {
                case AchievementDocumentValidationStatus.Valid:
                    return DocumentReadResult.Valid;
                case AchievementDocumentValidationStatus.UnsupportedVersion:
                    return DocumentReadResult.UnsupportedVersion;
                default:
                    document = null;
                    return DocumentReadResult.SchemaInvalid;
            }
        }

        private enum DocumentReadResult
        {
            Valid,
            Corrupt,
            SchemaInvalid,
            UnsupportedVersion,
            Missing,
        }

        [Serializable]
        private sealed class SchemaVersionProbe
        {
            public int SchemaVersion = int.MinValue;
        }
    }
}
