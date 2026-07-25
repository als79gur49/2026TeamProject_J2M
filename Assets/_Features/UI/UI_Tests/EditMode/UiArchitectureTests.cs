using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.UIAccess.Contracts;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.HUD;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using Game.Shared.Display;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.UI.Tests
{
    public sealed class UiArchitectureTests
    {
        [Test]
        public void OnlyCompositionUiAssemblyReferencesGameplayHostAssembly()
        {
            var hostAssemblyName = typeof(GameplaySceneHost).Assembly.GetName().Name;
            var runtimeUiAssemblies = GetRuntimeUiAssemblies();

            foreach (var assembly in runtimeUiAssemblies.Where(assembly => assembly != typeof(GameplayUiFlowInstaller).Assembly))
            {
                var references = assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
                Assert.That(references, Does.Not.Contain(hostAssemblyName), assembly.GetName().Name);
            }

            var compositionReferences = typeof(GameplayUiFlowInstaller).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            Assert.That(compositionReferences, Does.Contain(hostAssemblyName));
        }

        [Test]
        public void OnlyApplicationAndCompositionUiAssembliesReferenceGameplayUiAccessAssembly()
        {
            var uiAccessAssemblyName = typeof(IGameplayQueryFacade).Assembly.GetName().Name;
            var applicationAssembly = typeof(HUDRootPresenter).Assembly;
            var compositionAssembly = typeof(GameplayUiFlowInstaller).Assembly;

            foreach (var assembly in GetRuntimeUiAssemblies().Where(assembly => assembly != applicationAssembly && assembly != compositionAssembly))
            {
                var references = assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
                Assert.That(references, Does.Not.Contain(uiAccessAssemblyName), assembly.GetName().Name);
            }

            var applicationReferences = applicationAssembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            Assert.That(applicationReferences, Does.Contain(uiAccessAssemblyName));

            var compositionReferences = compositionAssembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            Assert.That(compositionReferences, Does.Contain(uiAccessAssemblyName));
        }

        [Test]
        public void LegacyDeveloperCommandAccess_IsRemoved()
        {
            var applicationSource = ReadRepoFile("Assets/_Features/UI/UI_Application/Runtime/GameplayUiFlowPorts.cs");
            var installerSource = ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs");
            var popupFactorySource = ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/GameplayPopupRuntimeFactory.cs");
            var removedAccessType = "Debug" + "CommandAccess";
            var removedBuildGate = "Debug" + "CommandBuildGate";

            Assert.That(applicationSource, Does.Not.Contain(removedAccessType));
            Assert.That(installerSource, Does.Not.Contain(removedAccessType));
            Assert.That(popupFactorySource, Does.Not.Contain(removedAccessType));
            Assert.That(installerSource, Does.Not.Contain(removedBuildGate));
            Assert.That(popupFactorySource, Does.Not.Contain(removedBuildGate));
            Assert.That(installerSource, Does.Not.Contain("SceneManager.LoadScene"));
            Assert.That(popupFactorySource, Does.Not.Contain("SceneManager.LoadScene"));
        }

        [Test]
        public void DemoStageControlPanelToggle_RemainsAfterDiagnosticsKeysAreRemoved()
        {
            var installerSource = ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs");
            var removedToggle = "TryToggle" + "Debug" + "CommandsPopup();";

            Assert.That(installerSource, Does.Not.Contain("WasF3PressedThisFrame()"));
            Assert.That(installerSource, Does.Not.Contain("WasF4PressedThisFrame()"));
            Assert.That(installerSource, Does.Not.Contain("ToggleVisibility();"));
            Assert.That(installerSource, Does.Not.Contain("ToggleExpanded();"));
            Assert.That(installerSource, Does.Contain("WasF10PressedThisFrame()"));
            Assert.That(installerSource, Does.Contain("TryToggleDemoStageControlPanel()"));
            Assert.That(installerSource, Does.Not.Contain(removedToggle));
        }

        [Test]
        public void GameplayUiFlowInstaller_F10_OpensOnlyDemoStageControl()
        {
            var installerSource = ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs");
            var removedPopupId = "PopupId." + "Debug" + "Commands";
            var removedRequest = "Request" + "Debug" + "CommandsPopup";

            Assert.That(installerSource, Does.Contain("WasDemoStageControlOpenKeyPressed() && TryToggleDemoStageControlPanel()"));
            Assert.That(installerSource, Does.Contain("KeyboardBridge.WasF10PressedThisFrame()"));
            Assert.That(installerSource, Does.Contain("Coordinator.RequestDemoStageControlPopup"));
            Assert.That(installerSource, Does.Not.Contain(removedPopupId));
            Assert.That(installerSource, Does.Not.Contain(removedRequest));
        }

        [Test]
        public void DemoStageControl_SettingsDisabled_OpenKeyNoOp()
        {
            var installerSource = ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs");

            Assert.That(installerSource, Does.Contain("!_demoStageControlSettings.Enabled"));
            Assert.That(installerSource, Does.Contain("return false;"));
            Assert.That(installerSource, Does.Contain("return settings.OpenKey == DemoStageControlOpenKey.BackQuote"));
        }

        [Test]
        public void DemoStageControl_DoesNotUseLegacyDeveloperBuildGate()
        {
            var installerSource = ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs");
            var hostFactorySource = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayHostRuntimeFactory.cs");
            var removedBuildGate = "Debug" + "CommandBuildGate";

            Assert.That(installerSource, Does.Not.Contain(removedBuildGate));
            Assert.That(hostFactorySource, Does.Not.Contain(removedBuildGate));
        }

        [Test]
        public void PopupCatalog_DoesNotContainLegacyDeveloperPopup()
        {
            var popupIdSource = ReadRepoFile("Assets/_Features/UI/UI_Flow/Runtime/PopupId.cs");
            var popupFactorySource = ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/GameplayPopupRuntimeFactory.cs");
            var popupCatalogAsset = ReadRepoFile("Assets/_Features/UI/UI_Popups/Prefabs/GameplayPopupPrefabCatalog.asset");
            var removedPopupName = "Debug" + "Commands";

            Assert.That(popupIdSource, Does.Not.Contain(removedPopupName));
            Assert.That(popupFactorySource, Does.Not.Contain(removedPopupName));
            Assert.That(popupCatalogAsset, Does.Not.Contain(removedPopupName));
        }

        [Test]
        public void ConfirmPopupProductionTypography_IsCompositionOwnedAndLifetimeDisposed()
        {
            var popupFactorySource = ReadRepoFile(
                "Assets/_Features/UI/UI_Composition/Runtime/GameplayPopupRuntimeFactory.cs");
            var presenterSource = ReadRepoFile(
                "Assets/_Features/UI/UI_Application/Runtime/PopupPresenters.cs");

            Assert.That(popupFactorySource, Does.Contain("ConfirmPopupProductionLocalizationComposer.Bind("));
            Assert.That(popupFactorySource, Does.Contain("typographyBindings.Dispose();"));
            Assert.That(popupFactorySource, Does.Contain("_typographyTheme"));
            Assert.That(popupFactorySource, Does.Not.Contain("TypographyPreview"));
            Assert.That(presenterSource, Does.Not.Contain("GameplayUiTypographyTheme"));
            Assert.That(presenterSource, Does.Not.Contain("TypographyBinding"));
        }

        [Test]
        public void PresenterOrchestrationTypes_RemainOwnedByApplicationAssembly()
        {
            var applicationAssembly = typeof(HUDRootPresenter).Assembly;
            var runtimeUiAssemblies = GetRuntimeUiAssemblies();

            var misplacedPresenterTypes = runtimeUiAssemblies
                .Where(assembly => assembly != applicationAssembly)
                .SelectMany(assembly => assembly.GetExportedTypes())
                .Where(type => !type.IsNested && type.Name.EndsWith("Presenter", StringComparison.Ordinal))
                .Select(type => type.FullName)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(misplacedPresenterTypes, Is.Empty);

            var representativePresenterAssemblies = new[]
            {
                typeof(HUDRootPresenter).Assembly,
                typeof(StageInfoPresenter).Assembly,
                typeof(ObjectiveHudPresenter).Assembly,
                typeof(PlayerStatusPresenter).Assembly,
                typeof(SettingsScreenPresenter).Assembly,
                typeof(StageResultScreenPresenter).Assembly,
                typeof(PausePopupPresenter).Assembly,
                typeof(ConfirmPopupPresenter).Assembly,
            }.Distinct().ToArray();

            Assert.That(representativePresenterAssemblies, Is.EqualTo(new[] { applicationAssembly }));
        }

        [Test]
        public void OnlyCompositionUiAssemblyReferencesSharedAudioAssembly()
        {
            var sharedAudioAssemblyName = typeof(Game.Shared.Audio.IAudioService).Assembly.GetName().Name;
            var compositionAssembly = typeof(GameplayUiFlowInstaller).Assembly;

            foreach (var assembly in GetRuntimeUiAssemblies().Where(assembly => assembly != compositionAssembly))
            {
                var references = assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
                Assert.That(references, Does.Not.Contain(sharedAudioAssemblyName), assembly.GetName().Name);
            }

            var compositionReferences = compositionAssembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            Assert.That(compositionReferences, Does.Contain(sharedAudioAssemblyName));
        }

        [Test]
        public void OnlyCompositionUiAssemblyReferencesSharedDisplayAssembly()
        {
            var sharedDisplayAssemblyName = typeof(IDisplaySettingsService).Assembly.GetName().Name;
            var compositionAssembly = typeof(GameplayUiFlowInstaller).Assembly;

            foreach (var assembly in GetRuntimeUiAssemblies().Where(assembly => assembly != compositionAssembly))
            {
                var references = assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
                Assert.That(references, Does.Not.Contain(sharedDisplayAssemblyName), assembly.GetName().Name);
            }

            var compositionReferences = compositionAssembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            Assert.That(compositionReferences, Does.Contain(sharedDisplayAssemblyName));
        }

        [Test]
        public void ApplicationUiAssembly_DoesNotReferenceGameplayAssembly()
        {
            var gameplayAssemblyName = typeof(WorldState).Assembly.GetName().Name;
            var references = typeof(HUDRootPresenter).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            Assert.That(references, Does.Not.Contain(gameplayAssemblyName));
        }

        [Test]
        public void ApplicationUiAssembly_OnlyGameplayUiPresentationSourceDependsOnGameplayPresentationFeed()
        {
            var applicationAssembly = typeof(HUDRootPresenter).Assembly;
            var feedType = typeof(IGameplayPresentationFeed);

            var dependentTypes = applicationAssembly
                .GetTypes()
                .Where(type => !type.IsNested && TypeDependsOn(type, feedType))
                .Select(type => type.FullName)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(
                dependentTypes,
                Is.EqualTo(new[]
                {
                    typeof(GameplayUiPresentationSource).FullName,
                }));
        }

        [Test]
        public void PresenterTypes_DoNotDependOnRawGameplayPresentationContracts()
        {
            var forbiddenTypes = new[]
            {
                typeof(IGameplayPresentationFeed),
                typeof(GameplayPresentationFrame),
                typeof(GameplayPresentationState),
            };
            var presenterTypes = new[]
            {
                typeof(HUDRootPresenter),
                typeof(StageInfoPresenter),
                typeof(ObjectiveHudPresenter),
                typeof(PlayerStatusPresenter),
                typeof(SettingsScreenPresenter),
            };

            foreach (var presenterType in presenterTypes)
            {
                foreach (var forbiddenType in forbiddenTypes)
                {
                    Assert.That(
                        TypeDependsOn(presenterType, forbiddenType),
                        Is.False,
                        $"{presenterType.FullName} depends on {forbiddenType.FullName}");
                }
            }
        }

        [Test]
        public void ObjectiveUiCode_DoesNotReferenceForbiddenObjectiveSources()
        {
            var objectiveUiSourcePaths = new[]
            {
                "Assets/_Features/UI/UI_Application/Runtime/ObjectiveHudPresenter.cs",
                "Assets/_Features/UI/UI_HUD/Runtime/ObjectiveHudView.cs",
                "Assets/_Features/UI/UI_HUD/Runtime/ObjectiveHudViewModel.cs",
            };
            var forbiddenTokens = new[]
            {
                "StageConditionAsset",
                "StageDefinition",
                "StageContentEntry",
                "StageLaunchContextStore",
                "SaveSlotStore",
                ".Details",
                "ConditionStatus.Details",
            };

            foreach (var sourcePath in objectiveUiSourcePaths)
            {
                var source = File.ReadAllText(sourcePath);
                foreach (var forbiddenToken in forbiddenTokens)
                {
                    Assert.That(source, Does.Not.Contain(forbiddenToken), $"{sourcePath} contains {forbiddenToken}");
                }
            }
        }

        [Test]
        public void FeatureUiAssemblies_DoNotDefineRuntimeOnGuiMethods()
        {
            var offendingMethods = GetRuntimeUiAssemblies()
                .SelectMany(assembly => assembly.GetExportedTypes())
                .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Where(method => method.Name == "OnGUI")
                    .Select(method => $"{type.FullName}.{method.Name}"))
                .ToArray();

            Assert.That(offendingMethods, Is.Empty);
        }

        [Test]
        public void HudScreenAndPopupAssemblies_DoNotExposeForbiddenGameplayTypes()
        {
            var forbiddenTypes = new HashSet<Type>
            {
                typeof(WorldState),
                typeof(TickResult),
                typeof(TickRunner),
                typeof(GameplayInputHost),
                typeof(GameplayTickViewPresenter),
            };

            var assemblies = new[]
            {
                typeof(HUDRootView).Assembly,
                typeof(SettingsScreenView).Assembly,
                typeof(PausePopupView).Assembly,
            };

            var leakedTypes = assemblies
                .Distinct()
                .SelectMany(assembly => assembly.GetExportedTypes())
                .SelectMany(GetPublicSurfaceTypes)
                .Select(NormalizeType)
                .Where(type => type != null &&
                               (forbiddenTypes.Contains(type) ||
                                (type.Namespace != null && type.Namespace.StartsWith("Game.Feature.Gameplay", StringComparison.Ordinal))))
                .Distinct()
                .ToArray();

            Assert.That(leakedTypes, Is.Empty);
        }

        [Test]
        public void GameplayQueryFacade_Surface_RemainsBoundedToStageOneReaders()
        {
            var propertyNames = typeof(IGameplayQueryFacade)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(propertyNames, Is.EqualTo(new[] { "Objectives", "PlayerHud", "Session", "Stage", "SurfaceButtonRemainders" }));
        }

        [Test]
        public void HUDRootPresenter_DoesNotDependOnGameplayCommandGateway()
        {
            Assert.That(
                TypeDependsOn(typeof(HUDRootPresenter), typeof(IGameplayCommandGateway)),
                Is.False);
        }

        [Test]
        public void HUDRootPresenter_PublicSurface_RemainsBoundedToMappedFanOut()
        {
            var publicPropertyNames = typeof(HUDRootPresenter)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();
            Assert.That(publicPropertyNames, Is.EqualTo(new[] { "ViewModel" }));

            var publicMethodNames = typeof(HUDRootPresenter)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .Select(method => method.Name)
                .OrderBy(name => name)
                .ToArray();
            Assert.That(publicMethodNames, Is.EqualTo(new[] { "Dispose" }));

            var constructors = typeof(HUDRootPresenter).GetConstructors(BindingFlags.Instance | BindingFlags.Public);
            Assert.That(constructors, Has.Length.EqualTo(2));
            var fullConstructor = constructors
                .First(constructor => constructor.GetParameters().Length == 6);
            Assert.That(
                fullConstructor.GetParameters().Select(parameter => parameter.ParameterType).ToArray(),
                Is.EqualTo(new[]
                {
                    typeof(IGameplayUiPresentationSource),
                    typeof(StageInfoPresenter),
                    typeof(ObjectiveHudPresenter),
                    typeof(ChancePanelPresenter),
                    typeof(SurfaceBeltIndicatorPresenter),
                    typeof(PlayerStatusPresenter),
                }));

            var forbiddenTypes = new[]
            {
                typeof(IGameplayCommandGateway),
                typeof(IGameplayQueryFacade),
                typeof(ScreenController),
                typeof(PopupController),
                typeof(UIFlowCoordinator),
                typeof(ObjectiveHudViewModel),
                typeof(PlayerStatusViewModel),
            };

            foreach (var forbiddenType in forbiddenTypes)
            {
                Assert.That(
                    TypeDependsOn(typeof(HUDRootPresenter), forbiddenType),
                    Is.False,
                    $"{typeof(HUDRootPresenter).FullName} depends on {forbiddenType.FullName}");
            }
        }

        [Test]
        public void HUDRootPresenter_DoesNotExposeActionCommandEntryPoints()
        {
            var methodNames = typeof(HUDRootPresenter)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(method => method.Name)
                .ToArray();

            Assert.That(methodNames, Does.Not.Contain("RequestSlot"));
            Assert.That(methodNames, Does.Not.Contain("RequestMoveUp"));
            Assert.That(methodNames.Any(name => name.StartsWith("Request", StringComparison.Ordinal)), Is.False);
        }

        [Test]
        public void HUDRootViewModel_PublicProperties_RemainShellOnly()
        {
            var propertyNames = typeof(HUDRootViewModel)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(propertyNames, Is.EqualTo(new[] { "IsDimmed", "IsGameplayReadOnly", "IsPauseButtonEnabled" }));
        }

        [Test]
        public void UIRecoveryCooldownSlice_PublicSurface_RemainsMinimal()
        {
            var propertyNames = typeof(UIRecoveryCooldownSlice)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(propertyNames, Is.EqualTo(new[]
            {
                "ActionKind",
                "RemainingRecoveryTicks",
                "TotalRecoveryTicks",
            }));
        }

        [Test]
        public void HUDController_DoesNotDependOnMappedSnapshotOrFlowBlockContracts()
        {
            var forbiddenTypes = new[]
            {
                typeof(UIPresentationSnapshot),
                typeof(IGameplayUiPresentationSource),
                typeof(IGameplayPresentationFeed),
                typeof(UIBlockSnapshot),
            };

            foreach (var forbiddenType in forbiddenTypes)
            {
                Assert.That(
                    TypeDependsOn(typeof(HUDController), forbiddenType),
                    Is.False,
                    $"{typeof(HUDController).FullName} depends on {forbiddenType.FullName}");
            }

            Assert.That(
                typeof(HUDController).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .Select(method => method.Name),
                Does.Not.Contain("ApplyBlockSnapshot"));
        }

        [Test]
        public void HUDController_PublicSurface_RemainsLifecycleAndBindingOnly()
        {
            var propertyNames = typeof(HUDController)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();
            Assert.That(
                propertyNames,
                Is.EqualTo(new[]
                {
                    "ChancePanelViewModel",
                    "IsGameplayReadOnly",
                    "ObjectiveHudViewModel",
                    "PlayerStatusViewModel",
                    "RootViewModel",
                    "StageInfoViewModel",
                    "SurfaceBeltViewModel",
                }));

            var methodNames = typeof(HUDController)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .Select(method => method.Name)
                .OrderBy(name => name)
                .ToArray();
            Assert.That(methodNames, Is.EqualTo(new[] { "AttachView", "Dispose" }));
        }

        [Test]
        public void HudPresenters_DoNotDependOnFlowPolicyTypes()
        {
            var presenterTypes = new[]
            {
                typeof(HUDRootPresenter),
                typeof(StageInfoPresenter),
                typeof(ObjectiveHudPresenter),
                typeof(ChancePanelPresenter),
                typeof(SurfaceBeltIndicatorPresenter),
                typeof(PlayerStatusPresenter),
            };
            var forbiddenTypes = new[]
            {
                typeof(UIFlowCoordinator),
                typeof(UIBlockPolicy),
                typeof(ScreenController),
                typeof(PopupController),
            };

            foreach (var presenterType in presenterTypes)
            {
                foreach (var forbiddenType in forbiddenTypes)
                {
                    Assert.That(
                        TypeDependsOn(presenterType, forbiddenType),
                        Is.False,
                        $"{presenterType.FullName} depends on {forbiddenType.FullName}");
                }
            }
        }

        [Test]
        public void HudViews_BindOnlyLocalViewModels()
        {
            AssertViewBindSignature(typeof(HUDRootView), typeof(HUDRootViewModel));
            AssertViewBindSignature(typeof(ObjectiveHudView), typeof(ObjectiveHudViewModel));
            AssertViewBindSignature(typeof(ChancePanelView), typeof(ChancePanelViewModel));
            AssertViewBindSignature(typeof(SurfaceBeltIndicatorView), typeof(SurfaceBeltViewModel));
            AssertViewBindSignature(typeof(PlayerStatusView), typeof(PlayerStatusViewModel));
        }

        [Test]
        public void SurfaceBeltViewModels_DoNotExposeUnityRenderingTypes()
        {
            var forbiddenTypes = new[]
            {
                typeof(Color),
                typeof(Sprite),
                typeof(RectTransform),
            };
            var modelTypes = new[]
            {
                typeof(SurfaceBeltViewModel),
                typeof(SurfaceBeltCellViewModel),
                typeof(SurfaceBeltButtonRemainderViewModel),
            };

            foreach (var modelType in modelTypes)
            {
                foreach (var memberType in GetPublicMemberTypes(modelType))
                {
                    Assert.That(
                        forbiddenTypes.Contains(memberType),
                        Is.False,
                        $"{modelType.FullName} exposes {memberType.FullName}");
                }
            }
        }

        [Test]
        public void HudViews_NoLongerExposeLegacyRuntimeConfigureEntryPoints()
        {
            var hudViewTypes = new[]
            {
                typeof(HUDRootView),
                typeof(ObjectiveHudView),
                typeof(ChancePanelView),
                typeof(SurfaceBeltIndicatorView),
                typeof(PlayerStatusView),
            };

            foreach (var hudViewType in hudViewTypes)
            {
                Assert.That(
                    hudViewType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                        .Select(method => method.Name),
                    Does.Not.Contain("Configure"),
                    hudViewType.FullName);
            }
        }

        [Test]
        public void HudViews_DoNotDependOnFlowGameplayAccessOrDiagnosticsTypes()
        {
            var hudViewTypes = new[]
            {
                typeof(HUDRootView),
                typeof(ObjectiveHudView),
                typeof(ChancePanelView),
                typeof(SurfaceBeltIndicatorView),
                typeof(PlayerStatusView),
            };
            var forbiddenTypes = new[]
            {
                typeof(IGameplayQueryFacade),
                typeof(IGameplayCommandGateway),
                typeof(IGameplayUiPresentationSource),
                typeof(UIFlowCoordinator),
                typeof(ScreenController),
                typeof(PopupController),
            };

            foreach (var hudViewType in hudViewTypes)
            {
                foreach (var forbiddenType in forbiddenTypes)
                {
                    Assert.That(
                        TypeDependsOn(hudViewType, forbiddenType),
                        Is.False,
                        $"{hudViewType.FullName} depends on {forbiddenType.FullName}");
                }
            }
        }

        [Test]
        public void PopupViews_BindOnlyLocalPopupViewModels()
        {
            AssertViewBindSignature(typeof(PausePopupView), typeof(PausePopupViewModel));
            AssertViewBindSignature(typeof(ConfirmPopupView), typeof(ConfirmPopupViewModel));
            Assert.That(TypeDependsOn(typeof(PopupLayerView), typeof(UIPresentationSnapshot)), Is.False);
        }

        [Test]
        public void PopupViews_NoLongerExposeLegacyRuntimeConfigureEntryPoints()
        {
            var popupViewTypes = new[]
            {
                typeof(PausePopupView),
                typeof(ConfirmPopupView),
            };

            foreach (var popupViewType in popupViewTypes)
            {
                Assert.That(
                    popupViewType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                        .Select(method => method.Name),
                    Does.Not.Contain("Configure"),
                    popupViewType.FullName);
            }
        }

        [Test]
        public void PopupViews_DoNotDependOnFlowGameplayAccessOrDiagnosticsTypes()
        {
            var popupViewTypes = new[]
            {
                typeof(PausePopupView),
                typeof(ConfirmPopupView),
            };
            var forbiddenTypes = new[]
            {
                typeof(IGameplayQueryFacade),
                typeof(IGameplayCommandGateway),
                typeof(IGameplayUiPresentationSource),
                typeof(UIFlowCoordinator),
                typeof(ScreenController),
                typeof(PopupController),
            };

            foreach (var popupViewType in popupViewTypes)
            {
                foreach (var forbiddenType in forbiddenTypes)
                {
                    Assert.That(
                        TypeDependsOn(popupViewType, forbiddenType),
                        Is.False,
                        $"{popupViewType.FullName} depends on {forbiddenType.FullName}");
                }
            }
        }

        [Test]
        public void PopupPrefabCatalog_PublicSurface_RemainsFixedShapePopupOnly()
        {
            Assert.That(
                GetPublicPropertyNames(typeof(PopupPrefabCatalog)),
                Is.EqualTo(new[]
                {
                    "ConfirmPrefab",
                    "PausePrefab",
                }));
            Assert.That(GetPublicEventNames(typeof(PopupPrefabCatalog)), Is.Empty);
            Assert.That(GetPublicMethodSignatures(typeof(PopupPrefabCatalog)), Is.Empty);
        }

        [Test]
        public void ScreenPrefabCatalog_PublicSurface_RemainsFixedShapeScreenOnly()
        {
            Assert.That(
                GetPublicPropertyNames(typeof(ScreenPrefabCatalog)),
                Is.EqualTo(new[]
                {
                    "GameClearPrefab",
                    "LevelFailedPrefab",
                    "SettingsPrefab",
                    "StageResultPrefab",
                }));
            Assert.That(GetPublicEventNames(typeof(ScreenPrefabCatalog)), Is.Empty);
            Assert.That(GetPublicMethodSignatures(typeof(ScreenPrefabCatalog)), Is.Empty);
        }

        [Test]
        public void PopupPresenters_DoNotDependOnFlowOrRawGameplayPresentationTypes()
        {
            var presenterTypes = new[]
            {
                typeof(PausePopupPresenter),
                typeof(ConfirmPopupPresenter),
            };
            var forbiddenTypes = new[]
            {
                typeof(UIFlowCoordinator),
                typeof(PopupController),
                typeof(UIBlockPolicy),
                typeof(IGameplayPresentationFeed),
                typeof(GameplayPresentationFrame),
                typeof(GameplayPresentationState),
            };

            foreach (var presenterType in presenterTypes)
            {
                foreach (var forbiddenType in forbiddenTypes)
                {
                    Assert.That(
                        TypeDependsOn(presenterType, forbiddenType),
                        Is.False,
                        $"{presenterType.FullName} depends on {forbiddenType.FullName}");
                }
            }
        }

        [Test]
        public void UIFlowCoordinator_DoesNotDependOnPopupPresentersOrViewModels()
        {
            var forbiddenTypes = new[]
            {
                typeof(PausePopupPresenter),
                typeof(ConfirmPopupPresenter),
                typeof(PausePopupViewModel),
                typeof(ConfirmPopupViewModel),
                typeof(PausePopupView),
                typeof(ConfirmPopupView),
            };

            foreach (var forbiddenType in forbiddenTypes)
            {
                Assert.That(
                    TypeDependsOn(typeof(UIFlowCoordinator), forbiddenType),
                    Is.False,
                    $"{typeof(UIFlowCoordinator).FullName} depends on {forbiddenType.FullName}");
            }
        }

        [Test]
        public void UIFlowCoordinator_PublicSurface_RemainsRoutingOnly()
        {
            Assert.That(
                GetPublicPropertyNames(typeof(UIFlowCoordinator)),
                Is.EqualTo(new[] { "CurrentBlockSnapshot" }));
            Assert.That(GetPublicEventNames(typeof(UIFlowCoordinator)), Is.Empty);
            Assert.That(
                GetPublicMethodSignatures(typeof(UIFlowCoordinator)),
                Is.EqualTo(new[]
                {
                    "Dispose()",
                    "HandleBackRequested()",
                    "HandlePopupBackdropClicked()",
                    "HandleScreenActionRequested(ScreenAction)",
                    "Initialize()",
                    "OpenSettingsScreen()",
                    "RequestConfirmPopup(ConfirmPopupPayload, Action<PopupCompletion>)",
                    "RequestDemoStageControlPopup(IPopupPayload)",
                    "RequestPausePopup()",
                }));
            Assert.That(
                GetConstructorSignatures(typeof(UIFlowCoordinator)),
                Is.EqualTo(new[]
                {
                    "UIFlowCoordinator(ScreenController, PopupController, UIBlockPolicy, IUiFlowPauseService, IGameplayUiPresentationSource, IUiAudioPort, IStageLaunchRouter, IMainMenuReturnRouter)",
                }));
        }

        [Test]
        public void ScreenController_PublicSurface_RemainsBoundedToRuntimeOwnership()
        {
            Assert.That(
                GetPublicPropertyNames(typeof(ScreenController)),
                Is.EqualTo(new[]
                {
                    "BackStackCount",
                    "CanPop",
                    "CurrentEntry",
                    "CurrentScreenId",
                }));
            Assert.That(
                GetPublicEventNames(typeof(ScreenController)),
                Is.EqualTo(new[] { "ActionRequested", "ScreenTransitioned", "StateChanged" }));
            Assert.That(
                GetPublicMethodSignatures(typeof(ScreenController)),
                Is.EqualTo(new[]
                {
                    "Clear()",
                    "Dispose()",
                    "HandleBackRequested()",
                    "Pop()",
                    "PopTo(ScreenId)",
                    "Push(ScreenRequest)",
                    "Replace(ScreenRequest)",
                    "SetRoot(ScreenRequest)",
                    "Show(ScreenRequest)",
                }));
            Assert.That(
                GetConstructorSignatures(typeof(ScreenController)),
                Is.EqualTo(new[] { "ScreenController(IScreenRuntimeFactory)" }));
        }

        [Test]
        public void PopupController_PublicSurface_RemainsBoundedToStackOwnership()
        {
            Assert.That(
                GetPublicPropertyNames(typeof(PopupController)),
                Is.EqualTo(new[]
                {
                    "CanPop",
                    "PopupCount",
                    "TopPopup",
                }));
            Assert.That(GetPublicEventNames(typeof(PopupController)), Is.EqualTo(new[] { "PopupCompleted", "PopupOpened", "StateChanged" }));
            Assert.That(
                GetPublicMethodSignatures(typeof(PopupController)),
                Is.EqualTo(new[]
                {
                    "Close(PopupInstanceId, PopupCloseReason)",
                    "Close(PopupInstanceId, PopupCloseReason, PopupCompletionKind)",
                    "CloseAll(PopupCloseReason)",
                    "CloseTop(PopupCloseReason)",
                    "CloseTop(PopupCloseReason, PopupCompletionKind)",
                    "Contains(PopupId)",
                    "Dispose()",
                    "HandleBackdropClicked()",
                    "HandleBackRequested()",
                    "PopTop(out PopupEntry)",
                    "Push(PopupRequest, out PopupInstanceId)",
                }));
            Assert.That(
                GetConstructorSignatures(typeof(PopupController)),
                Is.EqualTo(new[] { "PopupController(IPopupRuntimeFactory)" }));
        }

        [Test]
        public void UIBlockPolicy_PublicSurface_RemainsSinglePolicyEntryPoint()
        {
            Assert.That(GetPublicPropertyNames(typeof(UIBlockPolicy)), Is.Empty);
            Assert.That(GetPublicEventNames(typeof(UIBlockPolicy)), Is.Empty);
            Assert.That(
                GetPublicMethodSignatures(typeof(UIBlockPolicy)),
                Is.EqualTo(new[] { "Evaluate(UIFlowStateSnapshot)" }));
            Assert.That(
                GetConstructorSignatures(typeof(UIBlockPolicy)),
                Is.EqualTo(new[] { "UIBlockPolicy()" }));
        }

        [Test]
        public void UIBlockSnapshot_PublicSurface_RemainsPolicyOnly()
        {
            var propertyNames = typeof(UIBlockSnapshot)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();

            Assert.That(
                propertyNames,
                Is.EqualTo(new[]
                {
                    "BlocksHudInteraction",
                    "BlocksLowerLayerPointer",
                    "BlocksScreenInteraction",
                    "BlocksUiGameplayInput",
                    "PopupBackdropMode",
                    "PopupConsumesBack",
                    "ShowsPopupDim",
                }));
        }

        [Test]
        public void SettingsScreenPresenter_PublicSurface_RemainsBoundedToScreenState()
        {
            Assert.That(
                GetPublicPropertyNames(typeof(SettingsScreenPresenter)),
                Is.EqualTo(new[] { "AudioPresenter", "DisplayPresenter", "InputPresenter", "ViewModel" }));
            Assert.That(GetPublicEventNames(typeof(SettingsScreenPresenter)), Is.Empty);
            Assert.That(
                GetPublicMethodSignatures(typeof(SettingsScreenPresenter)),
                Is.EqualTo(new[]
                {
                    "Apply(SettingsScreenPayload, Double)",
                    "Dispose()",
                    "RefreshLocalization()",
                    "SelectNextLocale()",
                    "SelectSection(SettingsSectionId)",
                }));
            Assert.That(
                GetConstructorSignatures(typeof(SettingsScreenPresenter)),
                Is.EqualTo(new[]
                {
                    "SettingsScreenPresenter(IAudioSettingsPort, IDisplaySettingsPort)",
                    "SettingsScreenPresenter(IAudioSettingsPort, IDisplaySettingsPort, IKeyboardBindingSettingsPort)",
                    "SettingsScreenPresenter(IAudioSettingsPort, IDisplaySettingsPort, IKeyboardBindingSettingsPort, ILocalizedTextResolver)",
                    "SettingsScreenPresenter(IAudioSettingsPort, IDisplaySettingsPort, IKeyboardBindingSettingsPort, ILocalizedTextResolver, IUiLocaleSelectionPort)",
                }));
        }

        [Test]
        public void SettingsScreenPresenter_DoesNotDependOnFlowOrPopupOwnershipTypes()
        {
            var forbiddenTypes = new[]
            {
                typeof(UIFlowCoordinator),
                typeof(ScreenController),
                typeof(PopupController),
                typeof(PopupLayerView),
                typeof(PopupRequest),
                typeof(PopupId),
                typeof(IPopupRuntime),
                typeof(IPopupRuntimeFactory),
            };

            foreach (var forbiddenType in forbiddenTypes)
            {
                Assert.That(
                    TypeDependsOn(typeof(SettingsScreenPresenter), forbiddenType),
                    Is.False,
                    $"{typeof(SettingsScreenPresenter).FullName} depends on {forbiddenType.FullName}");
            }
        }

        [Test]
        public void UiNavigationInputRouter_PublicSurface_UsesOnlyCanonicalTargetResolver()
        {
            Assert.That(GetPublicPropertyNames(typeof(UiNavigationInputRouter)), Is.Empty);
            Assert.That(GetPublicEventNames(typeof(UiNavigationInputRouter)), Is.Empty);
            Assert.That(
                GetPublicMethodSignatures(typeof(UiNavigationInputRouter)),
                Is.EqualTo(new[]
                {
                    "DispatchCancel()",
                    "DispatchNavigate(UiNavigationCommand)",
                    "DispatchSubmit()",
                    "Initialize(InputActionAsset, IUiNavigationTargetResolver, Func<Boolean>, Func<Boolean>, IUiAudioPort)",
                }));

            var forbiddenTypes = new[]
            {
                typeof(PopupController),
                typeof(PopupLayerView),
                typeof(MainMenuScreenView),
            };
            foreach (var forbiddenType in forbiddenTypes)
            {
                Assert.That(
                    TypeDependsOn(typeof(UiNavigationInputRouter), forbiddenType),
                    Is.False,
                    $"{typeof(UiNavigationInputRouter).FullName} depends on {forbiddenType.FullName}");
            }
        }

        [Test]
        public void UiNavigationInputRouter_LegacyConcreteResolverPath_IsAbsent()
        {
            var routerSource = ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/UiNavigationInputRouter.cs");
            var gameplayInstallerSource = ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs");
            var mainMenuInstallerSource = ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs");
            var testSource = ReadRepoFile("Assets/_Features/UI/UI_Tests/EditMode/UiKeyboardNavigationTests.cs");

            Assert.That(routerSource, Does.Contain("IUiNavigationTargetResolver targetResolver"));
            Assert.That(routerSource, Does.Not.Contain("LegacyNavigationTargetResolver"));
            Assert.That(routerSource, Does.Not.Contain("PopupLayerView popupLayerView"));
            Assert.That(routerSource, Does.Not.Contain("MainMenuScreenView mainMenuScreenView"));
            Assert.That(routerSource, Does.Not.Contain("_popupLayerView"));
            Assert.That(routerSource, Does.Not.Contain("_mainMenuScreenView"));

            Assert.That(gameplayInstallerSource, Does.Contain("new UiLayeredNavigationTargetResolver("));
            Assert.That(mainMenuInstallerSource, Does.Contain("new UiLayeredNavigationTargetResolver("));
            Assert.That(gameplayInstallerSource, Does.Not.Contain("_navigationInputRouter.Initialize(\n                ResolveUiInputActions(),\n                PopupController"));
            Assert.That(mainMenuInstallerSource, Does.Not.Contain("_navigationInputRouter.Initialize(\n                _inputActions,\n                PopupController"));
            Assert.That(testSource, Does.Not.Contain("null, null, null"));
            Assert.That(testSource, Does.Not.Contain("CreatePopupLayerHarness"));
        }

        [Test]
        public void SettingsChildPresenters_PublicSurface_RemainsLocalAndBounded()
        {
            Assert.That(
                GetPublicPropertyNames(typeof(SettingsAudioPresenter)),
                Is.EqualTo(new[] { "ViewModel" }));
            Assert.That(GetPublicEventNames(typeof(SettingsAudioPresenter)), Is.Empty);
            Assert.That(
                GetPublicMethodSignatures(typeof(SettingsAudioPresenter)),
                Is.EqualTo(new[]
                {
                    "Apply()",
                    "Flush()",
                    "RefreshLocalization()",
                    "SetMuted(AudioSettingsChannel, Boolean)",
                    "SetVolume(AudioSettingsChannel, Single)",
                }));

            Assert.That(
                GetPublicPropertyNames(typeof(SettingsDisplayPresenter)),
                Is.EqualTo(new[] { "ViewModel" }));
            Assert.That(GetPublicEventNames(typeof(SettingsDisplayPresenter)), Is.Empty);
            Assert.That(
                GetPublicMethodSignatures(typeof(SettingsDisplayPresenter)),
                Is.EqualTo(new[]
                {
                    "Apply(Double)",
                    "Apply(LocalizedTextDescriptor, LocalizedTextDescriptor, LocalizedTextDescriptor, Double)",
                    "ApplyStagedSettings(Double)",
                    "CancelPreview()",
                    "ClearPreviewCountdown()",
                    "ClearTransientDisplayStatus(Double)",
                    "ConfirmPreview()",
                    "RefreshLocalization()",
                    "ResetStagedToCurrent()",
                    "ResyncState(Double)",
                    "SelectNextLocale()",
                    "SetPreviewCountdown(DisplayPreviewCountdownSnapshot)",
                    "StageResolution(Int32)",
                    "StageWindowMode(DisplayWindowMode)",
                }));
        }

        [Test]
        public void ScreenViews_DoNotDependOnFlowGameplayAccessOrDiagnosticsTypes()
        {
            var guardedViewTypes = new[]
            {
                typeof(SettingsScreenView),
                typeof(StageResultScreenView),
                typeof(LevelFailedScreenView),
                typeof(GameClearScreenView),
                typeof(SettingsAudioView),
                typeof(SettingsDisplayView),
            };
            var forbiddenTypes = new[]
            {
                typeof(ScreenController),
                typeof(PopupController),
                typeof(UIFlowCoordinator),
                typeof(ScreenRequest),
                typeof(ScreenAction),
                typeof(PopupRequest),
                typeof(IGameplayQueryFacade),
                typeof(IGameplayCommandGateway),
            };

            foreach (var viewType in guardedViewTypes)
            {
                foreach (var forbiddenType in forbiddenTypes)
                {
                    Assert.That(
                        TypeDependsOn(viewType, forbiddenType),
                        Is.False,
                        $"{viewType.FullName} depends on {forbiddenType.FullName}");
                }
            }
        }

        [Test]
        public void ScreenViews_NoLongerExposeLegacyRuntimeConfigureEntryPoints()
        {
            var guardedViewTypes = new[]
            {
                typeof(SettingsScreenView),
                typeof(StageResultScreenView),
                typeof(LevelFailedScreenView),
                typeof(GameClearScreenView),
                typeof(SettingsAudioView),
                typeof(SettingsDisplayView),
            };

            foreach (var viewType in guardedViewTypes)
            {
                Assert.That(
                    viewType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                        .Select(method => method.Name),
                    Does.Not.Contain("Configure"),
                    viewType.FullName);
            }
        }

        [Test]
        public void ScreenViews_PublicSurface_RemainsLocalIntentOnly()
        {
            var guardedViewTypes = new[]
            {
                typeof(SettingsScreenView),
                typeof(StageResultScreenView),
                typeof(LevelFailedScreenView),
                typeof(GameClearScreenView),
                typeof(SettingsAudioView),
                typeof(SettingsDisplayView),
            };
            var forbiddenSurfaceTypes = new[]
            {
                typeof(ScreenController),
                typeof(PopupController),
                typeof(UIFlowCoordinator),
                typeof(ScreenRequest),
                typeof(ScreenAction),
                typeof(PopupRequest),
                typeof(IGameplayQueryFacade),
                typeof(IGameplayCommandGateway),
            };

            foreach (var viewType in guardedViewTypes)
            {
                var surfacedTypes = GetPublicSurfaceTypes(viewType)
                    .Select(NormalizeType)
                    .Where(type => type != null)
                    .Distinct()
                    .ToArray();

                foreach (var forbiddenSurfaceType in forbiddenSurfaceTypes)
                {
                    Assert.That(
                        surfacedTypes,
                        Has.No.Member(forbiddenSurfaceType),
                        $"{viewType.FullName} surfaces {forbiddenSurfaceType.FullName}");
                }
            }
        }

        [Test]
        public void DiagnosticsOverlayRuntimeFeature_IsRemovedFromCompositionRuntime()
        {
            var diagnosticsTokens = new[]
            {
                "UiArchitectureDiagnostics",
                "DiagnosticsOverlay",
                "DiagnosticsLayer",
                "UiDiagnostics",
                "WasF3PressedThisFrame",
                "WasF4PressedThisFrame",
                "f3Key",
                "f4Key",
            };
            var runtimeFiles = new[]
            {
                "Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs",
                "Assets/_Features/UI/UI_Composition/Runtime/GameplayUiCanvasRootView.cs",
                "Assets/_Features/UI/UI_Composition/Runtime/GameplayUiRootShellValidator.cs",
            };

            foreach (var runtimeFile in runtimeFiles)
            {
                var source = ReadRepoFile(runtimeFile);
                foreach (var token in diagnosticsTokens)
                {
                    Assert.That(source, Does.Not.Contain(token), runtimeFile);
                }
            }
        }

        [Test]
        public void GameplayUiPortsAndFeaturePublicSurfaces_DoNotExposeRemovedDiagnosticsTypes()
        {
            var surfacedTypes = new[]
            {
                typeof(GameplayUiFlowPorts),
                typeof(GameplayUiFlowInstaller),
                typeof(GameplayUiCanvasRootView),
            }
            .SelectMany(GetPublicSurfaceTypes)
            .Select(NormalizeType)
            .Where(type => type != null)
            .Distinct()
            .ToArray();

            Assert.That(surfacedTypes.Select(type => type.Name), Does.Not.Contain("UiArchitectureDiagnosticsTracker"));
            Assert.That(surfacedTypes.Select(type => type.Name), Does.Not.Contain("UiArchitectureDiagnosticsSnapshot"));
            Assert.That(surfacedTypes.Select(type => type.Name), Does.Not.Contain("UiArchitectureDiagnosticsOverlayView"));
        }

        [Test]
        public void UiComposition_DoesNotReadOrWriteGameplayAuthority()
        {
            var forbiddenTokens = new[]
            {
                "WorldState",
                "CreateSnapshot(",
                "TickRunner",
                "TickPipeline",
                "FinalizationBatch",
                "WorldStateWriteContext",
                "IWorldWriteContext",
                "IWorldSnapshotWriter",
                "ProjectedWorld",
                "RecordingFinalizationContext",
            };

            AssertRuntimeSourcesDoNotContain(
                new[] { "Assets/_Features/UI/UI_Composition/Runtime" },
                forbiddenTokens);
        }

        [Test]
        public void UiComposition_MayReferenceGameplaySceneHostOnlyAsCompositionBridge()
        {
            var compositionSources = ReadRuntimeSources(new[] { "Assets/_Features/UI/UI_Composition/Runtime" });
            var installerSource = compositionSources.Single(source => source.RelativePath.EndsWith("GameplayUiFlowInstaller.cs", StringComparison.Ordinal));

            Assert.That(installerSource.Source, Does.Contain("Install(GameplaySceneHost sceneHost)"));
            Assert.That(installerSource.Source, Does.Contain("sceneHost.UiAccess"));
            Assert.That(installerSource.Source, Does.Contain("_sceneHost.InputHost.Actions"));

            var forbiddenAuthorityMemberTokens = new[]
            {
                ".WorldState",
                ".TickRunner",
                ".TickPipeline",
                "CreateSnapshot(",
            };
            foreach (var source in compositionSources)
            {
                foreach (var token in forbiddenAuthorityMemberTokens)
                {
                    Assert.That(source.Source, Does.Not.Contain(token), $"{source.RelativePath} contains {token}");
                }
            }
        }

        [Test]
        public void UiApplicationAndFlow_DoNotReferenceGameplayHostOrSimulationConcrete()
        {
            var forbiddenTokens = new[]
            {
                "GameplaySceneHost",
                "Game.Feature.Gameplay.Host",
                "WorldState",
                "TickRunner",
                "TickPipeline",
                "FinalizationBatch",
            };

            AssertRuntimeSourcesDoNotContain(
                new[]
                {
                    "Assets/_Features/UI/UI_Application/Runtime",
                    "Assets/_Features/UI/UI_Flow/Runtime",
                    "Assets/_Features/UI/UI_HUD/Runtime",
                    "Assets/_Features/UI/UI_Screens/Runtime",
                    "Assets/_Features/UI/UI_Popups/Runtime",
                },
                forbiddenTokens);

            var screensAsmdef = ReadRepoFile("Assets/_Features/UI/UI_Screens/UI.Screens.asmdef");
            Assert.That(screensAsmdef, Does.Not.Contain("Game.Feature.Stages"));

            AssertRuntimeSourcesDoNotContain(
                new[] { "Assets/_Features/UI/UI_Screens/Runtime" },
                new[]
                {
                    "Game.Feature.Stages",
                    "StageNavigationRequest",
                    "SaveSlotData",
                    "SaveSlotStore",
                    "ActiveSlotProvider",
                    "SaveSlotValidationService",
                    "SaveSlotValidationResult",
                    "SaveSlotValidationStatus",
                    "CampaignStageSequenceResolver",
                });
        }

        [Test]
        public void UiApplicationFlowViewModelsAndViews_DoNotReferenceSharedAudioPauseService()
        {
            AssertRuntimeSourcesDoNotContain(
                new[]
                {
                    "Assets/_Features/UI/UI_Application/Runtime",
                    "Assets/_Features/UI/UI_Flow/Runtime",
                    "Assets/_Features/UI/UI_HUD/Runtime",
                    "Assets/_Features/UI/UI_Screens/Runtime",
                    "Assets/_Features/UI/UI_Popups/Runtime",
                },
                new[]
                {
                    "IAudioPlaybackPauseService",
                    "AudioPlaybackPauseGroup",
                    "AudioPauseReason",
                });
        }

        [Test]
        public void UiComposition_GameplayPauseAudioBridge_IsOnlyUiRuntimeBridgeForAudioPauseGroup()
        {
            var compositionSources = ReadRuntimeSources(new[] { "Assets/_Features/UI/UI_Composition/Runtime" });
            var bridgeSource = compositionSources.Single(source => source.RelativePath.EndsWith("GameplayPauseAudioBridge.cs", StringComparison.Ordinal));
            var sourcesReferencingPauseService = compositionSources
                .Where(source => source.Source.Contains("IAudioPlaybackPauseService"))
                .Select(source => source.RelativePath)
                .OrderBy(path => path)
                .ToArray();

            Assert.That(
                sourcesReferencingPauseService,
                Is.EqualTo(new[]
                {
                    "Assets/_Features/UI/UI_Composition/Runtime/GameplayPauseAudioBridge.cs",
                    "Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs",
                }));
            Assert.That(bridgeSource.Source, Does.Contain("PauseChanged"));
            Assert.That(bridgeSource.Source, Does.Contain("AudioPlaybackPauseGroup.GameplayPresentation"));
            Assert.That(bridgeSource.Source, Does.Contain("AudioPauseReason.GameplayPause"));
        }

        [Test]
        public void UiViewsAndViewModels_ConsumeOnlyUiReadModels()
        {
            var forbiddenTokens = new[]
            {
                "GameplaySceneHost",
                "WorldState",
                "TickResult",
                "TickPresentationData",
                "StageRuntimeBuildResult",
                "EntityState",
            };
            var sources = ReadRuntimeSources(new[]
                {
                    "Assets/_Features/UI/UI_HUD/Runtime",
                    "Assets/_Features/UI/UI_Screens/Runtime",
                    "Assets/_Features/UI/UI_Popups/Runtime",
                })
                .Where(source =>
                    source.RelativePath.EndsWith("View.cs", StringComparison.Ordinal) ||
                    source.RelativePath.EndsWith("ViewModel.cs", StringComparison.Ordinal))
                .ToArray();

            foreach (var source in sources)
            {
                foreach (var token in forbiddenTokens)
                {
                    Assert.That(source.Source, Does.Not.Contain(token), $"{source.RelativePath} contains {token}");
                }
            }
        }

        private static Assembly[] GetRuntimeUiAssemblies()
        {
            return new[]
            {
                typeof(HUDRootPresenter).Assembly,
                typeof(HUDController).Assembly,
                typeof(HUDRootView).Assembly,
                typeof(SettingsScreenView).Assembly,
                typeof(PausePopupView).Assembly,
                typeof(GameplayUiFlowInstaller).Assembly,
            }.Distinct().ToArray();
        }

        private static void AssertRuntimeSourcesDoNotContain(IEnumerable<string> rootRelativePaths, IEnumerable<string> forbiddenTokens)
        {
            foreach (var source in ReadRuntimeSources(rootRelativePaths))
            {
                foreach (var token in forbiddenTokens)
                {
                    Assert.That(source.Source, Does.Not.Contain(token), $"{source.RelativePath} contains {token}");
                }
            }
        }

        private static SourceFile[] ReadRuntimeSources(IEnumerable<string> rootRelativePaths)
        {
            var projectRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
            return rootRelativePaths
                .SelectMany(rootRelativePath =>
                {
                    var rootPath = Path.Combine(projectRoot, rootRelativePath);
                    return Directory.GetFiles(rootPath, "*.cs", SearchOption.AllDirectories)
                        .Select(path => new SourceFile(
                            ToProjectRelativePath(projectRoot, path),
                            File.ReadAllText(path)));
                })
                .OrderBy(source => source.RelativePath)
                .ToArray();
        }

        private static string ToProjectRelativePath(string projectRoot, string path)
        {
            var fullPath = Path.GetFullPath(path);
            var prefix = projectRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? projectRoot
                : projectRoot + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(prefix, StringComparison.Ordinal)
                ? fullPath.Substring(prefix.Length).Replace(Path.DirectorySeparatorChar, '/')
                : fullPath.Replace(Path.DirectorySeparatorChar, '/');
        }

        private readonly struct SourceFile
        {
            public SourceFile(string relativePath, string source)
            {
                RelativePath = relativePath;
                Source = source;
            }

            public string RelativePath { get; }

            public string Source { get; }
        }

        private static string[] GetConstructorSignatures(Type type)
        {
            return type.GetConstructors(BindingFlags.Instance | BindingFlags.Public)
                .Select(ctor => $"{type.Name}({string.Join(", ", ctor.GetParameters().Select(FormatParameterType))})")
                .OrderBy(signature => signature)
                .ToArray();
        }

        private static string[] GetPublicEventNames(Type type)
        {
            return type.GetEvents(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(evt => evt.Name)
                .OrderBy(name => name)
                .ToArray();
        }

        private static string[] GetPublicMethodSignatures(Type type)
        {
            return type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName)
                .Select(method => $"{method.Name}({string.Join(", ", method.GetParameters().Select(FormatParameterSignature))})")
                .OrderBy(signature => signature)
                .ToArray();
        }

        private static string[] GetPublicPropertyNames(Type type)
        {
            return type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();
        }

        private static IEnumerable<Type> GetPublicSurfaceTypes(Type type)
        {
            yield return type;

            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                foreach (var surfacedType in ExpandType(property.PropertyType))
                {
                    yield return surfacedType;
                }
            }

            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName)
                {
                    continue;
                }

                foreach (var surfacedType in ExpandType(method.ReturnType))
                {
                    yield return surfacedType;
                }

                foreach (var parameter in method.GetParameters())
                {
                    foreach (var surfacedType in ExpandType(parameter.ParameterType))
                    {
                        yield return surfacedType;
                    }
                }
            }
        }

        private static void AssertViewBindSignature(Type viewType, Type expectedViewModelType)
        {
            var bindMethods = viewType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method.Name == "Bind")
                .ToArray();

            Assert.That(bindMethods, Has.Length.EqualTo(1), viewType.FullName);
            Assert.That(bindMethods[0].GetParameters().Select(parameter => parameter.ParameterType).ToArray(), Is.EqualTo(new[] { expectedViewModelType }));
            Assert.That(TypeDependsOn(viewType, typeof(UIPresentationSnapshot)), Is.False, viewType.FullName);
        }

        private static IEnumerable<Type> GetPublicMemberTypes(Type type)
        {
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                foreach (var surfacedType in ExpandType(property.PropertyType))
                {
                    yield return surfacedType;
                }
            }

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                foreach (var surfacedType in ExpandType(field.FieldType))
                {
                    yield return surfacedType;
                }
            }
        }

        private static IEnumerable<Type> ExpandType(Type type)
        {
            var normalizedType = NormalizeType(type);
            if (normalizedType == null)
            {
                yield break;
            }

            yield return normalizedType;

            if (!normalizedType.IsGenericType)
            {
                yield break;
            }

            foreach (var argument in normalizedType.GetGenericArguments())
            {
                foreach (var surfacedType in ExpandType(argument))
                {
                    yield return surfacedType;
                }
            }
        }

        private static Type NormalizeType(Type type)
        {
            if (type == null)
            {
                return null;
            }

            if (type.IsByRef)
            {
                type = type.GetElementType();
            }

            if (type.IsArray)
            {
                type = type.GetElementType();
            }

            return Nullable.GetUnderlyingType(type) ?? type;
        }

        private static string FormatParameterSignature(ParameterInfo parameter)
        {
            var prefix = parameter.IsOut ? "out " : parameter.ParameterType.IsByRef ? "ref " : string.Empty;
            return prefix + FormatParameterType(parameter);
        }

        private static string FormatParameterType(ParameterInfo parameter)
        {
            return FormatTypeName(parameter.ParameterType);
        }

        private static string FormatTypeName(Type type)
        {
            var normalizedType = NormalizeType(type);
            if (normalizedType == null)
            {
                return "Void";
            }

            if (!normalizedType.IsGenericType)
            {
                return normalizedType.Name;
            }

            var genericTypeName = normalizedType.Name;
            var tickIndex = genericTypeName.IndexOf('`');
            if (tickIndex >= 0)
            {
                genericTypeName = genericTypeName.Substring(0, tickIndex);
            }

            return $"{genericTypeName}<{string.Join(", ", normalizedType.GetGenericArguments().Select(FormatTypeName))}>";
        }

        private static bool TypeDependsOn(Type type, Type dependencyType)
        {
            if (type == null || dependencyType == null)
            {
                return false;
            }

            return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                       .Any(field => NormalizeType(field.FieldType) == dependencyType) ||
                   type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                       .Any(property => NormalizeType(property.PropertyType) == dependencyType) ||
                   type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                       .Any(ctor => ctor.GetParameters().Any(parameter => NormalizeType(parameter.ParameterType) == dependencyType)) ||
                   type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                       .Any(method =>
                           NormalizeType(method.ReturnType) == dependencyType ||
                           method.GetParameters().Any(parameter => NormalizeType(parameter.ParameterType) == dependencyType));
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        }
    }
}
