using System;
using System.Linq;
using System.Reflection;
using Game.Feature.UI.Screens;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.UI.Tests
{
    public sealed class SettingsSectionContractTests
    {
        [Test]
        public void RootView_DoesNotExposeTemporaryPassthroughContract()
        {
            var publicMethodNames = typeof(SettingsScreenView)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .Select(method => method.Name)
                .ToArray();

            Assert.That(publicMethodNames, Does.Not.Contain("ClickDisplayApply"));
            Assert.That(publicMethodNames, Does.Not.Contain("ClickDisplayRevert"));
            Assert.That(publicMethodNames, Does.Not.Contain("BeginAudioInteraction"));
            Assert.That(publicMethodNames, Does.Not.Contain("CommitAudioInteraction"));
            Assert.That(publicMethodNames, Does.Not.Contain("SelectDisplayResolution"));
            Assert.That(publicMethodNames, Does.Not.Contain("SetAudioMuted"));
            Assert.That(publicMethodNames, Does.Not.Contain("SetAudioVolume"));
            Assert.That(publicMethodNames, Does.Not.Contain("SetDisplayFullscreen"));
        }

        [Test]
        public void RootView_UsesSerializedSectionReferences()
        {
            var sectionFields = typeof(SettingsScreenView)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(field => field.GetCustomAttribute<SerializeField>() != null)
                .Where(field => field.FieldType == typeof(SettingsAudioView) ||
                                field.FieldType == typeof(SettingsDisplayView) ||
                                field.FieldType == typeof(SettingsInputView))
                .Select(field => field.Name)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(sectionFields, Is.EqualTo(new[] { "_audioView", "_displayView", "_inputView" }));
        }

        [Test]
        public void SectionViews_OwnTheirInternalControls()
        {
            Assert.That(typeof(SettingsAudioView).GetMethod("ValidateAuthoredControlsOrThrow"), Is.Not.Null);
            Assert.That(typeof(SettingsDisplayView).GetMethod("ValidateAuthoredControlsOrThrow"), Is.Not.Null);
            Assert.That(typeof(SettingsScreenView).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(method => method.Name), Does.Not.Contain("ValidateAuthoredControlsOrThrow"));
        }

        [Test]
        public void RootView_DoesNotValidateAuthoredChildNamesAsContract()
        {
            var settingsClone = UnityEngine.Object.Instantiate(
                UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(UiTestPrefabAssetUtility.SettingsScreenPrefabPath));

            try
            {
                settingsClone.AudioView.gameObject.name = "RenamedAudioSection";
                settingsClone.DisplayView.gameObject.name = "RenamedDisplaySection";
                settingsClone.InputView.gameObject.name = "RenamedInputSection";

                Assert.DoesNotThrow(() => settingsClone.ValidateAuthoredStructureOrThrow());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settingsClone.gameObject);
            }
        }
    }
}
