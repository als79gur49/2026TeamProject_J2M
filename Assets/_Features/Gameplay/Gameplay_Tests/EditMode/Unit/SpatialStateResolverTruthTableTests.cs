using System;
using System.Collections.Generic;
using System.IO;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class SpatialStateResolverTruthTableTests
    {
        [Test]
        [Category("Extended")]
        public void Resolve_OccupyingWithoutJump_OnActiveFace_ReturnsAnchoredVisible()
        {
            var resolved = SpatialStateResolver.Resolve(
                EntityBoardPresence.Occupying,
                jumpState: null,
                isFaceActive: true);

            AssertResolved(
                resolved,
                SpatialState.Anchored,
                claimsAuthoritativeOccupancy: true,
                isGameplayVisible: true,
                ResolvedSpatialStateSource.DefaultAnchored);
        }

        [Test]
        [Category("Extended")]
        public void Resolve_OccupyingWithoutJump_OnInactiveFace_ReturnsAnchoredHidden()
        {
            var resolved = SpatialStateResolver.Resolve(
                EntityBoardPresence.Occupying,
                jumpState: null,
                isFaceActive: false);

            AssertResolved(
                resolved,
                SpatialState.Anchored,
                claimsAuthoritativeOccupancy: true,
                isGameplayVisible: false,
                ResolvedSpatialStateSource.AnchoredHiddenByTopology);
        }

        [Test]
        [Category("Extended")]
        public void Resolve_OccupyingWithWindup_RemainsAnchored()
        {
            var resolved = SpatialStateResolver.Resolve(
                EntityBoardPresence.Occupying,
                new EnemyJumpRuntimeState { phase = EnemyJumpPhase.Windup },
                isFaceActive: true);

            AssertResolved(
                resolved,
                SpatialState.Anchored,
                claimsAuthoritativeOccupancy: true,
                isGameplayVisible: true,
                ResolvedSpatialStateSource.DefaultAnchored);
        }

        [Test]
        [Category("Extended")]
        public void Resolve_OccupyingWithCooldown_RemainsAnchored()
        {
            var resolved = SpatialStateResolver.Resolve(
                EntityBoardPresence.Occupying,
                new EnemyJumpRuntimeState { phase = EnemyJumpPhase.Cooldown },
                isFaceActive: false);

            AssertResolved(
                resolved,
                SpatialState.Anchored,
                claimsAuthoritativeOccupancy: true,
                isGameplayVisible: false,
                ResolvedSpatialStateSource.AnchoredHiddenByTopology);
        }

        [Test]
        [Category("Extended")]
        public void Resolve_DetachedAirborne_ReturnsAirborne()
        {
            var resolved = SpatialStateResolver.Resolve(
                EntityBoardPresence.Detached,
                new EnemyJumpRuntimeState { phase = EnemyJumpPhase.Airborne },
                isFaceActive: true);

            AssertResolved(
                resolved,
                SpatialState.Airborne,
                claimsAuthoritativeOccupancy: false,
                isGameplayVisible: false,
                ResolvedSpatialStateSource.JumpAirborne);
        }

        [Test]
        [Category("Extended")]
        public void Resolve_DetachedWithoutJump_RemainsAnchoredWithoutOccupancyClaim()
        {
            var resolved = SpatialStateResolver.Resolve(
                EntityBoardPresence.Detached,
                jumpState: null,
                isFaceActive: true);

            AssertResolved(
                resolved,
                SpatialState.Anchored,
                claimsAuthoritativeOccupancy: false,
                isGameplayVisible: false,
                ResolvedSpatialStateSource.DetachedNonAirborne);
        }

        [Test]
        [Category("Extended")]
        public void Resolve_DetachedWindup_ThrowsInvalidSourceCombination()
        {
            Assert.Throws<InvalidOperationException>(
                () => SpatialStateResolver.Resolve(
                    EntityBoardPresence.Detached,
                    new EnemyJumpRuntimeState { phase = EnemyJumpPhase.Windup },
                    isFaceActive: true));
        }

        [Test]
        [Category("Extended")]
        public void Resolve_OccupyingAirborne_ThrowsInvalidSourceCombination()
        {
            Assert.Throws<InvalidOperationException>(
                () => SpatialStateResolver.Resolve(
                    EntityBoardPresence.Occupying,
                    new EnemyJumpRuntimeState { phase = EnemyJumpPhase.Airborne },
                    isFaceActive: true));
        }

        [Test]
        [Category("Extended")]
        public void WorldSnapshot_HasAnyUnitAt_And_ImpactTargetQueries_IgnoreAirborneOccupant()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(40, targetCell, teamId: 2, boardPresence: EntityBoardPresence.Detached),
                });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyJumpState(
                40,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Airborne,
                    sequence = 1,
                    sourceCell = new SurfaceCell(FaceId.Floor, 0, 0),
                    lockedTargetCell = targetCell,
                    landingTick = 1,
                });

            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetResolvedSpatialState(40, out var spatialState), Is.True);
            Assert.That(spatialState.Kind, Is.EqualTo(SpatialState.Airborne));
            Assert.That(snapshot.HasAnyUnitAt(targetCell), Is.False);
            Assert.That(snapshot.TryPickImpactTargetAt(targetCell, sourceTeamId: 1, out _), Is.False);
            Assert.That(SpatialStateSemantics.ParticipatesInTraversalBlocking(spatialState), Is.False);
            Assert.That(SpatialStateSemantics.ParticipatesInSettlementBlocking(spatialState), Is.False);
            Assert.That(SpatialStateSemantics.ParticipatesInTargetSelection(spatialState), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void ReservedSpatialStates_AreConstrainedToAllowlistedFiles_AndFailFast()
        {
            Assert.Throws<InvalidOperationException>(() => SpatialStateSemantics.EnsureProductionSupported(SpatialState.Phased));
            Assert.Throws<InvalidOperationException>(() => SpatialStateSemantics.EnsureProductionSupported(SpatialState.Attached));

            var filesUsingReservedStates = FindFilesContainingReservedStates();
            CollectionAssert.IsSubsetOf(
                filesUsingReservedStates,
                new[]
                {
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/SpatialState.cs"),
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/SpatialStateResolverTruthTableTests.cs"),
                });
        }

        private static void AssertResolved(
            ResolvedSpatialState resolved,
            SpatialState expectedKind,
            bool claimsAuthoritativeOccupancy,
            bool isGameplayVisible,
            ResolvedSpatialStateSource expectedSource)
        {
            Assert.That(resolved.Kind, Is.EqualTo(expectedKind));
            Assert.That(resolved.ClaimsAuthoritativeOccupancy, Is.EqualTo(claimsAuthoritativeOccupancy));
            Assert.That(resolved.IsGameplayVisible, Is.EqualTo(isGameplayVisible));
            Assert.That(resolved.Source, Is.EqualTo(expectedSource));
        }

        private static IEnumerable<string> FindFilesContainingReservedStates()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(Path.Combine(projectRoot, "Assets"), "*.cs", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(file);
                if (!source.Contains("SpatialState.Phased", StringComparison.Ordinal) &&
                    !source.Contains("SpatialState.Attached", StringComparison.Ordinal))
                {
                    continue;
                }

                results.Add(NormalizeRelativePath(Path.GetRelativePath(projectRoot, file)));
            }

            return results;
        }

        private static string NormalizeRelativePath(string relativePath)
        {
            return relativePath.Replace('\\', '/');
        }

        private static EntityState CreateUnit(
            int entityId,
            SurfaceCell position,
            int teamId,
            EntityBoardPresence boardPresence)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = teamId,
                type = EntityType.Unit,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = boardPresence,
            };
        }
    }
}
