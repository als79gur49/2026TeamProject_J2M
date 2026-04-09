using System;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class StaticEntityPresentationCatalogResolverTests
    {
        [Test]
        public void StaticEntityPresentationCatalogResolver_DuplicatePresentationIdsAfterTrim_Throws()
        {
            var catalog = ScriptableObject.CreateInstance<StaticEntityPresentationCatalog>();
            var prefabObjectA = new GameObject("StaticEntityPresentationCatalogResolver_DuplicatePresentationIdsAfterTrim_Throws_A");
            var prefabObjectB = new GameObject("StaticEntityPresentationCatalogResolver_DuplicatePresentationIdsAfterTrim_Throws_B");

            try
            {
                PlayerViewPrefabTestUtility.SetSerializedField(
                    catalog,
                    "entries",
                    new[]
                    {
                        new StaticEntityPresentationCatalogEntry
                        {
                            PresentationId = " crate ",
                            ViewPrefab = prefabObjectA.AddComponent<GameplayEntityView>(),
                        },
                        new StaticEntityPresentationCatalogEntry
                        {
                            PresentationId = "crate",
                            ViewPrefab = prefabObjectB.AddComponent<GameplayEntityView>(),
                        },
                    });

                var exception = Assert.Throws<InvalidOperationException>(
                    () => StaticEntityPresentationCatalogResolver.BuildStaticViewPrefabs(
                        catalog,
                        new[]
                        {
                            new StaticEntityPresentationBinding
                            {
                                EntityId = 20,
                                PresentationId = "crate",
                            },
                        },
                        "StaticResolverOwner"));

                StringAssert.Contains("StaticResolverOwner", exception.Message);
                StringAssert.Contains("duplicate static entity presentation ids", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabObjectB);
                UnityEngine.Object.DestroyImmediate(prefabObjectA);
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void StaticEntityPresentationCatalogResolver_DuplicateEntityIdsInBindings_Throws()
        {
            var catalog = ScriptableObject.CreateInstance<StaticEntityPresentationCatalog>();
            var prefabObject = new GameObject("StaticEntityPresentationCatalogResolver_DuplicateEntityIdsInBindings_Throws");

            try
            {
                PlayerViewPrefabTestUtility.SetSerializedField(
                    catalog,
                    "entries",
                    new[]
                    {
                        new StaticEntityPresentationCatalogEntry
                        {
                            PresentationId = "crate",
                            ViewPrefab = prefabObject.AddComponent<GameplayEntityView>(),
                        },
                    });

                var exception = Assert.Throws<InvalidOperationException>(
                    () => StaticEntityPresentationCatalogResolver.BuildStaticViewPrefabs(
                        catalog,
                        new[]
                        {
                            new StaticEntityPresentationBinding
                            {
                                EntityId = 20,
                                PresentationId = "crate",
                            },
                            new StaticEntityPresentationBinding
                            {
                                EntityId = 20,
                                PresentationId = "crate",
                            },
                        },
                        "StaticResolverOwner"));

                StringAssert.Contains("StaticResolverOwner", exception.Message);
                StringAssert.Contains("duplicate entity ids", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(prefabObject);
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }
    }
}
