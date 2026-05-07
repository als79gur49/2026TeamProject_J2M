using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class ImpactDispositionArchitectureTests
    {
        private static readonly string[] ImpactDispositionTokens =
        {
            "ImpactDispositionPolicyKind",
            "ImpactDispositionKind",
            "ImpactDispositionResolutionRecord",
        };

        private static readonly HashSet<string> AllowedRelativePaths = new()
        {
            Normalize("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs"),
            Normalize("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/MovementPhaseResult.cs"),
            Normalize("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPresentationData.cs"),
            Normalize("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickResultBuilder.cs"),
            Normalize("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayExitPresentationController.cs"),
            Normalize("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPoseResolver.cs"),
            Normalize("Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyDeathExitEffectPlanBuilder.cs"),
        };

        [Test]
        [Category("Extended")]
        public void ImpactDispositionSymbols_StayWithinNarrowPushFlipRuntimeHostAndTestBoundary()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var gameplayRoot = Path.Combine(repoRoot, "Assets", "_Features", "Gameplay");
            var files = Directory.GetFiles(gameplayRoot, "*.cs", SearchOption.AllDirectories);

            for (var i = 0; i < files.Length; i++)
            {
                var source = File.ReadAllText(files[i]);
                if (!ContainsAnyImpactDispositionToken(source))
                {
                    continue;
                }

                var relativePath = Normalize(Path.GetRelativePath(repoRoot, files[i]));
                var isAllowed =
                    AllowedRelativePaths.Contains(relativePath) ||
                    relativePath.StartsWith(Normalize("Assets/_Features/Gameplay/Gameplay_Tests/"), System.StringComparison.Ordinal);
                Assert.That(
                    isAllowed,
                    Is.True,
                    $"ImpactDisposition narrow contract use-site must stay in runtime/host/test boundary. File={relativePath}");
            }
        }

        private static bool ContainsAnyImpactDispositionToken(string source)
        {
            for (var i = 0; i < ImpactDispositionTokens.Length; i++)
            {
                if (source.Contains(ImpactDispositionTokens[i], System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Normalize(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
