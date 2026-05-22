using System;
using System.Collections.Generic;
using System.IO;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class TileFeatureOverlayArchitectureTests
    {
        private const string TileFeatureOverlayAdrPath =
            "Docs/Architecture/ADR/ADR-006-TileFeature-Overlay-Layer-Gate.md";
        private const string GravityFieldLockedTargetPresentationPolicyPath =
            "Docs/Architecture/GravityField-LockedTarget-Presentation-Policy.md";
        private const string TilePresentationRequestPlannerPath =
            "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TilePresentationRequestPlanner.cs";
        private const string GameplayTickPresentationCoordinatorPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs";
        private const string GameplayHostRuntimeFactoryPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayHostRuntimeFactory.cs";
        private const string SurfaceCellPresentationPoseResolverPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoardSurfaceCellPresentationPoseResolver.cs";
        private const string GameplayLoopRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_Loop/Runtime";
        private const string GameplayHostRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime";
        private const string TileFeatureAudioRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_TileFeatureAudio/Runtime";
        private const string UiRuntimePath =
            "Assets/_Features/UI";
        private const string StageDefinitionPath =
            "Assets/_Features/Stages/Runtime/StageDefinition.cs";
        private const string StageRuntimeBuildResultPath =
            "Assets/_Features/Stages/Runtime/StageRuntimeBuildResult.cs";
        private const string StagePresentationDefinitionPath =
            "Assets/_Features/Stages/Runtime/Content/StagePresentationDefinition.cs";
        private const string TileFeatureAudioTypesPath =
            "Assets/_Features/Gameplay/Gameplay_TileFeatureAudio/Runtime/TileFeatureAudioTypes.cs";
        private const string GravityFieldPresentationRequestPlannerPath =
            "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/GravityFieldPresentationRequestPlanner.cs";
        private const string GravityFieldVisualControllerPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GravityFieldVisualPresentationController.cs";
        private const string GravityFieldAudioControllerPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GravityFieldAudioPresentationController.cs";
        private const string GravityFieldAudioRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_GravityFieldAudio/Runtime";
        private const string WorldStatePath =
            "Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldState.cs";
        private const string GameplayBoardStateRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_BoardState/Runtime";
        private static readonly string[] TileFeatureAudioForbiddenConsumerPaths =
        {
            "Assets/_Features/UI",
            "Assets/_Features/Gameplay/Gameplay_Audio",
            "Assets/_Features/Gameplay/Gameplay_ActionAudio",
        };
        private static readonly string[] TileFeatureVisualRuntimePaths =
        {
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/ITileFeatureVisualRegistry.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/TileFeatureVisualRegistry.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/TileFeatureVisualTargetView.cs",
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/TileFeatureVisualPresentationController.cs",
        };
        private static readonly string[] ForbiddenTerrainFlagTokens =
        {
            "Trap",
            "Hazard",
            "Buff",
            "Trigger",
            "Aura",
            "Zone",
            "TileFeature",
            "MoonBlockGenerator",
            "Barricade",
            "Exit",
            "GravityField",
            "Effect",
        };

        [Test]
        [Category("Core")]
        public void TileFeatureOverlayGateDocument_Exists()
        {
            Assert.That(File.Exists(GetAbsolutePath(TileFeatureOverlayAdrPath)), Is.True);
        }

        [Test]
        [Category("Core")]
        public void TileFeatureOverlayGate_ForbidsOccupancyAndTerrainReuse()
        {
            var document = File.ReadAllText(GetAbsolutePath(TileFeatureOverlayAdrPath));

            Assert.That(document, Does.Contain("TileFeature is a `SurfaceCell`-based gameplay overlay layer."));
            Assert.That(document, Does.Contain("TileFeature is not Unit/Solid/Projectile occupancy."));
            Assert.That(document, Does.Contain("Blocking Terrain remains owned by `TerrainData` and `TerrainFlags`"));
            Assert.That(document, Does.Contain("Box + TileFeature is allowed."));
            Assert.That(document, Does.Contain("Other Solid + TileFeature"));
            Assert.That(document, Does.Contain("requires an explicit future policy decision"));
            Assert.That(document, Does.Contain("## Implemented Order"));
            Assert.That(document, Does.Contain("19. MoonBlockGenerated feedback"));
            Assert.That(document, Does.Contain("Dynamic TileEffect mutation must not be implemented before TileFeature state/query/export/hash exists"));
            Assert.That(document, Does.Contain("TileEffect-free ticks must not increase snapshot materialization budget."));
            Assert.That(document, Does.Contain("`StageRuntimeBuildResult` is a gameplay-only seed."));
            Assert.That(document, Does.Contain("Presentation prefab and binding data are owned by `StagePresentationDefinition`"));
            Assert.That(document, Does.Contain("VFX, audio, and UI must not call `WorldState.CreateSnapshot`"));
        }

        [Test]
        [Category("Core")]
        public void GravityFieldLockedTargetPolicy_DocumentsReadModelAndLockedBoxOneShotScope()
        {
            var document = File.ReadAllText(GetAbsolutePath(GravityFieldLockedTargetPresentationPolicyPath));
            var requiredSnippets = new[]
            {
                "GravityField is `EntityType.Box + BoxArchetype.GravityField`, not a TileFeature.",
                "`GravityFieldVisualState.LockedTargetEntityIds` is the continuous presentation read model",
                "Target dimming consumes the previous/current `LockedTargetEntityIds` read model diff and remains independent of one-shot events.",
                "`GravityFieldPresentationEventKind.LockedBox`, `GravityFieldPresentationRequestKind.LockedBox`, and `GravityFieldAudioCue.LockedBox` are implemented",
                "`GravityFieldLockedBoxPayload` carries `EmitterEntityId`, `TargetEntityId`, `EmitterCell`, and `TargetCell`.",
                "LockedBox one-shot feedback is presentation-only and complements target dimming",
                "`MaterialPropertyBlock`-based actual dimming remains a future presentation-only step.",
                "The read model must not be inferred from a final snapshot diff.",
                "One-shot `LockedBox` event/audio debounces by `EmitterEntityId + TargetEntityId + ActiveWindow`.",
                "`Charging -> Active` starts a new active window.",
                "`Active -> Charging`, ineligible reset, and emitter destroyed/detached clear active-window memory.",
                "The same emitter-target pair emits at most once per active window.",
                "`LockedBox` audio is optional Sfx one-shot only",
                "Repeated one-shot dedupe belongs to resolver event generation",
                "Locked target presentation data is presentation-only and does not enter the canonical determinism hash.",
                "LockedBox one-shot state is transient resolver/pipeline memory",
                "Environmental destroy immunity is not implemented by LockedBox one-shot feedback.",
                "`TickPipeline` transports facts but must not execute prefab, audio, UI, or material work.",
                "GravityField remains `EntityType.Box + BoxArchetype.GravityField`, not `EntityType." + "GravityField` or `TileFeatureKind." + "GravityField`.",
            };

            for (var i = 0; i < requiredSnippets.Length; i++)
            {
                Assert.That(document, Does.Contain(requiredSnippets[i]));
            }
        }

        [Test]
        [Category("Core")]
        public void TileFeatureOverlayGate_DocumentsImplementedBarricadeMovementBlockerPolicy()
        {
            var document = File.ReadAllText(GetAbsolutePath(TileFeatureOverlayAdrPath));
            var requiredSnippets = new[]
            {
                "Button latch is runtime state stored as `TileFeatureFlags.Activated`.",
                "`ButtonActivatedCondition` reads only the final `WorldSnapshot` `Activated` flag.",
                "Button latch accepts accepted terminal stop facts from Push, Slide, and non-impact Flip movement",
                "Flip impact follow-through is not a Button latch source.",
                "`ButtonActivated` event `TargetEntityId` is `0`.",
                "`ButtonActivated` event does not carry the triggering box id yet.",
                "MoonBlock identity is `EntityType.Box + BoxArchetype.Moon`.",
                "Do not add `EntityType." + "MoonBlock`.",
                "Do not add `BoxCapabilities." + "Moon`.",
                "`MoonBlockOnly` selector is identity-based and must not re-check Push capability.",
                "`HasMoonBlockSource` is retained as current/future naming.",
                "`MoonBlockGenerated` event emits only on actual respawn success.",
                "Event source is MoonBlockGenerator respawn processor success fact.",
                "`MoonBlockGeneratorBlocked` is debounced presentation-only feedback for generator defer cases.",
                "`MoonBlockGeneratorBlocked` does not alter respawn gameplay policy.",
                "The event kind remains `MoonBlockGeneratorBlocked`; reason-specific event kinds are not introduced.",
                "`MoonBlockGeneratorBlocked` carries a presentation-only `MoonBlockGeneratorBlockedPayload`.",
                "Payload fields are `Reason`, `BlockingEntityId`, and `BlockedCell`.",
                "Reason values are `UnitOccupant`, `WallLikeSolid`, and `PlacementBlocked`.",
                "Reason-specific visual and audio feedback is payload-driven and falls back to generic blocked feedback",
                "`TargetEntityId` is the respawned MoonBlock entity id.",
                "Final snapshot diffing must not create the event.",
                "MoonBlockGenerator activation rule is `BottomFaceOnly`.",
                "Generator-bound initial MoonBlock spawn is the stable id/template source.",
                "Unit/player/enemy at the generator cell causes defer; no kill or eject occurs.",
                "Projectile is not a blocker and is not destroyed.",
                "Normal/non-Moon Box at the generator cell is detached/marked destroy before MoonBlock spawn.",
                "`TickPipeline` does not execute visual/audio/UI.",
                "DestroyTile v1 targets Box and Unit through movement-derived `TileEffectEntityContact`, not final snapshot scanning.",
                "Stationary boxes and stationary units are not destroyed.",
                "Player-authored ordinary Move into an active DestroyTile is rejected during movement expansion using the topology-resolved destination cell.",
                "DestroyTile is not a global traversal blocker; do not model it as runtime traversal, placement, or settlement blockage.",
                "The player DestroyTile access guard applies only to voluntary player movement and does not apply to enemy, box, projectile, push/flip, impact follow-through, jump/respawn, or scripted relocation paths.",
                "Free2D same-face voluntary player movement injects a scoped active DestroyTile predicate into the existing CollisionRadius-based continuous locomotion blocker/clamp structure.",
                "Free2D same-face DestroyTile access must not use DestroyTile-specific approach helpers or manual local-offset clamps.",
                "Free2D native topology transition checks `transition.TargetAnchor` under `transition.UpdatedTopology` before topology or anchor materialization.",
                "Air units may have DestroyTile hazard lethal exceptions, but player voluntary access guards still block active DestroyTile destination or Free2D scoped blocker entry.",
                "Unit targets are destroyed only when non-blocked Unit locomotion, locomotion anchor commit, or jump landing moves them into an active DestroyTile after movement and before attack collection.",
                "Player and Enemy are both Unit targets; Player death presentation/audio is transported through `TickResult` presentation facts, not direct gameplay UI/audio calls.",
                "Phase relocation, spawn/respawn, topology relocation, and projectile movement are not DestroyTile contact sources in v1.",
                "`DestroyTileTriggered` event `TargetEntityId` is the destroyed entity id.",
                "SlideTile handles only `PushEnter` and `SlideEnter` contact kinds.",
                "`FlipLanding` and `ImpactFollowThrough` are excluded from the MVP.",
                "SlideTile does not perform same-tick extra movement.",
                "For sliding boxes, `EntityState.facing` is the authoritative continuation direction. SlideTile redirect changes facing, not position.",
                "DestroyTile wins: a destroyed box is not Slide redirected.",
                "`SlideTileRedirected` event carries a `Direction` payload.",
                "Current `TilePresentationEvent` source matrix:",
                "Coordinator request cache is replaced every tick.",
                "Dedupe belongs to event generation.",
                "Visual consumers read only `CurrentTilePresentationRequests`.",
                "TileFeatureAudio is separate from core GameplayAudio, GameplayActionAudio, and UI audio lanes.",
                "Only Sfx one-shot playback is allowed.",
                "`StagePresentationDefinition` has no TileFeature audio binding.",
                "Barricade is a TileFeature movement blocker.",
                "Barricade remains a TileFeature overlay, not occupancy, terrain, or an entity type.",
                "Active Barricade blocks Unit ground traversal, including player, enemy, and future NPC/friendly units.",
                "Barricade does not occupy Unit, Solid, or Projectile layer.",
                "Barricade does not invalidate existing Unit occupancy.",
                "EnemyParticipationPolicy is unchanged; current enemy bottom-face participation remains unchanged.",
                "`BarricadeBlocked` is sourced from movement blocker facts",
                "`BarricadeCrushed` is sourced from `TileFeatureEffectResolver.ResolveBarricadeCrushes`",
                "Exit open is derived from required non-PrimaryGoal conditions complete plus active Exit.",
                "Exit open is not mutable TileFeature state and must not reuse `TileFeatureFlags.Activated`.",
                "Same-tick open and enter emits both events, ordered `ExitOpened` before `ExitEntered`.",
                "Live MoonBlock no-op and inactive generator do not emit `MoonBlockGeneratorBlocked`.",
            };

            for (var i = 0; i < requiredSnippets.Length; i++)
            {
                Assert.That(document, Does.Contain(requiredSnippets[i]));
            }

            Assert.That(document, Does.Not.Contain("Barricade box-only blocker MVP is implemented."));
            Assert.That(document, Does.Not.Contain("Barricade is a box-only movement blocker."));
            Assert.That(document, Does.Not.Contain("Unit/player/enemy traversal is not blocked."));
        }

        [Test]
        [Category("Core")]
        public void TickPipeline_DoesNotOwnBarricadeTraversalPolicy()
        {
            var source = File.ReadAllText(GetAbsolutePath("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs"));

            Assert.That(source, Does.Not.Contain("TileFeatureMovementBlockerQuery"));
            Assert.That(source, Does.Not.Contain("TryGetActiveBarricadeBlocker"));
            Assert.That(source, Does.Not.Contain("HasActiveBarricadeBlocker"));
        }

        [Test]
        [Category("Core")]
        public void EntityType_DoesNotContainTileFeatureKinds()
        {
            Assert.That(Enum.GetNames(typeof(EntityType)), Does.Not.Contain("TileFeature"));
            Assert.That(Enum.GetNames(typeof(EntityType)), Does.Not.Contain("DestroyTile"));
            Assert.That(Enum.GetNames(typeof(EntityType)), Does.Not.Contain("MoonBlock"));
            Assert.That(Enum.GetNames(typeof(EntityType)), Does.Not.Contain("SlideTile"));
            Assert.That(Enum.GetNames(typeof(EntityType)), Does.Not.Contain("Barricade"));
            Assert.That(Enum.GetNames(typeof(EntityType)), Does.Not.Contain("Exit"));
            Assert.That(Enum.GetNames(typeof(EntityType)), Does.Not.Contain("GravityField"));
        }

        [Test]
        [Category("Core")]
        public void BoxCapabilities_DoNotContainMoonIdentity()
        {
            Assert.That(Enum.GetNames(typeof(BoxCapabilities)), Does.Not.Contain("Moon"));
            Assert.That(Enum.GetNames(typeof(BoxCapabilities)), Does.Not.Contain("MoonBlock"));
            Assert.That(Enum.GetNames(typeof(BoxCapabilities)), Does.Not.Contain("GravityField"));
        }

        [Test]
        [Category("Core")]
        public void TileFeatureKind_DoesNotContainGravityField()
        {
            Assert.That(Enum.GetNames(typeof(TileFeatureKind)), Does.Not.Contain("GravityField"));
        }

        [Test]
        [Category("Core")]
        public void TerrainFlags_DoNotContainTileFeatureOrEffectSemantics()
        {
            var flagNames = Enum.GetNames(typeof(TerrainFlags));
            for (var i = 0; i < flagNames.Length; i++)
            {
                var flagName = flagNames[i];
                for (var tokenIndex = 0; tokenIndex < ForbiddenTerrainFlagTokens.Length; tokenIndex++)
                {
                    Assert.That(
                        flagName,
                        Does.Not.Contain(ForbiddenTerrainFlagTokens[tokenIndex]),
                        $"TerrainFlags value '{flagName}' must not encode TileFeature or effect semantics.");
                }

                if (flagName == nameof(TerrainFlags.None))
                {
                    continue;
                }

                Assert.That(
                    flagName.StartsWith("Blocks", StringComparison.Ordinal),
                    Is.True,
                    $"New TerrainFlags value '{flagName}' is not obviously blocker terrain vocabulary. Update ADR-004/ADR-006 and this test before adding non-blocker terrain semantics.");
            }
        }

        [Test]
        [Category("Core")]
        public void TickPresentationData_ExposesPresentationOwnedTileEvents()
        {
            var property = typeof(TickPresentationData).GetProperty("TileEvents");

            Assert.That(property, Is.Not.Null);
            Assert.That(property.PropertyType, Is.EqualTo(typeof(IReadOnlyList<TilePresentationEvent>)));
            Assert.That(property.SetMethod, Is.Null);
        }

        [Test]
        [Category("Core")]
        public void WorldState_DoesNotExposePublicTileFeatureMutationApis()
        {
            var source = File.ReadAllText(GetAbsolutePath(WorldStatePath));
            var forbiddenSignatures = new[]
            {
                "public void AddTileFeature",
                "public void UpdateTileFeature",
                "public void RemoveTileFeature",
            };

            for (var i = 0; i < forbiddenSignatures.Length; i++)
            {
                Assert.That(
                    source,
                    Does.Not.Contain(forbiddenSignatures[i]),
                    $"WorldState must not expose public TileFeature mutation API '{forbiddenSignatures[i]}'.");
            }
        }

        [Test]
        [Category("Core")]
        public void TilePresentationRequestPlanner_DoesNotReferenceAuthorityOrMutationTypes()
        {
            var source = File.ReadAllText(GetAbsolutePath(TilePresentationRequestPlannerPath));
            var forbiddenTokens = new[]
            {
                "WorldState",
                "WorldSnapshot",
                "CreateSnapshot",
                "TickPipeline",
                "ProjectedWorld",
                "FinalizationBatch",
                "DeterminismHashBuilder",
            };

            for (var i = 0; i < forbiddenTokens.Length; i++)
            {
                Assert.That(
                    source,
                    Does.Not.Contain(forbiddenTokens[i]),
                    $"Tile presentation request planner must not reference authority or mutation token '{forbiddenTokens[i]}'.");
            }
        }

        [Test]
        [Category("Core")]
        public void TilePresentationCoordinatorRefresh_DoesNotReferenceAuthorityOrPlaybackTypes()
        {
            var source = File.ReadAllText(GetAbsolutePath(GameplayTickPresentationCoordinatorPath));
            var body = ExtractMethodBody(source, "private void RefreshTilePresentationRequests(");
            var forbiddenTokens = new[]
            {
                "WorldState",
                "WorldSnapshot",
                "CreateSnapshot",
                "ProjectedWorld",
                "FinalizationBatch",
                "DeterminismHashBuilder",
                "PlayPlannedAudio",
                "PlayBlockBursts",
                "PlayHitEffect",
                "GameplayAudio",
                "GameplayVfx",
                "MonoBehaviour",
                "GameObject",
                "Prefab",
            };

            for (var i = 0; i < forbiddenTokens.Length; i++)
            {
                Assert.That(
                    body,
                    Does.Not.Contain(forbiddenTokens[i]),
                    $"Tile presentation request coordinator refresh must not reference authority, playback, or mutation token '{forbiddenTokens[i]}'.");
            }
        }

        [Test]
        [Category("Core")]
        public void TickPipeline_DoesNotPlanTilePresentationRequests()
        {
            var absoluteDirectory = GetAbsolutePath(GameplayLoopRuntimePath);
            var pipelineFiles = Directory.GetFiles(absoluteDirectory, "TickPipeline*.cs", SearchOption.TopDirectoryOnly);
            Assert.That(pipelineFiles, Is.Not.Empty);

            for (var i = 0; i < pipelineFiles.Length; i++)
            {
                var source = File.ReadAllText(pipelineFiles[i]);
                Assert.That(source, Does.Not.Contain("TilePresentationRequestPlanner"), pipelineFiles[i]);
                Assert.That(source, Does.Not.Contain("TilePresentationRequest"), pipelineFiles[i]);
                Assert.That(source, Does.Not.Contain("TileFeatureVisual"), pipelineFiles[i]);
                Assert.That(source, Does.Not.Contain("TileFeatureAudio"), pipelineFiles[i]);
                Assert.That(source, Does.Not.Contain("GravityFieldPresentationRequest"), pipelineFiles[i]);
                Assert.That(source, Does.Not.Contain("GravityFieldAudio"), pipelineFiles[i]);
                Assert.That(source, Does.Not.Contain("GravityFieldVisual"), pipelineFiles[i]);
                Assert.That(source, Does.Not.Contain("GameplayAudio"), pipelineFiles[i]);
                Assert.That(source, Does.Not.Contain("Play2D"), pipelineFiles[i]);
                Assert.That(source, Does.Not.Contain("PlayAttached"), pipelineFiles[i]);
                Assert.That(source, Does.Not.Contain("Instantiate"), pipelineFiles[i]);
                Assert.That(source, Does.Not.Contain("UnityEvent"), pipelineFiles[i]);
                Assert.That(source, Does.Not.Contain("Animator"), pipelineFiles[i]);
                Assert.That(source, Does.Not.Contain("ParticleSystem"), pipelineFiles[i]);
                Assert.That(source, Does.Not.Contain("GameObject"), pipelineFiles[i]);
                Assert.That(source, Does.Not.Contain("Prefab"), pipelineFiles[i]);
            }
        }

        [Test]
        [Category("Core")]
        public void MoonBlockGeneratorPresentationAudioSurface_OpensGeneratedAndBlocked()
        {
            Assert.That(Enum.GetNames(typeof(TilePresentationEventKind)), Does.Contain("MoonBlockGenerated"));
            Assert.That(Enum.GetNames(typeof(TilePresentationEventKind)), Does.Contain("MoonBlockGeneratorBlocked"));
            Assert.That(Enum.GetNames(typeof(TilePresentationEventKind)), Has.None.Contains("MoonBlockGeneratorUnitBlocked"));
            Assert.That(Enum.GetNames(typeof(TilePresentationEventKind)), Has.None.Contains("MoonBlockGeneratorWallLikeSolidBlocked"));
            Assert.That(Enum.GetNames(typeof(TilePresentationEventKind)), Has.None.Contains("MoonBlockGeneratorPlacementBlocked"));
            Assert.That(Enum.GetNames(typeof(TilePresentationRequestKind)), Does.Contain("MoonBlockGenerated"));
            Assert.That(Enum.GetNames(typeof(TilePresentationRequestKind)), Does.Contain("MoonBlockGeneratorBlocked"));
            Assert.That(Enum.GetNames(typeof(TilePresentationRequestKind)), Has.None.Contains("MoonBlockGeneratorUnitBlocked"));
            Assert.That(Enum.GetNames(typeof(TilePresentationRequestKind)), Has.None.Contains("MoonBlockGeneratorWallLikeSolidBlocked"));
            Assert.That(Enum.GetNames(typeof(TilePresentationRequestKind)), Has.None.Contains("MoonBlockGeneratorPlacementBlocked"));

            var audioTypesSource = File.ReadAllText(GetAbsolutePath(TileFeatureAudioTypesPath));
            Assert.That(audioTypesSource, Does.Contain("MoonBlockGenerated"));
            Assert.That(audioTypesSource, Does.Contain("MoonBlockGeneratorBlocked"));
            Assert.That(audioTypesSource, Does.Not.Contain("MoonBlockGeneratorBlockedUnitOccupant"));
            Assert.That(audioTypesSource, Does.Not.Contain("MoonBlockGeneratorBlockedWallLikeSolid"));
            Assert.That(audioTypesSource, Does.Not.Contain("MoonBlockGeneratorBlockedPlacementBlocked"));

            var visualRegistrySource = File.ReadAllText(GetAbsolutePath(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/ITileFeatureVisualRegistry.cs"));
            Assert.That(visualRegistrySource, Does.Contain("IMoonBlockGeneratedVisualTarget"));
            Assert.That(visualRegistrySource, Does.Contain("IMoonBlockGeneratorBlockedVisualTarget"));

            var requestPlannerSource = File.ReadAllText(GetAbsolutePath(TilePresentationRequestPlannerPath));
            Assert.That(requestPlannerSource, Does.Contain("MoonBlockGenerated"));
            Assert.That(requestPlannerSource, Does.Contain("MoonBlockGeneratorBlocked"));
        }

        [Test]
        [Category("Core")]
        public void MoonBlockGeneratorBlockedPayload_DoesNotEnterAuthoritativeStateOrStageBuildSurfaces()
        {
            var forbiddenTokens = new[]
            {
                "MoonBlockGeneratorBlockedPayload",
                "MoonBlockGeneratorBlockedReason",
            };
            var sourcePaths = new List<string>
            {
                WorldStatePath,
                StageDefinitionPath,
                StageRuntimeBuildResultPath,
            };
            sourcePaths.AddRange(Directory.GetFiles(
                GetAbsolutePath(GameplayBoardStateRuntimePath),
                "*.cs",
                SearchOption.AllDirectories));

            for (var sourceIndex = 0; sourceIndex < sourcePaths.Count; sourceIndex++)
            {
                var sourcePath = GetAbsolutePath(sourcePaths[sourceIndex]);
                if (!File.Exists(sourcePath))
                {
                    sourcePath = sourcePaths[sourceIndex];
                }

                var source = File.ReadAllText(sourcePath);
                for (var tokenIndex = 0; tokenIndex < forbiddenTokens.Length; tokenIndex++)
                {
                    Assert.That(
                        source,
                        Does.Not.Contain(forbiddenTokens[tokenIndex]),
                        $"{forbiddenTokens[tokenIndex]} must not enter {sourcePath}.");
                }
            }
        }

        [Test]
        [Category("Core")]
        public void GravityFieldPresentationSurface_RemainsSeparateFromTileFeatureLane()
        {
            Assert.That(Enum.GetNames(typeof(TilePresentationEventKind)), Does.Not.Contain("GravityFieldActivated"));
            Assert.That(Enum.GetNames(typeof(TilePresentationEventKind)), Does.Not.Contain("GravityFieldExpired"));
            Assert.That(Enum.GetNames(typeof(TilePresentationRequestKind)), Does.Not.Contain("GravityFieldActivated"));
            Assert.That(Enum.GetNames(typeof(TilePresentationRequestKind)), Does.Not.Contain("GravityFieldExpired"));

            var tileFeatureAudioSource = File.ReadAllText(GetAbsolutePath(TileFeatureAudioTypesPath));
            Assert.That(tileFeatureAudioSource, Does.Not.Contain("GravityField"));

            var visualRegistrySource = File.ReadAllText(GetAbsolutePath(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/ITileFeatureVisualRegistry.cs"));
            Assert.That(visualRegistrySource, Does.Not.Contain("GravityField"));
        }

        [Test]
        [Category("Core")]
        public void GravityFieldPresentationRequestPlanner_DoesNotReferenceAuthorityOrMutationTypes()
        {
            var source = File.ReadAllText(GetAbsolutePath(GravityFieldPresentationRequestPlannerPath));
            var forbiddenTokens = new[]
            {
                "WorldState",
                "WorldSnapshot",
                "CreateSnapshot",
                "TickPipeline",
                "ProjectedWorld",
                "FinalizationBatch",
                "DeterminismHashBuilder",
                "TilePresentationRequest",
                "TileEvents",
            };

            for (var i = 0; i < forbiddenTokens.Length; i++)
            {
                Assert.That(
                    source,
                    Does.Not.Contain(forbiddenTokens[i]),
                    $"GravityField presentation request planner must not reference authority or TileFeature token '{forbiddenTokens[i]}'.");
            }
        }

        [Test]
        [Category("Core")]
        public void GravityFieldVisualConsumer_DoesNotReferenceAuthorityTileFeatureOrPlaybackSurfaces()
        {
            var source = File.ReadAllText(GetAbsolutePath(GravityFieldVisualControllerPath));
            var forbiddenTokens = new[]
            {
                "WorldState",
                "WorldSnapshot",
                "CreateSnapshot",
                "ProjectedWorld",
                "FinalizationBatch",
                "TickPipeline",
                "DeterminismHashBuilder",
                "TileFeatureVisualRegistry",
                "ITileFeatureVisualRegistry",
                "StagePresentationDefinition",
                "AudioMap",
            };

            for (var i = 0; i < forbiddenTokens.Length; i++)
            {
                Assert.That(
                    source,
                    Does.Not.Contain(forbiddenTokens[i]),
                    $"GravityField visual consumer must not reference '{forbiddenTokens[i]}'.");
            }
        }

        [Test]
        [Category("Core")]
        public void GravityFieldAudio_DoesNotOpenTileFeatureSpatialOrStageBindingSurface()
        {
            var root = GetAbsolutePath(GravityFieldAudioRuntimePath);
            Assert.That(Directory.Exists(root), Is.True);

            var forbiddenTokens = new[]
            {
                "TileFeatureAudio",
                "TileFeatureAudioCue",
                "Play3D",
                "Spatial",
                "spatial",
                "StagePresentationDefinition",
                "TileFeaturePresentationBinding",
                "VisualPrefab",
            };
            var sources = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            for (var sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
            {
                var source = File.ReadAllText(sources[sourceIndex]);
                for (var tokenIndex = 0; tokenIndex < forbiddenTokens.Length; tokenIndex++)
                {
                    Assert.That(
                        source,
                        Does.Not.Contain(forbiddenTokens[tokenIndex]),
                        $"{sources[sourceIndex]} must not open GravityFieldAudio token '{forbiddenTokens[tokenIndex]}'.");
                }
            }
        }

        [Test]
        [Category("Core")]
        public void GravityFieldLockedBox_SurfaceOpensOnlyGravityFieldLane()
        {
            Assert.That(Enum.GetNames(typeof(TilePresentationEventKind)), Does.Not.Contain("LockedBox"));
            Assert.That(Enum.GetNames(typeof(TilePresentationRequestKind)), Does.Not.Contain("LockedBox"));
            Assert.That(File.ReadAllText(GetAbsolutePath(TileFeatureAudioTypesPath)), Does.Not.Contain("LockedBox"));

            var stageDefinitionSource = File.ReadAllText(GetAbsolutePath(StageDefinitionPath));
            var buildResultSource = File.ReadAllText(GetAbsolutePath(StageRuntimeBuildResultPath));
            Assert.That(stageDefinitionSource, Does.Not.Contain("GravityFieldLockedBoxPayload"));
            Assert.That(buildResultSource, Does.Not.Contain("GravityFieldLockedBoxPayload"));

            var boardStateSources = Directory.GetFiles(GetAbsolutePath(GameplayBoardStateRuntimePath), "*.cs", SearchOption.AllDirectories);
            for (var i = 0; i < boardStateSources.Length; i++)
            {
                var source = File.ReadAllText(boardStateSources[i]);
                Assert.That(source, Does.Not.Contain("GravityFieldLockedBoxPayload"), boardStateSources[i]);
            }

            var gravityFieldConsumerPaths = new[]
            {
                GravityFieldVisualControllerPath,
                GravityFieldAudioControllerPath,
                "Assets/_Features/Gameplay/Gameplay_GravityFieldAudio/Runtime/GravityFieldAudioTypes.cs",
                "Assets/_Features/Gameplay/Gameplay_GravityFieldAudio/Runtime/GravityFieldAudioRequestPlanner.cs",
            };
            var forbiddenConsumerTokens = new[]
            {
                "WorldState.CreateSnapshot",
                "Play3D",
                "Spatial",
                "spatial",
                "StagePresentationDefinition",
                "TileFeatureAudioCue",
            };
            for (var i = 0; i < gravityFieldConsumerPaths.Length; i++)
            {
                var source = File.ReadAllText(GetAbsolutePath(gravityFieldConsumerPaths[i]));
                for (var tokenIndex = 0; tokenIndex < forbiddenConsumerTokens.Length; tokenIndex++)
                {
                    Assert.That(source, Does.Not.Contain(forbiddenConsumerTokens[tokenIndex]), gravityFieldConsumerPaths[i]);
                }
            }

            var uiRoot = GetAbsolutePath(UiRuntimePath);
            var uiSources = Directory.GetFiles(uiRoot, "*.cs", SearchOption.AllDirectories);
            for (var i = 0; i < uiSources.Length; i++)
            {
                var source = File.ReadAllText(uiSources[i]);
                Assert.That(
                    source,
                    Does.Not.Contain("LockedBox"),
                    $"{uiSources[i]} must not open a LockedBox UI/HUD notification.");
            }
        }

        [Test]
        [Category("Core")]
        public void TileFeatureVisualConsumerPath_DoesNotReferenceAuthorityOrPlaybackSurfaces()
        {
            var forbiddenTokens = new[]
            {
                "WorldState",
                "WorldSnapshot",
                "CreateSnapshot",
                "ProjectedWorld",
                "FinalizationBatch",
                "TickPipeline",
                "DeterminismHashBuilder",
                "TileEffectResolver",
                "TickPresentationData",
                "TileEvents",
                "GameplayAudio",
                "AudioMap",
                "ObjectiveStatusScreen",
                "Hud",
            };

            for (var pathIndex = 0; pathIndex < TileFeatureVisualRuntimePaths.Length; pathIndex++)
            {
                var source = File.ReadAllText(GetAbsolutePath(TileFeatureVisualRuntimePaths[pathIndex]));
                for (var tokenIndex = 0; tokenIndex < forbiddenTokens.Length; tokenIndex++)
                {
                    Assert.That(
                        source,
                        Does.Not.Contain(forbiddenTokens[tokenIndex]),
                        $"{TileFeatureVisualRuntimePaths[pathIndex]} must not reference '{forbiddenTokens[tokenIndex]}'.");
                }
            }
        }

        [Test]
        [Category("Core")]
        public void TileFeatureVisualTarget_DoesNotExposeGameplayMutationMethods()
        {
            var source = File.ReadAllText(GetAbsolutePath(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/TileFeatureVisualTargetView.cs"));
            var forbiddenTokens = new[]
            {
                "AddTileFeature",
                "UpdateTileFeature",
                "RemoveTileFeature",
                "ApplyTo",
                "IWorldWriteContext",
                "IWorldStateMutationPort",
            };

            for (var i = 0; i < forbiddenTokens.Length; i++)
            {
                Assert.That(
                    source,
                    Does.Not.Contain(forbiddenTokens[i]),
                    $"Tile feature visual target must not expose gameplay mutation token '{forbiddenTokens[i]}'.");
            }
        }

        [Test]
        [Category("Core")]
        public void TileFeatureVisualBinding_RemainsPresentationOwned()
        {
            var stageDefinitionSource = File.ReadAllText(GetAbsolutePath(StageDefinitionPath));
            var buildResultSource = File.ReadAllText(GetAbsolutePath(StageRuntimeBuildResultPath));
            var stagePresentationDefinitionSource = File.ReadAllText(GetAbsolutePath(StagePresentationDefinitionPath));

            Assert.That(stageDefinitionSource, Does.Not.Contain("TileFeaturePresentationBinding"));
            Assert.That(stageDefinitionSource, Does.Not.Contain("VisualPrefab"));
            Assert.That(stageDefinitionSource, Does.Not.Contain("GameObject"));
            Assert.That(buildResultSource, Does.Not.Contain("TileFeaturePresentationBinding"));
            Assert.That(buildResultSource, Does.Not.Contain("TileFeaturePresentationResolvedBinding"));
            Assert.That(buildResultSource, Does.Not.Contain("VisualPrefab"));
            Assert.That(buildResultSource, Does.Not.Contain("GameObject"));
            Assert.That(buildResultSource, Does.Not.Contain("SurfaceCellPresentationPose"));
            Assert.That(stageDefinitionSource, Does.Not.Contain("SurfaceCellPresentationPose"));
            Assert.That(stagePresentationDefinitionSource, Does.Contain("TileFeaturePresentationBinding"));
            Assert.That(stagePresentationDefinitionSource, Does.Contain("VisualPrefab"));
            Assert.That(stagePresentationDefinitionSource, Does.Not.Contain("SurfaceCellPresentationPose"));
            Assert.That(stagePresentationDefinitionSource, Does.Not.Contain("TileFeatureAudio"));
        }

        [Test]
        [Category("Core")]
        public void TileFeatureAudio_RemainsSeparateFromUiCoreAndActionAudioLanes()
        {
            for (var rootIndex = 0; rootIndex < TileFeatureAudioForbiddenConsumerPaths.Length; rootIndex++)
            {
                var root = GetAbsolutePath(TileFeatureAudioForbiddenConsumerPaths[rootIndex]);
                if (!Directory.Exists(root))
                {
                    continue;
                }

                var sources = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                for (var sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
                {
                    var source = File.ReadAllText(sources[sourceIndex]);
                    Assert.That(
                        source,
                        Does.Not.Contain("TileFeatureAudio"),
                        $"{sources[sourceIndex]} must not reference TileFeatureAudio.");
                }
            }
        }

        [Test]
        [Category("Core")]
        public void Ui_DoesNotInferTileFeatureStateOrUsePresentationFactsAsObjectiveEvidence()
        {
            var root = GetAbsolutePath(UiRuntimePath);
            Assert.That(Directory.Exists(root), Is.True);

            var forbiddenTokens = new[]
            {
                "WorldState.CreateSnapshot",
                "TilePresentationEvent",
                "TilePresentationRequest",
                "TileFeatureAudio",
                "TileFeatureFlags.Activated",
            };
            var sources = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            for (var sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
            {
                var source = File.ReadAllText(sources[sourceIndex]);
                for (var tokenIndex = 0; tokenIndex < forbiddenTokens.Length; tokenIndex++)
                {
                    Assert.That(
                        source,
                        Does.Not.Contain(forbiddenTokens[tokenIndex]),
                        $"{sources[sourceIndex]} must not use '{forbiddenTokens[tokenIndex]}' as UI TileFeature authority.");
                }
            }
        }

        [Test]
        [Category("Core")]
        public void TileFeaturePresentationConsumers_DoNotInferStateThroughWorldSnapshots()
        {
            var roots = new[]
            {
                GameplayHostRuntimePath,
                TileFeatureAudioRuntimePath,
            };

            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                var root = GetAbsolutePath(roots[rootIndex]);
                Assert.That(Directory.Exists(root), Is.True, root);
                var sources = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                for (var sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
                {
                    var source = File.ReadAllText(sources[sourceIndex]);
                    Assert.That(
                        source,
                        Does.Not.Contain("WorldState.CreateSnapshot"),
                        $"{sources[sourceIndex]} must not infer TileFeature state through WorldState.CreateSnapshot.");
                }
            }
        }

        [Test]
        [Category("Core")]
        public void TileFeatureAudio_DoesNotOpenSpatialOrStagePresentationBindingSurface()
        {
            var root = GetAbsolutePath(TileFeatureAudioRuntimePath);
            Assert.That(Directory.Exists(root), Is.True);

            var forbiddenTokens = new[]
            {
                "Play3D",
                "Spatial",
                "spatial",
                "StagePresentationDefinition",
                "TileFeaturePresentationBinding",
                "VisualPrefab",
            };
            var sources = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            for (var sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
            {
                var source = File.ReadAllText(sources[sourceIndex]);
                for (var tokenIndex = 0; tokenIndex < forbiddenTokens.Length; tokenIndex++)
                {
                    Assert.That(
                        source,
                        Does.Not.Contain(forbiddenTokens[tokenIndex]),
                        $"{sources[sourceIndex]} must not open TileFeatureAudio token '{forbiddenTokens[tokenIndex]}'.");
                }
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayBoardState_DoesNotReferenceStagePresentationDefinition()
        {
            var sources = Directory.GetFiles(GetAbsolutePath(GameplayBoardStateRuntimePath), "*.cs", SearchOption.TopDirectoryOnly);
            Assert.That(sources, Is.Not.Empty);

            for (var i = 0; i < sources.Length; i++)
            {
                var source = File.ReadAllText(sources[i]);
                Assert.That(source, Does.Not.Contain("StagePresentationDefinition"), sources[i]);
                Assert.That(source, Does.Not.Contain("TileFeaturePresentationBinding"), sources[i]);
                Assert.That(source, Does.Not.Contain("VisualPrefab"), sources[i]);
                Assert.That(source, Does.Not.Contain("SurfaceCellPresentationPose"), sources[i]);
                Assert.That(source, Does.Not.Contain("GameplayCubeProjector"), sources[i]);
            }
        }

        [Test]
        [Category("Core")]
        public void BoardTileCatalog_DoesNotEnterGameplayLoopOrBoardState()
        {
            var boardTileCatalog = "BoardTile" + "PresentationCatalog";
            var boardTileStyleCatalog = "BoardTile" + "StyleCatalog";
            var boardTileStyle = "BoardTile" + "Style";
            var boardTilePaintOverride = "BoardTile" + "PaintOverride";
            var boardTileOverlayCatalog = "BoardTile" + "OverlayCatalog";
            var boardTileOverlayOverride = "BoardTile" + "OverlayOverride";
            var gameplayLoopSources = Directory.GetFiles(
                GetAbsolutePath(GameplayLoopRuntimePath),
                "*.cs",
                SearchOption.AllDirectories);
            var boardStateSources = Directory.GetFiles(
                GetAbsolutePath(GameplayBoardStateRuntimePath),
                "*.cs",
                SearchOption.AllDirectories);

            foreach (var sourcePath in gameplayLoopSources)
            {
                var source = File.ReadAllText(sourcePath);
                Assert.That(source, Does.Not.Contain(boardTileCatalog), sourcePath);
                Assert.That(source, Does.Not.Contain(boardTileStyleCatalog), sourcePath);
                Assert.That(source, Does.Not.Contain(boardTileStyle), sourcePath);
                Assert.That(source, Does.Not.Contain(boardTilePaintOverride), sourcePath);
                Assert.That(source, Does.Not.Contain(boardTileOverlayCatalog), sourcePath);
                Assert.That(source, Does.Not.Contain(boardTileOverlayOverride), sourcePath);
            }

            foreach (var sourcePath in boardStateSources)
            {
                var source = File.ReadAllText(sourcePath);
                Assert.That(source, Does.Not.Contain(boardTileCatalog), sourcePath);
                Assert.That(source, Does.Not.Contain(boardTileStyleCatalog), sourcePath);
                Assert.That(source, Does.Not.Contain(boardTileStyle), sourcePath);
                Assert.That(source, Does.Not.Contain(boardTilePaintOverride), sourcePath);
                Assert.That(source, Does.Not.Contain(boardTileOverlayCatalog), sourcePath);
                Assert.That(source, Does.Not.Contain(boardTileOverlayOverride), sourcePath);
            }
        }

        [Test]
        [Category("Core")]
        public void StageTileFeatureVisualInstantiation_DoesNotReferenceAuthorityOrMutationTypes()
        {
            var source = File.ReadAllText(GetAbsolutePath(GameplayHostRuntimeFactoryPath));
            var body = ExtractMethodBody(source, "private static void InstantiateStageTileFeatureVisuals(");
            var forbiddenTokens = new[]
            {
                "WorldState",
                "WorldSnapshot",
                "CreateSnapshot",
                "TickPipeline",
                "ProjectedWorld",
                "FinalizationBatch",
                "TileEffectResolver",
            };

            for (var i = 0; i < forbiddenTokens.Length; i++)
            {
                Assert.That(
                    body,
                    Does.Not.Contain(forbiddenTokens[i]),
                    $"Stage TileFeature visual instantiation must not reference authority or mutation token '{forbiddenTokens[i]}'.");
            }
        }

        [Test]
        [Category("Core")]
        public void SurfaceCellPresentationPoseResolver_DoesNotReferenceAuthorityOrPlaybackSurfaces()
        {
            var source = File.ReadAllText(GetAbsolutePath(SurfaceCellPresentationPoseResolverPath));
            var forbiddenTokens = new[]
            {
                "WorldState",
                "WorldSnapshot",
                "CreateSnapshot",
                "TickPipeline",
                "TileEffect",
                "StageRuntimeBuildResult",
                "StageDefinition",
                "BoardTile" + "PresentationCatalog",
                "Replace" + "BaseTile",
                "Play3D",
                "Spatial",
                "spatial",
            };

            for (var i = 0; i < forbiddenTokens.Length; i++)
            {
                Assert.That(
                    source,
                    Does.Not.Contain(forbiddenTokens[i]),
                    $"SurfaceCell presentation pose resolver must not reference '{forbiddenTokens[i]}'.");
            }
        }

        [Test]
        [Category("Core")]
        public void ReplaceBaseTile_DoesNotEnterGameplayDefinitionsOrRuntimeBuildResult()
        {
            var forbiddenTokens = new[]
            {
                "Replace" + "BaseTile",
                "TileFeature" + "Visual" + "PlacementMode",
                "TileFeature" + "Visual" + "FootprintMode",
                "Placement" + "Mode",
            };
            var sourcePaths = new[]
            {
                StageDefinitionPath,
                StageRuntimeBuildResultPath,
            };

            for (var sourceIndex = 0; sourceIndex < sourcePaths.Length; sourceIndex++)
            {
                var sourcePath = GetAbsolutePath(sourcePaths[sourceIndex]);
                var source = File.ReadAllText(sourcePath);
                for (var tokenIndex = 0; tokenIndex < forbiddenTokens.Length; tokenIndex++)
                {
                    Assert.That(source, Does.Not.Contain(forbiddenTokens[tokenIndex]), sourcePath);
                }
            }
        }

        [Test]
        [Category("Core")]
        public void ReplaceBaseTile_DoesNotEnterBoardStateOrGameplayLoop()
        {
            var forbiddenTokens = new[]
            {
                "Replace" + "BaseTile",
                "TileFeature" + "Visual" + "PlacementMode",
                "TileFeature" + "Visual" + "FootprintMode",
            };
            var sourcePaths = Directory
                .GetFiles(GetAbsolutePath(GameplayLoopRuntimePath), "*.cs", SearchOption.AllDirectories);
            var boardStateSourcePaths = Directory
                .GetFiles(GetAbsolutePath(GameplayBoardStateRuntimePath), "*.cs", SearchOption.AllDirectories);

            foreach (var sourcePath in sourcePaths)
            {
                var source = File.ReadAllText(sourcePath);
                for (var tokenIndex = 0; tokenIndex < forbiddenTokens.Length; tokenIndex++)
                {
                    Assert.That(source, Does.Not.Contain(forbiddenTokens[tokenIndex]), sourcePath);
                }
            }

            foreach (var sourcePath in boardStateSourcePaths)
            {
                var source = File.ReadAllText(sourcePath);
                for (var tokenIndex = 0; tokenIndex < forbiddenTokens.Length; tokenIndex++)
                {
                    Assert.That(source, Does.Not.Contain(forbiddenTokens[tokenIndex]), sourcePath);
                }
            }
        }

        [Test]
        [Category("Core")]
        public void TickPipeline_DoesNotReferenceReplaceBaseTile()
        {
            var source = File.ReadAllText(GetAbsolutePath("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs"));

            Assert.That(source, Does.Not.Contain("Replace" + "BaseTile"));
            Assert.That(source, Does.Not.Contain("TileFeature" + "Visual" + "PlacementMode"));
            Assert.That(source, Does.Not.Contain("TileFeature" + "Visual" + "FootprintMode"));
        }

        [Test]
        [Category("Core")]
        public void OccupancyLayer_DoesNotContainTileFeature_WhenLayerTypeExists()
        {
            var occupancyLayerType = typeof(EntityType).Assembly.GetType("Game.Feature.Gameplay.BoardState.OccupancyLayer");
            if (occupancyLayerType == null)
            {
                Assert.Pass("No OccupancyLayer enum exists in this phase.");
            }

            Assert.That(occupancyLayerType.IsEnum, Is.True);
            Assert.That(Enum.GetNames(occupancyLayerType), Does.Not.Contain("TileFeature"));
        }

        private static string GetAbsolutePath(string relativePath)
        {
            return Path.GetFullPath(relativePath);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.That(signatureIndex, Is.GreaterThanOrEqualTo(0), $"Missing method signature '{signature}'.");

            var bodyStart = source.IndexOf('{', signatureIndex);
            Assert.That(bodyStart, Is.GreaterThanOrEqualTo(0), $"Missing method body for '{signature}'.");

            var depth = 0;
            for (var i = bodyStart; i < source.Length; i++)
            {
                if (source[i] == '{')
                {
                    depth++;
                }
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return source.Substring(bodyStart, i - bodyStart + 1);
                    }
                }
            }

            Assert.Fail($"Could not extract method body for '{signature}'.");
            return string.Empty;
        }
    }
}
