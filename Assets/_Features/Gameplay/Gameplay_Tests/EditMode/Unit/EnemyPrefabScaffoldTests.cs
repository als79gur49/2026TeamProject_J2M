using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class EnemyPrefabScaffoldTests
    {
        [Test]
        public void EnemyViewNonAttackingPrefab_UsesMoveOnlyLocomotionAuthoringAlongsideEnemyAnimationTiming()
        {
            var prefabText = ReadNormalizedText("Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyView_NonAttacking.prefab");

            StringAssert.Contains("UnitLocomotionPresentationAuthoring", prefabText);
            StringAssert.Contains("moveMotionDurationSeconds: -1", prefabText);
            StringAssert.Contains("EnemyAnimationTimingAuthoring", prefabText);
            StringAssert.DoesNotContain("EntityMotionPresentationAuthoring", prefabText);
            StringAssert.DoesNotContain("pushMotionDurationSeconds", prefabText);
            StringAssert.DoesNotContain("flipMotionDurationSeconds", prefabText);
        }

        [Test]
        public void CombinedGameplayShowcaseEnemyPrefab_UsesMoveOnlyLocomotionAuthoringAlongsideEnemyAnimationTiming()
        {
            var prefabText = ReadNormalizedText("Assets/_Features/Stages/Stage_CombinedGameplayShowcase/Enemy/Prefabs/EnemyView_WindupMelee.prefab");

            StringAssert.Contains("UnitLocomotionPresentationAuthoring", prefabText);
            StringAssert.Contains("moveMotionDurationSeconds: -1", prefabText);
            StringAssert.Contains("EnemyAnimationTimingAuthoring", prefabText);
            StringAssert.Contains("attackWindupReferenceClip:", prefabText);
            StringAssert.Contains("recoverReferenceClip:", prefabText);
            StringAssert.DoesNotContain("EntityMotionPresentationAuthoring", prefabText);
            StringAssert.DoesNotContain("pushMotionDurationSeconds", prefabText);
            StringAssert.DoesNotContain("flipMotionDurationSeconds", prefabText);
        }

        [Test]
        public void EnemyViewAttackingPrefab_BindsExplicitEnemyTimingReferenceClips()
        {
            var prefabText = ReadNormalizedText("Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyView_Attacking.prefab");

            StringAssert.Contains("EnemyAnimationTimingAuthoring", prefabText);
            StringAssert.Contains("attackWindupAnimatorDurationSeconds: 1", prefabText);
            StringAssert.Contains("recoverAnimatorDurationSeconds: 1", prefabText);
            StringAssert.Contains("attackWindupReferenceClip:", prefabText);
            StringAssert.Contains("recoverReferenceClip:", prefabText);
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
