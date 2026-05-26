using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;

namespace Game.Feature.Gameplay.Tests.Replay
{
    public sealed class EnemyProfileContractReplayTests
    {
        private const int PlayerId = 10;
        private const int EnemyId = 40;
        private const string WindupProjectileProfilePath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/Profiles/Enemy_WindupProjectile/EnemyAi_WindupProjectile.asset";

        [Test]
        [Category("Extended")]
        public void Startis_ReplayDeterminism_InMovementContactAndTopologyResume()
        {
            var firstReplay = RunStartisReplaySequence();
            var secondReplay = RunStartisReplaySequence();

            AssertEquivalentReplayOutputs(firstReplay, secondReplay);
            Assert.That(firstReplay.Last().EventLogDump, Does.Contain("SourceKind=PassiveContact"));
            Assert.That(firstReplay.Last().FinalEntitiesDump, Does.Contain("E=10|Cell=Ceiling:0,0|Hp=2"));
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_ReplayDeterminism_ForWindupPendingImpactRecoverTopology()
        {
            var firstReplay = RunBlackEyeReplaySequence();
            var secondReplay = RunBlackEyeReplaySequence();

            AssertEquivalentReplayOutputs(firstReplay, secondReplay);
            Assert.That(firstReplay.Any(frame => frame.PendingImpactDump.Contains("Target=Floor:4,0")), Is.True);
            Assert.That(firstReplay.Any(frame => frame.ActionDump.Contains("Kind=ForwardCellProjectile")), Is.True);
        }

        private static IReadOnlyList<ReplayCaptureFrame> RunStartisReplaySequence()
        {
            var profile = EnemyAiProfileTestFactory.Create(new EnemyAiTestProfileSpec
            {
                PatrolStrategyKind = PatrolStrategyKind.Forward,
                PatrolSettings = new PatrolSettings(PatrolBlockedMovementResponse.Stop),
                DetectionStrategyKind = DetectionStrategyKind.None,
                AttackDecisionStrategyKind = AttackDecisionStrategyKind.None,
                IncludePassiveContact = true,
            });

            try
            {
                var cell = new SurfaceCell(FaceId.Ceiling, 0, 0);
                var worldState = CreateWorldState(
                    new[]
                    {
                        CreateUnit(PlayerId, 1, cell, UnitRole.Player),
                        CreateUnit(EnemyId, 2, cell, UnitRole.Enemy, EnemyAiMode.Patrol),
                    },
                    new CubeTopologyState(FaceId.Floor));
                var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState, profile);
                var frames = new List<ReplayCaptureFrame>();

                frames.Add(Capture(pipeline.RunTick(new TickInput(1)), worldState));
                worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Ceiling));
                frames.Add(Capture(pipeline.RunTick(new TickInput(2)), worldState));

                return frames;
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        private static IReadOnlyList<ReplayCaptureFrame> RunBlackEyeReplaySequence()
        {
            var profile = LoadWindupProjectileProfile();
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(PlayerId, 1, new SurfaceCell(FaceId.Floor, 4, 0), UnitRole.Player),
                    CreateUnit(EnemyId, 2, new SurfaceCell(FaceId.Floor, 0, 0), UnitRole.Enemy, EnemyAiMode.Attack),
                },
                new CubeTopologyState(FaceId.Floor));
            var pipeline = GameplayCompositionRoot.CreateDefaultBootstrapper(profile).CreateTickPipeline(worldState);
            var frames = new List<ReplayCaptureFrame>();

            frames.Add(Capture(pipeline.RunTick(new TickInput(1)), worldState));
            var originalExecuteTick = GetEnemyActionState(worldState).executeTick;

            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Front));
            frames.Add(Capture(pipeline.RunTick(new TickInput(2)), worldState));

            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Floor));
            frames.Add(Capture(pipeline.RunTick(new TickInput(originalExecuteTick + 1)), worldState));
            var restartedAction = GetEnemyActionState(worldState);
            frames.Add(Capture(pipeline.RunTick(new TickInput(restartedAction.executeTick)), worldState));
            frames.Add(Capture(pipeline.RunTick(new TickInput(restartedAction.executeTick + 1)), worldState));

            return frames;
        }

        private static WorldState CreateWorldState(EntityState[] initialEntities, CubeTopologyState topology)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                initialEntities,
                new BoardBounds(new Vector2Int(-1, -1), new Vector2Int(4, 4)),
                GameplayTerrainData.Empty,
                topology);
        }

        private static EntityState CreateUnit(
            int entityId,
            int teamId,
            SurfaceCell cell,
            UnitRole unitRole,
            EnemyAiMode aiMode = EnemyAiMode.None)
        {
            return new EntityState
            {
                entityId = entityId,
                position = cell,
                hp = 3,
                maxHp = 3,
                teamId = teamId,
                type = EntityType.Unit,
                unitRole = unitRole,
                unitMobilityKind = UnitMobilityKind.Ground,
                state = EntityPhaseState.Idle,
                facing = entityId == EnemyId ? Direction.Right : Direction.Left,
                boardPresence = EntityBoardPresence.Occupying,
                spawnTick = 0,
                aiMode = aiMode,
            };
        }

        private static EnemyAiProfile LoadWindupProjectileProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(WindupProjectileProfilePath);
            Assert.That(profile, Is.Not.Null, $"Missing EnemyAiProfile asset at '{WindupProjectileProfilePath}'.");
            return profile;
        }

        private static EnemyActionRuntimeState GetEnemyActionState(WorldState worldState)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEnemyActionState(EnemyId, out var action), Is.True);
            return action;
        }

        private static ReplayCaptureFrame Capture(TickResult result, WorldState worldState)
        {
            return new ReplayCaptureFrame(
                result.TickIndex,
                result.DeterminismHash,
                BuildFinalEntitiesDump(result.FinalEntities),
                BuildEventLogDump(result.EventLog),
                BuildDamageDump(result.AttackPhaseResult.DamageResolutions),
                BuildActionDump(worldState.CreateSnapshot()),
                BuildPendingImpactDump(worldState.CreateSnapshot()),
                result.Trace.Text);
        }

        private static void AssertEquivalentReplayOutputs(
            IReadOnlyList<ReplayCaptureFrame> firstReplay,
            IReadOnlyList<ReplayCaptureFrame> secondReplay)
        {
            Assert.That(firstReplay.Count, Is.EqualTo(secondReplay.Count));
            for (var i = 0; i < firstReplay.Count; i++)
            {
                Assert.That(secondReplay[i].TickIndex, Is.EqualTo(firstReplay[i].TickIndex));
                Assert.That(secondReplay[i].DeterminismHash, Is.EqualTo(firstReplay[i].DeterminismHash), $"Hash mismatch at frame {i}.");
                Assert.That(secondReplay[i].FinalEntitiesDump, Is.EqualTo(firstReplay[i].FinalEntitiesDump), $"Final entity mismatch at frame {i}.");
                Assert.That(secondReplay[i].EventLogDump, Is.EqualTo(firstReplay[i].EventLogDump), $"Event log mismatch at frame {i}.");
                Assert.That(secondReplay[i].DamageDump, Is.EqualTo(firstReplay[i].DamageDump), $"Damage mismatch at frame {i}.");
                Assert.That(secondReplay[i].ActionDump, Is.EqualTo(firstReplay[i].ActionDump), $"Action state mismatch at frame {i}.");
                Assert.That(secondReplay[i].PendingImpactDump, Is.EqualTo(firstReplay[i].PendingImpactDump), $"Pending impact mismatch at frame {i}.");
                Assert.That(secondReplay[i].Trace, Is.EqualTo(firstReplay[i].Trace), $"Trace mismatch at frame {i}.");
            }
        }

        private static string BuildFinalEntitiesDump(IReadOnlyList<EntityState> entities)
        {
            return string.Join(
                "\n",
                entities.Select(entity =>
                    $"E={entity.entityId}|Cell={FormatCell(entity.position)}|Hp={entity.hp}|Mode={entity.aiMode}|Timer={entity.aiStateTimer}|Presence={entity.boardPresence}"));
        }

        private static string BuildEventLogDump(IReadOnlyList<string> eventLog)
        {
            return eventLog.Count == 0 ? "<empty>" : string.Join("\n", eventLog);
        }

        private static string BuildDamageDump(IReadOnlyList<DamageResolutionRecord> damageResolutions)
        {
            return damageResolutions.Count == 0
                ? "<empty>"
                : string.Join(
                    "\n",
                    damageResolutions.Select(record =>
                        $"Source={record.SourceId}|SourceKind={record.SourceKind}|Target={record.TargetId}|Amount={record.Amount}|Accepted={record.Accepted}"));
        }

        private static string BuildActionDump(WorldSnapshot snapshot)
        {
            if (!snapshot.TryGetEnemyActionState(EnemyId, out var action))
            {
                return "<empty>";
            }

            return $"Kind={action.kind}|Seq={action.sequence}|Start={action.startTick}|Execute={action.executeTick}|Attempted={action.executionAttempted}|Target={FormatCell(action.lockedTargetCell)}";
        }

        private static string BuildPendingImpactDump(WorldSnapshot snapshot)
        {
            var impacts = new List<PendingCellImpactSnapshotEntry>();
            snapshot.EnumeratePendingCellImpactsOrdered(impacts);
            if (impacts.Count == 0)
            {
                return "<empty>";
            }

            var builder = new StringBuilder();
            for (var i = 0; i < impacts.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append('\n');
                }

                var impact = impacts[i].Impact;
                builder
                    .Append("Id=").Append(impact.ImpactId)
                    .Append("|Target=").Append(FormatCell(impact.TargetCell))
                    .Append("|Release=").Append(impact.ReleaseTick)
                    .Append("|Impact=").Append(impact.ImpactTick)
                    .Append("|Damage=").Append(impact.Damage);
            }

            return builder.ToString();
        }

        private static string FormatCell(SurfaceCell cell)
        {
            return $"{cell.face}:{cell.x},{cell.y}";
        }

        private readonly struct ReplayCaptureFrame
        {
            public ReplayCaptureFrame(
                int tickIndex,
                string determinismHash,
                string finalEntitiesDump,
                string eventLogDump,
                string damageDump,
                string actionDump,
                string pendingImpactDump,
                string trace)
            {
                TickIndex = tickIndex;
                DeterminismHash = determinismHash;
                FinalEntitiesDump = finalEntitiesDump;
                EventLogDump = eventLogDump;
                DamageDump = damageDump;
                ActionDump = actionDump;
                PendingImpactDump = pendingImpactDump;
                Trace = trace;
            }

            public int TickIndex { get; }

            public string DeterminismHash { get; }

            public string FinalEntitiesDump { get; }

            public string EventLogDump { get; }

            public string DamageDump { get; }

            public string ActionDump { get; }

            public string PendingImpactDump { get; }

            public string Trace { get; }
        }
    }
}
