using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Infrastructure
{
    public sealed class TickResultOwnershipInfrastructureTests
    {
        [Test]
        [Category("Extended")]
        public void OwnedFinalEntitiesFactory_HasExactWrapperBoundaryAndOneProductionCaller()
        {
            var field = typeof(TickResultData).GetField(
                "_finalEntities",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            Assert.That(field.FieldType, Is.EqualTo(typeof(ReadOnlyCollection<EntityState>)));

            var factory = typeof(TickResultData).GetMethod(
                "CreateFromOwnedFinalEntities",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(factory, Is.Not.Null);
            Assert.That(
                factory.GetParameters()[0].ParameterType,
                Is.EqualTo(typeof(ReadOnlyCollection<EntityState>)));

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

        private static int CountOccurrences(string source, string value)
        {
            var count = 0;
            for (var index = 0; (index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0; index += value.Length)
            {
                count++;
            }

            return count;
        }
    }
}
