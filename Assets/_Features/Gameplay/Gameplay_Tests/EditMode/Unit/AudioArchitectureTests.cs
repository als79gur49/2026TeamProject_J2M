using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class AudioArchitectureTests
    {
        [Test]
        [Category("Extended")]
        public void AudioDefinition_OnValidate_AndResolve_ShareReservedMasterCategoryRule()
        {
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            var clip = AudioClip.Create("ReservedMaster", 4410, 1, 44100, false);
            try
            {
                SetSerializedField(typeof(AudioDefinition), definition, "category", AudioCategory.Master);
                SetSerializedField(typeof(SingleAudioDefinition), definition, "clip", clip);

                Assert.That(
                    AudioDefinitionCategoryRules.TryGetReservedCategoryMessage(
                        AudioCategory.Master,
                        $"AudioDefinition '{definition.name}'",
                        out var expectedMessage),
                    Is.True);

                LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape(expectedMessage)));
                InvokeOnValidate(definition);

                var exception = Assert.Throws<InvalidOperationException>(() => definition.Resolve(default));
                Assert.That(exception.Message, Is.EqualTo(expectedMessage));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        [Category("Extended")]
        public void SharedAudioAssembly_PublicSurface_IsTwoDimensionalOnly()
        {
            var methods = typeof(IAudioService)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method => !method.IsSpecialName)
                .OrderBy(method => method.Name)
                .ToArray();
            var methodNames = methods.Select(method => method.Name).ToArray();

            Assert.That(
                methodNames,
                Is.EqualTo(new[]
                {
                    "Play2D",
                    "PlayAttached",
                    "PlayBgm",
                    "Stop",
                    "StopBgm",
                }));

            Assert.That(methodNames, Does.Not.Contain("Play3D"));
            Assert.That(typeof(IAudioService).GetMethod(nameof(IAudioService.PlayBgm))?.GetParameters(), Has.Length.EqualTo(1));
            Assert.That(typeof(IAudioService).GetMethod(nameof(IAudioService.Stop))?.GetParameters(), Has.Length.EqualTo(1));
            Assert.That(typeof(IAudioService).GetMethod(nameof(IAudioService.StopBgm))?.GetParameters(), Has.Length.EqualTo(0));

            var contextProperties = typeof(AudioPlaybackContext)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();
            Assert.That(
                contextProperties,
                Is.EqualTo(new[]
                {
                    "DebugTag",
                    "OwnerEntityId",
                    "PitchMultiplier",
                    "VolumeMultiplier",
                }));
        }

        [Test]
        [Category("Extended")]
        public void SharedAudioAssembly_DoesNotReferenceGameplayOrUiMappedSeamAssemblies()
        {
            var references = typeof(IAudioService).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Not.Contain("Game.Feature.Gameplay"));
            Assert.That(references, Does.Not.Contain("Game.Feature.Gameplay.UIAccess"));
            Assert.That(references, Does.Not.Contain("Game.Feature.UI.Application"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioAssembly_DoesNotReferenceHostOrUiMappedSeamAssemblies()
        {
            var references = typeof(GameplayAudioRequestPlanner).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Not.Contain("Game.Feature.Gameplay.Host"));
            Assert.That(references, Does.Not.Contain("Game.Feature.Gameplay.UIAccess"));
            Assert.That(references, Does.Not.Contain("Game.Feature.UI.Application"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayCoreAssembly_DoesNotReferenceSharedAudioAssembly_AndHostDoes()
        {
            var gameplayReferences = typeof(TickResult).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            var hostReferences = typeof(GameplayTickViewPresenter).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(gameplayReferences, Does.Not.Contain(typeof(IAudioService).Assembly.GetName().Name));
            Assert.That(hostReferences, Does.Contain(typeof(IAudioService).Assembly.GetName().Name));
            Assert.That(hostReferences, Does.Contain(typeof(GameplayAudioRequestPlanner).Assembly.GetName().Name));
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioMap_ResolveOrThrow_FailsFastOnMissingTypedSemantic()
        {
            var map = ScriptableObject.CreateInstance<GameplayAudioMap>();
            try
            {
                var exception = Assert.Throws<InvalidOperationException>(
                    () => map.ResolveOrThrow(GameplayAudioSemanticId.PlayerDamage));
                StringAssert.Contains("missing gameplay audio semantic 'PlayerDamage'", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(map);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioMap_HasNoStringFallbackResolvePath()
        {
            var stringResolve = typeof(GameplayAudioMap).GetMethod(
                "ResolveOrThrow",
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(string) },
                modifiers: null);

            Assert.That(stringResolve, Is.Null);
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioMap_ValidateOrThrow_FailsOnDuplicateTypedSemantic()
        {
            var map = ScriptableObject.CreateInstance<GameplayAudioMap>();
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                SetEntries(
                    map,
                    (GameplayAudioSemanticId.PlayerDamage, definition, default),
                    (GameplayAudioSemanticId.PlayerDamage, definition, default));

                var exception = Assert.Throws<InvalidOperationException>(() => map.ValidateOrThrow());
                StringAssert.Contains("duplicate gameplay audio semantic 'PlayerDamage'", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(map);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioMap_ValidateOrThrow_FailsOnEmptyTypedSemantic()
        {
            var map = ScriptableObject.CreateInstance<GameplayAudioMap>();
            var definition = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            try
            {
                SetEntries(map, (GameplayAudioSemanticId.None, definition, default));

                var exception = Assert.Throws<InvalidOperationException>(() => map.ValidateOrThrow());
                StringAssert.Contains("contains an empty gameplay audio semantic", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(map);
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioMap_ValidateRequiredSemanticsOrThrow_UsesTypedCatalogSourceOfTruth()
        {
            var map = ScriptableObject.CreateInstance<GameplayAudioMap>();
            var definitions = CreateDefinitionsForSemantics(GameplayAudioSemanticCatalog.RequiredOneShotV1);
            try
            {
                SetEntries(
                    map,
                    (GameplayAudioSemanticId.PlayerDamage, definitions[GameplayAudioSemanticId.PlayerDamage], default),
                    (GameplayAudioSemanticId.EnemyDamage, definitions[GameplayAudioSemanticId.EnemyDamage], default),
                    (GameplayAudioSemanticId.EntityExitItemConsume, definitions[GameplayAudioSemanticId.EntityExitItemConsume], default),
                    (GameplayAudioSemanticId.EntityExitEnemyDeath, definitions[GameplayAudioSemanticId.EntityExitEnemyDeath], default));

                var exception = Assert.Throws<InvalidOperationException>(
                    () => map.ValidateRequiredSemanticsOrThrow(GameplayAudioSemanticCatalog.RequiredOneShotV1));
                Assert.That(
                    exception.Message,
                    Is.EqualTo(
                        $"GameplayAudioMap '{map.name}' is missing required gameplay audio semantics: " +
                        "EntityExitBoxDestroy, EntityExitOutOfBounds."));
            }
            finally
            {
                DestroyDefinitions(definitions);
                UnityEngine.Object.DestroyImmediate(map);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioRequestPlanner_EmitsTypedIdsOnly_ForDamageAndExitFamilies()
        {
            var planner = new GameplayAudioRequestPlanner();
            var presentationData = new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                new[]
                {
                    new TickPlayerDamagePresentationSignal(10, tookDamageThisTick: true, damageAmount: 1),
                },
                new[]
                {
                    new TickEnemyDamagePresentationSignal(20, tookDamageThisTick: true, damageAmount: 2),
                },
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                new[]
                {
                    new TickEntityExitPresentationSignal(
                        30,
                        TickEntityExitCause.EnemyDeath,
                        new SurfaceCell(FaceId.Floor, 0, 0),
                        new CubeTopologyState(FaceId.Floor),
                        Direction.Up,
                        EntityType.Unit),
                });
            var result = CreateTickResult(presentationData);

            var requests = planner.BuildRequests(result);

            Assert.That(requests.Select(request => request.SemanticId).ToArray(), Is.EqualTo(new[]
            {
                GameplayAudioSemanticId.PlayerDamage,
                GameplayAudioSemanticId.EnemyDamage,
                GameplayAudioSemanticId.EntityExitEnemyDeath,
            }));
            Assert.That(requests.All(request => request.SemanticId != GameplayAudioSemanticId.None), Is.True);
            Assert.That(requests.Select(request => request.Context.DebugTag).ToArray(), Is.EqualTo(new[]
            {
                GameplayAudioSemanticCatalog.Format(GameplayAudioSemanticId.PlayerDamage),
                GameplayAudioSemanticCatalog.Format(GameplayAudioSemanticId.EnemyDamage),
                GameplayAudioSemanticCatalog.Format(GameplayAudioSemanticId.EntityExitEnemyDeath),
            }));
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioRequestPlanner_IsPure_AndRuntimeAgnostic()
        {
            var plannerType = typeof(GameplayAudioRequestPlanner);
            var constructors = plannerType.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
            var fields = plannerType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var buildRequests = plannerType.GetMethod(nameof(GameplayAudioRequestPlanner.BuildRequests));

            Assert.That(constructors, Has.Length.EqualTo(1));
            Assert.That(constructors[0].GetParameters(), Is.Empty);
            Assert.That(fields.Select(field => field.FieldType.Name), Does.Not.Contain(nameof(GameplayAudioMap)));
            Assert.That(fields.Select(field => field.FieldType.Name), Does.Not.Contain(nameof(IAudioService)));
            Assert.That(buildRequests, Is.Not.Null);
            Assert.That(buildRequests.GetParameters().Select(parameter => parameter.ParameterType), Is.EqualTo(new[] { typeof(TickResult) }));
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioSemanticCatalog_RequiredOneShotV1_IsCanonicalTypedSource()
        {
            Assert.That(
                GameplayAudioSemanticCatalog.RequiredOneShotV1.ToArray(),
                Is.EqualTo(new[]
                {
                    GameplayAudioSemanticId.PlayerDamage,
                    GameplayAudioSemanticId.EnemyDamage,
                    GameplayAudioSemanticId.EntityExitItemConsume,
                    GameplayAudioSemanticId.EntityExitBoxDestroy,
                    GameplayAudioSemanticId.EntityExitEnemyDeath,
                    GameplayAudioSemanticId.EntityExitOutOfBounds,
                }));
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioRuntimeCode_HasNoInlineGameplaySemanticStringLiterals_OutsideCatalogFormatter()
        {
            var runtimeRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "_Features/Gameplay"));
            var semanticTokens = new[]
            {
                "\"player.damage\"",
                "\"enemy.damage\"",
                "\"entity.exit.item_consume\"",
                "\"entity.exit.box_destroy\"",
                "\"entity.exit.enemy_death\"",
                "\"entity.exit.out_of_bounds\"",
                "\"PlayerDamage\"",
                "\"EnemyDamage\"",
                "\"EntityExitItemConsume\"",
                "\"EntityExitBoxDestroy\"",
                "\"EntityExitEnemyDeath\"",
                "\"EntityExitOutOfBounds\"",
            };

            var offendingFiles = Directory
                .GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains("_Tests", StringComparison.Ordinal))
                .Where(path => !path.EndsWith("GameplayAudioSemantics.cs", StringComparison.Ordinal))
                .Where(path => semanticTokens.Any(token => File.ReadAllText(path).Contains(token, StringComparison.Ordinal)))
                .Select(Path.GetFileName)
                .ToArray();

            Assert.That(offendingFiles, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void RemovedGameplayAudioSeamTypes_AreAbsentFromActiveRuntimeAssembly()
        {
            var typeNames = typeof(GameplayAudioMap).Assembly
                .GetTypes()
                .Select(type => type.Name)
                .ToArray();

            Assert.That(typeNames, Does.Not.Contain("GameplayAudioPresenter"));
            Assert.That(typeNames, Does.Not.Contain("IGameplayAudioCueProjector"));
            Assert.That(typeNames, Does.Not.Contain("GameplayAudioCue"));
        }

        private static TickResult CreateTickResult(
            TickPresentationData presentationData,
            IReadOnlyList<EntityState> finalEntities = null)
        {
            return new TickResult(
                1,
                new[] { TickPhase.Plan },
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                finalEntities ?? Array.Empty<EntityState>(),
                Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                presentationData,
                string.Empty,
                TickTrace.Empty,
                StageObjectiveTickResult.NoObjective);
        }

        private static Dictionary<GameplayAudioSemanticId, AudioDefinition> CreateDefinitionsForSemantics(
            IReadOnlyList<GameplayAudioSemanticId> semanticIds)
        {
            var definitions = new Dictionary<GameplayAudioSemanticId, AudioDefinition>();
            for (var i = 0; i < semanticIds.Count; i++)
            {
                definitions[semanticIds[i]] = ScriptableObject.CreateInstance<SingleAudioDefinition>();
            }

            return definitions;
        }

        private static void DestroyDefinitions(Dictionary<GameplayAudioSemanticId, AudioDefinition> definitions)
        {
            foreach (var definition in definitions.Values)
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        private static void SetEntries(
            GameplayAudioMap map,
            params (GameplayAudioSemanticId semanticId, AudioDefinition definition, AudioAttachmentSlot slot)[] entries)
        {
            var serializedObject = new SerializedObject(map);
            var entriesProperty = serializedObject.FindProperty("entries");
            entriesProperty.arraySize = entries.Length;

            for (var i = 0; i < entries.Length; i++)
            {
                var element = entriesProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("SemanticId").enumValueIndex = (int)entries[i].semanticId;
                var binding = element.FindPropertyRelative("Binding");
                binding.FindPropertyRelative("definition").objectReferenceValue = entries[i].definition;
                binding.FindPropertyRelative("attachmentSlot").FindPropertyRelative("id").stringValue = entries[i].slot.Id;
                binding.FindPropertyRelative("policy").managedReferenceValue = null;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void InvokeOnValidate(ScriptableObject scriptableObject)
        {
            var onValidate = scriptableObject.GetType().GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(onValidate, Is.Not.Null);
            onValidate.Invoke(scriptableObject, null);
        }

        private static void SetSerializedField(Type declaringType, object instance, string fieldName, object value)
        {
            var field = declaringType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {declaringType.FullName}.");
            field.SetValue(instance, value);
        }
    }
}
