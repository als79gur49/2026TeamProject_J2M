using System.Collections;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    public sealed class CampaignLaunchHandoffPlayModeTests
    {
        [SetUp]
        public void SetUp()
        {
            CampaignLaunchHandoffSessionStore.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            CampaignLaunchHandoffSessionStore.ResetForTests();
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator PendingHandoff_SurvivesRequiredCrossSceneLifetime()
        {
            var originalScene = SceneManager.GetActiveScene();
            var firstScene = SceneManager.CreateScene("CampaignHandoffSmoke_First");
            var secondScene = default(Scene);
            SceneManager.SetActiveScene(firstScene);
            var store = CampaignLaunchHandoffSessionStore.Instance;
            Assert.That(
                store.TryBegin(
                    1,
                    StageId.CreateOrThrow("stage-0-1"),
                    StageNavigationKind.Continue,
                    "playmode-cross-scene",
                    out var accepted),
                Is.True);

            secondScene = SceneManager.CreateScene("CampaignHandoffSmoke_Second");
            SceneManager.SetActiveScene(secondScene);
            yield return null;
            var unloadFirst = SceneManager.UnloadSceneAsync(firstScene);
            if (unloadFirst != null)
            {
                yield return unloadFirst;
            }

            var survived = store.TryPeek(out var afterSceneChange) &&
                           object.ReferenceEquals(afterSceneChange, accepted);

            if (originalScene.IsValid() && originalScene.isLoaded)
            {
                SceneManager.SetActiveScene(originalScene);
            }

            var unloadSecond = SceneManager.UnloadSceneAsync(secondScene);
            if (unloadSecond != null)
            {
                yield return unloadSecond;
            }

            Assert.That(survived, Is.True);
        }

        [UnityTest]
        [Category("Full")]
        public IEnumerator SubsystemRegistrationReset_DoesNotRestorePendingHandoff()
        {
            var store = CampaignLaunchHandoffSessionStore.Instance;
            Assert.That(
                store.TryBegin(
                    2,
                    StageId.CreateOrThrow("stage-1-1"),
                    StageNavigationKind.Continue,
                    "playmode-session-reset",
                    out _),
                Is.True);
            yield return null;

            CampaignLaunchHandoffSessionStore.ResetForTests();
            yield return null;

            Assert.That(store.TryPeek(out _), Is.False);
        }
    }
}
