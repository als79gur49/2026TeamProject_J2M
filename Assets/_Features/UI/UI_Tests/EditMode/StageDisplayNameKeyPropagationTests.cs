using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;
using UnityEditor;

namespace Game.Feature.UI.Tests
{
    public sealed class StageDisplayNameKeyPropagationTests
    {
        [Test]
        public void StagePresentationAssets_HaveCanonicalDisplayNameKeys()
        {
            var entries = LoadStageEntries();
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var entry in entries)
            {
                Assert.That(entry.PresentationDefinition, Is.Not.Null, entry.StageId.Value);
                var key = entry.PresentationDefinition.DisplayNameKey;
                var presentationPath = AssetDatabase.GetAssetPath(entry.PresentationDefinition);
                var presentationYaml = File.ReadAllText(presentationPath);

                Assert.That(key, Is.EqualTo(StageDisplayNameKeys.ForStage(entry.StageId)), entry.StageId.Value);
                Assert.That(seenKeys.Add(key), Is.True, key);
                Assert.That(presentationYaml, Does.Not.Contain("\n  displayName:"), presentationPath);
            }
        }

        [Test]
        public void StagePresentationAssembler_PreservesDisplayNameKeyWithoutResolvingText()
        {
            var entry = LoadStageEntries().First(entry => entry.StageId.Value == "stage-0-1");

            var resolved = StagePresentationAssembler.Resolve(entry.PresentationDefinition);

            Assert.That(resolved.DisplayNameKey, Is.EqualTo("stage.stage-0-1.display_name"));
            Assert.That(typeof(StagePresentationResolvedData).GetProperty("DisplayName"), Is.Null);
            Assert.That(typeof(StagePresentationResolvedData).GetProperty("LegacyDisplayNameFallback"), Is.Null);
        }

        [Test]
        public void CatalogResolverAndUiReadModels_PropagateStageDisplayNameKeyToDescriptor()
        {
            var provider = AssetDatabase.LoadAssetAtPath<ScriptableObjectStageCatalogProvider>(
                StageContentPaths.StageCatalogProviderAssetPath);
            Assert.That(provider, Is.Not.Null, StageContentPaths.StageCatalogProviderAssetPath);
            var resolver = new StageCatalogResolver(provider);
            var stageId = StageId.CreateOrThrow("stage-0-1");

            Assert.That(resolver.TryResolve(stageId, out var entry), Is.True);
            Assert.That(entry.PresentationDefinition, Is.Not.Null);
            var displayNameKey = StageDisplayNameKeys.RequireForStage(
                entry.StageId,
                entry.PresentationDefinition.DisplayNameKey);
            var gameplayReadModel = new GameplayStageReadModel(
                entry.StageId,
                displayNameKey);
            var mapper = new UIStateMapper();
            var result = mapper.ReduceRefresh(
                UIPresentationSnapshot.Empty,
                new UIStateRefreshInput(
                    tickIndex: 0,
                    shouldUpdateTickIndex: false,
                    finalTopology: default,
                    shouldUpdateFinalTopology: false,
                    isStageCleared: false,
                    isTopologyTransitionActive: false,
                    hasBlockingGameplayPresentation: false,
                    isPaused: false,
                    canAcceptGameplayCommands: true,
                    isUiGameplayInputBlocked: false,
                    stageId: gameplayReadModel.StageId,
                    stageDisplayNameKey: gameplayReadModel.DisplayNameKey));

            Assert.That(displayNameKey, Is.EqualTo("stage.stage-0-1.display_name"));
            Assert.That(typeof(GameplayStageReadModel).GetProperty("DisplayName"), Is.Null);
            Assert.That(typeof(GameplayStageReadModel).GetProperty("LegacyDisplayNameFallback"), Is.Null);
            Assert.That(result.Snapshot.Stage.DisplayNameKey, Is.EqualTo("stage.stage-0-1.display_name"));
            Assert.That(result.Snapshot.Stage.DisplayNameDescriptor.Table, Is.EqualTo("Stage"));
            Assert.That(result.Snapshot.Stage.DisplayNameDescriptor.Key, Is.EqualTo("stage.stage-0-1.display_name"));
            Assert.That(typeof(UIStageSlice).GetProperty("DisplayName"), Is.Null);
            Assert.That(typeof(UIStageSlice).GetProperty("LegacyDisplayNameFallback"), Is.Null);
        }

        [Test]
        public void StageInfoPresenter_ResolvesDisplayNameAtUiBoundary()
        {
            var resolver = new RecordingLocalizedTextResolver();
            var presenter = new StageInfoPresenter(resolver);

            presenter.Apply(new UIStageSlice(
                StageId.CreateOrThrow("stage-0-1"),
                "stage.stage-0-1.display_name"));

            Assert.That(presenter.ViewModel.StageName, Is.EqualTo("resolved:Stage:stage.stage-0-1.display_name"));
            Assert.That(resolver.LastDescriptor.Table, Is.EqualTo("Stage"));
            Assert.That(resolver.LastDescriptor.Key, Is.EqualTo("stage.stage-0-1.display_name"));
            presenter.Dispose();
        }

        [Test]
        public void StageDisplayNameLocalization_BoundariesStayPackageFreeOutsideComposition()
        {
            AssertNoAssemblyReference(typeof(StagePresentationDefinition).Assembly, "Unity.Localization");
            AssertNoAssemblyReference(typeof(StageCatalogResolver).Assembly, "Unity.Localization");
            AssertNoAssemblyReference(typeof(StageInfoPresenter).Assembly, "Unity.Localization");
            AssertNoAssemblyReference(typeof(LocalizedTextDescriptor).Assembly, "Unity.Localization");
        }

        [Test]
        public void StageDisplayNameFallback_LegacyRuntimeSurfaceIsRemoved()
        {
            Assert.That(typeof(StagePresentationDefinition).GetProperty("DisplayName"), Is.Null);
            Assert.That(typeof(StagePresentationDefinition).GetProperty("LegacyDisplayNameFallback"), Is.Null);
            Assert.That(typeof(StagePresentationDefinition).GetField(
                "displayName",
                BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null);
            Assert.That(typeof(MinimalStageCompletionReadModel).GetProperty("DisplayName"), Is.Null);
            Assert.That(typeof(MinimalStageCompletionReadModel).GetProperty("LegacyDisplayNameFallback"), Is.Null);
            Assert.That(typeof(UIStateRefreshInput).GetProperty("StageDisplayName"), Is.Null);

            var assemblerSource = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Presentation/StagePresentationAssemblers.cs");
            Assert.That(assemblerSource, Does.Not.Contain("LocalizedTextDescriptor"));
            Assert.That(assemblerSource, Does.Not.Contain("ILocalizedTextResolver"));
            Assert.That(assemblerSource, Does.Not.Contain("Unity.Localization"));
        }

        private static IReadOnlyList<StageContentEntry> LoadStageEntries()
        {
            var guids = AssetDatabase.FindAssets(
                $"t:{nameof(StageContentEntry)}",
                new[] { StageContentPaths.CampaignLevel01StagesRoot });
            var entries = guids
                .Select(guid => AssetDatabase.LoadAssetAtPath<StageContentEntry>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(entry => entry != null && entry.StageId.IsValid)
                .OrderBy(entry => entry.StageId.Value, StringComparer.Ordinal)
                .ToArray();

            Assert.That(entries, Is.Not.Empty);
            return entries;
        }

        private static void AssertNoAssemblyReference(Assembly assembly, string referenceName)
        {
            Assert.That(
                assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray(),
                Does.Not.Contain(referenceName),
                assembly.GetName().Name);
        }

        private sealed class RecordingLocalizedTextResolver : ILocalizedTextResolver
        {
            public string CurrentLocaleCode => "en-US";

            public event Action LocaleChanged;

            public LocalizedTextDescriptor LastDescriptor { get; private set; }

            public string Resolve(LocalizedTextDescriptor descriptor)
            {
                LastDescriptor = descriptor;
                return $"resolved:{descriptor.Table}:{descriptor.Key}";
            }

            public void RaiseLocaleChanged()
            {
                LocaleChanged?.Invoke();
            }
        }
    }
}
