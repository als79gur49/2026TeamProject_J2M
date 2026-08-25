using System;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class BackgroundSpaceOrbitTests
    {
        private const string OrbitPrefabPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Background/Prefabs/ExhibitionSpaceOrbit.prefab";
        private const string BackgroundVariantPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Background/Prefabs/WallRoot_Default_Exhibition.prefab";
        private static readonly string[] BackgroundVariantPaths =
        {
            BackgroundVariantPath,
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Background/Prefabs/WallRoot_AGate_Exhibition.prefab",
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Background/Prefabs/WallRoot_BGate_Exhibition.prefab",
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Background/Prefabs/WallRoot_Lv4Gate_Exhibition.prefab",
        };
        private const string StageContentRoot =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/Levels/level-01/Stages";
        private static readonly StageVariantExpectation[] StageVariantExpectations =
        {
            new StageVariantExpectation("stage-0-1", BackgroundVariantPaths[0]),
            new StageVariantExpectation("stage-0-2", BackgroundVariantPaths[0]),
            new StageVariantExpectation("stage-0-3", BackgroundVariantPaths[0]),
            new StageVariantExpectation("stage-1-1", BackgroundVariantPaths[0]),
            new StageVariantExpectation("stage-1-2", BackgroundVariantPaths[0]),
            new StageVariantExpectation("stage-2-1", BackgroundVariantPaths[1]),
            new StageVariantExpectation("stage-2-2", BackgroundVariantPaths[1]),
            new StageVariantExpectation("stage-3-1", BackgroundVariantPaths[2]),
            new StageVariantExpectation("stage-3-2", BackgroundVariantPaths[2]),
            new StageVariantExpectation("stage-3-3", BackgroundVariantPaths[2]),
            new StageVariantExpectation("stage-4-1", BackgroundVariantPaths[3]),
            new StageVariantExpectation("stage-4-2", BackgroundVariantPaths[3]),
            new StageVariantExpectation("stage-4-3", BackgroundVariantPaths[3]),
        };

        [Test]
        public void Advance_RotatesOrbitLayersInOppositeDirectionsAndSpinsSaturnIndependently()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.Authoring.Initialize();
                fixture.Authoring.Advance(2f, 0f);

                AssertRotation(fixture.CelestialPivot, Quaternion.AngleAxis(0.7f, Vector3.right));
                AssertRotation(fixture.DebrisPivot, Quaternion.AngleAxis(-1.3f, Vector3.right));
                AssertRotation(fixture.SaturnSpin, Quaternion.AngleAxis(8f, Vector3.up));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture.Root);
            }
        }

        [Test]
        public void Advance_ClampsTopologyResponseAndAppliesMaximumOrbitMultiplierOnlyToPivots()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.Authoring.Initialize();
                fixture.Authoring.Advance(1f, 2f);

                AssertRotation(fixture.CelestialPivot, Quaternion.AngleAxis(0.4725f, Vector3.right));
                AssertRotation(fixture.DebrisPivot, Quaternion.AngleAxis(-0.8775f, Vector3.right));
                AssertRotation(fixture.SaturnSpin, Quaternion.AngleAxis(4f, Vector3.up));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture.Root);
            }
        }

        [Test]
        public void ResetRuntimeState_RestoresAuthoredLocalRotations()
        {
            var fixture = CreateFixture(
                Quaternion.Euler(10f, 20f, 30f),
                Quaternion.Euler(5f, 15f, 25f),
                Quaternion.Euler(0f, 45f, 0f));
            try
            {
                var celestialAuthored = fixture.CelestialPivot.localRotation;
                var debrisAuthored = fixture.DebrisPivot.localRotation;
                var saturnAuthored = fixture.SaturnSpin.localRotation;

                fixture.Authoring.Initialize();
                fixture.Authoring.Advance(5f, 1f);
                fixture.Authoring.ResetRuntimeState();

                AssertRotation(fixture.CelestialPivot, celestialAuthored);
                AssertRotation(fixture.DebrisPivot, debrisAuthored);
                AssertRotation(fixture.SaturnSpin, saturnAuthored);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture.Root);
            }
        }

        [Test]
        public void Advance_WhenDeltaTimeIsNegative_Throws()
        {
            var fixture = CreateFixture();
            try
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Authoring.Advance(-0.01f, 0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture.Root);
            }
        }

        [Test]
        public void ResolveTopologyResponse_UsesOnlyActiveAngularVelocityAndClampsIt()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var inactive = TopologyTransitionVisualState.Inactive(topology, Quaternion.Euler(90f, 0f, 0f));
            var active = new TopologyTransitionVisualState(
                isActive: true,
                progress01: 0.5f,
                sourceTopology: topology,
                destinationTopology: new CubeTopologyState(FaceId.Front),
                rotationKind: CubeRotationKind.Forward,
                durationSeconds: 1f,
                presentedVisualRotation: Quaternion.Euler(90f, 0f, 0f),
                angularVelocityNormalized: 2f);

            Assert.That(BackgroundSpaceOrbitPresenterAdapter.ResolveTopologyResponse(inactive), Is.Zero);
            Assert.That(BackgroundSpaceOrbitPresenterAdapter.ResolveTopologyResponse(active), Is.EqualTo(1f));
        }

        [Test]
        public void ResolveSingleAuthoring_AllowsZeroOrOneAndRejectsMultiple()
        {
            var root = new GameObject(nameof(ResolveSingleAuthoring_AllowsZeroOrOneAndRejectsMultiple));
            try
            {
                Assert.That(BackgroundSpaceOrbitPresenterAdapter.ResolveSingleAuthoring(root), Is.Null);

                var first = new GameObject("First").AddComponent<BackgroundSpaceOrbitAuthoring>();
                first.transform.SetParent(root.transform);
                Assert.That(BackgroundSpaceOrbitPresenterAdapter.ResolveSingleAuthoring(root), Is.SameAs(first));

                var second = new GameObject("Second").AddComponent<BackgroundSpaceOrbitAuthoring>();
                second.transform.SetParent(root.transform);
                Assert.Throws<InvalidOperationException>(
                    () => BackgroundSpaceOrbitPresenterAdapter.ResolveSingleAuthoring(root));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CampaignStagePresentations_UseExpectedExhibitionVariants()
        {
            foreach (var expectation in StageVariantExpectations)
            {
                var variant = AssetDatabase.LoadAssetAtPath<GameObject>(expectation.VariantPath);
                var presentationPath =
                    $"{StageContentRoot}/{expectation.StageId}/{expectation.StageId}_Presentation.asset";
                var presentation = AssetDatabase.LoadAssetAtPath<StagePresentationDefinition>(presentationPath);

                Assert.That(variant, Is.Not.Null, expectation.VariantPath);
                Assert.That(presentation, Is.Not.Null, presentationPath);
                Assert.That(
                    presentation.BackgroundPrefab,
                    Is.SameAs(variant),
                    $"Expected {expectation.StageId} to use {variant.name}.");
            }
        }

        [Test]
        public void ExhibitionBackgroundVariants_PreserveWallContractsAndContainOneOrbit()
        {
            foreach (var path in BackgroundVariantPaths)
            {
                var variant = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(variant, Is.Not.Null, path);
                Assert.That(PrefabUtility.GetPrefabAssetType(variant), Is.EqualTo(PrefabAssetType.Variant), path);

                var instance = PrefabUtility.InstantiatePrefab(variant) as GameObject;
                try
                {
                    Assert.That(instance, Is.Not.Null, path);
                    Assert.That(instance.GetComponent<BackgroundWallSurfaceTintAuthoring>(), Is.Not.Null, path);
                    Assert.That(instance.GetComponent<TopologyVisualBridgeVisibilityController>(), Is.Not.Null, path);
                    Assert.That(
                        instance.GetComponentsInChildren<BackgroundSpaceOrbitAuthoring>(true),
                        Has.Length.EqualTo(1),
                        path);
                }
                finally
                {
                    if (instance != null)
                    {
                        UnityEngine.Object.DestroyImmediate(instance);
                    }
                }
            }
        }

        [Test]
        public void ExhibitionOrbitPrefab_HasConfiguredPresentationContent()
        {
            var orbitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OrbitPrefabPath);

            Assert.That(orbitPrefab, Is.Not.Null);

            var instance = PrefabUtility.InstantiatePrefab(orbitPrefab) as GameObject;
            try
            {
                Assert.That(instance, Is.Not.Null);

                var authorings = instance.GetComponentsInChildren<BackgroundSpaceOrbitAuthoring>(true);
                Assert.That(authorings, Has.Length.EqualTo(1));

                var orbitRoot = authorings[0].transform;
                var celestialTilt = orbitRoot.Find("CelestialOrbitTilt");
                var celestial = celestialTilt != null
                    ? celestialTilt.Find("CelestialOrbitPivot")
                    : null;
                var debris = orbitRoot.Find("DebrisOrbitPivot");
                Assert.That(celestialTilt, Is.Not.Null);
                Assert.That(celestial, Is.Not.Null);
                Assert.That(debris, Is.Not.Null);
                Assert.That(
                    Quaternion.Angle(celestialTilt.localRotation, Quaternion.Euler(0f, -6f, 12f)),
                    Is.LessThan(0.001f));

                var blackHole = celestial.Find("BlackHole");
                var saturn = celestial.Find("Saturn");
                var comet = debris.Find("CometShower_Loop");
                var meteor = debris.Find("MeteorShower_Loop");
                Assert.That(blackHole, Is.Not.Null);
                Assert.That(saturn, Is.Not.Null);
                Assert.That(comet, Is.Not.Null);
                Assert.That(meteor, Is.Not.Null);

                Assert.That(blackHole.localPosition.z, Is.GreaterThan(0f));
                Assert.That(saturn.localPosition.z, Is.LessThan(0f));
                Assert.That(comet.localPosition.y, Is.GreaterThan(0f));
                Assert.That(meteor.localPosition.y, Is.LessThan(0f));

                var blackHoleRenderer = blackHole.GetComponent<MeshRenderer>();
                Assert.That(blackHole.GetComponent<Collider>(), Is.Null);
                Assert.That(blackHoleRenderer, Is.Not.Null);
                Assert.That(
                    blackHoleRenderer.shadowCastingMode,
                    Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
                Assert.That(blackHoleRenderer.receiveShadows, Is.False);
                Assert.That(blackHoleRenderer.sharedMaterial.GetFloat("_DiskEmission"), Is.GreaterThan(0f));
                Assert.That(blackHoleRenderer.sharedMaterial.GetFloat("_PhotonEmission"), Is.GreaterThan(0f));
                Assert.That(blackHoleRenderer.sharedMaterial.GetFloat("_LensStrength"), Is.LessThan(0f));

                Assert.That(comet.GetComponent<ParticleSystem>().main.prewarm, Is.True);
                foreach (var particleSystem in meteor.GetComponentsInChildren<ParticleSystem>(true))
                {
                    Assert.That(particleSystem.shape.scale.x, Is.GreaterThan(0f));
                }

                var cometTextureImporter = AssetImporter.GetAtPath(
                    "Assets/3DM/전시회/Background/Comet/Comet_Texture.png") as TextureImporter;
                Assert.That(cometTextureImporter, Is.Not.Null);
                Assert.That(cometTextureImporter.mipmapEnabled, Is.True);
            }
            finally
            {
                if (instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
        }

        private static OrbitFixture CreateFixture(
            Quaternion? celestialRotation = null,
            Quaternion? debrisRotation = null,
            Quaternion? saturnRotation = null)
        {
            var root = new GameObject("OrbitRoot");
            var celestial = CreateChild(root.transform, "Celestial", celestialRotation ?? Quaternion.identity);
            var debris = CreateChild(root.transform, "Debris", debrisRotation ?? Quaternion.identity);
            var saturn = CreateChild(celestial, "Saturn", saturnRotation ?? Quaternion.identity);
            var authoring = root.AddComponent<BackgroundSpaceOrbitAuthoring>();

            SetPrivateField(authoring, "celestialOrbitPivot", celestial);
            SetPrivateField(authoring, "debrisOrbitPivot", debris);
            SetPrivateField(authoring, "saturnSelfSpinTarget", saturn);
            SetPrivateField(authoring, "celestialDegreesPerSecond", 0.35f);
            SetPrivateField(authoring, "debrisDegreesPerSecond", -0.65f);
            SetPrivateField(authoring, "saturnDegreesPerSecond", 4f);
            SetPrivateField(authoring, "maximumTopologySpeedMultiplier", 1.35f);

            return new OrbitFixture(root, authoring, celestial, debris, saturn);
        }

        private static Transform CreateChild(Transform parent, string name, Quaternion localRotation)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localRotation = localRotation;
            return child;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            field.SetValue(target, value);
        }

        private static void AssertRotation(Transform target, Quaternion expected)
        {
            Assert.That(Quaternion.Angle(target.localRotation, expected), Is.LessThan(0.001f));
        }

        private readonly struct StageVariantExpectation
        {
            public StageVariantExpectation(string stageId, string variantPath)
            {
                StageId = stageId;
                VariantPath = variantPath;
            }

            public string StageId { get; }
            public string VariantPath { get; }
        }

        private readonly struct OrbitFixture
        {
            public OrbitFixture(
                GameObject root,
                BackgroundSpaceOrbitAuthoring authoring,
                Transform celestialPivot,
                Transform debrisPivot,
                Transform saturnSpin)
            {
                Root = root;
                Authoring = authoring;
                CelestialPivot = celestialPivot;
                DebrisPivot = debrisPivot;
                SaturnSpin = saturnSpin;
            }

            public GameObject Root { get; }
            public BackgroundSpaceOrbitAuthoring Authoring { get; }
            public Transform CelestialPivot { get; }
            public Transform DebrisPivot { get; }
            public Transform SaturnSpin { get; }
        }
    }
}
