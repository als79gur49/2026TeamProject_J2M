using System.Reflection;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class ActionPlanCorrelationContractTests
    {
        [Test]
        [Category("Extended")]
        public void DamageResolutionRecord_ActionPlanId_RemainsWithoutLegacyGroupIdAlias()
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
            Assert.That(FindPublicInstanceProperty<DamageResolutionRecord>("ActionPlanId"), Is.Not.Null);
            Assert.That(FindPublicInstanceProperty<DamageResolutionRecord>("IntentId"), Is.Not.Null);
            Assert.That(FindPublicInstanceProperty<DamageResolutionRecord>("GroupId"), Is.Null);
        }

        [Test]
        [Category("Extended")]
        public void DestroyResolutionRecord_ActionPlanId_RemainsWithoutLegacyGroupIdAlias()
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
            Assert.That(FindPublicInstanceProperty<DestroyResolutionRecord>("ActionPlanId"), Is.Not.Null);
            Assert.That(FindPublicInstanceProperty<DestroyResolutionRecord>("IntentId"), Is.Not.Null);
            Assert.That(FindPublicInstanceProperty<DestroyResolutionRecord>("GroupId"), Is.Null);
        }

        [Test]
        [Category("Extended")]
        public void DelayedAttackEffectRecord_SourceActionPlanId_RemainsWithoutLegacySourceActionGroupIdAlias()
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
            Assert.That(FindPublicInstanceProperty<DelayedAttackEffectRecord>("SourceActionPlanId"), Is.Not.Null);
            Assert.That(FindPublicInstanceProperty<DelayedAttackEffectRecord>("SourceActionGroupId"), Is.Null);
        }

        [Test]
        [Category("Extended")]
        public void ActionGroup_GroupId_RemainsCompatibilityIrVocabulary()
        {
            var group = new ActionGroup(
                intentId: 23,
                sourceId: 10,
                priority: 5,
                groupKind: ActionGroupKind.Attack);

            group.AssignGroupId(17);

            Assert.That(group.GroupId, Is.EqualTo(17));
            Assert.That(FindPublicInstanceProperty<ActionGroup>("GroupId"), Is.Not.Null);
        }

        private static PropertyInfo FindPublicInstanceProperty<T>(string propertyName)
        {
            return typeof(T).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        }
    }
}
