using System.IO;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class MoonBlockGeneratorProductionProfileMigrationTests
    {
        private const string MoonGeneratorPrefabPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_MoonGenerator_Default.prefab";

        private const string MoonGeneratorProfilePath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Profiles/TileFeatureVisualProfile_MoonGenerator.asset";

        private const string TileFeatureVisualTargetViewPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/TileFeatureVisualTargetView.cs";

        [Test]
        [Category("Core")]
        public void ProductionPrefab_ResolvesMoonGeneratorProfileAndGeneratedTrigger()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MoonGeneratorPrefabPath);
            var profile = AssetDatabase.LoadAssetAtPath<TileFeatureVisualProfile>(MoonGeneratorProfilePath);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.FeatureKind, Is.EqualTo(TileFeatureKind.MoonBlockGenerator));

            var provider = prefab.GetComponent<TileFeatureVisualProfileProvider>();
            Assert.That(provider, Is.Not.Null);
            Assert.That(provider.TryGetProfile(TileFeatureKind.MoonBlockGenerator, out var providerProfile), Is.True);
            Assert.That(providerProfile, Is.SameAs(profile));

            Assert.That(profile.TryGetCueBinding(TileFeatureVisualCueId.MoonBlockGenerated, out var generated), Is.True);
            Assert.That(generated.AnimatorBinding.CueId, Is.EqualTo(TileFeatureVisualCueId.MoonBlockGenerated));
            Assert.That(generated.AnimatorBinding.Kind, Is.EqualTo(TileFeatureAnimatorBindingKind.Trigger));
            Assert.That(generated.AnimatorBinding.ParameterOrStateName, Is.EqualTo("MoonBlockGenerated"));

            var animator = prefab.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null);
            var controller = animator.runtimeAnimatorController as AnimatorController;
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.parameters.Any(parameter =>
                parameter.name == "MoonBlockGenerated" &&
                parameter.type == AnimatorControllerParameterType.Trigger), Is.True);
        }

        [Test]
        [Category("Core")]
        public void ProductionBlockedCue_UsesExplicitDiagnosticNoOpPolicy()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MoonGeneratorPrefabPath);
            var profile = AssetDatabase.LoadAssetAtPath<TileFeatureVisualProfile>(MoonGeneratorProfilePath);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.TryGetCueBinding(TileFeatureVisualCueId.MoonBlockGeneratorBlocked, out var blocked), Is.True);
            Assert.That(blocked.AnimatorBinding.CueId, Is.EqualTo(TileFeatureVisualCueId.None));
            Assert.That(blocked.AnimatorBinding.Hash, Is.Zero);
            Assert.That(blocked.SendGameplayVfx, Is.False);

            var animator = prefab.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null);
            var controller = animator.runtimeAnimatorController as AnimatorController;
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.parameters.Any(parameter => parameter.name == "MoonBlockGeneratorBlocked"), Is.False);
        }

        [Test]
        [Category("Core")]
        public void LegacyMoonGeneratorSurface_DoesNotReappearOnTargetViewOrProductionPrefab()
        {
            var targetViewSource = File.ReadAllText(TileFeatureVisualTargetViewPath);
            var prefabSource = File.ReadAllText(MoonGeneratorPrefabPath);

            Assert.That(targetViewSource, Does.Not.Contain("moonBlockGeneratedTriggerName"));
            Assert.That(targetViewSource, Does.Not.Contain("moonBlockGeneratorBlocked"));
            Assert.That(targetViewSource, Does.Not.Contain("PlayMoonBlock"));
            Assert.That(targetViewSource, Does.Not.Contain("UnityEvent"));
            Assert.That(targetViewSource, Does.Not.Contain("ParticleSystem"));

            Assert.That(prefabSource, Does.Not.Contain("moonBlockGeneratedTriggerName"));
            Assert.That(prefabSource, Does.Not.Contain("moonBlockGeneratorBlocked"));
            Assert.That(prefabSource, Does.Not.Contain("PlayMoonBlock"));
        }

    }
}
