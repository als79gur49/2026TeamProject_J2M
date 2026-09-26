using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class PlayerCaptureLaunchBootstrapSafetyTests
    {
        [SetUp]
        public void SetUp()
        {
            StageLaunchContextStore.Clear();
            EditorDirectPlayContextStore.Clear();
            EditorDirectPlayContextStore.ClearTemporaryCampaignState();
        }

        [TearDown]
        public void TearDown()
        {
            StageLaunchContextStore.Clear();
            EditorDirectPlayContextStore.Clear();
            EditorDirectPlayContextStore.ClearTemporaryCampaignState();
        }

        [Test]
        public void NormalSlot_ReleaseEquivalentEnvironment_RejectsBeforePersistenceCreation()
        {
            var factory = new RecordingPersistenceFactory();

            var primed = TryPrimeNormal(
                CreateEnvironment(
                    isDevelopmentBuild: false,
                    hasCaptureBuildCapability: true,
                    isolatedIdentity: true),
                factory,
                out var error);

            AssertRejectedWithoutMutation(primed, error, factory);
            Assert.That(error, Does.Contain("not a development build"));
        }

        [Test]
        public void NormalSlot_DevelopmentWithoutCaptureBuild_RejectsBeforePersistenceCreation()
        {
            var factory = new RecordingPersistenceFactory();

            var primed = TryPrimeNormal(
                CreateEnvironment(
                    isDevelopmentBuild: true,
                    hasCaptureBuildCapability: false,
                    isolatedIdentity: true),
                factory,
                out var error);

            AssertRejectedWithoutMutation(primed, error, factory);
            Assert.That(error, Does.Contain("capture build capability is absent"));
        }

        [Test]
        public void NormalSlot_CaptureBuildWithProductionIdentity_RejectsBeforePersistenceCreation()
        {
            var factory = new RecordingPersistenceFactory();

            var primed = TryPrimeNormal(
                CreateEnvironment(
                    isDevelopmentBuild: true,
                    hasCaptureBuildCapability: true,
                    isolatedIdentity: false),
                factory,
                out var error);

            AssertRejectedWithoutMutation(primed, error, factory);
            Assert.That(error, Does.Contain("persistence identity is not isolated"));
        }

        [Test]
        public void NormalSlot_FullyAuthorizedEnvironment_SeedsExpectedSlotAndActiveState()
        {
            var factory = new RecordingPersistenceFactory();

            var primed = TryPrimeNormal(
                CreateEnvironment(
                    isDevelopmentBuild: true,
                    hasCaptureBuildCapability: true,
                    isolatedIdentity: true),
                factory,
                out var error);

            Assert.That(primed, Is.True, error);
            Assert.That(factory.NormalCreateCount, Is.EqualTo(1));
            Assert.That(factory.TempCreateCount, Is.Zero);
            Assert.That(factory.Persistence.ClearAllCount, Is.EqualTo(1));
            Assert.That(factory.Persistence.ClearActiveSlotCount, Is.EqualTo(1));
            Assert.That(factory.Persistence.SaveSlotCount, Is.EqualTo(1));
            Assert.That(factory.Persistence.SetActiveSlotCount, Is.EqualTo(1));
            Assert.That(factory.Persistence.SavedSlots, Has.Count.EqualTo(1));
            Assert.That(factory.Persistence.SavedSlots[0].SlotNumber, Is.EqualTo(1));
            Assert.That(
                factory.Persistence.SavedSlots[0].StageId,
                Is.EqualTo(StageId.CreateOrThrow("stage-4-3")));
            Assert.That(factory.Persistence.ActiveSlotNumber, Is.EqualTo(1));
            Assert.That(StageLaunchContextStore.CurrentStageId.Value, Is.EqualTo("stage-4-3"));
        }

        [Test]
        public void NormalSlot_InvalidStageId_RejectsBeforePersistenceCreation()
        {
            var factory = new RecordingPersistenceFactory();

            var primed = PlayerCaptureLaunchBootstrap.TryPrimeFromArguments(
                new[]
                {
                    "Game.exe",
                    PlayerCaptureLaunchOptions.CaptureStageArg,
                    "???",
                    PlayerCaptureLaunchBootstrap.CampaignNormalSlotArgument,
                },
                logErrors: false,
                CreateEnvironment(true, true, isolatedIdentity: true),
                factory,
                out var error);

            AssertRejectedWithoutMutation(primed, error, factory);
            Assert.That(error, Does.Contain("cannot be normalized"));
        }

        [Test]
        public void ConflictingPersistenceModes_RejectBeforePersistenceCreation()
        {
            var factory = new RecordingPersistenceFactory();

            var primed = PlayerCaptureLaunchBootstrap.TryPrimeFromArguments(
                new[]
                {
                    "Game.exe",
                    PlayerCaptureLaunchOptions.CaptureStageArg,
                    "stage-4-3",
                    PlayerCaptureLaunchBootstrap.CampaignNormalSlotArgument,
                    PlayerCaptureLaunchBootstrap.CampaignTempSlotArgument,
                },
                logErrors: false,
                CreateEnvironment(true, true, isolatedIdentity: true),
                factory,
                out var error);

            AssertRejectedWithoutMutation(primed, error, factory);
            Assert.That(error, Does.Contain("cannot request normal and DirectPlay"));
        }

        [Test]
        public void NoCaptureArguments_DoesNotCreateOrMutatePersistence()
        {
            var factory = new RecordingPersistenceFactory();

            var primed = PlayerCaptureLaunchBootstrap.TryPrimeFromArguments(
                new[] { "Game.exe" },
                logErrors: false,
                CreateEnvironment(false, false, isolatedIdentity: false),
                factory,
                out var error);

            Assert.That(primed, Is.True, error);
            AssertNoPersistenceInteraction(factory);
            Assert.That(StageLaunchContextStore.TryPeek(out _), Is.False);
            Assert.That(EditorDirectPlayContextStore.GetCurrentOrNone().Mode, Is.EqualTo(EditorDirectPlayMode.None));
        }

        [TestCase("--capture-stage", false)]
        [TestCase("--capture-stage=", true)]
        [TestCase("-captureStage", false)]
        public void BareCaptureStage_PrimesValidatedTemporaryCampaignSlot(
            string argument, bool inline)
        {
            var factory = new RecordingPersistenceFactory();
            var args = inline
                ? new[] { "Game.exe", argument + "stage-3-2" }
                : new[] { "Game.exe", argument, "stage-3-2" };

            var primed = PlayerCaptureLaunchBootstrap.TryPrimeFromArguments(
                args, logErrors: false,
                CreateEnvironment(false, false, isolatedIdentity: false),
                factory, out var error);

            Assert.That(primed, Is.True, error);
            Assert.That(factory.TempCreateCount, Is.EqualTo(1));
            Assert.That(factory.NormalCreateCount, Is.Zero);
            Assert.That(factory.Persistence.SavedSlots, Has.Count.EqualTo(1));
            Assert.That(factory.Persistence.SavedSlots[0].LevelGroupId, Is.EqualTo("level-3"));
            Assert.That(factory.Persistence.SavedSlots[0].RemainingChances, Is.EqualTo(2));
            Assert.That(factory.Persistence.ActiveSlotNumber, Is.EqualTo(1));
            Assert.That(EditorDirectPlayContextStore.GetCurrentOrNone().Mode,
                Is.EqualTo(EditorDirectPlayMode.CampaignTempSlot));
            Assert.That(EditorDirectPlayContextStore.GetCurrentOrNone().SuppressCampaignFlow,
                Is.False);
        }

        [TestCase("--capture-stage", false)]
        [TestCase("--capture-stage=", true)]
        [TestCase("-captureStage", false)]
        public void BareCaptureStage_AcceptsTemporarySlotChanceOverride(
            string argument, bool inline)
        {
            var factory = new RecordingPersistenceFactory();
            var args = inline
                ? new[] { "Game.exe", argument + "stage-3-2",
                    PlayerCaptureLaunchBootstrap.CampaignTempSlotChancesArgument, "1" }
                : new[] { "Game.exe", argument, "stage-3-2",
                    PlayerCaptureLaunchBootstrap.CampaignTempSlotChancesArgument, "1" };

            var primed = PlayerCaptureLaunchBootstrap.TryPrimeFromArguments(
                args, logErrors: false,
                CreateEnvironment(false, false, isolatedIdentity: false),
                factory, out var error);

            Assert.That(primed, Is.True, error);
            Assert.That(factory.Persistence.SavedSlots, Has.Count.EqualTo(1));
            Assert.That(factory.Persistence.SavedSlots[0].RemainingChances, Is.EqualTo(1));
            Assert.That(EditorDirectPlayContextStore.GetCurrentOrNone().RemainingChances,
                Is.EqualTo(1));
        }

        [TestCase("--capture-stage", false)]
        [TestCase("--capture-stage=", true)]
        [TestCase("-captureStage", false)]
        public void CaptureStageOutsideCampaign_RejectsBeforePersistenceMutation(
            string argument, bool inline)
        {
            var factory = new RecordingPersistenceFactory();
            var args = inline
                ? new[] { "Game.exe", argument + "legacy-stage-5-1" }
                : new[] { "Game.exe", argument, "legacy-stage-5-1" };

            var primed = PlayerCaptureLaunchBootstrap.TryPrimeFromArguments(
                args, logErrors: false,
                CreateEnvironment(false, false, isolatedIdentity: false),
                factory, out var error);

            AssertRejectedWithoutMutation(primed, error, factory);
            Assert.That(error, Does.Contain("Campaign catalog and sequence"));
        }

        [TestCase("--capture-stage", "stage-3-2", "--capture-stage", "legacy-stage-5-1")]
        [TestCase("--capture-stage=stage-3-2", null, "-captureStage", "legacy-stage-5-1")]
        [TestCase("-captureStage", "stage-3-2", "--capture-stage=legacy-stage-5-1", null)]
        public void DuplicateCaptureStage_RejectsBeforePersistenceMutation(
            string firstArgument,
            string firstValue,
            string secondArgument,
            string secondValue)
        {
            var factory = new RecordingPersistenceFactory();
            var args = new List<string> { "Game.exe", firstArgument };
            if (firstValue != null)
            {
                args.Add(firstValue);
            }

            args.Add(secondArgument);
            if (secondValue != null)
            {
                args.Add(secondValue);
            }

            var primed = PlayerCaptureLaunchBootstrap.TryPrimeFromArguments(
                args.ToArray(), logErrors: false,
                CreateEnvironment(false, false, isolatedIdentity: false),
                factory, out var error);

            AssertRejectedWithoutMutation(primed, error, factory);
            Assert.That(error, Does.Contain("cannot be repeated"));
        }

        [Test]
        public void UnrelatedVisualCaptureArguments_DoNotBypassNormalSlotAuthorization()
        {
            var factory = new RecordingPersistenceFactory();

            var primed = PlayerCaptureLaunchBootstrap.TryPrimeFromArguments(
                new[]
                {
                    "Game.exe",
                    PlayerCaptureLaunchOptions.CaptureStageArg,
                    "stage-4-3",
                    PlayerCaptureLaunchBootstrap.CampaignNormalSlotArgument,
                    "--terminal-player-build-smoke",
                    "--capture-screenshot",
                },
                logErrors: false,
                CreateEnvironment(
                    isDevelopmentBuild: true,
                    hasCaptureBuildCapability: false,
                    isolatedIdentity: true),
                factory,
                out var error);

            AssertRejectedWithoutMutation(primed, error, factory);
            Assert.That(error, Does.Contain("capture build capability is absent"));
        }

        [Test]
        public void UnauthorizedNormalSlot_PreservesProfileBackupAndActiveStateHashes()
        {
            var root = CreateDisposableRoot();
            try
            {
                var saveRoot = Path.Combine(root, "Saves");
                Directory.CreateDirectory(saveRoot);
                var profilePath = Path.Combine(saveRoot, "profile.json");
                var backupPath = Path.Combine(saveRoot, "profile.json.bak");
                var activePath = Path.Combine(saveRoot, "local-launch-state.json");
                File.WriteAllText(profilePath, "profile sentinel slots=1,2,3");
                File.WriteAllText(backupPath, "backup sentinel slots=1,2,3");
                File.WriteAllText(activePath, "active sentinel slot=2");
                var before = new[] { profilePath, backupPath, activePath }
                    .ToDictionary(path => path, ComputeSha256);
                var factory = new RecordingPersistenceFactory(() =>
                {
                    File.WriteAllText(profilePath, "MUTATED");
                    File.WriteAllText(backupPath, "MUTATED");
                    File.WriteAllText(activePath, "MUTATED");
                });

                var primed = TryPrimeNormal(
                    CreateEnvironment(
                        isDevelopmentBuild: true,
                        hasCaptureBuildCapability: false,
                        isolatedIdentity: true),
                    factory,
                    out var error);

                AssertRejectedWithoutMutation(primed, error, factory);
                foreach (var pair in before)
                {
                    Assert.That(ComputeSha256(pair.Key), Is.EqualTo(pair.Value), pair.Key);
                }
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void UnauthorizedNormalSlot_DoesNotCreateMissingSaveFilesOrBackup()
        {
            var root = CreateDisposableRoot();
            try
            {
                var saveRoot = Path.Combine(root, "Saves");
                var profilePath = Path.Combine(saveRoot, "profile.json");
                var backupPath = Path.Combine(saveRoot, "profile.json.bak");
                var activePath = Path.Combine(saveRoot, "local-launch-state.json");
                var factory = new RecordingPersistenceFactory(() =>
                {
                    Directory.CreateDirectory(saveRoot);
                    File.WriteAllText(profilePath, "MUTATED");
                    File.WriteAllText(backupPath, "MUTATED");
                    File.WriteAllText(activePath, "MUTATED");
                });

                var primed = TryPrimeNormal(
                    CreateEnvironment(
                        isDevelopmentBuild: false,
                        hasCaptureBuildCapability: true,
                        isolatedIdentity: true),
                    factory,
                    out var error);

                AssertRejectedWithoutMutation(primed, error, factory);
                Assert.That(File.Exists(profilePath), Is.False);
                Assert.That(File.Exists(backupPath), Is.False);
                Assert.That(File.Exists(activePath), Is.False);
                Assert.That(Directory.Exists(saveRoot), Is.False);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void CaptureBuilder_UsesBuildOnlyCapabilityAndUniqueProductIdentity()
        {
            var defines = PlayerProfilerCaptureCli.CaptureBuildScriptingDefinesForTests();
            var productionName = "VectorQuake";
            var isolatedName =
                PlayerCapturePersistenceAuthorization.IsolatedProductNamePrefix +
                "20260813T120000Z";

            Assert.That(
                defines,
                Is.EqualTo(new[]
                {
                    PlayerCapturePersistenceAuthorization.BuildCapabilityDefine,
                }));
            Assert.That(
                PlayerProfilerCaptureCli.IsAllowedCaptureProductNameForTests(
                    productionName,
                    isolatedName),
                Is.True);
            Assert.That(
                PlayerProfilerCaptureCli.IsAllowedCaptureProductNameForTests(
                    productionName,
                    productionName),
                Is.False);
            Assert.That(
                PlayerProfilerCaptureCli.IsAllowedCaptureProductNameForTests(
                    productionName,
                    "VectorQuake-ArbitraryCapture"),
                Is.False);
        }

        [Test]
        public void PerformanceCaptureBuilder_UsesReleaseLikeBuildOptions()
        {
            Assert.That(
                PlayerProfilerCaptureCli.PerformanceBuildOptionsForTests(),
                Is.EqualTo(UnityEditor.BuildOptions.None));
            Assert.That(
                PlayerProfilerCaptureCli.PerformanceFrameTimingStatsEnabledForTests(),
                Is.True);
        }

        [Test]
        public void NormalBuildConfiguration_DoesNotEnableCaptureCapabilityGlobally()
        {
            var projectSettings = File.ReadAllText("ProjectSettings/ProjectSettings.asset");
            var releaseBuilder = File.ReadAllText(
                "Assets/_Features/Stages/Editor/Build/WindowsReleaseBuildCli.cs");

            Assert.That(PlayerCapturePersistenceAuthorization.HasBuildCapability, Is.False);
            Assert.That(
                projectSettings,
                Does.Not.Contain(PlayerCapturePersistenceAuthorization.BuildCapabilityDefine));
            Assert.That(
                releaseBuilder,
                Does.Not.Contain(PlayerCapturePersistenceAuthorization.BuildCapabilityDefine));
        }

        [TestCase("VectorQuake-P0Phase4Smoke-20260813", "C:/Users/test/AppData/LocalLow/J2M/VectorQuake-P0Phase4Smoke-20260813", true)]
        [TestCase("VectorQuake-P0Phase4Smoke-20260813", "C:/Users/test/AppData/LocalLow/J2M/VectorQuake", false)]
        [TestCase("VectorQuake", "C:/Users/test/AppData/LocalLow/J2M/VectorQuake", false)]
        [TestCase("VectorQuake-P0Phase4Smoke-", "C:/Users/test/AppData/LocalLow/J2M/VectorQuake-P0Phase4Smoke-", false)]
        public void PersistenceIdentity_RequiresCapturePrefixAndMatchingPathLeaf(
            string productName,
            string persistentDataPath,
            bool expected)
        {
            Assert.That(
                PlayerCapturePersistenceAuthorization.HasIsolatedPersistenceIdentity(
                    productName,
                    persistentDataPath),
                Is.EqualTo(expected));
        }

        private static bool TryPrimeNormal(
            PlayerCaptureRuntimeEnvironment environment,
            RecordingPersistenceFactory factory,
            out string error)
        {
            return PlayerCaptureLaunchBootstrap.TryPrimeFromArguments(
                new[]
                {
                    "Game.exe",
                    PlayerCaptureLaunchOptions.CaptureStageArg,
                    "stage-4-3",
                    PlayerCaptureLaunchBootstrap.CampaignNormalSlotArgument,
                },
                logErrors: false,
                environment,
                factory,
                out error);
        }

        private static PlayerCaptureRuntimeEnvironment CreateEnvironment(
            bool isDevelopmentBuild,
            bool hasCaptureBuildCapability,
            bool isolatedIdentity)
        {
            var productName = isolatedIdentity
                ? PlayerCapturePersistenceAuthorization.IsolatedProductNamePrefix +
                  "20260813T120000Z"
                : "VectorQuake";
            return new PlayerCaptureRuntimeEnvironment(
                isDevelopmentBuild,
                hasCaptureBuildCapability,
                productName,
                $"C:/Users/test/AppData/LocalLow/J2M/{productName}");
        }

        private static void AssertRejectedWithoutMutation(
            bool primed,
            string error,
            RecordingPersistenceFactory factory)
        {
            Assert.That(primed, Is.False);
            Assert.That(error, Is.Not.Empty);
            AssertNoPersistenceInteraction(factory);
            Assert.That(StageLaunchContextStore.TryPeek(out _), Is.False);
            Assert.That(
                EditorDirectPlayContextStore.GetCurrentOrNone().Mode,
                Is.EqualTo(EditorDirectPlayMode.None));
        }

        private static void AssertNoPersistenceInteraction(
            RecordingPersistenceFactory factory)
        {
            Assert.That(factory.NormalCreateCount, Is.Zero);
            Assert.That(factory.TempCreateCount, Is.Zero);
            Assert.That(factory.Persistence.ClearAllCount, Is.Zero);
            Assert.That(factory.Persistence.ClearActiveSlotCount, Is.Zero);
            Assert.That(factory.Persistence.SaveSlotCount, Is.Zero);
            Assert.That(factory.Persistence.SetActiveSlotCount, Is.Zero);
        }

        private static string CreateDisposableRoot()
        {
            var path = Path.Combine(
                "Temp",
                "PlayerCaptureLaunchBootstrapSafetyTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static string ComputeSha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha256 = SHA256.Create();
            return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
        }

        private sealed class RecordingPersistenceFactory : IPlayerCapturePersistenceFactory
        {
            private readonly Action onCreate;

            public RecordingPersistenceFactory(Action onCreate = null)
            {
                this.onCreate = onCreate;
            }

            public int NormalCreateCount { get; private set; }

            public int TempCreateCount { get; private set; }

            public RecordingFixturePersistence Persistence { get; } = new();

            public IPlayerCaptureFixturePersistence CreateNormalCampaignSlot()
            {
                NormalCreateCount++;
                onCreate?.Invoke();
                return Persistence;
            }

            public IPlayerCaptureFixturePersistence CreateTempCampaignSlot()
            {
                TempCreateCount++;
                onCreate?.Invoke();
                return Persistence;
            }
        }

        private sealed class RecordingFixturePersistence : IPlayerCaptureFixturePersistence
        {
            public int ClearAllCount { get; private set; }

            public int ClearActiveSlotCount { get; private set; }

            public int SaveSlotCount { get; private set; }

            public int SetActiveSlotCount { get; private set; }

            public int ActiveSlotNumber { get; private set; }

            public List<CampaignSlotSeedImportRequest> SavedSlots { get; } = new();

            public void ClearAll()
            {
                ClearAllCount++;
                SavedSlots.Clear();
            }

            public void ClearActiveSlot()
            {
                ClearActiveSlotCount++;
                ActiveSlotNumber = 0;
            }

            public void ImportSlotSeed(CampaignSlotSeedImportRequest request)
            {
                SaveSlotCount++;
                SavedSlots.Add(request);
            }

            public void SetActiveSlot(int slotNumber)
            {
                SetActiveSlotCount++;
                ActiveSlotNumber = slotNumber;
            }
        }
    }
}
