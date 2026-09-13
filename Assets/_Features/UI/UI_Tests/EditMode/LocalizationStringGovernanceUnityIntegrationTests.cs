using System;
using System.IO;
using System.Linq;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition.Editor;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Localization.Settings;

namespace Game.Feature.UI.Tests
{
    public sealed class LocalizationStringGovernanceUnityIntegrationTests
    {
        [Test]
        public void ProductionShadow_ActualUnityState_PassesGenericValidator()
        {
            var catalog = UiLocaleCatalog.CreateProductionShadow();
            var snapshot = LocalizationStringGovernanceUnityAdapter.Capture(catalog);
            var report = new LocalizationStringGovernanceValidator().Validate(snapshot);

            Assert.That(
                snapshot.RegisteredLocaleCodes.Intersect(
                    catalog.ShipReadyLocales.Select(locale => locale.CanonicalCode),
                    StringComparer.Ordinal),
                Is.EquivalentTo(catalog.ShipReadyLocales.Select(locale => locale.CanonicalCode)));
            Assert.That(
                report.Diagnostics,
                Is.Empty,
                string.Join(Environment.NewLine, report.Diagnostics.Select(diagnostic =>
                    $"{diagnostic.Code}: {diagnostic.LocaleCode}/{diagnostic.Table}/{diagnostic.Key} - {diagnostic.Message}")));
            Assert.That(report.HasBlockingFailures, Is.False);
        }

        [Test]
        public void GovernedUiProjection_UsesContractsAndDescriptorsWithoutBootstrapOrLocaleBranches()
        {
            var requirements = UiLocalizationRequirementProjection.Create();
            var distinctRequirements = requirements
                .GroupBy(requirement => requirement.Table + "\u001f" + requirement.Key, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
            var sharedData = UnityEditor.Localization.LocalizationEditorSettings
                .GetStringTableCollection("UI").SharedData.Entries.Select(entry => entry.Key);

            Assert.That(distinctRequirements.Select(requirement => requirement.Key), Is.EquivalentTo(sharedData));
            Assert.That(requirements.Select(requirement => requirement.Owner),
                Has.Some.StartsWith(nameof(SettingsLocalizationContract)));
            Assert.That(requirements.Select(requirement => requirement.Owner),
                Has.Some.StartsWith(nameof(MainMenuLocalizationContract)));

            var source = File.ReadAllText(
                "Assets/_Features/UI/UI_Application/Runtime/UiLocalizationRequirementProjection.cs");
            Assert.That(source, Does.Not.Contain("SettingsLocalizationAssetBootstrap"));
            Assert.That(source, Does.Not.Contain("en-US"));
            Assert.That(source, Does.Not.Contain("ko-KR"));
        }

        [Test]
        public void StageProjection_UsesActivePresentationKeysAndExplicitLegacyCompatibilityRequirement()
        {
            var snapshot = LocalizationStringGovernanceUnityAdapter.Capture(UiLocaleCatalog.CreateProductionShadow());
            var stageRequirements = snapshot.Requirements
                .Where(requirement => requirement.Table == StageDisplayNameKeys.Table)
                .ToArray();
            var sequence = AssetDatabase.LoadAssetAtPath<CampaignStageSequenceDefinition>(
                StageContentPaths.CampaignStageSequenceAssetPath);

            Assert.That(sequence, Is.Not.Null);
            Assert.That(stageRequirements.Length, Is.EqualTo(sequence.Entries.Count + 1));
            Assert.That(stageRequirements.Select(requirement => requirement.Key),
                Does.Contain(LocalizationStringGovernanceUnityAdapter.CompatibilityRetainedStageDisplayNameKey));
            Assert.That(stageRequirements.Single(requirement =>
                    requirement.Key == LocalizationStringGovernanceUnityAdapter.CompatibilityRetainedStageDisplayNameKey).Owner,
                Does.Contain("Compatibility-retained"));
        }

        [Test]
        public void SmartFormatterAnalysis_NormalizesOrderMultiplicityAndEscapedBraces()
        {
            var formatter = LocalizationSettings.StringDatabase.SmartFormatter;

            var analyzed = LocalizationStringGovernanceUnityAdapter.AnalyzeEntry(
                "key", "{1} / {0} / {1} / {{literal}}", true, formatter);

            Assert.That(analyzed.SmartAnalysisSucceeded, Is.True);
            Assert.That(analyzed.PlaceholderSignature,
                Is.EqualTo(PlaceholderSignature.FromSelectors(new[] { "0", "1", "1" })));
        }

        [Test]
        public void SmartFormatterAnalysis_MalformedInputDoesNotEscapeAsException()
        {
            var formatter = LocalizationSettings.StringDatabase.SmartFormatter;

            var analyzed = LocalizationStringGovernanceUnityAdapter.AnalyzeEntry(
                "key", "broken {0", true, formatter);

            Assert.That(analyzed.SmartAnalysisSucceeded, Is.False);
            Assert.That(analyzed.SmartAnalysisFailureReason, Is.Not.Empty);
        }

        [Test]
        public void SmartFormatterAnalysis_NonSmartLiteralBracesDoNotRequireParser()
        {
            var analyzed = LocalizationStringGovernanceUnityAdapter.AnalyzeEntry(
                "key", "literal { brace", false, null);

            Assert.That(analyzed.SmartAnalysisSucceeded, Is.True);
            Assert.That(analyzed.PlaceholderSignature, Is.EqualTo(PlaceholderSignature.Empty));
            Assert.That(analyzed.SmartAnalysisFailureReason, Is.Empty);
        }

        [Test]
        public void AdapterSource_IsReadOnlyAndPackageBoundaryRemainsOneWay()
        {
            var adapterSource = File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Editor/LocalizationStringGovernanceUnityAdapter.cs");
            foreach (var forbidden in new[]
                     {
                         ".AddLocale(", ".CreateStringTableCollection(", ".AddNewTable(",
                         ".AddEntry(", ".RemoveEntry(", ".SetPreloadTableFlag(",
                         "EditorUtility.SetDirty(", "AssetDatabase.SaveAssets(",
                         "AssetDatabase.Refresh("
                     })
            {
                Assert.That(adapterSource, Does.Not.Contain(forbidden), forbidden);
            }

            Assert.That(adapterSource, Does.Not.Contain("SettingsLocalizationAssetBootstrap"));
            Assert.That(adapterSource, Does.Not.Contain("Addressable"));
            Assert.That(adapterSource, Does.Not.Contain("en-US"));
            Assert.That(adapterSource, Does.Not.Contain("ko-KR"));
            var viewSharedSource = File.ReadAllText(
                "Assets/_Features/UI/UI_ViewShared/Runtime/LocalizationStringGovernance.cs");
            Assert.That(viewSharedSource, Does.Not.Contain("UnityEngine"));
            Assert.That(viewSharedSource, Does.Not.Contain("LocalizationSettings"));
            Assert.That(viewSharedSource, Does.Not.Contain("LocalizationEditorSettings"));
            Assert.That(viewSharedSource, Does.Not.Contain("Addressables"));
        }
    }
}
