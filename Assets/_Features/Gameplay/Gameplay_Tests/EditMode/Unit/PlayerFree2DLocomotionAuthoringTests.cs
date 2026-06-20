using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Stages;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class PlayerFree2DLocomotionAuthoringTests
    {
        [Test]
        [Category("Extended")]
        public void PlayerFree2DLocomotionAuthoring_DefaultCollisionRadius_IsZero()
        {
            var snapshot = PlayerFree2DLocomotionAuthoring.CreateDefault()
                .Compile(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(snapshot.CollisionRadiusUnits, Is.EqualTo(0));
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2DLocomotionAuthoring_DefaultTiming_UsesPlayerOwnedValue()
        {
            var snapshot = PlayerFree2DLocomotionAuthoring.CreateDefault()
                .Compile(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(snapshot.SecondsPerCellAtFullSpeed, Is.EqualTo(0.33333334f));
            Assert.That(snapshot.TicksPerCell, Is.EqualTo(20));
            Assert.That(snapshot.SpeedUnitsPerTick, Is.EqualTo(204));
            Assert.That(snapshot.UnitsPerTickRemainder, Is.EqualTo(16));
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2DLocomotionAuthoring_DefaultActionAssistSettleWindow_Is512Units()
        {
            var snapshot = PlayerFree2DLocomotionAuthoring.CreateDefault()
                .Compile(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(snapshot.ActionAssistSettleWindowUnits, Is.EqualTo(512));
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2DLocomotionAuthoring_CollisionRadiusCells_ConvertsToFixedUnits()
        {
            var settings = PlayerFree2DLocomotionAuthoring.CreateDefault();
            settings.CollisionRadiusCells = 0.1875f;
            var snapshot = settings.Compile(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(snapshot.CollisionRadiusUnits, Is.EqualTo(768));
            Assert.That(snapshot.CollisionRadiusUnits, Is.EqualTo(KinematicFixed.UnitsPerCell * 3 / 16));
        }

        [TestCase(0f, 0)]
        [TestCase(0.0625f, 256)]
        [TestCase(0.125f, 512)]
        [TestCase(0.1875f, 768)]
        [Category("Extended")]
        public void PlayerFree2DLocomotionAuthoring_ActionAssistSettleWindowCells_ConvertsToFixedUnits(
            float actionAssistSettleWindowCells,
            int expectedUnits)
        {
            var settings = PlayerFree2DLocomotionAuthoring.CreateDefault();
            settings.ActionAssistSettleWindowCells = actionAssistSettleWindowCells;
            var snapshot = settings.Compile(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(snapshot.ActionAssistSettleWindowUnits, Is.EqualTo(expectedUnits));
        }

        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        [Category("Extended")]
        public void PlayerFree2DLocomotionAuthoring_InvalidCollisionRadius_Throws(float collisionRadiusCells)
        {
            var settings = PlayerFree2DLocomotionAuthoring.CreateDefault();
            settings.CollisionRadiusCells = collisionRadiusCells;

            Assert.Throws<ArgumentOutOfRangeException>(
                () => settings.Compile(GameplayTimingProfile.DefaultSimulationTicksPerSecond));
        }

        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        [Category("Extended")]
        public void PlayerFree2DLocomotionAuthoring_InvalidActionAssistSettleWindow_Throws(
            float actionAssistSettleWindowCells)
        {
            var settings = PlayerFree2DLocomotionAuthoring.CreateDefault();
            settings.ActionAssistSettleWindowCells = actionAssistSettleWindowCells;

            Assert.Throws<ArgumentOutOfRangeException>(
                () => settings.Compile(GameplayTimingProfile.DefaultSimulationTicksPerSecond));
        }

        [TestCase(0f)]
        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(2.01f)]
        [Category("Extended")]
        public void PlayerFree2DLocomotionAuthoring_InvalidSecondsPerCell_Throws(float secondsPerCellAtFullSpeed)
        {
            var settings = PlayerFree2DLocomotionAuthoring.CreateDefault();
            settings.SecondsPerCellAtFullSpeed = secondsPerCellAtFullSpeed;

            Assert.Throws<ArgumentOutOfRangeException>(
                () => settings.Compile(GameplayTimingProfile.DefaultSimulationTicksPerSecond));
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2DLocomotionAuthoringResolver_NoOverride_ReturnsBaseline()
        {
            var baseline = PlayerFree2DLocomotionAuthoring.CreateDefault();
            baseline.SecondsPerCellAtFullSpeed = 0.4f;
            baseline.CollisionRadiusCells = 0.125f;
            baseline.ActionAssistSettleWindowCells = 0.1875f;

            var resolved = PlayerFree2DLocomotionAuthoringResolver.Resolve(
                baseline,
                PlayerFree2DLocomotionOverride.None);

            Assert.That(resolved.SecondsPerCellAtFullSpeed, Is.EqualTo(0.4f));
            Assert.That(resolved.CollisionRadiusCells, Is.EqualTo(0.125f));
            Assert.That(resolved.ActionAssistSettleWindowCells, Is.EqualTo(0.1875f));
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2DLocomotionAuthoringResolver_PartialOverride_ChangesOnlyEnabledFields()
        {
            var baseline = PlayerFree2DLocomotionAuthoring.CreateDefault();
            baseline.SecondsPerCellAtFullSpeed = 0.4f;
            baseline.CollisionRadiusCells = 0.125f;
            baseline.ActionAssistSettleWindowCells = 0.1875f;
            var overrideValue = PlayerFree2DLocomotionOverride.CreateCollisionAndActionAssist(
                collisionRadiusCells: 0.25f,
                actionAssistSettleWindowCells: 0.0625f);

            var resolved = PlayerFree2DLocomotionAuthoringResolver.Resolve(baseline, overrideValue);
            var snapshot = resolved.Compile(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(snapshot.SecondsPerCellAtFullSpeed, Is.EqualTo(0.4f));
            Assert.That(snapshot.TicksPerCell, Is.EqualTo(24));
            Assert.That(snapshot.CollisionRadiusUnits, Is.EqualTo(1024));
            Assert.That(snapshot.ActionAssistSettleWindowUnits, Is.EqualTo(256));
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2DLocomotionAuthoringResolver_ExplicitDefaultValuedOverride_IsApplied()
        {
            var baseline = PlayerFree2DLocomotionAuthoring.CreateDefault();
            baseline.CollisionRadiusCells = 0.125f;
            baseline.ActionAssistSettleWindowCells = 0.1875f;
            var overrideValue = PlayerFree2DLocomotionOverride.Create(
                overrideSecondsPerCellAtFullSpeed: false,
                secondsPerCellAtFullSpeed: 0f,
                overrideCollisionRadiusCells: true,
                collisionRadiusCells: 0f,
                overrideActionAssistSettleWindowCells: true,
                actionAssistSettleWindowCells: 0.125f);

            var resolved = PlayerFree2DLocomotionAuthoringResolver.Resolve(baseline, overrideValue);
            var snapshot = resolved.Compile(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(snapshot.CollisionRadiusUnits, Is.EqualTo(0));
            Assert.That(snapshot.ActionAssistSettleWindowUnits, Is.EqualTo(512));
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2DLocomotionAuthoringResolver_InvalidOverride_FailsAtCompile()
        {
            var baseline = PlayerFree2DLocomotionAuthoring.CreateDefault();
            var overrideValue = PlayerFree2DLocomotionOverride.Create(
                overrideSecondsPerCellAtFullSpeed: false,
                secondsPerCellAtFullSpeed: 0f,
                overrideCollisionRadiusCells: true,
                collisionRadiusCells: -0.01f,
                overrideActionAssistSettleWindowCells: false,
                actionAssistSettleWindowCells: 0f);

            var resolved = PlayerFree2DLocomotionAuthoringResolver.Resolve(baseline, overrideValue);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => resolved.Compile(GameplayTimingProfile.DefaultSimulationTicksPerSecond));
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2DLocomotionSource_DoesNotReferenceUnitOrEnemyTimingSettings()
        {
            const string path =
                "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/PlayerFree2DLocomotionSettings.cs";

            var source = File.ReadAllText(path);

            Assert.That(source, Does.Not.Contain("UnitKinematicLocomotionTimingSettings"));
            Assert.That(source, Does.Not.Contain("EnemyLocomotionTimingSettings"));
        }

        [Test]
        [Category("Extended")]
        public void TickPipeline_DoesNotCreateDefaultPlayerFree2DSettingsAtRuntime()
        {
            const string path =
                "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs";

            var source = File.ReadAllText(path);

            Assert.That(source, Does.Not.Contain("PlayerFree2DLocomotionAuthoring.CreateDefault"));
            Assert.That(source, Does.Not.Contain("GameplayTimingProfile.DefaultSimulationTicksPerSecond"));
        }

        [Test]
        [Category("Extended")]
        public void StageBackedInstaller_DescribesPlayerFree2DOverrideWithoutMutatingBaseline()
        {
            const string path =
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstaller.cs";

            var source = File.ReadAllText(path);

            Assert.That(source, Does.Contain("TryGetPlayerFree2DLocomotionOverride"));
            Assert.That(source, Does.Contain("PlayerFree2DLocomotionOverride.CreateCollisionAndActionAssist"));
            Assert.That(source, Does.Not.Contain("configuration.PlayerFree2DLocomotion ="));
        }

        [Test]
        [Category("Extended")]
        public void ActiveGameplaySources_DoNotReintroduceRetiredPlayerLocomotionNames()
        {
            AssertNoForbiddenActiveTokens(
                ForbiddenExact("PlayerContinuous" + "LocomotionScenarioTests"),
                ForbiddenExact("PlayerContinuous" + "LocomotionReplayTests"),
                ForbiddenExact("Default" + "GameplayLocomotion"),
                ForbiddenExact("DefaultPlayer" + "UnitsPerTick"),
                ForbiddenPattern("Player" + "LocomotionMode", @"\b" + "Player" + @"LocomotionMode\b"),
                ForbiddenPattern("Player" + "MovementMode", @"\b" + "Player" + @"MovementMode\b"),
                ForbiddenPattern("EnablePlayer" + "Free2D", @"\b" + "EnablePlayer" + @"Free2D[A-Za-z0-9_]*\b"),
                ForbiddenPattern("EnablePlayer" + "Kinematic", @"\b" + "EnablePlayer" + @"Kinematic[A-Za-z0-9_]*\b"),
                ForbiddenPattern("Player" + "KinematicLocomotion", @"\b" + "Player" + @"KinematicLocomotion[A-Za-z0-9_]*\b"),
                ForbiddenPattern("Player" + "DiscreteMovement", @"\b" + "Player" + @"DiscreteMovement[A-Za-z0-9_]*\b"),
                ForbiddenExact("Legacy" + "Discrete"),
                ForbiddenPattern("Kinematic" + "Fallback", @"\b" + "Kinematic" + @"Fallback\b"));
        }

        [Test]
        [Category("Extended")]
        public void ActiveGameplayAssets_DoNotContainRetiredPlayerLocomotionSerializedKeys()
        {
            AssertNoForbiddenActiveTokens(
                ForbiddenExact("player" + "KinematicLocomotionTiming"),
                ForbiddenExact("player" + "ContinuousLocomotion"),
                ForbiddenExact("EnablePlayer" + "SameFaceContinuousLocomotion"),
                ForbiddenPattern("Kinematic" + "MoveDurationSeconds", @"\b" + "Kinematic" + @"MoveDurationSeconds\b"));
        }

        [Test]
        [Category("Extended")]
        public void PlayerControlState_HasNoRetiredMoveCooldownMembers()
        {
            var retiredNames = new[]
            {
                "moveCooldownTicks",
                "MoveCooldownTicks",
                "nextMoveAllowedTick",
                "NextMoveAllowedTick",
                "ConsumeMoveCooldown",
                "IsMoveOnCooldown",
            };
            var members = typeof(PlayerControlState)
                .GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(member => member.Name)
                .ToArray();

            foreach (var retiredName in retiredNames)
            {
                Assert.That(members, Does.Not.Contain(retiredName));
            }

            Assert.That(members, Does.Contain("nextExplicitActionAllowedTick"));
        }

        [Test]
        [Category("Extended")]
        public void Free2DResolver_DoesNotReferenceRetiredMoveCooldownOrActionGate()
        {
            const string path = "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs";
            var source = File.ReadAllText(path);

            Assert.That(source, Does.Contain("ResolvePlayerFree2DLocalLocomotion"));
            Assert.That(source, Does.Not.Contain("ConsumeMoveCooldown"));
            Assert.That(source, Does.Not.Contain("IsMoveOnCooldown"));
            Assert.That(source, Does.Not.Contain("moveCooldownTicks"));
            Assert.That(source, Does.Not.Contain("nextMoveAllowedTick"));
            Assert.That(source, Does.Not.Contain("nextExplicitActionAllowedTick"));
            Assert.That(source, Does.Not.Contain("BlockExplicitActionStartUntil"));
        }

        [Test]
        [Category("Extended")]
        public void PlayerTiming_DoesNotExposeRetiredMoveCooldownPolicy()
        {
            AssertTypeDoesNotExposeMember(typeof(PlayerControlTimingSettings), "MoveCooldownSeconds");
            AssertTypeDoesNotExposeMember(typeof(PlayerControlTimingAuthoritativeSnapshot), "MoveCooldownSeconds");
            AssertTypeDoesNotExposeMember(typeof(PlayerControlTimingAuthoritativeSnapshot), "MoveCooldownTicks");
        }

        [Test]
        [Category("Extended")]
        public void CompiledGameplayAssemblies_DoNotExposeRetiredPlayerLocomotionApi()
        {
            var forbiddenNames = new[]
            {
                "Player" + "KinematicLocomotion",
                "Player" + "DiscreteMovement",
                "Legacy" + "Discrete",
                "Player" + "ContinuousLocomotionSettings",
                "BuildPlayerSameFace" + "KinematicLocomotionPlans",
                "Queue" + "KinematicTurn",
                "CreateDefault" + "PlayerFree2DLocomotionSettings",
                "Default" + "GameplayLocomotion",
            };
            var assemblies = new[]
                {
                    typeof(PlayerControlState).Assembly,
                    typeof(PlayerLogic).Assembly,
                    typeof(TickPipeline).Assembly,
                    typeof(EntityState).Assembly,
                }
                .Distinct()
                .ToArray();

            foreach (var type in assemblies.SelectMany(assembly => assembly.GetTypes()))
            {
                foreach (var forbiddenName in forbiddenNames)
                {
                    Assert.That(
                        type.FullName,
                        Does.Not.Contain(forbiddenName),
                        $"Retired Player locomotion API type remains compiled: {type.FullName}");
                }

                var memberNames = type
                    .GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Select(member => member.Name);
                foreach (var memberName in memberNames)
                {
                    foreach (var forbiddenName in forbiddenNames)
                    {
                        Assert.That(
                            memberName,
                            Does.Not.Contain(forbiddenName),
                            $"Retired Player locomotion API member remains compiled: {type.FullName}.{memberName}");
                    }
                }
            }
        }

        [Test]
        [Category("Extended")]
        public void MotionMode_RetiredNumericValue5_IsUndefined()
        {
            Assert.That(Enum.IsDefined(typeof(MotionMode), 5), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void UserSaveSlotSchema_DoesNotPersistLocomotionRuntimeState()
        {
            var memberNames = typeof(SaveSlotData)
                .GetMembers(BindingFlags.Instance | BindingFlags.Public)
                .Where(member => member.MemberType == MemberTypes.Field || member.MemberType == MemberTypes.Property)
                .Select(member => member.Name)
                .ToArray();

            Assert.That(memberNames, Does.Not.Contain("MotionMode"));
            Assert.That(memberNames, Does.Not.Contain("UnitKinematicState"));
            Assert.That(memberNames, Does.Not.Contain("PlayerControlState"));
        }

        [Test]
        [Category("Extended")]
        public void GeneratedTestSceneResidueGuard_NoRootInitTestScenes()
        {
            var residue = Directory.Exists("Assets")
                ? Directory.GetFiles("Assets", "InitTestScene*.unity", SearchOption.TopDirectoryOnly)
                    .Concat(Directory.GetFiles("Assets", "InitTestScene*.unity.meta", SearchOption.TopDirectoryOnly))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray()
                : Array.Empty<string>();

            Assert.That(residue, Is.Empty, "Generated InitTestScene residue must be cleaned by the test runner lifecycle.");
        }

        private static ForbiddenToken ForbiddenExact(string token)
        {
            return new ForbiddenToken(
                token,
                $@"(?<![A-Za-z0-9_]){Regex.Escape(token)}(?![A-Za-z0-9_])");
        }

        private static ForbiddenToken ForbiddenPattern(string label, string pattern)
        {
            return new ForbiddenToken(label, pattern);
        }

        private static void AssertNoForbiddenActiveTokens(params ForbiddenToken[] tokens)
        {
            foreach (var filePath in EnumerateActiveGameplayGuardFiles())
            {
                var source = File.ReadAllText(filePath);
                for (var i = 0; i < tokens.Length; i++)
                {
                    var token = tokens[i];
                    Assert.That(
                        Regex.IsMatch(source, token.Pattern),
                        Is.False,
                        $"Retired Player locomotion token '{token.Label}' was found in active file '{filePath}'.");
                }
            }
        }

        private static IEnumerable<string> EnumerateActiveGameplayGuardFiles()
        {
            foreach (var filePath in EnumerateFilesIfPresent("Assets/_Features/Gameplay"))
            {
                yield return filePath;
            }

            foreach (var filePath in EnumerateFilesIfPresent("Assets/_Features/Stages"))
            {
                yield return filePath;
            }

            if (File.Exists("run_tests.sh"))
            {
                yield return "run_tests.sh";
            }

            foreach (var filePath in EnumerateFilesIfPresent(".github"))
            {
                yield return filePath;
            }
        }

        private static IEnumerable<string> EnumerateFilesIfPresent(string root)
        {
            if (!Directory.Exists(root))
            {
                yield break;
            }

            foreach (var filePath in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                if (IsGuardedFileExtension(Path.GetExtension(filePath)))
                {
                    yield return filePath;
                }
            }
        }

        private static bool IsGuardedFileExtension(string extension)
        {
            switch (extension)
            {
                case ".cs":
                case ".asmdef":
                case ".asmref":
                case ".asset":
                case ".prefab":
                case ".unity":
                case ".json":
                case ".md":
                case ".txt":
                case ".csv":
                case ".meta":
                case ".sh":
                case ".yml":
                case ".yaml":
                    return true;
                default:
                    return false;
            }
        }

        private static void AssertTypeDoesNotExposeMember(Type type, string forbiddenMemberName)
        {
            var members = type
                .GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(member => member.Name)
                .ToArray();

            Assert.That(members, Does.Not.Contain(forbiddenMemberName));
        }

        private readonly struct ForbiddenToken
        {
            public ForbiddenToken(string label, string pattern)
            {
                Label = label;
                Pattern = pattern;
            }

            public string Label { get; }

            public string Pattern { get; }
        }
    }
}
