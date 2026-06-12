using System.IO;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class ExitProductionProfileMigrationTests
    {
        private const string Exit3x3PrefabPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Exit_3x3.prefab";

        private const string ExitDefaultPrefabPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_Exit_Default.prefab";

        private const string ExitProfilePath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Profiles/TileFeatureVisualProfile_Exit.asset";

        private const string ExitCatalogPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Catalogs/TileFeaturePresentationCatalog_CampaignMainBoard.asset";

        private const string TileFeatureVisualTargetViewPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/TileFeatureVisualTargetView.cs";

        [Test]
        [Category("Full")]
        public void Exit3x3_ProductionProfileMigration_AnimatorBindingsAreProfileLocal()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Exit3x3PrefabPath);
            var profile = AssetDatabase.LoadAssetAtPath<TileFeatureVisualProfile>(ExitProfilePath);

            Assert.That(prefab, Is.Not.Null, Exit3x3PrefabPath);
            Assert.That(profile, Is.Not.Null, ExitProfilePath);
            Assert.That(profile.FeatureKind, Is.EqualTo(TileFeatureKind.Exit));

            var targetView = prefab.GetComponent<TileFeatureVisualTargetView>();
            var adapter = prefab.GetComponent<LegacyTileFeatureVisualCueAdapter>();
            var animator = prefab.GetComponentInChildren<Animator>(includeInactive: true);
            Assert.That(targetView, Is.Not.Null, Exit3x3PrefabPath);
            Assert.That(adapter, Is.Not.Null, Exit3x3PrefabPath);
            Assert.That(animator, Is.Not.Null, Exit3x3PrefabPath);
            Assert.That(PrefabReferencesProfile(adapter, profile), Is.True);

            var diagnostics = TileFeatureVisualBindingDiagnostics.ForProfile(profile, targetView);
            Assert.That(diagnostics.IsValid, Is.True, string.Join("\n", diagnostics.Messages));

            var controller = animator.runtimeAnimatorController as AnimatorController;
            Assert.That(controller, Is.Not.Null);
            AssertAnimatorParameter(controller, "ExitOpened", AnimatorControllerParameterType.Trigger);
            AssertAnimatorParameter(controller, "ExitEntered", AnimatorControllerParameterType.Trigger);
            AssertAnimatorParameter(controller, "ExitOpen", AnimatorControllerParameterType.Bool);

            var stateNames = controller.layers[0].stateMachine.states
                .Select(childState => childState.state.name)
                .ToArray();
            Assert.That(stateNames, Does.Contain("ExitOpenedIdle"));
            Assert.That(stateNames, Does.Contain("ExitClosedIdle"));

            AssertCueAnimatorBinding(
                profile,
                TileFeatureVisualCueId.ExitOpened,
                TileFeatureAnimatorBindingKind.Trigger,
                "ExitOpened",
                controller,
                AnimatorControllerParameterType.Trigger);
            AssertCueAnimatorBinding(
                profile,
                TileFeatureVisualCueId.ExitEntered,
                TileFeatureAnimatorBindingKind.Trigger,
                "ExitEntered",
                controller,
                AnimatorControllerParameterType.Trigger);
            AssertCueAnimatorBinding(
                profile,
                TileFeatureVisualCueId.ExitOpenState,
                TileFeatureAnimatorBindingKind.Bool,
                "ExitOpen",
                controller,
                AnimatorControllerParameterType.Bool);
            AssertExitOpenStateBindings(profile, stateNames);

            var targetViewSource = File.ReadAllText(TileFeatureVisualTargetViewPath);
            var prefabSource = File.ReadAllText(Exit3x3PrefabPath);
            Assert.That(targetViewSource, Does.Not.Contain("exitOpenedTriggerName"));
            Assert.That(targetViewSource, Does.Not.Contain("exitEnteredTriggerName"));
            Assert.That(targetViewSource, Does.Not.Contain("exitOpenBoolName"));
            Assert.That(targetViewSource, Does.Not.Contain("exitOpenedStateName"));
            Assert.That(targetViewSource, Does.Not.Contain("exitClosedStateName"));
            Assert.That(targetViewSource, Does.Not.Contain("PlayExit"));
            Assert.That(prefabSource, Does.Not.Contain("exitOpenedTriggerName"));
            Assert.That(prefabSource, Does.Not.Contain("exitEnteredTriggerName"));
            Assert.That(prefabSource, Does.Not.Contain("exitOpenBoolName"));
            Assert.That(prefabSource, Does.Not.Contain("exitOpenedStateName"));
            Assert.That(prefabSource, Does.Not.Contain("exitClosedStateName"));
        }

        [Test]
        [Category("Full")]
        public void ExitOpened_DoesNotDependOnLegacyAdapterFallback()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Exit3x3PrefabPath);
            Assert.That(prefab, Is.Not.Null, Exit3x3PrefabPath);

            var instance = Object.Instantiate(prefab);
            try
            {
                var adapter = instance.GetComponent<LegacyTileFeatureVisualCueAdapter>();
                Assert.That(adapter, Is.Not.Null);

                adapter.PlayExitOpened();
                adapter.PlayExitEntered(playerEntityId: 20);
                adapter.SetExitOpenImmediate(true);

                Assert.That(adapter.DebugExitOpenedCount, Is.EqualTo(1));
                Assert.That(adapter.DebugExitEnteredCount, Is.EqualTo(1));
                Assert.That(adapter.DebugExitOpen, Is.True);
                Assert.That(adapter.DebugExitLegacyAnimatorFallbackCount, Is.Zero);
                Assert.That(adapter.DebugLegacyAnimatorFallbackCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        [Category("Full")]
        public void ExitDefault_NoAnimatorNoProfile_IsExplicitVfxOnlyNoOp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExitDefaultPrefabPath);
            Assert.That(prefab, Is.Not.Null, ExitDefaultPrefabPath);
            Assert.That(prefab.GetComponent<TileFeatureVisualTargetView>(), Is.Not.Null);
            Assert.That(prefab.GetComponentInChildren<Animator>(includeInactive: true), Is.Null);
            Assert.That(prefab.GetComponent<LegacyTileFeatureVisualCueAdapter>(), Is.Null);

            var prefabSource = File.ReadAllText(ExitDefaultPrefabPath);
            Assert.That(prefabSource, Does.Not.Contain("profiles:"));
            Assert.That(prefabSource, Does.Not.Contain("exitOpenedTriggerName"));
            Assert.That(prefabSource, Does.Not.Contain("exitOpenBoolName"));
        }

        [Test]
        [Category("Full")]
        public void Exit3x3_ProductionCatalogBinding_PreservesThreeByThreeSameFaceFootprint()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<TileFeaturePresentationCatalog>(ExitCatalogPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Exit3x3PrefabPath);
            Assert.That(catalog, Is.Not.Null, ExitCatalogPath);
            Assert.That(prefab, Is.Not.Null, Exit3x3PrefabPath);

            var entry = catalog.Entries.Single(candidate => candidate.PresentationKey == "exit.default");
            Assert.That(entry.Kind, Is.EqualTo(TileFeatureKind.Exit));
            Assert.That(entry.VisualPrefab, Is.SameAs(prefab));
            Assert.That(entry.PlacementMode, Is.EqualTo(TileFeatureVisualPlacementMode.ReplaceBaseTile));
            Assert.That(entry.FootprintMode, Is.EqualTo(TileFeatureVisualFootprintMode.ThreeByThreeSameFace));
        }

        private static bool PrefabReferencesProfile(
            LegacyTileFeatureVisualCueAdapter adapter,
            TileFeatureVisualProfile expectedProfile)
        {
            var serializedAdapter = new SerializedObject(adapter);
            var profiles = serializedAdapter.FindProperty("profiles");
            if (profiles == null || !profiles.isArray)
            {
                return false;
            }

            for (var i = 0; i < profiles.arraySize; i++)
            {
                if (profiles.GetArrayElementAtIndex(i).objectReferenceValue == expectedProfile)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssertCueAnimatorBinding(
            TileFeatureVisualProfile profile,
            TileFeatureVisualCueId cueId,
            TileFeatureAnimatorBindingKind expectedBindingKind,
            string expectedParameterName,
            AnimatorController controller,
            AnimatorControllerParameterType expectedParameterType)
        {
            Assert.That(profile.TryGetCueBinding(cueId, out var binding), Is.True, $"{profile.name}:{cueId}");
            Assert.That(binding.AnimatorBinding.CueId, Is.EqualTo(cueId));
            Assert.That(binding.AnimatorBinding.Kind, Is.EqualTo(expectedBindingKind));
            Assert.That(binding.AnimatorBinding.ParameterOrStateName, Is.EqualTo(expectedParameterName));
            Assert.That(binding.AnimatorBinding.Hash, Is.Not.Zero);
            AssertAnimatorParameter(controller, expectedParameterName, expectedParameterType);
        }

        private static void AssertExitOpenStateBindings(
            TileFeatureVisualProfile profile,
            string[] stateNames)
        {
            Assert.That(
                profile.TryGetCueBinding(TileFeatureVisualCueId.ExitOpenState, out var binding),
                Is.True,
                profile.name);
            Assert.That(binding.ActiveStateAnimatorBinding.CueId, Is.EqualTo(TileFeatureVisualCueId.ExitOpenState));
            Assert.That(binding.ActiveStateAnimatorBinding.Kind, Is.EqualTo(TileFeatureAnimatorBindingKind.State));
            Assert.That(binding.ActiveStateAnimatorBinding.ParameterOrStateName, Is.EqualTo("ExitOpenedIdle"));
            Assert.That(binding.ActiveStateAnimatorBinding.Hash, Is.Not.Zero);
            Assert.That(binding.InactiveStateAnimatorBinding.CueId, Is.EqualTo(TileFeatureVisualCueId.ExitOpenState));
            Assert.That(binding.InactiveStateAnimatorBinding.Kind, Is.EqualTo(TileFeatureAnimatorBindingKind.State));
            Assert.That(binding.InactiveStateAnimatorBinding.ParameterOrStateName, Is.EqualTo("ExitClosedIdle"));
            Assert.That(binding.InactiveStateAnimatorBinding.Hash, Is.Not.Zero);
            Assert.That(stateNames, Does.Contain("ExitOpenedIdle"));
            Assert.That(stateNames, Does.Contain("ExitClosedIdle"));
        }

        private static void AssertAnimatorParameter(
            AnimatorController controller,
            string parameterName,
            AnimatorControllerParameterType parameterType)
        {
            Assert.That(
                controller.parameters.Any(parameter =>
                    parameter.name == parameterName &&
                    parameter.type == parameterType),
                Is.True,
                $"Missing Animator parameter '{parameterName}' ({parameterType}).");
        }
    }
}
