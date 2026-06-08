using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Gameplay.EnemyAudio;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class EnemyAudioRequirementBindingTests
    {
        [Test]
        [Category("Core")]
        public void MissingTargetProfile_Fails()
        {
            using var bundle = CreateBindingBundle(null, CreatePolicy());

            var exception = Assert.Throws<InvalidOperationException>(() => bundle.Binding.ValidateOrThrow());

            StringAssert.Contains("targetProfile is required", exception.Message);
        }

        [Test]
        [Category("Core")]
        public void MissingPolicy_Fails()
        {
            using var profileBundle = CreateEnemyAudioProfile();
            using var bundle = CreateBindingBundle(profileBundle.Profile, null);

            var exception = Assert.Throws<InvalidOperationException>(() => bundle.Binding.ValidateOrThrow());

            StringAssert.Contains("policy is required", exception.Message);
        }

        [Test]
        [Category("Core")]
        public void RequiredCueMissingBinding_Fails()
        {
            using var profileBundle = CreateEnemyAudioProfile();
            using var bundle = CreateBindingBundle(
                profileBundle.Profile,
                CreatePolicy(requiredCues: new[] { EnemyAudioCue.Move }));

            var exception = Assert.Throws<InvalidOperationException>(() => bundle.Binding.ValidateOrThrow());

            StringAssert.Contains("requires target profile", exception.Message);
            StringAssert.Contains("Move", exception.Message);
        }

        [Test]
        [Category("Core")]
        public void OptionalCueMissingBinding_Passes()
        {
            using var profileBundle = CreateEnemyAudioProfile();
            using var bundle = CreateBindingBundle(
                profileBundle.Profile,
                CreatePolicy(optionalCues: new[] { EnemyAudioCue.Move }));

            Assert.DoesNotThrow(() => bundle.Binding.ValidateOrThrow());
        }

        [Test]
        [Category("Core")]
        public void ImplicitDisabledCueWithBinding_Fails()
        {
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Active, new DefinitionSpec(AudioCategory.Sfx, loop: false)));
            using var bundle = CreateBindingBundle(profileBundle.Profile, CreatePolicy());

            var exception = Assert.Throws<InvalidOperationException>(() => bundle.Binding.ValidateOrThrow());

            StringAssert.Contains("disables enemy audio cue 'Active'", exception.Message);
            StringAssert.Contains("carries a binding", exception.Message);
        }

        [Test]
        [Category("Core")]
        public void DisabledOverrideWithBinding_Fails()
        {
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Active, new DefinitionSpec(AudioCategory.Sfx, loop: false)));
            using var bundle = CreateBindingBundle(
                profileBundle.Profile,
                CreatePolicy(optionalCues: new[] { EnemyAudioCue.Active }),
                Override(EnemyAudioCue.Active, EnemyAudioCueRequirement.Disabled));

            var exception = Assert.Throws<InvalidOperationException>(() => bundle.Binding.ValidateOrThrow());

            StringAssert.Contains("disables enemy audio cue 'Active'", exception.Message);
        }

        [Test]
        [Category("Core")]
        public void OverrideCanRequireCue()
        {
            using var profileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(EnemyAudioCue.Move, new DefinitionSpec(AudioCategory.Sfx, loop: false)));
            using var bundle = CreateBindingBundle(
                profileBundle.Profile,
                CreatePolicy(),
                Override(EnemyAudioCue.Move, EnemyAudioCueRequirement.Required));

            Assert.DoesNotThrow(() => bundle.Binding.ValidateOrThrow());
            Assert.That(
                bundle.Binding.GetEffectiveRequirement(EnemyAudioCue.Move),
                Is.EqualTo(EnemyAudioCueRequirement.Required));
        }

        [Test]
        [Category("Core")]
        public void OverrideCanOptionalizeCue()
        {
            using var profileBundle = CreateEnemyAudioProfile();
            using var bundle = CreateBindingBundle(
                profileBundle.Profile,
                CreatePolicy(requiredCues: new[] { EnemyAudioCue.Move }),
                Override(EnemyAudioCue.Move, EnemyAudioCueRequirement.Optional));

            Assert.DoesNotThrow(() => bundle.Binding.ValidateOrThrow());
            Assert.That(
                bundle.Binding.GetEffectiveRequirement(EnemyAudioCue.Move),
                Is.EqualTo(EnemyAudioCueRequirement.Optional));
        }

        [Test]
        [Category("Core")]
        public void OverrideCanDisableCue()
        {
            using var profileBundle = CreateEnemyAudioProfile();
            using var bundle = CreateBindingBundle(
                profileBundle.Profile,
                CreatePolicy(requiredCues: new[] { EnemyAudioCue.Move }),
                Override(EnemyAudioCue.Move, EnemyAudioCueRequirement.Disabled));

            Assert.DoesNotThrow(() => bundle.Binding.ValidateOrThrow());
            Assert.That(
                bundle.Binding.GetEffectiveRequirement(EnemyAudioCue.Move),
                Is.EqualTo(EnemyAudioCueRequirement.Disabled));
        }

        [Test]
        [Category("Core")]
        public void DuplicateOverrideCue_Fails()
        {
            using var profileBundle = CreateEnemyAudioProfile();
            using var bundle = CreateBindingBundle(
                profileBundle.Profile,
                CreatePolicy(),
                Override(EnemyAudioCue.Move, EnemyAudioCueRequirement.Optional),
                Override(EnemyAudioCue.Move, EnemyAudioCueRequirement.Required));

            var exception = Assert.Throws<InvalidOperationException>(() => bundle.Binding.ValidateOrThrow());

            StringAssert.Contains("duplicate enemy audio requirement override", exception.Message);
        }

        [Test]
        [Category("Core")]
        public void ChargeActiveLoopRequired_RequiresLoopDefinitionAndAttachment()
        {
            using var nonLoopProfileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(
                    EnemyAudioCue.ChargeActiveLoop,
                    new DefinitionSpec(AudioCategory.Sfx, loop: false),
                    attachmentSlotId: "charge-active-loop"));
            using var nonLoopBindingBundle = CreateBindingBundle(
                nonLoopProfileBundle.Profile,
                CreatePolicy(requiredCues: new[] { EnemyAudioCue.ChargeActiveLoop }));

            var nonLoopException = Assert.Throws<InvalidOperationException>(
                () => nonLoopBindingBundle.Binding.ValidateOrThrow());
            StringAssert.Contains("requires a looping AudioDefinition", nonLoopException.Message);

            using var missingAttachmentProfileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(
                    EnemyAudioCue.ChargeActiveLoop,
                    new DefinitionSpec(AudioCategory.Sfx, loop: true)));
            using var missingAttachmentBindingBundle = CreateBindingBundle(
                missingAttachmentProfileBundle.Profile,
                CreatePolicy(requiredCues: new[] { EnemyAudioCue.ChargeActiveLoop }));

            var missingAttachmentException = Assert.Throws<InvalidOperationException>(
                () => missingAttachmentBindingBundle.Binding.ValidateOrThrow());
            StringAssert.Contains("requires an attachment slot", missingAttachmentException.Message);

            using var validProfileBundle = CreateEnemyAudioProfile(
                new EnemyAudioEntrySpec(
                    EnemyAudioCue.ChargeActiveLoop,
                    new DefinitionSpec(AudioCategory.Sfx, loop: true),
                    attachmentSlotId: "charge-active-loop"));
            using var validBindingBundle = CreateBindingBundle(
                validProfileBundle.Profile,
                CreatePolicy(requiredCues: new[] { EnemyAudioCue.ChargeActiveLoop }));

            Assert.DoesNotThrow(() => validBindingBundle.Binding.ValidateOrThrow());
        }

        private static EnemyAudioRequirementPolicy CreatePolicy(
            EnemyAudioCue[] requiredCues = null,
            EnemyAudioCue[] optionalCues = null)
        {
            var policy = ScriptableObject.CreateInstance<EnemyAudioRequirementPolicy>();
            policy.name = "TestEnemyAudioRequirementPolicy";
            SetSerializedField(
                typeof(EnemyAudioRequirementPolicy),
                policy,
                "requiredCues",
                requiredCues ?? Array.Empty<EnemyAudioCue>());
            SetSerializedField(
                typeof(EnemyAudioRequirementPolicy),
                policy,
                "optionalCues",
                optionalCues ?? Array.Empty<EnemyAudioCue>());
            return policy;
        }

        private static BindingBundle CreateBindingBundle(
            EnemyAudioProfile profile,
            EnemyAudioRequirementPolicy policy,
            params EnemyAudioRequirementOverride[] overrides)
        {
            var binding = ScriptableObject.CreateInstance<EnemyAudioRequirementBinding>();
            binding.name = "TestEnemyAudioRequirementBinding";
            SetSerializedField(typeof(EnemyAudioRequirementBinding), binding, "targetProfile", profile);
            SetSerializedField(typeof(EnemyAudioRequirementBinding), binding, "policy", policy);
            SetSerializedField(
                typeof(EnemyAudioRequirementBinding),
                binding,
                "overrides",
                overrides ?? Array.Empty<EnemyAudioRequirementOverride>());

            return new BindingBundle(binding, policy);
        }

        private static EnemyAudioRequirementOverride Override(
            EnemyAudioCue cue,
            EnemyAudioCueRequirement requirement)
        {
            var row = new EnemyAudioRequirementOverride();
            SetSerializedField(typeof(EnemyAudioRequirementOverride), row, "cue", cue);
            SetSerializedField(typeof(EnemyAudioRequirementOverride), row, "requirement", requirement);
            return row;
        }

        private static EnemyAudioProfileBundle CreateEnemyAudioProfile(params EnemyAudioEntrySpec[] entrySpecs)
        {
            var profile = ScriptableObject.CreateInstance<EnemyAudioProfile>();
            profile.name = "TestEnemyAudioProfile";
            var trackedObjects = new List<UnityEngine.Object> { profile };
            var entries = new EnemyAudioEntry[entrySpecs.Length];

            for (var i = 0; i < entrySpecs.Length; i++)
            {
                AudioBinding binding = null;
                if (entrySpecs[i].DefinitionSpec.HasValue)
                {
                    var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
                    definition.name = entrySpecs[i].Cue.ToString();
                    var clip = AudioClip.Create(definition.name, 4410, 1, 44100, false);
                    trackedObjects.Add(clip);
                    trackedObjects.Add(definition);
                    SetSerializedField(typeof(SingleAudioDefinition), definition, "clip", clip);
                    SetSerializedField(
                        typeof(AudioDefinition),
                        definition,
                        "category",
                        entrySpecs[i].DefinitionSpec.Value.Category);
                    SetSerializedField(typeof(AudioDefinition), definition, "defaultVolumeTrim", 1f);
                    SetSerializedField(typeof(AudioDefinition), definition, "pitchRange", Vector2.one);
                    SetSerializedField(typeof(AudioDefinition), definition, "loop", entrySpecs[i].DefinitionSpec.Value.Loop);

                    binding = new AudioBinding();
                    SetSerializedField(typeof(AudioBinding), binding, "definition", definition);
                    SetSerializedField(
                        typeof(AudioBinding),
                        binding,
                        "attachmentSlot",
                        AudioAttachmentSlot.FromId(entrySpecs[i].AttachmentSlotId));
                    SetSerializedField(typeof(AudioBinding), binding, "policy", null);
                }

                entries[i] = new EnemyAudioEntry
                {
                    Cue = entrySpecs[i].Cue,
                    Binding = binding,
                    IsOptional = entrySpecs[i].IsOptional,
                };
            }

            SetSerializedField(typeof(EnemyAudioProfile), profile, "entries", entries);
            return new EnemyAudioProfileBundle(profile, trackedObjects);
        }

        private static void SetSerializedField(Type declaringType, object target, string fieldName, object value)
        {
            var field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {declaringType.Name}.");
            field.SetValue(target, value);
        }

        private readonly struct DefinitionSpec
        {
            public DefinitionSpec(AudioCategory category, bool loop)
            {
                Category = category;
                Loop = loop;
            }

            public AudioCategory Category { get; }

            public bool Loop { get; }
        }

        private readonly struct EnemyAudioEntrySpec
        {
            public EnemyAudioEntrySpec(
                EnemyAudioCue cue,
                DefinitionSpec? definitionSpec,
                bool isOptional = false,
                string attachmentSlotId = null)
            {
                Cue = cue;
                DefinitionSpec = definitionSpec;
                IsOptional = isOptional;
                AttachmentSlotId = attachmentSlotId;
            }

            public EnemyAudioCue Cue { get; }

            public DefinitionSpec? DefinitionSpec { get; }

            public bool IsOptional { get; }

            public string AttachmentSlotId { get; }
        }

        private sealed class BindingBundle : IDisposable
        {
            private readonly EnemyAudioRequirementPolicy _policy;

            public BindingBundle(EnemyAudioRequirementBinding binding, EnemyAudioRequirementPolicy policy)
            {
                Binding = binding;
                _policy = policy;
            }

            public EnemyAudioRequirementBinding Binding { get; }

            public void Dispose()
            {
                if (Binding != null)
                {
                    UnityEngine.Object.DestroyImmediate(Binding);
                }

                if (_policy != null)
                {
                    UnityEngine.Object.DestroyImmediate(_policy);
                }
            }
        }

        private sealed class EnemyAudioProfileBundle : IDisposable
        {
            private readonly List<UnityEngine.Object> _trackedObjects;

            public EnemyAudioProfileBundle(EnemyAudioProfile profile, List<UnityEngine.Object> trackedObjects)
            {
                Profile = profile;
                _trackedObjects = trackedObjects;
            }

            public EnemyAudioProfile Profile { get; }

            public void Dispose()
            {
                for (var i = _trackedObjects.Count - 1; i >= 0; i--)
                {
                    if (_trackedObjects[i] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(_trackedObjects[i]);
                    }
                }
            }
        }
    }
}
