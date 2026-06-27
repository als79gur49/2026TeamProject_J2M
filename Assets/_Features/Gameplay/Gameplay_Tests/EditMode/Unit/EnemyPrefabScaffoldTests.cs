using System.IO;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class EnemyPrefabScaffoldTests
    {
        [Test]
        [Category("Full")]
        public void EnemyViewNonAttackingPrefab_UsesMoveOnlyLocomotionAuthoringAlongsideEnemyAnimationTiming()
        {
            var prefabText = ReadNormalizedText("Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyView_NonAttacking.prefab");

            StringAssert.Contains("UnitLocomotionPresentationAuthoring", prefabText);
            StringAssert.Contains("moveMotionDurationSeconds: 1", prefabText);
            StringAssert.Contains("EnemyAnimationTimingAuthoring", prefabText);
            StringAssert.DoesNotContain("EntityMotionPresentationAuthoring", prefabText);
            StringAssert.DoesNotContain("pushMotionDurationSeconds", prefabText);
            StringAssert.DoesNotContain("flipMotionDurationSeconds", prefabText);
        }

        [Test]
        [Category("Full")]
        public void EnemyViewAttackingPrefab_BindsExplicitEnemyTimingReferenceClips()
        {
            var prefabText = ReadNormalizedText("Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyView_Attacking.prefab");

            StringAssert.Contains("EnemyAnimationTimingAuthoring", prefabText);
            StringAssert.Contains("attackWindupAnimatorDurationSeconds: 1", prefabText);
            StringAssert.Contains("recoverAnimatorDurationSeconds: 1", prefabText);
            StringAssert.Contains("attackWindupReferenceClip:", prefabText);
            StringAssert.Contains("recoverReferenceClip:", prefabText);
        }

        [Test]
        [Category("Full")]
        public void EnemyViewChargePrefab_BindsGenericMoveAuthoringWithoutChargeMoveRuntimeField()
        {
            var prefabText = ReadNormalizedText(StageContentPaths.SharedEnemyPresentationRoot + "/Prefabs/EnemyView_RocketFace.prefab");

            StringAssert.Contains("UnitLocomotionPresentationAuthoring", prefabText);
            StringAssert.Contains("moveMotionDurationSeconds: 0.85", prefabText);
            StringAssert.Contains("EntityMotionPresentationAuthoring", prefabText);
            StringAssert.Contains(
                "attackWindupReferenceClip: {fileID: 3060872287085348379, guid: 1716406119d8be34d841f0d4eb033a2c, type: 3}",
                prefabText);
            StringAssert.Contains(
                "recoverReferenceClip: {fileID: -5059006814000262888, guid: 1716406119d8be34d841f0d4eb033a2c, type: 3}",
                prefabText);
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
            StringAssert.Contains("EnemyAnimationTimingAuthoring", prefabText);
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
            StringAssert.Contains("jumpWindupAnimatorDurationSeconds: 0.5", prefabText);
            StringAssert.Contains("jumpAirborneAnimatorDurationSeconds: 1.35", prefabText);
            StringAssert.Contains("jumpWindupReferenceClip: {fileID: 9067093048684652814", prefabText);
            StringAssert.Contains("jumpAirborneReferenceClip: {fileID: -5560811472042823391", prefabText);
            StringAssert.Contains("m_ApplyRootMotion: 0", prefabText);
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
