using System;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.EnemyAudio;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class EnemyAudioRequirementPolicyTests
    {
        [Test]
        [Category("Core")]
        public void DuplicateRequiredCue_Fails()
        {
            var policy = CreatePolicy(
                requiredCues: new[] { EnemyAudioCue.Move, EnemyAudioCue.Move },
                optionalCues: Array.Empty<EnemyAudioCue>());
            try
            {
                var exception = Assert.Throws<InvalidOperationException>(() => policy.ValidateOrThrow());
                StringAssert.Contains("duplicate enemy audio cue 'Move'", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(policy);
            }
        }

        [Test]
        [Category("Core")]
        public void DuplicateOptionalCue_Fails()
        {
            var policy = CreatePolicy(
                requiredCues: Array.Empty<EnemyAudioCue>(),
                optionalCues: new[] { EnemyAudioCue.Active, EnemyAudioCue.Active });
            try
            {
                var exception = Assert.Throws<InvalidOperationException>(() => policy.ValidateOrThrow());
                StringAssert.Contains("duplicate enemy audio cue 'Active'", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(policy);
            }
        }

        [Test]
        [Category("Core")]
        public void CueInRequiredAndOptional_Fails()
        {
            var policy = CreatePolicy(
                requiredCues: new[] { EnemyAudioCue.Death },
                optionalCues: new[] { EnemyAudioCue.Death });
            try
            {
                var exception = Assert.Throws<InvalidOperationException>(() => policy.ValidateOrThrow());
                StringAssert.Contains("in both required and optional cues", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(policy);
            }
        }

        [Test]
        [Category("Core")]
        public void UnknownCue_Fails()
        {
            var policy = CreatePolicy(
                requiredCues: new[] { EnemyAudioCue.None },
                optionalCues: Array.Empty<EnemyAudioCue>());
            try
            {
                var exception = Assert.Throws<InvalidOperationException>(() => policy.ValidateOrThrow());
                StringAssert.Contains("non-runtime enemy audio cue 'None'", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(policy);
            }
        }

        [Test]
        [Category("Core")]
        public void UnspecifiedCue_IsImplicitDisabled()
        {
            var policy = CreatePolicy(
                requiredCues: new[] { EnemyAudioCue.Move },
                optionalCues: new[] { EnemyAudioCue.Active });
            try
            {
                Assert.That(policy.GetRequirement(EnemyAudioCue.Move), Is.EqualTo(EnemyAudioCueRequirement.Required));
                Assert.That(policy.GetRequirement(EnemyAudioCue.Active), Is.EqualTo(EnemyAudioCueRequirement.Optional));
                Assert.That(policy.GetRequirement(EnemyAudioCue.Death), Is.EqualTo(EnemyAudioCueRequirement.Disabled));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(policy);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAudioCueCatalog_RuntimeCues_PublicSurface_IsReviewed()
        {
            Assert.That(
                EnemyAudioCueCatalog.RuntimeCues.ToArray(),
                Is.EqualTo(new[]
                {
                    EnemyAudioCue.Move,
                    EnemyAudioCue.Death,
                    EnemyAudioCue.Windup,
                    EnemyAudioCue.Landing,
                    EnemyAudioCue.Active,
                    EnemyAudioCue.Recover,
                    EnemyAudioCue.ForwardCellImpact,
                    EnemyAudioCue.ChargeActiveLoop,
                    EnemyAudioCue.StationaryActive,
                    EnemyAudioCue.PassiveContact,
                }));
        }

        private static EnemyAudioRequirementPolicy CreatePolicy(
            EnemyAudioCue[] requiredCues,
            EnemyAudioCue[] optionalCues)
        {
            var policy = ScriptableObject.CreateInstance<EnemyAudioRequirementPolicy>();
            policy.name = "TestEnemyAudioRequirementPolicy";
            SetSerializedField(typeof(EnemyAudioRequirementPolicy), policy, "requiredCues", requiredCues);
            SetSerializedField(typeof(EnemyAudioRequirementPolicy), policy, "optionalCues", optionalCues);
            return policy;
        }

        private static void SetSerializedField(Type declaringType, object target, string fieldName, object value)
        {
            var field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {declaringType.Name}.");
            field.SetValue(target, value);
        }
    }
}
