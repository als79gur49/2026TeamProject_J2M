using System;
using System.Collections.Generic;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class CampaignStageSequenceResolverSnapshotTests
    {
        private CampaignStageSequenceDefinition _definition;

        [TearDown]
        public void TearDown()
        {
            if (_definition != null)
            {
                UnityEngine.Object.DestroyImmediate(_definition);
            }
        }

        [Test]
        [Category("Core")]
        public void Constructor_CopiesStageOrderAndGroups_IndependentlyOfSourceMutation()
        {
            var first = CreateEntry("fixture-a", "group-a");
            var middle = CreateEntry("fixture-c", "group-b");
            var final = CreateEntry("fixture-b", "group-b");
            var resolver = CreateResolver(first, middle, final);

            first.Set(StageId.CreateOrThrow("mutated-stage"), "mutated-group");
            middle.Set(StageId.CreateOrThrow("mutated-middle"), "mutated-group");
            _definition.SetEntries(new[] { final, middle, first });

            Assert.That(resolver.FirstStageId, Is.EqualTo(StageId.CreateOrThrow("fixture-a")));
            Assert.That(resolver.FinalStageId, Is.EqualTo(StageId.CreateOrThrow("fixture-b")));
            Assert.That(
                resolver.TryGetNext(StageId.CreateOrThrow("fixture-a"), out var nextStageId),
                Is.True);
            Assert.That(nextStageId, Is.EqualTo(StageId.CreateOrThrow("fixture-c")));
            Assert.That(
                resolver.GetLevelGroupId(StageId.CreateOrThrow("fixture-c")),
                Is.EqualTo("group-b"));
            Assert.That(resolver.Contains(StageId.CreateOrThrow("mutated-stage")), Is.False);
        }

        [Test]
        [Category("Core")]
        public void Entries_AreReadOnlyValueSnapshots()
        {
            var resolver = CreateResolver(
                CreateEntry("fixture-a", "group-a"),
                CreateEntry("fixture-b", "group-b"));
            var mutableView = resolver.Entries as IList<CampaignStageSequenceSnapshotEntry>;

            Assert.That(mutableView, Is.Not.Null);
            Assert.Throws<NotSupportedException>(() =>
                mutableView[0] = mutableView[1]);
            Assert.That(resolver.FirstStageId, Is.EqualTo(StageId.CreateOrThrow("fixture-a")));
        }

        [Test]
        [Category("Core")]
        public void Constructor_RejectsNullOrMalformedSources()
        {
            Assert.Throws<ArgumentNullException>(() => new CampaignStageSequenceResolver(null));

            _definition = ScriptableObject.CreateInstance<CampaignStageSequenceDefinition>();
            _definition.SetEntries(Array.Empty<CampaignStageSequenceEntry>());
            Assert.Throws<ArgumentException>(() => new CampaignStageSequenceResolver(_definition));

            _definition.SetEntries(new CampaignStageSequenceEntry[] { null });
            Assert.Throws<ArgumentException>(() => new CampaignStageSequenceResolver(_definition));

            _definition.SetEntries(new[] { CreateEntry(StageId.None, "group-a") });
            Assert.Throws<ArgumentException>(() => new CampaignStageSequenceResolver(_definition));

            _definition.SetEntries(new[]
            {
                CreateEntry("fixture-a", "group-a"),
                CreateEntry("fixture-a", "group-b"),
            });
            Assert.Throws<ArgumentException>(() => new CampaignStageSequenceResolver(_definition));

            _definition.SetEntries(new[] { CreateEntry("fixture-a", "Invalid Group") });
            Assert.Throws<ArgumentException>(() => new CampaignStageSequenceResolver(_definition));
        }

        [Test]
        [Category("Core")]
        public void CompletionBuilder_UsesInjectedDivergentSequenceForNextAndFinal()
        {
            var resolver = CreateResolver(
                CreateEntry("fixture-a", "group-a"),
                CreateEntry("fixture-c", "group-b"),
                CreateEntry("fixture-b", "group-b"));

            var firstReadModel = MinimalStageCompletionReadModelBuilder.Build(
                entry: null,
                clearResult: CreateClearResult("fixture-a"),
                sequenceResolver: resolver);
            var finalReadModel = MinimalStageCompletionReadModelBuilder.Build(
                entry: null,
                clearResult: CreateClearResult("fixture-b"),
                sequenceResolver: resolver);

            Assert.That(
                firstReadModel.NextStageRequest.StageId,
                Is.EqualTo(StageId.CreateOrThrow("fixture-c")));
            Assert.That(firstReadModel.NextStageRequest.NavigationKind, Is.EqualTo(StageNavigationKind.NextStage));
            Assert.That(finalReadModel.NextStageRequest, Is.EqualTo(StageNavigationRequest.None));
        }

        private CampaignStageSequenceResolver CreateResolver(params CampaignStageSequenceEntry[] entries)
        {
            _definition = ScriptableObject.CreateInstance<CampaignStageSequenceDefinition>();
            _definition.SetEntries(entries);
            return new CampaignStageSequenceResolver(_definition);
        }

        private static CampaignStageSequenceEntry CreateEntry(string stageId, string levelGroupId)
        {
            return CreateEntry(StageId.CreateOrThrow(stageId), levelGroupId);
        }

        private static CampaignStageSequenceEntry CreateEntry(StageId stageId, string levelGroupId)
        {
            var entry = new CampaignStageSequenceEntry();
            entry.Set(
                stageId,
                levelGroupId);
            return entry;
        }

        private static StageClearResult CreateClearResult(string stageId)
        {
            return new StageClearResult(
                StageId.CreateOrThrow(stageId),
                finalTickIndex: 1);
        }
    }
}
