using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Stages;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class StageAudioDefinitionValidationTests
    {
        private readonly List<UnityEngine.Object> ownedObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (var i = ownedObjects.Count - 1; i >= 0; i--)
            {
                if (ownedObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(ownedObjects[i]);
                }
            }

            ownedObjects.Clear();
        }

        [Test]
        [Category("Core")]
        public void StageAudioDefinitionAllowsExplicitNoBgmTests()
        {
            var definition = Track(ScriptableObject.CreateInstance<StageAudioDefinition>());

            Assert.DoesNotThrow(() => definition.ValidateOrThrow());
        }

        [Test]
        [Category("Core")]
        public void StageAudioDefinitionRejectsNullRequiredGameplayBgmTests()
        {
            var definition = Track(ScriptableObject.CreateInstance<StageAudioDefinition>());
            SetField(definition, "gameplayBgm", null);

            var exception = Assert.Throws<InvalidOperationException>(() => definition.ValidateOrThrow());

            Assert.That(exception.Message, Is.EqualTo("StageAudioDefinition requires gameplayBgm to be authored explicitly."));
        }

        [Test]
        [Category("Core")]
        public void StageAudioDefinitionRejectsProfileOnNoneSlotTests()
        {
            var definition = Track(ScriptableObject.CreateInstance<StageAudioDefinition>());
            var slot = new StageBgmSlot();
            SetField(slot, "mode", StageBgmSlotMode.None);
            SetField(slot, "profile", CreateBgmProfile("ProfileOnNone", AudioCategory.Bgm, loop: true));
            SetField(definition, "gameplayBgm", slot);

            var exception = Assert.Throws<InvalidOperationException>(() => definition.ValidateOrThrow());

            Assert.That(
                exception.Message,
                Is.EqualTo("StageAudioDefinition.gameplayBgm cannot assign a BgmProfile when mode is None."));
        }

        [Test]
        [Category("Core")]
        public void StageAudioDefinitionRejectsNonBgmOrNonLoopProfileTests()
        {
            var nonBgm = Track(ScriptableObject.CreateInstance<StageAudioDefinition>());
            SetField(nonBgm, "gameplayBgm", CreateProfileSlot(CreateBgmProfile("NonBgmProfile", AudioCategory.Sfx, loop: true)));

            var nonBgmException = Assert.Throws<InvalidOperationException>(() => nonBgm.ValidateOrThrow());
            Assert.That(
                nonBgmException.Message,
                Is.EqualTo("StageAudioDefinition.gameplayBgm profile is invalid: BgmProfile 'NonBgmProfile' requires loopDefinition to use AudioCategory.Bgm."));

            var nonLoop = Track(ScriptableObject.CreateInstance<StageAudioDefinition>());
            SetField(nonLoop, "gameplayBgm", CreateProfileSlot(CreateBgmProfile("NonLoopProfile", AudioCategory.Bgm, loop: false)));

            var nonLoopException = Assert.Throws<InvalidOperationException>(() => nonLoop.ValidateOrThrow());
            Assert.That(
                nonLoopException.Message,
                Is.EqualTo("StageAudioDefinition.gameplayBgm profile is invalid: BgmProfile 'NonLoopProfile' requires loopDefinition to be loop enabled."));
        }

        [Test]
        [Category("Core")]
        public void StageAudioAssemblerProducesResolvedDataTests()
        {
            var profile = CreateBgmProfile("ResolvedProfile", AudioCategory.Bgm, loop: true);
            var definition = Track(ScriptableObject.CreateInstance<StageAudioDefinition>());
            SetField(definition, "gameplayBgm", CreateProfileSlot(profile));

            var resolved = StageAudioAssembler.Resolve(definition);

            Assert.That(resolved.GameplayBgm.Mode, Is.EqualTo(StageBgmSlotMode.Profile));
            Assert.That(resolved.GameplayBgm.Profile, Is.SameAs(profile));
            Assert.That(resolved.PreviewBgm.Mode, Is.EqualTo(StageBgmSlotMode.None));
            Assert.That(resolved.PhaseBgms, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void StageContentEntryRequiresAudioCompanionTests()
        {
            var entry = Track(ScriptableObject.CreateInstance<StageContentEntry>());
            SetField(entry, "stageId", StageId.CreateOrThrow("stage-a"));
            entry.AssignPresentationDefinition(Track(ScriptableObject.CreateInstance<StagePresentationDefinition>()));

            var report = new StageCatalogValidator().ValidateEntries(
                new[] { entry },
                aliasTable: null,
                new StageCatalogValidationOptions
                {
                    RequireAudioDefinition = true,
                    Timing = StageValidationTiming.TestOrCi,
                });

            Assert.That(report.Issues.Any(issue => issue.Code == "companion.audio.null"), Is.True);
        }

        private StageBgmSlot CreateProfileSlot(BgmProfile profile)
        {
            var slot = new StageBgmSlot();
            SetField(slot, "mode", StageBgmSlotMode.Profile);
            SetField(slot, "profile", profile);
            return slot;
        }

        private BgmProfile CreateBgmProfile(string name, AudioCategory category, bool loop)
        {
            var definition = Track(ScriptableObject.CreateInstance<SingleAudioDefinition>());
            definition.name = name + "_Definition";
            SetField(typeof(AudioDefinition), definition, "category", category);
            SetField(typeof(AudioDefinition), definition, "loop", loop);
            SetField(typeof(AudioDefinition), definition, "defaultVolumeTrim", 1f);
            SetField(typeof(AudioDefinition), definition, "pitchRange", Vector2.one);

            var profile = Track(ScriptableObject.CreateInstance<BgmProfile>());
            profile.name = name;
            SetField(profile, "loopDefinition", definition);
            return profile;
        }

        private T Track<T>(T value)
            where T : UnityEngine.Object
        {
            ownedObjects.Add(value);
            return value;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            SetField(target.GetType(), target, fieldName, value);
        }

        private static void SetField(Type targetType, object target, string fieldName, object value)
        {
            FieldInfo field = null;
            for (var type = targetType; type != null && field == null; type = type.BaseType)
            {
                field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            }

            Assert.That(field, Is.Not.Null, $"{targetType.Name}.{fieldName}");
            field.SetValue(target, value);
        }
    }
}
