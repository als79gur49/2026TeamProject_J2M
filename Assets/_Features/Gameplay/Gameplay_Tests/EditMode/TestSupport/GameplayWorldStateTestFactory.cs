using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests
{
    internal static class GameplayWorldStateTestFactory
    {
        private static readonly BoardBounds DefaultBoardBounds = new(
            new Vector2Int(-32, -32),
            new Vector2Int(32, 32));

        public static WorldState CreateBounded(IEnumerable<EntityState> initialEntities)
        {
            return CreateBounded(initialEntities, DefaultBoardBounds, GameplayTimingProfile.CreateDefault());
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            GameplayTimingProfile timingProfile)
        {
            return CreateBounded(initialEntities, DefaultBoardBounds, timingProfile);
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds)
        {
            return CreateBounded(
                initialEntities,
                boardBounds,
                new CubeTopologyState(FaceId.Floor),
                GameplayTimingProfile.CreateDefault());
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            GameplayTimingProfile timingProfile)
        {
            return CreateBounded(
                initialEntities,
                boardBounds,
                new CubeTopologyState(FaceId.Floor),
                timingProfile);
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            CubeTopologyState topology)
        {
            return CreateBounded(
                initialEntities,
                boardBounds,
                topology,
                GameplayTimingProfile.CreateDefault());
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            CubeTopologyState topology,
            GameplayTimingProfile timingProfile)
        {
            return CreateBounded(
                initialEntities,
                boardBounds,
                topology,
                timingProfile,
                initialTileFeatures: null);
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            CubeTopologyState topology,
            GameplayTimingProfile timingProfile,
            IEnumerable<TileFeatureState> initialTileFeatures)
        {
            var normalizedInitialEntities = SessionStartEntityNormalizer.Normalize(
                initialEntities,
                timingProfile ?? GameplayTimingProfile.CreateDefault());
            DebugSpawnValidityPolicy.EnsureRepresentable(
                boardBounds,
                topology,
                normalizedInitialEntities);

            return GameplayCompositionRoot.CreateWorldState(
                normalizedInitialEntities,
                boardBounds,
                topology,
                initialTileFeatures);
        }
    }

    internal static class StageAuthoredWallTestFactory
    {
        public static EntityState Create(int entityId, SurfaceCell cell, int hp = 1)
        {
            return RunInHost(
                entityId,
                cell,
                1,
                new SurfaceCell(FaceId.Floor, -32, -32),
                null,
                false,
                (_, wall) => wall,
                hp);
        }

        public static TResult RunInHost<TResult>(
            int entityId,
            SurfaceCell cell,
            int playerEntityId,
            SurfaceCell playerCell,
            EnemyAiProfile defaultEnemyAiProfile,
            bool enableEnemySameFaceContinuousLocomotion,
            Func<GameplaySceneHost, EntityState, TResult> scenario,
            int hp = 1)
        {
            if (scenario == null)
            {
                throw new ArgumentNullException(nameof(scenario));
            }

            var stage = ScriptableObject.CreateInstance<StageDefinition>();
            var entry = ScriptableObject.CreateInstance<StageContentEntry>();
            var presentation = ScriptableObject.CreateInstance<StagePresentationDefinition>();
            var audio = ScriptableObject.CreateInstance<StageAudioDefinition>();
            var catalog = ScriptableObject.CreateInstance<StageCatalog>();
            var provider = ScriptableObject.CreateInstance<ScriptableObjectStageCatalogProvider>();
            var sequence = ScriptableObject.CreateInstance<CampaignStageSequenceDefinition>();
            var installerObject = new GameObject("StageAuthoredWallTestInstaller");
            stage.name = "StageAuthoredWallTestFactory";

            try
            {
                SetPrivateField(
                    stage,
                    "board",
                    new StageBoardDefinition
                    {
                        MinInclusive = new Vector2Int(-32, -32),
                        MaxInclusive = new Vector2Int(32, 32),
                        InitialBottomFace = FaceId.Floor,
                    });
                SetPrivateField(
                    stage,
                    "playerSpawns",
                    new[]
                    {
                        new StageSpawnDefinition
                        {
                            EntityId = playerEntityId,
                            Kind = StageSpawnKind.Player,
                            Cell = playerCell,
                            Facing = Direction.Right,
                            Hp = 3,
                        },
                    });
                SetPrivateField(stage, "boxSpawns", Array.Empty<StageSpawnDefinition>());
                SetPrivateField(stage, "enemySpawns", Array.Empty<StageSpawnDefinition>());
                SetPrivateField(
                    stage,
                    "wallSpawns",
                    new[]
                    {
                        new StageSpawnDefinition
                        {
                            EntityId = entityId,
                            Kind = StageSpawnKind.Wall,
                            Cell = cell,
                            Facing = Direction.None,
                            Hp = hp,
                        },
                    });
                var stageId = StageId.CreateOrThrow("stage-authored-wall-test");
                entry.AssignStageId(stageId);
                entry.AssignGameplayDefinition(stage);
                entry.AssignPresentationDefinition(presentation);
                entry.AssignAudioDefinition(audio);
                presentation.SetOwnerMetadata(entry, "stage-authored-wall-test-entry");
                audio.SetOwnerMetadata(entry, "stage-authored-wall-test-entry");
                catalog.SetEntries(new[] { entry });
                provider.AssignCatalog(catalog);
                var sequenceEntry = new CampaignStageSequenceEntry();
                sequenceEntry.Set(stageId, "wall-test-group");
                sequence.SetEntries(new[] { sequenceEntry });

                var installer = installerObject.AddComponent<StageBackedGameplaySceneInstaller>();
                SetBasePrivateField(installer, "stageCatalogProvider", provider);
                SetBasePrivateField(installer, "campaignStageSequenceDefinition", sequence);
                StageLaunchContextStore.Clear();
                EditorDirectPlayContextStore.Clear();
                EditorDirectPlayContextStore.ClearTemporaryCampaignState();
                TerminalSessionRegistry.ResetForTests();
                SceneEntryPresentationRegistry.ResetForTests();
                StageLaunchContextStore.SetCurrent(stageId);

                var buildMethod = typeof(StageBackedGameplaySceneInstallerBase).GetMethod(
                    "BuildInitialGameplayState",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (buildMethod == null)
                {
                    throw new MissingMethodException(
                        typeof(StageBackedGameplaySceneInstallerBase).FullName,
                        "BuildInitialGameplayState");
                }

                var initialState = buildMethod.Invoke(installer, Array.Empty<object>());
                var entitiesProperty = initialState.GetType().GetProperty(
                    "InitialEntities",
                    BindingFlags.Instance | BindingFlags.Public);
                if (entitiesProperty == null ||
                    entitiesProperty.GetValue(initialState) is not EntityState[] entities)
                {
                    throw new InvalidOperationException("Stage-backed installer did not expose InitialEntities.");
                }

                var host = installerObject.AddComponent<GameplaySceneHost>();
                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = false,
                        DefaultEnemyAiProfile = defaultEnemyAiProfile,
                        EnableEnemySameFaceContinuousLocomotion = enableEnemySameFaceContinuousLocomotion,
                        InitialBoardBounds = new BoardBounds(
                            new Vector2Int(-32, -32),
                            new Vector2Int(32, 32)),
                        InitialEntities = entities,
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = playerEntityId,
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                    });
                var snapshot = host.WorldState.CreateSnapshot();
                if (snapshot.TryGetEntity(entityId, out var wall))
                {
                    return scenario(host, wall);
                }

                throw new InvalidOperationException($"Authored Wall {entityId} was not materialized.");
            }
            finally
            {
                StageLaunchContextStore.Clear();
                EditorDirectPlayContextStore.Clear();
                EditorDirectPlayContextStore.ClearTemporaryCampaignState();
                UnityEngine.Object.DestroyImmediate(installerObject);
                TerminalSessionRegistry.ResetForTests();
                SceneEntryPresentationRegistry.ResetForTests();
                UnityEngine.Object.DestroyImmediate(sequence);
                UnityEngine.Object.DestroyImmediate(provider);
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(audio);
                UnityEngine.Object.DestroyImmediate(presentation);
                UnityEngine.Object.DestroyImmediate(entry);
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        private static void SetPrivateField<TValue>(object target, string fieldName, TValue value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().FullName, fieldName);
            }

            field.SetValue(target, value);
        }

        private static void SetBasePrivateField<TValue>(
            StageBackedGameplaySceneInstaller installer,
            string fieldName,
            TValue value)
        {
            var field = typeof(StageBackedGameplaySceneInstallerBase).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(typeof(StageBackedGameplaySceneInstallerBase).FullName, fieldName);
            }

            field.SetValue(installer, value);
        }
    }

    internal enum WallIdentityDeltaVerdict
    {
        Equivalent = 0,
        ApprovedTypeOnlyDelta = 1,
        HoldReplay = 2,
    }

    internal readonly struct WallIdentityDeltaComparison
    {
        public WallIdentityDeltaComparison(WallIdentityDeltaVerdict verdict, int normalizedDeltaCount)
        {
            Verdict = verdict;
            NormalizedDeltaCount = normalizedDeltaCount;
        }

        public WallIdentityDeltaVerdict Verdict { get; }

        public int NormalizedDeltaCount { get; }
    }

    internal enum WallIdentityDeltaSource
    {
        PushRejected = 0,
        FlipRejected = 1,
        SlideStopper = 2,
        RespawnEventLog = 3,
        PendingReaction = 4,
        PendingEventLog = 5,
    }

    internal static class WallIdentityDeltaComparator
    {
        public static WallIdentityDeltaComparison Compare(
            WallIdentityDeltaSource source,
            string before,
            string after,
            int verifiedWallId)
        {
            var schema = GetSchema(source);
            if (string.IsNullOrEmpty(before) ||
                string.IsNullOrEmpty(after))
            {
                return HoldReplay();
            }

            var beforeTokens = Parse(before);
            var afterTokens = Parse(after);
            if (beforeTokens.Count != afterTokens.Count ||
                beforeTokens.Count < 3 ||
                !HasValidSchema(beforeTokens) ||
                !HasValidSchema(afterTokens) ||
                !MatchesExactSchema(beforeTokens, schema) ||
                !MatchesExactSchema(afterTokens, schema))
            {
                return HoldReplay();
            }

            var identityIndex = -1;
            var typeIndex = -1;
            for (var i = 0; i < beforeTokens.Count; i++)
            {
                if (!string.Equals(beforeTokens[i].Key, afterTokens[i].Key, StringComparison.Ordinal))
                {
                    return HoldReplay();
                }

                if (string.Equals(beforeTokens[i].Key, schema.IdentityKey, StringComparison.Ordinal))
                {
                    identityIndex = i;
                }

                if (string.Equals(beforeTokens[i].Key, schema.TypeKey, StringComparison.Ordinal))
                {
                    typeIndex = i;
                }
            }

            if (identityIndex < 0 || typeIndex < 0 ||
                !int.TryParse(beforeTokens[identityIndex].Value, out var beforeId) ||
                !int.TryParse(afterTokens[identityIndex].Value, out var afterId) ||
                beforeId != verifiedWallId ||
                afterId != verifiedWallId)
            {
                return HoldReplay();
            }

            for (var i = 0; i < beforeTokens.Count; i++)
            {
                if (i == typeIndex)
                {
                    continue;
                }

                if (!string.Equals(beforeTokens[i].Value, afterTokens[i].Value, StringComparison.Ordinal))
                {
                    return HoldReplay();
                }
            }

            var beforeType = beforeTokens[typeIndex].Value;
            var afterType = afterTokens[typeIndex].Value;
            if (string.Equals(beforeType, afterType, StringComparison.Ordinal))
            {
                return new WallIdentityDeltaComparison(WallIdentityDeltaVerdict.Equivalent, 0);
            }

            if ((string.Equals(beforeType, EntityType.None.ToString(), StringComparison.Ordinal) ||
                 string.Equals(beforeType, ((int)EntityType.None).ToString(), StringComparison.Ordinal)) &&
                (string.Equals(afterType, EntityType.Wall.ToString(), StringComparison.Ordinal) ||
                 string.Equals(afterType, ((int)EntityType.Wall).ToString(), StringComparison.Ordinal)))
            {
                return new WallIdentityDeltaComparison(
                    WallIdentityDeltaVerdict.ApprovedTypeOnlyDelta,
                    1);
            }

            return HoldReplay();
        }

        public static string GetIdentityKey(WallIdentityDeltaSource source)
        {
            return GetSchema(source).IdentityKey;
        }

        public static string GetTypeKey(WallIdentityDeltaSource source)
        {
            return GetSchema(source).TypeKey;
        }

        public static string GetLegacyTypeValue()
        {
            return EntityType.None.ToString();
        }

        public static string RewriteField(
            string record,
            string fieldKey,
            string replacementKey,
            string replacementValue)
        {
            var tokens = Parse(record);
            if (!HasValidSchema(tokens) ||
                string.IsNullOrEmpty(fieldKey) ||
                string.IsNullOrEmpty(replacementKey))
            {
                throw new InvalidOperationException("Cannot rewrite a malformed Wall identity record.");
            }

            var fieldIndex = -1;
            for (var i = 1; i < tokens.Count; i++)
            {
                if (!string.Equals(tokens[i].Key, fieldKey, StringComparison.Ordinal))
                {
                    continue;
                }

                if (fieldIndex >= 0)
                {
                    throw new InvalidOperationException($"Duplicate field '{fieldKey}' cannot be rewritten.");
                }

                fieldIndex = i;
            }

            if (fieldIndex < 0)
            {
                throw new InvalidOperationException($"Missing field '{fieldKey}'.");
            }

            tokens[fieldIndex] = new KeyValuePair<string, string>(replacementKey, replacementValue);
            return Serialize(tokens);
        }

        public static string SwapFirstTwoNonIdentityTypeFields(
            string record,
            string identityKey,
            string typeKey)
        {
            var tokens = Parse(record);
            if (!HasValidSchema(tokens))
            {
                throw new InvalidOperationException("Cannot reorder a malformed Wall identity record.");
            }

            var firstIndex = -1;
            var secondIndex = -1;
            for (var i = 1; i < tokens.Count; i++)
            {
                if (string.Equals(tokens[i].Key, identityKey, StringComparison.Ordinal) ||
                    string.Equals(tokens[i].Key, typeKey, StringComparison.Ordinal))
                {
                    continue;
                }

                if (firstIndex < 0)
                {
                    firstIndex = i;
                }
                else
                {
                    secondIndex = i;
                    break;
                }
            }

            if (firstIndex < 0 || secondIndex < 0)
            {
                throw new InvalidOperationException("Two non-identity/type fields are required for an ordering negative.");
            }

            (tokens[firstIndex], tokens[secondIndex]) = (tokens[secondIndex], tokens[firstIndex]);
            return Serialize(tokens);
        }

        private static List<KeyValuePair<string, string>> Parse(string record)
        {
            var rawTokens = record.Split('|');
            var tokens = new List<KeyValuePair<string, string>>(rawTokens.Length);
            for (var i = 0; i < rawTokens.Length; i++)
            {
                var separator = rawTokens[i].IndexOf('=');
                tokens.Add(separator < 0
                    ? new KeyValuePair<string, string>(string.Empty, rawTokens[i])
                    : new KeyValuePair<string, string>(
                        rawTokens[i].Substring(0, separator),
                        rawTokens[i].Substring(separator + 1)));
            }

            return tokens;
        }

        private static bool HasValidSchema(IReadOnlyList<KeyValuePair<string, string>> tokens)
        {
            if (!string.IsNullOrEmpty(tokens[0].Key) || string.IsNullOrEmpty(tokens[0].Value))
            {
                return false;
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 1; i < tokens.Count; i++)
            {
                if (string.IsNullOrEmpty(tokens[i].Key) || !keys.Add(tokens[i].Key))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MatchesExactSchema(
            IReadOnlyList<KeyValuePair<string, string>> tokens,
            WallIdentityDeltaSchema schema)
        {
            if (tokens.Count != schema.OrderedKeys.Length + 1 ||
                !string.Equals(tokens[0].Value, schema.Template, StringComparison.Ordinal))
            {
                return false;
            }

            for (var i = 0; i < schema.OrderedKeys.Length; i++)
            {
                if (!string.Equals(tokens[i + 1].Key, schema.OrderedKeys[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            if (string.IsNullOrEmpty(schema.RequiredValueKey))
            {
                return true;
            }

            for (var i = 1; i < tokens.Count; i++)
            {
                if (string.Equals(tokens[i].Key, schema.RequiredValueKey, StringComparison.Ordinal))
                {
                    return string.Equals(tokens[i].Value, schema.RequiredValue, StringComparison.Ordinal);
                }
            }

            return false;
        }

        private static WallIdentityDeltaSchema GetSchema(WallIdentityDeltaSource source)
        {
            switch (source)
            {
                case WallIdentityDeltaSource.PushRejected:
                    return new WallIdentityDeltaSchema(
                        "MovementRejected",
                        new[] { "Stage", "Source", "I", "Reason", "Cell", "Target", "Type" },
                        "Target",
                        "Type",
                        "Reason",
                        "PushTargetNotBox");
                case WallIdentityDeltaSource.FlipRejected:
                    return new WallIdentityDeltaSchema(
                        "MovementRejected",
                        new[] { "Stage", "Source", "I", "Reason", "Cell", "Target", "Type", "Capabilities" },
                        "Target",
                        "Type",
                        "Reason",
                        "FlipTargetNotFlippableBox");
                case WallIdentityDeltaSource.SlideStopper:
                    return new WallIdentityDeltaSchema(
                        "MovementRejected",
                        new[] { "Stage", "Source", "I", "Reason", "Target", "StopperKind", "Stopper", "StopperType", "Cell" },
                        "Stopper",
                        "StopperType",
                        "Reason",
                        "SlideStopperAdjacent");
                case WallIdentityDeltaSource.RespawnEventLog:
                    return new WallIdentityDeltaSchema(
                        "RespawnSkipped",
                        new[] { "E", "Pos", "Face", "Tick", "Reason", "BlockerEntity", "BlockerType" },
                        "BlockerEntity",
                        "BlockerType",
                        "Reason",
                        "Entity");
                case WallIdentityDeltaSource.PendingReaction:
                    return new WallIdentityDeltaSchema(
                        "PendingReaction",
                        new[]
                        {
                            "EnemyEntityId", "Kind", "ModeAtBlock", "SourceCell", "BlockedTargetCell",
                            "BlockedDirection", "BlockerKind", "BlockerSolidKind", "BlockerEntityType",
                            "BlockerEntityId", "CreatedTick", "ExpireTick",
                        },
                        "BlockerEntityId",
                        "BlockerEntityType",
                        null,
                        null);
                case WallIdentityDeltaSource.PendingEventLog:
                    return new WallIdentityDeltaSchema(
                        "PendingEnemyBlockedReactionSet",
                        new[]
                        {
                            "G", "I", "E", "Source", "BlockedTarget", "Direction", "BlockerKind", "SolidKind",
                            "BlockerEntityType", "BlockerEntityId", "Created", "Expire",
                        },
                        "BlockerEntityId",
                        "BlockerEntityType",
                        null,
                        null);
                default:
                    throw new ArgumentOutOfRangeException(nameof(source), source, null);
            }
        }

        private static string Serialize(IReadOnlyList<KeyValuePair<string, string>> tokens)
        {
            var rawTokens = new string[tokens.Count];
            rawTokens[0] = tokens[0].Value;
            for (var i = 1; i < tokens.Count; i++)
            {
                rawTokens[i] = $"{tokens[i].Key}={tokens[i].Value}";
            }

            return string.Join("|", rawTokens);
        }

        private static WallIdentityDeltaComparison HoldReplay()
        {
            return new WallIdentityDeltaComparison(WallIdentityDeltaVerdict.HoldReplay, 0);
        }

        private readonly struct WallIdentityDeltaSchema
        {
            public WallIdentityDeltaSchema(
                string template,
                string[] orderedKeys,
                string identityKey,
                string typeKey,
                string requiredValueKey,
                string requiredValue)
            {
                Template = template;
                OrderedKeys = orderedKeys;
                IdentityKey = identityKey;
                TypeKey = typeKey;
                RequiredValueKey = requiredValueKey;
                RequiredValue = requiredValue;
            }

            public string Template { get; }

            public string[] OrderedKeys { get; }

            public string IdentityKey { get; }

            public string TypeKey { get; }

            public string RequiredValueKey { get; }

            public string RequiredValue { get; }
        }
    }

    internal static class WallIdentityDeltaComparatorTestHarness
    {
        public static void AssertRuntimePositive(
            WallIdentityDeltaSource source,
            int verifiedWallId,
            Func<string> captureRuntimePositive)
        {
            var after = captureRuntimePositive();
            var identityKey = WallIdentityDeltaComparator.GetIdentityKey(source);
            var typeKey = WallIdentityDeltaComparator.GetTypeKey(source);
            var before = WallIdentityDeltaComparator.RewriteField(
                after,
                typeKey,
                typeKey,
                WallIdentityDeltaComparator.GetLegacyTypeValue());

            var positive = WallIdentityDeltaComparator.Compare(source, before, after, verifiedWallId);
            Assert.That(positive.Verdict, Is.EqualTo(WallIdentityDeltaVerdict.ApprovedTypeOnlyDelta));
            Assert.That(positive.NormalizedDeltaCount, Is.EqualTo(1));

            var unverifiedId = (verifiedWallId + 1).ToString();
            var wrongIdBefore = WallIdentityDeltaComparator.RewriteField(
                before, identityKey, identityKey, unverifiedId);
            var wrongIdAfter = WallIdentityDeltaComparator.RewriteField(
                after, identityKey, identityKey, unverifiedId);
            var wrongId = WallIdentityDeltaComparator.Compare(
                source, wrongIdBefore, wrongIdAfter, verifiedWallId);
            Assert.That(wrongId.Verdict, Is.EqualTo(WallIdentityDeltaVerdict.HoldReplay));
            Assert.That(wrongId.NormalizedDeltaCount, Is.Zero);

            var unexpectedTypeKey = $"Unexpected{typeKey}";
            var wrongKeyBefore = WallIdentityDeltaComparator.RewriteField(
                before, typeKey, unexpectedTypeKey, WallIdentityDeltaComparator.GetLegacyTypeValue());
            var wrongKeyAfter = WallIdentityDeltaComparator.RewriteField(
                after, typeKey, unexpectedTypeKey, EntityType.Wall.ToString());
            var wrongKey = WallIdentityDeltaComparator.Compare(
                source, wrongKeyBefore, wrongKeyAfter, verifiedWallId);
            Assert.That(wrongKey.Verdict, Is.EqualTo(WallIdentityDeltaVerdict.HoldReplay));
            Assert.That(wrongKey.NormalizedDeltaCount, Is.Zero);

            var orderMutation = WallIdentityDeltaComparator.SwapFirstTwoNonIdentityTypeFields(
                after,
                identityKey,
                typeKey);
            var payloadOrOrderMismatch = WallIdentityDeltaComparator.Compare(
                source, before, orderMutation, verifiedWallId);
            Assert.That(payloadOrOrderMismatch.Verdict, Is.EqualTo(WallIdentityDeltaVerdict.HoldReplay));
            Assert.That(payloadOrOrderMismatch.NormalizedDeltaCount, Is.Zero);

            TestContext.Out.WriteLine(
                $"WALL_IDENTITY_HISTOGRAM|Source={source}|IdentityKey={identityKey}|TypeKey={typeKey}|PreMatch=1|PostMatch=1|NormalizedDelta=1|WrongIdRejected=1|WrongKeyRejected=1|NonTypeMismatchRejected=1");
            TestContext.Out.WriteLine($"WALL_IDENTITY_RUNTIME_POSITIVE|Source={source}|Record={after}");
            TestContext.Out.WriteLine($"WALL_IDENTITY_SYNTHETIC_A|Source={source}|Before={wrongIdBefore}|After={wrongIdAfter}|Verdict={wrongId.Verdict}");
            TestContext.Out.WriteLine($"WALL_IDENTITY_SYNTHETIC_B|Source={source}|Before={wrongKeyBefore}|After={wrongKeyAfter}|Verdict={wrongKey.Verdict}");
            TestContext.Out.WriteLine($"WALL_IDENTITY_SYNTHETIC_C|Source={source}|Before={before}|After={orderMutation}|Verdict={payloadOrOrderMismatch.Verdict}");
        }
    }

}
