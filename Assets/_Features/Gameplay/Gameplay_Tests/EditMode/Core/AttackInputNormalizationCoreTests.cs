using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.BoardState;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class AttackInputNormalizationCoreTests
    {
        [Test]
        [Category("Core")]
        public void AttackInputNormalizer_DuplicateReservationSequenceForSameSource_Throws()
        {
            var impactReservations = new[]
            {
                new ImpactReservation(10, 20, new SurfaceCell(FaceId.Floor, 1, 0), 1, 5, 1, 1),
                new ImpactReservation(10, 30, new SurfaceCell(FaceId.Floor, 1, 1), 1, 5, 2, 1),
            };

            var exception = Assert.Throws<InvalidOperationException>(
                () => new AttackInputNormalizer().Normalize(
                    Array.Empty<RawAttackIntent>(),
                    impactReservations,
                    new List<AttackIntent>()));

            StringAssert.Contains("Duplicate attack input normalization key detected", exception.Message);
        }

        [Test]
        [Category("Core")]
        public void RawAttackIntent_AttackRejectsNonPositiveTargetIds()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RawAttackIntent(10, 5, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RawAttackIntent(10, 5, -1));
        }

        [Test]
        [Category("Core")]
        public void AttackInputNormalizer_PreservesExplicitAttackCommandKind()
        {
            var normalizedInputs = new List<AttackIntent>();

            new AttackInputNormalizer().Normalize(
                new[]
                {
                    new RawAttackIntent(20, 1, 30),
                },
                Array.Empty<ImpactReservation>(),
                normalizedInputs);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 20, Command: AttackCommandKind.Attack, TargetId: 30),
                },
                normalizedInputs
                    .Select(intent => (intent.SourceId, intent.CommandKind, intent.TargetId))
                    .ToArray());
        }

    }
}
