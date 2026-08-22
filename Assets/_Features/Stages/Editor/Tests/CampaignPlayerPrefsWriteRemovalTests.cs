using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    internal static class RemovedCampaignPlayerPrefsKeys
    {
        public const string LegacySaveSlotsKey = "Game.Feature.Stages.SaveSlots";
        public const string LegacyActiveSaveSlotKey = "Game.Feature.Stages.ActiveSaveSlot";
        public const string SaveSlotsKey = "Game.Feature.Stages.StageClearSaveSlots";
        public const string ActiveSaveSlotKey = "Game.Feature.Stages.ActiveStageClearSaveSlot";
    }

    public sealed class CampaignPlayerPrefsWriteRemovalTests
    {
        private PlayerPrefsTestStateScope _playerPrefsState;

        [SetUp]
        public void SetUp()
        {
            _playerPrefsState = PlayerPrefsTestStateScope.Capture(
                PlayerPrefsKeySpec.String(RemovedCampaignPlayerPrefsKeys.SaveSlotsKey),
                PlayerPrefsKeySpec.Int(RemovedCampaignPlayerPrefsKeys.ActiveSaveSlotKey));
        }

        [TearDown]
        public void TearDown()
        {
            _playerPrefsState?.Dispose();
            _playerPrefsState = null;
        }

        [Test]
        public void ProductionCompositionTypes_DoNotExposeCompatibilityDependencies()
        {
            var assembly = typeof(CampaignSaveCompositionProvider).Assembly;
            var optionProperties = typeof(CampaignSaveCompositionOptions)
                .GetProperties()
                .Select(property => property.Name)
                .ToArray();

            Assert.That(assembly.GetType(
                "Game.Feature.Stages.LegacyPlayerPrefsCampaignImporter"), Is.Null);
            Assert.That(assembly.GetType(
                "Game.Feature.Stages.CampaignSaveMigrationCoordinator"), Is.Null);
            Assert.That(assembly.GetType(
                "Game.Feature.Stages.CampaignLegacyImportDocument"), Is.Null);
            Assert.That(optionProperties, Does.Not.Contain("AllowLegacyImport"));
            Assert.That(optionProperties, Does.Not.Contain("BackendMode"));
            Assert.That(optionProperties, Does.Not.Contain("LegacyImportMarkerStore"));
        }

        [Test]
        public void ProductionCampaignSources_DoNotUsePlayerPrefsOrRemovedCompatibilityTypes()
        {
            var productionFiles = Directory
                .GetFiles(
                    Path.Combine("Assets", "_Features"),
                    "*.cs",
                    SearchOption.AllDirectories)
                .Where(path => path.Replace('\\', '/').Contains("/Runtime/", StringComparison.Ordinal))
                .ToArray();
            var removedTokens = new[]
            {
                "LegacyPlayerPrefsCampaignImporter",
                "CampaignSaveMigrationCoordinator",
                "SaveSlotStoreCompatibilityAdapter",
                RemovedCampaignPlayerPrefsKeys.LegacySaveSlotsKey,
                RemovedCampaignPlayerPrefsKeys.LegacyActiveSaveSlotKey,
                RemovedCampaignPlayerPrefsKeys.SaveSlotsKey,
                RemovedCampaignPlayerPrefsKeys.ActiveSaveSlotKey,
            };

            foreach (var path in productionFiles)
            {
                var source = File.ReadAllText(path);
                foreach (var token in removedTokens)
                {
                    Assert.That(source, Does.Not.Contain(token), path);
                }

                var normalizedPath = path.Replace('\\', '/');
                if (source.Contains("PlayerPrefs.", StringComparison.Ordinal))
                {
                    Assert.That(
                        normalizedPath,
                        Does.EndWith("/UI/UI_Composition/Runtime/UiSettingsBridgeAssembly.cs"),
                        $"Unexpected PlayerPrefs production owner: {path}");
                }
            }

            var sharedPlayerPrefsOwners = Directory
                .GetFiles(
                    Path.Combine("Assets", "_Shared"),
                    "*.cs",
                    SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains("PlayerPrefs.", StringComparison.Ordinal))
                .Select(path => path.Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            Assert.That(
                sharedPlayerPrefsOwners,
                Is.EqualTo(new[]
                {
                    "Assets/_Shared/Audio/Runtime/PlayerPrefsAudioSettingsStore.cs",
                    "Assets/_Shared/Display/Runtime/PlayerPrefsDisplaySettingsStore.cs",
                    "Assets/_Shared/Input/Runtime/PlayerPrefsKeyboardBindingStore.cs",
                }));
        }

        [Test]
        public void PlayerPrefsTestStateScope_RestoresTypedValuesAndOriginalAbsence()
        {
            const string stringKey = "Game.Feature.Stages.Tests.PlayerPrefsState.String";
            const string intKey = "Game.Feature.Stages.Tests.PlayerPrefsState.Int";
            const string floatKey = "Game.Feature.Stages.Tests.PlayerPrefsState.Float";
            const string absentKey = "Game.Feature.Stages.Tests.PlayerPrefsState.Absent";
            using var originalState = PlayerPrefsTestStateScope.Capture(
                PlayerPrefsKeySpec.String(stringKey),
                PlayerPrefsKeySpec.Int(intKey),
                PlayerPrefsKeySpec.Float(floatKey),
                PlayerPrefsKeySpec.String(absentKey));
            PlayerPrefs.SetString(stringKey, "before");
            PlayerPrefs.SetInt(intKey, 7);
            PlayerPrefs.SetFloat(floatKey, 0.75f);
            PlayerPrefs.DeleteKey(absentKey);
            PlayerPrefs.Save();
            var scope = PlayerPrefsTestStateScope.Capture(
                PlayerPrefsKeySpec.String(stringKey),
                PlayerPrefsKeySpec.Int(intKey),
                PlayerPrefsKeySpec.Float(floatKey),
                PlayerPrefsKeySpec.String(absentKey));
            PlayerPrefs.SetString(stringKey, "after");
            PlayerPrefs.SetInt(intKey, 99);
            PlayerPrefs.SetFloat(floatKey, 0.1f);
            PlayerPrefs.SetString(absentKey, "temporary");

            scope.Dispose();
            scope.Dispose();

            Assert.That(PlayerPrefs.GetString(stringKey), Is.EqualTo("before"));
            Assert.That(PlayerPrefs.GetInt(intKey), Is.EqualTo(7));
            Assert.That(PlayerPrefs.GetFloat(floatKey), Is.EqualTo(0.75f));
            Assert.That(PlayerPrefs.HasKey(absentKey), Is.False);
        }

        [Test]
        public void MissingProfile_DoesNotReadProgressionPlayerPrefs()
        {
            using var harness = new SaveHarness();
            PlayerPrefs.SetString(
                RemovedCampaignPlayerPrefsKeys.SaveSlotsKey,
                "{\"removedSchema\":true}");
            PlayerPrefs.Save();

            var facade = CampaignSaveFacadeFactory.Create(harness.Options());
            var load = facade.CampaignSaveSlots.LoadAllWithReport();

            Assert.That(load.Slots, Has.Length.EqualTo(CampaignSaveSlotPolicy.SlotCount));
            Assert.That(load.Slots.All(slot => slot.IsEmpty), Is.True);
            Assert.That(load.Report.Status, Is.EqualTo(CampaignSaveLoadStatus.Missing));
            Assert.That(File.Exists(harness.ProfilePath), Is.False);
            Assert.That(PlayerPrefs.HasKey(RemovedCampaignPlayerPrefsKeys.SaveSlotsKey), Is.True);
        }

        [Test]
        public void ProfileWrites_DoNotWriteOrDeleteProgressionPlayerPrefs()
        {
            using var harness = new SaveHarness();
            PlayerPrefs.SetString(RemovedCampaignPlayerPrefsKeys.SaveSlotsKey, "sentinel");
            PlayerPrefs.SetInt(RemovedCampaignPlayerPrefsKeys.ActiveSaveSlotKey, 3);
            PlayerPrefs.Save();
            var facade = CampaignSaveFacadeFactory.Create(harness.Options());

            facade.CampaignSaveSlots.SaveSlot(CreateSlot(1));
            facade.CampaignSaveSlots.DeleteSlot(1);
            facade.CampaignSaveSlots.ClearAll();

            Assert.That(PlayerPrefs.GetString(RemovedCampaignPlayerPrefsKeys.SaveSlotsKey),
                Is.EqualTo("sentinel"));
            Assert.That(PlayerPrefs.GetInt(RemovedCampaignPlayerPrefsKeys.ActiveSaveSlotKey),
                Is.EqualTo(3));
        }

        [Test]
        public void MissingLocalState_DoesNotReadActiveSlotPlayerPrefs()
        {
            using var harness = new SaveHarness();
            var profileDocument = CampaignProfileDocumentMapper.ToDocument(
                new[] { CreateSlot(2) },
                "profile",
                2,
                string.Empty,
                "product");
            var profileStore = new CampaignSaveSlotStoreAdapter(
                new CampaignSaveService(new LoadedRepository(profileDocument)));
            var localRepository = new FileCampaignLocalLaunchStateRepository(
                new AtomicTextFileStore(harness.SaveRootPath));
            var storage = new LocalStateActiveSlotStorage(localRepository, profileStore);
            PlayerPrefs.SetInt(RemovedCampaignPlayerPrefsKeys.ActiveSaveSlotKey, 2);
            PlayerPrefs.Save();

            var loaded = storage.TryGetActiveSlot(out var slotNumber);

            Assert.That(loaded, Is.False);
            Assert.That(slotNumber, Is.Zero);
            Assert.That(File.Exists(harness.LocalStatePath), Is.False);
            Assert.That(PlayerPrefs.GetInt(RemovedCampaignPlayerPrefsKeys.ActiveSaveSlotKey),
                Is.EqualTo(2));
        }

        [Test]
        public void DeleteSlot_UsesCurrentLaunchStateRepairWithoutCompatibilityMetadata()
        {
            var profile = new RecordingSaveSlotStore(CreateSlot(1));
            var active = new RecordingActiveSlotStorage(1);
            var pending = new RecordingLaunchHandoffStore(
                new CampaignLaunchHandoff(
                    1,
                    StageId.CreateOrThrow("stage-1-1"),
                    StageNavigationKind.Continue,
                    "test",
                    Guid.NewGuid()));
            var repairing = new CampaignLaunchStateRepairingCampaignSaveSlotStore(
                profile,
                active,
                pending);

            repairing.DeleteSlot(1);

            Assert.That(profile.DeleteCount, Is.EqualTo(1));
            Assert.That(active.ClearCount, Is.EqualTo(1));
            Assert.That(pending.ClearCount, Is.EqualTo(1));
        }

        private static SaveSlotData CreateSlot(int slotNumber)
        {
            return new SaveSlotData
            {
                SlotNumber = slotNumber,
                CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                CurrentLevelGroupId = "level-1",
                RemainingChances = 3,
                StageClearProfileSnapshot = new StageClearProfileSnapshot(),
            };
        }

        private sealed class SaveHarness : IDisposable
        {
            private readonly TemporarySavePathProvider _pathProvider;

            public SaveHarness()
            {
                SaveRootPath = Path.Combine(
                    "Temp",
                    "CampaignPlayerPrefsWriteRemovalTests",
                    Guid.NewGuid().ToString("N"));
                _pathProvider = new TemporarySavePathProvider(SaveRootPath);
            }

            public string SaveRootPath { get; }

            public string ProfilePath =>
                Path.Combine(SaveRootPath, FileCampaignProfileRepository.ProfileFileName);

            public string LocalStatePath =>
                Path.Combine(SaveRootPath, CampaignLocalLaunchStateRepository.FileName);

            public CampaignSaveCompositionOptions Options()
            {
                return new CampaignSaveCompositionOptions
                {
                    PathProvider = _pathProvider,
                    ProductVersion = "product",
                    ProfileId = "profile",
                };
            }

            public void Dispose()
            {
                if (Directory.Exists(SaveRootPath))
                {
                    Directory.Delete(SaveRootPath, recursive: true);
                }
            }
        }

        private sealed class TemporarySavePathProvider : SavePathProviderBase
        {
            public TemporarySavePathProvider(string saveRootPath)
                : base(saveRootPath)
            {
            }
        }

        private sealed class LoadedRepository : ICampaignProfileRepository
        {
            private CampaignProfileDocument _document;

            public LoadedRepository(CampaignProfileDocument document)
            {
                _document = document;
            }

            public CampaignProfileLoadResult Load()
            {
                return new CampaignProfileLoadResult(
                    CampaignProfileLoadStatus.Loaded,
                    _document,
                    "loaded");
            }

            public void Save(CampaignProfileDocument document)
            {
                _document = document;
            }

            public void SaveDestructive(CampaignProfileDocument document)
            {
                Save(document);
            }
        }

        private sealed class RecordingSaveSlotStore : ICampaignSaveSlotStore
        {
            private SaveSlotData _slot;

            public RecordingSaveSlotStore(SaveSlotData slot)
            {
                _slot = slot;
            }

            public int DeleteCount { get; private set; }

            public string DiagnosticsKey => "recording";

            public CampaignSaveLoadReport LastCampaignLoadReport =>
                CampaignSaveLoadReport.Loaded("loaded", string.Empty);

            public SaveSlotData[] LoadAll() => _slot == null
                ? Array.Empty<SaveSlotData>()
                : new[] { _slot };

            public CampaignSaveLoadResult LoadAllWithReport() =>
                new CampaignSaveLoadResult(LoadAll(), LastCampaignLoadReport);

            public SaveSlotData LoadSlot(int slotNumber) =>
                _slot != null && _slot.SlotNumber == slotNumber
                    ? _slot
                    : SaveSlotData.CreateEmpty(slotNumber);

            public void SaveSlot(SaveSlotData slot) => _slot = slot;

            public SaveSlotData InitializeNewGame(
                int slotNumber,
                CampaignStageSequenceResolver sequenceResolver,
                string lastPlayedAt) => throw new NotSupportedException();

            public void UpdateSlot(int slotNumber, Action<SaveSlotData> mutation) =>
                mutation(_slot);

            public void DeleteSlot(int slotNumber)
            {
                DeleteCount++;
                _slot = null;
            }

            public void ClearAll() => _slot = null;
        }

        private sealed class RecordingActiveSlotStorage : IActiveSlotStorage
        {
            private int _slotNumber;

            public RecordingActiveSlotStorage(int slotNumber)
            {
                _slotNumber = slotNumber;
            }

            public int ClearCount { get; private set; }

            public string DiagnosticsKey => "active";

            public bool TryGetActiveSlot(out int slotNumber)
            {
                slotNumber = _slotNumber;
                return slotNumber > 0;
            }

            public void SetActiveSlot(int slotNumber) => _slotNumber = slotNumber;

            public void ClearActiveSlot()
            {
                ClearCount++;
                _slotNumber = 0;
            }
        }

        private sealed class RecordingLaunchHandoffStore : ICampaignLaunchHandoffStore
        {
            private CampaignLaunchHandoff _handoff;

            public RecordingLaunchHandoffStore(CampaignLaunchHandoff handoff)
            {
                _handoff = handoff;
            }

            public int ClearCount { get; private set; }

            public bool TryBegin(
                int slotNumber,
                StageId stageId,
                StageNavigationKind navigationKind,
                string source,
                out CampaignLaunchHandoff handoff)
            {
                handoff = _handoff;
                return false;
            }

            public bool TryPeek(out CampaignLaunchHandoff handoff)
            {
                handoff = _handoff;
                return handoff != null;
            }

            public bool TryConsume(Guid token, out CampaignLaunchHandoff handoff)
            {
                handoff = null;
                return false;
            }

            public bool TryClear(Guid token)
            {
                if (_handoff == null || _handoff.Token != token)
                {
                    return false;
                }

                ClearCount++;
                _handoff = null;
                return true;
            }
        }
    }

    internal enum PlayerPrefsValueKind
    {
        String,
        Int,
        Float,
    }

    internal readonly struct PlayerPrefsKeySpec
    {
        private PlayerPrefsKeySpec(string key, PlayerPrefsValueKind kind)
        {
            Key = key;
            Kind = kind;
        }

        public string Key { get; }

        public PlayerPrefsValueKind Kind { get; }

        public static PlayerPrefsKeySpec String(string key) =>
            new PlayerPrefsKeySpec(key, PlayerPrefsValueKind.String);

        public static PlayerPrefsKeySpec Int(string key) =>
            new PlayerPrefsKeySpec(key, PlayerPrefsValueKind.Int);

        public static PlayerPrefsKeySpec Float(string key) =>
            new PlayerPrefsKeySpec(key, PlayerPrefsValueKind.Float);
    }

    internal sealed class PlayerPrefsTestStateScope : IDisposable
    {
        private readonly Entry[] _entries;
        private bool _disposed;

        private PlayerPrefsTestStateScope(PlayerPrefsKeySpec[] specs)
        {
            if (specs == null)
            {
                throw new ArgumentNullException(nameof(specs));
            }

            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            _entries = new Entry[specs.Length];
            for (var index = 0; index < specs.Length; index++)
            {
                var spec = specs[index];
                if (string.IsNullOrWhiteSpace(spec.Key) || !seenKeys.Add(spec.Key))
                {
                    throw new ArgumentException(
                        $"PlayerPrefs test key '{spec.Key}' is empty or duplicated.",
                        nameof(specs));
                }

                _entries[index] = Entry.Capture(spec);
            }
        }

        public static PlayerPrefsTestStateScope Capture(params PlayerPrefsKeySpec[] specs) =>
            new PlayerPrefsTestStateScope(specs);

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            List<Exception> failures = null;
            for (var index = 0; index < _entries.Length; index++)
            {
                try
                {
                    _entries[index].Restore();
                }
                catch (Exception exception)
                {
                    failures ??= new List<Exception>();
                    failures.Add(exception);
                }
            }

            try
            {
                PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                failures ??= new List<Exception>();
                failures.Add(exception);
            }

            if (failures?.Count == 1)
            {
                throw failures[0];
            }

            if (failures?.Count > 1)
            {
                throw new AggregateException(
                    "PlayerPrefs test state could not be fully restored.",
                    failures);
            }
        }

        private readonly struct Entry
        {
            private Entry(
                string key,
                PlayerPrefsValueKind kind,
                bool hadValue,
                string stringValue,
                int intValue,
                float floatValue)
            {
                Key = key;
                Kind = kind;
                HadValue = hadValue;
                StringValue = stringValue;
                IntValue = intValue;
                FloatValue = floatValue;
            }

            private string Key { get; }
            private PlayerPrefsValueKind Kind { get; }
            private bool HadValue { get; }
            private string StringValue { get; }
            private int IntValue { get; }
            private float FloatValue { get; }

            public static Entry Capture(PlayerPrefsKeySpec spec)
            {
                var hadValue = PlayerPrefs.HasKey(spec.Key);
                switch (spec.Kind)
                {
                    case PlayerPrefsValueKind.String:
                        return new Entry(
                            spec.Key,
                            spec.Kind,
                            hadValue,
                            hadValue ? PlayerPrefs.GetString(spec.Key, string.Empty) : string.Empty,
                            0,
                            0f);
                    case PlayerPrefsValueKind.Int:
                        return new Entry(
                            spec.Key,
                            spec.Kind,
                            hadValue,
                            string.Empty,
                            hadValue ? PlayerPrefs.GetInt(spec.Key, 0) : 0,
                            0f);
                    case PlayerPrefsValueKind.Float:
                        return new Entry(
                            spec.Key,
                            spec.Kind,
                            hadValue,
                            string.Empty,
                            0,
                            hadValue ? PlayerPrefs.GetFloat(spec.Key, 0f) : 0f);
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(spec),
                            spec.Kind,
                            "Unknown PlayerPrefs value kind.");
                }
            }

            public void Restore()
            {
                if (!HadValue)
                {
                    PlayerPrefs.DeleteKey(Key);
                    return;
                }

                switch (Kind)
                {
                    case PlayerPrefsValueKind.String:
                        PlayerPrefs.SetString(Key, StringValue);
                        break;
                    case PlayerPrefsValueKind.Int:
                        PlayerPrefs.SetInt(Key, IntValue);
                        break;
                    case PlayerPrefsValueKind.Float:
                        PlayerPrefs.SetFloat(Key, FloatValue);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(Kind),
                            Kind,
                            "Unknown PlayerPrefs value kind.");
                }
            }
        }
    }
}
