using System.IO;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxBoxSlideTrailAdapterTests
    {
        private const string HostDefaultCueMapPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset";
        private const string SparkFollowPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/BoxSlideSparkFollowVfx.prefab";
        private const string FollowBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/BoxSlideFollowLoop_Binding.asset";
        private const string LegacyDustPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/BoxSlideDustTrailVfx.prefab";
        private const string LegacyFollowPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/BoxSlideFollowLoopVfx.prefab";
        private const string LegacyDustBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/BoxSlideDustTrail_Binding.asset";
        private const string ProductionRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs";

        [Test]
        [Category("Extended")]
        public void BoxSlideSparkFollowPrefab_PassesValidation()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SparkFollowPrefabPath);

            Assert.That(prefab, Is.Not.Null, SparkFollowPrefabPath);
            var validation = VfxPrefabValidationDiagnostics.ValidatePrefab(prefab);

            Assert.That(validation.HasErrors, Is.False, string.Join("\n", validation.Messages));
            Assert.That(validation.HasWarnings, Is.False, string.Join("\n", validation.Messages));
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<AudioSource>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void BoxSlideSparkFollowBinding_ValidatesAsAttachedFollow()
        {
            var binding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(FollowBindingPath);

            Assert.That(binding, Is.Not.Null, FollowBindingPath);
            Assert.That(binding.CueId, Is.EqualTo(GameplayVfxCueId.From(BoxVfxCue.BoxSlideFollowLoop)));
            Assert.That(binding.Requirement, Is.EqualTo(VfxBindingRequirement.DiagnosticIfMissing));
            Assert.That(binding.MissingAnchorPolicy, Is.EqualTo(VfxMissingAnchorPolicy.ReportDiagnostic));
            Assert.That(binding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.Follow));
            Assert.That(binding.StopPolicy, Is.EqualTo(VfxStopPolicy.DetachThenStopEmittingThenRelease));
            Assert.That(binding.DefaultLifetimeSeconds, Is.Zero);
            Assert.That(binding.TailSeconds, Is.EqualTo(0.35f).Within(0.001f));
            Assert.That(binding.ValidateAuthoring().HasErrors, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void HostDefaultMap_ResolvesBoxSlideSparkFollow()
        {
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(HostDefaultCueMapPath);

            Assert.That(cueMap, Is.Not.Null, HostDefaultCueMapPath);
            Assert.That(
                cueMap.BuildRuntimeMap().TryResolve(GameplayVfxCueId.From(BoxVfxCue.BoxSlideFollowLoop), out var policy),
                Is.True);
            Assert.That(policy.PlaybackMode, Is.EqualTo(VfxPlaybackMode.Follow));
            Assert.That(policy.StopPolicy, Is.EqualTo(VfxStopPolicy.DetachThenStopEmittingThenRelease));
        }

        [Test]
        [Category("Extended")]
        public void LegacyBoxSlideDustAndFollowAssets_Removed()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(LegacyDustPrefabPath), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(LegacyFollowPrefabPath), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(LegacyDustBindingPath), Is.Null);
        }

        [Test]
        [Category("Extended")]
        public void Runtime_UsesAttachedFollowerInsteadOfParameterizedBoxSlideTrail()
        {
            var runtimeSource = ReadRepoFile(ProductionRuntimePath);

            Assert.That(runtimeSource, Does.Not.Contain("PlayBoxSlideTrailCommands"));
            Assert.That(runtimeSource, Does.Not.Contain("TryPlayBoxSlideTrailCommand"));
            Assert.That(runtimeSource, Does.Not.Contain("BoxSlideTrailVfxCommandBuilder"));
            Assert.That(runtimeSource, Does.Not.Contain("SlideDustTrail"));
            Assert.That(runtimeSource, Does.Contain("BoxSlideFollowLoop"));
        }

        private static string ReadRepoFile(string path)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), path));
        }
    }
}
