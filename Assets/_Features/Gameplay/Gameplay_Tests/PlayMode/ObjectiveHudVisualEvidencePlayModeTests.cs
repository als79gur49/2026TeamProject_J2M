#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Feature.Gameplay.Tests.PlayMode
{
    public sealed class ObjectiveHudVisualEvidencePlayModeTests
    {
        [Category("Full")]
        [UnityTest]
        public IEnumerator CaptureScreenSpaceOverlayEvidenceAfterSettledFrames()
        {
            var utilityType = Type.GetType(
                "Game.Feature.UI.Tests.ObjectiveHudVisualEvidenceUtility, Game.Feature.UI.Tests",
                throwOnError: false);
            Assert.That(
                utilityType,
                Is.Not.Null,
                "Objective HUD visual evidence utility assembly was not loaded.");

            var method = utilityType.GetMethod(
                "CaptureOverlayFromPlayMode",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(
                method,
                Is.Not.Null,
                "Objective HUD overlay capture entry point was not found.");

            var routine = method.Invoke(null, null) as IEnumerator;
            Assert.That(routine, Is.Not.Null);
            while (routine.MoveNext())
            {
                yield return routine.Current;
            }
        }
    }
}
#endif
