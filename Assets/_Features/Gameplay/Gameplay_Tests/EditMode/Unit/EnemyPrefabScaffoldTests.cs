using System.IO;
using Game.Feature.Gameplay.Host;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class EnemyPrefabScaffoldTests
    {
        [Test]
        [Category("Full")]
        public void EnemyViewChargePrefab_BindsGenericMoveAuthoringWithoutLegacyChargeEntityMotionRuntimeField()
        {
            var prefabPath = StageContentPaths.SharedEnemyPresentationRoot + "/Prefabs/EnemyView_RocketFace.prefab";
            var prefabText = ReadNormalizedText(prefabPath);
            var snapshot = LoadBinding(prefabPath);

            StringAssert.Contains("UnitLocomotionPresentationAuthoring", prefabText);
            StringAssert.Contains("moveMotionDurationSeconds: 0.85", prefabText);
            StringAssert.Contains("EntityMotionPresentationAuthoring", prefabText);
            AssertBinding(snapshot, EnemyAnimationCue.ChargeWindup,
                EnemyAnimationDispatchMode.State, "Windup", 0.6f);
            Assert.That(snapshot.TryGetBinding(EnemyAnimationCue.ChargeWindup, out var windupBinding), Is.True);
            var windupClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/3DM/7RF/Windup_Fix.anim");
            Assert.That(windupClip, Is.Not.Null);
            Assert.That(windupBinding.ReferenceClip, Is.SameAs(windupClip));
            Assert.That(windupClip.length, Is.EqualTo(0.6f).Within(0.0001f));
            AssertBinding(snapshot, EnemyAnimationCue.ChargeActive,
                EnemyAnimationDispatchMode.State, "Charge", -1f);
            AssertBinding(snapshot, EnemyAnimationCue.ChargeRecovery,
                EnemyAnimationDispatchMode.State, "Recover", 0.4f);
        }

        [Test]
        [Category("Full")]
        public void EnemyAnimatorChargeController_DrivesChargePhasesFromChargePhaseParameter()
        {
            var controllerText = ReadNormalizedText("Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyAnimator_Charge.controller");

            StringAssert.Contains(
                "m_ConditionEvent: EnemyChargePhase\n    m_EventTreshold: 1\n  m_DstStateMachine: {fileID: 0}\n  m_DstState: {fileID: -5566630273135047605}",
                controllerText);
            StringAssert.Contains(
                "m_ConditionEvent: EnemyChargePhase\n    m_EventTreshold: 2\n  m_DstStateMachine: {fileID: 0}\n  m_DstState: {fileID: 700200000000000001}",
                controllerText);
            StringAssert.Contains(
                "m_ConditionEvent: EnemyChargePhase\n    m_EventTreshold: 3\n  m_DstStateMachine: {fileID: 0}\n  m_DstState: {fileID: 3792528062960070313}",
                controllerText);
            StringAssert.DoesNotContain(
                "m_ConditionEvent: EnemyAiMode\n    m_EventTreshold: 3\n  m_DstStateMachine: {fileID: 0}\n  m_DstState: {fileID: -5566630273135047605}",
                controllerText);
        }

        [Test]
        [Category("Full")]
        public void EnemyViewWallFollowerSunPrefab_BindsSunModelAndAnimationController()
        {
            var prefabText = ReadNormalizedText(StageContentPaths.SharedEnemyPresentationRoot + "/Prefabs/EnemyView_Sunwheel.prefab");

            StringAssert.Contains("EnemyView_Sunwheel", prefabText);
            StringAssert.Contains("guid: bbf461ea260c63b4785c6c2c72a480bf", prefabText);
            StringAssert.Contains("guid: 506850279d08adc3d62015a52b2109e4", prefabText);
            StringAssert.Contains("UnitLocomotionPresentationAuthoring", prefabText);
            AssertBinding(LoadBinding(
                    StageContentPaths.SharedEnemyPresentationRoot + "/Prefabs/EnemyView_Sunwheel.prefab"),
                EnemyAnimationCue.Hit, EnemyAnimationDispatchMode.Trigger, "Hit", -1f);
            StringAssert.Contains("m_ApplyRootMotion: 0", prefabText);
        }

        [Test]
        [Category("Full")]
        public void EnemyViewJumpChaserAstraPrefab_BindsAstraModelAndJumpTimingClips()
        {
            var prefabText = ReadNormalizedText(StageContentPaths.SharedEnemyPresentationRoot + "/Prefabs/EnemyView_Astreton.prefab");

            StringAssert.Contains("EnemyView_Astreton", prefabText);
            StringAssert.Contains("guid: 9870afb7c6d615c458c88e0e341207e3", prefabText);
            StringAssert.Contains("guid: 6be19350b5cac6763aebb3b88b54c091", prefabText);
            var snapshot = LoadBinding(
                StageContentPaths.SharedEnemyPresentationRoot + "/Prefabs/EnemyView_Astreton.prefab");
            AssertBinding(snapshot, EnemyAnimationCue.JumpWindup,
                EnemyAnimationDispatchMode.State, "JumpWindup", 0.5f);
            AssertBinding(snapshot, EnemyAnimationCue.JumpAirborne,
                EnemyAnimationDispatchMode.State, "JumpAirborne", 1.35f);
            AssertBinding(snapshot, EnemyAnimationCue.JumpLanding,
                EnemyAnimationDispatchMode.State, "Move", -1f);
            StringAssert.Contains("m_ApplyRootMotion: 0", prefabText);
        }

        private static EnemyAnimationBindingSnapshot LoadBinding(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);
            Assert.That(prefab.GetComponent<EnemyAnimationTimingAuthoring>(), Is.Null, prefabPath);
            var authoring = prefab.GetComponent<EnemyAnimationBindingAuthoring>();
            Assert.That(authoring, Is.Not.Null, prefabPath);
            return authoring.CreateSnapshot();
        }

        private static void AssertBinding(
            EnemyAnimationBindingSnapshot snapshot,
            EnemyAnimationCue cue,
            EnemyAnimationDispatchMode mode,
            string target,
            float duration)
        {
            Assert.That(snapshot.TryGetBinding(cue, out var binding), Is.True, cue.ToString());
            Assert.That(binding.PrimaryDispatchMode, Is.EqualTo(mode), cue.ToString());
            Assert.That(binding.TargetName, Is.EqualTo(target), cue.ToString());
            Assert.That(binding.AnimatorDurationSeconds, Is.EqualTo(duration).Within(0.0001f), cue.ToString());
        }

        private static string ReadNormalizedText(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            var normalizedAssetPath = assetPath.Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(projectRoot, normalizedAssetPath);
            return File.ReadAllText(fullPath).Replace("\r\n", "\n");
        }
    }
}
