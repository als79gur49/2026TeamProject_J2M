using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class MoonBlockGeneratorVisualCueTests
    {
        private const string MoonGeneratorPrefabPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_MoonGenerator_Default.prefab";

        private const string MoonGeneratorProfilePath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Profiles/TileFeatureVisualProfile_MoonGenerator.asset";

        [Test]
        [Category("Core")]
        public void MoonBlockGeneratorGeneratedCue_UsesProductionProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<TileFeatureVisualProfile>(MoonGeneratorProfilePath);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.FeatureKind, Is.EqualTo(TileFeatureKind.MoonBlockGenerator));
            Assert.That(profile.TryGetCueBinding(TileFeatureVisualCueId.MoonBlockGenerated, out var binding), Is.True);
            Assert.That(binding.AnimatorBinding.CueId, Is.EqualTo(TileFeatureVisualCueId.MoonBlockGenerated));
            Assert.That(binding.AnimatorBinding.Kind, Is.EqualTo(TileFeatureAnimatorBindingKind.Trigger));
            Assert.That(binding.AnimatorBinding.ParameterOrStateName, Is.EqualTo("MoonBlockGenerated"));
            Assert.That(binding.SendGameplayVfx, Is.False);

            var handler = new MoonBlockGeneratorTileFeatureVisualHandler();
            Assert.That(handler.CanHandle(TileFeatureVisualCueId.MoonBlockGenerated), Is.True);

            var prefabInstance = InstantiateMoonGeneratorPrefab();
            try
            {
#pragma warning disable CS0618
                var adapter = prefabInstance.GetComponent<LegacyTileFeatureVisualCueAdapter>();
#pragma warning restore CS0618
                var target = prefabInstance.GetComponent<TileFeatureVisualTargetView>();
                var provider = prefabInstance.GetComponent<TileFeatureVisualProfileProvider>();
                var animator = prefabInstance.GetComponent<Animator>();
                Assert.That(adapter, Is.Null);
                Assert.That(target, Is.Not.Null);
                Assert.That(provider, Is.Not.Null);
                Assert.That(animator, Is.Not.Null);
                Assert.That(animator.runtimeAnimatorController, Is.Not.Null);

                var controller = animator.runtimeAnimatorController as AnimatorController;
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.parameters.Any(parameter =>
                    parameter.name == "MoonBlockGenerated" &&
                    parameter.type == AnimatorControllerParameterType.Trigger), Is.True);

                AssertProfileReference(provider, profile);
                target.Configure(100, new SurfaceCell(FaceId.Floor, 1, 1));
                var sink = prefabInstance.AddComponent<TileFeatureVisualProfileCueSink>();
                sink.Configure(target, provider);

                var handled = sink.TryHandle(new TileFeatureVisualRequest(
                    TileFeatureVisualCueId.MoonBlockGenerated,
                    100,
                    target.Cell,
                    TileFeatureKind.MoonBlockGenerator,
                    targetEntityId: 201));

                Assert.That(handled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(prefabInstance);
            }
        }

        [Test]
        [Category("Core")]
        public void MoonBlockGeneratorBlockedCue_ReportsReasonOrPlaysConfiguredBinding()
        {
            var profile = AssetDatabase.LoadAssetAtPath<TileFeatureVisualProfile>(MoonGeneratorProfilePath);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.TryGetCueBinding(TileFeatureVisualCueId.MoonBlockGeneratorBlocked, out var binding), Is.True);
            Assert.That(binding.AnimatorBinding.CueId, Is.EqualTo(TileFeatureVisualCueId.None));
            Assert.That(binding.SendGameplayVfx, Is.False);

            var handler = new MoonBlockGeneratorTileFeatureVisualHandler();
            Assert.That(handler.CanHandle(TileFeatureVisualCueId.MoonBlockGeneratorBlocked), Is.True);

            var prefabInstance = InstantiateMoonGeneratorPrefab();
            try
            {
#pragma warning disable CS0618
                var adapter = prefabInstance.GetComponent<LegacyTileFeatureVisualCueAdapter>();
#pragma warning restore CS0618
                var target = prefabInstance.GetComponent<TileFeatureVisualTargetView>();
                var provider = prefabInstance.GetComponent<TileFeatureVisualProfileProvider>();
                var animator = prefabInstance.GetComponent<Animator>();
                var controller = animator.runtimeAnimatorController as AnimatorController;
                Assert.That(adapter, Is.Null);
                Assert.That(target, Is.Not.Null);
                Assert.That(provider, Is.Not.Null);
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.parameters.Any(parameter => parameter.name == "MoonBlockGeneratorBlocked"), Is.False);
                AssertProfileReference(provider, profile);

                var blockedCell = new SurfaceCell(FaceId.Floor, 12, 4);
                var payload = new MoonBlockGeneratorBlockedPayload(
                    MoonBlockGeneratorBlockedReason.UnitOccupant,
                    blockingEntityId: 10,
                    blockedCell);
                target.Configure(100, blockedCell);
                var sink = prefabInstance.AddComponent<TileFeatureVisualProfileCueSink>();
                sink.Configure(target, provider);

                var handled = sink.TryHandle(new TileFeatureVisualRequest(
                    TileFeatureVisualCueId.MoonBlockGeneratorBlocked,
                    100,
                    blockedCell,
                    TileFeatureKind.MoonBlockGenerator,
                    moonBlockGeneratorBlockedPayload: payload));

                Assert.That(handled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(prefabInstance);
            }
        }

        [Test]
        [Category("Core")]
        public void MoonBlockGenerated_DoesNotRequireTileFeatureVisualTargetViewLegacyString()
        {
            var targetType = typeof(TileFeatureVisualTargetView);
            var members = targetType.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.That(members.Select(member => member.Name), Has.None.Contains("moonBlockGeneratedTriggerName"));
            Assert.That(members.Select(member => member.Name), Has.None.Contains("moonBlockGeneratorBlockedTriggerName"));
            Assert.That(members.Select(member => member.Name), Has.None.Contains("PlayMoonBlockGenerated"));
            Assert.That(members.Select(member => member.Name), Has.None.Contains("PlayMoonBlockGeneratorBlocked"));

            var targetInterfaces = targetType.GetInterfaces();
            Assert.That(targetInterfaces, Has.None.EqualTo(typeof(IMoonBlockGeneratedVisualTarget)));
            Assert.That(targetInterfaces, Has.None.EqualTo(typeof(IMoonBlockGeneratorBlockedVisualTarget)));
        }

        private static GameObject InstantiateMoonGeneratorPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MoonGeneratorPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            return Object.Instantiate(prefab);
        }

        private static void AssertProfileReference(TileFeatureVisualProfileProvider provider, TileFeatureVisualProfile profile)
        {
            var serialized = new SerializedObject(provider);
            var profiles = serialized.FindProperty("profiles");
            Assert.That(profiles, Is.Not.Null);
            Assert.That(profiles.arraySize, Is.GreaterThanOrEqualTo(1));
            Assert.That(profiles.GetArrayElementAtIndex(0).objectReferenceValue, Is.SameAs(profile));
        }
    }
}
