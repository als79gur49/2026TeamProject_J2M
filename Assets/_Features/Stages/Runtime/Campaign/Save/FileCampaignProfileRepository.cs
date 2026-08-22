using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Game.Feature.Stages
{
    public sealed class FileCampaignProfileRepository : ICampaignProfileRepository
    {
        public const string ProfileFileName = "profile.json";

        private readonly IAtomicTextFileStore _textFileStore;

        public FileCampaignProfileRepository(IAtomicTextFileStore textFileStore)
        {
            _textFileStore = textFileStore ?? throw new ArgumentNullException(nameof(textFileStore));
        }

        public CampaignProfileLoadResult Load()
        {
            try
            {
                _textFileStore.CleanupTempFiles(ProfileFileName);
                if (!_textFileStore.Exists(ProfileFileName))
                {
                    return LoadWithoutCanonical();
                }

                var rawProfile = _textFileStore.ReadAllText(ProfileFileName);
                var canonicalResult = TryDeserializeAndValidate(rawProfile, out var document);
                if (canonicalResult == ProfileReadResult.Valid)
                {
                    return new CampaignProfileLoadResult(
                        CampaignProfileLoadStatus.Loaded,
                        document,
                        "profile.json loaded.");
                }

                if (canonicalResult == ProfileReadResult.UnsupportedVersion)
                {
                    return new CampaignProfileLoadResult(
                        CampaignProfileLoadStatus.UnsupportedVersion,
                        null,
                        "profile.json was created with an unsupported schema version.");
                }

                if (TryLoadBackup(out var backupDocument))
                {
                    if (!_textFileStore.TryRestoreBackup(ProfileFileName))
                    {
                        return new CampaignProfileLoadResult(
                            CampaignProfileLoadStatus.IoFailed,
                            null,
                            "profile.json.bak could not restore the invalid canonical profile.");
                    }

                    return new CampaignProfileLoadResult(
                        CampaignProfileLoadStatus.BackupRecovered,
                        backupDocument,
                        "profile.json.bak recovered profile.json.");
                }

                if (canonicalResult == ProfileReadResult.InvalidDocument)
                {
                    return new CampaignProfileLoadResult(
                        CampaignProfileLoadStatus.InvalidDocument,
                        null,
                        "profile.json does not satisfy the current profile contract.");
                }

                return new CampaignProfileLoadResult(
                    CampaignProfileLoadStatus.CorruptNoFallback,
                    null,
                    "profile.json was corrupt and no fallback was available.");
            }
            catch (UnauthorizedAccessException exception)
            {
                return new CampaignProfileLoadResult(
                    CampaignProfileLoadStatus.Unauthorized,
                    null,
                    exception.Message);
            }
            catch (IOException exception)
            {
                return new CampaignProfileLoadResult(
                    CampaignProfileLoadStatus.IoFailed,
                    null,
                    exception.Message);
            }
        }

        public void Save(CampaignProfileDocument document)
        {
            Save(document, destructive: false);
        }

        public void SaveDestructive(CampaignProfileDocument document)
        {
            Save(document, destructive: true);
        }

        private void Save(CampaignProfileDocument document, bool destructive)
        {
            if (Validate(document) != CampaignProfileLoadStatus.Loaded)
            {
                throw new ArgumentException("Campaign profile document is invalid.", nameof(document));
            }

            var json = JsonUtility.ToJson(document);
            if (!destructive)
            {
                _textFileStore.WriteAllTextAtomic(ProfileFileName, json);
                return;
            }

            const string backupFileName = ProfileFileName + ".bak";
            _textFileStore.RecoverInterruptedWrite(backupFileName);
            _textFileStore.RecoverInterruptedWrite(ProfileFileName);
            var hadPreviousCanonical = _textFileStore.Exists(ProfileFileName);
            var previousCanonical = hadPreviousCanonical
                ? _textFileStore.ReadAllText(ProfileFileName)
                : null;
            var hadPreviousBackup = _textFileStore.Exists(backupFileName);
            var previousBackup = hadPreviousBackup
                ? _textFileStore.ReadAllText(backupFileName)
                : null;
            _textFileStore.WriteAllTextAtomicWithoutBackup(backupFileName, json);
            try
            {
                _textFileStore.WriteAllTextAtomicWithoutBackup(ProfileFileName, json);
            }
            catch (Exception saveException)
            {
                var failures = new List<Exception> { saveException };
                var canonicalCompensated = false;
                try
                {
                    RestoreFileState(
                        ProfileFileName,
                        hadPreviousCanonical,
                        previousCanonical);
                    canonicalCompensated = true;
                }
                catch (Exception canonicalCompensationException)
                {
                    failures.Add(canonicalCompensationException);
                }

                if (canonicalCompensated || hadPreviousBackup)
                {
                    try
                    {
                        RestoreFileState(
                            backupFileName,
                            hadPreviousBackup,
                            previousBackup);
                    }
                    catch (Exception backupCompensationException)
                    {
                        failures.Add(backupCompensationException);
                    }
                }

                if (failures.Count > 1)
                {
                    throw new AggregateException(
                        "Destructive profile save compensation did not fully restore the previous file state.",
                        failures);
                }

                throw;
            }
        }

        private void RestoreFileState(
            string fileName,
            bool previouslyExisted,
            string previousContents)
        {
            if (previouslyExisted)
            {
                _textFileStore.WriteAllTextAtomicWithoutBackup(fileName, previousContents);
                return;
            }

            if (!_textFileStore.Delete(fileName) && _textFileStore.Exists(fileName))
            {
                throw new IOException($"'{fileName}' could not be removed during save compensation.");
            }
        }

        private CampaignProfileLoadResult LoadWithoutCanonical()
        {
            var backupResult = ReadBackup(out var backupDocument);
            switch (backupResult)
            {
                case ProfileReadResult.Missing:
                    return new CampaignProfileLoadResult(
                        CampaignProfileLoadStatus.Missing,
                        null,
                        "profile.json and profile.json.bak are missing.");
                case ProfileReadResult.Valid:
                    if (!_textFileStore.TryRestoreBackup(ProfileFileName))
                    {
                        return new CampaignProfileLoadResult(
                            CampaignProfileLoadStatus.IoFailed,
                            null,
                            "profile.json.bak could not restore the missing canonical profile.");
                    }

                    return new CampaignProfileLoadResult(
                        CampaignProfileLoadStatus.BackupRecovered,
                        backupDocument,
                        "profile.json.bak recovered the missing profile.json.");
                case ProfileReadResult.UnsupportedVersion:
                    return new CampaignProfileLoadResult(
                        CampaignProfileLoadStatus.UnsupportedVersion,
                        null,
                        "profile.json.bak was created with an unsupported schema version.");
                case ProfileReadResult.InvalidDocument:
                    return new CampaignProfileLoadResult(
                        CampaignProfileLoadStatus.InvalidDocument,
                        null,
                        "profile.json.bak does not satisfy the current profile contract.");
                default:
                    return new CampaignProfileLoadResult(
                        CampaignProfileLoadStatus.CorruptNoFallback,
                        null,
                        "profile.json is missing and profile.json.bak is corrupt.");
            }
        }

        private ProfileReadResult ReadBackup(out CampaignProfileDocument document)
        {
            document = null;
            const string backupFileName = ProfileFileName + ".bak";
            if (!_textFileStore.Exists(backupFileName))
            {
                return ProfileReadResult.Missing;
            }

            var rawBackup = _textFileStore.ReadAllText(backupFileName);
            return TryDeserializeAndValidate(rawBackup, out document);
        }

        private bool TryLoadBackup(out CampaignProfileDocument document)
        {
            return ReadBackup(out document) == ProfileReadResult.Valid;
        }

        private static ProfileReadResult TryDeserializeAndValidate(
            string rawJson,
            out CampaignProfileDocument document)
        {
            document = null;
            if (!JsonSyntaxValidator.IsValid(rawJson))
            {
                return ProfileReadResult.Corrupt;
            }

            try
            {
                document = JsonUtility.FromJson<CampaignProfileDocument>(rawJson);
            }
            catch (ArgumentException)
            {
                return ProfileReadResult.Corrupt;
            }

            switch (Validate(document))
            {
                case CampaignProfileLoadStatus.Loaded:
                    return ProfileReadResult.Valid;
                case CampaignProfileLoadStatus.UnsupportedVersion:
                    return ProfileReadResult.UnsupportedVersion;
                default:
                    return ProfileReadResult.InvalidDocument;
            }
        }

        private static CampaignProfileLoadStatus Validate(CampaignProfileDocument document)
        {
            var validation = CampaignProfileDocumentValidator.Validate(document);
            if (validation == CampaignProfileDocumentValidationResult.UnsupportedVersion)
            {
                return CampaignProfileLoadStatus.UnsupportedVersion;
            }

            if (validation != CampaignProfileDocumentValidationResult.Valid)
            {
                return CampaignProfileLoadStatus.InvalidDocument;
            }

            document.Slots ??= Array.Empty<CampaignSlotDocument>();
            for (var i = 0; i < document.Slots.Length; i++)
            {
                Normalize(document.Slots[i]);
            }

            return CampaignProfileLoadStatus.Loaded;
        }

        private static void Normalize(CampaignSlotDocument slot)
        {
            if (slot == null)
            {
                return;
            }

            if (!slot.HasNormalCampaignCompletionReceipt)
            {
                slot.NormalCampaignCompletionReceipt = null;
            }

            slot.StageClearProfileSnapshot ??= new CampaignStageClearProfileDocument();
            slot.NormalStagePerformanceRecords ??=
                Array.Empty<NormalStagePerformanceRecordDocument>();
            slot.StageClearProfileSnapshot.Records ??= Array.Empty<PlayerStageClearRecordDocument>();
            slot.StageClearProfileSnapshot.ProcessedStageRunIds ??= Array.Empty<string>();
            slot.StageClearProfileSnapshot.ProcessedClearAttemptIds ??= Array.Empty<string>();
            for (var i = 0; i < slot.StageClearProfileSnapshot.Records.Length; i++)
            {
                var record = slot.StageClearProfileSnapshot.Records[i];
                if (record != null)
                {
                    record.ProcessedStageRunIds ??= Array.Empty<string>();
                }
            }
        }

        private enum ProfileReadResult
        {
            Missing,
            Valid,
            Corrupt,
            UnsupportedVersion,
            InvalidDocument,
        }

        private static class JsonSyntaxValidator
        {
            public static bool IsValid(string json)
            {
                if (string.IsNullOrWhiteSpace(json))
                {
                    return false;
                }

                var index = 0;
                if (!TrySkipValue(json, ref index))
                {
                    return false;
                }

                SkipWhitespace(json, ref index);
                return index == json.Length;
            }

            private static bool TrySkipValue(string json, ref int index)
            {
                SkipWhitespace(json, ref index);
                if (index >= json.Length)
                {
                    return false;
                }

                var current = json[index];
                if (current == '"')
                {
                    return TryReadString(json, ref index);
                }

                if (current == '{')
                {
                    return TrySkipObject(json, ref index);
                }

                if (current == '[')
                {
                    return TrySkipArray(json, ref index);
                }

                return TrySkipPrimitive(json, ref index);
            }

            private static bool TrySkipObject(string json, ref int index)
            {
                if (!TryConsume(json, ref index, '{'))
                {
                    return false;
                }

                SkipWhitespace(json, ref index);
                if (TryConsume(json, ref index, '}'))
                {
                    return true;
                }

                while (index < json.Length)
                {
                    if (!TryReadString(json, ref index))
                    {
                        return false;
                    }

                    SkipWhitespace(json, ref index);
                    if (!TryConsume(json, ref index, ':') || !TrySkipValue(json, ref index))
                    {
                        return false;
                    }

                    SkipWhitespace(json, ref index);
                    if (TryConsume(json, ref index, '}'))
                    {
                        return true;
                    }

                    if (!TryConsume(json, ref index, ','))
                    {
                        return false;
                    }

                    SkipWhitespace(json, ref index);
                }

                return false;
            }

            private static bool TrySkipArray(string json, ref int index)
            {
                if (!TryConsume(json, ref index, '['))
                {
                    return false;
                }

                SkipWhitespace(json, ref index);
                if (TryConsume(json, ref index, ']'))
                {
                    return true;
                }

                while (index < json.Length)
                {
                    if (!TrySkipValue(json, ref index))
                    {
                        return false;
                    }

                    SkipWhitespace(json, ref index);
                    if (TryConsume(json, ref index, ']'))
                    {
                        return true;
                    }

                    if (!TryConsume(json, ref index, ','))
                    {
                        return false;
                    }

                    SkipWhitespace(json, ref index);
                }

                return false;
            }

            private static bool TrySkipPrimitive(string json, ref int index)
            {
                var start = index;
                while (index < json.Length)
                {
                    var current = json[index];
                    if (current == ',' || current == '}' || current == ']' || char.IsWhiteSpace(current))
                    {
                        break;
                    }

                    index++;
                }

                if (index == start)
                {
                    return false;
                }

                var value = json.Substring(start, index - start);
                if (string.Equals(value, "true", StringComparison.Ordinal) ||
                    string.Equals(value, "false", StringComparison.Ordinal) ||
                    string.Equals(value, "null", StringComparison.Ordinal))
                {
                    return true;
                }

                return IsJsonNumber(value);
            }

            private static bool IsJsonNumber(string value)
            {
                var index = 0;
                if (index < value.Length && value[index] == '-')
                {
                    index++;
                }

                if (index >= value.Length)
                {
                    return false;
                }

                if (value[index] == '0')
                {
                    index++;
                }
                else if (value[index] >= '1' && value[index] <= '9')
                {
                    do
                    {
                        index++;
                    }
                    while (index < value.Length && char.IsDigit(value[index]));
                }
                else
                {
                    return false;
                }

                if (index < value.Length && value[index] == '.')
                {
                    index++;
                    var fractionStart = index;
                    while (index < value.Length && char.IsDigit(value[index]))
                    {
                        index++;
                    }

                    if (index == fractionStart)
                    {
                        return false;
                    }
                }

                if (index < value.Length && (value[index] == 'e' || value[index] == 'E'))
                {
                    index++;
                    if (index < value.Length && (value[index] == '+' || value[index] == '-'))
                    {
                        index++;
                    }

                    var exponentStart = index;
                    while (index < value.Length && char.IsDigit(value[index]))
                    {
                        index++;
                    }

                    if (index == exponentStart)
                    {
                        return false;
                    }
                }

                return index == value.Length;
            }

            private static bool TryReadString(string json, ref int index)
            {
                if (!TryConsume(json, ref index, '"'))
                {
                    return false;
                }

                while (index < json.Length)
                {
                    var current = json[index];
                    if (current == '"')
                    {
                        index++;
                        return true;
                    }

                    if (current == '\\')
                    {
                        index++;
                        if (index >= json.Length)
                        {
                            return false;
                        }

                        var escaped = json[index];
                        if (escaped == 'u')
                        {
                            if (index + 4 >= json.Length)
                            {
                                return false;
                            }

                            for (var i = 1; i <= 4; i++)
                            {
                                if (!Uri.IsHexDigit(json[index + i]))
                                {
                                    return false;
                                }
                            }

                            index += 5;
                            continue;
                        }

                        if (escaped == '"' ||
                            escaped == '\\' ||
                            escaped == '/' ||
                            escaped == 'b' ||
                            escaped == 'f' ||
                            escaped == 'n' ||
                            escaped == 'r' ||
                            escaped == 't')
                        {
                            index++;
                            continue;
                        }

                        return false;
                    }

                    if (char.IsControl(current))
                    {
                        return false;
                    }

                    index++;
                }

                return false;
            }

            private static bool TryConsume(string value, ref int index, char expected)
            {
                if (index >= value.Length || value[index] != expected)
                {
                    return false;
                }

                index++;
                return true;
            }

            private static void SkipWhitespace(string value, ref int index)
            {
                while (index < value.Length && char.IsWhiteSpace(value[index]))
                {
                    index++;
                }
            }
        }
    }
}
