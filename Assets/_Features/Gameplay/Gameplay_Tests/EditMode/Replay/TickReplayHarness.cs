using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;

namespace Game.Feature.Gameplay.Tests.Replay
{
    internal sealed class TickReplayHarness
    {
        public IReadOnlyList<TickReplayFrame> Run(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics,
            IReadOnlyList<TickInput> inputs,
            IReadOnlyList<DelayedAttackEffectRecord> initialDelayedAttackEffects = null,
            GameplayRuntimeFeatureFlags runtimeFeatureFlags = default,
            PlayerContinuousLocomotionSnapshot playerContinuousLocomotion = default)
        {
            var entityLogicList = new List<IEntityLogic>(entityLogics);
            var timingProfile = GameplayTimingProfile.CreateDefault();
            var playerControlTiming = PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                timingProfile.SimulationTicksPerSecond,
                timingProfile.RepeatedMoveIntervalSeconds);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                entityLogicList,
                timingProfile,
                playerControlTiming,
                runtimeFeatureFlags: runtimeFeatureFlags,
                playerContinuousLocomotion: playerContinuousLocomotion);
            return Run(pipeline, entityLogicList, inputs, initialDelayedAttackEffects);
        }

        public IReadOnlyList<TickReplayFrame> Run(
            GameplayBootstrapper bootstrapper,
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics,
            IReadOnlyList<TickInput> inputs,
            IReadOnlyList<DelayedAttackEffectRecord> initialDelayedAttackEffects = null,
            GameplayRuntimeFeatureFlags runtimeFeatureFlags = default,
            PlayerContinuousLocomotionSnapshot playerContinuousLocomotion = default)
        {
            if (bootstrapper == null)
            {
                throw new ArgumentNullException(nameof(bootstrapper));
            }

            var entityLogicList = new List<IEntityLogic>(entityLogics);
            var timingProfile = GameplayTimingProfile.CreateDefault();
            var playerControlTiming = PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                timingProfile.SimulationTicksPerSecond,
                timingProfile.RepeatedMoveIntervalSeconds);
            var pipeline = bootstrapper.CreateTickPipeline(
                worldState,
                entityLogicList,
                timingProfile,
                playerControlTiming,
                runtimeFeatureFlags: runtimeFeatureFlags,
                playerContinuousLocomotion: playerContinuousLocomotion);
            return Run(pipeline, entityLogicList, inputs, initialDelayedAttackEffects);
        }

        private IReadOnlyList<TickReplayFrame> Run(
            TickPipeline pipeline,
            IReadOnlyList<IEntityLogic> entityLogicList,
            IReadOnlyList<TickInput> inputs,
            IReadOnlyList<DelayedAttackEffectRecord> initialDelayedAttackEffects)
        {
            if (initialDelayedAttackEffects != null)
            {
                for (var i = 0; i < initialDelayedAttackEffects.Count; i++)
                {
                    pipeline.EnqueueDelayedAttackEffect(initialDelayedAttackEffects[i]);
                }
            }

            var frames = new List<TickReplayFrame>(inputs.Count);

            for (var i = 0; i < inputs.Count; i++)
            {
                SetReplayTickIndex(entityLogicList, inputs[i].TickIndex);
                var result = pipeline.RunTick(inputs[i]);
                frames.Add(
                    new TickReplayFrame(
                        result.TickIndex,
                        result.DeterminismHash,
                        result.Trace.Text,
                        BuildFinalEntitiesDump(result.FinalEntities),
                        BuildPlayerControlDump(result.Trace.Text),
                        BuildPlayerDamageDump(result.Trace.Text),
                        BuildEnemyActionDump(result.Trace.Text),
                        BuildEnemyChargeDump(result.Trace.Text),
                        BuildOccupancyDump(result.Trace.Text),
                        BuildMarkedForDeathDump(result.FinalEntities),
                        BuildEventLogDump(result.EventLog),
                        enemyPatrolDump: BuildEnemyPatrolDump(result.Trace.Text)));
            }

            return new ReadOnlyCollection<TickReplayFrame>(frames);
        }

        private static void SetReplayTickIndex(IReadOnlyList<IEntityLogic> entityLogics, int tickIndex)
        {
            for (var i = 0; i < entityLogics.Count; i++)
            {
                if (entityLogics[i] is IReplayTickAwareEntityLogic tickAwareEntityLogic)
                {
                    tickAwareEntityLogic.SetReplayTickIndex(tickIndex);
                }
            }
        }

        private static string BuildFinalEntitiesDump(IReadOnlyList<EntityState> finalEntities)
        {
            if (finalEntities.Count == 0)
            {
                return "<empty>";
            }

            var builder = new StringBuilder(finalEntities.Count * 64);

            for (var i = 0; i < finalEntities.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append('\n');
                }

                var entity = finalEntities[i];
                builder
                    .Append("E=").Append(entity.entityId)
                    .Append("|Pos=(").Append(entity.position.x).Append(',').Append(entity.position.y).Append(')')
                    .Append("|Hp=").Append(entity.hp)
                    .Append("|MaxHp=").Append(entity.maxHp)
                    .Append("|Team=").Append(entity.teamId)
                    .Append("|Type=").Append(entity.type)
                    .Append("|State=").Append(entity.state)
                    .Append("|Timer=").Append(entity.stateTimer)
                    .Append("|Facing=").Append(entity.facing)
                    .Append("|Marked=").Append(entity.markedForDeath ? 1 : 0)
                    .Append("|SpawnTick=").Append(entity.spawnTick)
                    .Append("|BoxCapabilities=").Append(entity.boxCapabilities)
                    .Append("|KineticInstigator=").Append(entity.kineticInstigatorEntityId)
                    .Append("|KineticTeam=").Append(entity.kineticInstigatorTeamId)
                    .Append("|AiMode=").Append(entity.aiMode)
                    .Append("|AiTimer=").Append(entity.aiStateTimer)
                    .Append("|LocomotionCooldown=").Append(entity.enemyLocomotionCooldownTicks)
                    .Append("|Face=").Append(entity.position.face)
                    .Append("|Presence=").Append(entity.boardPresence);
            }

            return builder.ToString();
        }

        private static string BuildOccupancyDump(string trace)
        {
            return ExtractSection(trace, "Final.Occupancy");
        }

        private static string BuildPlayerControlDump(string trace)
        {
            return ExtractSection(trace, "Final.PlayerControl");
        }

        private static string BuildEnemyActionDump(string trace)
        {
            return ExtractSection(trace, "Final.EnemyActions");
        }

        private static string BuildEnemyPatrolDump(string trace)
        {
            return ExtractSection(trace, "Final.EnemyPatrols");
        }

        private static string BuildEnemyChargeDump(string trace)
        {
            return ExtractSection(trace, "Final.EnemyCharges");
        }

        private static string BuildPlayerDamageDump(string trace)
        {
            return ExtractSection(trace, "Final.PlayerDamage");
        }

        private static string BuildMarkedForDeathDump(IReadOnlyList<EntityState> finalEntities)
        {
            var builder = new StringBuilder();

            for (var i = 0; i < finalEntities.Count; i++)
            {
                if (!finalEntities[i].markedForDeath)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(finalEntities[i].entityId);
            }

            return builder.Length == 0 ? "<empty>" : builder.ToString();
        }

        private static string BuildEventLogDump(IReadOnlyList<string> eventLog)
        {
            if (eventLog.Count == 0)
            {
                return "<empty>";
            }

            var builder = new StringBuilder(eventLog.Count * 32);

            for (var i = 0; i < eventLog.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(eventLog[i]);
            }

            return builder.ToString();
        }

        private static string ExtractSection(string trace, string title)
        {
            if (string.IsNullOrEmpty(trace))
            {
                return "<empty>";
            }

            var normalizedTrace = trace.Replace("\r\n", "\n");
            var lines = normalizedTrace.Split('\n');
            var builder = new StringBuilder();
            var insideSection = false;

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!insideSection)
                {
                    if (line == title)
                    {
                        insideSection = true;
                    }

                    continue;
                }

                if (line.Length > 0 && !char.IsWhiteSpace(line[0]))
                {
                    break;
                }

                var trimmedLine = line.Trim();
                if (trimmedLine.Length == 0)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(trimmedLine);
            }

            return builder.Length == 0 ? "<empty>" : builder.ToString();
        }
    }

    internal interface IReplayTickAwareEntityLogic
    {
        void SetReplayTickIndex(int tickIndex);
    }

    internal readonly struct TickReplayFrame
    {
        public TickReplayFrame(
            int tickIndex,
            string determinismHash,
            string trace,
            string finalEntitiesDump,
            string playerControlDump,
            string playerDamageDump,
            string enemyActionDump,
            string enemyChargeDump,
            string occupancyDump,
            string markedForDeathDump,
            string eventLogDump,
            string enemyPatrolDump = "<empty>")
        {
            TickIndex = tickIndex;
            DeterminismHash = determinismHash;
            Trace = trace;
            FinalEntitiesDump = finalEntitiesDump;
            PlayerControlDump = playerControlDump;
            PlayerDamageDump = playerDamageDump;
            EnemyActionDump = enemyActionDump;
            EnemyChargeDump = enemyChargeDump;
            OccupancyDump = occupancyDump;
            MarkedForDeathDump = markedForDeathDump;
            EventLogDump = eventLogDump;
            EnemyPatrolDump = enemyPatrolDump;
        }

        public int TickIndex { get; }

        public string DeterminismHash { get; }

        public string Trace { get; }

        public string FinalEntitiesDump { get; }

        public string PlayerControlDump { get; }

        public string PlayerDamageDump { get; }

        public string EnemyActionDump { get; }

        public string EnemyChargeDump { get; }

        public string EnemyPatrolDump { get; }

        public string OccupancyDump { get; }

        public string MarkedForDeathDump { get; }

        public string EventLogDump { get; }
    }
}
