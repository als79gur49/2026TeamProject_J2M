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
        internal CampaignHudReadStore HudReadStore { get; }

        public FileCampaignProfileRepository(IAtomicTextFileStore textFileStore)
        {
            if (textFileStore == null) throw new ArgumentNullException(nameof(textFileStore));
            HudReadStore = textFileStore is CampaignHudObservedFileStore observed ? observed.Reads :
                CampaignHudReadRegistry.Acquire(textFileStore is AtomicTextFileStore file
                    ? CampaignHudReadRegistry.FileKey(file.RootDirectory)
                    : CampaignHudReadRegistry.MemoryKey(textFileStore));
            _textFileStore = textFileStore is CampaignHudObservedFileStore ? textFileStore :
                new CampaignHudObservedFileStore(textFileStore, HudReadStore);
        }

        public CampaignProfileLoadResult Load()
        {
            using var operation = HudReadStore.BeginOperation();
            HudReadStore.InvalidateForValidation();
            var result = LoadCore();
            HudReadStore.ObserveProfile(result);
            return result;
        }

        private CampaignProfileLoadResult LoadCore()
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
            using var operation = HudReadStore.BeginOperation();
            HudReadStore.InvalidateForValidation();
            SaveCore(document, destructive);
            HudReadStore.ObserveProfile(new CampaignProfileLoadResult(
                CampaignProfileLoadStatus.Loaded, document, "Campaign profile committed."));
        }

        private void SaveCore(CampaignProfileDocument document, bool destructive)
        {
            if (ValidateAndMaterialize(document) != CampaignProfileLoadStatus.Loaded)
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
            if (!CampaignJsonSyntaxValidator.IsValid(rawJson))
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

            switch (ValidateAndMaterialize(document))
            {
                case CampaignProfileLoadStatus.Loaded:
                    return ProfileReadResult.Valid;
                case CampaignProfileLoadStatus.UnsupportedVersion:
                    return ProfileReadResult.UnsupportedVersion;
                default:
                    return ProfileReadResult.InvalidDocument;
            }
        }

        private static CampaignProfileLoadStatus ValidateAndMaterialize(
            CampaignProfileDocument document)
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

            CampaignProfileDocumentMaterializer.MaterializeValidated(document);

            return CampaignProfileLoadStatus.Loaded;
        }

        private enum ProfileReadResult
        {
            Missing,
            Valid,
            Corrupt,
            UnsupportedVersion,
            InvalidDocument,
        }

    }
}
