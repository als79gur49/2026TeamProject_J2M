using System;
using System.IO;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Infrastructure
{
    public sealed class SnapshotEntityLogicProviderPrefilterInfrastructureTests
    {
        [Test]
        [Category("Extended")]
        public void CandidateArrays_UseConcreteConstructorCacheAndUnknownTypesFallBackToFullRegistrationOrder()
        {
            var first = new SelectiveFactory(EntityType.Unit);
            var second = new UnscopedFactory();
            var provider = new SnapshotEntityLogicProvider(new IEntityLogicFactory[] { first, second, first });
            var resolver = typeof(SnapshotEntityLogicProvider).GetMethod(
                "ResolveCandidateFactories",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(resolver, Is.Not.Null);
            Assert.That(resolver.ReturnType, Is.EqualTo(typeof(IEntityLogicFactory[])));
            var unknown = (IEntityLogicFactory[])resolver.Invoke(provider, new object[] { (EntityType)int.MaxValue });
            Assert.That(unknown, Has.Length.EqualTo(3));
            Assert.That(unknown[0], Is.SameAs(first));
            Assert.That(unknown[1], Is.SameAs(second));
            Assert.That(unknown[2], Is.SameAs(first));
            CollectionAssert.AreEqual(
                new[]
                {
                    EntityType.None, EntityType.None,
                    EntityType.Unit, EntityType.Unit,
                    EntityType.Box, EntityType.Box,
                },
                first.AuditedTypes);

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            var source = File.ReadAllText(Path.Combine(
                projectRoot,
                "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/SnapshotEntityLogicProvider.cs"));
            Assert.That(CountOccurrences(source, "BuildCandidateFactoriesForKnownType("), Is.EqualTo(4));
            var buildStart = source.IndexOf("public EntityLogicSet Build(", StringComparison.Ordinal);
            var resolverStart = source.IndexOf("private IEntityLogicFactory[] ResolveCandidateFactories(", StringComparison.Ordinal);
            var buildSource = source.Substring(buildStart, resolverStart - buildStart);
            Assert.That(buildSource, Does.Contain("ResolveCandidateFactories(entity.type)"));
            Assert.That(buildSource, Does.Not.Contain("new List<IEntityLogicFactory>"));
            Assert.That(buildSource, Does.Not.Contain(".ToArray()"));
        }

        private static int CountOccurrences(string source, string value)
        {
            var count = 0;
            for (var index = 0; (index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0; index += value.Length)
            {
                count++;
            }

            return count;
        }

        private sealed class SelectiveFactory : IEntityLogicFactory, IEntityLogicFactoryEntityTypePrefilter
        {
            private readonly EntityType _supportedType;

            public SelectiveFactory(EntityType supportedType) => _supportedType = supportedType;

            public System.Collections.Generic.List<EntityType> AuditedTypes { get; } = new();

            public bool MayCreateForEntityType(EntityType entityType)
            {
                AuditedTypes.Add(entityType);
                return entityType == _supportedType;
            }

            public bool CanCreate(in EntityLogicCreationContext context) => context.Entity.type == _supportedType;
            public IEntityLogic Create(in EntityLogicCreationContext context) => new NoPhaseLogic();
        }

        private sealed class UnscopedFactory : IEntityLogicFactory
        {
            public bool CanCreate(in EntityLogicCreationContext context) => true;
            public IEntityLogic Create(in EntityLogicCreationContext context) => new NoPhaseLogic();
        }

        private sealed class NoPhaseLogic : IEntityLogic
        {
        }
    }
}
