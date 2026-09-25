using System.Collections.Generic;
using System.Text;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Host.UIAccess;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayHostCommandAdmissionPolicyTests
    {
        [Test]
        [Category("Extended")]
        public void AdmissionPolicy_TerminalSessionBlocksQueriesUntilRevealCompletes()
        {
            var hostObject = new GameObject(
                "AdmissionPolicy_TerminalSessionBlocksQueriesUntilRevealCompletes");

            try
            {
                TerminalSessionRegistry.ResetForTests();
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                }));

                using var policy = CreatePolicy(host);
                Assert.That(policy.CanAcceptActionableCommands(), Is.True);
                var authority = TerminalSessionRegistry.Authority;
                var sourceGeneration = authority.CurrentSceneGeneration > 0
                    ? authority.CurrentSceneGeneration
                    : authority.RegisterSceneBootstrap(501, "command-admission-test");
                var claim = authority.TryClaim(new TerminalClaimRequest(
                    TerminalTransitionKind.Defeat,
                    sourceGeneration,
                    TerminalDestinationKind.ReloadedGameplay));
                Assert.That(claim.Accepted, Is.True);

                Assert.That(policy.CanAcceptActionableCommands(out var reason), Is.False);
                Assert.That(reason, Is.EqualTo(GameplayCommandRejectionReason.TerminalSession));
                Assert.That(host.UiAccess.QueryFacade.Session.Read().CanAcceptGameplayCommands, Is.False);

                Assert.That(TerminalSessionRegistry.TryAdvance(
                    claim.Token,
                    TerminalSessionPhase.Revealing), Is.True);
                Assert.That(policy.CanAcceptActionableCommands(), Is.False);
                Assert.That(TerminalSessionRegistry.TryComplete(claim.Token), Is.True);
                Assert.That(policy.CanAcceptActionableCommands(), Is.True);
            }
            finally
            {
                TerminalSessionRegistry.ResetForTests();
                Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayInputHost_CreatedInDestinationSceneRejectsTicksAndAllBufferedInputUntilRevealCompletes()
        {
            var hostObject = new GameObject(
                "GameplayInputHost_CreatedInDestinationSceneRejectsTicksAndAllBufferedInputUntilRevealCompletes");

            try
            {
                TerminalSessionRegistry.ResetForTests();
                var authority = TerminalSessionRegistry.Authority;
                var sourceGeneration = authority.RegisterSceneBootstrap(8101, "SceneA");
                var claim = authority.TryClaim(new TerminalClaimRequest(
                    TerminalTransitionKind.Defeat,
                    sourceGeneration,
                    TerminalDestinationKind.ReloadedGameplay));
                Assert.That(claim.Accepted, Is.True);

                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                }));
                var nextTickBeforeReveal = host.TickRunner.NextTickIndex;

                host.InputHost.SetRawMoveInput(Vector2.right);
                host.InputHost.BufferPush();
                host.InputHost.BufferFlip();

                Assert.That(host.InputHost.AdvanceTime(10f), Is.Zero);
                Assert.That(host.InputHost.RunSingleTick(), Is.Null);
                Assert.That(host.TickRunner.NextTickIndex, Is.EqualTo(nextTickBeforeReveal));
                Assert.That(typeof(GameplayInputHost).GetField("_sampledMoveInput", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .GetValue(host.InputHost), Is.EqualTo(Vector2.zero));
                Assert.That(typeof(GameplayInputHost).GetField("_hasBufferedPush", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .GetValue(host.InputHost), Is.False);
                Assert.That(typeof(GameplayInputHost).GetField("_hasBufferedFlip", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .GetValue(host.InputHost), Is.False);
                Assert.That(host.UiAccess.QueryFacade.Session.Read().CanAcceptGameplayCommands, Is.False);

                Assert.That(
                    authority.TryAdvancePhase(claim.Token, TerminalSessionPhase.Revealing),
                    Is.True);
                Assert.That(authority.TryComplete(claim.Token), Is.True);

                var firstPostRevealTick = host.InputHost.RunSingleTick();
                Assert.That(firstPostRevealTick, Is.Not.Null);
                Assert.That(host.TickRunner.NextTickIndex, Is.EqualTo(nextTickBeforeReveal + 1));
                Assert.That(
                    GameplayCompositionRoot.CreateSnapshot(host.WorldState)
                        .TryGetEntity(10, out var player),
                    Is.True);
                Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            }
            finally
            {
                TerminalSessionRegistry.ResetForTests();
                Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayInputHost_ManualRetryEntrySessionBlocksEveryLifecyclePhaseAndReleasesOnce()
        {
            var hostObject = new GameObject(
                "GameplayInputHost_ManualRetryEntrySessionBlocksEveryLifecyclePhaseAndReleasesOnce");

            try
            {
                SceneEntryPresentationRegistry.ResetForTests();
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(
                        new SurfaceCell(FaceId.Floor, 0, 0),
                        facing: Direction.Right),
                }));
                host.InputHost.SetAutoAdvanceTicks(false);

                Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
                Assert.That(
                    SceneEntryPresentationRegistry.TryClaim(
                        SceneTransitionIntent.ManualRetry,
                        StageId.CreateOrThrow("stage-0-1"),
                        sourceSceneGeneration: 11,
                        out var token),
                    Is.True);
                Assert.That(host.InputHost.RunSingleTick(), Is.Null);
                host.InputHost.BufferPush();
                host.InputHost.BufferFlip();
                Assert.That(typeof(GameplayInputHost).GetField("_sampledMoveInput", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .GetValue(host.InputHost), Is.EqualTo(Vector2.zero));
                Assert.That(typeof(GameplayInputHost).GetField("_hasBufferedPush", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .GetValue(host.InputHost), Is.False);
                Assert.That(typeof(GameplayInputHost).GetField("_hasBufferedFlip", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .GetValue(host.InputHost), Is.False);

                Assert.That(
                    SceneEntryPresentationRegistry.TryBindTransition(token, 71),
                    Is.True);
                Assert.That(
                    SceneEntryPresentationRegistry.TryAdvance(
                        token,
                        SceneEntryPresentationPhase.PersistentCoverReady),
                    Is.True);
                Assert.That(
                    SceneEntryPresentationRegistry.TryAdvance(
                        token,
                        SceneEntryPresentationPhase.Loading),
                    Is.True);
                Assert.That(host.InputHost.AdvanceTime(10f), Is.Zero);
                Assert.That(
                    SceneEntryPresentationRegistry.TryRegisterDestinationScene(token, 12),
                    Is.True);
                Assert.That(
                    SceneEntryPresentationRegistry.TryAdvance(
                        token,
                        SceneEntryPresentationPhase.EntryIrisClosed),
                    Is.True);
                Assert.That(
                    SceneEntryPresentationRegistry.TryAdvance(
                        token,
                        SceneEntryPresentationPhase.Opening),
                    Is.True);
                Assert.That(host.InputHost.RunSingleTick(), Is.Null);
                Assert.That(SceneEntryPresentationRegistry.TryComplete(token), Is.True);

                Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
                Assert.That(SceneEntryPresentationRegistry.TryComplete(token), Is.False);
            }
            finally
            {
                SceneEntryPresentationRegistry.ResetForTests();
                Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void AdmissionPolicy_SeedsSnapshotCache_AndReusesSameReference_WithinCompletedTickWindow()
        {
            var hostObject = new GameObject("AdmissionPolicy_SeedsSnapshotCache_AndReusesSameReference_WithinCompletedTickWindow");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                }));

                using var policy = CreatePolicy(host);

                Assert.That(policy.TryCreateSnapshot(out var firstSnapshot), Is.True);
                Assert.That(policy.TryCreateSnapshot(out var secondSnapshot), Is.True);
                Assert.That(ReferenceEquals(firstSnapshot, secondSnapshot), Is.True);

                var freshSnapshot = GameplayCompositionRoot.CreateSnapshot(host.WorldState);
                Assert.That(BuildSnapshotSemanticDump(firstSnapshot), Is.EqualTo(BuildSnapshotSemanticDump(freshSnapshot)));
            }
            finally
            {
                Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void AdmissionPolicy_TickCompletedRefresh_ReplacesCachedReference_AndMatchesFreshSnapshot()
        {
            var hostObject = new GameObject("AdmissionPolicy_TickCompletedRefresh_ReplacesCachedReference_AndMatchesFreshSnapshot");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(
                    new[]
                    {
                        CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                    },
                    boardBounds: new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0))));

                using var policy = CreatePolicy(host);

                Assert.That(policy.TryCreateSnapshot(out var beforeTickSnapshot), Is.True);
                host.InputHost.SetRawMoveInput(Vector2.right);
                Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
                Assert.That(policy.TryCreateSnapshot(out var afterTickSnapshot), Is.True);

                Assert.That(ReferenceEquals(beforeTickSnapshot, afterTickSnapshot), Is.False);
                Assert.That(BuildSnapshotSemanticDump(afterTickSnapshot), Is.EqualTo(BuildSnapshotSemanticDump(GameplayCompositionRoot.CreateSnapshot(host.WorldState))));
            }
            finally
            {
                Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void AdmissionPolicy_CommittedControllableActorAccessor_ReusesSameWindowFact_AndMatchesFreshSnapshotAfterTickCompleted()
        {
            var hostObject = new GameObject("AdmissionPolicy_CommittedControllableActorAccessor_ReusesSameWindowFact_AndMatchesFreshSnapshotAfterTickCompleted");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(
                    new[]
                    {
                        CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                    },
                    boardBounds: new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0))));

                using var policy = CreatePolicy(host);

                Assert.That(policy.TryGetCommittedControllableActor(out var firstActor), Is.True);
                Assert.That(policy.TryGetCommittedControllableActor(out var secondActor), Is.True);

                var freshBeforeTick = GameplayCompositionRoot.CreateSnapshot(host.WorldState);
                Assert.That(freshBeforeTick.TryGetEntity(10, out var freshBeforeActor), Is.True);

                Assert.That(firstActor.entityId, Is.EqualTo(freshBeforeActor.entityId));
                Assert.That(firstActor.position, Is.EqualTo(freshBeforeActor.position));
                Assert.That(secondActor.entityId, Is.EqualTo(firstActor.entityId));
                Assert.That(secondActor.position, Is.EqualTo(firstActor.position));

                host.InputHost.SetRawMoveInput(Vector2.right);
                Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
                Assert.That(policy.TryGetCommittedControllableActor(out var refreshedActor), Is.True);

                var freshAfterTick = GameplayCompositionRoot.CreateSnapshot(host.WorldState);
                Assert.That(freshAfterTick.TryGetEntity(10, out var freshAfterActor), Is.True);

                Assert.That(refreshedActor.entityId, Is.EqualTo(freshAfterActor.entityId));
                Assert.That(refreshedActor.position, Is.EqualTo(freshAfterActor.position));
                Assert.That(refreshedActor.facing, Is.EqualTo(freshAfterActor.facing));
            }
            finally
            {
                Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void AdmissionPolicy_Dispose_StopsTickCompletedRefresh_AndLeavesCachedReferenceUnchanged_WithoutFreshnessAssertions()
        {
            var hostObject = new GameObject("AdmissionPolicy_Dispose_StopsTickCompletedRefresh_AndLeavesCachedReferenceUnchanged_WithoutFreshnessAssertions");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(
                    new[]
                    {
                        CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                    },
                    boardBounds: new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0))));

                var policy = CreatePolicy(host);
                host.InputHost.SetRawMoveInput(Vector2.right);
                Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
                Assert.That(policy.TryCreateSnapshot(out var refreshedSnapshot), Is.True);

                policy.Dispose();

                host.WorldState.CreateWriteContext().MoveEntity(10, new SurfaceCell(FaceId.Floor, 1, 0));
                Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
                Assert.That(policy.TryCreateSnapshot(out var snapshotAfterDispose), Is.True);

                Assert.That(ReferenceEquals(refreshedSnapshot, snapshotAfterDispose), Is.True);
                Assert.That(BuildSnapshotSemanticDump(snapshotAfterDispose), Is.Not.EqualTo(BuildSnapshotSemanticDump(GameplayCompositionRoot.CreateSnapshot(host.WorldState))));
            }
            finally
            {
                Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void UiAccessContext_DisposeStopsProductionSubscriptions_AndRepeatedDisposeIsSafe()
        {
            var hostObject = new GameObject(nameof(UiAccessContext_DisposeStopsProductionSubscriptions_AndRepeatedDisposeIsSafe));
            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[]
                {
                    CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right),
                }));
                var context = host.UiAccess;
                var policy = (GameplayHostCommandAdmissionPolicy)typeof(GameplayHostUiAccessContext)
                    .GetField("_admissionPolicyLifetime", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .GetValue(context);
                foreach (var query in new object[] { context.QueryFacade.Session, context.QueryFacade.PlayerHud, context.QueryFacade.SurfaceButtonRemainders })
                {
                    Assert.That(query.GetType().GetField("_admissionPolicy", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        .GetValue(query), Is.SameAs(policy));
                }

                Assert.That(policy.TryCreateSnapshot(out var initial), Is.True);
                var frames = 0;
                context.PresentationFeed.FramePublished += _ => frames++;
                Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
                Assert.That(policy.TryCreateSnapshot(out var completed), Is.True);
                Assert.That(completed, Is.Not.SameAs(initial));
                Assert.That(frames, Is.EqualTo(1));

                context.Dispose();
                context.Dispose();
                Assert.That(host.InputHost.RunSingleTick(), Is.Not.Null);
                Assert.That(policy.TryCreateSnapshot(out var afterDispose), Is.True);
                Assert.That(afterDispose, Is.SameAs(completed));
                Assert.That(frames, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(hostObject);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        [Category("Extended")]
        public void UiAccessContext_DisposesEachOwnerOnce_EvenWhenPolicyDisposalThrows(bool throws)
        {
            var hostObject = new GameObject(nameof(UiAccessContext_DisposesEachOwnerOnce_EvenWhenPolicyDisposalThrows));
            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(CreateConfiguration(new[] { CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right) }));
                var lifetime = new RecordingLifetime(throws);
                var feed = new RecordingDisposableFeed();
                var context = new GameplayHostUiAccessContext(lifetime, host.UiAccess.QueryFacade, feed, host.UiAccess.PauseService);
                if (throws)
                    Assert.Throws<System.InvalidOperationException>(() => context.Dispose());
                else
                    context.Dispose();
                context.Dispose();
                Assert.That(lifetime.Count, Is.EqualTo(1));
                Assert.That(feed.Count, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void UiAccessFactory_CompositionFailureReleasesPolicyAndFeedSubscriptions()
        {
            var hostObject = new GameObject(nameof(UiAccessFactory_CompositionFailureReleasesPolicyAndFeedSubscriptions));
            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                var configuration = CreateConfiguration(new[] { CreatePlayerEntity(new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right) });
                var definition = new TileFeatureRuntimeDefinition(301, TileFeatureActivationRule.Always,
                    Direction2D.None, TileFeatureBoxSelector.AnyPushableBox, boundEntityId: 0);
                configuration.TileFeatureDefinitions = new[] { definition, definition };
                var exception = Assert.Throws<System.InvalidOperationException>(() => host.Initialize(configuration));
                Assert.That(exception.Message, Does.Contain("Duplicate TileFeatureRuntimeDefinition"));
                Assert.That(host.UiAccess, Is.Null);
                var input = hostObject.GetComponent<GameplayInputHost>();
                var handlers = (System.Delegate)typeof(GameplayInputHost)
                    .GetField("TickCompleted", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(input);
                foreach (var handler in handlers?.GetInvocationList() ?? System.Array.Empty<System.Delegate>())
                {
                    Assert.That(handler.Target, Is.Not.InstanceOf<GameplayHostCommandAdmissionPolicy>());
                    Assert.That(handler.Target, Is.Not.InstanceOf<GameplayHostPresentationFeed>());
                }
            }
            finally
            {
                Object.DestroyImmediate(hostObject);
            }
        }

        private sealed class RecordingLifetime : System.IDisposable
        {
            private readonly bool _throws;
            public RecordingLifetime(bool throws) { _throws = throws; }
            public int Count { get; private set; }
            public void Dispose()
            {
                Count++;
                if (_throws) throw new System.InvalidOperationException("Disposal failure.");
            }
        }

        private sealed class RecordingDisposableFeed : IGameplayPresentationFeed, System.IDisposable
        {
            public event System.Action<GameplayPresentationFrame> FramePublished { add { } remove { } }
            public event System.Action<GameplayPresentationState> StateChanged { add { } remove { } }
            public event System.Action<GameplayLevelFailedReadModel> LevelFailedCommitted { add { } remove { } }
            public GameplayPresentationState CurrentState => default;
            public MinimalStageCompletionReadModel CurrentMinimalStageCompletion => null;
            public GameplayLevelFailedReadModel CurrentLevelFailed => default;
            public bool HasPendingStageClearPresentation => false;
            public int Count { get; private set; }
            public void Dispose() { Count++; }
        }

        private static GameplayHostCommandAdmissionPolicy CreatePolicy(GameplaySceneHost host)
        {
            return new GameplayHostCommandAdmissionPolicy(
                host.WorldState,
                host.TickRunner,
                host.InputHost,
                host.Presenter,
                (GameplayHostPauseService)host.UiAccess.PauseService);
        }

        private static string BuildSnapshotSemanticDump(WorldSnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.Append("Bounds=").Append(snapshot.BoardBounds).Append('\n');
            builder.Append("Topology=").Append(snapshot.Topology.BottomFace).Append('\n');

            var entities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(entities);
            builder.Append("Entities=").Append(entities.Count).Append('\n');
            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                builder
                    .Append(entity.entityId).Append('|')
                    .Append(entity.position.face).Append('|')
                    .Append(entity.position.x).Append('|')
                    .Append(entity.position.y).Append('|')
                    .Append(entity.hp).Append('|')
                    .Append(entity.maxHp).Append('|')
                    .Append(entity.teamId).Append('|')
                    .Append(entity.type).Append('|')
                    .Append(entity.unitRole).Append('|')
                    .Append(entity.state).Append('|')
                    .Append(entity.stateTimer).Append('|')
                    .Append(entity.facing).Append('|')
                    .Append(entity.boardPresence).Append('|')
                    .Append(entity.markedForDeath).Append('|')
                    .Append(entity.spawnTick).Append('|')
                    .Append((int)entity.boxCapabilities).Append('|')
                    .Append(entity.kineticInstigatorEntityId).Append('|')
                    .Append(entity.kineticInstigatorTeamId).Append('|')
                    .Append(entity.aiMode).Append('|')
                    .Append(entity.aiStateTimer).Append('|')
                    .Append(entity.enemyLocomotionCooldownTicks).Append('\n');
            }

            AppendOccupancyEntries(builder, "Units", snapshot, static (source, buffer) => source.EnumerateUnitOccupancyOrdered(buffer));
            AppendOccupancyEntries(builder, "Solids", snapshot, static (source, buffer) => source.EnumerateSolidOccupancyOrdered(buffer));
            AppendPlayerControlEntries(builder, snapshot);
            AppendUnitKinematicEntries(builder, snapshot);
            AppendPlayerDamageEntries(builder, snapshot);
            AppendEnemyActionEntries(builder, snapshot);
            AppendEnemyJumpEntries(builder, snapshot);
            AppendExecutionLockEntries(builder, snapshot);

            return builder.ToString();
        }

        private static void AppendOccupancyEntries(
            StringBuilder builder,
            string label,
            WorldSnapshot snapshot,
            System.Action<WorldSnapshot, List<SnapshotOccupancyEntry>> populate)
        {
            var entries = new List<SnapshotOccupancyEntry>();
            populate(snapshot, entries);
            builder.Append(label).Append('=').Append(entries.Count).Append('\n');
            for (var i = 0; i < entries.Count; i++)
            {
                builder
                    .Append(entries[i].Cell.face).Append('|')
                    .Append(entries[i].Cell.x).Append('|')
                    .Append(entries[i].Cell.y).Append('|')
                    .Append(entries[i].EntityId).Append('\n');
            }
        }

        private static void AppendPlayerControlEntries(StringBuilder builder, WorldSnapshot snapshot)
        {
            var entries = new List<PlayerControlSnapshotEntry>();
            snapshot.EnumeratePlayerControlStatesOrdered(entries);
            builder.Append("PlayerControl=").Append(entries.Count).Append('\n');
            for (var i = 0; i < entries.Count; i++)
            {
                var state = entries[i].State;
                var action = state.activeAction;
                builder
                    .Append(entries[i].EntityId).Append('|')
                    .Append(state.nextExplicitActionAllowedTick).Append('|')
                    .Append(state.actionSequenceCounter).Append('|')
                    .Append(action.kind).Append('|')
                    .Append(action.sequence).Append('|')
                    .Append(action.direction).Append('|')
                    .Append(action.targetEntityId).Append('|')
                    .Append(action.startTick).Append('|')
                    .Append(action.executeTick).Append('|')
                    .Append(action.recoveryEndTick).Append('|')
                    .Append(action.executionAttempted).Append('\n');
            }
        }

        private static void AppendUnitKinematicEntries(StringBuilder builder, WorldSnapshot snapshot)
        {
            var entries = new List<UnitKinematicSnapshotEntry>();
            snapshot.EnumerateUnitKinematicStatesOrdered(entries);
            builder.Append("UnitKinematic=").Append(entries.Count).Append('\n');
            for (var i = 0; i < entries.Count; i++)
            {
                var state = entries[i].State;
                builder
                    .Append(entries[i].EntityId).Append('|')
                    .Append(state.mode).Append('|')
                    .Append(state.localOffset.X.RawValue).Append('|')
                    .Append(state.localOffset.Y.RawValue).Append('|')
                    .Append(state.remainingTicks).Append('|')
                    .Append(state.elapsedTicks).Append('|')
                    .Append(state.totalTicks).Append('|')
                    .Append(state.startedTick).Append('|')
                    .Append(state.commitTick).Append('|')
                    .Append(state.stepDirectionX).Append('|')
                    .Append(state.stepDirectionY).Append('\n');
            }
        }

        private static void AppendPlayerDamageEntries(StringBuilder builder, WorldSnapshot snapshot)
        {
            var entries = new List<PlayerDamageSnapshotEntry>();
            snapshot.EnumeratePlayerDamageStatesOrdered(entries);
            builder.Append("PlayerDamage=").Append(entries.Count).Append('\n');
            for (var i = 0; i < entries.Count; i++)
            {
                builder
                    .Append(entries[i].EntityId).Append('|')
                    .Append(entries[i].State.nextDamageAllowedTick).Append('\n');
            }
        }

        private static void AppendEnemyActionEntries(StringBuilder builder, WorldSnapshot snapshot)
        {
            var entries = new List<EnemyActionSnapshotEntry>();
            snapshot.EnumerateEnemyActionStatesOrdered(entries);
            builder.Append("EnemyAction=").Append(entries.Count).Append('\n');
            for (var i = 0; i < entries.Count; i++)
            {
                var state = entries[i].State;
                builder
                    .Append(entries[i].EntityId).Append('|')
                    .Append(state.kind).Append('|')
                    .Append(state.sequence).Append('|')
                    .Append(state.lockedTargetEntityId).Append('|')
                    .Append(state.direction).Append('|')
                    .Append(state.startTick).Append('|')
                    .Append(state.executeTick).Append('|')
                    .Append(state.executionAttempted).Append('\n');
            }
        }

        private static void AppendEnemyJumpEntries(StringBuilder builder, WorldSnapshot snapshot)
        {
            var entries = new List<EnemyJumpSnapshotEntry>();
            snapshot.EnumerateEnemyJumpStatesOrdered(entries);
            builder.Append("EnemyJump=").Append(entries.Count).Append('\n');
            for (var i = 0; i < entries.Count; i++)
            {
                var state = entries[i].State;
                builder
                    .Append(entries[i].EntityId).Append('|')
                    .Append(state.phase).Append('|')
                    .Append(state.sequence).Append('|')
                    .Append(state.sourceCell.face).Append('|')
                    .Append(state.sourceCell.x).Append('|')
                    .Append(state.sourceCell.y).Append('|')
                    .Append(state.lockedTargetCell.face).Append('|')
                    .Append(state.lockedTargetCell.x).Append('|')
                    .Append(state.lockedTargetCell.y).Append('|')
                    .Append(state.windupEndTick).Append('|')
                    .Append(state.landingTick).Append('|')
                    .Append(state.cooldownRemainingTicks).Append('|')
                    .Append(state.retryCount).Append('|')
                    .Append(state.topologySuspendLastTick).Append('\n');
            }
        }

        private static void AppendExecutionLockEntries(StringBuilder builder, WorldSnapshot snapshot)
        {
            var entries = new List<EntityExecutionLockSnapshotEntry>();
            snapshot.EnumerateEntityExecutionLockStatesOrdered(entries);
            builder.Append("ExecutionLock=").Append(entries.Count).Append('\n');
            for (var i = 0; i < entries.Count; i++)
            {
                builder
                    .Append(entries[i].EntityId).Append('|')
                    .Append(entries[i].State.phase).Append('|')
                    .Append(entries[i].State.sequence).Append('|')
                    .Append(entries[i].State.unlockTickExclusive).Append('\n');
            }
        }

        private static GameplaySceneHostConfiguration CreateConfiguration(
            EntityState[] initialEntities,
            StageObjectiveRuntimeDefinition objectiveDefinition = null,
            BoardBounds? boardBounds = null,
            PlayerControlTimingSettings playerControlTiming = null)
        {
            var configuration = new GameplaySceneHostConfiguration
            {
                AutoAdvanceTicks = false,
                AutoCreateViews = false,
                InitialBoardBounds = boardBounds ?? new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                InitialEntities = initialEntities,
                InitialTopology = new CubeTopologyState(FaceId.Floor),
                ObjectiveRuntimeDefinition = objectiveDefinition ?? StageObjectiveRuntimeDefinition.Disabled,
                PlayerEntityId = 10,
                PlayerControlTiming = playerControlTiming ?? PlayerControlTimingSettings.CreateDefault(),
            };
            configuration.ApplyRuntimeFeatureFlags(GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);
            return configuration;
        }

        private static EntityState CreatePlayerEntity(
            SurfaceCell position,
            Direction facing = Direction.Up)
        {
            return new EntityState
            {
                entityId = 10,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                state = EntityPhaseState.Idle,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = EnemyAiMode.None,
            };
        }
    }
}
