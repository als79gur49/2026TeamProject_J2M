using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TopologyVisualContractsExtractionArchitectureTests
    {
        private const string PresenterRelativePath = "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs";
        private const string HostConfigurationRelativePath = "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHostConfiguration.cs";
        private const string RuntimeRelativeDirectory = "Assets/_Features/Gameplay/Gameplay_Host/Runtime";
        private const string ContractsRelativeDirectory = "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Contracts";

        [Test]
        [Category("Extended")]
        public void ContractFiles_Exist_AsStandaloneDeclarations()
        {
            var visualStateSource = ReadRepoFile($"{ContractsRelativeDirectory}/TopologyTransitionVisualState.cs");
            var presentationPhaseSource = ReadRepoFile($"{ContractsRelativeDirectory}/GameplayPresentationPhase.cs");
            var topologyRotationSource = ReadRepoFile($"{ContractsRelativeDirectory}/TopologyRotationVisualMapping.cs");

            Assert.That(visualStateSource, Does.Contain("public readonly struct TopologyTransitionVisualState"));
            Assert.That(presentationPhaseSource, Does.Contain("public enum GameplayPresentationPhase"));
            Assert.That(topologyRotationSource, Does.Contain("public enum TopologyRotationVisualMapping"));
        }

        [Test]
        [Category("Extended")]
        public void TopologyTransitionVisualState_PublicConstructorSignature_RemainsUnchanged()
        {
            var constructor = typeof(TopologyTransitionVisualState)
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Single();
            var parameterTypes = constructor
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray();

            Assert.That(parameterTypes, Is.EqualTo(new[]
            {
                typeof(bool),
                typeof(float),
                typeof(CubeTopologyState),
                typeof(CubeTopologyState),
                typeof(CubeRotationKind),
                typeof(float),
                typeof(Quaternion),
                typeof(float),
            }));
        }

        [Test]
        [Category("Extended")]
        public void TopologyTransitionVisualState_InactiveFactorySignature_AndBehavior_RemainUnchanged()
        {
            var factoryMethod = typeof(TopologyTransitionVisualState).GetMethod(
                nameof(TopologyTransitionVisualState.Inactive),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            var parameters = factoryMethod?.GetParameters();
            var topology = new CubeTopologyState(FaceId.Back);
            var presentedVisualRotation = Quaternion.Euler(12f, 24f, 36f);
            var visualState = TopologyTransitionVisualState.Inactive(topology, presentedVisualRotation);

            Assert.That(factoryMethod, Is.Not.Null);
            Assert.That(parameters, Is.Not.Null);
            Assert.That(parameters, Has.Length.EqualTo(2));
            Assert.That(parameters?[0].ParameterType, Is.EqualTo(typeof(CubeTopologyState)));
            Assert.That(parameters?[1].ParameterType, Is.EqualTo(typeof(Quaternion)));

            Assert.That(visualState.IsActive, Is.False);
            Assert.That(visualState.Progress01, Is.EqualTo(0f));
            Assert.That(visualState.SourceTopology, Is.EqualTo(topology));
            Assert.That(visualState.DestinationTopology, Is.EqualTo(topology));
            Assert.That(visualState.RotationKind, Is.EqualTo(CubeRotationKind.None));
            Assert.That(visualState.DurationSeconds, Is.EqualTo(0f));
            Assert.That(visualState.PresentedVisualRotation, Is.EqualTo(presentedVisualRotation));
            Assert.That(visualState.AngularVelocityNormalized, Is.EqualTo(0f));
        }

        [Test]
        [Category("Extended")]
        public void GameplayPresentationPhase_EnumValues_RemainUnchanged()
        {
            Assert.That((int)GameplayPresentationPhase.Idle, Is.EqualTo(0));
            Assert.That((int)GameplayPresentationPhase.EntityMotion, Is.EqualTo(1));
            Assert.That((int)GameplayPresentationPhase.TopologyTransition, Is.EqualTo(2));
        }

        [Test]
        [Category("Extended")]
        public void TopologyRotationVisualMapping_EnumValues_RemainUnchanged()
        {
            Assert.That((int)TopologyRotationVisualMapping.ForwardUsesNegativeX, Is.EqualTo(0));
            Assert.That((int)TopologyRotationVisualMapping.ForwardUsesPositiveX, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void Presenter_And_HostConfiguration_DoNotReintroduceNamespaceScopeContractDeclarations()
        {
            var presenterSource = ReadRepoFile(PresenterRelativePath);
            var hostConfigurationSource = ReadRepoFile(HostConfigurationRelativePath);

            Assert.That(presenterSource, Does.Not.Contain("public readonly struct TopologyTransitionVisualState"));
            Assert.That(presenterSource, Does.Not.Contain("public enum GameplayPresentationPhase"));
            Assert.That(hostConfigurationSource, Does.Not.Contain("public enum TopologyRotationVisualMapping"));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeDirectory_ContainsSingleDeclaration_ForEachExtractedContractType()
        {
            var runtimeSource = ReadCombinedRuntimeSource();

            Assert.That(CountMatches(runtimeSource, @"\bstruct\s+TopologyTransitionVisualState\b"), Is.EqualTo(1));
            Assert.That(CountMatches(runtimeSource, @"\benum\s+GameplayPresentationPhase\b"), Is.EqualTo(1));
            Assert.That(CountMatches(runtimeSource, @"\benum\s+TopologyRotationVisualMapping\b"), Is.EqualTo(1));
        }

        private static int CountMatches(string source, string pattern)
        {
            return Regex.Matches(source, pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant).Count;
        }

        private static string ReadCombinedRuntimeSource()
        {
            var runtimeDirectoryPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", RuntimeRelativeDirectory));
            return string.Join(
                "\n",
                Directory.GetFiles(runtimeDirectoryPath, "*.cs", SearchOption.AllDirectories)
                    .OrderBy(path => path)
                    .Select(File.ReadAllText));
        }

        private static string ReadRepoFile(string relativePath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            return File.ReadAllText(absolutePath);
        }
    }
}
