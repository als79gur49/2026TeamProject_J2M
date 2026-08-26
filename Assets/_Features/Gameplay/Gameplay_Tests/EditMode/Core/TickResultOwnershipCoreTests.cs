using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class TickResultOwnershipCoreTests
    {
        [Test]
        [Category("Core")]
        public void GeneralEnumerableConstructors_DefensivelyCopyMutableFinalEntityInputs()
        {
            var source = new List<EntityState> { CreateEntity(10) };
            var data = new TickResultData(source, Array.Empty<DelayedAttackEffectRecord>(), Array.Empty<string>());
            var result = new TickResult(
                1,
                CompletedPhases(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                source,
                Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                string.Empty,
                TickTrace.Empty);

            source[0] = CreateEntity(99);
            source.Add(CreateEntity(20));

            Assert.That(data.FinalEntities.Count, Is.EqualTo(1));
            Assert.That(data.FinalEntities[0].entityId, Is.EqualTo(10));
            Assert.That(result.FinalEntities.Count, Is.EqualTo(1));
            Assert.That(result.FinalEntities[0].entityId, Is.EqualTo(10));
        }

        [Test]
        [Category("Core")]
        public void CreateFromOwnedData_SharesOwnedWrapper_AndRecordsOneCopyOneShare()
        {
            TickResultData data;
            TickResult result;
            GameplayTickWorkloadCounts counts;

            using (var capture = GameplayTickWorkloadDiagnostics.BeginCapture())
            {
                data = new TickResultData(
                    new[] { CreateEntity(10) },
                    Array.Empty<DelayedAttackEffectRecord>(),
                    new[] { "event" });
                result = CreateFromOwnedData(data);
                counts = capture.Counts;
            }

            Assert.That(result.FinalEntities, Is.SameAs(data.FinalEntities));
            Assert.That(counts.FinalEntityDefensiveCopyCount, Is.EqualTo(1));
            Assert.That(counts.FinalEntityDefensiveCopiedItemCount, Is.EqualTo(1));
            Assert.That(counts.TickResultFinalEntityTrustedShareCount, Is.EqualTo(1));
            Assert.That(counts.TickResultFinalEntityTrustedSharedItemCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void TrustedFinalEntities_AreReadOnlyAndDefensiveOriginCannotMutateEitherResult()
        {
            var source = new List<EntityState> { CreateEntity(10) };
            var data = new TickResultData(source, Array.Empty<DelayedAttackEffectRecord>(), Array.Empty<string>());
            var result = CreateFromOwnedData(data);
            source.Clear();

            var dataList = (IList<EntityState>)data.FinalEntities;
            var resultList = (IList<EntityState>)result.FinalEntities;

            Assert.Throws<NotSupportedException>(() => dataList.Add(CreateEntity(20)));
            Assert.Throws<NotSupportedException>(() => resultList.RemoveAt(0));
            Assert.Throws<NotSupportedException>(() => resultList[0] = CreateEntity(99));
            Assert.That(data.FinalEntities.Count, Is.EqualTo(1));
            Assert.That(result.FinalEntities.Count, Is.EqualTo(1));
            Assert.That(data.FinalEntities[0].entityId, Is.EqualTo(10));
            Assert.That(result.FinalEntities[0].entityId, Is.EqualTo(10));
        }

        [Test]
        [Category("Core")]
        public void CreateFromOwnedFinalEntities_WrapsWithoutDefensiveCopy()
        {
            var owned = new List<EntityState> { CreateEntity(10) };
            TickResultData data;
            GameplayTickWorkloadCounts counts;

            using (var capture = GameplayTickWorkloadDiagnostics.BeginCapture())
            {
                data = TickResultData.CreateFromOwnedFinalEntities(
                    owned,
                    Array.Empty<DelayedAttackEffectRecord>(),
                    Array.Empty<string>(),
                    TickPresentationData.Empty);
                counts = capture.Counts;
            }

            Assert.That(counts.FinalEntityDefensiveCopyCount, Is.Zero);
            Assert.That(counts.FinalEntityOwnedWrapperCreationCount, Is.EqualTo(1));
            Assert.That(counts.FinalEntityOwnedWrapperItemCount, Is.EqualTo(1));
            Assert.Throws<NotSupportedException>(
                () => ((IList<EntityState>)data.FinalEntities).Add(CreateEntity(20)));
            Assert.That(data.FinalEntities[0].entityId, Is.EqualTo(10));
        }

        [Test]
        [Category("Core")]
        public void BuilderOwnedResult_RemainsImmutableAfterWorldChangesAndNextTick()
        {
            var worldState = GameplayCompositionRoot.CreateWorldState(
                new[] { CreateEntity(10) },
                new BoardBounds(new Vector2Int(-2, -2), new Vector2Int(4, 4)),
                new CubeTopologyState(FaceId.Floor));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);
            var first = pipeline.RunTick(new TickInput(1));

            var writeContext = worldState.CreateWriteContext();
            writeContext.RemoveEntity(10);
            writeContext.SpawnEntity(CreateEntity(20));
            var second = pipeline.RunTick(new TickInput(2));

            CollectionAssert.AreEqual(new[] { 10 }, first.FinalEntities.Select(entity => entity.entityId));
            CollectionAssert.AreEqual(new[] { 20 }, second.FinalEntities.Select(entity => entity.entityId));
        }

        [Test]
        [Category("Core")]
        public void OwnedFinalEntitiesFactory_ProductionCallerIsOnlyTickResultBuilder()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            var gameplayRoot = Path.Combine(projectRoot, "Assets/_Features/Gameplay");
            const string invocation = "TickResultData.CreateFromOwnedFinalEntities(";

            var callers = Directory.GetFiles(gameplayRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => path.IndexOf("Gameplay_Tests", StringComparison.Ordinal) < 0)
                .SelectMany(path => Enumerable.Repeat(path, CountOccurrences(File.ReadAllText(path), invocation)))
                .ToArray();

            Assert.That(callers, Has.Length.EqualTo(1));
            Assert.That(Path.GetFileName(callers[0]), Is.EqualTo("TickResultBuilder.cs"));
        }

        private static TickResult CreateFromOwnedData(TickResultData data)
        {
            return TickResult.CreateFromOwnedData(
                1,
                CompletedPhases(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                data,
                new CubeTopologyState(FaceId.Floor),
                string.Empty,
                TickTrace.Empty);
        }

        private static TickPhase[] CompletedPhases()
        {
            return new[]
            {
                TickPhase.Plan,
                TickPhase.Resolve,
                TickPhase.Finalize,
                TickPhase.Cleanup,
                TickPhase.Respawn,
            };
        }

        private static EntityState CreateEntity(int entityId)
        {
            return new EntityState
            {
                entityId = entityId,
                position = new SurfaceCell(FaceId.Floor, entityId == 20 ? 1 : 0, 0),
                hp = 1,
                maxHp = 1,
                teamId = 1,
                type = EntityType.Unit,
                boardPresence = EntityBoardPresence.Occupying,
                facing = Direction.Right,
            };
        }

        private static int CountOccurrences(string source, string value)
        {
            var count = 0;
            var startIndex = 0;
            while ((startIndex = source.IndexOf(value, startIndex, StringComparison.Ordinal)) >= 0)
            {
                count++;
                startIndex += value.Length;
            }

            return count;
        }
    }
}
