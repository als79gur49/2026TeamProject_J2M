using System;
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
                    return new CampaignProfileLoadResult(
                        CampaignProfileLoadStatus.Missing,
                        null,
                        "profile.json is missing.");
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

                if (canonicalResult == ProfileReadResult.SchemaInvalid)
                {
                    return new CampaignProfileLoadResult(
                        CampaignProfileLoadStatus.SchemaInvalid,
                        null,
                        "profile.json schema is invalid.");
                }

                if (TryLoadBackup(out var backupDocument))
                {
                    _textFileStore.TryRestoreBackup(ProfileFileName);
                    return new CampaignProfileLoadResult(
                        CampaignProfileLoadStatus.BackupRecovered,
                        backupDocument,
                        "profile.json.bak recovered profile.json.");
                }

                if (_textFileStore.TryQuarantine(ProfileFileName, out _))
                {
                    return new CampaignProfileLoadResult(
                        CampaignProfileLoadStatus.CorruptQuarantined,
                        null,
                        "profile.json was corrupt and quarantined.");
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
            if (Validate(document) != CampaignProfileLoadStatus.Loaded)
            {
                throw new ArgumentException("Campaign profile document is invalid.", nameof(document));
            }

            var json = JsonUtility.ToJson(document);
            _textFileStore.WriteAllTextAtomic(ProfileFileName, json);
        }

        private bool TryLoadBackup(out CampaignProfileDocument document)
        {
            document = null;
            const string backupFileName = ProfileFileName + ".bak";
            if (!_textFileStore.Exists(backupFileName))
            {
                return false;
            }

            var rawBackup = _textFileStore.ReadAllText(backupFileName);
            return TryDeserializeAndValidate(rawBackup, out document) == ProfileReadResult.Valid;
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

            return Validate(document) == CampaignProfileLoadStatus.Loaded
                ? ProfileReadResult.Valid
                : ProfileReadResult.SchemaInvalid;
        }

        private static CampaignProfileLoadStatus Validate(CampaignProfileDocument document)
        {
            if (document == null)
            {
                return CampaignProfileLoadStatus.SchemaInvalid;
            }

            if (document.SchemaVersion <= 0)
            {
                return CampaignProfileLoadStatus.SchemaInvalid;
            }

            if (string.IsNullOrWhiteSpace(document.ProfileId))
            {
                return CampaignProfileLoadStatus.SchemaInvalid;
            }

            document.Slots ??= Array.Empty<CampaignSlotDocument>();
            document.LegacyImport ??= new CampaignLegacyImportDocument();
            return CampaignProfileLoadStatus.Loaded;
        }

        private enum ProfileReadResult
        {
            Valid,
            Corrupt,
            SchemaInvalid,
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
