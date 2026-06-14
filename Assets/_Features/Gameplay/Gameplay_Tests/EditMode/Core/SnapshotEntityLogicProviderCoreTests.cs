using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class SnapshotEntityLogicProviderCoreTests
    {
        [Test]
        [Category("Core")]
        public void SnapshotEntityLogicProvider_PhaseOwnershipConflict_DetectsDuplicateOwner()
        {
            var provider = new SnapshotEntityLogicProvider(new IEntityLogicFactory[]
            {
                new ScriptedFactory(entity => new MovementBindingLogic($"first-{entity.entityId}", entity.entityId)),
                new ScriptedFactory(entity => new MovementBindingLogic($"second-{entity.entityId}", entity.entityId)),
            });

            var logicSet = provider.Build(
                CreateSnapshot(CreateEntity(10, 0)),
                Array.Empty<IEntityLogic>());

            CollectionAssert.AreEqual(
                new[] { "first-10" },
                GetLabels(logicSet.MovementLogics));
        }

        [Test]
        [Category("Core")]
        public void SnapshotEntityLogicProvider_PhaseOwnershipConflict_AllowsDifferentOwners()
        {
            var provider = new SnapshotEntityLogicProvider(new IEntityLogicFactory[]
            {
                new ScriptedFactory(entity => new MovementBindingLogic($"move-{entity.entityId}", entity.entityId)),
            });

            var logicSet = provider.Build(
                CreateSnapshot(
                    CreateEntity(10, 0),
                    CreateEntity(20, 1)),
                Array.Empty<IEntityLogic>());

            CollectionAssert.AreEqual(
                new[] { "move-10", "move-20" },
                GetLabels(logicSet.MovementLogics));
        }

        [Test]
        [Category("Core")]
        public void SnapshotEntityLogicProvider_PhaseOwnershipConflict_AllowsNonConflictingPhases()
        {
            var provider = new SnapshotEntityLogicProvider(new IEntityLogicFactory[]
            {
                new ScriptedFactory(entity => new MovementBindingLogic($"move-{entity.entityId}", entity.entityId)),
                new ScriptedFactory(entity => new AttackBindingLogic($"attack-{entity.entityId}", entity.entityId)),
            });

            var logicSet = provider.Build(
                CreateSnapshot(CreateEntity(10, 0)),
                Array.Empty<IEntityLogic>());

            CollectionAssert.AreEqual(
                new[] { "move-10" },
                GetLabels(logicSet.MovementLogics));
            CollectionAssert.AreEqual(
                new[] { "attack-10" },
                GetLabels(logicSet.AttackLogics));
        }

        [Test]
        [Category("Core")]
        public void SnapshotEntityLogicProvider_PhaseOwnershipConflict_PreservesBindinglessLogicSemantics()
        {
            var existingLogics = new IEntityLogic[]
            {
                new BindinglessMovementLogic("bindingless-existing"),
                new MovementBindingLogic("move-existing-10", 10),
                new AttackBindingLogic("attack-existing-10", 10),
            };

            var candidates = new IEntityLogic[]
            {
                new BindinglessMovementLogic("bindingless-candidate"),
                new MovementBindingLogic("move-conflict-10", 10),
                new MovementBindingLogic("move-free-20", 20),
                new AttackBindingLogic("attack-conflict-10", 10),
                new PreMovementBindingLogic("pre-free-10", 10),
                new BindinglessEmptyLogic(),
            };

            foreach (var candidate in candidates)
            {
                Assert.That(
                    SnapshotEntityLogicProvider.HasPhaseOwnershipConflictIndexedForTest(candidate, existingLogics),
                    Is.EqualTo(SnapshotEntityLogicProvider.HasPhaseOwnershipConflictSlowForTest(candidate, existingLogics)),
                    candidate.GetType().Name);
            }
        }

        [Test]
        [Category("Core")]
        public void SnapshotEntityLogicProvider_Build_PreservesProviderSet()
        {
            var staticLogics = new IEntityLogic[]
            {
                new MovementBindingLogic("static-move-99", 99),
            };
            var provider = new SnapshotEntityLogicProvider(new IEntityLogicFactory[]
            {
                new ScriptedFactory(entity => new MovementBindingLogic($"move-{entity.entityId}", entity.entityId)),
                new ScriptedFactory(entity => new AttackBindingLogic($"attack-{entity.entityId}", entity.entityId)),
                new ScriptedFactory(entity => new MovementBindingLogic($"duplicate-move-{entity.entityId}", entity.entityId)),
                new ScriptedFactory(entity => new PreMovementBindingLogic($"pre-{entity.entityId}", entity.entityId)),
            });

            var logicSet = provider.Build(
                CreateSnapshot(
                    CreateEntity(10, 0),
                    CreateEntity(20, 1)),
                staticLogics);

            CollectionAssert.AreEqual(
                new[] { "static-move-99", "move-10", "move-20" },
                GetLabels(logicSet.MovementLogics));
            CollectionAssert.AreEqual(
                new[] { "attack-10", "attack-20" },
                GetLabels(logicSet.AttackLogics));
            CollectionAssert.AreEqual(
                new[] { "pre-10", "pre-20" },
                GetLabels(logicSet.PreMovementStateLogics));
        }

        [Test]
        [Category("Core")]
        public void SnapshotEntityLogicProvider_Build_PreservesExecutionOrder()
        {
            var provider = new SnapshotEntityLogicProvider(new IEntityLogicFactory[]
            {
                new ScriptedFactory(entity => new PreMovementBindingLogic($"pre-A-{entity.entityId}", entity.entityId)),
                new ScriptedFactory(entity => new MovementBindingLogic($"move-B-{entity.entityId}", entity.entityId)),
                new ScriptedFactory(entity => new AttackBindingLogic($"attack-C-{entity.entityId}", entity.entityId)),
                new ScriptedFactory(entity => new PreMovementBindingLogic($"pre-D-{entity.entityId}", entity.entityId)),
            });

            var logicSet = provider.Build(
                CreateSnapshot(
                    CreateEntity(10, 0),
                    CreateEntity(20, 1)),
                Array.Empty<IEntityLogic>());

            CollectionAssert.AreEqual(
                new[] { "pre-A-10", "pre-A-20" },
                GetLabels(logicSet.PreMovementStateLogics));
            CollectionAssert.AreEqual(
                new[] { "move-B-10", "move-B-20" },
                GetLabels(logicSet.MovementLogics));
            CollectionAssert.AreEqual(
                new[] { "attack-C-10", "attack-C-20" },
                GetLabels(logicSet.AttackLogics));
        }

        [Test]
        [Category("Core")]
        public void SnapshotEntityLogicProvider_PlayerControlStateLogic_OrderPreserved()
        {
            var provider = new SnapshotEntityLogicProvider(Array.Empty<IEntityLogicFactory>());
            var staticLogics = new IEntityLogic[]
            {
                new PreMovementBindingLogic("pre-before-player", 50),
                new PlayerLogic(10),
                new PreMovementBindingLogic("pre-after-player", 60),
            };

            var logicSet = provider.Build(
                CreateSnapshot(),
                staticLogics);

            CollectionAssert.AreEqual(
                new[]
                {
                    "pre-before-player",
                    "PlayerControlStateLogic:10",
                    "pre-after-player",
                },
                GetLabels(logicSet.PreMovementStateLogics));
            CollectionAssert.AreEqual(
                new[] { "PlayerLogic:10" },
                GetLabels(logicSet.MovementLogics));
        }

        private static WorldSnapshot CreateSnapshot(params EntityState[] entities)
        {
            return GameplayCompositionRoot.CreateWorldState(
                    entities,
                    new BoardBounds(new Vector2Int(-2, -2), new Vector2Int(4, 4)),
                    new CubeTopologyState(FaceId.Floor))
                .CreateSnapshot();
        }

        private static EntityState CreateEntity(int entityId, int x)
        {
            return new EntityState
            {
                entityId = entityId,
                position = new SurfaceCell(FaceId.Floor, x, 0),
                hp = 1,
                maxHp = 1,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Enemy,
                boardPresence = EntityBoardPresence.Occupying,
                facing = Direction.Right,
            };
        }

        private static string[] GetLabels<TLogic>(IEnumerable<TLogic> logics)
            where TLogic : IEntityLogic
        {
            return logics.Select(logic => GetLabel(logic)).ToArray();
        }

        private static string GetLabel(IEntityLogic logic)
        {
            if (logic is ILabeledLogic labeledLogic)
            {
                return labeledLogic.Label;
            }

            if (logic is IEntityLogicSourceBinding binding)
            {
                return $"{logic.GetType().Name}:{binding.ControlledEntityId}";
            }

            return logic.GetType().Name;
        }

        private interface ILabeledLogic : IEntityLogic
        {
            string Label { get; }
        }

        private sealed class ScriptedFactory : IEntityLogicFactory
        {
            private readonly Func<EntityState, IEntityLogic> _create;

            public ScriptedFactory(Func<EntityState, IEntityLogic> create)
            {
                _create = create;
            }

            public bool CanCreate(in EntityLogicCreationContext context)
            {
                return true;
            }

            public IEntityLogic Create(in EntityLogicCreationContext context)
            {
                return _create(context.Entity);
            }
        }

        private sealed class MovementBindingLogic : ILabeledLogic, IMovementEntityLogic, IEntityLogicSourceBinding
        {
            public MovementBindingLogic(string label, int controlledEntityId)
            {
                Label = label;
                ControlledEntityId = controlledEntityId;
            }

            public string Label { get; }

            public int ControlledEntityId { get; }

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
            }
        }

        private sealed class BindinglessMovementLogic : ILabeledLogic, IMovementEntityLogic
        {
            public BindinglessMovementLogic(string label)
            {
                Label = label;
            }

            public string Label { get; }

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
            }
        }

        private sealed class PreMovementBindingLogic : ILabeledLogic, IPreMovementStateLogic, IEntityLogicSourceBinding
        {
            public PreMovementBindingLogic(string label, int controlledEntityId)
            {
                Label = label;
                ControlledEntityId = controlledEntityId;
            }

            public string Label { get; }

            public int ControlledEntityId { get; }

            public void CommitPreMovementState(
                WorldSnapshot snapshot,
                in TickInput input,
                IPreMovementStateCommitContext writeContext,
                List<string> updates,
                List<PlayerActionTransition> actionTransitions)
            {
            }
        }

        private sealed class AttackBindingLogic : ILabeledLogic, IAttackEntityLogic, IEntityLogicSourceBinding
        {
            public AttackBindingLogic(string label, int controlledEntityId)
            {
                Label = label;
                ControlledEntityId = controlledEntityId;
            }

            public string Label { get; }

            public int ControlledEntityId { get; }

            public void CollectAttackIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawAttackIntent> buffer)
            {
            }
        }

        private sealed class BindinglessEmptyLogic : IEntityLogic
        {
        }
    }
}
