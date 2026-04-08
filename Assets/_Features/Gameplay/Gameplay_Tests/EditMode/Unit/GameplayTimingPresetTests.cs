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
        public void GameplaySimulationTimingPreset_ApplyTo_MatchesLegacyConfigurationAndClonesPlayerControlTiming()
        {
            var sourceTiming = new PlayerControlTimingSettings
            {
                MoveCooldownSeconds = 0.5f,
                PushContactThresholdSeconds = 0.2f,
                PushExecuteDelaySeconds = 0.1f,
                PushInputLockDurationSeconds = 0.15f,
                FlipExecuteDelaySeconds = 0.12f,
                FlipInputLockDurationSeconds = 0.18f,
            };
            var preset = CreateSimulationTimingPreset(
                initialMoveDelaySeconds: 0f,
                playerControlTiming: sourceTiming,
                repeatedMoveIntervalSeconds: 0.6f,
                boxSlideStepIntervalSeconds: 0.2f,
                projectileStepIntervalSeconds: 0.2f);

            try
            {
                var legacyConfiguration = new GameplaySceneHostConfiguration
                {
                    InitialMoveDelaySeconds = 0f,
                    PlayerControlTiming = sourceTiming.Clone(),
                    RepeatedMoveIntervalSeconds = 0.6f,
                    BoxSlideStepIntervalSeconds = 0.2f,
                    ProjectileStepIntervalSeconds = 0.2f,
                };
                var presetConfiguration = new GameplaySceneHostConfiguration();

                preset.ApplyTo(presetConfiguration);

                Assert.That(presetConfiguration.PlayerControlTiming, Is.Not.SameAs(sourceTiming));
                Assert.That(presetConfiguration.PlayerControlTiming.MoveCooldownSeconds, Is.EqualTo(sourceTiming.MoveCooldownSeconds));

                var legacyTimingProfile = legacyConfiguration.CreateTimingProfile();
                var presetTimingProfile = presetConfiguration.CreateTimingProfile();
                Assert.That(presetTimingProfile.InitialMoveDelaySeconds, Is.EqualTo(legacyTimingProfile.InitialMoveDelaySeconds));
                Assert.That(presetTimingProfile.RepeatedMoveIntervalSeconds, Is.EqualTo(legacyTimingProfile.RepeatedMoveIntervalSeconds));
                Assert.That(presetTimingProfile.BoxSlideStepIntervalSeconds, Is.EqualTo(legacyTimingProfile.BoxSlideStepIntervalSeconds));
                Assert.That(presetTimingProfile.ProjectileStepIntervalSeconds, Is.EqualTo(legacyTimingProfile.ProjectileStepIntervalSeconds));

                var legacySnapshot = legacyConfiguration.CreatePlayerControlTimingSnapshot();
                var presetSnapshot = presetConfiguration.CreatePlayerControlTimingSnapshot();
                AssertPlayerControlSnapshotsEqual(legacySnapshot, presetSnapshot);

                presetConfiguration.PlayerControlTiming.MoveCooldownSeconds = 9f;
                Assert.That(sourceTiming.MoveCooldownSeconds, Is.EqualTo(0.5f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preset);
            }
        }

        [Test]
        public void GameplayPresentationTimingPreset_ApplyTo_PreservesFallbackSemanticsAgainstLegacyConfiguration()
        {
            var preset = CreatePresentationTimingPreset(
                moveMotionDurationSeconds: -1f,
                pushMotionDurationSeconds: 0.3f,
                flipMotionDurationSeconds: 0.2f,
                topologyMotionDurationSeconds: -1f,
                itemConsumeEffectDurationSeconds: -1f,
                boxDestroyEffectDurationSeconds: -1f);

            try
            {
                var legacyConfiguration = new GameplaySceneHostConfiguration
                {
                    MoveMotionDurationSeconds = -1f,
                    PushMotionDurationSeconds = 0.3f,
                    FlipMotionDurationSeconds = 0.2f,
                    TopologyMotionDurationSeconds = -1f,
                    ItemConsumeEffectDurationSeconds = -1f,
                    BoxDestroyEffectDurationSeconds = -1f,
                };
                var presetConfiguration = new GameplaySceneHostConfiguration();

                preset.ApplyTo(presetConfiguration);

                var legacyTimingProfile = legacyConfiguration.CreateTimingProfile();
                var presetTimingProfile = presetConfiguration.CreateTimingProfile();
                Assert.That(presetTimingProfile.MoveMotionDurationSeconds, Is.EqualTo(legacyTimingProfile.MoveMotionDurationSeconds));
                Assert.That(presetTimingProfile.PushMotionDurationSeconds, Is.EqualTo(legacyTimingProfile.PushMotionDurationSeconds));
                Assert.That(presetTimingProfile.FlipMotionDurationSeconds, Is.EqualTo(legacyTimingProfile.FlipMotionDurationSeconds));
                Assert.That(presetTimingProfile.TopologyMotionDurationSeconds, Is.EqualTo(legacyTimingProfile.TopologyMotionDurationSeconds));
                Assert.That(presetTimingProfile.ItemConsumeEffectDurationSeconds, Is.EqualTo(legacyTimingProfile.ItemConsumeEffectDurationSeconds));
                Assert.That(presetTimingProfile.BoxDestroyEffectDurationSeconds, Is.EqualTo(legacyTimingProfile.BoxDestroyEffectDurationSeconds));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preset);
            }
        }

        [Test]
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
        public void PlayerControlTimingSettings_CreateDefault_UsesPhaseAlignedExecuteAndRecoveryWindows()
        {
            var snapshot = PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                repeatedMoveIntervalSeconds: 0.6f);

            Assert.That(snapshot.PushExecuteDelaySeconds, Is.EqualTo(11f / 60f).Within(0.0001f));
            Assert.That(snapshot.PushInputLockDurationSeconds, Is.EqualTo(29f / 60f).Within(0.0001f));
            Assert.That(snapshot.PushWindupTicks, Is.EqualTo(11));
            Assert.That(snapshot.PushRecoveryTicks, Is.EqualTo(18));
            Assert.That(snapshot.FlipExecuteDelaySeconds, Is.EqualTo(23f / 60f).Within(0.0001f));
            Assert.That(snapshot.FlipInputLockDurationSeconds, Is.EqualTo(57f / 60f).Within(0.0001f));
            Assert.That(snapshot.FlipWindupTicks, Is.EqualTo(23));
            Assert.That(snapshot.FlipRecoveryTicks, Is.EqualTo(34));
        }

        [Test]
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
        }

        private static GameplaySimulationTimingPreset CreateSimulationTimingPreset(
            float initialMoveDelaySeconds = 0f,
            PlayerControlTimingSettings playerControlTiming = null,
            float repeatedMoveIntervalSeconds = 0.6f,
            float boxSlideStepIntervalSeconds = 0.2f,
            float projectileStepIntervalSeconds = 0.2f)
        {
            var preset = ScriptableObject.CreateInstance<GameplaySimulationTimingPreset>();
            SetPrivateField(preset, "initialMoveDelaySeconds", initialMoveDelaySeconds);
            SetPrivateField(preset, "playerControlTiming", playerControlTiming ?? PlayerControlTimingSettings.CreateDefault());
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
            float boxDestroyEffectDurationSeconds = -1f)
        {
            var preset = ScriptableObject.CreateInstance<GameplayPresentationTimingPreset>();
            SetPrivateField(preset, "moveMotionDurationSeconds", moveMotionDurationSeconds);
            SetPrivateField(preset, "pushMotionDurationSeconds", pushMotionDurationSeconds);
            SetPrivateField(preset, "flipMotionDurationSeconds", flipMotionDurationSeconds);
            SetPrivateField(preset, "topologyMotionDurationSeconds", topologyMotionDurationSeconds);
            SetPrivateField(preset, "itemConsumeEffectDurationSeconds", itemConsumeEffectDurationSeconds);
            SetPrivateField(preset, "boxDestroyEffectDurationSeconds", boxDestroyEffectDurationSeconds);
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
            Assert.That(actual.PushContactThresholdSeconds, Is.EqualTo(expected.PushContactThresholdSeconds));
            Assert.That(actual.PushContactThresholdTicks, Is.EqualTo(expected.PushContactThresholdTicks));
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
    }
}
