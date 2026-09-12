using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class EnemyAnimationCueCatalogTests
    {
        [Test]
        [Category("Core")]
        public void CueNumericValues_AreStableAndSparse()
        {
            var expected = new Dictionary<EnemyAnimationCue, int>
            {
                [EnemyAnimationCue.None] = 0,
                [EnemyAnimationCue.ActionWindup] = 10,
                [EnemyAnimationCue.ActionExecute] = 11,
                [EnemyAnimationCue.ActionRecovery] = 12,
                [EnemyAnimationCue.JumpWindup] = 20,
                [EnemyAnimationCue.JumpAirborne] = 21,
                [EnemyAnimationCue.JumpLanding] = 22,
                [EnemyAnimationCue.ChargeWindup] = 30,
                [EnemyAnimationCue.ChargeActive] = 31,
                [EnemyAnimationCue.ChargeRecovery] = 32,
                [EnemyAnimationCue.GlideWindup] = 40,
                [EnemyAnimationCue.GlideActive] = 41,
                [EnemyAnimationCue.GlideRecovery] = 42,
                [EnemyAnimationCue.UtilityWindup] = 50,
                [EnemyAnimationCue.UtilityRecovery] = 51,
                [EnemyAnimationCue.Hit] = 90,
                [EnemyAnimationCue.Death] = 91,
            };

            foreach (var pair in expected)
            {
                Assert.That((int)pair.Key, Is.EqualTo(pair.Value), pair.Key.ToString());
            }
        }

        [Test]
        [Category("Core")]
        public void Catalog_HasExactlyOneEntryForEverySupportedCue()
        {
            var supportedCues = Enum.GetValues(typeof(EnemyAnimationCue))
                .Cast<EnemyAnimationCue>()
                .Where(cue => cue != EnemyAnimationCue.None)
                .ToArray();

            Assert.That(EnemyAnimationCueCatalog.All.Select(metadata => metadata.Cue),
                Is.EquivalentTo(supportedCues));
            Assert.That(EnemyAnimationCueCatalog.All.Select(metadata => metadata.Cue).Distinct().Count(),
                Is.EqualTo(supportedCues.Length));
            Assert.That(EnemyAnimationCueCatalog.TryGet(EnemyAnimationCue.None, out _), Is.False);
            Assert.That(EnemyAnimationCueCatalog.TryGet((EnemyAnimationCue)999, out _), Is.False);
        }

        [TestCase(EnemyAnimationCue.ActionWindup, true, true, true)]
        [TestCase(EnemyAnimationCue.ActionExecute, true, false, false)]
        [TestCase(EnemyAnimationCue.ActionRecovery, true, true, true)]
        [TestCase(EnemyAnimationCue.JumpWindup, true, true, true)]
        [TestCase(EnemyAnimationCue.JumpAirborne, true, true, true)]
        [TestCase(EnemyAnimationCue.JumpLanding, false, true, false)]
        [TestCase(EnemyAnimationCue.ChargeWindup, true, true, true)]
        [TestCase(EnemyAnimationCue.ChargeActive, false, true, false)]
        [TestCase(EnemyAnimationCue.ChargeRecovery, true, true, true)]
        [TestCase(EnemyAnimationCue.GlideWindup, false, true, true)]
        [TestCase(EnemyAnimationCue.GlideActive, false, true, false)]
        [TestCase(EnemyAnimationCue.GlideRecovery, false, true, true)]
        [TestCase(EnemyAnimationCue.UtilityWindup, true, true, true)]
        [TestCase(EnemyAnimationCue.UtilityRecovery, true, true, true)]
        [TestCase(EnemyAnimationCue.Hit, true, false, false)]
        [TestCase(EnemyAnimationCue.Death, true, false, false)]
        [Category("Core")]
        public void Catalog_DefinesAllowedDispatchAndTiming(
            EnemyAnimationCue cue,
            bool allowsTrigger,
            bool allowsState,
            bool supportsTiming)
        {
            var metadata = EnemyAnimationCueCatalog.GetRequired(cue);

            Assert.That(metadata.Allows(EnemyAnimationDispatchMode.Trigger), Is.EqualTo(allowsTrigger));
            Assert.That(metadata.Allows(EnemyAnimationDispatchMode.State), Is.EqualTo(allowsState));
            Assert.That(metadata.SupportsTiming, Is.EqualTo(supportsTiming));
            Assert.That(metadata.RequiresResolvableState, Is.EqualTo(allowsState));
            Assert.That(
                metadata.RequiresSeparateSustainedStateWhenPrimaryTrigger,
                Is.EqualTo(cue == EnemyAnimationCue.JumpAirborne));
        }
    }
}
