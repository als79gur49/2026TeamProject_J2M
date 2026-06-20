using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Feature.Gameplay.ActionAudio;
using Game.Feature.Gameplay.Audio;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.EnemyAudio;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Stages;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class AudioArchitectureTests
    {
        private const string GameplayAudioMapPath =
            "Assets/_Features/Gameplay/Gameplay_Audio/Maps/GameplayAudioMap_CampaignV1.asset";
        private const string PlayerHurtDefinitionPath =
            "Assets/_Shared/Audio/Definitions/Sfx/PlayerSounds/Player_Hurt_Def.asset";
        private const string PlayerHurtClipPath =
            "Assets/_Shared/Audio/Clips/Sfx/PlayerSounds/Player_hurt.mp3";

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
            var playBgmParameters = typeof(IAudioService).GetMethod(nameof(IAudioService.PlayBgm))?.GetParameters();
            var stopBgmParameters = typeof(IAudioService).GetMethod(nameof(IAudioService.StopBgm))?.GetParameters();
            Assert.That(playBgmParameters, Has.Length.EqualTo(1));
            Assert.That(playBgmParameters?[0].ParameterType, Is.EqualTo(typeof(AudioBgmPlaybackRequest)));
            Assert.That(typeof(IAudioService).GetMethod(nameof(IAudioService.Stop))?.GetParameters(), Has.Length.EqualTo(1));
            Assert.That(stopBgmParameters, Has.Length.EqualTo(1));
            Assert.That(stopBgmParameters?[0].ParameterType, Is.EqualTo(typeof(AudioBgmStopRequest)));

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
        public void SharedAudioRuntime_ExposesGameplayPauseThroughNarrowServiceOnly()
        {
            var audioServiceMethodNames = typeof(IAudioService)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method => !method.IsSpecialName)
                .Select(method => method.Name)
                .OrderBy(name => name)
                .ToArray();
            var pauseServiceMethodNames = typeof(IAudioPlaybackPauseService)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method => !method.IsSpecialName)
                .Select(method => method.Name)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(audioServiceMethodNames, Does.Not.Contain("PauseGroup"));
            Assert.That(audioServiceMethodNames, Does.Not.Contain("ResumeGroup"));
            Assert.That(
                pauseServiceMethodNames,
                Is.EqualTo(new[]
                {
                    "IsGroupPaused",
                    "PauseGroup",
                    "ResumeGroup",
                }));
            Assert.That(typeof(IAudioPlaybackPauseService).Assembly, Is.EqualTo(typeof(IAudioService).Assembly));
            Assert.That(typeof(AudioManager).GetInterfaces(), Does.Contain(typeof(IAudioPlaybackPauseService)));
            Assert.That(typeof(AudioRuntimeRoot).GetProperty(nameof(AudioRuntimeRoot.AudioPlaybackPauseService)), Is.Not.Null);
            Assert.That(typeof(AudioRuntimeInstaller).GetProperty(nameof(AudioRuntimeInstaller.AudioPlaybackPauseService)), Is.Not.Null);
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
            Assert.That(references, Does.Not.Contain("Game.Feature.Gameplay.ActionAudio"));
            Assert.That(references, Does.Not.Contain("Game.Feature.Gameplay.UIAccess"));
            Assert.That(references, Does.Not.Contain("Game.Feature.UI.Application"));
            Assert.That(references, Does.Not.Contain("Game.Feature.Flow.Audio"));
        }

        [Test]
        [Category("Extended")]
        public void SharedAudioRuntime_SourceSentinel_DoesNotImplementCrossfadeMultiSource()
        {
            var source = ReadRepoFile("Assets/_Shared/Audio/Runtime/AudioPlaybackService.cs");

            Assert.That(source, Does.Contain("private AudioSource bgmSource;"));
            Assert.That(source, Does.Not.Contain("secondBgmSource"));
            Assert.That(source, Does.Not.Contain("secondaryBgmSource"));
            Assert.That(source, Does.Not.Contain("bgmSourceA"));
            Assert.That(source, Does.Not.Contain("bgmSourceB"));
            Assert.That(source, Does.Not.Contain("Crossfade"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayActionAudioAssembly_ReferencesGameplayAndShared_WhileHostReferencesActionAudio()
        {
            var actionAudioReferences = typeof(GameplayActionAudioProfile).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            var hostReferences = typeof(GameplayTickViewPresenter).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(actionAudioReferences, Does.Contain("Game.Feature.Gameplay"));
            Assert.That(actionAudioReferences, Does.Contain("Game.Shared.Audio"));
            Assert.That(actionAudioReferences, Does.Not.Contain("Game.Feature.Gameplay.Host"));
            Assert.That(hostReferences, Does.Contain("Game.Feature.Gameplay.ActionAudio"));
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
            Assert.That(gameplayReferences, Does.Not.Contain(typeof(GameplayActionAudioProfile).Assembly.GetName().Name));
            Assert.That(hostReferences, Does.Contain(typeof(IAudioService).Assembly.GetName().Name));
            Assert.That(hostReferences, Does.Contain(typeof(GameplayAudioRequestPlanner).Assembly.GetName().Name));
            Assert.That(hostReferences, Does.Contain(typeof(GameplayActionAudioProfile).Assembly.GetName().Name));
        }

        [Test]
        [Category("Extended")]
        public void AudioBindingDiagnostics_PublicSurface_RemainsBindingLocal_AndSemanticFree()
        {
            var publicMethods = typeof(AudioBindingDiagnostics)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            var publicMethodSignatureText = string.Join(
                "\n",
                publicMethods.Select(method => $"{method.ReturnType.Name}:{method.Name}:{string.Join(",", method.GetParameters().Select(parameter => parameter.ParameterType.FullName))}"));

            Assert.That(typeof(AudioBindingDiagnostics).Assembly, Is.EqualTo(typeof(IAudioService).Assembly));
            Assert.That(typeof(AudioBindingValidationOptions).Assembly, Is.EqualTo(typeof(IAudioService).Assembly));
            Assert.That(publicMethodSignatureText, Does.Not.Contain(nameof(GameplayActionKind)));
            Assert.That(publicMethodSignatureText, Does.Not.Contain(nameof(GameplayActionAudioMoment)));
            Assert.That(publicMethodSignatureText, Does.Not.Contain(nameof(GameplayAudioSemanticId)));
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioMap_UsesAudioBindingDiagnostics_ForBindingLocalValidationOnly()
        {
            var source = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Audio/Runtime/GameplayAudioMap.cs");

            Assert.That(source, Does.Contain("AudioBindingDiagnostics.AppendValidationErrors"));
            Assert.That(source, Does.Contain("AudioBindingValidationOptions.Default"));
            Assert.That(source, Does.Not.Contain("allowedCategories:"));
            Assert.That(source, Does.Not.Contain("allowLoopingDefinitions: false"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayActionAudioProfile_PassesSfxOnlyOneShotPolicy_IntoSharedDiagnostics()
        {
            var source = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_ActionAudio/Runtime/GameplayActionAudioProfile.cs");

            Assert.That(source, Does.Contain("AudioBindingDiagnostics.AppendValidationErrors"));
            Assert.That(source, Does.Contain("AudioCategory.Sfx"));
            Assert.That(source, Does.Contain("allowLoopingDefinitions: false"));
            Assert.That(source, Does.Contain("allowNullBinding: entry.IsOptional"));
        }

        [Test]
        [Category("Extended")]
        public void SharedAudioRuntime_SourceSentinel_DoesNotMentionGameplayActionSymbols()
        {
            var runtimeDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "_Shared/Audio/Runtime"));
            var combinedSource = string.Join(
                "\n",
                Directory.GetFiles(runtimeDirectory, "*.cs", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path)
                    .Select(File.ReadAllText));

            Assert.That(combinedSource, Does.Not.Contain(nameof(GameplayActionKind)));
            Assert.That(combinedSource, Does.Not.Contain(nameof(GameplayActionAudioMoment)));
            Assert.That(combinedSource, Does.Not.Contain(nameof(GameplayAudioSemanticId)));
        }

        [Test]
        [Category("Extended")]
        public void GameplaySceneHostConfiguration_RequiresPresentationAudioConfig()
        {
            var hostFields = typeof(GameplaySceneHostConfiguration)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .ToArray();
            var hostFieldTypeNames = hostFields.Select(field => field.FieldType.Name).ToArray();

            Assert.That(
                hostFields.Count(field => field.FieldType == typeof(GameplayPresentationAudioConfig)),
                Is.EqualTo(1));
            Assert.That(hostFieldTypeNames, Does.Not.Contain(nameof(GameplayAudioMap)));
            Assert.That(hostFieldTypeNames, Does.Not.Contain("BlockAudioMap"));
            Assert.That(hostFieldTypeNames, Does.Not.Contain("PlayerLocomotionAudioMap"));
            Assert.That(hostFieldTypeNames, Does.Not.Contain("TopologyAudioMap"));
            Assert.That(hostFieldTypeNames, Does.Not.Contain("GravityFieldAudioMap"));
            Assert.That(hostFieldTypeNames, Does.Not.Contain("TileFeatureAudioMap"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayPresentationAudioConfig_ExcludesOtherOwnership()
        {
            var configFields = typeof(GameplayPresentationAudioConfig)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .ToArray();
            var configFieldTypeNames = configFields.Select(field => field.FieldType.Name).ToArray();
            var configFieldNames = configFields.Select(field => field.Name).ToArray();
            var configSource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPresentationAudioConfig.cs");

            var excludedTypeNames = new[]
            {
                nameof(GameplayActionAudioProfile),
                "EnemyAudioProfile",
                nameof(EnemyAudioRequirementPolicy),
                nameof(EnemyAudioRequirementBinding),
                "UiAudioCueMap",
                nameof(BgmProfile),
                nameof(StageAudioDefinition),
                nameof(AudioRuntimeInstaller),
                "AudioSettingsPortAdapter",
                "UIAudioChannelMapper",
                "GlobalAudioFlowBootstrap",
                "SceneBgmRequestSource",
            };

            foreach (var excludedTypeName in excludedTypeNames)
            {
                Assert.That(configFieldTypeNames, Does.Not.Contain(excludedTypeName), excludedTypeName);
                Assert.That(configFieldNames.Any(name => name.Contains(excludedTypeName, StringComparison.Ordinal)), Is.False, excludedTypeName);
                Assert.That(configSource, Does.Not.Contain(excludedTypeName), excludedTypeName);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayHostRuntimeFactory_UsesPresentationAudioConfigButStillCreatesTypedControllers()
        {
            var factorySource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayHostRuntimeFactory.cs");

            Assert.That(factorySource, Does.Contain("configuration?.GameplayPresentationAudioConfig"));
            Assert.That(factorySource, Does.Contain("audioConfig.ValidateOrThrow()"));
            Assert.That(factorySource, Does.Contain("presenter.AttachGameplayAudioRuntime(playbackPort, audioConfig.GameplayAudioMap)"));
            Assert.That(factorySource, Does.Contain("presenter.AttachBlockAudioRuntime(playbackPort, audioConfig.BlockAudioMap)"));
            Assert.That(factorySource, Does.Contain("presenter.AttachPlayerLocomotionAudioRuntime(playbackPort, audioConfig.PlayerLocomotionAudioMap)"));
            Assert.That(factorySource, Does.Contain("presenter.AttachTopologyAudioRuntime(playbackPort, audioConfig.TopologyAudioMap)"));
            Assert.That(factorySource, Does.Contain("presenter.AttachGravityFieldAudioRuntime(playbackPort, audioConfig.GravityFieldAudioMap)"));
            Assert.That(factorySource, Does.Contain("presenter.AttachTileFeatureAudioRuntime(playbackPort, audioConfig.TileFeatureAudioMap)"));
            Assert.That(factorySource, Does.Not.Contain("GenericAudioDispatcher"));
            Assert.That(factorySource, Does.Not.Contain("AudioDispatcher"));
        }

        [Test]
        [Category("Extended")]
        public void AudioArchitecture_NoGenericAudioDispatcherIntroduced()
        {
            var hostSource = ReadRepoFilesUnder("Assets/_Features/Gameplay/Gameplay_Host/Runtime");

            Assert.That(hostSource, Does.Not.Contain("GenericAudioDispatcher"));
            Assert.That(hostSource, Does.Not.Contain("AudioDispatcher"));
            Assert.That(hostSource, Does.Not.Contain("AudioManager.Instance"));
            Assert.That(hostSource, Does.Not.Contain("FindObjectOfType<"));
            Assert.That(hostSource, Does.Not.Contain("FindAnyObjectByType<"));
            Assert.That(hostSource, Does.Not.Contain("new AudioManager"));
            Assert.That(hostSource, Does.Not.Contain(".PlayBgm("));
        }

        [Test]
        [Category("Extended")]
        public void EnemyAudioRequirementPolicy_IsEnemyLaneOwned()
        {
            var enemyAudioAssembly = typeof(EnemyAudioProfile).Assembly;
            var references = typeof(EnemyAudioRequirementPolicy).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(typeof(EnemyAudioRequirementPolicy).Assembly, Is.EqualTo(enemyAudioAssembly));
            Assert.That(enemyAudioAssembly.GetName().Name, Is.EqualTo("Game.Feature.Gameplay.EnemyAudio"));
            Assert.That(references, Does.Not.Contain("Game.Feature.Gameplay.Host"));
            Assert.That(references, Does.Not.Contain("Game.Feature.Gameplay.UIAccess"));
            Assert.That(references, Does.Not.Contain("Game.Feature.UI.Application"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyAudioRequirementBinding_IsEnemyLaneOwned()
        {
            var enemyAudioAssembly = typeof(EnemyAudioProfile).Assembly;
            var references = typeof(EnemyAudioRequirementBinding).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(typeof(EnemyAudioRequirementBinding).Assembly, Is.EqualTo(enemyAudioAssembly));
            Assert.That(enemyAudioAssembly.GetName().Name, Is.EqualTo("Game.Feature.Gameplay.EnemyAudio"));
            Assert.That(references, Does.Not.Contain("Game.Feature.Gameplay.Host"));
            Assert.That(references, Does.Not.Contain("Game.Feature.Gameplay.UIAccess"));
            Assert.That(references, Does.Not.Contain("Game.Feature.UI.Application"));
        }

        [Test]
        [Category("Extended")]
        public void SharedAudioRuntime_DoesNotReferenceEnemyAudioCueNames()
        {
            var runtimeSource = ReadRepoFilesUnder("Assets/_Shared/Audio/Runtime");

            Assert.That(runtimeSource, Does.Not.Contain(nameof(EnemyAudioCue)));
            Assert.That(runtimeSource, Does.Not.Contain(nameof(EnemyAudioCue.ChargeActiveLoop)));
            Assert.That(runtimeSource, Does.Not.Contain(nameof(EnemyAudioCue.ForwardCellImpact)));
            Assert.That(runtimeSource, Does.Not.Contain(nameof(EnemyAudioCue.StationaryActive)));
            Assert.That(runtimeSource, Does.Not.Contain(nameof(EnemyAudioCue.PassiveContact)));
        }

        [Test]
        [Category("Extended")]
        public void GameplayActionAudioMoment_RemainsOneShotOnly_WithoutLoopValues()
        {
            var names = Enum.GetNames(typeof(GameplayActionAudioMoment));

            Assert.That(names, Does.Not.Contain("Execute"));
            Assert.That(names, Does.Not.Contain("Recovery"));
            Assert.That(names, Does.Not.Contain("Contact"));
            Assert.That(names, Does.Not.Contain("ImpactEnemy"));
            Assert.That(names, Does.Not.Contain("Blocked"));
            Assert.That(names, Does.Not.Contain("Loop"));
            Assert.That(names, Does.Not.Contain("SlideLoop"));
            Assert.That(names, Does.Not.Contain("ChargeLoop"));
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
                LogAssert.Expect(LogType.Error, new Regex("duplicate gameplay audio semantic 'PlayerDamage'"));
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
                LogAssert.Expect(LogType.Error, new Regex("contains an empty gameplay audio semantic"));
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
        [Category("Core")]
        public void GameplayAudioMap_PlayerDamage_UsesPlayerHurtDefinition()
        {
            var map = AssetDatabase.LoadAssetAtPath<GameplayAudioMap>(GameplayAudioMapPath);
            var definition = AssetDatabase.LoadAssetAtPath<SingleAudioDefinition>(PlayerHurtDefinitionPath);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(PlayerHurtClipPath);

            Assert.That(map, Is.Not.Null, GameplayAudioMapPath);
            Assert.That(definition, Is.Not.Null, PlayerHurtDefinitionPath);
            Assert.That(clip, Is.Not.Null, PlayerHurtClipPath);

            var binding = map.ResolveOrThrow(GameplayAudioSemanticId.PlayerDamage);
            var playback = definition.Resolve(default);

            Assert.That(binding.Definition, Is.SameAs(definition));
            Assert.That(definition.Category, Is.EqualTo(AudioCategory.Sfx));
            Assert.That(definition.Loop, Is.False);
            Assert.That(playback.Clip, Is.SameAs(clip));
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
            GameplayAudioGovernanceAssertions.AssertOnlyApprovedHostFamilies(
                requests.Select(request => request.SemanticId),
                "Planner output");
        }

        [Test]
        [Category("Core")]
        public void GameplayAudioRequestPlanner_PlayerDeathOnly_EmitsPlayerDamageFallback()
        {
            var planner = new GameplayAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                playerDeathSignals: new[]
                {
                    CreatePlayerDeathSignal(10),
                }));

            var requests = planner.BuildRequests(result);

            Assert.That(requests.Select(request => request.SemanticId).ToArray(), Is.EqualTo(new[]
            {
                GameplayAudioSemanticId.PlayerDamage,
            }));
            Assert.That(requests[0].OwnerEntityId, Is.EqualTo(10));
            Assert.That(
                requests[0].Context.DebugTag,
                Is.EqualTo(GameplayAudioSemanticCatalog.Format(GameplayAudioSemanticId.PlayerDamage)));
            GameplayAudioGovernanceAssertions.AssertOnlyApprovedHostFamilies(
                requests.Select(request => request.SemanticId),
                "Player death fallback planner output");
        }

        [Test]
        [Category("Core")]
        public void GameplayAudioRequestPlanner_PlayerDamageAndDeathSameTick_DoesNotDuplicatePlayerDamage()
        {
            var planner = new GameplayAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                playerDamageSignals: new[]
                {
                    new TickPlayerDamagePresentationSignal(10, tookDamageThisTick: true, damageAmount: 1),
                },
                playerDeathSignals: new[]
                {
                    CreatePlayerDeathSignal(10),
                },
                enemyDamageSignals: new[]
                {
                    new TickEnemyDamagePresentationSignal(20, tookDamageThisTick: true, damageAmount: 1),
                }));

            var requests = planner.BuildRequests(result);

            Assert.That(requests.Select(request => request.SemanticId).ToArray(), Is.EqualTo(new[]
            {
                GameplayAudioSemanticId.PlayerDamage,
                GameplayAudioSemanticId.EnemyDamage,
            }));
            Assert.That(
                requests.Count(request => request.SemanticId == GameplayAudioSemanticId.PlayerDamage),
                Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void PlayerInvincible_DoesNotEmitPlayerDamageAudioIfPolicyRequires()
        {
            var planner = new GameplayAudioRequestPlanner();
            var result = CreateTickResult(CreatePresentationData(
                playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>()));

            var requests = planner.BuildRequests(result);

            Assert.That(requests.Select(request => request.SemanticId), Has.No.EqualTo(GameplayAudioSemanticId.PlayerDamage));
        }

        [Test]
        [Category("Core")]
        public void GameplayAudioRequestPlanner_EntityExitBoxDestroy_AfterEntityMotion_UsesEntityMotionDurationDelay()
        {
            var planner = new GameplayAudioRequestPlanner();
            var timingProfile = CreateTimingProfile(flipMotionDurationSeconds: 0.73f);
            var exitedEntityId = 30;
            var result = CreateTickResult(CreatePresentationData(
                entityMotions: new[]
                {
                    new TickEntityMotion(
                        exitedEntityId,
                        TickEntityMotionKind.Flip,
                        new SurfaceCell(FaceId.Floor, -1, 0),
                        new SurfaceCell(FaceId.Floor, 1, 0)),
                },
                entityExitSignals: new[]
                {
                    CreateExitSignal(
                        exitedEntityId,
                        TickEntityExitCause.BoxDestroy,
                        EntityType.Box,
                        EntityExitPresentationTiming.AfterEntityMotion),
                }));

            var requests = planner.BuildRequests(result, timingProfile);

            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].SemanticId, Is.EqualTo(GameplayAudioSemanticId.EntityExitBoxDestroy));
            Assert.That(requests[0].DelaySeconds, Is.GreaterThan(0f));
            Assert.That(requests[0].DelaySeconds, Is.EqualTo(timingProfile.FlipMotionDurationSeconds).Within(0.0001f));
        }

        [Test]
        [Category("Core")]
        public void GameplayAudioRequestPlanner_EntityExitBoxDestroy_AfterEntityMotion_NoMotion_FallsBackImmediate()
        {
            var planner = new GameplayAudioRequestPlanner();
            var exitedEntityId = 30;
            var result = CreateTickResult(CreatePresentationData(
                entityExitSignals: new[]
                {
                    CreateExitSignal(
                        exitedEntityId,
                        TickEntityExitCause.BoxDestroy,
                        EntityType.Box,
                        EntityExitPresentationTiming.AfterEntityMotion),
                }));

            var requests = planner.BuildRequests(result);

            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].SemanticId, Is.EqualTo(GameplayAudioSemanticId.EntityExitBoxDestroy));
            Assert.That(requests[0].DelaySeconds, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void GameplayAudioRequestPlanner_EntityExitEnemyDeath_AtContactTime_KeepsExistingContactDelay()
        {
            var planner = new GameplayAudioRequestPlanner();
            var timingProfile = CreateTimingProfile(flipMotionDurationSeconds: 0.8f);
            var contactNormalizedTime = 0.35f;
            var result = CreateTickResult(CreatePresentationData(
                entityExitSignals: new[]
                {
                    CreateExitSignal(
                        40,
                        TickEntityExitCause.EnemyDeath,
                        EntityType.Unit,
                        EntityExitPresentationTiming.AtContactTime,
                        contactNormalizedTime),
                }));

            var requests = planner.BuildRequests(result, timingProfile);

            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].SemanticId, Is.EqualTo(GameplayAudioSemanticId.EntityExitEnemyDeath));
            Assert.That(
                requests[0].DelaySeconds,
                Is.EqualTo(timingProfile.FlipMotionDurationSeconds * contactNormalizedTime).Within(0.0001f));
        }

        [Test]
        [Category("Core")]
        public void GameplayAudioRequestPlanner_NonBoxDestroy_AfterEntityMotion_RemainsExistingTiming()
        {
            var planner = new GameplayAudioRequestPlanner();
            var timingProfile = CreateTimingProfile(flipMotionDurationSeconds: 0.73f);
            var exitedEntityId = 50;
            var result = CreateTickResult(CreatePresentationData(
                entityMotions: new[]
                {
                    new TickEntityMotion(
                        exitedEntityId,
                        TickEntityMotionKind.Flip,
                        new SurfaceCell(FaceId.Floor, -1, 0),
                        new SurfaceCell(FaceId.Floor, 1, 0)),
                },
                entityExitSignals: new[]
                {
                    CreateExitSignal(
                        exitedEntityId,
                        TickEntityExitCause.ItemConsume,
                        EntityType.Box,
                        EntityExitPresentationTiming.AfterEntityMotion),
                }));

            var requests = planner.BuildRequests(result, timingProfile);

            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].SemanticId, Is.EqualTo(GameplayAudioSemanticId.EntityExitItemConsume));
            Assert.That(requests[0].DelaySeconds, Is.Zero);
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
            var parameters = buildRequests.GetParameters();
            Assert.That(parameters, Has.Length.EqualTo(2));
            Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(TickResult)));
            Assert.That(parameters[1].ParameterType, Is.EqualTo(typeof(GameplayTimingProfile)));
            Assert.That(parameters[1].IsOptional, Is.True);
            Assert.That(parameters[1].DefaultValue, Is.Null);
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioSemanticCatalog_CatalogsEveryNonNoneSemanticExactlyOnce()
        {
            var enumValues = Enum.GetValues(typeof(GameplayAudioSemanticId))
                .Cast<GameplayAudioSemanticId>()
                .Where(semanticId => semanticId != GameplayAudioSemanticId.None)
                .OrderBy(semanticId => semanticId)
                .ToArray();
            var catalogedValues = GameplayAudioSemanticCatalog.GovernedSemantics
                .Select(descriptor => descriptor.SemanticId)
                .OrderBy(semanticId => semanticId)
                .ToArray();

            Assert.That(
                catalogedValues,
                Is.EqualTo(enumValues),
                $"Every non-None gameplay audio semantic id must have catalog metadata. {GameplayAudioGovernanceAssertions.ExpansionGuidance}");
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioSemanticCatalog_RequiredOneShotV1_MatchesApprovedGovernedSet()
        {
            var descriptorDerivedSet = GameplayAudioSemanticCatalog.GovernedSemantics
                .Where(descriptor => descriptor.IsRequiredForHostOneShotV1)
                .Select(descriptor => descriptor.SemanticId)
                .ToArray();

            Assert.That(
                GameplayAudioSemanticCatalog.RequiredOneShotV1.ToArray(),
                Is.EqualTo(descriptorDerivedSet),
                $"Required gameplay host one-shot set must stay descriptor-derived. {GameplayAudioGovernanceAssertions.ExpansionGuidance}");
            GameplayAudioGovernanceAssertions.AssertExactRequiredHostOneShotSet(GameplayAudioSemanticCatalog.RequiredOneShotV1);
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioSemanticCatalog_RequiredOneShotV1_ContainsOnlyApprovedHostFamilies()
        {
            GameplayAudioGovernanceAssertions.AssertOnlyApprovedHostFamilies(
                GameplayAudioSemanticCatalog.RequiredOneShotV1,
                "Required gameplay host one-shot set");
        }

        [Test]
        [Category("Extended")]
        public void GameplayAudioSemanticCatalog_NonApprovedFamilies_AreExcludedFromRequiredOneShotV1()
        {
            var disallowedRequiredDescriptors = GameplayAudioSemanticCatalog.GovernedSemantics
                .Where(descriptor => descriptor.IsRequiredForHostOneShotV1)
                .Where(descriptor => !GameplayAudioSemanticCatalog.IsApprovedHostOneShotFamily(descriptor.Family))
                .Select(descriptor => $"{GameplayAudioSemanticCatalog.Format(descriptor.SemanticId)} ({descriptor.Family})")
                .ToArray();

            Assert.That(
                disallowedRequiredDescriptors,
                Is.Empty,
                $"Only approved gameplay host one-shot families may enter the required set. {GameplayAudioGovernanceAssertions.ExpansionGuidance}");
        }

        [Test]
        [Category("Extended")]
        public void StageAssembly_DoesNotReferenceGameplayHostAssembly()
        {
            var stageReferences = LoadRequiredAssembly("Game.Feature.Stages")
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(stageReferences, Does.Not.Contain(typeof(GameplayTickViewPresenter).Assembly.GetName().Name));
        }

        [Test]
        [Category("Extended")]
        public void UiRuntimeAssemblies_OutsideComposition_DoNotReferenceGameplayHostAssembly()
        {
            var hostAssemblyName = typeof(GameplayTickViewPresenter).Assembly.GetName().Name;
            var uiAssemblyNames = new[]
            {
                "Game.Feature.UI.Application",
                "Game.Feature.UI.Flow",
                "Game.Feature.UI.HUD",
                "Game.Feature.UI.Popups",
                "Game.Feature.UI.Screens",
            };

            foreach (var assemblyName in uiAssemblyNames)
            {
                var references = LoadRequiredAssembly(assemblyName)
                    .GetReferencedAssemblies()
                    .Select(reference => reference.Name)
                    .ToArray();
                Assert.That(references, Does.Not.Contain(hostAssemblyName), assemblyName);
            }
        }

        [Test]
        [Category("Extended")]
        public void UiCompositionAndStageRuntime_DoNotReferenceGameplayAudioPresentationController_AsSecondaryGuard()
        {
            // Assembly/reference-level checks are the primary guard.
            // This narrow scan exists only because UI composition legally references the host assembly for other reasons.
            var searchRoots = new[]
            {
                Path.Combine(Application.dataPath, "_Features/UI/UI_Composition/Runtime"),
                Path.Combine(Application.dataPath, "_Features/UI/UI_Flow/Runtime"),
                Path.Combine(Application.dataPath, "_Features/Stages/Runtime"),
            };
            var offendingFiles = searchRoots
                .SelectMany(root => Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                .Where(path => File.ReadAllText(path).Contains(nameof(GameplayAudioPresentationController), StringComparison.Ordinal))
                .Select(Path.GetFileName)
                .OrderBy(name => name)
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

        private static TickPresentationData CreatePresentationData(
            TickEntityMotion[] entityMotions = null,
            TickPlayerDamagePresentationSignal[] playerDamageSignals = null,
            TickPlayerDeathPresentationSignal[] playerDeathSignals = null,
            TickEnemyDamagePresentationSignal[] enemyDamageSignals = null,
            TickEntityExitPresentationSignal[] entityExitSignals = null)
        {
            return new TickPresentationData(
                entityMotions ?? Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals ?? Array.Empty<TickPlayerDamagePresentationSignal>(),
                playerDeathSignals ?? Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals ?? Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                entityExitSignals ?? Array.Empty<TickEntityExitPresentationSignal>());
        }

        private static TickEntityExitPresentationSignal CreateExitSignal(
            int entityId,
            TickEntityExitCause exitCause,
            EntityType entityType,
            EntityExitPresentationTiming timing,
            float visualContactNormalizedTime = 0f)
        {
            return new TickEntityExitPresentationSignal(
                entityId,
                exitCause,
                new SurfaceCell(FaceId.Floor, 0, 0),
                new CubeTopologyState(FaceId.Floor),
                Direction.Up,
                entityType,
                timing: timing,
                visualContactNormalizedTime: visualContactNormalizedTime);
        }

        private static GameplayTimingProfile CreateTimingProfile(float flipMotionDurationSeconds)
        {
            return new GameplayTimingProfile(
                simulationTicksPerSecond: GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                initialMoveDelaySeconds: GameplayTimingProfile.DefaultInitialMoveDelaySeconds,
                repeatedMoveIntervalSeconds: GameplayTimingProfile.DefaultRepeatedMoveIntervalSeconds,
                boxSlideStepIntervalSeconds: GameplayTimingProfile.DefaultBoxSlideStepIntervalSeconds,
                forwardCellTravelStepIntervalSeconds: GameplayTimingProfile.DefaultForwardCellTravelStepIntervalSeconds,
                moveMotionDurationSeconds: GameplayTimingProfile.DefaultMoveMotionDurationSeconds,
                pushMotionDurationSeconds: GameplayTimingProfile.DefaultPushMotionDurationSeconds,
                topologyMotionDurationSeconds: GameplayTimingProfile.DefaultTopologyMotionDurationSeconds,
                flipMotionDurationSeconds: flipMotionDurationSeconds,
                flipArcHeightInCells: GameplayTimingProfile.DefaultFlipArcHeightInCells,
                maxTicksPerFrame: GameplayTimingProfile.DefaultMaxTicksPerFrame);
        }

        private static TickPlayerDeathPresentationSignal CreatePlayerDeathSignal(int entityId)
        {
            return new TickPlayerDeathPresentationSignal(
                entityId,
                didDieThisTick: true,
                sourceEntityId: 0,
                fallbackFacing: Direction.Up,
                resolvedDamageSourceAvailable: false,
                damageAmountAtFatalHit: 1,
                DeathDirectionHintKind.FacingReverse);
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

        private static Assembly LoadRequiredAssembly(string assemblyName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                       .FirstOrDefault(assembly => assembly.GetName().Name == assemblyName) ??
                   Assembly.Load(assemblyName);
        }

        private static string ReadRepoFile(string relativePath)
        {
            var fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            Assert.That(File.Exists(fullPath), Is.True, $"Missing file at '{fullPath}'.");
            return File.ReadAllText(fullPath);
        }

        private static string ReadRepoFilesUnder(string relativeDirectory)
        {
            var fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativeDirectory));
            Assert.That(Directory.Exists(fullPath), Is.True, $"Missing directory at '{fullPath}'.");
            return string.Join(
                "\n",
                Directory.GetFiles(fullPath, "*.cs", SearchOption.AllDirectories)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .Select(File.ReadAllText));
        }
    }

    internal static class GameplayAudioGovernanceAssertions
    {
        private const string ExpansionRemediationMessage =
            "Gameplay audio semantic expansion requires governance, docs, and test updates in the same change.";

        private static readonly GameplayAudioSemanticId[] ExpectedRequiredHostOneShotV1 =
        {
            GameplayAudioSemanticId.PlayerDamage,
            GameplayAudioSemanticId.EnemyDamage,
            GameplayAudioSemanticId.EntityExitItemConsume,
            GameplayAudioSemanticId.EntityExitBoxDestroy,
            GameplayAudioSemanticId.EntityExitEnemyDeath,
            GameplayAudioSemanticId.EntityExitOutOfBounds,
        };

        public static string ExpansionGuidance => ExpansionRemediationMessage;

        public static void AssertExactRequiredHostOneShotSet(IReadOnlyList<GameplayAudioSemanticId> actual)
        {
            Assert.That(
                actual.ToArray(),
                Is.EqualTo(ExpectedRequiredHostOneShotV1),
                $"Approved v1 gameplay host one-shot set drifted. {ExpansionRemediationMessage}");
        }

        public static void AssertOnlyApprovedHostFamilies(IEnumerable<GameplayAudioSemanticId> semanticIds, string subject)
        {
            var rejected = semanticIds
                .Distinct()
                .Where(semanticId => !GameplayAudioSemanticCatalog.IsAllowedInHostOneShotV1(semanticId))
                .Select(semanticId => $"{GameplayAudioSemanticCatalog.Format(semanticId)} ({GameplayAudioSemanticCatalog.GetFamily(semanticId)})")
                .ToArray();

            Assert.That(
                rejected,
                Is.Empty,
                $"{subject} must stay within approved gameplay host one-shot families. {ExpansionRemediationMessage}");
        }
    }
}
