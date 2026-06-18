using System;
using System.Reflection;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Timing;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayTimingPresetTests
    {
        [Test]
        [Category("Extended")]
        public void GameplaySimulationTimingPreset_ApplyTo_MatchesDirectConfigurationAndClonesPlayerControlTiming()
        {
            var sourceTiming = new PlayerControlTimingSettings
            {
                MoveCooldownSeconds = 0.5f,
                DamageCooldownSeconds = 0.25f,
                PushExecuteDelaySeconds = 0.1f,
                PushInputLockDurationSeconds = 0.15f,
                FlipExecuteDelaySeconds = 0.12f,
                FlipInputLockDurationSeconds = 0.18f,
            };
            var sourceRespawnTiming = new PlayerRespawnTimingSettings
            {
                RespawnDelaySeconds = 0.45f,
            };
            var sourcePlayerFree2DLocomotion = PlayerFree2DLocomotionAuthoring.CreateDefault();
            sourcePlayerFree2DLocomotion.SecondsPerCellAtFullSpeed = 0.4f;
            sourcePlayerFree2DLocomotion.CollisionRadiusCells = 0.125f;
            sourcePlayerFree2DLocomotion.ActionAssistSettleWindowCells = 0.1875f;
            var preset = CreateSimulationTimingPreset(
                initialMoveDelaySeconds: 0f,
                playerControlTiming: sourceTiming,
                playerFree2DLocomotion: sourcePlayerFree2DLocomotion,
                playerRespawnTiming: sourceRespawnTiming,
                repeatedMoveIntervalSeconds: 0.6f,
                boxSlideStepIntervalSeconds: 0.2f,
                projectileStepIntervalSeconds: 0.2f);

            try
            {
                var directConfiguration = new GameplaySceneHostConfiguration
                {
                    InitialMoveDelaySeconds = 0f,
                    PlayerControlTiming = sourceTiming.Clone(),
                    PlayerFree2DLocomotion = sourcePlayerFree2DLocomotion,
                    PlayerRespawnTiming = sourceRespawnTiming.Clone(),
                    RepeatedMoveIntervalSeconds = 0.6f,
                    BoxSlideStepIntervalSeconds = 0.2f,
                    ProjectileStepIntervalSeconds = 0.2f,
                };
                var presetConfiguration = new GameplaySceneHostConfiguration();

                preset.ApplyTo(presetConfiguration);

                Assert.That(presetConfiguration.PlayerControlTiming, Is.Not.SameAs(sourceTiming));
                Assert.That(presetConfiguration.PlayerControlTiming.MoveCooldownSeconds, Is.EqualTo(sourceTiming.MoveCooldownSeconds));
                Assert.That(presetConfiguration.PlayerControlTiming.DamageCooldownSeconds, Is.EqualTo(sourceTiming.DamageCooldownSeconds));
                Assert.That(presetConfiguration.PlayerRespawnTiming, Is.Not.SameAs(sourceRespawnTiming));
                Assert.That(presetConfiguration.PlayerRespawnTiming.RespawnDelaySeconds, Is.EqualTo(sourceRespawnTiming.RespawnDelaySeconds));

                var directTimingProfile = directConfiguration.CreateTimingProfile();
                var presetTimingProfile = presetConfiguration.CreateTimingProfile();
                Assert.That(presetTimingProfile.InitialMoveDelaySeconds, Is.EqualTo(directTimingProfile.InitialMoveDelaySeconds));
                Assert.That(presetTimingProfile.RepeatedMoveIntervalSeconds, Is.EqualTo(directTimingProfile.RepeatedMoveIntervalSeconds));
                Assert.That(presetTimingProfile.BoxSlideStepIntervalSeconds, Is.EqualTo(directTimingProfile.BoxSlideStepIntervalSeconds));
                Assert.That(presetTimingProfile.ProjectileStepIntervalSeconds, Is.EqualTo(directTimingProfile.ProjectileStepIntervalSeconds));

                var directSnapshot = directConfiguration.CreatePlayerControlTimingSnapshot();
                var presetSnapshot = presetConfiguration.CreatePlayerControlTimingSnapshot();
                AssertPlayerControlSnapshotsEqual(directSnapshot, presetSnapshot);
                Assert.That(
                    presetConfiguration.CreatePlayerRespawnTimingSnapshot().RespawnDelayTicks,
                    Is.EqualTo(directConfiguration.CreatePlayerRespawnTimingSnapshot().RespawnDelayTicks));
                AssertPlayerFree2DLocomotionSettingsEqual(
                    directConfiguration.CreatePlayerFree2DLocomotionSettings(),
                    presetConfiguration.CreatePlayerFree2DLocomotionSettings());

                presetConfiguration.PlayerControlTiming.MoveCooldownSeconds = 9f;
                Assert.That(sourceTiming.MoveCooldownSeconds, Is.EqualTo(0.5f));
                presetConfiguration.PlayerRespawnTiming.RespawnDelaySeconds = 9f;
                Assert.That(sourceRespawnTiming.RespawnDelaySeconds, Is.EqualTo(0.45f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preset);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayPresentationTimingPreset_ApplyTo_PreservesFallbackSemanticsAgainstDirectConfiguration()
        {
            var preset = CreatePresentationTimingPreset(
                moveMotionDurationSeconds: -1f,
                pushMotionDurationSeconds: 0.3f,
                flipMotionDurationSeconds: 0.2f,
                topologyMotionDurationSeconds: -1f,
                itemConsumeEffectDurationSeconds: -1f,
                boxDestroyEffectDurationSeconds: -1f,
                enemyDeathEffectDurationSeconds: -1f);

            try
            {
                var directConfiguration = new GameplaySceneHostConfiguration
                {
                    MoveMotionDurationSeconds = -1f,
                    PushMotionDurationSeconds = 0.3f,
                    FlipMotionDurationSeconds = 0.2f,
                    TopologyMotionDurationSeconds = -1f,
                    ItemConsumeEffectDurationSeconds = -1f,
                    BoxDestroyEffectDurationSeconds = -1f,
                    EnemyDeathEffectDurationSeconds = -1f,
                };
                var presetConfiguration = new GameplaySceneHostConfiguration();

                preset.ApplyTo(presetConfiguration);

                var directTimingProfile = directConfiguration.CreateTimingProfile();
                var presetTimingProfile = presetConfiguration.CreateTimingProfile();
                Assert.That(presetTimingProfile.MoveMotionDurationSeconds, Is.EqualTo(directTimingProfile.MoveMotionDurationSeconds));
                Assert.That(presetTimingProfile.PushMotionDurationSeconds, Is.EqualTo(directTimingProfile.PushMotionDurationSeconds));
                Assert.That(presetTimingProfile.FlipMotionDurationSeconds, Is.EqualTo(directTimingProfile.FlipMotionDurationSeconds));
                Assert.That(presetTimingProfile.TopologyMotionDurationSeconds, Is.EqualTo(directTimingProfile.TopologyMotionDurationSeconds));
                Assert.That(presetTimingProfile.ItemConsumeEffectDurationSeconds, Is.EqualTo(directTimingProfile.ItemConsumeEffectDurationSeconds));
                Assert.That(presetTimingProfile.BoxDestroyEffectDurationSeconds, Is.EqualTo(directTimingProfile.BoxDestroyEffectDurationSeconds));
                Assert.That(presetTimingProfile.EnemyDeathEffectDurationSeconds, Is.EqualTo(directTimingProfile.EnemyDeathEffectDurationSeconds));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preset);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplaySimulationTimingPreset_ApplyTo_InvalidInterval_Throws()
        {
            var preset = CreateSimulationTimingPreset(repeatedMoveIntervalSeconds: 0f);

            try
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => preset.ApplyTo(new GameplaySceneHostConfiguration()));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preset);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayPresentationTimingPreset_ApplyTo_InvalidDuration_Throws()
        {
            var preset = CreatePresentationTimingPreset(pushMotionDurationSeconds: 0f);

            try
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => preset.ApplyTo(new GameplaySceneHostConfiguration()));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preset);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplaySimulationTimingPreset_PlayerFree2DTiming_IsIndependentFromUnitKinematicTiming()
        {
            var playerFree2DLocomotion = PlayerFree2DLocomotionAuthoring.CreateDefault();
            playerFree2DLocomotion.SecondsPerCellAtFullSpeed = 0.33333334f;
            var unitKinematicLocomotionTiming = new UnitKinematicLocomotionTimingSettings
            {
                MoveDurationSeconds = 0.5f,
            };
            var preset = CreateSimulationTimingPreset(
                playerFree2DLocomotion: playerFree2DLocomotion,
                unitKinematicLocomotionTiming: unitKinematicLocomotionTiming);

            try
            {
                var configuration = new GameplaySceneHostConfiguration();

                preset.ApplyTo(configuration);

                Assert.That(configuration.CreatePlayerFree2DLocomotionSettings().TicksPerCell, Is.EqualTo(20));
                Assert.That(configuration.CreateUnitKinematicLocomotionTimingSnapshot().TicksPerCell, Is.EqualTo(30));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preset);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2DConfig_NoOverride_DoesNotOverwritePreset()
        {
            var playerFree2DLocomotion = PlayerFree2DLocomotionAuthoring.CreateDefault();
            playerFree2DLocomotion.SecondsPerCellAtFullSpeed = 0.4f;
            playerFree2DLocomotion.CollisionRadiusCells = 0.125f;
            playerFree2DLocomotion.ActionAssistSettleWindowCells = 0.1875f;
            var preset = CreateSimulationTimingPreset(playerFree2DLocomotion: playerFree2DLocomotion);

            try
            {
                var configuration = new GameplaySceneHostConfiguration();

                preset.ApplyTo(configuration);
                var settings = configuration.CreatePlayerFree2DLocomotionSettings();

                Assert.That(configuration.PlayerFree2DLocomotionOverride.OverridesAny, Is.False);
                Assert.That(settings.SecondsPerCellAtFullSpeed, Is.EqualTo(0.4f));
                Assert.That(settings.TicksPerCell, Is.EqualTo(24));
                Assert.That(settings.CollisionRadiusUnits, Is.EqualTo(512));
                Assert.That(settings.ActionAssistSettleWindowUnits, Is.EqualTo(768));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preset);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2DConfig_PresetApply_ClearsStaleOverride()
        {
            var playerFree2DLocomotion = PlayerFree2DLocomotionAuthoring.CreateDefault();
            playerFree2DLocomotion.CollisionRadiusCells = 0.125f;
            var preset = CreateSimulationTimingPreset(playerFree2DLocomotion: playerFree2DLocomotion);

            try
            {
                var configuration = new GameplaySceneHostConfiguration
                {
                    PlayerFree2DLocomotionOverride =
                        PlayerFree2DLocomotionOverride.CreateCollisionAndActionAssist(
                            collisionRadiusCells: 0f,
                            actionAssistSettleWindowCells: 0f),
                };

                preset.ApplyTo(configuration);
                var settings = configuration.CreatePlayerFree2DLocomotionSettings();

                Assert.That(configuration.PlayerFree2DLocomotionOverride.OverridesAny, Is.False);
                Assert.That(settings.CollisionRadiusUnits, Is.EqualTo(512));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preset);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2DConfig_PartialOverride_ChangesOnlySelectedFields()
        {
            var playerFree2DLocomotion = PlayerFree2DLocomotionAuthoring.CreateDefault();
            playerFree2DLocomotion.SecondsPerCellAtFullSpeed = 0.4f;
            playerFree2DLocomotion.CollisionRadiusCells = 0.125f;
            playerFree2DLocomotion.ActionAssistSettleWindowCells = 0.1875f;
            var preset = CreateSimulationTimingPreset(playerFree2DLocomotion: playerFree2DLocomotion);

            try
            {
                var configuration = new GameplaySceneHostConfiguration();

                preset.ApplyTo(configuration);
                configuration.PlayerFree2DLocomotionOverride =
                    PlayerFree2DLocomotionOverride.CreateCollisionAndActionAssist(
                        collisionRadiusCells: 0f,
                        actionAssistSettleWindowCells: 0.0625f);
                var settings = configuration.CreatePlayerFree2DLocomotionSettings();

                Assert.That(settings.SecondsPerCellAtFullSpeed, Is.EqualTo(0.4f));
                Assert.That(settings.TicksPerCell, Is.EqualTo(24));
                Assert.That(settings.CollisionRadiusUnits, Is.EqualTo(0));
                Assert.That(settings.ActionAssistSettleWindowUnits, Is.EqualTo(256));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preset);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2DConfig_ExplicitDefaultValuedOverride_IsStillOverride()
        {
            var playerFree2DLocomotion = PlayerFree2DLocomotionAuthoring.CreateDefault();
            playerFree2DLocomotion.CollisionRadiusCells = 0.125f;
            playerFree2DLocomotion.ActionAssistSettleWindowCells = 0.1875f;
            var preset = CreateSimulationTimingPreset(playerFree2DLocomotion: playerFree2DLocomotion);

            try
            {
                var configuration = new GameplaySceneHostConfiguration();

                preset.ApplyTo(configuration);
                configuration.PlayerFree2DLocomotionOverride = PlayerFree2DLocomotionOverride.Create(
                    overrideSecondsPerCellAtFullSpeed: false,
                    secondsPerCellAtFullSpeed: 0f,
                    overrideCollisionRadiusCells: true,
                    collisionRadiusCells: 0f,
                    overrideActionAssistSettleWindowCells: true,
                    actionAssistSettleWindowCells: 0.125f);
                var settings = configuration.CreatePlayerFree2DLocomotionSettings();

                Assert.That(configuration.PlayerFree2DLocomotionOverride.OverridesAny, Is.True);
                Assert.That(settings.CollisionRadiusUnits, Is.EqualTo(0));
                Assert.That(settings.ActionAssistSettleWindowUnits, Is.EqualTo(512));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preset);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerControlTimingSettings_CreateDefault_UsesPhaseAlignedExecuteAndRecoveryWindows()
        {
            var snapshot = PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                repeatedMoveIntervalSeconds: 0.6f);

            Assert.That(snapshot.PushExecuteDelaySeconds, Is.EqualTo(11f / 60f).Within(0.0001f));
            Assert.That(snapshot.PushInputLockDurationSeconds, Is.EqualTo(29f / 60f).Within(0.0001f));
            Assert.That(snapshot.PushWindupTicks, Is.EqualTo(11));
            Assert.That(snapshot.PushRecoveryTicks, Is.EqualTo(18));
            Assert.That(snapshot.DamageCooldownSeconds, Is.EqualTo(1f / 60f).Within(0.0001f));
            Assert.That(snapshot.DamageCooldownTicks, Is.EqualTo(1));
            Assert.That(snapshot.FlipExecuteDelaySeconds, Is.EqualTo(23f / 60f).Within(0.0001f));
            Assert.That(snapshot.FlipInputLockDurationSeconds, Is.EqualTo(57f / 60f).Within(0.0001f));
            Assert.That(snapshot.FlipWindupTicks, Is.EqualTo(23));
            Assert.That(snapshot.FlipRecoveryTicks, Is.EqualTo(34));
        }

        [Test]
        [Category("Full")]
        public void GameplaySimulationTimingPreset_DefaultShowcase_UsesPlayerPhaseTimingWindows()
        {
            const string presetPath =
                "Assets/_Features/Gameplay/Gameplay_Timing/Showcase/GameplaySimulationTimingPreset_DefaultShowcase.asset";

            var preset = AssetDatabase.LoadAssetAtPath<GameplaySimulationTimingPreset>(presetPath);

            Assert.That(preset, Is.Not.Null, $"Missing preset at '{presetPath}'.");

            var configuration = new GameplaySceneHostConfiguration();
            preset.ApplyTo(configuration);

            Assert.That(configuration.PlayerControlTiming.PushExecuteDelaySeconds, Is.EqualTo(0.18333334f).Within(0.0000001f));
            Assert.That(configuration.PlayerControlTiming.PushInputLockDurationSeconds, Is.EqualTo(0.48333335f).Within(0.0000001f));
            Assert.That(configuration.PlayerControlTiming.FlipExecuteDelaySeconds, Is.EqualTo(0.38333333f).Within(0.0000001f));
            Assert.That(configuration.PlayerControlTiming.FlipInputLockDurationSeconds, Is.EqualTo(0.95f).Within(0.0000001f));
            Assert.That(
                configuration.CreateUnitKinematicLocomotionTimingSnapshot().TicksPerCell,
                Is.EqualTo(20));
            Assert.That(
                configuration.CreatePlayerFree2DLocomotionSettings().SecondsPerCellAtFullSpeed,
                Is.EqualTo(0.33333334f));
            Assert.That(
                configuration.CreatePlayerFree2DLocomotionSettings().TicksPerCell,
                Is.EqualTo(20));
            Assert.That(
                configuration.CreatePlayerFree2DLocomotionSettings().SpeedUnitsPerTick,
                Is.EqualTo(204));
        }

        private static GameplaySimulationTimingPreset CreateSimulationTimingPreset(
            float initialMoveDelaySeconds = 0f,
            PlayerControlTimingSettings playerControlTiming = null,
            UnitKinematicLocomotionTimingSettings unitKinematicLocomotionTiming = null,
            PlayerFree2DLocomotionAuthoring? playerFree2DLocomotion = null,
            PlayerRespawnTimingSettings playerRespawnTiming = null,
            float repeatedMoveIntervalSeconds = 0.6f,
            float boxSlideStepIntervalSeconds = 0.2f,
            float projectileStepIntervalSeconds = 0.2f)
        {
            var preset = ScriptableObject.CreateInstance<GameplaySimulationTimingPreset>();
            SetPrivateField(preset, "initialMoveDelaySeconds", initialMoveDelaySeconds);
            SetPrivateField(preset, "playerControlTiming", playerControlTiming ?? PlayerControlTimingSettings.CreateDefault());
            SetPrivateField(preset, "unitKinematicLocomotionTiming", unitKinematicLocomotionTiming ?? UnitKinematicLocomotionTimingSettings.CreateDefault());
            SetPrivateField(preset, "playerFree2DLocomotion", playerFree2DLocomotion ?? PlayerFree2DLocomotionAuthoring.CreateDefault());
            SetPrivateField(preset, "playerRespawnTiming", playerRespawnTiming ?? PlayerRespawnTimingSettings.CreateDefault());
            SetPrivateField(preset, "repeatedMoveIntervalSeconds", repeatedMoveIntervalSeconds);
            SetPrivateField(preset, "boxSlideStepIntervalSeconds", boxSlideStepIntervalSeconds);
            SetPrivateField(preset, "projectileStepIntervalSeconds", projectileStepIntervalSeconds);
            return preset;
        }

        private static GameplayPresentationTimingPreset CreatePresentationTimingPreset(
            float moveMotionDurationSeconds = -1f,
            float pushMotionDurationSeconds = 1f,
            float flipMotionDurationSeconds = 1f,
            float topologyMotionDurationSeconds = -1f,
            float itemConsumeEffectDurationSeconds = -1f,
            float boxDestroyEffectDurationSeconds = -1f,
            float enemyDeathEffectDurationSeconds = -1f)
        {
            var preset = ScriptableObject.CreateInstance<GameplayPresentationTimingPreset>();
            SetPrivateField(preset, "moveMotionDurationSeconds", moveMotionDurationSeconds);
            SetPrivateField(preset, "pushMotionDurationSeconds", pushMotionDurationSeconds);
            SetPrivateField(preset, "flipMotionDurationSeconds", flipMotionDurationSeconds);
            SetPrivateField(preset, "topologyMotionDurationSeconds", topologyMotionDurationSeconds);
            SetPrivateField(preset, "itemConsumeEffectDurationSeconds", itemConsumeEffectDurationSeconds);
            SetPrivateField(preset, "boxDestroyEffectDurationSeconds", boxDestroyEffectDurationSeconds);
            SetPrivateField(preset, "enemyDeathEffectDurationSeconds", enemyDeathEffectDurationSeconds);
            return preset;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void AssertPlayerControlSnapshotsEqual(
            PlayerControlTimingAuthoritativeSnapshot expected,
            PlayerControlTimingAuthoritativeSnapshot actual)
        {
            Assert.That(actual.MoveCooldownSeconds, Is.EqualTo(expected.MoveCooldownSeconds));
            Assert.That(actual.MoveCooldownTicks, Is.EqualTo(expected.MoveCooldownTicks));
            Assert.That(actual.DamageCooldownSeconds, Is.EqualTo(expected.DamageCooldownSeconds));
            Assert.That(actual.DamageCooldownTicks, Is.EqualTo(expected.DamageCooldownTicks));
            Assert.That(actual.PushExecuteDelaySeconds, Is.EqualTo(expected.PushExecuteDelaySeconds));
            Assert.That(actual.PushExecuteDelayTicks, Is.EqualTo(expected.PushExecuteDelayTicks));
            Assert.That(actual.PushInputLockDurationSeconds, Is.EqualTo(expected.PushInputLockDurationSeconds));
            Assert.That(actual.PushInputLockDurationTicks, Is.EqualTo(expected.PushInputLockDurationTicks));
            Assert.That(actual.PushWindupTicks, Is.EqualTo(expected.PushWindupTicks));
            Assert.That(actual.PushRecoveryTicks, Is.EqualTo(expected.PushRecoveryTicks));
            Assert.That(actual.FlipExecuteDelaySeconds, Is.EqualTo(expected.FlipExecuteDelaySeconds));
            Assert.That(actual.FlipExecuteDelayTicks, Is.EqualTo(expected.FlipExecuteDelayTicks));
            Assert.That(actual.FlipInputLockDurationSeconds, Is.EqualTo(expected.FlipInputLockDurationSeconds));
            Assert.That(actual.FlipInputLockDurationTicks, Is.EqualTo(expected.FlipInputLockDurationTicks));
            Assert.That(actual.FlipWindupTicks, Is.EqualTo(expected.FlipWindupTicks));
            Assert.That(actual.FlipRecoveryTicks, Is.EqualTo(expected.FlipRecoveryTicks));
        }

        private static void AssertPlayerFree2DLocomotionSettingsEqual(
            PlayerFree2DLocomotionSettings expected,
            PlayerFree2DLocomotionSettings actual)
        {
            Assert.That(actual.SecondsPerCellAtFullSpeed, Is.EqualTo(expected.SecondsPerCellAtFullSpeed));
            Assert.That(actual.TicksPerCell, Is.EqualTo(expected.TicksPerCell));
            Assert.That(actual.SpeedUnitsPerTick, Is.EqualTo(expected.SpeedUnitsPerTick));
            Assert.That(actual.UnitsPerTickRemainder, Is.EqualTo(expected.UnitsPerTickRemainder));
            Assert.That(actual.CollisionRadiusUnits, Is.EqualTo(expected.CollisionRadiusUnits));
            Assert.That(actual.ActionAssistSettleWindowUnits, Is.EqualTo(expected.ActionAssistSettleWindowUnits));
        }
    }
}
