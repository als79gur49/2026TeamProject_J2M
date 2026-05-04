using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Vfx;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxCoreValueTests
    {
        [Test]
        [Category("Extended")]
        public void CueId_UsesTypedFamilyAndCodeIdentity()
        {
            var playerCue = GameplayVfxCueId.From(PlayerVfxCue.Damage);
            var boxCue = GameplayVfxCueId.From(BoxVfxCue.DestroySmoke);

            Assert.That(playerCue.Family, Is.EqualTo(GameplayVfxFamily.Player));
            Assert.That(playerCue.Code, Is.EqualTo((int)PlayerVfxCue.Damage));
            Assert.That(boxCue.Family, Is.EqualTo(GameplayVfxFamily.Box));
            Assert.That(boxCue.Code, Is.EqualTo((int)BoxVfxCue.DestroySmoke));
            Assert.That(typeof(GameplayVfxCueId).GetConstructors().Any(constructor =>
                constructor.GetParameters().Any(parameter => parameter.ParameterType == typeof(string))), Is.False);
            Assert.That(typeof(GameplayVfxCueId).GetMethod(nameof(GameplayVfxCueId.From), new[] { typeof(PlayerVfxCue) }), Is.Not.Null);
            Assert.That(typeof(GameplayVfxCueId).GetMethod(nameof(GameplayVfxCueId.From), new[] { typeof(BoxVfxCue) }), Is.Not.Null);
            Assert.That(typeof(GameplayVfxCueId).GetMethod(nameof(GameplayVfxCueId.From), new[] { typeof(EnemyVfxCue) }), Is.Not.Null);
        }

        [Test]
        [Category("Extended")]
        public void GameplayVfxRequest_SourceEntityId_ParticipatesInIdentity()
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget);
            var anchor = VfxAnchor.ForCell(
                new SurfaceCell(FaceId.Floor, 1, 2),
                new CubeTopologyState(FaceId.Floor),
                VfxAnchorSlot.CellFloor);
            var missingSource = new GameplayVfxRequest(
                5,
                7,
                17,
                cueId,
                anchor,
                VfxTimingKind.ImmediateOnTickPresentation);
            var sourceA = new GameplayVfxRequest(
                5,
                7,
                17,
                10,
                cueId,
                anchor,
                VfxTimingKind.ImmediateOnTickPresentation);
            var sourceB = new GameplayVfxRequest(
                5,
                7,
                17,
                11,
                cueId,
                anchor,
                VfxTimingKind.ImmediateOnTickPresentation);

            Assert.That(missingSource.SourceEntityId, Is.Zero);
            Assert.That(sourceA.SourceEntityId, Is.EqualTo(10));
            Assert.That(sourceA, Is.Not.EqualTo(sourceB));
            Assert.That(sourceA.GetHashCode(), Is.Not.EqualTo(sourceB.GetHashCode()));
            Assert.That(sourceA.CompareTo(sourceB), Is.LessThan(0));
            Assert.That(sourceB.CompareTo(sourceA), Is.GreaterThan(0));
            Assert.That(sourceA.ToString(), Does.Contain("SourceEntityId=10"));
        }

        [Test]
        [Category("Extended")]
        public void RequestPlan_SortsRequestsDeterministically()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var builder = new GameplayVfxRequestPlanBuilder();
            builder.Add(CreateRequest(2, 3, GameplayVfxCueId.From(EnemyVfxCue.Spawn), VfxAnchor.ForEntity(5)));
            builder.Add(CreateRequest(1, 2, GameplayVfxCueId.From(BoxVfxCue.DestroySmoke), VfxAnchor.ForCell(new SurfaceCell(FaceId.Front, 2, 3), topology)));
            builder.Add(CreateRequest(1, 1, GameplayVfxCueId.From(PlayerVfxCue.Damage), VfxAnchor.ForEntity(9)));
            builder.Add(CreateRequest(1, 0, GameplayVfxCueId.From(PlayerVfxCue.Death), VfxAnchor.ForEntity(1)));

            var plan = builder.Build();

            Assert.That(plan.Requests.Select(request => request.CueId).ToArray(), Is.EqualTo(new[]
            {
                GameplayVfxCueId.From(PlayerVfxCue.Death),
                GameplayVfxCueId.From(PlayerVfxCue.Damage),
                GameplayVfxCueId.From(BoxVfxCue.DestroySmoke),
                GameplayVfxCueId.From(EnemyVfxCue.Spawn),
            }));
        }

        [Test]
        [Category("Extended")]
        public void PersistentKey_EqualityAndOrdering_AreDeterministic()
        {
            var cue = GameplayVfxCueId.From(TileFeatureVfxCue.HazardPulse);
            var cell = new SurfaceCell(FaceId.Front, 1, 2);
            var first = new VfxPersistentKey(cue, VfxAnchorKind.Cell, cell: cell, hasCell: true, effectIndex: 1, activationSequence: 10);
            var same = new VfxPersistentKey(cue, VfxAnchorKind.Cell, cell: cell, hasCell: true, effectIndex: 1, activationSequence: 10);
            var laterActivation = new VfxPersistentKey(cue, VfxAnchorKind.Cell, cell: cell, hasCell: true, effectIndex: 1, activationSequence: 11);

            Assert.That(first, Is.EqualTo(same));
            Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
            Assert.That(first, Is.Not.EqualTo(laterActivation));
            Assert.That(first.CompareTo(laterActivation), Is.LessThan(0));
        }

        [Test]
        [Category("Extended")]
        public void Request_DoesNotOwnExecutionPolicy()
        {
            var publicMemberNames = typeof(GameplayVfxRequest)
                .GetMembers()
                .Where(member => member.DeclaringType == typeof(GameplayVfxRequest))
                .Select(member => member.Name)
                .ToArray();

            Assert.That(publicMemberNames, Does.Not.Contain("MissingAnchorPolicy"));
            Assert.That(publicMemberNames, Does.Not.Contain("PlaybackMode"));
            Assert.That(publicMemberNames, Does.Not.Contain("StopPolicy"));
            Assert.That(publicMemberNames, Does.Not.Contain("TailSeconds"));
            Assert.That(publicMemberNames, Does.Not.Contain("MaxConcurrentInstances"));
            Assert.That(publicMemberNames, Does.Not.Contain("Requirement"));
        }

        [Test]
        [Category("Extended")]
        public void Anchor_PreservesSurfaceCellFace()
        {
            var floor = VfxAnchor.ForCell(
                new SurfaceCell(FaceId.Floor, 4, 7),
                new CubeTopologyState(FaceId.Floor));
            var front = VfxAnchor.ForCell(
                new SurfaceCell(FaceId.Front, 4, 7),
                new CubeTopologyState(FaceId.Floor));

            Assert.That(floor.HasCell, Is.True);
            Assert.That(front.HasCell, Is.True);
            Assert.That(floor, Is.Not.EqualTo(front));
            Assert.That(floor.Cell.PlanarPosition, Is.EqualTo(front.Cell.PlanarPosition));
            Assert.That(floor.Cell.face, Is.Not.EqualTo(front.Cell.face));
        }

        [Test]
        [Category("Extended")]
        public void FamilyPlannerSkeletons_AreNoOpAndExposeFamilyMetadata()
        {
            IGameplayVfxFamilyRequestPlanner[] planners =
            {
                new PlayerVfxRequestPlanner(),
                new BoxVfxRequestPlanner(),
                new EnemyVfxRequestPlanner(),
                new TileFeatureVfxRequestPlanner(),
                new TerrainVfxRequestPlanner(),
                new ProjectileVfxRequestPlanner(),
                new ObjectiveStageVfxRequestPlanner(),
            };
            var builder = new GameplayVfxRequestPlanBuilder();
            var context = new GameplayVfxPlanningContext(12);

            foreach (var planner in planners)
            {
                planner.Plan(context, builder);
            }

            Assert.That(planners.Select(planner => planner.Family).ToArray(), Is.EqualTo(new[]
            {
                GameplayVfxFamily.Player,
                GameplayVfxFamily.Box,
                GameplayVfxFamily.Enemy,
                GameplayVfxFamily.TileFeature,
                GameplayVfxFamily.Terrain,
                GameplayVfxFamily.Projectile,
                GameplayVfxFamily.ObjectiveStage,
            }));
            Assert.That(builder.Build(), Is.SameAs(GameplayVfxRequestPlan.Empty));
        }

        private static GameplayVfxRequest CreateRequest(
            int tick,
            int sequence,
            GameplayVfxCueId cueId,
            VfxAnchor anchor)
        {
            return new GameplayVfxRequest(
                tick,
                sequence,
                presentationSeed: sequence * 17,
                cueId,
                anchor,
                VfxTimingKind.ImmediateOnTickPresentation);
        }
    }
}
