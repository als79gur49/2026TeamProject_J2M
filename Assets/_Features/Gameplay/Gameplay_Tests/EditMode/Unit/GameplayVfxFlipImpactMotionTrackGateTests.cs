using System;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Host;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxFlipImpactMotionTrackGateTests
    {
        private const string AnchorPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/FlipImpact/FlipImpactContactVfxAnchor.cs";
        private const string BuilderPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/FlipImpact/FlipImpactContactVfxAnchorBuilder.cs";
        private const string VfxPlanningPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Runtime/GameplayVfxPlanning.cs";
        private const string VfxProductionRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs";
        [Test]
        [Category("Extended")]
        public void Builder_StaySignal_BuildsContactAnchor()
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            var signal = CreateSignal(FlipImpactPresentationDisposition.Stay);

            var result = FlipImpactContactVfxAnchorBuilder.TryBuild(signal, timingProfile, out var anchor);

            Assert.That(result, Is.True);
            AssertAnchorMatchesSignal(anchor, signal);
            Assert.That(anchor.ContactNormalizedTime, Is.EqualTo(ExpectedContactNormalizedTime(timingProfile)));
        }

        [Test]
        [Category("Extended")]
        public void Builder_DestroySelfSignal_BuildsContactAnchor()
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            var signal = CreateSignal(FlipImpactPresentationDisposition.DestroySelf);

            var result = FlipImpactContactVfxAnchorBuilder.TryBuild(signal, timingProfile, out var anchor);

            Assert.That(result, Is.True);
            AssertAnchorMatchesSignal(anchor, signal);
            Assert.That(anchor.ContactNormalizedTime, Is.EqualTo(ExpectedContactNormalizedTime(timingProfile)));
        }

        [Test]
        [Category("Extended")]
        public void Builder_InvalidBoxEntity_ReturnsFalse()
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            var signal = CreateSignal(FlipImpactPresentationDisposition.Stay, boxEntityId: 0);

            var result = FlipImpactContactVfxAnchorBuilder.TryBuild(signal, timingProfile, out var anchor);

            Assert.That(result, Is.False);
            Assert.That(anchor.BoxEntityId, Is.EqualTo(0));
        }

        [Test]
        [Category("Extended")]
        public void Builder_PreservesSurfaceCellFace()
        {
            var sourceCell = new SurfaceCell(FaceId.Front, 3, 4);
            var impactCell = new SurfaceCell(FaceId.Back, 5, 6);
            var signal = CreateSignal(
                FlipImpactPresentationDisposition.Stay,
                sourceCell: sourceCell,
                impactCell: impactCell);

            var result = FlipImpactContactVfxAnchorBuilder.TryBuild(
                signal,
                GameplayTimingProfile.CreateDefault(),
                out var anchor);

            Assert.That(result, Is.True);
            Assert.That(anchor.SourceCell, Is.EqualTo(sourceCell));
            Assert.That(anchor.ImpactCell, Is.EqualTo(impactCell));
            Assert.That(anchor.SourceCell.face, Is.EqualTo(FaceId.Front));
            Assert.That(anchor.ImpactCell.face, Is.EqualTo(FaceId.Back));
        }

        [Test]
        [Category("Extended")]
        public void Builder_UnsupportedDisposition_ReturnsFalse()
        {
            var signal = CreateSignal((FlipImpactPresentationDisposition)0);

            var result = FlipImpactContactVfxAnchorBuilder.TryBuild(
                signal,
                GameplayTimingProfile.CreateDefault(),
                out _);

            Assert.That(result, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Builder_DoesNotReadAuthority()
        {
            var source = ReadRepoFile(AnchorPath) + "\n" + ReadRepoFile(BuilderPath);

            AssertForbiddenAuthorityTokensAbsent(source);
        }

        [Test]
        [Category("Extended")]
        public void HostAnchorResolver_MotionTrackStillUnsupported()
        {
            var resolver = new GameplayVfxHostAnchorResolver(
                new RejectingCellProjector(),
                new RejectingEntityProjector());
            var request = new GameplayVfxRequest(
                1,
                1,
                17,
                GameplayVfxCueId.From(PlayerVfxCue.Damage),
                VfxAnchor.ForMotionTrack(30),
                VfxTimingKind.AtMotionContact);

            var result = resolver.TryResolve(request, out var resolved);

            Assert.That(result, Is.False);
            Assert.That(resolved.IsResolved, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void BurstSlice_DoesNotAddMotionTrackSupportOrOldPresenterBypass()
        {
            var planningSource = ReadRepoFile(VfxPlanningPath);
            var productionRuntimeSource = ReadRepoFile(VfxProductionRuntimePath);
            var oldPresenterSource = string.Join(
                "\n",
                new[]
                {
                    "Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxFlipInteractionDriver.cs",
                    "Assets/_Features/Gameplay/Gameplay_Host/Runtime/PresentationMotionTrack.cs",
                    "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayExitPresentationController.cs",
                }.Select(ReadRepoFile));

            Assert.That(planningSource, Does.Not.Contain("FlipImpactContactVfxAnchorBuilder"));
            Assert.That(productionRuntimeSource, Does.Not.Contain("SuppressLegacyFlipImpact"));
            Assert.That(oldPresenterSource, Does.Not.Contain("FlipImpactContactVfxAnchor"));
            Assert.That(oldPresenterSource, Does.Not.Contain("SuppressLegacyFlipImpact"));
        }

        private static FlipImpactPresentationSignal CreateSignal(
            FlipImpactPresentationDisposition disposition,
            int boxEntityId = 30,
            SurfaceCell? sourceCell = null,
            SurfaceCell? impactCell = null)
        {
            return new FlipImpactPresentationSignal(
                sourceActionPlanId: 7,
                boxEntityId: boxEntityId,
                impactTargetEntityId: 40,
                actorEntityId: 10,
                sourceCell: sourceCell ?? new SurfaceCell(FaceId.Floor, 0, 0),
                impactCell: impactCell ?? new SurfaceCell(FaceId.Floor, 2, 0),
                topology: new CubeTopologyState(FaceId.Ceiling),
                sourceFacing: Direction.Left,
                impactFacing: Direction.Right,
                disposition: disposition);
        }

        private static void AssertAnchorMatchesSignal(
            FlipImpactContactVfxAnchor anchor,
            FlipImpactPresentationSignal signal)
        {
            Assert.That(anchor.SourceActionPlanId, Is.EqualTo(signal.SourceActionPlanId));
            Assert.That(anchor.BoxEntityId, Is.EqualTo(signal.BoxEntityId));
            Assert.That(anchor.ActorEntityId, Is.EqualTo(signal.ActorEntityId));
            Assert.That(anchor.ImpactTargetEntityId, Is.EqualTo(signal.ImpactTargetEntityId));
            Assert.That(anchor.SourceCell, Is.EqualTo(signal.SourceCell));
            Assert.That(anchor.ImpactCell, Is.EqualTo(signal.ImpactCell));
            Assert.That(anchor.Topology, Is.EqualTo(signal.Topology));
            Assert.That(anchor.SourceFacing, Is.EqualTo(signal.SourceFacing));
            Assert.That(anchor.ImpactFacing, Is.EqualTo(signal.ImpactFacing));
            Assert.That(anchor.Disposition, Is.EqualTo(signal.Disposition));
        }

        private static float ExpectedContactNormalizedTime(GameplayTimingProfile timingProfile)
        {
            return GameplayMotionTimingResolver
                .CreateFlipImpactTimingSettings(timingProfile)
                .ContactNormalizedTime;
        }

        private static void AssertForbiddenAuthorityTokensAbsent(string source)
        {
            var forbiddenTokens = new[]
            {
                "WorldState",
                "WorldSnapshot",
                "TickPipeline",
                "ProjectedWorld",
                "FinalizationBatch",
                "DeterminismHashBuilder",
                "CreateSnapshot",
            };

            foreach (var token in forbiddenTokens)
            {
                Assert.That(source, Does.Not.Contain(token), token);
            }
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(GetAbsolutePath(relativePath)).Replace("\r\n", "\n");
        }

        private static string GetAbsolutePath(string relativePath)
        {
            return Path.GetFullPath(relativePath);
        }

        private sealed class RejectingCellProjector : IGameplayVfxCellAnchorProjector
        {
            public bool TryResolveCell(
                SurfaceCell cell,
                CubeTopologyState topology,
                VfxAnchorSlot slot,
                GameplayVfxVisibilityMode visibilityMode,
                out VfxResolvedAnchor resolvedAnchor)
            {
                resolvedAnchor = VfxResolvedAnchor.Unresolved(VfxMissingAnchorPolicy.SkipOptional);
                return false;
            }
        }

        private sealed class RejectingEntityProjector : IGameplayVfxEntityAnchorProjector
        {
            public bool TryResolveEntity(
                int entityId,
                VfxAnchorSlot slot,
                out VfxResolvedAnchor resolvedAnchor)
            {
                resolvedAnchor = VfxResolvedAnchor.Unresolved(VfxMissingAnchorPolicy.SkipOptional);
                return false;
            }
        }
    }
}
