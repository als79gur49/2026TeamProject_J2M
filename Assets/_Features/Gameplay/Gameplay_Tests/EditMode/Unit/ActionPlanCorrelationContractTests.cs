using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class ActionPlanCorrelationContractTests
    {
        [Test]
        [Category("Extended")]
        public void DamageResolutionRecord_ActionPlanId_IsCanonicalCorrelationField()
        {
            var record = new DamageResolutionRecord(
                actionPlanId: 17,
                intentId: 23,
                sourceId: 10,
                sourceKind: AttackSourceKind.Combat,
                targetId: 20,
                amount: 3,
                accepted: true,
                rejectReason: DamageRejectReason.None,
                localActionIndex: 2);

            Assert.That(record.ActionPlanId, Is.EqualTo(17));
#pragma warning disable CS0618
            Assert.That(record.IntentId, Is.EqualTo(23));
#pragma warning restore CS0618
        }

        [Test]
        [Category("Extended")]
        public void DestroyResolutionRecord_ActionPlanId_IsCanonicalCorrelationField()
        {
            var record = new DestroyResolutionRecord(
                actionPlanId: 31,
                intentId: 41,
                sourceId: 10,
                targetId: 20,
                condition: DestroyCondition.WhenHpDepleted,
                finalHp: 0,
                accepted: true,
                localActionIndex: 1);

            Assert.That(record.ActionPlanId, Is.EqualTo(31));
#pragma warning disable CS0618
            Assert.That(record.IntentId, Is.EqualTo(41));
#pragma warning restore CS0618
        }

        [Test]
        [Category("Extended")]
        public void DelayedAttackEffectRecord_SourceActionPlanId_IsCanonicalCorrelationField()
        {
            var record = new DelayedAttackEffectRecord(
                sourceId: 10,
                targetId: 20,
                damage: 1,
                priority: 5,
                tickGenerated: 3,
                executeAtTick: 4,
                sourceActionPlanId: 29,
                effectSequence: 2);

            Assert.That(record.SourceActionPlanId, Is.EqualTo(29));
        }
    }
}
